using System.Globalization;
using CaValAim.Core;
using CaValAim.Modes;

namespace CaValAim.Analysis;

/// <summary>
/// The coach's drill builder (2.10). <see cref="PersonalDrills"/> picks from authored templates; when the coach has
/// the player's measured numbers it builds the drill from them instead: the target is sized to the error the player
/// actually makes (how far the flick ends past the target, how wide the tremor is, how far the crosshair trails),
/// the flick distances are the ones the player was measured on, and the tracking speed follows the measured jitter.
/// As the numbers improve, the next plan builds a tighter drill, so the task always sits just above what the player
/// can already do.
///
/// A built drill is an ordinary <see cref="AimVariant"/> whose id carries its parameters
/// (<c>forge-over-12-30-10</c>), so it is reproducible: the same id always gives the same drill, stats and replays
/// keep working, and nothing has to be stored. Parameters are rounded to coarse steps so the key stays the same while
/// the player's numbers are stable.
/// </summary>
public static class CoachForge
{
    public const string Prefix = "forge-";
    /// <summary>Every built flick / micro target stands at this distance, so a size in degrees maps to one radius.</summary>
    const float Dist = 14f;

    public static bool IsForged(string id) => id.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Mode key ("flickx:forge-over-12-30-10") of a drill built for this finding from these metrics, or null
    /// when the finding has no built form or the metrics do not carry enough measurements for it.</summary>
    public static string? For(Diagnosis issue, IReadOnlyDictionary<string, float>? m)
    {
        if (m == null || m.Count == 0) return null;
        float G(string k, float fallback = float.NaN) => m.TryGetValue(k, out var v) && float.IsFinite(v) ? v : fallback;
        bool flicks = G("flick.n_still", 0) >= 12 || G("flick.n_prim", 0) >= 12;
        bool tracking = G("track.secs", 0) >= 10;
        float headDeg = G("flick.r_deg", 0.57f);
        float amp = G("flick.amp_deg", 20f);

        string? id = issue.Id switch
        {
            "OVERFLICK" or "RECOVERED_OVER" when flicks && G("flick.mean_overshoot_deg") > 0 =>
                Id("over", Cm(G("flick.mean_overshoot_deg") * 0.9f), Step(amp * 1.4f, 5, 15, 60)),
            "OVER_NEAR" when flicks && G("flick.mean_overshoot_deg") > 0 =>
                Id("over", Cm(G("flick.mean_overshoot_deg") * 0.9f), Step(amp * 0.9f, 5, 10, 25)),
            "OVER_WIDE" when flicks && G("flick.mean_overshoot_deg") > 0 =>
                Id("over", Cm(G("flick.mean_overshoot_deg") * 0.9f), 60),
            "UNDERFLICK" when flicks && G("flick.mean_undershoot_deg") > 0 =>
                Id("under", Cm(G("flick.mean_undershoot_deg") * 0.9f), Step(amp * 1.4f, 5, 15, 60)),
            "MIXED_FLICK" or "CURVED" when flicks && G("flick.end_scatter_r") > 0 =>
                Id("settle", Cm(G("flick.end_scatter_r") * headDeg), Step(amp * 1.2f, 5, 15, 50)),
            "MICRO_CONFIRM" or "NO_MICRO" or "SLOW_MICRO" or "PREMATURE" when flicks =>
                Id("micro", Cm(Math.Max(G("flick.shot_err_deg", headDeg) * 0.9f, 0.35f), 6, 24),
                    Step(G("flick.corr_amp_deg", 1.5f) * 10f, 2, 6, 40)),
            "AIM_TREMOR" when G("flick.tremor_n", 0) >= 8 && G("flick.tremor_r") > 0 =>
                Id("steady", Cm(G("flick.tremor_r") * headDeg * 1.6f, 6, 24)),
            "JITTER" when tracking && G("track.jitter") > 0 =>
                Id("smooth", Cm(G("track.r_deg", 1.2f) * 0.85f, 12, 40), Step((3.3f - G("track.jitter")) * 10f, 5, 15, 30)),
            "TRACK_BEHIND" when tracking =>
                Id("lag", Cm(Math.Max(MathF.Abs(G("track.lag_deg", 0.8f)) * 1.25f, 0.7f), 14, 44)),
            "TRACK_OVERRUN" or "SLOW_REACQ" when tracking =>
                Id("turn", Cm(Math.Max(G("track.overrun_deg", 0.9f) * 1.1f, 0.8f), 16, 48)),
            "SLOW_REACT" or "SLOW_START" when G("react.ms", G("flick.onset_ms")) > 0 =>
                Id("react", Step(G("react.ms", G("flick.onset_ms")) * 0.22f, 5, 50, 140)),
            "UP_WEAK" or "DOWN_WEAK" when flicks => Id("vert", Cm(headDeg * 1.15f)),
            "LEFT_WEAK" or "RIGHT_WEAK" when flicks => Id("horiz", Cm(headDeg * 1.15f)),
            _ => null,
        };
        return id == null ? null : Resolve(id)?.Key;
    }

    static string Id(string focus, params int[] values) =>
        Prefix + focus + "-" + string.Join("-", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Target radius in whole centimetres (even numbers) for an angular radius at <see cref="Dist"/>.</summary>
    static int Cm(float deg, int min = 8, int max = 36)
    {
        if (!float.IsFinite(deg) || deg <= 0) deg = 0.57f;
        float cm = MathF.Tan(deg * MathF.PI / 180f) * Dist * 100f;
        return Math.Clamp((int)MathF.Round(cm / 2f) * 2, min, max);
    }

    static int Step(float value, int step, int min, int max) =>
        !float.IsFinite(value) ? min : Math.Clamp((int)MathF.Round(value / step) * step, min, max);

    static float Deg(int cm) => MathF.Atan(cm / 100f / Dist) * 180f / MathF.PI;

    /// <summary>The drill a built id stands for; null for anything that is not a well-formed built id.</summary>
    public static AimVariant? Resolve(string id)
    {
        if (!IsForged(id)) return null;
        var parts = id.Split('-');
        if (parts.Length < 3) return null;
        var v = new int[parts.Length - 2];
        for (int i = 0; i < v.Length; i++)
            if (!int.TryParse(parts[i + 2], NumberStyles.None, CultureInfo.InvariantCulture, out v[i])) return null;
        int A(int i, int min, int max, int fallback) => i < v.Length ? Math.Clamp(v[i], min, max) : fallback;
        float r = A(0, 6, 48, 14) / 100f;
        string size = Deg(A(0, 6, 48, 14)).ToString("0.0", CultureInfo.InvariantCulture);

        AimVariant Build(AimFamily family, string en, string es, string cueEn, string cueEs) =>
            new AimVariant(id, family, "Coach-built · " + en, cueEn)
            {
                NameEs = "Creado por el coach · " + es, DescriptionEs = cueEs, CategoryOverride = "Sensitivity",
                Radius = r, DistMin = Dist, DistMax = Dist + 0.5f,
            };

        switch (parts[1])
        {
            case "over":
            {
                int yaw = A(1, 10, 60, 30);
                return Build(AimFamily.Flick, $"stopping on the target ({yaw}°)", $"frenar sobre el blanco ({yaw}°)",
                    $"Targets of {size}° sized to how far your flicks travel past the target, at the distances you were measured on (up to {yaw}°). Brake before you arrive and click once the crosshair has stopped. When most first clicks hit, the coach builds a tighter one.",
                    $"Blancos de {size}° ajustados a cuánto se pasan tus flicks, en las distancias en las que se te midió (hasta {yaw}°). Frena antes de llegar y pulsa cuando la mira se haya detenido. Cuando acierte la mayoría de los primeros clics, el coach creará uno más exigente.")
                    with { TapOnly = true, Yaw = yaw, MinAngle = Math.Max(3, yaw / 3) };
            }
            case "under":
            {
                int yaw = A(1, 10, 60, 30);
                return Build(AimFamily.Flick, $"reaching the target ({yaw}°)", $"llegar al blanco ({yaw}°)",
                    $"Targets of {size}° sized to how far your flicks stop short. Commit to the whole distance in one move; do not creep the last part.",
                    $"Blancos de {size}° ajustados a cuánto se quedan cortos tus flicks. Comprométete con todo el recorrido en un solo movimiento; no te arrastres en el tramo final.")
                    with { TapOnly = true, Yaw = yaw, MinAngle = Math.Max(4, yaw / 2) };
            }
            case "settle":
            {
                int yaw = A(1, 10, 60, 25);
                return Build(AimFamily.Flick, "landing in the same place", "llegar siempre al mismo sitio",
                    $"Targets of {size}° sized to the spread of where your flicks end. Same pace every time: one move, a short stop, one click.",
                    $"Blancos de {size}° ajustados a la dispersión de tus llegadas. El mismo ritmo siempre: un movimiento, una parada breve y un clic.")
                    with { TapOnly = true, Pattern = AimPattern.Horizontal, Yaw = yaw, MinAngle = Math.Max(4, yaw / 3) };
            }
            case "micro":
            {
                float off = A(1, 6, 40, 15) / 10f;
                return Build(AimFamily.Micro, "your correction distance", "tu distancia de corrección",
                    $"Heads of {size}° that appear about {off.ToString("0.0", CultureInfo.InvariantCulture)}° away: the size of the corrections you make after a flick. One small move and one click.",
                    $"Cabezas de {size}° que aparecen a unos {off.ToString("0.0", CultureInfo.InvariantCulture)}°: el tamaño de las correcciones que haces tras un flick. Un ajuste pequeño y un clic.")
                    with { TapOnly = true, OffMin = MathF.Max(0.3f, off * 0.5f), OffMax = MathF.Max(off * 0.5f + 0.5f, off * 1.6f) };
            }
            case "steady":
                return Build(AimFamily.Flick, "holding still", "mantener la mira quieta",
                    $"Hold fire and keep the crosshair inside a {size}° target for a third of a second: a little wider than your measured tremor. Loosen the grip until it stops shaking.",
                    $"Mantén disparo y deja la mira dentro de un blanco de {size}° durante un tercio de segundo: algo más ancho que el temblor que se te midió. Afloja el agarre hasta que deje de temblar.")
                    with { Count = 1, ContactSeconds = 0.33f, Yaw = 12, MinAngle = 2 };
            case "smooth":
            {
                float speed = A(1, 15, 30, 20) / 10f;
                return Build(AimFamily.Track, "smooth at your pace", "suavidad a tu ritmo",
                    $"A {size}° target at {speed.ToString("0.0", CultureInfo.InvariantCulture)} m/s, slow enough for your hand to stay loose. Glide with it; the coach raises the speed as your jitter drops.",
                    $"Un blanco de {size}° a {speed.ToString("0.0", CultureInfo.InvariantCulture)} m/s, lo bastante lento para que la mano vaya suelta. Deslízate con él; el coach subirá la velocidad cuando baje tu temblor.")
                    with { Speed = speed, TurnMin = 1.1f, TurnMax = 2.4f, Width = 7f };
            }
            case "lag":
                return Build(AimFamily.Track, "keeping up with the target", "no quedarse atrás",
                    $"A {size}° target on long strafes, sized to how far your crosshair trails. Match its speed instead of catching up.",
                    $"Un blanco de {size}° con desplazamientos largos, ajustado a cuánto se queda atrás tu mira. Iguala su velocidad en vez de alcanzarlo.")
                    with { Speed = 3.6f, TurnMin = 0.9f, TurnMax = 1.9f, Width = 7f };
            case "turn":
                return Build(AimFamily.Track, "turning with the target", "girar con el blanco",
                    $"A {size}° target that reverses often, sized to how far you run past each turn. Slow down, turn and speed up with it.",
                    $"Un blanco de {size}° que cambia de dirección a menudo, ajustado a cuánto te pasas en cada giro. Frena, gira y acelera con él.")
                    with { Speed = 4f, TurnMin = 0.3f, TurnMax = 1.0f };
            case "react":
            {
                float window = A(0, 50, 140, 90) / 100f;
                return Build(AimFamily.Reflex, "reacting in time", "reaccionar a tiempo",
                    $"Heads that stay {window.ToString("0.00", CultureInfo.InvariantCulture)} s, set from your measured reaction. Wait relaxed, do not guess, click once.",
                    $"Cabezas que duran {window.ToString("0.00", CultureInfo.InvariantCulture)} s, según tu reacción medida. Espera relajado, no adivines y pulsa una vez.")
                    with { Radius = 0.14f, CategoryOverride = "Reflex", TapOnly = true, Yaw = 8, MinAngle = 0, DelayMin = 0.9f, DelayMax = 3.4f, Window = window };
            }
            case "vert":
                return Build(AimFamily.Flick, "your vertical flicks", "tus flicks verticales",
                    $"Targets of {size}° above and below the crosshair: the direction you are slowest in.",
                    $"Blancos de {size}° por encima y por debajo de la mira: la dirección en la que eres más lento.")
                    with { TapOnly = true, Pattern = AimPattern.Vertical, PitchMin = -6, PitchMax = 20, MinAngle = 6 };
            case "horiz":
                return Build(AimFamily.Flick, "your horizontal flicks", "tus flicks horizontales",
                    $"Targets of {size}° along the head line, far to each side: train the side you are slowest on.",
                    $"Blancos de {size}° sobre la línea de cabezas, lejos a cada lado: entrena el lado en el que eres más lento.")
                    with { TapOnly = true, Pattern = AimPattern.Horizontal, Yaw = 45, MinAngle = 12 };
            default:
                return null;
        }
    }
}
