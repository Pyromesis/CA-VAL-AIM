namespace CaValAim.Core;

/// <summary>Where a glossary term is implemented inside CA-VAL-AIM: a drill, the coach, a settings page or a data screen.</summary>
public enum GlossaryTargetKind
{
    Drill,
    Coach,
    Settings,
    SensLab,
    Stats,
    Reactions,
    Rewind,
    Routines,
    Playlists,
    Agents,
    Guide,
    Data,
    LockIn,
}

/// <summary>
/// One functional destination of a glossary term. <see cref="Arg"/> is the drill key or routine id when the kind needs one.
/// The screen turns each target into a button, so a term is never just a definition: it points at the real feature,
/// drill or coach signal that implements it.
/// </summary>
public sealed record GlossaryTarget(GlossaryTargetKind Kind, string Arg, string TitleEs, string TitleEn,
    string? WhyEs = null, string? WhyEn = null);

/// <summary>
/// The bridge between <see cref="Glossary"/> and the app: every term that CA-VAL-AIM can actually train, measure or
/// configure is mapped to the place where it happens. Terms that describe hardware, genres or community slang and have
/// no in-app equivalent are listed as context in <see cref="ContextNote"/> instead of pretending to be a feature.
/// The map is data only (no Godot types), so it can be audited by <c>--coach-selftest</c>.
/// </summary>
public static class GlossaryActions
{
    static GlossaryTarget D(string key, string title, string? whyEs = null, string? whyEn = null) =>
        new(GlossaryTargetKind.Drill, key, title, title, whyEs, whyEn);
    static GlossaryTarget C(string? whyEs = null, string? whyEn = null) =>
        new(GlossaryTargetKind.Coach, "", "Diagnóstico", "Diagnosis", whyEs, whyEn);
    static GlossaryTarget Set(string? whyEs = null, string? whyEn = null) =>
        new(GlossaryTargetKind.Settings, "", "", "", whyEs, whyEn);
    static GlossaryTarget Sens() => new(GlossaryTargetKind.SensLab, "", "cm/360 y sensibilidad", "cm/360 and sensitivity",
        "Calcula tus cm/360 y compara tu sensibilidad con tus propios datos.", "Computes your cm/360 and compares sensitivity with your own runs.");
    static GlossaryTarget Stats(string? whyEs = null, string? whyEn = null) =>
        new(GlossaryTargetKind.Stats, "", "por ejercicio", "per drill", whyEs, whyEn);
    static GlossaryTarget Reactions() => new(GlossaryTargetKind.Reactions, "", "por categoría", "by category",
        "Tus tiempos por categoría: visual, auditiva, periférica, elección y flick.", "Your times by category: visual, audio, peripheral, choice and flick.");
    static GlossaryTarget Rewind(string? whyEs = null, string? whyEn = null) =>
        new(GlossaryTargetKind.Rewind, "", "última run", "last run", whyEs, whyEn);
    static GlossaryTarget Routines(string id = "", string? whyEs = null, string? whyEn = null) =>
        new(GlossaryTargetKind.Routines, id, "", "", whyEs, whyEn);
    static GlossaryTarget Playlists() => new(GlossaryTargetKind.Playlists, "", "", "",
        "Tus playlists guardadas y las que crea el coach.", "Your saved playlists and the ones the coach builds.");
    static GlossaryTarget Agents() => new(GlossaryTargetKind.Agents, "", "", "",
        "Habilidades y utilidad entrenadas con cada agente.", "Abilities and utility trained with each agent.");
    static GlossaryTarget Guide(string? whyEs = null, string? whyEn = null) =>
        new(GlossaryTargetKind.Guide, "", "", "", whyEs, whyEn);
    static GlossaryTarget Data() => new(GlossaryTargetKind.Data, "", "", "",
        "Exporta rutinas, pasos, glosario y progreso en tablas locales.", "Export routines, steps, glossary and progress as local tables.");
    static GlossaryTarget LockIn() => new(GlossaryTargetKind.LockIn, "", "", "",
        "Calibración de nivel que adapta el ejercicio a tu porcentaje de acierto.", "Level calibration that adapts the drill to your hit rate.");

    /// <summary>Terms with a real destination in the app, keyed by the exact glossary name.</summary>
    static readonly Dictionary<string, GlossaryTarget[]> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Accuracy"] = new[] { D("gridshot", "Gridshot"), C("El coach usa la precisión por run y por segundo en el diagnóstico.", "The coach uses per-run and per-second accuracy in the diagnosis."), Stats() },
        ["ADS"] = new[] { D("operator", "Operator") },
        ["Aim"] = new[] { C(), Routines() },
        ["Aim Theory"] = new[] { Guide("La guía explica el método y el orden de las rutinas.", "The guide explains the method and the order of the routines."), C() },
        ["Aim Training"] = new[] { Routines(), Data() },
        ["AIO"] = new[] { D("deathmatch", "Deathmatch") },
        ["Angelic"] = new[] { D("flickx:sc-cartoon-dev-ts-static-angelic", "devTS Static Click Angelic"), D("flickx:sc-cartoon-float-ts-angelic-easy", "FloatTS Angelic Easy") },
        ["Angle"] = new[] { D("siteclear", "Site Clear"), C("La colocación de mira se mide en cada avistamiento.", "Crosshair placement is measured at every sighting.") },
        ["Anti-Mirroring"] = new[] { D("anti-mirror", "Anti-Mirror", "El ejercicio puntúa los frames en los que te mueves contra el strafe del objetivo.", "The drill scores the frames you move against the target's strafe."), C("Detecta espejo y anti-espejo con la posición del objetivo y tu A/D en cada frame.", "Detects mirroring and anti-mirroring from the target position and your A/D every frame."), Stats() },
        ["Arc"] = new[] { D("airtrack", "Air Tracking") },
        ["Bardpill"] = new[] { Routines("bardoz-advanced"), D("microshot", "Microshot") },
        ["Benchmark"] = new[] { C("Compara tus métricas con las bandas de referencia por rango.", "Compares your metrics with the rank reference bands."), Stats() },
        ["Bot"] = new[] { D("deathmatch", "Deathmatch"), Agents() },
        ["Click-Timing"] = new[] { D("flickx:drift", "Drifting Flicks"), D("reflex:mixed", "Mixed Reflex") },
        ["Clicking"] = new[] { D("gridshot", "Gridshot"), D("flick", "Flick") },
        ["CM/360°"] = new[] { Sens(), Set() },
        ["Coach"] = new[] { C() },
        ["Counter-Strafe"] = new[] { D("counterstrafe", "Counter-Strafe"), C("Mide el tiempo de parada y si usaste la tecla contraria.", "Measures stop time and whether you used the opposite key.") },
        ["CPI"] = new[] { Sens(), Set() },
        ["Crosshair"] = new[] { D("xhairfinder", "Crosshair Finder"), Set() },
        ["Crosshair Placement"] = new[] { D("siteclear", "Site Clear"), D("peek", "Peek"), C() },
        ["Deliberate Practice"] = new[] { Routines(), C() },
        ["Dodge"] = new[] { D("strafebots", "Strafe Bots"), D("anti-mirror", "Anti-Mirror") },
        ["Dodge Profile"] = new[] { D("strafebots", "Strafe Bots"), D("deathmatch", "Deathmatch") },
        ["Dot"] = new[] { D("xhairfinder", "Crosshair Finder") },
        ["DPI"] = new[] { Sens(), Set() },
        ["Drop"] = new[] { D("trackv:jumper", "Jumper Track") },
        ["Dynamic Clicking"] = new[] { D("flickx:drift", "Drifting Flicks"), D("reflex:mixed", "Mixed Reflex"), D("popup", "Popup") },
        ["Edge Tracking"] = new[] { D("tracking", "Strafe Tracking"), C("El lag y el underaim se miden contra el centro del objetivo.", "Lag and underaim are measured against the target's centre."), Rewind("Momentos TRACK_BEHIND y TRACK_OVERRUN.", "TRACK_BEHIND and TRACK_OVERRUN moments.") },
        ["eDPI"] = new[] { Sens(), Set() },
        ["Evasive Switching"] = new[] { D("switch", "Target Switch"), D("strafebots", "Strafe Bots") },
        ["Flicking"] = new[] { D("flick", "Flick"), D("flickx:mid", "Mid-range Flicks"), C() },
        ["Gamesense"] = new[] { D("siteclear", "Site Clear"), D("postplant", "Post-plant") },
        ["Gauntlet"] = new[] { D("deathmatch", "Deathmatch"), D("arsenal", "Arsenal") },
        ["Goated"] = new[] { D("microx:sc-cartoon-1w3ts-pasu-micro-goated", "1w3ts Pasu Perfected Micro Goated"), Routines("cartoon-micro") },
        ["Hipfire"] = new[] { D("gridshot", "Gridshot"), D("flick", "Flick") },
        ["Hit Confirming"] = new[] { D("microx:tiny", "Micro Steps"), C("El coach marca los disparos precipitados y las correcciones que faltan.", "The coach flags premature shots and missing corrections.") },
        ["Hitscan"] = new[] { D("flick", "Flick"), D("spray_vandal", "Vandal Spray") },
        ["Invincible"] = new[] { D("tracking", "Strafe Tracking"), D("airtrack", "Air Tracking") },
        ["Jitter-Clicking"] = new[] { C("Los momentos de temblor y las correcciones nerviosas salen del timeline.", "Tremor moments and jittery corrections come from the timeline."), Rewind() },
        ["Leaderboard"] = new[] { Stats("Récords locales por ejercicio y nivel; no hay leaderboard online.", "Local records per drill and tier; there is no online leaderboard.") },
        ["Map"] = new[] { Routines(), D("siteclear", "Site Clear") },
        ["Mechanical Skill"] = new[] { C(), Stats() },
        ["Micro-Flick"] = new[] { D("microx:small", "Small Adjusts"), D("flickx:tiny", "Tiny Flicks") },
        ["Mirroring"] = new[] { D("anti-mirror", "Anti-Mirror"), C("El hallazgo MIRRORING explica cuántos frames te moviste con el objetivo.", "The MIRRORING finding explains how many frames you moved with the target.") },
        ["Mouse Control"] = new[] { Sens(), D("tracking", "Strafe Tracking") },
        ["Micro-correction"] = new[] { D("microx:pinpoint", "Pinpoint"), C("El tiempo de asentamiento y las correcciones se miden por objetivo.", "Settle time and corrections are measured per target.") },
        ["NUNS"] = new[] { D("airtrack", "Air Tracking") },
        ["Over-Flicking"] = new[] { D("flick", "Flick"), C("El diagnóstico OVERFLICK mide cuánto te pasas.", "The OVERFLICK diagnosis measures how far past the target you went."), Rewind("Momentos OVERFLICK.", "OVERFLICK moments.") },
        ["PGTI"] = new[] { D("trackv:surge", "Surge Track"), D("tracking", "Strafe Tracking") },
        ["Playlist"] = new[] { Playlists(), Routines() },
        ["Plaza"] = new[] { D("deathmatch", "Deathmatch") },
        ["Plus"] = new[] { D("xhairfinder", "Crosshair Finder") },
        ["Precise"] = new[] { D("trackv:slow", "Slow Strafe Track"), C() },
        ["Precision"] = new[] { C(), Stats() },
        ["Prediction"] = new[] { D("reflex:patient", "Patient Reflex"), C("Los pops con pausas largas castigan la anticipación.", "Long, random pauses punish anticipation.") },
        ["Rank"] = new[] { C("El perfil estima tu rango por habilidad y su confianza.", "The profile estimates your skill rank and its confidence."), Stats() },
        ["Reactive Tracking"] = new[] { D("reacttrack", "Reactive Tracking"), D("tracking", "Strafe Tracking") },
        ["Reactivity"] = new[] { D("reactlab:visual", "Reaction Lab"), D("reacttrack", "Reactive Tracking") },
        ["Reading"] = new[] { D("reacttrack", "Reactive Tracking"), D("flickx:drift", "Drifting Flicks") },
        ["Reload"] = new[] { D("spray_vandal", "Vandal Spray"), Set("Modo de munición: real o infinita.", "Ammo mode: real or infinite.") },
        ["Recoil"] = new[] { D("spray_vandal", "Vandal Spray"), D("spray_transfer", "Spray Transfer"), Set("El retroceso se puede activar o desactivar en pausa.", "Recoil can be switched on or off in the pause menu.") },
        ["Reticle"] = new[] { D("operator", "Operator"), D("xhairfinder", "Crosshair Finder") },
        ["RNG"] = new[] { D("reflex:mixed", "Mixed Reflex"), D("deathmatch", "Deathmatch") },
        ["RoF"] = new[] { D("arsenal", "Arsenal"), D("spray_vandal", "Vandal Spray") },
        ["Routine"] = new[] { Routines() },
        ["Run"] = new[] { Stats(), Rewind() },
        ["Scenario"] = new[] { Routines(), Playlists() },
        ["Sensitivity"] = new[] { Sens(), Set() },
        ["Share Code"] = new[] { Routines("cartoon-flicks", "Los códigos de KovaaK's se conservan en cada rutina.", "The KovaaK's codes are kept in each routine.") },
        ["Skybot"] = new[] { D("airtrack", "Air Tracking") },
        ["Smoothness"] = new[] { D("trackv:slow", "Slow Strafe Track"), C("El temblor y el jitter se miden en cada run.", "Tremor and jitter are measured in every run.") },
        ["Speed"] = new[] { D("flickx:blitz", "Blitz Flicks"), Stats() },
        ["Sphere"] = new[] { D("gridshot", "Gridshot") },
        ["Spread"] = new[] { D("spray_vandal", "Vandal Spray"), D("arsenal", "Arsenal") },
        ["Strafe Aiming"] = new[] { D("anti-mirror", "Anti-Mirror"), D("strafebots", "Strafe Bots") },
        ["Strafe Scenarios"] = new[] { D("strafebots", "Strafe Bots"), D("counterstrafe", "Counter-Strafe") },
        ["Strafing"] = new[] { D("anti-mirror", "Anti-Mirror"), D("counterstrafe", "Counter-Strafe") },
        ["Static Clicking"] = new[] { D("gridshot", "Gridshot"), D("flickx:sc-cartoon-dev-ts-static-angelic", "devTS Static Click Angelic") },
        ["Switching"] = new[] { D("switch", "Target Switch"), D("flickx:sc-cartoon-1w2ts-perfected", "1w2ts Perfected") },
        ["Tactical Shooter"] = new[] { D("siteclear", "Site Clear"), D("postplant", "Post-plant") },
        ["Task"] = new[] { Routines(), Playlists() },
        ["TE"] = new[] { D("flickx:big", "Big Flicks"), D("reflex:big", "Big Reflex") },
        ["Tensing"] = new[] { C("El temblor y la falta de suavidad aparecen como hallazgos del coach.", "Tremor and lack of smoothness show up as coach findings."), Sens() },
        ["TI"] = new[] { D("tracking", "Strafe Tracking"), D("airtrack", "Air Tracking") },
        ["Tracking"] = new[] { D("tracking", "Strafe Tracking"), D("trackv:slow", "Slow Strafe Track"), C() },
        ["Trailing"] = new[] { D("tracking", "Strafe Tracking"), C("El lag de tracking mide cuánto vas por detrás.", "Tracking lag measures how far behind you are."), Rewind("Momentos TRACK_BEHIND.", "TRACK_BEHIND moments.") },
        ["Trajectory"] = new[] { D("airtrack", "Air Tracking") },
        ["TS"] = new[] { D("switch", "Target Switch") },
        ["TTK"] = new[] { D("deathmatch", "Deathmatch"), D("reflex:mixed", "Mixed Reflex") },
        ["Underaiming"] = new[] { D("tracking", "Strafe Tracking"), C("El underaim y el lag se miden contra el centro.", "Underaim and lag are measured against the centre.") },
        ["Under-flicking"] = new[] { D("flickx:mid", "Mid-range Flicks"), C("El diagnóstico UNDERFLICK mide cuánto te quedas corto.", "The UNDERFLICK diagnosis measures how short you stop."), Rewind("Momentos UNDERFLICK.", "UNDERFLICK moments.") },
        ["Utility"] = new[] { Agents(), D("flashmap", "Flash Dodge"), D("smokeexec", "Smoke Execute") },
        ["Verticality"] = new[] { D("flickx:vertical", "Vertical Flicks"), D("reflex:vertical", "Vertical Reflex") },
        ["VRT"] = new[] { Reactions(), D("reactlab:visual", "Reaction Lab") },
        ["VoD"] = new[] { Rewind("Reproduce la última run con la telemetría completa.", "Replays the last run with the full telemetry.") },
        ["VSS"] = new[] { D("trackv:surge", "Surge Track"), D("trackv:stopgo", "Stop-and-go Track") },
        ["Wide Angle"] = new[] { D("flickx:wide", "Wide Flicks"), D("flickx:sc-cartoon-pokeball-frenzy-wide-4-small", "Pokeball Frenzy Wide Wall 4 Targets Small") },
        ["WW"] = new[] { D("flickx:wide", "Wide Flicks"), D("flickx:sc-cartoon-pokeball-frenzy-wide-4-small", "Pokeball Frenzy Wide Wall 4 Targets Small") },
    };

    /// <summary>
    /// Terms that are context only: hardware, genres, community slang or scenario types this trainer does not reproduce.
    /// The note says why, instead of offering a button that leads nowhere.
    /// </summary>
    static readonly Dictionary<string, (string Es, string En)> Context = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acc-Based Scoring"] = ("Fórmula de puntuación antigua. CA-VAL-AIM guarda impacto y precisión por run, no esa fórmula.", "An old scoring formula. CA-VAL-AIM stores hits and accuracy per run, not that formula."),
        ["Acceleration"] = ("Aceleración del ratón: se configura en el software del ratón, no en el juego.", "Mouse acceleration: configured in the mouse software, not in the game."),
        ["Arena FPS"] = ("Género (Quake, Diabotical); el entrenamiento de CA-VAL-AIM es para VALORANT.", "A genre (Quake, Diabotical); CA-VAL-AIM trains for VALORANT."),
        ["Blink"] = ("Teletransporte del objetivo: los objetivos de CA-VAL-AIM no se teletransportan.", "Target teleport: CA-VAL-AIM targets do not teleport."),
        ["Bounce"] = ("Objetivos que botan: aquí los objetivos se mueven en línea o se elevan, no rebotan.", "Bouncing targets: here targets strafe or rise, they do not bounce."),
        ["Cheesing"] = ("Ajustes exagerados para puntuar más: una advertencia, no una función del entrenador.", "Absurd settings just to score higher: a warning, not a trainer feature."),
        ["Claw Grip"] = ("Forma de agarrar el ratón: depende de tu mano y tu hardware.", "How you hold the mouse: it depends on your hand and hardware."),
        ["CPS"] = ("Clics por segundo: CA-VAL-AIM mide precisión y tiempo de impacto, no CPS.", "Clicks per second: CA-VAL-AIM measures accuracy and time to hit, not CPS."),
        ["Cylinder"] = ("Objetivo cilíndrico: los objetivos de CA-VAL-AIM son esferas.", "Cylindrical target: CA-VAL-AIM targets are spheres."),
        ["Fingertip Grip"] = ("Forma de agarrar el ratón: depende de tu mano y tu hardware.", "How you hold the mouse: it depends on your hand and hardware."),
        ["FOV"] = ("Campo de visión: CA-VAL-AIM usa el FOV de VALORANT y no lo expone como ajuste.", "Field of view: CA-VAL-AIM uses VALORANT's FOV and does not expose it as a setting."),
        ["FPS"] = ("Género de videojuegos; no es una mecánica que se entrene por separado.", "A video-game genre; not a mechanic trained on its own."),
        ["Leading"] = ("Adelantar un proyectil: las armas de CA-VAL-AIM son hitscan.", "Leading a projectile: CA-VAL-AIM weapons are hitscan."),
        ["Metronome"] = ("Ritmo de clic externo: la app no reproduce un metrónomo.", "External click tempo: the app does not play a metronome."),
        ["Mouse Grip"] = ("Forma de agarrar el ratón: depende de tu mano y tu hardware.", "How you hold the mouse: it depends on your hand and hardware."),
        ["Nevermiss"] = ("Escenario que termina al fallar: CA-VAL-AIM no tiene modo de fallo automático.", "A scenario that ends on a miss: CA-VAL-AIM has no auto-fail mode."),
        ["OTP"] = ("Jugar una sola cosa: es un hábito del jugador, no una función.", "Playing only one thing: a player habit, not a feature."),
        ["Palm Grip"] = ("Forma de agarrar el ratón: depende de tu mano y tu hardware.", "How you hold the mouse: it depends on your hand and hardware."),
        ["Pill"] = ("Objetivo en forma de píldora: los objetivos de CA-VAL-AIM son esferas.", "Pill-shaped target: CA-VAL-AIM targets are spheres."),
        ["Projectile"] = ("Munición con tiempo de viaje: las armas de CA-VAL-AIM son hitscan.", "Ammo with travel time: CA-VAL-AIM weapons are hitscan."),
        ["Reset"] = ("Reiniciar una run: se hace desde el menú de pausa, no es un término entrenable.", "Restarting a run: done from the pause menu; not a trainable term."),
        ["Shooter"] = ("Género de videojuegos; no es una mecánica que se entrene por separado.", "A video-game genre; not a mechanic trained on its own."),
        ["SQRT Accuracy"] = ("Fórmula de puntuación alternativa. CA-VAL-AIM usa su propia puntuación por ejercicio.", "An alternative scoring formula. CA-VAL-AIM uses its own per-drill score."),
        ["Third-Person Shooter"] = ("Género con cámara en tercera persona; CA-VAL-AIM es primera persona.", "A third-person genre; CA-VAL-AIM is first person."),
        ["Timescale"] = ("Acelerar o ralentizar el escenario: la app corre a velocidad real.", "Speeding up or slowing the scenario: the app runs at real speed."),
        ["Travel"] = ("Retardo del proyectil: las armas de CA-VAL-AIM son hitscan.", "Projectile delay: CA-VAL-AIM weapons are hitscan."),
        ["UFO"] = ("Teletransportes del objetivo: los objetivos de CA-VAL-AIM no se teletransportan.", "Target teleports: CA-VAL-AIM targets do not teleport."),
    };

    public static IReadOnlyList<GlossaryTarget> For(string term) =>
        Map.TryGetValue(term, out var list) ? list : Array.Empty<GlossaryTarget>();

    public static bool Applicable(string term) => Map.ContainsKey(term);

    public static bool IsContext(string term) => Context.ContainsKey(term);

    public static (string Es, string En) ContextNote(string term) =>
        Context.TryGetValue(term, out var note) ? note : ("", "");

    /// <summary>Every term name the map and the context notes know (the self-test checks them against the glossary).</summary>
    public static IReadOnlyCollection<string> MappedTerms => Map.Keys;

    public static IReadOnlyCollection<string> ContextTerms => Context.Keys;

    /// <summary>Terms with at least one functional destination (shown as coverage on the glossary screen).</summary>
    public static int ImplementedCount => Map.Count;

    /// <summary>Terms that are context only, with an explanation instead of a dead button.</summary>
    public static int ContextCount => Context.Count;
}
