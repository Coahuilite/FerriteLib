using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Element-identity lane (0.4.0 identity layer). It pins the one thing the old bare-path key could not
/// express: two unnamed siblings of the same kind are two elements, so they must never share an arranged
/// key, a widget instance or a state slot - and the identity they get must be the same value in Measure,
/// in Draw and on the next pass over an unchanged tree, or their state cannot survive a frame.
/// <para>
/// The probe widget deliberately resolves its state through the bare-string overload
/// (<c>ctx.Session.GetOrCreateValueState("probe")</c>), which is the call shape every shipped widget and
/// both backend funnels use. That is the claim this lane grades: the identity layer has to make those
/// callers per-element without a line changing in them.
/// </para>
/// </summary>
internal static class KernelIdentityTests
{
    private const string Scope = "identity-test";
    private const string ProbeKind = "test/identity-probe";
    private const string SlashlessKind = "identityprobe";
    private const string ThrowingKind = "test/identity-throws";
    private static readonly Rect Viewport = new(0f, 0f, 300f, 200f);

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        ResetProbe();
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new IdentityProbeWidget(ProbeKind),
            new[] { "Id", "Kind", "Text", "Dragging", "Cursor", "Hidden", "Tab", "Height" });
        // A kind without a separator, used where a lane grades the sibling-collision rule itself: with a
        // namespaced kind, the Id that spells its generated segment would carry '/' and the separator
        // rule would refuse it first, so that lane would grade nothing.
        UiWidgetRegistry.Register(Scope, SlashlessKind, () => new IdentityProbeWidget(SlashlessKind),
            new[] { "Id", "Kind", "Text", "Dragging", "Cursor", "Hidden", "Tab" });
        UiWidgetRegistry.Register(Scope, ThrowingKind, () => new ThrowingProbeWidget(),
            new[] { "Id", "Kind", "Text" });

        try
        {
            Run("two unnamed same-kind siblings never share an arranged key", VerifyArrangedKeysAreUnique);
            Run("unnamed siblings get distinct identities, stable between Measure and Draw", VerifyIdentitiesAreDistinctAndStable);
            Run("identity survives a re-arrange and a Hidden sibling", VerifyIdentitySurvivesHideAndReArrange);
            Run("a Tab switch does not renumber the visible sibling", VerifyTabSwitchDoesNotRenumber);
            Run("a declared Id may not spell a sibling's generated segment", VerifyDeclaredIdMayNotSpellAGeneratedSegment);
            Run("a declared Id may not forge a deeper path with '/'", VerifyDeclaredIdMayNotForgeADeeperPath);
            Run("per-element state does not cross between unnamed siblings", VerifyStateIsIsolatedBetweenSiblings);
            Run("a throwing sibling does not move the next element's state slot", VerifyThrowerDoesNotLeakTheScope);
            Run("hover claims carry the element that made them", VerifyHoverClaimCarriesItsElement);
            Run("a node owns its element's state and survives a re-arrange", VerifyNodeOwnsItsState);
            Run("a namespaced kind no longer shares identity or state", VerifySeparatorKindDoesNotAliasState);
            Run("a node marked dirty forces the next arrange to re-measure", VerifyDirtyNodeForcesReArrange);
            Run("nodes carry the arranged tree and its geometry", VerifyNodeHierarchyAndGeometry);
            Run("a widget's sub-node keeps its own identity and state", VerifySubNodeIdentityAndState);
            Run("recovery slots are node-keyed where display paths collide", VerifyRecoverySlotsAreNodeKeyed);
            Run("scroll state and scroll targets are node-keyed", VerifyScrollStateIsNodeKeyed);
        }
        finally
        {
            ResetProbe();
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
        }

        return failures;
    }

    /// <summary>
    /// Runs one assertion and counts its failure. Counting is done here rather than by summing the return
    /// value at each call site: a lane that prints FAIL and still returns zero failures is the silent hole
    /// this harness exists to close, and it was one until the node step's run caught it.
    /// </summary>
    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    // --- the four claims -----------------------------------------------------------------------

    /// <summary>
    /// The alias itself, observed without naming the new type: every arranged entry must own a key, and
    /// the old engine emitted the same key twice for two unnamed siblings.
    /// </summary>
    private static void VerifyArrangedKeysAreUnique()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in snapshot.VisibleIds)
        {
            if (!seen.Add(key))
            {
                throw new Exception("two arranged entries share the key '" + key + "'; identity must not alias");
            }
        }

        if (seen.Count != 3)
        {
            throw new Exception("expected row + two siblings as three keyed entries, got " + seen.Count);
        }
    }

    private static void VerifyIdentitiesAreDistinctAndStable()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());
        host.DrawFrame(Viewport);

        UiNodeId measuredA = IdentityOf("A", "measure");
        UiNodeId measuredB = IdentityOf("B", "measure");
        UiNodeId drawnA = IdentityOf("A", "draw");
        UiNodeId drawnB = IdentityOf("B", "draw");

        if (measuredA.IsNone || measuredB.IsNone)
        {
            throw new Exception("an unnamed sibling was left without an identity: A=" + measuredA + ", B=" + measuredB);
        }

        if (measuredA == measuredB || drawnA == drawnB)
        {
            throw new Exception("two unnamed same-kind siblings collapsed onto one identity: " + drawnA);
        }

        if (measuredA != drawnA || measuredB != drawnB)
        {
            throw new Exception("identity changed between Measure and Draw: measured " + measuredA + "/" + measuredB
                + ", drawn " + drawnA + "/" + drawnB);
        }

        if (string.Equals(measuredA.Key, measuredB.Key, StringComparison.Ordinal))
        {
            throw new Exception("two unnamed same-kind siblings share one canonical key: " + measuredA.Key);
        }

        if (measuredA.Path.Length == 0 || !string.Equals(measuredA.Path, PathOf("A", "draw"), StringComparison.Ordinal))
        {
            throw new Exception("the context path is not the identity's display path: '" + measuredA.Path
                + "' vs '" + PathOf("A", "draw") + "'");
        }
    }

    private static void VerifyIdentitySurvivesHideAndReArrange()
    {
        ResetProbe();
        UiNodeId before;
        using (UiHost host = Host(TwoSiblingPage()))
        {
            host.DrawFrame(Viewport);
            before = IdentityOf("B", "draw");

            // One content-revision bump forces the engine to re-measure the same tree: the sibling must
            // keep the identity it had, which is what makes its state survive a data refresh.
            host.Session.BumpContentRevision();
            ResetProbe();
            host.DrawFrame(Viewport);
            UiNodeId after = IdentityOf("B", "measure");
            if (after != before)
            {
                throw new Exception("re-arrange renumbered B: " + before + " -> " + after);
            }
        }

        // Hiding the declared sibling before it must not renumber B either: the ordinal is the declared
        // index, not the index among the children that happened to be visible.
        ResetProbe();
        using (UiHost hidden = Host(HiddenSiblingPage()))
        {
            hidden.DrawFrame(Viewport);
            UiNodeId withHiddenA = IdentityOf("B", "draw");
            if (withHiddenA != before)
            {
                throw new Exception("hiding the preceding sibling renumbered B: " + before + " -> " + withHiddenA);
            }
        }
    }

    /// <summary>
    /// A workspace Tab switch hides one sibling and shows the other. The visible one must keep the
    /// identity it has in a page with no Tab attributes at all: the ordinal is the declared index, so
    /// filtering by visibility can never renumber it, and the hidden sibling keeps its state slot.
    /// </summary>
    private static void VerifyTabSwitchDoesNotRenumber()
    {
        ResetProbe();
        string tab = "TabB";
        var bindings = new UiBindings();
        bindings.BindValue(UiBindings.ActiveTabKey, () => tab, value => tab = value);

        using UiHost host = Host(TabSiblingPage(), bindings);
        host.DrawFrame(Viewport);

        UiNodeId b = IdentityOf("B", "draw");
        if (!string.Equals(b.Path, "row/" + ProbeKind + "[1]", StringComparison.Ordinal))
        {
            throw new Exception("the Tab-gated second sibling did not keep its declared ordinal: " + b.Path);
        }

        tab = "TabA";
        ResetProbe();
        host.Session.BumpContentRevision();
        host.DrawFrame(Viewport);

        UiNodeId a = IdentityOf("A", "draw");
        if (!string.Equals(a.Path, "row/" + ProbeKind + "[0]", StringComparison.Ordinal))
        {
            throw new Exception("a Tab switch renumbered the visible sibling: " + a.Path);
        }

        if (!host.Session.GetValueStates(b).ContainsKey("probe"))
        {
            throw new Exception("the Tab-hidden sibling lost its state slot");
        }
    }

    /// <summary>
    /// The identity grammar's one hole, closed where the rest of the creation-time contract lives: an
    /// unnamed sibling contributes an identity segment of <c>Kind[declaredIndex]</c>, so a declared Id
    /// written as exactly that would give two siblings one key. The refusal is a collision check, not a
    /// ban on the spelling - which is what keeps it off every manifest that never writes such an Id.
    /// </summary>
    private static void VerifyDeclaredIdMayNotSpellAGeneratedSegment()
    {
        // Slash-free on purpose: see the registration note above.
        string generated = SlashlessKind + "[0]";

        ExpectRefusal(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"row\">"
            + "<Widget Kind=\"" + SlashlessKind + "\" Text=\"A\" Cursor=\"1\" />"
            + "<Widget Id=\"" + generated + "\" Kind=\"" + SlashlessKind + "\" Text=\"B\" Cursor=\"2\" />"
            + "</Row></UiPage>",
            generated,
            "a child Id spelling a sibling's generated segment");

        ExpectRefusal(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Kind=\"" + SlashlessKind + "\" Text=\"A\" Cursor=\"1\" />"
            + "<Widget Id=\"" + generated + "\" Kind=\"" + SlashlessKind + "\" Text=\"B\" Cursor=\"2\" />"
            + "</UiPage>",
            generated,
            "a root Id spelling a sibling's generated segment");

        // The same Id shape with no unnamed sibling beside it stays legal: an Id that merely looks like
        // the generated form collides with nothing, and that is why existing manifests are unaffected
        // (measured: no Id in this repository, its harness or the wired consumer's two Schema=2
        // manifests ends in "[<digits>]").
        UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"row\">"
            + "<Widget Id=\"" + generated + "\" Kind=\"" + SlashlessKind + "\" Text=\"B\" Cursor=\"2\" />"
            + "</Row></UiPage>");
    }

    /// <summary>
    /// The forgery shape a flat key admits when an Id carries the path separator: a name spelled like
    /// the path of some other element's descendant. Refused at creation time, because a key two
    /// elements share is exactly the alias the identity layer removes - and it surfaces far from the
    /// manifest, as one control quietly holding another control's state.
    /// </summary>
    private static void VerifyDeclaredIdMayNotForgeADeeperPath()
    {
        ExpectRefusal(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\"><Widget Kind=\"" + ProbeKind + "\" Text=\"A\" Cursor=\"1\" /></Row>"
            + "<Widget Id=\"row/" + ProbeKind + "\" Kind=\"" + ProbeKind + "\" Text=\"B\" Cursor=\"2\" />"
            + "</UiPage>",
            "row/" + ProbeKind,
            "a root Id containing the path separator");

        ExpectRefusal(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"row\">"
            + "<Widget Id=\"a/b\" Kind=\"" + ProbeKind + "\" Text=\"B\" Cursor=\"2\" />"
            + "</Row></UiPage>",
            "a/b",
            "a child Id containing the path separator");

        // The rule is about the separator, not about unusual names - which is why no existing manifest
        // is affected: a name that merely looks like a path with another character in it stays legal.
        UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"row\">"
            + "<Widget Id=\"row-2\" Kind=\"" + ProbeKind + "\" Text=\"B\" Cursor=\"2\" />"
            + "</Row></UiPage>");
    }

    private static void ExpectRefusal(string xml, string named, string what)
    {
        try
        {
            UiLayoutManifest.Parse(xml);
        }
        catch (FormatException ex)
        {
            if (ex.Message.IndexOf(named, StringComparison.Ordinal) < 0)
            {
                throw new Exception("the refusal of " + what + " does not name '" + named + "': " + ex.Message);
            }

            return;
        }

        throw new Exception(what + " was accepted; the two siblings would share one identity key");
    }

    private static void VerifyStateIsIsolatedBetweenSiblings()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());
        host.DrawFrame(Viewport);

        UiNodeId a = IdentityOf("A", "draw");
        UiNodeId b = IdentityOf("B", "draw");

        IReadOnlyDictionary<string, UiValueState> slotsA = host.Session.GetValueStates(a);
        IReadOnlyDictionary<string, UiValueState> slotsB = host.Session.GetValueStates(b);
        if (!slotsA.TryGetValue("probe", out UiValueState? stateA) || !slotsB.TryGetValue("probe", out UiValueState? stateB))
        {
            throw new Exception("an element drew without owning a 'probe' slot (A=" + slotsA.Count + ", B=" + slotsB.Count + ")");
        }

        if (ReferenceEquals(stateA, stateB))
        {
            throw new Exception("both siblings share one UiValueState object");
        }

        if (stateA!.EditText != "A" || stateB!.EditText != "B")
        {
            throw new Exception("edit buffers crossed: A='" + stateA.EditText + "', B='" + stateB.EditText + "'");
        }

        if (!stateA.Dragging || stateA.Cursor != 1 || stateB.Dragging || stateB.Cursor != 2)
        {
            throw new Exception("drag state crossed: A(dragging=" + stateA.Dragging + ", cursor=" + stateA.Cursor
                + "), B(dragging=" + stateB.Dragging + ", cursor=" + stateB.Cursor + ")");
        }

        if (IdentityProbeWidget.MissedOwnSlot.Count > 0)
        {
            throw new Exception("the bare-string overload resolved outside the drawing element for: "
                + string.Join(", ", IdentityProbeWidget.MissedOwnSlot));
        }

        // Nothing may have landed in the element-less namespace: a read outside any element resolves
        // there, and a widget's state that aliased into it would make this non-empty.
        UiValueState unscoped = host.Session.GetOrCreateValueState("probe");
        if (unscoped.EditText.Length != 0 || unscoped.Dragging)
        {
            throw new Exception("widget state leaked into the session-level namespace");
        }
    }

    private static void VerifyThrowerDoesNotLeakTheScope()
    {
        ResetProbe();
        using UiHost host = Host(ThrowerPage());
        host.DrawFrame(Viewport);

        UiNodeId after = IdentityOf("after", "draw");
        IReadOnlyDictionary<string, UiValueState> slots = host.Session.GetValueStates(after);
        if (!slots.TryGetValue("probe", out UiValueState? state) || state.EditText != "after")
        {
            throw new Exception("the element drawn after a throwing sibling did not get its own slot");
        }

        if (IdentityProbeWidget.MissedOwnSlot.Count > 0)
        {
            throw new Exception("a widget after the thrower resolved outside its own element: "
                + string.Join(", ", IdentityProbeWidget.MissedOwnSlot));
        }

        if (!host.Session.ActiveElement.IsNone)
        {
            throw new Exception("the element scope outlived the pass: ActiveElement=" + host.Session.ActiveElement);
        }
    }

    private static void VerifyHoverClaimCarriesItsElement()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());

        // Both siblings claim the SAME string, because a widget's help key is its own vocabulary: the
        // claim string therefore cannot tell them apart, and the identity is the only axis that can.
        IdentityProbeWidget.ClaimingTag = "A";
        host.DrawFrame(Viewport);
        UiNodeId a = IdentityOf("A", "draw");
        if (!string.Equals(host.Session.HoverClaim, IdentityProbeWidget.HelpKey, StringComparison.Ordinal))
        {
            throw new Exception("the hover claim was not presented in the pass that made it");
        }

        if (host.Session.HoverClaimElement != a)
        {
            throw new Exception("the claim is not attributed to A: " + host.Session.HoverClaimElement);
        }

        host.Session.HoverGraceFrames = 2;
        ResetProbe();
        IdentityProbeWidget.ClaimingTag = "B";
        host.DrawFrame(Viewport);
        UiNodeId b = IdentityOf("B", "draw");

        if (!string.Equals(host.Session.HoverClaim, IdentityProbeWidget.HelpKey, StringComparison.Ordinal))
        {
            throw new Exception("both siblings claim the same string, so the string read cannot distinguish them");
        }

        if (host.Session.HoverClaimElement != b || host.Session.HoverClaimElement == a)
        {
            throw new Exception("a live claim was not re-attributed to B: " + host.Session.HoverClaimElement);
        }

        // Two claim-free passes: the first is the boundary that holds the finished claim, the second is
        // the grace pass that re-presents it. The held claim must carry B's identity, not A's.
        IdentityProbeWidget.ClaimingTag = "";
        host.DrawFrame(Viewport);
        host.DrawFrame(Viewport);

        if (!string.Equals(host.Session.HoverClaim, IdentityProbeWidget.HelpKey, StringComparison.Ordinal))
        {
            throw new Exception("the grace window did not re-present the held claim");
        }

        if (host.Session.HoverClaimElement != b)
        {
            throw new Exception("the held claim lost its element attribution: " + host.Session.HoverClaimElement);
        }
    }

    /// <summary>
    /// The node step's first claim: the element's state is the node's state, that node is the session's
    /// node for the identity, and a re-arrange hands the same node back instead of replacing it - which
    /// is what lets state survive a frame with no string key in between.
    /// </summary>
    private static void VerifyNodeOwnsItsState()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());
        host.DrawFrame(Viewport);

        UiNodeId idA = IdentityOf("A", "draw");
        UiNode? first = NodeOf("A", "draw");
        if (first == null)
        {
            throw new Exception("the probe drew without a node");
        }

        if (!ReferenceEquals(first, host.Session.GetNode(idA)))
        {
            throw new Exception("the session's node for the identity is not the node the widget was handed");
        }

        if (!ReferenceEquals(first.State, host.Session.GetOrCreateValueState(idA)))
        {
            throw new Exception("the element's own state is not the node's state");
        }

        if (!ReferenceEquals(first.ValueStates["probe"], host.Session.GetOrCreateValueState(idA, "probe")))
        {
            throw new Exception("a named slot is not the node's slot");
        }

        host.DrawFrame(Viewport);
        if (!ReferenceEquals(first, NodeOf("A", "draw")))
        {
            throw new Exception("a re-arrange replaced the node instead of reusing it");
        }

        if (!first.ValueStates.TryGetValue("probe", out UiValueState? state) || state.EditText != "A")
        {
            throw new Exception("the element's state did not survive the pass");
        }
    }

    /// <summary>
    /// The shape the flat key could not survive: a namespaced kind inside a generated segment makes two
    /// different elements print the same display path (the unnamed first sibling generates
    /// <c>test/identity-probe[0]</c>, and the second one is literally named that under an
    /// <c>Id="test"</c> row). Identity is structural now, so the two must keep separate nodes and state.
    /// </summary>
    private static void VerifySeparatorKindDoesNotAliasState()
    {
        ResetProbe();
        using UiHost host = Host(SeparatorKindPage());
        host.DrawFrame(Viewport);

        UiNodeId a = IdentityOf("A", "draw");
        UiNodeId b = IdentityOf("B", "draw");
        UiNode? nodeA = NodeOf("A", "draw");
        UiNode? nodeB = NodeOf("B", "draw");
        if (nodeA == null || nodeB == null)
        {
            throw new Exception("a sibling drew without a node");
        }

        if (a == b || string.Equals(a.Key, b.Key, StringComparison.Ordinal))
        {
            throw new Exception("two elements still share one identity key: " + a.Key);
        }

        if (ReferenceEquals(nodeA, nodeB)
            || !ReferenceEquals(nodeA, host.Session.GetNode(a))
            || !ReferenceEquals(nodeB, host.Session.GetNode(b)))
        {
            throw new Exception("the session did not keep the two nodes apart");
        }

        IReadOnlyDictionary<string, UiValueState> slotsA = host.Session.GetValueStates(a);
        IReadOnlyDictionary<string, UiValueState> slotsB = host.Session.GetValueStates(b);
        if (!slotsA.TryGetValue("probe", out UiValueState? stateA) || !slotsB.TryGetValue("probe", out UiValueState? stateB))
        {
            throw new Exception("a sibling did not own its own 'probe' slot");
        }

        if (ReferenceEquals(stateA, stateB) || stateA!.EditText != "A" || stateB!.EditText != "B"
            || stateA.Cursor != 1 || stateB.Cursor != 2)
        {
            throw new Exception("state crossed between the two elements: A='" + stateA.EditText
                + "', B='" + stateB.EditText + "'");
        }

        // Residual, asserted on purpose: the display path is still ambiguous for this shape - the fit
        // audit and the recovery band can print the same text for two elements - while identity and
        // state no longer depend on it. A step that narrows the residual updates this assertion too.
        if (!string.Equals(a.Path, b.Path, StringComparison.Ordinal))
        {
            throw new Exception("the display path is no longer ambiguous; narrow the documented residual and "
                + "update this assertion: '" + a.Path + "' vs '" + b.Path + "'");
        }

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
        int printed = 0;
        foreach (string key in snapshot.VisibleIds)
        {
            if (string.Equals(key, a.Path, StringComparison.Ordinal))
            {
                printed++;
            }
        }

        if (printed != 2)
        {
            throw new Exception("expected the shared display path to be printed twice, got " + printed);
        }
    }

    /// <summary>
    /// The dirty flag is the per-element invalidation signal the roadmap asks for: an unchanged pass
    /// reuses the arranged snapshot, and one node asking for a re-measure is enough to force the next
    /// arrange even though size, content revision, definition and language all stand still.
    /// </summary>
    private static void VerifyDirtyNodeForcesReArrange()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());
        host.DrawFrame(Viewport);
        if (CountObservations("A", "measure") != 1)
        {
            throw new Exception("expected one Measure on the first arrange, got " + CountObservations("A", "measure"));
        }

        host.DrawFrame(Viewport);
        if (CountObservations("A", "measure") != 1)
        {
            throw new Exception("an unchanged pass re-measured the tree; the snapshot cache stopped working");
        }

        UiNode? node = NodeOf("A", "draw");
        if (node == null)
        {
            throw new Exception("the probe drew without a node");
        }

        if (node.IsDirty)
        {
            throw new Exception("a node is still dirty after a successful arrange");
        }

        node.MarkDirty();
        if (!node.IsDirty)
        {
            throw new Exception("MarkDirty did not set the flag");
        }

        host.DrawFrame(Viewport);
        if (CountObservations("A", "measure") != 2)
        {
            throw new Exception("a dirty node did not force a re-arrange");
        }

        if (node.IsDirty)
        {
            throw new Exception("the arrange did not clear the dirty flag");
        }
    }

    /// <summary>
    /// The tree and its geometry live on nodes now: a parent lists its arranged children in declared
    /// order, every arranged node carries the rect the snapshot reports, and a node that was not arranged
    /// this pass keeps its identity and state while losing its geometry.
    /// </summary>
    private static void VerifyNodeHierarchyAndGeometry()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());
        host.DrawFrame(Viewport);

        UiNode? row = host.Session.GetNodeByElementId("row");
        UiNode? a = NodeOf("A", "draw");
        UiNode? b = NodeOf("B", "draw");
        if (row == null || a == null || b == null)
        {
            throw new Exception("an arranged element has no node");
        }

        if (!ReferenceEquals(a.Parent, row) || !ReferenceEquals(b.Parent, row))
        {
            throw new Exception("the siblings are not linked to their container's node");
        }

        if (row.Children.Count != 2 || !ReferenceEquals(row.Children[0], a) || !ReferenceEquals(row.Children[1], b))
        {
            throw new Exception("the container node does not list its children in declared order");
        }

        if (!a.IsArranged || !a.Rect.HasValue || !b.Rect.HasValue)
        {
            throw new Exception("arranged geometry was not published to the nodes");
        }

        if (Math.Abs(b.Rect.Value.x - (a.Rect.Value.x + a.Rect.Value.width + 4f)) > 0.01f)
        {
            throw new Exception("the node rects do not match the arranged row geometry (b.x=" + b.Rect.Value.x + ")");
        }

        // A child that is not arranged this pass leaves its parent's list and keeps no geometry.
        ResetProbe();
        using (UiHost hidden = Host(HiddenSiblingPage()))
        {
            hidden.DrawFrame(Viewport);
            UiNode? hiddenRow = hidden.Session.GetNodeByElementId("row");
            if (hiddenRow == null || hiddenRow.Children.Count != 1)
            {
                throw new Exception("a hidden child stayed in its parent's children list");
            }

            UiNode only = hiddenRow.Children[0];
            if (!ReferenceEquals(only, NodeOf("B", "draw")) || !only.IsArranged)
            {
                throw new Exception("the remaining child is not the arranged visible sibling");
            }
        }
    }

    /// <summary>
    /// A widget's sub-control gets its own node: distinct identity, its own state, a display path under the
    /// element, and the same node object on the next pass.
    /// </summary>
    private static void VerifySubNodeIdentityAndState()
    {
        ResetProbe();
        using UiHost host = Host(TwoSiblingPage());
        host.DrawFrame(Viewport);

        UiNode? leftA = SubOf("A", "draw");
        UiNode? leftB = SubOf("B", "draw");
        UiNode? elementA = NodeOf("A", "draw");
        if (leftA == null || leftB == null || elementA == null)
        {
            throw new Exception("the probe minted no sub-node");
        }

        if (ReferenceEquals(leftA, leftB) || leftA.Id == leftB.Id)
        {
            throw new Exception("two sub-controls share one node identity");
        }

        if (leftA.Id == elementA.Id)
        {
            throw new Exception("a sub-node reused its element's identity");
        }

        if (!ReferenceEquals(leftA.Parent, elementA) || !elementA.Children.Contains(leftA))
        {
            throw new Exception("the sub-node is not linked under the element's node");
        }

        if (!leftA.Path.EndsWith("/@left", StringComparison.Ordinal))
        {
            throw new Exception("the sub-node's display path does not name it: " + leftA.Path);
        }

        if (!leftA.ValueStates.TryGetValue("buffer", out UiValueState? bufferA) || bufferA.EditText != "A"
            || !leftB.ValueStates.TryGetValue("buffer", out UiValueState? bufferB) || bufferB.EditText != "B")
        {
            throw new Exception("sub-node state crossed between the two elements");
        }

        host.DrawFrame(Viewport);
        if (!ReferenceEquals(leftA, SubOf("A", "draw")))
        {
            throw new Exception("a re-arrange replaced the sub-node instead of reusing it");
        }
    }

    /// <summary>
    /// The tripwire, updated rather than removed by the node step: the display path still names two
    /// different elements alike - <c>VisibleIds</c> prints it twice - while the recovery slot, which this
    /// step moved to the node, is two slots. A step that narrows the display path must update this
    /// assertion and the api-tiers residual line.
    /// </summary>
    private static void VerifyRecoverySlotsAreNodeKeyed()
    {
        ResetProbe();
        using UiHost host = Host(ThrowingSeparatorPage());
        host.DrawFrame(Viewport);

        if (host.Session.TrippedNodes.Count != 2)
        {
            throw new Exception("expected two tripped nodes, got " + host.Session.TrippedNodes.Count);
        }

        var tripped = new List<UiNode>(host.Session.TrippedNodes);
        if (ReferenceEquals(tripped[0], tripped[1]) || tripped[0].Id == tripped[1].Id)
        {
            throw new Exception("two tripped elements share one recovery slot");
        }

        if (!host.Session.TryGetTripLog(tripped[0], out _) || !host.Session.TryGetTripLog(tripped[1], out _))
        {
            throw new Exception("a tripped node has no diagnostic of its own");
        }

        if (!string.Equals(tripped[0].Path, tripped[1].Path, StringComparison.Ordinal))
        {
            throw new Exception("the display-path collision this tripwire pins is gone; update the tripwire and the "
                + "api-tiers residual line: '" + tripped[0].Path + "' vs '" + tripped[1].Path + "'");
        }

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(Viewport.width, Viewport.height));
        int printed = 0;
        foreach (string key in snapshot.VisibleIds)
        {
            if (string.Equals(key, tripped[0].Path, StringComparison.Ordinal))
            {
                printed++;
            }
        }

        if (printed != 2)
        {
            throw new Exception("expected the shared display path to be printed twice, got " + printed);
        }
    }

    /// <summary>
    /// Scroll state belongs to the scroll container's node, and a scroll-target request is consumer
    /// vocabulary that the engine resolves to a node: consumed exactly once, and left pending while no
    /// arranged element carries its Id.
    /// </summary>
    private static void VerifyScrollStateIsNodeKeyed()
    {
        ResetProbe();
        using UiHost host = Host(ScrollSiblingPage());
        host.DrawFrame(Viewport);

        UiNode? scrollA = NodeOf("A", "draw")?.Parent;
        UiNode? scrollB = NodeOf("B", "draw")?.Parent;
        if (scrollA == null || scrollB == null || ReferenceEquals(scrollA, scrollB))
        {
            throw new Exception("the two unnamed Scroll siblings did not get their own nodes");
        }

        host.Session.SetScrollPosition(scrollA, new Vector2(0f, 5f));
        if (Math.Abs(host.Session.GetScrollPosition(scrollB).y) > 0.01f)
        {
            throw new Exception("scroll state crossed between two sibling scroll containers");
        }

        if (Math.Abs(host.Session.GetScrollPosition(scrollA).y - 5f) > 0.01f)
        {
            throw new Exception("the scroll container's own position was lost");
        }

        // A scroll-target request: consumer vocabulary in, node state out, consumed exactly once.
        ResetProbe();
        using (UiHost target = Host(TargetPage()))
        {
            target.Session.SetScrollTarget("target");
            target.MeasureAndArrange(new Vector2(300f, 200f));
            UiNode? scroll = target.Session.GetNodeByElementId("scroll");
            if (scroll == null)
            {
                throw new Exception("the scroll element has no node");
            }

            if (target.Session.ScrollTargetElementId != null)
            {
                throw new Exception("the resolved request was not consumed");
            }

            if (target.Session.GetScrollPosition(scroll).y <= 0f)
            {
                throw new Exception("the target did not move its scroll container");
            }

            target.Session.SetScrollPosition(scroll, new Vector2(0f, 7f));
            target.MeasureAndArrange(new Vector2(300f, 200f));
            if (Math.Abs(target.Session.GetScrollPosition(scroll).y - 7f) > 0.01f)
            {
                throw new Exception("a consumed request applied a second time");
            }

            target.Session.SetScrollTarget("missing-id");
            target.MeasureAndArrange(new Vector2(300f, 200f));
            if (target.Session.ScrollTargetElementId == null)
            {
                throw new Exception("an unresolvable target was dropped instead of staying pending");
            }
        }
    }

    // --- fixture -------------------------------------------------------------------------------

    private static UiHost Host(string xml, IUiBindings? bindings = null)
    {
        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(xml),
            bindings ?? new UiBindings(),
            UiTheme.DarkGold,
            new FixedMetrics(),
            new FixedTranslation());
    }

    private static string TwoSiblingPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Gap=\"4\">"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"A\" Dragging=\"true\" Cursor=\"1\" />"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"B\" Dragging=\"false\" Cursor=\"2\" />"
            + "</Row>"
            + "</UiPage>";
    }

    private static string HiddenSiblingPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Gap=\"4\">"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"A\" Hidden=\"true\" Cursor=\"1\" />"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"B\" Cursor=\"2\" />"
            + "</Row>"
            + "</UiPage>";
    }

    private static string TabSiblingPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Gap=\"4\">"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"A\" Tab=\"TabA\" Cursor=\"1\" />"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"B\" Tab=\"TabB\" Cursor=\"2\" />"
            + "</Row>"
            + "</UiPage>";
    }

    /// <summary>
    /// The shape the flat key could not survive: a namespaced kind inside a generated segment makes two
    /// different elements print the same display path (the unnamed first sibling generates
    /// <c>test/identity-probe[0]</c>, and the second one is literally named that under an
    /// <c>Id="test"</c> row). Identity is structural now, so the two must keep separate nodes and state.
    /// </summary>
    private static string SeparatorKindPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\">"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"A\" Cursor=\"1\" />"
            + "<Row Id=\"test\"><Widget Id=\"identity-probe[0]\" Kind=\"" + ProbeKind + "\" Text=\"B\" Cursor=\"2\" /></Row>"
            + "</Row></UiPage>";
    }

    private static string ThrowingSeparatorPage()
    {
        // The forgery shape SeparatorKindPage pins, with widgets that trip: the unnamed first sibling
        // generates test/identity-throws[0], and the second one declares an Id spelling that display path.
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"row\">"
            + "<Widget Kind=\"" + ThrowingKind + "\" Text=\"A\" />"
            + "<Row Id=\"test\"><Widget Id=\"identity-throws[0]\" Kind=\"" + ThrowingKind + "\" Text=\"B\" /></Row>"
            + "</Row></UiPage>";
    }

    private static string ScrollSiblingPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Row Id=\"row\">"
            + "<Scroll Height=\"50\"><Widget Kind=\"" + ProbeKind + "\" Text=\"A\" Cursor=\"1\" /></Scroll>"
            + "<Scroll Height=\"50\"><Widget Kind=\"" + ProbeKind + "\" Text=\"B\" Cursor=\"2\" /></Scroll>"
            + "</Row></UiPage>";
    }

    private static string TargetPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Scroll Id=\"scroll\" Height=\"50\">"
            + "<Column Id=\"col\">"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"A\" Cursor=\"1\" Height=\"300\" />"
            + "<Widget Id=\"target\" Kind=\"" + ProbeKind + "\" Text=\"B\" Cursor=\"2\" Height=\"30\" />"
            + "</Column></Scroll></UiPage>";
    }

    private static string ThrowerPage()
    {
        return "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Gap=\"4\">"
            + "<Widget Kind=\"" + ThrowingKind + "\" Text=\"boom\" />"
            + "<Widget Kind=\"" + ProbeKind + "\" Text=\"after\" Cursor=\"3\" />"
            + "</Row>"
            + "</UiPage>";
    }

    private static UiNodeId IdentityOf(string tag, string phase)
    {
        foreach (Observation observation in IdentityProbeWidget.Seen)
        {
            if (string.Equals(observation.Tag, tag, StringComparison.Ordinal)
                && string.Equals(observation.Phase, phase, StringComparison.Ordinal))
            {
                return observation.Id;
            }
        }

        throw new Exception("no " + phase + " observation for '" + tag + "'");
    }

    private static UiNode? NodeOf(string tag, string phase)
    {
        foreach (Observation observation in IdentityProbeWidget.Seen)
        {
            if (string.Equals(observation.Tag, tag, StringComparison.Ordinal)
                && string.Equals(observation.Phase, phase, StringComparison.Ordinal))
            {
                return observation.Node;
            }
        }

        throw new Exception("no " + phase + " observation for '" + tag + "'");
    }

    private static int CountObservations(string tag, string phase)
    {
        int count = 0;
        foreach (Observation observation in IdentityProbeWidget.Seen)
        {
            if (string.Equals(observation.Tag, tag, StringComparison.Ordinal)
                && string.Equals(observation.Phase, phase, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static UiNode? SubOf(string tag, string phase)
    {
        foreach (Observation observation in IdentityProbeWidget.Seen)
        {
            if (string.Equals(observation.Tag, tag, StringComparison.Ordinal)
                && string.Equals(observation.Phase, phase, StringComparison.Ordinal))
            {
                return observation.Sub;
            }
        }

        throw new Exception("no " + phase + " observation for '" + tag + "'");
    }

    private static string PathOf(string tag, string phase)
    {
        foreach (Observation observation in IdentityProbeWidget.Seen)
        {
            if (string.Equals(observation.Tag, tag, StringComparison.Ordinal)
                && string.Equals(observation.Phase, phase, StringComparison.Ordinal))
            {
                return observation.Path;
            }
        }

        throw new Exception("no " + phase + " observation for '" + tag + "'");
    }

    private static void ResetProbe()
    {
        IdentityProbeWidget.Seen.Clear();
        IdentityProbeWidget.MissedOwnSlot.Clear();
        IdentityProbeWidget.ClaimingTag = "";
    }

    // --- the probe -----------------------------------------------------------------------------

    private readonly struct Observation
    {
        internal readonly string Tag;
        internal readonly string Phase;
        internal readonly UiNodeId Id;
        internal readonly string Path;
        internal readonly UiNode? Node;
        internal readonly UiNode? Sub;

        internal Observation(string tag, string phase, UiNodeId id, string path, UiNode? node, UiNode? sub)
        {
            Tag = tag;
            Phase = phase;
            Id = id;
            Path = path;
            Node = node;
            Sub = sub;
        }
    }

    /// <summary>
    /// Records the identity it was handed in Measure and in Draw, writes its own state through the
    /// bare-string overload, and checks in-pass that the overload resolved into the element it is.
    /// </summary>
    private sealed class IdentityProbeWidget : IUiWidget
    {
        internal const string HelpKey = "identity-test/help";

        private readonly string kind;

        internal static readonly List<Observation> Seen = new();
        internal static readonly List<string> MissedOwnSlot = new();
        internal static string ClaimingTag = "";

        private string tag = "";
        private bool dragging;
        private int cursor;
        private float height = 14f;

        internal IdentityProbeWidget(string kind)
        {
            this.kind = kind;
        }

        public string Kind => kind;

        public void Configure(UiElementSpec spec)
        {
            tag = spec.TryGetAttribute("Text", out string text) ? text : "";
            dragging = spec.TryGetAttribute("Dragging", out string drag)
                && string.Equals(drag.Trim(), "true", StringComparison.OrdinalIgnoreCase);
            cursor = spec.TryGetAttribute("Cursor", out string raw) && int.TryParse(raw.Trim(), out int parsed)
                ? parsed
                : 0;
            height = spec.TryGetAttribute("Height", out string rawHeight)
                && float.TryParse(rawHeight.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float declared)
                ? declared
                : 14f;
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            Seen.Add(new Observation(tag, "measure", ctx.ElementId, ctx.ElementPath, ctx.Node, null));
            return height;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            // A sub-control this widget owns: minted as a node under the element's node, with its own
            // state, so it can be graded for identity and isolation like any element.
            UiWidgetContext left = ctx.Child("left");
            UiNode? sub = left.Node;
            if (sub != null)
            {
                sub.GetOrCreateState("buffer").EditText = tag;
            }

            Seen.Add(new Observation(tag, "draw", ctx.ElementId, ctx.ElementPath, ctx.Node, sub));

            UiValueState state = ctx.Session.GetOrCreateValueState("probe");
            state.EditText = tag;
            state.Dragging = dragging;
            state.Cursor = cursor;

            if (!ReferenceEquals(state, ctx.Session.GetOrCreateValueState(ctx.ElementId, "probe")))
            {
                MissedOwnSlot.Add(tag);
            }

            if (ctx.Session.ActiveElement != ctx.ElementId)
            {
                MissedOwnSlot.Add(tag);
            }

            if (ctx.Node == null || ctx.Node.Id != ctx.ElementId || !ReferenceEquals(ctx.Session.ActiveNode, ctx.Node))
            {
                MissedOwnSlot.Add(tag + " (context node)");
            }

            if (string.Equals(tag, ClaimingTag, StringComparison.Ordinal))
            {
                ctx.Session.ClaimHover(HelpKey);
            }
        }
    }

    /// <summary>Throws in both halves of the pass, so the guard has to recover it.</summary>
    private sealed class ThrowingProbeWidget : IUiWidget
    {
        public string Kind => ThrowingKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            throw new InvalidOperationException("identity probe: measure boom");
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            throw new InvalidOperationException("identity probe: draw boom");
        }
    }

    private sealed class FixedMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => text.Length;

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }

    private sealed class FixedTranslation : IUiTranslation
    {
        public string Translate(string key) => key;

        public int TranslationRevision => 0;
    }
}
