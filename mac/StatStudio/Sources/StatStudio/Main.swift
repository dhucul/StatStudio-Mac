import Foundation

// Entry point. `--selftest` exercises the Swift↔engine IPC round-trip headlessly (no GUI)
// and exits — useful for CI and for verifying the bridge without a window. Otherwise the
// SwiftUI app launches normally.
@main
enum Main {
    static func main() {
        if CommandLine.arguments.contains("--selftest") {
            SelfTest.run()
            exit(0)
        }
        StatStudioApp.main()
    }
}

private enum SelfTest {
    static func run() {
        let engine = EngineClient()
        let sem = DispatchSemaphore(value: 0)
        Task { await runAsync(engine); sem.signal() }
        sem.wait()
    }

    static func runAsync(_ engine: EngineClient) async {
        var passed = 0, failed = 0
        func check(_ label: String, _ body: () async throws -> Bool) async {
            do {
                let ok = try await body()
                print((ok ? "  PASS  " : "  FAIL  ") + label)
                ok ? (passed += 1) : (failed += 1)
            } catch {
                print("  FAIL  \(label) — \(error)"); failed += 1
            }
        }

        print("== StatStudio Swift↔engine selftest ==")
        let demo = DemoData.build().toDTO()

        await check("descriptives returns a table") {
            let r = try await engine.send(op: "descriptives", worksheet: demo,
                                          params: ["columns": .strings(["Height", "Weight"])])
            return r.ok && (r.sessionText?.contains("Mean") ?? false)
        }
        await check("samples.list returns datasets") {
            let r = try await engine.send(op: "samples.list")
            return r.ok && (r.samples?.count ?? 0) > 0
        }
        await check("samples.load returns a worksheet") {
            let list = try await engine.send(op: "samples.list")
            let name = list.samples?.first?.name ?? ""
            let r = try await engine.send(op: "samples.load", params: ["name": .string(name)])
            return r.ok && (r.worksheet?.columns.isEmpty == false)
        }

        let tmpCsv = NSTemporaryDirectory() + "statstudio_selftest.csv"
        let tmpXlsx = NSTemporaryDirectory() + "statstudio_selftest.xlsx"
        try? "A,B\n1,10\n2,20\n3,30\n".write(toFile: tmpCsv, atomically: true, encoding: .utf8)

        await check("import reads a CSV") {
            let r = try await engine.send(op: "import", params: ["path": .string(tmpCsv)])
            return r.ok && r.worksheet?.columns.count == 2
        }
        await check("export → re-import (xlsx) round-trips") {
            let imp = try await engine.send(op: "import", params: ["path": .string(tmpCsv)])
            let exp = try await engine.send(op: "export", worksheet: imp.worksheet,
                                            params: ["path": .string(tmpXlsx)])
            guard exp.ok else { return false }
            let re = try await engine.send(op: "import", params: ["path": .string(tmpXlsx)])
            return re.ok && re.worksheet?.columns.count == 2
        }
        await check("graph.histogram returns a valid PNG") {
            let r = try await engine.send(op: "graph.histogram", worksheet: demo,
                                          params: ["column": .string("Height")])
            guard let b64 = r.graphs?.first?.png, let data = Data(base64Encoded: b64) else { return false }
            return r.ok && data.count > 1000 &&
                data.prefix(8) == Data([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        }

        print("\(passed) passed, \(failed) failed.")
    }
}
