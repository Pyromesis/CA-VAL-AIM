using System.Globalization;
using Godot;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Game.Bots;
using ValTrainer.UI;
using ValTrainer.Valorant;

namespace ValTrainer.Modes;

/// <summary>
/// Arsenal Range: every VALORANT weapon (sidearms, SMGs, shotguns, rifles, snipers, machine guns, knife) on a range with
/// dummies at 10, 20, 30 and 50 m. Q / E (Ability 1 / 2) cycle the weapons, the Equip keys jump to a rifle, a sidearm or
/// the knife. The panel shows the weapon's real numbers (damage per range, fire rate, magazine, run speed, price) and the
/// shots / time to kill a 150 HP (100 + heavy shields) target at each dummy's distance; every hit shows its damage.
/// Movement follows the gun in hand (run speed 6.75 m/s with the knife, 5.73 sidearms/SMGs, 5.40 rifles, 5.13 heavies).
/// Nothing is graded: it is a place to learn the guns.
/// </summary>
public sealed class ArsenalMode : BotMode
{
    public override string Key => "arsenal";
    public override string Name => "Arsenal Range";
    public override string Description => "Every VALORANT weapon with its real damage, fire rate and run speed. Q / E change gun; dummies at 10–50 m show the damage and time to kill.";
    public override bool Timed => false;
    public override bool Movement => true;
    public override bool ShowScore => false;
    public override bool RifleSelectable => false;
    public override WeaponKind Weapon => start;
    public override bool InfiniteAmmo => false;
    public override bool InfiniteReserve => true;
    public override string SimKind => "bot";

    static readonly float[] Distances = { 10f, 20f, 30f, 50f };
    // Dev "--weapon odin": start with that gun (viewmodel screenshots).
    readonly WeaponKind start = Enum.TryParse<WeaponKind>(CmdLine.DevAfter("--weapon") ?? "", true, out var dw) && Weapons.Get(dw) != null ? dw
        : Main.I?.Settings.PrimaryRifle == 1 ? WeaponKind.Phantom : WeaponKind.Vandal;
    readonly BotCharacter?[] dummies = new BotCharacter?[Distances.Length];
    readonly float[] respawnAt = new float[Distances.Length], lastHitAt = new float[Distances.Length];
    readonly List<(float T, string Text, Color Col)> hits = new();
    int index;
    bool q, e, two, three, one;
    float bannerUntil;

    public override IEnumerable<Box> ExtraSolids
    {
        get
        {
            // low lane dividers and a back wall so misses leave marks
            yield return new Box(new Vector3(-14f, 0f, -56f), new Vector3(14f, 5f, -55.6f));
            for (int i = 0; i < Distances.Length; i++)
                yield return new Box(new Vector3(-9.5f + i * 6f - 1.4f, 0f, -Distances[i] - 0.9f), new Vector3(-9.5f + i * 6f + 1.4f, 0.55f, -Distances[i] - 0.6f));
        }
    }

    Vector3 SpotOf(int i) => new(-9.5f + i * 6f, 0f, -Distances[i]);

    protected override void Setup()
    {
        var all = Weapons.AllPlayable;
        index = Math.Max(0, Array.FindIndex(all, w => w.Kind == start));
        for (int i = 0; i < Distances.Length; i++) SpawnDummy(i);
    }

    void SpawnDummy(int i)
    {
        var b = G.SpawnBot(SpotOf(i));
        b.FacingYaw = 180f;
        dummies[i] = b;
        respawnAt[i] = -1f;
    }

    void Equip(int next)
    {
        var all = Weapons.AllPlayable;
        index = (next % all.Length + all.Length) % all.Length;
        if (G is GameSession s) s.SwitchWeapon(all[index].Kind);
        bannerUntil = Now + 1.4f;
        G.Sound("tick", 0.5f, 1.2f);
    }

    public override void Update(float dt)
    {
        var binds = Main.I.Valorant.Binds;
        bool qn = binds.IsDown(GameAction.Ability1), en = binds.IsDown(GameAction.Ability2);
        bool on = binds.IsDown(GameAction.EquipPrimary), twn = binds.IsDown(GameAction.EquipSecondary), thn = binds.IsDown(GameAction.EquipMelee);
        var all = Weapons.AllPlayable;
        if (qn && !q) Equip(index - 1);
        if (en && !e) Equip(index + 1);
        if (on && !one) Equip(Array.FindIndex(all, w => w.Kind == start));
        if (twn && !two) Equip(Array.FindIndex(all, w => w.Kind == WeaponKind.Classic));
        if (thn && !three) Equip(Array.FindIndex(all, w => w.Kind == WeaponKind.Knife));
        q = qn; e = en; one = on; two = twn; three = thn;

        for (int i = 0; i < dummies.Length; i++)
        {
            var b = dummies[i];
            if (b == null || !GodotObject.IsInstanceValid(b) || b.Dead)
            {
                if (respawnAt[i] < 0) respawnAt[i] = Now + 1.2f;
                else if (Now >= respawnAt[i]) SpawnDummy(i);
                continue;
            }
            b.Velocity = Vector3.Zero;
            // armour and health come back after 2.5 s without damage (like taking a fresh duel)
            if (Now - lastHitAt[i] > 2.5f && b.Hp < BotCharacter.MaxHp) b.Hp = BotCharacter.MaxHp;
        }
        hits.RemoveAll(h => Now - h.T > 4f);
    }

    public override float OnBullet(Vector3 o, Vector3 d, bool accurate)
    {
        Shots++;
        var live = dummies.Where(b => b != null && GodotObject.IsInstanceValid(b) && !b.Dead).Select(b => b!).ToList();
        int hpBefore = 0;
        foreach (var b in live) hpBefore += b.Hp;
        var hit = ShootBots(live, o, d, out var zone, out bool killed, out float dist);
        if (hit == null) return float.PositiveInfinity;
        int i = Array.IndexOf(dummies, hit);
        if (i >= 0) lastHitAt[i] = Now;
        var w = G.Weapon!;
        float dmg = w.Damage((int)zone, dist);
        string z = zone switch { HitZone.Head => "HEAD", HitZone.Legs => "LEGS", _ => "BODY" };
        hits.Add((Now, $"{z} {dmg:0} @ {dist:0} m{(killed ? "  ·  KILL" : "")}", zone == HitZone.Head ? UiTheme.Warn : killed ? UiTheme.Good : UiTheme.Text));
        if (hits.Count > 6) hits.RemoveAt(0);
        return dist;
    }

    /// <summary>Shots and time to kill a 150 HP target (100 HP + heavy shields) at a distance, with every hit in one zone.</summary>
    static (int Shots, float Ms) Ttk(WeaponDef w, int zone, float dist)
    {
        float per = w.Damage(zone, dist) * Math.Max(1, w.Pellets);
        if (per <= 0) return (99, float.PositiveInfinity);
        int n = (int)Mathf.Ceil(150f / per);
        return (n, (n - 1) * w.Interval * 1000f);
    }

    public override IEnumerable<string> HudLines()
    {
        var w = Weapons.AllPlayable[index];
        yield return $"{w.Name} · {Weapons.ClassName(w.Class)} · Q / E change weapon";
    }

    public override void Draw2D(CanvasItem c, Vector2 size)
    {
        float k = size.Y / 1080f;
        var w = Weapons.AllPlayable[index];
        var inv = CultureInfo.InvariantCulture;
        float pw = 420 * k, x = size.X - pw - 28 * k, y = 150 * k;
        int hs = UiTheme.Fs(30, k), ls = UiTheme.Fs(13, k), vs = UiTheme.Fs(16, k);
        var panel = new Rect2(x, y, pw, 400 * k);
        c.DrawRect(panel, new Color(0.03f, 0.04f, 0.055f, 0.78f));
        c.DrawRect(new Rect2(x, y, 3 * k, panel.Size.Y), UiTheme.Accent);
        float ty = y + 40 * k;
        Gfx.Text(c, UiTheme.DisplayWide, w.Name.ToUpperInvariant(), x + 22 * k, ty, hs, UiTheme.Text);
        Gfx.TextR(c, UiTheme.HudWide, w.Cost > 0 ? $"¤ {w.Cost}" : "FREE", x + pw - 20 * k, ty - 6 * k, ls, UiTheme.Warn);
        ty += 24 * k;
        Gfx.Text(c, UiTheme.HudWide, $"{Weapons.ClassName(w.Class).ToUpperInvariant()}  ·  {index + 1} / {Weapons.AllPlayable.Length}", x + 22 * k, ty, ls, UiTheme.Faint);
        ty += 30 * k;
        void Row(string label, string value)
        {
            Gfx.Text(c, UiTheme.Body, label, x + 22 * k, ty, vs, UiTheme.Dim);
            Gfx.TextR(c, UiTheme.BodySemi, value, x + pw - 20 * k, ty, vs, UiTheme.Text);
            ty += 26 * k;
        }
        if (w.Melee)
        {
            Row(Loc.Spanish ? "Daño" : "Damage", Loc.Spanish ? "50 por corte · 2,5 m de alcance" : "50 per slash · 2.5 m reach");
            Row(Loc.Spanish ? "Velocidad al correr" : "Run speed", Loc.Spanish ? $"{w.RunSpeed.ToString("0.00", inv)} m/s (la más rápida)" : $"{w.RunSpeed.ToString("0.00", inv)} m/s (fastest)");
        }
        else
        {
            bool es = Loc.Spanish;
            string dmg = string.Join("  ", Enumerable.Range(0, w.DamageRangeEnd.Length).Select(i =>
                $"{w.DamageHead[i]:0}/{w.DamageBody[i]:0}/{w.DamageLegs[i]:0}" + (w.DamageRangeEnd[i] < 900 ? $" ≤{w.DamageRangeEnd[i]:0}m" : "")));
            Row(es ? "Cabeza / cuerpo / piernas" : "Head / body / legs", dmg + (w.Pellets > 1 ? $" ×{w.Pellets}" : ""));
            Row(es ? "Cadencia" : "Fire rate", $"{w.Rps.ToString("0.##", inv)} /s{(w.Auto ? (es ? " auto" : " auto") : (es ? " semiauto" : " semi"))}{(w.Zoom > 1 ? $" · {w.Zoom.ToString("0.##", inv)}× {(es ? "zoom" : "zoom")}" : "")}");
            Row(es ? "Cargador / recarga" : "Magazine / reload", $"{w.Mag} · {w.Reload.ToString("0.##", inv)} s");
            Row(es ? "Velocidad al correr" : "Run speed", es
                ? $"{w.RunSpeed.ToString("0.00", inv)} m/s · apuntando {(w.RunSpeed * w.AdsMoveMul).ToString("0.00", inv)}"
                : $"{w.RunSpeed.ToString("0.00", inv)} m/s · aimed {(w.RunSpeed * w.AdsMoveMul).ToString("0.00", inv)}");
            Row(es ? "Primer disparo" : "First shot", es
                ? $"{w.FirstShotHip.ToString("0.###", inv)}° cadera{(w.Zoom > 1 ? $" · {w.FirstShotAds.ToString("0.###", inv)}° apuntando" : "")}"
                : $"{w.FirstShotHip.ToString("0.###", inv)}° hip{(w.Zoom > 1 ? $" · {w.FirstShotAds.ToString("0.###", inv)}° aimed" : "")}");
            ty += 6 * k;
            Gfx.Text(c, UiTheme.HudWide, es ? "TIEMPO DE BAJA (150 HP)" : "TIME TO KILL (150 HP)", x + 22 * k, ty, ls, UiTheme.Faint);
            ty += 24 * k;
            foreach (float d in Distances)
            {
                var (hn, hms) = Ttk(w, 0, d);
                var (bn, bms) = Ttk(w, 1, d);
                Gfx.Text(c, UiTheme.Body, $"{d:0} m", x + 22 * k, ty, vs, UiTheme.Dim);
                Gfx.TextR(c, UiTheme.BodySemi, es ? $"cabeza {hn} ({hms:0} ms)   cuerpo {bn} ({bms:0} ms)" : $"head {hn} ({hms:0} ms)   body {bn} ({bms:0} ms)", x + pw - 20 * k, ty, vs, UiTheme.TextSoft);
                ty += 24 * k;
            }
        }
        if (w.RecoilReconstructed)
            Gfx.TextFit(c, UiTheme.Body, Loc.Spanish ? "Patrón de retroceso reconstruido (Riot no publica ninguno)" : "Recoil pattern reconstructed (Riot publishes none)", x + 22 * k, panel.End.Y - 14 * k, UiTheme.Fs(12, k), UiTheme.Faint, pw - 40 * k);

        // hit log under the crosshair
        float hy = size.Y * 0.62f;
        for (int i = hits.Count - 1, n = 0; i >= 0 && n < 5; i--, n++)
        {
            var (t, text, col) = hits[i];
            float a = Mathf.Clamp(1f - (Now - t) / 4f, 0, 1);
            int fs = UiTheme.Fs(n == 0 ? 22 : 16, k);
            Gfx.TextC(c, UiTheme.Display, text, size.X / 2, hy + n * 26 * k, fs, new Color(col, a));
        }
        if (Now < bannerUntil)
        {
            float a = Mathf.Clamp((bannerUntil - Now) / 0.4f, 0, 1);
            Gfx.TextC(c, UiTheme.DisplayWide, w.Name.ToUpperInvariant(), size.X / 2, size.Y * 0.30f, UiTheme.Fs(40, k), new Color(UiTheme.Text, a));
        }
    }

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        yield return ("Shots", Shots.ToString());
        yield return ("Hits", $"{Hits} ({Accuracy * 100:0}%)");
        if (Hits > 0) yield return ("Headshots", $"{100f * HeadHits / Hits:0}%");
    }
}
