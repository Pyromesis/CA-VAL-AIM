using CaValAim.Analysis;
using CaValAim.Core;
using CaValAim.Modes;
using Godot;

namespace CaValAim.UI;

/// <summary>
/// The coach's technique library (menu → ACADEMY): a list of lessons on the left and, for the selected one, what the
/// technique is, how to do it step by step, the usual mistakes, the cue to repeat while playing, the drills that
/// train it (one click to play) and third-party video guides, each credited to its author and opened in the browser.
/// </summary>
public partial class AcademyScreen : ScreenBase
{
    string selected;
    readonly Action? back;

    public AcademyScreen() : this(null) { }

    /// <param name="techniqueId">Lesson to open (a coach finding's technique); null = the first one.</param>
    /// <param name="back">Where Esc / BACK returns to; null = the main menu.</param>
    public AcademyScreen(string? techniqueId, Action? back = null)
    {
        selected = Academy.Find(techniqueId ?? "")?.Id ?? Academy.All[0].Id;
        this.back = back;
    }

    static string L(string es, string en) => Loc.Spanish ? es : en;

    protected override void Back()
    {
        if (back != null) back(); else Main.I.ShowMenu();
    }

    protected override void Build()
    {
        var page = VBox(18 * K);
        var root = Margins(page, 70 * K, 42 * K, 70 * K, 36 * K);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);

        page.AddChild(Heading(L("ACADEMIA", "ACADEMY"),
            L("TÉCNICAS DE AIM, EJERCICIOS PARA CADA UNA Y GUÍAS EN VÍDEO CON SUS AUTORES", "AIM TECHNIQUES, A DRILL FOR EACH AND VIDEO GUIDES WITH THEIR AUTHORS")));

        var columns = HBox(28 * K);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        page.AddChild(columns);

        // ---- lessons ----
        var listScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(310 * K, 0) };
        var list = VBox(6 * K);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        listScroll.AddChild(list);
        columns.AddChild(listScroll);
        foreach (var t in Academy.All)
        {
            string id = t.Id;
            var b = Btn(t.Name, id == selected ? VButton.Look.Primary : VButton.Look.Secondary, () => { selected = id; Rebuild(); }, 290, 42, 15);
            list.AddChild(b);
        }
        list.AddChild(Spacer(0, 10 * K));
        list.AddChild(Btn(L("VOLVER", "BACK"), VButton.Look.Ghost, Back, 290, 42, 15));

        // ---- the selected lesson ----
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var body = VBox(10 * K);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(body);
        columns.AddChild(scroll);

        var lesson = Academy.Find(selected) ?? Academy.All[0];
        void Line(string text, Font font, float px, Color color)
        {
            // Lesson text is already in the UI language: bypass the English → Spanish table.
            var label = new Label { Text = text, LabelSettings = UiTheme.Label(font, px, K, color), MouseFilter = MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            body.AddChild(label);
        }
        void Section(string title)
        {
            body.AddChild(Spacer(0, 8 * K));
            Line(title, UiTheme.HudWide, 12, UiTheme.Teal);
        }

        Line(lesson.Name, UiTheme.Light, 34, UiTheme.Text);
        Line(lesson.Summary, UiTheme.Body, 18, UiTheme.TextSoft);

        Section(L("LA CONSIGNA", "THE CUE"));
        Line("« " + lesson.Cue + " »", UiTheme.BodySemi, 20, UiTheme.Accent);

        Section(L("CÓMO SE HACE", "HOW TO DO IT"));
        for (int i = 0; i < lesson.Steps.Length; i++) Line($"{i + 1}.  {lesson.Steps[i]}", UiTheme.Body, 17, UiTheme.Text);

        Section(L("ERRORES HABITUALES", "COMMON MISTAKES"));
        foreach (string m in lesson.Mistakes) Line("•  " + m, UiTheme.Body, 17, UiTheme.Dim);

        var drills = lesson.Drills.Where(k => ModeRegistry.Find(k) != null).ToArray();
        if (drills.Length > 0)
        {
            Section(L("EJERCICIOS PARA ENTRENARLO", "DRILLS THAT TRAIN IT"));
            var row = new HFlowContainer();
            row.AddThemeConstantOverride("h_separation", (int)(10 * K));
            row.AddThemeConstantOverride("v_separation", (int)(8 * K));
            foreach (string key in drills)
            {
                var make = ModeRegistry.Find(key)!;
                string name = Loc.T(CoachData.DrillName(key));
                float w = Gfx.RawW(UiTheme.HudWide, name, UiTheme.Fs(14, K)) / K + 70;
                row.AddChild(Btn(name, VButton.Look.Secondary, () => Main.I.StartMode(make), w, 42, 14));
            }
            body.AddChild(row);
        }

        if (lesson.Videos.Length > 0)
        {
            Section(L("GUÍAS EN VÍDEO (SE ABREN EN EL NAVEGADOR)", "VIDEO GUIDES (OPEN IN YOUR BROWSER)"));
            foreach (var video in lesson.Videos.OrderByDescending(v => v.Language == (Loc.Spanish ? "es" : "en")))
            {
                var line = HBox(12 * K);
                string url = video.Url;
                line.AddChild(Btn(L("VER", "WATCH"), VButton.Look.Secondary, () => OpenVideo(url), 90, 36, 13));
                var text = new Label
                {
                    Text = $"{video.Title}\n{L("Autor", "By")}: {video.Author}  ·  YouTube  ·  {(video.Language == "es" ? L("en español", "in Spanish") : L("en inglés", "in English"))}",
                    LabelSettings = UiTheme.Label(UiTheme.Body, 15, K, UiTheme.TextSoft), MouseFilter = MouseFilterEnum.Ignore,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                line.AddChild(text);
                body.AddChild(line);
            }
            Line(L("Los vídeos pertenecen a sus autores; CA-VAL-AIM solo enlaza a ellos y no descarga ni incrusta su contenido.",
                "The videos belong to their authors; CA-VAL-AIM only links to them and does not download or embed their content."),
                UiTheme.Body, 13, UiTheme.Faint);
        }
        body.AddChild(Spacer(0, 20 * K));
    }

    /// <summary>Opens a credited video in the default browser (only https links from the built-in list).</summary>
    static void OpenVideo(string url)
    {
        if (!url.StartsWith("https://www.youtube.com/watch?v=", StringComparison.Ordinal)) return;
        try { OS.ShellOpen(url); }
        catch (Exception e) { Log.Error("Academy: could not open the link: " + e.Message); }
    }
}
