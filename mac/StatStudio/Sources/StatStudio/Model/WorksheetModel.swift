import Foundation

/// The worksheet as a plain string matrix (row-major). The native grid edits this; the
/// engine owns the numeric/text typing rules, so this stays "dumb strings".
struct WorksheetModel {
    var name: String
    var columnNames: [String]
    var rows: [[String]]

    static func empty(cols: Int = 8, rows: Int = 20) -> WorksheetModel {
        let columnCount = max(cols, 1)
        let names = (0..<columnCount).map { "C\($0 + 1)" }
        let blank = Array(repeating: "", count: columnCount)
        return WorksheetModel(name: "Worksheet 1", columnNames: names,
                              rows: Array(repeating: blank, count: max(rows, 0)))
    }

    /// Column-major DTO for the engine (empty string → null/missing).
    func toDTO() -> WorksheetDTO {
        var cols: [ColumnDTO] = []
        cols.reserveCapacity(columnNames.count)
        for (j, n) in columnNames.enumerated() {
            var cells: [String?] = []
            cells.reserveCapacity(rows.count)
            for r in rows {
                let v = j < r.count ? r[j] : ""
                cells.append(v.isEmpty ? nil : v)
            }
            cols.append(ColumnDTO(name: n, cells: cells))
        }
        return WorksheetDTO(name: name, columns: cols)
    }

    /// Replace contents from an engine-returned worksheet, padding with blank edit rows.
    static func from(_ dto: WorksheetDTO, padRows: Int = 5) -> WorksheetModel {
        let names = dto.columns.map { $0.name }
        let height = dto.columns.map { $0.cells.count }.max() ?? 0
        var rows: [[String]] = []
        rows.reserveCapacity(height + padRows)
        for r in 0..<height {
            rows.append(dto.columns.map { r < $0.cells.count ? ($0.cells[r] ?? "") : "" })
        }
        let blank = Array(repeating: "", count: max(names.count, 1))
        for _ in 0..<padRows { rows.append(blank) }
        return WorksheetModel(name: dto.name ?? "Worksheet 1",
                              columnNames: names.isEmpty ? ["C1"] : names, rows: rows)
    }

    /// Cheap numeric-vs-text sniff for the Navigator (mirrors the engine's intent).
    func looksNumeric(column j: Int) -> Bool {
        var seen = false
        for r in rows where j < r.count {
            let v = r[j].trimmingCharacters(in: .whitespaces)
            if v.isEmpty || v == "*" { continue }
            seen = true
            if Double(v) == nil { return false }
        }
        return seen
    }

    func nonMissingCount(column j: Int) -> Int {
        rows.reduce(0) { acc, r in
            guard j < r.count else { return acc }
            let v = r[j].trimmingCharacters(in: .whitespaces)
            return (v.isEmpty || v == "*") ? acc : acc + 1
        }
    }
}
