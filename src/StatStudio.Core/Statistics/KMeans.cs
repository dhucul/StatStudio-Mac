namespace StatStudio.Core.Statistics;

public sealed record KMeansResult(
    int K, IReadOnlyList<string> Variables, int[] Assignments, double[][] Centroids,
    int[] Sizes, double[] WithinSS, double TotalWithinSS, int Iterations, bool Converged);

public static class KMeans
{
    /// <summary>k-means clustering (Lloyd's algorithm, k-means++ seeding) on n×p data.</summary>
    public static KMeansResult Cluster(double[][] data, int k, IReadOnlyList<string> names,
        int maxIter = 100, int seed = 12345)
    {
        int n = data.Length, p = names.Count;
        if (k < 2 || k > n) throw new ArgumentException("k must be between 2 and the number of observations.");
        if (maxIter <= 0) throw new ArgumentException("Maximum iterations must be positive.");
        if (p == 0 || data.Any(row => row.Length != p || row.Any(v => !double.IsFinite(v))))
            throw new ArgumentException("All observations must contain one finite value for every variable.");
        int distinct = data.Select(PointKey).Distinct(StringComparer.Ordinal).Count();
        if (distinct < k)
            throw new ArgumentException($"k={k} exceeds the {distinct} distinct observation(s).");

        var rnd = new Random(seed);
        var centroids = SeedPlusPlus(data, k, p, rnd);
        var assign = Enumerable.Repeat(-1, n).ToArray();
        int iter = 0;
        bool converged = false;

        for (; iter < maxIter; iter++)
        {
            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                int best = 0; double bestD = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double d = Dist2(data[i], centroids[c]);
                    if (d < bestD) { bestD = d; best = c; }
                }
                if (assign[i] != best) { assign[i] = best; changed = true; }
            }

            var sum = new double[k][];
            var cnt = new int[k];
            for (int c = 0; c < k; c++) sum[c] = new double[p];
            for (int i = 0; i < n; i++)
            {
                cnt[assign[i]]++;
                for (int j = 0; j < p; j++) sum[assign[i]][j] += data[i][j];
            }
            for (int c = 0; c < k; c++)
            {
                if (cnt[c] != 0) continue;
                int chosen = Enumerable.Range(0, n)
                    .Where(i => cnt[assign[i]] > 1)
                    .MaxBy(i => Dist2(data[i], centroids[assign[i]]));
                int donor = assign[chosen];
                cnt[donor]--;
                for (int j = 0; j < p; j++) sum[donor][j] -= data[chosen][j];
                assign[chosen] = c;
                cnt[c] = 1;
                sum[c] = (double[])data[chosen].Clone();
                changed = true;
            }
            for (int c = 0; c < k; c++)
                for (int j = 0; j < p; j++) centroids[c][j] = sum[c][j] / cnt[c];

            if (!changed) { converged = true; iter++; break; }
        }

        var within = new double[k];
        var sizes = new int[k];
        for (int i = 0; i < n; i++) { within[assign[i]] += Dist2(data[i], centroids[assign[i]]); sizes[assign[i]]++; }
        return new KMeansResult(k, names, assign, centroids, sizes, within, within.Sum(), iter, converged);
    }

    private static double[][] SeedPlusPlus(double[][] data, int k, int p, Random rnd)
    {
        int n = data.Length;
        var centroids = new double[k][];
        centroids[0] = (double[])data[rnd.Next(n)].Clone();
        var d2 = new double[n];
        for (int c = 1; c < k; c++)
        {
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                double best = double.MaxValue;
                for (int j = 0; j < c; j++) best = Math.Min(best, Dist2(data[i], centroids[j]));
                d2[i] = best; total += best;
            }
            double target = rnd.NextDouble() * total, acc = 0;
            int chosen = n - 1;
            for (int i = 0; i < n; i++) { acc += d2[i]; if (acc >= target) { chosen = i; break; } }
            centroids[c] = (double[])data[chosen].Clone();
        }
        return centroids;
    }

    private static double Dist2(double[] a, double[] b)
    {
        double s = 0;
        for (int j = 0; j < a.Length; j++) { double d = a[j] - b[j]; s += d * d; }
        return s;
    }

    private static string PointKey(double[] point) =>
        string.Join(",", point.Select(v => (v == 0 ? 0 : BitConverter.DoubleToInt64Bits(v)).ToString("X16")));
}
