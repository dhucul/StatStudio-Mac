import SwiftUI

/// Generic analysis dialog: renders a spec's fields, validates input, and hands back the
/// engine `params`. One sheet drives every analysis in the catalog.
struct AnalysisSheet: View {
    let spec: AnalysisSpec
    let columns: [(name: String, numeric: Bool)]
    let onRun: ([String: JSONValue]) -> Void

    @Environment(\.dismiss) private var dismiss

    @State private var multi: [String: Set<String>] = [:]
    @State private var single: [String: String] = [:]
    @State private var text: [String: String] = [:]
    @State private var flags: [String: Bool] = [:]
    @State private var choiceIndex: [String: Int] = [:]
    @State private var errorMessage: String?

    init(spec: AnalysisSpec, columns: [(name: String, numeric: Bool)],
         onRun: @escaping ([String: JSONValue]) -> Void) {
        self.spec = spec
        self.columns = columns
        self.onRun = onRun

        var m: [String: Set<String>] = [:], s: [String: String] = [:]
        var t: [String: String] = [:], f: [String: Bool] = [:], ci: [String: Int] = [:]
        for field in spec.fields {
            switch field.kind {
            case .columns: m[field.key] = []
            case .column(let numericOnly):
                s[field.key] = Self.eligible(columns, numericOnly).first ?? ""
            case .columnOptional: s[field.key] = ""        // "(none)"
            case .number(let d): t[field.key] = Self.trim(d)
            case .numberOptional: t[field.key] = ""
            case .integer(let i): t[field.key] = String(i)
            case .text(let def): t[field.key] = def
            case .choice: ci[field.key] = 0
            case .boolean(let b): f[field.key] = b
            }
        }
        _multi = State(initialValue: m); _single = State(initialValue: s)
        _text = State(initialValue: t); _flags = State(initialValue: f)
        _choiceIndex = State(initialValue: ci)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(spec.title).font(.headline)

            ForEach(spec.fields) { field in
                fieldView(field)
            }

            if let e = errorMessage {
                Text(e).font(.callout).foregroundStyle(.red)
            }

            HStack {
                Spacer()
                Button("Cancel") { dismiss() }.keyboardShortcut(.cancelAction)
                Button("OK") { runTapped() }.keyboardShortcut(.defaultAction)
            }
        }
        .padding(16)
        .frame(width: 420)
    }

    // MARK: field rendering

    @ViewBuilder
    private func fieldView(_ field: Field) -> some View {
        switch field.kind {
        case .columns(_, let numericOnly):
            VStack(alignment: .leading, spacing: 4) {
                Text(field.label)
                let names = Self.eligible(columns, numericOnly)
                ScrollView {
                    VStack(alignment: .leading, spacing: 2) {
                        ForEach(names, id: \.self) { name in
                            Toggle(name, isOn: columnBinding(field.key, name))
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .frame(height: min(160, max(44, CGFloat(names.count) * 22)))
                .border(Color(nsColor: .separatorColor))
            }

        case .column(let numericOnly):
            let names = Self.eligible(columns, numericOnly)
            Picker(field.label, selection: bindString($single, field.key)) {
                ForEach(names, id: \.self) { Text($0).tag($0) }
            }

        case .columnOptional(let numericOnly):
            let names = Self.eligible(columns, numericOnly)
            Picker(field.label, selection: bindString($single, field.key)) {
                Text("(none)").tag("")
                ForEach(names, id: \.self) { Text($0).tag($0) }
            }

        case .choice(let options):
            Picker(field.label, selection: bindInt($choiceIndex, field.key)) {
                ForEach(Array(options.enumerated()), id: \.offset) { i, opt in
                    Text(opt.0).tag(i)
                }
            }

        case .number, .integer, .numberOptional:
            HStack {
                Text(field.label)
                Spacer()
                TextField("", text: bindString($text, field.key))
                    .frame(width: 120)
                    .multilineTextAlignment(.trailing)
            }

        case .text:
            VStack(alignment: .leading, spacing: 4) {
                Text(field.label)
                TextField("", text: bindString($text, field.key))
            }

        case .boolean:
            Toggle(field.label, isOn: bindBool($flags, field.key))
        }
    }

    // MARK: actions

    private func runTapped() {
        var params: [String: JSONValue] = [:]
        for field in spec.fields {
            switch field.kind {
            case .columns(let min, _):
                let order = columns.map { $0.name }
                let sel = (multi[field.key] ?? []).sorted {
                    (order.firstIndex(of: $0) ?? 0) < (order.firstIndex(of: $1) ?? 0)
                }
                if sel.count < min {
                    errorMessage = "Select at least \(min) column\(min == 1 ? "" : "s") for “\(field.label)”."
                    return
                }
                params[field.key] = .strings(sel)

            case .column:
                guard let v = single[field.key], !v.isEmpty else {
                    errorMessage = "Choose a column for “\(field.label)”."; return
                }
                params[field.key] = .string(v)

            case .columnOptional:
                let v = single[field.key] ?? ""
                if !v.isEmpty { params[field.key] = .string(v) }   // omitted if "(none)"

            case .number:
                guard let d = Double((text[field.key] ?? "").trimmingCharacters(in: .whitespaces)) else {
                    errorMessage = "Enter a number for “\(field.label)”."; return
                }
                params[field.key] = .double(d)

            case .numberOptional:
                let s = (text[field.key] ?? "").trimmingCharacters(in: .whitespaces)
                if !s.isEmpty {
                    guard let d = Double(s) else {
                        errorMessage = "Enter a number for “\(field.label)” (or leave blank)."; return
                    }
                    params[field.key] = .double(d)
                }

            case .text:
                let s = (text[field.key] ?? "").trimmingCharacters(in: .whitespaces)
                if s.isEmpty { errorMessage = "Enter a value for “\(field.label)”."; return }
                params[field.key] = .string(s)

            case .integer:
                guard let i = Int((text[field.key] ?? "").trimmingCharacters(in: .whitespaces)) else {
                    errorMessage = "Enter an integer for “\(field.label)”."; return
                }
                params[field.key] = .int(i)

            case .choice(let options):
                params[field.key] = options[choiceIndex[field.key] ?? 0].1

            case .boolean:
                params[field.key] = .bool(flags[field.key] ?? false)
            }
        }
        onRun(params)
        dismiss()
    }

    // MARK: helpers

    private static func eligible(_ columns: [(name: String, numeric: Bool)], _ numericOnly: Bool) -> [String] {
        columns.filter { !numericOnly || $0.numeric }.map { $0.name }
    }

    private static func trim(_ d: Double) -> String {
        d == d.rounded() ? String(Int(d)) : String(d)
    }

    private func columnBinding(_ key: String, _ name: String) -> Binding<Bool> {
        Binding(
            get: { multi[key]?.contains(name) ?? false },
            set: { on in
                var set = multi[key] ?? []
                if on { set.insert(name) } else { set.remove(name) }
                multi[key] = set
            })
    }

    private func bindString(_ dict: Binding<[String: String]>, _ key: String) -> Binding<String> {
        Binding(get: { dict.wrappedValue[key] ?? "" }, set: { dict.wrappedValue[key] = $0 })
    }
    private func bindInt(_ dict: Binding<[String: Int]>, _ key: String) -> Binding<Int> {
        Binding(get: { dict.wrappedValue[key] ?? 0 }, set: { dict.wrappedValue[key] = $0 })
    }
    private func bindBool(_ dict: Binding<[String: Bool]>, _ key: String) -> Binding<Bool> {
        Binding(get: { dict.wrappedValue[key] ?? false }, set: { dict.wrappedValue[key] = $0 })
    }
}
