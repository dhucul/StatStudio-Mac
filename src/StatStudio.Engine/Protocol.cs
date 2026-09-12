using System.Text.Json;
using System.Text.Json.Serialization;

namespace StatStudio.Engine;

/// <summary>One column of the worksheet as raw strings (missing = null or "").</summary>
public sealed class ColumnDto
{
    public string Name { get; set; } = "";
    public List<string?> Cells { get; set; } = new();
    public string? Type { get; set; }
}

/// <summary>The worksheet as a plain string matrix (the SwiftUI grid mirror).</summary>
public sealed class WorksheetDto
{
    public string? Name { get; set; }
    public List<ColumnDto> Columns { get; set; } = new();
}

/// <summary>A request line: { id, op, worksheet?, params? }.</summary>
public sealed class EngineRequest
{
    public int Id { get; set; }
    public string Op { get; set; } = "";
    public WorksheetDto? Worksheet { get; set; }
    public JsonElement? Params { get; set; }
}

/// <summary>Name + description of a built-in sample dataset (for the File ▸ Sample Data menu).</summary>
public sealed class SampleInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>A titled graph image (base64 PNG). An analysis may emit several.</summary>
public sealed class GraphImage
{
    public string Title { get; set; } = "";
    public string Png { get; set; } = "";
}

/// <summary>A response line. Any of sessionText / worksheet / graphs / samples may be set.</summary>
public sealed class EngineResponse
{
    public int Id { get; set; }
    public bool Ok { get; set; }
    public string? StatusTitle { get; set; }
    public string? SessionText { get; set; }
    public WorksheetDto? Worksheet { get; set; }
    public List<GraphImage>? Graphs { get; set; }
    public List<SampleInfo>? Samples { get; set; }
    public string? Error { get; set; }
}

/// <summary>Shared JSON settings: camelCase out, case-insensitive in, skip nulls.</summary>
public static class EngineJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>Session-text formatting mirroring MainWindow.Output / OutputRaw.</summary>
internal static class Out
{
    /// <summary>Titled block: blank line, title, underline, body. (cf. MainWindow.Output)</summary>
    public static string Block(string title, string body)
    {
        var underline = new string('─', Math.Max(title.Length, 8));
        return $"\n{title}\n{underline}\n{body.TrimEnd()}\n";
    }

    /// <summary>Pre-formatted block whose first line is its own title. (cf. OutputRaw)</summary>
    public static string Raw(string body) => $"\n{body.TrimEnd()}\n";
}
