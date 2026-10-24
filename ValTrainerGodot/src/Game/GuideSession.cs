using Godot;
using ValTrainer.Core;
using ValTrainer.Game.Bots;

namespace ValTrainer.Game;

/// <summary>
/// The guide (Settings → Gameplay → Guide line): while a drill runs it shows the optimal way to the nearest target.
/// <list type="bullet">
/// <item><b>Aim line</b> (HUD, <see cref="UI.Hud"/>): a straight line from the crosshair to the target the drill is
/// focused on (<see cref="Modes.TrainingMode.Focus"/>, always the one closest to the crosshair). A straight flick along
/// it is the shortest crosshair path; when the target is behind you an arrow on the screen edge shows which way to turn.</item>
/// <item><b>Route</b> (map drills, this file): when no enemy is in sight, a ribbon on the floor along the shortest walk
/// (the bots' navigation grid, <see cref="BotNav"/>) to the nearest enemy still alive (<see cref="Modes.TrainingMode.GuideTargets"/>).</item>
/// </list>
/// Both are training aids: they never change a drill's rules or its scoring.
/// </summary>
public partial class GameSession
{
    /// <summary>0 = off, 1 = aim line, 2 = aim line + route (map drills).</summary>
    public int GuideMode => Demo ? 0 : Math.Clamp(Main.I.Settings.GuideLine, 0, 2);

    const float RouteEvery = 0.35f;     // s between path refreshes
    const float RouteLift = 0.07f;      // ribbon height over the floor (m)
    const float RouteWidth = 0.16f;
    const float RouteMaxLength = 70f;   // only the first stretch is drawn (m)

    MeshInstance3D? routeMesh;
    ImmediateMesh? routeGeo;
    Task<List<Vector3>?>? routeTask;
    Task<BotNav>? routeNavTask;
    BotNav? routeNav;
    List<Vector3>? routePath;
    float routeAt = -99f;
    Vector3 routeFrom;

    /// <summary>Feet of the target the route leads to (null = no route on screen).</summary>
    public Vector3? RouteTarget { get; private set; }

    /// <summary>Called every frame after the camera moved.</summary>
    void UpdateGuide()
    {
        bool want = GuideMode >= 2 && State == St.Running && Mode.Map is { } && !Mode.Is2D && !Player.Dead && Mode.Focus == null;
        if (!want)
        {
            HideRoute();
            return;
        }
        var feet = new Vector3(View.Eye.X, feetY, View.Eye.Z);
        if (routeNav == null)
        {
            // The grid is the bots' own; it is cached per map for the whole process. Build it off the frame.
            var spot = Mode.Map!;
            var world = solid;
            routeNavTask ??= Task.Run(() => BotNav.For($"{spot.Key}:{world.Count}", world, spot.StartFeet));
            if (!routeNavTask.IsCompleted) return;
            if (routeNavTask.IsFaulted) { routeNavTask = null; return; }
            routeNav = routeNavTask.Result;
        }

        if (routeTask is { IsCompleted: true } done)
        {
            routePath = done.IsCompletedSuccessfully ? done.Result : null;
            routeTask = null;
            DrawRoute(routeFrom);
        }
        if (routeTask == null && (Now - routeAt >= RouteEvery || feet.DistanceTo(routeFrom) > 1.5f))
        {
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (var t in Mode.GuideTargets)
            {
                float d = t.DistanceSquaredTo(feet);
                if (d < bestD) { bestD = d; best = t; }
            }
            RouteTarget = best;
            if (best is not { } target)
            {
                HideRoute();
                return;
            }
            routeAt = Now;
            routeFrom = feet;
            routeTask = routeNav.FindPathAsync(feet, target);
        }
    }

    void HideRoute()
    {
        RouteTarget = null;
        routePath = null;
        if (routeMesh != null && IsInstanceValid(routeMesh)) routeMesh.Visible = false;
    }

    void DrawRoute(Vector3 from)
    {
        if (routePath is not { Count: > 0 } path || routeNav == null)
        {
            if (routeMesh != null && IsInstanceValid(routeMesh)) routeMesh.Visible = false;
            return;
        }
        if (routeMesh == null || !IsInstanceValid(routeMesh) || routeMesh.GetParent() != World)
        {
            routeGeo = new ImmediateMesh();
            routeMesh = new MeshInstance3D
            {
                Name = "GuideRoute",
                Mesh = routeGeo,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = RouteMaterial(),
            };
            World.AddChild(routeMesh);
        }

        // Resample the smoothed waypoints every 0.4 m and lay each sample on the walk grid's floor.
        var pts = new List<Vector3> { from };
        pts.AddRange(path);
        var samples = new List<Vector3>();
        float walked = 0;
        for (int i = 0; i + 1 < pts.Count && walked < RouteMaxLength; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            float len = new Vector2(b.X - a.X, b.Z - a.Z).Length();
            int n = Math.Max(1, (int)(len / 0.4f));
            for (int k = 0; k < n && walked < RouteMaxLength; k++)
            {
                var p = a.Lerp(b, k / (float)n);
                int node = routeNav.At(p, 1.2f);
                float y = node >= 0 ? routeNav.Pos[node].Y : p.Y;
                samples.Add(new Vector3(p.X, y + RouteLift, p.Z));
                walked += len / n;
            }
        }
        var end = pts[^1];
        samples.Add(end + new Vector3(0, RouteLift, 0));
        if (samples.Count < 2)
        {
            routeMesh.Visible = false;
            return;
        }

        routeGeo!.ClearSurfaces();
        routeGeo.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        float total = Math.Max(0.01f, walked);
        float along = 0;
        for (int i = 0; i + 1 < samples.Count; i++)
        {
            Vector3 a = samples[i], b = samples[i + 1];
            var dir = new Vector3(b.X - a.X, 0, b.Z - a.Z);
            float len = dir.Length();
            if (len < 1e-3f) continue;
            var side = new Vector3(-dir.Z, 0, dir.X) / len * (RouteWidth * 0.5f);
            // Fade in over the first metre (it starts under your feet) and out toward the end of the drawn stretch.
            float fa = Mathf.Clamp(along / 1.0f, 0, 1) * Mathf.Clamp((total - along) / 6f + 0.25f, 0, 1);
            along += len;
            float fb = Mathf.Clamp(along / 1.0f, 0, 1) * Mathf.Clamp((total - along) / 6f + 0.25f, 0, 1);
            var ca = new Color(1, 1, 1, fa);
            var cb = new Color(1, 1, 1, fb);
            Quad(a - side, a + side, b + side, b - side, ca, cb);
        }
        // Arrow head at the target end.
        {
            Vector3 a = samples[^2], b = samples[^1];
            var dir = new Vector3(b.X - a.X, 0, b.Z - a.Z);
            if (dir.LengthSquared() > 1e-6f)
            {
                dir = dir.Normalized();
                var side = new Vector3(-dir.Z, 0, dir.X);
                var tip = b + dir * 0.45f;
                var c = new Color(1, 1, 1, 0.9f);
                routeGeo.SurfaceSetColor(c); routeGeo.SurfaceAddVertex(b + side * 0.32f);
                routeGeo.SurfaceSetColor(c); routeGeo.SurfaceAddVertex(tip);
                routeGeo.SurfaceSetColor(c); routeGeo.SurfaceAddVertex(b - side * 0.32f);
            }
        }
        routeGeo.SurfaceEnd();
        routeMesh.Visible = true;

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color c0, Color c1)
        {
            routeGeo.SurfaceSetColor(c0); routeGeo.SurfaceAddVertex(p0);
            routeGeo.SurfaceSetColor(c0); routeGeo.SurfaceAddVertex(p1);
            routeGeo.SurfaceSetColor(c1); routeGeo.SurfaceAddVertex(p2);
            routeGeo.SurfaceSetColor(c0); routeGeo.SurfaceAddVertex(p0);
            routeGeo.SurfaceSetColor(c1); routeGeo.SurfaceAddVertex(p2);
            routeGeo.SurfaceSetColor(c1); routeGeo.SurfaceAddVertex(p3);
        }
    }

    static StandardMaterial3D? routeMaterial;

    static StandardMaterial3D RouteMaterial() => routeMaterial ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        VertexColorUseAsAlbedo = true,
        AlbedoColor = new Color(UI.UiTheme.Teal, 0.78f),
        NoDepthTest = false,
        RenderPriority = 1,
    };

    /// <summary>A new run: the old ribbon belongs to the freed world.</summary>
    void ResetGuide()
    {
        routeMesh = null;
        routeGeo = null;
        routeTask = null;
        routePath = null;
        routeAt = -99f;
        RouteTarget = null;
        routeNav = null; // the map may have changed; BotNav.For returns the cached grid at once when it has not
        routeNavTask = null;
    }
}
