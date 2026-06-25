using StatStudio.Core.Data;

namespace StatStudio.Engine;

/// <summary>
/// Converts between the wire <see cref="WorksheetDto"/> (a raw string matrix from the
/// SwiftUI grid) and a Core <see cref="Worksheet"/>. <see cref="FromDto"/> applies the
/// worksheet typing rules: trailing blank rows are dropped, interior blanks are preserved
/// as missing values, and each column is sniffed numeric-vs-text. This is the load-bearing
/// typing contract the analyses depend on.
/// </summary>
internal static class WorksheetBridge
{
    public static Worksheet FromDto(WorksheetDto? dto)
    {
        var ws = new Worksheet { Name = string.IsNullOrEmpty(dto?.Name) ? "Worksheet 1" : dto!.Name };
        if (dto is null || dto.Columns.Count == 0) return ws;

        int rowCount = dto.Columns.Max(c => c.Cells.Count);

        // Index of the last row holding any value, so trailing blank rows (the empty
        // editing padding) are dropped; interior blanks stay as genuine missing values.
        int last = -1;
        for (int r = 0; r < rowCount; r++)
            foreach (var c in dto.Columns)
            {
                if (r < c.Cells.Count && !string.IsNullOrEmpty(c.Cells[r])) { last = r; break; }
            }

        foreach (var c in dto.Columns)
        {
            var col = ws.AddColumn(string.IsNullOrEmpty(c.Name) ? null : c.Name);
            for (int r = 0; r <= last; r++)
            {
                var v = r < c.Cells.Count ? c.Cells[r] : null;
                col.Add(string.IsNullOrEmpty(v) ? null : v);
            }
            col.Type = col.LooksNumeric() ? ColumnType.Numeric : ColumnType.Text;
        }
        return ws;
    }

    public static WorksheetDto ToDto(Worksheet ws) => new()
    {
        Name = ws.Name,
        Columns = ws.Columns.Select(c => new ColumnDto
        {
            Name = c.Name,
            Cells = c.Cells.ToList(),
        }).ToList(),
    };
}
