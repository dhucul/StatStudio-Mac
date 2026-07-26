using StatStudio.Core.Data;
using StatStudio.Core.Statistics;
using StatStudio.Engine.Graphs;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>Multivariate, reliability/survival, power &amp; sample size, Bayesian, mixed model.</summary>
internal static class OpsMultivariate
{
    public static void Pca(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var names = Strings(req, "columns");
        var cols = names.Select(n => Require(ws, n)).ToList();
        if (cols.Count < 2) throw new ArgumentException("Select at least two variables.");
        var rows = Columns.Rows(cols);
        if (rows.Count < 2) throw new ArgumentException("Not enough complete rows.");
        var r = StatStudio.Core.Statistics.Pca.Compute(rows.ToArray(), names, correlation: true);
        res.StatusTitle = "Principal Components";
        res.SessionText = Out.Raw(MultivariateFormatters.Pca(r));
        res.AddGraph("Scree Plot", p => Plots.Scree(p, r.Eigenvalues));
    }

    public static void Factor(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var names = Strings(req, "columns");
        var cols = names.Select(n => Require(ws, n)).ToList();
        var rows = Columns.Rows(cols);
        if (rows.Count < 2) throw new ArgumentException("Not enough complete rows.");
        var r = FactorAnalysis.Extract(rows.ToArray(), names, PositiveInt(req, "factors", 2), Bool(req, "varimax", true));
        res.StatusTitle = "Factor Analysis";
        res.SessionText = Out.Raw(MultivariateFormatters.FactorAnalysis(r));
    }

    public static void KMeans(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var names = Strings(req, "columns");
        var cols = names.Select(n => Require(ws, n)).ToList();
        var rows = Columns.Rows(cols);
        int k = PositiveInt(req, "k", 3);
        if (rows.Count < k) throw new ArgumentException("Need at least k complete rows.");
        var r = StatStudio.Core.Statistics.KMeans.Cluster(rows.ToArray(), k, names);
        res.StatusTitle = "K-Means Clustering";
        res.SessionText = Out.Raw(MultivariateFormatters.KMeans(r));
        if (names.Length == 2)
            res.AddGraph("K-Means Clusters",
                p => Plots.ClusterScatter(p, names[0], names[1], rows.ToArray(), r.Assignments, r.K));
    }

    public static void DistFit(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var col = StrReq(req, "column");
        var t = Require(ws, col).NumericValues();
        if (t.Length < 3) throw new ArgumentException("Need at least 3 observations.");
        var dist = Str(req, "distribution") ?? "Weibull";
        if (dist is not ("Weibull" or "Exponential" or "Lognormal" or "Normal"))
            throw new ArgumentException($"Unknown distribution '{dist}'.");
        if (dist != "Normal" && t.Any(v => v <= 0))
            throw new ArgumentException($"{dist} requires all times > 0.");
        var fit = dist switch
        {
            "Exponential" => Reliability.FitExponential(t),
            "Lognormal" => Reliability.FitLognormal(t),
            "Normal" => Reliability.FitNormal(t),
            _ => Reliability.FitWeibull(t),
        };
        res.StatusTitle = $"Distribution Analysis of {col}";
        res.SessionText = Out.Raw(ReliabilityFormatters.DistributionFit(fit, col));
        if (dist == "Weibull")
        {
            double beta = fit.Parameters[0].Value, eta = fit.Parameters[1].Value;
            res.AddGraph($"Weibull Plot of {col}", p => Plots.WeibullPlot(p, col, t, beta, eta));
        }
        else res.AddGraph($"Histogram of {col}", p => Plots.Histogram(p, col, t));
    }

    public static void KaplanMeier(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var timesCol = StrReq(req, "column");
        var censorCol = Str(req, "censor");
        double[] times;
        bool[] censored;
        if (string.IsNullOrEmpty(censorCol))
        {
            times = Require(ws, timesCol).NumericValues();
            censored = new bool[times.Length];
        }
        else
        {
            var (tv, cv) = Columns.Pairwise(Require(ws, timesCol), Require(ws, censorCol));
            if (cv.Any(v => v != 0 && v != 1))
                throw new ArgumentException("Censor values must be 0 (event) or 1 (censored).");
            times = tv;
            censored = cv.Select(v => v == 1).ToArray();
        }
        if (times.Length < 2) throw new ArgumentException("Need at least 2 observations.");
        if (times.Any(v => v < 0)) throw new ArgumentException("Survival times must be non-negative.");
        var km = Reliability.KaplanMeier(times, censored);
        res.StatusTitle = $"Kaplan-Meier Survival of {timesCol}";
        res.SessionText = Out.Raw(ReliabilityFormatters.KaplanMeier(km, timesCol));
        res.AddGraph($"Kaplan-Meier Survival of {timesCol}",
            p => Plots.StepSurvival(p, timesCol, km.Rows.Select(r => r.Time).ToArray(),
                                    km.Rows.Select(r => r.Survival).ToArray()));
    }

    public static void PowerSampleSize(EngineRequest req, EngineResponse res)
    {
        int testIndex = NonNegativeInt(req, "testIndex", 0);
        if (testIndex > 2) throw new ArgumentException("'testIndex' must be 0, 1, or 2.");
        bool solveForPower = Bool(req, "solveForPower");
        double alpha = Probability(req, "alpha", 0.05, open: true);
        var alt = Alt(req);
        string test = testIndex switch { 1 => "2-Sample t", 2 => "1 Proportion", _ => "1-Sample t" };
        string solveFor = solveForPower ? "power" : "sample size";
        double n, power;
        string effectDesc;

        if (testIndex == 2)
        {
            double p0 = Probability(req, "p0", 0.5), p1 = Probability(req, "p1", 0.6);
            if (p0 == p1) throw new ArgumentException("'p0' and 'p1' must differ.");
            if ((alt == Alternative.Greater && p1 <= p0) || (alt == Alternative.Less && p1 >= p0))
                throw new ArgumentException("'p1' must point in the direction of the one-sided alternative.");
            effectDesc = $"p0 = {p0}, p1 = {p1}";
            if (solveForPower) { n = PositiveNum(req, "n", 30); power = Power.OneProportionPower(n, p0, p1, alpha, alt); }
            else { power = Probability(req, "targetPower", 0.8, open: true); n = Power.OneProportionSampleSize(power, p0, p1, alpha, alt); }
        }
        else
        {
            double d = PositiveNum(req, "effectSize", 0.5);
            effectDesc = $"d = {d}";
            bool two = testIndex == 1;
            if (solveForPower)
            {
                n = PositiveNum(req, "n", 30);
                power = two ? Power.TwoSampleTPower(n, d, alpha, alt) : Power.OneSampleTPower(n, d, alpha, alt);
            }
            else
            {
                power = Probability(req, "targetPower", 0.8, open: true);
                n = two ? Power.TwoSampleTSampleSize(power, d, alpha, alt) : Power.OneSampleTSampleSize(power, d, alpha, alt);
            }
        }
        res.StatusTitle = "Power and Sample Size";
        res.SessionText = Out.Raw(MultivariateFormatters.Power(test, solveFor, alpha, alt, effectDesc, n, power));
    }

    public static void BayesProportion(EngineRequest req, EngineResponse res)
    {
        int x = NonNegativeInt(req, "x", 0), n = PositiveInt(req, "n", 1);
        if (x > n) throw new ArgumentException("'x' cannot exceed 'n'.");
        var r = Bayes.Proportion(x, n, PositiveNum(req, "priorA", 1), PositiveNum(req, "priorB", 1),
            Conf(req), Probability(req, "threshold", 0.5));
        res.StatusTitle = "Bayesian Proportion";
        res.SessionText = Out.Raw(BayesFormatters.Proportion(r, "Sample"));
    }

    public static void BayesNormal(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var col = StrReq(req, "column");
        var v = Require(ws, col).NumericValues();
        if (v.Length < 2) throw new ArgumentException("Need at least 2 values.");
        var r = Bool(req, "knownVariance")
            ? Bayes.NormalMeanKnownVar(v, Num(req, "priorMean", 0), PositiveNum(req, "priorSd", 1),
                                       PositiveNum(req, "knownSigma", 1), Conf(req), Num(req, "threshold", 0))
            : Bayes.NormalMeanUnknownVar(v, Conf(req), Num(req, "threshold", 0));
        res.StatusTitle = "Bayesian Normal Mean";
        res.SessionText = Out.Raw(BayesFormatters.NormalMean(r, col));
    }

    public static void BayesRegression(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var response = StrReq(req, "response");
        var names = Strings(req, "predictors");
        var (y, x) = Columns.Design(Require(ws, response), names.Select(n => Require(ws, n)).ToList());
        if (y.Length <= names.Length + 1) throw new ArgumentException("Not enough complete rows.");
        res.StatusTitle = "Bayesian Linear Regression";
        res.SessionText = Out.Raw(BayesFormatters.Regression(Bayes.LinearRegression(y, x, names.ToList(), response)));
    }

    public static void OneWayRandom(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var groups = Strings(req, "columns").Select(n => (n, Require(ws, n).NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) throw new ArgumentException("Need at least 2 groups.");
        res.StatusTitle = "One-Way Random Effects";
        res.SessionText = Out.Raw(MixedFormatters.OneWayRandom(MixedModel.OneWayRandom(groups), "Response", "Group"));
    }
}
