using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>Predicts next-run accuracy from the last three comparable runs using a local neural network.</summary>
public sealed record AimForecast(string Drill, float Accuracy, int ValidationRuns, string Metric = "accuracy",
    float ValidationError = 0, float BaselineError = 0, string Map = "", int Dpi = -1, int Weapon = -1,
    int Recoil = -1, float Sensitivity = 0);

public static class CoachNetwork
{
    const int Inputs = 3, Hidden = 6;

    public static List<AimForecast> Forecast(IEnumerable<RunRecord> newestFirst, float sens, int tier, int? dpi = null)
    {
        var result = new List<AimForecast>();
        var runs = newestFirst.Where(r => r.Tier == tier && SensLab.SameSens(r.Sens, sens)
            && (dpi == null || Context(r, "context.dpi") == dpi)
            && Context(r, "sens.changed") != 1 && Context(r, "weapon.changed") != 1 && Context(r, "recoil.changed") != 1
            && Context(r, "adapt.trials") < 0).OrderByDescending(r => r.When).ToArray();
        foreach (string drill in runs.Select(r => r.Mode).Distinct().Where(IsSupported))
        {
            var latest = runs.First(r => r.Mode == drill);
            var history = runs.Where(r => r.Mode == drill && r.Map == latest.Map &&
                Context(r, "run.weapon") == Context(latest, "run.weapon") &&
                Context(r, "context.dpi") == Context(latest, "context.dpi") &&
                Context(r, "context.weapon") == Context(latest, "context.weapon") &&
                Context(r, "run.recoil") == Context(latest, "run.recoil") && r.Accuracy is >= 0 and <= 1 &&
                HasSample(r))
                .Take(96).Reverse().Select(r => r.Accuracy).ToArray();
            if (history.Length < 24) continue;

            int split = history.Length * 3 / 4;
            var weights = Fit(history, split);
            float networkError = 0, averageError = 0, previousError = 0;
            for (int i = split; i < history.Length; i++)
            {
                networkError += MathF.Abs(Predict(history, i, weights) - history[i]);
                float average = (history[i - 1] + history[i - 2] + history[i - 3]) / Inputs;
                averageError += MathF.Abs(average - history[i]);
                previousError += MathF.Abs(history[i - 1] - history[i]);
            }
            float baselineError = MathF.Min(averageError, previousError);
            if (networkError + 0.01f * (history.Length - split) >= baselineError) continue;

            result.Add(new AimForecast(drill, Predict(history, history.Length, Fit(history, history.Length)),
                history.Length - split, "accuracy", networkError / (history.Length - split), baselineError / (history.Length - split),
                latest.Map, Context(latest, "context.dpi"), Context(latest, "run.weapon"), Context(latest, "run.recoil"), sens));
        }
        foreach (string metric in new[] { "overshoot", "undershoot", "first-hit", "correction-load", "recovered-over" })
        foreach (var group in runs.Where(r => IsSupported(r.Mode) && r.Metrics?.GetValueOrDefault("analysis.version") >= 2
            && r.Metrics.GetValueOrDefault("sens.changed") == 0 && r.Metrics.GetValueOrDefault("flick.n_still") >= 15
            && r.Metrics.ContainsKey("context.dpi") && r.Metrics.ContainsKey("context.weapon"))
            .GroupBy(r => (r.Mode, r.Map, Dpi: Context(r, "context.dpi"), Weapon: Context(r, "context.weapon"), Recoil: Context(r, "run.recoil"))))
        {
            float Value(RunRecord r)
            {
                var m = r.Metrics!;
                float n = m.GetValueOrDefault("flick.n_still");
                return metric switch {
                    "recovered-over" => m.GetValueOrDefault("flick.recovered_over_n", float.NaN) / n,
                    "overshoot" => m.GetValueOrDefault("flick.n_over") / n,
                    "undershoot" => m.GetValueOrDefault("flick.n_under") / n,
                    "first-hit" => m.GetValueOrDefault("micro.n") >= 15
                        ? m.GetValueOrDefault("micro.first_hit_share", float.NaN) : float.NaN,
                    _ => m.GetValueOrDefault("flick.n_hit") >= 15 && m.ContainsKey("flick.nc")
                        ? Math.Clamp(m["flick.nc"] / 4f, 0, 1) : float.NaN,
                };
            }
            var history = group.OrderByDescending(r => r.When).Take(96).Reverse().Select(Value)
                .Where(v => float.IsFinite(v) && v >= 0 && v <= 1).ToArray();
            if (history.Length < 32) continue;
            int split = history.Length * 3 / 4;
            var weights = Fit(history, split);
            float net = 0, baseline = 0;
            for (int i = split; i < history.Length; i++) {
                net += MathF.Abs(Predict(history, i, weights) - history[i]);
                float mean = (history[i-1] + history[i-2] + history[i-3]) / Inputs;
                // Must beat both persistence and the rolling mean, not an artificially weak baseline.
                baseline += MathF.Min(MathF.Abs(mean - history[i]), MathF.Abs(history[i-1] - history[i]));
            }
            int validation = history.Length - split;
            if (net + .01f * validation >= baseline) continue;
            result.Add(new AimForecast(group.First().Mode, Predict(history, history.Length, Fit(history, history.Length)),
                validation, metric, net / validation, baseline / validation, group.Key.Map, group.Key.Dpi,
                Context(group.First(), "run.weapon"), group.Key.Recoil, sens));
        }
        return result;
    }

    static int Context(RunRecord r, string key) => (int)(r.Metrics?.GetValueOrDefault(key, -1) ?? -1);

    static bool IsTracking(string key) => RunAnalysis.Base(key) is "tracking" or "trackv" or "airtrack" or "reacttrack";
    static bool IsSupported(string key) => IsTracking(key) ||
        RunAnalysis.Base(key) is "flick" or "flickx" or "microshot" or "microx" or "reflex";
    internal static bool HasSample(RunRecord r) => IsTracking(r.Mode)
        ? r.Metrics?.GetValueOrDefault("track.secs") >= 30 : r.Shots >= 15;

    static float[] Fit(float[] history, int end)
    {
        var random = new Random(42);
        var weights = new float[Hidden * (Inputs + 2) + 1];
        for (int i = 0; i < weights.Length; i++) weights[i] = random.NextSingle() * 0.4f - 0.2f;
        Span<float> hidden = stackalloc float[Hidden];
        for (int epoch = 0; epoch < 90; epoch++)
            for (int i = Inputs; i < end; i++)
            {
                float prediction = Forward(history, i, weights, hidden);
                float delta = 0.08f * (history[i] - prediction) * prediction * (1 - prediction);
                weights[^1] += delta;
                for (int h = 0; h < Hidden; h++)
                {
                    int offset = h * (Inputs + 2);
                    int output = offset + Inputs + 1;
                    float back = delta * weights[output] * (1 - hidden[h] * hidden[h]);
                    weights[output] += delta * hidden[h];
                    weights[offset + Inputs] += back;
                    for (int j = 0; j < Inputs; j++)
                        weights[offset + j] += back * (2 * history[i - 1 - j] - 1);
                }
            }
        return weights;
    }

    static float Predict(float[] history, int i, float[] weights) =>
        Forward(history, i, weights, stackalloc float[Hidden]);

    static float Forward(float[] history, int i, float[] weights, Span<float> hidden)
    {
        float output = weights[^1];
        for (int h = 0; h < Hidden; h++)
        {
            int offset = h * (Inputs + 2);
            float sum = weights[offset + Inputs];
            for (int j = 0; j < Inputs; j++)
                sum += weights[offset + j] * (2 * history[i - 1 - j] - 1);
            hidden[h] = MathF.Tanh(sum);
            output += hidden[h] * weights[offset + Inputs + 1];
        }
        return 1 / (1 + MathF.Exp(-output));
    }
}
