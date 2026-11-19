using System.Text.Json;

namespace CaValAim.Core;

/// <summary>One completed block in a blinded comparison. The original frames and shots are stored in telemetry.</summary>
public sealed record SensitivityBlockResult(bool Candidate, float Score, float MedianMs, int Hits, int Misses,
    int Samples, float TrackingSeconds, int Seed);

public sealed record SensitivityTestResult(Guid Id, DateTime WhenUtc, string Category, string Drill, int Tier,
    int Dpi, float Baseline, float Candidate, int Preferred, SensitivityBlockResult[] Blocks)
{
    public float PairGain(int first)
    {
        var a = Blocks[first];
        var b = Blocks[first + 1];
        return a.Candidate ? a.Score - b.Score : b.Score - a.Score;
    }
}

/// <summary>Append-only local evidence. Each block is closed before gameplay proceeds to the next one.</summary>
public static class SensitivityHistory
{
    static readonly object Gate = new();
    static string FilePath => Path.Combine(Paths.DataDir, "sensitivity-history.jsonl");
    static string? lastConfiguration;
    static string? cachedPath;
    static Dictionary<Guid, SensitivityTestResult>? tests;

    sealed record Entry(DateTime WhenUtc, SensitivityTestResult? Test = null, string? Configuration = null);

    public static void Record(SensitivityTestResult test)
    {
        if (AppSettings.ReadOnly) return;
        lock (Gate)
        {
            EnsureLoaded();
            if (Append(new(test.WhenUtc, Test: test))) tests![test.Id] = test;
        }
    }

    internal static void RecordConfiguration(AppSettings settings)
    {
        lock (Gate)
        {
            EnsureLoaded();
            string snapshot = JsonSerializer.Serialize(new
            {
                settings.UseSensOverride, settings.SensOverride, settings.ValorantSens, settings.Dpi,
                settings.CategorySensitivities, settings.LiveSensitivityEnabled, settings.LiveSensitivityValues,
            });
            if (snapshot != lastConfiguration && Append(new(DateTime.UtcNow, Configuration: snapshot)))
                lastConfiguration = snapshot;
        }
    }

    public static SensitivityTestResult[] Tests()
    {
        lock (Gate)
        {
            EnsureLoaded();
            return tests!.Values.OrderByDescending(t => t.WhenUtc).ToArray();
        }
    }

    static void EnsureLoaded()
    {
        string path = FilePath;
        if (cachedPath == path && tests != null) return;
        cachedPath = path;
        lastConfiguration = null;
        tests = new();
        try
        {
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadLines(path))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<Entry>(line);
                    if (entry?.Test is { } test) tests[test.Id] = test;
                    if (entry?.Configuration is { } configuration) lastConfiguration = configuration;
                }
                catch (JsonException) { /* An interrupted write costs just that line. */ }
            }
        }
        catch (Exception ex) { Log.Error($"Reading sensitivity history failed: {ex.Message}"); }
    }

    static bool Append(Entry entry)
    {
        try
        {
            Directory.CreateDirectory(Paths.DataDir);
            // The leading newline isolates a previous write that was interrupted before its line terminator.
            File.AppendAllText(FilePath, "\n" + JsonSerializer.Serialize(entry) + "\n");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Saving sensitivity history failed: {ex.Message}");
            return false;
        }
    }
}
