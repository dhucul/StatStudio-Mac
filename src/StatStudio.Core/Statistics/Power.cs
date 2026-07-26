using MathNet.Numerics.Distributions;

namespace StatStudio.Core.Statistics;

/// <summary>
/// Power and sample-size calculations (normal approximation, which is exact for the
/// z-test and a close approximation for the corresponding t-tests).
/// </summary>
public static class Power
{
    private static double Zc(double alpha, Alternative alt) =>
        alt == Alternative.TwoSided ? Normal.InvCDF(0, 1, 1 - alpha / 2) : Normal.InvCDF(0, 1, 1 - alpha);

    // ---- one-sample t (effect = |mean - mu0| / sigma) ----------------------

    public static double OneSampleTPower(double n, double effect, double alpha, Alternative alt)
    {
        double ncp = Math.Abs(effect) * Math.Sqrt(n);
        double zc = Zc(alpha, alt);
        double power = Normal.CDF(0, 1, ncp - zc);
        if (alt == Alternative.TwoSided) power += Normal.CDF(0, 1, -ncp - zc);
        return power;
    }

    public static double OneSampleTSampleSize(double power, double effect, double alpha, Alternative alt)
    {
        double zc = Zc(alpha, alt), zb = Normal.InvCDF(0, 1, power);
        return Math.Pow((zc + zb) / Math.Abs(effect), 2);
    }

    // ---- two-sample t (effect = |mu1 - mu2| / sigma, equal n per group) -----

    public static double TwoSampleTPower(double nPerGroup, double effect, double alpha, Alternative alt)
    {
        double ncp = Math.Abs(effect) * Math.Sqrt(nPerGroup / 2.0);
        double zc = Zc(alpha, alt);
        double power = Normal.CDF(0, 1, ncp - zc);
        if (alt == Alternative.TwoSided) power += Normal.CDF(0, 1, -ncp - zc);
        return power;
    }

    public static double TwoSampleTSampleSize(double power, double effect, double alpha, Alternative alt)
    {
        double zc = Zc(alpha, alt), zb = Normal.InvCDF(0, 1, power);
        return 2 * Math.Pow((zc + zb) / Math.Abs(effect), 2);
    }

    // ---- one proportion ----------------------------------------------------

    public static double OneProportionPower(double n, double p0, double p1, double alpha, Alternative alt)
    {
        if (n <= 0 || p0 is < 0 or > 1 || p1 is < 0 or > 1 || alpha is <= 0 or >= 1)
            throw new ArgumentException("n must be positive; p0, p1, and alpha must be valid probabilities.");
        double zc = Zc(alpha, alt);
        double se0 = Math.Sqrt(p0 * (1 - p0)), se1 = Math.Sqrt(p1 * (1 - p1));
        if (se1 <= 0) return double.NaN;
        double nullSe = se0 / Math.Sqrt(n);
        double altSe = se1 / Math.Sqrt(n);
        double lower = p0 - zc * nullSe;
        double upper = p0 + zc * nullSe;
        return alt switch
        {
            Alternative.Less => Normal.CDF(p1, altSe, lower),
            Alternative.Greater => 1 - Normal.CDF(p1, altSe, upper),
            _ => Normal.CDF(p1, altSe, lower) + 1 - Normal.CDF(p1, altSe, upper),
        };
    }

    public static double OneProportionSampleSize(double power, double p0, double p1, double alpha, Alternative alt)
    {
        double zc = Zc(alpha, alt), zb = Normal.InvCDF(0, 1, power);
        double num = zc * Math.Sqrt(p0 * (1 - p0)) + zb * Math.Sqrt(p1 * (1 - p1));
        return Math.Pow(num / (p1 - p0), 2);
    }
}
