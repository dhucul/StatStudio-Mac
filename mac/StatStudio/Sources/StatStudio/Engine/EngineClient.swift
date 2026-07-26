import Foundation

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
/// Requests are serialized through the actor, so responses are read back in order.
actor EngineClient {
    private var process: Process?
    private var inWrite: FileHandle?
    private var outRead: FileHandle?
    private var buffer = Data()
    private var counter = 0
    private let responseTimeout: TimeInterval

    init(responseTimeout: TimeInterval = 30) {
        self.responseTimeout = responseTimeout
    }

    /// Resolve the helper: explicit native exe, dev DLL via dotnet, or the app bundle.
    private func startIfNeeded() throws {
        if process?.isRunning == true { return }
        resetConnection()
        let p = Process()
        let env = ProcessInfo.processInfo.environment

        if let exe = env["STATSTUDIO_ENGINE"] {
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
                try writer.write(contentsOf: data)
                let line = try await readLine()
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

    private func readLine() async throws -> Data {
        while true {
            if let nl = buffer.firstIndex(of: 0x0a) {
                let line = buffer.subdata(in: buffer.startIndex..<nl)
                buffer.removeSubrange(buffer.startIndex...nl)
                if line.isEmpty { continue }
                return line
            }
            guard let reader = outRead else { throw EngineError.disconnected }
            let timeout = responseTimeout
            let chunk = try await withCheckedThrowingContinuation {
                (continuation: CheckedContinuation<Data, Error>) in
                let completion = ReadCompletion(continuation)
                DispatchQueue.global(qos: .userInitiated).async {
                    completion.resume(returning: reader.availableData)
                }
                DispatchQueue.global(qos: .userInitiated).asyncAfter(deadline: .now() + timeout) {
                    completion.resume(throwing: EngineError.timeout)
                }
            }
            if chunk.isEmpty { throw EngineError.eof }
            buffer.append(chunk)
        }
    }

    private func resetConnection() {
        try? inWrite?.close()
        try? outRead?.close()
        if process?.isRunning == true { process?.terminate() }
        process = nil
        inWrite = nil
        outRead = nil
        buffer.removeAll(keepingCapacity: true)
    }
}
