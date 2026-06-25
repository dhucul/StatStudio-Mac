import Foundation

/// The analysis catalog: one `AnalysisSpec` per menu item, with field keys matching the
/// engine's `params`. The menus in StatStudioApp reference these.
enum Specs {

    // ---- Basic Statistics --------------------------------------------------

    static let descriptives = AnalysisSpec("Display Descriptive Statistics", op: "descriptives",
        fields: [.cols("columns", "Variables (numeric):", min: 1)])
    static let oneSampleT = AnalysisSpec("1-Sample t", op: "ttest.one",
        fields: [.cols("columns", "Variables (a test for each):", min: 1),
                 .num("mu0", "Hypothesized mean:", 0), .alternative(), .confidence()])
    static let twoSampleT = AnalysisSpec("2-Sample t", op: "ttest.two",
        fields: [.col("column1", "First sample:"), .col("column2", "Second sample:"),
                 .flag("pooled", "Assume equal variances (pooled)", false), .alternative(), .confidence()])
    static let pairedT = AnalysisSpec("Paired t", op: "ttest.paired",
        fields: [.col("column1", "First sample:"), .col("column2", "Second sample:"),
                 .alternative(), .confidence()])
    static let oneProportion = AnalysisSpec("1 Proportion", op: "prop.one",
        fields: [.int("events1", "Events:", 0), .int("trials1", "Trials:", 1),
                 .num("p0", "Hypothesized p:", 0.5), .alternative(), .confidence()])
    static let twoProportions = AnalysisSpec("2 Proportions", op: "prop.two",
        fields: [.int("events1", "Events 1:", 0), .int("trials1", "Trials 1:", 1),
                 .int("events2", "Events 2:", 0), .int("trials2", "Trials 2:", 1),
                 .alternative(), .confidence()])
    static let chiGof = AnalysisSpec("Chi-Square Goodness-of-Fit", op: "chisq.gof",
        fields: [.cols("columns", "Columns of observed counts:", min: 1)])
    static let chiAssoc = AnalysisSpec("Cross Tabulation & Chi-Square", op: "chisq.assoc",
        fields: [.cols("columns", "Columns forming the table:", min: 2)])
    static let corrPearson = AnalysisSpec("Correlation (Pearson)", op: "corr.pearson",
        fields: [.cols("columns", "Variables (2 or more):", min: 2)])
    static let corrSpearman = AnalysisSpec("Correlation (Spearman)", op: "corr.spearman",
        fields: [.cols("columns", "Variables (2 or more):", min: 2)])
    static let normality = AnalysisSpec("Normality Test", op: "normality",
        fields: [.cols("columns", "Variables to test:", min: 1)])
    static let twoVariances = AnalysisSpec("2 Variances (F-Test)", op: "var.two",
        fields: [.col("column1", "First sample:"), .col("column2", "Second sample:"), .confidence()])
    static let fisher = AnalysisSpec("Fisher's Exact Test (2×2)", op: "fisher",
        fields: [.int("a", "Cell a (row1,col1):", 0), .int("b", "Cell b (row1,col2):", 0),
                 .int("c", "Cell c (row2,col1):", 0), .int("d", "Cell d (row2,col2):", 0)])

    // ---- ANOVA -------------------------------------------------------------

    static let oneWayAnova = AnalysisSpec("One-Way ANOVA (with Tukey)", op: "anova.oneway",
        fields: [.cols("columns", "Response columns (each a group):", min: 2)])
    static let twoWayAnova = AnalysisSpec("Two-Way ANOVA", op: "anova.twoway",
        fields: [.col("response", "Response:"), .col("factorA", "Factor A:", numeric: false),
                 .col("factorB", "Factor B:", numeric: false)])
    static let equalVariances = AnalysisSpec("Test for Equal Variances", op: "var.equal",
        fields: [.cols("columns", "Group columns:", min: 2)])

    // ---- Nonparametrics ----------------------------------------------------

    static let mannWhitney = AnalysisSpec("Mann-Whitney", op: "np.mannwhitney",
        fields: [.col("column1", "First sample:"), .col("column2", "Second sample:"), .alternative()])
    static let wilcoxon = AnalysisSpec("Wilcoxon Signed-Rank", op: "np.wilcoxon",
        fields: [.cols("columns", "Variables:", min: 1), .num("mu0", "Hypothesized median:", 0), .alternative()])
    static let kruskal = AnalysisSpec("Kruskal-Wallis", op: "np.kruskal",
        fields: [.cols("columns", "Response columns (each a group):", min: 2)])
    static let signTest = AnalysisSpec("Sign Test for Median", op: "np.sign",
        fields: [.cols("columns", "Variables:", min: 1), .num("mu0", "Hypothesized median:", 0), .alternative()])
    static let runsTest = AnalysisSpec("Runs Test", op: "np.runs",
        fields: [.cols("columns", "Columns to test for randomness:", min: 1)])

    // ---- Regression --------------------------------------------------------

    static let simpleReg = AnalysisSpec("Simple Regression", op: "reg.simple",
        fields: [.col("y", "Response (Y):"), .col("x", "Predictor (X):")])
    static let multipleReg = AnalysisSpec("Multiple Regression", op: "reg.multiple",
        fields: [.col("response", "Response:"), .cols("predictors", "Predictors:", min: 1)])
    static let polyReg = AnalysisSpec("Polynomial Regression", op: "reg.poly",
        fields: [.col("y", "Response (Y):"), .col("x", "Predictor (X):"), .int("degree", "Degree:", 2)])
    static let bestSubsets = AnalysisSpec("Best Subsets", op: "reg.bestsubsets",
        fields: [.col("response", "Response:"), .cols("predictors", "Candidate predictors:", min: 1)])
    static let stepwise = AnalysisSpec("Stepwise", op: "reg.stepwise",
        fields: [.col("response", "Response:"), .cols("predictors", "Candidate predictors:", min: 1)])
    static let logistic = AnalysisSpec("Binary Logistic Regression", op: "reg.logistic",
        fields: [.col("response", "Response (0/1):"), .cols("predictors", "Predictors:", min: 1)])

    // ---- Time Series -------------------------------------------------------

    static let trend = AnalysisSpec("Trend Analysis", op: "ts.trend",
        fields: [.col("column", "Series:"), .flag("quadratic", "Quadratic trend", false), .int("forecasts", "Forecasts:", 0)])
    static let movingAvg = AnalysisSpec("Moving Average", op: "ts.movavg",
        fields: [.col("column", "Series:"), .int("length", "MA length:", 3), .int("forecasts", "Forecasts:", 0)])
    static let singleExp = AnalysisSpec("Single Exp Smoothing", op: "ts.singleexp",
        fields: [.col("column", "Series:"), .num("alpha", "Alpha:", 0.2), .int("forecasts", "Forecasts:", 0)])
    static let doubleExp = AnalysisSpec("Double Exp Smoothing", op: "ts.doubleexp",
        fields: [.col("column", "Series:"), .num("alpha", "Alpha:", 0.2), .num("beta", "Beta:", 0.1), .int("forecasts", "Forecasts:", 0)])
    static let winters = AnalysisSpec("Winters' Method", op: "ts.winters",
        fields: [.col("column", "Series:"), .int("period", "Season length:", 12),
                 .num("alpha", "Alpha:", 0.2), .num("beta", "Beta:", 0.1), .num("gamma", "Gamma:", 0.1),
                 .flag("multiplicative", "Multiplicative", false), .int("forecasts", "Forecasts:", 0)])
    static let decomposition = AnalysisSpec("Decomposition", op: "ts.decomp",
        fields: [.col("column", "Series:"), .int("period", "Season length:", 12), .flag("multiplicative", "Multiplicative", false)])
    static let acf = AnalysisSpec("Autocorrelation (ACF)", op: "ts.acf",
        fields: [.col("column", "Series:"), .int("maxlag", "Max lag:", 20)])
    static let pacf = AnalysisSpec("Partial Autocorrelation (PACF)", op: "ts.pacf",
        fields: [.col("column", "Series:"), .int("maxlag", "Max lag:", 20)])
    static let arima = AnalysisSpec("ARIMA", op: "ts.arima",
        fields: [.col("column", "Series:"), .int("p", "p (AR):", 1), .int("d", "d (diff):", 1), .int("q", "q (MA):", 1),
                 .int("forecasts", "Forecasts:", 10), .flag("includeConstant", "Include constant", true)])
    static let sarima = AnalysisSpec("SARIMA (Seasonal)", op: "ts.sarima",
        fields: [.col("column", "Series:"), .int("p", "p:", 1), .int("d", "d:", 1), .int("q", "q:", 1),
                 .int("sp", "P (seasonal):", 0), .int("sd", "D (seasonal):", 1), .int("sq", "Q (seasonal):", 1),
                 .int("season", "Season length:", 12), .int("forecasts", "Forecasts:", 12),
                 .flag("includeConstant", "Include constant", false)])

    // ---- Multivariate / Reliability / Power / Bayes / Mixed ----------------

    static let pca = AnalysisSpec("Principal Components", op: "mv.pca",
        fields: [.cols("columns", "Variables (2 or more):", min: 2)])
    static let factor = AnalysisSpec("Factor Analysis", op: "mv.factor",
        fields: [.cols("columns", "Variables:", min: 2), .int("factors", "Number of factors:", 2),
                 .flag("varimax", "Varimax rotation", true)])
    static let kmeans = AnalysisSpec("Cluster K-Means", op: "mv.kmeans",
        fields: [.cols("columns", "Variables:", min: 1), .int("k", "Number of clusters (k):", 3)])
    static let distFit = AnalysisSpec("Distribution Analysis", op: "rel.distfit",
        fields: [.col("column", "Times/Life column:"),
                 .choice("distribution", "Distribution:", [
                    ("Weibull", .string("Weibull")), ("Exponential", .string("Exponential")),
                    ("Lognormal", .string("Lognormal")), ("Normal", .string("Normal"))])])
    static let kaplanMeier = AnalysisSpec("Kaplan-Meier", op: "rel.kaplanmeier",
        fields: [.col("column", "Times column:"), .optcol("censor", "Censor column (1 = censored):")])
    static let power = AnalysisSpec("Power and Sample Size", op: "power",
        fields: [.choice("testIndex", "Test:", [("1-Sample t", .int(0)), ("2-Sample t", .int(1)), ("1 Proportion", .int(2))]),
                 .flag("solveForPower", "Solve for power (else sample size)", false),
                 .num("alpha", "Alpha:", 0.05), .alternative(),
                 .num("effectSize", "Effect size d (t-tests):", 0.5),
                 .num("p0", "p0 (proportion):", 0.5), .num("p1", "p1 (proportion):", 0.6),
                 .num("n", "n (when solving power):", 30), .num("targetPower", "Target power:", 0.8)])
    static let bayesProp = AnalysisSpec("Bayesian Proportion", op: "bayes.prop",
        fields: [.int("x", "Events:", 0), .int("n", "Trials:", 1),
                 .num("priorA", "Prior alpha:", 1), .num("priorB", "Prior beta:", 1),
                 .confidence(), .num("threshold", "Threshold p:", 0.5)])
    static let bayesNormal = AnalysisSpec("Bayesian 1-Sample Normal Mean", op: "bayes.normal",
        fields: [.col("column", "Data column:"), .flag("knownVariance", "Known variance", false),
                 .num("priorMean", "Prior mean:", 0), .num("priorSd", "Prior SD:", 1),
                 .num("knownSigma", "Known sigma:", 1), .confidence(), .num("threshold", "Threshold:", 0)])
    static let bayesReg = AnalysisSpec("Bayesian Linear Regression", op: "bayes.reg",
        fields: [.col("response", "Response:"), .cols("predictors", "Predictors:", min: 1)])
    static let oneWayRandom = AnalysisSpec("One-Way Random Effects", op: "mixed.onewayrandom",
        fields: [.cols("columns", "Group columns (random-effect levels):", min: 2)])

    // ---- Control Charts / Quality ------------------------------------------

    static let xbarR = AnalysisSpec("Xbar-R Chart", op: "spc.xbarr",
        fields: [.cols("columns", "Subgroup columns:", min: 2)])
    static let xbarS = AnalysisSpec("Xbar-S Chart", op: "spc.xbars",
        fields: [.cols("columns", "Subgroup columns:", min: 2)])
    static let imr = AnalysisSpec("I-MR Chart", op: "spc.imr",
        fields: [.col("column", "Individual measurements:")])
    static let pChart = AnalysisSpec("P Chart", op: "spc.p",
        fields: [.col("counts", "Defective counts:"), .col("sizes", "Sample sizes:")])
    static let npChart = AnalysisSpec("NP Chart", op: "spc.np",
        fields: [.col("counts", "Defective counts:"), .int("size", "Constant sample size:", 50)])
    static let cChart = AnalysisSpec("C Chart", op: "spc.c",
        fields: [.col("counts", "Defect counts:")])
    static let uChart = AnalysisSpec("U Chart", op: "spc.u",
        fields: [.col("counts", "Defect counts:"), .col("sizes", "Sample sizes:")])
    static let capability = AnalysisSpec("Capability Analysis (Normal)", op: "qual.capability",
        fields: [.col("column", "Measurement column:"), .optnum("lsl", "LSL (blank = none):"),
                 .optnum("usl", "USL (blank = none):"), .optnum("target", "Target (blank = none):")])
    static let gageRR = AnalysisSpec("Gage R&R (Crossed)", op: "qual.gagerr",
        fields: [.col("response", "Measurement:"), .col("part", "Part:", numeric: false),
                 .col("operator", "Operator:", numeric: false)])

    // ---- DOE ---------------------------------------------------------------

    static let createFactorial = AnalysisSpec("Create Factorial Design", op: "doe.factorial.create",
        fields: [.int("factors", "Number of factors:", 2), .int("replicates", "Replicates:", 1),
                 .int("centerPoints", "Center points:", 0), .flag("randomize", "Randomize run order", false)])
    static let createFractional = AnalysisSpec("Create Fractional Factorial", op: "doe.fractional.create",
        fields: [.int("factors", "Number of factors:", 4), .int("runs", "Runs:", 8),
                 .flag("randomize", "Randomize run order", false)])
    static let analyzeFactorial = AnalysisSpec("Analyze Factorial Design", op: "doe.factorial.analyze",
        fields: [.col("response", "Response:"), .cols("predictors", "Factors:", min: 2)])
    static let createRsm = AnalysisSpec("Create Response Surface", op: "doe.rsm.create",
        fields: [.int("factors", "Number of factors:", 2), .int("centerPoints", "Center points:", 3),
                 .flag("boxBehnken", "Box-Behnken (else central composite)", false),
                 .flag("faceCentered", "Face-centered (CCD)", false), .flag("randomize", "Randomize", false)])
    static let analyzeRsm = AnalysisSpec("Analyze Response Surface", op: "doe.rsm.analyze",
        fields: [.col("response", "Response:"), .cols("predictors", "Factors:", min: 2)])
    static let createMixture = AnalysisSpec("Create Mixture Design", op: "doe.mixture.create",
        fields: [.int("components", "Components:", 3), .int("degree", "Lattice degree:", 2),
                 .flag("lattice", "Simplex-lattice (else centroid)", true), .flag("randomize", "Randomize", false)])
    static let analyzeMixture = AnalysisSpec("Analyze Mixture Design", op: "doe.mixture.analyze",
        fields: [.col("response", "Response:"), .cols("predictors", "Components:", min: 2)])

    // ---- Calc --------------------------------------------------------------

    static let calculator = AnalysisSpec("Calculator", op: "calc.evaluate",
        fields: [.text("targetColumn", "Store result in column:"), .text("expression", "Expression:")])

    // ---- Graph -------------------------------------------------------------

    static let histogram = AnalysisSpec("Histogram", op: "graph.histogram",
        fields: [.cols("columns", "Graph variables:", min: 1)])
    static let boxplot = AnalysisSpec("Boxplot", op: "graph.boxplot",
        fields: [.cols("columns", "Graph variables:", min: 1)])
    static let scatter = AnalysisSpec("Scatterplot", op: "graph.scatter",
        fields: [.col("y", "Y variable:"), .col("x", "X variable:")])
    static let timeSeries = AnalysisSpec("Time Series Plot", op: "graph.timeseries",
        fields: [.cols("columns", "Graph variables:", min: 1)])
    static let probPlot = AnalysisSpec("Probability Plot", op: "graph.probplot",
        fields: [.cols("columns", "Graph variables:", min: 1)])
}
