using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

namespace ValTrainer.Core;

/// <summary>
/// Tiny built-in English → Spanish layer. ValTrainer has no translation files, so the UI calls
/// <see cref="T"/> at draw time (Gfx / HUD) and on the few Godot labels it creates. Exact entries cover
/// the menu, settings, pause, HUD, drill names and descriptions; a small phrase table keeps dynamic
/// coach / result text readable when it contains numbers or names.
/// </summary>
public static class Loc
{
    public static string Language { get; private set; } = "en";
    /// <summary>Bumped whenever the language changes: screens rebuild themselves on the next frame.</summary>
    public static int Revision { get; private set; }
    public static bool Spanish => Language == "es";

    static readonly CultureInfo SpanishNumbers = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Grouped integer in the selected language (1.423 in Spanish, 1,423 in English).</summary>
    public static string Score(long n) => n.ToString("N0", Spanish ? SpanishNumbers : CultureInfo.InvariantCulture);

    /// <summary>Signed grouped delta ("+1.423" / "-250") in the selected language.</summary>
    public static string Delta(long n) => n.ToString("+#,0;-#,0;0", Spanish ? SpanishNumbers : CultureInfo.InvariantCulture);

    /// <summary>Result values keep their layout, but Spanish uses a decimal comma ("80.0%" → "80,0%").</summary>
    public static string Numeric(string s)
    {
        if (!Spanish) return s;
        var a = s.ToCharArray();
        for (int i = 1; i + 1 < a.Length; i++)
            if (a[i] == '.' && char.IsDigit(a[i - 1]) && char.IsDigit(a[i + 1])) a[i] = ',';
        return new string(a);
    }

    static readonly string[] MonthsEs = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

    /// <summary>Three-letter month in the selected language ("Oct" / "oct"). Dates keep the app's invariant layout.</summary>
    public static string MonthShort(DateTime d) => Spanish ? MonthsEs[d.Month - 1] : d.ToString("MMM", CultureInfo.InvariantCulture);

    public static string Normalize(string? lang) => string.Equals(lang, "es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";

    public static void SetLanguage(string? lang)
    {
        string next = Normalize(lang);
        if (next == Language) return;
        Language = next;
        Revision++;
        cache.Clear();
        translated.Clear();
    }

    /// <summary>
    /// Translates <paramref name="text"/> when Spanish is selected. It is idempotent: a value that this
    /// method already produced is returned untouched, so drawing and measuring the same string is safe.
    /// </summary>
    public static string T(string? text)
    {
        if (!Spanish || string.IsNullOrEmpty(text)) return text ?? "";
        if (translated.Contains(text)) return text;
        if (Overrides.TryGetValue(text, out var over))
        {
            over = MatchCase(text, over);
            translated.Add(over);
            return over;
        }
        if (Exact.TryGetValue(text, out var exact))
        {
            exact = MatchCase(text, exact);
            translated.Add(exact);
            return exact;
        }
        if (SpanishValues.Contains(text) || Keep.Contains(text)) return text;
        if (cache.TryGetValue(text, out var hit)) return hit;
        string result = Fallback(text);
        if (result != text) translated.Add(result);
        cache[text] = result;
        return result;
    }

    /// <summary>Convenience for count labels ("1 drill", "3 drills").</summary>
    public static string Count(int n, string one, string many) => T($"{n} {(n == 1 ? one : many)}");

    /// <summary>Translates labels and tooltips of a freshly built screen. Custom-drawn text goes through Gfx.</summary>
    public static void Apply(Node root)
    {
        if (!Spanish) return;
        foreach (Node child in root.GetChildren())
        {
            if (child is Control c)
            {
                if (!string.IsNullOrEmpty(c.TooltipText)) c.TooltipText = T(c.TooltipText);
                switch (c)
                {
                    case Label l when !string.IsNullOrEmpty(l.Text): l.Text = T(l.Text); break;
                    case LineEdit e when !string.IsNullOrEmpty(e.PlaceholderText): e.PlaceholderText = T(e.PlaceholderText); break;
                    case Button b when !string.IsNullOrEmpty(b.Text): b.Text = T(b.Text); break;
                }
            }
            Apply(child);
        }
    }

    static string MatchCase(string source, string value)
    {
        bool sourceUpper = source.Any(char.IsLetter) && source.All(c => !char.IsLetter(c) || char.IsUpper(c));
        bool valueUpper = value.Any(char.IsLetter) && value.All(c => !char.IsLetter(c) || char.IsUpper(c));
        if (sourceUpper) return value.ToUpperInvariant(); // "PAUSED" → "PAUSA", even if the table stores sentence case
        if (!valueUpper) return value;
        // Stored upper-case label ("EJERCICIOS") used in a mixed-case sentence: make it "Ejercicios".
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }

    static string Fallback(string s)
    {
        // Some screens build their Spanish text themselves (coach sentences, Sens Lab values). Do not
        // run the English phrase table over it: words like "en" or "con" also exist as English keys and
        // would otherwise be swapped inside an already-Spanish sentence.
        if (LooksSpanish(s)) return s;
        var verbatim = new List<string>();
        string work = ProtectVerbatim(s, verbatim);
        if (!FullyCovered(work)) return s; // half-English is worse than English: exact entries handle these
        string r = PhraseRegex.Replace(work, m =>
        {
            string key = m.Value;
            if (!Phrases.TryGetValue(key, out var val)) return key;
            if (key.All(char.IsUpper) && val.Any(char.IsLetter)) return val.ToUpperInvariant();
            if (char.IsUpper(key[0]) && key.Skip(1).All(c => !char.IsLetter(c) || char.IsLower(c))) return char.ToUpperInvariant(val[0]) + val[1..];
            return val;
        });
        return UnprotectVerbatim(r, verbatim);
    }

    /// <summary>
    /// True when every word of <paramref name="s"/> is part of a known phrase: the word-by-word fallback must never
    /// leave "risky swing" or "locked en" in the middle of a sentence. Words that no entry covers abort the fallback
    /// (the caller then keeps the English text until an exact translation exists).
    /// </summary>
    static bool FullyCovered(string s)
    {
        var covered = new bool[s.Length];
        foreach (Match m in PhraseRegex.Matches(s))
            for (int i = m.Index; i < m.Index + m.Length; i++) covered[i] = true;
        foreach (Match w in WordRegex.Matches(s))
        {
            if (w.Length < 2 || Keep.Contains(w.Value)) continue;
            for (int i = w.Index; i < w.Index + w.Length; i++)
                if (!covered[i]) return false;
        }
        return true;
    }

    // "CA-VAL-AIM 2.2.0 IS AVAILABLE" must not become "CA-VAL-PUNTERÍA …" ("aim" is a translated word): brand
    // names and hyphenated product terms are swapped out for tokens before the phrase pass and put back afterwards.
    static readonly string[] Verbatim = { "LOCK-INS", "LOCK-IN", "CA-VAL-AIM", "ValTraining", "ValTrainer" };

    static string ProtectVerbatim(string s, List<string> kept)
    {
        foreach (var brand in Verbatim)
        {
            int from = 0;
            while (true)
            {
                int at = s.IndexOf(brand, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0) break;
                string token = "\u0001" + kept.Count.ToString(CultureInfo.InvariantCulture) + "\u0002";
                kept.Add(s.Substring(at, brand.Length));
                s = s[..at] + token + s[(at + brand.Length)..];
                from = at + token.Length;
            }
        }
        return s;
    }

    static string UnprotectVerbatim(string s, List<string> kept) => kept.Count == 0
        ? s
        : Regex.Replace(s, "\u0001(\\d+)\u0002", m => kept[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)]);

    static bool LooksSpanish(string s)
    {
        if (s.Any(c => "áéíóúüñ¿¡".Contains(char.ToLowerInvariant(c)))) return true;
        foreach (Match m in WordRegex.Matches(s))
            if (SpanishWords.Contains(m.Value)) return true;
        return false;
    }

    static readonly HashSet<string> SpanishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "el", "la", "los", "las", "un", "una", "unos", "unas", "de", "del", "al", "que", "en", "y",
        "con", "por", "para", "es", "son", "se", "su", "sus", "lo", "le", "les", "más", "pero", "como",
        "este", "esta", "esto", "estos", "estas", "entre", "cuando", "muy", "sin", "sobre", "también",
        "hasta", "hay", "donde", "desde", "todo", "todos", "toda", "todas", "nos", "ni", "contra", "otro",
        "otra", "otros", "otras", "ese", "esa", "eso", "esos", "esas", "porque", "qué", "solo", "sólo",
        "han", "yo", "vez", "puede", "así", "parte", "tiene", "él", "bien", "tiempo", "mismo", "ahora",
        "cada", "vida", "después", "te", "aunque", "hace", "tan", "siempre", "día", "tanto", "ella",
        "tres", "sí", "dijo", "sido", "gran", "país", "según", "menos", "mundo", "año", "antes", "estado",
        "cinco", "nada", "hacer", "algo", "ser", "ver", "dar", "ir", "fin", "red", "pie", "mar", "mes",
    };

    static readonly Regex WordRegex = new(@"[\p{L}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly Dictionary<string, string> cache = new(StringComparer.Ordinal);
    static readonly HashSet<string> translated = new(StringComparer.Ordinal);

    static readonly HashSet<string> Keep = new(StringComparer.OrdinalIgnoreCase)
    {
        "VAL", "TRAINER", "VALTRAINER", "VALORANT", "VANDAL", "PHANTOM", "SHERIFF", "OPERATOR",
        "JETT", "SOVA", "OMEN", "VIPER", "KILLJOY", "CYPHER", "SAGE", "PHOENIX", "REYNA", "RAZE",
        "BREACH", "SKYE", "YORU", "ASTRA", "KAY/O", "CHAMBER", "NEON", "FADE", "HARBOR", "GEKKO",
        "DEADLOCK", "ISO", "CLOVE", "VYSE", "TEJO", "WAYLAY", "ASCENT", "BIND", "HAVEN", "SPLIT",
        "LOTUS", "SUNSET", "ICEBOX", "BREEZE", "FRACTURE", "PEARL", "RANDOM", "E", "Q", "C", "X",
    };

    /// <summary>Dictionary initializer that silently keeps the first translation when two entries share a key.</summary>
    sealed class SafeDictionary : Dictionary<string, string>
    {
        public SafeDictionary() : base(StringComparer.OrdinalIgnoreCase) { }
        public new void Add(string key, string value) => TryAdd(key, value);
    }

    static readonly SafeDictionary Exact = new()
    {
        // ---- shell / menu ----
        ["LOCK IN"] = "LOCK-IN",
        ["AGENTS"] = "AGENTES",
        ["COACH"] = "COACH",
        ["SETTINGS"] = "AJUSTES",
        ["STATS"] = "ESTADÍSTICAS",
        ["QUIT"] = "SALIR",
        ["AIM · MOVEMENT · UTILITY — YOUR OWN VALORANT SETTINGS"] = "PUNTERÍA · MOVIMIENTO · UTILIDAD — TUS AJUSTES DE VALORANT",
        ["TRAINING"] = "ENTRENAMIENTO",
        ["CATEGORIES"] = "CATEGORÍAS",
        ["DRILLS"] = "EJERCICIOS",
        ["DRILL"] = "EJERCICIO",
        ["ALL"] = "TODOS",
        ["Aim"] = "Puntería",
        ["Valorant"] = "Valorant",
        ["Recoil"] = "Retroceso",
        ["Movement"] = "Movimiento",
        ["Map"] = "Mapa",
        ["Utility"] = "Utilidad",
        ["Coach"] = "Coach",
        ["Other"] = "Extra",
        ["Agents"] = "Agentes",
        ["DIFFICULTY TIER"] = "NIVEL DE DIFICULTAD",
        ["DIFFICULTY"] = "DIFICULTAD",
        ["Bots, target sizes and rank benchmarks scale with the tier"] = "Los bots, el tamaño de los objetivos y los rangos se adaptan al nivel",
        ["bots and targets scale with it"] = "los bots y objetivos se adaptan al nivel",
        ["YOUR SETUP"] = "TU CONFIGURACIÓN",
        ["auto-imported"] = "auto-importado",
        ["defaults"] = "por defecto",
        ["CROSSHAIR"] = "MIRA",
        ["real pixel size"] = "tamaño real en píxeles",
        ["COPY CROSSHAIR CODE"] = "COPIAR CÓDIGO DE MIRA",
        ["COPIED ✓"] = "COPIADO ✓",
        ["Sensitivity"] = "Sensibilidad",
        ["eDPI / cm per 360"] = "eDPI / cm por 360°",
        ["Display"] = "Pantalla",
        ["Enemy highlight"] = "Color de enemigos",
        ["Crosshair profile"] = "Perfil de mira",
        ["Valorant data"] = "Datos de Valorant",
        ["Not found — check Settings"] = "No encontrado — revisa Ajustes",
        ["CUSTOM"] = "PERSONALIZADA",
        ["OVERRIDE"] = "CAMBIADO",
        ["PB"] = "RÉCORD",
        ["NEW"] = "NUEVO",
        ["COMMUNITY"] = "COMUNIDAD",
        ["SIGNATURE"] = "FIRMA",
        ["PRACTICE SETUP"] = "CONFIGURACIÓN DE PRÁCTICA",
        ["applies to the next drill"] = "se aplica al siguiente drill",
        ["PRIMARY WEAPON"] = "ARMA PRINCIPAL",
        ["WEAPON"] = "ARMA",
        ["AMMO"] = "MUNICIÓN",
        ["AMMO IN DRILLS"] = "BALAS EN LOS DRILLS",
        ["DRILL DEFAULT"] = "SEGÚN DRILL",
        ["INFINITE"] = "INFINITAS",
        ["REAL"] = "REALES",
        ["LANGUAGE"] = "IDIOMA",
        ["SPANISH"] = "ESPAÑOL",
        ["ENGLISH"] = "INGLÉS",
        ["VANDAL"] = "VANDAL",
        ["PHANTOM"] = "PHANTOM",
        ["Fan-made trainer · not endorsed by or affiliated with Riot Games"] = "Entrenador fan-made · no afiliado ni respaldado por Riot Games",
        ["SUPPORT"] = "APOYAR",
        ["REPORT A BUG"] = "REPORTAR UN FALLO",
        ["SUGGEST A FEATURE"] = "SUGERIR UNA IDEA",
        ["DISCORD"] = "DISCORD",
        ["Open the aim coach"] = "Abrir el coach de puntería",
        ["Fix first:"] = "Corrige primero:",
        ["COACH FOCUS"] = "FOCO DEL COACH",
        ["PREVIEW · SIMULATED PLAYER"] = "VISTA PREVIA · JUGADOR SIMULADO",
        ["NO PREVIEW FOR THIS ONE"] = "SIN VISTA PREVIA PARA ESTE DRILL",
        ["It runs on your own picks and reactions. Press START to try it."] = "Depende de tus decisiones y reacciones. Pulsa EMPEZAR para probarlo.",
        ["Space / Enter starts, Esc goes back."] = "Espacio / Enter empieza, Esc vuelve.",
        ["SPACE / ENTER STARTS, ESC GOES BACK."] = "ESPACIO / ENTER EMPIEZA, ESC VUELVE.",
        ["START"] = "EMPEZAR",
        ["BACK"] = "VOLVER",
        ["FREE PLAY"] = "JUEGO LIBRE",
        ["Length"] = "Duración",
        ["Weapon"] = "Arma",
        ["Where"] = "Dónde",
        ["Trains"] = "Entrena",
        ["YOUR RECORD"] = "TU RÉCORD",
        ["Until you finish it"] = "Hasta que lo termines",
        ["On screen"] = "En pantalla",
        ["Shooting range"] = "Campo de tiro",
        ["RANDOM"] = "ALEATORIO",
        ["Made by the community:"] = "Hecho por la comunidad:",
        ["Map for the map, utility and agent drills · click to see every map"] = "Mapa para los drills de mapa, utilidad y agentes · pulsa para ver todos",
        ["Previous map"] = "Mapa anterior",
        ["Next map"] = "Mapa siguiente",
        ["Map drills pick a random map each run"] = "Los drills de mapa eligen un mapa distinto en cada partida",
        ["Map drills use"] = "Los drills de mapa usan",
        ["MAP"] = "MAPA",
        ["Search agents, abilities or utility"] = "Buscar agentes, habilidades o utilidad",
        ["Drills run at the tier picked on the menu"] = "Los drills usan el nivel elegido en el menú",
        ["HOW IT WORKS"] = "CÓMO FUNCIONA",
        ["everything runs at your tier and map"] = "todo usa tu nivel y tu mapa",
        ["ABILITIES"] = "HABILIDADES",
        ["what each one does · the drills that train it"] = "qué hace cada una · los drills que la entrenan",
        ["SIGNATURE"] = "FIRMA",
        ["ROLE"] = "ROL",
        ["set on the menu"] = "se elige en el menú",
        ["No agent or ability matches"] = "Ningún agente o habilidad coincide con",
        ["Try a name (Sova), an ability (Updraft) or what it does (smoke, flash, wall)."] = "Prueba un nombre (Sova), una habilidad (Updraft) o lo que hace (humo, flash, muro).",
        ["Check-in first, your goal and cue at the end."] = "Primero el check-in; al final, tu objetivo y tu señal.",

        // ---- pause ----
        ["PAUSED"] = "PAUSA",
        ["RESUME"] = "CONTINUAR",
        ["RESTART"] = "REINICIAR",
        ["BACK TO MENU"] = "VOLVER AL MENÚ",
        ["USE VALORANT SENSITIVITY"] = "USAR SENSIBILIDAD DE VALORANT",
        ["SENSITIVITY"] = "SENSIBILIDAD",
        ["AUDIO"] = "AUDIO",
        ["MASTER VOLUME"] = "VOLUMEN GENERAL",
        ["GRAPHICS"] = "GRÁFICOS",
        ["QUALITY PRESET"] = "CALIDAD",
        ["APPLIES NEXT DRILL"] = "SE APLICA AL SIGUIENTE DRILL",
        ["VSYNC"] = "V-Sync",
        ["FPS CAP"] = "LÍMITE DE FPS",
        ["VSYNC AND FPS CAP APPLY IMMEDIATELY"] = "V-SYNC Y LÍMITE DE FPS SE APLICAN AL INSTANTE",
        ["ESC — RESUME"] = "ESC — CONTINUAR",
        ["CONTINUE"] = "CONTINUAR",
        ["PRACTICE"] = "PRÁCTICA",
        ["QUALITY"] = "CALIDAD",
        ["LOW"] = "BAJO",
        ["MED"] = "MEDIO",
        ["HIGH"] = "ALTO",
        ["ULTRA"] = "ULTRA",
        ["ON"] = "SÍ",
        ["OFF"] = "NO",
        ["Unlimited"] = "Sin límite",
        ["FROM VALORANT"] = "DESDE VALORANT",
        ["CUSTOM"] = "PERSONALIZADA",

        // ---- settings: shell ----
        ["GENERAL"] = "GENERAL",
        ["ABOUT"] = "ACERCA DE",
        ["CHANGES APPLY IMMEDIATELY AND ARE SAVED · ANYTHING LEFT ON \"VALORANT\" FOLLOWS YOUR GAME"] = "LOS CAMBIOS SE APLICAN Y SE GUARDAN AL INSTANTE · LO QUE ESTÉ EN «VALORANT» SIGUE A TU JUEGO",
        ["VERSION · UPDATES · FOLDERS · OPEN-SOURCE LICENSES"] = "VERSIÓN · ACTUALIZACIONES · CARPETAS · LICENCIAS",
        ["AIM & ACCOUNT"] = "PUNTERÍA Y CUENTA",
        ["VIDEO & GAMEPLAY"] = "VÍDEO Y JUGABILIDAD",
        ["IMPORTED FROM VALORANT"] = "IMPORTADO DE VALORANT",
        ["read-only"] = "solo lectura",
        ["Esc — back to menu   ·   CA-VAL-AIM is free software (GPL-3.0)   ·   fan-made, not affiliated with or endorsed by Riot Games"] = "Esc — volver al menú   ·   CA-VAL-AIM es software libre (GPL-3.0)   ·   fan-made, no afiliado ni respaldado por Riot Games",
        ["Esc — back to menu   ·   F11 — toggle windowed / borderless   ·   Shift-click the custom sensitivity arrows for ±0.001"] = "Esc — volver al menú   ·   F11 — ventana / sin bordes   ·   Shift-clic en las flechas de sensibilidad para ±0.001",
        ["VALORANT ACCOUNT"] = "CUENTA DE VALORANT",
        ["ACCOUNT"] = "CUENTA",
        ["None found"] = "Ninguna encontrada",
        ["No Valorant config folder found on this PC"] = "No se encontró la carpeta de configuración de Valorant en este PC",
        ["this one was used last"] = "esta fue la última usada",
        ["SENSITIVITY"] = "SENSIBILIDAD",
        ["Sensitivity source"] = "Origen de la sensibilidad",
        ["VALORANT"] = "VALORANT",
        ["CUSTOM"] = "PERSONALIZADA",
        ["Mouse DPI"] = "DPI del ratón",
        ["Only for showing eDPI and cm/360"] = "Solo para mostrar eDPI y cm/360",
        ["Custom · type a value or use the arrows (Shift ±0.001)"] = "Personalizado · escribe un valor o usa las flechas (Shift ±0.001)",
        ["From VALORANT · type a value or use the arrows to set your own"] = "De VALORANT · escribe un valor o usa las flechas para usar el tuyo",
        ["Click, type your sensitivity (e.g. 0.35), press Enter"] = "Haz clic, escribe tu sensibilidad (p. ej. 0.35) y pulsa Enter",
        ["20 found · this one was used last"] = "20 encontradas · esta fue la última usada",
        ["Red"] = "Rojo",
        ["Yellow (Deuteranopia)"] = "Amarillo (Deuteranopía)",
        ["Yellow (Protanopia)"] = "Amarillo (Protanopía)",
        ["Purple (Tritanopia)"] = "Morado (Tritanopía)",
        ["Valorant (Red)"] = "Valorant (Rojo)",
        ["Valorant (Yellow (Deuteranopia))"] = "Valorant (Amarillo (Deuteranopía))",
        ["Valorant (Yellow (Protanopia))"] = "Valorant (Amarillo (Protanopía))",
        ["Valorant (Purple (Tritanopia))"] = "Valorant (Morado (Tritanopía))",
        ["ENEMIES"] = "ENEMIGOS",
        ["Enemy highlight"] = "Color de enemigos",
        ["Enemy outlines + fresnel"] = "Contornos + fresnel de enemigos",
        ["Valorant: \"Hide outlines and fresnel\" off = ON"] = "Valorant: «Ocultar contornos y fresnel» desactivado = SÍ",
        ["Aim drill targets"] = "Objetivos de los drills",
        ["VALORANT"] = "VALORANT",
        ["CLASSIC"] = "CLÁSICO",
        ["Solid spheres in the highlight colour"] = "Esferas sólidas del color de resaltado",
        ["Dark body + highlight outline, like VALORANT enemies"] = "Cuerpo oscuro + contorno de resaltado, como los enemigos de VALORANT",
        ["CROSSHAIR"] = "MIRA",
        ["Crosshair"] = "Mira",
        ["Run the Crosshair Finder (Coach) or paste a code below for a second option"] = "Usa el Buscador de miras (Coach) o pega un código abajo para una segunda opción",
        ["Your imported VALORANT crosshair"] = "Tu mira importada de VALORANT",
        ["CA-VAL-AIM only"] = "Solo CA-VAL-AIM",
        ["Code of the crosshair CA-VAL-AIM uses now"] = "Código de la mira que usa CA-VAL-AIM ahora",
        ["Last Crosshair Finder result"] = "Último resultado del Buscador de miras",
        ["Use a crosshair code"] = "Usa un código de mira",
        ["Copy it in VALORANT (Export Profile Code), then PASTE here"] = "Cópialo en VALORANT (Exportar código de perfil) y pégalo aquí",
        ["PASTE"] = "PEGAR",
        ["NOT A CODE"] = "NO ES UN CÓDIGO",
        ["USED ✓"] = "USADO ✓",
        ["COPY"] = "COPIAR",
        ["COPIED ✓"] = "COPIADO ✓",
        ["Your VALORANT crosshair stands out on common map colours."] = "Tu mira de VALORANT se distingue en los colores habituales de los mapas.",
        ["DISPLAY"] = "PANTALLA",
        ["Display mode"] = "Modo de pantalla",
        ["Monitor"] = "Monitor",
        ["VSync"] = "VSync",
        ["FPS cap"] = "Límite de FPS",
        ["GRAPHICS"] = "GRÁFICOS",
        ["Graphics quality"] = "Calidad gráfica",
        ["Applies on next drill"] = "Se aplica en el siguiente drill",
        ["GAMEPLAY"] = "JUGABILIDAD",
        ["Difficulty tier"] = "Nivel de dificultad",
        ["Bots react in"] = "Los bots reaccionan en",
        ["Viewmodel (first-person gun)"] = "Modelo en primera persona (arma)",
        ["Weapon hand"] = "Mano del arma",
        ["RIGHT"] = "DERECHA",
        ["LEFT"] = "IZQUIERDA",
        ["Applies on next drill · VALORANT uses"] = "Se aplica en el siguiente drill · VALORANT usa",
        ["AUDIO"] = "AUDIO",
        ["Volume"] = "Volumen",
        ["PRACTICE DEFAULTS"] = "VALORES DE PRÁCTICA",
        ["Primary rifle"] = "Rifle principal",
        ["Ammo in drills"] = "Balas en los drills",
        ["Drill default"] = "Según el ejercicio",
        ["Infinite"] = "Infinitas",
        ["Real magazines"] = "Cargadores reales",
        ["Rifle drills use this weapon. The Vandal Spray and Phantom Spray keep their own gun."] = "Los ejercicios de rifle usan esta arma. Ráfaga Vandal y Ráfaga Phantom mantienen su arma.",
        ["Infinite: no reloads. Real: magazine + reserve. Drill default keeps each drill's own rule."] = "Infinitas: sin recargar. Reales: cargador + reserva. Según el ejercicio se mantiene la regla de cada uno.",
        ["APPLIES TO RIFLE DRILLS"] = "SE APLICA A LOS DRILLS DE RIFLE",
        ["LANGUAGE"] = "IDIOMA",
        ["Language"] = "Idioma",
        ["English"] = "Inglés",
        ["Español"] = "Español",
        ["The whole interface changes language immediately."] = "Toda la interfaz cambia de idioma al instante.",
        ["Fullscreen"] = "Pantalla completa",
        ["Windowed Fullscreen"] = "Pantalla completa sin bordes",
        ["Windowed"] = "Ventana",
        ["Default"] = "Predeterminado",
        ["None"] = "Ninguno",
        ["Medium"] = "Medio",
        ["High"] = "Alto",
        ["Low"] = "Bajo",
        ["Competitive (Low)"] = "Competitivo (Bajo)",
        ["Ultra"] = "Ultra",
        ["On"] = "Sí",
        ["Off"] = "No",
        ["Unlimited"] = "Sin límite",
        ["Not found — Valorant defaults"] = "No encontrado — valores de Valorant",
        ["Source"] = "Origen",
        ["Scoped / ADS multiplier"] = "Multiplicador de mira / ADS",
        ["Sniper scope input"] = "Entrada de mira del francotirador",
        ["Hold"] = "Mantener",
        ["Toggle"] = "Alternar",
        ["Move (forward left back right)"] = "Movimiento (adelante izquierda atrás derecha)",
        ["Walk / crouch / jump"] = "Caminar / agacharse / saltar",
        ["Fire / alt fire"] = "Disparar / disparo alternativo",
        ["Reload / use / plant-defuse"] = "Recargar / usar / plantar-desactivar",
        ["Abilities C Q E X"] = "Habilidades C Q E X",
        ["Resolution"] = "Resolución",
        ["Display mode / monitor"] = "Modo de pantalla / monitor",
        ["VSync / FPS limit"] = "VSync / límite de FPS",
        ["Material / Texture / Detail"] = "Material / Textura / Detalle",
        ["UI quality / Anisotropic"] = "Calidad de interfaz / Anisotrópico",
        ["Anti-aliasing"] = "Antialiasing",
        ["Improve clarity / Bloom"] = "Mejorar claridad / Bloom",
        ["NVIDIA Reflex"] = "NVIDIA Reflex",
        ["Show FPS counter"] = "Mostrar contador de FPS",
        ["Crosshair profile"] = "Perfil de mira",
        ["RE-IMPORT FROM VALORANT"] = "REIMPORTAR DE VALORANT",
        ["Re-imported"] = "Reimportado",
        ["account(s)"] = "cuenta(s)",
        ["VALTRAINER"] = "VALTRAINER",
        ["THIS PC"] = "ESTE PC",
        ["Version"] = "Versión",
        ["Renderer"] = "Renderizador",
        ["Graphics card"] = "Tarjeta gráfica",
        ["Unknown"] = "Desconocido",
        ["FOLDERS"] = "CARPETAS",
        ["Settings, stats & recordings"] = "Ajustes, estadísticas y grabaciones",
        ["Logs (attach godot.log to bug reports)"] = "Registros (adjunta godot.log a los reportes)",
        ["UPDATES"] = "ACTUALIZACIONES",
        ["Check for updates"] = "Buscar actualizaciones",
        ["Download updates automatically"] = "Descargar actualizaciones automáticamente",
        ["CHECK NOW"] = "COMPROBAR AHORA",
        ["CHECK FOR UPDATES"] = "BUSCAR ACTUALIZACIONES",
        ["CHECKING…"] = "BUSCANDO…",
        ["UP TO DATE"] = "AL DÍA",
        ["UPDATE TO"] = "ACTUALIZAR A",
        ["UPDATE"] = "ACTUALIZAR",
        ["DOWNLOADING"] = "DESCARGANDO",
        ["INSTALLING…"] = "INSTALANDO…",
        ["OPEN DOWNLOAD"] = "ABRIR DESCARGA",
        ["DOWNLOAD"] = "DESCARGAR",
        ["RESTART TO UPDATE"] = "REINICIAR PARA ACTUALIZAR",
        ["RESTART NOW"] = "REINICIAR AHORA",
        ["UPDATE NOW"] = "ACTUALIZAR AHORA",
        ["Update available"] = "Actualización disponible",
        ["WHAT'S NEW"] = "NOVEDADES",
        ["SKIP THIS VERSION"] = "SALTAR ESTA VERSIÓN",
        ["Downloads and installs the new version in place."] =
            "Descarga e instala la nueva versión en el sitio.",
        ["CA-VAL-AIM closes for a few seconds and opens again on the new version."] =
            "CA-VAL-AIM se cierra unos segundos y se vuelve a abrir con la nueva versión.",
        ["LATER leaves it in the strip under the header; nothing is forced now."] =
            "Si eliges MÁS TARDE, la actualización queda en la franja bajo la cabecera; no se instala nada ahora.",
        ["Windows will ask for administrator permission to install it."] =
            "Windows pedirá permiso de administrador para instalarla.",
        ["GET"] = "OBTENER",
        ["SUPPORT"] = "APOYO",
        ["Support reminders"] = "Recordatorios de apoyo",
        ["BUY ME A COFFEE"] = "INVÍTAME A UN CAFÉ",
        ["KO-FI"] = "KO-FI",
        ["HELP & FEEDBACK"] = "AYUDA Y OPINIONES",
        ["DISCORD"] = "DISCORD",
        ["REPORT A BUG"] = "REPORTAR UN FALLO",
        ["SUGGEST A FEATURE"] = "SUGERIR UNA IDEA",
        ["GITHUB PAGE"] = "PÁGINA DE GITHUB",
        ["COPY SYSTEM INFO"] = "COPIAR INFO DEL SISTEMA",
        ["Copied"] = "Copiado",
        ["OPEN"] = "ABRIR",
        ["DELETE ALL"] = "BORRAR TODO",
        ["CONFIRM?"] = "¿CONFIRMAR?",
        ["Rewind recordings"] = "Grabaciones de Rewind",
        ["Kept: your last"] = "Se guardan: tus últimas",
        ["runs + examples of problems the coach still sees."] = "partidas + ejemplos de problemas que el coach aún detecta.",
        ["Deleted automatically: problems you fixed, and runs older than"] = "Se borran solos: problemas corregidos y partidas de más de",
        ["days."] = "días.",
        ["LICENSES"] = "LICENCIAS",
        ["open-source notices"] = "avisos de código abierto",
        ["Free, fan-made aim trainer · open source (GPL-3.0) · not affiliated with Riot Games"] = "Entrenador de puntería fan-made y gratuito · código abierto (GPL-3.0) · no afiliado a Riot Games",
        ["Checking GitHub…"] = "Comprobando GitHub…",
        ["You have the latest version"] = "Tienes la última versión",
        ["Couldn't check for updates"] = "No se pudieron buscar actualizaciones",
        ["is available"] = "está disponible",
        ["minutes ago"] = "minutos atrás",
        ["hours ago"] = "horas atrás",
        ["days ago"] = "días atrás",
        ["just now"] = "ahora mismo",

        // ---- results / HUD / common ----
        ["Accuracy"] = "Precisión",
        ["Kills"] = "Bajas",
        ["Avg time to kill"] = "Tiempo medio de baja",
        ["Headshot %"] = "Porcentaje de tiros a la cabeza",
        ["Level at"] = "Nivel con",
        ["hits"] = "aciertos",
        ["Counted sprays"] = "Ráfagas contadas",
        ["Bullets on target"] = "Balas en el objetivo",
        ["Best spray"] = "Mejor ráfaga",
        ["Short sprays (not counted)"] = "Ráfagas cortas (no contadas)",
        ["Shots"] = "Disparos",
        ["Hits"] = "Aciertos",
        ["Headshots"] = "Tiros a la cabeza",
        ["Agent"] = "Agente",
        ["Escaped"] = "Escapados",
        ["Bots that got away"] = "Bots que se escaparon",
        ["Bots that timed out"] = "Bots que no llegaron a tiempo",
        ["Duels won / lost"] = "Duelos ganados / perdidos",
        ["Median time to kill"] = "Tiempo medio de baja",
        ["Rounds won"] = "Rondas ganadas",
        ["Median clear time"] = "Tiempo medio de despeje",
        ["Kills / deaths"] = "Bajas / muertes",
        ["Rounds won / lost"] = "Rondas ganadas / perdidas",
        ["Avg clear time"] = "Tiempo medio de despeje",
        ["Peeks"] = "Asomadas",
        ["Pre-aim error"] = "Error de preapuntado",
        ["Fakes read"] = "Fintas leídas",
        ["First sight → first hit"] = "Primera vista → primer impacto",
        ["On the angle (≤ 10°)"] = "En el ángulo (≤ 10°)",
        ["Visible head to first hit"] = "Cabeza visible → primer impacto",
        ["Kill to next first hit"] = "Baja → siguiente primer impacto",
        ["On target while firing"] = "En el objetivo mientras disparas",
        ["Time on target"] = "Tiempo en el objetivo",
        ["Re-acquire after a change"] = "Recuperación tras un cambio",
        ["NEW PERSONAL BEST"] = "NUEVO RÉCORD PERSONAL",
        ["FIRST RUN AT"] = "PRIMERA PARTIDA EN",
        ["previous best"] = "récord anterior",
        ["best"] = "récord",
        ["BELOW ROOKIE"] = "POR DEBAJO DE NOVATO",
        ["keep practising"] = "sigue practicando",
        ["GET READY"] = "PREPÁRATE",
        ["FREE PLAY"] = "JUEGO LIBRE",
        ["NO SCORE · ESC TO STOP"] = "SIN PUNTUACIÓN · ESC PARA PARAR",
        ["MADE BY THE COMMUNITY"] = "HECHO POR LA COMUNIDAD",
        ["SPRAY (LIVE)"] = "RÁFAGA (EN VIVO)",
        ["LAST SPRAY"] = "ÚLTIMA RÁFAGA",
        ["on target"] = "en el objetivo",
        ["head"] = "cabeza",
        ["body"] = "cuerpo",
        ["miss"] = "fallo",
        ["raw pattern"] = "patrón sin compensar",
        ["Distance"] = "Distancia",
        ["spray ≥"] = "ráfaga ≥",
        ["bullets"] = "balas",
        ["Last spray"] = "Última ráfaga",
        ["HP"] = "VIDA",
        ["SHIELD"] = "ESCUDO",
        ["AMMO"] = "BALAS",
        ["RELOADING"] = "RECARGANDO",
        ["SPEED"] = "VELOCIDAD",
        ["STOP"] = "PARADO",
        ["FPS"] = "FPS",
        ["PAUSED"] = "PAUSA",
        ["RESULTS"] = "RESULTADOS",
        ["MENU"] = "MENÚ",
        ["RETRY"] = "REINTENTAR",
        ["NEXT"] = "SIGUIENTE",
        ["LAST"] = "ÚLTIMA",
        ["REWIND"] = "REBOBINAR",
        ["SHARE"] = "COMPARTIR",
        ["SAVE"] = "GUARDAR",
        ["CANCEL"] = "CANCELAR",
        ["CLOSE"] = "CERRAR",
        ["SHORT TITLE"] = "TÍTULO CORTO",
        ["WHAT HAPPENED?"] = "¿QUÉ PASÓ?",
        ["STEPS TO REPRODUCE"] = "PASOS PARA REPRODUCIRLO",
        ["WHAT DID YOU EXPECT?"] = "¿QUÉ ESPERABAS?",
        ["WHAT'S YOUR IDEA?"] = "¿CUÁL ES TU IDEA?",
        ["WHY WOULD IT HELP?"] = "¿POR QUÉ AYUDARÍA?",
        ["DISCORD USERNAME"] = "USUARIO DE DISCORD",
        ["Your Discord username"] = "Tu usuario de Discord",
        ["optional"] = "opcional",
        ["optional, so we can reply"] = "opcional, para poder responderte",
        ["COPY REPORT"] = "COPIAR REPORTE",
        ["COPY IDEA"] = "COPIAR IDEA",
        ["OPEN DISCORD"] = "ABRIR DISCORD",
        ["VIEW MY REPORT"] = "VER MI REPORTE",
        ["VIEW MY IDEA"] = "VER MI IDEA",
        ["OPEN ON GITHUB"] = "ABRIR EN GITHUB",
        ["Short title, e.g. \"Crosshair is missing in Gridshot\""] =
            "Título corto, p. ej. \"Falta la mira en Gridshot\"",
        ["Short title, e.g. \"Drill with crouch-spamming bots\""] =
            "Título corto, p. ej. \"Ejercicio con bots que spamean agacharse\"",
        ["Posts the report and the info on the right to #bug-reports on the CA-VAL-AIM Discord"] =
            "Publica el reporte y la información de la derecha en #bug-reports del Discord de CA-VAL-AIM",
        ["This build can't send reports itself: copies the report, then opens the Discord invite so you can paste it in #bug-reports"] =
            "Esta versión no puede enviar reportes sola: copia el reporte y abre la invitación de Discord para que lo pegues en #bug-reports",
        ["Copies your idea as text, to paste anywhere"] = "Copia tu idea como texto, para pegarla donde quieras",
        ["Copies the report and the info on the right as text, to paste anywhere"] =
            "Copia el reporte y la información de la derecha como texto, para pegarlos donde quieras",
        ["Opens the CA-VAL-AIM Discord invite in your browser"] = "Abre la invitación del Discord de CA-VAL-AIM en tu navegador",
        ["Opens GitHub's issue form in your browser instead (needs a GitHub account; attach godot.log there)"] =
            "Abre el formulario de issues de GitHub en tu navegador (necesitas cuenta de GitHub; adjunta allí godot.log)",
        ["Dev run: prints the JSON to the log instead of sending it"] =
            "Ejecución dev: imprime el JSON en el log en vez de enviarlo",
        ["YES"] = "SÍ",
        ["NO"] = "NO",
        ["OK"] = "OK",
        ["Warning"] = "Aviso",
        ["Error"] = "Error",
        ["Level"] = "Nivel",
        ["run"] = "partida",
        ["runs"] = "partidas",
        ["drill"] = "drill",
        ["drills"] = "drills",
        ["agent"] = "agente",
        ["agents"] = "agentes",
        ["target"] = "objetivo",
        ["targets"] = "objetivos",
        ["bot"] = "bot",
        ["bots"] = "bots",
        ["shot"] = "disparo",
        ["shots"] = "disparos",
        ["kill"] = "baja",
        ["kills"] = "bajas",
        ["score"] = "puntuación",
        ["time"] = "tiempo",
        ["best"] = "mejor",
        ["average"] = "media",
        ["headshot"] = "tiro a la cabeza",
        ["headshots"] = "tiros a la cabeza",
        ["accuracy"] = "precisión",
        ["round"] = "ronda",
        ["rounds"] = "rondas",
        ["seconds"] = "segundos",
        ["second"] = "segundo",
        ["minutes"] = "minutos",
        ["minute"] = "minuto",
        ["map"] = "mapa",
        ["weapon"] = "arma",
        ["settings"] = "ajustes",
        ["menu"] = "menú",
        ["back"] = "volver",
        ["start"] = "empezar",
        ["stop"] = "parar",
        ["open"] = "abrir",
        ["close"] = "cerrar",
        ["save"] = "guardar",
        ["copy"] = "copiar",
        ["paste"] = "pegar",
        ["search"] = "buscar",
        ["select"] = "seleccionar",
        ["selected"] = "seleccionado",
        ["enabled"] = "activado",
        ["disabled"] = "desactivado",
        ["on"] = "sí",
        ["off"] = "no",
        ["not found"] = "no encontrado",
        ["read-only"] = "solo lectura",
        ["applies immediately"] = "se aplica al instante",
        ["applies on next drill"] = "se aplica en el siguiente drill",
        ["saved"] = "guardado",
        ["loading"] = "cargando",
        ["ready"] = "listo",
        ["failed"] = "falló",
        ["unknown"] = "desconocido",
        ["default"] = "predeterminado",
        ["recommended"] = "recomendado",
        ["custom"] = "personalizado",
        ["minimum"] = "mínimo",
        ["maximum"] = "máximo",
        ["left"] = "izquierda",
        ["right"] = "derecha",
        ["center"] = "centro",
        ["centre"] = "centro",
        ["top"] = "arriba",
        ["bottom"] = "abajo",
        ["up"] = "arriba",
        ["down"] = "abajo",
        ["more"] = "más",
        ["less"] = "menos",
        ["good"] = "bien",
        ["great"] = "genial",
        ["bad"] = "mal",
        ["easy"] = "fácil",
        ["normal"] = "normal",
        ["hard"] = "difícil",
        ["practice"] = "práctica",
        ["training"] = "entrenamiento",
        ["aim"] = "puntería",
        ["movement"] = "movimiento",
        ["recoil"] = "retroceso",
        ["utility"] = "utilidad",
        ["crosshair"] = "mira",
        ["weapon"] = "arma",
        ["volume"] = "volumen",
        ["graphics"] = "gráficos",
        ["quality"] = "calidad",
        ["display"] = "pantalla",
        ["monitor"] = "monitor",
        ["fullscreen"] = "pantalla completa",
        ["windowed"] = "ventana",
        ["update"] = "actualización",
        ["updates"] = "actualizaciones",
        ["download"] = "descargar",
        ["check"] = "comprobar",
        ["support"] = "apoyo",
        ["license"] = "licencia",
        ["licenses"] = "licencias",
        ["version"] = "versión",
        ["folder"] = "carpeta",
        ["folders"] = "carpetas",
        ["file"] = "archivo",
        ["files"] = "archivos",
        ["setup"] = "configuración",
        ["record"] = "récord",
        ["personal"] = "personal",
        ["new"] = "nuevo",
        ["best"] = "mejor",
        ["first"] = "primera",
        ["last"] = "última",
        ["next"] = "siguiente",
        ["previous"] = "anterior",
        ["all"] = "todos",
        ["none"] = "ninguno",
        ["any"] = "cualquiera",
        ["other"] = "otro",
        ["more"] = "más",
        ["view"] = "ver",
        ["show"] = "mostrar",
        ["hide"] = "ocultar",
        ["edit"] = "editar",
        ["delete"] = "borrar",
        ["reset"] = "restablecer",
        ["apply"] = "aplicar",
        ["choose"] = "elegir",
        ["click"] = "clic",
        ["press"] = "pulsa",
        ["type"] = "escribe",
        ["drag"] = "arrastra",
        ["hold"] = "mantén",
        ["release"] = "suelta",
        ["move"] = "mueve",
        ["turn"] = "gira",
        ["look"] = "mira",
        ["shoot"] = "dispara",
        ["fire"] = "dispara",
        ["reload"] = "recarga",
        ["recoil"] = "retroceso",
        ["spray"] = "ráfaga",
        ["sprays"] = "ráfagas",
        ["pattern"] = "patrón",
        ["control"] = "control",
        ["tracking"] = "seguimiento",
        ["flick"] = "flick",
        ["flicks"] = "flicks",
        ["switch"] = "cambio",
        ["peek"] = "asomarse",
        ["peeks"] = "asomadas",
        ["strafe"] = "strafe",
        ["strafing"] = "strafing",
        ["counter-strafe"] = "contra-strafe",
        ["deathmatch"] = "deathmatch",
        ["operator"] = "operador",
        ["sheriff"] = "sheriff",
        ["vandal"] = "vandal",
        ["phantom"] = "phantom",
        ["head"] = "cabeza",
        ["body"] = "cuerpo",
        ["legs"] = "piernas",
        ["chest"] = "pecho",
        ["wall"] = "muro",
        ["smoke"] = "humo",
        ["flash"] = "flash",
        ["recon"] = "recon",
        ["ability"] = "habilidad",
        ["abilities"] = "habilidades",
        ["ultimate"] = "ultimate",
        ["signature"] = "firma",
        ["role"] = "rol",
        ["duelist"] = "duelista",
        ["initiator"] = "iniciador",
        ["controller"] = "controlador",
        ["sentinel"] = "centinela",
        ["damage"] = "daño",
        ["health"] = "vida",
        ["armor"] = "armadura",
        ["shield"] = "escudo",
        ["ammo"] = "munición",
        ["magazine"] = "cargador",
        ["reserve"] = "reserva",
        ["reload"] = "recarga",
        ["rate"] = "cadencia",
        ["speed"] = "velocidad",
        ["distance"] = "distancia",
        ["size"] = "tamaño",
        ["wide"] = "ancho",
        ["small"] = "pequeño",
        ["large"] = "grande",
        ["fast"] = "rápido",
        ["slow"] = "lento",
        ["higher"] = "más alto",
        ["lower"] = "más bajo",
        ["increase"] = "aumentar",
        ["decrease"] = "disminuir",
        ["higher tiers"] = "niveles altos",
        ["lower tiers"] = "niveles bajos",
        ["tier"] = "nivel",
        ["tiers"] = "niveles",
        ["rank"] = "rango",
        ["ranks"] = "rangos",
        ["badge"] = "insignia",
        ["score"] = "puntuación",
        ["points"] = "puntos",
        ["percent"] = "por ciento",
        ["percentage"] = "porcentaje",
        ["average"] = "media",
        ["median"] = "mediana",
        ["total"] = "total",
        ["count"] = "recuento",
        ["counted"] = "contado",
        ["not counted"] = "no contado",
        ["short"] = "corta",
        ["long"] = "larga",
        ["full"] = "completa",
        ["empty"] = "vacío",
        ["ready"] = "listo",
        ["get ready"] = "prepárate",
        ["resume"] = "continuar",
        ["restart"] = "reiniciar",
        ["quit"] = "salir",
        ["exit"] = "salir",
        ["back to menu"] = "volver al menú",
        ["main menu"] = "menú principal",
        ["pause"] = "pausa",
        ["paused"] = "pausa",
        ["coach"] = "coach",
        ["focus"] = "foco",
        ["fix first"] = "corrige primero",
        ["problem"] = "problema",
        ["problems"] = "problemas",
        ["tip"] = "consejo",
        ["tips"] = "consejos",
        ["drill"] = "drill",
        ["drills"] = "drills",
        ["simulated player"] = "jugador simulado",
        ["preview"] = "vista previa",
        ["free play"] = "juego libre",
        ["no score"] = "sin puntuación",
        ["esc to stop"] = "esc para parar",
        ["start"] = "empezar",
        ["back"] = "volver",
        ["made by the community"] = "hecho por la comunidad",
        ["set on the menu"] = "se elige en el menú",
        ["how it works"] = "cómo funciona",
        ["everything runs at your tier and map"] = "todo usa tu nivel y tu mapa",
        ["search agents, abilities or utility"] = "buscar agentes, habilidades o utilidad",
        ["no agent or ability matches"] = "ningún agente o habilidad coincide",
        ["try a name"] = "prueba un nombre",
        ["no signature drill for"] = "aún no hay drill de firma para",
        ["the"] = "el",
        ["and"] = "y",
        ["or"] = "o",
        ["with"] = "con",
        ["without"] = "sin",
        ["from"] = "de",
        ["to"] = "a",
        ["in"] = "en",
        ["on"] = "en",
        ["at"] = "a",
        ["for"] = "para",
        ["of"] = "de",
        ["the"] = "el",
        ["your"] = "tu",
        ["you"] = "tú",
        ["their"] = "su",
        ["they"] = "ellos",
        ["this"] = "este",
        ["that"] = "ese",
        ["these"] = "estos",
        ["those"] = "esos",
        ["is"] = "es",
        ["are"] = "son",
        ["was"] = "era",
        ["were"] = "eran",
        ["be"] = "ser",
        ["been"] = "sido",
        ["has"] = "tiene",
        ["have"] = "tienen",
        ["can"] = "puede",
        ["will"] = "va a",
        ["may"] = "puede",
        ["must"] = "debe",
        ["should"] = "debería",
        ["more"] = "más",
        ["less"] = "menos",
        ["than"] = "que",
        ["then"] = "luego",
        ["before"] = "antes",
        ["after"] = "después",
        ["while"] = "mientras",
        ["when"] = "cuando",
        ["where"] = "dónde",
        ["what"] = "qué",
        ["which"] = "cuál",
        ["who"] = "quién",
        ["how"] = "cómo",
        ["why"] = "por qué",
        ["because"] = "porque",
        ["but"] = "pero",
        ["if"] = "si",
        ["not"] = "no",
        ["no"] = "no",
        ["yes"] = "sí",
        ["only"] = "solo",
        ["also"] = "también",
        ["very"] = "muy",
        ["too"] = "demasiado",
        ["about"] = "sobre",
        ["over"] = "sobre",
        ["under"] = "debajo",
        ["between"] = "entre",
        ["during"] = "durante",
        ["each"] = "cada",
        ["every"] = "cada",
        ["some"] = "algunos",
        ["most"] = "la mayoría",
        ["much"] = "mucho",
        ["many"] = "muchos",
        ["few"] = "pocos",
        ["first"] = "primera",
        ["second"] = "segunda",
        ["third"] = "tercera",
        ["last"] = "última",
        ["next"] = "siguiente",
        ["new"] = "nuevo",
        ["old"] = "antiguo",
        ["same"] = "mismo",
        ["different"] = "diferente",
        ["good"] = "bueno",
        ["bad"] = "malo",
        ["best"] = "mejor",
        ["worst"] = "peor",
        ["high"] = "alto",
        ["low"] = "bajo",
        ["big"] = "grande",
        ["small"] = "pequeño",
        ["long"] = "largo",
        ["short"] = "corto",
        ["fast"] = "rápido",
        ["slow"] = "lento",
        ["left"] = "izquierda",
        ["right"] = "derecha",
        ["up"] = "arriba",
        ["down"] = "abajo",
        ["here"] = "aquí",
        ["there"] = "allí",
        ["now"] = "ahora",
        ["later"] = "después",
        ["again"] = "otra vez",
        ["still"] = "todavía",
        ["already"] = "ya",
        ["always"] = "siempre",
        ["never"] = "nunca",
        ["sometimes"] = "a veces",
        ["usually"] = "normalmente",
        ["really"] = "realmente",
        ["just"] = "solo",
        ["like"] = "como",
        ["want"] = "quieres",
        ["need"] = "necesitas",
        ["use"] = "usa",
        ["make"] = "haz",
        ["get"] = "consigue",
        ["keep"] = "mantén",
        ["try"] = "prueba",
        ["play"] = "juega",
        ["see"] = "mira",
        ["find"] = "encuentra",
        ["help"] = "ayuda",
        ["work"] = "funciona",
        ["works"] = "funciona",
        ["run"] = "corre",
        ["running"] = "corriendo",
        ["move"] = "muévete",
        ["shoot"] = "dispara",
        ["shooting"] = "disparando",
        ["kill"] = "mata",
        ["killed"] = "eliminado",
        ["hit"] = "acierta",
        ["miss"] = "falla",
        ["hold"] = "mantén",
        ["release"] = "suelta",
        ["pull"] = "tira",
        ["push"] = "empuja",
        ["aim"] = "apunta",
        ["look"] = "mira",
        ["turn"] = "gira",
        ["stop"] = "para",
        ["start"] = "empieza",
        ["continue"] = "continúa",
        ["wait"] = "espera",
        ["watch"] = "observa",
        ["listen"] = "escucha",
        ["click"] = "haz clic",
        ["type"] = "escribe",
        ["press"] = "pulsa",
        ["select"] = "selecciona",
        ["choose"] = "elige",
        ["change"] = "cambia",
        ["open"] = "abre",
        ["close"] = "cierra",
        ["save"] = "guarda",
        ["copy"] = "copia",
        ["paste"] = "pega",
        ["delete"] = "borra",
        ["reset"] = "restablece",
        ["apply"] = "aplica",
        ["check"] = "comprueba",
        ["download"] = "descarga",
        ["install"] = "instala",
        ["restart"] = "reinicia",
        ["quit"] = "sal",
        ["resume"] = "continúa",
        ["back"] = "vuelve",
        ["open"] = "abre",
        ["copy"] = "copia",
        ["settings"] = "ajustes",
        ["menu"] = "menú",
        ["drill"] = "drill",
        ["drills"] = "drills",
        ["agent"] = "agente",
        ["agents"] = "agentes",
        ["target"] = "objetivo",
        ["targets"] = "objetivos",
        ["bot"] = "bot",
        ["bots"] = "bots",
        ["shot"] = "disparo",
        ["shots"] = "disparos",
        ["kill"] = "baja",
        ["kills"] = "bajas",
        ["score"] = "puntuación",
        ["time"] = "tiempo",
        ["accuracy"] = "precisión",
        ["headshot"] = "tiro a la cabeza",
        ["headshots"] = "tiros a la cabeza",
        ["round"] = "ronda",
        ["rounds"] = "rondas",
        ["seconds"] = "segundos",
        ["second"] = "segundo",
        ["minutes"] = "minutos",
        ["minute"] = "minuto",
        ["map"] = "mapa",
        ["weapon"] = "arma",
        ["volume"] = "volumen",
        ["graphics"] = "gráficos",
        ["quality"] = "calidad",
        ["display"] = "pantalla",
        ["monitor"] = "monitor",
        ["fullscreen"] = "pantalla completa",
        ["windowed"] = "ventana",
        ["update"] = "actualización",
        ["updates"] = "actualizaciones",
        ["download"] = "descargar",
        ["check"] = "comprobar",
        ["support"] = "apoyo",
        ["license"] = "licencia",
        ["licenses"] = "licencias",
        ["version"] = "versión",
        ["folder"] = "carpeta",
        ["folders"] = "carpetas",
        ["file"] = "archivo",
        ["files"] = "archivos",
        ["setup"] = "configuración",
        ["record"] = "récord",
        ["personal"] = "personal",
        ["new"] = "nuevo",
        ["first"] = "primera",
        ["last"] = "última",
        ["next"] = "siguiente",
        ["previous"] = "anterior",
        ["all"] = "todos",
        ["none"] = "ninguno",
        ["any"] = "cualquiera",
        ["other"] = "otro",
        ["more"] = "más",
        ["view"] = "ver",
        ["show"] = "mostrar",
        ["hide"] = "ocultar",
        ["edit"] = "editar",
        ["delete"] = "borrar",
        ["reset"] = "restablecer",
        ["apply"] = "aplicar",
        ["choose"] = "elegir",
        ["click"] = "clic",
        ["press"] = "pulsa",
        ["type"] = "escribe",
        ["drag"] = "arrastra",
        ["hold"] = "mantén",
        ["release"] = "suelta",
        ["move"] = "mueve",
        ["turn"] = "gira",
        ["look"] = "mira",
        ["shoot"] = "dispara",
        ["fire"] = "dispara",
        ["reload"] = "recarga",
        ["recoil"] = "retroceso",
        ["spray"] = "ráfaga",
        ["sprays"] = "ráfagas",
        ["pattern"] = "patrón",
        ["control"] = "control",
        ["tracking"] = "seguimiento",
        ["flick"] = "flick",
        ["flicks"] = "flicks",
        ["switch"] = "cambio",
        ["peek"] = "asomarse",
        ["peeks"] = "asomadas",
        ["strafe"] = "strafe",
        ["strafing"] = "strafing",
        ["counter-strafe"] = "contra-strafe",
        ["deathmatch"] = "deathmatch",
        ["operator"] = "operador",
        ["sheriff"] = "sheriff",
        ["vandal"] = "vandal",
        ["phantom"] = "phantom",
        ["head"] = "cabeza",
        ["body"] = "cuerpo",
        ["legs"] = "piernas",
        ["chest"] = "pecho",
        ["wall"] = "muro",
        ["smoke"] = "humo",
        ["flash"] = "flash",
        ["recon"] = "recon",
        ["ability"] = "habilidad",
        ["abilities"] = "habilidades",
        ["ultimate"] = "ultimate",
        ["signature"] = "firma",
        ["role"] = "rol",
        ["duelist"] = "duelista",
        ["initiator"] = "iniciador",
        ["controller"] = "controlador",
        ["sentinel"] = "centinela",
        ["damage"] = "daño",
        ["health"] = "vida",
        ["armor"] = "armadura",
        ["shield"] = "escudo",
        ["ammo"] = "munición",
        ["magazine"] = "cargador",
        ["reserve"] = "reserva",
        ["reload"] = "recarga",
        ["rate"] = "cadencia",
        ["speed"] = "velocidad",
        ["distance"] = "distancia",
        ["size"] = "tamaño",
        ["wide"] = "ancho",
        ["small"] = "pequeño",
        ["large"] = "grande",
        ["fast"] = "rápido",
        ["slow"] = "lento",
        ["higher"] = "más alto",
        ["lower"] = "más bajo",
        ["increase"] = "aumentar",
        ["decrease"] = "disminuir",
        ["higher tiers"] = "niveles altos",
        ["lower tiers"] = "niveles bajos",
        ["tier"] = "nivel",
        ["tiers"] = "niveles",
        ["rank"] = "rango",
        ["ranks"] = "rangos",
        ["badge"] = "insignia",
        ["score"] = "puntuación",
        ["points"] = "puntos",
        ["percent"] = "por ciento",
        ["percentage"] = "porcentaje",
        ["average"] = "media",
        ["median"] = "mediana",
        ["total"] = "total",
        ["count"] = "recuento",
        ["counted"] = "contado",
        ["not counted"] = "no contado",
        ["short"] = "corta",
        ["long"] = "larga",
        ["full"] = "completa",
        ["empty"] = "vacío",
        ["ready"] = "listo",
        ["get ready"] = "prepárate",
        ["resume"] = "continuar",
        ["restart"] = "reiniciar",
        ["quit"] = "salir",
        ["exit"] = "salir",
        ["back to menu"] = "volver al menú",
        ["main menu"] = "menú principal",
        ["pause"] = "pausa",
        ["paused"] = "pausa",
        ["coach"] = "coach",
        ["focus"] = "foco",
        ["fix first"] = "corrige primero",
        ["problem"] = "problema",
        ["problems"] = "problemas",
        ["tip"] = "consejo",
        ["tips"] = "consejos",
        ["simulated player"] = "jugador simulado",
        ["preview"] = "vista previa",
        ["free play"] = "juego libre",
        ["no score"] = "sin puntuación",
        ["esc to stop"] = "esc para parar",
        ["made by the community"] = "hecho por la comunidad",
        ["set on the menu"] = "se elige en el menú",
        ["how it works"] = "cómo funciona",
        ["everything runs at your tier and map"] = "todo usa tu nivel y tu mapa",
        ["search agents, abilities or utility"] = "buscar agentes, habilidades o utilidad",
        ["no agent or ability matches"] = "ningún agente o habilidad coincide",
        ["try a name"] = "prueba un nombre",
        ["no signature drill for"] = "aún no hay drill de firma para",
        ["the"] = "el",
        ["and"] = "y",
        ["or"] = "o",
        ["with"] = "con",
        ["without"] = "sin",
        ["from"] = "de",
        ["to"] = "a",
        ["in"] = "en",
        ["on"] = "en",
        ["at"] = "a",
        ["for"] = "para",
        ["of"] = "de",
        ["your"] = "tu",
        ["you"] = "tú",
        ["their"] = "su",
        ["they"] = "ellos",
        ["this"] = "este",
        ["that"] = "ese",
        ["these"] = "estos",
        ["those"] = "esos",
        ["is"] = "es",
        ["are"] = "son",
        ["was"] = "era",
        ["were"] = "eran",
        ["be"] = "ser",
        ["been"] = "sido",
        ["has"] = "tiene",
        ["have"] = "tienen",
        ["can"] = "puede",
        ["will"] = "va a",
        ["may"] = "puede",
        ["must"] = "debe",
        ["should"] = "debería",
        ["than"] = "que",
        ["then"] = "luego",
        ["before"] = "antes",
        ["after"] = "después",
        ["while"] = "mientras",
        ["when"] = "cuando",
        ["where"] = "dónde",
        ["what"] = "qué",
        ["which"] = "cuál",
        ["who"] = "quién",
        ["how"] = "cómo",
        ["why"] = "por qué",
        ["because"] = "porque",
        ["but"] = "pero",
        ["if"] = "si",
        ["not"] = "no",
        ["yes"] = "sí",
        ["only"] = "solo",
        ["also"] = "también",
        ["very"] = "muy",
        ["too"] = "demasiado",
        ["about"] = "sobre",
        ["over"] = "sobre",
        ["under"] = "debajo",
        ["between"] = "entre",
        ["during"] = "durante",
        ["each"] = "cada",
        ["every"] = "cada",
        ["some"] = "algunos",
        ["most"] = "la mayoría",
        ["much"] = "mucho",
        ["many"] = "muchos",
        ["few"] = "pocos",
        // 1.18: interface sounds, reaction stats, loading
        ["Interface sounds"] = "Sonidos de interfaz",
        ["Interface volume"] = "Volumen de interfaz",
        ["A soft click on buttons and a quiet chime on confirmations; hover is silent"] = "Un clic suave en los botones y un aviso discreto al confirmar; pasar el ratón no suena",
        ["Menus make no sound"] = "Los menús no hacen ningún sonido",
        ["Relative to the volume above"] = "Relativo al volumen de arriba",
    };

    /// <summary>
    /// Curated entries that win over the older word table. Keeping them separate avoids duplicate-key
    /// accidents in the big dictionary and lets the menu, drills and settings use natural Spanish.
    /// </summary>
    static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- shell ----
        ["LOCK IN"] = "LOCK-IN",
        ["AGENTS"] = "AGENTES",
        ["COACH"] = "ENTRENADOR",
        ["SETTINGS"] = "AJUSTES",
        ["STATS"] = "ESTADÍSTICAS",
        ["QUIT"] = "SALIR",
        ["YOUR SETUP"] = "TU CONFIGURACIÓN",
        ["CROSSHAIR"] = "MIRA",
        ["CATEGORIES"] = "CATEGORÍAS",
        ["ALL"] = "TODOS",
        ["ALL DRILLS"] = "TODOS LOS EJERCICIOS",
        ["DRILLS"] = "EJERCICIOS",
        ["DRILL"] = "EJERCICIO",
        ["DIFFICULTY"] = "DIFICULTAD",
        ["DIFFICULTY TIER"] = "NIVEL DE DIFICULTAD",
        ["MENU"] = "MENÚ",
        ["What are we training today?"] = "¿Qué entrenamos hoy?",
        ["GUIDED SESSION  ·  12 MIN"] = "SESIÓN GUIADA  ·  12 MIN",
        ["Aim better."] = "Apunta mejor.",
        ["Climb higher."] = "Sube de rango.",
        ["Check-in, adaptive aim drills, a deathmatch and your goal — ready for ranked."] = "Check-in, ejercicios de aim adaptados a ti, un deathmatch y tu objetivo. Listo para ranked.",
        ["START LOCK-IN"] = "EMPEZAR LOCK-IN",
        ["BROWSE DRILLS"] = "VER EJERCICIOS",
        ["Bots and targets scale"] = "Los bots y objetivos escalan",
        ["applies to every drill"] = "se aplica a todos los ejercicios",
        ["QUICK SETTINGS"] = "AJUSTES RÁPIDOS",
        ["RIFLE"] = "RIFLE",
        ["AMMO"] = "MUNICIÓN",
        ["LANGUAGE"] = "IDIOMA",
        ["DEFAULT"] = "POR DEFECTO",
        ["REAL"] = "REAL",
        ["INFINITE"] = "INFINITA",
        ["ENGLISH"] = "INGLÉS",
        ["ESPAÑOL"] = "ESPAÑOL",
        ["EN"] = "EN",
        ["ES"] = "ES",
        ["Imported"] = "Importado",
        ["PRIMARY"] = "PRINCIPAL",
        ["SNIPER"] = "FRANCO",
        ["ADS"] = "ADS",
        ["Profile"] = "Perfil",
        ["CUSTOM"] = "PERSONALIZADO",
        ["RANDOM"] = "ALEATORIO",
        ["MADE BY THE COMMUNITY"] = "HECHO POR LA COMUNIDAD",
        ["COMMUNITY"] = "COMUNIDAD",
        ["NEW"] = "NUEVO",
        ["PB"] = "RÉCORD",
        ["PERSONAL BEST"] = "RÉCORD PERSONAL",

        // ---- weapon picker ----
        ["CHOOSE A WEAPON"] = "ELIGE UN ARMA",
        ["Every drill · every gun · Esc closes"] = "Cada ejercicio · cualquier arma · Esc cierra",
        ["each drill's own weapon"] = "el arma propia de cada ejercicio",
        ["Sidearm"] = "Pistola",
        ["SMG"] = "Subfusil",
        ["Shotgun"] = "Escopeta",
        ["Machine gun"] = "Ametralladora",
        ["Melee"] = "Cuerpo a cuerpo",
        ["Knife"] = "Cuchillo",
        ["Fan-made trainer · not endorsed by or affiliated with Riot Games"] =
            "Entrenador no oficial · no está afiliado ni respaldado por Riot Games",

        // ---- toggles ----
        ["ON"] = "SÍ",
        ["VSYNC"] = "V-Sync",

        // ---- results ----
        ["Accuracy"] = "Precisión",
        ["Median time to hit"] = "Mediana hasta impactar",
        ["Expired targets"] = "Objetivos expirados",
        ["REWIND"] = "REBOBINAR",

        // ---- drill preview ----
        ["Length"] = "Duración",
        ["Weapon"] = "Arma",
        ["Where"] = "Dónde",
        ["On screen"] = "En pantalla",
        ["Shooting range"] = "Campo de tiro",
        ["Until you finish it"] = "Hasta que lo termines",
        ["Trains"] = "Entrena",
        ["YOUR RECORD"] = "TU RÉCORD",
        ["bots and targets scale with it"] = "los bots y objetivos escalan con él",
        ["BEST"] = "MEJOR",
        ["BEST BADGE"] = "MEJOR INSIGNIA",
        ["LAST SESSION"] = "ÚLTIMA SESIÓN",
        ["DAY STREAK"] = "RACHA DE DÍAS",
        ["STREAK"] = "RACHA",
        ["REAL ART"] = "ARTE REAL",
        ["TRACED"] = "TRAZADO",
        ["MAP LIBRARY"] = "BIBLIOTECA DE MAPAS",
        ["ALL RUNS"] = "TODAS LAS PARTIDAS",
        ["LAST 20 RUNS"] = "ÚLTIMAS 20 PARTIDAS",
        ["20 RUNS"] = "20 PARTIDAS",
        ["NO RUNS YET"] = "SIN PARTIDAS AÚN",
        ["TRAINED TODAY"] = "ENTRENADO HOY",
        ["NOT TRAINED TODAY"] = "SIN ENTRENAR HOY",
        ["START TODAY"] = "EMPIEZA HOY",
        ["ACCURACY"] = "PRECISIÓN",
        ["Not played at this tier yet"] = "Aún no jugado en este nivel",
        ["NO PREVIEW FOR THIS ONE"] = "SIN VISTA PREVIA",
        ["It runs on your own picks and reactions. Press START to try it."] =
            "Depende de tus elecciones y reacciones. Pulsa EMPEZAR para probarlo.",
        ["BACK"] = "VOLVER",
        ["FREE PLAY"] = "JUEGO LIBRE",
        ["START"] = "EMPEZAR",
        ["Preview: the built-in simulated player, muted. Space / Enter starts, F free play, Esc goes back."] =
            "Vista previa: jugador simulado, sin sonido. Espacio / Enter empieza, F juego libre, Esc vuelve.",
        ["Preview: the built-in simulated player, muted. Space / Enter starts, Esc goes back."] =
            "Vista previa: jugador simulado, sin sonido. Espacio / Enter empieza, Esc vuelve.",
        ["Space / Enter starts, Esc goes back."] = "Espacio / Enter empieza, Esc vuelve.",
        ["Play it with no clock and no score, and nothing saved. It starts over when the time is up; Esc → menu to stop."] =
            "Juégalo sin reloj, sin puntuación y sin guardar nada. Se reinicia al acabar el tiempo; Esc → menú para parar.",

        // ---- categories ----
        ["Aim"] = "Puntería",
        ["Movement"] = "Movimiento",
        ["Utility"] = "Utilidad",
        ["Recoil"] = "Retroceso",
        ["Map"] = "Mapa",
        ["Other"] = "Otros",
        ["Valorant"] = "VALORANT",

        // ---- tiers ----
        ["Rookie"] = "Novato",
        ["Regular"] = "Regular",
        ["Veteran"] = "Veterano",
        ["Elite"] = "Élite",
        ["Pro"] = "Pro",
        ["Iron - Bronze"] = "Hierro - Bronce",
        ["Silver - Gold"] = "Plata - Oro",
        ["Platinum - Diamond"] = "Platino - Diamante",
        ["Ascendant - Immortal"] = "Ascendente - Inmortal",
        ["Radiant"] = "Radiante",
        ["BELOW ROOKIE"] = "POR DEBAJO DE NOVATO",
        ["below Rookie"] = "por debajo de Novato",
        ["keep practising"] = "sigue practicando",
        ["Bots react in"] = "Los bots reaccionan en",
        ["ms · Gridshot targets"] = "ms · objetivos de Gridshot",
        ["cm wide ·"] = "cm de ancho ·",
        ["head flicks at"] = "flicks a la cabeza a",
        ["bots run-and-gun"] = "los bots disparan mientras corren",
        ["bots stop before shooting"] = "los bots se detienen antes de disparar",

        // ---- settings: language, rifle, ammo ----
        ["Interface language"] = "Idioma de la interfaz",
        ["The whole interface changes immediately"] = "Toda la interfaz cambia al instante",
        ["Primary rifle"] = "Rifle principal",
        ["Used by every drill that accepts a rifle; Vandal Spray and Phantom Spray keep their own"] =
            "Se usa en todos los ejercicios con rifle; Ráfaga Vandal y Ráfaga Phantom mantienen el suyo",
        ["Drill default"] = "Según el ejercicio",
        ["Real magazines"] = "Cargadores reales",
        ["Each drill decides (aim drills: infinite; bot drills: reloads)"] =
            "Cada ejercicio decide (puntería: infinita; bots: recargas)",
        ["Magazines and reserve run out"] = "Los cargadores y la reserva se acaban",
        ["No reloading, unlimited reserve"] = "Sin recargar, reserva ilimitada",
        ["Guide line"] = "Línea guía",
        ["YOU DIED"] = "HAS MUERTO",
        ["YOU DIED — they hit first"] = "HAS MUERTO: dispararon primero",
        ["TODAY'S PLAN"] = "PLAN DE HOY",
        ["Skipped the nearest target"] = "Te saltaste el objetivo más cercano",
        ["Every extra degree is flick time and flick error (Fitts' law). Players who scan for the target closest to the crosshair keep every flick short and clear multi-target fights faster."] =
            "Cada grado de más es tiempo y error de flick (ley de Fitts). Quien busca el objetivo más cercano a la mira mantiene cortos todos los flicks y resuelve antes las peleas contra varios.",
        ["Before each flick, find the target closest to your crosshair and go there first."] =
            "Antes de cada flick, busca el objetivo más cercano a tu mira y ve primero a por él.",
        ["Turn on Settings → Guide line: it always points at the nearest target. Follow it until the habit sticks, then turn it off."] =
            "Activa Ajustes → Línea guía: siempre apunta al objetivo más cercano. Síguela hasta coger el hábito y luego desactívala.",
        ["Clear groups in a loop that keeps the flicks short instead of zig-zagging across the grid."] =
            "Limpia los grupos en un recorrido que mantenga cortos los flicks en vez de zigzaguear por la cuadrícula.",
        ["LINE"] = "LÍNEA",
        ["LINE + ROUTE"] = "LÍNEA + RUTA",
        ["No guide"] = "Sin guía",
        ["A line from the crosshair to the nearest target"] = "Una línea desde la mira hasta el objetivo más cercano",
        ["Aim line, plus the shortest route to the nearest enemy on map drills"] =
            "Línea de apuntado y, en los mapas, la ruta más corta hasta el enemigo más cercano",
        ["APPLIES NEXT DRILL"] = "SE APLICA AL SIGUIENTE EJERCICIO",
        ["Fullscreen"] = "Completa",
        ["Windowed Fullscreen"] = "Sin bordes",
        ["Windowed"] = "Ventana",
        ["Ignore"] = "Ignorar",
        ["ENGINE PARTS"] = "PARTES DEL MOTOR",
        ["ASSETS"] = "RECURSOS",

        // ---- drill names ----
        ["Gridshot"] = "Gridshot",
        ["Head Flicks"] = "Flicks a la cabeza",
        ["Spidershot"] = "Spidershot",
        ["Strafe Tracking"] = "Seguimiento en strafe",
        ["Reaction Test"] = "Test de reacción",
        ["Long-range Taps"] = "Taps a larga distancia",
        ["Microshot"] = "Microshot",
        ["Pop-up Reflex"] = "Reflejos emergentes",
        ["Target Switch"] = "Cambio de objetivo",
        ["Strafe Bots"] = "Bots en strafe",
        ["Peek Practice"] = "Práctica de asomarse",
        ["Operator"] = "Operator",
        ["Crosshair Finder"] = "Buscador de mira",
        ["Deathmatch"] = "Deathmatch",
        ["Site Anchor"] = "Ancla de sitio",
        ["Sound Lock"] = "Bloqueo por sonido",
        ["Site Clear"] = "Despeje de sitio",
        ["Flash Dodge"] = "Esquivar flashes",
        ["Counter-Strafe"] = "Contra-strafe",
        ["Peek Duels"] = "Duelos de asomada",
        ["Jump Peek"] = "Asomada con salto",
        ["Jiggle Peek"] = "Asomada corta",
        ["Mixed Peeks"] = "Asomadas mixtas",
        ["Vandal Spray"] = "Ráfaga Vandal",
        ["Phantom Spray"] = "Ráfaga Phantom",
        ["Spray Transfer"] = "Transferencia de ráfaga",
        ["Sens Finder"] = "Buscador de sensibilidad",
        ["Post-Plant"] = "Post-plantada",
        ["Retake"] = "Recaptura",

        // ---- drill descriptions ----
        ["3 targets on a 3x3 grid. Warm-up for speed and click timing."] =
            "3 objetivos en una cuadrícula de 3x3. Calentamiento para velocidad y precisión al hacer clic.",
        ["One real-size head (r 0.14 m). Flick, click, repeat. Higher tiers: farther, wider, timed."] =
            "Una cabeza a tamaño real (r 0,14 m). Flick, clic y repite. Niveles altos: más lejos, más ancho y con tiempo.",
        ["Centre target, then a random one. Flick out and back; higher tiers are smaller and wider."] =
            "Objetivo central y luego uno aleatorio. Flick de ida y vuelta; los niveles altos son más pequeños y amplios.",
        ["Hold Mouse 1 on a target that A-D strafes like a real player (realistic stops and reversals)."] =
            "Mantén el clic izquierdo sobre un objetivo que hace strafe A-D como un jugador real (paradas y giros realistas).",
        ["Click the moment the screen flashes. 10 trials; early clicks count as fails."] =
            "Haz clic en el instante en que la pantalla destelle. 10 intentos; los clics anticipados cuentan como fallo.",
        ["Tiny heads at 30-50 m with your rifle: make the first bullet count, then let the recoil reset before the next tap. Higher tiers add strafers and peekers."] =
            "Cabezas diminutas a 30-50 m con tu rifle: que cuente la primera bala y deja que el retroceso se recupere antes del siguiente tap. Los niveles altos añaden strafe y asomadas.",
        ["Real-size heads 1.5–6° apart at 10–20 m: small, precise corrections without overshooting. Higher tiers: farther, wider apart, timed."] =
            "Cabezas a tamaño real separadas 1,5-6° a 10-20 m: correcciones pequeñas y precisas sin pasarte. Niveles altos: más lejos, más separadas y con tiempo.",
        ["Heads pop up anywhere on screen for a split second (700 ms at Rookie, 320 ms at Pro, a little longer when far): see, flick, click."] =
            "Cabezas aparecen en cualquier parte de la pantalla por una fracción de segundo (700 ms en Novato, 320 ms en Pro, algo más si están lejos): ver, flick, clic.",
        ["Three strafing agents (rifle: 1 head or 4 body). Kill one, switch to the next - fast switches, no flicking past them."] =
            "Tres agentes en strafe (rifle: 1 cabeza o 4 cuerpo). Mata a uno y cambia al siguiente: cambios rápidos, sin pasarte de largo.",
        ["A-D strafing bots. Rifle damage: 1 head or 4 body. Kill before they leave."] =
            "Bots que hacen strafe A-D. Daño de rifle: 1 cabeza o 4 cuerpo. Mátalos antes de que se vayan.",
        ["Bots wide-swing (and jiggle at high tiers) from cover. Pre-aim at head level; kill them before they leave."] =
            "Los bots se asoman ampliamente (y hacen jiggle en niveles altos) desde cobertura. Preapunta a la altura de la cabeza; mátalos antes de que se vayan.",
        ["Bots cross a gap between cover at 22–45 m. Scope (2.5x), one-shot body, bolt action."] =
            "Los bots cruzan un hueco entre coberturas a 22-45 m. Usa la mira (2,5x), un tiro al cuerpo y cerrojo.",
        ["Try dot, cross, plus, circle and box crosshairs in real drills (hip fire, ADS or the sniper scope); get the one you see and shoot best with as a VALORANT code."] =
            "Prueba miras de punto, cruz, más, círculo y caja en ejercicios reales (cadera, ADS o mira de francotirador); obtén la que mejor ves y disparas como código de VALORANT.",
        ["Free-for-all against bots at your tier on a VALORANT map. They hunt you and shoot back; respawn and keep fighting."] =
            "Todos contra todos contra bots de tu nivel en un mapa de VALORANT. Te cazan y disparan; reaparece y sigue peleando.",
        ["Hold the site alone while 2-4 attackers execute through the choke. Hear them, hold your angle, win the duels."] =
            "Aguanta el sitio solo mientras 2-4 atacantes entran por el pasillo. Escúchalos, mantén tu ángulo y gana los duelos.",
        ["Listen for footsteps and pre-aim where the enemy will appear."] =
            "Escucha los pasos y preapunta donde aparecerá el enemigo.",
        ["Attack through a real choke on any of the six maps. Defenders hold real angles and duel you."] =
            "Ataca por un pasillo real en cualquiera de los seis mapas. Los defensores mantienen ángulos reales y te desafían.",
        ["Hold a real angle. Hear the flash, turn it off-screen (60–90°+), swing back, win the duel. Shoot Gekko's Dizzy before she spits."] =
            "Mantén un ángulo real. Escucha el flash, gira fuera de pantalla (60-90°+), vuelve y gana el duelo. Dispara a la Dizzy de Gekko antes de que escupa.",
        ["Strafe A/D. A bot appears while you run: tap the opposite key to stop, then one-tap."] =
            "Haz strafe A/D. Aparece un bot mientras corres: pulsa la tecla opuesta para parar y dale un tap.",
        ["Swing out from a corner vs a bot holding the angle. It shoots back — stop, hit first, get back."] =
            "Asómate desde una esquina contra un bot que mantiene el ángulo. Te dispara: para, acierta primero y vuelve.",
        ["An Operator holds a long angle. Jump-peek to spot him and get back before he shoots; if he misses, swing and kill him during his bolt."] =
            "Un Operator mantiene un ángulo largo. Asómate con salto para verlo y vuelve antes de que dispare; si falla, asómate y mátalo mientras recarga el cerrojo.",
        ["Shoulder-peek an Operator to bait his shot without showing your head, then wide-swing during his bolt and kill him."] =
            "Asoma el hombro contra un Operator para provocar su tiro sin mostrar la cabeza; luego asómate amplio mientras recarga y mátalo.",
        ["Hold an angle against unpredictable peeks. Shoot a bait and it punishes you; hold fire and it commits."] =
            "Mantén un ángulo contra asomadas impredecibles. Si disparas a un señuelo te castiga; si esperas, se compromete.",
        ["Recoil control with the Phantom (11 rps, 30 rounds). Keep the spray on the body; chart + tips after each spray."] =
            "Control de retroceso con la Phantom (11 disparos/s, 30 balas). Mantén la ráfaga en el cuerpo; gráfico y consejos después de cada ráfaga.",
        ["Recoil control with the Vandal (9.75 rps, 25 rounds). Pull down, counter the drift; chart + tips after each spray."] =
            "Control de retroceso con la Vandal (9,75 disparos/s, 25 balas). Tira hacia abajo y corrige la deriva; gráfico y consejos después de cada ráfaga.",
        ["Kill all the agents in one continuous rifle spray: control the recoil while dragging between targets."] =
            "Mata a todos los agentes con una ráfaga continua de rifle: controla el retroceso mientras arrastras entre objetivos.",
        ["Blind A/B test of sensitivities with flicks + tracking. Finds the sens you actually perform best with."] =
            "Prueba A/B a ciegas de sensibilidades con flicks y seguimiento. Encuentra la sensibilidad con la que realmente rindes mejor.",
        ["Blind A/B test of sensitivities with flicks, micro-adjustments and tracking. Finds the sens you perform best with, then fine-tunes it on the small corrections."] =
            "Prueba A/B a ciegas de sensibilidades con flicks, microajustes y seguimiento. Encuentra la sensibilidad con la que rindes mejor y luego la afina en las correcciones pequeñas.",
        ["Blind A/B test"] = "Prueba A/B a ciegas",
        ["The spike is down: hold your post-plant angle while defenders retake. Play the defuse, not the peek."] =
            "La spike está plantada: mantén tu ángulo post-plantada mientras los defensores recapturan. Juega el defuse, no la asomada.",

        // ---- Sens Lab ----
        ["SENS LAB"] = "SENSIBILIDAD",
        ["SENSITIVITY LAB"] = "LABORATORIO DE SENSIBILIDAD",
        ["OVER/UNDERSHOOT, TREMOR AND TRACKING · ACCUMULATED OVER TIME"] =
            "PASARSE / QUEDARSE CORTO, TEMBLOR Y TRACKING · ACUMULADO EN EL TIEMPO",
        ["WHAT WE MEASURE"] = "QUÉ MEDIMOS",
        ["HISTORY BY SENSITIVITY"] = "HISTORIAL POR SENSIBILIDAD",
        ["COACH ADVICE"] = "CONSEJO DEL COACH",
        ["NO DATA YET"] = "AÚN NO HAY DATOS",
        ["CURRENT SENSITIVITY"] = "SENSIBILIDAD ACTUAL",
        ["CURRENT VERDICT"] = "VEREDICTO ACTUAL",
        ["OVERSHOOT"] = "SE PASA",
        ["UNDERSHOOT"] = "SE QUEDA CORTO",
        ["MEAN OVERSHOOT"] = "PASADA MEDIA",
        ["TREMOR (JITTER)"] = "TEMBLOR (JITTER)",
        ["CORRECTIONS PER FLICK"] = "CORRECCIONES POR FLICK",
        ["TRACKING ON TARGET"] = "TRACKING EN OBJETIVO",
        ["CENTERED"] = "CENTRADO",
        ["SHORT"] = "CORTO",
        ["TREMOR"] = "TEMBLOR",
        ["TRACKING"] = "TRACKING",
        ["RUN SENS FINDER"] = "BUSCADOR DE SENS",
        ["AIM COACH"] = "COACH DE PUNTERÍA",

        // ---- coach / profile ----
        ["YOUR ESTIMATED RANK · WHAT HOLDS YOU BACK · HOW TO FIX IT"] =
            "TU RANGO ESTIMADO · QUÉ TE FRENA · CÓMO SOLUCIONARLO",
        ["ESTIMATED RANK · AIM ONLY"] = "RANGO ESTIMADO · SOLO PUNTERÍA",
        ["SKILL BREAKDOWN"] = "DESGLOSE DE HABILIDADES",
        ["WHAT TO FIX"] = "QUÉ CORREGIR",
        ["COACH REVIEW"] = "REVISIÓN DEL ENTRENADOR",
        ["TOP ISSUE"] = "PROBLEMA PRINCIPAL",
        ["TOP ISSUES"] = "PROBLEMAS PRINCIPALES",
        ["WHAT WENT WELL"] = "QUÉ SALIÓ BIEN",
        ["SHOW ME >"] = "MUÉSTRAME >",
        ["GET RATED"] = "OBTÉN TU RANGO",
        ["WHAT YOU DO WELL"] = "LO QUE HACES BIEN",
        ["WHY IT HAPPENS"] = "POR QUÉ PASA",
        ["HOW TO FIX"] = "CÓMO SOLUCIONARLO",
        ["PRACTISE"] = "PRACTICAR",
        ["SHOW ME"] = "MUÉSTRAME",
        ["MAJOR"] = "GRAVE",
        ["MINOR"] = "LEVE",
        ["POSSIBLE"] = "POSIBLE",
        ["UNRANKED"] = "SIN RANGO",
        ["ANALYSING YOUR RUNS…"] = "ANALIZANDO TUS PARTIDAS…",
        ["JUST NOW"] = "AHORA MISMO",
        ["TODAY"] = "HOY",
        ["NO COACHED RUNS YET"] = "AÚN NO HAY PARTIDAS ANALIZADAS",
        ["BASED ON"] = "BASADO EN",
        ["recommendation"] = "recomendación",
        ["confidence"] = "confianza",
        ["CURRENT"] = "ACTUAL",
        ["KEEP"] = "MANTÉN",
        ["KEEP FOR NOW"] = "MANTÉN POR AHORA",
        ["KEEP YOUR SENS"] = "MANTÉN TU SENS",
        ["NOT YET"] = "AÚN NO",
        ["CM / 360 VS PROS"] = "CM / 360 VS PROS",
        ["pros 33–87 cm · median 54"] = "pros 33–87 cm · mediana 54",
        ["NOW"] = "AHORA",
        ["dashed ring = your overall rank"] = "anillo discontinuo = tu rango general",
        ["centre = Iron · rim = Radiant"] = "centro = Hierro · borde = Radiante",
        ["of 8 rated"] = "de 8 valoradas",
        ["hover a skill for details"] = "pasa el ratón por una habilidad para ver detalles",
        ["found · most important first · click to expand"] = "encontrados · primero los más importantes · clic para ampliar",
        ["most likely"] = "probablemente",
        ["skills rated"] = "habilidades valoradas",
        ["Not enough data yet — the coach needs about"] = "Aún no hay suficientes datos: el coach necesita unas",
        ["more runs."] = "partidas más.",
        ["No problems found in your recent runs — solid fundamentals. Keep playing: the coach re-checks after every run."] =
            "No se han encontrado problemas en tus partidas recientes: fundamentos sólidos. Sigue jugando; el coach vuelve a analizar después de cada partida.",

        // issue titles
        ["Over-flicking: your flicks travel too far"] = "Flicks demasiado largos",
        ["Stopping short and dragging onto the target"] = "Te quedas corto",
        ["Flicks have no micro-adjustment"] = "Sin microajuste",
        ["Slow, fragmented micro-adjustments"] = "Microajustes lentos",
        ["Clicking while the crosshair is still flying"] = "Clic en movimiento",
        ["Hesitating on the click"] = "Dudas antes de hacer clic",
        ["Curved flicks"] = "Flicks curvos",
        ["Slow flick start"] = "Inicio del flick lento",
        ["Slow reaction"] = "Reacción lenta",
        ["Tracking lags behind the target"] = "El tracking se queda atrás del objetivo",
        ["Overshooting while tracking"] = "Te pasas mientras haces tracking",
        ["Shaky tracking (hand jitter)"] = "Tracking tembloroso (pulso)",
        ["Slow re-acquire after a direction change"] = "Readquisición lenta tras cambiar de dirección",
        ["Slow to re-acquire after a direction change"] = "Tardas en readquirir",
        ["Crosshair too low"] = "Mira baja",
        ["Crosshair below head level"] = "La mira está por debajo de la cabeza",
        ["Crosshair too far from the head"] = "La mira está demasiado lejos de la cabeza",
        ["Run-and-gun"] = "Disparar corriendo",
        ["Clumsy stops"] = "Paradas torpes",
        ["Vertical spray drift"] = "Deriva vertical del spray",
        ["Horizontal spray scatter"] = "Dispersión horizontal del spray",
        ["Slow flash turn"] = "Giro lento ante el flash",
        ["Late recoil compensation"] = "Compensas tarde",
        ["Inconsistent recoil control"] = "Control irregular",
        ["Spray climbs over the target"] = "El spray sube",
        ["Over-pulling your spray"] = "Tiras demasiado",
        ["On the head but waiting to click"] = "Esperas para hacer clic",
        ["Curved flicks that end under the head"] = "Flicks curvos y bajos",
        ["Flicks that end under the head"] = "Flicks bajos",
        ["Slow to start moving"] = "Tardas en arrancar",
        ["Slow reaction time"] = "Reacción lenta",
        ["Chasing the target instead of matching it"] = "Persigues al objetivo",
        ["Overshooting strafe reversals"] = "Te pasas al girar",
        ["Shaky tracking"] = "Tracking tembloroso",
        ["Not pre-aiming the angle"] = "No preapuntas",
        ["Shooting while moving"] = "Disparas en movimiento",
        ["Spray drifts sideways"] = "Spray lateral",
        ["Late flash turns"] = "Giros tardíos",
        ["Weaker flicks to the left"] = "Flicks más flojos a la izquierda",
        ["Weaker flicks to the right"] = "Flicks más flojos a la derecha",
        ["Weaker flicks upward"] = "Flicks más flojos hacia arriba",
        ["Weaker flicks downward"] = "Flicks más flojos hacia abajo",

        // issue explanations
        ["The flick is planned too long, or the arm is tense and brakes late. Undoing an overshoot costs more time than finishing a short flick: you have to stop, reverse and re-aim."] =
            "El flick se planifica demasiado largo, o el brazo está tenso y frena tarde. Deshacer un exceso cuesta más tiempo que terminar un flick corto: tienes que parar, invertir y volver a apuntar.",
        ["Hesitation, or your arm runs out of room on wide angles, so every flick ends with a slow drag onto the head."] =
            "Dudas, o tu brazo se queda sin recorrido en ángulos amplios, así que cada flick termina con un arrastre lento hasta la cabeza.",
        ["The click is chained to the flick: you fire the moment your hand stops, not when the crosshair is actually on the head."] =
            "El clic está encadenado al flick: disparas cuando se detiene la mano, no cuando la mira está realmente en la cabeza.",
        ["Many small corrections instead of one decisive one — every extra correction costs about 100–150 ms."] =
            "Muchas correcciones pequeñas en vez de una decisiva; cada corrección extra cuesta unos 100–150 ms.",
        ["Rhythm-clicking: the click is timed to the motion instead of to the crosshair being on target."] =
            "Clics por ritmo: el clic sigue al movimiento en vez de esperar a que la mira esté sobre el objetivo.",
        ["Over-checking: you confirm twice. In a real duel the enemy fires in that gap."] =
            "Compruebas de más: confirmas dos veces. En un duelo real, el enemigo dispara en ese hueco.",
        ["The wrist arcs instead of moving in a straight line, or the mouse pad sits at an angle, so flicks curve and land low."] =
            "La muñeca hace un arco en vez de moverse en línea recta, o la alfombrilla está inclinada, así que los flicks se curvan y caen bajos.",
        ["The delay is in finding the target or deciding where to go, not in your hands."] =
            "El retraso está en encontrar el objetivo o decidir hacia dónde ir, no en tus manos.",
        ["Usually fatigue or input lag rather than talent."] =
            "Normalmente es fatiga o latencia de entrada, no falta de talento.",
        ["You react to where the target was instead of matching its speed."] =
            "Reaccionas a donde estaba el objetivo en vez de igualar su velocidad.",
        ["You're anticipating the strafe instead of reacting to it."] =
            "Estás anticipando el strafe en vez de reaccionar a él.",
        ["A tight grip or fingertip-only aiming adds tremor and constant over-corrections."] =
            "Un agarre tenso o apuntar solo con las puntas de los dedos añade temblor y correcciones constantes.",
        ["After the target changes direction your correction comes late or overshoots, so you spend time off target."] =
            "Cuando el objetivo cambia de dirección, tu corrección llega tarde o se pasa, y pierdes tiempo fuera del objetivo.",
        ["You're looking at the floor or body height, so every fight starts with a vertical flick."] =
            "Miras al suelo o a la altura del cuerpo, así que cada pelea empieza con un flick vertical.",
        ["The crosshair is parked in open space instead of where enemies will appear."] =
            "La mira está aparcada en espacio abierto en vez de donde aparecerán los enemigos.",
        ["Moving bullets are wildly inaccurate in VALORANT — the first shot can land metres off."] =
            "Las balas en movimiento son muy imprecisas en VALORANT: el primer disparo puede caer a metros de distancia.",
        ["Releasing the keys and waiting to slow down wastes time every time you peek."] =
            "Soltar las teclas y esperar a frenar pierde tiempo cada vez que te asomas.",
        ["You wait for the climb before reacting to it, so the first bullets after the opener already go high."] =
            "Esperas a que el arma suba para reaccionar, así que las primeras balas tras el inicio ya se van altas.",
        ["Your pull changes from spray to spray — sometimes too little, sometimes too much — so the pattern never settles."] =
            "Tu control cambia de un spray a otro: a veces tiras poco, a veces demasiado, así que el patrón nunca se estabiliza.",
        ["You start pulling down too late or not hard enough, so the climb carries bullets 4+ over the target."] =
            "Empiezas a tirar demasiado tarde o con poca fuerza, así que la subida lleva las balas 4+ por encima del objetivo.",
        ["You pull down harder than the gun climbs, dragging the spray under the target."] =
            "Tiras hacia abajo más de lo que sube el arma y arrastras el spray por debajo del objetivo.",
        ["You're not countering the sideways drift that starts around bullet 8."] =
            "No estás compensando la deriva lateral que empieza hacia la bala 8.",
        ["Waiting to see the flash instead of reacting to the throw sound."] =
            "Esperas a ver el flash en vez de reaccionar al sonido del lanzamiento.",
        ["The arm has less room on one side (the mouse sits off-centre or the pad is too small), so the same flick is planned differently depending on where the target appears."] =
            "El brazo tiene menos recorrido hacia un lado (el ratón está descentrado o la alfombrilla es pequeña), así que el mismo flick se planifica distinto según dónde aparezca el objetivo.",

        // issue fixes
        ["Aim to land about 90% of the way there and finish with one small micro-adjustment."] =
            "Apunta a llegar al 90% del recorrido y termina con un pequeño microajuste.",
        ["Brake early: the second half of the flick should feel like slowing down, not pushing."] =
            "Frena antes: la segunda mitad del flick debe sentirse como desacelerar, no como empujar.",
        ["Relax your grip and forearm — a tense arm overshoots and can't stop cleanly."] =
            "Relaja el agarre y el antebrazo: un brazo tenso se pasa y no frena limpio.",
        ["Play Head Flicks one tier lower until most flicks land on or just short of the head."] =
            "Juega Flicks a la Cabeza un nivel más bajo hasta que la mayoría caigan en la cabeza o justo antes.",
        ["Commit to one full motion that reaches the head; keep the micro-adjustment tiny."] =
            "Comprométete con un movimiento completo hasta la cabeza; mantén el microajuste mínimo.",
        ["Use your arm, not just the wrist, for wide flicks so the first motion covers the whole distance."] =
            "Usa el brazo, no solo la muñeca, en flicks amplios para que el primer movimiento cubra toda la distancia.",
        ["Look at the target first, then move — the eyes lead the hand."] =
            "Mira primero al objetivo y luego muévete: los ojos guían la mano.",
        ["Think \"flick, confirm, click\" — a split-second check before every shot."] =
            "Piensa «flick, confirma, clic»: una comprobación de una fracción de segundo antes de cada disparo.",
        ["If the flick lands off, make one small correction instead of firing anyway."] =
            "Si el flick cae desviado, haz una corrección pequeña en vez de disparar igualmente.",
        ["Play Head Flicks at Rookie (untimed) until you reach 85% accuracy, then speed up."] =
            "Juega Flicks a la Cabeza en Principiante (sin tiempo) hasta llegar al 85% de precisión, luego acelera.",
        ["Make one deliberate correction with your fingers and wrist, then click."] =
            "Haz una corrección deliberada con los dedos y la muñeca, y luego haz clic.",
        ["Keep the hand loose after the flick so the correction is quick and small."] =
            "Mantén la mano suelta tras el flick para que la corrección sea rápida y pequeña.",
        ["The head is bigger than its centre: click once you're on it, don't chase the exact middle."] =
            "La cabeza es más grande que su centro: haz clic cuando estés en ella, no persigas el medio exacto.",
        ["Stop, then shoot — the crosshair should be still when you click."] =
            "Detente y luego dispara: la mira debe estar quieta al hacer clic.",
        ["Slow your flicks down about 10% until your accuracy recovers."] =
            "Baja la velocidad de tus flicks un 10% hasta que vuelva la precisión.",
        ["Say \"stop-click\" in your head for a few sessions to break the rhythm."] =
            "Repite «para, clic» en tu cabeza durante varias sesiones para romper el ritmo.",
        ["Fire as soon as the crosshair touches the head."] =
            "Dispara en cuanto la mira toque la cabeza.",
        ["Trust the first confirmation — speed up the click, not the flick."] =
            "Confía en la primera confirmación: acelera el clic, no el flick.",
        ["Play Gridshot focusing only on clicking the instant you arrive."] =
            "Juega Gridshot centrándote solo en hacer clic en el instante en que llegas.",
        ["Straighten the motion: move along the line to the target instead of pivoting around the wrist."] =
            "Endereza el movimiento: muévete en línea hacia el objetivo en vez de girar alrededor de la muñeca.",
        ["Line your mouse pad up with the monitor and sit square to it."] =
            "Alinea la alfombrilla con el monitor y siéntate de frente a él.",
        ["Level the crosshair at head height first, then flick sideways along that line."] =
            "Nivela primero la mira a la altura de la cabeza y luego haz el flick lateral sobre esa línea.",
        ["Keep your eyes at the centre of the screen and use peripheral vision to spot targets."] =
            "Mantén los ojos en el centro de la pantalla y usa la visión periférica para detectar objetivos.",
        ["Start moving the moment you see colour — refine the exact spot while already moving."] =
            "Empieza a moverte en cuanto veas color y ajusta el punto exacto mientras ya te mueves.",
        ["Warm up with the Reaction Test, then Gridshot."] =
            "Calienta con la Prueba de Reacción y luego Gridshot.",
        ["Turn V-Sync off and check your FPS cap and mouse polling rate."] =
            "Desactiva V-Sync y revisa el límite de FPS y la frecuencia del ratón.",
        ["Warm up for 5 minutes before playing ranked."] =
            "Calienta 5 minutos antes de jugar competitivo.",
        ["Test again when rested — tiredness costs 20–40 ms."] =
            "Vuelve a medir descansado: el cansancio cuesta 20–40 ms.",
        ["Match the target's speed first, then fine-tune the position."] =
            "Iguala primero la velocidad del objetivo y luego ajusta la posición.",
        ["Ride the leading edge of the target, not its centre."] =
            "Sigue el borde delantero del objetivo, no su centro.",
        ["Play Strafe Tracking one tier lower until you stay on target 50%+."] =
            "Juega Strafe Tracking un nivel más bajo hasta mantenerte en el objetivo el 50% o más.",
        ["React to the stop — watch the body, not where you expect it to go."] =
            "Reacciona a la parada: mira el cuerpo, no donde esperas que vaya.",
        ["Ease off as the target slows down; don't keep pushing through."] =
            "Suelta a medida que el objetivo frena; no sigas empujando.",
        ["Play Strafe Tracking at Veteran or higher to practise reversals."] =
            "Juega Strafe Tracking en Veterano o superior para practicar los cambios de dirección.",
        ["Relax your grip — hold the mouse, don't squeeze it."] =
            "Relaja el agarre: sujeta el ratón, no lo aprietes.",
        ["Track with your arm and wrist; keep the fingers quiet."] =
            "Haz tracking con el brazo y la muñeca; mantén los dedos quietos.",
        ["Smooth beats fast: let small errors go instead of chasing them."] =
            "Lo suave gana a lo rápido: deja pasar los errores pequeños en vez de perseguirlos.",
        ["Watch the body for the moment it plants, then reverse immediately."] =
            "Mira el cuerpo para ver cuándo se frena e invierte inmediatamente.",
        ["Make the reversal one quick, small adjustment instead of a big swing."] =
            "Haz el cambio de dirección con un ajuste rápido y pequeño, no con un giro amplio.",
        ["Hold a head-height line and move the crosshair along it."] =
            "Mantén una línea a la altura de la cabeza y mueve la mira a lo largo de ella.",
        ["Use map details at head height (door frames, box edges) as reference points."] =
            "Usa detalles del mapa a la altura de la cabeza (marcos de puerta, bordes de cajas) como referencias.",
        ["After every kill, reset the crosshair to head level — not down."] =
            "Después de cada baja, vuelve a poner la mira a la altura de la cabeza, no hacia abajo.",
        ["Sit just off the cover edge where they'll appear."] =
            "Colócate justo al borde de la cobertura por donde aparecerán.",
        ["Clear one angle at a time, keeping the crosshair glued to the edge."] =
            "Limpia un ángulo cada vez, manteniendo la mira pegada al borde.",
        ["When you move, slide the crosshair along the wall edge with you."] =
            "Cuando te muevas, desliza la mira por el borde de la pared contigo.",
        ["Stop, then shoot: counter-strafe and click once you're still."] =
            "Detente y dispara: haz contramovimiento y haz clic cuando estés quieto.",
        ["Don't hold a movement key while firing — tap the opposite key, then click."] =
            "No mantengas una tecla de movimiento al disparar: toca la tecla opuesta y luego haz clic.",
        ["Tap the opposite key for about 80 ms, then release both."] =
            "Pulsa la tecla opuesta unos 80 ms y luego suelta ambas.",
        ["Drill the rhythm: strafe, tap, shoot, strafe."] =
            "Entrena el ritmo: strafe, toque, disparo, strafe.",
        ["Start pulling down by bullet 2–3, steadily — not one big late drag."] =
            "Empieza a tirar hacia abajo en la bala 2–3, de forma constante, no con un gran arrastre tardío.",
        ["Pull at a constant speed for the first second; the Vandal climbs about 5°/s."] =
            "Tira a velocidad constante durante el primer segundo; la Vandal sube unos 5°/s.",
        ["Watch the chart after each spray and match the grey pattern in reverse."] =
            "Mira el gráfico después de cada spray e imita el patrón gris a la inversa.",
        ["Use the same pull every time: start by bullet 2–3 and keep a steady speed."] =
            "Usa el mismo control siempre: empieza en la bala 2–3 y mantén una velocidad constante.",
        ["Centre the mouse on the pad and give yourself the same room to both sides."] =
            "Centra el ratón en la alfombrilla y darte el mismo recorrido hacia ambos lados.",
        ["Turn the keyboard slightly so the arm sweeps freely in both directions."] =
            "Gira el teclado un poco para que el brazo barra con libertad en ambas direcciones.",
        ["Practise the weak side deliberately: overshoot it by a little, then settle back."] =
            "Entrena el lado flojo a propósito: pásate un poco y luego vuelve al objetivo.",
        ["Keep the wrist loose on the cramped side instead of steering with the fingertips."] =
            "Mantén la muñeca suelta en el lado con menos espacio en vez de guiar con las puntas de los dedos.",
        ["Rest the crosshair on the target before each spray so every spray starts the same way."] =
            "Deja la mira sobre el objetivo antes de cada spray para que todos empiecen igual.",
        ["Pull less: match the climb, don't beat it."] =
            "Tira menos: iguala la subida, no la superes.",
        ["Ease off the pull after bullet 10 — the climb flattens out."] =
            "Suelta el control después de la bala 10: la subida se aplana.",
        ["From bullet 8 on, watch which way the spray drifts and pull the opposite way."] =
            "Desde la bala 8, observa hacia dónde deriva el spray y tira en sentido contrario.",
        ["Keep the counter-pull smooth — sudden sideways jerks make it worse."] =
            "Mantén el contracontrol suave: los tirones laterales bruscos lo empeoran.",
        ["Turn on the audio cue — every agent's flash has a distinct sound."] =
            "Activa la señal de audio: el flash de cada agente tiene un sonido distinto.",
        ["Turn at least 90° away, then swing back right after the pop."] =
            "Gírate al menos 90° y vuelve justo después de la explosión.",

        // issue evidence fragments
        ["Only"] = "Solo",
        ["of stops were counter-strafed"] = "de las paradas fueron counter-strafe",
        ["None of your stops were counter-strafed"] = "Ninguna de tus paradas fue counter-strafe",
        ["average stop"] = "parada media",
        ["ms to accurate speed (best ≈ 105)"] = "ms hasta velocidad precisa (mejor ≈ 105)",
        ["to reacquire after changing direction"] = "en readquirir tras cambiar de dirección",
        ["It took"] = "Tardaste",
        ["to get back on after a direction change"] = "en volver al objetivo tras cambiar de dirección",
        ["you needed"] = "necesitaste",
        ["to get back on after each direction change"] = "para volver al objetivo tras cada cambio de dirección",
        ["typical for"] = "típico en",
        ["direction changes"] = "cambios de dirección",
        ["Releasing the keys and waiting to slow down wastes time every time you peek."] =
            "Soltar las teclas y esperar a frenar pierde tiempo cada vez que te asomas.",
        ["Tap the opposite key for about 80 ms, then release both."] =
            "Pulsa la tecla opuesta unos 80 ms y luego suelta ambas.",
        ["Drill the rhythm: strafe, tap, shoot, strafe."] =
            "Entrena el ritmo: strafe, toque, disparo, strafe.",
        ["After the target changes direction your correction comes late or overshoots, so you spend time off target."] =
            "Cuando el objetivo cambia de dirección, tu corrección llega tarde o se pasa, y pierdes tiempo fuera del objetivo.",
        ["Watch the body for the moment it plants, then reverse immediately."] =
            "Mira el cuerpo para ver cuándo se frena e invierte inmediatamente.",
        ["Make the reversal one quick, small adjustment instead of a big swing."] =
            "Haz el cambio de dirección con un ajuste rápido y pequeño, no con un giro amplio.",
        ["of your off-target flicks ended past the target, by"] = "de tus flicks fallados se pasaron del objetivo, por",
        ["of the distance"] = "de la distancia",
        ["Your flicks covered"] = "Tus flicks recorrieron",
        ["of the off-target ones stopped short"] = "de los fallados se quedaron cortos",
        ["on average"] = "de media",
        ["of your misses were fired straight off the flick"] = "de tus fallos se dispararon directamente al terminar el flick",
        ["from the target, with no correction"] = "del objetivo, sin corrección",
        ["with no correction"] = "sin corrección",
        ["After the flick you needed"] = "Después del flick necesitaste",
        ["to settle"] = "para asentarte",
        ["of your misses were fired while the crosshair was still moving"] = "de tus fallos se dispararon mientras la mira seguía moviéndose",
        ["before you stopped"] = "antes de parar",
        ["of your sprays"] = "de tus sprays",
        ["started pulling down"] = "empezaste a tirar hacia abajo",
        ["into the spray"] = "dentro del spray",
        ["into each spray"] = "en cada spray",
        ["Bullets 4–15"] = "Balas 4–15",
        ["strayed"] = "se desviaron",
        ["vertically"] = "verticalmente",
        ["too high in some sprays, too low in others"] = "demasiado altas en unos sprays y demasiado bajas en otros",
        ["landed"] = "aterrizaron",
        ["stops"] = "paradas",
        ["misses"] = "fallos",
        ["hits"] = "aciertos",
        ["off-target flicks"] = "flicks fallados",
        ["Sensitivity:"] = "Sensibilidad:",
        ["NOT RATED"] = "SIN VALORAR",
        ["RATED"] = "VALORADO",
        ["Not rated yet"] = "Aún sin valorar",
        ["HIGH CONFIDENCE"] = "CONFIANZA ALTA",
        ["MEDIUM CONFIDENCE"] = "CONFIANZA MEDIA",
        ["LOW CONFIDENCE"] = "CONFIANZA BAJA",
        ["CONFIDENCE"] = "CONFIANZA",
        ["samples"] = "muestras",
        ["click to play"] = "clic para jugar",
        ["IRON"] = "HIERRO",
        ["BRONZE"] = "BRONCE",
        ["SILVER"] = "PLATA",
        ["GOLD"] = "ORO",
        ["PLATINUM"] = "PLATINO",
        ["DIAMOND"] = "DIAMANTE",
        ["ASCENDANT"] = "ASCENDENTE",
        ["IMMORTAL"] = "INMORTAL",
        ["RADIANT"] = "RADIANTE",
        ["Flicking"] = "Flick",
        ["Precision"] = "Precisión",
        ["Reaction"] = "Reacción",
        ["Listen…"] = "Escucha…",
        ["Eyes on the centre"] = "Ojos en el centro",
        ["Green = click · Red = wait"] = "Verde = clic · Rojo = espera",
        ["Get ready to flick"] = "Prepárate para el flick",
        ["Wait for it…"] = "Espera…",
        ["Too early!"] = "¡Demasiado pronto!",
        ["Crosshair placement"] = "Colocación de mira",
        ["Movement"] = "Movimiento",
        ["Utility"] = "Utilidad",
        ["Spray control"] = "Control de spray",
        ["PLACEMENT"] = "COLOCACIÓN",
        ["NO CLEAR PROBLEMS THIS RUN"] = "SIN PROBLEMAS CLAROS EN ESTA PARTIDA",
        ["WHAT TO FIX NEXT RUN"] = "QUÉ CORREGIR EN LA PRÓXIMA",
        ["Not enough data this run for a review - play at least 15 seconds and hit a few targets."] =
            "No hay suficientes datos en esta partida para una revisión: juega al menos 15 segundos y acierta algunos objetivos.",
        ["Not enough data this run for a review — play at least 15 seconds and hit a few targets."] =
            "No hay suficientes datos en esta partida para una revisión: juega al menos 15 segundos y acierta algunos objetivos.",
        ["PLAYED AT"] = "JUGADO EN",
        ["PERFORMED AT"] = "RENDIMIENTO EN",
        ["FULL COACH PROFILE  ›"] = "PERFIL DEL COACH  ›",
        ["FULL COACH PROFILE"] = "PERFIL DEL COACH",

        // ---- UI sweep: labels and sentences that used to fall through to the word-by-word table ----
        ["DONE"] = "HECHO",
        ["SKIP"] = "SALTAR",
        ["GOT IT"] = "ENTENDIDO",
        ["Close"] = "Cerrar",
        ["PICK"] = "ELIGE",
        ["PICKED"] = "ELEGIDA",
        ["PLAYER"] = "JUGADOR",
        ["MATCHES"] = "PARTIDAS",
        ["RESULT"] = "RESULTADO",
        ["ROUTINE"] = "RUTINA",
        ["CAREER"] = "CARRERA",
        ["PLAYLISTS"] = "LISTAS",
        ["CATEGORY"] = "CATEGORÍA",
        ["TIMELINE"] = "CRONOLOGÍA",
        ["USUAL"] = "HABITUAL",
        ["PASSIVE"] = "PASIVA",
        ["TRAINED BY"] = "ENTRENADO POR",
        ["NO DATA"] = "SIN DATOS",
        ["NO TARGET"] = "SIN OBJETIVO",
        ["NO FRAMES RECORDED"] = "SIN FOTOGRAMAS GRABADOS",
        ["THIS RUN"] = "ESTA PARTIDA",
        ["THIS ENGAGEMENT"] = "ESTE ENCUENTRO",
        ["this engagement"] = "este encuentro",
        // ---- Rewind review: frame overlays, engagement tiles and the flick rose ----
        ["PLAYER'S VIEW · 1×"] = "VISTA DEL JUGADOR · 1×",
        ["CLOSE-UP · 4×"] = "PRIMER PLANO · 4×",
        ["FIRST SHOT"] = "PRIMER DISPARO",
        ["FIRST BULLET"] = "PRIMERA BALA",
        ["WHILE MOVING"] = "EN MOVIMIENTO",
        ["SHOTS WHILE MOVING"] = "DISPAROS EN MOVIMIENTO",
        ["ALL SHOTS"] = "TODOS LOS DISPAROS",
        ["TYPICAL"] = "HABITUAL",
        ["no shot"] = "sin disparo",
        ["SETTLE"] = "AJUSTE",
        ["mostly overshoot"] = "casi siempre se pasa",
        ["mostly short"] = "casi siempre se queda corto",
        ["about right"] = "en su punto",
        ["No target at this point of the run."] = "No hay objetivo en este punto de la partida.",
        ["EASIER"] = "MÁS FÁCIL",
        ["LEVEL"] = "NIVEL",
        ["SENS"] = "SENS",
        ["VIEW"] = "VISTA",
        ["IN"] = "DENTRO",
        ["OUT"] = "FUERA",
        ["DETAILS  (D)"] = "DETALLES  (D)",
        ["JOIN DISCORD"] = "UNIRSE A DISCORD",
        ["SEND"] = "ENVIAR",
        ["SENDING."] = "ENVIANDO.",
        ["SENT"] = "ENVIADO",
        ["WHAT GETS SENT"] = "QUÉ SE ENVÍA",
        ["COPY REPORT & OPEN DISCORD"] = "COPIAR REPORTE Y ABRIR DISCORD",
        ["COPY IDEA & OPEN DISCORD"] = "COPIAR IDEA Y ABRIR DISCORD",
        ["LAUNCH VALORANT"] = "ABRIR VALORANT",
        ["STARTING VALORANT."] = "ABRIENDO VALORANT.",
        ["Starts VALORANT through the Riot Client"] = "Abre VALORANT a través del Riot Client",
        ["BREATHING GUIDE"] = "GUÍA DE RESPIRACIÓN",
        ["4 seconds in, 6 seconds out."] = "4 segundos dentro, 6 segundos fuera.",
        ["LOCK IN · BREATHE"] = "LOCK-IN · RESPIRA",
        ["LOCK IN AGAIN"] = "LOCK-IN OTRA VEZ",
        ["LOCK-IN DETAILS"] = "DETALLES DEL LOCK-IN",
        ["LOCK-IN GRAPH"] = "GRÁFICA DEL LOCK-IN",
        ["LOCK-IN TIMELINE (PLAY TIME)"] = "CRONOLOGÍA DEL LOCK-IN (TIEMPO JUGADO)",
        ["CONTINUE  (SPACE)"] = "CONTINUAR  (ESPACIO)",
        ["ESC PAUSES · IN A DRILL, ESC → BACK TO MENU ENDS THE LOCK-IN"] =
            "ESC PAUSA · EN UN EJERCICIO, ESC → MENÚ TERMINA EL LOCK-IN",
        ["PAUSED · SPACE TO START · ESC AGAIN TO QUIT THE LOCK-IN"] =
            "EN PAUSA · ESPACIO PARA EMPEZAR · ESC OTRA VEZ PARA SALIR DEL LOCK-IN",
        ["YOUR CALL · SPACE STARTS THE NEXT DRILL"] = "TU TURNO · ESPACIO EMPIEZA EL SIGUIENTE EJERCICIO",
        ["VS YOUR USUAL · LAST 20"] = "FRENTE A TU HABITUAL · ÚLTIMAS 20",
        ["PERFORMANCE PER DRILL · % OF YOUR USUAL"] = "RENDIMIENTO POR EJERCICIO · % DE TU HABITUAL",
        ["WHAT WORKS FOR YOU"] = "LO QUE TE FUNCIONA",
        ["WHY THIS WORKS"] = "POR QUÉ FUNCIONA",
        ["Finish a Lock-In to see how each one compares with your usual."] =
            "Termina un Lock-In para ver cómo se compara cada uno con tu habitual.",
        ["No study has tested an aim-trainer warm-up on ranked results yet."] =
            "Ningún estudio ha medido todavía un calentamiento con aim trainer en resultados de ranked.",
        ["Space starts · a 30-second check-in comes first · Esc pauses a drill"] =
            "Espacio empieza · antes va un check-in de 30 segundos · Esc pausa un ejercicio",
        ["same setup as your match · nothing here is required"] =
            "misma configuración que en tu partida · nada de esto es obligatorio",
        ["SETUP CHECK"] = "REVISIÓN DE CONFIGURACIÓN",
        ["The lock-in graph and every drill's result"] = "La gráfica del lock-in y el resultado de cada ejercicio",
        ["YOUR GOAL"] = "TU OBJETIVO",
        ["YOUR CUE"] = "TU SEÑAL",
        ["EVERY BUY PHASE"] = "CADA FASE DE COMPRA",
        ["A small personal sample, not proof."] = "Una muestra personal pequeña, no una prueba.",
        ["A match depends on much more than a warm-up."] = "Una partida depende de mucho más que un calentamiento.",
        ["TILTED OR ANNOYED"] = "TILTADO O MOLESTO",
        ["FELT LOCKED IN"] = "TE SENTISTE CONECTADO",
        ["FELT LOCKED IN?"] = "¿TE SENTISTE CONECTADO?",
        ["WON"] = "GANADA",
        ["none yet"] = "ninguna todavía",
        ["CALIBRATION TODAY   "] = "CALIBRACIÓN HOY   ",
        ["BACK TO YOUR REAL SENS FROM HERE ON"] = "VUELTA A TU SENSIBILIDAD REAL DESDE AQUÍ",
        ["SKIP  (SPACE)"] = "SALTAR  (ESPACIO)",
        ["SKIP DRILL"] = "SALTAR EJERCICIO",
        ["FINISH NOW"] = "TERMINAR AHORA",
        ["START NOW  (SPACE)"] = "EMPEZAR AHORA  (ESPACIO)",
        ["YOUR LOCK-INS"] = "TUS LOCK-INS",
        ["AGENT LOCK-IN"] = "LOCK-IN DE AGENTE",
        ["SAME LOCK-IN"] = "MISMO LOCK-IN",
        ["AGENT TRAINING"] = "ENTRENAMIENTO DE AGENTES",
        ["CHOOSE AN AGENT"] = "ELIGE UN AGENTE",
        ["CHOOSE A MAP"] = "ELIGE UN MAPA",
        ["No drill yet"] = "Todavía sin ejercicio",
        ["SIGNATURE DRILLS WITH EACH AGENT'S OWN UTILITY · ROLE DRILLS · A 10-MINUTE AGENT LOCK-IN"] =
            "EJERCICIOS DE HABILIDADES CON LA UTILIDAD DE CADA AGENTE · EJERCICIOS POR ROL · UN LOCK-IN DE AGENTE DE 10 MINUTOS",
        ["PLAY BY PLAY"] = "JUGADA A JUGADA",
        ["OPEN LAST RUN"] = "ABRIR ÚLTIMA PARTIDA",
        ["UNLOCK YOUR OVERALL RANK"] = "DESBLOQUEA TU RANGO GENERAL",
        ["SEARCHING."] = "BUSCANDO.",
        ["SEARCHING…"] = "BUSCANDO…",
        ["SENDING…"] = "ENVIANDO…",
        ["LOADING RUN."] = "CARGANDO PARTIDA.",
        ["LOADING RUN…"] = "CARGANDO PARTIDA…",
        ["LOADING."] = "CARGANDO.",
        ["LOADING…"] = "CARGANDO…",
        ["Analysing."] = "Analizando.",
        ["Analysing…"] = "Analizando…",
        ["STARTING VALORANT…"] = "ABRIENDO VALORANT…",
        ["UPDATING CA-VAL-AIM"] = "ACTUALIZANDO CA-VAL-AIM",
        ["Your settings and stats are kept."] = "Tus ajustes y estadísticas se conservan.",
        ["Blind A/B test of sensitivities with flicks + tracking (about 15 minutes)"] =
            "Prueba ciega A/B de sensibilidades con flicks + tracking (unos 15 minutos)",
        ["An aim-only estimate: CA-VAL-AIM rates your mechanics (aim, movement, flash dodging).\nYour in-game rank also depends on game sense, comms and utility."] =
            "Estimación solo de puntería: CA-VAL-AIM valora tu mecánica (puntería, movimiento, esquiva de flashes).\nTu rango en partida también depende de la lectura, las comunicaciones y la utilidad.",
        ["Overshoot, undershoot, tremor and tracking evidence accumulated per sensitivity. CA-VAL-AIM can't change Valorant's settings — set it in Valorant: Settings → General → Mouse → Sensitivity."] =
            "Evidencia de pasarse, quedarse corto, temblor y tracking acumulada por sensibilidad. CA-VAL-AIM no puede cambiar los ajustes de VALORANT: cámbiala en VALORANT → Ajustes → General → Ratón → Sensibilidad.",
        ["Replay a moment from your recent runs where this happened"] =
            "Reproduce un momento de tus partidas recientes en el que pasó esto",
        ["The VALORANT code of this crosshair: paste it in VALORANT → Settings → Crosshair → Import Profile Code"] =
            "El código de VALORANT de esta mira: pégalo en VALORANT → Ajustes → Mira → Importar código de perfil",
        ["Copy, then paste it in VALORANT: Settings → Crosshair → Import Profile Code"] =
            "Cópialo y pégalo en VALORANT: Ajustes → Mira → Importar código de perfil",
        ["Uses the crosshair code on your clipboard in ValTrainer (Crosshair → CUSTOM). VALORANT isn't changed."] =
            "Usa en ValTrainer el código de mira de tu portapapeles (Mira → PERSONALIZADA). VALORANT no cambia.",
        ["Version, renderer, GPU and folders, for a bug report"] =
            "Versión, renderizador, GPU y carpetas, para un reporte de fallo",
        ["Weapon for every drill · click for the full arsenal"] =
            "Arma para cada ejercicio · clic para ver todo el arsenal",
        ["Free-for-all against bots on the selected map, with the difficulty, lobby and length above"] =
            "Todos contra todos con bots en el mapa elegido, con la dificultad, el lobby y la duración de arriba",
        ["Lock-In: about 12 minutes before ranked. A check-in, aim drills that adapt to you, a deathmatch, then your goal and cue"] =
            "Lock-In: unos 12 minutos antes de ranked. Un check-in, ejercicios de puntería adaptados a ti, un deathmatch y tu objetivo y señal.",
        ["Deletes every Rewind recording. Your stats, scores and coach results are kept."] =
            "Borra todas las grabaciones de Rewind. Tus estadísticas, puntuaciones y resultados del coach se conservan.",
        ["ALL RELEASES ON GITHUB"] = "TODAS LAS VERSIONES EN GITHUB",
        ["What's new"] = "Novedades",
        ["Updates"] = "Actualizaciones",
        ["Opens the releases page in your browser"] = "Abre la página de versiones en tu navegador",
        ["Opens the release notes in your browser"] = "Abre las notas de la versión en tu navegador",
        ["Opens the Crosshair Finder (leaves this screen)"] = "Abre el Buscador de mira (sale de esta pantalla)",
        ["Opens the CA-VAL-AIM Discord invite in your browser"] =
            "Abre la invitación de Discord de CA-VAL-AIM en tu navegador",
        ["Opens GitHub's issue form in your browser instead (needs a GitHub account; attach godot.log there)"] =
            "Abre el formulario de incidencias de GitHub en tu navegador (necesita cuenta de GitHub; adjunta allí godot.log)",
        ["Check GitHub for new versions?"] = "¿Buscar versiones nuevas en GitHub?",
        ["Check GitHub for new versions and download them in the background"] =
            "Buscar versiones nuevas en GitHub y descargarlas en segundo plano",
        ["ENABLE UPDATES"] = "ACTIVAR ACTUALIZACIONES",
        ["NOT NOW"] = "AHORA NO",
        ["ask github.com for the latest CA-VAL-AIM release, at startup and every 6 hours"] =
            "preguntar a github.com por la última versión de CA-VAL-AIM, al arrancar y cada 6 horas",
        ["download a newer version in the background and install it when you restart CA-VAL-AIM"] =
            "descargar una versión nueva en segundo plano e instalarla al reiniciar CA-VAL-AIM",
        ["send nothing about you: no settings, stats, recordings or IDs (GitHub sees an ordinary web request)"] =
            "no enviar nada sobre ti: ni ajustes, ni estadísticas, ni grabaciones, ni identificadores (GitHub ve una petición web normal)",
        ["CA-VAL-AIM can keep itself up to date. If you turn this on, it will:"] =
            "CA-VAL-AIM puede mantenerse actualizado por su cuenta. Si lo activas, hará lo siguiente:",
        ["Until you say yes, CA-VAL-AIM makes no network requests. You can change this any time in Settings → About, where CHECK NOW also checks once."] =
            "Hasta que digas que sí, CA-VAL-AIM no hace ninguna petición de red. Puedes cambiarlo cuando quieras en Ajustes → Acerca de, donde COMPROBAR AHORA también comprueba una vez.",
        ["No update checks; you can turn them on later in Settings → About"] =
            "Sin búsqueda de actualizaciones; puedes activarla luego en Ajustes → Acerca de",
        ["Downloads, verifies and installs the update"] = "Descarga, verifica e instala la actualización",
        ["Keeps training now; the update stays in the strip under the header"] =
            "Sigue entrenando; la actualización queda en la franja bajo la cabecera",
        ["LATER"] = "MÁS TARDE",
        ["HIDE"] = "OCULTAR",
        ["Installs the update and restarts CA-VAL-AIM. CA-VAL-AIM is installed for all users, so Windows asks for permission"] =
            "Instala la actualización y reinicia CA-VAL-AIM. CA-VAL-AIM está instalado para todos los usuarios, así que Windows pedirá permiso",
        ["Installs the update and restarts CA-VAL-AIM (takes a few seconds; settings and stats are kept)"] =
            "Instala la actualización y reinicia CA-VAL-AIM (tarda unos segundos; los ajustes y las estadísticas se conservan)",
        ["Installs the next time you start CA-VAL-AIM"] =
            "Instala la actualización la próxima vez que inicies CA-VAL-AIM",
        ["Hide until next launch"] = "Ocultar hasta el próximo arranque",
        ["Downloads the update, verifies it against SHA256SUMS.txt and offers to restart"] =
            "Descarga la actualización, la verifica con SHA256SUMS.txt y ofrece reiniciar",
        ["No download page is set for this build"] = "Esta compilación no tiene página de descarga",

        // ---- rewind / results ----
        ["No shots or flicks were recorded in this run."] = "No se grabaron disparos ni flicks en esta partida.",
        ["MEDIAN TIME TO HIT"] = "TIEMPO MEDIANO HASTA IMPACTAR",
        ["length = how many flicks"] = "longitud = cuántos flicks",
        ["% = past (+) or short (-)"] = "% = pasado (+) o corto (-)",
        ["% = past (+) or short (−)"] = "% = pasado (+) o corto (−)",
        ["  ·  FIRING"] = "  ·  DISPARANDO",
        ["PLAYS WHEN READY · PRESS V FOR 2D NOW"] = "REPRODUCE AL CARGAR · PULSA V PARA 2D",

        // ---- crosshair finder / sens finder ----
        ["CROSSHAIR FINDER"] = "BUSCADOR DE MIRA",
        ["VISIBILITY + SHAPE TEST · VALORANT CODE AT THE END"] = "PRUEBA DE VISIBILIDAD Y FORMA · CÓDIGO DE VALORANT AL FINAL",
        ["VISIBILITY"] = "VISIBILIDAD",
        ["DOT SIZE"] = "TAMAÑO DEL PUNTO",
        ["PART 1 · HOW FAST YOU SPOTTED EACH COLOUR"] = "PARTE 1 · QUÉ RÁPIDO DISTINGUISTE CADA COLOR",
        ["TIME TO SPOT (SHORTER = BETTER) · CONTRAST (LONGER = BETTER)"] =
            "TIEMPO EN VERLA (MENOS = MEJOR) · CONTRASTE (MÁS = MEJOR)",
        ["×3 · REAL SIZE ON HAVEN, SPLIT, AN AGENT"] = "×3 · TAMAÑO REAL EN HAVEN, SPLIT, UN AGENTE",
        ["PART 1 RESULT · MOST VISIBLE FOR YOU"] = "RESULTADO DE LA PARTE 1 · LA MÁS VISIBLE PARA TI",
        ["  (yours)"] = "  (tuya)",
        ["WHICH CROSSHAIR? SHOOT ONE TO START"] = "¿QUÉ MIRA? DISPARA A UNA PARA EMPEZAR",
        ["CLICK TO START"] = "HAZ CLIC PARA EMPEZAR",
        ["CLICK TO CONTINUE"] = "HAZ CLIC PARA CONTINUAR",
        ["SHOOT TO START"] = "DISPARA PARA EMPEZAR",
        ["YOURS NOW"] = "AHORA ES TUYA",
        ["FINAL CHECK"] = "COMPROBACIÓN FINAL",
        ["PART 2 · HEAD-TO-HEAD (AIM SCORE, THEN YOUR PICK)"] =
            "PARTE 2 · CARA A CARA (PUNTUACIÓN DE PUNTERÍA Y LUEGO TU ELECCIÓN)",
        ["Shoot A, SAME or B. Your aim decides; this breaks ties."] =
            "Dispara a A, IGUAL o B. Tu puntería decide; esto rompe empates.",
        ["WHICH FELT BETTER?"] = "¿CUÁL SE SINTIÓ MEJOR?",
        ["START BRACKET"] = "EMPEZAR EL CUADRO",
        ["SEMI-FINAL"] = "SEMIFINAL",
        ["SEMI-FINAL 1"] = "SEMIFINAL 1",
        ["SEMI-FINAL 2"] = "SEMIFINAL 2",
        ["WIN · "] = "GANADOR · ",
        ["YOUR CROSSHAIR AS A VALORANT CODE"] = "TU MIRA COMO CÓDIGO DE VALORANT",
        ["×8 ZOOM · THEN REAL SIZE ON EACH MAP"] = "ZOOM ×8 · LUEGO TAMAÑO REAL EN CADA MAPA",
        ["NEXT ROUND"] = "SIGUIENTE RONDA",
        ["WHAT WON EACH STEP (HIGHER SENS →)"] = "QUÉ GANÓ EN CADA PASO (SENSIBILIDAD MÁS ALTA →)",
        ["SENS FINDER"] = "BUSCADOR DE SENSIBILIDAD",
        ["Below Rookie"] = "Por debajo de Novato",

        // ---- in-game HUD / agent drills ----
        ["BAITED — WIDE SWING NOW"] = "CEBADO — ASÓMATE AHORA",
        ["CLEAN PEEK — he couldn't get the shot off"] = "ASOMADA LIMPIA: no pudo disparar",
        ["HE HELD FIRE — you hid too early: stay out a touch longer"] =
            "NO DISPARÓ: te escondiste demasiado pronto, aguanta un poco más",
        ["KILL — but he wasn't baited (risky)"] = "BAJA, pero no picó (arriesgado)",
        ["LEG SHOT — 120 damage. Shorter peeks!"] = "DISPARO A LA PIERNA: 120 de daño. ¡Asomadas más cortas!",
        ["NO INFO — the Op repositioned"] = "SIN INFO: el Op se recolocó",
        ["NO REACTION — too quick for him: show the shoulder a bit longer"] =
            "SIN REACCIÓN: demasiado rápido para él, enseña el hombro un poco más",
        ["OP DOWN — risky swing, but it worked"] = "OP ABAJO: asomada arriesgada, pero salió bien",
        ["OP MISSED — SWING NOW"] = "OP FALLÓ: ASÓMATE AHORA",
        ["PLAY MADE — punished the missed shot"] = "JUGADA: castigaste el disparo fallado",
        ["PUNISHED"] = "CASTIGADO",
        ["TIME — he repositions"] = "TIEMPO: se recoloca",
        ["YOU DIED — the Op saw too much of you"] = "HAS MUERTO: el Op te vio demasiado",
        ["SITE CLEAR — DEFUSE!"] = "SITIO LIMPIO: ¡DESACTIVA!",
        ["PLACE YOUR UTILITY — THE PUSH COMES AFTER THE SETUP"] =
            "COLOCA TU UTILIDAD: LA PUSH VIENE DESPUÉS DEL SETUP",
        ["THEY'RE COMING"] = "YA VIENEN",
        ["CUT THESE SIGHTLINES"] = "CORTA ESTAS LÍNEAS DE VISIÓN",
        ["SIGHTLINES CUT"] = "LÍNEAS CORTADAS",
        ["Entry denied"] = "Entrada denegada",
        ["They didn't swing"] = "No se asomaron",
        ["Too slow — the entry survived"] = "Demasiado lento: la entrada sobrevivió",
        ["NO SMOKES OUT"] = "SIN HUMOS",
        ["ENTERED BEFORE YOUR SMOKES WERE UP"] = "ENTRARON ANTES DE QUE ESTUVIERAN TUS HUMOS",
        ["EXECUTE"] = "EJECUTA",
        ["YOUR SMOKES VS THE STANDARD SPOTS"] = "TUS HUMOS FRENTE A LOS PUNTOS ESTÁNDAR",
        ["SMOKE REPLAY"] = "REPETICIÓN DEL HUMO",
        ["SMOKE  |  KILL  |  ◆ ENTRY"] = "HUMO  |  BAJA  |  ◆ ENTRADA",
        ["standard smoke (shown at Rookie / Regular)"] = "humo estándar (se muestra en Novato / Regular)",
        ["defender sightline onto your entry"] = "línea del defensor hacia tu entrada",
        ["X  ASTRAL FORM — PLACE YOUR STARS"] = "X  FORMA ASTRAL: COLOCA TUS ESTRELLAS",
        ["E  NEBULA   ·   F  DISSIPATE"] = "E  NEBULOSA   ·   F  DISIPAR",
        ["YOUR KIT"] = "TU KIT",
        ["LEGEND"] = "LEYENDA",
        ["FLASH INTERCEPTED"] = "FLASH INTERCEPTADO",
        ["Flash was blocked"] = "El flash fue bloqueado",
        ["Recoil pattern reconstructed (Riot publishes none)"] =
            "Patrón de retroceso reconstruido (Riot no publica ninguno)",
        ["Spray broken — keep holding until everyone is down"] =
            "Spray roto: sigue manteniendo hasta que caigan todos",
        ["TOO FAST — NO POINTS"] = "DEMASIADO RÁPIDO: SIN PUNTOS",
        ["TIME TO KILL (150 HP)"] = "TIEMPO HASTA MATAR (150 PV)",
        ["Round held"] = "Ronda aguantada",
        ["Out of bullets"] = "Sin balas",
        ["out of bullets"] = "sin balas",
        ["Out of energy"] = "Sin energía",
        ["Scan: nobody seen"] = "Escaneo: nadie visto",
        ["No wall hit — the shard faded"] = "No dio en la pared: la esquirla se desvaneció",
        ["Lost it off the map"] = "Se perdió fuera del mapa",
        ["Refract beacon faded"] = "Baliza de Refract desvanecida",
        ["Tether faded"] = "Tether desvanecida",
        ["Tailwind faded"] = "Tailwind desvanecida",
        ["Curveball recharged (2 kills)"] = "Curveball recargada (2 bajas)",
        ["Gatecrash recharged (2 kills)"] = "Gatecrash recargada (2 bajas)",
        ["Refract recharged (2 kills)"] = "Refract recargada (2 bajas)",
        ["Slide recharged (2 kills)"] = "Slide recargada (2 bajas)",
        ["Tailwind recharged (2 kills)"] = "Tailwind recargada (2 bajas)",
        ["at the end · B"] = "al final · B",
        ["↑↓ pick a row · ←→ or 1–9 change it · Space continues"] =
            "↑↓ elige una fila · ←→ o 1–9 la cambian · Espacio continúa",

        // ---- settings: notes, option values and status lines ----
        ["Live coach"] = "Coach en vivo",
        ["The whole interface changes immediately"] = "Toda la interfaz cambia al instante",
        ["No Valorant config folder found on this PC"] = "No se ha encontrado la configuración de VALORANT en este PC",
        ["Only for showing eDPI and cm/360"] = "Solo para mostrar eDPI y cm/360",
        ["Valorant: \"Hide outlines and fresnel\" off = ON"] =
            "VALORANT: con \"Ocultar contornos y fresnel\" desactivado = SÍ",
        ["Solid spheres in the highlight colour"] = "Esferas sólidas del color de resalte",
        ["Dark body + highlight outline, like VALORANT enemies"] =
            "Cuerpo oscuro + contorno resaltado, como los enemigos de VALORANT",
        ["Run the Crosshair Finder (Coach) or paste a code below for a second option"] =
            "Abre el Buscador de mira (Coach) o pega un código abajo para una segunda opción",
        ["Your imported VALORANT crosshair"] = "Tu mira importada de VALORANT",
        ["Applies on next drill"] = "Se aplica en el siguiente ejercicio",
        ["Used by every drill that accepts a rifle; Vandal Spray and Phantom Spray keep their own"] =
            "Se usa en todo ejercicio que admita rifle; Ráfaga Vandal y Ráfaga Phantom usan la suya",
        ["Drill default"] = "Según el ejercicio",
        ["Real magazines"] = "Cargadores reales",
        ["Infinite"] = "Infinito",
        ["Each drill decides (aim drills: infinite; bot drills: reloads)"] =
            "Cada ejercicio decide (puntería: infinito; bots: recargas)",
        ["Magazines and reserve run out"] = "Se acaban los cargadores y la reserva",
        ["No reloading, unlimited reserve"] = "Sin recargas, reserva ilimitada",
        ["No guide"] = "Sin línea",
        ["A line from the crosshair to the nearest target"] = "Una línea de la mira al objetivo más cercano",
        ["Aim line, plus the shortest route to the nearest enemy on map drills"] =
            "Línea de mira y la ruta más corta al enemigo más cercano en los ejercicios de mapa",
        ["Short cues during the run: what is going wrong and how to fix it"] =
            "Avisos breves durante la partida: qué va mal y cómo corregirlo",
        ["The coach only reviews the run at the end"] = "El coach solo revisa la partida al final",
        ["A soft click on buttons and a quiet chime on confirmations; hover is silent"] =
            "Un clic suave en los botones y un tono discreto al confirmar; el hover es silencioso",
        ["Menus make no sound"] = "Los menús no suenan",
        ["Relative to the volume above"] = "Relativo al volumen de arriba",
        ["Custom — type a value or use the arrows (Shift ±0.001)"] =
            "Personalizada: escribe un valor o usa las flechas (Mayús ±0.001)",
        ["From VALORANT — type a value or use the arrows to set your own"] =
            "Desde VALORANT: escribe un valor o usa las flechas para poner la tuya",
        ["Not available: this build has no GitHub page set"] =
            "No disponible: esta compilación no tiene página de GitHub",
        ["On by default: at startup and every 6 hours, asks GitHub whether a newer release is out (nothing about you is sent)"] =
            "Activado por defecto: al arrancar y cada 6 horas pregunta a GitHub si hay una versión nueva (no se envía nada sobre ti)",
        ["Off: CA-VAL-AIM makes no network requests. ON asks GitHub at startup and every 6 hours; CHECK NOW asks once"] =
            "Desactivado: CA-VAL-AIM no hace peticiones de red. ACTIVADO pregunta a GitHub al arrancar y cada 6 horas; COMPROBAR AHORA pregunta una vez",
        ["Checking GitHub…"] = "Comprobando GitHub…",
        ["You have the latest version"] = "Tienes la última versión",
        ["Couldn't check for updates"] = "No se pudo comprobar si hay actualizaciones",
        ["just now"] = "ahora mismo",
        ["Ask GitHub once now whether a newer release is out"] =
            "Pregunta a GitHub ahora si hay una versión nueva",
        ["Installs the update and restarts CA-VAL-AIM"] = "Instala la actualización y reinicia CA-VAL-AIM",
        ["Downloads the installer, verifies it against SHA256SUMS.txt and offers to restart; opens the release page if this copy can't update itself"] =
            "Descarga el instalador, lo verifica con SHA256SUMS.txt y ofrece reiniciar; abre la página de la versión si esta copia no puede actualizarse sola",

        // ---- coach quick-start tiles ----
        ["Rates flicking · precision"] = "Valora flick · precisión",
        ["Rates tracking"] = "Valora seguimiento",
        ["Rates crosshair placement"] = "Valora colocación de mira",
        ["Rates movement"] = "Valora movimiento",
        ["Rates spray control"] = "Valora control de spray",

        // ---- agents ----
        ["SIGNATURE DRILLS"] = "EJERCICIOS DE FIRMA",
        ["ROLE DRILLS"] = "EJERCICIOS DE ROL",
        ["Mobility Entry"] = "Entrada con movilidad",
        ["Flash & Peek"] = "Destello y asomada",
        ["Flash & Clear"] = "Destello y limpieza",
        ["Recon & Clear"] = "Reconocimiento y limpieza",
        ["Smoke Execute"] = "Ejecución con humo",
        ["Chamber Guns"] = "Armas de Chamber",
        ["drills played with the agent's own utility: flash and swing, recon and clear, smoke and execute, dash in, anchor a site."] =
            "ejercicios con la utilidad del propio agente: destello y asomada, reconocimiento y limpieza, humo y ejecución, entrada con dash, anclar un sitio.",
        ["drills for the gunfights the role takes most, from opening duels to post-plant holds."] =
            "ejercicios para los duelos que más toma el rol, desde el duelo inicial hasta la defensa tras plantar.",
        ["a check-in, aim, role and signature drills and a deathmatch in about 10 minutes, then your goal and cue."] =
            "un chequeo, ejercicios de puntería, rol y firma y un deathmatch en unos 10 minutos; al final, tu objetivo y tu consigna.",
        ["everything runs at your tier and map"] = "todo va a tu nivel y en tu mapa",

        // ---- lock-in / warm-up ----
        ["SHORT AND AT YOUR REAL SENS"] = "CORTO Y A TU SENSIBILIDAD REAL",
        ["A warm-up wakes up skill you already have. About 10 to 15 minutes at the sens you play with is enough; longer mostly adds fatigue."] =
            "Un calentamiento activa una habilidad que ya tienes. Con unos 10 a 15 minutos a la sensibilidad con la que juegas basta; más tiempo sobre todo añade fatiga.",
        ["SIMPLE TO GAME-LIKE"] = "DE LO SIMPLE A LO REAL",
        ["Easy drills first, then drills that adapt to keep you near 8 of 10 hits, then bots and a deathmatch. Its last minute is easier, so the session ends on a good note."] =
            "Primero ejercicios fáciles, luego ejercicios que se adaptan para mantenerte cerca de 8 aciertos de cada 10, después bots y un deathmatch. Su último minuto es más fácil para que la sesión termine bien.",
        ["CARRY IT INTO THE MATCH"] = "LLÉVALO A LA PARTIDA",
        ["The effect fades after a 5 to 15 minute break, so queue right away. One goal and one cue word give you something simple to come back to each round."] =
            "El efecto se desvanece tras un descanso de 5 a 15 minutos, así que entra a cola enseguida. Un objetivo y una palabra clave te dan algo simple al que volver cada round.",
        ["what the research supports"] = "lo que respalda la investigación",
        ["COMPARED WITH VALORANT"] = "COMPARADO CON VALORANT",
        ["BEFORE YOU START"] = "ANTES DE EMPEZAR",
        ["Same headset and volume as in VALORANT"] = "Los mismos auriculares y volumen que en VALORANT",
        ["A lamp on, not a dark room"] = "Una lámpara encendida, no una habitación a oscuras",
        ["Hands warm"] = "Manos calientes",
        ["Sens · FOV"] = "Sens · FOV",
        ["Display"] = "Pantalla",
        ["VSync · FPS cap"] = "VSync · límite FPS",
        ["ADAPTIVE: LEVEL HELD"] = "ADAPTATIVO: NIVEL FIJO",
        ["% OF YOUR USUAL"] = "% DE TU HABITUAL",
        ["SENSITIVITY (RIGHT AXIS)"] = "SENSIBILIDAD (EJE DERECHO)",
        ["YOUR SENS"] = "TU SENS",
        ["SHIFTED SENS"] = "SENS CAMBIADA",
        ["one tier below yours"] = "un nivel por debajo del tuyo",
        ["gets harder or easier to keep you near 80% hits"] = "sube o baja para mantenerte cerca del 80% de aciertos",
        ["the deathmatch's last minute has easier bots"] = "el último minuto del deathmatch tiene bots más fáciles",

        // ---- match log / what works for you ----
        ["After a Lock-In, the menu asks how your first match went. One tap, optional."] =
            "Tras un Lock-In, el menú pregunta cómo fue tu primera partida. Un toque, opcional.",
        ["Rate your first match after a Lock-In when the menu asks."] =
            "Valora tu primera partida tras un Lock-In cuando el menú te pregunte.",
        ["ENERGY"] = "ENERGÍA",
        ["BREATHING"] = "RESPIRACIÓN",
        ["ROUTINE"] = "RUTINA",
        ["Low (1-3)"] = "Baja (1-3)",
        ["Mid (4-6)"] = "Media (4-6)",
        ["High (7-9)"] = "Alta (7-9)",
        ["With"] = "Con",
        ["Without"] = "Sin",
        ["Full routine"] = "Rutina completa",
        ["Ended early"] = "Terminada antes",
        ["MATCHES"] = "PARTIDAS",
        ["WON"] = "GANADAS",
        ["FELT LOCKED IN"] = "EN FOCUS",
        ["FELT LOCKED IN?"] = "¿EN FOCUS?",
        ["RESULT"] = "RESULTADO",
        ["How did your first match after Lock-In go?"] = "¿Cómo fue tu primera partida tras el Lock-In?",
        ["Next time you open CA-VAL-AIM it asks how your match went (optional)."] =
            "La próxima vez que abras CA-VAL-AIM te preguntará cómo fue tu partida (opcional).",
        ["Don't ask about this Lock-In again"] = "No volver a preguntar por este Lock-In",
        ["Saved. Thanks."] = "Guardado. Gracias.",
        ["Optional. Saved on this PC only."] = "Opcional. Solo se guarda en este PC.",
        ["too few to say"] = "muy pocas para decir",
    };

    static readonly HashSet<string> SpanishValues = new(Exact.Values.Concat(Overrides.Values), StringComparer.Ordinal);

    // Longer phrases first: the regex stops at the longest matching entry and skips already translated Spanish.
    static readonly Dictionary<string, string> Phrases = Exact
        .Concat(Overrides)
        .Where(kv => kv.Key.Length > 1 && !string.Equals(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase))
        .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
        .Select(g => (Key: g.Key, Value: Overrides.TryGetValue(g.Key, out var over) ? over : g.First().Value))
        .OrderByDescending(kv => kv.Key.Length)
        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    static readonly Regex PhraseRegex = BuildRegex();

    static Regex BuildRegex()
    {
        var keys = Phrases.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape);
        return new Regex($"(?<![\\p{{L}}\\p{{N}}])(?:{string.Join("|", keys)})(?![\\p{{L}}\\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
