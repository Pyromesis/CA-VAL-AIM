using Godot;
using CaValAim.Core;

namespace CaValAim.UI;

/// <summary>A separate button: starring never starts the parent row's task.</summary>
public partial class FavoriteStar : BaseButton
{
    public FavoriteKind Kind;
    public string Key = "";
    public float K = 1;
    public Action? Changed;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        Pressed += () => { Favorites.Toggle(Kind, Key); UiTheme.ClickSound(); Refresh(); Changed?.Invoke(); };
        Favorites.Changed += Refresh;
        Refresh();
    }
    public override void _ExitTree() => Favorites.Changed -= Refresh;
    void Refresh()
    {
        TooltipText = Favorites.Has(Kind, Key)
            ? (Loc.Spanish ? "Quitar de favoritos" : "Remove from favorites")
            : (Loc.Spanish ? "Añadir a favoritos" : "Add to favorites");
        QueueRedraw();
    }
    public override void _Draw()
    {
        bool on = Favorites.Has(Kind, Key);
        var points = Enumerable.Range(0, 10).Select(i => Size / 2 + Vector2.Up.Rotated(i * Mathf.Pi / 5) * (i % 2 == 0 ? 10 : 4.5f) * K).ToArray();
        var color = on ? UiTheme.Warn : IsHovered() || HasFocus() ? UiTheme.Text : UiTheme.Dim;
        if (on) DrawColoredPolygon(points, color);
        DrawPolyline(points.Append(points[0]).ToArray(), color, Math.Max(1, 1.4f * K), true);
    }
    public static FavoriteStar Attach(Control host, FavoriteKind kind, string key, float k, Action? changed = null)
    {
        var star = new FavoriteStar { Kind = kind, Key = key, K = k, Changed = changed,
            AnchorLeft = 1, AnchorRight = 1, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -36 * k, OffsetRight = -2 * k, OffsetTop = -17 * k, OffsetBottom = 17 * k };
        host.AddChild(star);
        return star;
    }
}
