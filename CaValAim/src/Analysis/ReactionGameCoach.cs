using CaValAim.Core;
using CaValAim.Game;
using CaValAim.Modes;

namespace CaValAim.Analysis;

/// <summary>Game-specific feedback, deliberately separate from simple visual reaction benchmarks.</summary>
public static class ReactionGameCoach
{
    public static Dictionary<string, float> Measure(RunTelemetry t)
    {
        var m = new Dictionary<string, float>();
        var trials = t.Events.Where(e => e.Kind == "reaction_game_trial").ToArray();
        var rt = t.Events.Where(e => e.Kind == "reaction_game" && float.IsFinite(e.A) && e.A >= 90).Select(e => e.A).Order().ToArray();
        m["game.trials"] = trials.Length;
        m["game.success"] = trials.Count(e => e.A == 1);
        m["game.early"] = t.Events.Count(e => e.Kind == "early_click");
        m["game.false"] = t.Events.Count(e => e.Kind == "reaction_game_false_alarm");
        m["game.aim_miss"] = t.Events.Count(e => e.Kind == "reaction_game_aim_miss");
        m["game.missed"] = t.Events.Count(e => e.Kind == "reaction_game_timeout");
        if (rt.Length > 0) { m["game.response_ms"] = rt.Length % 2 == 0 ? (rt[rt.Length / 2 - 1] + rt[rt.Length / 2]) / 2 : rt[rt.Length / 2]; m["game.responses"] = rt.Length; }
        return m;
    }
    public static void Gather(RunTelemetry t, PlayAnalysis analysis)
    {
        analysis.Kind = "reactions";
        float from = -1;
        foreach (var trial in t.Events.Where(e => e.Kind == "reaction_game_trial"))
        {
            var events = t.Events.Where(e => e.T > from && e.T <= trial.T).ToArray();
            bool good = trial.A == 1, es = Loc.Spanish;
            var response = events.LastOrDefault(e => e.Kind == "reaction_game");
            string reason = events.Any(e => e.Kind == "early_click") ? "early"
                : events.Any(e => e.Kind == "reaction_game_false_alarm") ? "false_alarm"
                : events.Any(e => e.Kind == "reaction_game_timeout") ? "slow"
                : events.Any(e => e.Kind == "reaction_game_aim_miss") ? "miss" : "clean";
            analysis.Plays.Add(new Play
            {
                T0 = Math.Max(0, events.LastOrDefault(e => e.Kind == "reaction_game_signal")?.T ?? trial.T - 0.5f),
                T1 = trial.T, Kind = "reaction", Skill = Skill.Reaction, Score = good ? 85 : 30, Reason = reason,
                Title = es ? (good ? "Intento correcto" : "Intento a corregir") : (good ? "Correct trial" : "Trial to improve"),
                Detail = response != null ? (es ? $"Respuesta: {response.A:0} ms." : $"Response: {response.A:0} ms.")
                    : es ? (good ? "Esperaste como pedía la regla." : "Revisa la señal y la regla antes de responder.")
                    : good ? "You withheld your response as instructed." : "Check the signal and rule before responding.",
                Advice = es ? "Compara el tiempo y los errores con tus repeticiones de este mismo nivel." : "Compare time and errors with your repetitions of this same level.",
                Drills = new[] { t.Mode },
            });
            from = trial.T;
        }
    }
    public static void Review(RunTelemetry t, RunReview review)
    {
        var m = review.Metrics; bool es = Loc.Spanish;
        float n = m.GetValueOrDefault("game.trials"), success = m.GetValueOrDefault("game.success");
        review.Summary = es ? $"{success:0}/{n:0} intentos correctos. Compara tiempos solo dentro del mismo minijuego y nivel." : $"{success:0}/{n:0} correct trials. Compare times only within the same game and level.";
        if (m.TryGetValue("game.response_ms", out var ms)) review.Summary += es ? $" Mediana de respuesta: {ms:0} ms." : $" Median response: {ms:0} ms.";
        string arg = t.Mode.Split(':', 2).Last();
        var mode = ReactionGameMode.Parse(arg);
        string drill = mode == null ? t.Mode : new ReactionGameMode(mode.Kind, Math.Max(0, mode.Level - 1)).Key;
        Add("EARLY", "game.early", "Estás anticipando la señal", "You are anticipating the signal", "Espera la señal completa. Mantén el dedo relajado durante la preparación.", "Wait for the full signal. Relax your finger during preparation.");
        Add("RULE", "game.false", "Respondes cuando debes esperar", "You respond when you should wait", "Lee la regla antes de cada intento. Prioriza acertar antes de acelerar.", "Read the rule before each trial. Prioritize correct decisions before speeding up.");
        Add("AIM", "game.aim_miss", "Pulsas fuera del objetivo", "You click outside the target", "Frena sobre el centro y confirma antes de pulsar. Practica el mismo juego un nivel más fácil.", "Brake at the center and confirm before pressing. Practise the same game one level easier.");
        Add("TIME", "game.missed", "Se agota la ventana de respuesta", "The response window expires", "Baja un nivel, prepara la respuesta y vuelve a subir cuando completes la ronda con control.", "Drop one level, prepare your response and move back up after a controlled round.");
        if (n >= 8 && success == n) review.Strengths.Add(es ? "Ronda sin errores de decisión ni anticipaciones." : "No decision errors or anticipations this round.");
        review.PrescribedDrills = new[] { review.Issues.Count > 0 ? drill : t.Mode };
        void Add(string id, string metric, string titleEs, string titleEn, string cueEs, string cueEn)
        {
            int count = (int)m.GetValueOrDefault(metric);
            if (count == 0) return;
            review.Issues.Add(new Diagnosis("REACTION_GAME_" + id, Skill.Reaction, count >= 4 ? Severity.Major : Severity.Minor,
                es ? titleEs : titleEn, es ? $"{count} casos registrados." : $"{count} recorded cases.",
                es ? "Este resultado corresponde a la mecánica de este juego; no es una medida de rango competitivo." : "This result belongs to this game's mechanics; it is not a competitive rank measurement.",
                new[] { es ? cueEs : cueEn }, new[] { drill }, count));
        }
    }
}
