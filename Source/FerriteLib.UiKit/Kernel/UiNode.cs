using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// One arranged element's identity carrier: the object that owns the element's per-element state and the
/// dirty flag that says its Measure must run again (0.4.0 identity layer, node step 1). The layout engine
/// creates a node for every arranged entry during arrange, and <see cref="UiSession"/> caches it under
/// <see cref="Id"/> - so the same element reports the same node across passes, its state survives a
/// re-arrange, and nothing here is keyed by a path string.
/// <para>
/// This is deliberately not yet the whole tree: a node carries identity, kind, declared ordinal, state
/// and its dirty flag, but no parent/child links and no geometry. The arranged rect still lives on the
/// engine's entry, which is why this step ships without rebuilding the engine or the snapshot. What it
/// does remove is the flat-string identity: <see cref="Id"/> is injective (see <see cref="UiNodeId"/>),
/// so the shape where a namespaced kind made two elements share one display path no longer shares state.
/// </para>
/// </summary>
public sealed class UiNode
{
    private readonly UiSession session;
    private readonly Dictionary<string, UiValueState> slots = new(StringComparer.Ordinal);

    internal UiNode(UiSession session, UiNodeId id, string kind, int ordinal)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        Id = id;
        Kind = kind ?? "";
        Ordinal = ordinal;
        State = new UiValueState();
        slots.Add("", State);

        // A node that has never been measured is dirty by definition; the session clears every flag at
        // the end of a successful arrange.
        IsDirty = true;
    }

    /// <summary>This element's stable identity.</summary>
    public UiNodeId Id { get; }

    /// <summary>The display path of <see cref="Id"/>, for diagnostics.</summary>
    public string Path => Id.Path;

    /// <summary>The element's kind (a container's element name, a widget's registered kind, "" for an orphan).</summary>
    public string Kind { get; }

    /// <summary>The element's declared ordinal among its siblings; -1 for an orphan or <see cref="UiNodeId.None"/>.</summary>
    public int Ordinal { get; }

    /// <summary>The element's own value state - the unnamed slot every element has.</summary>
    public UiValueState State { get; }

    /// <summary>Every named state slot this node owns, keyed by the name the widget used.</summary>
    public IReadOnlyDictionary<string, UiValueState> ValueStates => slots;

    /// <summary>
    /// True while this node's own Measure must run again. The engine re-arranges when any live node is
    /// dirty and clears every flag after a successful arrange, so a writer that marks a node dirty gets
    /// its geometry recomputed on the next pass even when no content revision moved.
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
    /// The node's named state slot, created on first use. A composite that drives several stateful
    /// controls inside one element (a slider and a number field) keeps them apart by name; two elements
    /// never share a slot whatever names they use, because the slots belong to the node.
    /// </summary>
    internal UiValueState GetOrCreateState(string stateKey)
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
}
