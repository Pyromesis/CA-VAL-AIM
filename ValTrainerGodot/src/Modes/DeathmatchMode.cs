using Godot;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Game.Bots;
using ValTrainer.UI;

namespace ValTrainer.Modes;

/// <summary>
/// VALORANT-style free-for-all Deathmatch against bots on any of the 13 maps (the whole extracted map).
/// Rules (Riot support "FFA Deathmatch" + the official VALORANT wiki): first to the kill target or most kills when time
/// runs out. From the menu it is a full 12-player lobby (you + 11 bots), first to 40 kills in 9 minutes like VALORANT,
/// with the bot difficulty chosen there (<see cref="DmDifficulty"/>: Easy / Medium / Hard / Nightmare). 1.5 s respawn at
/// a random spot inside the map and free of enemies, brief spawn protection that ends when you fire, heavy armour every
/// life (100 HP + 50 shield), every kill reloads your gun, every death drops a health pack (10 s, full HP + armour), and
/// respawning briefly reveals where the enemies are. Bots roam the map on a nav grid (<see cref="BotNav"/>), hunt your
/// last known position (gunfire, damage, running footsteps, the respawn reveal), hold angles, and fight you with their
/// difficulty's duel model (<see cref="BotBrain"/>) — and each other. Tab shows the scoreboard.
/// <para>Warm-up / routines: <c>new DeathmatchMode(durationSeconds: 180)</c> or <c>DeathmatchMode.Create(180)</c>: a small
/// lobby sized to the map at the session's tier; the kill target scales with the length. A session <c>DurationOverride</c>
/// is honoured too.</para>
/// </summary>
public sealed partial class DeathmatchMode : MapMode
{
    public const float DefaultSeconds = 300f;
    public const float RespawnDelay = 1.5f, SpawnProtection = 1.5f, PackLifetime = 10f, RevealTime = 2f;

    readonly float duration;
    readonly int? killTargetArg;
    readonly WeaponKind weapon;
    int killTarget;
    /// <summary>Menu Deathmatch: the chosen bot difficulty and lobby size (null = routine: session tier, map-sized lobby).</summary>
    readonly DmDifficulty? difficulty;
    readonly int lobby;

    /// <param name="durationSeconds">Run length (default 5 min).</param>
    /// <param name="killTarget">Kills that end the match (0 = 1 per 10 s of run length, i.e. 30 for 5 min).</param>
    /// <param name="weapon">Your rifle (Vandal, Phantom or Sheriff).</param>
    public DeathmatchMode(float durationSeconds = DefaultSeconds, int killTarget = 0, WeaponKind weapon = WeaponKind.Vandal)
    {
        duration = Mathf.Max(10f, durationSeconds);
        killTargetArg = killTarget > 0 ? killTarget : null;
        this.weapon = weapon;
        this.killTarget = killTargetArg ?? KillsFor(duration);
    }

    /// <summary>The menu's Deathmatch: a lobby of <paramref name="players"/> (you included) against bots of
    /// <paramref name="difficulty"/>, VALORANT's first-to-40 scaled to the length.</summary>
    public DeathmatchMode(DmDifficulty difficulty, int players, float minutes, WeaponKind weapon = WeaponKind.Vandal)
        : this(Mathf.Clamp(minutes, 1f, 20f) * 60f, 0, weapon)
    {
        this.difficulty = difficulty;
        lobby = Math.Clamp(players, DeathmatchRules.MinPlayers, DeathmatchRules.LobbyPlayers);
        killTarget = DeathmatchRules.KillTarget(duration, lobby);
    }

    /// <summary>The menu's Deathmatch with the player's saved lobby settings.</summary>
    public static DeathmatchMode FromSettings()
    {
        var st = Main.I?.Settings;
        return st == null ? new DeathmatchMode()
            : new DeathmatchMode(DeathmatchRules.FromSetting(st.DmDifficulty), st.DmPlayers, st.DmMinutes);
    }

    /// <summary>Factory for routines (warm-up): a Deathmatch of <paramref name="seconds"/>.</summary>
    public static DeathmatchMode Create(float seconds, int killTarget = 0, WeaponKind weapon = WeaponKind.Vandal) => new(seconds, killTarget, weapon);

    static int KillsFor(float seconds) => Math.Max(5, (int)Mathf.Round(seconds / 10f));

    public override string Key => "deathmatch";
    public override string Name => "Deathmatch";
    public override string Description => difficulty is { } d
        ? Loc.Spanish
            ? $"Todos contra todos de VALORANT: {lobby} jugadores, primero en llegar a {killTarget}. Los bots {DeathmatchRules.NameLocal(d).ToLowerInvariant()} te buscan por todo el mapa."
            : $"VALORANT free-for-all: {lobby} players, first to {killTarget}. {DeathmatchRules.Name(d)} bots hunt you across the whole map."
        : Loc.Spanish
            ? "Todos contra todos contra bots de tu nivel en un mapa de VALORANT. Te buscan y disparan; reapareces y sigues."
            : "Free-for-all against bots at your tier on a VALORANT map. They hunt you and shoot back; respawn and keep fighting.";
    public override bool Timed => true;
    public override float Duration => duration;
    public override WeaponKind Weapon => weapon;
    public override bool Done => winner != null;
    public override int? PreferredTier => difficulty is { } d ? DeathmatchRules.Tier(d) : null;
    public override string? Subtitle => difficulty is { } d
        ? Loc.Spanish
            ? $"{MapSpot.Map} — Deathmatch · {lobby} jugadores · {DeathmatchRules.NameLocal(d)} · primero a {killTarget}"
            : $"{MapSpot.Map} — Deathmatch · {lobby} players · {DeathmatchRules.Name(d)} · first to {killTarget}"
        : Loc.Spanish ? $"{MapSpot.Map} — Deathmatch · primero a {killTarget}" : $"{MapSpot.Map} — Deathmatch · first to {killTarget}";
    public int KillTarget => killTarget;
    /// <summary>Behaviour level of the bots: the tier, 5 = Nightmare.</summary>
    internal int AiLevel => difficulty is { } d ? DeathmatchRules.AiLevel(d) : Tier;

    // ---------------- state ----------------

    BotNav nav = null!;
    readonly List<Bot> bots = new();
    int[] spawnPool = Array.Empty<int>();
    string? winner;
    float playerRespawnAt = -1f, playerSpawnedAt, revealUntil;
    int lastShots, pathBudget, headKills, packsTaken, spawnDeaths, botSpawnDeaths, botBotKills, compromisedSpawns;
    float noiseAt = -99f;
    readonly List<(float T, string Text)> feed = new();
    static readonly bool DevLog = Main.I?.Dev ?? false;
    float logAt = 10f;
    bool started, lastSpawnClean = true, playerSpawnClean = true;
    int inWallFrames;

    Vector3 PlayerFeet => G.View.Eye - new Vector3(0, G.Mover.EyeHeight, 0);
    bool PlayerAlive => !G.Player.Dead;

    protected override void Setup()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        nav = BotNav.For($"{MapSpot.Key}:{G.Solid.Count}", G.Solid, SpawnFeet);
        if ((G as GameSession)?.DurationOverride is { } ov && killTargetArg == null)
            killTarget = difficulty != null ? DeathmatchRules.KillTarget(ov, lobby) : KillsFor(ov);
        spawnPool = SpawnPool(nav, G.Solid, MapSpot, out var why);
        if (DevLog) GD.Print($"[dm] spawn pool: {why}");
        int n = BotCount();
        for (int i = 0; i < n; i++) bots.Add(new Bot(this, i));
        // First spawn: you at the map's attacker start, bots away from you and out of sight.
        foreach (var b in bots) SpawnLife(b, SpawnFeet);
        if (DevLog)
            GD.Print($"[dm] map {MapSpot.Key} tier {Tier}: nav {nav.Count} nodes ({nav.Area:0} m², built {nav.BuildMs:0} ms), " +
                     $"{spawnPool.Length} spawn spots, {n} bots, first to {killTarget}, setup {sw.Elapsed.TotalMilliseconds:0} ms");
    }

    /// <summary>
    /// Deathmatch spawn spots: nav nodes on open floor (all 8 neighbours walkable) that pass <see cref="SpawnSafety"/>
    /// (standing room, head room) and lie inside the official minimap's playable area. The nav floods every floor reachable
    /// from the attacker start, which on the extracted maps includes slabs under stairs, inside props and beyond the map's
    /// edge: spawning there was the "under the map" bug. Falls back to looser sets only when a map has too few spots.
    /// Shared with the dev spawn audit (<see cref="Maps.SpawnAudit"/>).
    /// </summary>
    internal static int[] SpawnPool(BotNav nav, IReadOnlyList<Box> solid, Maps.MapSpot spot, out string why)
    {
        var playable = PlayableTest(spot);
        // Nodes the offline art check rejected (inside art geometry, under a floor, on an invisible slab):
        // maps/<key>/spawn_exclude.json from tools/mapgen/audit_spawns.py --write-exclude.
        var exclude = string.IsNullOrWhiteSpace(spot.RealMapFolder) ? Array.Empty<Vector3>()
            : World.MapAssets.LoadPoints(spot.RealMapFolder + "/spawn_exclude.json");
        var excludeCells = new HashSet<(int, int, int)>();
        foreach (var e in exclude) excludeCells.Add(((int)MathF.Floor(e.X / 0.5f), (int)MathF.Floor(e.Y / 0.5f), (int)MathF.Floor(e.Z / 0.5f)));
        bool Excluded(Vector3 p)
        {
            if (excludeCells.Count == 0) return false;
            int cx = (int)MathF.Floor(p.X / 0.5f), cy = (int)MathF.Floor(p.Y / 0.5f), cz = (int)MathF.Floor(p.Z / 0.5f);
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (excludeCells.Contains((cx + dx, cy + dy, cz + dz)) && exclude.Any(e => e.DistanceTo(p) < 0.35f)) return true;
            return false;
        }
        int open = 0, standable = 0, inside = 0, stacked = 0, artRejected = 0;
        var strict = new List<int>();
        var loose = new List<int>();
        for (int i = 0; i < nav.Count; i++)
        {
            if (nav.Degree(i) != 8) continue;
            open++;
            var p = nav.Pos[i];
            if (!SpawnSafety.Standable(p, solid, _ => -1)) continue;
            // Not under another walkable floor (the hidden ground under raised floors) and not in a crawl space.
            if (nav.Stacked(i, 3.2f) || Collision.Ceiling(p, solid, p.Y + 0.05f) < p.Y + 2.2f) { stacked++; continue; }
            if (Excluded(p)) { artRejected++; continue; }
            standable++;
            loose.Add(i);
            if (playable != null && playable(p) == 0) continue;
            inside++;
            strict.Add(i);
        }
        why = $"{nav.Count} nav nodes, {open} open, {stacked} under another floor, {artRejected} rejected by the art check, {standable} standable, {inside} inside the minimap" +
              (playable == null ? " (no minimap mask)" : "");
        if (strict.Count >= 40) return strict.ToArray();
        if (loose.Count >= 40) { why += " → using standable (mask too small)"; return loose.ToArray(); }
        why += " → using every nav node";
        return Enumerable.Range(0, nav.Count).ToArray();
    }

    int BotCount()
    {
        if (difficulty != null) return lobby - 1; // the menu's lobby: 11 bots in a full 12-player match
        // Routines: ~250 m² of floor per bot, 3–6 (short warm-up deathmatches stay readable).
        int byArea = Math.Clamp((int)Mathf.Round(nav.Area / 250f), 3, 6);
        return Tier <= 1 ? Math.Min(byArea, 5) : byArea;
    }

    // ---------------- frame ----------------

    public override void Update(float dt)
    {
        pathBudget = 2;
        if (!started)
        {
            // First running frame (the session's Respawn after Setup resets the player state).
            started = true;
            G.Player.ProtectedUntil = Now + SpawnProtection;
            playerSpawnedAt = Now;
            revealUntil = Now + RevealTime;
        }
        if (G.Player.Dead)
        {
            if (Now >= playerRespawnAt) RespawnPlayer();
        }
        else if (Shots > lastShots)
        {
            if (Now < G.Player.ProtectedUntil) G.Player.ProtectedUntil = Now; // firing ends spawn protection
            if (Now - noiseAt > 0.5f) { noiseAt = Now; Noise(PlayerFeet, null, 35f); }
        }
        lastShots = Shots;
        if (!eased && G.EaseLastSeconds > 0 && Now >= G.RunLength - G.EaseLastSeconds) EaseBots();

        foreach (var b in bots) b.Update(dt);
        UpdatePacks(dt);
        UpdateSight();

        var top = bots.OrderByDescending(b => b.Kills).FirstOrDefault();
        if (Kills >= killTarget) winner = "You";
        else if (top != null && top.Kills >= killTarget) winner = top.Name;

        if (DevLog && Now >= logAt) { logAt += 10f; DevReport(false); }
    }

    // ---------------- finisher (Lock-In) ----------------

    bool eased;

    /// <summary>The bots' combat skill right now: the tier's, or one tier easier once the finisher started.</summary>
    internal BotSkill BotSkillNow => eased ? Difficulty.EasedBot(Tier)
        : difficulty is { } d ? DeathmatchRules.Skill(d) : Difficulty.Get(Tier).Bot;

    /// <summary>Last <see cref="IGame.EaseLastSeconds"/> of a routine's deathmatch: every bot (alive or respawning) fights
    /// one tier easier, so the player finishes the session on a run of wins. The Lock-In screen says so up front.</summary>
    void EaseBots()
    {
        eased = true;
        var skill = BotSkillNow;
        foreach (var b in bots) b.SetSkill(skill);
        float left = G.RunLength - Now;
        string when = Loc.Spanish
            ? Mathf.Abs(G.EaseLastSeconds - 60f) < 8f ? "ÚLTIMO MINUTO" : $"ÚLTIMOS {Mathf.Round(G.EaseLastSeconds)} SEGUNDOS"
            : Mathf.Abs(G.EaseLastSeconds - 60f) < 8f ? "LAST MINUTE" : $"LAST {Mathf.Round(G.EaseLastSeconds)} SECONDS";
        G.Banner($"{when} · {(Loc.Spanish ? "TERMINA FUERTE" : "FINISH STRONG")}", UiTheme.Good);
        G.Sound("go", 0.6f);
        Log.Info($"[dm] finisher: bots eased to tier {Math.Max(0, Tier - 1)}{(Tier == 0 ? " (slower Rookie)" : "")} at {Now:0.0}s, {left:0} s left " +
                 $"(react {skill.ReactMs:0} ms, first-shot hit {skill.PHit1:0.00})");
    }

    // ---------------- spawning ----------------

    /// <summary>Random open spot that no living enemy can see, ≥10 m from all of them (prefers 12–28 m: "close to the action").</summary>
    Vector3 PickSpawn(List<Vector3> enemyEyes, out float yaw)
    {
        Vector3 best = nav.Pos[spawnPool[Rng.Next(spawnPool.Length)]];
        float bestScore = float.MinValue;
        bool bestOk = false, lastSpawnOk;
        for (int k = 0; k < 90; k++)
        {
            var p = nav.Pos[spawnPool[Rng.Next(spawnPool.Length)]];
            var eye = p + new Vector3(0, PlayerView.EyeHeight, 0);
            float minD = 999f; bool seen = false;
            foreach (var e in enemyEyes)
            {
                minD = Mathf.Min(minD, e.DistanceTo(eye));
                if (!seen && (G.LineOfSight(e, eye) || G.LineOfSight(e, p + new Vector3(0, 1.0f, 0)))) seen = true;
            }
            lastSpawnOk = !seen && minD >= 10f;
            float score = (seen ? -1000f : 0f) + (minD < 10f ? -400f + minD * 10f : 0f) - Mathf.Abs(Mathf.Min(minD, 60f) - 20f) * 0.5f;
            if (score > bestScore) { bestScore = score; best = p; bestOk = lastSpawnOk; }
            if (k >= 24 && !seen && minD is >= 12f and <= 28f) break;
        }
        yaw = OpenYaw(best);
        if (!bestOk) compromisedSpawns++;
        lastSpawnClean = bestOk;
        return best;
    }

    /// <summary>Facing toward the longest open sightline (and not into a wall) from a spot.</summary>
    float OpenYaw(Vector3 feet)
    {
        var eye = feet + new Vector3(0, PlayerView.EyeHeight, 0);
        float bestYaw = 0, bestD = -1;
        for (int i = 0; i < 16; i++)
        {
            float yaw = i * 22.5f, r = Mathf.DegToRad(yaw);
            var d = new Vector3(Mathf.Sin(r), 0, -Mathf.Cos(r));
            float dist = Mathf.Min(40f, Collision.FirstHit(eye, d, G.Solid)) + (float)Rng.NextDouble() * 3f;
            if (dist > bestD) { bestD = dist; bestYaw = yaw; }
        }
        return bestYaw;
    }

    List<Vector3> EnemyEyes(Bot? except, Vector3? playerFeet)
    {
        var l = new List<Vector3>();
        if (playerFeet is { } pf) l.Add(pf + new Vector3(0, PlayerView.EyeHeight, 0));
        foreach (var b in bots) if (b != except && b.Alive) l.Add(b.Body!.Feet + new Vector3(0, PlayerView.EyeHeight, 0)); // (Head isn't posed until the first frame)
        return l;
    }

    void SpawnLife(Bot b, Vector3? playerFeet)
    {
        var p = PickSpawn(EnemyEyes(b, playerFeet), out float yaw);
        var body = G.SpawnBot(p);
        body.FacingYaw = yaw;
        var r = Mathf.DegToRad(yaw);
        var brain = new BotBrain(body, BotSkillNow, Rng)
        {
            HeldDir = new Vector3(Mathf.Sin(r), 0, -Mathf.Cos(r)),
            ShoulderSight = Difficulty.ShoulderSight(Tier),
        };
        b.Begin(body, brain, playerFeet);
    }

    void RespawnPlayer()
    {
        var p = PickSpawn(EnemyEyes(null, null), out float yaw);
        G.Respawn(p, yaw);
        playerSpawnClean = lastSpawnClean;
        G.Mover.ResetStops();
        G.Player.ProtectedUntil = Now + SpawnProtection;
        if (G is GameSession s && s.Gun != null) { s.Gun.Ammo = s.Gun.Def.Mag; s.Gun.ReloadLeft = 0; }
        playerRespawnAt = -1f;
        playerSpawnedAt = Now;
        revealUntil = Now + RevealTime; // VALORANT: respawning pulses every enemy on your minimap
    }

    // ---------------- kills ----------------

    public override void OnPlayerDied()
    {
        Deaths++;
        Score = Math.Max(0, Score - 50);
        playerRespawnAt = Now + RespawnDelay;
        if (Now - playerSpawnedAt < 2.5f) spawnDeaths++;
        if (DevLog) GD.Print($"[dm] you died {Now:0.0}s after {Now - playerSpawnedAt:0.0}s alive (spawn {(playerSpawnClean ? "clean" : "NOT clean")}, shots {Shots})");
        // The killer is the bot that fired the last hit (damage comes from its head position).
        Bot? killer = null; float bd = float.MaxValue;
        foreach (var b in bots)
        {
            if (!b.Alive) continue;
            float d = b.Body!.Head.DistanceSquaredTo(G.Player.LastHitFrom);
            if (d < bd) { bd = d; killer = b; }
        }
        if (killer != null) { killer.Kills++; killer.ForgetPlayer(); }
        string killerName = killer?.Name ?? "?";
        AddFeed(Loc.Spanish ? $"{killerName} te eliminó" : $"{killerName} killed You");
        string bannerName = killer?.Name.ToUpperInvariant() ?? (Loc.Spanish ? "UN BOT" : "A BOT");
        G.Banner(Loc.Spanish ? $"ELIMINADO POR {bannerName}" : $"KILLED BY {bannerName}", UiTheme.Accent);
        DropPack(PlayerFeet);
    }

    /// <summary>A bot died (to you if <paramref name="killer"/> is null).</summary>
    void OnBotKilled(Bot victim, Bot? killer, bool headshot)
    {
        var feet = victim.Body!.Feet;
        if (Now - victim.SpawnedAt < 2.5f) botSpawnDeaths++;
        victim.OnDeath(Now + RespawnDelay);
        if (killer == null)
        {
            Score += 100 + (headshot ? 50 : 0);
            if (headshot) headKills++;
            // VALORANT DM: every kill reloads your gun.
            if (G is GameSession s && s.Gun != null) { s.Gun.Ammo = s.Gun.Def.Mag; s.Gun.ReloadLeft = 0; }
            AddFeed(Loc.Spanish
                ? $"Eliminaste a {victim.Name}{(headshot ? " · a la cabeza" : "")}"
                : $"You killed {victim.Name}{(headshot ? " · headshot" : "")}");
        }
        else
        {
            killer.Kills++;
            botBotKills++;
            AddFeed(Loc.Spanish ? $"{killer.Name} eliminó a {victim.Name}" : $"{killer.Name} killed {victim.Name}");
        }
        DropPack(feet);
    }

    void AddFeed(string text)
    {
        feed.Add((Now, text));
        if (feed.Count > 12) feed.RemoveAt(0);
    }

    /// <summary>Gunfire is heard: idle bots in range head for it (they hunt the player if it was you).</summary>
    void Noise(Vector3 at, Bot? source, float range)
    {
        foreach (var b in bots)
            if (b != source && b.Alive && b.Body!.Feet.DistanceTo(at) < range) b.Hear(at, source == null);
    }

    // ---------------- bullets ----------------

    public override float OnBullet(Vector3 o, Vector3 d, bool accurate)
    {
        Shots++;
        if (!accurate) MovingShots++;
        if (G.Player.Dead) return float.PositiveInfinity;
        float maxDist = Wall(o, d);
        // Spawn-protected bots stop the bullet but take no damage.
        foreach (var b in bots)
            if (b.Alive && b.Protected)
            {
                var z = b.Body!.Raycast(o, d, out var t);
                if (z != HitZone.None && t < maxDist) maxDist = t;
            }
        var live = bots.Where(b => b.Alive && !b.Protected).Select(b => b.Body!).ToList();
        var hit = ShootBots(live, o, d, out var zone, out bool killed, out float dist, maxDist);
        if (hit == null) return maxDist < Wall(o, d) ? maxDist : float.PositiveInfinity;
        var bot = bots.First(b => b.Body == hit);
        bot.Hurt(null);
        if (killed) OnBotKilled(bot, null, zone == HitZone.Head);
        return dist;
    }

    // ---------------- what the player sees (aim coach) ----------------

    readonly List<Bot> visible = new();

    void UpdateSight()
    {
        visible.Clear();
        if (!PlayerAlive) return;
        var eye = G.View.Eye;
        foreach (var b in bots)
        {
            if (!b.Alive) continue;
            var head = b.Body!.Head;
            if (!G.LineOfSight(eye, head)) continue;
            visible.Add(b);
            // A new engagement: first sight after ≥2.5 s unseen. Crosshair placement is graded only for enemies in front.
            if (Now - b.SeenByPlayerAt > 2.5f)
            {
                b.Body.SpawnTime = Now; // time-to-kill counts from first sight
                SeenEvent(head); // graded only within SeenMaxDeg (60°) of the crosshair
            }
            b.SeenByPlayerAt = Now;
        }
    }

    public override AimFocus? Focus
    {
        get
        {
            AimFocus? best = null; float bestA = float.MaxValue;
            foreach (var b in visible)
            {
                if (!b.Alive) continue;
                float a = AngleFromCrosshair(b.Body!.Head);
                if (a < bestA) { bestA = a; best = new AimFocus(IdOf(b.Body), b.Body.Head, BotCharacter.HeadRadius); }
            }
            return best;
        }
    }

    public override IEnumerable<Vector3> GuideTargets => bots.Where(b => b.Alive).Select(b => b.Body!.Feet);

    // ---------------- HUD ----------------

    float KD => Deaths == 0 ? Kills : (float)Kills / Deaths;

    public override IEnumerable<string> HudLines()
    {
        bool es = Loc.Spanish;
        yield return es
            ? $"Bajas {Kills} / {killTarget}   Muertes {Deaths}   K/D {KD:0.00}"
            : $"Kills {Kills} / {killTarget}   Deaths {Deaths}   K/D {KD:0.00}";
        var top = bots.OrderByDescending(b => b.Kills).FirstOrDefault();
        if (top != null)
            yield return top.Kills > Kills
                ? (es ? $"Líder: {top.Name} ({top.Kills})" : $"Leader: {top.Name} ({top.Kills})")
                : top.Kills == Kills
                    ? (es ? $"Empatados en cabeza ({Kills})" : $"Tied for the lead ({Kills})")
                    : (es ? $"Vas {Kills - top.Kills} por delante" : $"You lead by {Kills - top.Kills}");
        if (PlayerAlive && Now < G.Player.ProtectedUntil) yield return es ? "Protección al reaparecer" : "Spawn protection";
        if (eased) yield return es ? "Termina fuerte · los bots ahora son más fáciles" : "Finish strong · bots are easier now";
        for (int i = feed.Count - 1, n = 0; i >= 0 && n < 4; i--)
            if (Now - feed[i].T < 5f) { yield return feed[i].Text; n++; }
    }

    public override string? Prompt => G.Player.Dead && playerRespawnAt > 0
        ? Loc.Spanish
            ? $"Reapareciendo en {Mathf.Max(0, playerRespawnAt - Now):0.0}s"
            : $"Respawning in {Mathf.Max(0, playerRespawnAt - Now):0.0}s"
        : null;

    /// <summary>Respawn reveal: red markers around the crosshair toward every living enemy (fades over 2 s); Tab (VALORANT's
    /// scoreboard key) shows the lobby's scoreboard.</summary>
    public override void Draw2D(CanvasItem canvas, Vector2 size)
    {
        if (G == null) return;
        if (Input.IsPhysicalKeyPressed(Godot.Key.Tab)) DrawScoreboard(canvas, size);
        if (Now >= revealUntil || !PlayerAlive) return;
        float a = Mathf.Clamp((revealUntil - Now) / RevealTime, 0, 1);
        float k = size.Y / 1080f, rad = 150f * k;
        var c = size / 2;
        var pts = new Vector2[3];
        foreach (var b in bots)
        {
            if (!b.Alive) continue;
            var d = b.Body!.Feet - G.View.Eye;
            float rel = Mathf.Wrap(Mathf.RadToDeg(Mathf.Atan2(d.X, -d.Z)) - G.View.ViewYaw, -180f, 180f);
            float ang = Mathf.DegToRad(rel) - Mathf.Pi / 2;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            var perp = new Vector2(-dir.Y, dir.X);
            pts[0] = c + dir * (rad + 20 * k); pts[1] = c + dir * rad + perp * 9 * k; pts[2] = c + dir * rad - perp * 9 * k;
            canvas.DrawColoredPolygon(pts, new Color(UiTheme.Accent, 0.9f * a));
        }
    }

    /// <summary>Scoreboard: every player of the lobby by kills (you highlighted), K / D and K/D, the kill target and time left.</summary>
    void DrawScoreboard(CanvasItem c, Vector2 size)
    {
        float k = size.Y / 1080f;
        var rows = bots.Select(b => (Name: b.Name, b.Kills, b.Deaths, You: false))
            .Append((Name: "You", Kills, Deaths, You: true))
            .OrderByDescending(r => r.Kills).ThenBy(r => r.Deaths).ToList();
        float rowH = 34 * k, w = 560 * k, head = 64 * k;
        float h = head + rows.Count * rowH + 16 * k;
        var r0 = new Rect2((size.X - w) / 2, (size.Y - h) / 2, w, h);
        c.DrawRect(r0, new Color(0.035f, 0.045f, 0.06f, 0.88f));
        c.DrawRect(new Rect2(r0.Position, new Vector2(w, 2 * k)), UiTheme.Accent);
        int ts = UiTheme.Fs(20, k), ls = UiTheme.Fs(12, k), vs = UiTheme.Fs(16, k);
        Gfx.Text(c, UiTheme.DisplayWide, "DEATHMATCH", r0.Position.X + 22 * k, r0.Position.Y + 34 * k, ts, UiTheme.Text);
        float left = Mathf.Max(0, G.RunLength - Now);
        Gfx.TextR(c, UiTheme.HudWide, $"FIRST TO {killTarget}  ·  {(int)left / 60}:{(int)left % 60:00}", r0.End.X - 22 * k, r0.Position.Y + 32 * k, ls, UiTheme.Dim);
        float cx1 = r0.End.X - 210 * k, cx2 = r0.End.X - 140 * k, cx3 = r0.End.X - 22 * k;
        float y = r0.Position.Y + head;
        Gfx.Text(c, UiTheme.HudWide, "PLAYER", r0.Position.X + 56 * k, y - 8 * k, ls, UiTheme.Faint);
        Gfx.TextR(c, UiTheme.HudWide, "K", cx1, y - 8 * k, ls, UiTheme.Faint);
        Gfx.TextR(c, UiTheme.HudWide, "D", cx2, y - 8 * k, ls, UiTheme.Faint);
        Gfx.TextR(c, UiTheme.HudWide, "K/D", cx3, y - 8 * k, ls, UiTheme.Faint);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            float ry = y + i * rowH;
            if (row.You) c.DrawRect(new Rect2(r0.Position.X + 8 * k, ry, w - 16 * k, rowH - 2 * k), new Color(UiTheme.Accent, 0.16f));
            else if (i % 2 == 0) c.DrawRect(new Rect2(r0.Position.X + 8 * k, ry, w - 16 * k, rowH - 2 * k), new Color(1, 1, 1, 0.025f));
            float cy = ry + rowH / 2;
            var col = row.You ? UiTheme.Text : UiTheme.TextSoft;
            Gfx.TextR(c, UiTheme.HudWide, (i + 1).ToString(), r0.Position.X + 40 * k, Gfx.Mid(cy, vs), vs, UiTheme.Faint);
            Gfx.Text(c, UiTheme.BodySemi, row.You ? "You" : row.Name, r0.Position.X + 56 * k, Gfx.Mid(cy, vs), vs, col);
            Gfx.TextR(c, UiTheme.Display, row.Kills.ToString(), cx1, Gfx.Mid(cy, vs), vs, col);
            Gfx.TextR(c, UiTheme.Display, row.Deaths.ToString(), cx2, Gfx.Mid(cy, vs), vs, UiTheme.Dim);
            float kd = row.Deaths == 0 ? row.Kills : (float)row.Kills / row.Deaths;
            Gfx.TextR(c, UiTheme.Display, kd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), cx3, Gfx.Mid(cy, vs), vs, UiTheme.Dim);
        }
    }

    // ---------------- results ----------------

    int Placement => 1 + bots.Count(b => b.Kills > Kills);

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        bool es = Loc.Spanish;
        yield return ("Map", $"{MapSpot.Map} {MapSpot.Name}");
        if (difficulty is { } d)
            yield return (es ? "Sala" : "Lobby",
                es ? $"{bots.Count + 1} jugadores · bots {DeathmatchRules.NameLocal(d).ToLowerInvariant()}"
                   : $"{bots.Count + 1} players · {DeathmatchRules.Name(d)} bots");
        yield return (es ? "Bajas / muertes" : "Kills / deaths", $"{Kills} / {Deaths}  (K/D {KD:0.00})");
        yield return (es ? "Puesto" : "Placement",
            winner == "You"
                ? (es ? $"1.º de {bots.Count + 1} — primero a {killTarget}" : $"1st of {bots.Count + 1} — first to {killTarget}")
                : (es ? $"{(Placement == 1 ? "1.º" : $"{Placement}.º")} de {bots.Count + 1}" : $"{Ordinal(Placement)} of {bots.Count + 1}")
                  + (winner != null ? (es ? $" ({winner} llegó a {killTarget})" : $" ({winner} reached {killTarget})") : ""));
        if (Hits > 0)
            yield return (es ? "Tiros a la cabeza" : "Headshot %",
                es ? $"{100f * HeadHits / Hits:0}%  ({headKills} bajas a la cabeza)" : $"{100f * HeadHits / Hits:0}%  ({headKills} headshot kills)");
        yield return (es ? "Precisión" : "Accuracy", $"{Accuracy * 100:0.0}%  ({Hits}/{Shots})");
        if (KillTimes.Count > 0)
            yield return (es ? "Tiempo medio de baja" : "Avg time to kill",
                es ? $"{KillTimes.Average():0} ms desde el primer avistamiento" : $"{KillTimes.Average():0} ms from first sight");
        if (packsTaken > 0) yield return (es ? "Botiquines" : "Health packs", packsTaken.ToString());
        foreach (var l in MovementLines()) yield return l;
    }

    static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    /// <summary>Kill share vs the tier's bots: 50% (K/D 1.0) matches the tier, 70%+ (K/D 2.3) earns the next one.</summary>
    public override int Badge()
    {
        if (DevLog && !finalLogged) { finalLogged = true; DevReport(true); } // MakeRecord asks once when the run ends
        return Kills + Deaths < 3 ? -1 : Difficulty.BadgeWinRate(Tier, (float)Kills / (Kills + Deaths));
    }
    bool finalLogged;

    // ---------------- dev ----------------

    void DevReport(bool final)
    {
        var bs = string.Join(" ", bots.Select(b => $"{b.Kills}/{b.Deaths}{(b.Alive ? "" : "x")}:{b.State}:{b.Moved:0}m"));
        GD.Print($"[dm] {(final ? "FINAL " : "")}t={Now:0} you {Kills}/{Deaths} K/D {KD:0.00} HS {(Hits > 0 ? 100f * HeadHits / Hits : 0):0}% acc {Accuracy * 100:0}% | bots {bs} | " +
                 $"paths {nav.Paths} fail {nav.Failed} avg {(nav.Paths > 0 ? nav.PathMs / (nav.Paths + nav.Failed) : 0):0.00} ms (smooth {nav.SmoothMs:0} ms total, max {nav.MaxPathMs:0.0}) | stuck {bots.Sum(b => b.Stuck)} inwall {inWallFrames} spawnDeaths you {spawnDeaths} bots {botSpawnDeaths} (spawns not clean {compromisedSpawns}) botbot {botBotKills} packs {packsTaken}");
    }
}

