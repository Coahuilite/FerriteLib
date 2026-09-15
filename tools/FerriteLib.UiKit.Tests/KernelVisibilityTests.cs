using System;
using System.Collections.Generic;
using System.Linq;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Conditional-visibility and structural-update lane. It holds the claims task-2 owes:
/// <list type="bullet">
/// <item><c>Visible="true|false"</c> is static and engine-wide; <c>Hidden</c> keeps its old meaning; a
/// malformed <c>Visible</c> is refused at creation rather than interpreted per frame.</item>
/// <item><c>VisibleKey</c> is dynamic: the element follows a bool value binding, and announcing that key
/// hides or shows it without reopening the page.</item>
/// <item>An unresolvable <c>VisibleKey</c> is not fatal - the element stays visible and one
/// <b>deduplicated</b> report reaches the fail-soft appearance channel, so fail-soft is not silent and a
/// 60 fps page does not record sixty findings.</item>
/// <item>Hiding does not renumber identity: an unnamed sibling keeps the declared ordinal it was
/// arranged under, because <c>UiNodeId</c> keys on the declared index on purpose. This is the assertion a
/// mutation of <c>VisibleChildren</c>'s declared index reddens.</item>
/// <item>A declared but hidden element keeps its node and its state; an identity the definition no longer
/// contains is released - node, state, sub-nodes, scroll position, recovery record - at the end of the
/// arrange that rebuilt the tree. That second half is the prune P4's reload needed and could not reach
/// (<c>UiSession</c> owns node lifetime); the first half is the anti-over-prune control that keeps the
/// prune from becoming "drop everything that was not arranged this pass".</item>
/// </list>
/// </summary>
internal static class KernelVisibilityTests
{
    private const string Scope = "visibility-lane";
    private const string ProbeKind = "test/visibility-probe";

    private static readonly Dictionary<string, string> MeasuredPaths = new(StringComparer.Ordinal);
    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Visible is static and its grammar is a creation-time contract", VerifyStaticVisibilityAndGrammar);
        Run("VisibleKey follows its bool binding without reopening the page", VerifyVisibleKeyToggles);
        Run("An unresolvable VisibleKey stays visible and reports once", VerifyUnresolvableKeyIsSoftAndDeduplicated);
        Run("Hiding a sibling does not renumber identity", VerifyHiddenSiblingKeepsDeclaredOrdinal);
        Run("Hiding keeps the hidden element's state and spares its siblings", VerifyHideKeepsState);
        Run("A removed identity releases its node; a hidden one keeps it", VerifyRemovedIdentityReleasesItsNode);
        return failures;
    }

    private static void VerifyStaticVisibilityAndGrammar()
    {
        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\" Gap=\"2\">"
            + "<Widget Id=\"gone\" Kind=\"" + ProbeKind + "\" Tag=\"gone\" Height=\"12\" Visible=\"false\" />"
            + "<Widget Id=\"here\" Kind=\"" + ProbeKind + "\" Tag=\"here\" Height=\"12\" Visible=\"true\" />"
            + "<Widget Id=\"legacy\" Kind=\"" + ProbeKind + "\" Tag=\"legacy\" Height=\"12\" Hidden=\"true\" />"
            + "<Row Id=\"hidden-region\" Visible=\"false\"><Widget Id=\"inside\" Kind=\"" + ProbeKind + "\" Tag=\"inside\" Height=\"12\" /></Row>"
            + "</Column>"
            + "</UiPage>",
            new UiBindings());

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(!snapshot.RectById.ContainsKey("gone"), "Visible=false hides the widget it is declared on");
        Check(snapshot.RectById.ContainsKey("here"), "Visible=true shows it");
        Check(!snapshot.RectById.ContainsKey("legacy"), "and the legacy static Hidden still hides");
        Check(
            !snapshot.RectById.ContainsKey("hidden-region") && !snapshot.RectById.ContainsKey("inside"),
            "Visible is engine-wide: a hidden container takes its subtree with it");

        Reject(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"keeper\" Kind=\"" + ProbeKind + "\" Height=\"12\" Visible=\"maybe\" />"
            + "</UiPage>",
            "a malformed Visible is refused at creation");
    }

    private static void VerifyVisibleKeyToggles()
    {
        bool show = false;
        var bindings = new UiBindings();
        bindings.BindReadOnly("show-keeper", () => show, UiInvalidation.Structure);

        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\" Gap=\"2\">"
            + "<Widget Id=\"keeper\" Kind=\"" + ProbeKind + "\" Tag=\"keeper\" Height=\"12\" VisibleKey=\"show-keeper\" />"
            + "<Widget Id=\"other\" Kind=\"" + ProbeKind + "\" Tag=\"other\" Height=\"12\" />"
            + "</Column>"
            + "</UiPage>",
            bindings);

        UiLayoutSnapshot hidden = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(!hidden.RectById.ContainsKey("keeper"), "a false VisibleKey hides the element on the first arrange");
        Check(hidden.RectById.ContainsKey("other"), "and leaves its sibling alone");

        show = true;
        bindings.NotifyChanged("show-keeper");
        UiLayoutSnapshot shown = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            shown.RectById.ContainsKey("keeper"),
            "and announcing the key shows the element again without reopening the page");
        Check(shown.RectById.ContainsKey("other"), "while the sibling is still there");

        show = false;
        bindings.NotifyChanged("show-keeper");
        UiLayoutSnapshot hiddenAgain = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(!hiddenAgain.RectById.ContainsKey("keeper"), "and it hides again the same way");
    }

    private static void VerifyUnresolvableKeyIsSoftAndDeduplicated()
    {
        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\">"
            + "<Widget Id=\"keeper\" Kind=\"" + ProbeKind + "\" Tag=\"keeper\" Height=\"12\" VisibleKey=\"never-bound\" />"
            + "</Column>"
            + "</UiPage>",
            new UiBindings());

        UiFitAudit.Reset();
        UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(200f, 200f));
        int reported = UiFitAudit.StyleFallbackCount;

        Check(
            first.RectById.ContainsKey("keeper"),
            "an unresolvable VisibleKey leaves the element visible: a page must not fail to exist because a model key is missing");
        Check(reported == 1, "and it is reported once through the fail-soft appearance channel");
        Check(
            UiFitAudit.LastStyleFallbackDiagnostic != null
                && UiFitAudit.LastStyleFallbackDiagnostic!.IndexOf("never-bound", StringComparison.Ordinal) >= 0,
            "with the key named, so the author can see which declaration did nothing");

        // A different size forces a real re-arrange, so the visibility query runs again: the finding must
        // not be recorded a second time.
        host.MeasureAndArrange(new Vector2(201f, 200f));
        Check(
            UiFitAudit.StyleFallbackCount == reported,
            "and a page that re-arranges every frame records it once, not once per frame");
    }

    private static void VerifyHiddenSiblingKeepsDeclaredOrdinal()
    {
        bool showMiddle = true;
        var bindings = new UiBindings();
        bindings.BindReadOnly("show-middle", () => showMiddle, UiInvalidation.Structure);

        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            // No Height on these three on purpose: the declared band would let the engine answer the
            // measure without calling the widget, and this lane's whole observation is the identity the
            // widget is handed.
            + "<Row Id=\"row\" Gap=\"2\">"
            + "<Widget Kind=\"" + ProbeKind + "\" Tag=\"a\" />"
            + "<Widget Kind=\"" + ProbeKind + "\" Tag=\"b\" VisibleKey=\"show-middle\" />"
            + "<Widget Kind=\"" + ProbeKind + "\" Tag=\"c\" />"
            + "</Row>"
            + "</UiPage>",
            bindings);

        UiLayoutSnapshot all = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            PathOf("a") == "row/" + ProbeKind + "[0]" && PathOf("c") == "row/" + ProbeKind + "[2]",
            "unnamed siblings are identified by their declared ordinal (the lane's own control: identity is observable at all)");

        showMiddle = false;
        bindings.NotifyChanged("show-middle");
        UiLayoutSnapshot hidden = host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(!hidden.VisibleIds.Contains("row/" + ProbeKind + "[1]"), "the middle sibling is hidden");
        Check(
            PathOf("c") == "row/" + ProbeKind + "[2]",
            "and the sibling after it keeps the declared ordinal it had - a hidden element does not renumber identity");
        Check(PathOf("a") == "row/" + ProbeKind + "[0]", "nor does the one before it move");

        showMiddle = true;
        bindings.NotifyChanged("show-middle");
        host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            PathOf("b") == "row/" + ProbeKind + "[1]",
            "showing it again reuses the same identity, so its state was never handed to another element");
    }

    private static void VerifyHideKeepsState()
    {
        bool show = true;
        var bindings = new UiBindings();
        bindings.BindReadOnly("show-keeper", () => show, UiInvalidation.Structure);

        using UiHost host = Host(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Column Id=\"column\" Gap=\"2\">"
            + "<Widget Id=\"keeper\" Kind=\"" + ProbeKind + "\" Tag=\"keeper\" Height=\"12\" VisibleKey=\"show-keeper\" />"
            + "<Widget Id=\"other\" Kind=\"" + ProbeKind + "\" Tag=\"other\" Height=\"12\" />"
            + "</Column>"
            + "</UiPage>",
            bindings);

        host.MeasureAndArrange(new Vector2(200f, 200f));
        UiNode keeper = host.Session.GetNodeByElementId("keeper")
            ?? throw new Exception("the keeper was not arranged");
        keeper.GetOrCreateState("draft").EditText = "typed";
        UiNode other = host.Session.GetNodeByElementId("other")
            ?? throw new Exception("the sibling was not arranged");
        other.GetOrCreateState("draft").EditText = "untouched";

        show = false;
        bindings.NotifyChanged("show-keeper");
        host.MeasureAndArrange(new Vector2(200f, 200f));

        UiNode? keeperAfter = host.Session.GetNodeByElementId("keeper");
        Check(
            keeperAfter != null && ReferenceEquals(keeperAfter, keeper),
            "a hidden element keeps its node - it is not arranged, but it is still declared");
        Check(
            keeperAfter != null,
            "GetNodeByElementId answers by identity rather than by arrangement, which is what keeps a hidden element's draft reachable");
        Check(
            keeperAfter!.ValueStates["draft"].EditText == "typed",
            "and its draft survives the hide");
        Check(
            ReferenceEquals(host.Session.GetNodeByElementId("other"), other)
                && other.ValueStates["draft"].EditText == "untouched",
            "while an unrelated element's identity and state are untouched");

        show = true;
        bindings.NotifyChanged("show-keeper");
        host.MeasureAndArrange(new Vector2(200f, 200f));
        Check(
            ReferenceEquals(host.Session.GetNodeByElementId("keeper"), keeper)
                && keeper.ValueStates["draft"].EditText == "typed",
            "showing it again resumes on the same node with the same state");
    }

    private static void VerifyRemovedIdentityReleasesItsNode()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Id", "Kind", "Tag", "Height" });

        bool showC = true;
        var bindings = new UiBindings();
        bindings.BindReadOnly("show-c", () => showC, UiInvalidation.Structure);

        using var session = new UiSession();
        UiTheme theme = UiTheme.DarkGold;
        UiWidgetContext ctx = new(
            Scope, session, new StubMetrics(), theme, new StubTranslation(), bindings, 200f, "root");

        // Two definitions over one session, the way a document reload swaps the tree: the first carries a,
        // b and c; the second drops b entirely.
        string three = Page("a", "b", "c");
        string two = Page("a", "c");

        new UiLayoutEngine(Scope).ArrangeRoots(ctx, new Vector2(200f, 200f), UiLayoutManifest.Parse(three).Roots);

        UiNode a = session.GetNodeByElementId("a") ?? throw new Exception("a was not arranged");
        UiNode b = session.GetNodeByElementId("b") ?? throw new Exception("b was not arranged");
        UiNode c = session.GetNodeByElementId("c") ?? throw new Exception("c was not arranged");
        a.GetOrCreateState("draft").EditText = "keep";
        b.GetOrCreateState("draft").EditText = "gone";
        c.GetOrCreateState("draft").EditText = "hidden-but-here";
        UiNode body = session.GetNodeByElementId("body") ?? throw new Exception("the scroll container was not arranged");
        session.SetScrollPosition(body, new Vector2(0f, 5f));

        // First: hide c through its key. A declared identity keeps everything.
        showC = false;
        bindings.NotifyChanged("show-c");
        new UiLayoutEngine(Scope).ArrangeRoots(ctx, new Vector2(200f, 200f), UiLayoutManifest.Parse(three).Roots);

        Check(
            ReferenceEquals(session.GetNodeByElementId("c"), c) && c.ValueStates["draft"].EditText == "hidden-but-here",
            "a declared but hidden identity keeps its node and its state - the prune is about removal, not about being arranged");

        // Then: remove b. That identity, and only that identity, is released.
        new UiLayoutEngine(Scope).ArrangeRoots(ctx, new Vector2(200f, 200f), UiLayoutManifest.Parse(two).Roots);

        Check(
            session.GetNodeByElementId("b") == null,
            "a removed identity answers null from the Id bridge too: the node is released by identity, not merely un-arranged");
        Check(session.GetNode(b.Id) == null, "and the node is unreachable by identity too, so nothing holds a stale Kind");
        Check(
            ReferenceEquals(session.GetNodeByElementId("a"), a) && a.ValueStates["draft"].EditText == "keep",
            "while a surviving identity keeps its node and its state");
        Check(
            ReferenceEquals(session.GetNodeByElementId("c"), c) && c.ValueStates["draft"].EditText == "hidden-but-here",
            "including a hidden one the new definition still declares");
        Vector2 scroll = session.GetScrollPosition(body);
        Check(Near(scroll.x, 0f) && Near(scroll.y, 5f), "and unrelated session state - a scroll position - survives the prune");

        Check(
            session.GetValueStates(b.Id).Count == 0,
            "and the released node's state slots went with it rather than staying reachable");
    }

    // --- helpers -------------------------------------------------------------------------------

    /// <summary>
    /// Three columns of one page: <c>a</c>, an optional middle element (the one a test removes or hides),
    /// and <c>c</c>, inside a scroll container so unrelated session state has somewhere to live.
    /// </summary>
    private static string Page(params string[] ids)
    {
        string xml = "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Scroll Id=\"body\" Height=\"40\">";
        foreach (string id in ids)
        {
            xml += Element(id);
        }

        return xml + "</Scroll></UiPage>";
    }

    private static string Element(string id)
    {
        string visible = id == "c" ? " VisibleKey=\"show-c\"" : "";
        // Tall enough that even one surviving element overflows the 40-high viewport, so the scroll
        // position under test is never clamped away for a reason the lane did not mean to test.
        return "<Widget Id=\"" + id + "\" Kind=\"" + ProbeKind + "\" Tag=\"" + id + "\" Height=\"60\""
            + visible + " />";
    }

    private static UiHost Host(string xml, IUiBindings bindings)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, ProbeKind, () => new ProbeWidget(), new[] { "Id", "Kind", "Tag", "Height" });
        MeasuredPaths.Clear();
        return new UiHost(
            Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
    }

    private static void Reject(string xml, string what)
    {
        try
        {
            using UiHost host = Host(xml, new UiBindings());
            Check(false, what + " - but the host accepted it");
        }
        catch (UiContractException)
        {
            Check(true, what + " - rejected at creation");
        }
    }

    private static string PathOf(string tag)
    {
        return MeasuredPaths.TryGetValue(tag, out string? path) ? path : "(not measured)";
    }

    private static bool Near(float expected, float actual)
    {
        return Math.Abs(expected - actual) <= 0.01f;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// One probe: measures a fixed band and records the identity it was handed, which is the only way a
    /// lane can observe "identity did not renumber" rather than read the engine's intent.
    /// </summary>
    private sealed class ProbeWidget : IUiWidget
    {
        private UiElementSpec spec = UiElementSpec.Empty;

        string IUiWidget.Kind => ProbeKind;

        public void Configure(UiElementSpec spec)
        {
            this.spec = spec;
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            string tag = spec.TryGetAttribute("Tag", out string value) ? value : spec.Id;
            MeasuredPaths[tag] = ctx.Node != null ? ctx.Node.Path : ctx.ElementPath;
            return 12f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }

        public float MeasureWidth(string text, UiFont font)
        {
            return string.IsNullOrEmpty(text) ? 0f : StubTextWidth.Of(text, font);
        }
    }

    private sealed class StubTranslation : IUiTranslation
    {
        public string Translate(string key)
        {
            return "[" + key + "]";
        }

        public int TranslationRevision => 0;
    }
}
