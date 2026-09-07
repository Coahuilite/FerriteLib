using UnityEngine;
using Verse;

using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Theme-aware, pure drawing helpers for greenfield widgets. These helpers only paint; input
/// authority remains with native IMGUI/Verse controls. Every public helper restores the IMGUI
/// and Verse text state it temporarily uses.
/// </summary>
public static class UiThemeDraw
{
    /// <param name="singleLine">
    /// True for labels that must stay on one line (badges, dropdown display text). The fitting audit then
    /// checks width instead of height, because for those labels wrapping is not an available answer.
    /// </param>
    public static void Label(Rect rect, string text, UiTheme theme, Color? color = null, UiFont? font = null, TextAnchor anchor = TextAnchor.MiddleLeft, bool singleLine = false)
    {
        UiFont resolvedFont = font ?? theme.DefaultFont;
        // Every kernel label funnels through this method, which is what makes the fitting audit cheap:
        // one hook covers the whole page, and widgets never have to remember to check themselves.
        UiFitAudit.Check(rect, text, resolvedFont, singleLine);

        Color oldColor = GUI.color;
        TextAnchor oldAnchor = Text.Anchor;
        GameFont oldFont = Text.Font;
        try
        {
            Text.Font = UiKitFonts.ToGameFont(resolvedFont);
            Text.Anchor = anchor;
            GUI.color = color ?? theme.TextPrimary;
            VerseWidgets.Label(rect, text);
        }
        finally
        {
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }
    }

    public static void Surface(Rect rect, UiTheme theme, Color fill, Color? border = null)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;

        Color oldColor = GUI.color;
        try
        {
            Solid(rect, fill);
            Color borderColor = border ?? theme.Border;
            Solid(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            Solid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), borderColor);
            Solid(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            Solid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), borderColor);
        }
        finally
        {
            GUI.color = oldColor;
        }
    }

    public static void Panel(Rect rect, UiTheme theme)
    {
        Surface(rect, theme, theme.Panel, theme.Border);
    }

    public static void Base(Rect rect, UiTheme theme)
    {
        Surface(rect, theme, theme.Base, theme.Border);
    }

    /// <summary>Paints the uninterrupted workspace plane behind a multi-column layout.</summary>
    public static void Workspace(Rect rect, UiTheme theme)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Color oldColor = GUI.color;
        try
        {
            Solid(rect, theme.WorkspacePlane);
        }
        finally
        {
            GUI.color = oldColor;
        }
    }

    /// <summary>Descriptive alias for callers that treat the workspace as a background plane.</summary>
    public static void BackgroundPlane(Rect rect, UiTheme theme)
    {
        Workspace(rect, theme);
    }

    /// <summary>Paints a flat section band and its one-pixel lower rule.</summary>
    public static void SectionBand(Rect rect, UiTheme theme, Color? fill = null, Color? rule = null)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Color oldColor = GUI.color;
        try
        {
            Solid(rect, fill ?? theme.SectionBand);
            Solid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), rule ?? theme.Divider);
        }
        finally
        {
            GUI.color = oldColor;
        }
    }

    /// <summary>Paints a section band with an optional general-purpose title.</summary>
    public static void SectionHeader(Rect rect, string title, UiTheme theme, Color? color = null, UiFont? font = null)
    {
        SectionBand(rect, theme);
        if (!string.IsNullOrEmpty(title))
        {
            Label(rect, title, theme, color ?? theme.TextPrimary, font ?? UiFont.Small);
        }
    }

    /// <summary>Paints the narrow edge marker used for active or focused content.</summary>
    public static void AccentRail(Rect rect, UiTheme theme, bool active = true, float width = 2f, Color? color = null)
    {
        if (!active || rect.width <= 0f || rect.height <= 0f) return;
        Color oldColor = GUI.color;
        try
        {
            Solid(new Rect(rect.x, rect.y, Mathf.Clamp(width, 1f, rect.width), rect.height), color ?? theme.AccentGold);
        }
        finally
        {
            GUI.color = oldColor;
        }
    }

    /// <summary>Alias emphasizing that the rail marks keyboard or mouse focus.</summary>
    public static void FocusRail(Rect rect, UiTheme theme, bool focused = true, float width = 2f)
    {
        AccentRail(rect, theme, focused, width);
    }

    /// <summary>Paints a compact rectangular status treatment without owning interaction.</summary>
    public static void StatusTreatment(Rect rect, UiTheme theme, UiStatusTone tone = UiStatusTone.Neutral)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;

        Color fill;
        Color border;
        switch (tone)
        {
            case UiStatusTone.Active:
                fill = theme.Selected;
                border = theme.AccentGold;
                break;
            case UiStatusTone.Success:
                fill = theme.Success;
                border = theme.BorderStrong;
                break;
            case UiStatusTone.Warning:
                fill = theme.Warning;
                border = theme.Danger;
                break;
            case UiStatusTone.Danger:
                fill = theme.Danger;
                border = theme.Danger;
                break;
            case UiStatusTone.Disabled:
                fill = theme.Base;
                border = theme.Divider;
                break;
            default:
                fill = theme.Raised;
                border = theme.Border;
                break;
        }
        Surface(rect, theme, fill, border);
    }

    /// <summary>Paints a status treatment and centers its short, reusable badge label.</summary>
    public static void StatusBadge(Rect rect, string text, UiTheme theme, UiStatusTone tone = UiStatusTone.Neutral, UiFont? font = null)
    {
        StatusTreatment(rect, theme, tone);
        Color textColor = tone switch
        {
            UiStatusTone.Active => theme.TextOnGold,
            UiStatusTone.Success => theme.TextOnGold,
            UiStatusTone.Warning => theme.TextOnDanger,
            UiStatusTone.Danger => theme.TextOnDanger,
            UiStatusTone.Disabled => theme.TextDisabled,
            _ => theme.TextSecondary
        };
        Label(rect, text, theme, textColor, font ?? UiFont.Tiny, TextAnchor.MiddleCenter, singleLine: true);
    }

    /// <summary>
    /// Paints one axis-aligned solid rect. This is the only fill primitive in the library: widgets that
    /// need a rule, a glyph cell or a data point call this instead of reaching for
    /// <c>Verse.Widgets.DrawBoxSolid</c>, which is what keeps the backend-contact allowlist short enough
    /// to gate (see the harness containment lane).
    /// </summary>
    public static void Solid(Rect rect, Color color)
    {
        if (rect.width > 0f && rect.height > 0f)
        {
            VerseWidgets.DrawBoxSolid(rect, color);
        }
    }
}

/// <summary>Generic visual states understood by <see cref="UiThemeDraw.StatusTreatment"/>.</summary>
public enum UiStatusTone
{
    Neutral,
    Active,
    Success,
    Warning,
    Danger,
    Disabled
}
