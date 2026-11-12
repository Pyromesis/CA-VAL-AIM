namespace CaValAim.Modes;

/// <summary>
/// Technique drills (2.10): one drill per habit taught in the Academy (Core/Academy.cs). Each isolates a single
/// technique with generous targets, so the player can attend to how the movement is made instead of the score.
/// They are ordinary <see cref="AimVariant"/>s: playable from the library ("Technique" section), from a lesson
/// and from the coach's plan.
/// </summary>
public static class TechniqueDrills
{
    public const string Section = "Technique";

    static AimVariant Make(AimFamily family, string id, string en, string es, string cueEn, string cueEs) =>
        new AimVariant(id, family, "Technique: " + en, cueEn) { NameEs = "Técnica: " + es, DescriptionEs = cueEs, CategoryOverride = Section };

    public static readonly AimVariant[] All =
    {
        Make(AimFamily.Flick, "tech-clean-pace", "clean pace", "ritmo limpio",
            "One target at a time, no timer. Play at the speed where almost every first click hits; only then go faster.",
            "Un blanco cada vez, sin límite de tiempo. Juega a la velocidad en la que casi todos los primeros clics aciertan; solo entonces acelera.")
            with { TapOnly = true, Radius = 0.16f, Yaw = 22, MinAngle = 6 },
        Make(AimFamily.Flick, "tech-brake-ladder", "braking at every distance", "frenada a cualquier distancia",
            "Short and long flicks along the head line, mixed. Accelerate, brake over the last third and click once the crosshair has stopped.",
            "Flicks cortos y largos mezclados sobre la línea de cabezas. Acelera, frena en el último tercio y pulsa cuando la mira se haya detenido.")
            with { TapOnly = true, Pattern = AimPattern.Horizontal, Radius = 0.13f, Yaw = 46, MinAngle = 4 },
        Make(AimFamily.Flick, "tech-head-line", "head-height line", "línea a la altura de la cabeza",
            "Every head appears at the same height for a moment. Keep the crosshair on that line between targets: move sideways only.",
            "Todas las cabezas aparecen a la misma altura durante un instante. Mantén la mira en esa línea entre blancos: muévete solo de lado.")
            with { Pattern = AimPattern.Horizontal, Radius = 0.14f, Yaw = 32, MinAngle = 8, Life = 1.3f },
        Make(AimFamily.Flick, "tech-sweep-order", "sweep in order", "barrido en orden",
            "Four targets at once. Take the nearest to your crosshair first and sweep in one direction; confirm each hit before you leave.",
            "Cuatro blancos a la vez. Empieza por el más cercano a tu mira y barre en una sola dirección; confirma cada impacto antes de salir.")
            with { Count = 4, Radius = 0.17f, Yaw = 38, MinAngle = 6 },
        Make(AimFamily.Flick, "tech-still-hold", "steady hand", "mano firme",
            "Hold fire and keep the crosshair inside a tiny target for a third of a second. Loose grip: a hand that squeezes shakes.",
            "Mantén disparo y deja la mira dentro de un blanco diminuto durante un tercio de segundo. Agarre suelto: una mano que aprieta tiembla.")
            with { Count = 1, ContactSeconds = 0.34f, Radius = 0.085f, Yaw = 12, MinAngle = 2 },
        Make(AimFamily.Flick, "tech-far-first-bullet", "first bullet at range", "primera bala a distancia",
            "Heads at 28–40 m, one tap each. Settle the crosshair completely and fire one accurate bullet.",
            "Cabezas a 28–40 m, un tiro por blanco. Asienta la mira del todo y dispara una sola bala precisa.")
            with { TapOnly = true, Radius = 0.14f, DistMin = 28, DistMax = 40, Yaw = 14, MinAngle = 3, PitchMin = -1, PitchMax = 4 },
        Make(AimFamily.Track, "tech-smooth-slow", "slow and smooth", "lento y suave",
            "A slow target on long strafes. Hold fire and glide with it at its speed, with the loosest grip that stays on target.",
            "Un blanco lento con desplazamientos largos. Mantén disparo y deslízate con él a su velocidad, con el agarre más suelto que consiga seguirlo.")
            with { Radius = 0.2f, Speed = 1.6f, TurnMin = 1.2f, TurnMax = 2.6f, Width = 7f },
        Make(AimFamily.Track, "tech-smooth-small", "small and smooth", "pequeño y suave",
            "A small target that drifts in any direction. No jumps: follow it in one continuous movement.",
            "Un blanco pequeño que deriva en cualquier dirección. Sin saltos: síguelo en un movimiento continuo.")
            with { Radius = 0.12f, Speed = 2.2f, Motion = TrackMotion.Wander },
        Make(AimFamily.Track, "tech-reversal-read", "reading reversals", "leer los cambios de dirección",
            "The target reverses and stops at random. Slow down, turn and speed up with it; never snap back with a flick.",
            "El blanco cambia de dirección y se para al azar. Frena, gira y acelera con él; no recuperes con un flick.")
            with { Radius = 0.24f, Speed = 4.2f, TurnMin = 0.3f, TurnMax = 1.6f, StopChance = 0.2f },
        Make(AimFamily.Micro, "tech-one-correction", "one correction", "una sola corrección",
            "Tiny heads a little away from the last one. One small finger movement, then the click. Count how many first shots hit.",
            "Cabezas diminutas a poca distancia de la anterior. Un único movimiento pequeño de dedos y el clic. Cuenta cuántos primeros disparos aciertan.")
            with { TapOnly = true, Radius = 0.09f, OffMin = 0.6f, OffMax = 2.4f },
        Make(AimFamily.Reflex, "tech-wait-commit", "wait, then commit", "esperar y decidirse",
            "Long, unpredictable pauses. Keep the hand relaxed, do not guess, and click once as soon as the head appears.",
            "Pausas largas e imprevisibles. Mantén la mano relajada, no adivines y pulsa una vez en cuanto aparezca la cabeza.")
            with { TapOnly = true, Yaw = 8, MinAngle = 0, DelayMin = 1.2f, DelayMax = 4.5f, Window = 0.7f },

        // ---- one habit each: the rest of the Academy's lessons ----
        Make(AimFamily.Flick, "tech-vertical-brake", "braking up and down", "frenada vertical",
            "Targets above and below. The vertical flick is shorter and stiffer than the horizontal one: brake early and click once stopped.",
            "Blancos arriba y abajo. El flick vertical es más corto y rígido que el horizontal: frena pronto y pulsa cuando la mira esté quieta.")
            with { TapOnly = true, Pattern = AimPattern.Vertical, Radius = 0.15f, PitchMin = -6, PitchMax = 20, MinAngle = 6 },
        Make(AimFamily.Flick, "tech-wide-arm", "wide flicks from the arm", "flicks amplios con el brazo",
            "Targets far to each side. Start the move from the elbow, finish it with the wrist and stop dead on the target.",
            "Blancos muy a los lados. Inicia el movimiento desde el codo, termínalo con la muñeca y frena en seco sobre el blanco.")
            with { TapOnly = true, Radius = 0.2f, Yaw = 75, MinAngle = 40 },
        Make(AimFamily.Flick, "tech-reset-centre", "out and back to centre", "ida y vuelta al centro",
            "A centre target, then one further out, then the centre again. Return to exactly the same point every time.",
            "Un blanco central, luego uno alejado y otra vez el centro. Vuelve siempre exactamente al mismo punto.")
            with { TapOnly = true, Pattern = AimPattern.CenterOut, Radius = 0.14f, Yaw = 30, MinAngle = 8 },
        Make(AimFamily.Flick, "tech-moving-click", "clicking a walking target", "clic sobre un blanco que camina",
            "Each head walks sideways. Catch up, match its speed for an instant and click on its centre.",
            "Cada cabeza camina de lado. Alcánzala, iguala su velocidad un instante y pulsa en su centro.")
            with { TapOnly = true, Radius = 0.16f, Drift = 1.6f, Yaw = 24, MinAngle = 6 },
        Make(AimFamily.Flick, "tech-confirm-switch", "confirm, then switch", "confirmar y cambiar",
            "Three targets. Hold fire, stay on one until it breaks, and only then move to the next.",
            "Tres blancos. Mantén disparo, quédate en uno hasta que se rompa y solo entonces pasa al siguiente.")
            with { Count = 3, ContactSeconds = 0.14f, Radius = 0.16f, Yaw = 30, MinAngle = 6 },
        Make(AimFamily.Flick, "tech-stop-shoot", "move, stop, shoot", "moverse, parar, disparar",
            "Strafe with the Classic, stop (release or tap the opposite key) and fire one bullet once you are still.",
            "Muévete de lado con la Classic, para (suelta o toca la tecla contraria) y dispara una bala cuando estés quieto.")
            with { SideStep = true, TapOnly = true, Walls = 1, Count = 1, Radius = 0.16f, DistMin = 10, DistMax = 16 },
        Make(AimFamily.Track, "tech-vertical-track", "smooth on the vertical axis", "suavidad en el eje vertical",
            "A slow target that rises and falls while it strafes. Follow the height with the wrist without stepping.",
            "Un blanco lento que sube y baja mientras se desplaza. Sigue la altura con la muñeca, sin escalones.")
            with { Radius = 0.22f, Motion = TrackMotion.Bob, Speed = 2.4f, Height = 1.6f },
        Make(AimFamily.Track, "tech-curve-track", "curves without corners", "curvas sin esquinas",
            "The target circles slowly. Draw the circle with it: a curve made of straight flicks means the hand is too tense.",
            "El blanco gira despacio en círculo. Dibuja el círculo con él: una curva hecha de flicks rectos indica que la mano va demasiado tensa.")
            with { Radius = 0.2f, Motion = TrackMotion.Orbit, Speed = 2.2f, Width = 3f },
        Make(AimFamily.Track, "tech-pace-match", "matching a changing pace", "igualar un ritmo cambiante",
            "The target keeps speeding up and slowing down. Read its pace and change yours with it instead of catching up late.",
            "El blanco acelera y frena continuamente. Lee su ritmo y cambia el tuyo con él en lugar de alcanzarlo tarde.")
            with { Radius = 0.26f, Motion = TrackMotion.Surge, Speed = 4f },
        Make(AimFamily.Track, "tech-close-arm-track", "close targets with the arm", "blancos cercanos con el brazo",
            "A target strafing 5 m away crosses the screen fast. Follow it with the forearm; the wrist alone runs out of range.",
            "Un blanco a 5 m cruza la pantalla muy rápido. Síguelo con el antebrazo; la muñeca sola se queda sin recorrido.")
            with { Radius = 0.3f, DistMin = 5, DistMax = 5, Speed = 4.5f, Width = 4f },
        Make(AimFamily.Micro, "tech-body-to-head", "from body to head", "del cuerpo a la cabeza",
            "Each head appears just above your crosshair. One short move up and the click: the correction after a body-height spot.",
            "Cada cabeza aparece justo encima de tu mira. Un movimiento corto hacia arriba y el clic: la corrección tras apuntar a la altura del cuerpo.")
            with { TapOnly = true, Pattern = AimPattern.Ladder, Radius = 0.1f, OffMin = 0.8f, OffMax = 2.5f, PitchMin = 0f },
        Make(AimFamily.Micro, "tech-micro-moving", "correcting onto a moving point", "corregir sobre un punto móvil",
            "Small heads that slide slowly. Correct to where the head is going to be when you click.",
            "Cabezas pequeñas que se deslizan despacio. Corrige hacia donde estará la cabeza cuando pulses.")
            with { TapOnly = true, Radius = 0.1f, OffMin = 1f, OffMax = 3f, Drift = 0.5f },
        Make(AimFamily.Reflex, "tech-two-tap", "hit, check, hit", "acierto, compruebo, acierto",
            "Each head needs two hits and moves after the first. Read its new position before the second click.",
            "Cada cabeza necesita dos impactos y se mueve tras el primero. Lee su nueva posición antes del segundo clic.")
            with { HitsToClear = 2, TapOnly = true, Window = 1.3f, Radius = 0.16f, Yaw = 14, MinAngle = 3 },
        Make(AimFamily.Reflex, "tech-eyes-first", "eyes first", "primero los ojos",
            "Heads pop near the edge of the screen. Look at the head, then move the hand to where you are looking.",
            "Las cabezas aparecen cerca del borde de la pantalla. Mira la cabeza y después lleva la mano adonde estás mirando.")
            with { TapOnly = true, Radius = 0.18f, Yaw = 42, MinAngle = 26, PitchMin = -2, PitchMax = 9, Window = 1.1f },
    };
}
