import Foundation

// Wire types mirroring src/StatStudio.Engine/Protocol.cs (camelCase JSON).

struct ColumnDTO: Codable, Sendable {
    var name: String
    var cells: [String?]
    var type: String? = nil
}

struct WorksheetDTO: Codable, Sendable {
    var name: String?
    var columns: [ColumnDTO]
}

struct EngineRequest: Encodable, Sendable {
    var id: Int
    var op: String
    var worksheet: WorksheetDTO?
    var params: [String: JSONValue]?
}

struct SampleInfo: Codable, Sendable, Identifiable {
    var name: String
    var description: String
    var id: String { name }
}

struct GraphImageDTO: Codable, Sendable {
    var title: String
    var png: String
}

struct EngineResponse: Decodable, Sendable {
    var id: Int
    var ok: Bool
    var statusTitle: String?
    var sessionText: String?
    var worksheet: WorksheetDTO?
    var graphs: [GraphImageDTO]?
    var samples: [SampleInfo]?
    var error: String?
}

/// A minimal Codable JSON value, used for the flexible per-op `params` payload.
enum JSONValue: Codable, Sendable {
    case string(String)
    case int(Int)
    case double(Double)
    case bool(Bool)
    case array([JSONValue])
    case object([String: JSONValue])
    case null

    init(from decoder: Decoder) throws {
        let c = try decoder.singleValueContainer()
        if c.decodeNil() { self = .null }
        else if let b = try? c.decode(Bool.self) { self = .bool(b) }
        else if let i = try? c.decode(Int.self) { self = .int(i) }
        else if let d = try? c.decode(Double.self) { self = .double(d) }
        else if let s = try? c.decode(String.self) { self = .string(s) }
        else if let a = try? c.decode([JSONValue].self) { self = .array(a) }
        else if let o = try? c.decode([String: JSONValue].self) { self = .object(o) }
        else { throw DecodingError.dataCorruptedError(in: c, debugDescription: "unsupported JSON value") }
    }

    func encode(to encoder: Encoder) throws {
        var c = encoder.singleValueContainer()
        switch self {
        case .string(let s): try c.encode(s)
        case .int(let i): try c.encode(i)
        case .double(let d): try c.encode(d)
        case .bool(let b): try c.encode(b)
        case .array(let a): try c.encode(a)
        case .object(let o): try c.encode(o)
        case .null: try c.encodeNil()
        }
    }
}

extension JSONValue {
    /// Convenience for building a `["columns": .strings([...])]` param.
    static func strings(_ xs: [String]) -> JSONValue { .array(xs.map { .string($0) }) }
}
