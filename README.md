# StatStudio for macOS

A Minitab-style statistics workbench, **native for Apple Silicon (macOS, arm64)**.

StatStudio is a native **SwiftUI / AppKit** application backed by a bundled **.NET 10**
statistics engine. The Swift app is pure UI; all of the math and all of the graphs come
from the engine, so results are numerically identical to the Windows edition (the engine
is the same code, verified by a 309-check reference suite).

> This is the macOS port. The original Windows (.NET 10 + WPF) edition lives at
> **[dhucul/StatStudio](https://github.com/dhucul/StatStudio)**; this repo shares only the
> platform-neutral `StatStudio.Core` engine.

## Features

- **Worksheet** — editable column grid; import/export **CSV/TSV** and **Excel (.xlsx)**;
  native **`.ssproj`** projects; missing values as `*`; ten built-in **Sample Data** sets.
- **Basic Statistics** — descriptive statistics, 1-/2-sample & paired *t*, 1-/2-proportion,
  chi-square (GOF + association), Pearson/Spearman correlation, Anderson-Darling normality,
  F-test for two variances, Fisher's exact.
- **Nonparametrics** — Mann-Whitney, Wilcoxon signed-rank, Kruskal-Wallis, sign, runs.
- **ANOVA** — one-way (+ Tukey), two-way, tests for equal variances.
- **Regression** — simple, multiple, polynomial, best-subsets, stepwise, binary logistic.
- **Time series** — trend, moving average, single/double/Winters smoothing, decomposition,
  ACF/PACF, **ARIMA** and seasonal **SARIMA** with forecasts.
- **Multivariate** — principal components, factor analysis, k-means.
- **Reliability / survival** — parametric life-data fitting (Weibull/exp/lognormal/normal)
  and Kaplan-Meier.
- **Bayesian** — Beta-Binomial proportion, normal mean, reference-prior regression.
- **Mixed** — one-way random effects (variance components, ICC, BLUPs).
- **DOE** — create/analyze factorial, fractional, response-surface, and mixture designs.
- **SPC** — Xbar-R/S, I-MR, P, NP, C, U; process capability; crossed Gage R&R.
- **Power & sample size**, a worksheet **Calculator**, and the standard **Graphs**.

## Architecture

```
StatStudio.app  (native macOS bundle, arm64)
├─ Contents/MacOS/StatStudio          SwiftUI/AppKit front-end
│    • native menu bar, editable worksheet grid, Session pane, analysis dialogs, graph windows
│    • talks to the engine over newline-delimited JSON (stdin/stdout)
└─ Contents/Resources/Engine/          self-contained .NET 10 helper (StatStudio.Engine)
     • references StatStudio.Core (all the math) and renders ScottPlot graphs to PNG
```

| Path | What |
|---|---|
| `src/StatStudio.Core`   | Pure, UI-free statistics engine (Math.NET, ClosedXML). Cross-platform. |
| `src/StatStudio.Engine` | .NET 10 JSON-RPC helper wrapping Core; ~60 ops; headless ScottPlot→PNG. |
| `mac/StatStudio`        | The native SwiftUI app (Swift Package). |
| `tools/StatStudio.Smoke`| 309 deterministic numeric self-tests against reference values. |
| `scripts/`              | Build / package / run scripts. |

## Prerequisites
- macOS on Apple Silicon, **Xcode** (Swift toolchain).
- **.NET 10 SDK** (osx-arm64) for the engine:
  `curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$HOME/.dotnet"`

## Build / run / test
```bash
# Prove the engine math:
dotnet run --project tools/StatStudio.Smoke -c Release      # 309 checks

# Develop (builds engine + app, launches the window):
scripts/run-mac-dev.sh
scripts/run-mac-dev.sh --selftest                           # headless Swift↔engine check

# Package a distributable app + installer:
scripts/build-mac.sh --dmg                                  # dist/StatStudio.app (+ .dmg)
scripts/build-installer.sh                                  # dist/StatStudioInstaller.pkg → /Applications
```

The engine is published **self-contained**, so the packaged app needs no .NET install on the
target Mac. Unsigned builds: first launch may need right-click ▸ Open (Gatekeeper). For
frictionless distribution, sign with an Apple Developer ID and notarize.

## License
Same as the upstream StatStudio project.
