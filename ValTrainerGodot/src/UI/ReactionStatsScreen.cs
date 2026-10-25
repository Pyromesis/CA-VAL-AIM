using System.Globalization;
using Godot;
using ValTrainer.Core;
using ValTrainer.Modes;

namespace ValTrainer.UI;

/// <summary>
/// Reaction times by category (Stats → REACTION TIMES, or the dev flag "--screen reaction"): one row per Reaction Lab
/// category (visual, audio, peripheral, choice, flick) with the last median, the best median, the average of the last
/// five runs and its change against the five before, the rank band of that average (<see cref="ReactionStats.Reference"/>:
/// typical human values, not Riot data), the category's own extra (choice: false alarms, flick: hit rate) and the history
/// of medians as a line where lower is better. PLAY starts that category. The old Reaction Test counts as Visual.
/// </summary>
public partial class ReactionStatsScreen : ScreenBase
{
    protected override bool AnimatedBackdrop => false;

    static string L(string en, string es) => Loc.Spanish ? es : en;

    protected override void Back() => Main.I.ShowStats();

    protected override void Build()
    {
        float k = K;
        var vp = GetViewportRect().Size;
        float side = Mathf.Clamp(vp.X * 0.05f, 28 * k, 112 * k);
        AddChild(new MinimalBackdrop());
        var page = Margins(VBox(0), side, 44 * k, side, 30 * k);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(page);
        var col = (VBoxContainer)page.GetChild(0);

        var head = HBox(0);
        var back = new MLink { K = k, Label = L("← STATS", "← ESTADÍSTICAS"), Px = 12.5f, Delay = 0.02f };
        back.Pressed += Main.I.ShowStats;
        head.AddChild(back);
        head.AddChild(Expander());
        var menu = new MLink { K = k, Label = "MENU", Px = 12.5f, Delay = 0.05f };
        menu.Pressed += Main.I.ShowMenu;
        head.AddChild(menu);
        col.AddChild(head);
        col.AddChild(Spacer(0, 40 * k));

        col.AddChild(new MText { K = k, Text = L("REACTION LAB", "LABORATORIO DE REACCIÓN"), Font = UiTheme.HudWide, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.08f });
        col.AddChild(Spacer(0, 6 * k));
        col.AddChild(new MText { K = k, Text = L("Reaction times", "Tiempos de reacción"), Font = UiTheme.Light, Px = 56, Delay = 0.12f });
        col.AddChild(Spacer(0, 6 * k));
        col.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 15, Color = UiTheme.Dim, Delay = 0.16f,
            Text = L("Median of each run, by what you react to. Lower is better; the line shows every run, oldest on the left.",
                     "Mediana de cada partida según a qué reaccionas. Menos es mejor; la línea muestra todas las partidas, la más antigua a la izquierda."),
        });
        col.AddChild(Spacer(0, 34 * k));

        col.AddChild(new HeaderRow { K = k, CustomMinimumSize = new Vector2(0, 26 * k) });
        col.AddChild(new MRule { Delay = 0.2f });
        var stats = Main.I.Stats;
        int i = 0;
        foreach (var c in ReactionStats.All)
        {
            var h = ReactionStats.History(stats, c);
            var row = new CategoryRow(c, h)
            {
                K = k, Delay = 0.22f + i++ * Mn.Stagger * 1.4f,
                CustomMinimumSize = new Vector2(0, 92 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = Loc.T(ReactionStats.Blurb(c)),
            };
            var cat = c;
            row.Pressed += () => Main.I.StartMode(() => new ReactionLabMode(cat));
            col.AddChild(row);
        }
        col.AddChild(Spacer(0, 22 * k));
        col.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 13, Color = UiTheme.Faint, Delay = 0.5f,
            Text = L("Rank bands are typical human reaction times per category (reaction-time research), not Riot data. Click a row to play it.",
                     "Las bandas de rango son tiempos de reacción humanos típicos por categoría (investigación sobre tiempo de reacción), no datos de Riot. Haz clic en una fila para jugarla."),
        });
    }

    /// <summary>Column x positions as fractions of the row width.</summary>
    static readonly float[] Cols = { 0f, 0.20f, 0.30f, 0.40f, 0.52f, 0.66f, 0.76f };

    static string[] Heads => Loc.Spanish
        ? new[] { "CATEGORÍA", "ÚLTIMA", "MEJOR", "MEDIA · 5", "RANGO", "EXTRA", "HISTORIAL" }
        : new[] { "CATEGORY", "LAST", "BEST", "AVG · 5", "RANK", "EXTRA", "HISTORY" };

    public partial class HeaderRow : Control
    {
        public float K = 1f;
        float t;
        public HeaderRow() => MouseFilter = MouseFilterEnum.Ignore;
        public override void _Process(double delta) { if (t < 1.2f) { t += (float)delta; QueueRedraw(); } }
        public override void _Draw()
        {
            float r = Mn.Reveal(t, 0.18f);
            int fs = UiTheme.Fs(10.5f, K);
            var h = Heads;
            for (int i = 0; i < h.Length; i++)
                DrawString(UiTheme.HudWide, new Vector2(Size.X * Cols[i] + (i == 0 ? 14 * K : 0), Gfx.Mid(Size.Y / 2, fs)), h[i],
                    HorizontalAlignment.Left, -1, fs, Mn.A(UiTheme.Faint, r));
        }
    }

    /// <summary>One category: numbers and the history line; hover brightens it like the menu's drill rows.</summary>
    public partial class CategoryRow : MButtonBase
    {
        readonly ReactionCategory cat;
        readonly List<(DateTime When, float Median, float Best, float Sd, float Extra)> h;
        readonly (float Last, float Best, float Recent, float Trend, int Runs) s;

        public CategoryRow(ReactionCategory cat, List<(DateTime When, float Median, float Best, float Sd, float Extra)> h)
        {
            this.cat = cat;
            this.h = h;
            s = ReactionStats.Summary(h);
        }

        static string Ms(float v) => v > 0 ? v.ToString("0", CultureInfo.InvariantCulture) : "—";

        string CatName => Loc.Spanish ? cat switch
        {
            ReactionCategory.Visual => "Visual", ReactionCategory.Audio => "Auditiva", ReactionCategory.Peripheral => "Periférica",
            ReactionCategory.Choice => "Elección", _ => "Flick",
        } : ReactionStats.Name(cat);

        public override void _Draw()
        {
            if (!BeginDraw()) return;
            float k = K, w = Size.X, hgt = Size.Y, cy = hgt * 0.42f;
            if (Hover > 0.01f) DrawRect(new Rect2(0, 0, w, hgt), new Color(1, 1, 1, 0.03f * Hover));
            DrawRect(new Rect2(0, hgt * (0.5f - 0.3f * Hover), Mathf.Max(1.5f, 2 * k), hgt * 0.6f * Hover), Mn.A(UiTheme.Accent, Hover));
            DrawRect(new Rect2(0, hgt - 1, w, 1), UiTheme.Line);
            int ns = UiTheme.Fs(22, k), vs = UiTheme.Fs(22, k), us = UiTheme.Fs(11, k), ss = UiTheme.Fs(12, k);
            float x0 = 14 * k + 6 * k * Hover;
            DrawString(UiTheme.Light, new Vector2(x0, Gfx.Mid(cy, ns)), CatName, HorizontalAlignment.Left, -1, ns, UiTheme.Text);
            string runs = s.Runs == 0 ? L("not played yet", "sin jugar") : Loc.Spanish ? $"{s.Runs} partidas" : $"{s.Runs} runs";
            DrawString(UiTheme.Body, new Vector2(x0, cy + ns * 0.95f), runs, HorizontalAlignment.Left, -1, ss, UiTheme.Faint);

            void Num(int col, float v, Color c)
            {
                float x = w * Cols[col];
                string t = Ms(v);
                DrawString(UiTheme.Hud, new Vector2(x, Gfx.Mid(cy, vs)), t, HorizontalAlignment.Left, -1, vs, c);
                if (v > 0) DrawString(UiTheme.HudWide, new Vector2(x + Gfx.RawW(UiTheme.Hud, t, vs) + 4 * k, Gfx.Mid(cy, vs)), "MS", HorizontalAlignment.Left, -1, us, UiTheme.Faint);
            }
            Num(1, s.Last, UiTheme.TextSoft);
            Num(2, s.Best, UiTheme.Text);
            Num(3, s.Recent, UiTheme.Text);
            if (s.Runs > 5 && MathF.Abs(s.Trend) >= 1f)
            {
                bool faster = s.Trend < 0;
                string tr = (faster ? "▼ " : "▲ ") + MathF.Abs(s.Trend).ToString("0", CultureInfo.InvariantCulture) + " ms";
                DrawString(UiTheme.Body, new Vector2(w * Cols[3], cy + ns * 0.95f), tr, HorizontalAlignment.Left, -1, ss, faster ? UiTheme.Good : UiTheme.Faint);
            }
            if (s.Runs > 0)
            {
                string rank = ReactionStats.RankOf(cat, s.Recent);
                Gfx.TextFit(this, UiTheme.Body, rank, w * Cols[4], Gfx.Mid(cy, ss), ss, UiTheme.TextSoft, w * (Cols[5] - Cols[4]) - 10 * k);
                string extra = cat switch
                {
                    ReactionCategory.Choice => (Loc.Spanish ? "falsas alarmas " : "false alarms ") + h[^1].Extra.ToString("0", CultureInfo.InvariantCulture),
                    ReactionCategory.Flick => h[^1].Extra > 0 ? (Loc.Spanish ? "impacto en " : "hit in ") + h[^1].Extra.ToString("0", CultureInfo.InvariantCulture) + " ms" : "",
                    _ => h[^1].Sd > 0 ? "± " + h[^1].Sd.ToString("0", CultureInfo.InvariantCulture) + " ms" : "",
                };
                DrawString(UiTheme.Body, new Vector2(w * Cols[5], Gfx.Mid(cy, ss)), extra, HorizontalAlignment.Left, -1, ss, UiTheme.Dim);
                DrawHistory(new Rect2(w * Cols[6], hgt * 0.22f, w * (1 - Cols[6]) - 14 * k, hgt * 0.5f));
            }
            else
            {
                string play = L("PLAY →", "JUGAR →");
                DrawString(UiTheme.HudWide, new Vector2(w * Cols[6], Gfx.Mid(cy, ss)), play, HorizontalAlignment.Left, -1, ss, UiTheme.Faint.Lerp(UiTheme.Text, Hover));
            }
        }

        /// <summary>Medians as a line, inverted so faster runs sit higher; the best run is a dot in the accent.</summary>
        void DrawHistory(Rect2 r)
        {
            float k = K;
            var v = h.Select(x => x.Median).TakeLast(40).ToList();
            DrawRect(new Rect2(r.Position.X, r.End.Y, r.Size.X, 1), new Color(1, 1, 1, 0.06f));
            if (v.Count == 0) return;
            float lo = v.Min(), hi = v.Max(), span = Mathf.Max(8f, hi - lo);
            Vector2 P(int i) => new(v.Count == 1 ? r.GetCenter().X : r.Position.X + i * r.Size.X / (v.Count - 1),
                                    r.Position.Y + (v[i] - lo) / span * r.Size.Y);
            for (int i = 1; i < v.Count; i++) DrawLine(P(i - 1), P(i), Mn.A(UiTheme.TextSoft, 0.8f), Mathf.Max(1.2f, 1.5f * k), true);
            int best = v.IndexOf(lo);
            DrawCircle(P(best), 3.2f * k, UiTheme.Accent);
            DrawCircle(P(v.Count - 1), 2.4f * k, UiTheme.Text);
        }
    }
}
