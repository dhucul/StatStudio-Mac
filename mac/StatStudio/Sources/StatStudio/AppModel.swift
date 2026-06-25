import SwiftUI
import AppKit
import UniformTypeIdentifiers

/// Central UI state: the worksheet, the Session log, and the engine client.
@MainActor
final class AppModel: ObservableObject {
    @Published var worksheet = WorksheetModel.empty()
    @Published var sessionText = ""
    @Published var statusText = "Ready"
    @Published var samples: [SampleInfo] = []

    /// The analysis dialog currently being presented (nil = none).
    @Published var pendingSpec: AnalysisSpec?

    /// Bumped only on *structural* worksheet replacement (new/load/import/project), so the
    /// grid reloads then — but NOT on individual cell edits (which would interrupt typing).
    @Published var gridGeneration = 0

    let engine = EngineClient()

    init() {
        log("StatStudio — ready.")
        log("Open a CSV (File ▸ Open Data) or type into the worksheet, then run an analysis from the Stat or Graph menu.")
        log("")
        Task { await loadSamples() }
    }

    var dims: String {
        "\(worksheet.columnNames.count) cols × \(worksheet.rows.count) rows"
    }

    /// Column names + numeric flag, for populating dialog pickers.
    var columnsInfo: [(name: String, numeric: Bool)] {
        worksheet.columnNames.enumerated().map { (j, name) in
            (name, worksheet.looksNumeric(column: j))
        }
    }

    /// Present an analysis dialog; the sheet collects params and calls `run`.
    func present(_ spec: AnalysisSpec) { pendingSpec = spec }

    // ---- Session output (mirrors MainWindow.Log/Output/OutputRaw) ----------

    func log(_ text: String) { sessionText += text + "\n" }

    func append(_ block: String?) {
        guard let block, !block.isEmpty else { return }
        sessionText += block
    }

    func showError(_ title: String, _ message: String) {
        log("ERROR — \(title): \(message)")
        statusText = title
    }

    // ---- worksheet plumbing -----------------------------------------------

    /// Structural replace: swap the worksheet and tell the grid to rebuild.
    func setWorksheet(_ ws: WorksheetModel) {
        worksheet = ws
        gridGeneration += 1
    }

    func newWorksheet() {
        setWorksheet(.empty())
        statusText = "New worksheet"
    }

    func loadDemo() {
        setWorksheet(DemoData.build())
        log("Loaded demo dataset (Height, Weight, Group).")
        statusText = "Demo loaded"
    }

    func loadSamples() async {
        do {
            let res = try await engine.send(op: "samples.list")
            if let s = res.samples { samples = s }
        } catch {
            // Non-fatal: the Sample Data submenu just stays minimal.
        }
    }

    func loadSample(name: String) async {
        await run(op: "samples.load", params: ["name": .string(name)])
    }

    // ---- native file open / save ------------------------------------------

    func openData() {
        let panel = NSOpenPanel()
        panel.title = "Open Data"
        panel.canChooseFiles = true
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = uttypes(["csv", "tsv", "txt", "xlsx"])
        if panel.runModal() == .OK, let url = panel.url {
            Task { await run(op: "import", params: ["path": .string(url.path)]) }
        }
    }

    func saveData() {
        let panel = NSSavePanel()
        panel.title = "Save Worksheet As"
        panel.nameFieldStringValue = worksheet.name + ".csv"
        panel.allowedContentTypes = uttypes(["csv", "tsv", "xlsx"])
        if panel.runModal() == .OK, let url = panel.url {
            Task { await run(op: "export", params: ["path": .string(url.path)]) }
        }
    }

    func openProject() {
        let panel = NSOpenPanel()
        panel.title = "Open Project"
        panel.canChooseFiles = true
        panel.allowsMultipleSelection = false
        panel.allowedContentTypes = uttypes(["ssproj"])
        if panel.runModal() == .OK, let url = panel.url {
            Task { await run(op: "project.load", params: ["path": .string(url.path)]) }
        }
    }

    func saveProject() {
        let panel = NSSavePanel()
        panel.title = "Save Project As"
        panel.nameFieldStringValue = worksheet.name + ".ssproj"
        panel.allowedContentTypes = uttypes(["ssproj"])
        if panel.runModal() == .OK, let url = panel.url {
            Task { await run(op: "project.save", params: ["path": .string(url.path)]) }
        }
    }

    private func uttypes(_ exts: [String]) -> [UTType] {
        exts.compactMap { UTType(filenameExtension: $0) }
    }

    // ---- analyses ----------------------------------------------------------

    /// Run an op that returns Session text and/or replaces the worksheet.
    func run(op: String, params: [String: JSONValue] = [:]) async {
        do {
            let res = try await engine.send(op: op, worksheet: worksheet.toDTO(), params: params)
            guard res.ok else { showError(op, res.error ?? "unknown error"); return }
            if let ws = res.worksheet { setWorksheet(.from(ws)) }
            append(res.sessionText)
            for g in res.graphs ?? [] {
                GraphWindows.show(title: g.title, pngBase64: g.png)
            }
            if let t = res.statusTitle { statusText = t }
        } catch {
            showError(op, String(describing: error))
        }
    }

    func runDescriptives() async {
        await run(op: "descriptives")
    }
}
