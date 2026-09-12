using StatStudio.Core.Data;
using StatStudio.Core.Statistics;
using StatStudio.Core.Statistics.Spc;
using StatStudio.Engine.Graphs;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>Control charts (variables + attributes), capability, Gage R&amp;R.</summary>
internal static class OpsSpc
{
    private static int CheckedCount(double value, string label)
    {
        double rounded = Math.Round(value);
        if (!double.IsFinite(value) || value < 0 || Math.Abs(value - rounded) > 1e-9 || rounded > int.MaxValue)
            throw new ArgumentException($"{label} values must be non-negative integers.");
        return (int)rounded;
    }

    private static int[] IntColumn(Worksheet ws, string name) =>
        Require(ws, name).OrderedNumericValues().Select(v => CheckedCount(v, name)).ToArray();

    public static void VariablesChart(EngineRequest req, EngineResponse res, bool useRange)
    {
        var ws = Ws(req);
        var cols = Strings(req, "columns").Select(n => Require(ws, n)).ToList();
        if (cols.Count < 2) throw new ArgumentException("Need at least 2 subgroup columns.");
        var subgroups = Columns.Rows(cols, requireContiguous: true);
        var (mean, spread) = useRange ? ControlCharts.XbarR(subgroups) : ControlCharts.XbarS(subgroups);
        string kind = useRange ? "Xbar-R" : "Xbar-S";
        res.StatusTitle = $"{kind} Chart";
        res.SessionText = Out.Raw(SpcFormatter.Pair($"{kind} Chart", mean, spread));
        res.AddGraph(mean.Title, p => Plots.ControlChart(p, mean));
        res.AddGraph(spread.Title, p => Plots.ControlChart(p, spread));
    }

    public static void Imr(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var v = Require(ws, StrReq(req, "column")).OrderedNumericValues();
        if (v.Length < 2) throw new ArgumentException("Need at least 2 values.");
        var (ind, mr) = ControlCharts.IMR(v);
        res.StatusTitle = "I-MR Chart";
        res.SessionText = Out.Raw(SpcFormatter.Pair("I-MR Chart", ind, mr));
        res.AddGraph(ind.Title, p => Plots.ControlChart(p, ind));
        res.AddGraph(mr.Title, p => Plots.ControlChart(p, mr));
    }

    public static void Attribute(EngineRequest req, EngineResponse res, string kind)
    {
        var ws = Ws(req);
        string countsName = StrReq(req, "counts");
        SpcChart chart;
        if (kind == "P" || kind == "U")
        {
            string sizesName = StrReq(req, "sizes");
            var rows = Columns.Rows(new[] { Require(ws, countsName), Require(ws, sizesName) }, requireContiguous: true);
            if (rows.Count < 2) throw new ArgumentException("Need at least 2 complete rows.");
            var counts = rows.Select(r => CheckedCount(r[0], countsName)).ToArray();
            var sizes = rows.Select(r => CheckedCount(r[1], sizesName)).ToArray();
            if (sizes.Any(n => n <= 0)) throw new ArgumentException("Subgroup sizes must be positive integers.");
            if (kind == "P" && counts.Where((count, i) => count > sizes[i]).Any())
                throw new ArgumentException("Defective counts cannot exceed subgroup sizes.");
            chart = kind == "P"
                ? ControlCharts.PChart(counts, sizes)
                : ControlCharts.UChart(counts, sizes);
        }
        else if (kind == "NP")
        {
            var counts = IntColumn(ws, countsName);
            if (counts.Length < 2) throw new ArgumentException("Need at least 2 rows.");
            int size = PositiveInt(req, "size", 50);
            if (counts.Any(count => count > size))
                throw new ArgumentException("Defective counts cannot exceed subgroup size.");
            chart = ControlCharts.NPChart(counts, size);
        }
        else // C
        {
            var counts = IntColumn(ws, countsName);
            if (counts.Length < 2) throw new ArgumentException("Need at least 2 rows.");
            chart = ControlCharts.CChart(counts);
        }
        res.StatusTitle = $"{kind} Chart";
        res.SessionText = Out.Raw(SpcFormatter.Chart(chart));
        res.AddGraph(chart.Title, p => Plots.ControlChart(p, chart));
    }

    public static void Capability(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var col = StrReq(req, "column");
        var v = Require(ws, col).OrderedNumericValues();
        if (v.Length < 2) throw new ArgumentException("Need at least 2 values.");
        double? lsl = NumOpt(req, "lsl"), usl = NumOpt(req, "usl"), target = NumOpt(req, "target");
        if (lsl.HasValue && usl.HasValue && lsl.Value >= usl.Value)
            throw new ArgumentException("LSL must be less than USL.");
        var cap = StatStudio.Core.Statistics.Spc.Capability.FromIndividuals(v, lsl, usl, target);
        res.StatusTitle = $"Process Capability of {col}";
        res.SessionText = Out.Raw(SpcFormatter.Capability(cap));
        res.AddGraph($"Process Capability of {col}",
            p => Plots.CapabilityHistogram(p, col, v, lsl, usl, target));
    }

    public static void GageRR(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var (y, part, op) = Columns.Factorial(
            Require(ws, StrReq(req, "response")), Require(ws, StrReq(req, "part")), Require(ws, StrReq(req, "operator")));
        var g = StatStudio.Core.Statistics.GageRR.Analyze(y, part, op);
        res.StatusTitle = "Gage R&R (Crossed)";
        res.SessionText = Out.Raw(DoeFormatters.GageRR(g));
        var comps = g.Components.Where(c => c.Source.Trim() != "Total Variation").ToList();
        res.AddGraph("Gage R&R Components", p => Plots.LabeledBars(p, "Gage R&R — % Study Var", "Source", "% Study Var",
            comps.Select(c => c.Source.Trim()).ToList(), comps.Select(c => c.PctStudyVar).ToList()));
    }
}
