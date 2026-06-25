using System.Text;
using StatStudio.Core.Statistics;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>ANOVA: one-way (+Tukey), two-way, test for equal variances.</summary>
internal static class OpsAnova
{
    public static void OneWay(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var groups = Strings(req, "columns").Select(n => (n, Require(ws, n).NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) throw new ArgumentException("Need at least 2 non-empty groups.");
        var sb = new StringBuilder();
        sb.Append(Out.Raw(AnovaFormatter.OneWay(Anova.OneWay(groups), "Factor", "Response")));
        try { sb.Append(Out.Raw(AdvancedFormatters.Tukey(AnovaExtensions.Tukey(groups)))); }
        catch (Exception ex) { sb.Append(Out.Raw($"Tukey: {ex.Message}")); }
        res.StatusTitle = "One-Way ANOVA";
        res.SessionText = sb.ToString();
    }

    public static void TwoWay(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var resp = StrReq(req, "response");
        var facA = StrReq(req, "factorA"); var facB = StrReq(req, "factorB");
        var (y, a, b) = Columns.Factorial(Require(ws, resp), Require(ws, facA), Require(ws, facB));
        if (y.Length < 4) throw new ArgumentException("Not enough complete rows.");
        res.StatusTitle = "Two-Way ANOVA";
        res.SessionText = Out.Raw(AdvancedFormatters.TwoWayAnova(AnovaExtensions.TwoWay(y, a, b, facA, facB), resp));
    }

    public static void EqualVariances(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var groups = Strings(req, "columns").Select(n => (n, Require(ws, n).NumericValues()))
            .Where(t => t.Item2.Length > 1).ToList();
        if (groups.Count < 2) throw new ArgumentException("Need at least 2 groups with >1 value.");
        res.StatusTitle = "Test for Equal Variances";
        res.SessionText = Out.Raw(AdvancedFormatters.EqualVariances(
            VarianceTests.EqualVariances(groups), "Response", "Factor"));
    }
}
