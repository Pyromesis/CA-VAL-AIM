using CaValAim.Core;
using CaValAim.Game;

namespace CaValAim.Analysis;

public sealed record SensitivityPoints(float Sensitivity, int Hits, int Misses)
{
    public int Total => 10 * Hits - 5 * Misses;
}

public sealed record TrainingFeedback(int Earned, int Lost, int Successes, int Errors, string Issue, SensitivityPoints[] Sensitivities)
{
    public int Score => Earned - Lost;

    public int PracticeRank()
    {
        int samples = Successes + Errors;
        if (samples < 12) return 0;
        float rate = (float)Successes / samples;
        return rate >= .94f && samples >= 24 ? 4 : rate >= .85f && samples >= 18 ? 3
            : rate >= .74f ? 2 : rate >= .58f ? 1 : 0;
    }

    public string Tip(bool es) => Issue switch
    {
        "tracking" => es ? "Sigue el centro del blanco y recupera el contacto con un solo movimiento tras cada giro."
            : "Follow the center and regain contact in one movement after each turn.",
        "early" => es ? "Espera a ver u oír la señal completa antes de pulsar." : "Wait until you see or hear the full cue before pressing.",
        "flash" => es ? "Gira al detectar el destello y vuelve al ángulo cuando pase." : "Turn away on the flash cue, then return to the angle.",
        "movement" => es ? "Suelta la tecla de movimiento antes del primer disparo." : "Release movement before taking the first shot.",
        "timeout" => es ? "Prepara la mira cerca de donde aparece el blanco para reducir el recorrido."
            : "Place your crosshair near the spawn to shorten the movement.",
        "duel" => es ? "Aísla un ángulo y deja la mira a la altura del primer blanco antes de asomarte."
            : "Isolate one angle and set your crosshair at target height before peeking.",
        "miss" => es ? "Llega al centro del blanco, confirma la posición y luego pulsa."
            : "Reach the center, confirm your aim, then click.",
        _ => Successes > 0
            ? (es ? "Repite el mismo ejercicio y comprueba que puedes mantener la tasa de acierto con un ritmo ligeramente mayor."
                : "Repeat the drill and see whether you can keep the same success rate at a slightly faster pace.")
            : (es ? "Completa varios intentos siguiendo la consigna del ejercicio para obtener una muestra."
                : "Follow the drill cue and complete several attempts to get a sample."),
    };
}

/// <summary>Same scoring for live play and archived telemetry. Each bullet is attributed to its setting at fire time.</summary>
public sealed class TrainingFeedbackCounter(bool tracking)
{
    readonly Dictionary<float, (int Hits, int Misses)> sensitivity = new();
    readonly Dictionary<string, int> errors = new();
    int shots, events, frames;
    int successes, failures, earned, lost;
    float onTime, offTime;
    public int Score => earned - lost;
    public int PointsAt(float setting)
    {
        var counts = sensitivity.GetValueOrDefault(setting);
        return 10 * counts.Hits - 5 * counts.Misses;
    }

    void Credit(bool correct, string issue, int points = 10)
    {
        if (correct) { successes++; earned += points; return; }
        failures++;
        lost += points / 2;
        errors[issue] = errors.GetValueOrDefault(issue) + 1;
    }

    public void Update(RunTelemetry run)
    {
        float? legacySensitivity = null;
        for (; shots < run.Shots.Count; shots++)
        {
            var shot = run.Shots[shots];
            Credit(shot.Hit, shot.Accurate ? "miss" : "movement");
            float sens = SensitivityProfiles.Valid(shot.Sensitivity) ? shot.Sensitivity
                : legacySensitivity ??= run.Events.Any(e => e.Kind is "sens_changed" or "sens_set") ? 0 : run.Sens;
            if (SensitivityProfiles.Valid(sens))
            {
                var counts = sensitivity.GetValueOrDefault(sens);
                sensitivity[sens] = shot.Hit ? (counts.Hits + 1, counts.Misses) : (counts.Hits, counts.Misses + 1);
            }
        }
        bool game = run.Mode.StartsWith("reactgame:", StringComparison.Ordinal);
        bool lab = run.Mode.StartsWith("reactlab:", StringComparison.Ordinal);
        for (; events < run.Events.Count; events++)
        {
            var evt = run.Events[events];
            switch (evt.Kind)
            {
                case "reaction_game_aim_miss": Credit(false, "miss"); break;
                case "reaction_game_trial": Credit(evt.A == 1, "early"); break;
                case "early_click" when !game && !lab: Credit(false, "early"); break;
                case "trial_fail" when lab: Credit(false, "early"); break;
                case "round_won": Credit(true, "duel", 20); break;
                case "round_lost": Credit(false, "duel", 20); break;
                case "died": Credit(false, "duel"); break;
                case "target_expired":
                case "sf_flick" when evt.B < 0: Credit(false, "timeout"); break;
                case "flash" when evt.A >= 0: Credit(evt.A == 0, "flash"); break;
                case "xhair_t":
                case "xhair_vis": Credit(evt.B > 0, "miss"); break;
                default:
                    if (evt.Kind == "reaction" || (lab && evt.Kind.StartsWith("reaction_", StringComparison.Ordinal)))
                        Credit(evt.A > 0, "early");
                    break;
            }
        }
        if (!tracking) return;
        for (; frames < run.Frames.Count; frames++)
        {
            if (frames == 0) continue;
            var f = run.Frames[frames - 1];
            if (!f.Has(FrameFlags.Firing) || f.FocusId == 0 || f.FocusRadius <= 0) continue;
            float dt = Math.Clamp(run.Frames[frames].T - f.T, 0, .05f);
            if (!float.IsFinite(dt)) continue;
            bool on = f.FocusErr <= f.FocusRadius;
            if (on) onTime += dt; else offTime += dt;
            if (onTime >= .25f) { onTime -= .25f; Credit(true, "tracking", 4); }
            if (offTime >= .25f) { offTime -= .25f; Credit(false, "tracking", 4); }
        }
    }

    public TrainingFeedback Snapshot() => new(earned, lost, successes, failures,
        errors.OrderByDescending(e => e.Value).Select(e => e.Key).FirstOrDefault() ?? (tracking ? "tracking" : ""),
        sensitivity.OrderBy(p => p.Key).Select(p => new SensitivityPoints(p.Key, p.Value.Hits, p.Value.Misses)).ToArray());

    public static TrainingFeedback Read(RunTelemetry run)
    {
        var counter = new TrainingFeedbackCounter(Tracks(run));
        counter.Update(run);
        return counter.Snapshot();
    }

    public static bool Tracks(RunTelemetry run) => run.TrackFeedback ||
        RunAnalysis.Base(run.Mode) is "tracking" or "trackv" or "airtrack" or "reacttrack" or "anti-mirror";
}
