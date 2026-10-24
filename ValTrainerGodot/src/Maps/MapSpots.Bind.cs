using Godot;

namespace ValTrainer.Maps;

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
        StartFeet = V(-6.08f, 0.6f, 30.81f), StartYaw = 11, Choke = V(-6.08f, 1.9f, 30.81f),
        RealMapFolder = "maps/bind",
        Palette = new MapPalette(C(178, 150, 112), C(222, 190, 142), C(178, 98, 62), C(170, 124, 82), C(44, 128, 134), C(165, 195, 220)),
        // Only used when the external real-map files are absent: a floor at site level so the drills still run.
        Boxes = new[] { B(-80f, -0.1f, -80f, 80f, 0.6f, 80f, Surf.Floor) },
        Enemies = new[]
        {
            E("Under Window", -5.0f, 0.60f, 27.5f, Stance.Crouch),
            E("Default", 5.0f, 0.60f, 0.0f),
            E("Elevator", 4.5f, 1.40f, 3.0f),
            E("Back Site", -2.0f, 0.60f, -5.0f),
            E("Hall Door", 0.3f, -1.60f, -9.5f),
            E("Hall", 0.3f, -1.60f, -12.5f),
            E("Elbow", 21.8f, 0.60f, 7.0f),
            E("Back Elbow", 18.0f, 0.60f, 12.0f),
            E("Garden", 6.8f, 0.60f, 27.6f),
            E("B Long", 16.0f, -2.15f, 42.4f),
        },
        Flashes = new[]
        {
            F("Hookah window pop", V(0.5f, 3.6f, 3.0f), V(0, 3.4f, -3.5f)),
            F("Window curve left", V(1.5f, 3.6f, 2.5f), V(-4.5f, 3.2f, -4.0f)),
            F("Window curve right", V(-1.5f, 3.6f, 2.5f), V(3.0f, 3.4f, -5.0f)),
            F("High pop out of the window", V(0.0f, 3.6f, 1.5f), V(-2.0f, 6.0f, -7.0f)),
            F("Deep pop over Elevator", V(1.0f, 3.6f, 1.5f), V(-3.0f, 5.2f, -10.0f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Elbow", V(21.6f, 0.60f, 7.0f)),
            new SmokeSpot("Hall", V(0.3f, -1.60f, -8.0f)),
            new SmokeSpot("Garden", V(7.0f, 0.60f, 27.0f)),
        },
        PlantSpots = new[] { V(5.0f, 0.60f, 0.0f), V(2.0f, 0.05f, -3.0f), V(-2.0f, 0.60f, -5.0f) },
        RetakeEntries = new[] { V(-36.2f, -1.65f, -31.9f), V(0.3f, -1.60f, -12.0f), V(6.8f, 0.60f, 27.6f) },
    }.Mirrored();
}
