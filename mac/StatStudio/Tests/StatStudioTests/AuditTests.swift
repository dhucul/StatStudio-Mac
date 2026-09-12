import AppKit
import Foundation
import XCTest
@testable import StatStudio

final class AuditTests: XCTestCase {
    private func helper(_ body: (URL) -> String, timeout: TimeInterval = 2) throws -> (URL, EngineClient) {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("statstudio-audit-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let script = root.appendingPathComponent("engine.sh")
        try ("#!/bin/sh\n" + body(root)).write(to: script, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: script.path)
        return (root, EngineClient(responseTimeout: timeout, executableURL: script))
    }

    private func waitForFile(_ url: URL) async throws {
        for _ in 0..<200 {
            if FileManager.default.fileExists(atPath: url.path) { return }
            try await Task.sleep(nanoseconds: 5_000_000)
        }
        throw NSError(domain: "AuditTest", code: 1, userInfo: [NSLocalizedDescriptionKey: "Helper did not receive a request"])
    }

    func testOverlappingStartupAndSaveStayOnOneConnection() async throws {
        let (root, client) = try helper { root in """
        echo started >> '\(root.path)/starts'
        IFS= read -r first
        touch '\(root.path)/received'
        sleep 0.1
        printf '%s\\n' '{"id":1,"ok":true,"samples":[]}'
        IFS= read -r second
        printf '%s\\n' '{"id":2,"ok":true}'
        """ }
        defer { try? FileManager.default.removeItem(at: root) }
        async let startup = client.send(op: "samples.list")
        try await waitForFile(root.appendingPathComponent("received"))
        async let save = client.send(op: "export")
        let results = try await (startup, save)
        XCTAssertEqual(results.0.id, 1)
        XCTAssertEqual(results.1.id, 2)
        XCTAssertEqual(try String(contentsOf: root.appendingPathComponent("starts"), encoding: .utf8), "started\n")
        await client.shutdown()
    }

    func testCancelledWaiterDoesNotWriteAndReleasesGate() async throws {
        let (root, client) = try helper { root in """
        IFS= read -r first
        touch '\(root.path)/received'
        while [ ! -f '\(root.path)/release' ]; do sleep 0.01; done
        printf '%s\\n' '{"id":1,"ok":true}'
        IFS= read -r second
        printf '%s\\n' "$second" > '\(root.path)/second'
        printf '%s\\n' '{"id":2,"ok":true}'
        """ }
        defer { try? FileManager.default.removeItem(at: root) }
        let active = Task { try await client.send(op: "ping") }
        try await waitForFile(root.appendingPathComponent("received"))
        let cancelled = Task { try await client.send(op: "export") }
        await Task.yield()
        cancelled.cancel()
        try Data().write(to: root.appendingPathComponent("release"))
        _ = try await active.value
        do { _ = try await cancelled.value; XCTFail("Cancelled request must not be sent") }
        catch is CancellationError { }
        _ = try await client.send(op: "ping")
        let request = try String(contentsOf: root.appendingPathComponent("second"), encoding: .utf8)
        XCTAssertFalse(request.contains("export"))
        await client.shutdown()
    }

    func testAHelperThatDoesNotReadCannotBlockTheClient() async throws {
        let (root, client) = try helper({ _ in "exec /bin/sleep 5" }, timeout: 0.05)
        defer { try? FileManager.default.removeItem(at: root) }
        let worksheet = WorksheetDTO(name: "Large", columns: [ColumnDTO(name: "A", cells: [String(repeating: "1", count: 1_000_000)])])
        let start = Date()
        do { _ = try await client.send(op: "export", worksheet: worksheet); XCTFail("Expected a write timeout") }
        catch EngineError.timeout { }
        XCTAssertLessThan(Date().timeIntervalSince(start), 1)
        await client.shutdown()
    }

    func testNumericSyntaxMatchesCore() {
        XCTAssertEqual(NumericCell.parse(" 1,234.5e-2 "), 12.345)
        XCTAssertEqual(NumericCell.parse("-.5"), -0.5)
        XCTAssertEqual(NumericCell.parse("1,000"), 1000)
        for invalid in ["Infinity", "NaN", "1e999", "1.2,3", "abc"] { XCTAssertNil(NumericCell.parse(invalid)) }
        let worksheet = WorksheetModel(name: "Imported", columnNames: ["X"], rows: [["1,000"], ["2,000"]])
        XCTAssertTrue(worksheet.looksNumeric(column: 0))
    }

    func testProjectTypesSurviveSwiftRoundTrip() throws {
        let source = WorksheetDTO(name: "IDs", columns: [ColumnDTO(name: "ID", cells: ["00123"], type: "Text")])
        let model = WorksheetModel.from(source)
        let roundTrip = try JSONDecoder().decode(WorksheetDTO.self, from: JSONEncoder().encode(model.toDTO()))
        XCTAssertEqual(roundTrip.columns[0].type, "Text")
        XCTAssertEqual(roundTrip.columns[0].cells[0], "00123")
    }

    @MainActor
    func testWorksheetConflictSuppressesSuccessAndCancelsDependents() async throws {
        let (root, client) = try helper { root in """
        IFS= read -r request
        touch '\(root.path)/received'
        while [ ! -f '\(root.path)/release' ]; do sleep 0.01; done
        printf '%s\\n' '{"id":1,"ok":true,"worksheet":{"name":"Calculated","columns":[{"name":"A","cells":["99"]}]},"sessionText":"UNAPPLIED SUCCESS","statusTitle":"Calculated"}'
        if IFS= read -r request; then touch '\(root.path)/unexpected'; fi
        """ }
        defer { try? FileManager.default.removeItem(at: root) }
        let model = AppModel(engine: client, loadSamplesOnInit: false)
        model.finishWorksheetEditing = {}
        model.setWorksheet(WorksheetModel(name: "Original", columnNames: ["A"], rows: [["1"]]))
        let first = Task { await model.run(op: "calc.evaluate") }
        try await waitForFile(root.appendingPathComponent("received"))
        let second = Task { await model.run(op: "export") }
        await Task.yield()
        model.worksheet.rows[0][0] = "edited"
        model.worksheetDidEdit()
        try Data().write(to: root.appendingPathComponent("release"))
        await first.value
        await second.value
        XCTAssertEqual(model.worksheet.rows[0][0], "edited")
        XCTAssertEqual(model.statusText, "Worksheet conflict")
        XCTAssertFalse(model.sessionText.contains("UNAPPLIED SUCCESS"))
        XCTAssertFalse(FileManager.default.fileExists(atPath: root.appendingPathComponent("unexpected").path))
        await client.shutdown()
    }

    @MainActor
    func testSnapshotFinishesCellEditingBeforeSending() async throws {
        let (root, client) = try helper { root in """
        IFS= read -r request
        printf '%s\\n' "$request" > '\(root.path)/request'
        printf '%s\\n' '{"id":1,"ok":true}'
        """ }
        defer { try? FileManager.default.removeItem(at: root) }
        let model = AppModel(engine: client, loadSamplesOnInit: false)
        model.worksheet = WorksheetModel(name: "W", columnNames: ["A"], rows: [["old"]])
        model.finishWorksheetEditing = { model.worksheet.rows[0][0] = "committed"; model.worksheetDidEdit() }
        await model.run(op: "export")
        let request = try String(contentsOf: root.appendingPathComponent("request"), encoding: .utf8)
        XCTAssertTrue(request.contains("committed"))
        model.finishWorksheetEditing = {}
        await client.shutdown()
    }

    @MainActor
    func testLateEditorCommitCannotOverwriteReplacementWorksheet() async {
        let model = AppModel(loadSamplesOnInit: false)
        model.finishWorksheetEditing = {}
        model.setWorksheet(WorksheetModel(name: "Old", columnNames: ["A"], rows: [["1"]]))
        let coordinator = WorksheetGridView.Coordinator(model)
        let oldField = NSTextField(string: "stale")
        coordinator.beginCellEdit(oldField, row: 0, col: 0)
        model.setWorksheet(WorksheetModel(name: "New", columnNames: ["A"], rows: [["2"]]))
        let newField = NSTextField(string: "3")
        coordinator.beginCellEdit(newField, row: 0, col: 0)
        coordinator.finishCellEdit(oldField)
        XCTAssertEqual(model.worksheet.rows[0][0], "2")
        coordinator.finishCellEdit(newField)
        XCTAssertEqual(model.worksheet.rows[0][0], "3")
    }
}
