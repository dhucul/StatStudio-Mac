import SwiftUI
import AppKit

/// Makes a bare-executable dev run behave as a normal foreground app (Dock icon, active
/// menu bar). In a packaged .app the Info.plist already provides this.
final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ note: Notification) {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        // `--smoke`: bring up the full window (exercising the grid) then quit — a startup
        // crash check for the GUI that can run from the command line.
        if CommandLine.arguments.contains("--smoke") {
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) {
                print("smoke: window initialized OK")
                NSApp.terminate(nil)
            }
        }
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

struct StatStudioApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var model = AppModel()

    var body: some Scene {
        WindowGroup {
            MainView()
                .environmentObject(model)
                .frame(minWidth: 960, minHeight: 640)
        }
        .commands { menuCommands }
    }

    /// A menu item that opens an analysis dialog.
    private func item(_ spec: AnalysisSpec) -> some View {
        Button(spec.title + "…") { model.present(spec) }
    }

    @ViewBuilder private func entryView(_ e: MenuEntry) -> some View {
        switch e {
        case .item(let spec): item(spec)
        case .divider: Divider()
        }
    }

    @ViewBuilder private func statNode(_ node: StatNode) -> some View {
        switch node {
        case .submenu(let title, let entries):
            Menu(title) {
                ForEach(Array(entries.enumerated()), id: \.offset) { _, e in entryView(e) }
            }
        case .leaf(let spec):
            item(spec)
        }
    }

    @CommandsBuilder private var menuCommands: some Commands {
        // ---- File ----
        CommandGroup(replacing: .newItem) {
            Button("New Worksheet") { model.newWorksheet() }
                .keyboardShortcut("n", modifiers: .command)
            Divider()
            Button("Open Data (CSV / Excel)…") { model.openData() }
                .keyboardShortcut("o", modifiers: .command)
            Button("Save Worksheet As…") { model.saveData() }
                .keyboardShortcut("s", modifiers: .command)
            Divider()
            Button("Open Project (.ssproj)…") { model.openProject() }
            Button("Save Project As…") { model.saveProject() }
            Divider()
            Menu("Sample Data") {
                Button("Demo (Height / Weight / Group)") { model.loadDemo() }
                if !model.samples.isEmpty {
                    Divider()
                    ForEach(model.samples) { s in
                        Button(s.name) { Task { await model.loadSample(name: s.name) } }
                    }
                }
            }
        }

        // ---- Calc ----
        CommandMenu("Calc") {
            item(Specs.calculator)
        }

        // ---- Stat ----
        CommandMenu("Stat") {
            ForEach(Array(MenuTree.stat.enumerated()), id: \.offset) { _, node in
                statNode(node)
            }
        }

        // ---- Graph ----
        CommandMenu("Graph") {
            ForEach(MenuTree.graph) { spec in item(spec) }
        }

        // ---- Help ----
        CommandGroup(replacing: .help) {
            Button("About StatStudio") {
                model.log("StatStudio for macOS — native SwiftUI front-end + .NET engine.")
            }
        }
    }
}
