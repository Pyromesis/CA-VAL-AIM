namespace ValTrainer.Analysis;

/// <summary>What the player's aim did with the sensitivity they used: too fast, too slow, unstable or balanced.</summary>
public enum SensVerdict { NoData, Balanced, Overshoot, Undershoot, Mixed, Shaky, Tracking }

/// <summary>One run's sensitivity signals, shown on the results screen.</summary>
public sealed record SensFeedback(
    int Flicks, int OffTarget, float TrackSeconds,
    float OvershootShare, float UndershootShare,
    float MeanOvershootPct, float MeanUndershootDeg,
    float Corrections, float Jitter, float TrackOnPct,
    float Delta, float Confidence, SensVerdict Verdict)
{
    public bool HasFlickData => Flicks > 0 || OffTarget > 0;
    public bool HasTrackData => TrackSeconds > 0;
    public bool HasData => HasFlickData || HasTrackData;
}

/// <summary>All runs played at one sensitivity, aggregated for the Sens Lab history.</summary>
public sealed record SensBlock(
    float Sens, int Runs, int Sessions, DateTime First, DateTime Last,
    int Flicks, int OffTarget, float TrackSeconds,
    float OvershootShare, float UndershootShare,
    float MeanOvershootPct, float MeanUndershootDeg,
    float Corrections, float Jitter, float TrackOnPct,
    float Delta, float Confidence, SensVerdict Verdict)
{
    public bool HasData => Flicks > 0 || OffTarget > 0 || TrackSeconds > 0;
}

/// <summary>Accumulated sensitivity evidence (current sens + recent sens blocks).</summary>
public sealed class SensLabData
{
    public float CurrentSens;
    public int Dpi;
    public SensBlock? Current;
    public List<SensBlock> History = new();
    public int Runs;
    public int Sessions;

    public bool HasData => Current is { HasData: true } || History.Any(h => h.HasData);
}

/// <summary>
/// Aim Lab-style sensitivity lab: turns the existing flick/tracking telemetry into overshoot, undershoot,
/// correction, tremor (jitter) and tracking signals, then accumulates them per sensitivity.
/// <para>The actual recommendation stays in <see cref="SensAdvisor"/> (H1–H5 signals, confidence gates and
/// guardrails). This class only exposes the evidence in a form the player can inspect and learn from.</para>
/// </summary>
public static class SensLab
{
    const float SessionGapHours = 0.75f;
    const float SameSensPct = 0.02f;

    static float Get(Dictionary<string, float> m, string k) => m.TryGetValue(k, out var v) && !float.IsNaN(v) ? v : 0;
    static int I(Dictionary<string, float> m, string k) => (int)MathF.Round(Get(m, k));
    static float Clip(float x) => Math.Clamp(x, -1f, 1f);

    public static bool SameSens(float a, float b) => a > 0 && b > 0 && MathF.Abs(a - b) / b < SameSensPct;

    /// <summary>Signals from one run's metric dictionary.</summary>
    public static SensFeedback Feedback(Dictionary<string, float> m, float refTier)
    {
        int flicks = I(m, "flick.n_prim");
        int off = I(m, "flick.n_off");
        float secs = Get(m, "track.secs");
        float over = Get(m, "flick.overshoot_share");
        float under = Get(m, "flick.under_share");
        float jitter = Get(m, "track.jitter");
        float on = Get(m, "track.ontarget");
        float corr = Get(m, "flick.nc");
        float conf = Confidence(off, secs);
        bool enough = conf >= 0.35f && (off >= 8 || secs >= 15f);
        var verdict = enough ? Classify(over, under, off, flicks, jitter, on, secs, refTier) : SensVerdict.NoData;
        float delta = SensAdvisor.Step(m, refTier).Delta;
        return new SensFeedback(flicks, off, secs, over, under,
            Get(m, "flick.mean_overshoot_pct"), Get(m, "flick.mean_undershoot_deg"),
            corr, jitter, on, float.IsNaN(delta) ? 0 : delta, conf, verdict);
    }

    /// <summary>Groups runs by sensitivity and aggregates the newest useful blocks.</summary>
    internal static SensLabData Build(IEnumerable<SensAdvisor.RunInfo> runs, float currentSens, int dpi, float refTier)
    {
        var all = runs.Where(r => r.Sens > 0 && r.M is { Count: > 0 }).OrderBy(r => r.When).ToList();
        var groups = new List<(float Sens, List<SensAdvisor.RunInfo> Runs)>();
        foreach (var r in all)
        {
            int hit = groups.FindIndex(g => SameSens(g.Sens, r.Sens));
            if (hit < 0) groups.Add((r.Sens, new List<SensAdvisor.RunInfo> { r }));
            else groups[hit].Runs.Add(r);
        }

        var blocks = groups.Select(g => Block(g.Sens, g.Runs, refTier)).Where(b => b.HasData).OrderBy(b => b.Last).ToList();
        var currentRuns = all.Where(r => SameSens(r.Sens, currentSens)).ToList();
        var data = new SensLabData
        {
            CurrentSens = currentSens,
            Dpi = dpi,
            Runs = all.Count,
            Sessions = Sessions(all),
            History = blocks.TakeLast(10).ToList(),
        };
        if (currentRuns.Count > 0) data.Current = Block(currentSens, currentRuns, refTier);
        return data;
    }

    static SensBlock Block(float sens, List<SensAdvisor.RunInfo> runs, float refTier)
    {
        var m = Agg.Aggregate(runs.Select(r => (r.When, r.M)), DateTime.Now);
        int flicks = I(m, "flick.n_prim"), off = I(m, "flick.n_off");
        float secs = Get(m, "track.secs");
        float over = Get(m, "flick.overshoot_share"), under = Get(m, "flick.under_share");
        float jitter = Get(m, "track.jitter"), on = Get(m, "track.ontarget");
        float conf = Confidence(off, secs);
        float delta = SensAdvisor.Step(m, refTier).Delta;
        bool enough = conf >= 0.35f && (off >= 8 || secs >= 15f);
        var verdict = enough ? Classify(over, under, off, flicks, jitter, on, secs, refTier) : SensVerdict.NoData;
        return new SensBlock(sens, runs.Count, Sessions(runs), runs.Min(r => r.When), runs.Max(r => r.When),
            flicks, off, secs, over, under,
            Get(m, "flick.mean_overshoot_pct"), Get(m, "flick.mean_undershoot_deg"),
            Get(m, "flick.nc"), jitter, on,
            float.IsNaN(delta) ? 0 : delta, conf, verdict);
    }

    static float Confidence(int off, float secs)
    {
        float a = off <= 0 ? 0 : Math.Min(1f, off / 40f);
        float b = secs <= 0 ? 0 : Math.Min(1f, secs / 90f);
        return (a + b) / 2f;
    }

    static SensVerdict Classify(float over, float under, int off, int flicks, float jitter, float on, float secs, float refTier)
    {
        bool hasFlick = off >= 6 || flicks >= 10;
        bool hasTrack = secs >= 8;
        if (!hasFlick && !hasTrack) return SensVerdict.NoData;

        // Tremor first: a shaky hand changes the meaning of an over/undershoot (it can produce both).
        float bandJ = Bench.ValueAt(refTier, Bench.Jitter);
        if (hasTrack && jitter > bandJ * 1.35f) return SensVerdict.Shaky;
        if (hasTrack && on > 0 && on < Bench.ValueAt(refTier, Bench.TrackOnPct) * 0.8f) return SensVerdict.Tracking;

        if (hasFlick)
        {
            if (over >= 0.55f && under <= 0.35f) return SensVerdict.Overshoot;
            if (under >= 0.55f && over <= 0.35f) return SensVerdict.Undershoot;
            if (over >= 0.32f && under >= 0.32f) return SensVerdict.Mixed;
        }
        return SensVerdict.Balanced;
    }

    static int Sessions(List<SensAdvisor.RunInfo> runs)
    {
        int n = 0;
        DateTime last = DateTime.MinValue;
        foreach (var r in runs.OrderBy(r => r.When))
        {
            if (n == 0 || (r.When - last).TotalHours > SessionGapHours) n++;
            last = r.When;
        }
        return n;
    }

    /// <summary>False when the evidence is too thin for a real recommendation (the UI shows a "keep collecting" note).</summary>
    public static bool Enough(SensFeedback f) => f.Confidence >= 0.35f && (f.OffTarget >= 8 || f.TrackSeconds >= 15f);
    public static bool Enough(SensBlock b) => b.Confidence >= 0.35f && (b.OffTarget >= 8 || b.TrackSeconds >= 15f);

    /// <summary>Net bias in [-1, 1]: + = overshooting, - = stopping short, 0 = balanced.</summary>
    public static float Bias(SensBlock b) => Clip(b.OvershootShare - b.UndershootShare);
    public static float Bias(SensFeedback f) => Clip(f.OvershootShare - f.UndershootShare);
}
