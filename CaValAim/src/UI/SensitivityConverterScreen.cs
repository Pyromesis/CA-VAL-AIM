using System.Globalization;
using Godot;
using CaValAim.Core;

namespace CaValAim.UI;

/// <summary>
/// Physical hipfire sensitivity conversion between documented game scales. Rotation and osu!'s 2D travel are
/// deliberately incompatible. Nothing is applied to CA-VAL-AIM until the player presses APPLY.
/// </summary>
public partial class SensitivityConverterScreen : ScreenBase
{
    const string Custom = "custom";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] Ids = GameSensitivity.Presets.Select(p => p.Id).Append(Custom).ToArray();

    string sourceId = "valorant", targetId = "cs2";
    string sourceSensText = Main.I.Sens.ToString("0.###", Inv), dpiText = Main.I.Settings.Dpi.ToString(Inv);
    string sourceYawText = ".022", targetYawText = ".022";
    string sourceSiegeText = ".02", targetSiegeText = ".02", widthText = "1920";

    OptionButton? sourcePicker, targetPicker;
    LineEdit? sourceSensEdit, dpiEdit, widthEdit;
    LineEdit? sourceYawEdit, targetYawEdit, sourceSiegeEdit, targetSiegeEdit;
    Control? sourceCustomRow, targetCustomRow, sourceSiegeRow, targetSiegeRow, widthRow;
    Label? targetValue, physicalValue, resultDetail, statusText, applyValue, sourceNote, targetNote, rawNote;
    VButton? swapButton, copyButton, applyButton, sourceLink, targetLink;

    bool valid, syncing;
    double convertedSensitivity, physicalCm;
    int convertedDpi;
    SensitivityMeasure convertedMeasure;
    string clipboardText = "";

    static string L(string es, string en) => Loc.Spanish ? es : en;
    static string Num(double value) => value.ToString("0.########", Inv);
    static string Cm(double value) => Loc.Numeric(value.ToString("0.000", Inv));

    public override void _Ready()
    {
        base._Ready();
        if (Main.I.Dev && CmdLine.Has("--converter-check")) Callable.From(RunDevCheck).CallDeferred();
        else Callable.From(() => { if (IsInstanceValid(sourcePicker)) sourcePicker.GrabFocus(); }).CallDeferred();
    }

    protected override void Build()
    {
        float k = K;
        var root = Margins(VBox(14 * k), 44 * k, 24 * k, 44 * k, 24 * k);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        var col = (VBoxContainer)root.GetChild(0);

        var head = HBox(16 * k);
        var title = Heading("SENS CONVERTER", L(
            "CM/360 PARA FPS · CM POR PANTALLA PARA OSU! · SIN CAMBIAR OTROS JUEGOS",
            "CM/360 FOR FPS · CM PER SCREEN FOR OSU! · NO OTHER GAME IS CHANGED"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var back = Btn("BACK", VButton.Look.Primary, Main.I.ShowMenu, 140, 46, 18);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(back);
        col.AddChild(head);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddChild(scroll);
        var body = VBox(14 * k);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(body);

        var games = HBox(14 * k);
        games.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(games);
        games.AddChild(BuildGamePanel(source: true));
        var swapCol = VBox(8 * k);
        swapCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        swapButton = Btn("⇄", VButton.Look.Secondary, Swap, 68, 52, 27);
        swapButton.TooltipText = L("Invierte la conversión conservando la distancia física", "Reverse the conversion while preserving physical distance");
        swapCol.AddChild(swapButton);
        games.AddChild(swapCol);
        games.AddChild(BuildGamePanel(source: false));

        var inputPanel = new VPanel { K = k, Title = L("ENTRADA Y PANTALLA", "INPUT AND DISPLAY") };
        body.AddChild(inputPanel);
        var input = new HFlowContainer();
        input.AddThemeConstantOverride("h_separation", (int)(20 * k));
        input.AddThemeConstantOverride("v_separation", (int)(8 * k));
        inputPanel.AddChild(input);
        dpiEdit = Editor(dpiText, v => dpiText = v, L("DPI reales del ratón, no eDPI", "The mouse's actual DPI, not eDPI"), 150);
        input.AddChild(Field("DPI", dpiEdit));
        widthEdit = Editor(widthText, v => widthText = v, L(
            "Anchura de la resolución de osu! con raw input", "osu! resolution width with raw input"), 190);
        widthRow = Field(L("ANCHURA OSU! (PX)", "OSU! WIDTH (PX)"), widthEdit);
        input.AddChild(widthRow);
        rawNote = Note(L("osu!: raw input obligatorio. La distancia es por anchura de pantalla; no existe una conversión física universal entre desplazamiento 2D y rotación.",
            "osu!: raw input is required. Distance is per screen width; there is no universal physical conversion between 2D travel and rotation."), 15);
        rawNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rawNote.CustomMinimumSize = new Vector2(520 * k, 0);
        input.AddChild(rawNote);

        var result = new VPanel { K = k, Title = L("RESULTADO FÍSICO", "PHYSICAL RESULT") };
        body.AddChild(result);
        var resultBody = VBox(9 * k);
        result.AddChild(resultBody);
        physicalValue = Lbl("—", UiTheme.Display, 31, UiTheme.Text);
        resultBody.AddChild(physicalValue);
        resultDetail = Note("", 14);
        resultBody.AddChild(resultDetail);
        statusText = Note("", 15);
        resultBody.AddChild(statusText);
        applyValue = Note("", 14);
        resultBody.AddChild(applyValue);
        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", (int)(12 * k));
        actions.AddThemeConstantOverride("v_separation", (int)(8 * k));
        resultBody.AddChild(actions);
        copyButton = Btn(L("COPIAR RESULTADO", "COPY RESULT"), VButton.Look.Secondary, Copy, 220, 44, 16);
        applyButton = Btn(L("APLICAR A CA-VAL-AIM", "APPLY TO CA-VAL-AIM"), VButton.Look.Primary, Apply, 280, 44, 16);
        actions.AddChild(copyButton);
        actions.AddChild(applyButton);

        var help = new VPanel { K = k, Title = L("QUÉ SE CONVIERTE", "WHAT IS CONVERTED") };
        body.AddChild(help);
        var helpBody = VBox(8 * k);
        help.AddChild(helpBody);
        helpBody.AddChild(Note(L(
            "Hipfire lineal: cm/360 = 914.4 / (DPI × sens × yaw). Entre juegos: sens destino = sens origen × yaw origen / yaw destino. PUBG y Minecraft usan sus escalas no lineales documentadas.",
            "Linear hipfire: cm/360 = 914.4 / (DPI × sens × yaw). Across games: target sens = source sens × source yaw / target yaw. PUBG and Minecraft use their documented nonlinear scales."), 15));
        helpBody.AddChild(Note(L(
            "FOV no entra en el hipfire lineal; PUBG es la excepción documentada y este preset fija TPP FOV 80. ADS, zoom, targeting y monitor distance no se convierten aquí. APLICAR solo cambia la sensibilidad CUSTOM y el DPI declarado en CA-VAL-AIM; nunca escribe la configuración de VALORANT ni de otros juegos.",
            "FOV is not part of linear hipfire; PUBG is the documented exception and this preset fixes TPP FOV 80. ADS, zoom, targeting and monitor distance are not converted here. APPLY only changes CUSTOM sensitivity and declared DPI in CA-VAL-AIM; it never writes VALORANT or any other game's configuration."), 15));

        RefreshConditional();
        Recalculate();
    }

    Control BuildGamePanel(bool source)
    {
        float k = K;
        var panel = new VPanel
        {
            K = k,
            Title = source ? L("1 · JUEGO DE ORIGEN", "1 · SOURCE GAME") : L("2 · JUEGO DE DESTINO", "2 · TARGET GAME"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var content = VBox(9 * k);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(content);

        var picker = new OptionButton { CustomMinimumSize = new Vector2(0, 42 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string id in Ids) picker.AddItem(id == Custom ? L("Otro juego (yaw personalizado)", "Other game (custom yaw)") : GameSensitivity.Find(id)!.Name);
        picker.Selected = Array.IndexOf(Ids, source ? sourceId : targetId);
        picker.ItemSelected += index => GameChanged(source, index);
        content.AddChild(picker);
        if (source) sourcePicker = picker; else targetPicker = picker;

        if (source)
        {
            sourceSensEdit = Editor(sourceSensText, v => sourceSensText = v, L(
                "Sensibilidad exactamente como la muestra el juego de origen", "Sensitivity exactly as displayed by the source game"));
            content.AddChild(Field(L("SENSIBILIDAD DE ORIGEN", "SOURCE SENSITIVITY"), sourceSensEdit));
        }
        else
        {
            targetValue = Lbl("—", UiTheme.Display, 36, UiTheme.Accent);
            targetValue.CustomMinimumSize = new Vector2(0, 54 * k);
            targetValue.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            targetValue.ClipText = true;
            content.AddChild(Field(L("SENSIBILIDAD CONVERTIDA", "CONVERTED SENSITIVITY"), targetValue));
        }

        var yaw = Editor(source ? sourceYawText : targetYawText,
            v => { if (source) sourceYawText = v; else targetYawText = v; },
            L("Grados por count por unidad de sensibilidad, solo hipfire lineal", "Degrees per count per sensitivity unit, linear hipfire only"));
        var yawRow = Field(L("YAW PERSONALIZADO (°/COUNT)", "CUSTOM YAW (°/COUNT)"), yaw);
        content.AddChild(yawRow);
        if (source) { sourceYawEdit = yaw; sourceCustomRow = yawRow; } else { targetYawEdit = yaw; targetCustomRow = yawRow; }

        var siege = Editor(source ? sourceSiegeText : targetSiegeText,
            v => { if (source) sourceSiegeText = v; else targetSiegeText = v; },
            "MouseSensitivityMultiplierUnit · " + L("predeterminado 0.02", "default 0.02"));
        var siegeRow = Field(L("MULTIPLICADOR INI DE SIEGE", "SIEGE INI MULTIPLIER"), siege);
        content.AddChild(siegeRow);
        if (source) { sourceSiegeEdit = siege; sourceSiegeRow = siegeRow; } else { targetSiegeEdit = siege; targetSiegeRow = siegeRow; }

        var note = Note(Describe(GameSensitivity.Find(source ? sourceId : targetId)), 14);
        content.AddChild(note);
        var link = Btn(L("ABRIR FUENTE", "OPEN SOURCE"), VButton.Look.Ghost,
            () => OpenSource(source ? sourceId : targetId), 170, 36, 13);
        content.AddChild(link);
        if (source) { sourceNote = note; sourceLink = link; } else { targetNote = note; targetLink = link; }
        return panel;
    }

    VBoxContainer Field(string caption, Control editor)
    {
        var field = VBox(4 * K);
        field.AddChild(Lbl(caption, UiTheme.HudWide, 11, UiTheme.Dim));
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(editor);
        return field;
    }

    LineEdit Editor(string value, Action<string> changed, string tip, float width = 0)
    {
        var edit = new LineEdit
        {
            Text = value,
            TooltipText = tip,
            CustomMinimumSize = new Vector2(width * K, 40 * K),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        edit.TextChanged += text => { changed(text); if (!syncing) Recalculate(); };
        return edit;
    }

    Label Note(string text, float px)
    {
        var label = Lbl(text, UiTheme.Body, px, UiTheme.Dim);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    void GameChanged(bool source, long index)
    {
        if (syncing || index < 0 || index >= Ids.Length) return;
        string id = Ids[(int)index];
        if (source)
        {
            sourceId = id;
            if (GameSensitivity.Find(id) is { } preset && sourceSensEdit != null)
                sourceSensEdit.Text = Num(preset.DefaultSensitivity);
        }
        else targetId = id;
        RefreshConditional();
        Recalculate();
    }

    void RefreshConditional()
    {
        if (sourceCustomRow == null) return;
        sourceCustomRow.Visible = sourceId == Custom;
        targetCustomRow!.Visible = targetId == Custom;
        sourceSiegeRow!.Visible = sourceId == "r6";
        targetSiegeRow!.Visible = targetId == "r6";
        bool osu = sourceId == "osu" || targetId == "osu";
        widthRow!.Visible = osu;
        rawNote!.Visible = osu;
        if (sourceNote != null) sourceNote.Text = Describe(GameSensitivity.Find(sourceId));
        if (targetNote != null) targetNote.Text = Describe(GameSensitivity.Find(targetId));
        if (sourceLink != null) sourceLink.Disabled = sourceId == Custom;
        if (targetLink != null) targetLink.Disabled = targetId == Custom;
    }

    static GamePreset? MakePreset(string id, string yawText)
    {
        if (GameSensitivity.Find(id) is { } known) return known;
        if (id != Custom || !GameSensitivity.TryParsePositive(yawText, out double yaw)) return null;
        return new GamePreset(Custom, "Custom yaw", yaw, SensitivityMeasure.Rotation, 1, "", "Linear hipfire");
    }

    static bool SiegeFactor(string id, string text, out double factor)
    {
        factor = 1;
        if (id != "r6") return true;
        if (!GameSensitivity.TryParsePositive(text, out double ini)) return false;
        factor = ini / .02;
        return double.IsFinite(factor) && factor > 0;
    }

    static bool TryInteger(string text, int min, int max, out int value)
    {
        value = 0;
        if (!GameSensitivity.TryParsePositive(text, out double parsed)) return false;
        value = (int)Math.Round(parsed);
        return value >= min && value <= max && Math.Abs(parsed - value) < .0001;
    }

    void Recalculate()
    {
        if (targetValue == null) return;
        // Programmatic Text assignments (preset defaults, swap, dev checks) do not emit TextChanged in Godot.
        // Read the controls at the calculation boundary so displayed input and stored state cannot diverge.
        sourceSensText = sourceSensEdit!.Text;
        dpiText = dpiEdit!.Text;
        widthText = widthEdit!.Text;
        sourceYawText = sourceYawEdit!.Text;
        targetYawText = targetYawEdit!.Text;
        sourceSiegeText = sourceSiegeEdit!.Text;
        targetSiegeText = targetSiegeEdit!.Text;
        valid = false;
        clipboardText = "";
        targetValue.Text = "—";
        physicalValue!.Text = "—";
        resultDetail!.Text = "";
        applyValue!.Text = "";
        copyButton!.Disabled = applyButton!.Disabled = swapButton!.Disabled = true;
        copyButton.Label = L("COPIAR RESULTADO", "COPY RESULT");
        applyButton.Label = L("APLICAR A CA-VAL-AIM", "APPLY TO CA-VAL-AIM");
        statusText!.LabelSettings.FontColor = UiTheme.Dim;

        void Fail(string message) => statusText.Text = message;
        var source = MakePreset(sourceId, sourceYawText);
        var target = MakePreset(targetId, targetYawText);
        if (source == null || target == null)
        {
            Fail(L("El yaw personalizado debe ser un número positivo.", "Custom yaw must be a positive number."));
            return;
        }
        if (source.Measure != target.Measure)
        {
            Fail(L("No se puede equiparar una rotación FPS con desplazamiento 2D de osu!. Elige dos juegos FPS o usa osu! en ambos lados.",
                "An FPS rotation cannot be equated with osu!'s 2D travel. Choose two FPS games or use osu! on both sides."));
            return;
        }
        if (!GameSensitivity.TryParseSensitivity(source, sourceSensText, out double sourceSens))
        {
            Fail(source.Model == NonlinearModel.Minecraft
                ? L("Minecraft usa un valor interno entre 0 y 1 (100% = 0.5).", "Minecraft uses an internal value between 0 and 1 (100% = 0.5).")
                : L("Introduce una sensibilidad de origen positiva.", "Enter a positive source sensitivity."));
            return;
        }
        if (!TryInteger(dpiText, 1, 38400, out int dpi))
        {
            Fail(L("DPI debe ser un número entero entre 1 y 38400.", "DPI must be a whole number between 1 and 38400."));
            return;
        }
        if (!SiegeFactor(sourceId, sourceSiegeText, out double sourceFactor)
            || !SiegeFactor(targetId, targetSiegeText, out double targetFactor))
        {
            Fail(L("El multiplicador INI de Siege debe ser positivo (predeterminado 0.02).", "Siege's INI multiplier must be positive (default 0.02)."));
            return;
        }
        int width = 1920;
        if (source.Measure == SensitivityMeasure.ScreenWidth && !TryInteger(widthText, 16, 16384, out width))
        {
            Fail(L("La anchura de pantalla debe ser un número entero en píxeles.", "Screen width must be a whole number of pixels."));
            return;
        }
        if (!GameSensitivity.TryConvertPreset(sourceSens, source, target, dpi, width, out double converted, out double cm,
                sourceFactor, targetFactor))
        {
            Fail(L("No hay un resultado físico válido para estos valores.", "These values do not produce a valid physical result."));
            return;
        }

        valid = true;
        convertedSensitivity = converted;
        physicalCm = cm;
        convertedDpi = dpi;
        convertedMeasure = target.Measure;
        targetValue.Text = Num(converted);
        physicalValue.Text = target.Measure == SensitivityMeasure.Rotation
            ? $"{Cm(cm)} cm/360"
            : $"{Cm(cm)} cm / {L("anchura de pantalla", "screen width")}";
        resultDetail.Text = target.Measure == SensitivityMeasure.Rotation
            ? $"{L("HIPFIRE", "HIPFIRE")} · {dpi} DPI · {source.Name} → {target.Name}"
            : $"RAW INPUT · {width} px · {dpi} DPI · {source.Name} → {target.Name}";
        statusText.Text = L("Conversión calculada. Revisa el valor antes de copiarlo o aplicarlo.", "Conversion calculated. Review the value before copying or applying it.");
        clipboardText = target.Measure == SensitivityMeasure.Rotation
            ? $"{source.Name} {Num(sourceSens)} → {target.Name} {Num(converted)} | {dpi} DPI | {cm.ToString("0.0000", Inv)} cm/360 | hipfire"
            : $"{source.Name} {Num(sourceSens)} → {target.Name} {Num(converted)} | {dpi} DPI | {width} px | {cm.ToString("0.0000", Inv)} cm/screen width | raw input";
        copyButton.Disabled = swapButton.Disabled = false;
        if (target.Measure == SensitivityMeasure.Rotation && GameSensitivity.TryValorantOverride(cm, dpi, out float valorant))
        {
            applyButton.Disabled = false;
            applyValue.Text = L(
                $"Se aplicará CUSTOM {valorant.ToString("0.000", Inv)} a {dpi} DPI en CA-VAL-AIM (equivalente VALORANT).",
                $"Will apply CUSTOM {valorant.ToString("0.000", Inv)} at {dpi} DPI in CA-VAL-AIM (VALORANT equivalent).");
        }
        else applyValue.Text = target.Measure == SensitivityMeasure.ScreenWidth
            ? L("osu! es 2D: no se aplica como rotación a CA-VAL-AIM.", "osu! is 2D: it is not applied as a rotation to CA-VAL-AIM.")
            : L("La sensibilidad equivalente queda fuera del rango 0.001–10 de CA-VAL-AIM.", "The equivalent sensitivity is outside CA-VAL-AIM's 0.001–10 range.");
    }

    void Swap()
    {
        if (!valid || sourcePicker == null || targetPicker == null || sourceSensEdit == null) return;
        (sourceId, targetId) = (targetId, sourceId);
        (sourceYawText, targetYawText) = (targetYawText, sourceYawText);
        (sourceSiegeText, targetSiegeText) = (targetSiegeText, sourceSiegeText);
        sourceSensText = Num(convertedSensitivity);
        syncing = true;
        sourcePicker.Selected = Array.IndexOf(Ids, sourceId);
        targetPicker.Selected = Array.IndexOf(Ids, targetId);
        sourceSensEdit.Text = sourceSensText;
        sourceYawEdit!.Text = sourceYawText;
        targetYawEdit!.Text = targetYawText;
        sourceSiegeEdit!.Text = sourceSiegeText;
        targetSiegeEdit!.Text = targetSiegeText;
        syncing = false;
        RefreshConditional();
        Recalculate();
    }

    void Copy()
    {
        if (!valid || clipboardText.Length == 0 || copyButton == null) return;
        DisplayServer.ClipboardSet(clipboardText);
        copyButton.Label = L("COPIADO ✓", "COPIED ✓");
        copyButton.QueueRedraw();
    }

    void Apply()
    {
        if (!valid || convertedMeasure != SensitivityMeasure.Rotation || applyButton == null) return;
        if (!GameSensitivity.TryValorantOverride(physicalCm, convertedDpi, out float valorant)) return;
        var settings = Main.I.Settings;
        settings.UseSensOverride = true;
        settings.SensOverride = valorant;
        settings.Dpi = convertedDpi;
        settings.Save();
        applyButton.Label = L("APLICADO ✓", "APPLIED ✓");
        applyButton.QueueRedraw();
        applyValue!.Text = L(
            $"Aplicado en CA-VAL-AIM: CUSTOM {valorant:0.000} · {convertedDpi} DPI · {Cm(GameSensitivity.Cm360Preset(GameSensitivity.Find("valorant")!, valorant, convertedDpi))} cm/360 reales tras redondear.",
            $"Applied in CA-VAL-AIM: CUSTOM {valorant:0.000} · {convertedDpi} DPI · {Cm(GameSensitivity.Cm360Preset(GameSensitivity.Find("valorant")!, valorant, convertedDpi))} actual cm/360 after rounding.");
        statusText!.Text = L("No se ha cambiado ningún archivo de VALORANT ni de otros juegos.", "No VALORANT or other game file has been changed.");
        UiTheme.ConfirmSound();
    }

    static string Describe(GamePreset? preset)
    {
        if (preset == null) return L("Hipfire lineal. Debes verificar el yaw de tu juego/versión; ADS no está incluido.",
            "Linear hipfire. Verify the yaw for your game/version; ADS is not included.");
        return preset.Id switch
        {
            "valorant" => L("Hipfire · 0.07°/count. ADS y zoom son ajustes separados.", "Hipfire · 0.07°/count. ADS and zoom are separate settings."),
            "cod" => L("Hipfire · convención KovaaK 0.0066°/count para entregas modernas. ADS/monitor distance no incluidos.", "Hipfire · KovaaK convention 0.0066°/count for modern titles. ADS/monitor distance not included."),
            "fortnite" => L("Usa el porcentaje X mostrado (8 = 8%). Targeting y scope son ajustes separados.", "Use the displayed X percentage (8 = 8%). Targeting and scope are separate settings."),
            "r6" => L("Hipfire · 0.018/π °/count con INI 0.02. El sistema ADS Y5S3+ no está incluido.", "Hipfire · 0.018/π °/count with INI 0.02. The Y5S3+ ADS system is not included."),
            "cs2" or "quake" => L("Hipfire · m_yaw predeterminado 0.022. Desactiva aceleración; zoom/ADS no incluidos.", "Hipfire · default m_yaw 0.022. Disable acceleration; zoom/ADS not included."),
            "apex" => L("Hipfire · convención Source/KovaaK 0.022. Cada óptica requiere su propio ajuste.", "Hipfire · Source/KovaaK convention 0.022. Each optic needs its own setting."),
            "overwatch" or "destiny2" => L("Hipfire · convención KovaaK 0.0066°/count. Zoom/ADS por héroe o arma no incluidos.", "Hipfire · KovaaK convention 0.0066°/count. Per-hero/weapon zoom/ADS not included."),
            "rust" => L("Convención comunitaria no oficial 0.1125. Verifica midiendo tu versión instalada.", "Unofficial community convention 0.1125. Verify by measuring your installed version."),
            "pubg" => L("Escala General exponencial, TPP FOV 80. ADS y otros FOV no incluidos.", "Exponential General scale, TPP FOV 80. ADS and other FOV values not included."),
            "minecraft" => L("Escala cúbica vanilla; introduce el valor interno 0–1 (100% = 0.5).", "Vanilla cubic scale; enter the internal 0–1 value (100% = 0.5)."),
            "osu" => L("Raw input · píxeles/count. El resultado es cm por anchura de pantalla, nunca cm/360.", "Raw input · pixels/count. The result is cm per screen width, never cm/360."),
            _ => preset.Note,
        };
    }

    static void OpenSource(string id)
    {
        string? url = GameSensitivity.Find(id)?.SourceUrl;
        if (url == null || !url.StartsWith("https://", StringComparison.Ordinal)) return;
        try { OS.ShellOpen(url); }
        catch (Exception e) { Log.Error($"Couldn't open sensitivity source: {e.Message}"); }
    }

    /// <summary>Dev-only end-to-end check of the real controls, including swap, incompatible measures and APPLY.</summary>
    void RunDevCheck()
    {
        int passed = 0;
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            if (condition) { passed++; GD.Print($"[converter-check] PASS {name}"); }
            else { failures.Add(name); GD.PrintErr($"[converter-check] FAIL {name}"); }
        }
        void Game(bool source, string id)
        {
            int index = Array.IndexOf(Ids, id);
            if (source) sourcePicker!.Selected = index; else targetPicker!.Selected = index;
            GameChanged(source, index);
        }
        void Text(LineEdit edit, string value) { edit.Text = value; Recalculate(); }
        static bool Near(double a, double b) => Math.Abs(a - b) <= .00001;

        Check(valid, "initial conversion is valid");
        Game(true, "cs2"); Game(false, "overwatch"); Text(sourceSensEdit!, "2"); Text(dpiEdit!, "800");
        Check(valid && Near(convertedSensitivity, 20.0 / 3) && Near(physicalCm, 25.9772727273), "CS2 converts to Overwatch");
        Swap();
        Check(valid && sourceId == "overwatch" && targetId == "cs2" && Near(convertedSensitivity, 2), "swap reverses the result");
        Game(true, "osu");
        Check(!valid && copyButton!.Disabled && applyButton!.Disabled, "rotation to osu is rejected");
        Game(false, "osu");
        Check(valid && convertedMeasure == SensitivityMeasure.ScreenWidth && Near(physicalCm, 6.096), "osu reports cm per screen width");
        Game(true, Custom); Game(false, Custom); Text(sourceYawEdit!, ".022"); Text(targetYawEdit!, ".044"); Text(sourceSensEdit!, "2");
        Check(valid && Near(convertedSensitivity, 1), "custom yaw converts on both sides");
        Game(true, "minecraft"); Game(false, "valorant"); Text(sourceSensEdit!, "0");
        Check(valid && Near(physicalCm, 119.0625), "Minecraft zero percent converts from the real input");
        Game(false, "minecraft");
        Check(valid && Near(convertedSensitivity, 0), "Minecraft zero percent is a valid target");
        Text(sourceSensEdit!, "1");
        Check(valid && Near(convertedSensitivity, 1) && Near(physicalCm, 1.8603515625), "Minecraft maximum survives a round trip");
        Text(sourceSensEdit!, "1.2");
        Check(!valid && copyButton!.Disabled && applyButton!.Disabled, "Minecraft rejects input above one");
        Game(true, "r6"); Game(false, "valorant"); Text(sourceSensEdit!, "8"); Text(sourceSiegeEdit!, ".01");
        Check(valid && Near(physicalCm, 49.87278338), "Siege INI multiplier affects physical distance");
        Game(true, "cs2"); Game(false, "overwatch"); Text(sourceSensEdit!, "2"); Text(dpiEdit!, "800"); Apply();
        Check(Main.I.Settings.UseSensOverride && Main.I.Settings.Dpi == 800 && Near(Main.I.Settings.SensOverride, .629),
            "apply updates the trainer's rounded CUSTOM settings in memory");
        if (Main.I.SavesData)
        {
            string settingsPath = Path.Combine(Paths.DataDir, "settings.json");
            var saved = AppSettings.Load();
            Check(File.Exists(settingsPath) && saved.UseSensOverride && saved.Dpi == 800 && Near(saved.SensOverride, .629),
                "apply persists the trainer's CUSTOM settings and reloads them");
        }
        Text(sourceSensEdit!, "0");
        Check(!valid && copyButton!.Disabled && applyButton!.Disabled, "invalid input disables result actions");

        GD.Print($"[converter-check] RESULT: {(failures.Count == 0 ? "PASS" : "FAIL")} ({passed} passed, {failures.Count} failed)");
        GetTree().Quit(failures.Count == 0 ? 0 : 1);
    }
}
