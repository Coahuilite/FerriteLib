using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Greenfield constrained layout engine. Produces a stable <see cref="UiLayoutSnapshot"/> with
/// page-local rects. Supports the Schema=2 container vocabulary: Stack, Row, Column, Wrap,
/// Section, Surface, Scroll, Clip and Overlay. Scroll/Clip/Overlay are structural containers and
/// are the only places in this engine that open/close native IMGUI scopes.
/// Containers with <c>Fill="true"</c> (and no explicit Height) size their viewport to the available
/// height passed down from the arrange call, so a root Scroll fills the window while its content
/// keeps its natural height.
/// </summary>
public sealed class UiLayoutEngine
{
    private sealed class PlacedEntry
    {
        internal UiElementSpec Spec = UiElementSpec.Empty;
        internal string Path = "";
        internal IUiWidget? Widget;
        internal bool IsContainer;
        internal string ContainerKind = "";
        internal float MeasureWidth;
        internal Rect Rect;
        internal Rect? ContentRect;
        internal int SubtreeCount;
    }

    private sealed class MeasuredBox
    {
        internal float Width;
        internal float Height;
        internal List<PlacedEntry> Entries = new();
    }

    private const float SectionTitleHeight = 22f;

    /// <summary>
    /// Height a tripped element keeps. Recovery must not resize the page: an element that collapses to
    /// zero moves every sibling, so a single failing control would still reshape the whole layout.
    /// </summary>
    private const float RecoveryBandHeight = 22f;
    // Reserved width for a vertical scrollbar drawn inside the right edge of a Scroll viewport.
    // Matches Verse.GenUI.ScrollBarWidth (16f), the convention the US pages already follow.
    private const float ScrollbarWidth = 16f;

    private readonly string scope;
    private readonly Dictionary<string, IUiWidget> widgetInstances = new(StringComparer.Ordinal);

    private UiLayoutSnapshot? cachedSnapshot;
    private Vector2 cachedAvailable;
    private int cachedContentRevision = -1;
    private int cachedDefinitionRevision;
    private int cachedTranslationRevision = int.MinValue;
    private List<PlacedEntry> lastEntries = new();

    public UiLayoutEngine(string scope)
    {
        this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public UiLayoutSnapshot ArrangeRoots(UiWidgetContext ctx, Vector2 available, IReadOnlyList<UiElementSpec> roots)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (roots == null) throw new ArgumentNullException(nameof(roots));

        float width = Normalize(available.x, 1f);
        float height = Normalize(available.y, 1f);

        if (cachedSnapshot != null
            && Math.Abs(cachedAvailable.x - width) < 0.01f
            && Math.Abs(cachedAvailable.y - height) < 0.01f
            && cachedContentRevision == ctx.Session.ContentRevision
            && cachedDefinitionRevision == DefinitionRevision(ctx)
            && cachedTranslationRevision == ctx.Translation.TranslationRevision)
        {
            ClampScrollPositions(ctx.Session, cachedSnapshot);
            return cachedSnapshot;
        }

        var box = new MeasuredBox { Width = width, Height = 0f };
        float y = 0f;
        foreach (UiElementSpec root in roots)
        {
            if (IsHidden(root, ctx)) continue;

            MeasuredBox child = MeasureElement(
                ctx,
                root,
                width,
                Math.Max(0f, height - y),
                root.Id.Length > 0 ? root.Id : root.Kind);
            child = OffsetBox(child, 0f, y);
            box.Entries.AddRange(child.Entries);
            y += child.Height;
        }

        box.Height = Math.Max(1f, y);
        lastEntries = box.Entries;

        var rects = new Dictionary<string, Rect>(StringComparer.Ordinal);
        var visible = new List<string>();
        var viewports = new Dictionary<string, Rect>(StringComparer.Ordinal);
        var scrollContents = new Dictionary<string, Rect>(StringComparer.Ordinal);

        foreach (PlacedEntry entry in box.Entries)
        {
            if (entry.Spec.Id.Length > 0)
            {
                rects[entry.Spec.Id] = entry.Rect;
            }

            visible.Add(entry.Path);

            if (string.Equals(entry.ContainerKind, "Scroll", StringComparison.Ordinal))
            {
                string key = ScrollKey(entry.Spec, entry.Path);
                viewports[key] = entry.Rect;
                if (entry.ContentRect.HasValue)
                {
                    scrollContents[key] = entry.ContentRect.Value;
                }
            }
        }

        cachedAvailable = new Vector2(width, height);
        cachedContentRevision = ctx.Session.ContentRevision;
        cachedDefinitionRevision = DefinitionRevision(ctx);
        cachedTranslationRevision = ctx.Translation.TranslationRevision;
        cachedSnapshot = new UiLayoutSnapshot(
            new Vector2(width, box.Height),
            rects,
            visible,
            viewports,
            scrollContents,
            cachedDefinitionRevision);

        ClampScrollPositions(ctx.Session, cachedSnapshot);
        return cachedSnapshot;
    }

    public void Draw(UiWidgetContext ctx, UiLayoutSnapshot snapshot, Rect viewport)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        DrawEntries(lastEntries, 0, lastEntries.Count, ctx, new Vector2(viewport.x, viewport.y), null, Vector2.zero);
    }

    private static void DrawEntries(
        List<PlacedEntry> entries,
        int start,
        int end,
        UiWidgetContext ctx,
        Vector2 viewportPosition,
        Vector2? nativeOrigin,
        Vector2 windowOrigin)
    {
        int index = start;
        while (index < end)
        {
            PlacedEntry entry = entries[index];
            Rect drawRect = ToDrawRect(entry.Rect, viewportPosition, nativeOrigin);
            UiWidgetContext entryCtx = entry.MeasureWidth > 0f
                ? ctx.WithViewWidth(entry.MeasureWidth).WithWindowOrigin(windowOrigin)
                : ctx.WithWindowOrigin(windowOrigin);

            if (IsScopedContainer(entry.ContainerKind))
            {
                DrawScopedContainer(
                    entry,
                    drawRect,
                    entryCtx,
                    entries,
                    index + 1,
                    index + entry.SubtreeCount,
                    viewportPosition,
                    nativeOrigin,
                    windowOrigin);
                index += entry.SubtreeCount;
            }
            else
            {
                // Announce the owning element so the text-fit audit can attribute a finding by path
                // without every widget threading its own identity through the drawing helpers.
                UiFitAudit.BeginElement(entry.Path);
                try
                {
                    if (entry.IsContainer)
                    {
                        DrawContainer(entry, drawRect, entryCtx);
                    }
                    else if (entry.Widget != null)
                    {
                        // Recovery is the tree's job, not each widget's. Before this the guard existed
                        // but nothing in the library called it: the documented "recovery lives in the
                        // per-widget guard" contract was consumer opt-in fiction, and a core widget that
                        // threw took the whole frame — and with it the shell's page — down with it. A
                        // tripped element now records into the session and paints a stable band, so one
                        // bad control cannot end the page. (FL→US round 1, item C.)
                        UiSessionGuard.DrawWidget(entry.Widget, drawRect, entryCtx, entry.Path);
                    }
                }
                finally
                {
                    UiFitAudit.EndElement();
                }

                index++;
            }
        }
    }

    private static void DrawScopedContainer(
        PlacedEntry entry,
        Rect outRect,
        UiWidgetContext ctx,
        List<PlacedEntry> entries,
        int childStart,
        int childEnd,
        Vector2 viewportPosition,
        Vector2? nativeOrigin,
        Vector2 windowOrigin)
    {
        string kind = entry.ContainerKind;

        if (string.Equals(kind, "Scroll", StringComparison.Ordinal))
        {
            string key = ScrollKey(entry.Spec, entry.Path);
            Vector2 scrollPosition = ctx.Session.GetScrollPosition(key);
            Rect contentRect = entry.ContentRect ?? new Rect(0f, 0f, Math.Max(1f, outRect.width), Math.Max(1f, outRect.height));
            scrollPosition = ClampScroll(scrollPosition, contentRect, outRect);

            // Children draw in scroll-content-local coordinates; the popup/overlay pass later
            // draws outside BeginScrollView, so anchors must be converted to Host window space.
            Vector2 scopeWindowOrigin = nativeOrigin.HasValue
                ? new Vector2(windowOrigin.x + outRect.x, windowOrigin.y + outRect.y)
                : outRect.position;
            Vector2 childWindowOrigin = new(
                scopeWindowOrigin.x - scrollPosition.x,
                scopeWindowOrigin.y - scrollPosition.y);

            try
            {
                VerseWidgets.BeginScrollView(outRect, ref scrollPosition, contentRect);
                DrawEntries(entries, childStart, childEnd, ctx, viewportPosition, entry.Rect.position, childWindowOrigin);
            }
            finally
            {
                ctx.Session.SetScrollPosition(key, scrollPosition);
                VerseWidgets.EndScrollView();
            }

            return;
        }

        // Clip and Overlay are structural groups. The engine owns Begin/EndGroup so leaf widgets
        // never create native structure scopes. Children draw relative to the group origin, so
        // their window-space offset is the group's draw position on top of the parent origin.
        Vector2 groupWindowOrigin = nativeOrigin.HasValue
            ? new Vector2(windowOrigin.x + outRect.x, windowOrigin.y + outRect.y)
            : outRect.position;
        try
        {
            GUI.BeginGroup(outRect);
            DrawEntries(entries, childStart, childEnd, ctx, viewportPosition, entry.Rect.position, groupWindowOrigin);
        }
        finally
        {
            GUI.EndGroup();
        }
    }

    private static Rect ToDrawRect(Rect rect, Vector2 viewportPosition, Vector2? nativeOrigin)
    {
        if (nativeOrigin.HasValue)
        {
            return new Rect(
                rect.x - nativeOrigin.Value.x,
                rect.y - nativeOrigin.Value.y,
                rect.width,
                rect.height);
        }

        return new Rect(
            viewportPosition.x + rect.x,
            viewportPosition.y + rect.y,
            rect.width,
            rect.height);
    }

    private static void ClampScrollPositions(UiSession session, UiLayoutSnapshot snapshot)
    {
        foreach (KeyValuePair<string, Rect> pair in snapshot.ScrollContents)
        {
            string key = pair.Key;
            if (!snapshot.Viewports.TryGetValue(key, out Rect viewport)) continue;

            session.SetScrollPosition(key, ClampScroll(session.GetScrollPosition(key), pair.Value, viewport));
        }
    }

    private static Vector2 ClampScroll(Vector2 position, Rect content, Rect viewport)
    {
        float maxX = Math.Max(0f, content.width - viewport.width);
        float maxY = Math.Max(0f, content.height - viewport.height);
        return new Vector2(ClampFloat(position.x, 0f, maxX), ClampFloat(position.y, 0f, maxY));
    }

    private static float ClampFloat(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return min;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static string ScrollKey(UiElementSpec spec, string path)
    {
        return spec.Id.Length > 0 ? spec.Id : path;
    }

    private MeasuredBox MeasureElement(UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight, string path)
    {
        string containerKind = GetContainerKind(spec);
        if (containerKind.Length == 0)
        {
            IUiWidget widget = GetOrCreateWidget(spec, path);
            // Measure must see the widget's arranged width, not the page width, so width-dependent
            // widgets (e.g. stacked narrow Mood rows) agree with the Draw pass, which already uses
            // entry.MeasureWidth via WithViewWidth.
            UiWidgetContext measureCtx = ctx.WithViewWidth(width);
            float height = ResolveHeight(spec, widget, measureCtx, width, path);
            var leaf = new PlacedEntry
            {
                Spec = spec,
                Path = path,
                Widget = widget,
                IsContainer = false,
                MeasureWidth = width,
                Rect = new Rect(0f, 0f, width, height),
                SubtreeCount = 1
            };
            var box = new MeasuredBox { Width = width, Height = height };
            box.Entries.Add(leaf);
            return box;
        }

        return MeasureContainer(ctx, spec, width, availableHeight, path);
    }

    private MeasuredBox MeasureContainer(UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight, string path)
    {
        string kind = GetContainerKind(spec);
        Padding padding = ParsePadding(spec);
        float gap = ReadGap(spec);
        float titleHeight = HasTitle(spec) ? SectionTitleHeight : 0f;
        float innerWidth = Math.Max(1f, width - padding.Left - padding.Right);
        float innerY = padding.Top + titleHeight;

        if (string.Equals(kind, "Row", StringComparison.Ordinal))
        {
            return MeasureRow(ctx, spec, kind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, path);
        }

        if (string.Equals(kind, "Wrap", StringComparison.Ordinal))
        {
            return MeasureWrap(ctx, spec, kind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, path);
        }

        if (string.Equals(kind, "Overlay", StringComparison.Ordinal))
        {
            return MeasureOverlay(ctx, spec, kind, width, padding, titleHeight, innerWidth, innerY, availableHeight, path);
        }

        if (string.Equals(kind, "Scroll", StringComparison.Ordinal))
        {
            return MeasureScroll(ctx, spec, kind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, path);
        }

        // Stack, Column, Section, Surface and Clip are vertical stacks. Clip additionally becomes
        // a structural group during Draw.
        return MeasureStack(ctx, spec, kind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, path);
    }

    private MeasuredBox MeasureStack(
        UiWidgetContext ctx,
        UiElementSpec spec,
        string kind,
        float width,
        Padding padding,
        float gap,
        float titleHeight,
        float innerWidth,
        float innerY,
        float availableHeight,
        string path)
    {
        var visible = new List<UiElementSpec>();
        foreach (UiElementSpec child in spec.Children)
        {
            if (!IsHidden(child, ctx)) visible.Add(child);
        }

        // Fill-aware vertical allocation. A pre-pass measures the flow height of every non-Fill
        // sibling (fixed and natural heights are content-determined), then the remaining inner
        // height after all fixed/natural siblings and gaps is shared by the Fill children. This
        // keeps a natural (non-Fill) vertical stack's height semantics unchanged while making a
        // Fill child absorb exactly the leftover space, including space needed by siblings that
        // come after it (e.g. a root Column whose body Row fills the window and whose footer must
        // stay visible at the bottom).
        float availableInner = Math.Max(0f, availableHeight - padding.Top - padding.Bottom - titleHeight);
        var flowHeights = new float[visible.Count];
        float nonFillTotal = 0f;
        int fillCount = 0;
        for (int i = 0; i < visible.Count; i++)
        {
            if (IsFlexibleFill(visible[i]))
            {
                fillCount++;
                flowHeights[i] = 0f;
                continue;
            }

            string childPath = path + "/" + (visible[i].Id.Length > 0 ? visible[i].Id : visible[i].Kind);
            float flow = MeasureElement(ctx, visible[i], innerWidth, availableInner, childPath).Height;
            flowHeights[i] = flow;
            nonFillTotal += flow;
        }

        float leftover = Math.Max(0f, availableInner - nonFillTotal - gap * Math.Max(0, visible.Count - 1));
        float fillShare = fillCount > 0 ? leftover / fillCount : 0f;

        var box = new MeasuredBox { Width = width, Height = 0f };
        float y = innerY;
        bool first = true;

        for (int i = 0; i < visible.Count; i++)
        {
            if (!first) y += gap;

            UiElementSpec child = visible[i];
            string childPath = path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind);
            float childAvailable;
            if (IsFlexibleFill(child))
            {
                childAvailable = fillShare;
            }
            else
            {
                float reservedAfter = gap * Math.Max(0, visible.Count - 1 - i);
                for (int j = i + 1; j < visible.Count; j++)
                {
                    reservedAfter += flowHeights[j];
                }

                childAvailable = Math.Max(0f, availableInner - (y - innerY) - reservedAfter);
            }

            MeasuredBox childBox = MeasureElement(ctx, child, innerWidth, childAvailable, childPath);
            childBox = OffsetBox(childBox, padding.Left, y);
            box.Entries.AddRange(childBox.Entries);
            y += childBox.Height;
            first = false;
        }

        float naturalHeight = padding.Top + titleHeight + Math.Max(0f, y - innerY) + padding.Bottom;
        float height = ResolveContainerHeight(spec, naturalHeight, availableHeight);
        box.Height = height;

        var containerEntry = new PlacedEntry
        {
            Spec = spec,
            Path = path,
            IsContainer = true,
            ContainerKind = kind,
            MeasureWidth = width,
            Rect = new Rect(0f, 0f, width, height)
        };
        box.Entries.Insert(0, containerEntry);
        containerEntry.SubtreeCount = box.Entries.Count;
        return box;
    }

    private MeasuredBox MeasureRow(
        UiWidgetContext ctx,
        UiElementSpec spec,
        string kind,
        float width,
        Padding padding,
        float gap,
        float titleHeight,
        float innerWidth,
        float innerY,
        float availableHeight,
        string path)
    {
        var visible = new List<UiElementSpec>();
        foreach (UiElementSpec child in spec.Children)
        {
            if (!IsHidden(child, ctx)) visible.Add(child);
        }

        float[] widths = ResolveColumnWidths(visible, innerWidth, gap);
        var box = new MeasuredBox { Width = width, Height = 0f };
        float x = padding.Left;
        float maxHeight = 0f;

        for (int i = 0; i < visible.Count; i++)
        {
            string childPath = path + "/" + (visible[i].Id.Length > 0 ? visible[i].Id : visible[i].Kind);
            MeasuredBox childBox = MeasureElement(ctx, visible[i], widths[i], availableHeight, childPath);
            childBox = OffsetBox(childBox, x, innerY);
            box.Entries.AddRange(childBox.Entries);
            maxHeight = Math.Max(maxHeight, childBox.Height);
            x += widths[i] + gap;
        }

        float naturalHeight = padding.Top + titleHeight + maxHeight + padding.Bottom;
        float height = ResolveContainerHeight(spec, naturalHeight, availableHeight);
        box.Height = height;

        var containerEntry = new PlacedEntry
        {
            Spec = spec,
            Path = path,
            IsContainer = true,
            ContainerKind = kind,
            MeasureWidth = width,
            Rect = new Rect(0f, 0f, width, height)
        };
        box.Entries.Insert(0, containerEntry);
        containerEntry.SubtreeCount = box.Entries.Count;
        return box;
    }

    private MeasuredBox MeasureWrap(
        UiWidgetContext ctx,
        UiElementSpec spec,
        string kind,
        float width,
        Padding padding,
        float gap,
        float titleHeight,
        float innerWidth,
        float innerY,
        float availableHeight,
        string path)
    {
        var box = new MeasuredBox { Width = width, Height = 0f };
        float x = padding.Left;
        float y = innerY;
        float lineHeight = 0f;
        bool firstInLine = true;

        foreach (UiElementSpec child in spec.Children)
        {
            if (IsHidden(child, ctx)) continue;

            float childWidth = ResolveWrapWidth(child, innerWidth);
            string childPath = path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind);

            if (!firstInLine && x + childWidth > padding.Left + innerWidth)
            {
                x = padding.Left;
                y += lineHeight + gap;
                lineHeight = 0f;
                firstInLine = true;
            }

            MeasuredBox childBox = MeasureElement(
                ctx,
                child,
                childWidth,
                Math.Max(0f, availableHeight - (y - innerY)),
                childPath);
            childBox = OffsetBox(childBox, x, y);
            box.Entries.AddRange(childBox.Entries);

            x += childWidth + gap;
            lineHeight = Math.Max(lineHeight, childBox.Height);
            firstInLine = false;
        }

        float contentHeight = Math.Max(0f, y + lineHeight - innerY);
        float naturalHeight = padding.Top + titleHeight + contentHeight + padding.Bottom;
        float height = ResolveContainerHeight(spec, naturalHeight, availableHeight);
        box.Height = height;

        var containerEntry = new PlacedEntry
        {
            Spec = spec,
            Path = path,
            IsContainer = true,
            ContainerKind = kind,
            MeasureWidth = width,
            Rect = new Rect(0f, 0f, width, height)
        };
        box.Entries.Insert(0, containerEntry);
        containerEntry.SubtreeCount = box.Entries.Count;
        return box;
    }

    private MeasuredBox MeasureOverlay(
        UiWidgetContext ctx,
        UiElementSpec spec,
        string kind,
        float width,
        Padding padding,
        float titleHeight,
        float innerWidth,
        float innerY,
        float availableHeight,
        string path)
    {
        var box = new MeasuredBox { Width = width, Height = 0f };
        float maxHeight = 0f;

        foreach (UiElementSpec child in spec.Children)
        {
            if (IsHidden(child, ctx)) continue;

            string childPath = path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind);
            MeasuredBox childBox = MeasureElement(ctx, child, innerWidth, availableHeight, childPath);
            childBox = OffsetBox(childBox, padding.Left, innerY);
            box.Entries.AddRange(childBox.Entries);
            maxHeight = Math.Max(maxHeight, childBox.Height);
        }

        float naturalHeight = padding.Top + titleHeight + maxHeight + padding.Bottom;
        float height = ResolveContainerHeight(spec, naturalHeight, availableHeight);
        box.Height = height;

        var containerEntry = new PlacedEntry
        {
            Spec = spec,
            Path = path,
            IsContainer = true,
            ContainerKind = kind,
            MeasureWidth = width,
            Rect = new Rect(0f, 0f, width, height)
        };
        box.Entries.Insert(0, containerEntry);
        containerEntry.SubtreeCount = box.Entries.Count;
        return box;
    }

    private MeasuredBox MeasureScroll(
        UiWidgetContext ctx,
        UiElementSpec spec,
        string kind,
        float width,
        Padding padding,
        float gap,
        float titleHeight,
        float innerWidth,
        float innerY,
        float availableHeight,
        string path)
    {
        var entries = new List<PlacedEntry>();
        float naturalContentHeight = MeasureScrollContent(
            ctx, spec, innerWidth, padding, gap, innerY, availableHeight, path, entries);

        float naturalHeight = padding.Top + titleHeight + naturalContentHeight + padding.Bottom;
        float viewportHeight = ResolveContainerHeight(spec, naturalHeight, availableHeight);

        // A vertical scrollbar is drawn inside the right edge of the viewport only when the
        // natural content height exceeds the viewport. Reserve its width then (and only then) so
        // content is measured and arranged against the actually visible area: pure vertical
        // overflow must never create a horizontal scrollbar, and a non-overflowing scroll keeps
        // the full viewport width. Children are re-measured at the reserved width so the arranged
        // rects, the published content rect and the BeginScrollView view rect all agree.
        float contentWidth = width;
        if (naturalContentHeight > viewportHeight + 0.01f)
        {
            contentWidth = Math.Max(1f, width - ScrollbarWidth);
            float reservedInnerWidth = Math.Max(1f, contentWidth - padding.Left - padding.Right);
            entries.Clear();
            float reflowedContentHeight = MeasureScrollContent(
                ctx, spec, reservedInnerWidth, padding, gap, innerY, availableHeight, path, entries);
            naturalHeight = padding.Top + titleHeight + reflowedContentHeight + padding.Bottom;
        }

        var box = new MeasuredBox { Width = width, Height = viewportHeight };
        var containerEntry = new PlacedEntry
        {
            Spec = spec,
            Path = path,
            IsContainer = true,
            ContainerKind = kind,
            MeasureWidth = width,
            Rect = new Rect(0f, 0f, width, viewportHeight),
            ContentRect = new Rect(0f, 0f, contentWidth, naturalHeight)
        };
        box.Entries.Add(containerEntry);
        box.Entries.AddRange(entries);
        containerEntry.SubtreeCount = box.Entries.Count;
        return box;
    }

    /// <summary>
    /// Measures the scroll children at <paramref name="contentInnerWidth"/> into
    /// <paramref name="entries"/> and returns the resulting natural content height
    /// (scroll padding and title excluded).
    /// </summary>
    private float MeasureScrollContent(
        UiWidgetContext ctx,
        UiElementSpec spec,
        float contentInnerWidth,
        Padding padding,
        float gap,
        float innerY,
        float availableHeight,
        string path,
        List<PlacedEntry> entries)
    {
        float y = innerY;
        bool first = true;

        foreach (UiElementSpec child in spec.Children)
        {
            if (IsHidden(child, ctx)) continue;

            if (!first) y += gap;
            string childPath = path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind);
            MeasuredBox childBox = MeasureElement(
                ctx,
                child,
                contentInnerWidth,
                Math.Max(0f, availableHeight - (y - innerY)),
                childPath);
            childBox = OffsetBox(childBox, padding.Left, y);
            entries.AddRange(childBox.Entries);
            y += childBox.Height;
            first = false;
        }

        return Math.Max(0f, y - innerY);
    }

    private IUiWidget GetOrCreateWidget(UiElementSpec spec, string path)
    {
        if (widgetInstances.TryGetValue(path, out IUiWidget? widget))
        {
            return widget;
        }

        widget = UiWidgetRegistry.Resolve(scope, spec.Kind);
        widget.Configure(spec);
        widgetInstances.Add(path, widget);
        return widget;
    }

    private static MeasuredBox OffsetBox(MeasuredBox box, float x, float y)
    {
        var result = new MeasuredBox { Width = box.Width, Height = box.Height };
        foreach (PlacedEntry entry in box.Entries)
        {
            var copy = new PlacedEntry
            {
                Spec = entry.Spec,
                Path = entry.Path,
                Widget = entry.Widget,
                IsContainer = entry.IsContainer,
                ContainerKind = entry.ContainerKind,
                MeasureWidth = entry.MeasureWidth,
                Rect = new Rect(entry.Rect.x + x, entry.Rect.y + y, entry.Rect.width, entry.Rect.height),
                ContentRect = entry.ContentRect,
                SubtreeCount = entry.SubtreeCount
            };
            result.Entries.Add(copy);
        }

        return result;
    }

    private static string GetContainerKind(UiElementSpec spec)
    {
        if (string.Equals(spec.Kind, "Stack", StringComparison.Ordinal)
            || string.Equals(spec.Kind, "Column", StringComparison.Ordinal)
            || string.Equals(spec.Kind, "Section", StringComparison.Ordinal)
            || string.Equals(spec.Kind, "Surface", StringComparison.Ordinal)
            || string.Equals(spec.Kind, "Scroll", StringComparison.Ordinal)
            || string.Equals(spec.Kind, "Clip", StringComparison.Ordinal)
            || string.Equals(spec.Kind, "Overlay", StringComparison.Ordinal))
        {
            return spec.Kind;
        }

        if (string.Equals(spec.Kind, "Row", StringComparison.Ordinal))
        {
            return "Row";
        }

        if (string.Equals(spec.Kind, "Wrap", StringComparison.Ordinal))
        {
            return "Wrap";
        }

        return "";
    }

    private static bool IsScopedContainer(string kind)
    {
        return string.Equals(kind, "Scroll", StringComparison.Ordinal)
            || string.Equals(kind, "Clip", StringComparison.Ordinal)
            || string.Equals(kind, "Overlay", StringComparison.Ordinal);
    }

    private static float ResolveWrapWidth(UiElementSpec spec, float innerWidth)
    {
        if (spec.TryGetAttribute("Width", out string raw)
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedWidth)
            && fixedWidth > 0f)
        {
            return fixedWidth;
        }

        return Math.Max(1f, innerWidth);
    }

    private static float ResolveHeight(UiElementSpec spec, IUiWidget widget, UiWidgetContext ctx, float width, string path)
    {
        if (spec.TryGetAttribute("Height", out string raw))
        {
            string value = raw.Trim();
            if (value.Length == 0 || string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return MeasuredHeight(widget, ctx, path);
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedHeight))
            {
                return Math.Max(0f, fixedHeight);
            }

            throw new FormatException(
                $"Widget id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Height '{raw}'; expected a number or Auto.");
        }

        return MeasuredHeight(widget, ctx, path);
    }

    /// <summary>
    /// One widget's measured height, recovered through the session if the widget throws. The guard is
    /// here rather than in each widget because a measurement that dies takes the whole arrange pass
    /// with it, and the tree — not the control — owns whether the page survives that. The fallback keeps
    /// the element's slot instead of collapsing it, so a trip cannot reshuffle the rest of the page.
    /// </summary>
    private static float MeasuredHeight(IUiWidget widget, UiWidgetContext ctx, string path)
    {
        // The guard is here rather than in each widget because a measurement that dies takes the whole
        // arrange pass with it, and the tree — not the control — owns whether the page survives that.
        // The fallback keeps the element's slot instead of collapsing it, so a trip cannot reshuffle
        // the rest of the page.
        return Math.Max(0f, UiSessionGuard.MeasureWidget(widget, ctx, path, RecoveryBandHeight));
    }

    private static float ResolveContainerHeight(UiElementSpec spec, float naturalHeight, float availableHeight)
    {
        if (spec.TryGetAttribute("Height", out string raw) && raw.Trim().Length > 0
            && !string.Equals(raw.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
        {
            if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedHeight))
            {
                return Math.Max(0f, fixedHeight);
            }

            throw new FormatException(
                $"Container id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Height '{raw}'; expected a number or Auto.");
        }

        if (IsFill(spec) && availableHeight > 0f)
        {
            return availableHeight;
        }

        return Math.Max(0f, naturalHeight);
    }

    private static bool IsFill(UiElementSpec spec)
    {
        return spec.TryGetAttribute("Fill", out string raw)
            && bool.TryParse(raw.Trim(), out bool fill)
            && fill;
    }

    /// <summary>
    /// True when a container child of a vertical stack is a real flex slot: it is a container,
    /// declares Fill="true" and has no explicit fixed Height. Such children share the stack's
    /// remaining inner height instead of contributing to the natural flow.
    /// </summary>
    private static bool IsFlexibleFill(UiElementSpec spec)
    {
        if (GetContainerKind(spec).Length == 0 || !IsFill(spec)) return false;
        if (spec.TryGetAttribute("Height", out string raw))
        {
            string value = raw.Trim();
            if (value.Length > 0 && !string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasTitle(UiElementSpec spec)
    {
        return spec.TryGetAttribute("Title", out string raw) && raw.Trim().Length > 0
            || spec.TryGetAttribute("TitleKey", out string key) && key.Trim().Length > 0;
    }

    private static float[] ResolveColumnWidths(IReadOnlyList<UiElementSpec> children, float innerWidth, float gap)
    {
        var widths = new float[children.Count];
        float fixedSum = 0f;
        int autoCount = 0;

        for (int i = 0; i < children.Count; i++)
        {
            if (children[i].TryGetAttribute("Width", out string raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedWidth)
                && fixedWidth > 0f)
            {
                widths[i] = fixedWidth;
                fixedSum += fixedWidth;
            }
            else
            {
                widths[i] = 0f;
                autoCount++;
            }
        }

        float totalGap = gap * Math.Max(0, children.Count - 1);
        float remaining = Math.Max(0f, innerWidth - totalGap - fixedSum);
        float autoWidth = autoCount > 0 ? Math.Max(1f, remaining / autoCount) : 0f;

        for (int i = 0; i < widths.Length; i++)
        {
            if (widths[i] <= 0f) widths[i] = autoWidth;
        }

        return widths;
    }

    private static Padding ParsePadding(UiElementSpec spec)
    {
        if (!spec.TryGetAttribute("Padding", out string raw))
        {
            return Padding.Zero;
        }

        string[] parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 1 && parts.Length != 2 && parts.Length != 4)
        {
            throw new FormatException(
                $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Padding '{raw}'.");
        }

        var values = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                throw new FormatException(
                    $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Padding value '{parts[i].Trim()}'.");
            }
        }

        return parts.Length switch
        {
            1 => new Padding(values[0], values[0], values[0], values[0]),
            2 => new Padding(values[0], values[1], values[0], values[1]),
            _ => new Padding(values[0], values[1], values[2], values[3]),
        };
    }

    private static float ReadGap(UiElementSpec spec)
    {
        if (!spec.TryGetAttribute("Gap", out string raw) || raw.Trim().Length == 0)
        {
            return 0f;
        }

        if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float gap) && gap >= 0f)
        {
            return gap;
        }

        throw new FormatException(
            $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Gap '{raw}'.");
    }

    private static bool IsHidden(UiElementSpec spec, UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("Hidden", out string raw))
        {
            string value = raw.Trim();
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (!spec.TryGetAttribute("Tab", out string tab) || tab.Trim().Length == 0)
        {
            return false;
        }

        string activeTab = ctx.Bindings.TryGet(UiBindings.ActiveTabKey, out string current) ? current : "";
        return !string.Equals(tab.Trim(), activeTab, StringComparison.OrdinalIgnoreCase);
    }

    private static int DefinitionRevision(UiWidgetContext ctx)
    {
        // The host can bump this later; for now the layout definition is immutable per engine.
        return 0;
    }

    private static float Normalize(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 1f) return fallback;
        return value;
    }

    private static void DrawContainer(PlacedEntry entry, Rect rect, UiWidgetContext ctx)
    {
        string kind = entry.ContainerKind;
        bool drawSurface = string.Equals(kind, "Section", StringComparison.Ordinal)
            || string.Equals(kind, "Surface", StringComparison.Ordinal);

        if (drawSurface)
        {
            // The two container surfaces are exactly the theme's Panel/Base treatments; routing them
            // through UiThemeDraw keeps one border rule instead of a hand-copied five-rect version.
            if (string.Equals(kind, "Section", StringComparison.Ordinal))
            {
                UiThemeDraw.Panel(rect, ctx.Theme);
            }
            else
            {
                UiThemeDraw.Base(rect, ctx.Theme);
            }
        }

        if (HasTitle(entry.Spec))
        {
            Padding padding = ParsePadding(entry.Spec);
            float innerWidth = Math.Max(1f, rect.width - padding.Left - padding.Right);
            var headerRect = new Rect(rect.x + padding.Left, rect.y + padding.Top, innerWidth, SectionTitleHeight);
            // One text outlet: routing the container title through UiThemeDraw.Label is what makes it
            // visible to the fit audit (BeginElement above already attributes by path), which the
            // engine's own private Label copy could never do.
            UiThemeDraw.Label(headerRect, ReadTitle(entry.Spec, ctx), ctx.Theme, ctx.Theme.TextPrimary);
        }
    }


    private static string ReadTitle(UiElementSpec spec, UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("TitleKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Title", out string title) ? title : "";
    }

    private readonly struct Padding
    {
        internal readonly float Top;
        internal readonly float Right;
        internal readonly float Bottom;
        internal readonly float Left;

        internal Padding(float top, float right, float bottom, float left)
        {
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
        }

        internal static readonly Padding Zero = new(0f, 0f, 0f, 0f);
    }
}
