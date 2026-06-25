import SwiftUI

/// Left pane listing the worksheet and its columns (name, type, non-missing count).
struct NavigatorView: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("Navigator")
                .font(.headline)
                .foregroundStyle(Color.accentColor)
                .padding(.horizontal, 8).padding(.vertical, 6)

            List {
                Text(model.worksheet.name).fontWeight(.semibold)
                ForEach(Array(model.worksheet.columnNames.enumerated()), id: \.offset) { j, name in
                    let kind = model.worksheet.looksNumeric(column: j) ? "num" : "text"
                    let n = model.worksheet.nonMissingCount(column: j)
                    Text("   \(name)  (\(kind), n=\(n))")
                        .font(.callout)
                }
            }
            .listStyle(.sidebar)
        }
    }
}
