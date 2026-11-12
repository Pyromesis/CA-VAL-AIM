using Godot;

namespace CaValAim.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Bind, B Short / Hookah into B Site, on the extracted Valorant art.
    /// The map is shifted so BombDestination_B is at the origin, Y is up. The spots below were authored on the
    /// old extraction, which came out mirrored east-west (see tools/mapgen/compose_map.py); <see cref="MapSpot.Mirrored"/>
    /// flips them onto the corrected map (manifest origin_x negated), so in game +X is east like on the minimap:
    /// attackers come up B Short, enter Hookah (south-east of the site), climb to the window platform and drop
    /// through the window onto the site. Elevator sits mid-site, Hall behind the site to the north, Elbow west,
    /// Garden and B Long south-west.
    /// </summary>
    public static readonly MapSpot Bind = new MapSpot
    {
        Key = "bind", Map = "Bind", Name = "Hookah -> B Site",
        Drill = "Climb Hookah, swing the window and drop in: clear Under Window, Default, Elevator, Back Site, Hall, Elbow and Garden",
        // Hookah's visible upper floor; the old start resolved onto hidden ground underneath it.
        StartFeet = V(-7.25f, 2.5f, 32.25f), StartYaw = 7.5f, Choke = V(-5.25f, 2.5f, 17.0f),
        RealMapFolder = "maps/bind",
        // The Hookah window: the art opens it from y=3.05 (a 0.55 m sill over the 2.5 m floor), but the extracted
        // collision included the sill as a slab up to y=3.5 and a 0.5 m lip outside it, so a standing jump hit an
        // invisible wall. Cut both above the real sill; authored in the pre-mirror frame like every other point.
        Cuts = new[] { Box3(-7.08f, 3.06f, 16.3f, -3.42f, 3.7f, 17.6f) },
        Palette = new MapPalette(C(178, 150, 112), C(222, 190, 142), C(178, 98, 62), C(170, 124, 82), C(44, 128, 134), C(165, 195, 220)),
        // Only used when the external real-map files are absent: a floor at site level so the drills still run.
        Boxes = new[] { B(-80f, -0.1f, -80f, 80f, 0.6f, 80f, Surf.Floor) },
        Enemies = new[]
        {
            E("Under Window", -5.0f, 0.60f, 14.5f, Stance.Crouch),
            E("Default", 5.0f, 0.60f, 0.0f),
            E("Elevator", 4.5f, 1.40f, 3.0f),
            E("Back Site", -2.0f, 0.60f, -5.0f),
            E("Hall Door", 0.25f, 0.40f, -9.25f),
            E("Hall", 0.25f, 0.40f, -11.25f),
            E("Elbow", 21.8f, 0.60f, 7.0f),
            E("Back Elbow", 18.0f, 0.60f, 12.0f),
            E("Garden", 6.8f, 0.60f, 27.6f),
            E("B Long", 16.0f, -2.15f, 42.4f),
        },
        Flashes = new[]
        {
            F("B Short / Hookah window pop", V(-5.25f, 4.1f, 20.5f), V(-5.25f, 3.8f, 14.0f)),
            F("Short window curve left", V(-5.25f, 4.1f, 20.5f), V(-1.5f, 3.6f, 13.0f)),
            F("Short window curve right", V(-5.25f, 4.1f, 20.5f), V(-8.0f, 3.6f, 13.0f)),
            F("High pop from short", V(-5.25f, 4.1f, 20.5f), V(-5.25f, 5.0f, 13.0f)),
            F("Short entry pop", V(-5.25f, 4.1f, 20.5f), V(-3.5f, 3.8f, 11.0f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Elbow", V(21.6f, 0.60f, 7.0f)),
            new SmokeSpot("Hall", V(0.3f, -1.60f, -8.0f)),
            new SmokeSpot("Garden", V(7.0f, 0.60f, 27.0f)),
        },
        PlantSpots = new[] { V(5.0f, 0.60f, 0.0f), V(2.0f, 0.05f, -3.0f), V(-2.0f, 0.60f, -5.0f) },
        RetakeEntries = new[] { V(-36.2f, -1.65f, -31.9f), V(0.75f, 0.40f, -11.25f), V(6.8f, 0.60f, 27.6f) },
    }.Mirrored();
}
