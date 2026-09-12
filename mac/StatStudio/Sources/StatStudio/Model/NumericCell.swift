import Foundation

/// The invariant decimal/thousands/exponent syntax used by Core's DataColumn.
enum NumericCell {
    private static let syntax = try! NSRegularExpression(
        pattern: #"^[+-]?(?:[0-9][0-9,]*(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$"#)

    static func parse(_ text: String) -> Double? {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        let range = NSRange(value.startIndex..<value.endIndex, in: value)
        guard let match = syntax.firstMatch(in: value, range: range), match.range == range,
              let number = Double(value.replacingOccurrences(of: ",", with: "")), number.isFinite else { return nil }
        return number
    }
}
