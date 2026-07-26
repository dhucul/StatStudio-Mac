import SwiftUI
import AppKit
import UniformTypeIdentifiers

/// Opens engine-rendered graph PNGs in independent native windows.
@MainActor
enum GraphWindows {
    fileprivate static var controllers: [GraphWindowController] = []

    static func show(title: String, pngBase64: String) {
        guard let data = Data(base64Encoded: pngBase64), let image = NSImage(data: data) else { return }
        let host = NSHostingController(rootView: GraphView(image: image, pngData: data, title: title))
        let window = NSWindow(contentViewController: host)
        window.title = title
        window.styleMask = [.titled, .closable, .resizable, .miniaturizable]
        window.setContentSize(NSSize(width: 820, height: 620))
        window.isReleasedWhenClosed = false
        window.center()

        let wc = GraphWindowController(window: window)
        wc.showWindow(nil)
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        controllers.append(wc)
    }
}

@MainActor
fileprivate final class GraphWindowController: NSWindowController, NSWindowDelegate {
    override init(window: NSWindow?) {
        super.init(window: window)
        window?.delegate = self
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        window?.delegate = self
    }

    func windowWillClose(_ notification: Notification) {
        GraphWindows.controllers.removeAll { $0 === self }
    }
}

private struct GraphView: View {
    let image: NSImage
    let pngData: Data
    let title: String

    var body: some View {
        VStack(spacing: 0) {
            Image(nsImage: image)
                .resizable()
                .interpolation(.high)
                .scaledToFit()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .padding(8)
            Divider()
            HStack {
                Spacer()
                Button("Save Image…", action: save)
            }
            .padding(8)
        }
        .background(Color(nsColor: .windowBackgroundColor))
    }

    private func save() {
        let panel = NSSavePanel()
        panel.nameFieldStringValue = title + ".png"
        panel.allowedContentTypes = [.png]
        if panel.runModal() == .OK, let url = panel.url {
            do {
                try pngData.write(to: url, options: .atomic)
            } catch {
                let alert = NSAlert()
                alert.alertStyle = .critical
                alert.messageText = "Couldn’t Save Image"
                alert.informativeText = error.localizedDescription
                alert.runModal()
            }
        }
    }
}
