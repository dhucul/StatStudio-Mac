using System.Text.Json;
using StatStudio.Core.Data;
using StatStudio.Engine;
using StatStudio.Engine.Graphs;
using ColumnDto = StatStudio.Engine.ColumnDto;

namespace StatStudio.Smoke;

internal static class EngineAuditTests
{
    private static EngineResponse Send(string op, WorksheetDto? ws = null, object? args = null) =>
        Dispatcher.Handle(new EngineRequest { Id = 123, Op = op, Worksheet = ws, Params = args is null ? null : JsonSerializer.SerializeToElement(args) });

    public static void Run()
    {
        Check.Section("Audit — engine boundaries");
        var ws = new WorksheetDto { Columns = new() { new ColumnDto { Name = "A", Cells = new() { "1" } } } };
        var calc = Send("calc.evaluate", ws, new { targetColumn = "Tiny", expression = "0.00000000001" });
        Check.True(calc.Ok, "calculator request succeeds");
        Check.True(calc.Worksheet!.Columns[1].Cells[0] != "0" && double.Parse(calc.Worksheet.Columns[1].Cells[0]!, System.Globalization.CultureInfo.InvariantCulture) == 1e-11, "calculator stores the original double precision");
        var duplicate = new WorksheetDto { Columns = new() { new ColumnDto { Name = "A" }, new ColumnDto { Name = "a" } } };
        var bad = Send("descriptives", duplicate);
        Check.True(!bad.Ok && bad.Error!.Contains("Duplicate"), "ambiguous worksheet rejected at protocol boundary");
        var gap = new WorksheetDto { Columns = new() { new ColumnDto { Name = "Series", Cells = new() { "1", null, "2", "3", "4" } } } };
        foreach (string op in new[] { "ts.trend", "graph.timeseries", "spc.imr", "qual.capability" })
        {
            var rejected = Send(op, gap, new { column = "Series" });
            Check.True(!rejected.Ok && rejected.Error!.Contains("row 2"), $"{op} rejects missing time position");
        }
        using (var plot = new ScottPlot.Plot())
        {
            Plots.Boxplot(plot, new[] { ("X", Enumerable.Repeat(0d, 9).Append(100d).ToArray()) });
            var points = plot.GetPlottables<ScottPlot.Plottables.Scatter>().SelectMany(s => s.Data.GetScatterPoints()).ToArray();
            Check.True(points.Any(p => p.Y == 100), "boxplot includes extreme observation as a marker");
        }
        string dir = Path.Combine(Path.GetTempPath(), "statstudio-engine-audit-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var numericNames = new WorksheetDto { Columns = new() { new ColumnDto { Name = "1", Cells = new() { "3" } }, new ColumnDto { Name = "2", Cells = new() { "4" } } } };
            foreach (string ext in new[] { ".csv", ".tsv", ".xlsx" })
            {
                string path = Path.Combine(dir, "roundtrip" + ext);
                Check.True(Send("export", numericNames, new { path }).Ok, $"export numeric headers {ext}");
                var back = Send("import", args: new { path, hasHeader = true });
                Check.True(back.Ok && back.Worksheet!.Columns[0].Name == "1" && back.Worksheet.Columns[0].Cells.Count == 1 && back.Worksheet.Columns[0].Cells[0] == "3", $"explicit header preserves numeric names {ext}");
                numericNames.Columns[0].Cells.Clear(); numericNames.Columns[1].Cells.Clear();
                Check.True(Send("export", numericNames, new { path }).Ok, $"export header-only {ext}");
                back = Send("import", args: new { path, hasHeader = true });
                Check.True(back.Ok && back.Worksheet!.Columns[0].Cells.Count == 0, $"header-only file does not gain an observation {ext}");
                numericNames.Columns[0].Cells.Add("3"); numericNames.Columns[1].Cells.Add("4");
            }
            string plain = Path.Combine(dir, "no-header.csv"); File.WriteAllText(plain, "red,small\nblue,large\n");
            var allText = Send("import", args: new { path = plain, hasHeader = false });
            Check.True(allText.Ok && allText.Worksheet!.Columns[0].Cells.Count == 2 && allText.Worksheet.Columns[0].Cells[0] == "red", "headerless text retains first row");
            string project = Path.Combine(dir, "ids.ssproj");
            var identifiers = new Worksheet(); identifiers.AddColumn("ID", ColumnType.Text).Add("00123"); ProjectStore.Save(identifiers, project);
            var loaded = Send("project.load", args: new { path = project });
            Check.True(loaded.Ok && loaded.Worksheet!.Columns[0].Type == "Text", "project type reaches the wire");
            string excel = Path.Combine(dir, "ids.xlsx");
            Check.True(Send("export", loaded.Worksheet, new { path = excel }).Ok, "export project identifiers");
            using var wb = new ClosedXML.Excel.XLWorkbook(excel);
            Check.True(wb.Worksheets.First().Cell(2, 1).DataType == ClosedXML.Excel.XLDataType.Text && wb.Worksheets.First().Cell(2, 1).GetString() == "00123", "Excel preserves identifier type and leading zeros");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
