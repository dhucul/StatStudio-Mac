using System.Text;
using StatStudio.Core.Statistics;
using StatStudio.Engine.Graphs;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>Basic Statistics: descriptives, t-tests, proportions, chi-square, correlation,
/// normality, F-test, Fisher's exact.</summary>
internal static class OpsBasic
{
    public static void Descriptives(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var cols = Strings(req, "columns");
        if (cols.Length == 0) cols = ws.NumericColumns().Select(c => c.Name).ToArray();
        var stats = cols.Select(n => StatStudio.Core.Statistics.Descriptives.Compute(Require(ws, n))).ToList();
        res.StatusTitle = "Descriptive Statistics";
        res.SessionText = Out.Block("Descriptive Statistics", DescriptivesFormatter.Format(stats));
    }

    public static void OneSampleT(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        double mu0 = Num(req, "mu0", 0);
        var sb = new StringBuilder();
        foreach (var n in Strings(req, "columns"))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length < 2) { sb.Append(Out.Raw($"{n}: need at least 2 values.")); continue; }
            var r = HypothesisTests.OneSampleT(v, mu0, Conf(req), Alt(req));
            sb.Append(Out.Raw(HypothesisFormatters.OneSampleT(r, n, mu0)));
        }
        res.StatusTitle = "1-Sample t";
        res.SessionText = sb.ToString();
    }

    public static void TwoSampleT(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var c1 = StrReq(req, "column1"); var c2 = StrReq(req, "column2");
        var x1 = Require(ws, c1).NumericValues(); var x2 = Require(ws, c2).NumericValues();
        if (x1.Length < 2 || x2.Length < 2) throw new ArgumentException("Each sample needs at least 2 values.");
        var r = HypothesisTests.TwoSampleT(x1, x2, Bool(req, "pooled"), Conf(req), Alt(req));
        res.StatusTitle = "2-Sample t";
        res.SessionText = Out.Raw(HypothesisFormatters.TwoSampleT(r, c1, c2));
    }

    public static void PairedT(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var c1 = StrReq(req, "column1"); var c2 = StrReq(req, "column2");
        var (x1, x2) = Columns.Pairwise(Require(ws, c1), Require(ws, c2));
        if (x1.Length < 2) throw new ArgumentException("Need at least 2 paired observations.");
        var r = HypothesisTests.PairedT(x1, x2, Conf(req), Alt(req));
        res.StatusTitle = "Paired t";
        res.SessionText = Out.Raw(HypothesisFormatters.PairedT(r, c1, c2));
    }

    public static void OneProportion(EngineRequest req, EngineResponse res)
    {
        double p0 = Probability(req, "p0", 0.5);
        int events = NonNegativeInt(req, "events1", 0);
        int trials = PositiveInt(req, "trials1", 1);
        if (events > trials) throw new ArgumentException("'events1' cannot exceed 'trials1'.");
        var r = HypothesisTests.OneProportion(events, trials, p0, Conf(req), Alt(req));
        res.StatusTitle = "1 Proportion";
        res.SessionText = Out.Raw(HypothesisFormatters.OneProportion(r, "Sample", p0));
    }

    public static void TwoProportions(EngineRequest req, EngineResponse res)
    {
        int e1 = NonNegativeInt(req, "events1", 0), n1 = PositiveInt(req, "trials1", 1);
        int e2 = NonNegativeInt(req, "events2", 0), n2 = PositiveInt(req, "trials2", 1);
        if (e1 > n1 || e2 > n2) throw new ArgumentException("Events cannot exceed trials.");
        var r = HypothesisTests.TwoProportions(e1, n1, e2, n2, Conf(req), Alt(req));
        res.StatusTitle = "2 Proportions";
        res.SessionText = Out.Raw(HypothesisFormatters.TwoProportions(r, "Sample 1", "Sample 2"));
    }

    public static void ChiSquareGof(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var sb = new StringBuilder();
        foreach (var n in Strings(req, "columns"))
        {
            var obs = Require(ws, n).NumericValues();
            if (obs.Length < 2) { sb.Append(Out.Raw($"{n}: need at least 2 categories.")); continue; }
            if (obs.Any(v => v < 0) || obs.Sum() <= 0)
                throw new ArgumentException($"{n}: observed counts must be non-negative with a positive total.");
            var r = HypothesisTests.ChiSquareGof(obs);
            var cats = Enumerable.Range(1, obs.Length).Select(i => i.ToString()).ToList();
            sb.Append(Out.Raw($"Goodness-of-Fit for {n}\n" + HypothesisFormatters.ChiSquareGof(r, cats)));
        }
        res.StatusTitle = "Chi-Square Goodness-of-Fit";
        res.SessionText = sb.ToString();
    }

    public static void ChiSquareAssoc(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var names = Strings(req, "columns");
        if (names.Length < 2) throw new ArgumentException("Select at least two count columns.");
        var complete = Columns.Rows(names.Select(n => Require(ws, n)).ToList());
        if (complete.Count < 2) throw new ArgumentException("Need at least 2 complete rows of counts.");
        if (complete.Any(row => row.Any(v => v < 0)))
            throw new ArgumentException("Chi-square counts must be non-negative.");
        int rows = complete.Count;
        var table = new double[rows, names.Length];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < names.Length; j++) table[i, j] = complete[i][j];
        if (Enumerable.Range(0, rows).Any(i => Enumerable.Range(0, names.Length).Sum(j => table[i, j]) <= 0) ||
            Enumerable.Range(0, names.Length).Any(j => Enumerable.Range(0, rows).Sum(i => table[i, j]) <= 0))
            throw new ArgumentException("Every chi-square row and column must have a positive total.");
        var r = HypothesisTests.ChiSquareAssociation(table);
        var rowLabels = Enumerable.Range(1, rows).Select(i => $"R{i}").ToList();
        res.StatusTitle = "Cross Tabulation & Chi-Square";
        res.SessionText = Out.Raw(HypothesisFormatters.Contingency(r, rowLabels, names.ToList()));
    }

    public static void Correlation(EngineRequest req, EngineResponse res, bool spearman)
    {
        var ws = Ws(req);
        var cols = Strings(req, "columns").Select(n => Require(ws, n)).ToList();
        if (cols.Count < 2) throw new ArgumentException("Select at least two variables.");
        res.StatusTitle = spearman ? "Correlation (Spearman)" : "Correlation (Pearson)";
        res.SessionText = Out.Raw(NonparametricFormatters.Correlation(
            StatStudio.Core.Statistics.Correlation.Matrix(cols, spearman)));
    }

    public static void Normality(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var sb = new StringBuilder();
        foreach (var n in Strings(req, "columns"))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length < 3) { sb.Append(Out.Raw($"{n}: need at least 3 values.")); continue; }
            sb.Append(Out.Raw(NonparametricFormatters.AndersonDarling(
                StatStudio.Core.Statistics.Normality.AndersonDarling(v), n)));
            res.AddGraph($"Probability Plot of {n}", p => Plots.ProbabilityPlot(p, n, v));
        }
        res.StatusTitle = "Normality Test";
        res.SessionText = sb.ToString();
    }

    public static void TwoVariances(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var c1 = StrReq(req, "column1"); var c2 = StrReq(req, "column2");
        var x1 = Require(ws, c1).NumericValues(); var x2 = Require(ws, c2).NumericValues();
        if (x1.Length < 2 || x2.Length < 2) throw new ArgumentException("Each sample needs at least 2 values.");
        res.StatusTitle = "2 Variances (F-Test)";
        res.SessionText = Out.Raw(AdvancedFormatters.FTest(VarianceTests.FTest(x1, x2, Conf(req)), c1, c2));
    }

    public static void Fisher(EngineRequest req, EngineResponse res)
    {
        int a = NonNegativeInt(req, "a", 0), b = NonNegativeInt(req, "b", 0);
        int c = NonNegativeInt(req, "c", 0), d = NonNegativeInt(req, "d", 0);
        if ((long)a + b + c + d == 0) throw new ArgumentException("The contingency table must contain observations.");
        var r = FishersExact.Test(a, b, c, d);
        res.StatusTitle = "Fisher's Exact Test";
        res.SessionText = Out.Raw(MultivariateFormatters.Fisher(r));
    }
}
