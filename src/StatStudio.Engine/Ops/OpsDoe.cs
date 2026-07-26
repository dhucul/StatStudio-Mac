using System.Globalization;
using StatStudio.Core.Data;
using StatStudio.Core.Statistics;
using StatStudio.Engine.Graphs;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>Design of Experiments: create + analyze factorial, fractional, response-surface,
/// and mixture designs.</summary>
internal static class OpsDoe
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void CreateFactorial(EngineRequest req, EngineResponse res)
    {
        int factors = Int(req, "factors", 2);
        var design = DoeDesign.FullFactorial(factors, Int(req, "replicates", 1),
            Int(req, "centerPoints", 0), Bool(req, "randomize"));
        var ws = new Worksheet { Name = $"FactorialDesign_{factors}f" };
        var so = ws.AddColumn("StdOrder"); var ro = ws.AddColumn("RunOrder"); var cp = ws.AddColumn("CenterPt");
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString()); ro.Add(run.RunOrder.ToString()); cp.Add(run.CenterPt.ToString());
            for (int j = 0; j < fcols.Count; j++) fcols[j].Add(run.Factors[j].ToString(Inv));
        }
        res.Worksheet = WorksheetBridge.ToDto(ws);
        res.StatusTitle = "Create Factorial Design";
        res.SessionText = Out.Raw(DoeFormatters.Design(design));
    }

    public static void CreateFractional(EngineRequest req, EngineResponse res)
    {
        int factors = Int(req, "factors", 3);
        int runs = Int(req, "runs", 8);
        var design = DoeDesign.FractionalFactorial(factors, runs, Bool(req, "randomize"));
        var ws = new Worksheet { Name = $"FracFactorial_{factors}f{runs}r" };
        var so = ws.AddColumn("StdOrder"); var ro = ws.AddColumn("RunOrder");
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString()); ro.Add(run.RunOrder.ToString());
            for (int j = 0; j < fcols.Count; j++) fcols[j].Add(run.Factors[j].ToString(Inv));
        }
        res.Worksheet = WorksheetBridge.ToDto(ws);
        res.StatusTitle = "Create Fractional Factorial";
        res.SessionText = Out.Raw(DoeFormatters.Fractional(design));
    }

    public static void AnalyzeFactorial(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var response = StrReq(req, "response");
        var names = Strings(req, "predictors");
        var (y, x) = Columns.Design(Require(ws, response), names.Select(n => Require(ws, n)).ToList());
        if (y.Length < 4) throw new ArgumentException("Need at least 4 complete runs.");
        var r = FactorialAnalysis.Analyze(y, x, names.ToList(), response);
        res.StatusTitle = "Analyze Factorial Design";
        res.SessionText = Out.Raw(DoeFormatters.Factorial(r));
        var effects = r.Terms.Where(t => t.Name != "Constant")
            .OrderByDescending(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList();
        string yl = r.DfError > 0 ? "|Standardized effect|" : "|Effect|";
        res.AddGraph("Pareto of Effects", p => Plots.LabeledBars(p, "Pareto of Effects", "Term", yl,
            effects.Select(t => t.Name).ToList(),
            effects.Select(t => Math.Abs(double.IsNaN(t.T) ? t.Effect : t.T)).ToList()));
    }

    public static void CreateRsm(EngineRequest req, EngineResponse res)
    {
        int factors = Int(req, "factors", 2);
        bool boxBehnken = Bool(req, "boxBehnken");
        var design = boxBehnken
            ? ResponseSurface.BoxBehnken(factors, Int(req, "centerPoints", 3), Bool(req, "randomize"))
            : ResponseSurface.CentralComposite(factors, Int(req, "centerPoints", 3),
                                               Bool(req, "faceCentered"), Bool(req, "randomize"));
        var ws = new Worksheet { Name = boxBehnken ? $"BoxBehnken_{factors}f" : $"CCD_{factors}f" };
        var so = ws.AddColumn("StdOrder"); var ro = ws.AddColumn("RunOrder");
        var pt = ws.AddColumn("PtType", ColumnType.Text);
        var fcols = design.FactorNames.Select(fn => ws.AddColumn(fn)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString()); ro.Add(run.RunOrder.ToString()); pt.Add(run.PointType);
            for (int j = 0; j < fcols.Count; j++) fcols[j].Add(run.Factors[j].ToString("0.#####", Inv));
        }
        res.Worksheet = WorksheetBridge.ToDto(ws);
        res.StatusTitle = "Create Response Surface";
        res.SessionText = Out.Raw(DoeFormatters.Rsm(design));
    }

    public static void AnalyzeRsm(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var response = StrReq(req, "response");
        var names = Strings(req, "predictors");
        var (y, x) = Columns.Design(Require(ws, response), names.Select(n => Require(ws, n)).ToList());
        if (x.Length < 2) throw new ArgumentException("Select at least two factors.");
        int terms = 1 + 2 * x.Length + x.Length * (x.Length - 1) / 2;
        if (y.Length <= terms) throw new ArgumentException($"Need more than {terms} complete runs for a quadratic model in {x.Length} factors.");
        var r = ResponseSurface.Analyze(y, x, names.ToList(), response);
        res.StatusTitle = "Analyze Response Surface";
        res.SessionText = Out.Raw("Response Surface Regression (full quadratic model)\n\n" + RegressionFormatter.Format(r));
        res.AddGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
    }

    public static void CreateMixture(EngineRequest req, EngineResponse res)
    {
        int components = Int(req, "components", 3);
        bool lattice = Bool(req, "lattice", true);
        var design = lattice
            ? MixtureDesign.SimplexLattice(components, Int(req, "degree", 2), Bool(req, "randomize"))
            : MixtureDesign.SimplexCentroid(components, Bool(req, "randomize"));
        var ws = new Worksheet { Name = $"Mixture_{components}c" };
        var so = ws.AddColumn("StdOrder"); var ro = ws.AddColumn("RunOrder");
        var pt = ws.AddColumn("PtType", ColumnType.Text);
        var ccols = design.ComponentNames.Select(n => ws.AddColumn(n)).ToList();
        foreach (var run in design.RunList)
        {
            so.Add(run.StdOrder.ToString()); ro.Add(run.RunOrder.ToString()); pt.Add(run.PointType);
            for (int j = 0; j < ccols.Count; j++) ccols[j].Add(run.Components[j].ToString("0.#####", Inv));
        }
        res.Worksheet = WorksheetBridge.ToDto(ws);
        res.StatusTitle = "Create Mixture Design";
        res.SessionText = Out.Raw(DoeFormatters.Mixture(design));
    }

    public static void AnalyzeMixture(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var response = StrReq(req, "response");
        var names = Strings(req, "predictors");
        var (y, comps) = Columns.Design(Require(ws, response), names.Select(n => Require(ws, n)).ToList());
        if (comps.Length < 2) throw new ArgumentException("Select at least two components.");
        res.StatusTitle = "Analyze Mixture Design";
        res.SessionText = Out.Raw(DoeFormatters.MixtureModel(
            MixtureAnalysis.Fit(y, comps, names.ToList(), quadratic: true, response: response)));
    }
}
