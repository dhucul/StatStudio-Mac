import SwiftUI

/// Read-only, monospaced Session output (the Minitab-style results pane).
struct SessionView: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("Session")
                .font(.headline)
                .foregroundStyle(Color.accentColor)
                .padding(.horizontal, 8).padding(.vertical, 6)

            ScrollViewReader { proxy in
                ScrollView([.vertical, .horizontal]) {
                    Text(model.sessionText)
                        .font(.system(.body, design: .monospaced))
                        .textSelection(.enabled)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(8)
                        .id("end")
                }
                .onChange(of: model.sessionText) { _, _ in
                    proxy.scrollTo("end", anchor: .bottom)
                }
            }
        }
        .background(Color(nsColor: .textBackgroundColor))
    }
}
