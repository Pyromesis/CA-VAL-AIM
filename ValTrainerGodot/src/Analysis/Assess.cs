using ValTrainer.Core;

namespace ValTrainer.Analysis;

/// <summary>Result of rating + diagnosing one metric dictionary (a single run or the aggregated profile).</summary>
sealed class Assessment
{
    public readonly Dictionary<Skill, SkillRating> Ratings = new();
    public float Overall = -1;            // −1 = fewer than 4 skills with confidence ≥ 0.5
    public float Provisional = float.NaN; // same formula over whatever is rated (used as a reference when Overall is −1)
    public float OverallConf;
    public float RefTier;                 // tier whose benchmark values are the "band" in the rules
    public float RawMean = float.NaN;     // robust weighted mean of the skill tiers, before shrinking toward the population median
    /// <summary>Dev trace (CoachCli --explain): every benchmark component and how the overall was formed.</summary>
    public readonly List<string> Trace = new();
    public readonly List<Issue> Issues = new();
    public readonly List<(Skill Skill, float Score, string Text)> Strengths = new();

    public sealed record Issue(Diagnosis D, float Importance, string Short, bool Possible);
}

/// <summary>Spec B (skill tiers, composites, confidence, overall rank) and C (the 20 diagnosis rules).</summary>
static class Assess
{
    static bool Es => Loc.Spanish;
    static string S(string en, string es) => Es ? es : en;
    static string F2(FormattableString en, FormattableString es) => Es ? Comma(FormattableString.Invariant(es)) : FormattableString.Invariant(en);

    /// <summary>Spanish decimals use a comma; the analysis formats every number invariantly with a dot.</summary>
    static string Comma(string s)
    {
        var a = s.ToCharArray();
        for (int i = 1; i + 1 < a.Length; i++)
            if (a[i] == '.' && char.IsDigit(a[i - 1]) && char.IsDigit(a[i + 1])) a[i] = ',';
        return new string(a);
    }

    static string UnitEs(string unit) => unit switch
    {
        "samples" => "muestras",
        "off-target flicks" => "flicks fallados",
        "flicks" => "flicks",
        "misses" => "fallos",
        "hits" => "impactos",
        "trials" => "pruebas",
        "sightings" => "avistamientos",
        "shots" => "disparos",
        "stops" => "paradas",
        "sprays" => "sprays",
        "flashes" => "flashes",
        "direction changes" => "cambios de dirección",
        "s of tracking" => "segundos de tracking",
        _ => unit,
    };

    public static readonly Dictionary<Skill, float> SkillWeight = new()
    {
        [Skill.CrosshairPlacement] = 0.20f, [Skill.Flicking] = 0.15f, [Skill.Precision] = 0.15f, [Skill.Movement] = 0.15f,
        [Skill.SprayControl] = 0.10f, [Skill.Tracking] = 0.10f, [Skill.Utility] = 0.10f, [Skill.Reaction] = 0.05f,
    };

    static float V(Dictionary<string, float> m, string k) => m.TryGetValue(k, out var v) ? v : float.NaN;
    static float N(Dictionary<string, float> m, string k) => m.TryGetValue(k, out var v) && !float.IsNaN(v) ? v : 0;
    static string F(FormattableString s) => FormattableString.Invariant(s);
    static float Conf(float n, float nMin) => n <= 0 ? 0 : n / (n + nMin);
    /// <summary>"+1.2" / "−0.8" / "0.0" (never "-0.0").</summary>
    static string Signed(float v) => MathF.Abs(v) < 0.05f ? "0.0" : F($"{(v > 0 ? "+" : "")}{v:0.0}");

    /// <summary>
    /// Movement is only rated once the player has actually stopped a few times: a drill played standing still would
    /// otherwise score "0% moving shots" as Radiant.
    /// </summary>
    const float MinStops = 5;

    static float WeightedMedian(List<(float T, float W)> xs)
    {
        xs.Sort((x, y) => x.T.CompareTo(y.T));
        float half = xs.Sum(x => x.W) / 2, cum = 0;
        foreach (var (t, w) in xs) { cum += w; if (cum >= half) return t; }
        return xs[^1].T;
    }

    static float Comp(params (float T, float W)[] parts)
    {
        float s = 0, w = 0;
        foreach (var (t, wt) in parts) if (!float.IsNaN(t)) { s += t * wt; w += wt; }
        return w > 0 ? s / w : float.NaN;
    }

    /// <summary>
    /// The precision benchmarks are for head-sized targets: the rating uses head-sized trials only (first-shot hits from
    /// any head, settle time / corrections from static heads). The click-behaviour rules fall back to all static targets.
    /// </summary>
    static bool HeadData(Dictionary<string, float> m) => N(m, "head.n_first") >= 5;
    static bool HeadSettle(Dictionary<string, float> m) => N(m, "head.n_hit") >= 10;
    static float Tc(Dictionary<string, float> m) => HeadSettle(m) ? V(m, "head.tc_ms") : V(m, "flick.tc_ms");
    static float Nc(Dictionary<string, float> m) => HeadSettle(m) ? V(m, "head.nc") : V(m, "flick.nc");
    static float NHit(Dictionary<string, float> m) => HeadSettle(m) ? N(m, "head.n_hit") : N(m, "flick.n_hit");

    public static Assessment Run(Dictionary<string, float> m, bool runLevel, int tierPlayed)
    {
        var a = new Assessment();
        Rate(m, a);
        // Overall (spec B "Overall rank"): a robust weighted mean of the skill tiers (weights w·c; each skill clipped to
        // ±OutlierClip around the weighted median so one standout skill can't carry the estimate), shrunk toward the
        // median player by how much evidence there is and how well aim mechanics predict rank at all. Needs ≥ 4 skills
        // with c ≥ 0.5.
        var rated = a.Ratings.Values.Where(r => r.Tier > -0.9f && r.Confidence > 0).ToList();
        float wAll = SkillWeight.Values.Sum();
        float wc = rated.Sum(r => SkillWeight[r.Skill] * r.Confidence);
        int confident = rated.Count(r => r.Confidence >= 0.5f);
        a.OverallConf = wc / wAll;
        if (wc > 0)
        {
            float med = WeightedMedian(rated.Select(r => (r.Tier, SkillWeight[r.Skill] * r.Confidence)).ToList());
            float s = 0;
            foreach (var r in rated) s += SkillWeight[r.Skill] * r.Confidence * Math.Clamp(r.Tier, med - Bench.OutlierClip, med + Bench.OutlierClip);
            a.RawMean = s / wc;
            a.Provisional = Bench.Shrink(a.RawMean, a.OverallConf);
            a.Trace.Add(F($"overall: weighted median {med:0.00}, robust mean {a.RawMean:0.00}, evidence {a.OverallConf:0.00} → λ {Bench.ShrinkFactor(a.OverallConf):0.00} → {a.Provisional:0.00} ({Bench.Rank(a.Provisional)}), prior {Bench.PriorTier:0.00}"));
        }
        if (confident >= 4) a.Overall = a.Provisional;
        a.RefTier = runLevel ? Math.Clamp(tierPlayed, 0, 4)
            : a.Overall >= 0 ? a.Overall
            : !float.IsNaN(a.Provisional) ? Math.Clamp(a.Provisional, 0, 4) : Math.Clamp(tierPlayed, 0, 4);
        Diagnose(m, a, runLevel);
        Strengths(m, a, runLevel, tierPlayed);
        return a;
    }

    // =====================================================================================================
    // Skill ratings
    // =====================================================================================================

    static void Rate(Dictionary<string, float> m, Assessment a)
    {
        void Put(Skill sk, float t, float c, float n, string headline)
        {
            // No confidence (e.g. tracking without a single target reversal) = not rated yet: show the progress line.
            if (float.IsNaN(t) || n <= 0 || !(c > 0)) { a.Ratings[sk] = new SkillRating(sk, -1, 0, (int)MathF.Round(Math.Max(0, n)), Progress(sk, m)); return; }
            t = Math.Clamp(t, -0.5f, 4.5f); // −1 is reserved for "not enough data"
            a.Ratings[sk] = new SkillRating(sk, t, c, (int)MathF.Round(n), headline.Length == 0 ? Loc.T(Bench.Rank(t)) : $"{Loc.T(Bench.Rank(t))} · {headline}");
            a.Trace.Add(F($"{sk} = {t:0.00} (c {c:0.00}, n {n:0})"));
        }
        // One benchmark component: tier of value x on table b, optionally capped (metrics that only show "not bad").
        float B(string label, float x, float[] b, float cap = 4.5f)
        {
            float t = Math.Min(Bench.TierOf(x, b), cap);
            if (!float.IsNaN(t)) a.Trace.Add(F($"    {label} = {x:0.###} → {t:0.00}"));
            return t;
        }
        float Given(string label, float t)
        {
            if (!float.IsNaN(t)) a.Trace.Add(F($"    {label} → {t:0.00}"));
            return t;
        }

        // Flicking: 0.55 time-to-hit (or MT_ref), 0.3 endpoint error, 0.15 onset RT (drill targets appear on a rhythm, so
        // onset partly measures anticipation).
        {
            float speedT = Given("speed tier (badges of the tier played)", V(m, "speed.tier"));
            string speed = "";
            if (!float.IsNaN(V(m, "flick.ttk_hit_ms"))) speed = F2($"{V(m, "flick.ttk_hit_ms"):0} ms to hit", $"{V(m, "flick.ttk_hit_ms"):0} ms hasta impactar");
            else if (!float.IsNaN(V(m, "speed.score_min"))) speed = F2($"{V(m, "speed.score_min"):0} pts/min", $"{V(m, "speed.score_min"):0} pts/min");
            if (float.IsNaN(speedT))
            {
                speedT = B("flick time MT_ref ms", V(m, "flick.mt_ref_ms"), Bench.MtRefMs);
                if (!float.IsNaN(speedT)) speed = F2($"{V(m, "flick.mt_ref_ms"):0} ms flick time", $"{V(m, "flick.mt_ref_ms"):0} ms por flick");
            }
            float endT = B("endpoint error %", V(m, "flick.end_err_pct"), Bench.EndErrPct);
            float onT = B("onset ms", V(m, "flick.onset_ms"), Bench.OnsetMs);
            var parts = new List<string>();
            if (speed.Length > 0) parts.Add(speed);
            if (!float.IsNaN(endT)) parts.Add(F2($"lands within {V(m, "flick.end_err_pct"):0}%", $"termina dentro del {V(m, "flick.end_err_pct"):0}%"));
            if (!float.IsNaN(onT)) parts.Add(F2($"starts in {V(m, "flick.onset_ms"):0} ms", $"arranca en {V(m, "flick.onset_ms"):0} ms"));
            float n = N(m, "flick.n5");
            Put(Skill.Flicking, Comp((speedT, .55f), (endT, .3f), (onT, .15f)), Conf(n, 30), n, string.Join(", ", parts));
        }
        // Precision: 0.4 first-shot hit, 0.4 T_c, 0.2 N_c — head-sized targets only (the benchmarks are for heads).
        {
            float fsh = V(m, "head.fsh_pct"), n = N(m, "head.n_first");
            bool settle = HeadSettle(m);
            float tc = settle ? V(m, "head.tc_ms") : float.NaN, nc = settle ? V(m, "head.nc") : float.NaN;
            float t = HeadData(m) ? Comp((B("first-shot head hit %", fsh, Bench.FirstShotHitPct), .4f), (B("settle T_c ms", tc, Bench.TcMs), .4f), (B("corrections N_c", nc, Bench.NcRating), .2f)) : float.NaN;
            var parts = new List<string>();
            if (!float.IsNaN(fsh)) parts.Add(F2($"{fsh:0}% first-shot head hits", $"{fsh:0}% de aciertos a la cabeza al primer disparo"));
            if (!float.IsNaN(tc)) parts.Add(F2($"settles in {tc:0} ms", $"se asienta en {tc:0} ms"));
            if (!float.IsNaN(nc)) parts.Add(F2($"{nc:0.0} corrections", $"{nc:0.0} correcciones"));
            Put(Skill.Precision, t, Conf(n, 30), n, string.Join(", ", parts));
        }
        // Tracking: 0.6 on-target, 0.2 τ, 0.2 re-acquire.
        {
            float secs = N(m, "track.secs"), rev = N(m, "track.reversals");
            float t = Comp((Given("on-target tier (tier played)", V(m, "track.ontarget_tier")), .6f), (B("lag τ ms", V(m, "track.tau_ms"), Bench.TauMs), .2f), (B("re-acquire ms", V(m, "track.reacq_ms"), Bench.ReacqMs), .2f));
            var parts = new List<string>();
            if (!float.IsNaN(V(m, "track.ontarget"))) parts.Add(F2($"{V(m, "track.ontarget"):0}% on target", $"{V(m, "track.ontarget"):0}% en el objetivo"));
            if (!float.IsNaN(V(m, "track.tau_ms"))) parts.Add(F2($"{V(m, "track.tau_ms"):0} ms lag", $"{V(m, "track.tau_ms"):0} ms de retraso"));
            if (!float.IsNaN(V(m, "track.reacq_ms"))) parts.Add(F2($"re-acquires in {V(m, "track.reacq_ms"):0} ms", $"readquiere en {V(m, "track.reacq_ms"):0} ms"));
            Put(Skill.Tracking, t, Math.Min(Conf(secs, 60), Conf(rev, 10)), secs, string.Join(", ", parts));
        }
        // Reaction.
        {
            float n = N(m, "react.n"), ms = V(m, "react.ms");
            Put(Skill.Reaction, B("reaction ms", ms, Bench.ReactMs), Conf(n, 10), n, F2($"{ms:0} ms median", $"{ms:0} ms de mediana"));
        }
        // Crosshair placement: 0.7 E, 0.3 |ε_p| (too high is as bad as too low). Head height on a flat drill map is
        // necessary but not sufficient, so the vertical part is capped, and a rating from it alone counts half.
        {
            float n = N(m, "xhair.n"), e = V(m, "xhair.err_deg"), p = V(m, "xhair.pitch_deg");
            float pAbs = !float.IsNaN(V(m, "xhair.pitch_abs_deg")) ? V(m, "xhair.pitch_abs_deg") : MathF.Abs(p);
            float t = Comp((B("first-sight error E°", e, Bench.XhairErrDeg), .7f), (B("|vertical error|°", pAbs, Bench.PitchAbsDeg, Bench.PitchCap), .3f));
            string head = float.IsNaN(p) ? F2($"{e:0.0}° off on first sight", $"{e:0.0}° de desvío al primer avistamiento")
                : float.IsNaN(e) ? F2($"{Signed(p)}° from head height on first sight", $"{Signed(p)}° respecto a la altura de la cabeza al primer avistamiento")
                : F2($"{e:0.0}° off on first sight, {Signed(p)}° vertical", $"{e:0.0}° de desvío al primer avistamiento, {Signed(p)}° vertical");
            Put(Skill.CrosshairPlacement, t, float.IsNaN(e) ? 0.5f * Conf(n, 15) : Conf(n, 15), n, head);
        }
        // Spray: 0.4 |V|, 0.3 H, 0.3 compensation delay.
        {
            float n = N(m, "spray.n");
            float t = Comp((B("spray |V|°", V(m, "spray.v_abs"), Bench.SprayV), .4f), (B("spray H°", V(m, "spray.h"), Bench.SprayH), .3f),
                (B("pull delay ms", V(m, "spray.delay_ms"), Bench.CompDelayMs, Bench.CompDelayCap), .3f));
            var parts = new List<string>();
            if (!float.IsNaN(V(m, "spray.v_abs"))) parts.Add(F2($"{V(m, "spray.v_abs"):0.0}° vertical", $"{V(m, "spray.v_abs"):0.0}° vertical"));
            if (!float.IsNaN(V(m, "spray.h"))) parts.Add(F2($"{V(m, "spray.h"):0.0}° sideways", $"{V(m, "spray.h"):0.0}° horizontal"));
            if (!float.IsNaN(V(m, "spray.delay_ms"))) parts.Add(F2($"pulls after {V(m, "spray.delay_ms"):0} ms", $"compensa tras {V(m, "spray.delay_ms"):0} ms"));
            Put(Skill.SprayControl, t, Conf(n, 8), n, string.Join(", ", parts));
        }
        // Movement: 0.4 moving shots, 0.3 counter-strafe use, 0.3 stop time.
        {
            float shots = N(m, "move.shots"), stops = N(m, "move.stops");
            float stop = V(m, "move.stop_ms");
            float t = stops < MinStops ? float.NaN
                : Comp((B("moving shots %", V(m, "move.moving_shot_pct"), Bench.MovingShotPct), .4f), (B("counter-strafed %", V(m, "move.counter_pct"), Bench.CounterPct), .3f),
                    (B("stop ms (release → accurate)", stop, Bench.StopMs), .3f));
            var cs = new List<float>();
            if (shots > 0) cs.Add(Conf(shots, 30));
            if (stops > 0) cs.Add(Conf(stops, 20));
            var parts = new List<string>();
            if (!float.IsNaN(V(m, "move.moving_shot_pct"))) parts.Add(F2($"{V(m, "move.moving_shot_pct"):0}% moving shots", $"{V(m, "move.moving_shot_pct"):0}% de disparos en movimiento"));
            if (!float.IsNaN(V(m, "move.counter_pct"))) parts.Add(F2($"{V(m, "move.counter_pct"):0}% counter-strafed", $"{V(m, "move.counter_pct"):0}% a contra-strafe"));
            Put(Skill.Movement, t, cs.Count == 0 ? 0 : cs.Average(), shots + stops, string.Join(", ", parts));
        }
        // Utility (flash): 0.6 turn-away, 0.4 outcome.
        {
            float n = N(m, "flash.n");
            // Outcome normalised for the tier played (low-tier attackers swing late and hold fire on a blinded player).
            float dodgeT = !float.IsNaN(V(m, "flash.dodge_tier")) ? Given("flash outcome tier (tier played)", V(m, "flash.dodge_tier"))
                : B("flash outcome %", V(m, "flash.dodge_pct"), Bench.FlashDodgePct);
            float t = Comp((B("flash turn-away ms", V(m, "flash.turn_ms"), Bench.FlashTurnMs), .6f), (dodgeT, .4f));
            var parts = new List<string>();
            if (!float.IsNaN(V(m, "flash.turn_ms"))) parts.Add(F2($"turns in {V(m, "flash.turn_ms"):0} ms", $"gira en {V(m, "flash.turn_ms"):0} ms"));
            if (!float.IsNaN(V(m, "flash.flashed_pct"))) parts.Add(F2($"flashed {V(m, "flash.flashed_pct"):0}%", $"cegado {V(m, "flash.flashed_pct"):0}%"));
            Put(Skill.Utility, t, Conf(n, 8), n, string.Join(", ", parts));
        }
    }

    /// <summary>Headline for an unrated skill: how far along the minimum sample is and which drill fills it.</summary>
    static string Progress(Skill sk, Dictionary<string, float> m) => sk switch
    {
        Skill.Flicking => F2($"{N(m, "flick.n5"):0} of 30 flicks (5°+) — play Head Flicks or Spidershot", $"{N(m, "flick.n5"):0} de 30 flicks (5°+) — juega Flicks a la cabeza o Spidershot"),
        Skill.Precision => F2($"{N(m, "head.n_first"):0} of 30 head-sized targets — play Head Flicks or Strafe Bots", $"{N(m, "head.n_first"):0} de 30 objetivos del tamaño de una cabeza — juega Flicks a la cabeza o Bots en strafe"),
        Skill.Tracking => N(m, "track.secs") >= 60 ? F2($"{N(m, "track.reversals"):0} of 10 target reversals — play Strafe Tracking", $"{N(m, "track.reversals"):0} de 10 cambios de dirección del objetivo — juega Seguimiento en strafe")
                                                   : F2($"{N(m, "track.secs"):0} of 60 s of tracking — play Strafe Tracking", $"{N(m, "track.secs"):0} de 60 s de seguimiento — juega Seguimiento en strafe"),
        Skill.Reaction => F2($"{N(m, "react.n"):0} of 10 trials — take the Reaction Test", $"{N(m, "react.n"):0} de 10 pruebas — haz el Test de reacción"),
        Skill.CrosshairPlacement => F2($"{N(m, "xhair.n"):0} of 15 enemy sightings — play Peek Practice or Site Clear", $"{N(m, "xhair.n"):0} de 15 avistamientos — juega Práctica de asomarse o Despeje de sitio"),
        Skill.SprayControl => F2($"{N(m, "spray.n"):0} of 8 sprays (10+ bullets) — play Vandal or Phantom Spray", $"{N(m, "spray.n"):0} de 8 ráfagas (10+ balas) — juega Ráfaga Vandal o Ráfaga Phantom"),
        Skill.Movement => F2($"{N(m, "move.stops"):0} of 20 stops, {N(m, "move.shots"):0} of 30 shots — play Counter-Strafe or Peek Duels", $"{N(m, "move.stops"):0} de 20 paradas, {N(m, "move.shots"):0} de 30 disparos — juega Contra-strafe o Duelos de asomada"),
        Skill.Utility => F2($"{N(m, "flash.n"):0} of 8 flashes — play Flash Dodge", $"{N(m, "flash.n"):0} de 8 flashes — juega Esquivar flashes"),
        _ => S("Not enough data yet", "Aún no hay datos suficientes"),
    };

    // =====================================================================================================
    // Diagnoses (spec C)
    // =====================================================================================================

    static void Diagnose(Dictionary<string, float> m, Assessment a, bool runLevel)
    {
        float refT = a.RefTier;
        string rankName = Loc.T(Bench.Rank(refT));
        float overallRef = a.Overall >= 0 ? a.Overall : !float.IsNaN(a.Provisional) && !runLevel ? a.Provisional : refT;

        void Add(string id, Skill skill, int level, float n, int nMin, string title, string evidence, string why,
                 string[] fixes, string[] drills, string shortText, string? sens = null, bool forcePossible = false, string unit = "samples")
        {
            if (level <= 0 || float.IsNaN(n)) return;
            int minShow = runLevel ? Math.Max(4, nMin / 4) : Math.Max(5, nMin / 3);
            if (n < minShow) return;
            bool possible = n < nMin || forcePossible;
            var sev = level >= 2 ? Severity.Major : Severity.Minor;
            float skillT = a.Ratings.TryGetValue(skill, out var rt) && rt.Tier > -0.9f ? rt.Tier : float.NaN;
            if (!float.IsNaN(skillT) && skillT <= overallRef - 1f) sev = Severity.Major;
            if (possible) sev = Severity.Tip;
            string unitText = Es ? UnitEs(unit) : unit;
            string ev = possible
                ? (forcePossible && n >= nMin ? evidence : Es
                    ? $"{evidence} — señal temprana: solo {MathF.Floor(n):0} de {nMin} {unitText} necesarios"
                    : $"{evidence} — early signal, only {MathF.Floor(n):0} of the {nMin} {unit} needed")
                : (Es ? $"{evidence} ({n:0} {unitText})" : $"{evidence} ({n:0} {unit})");
            var fx = fixes.ToList();
            if (sens != null) fx.Add((Es ? "Sensibilidad: " : "Sensitivity: ") + sens);
            if (Es)
            {
                title = Loc.T(title);
                why = Loc.T(why);
                fx = fx.Select(Loc.T).ToList();
            }
            var d = new Diagnosis(id, skill, sev, title, ev, why, fx.ToArray(), drills, (int)MathF.Round(n));
            float sevW = sev switch { Severity.Major => 2f, Severity.Minor => 1f, _ => 0.5f };
            float gap = float.IsNaN(skillT) ? 0.5f : Math.Max(0, overallRef - skillT);
            float imp = sevW * SkillWeight[skill] * (1f + gap) * (level >= 2 ? 1.25f : 1f) * (possible ? 0.5f : 1f);
            a.Issues.Add(new Assessment.Issue(d, imp, shortText, possible));
        }

        // ---- shared flick quantities ----
        float r = V(m, "flick.r_deg");
        float nOff = N(m, "flick.n_off"), nFl = N(m, "flick.n"), nPrim = N(m, "flick.n_prim"), misses = N(m, "flick.misses");
        float O = V(m, "flick.overshoot_share"), dOver = V(m, "flick.mean_overshoot_deg"), pOver = V(m, "flick.mean_overshoot_pct");
        float nc = Nc(m), tc = Tc(m), nHit = NHit(m);
        float J = V(m, "track.jitter"), bandJ = Bench.ValueAt(refT, Bench.Jitter);
        float bandNc = Bench.ValueAt(refT, Bench.Nc), bandTc = Bench.ValueAt(refT, Bench.TcMs);

        // OVERFLICK
        if (O >= .55f && dOver >= 1.5f * r && pOver >= 4f)
        {
            bool highNcJ = nc > bandNc + 0.3f || (!float.IsNaN(J) && J > 1.3f * bandJ);
            Add("OVERFLICK", Skill.Flicking, O >= .70f ? 2 : 1, nOff, 30,
                "Over-flicking: your flicks travel too far",
                F2($"{O * 100:0}% of your off-target flicks ended past the target, by {dOver:0.0}° ({pOver:0}% of the distance)",
                    $"El {O * 100:0}% de tus flicks fallados se pasaron del objetivo, por {dOver:0.0}° ({pOver:0}% de la distancia)"),
                "The flick is planned too long, or the arm is tense and brakes late. Undoing an overshoot costs more time than finishing a short flick: you have to stop, reverse and re-aim.",
                new[]
                {
                    "Aim to land about 90% of the way there and finish with one small micro-adjustment.",
                    "Brake early: the second half of the flick should feel like slowing down, not pushing.",
                    "Relax your grip and forearm — a tense arm overshoots and can't stop cleanly.",
                    "Play Head Flicks one tier lower until most flicks land on or just short of the head.",
                },
                new[] { "flick", "spider" },
                F2($"{O * 100:0}% of your off-target flicks overshot the target by {dOver:0.0}°",
                    $"El {O * 100:0}% de tus flicks fallados se pasaron {dOver:0.0}° del objetivo"),
                highNcJ ? S("you also need many corrections / track shakily — try 5–10% lower sens.", "además necesitas muchas correcciones o haces tracking inestable: prueba una sensibilidad un 5–10% más baja.") : null, unit: "off-target flicks");
        }

        // UNDERFLICK
        float G = V(m, "flick.gain"), U = V(m, "flick.under_share"), dUnder = V(m, "flick.mean_undershoot_deg");
        if (G < .85f && U >= .85f || G < .75f && U >= .7f)
        {
            float gs = V(m, "flick.gain_small"), gl = V(m, "flick.gain_large");
            bool wideOnly = gl < .85f && gs >= .9f && N(m, "flick.n_large") >= 5;
            Add("UNDERFLICK", Skill.Flicking, G < .75f ? 2 : 1, nPrim, 30,
                "Stopping short and dragging onto the target",
                F2($"Your flicks covered {G * 100:0}% of the distance; {U * 100:0}% of the off-target ones stopped short",
                    $"Tus flicks recorrieron el {G * 100:0}% de la distancia; el {U * 100:0}% de los fallados se quedaron cortos")
                    + (float.IsNaN(dUnder) ? "" : F2($", by {dUnder:0.0}° on average", $", por {dUnder:0.0}° de media")),
                "Hesitation, or your arm runs out of room on wide angles, so every flick ends with a slow drag onto the head.",
                new[]
                {
                    "Commit to one full motion that reaches the head; keep the micro-adjustment tiny.",
                    "Use your arm, not just the wrist, for wide flicks so the first motion covers the whole distance.",
                    "Look at the target first, then move — the eyes lead the hand.",
                },
                new[] { "spider", "flick" },
                F2($"your flicks stopped short (covering {G * 100:0}% of the distance)", $"tus flicks se quedaron cortos (recorriendo el {G * 100:0}% de la distancia)"),
                wideOnly ? F2($"only your wide flicks (≥ 25°) fall short ({gl * 100:0}% vs {gs * 100:0}%) — try 5–10% higher sens.",
                    $"solo tus flicks amplios (≥ 25°) se quedan cortos ({gl * 100:0}% vs {gs * 100:0}%): prueba una sensibilidad un 5–10% más alta.") : null, unit: "flicks");
        }

        // NO_MICRO
        float snapShare = V(m, "flick.snap_share"), snapErr = V(m, "flick.snap_err_deg");
        if (snapShare >= .40f)
            Add("NO_MICRO", Skill.Precision, snapShare >= .60f ? 2 : 1, misses, 20,
                "Flicks have no micro-adjustment",
                float.IsNaN(snapErr)
                    ? F2($"{snapShare * 100:0}% of your misses were fired straight off the flick, with no correction",
                        $"El {snapShare * 100:0}% de tus fallos se dispararon directamente al terminar el flick, sin corrección")
                    : F2($"{snapShare * 100:0}% of your misses were fired straight off the flick, {snapErr:0.0}° from the target, with no correction",
                        $"El {snapShare * 100:0}% de tus fallos se dispararon directamente al terminar el flick, a {snapErr:0.0}° del objetivo, sin corrección"),
                "The click is chained to the flick: you fire the moment your hand stops, not when the crosshair is actually on the head.",
                new[]
                {
                    "Think \"flick, confirm, click\" — a split-second check before every shot.",
                    "If the flick lands off, make one small correction instead of firing anyway.",
                    "Play Head Flicks at Rookie (untimed) until you reach 85% accuracy, then speed up.",
                },
                new[] { "flick", "gridshot" },
                F2($"{snapShare * 100:0}% of your misses were fired straight off the flick with no correction",
                    $"El {snapShare * 100:0}% de tus fallos se dispararon al terminar el flick sin corregir"), unit: "misses");

        // SLOW_MICRO
        if (!float.IsNaN(tc) && (tc > 1.3f * bandTc || nc >= 2.5f))
        {
            float amp = V(m, "flick.corr_amp_r");
            string? sens = amp < 2f && nc >= Math.Max(2f, bandNc + 0.3f) ? S("lots of corrections smaller than two head-widths usually means the sens is a bit high — try 5% lower.",
                    "muchas correcciones más pequeñas que dos cabezas suelen indicar que la sensibilidad es algo alta: prueba un 5% menos.")
                : amp >= 2f && nc < 1.6f ? S("one long, slow drag after the flick can mean the sens is a bit low — try 5% higher.",
                    "un arrastre largo y lento después del flick puede indicar que la sensibilidad es algo baja: prueba un 5% más.") : null;
            Add("SLOW_MICRO", Skill.Precision, nc >= 3f || tc > 1.7f * bandTc ? 2 : 1, nHit, 30,
                "Slow, fragmented micro-adjustments",
                float.IsNaN(nc)
                    ? F2($"After the flick you needed {tc:0} ms to settle (typical for {rankName}: {bandTc:0} ms)",
                        $"Después del flick necesitaste {tc:0} ms para estabilizarte (típico en {rankName}: {bandTc:0} ms)")
                    : F2($"After the flick you needed {tc:0} ms and {nc:0.0} corrections to settle (typical for {rankName}: {bandTc:0} ms, {bandNc:0.0})",
                        $"Después del flick necesitaste {tc:0} ms y {nc:0.0} correcciones para estabilizarte (típico en {rankName}: {bandTc:0} ms, {bandNc:0.0})"),
                "Many small corrections instead of one decisive one — every extra correction costs about 100–150 ms.",
                new[]
                {
                    "Make one deliberate correction with your fingers and wrist, then click.",
                    "Keep the hand loose after the flick so the correction is quick and small.",
                    "The head is bigger than its centre: click once you're on it, don't chase the exact middle.",
                },
                new[] { "gridshot", "flick" },
                float.IsNaN(nc) ? F2($"you took {tc:0} ms to settle after each flick", $"tardaste {tc:0} ms en estabilizarte después de cada flick")
                    : F2($"you took {tc:0} ms and {nc:0.0} corrections to settle after each flick",
                        $"tardaste {tc:0} ms y {nc:0.0} correcciones en estabilizarte después de cada flick"),
                sens, unit: "hits");
        }

        // PREMATURE
        float prem = V(m, "flick.premature_share"), premDps = V(m, "flick.premature_dps");
        if (prem >= .25f)
            Add("PREMATURE", Skill.Precision, prem >= .40f ? 2 : 1, misses, 20,
                "Clicking while the crosshair is still flying",
                float.IsNaN(premDps)
                    ? F2($"{prem * 100:0}% of your misses were fired while the crosshair was still moving",
                        $"El {prem * 100:0}% de tus fallos se dispararon mientras la mira seguía moviéndose")
                    : F2($"{prem * 100:0}% of your misses were fired at {premDps:0}°/s, before you stopped",
                        $"El {prem * 100:0}% de tus fallos se dispararon a {premDps:0}°/s, antes de detenerte"),
                "Rhythm-clicking: the click is timed to the motion instead of to the crosshair being on target.",
                new[]
                {
                    "Stop, then shoot — the crosshair should be still when you click.",
                    "Slow your flicks down about 10% until your accuracy recovers.",
                    "Say \"stop-click\" in your head for a few sessions to break the rhythm.",
                },
                new[] { "flick" },
                F2($"{prem * 100:0}% of your misses were fired while the crosshair was still moving",
                    $"El {prem * 100:0}% de tus fallos se dispararon con la mira en movimiento"), unit: "misses");

        // HESITATE
        float dwell = V(m, "flick.dwell_ms");
        if (dwell > 150)
            Add("HESITATE", Skill.Precision, dwell > 250 ? 2 : 1, N(m, "flick.n_hit"), 30,
                "On the head but waiting to click",
                F2($"You sat on the target for a median {dwell:0} ms before firing",
                    $"Te quedaste sobre el objetivo una mediana de {dwell:0} ms antes de disparar"),
                "Over-checking: you confirm twice. In a real duel the enemy fires in that gap.",
                new[]
                {
                    "Fire as soon as the crosshair touches the head.",
                    "Trust the first confirmation — speed up the click, not the flick.",
                    "Play Gridshot focusing only on clicking the instant you arrive.",
                },
                new[] { "gridshot" },
                F2($"you waited {dwell:0} ms on the target before clicking", $"esperaste {dwell:0} ms sobre el objetivo antes de hacer clic"), unit: "hits");

        // CURVED (path efficiency of the primary flick itself, so overshoot returns don't count as curvature)
        float etaP = V(m, "flick.eta_p"), endY = V(m, "flick.end_y_deg"), low = V(m, "flick.low_share");
        bool curved = etaP < .85f, lowEnd = endY < -r;
        if (curved || lowEnd)
        {
            string lowTxt = float.IsNaN(low)
                ? F2($"they ended {-endY:0.0}° below the head on average", $"terminaron de media {-endY:0.0}° por debajo de la cabeza")
                : F2($"{low * 100:0}% ended below the head ({-endY:0.0}° low on average)",
                    $"el {low * 100:0}% terminaron por debajo de la cabeza ({-endY:0.0}° por debajo de media)");
            Add("CURVED", Skill.Flicking, etaP < .75f ? 2 : 1, nPrim, 30,
                curved && lowEnd ? S("Curved flicks that end under the head", "Flicks curvos que terminan bajo la cabeza") : curved ? S("Curved flicks", "Flicks curvos") : S("Flicks that end under the head", "Flicks que terminan bajo la cabeza"),
                curved && lowEnd ? F2($"Your flicks wasted {(1 - etaP) * 100:0}% of their path; {lowTxt}",
                    $"Tus flicks desperdiciaron el {(1 - etaP) * 100:0}% de su recorrido; {lowTxt}")
                    : curved ? F2($"Your flicks wasted {(1 - etaP) * 100:0}% of their path by curving instead of going straight",
                        $"Tus flicks desperdiciaron el {(1 - etaP) * 100:0}% de su recorrido por curvarse en vez de ir rectos")
                    : float.IsNaN(low) ? F2($"Your flicks ended {-endY:0.0}° below the head on average",
                        $"Tus flicks terminaron de media {-endY:0.0}° por debajo de la cabeza")
                    : F2($"{low * 100:0}% of your flicks ended below the head ({-endY:0.0}° low on average)",
                        $"El {low * 100:0}% de tus flicks terminaron por debajo de la cabeza ({-endY:0.0}° por debajo de media)"),
                "The wrist arcs instead of moving in a straight line, or the mouse pad sits at an angle, so flicks curve and land low.",
                new[]
                {
                    "Straighten the motion: move along the line to the target instead of pivoting around the wrist.",
                    "Line your mouse pad up with the monitor and sit square to it.",
                    "Level the crosshair at head height first, then flick sideways along that line.",
                },
                new[] { "spider" },
                lowEnd ? F2($"your flicks tended to end {-endY:0.0}° below the head", $"tus flicks tendían a terminar {-endY:0.0}° por debajo de la cabeza")
                    : F2($"your flicks curved, wasting {(1 - etaP) * 100:0}% of their path", $"tus flicks se curvaron y desperdiciaron el {(1 - etaP) * 100:0}% de su recorrido"),
                unit: "flicks");
        }

        // SLOW_START
        float onset = V(m, "flick.onset_ms"), react = V(m, "react.ms");
        bool haveReact = !float.IsNaN(react) && N(m, "react.n") >= 5;
        float baseRt = haveReact ? react : 250f;
        if (onset - baseRt > 120)
            Add("SLOW_START", Skill.Flicking, onset - baseRt > 180 ? 2 : 1, N(m, "flick.onset_n"), 30,
                "Slow to start moving",
                haveReact
                    ? F2($"You start moving {onset - baseRt:0} ms later than your raw reaction time (flick onset {onset:0} ms vs {react:0} ms in the Reaction Test)",
                        $"Empiezas a moverte {onset - baseRt:0} ms más tarde que tu reacción pura (inicio del flick {onset:0} ms vs {react:0} ms en la Prueba de Reacción)")
                    : F2($"Your flicks start {onset:0} ms after the target appears — about {onset - baseRt:0} ms slower than a typical 250 ms reaction (take the Reaction Test for an exact comparison)",
                        $"Tus flicks empiezan {onset:0} ms después de que aparezca el objetivo, unos {onset - baseRt:0} ms más lentos que una reacción típica de 250 ms (haz la Prueba de Reacción para una comparación exacta)"),
                "The delay is in finding the target or deciding where to go, not in your hands.",
                new[]
                {
                    "Keep your eyes at the centre of the screen and use peripheral vision to spot targets.",
                    "Start moving the moment you see colour — refine the exact spot while already moving.",
                    "Warm up with the Reaction Test, then Gridshot.",
                },
                new[] { "reaction", "gridshot" },
                F2($"you took {onset:0} ms to start moving toward each target", $"tardaste {onset:0} ms en empezar a moverte hacia cada objetivo"), null, !haveReact, unit: "flicks");

        // SLOW_REACT
        float bandR = Bench.ValueAt(refT, Bench.ReactMs);
        if (react > bandR + 30 || react > 300)
            Add("SLOW_REACT", Skill.Reaction, react > bandR + 80 || react > 360 ? 2 : 1, N(m, "react.n"), 10,
                "Slow reaction time",
                F2($"Median {react:0} ms (typical for {rankName}: {bandR:0} ms)",
                    $"Mediana de {react:0} ms (típico en {rankName}: {bandR:0} ms)"),
                "Usually fatigue or input lag rather than talent.",
                new[]
                {
                    "Turn V-Sync off and check your FPS cap and mouse polling rate.",
                    "Warm up for 5 minutes before playing ranked.",
                    "Test again when rested — tiredness costs 20–40 ms.",
                },
                new[] { "reaction" },
                F2($"your median reaction was {react:0} ms", $"tu reacción mediana fue de {react:0} ms"), unit: "trials");

        // ---- tracking ----
        float tSecs = N(m, "track.secs"), rev = N(m, "track.reversals"), tr = V(m, "track.r_deg");
        float lag = V(m, "track.lag_deg"), tau = V(m, "track.tau_ms"), overrun = V(m, "track.overrun_deg"), overEx = V(m, "track.overrun_excess_deg");
        if (!(tr > 0)) tr = float.NaN; // no target size → the lag / overrun thresholds can't be judged
        bool behind = lag > 0.6f * tr, lateT = tau > 200;
        if (behind || lateT)
            Add("TRACK_BEHIND", Skill.Tracking, lag > tr ? 2 : 1, tSecs, 60,
                "Chasing the target instead of matching it",
                behind && !float.IsNaN(tau) ? F2($"You trailed the target by {lag:0.00}° on average, following its motion {tau:0} ms late",
                    $"Te quedaste {lag:0.00}° por detrás del objetivo de media, siguiendo su movimiento {tau:0} ms tarde")
                    : behind ? F2($"You trailed the target by {lag:0.00}° on average", $"Te quedaste {lag:0.00}° por detrás del objetivo de media")
                    : F2($"Your aim followed the target's motion {tau:0} ms late (typical for {rankName}: {Bench.ValueAt(refT, Bench.TauMs):0} ms)",
                        $"Tu mira siguió el movimiento del objetivo {tau:0} ms tarde (típico en {rankName}: {Bench.ValueAt(refT, Bench.TauMs):0} ms)"),
                "You react to where the target was instead of matching its speed.",
                new[]
                {
                    "Match the target's speed first, then fine-tune the position.",
                    "Ride the leading edge of the target, not its centre.",
                    "Play Strafe Tracking one tier lower until you stay on target 50%+.",
                },
                new[] { "tracking", "strafebots" },
                behind ? F2($"you trailed the target by {lag:0.00}°", $"te quedaste {lag:0.00}° por detrás del objetivo")
                    : F2($"your aim followed the target {tau:0} ms late", $"tu mira siguió al objetivo {tau:0} ms tarde"),
                !float.IsNaN(J) && J <= bandJ ? S("your tracking is smooth but behind — try 5% higher sens.", "tu tracking es suave pero va por detrás: prueba una sensibilidad un 5% más alta.") : null, unit: "s of tracking");
        // Overrun is judged beyond the unavoidable carry-over of a ~120 ms reaction (see RunAnalysis.Tracking).
        bool ahead = lag < -0.4f * tr, overran = overEx > 1.5f * tr && !float.IsNaN(overrun);
        if (ahead || overran)
            Add("TRACK_OVERRUN", Skill.Tracking, overEx > 2.5f * tr ? 2 : 1, rev, 10,
                "Overshooting strafe reversals",
                overran
                    ? F2($"When the target changed direction you ran {overrun:0.00}° past it — {overEx:0.00}° more than a normal reaction carries you",
                        $"Cuando el objetivo cambió de dirección te pasaste {overrun:0.00}°: {overEx:0.00}° más de lo que arrastra una reacción normal")
                      + (ahead ? F2($"; you also ran {-lag:0.00}° ahead of it on average", $"; además ibas {-lag:0.00}° por delante de media") : "")
                    : F2($"You ran {-lag:0.00}° ahead of the target on average, as if expecting it to keep going",
                        $"Ibas {-lag:0.00}° por delante del objetivo de media, como si esperaras que siguiera"),
                "You're anticipating the strafe instead of reacting to it.",
                new[]
                {
                    "React to the stop — watch the body, not where you expect it to go.",
                    "Ease off as the target slows down; don't keep pushing through.",
                    "Play Strafe Tracking at Veteran or higher to practise reversals.",
                },
                new[] { "tracking" },
                overran ? F2($"you ran {overrun:0.00}° past the target when it changed direction", $"te pasaste {overrun:0.00}° del objetivo cuando cambió de dirección")
                    : F2($"you ran {-lag:0.00}° ahead of the target", $"ibas {-lag:0.00}° por delante del objetivo"),
                !float.IsNaN(J) && J > 1.3f * bandJ ? S("combined with shaky tracking this points to a slightly high sens — try 5% lower.",
                    "junto con un tracking inestable, esto apunta a una sensibilidad algo alta: prueba un 5% menos.") : null,
                unit: "direction changes");
        if (J > 1.3f * bandJ)
            Add("JITTER", Skill.Tracking, J > 1.6f * bandJ ? 2 : 1, tSecs, 60,
                "Shaky tracking",
                F2($"Your aim speed wobbled {J:0.00}× the target's motion (typical for {rankName}: {bandJ:0.00})",
                    $"La velocidad de tu mira osciló {J:0.00}× el movimiento del objetivo (típico en {rankName}: {bandJ:0.00})"),
                "A tight grip or fingertip-only aiming adds tremor and constant over-corrections.",
                new[]
                {
                    "Relax your grip — hold the mouse, don't squeeze it.",
                    "Track with your arm and wrist; keep the fingers quiet.",
                    "Smooth beats fast: let small errors go instead of chasing them.",
                },
                new[] { "tracking" },
                F2($"your tracking wobbled ({J:0.00}× the target's motion)", $"tu tracking osciló ({J:0.00}× el movimiento del objetivo)"),
                S("try 5–10% lower sens.", "prueba una sensibilidad un 5–10% más baja."), unit: "s of tracking");
        float reacq = V(m, "track.reacq_ms"), bandRq = Bench.ValueAt(refT, Bench.ReacqMs);
        if (reacq > 1.3f * bandRq)
            Add("SLOW_REACQ", Skill.Tracking, reacq > 1.6f * bandRq ? 2 : 1, rev, 10,
                "Slow to re-acquire after a direction change",
                F2($"It took {reacq:0} ms to get back on after a direction change (typical for {rankName}: {bandRq:0} ms)",
                    $"Tardaste {reacq:0} ms en volver a ponerte encima tras un cambio de dirección (típico en {rankName}: {bandRq:0} ms)"),
                "After the target changes direction your correction comes late or overshoots, so you spend time off target.",
                new[]
                {
                    "Watch the body for the moment it plants, then reverse immediately.",
                    "Make the reversal one quick, small adjustment instead of a big swing.",
                },
                new[] { "strafebots", "tracking" },
                F2($"you needed {reacq:0} ms to get back on after each direction change",
                    $"necesitaste {reacq:0} ms para volver a ponerte encima tras cada cambio de dirección"), unit: "direction changes");

        // ---- crosshair placement ----
        float xn = N(m, "xhair.n"), ep = V(m, "xhair.pitch_deg"), below = V(m, "xhair.below_pct"), eh = V(m, "xhair.h_deg");
        bool epLow = ep < -0.05f;
        if (ep < -1.5f || below >= 60)
            Add("XHAIR_LOW", Skill.CrosshairPlacement, ep < -3f ? 2 : 1, xn, 15,
                "Crosshair too low",
                epLow && !float.IsNaN(below) ? F2($"Enemies appeared {-ep:0.0}° above your crosshair on average (it was below the head in {below:0}% of sightings)",
                    $"Los enemigos aparecieron {-ep:0.0}° por encima de tu mira de media (estaba por debajo de la cabeza en el {below:0}% de los avistamientos)")
                    : epLow ? F2($"Enemies appeared {-ep:0.0}° above your crosshair on average", $"Los enemigos aparecieron {-ep:0.0}° por encima de tu mira de media")
                    : F2($"Your crosshair was below the head in {below:0}% of sightings", $"Tu mira estaba por debajo de la cabeza en el {below:0}% de los avistamientos"),
                "You're looking at the floor or body height, so every fight starts with a vertical flick.",
                new[]
                {
                    "Hold a head-height line and move the crosshair along it.",
                    "Use map details at head height (door frames, box edges) as reference points.",
                    "After every kill, reset the crosshair to head level — not down.",
                },
                new[] { "peek", "siteclear" },
                epLow ? F2($"enemies appeared {-ep:0.0}° above your crosshair", $"los enemigos aparecieron {-ep:0.0}° por encima de tu mira")
                    : F2($"your crosshair was below head height in {below:0}% of sightings", $"tu mira estaba por debajo de la altura de la cabeza en el {below:0}% de los avistamientos"),
                unit: "sightings");
        float bandE = Bench.ValueAt(refT, Bench.XhairErrDeg);
        if (eh > bandE)
            Add("XHAIR_WIDE", Skill.CrosshairPlacement, eh > 2 * bandE ? 2 : 1, N(m, "xhair.nh"), 15,
                "Not pre-aiming the angle",
                F2($"Enemies appeared {eh:0.0}° to the side of your crosshair (typical for {rankName}: {bandE:0.0}°)",
                    $"Los enemigos aparecieron a {eh:0.0}° de tu mira (típico en {rankName}: {bandE:0.0}°)"),
                "The crosshair is parked in open space instead of where enemies will appear.",
                new[]
                {
                    "Sit just off the cover edge where they'll appear.",
                    "Clear one angle at a time, keeping the crosshair glued to the edge.",
                    "When you move, slide the crosshair along the wall edge with you.",
                },
                new[] { "peekduel", "siteclear", "anchor" },
                F2($"enemies appeared {eh:0.0}° to the side of your crosshair", $"los enemigos aparecieron a {eh:0.0}° del lado de tu mira"), unit: "sightings");

        // ---- movement ----
        float mv = V(m, "move.moving_shot_pct"), early = V(m, "move.early_shot_pct"), mvSpeed = V(m, "move.moving_speed");
        bool moving = mv >= 15, earlyStop = early >= 25;
        if (moving || earlyStop)
            Add("RUN_GUN", Skill.Movement, mv >= 30 || early >= 30 ? 2 : 1, N(m, "move.shots"), 30,
                "Shooting while moving",
                (moving
                    ? (float.IsNaN(mvSpeed) ? F2($"{mv:0}% of your shots were fired while moving", $"El {mv:0}% de tus disparos se hicieron en movimiento")
                        : F2($"{mv:0}% of your shots were fired at {mvSpeed:0.0} m/s", $"El {mv:0}% de tus disparos se hicieron a {mvSpeed:0.0} m/s"))
                      + S(" (only shots under 1.35 m/s are accurate)", " (solo los disparos por debajo de 1,35 m/s son precisos)")
                    : "")
                + (earlyStop
                    ? (moving ? F2($"; {early:0}% within 0.1 s of starting to stop", $"; el {early:0}% en los 0,1 s posteriores a empezar a frenar")
                        : F2($"{early:0}% of your shots were fired within 0.1 s of starting to stop, before you were accurate",
                            $"El {early:0}% de tus disparos se hicieron en los 0,1 s posteriores a empezar a frenar, antes de ser preciso"))
                    : ""),
                "Moving bullets are wildly inaccurate in VALORANT — the first shot can land metres off.",
                new[]
                {
                    "Stop, then shoot: counter-strafe and click once you're still.",
                    "Don't hold a movement key while firing — tap the opposite key, then click.",
                },
                new[] { "counterstrafe", "peekduel" },
                moving ? F2($"{mv:0}% of your shots were fired while moving", $"El {mv:0}% de tus disparos se hicieron en movimiento")
                    : F2($"{early:0}% of your shots were fired before you had finished stopping", $"El {early:0}% de tus disparos se hicieron antes de terminar de frenar"),
                unit: "shots");
        float cs = V(m, "move.counter_pct"), stopMs = V(m, "move.stop_ms"), stopAdj = stopMs + Bench.StopOffsetMs;
        bool fewCounter = cs < 40, slowStop = stopAdj > 200;
        if (fewCounter || slowStop)
            Add("CLUMSY_MOVE", Skill.Movement, cs < 20 || stopAdj > 240 ? 2 : 1, N(m, "move.stops"), 20,
                "Clumsy stops",
                fewCounter
                    ? (cs < 0.5f ? S("None of your stops were counter-strafed", "Ninguna de tus paradas fue contramovimiento")
                        : F2($"Only {cs:0}% of stops were counter-strafed", $"Solo el {cs:0}% de tus paradas fueron contramovimiento"))
                      + (float.IsNaN(stopMs) ? "" : F2($"; average stop {stopMs:0} ms to accurate speed (best ≈ 105)",
                          $"; parada media de {stopMs:0} ms hasta velocidad precisa (mejor ≈ 105)"))
                    : F2($"Your stops took {stopMs:0} ms on average to reach accurate speed (best ≈ 105)",
                        $"Tus paradas tardaron {stopMs:0} ms de media en llegar a velocidad precisa (mejor ≈ 105)")
                      + (float.IsNaN(cs) ? "" : F2($", with {cs:0}% counter-strafed", $", con el {cs:0}% a contramovimiento")),
                "Releasing the keys and waiting to slow down wastes time every time you peek.",
                new[]
                {
                    "Tap the opposite key for about 80 ms, then release both.",
                    "Drill the rhythm: strafe, tap, shoot, strafe.",
                },
                new[] { "counterstrafe" },
                !fewCounter ? F2($"your stops took {stopMs:0} ms to reach accurate speed", $"tus paradas tardaron {stopMs:0} ms en llegar a velocidad precisa")
                    : cs < 0.5f ? S("none of your stops were counter-strafed", "ninguna de tus paradas fue contramovimiento")
                    : F2($"only {cs:0}% of your stops were counter-strafed", $"solo el {cs:0}% de tus paradas fueron contramovimiento"),
                unit: "stops");

        // ---- spray ----
        float sn = N(m, "spray.n"), sv = V(m, "spray.v"), svAbs = V(m, "spray.v_abs"), sh = V(m, "spray.h"), sd = V(m, "spray.delay_ms");
        bool vert = svAbs > 1.0f, latePull = sd > 300;
        if (vert || latePull)
        {
            bool high = float.IsNaN(sv) || sv >= 0;
            // Sprays that miss high and low in turn average out: report the typical size, not the (small) mean.
            bool mixed = vert && !float.IsNaN(sv) && MathF.Abs(sv) < 0.5f * svAbs;
            string where = mixed
                ? F2($"strayed {svAbs:0.0}° vertically — too high in some sprays, too low in others",
                    $"se desviaron {svAbs:0.0}° en vertical: demasiado altas en algunos sprays y demasiado bajas en otros")
                : F2($"landed {svAbs:0.0}° {(high ? "high" : "low")}", $"impactaron {svAbs:0.0}° {(high ? "altas" : "bajas")}");
            string[] highFix = { "Start pulling down by bullet 2–3, steadily — not one big late drag.", "Pull at a constant speed for the first second; the Vandal climbs about 5°/s.", "Watch the chart after each spray and match the grey pattern in reverse." };
            Add("SPRAY_VERT", Skill.SprayControl, svAbs > 1.8f ? 2 : 1, sn, 8,
                !vert ? "Late recoil compensation" : mixed ? "Inconsistent recoil control" : high ? "Spray climbs over the target" : "Over-pulling your spray",
                !vert ? F2($"You started pulling down {sd:0} ms into the spray (aim for under 300 ms)",
                    $"Empezaste a tirar hacia abajo {sd:0} ms dentro del spray (objetivo: menos de 300 ms)")
                    : (Es ? $"Las balas 4–15 {where}" : $"Bullets 4–15 {where}")
                      + (float.IsNaN(sd) ? "" : F2($"; you started pulling {sd:0} ms in", $"; empezaste a tirar a los {sd:0} ms")),
                !vert ? "You wait for the climb before reacting to it, so the first bullets after the opener already go high."
                    : mixed ? "Your pull changes from spray to spray — sometimes too little, sometimes too much — so the pattern never settles."
                    : high ? "You start pulling down too late or not hard enough, so the climb carries bullets 4+ over the target."
                    : "You pull down harder than the gun climbs, dragging the spray under the target.",
                mixed ? new[] { "Use the same pull every time: start by bullet 2–3 and keep a steady speed.", "Rest the crosshair on the target before each spray so every spray starts the same way.", "Watch the chart after each spray and match the grey pattern in reverse." }
                    : high || !vert ? highFix
                    : new[] { "Pull less: match the climb, don't beat it.", "Ease off the pull after bullet 10 — the climb flattens out.", "Watch the chart after each spray and match the grey pattern in reverse." },
                new[] { "spray_vandal", "spray_phantom" },
                !vert ? F2($"you started pulling down {sd:0} ms into each spray", $"empezaste a tirar hacia abajo {sd:0} ms dentro de cada spray")
                    : (Es ? $"las balas 4–15 de tus sprays {where}" : $"bullets 4–15 of your sprays {where}"),
                unit: "sprays");
        }
        if (sh > 0.9f)
            Add("SPRAY_HORIZ", Skill.SprayControl, sh > 1.4f ? 2 : 1, sn, 8,
                "Spray drifts sideways",
                F2($"Late bullets scattered {sh:0.0}° left-right", $"Las balas tardías se dispersaron {sh:0.0}° de izquierda a derecha"),
                "You're not countering the sideways drift that starts around bullet 8.",
                new[]
                {
                    "From bullet 8 on, watch which way the spray drifts and pull the opposite way.",
                    "Keep the counter-pull smooth — sudden sideways jerks make it worse.",
                },
                new[] { "spray_transfer", "spray_vandal" },
                F2($"your late bullets scattered {sh:0.0}° sideways", $"tus balas tardías se dispersaron {sh:0.0}° en horizontal"), unit: "sprays");

        // ---- utility ----
        float turn = V(m, "flash.turn_ms"), bandF = Bench.ValueAt(refT, Bench.FlashTurnMs);
        if (turn > bandF + 100)
            Add("FLASH_SLOW", Skill.Utility, turn > bandF + 250 ? 2 : 1, N(m, "flash.turn_n"), 8,
                "Late flash turns",
                F2($"You turned away {turn:0} ms after the cue (typical for {rankName}: {bandF:0} ms)",
                    $"Te giraste {turn:0} ms después de la señal (típico en {rankName}: {bandF:0} ms)"),
                "Waiting to see the flash instead of reacting to the throw sound.",
                new[]
                {
                    "Turn on the audio cue — every agent's flash has a distinct sound.",
                    "Turn at least 90° away, then swing back right after the pop.",
                },
                new[] { "flashmap" },
                F2($"you turned away from flashes {turn:0} ms after the cue", $"te giraste ante los flashes {turn:0} ms después de la señal"), unit: "flashes");

        a.Issues.Sort((x, y) => y.Importance.CompareTo(x.Importance));
    }

    // =====================================================================================================
    // Strengths
    // =====================================================================================================

    static void Strengths(Dictionary<string, float> m, Assessment a, bool runLevel, int tierPlayed)
    {
        float baseT = runLevel ? Math.Clamp(tierPlayed, 0, 4) : a.Overall >= 0 ? a.Overall : a.Provisional;
        if (float.IsNaN(baseT)) return;
        float minC = runLevel ? 0.2f : 0.5f;
        // Any issue (even a "possible" one) blocks praising the same skill, so a review never says both "slow micro-adjustments" and "clean micro-adjustments".
        var flagged = a.Issues.Select(i => i.D.Skill).ToHashSet();
        foreach (var r in a.Ratings.Values)
        {
            if (r.Tier < -0.9f || r.Confidence < minC || r.Tier < baseT + 0.5f || flagged.Contains(r.Skill)) continue;
            string rank = Loc.T(Bench.Rank(r.Tier));
            bool fast = V(m, "speed.tier") >= baseT || float.IsNaN(V(m, "speed.tier")) && !float.IsNaN(V(m, "flick.mt_ref_ms")) && Bench.TierOf(V(m, "flick.mt_ref_ms"), Bench.MtRefMs) >= baseT;
            float endErr = V(m, "flick.end_err_pct"), turnMs = V(m, "flash.turn_ms"), vAbs = V(m, "spray.v_abs");
            string text = r.Skill switch
            {
                Skill.Flicking => float.IsNaN(endErr) ? S($"Fast flicks ({rank} level).", $"Flicks rápidos (nivel {rank}).")
                    : fast ? F2($"Fast, accurate flicks ({rank} level): they land within {endErr:0}% of the distance.",
                        $"Flicks rápidos y precisos (nivel {rank}): aterrizan dentro del {endErr:0}% de la distancia.")
                    : F2($"Accurate flicks ({rank} level): they land within {endErr:0}% of the distance.",
                        $"Flicks precisos (nivel {rank}): aterrizan dentro del {endErr:0}% de la distancia."),
                Skill.Precision => float.IsNaN(V(m, "head.tc_ms"))
                    ? F2($"Precise first shots ({rank} level): {V(m, "head.fsh_pct"):0}% of them hit the head.",
                        $"Primeros disparos precisos (nivel {rank}): el {V(m, "head.fsh_pct"):0}% dio en la cabeza.")
                    : F2($"Clean micro-adjustments ({rank} level): you settle on the head in {V(m, "head.tc_ms"):0} ms.",
                        $"Microajustes limpios (nivel {rank}): te estabilizas en la cabeza en {V(m, "head.tc_ms"):0} ms."),
                Skill.Tracking => F2($"Smooth tracking ({rank} level): on target {V(m, "track.ontarget"):0}% of the time while firing.",
                    $"Tracking suave (nivel {rank}): sobre el objetivo el {V(m, "track.ontarget"):0}% del tiempo que disparas."),
                Skill.Reaction => F2($"Quick reactions ({rank} level): {V(m, "react.ms"):0} ms median.",
                    $"Reacciones rápidas (nivel {rank}): {V(m, "react.ms"):0} ms de mediana."),
                Skill.CrosshairPlacement => float.IsNaN(V(m, "xhair.err_deg"))
                    ? F2($"Great crosshair height ({rank} level): enemies appear within {MathF.Abs(V(m, "xhair.pitch_deg")):0.0}° of head level.",
                        $"Altura de mira excelente (nivel {rank}): los enemigos aparecen a {MathF.Abs(V(m, "xhair.pitch_deg")):0.0}° de la altura de la cabeza.")
                    : F2($"Great crosshair placement ({rank} level): enemies appear only {V(m, "xhair.err_deg"):0.0}° from your crosshair.",
                        $"Colocación de mira excelente (nivel {rank}): los enemigos aparecen a solo {V(m, "xhair.err_deg"):0.0}° de tu mira."),
                Skill.SprayControl => float.IsNaN(vAbs) ? S($"Good spray control ({rank} level).", $"Buen control de spray (nivel {rank}).")
                    : F2($"Good spray control ({rank} level): bullets 4–15 stay within {vAbs:0.0}° vertically.",
                        $"Buen control de spray (nivel {rank}): las balas 4–15 se mantienen dentro de {vAbs:0.0}° en vertical."),
                Skill.Movement => F2($"Disciplined movement ({rank} level): {V(m, "move.counter_pct"):0}% of stops counter-strafed.",
                    $"Movimiento disciplinado (nivel {rank}): el {V(m, "move.counter_pct"):0}% de las paradas fueron contramovimiento."),
                Skill.Utility => float.IsNaN(turnMs)
                    ? F2($"Sharp flash dodging ({rank} level): you beat {V(m, "flash.dodge_pct"):0}% of flashes.",
                        $"Esquiva de flashes precisa (nivel {rank}): superaste el {V(m, "flash.dodge_pct"):0}% de los flashes.")
                    : F2($"Sharp flash dodging ({rank} level): you turn away in {turnMs:0} ms.",
                        $"Esquiva de flashes precisa (nivel {rank}): te giras en {turnMs:0} ms."),
                _ => "",
            };
            a.Strengths.Add((r.Skill, r.Tier - baseT, text));
        }
        // Metric-level positives (useful for single-run reviews where few skills are rated).
        void Pos(string text, float score) { if (!a.Strengths.Any(s => s.Text == text)) a.Strengths.Add((Skill.Precision, score, text)); }
        float dwell = V(m, "flick.dwell_ms");
        if (dwell < 90 && N(m, "flick.n_hit") >= 8 && !flagged.Contains(Skill.Precision))
            Pos(F2($"Decisive clicks: you fire {dwell:0} ms after reaching the target.",
                $"Clics decisivos: disparas {dwell:0} ms después de llegar al objetivo."), 0.3f);
        float o = V(m, "flick.overshoot_share"), err = V(m, "flick.end_err_pct");
        if (err <= 10 && N(m, "flick.n_prim") >= 8 && !flagged.Contains(Skill.Flicking))
            Pos(F2($"Accurate first motions: your flicks land within {err:0}% of the distance.",
                $"Primeros movimientos precisos: tus flicks aterrizan dentro del {err:0}% de la distancia."), 0.25f);
        float fsh = HeadData(m) ? V(m, "head.fsh_pct") : float.NaN;
        if (fsh >= 85 && N(m, "head.n_first") >= 8)
            Pos(F2($"{fsh:0}% of your first shots hit the head.", $"El {fsh:0}% de tus primeros disparos dieron en la cabeza."), 0.3f);
        if (V(m, "xhair.pitch_deg") is var xp && MathF.Abs(xp) < 0.6f && N(m, "xhair.n") >= 5)
            Pos(F2($"Crosshair at head height: enemies appear within {MathF.Abs(xp):0.0}° vertically.",
                $"Mira a la altura de la cabeza: los enemigos aparecen a {MathF.Abs(xp):0.0}° en vertical."), 0.3f);
        a.Strengths.Sort((x, y) => y.Score.CompareTo(x.Score));
        _ = o;
    }
}
