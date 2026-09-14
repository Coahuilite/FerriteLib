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

        /// <summary>
        /// This element's stable identity, built once during arrange and reused by the draw pass, so
        /// Measure and Draw agree about which element they are on (0.4.0 identity layer). Its
        /// <see cref="UiNodeId.Path"/> is the entry's arranged display path; the identity itself is
        /// <see cref="UiNodeId.Key"/>, which is what the session keys nodes and state by.
        /// </summary>
        internal UiNodeId Id;

        /// <summary>The element's node: identity, state and dirty flag, owned by the session.</summary>
        internal UiNode Node = null!;

        /// <summary>
        /// The style chain this entry resolved in, recorded during arrange so the draw pass re-applies the
        /// same scope instead of rebuilding it: measure and draw must hand the widget one theme, and the
        /// chain is what that theme is derived from.
        /// </summary>
        internal IReadOnlyList<UiStyleDeclaration>? StyleChain;

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

    // The document resolver is the engine's answer to "what does this scope look like": null means the
    // tree has no style document at all, in which case every element draws with the injected theme.
    private readonly UiStyleResolver? styleResolver;

    // Widget instances are keyed by element identity, not by path text: two unnamed same-kind siblings
    // used to collide here, so the second one drew with the first one's spec.
    private readonly Dictionary<UiNodeId, IUiWidget> widgetInstances = new();

    private UiLayoutSnapshot? cachedSnapshot;
    private Vector2 cachedAvailable;
    private int cachedContentRevision = -1;
    private int cachedDefinitionRevision;
    private int cachedTranslationRevision = int.MinValue;
    private List<PlacedEntry> lastEntries = new();

    /// <summary>
    /// The engine a <see cref="UiHost"/> builds: <paramref name="styleResolver"/> turns the chain each
    /// element declares into the theme that element draws with. Null keeps the pre-document behaviour,
    /// where the injected theme is the only theme.
    /// </summary>
    public UiLayoutEngine(string scope, UiStyleResolver? styleResolver = null)
    {
        this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        this.styleResolver = styleResolver;
    }

    public UiLayoutSnapshot ArrangeRoots(UiWidgetContext ctx, Vector2 available, IReadOnlyList<UiElementSpec> roots)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (roots == null) throw new ArgumentNullException(nameof(roots));

        float width = Normalize(available.x, 1f);
        float height = Normalize(available.y, 1f);

        // A dirty node is the per-element invalidation signal: a widget that marked its own node dirty
        // asks for a re-measure even when size, content revision, definition and language all stand still.
        if (cachedSnapshot != null
            && !ctx.Session.HasDirtyNodes
            && Math.Abs(cachedAvailable.x - width) < 0.01f
            && Math.Abs(cachedAvailable.y - height) < 0.01f
            && cachedContentRevision == ctx.Session.ContentRevision
            && cachedDefinitionRevision == DefinitionRevision(ctx)
            && cachedTranslationRevision == ctx.Translation.TranslationRevision)
        {
            ClampScrollPositions(ctx.Session, lastEntries);
            ApplyScrollTarget(ctx);
            return cachedSnapshot;
        }

        // A fresh arrange: every node forgets its children and geometry, and the entries below publish
        // them again for the elements this pass visits.
        ctx.Session.BeginArrange();

        var box = new MeasuredBox { Width = width, Height = 0f };
        float y = 0f;
        for (int i = 0; i < roots.Count; i++)
        {
            UiElementSpec root = roots[i];
            if (IsHidden(root, ctx, narrow: false)) continue;

            UiNode rootNode = ctx.Session.GetOrCreateNode(UiNodeId.Root(root, i), root.Kind, i, root.Id);
            MeasuredBox child = MeasureElement(
                ctx,
                root,
                width,
                Math.Max(0f, height - y),
                rootNode);
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

            visible.Add(entry.Id.Path);

            // Geometry lands on the node: the snapshot stays the public view, the node is what the next
            // steps (hit stack, scroll targeting) read.
            entry.Node.Rect = entry.Rect;
            entry.Node.ContentRect = entry.ContentRect;

            if (string.Equals(entry.ContainerKind, "Scroll", StringComparison.Ordinal))
            {
                string key = ScrollKey(entry);
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

        // The arrange succeeded, so every node's Measure has been delivered and the flags mean
        // "recompute me on the next pass" again. A widget that marks its node dirty during Draw lands
        // here on the following pass; one that marks it during Measure is cleared because that measure
        // just happened.
        ctx.Session.ClearDirtyNodes();

        ClampScrollPositions(ctx.Session, lastEntries);
        ApplyScrollTarget(ctx);
        return cachedSnapshot;
    }

    /// <summary>
    /// Consumes a pending scroll-target request exactly once: the element whose declared Id the request
    /// names is looked up by node, its containing scroll container's node-keyed position is written, and
    /// the request is cleared. A request naming an Id no arranged element carries stays pending - the
    /// documented Tab-switch case, where the target becomes visible on a later frame - so there is no
    /// third behaviour between "consumed" and "still pending".
    /// </summary>
    private void ApplyScrollTarget(UiWidgetContext ctx)
    {
        string? targetId = ctx.Session.ScrollTargetElementId;
        if (string.IsNullOrEmpty(targetId)) return;

        PlacedEntry? target = null;
        foreach (PlacedEntry entry in lastEntries)
        {
            if (entry.Spec.Id.Length > 0 && string.Equals(entry.Spec.Id, targetId, StringComparison.Ordinal))
            {
                target = entry;
                break;
            }
        }

        if (target == null) return;

        Rect targetRect = target.Rect;
        foreach (PlacedEntry entry in lastEntries)
        {
            if (!string.Equals(entry.ContainerKind, "Scroll", StringComparison.Ordinal)) continue;

            Rect viewport = entry.Rect;
            Rect content = entry.ContentRect
                ?? new Rect(0f, 0f, Math.Max(1f, viewport.width), Math.Max(1f, viewport.height));
            float contentLocalY = targetRect.y - viewport.y;
            if (contentLocalY < -1f || contentLocalY > content.height + 1f) continue;

            float maxY = Math.Max(0f, content.height - viewport.height);
            float clampedY = ClampFloat(contentLocalY, 0f, maxY);
            Vector2 current = ctx.Session.GetScrollPosition(entry.Node);
            ctx.Session.SetScrollPosition(entry.Node, new Vector2(current.x, clampedY));
            ctx.Session.ClearScrollTarget();
            return;
        }
    }

    public void Draw(UiWidgetContext ctx, UiLayoutSnapshot snapshot, Rect viewport)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        // One hit pass per draw: the previous pass's complete paint order becomes the stack input dispatch
        // reads, and this pass builds the next one as its elements draw.
        ctx.Session.BeginHitPass();
        DrawEntries(lastEntries, 0, lastEntries.Count, ctx, new Vector2(viewport.x, viewport.y), null, Vector2.zero);
    }

    private void DrawEntries(
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

            // The element's arranged entry carries the scope it was measured in, so the draw pass
            // re-applies exactly that scope: the same chain resolves through the same resolver cache to
            // the same theme instance the Measure half used. The chain is set before the theme because
            // the theme is what the chain resolves to, and the page level (an empty chain) keeps the
            // injected theme rather than a clone of it.
            UiWidgetContext entryCtx = ctx.WithStyleChain(entry.StyleChain);
            entryCtx = entryCtx.WithTheme(ScopeTheme(entryCtx));
            entryCtx = entry.MeasureWidth > 0f ? entryCtx.WithViewWidth(entry.MeasureWidth) : entryCtx;
            entryCtx = entryCtx.WithWindowOrigin(windowOrigin).WithNode(entry.Node);

            // The element's content layer, in window space (draw rect plus the context's offset), and the
            // origin the funnel lifts a draw-local pointer by. Only widgets are hit surfaces: a container
            // layer would otherwise cover its own children.
            Vector2 entryOrigin = entryCtx.WindowOrigin;
            ctx.Session.SetCurrentWindowOrigin(entryOrigin);
            if (entry.Widget != null)
            {
                ctx.Session.PushHitLayer(
                    entry.Node,
                    new Rect(drawRect.x + entryOrigin.x, drawRect.y + entryOrigin.y, drawRect.width, drawRect.height),
                    isPopup: false);
            }

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
                UiFitAudit.BeginElement(entry.Id.Path);
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
                        //
                        // The element scope is what lets a widget written against the bare-string state
                        // key resolve into its own element. The finally is load-bearing rather than
                        // tidy: the guard swallows a throwing widget and keeps drawing, so an exception
                        // path that did not restore the previous element would send every later widget's
                        // keys into the failed element's namespace.
                        UiNode previous = ctx.Session.EnterNode(entry.Node);
                        try
                        {
                            UiSessionGuard.DrawWidget(entry.Widget, drawRect, entryCtx, entry.Node);
                        }
                        finally
                        {
                            ctx.Session.ExitNode(previous);
                        }
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

    private void DrawScopedContainer(
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
            Vector2 scrollPosition = ctx.Session.GetScrollPosition(entry.Node);
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
                ctx.Session.SetScrollPosition(entry.Node, scrollPosition);
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

    /// <summary>
    /// Re-clamps every scroll container's node-keyed position against the geometry this arrange produced.
    /// It reads the entries rather than the snapshot's string-keyed viewports on purpose: the snapshot
    /// keeps its declared-id view for consumers, while the state itself belongs to the node.
    /// </summary>
    private static void ClampScrollPositions(UiSession session, List<PlacedEntry> entries)
    {
        foreach (PlacedEntry entry in entries)
        {
            if (!string.Equals(entry.ContainerKind, "Scroll", StringComparison.Ordinal)) continue;
            if (!entry.ContentRect.HasValue) continue;

            session.SetScrollPosition(
                entry.Node,
                ClampScroll(session.GetScrollPosition(entry.Node), entry.ContentRect.Value, entry.Rect));
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

    /// <summary>
    /// Key for one scroll container in the snapshot's string-keyed geometry maps
    /// (<see cref="UiLayoutSnapshot.Viewports"/> and <see cref="UiLayoutSnapshot.ScrollContents"/>): the
    /// declared <c>Id</c> when the element has one, otherwise its display path, which is unique where the
    /// old path fallback aliased two unnamed Scroll siblings.
    /// <para>
    /// It is not the key of the session's scroll state. <see cref="UiSession.ScrollPositions"/>,
    /// <see cref="UiSession.GetScrollPosition"/> and <see cref="UiSession.SetScrollPosition"/> are keyed by
    /// the container's node (0.4.0 node step 2), so a caller holding the declared <c>Id</c> bridges
    /// through <see cref="UiSession.GetNodeByElementId(string)"/> - which answers for a declared <c>Id</c>
    /// and nothing else - and then reads or writes that node.
    /// </para>
    /// </summary>
    private static string ScrollKey(PlacedEntry entry)
    {
        return entry.Spec.Id.Length > 0 ? entry.Spec.Id : entry.Id.Path;
    }

    /// <summary>
    /// The element's own style scope: its declaration becomes the nearest link of the chain, and the theme
    /// the element sees becomes the one that chain resolves to. Both halves of a pass go through here -
    /// Measure directly, Draw through the chain each arranged entry recorded - so a widget never measures
    /// with one theme and draws with another.
    /// </summary>
    private UiWidgetContext ElementScope(UiWidgetContext ctx, UiElementSpec spec)
    {
        UiWidgetContext scoped = ctx.WithStyleDeclaration(DeclarationOf(spec));
        return scoped.WithTheme(ScopeTheme(scoped));
    }

    /// <summary>
    /// The theme one chain resolves to. An empty chain is the page level: there the answer is the injected
    /// theme itself, which already carries the document's page-level defaults because the host applies them
    /// before the first arrangement - asking the resolver for a clone would print a different instance for
    /// the same values. A declared scope resolves through the resolver, which builds each effective
    /// (scheme, density) pair once and hands out that same instance on every later frame.
    /// </summary>
    private UiTheme ScopeTheme(UiWidgetContext ctx)
    {
        IReadOnlyList<UiStyleDeclaration>? chain = ctx.StyleChain;
        if (styleResolver == null || chain == null || chain.Count == 0) return ctx.Theme;
        return styleResolver.ThemeFor(chain);
    }

    /// <summary>
    /// The declaration one element contributes to the chain: its own <c>Scheme</c> and <c>Density</c>
    /// attributes and nothing else. Those are the two axes that inherit, which is exactly what a chain
    /// carries; <c>Tone</c> and <c>Emphasis</c> stay on the element's own spec, because a role that
    /// inherited would destroy the information the tag exists to carry.
    /// </summary>
    private static UiStyleDeclaration DeclarationOf(UiElementSpec spec)
    {
        return new UiStyleDeclaration(ReadScopeName(spec, "Scheme"), ReadScopeName(spec, "Density"));
    }

    /// <summary>A trimmed attribute value, or null when the element did not declare it (or declared nothing).</summary>
    private static string? ReadScopeName(UiElementSpec spec, string attribute)
    {
        if (!spec.TryGetAttribute(attribute, out string raw)) return null;
        if (raw == null) return null;
        string value = raw.Trim();
        return value.Length == 0 ? null : value;
    }

    private MeasuredBox MeasureElement(UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight, UiNode node)
    {
        // The element's own style scope comes first: its declaration joins the chain and the theme below
        // every branch resolves against the chain, so a widget measures - and later draws - with the theme
        // its own scope resolved to. Containers are scoped the same way, which is what lets a region's
        // font reach the label widths measured inside it.
        ctx = ElementScope(ctx, spec);

        string containerKind = GetContainerKind(spec);
        if (containerKind.Length == 0)
        {
            IUiWidget widget = GetOrCreateWidget(spec, node.Id);
            // Measure must see the widget's arranged width, not the page width, so width-dependent
            // widgets (e.g. stacked narrow Mood rows) agree with the Draw pass, which already uses
            // entry.MeasureWidth via WithViewWidth. The element is bound to this context too, so a
            // widget's Measure and Draw report the same identity for the same element.
            UiWidgetContext measureCtx = ctx.WithViewWidth(width).WithNode(node);

            // The same element scope the draw pass enters, and the same try/finally: the guard recovers
            // a throwing Measure, so the scope must not leak into the sibling measured next.
            UiNode previous = ctx.Session.EnterNode(node);
            float height;
            try
            {
                height = ResolveHeight(spec, widget, measureCtx, width, node);
            }
            finally
            {
                ctx.Session.ExitNode(previous);
            }

            var leaf = new PlacedEntry
            {
                Spec = spec,
                Id = node.Id,
                Node = node,
                StyleChain = ctx.StyleChain,
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

        return MeasureContainer(ctx, spec, width, availableHeight, node);
    }

    private MeasuredBox MeasureContainer(UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight, UiNode node)
    {
        string declaredKind = GetContainerKind(spec);
        Padding padding = ParsePadding(spec);
        float gap = ReadGap(spec);
        float titleHeight = HasTitle(spec) ? SectionTitleHeight : 0f;
        float innerWidth = Math.Max(1f, width - padding.Left - padding.Right);
        float innerY = padding.Top + titleHeight;

        // N2 (US->FL round 3): one numeric threshold against this container's own inner width.
        // The narrow state selects the declared variants — direction (Narrow), column count
        // (NarrowCols) and per-child visibility (NarrowHidden) — and nothing else; there is no
        // expression language here by standing decision.
        bool narrow = IsNarrow(spec, innerWidth);
        string kind = ResolveEffectiveKind(spec, declaredKind, narrow);

        if (string.Equals(kind, "Row", StringComparison.Ordinal))
        {
            return MeasureRow(ctx, spec, declaredKind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, node, narrow);
        }

        if (string.Equals(kind, "Wrap", StringComparison.Ordinal))
        {
            return MeasureWrap(ctx, spec, declaredKind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, node, narrow);
        }

        if (string.Equals(kind, "Overlay", StringComparison.Ordinal))
        {
            return MeasureOverlay(ctx, spec, declaredKind, width, padding, titleHeight, innerWidth, innerY, availableHeight, node, narrow);
        }

        if (string.Equals(kind, "Scroll", StringComparison.Ordinal))
        {
            return MeasureScroll(ctx, spec, declaredKind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, node, narrow);
        }

        // Stack, Column, Section, Surface and Clip are vertical stacks. Clip additionally becomes
        // a structural group during Draw.
        return MeasureStack(ctx, spec, declaredKind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, node, narrow);
    }

    /// <summary>
    /// One child plus the ordinal it was declared with. The ordinal is the fallback identity segment,
    /// and it is taken from the declared order - not from the order of the children that survive
    /// Hidden/Tab filtering - so a hidden sibling or a Tab switch never renumbers the others.
    /// </summary>
    private readonly struct ChildSlot
    {
        internal readonly UiElementSpec Spec;
        internal readonly int DeclaredIndex;

        internal ChildSlot(UiElementSpec spec, int declaredIndex)
        {
            Spec = spec;
            DeclaredIndex = declaredIndex;
        }
    }

    /// <summary>
    /// The node for one child, created on first use. Identity is the parent's identity plus the child's
    /// declared ordinal, so the same element keeps the same node - and its state - across passes.
    /// </summary>
    private static UiNode ChildNode(UiWidgetContext ctx, UiNode parent, UiElementSpec spec, int declaredIndex)
    {
        UiNode child = ctx.Session.GetOrCreateNode(
            parent.Id.Child(spec, declaredIndex), spec.Kind, declaredIndex, spec.Id);
        parent.AddChild(child);
        return child;
    }

    private static List<ChildSlot> VisibleChildren(UiElementSpec spec, UiWidgetContext ctx, bool narrow)
    {
        var visible = new List<ChildSlot>();
        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            if (!IsHidden(child, ctx, narrow)) visible.Add(new ChildSlot(child, i));
        }

        return visible;
    }

    private static bool IsNarrow(UiElementSpec spec, float innerWidth)
    {
        if (!spec.TryGetAttribute("Breakpoint", out string raw))
        {
            return false;
        }

        // Malformed thresholds are rejected at creation time (UiHost); a value that still fails to
        // parse here can only come from a programmatically built spec, and degrades to never-narrow.
        if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float breakpoint)
            || breakpoint <= 0f)
        {
            return false;
        }

        return innerWidth < breakpoint;
    }

    private static string ResolveEffectiveKind(UiElementSpec spec, string declaredKind, bool narrow)
    {
        if (!narrow || !spec.TryGetAttribute("Narrow", out string alternative))
        {
            return declaredKind;
        }

        alternative = alternative.Trim();
        bool declaredIsRow = string.Equals(declaredKind, "Row", StringComparison.Ordinal);
        if (declaredIsRow
            && (string.Equals(alternative, "Column", StringComparison.OrdinalIgnoreCase)
                || string.Equals(alternative, "Stack", StringComparison.OrdinalIgnoreCase)))
        {
            return "Column";
        }

        if (!declaredIsRow
            && string.Equals(alternative, "Row", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(declaredKind, "Column", StringComparison.Ordinal)
                || string.Equals(declaredKind, "Stack", StringComparison.Ordinal)
                || string.Equals(declaredKind, "Section", StringComparison.Ordinal)
                || string.Equals(declaredKind, "Surface", StringComparison.Ordinal)))
        {
            return "Row";
        }

        return declaredKind;
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
        UiNode node,
        bool narrow)
    {
        List<ChildSlot> visible = VisibleChildren(spec, ctx, narrow);

        // Per-child width: a stack child is full-width unless it declares Width="Auto", in which
        // case its slot hugs its own label text (N1). The pre-pass and the arrange pass must use
        // the same width or a wrap-sensitive child measures its height against a band it will not
        // be drawn in.
        var childWidths = new float[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            childWidths[i] = ResolveStackChildWidth(visible[i].Spec, innerWidth, ctx);
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
            if (IsFlexibleFill(visible[i].Spec))
            {
                fillCount++;
                flowHeights[i] = 0f;
                continue;
            }

            UiNode childNode = ChildNode(ctx, node, visible[i].Spec, visible[i].DeclaredIndex);
            float flow = MeasureElement(ctx, visible[i].Spec, childWidths[i], availableInner, childNode).Height;
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

            ChildSlot child = visible[i];
            UiNode childNode = ChildNode(ctx, node, child.Spec, child.DeclaredIndex);
            float childAvailable;
            if (IsFlexibleFill(child.Spec))
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

            MeasuredBox childBox = MeasureElement(ctx, child.Spec, childWidths[i], childAvailable, childNode);
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
            Id = node.Id,
            Node = node,
            StyleChain = ctx.StyleChain,
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
        UiNode node,
        bool narrow)
    {
        List<ChildSlot> visible = VisibleChildren(spec, ctx, narrow);

        float[] widths = ResolveColumnWidths(visible, innerWidth, gap, ctx);
        var box = new MeasuredBox { Width = width, Height = 0f };
        float x = padding.Left;
        float maxHeight = 0f;

        for (int i = 0; i < visible.Count; i++)
        {
            UiNode childNode = ChildNode(ctx, node, visible[i].Spec, visible[i].DeclaredIndex);
            MeasuredBox childBox = MeasureElement(ctx, visible[i].Spec, widths[i], availableHeight, childNode);
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
            Id = node.Id,
            Node = node,
            StyleChain = ctx.StyleChain,
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
        UiNode node,
        bool narrow)
    {
        var box = new MeasuredBox { Width = width, Height = 0f };
        float x = padding.Left;
        float y = innerY;
        float lineHeight = 0f;
        bool firstInLine = true;
        int placedInLine = 0;

        // N2's column variant: declared Cols (wide) / NarrowCols (narrow) fix a uniform grid —
        // the acceptance row the rebuild contract promised ("响应式列数") is a column COUNT, and
        // a count is only declarable if the grid can be pinned. Without either, flow-by-width
        // stays exactly as shipped.
        int cols = ResolveWrapCols(spec, narrow);
        float cellWidth = cols > 0
            ? Math.Max(1f, (innerWidth - gap * (cols - 1)) / cols)
            : 0f;

        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            if (IsHidden(child, ctx, narrow)) continue;

            float childWidth = cols > 0 ? cellWidth : ResolveWrapWidth(child, innerWidth, ctx);
            UiNode childNode = ChildNode(ctx, node, child, i);

            bool lineFull = cols > 0 ? placedInLine >= cols : x + childWidth > padding.Left + innerWidth;
            if (!firstInLine && lineFull)
            {
                x = padding.Left;
                y += lineHeight + gap;
                lineHeight = 0f;
                firstInLine = true;
                placedInLine = 0;
            }

            MeasuredBox childBox = MeasureElement(
                ctx,
                child,
                childWidth,
                Math.Max(0f, availableHeight - (y - innerY)),
                childNode);
            childBox = OffsetBox(childBox, x, y);
            box.Entries.AddRange(childBox.Entries);

            x += childWidth + gap;
            lineHeight = Math.Max(lineHeight, childBox.Height);
            firstInLine = false;
            placedInLine++;
        }

        float contentHeight = Math.Max(0f, y + lineHeight - innerY);
        float naturalHeight = padding.Top + titleHeight + contentHeight + padding.Bottom;
        float height = ResolveContainerHeight(spec, naturalHeight, availableHeight);
        box.Height = height;

        var containerEntry = new PlacedEntry
        {
            Spec = spec,
            Id = node.Id,
            Node = node,
            StyleChain = ctx.StyleChain,
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
        UiNode node,
        bool narrow)
    {
        var box = new MeasuredBox { Width = width, Height = 0f };
        float maxHeight = 0f;

        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            if (IsHidden(child, ctx, narrow)) continue;

            UiNode childNode = ChildNode(ctx, node, child, i);
            MeasuredBox childBox = MeasureElement(ctx, child, innerWidth, availableHeight, childNode);
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
            Id = node.Id,
            Node = node,
            StyleChain = ctx.StyleChain,
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
        UiNode node,
        bool narrow)
    {
        var entries = new List<PlacedEntry>();
        float naturalContentHeight = MeasureScrollContent(
            ctx, spec, innerWidth, padding, gap, innerY, availableHeight, node, entries, narrow);

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
                ctx, spec, reservedInnerWidth, padding, gap, innerY, availableHeight, node, entries,
                IsNarrow(spec, reservedInnerWidth));
            naturalHeight = padding.Top + titleHeight + reflowedContentHeight + padding.Bottom;
        }

        var box = new MeasuredBox { Width = width, Height = viewportHeight };
        var containerEntry = new PlacedEntry
        {
            Spec = spec,
            Id = node.Id,
            Node = node,
            StyleChain = ctx.StyleChain,
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
        UiNode node,
        List<PlacedEntry> entries,
        bool narrow)
    {
        float y = innerY;
        bool first = true;

        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            if (IsHidden(child, ctx, narrow)) continue;

            if (!first) y += gap;
            UiNode childNode = ChildNode(ctx, node, child, i);
            MeasuredBox childBox = MeasureElement(
                ctx,
                child,
                ResolveStackChildWidth(child, contentInnerWidth, ctx),
                Math.Max(0f, availableHeight - (y - innerY)),
                childNode);
            childBox = OffsetBox(childBox, padding.Left, y);
            entries.AddRange(childBox.Entries);
            y += childBox.Height;
            first = false;
        }

        return Math.Max(0f, y - innerY);
    }

    private IUiWidget GetOrCreateWidget(UiElementSpec spec, UiNodeId id)
    {
        if (widgetInstances.TryGetValue(id, out IUiWidget? widget))
        {
            return widget;
        }

        widget = UiWidgetRegistry.Resolve(scope, spec.Kind);
        widget.Configure(spec);
        widgetInstances.Add(id, widget);
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
                Id = entry.Id,
                Node = entry.Node,
                StyleChain = entry.StyleChain,
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

    /// <summary>Numeric Width on a child; Auto and malformed values are not fixed.</summary>
    private static bool TryFixedWidth(UiElementSpec spec, out float width)
    {
        width = 0f;
        return spec.TryGetAttribute("Width", out string raw)
            && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out width)
            && width > 0f;
    }

    private static bool IsAutoWidth(UiElementSpec spec)
    {
        return spec.TryGetAttribute("Width", out string raw)
            && string.Equals(raw.Trim(), "Auto", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Text-natural width of a child (N1): the maximum measured advance over the label attributes
    /// its kind declared at registration, each resolved through translation when the attribute
    /// name ends in Key. Zero means "not Auto-measurable" — a kind without a declared label set,
    /// or with none of them filled — and the caller falls back to the unsized distribution.
    /// General widget natural-size measurement stays unshipped until a second citation makes it
    /// real; this seam is deliberately text-only.
    /// </summary>
    private static float MeasureLabelWidth(UiElementSpec spec, UiWidgetContext ctx)
    {
        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(ctx.Source, spec.Kind);
        if (labels == null) return 0f;

        float widest = 0f;
        foreach (string attribute in labels)
        {
            if (!spec.TryGetAttribute(attribute, out string value)) continue;
            value = value.Trim();
            if (value.Length == 0) continue;

            string text = attribute.EndsWith("Key", StringComparison.OrdinalIgnoreCase)
                ? ctx.Translation.Translate(value)
                : value;
            widest = Math.Max(widest, ctx.Metrics.MeasureWidth(text, ctx.Theme.DefaultFont));
        }

        return widest;
    }

    /// <summary>MinWidth/MaxWidth clamp, applied only when declared (absent attributes change nothing).</summary>
    private static float ClampDeclaredWidth(UiElementSpec spec, float width)
    {
        if (spec.TryGetAttribute("MinWidth", out string raw)
            && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float min)
            && min > 0f)
        {
            width = Math.Max(width, min);
        }

        if (spec.TryGetAttribute("MaxWidth", out string raw2)
            && float.TryParse(raw2.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float max)
            && max > 0f)
        {
            width = Math.Min(width, max);
        }

        return width;
    }

    private static float ResolveWrapWidth(UiElementSpec spec, float innerWidth, UiWidgetContext ctx)
    {
        if (TryFixedWidth(spec, out float fixedWidth))
        {
            return fixedWidth;
        }

        if (IsAutoWidth(spec))
        {
            float natural = ClampDeclaredWidth(spec, MeasureLabelWidth(spec, ctx));
            if (natural > 0f) return Math.Min(natural, innerWidth);
            // fall through: not measurable, keep the historical full-row width
        }

        return Math.Max(1f, innerWidth);
    }

    /// <summary>
    /// A vertical-stack child's slot width: full inner width, except a child that declares
    /// Width="Auto" and measures a positive label width hugs that width instead.
    /// </summary>
    private static float ResolveStackChildWidth(UiElementSpec spec, float innerWidth, UiWidgetContext ctx)
    {
        if (IsAutoWidth(spec))
        {
            float natural = ClampDeclaredWidth(spec, MeasureLabelWidth(spec, ctx));
            if (natural > 0f) return Math.Min(natural, innerWidth);
        }

        return Math.Max(1f, innerWidth);
    }

    /// <summary>
    /// Declared column count for a Wrap (N2): NarrowCols in the narrow state, Cols otherwise;
    /// zero means "no fixed grid, flow by width" — the shipped behavior.
    /// </summary>
    private static int ResolveWrapCols(UiElementSpec spec, bool narrow)
    {
        if (narrow && TryReadInt(spec, "NarrowCols", out int narrowCols) && narrowCols > 0)
        {
            return narrowCols;
        }

        if (TryReadInt(spec, "Cols", out int cols) && cols > 0)
        {
            return cols;
        }

        return 0;
    }

    private static bool TryReadInt(UiElementSpec spec, string attribute, out int value)
    {
        value = 0;
        return spec.TryGetAttribute(attribute, out string raw)
            && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static float ResolveHeight(UiElementSpec spec, IUiWidget widget, UiWidgetContext ctx, float width, UiNode node)
    {
        if (spec.TryGetAttribute("Height", out string raw))
        {
            string value = raw.Trim();
            if (value.Length == 0 || string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return MeasuredHeight(widget, ctx, node);
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedHeight))
            {
                return Math.Max(0f, fixedHeight);
            }

            throw new FormatException(
                $"Widget id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Height '{raw}'; expected a number or Auto.");
        }

        return MeasuredHeight(widget, ctx, node);
    }

    /// <summary>
    /// One widget's measured height, recovered through the session if the widget throws. The guard is
    /// here rather than in each widget because a measurement that dies takes the whole arrange pass
    /// with it, and the tree — not the control — owns whether the page survives that. The fallback keeps
    /// the element's slot instead of collapsing it, so a trip cannot reshuffle the rest of the page.
    /// </summary>
    private static float MeasuredHeight(IUiWidget widget, UiWidgetContext ctx, UiNode node)
    {
        // The guard is here rather than in each widget because a measurement that dies takes the whole
        // arrange pass with it, and the tree — not the control — owns whether the page survives that.
        // The fallback keeps the element's slot instead of collapsing it, so a trip cannot reshuffle
        // the rest of the page.
        return Math.Max(0f, UiSessionGuard.MeasureWidget(widget, ctx, node, RecoveryBandHeight));
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

    private static float[] ResolveColumnWidths(
        IReadOnlyList<ChildSlot> children, float innerWidth, float gap, UiWidgetContext ctx)
    {
        var widths = new float[children.Count];
        float fixedSum = 0f;
        int flexCount = 0;
        var autoSlots = new List<int>();

        for (int i = 0; i < children.Count; i++)
        {
            if (TryFixedWidth(children[i].Spec, out float fixedWidth))
            {
                widths[i] = fixedWidth;
                fixedSum += fixedWidth;
            }
            else if (IsAutoWidth(children[i].Spec))
            {
                autoSlots.Add(i);
            }
            else
            {
                flexCount++;
            }
        }

        float totalGap = gap * Math.Max(0, children.Count - 1);

        // Auto columns take their measured natural width first (N1: "the label column is as wide
        // as its widest label"), clamped by declared Min/Max, then capped collectively by what
        // the fixed siblings leave behind: when the autos do not fit, they shrink proportionally
        // and flex siblings keep at least their historical floor.
        float autoBudget = Math.Max(0f, innerWidth - totalGap - fixedSum);
        float autoNaturalTotal = 0f;
        var autoNaturals = new float[autoSlots.Count];
        for (int s = 0; s < autoSlots.Count; s++)
        {
            float natural = ClampDeclaredWidth(
                children[autoSlots[s]].Spec, MeasureLabelWidth(children[autoSlots[s]].Spec, ctx));
            autoNaturals[s] = Math.Max(1f, natural);
            autoNaturalTotal += autoNaturals[s];
        }

        float autoScale = autoNaturalTotal > autoBudget && autoNaturalTotal > 0f
            ? autoBudget / autoNaturalTotal
            : 1f;
        float autoSum = 0f;
        for (int s = 0; s < autoSlots.Count; s++)
        {
            float w = Math.Max(1f, autoNaturals[s] * autoScale);
            widths[autoSlots[s]] = w;
            autoSum += w;
        }

        float remaining = Math.Max(0f, innerWidth - totalGap - fixedSum - autoSum);
        float flexWidth = flexCount > 0 ? Math.Max(1f, remaining / flexCount) : 0f;

        for (int i = 0; i < widths.Length; i++)
        {
            if (widths[i] <= 0f && !autoSlots.Contains(i))
            {
                widths[i] = ClampDeclaredWidth(children[i].Spec, flexWidth);
            }
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

    private static bool IsHidden(UiElementSpec spec, UiWidgetContext ctx, bool narrow)
    {
        if (narrow
            && spec.TryGetAttribute("NarrowHidden", out string narrowRaw)
            && (string.Equals(narrowRaw.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(narrowRaw.Trim(), "1", StringComparison.Ordinal)))
        {
            return true;
        }

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
