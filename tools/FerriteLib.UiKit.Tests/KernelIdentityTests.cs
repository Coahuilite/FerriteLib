using System;
using System.Collections.Generic;
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
    private const string ThrowingKind = "test/identity-throws";
    private static readonly Rect Viewport = new(0f, 0f, 300f, 200f);

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        ResetProbe();
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new IdentityProbeWidget(),
            new[] { "Id", "Kind", "Text", "Dragging", "Cursor", "Hidden", "Tab" });
        UiWidgetRegistry.Register(Scope, ThrowingKind, () => new ThrowingProbeWidget(),
            new[] { "Id", "Kind", "Text" });

        try
        {
            Run("two unnamed same-kind siblings never share an arranged key", VerifyArrangedKeysAreUnique);
            Run("unnamed siblings get distinct identities, stable between Measure and Draw", VerifyIdentitiesAreDistinctAndStable);
            Run("identity survives a re-arrange and a Hidden sibling", VerifyIdentitySurvivesHideAndReArrange);
            Run("a Tab switch does not renumber the visible sibling", VerifyTabSwitchDoesNotRenumber);
            Run("per-element state does not cross between unnamed siblings", VerifyStateIsIsolatedBetweenSiblings);
            Run("a throwing sibling does not move the next element's state slot", VerifyThrowerDoesNotLeakTheScope);
            Run("hover claims carry the element that made them", VerifyHoverClaimCarriesItsElement);
        }
        finally
        {
            ResetProbe();
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
        }

        return failures;
    }

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

        if (measuredA.Key.Length == 0 || !string.Equals(measuredA.Key, PathOf("A", "draw"), StringComparison.Ordinal))
        {
            throw new Exception("the element's path is not its identity: key='" + measuredA.Key
                + "', ctx.ElementPath='" + PathOf("A", "draw") + "'");
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
        if (!string.Equals(b.Key, "row/" + ProbeKind + "[1]", StringComparison.Ordinal))
        {
            throw new Exception("the Tab-gated second sibling did not keep its declared ordinal: " + b.Key);
        }

        tab = "TabA";
        ResetProbe();
        host.Session.BumpContentRevision();
        host.DrawFrame(Viewport);

        UiNodeId a = IdentityOf("A", "draw");
        if (!string.Equals(a.Key, "row/" + ProbeKind + "[0]", StringComparison.Ordinal))
        {
            throw new Exception("a Tab switch renumbered the visible sibling: " + a.Key);
        }

        if (!host.Session.GetValueStates(b).ContainsKey("probe"))
        {
            throw new Exception("the Tab-hidden sibling lost its state slot");
        }
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

        internal Observation(string tag, string phase, UiNodeId id, string path)
        {
            Tag = tag;
            Phase = phase;
            Id = id;
            Path = path;
        }
    }

    /// <summary>
    /// Records the identity it was handed in Measure and in Draw, writes its own state through the
    /// bare-string overload, and checks in-pass that the overload resolved into the element it is.
    /// </summary>
    private sealed class IdentityProbeWidget : IUiWidget
    {
        internal const string HelpKey = "identity-test/help";

        internal static readonly List<Observation> Seen = new();
        internal static readonly List<string> MissedOwnSlot = new();
        internal static string ClaimingTag = "";

        private string tag = "";
        private bool dragging;
        private int cursor;

        public string Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
            tag = spec.TryGetAttribute("Text", out string text) ? text : "";
            dragging = spec.TryGetAttribute("Dragging", out string drag)
                && string.Equals(drag.Trim(), "true", StringComparison.OrdinalIgnoreCase);
            cursor = spec.TryGetAttribute("Cursor", out string raw) && int.TryParse(raw.Trim(), out int parsed)
                ? parsed
                : 0;
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            Seen.Add(new Observation(tag, "measure", ctx.ElementId, ctx.ElementPath));
            return 14f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            Seen.Add(new Observation(tag, "draw", ctx.ElementId, ctx.ElementPath));

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
