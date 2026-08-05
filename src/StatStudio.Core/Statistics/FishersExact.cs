using MathNet.Numerics;
using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

public sealed record FisherResult(
    int A, int B, int C, int D, double PTwoSided, double PLess, double PGreater, double OddsRatio,
    bool Approximate);

public static class FishersExact
{
    /// <summary>
    /// Largest hypergeometric support (number of possible values of cell A) still worth
    /// enumerating term by term. Past this the normal approximation is used instead.
    /// </summary>
    /// <remarks>
    /// The support size is bounded by min(r1, r2, c1, c2) + 1, so exceeding this limit
    /// forces every margin above 200 000. With the table total capped at int.MaxValue,
    /// the smallest possible expected cell count is then 200000² / 2147483647 ≈ 18.6 —
    /// comfortably past the conventional "expected count ≥ 5" threshold at which the
    /// normal/chi-square approximation is considered sound. The approximation therefore
    /// only ever replaces the exact test on tables where it is known to be accurate.
    /// </remarks>
    public const int ExactSupportLimit = 200_000;

    /// <summary>Fisher's exact test for a 2×2 table [[a,b],[c,d]] (hypergeometric).</summary>
    public static FisherResult Test(int a, int b, int c, int d)
    {
        if (a < 0 || b < 0 || c < 0 || d < 0 || (long)a + b + c + d == 0)
            throw new ArgumentException("Fisher's exact test needs non-negative cells and a positive total.");
        // Guard the margin sums below against silent int overflow.
        if ((long)a + b + c + d > int.MaxValue)
            throw new ArgumentException("Fisher's exact test table total is too large.");

        int r1 = a + b, r2 = c + d, c1 = a + c, n = a + b + c + d;
        int lo = Math.Max(0, c1 - r2), hi = Math.Min(r1, c1);
        double or = (b == 0 || c == 0) ? double.PositiveInfinity : (double)a * d / ((double)b * c);

        // Enumerating the support costs one term per point; on a huge table that would
        // stall the engine (which serves requests one at a time).
        if ((long)hi - lo + 1 > ExactSupportLimit)
        {
            var (twoSided, less, greater) = NormalApproximation(a, r1, r2, c1, n);
            return new FisherResult(a, b, c, d, twoSided, less, greater, or, Approximate: true);
        }

        double lnDenominator = LnChoose(n, c1);   // loop-invariant
        double pObs = HyperProb(a, r1, r2, c1, lnDenominator);
        double pTwo = 0, pLess = 0, pGreater = 0;
        for (int x = lo; x <= hi; x++)
        {
            double px = HyperProb(x, r1, r2, c1, lnDenominator);
            if (px <= pObs * (1 + 1e-7)) pTwo += px;
            if (x <= a) pLess += px;
            if (x >= a) pGreater += px;
        }
        return new FisherResult(a, b, c, d,
            Math.Min(1, pTwo), Math.Min(1, pLess), Math.Min(1, pGreater), or, Approximate: false);
    }

    /// <summary>
    /// Normal approximation to the hypergeometric with a continuity correction — the
    /// large-sample equivalent of the exact test (and of the Yates-corrected chi-square
    /// test of independence). The two-sided value stays exactly 2·min(less, greater),
    /// matching how the exact branch relates its tails.
    /// </summary>
    private static (double TwoSided, double Less, double Greater) NormalApproximation(
        int a, int r1, int r2, int c1, int n)
    {
        int c2 = n - c1;
        double mean = (double)r1 * c1 / n;
        // Var(X) = r1·r2·c1·c2 / (n²(n−1)). Zero (or 0/0) only when a margin is
        // degenerate, in which case the table is fully determined and carries no evidence.
        double variance = ((double)r1 * r2 * c1 * c2) / ((double)n * n * (n - 1));
        if (!(variance > 0)) return (1, 1, 1);
        double sd = Math.Sqrt(variance);

        double deviation = Math.Max(0, Math.Abs(a - mean) - 0.5);
        double twoSided = Math.Min(1, 2 * (1 - Normal.CDF(0, 1, deviation / sd)));
        double less = Math.Min(1, Normal.CDF(0, 1, (a - mean + 0.5) / sd));
        double greater = Math.Min(1, Normal.CDF(0, 1, (mean - a + 0.5) / sd));
        return (twoSided, less, greater);
    }

    // P(X = x) for the hypergeometric with the table's margins fixed. The denominator
    // ln C(n, c1) is loop-invariant, so callers hoist it out.
    private static double HyperProb(int x, int r1, int r2, int c1, double lnDenominator) =>
        Math.Exp(LnChoose(r1, x) + LnChoose(r2, c1 - x) - lnDenominator);

    private static double LnChoose(int n, int k)
    {
        if (k < 0 || k > n) return double.NegativeInfinity;
        return SpecialFunctions.FactorialLn(n) - SpecialFunctions.FactorialLn(k) - SpecialFunctions.FactorialLn(n - k);
    }
}
