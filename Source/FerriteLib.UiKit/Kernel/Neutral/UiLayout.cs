using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
/// <summary>A bounded, located fact from the successful arrangement, not a business mutation.</summary>
public readonly struct UiLayoutDiagnostic
{
    public UiLayoutDiagnostic(string path, string code, string message)
    {
        Path = path;
        Code = code;
        Message = message;
    }

    public string Path { get; }
    public string Code { get; }
    public string Message { get; }
}

public readonly struct UiLayoutSize
{
    public UiLayoutSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public float Width { get; }
    public float Height { get; }
}

public readonly struct UiLayoutPoint
{
    public UiLayoutPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }
    public float Y { get; }
}

public readonly struct UiLayoutRect
{
    public UiLayoutRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
}

public readonly struct UiLayoutHeight
{
    public UiLayoutHeight(float? fixedHeight, bool fill)
    {
        Fixed = fixedHeight;
        Fill = fill;
    }

    public float? Fixed { get; }
    public bool Fill { get; }

    public float Resolve(float natural, float available) => Fixed.HasValue ? Math.Max(0, Fixed.Value) : Fill && available > 0 ? available : Math.Max(0, natural);
}

public readonly struct UiLayoutFrame
{
    public UiLayoutFrame(float width, float availableHeight, float left, float top, float right, float bottom, float title, float gap, UiLayoutHeight height)
    {
        Width = width;
        AvailableHeight = availableHeight;
        Left = left;
        Top = top + title;
        Right = right;
        Bottom = bottom;
        Gap = gap;
        Height = height;
    }

    public float Width { get; }
    public float AvailableHeight { get; }
    public float Left { get; }
    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }
    public float Gap { get; }
    public UiLayoutHeight Height { get; }
    public float InnerWidth => Math.Max(1, Width - Left - Right);
    public float AvailableInnerHeight => Math.Max(0, AvailableHeight - Top - Bottom);
}

public readonly struct UiLayoutChild
{
    public UiLayoutChild(float width, bool fill = false, float alignX = 0, float alignY = 0, UiLayoutOffset offsetX = default, UiLayoutOffset offsetY = default, bool matchContentHeight = false)
    {
        Width = width;
        Fill = fill;
        AlignX = alignX;
        AlignY = alignY;
        OffsetX = offsetX;
        OffsetY = offsetY;
        MatchContentHeight = matchContentHeight;
    }

    public float Width { get; }
    public bool Fill { get; }
    public float AlignX { get; }
    public float AlignY { get; }
    public UiLayoutOffset OffsetX { get; }
    public UiLayoutOffset OffsetY { get; }
    public bool MatchContentHeight { get; }
}

public readonly struct UiLayoutOffset
{
    public UiLayoutOffset(float pixels, float fraction = 0)
    {
        Pixels = pixels;
        Fraction = fraction;
    }

    public float Pixels { get; }
    public float Fraction { get; }

    public float At(float span) => Pixels + Fraction * span;
}

public sealed class UiLayoutPlan
{
    internal UiLayoutPlan(UiLayoutSize size, UiLayoutRect[] slots, float naturalHeight)
    {
        Size = size;
        Slots = Array.AsReadOnly(slots);
        NaturalHeight = naturalHeight;
    }

    public UiLayoutSize Size { get; }
    public IReadOnlyList<UiLayoutRect> Slots { get; }
    public float NaturalHeight { get; }
    public bool HeightOverflow => NaturalHeight > Size.Height + .01f;
}

/// <summary>The same width-dependent measurement service is used by native and nonrendering layout.</summary>
public delegate UiLayoutSize UiLayoutMeasure(int index, float width, float availableHeight, float? contentHeightReference);
public enum UiWidthMode
{
    Fixed,
    Auto,
    Flexible
}

public readonly struct UiWidthDemand
{
    public UiWidthDemand(UiWidthMode mode, float natural, float minimum = 1, float maximum = float.MaxValue)
    {
        Mode = mode;
        Natural = natural;
        Minimum = minimum;
        Maximum = maximum;
    }

    public UiWidthMode Mode { get; }
    public float Natural { get; }
    public float Minimum { get; }
    public float Maximum { get; }
}

public sealed class UiWidthPlan
{
    internal UiWidthPlan(float[] widths, float required, float available)
    {
        Widths = Array.AsReadOnly(widths);
        Required = required;
        Available = available;
    }

    public IReadOnlyList<float> Widths { get; }
    public float Required { get; }
    public float Available { get; }
    public bool Overflow => Required > Available + .01f;
}

/// <summary>An axis-local reference to the parent (-1) or a direct sibling index.</summary>
public readonly struct UiLayoutAnchor
{
    public UiLayoutAnchor(int targetIndex, float targetFraction, float ownFraction, UiLayoutOffset offset)
    {
        TargetIndex = targetIndex;
        TargetFraction = targetFraction;
        OwnFraction = ownFraction;
        Offset = offset;
    }

    public int TargetIndex { get; }
    public float TargetFraction { get; }
    public float OwnFraction { get; }
    public UiLayoutOffset Offset { get; }
}

/// <summary>Production flow/placement rules. No declaration parsing, business state or platform geometry.</summary>
public static class UiLayout
{
    public static float SharedLabelColumn(IReadOnlyList<float> demands)
    {
        float width = 0;
        foreach (float demand in demands)
        {
            if (float.IsNaN(demand) || float.IsInfinity(demand) || demand < 0)
                throw new ArgumentException("Label demand must be finite and nonnegative.");
            width = Math.Max(width, demand);
        }

        return width;
    }

    /// <summary>Resolves only same-parent, axis-local position dependencies; measurement is already complete.</summary>
    public static UiLayoutPlan Anchored(UiLayoutPlan plan, UiLayoutFrame frame, IReadOnlyList<UiLayoutAnchor?> horizontal, IReadOnlyList<UiLayoutAnchor?> vertical)
    {
        if (horizontal.Count != plan.Slots.Count || vertical.Count != plan.Slots.Count)
            throw new ArgumentException("Anchor count must match the direct children.");
        var slots = new UiLayoutRect[plan.Slots.Count];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = plan.Slots[i];
        ResolveAnchors(slots, horizontal, frame.Left, frame.InnerWidth, true);
        float innerHeight = Math.Max(0, plan.Size.Height - frame.Top - frame.Bottom);
        ResolveAnchors(slots, vertical, frame.Top, innerHeight, false);
        return new UiLayoutPlan(plan.Size, slots, plan.NaturalHeight);
    }

    private static void ResolveAnchors(UiLayoutRect[] slots, IReadOnlyList<UiLayoutAnchor?> anchors, float parentOrigin, float parentSpan, bool horizontal)
    {
        var resolved = new bool[slots.Length];
        int pending = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            resolved[i] = !anchors[i].HasValue;
            if (!resolved[i])
                pending++;
        }

        while (pending > 0)
        {
            bool progressed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (resolved[i])
                    continue;
                UiLayoutAnchor anchor = anchors[i]!.Value;
                int target = anchor.TargetIndex;
                if (target < -1 || target >= slots.Length)
                    throw new ArgumentException("Anchor target is not a direct sibling: child " + i + ".");
                if (target >= 0 && !resolved[target])
                    continue;
                UiLayoutRect self = slots[i];
                float origin = target < 0 ? parentOrigin : horizontal ? slots[target].X : slots[target].Y;
                float span = target < 0 ? parentSpan : horizontal ? slots[target].Width : slots[target].Height;
                float ownSpan = horizontal ? self.Width : self.Height;
                float position = origin + span * anchor.TargetFraction - ownSpan * anchor.OwnFraction + anchor.Offset.At(parentSpan);
                if (float.IsNaN(position) || float.IsInfinity(position))
                    throw new ArgumentException("Anchor position is not finite: child " + i + ".");
                slots[i] = horizontal ? new UiLayoutRect(position, self.Y, self.Width, self.Height) : new UiLayoutRect(self.X, position, self.Width, self.Height);
                resolved[i] = true;
                pending--;
                progressed = true;
            }

            if (!progressed)
                throw new ArgumentException("Cyclic same-parent " + (horizontal ? "horizontal" : "vertical") + " anchors.");
        }
    }

    public static UiWidthPlan Columns(IReadOnlyList<UiWidthDemand> demands, float width, float gap)
    {
        var widths = new float[demands.Count];
        float fixedSum = 0;
        float automaticNatural = 0;
        var natural = new float[demands.Count];
        var pending = new bool[demands.Count];
        float flexMin = 0;
        int flexCount = 0;
        for (int i = 0; i < demands.Count; i++)
        {
            UiWidthDemand item = demands[i];
            if (item.Mode == UiWidthMode.Fixed)
            {
                widths[i] = Clamp(item.Natural, item.Minimum, item.Maximum);
                fixedSum += widths[i];
            }
            else if (item.Mode == UiWidthMode.Auto)
            {
                natural[i] = Clamp(item.Natural, item.Minimum, item.Maximum);
                automaticNatural += natural[i];
                pending[i] = true;
            }
            else
            {
                flexMin += item.Minimum;
                flexCount++;
            }
        }

        float gaps = gap * Math.Max(0, demands.Count - 1);
        float budget = Math.Max(0, width - gaps - fixedSum - flexMin);
        float remainingNatural = automaticNatural;
        float autoSum = 0;
        while (remainingNatural > 0)
        {
            float scale = Math.Min(1, Math.Max(0, budget / remainingNatural));
            bool constrained = false;
            for (int i = 0; i < demands.Count; i++)
            {
                if (!pending[i] || natural[i] * scale >= demands[i].Minimum)
                    continue;
                widths[i] = demands[i].Minimum;
                budget -= widths[i];
                remainingNatural -= natural[i];
                autoSum += widths[i];
                pending[i] = false;
                constrained = true;
            }

            if (constrained)
                continue;
            for (int i = 0; i < demands.Count; i++)
            {
                if (!pending[i])
                    continue;
                widths[i] = natural[i] * scale;
                autoSum += widths[i];
            }

            break;
        }

        float share = flexCount > 0 ? Math.Max(0, width - gaps - fixedSum - autoSum) / flexCount : 0;
        float required = gaps;
        for (int i = 0; i < demands.Count; i++)
        {
            UiWidthDemand item = demands[i];
            if (item.Mode == UiWidthMode.Flexible)
                widths[i] = Clamp(share, item.Minimum, item.Maximum);
            required += widths[i];
        }

        return new UiWidthPlan(widths, required, width);
    }

    public static float ScrollContentWidth(float width, float naturalHeight, float viewportHeight, float scrollbarWidth) => naturalHeight > viewportHeight + .01f ? Math.Max(1, width - scrollbarWidth) : width;
    public static float ViewportRows(IReadOnlyList<UiLayoutRect> rows, float count, float bottomPadding, float naturalHeight)
    {
        if (count <= 0 || float.IsNaN(count) || float.IsInfinity(count))
            throw new ArgumentOutOfRangeException(nameof(count));
        if (rows.Count == 0 || count >= rows.Count)
            return naturalHeight;
        int full = (int)Math.Floor(count);
        float fraction = count - full;
        float bottom = fraction > 0 ? rows[full].Y + fraction * rows[full].Height : rows[full - 1].Y + rows[full - 1].Height;
        return Math.Min(naturalHeight, bottom + bottomPadding);
    }

    public static UiLayoutPoint ClampScroll(UiLayoutPoint position, UiLayoutSize content, UiLayoutSize viewport) => new(Clamp(position.X, 0, Math.Max(0, content.Width - viewport.Width)), Clamp(position.Y, 0, Math.Max(0, content.Height - viewport.Height)));
    public static float Clamp(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return min;
        return Math.Max(min, Math.Min(max, value));
    }

    public static float Origin(float origin, float span, float self, float fraction, float offset) => origin + fraction * (span - self) + offset;
    public static UiLayoutPlan Stack(UiLayoutFrame frame, IReadOnlyList<UiLayoutChild> children, UiLayoutMeasure measure)
    {
        var flow = new float[children.Count];
        float nonFill = 0;
        int fills = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].Fill)
            {
                fills++;
                continue;
            }

            flow[i] = measure(i, children[i].Width, frame.AvailableInnerHeight, null).Height;
            nonFill += flow[i];
        }

        float leftover = Math.Max(0, frame.AvailableInnerHeight - nonFill - frame.Gap * Math.Max(0, children.Count - 1));
        float share = fills == 0 ? 0 : leftover / fills;
        var slots = new UiLayoutRect[children.Count];
        float y = frame.Top;
        for (int i = 0; i < children.Count; i++)
        {
            if (i > 0)
                y += frame.Gap;
            float reservedAfter = frame.Gap * Math.Max(0, children.Count - 1 - i);
            for (int j = i + 1; j < children.Count; j++)
                reservedAfter += flow[j];
            float available = children[i].Fill ? share : Math.Max(0, frame.AvailableInnerHeight - (y - frame.Top) - reservedAfter);
            UiLayoutSize size = measure(i, children[i].Width, available, null);
            float x = Origin(frame.Left, frame.InnerWidth, size.Width, children[i].AlignX, children[i].OffsetX.At(frame.InnerWidth));
            slots[i] = new UiLayoutRect(x, y, size.Width, size.Height);
            y += size.Height;
        }

        return Plan(frame, slots, frame.Top + Math.Max(0, y - frame.Top) + frame.Bottom);
    }

    public static UiLayoutPlan VerticalContent(UiLayoutFrame frame, IReadOnlyList<UiLayoutChild> children, UiLayoutMeasure measure)
    {
        var slots = new UiLayoutRect[children.Count];
        float y = frame.Top;
        for (int i = 0; i < children.Count; i++)
        {
            if (i > 0)
                y += frame.Gap;
            UiLayoutSize size = measure(i, children[i].Width, Math.Max(0, frame.AvailableHeight - (y - frame.Top)), null);
            float x = Origin(frame.Left, frame.InnerWidth, size.Width, children[i].AlignX, children[i].OffsetX.At(frame.InnerWidth));
            slots[i] = new UiLayoutRect(x, y, size.Width, size.Height);
            y += size.Height;
        }

        return Plan(frame, slots, frame.Top + Math.Max(0, y - frame.Top) + frame.Bottom);
    }

    public static UiLayoutPlan Row(UiLayoutFrame frame, IReadOnlyList<UiLayoutChild> children, UiLayoutMeasure measure)
    {
        UiLayoutSize[] sizes = MeasureCrossGroup(frame, children, measure, out float maximum);
        float natural = frame.Top + maximum + frame.Bottom;
        float height = frame.Height.Resolve(natural, frame.AvailableHeight);
        float innerHeight = Math.Max(0, height - frame.Top - frame.Bottom);
        var slots = new UiLayoutRect[children.Count];
        float x = frame.Left;
        for (int i = 0; i < children.Count; i++)
        {
            float y = Origin(frame.Top, innerHeight, sizes[i].Height, children[i].AlignY, children[i].OffsetY.At(innerHeight));
            slots[i] = new UiLayoutRect(x, y, sizes[i].Width, sizes[i].Height);
            x += children[i].Width + frame.Gap;
        }

        return new UiLayoutPlan(new UiLayoutSize(frame.Width, height), slots, natural);
    }

    public static UiLayoutPlan Overlay(UiLayoutFrame frame, IReadOnlyList<UiLayoutChild> children, UiLayoutMeasure measure)
    {
        UiLayoutSize[] sizes = MeasureCrossGroup(frame, children, measure, out float maximum);
        float natural = frame.Top + maximum + frame.Bottom;
        float height = frame.Height.Resolve(natural, frame.AvailableHeight);
        float innerHeight = Math.Max(0, height - frame.Top - frame.Bottom);
        var slots = new UiLayoutRect[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            float x = Origin(frame.Left, frame.InnerWidth, sizes[i].Width, children[i].AlignX, children[i].OffsetX.At(frame.InnerWidth));
            float y = Origin(frame.Top, innerHeight, sizes[i].Height, children[i].AlignY, children[i].OffsetY.At(innerHeight));
            slots[i] = new UiLayoutRect(x, y, sizes[i].Width, sizes[i].Height);
        }

        return new UiLayoutPlan(new UiLayoutSize(frame.Width, height), slots, natural);
    }

    public static UiLayoutPlan Wrap(UiLayoutFrame frame, IReadOnlyList<UiLayoutChild> children, int columns, UiLayoutMeasure measure)
    {
        var slots = new UiLayoutRect[children.Count];
        float x = frame.Left;
        float y = frame.Top;
        float lineHeight = 0;
        int start = 0;
        for (int i = 0; i < children.Count; i++)
        {
            bool full = columns > 0 ? i - start >= columns : x + children[i].Width > frame.Left + frame.InnerWidth;
            if (i > start && full)
            {
                AlignLine(slots, children, start, i, y, lineHeight);
                x = frame.Left;
                y += lineHeight + frame.Gap;
                lineHeight = 0;
                start = i;
            }

            UiLayoutSize size = measure(i, children[i].Width, Math.Max(0, frame.AvailableHeight - (y - frame.Top)), null);
            slots[i] = new UiLayoutRect(x, y, size.Width, size.Height);
            x += children[i].Width + frame.Gap;
            lineHeight = Math.Max(lineHeight, size.Height);
        }

        AlignLine(slots, children, start, children.Count, y, lineHeight);
        return Plan(frame, slots, frame.Top + Math.Max(0, y + lineHeight - frame.Top) + frame.Bottom);
    }

    private static void AlignLine(UiLayoutRect[] slots, IReadOnlyList<UiLayoutChild> children, int start, int end, float top, float height)
    {
        for (int i = start; i < end; i++)
        {
            UiLayoutRect item = slots[i];
            float y = Origin(top, height, item.Height, children[i].AlignY, children[i].OffsetY.At(height));
            slots[i] = new UiLayoutRect(item.X, y, item.Width, item.Height);
        }
    }

    private static UiLayoutSize[] MeasureCrossGroup(UiLayoutFrame frame, IReadOnlyList<UiLayoutChild> children, UiLayoutMeasure measure, out float maximum)
    {
        var sizes = new UiLayoutSize[children.Count];
        maximum = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].MatchContentHeight)
                continue;
            sizes[i] = measure(i, children[i].Width, frame.AvailableHeight, null);
            maximum = Math.Max(maximum, sizes[i].Height);
        }

        for (int i = 0; i < children.Count; i++)
            if (children[i].MatchContentHeight)
                sizes[i] = measure(i, children[i].Width, frame.AvailableHeight, maximum);
        return sizes;
    }

    private static UiLayoutPlan Plan(UiLayoutFrame frame, UiLayoutRect[] slots, float natural) => new UiLayoutPlan(new UiLayoutSize(frame.Width, frame.Height.Resolve(natural, frame.AvailableHeight)), slots, natural);
}
