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
    private var operationTail: Task<Void, Never>?
    private var operationEpoch = 0
    private var operationSequence = 0
    private var worksheetRevision = 0

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

    /// Session output is one `@Published` string re-laid-out in full on every append, so
    /// an unbounded log turns each analysis into a progressively slower full re-render.
    private static let sessionLimit = 400_000

    func log(_ text: String) {
        sessionText += text + "\n"
        trimSession()
    }

    func append(_ block: String?) {
        guard let block, !block.isEmpty else { return }
        sessionText += block
        trimSession()
    }

    private func trimSession() {
        guard sessionText.count > Self.sessionLimit else { return }
        sessionText = String(sessionText.suffix(Self.sessionLimit / 2))
    }

    func showError(_ title: String, _ message: String) {
        log("ERROR — \(title): \(message)")
        statusText = title
    }

    // ---- worksheet plumbing -----------------------------------------------

    /// Structural replace: swap the worksheet and tell the grid to rebuild.
    func setWorksheet(_ ws: WorksheetModel) {
        worksheet = ws
        worksheetRevision += 1
        gridGeneration += 1
    }

    func worksheetDidEdit() {
        worksheetRevision += 1
    }

    func newWorksheet() {
        invalidatePendingOperations()
        setWorksheet(.empty())
        statusText = "New worksheet"
    }

    func loadDemo() {
        invalidatePendingOperations()
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

    /// Queue an op so its worksheet snapshot is taken only after every earlier op has
    /// completed. Local worksheet replacements invalidate queued/running responses.
    func run(op: String, params: [String: JSONValue] = [:]) async {
        let previous = operationTail
        let epoch = operationEpoch
        operationSequence += 1
        let sequence = operationSequence
        let task = Task { @MainActor [weak self] in
            await previous?.value
            guard let self, !Task.isCancelled, epoch == self.operationEpoch else { return }
            await self.performRun(op: op, params: params, epoch: epoch)
        }
        operationTail = task
        await task.value
        if sequence == operationSequence { operationTail = nil }
    }

    private func performRun(op: String, params: [String: JSONValue], epoch: Int) async {
        do {
            let snapshotRevision = worksheetRevision
            let snapshot = worksheet.toDTO()
            let res = try await engine.send(op: op, worksheet: snapshot, params: params)
            guard !Task.isCancelled, epoch == operationEpoch else { return }
            guard res.ok else { showError(op, res.error ?? "unknown error"); return }
            if let ws = res.worksheet {
                if snapshotRevision == worksheetRevision {
                    setWorksheet(.from(ws))
                } else {
                    log("NOTICE — \(op): worksheet changed while the command was running; its worksheet result was not applied.")
                }
            }
            append(res.sessionText)
            for g in res.graphs ?? [] {
                GraphWindows.show(title: g.title, pngBase64: g.png)
            }
            if let t = res.statusTitle { statusText = t }
        } catch {
            guard !Task.isCancelled, epoch == operationEpoch else { return }
            showError(op, String(describing: error))
        }
    }

    private func invalidatePendingOperations() {
        operationEpoch += 1
        operationSequence += 1
        operationTail?.cancel()
        // The tail deliberately stays: the epoch guard already discards stale results, and
        // dropping it would unchain the queue, letting the next op race ahead of requests
        // still in flight inside the engine actor.
    }

    func runDescriptives() async {
        await run(op: "descriptives")
    }
}
