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
        double p0 = Num(req, "p0", 0.5);
        var r = HypothesisTests.OneProportion(Int(req, "events1", 0), Int(req, "trials1", 1), p0, Conf(req), Alt(req));
        res.StatusTitle = "1 Proportion";
        res.SessionText = Out.Raw(HypothesisFormatters.OneProportion(r, "Sample", p0));
    }

    public static void TwoProportions(EngineRequest req, EngineResponse res)
    {
        var r = HypothesisTests.TwoProportions(Int(req, "events1", 0), Int(req, "trials1", 1),
                                               Int(req, "events2", 0), Int(req, "trials2", 1), Conf(req), Alt(req));
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
        var cols = names.Select(n => Require(ws, n).NumericValues()).ToList();
        int rows = cols.Min(c => c.Length);
        if (rows < 2) throw new ArgumentException("Need at least 2 rows of counts.");
        var table = new double[rows, cols.Count];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols.Count; j++) table[i, j] = cols[j][i];
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
        var r = FishersExact.Test(Int(req, "a", 0), Int(req, "b", 0), Int(req, "c", 0), Int(req, "d", 0));
        res.StatusTitle = "Fisher's Exact Test";
        res.SessionText = Out.Raw(MultivariateFormatters.Fisher(r));
    }
}
