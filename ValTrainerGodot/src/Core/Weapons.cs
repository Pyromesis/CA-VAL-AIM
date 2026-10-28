namespace ValTrainer.Core;

/// <summary>Guns. Headhunter and TourDeForce are Chamber's ability guns (they reuse the Sheriff / Operator viewmodels and
/// behaviour, see <see cref="WeaponDef.Family"/>).</summary>
/// <para>Since 1.18 the whole VALORANT arsenal is here (appended, so saved replays keep their numbers): sidearms, SMGs,
/// shotguns, rifles, snipers, machine guns and the knife (<see cref="Weapons.Arsenal"/>).</para>
public enum WeaponKind
{
    None, Vandal, Phantom, Sheriff, Operator, Headhunter, TourDeForce,
    Classic, Shorty, Frenzy, Ghost, Bandit, Stinger, Spectre, Bucky, Judge, Bulldog, Guardian, Warden, Marshal, Outlaw, Ares, Odin, Knife,
}

/// <summary>VALORANT's shop categories (plus the knife).</summary>
public enum WeaponClass { Sidearm, Smg, Shotgun, Rifle, Sniper, Heavy, Melee }

/// <summary>
/// Weapon stats from the official wiki / valorant-api / patch notes (verified 20 Oct 2026, patch 13.08).
/// Phantom values match the official wiki: 11 rps, 30/60, 2.5 s reload, 1 s equip, 1.25× ADS, 156/39/33
/// inside 20 m and 140/35/29.75 beyond it; patch 11.08's eight protected bullets and 0.6 s side-switch are below.
/// The Vandal and Phantom recoil tables are their real spray patterns (measured points from SprayTrace: 9.0° climb and
/// 5.0° sweep over 25 rounds for the Vandal, 7.5° / 4.5° over 30 for the Phantom), applied as fixed per-bullet cumulative
/// offsets. The rest of the per-bullet recoil tables, and every spread table (including these two rifles'), are
/// RECONSTRUCTED from Riot's documented recoil rules (deterministic first bullets, vertical climb that plateaus,
/// side-locked horizontal drift with 10% swap chance after the protected bullets) — Riot never published per-bullet
/// numbers. All angles in degrees, cumulative from the aim point.
/// <para>RunSpeed (m/s) is the in-game "Run Speed" stat with the gun in hand, which the player's movement uses (Mover.Hold).
/// VALORANT's tiers: knife 6.75; Classic / Ghost / Frenzy / Stinger / Spectre 5.73; Vandal / Phantom / Sheriff / Shorty /
/// Bulldog / Guardian / Marshal / Outlaw 5.40; Operator / Odin / Ares 5.13; Bucky / Judge 5.06. Chamber's guns have no
/// published number: Headhunter uses the Sheriff's, Tour De Force the Operator's.</para>
/// </summary>
public sealed record WeaponDef(
    WeaponKind Kind, string Name,
    bool Auto, float Rps, float AdsRpsMul, int Mag, int Reserve, float Reload, float Equip,
    float FirstShotHip, float FirstShotAds, float MaxSpread,
    float RecoveryTime, float TapEfficiency, int ProtectedBullets, float YawSwitchTime,
    float Zoom, float[] Pitch, float[] Yaw, float[] Spread, int HeatCap,
    float RunErr, float WalkErr, float CrouchWalkErr, float AirErr,
    float RunSpeed, float[] DamageHead, float[] DamageBody, float[] DamageLegs, float[] DamageRangeEnd)
{
    public float Interval => 1f / Rps;

    /// <summary>Pellets per shot (shotguns; 1 for everything else). Each pellet is its own bullet inside a cone of
    /// <see cref="PelletSpread"/> degrees.</summary>
    public int Pellets { get; init; } = 1;
    public float PelletSpread { get; init; }
    /// <summary>Reach of the attack in metres (the knife: 2.5 m; guns: unlimited).</summary>
    public float Range { get; init; } = float.PositiveInfinity;
    public bool Melee => Range < 10f;
    public WeaponClass Class { get; init; } = WeaponClass.Rifle;
    /// <summary>Shop price (credits), shown in the arsenal.</summary>
    public int Cost { get; init; }
    /// <summary>Aimed / scoped move-speed multiplier from the data (null = the per-gun rule of <see cref="AdsMoveMul"/>).</summary>
    public float? AdsMoveOverride { get; init; }
    /// <summary>True when the recoil table is this project's reconstruction (Riot publishes no per-bullet numbers).</summary>
    public bool RecoilReconstructed { get; init; } = true;
    /// <summary>True when <see cref="Pitch"/> and <see cref="Yaw"/> are the real spray pattern: signed, cumulative and
    /// applied as-is, with no random side flip and no <c>sideBlend</c> (Vandal and Phantom).</summary>
    public bool FixedRecoil { get; init; }

    /// <summary>The base gun this one plays like (viewmodel, sounds, scope rules): Headhunter → Sheriff, Tour De Force → Operator.</summary>
    public WeaponKind Family => Weapons.FamilyOf(Kind);
    /// <summary>Sniper scope: the scope overlay, zoomed sensitivity, toggle-able scope, hip error = <see cref="FirstShotHip"/>.</summary>
    public bool Sniper => Family == WeaponKind.Operator;
    /// <summary>Bolt action (Operator only): the scope drops after every shot and the bolt cycles.</summary>
    public bool Bolt => Kind == WeaponKind.Operator;
    /// <summary>Max move speed while aimed / scoped, as a fraction of the run speed (valorant-api adsStats
    /// runSpeedMultiplier; Operator "decreased scoped movement speed 76% >>> 72%" = 3.69 m/s). Chamber's guns: no number,
    /// 0.76. Once added: Outlaw 0.80, Marshal 0.90 (patch 2.03).</summary>
    public float AdsMoveMul => AdsMoveOverride ?? (Kind == WeaponKind.Operator ? 0.72f : 0.76f);

    /// <summary>Damage for a hit zone at a distance (meters).</summary>
    public float Damage(int zone, float dist)
    {
        int i = 0;
        while (i < DamageRangeEnd.Length - 1 && dist > DamageRangeEnd[i]) i++;
        return zone switch { 0 => DamageHead[i], 1 => DamageBody[i], _ => DamageLegs[i] };
    }

    /// <summary>Linear interpolation into a per-bullet table at a fractional bullet index (0-based).</summary>
    public static float Lerp(float[] table, float index)
    {
        if (table.Length == 0) return 0;
        if (index <= 0) return table[0];
        int i = (int)index;
        if (i >= table.Length - 1) return table[^1];
        float f = index - i;
        return table[i] + (table[i + 1] - table[i]) * f;
    }
}

public static class Weapons
{
    static float[] Pad(float[] v, int n) { var r = new float[n]; for (int i = 0; i < n; i++) r[i] = v[Math.Min(i, v.Length - 1)]; return r; }

    public static readonly WeaponDef Vandal = new(
        WeaponKind.Vandal, "Vandal", Auto: true, Rps: 9.75f, AdsRpsMul: 0.9f, Mag: 25, Reserve: 50, Reload: 2.5f, Equip: 1.0f,
        FirstShotHip: 0.25f, FirstShotAds: 0.157f, MaxSpread: 1.0f,
        RecoveryTime: 0.375f, TapEfficiency: 6, ProtectedBullets: 6, YawSwitchTime: 0.6f, Zoom: 1.25f,
        Pitch: new[] { 0f, 1.07f, 2.13f, 3.2f, 4.14f, 4.97f, 5.68f, 6.28f, 6.75f, 7.11f, 7.46f, 7.7f, 7.93f, 8.05f, 8.17f, 8.29f, 8.41f, 8.53f, 8.64f, 8.64f, 8.76f, 8.76f, 8.88f, 8.88f, 9f },
        Yaw: new[] { 0f, 0f, 0f, 0f, 0.5f, 0.5f, 1f, 1.5f, 2f, 2f, 1.5f, 0.5f, -0.5f, -1.5f, -2.5f, -3f, -2f, -0.5f, 1f, 2f, 1f, -0.5f, -1.5f, -0.5f, 0.5f },
        Spread: Pad(new[] { 0.25f, 0.3f, 0.4f, 0.52f, 0.65f, 0.78f, 0.9f, 1.0f }, 25), HeatCap: 24,
        RunErr: 6f, WalkErr: 3f, CrouchWalkErr: 0.8f, AirErr: 10f, RunSpeed: 5.4f,
        DamageHead: new[] { 160f }, DamageBody: new[] { 40f }, DamageLegs: new[] { 34f }, DamageRangeEnd: new[] { 999f }) { Class = WeaponClass.Rifle, Cost = 2900, RecoilReconstructed = false, FixedRecoil = true };

    public static readonly WeaponDef Phantom = new(
        WeaponKind.Phantom, "Phantom", Auto: true, Rps: 11f, AdsRpsMul: 0.9f, Mag: 30, Reserve: 60, Reload: 2.5f, Equip: 1.0f,
        FirstShotHip: 0.2f, FirstShotAds: 0.11f, MaxSpread: 0.9f,
        RecoveryTime: 0.35f, TapEfficiency: 4, ProtectedBullets: 8, YawSwitchTime: 0.6f, Zoom: 1.25f,
        Pitch: new[] { 0f, 0.82f, 1.64f, 2.47f, 3.18f, 3.8f, 4.42f, 4.93f, 5.34f, 5.65f, 5.96f, 6.16f, 6.37f, 6.47f, 6.58f, 6.68f, 6.78f, 6.88f, 6.99f, 6.99f, 7.09f, 7.09f, 7.19f, 7.19f, 7.29f, 7.29f, 7.4f, 7.4f, 7.5f, 7.5f },
        Yaw: new[] { 0f, 0f, 0f, 0f, 0.64f, 0.64f, 1.29f, 1.29f, 1.93f, 1.93f, 1.29f, 0.64f, -0.64f, -1.29f, -1.93f, -2.57f, -1.93f, -0.64f, 0.64f, 1.93f, 1.29f, 0f, -1.29f, -1.93f, -0.64f, 0.64f, 0f, -0.64f, 0.64f, 0f },
        Spread: Pad(new[] { 0.2f, 0.23f, 0.3f, 0.38f, 0.47f, 0.56f, 0.65f, 0.73f, 0.8f, 0.86f, 0.9f }, 30), HeatCap: 29,
        RunErr: 6f, WalkErr: 3f, CrouchWalkErr: 0.8f, AirErr: 10f, RunSpeed: 5.4f,
        DamageHead: new[] { 156f, 140f }, DamageBody: new[] { 39f, 35f }, DamageLegs: new[] { 33f, 29.75f }, DamageRangeEnd: new[] { 20f, 999f }) { Class = WeaponClass.Rifle, Cost = 2900, RecoilReconstructed = false, FixedRecoil = true };

    public static readonly WeaponDef Sheriff = new(
        WeaponKind.Sheriff, "Sheriff", Auto: false, Rps: 4f, AdsRpsMul: 1f, Mag: 6, Reserve: 24, Reload: 2.25f, Equip: 1.0f,
        FirstShotHip: 0.25f, FirstShotAds: 0.25f, MaxSpread: 2.75f,
        RecoveryTime: 0.4f, TapEfficiency: 3, ProtectedBullets: 1, YawSwitchTime: 0.6f, Zoom: 1f,
        Pitch: new[] { 0f, 1.2f, 2.2f, 2.9f, 3.4f, 3.7f }, Yaw: new[] { 0f, 0.2f, 0.4f, 0.6f, 0.7f, 0.8f },
        Spread: new[] { 0.25f, 1.0f, 2.0f, 2.75f, 2.75f, 2.75f }, HeatCap: 5,
        RunErr: 3f, WalkErr: 1.2f, CrouchWalkErr: 0.5f, AirErr: 7f, RunSpeed: 5.4f,
        DamageHead: new[] { 159.5f, 145f }, DamageBody: new[] { 55f, 50f }, DamageLegs: new[] { 46.75f, 42.5f }, DamageRangeEnd: new[] { 30f, 999f }) { Class = WeaponClass.Sidearm, Cost = 800 };

    public static readonly WeaponDef Operator = new(
        WeaponKind.Operator, "Operator", Auto: false, Rps: 0.6f, AdsRpsMul: 1f, Mag: 5, Reserve: 10, Reload: 3.7f, Equip: 1.5f,
        FirstShotHip: 5f, FirstShotAds: 0f, MaxSpread: 5f,
        RecoveryTime: 1.6f, TapEfficiency: 1, ProtectedBullets: 1, YawSwitchTime: 0.6f, Zoom: 2.5f,
        Pitch: new[] { 0f }, Yaw: new[] { 0f }, Spread: new[] { 5f }, HeatCap: 1,
        RunErr: 10f, WalkErr: 6f, CrouchWalkErr: 3f, AirErr: 15f, RunSpeed: 5.13f,
        DamageHead: new[] { 255f }, DamageBody: new[] { 150f }, DamageLegs: new[] { 120f }, DamageRangeEnd: new[] { 999f }) { Class = WeaponClass.Sniper, Cost = 4700 };

    /// <summary>
    /// Chamber's Headhunter (official wiki, game-file values; patch 13.0x): semi-auto heavy pistol, 4 rps, 8 rounds and no
    /// reserve, 0.3 s equip, no damage falloff (159 head / 55 body / 46 legs), first shot 0.25° (ADS ×1.5: 0°), max spread
    /// 3° (ADS 2.75°) with the spread growing after the 2nd spammed bullet (patch 5.12), movement error crouch 0.1° / walk
    /// 1.2° / run 3° / air 7°. Recoil climb, recovery and the per-bullet spread curve are estimates (Sheriff-like).
    /// </summary>
    public static readonly WeaponDef Headhunter = new(
        WeaponKind.Headhunter, "Headhunter", Auto: false, Rps: 4f, AdsRpsMul: 1f, Mag: 8, Reserve: 0, Reload: 2.25f, Equip: 0.3f,
        FirstShotHip: 0.25f, FirstShotAds: 0f, MaxSpread: 3f,
        RecoveryTime: 0.4f, TapEfficiency: 3, ProtectedBullets: 2, YawSwitchTime: 0.6f, Zoom: 1.5f,
        Pitch: new[] { 0f, 0.9f, 1.8f, 2.5f, 3.0f, 3.3f, 3.5f, 3.6f }, Yaw: new[] { 0f, 0.15f, 0.3f, 0.45f, 0.55f, 0.6f, 0.65f, 0.7f },
        Spread: new[] { 0.25f, 0.25f, 1.4f, 2.4f, 3f, 3f, 3f, 3f }, HeatCap: 7,
        RunErr: 3f, WalkErr: 1.2f, CrouchWalkErr: 0.1f, AirErr: 7f, RunSpeed: 5.4f,
        DamageHead: new[] { 159f }, DamageBody: new[] { 55f }, DamageLegs: new[] { 46f }, DamageRangeEnd: new[] { 999f }) { Class = WeaponClass.Sidearm };

    /// <summary>
    /// Chamber's Tour De Force (official wiki; fire rate per patch 8.03): semi-auto sniper, 0.9 rps scoped or not, 5 rounds
    /// and no reserve, 0.667 s re-equip, 150 body (one-shots 150 HP) ×1.7 head / ×0.85 legs, hip error 5° (crouched 4.5°),
    /// scoped 0°, scope 2.5× (first level), movement error crouch 7.5° / walk 10° / run 15° / air 15°. Not bolt action:
    /// the scope stays up between shots (assumption).
    /// </summary>
    public static readonly WeaponDef TourDeForce = new(
        WeaponKind.TourDeForce, "Tour De Force", Auto: false, Rps: 0.9f, AdsRpsMul: 1f, Mag: 5, Reserve: 0, Reload: 3.7f, Equip: 0.667f,
        FirstShotHip: 5f, FirstShotAds: 0f, MaxSpread: 5f,
        RecoveryTime: 1.1f, TapEfficiency: 1, ProtectedBullets: 1, YawSwitchTime: 0.6f, Zoom: 2.5f,
        Pitch: new[] { 0f }, Yaw: new[] { 0f }, Spread: new[] { 5f }, HeatCap: 1,
        RunErr: 15f, WalkErr: 10f, CrouchWalkErr: 7.5f, AirErr: 15f, RunSpeed: 5.13f,
        DamageHead: new[] { 255f }, DamageBody: new[] { 150f }, DamageLegs: new[] { 127.5f }, DamageRangeEnd: new[] { 999f }) { Class = WeaponClass.Sniper };

    public static WeaponDef? Get(WeaponKind k) => k switch
    {
        WeaponKind.Vandal => Vandal, WeaponKind.Phantom => Phantom, WeaponKind.Sheriff => Sheriff,
        WeaponKind.Operator => Operator, WeaponKind.Headhunter => Headhunter, WeaponKind.TourDeForce => TourDeForce,
        WeaponKind.None => null,
        _ => Array.Find(Arsenal, w => w.Kind == k),
    };

    /// <summary>The base gun a kind plays like (see <see cref="WeaponDef.Family"/>): its viewmodel pose, scope rules and
    /// hand animations. Sidearms and the knife hold like the Sheriff, snipers like the Operator, the rest like the Vandal.</summary>
    public static WeaponKind FamilyOf(WeaponKind k) => k switch
    {
        WeaponKind.Headhunter => WeaponKind.Sheriff,
        WeaponKind.TourDeForce => WeaponKind.Operator,
        WeaponKind.Classic or WeaponKind.Shorty or WeaponKind.Frenzy or WeaponKind.Ghost or WeaponKind.Bandit or WeaponKind.Knife => WeaponKind.Sheriff,
        WeaponKind.Marshal or WeaponKind.Outlaw => WeaponKind.Operator,
        WeaponKind.Stinger or WeaponKind.Spectre or WeaponKind.Bucky or WeaponKind.Judge or WeaponKind.Bulldog or WeaponKind.Guardian
            or WeaponKind.Warden or WeaponKind.Ares or WeaponKind.Odin => WeaponKind.Vandal,
        _ => k,
    };

    /// <summary>Chamber's ability guns (gold finish on their family's model).</summary>
    public static bool IsAbilityGun(WeaponKind k) => k is WeaponKind.Headhunter or WeaponKind.TourDeForce;

    /// <summary>Gunshot sample for a kind: the closest of the four recorded CC0 gunshots ("vandal", "phantom", "sheriff",
    /// "operator"). There are no per-gun recordings in the repository (see CREDITS.md).</summary>
    public static string SoundOf(WeaponKind k) => k switch
    {
        WeaponKind.Phantom or WeaponKind.Spectre or WeaponKind.Stinger or WeaponKind.Ghost => "phantom",
        WeaponKind.Operator or WeaponKind.Marshal or WeaponKind.Outlaw or WeaponKind.TourDeForce => "operator",
        WeaponKind.Sheriff or WeaponKind.Headhunter or WeaponKind.Classic or WeaponKind.Frenzy or WeaponKind.Bandit or WeaponKind.Shorty => "sheriff",
        _ => "vandal",
    };

    // ------------------------------------------------------------------ the rest of the arsenal
    //
    // Fire rate, magazine, reload, equip, first-shot error (hip / aimed), zoom, aimed fire rate, run-speed multiplier
    // (x 6.75 m/s, the knife speed), aimed move multiplier, pellet count and every damage range are VALORANT's own numbers
    // as published by valorant-api.com (/v1/weapons, game-file data, fetched 24 Oct 2026); reserve ammo is the in-game
    // value. Riot publishes no per-bullet recoil, spread curve or movement error for these guns: those come from Recoil()
    // below, a reconstruction per weapon class following the documented rules (vertical climb that plateaus, side drift
    // after the first bullets, sidearms and SMGs forgiving on the move). Every one of them is marked RecoilReconstructed.

    const float KnifeSpeed = 6.75f;

    static (float[] Pitch, float[] Yaw, float[] Spread, int Heat, float Run, float Walk, float Crouch, float Air) Recoil(
        WeaponClass c, int mag, float firstHip, float climb, float plateau, float drift, float maxSpread)
    {
        int n = Math.Max(1, mag);
        var pitch = new float[n];
        var yaw = new float[n];
        var spread = new float[n];
        for (int i = 0; i < n; i++)
        {
            float u = i / Math.Max(1f, Math.Min(n - 1, 12f));
            pitch[i] = plateau * (1f - MathF.Exp(-i * climb / Math.Max(0.01f, plateau)));
            yaw[i] = i < 4 ? 0f : drift * MathF.Min(1f, (i - 3) / 8f);
            spread[i] = firstHip + (maxSpread - firstHip) * MathF.Min(1f, u);
        }
        int heat = Math.Max(1, Math.Min(n - 1, 14));
        return c switch
        {
            WeaponClass.Sidearm => (pitch, yaw, spread, heat, 3f, 1.2f, 0.5f, 7f),
            WeaponClass.Smg => (pitch, yaw, spread, heat, 2.5f, 1.5f, 0.6f, 6f),
            WeaponClass.Shotgun => (pitch, yaw, spread, heat, 2f, 1.2f, 0.6f, 5f),
            WeaponClass.Sniper => (pitch, yaw, spread, heat, 10f, 6f, 3f, 15f),
            WeaponClass.Heavy => (pitch, yaw, spread, heat, 6f, 3.5f, 1.5f, 12f),
            WeaponClass.Melee => (pitch, yaw, spread, heat, 0f, 0f, 0f, 0f),
            _ => (pitch, yaw, spread, heat, 6f, 3f, 0.8f, 10f),
        };
    }

    static WeaponDef Make(WeaponKind kind, string name, WeaponClass c, int cost, bool auto, float rps, float adsRps, int mag, int reserve,
        float reload, float equip, float firstHip, float firstAds, float zoom, float runMul, float adsMoveMul,
        float[] head, float[] body, float[] legs, float[] rangeEnd, float climb, float plateau, float drift, float maxSpread,
        int pellets = 1, float recovery = 0.4f, int protectedBullets = 1)
    {
        var r = Recoil(c, mag, firstHip, climb, plateau, drift, maxSpread);
        return new WeaponDef(kind, name, auto, rps, adsRps > 0 ? adsRps / rps : 1f, mag, reserve, reload, equip,
            firstHip, firstAds, maxSpread, recovery, TapEfficiency: 3, ProtectedBullets: protectedBullets, YawSwitchTime: 0.6f,
            Zoom: zoom, Pitch: r.Pitch, Yaw: r.Yaw, Spread: r.Spread, HeatCap: r.Heat,
            RunErr: r.Run, WalkErr: r.Walk, CrouchWalkErr: r.Crouch, AirErr: r.Air, RunSpeed: KnifeSpeed * runMul,
            DamageHead: head, DamageBody: body, DamageLegs: legs, DamageRangeEnd: rangeEnd)
        {
            Class = c, Cost = cost, AdsMoveOverride = adsMoveMul, Pellets = pellets,
            PelletSpread = pellets > 1 ? firstHip : 0f,
        };
    }

    static float[] A(params float[] v) => v;

    /// <summary>
    /// The rest of VALORANT's shop (Vandal, Phantom, Sheriff and Operator are the hand-tuned definitions above) plus the
    /// knife. Shotgun pellets are bullets of their own; the knife reaches 2.5 m.
    /// </summary>
    public static readonly WeaponDef[] Arsenal =
    {
        // ---- sidearms
        Make(WeaponKind.Classic, "Classic", WeaponClass.Sidearm, 0, false, 6.75f, 0, 12, 36, 1.75f, 0.75f, 0.4f, 0.4f, 1f, 0.85f, 0.76f,
            A(78, 66), A(26, 22), A(22.1f, 18.7f), A(30, 999), climb: 0.55f, plateau: 2.2f, drift: 0.5f, maxSpread: 2.0f),
        Make(WeaponKind.Shorty, "Shorty", WeaponClass.Sidearm, 300, false, 3.3f, 0, 2, 6, 1.75f, 0.75f, 4f, 4f, 1f, 0.8f, 0.76f,
            A(22, 12, 6), A(11, 6, 3), A(9.35f, 5.1f, 2.55f), A(7, 15, 999), climb: 1.2f, plateau: 2f, drift: 0.2f, maxSpread: 4f, pellets: 15),
        Make(WeaponKind.Frenzy, "Frenzy", WeaponClass.Sidearm, 450, true, 10f, 0, 15, 45, 1.5f, 1f, 0.65f, 0.65f, 1f, 0.85f, 0.76f,
            A(78, 63), A(26, 21), A(22.1f, 17.85f), A(20, 999), climb: 0.45f, plateau: 3.2f, drift: 1.2f, maxSpread: 3.5f),
        Make(WeaponKind.Ghost, "Ghost", WeaponClass.Sidearm, 500, false, 6.75f, 0, 13, 39, 1.5f, 0.75f, 0.3f, 0.3f, 1f, 0.85f, 0.76f,
            A(105, 87.5f), A(30, 25), A(25.5f, 21.25f), A(30, 999), climb: 0.5f, plateau: 2.2f, drift: 0.4f, maxSpread: 1.8f),
        Make(WeaponKind.Bandit, "Bandit", WeaponClass.Sidearm, 600, false, 5.1f, 0, 8, 24, 1.5f, 0.75f, 0.275f, 0.275f, 1f, 0.85f, 0.76f,
            A(152, 128, 112), A(39, 39, 34), A(33, 33, 28), A(10, 30, 999), climb: 0.8f, plateau: 2.6f, drift: 0.4f, maxSpread: 2.2f),
        // ---- SMGs
        Make(WeaponKind.Stinger, "Stinger", WeaponClass.Smg, 1100, true, 16f, 8.47f, 20, 60, 2.25f, 0.75f, 0.65f, 0.35f, 1.15f, 0.85f, 0.76f,
            A(67.5f, 57), A(27, 23), A(22.95f, 19), A(15, 999), climb: 0.35f, plateau: 2.8f, drift: 1.4f, maxSpread: 3.5f, protectedBullets: 3),
        Make(WeaponKind.Spectre, "Spectre", WeaponClass.Smg, 1600, true, 13.33f, 12f, 30, 90, 2.25f, 0.75f, 0.4f, 0.25f, 1.15f, 0.85f, 0.76f,
            A(78, 66, 60), A(26, 22, 20), A(22.1f, 18.7f, 17), A(15, 30, 999), climb: 0.3f, plateau: 2.4f, drift: 1.2f, maxSpread: 2.6f, protectedBullets: 4),
        // ---- shotguns
        Make(WeaponKind.Bucky, "Bucky", WeaponClass.Shotgun, 850, false, 1.1f, 0, 5, 10, 2.5f, 1f, 3f, 3f, 1f, 0.75f, 0.76f,
            A(34, 26, 18), A(17, 13, 9), A(14, 11.05f, 7.65f), A(8, 12, 999), climb: 1.5f, plateau: 3f, drift: 0.2f, maxSpread: 3f, pellets: 15),
        Make(WeaponKind.Judge, "Judge", WeaponClass.Shotgun, 1850, true, 3.5f, 0, 5, 15, 2.2f, 1f, 2.5f, 2.5f, 1f, 0.75f, 0.76f,
            A(34, 20, 14), A(17, 10, 7), A(14.45f, 8.5f, 5.95f), A(10, 15, 999), climb: 1.4f, plateau: 4f, drift: 0.8f, maxSpread: 2.5f, pellets: 12),
        // ---- rifles (Vandal and Phantom above)
        Make(WeaponKind.Bulldog, "Bulldog", WeaponClass.Rifle, 2050, true, 10f, 6.3157f, 24, 72, 2.5f, 1f, 0.3f, 0.3f, 1.25f, 0.8f, 0.76f,
            A(115.5f), A(35), A(29.75f), A(999), climb: 0.5f, plateau: 4.2f, drift: 2f, maxSpread: 1.5f, recovery: 0.375f, protectedBullets: 4),
        Make(WeaponKind.Guardian, "Guardian", WeaponClass.Rifle, 2250, false, 5.25f, 5.25f, 12, 36, 2.5f, 1f, 0.1f, 0f, 1.5f, 0.8f, 0.76f,
            A(195), A(65), A(48.75f), A(999), climb: 0.9f, plateau: 3f, drift: 0.3f, maxSpread: 1.2f, recovery: 0.36f),
        Make(WeaponKind.Warden, "Warden", WeaponClass.Rifle, 2900, true, 6.5f, 6.5f, 18, 54, 2.5f, 1f, 0.15f, 0.04f, 2f, 0.8f, 0.76f,
            A(200), A(50), A(42), A(999), climb: 0.8f, plateau: 3.6f, drift: 0.8f, maxSpread: 1.2f, recovery: 0.36f, protectedBullets: 2),
        // ---- snipers (Operator above)
        Make(WeaponKind.Marshal, "Marshal", WeaponClass.Sniper, 950, false, 1.5f, 1.2f, 5, 10, 2.5f, 1.25f, 1f, 0f, 3.5f, 0.8f, 0.9f,
            A(202), A(101), A(85.85f), A(999), climb: 1.5f, plateau: 1.5f, drift: 0f, maxSpread: 1f, recovery: 0.9f),
        Make(WeaponKind.Outlaw, "Outlaw", WeaponClass.Sniper, 2400, false, 2.75f, 2.75f, 2, 10, 3.8f, 1.25f, 3.5f, 0f, 3.5f, 0.8f, 0.8f,
            A(238), A(140), A(119), A(999), climb: 1.5f, plateau: 1.5f, drift: 0f, maxSpread: 3.5f, recovery: 0.8f),
        // ---- machine guns
        Make(WeaponKind.Ares, "Ares", WeaponClass.Heavy, 1600, true, 13f, 13f, 50, 100, 3.25f, 1.25f, 1f, 0.9f, 1.15f, 0.76f, 0.76f,
            A(75, 70), A(30, 28), A(25.5f, 23.8f), A(30, 999), climb: 0.25f, plateau: 2.6f, drift: 2.2f, maxSpread: 3f, recovery: 0.6f, protectedBullets: 3),
        Make(WeaponKind.Odin, "Odin", WeaponClass.Heavy, 3200, true, 12f, 15.6f, 100, 200, 5f, 1.25f, 0.8f, 0.79f, 1.15f, 0.76f, 0.76f,
            A(95, 77.5f), A(38, 31), A(32.3f, 26.35f), A(30, 999), climb: 0.25f, plateau: 2.8f, drift: 2.4f, maxSpread: 3.2f, recovery: 0.6f, protectedBullets: 3),
        // ---- knife: the fastest run speed in the game (6.75 m/s); primary slash 50, no headshot bonus.
        new WeaponDef(WeaponKind.Knife, "Knife", Auto: false, Rps: 2f, AdsRpsMul: 1f, Mag: 1, Reserve: 0, Reload: 0f, Equip: 0.5f,
            FirstShotHip: 0f, FirstShotAds: 0f, MaxSpread: 0f, RecoveryTime: 0.1f, TapEfficiency: 1, ProtectedBullets: 1, YawSwitchTime: 0.6f,
            Zoom: 1f, Pitch: new[] { 0f }, Yaw: new[] { 0f }, Spread: new[] { 0f }, HeatCap: 1,
            RunErr: 0f, WalkErr: 0f, CrouchWalkErr: 0f, AirErr: 0f, RunSpeed: KnifeSpeed,
            DamageHead: new[] { 50f }, DamageBody: new[] { 50f }, DamageLegs: new[] { 50f }, DamageRangeEnd: new[] { 999f })
        { Class = WeaponClass.Melee, Range = 2.5f, AdsMoveOverride = 1f },
    };

    /// <summary>Every weapon the player can hold, in shop order: sidearms, SMGs, shotguns, rifles, snipers, heavies, knife.</summary>
    public static WeaponDef[] AllPlayable => allPlayable ??= new[]
    {
        WeaponKind.Classic, WeaponKind.Shorty, WeaponKind.Frenzy, WeaponKind.Ghost, WeaponKind.Bandit, WeaponKind.Sheriff,
        WeaponKind.Stinger, WeaponKind.Spectre, WeaponKind.Bucky, WeaponKind.Judge,
        WeaponKind.Bulldog, WeaponKind.Guardian, WeaponKind.Warden, WeaponKind.Phantom, WeaponKind.Vandal,
        WeaponKind.Marshal, WeaponKind.Outlaw, WeaponKind.Operator, WeaponKind.Ares, WeaponKind.Odin, WeaponKind.Knife,
    }.Select(k => Get(k)!).ToArray();
    static WeaponDef[]? allPlayable;

    public static string ClassName(WeaponClass c) => c switch
    {
        WeaponClass.Sidearm => "Sidearm", WeaponClass.Smg => "SMG", WeaponClass.Shotgun => "Shotgun", WeaponClass.Rifle => "Rifle",
        WeaponClass.Sniper => "Sniper", WeaponClass.Heavy => "Machine gun", _ => "Melee",
    };
}
