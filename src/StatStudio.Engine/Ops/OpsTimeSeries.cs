using StatStudio.Core.Statistics;
using StatStudio.Engine.Graphs;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>Time series: trend, moving average, exponential smoothing, decomposition,
/// ACF/PACF, ARIMA, SARIMA.</summary>
internal static class OpsTimeSeries
{
    private static (double[] v, string name) Series(EngineRequest req)
    {
        var ws = Ws(req);
        var name = StrReq(req, "column");
        return (Require(ws, name).NumericValues(), name);
    }

    public static void Trend(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        if (v.Length < 3) throw new ArgumentException("Need at least 3 points.");
        int fc = Int(req, "forecasts", 0);
        var r = Bool(req, "quadratic") ? TimeSeries.QuadraticTrend(v, fc) : TimeSeries.LinearTrend(v, fc);
        res.StatusTitle = $"Trend Analysis of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Trend(r, name, v.Length));
        res.AddGraph($"Trend Analysis of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    public static void MovingAverage(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        int len = Int(req, "length", 3);
        if (v.Length <= len) throw new ArgumentException("Series shorter than the MA length.");
        var r = TimeSeries.MovingAverage(v, len, Int(req, "forecasts", 0));
        res.StatusTitle = $"Moving Average of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
        res.AddGraph($"Moving Average of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    public static void SingleExp(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        if (v.Length < 2) throw new ArgumentException("Need at least 2 points.");
        var r = TimeSeries.SingleExp(v, Num(req, "alpha", 0.2), Int(req, "forecasts", 0));
        res.StatusTitle = $"Single Exp Smoothing of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
        res.AddGraph($"Single Exp Smoothing of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    public static void DoubleExp(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        if (v.Length < 3) throw new ArgumentException("Need at least 3 points.");
        var r = TimeSeries.DoubleExp(v, Num(req, "alpha", 0.2), Num(req, "beta", 0.1), Int(req, "forecasts", 0));
        res.StatusTitle = $"Double Exp Smoothing of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
        res.AddGraph($"Double Exp Smoothing of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    public static void Winters(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        var r = TimeSeries.Winters(v, Int(req, "period", 12), Num(req, "alpha", 0.2), Num(req, "beta", 0.1),
            Num(req, "gamma", 0.1), Bool(req, "multiplicative"), Int(req, "forecasts", 0));
        res.StatusTitle = $"Winters' Method of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Smoothing(r, name, v.Length));
        res.AddGraph($"Winters' Method of {name}", p => Plots.TimeSeriesFit(p, name, v, r.Fitted, r.Forecasts));
    }

    public static void Decompose(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        int period = Int(req, "period", 12);
        if (v.Length < 2 * period) throw new ArgumentException("Need at least two full seasons.");
        var r = TimeSeries.Decompose(v, period, Bool(req, "multiplicative"));
        res.StatusTitle = $"Decomposition of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Decomposition(r, name));
        res.AddGraph($"Decomposition of {name} (trend)",
            p => Plots.TimeSeriesFit(p, name, v, r.Trend, Array.Empty<double>()));
    }

    public static void Acf(EngineRequest req, EngineResponse res, bool partial)
    {
        var (v, name) = Series(req);
        if (v.Length < 4) throw new ArgumentException("Need at least 4 points.");
        var r = TimeSeries.Autocorrelation(v, Int(req, "maxlag", Math.Min(20, v.Length - 1)));
        var vals = partial ? r.Pacf : r.Acf;
        string lbl = partial ? "PACF" : "ACF";
        res.StatusTitle = $"{lbl} of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Acf(r, name, partial));
        res.AddGraph($"{lbl} of {name}", p => Plots.Acf(p, $"{lbl} of {name}", vals, r.N, lbl));
    }

    public static void Arima(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        var r = StatStudio.Core.Statistics.Arima.Fit(v, Int(req, "p", 1), Int(req, "d", 1), Int(req, "q", 1),
            Int(req, "forecasts", 10), Bool(req, "includeConstant", true));
        res.StatusTitle = $"ARIMA of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Arima(r, name));
        if (r.Forecasts.Length > 0)
            res.AddGraph($"ARIMA Forecast of {name}",
                p => Plots.ForecastPlot(p, name, v, r.Forecasts, r.ForecastLower, r.ForecastUpper));
    }

    public static void Sarima(EngineRequest req, EngineResponse res)
    {
        var (v, name) = Series(req);
        var r = StatStudio.Core.Statistics.Sarima.Fit(v, Int(req, "p", 1), Int(req, "d", 1), Int(req, "q", 1),
            Int(req, "sp", 0), Int(req, "sd", 1), Int(req, "sq", 1), Int(req, "season", 12),
            Int(req, "forecasts", 12), Bool(req, "includeConstant"));
        res.StatusTitle = $"SARIMA of {name}";
        res.SessionText = Out.Raw(TimeSeriesFormatters.Sarima(r, name));
        if (r.Forecasts.Length > 0)
            res.AddGraph($"SARIMA Forecast of {name}",
                p => Plots.ForecastPlot(p, name, v, r.Forecasts, r.ForecastLower, r.ForecastUpper));
    }
}
