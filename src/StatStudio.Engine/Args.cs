using System.Globalization;
using System.Text.Json;
using StatStudio.Core.Data;
using StatStudio.Core.Statistics;

namespace StatStudio.Engine;

/// <summary>Typed accessors over a request's JSON <c>params</c>, shared by all op handlers.</summary>
internal static class Args
{
    public static Worksheet Ws(EngineRequest req) => WorksheetBridge.FromDto(req.Worksheet);

    public static DataColumn Require(Worksheet ws, string name) =>
        ws.Find(name) ?? throw new ArgumentException($"column '{name}' not found");

    public static string? Str(EngineRequest req, string key)
    {
        if (req.Params is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    public static string StrReq(EngineRequest req, string key) =>
        Str(req, key) ?? throw new ArgumentException($"'{key}' is required");

    public static string[] Strings(EngineRequest req, string key)
    {
        if (req.Params is { } e && e.ValueKind == JsonValueKind.Object &&
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array)
            return v.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString()!).ToArray();
        return Array.Empty<string>();
    }

    public static double? NumOpt(EngineRequest req, string key)
    {
        if (req.Params is { } e && e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String &&
                double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
        }
        return null;
    }

    public static double Num(EngineRequest req, string key, double dflt) => NumOpt(req, key) ?? dflt;

    public static int Int(EngineRequest req, string key, int dflt)
    {
        var n = NumOpt(req, key);
        return n.HasValue ? (int)Math.Round(n.Value) : dflt;
    }

    public static bool Bool(EngineRequest req, string key, bool dflt = false)
    {
        if (req.Params is { } e && e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        return dflt;
    }

    /// <summary>Confidence accepting "95" or "0.95" (cf. WPF TestOptions.ParseConf).</summary>
    public static double Conf(EngineRequest req, string key = "confidence", double dflt = 0.95)
    {
        var n = NumOpt(req, key);
        if (!n.HasValue) return dflt;
        return n.Value > 1 ? n.Value / 100.0 : n.Value;
    }

    public static Alternative Alt(EngineRequest req, string key = "alt") => Str(req, key) switch
    {
        "less" => Alternative.Less,
        "greater" => Alternative.Greater,
        _ => Alternative.TwoSided,
    };
}
