using StatStudio.Core.Statistics;
using StatStudio.Engine.Graphs;
using static StatStudio.Engine.Args;

namespace StatStudio.Engine;

/// <summary>Regression: simple, multiple, polynomial, best subsets, stepwise, logistic.</summary>
internal static class OpsRegression
{
    public static void Simple(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var xn = StrReq(req, "x"); var yn = StrReq(req, "y");
        var (xs, ys) = Columns.Pairwise(Require(ws, xn), Require(ws, yn));
        if (xs.Length < 3) throw new ArgumentException("Need at least 3 paired observations.");
        var r = Regression.SimpleLinear(xs, ys, xn, yn);
        res.StatusTitle = "Simple Regression";
        res.SessionText = Out.Raw(RegressionFormatter.Format(r));
        res.AddGraph($"Fitted Line Plot of {yn} vs {xn}",
            p => Plots.FittedLine(p, xn, yn, xs, ys, r.Coefficients[0], r.Coefficients[1]));
        res.AddGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
    }

    public static void Multiple(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var response = StrReq(req, "response");
        var names = Strings(req, "predictors");
        var (y, x) = Columns.Design(Require(ws, response), names.Select(n => Require(ws, n)).ToList());
        if (y.Length <= names.Length + 1) throw new ArgumentException("Not enough complete rows for the number of predictors.");
        var r = Regression.Fit(y, x, names.ToList(), response);
        res.StatusTitle = "Multiple Regression";
        res.SessionText = Out.Raw(RegressionFormatter.Format(r));
        res.AddGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
    }

    public static void Polynomial(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var xn = StrReq(req, "x"); var yn = StrReq(req, "y");
        int degree = Int(req, "degree", 2);
        var (xs, ys) = Columns.Pairwise(Require(ws, xn), Require(ws, yn));
        if (xs.Length <= degree + 1) throw new ArgumentException("Not enough points for that degree.");
        var r = RegressionExtensions.Polynomial(xs, ys, degree, xn, yn);
        res.StatusTitle = "Polynomial Regression";
        res.SessionText = Out.Raw(RegressionFormatter.Format(r));
        res.AddGraph("Residuals vs Fitted", p => Plots.ResidualVsFitted(p, r.Fitted, r.Residuals));
    }

    public static void BestSubsets(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var (y, x, names) = Design(req, ws);
        res.StatusTitle = "Best Subsets Regression";
        res.SessionText = Out.Raw(AdvancedFormatters.BestSubsets(RegressionExtensions.BestSubsets(y, x, names.ToList())));
    }

    public static void Stepwise(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var (y, x, names) = Design(req, ws);
        res.StatusTitle = "Stepwise Regression";
        res.SessionText = Out.Raw(AdvancedFormatters.Stepwise(RegressionExtensions.Stepwise(y, x, names.ToList())));
    }

    public static void Logistic(EngineRequest req, EngineResponse res)
    {
        var ws = Ws(req);
        var response = StrReq(req, "response");
        var (y, x, names) = Design(req, ws);
        var fit = StatStudio.Core.Statistics.Logistic.Fit(y, x, names.ToList(), response);
        res.StatusTitle = "Binary Logistic Regression";
        res.SessionText = Out.Raw(AdvancedFormatters.Logistic(fit));
    }

    private static (double[] y, double[][] x, string[] names) Design(EngineRequest req,
        StatStudio.Core.Data.Worksheet ws)
    {
        var response = StrReq(req, "response");
        var names = Strings(req, "predictors");
        var (y, x) = Columns.Design(Require(ws, response), names.Select(n => Require(ws, n)).ToList());
        if (y.Length <= names.Length + 1) throw new ArgumentException("Not enough complete rows.");
        return (y, x, names);
    }
}
