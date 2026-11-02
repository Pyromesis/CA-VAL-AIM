using CaValAim.Core;
using CaValAim.Game;

namespace CaValAim.Analysis;

/// <summary>How one second of a run felt: good, worth watching, neutral or bad.</summary>
public enum TimelineTone { Bad, Warn, Neutral, Good }

/// <summary>
/// One second of a run, measured from the real telemetry: frames, targets, aim error, shots, movement and events.
/// Every value is stored, so the coach can explain the second later without the original .vtt file.
/// </summary>
public sealed class TimelineSecond
{
    public int Index;                 // 0-based second since the run started
    public float T;                   // start of the second
    public int Frames;                // valid telemetry frames in the second
    public int FocusFrames;           // frames with a target in view
    public int OnTargetFrames;        // frames whose crosshair was inside the target radius
    public int Shots, Hits, Headshots, MovingShots;
    public int FiringFrames, AdsFrames, BlindFrames, AirborneFrames, CrouchFrames;
    public float AimErrR;             // mean crosshair error / target radius (0 = perfect, 1 = edge)
    public float AimErrDeg;           // mean crosshair error in degrees
    public float OnTargetPct;         // 0..1 of focused frames inside the target
    public float Accuracy;            // 0..1 of shots that hit
    public float ShotErrDeg;          // mean bullet distance from target centre, all shots
    public float SpeedAvg, SpeedMax;  // player ground speed (m/s)
    public float MouseCounts;         // raw mouse counts in the second
    public float TurnDeg;             // crosshair angular travel in the second
    public float BiasYaw, BiasPitch;  // mean signed focus error / radius (+yaw = crosshair left of target)
    public int Events, Kills, Deaths, Spawns, Reactions, Stops, Flashes, Sprays;
    public int Plays;                 // plays that overlap this second
    public int Score;                 // 0..100, the coach's read of this second
    public string Tag = "";           // stable reason code, localised by TimelineText

    /// <summary>No target, no shot and no event, and the player was almost still.</summary>
    public bool Dead => Frames > 0 && FocusFrames == 0 && Shots == 0 && Events == 0 && SpeedAvg < 0.35f;
}

/// <summary>A conclusion drawn from the per-second timeline. Codes are stable; the text is localised when shown.</summary>
public sealed class TimelineFinding
{
    public string Code = "";
    public TimelineTone Tone = TimelineTone.Neutral;
    public float T0, T1;
    /// <summary>First value of the finding (see <see cref="TimelineText"/> for its meaning per code).</summary>
    public float A;
    /// <summary>Second value of the finding (see <see cref="TimelineText"/> for its meaning per code).</summary>
    public float B;
    public int N;
    public int Second = -1;

    public string Title() => TimelineText.Title(this);
    public string Detail() => TimelineText.Detail(this);
}

/// <summary>Aggregate of the whole timeline: enough to describe the run in one line and to compare runs later.</summary>
public sealed class TimelineSummary
{
    public int Seconds;               // seconds in the run (including empty ones)
    public int ScoredSeconds;         // seconds with frames, shots or events
    public int ActiveSeconds;         // seconds with aim, shots or events
    public int AimSeconds;            // seconds with a target in view
    public int DeadSeconds;
    public int BlindSeconds;
    public int Kills, Deaths;
    public int Shots, Hits, Headshots, MovingShots;
    public float Accuracy, HeadshotPct, MovingShotPct;
    public float AimErrR, AimErrDeg, OnTargetPct;
    public float DeadPct;
    public float Average, Consistency; // mean and standard deviation of the per-second score
    public float FirstThird, LastThird, Trend;
    public int BestSecond = -1, WorstSecond = -1;
    public float BestScore, WorstScore;
    public int BestWindow = -1, WorstWindow = -1;
    public float BestWindowScore, WorstWindowScore;
    public float ShotsPerMinute, HitsPerMinute;
    public float MousePerSecond, AvgSpeed, MovingPct;
    public float BiasYaw, BiasPitch;
}

/// <summary>
/// Second-by-second analysis of one run. It uses only what the game recorded (frames, shots and events), so it works on
/// old runs too, and it is stored with the run's coach archive. No arbitrary cap: a 10-minute run keeps 600 seconds.
/// </summary>
public sealed class RunTimeline
{
    public float Bucket = 1f;
    public float Duration;
    public List<TimelineSecond> Seconds = new();
    public TimelineSummary Summary = new();
    public List<TimelineFinding> Findings = new();

    public bool HasData => Seconds.Count > 0 && (Summary.ScoredSeconds > 0 || Summary.Shots > 0 || Summary.Kills > 0);

    public int PageCount(int pageSize) => Math.Max(1, (Seconds.Count + Math.Max(1, pageSize) - 1) / Math.Max(1, pageSize));

    /// <summary>The findings worth putting in the results panel (not the best/worst highlight lines).</summary>
    public IEnumerable<TimelineFinding> Actionable(int max)
    {
        foreach (var f in Findings)
        {
            if (f.Code is "best" or "worst" or "best_window" or "worst_window" or "steady" or "accuracy_high" or "no_data") continue;
            if (max-- <= 0) yield break;
            yield return f;
        }
    }

    /// <summary>Builds the timeline. Never throws: a malformed run gets an empty timeline.</summary>
    public static RunTimeline Build(RunTelemetry t, PlayAnalysis? plays = null)
    {
        var tl = new RunTimeline();
        try { tl.Fill(t, plays); }
        catch (Exception e) { Log.Error($"Timeline failed for {t.Mode}: {e.Message}"); }
        return tl;
    }

    void Fill(RunTelemetry t, PlayAnalysis? plays)
    {
        float last = 0;
        foreach (var f in t.Frames) if (float.IsFinite(f.T) && f.T > last) last = f.T;
        foreach (var s in t.Shots) if (float.IsFinite(s.T) && s.T > last) last = s.T;
        foreach (var e in t.Events) if (float.IsFinite(e.T) && e.T > last) last = e.T;
        Duration = float.IsFinite(t.Duration) && t.Duration > 0
            ? MathF.Max(t.Duration, last)
            : last + 0.05f;
        if (Duration <= 0) return;
        // A day is already absurd for a training run; the cap only protects the process from corrupt input.
        int n = Math.Clamp((int)MathF.Ceiling(Duration), 1, 24 * 60 * 60);

        var frames = new int[n];
        var focus = new int[n];
        var onTarget = new int[n];
        var firing = new int[n];
        var ads = new int[n];
        var blind = new int[n];
        var airborne = new int[n];
        var crouch = new int[n];
        var errR = new double[n];
        var errDeg = new double[n];
        var biasYaw = new double[n];
        var biasPitch = new double[n];
        var speedSum = new double[n];
        var speedMax = new float[n];
        var mouse = new double[n];
        var turn = new double[n];
        var shots = new int[n];
        var hits = new int[n];
        var heads = new int[n];
        var movingShots = new int[n];
        var shotErr = new double[n];
        var events = new int[n];
        var kills = new int[n];
        var deaths = new int[n];
        var spawns = new int[n];
        var reactions = new int[n];
        var stops = new int[n];
        var flashes = new int[n];
        var sprays = new int[n];
        var playCount = new int[n];

        int B(float time) => Math.Clamp((int)(time / Bucket), 0, n - 1);

        float prevT = float.NaN, prevYaw = 0, prevPitch = 0;
        foreach (var f in t.Frames)
        {
            if (!float.IsFinite(f.T) || f.T < 0) continue;
            int b = B(f.T);
            frames[b]++;
            if (float.IsFinite(f.Dx) && float.IsFinite(f.Dy)) mouse[b] += MathF.Abs(f.Dx) + MathF.Abs(f.Dy);
            if (f.Has(FrameFlags.Firing)) firing[b]++;
            if (f.Has(FrameFlags.Ads)) ads[b]++;
            if (f.Has(FrameFlags.Blind)) blind[b]++;
            if (f.Has(FrameFlags.Airborne)) airborne[b]++;
            if (f.Has(FrameFlags.Crouch)) crouch[b]++;
            if (float.IsFinite(f.Speed))
            {
                speedSum[b] += f.Speed;
                speedMax[b] = MathF.Max(speedMax[b], f.Speed);
            }
            if (f.FocusId != 0 && float.IsFinite(f.FocusYaw) && float.IsFinite(f.FocusPitch) &&
                float.IsFinite(f.FocusRadius) && f.FocusRadius > 0.01f)
            {
                float e = MathF.Sqrt(f.FocusYaw * f.FocusYaw + f.FocusPitch * f.FocusPitch);
                if (float.IsFinite(e))
                {
                    focus[b]++;
                    float r = MathF.Max(0.05f, f.FocusRadius);
                    errR[b] += e / r;
                    errDeg[b] += e;
                    biasYaw[b] += f.FocusYaw / r;
                    biasPitch[b] += f.FocusPitch / r;
                    if (e <= f.FocusRadius) onTarget[b]++;
                }
            }
            if (float.IsFinite(prevT) && float.IsFinite(f.Yaw) && float.IsFinite(f.Pitch))
            {
                float dt = f.T - prevT;
                if (dt > 0 && dt < 0.5f)
                {
                    float dy = f.Yaw - prevYaw, dp = f.Pitch - prevPitch;
                    float d = MathF.Sqrt(dy * dy + dp * dp);
                    if (float.IsFinite(d) && d < 720f) turn[b] += d;
                }
            }
            prevT = f.T; prevYaw = f.Yaw; prevPitch = f.Pitch;
        }

        foreach (var s in t.Shots)
        {
            if (!float.IsFinite(s.T) || s.T < 0) continue;
            int b = B(s.T);
            shots[b]++;
            if (s.Hit) { hits[b]++; if (s.Zone == 0) heads[b]++; }
            if (float.IsFinite(s.Speed) && s.Speed > 1.35f) movingShots[b]++;
            if (float.IsFinite(s.Err)) shotErr[b] += s.Err;
        }

        foreach (var e in t.Events)
        {
            if (!float.IsFinite(e.T) || e.T < 0) continue;
            int b = B(e.T);
            events[b]++;
            switch (e.Kind)
            {
                case "kill": kills[b]++; break;
                case "died": deaths[b]++; break;
                case "target_spawn": spawns[b]++; break;
                case "reaction": reactions[b]++; break;
                case "stop": stops[b]++; break;
                case "flash": flashes[b]++; break;
                case "spray_end": sprays[b]++; break;
            }
        }

        if (plays != null)
        {
            foreach (var p in plays.Plays)
            {
                if (!float.IsFinite(p.T0) || !float.IsFinite(p.T1) || p.T1 < p.T0) continue;
                int b0 = B(MathF.Max(0, p.T0)), b1 = B(MathF.Max(0, p.T1));
                for (int b = b0; b <= b1; b++) playCount[b]++;
            }
        }

        for (int i = 0; i < n; i++)
        {
            var s = new TimelineSecond
            {
                Index = i,
                T = i * Bucket,
                Frames = frames[i],
                FocusFrames = focus[i],
                OnTargetFrames = onTarget[i],
                Shots = shots[i],
                Hits = hits[i],
                Headshots = heads[i],
                MovingShots = movingShots[i],
                FiringFrames = firing[i],
                AdsFrames = ads[i],
                BlindFrames = blind[i],
                AirborneFrames = airborne[i],
                CrouchFrames = crouch[i],
                Events = events[i],
                Kills = kills[i],
                Deaths = deaths[i],
                Spawns = spawns[i],
                Reactions = reactions[i],
                Stops = stops[i],
                Flashes = flashes[i],
                Sprays = sprays[i],
                Plays = playCount[i],
                SpeedAvg = frames[i] > 0 ? (float)(speedSum[i] / frames[i]) : 0,
                SpeedMax = speedMax[i],
                MouseCounts = (float)mouse[i],
                TurnDeg = (float)turn[i],
                Accuracy = shots[i] > 0 ? (float)hits[i] / shots[i] : 0,
                AimErrR = focus[i] > 0 ? (float)(errR[i] / focus[i]) : 0,
                AimErrDeg = focus[i] > 0 ? (float)(errDeg[i] / focus[i]) : 0,
                OnTargetPct = focus[i] > 0 ? (float)onTarget[i] / focus[i] : 0,
                ShotErrDeg = shots[i] > 0 ? (float)(shotErr[i] / shots[i]) : 0,
                BiasYaw = focus[i] > 0 ? (float)(biasYaw[i] / focus[i]) : 0,
                BiasPitch = focus[i] > 0 ? (float)(biasPitch[i] / focus[i]) : 0,
            };
            s.Score = ScoreOf(s);
            s.Tag = TagOf(s);
            Seconds.Add(s);
        }

        Summarize();
        BuildFindings();
    }

    static int ScoreOf(TimelineSecond s)
    {
        if (s.Frames == 0 && s.Shots == 0 && s.Events == 0) return 0;
        float score;
        if (s.FocusFrames > 0)
        {
            float on = MathfClamp01(s.OnTargetPct);
            float err = MathF.Min(1.5f, MathF.Max(0, s.AimErrR));
            score = 30f + 55f * on + 15f * (1f - err / 1.5f);
        }
        else if (s.Shots > 0)
        {
            score = 55f + 45f * MathfClamp01(s.Accuracy);
        }
        else if (s.SpeedAvg > 0.8f) score = 55f;
        else if (s.Events > 0) score = 58f;
        else score = 42f;

        if (s.Shots > 0)
        {
            score = score * 0.65f + (25f + 70f * MathfClamp01(s.Accuracy)) * 0.35f;
            score -= 20f * ((float)s.MovingShots / s.Shots);
            if (s.Hits == 0) score -= 10f;
        }
        if (s.Frames > 0 && s.BlindFrames > s.Frames / 2) score -= 5f;
        if (s.Dead) score = MathF.Min(score, 35f);
        return Math.Clamp((int)MathF.Round(score), 0, 100);
    }

    static string TagOf(TimelineSecond s)
    {
        if (s.Dead) return "dead";
        if (s.Frames > 0 && s.BlindFrames > s.Frames / 2) return "blind";
        if (s.Shots > 0 && (float)s.MovingShots / s.Shots >= 0.25f) return "rushed";
        if (s.Shots > 0 && s.Accuracy >= 0.8f) return "clean";
        if (s.FocusFrames > 0 && s.OnTargetPct >= 0.7f) return "locked";
        if (s.FocusFrames > 0 && s.AimErrR > 1.3f) return "sloppy";
        if (s.Shots > 0 && s.Accuracy < 0.4f) return "misses";
        if (s.SpeedAvg > 2f) return "moving";
        if (s.FocusFrames == 0 && s.Shots == 0) return "idle";
        return "steady";
    }

    void Summarize()
    {
        var sum = new TimelineSummary { Seconds = Seconds.Count };
        var scored = Seconds.Where(s => s.Frames > 0 || s.Shots > 0 || s.Events > 0).ToList();
        var active = Seconds.Where(s => s.Frames > 0 && (s.FocusFrames > 0 || s.Shots > 0 || s.Events > 0)).ToList();
        var aim = active.Where(s => s.FocusFrames > 0 || s.Shots > 0).ToList();
        sum.ScoredSeconds = scored.Count;
        sum.ActiveSeconds = active.Count;
        sum.AimSeconds = aim.Count;
        sum.DeadSeconds = Seconds.Count(s => s.Dead);
        sum.BlindSeconds = Seconds.Count(s => s.Frames > 0 && s.BlindFrames > s.Frames / 2);
        sum.Kills = Seconds.Sum(s => s.Kills);
        sum.Deaths = Seconds.Sum(s => s.Deaths);
        sum.Shots = Seconds.Sum(s => s.Shots);
        sum.Hits = Seconds.Sum(s => s.Hits);
        sum.Headshots = Seconds.Sum(s => s.Headshots);
        sum.MovingShots = Seconds.Sum(s => s.MovingShots);

        int focusFrames = Seconds.Sum(s => s.FocusFrames);
        int onTargetFrames = Seconds.Sum(s => s.OnTargetFrames);
        int frames = Seconds.Sum(s => s.Frames);
        sum.Accuracy = sum.Shots > 0 ? (float)sum.Hits / sum.Shots : 0;
        sum.HeadshotPct = sum.Hits > 0 ? (float)sum.Headshots / sum.Hits : 0;
        sum.MovingShotPct = sum.Shots > 0 ? (float)sum.MovingShots / sum.Shots : 0;
        sum.AimErrR = focusFrames > 0 ? Seconds.Sum(s => s.AimErrR * s.FocusFrames) / focusFrames : 0;
        sum.AimErrDeg = focusFrames > 0 ? Seconds.Sum(s => s.AimErrDeg * s.FocusFrames) / focusFrames : 0;
        sum.OnTargetPct = focusFrames > 0 ? (float)onTargetFrames / focusFrames : 0;
        sum.BiasYaw = focusFrames > 0 ? Seconds.Sum(s => s.BiasYaw * s.FocusFrames) / focusFrames : 0;
        sum.BiasPitch = focusFrames > 0 ? Seconds.Sum(s => s.BiasPitch * s.FocusFrames) / focusFrames : 0;
        sum.DeadPct = scored.Count > 0 ? (float)sum.DeadSeconds / scored.Count : 0;
        sum.Average = scored.Count > 0 ? scored.Average(s => (float)s.Score) : 0;
        sum.Consistency = StdDev(active.Select(s => (float)s.Score));
        sum.ShotsPerMinute = Duration > 0 ? sum.Shots * 60f / Duration : 0;
        sum.HitsPerMinute = Duration > 0 ? sum.Hits * 60f / Duration : 0;
        sum.MousePerSecond = Duration > 0 ? (float)(Seconds.Sum(s => s.MouseCounts) / Duration) : 0;
        sum.AvgSpeed = frames > 0 ? Seconds.Sum(s => s.SpeedAvg * s.Frames) / frames : 0;
        sum.MovingPct = frames > 0 ? (float)Seconds.Count(s => s.SpeedAvg > 0.8f) / Seconds.Count(s => s.Frames > 0) : 0;

        if (aim.Count > 0)
        {
            var best = aim.OrderByDescending(s => s.Score).ThenBy(s => s.Index).First();
            var worst = aim.OrderBy(s => s.Score).ThenBy(s => s.Index).First();
            sum.BestSecond = best.Index; sum.BestScore = best.Score;
            sum.WorstSecond = worst.Index; sum.WorstScore = worst.Score;
        }

        var thirds = active.Count >= 6 ? active : aim;
        if (thirds.Count >= 3)
        {
            int third = Math.Max(1, thirds.Count / 3);
            sum.FirstThird = thirds.Take(third).Average(s => (float)s.Score);
            sum.LastThird = thirds.Skip(thirds.Count - third).Average(s => (float)s.Score);
            sum.Trend = sum.LastThird - sum.FirstThird;
        }
        else
        {
            sum.FirstThird = sum.LastThird = sum.Average;
        }

        (sum.BestWindow, sum.BestWindowScore) = BestWindow(active, 5, highest: true);
        (sum.WorstWindow, sum.WorstWindowScore) = BestWindow(active, 5, highest: false);
        Summary = sum;
    }

    static (int Start, float Score) BestWindow(List<TimelineSecond> active, int width, bool highest)
    {
        if (active.Count < width) return (-1, 0);
        var byTime = active.OrderBy(s => s.Index).ToList();
        int start = byTime[0].Index;
        float best = highest ? float.NegativeInfinity : float.PositiveInfinity;
        for (int i = 0; i + width <= byTime.Count; i++)
        {
            var win = byTime.Skip(i).Take(width).ToList();
            if (win[^1].Index - win[0].Index >= width + 1) continue; // a gap in the middle is not a window
            float avg = win.Average(s => (float)s.Score);
            if (highest ? avg > best : avg < best) { best = avg; start = win[0].Index; }
        }
        return best is float.NegativeInfinity or float.PositiveInfinity ? (-1, 0) : (start, best);
    }

    void BuildFindings()
    {
        var s = Summary;
        void Add(string code, TimelineTone tone, float a = 0, float b = 0, int n = 0, float t0 = 0, float t1 = 0, int second = -1)
            => Findings.Add(new TimelineFinding { Code = code, Tone = tone, A = a, B = b, N = n, T0 = t0, T1 = t1, Second = second });

        if (s.ActiveSeconds < 3 && s.Shots == 0 && s.Kills == 0 && s.Deaths == 0)
        {
            Add("no_data", TimelineTone.Neutral, n: s.ActiveSeconds);
            return;
        }

        // Fatigue / warm-up: the same player, compared with themselves.
        if (s.ActiveSeconds >= 12)
        {
            if (s.FirstThird - s.LastThird >= 8)
                Add("fatigue", s.FirstThird - s.LastThird >= 16 ? TimelineTone.Bad : TimelineTone.Warn, s.FirstThird, s.LastThird, s.ActiveSeconds);
            else if (s.LastThird - s.FirstThird >= 8)
                Add("climb", TimelineTone.Good, s.FirstThird, s.LastThird, s.ActiveSeconds);
            else if (s.Consistency <= 8)
                Add("steady", TimelineTone.Good, s.Consistency, 0, s.ActiveSeconds);
            else if (s.Consistency >= 18)
                Add("volatile", TimelineTone.Warn, s.Consistency, 0, s.ActiveSeconds);
        }

        if (s.ScoredSeconds >= 10 && s.DeadPct >= 0.2f && s.DeadSeconds >= 5)
            Add("dead", s.DeadPct >= 0.35f ? TimelineTone.Bad : TimelineTone.Warn, s.DeadPct, s.DeadSeconds, s.DeadSeconds);

        if (s.Shots >= 8 && s.MovingShotPct >= 0.15f)
            Add("rushed", s.MovingShotPct >= 0.3f ? TimelineTone.Bad : TimelineTone.Warn, s.MovingShotPct, s.MovingShots, s.Shots);

        if (s.Shots >= 10 && s.Accuracy < 0.35f)
            Add("accuracy_low", s.Accuracy < 0.22f ? TimelineTone.Bad : TimelineTone.Warn, s.Accuracy, s.Shots, s.Shots);
        else if (s.Shots >= 10 && s.Accuracy >= 0.65f)
            Add("accuracy_high", TimelineTone.Good, s.Accuracy, s.Shots, s.Shots);

        int focusFrames = Seconds.Sum(x => x.FocusFrames);
        if (focusFrames >= 120)
        {
            float yaw = MathF.Abs(s.BiasYaw), pitch = MathF.Abs(s.BiasPitch);
            if (yaw >= 0.25f && yaw >= pitch)
                Add(s.BiasYaw > 0 ? "bias_left" : "bias_right", yaw >= 0.5f ? TimelineTone.Warn : TimelineTone.Neutral, yaw, s.BiasYaw, focusFrames);
            else if (pitch >= 0.25f)
                Add(s.BiasPitch > 0 ? "bias_low" : "bias_high", pitch >= 0.5f ? TimelineTone.Warn : TimelineTone.Neutral, pitch, s.BiasPitch, focusFrames);
        }

        if (s.ActiveSeconds >= 15 && s.Average - s.FirstThird >= 6)
            Add("slow_start", TimelineTone.Warn, s.FirstThird, s.Average, s.ActiveSeconds);

        if (s.BlindSeconds >= 3 && s.ScoredSeconds > 0 && (float)s.BlindSeconds / s.ScoredSeconds >= 0.05f)
            Add("blind", TimelineTone.Warn, (float)s.BlindSeconds / s.ScoredSeconds, s.BlindSeconds, s.BlindSeconds);

        if (s.BestSecond >= 0)
            Add("best", TimelineTone.Good, s.BestScore, 0, 1, s.BestSecond, s.BestSecond + 1, s.BestSecond);
        if (s.WorstSecond >= 0)
            Add("worst", TimelineTone.Warn, s.WorstScore, 0, 1, s.WorstSecond, s.WorstSecond + 1, s.WorstSecond);
        if (s.BestWindow >= 0)
            Add("best_window", TimelineTone.Good, s.BestWindowScore, 0, 5, s.BestWindow, s.BestWindow + 5, s.BestWindow);
        if (s.WorstWindow >= 0)
            Add("worst_window", TimelineTone.Warn, s.WorstWindowScore, 0, 5, s.WorstWindow, s.WorstWindow + 5, s.WorstWindow);
    }

    static float StdDev(IEnumerable<float> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return 0;
        float mean = list.Average();
        double v = 0;
        foreach (var x in list) v += (x - mean) * (x - mean);
        return MathF.Sqrt((float)(v / list.Count));
    }

    static float MathfClamp01(float v) => float.IsNaN(v) ? 0 : Math.Clamp(v, 0f, 1f);
}

/// <summary>Spanish/English text for timeline codes. The stored data never contains prose, only stable codes and numbers.</summary>
public static class TimelineText
{
    static bool Es => Loc.Spanish;
    static string S(string en, string es) => Es ? es : en;

    static string N(float v, string fmt = "0")
    {
        string s = v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
        if (!Es) return s;
        var a = s.ToCharArray();
        for (int i = 1; i + 1 < a.Length; i++)
            if (a[i] == '.' && char.IsDigit(a[i - 1]) && char.IsDigit(a[i + 1])) a[i] = ',';
        return new string(a);
    }

    static string P(float v) => N(v * 100f);
    public static string Clock(float t)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, t));
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)
                                  : ts.ToString(@"m\:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string Title(TimelineFinding f) => f.Code switch
    {
        "fatigue" => S("You fade late", "Bajas al final"),
        "climb" => S("You finish stronger", "Acabas más fuerte"),
        "steady" => S("Steady level", "Nivel estable"),
        "volatile" => S("Inconsistent level", "Nivel irregular"),
        "dead" => S("Dead time", "Tiempo muerto"),
        "rushed" => S("Shots while moving", "Disparos en movimiento"),
        "accuracy_low" => S("Low accuracy", "Precisión baja"),
        "accuracy_high" => S("High accuracy", "Precisión alta"),
        "bias_left" => S("Crosshair left of the target", "Mira a la izquierda del objetivo"),
        "bias_right" => S("Crosshair right of the target", "Mira a la derecha del objetivo"),
        "bias_low" => S("Crosshair below the target", "Mira por debajo del objetivo"),
        "bias_high" => S("Crosshair above the target", "Mira por encima del objetivo"),
        "slow_start" => S("Slow start", "Arranque lento"),
        "blind" => S("Time blinded", "Tiempo cegado"),
        "best" => S("Best second", "Mejor segundo"),
        "worst" => S("Worst second", "Peor segundo"),
        "best_window" => S("Best window", "Mejor ventana"),
        "worst_window" => S("Worst window", "Peor ventana"),
        "no_data" => S("Not enough data", "Sin datos suficientes"),
        _ => f.Code,
    };

    public static string Detail(TimelineFinding f) => f.Code switch
    {
        "fatigue" => S($"First third {N(f.A)}/100, last third {N(f.B)}/100. Your hand or your focus drops as the run goes on.",
                       $"Primer tercio {N(f.A)}/100, último tercio {N(f.B)}/100. La mano o la concentración bajan según avanza la partida."),
        "climb" => S($"First third {N(f.A)}/100, last third {N(f.B)}/100. You warm up and finish at your real level.",
                     $"Primer tercio {N(f.A)}/100, último tercio {N(f.B)}/100. Entras en calor y terminas a tu nivel real."),
        "steady" => S($"Your per-second score moves only ±{N(f.A, "0.0")} points. That consistency is a platform to build on.",
                      $"Tu puntuación por segundo varía solo ±{N(f.A, "0.0")} puntos. Esa constancia es una base sobre la que construir."),
        "volatile" => S($"Your per-second score swings ±{N(f.A, "0.0")} points. Find one routine and repeat it instead of changing speed every play.",
                        $"Tu puntuación por segundo oscila ±{N(f.A, "0.0")} puntos. Encuentra una rutina y repítela en vez de cambiar de velocidad en cada jugada."),
        "dead" => S($"{P(f.A)}% of the run ({N(f.B)} s) had no target, no shot and almost no movement. Use that time to reposition and pre-aim.",
                    $"{P(f.A)}% de la partida ({N(f.B)} s) estuvo sin objetivo, sin disparo y casi sin movimiento. Usa ese tiempo para reposicionarte y pre-apuntar."),
        "rushed" => S($"{P(f.A)}% of your shots ({N(f.B)} of {N(f.N)}) left while you were moving. Stop first: the first bullet is only accurate at zero speed.",
                      $"{P(f.A)}% de tus disparos ({N(f.B)} de {N(f.N)}) salieron en movimiento. Frena primero: la primera bala solo es precisa a velocidad cero."),
        "accuracy_low" => S($"{P(f.A)}% accuracy over {N(f.N)} shots. Slow the first movement down and fire only when the dot is inside the target.",
                            $"{P(f.A)}% de acierto en {N(f.N)} disparos. Baja la velocidad del primer movimiento y dispara solo cuando el punto esté dentro del objetivo."),
        "accuracy_high" => S($"{P(f.A)}% accuracy over {N(f.N)} shots. Keep this trigger discipline and raise the speed, not the volume.",
                             $"{P(f.A)}% de acierto en {N(f.N)} disparos. Mantén esa disciplina de disparo y sube la velocidad, no el volumen."),
        "bias_left" or "bias_right" => S($"Your crosshair sat {P(f.A)}% of a target radius to the {(f.B > 0 ? "left" : "right")} of the target. Correct towards the centre before you click.",
                                          $"Tu mira estaba un {P(f.A)}% del radio del objetivo a la {(f.B > 0 ? "izquierda" : "derecha")}. Corrige hacia el centro antes de hacer clic."),
        "bias_low" or "bias_high" => S($"Your crosshair sat {P(f.A)}% of a target radius {(f.B > 0 ? "below" : "above")} the target. Hold head height and let the target come to the dot.",
                                        $"Tu mira estaba un {P(f.A)}% del radio del objetivo {(f.B > 0 ? "por debajo" : "por encima")}. Mantén la altura de la cabeza y deja que el objetivo llegue al punto."),
        "slow_start" => S($"The first third averaged {N(f.A)}/100 against {N(f.B)}/100 overall. Give yourself two easy seconds before the first real duel.",
                          $"El primer tercio promedió {N(f.A)}/100 frente a {N(f.B)}/100 del total. Date dos segundos fáciles antes del primer duelo real."),
        "blind" => S($"You spent {N(f.B)} s ({P(f.A)}% of the run) blinded. Turn on the sound cue, not on the white screen.",
                     $"Pasaste {N(f.B)} s ({P(f.A)}% de la partida) cegado. Gira al sonido, no a la pantalla blanca."),
        "best" => S($"Second {Clock(f.T0)} scored {N(f.A)}/100. Rewatch it: that is the tempo to repeat.",
                    $"El segundo {Clock(f.T0)} puntuó {N(f.A)}/100. Rebobínalo: ese es el tempo que hay que repetir."),
        "worst" => S($"Second {Clock(f.T0)} scored {N(f.A)}/100. Find the cause in the rows around it.",
                     $"El segundo {Clock(f.T0)} puntuó {N(f.A)}/100. Busca la causa en las filas de alrededor."),
        "best_window" => S($"From {Clock(f.T0)} to {Clock(f.T1)} you averaged {N(f.A)}/100: your best five-second stretch.",
                           $"De {Clock(f.T0)} a {Clock(f.T1)} promediaste {N(f.A)}/100: tu mejor tramo de cinco segundos."),
        "worst_window" => S($"From {Clock(f.T0)} to {Clock(f.T1)} you averaged {N(f.A)}/100: your weakest five-second stretch.",
                            $"De {Clock(f.T0)} a {Clock(f.T1)} promediaste {N(f.A)}/100: tu tramo más flojo de cinco segundos."),
        "no_data" => S("The run is too short or the drill doesn't record frames, so there is no second-by-second read yet.",
                       "La partida es demasiado corta o el ejercicio no graba frames, así que aún no hay lectura segundo a segundo."),
        _ => "",
    };

    public static string TagName(string tag) => tag switch
    {
        "dead" => S("dead time", "tiempo muerto"),
        "blind" => S("blinded", "cegado"),
        "rushed" => S("moving shot", "disparo en movimiento"),
        "clean" => S("clean shots", "disparos limpios"),
        "locked" => S("locked on", "mira fijada"),
        "sloppy" => S("off target", "fuera del objetivo"),
        "misses" => S("missing", "fallando"),
        "moving" => S("rotating", "rotando"),
        "idle" => S("idle", "quieto"),
        _ => S("steady", "estable"),
    };
}
