namespace CaValAim.Core;

/// <summary>Numeric regression checks for the game sensitivity converter.</summary>
public static class GameSensitivityChecks
{
    public static void Run()
    {
        Check(Near(GameSensitivity.Cm360(.07, .225, 800), 914.4 / (.07 * .225 * 800)), "VALORANT cm/360 at 800 DPI");
        Check(Near(GameSensitivity.SensitivityForCm(.07, 914.4 / (.07 * .225 * 800), 800), .225), "cm/360 reverses to source sensitivity");
        Check(Near(GameSensitivity.Cm360(.07, .225, 400), 914.4 / (.07 * .225 * 400)), "cm/360 scales inversely with DPI");
        Check(Near(GameSensitivity.Convert(.225, .07, .07, 800), .225), "same game and yaw is an identity conversion");
        Check(Near(GameSensitivity.Convert(.225, .07, .14, 800), .1125), "doubling yaw halves the target sensitivity");
        Check(Near(GameSensitivity.CmPerScreenWidth(1920, 1, 800), 1920.0 * 2.54 / 800), "osu raw-input cm per screen width");
        Check(Near(GameSensitivity.SensitivityForScreenWidth(1920, 1920.0 * 2.54 / 800, 800), 1), "screen-width conversion reverses");
        Check(GameSensitivity.Cm360(.07, 0, 800) == 0 && GameSensitivity.Cm360(.07, .225, 0) == 0
            && GameSensitivity.Cm360(0, .225, 800) == 0 && GameSensitivity.CmPerScreenWidth(1920, -1, 800) == 0,
            "invalid physical inputs return zero instead of NaN or Infinity");
        Check(!GameSensitivity.TryConvert(.2, .07, true, .1, false, 800, out _),
            "2D and rotation are not falsely presented as equivalent motions");
        Check(GameSensitivity.Presets.Select(p => p.Id).Distinct().Count() == GameSensitivity.Presets.Length
            && GameSensitivity.Presets.All(p => p.Yaw > 0 && p.SourceUrl.StartsWith("https://", StringComparison.Ordinal)),
            "every game preset has a unique ID, positive yaw and a source URL");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("cs2")!, 2, 800), 25.9772727273),
            "CS2 hipfire matches the published Source yaw");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("overwatch")!, 5, 800), 34.6363636364),
            "Overwatch hipfire matches its published yaw");
        Check(Near(GameSensitivity.ConvertPreset(2, GameSensitivity.Find("cs2")!, GameSensitivity.Find("overwatch")!, 800), 20.0 / 3),
            "CS2 to Overwatch conversion uses the yaw ratio");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("pubg")!, 50, 800), 25.7200720072),
            "PUBG TPP FOV 80 uses its documented exponential scale");
        Check(Near(GameSensitivity.SensitivityForPresetCm(GameSensitivity.Find("pubg")!, 914.4 / (.022 * 2 * 800), 800), 49.7839313109),
            "PUBG inverse conversion reproduces the reference value");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("minecraft")!, .5, 800), 7.62),
            "Minecraft 100 percent uses the vanilla cubic scale");
        Check(Near(GameSensitivity.SensitivityForPresetCm(GameSensitivity.Find("minecraft")!, 7.62, 800), .5),
            "Minecraft inverse conversion returns 100 percent");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("minecraft")!, 0, 800), 119.0625),
            "Minecraft zero percent is a valid nonzero physical rotation");
        Check(Near(GameSensitivity.SensitivityForPresetCm(GameSensitivity.Find("minecraft")!, 119.0625, 800), 0),
            "Minecraft inverse conversion accepts the zero-percent endpoint");
        Check(GameSensitivity.Cm360Preset(GameSensitivity.Find("minecraft")!, 1.01, 800) == 0
            && GameSensitivity.SensitivityForPresetCm(GameSensitivity.Find("minecraft")!, 1, 800) == 0,
            "Minecraft rejects values outside its internal zero-to-one domain");
        Check(GameSensitivity.TryConvertPreset(0, GameSensitivity.Find("minecraft")!, GameSensitivity.Find("minecraft")!, 800, 1920,
            out double minecraftZero, out double minecraftCm) && Near(minecraftZero, 0) && Near(minecraftCm, 119.0625),
            "preset conversion preserves a valid zero-valued Minecraft result");
        Check(GameSensitivity.TryConvertPreset(1, GameSensitivity.Find("minecraft")!, GameSensitivity.Find("minecraft")!, 800, 1920,
            out double minecraftMax, out double minecraftMaxCm) && Near(minecraftMax, 1) && Near(minecraftMaxCm, 1.8603515625),
            "Minecraft maximum survives floating-point round trips");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("cod")!, 6, 800), 28.8636363636),
            "Call of Duty hipfire uses the KovaaK yaw");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("fortnite")!, 8, 800), 25.7200720072),
            "Fortnite percentage X uses its documented yaw");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("r6")!, 8, 800), 24.93639169),
            "Rainbow Six default INI multiplier produces its documented cm/360");
        Check(Near(GameSensitivity.Cm360Preset(GameSensitivity.Find("r6")!, 8, 800, .5), 49.87278338),
            "Halving the Siege INI multiplier doubles cm/360");
        Check(GameSensitivity.TryParsePositive(" 0,022 ", out double parsed) && Near(parsed, .022)
            && !GameSensitivity.TryParsePositive("NaN", out _) && !GameSensitivity.TryParsePositive("0", out _)
            && !GameSensitivity.TryParsePositive("-0.1", out _) && !GameSensitivity.TryParsePositive("junk", out _),
            "converter fields accept decimal commas and reject non-positive or non-finite values");
        Check(GameSensitivity.TryConvertPreset(2, GameSensitivity.Find("cs2")!, GameSensitivity.Find("overwatch")!, 800, 1920,
            out double converted, out double cm) && Near(converted, 20.0 / 3) && Near(cm, 25.9772727273),
            "preset conversion returns both the target sensitivity and physical rotation");
        Check(!GameSensitivity.TryConvertPreset(1, GameSensitivity.Find("osu")!, GameSensitivity.Find("cs2")!, 800, 1920,
            out _, out _) && !GameSensitivity.TryConvertPreset(2, GameSensitivity.Find("cs2")!, GameSensitivity.Find("osu")!, 800, 1920,
            out _, out _), "preset conversion rejects rotation to 2D in both directions");
        Check(GameSensitivity.TryConvertPreset(1, GameSensitivity.Find("osu")!, GameSensitivity.Find("osu")!, 800, 1920,
            out converted, out cm) && Near(converted, 1) && Near(cm, 6.096),
            "osu raw-input conversion returns cm per screen width");
        Check(GameSensitivity.TryValorantOverride(25.9772727273, 800, out float valorant) && Near(valorant, .629),
            "CA-VAL-AIM application uses VALORANT's three-decimal sensitivity");
        Check(!GameSensitivity.TryValorantOverride(1000000, 800, out _) && !GameSensitivity.TryValorantOverride(0, 800, out _),
            "CA-VAL-AIM application rejects values outside VALORANT's supported range");
    }

    static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-7;

    static void Check(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("game sensitivity: " + what);
    }
}
