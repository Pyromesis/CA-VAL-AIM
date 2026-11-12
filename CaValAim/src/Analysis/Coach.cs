using CaValAim.Core;
using CaValAim.Game;

namespace CaValAim.Analysis;

/// <summary>Skill areas the coach rates (each gets a rank tier estimate).</summary>
public enum Skill { Flicking, Precision, Tracking, Reaction, CrosshairPlacement, SprayControl, Movement, Utility }

public enum Severity { Tip, Minor, Major }

/// <summary>A detected problem with evidence, the reason it happens, how to fix it and which drills train it.</summary>
public sealed record Diagnosis(
    string Id, Skill Skill, Severity Severity,
    string Title,        // "Over-flicking: your flicks travel too far"
    string Evidence,     // "62% of your flicks ended beyond the target, by 2.1° on average (48 flicks)"
    string Why,          // why it happens
    string[] Fixes,      // concrete technique cues
    string[] Drills,     // CA-VAL-AIM mode keys to practise (e.g. "flick", "spider")
    int Samples);

/// <summary>Rank estimate for one skill. Tier is continuous: 0 = Rookie … 4 = Pro, -1 = below Rookie / not enough data.</summary>
public sealed record SkillRating(Skill Skill, float Tier, float Confidence, int Samples, string Headline);

/// <summary>Sensitivity recommendation (in Valorant's own sens units at the user's DPI).</summary>
public sealed record SensAdvice(
    float CurrentSens, int Dpi, float RecommendedSens, float CurrentCm360, float RecommendedCm360,
    string Direction,    // "lower" | "higher" | "keep"
    string Reason, float Confidence);

/// <summary>Everything the Skill Profile screen shows.</summary>
public sealed class SkillProfile
{
    public float OverallTier = -1;
    public float OverallConfidence;
    /// <summary>The estimate with its plausible band: bottom, top and how much the skills disagree.</summary>
    public RankEstimate.Result? Estimate;
    /// <summary>Population percentile of the estimate (the share of ranked players it beats).</summary>
    public float OverallPercentile = float.NaN;
    public List<SkillRating> Skills = new();
    public List<Diagnosis> Problems = new();
    public List<string> Strengths = new();
    public SensAdvice? Sens;
    public SensLabData? SensLab;
    public List<AimForecast> Forecasts = new();
    public int ForecastTier;
    public float ForecastSens;
    /// <summary>Per-skill change of the rating (tiers; 0.5 ≈ one rank) between the older and the newer runs, only for skills
    /// rated in both (see <see cref="Coach.Trends"/>). Empty until there is enough history.</summary>
    public Dictionary<Skill, float> Trend = new();
    /// <summary>What the trend compares: "week" (the last 7 days vs before) or "runs" (newer half vs older half).</summary>
    public string TrendSpan = "";
    public int RunsAnalyzed;
    /// <summary>The aggregated measurements behind the ratings; the coach builds its drills from them (<see cref="CoachForge"/>).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, float> Metrics = new();
    public DateTime Updated = DateTime.Now;
}

/// <summary>Post-run review shown on the results screen.</summary>
public sealed class RunReview
{
    public List<CoachObservation> Observations = new();
    public string[] PrescribedDrills = Array.Empty<string>();
    public string Summary = "";
    public List<Diagnosis> Issues = new();
    public List<Diagnosis> Findings = new();
    public List<string> Strengths = new();
    public Dictionary<string, float> Metrics = new();
    /// <summary>Aim Lab-style overshoot / undershoot / tremor evidence from this run.</summary>
    public SensFeedback? Sens;
    /// <summary>Every play of the run, judged on its own (see <see cref="PlayByPlay"/>).</summary>
    public PlayAnalysis? Plays;
    /// <summary>Second-by-second read of the same run (lives inside <see cref="PlayAnalysis.Timeline"/>).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public RunTimeline? Timeline => Plays?.Timeline;
}

/// <summary>
/// The aim coach (docs/coach_spec.md): per-run metrics from telemetry (A), rank estimates per skill (B),
/// prioritised problems with evidence / why / fixes / drills (C) and a sensitivity recommendation (D).
/// <para>Tiers are continuous on the spec's scale: 0 ≈ Iron–Bronze … 4 ≈ Radiant (see <see cref="RankName"/>);
/// a SkillRating.Tier of −1 means "not enough data". Confidence = n/(n + n_min): &lt; 0.5 Low, &lt; 0.75 Medium.</para>
/// Problems with Severity.Tip are low-confidence ("possible") findings.
/// </summary>
public static class Coach
{
    /// <summary>Per-run metrics (stored in the run's stats record and aggregated into the profile).</summary>
    public static Dictionary<string, float> Summarize(RunTelemetry t)
    {
        var m = RunAnalysis.Run(t);
        var change = t.Events.LastOrDefault(e => e.Kind == "sens_changed");
        if (change == null || change.B <= 0 || t.Mode is SensFinderKey or CrosshairFinderKey) return m;
        // A fresh segment cannot borrow a target onset, recoil history or movement from the old setting.
        var segment = new RunTelemetry { Mode = t.Mode, Tier = t.Tier, Sens = change.B, Dpi = t.Dpi,
            Weapon = t.Weapon, Map = t.Map, When = t.When, HFov = t.HFov, ZoomSensMult = t.ZoomSensMult };
        segment.Frames.AddRange(t.Frames.Where(f => f.T > change.T + .15f));
        segment.Shots.AddRange(t.Shots.Where(f => f.T > change.T + .15f));
        segment.Events.AddRange(t.Events.Where(e => e.T > change.T + .15f && e.Kind != "sens_changed"));
        foreach (var (key, value) in RunAnalysis.Run(segment)) m["current." + key] = value;
        m["current.sens"] = change.B;
        return m;
    }

    /// <summary>What went wrong / right in this run and how to fix it.</summary>
    public static RunReview ReviewRun(RunTelemetry t, Dictionary<string, float> metrics, int tier)
    {
        var review = new RunReview { Metrics = metrics };
        if (t.Mode is SensFinderKey or CrosshairFinderKey) return review; // these drills show their own result
        review.Observations = CoachObservations.Build(t);
        review.Plays = PlayByPlay.Analyze(t);
        if (RunAnalysis.Base(t.Mode) == "reactgame") { ReactionGameCoach.Review(t, review); return review; }
        review.Sens = SensLab.Feedback(metrics, tier);
        if (metrics.Count == 0) { review.Summary = RunSummary.NoData(t.Mode); return review; }
        var a = Assess.Run(metrics, runLevel: true, tierPlayed: tier);
        review.Findings = a.Issues.Select(i => i.D).ToList();
        review.Issues = review.Findings.Take(3).ToList();
        review.Strengths = a.Strengths.Take(2).Select(s => s.Text).ToList();
        review.Summary = RunSummary.Write(t.Mode, tier, metrics, a);
        review.PrescribedDrills = CoachPlan.FromRun(review).Drills.ToArray();
        return review;
    }

    /// <summary>Aggregate profile across recent runs (stats records + recent telemetry files).</summary>
    public static SkillProfile BuildProfile(StatsStore stats, float currentSens, int dpi, int? currentTier = null)
    {
        var runs = new List<SensAdvisor.RunInfo>();
        int analysed = 0;
        SensAdvisor.FinderResult? finder = null;
        // Own snapshot (callers may run this on a worker thread while the game appends runs), newest first so the
        // bounded telemetry re-analysis below goes to the runs that matter most (and still have their files).
        var all = stats.Runs.ToArray().Where(r => r != null).OrderByDescending(r => r.When).ToList();
        foreach (var r in all)
        {
            if (r.Mode == SensFinderKey)
            {
                // Its sens changes mid-run: no skill data, but remember the latest recommendation.
                if (finder == null && r.Metrics != null && r.Metrics.TryGetValue("sensfinder.sens", out var fs) && fs > 0)
                    finder = new SensAdvisor.FinderResult(r.When, fs, (int)r.Metrics.GetValueOrDefault("sensfinder.conf"));
                continue;
            }
            if (r.Mode == CrosshairFinderKey) continue; // crosshair changes mid-run: no skill data
            var m = r.Metrics;
            if ((m == null || m.GetValueOrDefault("analysis.version") < 3) && !string.IsNullOrEmpty(r.TelemetryFile) && analysed < 10 && File.Exists(r.TelemetryFile))
            {
                m = FromFile(r.TelemetryFile!) ?? m; // bounded: ≈10–40 ms per file, cached
                analysed++;
            }
            if (m != null && m.Count > 0) runs.Add(new SensAdvisor.RunInfo(r.When, r.Sens, RunAnalysis.Normalize(r.Mode, r.Tier, m), $"{r.Mode}|{r.Tier}|{r.Map}"));
        }
        var agg = Agg.Aggregate(runs.Select(r => (r.When, r.M)), DateTime.Now);
        int tierGuess = (int)MathF.Round((float)all.Take(20).Where(r => r.Tier >= 0).Select(r => r.Tier).DefaultIfEmpty(1).Average());
        var a = Assess.Run(agg, runLevel: false, tierPlayed: tierGuess);
        int forecastTier = currentTier ?? tierGuess;
        var p = new SkillProfile
        {
            OverallTier = a.Overall,
            OverallConfidence = a.OverallConf,
            Estimate = a.Estimate,
            OverallPercentile = a.Estimate is { } est && !float.IsNaN(est.Tier) ? RankModel.Percentile(est.Tier) : float.NaN,
            Skills = a.Ratings.Values.OrderBy(s => s.Skill).ToList(),
            Problems = a.Issues.Select(i => i.D).ToList(),
            Strengths = a.Strengths.Where(s => s.Score >= 0.5f || a.Overall < 0).Take(4).Select(s => s.Text).ToList(),
            RunsAnalyzed = runs.Count,
            Metrics = agg,
            Forecasts = CoachNetwork.Forecast(all, currentSens, forecastTier),
            ForecastTier = forecastTier,
            ForecastSens = currentSens,
            Updated = DateTime.Now,
        };
        var sensRuns = runs.Select(r => {
            if (r.M.GetValueOrDefault("sens.changed") == 0) return r;
            var segment = r.M.Where(kv => kv.Key.StartsWith("current.")).ToDictionary(kv => kv.Key[8..], kv => kv.Value);
            return new SensAdvisor.RunInfo(r.When, segment.GetValueOrDefault("sens"), segment, r.Context);
        }).Where(r => r.Sens > 0).ToList();
        p.Sens = SensAdvisor.Advise(sensRuns, currentSens, dpi, a.RefTier, finder);
        p.SensLab = SensLab.Build(sensRuns, currentSens, dpi, a.RefTier);
        (p.Trend, p.TrendSpan) = Trends(runs.Select(r => (r.When, r.M)).ToList(), tierGuess);
        return p;
    }

    /// <summary>
    /// Is each skill getting better? Rates the older runs and the newer runs separately and returns the difference
    /// (newer − older, in tiers). The split is the last 7 days against everything before when both sides have at least
    /// 6 runs; otherwise, from 16 runs on, the newer half against the older half. A skill needs a rating with at least
    /// low-medium confidence (0.3) on both sides to get a trend.
    /// </summary>
    internal static (Dictionary<Skill, float> Trend, string Span) Trends(List<(DateTime When, Dictionary<string, float> M)> runs, int tierGuess)
    {
        var trend = new Dictionary<Skill, float>();
        if (runs.Count < 12) return (trend, "");
        var now = DateTime.Now;
        var ordered = runs.OrderBy(r => r.When).ToList();
        var cut = now.AddDays(-7);
        var older = ordered.Where(r => r.When < cut).ToList();
        var newer = ordered.Where(r => r.When >= cut).ToList();
        string span = "week";
        if (older.Count < 6 || newer.Count < 6)
        {
            if (ordered.Count < 16) return (trend, "");
            older = ordered.Take(ordered.Count / 2).ToList();
            newer = ordered.Skip(ordered.Count / 2).ToList();
            span = "runs";
        }
        // Each side is rated as of its own newest run, so the recency weighting doesn't fade the older side away.
        var aOld = Assess.Run(Agg.Aggregate(older, older[^1].When), runLevel: false, tierPlayed: tierGuess);
        var aNew = Assess.Run(Agg.Aggregate(newer, newer[^1].When), runLevel: false, tierPlayed: tierGuess);
        foreach (var (skill, rn) in aNew.Ratings)
        {
            if (!aOld.Ratings.TryGetValue(skill, out var ro)) continue;
            if (rn.Tier < -0.9f || ro.Tier < -0.9f || rn.Confidence < 0.3f || ro.Confidence < 0.3f) continue;
            trend[skill] = rn.Tier - ro.Tier;
        }
        return (trend, trend.Count > 0 ? span : "");
    }

    const string SensFinderKey = "sensfinder", CrosshairFinderKey = "xhairfinder";

    /// <summary>Rank name for a continuous tier (Iron &lt; 0 ≤ Bronze &lt; 0.5 ≤ Silver … 3.5 ≤ Radiant; −1 = "Unrated").</summary>
    public static string RankName(float tier) => Bench.Rank(tier);

    /// <summary>cm per 360° turn: 13063 / (sens · dpi).</summary>
    public static float Cm360(float sens, int dpi) => SensAdvisor.Cm360(sens, dpi);

    /// <summary>Plain-language name of a skill (in the UI language, so it reads naturally inside coach sentences).</summary>
    public static string SkillName(Skill s)
    {
        bool es = Loc.Spanish;
        return s switch
        {
            Skill.CrosshairPlacement => es ? "Colocación de mira" : "Crosshair placement",
            Skill.SprayControl => es ? "Control de spray" : "Spray control",
            Skill.Precision => es ? "Precisión" : "Precision (micro-adjustment)",
            Skill.Utility => es ? "Utilidad" : "Utility (flashes)",
            Skill.Tracking => es ? "Seguimiento" : "Tracking",
            Skill.Reaction => es ? "Reacción" : "Reaction",
            Skill.Movement => es ? "Movimiento" : "Movement",
            _ => s.ToString(),
        };
    }

    // Telemetry re-analysis cache (runs saved before metrics existed). Keyed by file name + write time.
    static readonly Dictionary<string, Dictionary<string, float>> fileCache = new();

    static Dictionary<string, float>? FromFile(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            string key = file + "|" + File.GetLastWriteTimeUtc(file).Ticks;
            lock (fileCache)
                if (fileCache.TryGetValue(key, out var hit)) return hit;
            var t = RunTelemetry.Load(file);
            if (t == null) return null;
            var m = Summarize(t);
            lock (fileCache) fileCache[key] = m;
            return m;
        }
        catch { return null; }
    }
}
