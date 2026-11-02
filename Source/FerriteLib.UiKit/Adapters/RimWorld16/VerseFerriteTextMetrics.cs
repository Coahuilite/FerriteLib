using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The production <see cref="ITextMetrics"/>.
/// It lives in the library, not in a consumer, because it is the only real implementation of the
/// library's own interface: shipping the interface without it would force every consumer to re-derive
/// the same save/restore dance around Verse's process-global text state, and that dance is precisely
/// where a previous round of measurement defects came from. The font mapping is the library's
/// <see cref="UiKitFonts"/>, so measurement and drawing cannot disagree about a font size.
/// </summary>
public sealed class VerseFerriteTextMetrics : ITextMetrics, IUiTextMetricsEnvironment
{
    /// <summary>
    /// Shared instance; the type retains no UI state. A host and its text-fit audit must be handed the same
    /// metrics *model* - a wrap-aware one like this - or layout and audit measure against two different
    /// rulers and the audit goes quietly vacuous.
    /// </summary>
    public static readonly VerseFerriteTextMetrics Instance = new();

    // The 1.7 host resolves Font to UnityStubs, which has no fontNames or complete font-data revision.
    // Unknown backends revoke optional measurement reuse rather than publish an incomplete stamp.
    private static readonly bool HasUnityFontEnvironment =
        string.Equals(typeof(Font).Assembly.GetName().Name, "UnityEngine.TextRenderingModule", StringComparison.Ordinal);
    private static readonly bool Has17FontProvider = string.Equals(typeof(Font).Assembly.GetName().Name, "UnityStubs", StringComparison.Ordinal);
    private const bool NumericLabels17 = true;
    internal static bool SupportsTextPurpose(UiTextPurpose purpose) => purpose == UiTextPurpose.Label
        ? HasUnityFontEnvironment || Has17FontProvider && NumericLabels17 : purpose == UiTextPurpose.Editor && HasUnityFontEnvironment;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static T WithFontSize<T>(UiTextFont font, bool wrap, Func<T> action)
    {
        GameFont previousFont = Text.Font; TextAnchor previousAnchor = Text.Anchor; bool previousWrap = Text.WordWrap;
        try
        {
            Text.Font = UiKitFonts.ToGameFont(font.Alias); Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = wrap;
            GUIStyle style = Text.CurFontStyle;
            int previousSize = style.fontSize;
            try { if (font.Size.HasValue) style.fontSize = font.Size.Value; return action(); }
            finally { style.fontSize = previousSize; }
        }
        finally { Text.Font = previousFont; Text.Anchor = previousAnchor; Text.WordWrap = previousWrap; }
    }
    internal UiMeasureInputs? NumericEnvironment(UiTextFont font)
        => HasUnityFontEnvironment ? WithFontSize(font, true, () => CaptureUnityEnvironment(font.Alias)) : null;

    UiMeasureInputs? IUiTextMetricsEnvironment.CaptureEnvironment(UiFont font)
        => HasUnityFontEnvironment ? CaptureUnityEnvironment(font) : null;

    // Keep unavailable Unity members out of the 1.7 caller's JIT body, including under inlining.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static UiMeasureInputs? CaptureUnityEnvironment(UiFont font)
    {
        GameFont previous = Text.Font;
        try
        {
            Text.Font = UiKitFonts.ToGameFont(font); // Includes Verse's real Tiny-to-Small fallback.
            GUIStyle style = Text.CurFontStyle;
            Font resource = style.font;
            if (resource == null) return null;
            var identity = new List<string> { "Verse1.6/IMGUI", resource.GetInstanceID().ToString(CultureInfo.InvariantCulture) };
            identity.AddRange(resource.fontNames);
            return new UiMeasureInputs(identity.ToArray(), new[] {
                (float)Text.Font, resource.fontSize, resource.dynamic ? 1f : 0f,
                style.fontSize, (float)style.fontStyle, style.richText ? 1f : 0f,
                style.fixedWidth, style.fixedHeight, style.stretchWidth ? 1f : 0f, style.stretchHeight ? 1f : 0f,
                (float)style.imagePosition, (float)style.clipping, style.contentOffset.x, style.contentOffset.y,
                style.padding.left, style.padding.right, style.padding.top, style.padding.bottom,
                style.border.left, style.border.right, style.border.top, style.border.bottom });
        }
        finally { Text.Font = previous; }
    }

    public float MeasureText(string text, UiFont font, float width)
    {
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        bool oldWrap = Text.WordWrap;
        Text.Font = UiKitFonts.ToGameFont(font);
        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = true;
        try
        {
            return Text.CalcHeight(text ?? "", Mathf.Max(1f, width));
        }
        finally
        {
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            Text.WordWrap = oldWrap;
        }
    }

    public float MeasureWidth(string text, UiFont font)
    {
        GameFont oldFont = Text.Font;
        TextAnchor oldAnchor = Text.Anchor;
        bool oldWrap = Text.WordWrap;
        Text.Font = UiKitFonts.ToGameFont(font);
        Text.Anchor = TextAnchor.UpperLeft;
        // Wrapping would cap the reported width at the wrap point and hide exactly the overflow the
        // caller is looking for; the unwrapped extent is the number that must be compared to a rect.
        Text.WordWrap = false;
        try
        {
            return Text.CalcSize(text ?? "").x;
        }
        finally
        {
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            Text.WordWrap = oldWrap;
        }
    }
}
