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
            double value;
            if (v.ValueKind == JsonValueKind.Number) value = v.GetDouble();
            else if (v.ValueKind == JsonValueKind.String &&
                     double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                value = d;
            else
                throw new ArgumentException($"'{key}' must be numeric.");
            if (!double.IsFinite(value)) throw new ArgumentException($"'{key}' must be finite.");
            return value;
        }
        return null;
    }

    public static double Num(EngineRequest req, string key, double dflt) => NumOpt(req, key) ?? dflt;

    public static int Int(EngineRequest req, string key, int dflt)
    {
        var n = NumOpt(req, key);
        if (!n.HasValue) return dflt;
        double rounded = Math.Round(n.Value);
        if (Math.Abs(n.Value - rounded) > 1e-9 || rounded < int.MinValue || rounded > int.MaxValue)
            throw new ArgumentException($"'{key}' must be an integer.");
        return (int)rounded;
    }

    public static int PositiveInt(EngineRequest req, string key, int dflt)
    {
        int value = Int(req, key, dflt);
        return value > 0 ? value : throw new ArgumentException($"'{key}' must be greater than 0.");
    }

    public static int NonNegativeInt(EngineRequest req, string key, int dflt)
    {
        int value = Int(req, key, dflt);
        return value >= 0 ? value : throw new ArgumentException($"'{key}' must be non-negative.");
    }

    public static double PositiveNum(EngineRequest req, string key, double dflt)
    {
        double value = Num(req, key, dflt);
        return value > 0 ? value : throw new ArgumentException($"'{key}' must be greater than 0.");
    }

    public static double Probability(EngineRequest req, string key, double dflt, bool open = false)
    {
        double value = Num(req, key, dflt);
        bool valid = open ? value is > 0 and < 1 : value is >= 0 and <= 1;
        return valid ? value : throw new ArgumentException(
            $"'{key}' must be {(open ? "between 0 and 1" : "from 0 through 1")}.");
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
        double value = n.Value > 1 ? n.Value / 100.0 : n.Value;
        return value is > 0 and < 1
            ? value
            : throw new ArgumentException($"'{key}' must be between 0 and 1 (or 0 and 100 percent).");
    }

    public static Alternative Alt(EngineRequest req, string key = "alt") => Str(req, key) switch
    {
        "less" => Alternative.Less,
        "greater" => Alternative.Greater,
        _ => Alternative.TwoSided,
    };
}
