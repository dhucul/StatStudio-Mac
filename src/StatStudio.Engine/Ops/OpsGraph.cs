using StatStudio.Core.Data;
using StatStudio.Core.Statistics;
using StatStudio.Engine.Graphs;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>The Graph menu: histogram, boxplot, scatterplot, time series, probability plot.</summary>
internal static class OpsGraph
{
    private static string[] Cols(EngineRequest req, Worksheet ws)
    {
        var many = Strings(req, "columns");
        if (many.Length > 0) return many;
        var one = Str(req, "column");
        return one is not null ? new[] { one } : ws.NumericColumns().Select(c => c.Name).ToArray();
    }

    public static void Histogram(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        foreach (var n in Cols(req, ws))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length == 0) continue;
            res.AddGraph($"Histogram of {n}", p => Plots.Histogram(p, n, v));
        }
        Done(res, "Histogram");
    }

    public static void Boxplot(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var series = Cols(req, ws).Select(n => (n, Require(ws, n).NumericValues()))
            .Where(t => t.Item2.Length > 0).ToList();
        if (series.Count == 0) throw new ArgumentException("No data to plot.");
        res.StatusTitle = series.Count == 1 ? $"Boxplot of {series[0].Item1}" : "Boxplot";
        res.AddGraph(res.StatusTitle, p => Plots.Boxplot(p, series));
    }

    public static void Scatter(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var xn = StrReq(req, "x"); var yn = StrReq(req, "y");
        var (xs, ys) = Columns.Pairwise(Require(ws, xn), Require(ws, yn));
        if (xs.Length == 0) throw new ArgumentException("No paired (X, Y) rows to plot.");
        res.StatusTitle = $"Scatterplot of {yn} vs {xn}";
        res.AddGraph(res.StatusTitle, p => Plots.Scatter(p, xn, yn, xs, ys));
    }

    public static void TimeSeries(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        foreach (var n in Cols(req, ws))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length == 0) continue;
            res.AddGraph($"Time Series Plot of {n}", p => Plots.TimeSeries(p, n, v));
        }
        Done(res, "Time Series Plot");
    }

    public static void ProbPlot(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        foreach (var n in Cols(req, ws))
        {
            var v = Require(ws, n).NumericValues();
            if (v.Length < 3) continue;
            res.AddGraph($"Probability Plot of {n}", p => Plots.ProbabilityPlot(p, n, v));
        }
        Done(res, "Probability Plot");
    }

    private static void Done(EngineResponse res, string title)
    {
        res.StatusTitle = title;
        res.SessionText = Out.Raw($"{title}: {res.Graphs?.Count ?? 0} graph(s) created.");
    }
}
