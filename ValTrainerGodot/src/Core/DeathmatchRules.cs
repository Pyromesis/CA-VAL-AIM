namespace ValTrainer.Core;

/// <summary>Deathmatch bot difficulty chosen in the menu (independent of the drill tier).</summary>
public enum DmDifficulty { Easy, Medium, Hard, Nightmare }

/// <summary>
/// Deathmatch lobby rules and the four bot difficulties. VALORANT's FFA Deathmatch is first to 40 kills or the most kills
/// after 9 minutes; this lobby is 12 players (you + 11 bots). Each difficulty is a rank tier's bot (reaction, crosshair
/// error, Fitts' law settle, per-shot hit chances: see <see cref="Difficulty.Tiers"/>) plus its movement habits:
/// <list type="bullet">
/// <item><b>Easy</b> (Rookie: Iron–Bronze): slow reactions, run-and-gun, no strafing, jog instead of sprint.</item>
/// <item><b>Medium</b> (Veteran: Platinum–Diamond): stop before shooting, pre-aim where you were, walk the last metres.</item>
/// <item><b>Hard</b> (Elite: Ascendant–Immortal): A-D jiggle between bursts, hold angles after losing you, crouch sprays.</item>
/// <item><b>Nightmare</b>: beyond Radiant — 150 ms reactions, near-perfect placement and hits, hears every running step.
/// Built to be beaten only with good positioning, counter-strafing and first-bullet accuracy.</item>
/// </list>
/// </summary>
public static class DeathmatchRules
{
    public const int LobbyPlayers = 12;
    public const int MinPlayers = 4;
    public const int ValorantKills = 40;
    public const float ValorantMinutes = 9f;

    public static readonly DmDifficulty[] All = { DmDifficulty.Easy, DmDifficulty.Medium, DmDifficulty.Hard, DmDifficulty.Nightmare };

    public static DmDifficulty FromSetting(int v) => (DmDifficulty)Math.Clamp(v, 0, 3);

    /// <summary>The rank tier whose bot model and behaviours the difficulty uses (results and badges are graded at it).</summary>
    public static int Tier(DmDifficulty d) => d switch { DmDifficulty.Easy => 0, DmDifficulty.Medium => 2, DmDifficulty.Hard => 3, _ => 4 };

    /// <summary>Behaviour level: the tier, and 5 for Nightmare (Pro habits plus its own extras).</summary>
    public static int AiLevel(DmDifficulty d) => d == DmDifficulty.Nightmare ? 5 : Tier(d);

    /// <summary>Nightmare's combat skill: faster and steadier than the Radiant tier.</summary>
    public static readonly BotSkill Nightmare = new(150, 15, 0.5f, 62, .92f, .55f, .975f, 4, 115, false);

    public static BotSkill Skill(DmDifficulty d) => d == DmDifficulty.Nightmare ? Nightmare : Difficulty.Get(Tier(d)).Bot;

    public static string Name(DmDifficulty d) => d switch
    {
        DmDifficulty.Easy => "Easy", DmDifficulty.Medium => "Medium", DmDifficulty.Hard => "Hard", _ => "Nightmare",
    };

    public static string Blurb(DmDifficulty d) => d switch
    {
        DmDifficulty.Easy => "Iron–Bronze bots: slow reactions, they shoot while running",
        DmDifficulty.Medium => "Platinum–Diamond bots: they stop to shoot and pre-aim where you were",
        DmDifficulty.Hard => "Ascendant–Immortal bots: jiggle peeks, held angles, crouch sprays",
        _ => "Beyond Radiant: 150 ms reactions and they hear every running step",
    };

    /// <summary>Difficulty name in the current language (menus, mode description, result rows).</summary>
    public static string NameLocal(DmDifficulty d) => !Loc.Spanish ? Name(d) : d switch
    {
        DmDifficulty.Easy => "Fácil", DmDifficulty.Medium => "Medio", DmDifficulty.Hard => "Difícil", _ => "Pesadilla",
    };

    /// <summary>One-line difficulty explanation in the current language.</summary>
    public static string BlurbLocal(DmDifficulty d) => !Loc.Spanish ? Blurb(d) : d switch
    {
        DmDifficulty.Easy => "Bots Hierro–Bronce: reaccionan lento y disparan corriendo",
        DmDifficulty.Medium => "Bots Platino–Diamante: se paran para disparar y apuntan donde estabas",
        DmDifficulty.Hard => "Bots Ascendente–Inmortal: jiggle peeks, ángulos aguantados, ráfagas agachados",
        _ => "Más allá de Radiante: 150 ms de reacción y oyen cada paso que das corriendo",
    };

    /// <summary>Kill target for a lobby: VALORANT's 40 in 9 minutes, scaled to the run length and lobby size.</summary>
    public static int KillTarget(float seconds, int players)
    {
        float perMinute = ValorantKills / ValorantMinutes * Math.Clamp(players / (float)LobbyPlayers, 0.5f, 1.2f);
        return Math.Max(5, (int)MathF.Round(perMinute * seconds / 60f));
    }
}
