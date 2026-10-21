using ValTrainer.Core;

namespace ValTrainer.Analysis;

/// <summary>One-paragraph, plain-English run review ("Your flicks are fast but 64% overshoot…").</summary>
static class RunSummary
{
    static float V(Dictionary<string, float> m, string k) => m.TryGetValue(k, out var v) ? v : float.NaN;
    static string F(FormattableString s) => FormattableString.Invariant(s);
    static bool Es => Loc.Spanish;
    static string S(string en, string es) => Es ? es : en;
    static string F2(FormattableString en, FormattableString es) => Es ? Comma(FormattableString.Invariant(es)) : FormattableString.Invariant(en);

    static string Comma(string s)
    {
        var a = s.ToCharArray();
        for (int i = 1; i + 1 < a.Length; i++)
            if (a[i] == '.' && char.IsDigit(a[i - 1]) && char.IsDigit(a[i + 1])) a[i] = ',';
        return new string(a);
    }

    /// <summary>Modes whose telemetry the coach analyses (everything else gets the "doesn't feed the coach" note).</summary>
    static readonly HashSet<string> CoachModes = new()
    {
        "flick", "spider", "gridshot", "tracking", "strafebots", "peek", "counterstrafe", "peekduel", "siteclear", "flashmap", "deathmatch",
        "reaction", "spray_vandal", "spray_phantom", "microshot", "popup", "longtaps", "jumppeek", "jigglepeek", "peek_mix",
        "flashpeek", "recon", "smokeexec", "mobility", "chamber", "postplant", "retake", "anchor", "sound",
        "switch", "operator", "spray_transfer",
    };

    /// <summary>Summary for a run without enough data for any finding: what this mode needs, or that it doesn't feed the coach.</summary>
    public static string NoData(string mode)
    {
        mode = RunAnalysis.Base(mode);
        if (!CoachModes.Contains(mode))
            return S("This drill doesn't feed the aim coach yet — your score and accuracy above are the measure. Head Flicks, Spidershot, Gridshot, Strafe Tracking, the bot drills and the spray drills all get a detailed review.",
                "Este drill todavía no alimenta al entrenador de puntería: tu puntuación y precisión de arriba son la medida. Flicks a la Cabeza, Spidershot, Gridshot, Strafe Tracking, los drills con bots y los de spray sí reciben una revisión detallada.");
        string need = mode switch
        {
            "flick" or "microshot" or "popup" => S("hit at least 5 targets", "impacta al menos 5 objetivos"),
            "longtaps" or "jumppeek" or "jigglepeek" or "peek_mix" or "chamber" or "sound" => S("let a few enemies show up and shoot them", "deja que aparezcan varios enemigos y dispárales"),
            "flashpeek" or "recon" or "smokeexec" or "mobility" or "postplant" or "retake" or "anchor" => S("play a few rounds and take some duels", "juega varias rondas y toma algunos duelos"),
            "spider" or "gridshot" => S("play at least 15 seconds and hit a few targets", "juega al menos 15 segundos e impacta varios objetivos"),
            "tracking" => S("hold fire on the target for at least a few seconds", "mantén el fuego sobre el objetivo durante varios segundos"),
            "strafebots" or "switch" => S("take on a few bots", "enfréntate a varios bots"),
            "operator" => S("let a few agents cross the gap while you hold it", "deja que varios agentes crucen el hueco mientras lo cubres"),
            "peek" => S("let a few bots peek you", "deja que varios bots se asomen"),
            "counterstrafe" => S("stop and shoot a few times", "detente y dispara varias veces"),
            "peekduel" or "siteclear" or "deathmatch" => S("get a few duels in", "completa algunos duelos"),
            "flashmap" => S("face a few flashes", "enfréntate a varios flashes"),
            "reaction" => S("finish the trials", "completa las pruebas"),
            _ => S("fire at least 10 bullets per spray a few times", "dispara al menos 10 balas por spray varias veces"), // spray_vandal / spray_phantom
        };
        return Es ? $"No hay datos suficientes en esta partida para una revisión: {need}." : $"Not enough data this run for a review — {need}.";
    }

    public static string Write(string mode, int tier, Dictionary<string, float> m, Assessment a)
    {
        string tierName = Loc.T(Bench.TierName(tier));
        var top = a.Issues.FirstOrDefault();
        var (lead, positive) = Lead(mode, tier, tierName, m, a, a.Issues.Count > 0, top?.D.Id);
        var parts = new List<string>();
        if (top != null)
        {
            string clause = top.Possible
                ? (Es ? $"parece que {top.Short} (aún no está confirmado; vigílalo)" : $"it looks like {top.Short} (not confirmed yet — keep an eye on it)")
                : top.Short;
            parts.Add(lead.Length > 0
                ? (Es ? $"{lead}{(positive ? ", pero " : "; además, ")}{clause}." : $"{lead}{(positive ? ", but " : "; also, ")}{clause}.")
                : Cap(clause) + ".");
            var fix = top.D.Fixes.FirstOrDefault(f => !f.StartsWith("Sensitivity"));
            if (fix != null) parts.Add(Loc.T(fix));
            if (a.Issues.Count > 1 && !a.Issues[1].Possible)
                parts.Add((Es ? "Siguiente en la lista: " : "Next on the list: ") + a.Issues[1].Short + ".");
        }
        else if (lead.Length == 0 && a.Ratings.Values.All(r => r.Tier < -0.9f))
            return NoData(mode);
        else
        {
            parts.Add(lead.Length > 0 ? $"{lead}." : S("Solid run.", "Partida sólida."));
            parts.Add(S("No clear problems this run — raise the tier or play longer runs for a sharper read.",
                "No hay problemas claros en esta partida: sube el nivel o juega partidas más largas para una lectura más precisa."));
        }
        if (a.Strengths.Count > 0)
            parts.Add((Es ? "Lo bueno: " : "On the plus side: ") + Uncap(Loc.T(a.Strengths[0].Text)));
        return string.Join(" ", parts);
    }

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    static string Uncap(string s) => s.Length > 1 && char.IsUpper(s[0]) && !char.IsUpper(s[1]) ? char.ToLowerInvariant(s[0]) + s[1..] : s;

    static string Pace(float t, int tier) => t >= tier + 0.5f ? S("fast", "rápidos") : t >= tier - 0.5f ? S("solid", "sólidos") : S("slow", "lentos");
    static string PaceF(float t, int tier) => t >= tier + 0.5f ? S("fast", "rápida") : t >= tier - 0.5f ? S("solid", "sólida") : S("slow", "lenta");

    static (string Text, bool Positive) Lead(string mode, int tier, string tierName, Dictionary<string, float> m, Assessment a, bool issue, string? topId)
    {
        float sp = V(m, "speed.tier");
        switch (mode)
        {
            case "flick":
            {
                float ttk = V(m, "flick.ttk_hit_ms");
                if (float.IsNaN(ttk)) break;
                string pace = float.IsNaN(sp) ? "" : Pace(sp, tier);
                string rank = Loc.T(Bench.Rank(sp));
                return (F2($"Your flicks were {(pace.Length > 0 ? pace : "measured")} — {ttk:0} ms median to hit{(float.IsNaN(sp) ? "" : $" ({rank} pace on {tierName})")}",
                    $"Tus flicks fueron {(pace.Length > 0 ? pace : "medidos")}: {ttk:0} ms de mediana hasta impactar{(float.IsNaN(sp) ? "" : $" (ritmo {rank} en {tierName})")}"), sp >= tier - 0.5f);
            }
            case "spider":
            case "gridshot":
            {
                float spm = V(m, "speed.score_min");
                if (float.IsNaN(sp) || float.IsNaN(spm)) break;
                string rank = Loc.T(Bench.Rank(sp));
                return (F2($"Your speed was {PaceF(sp, tier)} — {spm:0} points/min ({rank} level on {tierName})",
                    $"Tu velocidad fue {PaceF(sp, tier)}: {spm:0} puntos/min (nivel {rank} en {tierName})"), sp >= tier - 0.5f);
            }
            case "tracking":
            {
                float on = V(m, "track.ontarget"), t = a.Ratings.TryGetValue(Skill.Tracking, out var r) ? r.Tier : float.NaN;
                if (float.IsNaN(on)) break;
                string rank = Loc.T(Bench.Rank(V(m, "track.ontarget_tier")));
                return (F2($"You were on target {on:0}% of the time you were firing ({rank} level on {tierName})",
                    $"Estuviste sobre el objetivo el {on:0}% del tiempo que disparaste (nivel {rank} en {tierName})"), !float.IsNaN(t) && t >= tier - 0.5f);
            }
            case "spray_vandal":
            case "spray_phantom":
            {
                float v = V(m, "spray.v"), h = V(m, "spray.h"), n = V(m, "spray.n");
                if (float.IsNaN(v)) break;
                float t = a.Ratings.TryGetValue(Skill.SprayControl, out var r) ? r.Tier : float.NaN;
                string rank = Loc.T(Bench.Rank(t));
                if (issue) return (F2($"Over {n:0} sprays your recoil control was {rank} level",
                    $"En {n:0} sprays, tu control del retroceso fue de nivel {rank}"), t >= tier - 0.5f);
                return (F2($"Over {n:0} sprays, bullets 4–15 landed {MathF.Abs(v):0.0}° {(v >= 0 ? "high" : "low")} with {h:0.0}° of sideways scatter ({rank} level)",
                    $"En {n:0} sprays, las balas 4–15 impactaron {MathF.Abs(v):0.0}° {(v >= 0 ? "altas" : "bajas")} con {h:0.0}° de dispersión lateral (nivel {rank})"), t >= tier - 0.5f);
            }
            case "reaction":
            {
                float ms = V(m, "react.ms");
                if (float.IsNaN(ms)) break;
                float t = Bench.TierOf(ms, Bench.ReactMs);
                string rank = Loc.T(Bench.Rank(t));
                return (F2($"Your median reaction was {ms:0} ms over {V(m, "react.n"):0} trials ({rank} level)",
                    $"Tu reacción mediana fue {ms:0} ms en {V(m, "react.n"):0} pruebas (nivel {rank})"), t >= 1);
            }
            case "flashmap":
            {
                float d = V(m, "flash.dodge_pct"), turn = V(m, "flash.turn_ms");
                if (float.IsNaN(d)) break;
                return (F2($"You beat {d:0}% of the flashes", $"Esquivaste el {d:0}% de los flashes")
                    + (float.IsNaN(turn) ? "" : F2($" and turned away in {turn:0} ms on average", $" y te giraste en {turn:0} ms de media")), d >= 60);
            }
        }
        // Bot / movement modes and fallbacks (skipping the fact the top issue is about, so it isn't said twice).
        var bits = new List<string>();
        float fsh = V(m, "head.fsh_pct"), nf = V(m, "head.n_first");
        if (!float.IsNaN(fsh) && nf >= 3) bits.Add(F2($"{fsh:0}% of your first shots hit the head", $"{fsh:0}% de tus primeros disparos dieron en la cabeza"));
        float xe = V(m, "xhair.err_deg");
        if (!float.IsNaN(xe) && topId is not ("XHAIR_WIDE" or "XHAIR_LOW")) bits.Add(F2($"enemies appeared {xe:0.0}° from your crosshair", $"los enemigos aparecieron a {xe:0.0}° de tu mira"));
        float mv = V(m, "move.moving_shot_pct");
        if (!float.IsNaN(mv) && topId != "RUN_GUN") bits.Add(F2($"{mv:0}% of shots were fired while moving", $"{mv:0}% de los disparos fueron en movimiento"));
        float cs = V(m, "move.counter_pct");
        if (!float.IsNaN(cs) && topId != "CLUMSY_MOVE") bits.Add(F2($"{cs:0}% of stops were counter-strafed", $"{cs:0}% de las paradas fueron contramovimiento"));
        if (bits.Count == 0) return ("", false);
        // "…, but <problem>" only when the facts listed are good ones; otherwise "…; also, <problem>".
        bool good = !(fsh < Bench.ValueAt(tier, Bench.FirstShotHitPct) && nf >= 3) && !(xe > Bench.ValueAt(tier, Bench.XhairErrDeg))
                    && !(mv >= 15) && !(cs < 40);
        return (Cap(string.Join(", ", bits.Take(3))), good);
    }
}
