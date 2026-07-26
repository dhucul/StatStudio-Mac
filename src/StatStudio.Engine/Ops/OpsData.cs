using System.Globalization;
using StatStudio.Core.Data;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>File I/O, sample datasets, and the worksheet calculator.</summary>
internal static class OpsData
{
    public static void Import(EngineRequest req, EngineResponse res)
    {
        var path = StrReq(req, "path");
        var ws = path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? WorksheetIo.ReadXlsx(path)
            : WorksheetIo.ReadCsv(path);
        res.Worksheet = WorksheetBridge.ToDto(ws);
        res.StatusTitle = "Imported";
        res.SessionText = Out.Raw($"Opened '{Path.GetFileName(path)}' — {ws.ColumnCount} columns, {ws.RowCount} rows.");
    }

    public static void Export(EngineRequest req, EngineResponse res)
    {
        var path = StrReq(req, "path");
        var ws = Ws(req);
        if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) WorksheetIo.WriteXlsx(ws, path);
        else WorksheetIo.WriteCsv(ws, path,
            path.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',');
        res.StatusTitle = "Saved";
        res.SessionText = Out.Raw($"Saved worksheet to '{Path.GetFileName(path)}'.");
    }

    public static void ProjectLoad(EngineRequest req, EngineResponse res)
    {
        var path = StrReq(req, "path");
        res.Worksheet = WorksheetBridge.ToDto(ProjectStore.Load(path));
        res.StatusTitle = "Project";
        res.SessionText = Out.Raw($"Opened project '{Path.GetFileName(path)}'.");
    }

    public static void ProjectSave(EngineRequest req, EngineResponse res)
    {
        var path = StrReq(req, "path");
        ProjectStore.Save(Ws(req), path);
        res.StatusTitle = "Project saved";
        res.SessionText = Out.Raw($"Saved project to '{Path.GetFileName(path)}'.");
    }

    public static void SamplesList(EngineRequest req, EngineResponse res) =>
        res.Samples = SampleData.All
            .Select(d => new SampleInfo { Name = d.Name, Description = d.Description })
            .ToList();

    public static void SamplesLoad(EngineRequest req, EngineResponse res)
    {
        var name = StrReq(req, "name");
        var ds = SampleData.All.FirstOrDefault(d => d.Name == name)
                 ?? throw new ArgumentException($"sample '{name}' not found");
        res.Worksheet = WorksheetBridge.ToDto(ds.Build());
        res.StatusTitle = ds.Name;
        res.SessionText = Out.Raw($"Loaded sample dataset: {ds.Name}.");
    }

    public static void Calc(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var expr = StrReq(req, "expression");
        var target = StrReq(req, "targetColumn");
        var result = Calculator.Evaluate(expr, ws);
        var col = ws.Find(target) ?? ws.AddColumn(target);
        col.Clear();
        foreach (var v in result)
            col.Add(double.IsNaN(v) ? null : v.ToString("0.##########", CultureInfo.InvariantCulture));
        res.Worksheet = WorksheetBridge.ToDto(ws);
        res.StatusTitle = "Calculator";
        res.SessionText = Out.Raw($"Calculated '{target}' = {expr}  ({result.Length} rows).");
    }
}
