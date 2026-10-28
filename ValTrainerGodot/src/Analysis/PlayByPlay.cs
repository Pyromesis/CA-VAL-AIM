using ValTrainer.Core;
using ValTrainer.Game;

namespace ValTrainer.Analysis;

/// <summary>How one play went, from best to worst.</summary>
public enum PlayGrade { Excellent, Good, Fair, Poor, Bad }

/// <summary>
/// One "play" of a run: a single engagement, spray, reaction, flash, round, stop or tracking window, with the same
/// things a coach would say about it — what happened, the numbers behind it, what to change next time and which drill
/// trains it. Plays are ordered by time, so the screen reads like a match review.
/// </summary>
public sealed class Play
{
    public int Index;
    public float T0, T1;
    /// <summary>engagement · spray · reaction · flash · round · stop · track · kill</summary>
    public string Kind = "";
    public string Title = "";
    public string Detail = "";
    public string Advice = "";
    public string[] Drills = Array.Empty<string>();
    public Skill Skill = Skill.Flicking;
    /// <summary>Stable reason code for the run verdict ("clean", "over", "under", "slow", …).</summary>
    public string Reason = "clean";
    public int Score;

    public PlayGrade Grade => Score >= 85 ? PlayGrade.Excellent : Score >= 70 ? PlayGrade.Good
        : Score >= 55 ? PlayGrade.Fair : Score >= 40 ? PlayGrade.Poor : PlayGrade.Bad;
    /// <summary>S / A / B / C / D, the chip shown on the card.</summary>
    public string Letter => Grade switch
    {
        PlayGrade.Excellent => "S", PlayGrade.Good => "A", PlayGrade.Fair => "B", PlayGrade.Poor => "C", _ => "D",
    };
}

/// <summary>Everything the play-by-play screen shows for one run.</summary>
public sealed class PlayAnalysis
{
    public string Mode = "";
    /// <summary>What the plays are: "engagements", "sprays", "reactions", "flashes", "rounds", "stops", "tracking", "kills".</summary>
    public string Kind = "";
    public readonly List<Play> Plays = new();
    public string Verdict = "";
    public int Excellent, Good, Fair, Poor;
    public float Average;
    /// <summary>Drills for the most repeated mistake (empty when the run was clean).</summary>
    public string[] Drills = Array.Empty<string>();
    public bool HasData => Plays.Count > 0;
}

/// <summary>
/// Per-play analysis: the run-level metrics say <em>what</em> is wrong, this says <em>when</em> and <em>why</em>, play
/// by play, in the words a coach would use. It reuses the same trials / sprays the metrics are built from
/// (<see cref="RunAnalysis"/>), so a play's numbers always match the review that points at it.
/// </summary>
public static class PlayByPlay
{
    static bool Es => Loc.Spanish;
    static string S(string en, string es) => Es ? es : en;
    static string F2(FormattableString en, FormattableString es) =>
        Es ? Comma(FormattableString.Invariant(es)) : FormattableString.Invariant(en);
    static string F(FormattableString s) => FormattableString.Invariant(s);

    /// <summary>Rounded number with a decimal comma in Spanish (the app runs with an invariant culture).</summary>
    static string N(float v, string fmt = "0")
    {
        string s = v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
        return Es ? Comma(s) : s;
    }

    static string Comma(string s)
    {
        var a = s.ToCharArray();
        for (int i = 1; i + 1 < a.Length; i++)
            if (a[i] == '.' && char.IsDigit(a[i - 1]) && char.IsDigit(a[i + 1])) a[i] = ',';
        return new string(a);
    }

    /// <summary>Plays a run into a review. Never throws: a mode with no per-instance data gets an empty analysis.</summary>
    public static PlayAnalysis Analyze(RunTelemetry t)
    {
        var a = new PlayAnalysis { Mode = t.Mode };
        try
        {
            Gather(t, a);
        }
        catch (Exception e)
        {
            Log.Error($"Play-by-play failed for {t.Mode}: {e.Message}");
            a.Plays.Clear();
        }
        Finish(a);
        return a;
    }

    // =====================================================================================================
    // Which plays this mode has
    // =====================================================================================================

    static void Gather(RunTelemetry t, PlayAnalysis a)
    {
        string mode = RunAnalysis.Base(t.Mode);
        var prep = RunAnalysis.Prepare(t);

        // Discrete targets first: an engagement is the play, and it is what every aim drill is about.
        if (prep != null && EngagementModes.Contains(mode))
            AddEngagements(t, prep, a);
        if (a.Plays.Count >= 2) { a.Kind = "engagements"; return; }

        if (prep != null && SprayModes.Contains(mode)) AddSprays(t, prep, a);
        if (a.Plays.Count >= 2) { a.Kind = "sprays"; return; }

        if (ReactionEvents(t) is { Count: >= 2 } reactions) { a.Kind = "reactions"; AddReactions(t, reactions, a); return; }

        if (FlashEvents(t) is { Count: >= 2 } flashes) { a.Kind = "flashes"; AddFlashes(flashes, a); return; }

        if (Rounds(t) is { Count: >= 2 } rounds) { a.Kind = "rounds"; AddRounds(t, rounds, a); return; }

        if (prep != null && TrackingModes.Contains(mode) && AddTracking(t, a)) { a.Kind = "tracking"; return; }

        if (Stops(t) is { Count: >= 4 } stops) { a.Kind = "stops"; AddStops(stops, a); return; }

        // Last resort: kills (and deaths) as they come, grouped in short windows.
        AddKills(t, a);
        a.Kind = "kills";
    }

    static readonly HashSet<string> EngagementModes = new()
    {
        "flick", "spider", "gridshot", "microshot", "popup", "flickx", "microx", "reflex",
        "strafebots", "counterstrafe", "switch", "peek", "peekduel", "siteclear", "deathmatch",
        "longtaps", "jumppeek", "jigglepeek", "peek_mix", "flashpeek", "recon", "smokeexec",
        "mobility", "chamber", "postplant", "retake", "anchor", "sound", "arsenal",
    };

    static readonly HashSet<string> SprayModes = new() { "spray_vandal", "spray_phantom", "spray_transfer" };
    static readonly HashSet<string> TrackingModes = new() { "tracking", "airtrack", "reacttrack", "trackv" };

    // =====================================================================================================
    // Engagements (one target at a time)
    // =====================================================================================================

    static void AddEngagements(RunTelemetry t, Prep p, PlayAnalysis a)
    {
        bool micro = MicroModes.Contains(RunAnalysis.Base(t.Mode));
        var trials = RunAnalysis.Trials(p);
        foreach (var (tr, r) in trials)
        {
            if (tr.T0 < p.Ft[0] - 0.5f) continue;
            var play = r != null ? FlickPlay(t, tr, r) : PreAimedPlay(t, p, tr, micro);
            if (play != null) a.Plays.Add(play);
        }
    }

    static Play? FlickPlay(RunTelemetry t, RunAnalysis.Trial tr, RunAnalysis.TrialResult r)
    {
        float end = r.Hit ? r.Ttk is > 0 ? tr.T0 + r.Ttk / 1000f : tr.T0 : tr.TEnd;
        if (end <= tr.T0) end = tr.T0 + 0.05f;
        var play = new Play
        {
            T0 = tr.T0,
            T1 = MathF.Max(end, tr.TEnd > tr.T0 ? MathF.Min(tr.TEnd, tr.T0 + 8f) : end),
            Kind = "engagement",
            Skill = r.Head ? Skill.Precision : Skill.Flicking,
            Drills = r.Head ? new[] { "microshot", "flick" } : new[] { "flick", "gridshot" },
        };

        float ms = r.Hit ? r.Ttk : (tr.TEnd - tr.T0) * 1000f;
        float expected = 190f + 7.5f * MathF.Max(3f, r.A);
        bool moving = ShotSpeed(t, tr, r) > 1.35f;
        float overDeg = MathF.Max(0f, r.S - MathF.Max(0.05f, r.R));
        float underDeg = MathF.Max(0f, -r.S - MathF.Max(0.05f, r.R));
        bool died = DiedIn(t, tr.T0, tr.TEnd + 0.3f);

        int score = 50;
        score += r.Hit ? 22 : -18;
        if (r.Hit)
        {
            if (ms <= expected * 0.7f) score += 14;
            else if (ms <= expected) score += 9;
            else if (ms <= expected * 1.5f) score += 1;
            else score -= 9;
        }
        if (r.Over) score -= 15;
        else if (r.Under) score -= 12;
        if (r.HasPrimary) score += 4;
        if (r.Nc == 0) score += 6;
        else if (r.Nc == 1) score += 2;
        else score -= Math.Min(12, r.Nc * 3);
        if (r.HasFirstShot) score += r.FirstHit ? 9 : -9;
        if (r.Misses > 0) score -= Math.Min(12, r.Misses * 4);
        if (moving) score -= 8;
        if (died) score -= 10;
        if (r.Head && r.Hit) score += 4;
        if (r.Hit && r.Tc > 160) score -= r.Tc > 320 ? 8 : 5; // on target but waiting is a real cost
        play.Score = Math.Clamp(score, 0, 100);

        string shot = r.HasFirstShot
            ? (r.FirstHit ? S("first shot on target", "primer disparo al objetivo") : S("first shot missed", "primer disparo fallado"))
            : S("no shot recorded", "sin disparo registrado");
        play.Title = S($"Flick {r.A:0}° · {ms:0} ms", $"Flick de {r.A:0}° · {ms:0} ms");
        var parts = new List<string>
        {
            S($"target {N(r.R, "0.0")}°", $"objetivo de {N(r.R, "0.0")}°"),
            S($"{r.Nc} {(r.Nc == 1 ? "correction" : "corrections")}", $"{r.Nc} {(r.Nc == 1 ? "corrección" : "correcciones")}"),
            shot,
        };
        if (!r.Hit) parts.Add(S("didn't connect", "no conectó"));
        if (r.Over) parts.Add(S($"{N(overDeg, "0.0")}° too far", $"{N(overDeg, "0.0")}° de más"));
        if (r.Under) parts.Add(S($"{N(underDeg, "0.0")}° short", $"{N(underDeg, "0.0")}° corto"));
        if (moving) parts.Add(S("shot while moving", "disparo en movimiento"));
        if (died) parts.Add(S("you died", "te mataron"));
        play.Detail = string.Join(" · ", parts);

        // ----- the advice: one concrete cue, chosen by what cost the most points
        if (moving)
        {
            play.Reason = "moving";
            play.Drills = new[] { "counterstrafe", "strafebots" };
            play.Advice = S(
                "You fired while your feet were still moving. Release A/D a split second earlier and let the counter-strafe stop you: the first bullet is only accurate once the speed is at zero.",
                "Disparaste con los pies en movimiento. Suelta A/D un instante antes y deja que el counter-strafe te frene: la primera bala solo sale recta con la velocidad a cero.");
        }
        else if (!r.Hit && r.Over)
        {
            play.Reason = "over";
            play.Advice = S(
                $"You went {overDeg:0.0}° past the target. Stop the wrist earlier: move fast to just in front of the target and let the last small correction land it. If you keep pushing, every flick ends up long.",
                $"Te pasaste {N(overDeg, "0.0")}°. Frena la muñeca antes: llega rápido hasta justo delante del objetivo y deja que la última corrección lo cierre. Si sigues empujando, todos los flicks se te van largos.");
        }
        else if (!r.Hit && r.Under)
        {
            play.Reason = "under";
            play.Advice = S(
                $"You stopped {underDeg:0.0}° short. Commit to the full distance and don't brake before the target: the small correction is for the end, not for the middle of the flick.",
                $"Te quedaste {N(underDeg, "0.0")}° corto. Comprométete con la distancia completa y no frenes antes del objetivo: la corrección pequeña es para el final, no para la mitad del flick.");
        }
        else if (!r.Hit)
        {
            play.Reason = "miss";
            play.Advice = S(
                "The flick didn't finish on the target. Start with one clean movement towards it (not several small pushes), and only fire when the dot is inside the head — not when your hand stops.",
                "El flick no terminó sobre el objetivo. Haz un solo movimiento limpio hacia él (no varios empujes pequeños) y dispara solo cuando el punto esté dentro de la cabeza, no cuando pare la mano.");
        }
        else if (r.Nc >= 2)
        {
            play.Reason = "corrections";
            play.Drills = new[] { "microshot", "flick" };
            play.Advice = S(
                $"It took {r.Nc} corrections after the main flick. Trust the first movement: arrive with a little margin and let the eyes verify, instead of adding micro-flicks that cost {ms - r.Mt:0} ms.",
                $"Necesitaste {r.Nc} correcciones después del flick principal. Confía en el primer movimiento: llega con un poco de margen y deja que los ojos verifiquen, en vez de añadir micro-flicks que te cuestan {N(ms - r.Mt)} ms.");
        }
        else if (r.Hit && r.Tc > 160)
        {
            play.Reason = "float";
            play.Advice = S(
                $"You were on the target but waited {r.Tc:0} ms before firing. The shot must leave as soon as the dot is inside: waiting lets the target move or punish you.",
                $"Estabas sobre el objetivo pero esperaste {N(r.Tc)} ms antes de disparar. El tiro debe salir en cuanto el punto está dentro: esperar deja que el objetivo se mueva o te castigue.");
        }
        else if (r.Hit && ms > expected * 1.4f)
        {
            play.Reason = "slow";
            play.Drills = new[] { "flick", "popup" };
            play.Advice = S(
                $"The flick itself was clean but slow: {ms:0} ms for {r.A:0}° (a good reference is around {expected:0} ms). Shorten the first movement and start it as soon as the target appears.",
                $"El flick fue limpio pero lento: {N(ms)} ms para {N(r.A)}° (una referencia buena ronda {N(expected)} ms). Acorta el primer movimiento y empiezalo en cuanto aparezca el objetivo.");
        }
        else if (r.HasFirstShot && !r.FirstHit)
        {
            play.Reason = "firstmiss";
            play.Advice = S(
                "The first bullet left before the crosshair was on the target. Add a beat: see the dot inside the head, then click. One well-timed bullet beats three rushed ones.",
                "La primera bala salió antes de que la mira estuviera en el objetivo. Añade un tiempo: ve el punto dentro de la cabeza y luego haz clic. Una bala bien timed vale más que tres precipitadas.");
        }
        else
        {
            play.Reason = "clean";
            play.Advice = S(
                r.Head
                    ? "Clean headshot: one movement, one shot. Remember this feeling — this is the tempo to repeat."
                    : "Clean and decisive: one movement, no extra corrections. Repeat that exact speed.",
                r.Head
                    ? "Headshot limpio: un movimiento, un disparo. Guarda esta sensación: ese es el tempo que hay que repetir."
                    : "Limpio y decidido: un movimiento, sin correcciones extra. Repite exactamente esa velocidad.");
        }
        return play;
    }

    /// <summary>Targets that spawn close to the crosshair (micro-corrections): fast is good, but the drills differ.</summary>
    static readonly HashSet<string> MicroModes = new() { "microshot", "microx", "reflex" };

    static Play? PreAimedPlay(RunTelemetry t, Prep p, RunAnalysis.Trial tr, bool micro)
    {
        float hit = tr.THit >= 0 ? MathF.Max(0f, tr.THit - tr.T0) * 1000f : -1f;
        float end = tr.TEnd > tr.T0 ? tr.TEnd : tr.T0 + 0.05f;
        bool died = DiedIn(t, tr.T0, tr.TEnd + 0.3f);
        // Where the target appeared relative to the crosshair: a trial without a measurable primary flick is not
        // necessarily "pre-aimed" — it can be a slow close on a target that appeared well off the dot.
        float d0 = 0;
        try
        {
            var (cy, cp) = p.CrossAt(tr.T0);
            float dy = cy - tr.AbsYaw, dp = cp - tr.AbsPitch;
            d0 = MathF.Sqrt(dy * dy + dp * dp);
        }
        catch { /* no frame at that time: keep 0 */ }
        bool pre = d0 <= 1.2f;
        var play = new Play
        {
            T0 = tr.T0, T1 = MathF.Min(end, tr.T0 + 8f), Kind = "engagement",
            Skill = pre ? Skill.CrosshairPlacement : Skill.Flicking,
            Drills = micro ? new[] { "microshot", "microx" } : pre ? new[] { "peek", "deathmatch" } : new[] { "flick", "gridshot" },
        };
        if (!tr.Hit)
        {
            play.Score = 38;
            play.Reason = "notplaced";
            play.Title = micro ? S("Target lost", "Objetivo perdido") : S("Duel without a clean shot", "Duelo sin disparo limpio");
            play.Detail = S("the target left before you connected", "el objetivo se fue antes de que conectaras");
            play.Advice = micro
                ? S($"You had it {d0:0.0}° away and it left. Slow the hand down: a micro-correction is a short, quiet movement — most misses are the wrist overshooting a 2° target.",
                    $"Lo tenías a {N(d0, "0.0")}° y se fue. Baja la velocidad de la mano: un micro-ajuste es un movimiento corto y silencioso; casi todos los fallos son la muñeca pasándose en un objetivo de 2°.")
                : S("You saw it but didn't finish. Pre-aim at head height and keep the crosshair at the next angle while you move, so the shot is only a click away.",
                    "Lo viste pero no lo cerraste. Pre-apunta a la altura de la cabeza y mantén la mira en el siguiente ángulo mientras te mueves: así el disparo está a un clic.");
            return play;
        }

        if (micro)
        {
            play.Score = Math.Clamp(hit switch
            {
                < 220 => 92, < 300 => 84, < 400 => 74, < 550 => 60, < 800 => 48, _ => 38,
            } - (died ? 10 : 0), 0, 100);
            play.Reason = play.Score >= 85 ? "clean" : hit > 500 ? "slow" : "placed";
            play.Title = S($"Micro-adjust · {hit:0} ms", $"Micro-ajuste · {N(hit)} ms");
            play.Detail = S(
                $"{d0:0.0}° off the crosshair when it appeared{(died ? " · you died" : "")}",
                $"{N(d0, "0.0")}° de la mira al aparecer{(died ? " · te mataron" : "")}");
            play.Advice = hit < 350
                ? S("Short, clean correction: the hand moved just enough. That is the touch these drills train — keep the grip relaxed so the small movement stays small.",
                    "Corrección corta y limpia: la mano se movió justo lo necesario. Ese es el tacto que entrenan estos ejercicios; mantén la mano relajada para que el movimiento pequeño siga siendo pequeño.")
                : S($"It took {hit:0} ms for a target that was only {d0:0.0}° away. Don't aim with the whole arm here: move the wrist in one short push and click as soon as the dot touches the target.",
                    $"Tardaste {N(hit)} ms en un objetivo que estaba a solo {N(d0, "0.0")}°. Aquí no apuntes con todo el brazo: mueve la muñeca en un empuje corto y haz clic en cuanto el punto toque el objetivo.");
            return play;
        }

        play.Score = Math.Clamp(hit switch
        {
            >= 0 and < 220 => 90, >= 0 and < 350 => 80, >= 0 and < 500 => 70, >= 0 and < 800 => 58, _ => 48,
        } - (died ? 10 : 0), 0, 100);
        play.Reason = play.Score >= 85 ? "clean" : hit > 500 ? "slow" : "placed";
        play.Title = pre
            ? S($"Pre-aimed duel · {hit:0} ms", $"Duelo con pre-aim · {N(hit)} ms")
            : S($"Target {d0:0.0}° away · {hit:0} ms", $"Objetivo a {N(d0, "0.0")}° · {N(hit)} ms");
        play.Detail = pre
            ? S($"you were already on the target when it appeared{(died ? " · you died" : "")}",
                $"ya estabas sobre el objetivo cuando apareció{(died ? " · te mataron" : "")}")
            : S($"it appeared {d0:0.0}° off your crosshair{(died ? " · you died" : "")}",
                $"apareció a {N(d0, "0.0")}° de tu mira{(died ? " · te mataron" : "")}");
        play.Advice = pre
            ? hit <= 350
                ? S("The crosshair was already in the right place: that is real crosshair placement. Keep reading angles before you swing.",
                    "La mira ya estaba donde tenía que estar: eso es colocación real. Sigue leyendo los ángulos antes de asomarte.")
                : S($"You were placed well but took {hit:0} ms from seeing it to shooting. Decide before you peek: see the head, click — don't wait to be sure.",
                    $"Estabas bien colocado pero tardaste {N(hit)} ms desde verlo hasta disparar. Decide antes de asomarte: ve la cabeza y haz clic; no esperes a estar seguro.")
            : hit <= 350
                ? S($"It appeared {d0:0.0}° away and you closed it in {hit:0} ms without a measurable flick: a clean, direct first movement. Repeat that.",
                    $"Apareció a {N(d0, "0.0")}° y lo cerraste en {N(hit)} ms sin un flick medible: un primer movimiento limpio y directo. Repítelo.")
                : S($"The target appeared {d0:0.0}° away and took {hit:0} ms. Make the first movement decisive: arrive fast with one push and correct only at the end, instead of creeping towards it.",
                    $"El objetivo apareció a {N(d0, "0.0")}° y tardaste {N(hit)} ms. Haz el primer movimiento decidido: llega rápido con un solo empuje y corrige solo al final, en vez de acercarte a tirones.");
        return play;
    }

    /// <summary>Speed of the first shot of the engagement (m/s), 0 when unknown.</summary>
    static float ShotSpeed(RunTelemetry t, RunAnalysis.Trial tr, RunAnalysis.TrialResult? r)
    {
        float from = tr.T0, to = r is { Hit: true } ? tr.T0 + MathF.Max(0.05f, r.Ttk / 1000f) : tr.TEnd;
        foreach (var s in t.Shots)
            if (s.T > from - 1e-4f && s.T <= to + 1e-4f) return s.Speed;
        return 0;
    }

    static bool DiedIn(RunTelemetry t, float t0, float t1) =>
        t.Events.Any(e => e.Kind == "died" && e.T >= t0 - 0.1f && e.T <= t1 + 0.1f);

    // =====================================================================================================
    // Sprays
    // =====================================================================================================

    static void AddSprays(RunTelemetry t, Prep p, PlayAnalysis a)
    {
        var sprays = RunAnalysis.Sprays(p);
        foreach (var s in sprays)
        {
            var play = new Play
            {
                T0 = s.T0, T1 = MathF.Max(s.T1, s.T0 + 0.2f), Kind = "spray", Skill = Skill.SprayControl,
                Drills = new[] { "spray_vandal", "spray_phantom" },
            };
            float v = s.V, h = s.H;
            int score = 55;
            if (MathF.Abs(v) <= 0.8f) score += 25;
            else if (MathF.Abs(v) <= 1.5f) score += 12;
            else if (MathF.Abs(v) <= 2.5f) score += 1;
            else score -= 16;
            if (h <= 0.6f) score += 15;
            else if (h <= 1.2f) score += 5;
            else if (h <= 2f) score -= 4;
            else score -= 12;
            play.Score = Math.Clamp(score, 0, 100);
            play.Title = S($"{s.Bullets}-bullet spray", $"Spray de {s.Bullets} balas");
            play.Detail = S(
                $"vertical {Signed(v)}° ({(v > 0 ? "high" : "low")}) · horizontal {N(h, "0.0")}°",
                $"vertical {Signed(v)}° ({(v > 0 ? "alto" : "bajo")}) · horizontal {N(h, "0.0")}°");
            if (MathF.Abs(v) > 1.5f)
            {
                play.Reason = v > 0 ? "spray_high" : "spray_low";
                play.Advice = v > 0
                    ? S($"Your spray finished {v:0.0}° too high. Pull down sooner and a little harder after the first four bullets: start the pull as the recoil begins, not when you see the dot rise.",
                        $"Tu spray terminó {N(v, "0.0")}° por encima. Baja antes y un poco más fuerte después de las cuatro primeras balas: empieza el arrastre cuando arranca el retroceso, no cuando ves subir el punto.")
                    : S($"Your spray finished {-v:0.0}° too low. Ease the pull: you are over-correcting downwards and the last bullets fall below the target.",
                        $"Tu spray terminó {N(-v, "0.0")}° por debajo. Suaviza el arrastre: estás corrigiendo de más hacia abajo y las últimas balas caen bajo el objetivo.");
            }
            else if (h > 1.2f)
            {
                play.Reason = "spray_side";
                play.Advice = S($"The spray drifted {h:0.0}° sideways. Keep the forearm still and pull straight down: sideways drift comes from the wrist rotating while you compensate.",
                    $"El spray se fue {N(h, "0.0")}° de lado. Mantén el antebrazo quieto y arrastra recto hacia abajo: el desvío lateral viene de girar la muñeca mientras compensas.");
            }
            else
            {
                play.Reason = "clean";
                play.Advice = S("Tight spray: the pattern stayed on the target. Keep that pull speed as your reference.",
                    "Spray cerrado: el patrón se quedó sobre el objetivo. Usa esa velocidad de arrastre como referencia.");
            }
            a.Plays.Add(play);
        }
    }

    static string Signed(float v)
    {
        string s = MathF.Abs(v) < 0.05f ? "0.0" : F($"{(v > 0 ? "+" : "")}{v:0.0}");
        return Es ? Comma(s) : s;
    }

    // =====================================================================================================
    // Reactions
    // =====================================================================================================

    static List<TelemetryEvent>? ReactionEvents(RunTelemetry t)
    {
        var list = t.Events.Where(e => e.Kind is "reaction" or "early_click" or "false_alarm").ToList();
        return list.Count > 0 ? list : null;
    }

    static void AddReactions(RunTelemetry t, List<TelemetryEvent> events, PlayAnalysis a)
    {
        foreach (var e in events)
        {
            var play = new Play { T0 = MathF.Max(0, e.T - 0.4f), T1 = e.T, Kind = "reaction", Skill = Skill.Reaction, Drills = new[] { "reaction" } };
            if (e.Kind != "reaction")
            {
                play.Score = 20;
                play.Reason = e.Kind == "false_alarm" ? "false_alarm" : "early";
                play.Title = e.Kind == "false_alarm" ? S("False start", "Salida en falso") : S("Early click", "Clic anticipado");
                play.Detail = e.Kind == "false_alarm"
                    ? S("you clicked on a target you shouldn't have", "hiciste clic en un objetivo que no tocaba")
                    : S("under 90 ms: a guess, not a reaction", "menos de 90 ms: una apuesta, no una reacción");
                play.Advice = S("Don't chase the clock: wait for the target to actually appear. A fast guess costs the run.",
                    "No persigas el reloj: espera a que el objetivo aparezca de verdad. Una apuesta rápida te cuesta la partida.");
            }
            else
            {
                float ms = e.A;
                play.Score = ms switch { < 180 => 96, < 220 => 88, < 260 => 76, < 320 => 60, < 400 => 45, _ => 30 };
                play.Reason = ms < 220 ? "clean" : ms < 320 ? "react_ok" : "react_slow";
                play.Title = S($"Reaction · {ms:0} ms", $"Reacción · {N(ms)} ms");
                play.Detail = ms < 220 ? S("elite range", "rango élite") : ms < 260 ? S("solid", "sólido") : ms < 320 ? S("average", "normal") : S("slow", "lento");
                play.Advice = ms < 220
                    ? S("Keep this: your eyes are leading. Don't start predicting to go faster.", "Mantén esto: tus ojos van por delante. No empieces a predecir para ir más rápido.")
                    : ms < 320
                        ? S("Look at the edge of the target, not the centre: the hand reacts to the first pixel that appears.", "Mira el borde del objetivo, no el centro: la mano reacciona al primer píxel que aparece.")
                        : S($"At {ms:0} ms you're waiting to be sure. Fix your gaze on the spot where the target appears and let the hand go on the first flash of colour.",
                            $"Con {N(ms)} ms estás esperando a estar seguro. Fija la mirada en el punto donde aparece el objetivo y deja que la mano salga al primer destello de color.");
            }
            a.Plays.Add(play);
        }
    }

    // =====================================================================================================
    // Flashes
    // =====================================================================================================

    static List<TelemetryEvent>? FlashEvents(RunTelemetry t)
    {
        var list = t.Events.Where(e => e.Kind == "flash").ToList();
        return list.Count > 0 ? list : null;
    }

    static void AddFlashes(List<TelemetryEvent> events, PlayAnalysis a)
    {
        int i = 0;
        foreach (var e in events)
        {
            i++;
            var play = new Play { T0 = MathF.Max(0, e.T - 0.3f), T1 = e.T, Kind = "flash", Skill = Skill.Utility, Drills = new[] { "flashmap", "flashpeek" } };
            int grade = (int)e.A;
            float turn = e.B;
            switch (grade)
            {
                case 2:
                    play.Score = 25; play.Reason = "flashed";
                    play.Title = S($"Flash {i} · fully blinded", $"Flash {i} · cegado");
                    play.Detail = S("you took the full flash", "te comiste el flash completo");
                    play.Advice = turn > 0
                        ? S($"You turned after {turn:0} ms. Turn on the pop, not when the screen goes white: that delay is the difference between seeing the duel and not.",
                            $"Giraste a los {N(turn)} ms. Gira al oír el pop, no cuando la pantalla se pone blanca: ese retraso es la diferencia entre ver el duelo o no.")
                        : S("Turn away as soon as you hear it — the sound comes before the white.", "Gira en cuanto lo oigas: el sonido llega antes que el blanco.");
                    break;
                case 1:
                    play.Score = 62; play.Reason = "partial";
                    play.Title = S($"Flash {i} · partially blinded", $"Flash {i} · cegado a medias");
                    play.Detail = turn > 0 ? S($"turned after {turn:0} ms", $"girado a los {N(turn)} ms") : S("the edge caught you", "te pilló el borde");
                    play.Advice = S("Turn a little more and use the wall: the goal is to keep one useful sliver of the screen, not to close your eyes.",
                        "Gira un poco más y usa la pared: el objetivo es conservar una franja útil de la pantalla, no cerrar los ojos.");
                    break;
                case -1:
                    play.Score = 72; play.Reason = "no_los";
                    play.Title = S($"Flash {i} · no line of sight", $"Flash {i} · sin línea de visión");
                    play.Detail = S("it never touched you", "no te llegó");
                    play.Advice = S("No reaction needed: you were behind cover. Keep using the geometry like that.", "No hacía falta reaccionar: estabas tras cobertura. Sigue usando la geometría así.");
                    break;
                default:
                    play.Score = 95; play.Reason = "clean";
                    play.Title = S($"Flash {i} · dodged", $"Flash {i} · esquivado");
                    play.Detail = turn > 0 ? S($"turned in {turn:0} ms", $"girado en {N(turn)} ms") : S("you were already looking away", "ya estabas mirando fuera");
                    play.Advice = S("Perfect: you kept your angle and your eyes. That's how you hold a site against flashes.",
                        "Perfecto: mantuviste tu ángulo y tus ojos. Así se aguanta un sitio contra flashes.");
                    break;
            }
            a.Plays.Add(play);
        }
    }

    // =====================================================================================================
    // Rounds
    // =====================================================================================================

    static List<TelemetryEvent>? Rounds(RunTelemetry t)
    {
        var list = t.Events.Where(e => e.Kind is "round_won" or "round_lost").ToList();
        return list.Count > 0 ? list : null;
    }

    static void AddRounds(RunTelemetry t, List<TelemetryEvent> events, PlayAnalysis a)
    {
        float prev = 0;
        int n = 0;
        foreach (var e in events)
        {
            n++;
            bool won = e.Kind == "round_won";
            var inner = t.Events.Where(x => x.T >= prev - 0.01f && x.T <= e.T + 0.01f).ToList();
            int kills = inner.Count(x => x.Kind == "kill");
            bool died = inner.Any(x => x.Kind == "died");
            bool first = inner.Any(x => x.Kind == "first_blood");
            int utilKills = inner.Count(x => x.Kind is "util_kill" or "util_kill_direct" or "util_caught");
            bool plant = inner.Any(x => x.Kind == "spike_planted");
            float secs = MathF.Max(0, e.T - prev);

            var play = new Play
            {
                T0 = prev, T1 = e.T, Kind = "round", Skill = Skill.CrosshairPlacement, Drills = new[] { "deathmatch", "siteclear" },
            };
            int score = won ? 78 : 34;
            score += Math.Min(18, kills * 6);
            if (first) score += 4;
            if (died) score -= 14;
            if (utilKills > 0) score += 4;
            if (plant) score += 3;
            play.Score = Math.Clamp(score, 0, 100);

            play.Title = S($"Round {n} · {(won ? "won" : "lost")}", $"Round {n} · {(won ? "ganado" : "perdido")}");
            play.Detail = string.Join(" · ", new[]
            {
                S($"{kills} kill{(kills == 1 ? "" : "s")}", $"{kills} kill{(kills == 1 ? "" : "es")}"),
                S(died ? "you died" : "you survived", died ? "moriste" : "sobreviviste"),
                S($"{secs:0} s", $"{N(secs)} s"),
            });
            if (won && !died && kills > 0) { play.Reason = "clean"; play.Advice = S("Clean round: you took the duel and stayed alive. Keep trading only when you have the numbers.", "Round limpio: buscaste el duelo y sobreviviste. Sigue intercambiando solo cuando tengas ventaja numérica."); }
            else if (won) { play.Reason = "won"; play.Advice = S("You won it, but without a kill or with a death. Winning while alive is what makes rounds repeatable.", "Ganaste, pero sin kills o con una muerte. Ganar vivo es lo que hace repetibles los rounds."); }
            else if (died) { play.Reason = "died"; play.Advice = S("You lost the duel before the round was decided. Peek only with information or a teammate's trade behind you.", "Perdiste el duelo antes de que se decidiera el round. Asómate solo con información o con un compañero que te cubra el trade."); }
            else { play.Reason = "lost"; play.Advice = S("You survived but the round was lost: look for impact earlier — a trade or a first contact — instead of playing for the end.", "Sobreviviste pero el round se perdió: busca impacto antes —un trade o un primer contacto— en vez de jugar al final."); }
            a.Plays.Add(play);
            prev = e.T;
        }
    }

    // =====================================================================================================
    // Counter-strafes
    // =====================================================================================================

    static List<TelemetryEvent>? Stops(RunTelemetry t)
    {
        var list = t.Events.Where(e => e.Kind == "stop").ToList();
        return list.Count > 0 ? list : null;
    }

    static void AddStops(List<TelemetryEvent> events, PlayAnalysis a)
    {
        int i = 0;
        foreach (var e in events)
        {
            i++;
            bool counter = e.B > 0.5f;
            float ms = e.A;
            var play = new Play
            {
                T0 = MathF.Max(0, e.T - 0.3f), T1 = e.T, Kind = "stop", Skill = Skill.Movement,
                Drills = new[] { "counterstrafe", "strafebots" },
            };
            play.Score = Math.Clamp((counter ? 70 : 48) + (ms switch { < 100 => 22, < 140 => 12, < 180 => 2, < 240 => -8, _ => -16 }), 0, 100);
            play.Reason = ms >= 180 ? "slowstop" : counter ? "clean" : "notstop";
            play.Title = S($"Stop {i} · {ms:0} ms", $"Frenado {i} · {N(ms)} ms");
            play.Detail = counter
                ? S("counter-strafe (A/D into the stop)", "counter-strafe (A/D hacia el frenado)")
                : S("keys released, no counter-strafe", "teclas soltadas, sin counter-strafe");
            play.Advice = ms < 100 && counter
                ? S("Sharp stop: that's the timing that makes the first bullet land.", "Frenado seco: ese es el timing que hace que la primera bala entre.")
                : counter
                    ? S($"Good counter-strafe, but {ms:0} ms is a beat late. Press the opposite key a hair earlier, while you're still moving.",
                        $"Buen counter-strafe, pero {N(ms)} ms es un poco tarde. Pulsa la tecla contraria un pelo antes, todavía en movimiento.")
                    : S($"Releasing the keys took {ms:0} ms. Add the opposite key: A then D (or D then A) stops you almost twice as fast.",
                        $"Soltar las teclas tardó {N(ms)} ms. Añade la tecla contraria: A y luego D (o D y luego A) te frena casi el doble de rápido.");
            a.Plays.Add(play);
        }
    }

    // =====================================================================================================
    // Tracking (continuous: one play per few seconds)
    // =====================================================================================================

    static bool AddTracking(RunTelemetry t, PlayAnalysis a)
    {
        var fr = t.Frames;
        if (fr.Count < 30) return false;
        float t0 = fr[0].T, t1 = fr[^1].T;
        if (t1 - t0 < 2f) return false;
        const float window = 4f;
        for (float s = t0; s < t1 - 1f; s += window)
        {
            float e = MathF.Min(t1, s + window);
            int total = 0, onTarget = 0;
            float err = 0, radius = 0, speed = 0;
            foreach (var f in fr)
            {
                if (f.T < s || f.T >= e || f.FocusId == 0) continue;
                total++;
                float r = MathF.Max(0.05f, f.FocusRadius);
                float errR = f.FocusErr / r;
                if (errR <= 1f) onTarget++;
                err += errR;
                radius += r;
                speed += f.Speed;
            }
            if (total < 20) continue;
            float frac = (float)onTarget / total;
            float mean = err / total;
            var play = new Play
            {
                T0 = s, T1 = e, Kind = "track", Skill = Skill.Tracking, Drills = new[] { "tracking", "airtrack" },
            };
            play.Score = Math.Clamp((int)(20 + frac * 62f) + (mean < 1.2f ? 10 : mean < 2f ? 4 : 0) + (speed / total < 1f ? 4 : 0), 0, 100);
            play.Reason = frac >= 0.65f ? "clean" : frac >= 0.4f ? "track_ok" : "track_lag";
            play.Title = S($"{frac * 100:0}% on target", $"{N(frac * 100)}% sobre el objetivo");
            play.Detail = S($"{mean:0.0}× the target radius on average · {e - s:0} s window",
                            $"{N(mean, "0.0")}× el radio del objetivo de media · ventana de {N(e - s)} s");
            play.Advice = frac >= 0.65f
                ? S("Stable tracking: keep the crosshair slightly ahead of the movement and correct with small wrist adjustments.",
                    "Tracking estable: mantén la mira un poco por delante del movimiento y corrige con pequeños ajustes de muñeca.")
                : frac >= 0.4f
                    ? S("You catch the target but lose it on direction changes. Watch the body, not the head, and move the arm with the strafe.",
                        "Pillas el objetivo pero lo pierdes en los cambios de dirección. Mira el cuerpo, no la cabeza, y acompaña el strafe con el brazo.")
                    : S("The crosshair is trailing the target. React to the first movement and keep the dot on it instead of chasing the centre.",
                        "La mira va por detrás del objetivo. Reacciona al primer movimiento y mantén el punto sobre él en vez de perseguir el centro.");
            a.Plays.Add(play);
        }
        return a.Plays.Count > 0;
    }

    // =====================================================================================================
    // Kills (fallback)
    // =====================================================================================================

    static void AddKills(RunTelemetry t, PlayAnalysis a)
    {
        // Group kills and deaths into short windows so one fight is one play.
        var marks = t.Events.Where(e => e.Kind is "kill" or "died").OrderBy(e => e.T).ToList();
        int i = 0;
        foreach (var e in marks)
        {
            i++;
            bool kill = e.Kind == "kill";
            bool head = kill && e.B > 0.5f;
            float ttk = kill ? e.A : 0;
            var play = new Play
            {
                T0 = MathF.Max(0, e.T - 0.5f), T1 = e.T, Kind = "kill",
                Skill = kill ? Skill.Flicking : Skill.CrosshairPlacement,
                Drills = kill ? new[] { "deathmatch" } : new[] { "peek", "deathmatch" },
            };
            if (kill)
            {
                play.Score = Math.Clamp((head ? 88 : 76) + (ttk switch { <= 0 => 0, < 250 => 8, < 450 => 4, < 700 => 0, _ => -8 }), 0, 100);
                play.Reason = play.Score >= 85 ? "clean" : "kill";
                play.Title = S($"Kill{(head ? " · headshot" : "")}", $"Kill{(head ? " · a la cabeza" : "")}");
                play.Detail = ttk > 0 ? S($"{ttk:0} ms from first sight", $"{N(ttk)} ms desde que lo viste") : S("target down", "objetivo abatido");
                play.Advice = head
                    ? S("Headshot: keep the crosshair at head height before the fight, not during it.", "A la cabeza: mantén la mira a la altura de la cabeza antes del duelo, no durante.")
                    : S($"Body kill in {ttk:0} ms. The next step is starting the duel already at head height so the first bullet can finish it.",
                        $"Kill al cuerpo en {N(ttk)} ms. El siguiente paso es empezar el duelo ya a la altura de la cabeza para que la primera bala lo cierre.");
            }
            else
            {
                play.Score = 30; play.Reason = "died";
                play.Title = S("Death", "Muerte");
                play.Detail = S("you lost the duel", "perdiste el duelo");
                play.Advice = S("Check what you could see before it: if you had no information, that peek was a coin flip.",
                    "Revisa qué veías antes: si no tenías información, ese peek fue una moneda al aire.");
            }
            a.Plays.Add(play);
        }
    }

    // =====================================================================================================
    // Run verdict
    // =====================================================================================================

    static void Finish(PlayAnalysis a)
    {
        if (a.Plays.Count == 0)
        {
            a.Verdict = S("There wasn't enough recorded detail to judge this run play by play.",
                          "No había suficiente detalle grabado para juzgar esta partida jugada a jugada.");
            return;
        }
        a.Plays.Sort((x, y) => x.T0.CompareTo(y.T0));
        if (a.Plays.Count > 48) Trim(a);
        for (int i = 0; i < a.Plays.Count; i++) a.Plays[i].Index = i + 1;

        foreach (var p in a.Plays)
        {
            if (p.Score >= 85) a.Excellent++;
            else if (p.Score >= 70) a.Good++;
            else if (p.Score >= 55) a.Fair++;
            else a.Poor++;
        }
        a.Average = a.Plays.Average(p => (float)p.Score);

        string GradeCounts()
        {
            var parts = new List<string>();
            void Add(int n, string es1, string esN, string en1, string enN)
            {
                if (n <= 0) return;
                parts.Add(Es ? $"{n} {(n == 1 ? es1 : esN)}" : $"{n} {(n == 1 ? en1 : enN)}");
            }
            Add(a.Excellent, "limpia", "limpias", "clean", "clean");
            Add(a.Good, "buena", "buenas", "good", "good");
            Add(a.Fair, "regular", "regulares", "fair", "fair");
            Add(a.Poor, "por corregir", "por corregir", "to fix", "to fix");
            return parts.Count switch
            {
                0 => Es ? "sin jugadas valoradas" : "no graded plays",
                1 => parts[0],
                _ => string.Join(", ", parts.Take(parts.Count - 1)) + (Es ? " y " : " and ") + parts[^1],
            };
        }

        var bad = a.Plays.Where(p => p.Score < 70 && p.Reason != "clean").ToList();
        var leak = bad.GroupBy(p => p.Reason).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (leak != null)
        {
            a.Drills = leak.First().Drills;
            string what = leak.Key switch
            {
                "over" => S($"going past the target ({Pn(leak.Count())})", $"pasarte de largo ({Pn(leak.Count())})"),
                "under" => S($"stopping short ({Pn(leak.Count())})", $"quedarte corto ({Pn(leak.Count())})"),
                "corrections" => S($"needing extra corrections ({Pn(leak.Count())})", $"necesitar correcciones extra ({Pn(leak.Count())})"),
                "slow" => S($"slow first movements ({Pn(leak.Count())})", $"primeros movimientos lentos ({Pn(leak.Count())})"),
                "float" => S($"waiting on the target before firing ({Pn(leak.Count())})", $"esperar sobre el objetivo antes de disparar ({Pn(leak.Count())})"),
                "placed" => S($"slow closes from a good angle ({Pn(leak.Count())})", $"cierres lentos desde un ángulo bueno ({Pn(leak.Count())})"),
                "firstmiss" => S($"firing before you're on target ({Pn(leak.Count())})", $"disparar antes de estar sobre el objetivo ({Pn(leak.Count())})"),
                "moving" => S($"shooting while moving ({Pn(leak.Count())})", $"disparar en movimiento ({Pn(leak.Count())})"),
                "flashed" => S($"taking full flashes ({Pn(leak.Count())})", $"comerte los flashes enteros ({Pn(leak.Count())})"),
                "partial" => S($"taking half flashes ({Pn(leak.Count())})", $"comerte los flashes a medias ({Pn(leak.Count())})"),
                "early" or "false_alarm" => S($"guessing instead of reacting ({Pn(leak.Count())})", $"adivinar en vez de reaccionar ({Pn(leak.Count())})"),
                "died" => S($"losing the duel ({Pn(leak.Count())})", $"perder el duelo ({Pn(leak.Count())})"),
                "slowstop" or "notstop" => S($"slow counter-strafes ({Pn(leak.Count())})", $"counter-strafes lentos ({Pn(leak.Count())})"),
                "spray_high" => S($"sprays finishing high ({Pn(leak.Count())})", $"sprays que terminan altos ({Pn(leak.Count())})"),
                "spray_low" => S($"sprays finishing low ({Pn(leak.Count())})", $"sprays que terminan bajos ({Pn(leak.Count())})"),
                "spray_side" => S($"sprays drifting sideways ({Pn(leak.Count())})", $"sprays que se van de lado ({Pn(leak.Count())})"),
                "track_lag" => S($"losing the target while tracking ({Wn(leak.Count())})", $"perder el objetivo en el tracking ({Wn(leak.Count())})"),
                "react_slow" => S($"slow reactions ({Pn(leak.Count())})", $"reacciones lentas ({Pn(leak.Count())})"),
                "track_ok" => S($"unstable tracking ({Wn(leak.Count())})", $"tracking irregular ({Wn(leak.Count())})"),
                "miss" or "notplaced" or "lost" => S($"not finishing the engagement ({Pn(leak.Count())})", $"no cerrar el duelo ({Pn(leak.Count())})"),
                _ => S($"inconsistent plays ({Pn(leak.Count())})", $"jugadas irregulares ({Pn(leak.Count())})"),
            };
            a.Verdict = S(
                $"{Pn(a.Plays.Count)}: {GradeCounts()}. Your main weakness was {what}. ",
                $"{Pn(a.Plays.Count)}: {GradeCounts()}. Tu principal punto débil fue {what}. ");
        }
        else
        {
            a.Verdict = S(
                $"{Pn(a.Plays.Count)}: {GradeCounts()} — no repeated mistake in this run. ",
                $"{Pn(a.Plays.Count)}: {GradeCounts()}; ningún error repetido en esta partida. ");
        }

        var best = a.Plays.OrderByDescending(p => p.Score).ThenBy(p => p.Index).First();
        if (best.Score >= 70)
            a.Verdict += S($"Best play: #{best.Index} ({best.Title}). ", $"Mejor jugada: la #{best.Index} ({best.Title}). ");

        // Trend: the last third against the first third.
        int third = Math.Max(2, a.Plays.Count / 3);
        float first = a.Plays.Take(third).Average(p => (float)p.Score);
        float last = a.Plays.Skip(a.Plays.Count - third).Average(p => (float)p.Score);
        if (last - first >= 6)
            a.Verdict += S("You finished stronger than you started: the tempo was better late in the run.",
                           "Terminaste más fuerte de lo que empezaste: el tempo fue mejor al final de la partida.");
        else if (first - last >= 6)
            a.Verdict += S("You started well and dropped off: watch your grip and breathing in the last plays.",
                           "Empezaste bien y bajaste: vigila la presión de la mano y la respiración en las últimas jugadas.");
        else
            a.Verdict += S("Your level was stable from the first play to the last.",
                           "Tu nivel se mantuvo estable de la primera jugada a la última.");
    }

    /// <summary>"1 play" / "3 plays" (the verdict counts plays, never "1 plays").</summary>
    static string Pn(int n) => Es ? $"{n} jugada{(n == 1 ? "" : "s")}" : $"{n} play{(n == 1 ? "" : "s")}";
    static string Wn(int n) => Es ? $"{n} ventana{(n == 1 ? "" : "s")}" : $"{n} window{(n == 1 ? "" : "s")}";

    /// <summary>Long runs keep every bad play and an even sample of the rest, never more than 48 cards.</summary>
    static void Trim(PlayAnalysis a)
    {
        var keep = a.Plays.Where(p => p.Score < 55).ToHashSet();
        if (keep.Count > 32) keep = keep.OrderBy(p => p.Score).Take(32).ToHashSet();
        int room = 48 - keep.Count;
        var rest = a.Plays.Where(p => !keep.Contains(p)).ToList();
        if (room > 0 && rest.Count > 0)
        {
            float step = (float)rest.Count / room;
            for (int k = 0; k < room; k++) keep.Add(rest[Math.Min(rest.Count - 1, (int)(k * step))]);
        }
        a.Plays.Clear();
        a.Plays.AddRange(keep.OrderBy(p => p.T0));
    }
}
