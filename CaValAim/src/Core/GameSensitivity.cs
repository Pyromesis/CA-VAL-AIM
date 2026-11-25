using System.Globalization;

namespace CaValAim.Core;

public enum SensitivityMeasure { Rotation, ScreenWidth }
public enum NonlinearModel { Linear, Pubg, Minecraft }

/// <summary>A game's mouse-count scale and the source used to document it.</summary>
public sealed record GamePreset(
    string Id,
    string Name,
    double Yaw,
    SensitivityMeasure Measure,
    double DefaultSensitivity,
    string SourceUrl,
    string Note,
    NonlinearModel Model = NonlinearModel.Linear,
    int Fov = 80);

/// <summary>Physical mouse sensitivity conversions. Rotation uses degrees per count; osu uses pixels per count.</summary>
public static class GameSensitivity
{
    public static readonly GamePreset[] Presets =
    {
        new("valorant", "VALORANT", .07, SensitivityMeasure.Rotation, .225,
            "https://support-valorant.riotgames.com/hc/en-us/articles/360058954872", "0.07 degrees per mouse count"),
        new("cod", "Call of Duty / Warzone", .0066, SensitivityMeasure.Rotation, 6,
            "https://kovaaks.com/kovaaks/sens-converter", "KovaaK convention for modern CoD; ADS and monitor distance are separate"),
        new("fortnite", "Fortnite (X %)", .005555, SensitivityMeasure.Rotation, 8,
            "https://kovaaks.com/kovaaks/sens-converter/csgo-to-fortnite", "Input is the displayed percentage X; targeting/scope are separate"),
        new("r6", "Rainbow Six Siege", .018 / Math.PI, SensitivityMeasure.Rotation, 8,
            "https://www.ubisoft.com/en-us/game/rainbow-six/siege/news-updates/6kY6b5JByBY3P6vQWWinla/fov-and-input-sensitivity", "Default INI multiplier 0.02; Y5S3+ ADS is a separate system"),
        new("cs2", "Counter-Strike 2", .022, SensitivityMeasure.Rotation, 2,
            "https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/client/in_mouse.cpp", "Default m_yaw 0.022; ADS uses a separate FOV/zoom state"),
        new("apex", "Apex Legends", .022, SensitivityMeasure.Rotation, 2,
            "https://github.com/KovaaK/SensitivityMatcher/blob/master/ReleaseAssets/bin/SensitivityMatcher.au3", "Hipfire uses the Source yaw; each optic needs its own state"),
        new("overwatch", "Overwatch 2", .0066, SensitivityMeasure.Rotation, 5,
            "https://github.com/KovaaK/SensitivityMatcher/blob/master/ReleaseAssets/bin/SensitivityMatcher.au3", "Hipfire yaw; hero zoom settings are separate"),
        new("destiny2", "Destiny 2", .0066, SensitivityMeasure.Rotation, 5,
            "https://github.com/KovaaK/SensitivityMatcher/blob/master/ReleaseAssets/bin/SensitivityMatcher.au3", "Same numeric yaw as Overwatch; weapon ADS is separate"),
        new("quake", "Quake / Quake Champions", .022, SensitivityMeasure.Rotation, 1.5,
            "https://github.com/id-Software/Quake-III-Arena/blob/master/code/client/cl_input.c", "Default m_yaw 0.022; acceleration must be off"),
        new("rust", "Rust (KovaaK convention)", .1125, SensitivityMeasure.Rotation, .5,
            "https://kovaaks.com/kovaaks/sens-converter", "Community convention, not official; measure your installed version"),
        new("pubg", "PUBG (General · TPP FOV 80)", 1, SensitivityMeasure.Rotation, 50,
            "https://aimhub.gg/en/sensitivity-calculator/pubg", "Exponential General scale; FOV 80 and ADS are separate", NonlinearModel.Pubg),
        new("minecraft", "Minecraft Java (internal 0–1)", 1, SensitivityMeasure.Rotation, .5,
            "https://github.com/neoforged/NeoForge/blob/11b0a67cc80bf3a1bb25852ed2fa445c05d96645/patches/net/minecraft/client/MouseHandler.java.patch", "Vanilla cubic scale; 100% = internal 0.5", NonlinearModel.Minecraft),
        new("osu", "osu! (raw input)", 1, SensitivityMeasure.ScreenWidth, 1,
            "https://osu.ppy.sh/wiki/en/Options", "Pixels per mouse count; cm per screen, not cm/360"),
    };

    public static GamePreset? Find(string? id) => Presets.FirstOrDefault(p => p.Id == id);

    /// <summary>Positive numeric input with either decimal separator; game values are never culture-dependent.</summary>
    public static bool TryParsePositive(string? text, out double value)
    {
        value = 0;
        string normalized = (text ?? "").Trim().Replace(',', '.');
        return normalized.Length > 0 && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value) && value > 0;
    }

    /// <summary>Only Minecraft's documented internal scale includes zero and has a finite upper endpoint.</summary>
    public static bool ValidSensitivity(GamePreset preset, double sensitivity) =>
        double.IsFinite(sensitivity) && (preset.Model == NonlinearModel.Minecraft ? sensitivity is >= 0 and <= 1 : sensitivity > 0);

    /// <summary>Parse a value in the input domain of the selected preset.</summary>
    public static bool TryParseSensitivity(GamePreset preset, string? text, out double value)
    {
        value = 0;
        string normalized = (text ?? "").Trim().Replace(',', '.');
        return normalized.Length > 0 && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && ValidSensitivity(preset, value);
    }

    /// <summary>cm/360 using a preset's linear or explicitly supported nonlinear model.</summary>
    public static double Cm360Preset(GamePreset preset, double sensitivity, int dpi, double multiplier = 1)
    {
        if (preset.Measure != SensitivityMeasure.Rotation || !ValidSensitivity(preset, sensitivity) || dpi <= 0 || multiplier <= 0 || preset.Fov <= 0) return 0;
        double q = preset.Model switch
        {
            NonlinearModel.Pubg => .00005555 * preset.Fov * Math.Pow(10, sensitivity / 50) * multiplier,
            NonlinearModel.Minecraft => 1.2 * Math.Pow(.6 * sensitivity + .2, 3) * multiplier,
            _ => preset.Yaw * sensitivity * multiplier,
        };
        return q > 0 && double.IsFinite(q) ? 914.4 / (q * dpi) : 0;
    }

    /// <summary>Inverse of <see cref="Cm360Preset"/>, including PUBG's logarithm and Minecraft's cubic root.</summary>
    public static double SensitivityForPresetCm(GamePreset preset, double cm, int dpi, double multiplier = 1)
    {
        if (preset.Measure != SensitivityMeasure.Rotation || cm <= 0 || dpi <= 0 || multiplier <= 0 || preset.Fov <= 0) return 0;
        double q = 914.4 / (dpi * cm);
        double result = preset.Model switch
        {
            NonlinearModel.Pubg => 50 * Math.Log10(q / (.00005555 * preset.Fov * multiplier)),
            NonlinearModel.Minecraft => (Math.Cbrt(q / (1.2 * multiplier)) - .2) / .6,
            _ => q / (preset.Yaw * multiplier),
        };
        // Cube roots can put an exact input endpoint a few ulps outside 0..1 on a round trip. Clamp only that
        // numerical noise here; TryParseSensitivity still rejects every user-entered value outside the domain.
        if (preset.Model == NonlinearModel.Minecraft && double.IsFinite(result))
        {
            if (Math.Abs(result) <= 1e-12) result = 0;
            else if (Math.Abs(result - 1) <= 1e-12) result = 1;
        }
        return ValidSensitivity(preset, result) ? result : 0;
    }

    /// <summary>Convert equal physical rotation between supported presets at the same DPI.</summary>
    public static double ConvertPreset(double sensitivity, GamePreset source, GamePreset target, int dpi,
        double sourceMultiplier = 1, double targetMultiplier = 1)
    {
        if (source.Measure != SensitivityMeasure.Rotation || target.Measure != SensitivityMeasure.Rotation) return 0;
        double cm = Cm360Preset(source, sensitivity, dpi, sourceMultiplier);
        return SensitivityForPresetCm(target, cm, dpi, targetMultiplier);
    }

    /// <summary>Convert equal physical motion between presets; rotation and 2D screen travel never mix.</summary>
    public static bool TryConvertPreset(double sensitivity, GamePreset source, GamePreset target, int dpi, int screenWidth,
        out double targetSensitivity, out double physicalCm, double sourceMultiplier = 1, double targetMultiplier = 1)
    {
        targetSensitivity = physicalCm = 0;
        if (source.Measure != target.Measure) return false;
        if (source.Measure == SensitivityMeasure.Rotation)
        {
            physicalCm = Cm360Preset(source, sensitivity, dpi, sourceMultiplier);
            targetSensitivity = SensitivityForPresetCm(target, physicalCm, dpi, targetMultiplier);
        }
        else
        {
            physicalCm = CmPerScreenWidth(screenWidth, sensitivity, dpi);
            targetSensitivity = SensitivityForScreenWidth(screenWidth, physicalCm, dpi);
        }
        return physicalCm > 0 && double.IsFinite(physicalCm) && ValidSensitivity(target, targetSensitivity);
    }

    /// <summary>Rounded CUSTOM sensitivity that CA-VAL-AIM can apply at the declared DPI.</summary>
    public static bool TryValorantOverride(double cm, int dpi, out float sensitivity)
    {
        sensitivity = 0;
        var valorant = Find("valorant");
        if (valorant == null) return false;
        double value = SensitivityForPresetCm(valorant, cm, dpi);
        double rounded = Math.Round(value, 3);
        if (!double.IsFinite(rounded) || rounded < SensInput.Min || rounded > SensInput.Max) return false;
        sensitivity = (float)rounded;
        return true;
    }

    /// <summary>Centimetres for a complete rotation: 914.4 / (degrees/count × sensitivity × DPI × ADS multiplier).</summary>
    public static double Cm360(double yaw, double sensitivity, int dpi, double multiplier = 1) =>
        yaw <= 0 || sensitivity <= 0 || dpi <= 0 || multiplier <= 0 || !double.IsFinite(yaw * sensitivity * multiplier)
            ? 0 : 914.4 / (yaw * sensitivity * dpi * multiplier);

    /// <summary>Game sensitivity that produces <paramref name="cm"/> per rotation.</summary>
    public static double SensitivityForCm(double yaw, double cm, int dpi, double multiplier = 1) =>
        yaw <= 0 || cm <= 0 || dpi <= 0 || multiplier <= 0 || !double.IsFinite(yaw * cm * multiplier)
            ? 0 : 914.4 / (yaw * dpi * cm * multiplier);

    /// <summary>Convert equal physical rotation between two yaw scales at the same DPI.</summary>
    public static double Convert(double sensitivity, double sourceYaw, double targetYaw, int dpi,
        double sourceMultiplier = 1, double targetMultiplier = 1) =>
        sourceYaw <= 0 || targetYaw <= 0 || sensitivity <= 0 || dpi <= 0 || sourceMultiplier <= 0 || targetMultiplier <= 0
            ? 0 : sensitivity * sourceYaw * sourceMultiplier / (targetYaw * targetMultiplier);

    /// <summary>Centimetres to cross the screen once with raw input (pixels/count × sensitivity × DPI).</summary>
    public static double CmPerScreenWidth(int pixels, double sensitivity, int dpi) =>
        pixels <= 0 || sensitivity <= 0 || dpi <= 0 ? 0 : pixels * 2.54 / (sensitivity * dpi);

    public static double SensitivityForScreenWidth(int pixels, double cm, int dpi) =>
        pixels <= 0 || cm <= 0 || dpi <= 0 ? 0 : pixels * 2.54 / (cm * dpi);

    /// <summary>Convert only within the same kind of motion. 2D screen travel is not presented as a 360° rotation.</summary>
    public static bool TryConvert(double sensitivity, double sourceYaw, bool sourceRotation, double targetYaw,
        bool targetRotation, int dpi, out double targetSensitivity)
    {
        targetSensitivity = 0;
        if (sourceRotation != targetRotation || sensitivity <= 0 || sourceYaw <= 0 || targetYaw <= 0 || dpi <= 0) return false;
        targetSensitivity = sensitivity * sourceYaw / targetYaw;
        return double.IsFinite(targetSensitivity) && targetSensitivity > 0;
    }
}
