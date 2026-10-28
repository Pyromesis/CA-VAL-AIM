using System.Globalization;
using Godot;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.UI;

namespace ValTrainer.Modes;

/// <summary>The reaction-time categories the Reaction Lab measures (each has its own history and statistics).</summary>
public enum ReactionCategory { Visual, Audio, Peripheral, Choice, Flick }

/// <summary>
/// Reaction Lab: reaction time by category, measured precisely and kept per category.
/// <list type="bullet">
/// <item><b>Visual</b>: the screen flashes in your enemy colour; click. Simple reaction time.</item>
/// <item><b>Audio</b>: the screen stays still; a sharp click sounds; click. Hearing usually beats sight by 30–50 ms.</item>
/// <item><b>Peripheral</b>: keep your eyes on the centre; a small dot appears near the edge of the screen; click.</item>
/// <item><b>Choice</b> (go / no-go): green means click, red means don't. Reaction time on green, false alarms on red.</item>
/// <item><b>Flick</b>: a target appears 5–25° away; move and click it. Movement onset (when your hand starts) and the
/// time to the hit are measured separately: onset is reaction, the rest is aim.</item>
/// </list>
/// A random 1.2–3.8 s wait (foreperiod) before each stimulus prevents anticipation; clicking before it counts as an early
/// click and the trial is repeated. Clicks and mouse movement are timestamped when the input event arrives (not at the
/// next frame); the stimulus time is the frame it is drawn on, so display latency is included, as in every on-screen test.
/// Per-run statistics (median, mean, best, standard deviation, early clicks, false alarms) go into the run record and
/// build the per-category history on the Reaction profile (<see cref="ReactionStats"/>).
/// </summary>
public sealed class ReactionLabMode : TrainingMode
{
    readonly ReactionCategory cat;
    /// <summary>Visual trials are the coach's "reaction" events (same as the Reaction Test); the other categories have
    /// their own event so the coach's simple-reaction norms aren't applied to them.</summary>
    string EventName => cat == ReactionCategory.Visual ? "reaction" : "reaction_" + cat.ToString().ToLowerInvariant();

    public ReactionLabMode(string arg) => cat = ReactionStats.Parse(arg);
    public ReactionLabMode(ReactionCategory c) => cat = c;

    public ReactionCategory Cat => cat;
    public override string Key => ReactionStats.KeyOf(cat);
    public override string Name => Loc.Spanish ? $"Laboratorio de reacción: {ReactionStats.Name(cat)}" : $"Reaction Lab: {ReactionStats.Name(cat)}";
    public override string Description => ReactionStats.Blurb(cat);
    public override string Category => "Other";
    public override bool Timed => false;
    public override bool Is2D => true;
    public override bool PreviewDemo => false;
    public override bool HasPreview => false;
    public override WeaponKind Weapon => WeaponKind.None;
    public override bool Done => rts.Count >= Trials && (cat != ReactionCategory.Choice || noGoTrials >= NoGoTrials);

    const int Trials = 12, NoGoTrials = 5;
    enum St { Wait, Stim, Show, Early, Rest }
    St st;
    float t, foreperiod, stimShownAt;
    ulong stimUsec, lastClickSeen;
    bool stimDrawn, noGo;
    Vector2 stimPos;          // peripheral dot (0..1 of the screen) / flick target (degrees from the crosshair)
    float targetRadiusDeg = 1.6f;
    readonly List<float> rts = new(), onsets = new(), hitsMs = new();
    int early, falseAlarms, noGoTrials, flickMisses;
    float lastMs;
    string lastText = "";
    GameSession? S => G as GameSession;

    protected override void Setup() => Next();

    void Next()
    {
        st = St.Wait;
        t = 0;
        foreperiod = R(1.2f, 3.8f);
        stimDrawn = false;
        lastClickSeen = S?.LastFirePressUsec ?? 0;
        noGo = cat == ReactionCategory.Choice && (noGoTrials < NoGoTrials && (rts.Count >= Trials || Rng.NextDouble() < 0.33));
        if (cat == ReactionCategory.Peripheral)
        {
            float a = R(0, Mathf.Tau);
            stimPos = new Vector2(0.5f + 0.42f * Mathf.Cos(a), 0.5f + 0.40f * Mathf.Sin(a));
        }
        else if (cat == ReactionCategory.Flick)
        {
            float a = R(0, Mathf.Tau), d = R(5f, 25f);
            stimPos = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.6f);
            yaw0 = G.View.ViewYaw; pitch0 = G.View.ViewPitch;
        }
    }

    float yaw0, pitch0;

    /// <summary>The flick target's offset from the crosshair right now (degrees, +x right, +y down).</summary>
    Vector2 FlickOffset() => new(stimPos.X - Mathf.Wrap(G.View.ViewYaw - yaw0, -180f, 180f), stimPos.Y + (G.View.ViewPitch - pitch0));

    public override void Update(float dt)
    {
        t += dt;
        ulong click = S?.LastFirePressUsec ?? 0;
        bool newClick = click != 0 && click != lastClickSeen;
        switch (st)
        {
            case St.Wait:
                if (newClick && cat != ReactionCategory.Flick)
                {
                    lastClickSeen = click;
                    early++;
                    st = St.Early; t = 0;
                    G.Sound("fail");
                    Event("early_click");
                    return;
                }
                if (t >= foreperiod)
                {
                    st = St.Stim; t = 0;
                    stimUsec = Time.GetTicksUsec(); // replaced by the draw time of the first frame showing it (Draw2D)
                    stimDrawn = cat == ReactionCategory.Audio;
                    if (cat == ReactionCategory.Audio) G.Sound("tick", 1f, 1f);
                    if (cat == ReactionCategory.Flick) { yaw0 = G.View.ViewYaw; pitch0 = G.View.ViewPitch; }
                    lastClickSeen = click;
                }
                break;
            case St.Stim:
                if (cat == ReactionCategory.Choice && noGo)
                {
                    if (newClick) { lastClickSeen = click; falseAlarms++; noGoTrials++; Finish(Loc.Spanish ? "¡Rojo: no hagas clic!" : "Red: don't click!", false); G.Sound("fail"); Event("false_alarm"); }
                    else if (t > 1.0f) { noGoTrials++; Finish(Loc.Spanish ? "Aguantaste: correcto" : "Held it: correct", true); }
                    break;
                }
                if (cat == ReactionCategory.Flick)
                {
                    if (newClick)
                    {
                        lastClickSeen = click;
                        if (FlickOffset().Length() <= targetRadiusDeg)
                        {
                            float hit = Mathf.Max(0f, ((long)click - (long)stimUsec) / 1000f);
                            ulong mv = S?.FirstMoveAfter(stimUsec, 6f) ?? 0;
                            float onset = mv > stimUsec ? (mv - stimUsec) / 1000f : hit;
                            rts.Add(onset); onsets.Add(onset); hitsMs.Add(hit);
                            Hits++; Shots++;
                            Event(EventName, onset, hit);
                            G.Sound("head", 0.6f);
                            Finish(Loc.Spanish ? $"{onset:0} ms inicio · {hit:0} ms acierto" : $"{onset:0} ms onset · {hit:0} ms hit", true);
                        }
                        else Shots++;
                    }
                    else if (t > 2.5f) { flickMisses++; Finish(Loc.Spanish ? "Demasiado lento" : "Too slow", false); G.Sound("fail"); }
                    break;
                }
                if (newClick && stimDrawn)
                {
                    lastClickSeen = click;
                    lastMs = ((long)click - (long)stimUsec) / 1000f; // a click between the update and the first draw is negative: early
                    if (lastMs < 90f) { early++; Finish(Loc.Spanish ? "Anticipado (< 90 ms)" : "Anticipated (< 90 ms)", false); Event("early_click"); break; } // faster than a human: a guess
                    rts.Add(lastMs);
                    KillTimes.Add(lastMs);
                    Hits++; Shots++;
                    Event(EventName, lastMs);
                    G.Sound("body");
                    Finish($"{lastMs:0} ms", true);
                }
                else if (t > 2.0f) { Finish(Loc.Spanish ? "Fallado" : "Missed", false); }
                break;
            case St.Show:
            case St.Early:
                if (t > 1.0f && !Done) Next();
                break;
        }
        Score = rts.Count == 0 ? 0 : Math.Max(0, (int)(1000 - Median(rts)) - early * 20 - falseAlarms * 40);
    }

    void Finish(string text, bool good)
    {
        lastText = text;
        st = St.Show; t = 0;
        if (!good) Event("trial_fail");
    }

    public override float OnBullet(Vector3 o, Vector3 d, bool accurate) => float.PositiveInfinity; // clicks are read by timestamp

    public override float Accuracy => rts.Count + early + falseAlarms == 0 ? 0 : (float)rts.Count / (rts.Count + early + falseAlarms + flickMisses);

    // ---------------- results / stats ----------------

    static float Sd(List<float> v)
    {
        if (v.Count < 2) return 0;
        float m = v.Average();
        return Mathf.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Count - 1));
    }

    public override IEnumerable<(string Key, float Value)> ExtraMetrics()
    {
        if (rts.Count == 0) yield break;
        yield return ("rt.median", Median(rts));
        yield return ("rt.mean", rts.Average());
        yield return ("rt.best", rts.Min());
        yield return ("rt.sd", Sd(rts));
        yield return ("rt.trials", rts.Count);
        yield return ("rt.early", early);
        if (cat == ReactionCategory.Choice) yield return ("rt.false", falseAlarms);
        if (cat == ReactionCategory.Flick && hitsMs.Count > 0) { yield return ("rt.hit", Median(hitsMs)); yield return ("rt.miss", flickMisses); }
    }

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        bool es = Loc.Spanish;
        if (rts.Count == 0) { yield return (es ? "Intentos" : "Trials", es ? "ninguno completado" : "none completed"); yield break; }
        string label = cat == ReactionCategory.Flick
            ? (es ? "Inicio medio del movimiento" : "Median movement onset")
            : (es ? "Reacción media" : "Median reaction");
        yield return (label, es
            ? $"{Median(rts):0} ms  (media {rts.Average():0}, mejor {rts.Min():0})"
            : $"{Median(rts):0} ms  (mean {rts.Average():0}, best {rts.Min():0})");
        yield return (es ? "Consistencia (DE)" : "Consistency (SD)", $"{Sd(rts):0} ms");
        if (cat == ReactionCategory.Flick && hitsMs.Count > 0)
            yield return (es ? "Tiempo medio hasta acertar" : "Median time to hit", es ? $"{Median(hitsMs):0} ms  ({flickMisses} demasiado lentos)" : $"{Median(hitsMs):0} ms  ({flickMisses} too slow)");
        if (cat == ReactionCategory.Choice) yield return (es ? "Falsas alarmas en rojo" : "False alarms on red", $"{falseAlarms} / {NoGoTrials}");
        yield return (es ? "Clics anticipados" : "Early clicks", early.ToString());
        yield return (es ? "Rango de referencia" : "Rank reference", ReactionStats.RankOf(cat, Median(rts)));
    }

    public override int Badge() => rts.Count < 5 ? -1 : Difficulty.BadgeLower(Median(rts), ReactionStats.Reference(cat));

    // ---------------- drawing ----------------

    public override void Draw2D(CanvasItem c, Vector2 size)
    {
        float k = size.Y / 1080f;
        var bg = new Color(0.035f, 0.045f, 0.06f);
        var text = UiTheme.Text;
        bool stim = st == St.Stim;
        if (stim && !stimDrawn) { stimDrawn = true; stimUsec = Time.GetTicksUsec(); } // the first frame that shows it
        switch (cat)
        {
            case ReactionCategory.Visual when stim:
                bg = G.Enemy; text = Colors.Black; break;
            case ReactionCategory.Choice when stim:
                break;
        }
        if (st == St.Early) bg = new Color(0.20f, 0.08f, 0.09f);
        c.DrawRect(new Rect2(Vector2.Zero, size), bg);
        var center = size / 2;

        // stimulus
        if (stim)
        {
            switch (cat)
            {
                case ReactionCategory.Peripheral:
                    c.DrawCircle(new Vector2(stimPos.X * size.X, stimPos.Y * size.Y), 16 * k, G.Enemy);
                    break;
                case ReactionCategory.Choice:
                    c.DrawCircle(center, 120 * k, noGo ? new Color(0.92f, 0.22f, 0.25f) : new Color(0.25f, 0.88f, 0.52f));
                    break;
                case ReactionCategory.Flick:
                {
                    float pxPerDeg = size.Y / 70f; // roughly the vertical FOV mapped to the screen
                    var off = FlickOffset();
                    var p = center + off * pxPerDeg;
                    c.DrawCircle(p, targetRadiusDeg * pxPerDeg, G.Enemy);
                    c.DrawCircle(p, targetRadiusDeg * pxPerDeg * 0.35f, new Color(1, 1, 1, 0.9f));
                    break;
                }
            }
        }
        // fixation cross (peripheral / flick / choice): where the eyes rest
        if (cat is ReactionCategory.Peripheral or ReactionCategory.Flick or ReactionCategory.Choice && !(stim && cat == ReactionCategory.Choice))
        {
            var col = new Color(1, 1, 1, 0.85f);
            c.DrawLine(center - new Vector2(10 * k, 0), center + new Vector2(10 * k, 0), col, 2 * k);
            c.DrawLine(center - new Vector2(0, 10 * k), center + new Vector2(0, 10 * k), col, 2 * k);
        }

        string big = st switch
        {
            St.Wait => cat switch
            {
                ReactionCategory.Audio => "Listen…",
                ReactionCategory.Peripheral => "Eyes on the centre",
                ReactionCategory.Choice => "Green = click · Red = wait",
                ReactionCategory.Flick => "Get ready to flick",
                _ => "Wait for it…",
            },
            St.Stim => cat == ReactionCategory.Visual ? "CLICK!" : "",
            St.Early => "Too early!",
            _ => lastText,
        };
        if (big.Length > 0)
        {
            int fs = UiTheme.Fs(st == St.Stim ? 96 : 54, k);
            float y = cat is ReactionCategory.Choice or ReactionCategory.Peripheral or ReactionCategory.Flick ? size.Y * 0.80f : size.Y / 2 + 20 * k;
            Gfx.TextC(c, UiTheme.Display, Loc.T(big), center.X, y, fs, text);
        }
        int done = rts.Count + (cat == ReactionCategory.Choice ? noGoTrials : 0);
        int total = Trials + (cat == ReactionCategory.Choice ? NoGoTrials : 0);
        Gfx.TextC(c, UiTheme.HudWide, $"{ReactionStats.Name(cat).ToUpperInvariant()}  ·  {Math.Min(done + 1, total)} / {total}", center.X, size.Y - 90 * k, UiTheme.Fs(18, k), new Color(text, 0.65f));
        if (rts.Count > 0)
            Gfx.TextC(c, UiTheme.HudWide, Loc.Spanish
            ? $"MEDIA {Median(rts):0} MS  ·  MEJOR {rts.Min():0} MS"
            : $"MEDIAN {Median(rts):0} MS  ·  BEST {rts.Min():0} MS", center.X, size.Y - 60 * k, UiTheme.Fs(15, k), new Color(text, 0.5f));
    }
}

/// <summary>
/// Reaction-time history and statistics per category, from the saved runs (<see cref="RunRecord.Metrics"/> "rt.*"; the
/// older Reaction Test counts as Visual). References are typical human values per rank band: simple visual reaction
/// (the existing Reaction Test's thresholds), audio ≈ 40 ms faster, peripheral ≈ 35 ms slower, choice ≈ 90 ms slower and
/// flick onset ≈ 15 ms slower than simple visual (reaction-time literature: Hick's law, auditory vs visual RT,
/// eccentricity). They are guides, not Riot data.
/// </summary>
public static class ReactionStats
{
    public const string Base = "reactlab";
    public static readonly ReactionCategory[] All = { ReactionCategory.Visual, ReactionCategory.Audio, ReactionCategory.Peripheral, ReactionCategory.Choice, ReactionCategory.Flick };

    public static ReactionCategory Parse(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "audio" or "sound" => ReactionCategory.Audio,
        "peripheral" or "edge" => ReactionCategory.Peripheral,
        "choice" or "gonogo" => ReactionCategory.Choice,
        "flick" => ReactionCategory.Flick,
        _ => ReactionCategory.Visual,
    };

    public static string KeyOf(ReactionCategory c) => $"{Base}:{c.ToString().ToLowerInvariant()}";

    public static string Name(ReactionCategory c) => Loc.Spanish
        ? c switch
        {
            ReactionCategory.Visual => "Visual", ReactionCategory.Audio => "Oído", ReactionCategory.Peripheral => "Periférica",
            ReactionCategory.Choice => "Elección", _ => "Flick",
        }
        : c switch
        {
            ReactionCategory.Visual => "Visual", ReactionCategory.Audio => "Audio", ReactionCategory.Peripheral => "Peripheral",
            ReactionCategory.Choice => "Choice", _ => "Flick",
        };

    public static string Blurb(ReactionCategory c) => Loc.Spanish
        ? c switch
        {
            ReactionCategory.Visual => "La pantalla destella: haz clic. Tiempo de reacción visual simple, 12 intentos.",
            ReactionCategory.Audio => "Suena un clic: haz clic. Tiempo de reacción auditivo (pasos, disparos), 12 intentos.",
            ReactionCategory.Peripheral => "Ojos en el centro; un punto aparece cerca del borde: haz clic. Reacción a lo que ves con el rabillo del ojo.",
            ReactionCategory.Choice => "Verde: clic. Rojo: no. Velocidad de decisión y falsas alarmas (go / no-go).",
            _ => "Aparece un objetivo a 5–25°: flick y clic. Inicio del movimiento (reacción) y tiempo hasta acertar (puntería), por separado.",
        }
        : c switch
        {
            ReactionCategory.Visual => "The screen flashes: click. Simple visual reaction time, 12 trials.",
            ReactionCategory.Audio => "A click sounds: click. Hearing reaction time (footsteps, gunfire), 12 trials.",
            ReactionCategory.Peripheral => "Eyes on the centre; a dot pops near the edge: click. Reaction to what you see from the corner of your eye.",
            ReactionCategory.Choice => "Green: click. Red: don't. Decision speed and false alarms (go / no-go).",
            _ => "A target appears 5–25° away: flick and click. Movement onset (reaction) and time to hit (aim), separately.",
        };

    /// <summary>Median-reaction thresholds (ms) per rank badge Rookie … Pro for a category (lower is better).</summary>
    public static float[] Reference(ReactionCategory c)
    {
        float off = c switch { ReactionCategory.Audio => -40f, ReactionCategory.Peripheral => 35f, ReactionCategory.Choice => 90f, ReactionCategory.Flick => 15f, _ => 0f };
        return new[] { 330f + off, 265f + off, 230f + off, 200f + off, 175f + off };
    }

    public static string RankOf(ReactionCategory c, float medianMs)
    {
        int b = Difficulty.BadgeLower(medianMs, Reference(c));
        return b < 0
            ? (Loc.Spanish ? "por debajo de Principiante" : "below Rookie")
            : Difficulty.Tiers[b].Name + " (" + Difficulty.Tiers[b].Ranks + ")";
    }

    /// <summary>One category's history: (when, median, best, sd) per run, oldest first.</summary>
    public static List<(DateTime When, float Median, float Best, float Sd, float Extra)> History(StatsStore stats, ReactionCategory c)
    {
        var list = new List<(DateTime, float, float, float, float)>();
        foreach (var r in stats.Runs)
        {
            if (r.Mode == KeyOf(c) && r.Metrics is { } m && m.TryGetValue("rt.median", out var med))
                list.Add((r.When, med, m.GetValueOrDefault("rt.best", med), m.GetValueOrDefault("rt.sd", 0), m.GetValueOrDefault(c == ReactionCategory.Choice ? "rt.false" : "rt.hit", 0)));
            else if (c == ReactionCategory.Visual && r.Mode == "reaction" && r.AvgKillMs > 0)
                list.Add((r.When, r.AvgKillMs, r.AvgKillMs, 0, 0));
        }
        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list;
    }

    /// <summary>Summary of a history: last, best ever (lowest median), average of the last five and the change against
    /// the five before (ms, negative = faster).</summary>
    public static (float Last, float Best, float Recent, float Trend, int Runs) Summary(List<(DateTime When, float Median, float Best, float Sd, float Extra)> h)
    {
        if (h.Count == 0) return (0, 0, 0, 0, 0);
        var meds = h.Select(x => x.Median).ToList();
        float recent = meds.TakeLast(5).Average();
        float before = meds.Count > 5 ? meds.SkipLast(5).TakeLast(5).Average() : recent;
        return (meds[^1], meds.Min(), recent, recent - before, h.Count);
    }

    public static string Ms(float v) => v.ToString("0", CultureInfo.InvariantCulture) + " ms";
}
