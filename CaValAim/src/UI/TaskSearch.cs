using System.Globalization;
using System.Text;
using Godot;
using CaValAim.Core;

namespace CaValAim.UI;

/// <summary>Shared search: all words, case/accent insensitive, with a clear button.</summary>
public static class TaskSearch
{
    static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
    public static bool Matches(string query, params string[] fields)
    {
        string haystack = Normalize(string.Join(" ", fields));
        return Normalize(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).All(haystack.Contains);
    }
    public static LineEdit Create(float k, string text, Action<string> changed)
    {
        var box = new LineEdit { Text = text, PlaceholderText = Loc.Spanish ? "Buscar por nombre o descripción…" : "Search by name or description…",
            ClearButtonEnabled = true, MaxLength = 160, CustomMinimumSize = new Vector2(0, 38 * k), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        SettingsScreen.StyleEdit(box, k, 14, UiTheme.Body);
        box.TextChanged += value => changed(value);
        return box;
    }
    public static void Bind(VBoxContainer host, float k, List<(Control Row, string Text)> rows)
    {
        var empty = new Label { Text = Loc.Spanish ? "Sin resultados. Prueba otras palabras." : "No results. Try different words.", Visible = false };
        host.AddChild(Create(k, "", query => {
            int count = 0;
            foreach (var (row, text) in rows) { row.Visible = Matches(query, text); if (row.Visible) count++; }
            empty.Visible = count == 0;
        }));
        host.AddChild(empty);
    }
}
