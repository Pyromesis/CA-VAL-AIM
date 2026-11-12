using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>Descriptive matched comparison, never proof that sensitivity caused a change.</summary>
public sealed record SensComparison(float Before, float After, int BeforeN, int AfterN,
    float BeforeOver, float AfterOver, float BeforeUnder, float AfterUnder,
    float BeforeMs, float AfterMs, float BeforeError, float AfterError)
{
    public bool Improved => BeforeOver + BeforeUnder - AfterOver - AfterUnder > .10f && AfterOver <= BeforeOver + .05f && AfterUnder <= BeforeUnder + .05f
        && AfterMs <= BeforeMs * 1.08f && AfterError <= BeforeError * 1.05f;
    public string Describe(bool es) => es
        ? $"Comparación equivalente {Before:0.000} → {After:0.000}: sobrepasos {BeforeOver:P0} → {AfterOver:P0}, cortos {BeforeUnder:P0} → {AfterUnder:P0}, error final {BeforeError:0.0}% → {AfterError:0.0}%, movimientos {BeforeMs:0} → {AfterMs:0} ms ({BeforeN}/{AfterN} intentos). "
            + (Improved ? "Menos errores de frenado sin una pérdida apreciable de velocidad o precisión. Repite A/B para confirmar; esto no demuestra causalidad." : "El cambio todavía no muestra una mejora conjunta de control y velocidad. Repite A/B antes de volver a cambiar.")
        : $"Matched comparison {Before:0.000} → {After:0.000}: overshoots {BeforeOver:P0} → {AfterOver:P0}, short {BeforeUnder:P0} → {AfterUnder:P0}, endpoint error {BeforeError:0.0}% → {AfterError:0.0}%, movements {BeforeMs:0} → {AfterMs:0} ms ({BeforeN}/{AfterN} attempts). "
            + (Improved ? "Fewer braking errors without appreciable speed or precision loss. Repeat A/B to confirm; this does not establish causality." : "No joint improvement in control and speed yet. Repeat A/B before another change.");

    internal static SensComparison? Build(IEnumerable<SensAdvisor.RunInfo> source, float current)
    {
        var all = source.Where(r => r.Sens > 0 && !string.IsNullOrEmpty(r.Context)
            && r.M.GetValueOrDefault("analysis.version") >= 2 && r.M.GetValueOrDefault("sens.changed") == 0
            && r.M.GetValueOrDefault("flick.n_still") > 0).OrderBy(r => r.When).ToList();
        int change = all.FindLastIndex(r => !SensLab.SameSens(r.Sens, current));
        if (change < 0) return null;
        var prior = all[change];
        // Only the latest contiguous block: do not mix months of practice at a reused sensitivity.
        var after = all.Skip(change + 1).Where(r => SensLab.SameSens(r.Sens, current)).TakeLast(8).ToList();
        var before = all.Take(change + 1).Reverse().TakeWhile(r => SensLab.SameSens(r.Sens, prior.Sens)).Take(8).ToList();
        foreach (var context in after.Select(r => r.Context).Distinct())
        {
            var a = after.Where(r => r.Context == context).ToList();
            var b = before.Where(r => r.Context == context && (a[^1].When - r.When).TotalDays <= 14).ToList();
            if (a.Count == 0 || b.Count == 0) continue;
            var am = Agg.Aggregate(a.Select(r => (r.When, r.M)), a[^1].When);
            var bm = Agg.Aggregate(b.Select(r => (r.When, r.M)), a[^1].When);
            float V(Dictionary<string, float> m, string k) => m.GetValueOrDefault(k, float.NaN);
            bool Same(string k) => a.Concat(b).All(r => r.M.TryGetValue(k, out var v) && v == a[0].M.GetValueOrDefault(k));
            if (a[0].M.GetValueOrDefault("context.dpi") <= 0 || !Same("context.dpi") || !Same("context.weapon")) continue;
            if (V(am, "flick.n_still") < 20 || V(bm, "flick.n_still") < 20) continue;
            // Random target distributions must also be similar in size and angle.
            bool Near(string k) => float.IsFinite(V(am, k)) && float.IsFinite(V(bm, k))
                && MathF.Abs(V(am, k) - V(bm, k)) <= MathF.Max(.05f, MathF.Abs(V(bm, k)) * .20f);
            if (!Near("flick.amp_deg") || !Near("flick.r_deg")) continue;
            float an = V(am, "flick.n_still"), bn = V(bm, "flick.n_still");
            var result = new SensComparison(prior.Sens, current, (int)bn, (int)an,
                V(bm, "flick.n_over") / bn, V(am, "flick.n_over") / an,
                V(bm, "flick.n_under") / bn, V(am, "flick.n_under") / an,
                V(bm, "flick.mt_ms"), V(am, "flick.mt_ms"), V(bm, "flick.end_err_pct"), V(am, "flick.end_err_pct"));
            if (float.IsFinite(result.BeforeMs) && float.IsFinite(result.AfterMs)
                && float.IsFinite(result.BeforeError) && float.IsFinite(result.AfterError)) return result;
        }
        return null;
    }
}
