using ValTrainer.Core;
using ValTrainer.Game;

namespace ValTrainer.Analysis;

/// <summary>One live coaching cue: what is going wrong right now and the one thing to do about it.</summary>
public sealed record LiveTip(string Id, string Headline, string Action, string Evidence, bool Positive, float At);

/// <summary>
/// The coach during the run (Settings → Gameplay → Live coach). Every second it looks at the last ~12 seconds of the
/// run's own telemetry (the same frames, shots and events the after-run coach analyses) and, when a pattern is clear
/// enough, shows one short cue with the evidence and a concrete fix: shooting while moving, sprays too long for the
/// distance, crosshair placement too low / high / far from where enemies appear, overshooting or stopping short of
/// flicks, trailing a tracking target, too few headshots, anticipating the reaction signal, dying while running. A cue
/// needs enough evidence (a minimum number of shots / sightings), each kind waits ≥ 25 s before repeating and two
/// cues are ≥ 7 s apart, so it never nags. Good streaks get a short positive note. Every cue is logged in the telemetry
/// ("coach_tip") and listed on the results screen.
/// </summary>
public sealed class LiveCoach
{
    const float Window = 12f, Every = 1f, MinGap = 7f, Repeat = 25f, Show = 5.5f;

    readonly RunTelemetry tel;
    readonly bool movementDrill, staticDrill, trackingDrill, botDrill, reactionDrill;
    readonly Dictionary<string, float> lastById = new();
    float nextCheck = 3f, lastTipAt = -99f;
    public LiveTip? Current { get; private set; }
    public readonly List<LiveTip> History = new();

    static bool Es => Loc.Spanish;

    public LiveCoach(RunTelemetry telemetry, string modeKey, bool movement)
    {
        tel = telemetry;
        string b = RunAnalysis.Base(modeKey);
        movementDrill = movement;
        staticDrill = b is "flick" or "gridshot" or "spider" or "microshot" or "popup" or "switch";
        trackingDrill = b is "tracking" or "airtrack" or "reacttrack";
        reactionDrill = b is "reaction" or "reactlab";
        botDrill = !staticDrill && !trackingDrill && !reactionDrill && b is not ("sensfinder" or "xhairfinder" or "crosshairfinder");
    }

    /// <summary>Call every frame while the run is running.</summary>
    public void Tick(float now)
    {
        if (Current != null && now - Current.At > Show) Current = null;
        if (staticDrill) PollFlicks(now);
        if (now < nextCheck) return;
        nextCheck = now + Every;
        if (now - lastTipAt < MinGap) return;
        var tip = Evaluate(now);
        if (tip == null) return;
        lastById[tip.Id] = now;
        lastTipAt = now;
        Current = tip;
        History.Add(tip);
        tel.Events.Add(new TelemetryEvent(now, "coach_tip", History.Count, tip.Positive ? 1 : 0));
        if (CmdLine.Dev) Godot.GD.Print($"[livecoach] t={now:0.0} {tip.Id}: {tip.Headline} — {tip.Evidence}");
    }

    // ---- flick over/undershoot: the after-run analysis on a snapshot of the last FlickWindow seconds ----

    const float FlickWindow = 15f, FlickEvery = 2.5f;
    /// <param name="OverDeg">Mean overshoot of the flicks that went past (deg), <paramref name="OverPct"/> the same as a
    /// share of the flick distance, <paramref name="OverR"/> their mean target radius; <paramref name="Gain"/> the mean
    /// distance covered by the primary movement / the distance needed (all flicks).</param>
    readonly record struct FlickCounts(int Flicks, int Over, int Under, float OverDeg, float OverPct, float OverR, float Gain, float At);
    FlickCounts? flick;
    System.Threading.Tasks.Task<FlickCounts>? flickTask;
    float nextFlickAt = 4f;

    /// <summary>Collects a finished analysis and, every few seconds, starts the next one on a copy of the recent
    /// telemetry (the lists keep growing on the main thread, so the worker never touches them).</summary>
    void PollFlicks(float now)
    {
        if (flickTask is { IsCompleted: true } done)
        {
            if (done.Status == System.Threading.Tasks.TaskStatus.RanToCompletion) flick = done.Result;
            flickTask = null;
        }
        if (flickTask != null || now < nextFlickAt) return;
        nextFlickAt = now + FlickEvery;
        float from = now - FlickWindow;
        var snap = new RunTelemetry { Mode = tel.Mode, Tier = tel.Tier, Sens = tel.Sens, Dpi = tel.Dpi, Weapon = tel.Weapon, HFov = tel.HFov, ZoomSensMult = tel.ZoomSensMult };
        int fi = tel.Frames.Count - 1;
        while (fi > 0 && tel.Frames[fi - 1].T >= from) fi--;
        for (int i = Math.Max(0, fi); i < tel.Frames.Count; i++) snap.Frames.Add(tel.Frames[i]);
        foreach (var sh in tel.Shots) if (sh.T >= from) snap.Shots.Add(sh);
        foreach (var e in tel.Events) if (e.T >= from) snap.Events.Add(e);
        float at = now;
        flickTask = System.Threading.Tasks.Task.Run(() =>
        {
            int flicks = 0, over = 0, under = 0;
            float overDeg = 0, overPct = 0, overR = 0, gain = 0;
            try
            {
                if (RunAnalysis.Prepare(snap) is { } p)
                    foreach (var (_, r) in RunAnalysis.Trials(p))
                    {
                        if (r is not { HasPrimary: true, Bot: false }) continue;
                        flicks++;
                        gain += r.Gain;
                        if (r.Over) { over++; overDeg += r.S; overPct += r.S / r.A; overR += r.R; }
                        else if (r.Under) under++;
                    }
            }
            catch { /* a partial window the analysis can't read: no cue this time */ }
            return over > 0
                ? new FlickCounts(flicks, over, under, overDeg / over, 100f * overPct / over, overR / over, flicks > 0 ? gain / flicks : 1f, at)
                : new FlickCounts(flicks, over, under, 0, 0, 0, flicks > 0 ? gain / flicks : 1f, at);
        });
    }

    bool Ready(string id, float now) => !lastById.TryGetValue(id, out var t) || now - t >= Repeat;

    LiveTip Tip(string id, float now, string hEn, string hEs, string aEn, string aEs, string eEn, string eEs, bool positive = false) =>
        new(id, Es ? hEs : hEn, Es ? aEs : aEn, Es ? eEs : eEn, positive, now);

    LiveTip? Evaluate(float now)
    {
        float from = now - Window;
        var shots = tel.Shots.Where(s => s.T >= from).ToList();
        var events = tel.Events.Where(e => e.T >= from).ToList();

        // ---- reaction tests: anticipation
        if (reactionDrill)
        {
            int early = events.Count(e => e.Kind == "early_click");
            if (early >= 2 && Ready("EARLY", now))
                return Tip("EARLY", now, "Wait for the signal", "Espera a la señal",
                    "Guessing costs more than it saves: react to what you see or hear.", "Adivinar cuesta más de lo que ahorra: reacciona a lo que ves u oyes.",
                    $"{early} early clicks in the last {Window:0} s", $"{early} clics adelantados en los últimos {Window:0} s");
            return null;
        }

        // ---- shooting while moving (VALORANT: accurate only below 27.5% of the run speed)
        if (movementDrill && shots.Count >= 6 && Ready("MOVING", now))
        {
            int moving = shots.Count(s => !s.Accurate);
            if (moving >= 3 && moving >= shots.Count * 0.35f)
                return Tip("MOVING", now, "Stop before you shoot", "Párate antes de disparar",
                    "Tap the opposite strafe key (counter-strafe), then click. Moving shots go wide.",
                    "Toca la tecla de strafe contraria (counter-strafe) y luego dispara. En movimiento las balas se abren.",
                    $"{moving} of {shots.Count} shots fired while moving", $"{moving} de {shots.Count} disparos en movimiento");
        }

        // ---- sprays too long for the distance (bot drills with rifles)
        if (botDrill && shots.Count >= 10 && Ready("SPRAY", now))
        {
            int late = 0, lateHits = 0, run = 0;
            for (int i = 0; i < shots.Count; i++)
            {
                run = i > 0 && shots[i].T - shots[i - 1].T < 0.16f ? run + 1 : 0;
                if (run >= 5) { late++; if (shots[i].Hit) lateHits++; }
            }
            if (late >= 6 && lateHits <= late * 0.2f)
                return Tip("SPRAY", now, "Shorter bursts", "Ráfagas más cortas",
                    "After the 5th bullet the gun climbs: fire 3–5, let it reset, fire again.",
                    "Después de la 5.ª bala el arma sube: dispara 3–5, deja que se recupere y repite.",
                    $"{lateHits} of {late} bullets after the 5th hit", $"{lateHits} de {late} balas tras la 5.ª acertaron");
        }

        // ---- crosshair placement at first sight (bot_seen: A = angle off the crosshair, B = vertical, + = too high)
        var seen = events.Where(e => e.Kind == "bot_seen").ToList();
        if (seen.Count >= 3)
        {
            float vert = seen.Average(e => e.B), off = seen.Average(e => e.A);
            if (vert < -1.6f && Ready("XLOW", now))
                return Tip("XLOW", now, "Crosshair too low", "Mira demasiado baja",
                    "Keep it at head height on the angle you expect: you won't need to flick up.",
                    "Mantenla a la altura de la cabeza en el ángulo que esperas: no tendrás que corregir hacia arriba.",
                    $"enemies appeared {-vert:0.0}° above your crosshair", $"los enemigos aparecieron {-vert:0.0}° por encima de tu mira");
            if (vert > 1.6f && Ready("XHIGH", now))
                return Tip("XHIGH", now, "Crosshair too high", "Mira demasiado alta",
                    "Lower it to head level: aiming over heads means a downward correction every time.",
                    "Bájala al nivel de la cabeza: apuntar por encima obliga a corregir hacia abajo siempre.",
                    $"enemies appeared {vert:0.0}° below your crosshair", $"los enemigos aparecieron {vert:0.0}° por debajo de tu mira");
            if (off > 14f && Ready("XFAR", now))
                return Tip("XFAR", now, "Pre-aim the angle", "Pre-apunta el ángulo",
                    "Point the crosshair at the corner an enemy will come from, not at the wall.",
                    "Pon la mira en la esquina por la que saldrá el enemigo, no en la pared.",
                    $"enemies appeared {off:0}° from your crosshair on average", $"los enemigos aparecieron a {off:0}° de tu mira de media");
        }

        // ---- overshoot / stopping short on flicks (static targets). The same measure as the after-run coach
        // (RunAnalysis: where the primary submovement of each flick ends, along the flick, relative to the target's
        // radius), computed on a snapshot of the last seconds off the main thread (see PollFlicks).
        if (staticDrill && Ready("FLICK", now) && flick is { } fl)
        {
            int n = fl.Over + fl.Under;
            if (CmdLine.Dev && CmdLine.Has("--livecoach-debug"))
                Godot.GD.Print($"[livecoach] t={now:0} flicks {fl.Flicks} over {fl.Over} ({fl.OverDeg:0.0}°, {fl.OverPct:0}%, r {fl.OverR:0.0}°) under {fl.Under} gain {fl.Gain:0.00} (snapshot at {fl.At:0.0})");
            // The after-run coach's thresholds (Assess: OVERFLICK / UNDERFLICK), a little stricter on the smaller sample:
            // most flicks off the target end past it, by more than 1.5 target radii and 4% of the distance; or the
            // flicks cover clearly less than the distance and nearly all off-target ones stop short. Ending a little
            // short is the normal human pattern and never triggers a cue.
            float underShare = n > 0 ? fl.Under / (float)n : 0;
            if (n >= 6 && fl.Over >= n * 0.68f && fl.OverDeg >= 1.5f * fl.OverR && fl.OverPct >= 4f)
                return Tip("FLICK", now, "You overshoot", "Te pasas del objetivo",
                    "Slow down the last part of the flick; let the crosshair land, then click.",
                    "Frena la última parte del flick: deja que la mira se asiente y luego dispara.",
                    $"{fl.Over} of your last {fl.Flicks} flicks went {fl.OverDeg:0.0}° past the target before correcting",
                    $"{fl.Over} de tus últimos {fl.Flicks} flicks se pasaron {fl.OverDeg:0.0}° del objetivo antes de corregir");
            if (n >= 6 && (fl.Gain < 0.85f && underShare >= 0.85f || fl.Gain < 0.75f && underShare >= 0.7f))
                return Tip("FLICK", now, "You stop short", "Te quedas corto",
                    "Commit to the flick: a full arm/wrist motion, then the small correction.",
                    "Comprométete con el flick: movimiento completo y luego la pequeña corrección.",
                    $"your flicks cover {fl.Gain * 100:0}% of the distance; {fl.Under} of the last {fl.Flicks} stopped before the target",
                    $"tus flicks recorren el {fl.Gain * 100:0}% de la distancia; {fl.Under} de los últimos {fl.Flicks} se quedaron antes del objetivo");
        }

        // ---- tracking: distance from the target while firing
        if (trackingDrill && Ready("TRACK", now))
        {
            var fr = tel.Frames.Where(f => f.T >= from && f.FocusId != 0 && f.Has(FrameFlags.Firing)).ToList();
            if (fr.Count >= 120)
            {
                float on = fr.Count(f => f.FocusErr <= f.FocusRadius) / (float)fr.Count;
                float err = fr.Average(f => f.FocusErr);
                if (on < 0.35f)
                    return Tip("TRACK", now, "Smoother tracking", "Seguimiento más suave",
                        "Match the target's speed with your arm, not quick wrist corrections; re-centre after every reversal.",
                        "Iguala la velocidad del objetivo con el brazo, sin correcciones bruscas de muñeca; re-centra tras cada cambio.",
                        $"on target {on * 100:0}% of the time, {err:0.0}° off on average", $"sobre el objetivo el {on * 100:0}% del tiempo, a {err:0.0}° de media");
                if (on > 0.62f && Ready("TRACKOK", now))
                    return Tip("TRACKOK", now, "Locked on", "Bien enganchado",
                        "Keep that rhythm.", "Mantén ese ritmo.",
                        $"on target {on * 100:0}% of the last {Window:0} s", $"sobre el objetivo el {on * 100:0}% de los últimos {Window:0} s", positive: true);
            }
        }

        // ---- headshots (bot drills)
        int hits = shots.Count(s => s.Hit);
        if (botDrill && hits >= 8 && Ready("HEAD", now))
        {
            int heads = shots.Count(s => s.Zone == 0);
            if (heads <= hits * 0.12f)
                return Tip("HEAD", now, "Aim for the head", "Apunta a la cabeza",
                    "Head level is where the first bullet goes: raise the crosshair before the duel starts.",
                    "La primera bala va donde está tu mira: súbela a la cabeza antes del duelo.",
                    $"{heads} headshots in {hits} hits", $"{heads} tiros a la cabeza en {hits} impactos");
            if (heads >= hits * 0.45f && Ready("HEADOK", now))
                return Tip("HEADOK", now, "Great headshot rate", "Gran porcentaje a la cabeza",
                    "Keep the crosshair there.", "Mantén la mira ahí.",
                    $"{heads} of {hits} hits on the head", $"{heads} de {hits} impactos en la cabeza", positive: true);
        }

        // ---- deaths while running (map / deathmatch)
        if (movementDrill && Ready("DIERUN", now))
        {
            int runningDeaths = 0, deaths = 0;
            foreach (var e in tel.Events.Where(e => e.Kind == "died" && e.T >= now - 40f))
            {
                deaths++;
                var f = tel.Frames.LastOrDefault(x => x.T <= e.T);
                if (f.Speed > 3.4f) runningDeaths++;
            }
            if (runningDeaths >= 2 && runningDeaths >= deaths * 0.6f)
                return Tip("DIERUN", now, "You die while running", "Mueres mientras corres",
                    "Walk (shift) near angles and stop before a peek: running makes you loud and inaccurate.",
                    "Camina (shift) cerca de los ángulos y párate antes de asomarte: correr te hace ruidoso e impreciso.",
                    $"{runningDeaths} of your last {deaths} deaths were at full speed", $"{runningDeaths} de tus últimas {deaths} muertes fueron a toda velocidad");
        }
        return null;
    }
}
