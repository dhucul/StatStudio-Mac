using StatStudio.Core.Data;
using StatStudio.Core.Statistics;
using StatStudio.Core.Statistics.Spc;

namespace StatStudio.Smoke;

internal static class AuditTests
{
    public static void Run()
    {
        Check.Section("Audit — probability and count invariants");
        var binary = Enumerable.Repeat(0d, 1000).Concat(Enumerable.Repeat(1d, 1000)).ToArray();
        var ad = Normality.AndersonDarling(binary);
        Check.True(ad.P >= 0 && ad.P < 1e-20, "extreme non-normal sample has a valid small p-value");
        Check.True(NonparametricFormatters.AndersonDarling(ad, "Binary").Contains("reject normality."), "normality verdict rejects binary data");
        var sample = Enumerable.Range(1, 50_000).Select(i => (double)i).ToArray();
        var mw = Nonparametric.MannWhitney(sample, sample);
        Check.Close(mw.U, 1_250_000_000, "identical large groups have central Mann-Whitney U");
        Check.Close(mw.P, 1, "identical large groups give p=1");
        var signed = Nonparametric.WilcoxonSignedRank(Enumerable.Range(1, 25_000).SelectMany(i => new[] { (double)i, -(double)i }).ToArray());
        Check.Close(signed.P, 1, "balanced large signed ranks give p=1");
        var kw = Nonparametric.KruskalWallis(new[] { ("A", sample), ("B", sample) });
        Check.Close(kw.H, 0, "identical large groups have H=0");
        Check.Close(kw.P, 1, "identical large groups have Kruskal-Wallis p=1");
        var prop = HypothesisTests.TwoProportions(500_000_000, 1_500_000_000, 500_000_000, 1_500_000_000);
        Check.Close(prop.P, 1, "large equal proportions avoid overflow");
        var pc = ControlCharts.PChart(new[] { 1, 1 }, new[] { 1_500_000_000, 1_500_000_000 });
        Check.Close(pc.Center * 1_500_000_000, 1, "P chart aggregates beyond Int32");
        var uc = ControlCharts.UChart(new[] { 1_500_000_000, 1_500_000_000 }, new[] { 1_500_000_000, 1_500_000_000 });
        Check.Close(uc.Center, 1, "U chart aggregates beyond Int32");

        Check.Section("Audit — model stopping and numerical scale");
        var forward = RegressionExtensions.Stepwise(new double[] { 1, 0, 0, 1 }, new[] { new double[] { -3, -1, 1, 3 } }, new[] { "X" });
        Check.Equal(forward.Final.Predictors.Count, 0, "no entry candidate leaves an intercept-only model");
        Check.Close(forward.Final.Coefficients[0], 0.5, "intercept-only estimate is the mean");
        Check.Throws<ArgumentException>(() => Sarima.Fit(new double[] { 1, 2, 3, 4, 5, 6 }, 0, 0, 0, 0, 0, 1, 12, 1, false), "SARIMA rejects unavailable seasonal MA history before fitting");
        var unit = new double[] { 0.999, 1, 1.001 };
        var w1 = Reliability.FitWeibull(unit);
        var w2 = Reliability.FitWeibull(unit.Select(x => x * 1e6).ToArray());
        Check.True(w1.Parameters[0].Value > 100, "Weibull expands the shape bracket");
        Check.Close(w1.Parameters[0].Value, w2.Parameters[0].Value, "Weibull shape is unit-invariant", 1e-8);
        Check.Close(w1.Parameters[1].Value, w2.Parameters[1].Value / 1e6, "Weibull scale follows measurement units", 1e-8);
        Check.Throws<ArgumentException>(() => Reliability.FitWeibull(new double[] { 10, 10, 10 }), "constant lifetimes do not return a fabricated shape");
        var two = Bayes.NormalMeanUnknownVar(new double[] { 0, 2 });
        Check.True(double.IsNaN(two.PosteriorSd), "df=1 posterior has no finite SD");
        var five = Bayes.NormalMeanUnknownVar(new double[] { 1, 2, 3, 4, 5 });
        Check.Close(five.PosteriorSd, 1, "posterior SD includes the t variance factor");
        var y = new double[] { 2, 4, 5, 4, 5 };
        var x = new[] { new double[] { 1, 2, 3, 4, 5 } };
        var ols = Regression.Fit(y, x, new[] { "X" });
        var br = Bayes.LinearRegression(y, x, new[] { "X" });
        Check.Close(br.Terms[1].PosteriorSd, ols.Terms[1].SeCoef * Math.Sqrt(3), "regression posterior SD uses df=3");
        var clusterData = new[] { new double[] { 0 }, new double[] { 1 }, new double[] { 5 }, new double[] { 6 } };
        var capped = KMeans.Cluster(clusterData, 2, new[] { "X" }, maxIter: 1);
        Check.True(!capped.Converged && MultivariateFormatters.KMeans(capped).Contains("not converged"), "iteration cap is distinct from convergence");
        Check.True(KMeans.Cluster(clusterData, 2, new[] { "X" }).Converged, "stable clustering reports convergence");

        Check.Section("Audit — worksheet and expression invariants");
        var ws = new Worksheet(); ws.AddColumn("A").Add("1");
        Check.Throws<ArgumentException>(() => ws.AddColumn("a"), "case-equivalent duplicate names are rejected");
        foreach (var invalid in new[] { "NaN", "Infinity", "-Infinity", "1e999" })
            Check.Throws<FormatException>(() => ws[0].Add(invalid), $"reject non-finite cell {invalid}");
        foreach (var expression in new[] { new string('-', 2000) + "1", string.Join("^", Enumerable.Repeat("1", 2000)), string.Join("+", Enumerable.Repeat("1", 2000)) })
            Check.Throws<FormatException>(() => Calculator.Evaluate(expression, ws), "complex expression is rejected without process failure");
        var gap = new DataColumn("Series"); foreach (var value in new[] { "1", "*", "3" }) gap.Add(value);
        Check.Throws<ArgumentException>(() => gap.OrderedNumericValues(), "ordered extraction rejects an interior gap");
        Check.Throws<ArgumentException>(() => Columns.Rows(new[] { gap }, requireContiguous: true), "control-chart extraction rejects a gap");
        var contiguous = new DataColumn("Series"); foreach (var value in new[] { "1", "2", "" }) contiguous.Add(value);
        Check.Equal(contiguous.OrderedNumericValues().Length, 2, "unused trailing cells are ignored");
        foreach (int length in new[] { 3, 4 })
        {
            var before = TimeSeries.MovingAverage(new double[] { 1, 2, 3, 4, 5, 6 }, length);
            var after = TimeSeries.MovingAverage(new double[] { 1, 2, 3, 4, 500, 600 }, length);
            Check.Close(before.Fitted[3], after.Fitted[3], $"length {length} does not use future observations");
        }
        var chart = ControlCharts.CChart(Enumerable.Repeat(1, 12).Concat(Enumerable.Repeat(10, 12)).ToArray());
        Check.True(chart.Signals.Any(s => s.Contains('2')) && chart.Signals.All(s => s.Split(',').Distinct().Count() == s.Split(',').Length), "long runs list each test once");
        var fa = FactorAnalysis.Extract(new[] { new double[] { 1, 2 }, new double[] { 2, 4 }, new double[] { 3, 6 } }, new[] { "A", "B" }, 1);
        Check.True(MultivariateFormatters.FactorAnalysis(fa).Contains("100.0000"), "factor variance row displays percent");

        Check.Section("Audit — atomic replacement failure");
        string dir = Path.Combine(Path.GetTempPath(), "statstudio-atomic-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "existing.csv"); File.WriteAllText(path, "original");
            Check.Throws<IOException>(() => AtomicFile.Write(path, temp => { File.WriteAllText(temp, "partial"); throw new IOException("simulated write failure"); }), "failed replacement surfaces its error");
            Check.Equal(File.ReadAllText(path), "original", "failed replacement preserves previous file");
            Check.Equal(Directory.GetFiles(dir).Length, 1, "failed replacement cleans up temporary file");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
