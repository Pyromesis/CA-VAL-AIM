using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Fracture, A Main → A Site, generated from the real extracted art/collision.
    /// The GLBs and collision.json are external, under <c>maps/fracture/</c>; the floor below
    /// is only the fallback for installs without those optional files.
    /// </summary>
    public static readonly MapSpot Fracture = new MapSpot
    {
        Key = "fracture", Map = "Fracture", Name = "A Main → A Site",
        Drill = "Clear Link, Rope, Site, Drop, Site 2, Site 3, Site 4",
        StartFeet = V(4.000f, 0.300f, -5.600f), StartYaw = -15.79f, Choke = V(-0.479f, 0.300f, -13.931f),
        Palette = new MapPalette(C(120,116,108), C(197,181,153), C(186,91,55), C(171,126,80), C(170,170,162), C(170,198,216)),
        RealMapFolder = "maps/fracture",
        // Only used when the external real-map files are absent.
        Boxes = new[]
        {
            B(-80f, -0.1f, -80f, 80f, 0.2f, 80f, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Link", -21.490f, 1.100f, -26.992f, Stance.Stand),
            E("Rope", -23.983f, -0.500f, -7.600f, Stance.Stand),
            E("Site", -0.772f, 0.300f, -22.470f, Stance.Stand),
            E("Drop", -4.450f, 2.300f, -33.850f, Stance.Stand),
            E("Site 2", 2.228f, 0.300f, -22.470f, Stance.Stand),
            E("Site 3", -3.772f, 0.300f, -21.650f, Stance.Stand),
            E("Site 4", -0.772f, 0.300f, -25.470f, Stance.Stand),
        },
        Flashes = new[]
        {
            F("Pop over the A Main entrance", V(4.000f, 1.900f, -5.600f), V(-0.424f, 1.500f, -12.358f)),
            F("High pop into the site", V(4.000f, 1.900f, -5.600f), V(-0.772f, 2.300f, -22.470f)),
            F("Curve around the choke", V(-0.479f, 1.900f, -13.931f), V(-0.695f, 1.700f, -20.223f)),
            F("Deep pop toward the back", V(4.000f, 1.900f, -5.600f), V(-3.772f, 2.500f, -24.470f)),
            F("Pop right of the site", V(4.000f, 1.900f, -5.600f), V(3.228f, 1.700f, -22.470f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Link", V(-21.490f, 1.100f, -26.992f)),
            new SmokeSpot("Rope", V(-23.983f, -0.500f, -7.600f)),
            new SmokeSpot("Site", V(-0.772f, 0.300f, -22.470f)),
        },
        PlantSpots = new[]
        {
            V(-0.772f, 0.300f, -22.470f),
            V(1.428f, 0.300f, -24.670f),
            V(-2.972f, 0.300f, -21.650f),
            V(-2.772f, 0.300f, -24.470f),
        },
        RetakeEntries = new[]
        {
            V(-21.490f, 1.100f, -26.992f),
            V(-4.450f, 2.300f, -33.850f),
            V(-19.000f, 1.100f, -27.106f),
        },
    };
}
