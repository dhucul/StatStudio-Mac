import SwiftUI

/// The 3-pane shell: Navigator | (Session over Worksheet), with a bottom status strip.
struct MainView: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        VStack(spacing: 0) {
            HSplitView {
                NavigatorView()
                    .frame(minWidth: 160, idealWidth: 200, maxWidth: 280)
                VSplitView {
                    SessionView().frame(minHeight: 150)
                    WorksheetView().frame(minHeight: 150)
                }
                .frame(minWidth: 600)
            }
            StatusStrip()
        }
        .sheet(item: $model.pendingSpec) { spec in
            AnalysisSheet(spec: spec, columns: model.columnsInfo) { params in
                Task { await model.run(op: spec.op, params: params) }
            }
        }
    }
}

private struct StatusStrip: View {
    @EnvironmentObject var model: AppModel
    var body: some View {
        HStack {
            Text(model.statusText)
            Spacer()
            Text(model.dims)
        }
        .font(.callout)
        .foregroundStyle(.white)
        .padding(.horizontal, 10)
        .padding(.vertical, 4)
        .background(Color.accentColor)
    }
}
