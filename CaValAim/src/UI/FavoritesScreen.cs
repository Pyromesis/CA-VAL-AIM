using Godot;
using CaValAim.Core;
using CaValAim.Modes;

namespace CaValAim.UI;

public partial class FavoritesScreen : ScreenBase
{
    protected override bool AnimatedBackdrop => false;
    protected override void Back() => Main.I.ShowMenu();
    protected override void Build()
    {
        AddChild(new MinimalBackdrop());
        var root = VBox(16 * K);
        var page = Margins(root, 65 * K, 35 * K, 65 * K, 30 * K);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(page);
        root.AddChild(Lbl(Loc.Spanish ? "Favoritos" : "Favorites", UiTheme.Light, 48, UiTheme.Text));
        root.AddChild(Lbl(Loc.Spanish ? "Tus tareas, rutinas y listas marcadas. Pulsa una estrella para quitarla." : "Your starred drills, routines and playlists. Press a star to remove it.", UiTheme.Body, 16, UiTheme.Dim));
        var searchRows = new List<(Control Row, string Text)>();
        TaskSearch.Bind(root, K, searchRows);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        var list = VBox(0); list.SizeFlagsHorizontal = SizeFlags.ExpandFill; scroll.AddChild(list);
        int visible = 0;
        foreach (var favorite in Favorites.All.ToArray())
        {
            string? title = null; Action? play = null;
            if (favorite.Kind == FavoriteKind.Drill && ModeRegistry.Find(favorite.Key) is { } make)
            { title = CoachData.DrillName(favorite.Key); play = () => Main.I.StartMode(make); }
            else if (favorite.Kind == FavoriteKind.Routine && RoutineLibrary.Find(favorite.Key) is { } routine)
            { title = routine.Name; play = () => { if (routine.Playable) routine.Play(); else RoutinesScreen.OpenRoutine(routine.Id); }; }
            else if (favorite.Kind == FavoriteKind.Playlist && PlaylistStore.All.FirstOrDefault(p => p.Id == favorite.Key) is { } playlist)
            { title = playlist.Name; play = () => PlaylistRunner.Play(playlist); }
            if (title == null || play == null) continue;
            visible++;
            string kind = favorite.Kind switch { FavoriteKind.Drill => Loc.Spanish ? "TAREA" : "DRILL", FavoriteKind.Routine => Loc.Spanish ? "RUTINA" : "ROUTINE", _ => Loc.Spanish ? "LISTA" : "PLAYLIST" };
            var row = new MDrillRow { K = K, Title = title, Meta = kind,
                FavoriteType = favorite.Kind, FavoriteKey = "", CustomMinimumSize = new Vector2(0, 64 * K), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            // Manual attachment refreshes the list immediately when a favorite is removed.
            row.Pressed += play;
            list.AddChild(row);
            searchRows.Add((row, $"{title} {kind} {favorite.Key} {(favorite.Kind == FavoriteKind.Drill && ModeRegistry.Find(favorite.Key) is { } factory ? Loc.T(factory().Description) : "")}"));
            FavoriteStar.Attach(row, favorite.Kind, favorite.Key, K, () => Callable.From(Rebuild).CallDeferred());
        }
        if (visible == 0) list.AddChild(Lbl(Loc.Spanish ? "Todavía no hay favoritos. Marca una estrella en la biblioteca, las rutinas o tus listas." : "No favorites yet. Star an item in the drill library, routines or playlists.", UiTheme.Body, 18, UiTheme.Dim));
        root.AddChild(Btn(Loc.Spanish ? "VOLVER" : "BACK", VButton.Look.Secondary, Back, 160, 42, 14));
    }
}
