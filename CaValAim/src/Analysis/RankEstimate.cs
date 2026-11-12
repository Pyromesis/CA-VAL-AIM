using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>
/// How sure the coach is about an overall rank, and how wide the band around it is.
/// <para>The old model was a single number: the confidence was a weighted share of the skills that had data, and the
/// band was a linear function of it (±0.15 tier at full evidence, ±0.45 at none). That band was wrong in three
/// ways. It did not widen when the skills disagreed with each other (a Diamond flick next to an Iron track is a very
/// different situation from eight consistent Diamond skills), it ignored that the tier scale is not linear in
/// population terms (near Radiant a third of a tier is a huge jump; around Gold it is a division), and it treated
/// "no data at all" and "a little data" as the same shape.</para>
/// <para>This model spreads the uncertainty the way the evidence actually arrives:</para>
/// <list type="bullet">
/// <item><b>Per-skill spread</b>: each rated skill contributes a tier with its own standard error, driven by its
/// confidence. A skill rated off 30 samples is far less sure than one rated off 300.</item>
/// <item><b>Disagreement</b>: the weighted spread between the skills' tiers. When they disagree, the mean is a
/// compromise between contradictory measurements and the estimate must say so.</item>
/// <item><b>Coverage</b>: skills with no data yet are not zero, they are unknown. The missing weight widens the band
/// by how much it could be worth.</item>
/// <item><b>Population prior</b>: even with perfect data, aim mechanics only partly predict rank (game sense, comms
/// and utility matter too), so the band never collapses to a point.</item>
/// </list>
/// </summary>
public static class RankEstimate
{
    /// <summary>How much of a rank the aim mechanics explain at best (the rest is game sense, comms and utility).</summary>
    public const float Validity = 0.88f;

    /// <summary>The tier the median ranked player sits at (Gold 2, ≈ the 50th percentile).</summary>
    public const float PriorTier = Bench.PriorTier;

    /// <summary>Tier width of one standard error on a single skill's rating, at full confidence.</summary>
    public const float SkillError = 0.28f;

    /// <summary>Tier width of one standard error on a single skill's rating, at zero confidence.</summary>
    public const float SkillErrorLow = 0.85f;

    /// <summary>Extra tier width the population prior always adds (the part aim mechanics cannot explain).</summary>
    public const float PriorWidth = 0.30f;

    /// <summary>The result of estimating one overall rank.</summary>
    public sealed record Result(
        float Tier,          // the point estimate (tier scale)
        float Low,           // bottom of the plausible band (tier scale)
        float High,          // top of the plausible band (tier scale)
        float Confidence,    // 0…1: how much of the rank the evidence supports
        float Spread,        // half-width of the band in tier units
        int Rated,           // skills with data
        int Total,           // skills the coach rates
        float Disagreement); // weighted spread between the skills' tiers (0 = they agree)

    /// <summary>
    /// Estimates the overall rank from the per-skill ratings.
    /// <para>The point estimate keeps the spec's rule (a confidence-weighted robust mean, clipped around the weighted
    /// median so one standout skill cannot carry it, shrunk toward the median player by how much evidence there is).
    /// The band around it is what this class adds.</para>
    /// </summary>
    public static Result Estimate(IReadOnlyDictionary<Skill, SkillRating> ratings)
    {
        var rated = ratings.Values.Where(r => r.Tier > -0.9f && r.Confidence > 0).ToList();
        int total = ratings.Count;
        if (rated.Count == 0)
            return new Result(float.NaN, float.NaN, float.NaN, 0f, 0f, 0, total, 0f);

        float wAll = Assess.SkillWeight.Values.Sum();
        float wc = rated.Sum(r => Assess.SkillWeight[r.Skill] * r.Confidence);
        float evidence = wc / wAll;
        if (wc <= 0) return new Result(float.NaN, float.NaN, float.NaN, 0f, 0f, 0, total, 0f);

        // ---- point estimate (spec B: robust weighted mean, shrunk toward the median player) ----
        float med = Assess.Median(rated.Select(r => (r.Tier, Assess.SkillWeight[r.Skill] * r.Confidence)).ToList());
        float s = 0;
        foreach (var r in rated)
            s += Assess.SkillWeight[r.Skill] * r.Confidence * Math.Clamp(r.Tier, med - Bench.OutlierClip, med + Bench.OutlierClip);
        float raw = s / wc;
        float tier = Bench.Shrink(raw, evidence);

        // ---- band ----
        // 1. How much do the skills disagree? A weighted spread around the median.
        float spread = 0, wsum = 0;
        foreach (var r in rated)
        {
            float w = Assess.SkillWeight[r.Skill] * r.Confidence;
            spread += w * (r.Tier - med) * (r.Tier - med);
            wsum += w;
        }
        float disagreement = wsum > 0 ? (float)Math.Sqrt(spread / wsum) : 0f;

        // 2. How unsure is each skill? Its own error, weighted by how much it contributes.
        float err = 0;
        foreach (var r in rated)
        {
            float w = Assess.SkillWeight[r.Skill] * r.Confidence / wc;   // normalised share of the estimate
            float se = SkillError + (SkillErrorLow - SkillError) * (1f - Math.Clamp(r.Confidence, 0f, 1f));
            err += w * w * se * se;
        }
        float skillError = (float)Math.Sqrt(err);

        // 3. How much of the rank is still unmeasured? The missing weight could be anywhere in the population.
        float missing = 1f - Math.Clamp(wc / wAll, 0f, 1f);
        float coverageError = missing * RankModel.PopulationSd;

        // 4. The disagreement and the uncovered part both add to the width.
        float halfWidth = (float)Math.Sqrt(
            skillError * skillError
            + (0.55f * disagreement) * (0.55f * disagreement)
            + coverageError * coverageError
            + PriorWidth * PriorWidth);

        // A tighter band is only honest when the estimate itself is more certain: with little evidence the point
        // estimate sits near the population median and the band must cover most of the population.
        float shrink = Bench.ShrinkFactor(evidence);
        halfWidth = Math.Max(halfWidth, (1f - shrink) * RankModel.PopulationSd * 0.55f);

        float low = Math.Max(RankModel.Min, tier - halfWidth);
        float high = Math.Min(RankModel.Max, tier + halfWidth);
        float confidence = Math.Clamp(evidence * (1f - Math.Clamp(disagreement / 1.5f, 0f, 0.8f)), 0f, 1f);

        return new Result(tier, low, high, confidence, halfWidth, rated.Count, total, disagreement);
    }
}
