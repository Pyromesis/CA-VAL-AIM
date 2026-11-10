using Godot;
using CaValAim.Core;
using CaValAim.Game;
using CaValAim.UI;

namespace CaValAim.Modes;

public enum ReactionGameKind { Flash, Audio, Peripheral, Inhibition, Parity, Memory, Rules, AimPop, AimSearch, AimMoving }

/// <summary>Ten different reaction games, each with five authored difficulty settings.</summary>
public sealed class ReactionGameMode : TrainingMode
{
    public readonly ReactionGameKind Kind;
    public readonly int Level;
    public bool NeedsAim => Kind >= ReactionGameKind.AimPop;
    public static IEnumerable<Func<TrainingMode>> Factories() =>
        Enum.GetValues<ReactionGameKind>().SelectMany(kind => Enumerable.Range(0, 5)
            .Select<int, Func<TrainingMode>>(level => () => new ReactionGameMode(kind, level)));
    public static ReactionGameMode? Parse(string arg)
    {
        var parts = arg.Split('-');
        return parts.Length == 2 && Enum.TryParse<ReactionGameKind>(parts[0], true, out var kind)
            && Enum.IsDefined(kind) && int.TryParse(parts[1], out int level) && level is >= 0 and < 5
            ? new(kind, level) : null;
    }
    public ReactionGameMode(ReactionGameKind kind, int level) { Kind = kind; Level = Math.Clamp(level, 0, 4); }
    public override string Key => $"reactgame:{Kind.ToString().ToLowerInvariant()}-{Level}";
    static string L(string es, string en) => Loc.Spanish ? es : en;
    public override string Name => (Kind switch
    {
        ReactionGameKind.Flash => L("Destello veloz", "Flash response"),
        ReactionGameKind.Audio => L("Pulso sonoro", "Sound pulse"),
        ReactionGameKind.Peripheral => L("Vigía periférico", "Peripheral watch"),
        ReactionGameKind.Inhibition => L("Semáforo de formas", "Shape stoplight"),
        ReactionGameKind.Parity => L("Números rápidos", "Quick numbers"),
        ReactionGameKind.Memory => L("Eco de memoria", "Memory echo"),
        ReactionGameKind.Rules => L("Cambio de regla", "Rule switch"),
        ReactionGameKind.AimPop => L("Aparición y disparo", "Pop and click"),
        ReactionGameKind.AimSearch => L("Encuentra el blanco", "Find the target"),
        _ => L("Intercepción móvil", "Moving interception"),
    }) + L($" · nivel {Level + 1}", $" · level {Level + 1}");
    public override string Description => Kind switch
    {
        ReactionGameKind.Flash => L("Pulsa disparo cuando se ilumine el panel; no te anticipes.", "Press fire when the panel lights up; do not anticipate."),
        ReactionGameKind.Audio => L("Pulsa disparo al oír el pulso. La pantalla no da la señal.", "Press fire on the sound pulse. There is no visual signal."),
        ReactionGameKind.Peripheral => L("Mira el centro y pulsa disparo cuando aparezca el punto en el borde. No necesitas apuntar.", "Watch the center and press fire on the edge dot. No aiming needed."),
        ReactionGameKind.Inhibition => L("Pulsa disparo ante un rombo; espera ante un círculo. La forma manda.", "Press fire for a diamond; wait for a circle. Follow the shape."),
        ReactionGameKind.Parity => L("Pulsa disparo si el número es par; espera si es impar.", "Press fire for an even number; wait for an odd number."),
        ReactionGameKind.Memory => L("Memoriza el número de preparación. Pulsa disparo solo si el nuevo número coincide.", "Remember the preparation number. Press fire only if the new number matches."),
        ReactionGameKind.Rules => L("Lee la regla de cada intento: PAR o ROMBO. Responde solo si se cumple.", "Read each trial rule: EVEN or DIAMOND. Respond only when it matches."),
        ReactionGameKind.AimPop => L("Mueve la mira al círculo y pulsa disparo. Separa reacción y tiempo hasta acertar.", "Move the crosshair to the circle and press fire. Onset and hit time are separate."),
        ReactionGameKind.AimSearch => L("Encuentra el círculo con punto central entre los distractores, apunta y pulsa disparo.", "Find the center-dotted circle among distractors, aim and press fire."),
        _ => L("Espera la aparición, intercepta el círculo móvil y pulsa disparo.", "Wait for the moving circle, intercept it and press fire."),
    };
    public override string Category => NeedsAim ? "Reflex" : "ReactionGames";
    public override bool Is2D => true;
    public override bool HasPreview => false;
    public override bool PreviewDemo => false;
    public override WeaponKind Weapon => WeaponKind.None;
    public override bool Timed => false;
    public override bool Done => completed >= 16;
    public override int Badge() => -1; // these games have no calibrated rank norms
    enum Phase { Wait, Signal, Feedback }
    Phase phase;
    float elapsed, wait, yaw0, pitch0;
    ulong stimulus, seen;
    bool drawn, go, diamond;
    int completed, early, wrong, missed, held, value, memory;
    Vector2 offset;
    string feedback = "";
    readonly List<float> response = new(), onset = new();
    GameSession? Session => G as GameSession;
    bool Auto => Main.I.Dev && CmdLine.Has("--reaction-games-autoplay");
    float Deadline => (Kind is ReactionGameKind.Memory or ReactionGameKind.Rules ? 2.4f : 1.8f) - Level * 0.18f;
    float Radius => 2.1f - Level * 0.28f;
    public static float ReachablePitchOffset(float pitch, float offset) => Mathf.Clamp(pitch + offset, -65, 65) - pitch;
    public static bool ShouldGo(ReactionGameKind kind, int number, int previous, bool isDiamond, int trial) => kind switch
    {
        ReactionGameKind.Inhibition => isDiamond,
        ReactionGameKind.Parity => number % 2 == 0,
        ReactionGameKind.Memory => number == previous,
        ReactionGameKind.Rules => trial % 2 == 0 ? number % 2 == 0 : isDiamond,
        _ => true,
    };
    protected override void Setup() => Next();
    public void ResumeTrial() { if (phase != Phase.Feedback) { Event("reaction_game_interrupted"); Next(); } }
    void Next()
    {
        phase = Phase.Wait; elapsed = 0; drawn = false;
        wait = Auto ? 0.25f : R(0.9f, 2.8f + Level * 0.25f);
        seen = Session?.LastFirePressUsec ?? 0;
        memory = Rng.Next(1, 5 + Level);
        value = Rng.NextDouble() < 0.5 ? memory : Rng.Next(1, 5 + Level);
        diamond = Rng.NextDouble() < 0.5;
        go = ShouldGo(Kind, value, memory, diamond, completed);
        float a = R(0, Mathf.Tau), d = R(7, 14 + Level * 3);
        offset = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.6f);
    }
    Vector2 TargetOffset => offset + (Kind == ReactionGameKind.AimMoving ? new Vector2(Mathf.Sin(elapsed * (1.5f + Level * 0.3f)) * 6, 0) : Vector2.Zero);
    public (int Id, Vector2 Error, float Radius)? AngularFocus => NeedsAim && phase == Phase.Signal ? (completed + 1, AimError, Radius) : null;
    Vector2 AimError => TargetOffset - new Vector2(Mathf.Wrap(G.View.ViewYaw - yaw0, -180, 180), G.View.ViewPitch - pitch0);
    public override void Update(float dt)
    {
        elapsed += dt;
        ulong click = Session?.LastFirePressUsec ?? 0;
        bool pressed = click != 0 && click != seen;
        if (pressed) seen = click;
        if (phase == Phase.Wait)
        {
            if (pressed) { early++; Event("early_click"); End(false, L("Anticipado", "Early")); }
            else if (elapsed >= wait)
            {
                phase = Phase.Signal; elapsed = 0; stimulus = Time.GetTicksUsec();
                yaw0 = G.View.ViewYaw; pitch0 = G.View.ViewPitch;
                if (NeedsAim) offset.Y = ReachablePitchOffset(pitch0, offset.Y);
                drawn = Kind == ReactionGameKind.Audio || Auto;
                if (Kind == ReactionGameKind.Audio) G.Sound("tick", 1, 1 + Level * 0.08f);
                Event("reaction_game_signal", go ? 1 : 0, (float)Kind);
            }
        }
        else if (phase == Phase.Signal)
        {
            if (Auto && go && elapsed >= 0.3f)
            {
                if (NeedsAim) { G.View.Yaw = yaw0 + TargetOffset.X; G.View.Pitch = pitch0 + TargetOffset.Y; }
                Respond(300, !NeedsAim || AimError.Length() <= Radius, 170);
            }
            else if (pressed && drawn)
            {
                float ms = ((long)click - (long)stimulus) / 1000f;
                ulong move = Session?.FirstMoveAfter(stimulus, 6) ?? 0;
                Respond(ms, !NeedsAim || AimError.Length() <= Radius, move > stimulus ? (move - stimulus) / 1000f : ms);
            }
            else if (elapsed >= (Auto ? 0.5f : Deadline))
            {
                if (!go) { held++; Event("reaction_game_hold"); End(true, L("Correcto: no respondiste", "Correct: response withheld")); }
                else { missed++; Event("reaction_game_timeout"); End(false, L("Se agotó el tiempo", "Time ran out")); }
            }
        }
        else if (elapsed >= (Auto ? 0.1f : 0.65f) && !Done) Next();
    }
    void Respond(float ms, bool aimed, float movement)
    {
        if (ms < 90) { early++; Event("early_click"); End(false, L("Anticipado", "Anticipated")); return; }
        Shots++;
        if (!go) { wrong++; Event("reaction_game_false_alarm"); End(false, L("La regla pedía esperar", "The rule said wait")); return; }
        if (!aimed) { wrong++; Event("reaction_game_aim_miss"); G.Sound("fail", 0.3f); return; }
        Hits++; response.Add(ms); KillTimes.Add(ms);
        Event("reaction_game", ms, NeedsAim ? movement : 0);
        if (NeedsAim) { onset.Add(movement); Event("reaction_game_onset", movement); }
        End(true, NeedsAim ? L($"Inicio {movement:0} ms · acierto {ms:0} ms", $"Onset {movement:0} ms · hit {ms:0} ms") : $"{ms:0} ms");
    }
    void End(bool good, string text)
    {
        completed++; phase = Phase.Feedback; elapsed = 0; feedback = text;
        Event("reaction_game_trial", good ? 1 : 0);
        Score = Math.Max(0, (Hits + held) * 100 - (early + wrong + missed) * 25);
        G.Sound(good ? "body" : "fail", 0.5f);
        if (Auto && completed == 16) GD.Print($"[reaction-games] {Key}: {completed} completed, {early + wrong + missed} errors, {response.Count} responses, {held} holds");
    }
    public override float Accuracy => completed == 0 ? 0 : (float)(Hits + held) / Math.Max(completed, Hits + held + early + wrong + missed);
    public override IEnumerable<(string Key, float Value)> ExtraMetrics()
    {
        yield return ("game.trials", completed); yield return ("game.early", early);
        yield return ("game.wrong", wrong); yield return ("game.missed", missed); yield return ("game.holds", held);
        if (response.Count > 0) { yield return ("game.response_ms", Median(response)); yield return ("game.responses", response.Count); }
        if (onset.Count > 0) yield return ("game.onset_ms", Median(onset));
    }
    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        yield return (L("Intentos", "Trials"), $"{completed}/16");
        if (response.Count > 0) yield return (L("Mediana de respuesta", "Median response"), $"{Median(response):0} ms");
        if (onset.Count > 0) yield return (L("Mediana de inicio", "Median onset"), $"{Median(onset):0} ms");
        yield return (L("Anticipaciones / errores / omisiones", "Early / errors / omissions"), $"{early} / {wrong} / {missed}");
        yield return (L("Esperas correctas", "Correct holds"), held.ToString());
    }
    public override void Draw2D(CanvasItem c, Vector2 size)
    {
        bool signal = phase == Phase.Signal;
        if (signal && !drawn) { drawn = true; stimulus = Time.GetTicksUsec(); }
        float k = size.Y / 720; var center = size / 2;
        c.DrawRect(new Rect2(Vector2.Zero, size), new Color(0.025f, 0.035f, 0.055f));
        void Text(string text, float y, int fs, Color color) => Gfx.TextC(c, UiTheme.Display, text, center.X, y, UiTheme.Fs(fs, k), color);
        // The shared countdown presents Name and Description once; do not repeat them here.
        if (Kind == ReactionGameKind.Rules) Text(completed % 2 == 0 ? L("REGLA: PAR", "RULE: EVEN") : L("REGLA: ROMBO", "RULE: DIAMOND"), size.Y * 0.34f, 20, UiTheme.Teal);
        if (phase == Phase.Feedback) Text(feedback, size.Y * 0.57f, 30, UiTheme.Text);
        else if (phase == Phase.Wait)
        {
            // Memory needs its sample number; every other waiting phase stays visually quiet.
            if (Kind == ReactionGameKind.Memory && !ShowInstructions) Text(memory.ToString(), size.Y * 0.55f, 32, UiTheme.Text);
        }
        else if (NeedsAim)
        {
            float scale = size.Y / 70f; Vector2 pos = center + AimError * scale;
            if (Kind == ReactionGameKind.AimSearch)
                for (int i = 0; i < 3 + Level * 2; i++)
                {
                    float a = i * Mathf.Tau / (3 + Level * 2);
                    Vector2 decoy = new Vector2(Mathf.Cos(a) * 22, Mathf.Sin(a) * 12) - new Vector2(Mathf.Wrap(G.View.ViewYaw - yaw0, -180, 180), G.View.ViewPitch - pitch0);
                    c.DrawArc(center + decoy * scale, Radius * scale, 0, Mathf.Tau, 32, UiTheme.Dim, 2 * k);
                }
            c.DrawCircle(pos, Radius * scale, UiTheme.Teal); c.DrawCircle(pos, Radius * scale * 0.22f, Colors.White);
        }
        else if (Kind == ReactionGameKind.Flash) c.DrawRect(new Rect2(center - new Vector2(130, 70) * k, new Vector2(260, 140) * k), UiTheme.Teal);
        else if (Kind == ReactionGameKind.Peripheral) c.DrawCircle(center + offset.Normalized() * size.Y * (0.32f + Level * 0.02f), (16 - Level * 2) * k, UiTheme.Teal);
        else if (Kind != ReactionGameKind.Audio)
        {
            if (Kind is ReactionGameKind.Inhibition or ReactionGameKind.Rules)
            {
                if (diamond) c.DrawColoredPolygon(new[] { center + new Vector2(0, -65) * k, center + new Vector2(65, 0) * k, center + new Vector2(0, 65) * k, center + new Vector2(-65, 0) * k }, new Color(0.1f, 0.4f, 0.4f));
                else c.DrawCircle(center, 65 * k, new Color(0.1f, 0.4f, 0.4f));
            }
            if (Kind != ReactionGameKind.Inhibition) Text(value.ToString(), center.Y + 17 * k, 46, UiTheme.Text);
        }
        if (NeedsAim || Kind == ReactionGameKind.Peripheral)
        {
            c.DrawLine(center - Vector2.Right * 7 * k, center + Vector2.Right * 7 * k, Colors.White, 2 * k);
            c.DrawLine(center - Vector2.Down * 7 * k, center + Vector2.Down * 7 * k, Colors.White, 2 * k);
        }
        Text($"{Math.Min(completed + 1, 16)}/16", size.Y * 0.86f, 20, UiTheme.Text);
    }
}
