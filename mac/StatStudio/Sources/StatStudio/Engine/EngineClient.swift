import Foundation

enum EngineError: Error, CustomStringConvertible {
    case notFound
    case eof
    var description: String {
        switch self {
        case .notFound: return "engine helper not found (set STATSTUDIO_ENGINE or STATSTUDIO_ENGINE_DLL, or bundle it under Resources/Engine)"
        case .eof: return "engine closed the connection"
        }
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

    /// Resolve the helper: explicit native exe, dev DLL via dotnet, or the app bundle.
    private func startIfNeeded() throws {
        if process != nil { return }
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
        try startIfNeeded()
        counter += 1
        let req = EngineRequest(id: counter, op: op, worksheet: worksheet,
                                params: params.isEmpty ? nil : params)
        var data = try JSONEncoder().encode(req)
        data.append(0x0a)
        try inWrite!.write(contentsOf: data)
        let line = try readLine()
        return try JSONDecoder().decode(EngineResponse.self, from: line)
    }

    private func readLine() throws -> Data {
        while true {
            if let nl = buffer.firstIndex(of: 0x0a) {
                let line = buffer.subdata(in: buffer.startIndex..<nl)
                buffer.removeSubrange(buffer.startIndex...nl)
                if line.isEmpty { continue }
                return line
            }
            let chunk = outRead!.availableData
            if chunk.isEmpty { throw EngineError.eof }
            buffer.append(chunk)
        }
    }
}
