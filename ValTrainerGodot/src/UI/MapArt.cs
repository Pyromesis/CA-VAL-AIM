using System.Collections.Generic;
using System.IO;
using Godot;
using ValTrainer.Core;
using ValTrainer.Maps;

namespace ValTrainer.UI;

/// <summary>
/// The official minimaps, used to simulate the real map layout inside ValTrainer: menu postcards show the
/// map around the played site, and the HUD/minimap draws the same art with the player, spawn and choke
/// markers over it. The blockouts remain true-scale single-site geometry; this art is the read of the map.
///
/// The PNGs live in <c>assets/maps/</c> (source and exported pck) with a copy in <c>maps/</c> next to the
/// executable as a fallback for installs that patch the DLL without re-exporting the pck. If a file is
/// missing every caller falls back to the palette art, so a missing minimap never breaks a screen.
///
/// Calibration: <see cref="Info"/> stores where world (0,0) lands in image pixels and the metres per pixel.
/// Ascent uses a top-down plan generated from the extracted real collision (ascent-real.png); the other
/// maps keep their official minimaps. The site rectangle is the real plant area in the game frame.
/// </summary>
public static class MapArt
{
    /// <summary>Where a map's world coordinates land in its minimap PNG.</summary>
    public sealed record Info(string Key, float OriginX, float OriginY, float Scale,
        string Start, string Site, float SiteX0, float SiteY0, float SiteX1, float SiteY1)
    {
        public Rect2 SitePixels => new(SiteX0, SiteY0, SiteX1 - SiteX0, SiteY1 - SiteY0);

        /// <summary>World metres to minimap pixels (north/-Z is up, east/+X is right).</summary>
        public Vector2 WorldToPixel(Vector3 world) => new(OriginX + world.X / Scale, OriginY + world.Z / Scale);

        public Vector3 PixelToWorld(float px, float py) => new((px - OriginX) * Scale, 0, (py - OriginY) * Scale);
    }

    // Site rectangles are the green plant areas detected in the official PNGs; origins put the average
    // defender spot on the centre of that area with the per-map scale the map files were traced with.
    static readonly Dictionary<string, Info> infos = new()
    {
        ["ascent"] = new("ascent", 608.38f, 415.62f, 0.083008f, "A MAIN", "A SITE", 271f, 235f, 548f, 380f),
        ["bind"] = new("bind", 783f, 412f, 0.1655f, "HOOKAH", "B SITE", 694f, 287f, 790f, 384f),
        ["breeze"] = new("breeze", 225f, 489f, 0.1395f, "A LOBBY", "A SITE", 101f, 253f, 186f, 338f),
        ["haven"] = new("haven", 301f, 948f, 0.1300f, "C LONG", "C SITE", 320f, 790f, 427f, 941f),
        ["icebox"] = new("icebox", 457f, 225f, 0.1360f, "GREEN", "B SITE", 559f, 165f, 699f, 285f),
        ["split"] = new("split", 295f, 926f, 0.1400f, "A MAIN", "A SITE", 294f, 779f, 374f, 915f),
        ["fracture"] = new("fracture", 517.8068f, 639.7500f, 0.0861057f, "A MAIN", "A SITE", 433.3550f, 303.3064f, 584.3323f, 454.2836f),
        ["abyss"] = new("abyss", 609.9637f, 538.4767f, 0.0944227f, "A MAIN", "A SITE", 345.1969f, 416.6839f, 482.8756f, 554.3627f),
        ["lotus"] = new("lotus", 434.3569f, 647.9629f, 0.0899951f, "A MAIN", "A SITE", 517.4222f, 303.8067f, 661.8745f, 448.2590f),
        ["sunset"] = new("sunset", 453.9318f, 593.2955f, 0.0861057f, "A MAIN", "A SITE", 494.5795f, 355.2159f, 645.5568f, 506.1932f),
        ["pearl"] = new("pearl", 418.6018f, 525.1330f, 0.0933904f, "A MAIN", "A SITE", 535.8007f, 429.2694f, 675.0013f, 568.4700f),
        ["summit"] = new("summit", 370.9118f, 615.1485f, 0.1054305f, "A MAIN", "A SITE", 591.4362f, 347.1995f, 714.7401f, 470.5035f),
        ["corrode"] = new("corrode", 655.6452f, 395.4355f, 0.1061644f, "A MAIN", "A SITE", 307.1292f, 567.3387f, 429.5808f, 689.7903f),
    };

    static readonly Dictionary<string, Texture2D?> textures = new();

    /// <summary>Releases the cached minimap textures on shutdown so Godot does not report them as leaked.</summary>
    public static void Clear()
    {
        foreach (var tex in textures.Values) tex?.Dispose();
        textures.Clear();
    }
    static bool warned;

    public static Info? At(string? key) => key != null && infos.TryGetValue(key, out var i) ? i : null;

    public static Texture2D? Get(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (textures.TryGetValue(key, out var cached)) return cached;
        var tex = Load(key);
        textures[key] = tex;
        if (tex == null && !warned)
        {
            warned = true;
            GD.PushWarning("[maps] minimap art not found; using palette postcards. Copy assets/maps/*.png next to the executable.");
        }
        return tex;
    }

    /// <summary>The start area / site names shown on the postcards and the HUD ("A MAIN", "A SITE").</summary>
    public static string StartName(string? key) => At(key)?.Start ?? "START";
    public static string SiteName(string? key) => At(key)?.Site ?? "SITE";

    static Texture2D? Load(string key)
    {
        // The generated real Ascent plan is preferred over the official PNG; it is aligned with the
        // extracted collision and the mirroring in <see cref="ValTrainer.World.RealMap"/>.
        var real = LoadFile(key + "-real.png");
        if (real != null) return real;
        return LoadFile(key + ".png");
    }

    static Texture2D? LoadFile(string file)
    {
        var roots = new List<string>
        {
            ProjectSettings.GlobalizePath("res://assets/maps"),
            ProjectSettings.GlobalizePath("res://maps"),
            ProjectSettings.GlobalizePath("res://"),
            OS.GetExecutablePath().GetBaseDir() + "/maps",
            OS.GetExecutablePath().GetBaseDir() + "/assets/maps",
            Directory.GetCurrentDirectory() + "/assets/maps",
            Directory.GetCurrentDirectory() + "/maps",
        };
        foreach (var root in roots)
        {
            try
            {
                string path = Path.Combine(root, file);
                if (!File.Exists(path)) continue;
                var img = Image.LoadFromFile(path);
                if (img == null || img.IsEmpty()) continue;
                return ImageTexture.CreateFromImage(img);
            }
            catch { /* try the next root */ }
        }
        return null;
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>
    /// Source rectangle of the site and its surroundings, expanded to the target aspect. Extra world points
    /// (spawn, choke, player) are folded in so the approach stays inside the crop when the minimap has room.
    /// </summary>
    public static Rect2 Region(string key, float aspect, float grow = 2.2f, params Vector3[] include)
    {
        var info = At(key) ?? infos["ascent"];
        var site = info.SitePixels;
        var box = site;
        foreach (var inc in include) box = box.Merge(new Rect2(info.WorldToPixel(inc), Vector2.Zero));
        var c = box.GetCenter();
        float w = Mathf.Max(site.Size.X * grow, box.Size.X + 36f);
        float h = Mathf.Max(site.Size.Y * grow, box.Size.Y + 36f);
        if (w / h < aspect) w = h * aspect; else h = w / aspect;
        const float size = 1024f;
        w = Mathf.Min(w, size);
        h = Mathf.Min(h, size);
        float x = Mathf.Clamp(c.X - w / 2, 0, size - w);
        float y = Mathf.Clamp(c.Y - h / 2, 0, size - h);
        return new Rect2(x, y, w, h);
    }

    /// <summary>Draws the site and its surroundings (cover crop) into <paramref name="rect"/>.</summary>
    public static bool DrawRegion(CanvasItem ci, Rect2 rect, string key, Color tint, float grow = 2.2f)
    {
        var tex = Get(key);
        if (tex == null) return false;
        var src = Region(key, rect.Size.X / Mathf.Max(1f, rect.Size.Y), grow);
        ci.DrawTextureRectRegion(tex, rect, src, tint);
        return true;
    }

    /// <summary>Draws the whole minimap contained in <paramref name="rect"/>; returns the drawn area.</summary>
    public static Rect2 DrawFull(CanvasItem ci, Rect2 rect, string key, Color tint)
    {
        var tex = Get(key);
        if (tex == null) return rect;
        var size = tex.GetSize();
        float scale = Mathf.Min(rect.Size.X / size.X, rect.Size.Y / size.Y);
        var dest = new Rect2(rect.Position + (rect.Size - size * scale) / 2, size * scale);
        ci.DrawTextureRect(tex, dest, false, tint);
        return dest;
    }

    /// <summary>World point to screen inside a panel that draws <see cref="Region"/>.</summary>
    public static Vector2 WorldToScreen(string key, Rect2 panel, Vector3 world, float grow = 2.2f)
    {
        var src = Region(key, panel.Size.X / Mathf.Max(1f, panel.Size.Y), grow);
        var info = At(key) ?? infos["ascent"];
        var p = info.WorldToPixel(world);
        return panel.Position + new Vector2((p.X - src.Position.X) / src.Size.X * panel.Size.X,
                                            (p.Y - src.Position.Y) / src.Size.Y * panel.Size.Y);
    }

    /// <summary>
    /// A HUD "tactical screen": site art, the site plant area highlighted, the spawn/choke markers, an
    /// optional player arrow and optional defender callouts. Returns false when the minimap is missing.
    /// </summary>
    public static bool Panel(CanvasItem ci, Rect2 rect, MapSpot map, float k, bool callouts,
        bool player, Vector3 eye, float yaw, float alpha = 1f)
    {
        var info = At(map.Key);
        var tex = Get(map.Key);
        if (info == null || tex == null) return false;

        var tint = new Color(0.94f, 0.97f, 1f, 0.96f * alpha);
        Gfx.Plate(ci, rect, 10 * k, new Color(0.03f, 0.05f, 0.075f, 0.82f * alpha), new Color(UiTheme.Text, 0.18f * alpha));
        var inner = rect.Grow(-5 * k);
        var src = player
            ? Region(map.Key, inner.Size.X / Mathf.Max(1f, inner.Size.Y), 2.2f, map.StartFeet, map.Choke, eye)
            : Region(map.Key, inner.Size.X / Mathf.Max(1f, inner.Size.Y), 2.2f, map.StartFeet, map.Choke);
        ci.DrawTextureRectRegion(tex, inner, src, tint);

        // site plant area: teal wash + corners
        var a = ToScreen(info, src, inner, info.PixelToWorld(info.SitePixels.Position.X, info.SitePixels.Position.Y));
        var b = ToScreen(info, src, inner, info.PixelToWorld(info.SitePixels.End.X, info.SitePixels.End.Y));
        var site = new Rect2(a, b - a).Abs();
        ci.DrawRect(site, new Color(UiTheme.Teal, 0.22f * alpha));
        ci.DrawRect(site, new Color(UiTheme.Teal, 0.75f * alpha), false, Mathf.Max(1f, 1.5f * k));
        Gfx.Brackets(ci, site.Grow(2 * k), 7 * k, new Color(UiTheme.Teal, 0.85f * alpha), Mathf.Max(1f, 2 * k));

        // approach route: spawn -> choke -> centre of the site (schematic, drawn on top of the art)
        var spawnRaw = ToScreen(info, src, inner, map.StartFeet);
        var chokeRaw = ToScreen(info, src, inner, map.Choke);
        var centre = ToScreen(info, src, inner, info.PixelToWorld(info.SitePixels.GetCenter().X, info.SitePixels.GetCenter().Y));
        var route = new[] { ClampTo(inner, spawnRaw), ClampTo(inner, chokeRaw), ClampTo(inner, centre) };
        ci.DrawPolyline(route, new Color(UiTheme.Teal, 0.55f * alpha), Mathf.Max(1f, 2 * k), true);

        // choke marker and site badge (on a small plate so the callout labels never fight it)
        Gfx.Diamond(ci, chokeRaw, 4.5f * k, 4.5f * k, new Color(UiTheme.Accent, 0.95f * alpha));
        var used = new List<Rect2>();
        int ls = UiTheme.Fs(12, k);
        float siteW = Gfx.TextW(UiTheme.HudWide, info.Site, ls);
        var siteLabel = new Rect2(new Vector2(site.GetCenter().X - siteW / 2 - 6 * k, site.Position.Y - 8 * k),
                                  new Vector2(siteW + 12 * k, ls + 8 * k));
        ci.DrawRect(siteLabel, new Color(0.02f, 0.03f, 0.05f, 0.78f * alpha));
        ci.DrawRect(siteLabel, new Color(UiTheme.Teal, 0.7f * alpha), false, Mathf.Max(1f, 1.2f * k));
        ci.DrawString(UiTheme.HudWide, siteLabel.Position + new Vector2(6 * k, ls + 1 * k), info.Site,
            HorizontalAlignment.Left, -1, ls, new Color(UiTheme.Text, 0.95f * alpha));
        used.Add(siteLabel.Grow(3 * k));

        // start marker (hidden when the spawn sits outside the minimap crop, e.g. Haven C Long and Split)
        if (inner.Grow(8 * k).HasPoint(spawnRaw))
        {
            var s = new Rect2(spawnRaw - new Vector2(5 * k, 5 * k), new Vector2(10 * k, 10 * k));
            ci.DrawRect(s, new Color(0.02f, 0.03f, 0.05f, 0.9f * alpha));
            ci.DrawRect(s, new Color(UiTheme.Good, 0.95f * alpha), false, Mathf.Max(1f, 1.6f * k));
        }

        if (callouts)
        {
            int cs = UiTheme.Fs(11, k);
            foreach (var e in map.Enemies)
            {
                var p = ToScreen(info, src, inner, e.Feet);
                if (!inner.Grow(6 * k).HasPoint(p)) continue;
                Gfx.Diamond(ci, p, 3 * k, 3 * k, new Color(UiTheme.Accent, 0.75f * alpha));
                string text = e.Name.ToUpperInvariant();
                float tw = Gfx.TextW(UiTheme.HudWide, text, cs);
                var lp = p + new Vector2(6 * k, 4 * k);
                if (lp.X + tw > inner.End.X - 2 * k) lp.X = p.X - 6 * k - tw;
                var box = new Rect2(lp.X, lp.Y - cs, tw, cs * 1.25f);
                bool clash = false;
                foreach (var u in used) if (u.Intersects(box)) { clash = true; break; }
                if (clash) continue;
                used.Add(box.Grow(1.5f * k));
                ci.DrawStringOutline(UiTheme.HudWide, lp, text,
                    HorizontalAlignment.Left, -1, cs, Mathf.Max(1, cs / 7), new Color(0.02f, 0.03f, 0.05f, 0.85f * alpha));
                ci.DrawString(UiTheme.HudWide, lp, text,
                    HorizontalAlignment.Left, -1, cs, new Color(UiTheme.Text, 0.92f * alpha));
            }
        }

        if (player)
        {
            var p = ClampTo(inner, ToScreen(info, src, inner, eye));
            // yaw 0 looks to -Z (up on the art); rotate the triangle with the view direction
            var f = new Vector2(-Mathf.Sin(Mathf.DegToRad(yaw)), -Mathf.Cos(Mathf.DegToRad(yaw)));
            var right = new Vector2(-f.Y, f.X);
            var tip = p + f * 8 * k;
            var l = p - f * 4 * k - right * 5 * k;
            var r = p - f * 4 * k + right * 5 * k;
            ci.FillPoly(new[] { tip, l, r }, new Color(UiTheme.Text, 0.98f * alpha));
            ci.Polyline(new[] { tip, l, r, tip }, new Color(0.02f, 0.03f, 0.05f, 0.9f * alpha), Mathf.Max(1f, 1.4f * k));
        }

        return true;
    }

    static Vector2 ToScreen(Info info, Rect2 src, Rect2 panel, Vector3 world)
    {
        var p = info.WorldToPixel(world);
        return panel.Position + new Vector2((p.X - src.Position.X) / src.Size.X * panel.Size.X,
                                            (p.Y - src.Position.Y) / src.Size.Y * panel.Size.Y);
    }

    static Vector2 ClampTo(Rect2 r, Vector2 p) => new(Mathf.Clamp(p.X, r.Position.X + 2f, r.End.X - 2f),
                                                     Mathf.Clamp(p.Y, r.Position.Y + 2f, r.End.Y - 2f));
}
