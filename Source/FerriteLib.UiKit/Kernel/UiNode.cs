using System;
using System.Collections.Generic;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One arranged element's identity carrier: the object that owns the element's per-element state, its
/// place in the tree and the dirty flag that says its Measure must run again (0.4.0 identity layer).
/// The layout engine creates a node for every arranged entry during arrange, and <see cref="UiSession"/>
/// caches it under <see cref="Id"/> - so the same element reports the same node across passes, its state
/// survives a re-arrange, and nothing here is keyed by a path string.
/// <para>
/// Geometry lands here too now: <see cref="Rect"/> is the arranged rect and <see cref="ContentRect"/> the
/// scroll content rect, both published once per successful arrange, null for a node that was not arranged
/// in the current tree (a Tab-hidden element keeps its node and state and loses its geometry). The
/// engine's flat entry table is still what draws; the node is what everything else keys on.
/// </para>
/// </summary>
public sealed class UiNode
{
    private readonly UiSession session;
    private readonly Dictionary<string, UiValueState> slots = new(StringComparer.Ordinal);
    private readonly List<UiNode> children = new();

    internal UiNode(UiSession session, UiNodeId id, string kind, int ordinal, bool needsMeasure = true, string elementId = "")
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        Id = id;
        Kind = kind ?? "";
        Ordinal = ordinal;
        ElementId = elementId ?? "";
        State = new UiValueState();
        slots.Add("", State);

        // An element node that has never been measured is dirty by definition; the session clears every
        // flag at the end of a successful arrange. A sub-node has no Measure of its own, so it starts clean.
        IsDirty = needsMeasure;
    }

    /// <summary>This element's stable identity.</summary>
    public UiNodeId Id { get; }

    /// <summary>The display path of <see cref="Id"/>, for diagnostics.</summary>
    public string Path => Id.Path;

    /// <summary>The element's kind (a container's element name, a widget's registered kind, "" for a sub-node).</summary>
    public string Kind { get; }

    /// <summary>The element's declared ordinal among its siblings; -1 for a root, a sub-node or an orphan.</summary>
    public int Ordinal { get; }

    /// <summary>
    /// The element's declared <c>Id</c>, or "" when it has none. This is the one string a consumer
    /// legitimately holds - it is how a page names an element at runtime - so the node carries it, which is
    /// what lets a caller bridge from a declared Id to a node (<see cref="UiSession.GetNodeByElementId"/>)
    /// without reintroducing a string identity key.
    /// </summary>
    public string ElementId { get; }

    /// <summary>The node this one belongs to; null for a manifest root and for the session-level node.</summary>
    public UiNode? Parent { get; private set; }

    /// <summary>
    /// This node's children: the elements arranged inside it, plus any sub-node a widget minted with
    /// <see cref="UiWidgetContext.Child"/>. Rebuilt on every arrange; a sub-node is re-linked the next
    /// time its widget asks for it, which is what keeps its identity (and state) stable across passes.
    /// </summary>
    public IReadOnlyList<UiNode> Children => children;

    /// <summary>The element's arranged rect in page space, or null when it was not arranged this pass.</summary>
    public Rect? Rect { get; internal set; }

    /// <summary>Scroll content rect for a scroll container (scroll-local), or null.</summary>
    public Rect? ContentRect { get; internal set; }

    /// <summary>True when this node was arranged in the current tree and therefore carries a rect.</summary>
    public bool IsArranged => Rect.HasValue;

    /// <summary>The element's own value state - the unnamed slot every node has.</summary>
    public UiValueState State { get; }

    /// <summary>Every named state slot this node owns, keyed by the name the widget used.</summary>
    public IReadOnlyDictionary<string, UiValueState> ValueStates => slots;

    /// <summary>
    /// True while this node's own Measure must run again. The engine re-arranges when any element node is
    /// dirty and clears every flag after a successful arrange, so a writer that marks a node dirty gets its
    /// geometry recomputed on the next pass even when no content revision moved.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>Marks this node's Measure as needed. Idempotent.</summary>
    public void MarkDirty()
    {
        if (IsDirty) return;
        IsDirty = true;
        session.NodeMarkedDirty(this);
    }

    /// <summary>
    /// The node's named state slot, created on first use. A composite that drives several stateful controls
    /// inside one element (a slider and a number field), or a widget that minted a sub-node for one of its
    /// own controls, keeps them apart by name; two nodes never share a slot whatever names they use.
    /// </summary>
    public UiValueState GetOrCreateState(string stateKey)
    {
        if (stateKey == null) throw new ArgumentNullException(nameof(stateKey));
        if (!slots.TryGetValue(stateKey, out UiValueState? state))
        {
            state = new UiValueState();
            slots.Add(stateKey, state);
        }

        return state;
    }

    /// <summary>Clears the flag without notifying the session, which clears its whole dirty set in bulk.</summary>
    internal void ClearDirtyFlag()
    {
        IsDirty = false;
    }

    /// <summary>Hands the child its parent and keeps it once, in declared order.</summary>
    internal void AddChild(UiNode child)
    {
        child.Parent = this;
        if (!children.Contains(child))
        {
            children.Add(child);
        }
    }

    /// <summary>Forgets the arranged children; an arrange rebuilds them (and re-links sub-nodes on use).</summary>
    internal void ClearChildren()
    {
        children.Clear();
    }

    /// <summary>Drops this pass's geometry so "not arranged" is a state and not a leftover rect.</summary>
    internal void ClearGeometry()
    {
        Rect = null;
        ContentRect = null;
    }
}
