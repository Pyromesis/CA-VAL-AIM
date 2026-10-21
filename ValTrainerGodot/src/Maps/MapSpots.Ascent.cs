using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Ascent, A Main → A Site, using the real map geometry extracted from the installed game.
    ///
    /// Coordinates are metres in the final game frame: gx = 50 − ueY/100, gy = ueZ/100 − 3.8,
    /// gz = 61 − ueX/100. This is a proper 90° rotation of the extracted Unreal world, so the map is not
    /// mirrored and its signs/decals read the right way round. The textured GLBs and the collision file
    /// live outside the distribution in <c>maps/ascent/</c>; the small neutral floor below is only a
    /// fallback for installs without those optional files.
    ///
    /// Walkable heights sampled from the generated collision: A Main 0.2, site 1.2–1.4, Heaven 4.2.
    /// The site is entered through the A Main arch at (0, 0); Generator is south of the site, Tetris,
    /// Pillar and Back Site north/west, Hell under Heaven, A Link/Tree on the defender route.
    /// </summary>
    public static readonly MapSpot Ascent = new MapSpot
    {
        Key = "ascent", Map = "Ascent", Name = "A Main → A Site",
        Drill = "Clear Wine, Boxes, A Switch, A Link/Tree, Generator, Tetris, Pillar, Back Site, Hell, Heaven and Heaven Pillar",
        StartFeet = V(4.0f, 0.2f, 7.0f), StartYaw = -30, Choke = V(0, 0.2f, 0),
        Palette = new MapPalette(C(124, 114, 102), C(208, 184, 150), C(172, 96, 66), C(204, 170, 124), C(180, 172, 160), C(160, 190, 220)),
        RealMapFolder = "maps/ascent",
        // Only used if the external real-map assets are absent. The real collision file overrides this.
        Boxes = new[]
        {
            B(-80, -0.1f, -80, 80, 0.2f, 80, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Generator", -16.5f, 1.2f, 4.8f),
            E("Behind Gen", -18.8f, 1.2f, 0f, Stance.Crouch),
            E("Tetris", -15.0f, 1.4f, -9.5f, Stance.Crouch),
            E("Pillar", -23.5f, 1.2f, -7.2f),
            E("Back Site", -25.0f, 1.2f, -9.2f, Stance.Crouch),
            E("Hell", -28.0f, 1.2f, 0f),
            E("Heaven", -32.0f, 4.2f, 0f),
            E("Heaven Pillar", -32.2f, 4.2f, -1.5f),
            E("Tree", -18.65f, 0.6f, 21.2f),
            E("A Link", -8.0f, 0.2f, 22.0f),
            E("A Switch", -6.0f, 0.2f, 8.5f, Stance.Crouch),
            E("Boxes", -4.0f, 0.2f, 2.4f),
            E("Wine", -9.2f, 0.2f, 9.2f, Stance.Crouch),
        },
        Flashes = new[]
        {
            F("Pop through the A Main arch", V(2.0f, 1.8f, 6.0f), V(-2.0f, 2.4f, -2.0f)),
            F("High pop over the site stairs", V(1.0f, 1.8f, 5.0f), V(-10.0f, 3.6f, -9.0f)),
            F("Curve around A Peek, left side", V(5.0f, 1.8f, 7.0f), V(-7.0f, 2.6f, -4.0f)),
            F("Deep pop toward Heaven", V(3.0f, 1.8f, 4.0f), V(-30.0f, 4.2f, 0.0f)),
            F("Pop right toward Tree / A Link", V(-5.0f, 1.8f, 8.0f), V(-18.0f, 2.0f, 21.0f)),
        },
        // Standard A execute: Heaven, Tree (the A Link door) and Generator.
        Smokes = new[]
        {
            new SmokeSpot("Heaven", V(-30f, 4.2f, 0f)),
            new SmokeSpot("Tree", V(-18.65f, 0.6f, 21.2f)),
            new SmokeSpot("Generator", V(-16.5f, 1.2f, 4.8f)),
        },
        // Spike: open site between Tetris and Generator, safe behind Generator, back site by Pillar.
        PlantSpots = new[] { V(-15f, 1.4f, -9.5f), V(-21f, 1.2f, -8f), V(-25f, 1.2f, -9.2f) },
        // Retakes enter from the defender side via A Link / Tree / behind Heaven.
        RetakeEntries = new[] { V(-8f, 0.2f, 22f), V(-37f, 0.2f, 25.25f), V(-34.25f, 1.2f, 0f) },
    };
}
