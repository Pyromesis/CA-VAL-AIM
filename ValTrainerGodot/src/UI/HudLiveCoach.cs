using Godot;
using ValTrainer.Analysis;
using ValTrainer.Core;

namespace ValTrainer.UI;

public partial class Hud
{
    /// <summary>
    /// The live coach's current cue (<see cref="LiveCoach"/>): a quiet card under the top-right corner that slides in with
    /// an ease-out, holds, and fades. Accent bar red for a problem, green for a good streak; headline, the fix, and the
    /// evidence in small caps. It never covers the crosshair.
    /// </summary>
    void DrawLiveTip(Vector2 size, float k, LiveTip tip)
    {
        float age = s.Now - tip.At;
        float inT = Gfx.EaseOutCubic(Mathf.Clamp(age / 0.35f, 0, 1));
        float outT = Mathf.Clamp((5.5f - age) / 0.6f, 0, 1);
        float a = Mathf.Min(inT, outT);
        if (a <= 0.01f) return;
        float w = 440 * k, pad = 18 * k;
        int hs = UiTheme.Fs(20, k), bs = UiTheme.Fs(15, k), es = UiTheme.Fs(11.5f, k);
        // wrap the action to the card width (at most two lines)
        string action = tip.Action;
        float textW = w - pad * 2 - 6 * k;
        var lines = Wrap(action, UiTheme.Body, bs, textW, 2);
        float h = pad + 26 * k + lines.Count * 21 * k + 22 * k + pad * 0.6f;
        float x = size.X - w - 24 * k + (1f - inT) * 40 * k, y = 118 * k;
        var col = tip.Positive ? UiTheme.Good : UiTheme.Accent;
        DrawRect(new Rect2(x, y, w, h), new Color(0.03f, 0.04f, 0.055f, 0.82f * a));
        DrawRect(new Rect2(x, y, 3 * k, h), new Color(col, a));
        Gfx.Text(this, UiTheme.HudWide, Loc.Spanish ? "COACH" : "COACH", x + pad, y + pad + 2 * k, UiTheme.Fs(10.5f, k), new Color(col, 0.9f * a));
        float ty = y + pad + 26 * k;
        Gfx.TextFit(this, UiTheme.Display, tip.Headline, x + pad, ty, hs, new Color(UiTheme.Text, a), textW);
        ty += 22 * k;
        foreach (var l in lines)
        {
            DrawString(UiTheme.Body, new Vector2(x + pad, ty), l, HorizontalAlignment.Left, -1, bs, new Color(UiTheme.TextSoft, a));
            ty += 21 * k;
        }
        Gfx.TextFit(this, UiTheme.HudWide, tip.Evidence.ToUpperInvariant(), x + pad, ty + 4 * k, es, new Color(UiTheme.Faint, a), textW);
    }

    static List<string> Wrap(string text, Font f, int size, float width, int maxLines)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        string cur = "";
        foreach (var w in words)
        {
            string next = cur.Length == 0 ? w : cur + " " + w;
            if (f.GetStringSize(next, HorizontalAlignment.Left, -1, size).X > width && cur.Length > 0)
            {
                lines.Add(cur);
                cur = w;
                if (lines.Count == maxLines) break;
            }
            else cur = next;
        }
        if (lines.Count < maxLines && cur.Length > 0) lines.Add(cur);
        else if (lines.Count == maxLines && cur.Length > 0 && !lines[^1].EndsWith("…")) lines[^1] = lines[^1].TrimEnd('.', ',') + "…";
        return lines;
    }
}
