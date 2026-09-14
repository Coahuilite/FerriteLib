using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Host-provided translation seam. RimWorld keys are resolved by the host adapter.
/// <para>
/// <b>A missing key is drawn as the key, and that is a policy rather than a gap.</b> The library owns no
/// strings and cannot know which keys a consumer declared, so when a key resolves to nothing its only
/// choice is between a blank label and the key the consumer asked for. It draws the key: a visible,
/// slightly wrong word is diagnosable from a screenshot, while a blank rectangle has no author to blame.
/// A host that prefers a placeholder, an empty string or a log entry is free to resolve unknown keys that
/// way — this seam is the host's, and nothing in the library inspects the answer. The cost of the policy
/// is real and should be written down rather than discovered: a missing key reads as a foreign-language
/// word, not as a defect, and the library's own lanes cannot see a consumer's language files.
/// </para>
/// <para>
/// <b>Where the check belongs, if a host wants one (dev-only).</b> Only the host knows both the keys it
/// passes in and the language data that is actually loaded, so the self-check is the host's: at window
/// creation, or once after the mod's start-up, resolve each key the chrome uses, compare the result with
/// the key itself, and log the ones that came back equal — once per session, behind the development
/// build's logging switch. Running it in a release build is how a diagnostic turns into noise; skipping
/// it entirely is how a missing key reaches players as a foreign word, which is the failure this
/// paragraph exists to make a decision instead of an accident.
/// </para>
/// </summary>
public interface IUiTranslation
{
    string Translate(string key);

    /// <summary>
    /// A value that differs whenever the text resolved for a key may differ - in practice, the active
    /// game language. Measured text bands are derived from resolved strings, so the layout engine
    /// compares this by equality as part of the snapshot cache key: switching language while the size,
    /// the manifest and the content revision all stay put must force a re-measure instead of reusing
    /// the previous language's geometry. Hosts must not return a value that changes on every call.
    /// </summary>
    int TranslationRevision { get; }
}

/// <summary>
/// Per-frame context passed to widgets. It carries every cross-cutting dependency a widget needs
/// without exposing the Host itself: session, metrics, theme, translation, bindings and diagnostics.
/// </summary>
public sealed class UiWidgetContext
{
    public string Source { get; }

    public UiSession Session { get; }

    public ITextMetrics Metrics { get; }

    public UiTheme Theme { get; }

    public IUiTranslation Translation { get; }

    public IUiBindings Bindings { get; }

    public float ViewWidth { get; }

    /// <summary>
    /// The element this context belongs to. The layout engine sets it per arranged entry, so a widget
    /// measures and draws with its own identity (the same value in both halves of a pass), and the
    /// session resolves per-element state against it. <see cref="UiNodeId.None"/> for a context a
    /// caller built by hand, which is the historical "no element" behaviour.
    /// </summary>
    public UiNodeId ElementId { get; }

    /// <summary>
    /// This element's arranged path. The engine sets it per entry (0.4.0 identity layer) instead of
    /// handing every element the host's page-root path; for a hand-built context it is whatever the
    /// caller named. It is the display path: diagnostics read this, never <see cref="UiNodeId.Key"/>.
    /// </summary>
    public string ElementPath { get; }

    /// <summary>
    /// The element's node: the object that owns its state and its dirty flag (0.4.0 node step 1). The
    /// engine sets it per arranged entry, so a widget can read its own state through
    /// <see cref="UiNode.State"/> and ask for a re-measure with <see cref="UiNode.MarkDirty"/> instead of
    /// naming a string slot. Null for a context a caller built by hand.
    /// </summary>
    public UiNode? Node { get; }

    /// <summary>
    /// The nearest-first style declaration chain this element sits in: the element's own declaration at
    /// index 0, then its containers outward. The engine builds it as it descends (0.4.x batch B), because
    /// a <see cref="UiNode"/> carries no parent link yet - the chain is that link for style purposes, and
    /// migrating it onto the node is a later step, not a different shape. Only the two inheriting axes
    /// (scheme and density) travel here; a role never inherits, so <c>Tone</c>/<c>Emphasis</c> stay on the
    /// element's own spec.
    /// <para>
    /// Null means "nothing was declared on this path", which is the page level. A link is appended only
    /// for an element that declares something, so an element that styles nothing reuses its parent's
    /// list: one pass over a tree of unstyled elements allocates no chain at all.
    /// </para>
    /// </summary>
    public IReadOnlyList<UiStyleDeclaration>? StyleChain { get; }

    /// <summary>
    /// Offset from the widget's draw rect to the Host's final usable window space. The layout
    /// engine sets this while drawing inside scrolled/grouped containers so popups (drawn after
    /// the content pass, outside any IMGUI group or scroll matrix) share one coordinate space
    /// with their trigger anchor.
    /// </summary>
    public Vector2 WindowOrigin { get; }

    public UiWidgetContext(
        string source,
        UiSession session,
        ITextMetrics metrics,
        UiTheme theme,
        IUiTranslation translation,
        IUiBindings bindings,
        float viewWidth,
        string elementPath,
        Vector2 windowOrigin = default,
        UiNodeId elementId = default,
        UiNode? node = null,
        IReadOnlyList<UiStyleDeclaration>? styleChain = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        ViewWidth = viewWidth;
        ElementPath = elementPath ?? "";
        WindowOrigin = windowOrigin;
        ElementId = node?.Id ?? elementId;
        Node = node;
        StyleChain = styleChain;
    }

    /// <summary>
    /// Returns a context for a sub-control this widget owns, minted as a node under the element's node:
    /// <c>ctx.Child("left")</c> keeps its own identity - and therefore its own state - across passes, and
    /// its display path reads <c>&lt;element&gt;/@left</c>. The sub-node carries no arranged geometry; it
    /// exists so a widget's self-drawn controls stop sharing one element-wide state bag and so their state
    /// survives the widget instance. It inherits the element's style chain (a sub-control is drawn inside
    /// the element, so scheme and density cascade to it unchanged), and it replaces the earlier path-only
    /// <c>ForChild</c>, which could not mint identity (0.4.0 node step 2).
    /// </summary>
    public UiWidgetContext Child(string name)
    {
        if (Node == null)
        {
            throw new InvalidOperationException(
                "A context without an element node cannot mint a sub-node; the engine binds Node per arranged entry.");
        }

        UiNode child = Session.GetOrCreateSubNode(Node, name);
        return new UiWidgetContext(
            Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth,
            child.Path, WindowOrigin, child.Id, child, StyleChain);
    }

    /// <summary>
    /// Returns a context bound to <paramref name="node"/>: its identity, its display path and the node
    /// itself, so a widget can read its own state and mark itself dirty without a string key. Replaces
    /// the earlier <c>WithElement(UiNodeId)</c> in the same 0.4.0 window, because the node - not a value
    /// handed around by hand - is now what carries identity.
    /// </summary>
    public UiWidgetContext WithNode(UiNode node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        return new UiWidgetContext(
            Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth,
            node.Path, WindowOrigin, node.Id, node, StyleChain);
    }

    /// <summary>
    /// Returns a context with a different view width. The layout engine uses this so widgets nested
    /// in columns measure and draw against their arranged column width instead of the page width.
    /// </summary>
    public UiWidgetContext WithViewWidth(float viewWidth)
    {
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, viewWidth, ElementPath, WindowOrigin, ElementId, Node, StyleChain);
    }

    /// <summary>Returns a context whose draw rects are offset into Host window space by <paramref name="windowOrigin"/>.</summary>
    public UiWidgetContext WithWindowOrigin(Vector2 windowOrigin)
    {
        return new UiWidgetContext(Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth, ElementPath, windowOrigin, ElementId, Node, StyleChain);
    }

    /// <summary>
    /// Returns a context that draws with <paramref name="theme"/>. The layout engine sets it per element,
    /// so a widget measures and draws against the theme its own style scope resolved to - a region's
    /// scheme/density bag, or the injected theme at the page level. A context that already carries that
    /// theme comes back unchanged, which is the common case for an element inheriting its parent's scope.
    /// </summary>
    public UiWidgetContext WithTheme(UiTheme theme)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        if (ReferenceEquals(theme, Theme)) return this;
        return new UiWidgetContext(
            Source, Session, Metrics, theme, Translation, Bindings, ViewWidth,
            ElementPath, WindowOrigin, ElementId, Node, StyleChain);
    }

    /// <summary>
    /// Returns a context scoped to one element's own declaration: the declaration becomes the nearest link
    /// of <see cref="StyleChain"/> and everything already on the chain stays behind it, so nearest wins.
    /// A declaration that names neither scheme nor density returns this context unchanged - an element
    /// that styles nothing appends no list and allocates no context.
    /// </summary>
    public UiWidgetContext WithStyleDeclaration(UiStyleDeclaration declaration)
    {
        if (declaration.Scheme == null && declaration.Density == null) return this;

        int inherited = StyleChain?.Count ?? 0;
        var chain = new UiStyleDeclaration[inherited + 1];
        chain[0] = declaration;
        for (int i = 0; i < inherited; i++)
        {
            chain[i + 1] = StyleChain![i];
        }

        return new UiWidgetContext(
            Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth,
            ElementPath, WindowOrigin, ElementId, Node, chain);
    }

    /// <summary>
    /// Returns a context carrying exactly <paramref name="chain"/>, or this context when it already has
    /// it. The layout engine records each arranged element's chain during Measure and re-applies it in
    /// Draw, so both halves of a pass resolve one scope and hand the widget one theme - the agreement the
    /// resolved-value store exists to keep.
    /// </summary>
    public UiWidgetContext WithStyleChain(IReadOnlyList<UiStyleDeclaration>? chain)
    {
        if (ReferenceEquals(chain, StyleChain)) return this;
        return new UiWidgetContext(
            Source, Session, Metrics, Theme, Translation, Bindings, ViewWidth,
            ElementPath, WindowOrigin, ElementId, Node, chain);
    }

    /// <summary>Converts a draw-local rect (as passed to <c>IUiWidget.Draw</c>) into Host window space.</summary>
    public Rect ToWindowRect(Rect rect)
    {
        return new Rect(rect.x + WindowOrigin.x, rect.y + WindowOrigin.y, rect.width, rect.height);
    }
}
