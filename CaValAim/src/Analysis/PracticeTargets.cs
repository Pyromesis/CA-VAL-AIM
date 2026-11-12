using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>Operational practice targets, not population norms or a diagnosis of the player's body.</summary>
public static class PracticeTargets
{
    public static string For(PlanStep step)
    {
        bool es = Loc.Spanish;
        string id = step.Source?.Id ?? "";
        string target = id switch {
            "OVERFLICK" or "RECOVERED_OVER" or "OVER_NEAR" or "OVER_WIDE" => es ? "Reduce la proporción de sobrepasos; comprueba que no aumenten los movimientos cortos."
                : "Reduce overshoots while checking that short movements do not increase.",
            "UNDERFLICK" => es ? "Reduce los movimientos cortos sin convertirlos en sobrepasos."
                : "Reduce short movements without replacing them with overshoots.",
            "MIXED_FLICK" => es ? "Reduce los errores largos y cortos juntos; una media cercana a cero no basta."
                : "Reduce long and short errors together; a near-zero mean alone is not enough.",
            "MICRO_CONFIRM" or "NO_MICRO" => es ? "Aumenta los primeros disparos centrados y reduce correcciones repetidas."
                : "Increase centered first shots and reduce repeated corrections.",
            _ => step.Skill switch {
                Skill.Tracking => es ? "Aumenta el tiempo sobre el blanco y reduce el retraso al cambiar de dirección."
                    : "Increase time on target and reduce reversal reacquisition delay.",
                Skill.Reaction => es ? "Reduce el tiempo de respuesta sin aumentar anticipaciones ni decisiones incorrectas."
                    : "Reduce response time without increasing anticipations or incorrect decisions.",
                Skill.Movement => es ? "Reduce los disparos antes de detenerte sin perder velocidad al completar el duelo."
                    : "Reduce shots before stopping without slowing the completed duel.",
                _ => es ? "Repite el ejercicio y compara su métrica principal junto con el tiempo necesario."
                    : "Repeat this drill and compare its main metric together with completion time.",
            },
        };
        return target + (es
            ? " Compara al menos 20 intentos válidos por bloque (30 s en seguimiento), con igual tarea, dificultad, arma, DPI y sensibilidad. Primero controla el gesto; después repite a velocidad de juego. Confirma la mejora en otra sesión y en un ejercicio distinto antes de darla por aprendida."
            : " Compare at least 20 valid attempts per block (30 s for tracking), keeping task, difficulty, weapon, DPI and sensitivity fixed. Establish control first, then repeat at game speed. Confirm improvement in another session and a different drill before treating it as learned.");
    }
}
