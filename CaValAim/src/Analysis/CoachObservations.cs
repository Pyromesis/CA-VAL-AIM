using CaValAim.Game;

namespace CaValAim.Analysis;

/// <summary>Independent findings, so overshoot and a delayed reaction on the same play are both retained.</summary>
public sealed record CoachObservation(string Code, float At, float Until, int Target, float Value, string Unit,
    bool Controlled);

public static class CoachObservations
{
    public static List<CoachObservation> Build(RunTelemetry telemetry)
    {
        var result = new List<CoachObservation>();
        if (RunAnalysis.Prepare(telemetry) is { } prep)
        {
            foreach (var (trial, motion) in RunAnalysis.Trials(prep))
            {
                if (motion == null) continue;
                float at = float.IsFinite((float)motion.TeP) ? (float)motion.TeP : trial.T0;
                bool controlled = RunAnalysis.CleanAimSample(prep, motion);
                if (motion.HasPrimary)
                {
                    if (motion.Over && (motion.Bot || RunAnalysis.StaticTargetMode(telemetry.Mode))) Add("over", at, motion.S, "deg");
                    if (motion.Under && (motion.Bot || RunAnalysis.StaticTargetMode(telemetry.Mode))) Add("under", at, motion.EndErr, "deg");
                    if (float.IsFinite(motion.Onset)) Add("reaction_ms", trial.T0, motion.Onset, "ms");
                    if (motion.Nc > 0) Add("corrections", at, motion.Nc, "count");
                    if (motion.Premature > 0) Add("premature", at, motion.Premature, "count");
                }
                void Add(string code, float time, float value, string unit)
                {
                    if (!float.IsFinite(value)) return;
                    result.Add(new CoachObservation(code, Math.Max(0, time), Math.Max(time, trial.TEnd),
                        motion.TrialId, value, unit, controlled));
                }
            }
            if (RunAnalysis.Base(telemetry.Mode) is "tracking" or "trackv" or "airtrack" or "reacttrack")
            {
                var lag = new List<RunAnalysis.LagStretch>();
                foreach (var reversal in RunAnalysis.Reversals(prep, out _, lag))
                {
                    Keep("tracking_reacquisition_ms", reversal.T, reversal.T, reversal.ReacqMs, "ms");
                    Keep("tracking_overrun", reversal.T, reversal.T, reversal.OverrunExcessDeg, "deg");
                }
                foreach (var stretch in lag) Keep("tracking_lag", stretch.T0, stretch.T1, stretch.LagDeg, "deg");
            }
            if (telemetry.Mode is "spray_vandal" or "spray_phantom" or "spray_transfer")
                foreach (var spray in RunAnalysis.Sprays(prep))
                {
                    Keep("spray_vertical", spray.T0, spray.T1, spray.V, "deg");
                    Keep("spray_horizontal", spray.T0, spray.T1, spray.H, "deg");
                }
            void Keep(string code, float at, float until, float value, string unit)
            {
                if (float.IsFinite(value)) result.Add(new CoachObservation(code, at, until, 0, value, unit, false));
            }
        }
        foreach (var shot in telemetry.Shots)
            if (!shot.Accurate && float.IsFinite(shot.T) && float.IsFinite(shot.Speed))
                result.Add(new CoachObservation("inaccurate_shot", shot.T, shot.T, shot.FocusId,
                    shot.Speed, "m/s", false));
        foreach (var evt in telemetry.Events)
            if (evt.Kind is "reaction" or "reaction_audio" or "reaction_peripheral" or "reaction_choice" or "reaction_flick" or "reaction_game" or "reaction_game_onset")
                if (float.IsFinite(evt.T) && float.IsFinite(evt.A) && evt.A > 0)
                    result.Add(new CoachObservation(evt.Kind + "_ms", evt.T, evt.T, 0, evt.A, "ms", true));
        foreach (var evt in telemetry.Events)
            if (float.IsFinite(evt.T) && (evt.Kind is "early_click" or "reaction_game_false_alarm" or "reaction_game_aim_miss" or "reaction_game_timeout"))
                result.Add(new CoachObservation(evt.Kind, evt.T, evt.T, 0, 1, "count", false));
        return result.OrderBy(o => o.At).ThenBy(o => o.Code, StringComparer.Ordinal).ToList();
    }
}
