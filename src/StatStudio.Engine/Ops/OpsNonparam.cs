using System.Text;
using StatStudio.Core.Statistics;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>Nonparametrics: Mann-Whitney, Wilcoxon, Kruskal-Wallis, sign, runs.</summary>
internal static class OpsNonparam
{
    public static void MannWhitney(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var c1 = StrReq(req, "column1"); var c2 = StrReq(req, "column2");
        var x1 = Require(ws, c1).NumericValues(); var x2 = Require(ws, c2).NumericValues();
        if (x1.Length < 1 || x2.Length < 1) throw new ArgumentException("Each sample needs data.");
        res.StatusTitle = "Mann-Whitney";
        res.SessionText = Out.Raw(NonparametricFormatters.MannWhitney(
            Nonparametric.MannWhitney(x1, x2, Alt(req)), c1, c2));
    }

    public static void Wilcoxon(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        double mu0 = Num(req, "mu0", 0);
        var sb = new StringBuilder();
        foreach (var n in Strings(req, "columns"))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length < 2) { sb.Append(Out.Raw($"{n}: need at least 2 values.")); continue; }
            sb.Append(Out.Raw(NonparametricFormatters.Wilcoxon(
                Nonparametric.WilcoxonSignedRank(v, mu0, Alt(req)), n, mu0)));
        }
        res.StatusTitle = "Wilcoxon Signed-Rank";
        res.SessionText = sb.ToString();
    }

    public static void KruskalWallis(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var groups = Strings(req, "columns").Select(n => (n, Require(ws, n).NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (groups.Count < 2) throw new ArgumentException("Need at least 2 non-empty groups.");
        res.StatusTitle = "Kruskal-Wallis";
        res.SessionText = Out.Raw(NonparametricFormatters.KruskalWallis(
            Nonparametric.KruskalWallis(groups), "Response", "Factor"));
    }

    public static void SignTest(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        double mu0 = Num(req, "mu0", 0);
        var sb = new StringBuilder();
        foreach (var n in Strings(req, "columns"))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length < 1) { sb.Append(Out.Raw($"{n}: no data.")); continue; }
            sb.Append(Out.Raw(NonparametricFormatters.SignTest(
                Nonparametric.SignTest(v, mu0, Alt(req)), n, mu0)));
        }
        res.StatusTitle = "Sign Test";
        res.SessionText = sb.ToString();
    }

    public static void RunsTest(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var sb = new StringBuilder();
        foreach (var n in Strings(req, "columns"))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length < 3) { sb.Append(Out.Raw($"{n}: need at least 3 values.")); continue; }
            sb.Append(Out.Raw(NonparametricFormatters.RunsTest(Nonparametric.RunsTest(v), n)));
        }
        res.StatusTitle = "Runs Test";
        res.SessionText = sb.ToString();
    }
}
