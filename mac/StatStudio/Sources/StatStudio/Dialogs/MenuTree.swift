import Foundation

/// An entry inside a submenu: an analysis item or a separator.
enum MenuEntry {
    case item(AnalysisSpec)
    case divider
}

/// A node in the Stat menu: a named submenu or a top-level item.
enum StatNode {
    case submenu(String, [MenuEntry])
    case leaf(AnalysisSpec)
}

/// The Stat menu structure (mirrors MainWindow.xaml). Data-driven so it renders via
/// ForEach, sidestepping SwiftUI's 10-child ViewBuilder limit per menu.
enum MenuTree {
    static let stat: [StatNode] = [
        .submenu("Basic Statistics", [
            .item(Specs.descriptives), .divider,
            .item(Specs.oneSampleT), .item(Specs.twoSampleT), .item(Specs.pairedT), .divider,
            .item(Specs.oneProportion), .item(Specs.twoProportions), .divider,
            .item(Specs.chiGof), .item(Specs.chiAssoc), .divider,
            .item(Specs.corrPearson), .item(Specs.corrSpearman),
            .item(Specs.normality), .item(Specs.twoVariances), .item(Specs.fisher),
        ]),
        .submenu("ANOVA", [
            .item(Specs.oneWayAnova), .item(Specs.twoWayAnova), .divider, .item(Specs.equalVariances),
        ]),
        .submenu("Nonparametrics", [
            .item(Specs.mannWhitney), .item(Specs.wilcoxon), .item(Specs.kruskal),
            .item(Specs.signTest), .item(Specs.runsTest),
        ]),
        .submenu("Regression", [
            .item(Specs.simpleReg), .item(Specs.multipleReg), .item(Specs.polyReg), .divider,
            .item(Specs.bestSubsets), .item(Specs.stepwise), .divider, .item(Specs.logistic),
        ]),
        .submenu("Time Series", [
            .item(Specs.trend), .item(Specs.movingAvg), .divider,
            .item(Specs.singleExp), .item(Specs.doubleExp), .item(Specs.winters), .divider,
            .item(Specs.decomposition), .item(Specs.acf), .item(Specs.pacf), .divider,
            .item(Specs.arima), .item(Specs.sarima),
        ]),
        .submenu("Multivariate", [
            .item(Specs.pca), .item(Specs.factor), .item(Specs.kmeans),
        ]),
        .submenu("Reliability / Survival", [
            .item(Specs.distFit), .item(Specs.kaplanMeier),
        ]),
        .submenu("DOE", [
            .item(Specs.createFactorial), .item(Specs.createFractional), .item(Specs.analyzeFactorial), .divider,
            .item(Specs.createRsm), .item(Specs.analyzeRsm), .divider,
            .item(Specs.createMixture), .item(Specs.analyzeMixture),
        ]),
        .leaf(Specs.power),
        .submenu("Bayesian Statistics", [
            .item(Specs.bayesProp), .item(Specs.bayesNormal), .item(Specs.bayesReg),
        ]),
        .submenu("Mixed / Hierarchical", [
            .item(Specs.oneWayRandom),
        ]),
        .submenu("Control Charts", [
            .item(Specs.xbarR), .item(Specs.xbarS), .item(Specs.imr), .divider,
            .item(Specs.pChart), .item(Specs.npChart), .item(Specs.cChart), .item(Specs.uChart),
        ]),
        .submenu("Quality Tools", [
            .item(Specs.capability), .item(Specs.gageRR),
        ]),
    ]

    static let graph: [AnalysisSpec] = [
        Specs.histogram, Specs.boxplot, Specs.scatter, Specs.timeSeries, Specs.probPlot,
    ]
}
