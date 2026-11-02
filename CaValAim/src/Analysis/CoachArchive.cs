using System.Text.Json;
using System.Text.Json.Serialization;
using CaValAim.Core;
using CaValAim.Game;

namespace CaValAim.Analysis;

/// <summary>Metadata copied from the run so an archive can be read without the .vtt file.</summary>
public sealed class CoachRunInfo
{
    public string Mode = "";
    public int Tier;
    public float Sens;
    public int Dpi;
    public string Weapon = "";
    public string Map = "";
    public DateTime When;
    public float Duration;
    public string TelemetryFile = "";
    public int Frames, Shots, Events;
}

/// <summary>
/// One run's coach analysis, stored as versioned JSON next to the telemetry. It contains the review (summary, issues,
/// strengths, metrics, sensitivity feedback), every play with its structured evidence and the complete per-second
/// timeline, so opening an old run shows exactly what the coach said when it was played — no recomputation needed.
/// <para>Archives are derived data: a missing or unreadable file falls back to recomputing from the .vtt, so they can
/// never block the game or lose a run.</para>
/// </summary>
public sealed class CoachArchive
{
    public const int CurrentSchema = 1;

    public int Schema = CurrentSchema;
    public string App = "CA-VAL-AIM";
    public DateTime SavedUtc = DateTime.UtcNow;
    public CoachRunInfo Run = new();
    public RunReview Review = new();

    static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Where archives live: %APPDATA%\CA-VAL-AIM\coach (never throws).</summary>
    public static string Dir => Paths.DataSubDir("coach");

    static string SafeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));

    /// <summary>Archive path for a .vtt file (same base name, .json) or for a run without a file yet.</summary>
    public static string FileFor(string? telemetryFile, RunTelemetry? run = null, string? dir = null)
    {
        dir ??= Dir;
        string name = !string.IsNullOrWhiteSpace(telemetryFile)
            ? Path.GetFileNameWithoutExtension(telemetryFile)
            : (run ?? new RunTelemetry()).When.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "_" + SafeName((run ?? new RunTelemetry()).Mode);
        return Path.Combine(dir, name + ".json");
    }

    public static CoachArchive From(RunTelemetry t, RunReview review, string? telemetryFile) => new()
    {
        SavedUtc = DateTime.UtcNow,
        Run = new CoachRunInfo
        {
            Mode = t.Mode,
            Tier = t.Tier,
            Sens = t.Sens,
            Dpi = t.Dpi,
            Weapon = t.Weapon,
            Map = t.Map,
            When = t.When,
            Duration = t.Duration,
            TelemetryFile = string.IsNullOrWhiteSpace(telemetryFile) ? "" : Path.GetFileName(telemetryFile),
            Frames = t.Frames.Count,
            Shots = t.Shots.Count,
            Events = t.Events.Count,
        },
        Review = review,
    };

    /// <summary>
    /// Writes the archive atomically (temp file then swap) and keeps the newest <paramref name="keep"/> files. Returns
    /// the written path, or null when it couldn't be stored (the game keeps working; the .vtt is still there).
    /// </summary>
    public static string? Save(RunTelemetry t, RunReview review, string? telemetryFile, string? dir = null, int keep = 80)
    {
        try
        {
            dir ??= Dir;
            Directory.CreateDirectory(dir);
            string path = FileFor(telemetryFile, t, dir);
            string json = JsonSerializer.Serialize(From(t, review, telemetryFile), Json);
            if (!Paths.TryWriteAllText(path, json)) return null;
            try
            {
                foreach (var old in Directory.GetFiles(dir, "*.json")
                             .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                             .Skip(Math.Max(1, keep)))
                {
                    try { File.Delete(old); } catch { /* one locked file must not stop the rest */ }
                }
            }
            catch { /* retention is best effort */ }
            return path;
        }
        catch (Exception e)
        {
            Log.Error($"Saving the coach archive failed: {e.Message}");
            return null;
        }
    }

    /// <summary>Reads one archive. Wrong schema / corrupt JSON / future versions return null instead of guessing.</summary>
    public static CoachArchive? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var a = JsonSerializer.Deserialize<CoachArchive>(File.ReadAllText(path), Json);
            if (a == null || a.Schema != CurrentSchema || a.Review == null) return null;
            return a;
        }
        catch (Exception e) when (Paths.IsCorruptData(e))
        {
            Log.Error($"Coach archive {Path.GetFileName(path)} is unreadable ({e.Message}); the run can be recomputed from its telemetry.");
            return null;
        }
        catch (Exception e)
        {
            Log.Error($"Coach archive {Path.GetFileName(path)} couldn't be opened ({e.Message}).");
            return null;
        }
    }

    /// <summary>Archive for a saved run: the stored path when given, otherwise its sibling in the coach folder.</summary>
    public static CoachArchive? LoadFor(string? telemetryFile, string? coachFile = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(coachFile) && File.Exists(coachFile)) return Load(coachFile!);
            if (string.IsNullOrWhiteSpace(telemetryFile)) return null;
            return Load(FileFor(telemetryFile));
        }
        catch { return null; }
    }

    /// <summary>Most recent archives (newest first), for diagnostics and the profile's fallback search.</summary>
    public static IEnumerable<string> RecentFiles(int max = 80)
    {
        try { return Directory.GetFiles(Dir, "*.json").OrderByDescending(f => f).Take(max).ToList(); }
        catch { return Array.Empty<string>(); }
    }
}
