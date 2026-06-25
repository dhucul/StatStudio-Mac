using ScottPlot;
using StatStudio.Engine.Graphs;

namespace StatStudio.Engine;

/// <summary>
/// Builds a ScottPlot plot via the shared <see cref="Plots"/> helpers and renders it
/// headlessly (SkiaSharp) to a base64 PNG — no on-screen control needed.
/// </summary>
internal static class Render
{
    public static GraphImage Graph(string title, Action<Plot> build, int width = 820, int height = 560)
    {
        var plot = new Plot();
        Plots.ApplyTheme(plot);
        build(plot);
        byte[] bytes = plot.GetImage(width, height).GetImageBytes();
        return new GraphImage { Title = title, Png = Convert.ToBase64String(bytes) };
    }
}

internal static class ResponseGraphs
{
    /// <summary>Render + attach a graph to the response (analyses may add several).</summary>
    public static void AddGraph(this EngineResponse res, string title, Action<Plot> build)
        => (res.Graphs ??= new()).Add(Render.Graph(title, build));
}
