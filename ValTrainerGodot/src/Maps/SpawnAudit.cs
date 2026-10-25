using System.Globalization;
using System.Text;
using Godot;
using ValTrainer.Game;
using ValTrainer.Game.Bots;
using ValTrainer.Modes;

namespace ValTrainer.Maps;

/// <summary>
/// Dev tool (<c>--spawn-dump &lt;file.json&gt; [--spawn-maps a,b]</c>, works with --headless): runs the game's own spawn
/// code over every map — the drill start (<see cref="MapMode.SafeFeet"/>), every enemy / plant / retake spot
/// (<see cref="SpawnSafety"/>), the Deathmatch navigation grid and its spawn pool (<see cref="DeathmatchMode.SpawnPool"/>) —
/// and writes the results to JSON. <c>tools/mapgen/audit_spawns.py</c> then checks every point against the map's real
/// art (is there visible floor under it, is it under the map, inside a wall) and measures where the collision lets a body
/// walk through art walls.
/// </summary>
public static class SpawnAudit
{
    static string F(float v) => float.IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "null";
    static string V(Vector3 v) => $"[{F(v.X)},{F(v.Y)},{F(v.Z)}]";

    public static int Run(string file, string? only)
    {
        var keys = only?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.Append("{\"maps\":{");
        bool firstMap = true;
        int problems = 0;
        foreach (var spot in MapSpots.All)
        {
            if (keys != null && !keys.Contains(spot.Key)) continue;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var solid = spot.Solid;
            bool real = World.RealMap.HasArt(spot.RealMapFolder);
            var playable = MapMode.PlayableTest(spot);
            if (!firstMap) sb.Append(',');
            firstMap = false;
            sb.Append($"\"{spot.Key}\":{{\"real\":{(real ? "true" : "false")},\"boxes\":{solid.Count},\"bottom\":{F(Collision.Bottom(solid))}");

            var start = SpawnSafety.Resolve(spot.StartFeet, solid, out bool startOk, playable);
            if (!startOk) problems++;
            sb.Append($",\"start\":{{\"authored\":{V(spot.StartFeet)},\"resolved\":{V(start)},\"ok\":{(startOk ? "true" : "false")}}}");

            void Points(string name, IEnumerable<(string Name, Vector3 P)> pts)
            {
                sb.Append($",\"{name}\":[");
                bool first = true;
                foreach (var (n, p) in pts)
                {
                    var r = SpawnSafety.Resolve(p, solid, out bool ok);
                    if (!ok) problems++;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append($"{{\"name\":\"{n.Replace("\"", "'")}\",\"authored\":{V(p)},\"resolved\":{V(r)},\"ok\":{(ok ? "true" : "false")}}}");
                }
                sb.Append(']');
            }
            Points("enemies", spot.Enemies.Select(e => (e.Name, e.Feet)));
            Points("plants", spot.PlantSpots.Select((p, i) => ($"plant{i}", p)));
            Points("retakes", spot.RetakeEntries.Select((p, i) => ($"retake{i}", p)));

            var nav = BotNav.For($"{spot.Key}:{solid.Count}", solid, start);
            var pool = DeathmatchMode.SpawnPool(nav, solid, spot, out string why);
            int inside = 0, outside = 0;
            if (playable != null)
                for (int i = 0; i < nav.Count; i++) { if (playable(nav.Pos[i]) == 1) inside++; else outside++; }
            sb.Append($",\"nav\":{{\"count\":{nav.Count},\"buildMs\":{F(nav.BuildMs)},\"insideMask\":{inside},\"outsideMask\":{outside},\"why\":\"{why}\",\"pos\":[");
            for (int i = 0; i < nav.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var p = nav.Pos[i];
                sb.Append(F(p.X)).Append(',').Append(F(p.Y)).Append(',').Append(F(p.Z));
            }
            sb.Append("],\"pool\":[");
            for (int i = 0; i < pool.Length; i++) { if (i > 0) sb.Append(','); sb.Append(pool[i]); }
            sb.Append("]}}");
            GD.Print($"[spawn-audit] {spot.Key}: real={real} boxes={solid.Count} start {(startOk ? "ok" : "NOT OK")} {V(spot.StartFeet)} -> {V(start)}; " +
                     $"nav {nav.Count} nodes in {nav.BuildMs:0} ms ({inside} inside / {outside} outside the minimap); pool {pool.Length}: {why}; {sw.ElapsedMilliseconds} ms");
        }
        sb.Append("}}");
        try
        {
            File.WriteAllText(file, sb.ToString());
            GD.Print($"[spawn-audit] wrote {file}; {problems} spot(s) without a safe floor nearby");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[spawn-audit] could not write {file}: {e.Message}");
            return 1;
        }
        return 0;
    }
}
