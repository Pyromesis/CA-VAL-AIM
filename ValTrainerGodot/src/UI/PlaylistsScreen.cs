using Godot;
using ValTrainer.Core;
using ValTrainer.Modes;

namespace ValTrainer.UI;

/// <summary>
/// PLAYLISTS: build a named set from the whole drill library and play it back to back. Left: your playlists and the
/// create field; right: the selected playlist's drills and START. Same minimalist language as the main menu.
/// </summary>
public partial class PlaylistsScreen : ScreenBase
{
    static string? selectedId;
    static readonly List<(Func<TrainingMode> Make, TrainingMode Info)> modes =
        ModeRegistry.All.Select(f => (Make: f, Info: f())).Where(m => m.Info.Category != MenuScreen.AgentsCategory).ToList();

    readonly List<(MDrillRow Row, Playlist Playlist)> listRows = new();
    readonly List<(MDrillRow Row, string Key)> drillRows = new();
    VBoxContainer? listSlot, drillSlot;
    LineEdit? nameBox;
    MText? countLine;
    Playlist? selected;

    protected override bool AnimatedBackdrop => false;

    protected override void Back() => Main.I.ShowMenu();

    static string L(string en, string es) => Loc.Spanish ? es : en;

    protected override void Build()
    {
        listRows.Clear();
        drillRows.Clear();
        nameBox = null;
        listSlot = null;
        drillSlot = null;
        countLine = null;
        selected = null;

        var all = PlaylistStore.All;
        if (selectedId == null || all.All(p => p.Id != selectedId)) selectedId = all.FirstOrDefault()?.Id;
        selected = all.FirstOrDefault(p => p.Id == selectedId);

        float k = K;
        var vp = GetViewportRect().Size;
        float side = Mathf.Clamp(vp.X * 0.05f, 28 * k, 100 * k);

        AddChild(new MinimalBackdrop());
        var page = Margins(VBox(0), side, 34 * k, side, 24 * k);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(page);
        var col = (VBoxContainer)page.GetChild(0);

        // ---- header ----
        var head = HBox(0);
        head.CustomMinimumSize = new Vector2(0, 34 * k);
        head.AddChild(new MenuScreen.Wordmark { K = k, CustomMinimumSize = new Vector2(220 * k, 34 * k) });
        head.AddChild(Expander());
        var nav = HBox(34 * k);
        nav.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(nav);
        var back = new MLink { K = k, Label = L("← MENU", "← MENÚ"), Px = 12.5f, Delay = 0.06f, TooltipText = L("Back to the main menu", "Volver al menú principal") };
        back.Pressed += Main.I.ShowMenu;
        nav.AddChild(back);
        col.AddChild(head);

        col.AddChild(Spacer(0, 38 * k));
        col.AddChild(new MText { K = k, Text = "PLAYLISTS", Font = UiTheme.HudWide, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.08f });
        col.AddChild(Spacer(0, 4 * k));
        col.AddChild(new MText { K = k, Text = L("Your own sets.", "Tus propias series."), Font = UiTheme.Light, Px = 54, Color = UiTheme.Text, Delay = 0.12f });
        col.AddChild(Spacer(0, 4 * k));
        col.AddChild(new MText
        {
            K = k, Text = L("Pick drills from the library and play them back to back.",
                "Elige ejercicios de la biblioteca y juégalos uno tras otro."),
            Font = UiTheme.Body, Px = 15, Color = UiTheme.Dim, Delay = 0.16f,
        });
        col.AddChild(Spacer(0, 26 * k));
        col.AddChild(new MRule { Delay = 0.2f });
        col.AddChild(Spacer(0, 24 * k));

        // ---- body ----
        var body = HBox(Mathf.Clamp(vp.X * 0.04f, 30 * k, 80 * k));
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(body);
        float leftW = Mathf.Clamp(vp.X * 0.22f, 300 * k, 380 * k);

        var left = VBox(0);
        left.CustomMinimumSize = new Vector2(leftW, 0);
        body.AddChild(left);
        left.AddChild(new MText { K = k, Text = L("YOUR PLAYLISTS", "TUS SERIES"), Font = UiTheme.HudWide, Px = 11f, Color = UiTheme.Faint, Delay = 0.22f });
        left.AddChild(Spacer(0, 8 * k));

        var listScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        left.AddChild(listScroll);
        listSlot = VBox(0);
        listSlot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        listScroll.AddChild(listSlot);
        FillList(k);

        left.AddChild(Spacer(0, 14 * k));
        nameBox = new LineEdit
        {
            PlaceholderText = L("Playlist name", "Nombre de la serie"), MaxLength = 40,
            CustomMinimumSize = new Vector2(0, 40 * k),
        };
        SettingsScreen.StyleEdit(nameBox, k, 14, UiTheme.Body);
        left.AddChild(nameBox);
        left.AddChild(Spacer(0, 8 * k));
        var create = new MAction
        {
            K = k, Label = L("CREATE PLAYLIST", "CREAR SERIE"), Px = 12.5f, Delay = 0.24f,
            CustomMinimumSize = new Vector2(0, 44 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        create.Pressed += CreatePlaylist;
        nameBox.TextSubmitted += _ => CreatePlaylist();
        left.AddChild(create);

        var right = VBox(0);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(right);
        FillDetail(right, k);

        col.AddChild(Spacer(0, 16 * k));
        col.AddChild(new MRule { Delay = 0.45f });
        col.AddChild(Spacer(0, 10 * k));
        col.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 12, Color = UiTheme.Faint, Delay = 0.5f,
            Text = "Fan-made trainer · not endorsed by or affiliated with Riot Games",
        });
    }

    void FillList(float k)
    {
        if (listSlot == null) return;
        foreach (var c in listSlot.GetChildren()) { listSlot.RemoveChild(c); c.QueueFree(); }
        listRows.Clear();
        if (PlaylistStore.All.Count == 0)
        {
            listSlot.AddChild(new MText
            {
                K = k, Text = L("No playlists yet.", "Aún no hay series."), Font = UiTheme.Body, Px = 14, Color = UiTheme.Faint,
                Delay = 0.26f, CustomMinimumSize = new Vector2(0, 42 * k),
            });
            return;
        }
        int i = 0;
        foreach (var p in PlaylistStore.All)
        {
            var row = new MDrillRow
            {
                K = k, Title = p.Name, Meta = $"{p.Modes.Count} {L("drills", "ejercicios")}",
                Value = p == selected ? L("OPEN", "ABIERTA") : "", Marked = p == selected,
                Delay = 0.26f + Math.Min(0.25f, i++ * 0.03f),
                CustomMinimumSize = new Vector2(0, 58 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = p.Name,
            };
            var pp = p;
            row.Pressed += () => { selectedId = pp.Id; Callable.From(Rebuild).CallDeferred(); };
            listRows.Add((row, p));
            listSlot.AddChild(row);
        }
    }

    void FillDetail(VBoxContainer right, float k)
    {
        if (selected == null)
        {
            right.AddChild(new MText
            {
                K = k, Text = L("Create a playlist to get started.", "Crea una serie para empezar."),
                Font = UiTheme.Light, Px = 30, Color = UiTheme.TextSoft, Delay = 0.24f,
            });
            right.AddChild(Spacer(0, 8 * k));
            right.AddChild(new MText
            {
                K = k, Text = L("Name it on the left, then click drills to add them here.",
                    "Ponle nombre a la izquierda y luego haz clic en los ejercicios para añadirlos aquí."),
                Font = UiTheme.Body, Px = 14, Color = UiTheme.Faint, Delay = 0.28f,
            });
            return;
        }

        var p = selected;
        var head = HBox(0);
        var title = new MText
        {
            K = k, Text = p.Name, Font = UiTheme.Light, Px = 34, Color = UiTheme.Text, Delay = 0.22f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 48 * k),
        };
        head.AddChild(title);
        var del = new MLink
        {
            K = k, Label = L("DELETE", "ELIMINAR"), Px = 11.5f, Delay = 0.26f,
            TooltipText = L("Delete this playlist", "Eliminar esta serie"), SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        del.Pressed += () =>
        {
            PlaylistStore.Delete(p);
            selectedId = PlaylistStore.All.FirstOrDefault()?.Id;
            Callable.From(Rebuild).CallDeferred();
        };
        head.AddChild(del);
        right.AddChild(head);

        countLine = new MText
        {
            K = k, Font = UiTheme.Body, Px = 13, Color = UiTheme.Faint, Delay = 0.25f,
            Text = p.Modes.Count == 0 ? L("no drills yet · click one below", "aún no hay ejercicios · haz clic en uno abajo")
                : $"{p.Modes.Count} {L("drills", "ejercicios")} · {p.Modes.Count} × 60 s",
        };
        right.AddChild(countLine);
        right.AddChild(Spacer(0, 12 * k));

        var start = new MAction
        {
            K = k, Primary = true, Label = L("START PLAYLIST", "JUGAR SERIE"), Px = 13.5f, Delay = 0.28f,
            CustomMinimumSize = new Vector2(250 * k, 48 * k),
            TooltipText = L("Play every drill in order; the last one shows the results",
                "Juega todos los ejercicios en orden; el último muestra los resultados"),
        };
        start.Disabled = p.Modes.Count == 0;
        start.Pressed += () => PlaylistRunner.Play(p);
        var startRow = HBox(18 * k);
        startRow.AddChild(start);
        startRow.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 12.5f, Color = UiTheme.Faint, Delay = 0.32f,
            Text = p.Modes.Count == 0 ? L("Add at least one drill", "Añade al menos un ejercicio")
                : L("Runs back to back · Esc → menu leaves the set", "Seguidos · Esc → menú sale de la serie"),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        right.AddChild(startRow);
        right.AddChild(Spacer(0, 16 * k));
        right.AddChild(new MRule { Delay = 0.32f });
        right.AddChild(Spacer(0, 10 * k));
        right.AddChild(new MText
        {
            K = k, Text = L("DRILL LIBRARY · CLICK TO ADD OR REMOVE", "BIBLIOTECA · HAZ CLIC PARA AÑADIR O QUITAR"),
            Font = UiTheme.HudWide, Px = 11f, Color = UiTheme.Faint, Delay = 0.34f,
        });
        right.AddChild(Spacer(0, 6 * k));

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        right.AddChild(scroll);
        drillSlot = VBox(0);
        drillSlot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(drillSlot);

        int cols = GetViewportRect().Size.X / k >= 1450 ? 2 : 1;
        var grid = new GridContainer { Columns = cols, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", (int)(24 * k));
        grid.AddThemeConstantOverride("v_separation", 0);
        int i = 0;
        foreach (var (make, info) in modes)
        {
            bool inSet = p.Modes.Contains(info.Key);
            var row = new MDrillRow
            {
                K = k, Title = info.Name, Meta = $"{Loc.T(info.Category)}  ·  {Loc.T(info.Description)}",
                Value = inSet ? L("IN PLAYLIST", "EN LA SERIE") : L("+ ADD", "+ AÑADIR"), Marked = inSet,
                Delay = 0.36f + Math.Min(0.3f, i++ * 0.008f),
                CustomMinimumSize = new Vector2(0, 58 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = Loc.T(info.Description),
            };
            string key = info.Key;
            row.Pressed += () =>
            {
                bool added = PlaylistStore.Toggle(p, key);
                row.Value = added ? L("IN PLAYLIST", "EN LA SERIE") : L("+ ADD", "+ AÑADIR");
                row.Marked = added;
                row.QueueRedraw();
                if (countLine != null)
                {
                    countLine.Text = p.Modes.Count == 0
                        ? L("no drills yet · click one below", "aún no hay ejercicios · haz clic en uno abajo")
                        : $"{p.Modes.Count} {L("drills", "ejercicios")}";
                    countLine.QueueRedraw();
                }
                foreach (var (lr, lp) in listRows)
                    if (lp == p) { lr.Meta = $"{p.Modes.Count} {L("drills", "ejercicios")}"; lr.QueueRedraw(); }
            };
            drillRows.Add((row, key));
            grid.AddChild(row);
        }
        drillSlot.AddChild(grid);
    }

    void CreatePlaylist()
    {
        var p = PlaylistStore.Create(nameBox?.Text ?? "");
        selectedId = p.Id;
        Callable.From(Rebuild).CallDeferred();
    }
}
