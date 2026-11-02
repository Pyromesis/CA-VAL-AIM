using System.Globalization;
using Godot;
using CaValAim.Core;
using CaValAim.Game;
using CaValAim.Modes;

namespace CaValAim.UI;

/// <summary>
/// ROUTINES: Voltaic's VALORANT routines and glossary, cartoon's guide and external playlists, an Excel-style data
/// table with CSV export, and every credit/legal notice. Built-in routines are adaptations to CA-VAL-AIM drills; the
/// original scenario names are kept for attribution. Nothing here claims an endorsement by Voltaic, cartoon, Riot
/// Games, Aim Lab or Kovaak's.
/// </summary>
public partial class RoutinesScreen : ScreenBase
{
    public enum Tab { Routines, Guide, Glossary, Data, Credits }

    static Tab tab = Tab.Routines;
    static string? routineId;
    static int stepIndex = -1;
    static int termIndex;
    static string query = "";

    /// <summary>Dev-only: <c>--screen routines --tab glossary</c> opens that tab directly.</summary>
    public static string? DevTab { get; set; }

    /// <summary>Dev-only: <c>--term "Anti-Mirroring"</c> preselects that glossary entry (chips screenshots).</summary>
    public static string? DevTerm { get; set; }

    /// <summary>Dev-only: <c>--routine cartoon-flicks</c> preselects that routine (table screenshots).</summary>
    public static string? DevRoutine { get; set; }

    /// <summary>Opens the routines screen on a specific tab (glossary term chips, the guide, the CSV data).</summary>
    public static void OpenTab(Tab which)
    {
        tab = which;
        Main.I.ShowRoutines();
    }

    /// <summary>Opens the routines screen with one routine selected (a glossary term or a coach reference).</summary>
    public static void OpenRoutine(string id)
    {
        tab = Tab.Routines;
        routineId = id;
        stepIndex = -1;
        Main.I.ShowRoutines();
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    readonly List<RoutineStepRow> stepRows = new();
    readonly List<VButton> tabButtons = new();
    readonly List<GlossaryTerm> glossaryItems = new();
    VBoxContainer? stepDetail;
    GlossaryList? glossaryList;
    VBoxContainer? glossaryDetail;
    MText? statusLine;

    protected override bool AnimatedBackdrop => false;

    protected override void Back() => Main.I.ShowMenu();

    static string L(string en, string es) => Loc.Spanish ? es : en;

    protected override void Build()
    {
        if (DevTab is { } devTab) { tab = ParseTab(devTab); DevTab = null; }
        stepRows.Clear();
        tabButtons.Clear();
        stepDetail = null;
        glossaryList = null;
        glossaryDetail = null;
        statusLine = null;

        float k = K;
        var vp = GetViewportRect().Size;
        float side = Mathf.Clamp(vp.X * 0.05f, 28 * k, 100 * k);

        AddChild(new MinimalBackdrop());
        var page = Margins(VBox(0), side, 18 * k, side, 10 * k);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(page);
        var col = (VBoxContainer)page.GetChild(0);

        // ---- header ----
        var head = HBox(0);
        head.CustomMinimumSize = new Vector2(0, 34 * k);
        head.AddChild(new MenuScreen.Wordmark { K = k, CustomMinimumSize = new Vector2(200 * k, 34 * k) });
        head.AddChild(Expander());
        var nav = HBox(24 * k);
        nav.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(nav);
        nav.AddChild(MkLink("VOLTAIC DISCORD ↗", () => Open(CommunityLinks.VoltaicDiscord),
            L("Official Voltaic Discord server", "Servidor oficial de Discord de Voltaic")));
        nav.AddChild(MkLink("VOLTAIC.GG ↗", () => Open(CommunityLinks.VoltaicSite),
            L("Voltaic's website", "Web oficial de Voltaic")));
        nav.AddChild(MkLink(L("← MENU", "← MENÚ"), Main.I.ShowMenu,
            L("Back to the main menu", "Volver al menú principal")));
        col.AddChild(head);

        col.AddChild(Spacer(0, 12 * k));

        // ---- title + tabs ----
        var titleRow = HBox(20 * k);
        var titles = VBox(1 * k);
        titles.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titles.AddChild(new MText
        {
            K = k, Text = L("VOLTAIC · CARTOON · COMMUNITY", "VOLTAIC · CARTOON · COMUNIDAD"),
            Font = UiTheme.HudWide, Px = 11, Color = UiTheme.Faint, Delay = 0.04f,
        });
        titles.AddChild(new MText
        {
            K = k, Text = L("Routines that transfer to VALORANT", "Rutinas que se trasladan a VALORANT"),
            Font = UiTheme.Light, Px = 30, Color = UiTheme.Text, Delay = 0.07f,
        });
        titleRow.AddChild(titles);
        int totalSteps = RoutineLibrary.Routines.Sum(r => r.Steps.Count);
        titleRow.AddChild(new MText
        {
            K = k, Text = $"{RoutineLibrary.Routines.Count} {L("routines", "rutinas")} · {totalSteps} {L("steps", "pasos")} · {Glossary.All.Length} {L("terms", "términos")}",
            Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint, Delay = 0.09f,
            SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(350 * k, 0),
        });
        col.AddChild(titleRow);

        col.AddChild(Spacer(0, 10 * k));
        var tabs = HBox(3 * k);
        col.AddChild(tabs);
        TabButton(tabs, Tab.Routines, L("ROUTINES", "RUTINAS"), k);
        TabButton(tabs, Tab.Guide, L("GUIDE", "GUÍA"), k);
        TabButton(tabs, Tab.Glossary, L("GLOSSARY", "GLOSARIO"), k);
        TabButton(tabs, Tab.Data, L("DATA", "DATOS"), k);
        TabButton(tabs, Tab.Credits, L("CREDITS", "CRÉDITOS"), k);

        col.AddChild(Spacer(0, 10 * k));
        var body = VBox(0);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(body);
        switch (tab)
        {
            case Tab.Routines: BuildRoutines(body, k); break;
            case Tab.Guide: BuildGuide(body, k); break;
            case Tab.Glossary: BuildGlossary(body, k); break;
            case Tab.Data: BuildData(body, k); break;
            default: BuildCredits(body, k); break;
        }

        col.AddChild(Spacer(0, 4 * k));
        statusLine = new MText
        {
            K = k, Text = "", Font = UiTheme.Body, Px = 11, Color = UiTheme.Good, Delay = 0.2f,
            CustomMinimumSize = new Vector2(0, 14 * k),
        };
        col.AddChild(statusLine);
        col.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 10.5f, Color = UiTheme.Faint, Delay = 0.5f,
            Text = L("Fan-made · not affiliated with or endorsed by Riot Games, Voltaic, Aim Lab, Kovaak's or cartoon",
                "Proyecto fan · sin afiliación ni respaldo de Riot Games, Voltaic, Aim Lab, Kovaak's ni cartoon"),
        });
    }

    static Tab ParseTab(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "guide" or "guia" => Tab.Guide,
        "glossary" or "glosario" => Tab.Glossary,
        "data" or "datos" => Tab.Data,
        "credits" or "creditos" or "créditos" => Tab.Credits,
        _ => Tab.Routines,
    };

    void TabButton(HBoxContainer row, Tab which, string label, float k)
    {
        var b = new VButton
        {
            Kind = VButton.Look.Select, K = k, Label = label, FontPx = 12.5f,
            CustomMinimumSize = new Vector2(0, 34 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Selected = tab == which,
        };
        b.Pressed += () =>
        {
            if (tab == which) return;
            tab = which;
            Callable.From(Rebuild).CallDeferred();
        };
        tabButtons.Add(b);
        row.AddChild(b);
    }

    MLink MkLink(string label, Action action, string tip)
    {
        var l = new MLink { K = K, Label = label, Px = 12, Delay = 0.08f, TooltipText = tip };
        l.Pressed += action;
        return l;
    }

    Label Para(string text, float px, Color color)
    {
        var l = new Label
        {
            Text = text,
            LabelSettings = UiTheme.Label(UiTheme.Body, px, K, color),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(60 * K, 0),
        };
        return l;
    }

    Label Kicker(string text, Color color)
    {
        var l = new Label
        {
            Text = text,
            LabelSettings = UiTheme.Label(UiTheme.HudWide, 10.5f, K, color),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
        };
        return l;
    }

    static void Clear(Node n)
    {
        foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); }
    }

    void SetStatus(string text, Color? color = null)
    {
        if (statusLine == null) return;
        statusLine.Text = text;
        statusLine.Color = color ?? UiTheme.Good;
        statusLine.QueueRedraw();
    }

    void Open(string url)
    {
        if (!CommunityLinks.Open(url))
            SetStatus(L("This link could not be opened.", "No se pudo abrir este enlace."), UiTheme.Warn);
    }

    // ---------------- ROUTINES ----------------

    void BuildRoutines(VBoxContainer body, float k)
    {
        var all = RoutineLibrary.Routines;
        if (DevRoutine is { Length: > 0 } devRoutine)
        {
            DevRoutine = null;
            routineId = devRoutine;
            stepIndex = -1;
        }
        if (routineId == null || all.All(r => r.Id != routineId)) routineId = all.FirstOrDefault()?.Id;
        var routine = RoutineLibrary.Find(routineId ?? "");

        var row = HBox(26 * k);
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(row);

        float leftW = Mathf.Clamp(GetViewportRect().Size.X * 0.24f, 270 * k, 360 * k);
        var left = VBox(0);
        left.CustomMinimumSize = new Vector2(leftW, 0);
        row.AddChild(left);
        left.AddChild(Kicker(L("ROUTINES", "RUTINAS"), UiTheme.Faint));
        left.AddChild(Spacer(0, 6 * k));
        var listScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var list = VBox(0);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        listScroll.AddChild(list);
        left.AddChild(listScroll);

        int i = 0;
        foreach (var r in all)
        {
            var p = RoutineStore.Find(r.Id);
            string value = p == null ? L("NEW", "NUEVA") : p.Completed ? L("DONE", "HECHA") : $"{p.FurthestStep}/{r.Steps.Count}";
            string meta = $"{r.Source} · {(r.Minutes > 0 ? $"{r.Minutes:0.#} min" : L("external", "externa"))}";
            var rr = r;
            var item = new MDrillRow
            {
                K = k, Title = r.Name, Meta = meta, Value = value, Marked = r.Id == routineId,
                Delay = 0.12f + Math.Min(0.2f, i++ * 0.02f),
                CustomMinimumSize = new Vector2(0, 54 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = $"{r.Name} · {r.Source} · {meta}",
            };
            item.Pressed += () =>
            {
                routineId = rr.Id;
                stepIndex = -1;
                Callable.From(Rebuild).CallDeferred();
            };
            list.AddChild(item);
        }

        left.AddChild(Spacer(0, 10 * k));
        left.AddChild(MkLink("VOLTAIC DISCORD ↗", () => Open(CommunityLinks.VoltaicDiscord),
            L("Official Voltaic Discord server", "Servidor oficial de Discord de Voltaic")));

        var right = VBox(0);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(right);
        if (routine == null)
        {
            right.AddChild(new MText
            {
                K = k, Text = L("No routines available.", "No hay rutinas disponibles."),
                Font = UiTheme.Light, Px = 26, Color = UiTheme.TextSoft,
            });
            return;
        }
        BuildRoutineDetail(right, routine, k);
        Loc.Apply(this);
    }

    void BuildRoutineDetail(VBoxContainer right, Routine r, float k)
    {
        var head = HBox(16 * k);
        var titles = VBox(0);
        titles.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titles.AddChild(new MText
        {
            K = k, Text = r.Name, Font = UiTheme.Light, Px = 25, Color = UiTheme.Text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        titles.AddChild(new MText
        {
            K = k, Text = $"{r.Source} · {(Loc.Spanish ? r.LevelEs : r.LevelEn)}",
            Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        head.AddChild(titles);
        head.AddChild(new MText
        {
            K = k, Text = r.Minutes > 0 ? $"{r.Minutes:0.#} MIN" : L("EXTERNAL", "EXTERNA"),
            Font = UiTheme.Hud, Px = 24, Color = UiTheme.TextSoft, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(105 * k, 0), Align = HorizontalAlignment.Right,
        });
        right.AddChild(head);
        right.AddChild(Spacer(0, 3 * k));
        right.AddChild(Para(L(r.DescriptionEn, r.DescriptionEs), 11.5f, UiTheme.Dim));
        right.AddChild(Spacer(0, 2 * k));

        var credit = Para(L(r.CreditEn, r.CreditEs), 10.5f, UiTheme.Faint);
        right.AddChild(credit);
        right.AddChild(Spacer(0, 7 * k));

        var actions = HBox(8 * k);
        if (r.Playable)
        {
            var play = new MAction
            {
                K = k, Primary = true, Label = L("PLAY ROUTINE", "JUGAR RUTINA"), Px = 12.5f,
                CustomMinimumSize = new Vector2(220 * k, 40 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                TooltipText = L("Play every step in order; each step gets the sensitivity and duration its source asks for",
                    "Juega cada paso en orden; cada paso usa la sensibilidad y duración que pide su fuente"),
            };
            play.Pressed += () =>
            {
                if (!r.Play()) SetStatus(L("This routine cannot be played yet.", "Esta rutina todavía no se puede jugar."), UiTheme.Warn);
            };
            actions.AddChild(play);

            var save = new MAction
            {
                K = k, Label = L("SAVE AS PLAYLIST", "GUARDAR SERIE"), Px = 12,
                CustomMinimumSize = new Vector2(235 * k, 40 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                TooltipText = L("Save an editable copy in PLAYLISTS", "Guarda una copia editable en SERIES"),
            };
            save.Pressed += () =>
            {
                var p = r.SaveAsPlaylist();
                if (p == null) { SetStatus(L("Nothing to save.", "No hay nada que guardar."), UiTheme.Warn); return; }
                PlaylistsScreen.Select(p.Id);
                SetStatus($"{L("Saved as a playlist", "Guardada como serie")} · {p.Name}");
            };
            actions.AddChild(save);
        }
        else if (r.External && !string.IsNullOrEmpty(r.ShareCode))
        {
            var copy = new MAction
            {
                K = k, Primary = true, Label = L("COPY SHARE CODE", "COPIAR CÓDIGO"), Px = 12.5f,
                CustomMinimumSize = new Vector2(185 * k, 40 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                TooltipText = L("Copy the Kovaak's share code; the playlist itself belongs to its author",
                    "Copia el código de Kovaak's; la playlist pertenece a su autor"),
            };
            copy.Pressed += () =>
            {
                try
                {
                    DisplayServer.ClipboardSet(r.ShareCode!);
                    SetStatus($"{L("Copied", "Copiado")}: {r.ShareCode}");
                }
                catch (Exception e)
                {
                    Log.Error($"Clipboard failed: {e.Message}");
                    SetStatus(L("Could not copy the code.", "No se pudo copiar el código."), UiTheme.Warn);
                }
            };
            actions.AddChild(copy);
        }
        else
        {
            var guide = new MAction
            {
                K = k, Primary = true, Label = L("OPEN VOLTAIC GUIDE", "ABRIR GUÍA"), Px = 12.5f,
                CustomMinimumSize = new Vector2(225 * k, 40 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                TooltipText = L("Open the original document in your browser", "Abre el documento original en el navegador"),
            };
            guide.Pressed += () => Open(r.SourceDoc);
            actions.AddChild(guide);
        }

        var source = new MAction
        {
            K = k, Label = L("SOURCE DOCUMENT", "DOCUMENTO"), Px = 12,
            CustomMinimumSize = new Vector2(210 * k, 40 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = L("Open the original Voltaic/cartoon document", "Abre el documento original de Voltaic/cartoon"),
        };
        source.Pressed += () => Open(r.SourceDoc);
        actions.AddChild(source);
        actions.AddChild(Expander());
        right.AddChild(actions);
        right.AddChild(Spacer(0, 4 * k));

        var progress = RoutineStore.Find(r.Id);
        string progressText = progress == null
            ? L("Not started · progress is saved automatically", "Sin empezar · el progreso se guarda solo")
            : progress.Completed
                ? L("Completed · progress is saved automatically", "Completada · el progreso se guarda solo")
                : $"{L("Reached step", "Llegaste al paso")} {progress.FurthestStep}/{r.Steps.Count} · {L("progress is saved automatically", "el progreso se guarda solo")}";
        var metaLine = HBox(14 * k);
        metaLine.AddChild(new MText
        {
            K = k, Text = progressText, Font = UiTheme.Body, Px = 10.5f, Color = UiTheme.Faint,
            SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(330 * k, 0),
        });
        metaLine.AddChild(new MText
        {
            K = k, Text = L("ADAPTED TO CA-VAL-AIM DRILLS", "ADAPTADA A EJERCICIOS DE CA-VAL-AIM"),
            Font = UiTheme.HudWide, Px = 9.5f, Color = UiTheme.Faint, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, Align = HorizontalAlignment.Right,
        });
        right.AddChild(metaLine);
        right.AddChild(Spacer(0, 5 * k));

        right.AddChild(new RoutineStepHeader { K = k, CustomMinimumSize = new Vector2(0, 22 * k) });
        if (stepIndex < 0 || stepIndex >= r.Steps.Count) stepIndex = 0;
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var rows = VBox(0);
        rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(rows);
        right.AddChild(scroll);

        for (int i = 0; i < r.Steps.Count; i++)
        {
            int at = i;
            var row = new RoutineStepRow(r.Steps[i], i, k)
            {
                Selected = i == stepIndex,
                CustomMinimumSize = new Vector2(0, 34 * k),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.Pressed += () =>
            {
                stepIndex = at;
                foreach (var other in stepRows) other.SetSelected(other.Index == at);
                ShowStepDetail(r, k);
            };
            stepRows.Add(row);
            rows.AddChild(row);
        }

        right.AddChild(Spacer(0, 6 * k));
        var detailPanel = new VPanel { K = k, Pad = 11, CustomMinimumSize = new Vector2(0, 250 * k) };
        var detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        stepDetail = VBox(2 * k);
        stepDetail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailScroll.AddChild(stepDetail);
        detailPanel.AddChild(detailScroll);
        right.AddChild(detailPanel);
        ShowStepDetail(r, k);
    }

    void ShowStepDetail(Routine r, float k)
    {
        if (stepDetail == null) return;
        Clear(stepDetail);
        if (stepIndex < 0 || stepIndex >= r.Steps.Count) return;
        var s = r.Steps[stepIndex];
        stepDetail.AddChild(new MText
        {
            K = k, Text = $"{L("STEP", "PASO")} {stepIndex + 1} · {s.RunsLabel} · {L("ORIGINAL SCENARIO", "ESCENARIO ORIGINAL")}",
            Font = UiTheme.HudWide, Px = 9.5f, Color = UiTheme.Faint,
        });
        // The original task and the drill that plays it here wrap at full width: no single-line ellipsis on the two
        // fields the screen exists for.
        stepDetail.AddChild(Para(s.Scenario, 12.5f, UiTheme.Text));
        if (s.DrillName.Length > 0)
            stepDetail.AddChild(Para($"{L("HERE", "AQUÍ")}: {s.DrillName}", 10.5f, UiTheme.Teal));
        stepDetail.AddChild(Spacer(0, 2 * k));
        stepDetail.AddChild(Kicker(L("HOW TO PLAY", "CÓMO JUGARLO"), UiTheme.Faint));
        stepDetail.AddChild(Para(L(s.HowEn, s.HowEs), 11, UiTheme.TextSoft));
        stepDetail.AddChild(Spacer(0, 2 * k));
        stepDetail.AddChild(Kicker(L("WHY IT MATTERS", "POR QUÉ IMPORTA"), UiTheme.Faint));
        stepDetail.AddChild(Para(L(s.WhyEn, s.WhyEs), 11, UiTheme.Dim));
        string? note = L(s.SensNoteEn ?? "", s.SensNoteEs ?? "");
        if (!string.IsNullOrWhiteSpace(note))
        {
            stepDetail.AddChild(Spacer(0, 2 * k));
            stepDetail.AddChild(Para(note, 10.5f, UiTheme.Warn));
        }
        Loc.Apply(this);
    }

    // ---------------- GUIDE ----------------

    void BuildGuide(VBoxContainer body, float k)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var v = VBox(12 * k);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(v);
        body.AddChild(scroll);

        v.AddChild(new MText
        {
            K = k, Text = L("CARTOON · HOW TO IMPROVE YOUR AIM", "CARTOON · CÓMO MEJORAR TU PUNTERÍA"),
            Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint,
        });
        v.AddChild(new MText
        {
            K = k, Text = L("The guide, summarised", "La guía, resumida"), Font = UiTheme.Light, Px = 28, Color = UiTheme.Text,
        });
        v.AddChild(Para(L(
            "Cartoon's public guide is written for VALORANT players who want to improve faster than playing alone. These are our summaries and translations of its ideas; the original document is linked below and this screen does not claim to reproduce it word for word.",
            "La guía pública de cartoon está escrita para jugadores de VALORANT que quieren mejorar más rápido que jugando solo. Estos son nuestros resúmenes y traducciones de sus ideas; el documento original está enlazado abajo y esta pantalla no pretende reproducirlo palabra por palabra."),
            11.5f, UiTheme.Dim));
        var links = HBox(22 * k);
        links.AddChild(MkLink(L("CARTOON'S GUIDE ↗", "GUÍA DE CARTOON ↗"), () => Open(CommunityLinks.CartoonGuide),
            L("Open the original guide", "Abrir la guía original")));
        links.AddChild(MkLink("TWITCH ↗", () => Open(CommunityLinks.CartoonTwitch), "https://www.twitch.tv/cartoon"));
        links.AddChild(MkLink("YOUTUBE ↗", () => Open(CommunityLinks.CartoonYouTube), "https://www.youtube.com/c/cartoonfps"));
        v.AddChild(links);
        v.AddChild(new MRule { Delay = 0.15f });

        int n = 0;
        foreach (var section in RoutineLibrary.Guide)
        {
            n++;
            var head = HBox(9 * k);
            head.AddChild(new MText
            {
                K = k, Text = n.ToString("00", Inv), Font = UiTheme.Hud, Px = 18, Color = UiTheme.Accent,
                SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(40 * k, 0),
            });
            head.AddChild(new MText
            {
                K = k, Text = Loc.Spanish ? section.TitleEs : section.TitleEn, Font = UiTheme.Light, Px = 20,
                Color = UiTheme.Text, SizeFlagsVertical = SizeFlags.ShrinkCenter,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
            v.AddChild(Spacer(0, 4 * k));
            v.AddChild(head);
            var paragraphs = Loc.Spanish ? section.Es : section.En;
            foreach (var paragraph in paragraphs) v.AddChild(Para(paragraph, 11.5f, UiTheme.TextSoft));
            v.AddChild(Spacer(0, 3 * k));
            if (n < RoutineLibrary.Guide.Length) v.AddChild(new MRule());
        }
    }

    // ---------------- GLOSSARY ----------------

    void BuildGlossary(VBoxContainer body, float k)
    {
        var toolbar = HBox(12 * k);
        var search = new LineEdit
        {
            PlaceholderText = L("Search a term or a definition…", "Busca un término o una definición…"),
            Text = query, MaxLength = 60, CustomMinimumSize = new Vector2(390 * k, 36 * k),
        };
        SettingsScreen.StyleEdit(search, k, 13, UiTheme.Body);
        toolbar.AddChild(search);
        var count = new MText
        {
            K = k, Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint,
            SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(190 * k, 0),
        };
        toolbar.AddChild(count);
        toolbar.AddChild(Expander());
        toolbar.AddChild(MkLink("VOLTAIC AIM GLOSSARY ↗", () => Open(CommunityLinks.AimGlossary),
            L("Open the original glossary", "Abrir el glosario original")));
        toolbar.AddChild(MkLink("DISCORD ↗", () => Open(CommunityLinks.VoltaicDiscord),
            L("Official Voltaic Discord server", "Servidor oficial de Discord de Voltaic")));
        body.AddChild(toolbar);
        body.AddChild(Spacer(0, 8 * k));

        var row = HBox(22 * k);
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(row);
        float listW = Mathf.Clamp(GetViewportRect().Size.X * 0.3f, 300 * k, 420 * k);
        var left = VBox(0);
        left.CustomMinimumSize = new Vector2(listW, 0);
        row.AddChild(left);
        left.AddChild(Kicker(L(
            $"{Glossary.All.Length} TERMS · {GlossaryActions.ImplementedCount} WITH A DESTINATION",
            $"{Glossary.All.Length} TÉRMINOS · {GlossaryActions.ImplementedCount} CON DESTINO"), UiTheme.Faint));
        left.AddChild(Spacer(0, 5 * k));
        var listScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        glossaryList = new GlossaryList { K = k, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        listScroll.AddChild(glossaryList);
        left.AddChild(listScroll);

        var panel = new VPanel { K = k, Pad = 18, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var detailScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        glossaryDetail = VBox(5 * k);
        glossaryDetail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailScroll.AddChild(glossaryDetail);
        panel.AddChild(detailScroll);
        row.AddChild(panel);

        void Refresh(bool keepSelection)
        {
            glossaryItems.Clear();
            glossaryItems.AddRange(Glossary.Search(query));
            if (glossaryItems.Count == 0) termIndex = -1;
            else if (!keepSelection || termIndex < 0 || termIndex >= glossaryItems.Count) termIndex = 0;
            glossaryList?.SetItems(glossaryItems, termIndex);
            count.Text = $"{glossaryItems.Count}/{Glossary.All.Length} {L("terms", "términos")}";
            count.QueueRedraw();
            ShowTermDetail(k);
        }

        search.TextChanged += text =>
        {
            query = text;
            Refresh(false);
        };
        glossaryList.Picked += i =>
        {
            termIndex = i;
            ShowTermDetail(k);
        };
        Refresh(true);
        if (DevTerm is { Length: > 0 } devTerm)
        {
            DevTerm = null;
            query = devTerm;
            search.Text = devTerm;
            Refresh(false);
        }
    }

    void ShowTermDetail(float k)
    {
        if (glossaryDetail == null) return;
        Clear(glossaryDetail);
        if (termIndex < 0 || termIndex >= glossaryItems.Count)
        {
            glossaryDetail.AddChild(new MText
            {
                K = k, Text = L("No matches.", "Sin resultados."), Font = UiTheme.Light, Px = 24, Color = UiTheme.TextSoft,
            });
            glossaryDetail.AddChild(Para(L("Try another word or clear the search.",
                "Prueba otra palabra o borra la búsqueda."), 12, UiTheme.Faint));
            return;
        }
        var term = glossaryItems[termIndex];
        glossaryDetail.AddChild(new MText
        {
            K = k, Text = term.Letter, Font = UiTheme.Hud, Px = 18, Color = UiTheme.Accent,
        });
        glossaryDetail.AddChild(new MText
        {
            K = k, Text = term.Term, Font = UiTheme.Light, Px = 30, Color = UiTheme.Text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        glossaryDetail.AddChild(Kicker(L("VOLTAIC AIM GLOSSARY · PARAPHRASED DEFINITION",
            "GLOSARIO DE VOLTAIC · DEFINICIÓN PARAFRASEADA"), UiTheme.Faint));
        glossaryDetail.AddChild(new MRule());
        glossaryDetail.AddChild(Kicker("ESPAÑOL", UiTheme.TextSoft));
        glossaryDetail.AddChild(Para(term.Es, 13, UiTheme.TextSoft));
        glossaryDetail.AddChild(Spacer(0, 4 * k));
        glossaryDetail.AddChild(Kicker("ENGLISH", UiTheme.TextSoft));
        glossaryDetail.AddChild(Para(term.En, 13, UiTheme.Dim));

        // Where the term is implemented: a real drill, the coach, a settings page or a data screen. Terms that only
        // describe hardware, genres or community slang say so instead of offering a button that leads nowhere.
        glossaryDetail.AddChild(Spacer(0, 6 * k));
        glossaryDetail.AddChild(Kicker(L("IMPLEMENTED IN CA-VAL-AIM", "IMPLEMENTADO EN CA-VAL-AIM"), UiTheme.TextSoft));
        var targets = GlossaryActions.For(term.Term);
        if (targets.Count > 0)
        {
            var chips = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            chips.AddThemeConstantOverride("h_separation", (int)(8 * k));
            chips.AddThemeConstantOverride("v_separation", (int)(6 * k));
            foreach (var target in targets)
            {
                string label = TargetLabel(target);
                int fs = UiTheme.Fs(10.5f, k);
                float width = Mathf.Max(140 * k, Gfx.TextW(UiTheme.HudWide, label, fs) + 80 * k);
                var chip = new MAction
                {
                    K = k, Label = label, Px = 10.5f, TooltipText = TargetWhy(target),
                    CustomMinimumSize = new Vector2(width, 32 * k),
                    SizeFlagsVertical = SizeFlags.ShrinkBegin,
                };
                var captured = target;
                chip.Pressed += () => GoToGlossaryTarget(captured);
                chips.AddChild(chip);
            }
            glossaryDetail.AddChild(chips);
        }
        else
        {
            var note = GlossaryActions.ContextNote(term.Term);
            glossaryDetail.AddChild(Para(L(
                "Context term: it describes hardware, a genre or community slang and has no drill in this trainer.",
                "Término de contexto: describe hardware, un género o jerga de la comunidad y no tiene ejercicio en este entrenador."),
                10.5f, UiTheme.Faint));
            if (note.En.Length > 0)
                glossaryDetail.AddChild(Para(L(note.En, note.Es), 10.5f, UiTheme.Dim));
        }
        glossaryDetail.AddChild(Spacer(0, 8 * k));
        var links = HBox(22 * k);
        links.AddChild(MkLink("VOLTAIC AIM GLOSSARY ↗", () => Open(CommunityLinks.AimGlossary),
            L("Open the original glossary", "Abrir el glosario original")));
        links.AddChild(MkLink("VOLTAIC.GG ↗", () => Open(CommunityLinks.VoltaicSite), "https://voltaic.gg/"));
        glossaryDetail.AddChild(links);
        glossaryDetail.AddChild(Para(L(
            "Definitions are short paraphrases written for CA-VAL-AIM, not copies of the source. The glossary belongs to the Voltaic Aim Journey project.",
            "Las definiciones son paráfrasis breves escritas para CA-VAL-AIM, no copias de la fuente. El glosario pertenece al proyecto Aim Journey de Voltaic."),
            10.5f, UiTheme.Faint));
    }

    static string TargetLabel(GlossaryTarget t)
    {
        string kind = t.Kind switch
        {
            GlossaryTargetKind.Drill => L("PLAY", "JUGAR"),
            GlossaryTargetKind.Coach => "COACH",
            GlossaryTargetKind.Settings => L("SETTINGS", "AJUSTES"),
            GlossaryTargetKind.SensLab => "SENS LAB",
            GlossaryTargetKind.Stats => "SERIES",
            GlossaryTargetKind.Reactions => L("REACTION", "REACCIÓN"),
            GlossaryTargetKind.Rewind => "REWIND",
            GlossaryTargetKind.Routines => L("ROUTINES", "RUTINAS"),
            GlossaryTargetKind.Playlists => "PLAYLISTS",
            GlossaryTargetKind.Agents => L("AGENTS", "AGENTES"),
            GlossaryTargetKind.Guide => L("GUIDE", "GUÍA"),
            GlossaryTargetKind.Data => L("DATA", "DATOS"),
            GlossaryTargetKind.LockIn => "LOCK-IN",
            _ => "",
        };
        string title = Loc.Spanish ? t.TitleEs : t.TitleEn;
        // Only drills carry their name on the chip: coach / settings / series chips stay short so they never truncate at
        // 1280×720, and the title still explains them in the tooltip.
        return t.Kind == GlossaryTargetKind.Drill && title.Length > 0 ? $"{kind} · {title}" : kind;
    }

    static string TargetWhy(GlossaryTarget t)
    {
        string why;
        if (Loc.Spanish && !string.IsNullOrEmpty(t.WhyEs)) why = t.WhyEs!;
        else if (!Loc.Spanish && !string.IsNullOrEmpty(t.WhyEn)) why = t.WhyEn!;
        else why = t.Kind switch
        {
            GlossaryTargetKind.Drill => L("Open this drill and play a run.", "Abre este ejercicio y juega una partida."),
            GlossaryTargetKind.Coach => L("Your runs, the coach's signal and the plan that fixes it.", "Tus runs, la señal del coach y el plan para corregirlo."),
            GlossaryTargetKind.Settings => L("The settings page for this parameter.", "La página de ajustes de este parámetro."),
            GlossaryTargetKind.SensLab => L("cm/360 and sensitivity measured from your own runs.", "cm/360 y sensibilidad medidos con tus propias runs."),
            GlossaryTargetKind.Stats => L("Your runs for this drill: best, average and last 20.", "Tus partidas de este ejercicio: mejor, media y últimas 20."),
            GlossaryTargetKind.Reactions => L("Reaction times by category.", "Tiempos de reacción por categoría."),
            GlossaryTargetKind.Rewind => L("The last saved run, second by second.", "La última run guardada, segundo a segundo."),
            GlossaryTargetKind.Routines => L("The routines that apply this term.", "Las rutinas que aplican este término."),
            GlossaryTargetKind.Playlists => L("Saved playlists and the coach's plan.", "Playlists guardadas y el plan del coach."),
            GlossaryTargetKind.Agents => L("Ability and utility drills per agent.", "Ejercicios de habilidades y utilidad por agente."),
            GlossaryTargetKind.Guide => L("How to practise, ordered and explained.", "Cómo practicar, ordenado y explicado."),
            GlossaryTargetKind.Data => L("Local CSV tables with routines and progress.", "Tablas CSV locales con rutinas y progreso."),
            GlossaryTargetKind.LockIn => L("Adaptive calibration for this skill.", "Calibración adaptativa de esta habilidad."),
            _ => "",
        };
        string title = Loc.Spanish ? t.TitleEs : t.TitleEn;
        return title.Length == 0 ? why : $"{title} — {why}";
    }

    void GoToGlossaryTarget(GlossaryTarget t)
    {
        switch (t.Kind)
        {
            case GlossaryTargetKind.Drill:
                var make = ModeRegistry.Find(t.Arg);
                if (make == null)
                {
                    SetStatus(L($"The drill “{t.Arg}” is not available in this build.",
                        $"El ejercicio «{t.Arg}» no está disponible en esta compilación."), UiTheme.Warn);
                    return;
                }
                Main.I.StartMode(make);
                break;
            case GlossaryTargetKind.Coach: Main.I.ShowProfile(); break;
            case GlossaryTargetKind.Settings: Main.I.ShowSettings(); break;
            case GlossaryTargetKind.SensLab: Main.I.ShowSensLab(); break;
            case GlossaryTargetKind.Stats: Main.I.ShowStats(); break;
            case GlossaryTargetKind.Reactions: Main.I.ShowReactionStats(); break;
            case GlossaryTargetKind.Rewind: OpenLatestRewind(); break;
            case GlossaryTargetKind.Routines:
                if (string.IsNullOrEmpty(t.Arg)) OpenTab(Tab.Routines); else OpenRoutine(t.Arg);
                break;
            case GlossaryTargetKind.Playlists: Main.I.ShowPlaylists(); break;
            case GlossaryTargetKind.Agents: Main.I.ShowAgents(); break;
            case GlossaryTargetKind.Guide: OpenTab(Tab.Guide); break;
            case GlossaryTargetKind.Data: OpenTab(Tab.Data); break;
            case GlossaryTargetKind.LockIn: Main.I.ShowLockIn(); break;
        }
    }

    void OpenLatestRewind()
    {
        foreach (var file in RunTelemetry.RecentFiles(8))
        {
            var run = RunTelemetry.Load(file);
            if (run == null || !RewindData.CanRewind(run)) continue;
            run.SourceFile ??= file;
            RewindScreen.Open(Main.I, run);
            return;
        }
        SetStatus(L("No saved run to rewind yet: play a drill and save the data first.",
            "Aún no hay ninguna run guardada para el Rewind: juega un ejercicio y guarda los datos primero."), UiTheme.Warn);
    }

    // ---------------- DATA ----------------

    void BuildData(VBoxContainer body, float k)
    {
        int steps = RoutineLibrary.Routines.Sum(r => r.Steps.Count);
        int started = RoutineStore.All.Count(p => p.TimesPlayed > 0);
        int done = RoutineStore.All.Count(p => p.Completed);

        var head = HBox(12 * k);
        var summary = VBox(1 * k);
        summary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        summary.AddChild(new MText
        {
            K = k, Text = L("LOCAL DATA · EXCEL-FRIENDLY CSV", "DATOS LOCALES · CSV COMPATIBLE CON EXCEL"),
            Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint,
        });
        summary.AddChild(new MText
        {
            K = k, Text = $"{RoutineLibrary.Routines.Count} {L("routines", "rutinas")} · {steps} {L("steps", "pasos")} · {Glossary.All.Length} {L("terms", "términos")} · {started} {L("started", "empezadas")} · {done} {L("completed", "completadas")}",
            Font = UiTheme.Body, Px = 12, Color = UiTheme.Dim,
        });
        head.AddChild(summary);

        var export = new MAction
        {
            K = k, Primary = true, Label = L("EXPORT ALL CSV", "EXPORTAR CSV"), Px = 12,
            CustomMinimumSize = new Vector2(205 * k, 38 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = L("Write the four tables to the data folder", "Escribe las cuatro tablas en la carpeta de datos"),
        };
        export.Pressed += () =>
        {
            try
            {
                var files = RoutineLibrary.ExportAll();
                SetStatus($"{L("Exported", "Exportados")} {files.Length} CSV · {Paths.DataDir}");
            }
            catch (Exception e)
            {
                Log.Error($"Routine CSV export failed: {e.Message}");
                SetStatus($"{L("Export failed", "Error al exportar")}: {e.Message}", UiTheme.Warn);
            }
        };
        head.AddChild(export);

        var folder = new MAction
        {
            K = k, Label = L("OPEN FOLDER", "CARPETA"), Px = 12,
            CustomMinimumSize = new Vector2(170 * k, 38 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = L("Open the data folder", "Abrir la carpeta de datos"),
        };
        folder.Pressed += () =>
        {
            try { OS.ShellOpen(Paths.DataDir); }
            catch (Exception e) { SetStatus($"{L("Could not open the folder", "No se pudo abrir la carpeta")}: {e.Message}", UiTheme.Warn); }
        };
        head.AddChild(folder);

        var copyPath = new MAction
        {
            K = k, Label = L("COPY PATH", "COPIAR"), Px = 12,
            CustomMinimumSize = new Vector2(160 * k, 38 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = L("Copy the data folder path", "Copiar la ruta de la carpeta de datos"),
        };
        copyPath.Pressed += () =>
        {
            try { DisplayServer.ClipboardSet(Paths.DataDir); SetStatus($"{L("Copied", "Copiado")}: {Paths.DataDir}"); }
            catch (Exception e) { SetStatus($"{L("Could not copy the path", "No se pudo copiar la ruta")}: {e.Message}", UiTheme.Warn); }
        };
        head.AddChild(copyPath);
        body.AddChild(head);
        body.AddChild(Spacer(0, 3 * k));
        body.AddChild(Para(L(
            "voltaic-routines.csv (one row per routine) · voltaic-routine-steps.csv (one row per step) · voltaic-glossary.csv · voltaic-progress.csv. UTF-8 with BOM and semicolon separators, so Excel opens them in columns directly. Join the step and progress tables on routine_id.",
            "voltaic-routines.csv (una fila por rutina) · voltaic-routine-steps.csv (una fila por paso) · voltaic-glossary.csv · voltaic-progress.csv. UTF-8 con BOM y separador punto y coma, para que Excel los abra directamente en columnas. Une las tablas de pasos y progreso por routine_id."),
            10.5f, UiTheme.Faint));
        body.AddChild(Spacer(0, 7 * k));

        var data = new List<RoutineDataEntry>();
        foreach (var r in RoutineLibrary.Routines)
        {
            var p = RoutineStore.Find(r.Id);
            for (int i = 0; i < r.Steps.Count; i++)
            {
                var s = r.Steps[i];
                data.Add(new RoutineDataEntry(r.Name, i + 1, s.Scenario, s.DrillName, RoutineLibrary.FocusName(s.Focus, Loc.Spanish),
                    s.Minutes, s.SensLabel, s.RecoilOn, s.Optional, p?.TimesPlayed ?? 0, p?.Completed ?? false,
                    p?.LastPlayed ?? default));
            }
        }

        var panel = new VPanel { K = k, Pad = 10, SizeFlagsVertical = SizeFlags.ExpandFill };
        var table = VBox(0);
        table.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.AddChild(table);
        table.AddChild(new RoutineDataHeader { K = k, CustomMinimumSize = new Vector2(0, 24 * k) });
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(new RoutineDataGrid
        {
            K = k, Rows = data, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, Math.Max(1, data.Count) * 30 * k),
        });
        table.AddChild(scroll);
        body.AddChild(panel);
        body.AddChild(Spacer(0, 4 * k));
        body.AddChild(Para(L(
            "Progress lives in routines.json and is written only on this PC. The CSV files are exports, not the live save; deleting them never loses progress.",
            "El progreso vive en routines.json y solo se escribe en este PC. Los CSV son exportaciones, no el guardado en vivo; borrarlos nunca pierde el progreso."),
            10.5f, UiTheme.Faint));
    }

    // ---------------- CREDITS ----------------

    void BuildCredits(VBoxContainer body, float k)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var v = VBox(10 * k);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(v);
        body.AddChild(scroll);

        v.AddChild(new MText
        {
            K = k, Text = L("WHO MADE THIS · SOURCES · LEGAL", "QUIÉN LO HIZO · FUENTES · LEGAL"),
            Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint,
        });
        v.AddChild(new MText
        {
            K = k, Text = L("Credits and permissions", "Créditos y permisos"), Font = UiTheme.Light, Px = 28, Color = UiTheme.Text,
        });
        v.AddChild(Para(L(
            "CA-VAL-AIM adapts public community material with attribution. The original authors keep every right over their names, documents, scenario lists and share codes; this project only adds its own adaptation to its own drills.",
            "CA-VAL-AIM adapta material público de la comunidad con atribución. Los autores originales conservan todos los derechos sobre sus nombres, documentos, listas de escenarios y códigos; este proyecto solo añade su propia adaptación a sus propios ejercicios."),
            11.5f, UiTheme.Dim));

        v.AddChild(new MRule());
        v.AddChild(CreditsBlock(L("VOLTAIC", "VOLTAIC"), k,
            L("VALORANT routines for KovaaK's and Aim Lab, the aim-training guide and the 126-term Aim Glossary. Built-in routines are adaptations: the original scenario names and share codes are kept for attribution. Owner and original creator: sini. Routine credits include bardOZ, Cartoon, Trustemix and Zeeq.",
                "Rutinas de VALORANT para KovaaK's y Aim Lab, la guía de entrenamiento y el glosario de 126 términos. Las rutinas internas son adaptaciones: los nombres de escenarios y códigos originales se conservan para atribución. Responsable y creador original: sini. En las rutinas participan bardOZ, Cartoon, Trustemix y Zeeq."),
            new[]
            {
                (L("OFFICIAL DISCORD", "DISCORD OFICIAL"), CommunityLinks.VoltaicDiscord),
                ("VOLTAIC.GG", CommunityLinks.VoltaicSite),
                ("AIM JOURNEY", CommunityLinks.AimJourney),
                (L("GLOSSARY", "GLOSARIO"), CommunityLinks.AimGlossary),
                (L("KOVAAK'S ROUTINES", "RUTINAS KOVAAK'S"), CommunityLinks.KvKsRoutines),
                ("AIM LAB ROUTINES", CommunityLinks.AimlabsRoutines),
                ("TWITTER / X", CommunityLinks.VoltaicTwitter),
                ("PATREON", CommunityLinks.VoltaicPatreon),
                ("SINI", CommunityLinks.VoltaicSini),
            }));

        v.AddChild(new MRule());
        v.AddChild(CreditsBlock("CARTOON", k,
            L("Author of the public aim-training guide and of the external Flicks and Micro-adjustment playlists. CA-VAL-AIM links the guide and copies/export only the public Kovaak's share codes; it does not bundle or redistribute those playlists.",
                "Autor de la guía pública de aim training y de las playlists externas de Flicks y Microajustes. CA-VAL-AIM enlaza la guía y solo copia/exporta los códigos públicos de Kovaak's; no incluye ni redistribuye esas playlists."),
            new[]
            {
                (L("THE GUIDE", "LA GUÍA"), CommunityLinks.CartoonGuide),
                ("TWITCH", CommunityLinks.CartoonTwitch),
                ("TWITTER / X", CommunityLinks.CartoonTwitter),
                ("YOUTUBE", CommunityLinks.CartoonYouTube),
                (L("RAMP WARM-UP", "RAMP WARM-UP"), CommunityLinks.Minigodwarmup),
            }));

        v.AddChild(new MRule());
        v.AddChild(CreditsBlock(L("OTHER CREDITS", "OTROS CRÉDITOS"), k,
            L("CA-VAL-AIM is based on the open-source ValTrainer project (GPL-3.0): the original notice is in NOTICE and the modified source is on GitHub. Engine: Godot (MIT). Models, textures, sounds and map assets have their own credits in CREDITS.md and Settings → About → Licenses. The RAMP warm-up is by MiniGods and is recommended by cartoon's guide.",
                "CA-VAL-AIM se basa en el proyecto open source ValTrainer (GPL-3.0): el aviso original está en NOTICE y el código modificado en GitHub. Motor: Godot (MIT). Modelos, texturas, sonidos y assets de mapas tienen sus créditos en CREDITS.md y en Ajustes → Acerca de → Licencias. El RAMP warm-up es de MiniGods y lo recomienda la guía de cartoon."),
            new[]
            {
                ("VALTRAINER (GPL-3.0)", AppInfo.RepoUrl),
                (L("MINIGODS", "MINIGODS"), CommunityLinks.Minigod),
            }));

        v.AddChild(new MRule());
        var legal = new VPanel { K = k, Title = L("LEGAL", "LEGAL"), Pad = 16 };
        var lv = VBox(5 * k);
        lv.AddChild(Para(L(
            "CA-VAL-AIM is a free, non-commercial fan project. It is not affiliated with, sponsored by or endorsed by Riot Games, Voltaic, Aim Lab, Kovaak's or cartoon.",
            "CA-VAL-AIM es un proyecto fan gratuito y no comercial. No está afiliado, patrocinado ni respaldado por Riot Games, Voltaic, Aim Lab, Kovaak's ni cartoon."), 11.5f, UiTheme.TextSoft));
        lv.AddChild(Para(L(
            "VALORANT is a trademark of Riot Games, Inc. Aim Lab and Kovaak's are trademarks of their respective owners; their software is not included. Voltaic, cartoon and the other creators are credited by name, and their documents, scenario lists and share codes remain their property.",
            "VALORANT es una marca de Riot Games, Inc. Aim Lab y Kovaak's son marcas de sus respectivos titulares; su software no está incluido. Voltaic, cartoon y el resto de creadores aparecen acreditados por su nombre, y sus documentos, listas de escenarios y códigos siguen siendo de su propiedad."), 11.5f, UiTheme.TextSoft));
        lv.AddChild(Para(L(
            "The adapted routine instructions, guide summaries and glossary definitions are original paraphrases written for this project and distributed under the GNU GPL v3, like the rest of CA-VAL-AIM. Optional Riot Games map assets are handled separately in CREDITS.md and are not covered by the GPL.",
            "Las instrucciones adaptadas, los resúmenes de la guía y las definiciones del glosario son paráfrasis originales escritas para este proyecto y distribuidas bajo GNU GPL v3, como el resto de CA-VAL-AIM. Los assets opcionales de mapas de Riot Games se tratan aparte en CREDITS.md y no están cubiertos por la GPL."), 11.5f, UiTheme.Dim));
        lv.AddChild(Para(L(
            "Data: routines.json, playlists.json and the CSV exports stay in your local data folder. The trainer does not upload them. The share-code buttons only copy text to your clipboard; the external trainers are not installed or modified.",
            "Datos: routines.json, playlists.json y los CSV se quedan en tu carpeta local. El entrenador no los sube. Los botones de códigos solo copian texto al portapapeles; los entrenadores externos no se instalan ni se modifican."), 11.5f, UiTheme.Faint));
        legal.AddChild(lv);
        v.AddChild(legal);
    }

    Control CreditsBlock(string title, float k, string text, (string Label, string Url)[] links)
    {
        var v = VBox(6 * k);
        v.AddChild(new MText
        {
            K = k, Text = title, Font = UiTheme.Light, Px = 22, Color = UiTheme.Text,
        });
        v.AddChild(Para(text, 11.5f, UiTheme.Dim));
        var grid = new GridContainer
        {
            Columns = GetViewportRect().Size.X / k >= 1250 ? 3 : 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        grid.AddThemeConstantOverride("h_separation", (int)(7 * k));
        grid.AddThemeConstantOverride("v_separation", (int)(7 * k));
        foreach (var (label, url) in links)
        {
            var b = new MAction
            {
                K = k, Label = label, Px = 11, CustomMinimumSize = new Vector2(0, 36 * k),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            string u = url;
            b.Pressed += () => Open(u);
            grid.AddChild(b);
        }
        v.AddChild(grid);
        return v;
    }
}

/// <summary>Column captions of the routine's step table.</summary>
public partial class RoutineStepHeader : Control
{
    public float K = 1f;
    public RoutineStepHeader() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        float k = K, pad = 10 * k, w = Size.X - pad * 2, cy = Size.Y / 2;
        int fs = UiTheme.Fs(10, k);
        string[] heads = Loc.Spanish
            ? new[] { "#", "ESCENARIO ORIGINAL", "AQUÍ", "TIEMPO", "SENS", "RETROCESO", "OPC." }
            : new[] { "#", "SOURCE SCENARIO", "PLAYED HERE", "TIME", "SENS", "RECOIL", "OPT." };
        float[] at = { 0f, 0.05f, 0.47f, 0.65f, 0.76f, 0.86f, 0.95f };
        for (int i = 0; i < heads.Length; i++)
        {
            float x = pad + at[i] * w;
            float cw = (i + 1 < at.Length ? at[i + 1] : 1f) * w - at[i] * w - 6 * k;
            Gfx.TextFit(this, UiTheme.HudWide, heads[i], x, Gfx.Mid(cy, fs), fs, UiTheme.Dim, cw);
        }
        DrawRect(new Rect2(0, Size.Y - 1, Size.X, 1), new Color(UiTheme.Text, 0.12f));
    }
}

/// <summary>One clickable step of a routine: index, original scenario, the drill that plays it here, time, sens, recoil and optional marker.</summary>
public partial class RoutineStepRow : BaseButton
{
    public readonly RoutineStep Step;
    public readonly int Index;
    public float K = 1f;
    public bool Selected;
    float hover;

    static readonly float[] At = { 0f, 0.05f, 0.47f, 0.65f, 0.76f, 0.86f, 0.95f };

    public RoutineStepRow(RoutineStep step, int index, float k)
    {
        Step = step;
        Index = index;
        K = k;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        FocusMode = FocusModeEnum.None;
        TooltipText = step.Scenario;
        MouseEntered += UiTheme.HoverSound;
        Pressed += UiTheme.ClickSound;
    }

    public void SetSelected(bool on)
    {
        if (Selected == on) return;
        Selected = on;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float target = IsHovered() ? 1f : 0f;
        if (hover == target) return;
        hover = Mathf.MoveToward(hover, target, (float)delta * 8f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K, pad = 10 * k, w = Size.X - pad * 2, cy = Size.Y / 2;
        if (Selected) DrawRect(new Rect2(Vector2.Zero, Size), new Color(UiTheme.Accent, 0.10f));
        else if (hover > 0.01f) DrawRect(new Rect2(Vector2.Zero, Size), new Color(1, 1, 1, 0.035f * hover));
        if (Selected) DrawRect(new Rect2(0, 0, Mathf.Max(1.5f, 2 * k), Size.Y), UiTheme.Accent);
        DrawRect(new Rect2(0, Size.Y - 1, Size.X, 1), UiTheme.Line);

        int fs = UiTheme.Fs(10, k), ss = UiTheme.Fs(11.5f, k);
        float X(int i) => pad + At[i] * w;
        float W(int i) => (i + 1 < At.Length ? At[i + 1] : 1f) * w - At[i] * w - 6 * k;

        Gfx.Text(this, UiTheme.HudWide, (Index + 1).ToString("00", CultureInfo.InvariantCulture), X(0), Gfx.Mid(cy, fs), fs, UiTheme.Faint);
        Gfx.TextFit(this, UiTheme.Body, Step.Scenario, X(1), Gfx.Mid(cy, ss), ss, UiTheme.Text, W(1));
        string drill = Step.DrillName.Length > 0 ? Step.DrillName : "—";
        Gfx.TextFit(this, UiTheme.Body, drill, X(2), Gfx.Mid(cy, fs), fs, Step.DrillName.Length > 0 ? UiTheme.Teal : UiTheme.Faint, W(2));
        Gfx.TextFit(this, UiTheme.Hud, Step.Minutes > 0 ? $"{Step.Minutes:0.#}m" : "—", X(3), Gfx.Mid(cy, fs), fs, UiTheme.TextSoft, W(3));
        Gfx.TextFit(this, UiTheme.Hud, Step.SensLabel, X(4), Gfx.Mid(cy, fs), fs, UiTheme.TextSoft, W(4));
        Gfx.TextFit(this, UiTheme.HudWide, Step.RecoilOn ? "ON" : "OFF", X(5), Gfx.Mid(cy, fs), fs,
            Step.RecoilOn ? UiTheme.Good : UiTheme.Faint, W(5));
        if (Step.Optional)
            Gfx.TextFit(this, UiTheme.HudWide, "OPT", X(6), Gfx.Mid(cy, fs), fs, UiTheme.Warn, W(6));
        else
            Gfx.Text(this, UiTheme.HudWide, "·", X(6), Gfx.Mid(cy, fs), fs, UiTheme.Faint);
    }
}

/// <summary>Virtual, searchable A–Z list: draws every row itself so 126 terms do not create 126 nodes.</summary>
public partial class GlossaryList : Control
{
    public float K = 1f;
    public float RowH = 38f;
    readonly List<GlossaryTerm> items = new();
    public int Selected;
    public Action<int>? Picked;

    public GlossaryList()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public void SetItems(IReadOnlyList<GlossaryTerm> list, int selected)
    {
        items.Clear();
        items.AddRange(list);
        Selected = selected;
        CustomMinimumSize = new Vector2(0, Math.Max(1, items.Count) * RowH * K + 2);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b) return;
        int i = Mathf.FloorToInt(b.Position.Y / (RowH * K));
        if (i < 0 || i >= items.Count) return;
        Selected = i;
        QueueRedraw();
        Picked?.Invoke(i);
        AcceptEvent();
    }

    public override void _Draw()
    {
        float k = K, h = RowH * k;
        string lastLetter = "";
        for (int i = 0; i < items.Count; i++)
        {
            var t = items[i];
            var r = new Rect2(0, i * h, Size.X, h);
            if (i == Selected) DrawRect(r, new Color(UiTheme.Accent, 0.10f));
            else if (i % 2 == 1) DrawRect(r, new Color(1, 1, 1, 0.018f));
            if (i == Selected) DrawRect(new Rect2(0, r.Position.Y, Mathf.Max(1.5f, 2 * k), h), UiTheme.Accent);
            float cy = r.Position.Y + h / 2;
            if (t.Letter != lastLetter)
            {
                Gfx.Text(this, UiTheme.HudWide, t.Letter, 10 * k, Gfx.Mid(cy, UiTheme.Fs(10, k)), UiTheme.Fs(10, k), UiTheme.Accent);
                lastLetter = t.Letter;
            }
            int fs = UiTheme.Fs(12.5f, k);
            Gfx.TextFit(this, UiTheme.Body, t.Term, 34 * k, Gfx.Mid(cy, fs), fs,
                i == Selected ? UiTheme.Text : UiTheme.TextSoft, Size.X - 46 * k);
            DrawRect(new Rect2(0, r.End.Y - 1, Size.X, 1), UiTheme.Line);
        }
    }
}

/// <summary>One flattened step row of the data tab. Progress is per routine and repeated on its rows, like a spreadsheet.</summary>
public sealed record RoutineDataEntry(
    string Routine, int Step, string Scenario, string Drill, string Focus, float Minutes, string Sens,
    bool Recoil, bool Optional, int Played, bool Completed, DateTime LastPlayed);

/// <summary>The shared Excel-style column grid of the DATA tab.</summary>
public static class RoutineDataLayout
{
    public static readonly float[] Cols = { 0f, 0.03f, 0.17f, 0.215f, 0.43f, 0.525f, 0.585f, 0.63f, 0.69f, 0.765f, 0.80f, 0.86f, 0.91f };
    public static string[] Heads => Loc.Spanish
        ? new[] { "Nº", "RUTINA", "PASO", "ESCENARIO", "EJERCICIO", "FOCO", "MIN", "SENS", "RETRO.", "OPC", "JUGADA", "HECHA", "ÚLTIMA" }
        : new[] { "#", "ROUTINE", "STEP", "SCENARIO", "DRILL", "FOCUS", "MIN", "SENS", "RECOIL", "OPT", "PLAYED", "DONE", "LAST" };
}

public partial class RoutineDataHeader : Control
{
    public float K = 1f;
    public RoutineDataHeader() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        float k = K, pad = 7 * k, w = Size.X - pad * 2, cy = Size.Y / 2;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1, 1, 1, 0.06f));
        var heads = RoutineDataLayout.Heads;
        var cols = RoutineDataLayout.Cols;
        int fs = UiTheme.Fs(9.5f, k);
        for (int i = 0; i < heads.Length; i++)
        {
            float x = pad + cols[i] * w;
            float cw = (i + 1 < cols.Length ? cols[i + 1] : 1f) * w - cols[i] * w - 3 * k;
            Gfx.TextFit(this, UiTheme.HudWide, heads[i], x + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.Dim, cw);
            DrawRect(new Rect2(x, 0, 1, Size.Y), new Color(1, 1, 1, 0.07f));
        }
        DrawRect(new Rect2(0, Size.Y - 1, Size.X, 1), new Color(1, 1, 1, 0.14f));
    }
}

/// <summary>Draws the whole DATA sheet itself: grid lines, alternating rows, progress per routine and a hovered row.</summary>
public partial class RoutineDataGrid : Control
{
    public float K = 1f;
    public List<RoutineDataEntry> Rows = new();
    int hot = -1;

    public RoutineDataGrid() => MouseFilter = MouseFilterEnum.Pass;

    public override void _Process(double delta)
    {
        int i = Mathf.FloorToInt(GetLocalMousePosition().Y / (30 * K));
        if (i == hot) return;
        hot = i;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K, pad = 7 * k, w = Size.X - pad * 2, rh = 30 * k;
        var cols = RoutineDataLayout.Cols;
        for (int i = 0; i < Rows.Count; i++)
        {
            var r = Rows[i];
            var rect = new Rect2(0, i * rh, Size.X, rh);
            if (i == hot) DrawRect(rect, new Color(1, 1, 1, 0.035f));
            else if (i % 2 == 1) DrawRect(rect, new Color(1, 1, 1, 0.018f));
            int fs = UiTheme.Fs(9.5f, k), ss = UiTheme.Fs(10, k);
            float cy = rect.Position.Y + rh / 2;
            float X(int c) => pad + cols[c] * w;
            float W(int c) => (c + 1 < cols.Length ? cols[c + 1] : 1f) * w - cols[c] * w - 3 * k;

            Gfx.Text(this, UiTheme.Hud, (i + 1).ToString("000", CultureInfo.InvariantCulture), X(0) + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.Faint);
            Gfx.TextFit(this, UiTheme.Body, r.Routine, X(1) + 2 * k, Gfx.Mid(cy, ss), ss, UiTheme.TextSoft, W(1));
            Gfx.Text(this, UiTheme.Hud, r.Step.ToString("00", CultureInfo.InvariantCulture), X(2) + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.Dim);
            Gfx.TextFit(this, UiTheme.Body, r.Scenario, X(3) + 2 * k, Gfx.Mid(cy, ss), ss, UiTheme.Text, W(3));
            Gfx.TextFit(this, UiTheme.Body, r.Drill.Length > 0 ? r.Drill : "—", X(4) + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.Teal, W(4));
            Gfx.TextFit(this, UiTheme.HudWide, r.Focus, X(5) + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.Faint, W(5));
            Gfx.Text(this, UiTheme.Hud, r.Minutes.ToString("0.#", CultureInfo.InvariantCulture), X(6) + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.TextSoft);
            Gfx.TextFit(this, UiTheme.Hud, r.Sens, X(7) + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.TextSoft, W(7));
            Gfx.Text(this, UiTheme.HudWide, r.Recoil ? "ON" : "OFF", X(8) + 2 * k, Gfx.Mid(cy, fs), fs, r.Recoil ? UiTheme.Good : UiTheme.Faint);
            Gfx.Text(this, UiTheme.HudWide, r.Optional ? "OPT" : "—", X(9) + 2 * k, Gfx.Mid(cy, fs), fs, r.Optional ? UiTheme.Warn : UiTheme.Faint);
            Gfx.Text(this, UiTheme.Hud, r.Played.ToString(CultureInfo.InvariantCulture), X(10) + 2 * k, Gfx.Mid(cy, fs), fs, r.Played > 0 ? UiTheme.Text : UiTheme.Faint);
            Gfx.Text(this, UiTheme.HudWide, r.Completed ? "✓" : "—", X(11) + 2 * k, Gfx.Mid(cy, fs), fs, r.Completed ? UiTheme.Good : UiTheme.Faint);
            string last = r.LastPlayed == default ? "—" : r.LastPlayed.ToLocalTime().ToString("dd/MM/yy", CultureInfo.InvariantCulture);
            Gfx.Text(this, UiTheme.Hud, last, X(12) + 2 * k, Gfx.Mid(cy, fs), fs, UiTheme.Faint);

            for (int c = 0; c < cols.Length; c++)
                DrawRect(new Rect2(X(c), rect.Position.Y, 1, rh), new Color(1, 1, 1, 0.045f));
            DrawRect(new Rect2(0, rect.End.Y - 1, Size.X, 1), new Color(1, 1, 1, 0.035f));
        }
    }
}
