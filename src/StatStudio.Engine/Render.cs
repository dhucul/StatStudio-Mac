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
        // Plot and Image both own unmanaged SkiaSharp resources. The engine is a
        // long-lived process, so leaving them to the finalizer leaks native memory
        // that GC pressure never accounts for.
        using var plot = new Plot();
        Plots.ApplyTheme(plot);
        build(plot);
        using var image = plot.GetImage(width, height);
        byte[] bytes = image.GetImageBytes();
        return new GraphImage { Title = title, Png = Convert.ToBase64String(bytes) };
    }
}

internal static class ResponseGraphs
{
    /// <summary>Render + attach a graph to the response (analyses may add several).</summary>
    public static void AddGraph(this EngineResponse res, string title, Action<Plot> build)
        => (res.Graphs ??= new()).Add(Render.Graph(title, build));
}
