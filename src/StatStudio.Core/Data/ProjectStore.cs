using System.Text.Json;
using System.Text.Json.Serialization;

namespace StatStudio.Core.Data;

/// <summary>Serializable snapshot of a worksheet (the StatStudio .ssproj format).</summary>
public sealed class ProjectDto
{
    public string Name { get; set; } = "Worksheet 1";
    public List<ColumnDto> Columns { get; set; } = new();
}

public sealed class ColumnDto
{
    public string Name { get; set; } = "";
    public ColumnType Type { get; set; } = ColumnType.Numeric;
    public List<string?> Cells { get; set; } = new();
}

/// <summary>Reads/writes a worksheet as a JSON .ssproj project file.</summary>
public static class ProjectStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(Worksheet ws, string path)
    {
        var dto = new ProjectDto { Name = ws.Name };
        foreach (var c in ws.Columns)
            dto.Columns.Add(new ColumnDto { Name = c.Name, Type = c.Type, Cells = c.Cells.ToList() });
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(dto, Options));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static Worksheet Load(string path)
    {
        var dto = JsonSerializer.Deserialize<ProjectDto>(File.ReadAllText(path), Options)
                  ?? new ProjectDto();
        var ws = new Worksheet { Name = dto.Name };
        foreach (var cd in dto.Columns)
        {
            var col = ws.AddColumn(cd.Name, cd.Type);
            foreach (var v in cd.Cells) col.Add(v);
        }
        return ws;
    }
}
