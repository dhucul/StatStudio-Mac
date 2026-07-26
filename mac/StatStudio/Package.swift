// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "StatStudio",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "StatStudio",
            path: "Sources/StatStudio"
        ),
        .testTarget(
            name: "StatStudioTests",
            dependencies: ["StatStudio"],
            path: "Tests/StatStudioTests"
        )
    ],
    // Build in Swift 5 language mode for now (relaxed concurrency checking);
    // the native UI is single-threaded UI + one IPC actor, tightened later.
    swiftLanguageModes: [.v5]
)
