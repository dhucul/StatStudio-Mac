import Foundation

/// Deterministic demo dataset mirroring the WPF `BuildDemo` (Height/Weight/Group, 40 rows).
/// Seeded so it's reproducible; used before the editable grid (and by --selftest).
enum DemoData {
    static func build() -> WorksheetModel {
        var rng = LCG(seed: 12345)
        var rows: [[String]] = []
        for i in 0..<40 {
            let h = 170.0 + rng.gaussian() * 8.0
            let w = 0.9 * h - 90.0 + rng.gaussian() * 5.0
            rows.append([
                String(format: "%.1f", h),
                String(format: "%.1f", w),
                i % 2 == 0 ? "A" : "B",
            ])
        }
        return WorksheetModel(name: "Demo", columnNames: ["Height", "Weight", "Group"], rows: rows)
    }
}

/// Tiny seeded LCG + Box-Muller, so demo data is identical on every machine.
private struct LCG {
    var state: UInt64
    init(seed: UInt64) { state = seed }
    mutating func next() -> Double {
        state = 6364136223846793005 &* state &+ 1442695040888963407
        return Double(state >> 11) / Double(1 << 53)
    }
    mutating func gaussian() -> Double {
        let u1 = max(1e-12, 1.0 - next())
        let u2 = 1.0 - next()
        return (-2.0 * log(u1)).squareRoot() * sin(2.0 * .pi * u2)
    }
}
