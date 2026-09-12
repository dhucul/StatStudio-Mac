import Foundation
import Darwin

enum EngineError: Error, CustomStringConvertible {
    case notFound
    case eof
    case timeout
    case disconnected
    case responseMismatch(expected: Int, actual: Int)
    var description: String {
        switch self {
        case .notFound: return "engine helper not found (set STATSTUDIO_ENGINE or STATSTUDIO_ENGINE_DLL, or bundle it under Resources/Engine)"
        case .eof: return "engine closed the connection"
        case .timeout: return "engine response timed out"
        case .disconnected: return "engine connection is unavailable"
        case .responseMismatch(let expected, let actual):
            return "engine response ID mismatch (expected \(expected), received \(actual))"
        }
    }
}

private final class ReadCompletion: @unchecked Sendable {
    private let lock = NSLock()
    private var continuation: CheckedContinuation<Data, Error>?

    init(_ continuation: CheckedContinuation<Data, Error>) {
        self.continuation = continuation
    }

    func resume(returning data: Data) {
        lock.lock()
        let pending = continuation
        continuation = nil
        lock.unlock()
        pending?.resume(returning: data)
    }

    func resume(throwing error: Error) {
        lock.lock()
        let pending = continuation
        continuation = nil
        lock.unlock()
        pending?.resume(throwing: error)
    }
}

/// Owns the bundled .NET helper process and speaks newline-delimited JSON to it.
/// A transaction gate remains held across suspension, keeping each response with its request.
actor EngineClient {
    private var process: Process?
    private var inWrite: FileHandle?
    private var outRead: FileHandle?
    private var buffer = Data()
    private var counter = 0
    private let responseTimeout: TimeInterval
    private let executableURL: URL?
    private var transactionActive = false
    private var transactionWaiters: [CheckedContinuation<Void, Never>] = []

    init(responseTimeout: TimeInterval = 30, executableURL: URL? = nil) {
        self.responseTimeout = responseTimeout
        self.executableURL = executableURL
    }

    private func acquireTransaction() async {
        if !transactionActive { transactionActive = true; return }
        await withCheckedContinuation { transactionWaiters.append($0) }
    }

    private func releaseTransaction() {
        if transactionWaiters.isEmpty { transactionActive = false }
        else { transactionWaiters.removeFirst().resume() }
    }

    func shutdown() async {
        await acquireTransaction()
        defer { releaseTransaction() }
        resetConnection()
    }

    /// Resolve the helper: explicit native exe, dev DLL via dotnet, or the app bundle.
    private func startIfNeeded() throws {
        if process?.isRunning == true { return }
        resetConnection()
        let p = Process()
        let env = ProcessInfo.processInfo.environment

        if let executableURL {
            p.executableURL = executableURL
            p.arguments = []
        } else if let exe = env["STATSTUDIO_ENGINE"] {
            p.executableURL = URL(fileURLWithPath: exe)
            p.arguments = []
        } else if let dll = env["STATSTUDIO_ENGINE_DLL"] {
            p.executableURL = URL(fileURLWithPath: env["DOTNET"] ?? "/usr/bin/env")
            p.arguments = (env["DOTNET"] != nil) ? [dll] : ["dotnet", dll]
        } else if let url = Bundle.main.url(forResource: "StatStudio.Engine",
                                            withExtension: nil, subdirectory: "Engine") {
            p.executableURL = url
            p.arguments = []
        } else {
            throw EngineError.notFound
        }

        let inPipe = Pipe(), outPipe = Pipe()
        guard fcntl(inPipe.fileHandleForWriting.fileDescriptor, F_SETNOSIGPIPE, 1) != -1 else {
            throw POSIXError(POSIXErrorCode(rawValue: errno) ?? .EIO)
        }
        p.standardInput = inPipe
        p.standardOutput = outPipe
        // stderr inherited so engine crash traces surface in the console.
        try p.run()

        process = p
        inWrite = inPipe.fileHandleForWriting
        outRead = outPipe.fileHandleForReading
    }

    func send(op: String, worksheet: WorksheetDTO? = nil,
              params: [String: JSONValue] = [:]) async throws -> EngineResponse {
        await acquireTransaction()
        defer { releaseTransaction() }
        try Task.checkCancellation()
        counter += 1
        let req = EngineRequest(id: counter, op: op, worksheet: worksheet,
                                params: params.isEmpty ? nil : params)
        var data = try JSONEncoder().encode(req)
        data.append(0x0a)
        let mayRetry = op != "export" && op != "project.save"
        for attempt in 0...1 {
            do {
                try startIfNeeded()
                guard let writer = inWrite else { throw EngineError.disconnected }
                let deadline = Date().addingTimeInterval(responseTimeout)
                try await writeRequest(data, to: writer, deadline: deadline)
                let line = try await readLine(deadline: deadline)
                let response = try JSONDecoder().decode(EngineResponse.self, from: line)
                guard response.id == req.id else {
                    throw EngineError.responseMismatch(expected: req.id, actual: response.id)
                }
                return response
            } catch {
                resetConnection()
                if attempt == 0 && mayRetry { continue }
                throw error
            }
        }
        throw EngineError.disconnected
    }

    private func writeRequest(_ data: Data, to writer: FileHandle, deadline: Date) async throws {
        let _: Data = try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Data, Error>) in
            let completion = ReadCompletion(continuation)
            DispatchQueue.global(qos: .userInitiated).async {
                do { try writer.write(contentsOf: data); completion.resume(returning: Data()) }
                catch { completion.resume(throwing: error) }
            }
            DispatchQueue.global(qos: .userInitiated).asyncAfter(deadline: .now() + max(0, deadline.timeIntervalSinceNow)) {
                completion.resume(throwing: EngineError.timeout)
            }
        }
    }

    private func readLine(deadline: Date) async throws -> Data {
        // One deadline for the whole response. Re-arming the timeout per chunk lets an
        // engine that dribbles a byte at a time hold the connection open forever.
        while true {
            if let nl = buffer.firstIndex(of: 0x0a) {
                let line = buffer.subdata(in: buffer.startIndex..<nl)
                buffer.removeSubrange(buffer.startIndex...nl)
                if line.isEmpty { continue }
                return line
            }
            guard let reader = outRead else { throw EngineError.disconnected }
            let remaining = deadline.timeIntervalSinceNow
            guard remaining > 0 else { throw EngineError.timeout }
            let chunk = try await withCheckedThrowingContinuation {
                (continuation: CheckedContinuation<Data, Error>) in
                let completion = ReadCompletion(continuation)
                // A readability handler parks no thread in a blocking read, so a timed-out
                // request cannot strand one on a descriptor we are about to release — and
                // cannot have its orphaned read steal bytes from the next pipe if the fd
                // number gets recycled. Detaching is left to the handler itself and to
                // resetConnection(), both of which run without racing this closure.
                reader.readabilityHandler = { handle in
                    handle.readabilityHandler = nil
                    completion.resume(returning: handle.availableData)
                }
                DispatchQueue.global(qos: .userInitiated).asyncAfter(deadline: .now() + remaining) {
                    completion.resume(throwing: EngineError.timeout)
                }
            }
            if chunk.isEmpty { throw EngineError.eof }
            buffer.append(chunk)
        }
    }

    private func resetConnection() {
        // Detach first: a handler firing against a closed handle raises an Objective-C
        // exception, which Swift cannot catch and which would take the app down.
        outRead?.readabilityHandler = nil
        // The worker owns the handle until its write finishes; never close/reuse
        // its descriptor while that write may still be in progress.
        if process?.isRunning == true { process?.terminate() }
        process = nil
        inWrite = nil
        // Deliberately not closed here: an in-flight handler still holds this handle, so
        // releasing our reference lets it close on deinit once nothing is using it.
        outRead = nil
        buffer.removeAll(keepingCapacity: true)
    }
}
