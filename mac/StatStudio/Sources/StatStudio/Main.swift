import Foundation

// Entry point. `--selftest` exercises the Swift↔engine IPC round-trip headlessly (no GUI)
// and exits — useful for CI and for verifying the bridge without a window. Otherwise the
// SwiftUI app launches normally.
@main
enum Main {
    static func main() {
        if CommandLine.arguments.contains("--selftest") {
            exit(SelfTest.run() == 0 ? 0 : 1)
        }
        StatStudioApp.main()
    }
}

private enum SelfTest {
    static func run() -> Int {
        let engine = EngineClient()
        let sem = DispatchSemaphore(value: 0)
        var failures = 1
        Task { failures = await runAsync(engine); sem.signal() }
        sem.wait()
        return failures
    }

    static func runAsync(_ engine: EngineClient) async -> Int {
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

        let tempRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("statstudio-selftest-\(UUID().uuidString)", isDirectory: true)
        try? FileManager.default.createDirectory(at: tempRoot, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempRoot) }
        let tmpCsv = tempRoot.appendingPathComponent("input.csv").path
        let tmpXlsx = tempRoot.appendingPathComponent("roundtrip.xlsx").path
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
        await check("multiline CSV round-trips") {
            let path = tempRoot.appendingPathComponent("multiline.csv").path
            let source = WorksheetDTO(name: "Multiline", columns: [
                ColumnDTO(name: "A", cells: ["line1\nline2"]),
                ColumnDTO(name: "B", cells: ["x,y"]),
            ])
            let exp = try await engine.send(op: "export", worksheet: source,
                                            params: ["path": .string(path)])
            guard exp.ok else { return false }
            let re = try await engine.send(op: "import", params: ["path": .string(path)])
            return re.ok && re.worksheet?.columns.first?.cells.first == "line1\nline2"
        }
        await check(".tsv export uses tabs") {
            let path = tempRoot.appendingPathComponent("output.tsv").path
            let source = WorksheetDTO(name: "TSV", columns: [
                ColumnDTO(name: "A", cells: ["1"]),
                ColumnDTO(name: "B", cells: ["2"]),
            ])
            let exp = try await engine.send(op: "export", worksheet: source,
                                            params: ["path": .string(path)])
            let text = try String(contentsOfFile: path, encoding: .utf8)
            return exp.ok && text.hasPrefix("A\tB\n")
        }
        await check("invalid proportion is rejected") {
            let r = try await engine.send(op: "prop.one",
                params: ["events1": .int(1), "trials1": .int(0), "p0": .double(0.5)])
            return !r.ok && (r.error?.contains("trials1") ?? false)
        }
        await check("P chart retains worksheet row alignment") {
            let source = WorksheetDTO(name: "SPC", columns: [
                ColumnDTO(name: "Counts", cells: ["1", nil, "9"]),
                ColumnDTO(name: "Sizes", cells: ["10", "100", "10"]),
            ])
            let r = try await engine.send(op: "spc.p", worksheet: source,
                params: ["counts": .string("Counts"), "sizes": .string("Sizes")])
            return r.ok && (r.sessionText?.contains("0.5000") ?? false)
        }
        await check("graph.histogram returns a valid PNG") {
            let r = try await engine.send(op: "graph.histogram", worksheet: demo,
                                          params: ["column": .string("Height")])
            guard let b64 = r.graphs?.first?.png, let data = Data(base64Encoded: b64) else { return false }
            return r.ok && data.count > 1000 &&
                data.prefix(8) == Data([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
        }

        print("\(passed) passed, \(failed) failed.")
        return failed
    }
}
