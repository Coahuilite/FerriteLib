using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
/// <summary>
/// One arranged element's identity carrier: the object that owns the element's per-element state, its
/// place in the tree and the dirty flag that says its Measure must run again (0.4.0 identity layer).
/// The layout engine creates a node for every arranged entry during arrange, and <see cref = "UiSession"/>
/// caches it under <see cref = "Id"/> - so the same element reports the same node across passes, its state
/// survives a re-arrange, and nothing here is keyed by a path string.
/// <para>
/// Geometry lands here too now: <see cref = "Rect"/> is the arranged rect and <see cref = "ContentRect"/> the
/// scroll content rect, both published once per successful arrange, null for a node that was not arranged
/// in the current tree (a Tab-hidden element keeps its node and state and loses its geometry). The
/// engine's flat entry table is still what draws; the node is what everything else keys on.
/// </para>
/// </summary>
public sealed class UiNode
{
    private readonly UiSession session;
    internal UiSession Owner => session;
    private readonly Dictionary<string,object> customState = new(StringComparer.Ordinal);
    private readonly Dictionary<string,Action> customRelease = new(StringComparer.Ordinal);
    private bool released;
    internal UiBindings? OptionHelpOwner;
    internal UiValueEndpoint? OptionHelpEndpoint;
    internal string OptionHelpBind = "", OptionHelpRef = "";
    internal int OptionHelpPass;
    internal bool OptionHelpPopup;
    internal void RequireActive()
    {
        if (released || !session.IsActive) throw new ObjectDisposedException(nameof(UiNode));
    }
    public T GetState<T>(string name,Func<T> create,Action<T>? release=null) where T:class
    {
        if(!session.IsActive||released)throw new ObjectDisposedException(nameof(UiNode));
        if(create==null)throw new ArgumentNullException(nameof(create));
        if(!customState.TryGetValue(name,out object? state)){state=create()??throw new InvalidOperationException("Node state factory returned null.");customState.Add(name,state);if(release!=null){T owned=(T)state;customRelease.Add(name,()=>release(owned));}}
        return state as T??throw new InvalidOperationException("Node state '"+name+"' has another declared type.");
    }
    internal void ResetCustomState()
    {
        var releases=new List<Action>(customRelease.Values);customRelease.Clear();customState.Clear();
        foreach(var release in releases)try{release();}catch(Exception error){session.ReportWarning("[FerriteLib.UiKit] node state release failed at '"+Path+"': "+error);}
    }
    internal void ReleaseState()
    {
        if(released)return;released=true;
        try { Widgets.OptionHelp.Release(this); }
        finally
        {
            ResetCustomState();
            foreach(var slot in slots.Values){if(slot.Editor is IUiEditState editor)editor.Suspend();slot.Editor=null;slot.EditorTarget=null;slot.ChartIdentity=null;}
            slots.Clear();EditingState=null;children.Clear();Parent=null;
        }
    }
    private readonly Dictionary<string, UiControlState> slots = new(StringComparer.Ordinal);
    private readonly List<UiNode> children = new();

    internal UiNode(UiSession session, UiNodeId id, string kind, int ordinal, bool needsMeasure = true, string elementId = "")
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        Id = id;
        Kind = kind ?? "";
        Ordinal = ordinal;
        ElementId = elementId ?? "";
        Properties = new UiElementProperties(MarkDirty);
        State = new UiControlState();
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
    public string Kind { get; private set; }

    /// <summary>The element's declared ordinal among its siblings; -1 for a root, a sub-node or an orphan.</summary>
    public int Ordinal { get; private set; }

    /// <summary>
    /// The element's declared <c>Id</c>, or "" when it has none. This is the one string a consumer
    /// legitimately holds - it is how a page names an element at runtime - so the node carries it, which is
    /// what lets a caller bridge from a declared Id to a node (<see cref="UiSession.GetNodeByElementId"/>)
    /// without reintroducing a string identity key.
    /// </summary>
    public string ElementId { get; private set; }

    /// <summary>The node this one belongs to; null for a manifest root and for the session-level node.</summary>
    public UiNode? Parent { get; private set; }

    /// <summary>
    /// This node's children: the elements arranged inside it, plus any sub-node a widget minted with
    /// <see cref="UiWidgetContext.Child"/>. Declared elements are rebuilt on every arrange; private
    /// sub-nodes remain linked when their owner's measurement is reused.
    /// </summary>
    internal IReadOnlyList<UiNode> Children => children;

    /// <summary>The element's arranged rect in page space, or null when it was not arranged this pass.</summary>
    public UiLayoutRect? Rect { get; internal set; }

    /// <summary>Scroll content rect for a scroll container (scroll-local), or null.</summary>
    public UiLayoutRect? ContentRect { get; internal set; }

    /// <summary>True when this node was arranged in the current tree and therefore carries a rect.</summary>
    public bool IsArranged => Rect.HasValue;

    /// <summary>The element's own value state - the unnamed slot every node has.</summary>
    internal UiControlState State { get; }

    public UiElementProperties Properties { get; }

    /// <summary>Nearest effective update-scope owner, without changing this node identity.</summary>
    public UiNodeId UpdateScopeRoot { get; internal set; }
    internal int LayoutMeasureCount, LayoutArrangeCount;

    internal void SetUpdateScope(UiNodeId scope)
    {
        UpdateScopeRoot = scope;
        foreach (UiNode child in children)
            if (child.Id.Key.IndexOf(UiNodeId.SubNodeMarker) >= 0) child.SetUpdateScope(scope);
    }

    /// <summary>Explicit business identity; change when a getter switches logical objects.</summary>
    public string InteractionScope { get; set; } = "";
    internal UiControlState? EditingState;
    public float SharedLabelWidth { get; internal set; }

    /// <summary>Every named state slot this node owns, keyed by the name the widget used.</summary>
    internal IReadOnlyDictionary<string, UiControlState> ValueStates => slots;

    /// <summary>
    /// True while this node's own Measure must run again. The engine re-arranges when any element node is
    /// dirty and clears every flag after a successful arrange, so a writer that marks a node dirty gets its
    /// geometry recomputed on the next pass even when no content revision moved.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// True while this element is disabled for input: the command it declares answers
    /// <see cref="IUiBindingAccess.CanExecute"/> false. The engine resolves it once per element per draw and
    /// publishes it here because the node is the identity carrier every arranged element owns; the
    /// funnel's interactive entry points consult it, so a disabled element neither executes nor captures
    /// the pointer - one rule for every kind instead of a per-widget branch. Always false for an element
    /// that declares no command, which is why it cannot change the behaviour of a page that binds none.
    /// </summary>
    public bool IsDisabled { get; internal set; }
    public UiReactiveFault? ReactiveFault => QualificationFault ?? PresentationFault;
    internal UiReactiveFault? QualificationFault, PresentationFault;
    internal bool PresentationUnavailable;

    /// <summary>Latest model-derived discrete-selection facts; never a second selected-value authority.</summary>
    public UiSelectionObservation? Selection { get; internal set; }

    /// <summary>
    /// The command key this element declared as its Cancel binding, already resolved through whatever scope
    /// the element sits in (a row of a <c>Repeat</c> carries the item-scoped key, not the declared one), or
    /// empty when it declared none. The engine publishes it once per arrange next to
    /// <see cref="IsDisabled"/> because the node is the only carrier the Cancel ladder can walk: the ladder
    /// runs from a window hook, before any widget's context exists, so a fact it needs has to be on the tree
    /// rather than inside a kind. Executability is NOT cached here — the ladder asks the live binding, which
    /// is the same reason a disabled element's grey-out is resolved per draw and not stored.
    /// </summary>
    public string CancelBind { get; internal set; } = "";

    /// <summary>Marks this node's Measure as needed. Idempotent.</summary>
    public void MarkDirty()
    {
        if(released||!session.IsActive)throw new ObjectDisposedException(nameof(UiNode));
        if (IsDirty) return;
        IsDirty = true;
        session.NodeMarkedDirty(this);
    }
internal UiControlState GetOrCreateState(string stateKey)
    {
        if (stateKey == null) throw new ArgumentNullException(nameof(stateKey));
        if (!slots.TryGetValue(stateKey, out UiControlState? state))
        {
            state = new UiControlState();
            slots.Add(stateKey, state);
        }

        return state;
    }

    /// <summary>
    /// Refreshes the definition-owned facts of an identity that survived a definition change: the same
    /// declared Id can appear under a different kind after a reload, and the node - which belongs to the
    /// identity, not to the definition - must not go on describing the kind it used to be. State is not
    /// touched here: whoever decided the kind changed owns that decision.
    /// </summary>
    internal void RefreshIdentity(string kind, int ordinal, string elementId)
    {
        Kind = kind ?? "";
        Ordinal = ordinal;
        ElementId = elementId ?? "";
    }

    // Restore derived layout facts only, never business state or ended interactions.
    internal Action CaptureArrangement()
    {
        Action restoreAuthoring = Properties.CaptureAuthoring();
        UiNode? parent = Parent;
        UiNode[] priorChildren = children.ToArray();
        UiLayoutRect? rect = Rect;
        UiLayoutRect? content = ContentRect;
        string kind = Kind;
        int ordinal = Ordinal;
        string elementId = ElementId;
        string cancelBinding = CancelBind;
        float sharedLabelWidth = SharedLabelWidth;
        UiNodeId updateScope = UpdateScopeRoot;
        return () =>
        {
            restoreAuthoring();
            Parent = parent;
            children.Clear();
            children.AddRange(priorChildren);
            Rect = rect;
            ContentRect = content;
            RefreshIdentity(kind, ordinal, elementId);
            CancelBind = cancelBinding;
            SharedLabelWidth = sharedLabelWidth;
            UpdateScopeRoot = updateScope;
        };
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

    /// <summary>Forgets declared element links, retaining the owner's private sub-nodes.</summary>
    internal void ClearElementChildren()
    {
        children.RemoveAll(child => child.Id.Key.IndexOf(UiNodeId.SubNodeMarker) < 0);
    }

    internal void ClearChildren()
    {
        children.Clear();
    }

    /// <summary>Drops this pass's geometry so "not arranged" is a state and not a leftover rect.</summary>
    internal void ClearGeometry()
    {
        SharedLabelWidth = 0;
        Rect = null;
        ContentRect = null;
    }
}
