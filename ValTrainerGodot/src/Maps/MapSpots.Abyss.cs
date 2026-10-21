using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Abyss, A Main → A Site, generated from the real extracted art/collision.
    /// The GLBs and collision.json are external, under <c>maps/abyss/</c>; the floor below
    /// is only the fallback for installs without those optional files.
    /// </summary>
    public static readonly MapSpot Abyss = new MapSpot
    {
        Key = "abyss", Map = "Abyss", Name = "A Main → A Site",
        Drill = "Clear Bridge, Link, Site, Tower, Secret, Security, Vent",
        StartFeet = V(6.900f, -1.550f, 5.000f), StartYaw = -68.51f, Choke = V(-10.250f, -0.950f, -4.950f),
        Palette = new MapPalette(C(84,96,106), C(142,166,178), C(52,142,150), C(84,126,132), C(164,170,170), C(150,180,204)),
        RealMapFolder = "maps/abyss",
        // Only used when the external real-map files are absent.
        Boxes = new[]
        {
            B(-80f, -0.1f, -80f, 80f, 0.2f, 80f, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Bridge", -20.250f, -1.550f, -19.650f, Stance.Stand),
            E("Link", -41.000f, 0.450f, 10.000f, Stance.Stand),
            E("Site", -18.500f, -1.550f, -5.000f, Stance.Stand),
            E("Tower", -17.750f, 2.450f, 7.750f, Stance.Stand),
            E("Secret", -48.400f, -1.550f, -10.000f, Stance.Crouch),
            E("Security", -46.650f, -0.550f, -13.950f, Stance.Stand),
            E("Vent", -15.550f, 2.450f, 25.700f, Stance.Stand),
        },
        Flashes = new[]
        {
            F("Pop over the A Main entrance", V(6.900f, 0.050f, 5.000f), V(-10.175f, -0.350f, -2.750f)),
            F("High pop into the site", V(6.900f, 0.050f, 5.000f), V(-18.500f, 0.450f, -5.000f)),
            F("Curve around the choke", V(-10.250f, 0.650f, -4.950f), V(-16.650f, -0.150f, -4.500f)),
            F("Deep pop toward the back", V(6.900f, 0.050f, 5.000f), V(-21.500f, 0.650f, -7.000f)),
            F("Pop right of the site", V(6.900f, 0.050f, 5.000f), V(-14.500f, -0.150f, -5.000f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Link", V(-41.000f, 0.450f, 10.000f)),
            new SmokeSpot("Tower", V(-17.750f, 2.450f, 7.750f)),
            new SmokeSpot("Security", V(-46.650f, -0.550f, -13.950f)),
        },
        PlantSpots = new[]
        {
            V(-18.500f, -1.550f, -5.000f),
            V(-16.300f, -1.550f, -8.550f),
            V(-20.700f, -1.550f, -2.800f),
            V(-21.750f, -1.550f, -7.000f),
        },
        RetakeEntries = new[]
        {
            V(-41.000f, 0.450f, 10.000f),
            V(-17.750f, 2.450f, 7.750f),
            V(-48.400f, -1.550f, -10.000f),
        },
    };
}
