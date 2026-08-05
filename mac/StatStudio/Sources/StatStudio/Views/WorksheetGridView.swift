import SwiftUI
import AppKit

/// The Worksheet pane: header + the editable spreadsheet grid.
struct WorksheetView: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("Worksheet: \(model.worksheet.name)")
                .font(.headline)
                .foregroundStyle(Color.accentColor)
                .padding(.horizontal, 8).padding(.vertical, 6)
            WorksheetGridView(model: model)
        }
    }
}

// MARK: - NSTableView-backed editable grid

/// Minitab-style editable grid: dynamic string columns (C1…Cn), a row-number gutter,
/// click-to-edit cells, an auto-growing trailing blank row, and Delete-key row removal.
/// The model owns the data; the grid reloads only when `gridGeneration` changes (a
/// structural load), so cell edits never interrupt typing.
struct WorksheetGridView: NSViewRepresentable {
    let model: AppModel

    func makeCoordinator() -> Coordinator { Coordinator(model) }

    func makeNSView(context: Context) -> NSScrollView {
        let table = SpreadsheetTableView()
        table.dataSource = context.coordinator
        table.delegate = context.coordinator
        table.usesAlternatingRowBackgroundColors = true
        table.allowsColumnResizing = true
        table.allowsMultipleSelection = true
        table.columnAutoresizingStyle = .noColumnAutoresizing
        table.rowSizeStyle = .custom
        table.rowHeight = 20
        table.intercellSpacing = NSSize(width: 1, height: 1)
        table.coordinator = context.coordinator

        context.coordinator.tableView = table
        context.coordinator.lastGeneration = model.gridGeneration
        context.coordinator.rebuildColumns()

        let scroll = NSScrollView()
        scroll.documentView = table
        scroll.hasVerticalScroller = true
        scroll.hasHorizontalScroller = true
        scroll.autohidesScrollers = false
        return scroll
    }

    func updateNSView(_ nsView: NSScrollView, context: Context) {
        let coord = context.coordinator
        // Reload only on a structural change; plain cell edits leave generation alone.
        if coord.lastGeneration != model.gridGeneration {
            coord.lastGeneration = model.gridGeneration
            coord.rebuildColumns()
            (nsView.documentView as? NSTableView)?.reloadData()
        }
    }

    // MARK: Coordinator

    @MainActor
    final class Coordinator: NSObject, NSTableViewDataSource, NSTableViewDelegate, NSTextFieldDelegate {
        let model: AppModel
        weak var tableView: NSTableView?
        var lastGeneration = -1

        private let rowColID = NSUserInterfaceItemIdentifier("__row__")

        init(_ model: AppModel) { self.model = model }

        // Build the row-number gutter + one column per worksheet column.
        func rebuildColumns() {
            guard let tv = tableView else { return }
            for c in tv.tableColumns { tv.removeTableColumn(c) }

            let gutter = NSTableColumn(identifier: rowColID)
            gutter.title = ""
            gutter.width = 40
            gutter.minWidth = 32
            gutter.maxWidth = 60
            tv.addTableColumn(gutter)

            for (j, name) in model.worksheet.columnNames.enumerated() {
                let col = NSTableColumn(identifier: NSUserInterfaceItemIdentifier("\(j)"))
                col.title = name
                col.width = 90
                col.minWidth = 48
                tv.addTableColumn(col)
            }
        }

        // ---- data source ----

        func numberOfRows(in tableView: NSTableView) -> Int { model.worksheet.rows.count }

        // ---- view-based cells ----

        func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
            guard let col = tableColumn else { return nil }
            let isGutter = col.identifier == rowColID

            let field = (tableView.makeView(withIdentifier: col.identifier, owner: self) as? NSTextField)
                ?? makeField(id: col.identifier, gutter: isGutter)

            if isGutter {
                field.stringValue = "\(row + 1)"
            } else if let j = Int(col.identifier.rawValue) {
                // NSTableView caches its row count and only re-queries on reloadData(),
                // which SwiftUI defers to a later update pass — so `row` can outrun a
                // worksheet that was just replaced by a shorter one. Never trust it.
                guard row >= 0, row < model.worksheet.rows.count else {
                    field.stringValue = ""
                    return field
                }
                let r = model.worksheet.rows[row]
                field.stringValue = j < r.count ? r[j] : ""
            }
            return field
        }

        private func makeField(id: NSUserInterfaceItemIdentifier, gutter: Bool) -> NSTextField {
            let field = NSTextField()
            field.identifier = id
            field.isBordered = false
            field.drawsBackground = false
            field.lineBreakMode = .byClipping
            if gutter {
                field.isEditable = false
                field.isSelectable = false
                field.alignment = .right
                field.textColor = .secondaryLabelColor
                field.font = .systemFont(ofSize: 11)
            } else {
                field.isEditable = true
                field.isSelectable = true
                field.delegate = self
                field.font = .monospacedSystemFont(ofSize: 12, weight: .regular)
            }
            return field
        }

        // ---- editing ----

        func controlTextDidEndEditing(_ obj: Notification) {
            guard let field = obj.object as? NSTextField,
                  let tv = tableView,
                  let idRaw = field.identifier?.rawValue,
                  let j = Int(idRaw) else { return }
            let row = tv.row(for: field)
            guard row >= 0 else { return }
            commit(row: row, col: j, value: field.stringValue)
        }

        private func commit(row: Int, col j: Int, value: String) {
            guard row < model.worksheet.rows.count else { return }
            if j >= model.worksheet.rows[row].count {
                let pad = j + 1 - model.worksheet.rows[row].count
                model.worksheet.rows[row].append(contentsOf: Array(repeating: "", count: pad))
            }
            model.worksheet.rows[row][j] = value
            model.worksheetDidEdit()

            // Keep a trailing blank row so there's always room to type (cf. CanUserAddRows).
            let last = model.worksheet.rows.count - 1
            if row == last && !value.isEmpty {
                model.worksheet.rows.append(Array(repeating: "", count: model.worksheet.columnNames.count))
                tableView?.insertRows(at: IndexSet(integer: last + 1), withAnimation: [])
            }
        }

        func deleteRows(_ rows: IndexSet) {
            guard !rows.isEmpty else { return }
            // Commit any active cell editor before mutating. controlTextDidEndEditing
            // fires after the reload below, and would resolve its row against the
            // rebuilt table — writing the stale text into whatever row shifted up.
            _ = tableView?.window?.endEditing(for: nil)
            for r in rows.sorted(by: >) where r < model.worksheet.rows.count {
                model.worksheet.rows.remove(at: r)
            }
            if model.worksheet.rows.isEmpty {
                model.worksheet.rows.append(Array(repeating: "", count: model.worksheet.columnNames.count))
            }
            model.worksheetDidEdit()
            tableView?.reloadData()
        }
    }
}

/// NSTableView that forwards Delete/Backspace (when rows, not a cell editor, are focused)
/// to a handler for row removal.
final class SpreadsheetTableView: NSTableView {
    weak var coordinator: WorksheetGridView.Coordinator?

    override func keyDown(with event: NSEvent) {
        // keyCodes: 51 = Delete (Backspace), 117 = Forward Delete.
        if (event.keyCode == 51 || event.keyCode == 117), selectedRowIndexes.count > 0 {
            coordinator?.deleteRows(selectedRowIndexes)
            return
        }
        super.keyDown(with: event)
    }
}
