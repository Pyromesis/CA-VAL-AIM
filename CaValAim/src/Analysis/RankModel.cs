using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>
/// The VALORANT population behind the coach's rank estimate (spec B "Calibration notes").
/// <para>The benchmark tiers b0…b4 are anchored to population percentiles (5th / 41st / 81st / 98.6th / 99.9th of
/// ranked players), so a tier is really a position inside the ranked population and not an abstract 0–4 score. This
/// class is the one place that converts between the two, so the app can say "you are at the 78th percentile" instead
/// of only naming a rank.</para>
/// <para>The five anchors are nearly evenly spaced in the normal quantile z (−1.64 / −0.23 / 0.88 / 2.19 / 3.09), which
/// means the tier scale is a straight line through them in z space: interpolating z between the anchors and mapping z
/// back to a percentile reproduces the anchors exactly and behaves sensibly in between. A plain linear interpolation
/// of the percentiles would be badly wrong — the population is dense around Gold and almost empty around Radiant, so
/// the last tier step covers 1.3 points of the population while the first covers 36.</para>
/// </summary>
static class RankModel
{
    /// <summary>Rank names, oldest to newest.</summary>
    public static readonly string[] Names = { "Iron", "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Ascendant", "Immortal", "Radiant" };

    /// <summary>Spanish rank names, oldest to newest.</summary>
    public static readonly string[] NamesEs = { "Hierro", "Bronce", "Plata", "Oro", "Platino", "Diamante", "Ascendente", "Inmortal", "Radiante" };

    /// <summary>Share of ranked players in each rank (Sep 2026): Iron 4.7%, Bronze 15.7%, Silver 20.7%, Gold 22.1%,
    /// Platinum 17.6%, Diamond 11.6%, Ascendant 6.3%, Immortal 1.4%, Radiant 0.05%.</summary>
    public static readonly float[] Share = { 4.7f, 15.7f, 20.7f, 22.1f, 17.6f, 11.6f, 6.3f, 1.4f, 0.05f };

    /// <summary>Population percentile at the bottom edge of each rank (the same distribution as <see cref="Share"/>).</summary>
    public static readonly float[] Start = PopulationStarts();

    static float[] PopulationStarts()
    {
        // Rounded calibration shares need not sum to exactly 100. Normalize them so every band
        // has a positive percentile interval, including Radiant at the top of the distribution.
        float total = Share.Sum(), cumulative = 0;
        var starts = new float[Share.Length];
        for (int i = 0; i < Share.Length; i++)
        {
            starts[i] = cumulative / total * 100f;
            cumulative += Share[i];
        }
        return starts;
    }

    /// <summary>The benchmark tiers b0…b4 sit at these population percentiles (see <see cref="Bench"/>).</summary>
    static readonly float[] AnchorPct = { 5f, 41f, 81f, 98.6f, 99.9f };

    /// <summary>Normal quantile of each anchor (so the tier scale is linear in z).</summary>
    static readonly double[] AnchorZ = { -1.6449, -0.2279, 0.8779, 2.1919, 3.0902 };

    /// <summary>Bottom of the tier scale (below every rank).</summary>
    public const float Min = -0.5f;
    /// <summary>Top of the tier scale (above Radiant).</summary>
    public const float Max = 4.5f;
    /// <summary>Top of the ladder's axis: the nine rank bands end at Radiant's 4.0, so the drawn segments line up
    /// with the tier boundaries <see cref="Band"/> uses.</summary>
    public const float LadderMax = 4.0f;

    /// <summary>Width of a rank band on the tier scale: nine bands fill the scale from Iron to Radiant.</summary>
    public const float BandWidth = 0.5f;

    // ---------------- tier ↔ percentile ----------------

    /// <summary>
    /// Normal quantile of a tier. The anchors sit one tier apart, so each segment's slope is the difference of its
    /// two anchor z values; outside the anchors the nearest segment is extended.
    /// </summary>
    public static double ZAt(float tier)
    {
        if (float.IsNaN(tier)) return double.NaN;
        int i = 0;
        while (i < AnchorZ.Length - 2 && tier > i + 1) i++;
        double slope = AnchorZ[i + 1] - AnchorZ[i];
        return AnchorZ[i] + (tier - i) * slope;
    }

    /// <summary>Population percentile of a tier: the share of ranked players whose mechanics it beats.</summary>
    public static float Percentile(float tier)
    {
        if (float.IsNaN(tier)) return float.NaN;
        return (float)(100.0 * Phi(ZAt(tier)));
    }

    /// <summary>Inverse of <see cref="Percentile"/>: the tier that sits at a given population percentile.</summary>
    public static float TierOfPercentile(float pct)
    {
        if (float.IsNaN(pct)) return float.NaN;
        double z = Probit(pct / 100.0);
        int i = 0;
        while (i < AnchorZ.Length - 2 && z > AnchorZ[i + 1]) i++;
        double slope = AnchorZ[i + 1] - AnchorZ[i];
        return (float)(i + (z - AnchorZ[i]) / slope);
    }

    /// <summary>
    /// Standard deviation of the tier scale across the whole ranked population: how far a random player's mechanics
    /// sit from the median one. It is the natural width of the "you could be anywhere" prior the estimate shrinks
    /// toward, so it sets the floor of the uncertainty band.
    /// </summary>
    public static float PopulationSd { get; } = ComputePopulationSd();

    static float ComputePopulationSd()
    {
        // Inside a rank the population is spread over that rank's percentile range, so each player's tier is the
        // inverse percentile of their position inside the band. Sampling the bands is enough for a spread.
        const int SamplesPerRank = 64;
        double sum = 0, sum2 = 0;
        double weight = 0;
        for (int r = 0; r < Share.Length; r++)
        {
            float lo = Start[r], hi = r + 1 < Start.Length ? Start[r + 1] : 100f;
            for (int k = 0; k < SamplesPerRank; k++)
            {
                double p = lo + (hi - lo) * (k + 0.5) / SamplesPerRank;
                double t = TierOfPercentile((float)Math.Min(p, 99.999));
                double w = Share[r] / SamplesPerRank;
                sum += w * t; sum2 += w * t * t; weight += w;
            }
        }
        if (weight == 0) return 1f;
        double mean = sum / weight;
        double var = Math.Max(0.0001, sum2 / weight - mean * mean);
        return (float)Math.Sqrt(var);
    }

    // ---------------- rank bands ----------------

    /// <summary>The rank a tier falls in (0 = Iron … 8 = Radiant); −1 when the tier is unknown.</summary>
    public static int Band(float tier)
    {
        if (float.IsNaN(tier)) return -1;
        if (tier < 0) return 0;                    // Iron
        if (tier >= 8 * BandWidth - BandWidth) return 8;     // Radiant
        return Math.Clamp(1 + (int)(tier / BandWidth), 1, 7);
    }

    /// <summary>The division inside the rank (1…3); Radiant has no divisions.</summary>
    public static int Division(float tier)
    {
        int i = Band(tier);
        if (i <= 0 || i == 8) return i == 8 ? 0 : 1;
        float lo = (i - 1) * BandWidth;
        return Math.Clamp(1 + (int)((tier - lo) / (BandWidth / 3f)), 1, 3);
    }

    /// <summary>Rank name for a tier ("Gold"); "Unrated" below the scale.</summary>
    public static string Name(float tier)
    {
        if (float.IsNaN(tier) || tier < Min - 0.25f) return "Unrated";
        return Names[Band(tier)];
    }

    /// <summary>Spanish rank name for a tier ("Oro").</summary>
    public static string NameEs(float tier)
    {
        if (float.IsNaN(tier) || tier < Min - 0.25f) return Loc.Spanish ? "SIN VALORAR" : "Unrated";
        return NamesEs[Band(tier)];
    }

    /// <summary>
    /// How far the tier is from the bottom of its band, 0…1: how much is left to earn the next division.
    /// </summary>
    public static float DivisionProgress(float tier)
    {
        if (float.IsNaN(tier)) return 0;
        int i = Band(tier);
        if (i <= 0 || i == 8) return 1f;
        float lo = (i - 1) * BandWidth;
        return Math.Clamp((tier - lo) / BandWidth, 0f, 1f);
    }

    // ---------------- normal distribution helpers ----------------

    /// <summary>Standard normal cumulative distribution.</summary>
    static double Phi(double z) => 0.5 * Erfc(-z / Math.Sqrt(2.0));

    /// <summary>Complementary error function (Numerical Recipes' rational approximation, ~1.2e-7 accurate).</summary>
    static double Erfc(double x)
    {
        double z = Math.Abs(x);
        double t = 2.0 / (2.0 + z);
        double ty = 4.0 * t - 2.0;
        double[] cof = { -1.3026537197817094, 6.4196979235649026e-1, 1.9476473204185836e-2, -9.561514786808631e-3,
            -9.46595344482036e-4, 3.66839497852761e-4, 4.2523324806907e-5, -2.0278578112534e-5, -1.624290004647e-6,
            1.303655835580e-6, 1.5626441722e-8, -8.5238095915e-8, 6.529054439e-9, 5.059343495e-9, -9.91364156e-10,
            -2.27365122e-10, 9.6467911e-11, 2.394038e-12, -6.886027e-12, 8.94487e-13, 3.13092e-13, -1.12708e-13,
            3.81e-16, 7.106e-15 };
        double d = 0, dd = 0;
        for (int j = cof.Length - 1; j > 0; j--) { double tmp = d; d = ty * d - dd + cof[j]; dd = tmp; }
        double ans = t * Math.Exp(-z * z + 0.5 * (cof[0] + ty * d) - dd);
        return x >= 0.0 ? ans : 2.0 - ans;
    }

    /// <summary>Inverse of the standard normal cumulative distribution (Acklam's rational approximation).</summary>
    static double Probit(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        double[] a = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02,
            -3.066479806614716e+01, 2.506628277459239e+00 };
        double[] b = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01,
            -1.328068155288572e+01 };
        double[] c = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00,
            4.374664141464968e+00, 2.938163982698783e+00 };
        double[] d = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };
        const double low = 0.02425, high = 1 - low;
        if (p < low)
        {
            double q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        if (p > high)
        {
            double q = Math.Sqrt(-2 * Math.Log(1 - p));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        double qq = p - 0.5, r = qq * qq;
        return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * qq /
               (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
    }
}
