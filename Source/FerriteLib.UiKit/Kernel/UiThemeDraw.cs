using System;
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
        UiFont resolvedFont = font ?? theme.Styles.Font;
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

    /// <summary>
    /// Paints one surface treatment: the fill plus its hairline edge. This is the pair-shaped entry a
    /// table answer feeds, so a caller never spells a fill and a border separately — which is how the
    /// same two tokens used to end up re-derived per widget.
    /// </summary>
    public static void Surface(Rect rect, UiSurfaceStyle style, float hairline = 1f)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;

        float edge = Math.Max(1f, hairline);
        Color oldColor = GUI.color;
        try
        {
            Solid(rect, style.Fill);
            Solid(new Rect(rect.x, rect.y, rect.width, edge), style.Border);
            Solid(new Rect(rect.x, rect.yMax - edge, rect.width, edge), style.Border);
            Solid(new Rect(rect.x, rect.y, edge, rect.height), style.Border);
            Solid(new Rect(rect.xMax - edge, rect.y, edge, rect.height), style.Border);
        }
        finally
        {
            GUI.color = oldColor;
        }
    }

    /// <summary>Paints an explicit fill with an explicit edge colour, or the theme's shared border.</summary>
    public static void Surface(Rect rect, UiTheme theme, Color fill, Color? border = null)
    {
        Surface(rect, new UiSurfaceStyle(fill, border ?? theme.Border), theme.Geometry.Hairline);
    }

    public static void Panel(Rect rect, UiTheme theme)
    {
        Surface(rect, theme.PanelSurface, theme.Geometry.Hairline);
    }

    public static void Base(Rect rect, UiTheme theme)
    {
        Surface(rect, theme.BaseSurface, theme.Geometry.Hairline);
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

    /// <summary>Paints a flat section band and its lower rule, both at the theme's density.</summary>
    public static void SectionBand(Rect rect, UiTheme theme, Color? fill = null, Color? rule = null)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Color oldColor = GUI.color;
        try
        {
            float edge = Math.Max(1f, theme.Geometry.Hairline);
            Solid(rect, fill ?? theme.SectionBand);
            Solid(new Rect(rect.x, rect.yMax - edge, rect.width, edge), rule ?? theme.Divider);
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
        Surface(rect, theme.Styles.Resolve(tone).Surface, theme.Geometry.Hairline);
    }

    /// <summary>
    /// The stable paint of an element the tree recovered after it threw: an alarm-toned band carrying
    /// the layout path it was supposed to occupy. Deliberately generic — the library owns no product
    /// vocabulary and no translation keys, so it reports the slot's identity rather than a sentence
    /// nobody declared. The diagnostic text lives in the session, not here.
    /// </summary>
    public static void RecoveryBand(Rect rect, UiTheme theme, string elementPath)
    {
        if (rect.width <= 1f || rect.height <= 1f) return;

        Surface(rect, theme, theme.Danger, theme.Border);
        Label(
            new Rect(rect.x + 6f, rect.y, Math.Max(1f, rect.width - 12f), rect.height),
            elementPath ?? "",
            theme,
            theme.TextOnDanger,
            UiFont.Tiny,
            singleLine: true);
    }

    /// <summary>Paints a status treatment and centers its short, reusable badge label.</summary>
    public static void StatusBadge(Rect rect, string text, UiTheme theme, UiStatusTone tone = UiStatusTone.Neutral, UiFont? font = null)
    {
        // A badge is compact surface text, which is the emphasis whose neutral label is secondary; the
        // plane and the text come from one query, so a badge and a field can no longer disagree.
        UiResolvedStyle style = theme.Styles.Resolve(tone, UiEmphasis.Muted);
        Surface(rect, style.Surface, theme.Geometry.Hairline);
        Label(rect, text, theme, style.Text, font ?? UiFont.Tiny, TextAnchor.MiddleCenter, singleLine: true);
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

/// <summary>
/// Generic visual states understood by <see cref="UiThemeDraw.StatusTreatment"/>. What each one paints
/// is the <see cref="UiStyleTable"/>'s answer, not a switch here.
/// </summary>
public enum UiStatusTone
{
    Neutral,
    Active,
    Success,
    Warning,
    Danger,
    Disabled
}
