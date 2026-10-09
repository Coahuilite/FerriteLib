using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// The placement vocabulary (CP-1/CP-2, 0.7.x Batch 1): where one element sits inside its parent's inner
/// box. The whole rule is one line and both axes use it:
/// <code>origin = parentOrigin + fraction * (parentSpan - selfSpan) + offset</code>
/// where <c>AlignX</c>/<c>AlignY</c> names both the parent's reference point and the element's own pivot,
/// and <c>OffsetX</c>/<c>OffsetY</c> is a pixel nudge or a percentage of the parent's inner span.
/// <para>
/// <b>Stretch is the default and every absent attribute keeps the arrangement the engine already
/// produced.</b> A stretch element resolves to fraction zero, which is exactly where flow placed it, so an
/// existing manifest that never names the vocabulary does not migrate. An explicit attribute always wins.
/// </para>
/// <para>
/// <b>Home and boundary.</b> A single <c>Overlay</c> child may name either axis and an offset; a flow
/// container's child may name only the axis the flow does not own, with no percentage offset, because the
/// main axis already has an owner. This file is pure arithmetic over floats: no drawing outlet, no input,
/// no text, so it stays outside the backend funnel, and no new public type is introduced - the values are
/// attribute strings.
/// </para>
/// <para>
/// <b>The content-relative height mode shares this file's one question: who owns the box?</b>
/// <c>Height="MatchContent"</c> asks the parent for the height its content resolved to, and that question is
/// answerable only where the parent's content height is a <b>maximum</b> over its children (a <c>Row</c> or an
/// <c>Overlay</c>). A container that SUMS its children would have to include the declaring element in the very
/// reference it is asked for, a <c>Wrap</c> discovers a line's membership from the children in that line, and a
/// root has no parent at all. The vocabulary, the parent predicate and the refusal rule live here so the Host,
/// the engine and the template walk all read one definition.
/// </para>
/// </summary>
internal static class UiPlacement
{
    /// <summary>
    /// The content-relative height mode's one accepted value (0.7.x). <c>Height</c> otherwise takes a number or
    /// <c>Auto</c>; this value means "the height the parent's content resolved to".
    /// </summary>
    internal const string MatchContentValue = "MatchContent";

    /// <summary>The four manifest attribute names this vocabulary owns.</summary>
    internal const string AlignXAttribute = "AlignX";

    internal const string OffsetXAttribute = "OffsetX";

    internal const string AlignYAttribute = "AlignY";

    internal const string OffsetYAttribute = "OffsetY";

    /// <summary>
    /// The two placement axes. An edge name belongs to one of them: <c>Left</c> is not an <c>AlignY</c>
    /// value, which is what keeps "which side is this on" answerable from the name alone.
    /// </summary>
    internal enum Axis
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// One resolved alignment: the fraction of the parent's inner span that is shared by the reference point
    /// and the element's own pivot. <see cref="Stretch"/> is fraction zero plus the flag that says "keep the
    /// slot the flow already produced", which is why it is the default rather than a fifth edge.
    /// </summary>
    internal readonly struct Alignment
    {
        internal readonly float Fraction;

        internal readonly bool IsStretch;

        internal Alignment(float fraction, bool stretch)
        {
            Fraction = fraction;
            IsStretch = stretch;
        }

        internal static readonly Alignment Stretch = new(0f, true);
    }

    /// <summary>
    /// The one rule, both axes: the parent's reference point plus the offset, less the element's own pivot.
    /// Written as <c>origin + fraction * (span - selfSpan) + offset</c>, which is the same expression with
    /// the shared fraction factored out.
    /// </summary>
    internal static float Origin(float parentOrigin, float parentSpan, float selfSpan, Alignment alignment, float offset)
    {
        return parentOrigin + alignment.Fraction * (parentSpan - selfSpan) + offset;
    }

    /// <summary>
    /// Reads one alignment attribute. An absent (or empty) attribute is <see cref="Alignment.Stretch"/>,
    /// which is the shipped arrangement; anything else must name an edge of that axis. The engine calls this
    /// while arranging, so a programmatically built spec degrades through the same reader the creation-time
    /// contract used, and the two cannot disagree about what a name means.
    /// </summary>
    internal static Alignment ParseAlign(UiElementSpec spec, string attribute, Axis axis)
    {
        if (!spec.TryGetAttribute(attribute, out string raw)) return Alignment.Stretch;
        if (TryAlignmentOf(raw, axis, out Alignment alignment)) return alignment;

        throw new FormatException(
            "Element id=\"" + spec.Id + "\" (Kind=\"" + spec.Kind + "\") has invalid " + attribute
            + " '" + raw + "'; expected " + AcceptedNames(axis) + ".");
    }

    /// <summary>
    /// Reads one offset attribute as pixels. <c>N%</c> is a fraction of the parent's inner span (the span of
    /// the axis the attribute belongs to); a bare number is a pixel nudge. An absent (or empty) attribute is
    /// zero, so the placement rule collapses to the reference point.
    /// </summary>
    internal static float ParseOffset(UiElementSpec spec, string attribute, float parentSpan)
    {
        if (!spec.TryGetAttribute(attribute, out string raw)) return 0f;

        string value = raw.Trim();
        if (value.Length == 0) return 0f;

        bool percentage = value.EndsWith("%", StringComparison.Ordinal);
        string number = percentage ? value.Substring(0, value.Length - 1).Trim() : value;
        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            || float.IsNaN(parsed) || float.IsInfinity(parsed))
        {
            throw new FormatException(
                "Element id=\"" + spec.Id + "\" (Kind=\"" + spec.Kind + "\") has invalid " + attribute
                + " '" + raw + "'; expected a number or a percentage (N%).");
        }

        return percentage ? parsed / 100f * parentSpan : parsed;
    }

    /// <summary>
    /// Creation-time grammar and refusal matrix for the placement vocabulary, called from the Host's
    /// element walk (and from the engine's own template walk) while the element, the attribute and the path
    /// are all still known. Fail-closed, the A2/A3 precedent: an accepted declaration that the arrangement
    /// would then ignore is the silent no-op this contract layer exists to stop.
    /// <para>
    /// The refusals, each one thrown as a located <see cref="UiContractException"/>: an unknown alignment
    /// value; an edge name from the other axis; a ratio outside 0..100%; a malformed or non-finite offset;
    /// a percentage offset on a child of a flow container; an explicit <c>Stretch</c> together with a
    /// numeric <c>Width</c>; placement together with <c>Fill</c> on the same element; placement on an
    /// element no container owns; and the main-axis or main-axis-offset subset on a flow container's child.
    /// </para>
    /// </summary>
    /// <param name="spec">The element being validated.</param>
    /// <param name="path">Its layout path, which every message carries.</param>
    /// <param name="scope">The manifest source the located error reports as its scope.</param>
    /// <param name="parent">Its parent element, or null for a root (and for a template root).</param>
    /// <param name="parentKind">An explicit parent kind, for a caller whose parent is not an element.</param>
    internal static void ValidateChild(
        UiElementSpec spec, string path, string scope, UiElementSpec? parent, string? parentKind = null)
    {
        bool hasAlignX = spec.TryGetAttribute(AlignXAttribute, out string alignXRaw);
        bool hasAlignY = spec.TryGetAttribute(AlignYAttribute, out string alignYRaw);
        bool hasOffsetX = spec.TryGetAttribute(OffsetXAttribute, out string offsetXRaw);
        bool hasOffsetY = spec.TryGetAttribute(OffsetYAttribute, out string offsetYRaw);
        if (!hasAlignX && !hasAlignY && !hasOffsetX && !hasOffsetY) return;

        string? kind = parent == null ? parentKind : parent.Kind;
        bool placementContainer = IsPlacementContainer(kind);
        Axis crossAxis = Axis.Horizontal;
        bool flow = !placementContainer && TryCrossAxis(kind, out crossAxis);

        if (!placementContainer && !flow)
        {
            throw Contract(spec, path, scope,
                "Element id=\"" + spec.Id + "\" at '" + path + "' declares placement vocabulary but no container owns it; "
                + "AlignX/OffsetX/AlignY/OffsetY are valid on a child of an Overlay, or on the cross axis of a flow container.");
        }

        if (hasAlignX && !TryAlignmentOf(alignXRaw, Axis.Horizontal, out _))
        {
            throw Contract(spec, path, scope,
                "Element id=\"" + spec.Id + "\" at '" + path + "' has invalid " + AlignXAttribute + " '" + alignXRaw
                + "'; expected " + AcceptedNames(Axis.Horizontal) + ".");
        }

        if (hasAlignY && !TryAlignmentOf(alignYRaw, Axis.Vertical, out _))
        {
            throw Contract(spec, path, scope,
                "Element id=\"" + spec.Id + "\" at '" + path + "' has invalid " + AlignYAttribute + " '" + alignYRaw
                + "'; expected " + AcceptedNames(Axis.Vertical) + ".");
        }

        if (hasOffsetX) ValidateOffset(spec, path, scope, OffsetXAttribute, offsetXRaw);
        if (hasOffsetY) ValidateOffset(spec, path, scope, OffsetYAttribute, offsetYRaw);

        if (flow)
        {
            // R1: a flow container's child accepts the cross-axis subset only - the main axis already has an
            // owner, and a second one is the ambiguity this whole vocabulary refuses to introduce.
            if (crossAxis == Axis.Horizontal)
            {
                if (hasAlignY)
                {
                    throw Contract(spec, path, scope,
                        "Element id=\"" + spec.Id + "\" at '" + path + "' declares " + AlignYAttribute + " '" + alignYRaw
                        + "', which is the main axis of a " + kind + "; a flow container's child accepts the cross-axis "
                        + "subset only (" + AlignXAttribute + " here).");
                }

                if (hasOffsetY)
                {
                    throw Contract(spec, path, scope,
                        "Element id=\"" + spec.Id + "\" at '" + path + "' declares " + OffsetYAttribute + " '" + offsetYRaw
                        + "' on the main axis of a " + kind + ", where the flow decides the position; an offset is valid on "
                        + "the cross axis only.");
                }
            }
            else
            {
                if (hasAlignX)
                {
                    throw Contract(spec, path, scope,
                        "Element id=\"" + spec.Id + "\" at '" + path + "' declares " + AlignXAttribute + " '" + alignXRaw
                        + "', which is the main axis of a " + kind + "; a flow container's child accepts the cross-axis "
                        + "subset only (" + AlignYAttribute + " here).");
                }

                if (hasOffsetX)
                {
                    throw Contract(spec, path, scope,
                        "Element id=\"" + spec.Id + "\" at '" + path + "' declares " + OffsetXAttribute + " '" + offsetXRaw
                        + "' on the main axis of a " + kind + ", where the flow decides the position; an offset is valid on "
                        + "the cross axis only.");
                }
            }

            // CP-2's boundary, stated as the plan states it: a percentage is a fraction of the parent's span,
            // and inside a flow container that span is the flow's own main axis - which stays the flow's.
            if (hasOffsetX && IsPercentage(offsetXRaw))
            {
                throw Contract(spec, path, scope,
                    "Element id=\"" + spec.Id + "\" at '" + path + "' declares " + OffsetXAttribute + " '" + offsetXRaw
                    + "', a percentage placement on a child of a flow container; a percentage is a fraction of the "
                    + "parent's span, which the flow owns, so only a pixel nudge is accepted here.");
            }

            if (hasOffsetY && IsPercentage(offsetYRaw))
            {
                throw Contract(spec, path, scope,
                    "Element id=\"" + spec.Id + "\" at '" + path + "' declares " + OffsetYAttribute + " '" + offsetYRaw
                    + "', a percentage placement on a child of a flow container; a percentage is a fraction of the "
                    + "parent's span, which the flow owns, so only a pixel nudge is accepted here.");
            }
        }

        // R6: Stretch says "the parent's whole span"; a numeric Width says a span of its own. Only an
        // EXPLICIT Stretch is refused: with the attribute absent the declaration is exactly the one an
        // existing page already carries, and R2 promises that page does not migrate.
        if (hasAlignX && IsStretch(alignXRaw) && TryNumericWidth(spec, out float declaredWidth))
        {
            throw Contract(spec, path, scope,
                "Element id=\"" + spec.Id + "\" at '" + path + "' declares " + AlignXAttribute + "=\"Stretch\" together "
                + "with Width=\"" + declaredWidth + "\"; Stretch means the parent's whole inner width, so the declared "
                + "width could never be honoured.");
        }

        // R6: Fill claims the flow's remaining space and placement claims a reference point. One owner.
        if (IsFill(spec))
        {
            throw Contract(spec, path, scope,
                "Element id=\"" + spec.Id + "\" at '" + path + "' declares placement vocabulary together with "
                + "Fill=\"true\"; Fill takes the flow's remaining space and placement names a reference point, so the "
                + "two cannot both own this element's box.");
        }
    }

    /// <summary>The one container whose inner box is a two-axis placement frame.</summary>
    internal static bool IsPlacementContainer(string? kind)
    {
        return string.Equals(kind, "Overlay", StringComparison.Ordinal);
    }

    /// <summary>
    /// The axis a flow container leaves to its children, or false when the container is not a flow container
    /// the engine places children in (a placement container is asked separately, and the collection element
    /// is not a container a page authors a child of).
    /// </summary>
    internal static bool TryCrossAxis(string? kind, out Axis axis)
    {
        axis = Axis.Horizontal;

        // A line of flow is horizontal, so its cross axis is vertical.
        if (string.Equals(kind, "Row", StringComparison.Ordinal)
            || string.Equals(kind, "Wrap", StringComparison.Ordinal))
        {
            axis = Axis.Vertical;
            return true;
        }

        if (string.Equals(kind, "Column", StringComparison.Ordinal)
            || string.Equals(kind, "Stack", StringComparison.Ordinal)
            || string.Equals(kind, "Section", StringComparison.Ordinal)
            || string.Equals(kind, "Surface", StringComparison.Ordinal)
            || string.Equals(kind, "Scroll", StringComparison.Ordinal)
            || string.Equals(kind, "Clip", StringComparison.Ordinal))
        {
            axis = Axis.Horizontal;
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when this element declares the content-relative height mode. One reader for the Host's creation-time
    /// contract, the engine's arrange and the engine's template walk, so the three cannot disagree about what
    /// <c>Height="MatchContent"</c> names.
    /// </summary>
    internal static bool IsMatchContentHeight(UiElementSpec spec)
    {
        return spec.TryGetAttribute("Height", out string raw)
            && string.Equals(raw.Trim(), MatchContentValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when a container's content height is a MAXIMUM over its children, which is the whole precondition
    /// for <see cref="MatchContentValue"/>: the reference must exist before the declaring child is measured, and
    /// it must not be built from that child.
    /// <para>
    /// A <c>Row</c> takes the tallest of its children and an <c>Overlay</c> does the same, so both can answer. A
    /// vertical stack (<c>Column</c>/<c>Stack</c>/<c>Section</c>/<c>Surface</c>/<c>Scroll</c>/<c>Clip</c>) sums
    /// its children - the declaring element included, which makes the reference self-referential - a
    /// <c>Wrap</c> discovers a line's membership from the children in that line, and a root has no parent at
    /// all. Those are refused where the element, the path and the reason are all still known.
    /// </para>
    /// </summary>
    internal static bool HasContentHeightReference(string? kind)
    {
        return string.Equals(kind, "Row", StringComparison.Ordinal)
            || string.Equals(kind, "Overlay", StringComparison.Ordinal);
    }

    private static void ValidateOffset(UiElementSpec spec, string path, string scope, string attribute, string raw)
    {
        string value = raw.Trim();
        bool percentage = value.EndsWith("%", StringComparison.Ordinal);
        string number = percentage ? value.Substring(0, value.Length - 1).Trim() : value;
        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            || float.IsNaN(parsed) || float.IsInfinity(parsed))
        {
            throw Contract(spec, path, scope,
                "Element id=\"" + spec.Id + "\" at '" + path + "' has invalid " + attribute + " '" + raw
                + "'; expected a number or a percentage (N%).");
        }

        if (percentage && (parsed < 0f || parsed > 100f))
        {
            throw Contract(spec, path, scope,
                "Element id=\"" + spec.Id + "\" at '" + path + "' has " + attribute + " '" + raw
                + "' outside the accepted 0..100% range; a ratio is a fraction of the parent's inner span.");
        }
    }

    private static bool TryAlignmentOf(string? raw, Axis axis, out Alignment alignment)
    {
        alignment = Alignment.Stretch;
        string value = (raw ?? "").Trim();
        if (value.Length == 0) return true;
        if (string.Equals(value, "Stretch", StringComparison.OrdinalIgnoreCase)) return true;

        if (axis == Axis.Horizontal)
        {
            if (string.Equals(value, "Left", StringComparison.OrdinalIgnoreCase))
            {
                alignment = new Alignment(0f, false);
                return true;
            }

            if (string.Equals(value, "Center", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Middle", StringComparison.OrdinalIgnoreCase))
            {
                alignment = new Alignment(0.5f, false);
                return true;
            }

            if (string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase))
            {
                alignment = new Alignment(1f, false);
                return true;
            }

            return false;
        }

        if (string.Equals(value, "Top", StringComparison.OrdinalIgnoreCase))
        {
            alignment = new Alignment(0f, false);
            return true;
        }

        if (string.Equals(value, "Center", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Middle", StringComparison.OrdinalIgnoreCase))
        {
            alignment = new Alignment(0.5f, false);
            return true;
        }

        if (string.Equals(value, "Bottom", StringComparison.OrdinalIgnoreCase))
        {
            alignment = new Alignment(1f, false);
            return true;
        }

        return false;
    }

    private static string AcceptedNames(Axis axis)
    {
        return axis == Axis.Horizontal ? "Left, Center, Right or Stretch" : "Top, Middle, Bottom or Stretch";
    }

    private static bool IsStretch(string raw)
    {
        return string.Equals(raw.Trim(), "Stretch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPercentage(string raw)
    {
        return raw.Trim().EndsWith("%", StringComparison.Ordinal);
    }

    private static bool TryNumericWidth(UiElementSpec spec, out float width)
    {
        width = 0f;
        return spec.TryGetAttribute("Width", out string raw)
            && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out width)
            && width > 0f;
    }

    private static bool IsFill(UiElementSpec spec)
    {
        return spec.TryGetAttribute("Fill", out string raw)
            && bool.TryParse(raw.Trim(), out bool fill)
            && fill;
    }

    private static UiContractException Contract(UiElementSpec spec, string path, string scope, string message)
    {
        return new UiContractException(message, scope, spec.Id, spec.Kind, path);
    }
}
