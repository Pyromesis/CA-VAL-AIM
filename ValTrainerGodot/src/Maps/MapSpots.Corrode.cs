using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Corrode, A Main → A Site, generated from the real extracted art/collision.
    /// The GLBs and collision.json are external, under <c>maps/corrode/</c>; the floor below
    /// is only the fallback for installs without those optional files.
    /// </summary>
    public static readonly MapSpot Corrode = new MapSpot
    {
        Key = "corrode", Map = "Corrode", Name = "A Main → A Site",
        Drill = "Clear Crane, Elbow, Link, Pocket, Site, Yard, Site 2",
        StartFeet = V(-5.350f, -2.800f, 14.950f), StartYaw = -111.29f, Choke = V(-18.250f, 1.200f, 11.350f),
        Palette = new MapPalette(C(110,104,94), C(190,170,142), C(72,142,112), C(156,112,72), C(170,164,150), C(186,178,154)),
        RealMapFolder = "maps/corrode",
        // Only used when the external real-map files are absent.
        Boxes = new[]
        {
            B(-80f, -0.1f, -80f, 80f, 0.2f, 80f, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Crane", -53.850f, -0.600f, 18.150f, Stance.Stand),
            E("Elbow", -30.950f, -0.800f, 5.850f, Stance.Stand),
            E("Link", -27.700f, -0.800f, 38.950f, Stance.Stand),
            E("Pocket", -21.450f, -0.800f, 5.250f, Stance.Crouch),
            E("Site", -30.500f, -0.600f, 24.750f, Stance.Stand),
            E("Yard", -18.250f, 1.200f, 11.350f, Stance.Stand),
            E("Site 2", -27.500f, -0.600f, 24.750f, Stance.Stand),
        },
        Flashes = new[]
        {
            F("Pop over the A Main entrance", V(-5.350f, -1.200f, 14.950f), V(-16.775f, 0.600f, 13.613f)),
            F("High pop into the site", V(-5.350f, -1.200f, 14.950f), V(-30.500f, 1.400f, 24.750f)),
            F("Curve around the choke", V(-18.250f, 2.800f, 11.350f), V(-27.450f, 0.800f, 22.275f)),
            F("Deep pop toward the back", V(-5.350f, -1.200f, 14.950f), V(-33.500f, 1.600f, 22.750f)),
            F("Pop right of the site", V(-5.350f, -1.200f, 14.950f), V(-26.500f, 0.800f, 24.750f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Link", V(-27.700f, -0.800f, 38.950f)),
            new SmokeSpot("Elbow", V(-30.950f, -0.800f, 5.850f)),
            new SmokeSpot("Crane", V(-53.850f, -0.600f, 18.150f)),
        },
        PlantSpots = new[]
        {
            V(-30.500f, -0.600f, 24.750f),
            V(-26.750f, -0.600f, 21.050f),
            V(-32.700f, -0.600f, 26.950f),
            V(-32.500f, -0.600f, 22.750f),
        },
        RetakeEntries = new[]
        {
            V(-30.950f, -0.800f, 5.850f),
            V(-27.700f, -0.800f, 38.950f),
            V(-54.350f, -0.200f, 46.140f),
        },
    };
}
