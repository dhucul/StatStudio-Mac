import Foundation
import XCTest
@testable import StatStudio

final class EngineClientTests: XCTestCase {
    func testProtocolRoundTripPreservesRequestIdentity() throws {
        let request = EngineRequest(id: 42, op: "samples.list", worksheet: nil, params: nil)
        let data = try JSONEncoder().encode(request)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(object["id"] as? Int, 42)
        XCTAssertEqual(object["op"] as? String, "samples.list")
    }

    func testClientRestartsAfterUnexpectedEOF() async throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("statstudio-engine-test-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let marker = root.appendingPathComponent("started").path
        let script = root.appendingPathComponent("fake-engine.sh")
        let body = """
        #!/bin/sh
        if [ ! -f '\(marker)' ]; then
          : > '\(marker)'
          exit 0
        fi
        IFS= read -r request
        printf '%s\\n' '{"id":1,"ok":true,"samples":[]}'
        """
        try body.write(to: script, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: script.path)

        let oldEngine = ProcessInfo.processInfo.environment["STATSTUDIO_ENGINE"]
        setenv("STATSTUDIO_ENGINE", script.path, 1)
        defer {
            if let oldEngine { setenv("STATSTUDIO_ENGINE", oldEngine, 1) }
            else { unsetenv("STATSTUDIO_ENGINE") }
        }

        let response = try await EngineClient(responseTimeout: 2).send(op: "samples.list")
        XCTAssertTrue(response.ok)
        XCTAssertEqual(response.id, 1)
    }

    func testClientRejectsMismatchedResponseIdentity() async throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("statstudio-engine-test-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let script = root.appendingPathComponent("fake-engine.sh")
        let body = """
        #!/bin/sh
        IFS= read -r request
        printf '%s\\n' '{"id":999,"ok":true}'
        """
        try body.write(to: script, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: script.path)

        let oldEngine = ProcessInfo.processInfo.environment["STATSTUDIO_ENGINE"]
        setenv("STATSTUDIO_ENGINE", script.path, 1)
        defer {
            if let oldEngine { setenv("STATSTUDIO_ENGINE", oldEngine, 1) }
            else { unsetenv("STATSTUDIO_ENGINE") }
        }

        do {
            _ = try await EngineClient(responseTimeout: 2).send(op: "samples.list")
            XCTFail("Expected a response identity mismatch")
        } catch EngineError.responseMismatch(let expected, let actual) {
            XCTAssertEqual(expected, 1)
            XCTAssertEqual(actual, 999)
        }
    }

    func testClientTimesOutInsteadOfHangingForever() async throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("statstudio-engine-test-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let script = root.appendingPathComponent("fake-engine.sh")
        let body = """
        #!/bin/sh
        IFS= read -r request
        exec /bin/sleep 5
        """
        try body.write(to: script, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: script.path)

        let oldEngine = ProcessInfo.processInfo.environment["STATSTUDIO_ENGINE"]
        setenv("STATSTUDIO_ENGINE", script.path, 1)
        defer {
            if let oldEngine { setenv("STATSTUDIO_ENGINE", oldEngine, 1) }
            else { unsetenv("STATSTUDIO_ENGINE") }
        }

        do {
            _ = try await EngineClient(responseTimeout: 0.05).send(op: "samples.list")
            XCTFail("Expected the response to time out")
        } catch EngineError.timeout {
            // Expected after the one safe retry.
        }
    }

    /// The timeout budgets the whole response, not each chunk. An engine that dribbles a
    /// fragment just inside the interval would otherwise hold the connection open forever.
    func testSlowDribbleTripsTheOverallDeadline() async throws {
        let root = FileManager.default.temporaryDirectory
            .appendingPathComponent("statstudio-engine-test-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let script = root.appendingPathComponent("fake-engine.sh")
        // Each fragment lands well inside the 0.6 s timeout, but the complete line takes
        // ~1.2 s — so only a per-response deadline rejects it.
        let body = """
        #!/bin/sh
        IFS= read -r request
        printf '{'
        sleep 0.3
        printf '"id":1,'
        sleep 0.3
        printf '"ok":true'
        sleep 0.3
        printf '}\\n'
        """
        try body.write(to: script, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o755], ofItemAtPath: script.path)

        let oldEngine = ProcessInfo.processInfo.environment["STATSTUDIO_ENGINE"]
        setenv("STATSTUDIO_ENGINE", script.path, 1)
        defer {
            if let oldEngine { setenv("STATSTUDIO_ENGINE", oldEngine, 1) }
            else { unsetenv("STATSTUDIO_ENGINE") }
        }

        do {
            _ = try await EngineClient(responseTimeout: 0.6).send(op: "samples.list")
            XCTFail("Expected the dribbled response to time out")
        } catch EngineError.timeout {
            // Expected: the deadline covers the whole response.
        }
    }
}
