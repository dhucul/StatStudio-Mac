import Foundation

/// One input on an analysis dialog.
struct Field: Identifiable {
    enum Kind {
        case columns(min: Int, numericOnly: Bool)   // multi-select  -> [String]
        case column(numericOnly: Bool)              // single-select -> String
        case columnOptional(numericOnly: Bool)      // single + "(none)" -> String? (omitted if none)
        case number(Double)                         // numeric text  -> Double
        case numberOptional                         // numeric text  -> Double? (omitted if blank)
        case integer(Int)                           // integer text  -> Int
        case text(String)                           // free text     -> String
        case choice([(String, JSONValue)])          // pop-up        -> value
        case boolean(Bool)                          // checkbox      -> Bool
    }

    let id = UUID()
    let key: String
    let label: String
    let kind: Kind

    // Builders keep the catalog terse.
    static func cols(_ key: String, _ label: String, min: Int = 1, numeric: Bool = true) -> Field {
        Field(key: key, label: label, kind: .columns(min: min, numericOnly: numeric))
    }
    static func col(_ key: String, _ label: String, numeric: Bool = true) -> Field {
        Field(key: key, label: label, kind: .column(numericOnly: numeric))
    }
    static func optcol(_ key: String, _ label: String, numeric: Bool = false) -> Field {
        Field(key: key, label: label, kind: .columnOptional(numericOnly: numeric))
    }
    static func num(_ key: String, _ label: String, _ def: Double) -> Field {
        Field(key: key, label: label, kind: .number(def))
    }
    static func optnum(_ key: String, _ label: String) -> Field {
        Field(key: key, label: label, kind: .numberOptional)
    }
    static func int(_ key: String, _ label: String, _ def: Int) -> Field {
        Field(key: key, label: label, kind: .integer(def))
    }
    static func text(_ key: String, _ label: String, _ def: String = "") -> Field {
        Field(key: key, label: label, kind: .text(def))
    }
    static func choice(_ key: String, _ label: String, _ options: [(String, JSONValue)]) -> Field {
        Field(key: key, label: label, kind: .choice(options))
    }
    static func flag(_ key: String, _ label: String, _ def: Bool) -> Field {
        Field(key: key, label: label, kind: .boolean(def))
    }

    /// The standard alternative-hypothesis pop-up (two-sided / less / greater).
    static func alternative(_ key: String = "alt") -> Field {
        .choice(key, "Alternative:", [
            ("Two-sided  (≠)", .string("twosided")),
            ("Less than  (<)", .string("less")),
            ("Greater than  (>)", .string("greater")),
        ])
    }
    static func confidence(_ key: String = "confidence") -> Field {
        .num(key, "Confidence (%):", 95)
    }
}

/// A menu-driven analysis: its engine op + the inputs to collect first.
struct AnalysisSpec: Identifiable {
    let id = UUID()
    let title: String
    let op: String
    let fields: [Field]

    init(_ title: String, op: String, fields: [Field] = []) {
        self.title = title
        self.op = op
        self.fields = fields
    }
}
