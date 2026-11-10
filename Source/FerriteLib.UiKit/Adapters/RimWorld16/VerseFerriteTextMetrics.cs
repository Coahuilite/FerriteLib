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
    // Owned by the composed, window-local ruler, never by the shared legacy singleton. Scratch arrays
    // are not published: changed snapshots detach them before becoming measurement-cache inputs.
    internal sealed class EnvironmentCapture
    {
        internal readonly List<string> Identity = new();
        internal readonly float[] Numbers = new float[22];
        private UiMeasureInputs? last;
        internal UiMeasureInputs Snapshot()
        {
            if (last != null && last.Matches(Identity, Numbers)) return last;
            return last = UiMeasureInputs.FromOwnedArrays(Identity.ToArray(), (float[])Numbers.Clone());
        }
    }
    internal UiMeasureInputs? NumericEnvironment(UiTextFont font, EnvironmentCapture? capture = null)
        => HasUnityFontEnvironment ? WithFontSize(font, true, () => CaptureUnityEnvironment(font.Alias, capture)) : null;

    internal UiMeasureInputs? CaptureEnvironment(UiFont font, EnvironmentCapture capture)
        => HasUnityFontEnvironment ? CaptureUnityEnvironment(font, capture) : null;

    UiMeasureInputs? IUiTextMetricsEnvironment.CaptureEnvironment(UiFont font)
        => HasUnityFontEnvironment ? CaptureUnityEnvironment(font) : null;

    // Keep unavailable Unity members out of the 1.7 caller's JIT body, including under inlining.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static UiMeasureInputs? CaptureUnityEnvironment(UiFont font, EnvironmentCapture? capture = null)
    {
#if FER_RW17
        // UnityStubs has no complete native font revision. Revoke reuse instead of inventing a stamp.
        return null;
#else
        GameFont previous = Text.Font;
        try
        {
            Text.Font = UiKitFonts.ToGameFont(font); // Includes Verse's real Tiny-to-Small fallback.
            GUIStyle style = Text.CurFontStyle;
            Font resource = style.font;
            if (resource == null) return null;
            List<string> identity = capture?.Identity ?? new List<string>();
            identity.Clear();
            identity.Add("Verse1.6/IMGUI");
            identity.Add(resource.GetInstanceID().ToString(CultureInfo.InvariantCulture));
            identity.AddRange(resource.fontNames);
            float[] values = capture?.Numbers ?? new float[22];
            values[0] = (float)Text.Font; values[1] = resource.fontSize; values[2] = resource.dynamic ? 1f : 0f;
            values[3] = style.fontSize; values[4] = (float)style.fontStyle; values[5] = style.richText ? 1f : 0f;
            values[6] = style.fixedWidth; values[7] = style.fixedHeight;
            values[8] = style.stretchWidth ? 1f : 0f; values[9] = style.stretchHeight ? 1f : 0f;
            values[10] = (float)style.imagePosition; values[11] = (float)style.clipping;
            values[12] = style.contentOffset.x; values[13] = style.contentOffset.y;
            values[14] = style.padding.left; values[15] = style.padding.right;
            values[16] = style.padding.top; values[17] = style.padding.bottom;
            values[18] = style.border.left; values[19] = style.border.right;
            values[20] = style.border.top; values[21] = style.border.bottom;
            return capture?.Snapshot() ?? UiMeasureInputs.FromOwnedArrays(identity.ToArray(), values);
        }
        finally { Text.Font = previous; }
#endif
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
