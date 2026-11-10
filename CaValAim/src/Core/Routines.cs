using System.Globalization;
using System.Text;
using Godot;
using CaValAim.Modes;

namespace CaValAim.Core;

public enum RoutineFocus { Clicking, Switching, Tracking, Micro, Movement, InGame, Recoil, Deathmatch, Maps }

/// <summary>One run of a routine step: how long it lasts and the sensitivity multiplier it asks for.</summary>
public sealed class RoutineRun
{
    public float Minutes = 1f;
    public float Sens = 1f;

    public RoutineRun(float minutes, float sens = 1f)
    {
        Minutes = minutes;
        Sens = sens;
    }
}

/// <summary>
/// One step of a community routine, adapted to a CA-VAL-AIM drill. The source scenario name is kept for attribution;
/// <see cref="Drill"/> is the closest built-in exercise, not a claim that it is the original scenario.
/// </summary>
public sealed class RoutineStep
{
    public string Scenario = "";
    public string Drill = "";
    public RoutineFocus Focus;
    public bool Optional;
    /// <summary>Camera climb for this step. Aim routines always play without recoil; the source scenarios assume it off.</summary>
    public bool RecoilOn;
    public string HowEn = "", HowEs = "";
    public string WhyEn = "", WhyEs = "";
    public string? SensNoteEn, SensNoteEs;
    public List<RoutineRun> Runs = new();

    public float Minutes => Runs.Sum(r => r.Minutes);
    public bool AllOptional => Optional;

    /// <summary>The built-in drill's display name, or an empty string for an external/in-game step.</summary>
    public string DrillName
    {
        get
        {
            if (Drill.Length == 0) return "";
            try { return CaValAim.Modes.ModeRegistry.Find(Drill)?.Invoke().Name ?? Drill; }
            catch { return Drill; }
        }
    }

    public string SensLabel
    {
        get
        {
            var groups = new List<(float Sens, int Count)>();
            foreach (var r in Runs)
            {
                float s = r.Sens <= 0 ? 1f : r.Sens;
                int at = groups.FindIndex(g => MathF.Abs(g.Sens - s) < 0.001f);
                if (at >= 0) groups[at] = (groups[at].Sens, groups[at].Count + 1);
                else groups.Add((s, 1));
            }
            return string.Join(" + ", groups.Select(g =>
                $"{g.Sens.ToString("0.##", CultureInfo.InvariantCulture)}×{(g.Count > 1 ? $" ×{g.Count}" : "")}"));
        }
    }

    public string RunsLabel
    {
        get
        {
            if (Runs.Count == 0) return "—";
            if (Runs.All(r => MathF.Abs(r.Minutes - Runs[0].Minutes) < 0.001f))
            {
                if (MathF.Abs(Runs[0].Minutes - MathF.Round(Runs[0].Minutes)) > 0.001f)
                    return $"{Runs.Count} × {Mathf.RoundToInt(Runs[0].Minutes * 60f)} s";
                return $"{Runs.Count} × {Runs[0].Minutes:0.#} min";
            }
            return $"{Minutes:0.#} min";
        }
    }
}

public sealed class Routine
{
    public string Id = "";
    public string Name = "";
    public string Source = "";
    public string LevelEn = "", LevelEs = "";
    public string DescriptionEn = "", DescriptionEs = "";
    public string CreditEn = "", CreditEs = "";
    public string SourceDoc = "";
    /// <summary>KovaaK's/Aim Lab share code, when the source provides one.</summary>
    public string? ShareCode;
    /// <summary>The routine can be played with built-in drills (an adaptation). False = external code or in-game only.</summary>
    public bool Playable = true;
    /// <summary>True when the original is a playlist elsewhere (KovaaK's/Aim Lab) and the code is the point.</summary>
    public bool External;
    public List<RoutineStep> Steps = new();

    public float Minutes => Steps.Sum(s => s.Minutes);
    public int PlayableSteps => Steps.Count(s => s.Drill.Length > 0);
    public string FocusSummary
    {
        get
        {
            var f = Steps.Select(s => s.Focus).Distinct().ToList();
            return string.Join(" · ", f.Select(x => RoutineLibrary.FocusName(x, Loc.Spanish)));
        }
    }

    public bool Play(bool includeOptional = true)
    {
        if (!Playable) return false;
        var p = ToPlaylist(out var stepOfRun, includeOptional);
        if (p.Modes.Count == 0) return false;
        // Pause → task navigation shows the official task name (the first alternative when the source lists several).
        var labels = stepOfRun.Select(i => i >= 0 && i < Steps.Count
            ? Steps[i].Scenario.Split(" / ", StringSplitOptions.TrimEntries)[0]
            : "").ToList();
        RoutineStore.MarkStarted(Id);
        var completedRuns = new HashSet<int>();
        var requiredRuns = Enumerable.Range(0, stepOfRun.Count).Where(i => !Steps[stepOfRun[i]].Optional).ToArray();
        if (requiredRuns.Length == 0) requiredRuns = Enumerable.Range(0, stepOfRun.Count).ToArray();
        PlaylistRunner.Play(p, i =>
        {
            if (i >= 0 && i < stepOfRun.Count) RoutineStore.MarkStep(Id, stepOfRun[i], Steps.Count);
        }, i =>
        {
            if (i < 0 || i >= stepOfRun.Count) return;
            completedRuns.Add(i);
            RoutineStore.MarkRunFinished(Id, stepOfRun[i], Steps.Count, requiredRuns.All(completedRuns.Contains));
        }, stepOfRun, labels);
        return true;
    }

    /// <summary>An in-memory playlist with one entry per run (durations and sens factors included).</summary>
    public Playlist ToPlaylist(out List<int> stepOfRun, bool includeOptional = true)
    {
        stepOfRun = new List<int>();
        var p = new Playlist { Name = Name };
        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            if (!includeOptional && step.Optional) continue;
            if (step.Drill.Length == 0 || ModeRegistry.Find(step.Drill) == null) continue;
            foreach (var run in step.Runs)
            {
                p.Modes.Add(step.Drill);
                p.Recoil[step.Drill] = step.RecoilOn;
                p.RecoilByRun.Add(step.RecoilOn);
                p.Sens.Add(run.Sens <= 0 ? 1f : run.Sens);
                p.Minutes.Add(run.Minutes <= 0 ? 0f : run.Minutes);
                stepOfRun.Add(i);
            }
        }
        return p;
    }

    /// <summary>Saves the adapted routine as a normal playlist (the player can edit and replay it).</summary>
    public Playlist? SaveAsPlaylist()
    {
        if (!Playable) return null;
        var p = ToPlaylist(out _);
        if (p.Modes.Count == 0) return null;
        string name = Name.Length > 30 ? Name[..30] : Name;
        return PlaylistStore.ReplaceRuns("RUTINA · " + name,
            p.Modes.Select((k, i) => new PlaylistRun(k, p.RecoilAt(i) ?? false, p.SensAt(i), p.MinutesAt(i))));
    }

    public RoutineStep? StepOfRun(int run)
    {
        if (run < 0) return null;
        foreach (var step in Steps)
        {
            if (step.Drill.Length == 0 || ModeRegistry.Find(step.Drill) == null) continue;
            if (run < step.Runs.Count) return step;
            run -= step.Runs.Count;
        }
        return null;
    }
}

/// <summary>The built-in routines: Voltaic's VALORANT scenarios adapted to this trainer, plus cartoon's external
/// Kovaak's playlists and the in-game VALORANT practice from Voltaic's document.</summary>
public static class RoutineLibrary
{
    public static string FocusName(RoutineFocus f, bool es) => f switch
    {
        RoutineFocus.Clicking => es ? "CLICK" : "CLICK",
        RoutineFocus.Switching => es ? "SWITCH" : "SWITCH",
        RoutineFocus.Tracking => es ? "TRACK" : "TRACK",
        RoutineFocus.Micro => es ? "MICRO" : "MICRO",
        RoutineFocus.Movement => es ? "MOV" : "MOV",
        RoutineFocus.Recoil => es ? "SPRAY" : "SPRAY",
        RoutineFocus.Deathmatch => es ? "DM" : "DM",
        RoutineFocus.Maps => es ? "MAPA" : "MAP",
        _ => es ? "INGAME" : "IN-GAME",
    };

    static RoutineStep Step(string scenario, string drill, RoutineFocus focus, float minutes,
        string howEn, string howEs, string whyEn, string whyEs, bool optional = false,
        string? sensNoteEn = null, string? sensNoteEs = null, bool recoil = false)
        => new()
        {
            Scenario = scenario, Drill = drill, Focus = focus,
            Runs = drill.Contains(":sc-", StringComparison.Ordinal) && minutes >= 2 && minutes == MathF.Truncate(minutes)
                ? Enumerable.Range(0, (int)minutes).Select(_ => new RoutineRun(1)).ToList()
                : new List<RoutineRun> { new(minutes) },
            HowEn = howEn, HowEs = howEs, WhyEn = whyEn, WhyEs = whyEs,
            Optional = optional, SensNoteEn = sensNoteEn, SensNoteEs = sensNoteEs, RecoilOn = recoil,
        };

    /// <summary>A step repeated <paramref name="reps"/> times, each run lasting <paramref name="minutes"/>.</summary>
    static RoutineStep Reps(string scenario, string drill, RoutineFocus focus, int reps, float minutes,
        string howEn, string howEs, string whyEn, string whyEs, bool optional = false, bool recoil = false)
    {
        var runs = new List<RoutineRun>();
        for (int i = 0; i < reps; i++) runs.Add(new RoutineRun(minutes));
        return new()
        {
            Scenario = scenario, Drill = drill, Focus = focus, Runs = runs,
            HowEn = howEn, HowEs = howEs, WhyEn = whyEn, WhyEs = whyEs, Optional = optional, RecoilOn = recoil,
        };
    }

    /// <summary>One cartoon scenario, played four times, on the drill that reproduces it.</summary>
    static RoutineStep CartoonStep(AimVariant v, RoutineFocus focus, int seconds,
        string howEn, string howEs, string whyEn, string whyEs)
        => Reps(v.Name, v.Key, focus, 4, seconds / 60f, howEn, howEs, whyEn, whyEs);

    static RoutineStep SensStep(string scenario, string drill, RoutineFocus focus, float factor, int boosted,
        string howEn, string howEs, string whyEn, string whyEs, bool optional = false,
        string? noteEn = null, string? noteEs = null)
    {
        var runs = new List<RoutineRun>();
        for (int i = 0; i < 4; i++) runs.Add(new RoutineRun(1f, i < boosted ? factor : 1f));
        return new()
        {
            Scenario = scenario, Drill = drill, Focus = focus, Runs = runs,
            HowEn = howEn, HowEs = howEs, WhyEn = whyEn, WhyEs = whyEs, Optional = optional,
            SensNoteEn = noteEn, SensNoteEs = noteEs,
        };
    }

    public static readonly Routine[] All =
    {
        new()
        {
            Id = "control-foundations", Name = "CONTROL · Frenar, confirmar y transferir",
            Source = "CA-VAL-AIM · entrenamiento propio",
            LevelEs = "Cualquier nivel · 8 minutos", LevelEn = "Any level · 8 minutes",
            DescriptionEs = "Dos rondas por habilidad. Conserva tu sensibilidad, confirma cada eliminación y compara tus resultados entre repeticiones.",
            DescriptionEn = "Two rounds per skill. Keep your sensitivity, confirm every elimination and compare results across repetitions.",
            Steps =
            {
                Reps("Brake and confirm", "flickx:settle-confirm", RoutineFocus.Clicking, 2, 1,
                    "Hold fire and settle on each target until it clears.", "Mantén disparo y frena sobre cada blanco hasta eliminarlo.",
                    "Learn to stop without crossing the target.", "Aprende a frenar sin atravesar el blanco."),
                Reps("Acquire and follow", "flickx:moving-confirm", RoutineFocus.Switching, 2, 1,
                    "Hold fire, follow the target and switch only after the kill.", "Mantén disparo, acompaña al blanco y cambia solo al eliminarlo.",
                    "Join acquisition and short tracking.", "Une la adquisición con un seguimiento breve."),
                Reps("Stop and shoot", "flickx:stop-tap", RoutineFocus.Movement, 2, 1,
                    "Strafe between shots. Release movement and tap with the Classic.", "Muévete entre tiros. Suelta la dirección y dispara con la Classic.",
                    "Separate movement from accurate firing.", "Separa el desplazamiento del disparo preciso.", recoil: true),
                Reps("Confirm two hits", "reflex:double-confirm", RoutineFocus.Clicking, 2, 1,
                    "Hit, read the new position, then hit again before the deadline.", "Acierta, lee la nueva posición y vuelve a acertar antes del límite.",
                    "React to the result instead of pre-firing.", "Reacciona al resultado sin anticipar el segundo tiro."),
            },
        },
        new()
        {
            Id = "voltaic-easy",
            Name = "Voltaic Easy · VALORANT",
            Source = "Voltaic · KovaaK's 2.0",
            LevelEn = "Bronze · Silver · Gold", LevelEs = "Bronce · Plata · Oro",
            SourceDoc = CommunityLinks.KvKsRoutines,
            ShareCode = "KOVAAKSTELEPORTINGJEBAITEDBUYSTATION",
            DescriptionEn =
                "Voltaic's entry routine for VALORANT and CS. It mixes smooth tracking, target switching, static " +
                "clicking, long flicks and movement one-taps. The source recommends 60–100 minutes; the starred steps " +
                "are optional, so the core is about 60 minutes.",
            DescriptionEs =
                "La rutina de entrada de Voltaic para VALORANT y CS. Mezcla tracking suave, cambio de objetivos, " +
                "clicking estático, flicks largos y one-taps en movimiento. La fuente recomienda 60–100 minutos; los " +
                "pasos marcados son opcionales, así que el núcleo son unos 60 minutos.",
            CreditEn = "Routine by Voltaic (owner: sini). Scenario list from the official Voltaic KvKs document.",
            CreditEs = "Rutina de Voltaic (responsable: sini). Lista de escenarios del documento oficial de Voltaic para KvKs.",
            Steps =
            {
                Step("Thin Gauntlet Easy / Centering I Easy", "trackv:sc-voltaic-thin-gauntlet-easy", RoutineFocus.Tracking, 10,
                    "Track the bot smoothly and stay relaxed. Commit to its direction, follow through with the arm " +
                    "instead of tiny wrist corrections, and recentre only when you run out of mousepad. On Centering I " +
                    "keep the crosshair at head height while the bot strafes.",
                    "Sigue al bot con suavidad y sin tensar. Comprométete con su dirección, acompaña con el brazo en " +
                    "vez de corregir con la muñeca y recentra solo cuando te quedes sin alfombrilla. En Centering I " +
                    "mantén la mira a la altura de la cabeza mientras el bot hace strafe.",
                    "VALORANT barely has tracking, but smooth mouse control carries over to spray control and to moving " +
                    "your crosshair between angles without jerks.",
                    "En VALORANT apenas hay tracking, pero el control suave del ratón se traslada al control del spray " +
                    "y a mover la mira entre ángulos sin tirones.", optional: true),
                Step("patTargetSwitch (no reload) / patTargetSwitch", "flickx:sc-voltaic-pat-target-switch", RoutineFocus.Switching, 10,
                    "Go for heads, not bodies. Kill, then switch as fast as possible to the next moving target and " +
                    "hold fire almost continuously. Use the 360 version when you want larger angles.",
                    "Busca cabezas, no cuerpos. Mata y cambia lo más rápido posible al siguiente objetivo móvil, " +
                    "manteniendo el disparo casi continuo. Usa la versión 360 si quieres ángulos más amplios.",
                    "Target acquisition and fast switching between moving enemies, the situation you meet when several " +
                    "players peek the same site.",
                    "Adquisición de objetivos y cambios rápidos entre enemigos móviles, la situación típica cuando " +
                    "varios jugadores asoman por el mismo sitio."),
                Step("Pokeball Frenzy Auto TE 60s / Wide", "flickx:sc-voltaic-pokeball-frenzy-te", RoutineFocus.Switching, 10,
                    "Hold fire and eliminate static targets as fast as possible. Let the next target pull your " +
                    "crosshair; do not stop in the middle of the path. VoxTargetSwitch Voltaic is the alternative if " +
                    "you want non-static acquisition.",
                    "Mantén el disparo y elimina objetivos estáticos lo más rápido posible. Deja que el siguiente " +
                    "objetivo arrastre tu mira; no te detengas a mitad del recorrido. VoxTargetSwitch Voltaic es la " +
                    "alternativa si quieres adquisición no estática.",
                    "Speed and clean lines when several targets are already visible, like clearing a site after an " +
                    "execute.",
                    "Velocidad y líneas limpias cuando ya ves varios objetivos, como limpiar un sitio tras una ejecución."),
                Step("voxTargetSwitch click / 1wall5targets_pasu slow / patTargetClick 1shot", "flickx:sc-voltaic-vox-target-switch-click",
                    RoutineFocus.Clicking, 10,
                    "One tap per target and fast switches. Your goal is to eliminate as many targets as possible; do " +
                    "not drag the crosshair through the target.",
                    "Un tap por objetivo y cambios rápidos. El objetivo es eliminar tantos como puedas; no arrastres " +
                    "la mira a través del objetivo.",
                    "Fast one-tap switches and click timing, the base of VALORANT's first-shot duels.",
                    "Cambios rápidos de un tap y timing de clic, la base de los duelos a primer disparo en VALORANT."),
                Step("Wide Wall 3 Targets / Wide Wall 4 Targets", "flickx:sc-voltaic-wide-wall-3", RoutineFocus.Clicking, 10,
                    "Hit as many targets as you can. Move with the arm for the long flicks and stop dead on the " +
                    "target. If small flicks are your weakness, play 1w2t smallflicks instead.",
                    "Golpea tantos objetivos como puedas. Mueve el brazo en los flicks largos y frena en seco sobre " +
                    "el objetivo. Si tu debilidad son los flicks cortos, juega 1w2t smallflicks.",
                    "Long flicks with a clean stop: the shot you need when an enemy appears far from your crosshair.",
                    "Flicks largos con frenada limpia: el disparo que necesitas cuando un enemigo aparece lejos de tu mira."),
                Step("Floating Heads Timing Easy / tamSpeed 2bps", "flickx:sc-voltaic-floating-heads-easy", RoutineFocus.Clicking, 10,
                    "One tap on the head of each moving target. Time the click for the moment the head crosses your " +
                    "crosshair and never follow the trail behind it.",
                    "Un tap en la cabeza de cada objetivo móvil. Haz clic en el momento en que la cabeza cruza tu " +
                    "mira y no sigas la estela que deja detrás.",
                    "Click timing on moving heads, close to an enemy swinging out of an angle.",
                    "Timing de clic sobre cabezas móviles, muy parecido a un enemigo asomando por un ángulo."),
                Step("voxTargetClick Easy Strafe / Pasu Dodge EES 180", "flickx:sc-voltaic-vox-target-click-strafe", RoutineFocus.Movement, 10,
                    "Turn on the movement bar if the scenario has one. Strafe with A/D, counter-strafe and tap when " +
                    "the crosshair is on the head. The whole focus is movement one-taps.",
                    "Activa la barra de movimiento si el escenario la tiene. Haz strafe con A/D, counter-strafea y " +
                    "dispara cuando la mira esté en la cabeza. Todo el foco está en one-taps en movimiento.",
                    "In VALORANT you shoot while counter-strafing, not while standing still; this trains that timing.",
                    "En VALORANT disparas mientras haces counter-strafe, no quieto; esto entrena ese timing.", optional: true),
                Step("Reflex Micro++ Flick Reload", "reflex:sc-voltaic-reflex-micro-flick", RoutineFocus.Clicking, 10,
                    "React to each pop-up and correct immediately. Do not anticipate the spawn; let your eyes find it " +
                    "first, then move.",
                    "Reacciona a cada aparición y corrige de inmediato. No anticiples el spawn; primero lo encuentran " +
                    "los ojos y después mueves.",
                    "Quick adjustments when you are caught off guard, a situation that will happen in every match.",
                    "Ajustes rápidos cuando te pillan por sorpresa, algo que pasa en todas las partidas.", optional: true),
                Step("Microshot Speed Medium", "microx:sc-voltaic-microshot-speed-medium", RoutineFocus.Micro, 10,
                    "Make small, fast corrections onto each new target. Stop exactly on the head; if you overflick, " +
                    "come back with the smallest movement that works.",
                    "Haz correcciones pequeñas y rápidas sobre cada objetivo nuevo. Frena exactamente en la cabeza; si " +
                    "te pasas, vuelve con el movimiento más pequeño posible.",
                    "Micro-corrections decide the first shot after a peek, especially when your crosshair placement " +
                    "was almost right.",
                    "Las micro-correcciones deciden el primer disparo tras un peek, sobre todo cuando tu " +
                    "colocación de mira era casi correcta.", optional: true),
                Step("Reactive Clicking", "reflex:sc-voltaic-reactive-clicking", RoutineFocus.Clicking, 10,
                    "React to each head as it pops, click it and reset immediately for the next. Do not chase the target " +
                    "as it disappears; the next pop-up is your re-acquisition.",
                    "Reacciona a cada cabeza en cuanto aparezca, haz clic y recolócate al instante para la siguiente. No " +
                    "persigas al objetivo cuando desaparece; el siguiente pop-up es tu re-adquisición.",
                    "Reactive clicking and a fast reset between targets, the shot after a surprise peek.",
                    "Clicking reactivo y recolocación rápida entre objetivos, el disparo tras un peek por sorpresa.", optional: true),
            },
        },

        new()
        {
            Id = "bardoz-advanced",
            Name = "VT bardOZ VALORANT Advanced",
            Source = "Voltaic × bardOZ · KovaaK's 2.0",
            LevelEn = "Diamond and above · any level if executed properly",
            LevelEs = "Diamante o superior · cualquier nivel si se ejecuta bien",
            SourceDoc = CommunityLinks.KvKsRoutines,
            ShareCode = "KOVAAKSULTINGCLUTCHHIPFIRE",
            DescriptionEn =
                "The advanced VALORANT routine made with bardOZ. Most scenarios are played four times: twice on double " +
                "your VALORANT sensitivity and twice on your normal sensitivity. That teaches control at high sens " +
                "without losing your in-game feel. SmoothBot uses 4×. The trainer clamps the boost so you never go " +
                "below 15 cm/360, and caps it at 1.5× when your normal sens is already faster than 35 cm/360.",
            DescriptionEs =
                "La rutina avanzada de VALORANT creada con bardOZ. Casi todos los escenarios se juegan cuatro veces: " +
                "dos con el doble de tu sensibilidad de VALORANT y dos con la normal. Así aprendes a controlar una sens " +
                "alta sin perder tu sensación en partida. SmoothBot usa 4×. El entrenador limita la subida para que " +
                "nunca bajes de 15 cm/360 y la capa a 1,5× si tu sens normal ya es más rápida que 35 cm/360.",
            CreditEn = "Routine by Voltaic and bardOZ. The source recommends 30–40 minutes and describes this as a " +
                       "support routine: KovaaK's → deathmatch → ranked.",
            CreditEs = "Rutina de Voltaic y bardOZ. La fuente recomienda 30–40 minutos y la describe como rutina de " +
                       "apoyo: KovaaK's → deathmatch → ranked.",
            Steps =
            {
                SensStep("voxTS Voltaic", "flickx:sc-voltaic-vox-ts", RoutineFocus.Switching, 2, 2,
                    "Play twice on double sens and twice on your normal sens. Switch very fast but control the " +
                    "crosshair the moment you arrive. Use a kill-confirmed sound and switch immediately after the kill.",
                    "Juega dos veces con el doble de sens y dos con tu sens normal. Cambia muy rápido pero controla la " +
                    "mira al llegar. Usa un sonido de confirmación de muerte y cambia justo después de matar.",
                    "Spray transfers when the enemy team executes your site, plus smooth crosshair placement.",
                    "Spray transfers cuando el equipo enemigo ejecuta tu sitio, además de colocación de mira suave.",
                    noteEn: "2× on the first two runs, 1× on the last two.", noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("Pokeball Frenzy Auto 1w2ts", "flickx:sc-voltaic-pokeball-1w2ts", RoutineFocus.Switching, 2, 2,
                    "Do not overflick: make clean lines between targets and switch as fast as you can after a kill. " +
                    "Hold fire all the time.",
                    "No te pases con el flick: haz líneas limpias entre objetivos y cambia lo más rápido posible tras " +
                    "matar. Mantén el disparo todo el tiempo.",
                    "A 1v2 clutch where two enemies peek together: the first movement must be right, then you only " +
                    "micro-correct.",
                    "Un clutch 1v2 en el que dos enemigos asoman juntos: el primer movimiento debe ser correcto y " +
                    "después solo micro-corriges.",
                    noteEn: "2× on the first two runs, 1× on the last two.", noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("Pokeball auto ww6t", "flickx:sc-voltaic-pokeball-ww6t", RoutineFocus.Switching, 2, 2,
                    "Move smoothly from target to target and speed up when targets cluster. Bind fire to Shift instead " +
                    "of Mouse1 so your mouse keeps the same tension as in game.",
                    "Muévete con suavidad de objetivo a objetivo y acelera cuando se agrupen. Pon el disparo en Shift " +
                    "en vez de Mouse1 para mantener la misma tensión del ratón que en partida.",
                    "The large and small angle adjustments you make subconsciously while navigating the map and " +
                    "holding crosshair placement.",
                    "Los ajustes de ángulo grandes y pequeños que haces inconscientemente al moverte por el mapa y " +
                    "mantener la colocación de la mira.",
                    noteEn: "2× on the first two runs, 1× on the last two.", noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("1w2ts reload", "flickx:sc-voltaic-1w2ts-reload", RoutineFocus.Clicking, 2, 2,
                    "No overflick, clean lines, switch immediately after the kill. Keep accuracy above 85% and aim to " +
                    "approach your high score with 95%+, because VALORANT punishes the first miss.",
                    "Sin overflick, líneas limpias y cambio inmediato tras matar. Mantén la precisión por encima del " +
                    "85% y busca acercarte a tu récord con 95%+, porque VALORANT castiga el primer fallo.",
                    "The real simulation of a fight: one shot on target and a fast switch. Speed without accuracy is " +
                    "a bad habit in VALORANT.",
                    "La simulación real de un combate: un disparo al objetivo y cambio rápido. Velocidad sin precisión " +
                    "es un mal hábito en VALORANT.",
                    noteEn: "2× on the first two runs, 1× on the last two.", noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("5 sphere hipfire extra small", "microx:sc-voltaic-5-sphere-extra-small", RoutineFocus.Micro, 2, 2,
                    "Maximize the speed of your micro-correction. After the initial small flick, correct quickly and " +
                    "accurately instead of firing again and hoping.",
                    "Maximiza la velocidad de tu micro-corrección. Después del flick pequeño inicial, corrige rápido y " +
                    "con precisión en vez de volver a disparar y esperar.",
                    "Small angles are the most important shots in VALORANT: a peeker stops in a 300–400 ms window and " +
                    "you must fix the last mistake of your placement.",
                    "Los ángulos pequeños son los disparos más importantes de VALORANT: el que asoma se detiene en una " +
                    "ventana de 300–400 ms y tú debes corregir el último error de tu colocación.",
                    noteEn: "2× on the first two runs, 1× on the last two.", noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("6wall18targets small reload", "flickx:sc-voltaic-6wall18", RoutineFocus.Clicking, 2, 2,
                    "Stay fast and keep clean lines even on the awkward angles. Move the mouse only as much as the " +
                    "new target needs.",
                    "Mantén la velocidad y las líneas limpias incluso en los ángulos incómodos. Mueve el ratón solo " +
                    "lo que pida el nuevo objetivo.",
                    "Checking awkward 3D angles, like Icebox or A site Haven, where you defend a plant with a flank " +
                    "and a retake from heaven at very different verticality.",
                    "Revisar ángulos 3D incómodos, como Icebox o el sitio A de Haven, donde defiendes una plantada con " +
                    "un flanqueo y un retake desde heaven con verticalidad muy distinta.",
                    noteEn: "2× on the first two runs, 1× on the last two.", noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("Floating Heads Timing 400%", "flickx:sc-cartoon-floating-heads-400", RoutineFocus.Clicking, 2, 2,
                    "Do not drop below 85% accuracy and avoid useless movement. This is the shot you must not miss: a " +
                    "readable moving head swinging out of an angle.",
                    "No bajes del 85% de precisión y evita movimientos inútiles. Es el disparo que no puedes fallar: " +
                    "una cabeza móvil legible asomando por un ángulo.",
                    "Very close to the movement and speed of an opponent swinging out. If you miss the first shot, the " +
                    "second is inaccurate and you can die.",
                    "Muy parecido al movimiento y la velocidad de un rival asomando. Si fallas el primer disparo, el " +
                    "segundo es impreciso y puedes morir.",
                    noteEn: "2× on the first two runs, 1× on the last two.", noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("SmoothBot Invincible Goated", "trackv:sc-voltaic-smoothbot", RoutineFocus.Tracking, 4, 4,
                    "Track the bot smoothly and recentre when needed, especially on low sens. Learn not to tense when " +
                    "you have to move smoothly.",
                    "Sigue al bot con suavidad y recentra cuando haga falta, sobre todo con sens baja. Aprende a no " +
                    "tensar cuando tengas que moverte con suavidad.",
                    "While you move around the map you smoothly track an imaginary point where an enemy can appear. " +
                    "This also covers Raze or Jett flying in an arc.",
                    "Mientras te mueves por el mapa sigues con suavidad un punto imaginario de donde puede salir un " +
                    "enemigo. También cubre a Raze o Jett volando en arco.",
                    optional: true, noteEn: "4× on all four runs (the source's only 4× step).",
                    noteEs: "4× en las cuatro pasadas (el único paso a 4× de la fuente)."),
                SensStep("Centering I", "trackv:sc-voltaic-centering-i", RoutineFocus.Tracking, 2, 2,
                    "No verticality; the same smooth horizontal tracking as SmoothBot. Keep the crosshair at head " +
                    "height and follow the bot across the whole lane.",
                    "Sin verticalidad; el mismo tracking horizontal suave que SmoothBot. Mantén la mira a la altura " +
                    "de la cabeza y sigue al bot por todo el carril.",
                    "Most of the time you navigate angles horizontally, for example in a 1v1 on C site Haven while " +
                    "moving and holding an angle.",
                    "Casi siempre navegas ángulos en horizontal, por ejemplo en un 1v1 en el sitio C de Haven " +
                    "mientras te mueves y mantienes un ángulo.",
                    optional: true, noteEn: "2× on the first two runs, 1× on the last two.",
                    noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("Pistol Strafe Gallery Small", "trackv:sc-voltaic-pistol-strafe-gallery", RoutineFocus.Movement, 2, 2,
                    "Use the fire button like a close-range pistol spam and keep moving constantly. Do not stand " +
                    "still to aim.",
                    "Usa el disparo como harías spam con una pistola a corta distancia y muévete constantemente. No te " +
                    "quedes quieto para apuntar.",
                    "Pistol rounds decide two rounds per half, and pistols (and the current Phantom) keep decent " +
                    "accuracy while moving at close range.",
                    "Las rondas de pistola deciden dos rondas por mitad, y las pistolas (y el Phantom actual) mantienen " +
                    "una precisión decente al moverse a corta distancia.",
                    optional: true, noteEn: "2× on the first two runs, 1× on the last two.",
                    noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
                SensStep("Reflex Micro++ Flick Reload Small", "reflex:sc-voltaic-reflex-micro-flick-small", RoutineFocus.Clicking, 2, 2,
                    "Just get the highest score you can. React to the targets the instant they appear and let the " +
                    "micro-correction finish the movement.",
                    "Consigue la mejor puntuación posible. Reacciona en el instante en que aparecen los objetivos y " +
                    "deja que la micro-corrección termine el movimiento.",
                    "You will be caught off guard sometimes. This is a straightforward reaction and adjustment drill.",
                    "A veces te pillarán desprevenido. Es un ejercicio directo de reacción y ajuste.",
                    optional: true, noteEn: "2× on the first two runs, 1× on the last two.",
                    noteEs: "2× en las dos primeras pasadas y 1× en las dos últimas."),
            },
        },

        new()
        {
            Id = "voltaic-classic",
            Name = "Voltaic Advanced Classic",
            Source = "Voltaic · KovaaK's 2.0",
            LevelEn = "Platinum and above", LevelEs = "Platino o superior",
            SourceDoc = CommunityLinks.KvKsRoutines,
            ShareCode = "KOVAAKSBOBBINGSNEAKYBLOOM",
            DescriptionEn =
                "The classic Voltaic VALORANT routine for Platinum and above: smaller targets than Easy, more target " +
                "switching, movement one-taps and reactive clicking. The core is about 60 minutes; the starred steps " +
                "extend it to 100.",
            DescriptionEs =
                "La rutina clásica de Voltaic para VALORANT desde Platino: objetivos más pequeños que en Easy, más " +
                "cambio de objetivos, one-taps en movimiento y clicking reactivo. El núcleo son unos 60 minutos; los " +
                "pasos marcados la alargan hasta 100.",
            CreditEn = "Routine by Voltaic (owner: sini), from the official Voltaic KvKs document.",
            CreditEs = "Rutina de Voltaic (responsable: sini), del documento oficial de Voltaic para KvKs.",
            Steps =
            {
                Reps("Thin Gauntlet / Centering I", "trackv:sc-voltaic-thin-gauntlet", RoutineFocus.Tracking, 3, 5f / 3f,
                    "Three runs or five minutes of smooth tracking. Keep the wrist loose and follow the bot with the " +
                    "arm; this is a warm-up for the hand, not a score attempt.",
                    "Tres pasadas o cinco minutos de tracking suave. Mantén la muñeca suelta y sigue al bot con el " +
                    "brazo; es un calentamiento para la mano, no una búsqueda de récord.",
                    "Smoothness carries over to spray control and crosshair movement, even if VALORANT has little " +
                    "tracking.",
                    "La suavidad se traslada al control del spray y al movimiento de la mira, aunque VALORANT tenga " +
                    "poco tracking.", optional: true),
                Step("patTargetSwitch small no reload / patTargetSwitch Small", "flickx:sc-voltaic-pat-target-switch-small", RoutineFocus.Switching, 10,
                    "Heads only, never bodies. Switch as fast as possible between moving targets and hold fire as much " +
                    "as you can. Eliminate as many targets as possible.",
                    "Solo cabezas, nunca cuerpos. Cambia lo más rápido posible entre objetivos móviles y mantén el " +
                    "disparo todo lo que puedas. Elimina tantos objetivos como puedas.",
                    "Fast target acquisition and switching between small moving targets.",
                    "Adquisición rápida y cambio entre objetivos móviles pequeños."),
                Step("Pokeball Frenzy Auto Small 60s / Small Wide", "flickx:sc-voltaic-pokeball-frenzy-small", RoutineFocus.Switching, 10,
                    "Hold fire and clear static small targets as fast as possible. Think about the next target before " +
                    "you finish the current one.",
                    "Mantén el disparo y limpia objetivos pequeños estáticos lo más rápido posible. Piensa en el " +
                    "siguiente objetivo antes de terminar el actual.",
                    "Speed and clean lines when several small targets are visible.",
                    "Velocidad y líneas limpias cuando hay varios objetivos pequeños a la vista."),
                Step("voxTargetClick 20% small / Pasu Small Reload / patTargetClick small 1shot", "flickx:sc-voltaic-vox-target-click-small",
                    RoutineFocus.Clicking, 10,
                    "Fast one-tap switches: acquire, click, move on. Do not drag the crosshair; each shot is its own " +
                    "small flick.",
                    "Cambios rápidos de un tap: adquiere, haz clic y sigue. No arrastres la mira; cada disparo es su " +
                    "propio flick pequeño.",
                    "One-tap target acquisition and click timing on small targets.",
                    "Adquisición de objetivos de un tap y timing de clic sobre objetivos pequeños."),
                Step("1w3ts / 1w4ts reload", "flickx:sc-voltaic-1w3ts", RoutineFocus.Clicking, 10,
                    "Hit as many targets as you can. The extra targets make you plan the next shot while you finish " +
                    "the current one. If small flicks are hard, use 1w2t smallflicks small 60s.",
                    "Golpea tantos objetivos como puedas. Los objetivos extra te obligan a planear el siguiente " +
                    "disparo mientras terminas el actual. Si los flicks pequeños te cuestan, usa 1w2t smallflicks small 60s.",
                    "Long flicks plus multiple targets: the bridge between a flick and a target switch.",
                    "Flicks largos con varios objetivos: el puente entre un flick y un cambio de objetivo."),
                Step("Floating Heads Timing 400% / tamSpeed 2bpes", "flickx:sc-cartoon-floating-heads-400", RoutineFocus.Clicking, 10,
                    "One tap per moving head and no trail-chasing. tamSpeed focuses more on holding an angle, so " +
                    "keep the crosshair still and let the head come to it.",
                    "Un tap por cabeza móvil y sin perseguir la estela. tamSpeed se centra más en mantener un ángulo: " +
                    "deja la mira quieta y que la cabeza venga a ella.",
                    "One-tapping moving heads, the shot you need against an enemy swinging wide.",
                    "One-taps a cabezas móviles, el disparo que necesitas contra un rival que asoma amplio."),
                Step("voxTargetClick Easy Strafe / Pasu Dodge EES 180", "flickx:sc-voltaic-vox-target-click-strafe", RoutineFocus.Movement, 5,
                    "Turn on the movement bar, strafe and counter-strafe, and tap when the crosshair settles on the " +
                    "head. For more challenge use Pistol Strafe Gallery Sparky Small.",
                    "Activa la barra de movimiento, haz strafe y counter-strafe, y dispara cuando la mira se asiente " +
                    "en la cabeza. Para más reto usa Pistol Strafe Gallery Sparky Small.",
                    "Movement one-taps and the timing of stopping to shoot.",
                    "One-taps en movimiento y el timing de frenar para disparar.", optional: true),
                Step("Reflex Micro++ Flick Reload Small", "reflex:sc-voltaic-reflex-micro-flick-small", RoutineFocus.Clicking, 10,
                    "React and correct as fast as possible. Do not anticipate; the eyes find the target first.",
                    "Reacciona y corrige lo más rápido posible. No anticipes; los ojos encuentran el objetivo primero.",
                    "Quick adjustments when surprised, with smaller targets than the Easy routine.",
                    "Ajustes rápidos cuando te sorprenden, con objetivos más pequeños que en la rutina Easy.", optional: true),
                Step("Valorant Microshot Speed Small", "microx:sc-voltaic-microshot-speed-small", RoutineFocus.Micro, 10,
                    "Small, fast corrections onto each head. Stop exactly on target and avoid unnecessary movement.",
                    "Correcciones pequeñas y rápidas sobre cada cabeza. Frena exactamente en el objetivo y evita " +
                    "movimientos innecesarios.",
                    "Micro-corrections on small targets, the most common VALORANT adjustment.",
                    "Micro-correcciones sobre objetivos pequeños, el ajuste más común en VALORANT.", optional: true),
                Step("Reactive Clicking Hard", "reflex:sc-voltaic-reactive-clicking-hard", RoutineFocus.Clicking, 10,
                    "React to each pop-up and click before the 0.4 s window closes; reset immediately for the next. If " +
                    "that is too fast, use Mixed Reflex or Reflex Micro.",
                    "Reacciona a cada aparición y haz clic antes de que se cierre la ventana de 0,4 s; recolócate al " +
                    "instante para la siguiente. Si es demasiado rápido, usa Mixed Reflex o Reflex Micro.",
                    "Reactive clicking under pressure with short windows and faster targets.",
                    "Clicking reactivo bajo presión con ventanas cortas y objetivos más rápidos.", optional: true),
            },
        },

        new()
        {
            Id = "cartoon-warmup",
            Name = "VT Cartoon VALORANT Warm-up",
            Source = "Voltaic × Cartoon · Aim Lab",
            LevelEn = "Any level · before ranked", LevelEs = "Cualquier nivel · antes de ranked",
            SourceDoc = CommunityLinks.AimlabsRoutines,
            DescriptionEn =
                "Cartoon's 12-minute warm-up from the Voltaic Aim Lab document. Short, intensive and built around " +
                "micro-corrections, click timing and fast flicks. Cartoon recommends it followed by deathmatch, not " +
                "as a replacement for playing.",
            DescriptionEs =
                "El calentamiento de 12 minutos de Cartoon del documento de Voltaic para Aim Lab. Corto, intenso y " +
                "centrado en micro-correcciones, timing de clic y flicks rápidos. Cartoon recomienda combinarlo con " +
                "deathmatch, no usarlo en lugar de jugar.",
            CreditEn = "Warm-up by Cartoon for Voltaic. Scenario descriptions from the Voltaic Aim Lab document.",
            CreditEs = "Calentamiento de Cartoon para Voltaic. Descripciones de escenarios del documento de Voltaic para Aim Lab.",
            Steps =
            {
                Step("VT berryTS Valorant", "flickx:sc-vt-berryts", RoutineFocus.Switching, 3,
                    "Hold fire for the whole task. Track the target until it dies, time the switch and land the " +
                    "initial flick. Cartoon suggests binding fire to Left Ctrl so the mouse keeps its normal grip.",
                    "Mantén el disparo durante todo el ejercicio. Sigue al objetivo hasta matarlo, mide el cambio y " +
                    "clava el flick inicial. Cartoon sugiere poner el disparo en Ctrl izquierdo para que el ratón " +
                    "mantenga su agarre normal.",
                    "Target acquisition and spray transfer when enemies push the site you hold.",
                    "Adquisición de objetivos y spray transfer cuando los enemigos empujan el sitio que defiendes."),
                Step("VT Sky Dots Micro 30% Smaller", "microx:sc-vt-sky-dots-micro", RoutineFocus.Micro, 3,
                    "Chain an initial flick with a micro-correction, but lead the moving target slightly and click " +
                    "when it meets your crosshair. Do not chase it with the crosshair.",
                    "Encadena un flick inicial con una micro-corrección, pero adelanta ligeramente al objetivo móvil y " +
                    "haz clic cuando se encuentre con tu mira. No lo persigas con la mira.",
                    "Micro-flicks between two moving targets without wasting movement.",
                    "Micro-flicks entre dos objetivos móviles sin desperdiciar movimiento."),
                Step("VT Floating Dots Goated Valorant", "flickx:sc-vt-floating-dots", RoutineFocus.Clicking, 3,
                    "Read each target, flick to it and click as it crosses the crosshair. Keep the movement clean and " +
                    "do not follow the trail.",
                    "Lee cada objetivo, haz flick y dispara cuando cruce la mira. Mantén el movimiento limpio y no " +
                    "sigas la estela.",
                    "Reading, click timing and precision on fast, long-strafing targets.",
                    "Lectura, timing de clic y precisión sobre objetivos rápidos que hacen strafe largo."),
                Step("VT 1w2ts Smallflicks Valorant", "microx:sc-vt-1w2ts-smallflicks", RoutineFocus.Micro, 3,
                    "Initial flick plus micro-correction, with the next target in mind. When the two spawns are close " +
                    "enough, skip the micro-correction and click directly.",
                    "Flick inicial más micro-corrección, con el siguiente objetivo en mente. Cuando los dos spawns " +
                    "estén lo bastante cerca, sáltate la micro-corrección y haz clic directo.",
                    "Small accurate flicks and fast crosshair readjustment between two positions.",
                    "Flicks pequeños y precisos, y recolocación rápida de la mira entre dos posiciones."),
            },
        },

        new()
        {
            Id = "voltaic-valorant-advanced",
            Name = "Voltaic VALORANT Advanced",
            Source = "Voltaic × Cartoon, Trustemix, bardOZ · Aim Lab",
            LevelEn = "Diamond and above · playable at any level",
            LevelEs = "Diamante o superior · jugable en cualquier nivel",
            SourceDoc = CommunityLinks.AimlabsRoutines,
            DescriptionEn =
                "The 40-minute advanced Aim Lab routine from Voltaic. It isolates mouse control specific to VALORANT: " +
                "reactive clicking, small flicks, close tracking, target switching and micro-corrections. The source " +
                "marks four tracking/switching steps as optional +25% sensitivity; this trainer applies that boost " +
                "automatically and keeps those runs out of your personal bests.",
            DescriptionEs =
                "La rutina avanzada de 40 minutos de Voltaic para Aim Lab. Aísla el control del ratón específico de " +
                "VALORANT: clicking reactivo, flicks pequeños, tracking cercano, cambio de objetivos y " +
                "micro-correcciones. La fuente marca cuatro pasos de tracking/switching como +25% de sensibilidad " +
                "opcional; este entrenador aplica esa subida automáticamente y mantiene esas pasadas fuera de tus récords.",
            CreditEn = "Routine recommended and created with VT Cartoon, Trustemix and bardOZ for Voltaic.",
            CreditEs = "Rutina recomendada y creada con VT Cartoon, Trustemix y bardOZ para Voltaic.",
            Steps =
            {
                Step("VT Reactive Click Widewall Valorant", "reflex:sc-vt-reactive-click-widewall", RoutineFocus.Clicking, 4,
                    "React to heads that appear wide, flick and click before they vanish. Keep the crosshair still " +
                    "between pops and let your eyes find the target first.",
                    "Reacciona a las cabezas que aparecen abiertas, haz flick y clic antes de que desaparezcan. Mantén " +
                    "la mira quieta entre apariciones y deja que los ojos encuentren el objetivo primero.",
                    "Reacting to wide pop-ups and landing the click before the window closes.",
                    "Reaccionar a apariciones abiertas y acertar el clic antes de que se cierre la ventana."),
                Step("VT Valorant Smallflicks", "microx:sc-vt-valorant-smallflicks", RoutineFocus.Micro, 4,
                    "Headshots only. Acquire each target and move in one clean horizontal motion from one head to " +
                    "the next.",
                    "Solo headshots. Adquiere cada objetivo y muévete en un movimiento horizontal limpio de una cabeza " +
                    "a la siguiente.",
                    "Target acquisition and precision on horizontal angles, the most common VALORANT peek.",
                    "Adquisición y precisión en ángulos horizontales, el peek más común de VALORANT."),
                Step("VT Close Range Valorant Tracking", "trackv:sc-vt-close-range-tracking", RoutineFocus.Tracking, 3,
                    "Read the bot's movement and commit to its direction in one smooth motion instead of many small " +
                    "corrections. React fast to direction changes.",
                    "Lee el movimiento del bot y comprométete con su dirección en un movimiento suave en vez de muchas " +
                    "correcciones pequeñas. Reacciona rápido a los cambios de dirección.",
                    "Horizontal smoothness and reactivity for close-range fights.",
                    "Suavidad horizontal y reactividad para combates a corta distancia.",
                    sensNoteEn: "The source offers +25% sens; applied automatically on this step.",
                    sensNoteEs: "La fuente ofrece +25% de sens; se aplica automáticamente en este paso."),
                Step("VT ww3t Pokeball Valorant", "flickx:sc-vt-ww3t-pokeball", RoutineFocus.Switching, 3,
                    "Hold fire, switch as fast as possible and keep a straight line from target to target. Micro-adjust " +
                    "after the initial flick only if needed, with the next target in mind.",
                    "Mantén el disparo, cambia lo más rápido posible y mantén una línea recta entre objetivos. " +
                    "Micro-corrige tras el flick inicial solo si hace falta, con el siguiente objetivo en mente.",
                    "Precision and speed target acquisition with clean switches.",
                    "Adquisición de objetivos con precisión y velocidad, con cambios limpios.",
                    sensNoteEn: "The source offers +25% sens; applied automatically on this step.",
                    sensNoteEs: "La fuente ofrece +25% de sens; se aplica automáticamente en este paso."),
                Step("VT 1w2ts Pokeball Valorant", "flickx:sc-vt-1w2ts-pokeball", RoutineFocus.Switching, 3,
                    "Same as ww3t but with two small targets: hold fire, switch fast, no overflick, quick correction.",
                    "Igual que ww3t pero con dos objetivos pequeños: mantén el disparo, cambia rápido, sin overflick " +
                    "y con corrección rápida.",
                    "Precision and speed when only two targets are available.",
                    "Precisión y velocidad cuando solo hay dos objetivos.",
                    sensNoteEn: "The source offers +25% sens; applied automatically on this step.",
                    sensNoteEs: "La fuente ofrece +25% de sens; se aplica automáticamente en este paso."),
                Step("VT Sky Dots Micro 30% Smaller", "microx:sc-vt-sky-dots-micro", RoutineFocus.Micro, 3,
                    "Focus on the target closest to your crosshair to maximize efficiency; chain the flick and the " +
                    "micro-correction without visiting the other target first.",
                    "Céntrate en el objetivo más cercano a tu mira para maximizar la eficiencia; encadena el flick y " +
                    "la micro-corrección sin pasar primero por el otro objetivo.",
                    "Micro-flicks between two moving targets.",
                    "Micro-flicks entre dos objetivos móviles."),
                Step("VT Smoothsphere Valorant", "trackv:sc-vt-smoothsphere", RoutineFocus.Tracking, 3,
                    "Track the sphere precisely and minimize the number of times you reset the mouse. Use the whole " +
                    "mousepad when the sphere goes around you.",
                    "Sigue la esfera con precisión y minimiza las veces que recentras el ratón. Usa toda la alfombrilla " +
                    "cuando la esfera te rodea.",
                    "360 tracking and precise tracking at awkward angles.",
                    "Tracking de 360° y tracking preciso en ángulos incómodos.",
                    sensNoteEn: "The source offers +25% sens; applied automatically on this step.",
                    sensNoteEs: "La fuente ofrece +25% de sens; se aplica automáticamente en este paso."),
                Step("VT MPspeedTS Valorant", "flickx:sc-vt-mpspeedts", RoutineFocus.Switching, 3,
                    "Acquire each target swiftly, track it briefly and switch to the next as fast as possible. Always " +
                    "keep the next target in mind.",
                    "Adquiere cada objetivo con rapidez, síguelo brevemente y cambia al siguiente lo más rápido " +
                    "posible. Ten siempre presente el siguiente objetivo.",
                    "Reading, target acquisition and switching on targets with sporadic movement.",
                    "Lectura, adquisición y cambio sobre objetivos con movimiento esporádico."),
                Step("VT mpXY Valorant", "flickx:sc-vt-mpxy", RoutineFocus.Clicking, 4,
                    "Read each target, move in one smooth motion and click with minimal corrections. The targets move " +
                    "diagonally, so keep the crosshair level and let the target cross it.",
                    "Lee cada objetivo, muévete en un movimiento suave y haz clic con correcciones mínimas. Los " +
                    "objetivos se mueven en diagonal: mantén la mira nivelada y deja que el objetivo la cruce.",
                    "Reading, click timing and precision on diagonal movement.",
                    "Lectura, timing de clic y precisión sobre movimiento diagonal."),
                Step("VT 1w4ts VSS Valorant", "flickx:sc-vt-1w4ts-vss", RoutineFocus.Clicking, 4,
                    "Clean lines from target to target: fast large flick, quick micro-correction, next target in mind. " +
                    "Minimize wasted movement and confirm the kill before switching.",
                    "Líneas limpias entre objetivos: flick grande rápido, micro-corrección rápida y siguiente objetivo " +
                    "en mente. Minimiza el movimiento desperdiciado y confirma la muerte antes de cambiar.",
                    "Clean flicks, fast micro-corrections and kill confirmation.",
                    "Flicks limpios, micro-correcciones rápidas y confirmación de muerte."),
                Step("VT 1w2ts Smallflicks Valorant", "microx:sc-vt-1w2ts-smallflicks", RoutineFocus.Micro, 3,
                    "Keep clean lines and make sure you are on the target before clicking. The two targets are close, " +
                    "so the correction is tiny.",
                    "Mantén líneas limpias y asegúrate de estar en el objetivo antes de disparar. Los dos objetivos " +
                    "están cerca, así que la corrección es mínima.",
                    "Micro-corrections and micro-flicks, often the difference between a kill and a trade.",
                    "Micro-correcciones y micro-flicks, a menudo la diferencia entre matar o morir en el intercambio."),
                Step("VT berryTS Valorant", "flickx:sc-vt-berryts", RoutineFocus.Switching, 4,
                    "Acquire, track briefly, switch as fast as possible. Hold fire and keep the next target in mind.",
                    "Adquiere, sigue brevemente y cambia lo más rápido posible. Mantén el disparo y ten en mente el " +
                    "siguiente objetivo.",
                    "Target acquisition and spray transfer on slow strafing spheres.",
                    "Adquisición y spray transfer sobre esferas que hacen strafe lento."),
                Step("VT Floating Dots Goated Valorant", "flickx:sc-vt-floating-dots", RoutineFocus.Clicking, 4,
                    "Read the movement carefully and move with minimal jitter from one target to another. Click when " +
                    "the target meets the crosshair, not after it.",
                    "Lee el movimiento con cuidado y muévete con el mínimo temblor de un objetivo a otro. Haz clic " +
                    "cuando el objetivo se encuentre con la mira, no después.",
                    "Reading, click timing and precision on fast long-strafing targets.",
                    "Lectura, timing de clic y precisión sobre objetivos rápidos de strafe largo."),
            },
        },

        new()
        {
            Id = "cartoon-flicks",
            Name = "CRTN · Flicks básicos",
            Source = "cartoon · Kovaak's 2.0",
            LevelEn = "Intermediate · 8 scenarios · 4 reps each", LevelEs = "Intermedio · 8 escenarios · 4 repeticiones",
            SourceDoc = CommunityLinks.CartoonGuide,
            ShareCode = "KovaaKsReloadingMaroonLamp",
            DescriptionEn =
                "cartoon's basic-flicks playlist: 32 minutes, four repetitions of each of its eight scenarios. Every " +
                "task is reproduced here as a real CA-VAL-AIM drill with the same name and focus; the Kovaak's share " +
                "code is kept so you can also play the original. Follow the guide's rules: clean lines, one shot per " +
                "target in clicking scenarios, no trail-chasing and no chasing 100% accuracy.",
            DescriptionEs =
                "La playlist de flicks básicos de cartoon: 32 minutos, cuatro repeticiones de cada uno de sus ocho " +
                "escenarios. Cada tarea está reproducida aquí como un ejercicio real de CA-VAL-AIM con el mismo nombre " +
                "y enfoque; se conserva el código de Kovaak's para que también puedas jugar la original. Respeta las " +
                "reglas de la guía: líneas limpias, un disparo por objetivo en clicking, sin perseguir estelas y sin " +
                "buscar el 100% de precisión.",
            CreditEn = "Routine and guide by cartoon (Twitch/Twitter/YouTube). Scenario names from cartoon's public " +
                       "playlist; share code from cartoon's guide.",
            CreditEs = "Rutina y guía de cartoon (Twitch/Twitter/YouTube). Nombres de escenario de la playlist pública " +
                       "de cartoon; código de la guía de cartoon.",
            Steps =
            {
                CartoonStep(ScenarioDrills.CartoonFlicks[0], RoutineFocus.Clicking, 60,
                    "Flick between the two heads without dropping or lifting the crosshair. One shot per target, then " +
                    "reset to the other head.",
                    "Haz flick entre las dos cabezas sin bajar ni subir la mira. Un disparo por objetivo y vuelve a la " +
                    "otra cabeza.",
                    "Horizontal flicks: the wrist stays on the head line and the arm stops dead on target.",
                    "Flicks horizontales: la muñeca se queda en la línea de la cabeza y el brazo frena en seco."),
                CartoonStep(ScenarioDrills.CartoonFlicks[1], RoutineFocus.Switching, 60,
                    "Acquire the nearest head, kill it and move to the next before it settles. Four reps keep the pace " +
                    "honest.",
                    "Adquiere la cabeza más cercana, elimínala y pasa a la siguiente antes de que se asiente. Las cuatro " +
                    "repeticiones mantienen el ritmo honesto.",
                    "Target switching on larger heads: the first shot should not need a second correction.",
                    "Cambio de objetivo con cabezas grandes: el primer disparo no debería necesitar una segunda corrección."),
                CartoonStep(ScenarioDrills.CartoonFlicks[2], RoutineFocus.Switching, 60,
                    "Four small targets across a wide wall. Plan the route left-to-right and stop on every head instead " +
                    "of dragging through it.",
                    "Cuatro objetivos pequeños en una pared amplia. Planifica la ruta de izquierda a derecha y frena en " +
                    "cada cabeza en vez de arrastrar la mira.",
                    "Wide-wall clicking: route planning and clean stops at the edge of your range.",
                    "Clicking en pared amplia: planificación de ruta y paradas limpias en el borde de tu alcance."),
                CartoonStep(ScenarioDrills.CartoonFlicks[3], RoutineFocus.Clicking, 60,
                    "Two targets, one shot each. The first flick has to land and the second has to be just as clean.",
                    "Dos objetivos, un disparo a cada uno. El primer flick tiene que llegar y el segundo tiene que ser " +
                    "igual de limpio.",
                    "The simplest switch: no spray, no correction, just two precise clicks.",
                    "El cambio más simple: sin spray ni corrección, solo dos clics precisos."),
                CartoonStep(ScenarioDrills.CartoonFlicks[4], RoutineFocus.Switching, 60,
                    "Forgiving floating heads: read the drift, click when the head meets the dot and reset for the next.",
                    "Cabezas flotantes permisivas: lee la deriva, haz clic cuando la cabeza se encuentre con el punto y " +
                    "recoloca para la siguiente.",
                    "Reading a moving head before the click, the base of reactive switching.",
                    "Lectura de una cabeza móvil antes del clic, la base del cambio reactivo."),
                CartoonStep(ScenarioDrills.CartoonFlicks[5], RoutineFocus.Clicking, 60,
                    "Moving heads at 400% speed: time the click as the head crosses the crosshair. Never chase the trail.",
                    "Cabezas móviles al 400% de velocidad: temporiza el clic cuando la cabeza cruza la mira. Nunca " +
                    "persigas la estela.",
                    "Click timing on a fast crossing target: the shot is released before the head arrives.",
                    "Timing de clic sobre un objetivo rápido que cruza: el disparo sale antes de que llegue la cabeza."),
                CartoonStep(ScenarioDrills.CartoonFlicks[6], RoutineFocus.Clicking, 60,
                    "Static angelic targets: one tap per head, then a fast reset to the next target.",
                    "Objetivos angelic estáticos: un tap por cabeza y una recolocación rápida al siguiente objetivo.",
                    "Static clicking with a clean reset: accuracy without slowing down.",
                    "Clicking estático con recolocación limpia: precisión sin bajar la velocidad."),
                CartoonStep(ScenarioDrills.CartoonFlicks[7], RoutineFocus.Clicking, 60,
                    "Pasu-style pair with a forgiving size: click, confirm the hit and move on immediately.",
                    "Pareja estilo Pasu con tamaño permisivo: haz clic, confirma el impacto y pasa al siguiente de " +
                    "inmediato.",
                    "Confirmation and pace: the shot is finished before the next flick starts.",
                    "Confirmación y ritmo: el disparo termina antes de empezar el siguiente flick."),
            },
        },

        new()
        {
            Id = "cartoon-micro",
            Name = "CRTN · Microajustes",
            Source = "cartoon · Kovaak's 2.0",
            LevelEn = "Intermediate · 12 scenarios · 4 reps each", LevelEs = "Intermedio · 12 escenarios · 4 repeticiones",
            SourceDoc = CommunityLinks.CartoonGuide,
            ShareCode = "KovaaKsScreamingMehCt",
            DescriptionEn =
                "cartoon's micro-adjustment playlist: 36 minutes, four repetitions of each of its twelve scenarios. Each " +
                "task is a real CA-VAL-AIM drill here, with the same name and focus; the Kovaak's share code is kept " +
                "for the original. Clean lines, one shot per target, no trail-chasing and no tension in the wrist or arm.",
            DescriptionEs =
                "La playlist de microajustes de cartoon: 36 minutos, cuatro repeticiones de cada uno de sus doce " +
                "escenarios. Cada tarea es aquí un ejercicio real de CA-VAL-AIM, con el mismo nombre y enfoque; se " +
                "conserva el código de Kovaak's para la original. Líneas limpias, un disparo por objetivo, sin " +
                "perseguir estelas y sin tensar muñeca ni brazo.",
            CreditEn = "Routine and guide by cartoon. Scenario names from cartoon's public playlist; share code from " +
                       "cartoon's guide.",
            CreditEs = "Rutina y guía de cartoon. Nombres de escenario de la playlist pública de cartoon; código de la " +
                       "guía de cartoon.",
            Steps =
            {
                CartoonStep(ScenarioDrills.CartoonMicro[0], RoutineFocus.Switching, 45,
                    "Four small targets on a wide wall: micro-correct after each switch instead of over-flicking past " +
                    "the head.",
                    "Cuatro objetivos pequeños en una pared amplia: micro-corrige tras cada cambio en vez de pasarte " +
                    "de la cabeza.",
                    "The correction after a switch is the skill, not another full flick.",
                    "La corrección después del cambio es la habilidad, no otro flick completo."),
                CartoonStep(ScenarioDrills.CartoonMicro[1], RoutineFocus.Micro, 45,
                    "Small target switches with a short correction: the first movement is the flick, the second is the " +
                    "tiny settle.",
                    "Cambios pequeños con una corrección corta: el primer movimiento es el flick y el segundo es el " +
                    "ajuste mínimo.",
                    "Separating the flick from the correction keeps the line short.",
                    "Separar el flick de la corrección mantiene la línea corta."),
                CartoonStep(ScenarioDrills.CartoonMicro[2], RoutineFocus.Tracking, 45,
                    "A wandering tracking target: follow each direction change with the arm and keep the dot on it.",
                    "Un objetivo de tracking errante: sigue cada cambio de dirección con el brazo y mantén el punto " +
                    "sobre él.",
                    "Smooth tracking through course changes, not a series of re-flicks.",
                    "Tracking suave a través de cambios de rumbo, no una sucesión de re-flicks."),
                CartoonStep(ScenarioDrills.CartoonMicro[3], RoutineFocus.Micro, 45,
                    "Two narrow targets with more time to kill: settle on the head and make the correction count.",
                    "Dos objetivos estrechos con más tiempo para eliminar: asiéntate en la cabeza y haz que la " +
                    "corrección cuente.",
                    "Higher TTK gives you time to place the second shot instead of spraying.",
                    "Un TTK más alto te da tiempo para colocar el segundo disparo en vez de rociar."),
                CartoonStep(ScenarioDrills.CartoonMicro[4], RoutineFocus.Clicking, 45,
                    "A moving head at high speed: click when it meets the crosshair, not after it has passed.",
                    "Una cabeza móvil a gran velocidad: haz clic cuando se encuentra con la mira, no después de que " +
                    "haya pasado.",
                    "Click timing on a fast head: the eye leads the hand.",
                    "Timing de clic sobre una cabeza rápida: el ojo guía a la mano."),
                CartoonStep(ScenarioDrills.CartoonMicro[5], RoutineFocus.Micro, 45,
                    "Two targets on one horizontal line: a clean side-to-side micro-flick, with no vertical noise.",
                    "Dos objetivos en una misma línea horizontal: un micro-flick lateral limpio, sin ruido vertical.",
                    "Keeping the crosshair level turns a flick into a tiny correction.",
                    "Mantener la mira nivelada convierte un flick en una corrección mínima."),
                CartoonStep(ScenarioDrills.CartoonMicro[6], RoutineFocus.Micro, 45,
                    "Three-target Pasu pattern reduced to the last few degrees: chain tiny corrections without lifting " +
                    "the arm.",
                    "Patrón Pasu de tres objetivos reducido a los últimos grados: encadena correcciones mínimas sin " +
                    "levantar el brazo.",
                    "Micro-chaining: each target is a few pixels away, not a new flick.",
                    "Encadenar micro: cada objetivo está a unos píxeles, no es un flick nuevo."),
                CartoonStep(ScenarioDrills.CartoonMicro[7], RoutineFocus.Switching, 45,
                    "Controlled switching: one clean flick, a short confirm, then the next target.",
                    "Cambio controlado: un flick limpio, una confirmación corta y el siguiente objetivo.",
                    "Control means the switch never turns into a drag.",
                    "Control significa que el cambio nunca se convierte en un arrastre."),
                CartoonStep(ScenarioDrills.CartoonMicro[8], RoutineFocus.Switching, 45,
                    "The hard version: faster drift and smaller heads, so the correction has to be immediate.",
                    "La versión difícil: deriva más rápida y cabezas más pequeñas, así que la corrección tiene que ser " +
                    "inmediata.",
                    "The same switch under time pressure: no room for a second thought.",
                    "El mismo cambio bajo presión: no hay sitio para un segundo pensamiento."),
                CartoonStep(ScenarioDrills.CartoonMicro[9], RoutineFocus.Micro, 45,
                    "Two small heads nearly level: keep the crosshair on the line and correct sideways.",
                    "Dos cabezas pequeñas casi a la misma altura: mantén la mira en la línea y corrige de lado.",
                    "Horizontal micro-adjustments with a target that barely moves.",
                    "Microajustes horizontales con un objetivo que apenas se mueve."),
                CartoonStep(ScenarioDrills.CartoonMicro[10], RoutineFocus.Switching, 45,
                    "A wide three-target wall at half size: the flick still has to stop exactly on the head.",
                    "Una pared amplia de tres objetivos a mitad de tamaño: el flick sigue teniendo que frenar exacto " +
                    "en la cabeza.",
                    "Wide angles with small heads: precision at the end of the movement.",
                    "Ángulos amplios con cabezas pequeñas: precisión al final del movimiento."),
                CartoonStep(ScenarioDrills.CartoonMicro[11], RoutineFocus.Micro, 45,
                    "Two heads 30% smaller than the original: the last correction is a few pixels, not a new flick.",
                    "Dos cabezas un 30% más pequeñas que la original: la última corrección son unos píxeles, no un " +
                    "flick nuevo.",
                    "Pixel-level settle: the difference between a hit and a miss is one small nudge.",
                    "Asentamiento a nivel de píxel: la diferencia entre acierto y fallo es un pequeño empujón."),
            },
        },
        new()
        {
            Id = "voltaic-ingame",
            Name = "VALORANT · In-game practice",
            Source = "Voltaic · VALORANT",
            LevelEn = "Any level · 10–60 minutes", LevelEs = "Cualquier nivel · 10–60 minutos",
            SourceDoc = CommunityLinks.KvKsRoutines,
            Playable = true,
            DescriptionEn =
                "The in-game section of Voltaic's VALORANT document, with a built-in adaptation for every task. You " +
                "can play it as written in VALORANT, or press PLAY ROUTINE to train the same mechanics here: spray " +
                "transfer, long-range taps, site clearing and deathmatch. The original in-game instructions are kept " +
                "in each step.",
            DescriptionEs =
                "La sección in-game del documento de Voltaic para VALORANT, con una adaptación interna para cada " +
                "tarea. Puedes jugarla tal cual en VALORANT o pulsar JUGAR RUTINA para entrenar aquí las mismas " +
                "mecánicas: spray transfer, taps largos, limpieza de sitio y deathmatch. Las instrucciones originales " +
                "in-game se conservan en cada paso.",
            CreditEn = "In-game section by Voltaic; bridge idea credited to Zeeq in the source document.",
            CreditEs = "Sección in-game de Voltaic; la idea del puente se atribuye a Zeeq en el documento original.",
            Steps =
            {
                Step("Practice range — taps / spray transfer", "spray_transfer", RoutineFocus.InGame, 10,
                    "Start the VALORANT practice range, raise the target spawn speed or enable strafing for 50–100 " +
                    "targets, and either tap heads or practise headshot spray transfers. Move and counter-strafe " +
                    "between shots; add a Sage wall to practise corner peeks. Here the same task is played on the " +
                    "spray-transfer drill.",
                    "Entra en el campo de práctica de VALORANT, sube la velocidad de aparición o activa el strafe para " +
                    "50–100 objetivos y practica taps a la cabeza o spray transfers a la cabeza. Muévete y haz " +
                    "counter-strafe entre disparos; añade un muro de Sage para practicar asomarse por esquinas. Aquí " +
                    "la misma tarea se juega en el ejercicio de spray transfer.",
                    "Weapon control and crosshair placement in the real game; the built-in drill isolates the transfer.",
                    "Control del arma y colocación de la mira en el juego real; el ejercicio interno aísla el transfer.",
                    recoil: true),
                Step("Bridge — taps / spray transfer", "longtaps", RoutineFocus.InGame, 10,
                    "Go to the bridge in the practice range and get creative with the angles: flick to heads or " +
                    "spray-transfer between targets. Move and counter-strafe, and use a Sage wall for corner peeks. " +
                    "Here the long-range tap drill reproduces the angle changes.",
                    "Ve al puente del campo de práctica y sé creativo con los ángulos: flicks a la cabeza o spray " +
                    "transfers entre objetivos. Muévete y haz counter-strafe, y usa un muro de Sage para asomarte. " +
                    "Aquí el ejercicio de taps largos reproduce los cambios de ángulo.",
                    "A less controlled environment than the range: it forces you to read distance and verticality.",
                    "Un entorno menos controlado que el campo: te obliga a leer distancia y verticalidad.",
                    optional: true, recoil: true),
                Step("Custom game — clearing bombsites", "siteclear", RoutineFocus.InGame, 20,
                    "Play a custom game alone or with a friend and clear bomb sites with deliberate crosshair " +
                    "placement. Check every common angle at head height, clear near-to-far and pre-aim the next angle " +
                    "while you move. Here Site Clear reproduces the route.",
                    "Juega una partida personalizada solo o con un amigo y limpia sitios con colocación de mira " +
                    "deliberada. Revisa cada ángulo común a la altura de la cabeza, limpia de cerca a lejos y " +
                    "pre-apunta el siguiente ángulo mientras te mueves. Aquí Limpieza de sitio reproduce la ruta.",
                    "Crosshair placement and site-clearing habits, the part of aim that wins rounds without a flick.",
                    "Colocación de la mira y hábitos de limpieza de sitio, la parte de la puntería que gana rondas sin " +
                    "necesitar un flick.",
                    recoil: true),
                Step("Headshot deathmatch — taps / spray transfer", "deathmatch", RoutineFocus.InGame, 20,
                    "Play deathmatch and focus on headshots only: tap or spray-transfer, never spray the body. " +
                    "Counter-strafe before every shot and reset your crosshair to the next angle. Here Deathmatch " +
                    "reproduces the same pressure with bots.",
                    "Juega deathmatch y céntrate solo en la cabeza: tap o spray transfer, nunca dispares al cuerpo. " +
                    "Haz counter-strafe antes de cada disparo y recoloca la mira al siguiente ángulo. Aquí Deathmatch " +
                    "reproduce la misma presión con bots.",
                    "The bridge between the training and ranked: real players, real movement and real weapon control.",
                    "El puente entre el entrenamiento y la ranked: jugadores, movimiento y control de arma reales.",
                    optional: true, recoil: true),
            },
        },

        new()
        {
            Id = "recoil-lab",
            Name = "RECOIL · Laboratorio de spray",
            Source = "CA-VAL-AIM · entrenamiento propio",
            LevelEn = "Any level · 30 minutes", LevelEs = "Cualquier nivel · 30 minutos",
            DescriptionEn =
                "A focused spray session: full Vandal and Phantom magazines, then spray transfers, then shorter " +
                "bursts to finish. Every step uses a real magazine and camera climb, exactly like a match; the " +
                "global ammo and recoil options do not change these drills.",
            DescriptionEs =
                "Una sesión centrada en el spray: cargadores completos de Vandal y Phantom, después spray transfers y " +
                "al final ráfagas cortas. Cada paso usa cargador real y retroceso activo, igual que en partida; las " +
                "opciones globales de munición y retroceso no cambian estos ejercicios.",
            CreditEn = "Routine by CA-VAL-AIM. Vandal and Phantom patterns follow Riot's published spray values.",
            CreditEs = "Rutina de CA-VAL-AIM. Los patrones de Vandal y Phantom siguen los valores de spray publicados por Riot.",
            Steps =
            {
                Step("Vandal Spray", "spray_vandal", RoutineFocus.Recoil, 5,
                    "Empty the whole 25-round magazine on the dummy. Pull down first, then counter the drift; do not " +
                    "stop halfway. Read the chart after every spray and correct one thing at a time.",
                    "Vacía el cargador completo de 25 balas sobre el muñeco. Tira hacia abajo primero y corrige la " +
                    "deriva después; no pares a mitad. Lee el gráfico tras cada spray y corrige una sola cosa cada vez.",
                    "The first nine bullets decide a VALORANT duel: if the initial pull is late, every later correction " +
                    "is compensating for it.",
                    "Las nueve primeras balas deciden un duelo en VALORANT: si el tirón inicial llega tarde, cada " +
                    "corrección posterior compensa ese error.",
                    recoil: true),
                Step("Phantom Spray", "spray_phantom", RoutineFocus.Recoil, 5,
                    "Same full-magazine work with the Phantom's 30 rounds. Keep the same relaxed grip: the Phantom " +
                    "kicks less, so the mistake is usually pulling too hard.",
                    "El mismo trabajo de cargador completo con las 30 balas del Phantom. Mantén el agarre relajado: el " +
                    "Phantom patalea menos, así que el error habitual es tirar demasiado fuerte.",
                    "Switching rifles changes the recoil rhythm; you need both patterns under control, not just your " +
                    "favourite one.",
                    "Cambiar de rifle cambia el ritmo del retroceso; necesitas controlar ambos patrones, no solo tu " +
                    "favorito.",
                    recoil: true),
                Step("Spray Transfer", "spray_transfer", RoutineFocus.Recoil, 6,
                    "Kill every bot in one continuous spray. Do not release the trigger between targets: move the " +
                    "crosshair while the spray is still alive and let the pattern do the rest.",
                    "Mata a todos los bots en un solo spray continuo. No sueltes el gatillo entre objetivos: mueve la " +
                    "mira mientras el spray sigue vivo y deja que el patrón haga el resto.",
                    "This is the site-execute spray: several enemies peek together and a reload would lose the round.",
                    "Es el spray de ejecución de sitio: varios enemigos asoman juntos y recargar te costaría la ronda.",
                    recoil: true),
                Step("Vandal Spray", "spray_vandal", RoutineFocus.Recoil, 4,
                    "Now use short bursts: 8–12 bullets, release, reset the crosshair to the head and fire again. Stop " +
                    "as soon as the burst stops being accurate.",
                    "Ahora usa ráfagas cortas: 8–12 balas, suelta, recoloca la mira a la cabeza y vuelve a disparar. " +
                    "Para en cuanto la ráfaga deje de ser precisa.",
                    "Most real sprays end before the magazine does; knowing where to stop is worth more than holding " +
                    "the button longer.",
                    "Casi todos los sprays reales terminan antes que el cargador; saber dónde parar vale más que seguir " +
                    "apretando.",
                    recoil: true),
                Step("Spray Transfer", "spray_transfer", RoutineFocus.Recoil, 6,
                    "Repeat the transfer with cleaner lines: the first bullet goes to the head, the rest stay on the " +
                    "body. If you run dry with someone alive, the next run starts with a reload instead of a spray.",
                    "Repite el transfer con líneas más limpias: la primera bala va a la cabeza y el resto se queda en " +
                    "el cuerpo. Si te quedas sin balas con alguien vivo, la siguiente pasada empieza recargando.",
                    "Weapon control under the clock: the transfer has to survive the magazine, not just the first kill.",
                    "Control del arma contra el reloj: el transfer tiene que sobrevivir al cargador, no solo a la " +
                    "primera muerte.",
                    recoil: true),
                Step("Phantom Spray", "spray_phantom", RoutineFocus.Recoil, 4,
                    "Finish with Phantom bursts of 8–12 rounds at a longer distance. Keep the crosshair on the chest " +
                    "and let the pattern climb through the head.",
                    "Termina con ráfagas de 8–12 balas de Phantom a más distancia. Mantén la mira en el pecho y deja " +
                    "que el patrón suba por la cabeza.",
                    "The Phantom keeps its accuracy further into the spray; use that to hold longer angles.",
                    "El Phantom conserva la precisión más tiempo dentro del spray; aprovéchalo para cubrir ángulos más " +
                    "largos.",
                    recoil: true),
            },
        },

        new()
        {
            Id = "deathmatch-lab",
            Name = "DEATHMATCH · Presión real",
            Source = "CA-VAL-AIM · entrenamiento propio",
            LevelEn = "Any level · 36 minutes", LevelEs = "Cualquier nivel · 36 minutos",
            DescriptionEn =
                "A full deathmatch block with one goal per run: entry, duels, kill-to-kill resets, movement and a " +
                "final map to win. Every step uses a real magazine and camera climb, like the real mode; those " +
                "settings cannot be changed during the drill.",
            DescriptionEs =
                "Un bloque completo de deathmatch con un objetivo por pasada: entrada, duelos, recolocación tras " +
                "cada muerte, movimiento y un mapa final que ganar. Cada paso usa cargador real y retroceso activo, " +
                "como el modo real; esos ajustes no se pueden cambiar durante el ejercicio.",
            CreditEn = "Routine by CA-VAL-AIM. Deathmatch uses the built-in VALORANT map blockouts and bot duel model.",
            CreditEs = "Rutina de CA-VAL-AIM. El deathmatch usa los bloqueos de mapa de VALORANT y el modelo de duelo de bots.",
            Steps =
            {
                Step("Deathmatch", "deathmatch", RoutineFocus.Deathmatch, 8,
                    "First contact: play normally but keep the crosshair at head height and take only fights you can " +
                    "reset from. Note which deaths came from a bad angle, not from bad aim.",
                    "Primer contacto: juega normal pero mantén la mira a la altura de la cabeza y acepta solo peleas " +
                    "de las que puedas recuperarte. Apunta qué muertes vinieron de un mal ángulo, no de mala puntería.",
                    "Deathmatch is the bridge between the trainer and ranked: real movement, real sound and real " +
                    "weapon control.",
                    "El deathmatch es el puente entre el entrenador y la ranked: movimiento, sonido y control del arma " +
                    "reales.",
                    recoil: true),
                Step("Deathmatch", "deathmatch", RoutineFocus.Deathmatch, 5,
                    "Duels only: counter-strafe every peek and fire only when you are stopped. Vary the width: a short " +
                    "jiggle for information, a wide swing when you already know where the head is.",
                    "Solo duelos: haz counter-strafe en cada peek y dispara solo cuando estés parado. Varía la " +
                    "anchura: un jiggle corto para informarte y un swing amplio cuando ya sabes dónde está la cabeza.",
                    "Most deathmatch deaths come from peeking while moving or swinging without information.",
                    "Casi todas las muertes en deathmatch vienen de asomarse moviéndose o de hacer swing sin información.",
                    recoil: true),
                Step("Deathmatch", "deathmatch", RoutineFocus.Deathmatch, 9,
                    "Second block, with one rule: after every kill, reset the crosshair to the next angle before you " +
                    "move. No reload in the open; use cover for that.",
                    "Segundo bloque con una regla: tras cada muerte, recoloca la mira al siguiente ángulo antes de " +
                    "moverte. No recargues al descubierto; usa cobertura para eso.",
                    "Kill-to-kill reset is what turns a trade into a multikill; the deathmatch respawn gives you " +
                    "constant chances to practise it.",
                    "La recolocación entre muertes es lo que convierte un trade en un multikill; el respawn del " +
                    "deathmatch te da ocasiones constantes de practicarla.",
                    recoil: true),
                Step("Deathmatch", "deathmatch", RoutineFocus.Deathmatch, 5,
                    "Movement block: strafe, stop, one-tap, strafe again. Do not stand still after the shot and do not " +
                    "chase a target into the open.",
                    "Bloque de movimiento: strafea, para, haz one-tap y vuelve a strafear. No te quedes quieto después " +
                    "del disparo ni persigas a un objetivo al descubierto.",
                    "The duel is decided by the stop, not by the speed of the strafe: arrive stopped and accurate.",
                    "El duelo lo decide la parada, no la velocidad del strafe: llega parado y preciso.",
                    recoil: true),
                Step("Deathmatch", "deathmatch", RoutineFocus.Deathmatch, 9,
                    "Final block: play to win the map, not to chase every sound. Hold a site, clear it properly and " +
                    "only take a fight when your crosshair is already placed.",
                    "Bloque final: juega para ganar el mapa, no para perseguir cada sonido. Cubre un sitio, límpialo " +
                    "bien y acepta una pelea solo cuando la mira ya esté colocada.",
                    "The last block checks whether the habits survive when you are tired: that is the version of you " +
                    "that queues ranked.",
                    "El último bloque comprueba si los hábitos sobreviven al cansancio: esa es la versión de ti que " +
                    "entra a ranked.",
                    recoil: true),
            },
        },

        new()
        {
            Id = "map-lab",
            Name = "MAPAS · Limpieza y retake",
            Source = "CA-VAL-AIM · entrenamiento propio",
            LevelEn = "Any level · 35 minutes", LevelEs = "Cualquier nivel · 35 minutos",
            DescriptionEn =
                "A full map block: clear a site, hold the post-plant, retake it, anchor it, dodge a flash and lock a " +
                "sound. Every exercise runs on the VALORANT map blockout you have selected and always uses a real " +
                "magazine and camera climb; those settings are locked because the point is to simulate the match.",
            DescriptionEs =
                "Un bloque completo de mapa: limpia un sitio, defiende el post-plant, haz el retake, ánclalo, esquiva " +
                "un flash y fija un sonido. Cada ejercicio se juega en el bloqueo de mapa de VALORANT que tengas " +
                "elegido y usa siempre cargador real y retroceso activo; esos ajustes están bloqueados porque el " +
                "objetivo es simular la partida.",
            CreditEn = "Routine by CA-VAL-AIM. Map layouts are original blockouts; no Riot map data is redistributed.",
            CreditEs = "Rutina de CA-VAL-AIM. Los mapas son bloqueos originales; no se redistribuyen datos de mapas de Riot.",
            Steps =
            {
                Step("Site Clear", "siteclear", RoutineFocus.Maps, 8,
                    "Clear the site near-to-far with the crosshair already on each angle. Slice the pie, do not swing " +
                    "wide; pre-aim the next angle while you move.",
                    "Limpia el sitio de cerca a lejos con la mira ya colocada en cada ángulo. Haz slicing, no swings " +
                    "amplios; pre-apunta el siguiente ángulo mientras te mueves.",
                    "Site clearing is the part of aim that wins rounds without a flick: placement first, speed second.",
                    "La limpieza de sitio es la parte de la puntería que gana rondas sin flick: primero colocación, " +
                    "después velocidad.",
                    recoil: true),
                Step("Post-Plant", "postplant", RoutineFocus.Maps, 6,
                    "Hold the post-plant from cover and play the clock. Only take the fight when the defuser is " +
                    "committed; otherwise keep the angle and make them clear you.",
                    "Defiende el post-plant desde cobertura y juega el reloj. Acepta la pelea solo cuando el que " +
                    "desactiva esté comprometido; si no, mantén el ángulo y obliga a que te limpien.",
                    "Post-plant rounds are won by patience and crosshair placement, not by peeking first.",
                    "Las rondas de post-plant se ganan con paciencia y colocación de mira, no asomándose primero.",
                    recoil: true),
                Step("Retake", "retake", RoutineFocus.Maps, 8,
                    "Retake with a plan: utility first, then clear the site from the closest safe angle. Do not enter " +
                    "one by one; trade with the bot and reset after every kill.",
                    "Haz el retake con un plan: primero utilidad y luego limpia el sitio desde el ángulo seguro más " +
                    "cercano. No entres de uno en uno; haz trade con el bot y recolócate tras cada muerte.",
                    "Retakes combine utility, movement and aim under time pressure, the closest thing to a ranked round.",
                    "Los retakes combinan utilidad, movimiento y puntería contra el reloj, lo más parecido a una ronda " +
                    "de ranked.",
                    recoil: true),
                Step("Site Anchor", "anchor", RoutineFocus.Maps, 6,
                    "Anchor one site: hold a default angle, use off-angles only after a reset and rotate late enough " +
                    "to punish the plant. Never hold the same angle twice in a row.",
                    "Ancla un sitio: cubre un ángulo por defecto, usa off-angles solo tras recolocarte y rota lo " +
                    "suficientemente tarde para castigar la plantada. Nunca cubras dos veces seguidas el mismo ángulo.",
                    "Anchoring is angle discipline: the enemy should have to clear you, not the other way round.",
                    "Anclar es disciplina de ángulos: el enemigo debería tener que limpiarte a ti, no al contrario.",
                    recoil: true),
                Step("Flash Dodge", "flashmap", RoutineFocus.Maps, 4,
                    "Turn away from every flash and re-peek only when the white is gone. Keep your back to the flash " +
                    "while still holding the angle you expect them to push.",
                    "Gira la espalda en cada flash y vuelve a asomarte solo cuando el blanco desaparezca. Mantén la " +
                    "espalda al flash sin perder el ángulo por el que esperas que empujen.",
                    "A dodged flash is a free duel; a full flash forces you to spray blind.",
                    "Un flash esquivado es un duelo gratis; un flash de lleno te obliga a disparar a ciegas.",
                    recoil: true),
                Step("Sound Lock", "sound", RoutineFocus.Maps, 3,
                    "Identify the sound, call the direction and pre-aim the exact angle before the bot appears. Do not " +
                    "turn toward every noise: filter footsteps, reloads and utility.",
                    "Identifica el sonido, canta la dirección y pre-apunta el ángulo exacto antes de que aparezca el " +
                    "bot. No gires hacia cada ruido: filtra pasos, recargas y utilidad.",
                    "Sound is information; turning at the wrong noise gives away your angle and your timing.",
                    "El sonido es información; girar al ruido equivocado regala tu ángulo y tu timing.",
                    recoil: true),
            },
        },
    };

    static RoutineLibrary()
    {
        // Contact scenarios are held-fire drills. Older copy asked for isolated clicks
        // (sometimes even a second bullet), contradicting the actual completion rule.
        foreach (var step in All.SelectMany(r => r.Steps))
        {
            var key = step.Drill.Split(':', 2);
            if (key.Length != 2 || AimVariants.Find(key[0], key[1]) is not { ContactSeconds: > 0 } v) continue;
            int ms = (int)MathF.Round(v.ContactSeconds * 1000);
            step.HowEs = $"Mantén pulsado disparo. Centra la mira en cada blanco durante {ms} ms seguidos y cambia cuando se elimine. Si sales del blanco, vuelve a estabilizar la mira.";
            step.HowEn = $"Hold fire. Stay centered on each target for {ms} continuous ms and switch when it clears. If you leave the target, settle again.";
            if (v.RegenSeconds > 0)
            {
                step.HowEs = $"Mantén disparo y acompaña los pequeños cambios de dirección. Necesitas {ms} ms de contacto para eliminar. Tras {v.RegenDelay:0.0} s fuera del blanco comienza a recuperar vida; cambia solo cuando se elimine.";
                step.HowEn = $"Hold fire and follow the small direction changes. A kill needs {ms} ms of contact. Health regeneration starts after {v.RegenDelay:0.0} s off target; switch only after the kill.";
            }
            step.WhyEs = "Practica frenar el flick, confirmar el contacto y cambiar sin adelantarte a la eliminación.";
            step.WhyEn = "Practise braking the flick, confirming contact and switching after the elimination.";
        }
    }

    public static Routine? Find(string id) => All.FirstOrDefault(r => r.Id == id);
    public static IReadOnlyList<Routine> Routines => All;

    /// <summary>The guide text of cartoon's document, split into sections. Spanish is our translation/adaptation.</summary>
    public sealed record GuideSection(string TitleEn, string TitleEs, string[] En, string[] Es);

    public static readonly GuideSection[] Guide =
    {
        new("Before you start", "Antes de empezar",
            new[]
            {
                "If your goal is to be a better VALORANT player, most of your hours should go to the game itself: VOD " +
                "review, ranked, server time, deathmatch. Aim trainers accelerate mechanical progress; they do not " +
                "replace playing.",
                "cartoon recommends 30–60 minutes of aim training per day, including in-game practice. More is not " +
                "better: it produces diminishing returns and fatigue. Warm-up time is separate.",
                "Train focused. If you are only going through the motions to tick a box, skip the session. The goal is " +
                "that you learn to solve problems and find your own methods.",
            },
            new[]
            {
                "Si tu objetivo es ser mejor en VALORANT, la mayor parte de tus horas debe ir al juego: VOD review, " +
                "ranked, tiempo en servidor, deathmatch. Los aim trainers aceleran el progreso mecánico; no sustituyen " +
                "jugar.",
                "cartoon recomienda 30–60 minutos de aim training al día, incluyendo la práctica in-game. Más no es " +
                "mejor: produce rendimientos decrecientes y fatiga. El calentamiento va aparte.",
                "Entrena concentrado. Si solo vas a cumplir el expediente, mejor no juegues la sesión. La idea es que " +
                "aprendas a resolver problemas y a encontrar tus propios métodos.",
            }),
        new("Rules that matter more than the score", "Reglas que importan más que la puntuación",
            new[]
            {
                "Do not drag flicks: move, stop on the target, then click.",
                "One shot per target in clicking scenarios. Do not spam the same target.",
                "Do not track or follow the trail of moving clicking targets. Read the target and click where it will be.",
                "Watch for consistent overflicks. If you always pass the target, reduce the movement, not the speed.",
                "Automatic-weapon scenarios can be played holding fire for the full minute or tapping; both work, but " +
                "stay consistent within a run.",
                "Do not chase 100% accuracy. Fear of missing limits your speed. Aim for clean, controlled speed and let " +
                "accuracy follow.",
                "Do not tense your hand or arm hard. It causes injuries and fatigue. Keep the grip loose and take breaks.",
                "For now, use the same sensitivity in VALORANT and in the trainer, 103 FOV for VALORANT/Overwatch, and " +
                "do not overthink your settings.",
            },
            new[]
            {
                "No arrastres los flicks: muévete, frena sobre el objetivo y luego haz clic.",
                "Un disparo por objetivo en los escenarios de clicking. No hagas spam sobre el mismo objetivo.",
                "No sigas ni trackees la estela de objetivos móviles de clicking. Lee el objetivo y haz clic donde " +
                "estará.",
                "Cuidado con los overflicks consistentes. Si siempre te pasas, reduce el movimiento, no la velocidad.",
                "Los escenarios con arma automática se pueden jugar manteniendo el clic todo el minuto o dando toques; " +
                "ambas formas valen, pero sé consistente dentro de una pasada.",
                "No vayas por el 100% de precisión. El miedo a fallar te limita la velocidad. Busca velocidad limpia " +
                "y controlada; la precisión llega después.",
                "No tenses mucho la mano ni el brazo. Provoca lesiones y fatiga. Mantén el agarre suelto y descansa.",
                "Por ahora usa la misma sensibilidad en VALORANT y en el entrenador, 103 FOV para VALORANT/Overwatch, " +
                "y no le des demasiadas vueltas a los ajustes.",
            }),
        new("Schedule and rotation", "Horario y rotación",
            new[]
            {
                "Play the routine first thing in the day or last thing before you stop, whichever works better for " +
                "you. These routines are not designed as warm-ups; cartoon recommends the MiniGods RAMP warm-up for that.",
                "Train 3–5 days a week with two rest days. Rest days help progress; you do not need to play every day.",
                "Alternate routines weekly and give each skill enough time. Example: week 1 flicks, week 2 " +
                "micro-adjustments, week 3 flicks, week 4 micro-adjustments. Or two weeks of each.",
                "You can add other routines to the rotation. The point is deliberate practice, not collecting routines.",
            },
            new[]
            {
                "Juega la rutina a primera hora del día o al final antes de parar, lo que mejor te funcione. Estas " +
                "rutinas no están pensadas como calentamiento; para eso cartoon recomienda el RAMP warm-up de MiniGods.",
                "Entrena 3–5 días por semana con dos días de descanso. Los días de descanso ayudan al progreso; no " +
                "necesitas jugar todos los días.",
                "Alterna rutinas semanalmente y dale tiempo a cada habilidad. Ejemplo: semana 1 flicks, semana 2 " +
                "microajustes, semana 3 flicks, semana 4 microajustes. O dos semanas de cada una.",
                "Puedes añadir otras rutinas a la rotación. La clave es la práctica deliberada, no coleccionar rutinas.",
            }),
        new("How to use the VALORANT routines", "Cómo usar las rutinas de VALORANT",
            new[]
            {
                "These routines are supplementary. Use them together with VALORANT, and ideally with deathmatch, so the " +
                "movements transfer to the game. Ranked, scrims and matches should still be the bulk of your time.",
                "The Voltaic documents suggest the order Aim Lab/KovaaK's → deathmatch → ranked. The warm-up comes " +
                "before the session; the full routine can come before logging off.",
                "Keep each task under 40 minutes. If you only have 20, cut optional steps instead of rushing the core.",
                "Practise deliberately: understand what each task trains, keep clean lines, plan the next target and " +
                "avoid wasted mouse movement.",
            },
            new[]
            {
                "Estas rutinas son complementarias. Úsalas junto a VALORANT y, idealmente, con deathmatch, para que " +
                "los movimientos se trasladen al juego. Ranked, scrims y partidas deben seguir siendo la mayor parte " +
                "de tu tiempo.",
                "Los documentos de Voltaic sugieren el orden Aim Lab/KovaaK's → deathmatch → ranked. El calentamiento " +
                "va antes de la sesión; la rutina completa puede ir antes de desconectar.",
                "Mantén cada tarea por debajo de 40 minutos. Si solo tienes 20, recorta pasos opcionales en vez de " +
                "correr los del núcleo.",
                "Practica deliberadamente: entiende qué entrena cada tarea, mantén líneas limpias, planea el siguiente " +
                "objetivo y evita movimientos de ratón desperdiciados.",
            }),
    };

    /// <summary>
    /// Writes every routine, step, glossary term and the player's progress as Excel-friendly CSV (UTF-8 with BOM,
    /// semicolon separator). Four normalised tables instead of one wide sheet so each row means one thing and the files
    /// can be joined by <c>routine_id</c> in Excel or any database.
    /// </summary>
    public static string[] ExportAll()
    {
        string dir = Paths.DataDir;
        var inv = CultureInfo.InvariantCulture;
        var files = new List<string>();

        var routines = new StringBuilder();
        routines.AppendLine("routine_id;routine;source;level;playable;external;share_code;source_url;steps;minutes;description_es;credit_es");
        foreach (var r in All)
            routines.Append(Csv(r.Id)).Append(';').Append(Csv(r.Name)).Append(';').Append(Csv(r.Source)).Append(';')
                .Append(Csv(Loc.Spanish ? r.LevelEs : r.LevelEn)).Append(';').Append(r.Playable ? "yes" : "no").Append(';')
                .Append(r.External ? "yes" : "no").Append(';').Append(Csv(r.ShareCode ?? "")).Append(';').Append(Csv(r.SourceDoc))
                .Append(';').Append(r.Steps.Count).Append(';').Append(r.Minutes.ToString("0.#", inv)).Append(';')
                .Append(Csv(r.DescriptionEs)).Append(';').Append(Csv(r.CreditEs)).AppendLine();
        files.Add(Write("voltaic-routines.csv", routines));

        var steps = new StringBuilder();
        steps.AppendLine("routine_id;routine;step;scenario;adapted_drill;focus;minutes;runs;sens;recoil;optional;how_es;why_es;how_en;why_en");
        foreach (var r in All)
        {
            int n = 0;
            foreach (var s in r.Steps)
            {
                n++;
                steps.Append(Csv(r.Id)).Append(';').Append(Csv(r.Name)).Append(';').Append(n).Append(';')
                     .Append(Csv(s.Scenario)).Append(';').Append(Csv(s.Drill)).Append(';')
                     .Append(Csv(FocusName(s.Focus, true))).Append(';').Append(s.Minutes.ToString("0.#", inv)).Append(';')
                     .Append(s.Runs.Count).Append(';').Append(Csv(s.SensLabel)).Append(';')
                     .Append(s.RecoilOn ? "on" : "off").Append(';').Append(s.Optional ? "yes" : "no").Append(';')
                     .Append(Csv(s.HowEs)).Append(';').Append(Csv(s.WhyEs)).Append(';')
                     .Append(Csv(s.HowEn)).Append(';').Append(Csv(s.WhyEn)).AppendLine();
            }
        }
        files.Add(Write("voltaic-routine-steps.csv", steps));

        var glossary = new StringBuilder();
        glossary.AppendLine("term;letter;definition_es;definition_en;source");
        foreach (var t in Glossary.All.OrderBy(t => t.Term, StringComparer.OrdinalIgnoreCase))
            glossary.Append(Csv(t.Term)).Append(';').Append(t.Letter).Append(';').Append(Csv(t.Es)).Append(';')
                    .Append(Csv(t.En)).Append(';').Append(Csv("Voltaic Aim Glossary")).AppendLine();
        files.Add(Write("voltaic-glossary.csv", glossary));

        var progress = new StringBuilder();
        progress.AppendLine("routine_id;routine;times_played;furthest_step;last_step;completed;last_played_utc");
        foreach (var p in RoutineStore.All.OrderBy(p => p.Id, StringComparer.Ordinal))
            progress.Append(Csv(p.Id)).Append(';').Append(Csv(Find(p.Id)?.Name ?? p.Id)).Append(';')
                    .Append(p.TimesPlayed).Append(';').Append(p.FurthestStep).Append(';').Append(p.LastStep).Append(';')
                    .Append(p.Completed ? "yes" : "no").Append(';')
                    .Append(p.LastPlayed == default ? "" : p.LastPlayed.ToString("yyyy-MM-dd HH:mm:ss", inv)).AppendLine();
        files.Add(Write("voltaic-progress.csv", progress));
        return files.ToArray();

        static string Write(string name, StringBuilder csv)
        {
            string path = Path.Combine(Paths.DataDir, name);
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
            return path;
        }
    }

    static string Csv(string s)
    {
        s ??= "";
        bool quote = s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        s = s.Replace("\"", "\"\"");
        return quote ? $"\"{s}\"" : s;
    }
}

/// <summary>
/// Saves how far the player has reached in each built-in routine. Only built-in progress lives here; the actual run
/// scores stay in stats.json, so the routine file is small and can be rewritten safely.
/// </summary>
public sealed class RoutineProgress
{
    public string Id = "";
    public int TimesPlayed;
    public int FurthestStep;
    public int LastStep;
    public bool Completed;
    public DateTime LastPlayed;
}

public static class RoutineStore
{
    static readonly System.Text.Json.JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };
    static List<RoutineProgress>? all;
    static string FilePath => Path.Combine(Paths.DataDir, "routines.json");

    public static IReadOnlyList<RoutineProgress> All
    {
        get { Load(); return all!; }
    }

    static void Load()
    {
        if (all != null) return;
        all = new List<RoutineProgress>();
        try
        {
            if (File.Exists(FilePath))
                all = System.Text.Json.JsonSerializer.Deserialize<List<RoutineProgress>>(File.ReadAllText(FilePath), Json)
                      ?? new List<RoutineProgress>();
        }
        catch (Exception e) when (Paths.IsCorruptData(e)) { Paths.KeepCorrupt(FilePath, e); }
        catch (Exception e) { Log.Error($"routines.json couldn't be opened ({e.Message}); starting fresh"); all.Clear(); }
    }

    public static RoutineProgress Get(string id)
    {
        Load();
        var p = all!.FirstOrDefault(x => x.Id == id);
        if (p == null)
        {
            p = new RoutineProgress { Id = id };
            all!.Add(p);
        }
        return p;
    }

    /// <summary>The saved progress of a routine, or null when it was never started (viewing a routine never creates one).</summary>
    public static RoutineProgress? Find(string id)
    {
        Load();
        return all!.FirstOrDefault(x => x.Id == id);
    }

    public static void MarkStarted(string id)
    {
        var p = Get(id);
        p.TimesPlayed++;
        p.LastPlayed = DateTime.UtcNow;
        Save();
    }

    public static void MarkStep(string id, int step, int total)
    {
        var p = Get(id);
        p.LastStep = step;
        p.FurthestStep = Math.Max(p.FurthestStep, Math.Min(total, step + 1));
        p.LastPlayed = DateTime.UtcNow;
        Save();
    }

    /// <summary>Records progress and marks completion when the current attempt finished every required run.</summary>
    public static void MarkRunFinished(string id, int step, int total, bool complete)
    {
        var p = Get(id);
        p.LastStep = step;
        p.FurthestStep = Math.Max(p.FurthestStep, Math.Min(total, step + 1));
        p.Completed |= complete;
        p.LastPlayed = DateTime.UtcNow;
        Save();
    }

    static void Save()
    {
        if (AppSettings.ReadOnly) return;
        try { Paths.TryWriteAllText(FilePath, System.Text.Json.JsonSerializer.Serialize(All, Json)); }
        catch (Exception e) { Log.Error($"Saving routine progress failed: {e.Message}"); }
    }
}
