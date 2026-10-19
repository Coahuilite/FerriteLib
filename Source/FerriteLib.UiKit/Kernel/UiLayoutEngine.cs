using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using VerseWidgets = Verse.Widgets;

namespace FerriteLib.UiKit.Kernel;
/// <summary>
/// Greenfield constrained layout engine. Produces a stable <see cref = "UiLayoutSnapshot"/> with
/// page-local rects. Supports the Schema=2 container vocabulary: Stack, Row, Column, Wrap,
/// Section, Surface, Scroll, Clip and Overlay. Scroll/Clip/Overlay are structural containers and
/// are the only places in this engine that open/close native IMGUI scopes.
/// Containers with <c>Fill="true"</c> (and no explicit Height) size their viewport to the available
/// height passed down from the arrange call, so a root Scroll fills the window while its content
/// keeps its natural height.
/// </summary>
public sealed partial class UiLayoutEngine
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

        /// <summary>
        /// Set on the root entry of one MATERIALIZED Repeat row (D4 VisibleRows). The Scroll's counting
        /// budget must see rows, not the Repeat as one blob and not the title/detail/hover/hit pieces
        /// inside a row as several: this flag is stamped by <see cref="MeasureRepeat"/> on the single
        /// root entry each row box contributes, so counting reads the same measured rects the pass
        /// already produced instead of re-deriving anything.
        /// </summary>
        internal bool IsMaterializedRow;
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

    /// <summary>
    /// "This arrange has no content-height reference for the element": what every measure call that is not a
    /// child of a max-based parent passes, and the reason the mode degrades to the element's own content there
    /// instead of throwing or collapsing to zero.
    /// </summary>
    private const float NoContentHeightReference = -1f;
    // Reserved width for a vertical scrollbar drawn inside the right edge of a Scroll viewport.
    // Matches Verse.GenUI.ScrollBarWidth (16f), the convention the US pages already follow.
    private const float ScrollbarWidth = 16f;

    // Attribute names of the collection element. They are manifest vocabulary the engine reads, like the
    // Bind/ActionBind names above, not source text.
    private const string RepeatItemsAttribute = "Items";
    private const string RepeatTemplateAttribute = "Template";

    private readonly string scope;
    private readonly List<UiLayoutDiagnostic> layoutDiagnostics = new();

    // The document resolver is the engine's answer to "what does this scope look like": null means the
    // tree has no style document at all, in which case every element draws with the injected theme.
    private readonly UiStyleResolver? styleResolver;

    // The definition's template table (the manifest's <Templates> section), handed in by the host. A Repeat
    // element materializes a named template once per item key; an engine built without the table refuses the
    // row set with one bounded report instead of drawing a silently empty band.
    private readonly IReadOnlyDictionary<string, UiElementSpec> templates;

    // One entry per arranged Repeat element: the row keys the current items binding produced, in order, plus
    // the derived per-item subtree each key instantiates. Keyed by the repeat's node identity and pruned
    // with the node, so a removed <Repeat> leaves nothing behind - the same object lifetime rule the
    // session's own tables follow. A reorder reuses both the keys and the derived specs, which is what makes
    // reordering reuse nodes and state instead of rebuilding them.
    private readonly Dictionary<UiNodeId, RepeatState> repeatStates = new();

    // The repeats materialized by the arrange currently running. It is what lets the declared identity set
    // contain exactly the rows the current items binding produced: that set is what the session's
    // PruneNodesExcept reaps against, so a removed item's node - and its state, its sub-nodes and its hit
    // layers - is released rather than merely zeroed.
    private readonly List<MaterializedRepeat> materializedRepeats = new();
    // The template subtree is deliberately outside the Host's creation-time walk, because its binding keys
    // are item-scoped and cannot exist as page bindings. Its attribute vocabulary therefore has to be
    // enforced here, and these two sets are UiHost's own lists, copied because UiHost's are private.
    // KernelRepeatTests reflects the private originals and fails when the copies drift, so the duplication
    // cannot rot silently - which is the reason it is duplication rather than a hole.
    private static readonly HashSet<string> TemplateContainerAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "Kind",
        "Gap",
        "Padding",
        "Height",
        "Title",
        "TitleKey",
        "Hidden",
        "Tab",
        "Width",
        "WidthKey",
        "Fill",
        "MinWidth",
        "MaxWidth",
        "Breakpoint",
        "Narrow",
        "Cols",
        "NarrowCols",
        "NarrowHidden",
        "WideHidden",
        // D4: same vocabulary the page-level container gate keeps; KernelRepeatTests reflects both private
        // originals and fails when the copies drift, so the attribute is added in lockstep or not at all.
        "VisibleRows",
        "Scheme",
        "Density",
        "Visible",
        "VisibleKey",
        "AlignX",
        "OffsetX",
        "AlignY",
        "OffsetY",
        "AnchorX",
        "AnchorY",
        "Font",
        "SharedLabels",
        // FL-IC2: the Cancel layer of the tree walk. Engine-wide on a container as well as a widget, because
        // the ladder walks the TREE and a section is a legitimate layer to return from — the element that
        // owns no drawing of its own still names a business action. UiHost keeps the mirrored pair; the
        // drift lane refuses one-sided edits.
        "CancelBind"
    };
    private static readonly HashSet<string> EngineWideWidgetAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id",
        "Kind",
        "Hidden",
        "Tab",
        "Width",
        "WidthKey",
        "MinWidth",
        "MaxWidth",
        "NarrowHidden",
        "WideHidden",
        "SelectedKey",
        "Scheme",
        "Density",
        "Visible",
        "VisibleKey",
        "HelpKey",
        "AlignX",
        "OffsetX",
        "AlignY",
        "OffsetY",
        "AnchorX",
        "AnchorY",
        "Font",
        // FL-IC2: see the container list above; the two move together or the drift lane fails.
        "CancelBind"
    };

    private static readonly string[] NoSchema = Array.Empty<string>();

    // Widget instances are keyed by element identity, not by path text: two unnamed same-kind siblings
    // used to collide here, so the second one drew with the first one's spec.
    private readonly Dictionary<UiNodeId, IUiWidget> widgetInstances = new();

    private UiLayoutSnapshot? cachedSnapshot;
    private Vector2 cachedAvailable;
    private int cachedContentRevision = -1;
    private int cachedDefinitionRevision;
    private int cachedTranslationRevision = int.MinValue;
    private List<PlacedEntry> lastEntries = new();

    // Per-key notification state. keyNodes is the last arrange's map from an announced binding key to the
    // nodes whose elements declare it; committedRevisions is the revision this engine has already folded
    // into the arrangement. Together they are what makes an announcement target the elements that
    // declared the key instead of the page: a key no arranged element declares is never polled, and one
    // that is polled marks only its own nodes.
    private readonly Dictionary<string, List<UiNode>> keyNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> committedRevisions = new(StringComparer.Ordinal);

    // The three attribute names below are the page vocabulary the atoms and the consumer kinds read when
    // they resolve a binding (Bind, ActionBind, OptionsBind), plus the declared Id as the documented
    // value-atom fallback. They are manifest vocabulary, not source text: the engine is reading what the
    // author declared, which is the opposite of inferring an invalidation from a page's C#.
    private const string BindAttribute = "Bind";
    private const string ActionBindAttribute = "ActionBind";
    private const string OptionsBindAttribute = "OptionsBind";
    private const string VisibleKeyAttribute = "VisibleKey";
    private const string WidthKeyAttribute = "WidthKey";
    private const string HelpKeyAttribute = "HelpKey";
    // FL-IC2: the tree's Cancel layer. Engine-wide like VisibleKey/HelpKey because the ENGINE reads it and
    // publishes it on the node; no kind owns it, so no kind's schema has to grow for a page to declare it.
    private const string CancelBindAttribute = "CancelBind";
    private const string TabAttribute = "Tab";

    /// <summary>
    /// The engine a <see cref="UiHost"/> builds: <paramref name="styleResolver"/> turns the chain each
    /// element declares into the theme that element draws with. Null keeps the pre-document behaviour,
    /// where the injected theme is the only theme.
    /// </summary>
    public UiLayoutEngine(
        string scope,
        UiStyleResolver? styleResolver = null,
        IReadOnlyDictionary<string, UiElementSpec>? templates = null)
    {
        this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        this.styleResolver = styleResolver;
        this.templates = templates ?? new Dictionary<string, UiElementSpec>(StringComparer.Ordinal);
        if (this.templates.Count > 0)
        {
            ValidateTemplates();
        }
    }

    public UiLayoutSnapshot ArrangeRoots(UiWidgetContext ctx, Vector2 available, IReadOnlyList<UiElementSpec> roots)
    {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
        if (roots == null)
            throw new ArgumentNullException(nameof(roots));
        float width = Normalize(available.x, 1f);
        float height = Normalize(available.y, 1f);
        // The frame boundary: announcements raised during the previous pass are folded into one commit
        // before the cache is consulted, so a Paint-class key leaves the cache alone while a
        // Measure/Structure-class key marks its elements and moves the arrangement clock.
        CommitNotifications(ctx);
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

        Action restoreArrangement = ctx.Session.CaptureArrangement();
        var priorKeys = new Dictionary<string, List<UiNode>>(keyNodes, StringComparer.Ordinal);
        var priorRepeats = new Dictionary<UiNodeId, RepeatState>(repeatStates);
        var priorMaterialized = new List<MaterializedRepeat>(materializedRepeats);
        // Copy only derived materialization. Business state and completed requests are never rolled back.
        foreach (KeyValuePair<UiNodeId, RepeatState> entry in priorRepeats)
            repeatStates[entry.Key] = entry.Value.Copy();
        try
        {
            // A fresh arrange: every node forgets its children and geometry, and the entries below publish
            // them again for the elements this pass visits. The key map is rebuilt with it, so a key no longer
            // declared by any arranged element stops being polled instead of invalidating a page forever.
            ctx.Session.BeginArrange();
            layoutDiagnostics.Clear();
            keyNodes.Clear();
            materializedRepeats.Clear();
            var box = new MeasuredBox
            {
                Width = width,
                Height = 0f
            };
            float y = 0f;
            for (int i = 0; i < roots.Count; i++)
            {
                UiElementSpec root = roots[i];
                if (IsHidden(root, ctx, narrow: false, parent: null, declaredIndex: i))
                {
                    // A hidden root has no arranged ancestor, so its declared keys report to the session-level
                    // node - the same owner a host-level caller's state resolves into. That is what lets its
                    // VisibleKey/Tab come back and re-arrange the page when the model says so.
                    RecordDeclaredKeys(root, ctx.Session.ActiveNode, ctx.Bindings);
                    continue;
                }

                UiNode rootNode = ctx.Session.GetOrCreateElementNode(UiNodeId.Root(root, i), root, i);
                MeasuredBox child = MeasureElement(ctx, root, width, Math.Max(0f, height - y), rootNode);
                child = OffsetBox(child, 0f, y);
                box.Entries.AddRange(child.Entries);
                y += child.Height;
            }

            box.Height = Math.Max(1f, y);
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

            // The arrange succeeded, so every node's Measure has been delivered and the flags mean
            // "recompute me on the next pass" again. A widget that marks its node dirty during Draw lands
            // here on the following pass; one that marks it during Measure is cleared because that measure
            // just happened.
            // A definition that no longer declares an identity must not leave its node behind - a reload that
            // removes an element would otherwise keep a stale node, a stale Kind and orphaned state forever.
            // The session owns node lifetime, so the engine hands it the definition it just arranged and the
            // session releases what is gone. Declared-but-hidden elements are in the set on purpose: their
            // identity still exists and their state has to survive the hide.
            var declaredIdentities = new HashSet<UiNodeId>();
            CollectDeclaredIdentities(roots, declaredIdentities);
            // A row set's identities are data-driven, so the definition walk above cannot know them: this
            // arrange's materialization is the definition for this purpose. A key the items binding no longer
            // supplies instantiates nothing this pass, is therefore declared by nobody, and the session's prune
            // releases its node - with its state, its sub-nodes and its hit layers - through the one lifetime
            // path every other element already uses. That is why "removal cleans the item's state" needs no
            // second cleanup rule.
            CollectRepeatItemIdentities(declaredIdentities);
            // A declared-but-hidden collection keeps what it last materialized: P2's rule for a hidden element is
            // that its identity survives the hide and only a removed one is released, and that rule has to hold
            // for rows too or a Tab switch would silently discard every row's state the page still declares.
            CollectHiddenRepeatIdentities(roots, declaredIdentities);
            if (ctx.Session.PruneNodesExcept(declaredIdentities) > 0)
            {
                // A widget instance keyed by an identity that no longer exists is the same leak one level up.
                PruneWidgetInstances(ctx.Session);
            }

            PruneRepeatStates(ctx.Session);
            ClampScrollPositions(ctx.Session, box.Entries);
            ApplyScrollTarget(ctx, box.Entries);
            cachedAvailable = new Vector2(width, height);
            cachedContentRevision = ctx.Session.ContentRevision;
            cachedDefinitionRevision = DefinitionRevision(ctx);
            cachedTranslationRevision = ctx.Translation.TranslationRevision;
            cachedSnapshot = new UiLayoutSnapshot(new Vector2(width, box.Height), rects, visible, viewports, scrollContents, cachedDefinitionRevision, Array.AsReadOnly(layoutDiagnostics.ToArray()));
            lastEntries = box.Entries;
            observationLayoutFrame = ctx.Session.Frame;
            observationLayoutAtUtc = DateTime.UtcNow;
            ctx.Session.ClearDirtyNodes();
            return cachedSnapshot;
        }
        catch
        {
            restoreArrangement();
            keyNodes.Clear();
            foreach (KeyValuePair<string, List<UiNode>> entry in priorKeys)
                keyNodes.Add(entry.Key, entry.Value);
            repeatStates.Clear();
            foreach (KeyValuePair<UiNodeId, RepeatState> entry in priorRepeats)
                repeatStates.Add(entry.Key, entry.Value);
            materializedRepeats.Clear();
            materializedRepeats.AddRange(priorMaterialized);
            PruneWidgetInstances(ctx.Session);
            throw;
        }
    }

    /// <summary>
    /// Consumes a pending scroll-target request exactly once: the element whose declared Id the request
    /// names is looked up by node, its containing scroll container's node-keyed position is written, and
    /// the request is cleared. A request naming an Id no arranged element carries stays pending - the
    /// documented Tab-switch case, where the target becomes visible on a later frame - so there is no
    /// third behaviour between "consumed" and "still pending".
    /// </summary>
    private void ApplyScrollTarget(UiWidgetContext ctx, IReadOnlyList<PlacedEntry>? entries = null)
    {
        string? targetId = ctx.Session.ScrollTargetElementId;
        if (string.IsNullOrEmpty(targetId))
            return;
        PlacedEntry? target = null;
        foreach (PlacedEntry entry in entries ?? lastEntries)
        {
            if (entry.Spec.Id.Length > 0 && string.Equals(entry.Spec.Id, targetId, StringComparison.Ordinal))
            {
                target = entry;
                break;
            }
        }

        if (target == null)
            return;
        Rect targetRect = target.Rect;
        foreach (PlacedEntry entry in entries ?? lastEntries)
        {
            if (!string.Equals(entry.ContainerKind, "Scroll", StringComparison.Ordinal))
                continue;
            Rect viewport = entry.Rect;
            Rect content = entry.ContentRect ?? new Rect(0f, 0f, Math.Max(1f, viewport.width), Math.Max(1f, viewport.height));
            float contentLocalY = targetRect.y - viewport.y;
            if (contentLocalY < -1f || contentLocalY > content.height + 1f)
                continue;
            float maxY = Math.Max(0f, content.height - viewport.height);
            float clampedY = ClampFloat(contentLocalY, 0f, maxY);
            Vector2 current = ctx.Session.GetScrollPosition(entry.Node);
            ctx.Session.SetScrollPosition(entry.Node, new Vector2(current.x, clampedY));
            ctx.Session.ClearScrollTarget();
            return;
        }
    }

    /// <summary>
    /// Folds every announcement the page made since the last arrange into one commit at the frame
    /// boundary: per key, the declared invalidation class decides what moves.
    /// <see cref="UiInvalidation.Paint"/> costs nothing here - the next paint pass reads the new value
    /// and the arranged geometry is reused as it stands, which is what keeps a fixed-size readout from
    /// re-measuring the page. <see cref="UiInvalidation.Measure"/> and
    /// <see cref="UiInvalidation.Structure"/> mark the nodes that declared the key dirty, and the
    /// arrangement clock moves once for the whole batch.
    /// <para>
    /// One comparison per declared key per arrange is the whole cost, so however many announcements
    /// arrived during a pass and however many times one key was announced, the element is marked at most
    /// once (<see cref="UiNode.MarkDirty"/> is idempotent) and the batch produces exactly one commit.
    /// </para>
    /// <para>
    /// <b>What is load-bearing today, stated rather than assumed.</b> The page-wide snapshot cache cannot
    /// tell one element from another, so the clock bump alone already forces the re-arrange a Measure or
    /// Structure announcement needs, and the per-node flags are currently redundant with it: marking the
    /// affected nodes is the seam element-level measure reuse will need, and removing only those calls
    /// changes nothing observable yet (measured, 2026-09-15). What <i>is</i> observable, and what the
    /// invalidation lane pins, is the rest of the decision: which keys are polled at all (declared keys
    /// only - an announcement for a key no arranged element declares invalidates nothing), and the class
    /// the key declared (Paint moves neither a node nor the clock). Do not delete the flags on the grounds
    /// that the clock covers them; delete them with the reuse that makes them matter.
    /// </para>
    /// </summary>
    private void CommitNotifications(UiWidgetContext ctx)
    {
        if (keyNodes.Count == 0) return;

        bool arrangementMoved = false;
        foreach (KeyValuePair<string, List<UiNode>> entry in keyNodes)
        {
            int current = ctx.Bindings.GetRevision(entry.Key);
            if (!committedRevisions.TryGetValue(entry.Key, out int committed) || committed == current)
            {
                continue;
            }

            committedRevisions[entry.Key] = current;

            UiInvalidation invalidates = ctx.Bindings.GetInvalidation(entry.Key);
            if ((invalidates & (UiInvalidation.Measure | UiInvalidation.Structure)) == 0)
            {
                // Paint-class: the value is read again by the draw pass, nothing here moves.
                continue;
            }

            arrangementMoved = true;
            bool structural = (invalidates & UiInvalidation.Structure) != 0;
            for (int i = 0; i < entry.Value.Count; i++)
            {
                UiNode node = entry.Value[i];
                node.MarkDirty();
                if (structural)
                {
                    // Structure says the element's slot may change shape - it appears, disappears or stops
                    // occupying its old band - so the container that owns the slot reflows with it.
                    // Measure alone is the element's own geometry.
                    node.Parent?.MarkDirty();
                }
            }
        }

        if (arrangementMoved)
        {
            ctx.Session.BumpContentRevision();
        }
    }

    /// <summary>
    /// Records which announced binding keys one element declares and who owns them, so the next commit
    /// can target nodes instead of the page. An element that is not arranged this pass is recorded
    /// against its nearest arranged ancestor by the caller, which is how a hidden element's VisibleKey
    /// or Tab still brings it back.
    /// </summary>
    private void RecordDeclaredKeys(UiElementSpec spec, UiNode owner, IUiBindings bindings)
    {
        RecordKey(ReadDeclaredBindKey(spec), owner, bindings);
        RecordKey(ReadAttribute(spec, ActionBindAttribute), owner, bindings);
        RecordKey(ReadAttribute(spec, OptionsBindAttribute), owner, bindings);
        if (ReadAttribute(spec, TabAttribute).Length > 0)
        {
            // A Tab-gated element does not depend on its own tab name; it depends on the one shared
            // active-tab binding, whatever name the author wrote.
            RecordKey(UiBindings.ActiveTabKey, owner, bindings);
        }

        RecordKey(ReadAttribute(spec, VisibleKeyAttribute), owner, bindings);

        // B5: a WidthKey declaration depends on its own binding, so an announcement on it re-arranges the
        // node that carries it - the same registration VisibleKey gets, one line, and the reason the numeric
        // form is not a second mechanism.
        RecordKey(ReadAttribute(spec, WidthKeyAttribute), owner, bindings);

        // A collection element declares the binding its row set comes from. Without it an announcement on
        // that key would reach no node, and an insert, removal or reorder would leave the arranged rows as
        // stale as a page that announced nothing - the exact refresh the keyed repeater exists to remove.
        // Every other element answers "" here, so this costs one lookup on each.
        RecordKey(ReadAttribute(spec, RepeatItemsAttribute), owner, bindings);
    }

    private void RecordKey(string key, UiNode owner, IUiBindings bindings)
    {
        if (key.Length == 0) return;
        if (!committedRevisions.ContainsKey(key))
        {
            // First sight: this arrange is already using the key's current value, so its revision is
            // committed here and only a later announcement invalidates the element.
            committedRevisions[key] = bindings.GetRevision(key);
        }

        if (!keyNodes.TryGetValue(key, out List<UiNode>? owners))
        {
            owners = new List<UiNode>();
            keyNodes.Add(key, owners);
        }

        if (!owners.Contains(owner))
        {
            owners.Add(owner);
        }
    }

    /// <summary>
    /// The key a value atom reads for this element: the <c>Bind</c> attribute when declared, else the
    /// element's own <c>Id</c>. That fallback is the documented atom contract, not a second key space.
    /// </summary>
    private static string ReadDeclaredBindKey(UiElementSpec spec)
    {
        string bind = ReadAttribute(spec, BindAttribute);
        return bind.Length > 0 ? bind : spec.Id.Trim();
    }

    private static string ReadAttribute(UiElementSpec spec, string attribute)
    {
        return spec.TryGetAttribute(attribute, out string value) ? value.Trim() : "";
    }

    /// <summary>
    /// The element's disabled state: the command it declares as <c>ActionBind</c> answers
    /// <see cref="IUiBindings.CanExecute"/> false. An element that declares no command is never
    /// disabled, so this cannot change the behaviour of a page that binds no commands. The answer is
    /// published on the node because that is where the funnel's interactive entry points read it: one
    /// rule for every kind instead of a per-widget branch.
    /// </summary>
    private static bool ResolveDisabled(UiElementSpec spec, UiWidgetContext ctx)
    {
        string commandKey = ReadAttribute(spec, ActionBindAttribute);
        return commandKey.Length > 0 && !ctx.Bindings.CanExecute(commandKey);
    }

    public void Draw(UiWidgetContext ctx, UiLayoutSnapshot snapshot, Rect viewport)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        // One hit pass per draw: the previous pass's complete paint order becomes the stack input dispatch
        // reads, and this pass builds the next one as its elements draw.
        ctx.Session.BeginHitPass();

        // The wheel belongs to the open menu before it belongs to anything under it: a layer covering the point
        // outranks what is beneath it. Asking here - after the pass opened, so the layers are the ones
        // the last pass published, and before any element drew, so no native scroll view has consumed the
        // notch yet - is what makes that priority real instead of aspirational: the popup pass runs AFTER
        // content, and a menu over a scrolled page would otherwise never see a wheel event at all. When the
        // menu moves, the event is consumed and this frame's scroll containers keep their position.
        UiNative.DismissPopupOnOutsidePress(ctx.Session);
        UiNative.TakePopupWheelIfCovering(ctx.Session);

        try
        {
            var contentLayers = new List<UiPointerLayer>();
            CollectPointerLayers(lastEntries, 0, lastEntries.Count, ctx, new Vector2(viewport.x, viewport.y), null, Vector2.zero, ctx.Session.CurrentClip, contentLayers);
            ctx.Session.SetContentInputLayers(contentLayers);
            DrawEntries(lastEntries, 0, lastEntries.Count, ctx, new Vector2(viewport.x, viewport.y), null, Vector2.zero);
            if (ctx.Session.IsActive) ctx.Session.EndHitPass();
        }
        catch
        {
            ctx.Session.AbortHitPass();
            throw;
        }
    }

    private void CollectPointerLayers(List<PlacedEntry> entries, int start, int end, UiWidgetContext ctx,
        Vector2 viewportPosition, Vector2? nativeOrigin, Vector2 windowOrigin, Rect? clip, List<UiPointerLayer> layers)
    {
        for (int index = start; index < end; index++)
        {
            PlacedEntry entry = entries[index];
            Rect rect = ToDrawRect(entry.Rect, viewportPosition, nativeOrigin);
            UiWidgetContext local = ContextForEntry(entry, ctx, windowOrigin);
            entry.Node.IsDisabled = ResolveDisabled(entry.Spec, local);
            if (entry.Widget != null)
            {
                // Even an undescribed node has a paint-order marker; it does not acquire an opaque hit box.
                layers.Add(new UiPointerLayer(entry.Node.Id.Key, default, hasOperation: false));
                if (entry.Widget is IUiPointerLayout layout && !ctx.Session.IsTripped(entry.Node))
                {
                    var regions = new List<UiPointerRegion>();
                    UiNode previous = ctx.Session.EnterNode(entry.Node);
                    try
                    {
                        UiSessionGuard.MeasureOrFallback(ctx.Session, entry.Node.Path, entry.Widget.Kind,
                            entry.Node.Path, 0, () =>
                            {
                                regions.AddRange(layout.PointerRegions(local, new UiLayoutRect(0, 0, rect.width, rect.height)));
                                return 0;
                            });
                    }
                    finally
                    {
                        ctx.Session.ExitNode(previous);
                    }
                    if (ctx.Session.IsTripped(entry.Node)) regions.Clear();
                    foreach (UiPointerRegion region in regions)
                    {
                        UiLayoutRect bounds = region.Bounds;
                        var windowBounds = new UiLayoutRect(rect.x + windowOrigin.x + bounds.X,
                            rect.y + windowOrigin.y + bounds.Y, bounds.Width, bounds.Height);
                        UiLayoutRect? effectiveClip = clip.HasValue
                            ? new UiLayoutRect(clip.Value.x, clip.Value.y, clip.Value.width, clip.Value.height) : null;
                        layers.Add(new UiPointerLayer(entry.Node.Id.Key, windowBounds, effectiveClip,
                            region.HasOperation));
                    }
                }
            }
            if (!IsScopedContainer(entry.ContainerKind)) continue;
            Vector2 scopeOrigin = ScopeWindowOrigin(rect, nativeOrigin, windowOrigin);
            Rect scopeRect = new(scopeOrigin.x, scopeOrigin.y, rect.width, rect.height);
            Rect childClip = IntersectClip(clip, scopeRect);
            Vector2 childOrigin = scopeOrigin;
            if (entry.ContainerKind == "Scroll")
            {
                Rect content = entry.ContentRect ?? new Rect(0, 0, rect.width, rect.height);
                Vector2 scroll = ClampScroll(ctx.Session.GetScrollPosition(entry.Node), content, rect);
                childOrigin -= scroll;
            }
            CollectPointerLayers(entries, index + 1, index + entry.SubtreeCount, local,
                viewportPosition, entry.Rect.position, childOrigin, childClip, layers);
            index += entry.SubtreeCount - 1;
        }
    }

    private UiWidgetContext ContextForEntry(PlacedEntry entry, UiWidgetContext ctx, Vector2 windowOrigin)
    {
        UiWidgetContext local = ctx.WithStyleChain(entry.StyleChain);
        local = local.WithTheme(ScopeTheme(local, entry.Node));
        if (entry.MeasureWidth > 0) local = local.WithViewWidth(entry.MeasureWidth);
        return local.WithWindowOrigin(windowOrigin).WithNode(entry.Node);
    }

    private static Vector2 ScopeWindowOrigin(Rect rect, Vector2? nativeOrigin, Vector2 windowOrigin)
        => nativeOrigin.HasValue ? new Vector2(windowOrigin.x + rect.x, windowOrigin.y + rect.y) : rect.position;

    private static Rect IntersectClip(Rect? outer, Rect inner)
    {
        if (!outer.HasValue) return inner;
        float left = Math.Max(outer.Value.x, inner.x);
        float top = Math.Max(outer.Value.y, inner.y);
        float right = Math.Min(outer.Value.xMax, inner.xMax);
        float bottom = Math.Min(outer.Value.yMax, inner.yMax);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
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
            UiWidgetContext entryCtx = ContextForEntry(entry, ctx, windowOrigin);

            // Publish the element's disabled state before anything paints or hit-tests: the funnel's
            // interactive entry points read it, so a disabled element neither executes nor captures the
            // pointer. Resolved once per element per draw from the command the element declares.
            entry.Node.IsDisabled = ResolveDisabled(entry.Spec, entryCtx);

            // FL-IC2: the same one-per-draw publication for the Cancel ladder. Read from the entry's own spec,
            // which inside a Repeat row is the item-scoped one, so a row's Cancel names that row's command and
            // not a page-level key the row happens to share a name with.
            entry.Node.CancelBindingKey = ReadAttribute(entry.Spec, CancelBindAttribute);

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

#if FER_DEV
            // The development-only numeric instrument reads the geometry from this one point per entry:
            // the arranged rect and the draw rect disagree exactly when a scroll/group origin is involved,
            // which is the disagreement a reader must be able to see rather than infer. Identity, appearance
            // and provenance come from what has already resolved for this entry - the node the engine just
            // published, the theme its own chain resolved to, and the session's effective clip - so the
            // sample describes the pass that ran instead of a second derivation of it. Nothing is recorded
            // when the subscription did not opt in (the probe returns before it touches the resolver), and
            // nothing at all exists in a release build. The answer says whether the bounded capture kept
            // this entry, which is what the outline is faithful to WHILE a capture is live; with no capture
            // the same call still paints when the standalone outline switch is on (SA1.5). The outline now
            // covers every entry that reaches this step - the scoped containers included, at their own
            // draw rect after their content (DT1 closed the stated viewport limit).
            bool sampled = UiDevGeometryProbe.Note(
                entry.Node,
                entry.Spec,
                entry.ContainerKind,
                entry.IsContainer,
                IsScopedContainer(entry.ContainerKind),
                entry.Widget != null,
                entry.Rect,
                entry.ContentRect,
                drawRect,
                entryCtx,
                !ReferenceEquals(entryCtx.Theme, ctx.Theme),
                styleResolver,
                entryCtx.StyleChain);
#endif

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
                if (!ctx.Session.IsActive) return;
#if FER_DEV
                // DT1: the viewport's OWN band is outlined here, after its content has drawn and in this
                // scope's own draw-local space - the same rect, the same entry, the same `sampled` answer
                // Note() gave above. The nested walk below never had a step at this entry, which is how
                // the clipped edges went unseen; one moved call makes the picture the third rendering of
                // this entry too, and no second collector exists to disagree with the capture.
                UiDevGeometryProbe.Outline(drawRect, sampled);
#endif
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
                            // The element's declared help identity, claimed while the pointer is over it and
                            // BEFORE the widget draws - one rule for every kind, so a page that declares HelpKey
                            // gets an inspect overlay without a line of per-widget code. It is claimed inside
                            // EnterNode because that is what attributes the claim to THIS element: a claim made
                            // outside would be attributed to whatever node was active before. A widget that owns
                            // finer parts (a mode row's options) then refines the claim in its own Draw, where
                            // the more specific topic wins because a live claim replaces the held one in the same
                            // pass. Two things this deliberately does not do: it does not translate or interpret
                            // the value (it is the identity a consumer's own catalog is keyed by), and it does not
                            // refuse a DISABLED element - help explains why a control is unavailable rather than
                            // activating it, and the disabled refusal stays where input is refused
                            // (UiNative.Button). Only widgets claim: a container is not a hit surface, which is
                            // why the attribute is refused on one at creation instead of being inert here.
                            string helpKey = ReadAttribute(entry.Spec, HelpKeyAttribute);
                            if (helpKey.Length > 0 && UiNative.IsMouseOver(drawRect, entryCtx))
                            {
                                ctx.Session.ClaimHover(helpKey);
                            }

                            UiSessionGuard.DrawWidget(entry.Widget, drawRect, entryCtx, entry.Node);
                            if (!ctx.Session.IsActive) return;
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

#if FER_DEV
                // The optional outline is painted LAST for this entry, after the fit audit closed and after
                // the element drew, in the element's own draw-local space: an outline that could feed back
                // into the thing it measures would be the instrument changing the measurement. It paints only
                // what the capture retained, so the picture cannot show a node the data does not describe -
                // and with capture off (SA1.5) it paints this same walk and stores nothing.
                UiDevGeometryProbe.Outline(drawRect, sampled);
#endif

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
            Vector2 scopeWindowOrigin = ScopeWindowOrigin(outRect, nativeOrigin, windowOrigin);
            Rect? parentClip = ctx.Session.CurrentClip;
            ctx.Session.ConstrainClip(new Rect(scopeWindowOrigin.x, scopeWindowOrigin.y, outRect.width, outRect.height));

            try
            {
                VerseWidgets.BeginScrollView(outRect, ref scrollPosition, contentRect);
                // BeginScrollView may consume a wheel event and change the offset in this very pass.
                Vector2 childWindowOrigin = new(scopeWindowOrigin.x - scrollPosition.x, scopeWindowOrigin.y - scrollPosition.y);
                DrawEntries(entries, childStart, childEnd, ctx, viewportPosition, entry.Rect.position, childWindowOrigin);
            }
            finally
            {
                if (ctx.Session.IsActive)
                {
                    ctx.Session.CurrentClip = parentClip;
                    ctx.Session.SetScrollPosition(entry.Node, scrollPosition);
                }
                VerseWidgets.EndScrollView();
            }

            return;
        }

        // Clip and Overlay are structural groups. The engine owns Begin/EndGroup so leaf widgets
        // never create native structure scopes. Children draw relative to the group origin, so
        // their window-space offset is the group's draw position on top of the parent origin.
        Vector2 groupWindowOrigin = ScopeWindowOrigin(outRect, nativeOrigin, windowOrigin);
        Rect? outerClip = ctx.Session.CurrentClip;
        ctx.Session.ConstrainClip(new Rect(groupWindowOrigin.x, groupWindowOrigin.y, outRect.width, outRect.height));
        try
        {
            GUI.BeginGroup(outRect);
            DrawEntries(entries, childStart, childEnd, ctx, viewportPosition, entry.Rect.position, groupWindowOrigin);
        }
        finally
        {
            if (ctx.Session.IsActive) ctx.Session.CurrentClip = outerClip;
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
        UiLayoutPoint clamped = UiLayout.ClampScroll(new UiLayoutPoint(position.x, position.y), new UiLayoutSize(content.width, content.height), new UiLayoutSize(viewport.width, viewport.height));
        return new Vector2(clamped.X, clamped.Y);
    }

    private static float ClampFloat(float value, float min, float max)
    {
        return UiLayout.Clamp(value, min, max);
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
    private UiWidgetContext ElementScope(UiWidgetContext ctx, UiElementSpec spec, UiNode node)
    {
        node.Properties.Authoring(spec, styleResolver?.Document);
        UiWidgetContext scoped = ctx.WithStyleDeclaration(DeclarationOf(spec));
        return scoped.WithTheme(ScopeTheme(scoped, node));
    }

    /// <summary>
    /// The theme one chain resolves to. An empty chain is the page level: there the answer is the injected
    /// theme itself, which already carries the document's page-level defaults because the host applies them
    /// before the first arrangement - asking the resolver for a clone would print a different instance for
    /// the same values. A declared scope resolves through the resolver, which builds each effective
    /// (scheme, density) pair once and hands out that same instance on every later frame.
    /// </summary>
    private UiTheme ScopeTheme(UiWidgetContext ctx, UiNode node)
    {
        IReadOnlyList<UiStyleDeclaration>? chain = ctx.StyleChain;
        UiFont fallbackFont = UiWidgetRegistry.GetFallbackFont(ctx.Source, node.Kind);
        if (styleResolver == null || chain == null || chain.Count == 0) return node.Properties.Apply(node, styleResolver?.Baseline ?? ctx.Theme, fallbackFont);

        // The scope NAME is the resolver's to read, and only the tree knows where it was written: handing the
        // element's path down is what makes an unknown name a located finding instead of "some scheme is
        // missing somewhere in this document".
        styleResolver.ThemeFor(chain, node.Id.Path); // Preserve located unknown-name diagnostics.
        return node.Properties.Apply(node, styleResolver.Baseline, fallbackFont);
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

    private MeasuredBox MeasureElement(
        UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight, UiNode node,
        float contentHeightReference = NoContentHeightReference)
    {
        // The element's own style scope comes first: its declaration joins the chain and the theme below
        // every branch resolves against the chain, so a widget measures - and later draws - with the theme
        // its own scope resolved to. Containers are scoped the same way, which is what lets a region's
        // font reach the label widths measured inside it.
        ctx = ElementScope(ctx, spec, node);

        // Every arranged element declares its binding keys to the engine here, so the next announcement
        // of any of them can target this node instead of the page. An element that is not arranged this
        // pass is recorded against its nearest arranged ancestor by the caller instead.
        RecordDeclaredKeys(spec, node, ctx.Bindings);

        // The content-relative height mode. A parent that can answer has already handed the height its own
        // content resolved to (the declaring element contributed nothing to it); everywhere else - a vertical
        // flow, a Wrap line, a template the Host does not walk - the mode degrades to the element's own measured
        // content, exactly as Auto does, so a programmatic spec cannot take a frame down.
        bool matchContent = UiPlacement.IsMatchContentHeight(spec);
        float? reference = contentHeightReference >= 0f ? contentHeightReference : null;
        if (matchContent && !reference.HasValue)
            ReportLayout(node, "ContentHeightUnavailable", "The retained raw/template entry uses natural height because this parent supplies no independent content-height reference.");
        if (matchContent && reference.HasValue)
        {
            // The element's own content is not the geometry here, and for a container the reference is also the
            // height its inner Fill children may claim.
            availableHeight = reference.Value;
        }

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
                height = ResolveHeight(spec, widget, measureCtx, width, node, matchContent ? reference : null);
                if (!ctx.Session.IsTripped(node) && widget is IUiLayoutDiagnosticSource diagnosticSource)
                {
                    UiSessionGuard.MeasureOrFallback(ctx.Session, node.Path, widget.Kind, node.Path, 0, () =>
                    {
                        UiLayoutDiagnostic? diagnostic = diagnosticSource.DiagnoseLayout(measureCtx, height);
                        if (diagnostic.HasValue) ReportLayout(node, diagnostic.Value.Code, diagnostic.Value.Message);
                        return 0;
                    });
                }
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

        MeasuredBox container = MeasureContainer(ctx, spec, width, availableHeight, node);
        if (matchContent && reference.HasValue)
        {
            StretchToContent(container, node.Id, reference.Value);
        }

        return container;
    }

    /// <summary>
    /// Sets a container's own box - and the entry it published - to the height its parent's content resolved to.
    /// Its children keep the layout their own content produced, top-aligned in the taller box, which is what any
    /// container does with space its content does not fill.
    /// </summary>
    private static void StretchToContent(MeasuredBox box, UiNodeId id, float height)
    {
        box.Height = height;
        for (int i = 0; i < box.Entries.Count; i++)
        {
            PlacedEntry entry = box.Entries[i];
            if (entry.Id.Equals(id))
            {
                entry.Rect = new Rect(entry.Rect.x, entry.Rect.y, entry.Rect.width, height);
                return;
            }
        }
    }

    private MeasuredBox MeasureContainer(UiWidgetContext ctx, UiElementSpec spec, float width, float availableHeight, UiNode node)
    {
        string declaredKind = GetContainerKind(spec);
        Padding padding = ParsePadding(spec, ctx.Theme);
        float gap = ReadGap(spec, ctx.Theme);
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

        if (string.Equals(kind, "Repeat", StringComparison.Ordinal))
        {
            return MeasureRepeat(ctx, spec, declaredKind, width, padding, gap, titleHeight, innerWidth, innerY, availableHeight, node);
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
    /// <summary>
    /// Drops the widget instances whose identity the session no longer holds. A removed element's widget
    /// would otherwise stay alive in this table holding whatever a consumer's factory closed over.
    /// </summary>
    private void PruneWidgetInstances(UiSession session)
    {
        List<UiNodeId>? gone = null;
        foreach (UiNodeId id in widgetInstances.Keys)
        {
            if (session.GetNode(id) == null)
            {
                (gone ??= new List<UiNodeId>()).Add(id);
            }
        }

        if (gone == null) return;
        for (int i = 0; i < gone.Count; i++)
        {
            widgetInstances.Remove(gone[i]);
        }
    }

    private static UiNode ChildNode(UiWidgetContext ctx, UiNode parent, UiElementSpec spec, int declaredIndex)
    {
        UiNode child = ctx.Session.GetOrCreateElementNode(
            parent.Id.Child(spec, declaredIndex), spec, declaredIndex);
        parent.AddChild(child);
        return child;
    }

    private List<ChildSlot> VisibleChildren(UiElementSpec spec, UiWidgetContext ctx, bool narrow, UiNode parent)
    {
        var visible = new List<ChildSlot>();
        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            if (IsHidden(child, ctx, narrow, parent, i))
            {
                // A hidden element owns no arranged node, so its declared keys report to the container
                // that would hold its slot. That is what makes its VisibleKey (or Tab) able to bring it
                // back: the container is marked dirty and the page re-arranges with the element in it.
                RecordDeclaredKeys(child, parent, ctx.Bindings);
                continue;
            }

            visible.Add(new ChildSlot(child, i));
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

    private static UiLayoutFrame LayoutFrame(UiElementSpec spec, float width, float availableHeight, Padding padding, float title, float gap) => new(width, availableHeight, padding.Left, padding.Top, padding.Right, padding.Bottom, title, gap, ReadLayoutHeight(spec));
    private static UiLayoutChild LayoutChild(UiElementSpec spec, float width) => new(width, IsFlexibleFill(spec), UiPlacement.ParseAlign(spec, UiPlacement.AlignXAttribute, UiPlacement.Axis.Horizontal).Fraction, UiPlacement.ParseAlign(spec, UiPlacement.AlignYAttribute, UiPlacement.Axis.Vertical).Fraction, UiPlacement.ParseOffsetValue(spec, UiPlacement.OffsetXAttribute), UiPlacement.ParseOffsetValue(spec, UiPlacement.OffsetYAttribute), UiPlacement.IsMatchContentHeight(spec));
    private void ReportLayout(UiNode node, string code, string message)
    {
        const int limit = 128;
        if (layoutDiagnostics.Count < limit)
            layoutDiagnostics.Add(new UiLayoutDiagnostic(node.Path, code, message));
        else if (layoutDiagnostics.Count == limit)
            layoutDiagnostics.Add(new UiLayoutDiagnostic(node.Path, "Truncated", "Additional layout diagnostics omitted."));
    }

    private UiLayoutMeasure ChildMeasure(UiWidgetContext ctx, UiNode parent, UiElementSpec parentSpec, IReadOnlyList<ChildSlot> children, MeasuredBox[] measured)
    {
        var nodes = new UiNode[children.Count];
        for (int i = 0; i < children.Count; i++)
            nodes[i] = ChildNode(ctx, parent, children[i].Spec, children[i].DeclaredIndex);
        if (parentSpec.TryGetAttribute("SharedLabels", out string shared) && string.Equals(shared, "true", StringComparison.OrdinalIgnoreCase))
        {
            var participants = new List<UiNode>();
            var demands = new List<float>();
            for (int i = 0; i < children.Count; i++)
            {
                UiElementSpec child = children[i].Spec;
                if (GetContainerKind(child).Length > 0)
                    continue;
                IUiWidget widget = GetOrCreateWidget(child, nodes[i].Id);
                if (widget is not IUiLabelledWidget labelled)
                    continue;
                UiWidgetContext childContext = ElementScope(ctx.WithNode(nodes[i]), child, nodes[i]);
                demands.Add(labelled.MeasureLabel(childContext));
                participants.Add(nodes[i]);
            }

            float column = UiLayout.SharedLabelColumn(demands);
            foreach (UiNode participant in participants)
                participant.SharedLabelWidth = column;
        }

        return (index, width, availableHeight, reference) =>
        {
            MeasuredBox box = MeasureElement(ctx, children[index].Spec, width, availableHeight, nodes[index], reference ?? NoContentHeightReference);
            measured[index] = box;
            return new UiLayoutSize(box.Width, box.Height);
        };
    }

    private MeasuredBox AssembleLayout(UiWidgetContext ctx, UiElementSpec spec, string kind, UiNode node, UiLayoutPlan plan, IReadOnlyList<MeasuredBox> measured)
    {
        if (plan.HeightOverflow)
            ReportLayout(node, "HeightOverflow", "Natural content height " + plan.NaturalHeight + " exceeds allocated height " + plan.Size.Height + ".");
        foreach (UiLayoutRect slot in plan.Slots)
        {
            bool outside = slot.X < -.01f || slot.Y < -.01f || slot.X + slot.Width > plan.Size.Width + .01f || slot.Y + slot.Height > plan.Size.Height + .01f;
            if (outside)
            {
                ReportLayout(node, "ChildOverflow", "A direct child lies outside the allocated container; clipping/scrolling remains the declared container's policy.");
                break;
            }
        }

        var box = new MeasuredBox
        {
            Width = plan.Size.Width,
            Height = plan.Size.Height
        };
        var container = new PlacedEntry
        {
            Spec = spec,
            Id = node.Id,
            Node = node,
            StyleChain = ctx.StyleChain,
            IsContainer = true,
            ContainerKind = kind,
            MeasureWidth = plan.Size.Width,
            Rect = new Rect(0, 0, plan.Size.Width, plan.Size.Height)
        };
        box.Entries.Add(container);
        for (int i = 0; i < measured.Count; i++)
            box.Entries.AddRange(OffsetBox(measured[i], plan.Slots[i].X, plan.Slots[i].Y).Entries);
        container.SubtreeCount = box.Entries.Count;
        return box;
    }

    private MeasuredBox MeasureStack(UiWidgetContext ctx, UiElementSpec spec, string kind, float width, Padding padding, float gap, float titleHeight, float innerWidth, float innerY, float availableHeight, UiNode node, bool narrow)
    {
        List<ChildSlot> visible = VisibleChildren(spec, ctx, narrow, node);
        var children = new UiLayoutChild[visible.Count];
        var measured = new MeasuredBox[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            UiNode childNode = ChildNode(ctx, node, visible[i].Spec, visible[i].DeclaredIndex);
            UiWidgetContext childCtx = ElementScope(ctx.WithNode(childNode), visible[i].Spec, childNode);
            bool stretch = UiPlacement.ParseAlign(visible[i].Spec, UiPlacement.AlignXAttribute, UiPlacement.Axis.Horizontal).IsStretch;
            float childWidth = stretch ? ResolveStackChildWidth(visible[i].Spec, innerWidth, childCtx) : ResolveWrapWidth(visible[i].Spec, innerWidth, childCtx);
            children[i] = LayoutChild(visible[i].Spec, childWidth);
        }

        UiLayoutPlan plan = UiLayout.Stack(LayoutFrame(spec, width, availableHeight, padding, titleHeight, gap), children, ChildMeasure(ctx, node, spec, visible, measured));
        return AssembleLayout(ctx, spec, kind, node, plan, measured);
    }

    private MeasuredBox MeasureRow(UiWidgetContext ctx, UiElementSpec spec, string kind, float width, Padding padding, float gap, float titleHeight, float innerWidth, float innerY, float availableHeight, UiNode node, bool narrow)
    {
        List<ChildSlot> visible = VisibleChildren(spec, ctx, narrow, node);
        float[] widths = ResolveColumnWidths(visible, innerWidth, gap, ctx, node);
        var children = new UiLayoutChild[visible.Count];
        var measured = new MeasuredBox[visible.Count];
        for (int i = 0; i < visible.Count; i++)
            children[i] = LayoutChild(visible[i].Spec, widths[i]);
        UiLayoutPlan plan = UiLayout.Row(LayoutFrame(spec, width, availableHeight, padding, titleHeight, gap), children, ChildMeasure(ctx, node, spec, visible, measured));
        return AssembleLayout(ctx, spec, kind, node, plan, measured);
    }

    private MeasuredBox MeasureWrap(UiWidgetContext ctx, UiElementSpec spec, string kind, float width, Padding padding, float gap, float titleHeight, float innerWidth, float innerY, float availableHeight, UiNode node, bool narrow)
    {
        List<ChildSlot> visible = VisibleChildren(spec, ctx, narrow, node);
        int columns = ResolveWrapCols(spec, narrow);
        float cellWidth = columns > 0 ? Math.Max(1, (innerWidth - gap * (columns - 1)) / columns) : 0;
        var children = new UiLayoutChild[visible.Count];
        var measured = new MeasuredBox[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            UiNode childNode = ChildNode(ctx, node, visible[i].Spec, visible[i].DeclaredIndex);
            UiWidgetContext childCtx = ElementScope(ctx.WithNode(childNode), visible[i].Spec, childNode);
            float childWidth = columns > 0 ? cellWidth : ResolveWrapWidth(visible[i].Spec, innerWidth, childCtx);
            children[i] = LayoutChild(visible[i].Spec, childWidth);
        }

        UiLayoutPlan plan = UiLayout.Wrap(LayoutFrame(spec, width, availableHeight, padding, titleHeight, gap), children, columns, ChildMeasure(ctx, node, spec, visible, measured));
        return AssembleLayout(ctx, spec, kind, node, plan, measured);
    }

    /// <summary>
    /// The placement container (CP-1): every child resolves its own position against this container's inner
    /// box, so this is the one place in the engine where both axes may be named and where an offset may be a
    /// percentage of the parent's span.
    /// <para>
    /// The measurement width comes from the child's <c>AlignX</c>: a Stretch child keeps the whole inner
    /// width, which is the slot this container has always handed its children, while a named edge or centre
    /// measures the child at its own envelope (a declared width, or an Auto label width). The vertical
    /// placement needs the container's final height, which is only known once every child has been measured
    /// (a fixed Height or Fill makes it taller than its content), so children are measured first and offset
    /// in a second pass. With no vocabulary at all both axes resolve to fraction zero and every child lands
    /// exactly where flow put it.
    /// </para>
    /// </summary>
    private static UiLayoutAnchor?[] ReadAnchors(IReadOnlyList<ChildSlot> children, UiPlacement.Axis axis)
    {
        var definitions = new UiElementSpec[children.Count];
        for (int i = 0; i < definitions.Length; i++) definitions[i] = children[i].Spec;
        return UiPlacement.ReadAnchors(definitions, axis);
    }
    private MeasuredBox MeasureOverlay(UiWidgetContext ctx, UiElementSpec spec, string kind, float width, Padding padding, float titleHeight, float innerWidth, float innerY, float availableHeight, UiNode node, bool narrow)
    {
        List<ChildSlot> visible = VisibleChildren(spec, ctx, narrow, node);
        var children = new UiLayoutChild[visible.Count];
        var measured = new MeasuredBox[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            UiNode childNode = ChildNode(ctx, node, visible[i].Spec, visible[i].DeclaredIndex);
            UiWidgetContext childCtx = ElementScope(ctx.WithNode(childNode), visible[i].Spec, childNode);
            bool stretch = UiPlacement.ParseAlign(visible[i].Spec, UiPlacement.AlignXAttribute, UiPlacement.Axis.Horizontal).IsStretch;
            float childWidth = stretch ? ClampDeclaredWidth(visible[i].Spec, innerWidth) : ResolveWrapWidth(visible[i].Spec, innerWidth, childCtx);
            children[i] = LayoutChild(visible[i].Spec, childWidth);
        }

        UiLayoutPlan plan = UiLayout.Overlay(LayoutFrame(spec, width, availableHeight, padding, titleHeight, 0), children, ChildMeasure(ctx, node, spec, visible, measured));
        try
        {
            plan = UiLayout.Anchored(plan, LayoutFrame(spec, width, availableHeight, padding, titleHeight, 0), ReadAnchors(visible, UiPlacement.Axis.Horizontal), ReadAnchors(visible, UiPlacement.Axis.Vertical));
        }
        catch (ArgumentException ex)
        {
            throw new FormatException("Overlay '" + node.Path + "': " + ex.Message, ex);
        }

        return AssembleLayout(ctx, spec, kind, node, plan, measured);
    }

    private MeasuredBox MeasureScroll(UiWidgetContext ctx, UiElementSpec spec, string kind, float width, Padding padding, float gap, float titleHeight, float innerWidth, float innerY, float availableHeight, UiNode node, bool narrow)
    {
        var entries = new List<PlacedEntry>();
        var tops = new List<PlacedEntry>();
        float naturalContentHeight = MeasureScrollContent(ctx, spec, innerWidth, padding, gap, innerY, availableHeight, node, entries, narrow, tops);
        float naturalHeight = padding.Top + titleHeight + naturalContentHeight + padding.Bottom;
        // D4: `VisibleRows` is a third answer for a Scroll's own height, and the closed vocabulary stays
        // static-first - a declared numeric Height behaves exactly as it always did and the count is never
        // consulted. When the count answers, the budget is READ from the row-root rects this pass has
        // already measured: no consumer font/width chain is copied, and nothing waits for a previous
        // frame's write-back, because the children were measured before this line.
        bool counting = TryViewportRows(spec, out float rows);
        float viewportHeight = counting ? ViewportRowsBudget(tops, rows, padding, naturalHeight) : ResolveContainerHeight(spec, naturalHeight, availableHeight);
        // A vertical scrollbar is drawn inside the right edge of the viewport only when the
        // natural content height exceeds the viewport. Reserve its width then (and only then) so
        // content is measured and arranged against the actually visible area: pure vertical
        // overflow must never create a horizontal scrollbar, and a non-overflowing scroll keeps
        // the full viewport width. Children are re-measured at the reserved width so the arranged
        // rects, the published content rect and the BeginScrollView view rect all agree.
        float contentWidth = UiLayout.ScrollContentWidth(width, naturalHeight, viewportHeight, ScrollbarWidth);
        if (contentWidth < width)
        {
            float reservedInnerWidth = Math.Max(1f, contentWidth - padding.Left - padding.Right);
            entries.Clear();
            tops.Clear();
            float reflowedContentHeight = MeasureScrollContent(ctx, spec, reservedInnerWidth, padding, gap, innerY, availableHeight, node, entries, IsNarrow(spec, reservedInnerWidth), tops);
            naturalHeight = padding.Top + titleHeight + reflowedContentHeight + padding.Bottom;
            if (counting)
            {
                // The narrowed rects are the pass's real rows (wrapped text grows with the narrower
                // width): the budget recomputes FROM them, so a half row costs what the half row
                // actually occupies - never the full-width first round's estimate.
                viewportHeight = ViewportRowsBudget(tops, rows, padding, naturalHeight);
            }
        }

        var box = new MeasuredBox
        {
            Width = width,
            Height = viewportHeight
        };
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
    /// Measures the scroll children at <paramref name = "contentInnerWidth"/> into
    /// <paramref name = "entries"/> and returns the resulting natural content height
    /// (scroll padding and title excluded). <paramref name = "countableTops"/> (D4) collects the SAME
    /// pass's countable entries in flow order: one Repeat expands transparently to its materialized
    /// row roots - never the Repeat as one blob, never a row's title/detail/hover/hit pieces as
    /// several - while any other visible direct child counts as exactly its root entry. A child the
    /// <c>IsHidden</c> pass refuses (the consumer empty-state's VisibleKey half included) contributes no
    /// entry and nothing to count.
    /// </summary>
    private float MeasureScrollContent(UiWidgetContext ctx, UiElementSpec spec, float contentInnerWidth, Padding padding, float gap, float innerY, float availableHeight, UiNode node, List<PlacedEntry> entries, bool narrow, List<PlacedEntry> countableTops)
    {
        List<ChildSlot> visible = VisibleChildren(spec, ctx, narrow, node);
        var children = new UiLayoutChild[visible.Count];
        var measured = new MeasuredBox[visible.Count];
        for (int i = 0; i < visible.Count; i++)
        {
            UiNode childNode = ChildNode(ctx, node, visible[i].Spec, visible[i].DeclaredIndex);
            UiWidgetContext childCtx = ElementScope(ctx.WithNode(childNode), visible[i].Spec, childNode);
            bool stretch = UiPlacement.ParseAlign(visible[i].Spec, UiPlacement.AlignXAttribute, UiPlacement.Axis.Horizontal).IsStretch;
            float childWidth = stretch ? ResolveStackChildWidth(visible[i].Spec, contentInnerWidth, childCtx) : ResolveWrapWidth(visible[i].Spec, contentInnerWidth, childCtx);
            children[i] = LayoutChild(visible[i].Spec, childWidth);
        }

        var frame = new UiLayoutFrame(contentInnerWidth + padding.Left + padding.Right, availableHeight, padding.Left, padding.Top, padding.Right, padding.Bottom, innerY - padding.Top, gap, new UiLayoutHeight(null, false));
        UiLayoutPlan plan = UiLayout.VerticalContent(frame, children, ChildMeasure(ctx, node, spec, visible, measured));
        for (int i = 0; i < visible.Count; i++)
        {
            UiElementSpec child = visible[i].Spec;
            MeasuredBox childBox = OffsetBox(measured[i], plan.Slots[i].X, plan.Slots[i].Y);
            if (string.Equals(child.Kind, "Repeat", StringComparison.Ordinal))
            {
                // Walk the row roots by their subtree spans: after collecting one stamped root, jump its
                // full SubtreeCount. A nested Repeat inside a row stamps its own rows too - without the
                // jump those inner roots would count as extra OUTER entries, which the contract's "one
                // entry per materialized row of THIS scroll" forbids.
                // (e starts at 0: a Repeat is often the scroll's ONLY child, so the first row root is
                // the first entry - skipping it would silently answer "no countable rows".)
                List<PlacedEntry> rowEntries = childBox.Entries;
                for (int e = 0; e < rowEntries.Count;)
                {
                    PlacedEntry candidate = rowEntries[e];
                    if (candidate.IsMaterializedRow)
                    {
                        countableTops.Add(candidate);
                        e += candidate.SubtreeCount > 0 ? candidate.SubtreeCount : 1;
                    }
                    else
                    {
                        e++;
                    }
                }
            }
            else if (childBox.Entries.Count > 0)
            {
                countableTops.Add(childBox.Entries[0]);
            }

            entries.AddRange(childBox.Entries);
        }

        return Math.Max(0, plan.NaturalHeight - frame.Top - frame.Bottom);
    }

    /// <summary>
    /// The <c>VisibleRows</c> gate (D4). A Scroll-only attribute (UiHost refuses the name elsewhere and
    /// refuses bad numbers at creation; the checks here are the belt to that braces), consulted only when
    /// no usable numeric <c>Height</c> is written - the same static-first order <c>WidthKey</c> keeps
    /// against <c>Width</c>. Absent, blank or malformed answers "not declared" so a programmatically
    /// built spec can never crash a frame.
    /// </summary>
    private static bool TryViewportRows(UiElementSpec spec, out float rows)
    {
        rows = 0f;
        if (!spec.TryGetAttribute("VisibleRows", out string raw)) return false;
        string value = (raw ?? "").Trim();
        if (value.Length == 0) return false;
        if (spec.TryGetAttribute("Height", out string heightRaw)
            && float.TryParse((heightRaw ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float _))
        {
            // ANY numeric Height keeps its exact prior meaning, zero included: ResolveContainerHeight
            // answers a declared 0 with a 0-high viewport, and a new attribute must not rewrite that.
            return false;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out rows)
            && rows > 0f && !float.IsNaN(rows) && !float.IsInfinity(rows);
    }

    /// <summary>
    /// The budget from the SAME round's counted rects: the first floor(k) entries reach their measured
    /// bottoms; the fractional part shows that fraction of the NEXT entry's OWN measured height, taken
    /// from the gap the flow already left (the full gap before the next entry stays, so a "half row"
    /// genuinely shows half a row, not half a row minus half a gap); a list shorter than k - or nothing
    /// countable at all - answers natural, and the result is clamped to natural so a promise over rows
    /// that fit never reserves a scrollbar. Positions are the pass's own content-space rects, so the
    /// scroll's padding and title ride in by construction and there is no consumer arithmetic to copy.
    /// After a reserved-scrollbar re-measure this function runs again on the narrowed rects, which is
    /// what keeps "4.5 rows" true for wrapped text at the width the rows actually get.
    /// </summary>
    private static float ViewportRowsBudget(List<PlacedEntry> tops, float rows, Padding padding, float naturalHeight)
    {
        var rects = new UiLayoutRect[tops.Count];
        for (int i = 0; i < tops.Count; i++)
        {
            Rect rect = tops[i].Rect;
            rects[i] = new UiLayoutRect(rect.x, rect.y, rect.width, rect.height);
        }

        return UiLayout.ViewportRows(rects, rows, padding.Bottom, naturalHeight);
    }

    // --- the keyed repeater (P3) -----------------------------------------------------------------
    /// <summary>One materialized row: the consumer's key, its position in the binding, and its subtree.</summary>
    private sealed class ItemRow
    {
        internal string Key = "";
        internal int Index;
        internal UiElementSpec Spec = UiElementSpec.Empty;
    }

    /// <summary>
    /// One Repeat element's materialized row set: the keys the items binding supplied and the derived
    /// per-item subtree each key instantiates. Both are keyed by the item's business key, never by position,
    /// which is what makes a reorder reuse nodes and state and an insertion touch only the new key.
    /// </summary>
    private sealed class RepeatState
    {
        internal string ItemsKey = "";
        internal string TemplateName = "";
        internal readonly List<ItemRow> Rows = new();
        internal readonly Dictionary<string, UiElementSpec> DerivedByKey = new(StringComparer.Ordinal);
        internal RepeatState Copy()
        {
            var copy = new RepeatState
            {
                ItemsKey = ItemsKey,
                TemplateName = TemplateName
            };
            copy.Rows.AddRange(Rows);
            foreach (KeyValuePair<string, UiElementSpec> entry in DerivedByKey)
                copy.DerivedByKey.Add(entry.Key, entry.Value);
            return copy;
        }
    }

    /// <summary>One arranged Repeat element and the row set it produced in this arrange.</summary>
    private readonly struct MaterializedRepeat
    {
        internal readonly UiNodeId Node;
        internal readonly RepeatState State;

        internal MaterializedRepeat(UiNodeId node, RepeatState state)
        {
            Node = node;
            State = state;
        }
    }

    private RepeatState GetOrCreateRepeatState(UiNode node)
    {
        if (!repeatStates.TryGetValue(node.Id, out RepeatState? state))
        {
            state = new RepeatState();
            repeatStates.Add(node.Id, state);
        }

        return state;
    }

    /// <summary>
    /// Arranges the collection element: one row per accepted item key, each row the named template
    /// instantiated in that item's binding scope, stacked the way a vertical container stacks its children
    /// (the element's <c>Padding</c>, <c>Gap</c>, <c>Title</c> and <c>Height</c> mean what they mean on every
    /// other container).
    /// <para>
    /// Nothing here is a second reconciliation algorithm: identity is the item key composed into the
    /// ordinary element identity by <see cref = "BuildItemSpec"/>, reuse is the session's node table keyed by
    /// that identity, and release is the same <c>PruneNodesExcept</c> pass the definition walk feeds.
    /// </para>
    /// </summary>
    private MeasuredBox MeasureRepeat(UiWidgetContext ctx, UiElementSpec spec, string kind, float width, Padding padding, float gap, float titleHeight, float innerWidth, float innerY, float availableHeight, UiNode node)
    {
        RepeatState state = GetOrCreateRepeatState(node);
        string itemsKey = ReadAttribute(spec, RepeatItemsAttribute);
        string templateName = ReadAttribute(spec, RepeatTemplateAttribute);
        if (!string.Equals(state.ItemsKey, itemsKey, StringComparison.Ordinal) || !string.Equals(state.TemplateName, templateName, StringComparison.Ordinal))
        {
            // The declaration under this identity changed. A reload rebuilds the engine, so this is the
            // programmatic-spec path; either way the derived subtrees belong to the old scope and are dropped
            // rather than reused under a scope they were not built for.
            state.Rows.Clear();
            state.DerivedByKey.Clear();
            state.ItemsKey = itemsKey;
            state.TemplateName = templateName;
        }

        state.Rows.Clear();
        MaterializeRows(ctx, node, state, itemsKey, templateName);
        var rows = new List<ChildSlot>(state.Rows.Count);
        var children = new UiLayoutChild[state.Rows.Count];
        var measured = new MeasuredBox[state.Rows.Count];
        for (int i = 0; i < state.Rows.Count; i++)
        {
            ItemRow row = state.Rows[i];
            rows.Add(new ChildSlot(row.Spec, row.Index));
            UiNode rowNode = ChildNode(ctx, node, row.Spec, row.Index);
            UiWidgetContext rowCtx = ElementScope(ctx.WithNode(rowNode), row.Spec, rowNode);
            bool stretch = UiPlacement.ParseAlign(row.Spec, UiPlacement.AlignXAttribute, UiPlacement.Axis.Horizontal).IsStretch;
            float rowWidth = stretch ? ResolveStackChildWidth(row.Spec, innerWidth, rowCtx) : ResolveWrapWidth(row.Spec, innerWidth, rowCtx);
            children[i] = LayoutChild(row.Spec, rowWidth);
        }

        UiLayoutPlan plan = UiLayout.VerticalContent(LayoutFrame(spec, width, availableHeight, padding, titleHeight, gap), children, ChildMeasure(ctx, node, spec, rows, measured));
        for (int i = 0; i < measured.Length; i++)
            if (measured[i].Entries.Count > 0)
                measured[i].Entries[0].IsMaterializedRow = true;
        MeasuredBox box = AssembleLayout(ctx, spec, kind, node, plan, measured);
        materializedRepeats.Add(new MaterializedRepeat(node.Id, state));
        return box;
    }

    /// <summary>
    /// Reads the row set the items binding currently supplies and materializes one identity per accepted
    /// key. A row the key contract refuses is not rendered at all: it contributes no element, no node and no
    /// state, and the refusal is reported once per distinct key.
    /// </summary>
    private void MaterializeRows(
        UiWidgetContext ctx, UiNode node, RepeatState state, string itemsKey, string templateName)
    {
        if (itemsKey.Length == 0 || templateName.Length == 0)
        {
            ReportRepeat(
                ctx,
                node,
                RepeatItemsAttribute,
                "(blank)",
                itemsKey.Length == 0
                    ? "no rows (no item-key binding is declared)"
                    : "no rows (no template name is declared)");
            return;
        }

        if (!templates.TryGetValue(templateName, out UiElementSpec template))
        {
            ReportRepeat(
                ctx,
                node,
                RepeatTemplateAttribute,
                templateName,
                "no rows (no template of that name in the definition)");
            return;
        }

        IReadOnlyList<string> keys = ReadItemKeys(ctx, node, itemsKey);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i] == null ? "" : keys[i].Trim();
            if (!AcceptItemKey(ctx, node, key, seen)) continue;

            if (!state.DerivedByKey.TryGetValue(key, out UiElementSpec derived))
            {
                derived = BuildItemSpec(template, itemsKey, key, i);
                state.DerivedByKey.Add(key, derived);
            }

            state.Rows.Add(new ItemRow { Key = key, Index = i, Spec = derived });
        }

        // A key the binding no longer supplies keeps no derived subtree: the session's prune releases its
        // nodes, and this table must not hold the shape of rows that no longer exist.
        if (state.DerivedByKey.Count > state.Rows.Count)
        {
            state.DerivedByKey.Clear();
            for (int i = 0; i < state.Rows.Count; i++)
            {
                state.DerivedByKey.Add(state.Rows[i].Key, state.Rows[i].Spec);
            }
        }
    }

    /// <summary>
    /// The ordered item keys the binding currently supplies. The element's own declaration contract already
    /// validated that binding at creation; the guard here is for a programmatically built spec, where a
    /// mistyped key must be one bounded report rather than an exception out of arrange.
    /// </summary>
    private IReadOnlyList<string> ReadItemKeys(UiWidgetContext ctx, UiNode node, string itemsKey)
    {
        try
        {
            if (ctx.Bindings.TryGet<IReadOnlyList<string>>(itemsKey, out IReadOnlyList<string> keys) && keys != null)
            {
                return keys;
            }

            ReportRepeat(ctx, node, RepeatItemsAttribute, itemsKey, "not bound as an ordered item-key list");
        }
        catch (InvalidOperationException ex)
        {
            ReportRepeat(ctx, node, RepeatItemsAttribute, itemsKey, "bound to another type (" + ex.GetType().Name + ")");
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// The row-identity contract, applied to the data the binding supplied. A blank key, a key carrying a
    /// reserved identity character, or a key a sibling row already used is refused: the row produces no
    /// element, no node and no state, and one deduplicated report names the repeat element and the key.
    /// Refusing the row is the whole answer - reconciling two model rows onto one identity would silently
    /// show one row's state under another, which is the defect this element exists to prevent.
    /// </summary>
    private static bool AcceptItemKey(UiWidgetContext ctx, UiNode node, string key, HashSet<string> seen)
    {
        if (key.Length == 0)
        {
            ReportRepeat(ctx, node, RepeatItemsAttribute, "(blank)", "no row (a stable business key is required)");
            return false;
        }

        if (key.IndexOf('/') >= 0
            || key.IndexOf(UiLayoutManifest.ItemKeySeparator) >= 0
            || key.IndexOf(UiNodeId.KeySeparator) >= 0
            || key.IndexOf(UiNodeId.SubNodeMarker) >= 0)
        {
            ReportRepeat(ctx, node, RepeatItemsAttribute, key, "no row (the key carries a reserved identity character)");
            return false;
        }

        if (!seen.Add(key))
        {
            ReportRepeat(ctx, node, RepeatItemsAttribute, key, "no row (the key already names a row in this set)");
            return false;
        }

        return true;
    }

    /// <summary>
    /// One item's subtree: the named template with every element's declared identity and binding keys moved
    /// into that item's scope. The result is cached per (repeat, item key), so a reorder reuses the very same
    /// spec objects - and therefore the same identities, widget instances and state.
    /// <para>
    /// The composed identity is <c>&lt;declaredId&gt;#&lt;itemKey&gt;</c>: the declared Id stays readable in
    /// diagnostics, and the item key is what makes two rows of one template distinct. An element the author
    /// left unnamed contributes its generated segment, exactly as the session's identity grammar composes it,
    /// so an unnamed template element still gets a per-row identity instead of colliding with its namesake in
    /// the next row.
    /// </para>
    /// </summary>
    private static UiElementSpec BuildItemSpec(UiElementSpec template, string itemsKey, string itemKey, int declaredIndex)
    {
        string segment = template.Id.Length > 0 ? template.Id : UiNodeId.GeneratedSegment(template.Kind, declaredIndex);
        string id = segment + UiLayoutManifest.ItemKeySeparator + itemKey;
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> pair in template.Attributes)
        {
            string value = QualifyItemBinding(pair.Key, pair.Value, itemsKey, itemKey);
            if (string.Equals(pair.Key, "AnchorX", StringComparison.OrdinalIgnoreCase) || string.Equals(pair.Key, "AnchorY", StringComparison.OrdinalIgnoreCase))
            {
                UiPlacement.ParseAnchor(value, pair.Key.Equals("AnchorX", StringComparison.OrdinalIgnoreCase) ? UiPlacement.Axis.Horizontal : UiPlacement.Axis.Vertical, out string target, out _);
                if (!string.Equals(target, "parent", StringComparison.Ordinal))
                {
                    int edge = value.LastIndexOf('.');
                    value = target + UiLayoutManifest.ItemKeySeparator + itemKey + value.Substring(edge);
                }
            }

            attributes[pair.Key] = value;
        }

        attributes["Id"] = id;
        var children = new UiElementSpec[template.Children.Count];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = BuildItemSpec(template.Children[i], itemsKey, itemKey, i);
        }

        return new UiElementSpec(id, template.Kind, attributes, children);
    }

    /// <summary>
    /// One declared binding key moved into the item's scope: <c>done</c> becomes
    /// <c>&lt;itemsKey&gt;.&lt;itemKey&gt;.done</c>. Every binding role is scoped; <c>Tab</c> is
    /// deliberately not, because a tab is a page-level answer and not an item's, and every other attribute
    /// is layout or appearance with nothing to resolve against bindings. The criterion is "is this an
    /// answer about one row?": <c>SelectedKey</c> answers which row is selected, so it sits in the same
    /// table as <c>Bind</c>, <c>ActionBind</c>, <c>OptionsBind</c>, <c>VisibleKey</c> and <c>PayloadKey</c>
    /// rather than resolving the page-level key of that name on every row.
    /// </summary>
    private static string QualifyItemBinding(string attribute, string value, string itemsKey, string itemKey)
    {
        if (!string.Equals(attribute, BindAttribute, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, ActionBindAttribute, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, OptionsBindAttribute, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, VisibleKeyAttribute, StringComparison.OrdinalIgnoreCase)
            // G2: a button's payload is per item inside a template, so it is scoped exactly like the other
            // binding roles - that is what makes "this row's key" answerable at all.
            && !string.Equals(attribute, "PayloadKey", StringComparison.OrdinalIgnoreCase)
            // B1: SelectedKey is the binding-driven SELECTED state, and "which row is selected" is an
            // answer about one row - leaving it page-scoped made every row resolve one page-level key,
            // which is how a data-driven row set lost the ability to show which of its rows was selected.
            && !string.Equals(attribute, "SelectedKey", StringComparison.OrdinalIgnoreCase)
            // FL-IC2: "return from this row" is an answer about one row for exactly the same reason, and the
            // contract asks for the tree walk to reuse the existing scope resolution rather than invent a
            // second one - a Cancel declared inside a row template names the row's own command.
            && !string.Equals(attribute, CancelBindAttribute, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, "PointKeysBind", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, "DisclosureBind", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, "CheckBind", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, "WidthKey", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(attribute, Widgets.OptionHelp.Attribute, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        string declared = (value ?? "").Trim();
        return declared.Length == 0 ? value ?? "" : itemsKey + "." + itemKey + "." + declared;
    }

    /// <summary>
    /// The item identity set this arrange materialized, added to the declared set the session's prune uses.
    /// It mirrors the definition walk: every element of an instantiated row is declared, hidden or not - a
    /// hidden row element keeps its node and its state - while a key the binding no longer supplies declares
    /// nothing, so its subtree is released by the one lifetime path every other node follows.
    /// </summary>
    private void CollectRepeatItemIdentities(HashSet<UiNodeId> into)
    {
        for (int r = 0; r < materializedRepeats.Count; r++)
        {
            MaterializedRepeat repeat = materializedRepeats[r];
            List<ItemRow> rows = repeat.State.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                UiNodeId id = repeat.Node.Child(rows[i].Spec, rows[i].Index);
                into.Add(id);
                CollectChildIdentities(rows[i].Spec, id, into);
            }
        }
    }

    /// <summary>
    /// The last materialization of every Repeat element the definition still declares but this arrange did not
    /// visit (a <c>Visible</c>/<c>VisibleKey</c>/<c>Tab</c>-hidden one). Without it the prune would reap the
    /// rows of an element that is merely hidden, and a Tab switch would throw away scroll, selection, drafts
    /// and expansion the definition never removed - the one lifetime distinction P2 wrote down for elements.
    /// A key the binding dropped while the element was hidden stays until the next arrange that visits it, so
    /// the extra retention is bounded by the last row set the consumer actually supplied.
    /// </summary>
    private void CollectHiddenRepeatIdentities(IReadOnlyList<UiElementSpec> roots, HashSet<UiNodeId> into)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            UiNodeId id = UiNodeId.Root(roots[i], i);
            CollectHiddenRepeatIdentities(roots[i], id, into);
        }
    }

    private void CollectHiddenRepeatIdentities(UiElementSpec spec, UiNodeId id, HashSet<UiNodeId> into)
    {
        if (string.Equals(spec.Kind, "Repeat", StringComparison.Ordinal)
            && !IsMaterializedThisPass(id)
            && repeatStates.TryGetValue(id, out RepeatState? state))
        {
            List<ItemRow> rows = state!.Rows;
            for (int r = 0; r < rows.Count; r++)
            {
                UiNodeId rowId = id.Child(rows[r].Spec, rows[r].Index);
                into.Add(rowId);
                CollectChildIdentities(rows[r].Spec, rowId, into);
            }
        }

        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            CollectHiddenRepeatIdentities(child, id.Child(child, i), into);
        }
    }

    private bool IsMaterializedThisPass(UiNodeId id)
    {
        for (int i = 0; i < materializedRepeats.Count; i++)
        {
            if (materializedRepeats[i].Node == id) return true;
        }

        return false;
    }

    /// <summary>Drops the materialization of a Repeat element whose identity the session no longer holds.</summary>
    private void PruneRepeatStates(UiSession session)
    {
        List<UiNodeId>? gone = null;
        foreach (UiNodeId id in repeatStates.Keys)
        {
            if (session.GetNode(id) == null)
            {
                (gone ??= new List<UiNodeId>()).Add(id);
            }
        }

        if (gone == null) return;
        for (int i = 0; i < gone.Count; i++)
        {
            repeatStates.Remove(gone[i]);
        }
    }

    private static void ReportRepeat(
        UiWidgetContext ctx, UiNode node, string attribute, string authored, string resolved)
    {
        UiFitAudit.ReportStyleFallback(node.Path, "Repeat", attribute, authored, resolved);
    }

    // --- the template contract (P3) -------------------------------------------------------------

    /// <summary>
    /// The structural contract of the definition's templates, checked once when the engine is built - which
    /// is Host creation, and each document commit that rebuilds the engine. The template subtree is outside
    /// the Host's own walk on purpose (its binding keys are item-scoped and provably cannot exist as page
    /// bindings), so its kinds and attribute vocabulary are enforced here, with the template name and the
    /// element path in the refusal.
    /// <para>
    /// What this covers: an unknown widget kind, an attribute outside the kind's registered schema (plus the
    /// engine-wide names every element accepts), and an attribute outside the container vocabulary for a
    /// container. What it deliberately does not re-implement: UiHost's numeric and narrow-state grammar, which
    /// needs the parent chain and the Bindings the Host holds. A malformed number inside a template therefore
    /// degrades the way a programmatically built spec degrades instead of failing creation, and that boundary
    /// is written in <c>docs/development/0.5/20-api-and-xml.md</c> rather than implied here.
    /// </para>
    /// </summary>
    private void ValidateTemplates()
    {
        foreach (KeyValuePair<string, UiElementSpec> entry in templates)
        {
            // A template root is validated with no parent: the collection element owns the row slot, so the
            // root is not a child of a placement or flow container and the placement vocabulary is refused
            // there rather than accepted and then ignored by the row arrangement.
            ValidateTemplateElement(entry.Value, entry.Key, "<Templates>/" + entry.Key, parent: null);
        }
    }

    private void ValidateTemplateElement(UiElementSpec spec, string templateName, string path, UiElementSpec? parent)
    {
        // The same creation-time refusal matrix the Host runs over the page tree, run here because a template
        // subtree is outside that walk. Element, attribute and path are all still known, which is what makes
        // the located error possible.
        UiPlacement.ValidateChild(spec, path, scope, parent);
        UiPlacement.ValidateSiblingAnchors(spec, path, scope);

        if (IsContainerElementName(spec.Kind))
        {
            RejectUnknownTemplateAttributes(spec, templateName, path, TemplateContainerAttributes, engineWide: false);
        }
        else
        {
            try
            {
                UiWidgetRegistry.Resolve(scope, spec.Kind);
            }
            catch (UiUnknownWidgetKindException ex)
            {
                throw TemplateContract(templateName, path, spec, ex.Message);
            }

            RejectUnknownTemplateAttributes(
                spec,
                templateName,
                path,
                UiWidgetRegistry.GetAttributeSchema(scope, spec.Kind) ?? NoSchema,
                engineWide: true);
        }

        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiElementSpec child = spec.Children[i];
            ValidateTemplateElement(child, templateName, path + "/" + (child.Id.Length > 0 ? child.Id : child.Kind), spec);
        }
    }

    private void RejectUnknownTemplateAttributes(
        UiElementSpec spec, string templateName, string path, IReadOnlyCollection<string> allowed, bool engineWide)
    {
        foreach (KeyValuePair<string, string> pair in spec.Attributes)
        {
            if (ContainsAttribute(allowed, pair.Key) || UiAppearanceProperties.IsPaletteAttribute(pair.Key)) continue;
            if (engineWide && EngineWideWidgetAttributes.Contains(pair.Key)) continue;

            throw TemplateContract(
                templateName,
                path,
                spec,
                "Unknown attribute '" + pair.Key + "' on " + spec.Kind + " is not part of the creation-time contract.");
        }
    }

    private static bool ContainsAttribute(IReadOnlyCollection<string> allowed, string name)
    {
        foreach (string candidate in allowed)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private UiContractException TemplateContract(string templateName, string path, UiElementSpec spec, string message)
    {
        return new UiContractException(
            "Template '" + templateName + "' is invalid at '" + path + "': " + message,
            scope,
            spec.Id,
            spec.Kind,
            path);
    }

    private IUiWidget GetOrCreateWidget(UiElementSpec spec, UiNodeId id)
    {
        if (widgetInstances.TryGetValue(id, out IUiWidget? widget))
        {
            // An identity survives a definition change; the kind under it may not. A cached instance whose
            // kind no longer matches the spec would measure and draw the element as the kind it used to
            // be, so the instance is replaced with the one the current definition names.
            if (string.Equals(widget.Kind, spec.Kind, StringComparison.Ordinal))
            {
                // The identity is reused, but the element under it can still be a different one of the same
                // kind: a definition that renumbers unnamed siblings hands node [1] a different spec, and an
                // instance still configured with the old one would measure and draw the wrong element. The
                // table's whole point is reusing the instance, so it is re-configured instead of replaced.
                widget.Configure(spec);
                return widget;
            }

            widgetInstances.Remove(id);
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
                SubtreeCount = entry.SubtreeCount,
                // The row stamp must survive the offset copy, or the counting budget sees no rows.
                IsMaterializedRow = entry.IsMaterializedRow
            };
            result.Entries.Add(copy);
        }

        return result;
    }

    private static string GetContainerKind(UiElementSpec spec)
    {
        // The collection element is claimed here, before any widget path can see it: its rows are tree
        // elements materialized from a template, so it must never be handled as a drawn leaf.
        if (string.Equals(spec.Kind, "Repeat", StringComparison.Ordinal))
        {
            return "Repeat";
        }

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

    /// <summary>
    /// True when the element name is one the engine arranges as a container rather than drawing through a
    /// widget. The list is the engine's counterpart of <c>UiHost.IsContainerKind</c>, which decides how the
    /// Host validates the same element; the two must agree, and <c>Repeat</c> is the one name they agree on
    /// differently by design (the Host validates it as a widget so its declaration has a contract owner, the
    /// engine arranges it as a container so its rows exist in the tree).
    /// </summary>
    private static bool IsContainerElementName(string kind)
    {
        return string.Equals(kind, "Stack", StringComparison.Ordinal)
            || string.Equals(kind, "Row", StringComparison.Ordinal)
            || string.Equals(kind, "Column", StringComparison.Ordinal)
            || string.Equals(kind, "Wrap", StringComparison.Ordinal)
            || string.Equals(kind, "Overlay", StringComparison.Ordinal)
            || string.Equals(kind, "Section", StringComparison.Ordinal)
            || string.Equals(kind, "Surface", StringComparison.Ordinal)
            || string.Equals(kind, "Scroll", StringComparison.Ordinal)
            || string.Equals(kind, "Clip", StringComparison.Ordinal);
    }

    /// <summary>
    /// Numeric Width on a child; Auto and malformed values are not fixed. The static declaration comes
    /// first, exactly like Visible/VisibleKey: <c>WidthKey</c> is consulted only when no usable Width is
    /// written, and it answers the same question through a float value binding (B5).
    /// </summary>
    private static bool TryFixedWidth(UiElementSpec spec, UiWidgetContext ctx, out float width)
    {
        width = 0f;
        if (spec.TryGetAttribute("Width", out string raw)
            && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out width)
            && width > 0f)
        {
            return true;
        }

        return TryBoundWidth(spec, ctx, out width);
    }

    /// <summary>
    /// The <c>WidthKey</c> half of the rule: a numeric value binding answers the declared width. Fail-soft
    /// and loud, like <c>VisibleKey</c> - a key that is missing or bound to another type leaves the static
    /// answer in place and records one deduplicated appearance note, so a typo cannot silently size a column.
    /// </summary>
    private static bool TryBoundWidth(UiElementSpec spec, UiWidgetContext ctx, out float width)
    {
        width = 0f;
        string key = ReadAttribute(spec, WidthKeyAttribute);
        if (key.Length == 0) return false;

        try
        {
            if (ctx.Bindings.TryGet(key, out float bound) && !float.IsNaN(bound) && !float.IsInfinity(bound) && bound > 0f)
            {
                width = bound;
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            // A key bound to another type: the fallback and its note below, not an exception per frame.
        }

        UiFitAudit.ReportStyleFallback(ctx.ElementPath, spec.Kind, WidthKeyAttribute, key, "unsized");
        return false;
    }

    private static bool IsAutoWidth(UiElementSpec spec)
    {
        return spec.TryGetAttribute("Width", out string raw)
            && string.Equals(raw.Trim(), "Auto", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Natural width of a child (N1): the maximum measured advance over the label attributes its kind
    /// declared at registration, each resolved through translation when the attribute name ends in Key,
    /// PLUS the non-text body the kind declares at registration (a box, a track and the space it holds
    /// open). Text-only measurement reserved too little room for a control that draws its own body, which
    /// is the defect the body contribution closes; a kind that declares no body keeps its exact previous
    /// answer. Zero still means "not Auto-measurable" — no label set, no body, or neither filled — and the
    /// caller falls back to the unsized distribution.
    /// </summary>
    private static float MeasureLabelWidth(UiElementSpec spec, UiWidgetContext ctx)
    {
        float body = 0f;
        Func<UiElementSpec, UiWidgetContext, float>? naturalBody = UiWidgetRegistry.GetNaturalBody(ctx.Source, spec.Kind);
        if (naturalBody != null)
        {
            body = Math.Max(0f, naturalBody(spec, ctx));
        }

        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(ctx.Source, spec.Kind);
        if (labels == null) return body;

        float widest = 0f;
        foreach (string attribute in labels)
        {
            if (!spec.TryGetAttribute(attribute, out string value)) continue;
            value = value.Trim();
            if (value.Length == 0) continue;

            string text = IsTranslationKeyAttribute(attribute)
                ? ctx.Translation.Translate(value)
                : value;
            widest = Math.Max(widest, ctx.Metrics.MeasureWidth(text, ctx.Theme.DefaultFont));
        }

        return widest <= 0f ? body : widest + body;
    }

    /// <summary>
    /// True when a declared label attribute carries a translation KEY rather than literal text — the
    /// <c>*Key</c> convention, which has to survive an index suffix: <c>TitleKey1</c> is a key whose index
    /// follows the name exactly as it does in <c>Title1</c>/<c>Value1</c>, so a plain suffix test would miss it
    /// and measure the raw identifier. That is the defect this rule closes: an Auto column would reserve the
    /// width of the key text instead of the string the translation seam resolves it to.
    /// </summary>
    private static bool IsTranslationKeyAttribute(string attribute)
    {
        int end = attribute.Length;
        while (end > 0 && char.IsDigit(attribute[end - 1]))
        {
            end--;
        }

        return end >= 3 && string.Equals(attribute.Substring(end - 3, 3), "Key", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>MinWidth/MaxWidth clamp, applied only when declared (absent attributes change nothing).</summary>
    private static float ClampDeclaredWidth(UiElementSpec spec, float width)
    {
        return UiLayout.Clamp(width, ReadWidthBound(spec, "MinWidth", 0), ReadWidthBound(spec, "MaxWidth", float.MaxValue));
    }

    private static float ReadWidthBound(UiElementSpec spec, string attribute, float fallback) => spec.TryGetAttribute(attribute, out string raw) && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value > 0 ? value : fallback;
    private static float ResolveWrapWidth(UiElementSpec spec, float innerWidth, UiWidgetContext ctx)
    {
        if (TryFixedWidth(spec, ctx, out float fixedWidth))
        {
            return ClampDeclaredWidth(spec, fixedWidth);
        }

        if (IsAutoWidth(spec))
        {
            float natural = ClampDeclaredWidth(spec, MeasureLabelWidth(spec, ctx));
            if (natural > 0f)
                return ClampDeclaredWidth(spec, Math.Min(natural, innerWidth));
        // fall through: not measurable, keep the historical full-row width
        }

        return ClampDeclaredWidth(spec, Math.Max(1f, innerWidth));
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
            if (natural > 0f)
                return ClampDeclaredWidth(spec, Math.Min(natural, innerWidth));
        }

        return ClampDeclaredWidth(spec, Math.Max(1f, innerWidth));
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

    private static float ResolveHeight(
        UiElementSpec spec, IUiWidget widget, UiWidgetContext ctx, float width, UiNode node, float? contentHeight)
    {
        if (spec.TryGetAttribute("Height", out string raw))
        {
            string value = raw.Trim();
            if (value.Length == 0 || string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return MeasuredHeight(widget, ctx, node);
            }

            if (UiPlacement.IsMatchContentHeight(spec))
            {
                // The parent that can answer already handed the height, and the element's own content is not the
                // geometry. With no reference in reach the mode is Auto, which is the documented degradation.
                return contentHeight ?? MeasuredHeight(widget, ctx, node);
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedHeight))
            {
                return Math.Max(0f, fixedHeight);
            }

            throw new FormatException(
                $"Widget id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Height '{raw}'; expected a number, Auto or MatchContent.");
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

    private static float ResolveContainerHeight(UiElementSpec spec, float naturalHeight, float availableHeight) => ReadLayoutHeight(spec).Resolve(naturalHeight, availableHeight);
    private static UiLayoutHeight ReadLayoutHeight(UiElementSpec spec)
    {
        if (spec.TryGetAttribute("Height", out string raw) && raw.Trim().Length > 0 && !string.Equals(raw.Trim(), "Auto", StringComparison.OrdinalIgnoreCase) && !UiPlacement.IsMatchContentHeight(spec))
        {
            if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedHeight))
                return new UiLayoutHeight(fixedHeight, false);
            throw new FormatException("Container id=\"" + spec.Id + "\" (Kind=\"" + spec.Kind + "\") has invalid Height '" + raw + "'; expected a number, Auto or MatchContent.");
        }

        return new UiLayoutHeight(null, IsFill(spec));
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

    private float[] ResolveColumnWidths(IReadOnlyList<ChildSlot> children, float innerWidth, float gap, UiWidgetContext ctx, UiNode node)
    {
        var demands = new UiWidthDemand[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            UiElementSpec spec = children[i].Spec;
            UiNode childNode = ChildNode(ctx, node, spec, children[i].DeclaredIndex);
            UiWidgetContext childCtx = ElementScope(ctx.WithNode(childNode), spec, childNode);
            float min = ReadWidthBound(spec, "MinWidth", 0);
            float max = ReadWidthBound(spec, "MaxWidth", float.MaxValue);
            if (TryFixedWidth(spec, childCtx, out float fixedWidth))
            {
                demands[i] = new UiWidthDemand(UiWidthMode.Fixed, fixedWidth, min, max);
                continue;
            }

            float natural = IsAutoWidth(spec) ? ClampDeclaredWidth(spec, MeasureLabelWidth(spec, childCtx)) : 0;
            demands[i] = new UiWidthDemand(natural > 0 ? UiWidthMode.Auto : UiWidthMode.Flexible, natural, Math.Min(max, Math.Max(1, min)), max);
        }

        UiWidthPlan plan = UiLayout.Columns(demands, innerWidth, gap);
        if (plan.Overflow)
            ReportLayout(node, "WidthInsufficient", "Declared widths/minima require " + plan.Required + " but parent allocation is " + plan.Available + ".");
        var widths = new float[children.Count];
        for (int i = 0; i < widths.Length; i++)
            widths[i] = plan.Widths[i];
        return widths;
    }

    /// <summary>
    /// CP-0 (0.7.x Batch 1): density reaches container spacing. An absent <c>Padding</c> falls back to the
    /// theme's own geometry instead of zero, so a theme change moves the space BETWEEN containers and not
    /// only the space inside a control. An explicit attribute always wins, which is what makes
    /// <c>Padding="0"</c> the documented escape hatch back to the pre-CP-0 result.
    /// <para>
    /// This is called from both halves of the frame - the measure pass and the draw pass that builds a
    /// container's title rect - with the theme each half resolved through the element's own style chain.
    /// Both halves must read the same token or measure and draw disagree, which is the one invariant this
    /// library exists to keep; the placement lane pins the two answers against each other.
    /// </para>
    /// </summary>
    private static Padding ParsePadding(UiElementSpec spec, UiTheme theme)
    {
        if (!spec.TryGetAttribute("Padding", out string raw))
        {
            float token = theme.Geometry.Padding;
            return new Padding(token, token, token, token);
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

    /// <summary>
    /// The container's declared <c>Gap</c>, or the theme's geometry gap when the attribute is absent (CP-0).
    /// An explicit <c>Gap="0"</c> keeps the pre-CP-0 result exactly, like <c>Padding="0"</c>.
    /// </summary>
    private static float ReadGap(UiElementSpec spec, UiTheme theme)
    {
        if (!spec.TryGetAttribute("Gap", out string raw) || raw.Trim().Length == 0)
        {
            return theme.Geometry.Gap;
        }

        if (float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float gap) && gap >= 0f)
        {
            return gap;
        }

        throw new FormatException(
            $"Element id=\"{spec.Id}\" (Kind=\"{spec.Kind}\") has invalid Gap '{raw}'.");
    }

    /// <summary>
    /// The element's authored visibility, and with it the three visibility mechanisms the page
    /// vocabulary has: the static <c>Visible</c>, the dynamic <c>VisibleKey</c>, and the legacy static
    /// <c>Hidden</c>. An element is hidden when any of them says so; <paramref name="parent"/> and
    /// <paramref name="declaredIndex"/> exist only to name the element in the one diagnostic
    /// <see cref="VisibleKeyAttribute"/> can produce, and are not part of the decision.
    /// </summary>
    private static bool IsHidden(
        UiElementSpec spec, UiWidgetContext ctx, bool narrow, UiNode? parent, int declaredIndex)
    {
        if (narrow
            && spec.TryGetAttribute("NarrowHidden", out string narrowRaw)
            && (string.Equals(narrowRaw.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(narrowRaw.Trim(), "1", StringComparison.Ordinal)))
        {
            return true;
        }

        // B2(2): the exact mirror. A child that belongs only to the WIDE presentation declares WideHidden,
        // which is the same static rule as NarrowHidden read under the opposite state. That is what makes
        // "this element belongs to one presentation" expressible without the two mutually exclusive
        // subtrees the earlier workaround needed.
        if (!narrow
            && spec.TryGetAttribute("WideHidden", out string wideRaw)
            && (string.Equals(wideRaw.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(wideRaw.Trim(), "1", StringComparison.Ordinal)))
        {
            return true;
        }

        if (!IsVisibleDeclaration(spec, ctx, parent, declaredIndex))
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

    /// <summary>
    /// <c>Visible</c> (default true) and <c>VisibleKey</c>: the static declaration first, then a bool
    /// value binding resolved through <see cref="IUiBindings.TryGetBool"/>.
    /// <para>
    /// An unresolvable key is deliberately not fatal: a page must not fail to exist because a model key
    /// is missing or not yet bound, so the element stays visible. It is not silent either - the key is
    /// reported once through the appearance channel, which is deduplicated by element, kind, attribute
    /// and key, so a page drawn at 60 fps records one finding rather than sixty.
    /// </para>
    /// </summary>
    private static bool IsVisibleDeclaration(
        UiElementSpec spec, UiWidgetContext ctx, UiNode? parent, int declaredIndex)
    {
        if (spec.TryGetAttribute("Visible", out string visibleRaw))
        {
            string visible = visibleRaw.Trim();
            if (string.Equals(visible, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(visible, "0", StringComparison.Ordinal))
            {
                return false;
            }
        }

        string key = ReadAttribute(spec, VisibleKeyAttribute);
        if (key.Length == 0) return true;
        if (ctx.Bindings.TryGetBool(key, out bool boundVisible)) return boundVisible;

        UiNodeId id = parent == null ? UiNodeId.Root(spec, declaredIndex) : parent.Id.Child(spec, declaredIndex);
        UiFitAudit.ReportStyleFallback(id.Path, spec.Kind, VisibleKeyAttribute, key, "visible");
        return true;
    }

    /// <summary>
    /// Every element identity one definition declares, whether or not the arrange will visit it: hidden
    /// elements are included because their identity still exists and only a removed one may be released.
    /// </summary>
    private static void CollectDeclaredIdentities(IReadOnlyList<UiElementSpec> roots, HashSet<UiNodeId> into)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            UiNodeId id = UiNodeId.Root(roots[i], i);
            into.Add(id);
            CollectChildIdentities(roots[i], id, into);
        }
    }

    private static void CollectChildIdentities(UiElementSpec spec, UiNodeId parent, HashSet<UiNodeId> into)
    {
        for (int i = 0; i < spec.Children.Count; i++)
        {
            UiNodeId id = parent.Child(spec.Children[i], i);
            into.Add(id);
            CollectChildIdentities(spec.Children[i], id, into);
        }
    }

    private static int DefinitionRevision(UiWidgetContext ctx)
    {
        return ctx.Session.DefinitionRevision;
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
            // The draw half of CP-0: the same helper the measure half called, with the theme this entry's
            // own style chain resolved to, so a container's title rect sits on the same padding its children
            // were arranged against.
            Padding padding = ParsePadding(entry.Spec, ctx.Theme);
            float innerWidth = Math.Max(1f, rect.width - padding.Left - padding.Right);
            var headerRect = new Rect(rect.x + padding.Left, rect.y + padding.Top, innerWidth, SectionTitleHeight);
            // One text outlet: routing the container title through UiThemeDraw.Label is what makes it
            // visible to the fit audit (BeginElement above already attributes by path), which the
            // engine's own private Label copy could never do.
            UiThemeDraw.Label(headerRect, ReadTitle(entry.Spec, ctx), ctx.Theme, ctx.Theme[UiPaletteRole.Foreground]);
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
    }
}
