using Godot;
using CaValAim.Game;

namespace CaValAim.Core;

/// <summary>Runs every playable routine through the real session/playlist callbacks with shortened rounds.</summary>
public partial class RoutinePlaybackCheck : Node
{
    readonly List<Routine> routines = RoutineLibrary.Routines.Where(r => r.Playable).ToList();
    int index = -1, started, completed, total, failures;
    float elapsed;
    Playlist expected = null!;
    List<int> owners = new();
    GameSession? previous;
    public override void _Ready() => Next();
    void Check(bool ok, string message)
    {
        if (ok) return;
        failures++;
        GD.Print($"[routine-playback] FAIL {routines[index].Id}: {message}");
    }
    void Next()
    {
        if (++index >= routines.Count)
        {
            GD.Print($"[routine-playback] RESULT: {(failures == 0 ? "PASS" : "FAIL")} ({routines.Count} routines, {total} completed runs, {failures} failures)");
            GetTree().Quit(failures == 0 ? 0 : 1);
            return;
        }
        expected = routines[index].ToPlaylist(out owners);
        previous = null; started = completed = 0; elapsed = 0;
        Check(routines[index].Play(), "could not start");
        Observe();
    }
    void Observe()
    {
        var current = Main.I.TrainingSession;
        if (current == null || current == previous) return;
        if (previous != null) FinishedPrevious();
        Check(started < expected.Modes.Count, "unexpected extra session");
        if (started < expected.Modes.Count)
        {
            Check(current.Mode.Key == expected.Modes[started], $"wrong drill at run {started}");
            Check(PlaylistRunner.TaskNumber == owners.Take(started + 1).Distinct().Count(), $"wrong task counter at run {started}");
            int owner = owners[started];
            Check(PlaylistRunner.RunOfStep == owners.Take(started + 1).Count(o => o == owner) &&
                PlaylistRunner.RunsInStep == owners.Count(o => o == owner), $"wrong repetition counter at run {started}");
            Check(current.HasTimeLimit && current.RunLength > 0, $"missing time limit at run {started}");
            Check(Math.Abs(current.ClockSeconds - current.RunLength) < 0.05f, $"clock differs from actual duration at run {started}");
        }
        previous = current; started++;
    }
    void FinishedPrevious()
    {
        Check(previous!.State == GameSession.St.Results && previous.Record != null, $"run {completed} did not finish");
        completed++; total++;
    }
    public override void _Process(double delta)
    {
        if (index < 0 || index >= routines.Count) return;
        elapsed += (float)delta;
        Observe();
        if (previous is { State: GameSession.St.Results } && PlaylistRunner.TaskCount == 0)
        {
            FinishedPrevious();
            Check(started == expected.Modes.Count && completed == expected.Modes.Count, $"only {completed}/{expected.Modes.Count} runs completed");
            Check(RoutineStore.Find(routines[index].Id)?.Completed == true, "completion was not recorded");
            GD.Print($"[routine-playback] checked {routines[index].Id}: {completed}/{expected.Modes.Count} runs");
            previous = null;
            Callable.From(Next).CallDeferred();
        }
        else if (elapsed > expected.Modes.Count * 12 + 20)
        {
            Check(false, $"stalled after {completed}/{expected.Modes.Count} runs");
            PlaylistRunner.Restore();
            previous = null;
            Callable.From(Next).CallDeferred();
        }
    }
}
