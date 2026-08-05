using StatStudio.Core.Data;
using StatStudio.Core.Statistics;

namespace StatStudio.Smoke;

/// <summary>
/// Degenerate-input regressions. Each case previously crashed, returned a silently
/// meaningless result, or corrupted the worksheet; they are pinned here so the guards
/// that replaced that behaviour cannot quietly regress.
/// </summary>
internal static class RegressionTests
{
    public static void Run()
    {
        Check.Section("Degenerate inputs — hypothesis tests");
        // Welch's df is 0/0 when both samples are constant; a NaN df used to blow up the
        // StudentT backing the confidence interval.
        var flat = HypothesisTests.TwoSampleT(new double[] { 5, 5, 5 }, new double[] { 7, 7, 7 });
        Check.True(double.IsFinite(flat.Df), "2-sample t on two constant samples yields a finite df");
        Check.True(double.IsFinite(flat.CiLow) && double.IsFinite(flat.CiHigh),
            "2-sample t on two constant samples still produces a CI");

        Check.Section("Degenerate inputs — time series");
        // Differencing runs before the order/length check, so a short series used to
        // allocate a negative-length array (OverflowException).
        Check.Throws<ArgumentException>(() => Arima.Fit(Array.Empty<double>(), 1, 1, 1),
            "ARIMA on an empty series reports a length error");
        Check.Throws<ArgumentException>(() => Arima.Fit(new double[] { 1 }, 1, 2, 1),
            "ARIMA d=2 on one observation reports a length error");
        Check.Throws<ArgumentException>(() => Sarima.Fit(new double[] { 1, 2, 3, 4, 5 }, 1, 1, 1, 0, 1, 1, 12),
            "SARIMA seasonal difference longer than the series reports a length error");

        Check.Section("Degenerate inputs — calculator");
        var ws = new Worksheet();
        var col = ws.AddColumn("C1");
        foreach (var v in new[] { "1", "2", "3" }) col.Add(v);
        Check.Throws<FormatException>(() => Calculator.Evaluate("''+1", ws),
            "empty quoted column name is rejected, not indexed");
        Check.Throws<FormatException>(
            () => Calculator.Evaluate(new string('(', 5000) + "1" + new string(')', 5000), ws),
            "deeply nested expression is rejected instead of overflowing the stack");

        Check.Section("Singular / undefined models");
        var y = new double[] { 0, 0, 0, 0, 1, 1, 1, 1, 0, 1 };
        var constant = Enumerable.Repeat(1.0, 10).ToArray();
        Check.Throws<ArgumentException>(() => Logistic.Fit(y, new[] { constant }, new[] { "x" }, "y"),
            "collinear logistic design is reported, not returned as an all-NaN table");
        Check.Throws<ArgumentException>(
            () => AnovaExtensions.TwoWay(new double[] { 1, 2, 3, 4 },
                new[] { "a1", "a1", "a2", "a2" }, new[] { "b1", "b1", "b1", "b1" }),
            "two-way ANOVA rejects a single-level factor");
        Check.Throws<ArgumentException>(() => MixtureAnalysis.Fit(
                new double[] { 1, 2, 3 },
                new[] { new double[] { 1, 0, 0 }, new double[] { 0, 1, 0 }, new double[] { 0, 0, 1 } },
                new[] { "A", "B", "C" }, quadratic: false),
            "saturated mixture model (n == p) is rejected");

        // Bartlett is undefined against a zero-variance group; Levene stays valid, so the
        // op must degrade rather than throw or report a spurious p = 0.000.
        var mixed = VarianceTests.EqualVariances(new (string, double[])[]
        {
            ("g1", new double[] { 1, 2, 3, 4, 5 }),
            ("g2", new double[] { 7, 7, 7, 7, 7 }),
            ("g3", new double[] { 2, 3, 4, 5, 6 }),
        });
        Check.True(double.IsNaN(mixed.Bartlett) && double.IsNaN(mixed.BartlettP),
            "Bartlett reports missing against a zero-variance group");
        Check.True(double.IsFinite(mixed.Levene), "Levene is still computed alongside it");

        Check.Section("Delimiter detection");
        // Sampling only the header row misread files whose heading carries no delimiter.
        var semi = WorksheetIo.ReadCsv(new StringReader("Value\nA;1;x\nB;2;y\nC;3;z\n"));
        Check.Equal(semi.ColumnCount, 3, "semicolon data under a single-token header");
        var single = WorksheetIo.ReadCsv(new StringReader("Value\n1\n2\n3\n"));
        Check.Equal(single.ColumnCount, 1, "a genuine single-column file still reads as one column");

        Check.Section("Fisher's exact test — support cap and normal fallback");
        // Small tables must still take the exact path unchanged.
        var small = FishersExact.Test(3, 1, 1, 3);
        Check.True(!small.Approximate, "a small table is still computed exactly");
        Check.Close(small.PTwoSided, 0.4857142857, "exact two-sided p for [[3,1],[1,3]]", 1e-9);

        // Straddle the cap with statistically near-identical tables: the exact and
        // approximate branches must agree, so the switchover is not a visible cliff.
        var justUnder = FishersExact.Test(99999 + 300, 99999 - 300, 99999 - 300, 99999 + 300);
        var justOver = FishersExact.Test(100000 + 300, 100000 - 300, 100000 - 300, 100000 + 300);
        Check.True(!justUnder.Approximate, "table just under the cap is exact");
        Check.True(justOver.Approximate, "table just over the cap uses the approximation");
        Check.Close(justOver.PTwoSided, justUnder.PTwoSided, "two-sided p is continuous across the cap", 1e-4);
        Check.Close(justOver.PLess, justUnder.PLess, "less-tail p is continuous across the cap", 1e-4);
        Check.Close(justOver.PGreater, justUnder.PGreater, "greater-tail p is continuous across the cap", 1e-4);

        // Pinned against an independent exact enumeration of all 200 001 support points.
        var approx = FishersExact.Test(100300, 99700, 99700, 100300);
        Check.True(approx.Approximate, "200 001-point support falls back to the approximation");
        Check.Close(approx.PTwoSided, 0.05819775968, "approximation matches independent exact two-sided p", 1e-4);
        Check.Close(approx.PGreater, 0.02909887984, "approximation matches independent exact greater-tail p", 1e-4);
        // Tails stay mutually consistent: two-sided == 2 * min(less, greater).
        Check.Close(approx.PTwoSided, 2 * Math.Min(approx.PLess, approx.PGreater),
            "two-sided equals twice the smaller tail", 1e-12);

        // The whole point of the cap: a table that would need ~10^9 terms returns at once.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var huge = FishersExact.Test(500_000_000, 500_000_000, 500_000_000, 500_000_000);
        watch.Stop();
        Check.True(watch.ElapsedMilliseconds < 2000,
            $"n = 2x10^9 table returns promptly ({watch.ElapsedMilliseconds} ms)");
        Check.Close(huge.PTwoSided, 1.0, "perfectly null huge table gives p = 1", 1e-6);

        // A degenerate margin leaves a single possible table — no evidence either way.
        Check.Close(FishersExact.Test(0, 0, 5, 5).PTwoSided, 1.0, "degenerate margin gives p = 1", 1e-12);
        // Margin sums must not silently overflow int.
        Check.Throws<ArgumentException>(
            () => FishersExact.Test(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue),
            "table total beyond int range is rejected");

        Check.Section("Tukey critical-value cache");
        var groups = new (string, double[])[]
        {
            ("G1", new double[] { 1, 2, 3 }),
            ("G2", new double[] { 4, 5, 6 }),
            ("G3", new double[] { 7, 8, 9 }),
        };
        var first = AnovaExtensions.Tukey(groups);
        var second = AnovaExtensions.Tukey(groups);
        Check.Close(second.QCritical, first.QCritical, "memoized critical value matches", 1e-12);
        Check.Close(first.QCritical, 4.3394, "q(0.95, k=3, df=6)", 5e-3);
    }
}
