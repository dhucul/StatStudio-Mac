using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace StatStudio.Core.Statistics;

public sealed record FactorialTerm(string Name, double Effect, double Coef, double SeCoef, double T, double P);

public sealed record FactorialResult(
    string Response, IReadOnlyList<string> Factors, IReadOnlyList<FactorialTerm> Terms,
    double S, double RSquared, int N, int DfError);

/// <summary>
/// Analysis of a 2-level factorial design (orthogonal ±1 coding). Computes effects,
/// coefficients, and — when there is pure-error replication — t/p values.
/// </summary>
public static class FactorialAnalysis
{
    public static FactorialResult Analyze(double[] y, double[][] factors, IReadOnlyList<string> factorNames,
        string response = "Y")
    {
        int n = y.Length;
        int k = factors.Length;
        if (k < 1 || k > 7) throw new ArgumentException("Factorial analysis supports 1..7 factors.");
        if (factorNames.Count != k || factors.Any(f => f.Length != n))
            throw new ArgumentException("Response, factor columns, and factor names must have matching dimensions.");

        // Code each factor to ±1 (center -> 0).
        var coded = new double[k][];
        for (int j = 0; j < k; j++)
        {
            double min = factors[j].Min(), max = factors[j].Max();
            double mid = (min + max) / 2, half = (max - min) / 2;
            coded[j] = factors[j].Select(v => half > 0 ? (v - mid) / half : 0).ToArray();
        }

        double yBar = y.Average();
        double ssTotal = y.Sum(v => (v - yBar) * (v - yBar));

        // Pure error from replicated factor-level combinations.
        var groups = new Dictionary<string, List<double>>();
        for (int i = 0; i < n; i++)
        {
            string key = string.Join(",", coded.Select(c => Math.Round(c[i], 6)));
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<double>();
            list.Add(y[i]);
        }
        double ssPe = groups.Values.Sum(g => { double m = g.Average(); return g.Sum(v => (v - m) * (v - m)); });
        int dfPe = n - groups.Count;
        double msErr = dfPe > 0 ? ssPe / dfPe : double.NaN;
        StudentT? tDist = dfPe > 0 ? new StudentT(0, 1, dfPe) : null;

        var independent = new List<(string Name, double[] Column)>();
        for (int mask = 1; mask < (1 << k); mask++)
        {
            var idx = Enumerable.Range(0, k).Where(b => (mask & (1 << b)) != 0).ToArray();
            var col = new double[n];
            for (int i = 0; i < n; i++)
            {
                double v = 1;
                foreach (var b in idx) v *= coded[b][i];
                col[i] = v;
            }
            if (col.All(v => Math.Abs(v) < 1e-12)) continue;

            string name = string.Concat(idx.Select(b => factorNames[b]));
            if (col.All(v => Math.Abs(v - col[0]) < 1e-10)) continue; // defining word aliases the intercept
            int alias = independent.FindIndex(t => SameColumn(t.Column, col, out _));
            if (alias < 0)
            {
                independent.Add((name, col));
                continue;
            }

            SameColumn(independent[alias].Column, col, out int sign);
            string aliasName = sign > 0 ? name : $"-{name}";
            independent[alias] = ($"{independent[alias].Name} = {aliasName}", independent[alias].Column);
        }

        int parameterCount = 1 + independent.Count;
        if (n < parameterCount)
            throw new ArgumentException("The available runs cannot estimate every non-aliased factorial term.");
        var design = Matrix<double>.Build.Dense(n, parameterCount, 1);
        for (int j = 0; j < independent.Count; j++)
            for (int i = 0; i < n; i++) design[i, j + 1] = independent[j].Column[i];
        var svd = design.Svd();
        if (svd.Rank < parameterCount)
            throw new ArgumentException("The factorial model is rank-deficient after accounting for aliases.");

        var beta = design.QR().Solve(Vector<double>.Build.DenseOfArray(y));
        var fitted = design * beta;
        double sse = 0;
        for (int i = 0; i < n; i++) sse += Math.Pow(y[i] - fitted[i], 2);
        double ssModel = Math.Max(0, ssTotal - sse);
        Matrix<double>? covarianceScale = dfPe > 0
            ? design.TransposeThisAndMultiply(design).Inverse()
            : null;

        var terms = new List<FactorialTerm> {
            MakeTerm("Constant", double.NaN, beta[0], covarianceScale?[0, 0], msErr, tDist)
        };
        for (int termIndex = 0; termIndex < independent.Count; termIndex++)
        {
            int coefficientIndex = termIndex + 1;
            double coef = beta[coefficientIndex];
            terms.Add(MakeTerm(independent[termIndex].Name, 2 * coef, coef,
                covarianceScale?[coefficientIndex, coefficientIndex], msErr, tDist));
        }

        double r2 = ssTotal > 0 ? ssModel / ssTotal : double.NaN;
        return new FactorialResult(response, factorNames, terms, double.IsNaN(msErr) ? double.NaN : Math.Sqrt(msErr), r2, n, dfPe);
    }

    private static bool SameColumn(double[] a, double[] b, out int sign)
    {
        bool same = true, opposite = true;
        for (int i = 0; i < a.Length; i++)
        {
            same &= Math.Abs(a[i] - b[i]) < 1e-10;
            opposite &= Math.Abs(a[i] + b[i]) < 1e-10;
        }
        sign = same ? 1 : opposite ? -1 : 0;
        return sign != 0;
    }

    private static FactorialTerm MakeTerm(string name, double effect, double coef,
        double? covarianceScale, double msErr, StudentT? distribution)
    {
        double se = covarianceScale.HasValue && distribution != null
            ? Math.Sqrt(Math.Max(0, msErr * covarianceScale.Value))
            : double.NaN;
        double t = se > 0 ? coef / se : double.NaN;
        double p = distribution != null && !double.IsNaN(t)
            ? 2 * (1 - distribution.CumulativeDistribution(Math.Abs(t)))
            : double.NaN;
        return new FactorialTerm(name, effect, coef, se, t, p);
    }
}
