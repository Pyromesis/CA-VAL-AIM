using CaValAim.Analysis;

namespace CaValAim.Core;

/// <summary>An external video the Academy links to. The app only opens the link in the browser; the video belongs to
/// its author, who is credited by channel name next to it (and in CREDITS.md).</summary>
public sealed record VideoRef(string Title, string Author, string Url, string Language = "en");

/// <summary>One technique lesson: what it is, how to do it, what usually goes wrong, the cue to keep in mind while
/// playing, the drills that train it and where to watch it explained.</summary>
public sealed record Technique(
    string Id, Skill Skill,
    string NameEn, string NameEs,
    string SummaryEn, string SummaryEs,
    string[] StepsEn, string[] StepsEs,
    string[] MistakesEn, string[] MistakesEs,
    string CueEn, string CueEs,
    string[] Drills, VideoRef[] Videos, string[] Diagnoses)
{
    public string Name => Loc.Spanish ? NameEs : NameEn;
    public string Summary => Loc.Spanish ? SummaryEs : SummaryEn;
    public string[] Steps => Loc.Spanish ? StepsEs : StepsEn;
    public string[] Mistakes => Loc.Spanish ? MistakesEs : MistakesEn;
    public string Cue => Loc.Spanish ? CueEs : CueEn;
}

/// <summary>
/// The coach's technique library (menu → ACADEMY, and "Technique" on every coach finding). The lessons are written
/// for this project; the linked videos are third-party guides, credited to their authors. Each lesson names the
/// coach diagnoses it answers, so a finding leads straight to the technique that fixes it and to drills for it.
/// </summary>
public static class Academy
{
    static VideoRef Yt(string id, string author, string title, string lang = "en") =>
        new(title, author, "https://www.youtube.com/watch?v=" + id, lang);

    public static readonly Technique[] All =
    {
        new("method", Skill.Flicking,
            "How to practise", "Cómo entrenar",
            "Aim improves with short, focused, regular practice on the thing you are worst at, not with hours of the drill you already enjoy.",
            "El aim mejora con práctica corta, concentrada y regular sobre lo que peor haces, no con horas del ejercicio que ya te gusta.",
            new[]
            {
                "Split warm-up from training. A warm-up (5–12 min before ranked) wakes up what you already have; training (20–40 min, any other moment) builds something new.",
                "Pick one focus per session: the first problem in your coach profile. Everything else is maintenance.",
                "Accuracy first, speed second. Slow down until about 85–90% of attempts are clean, then speed up in small steps.",
                "Play blocks of 3 runs of the same drill and read the coach note between them. Change one thing at a time.",
                "Stop when your scores drop twice in a row: tired repetitions teach tired habits. 20 good minutes a day beat two hours on Sunday.",
                "Take the skill into a deathmatch or a match the same day with a single goal (for example: only head-level crosshair).",
            },
            new[]
            {
                "Separa calentar de entrenar. El calentamiento (5–12 min antes de competitiva) despierta lo que ya tienes; el entrenamiento (20–40 min, en otro momento) construye algo nuevo.",
                "Elige un único objetivo por sesión: el primer problema de tu perfil del coach. Lo demás es mantenimiento.",
                "Primero precisión, después velocidad. Baja el ritmo hasta que el 85–90 % de los intentos salgan limpios y acelera poco a poco.",
                "Juega bloques de 3 rondas del mismo ejercicio y lee la nota del coach entre ellas. Cambia una sola cosa cada vez.",
                "Para cuando tu puntuación baje dos veces seguidas: las repeticiones cansadas enseñan hábitos cansados. 20 minutos buenos al día valen más que dos horas el domingo.",
                "Lleva la habilidad a un deathmatch o a una partida el mismo día con un solo objetivo (por ejemplo: solo mira a la altura de la cabeza).",
            },
            new[] { "Chasing the score instead of the technique.", "Changing sensitivity, mouse or crosshair every few days.", "Only training what you are already good at." },
            new[] { "Perseguir la puntuación en vez de la técnica.", "Cambiar de sensibilidad, ratón o mira cada pocos días.", "Entrenar solo lo que ya se te da bien." },
            "One goal per session. Clean first, fast later.",
            "Un objetivo por sesión. Primero limpio, luego rápido.",
            new[] { "flickx:tech-clean-pace", "trackv:tech-smooth-slow", "microx:tech-one-correction" },
            new[]
            {
                Yt("JAu24DzgRek", "West Proter", "The Ultimate Aim Training Guide"),
                Yt("Ajl6N8cvVXo", "Lew", "How to start aim training"),
                Yt("yQ7EDnqGkLg", "Aimlabs", "Calm Aim Training Routine for VALORANT (Full Guide + Drills)"),
                Yt("VXqO4q0h93I", "Horcus", "Cómo mejorar el aim en VALORANT: rutina y guía completa", "es"),
                Yt("y0SMsQ72RDM", "Oscar Molina Garcia", "5 motivos por los que tu aim no mejora", "es"),
            },
            Array.Empty<string>()),

        new("tension", Skill.Tracking,
            "Grip, posture and tension", "Agarre, postura y tensión",
            "A shaking crosshair almost always comes from a hand that squeezes too hard. Tension is a tool you switch on to stop the mouse, not a state you stay in.",
            "Una mira que tiembla casi siempre viene de una mano que aprieta demasiado. La tensión es una herramienta que activas para frenar el ratón, no un estado permanente.",
            new[]
            {
                "Sit so the forearm rests on the desk and the elbow is roughly level with the mouse pad. The shoulder stays down.",
                "Hold the mouse with the minimum force that keeps it under control: about 3 out of 10. If your knuckles go white, it is too much.",
                "Large moves come from the arm and elbow, medium moves from the wrist, the last millimetres from the fingers.",
                "Tense only for the instant you brake on a target, then let go again. Breathe out while you track.",
                "Between runs, open the hand fully and shake it out for three seconds.",
                "Train it slowly: follow a slow target with the loosest grip that still stays on it, then raise the speed.",
            },
            new[]
            {
                "Siéntate con el antebrazo apoyado en la mesa y el codo más o menos a la altura de la alfombrilla. El hombro, bajo.",
                "Sujeta el ratón con la fuerza mínima que lo mantiene bajo control: un 3 sobre 10. Si los nudillos se ponen blancos, es demasiado.",
                "Los movimientos grandes salen del brazo y el codo, los medios de la muñeca y los últimos milímetros de los dedos.",
                "Tensa solo en el instante de frenar sobre el blanco y vuelve a soltar. Suelta el aire mientras sigues un objetivo.",
                "Entre rondas, abre la mano del todo y sacúdela tres segundos.",
                "Entrénalo despacio: sigue un blanco lento con el agarre más suelto que consiga mantenerse encima y sube la velocidad después.",
            },
            new[] { "Death grip during the whole fight.", "Wrist anchored so hard that the arm cannot help.", "Raising the shoulder or leaning into the screen when the fight starts." },
            new[] { "Apretar el ratón durante todo el duelo.", "Anclar tanto la muñeca que el brazo no puede ayudar.", "Subir el hombro o echarse sobre la pantalla cuando empieza el duelo." },
            "Loose hand. Tense only to stop.",
            "Mano suelta. Tensa solo para frenar.",
            new[] { "trackv:tech-smooth-slow", "trackv:tech-smooth-small", "flickx:tech-still-hold" },
            new[]
            {
                Yt("LvgicX0HOe8", "West Proter", "Aim Coaching To Make SHAKY AIM Into SMOOTH AIM"),
                Yt("gNDfH7J2DNE", "West Proter", "Fix Your Terrible Mouse Grip NATURALLY"),
                Yt("9vmQWQL6AMY", "Sensu", "How to Switch to Arm Aiming (Ergonomics Guide)"),
                Yt("a-MShVYe3kY", "Corporate Serf", "The Corporate Serf Smoothness Training Method - Aim Trainer Tracking"),
            },
            new[] { "AIM_TREMOR", "JITTER", "LEFT_WEAK", "RIGHT_WEAK", "UP_WEAK", "DOWN_WEAK" }),

        new("flick", Skill.Flicking,
            "Flick and brake", "Flick y frenada",
            "A good flick is one fast move that slows down as it arrives, so the crosshair lands on the target instead of flying past it.",
            "Un buen flick es un solo movimiento rápido que frena al llegar, de modo que la mira aterriza en el blanco en vez de pasarse.",
            new[]
            {
                "Look at the target first. The eyes arrive, then the hand follows to where you are looking.",
                "Start fast, decelerate over the last third of the distance. Think \"accelerate, brake, click\", not \"throw and hope\".",
                "Click when the crosshair has stopped on the target, not while it is still travelling.",
                "If you keep going past the target, aim to stop slightly short and add one small correction; if you keep stopping short, commit to the full distance.",
                "Wide flicks start from the arm; do not try to cover 60° with the wrist alone.",
                "Raise the speed only while your overshoots stay under about one in four flicks.",
            },
            new[]
            {
                "Mira primero al blanco. Llegan los ojos y la mano va después hacia donde miras.",
                "Empieza rápido y desacelera en el último tercio del recorrido. Piensa «acelero, freno, clic», no «lanzo y a ver».",
                "Dispara cuando la mira se haya detenido sobre el blanco, no mientras sigue viajando.",
                "Si te sueles pasar, intenta frenar un pelo antes y añade una corrección pequeña; si te quedas corto, comprométete con el recorrido completo.",
                "Los flicks amplios salen del brazo; no intentes cubrir 60° solo con la muñeca.",
                "Sube la velocidad solo mientras te pases en menos de uno de cada cuatro flicks, más o menos.",
            },
            new[] { "Clicking during the movement.", "Same hand speed for a 5° and a 50° flick.", "Lowering sensitivity to hide a braking problem." },
            new[] { "Disparar durante el movimiento.", "Usar la misma velocidad de mano para un flick de 5° y uno de 50°.", "Bajar la sensibilidad para esconder un problema de frenada." },
            "Accelerate, brake, click.",
            "Acelero, freno, clic.",
            new[] { "flickx:tech-brake-ladder", "flickx:coach-brake-near", "flickx:coach-brake-wide", "flick" },
            new[]
            {
                Yt("xAE2uPM59FY", "Aimlabs", "Better Flicking Guaranteed! | Aim Guide"),
                Yt("Pvu9QcGQl3A", "Struth Gaming", "How to Flick Shot (complete guide)"),
            },
            new[] { "OVERFLICK", "OVER_NEAR", "OVER_WIDE", "RECOVERED_OVER", "UNDERFLICK", "MIXED_FLICK", "CURVED", "SLOW_START" }),

        new("micro", Skill.Precision,
            "Micro-corrections", "Microcorrecciones",
            "Most kills are decided by the last two degrees. A micro-correction is one small, deliberate move onto the head, followed by the click.",
            "La mayoría de las bajas se deciden en los dos últimos grados. Una microcorrección es un único movimiento pequeño y deliberado hasta la cabeza, seguido del clic.",
            new[]
            {
                "After the flick, check where the crosshair really is before you click. That check takes less than a tenth of a second.",
                "Correct with the fingers and the wrist, never with a second big arm move.",
                "One correction, then shoot. If you need three, the first move was too fast: slow the flick down.",
                "Practise on small offsets (0.5°–3°) with tiny targets until the first shot is the one that hits.",
                "Keep the rhythm: correction and click belong together, there is no pause to admire the crosshair.",
            },
            new[]
            {
                "Tras el flick, comprueba dónde está de verdad la mira antes de disparar. Esa comprobación dura menos de una décima.",
                "Corrige con los dedos y la muñeca, nunca con un segundo movimiento grande de brazo.",
                "Una corrección y disparo. Si necesitas tres, el primer movimiento fue demasiado rápido: frena el flick.",
                "Practica con desvíos pequeños (0,5°–3°) y blancos diminutos hasta que el primer disparo sea el que acierta.",
                "Mantén el ritmo: corrección y clic van juntos, sin una pausa para admirar la mira.",
            },
            new[] { "Shooting as soon as the flick ends, wherever it ended.", "Spraying to fix a missed first bullet.", "Correcting with the whole arm." },
            new[] { "Disparar en cuanto acaba el flick, haya acabado donde haya acabado.", "Hacer spray para arreglar una primera bala fallada.", "Corregir con todo el brazo." },
            "Land, check, one small move, click.",
            "Llego, compruebo, un ajuste pequeño, clic.",
            new[] { "microx:tech-one-correction", "microx:coach-micro-confirm", "microshot" },
            new[]
            {
                Yt("s0BuZ4sQpsU", "Zapper", "Aim Guide for PERFECT Micro Corrections (Ep. 1)"),
                Yt("x9q6d0vBFag", "Lorys", "the KEY approach for AIM CONSISTENCY"),
            },
            new[] { "MICRO_CONFIRM", "NO_MICRO", "SLOW_MICRO", "PREMATURE" }),

        new("smooth", Skill.Tracking,
            "Smooth tracking", "Seguimiento suave",
            "Tracking is matching the target's speed, not chasing it. The crosshair should glide with the target instead of catching up in jumps.",
            "Seguir un objetivo es igualar su velocidad, no perseguirlo. La mira debe deslizarse con el blanco en vez de alcanzarlo a saltos.",
            new[]
            {
                "Watch the target, not your crosshair. The hand copies what the eyes follow.",
                "Move the mouse at the target's speed in one continuous motion; do not stop and restart.",
                "When it changes direction, slow down, turn and speed up again. Do not snap back with a flick.",
                "If you trail behind, you are reacting late: read the target's body, expect the reversal.",
                "If you run past on each reversal, you are moving faster than the target: slow your hand.",
                "Start with slow, large targets and a loose grip; shrink the target before you raise the speed.",
            },
            new[]
            {
                "Mira al blanco, no a tu mira. La mano copia lo que siguen los ojos.",
                "Mueve el ratón a la velocidad del blanco en un movimiento continuo; no pares y arranques.",
                "Cuando cambie de dirección, frena, gira y vuelve a acelerar. No recuperes con un flick.",
                "Si vas por detrás, reaccionas tarde: lee el cuerpo del objetivo y espera el cambio.",
                "Si te pasas en cada cambio, vas más rápido que el blanco: frena la mano.",
                "Empieza con blancos lentos y grandes y agarre suelto; reduce el blanco antes de subir la velocidad.",
            },
            new[] { "Tracking in a chain of small flicks.", "Staring at the crosshair.", "Tensing the whole arm to \"hold\" the target." },
            new[] { "Seguir con una cadena de pequeños flicks.", "Mirar fijamente la mira.", "Tensar todo el brazo para «sujetar» el blanco." },
            "Match its speed. Eyes on the target.",
            "Iguala su velocidad. Ojos en el blanco.",
            new[] { "trackv:tech-smooth-slow", "trackv:tech-reversal-read", "tracking", "reacttrack" },
            new[]
            {
                Yt("CMQh7HKSrdA", "Daniel Kapadia", "Why Your Tracking Aim NEEDS This Training Method"),
                Yt("a-MShVYe3kY", "Corporate Serf", "The Corporate Serf Smoothness Training Method - Aim Trainer Tracking"),
                Yt("wD-WRSD3LmQ", "MattyOW", "Scucchi, God's Smoothest Aimer - Unraveling the Secrets of Aim Ep. 4"),
            },
            new[] { "JITTER", "TRACK_BEHIND", "TRACK_OVERRUN", "SLOW_REACQ" }),

        new("switch", Skill.Flicking,
            "Target switching", "Cambio de objetivo",
            "With several enemies on screen, the order you shoot them in and how cleanly you leave one for the next matter as much as raw speed.",
            "Con varios enemigos en pantalla, el orden en que los disparas y lo limpio que sales de uno hacia el siguiente importan tanto como la velocidad.",
            new[]
            {
                "Decide the order before the first shot: nearest to your crosshair first, then sweep in one direction.",
                "Confirm the kill (sound or the target disappearing), then move. Leaving early costs a second trip.",
                "Your eyes jump to the next target while the hand finishes the current one.",
                "Each switch is a small flick with its own brake; do not drag the crosshair across.",
            },
            new[]
            {
                "Decide el orden antes del primer disparo: primero el más cercano a tu mira y luego barre en una sola dirección.",
                "Confirma la baja (sonido o el blanco desaparece) y entonces muévete. Salir antes cuesta un segundo viaje.",
                "Los ojos saltan al siguiente blanco mientras la mano termina el actual.",
                "Cada cambio es un flick pequeño con su propia frenada; no arrastres la mira.",
            },
            new[] { "Zig-zagging between far targets.", "Leaving a target at one hit from dying.", "Looking at the target you are already shooting." },
            new[] { "Ir en zigzag entre blancos lejanos.", "Dejar un objetivo a un impacto de morir.", "Mirar al blanco al que ya estás disparando." },
            "Nearest first. Confirm, then move.",
            "Primero el más cercano. Confirma y muévete.",
            new[] { "flickx:tech-sweep-order", "switch", "gridshot" },
            new[] { Yt("y-xYxbYl1d0", "Voltaic", "Voltaic Speed Target Switching Training") },
            new[] { "TARGET_ORDER" }),

        new("placement", Skill.CrosshairPlacement,
            "Crosshair placement", "Colocación de mira",
            "The fastest flick is the one you do not need. Keep the crosshair where a head will appear and most duels become a click.",
            "El flick más rápido es el que no necesitas. Mantén la mira donde va a aparecer una cabeza y la mayoría de duelos se quedan en un clic.",
            new[]
            {
                "Head height at all times: find a reference on the map (a line on a wall, a box edge) and keep the crosshair on it while you move.",
                "Aim at the edge of cover the enemy will come from, a little away from the wall, not at the wall itself.",
                "Clear one angle at a time (slice the pie): show yourself to one position, check it, then the next.",
                "Adjust height for distance and stairs: far heads are lower on the screen, enemies on a box are higher.",
                "Never look at the floor while rotating, reloading or using utility.",
                "In deathmatch, play a full game where the only goal is that every enemy appears on your crosshair.",
            },
            new[]
            {
                "Altura de cabeza siempre: busca una referencia en el mapa (una línea en la pared, el borde de una caja) y mantén la mira en ella mientras te mueves.",
                "Apunta al borde de la cobertura por donde saldrá el enemigo, un poco separado de la pared, no a la pared.",
                "Limpia un ángulo cada vez (cortar el pastel): exponte a una posición, compruébala y pasa a la siguiente.",
                "Ajusta la altura según distancia y desniveles: las cabezas lejanas quedan más abajo en pantalla y un enemigo sobre una caja, más arriba.",
                "No mires al suelo al rotar, recargar o usar utilidad.",
                "En deathmatch, juega una partida entera con un único objetivo: que cada enemigo aparezca sobre tu mira.",
            },
            new[] { "Crosshair at chest or floor height.", "Hugging the wall with the crosshair so the enemy appears beside it.", "Clearing two angles at once." },
            new[] { "Mira a la altura del pecho o del suelo.", "Pegar la mira a la pared, de modo que el enemigo aparece al lado.", "Limpiar dos ángulos a la vez." },
            "Head height, off the wall, one angle at a time.",
            "Altura de cabeza, separado de la pared, un ángulo cada vez.",
            new[] { "flickx:tech-head-line", "siteclear", "peek", "peek_mix" },
            new[]
            {
                Yt("1vKfPS0OlGA", "Konpeki", "An Updated Crosshair Placement Guide for 2024"),
                Yt("McObrCzx4Tc", "THE GUIDE - Valorant", "How To PEEK & CLEAR Angles! Crosshair Placement Guide Part 2"),
                Yt("1lfZ3GJUPpA", "Jaeky", "How to Master Crosshair Placement (Valorant Tips)"),
                Yt("IBgGYuzgrvE", "Ernezt3d", "Crosshair Placement en Valorant: deja de apuntar al suelo", "es"),
            },
            new[] { "XHAIR_LOW", "XHAIR_WIDE" }),

        new("stop", Skill.Movement,
            "Stopping before you shoot", "Parar antes de disparar",
            "In VALORANT rifles are only accurate when you are nearly still. Every duel is move, stop, shoot, move again.",
            "En VALORANT los rifles solo son precisos cuando estás casi quieto. Cada duelo es moverse, parar, disparar y volver a moverse.",
            new[]
            {
                "Strafe out, then stop: release the key, or tap the opposite key for an instant (counter-strafe) to stop sooner.",
                "Shoot only once the stop has finished. The first bullet is the accurate one.",
                "Fire a short burst (1–3 bullets), then strafe again. Standing still after the burst makes you an easy target.",
                "Aim with the mouse in the opposite direction of your strafe as you stop, so the crosshair stays on the head.",
                "Train the rhythm without enemies first: A, stop, shot, D, stop, shot.",
            },
            new[]
            {
                "Sal de lado y para: suelta la tecla, o toca un instante la tecla contraria (counter-strafe) para frenar antes.",
                "Dispara solo cuando la parada haya terminado. La primera bala es la precisa.",
                "Haz una ráfaga corta (1–3 balas) y vuelve a moverte. Quedarte quieto tras la ráfaga te convierte en un blanco fácil.",
                "Mientras frenas, compensa con el ratón en sentido contrario a tu desplazamiento para que la mira siga en la cabeza.",
                "Entrena el ritmo primero sin enemigos: A, parar, disparo, D, parar, disparo.",
            },
            new[] { "Shooting while still sliding.", "Holding the opposite key too long and moving the other way.", "Crouching in every fight instead of stopping." },
            new[] { "Disparar mientras aún te deslizas.", "Mantener demasiado la tecla contraria y salir hacia el otro lado.", "Agacharse en cada duelo en vez de parar." },
            "Move, stop, shoot, move.",
            "Me muevo, paro, disparo, me muevo.",
            new[] { "counterstrafe", "anti-mirror", "jigglepeek", "strafebots" },
            new[]
            {
                Yt("yZwhnmZI69Y", "Arc", "What Is Counter-Strafing? - Valorant 101"),
                Yt("mnQoX4JKORU", "royalG", "My Secrets for Mastering Movement (Valorant Fundamentals Guide)"),
                Yt("LEzBXTiUQXA", "CodyAC", "How to strafe, burst, deadzone in Valorant."),
                Yt("6ioMB4_AL1A", "ValorantDomingo", "[Paso a paso] Cómo mejorar tu counter strafe en VALORANT", "es"),
            },
            new[] { "RUN_GUN", "CLUMSY_MOVE", "MIRRORING" }),

        new("peek", Skill.Movement,
            "Peeking", "Asomarse",
            "How you come round a corner decides who shoots first. Choose the peek for the information you have, with the crosshair already placed.",
            "Cómo sales de una esquina decide quién dispara primero. Elige la salida según la información que tienes, con la mira ya colocada.",
            new[]
            {
                "Jiggle (shoulder) peek: show a shoulder and come back to bait a shot or get information without committing.",
                "Wide swing: come out far and fast when you know where the enemy is aiming; they have to move their crosshair, you do not.",
                "Tight peek: when you do not know, step out just enough to see one angle, stop and shoot.",
                "Stand back from the corner. The further you are from the wall, the earlier you see the enemy.",
                "Pre-aim the spot before you move. The peek ends with a stop and a shot, never with a flick.",
            },
            new[]
            {
                "Asomar el hombro (jiggle): enseña un hombro y vuelve para provocar un disparo o conseguir información sin comprometerte.",
                "Salida amplia: sal lejos y rápido cuando sabes adónde apunta el enemigo; él tiene que mover la mira y tú no.",
                "Salida ajustada: cuando no lo sabes, sal lo justo para ver un ángulo, para y dispara.",
                "Sepárate de la esquina. Cuanto más lejos de la pared estés, antes ves al enemigo.",
                "Preapunta el punto antes de moverte. La salida acaba con una parada y un disparo, nunca con un flick.",
            },
            new[] { "Peeking glued to the wall.", "Peeking the same way twice in a row.", "Walking out slowly into an angle that is already held." },
            new[] { "Asomarse pegado a la pared.", "Asomarse dos veces seguidas de la misma forma.", "Salir andando despacio a un ángulo que ya te están sujetando." },
            "Pre-aim, step out, stop, shoot.",
            "Preapunto, salgo, paro, disparo.",
            new[] { "peek_mix", "jigglepeek", "jumppeek", "peekduel" },
            new[]
            {
                Yt("W-Qce5B9ChM", "Konpeki", "An Updated Peeking Guide for 2024"),
                Yt("oOzR2GGlIfM", "Aimlabs", "The Ultimate Peeking Guide - Tips and Tricks for Peeking in VALORANT"),
                Yt("xuKZm_8CLq8", "SeLFlo", "How to perfect SHOULDER Peek & JUMP Peek in VALORANT (Guide)"),
            },
            new[] { "HESITATE" }),

        new("spray", Skill.SprayControl,
            "Tap, burst and spray", "Tap, ráfaga y spray",
            "Pick the fire mode for the distance. The recoil of the first bullets is predictable; after that the spread decides.",
            "Elige el modo de disparo según la distancia. El retroceso de las primeras balas es predecible; después manda la dispersión.",
            new[]
            {
                "Long range (25 m+): single taps or two-bullet bursts, resetting between them.",
                "Medium range: bursts of 3–5 bullets, pulling down gently from the second bullet.",
                "Close range: spray, pull down steadily and let the horizontal sway happen; aim at the upper chest.",
                "Start pulling down at once: the climb begins with the second bullet, not the fifth.",
                "If the spray is lost after ~8 bullets, stop, strafe and start a new burst instead of fighting it.",
                "Learn the first 10 bullets of your rifle on a wall, then on a target, then while strafing between bursts.",
            },
            new[]
            {
                "Larga distancia (25 m o más): tiros sueltos o ráfagas de dos balas, reiniciando entre ellas.",
                "Media distancia: ráfagas de 3–5 balas, tirando suavemente hacia abajo desde la segunda bala.",
                "Corta distancia: spray, baja de forma constante y deja que el vaivén horizontal ocurra; apunta al pecho alto.",
                "Empieza a bajar enseguida: la subida empieza en la segunda bala, no en la quinta.",
                "Si el spray se pierde tras unas 8 balas, para, muévete y empieza otra ráfaga en vez de pelearte con él.",
                "Aprende las 10 primeras balas de tu rifle contra una pared, luego sobre un blanco y luego moviéndote entre ráfagas.",
            },
            new[] { "Full sprays at long range.", "Pulling down too late or too hard.", "Crouch-spraying every fight." },
            new[] { "Spray completo a larga distancia.", "Bajar demasiado tarde o demasiado fuerte.", "Agacharse y hacer spray en todos los duelos." },
            "Far: tap. Mid: burst. Close: spray and pull down.",
            "Lejos: tap. Media: ráfaga. Cerca: spray y bajo.",
            new[] { "spray_vandal", "spray_phantom", "spray_transfer", "longtaps" },
            new[]
            {
                Yt("OINm1ev_pf4", "Kelly Jepsen", "Tapping, Bursting and Recoil Control in VALORANT"),
                Yt("YcYR5ixYeQM", "quach", "Valorant - How to ACTUALLY Master Spray Control"),
            },
            new[] { "SPRAY_VERT", "SPRAY_HORIZ" }),

        new("reaction", Skill.Reaction,
            "Reacting faster", "Reaccionar antes",
            "Raw reaction time barely changes with training. What changes is how ready you are: where you look, what you expect and how little you have to move.",
            "El tiempo de reacción puro apenas cambia con entrenamiento. Lo que cambia es lo preparado que estás: adónde miras, qué esperas y lo poco que tienes que moverte.",
            new[]
            {
                "Good crosshair placement turns a reaction plus a flick into a reaction plus a click.",
                "Decide before the enemy appears what you will do (shoot, fall back, turn from the flash). A decision made in advance is fast.",
                "Look at the place where the enemy will appear, with a relaxed hand ready to click.",
                "Do not guess: clicking before the signal teaches you to shoot at nothing. Wait, then commit.",
                "Sleep, a warm hand and a stable frame rate are worth more milliseconds than any drill.",
            },
            new[]
            {
                "Una buena colocación de mira convierte reacción más flick en reacción más clic.",
                "Decide antes de que aparezca el enemigo qué vas a hacer (disparar, retroceder, girarte ante la flash). Una decisión tomada de antemano es rápida.",
                "Mira el punto por donde aparecerá el enemigo, con la mano relajada y lista para pulsar.",
                "No adivines: pulsar antes de la señal te enseña a disparar a la nada. Espera y luego comprométete.",
                "Dormir, tener la mano caliente y unos FPS estables valen más milisegundos que cualquier ejercicio.",
            },
            new[] { "Anticipating the signal.", "Staring at the centre of the screen instead of the angle.", "Playing tense, which slows the first move." },
            new[] { "Anticiparse a la señal.", "Mirar al centro de la pantalla en vez de al ángulo.", "Jugar en tensión, lo que retrasa el primer movimiento." },
            "Ready, not guessing.",
            "Preparado, sin adivinar.",
            new[] { "reaction", "reflex:tech-wait-commit", "popup", "flashmap" },
            Array.Empty<VideoRef>(),
            new[] { "SLOW_REACT", "FLASH_SLOW" }),

        new("sens", Skill.Flicking,
            "Choosing a sensitivity", "Elegir sensibilidad",
            "There is no perfect sensitivity, only a range that lets you both turn and stop. Find it once, with data, and then leave it alone.",
            "No existe la sensibilidad perfecta, solo un rango que te deja girar y también frenar. Encuéntrala una vez, con datos, y después déjala quieta.",
            new[]
            {
                "Work in cm/360 (the mouse distance for a full turn): it compares setups with different DPI. Most VALORANT players are comfortable between about 30 and 60 cm/360.",
                "Turn off mouse acceleration in Windows (\"Enhance pointer precision\") and keep the same DPI everywhere.",
                "Too high: you overshoot small targets and the crosshair shakes. Too low: you stop short and cannot follow close targets or turn 180°.",
                "Use the Sens Finder for a starting point, then the Sens Lab A/B test to compare two values on the same targets.",
                "Change in small steps (5–10%) and keep the new value for at least two weeks before judging it.",
                "A sensitivity change never fixes technique: if you overshoot at every sensitivity, train braking.",
            },
            new[]
            {
                "Trabaja en cm/360 (lo que recorre el ratón en un giro completo): permite comparar configuraciones con distinto DPI. La mayoría de jugadores de VALORANT están cómodos entre unos 30 y 60 cm/360.",
                "Desactiva la aceleración del ratón en Windows («Mejorar la precisión del puntero») y usa el mismo DPI en todo.",
                "Demasiado alta: te pasas en blancos pequeños y la mira tiembla. Demasiado baja: te quedas corto y no puedes seguir blancos cercanos ni girar 180°.",
                "Usa el Buscador de sensibilidad para tener un punto de partida y después la prueba A/B del Sens Lab para comparar dos valores con los mismos blancos.",
                "Cambia en pasos pequeños (5–10 %) y mantén el nuevo valor al menos dos semanas antes de juzgarlo.",
                "Cambiar de sensibilidad nunca arregla la técnica: si te pasas con cualquier sensibilidad, entrena la frenada.",
            },
            new[] { "Copying a professional's sensitivity.", "Changing it after one bad match.", "Different sensitivity in the trainer and in the game." },
            new[] { "Copiar la sensibilidad de un profesional.", "Cambiarla tras una mala partida.", "Sensibilidad distinta en el entrenador y en el juego." },
            "Measure, change little, keep it.",
            "Mide, cambia poco, mantenla.",
            new[] { "sensfinder", "flickx:coach-brake-wide", "microx:coach-micro-confirm", "tracking" },
            new[]
            {
                Yt("9yaYs34Oqeo", "Aimlabs", "The ONLY Sensitivity Guide You'll Ever Need | All FPS Games"),
                Yt("qbdqeIwRXC4", "Pandase", "How to Find your Perfect Sensitivity in Valorant (PSA Method)"),
            },
            Array.Empty<string>()),
    };

    public static Technique? Find(string id) => All.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The lesson that answers a coach finding: by diagnosis id first, then the lesson of the finding's skill.</summary>
    public static Technique? For(Diagnosis d)
    {
        foreach (var t in All)
            if (t.Diagnoses.Any(id => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) return t;
        // Variants of a known finding ("OVERFLICK_LARGE", "JITTER_FAST") inherit its lesson.
        foreach (var t in All)
            if (t.Diagnoses.Any(id => d.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase))) return t;
        return d.Skill switch
        {
            Skill.Flicking => Find("flick"),
            Skill.Precision => Find("micro"),
            Skill.Tracking => Find("smooth"),
            Skill.Reaction => Find("reaction"),
            Skill.CrosshairPlacement => Find("placement"),
            Skill.SprayControl => Find("spray"),
            Skill.Movement => Find("stop"),
            _ => null,
        };
    }

    /// <summary>Every credited author, for the credits list.</summary>
    public static IEnumerable<string> Authors => All.SelectMany(t => t.Videos).Select(v => v.Author).Distinct().OrderBy(a => a, StringComparer.OrdinalIgnoreCase);
}
