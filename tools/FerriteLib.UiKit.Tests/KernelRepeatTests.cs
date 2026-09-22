using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// The keyed repeater's lane: the collection element, its per-item binding scope, node/state reuse by the
/// consumer's business key, and the two removal answers the round's acceptance names - a removed item's
/// state is released, and insertion/removal cycles accumulate nothing.
/// <para>
/// The claims, in the order they are checked:
/// <list type="bullet">
/// <item>one arranged element per accepted item key, published under <c>&lt;templateId&gt;#&lt;itemKey&gt;</c>;</item>
/// <item>a row's controls resolve <c>done</c> against <c>&lt;items&gt;.&lt;key&gt;.done</c>, never against a
/// page-level key of the same name - the "row reads its own item, not a global selection" claim;</item>
/// <item>the same for <c>SelectedKey</c>: "which row is selected" is one row's answer, so exactly one row
/// carries the active treatment even while the page-level binding of the same name answers <c>true</c>;</item>
/// <item>a reorder moves the geometry and leaves each key's node and state where they were;</item>
/// <item>an insert touches only the inserted key;</item>
/// <item>four insert/remove cycles leave the session node table, the engine's widget table and the
/// materialization table at their baseline sizes;</item>
/// <item>a removed key's element node, state slots and widget sub-node are <b>gone</b> from the session
/// (P2b made that observable), not merely zeroed;</item>
/// <item>a duplicate, blank or reserved-character key is refused with a report, never reconciled onto
/// another row's identity;</item>
/// <item>an unknown kind or attribute inside a template is a creation-time refusal naming the template and
/// the path, and the boundary the engine's validator shares with the host is pinned against drift.</item>
/// </list>
/// </para>
/// </summary>
internal static class KernelRepeatTests
{
    private const string Scope = "repeat-lane";
    private const string ChildProbeKind = "test/child-probe";

    private static int failures;
    private static readonly List<string> Keys = new();
    private static readonly Dictionary<string, bool> Done = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, float> Pct = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Caption = new(StringComparer.Ordinal);
    private static readonly HashSet<string> BoundKeys = new(StringComparer.Ordinal);
    private static int pageLevelDone;
    private static bool showRows = true;

    public static int RunAll()
    {
        failures = 0;
        RegisterProbeKind();
        Run("A row set renders one element per item key", VerifyRowSetRendersPerKey);
        Run("A row reads its own item scope, not the page's key of the same name", VerifyItemLocalScope);
        Run("SelectedKey answers per row, not for the page the row set sits on", VerifySelectedKeyIsPerRow);
        Run("Reordering moves the geometry and keeps each key's node and state", VerifyReorderKeepsState);
        Run("Inserting a key does not renumber the rows that already exist", VerifyInsertDoesNotRenumber);
        Run("Insert/remove cycles accumulate no nodes, widget instances or materialization", VerifyCyclesDoNotAccumulate);
        Run("Removal releases the item's node, its state and its widget's sub-node", VerifyRemovalReleasesEverything);
        Run("A hidden collection keeps its rows' nodes and state", VerifyHiddenCollectionKeepsState);
        Run("A duplicate item key is refused, not reconciled", VerifyDuplicateKeyIsRefused);
        Run("A blank or reserved-character item key is refused", VerifyInvalidKeysAreRefused);
        Run("The template's kinds and attributes are refused at creation, with the template named", VerifyTemplateContractIsFailClosed);
        Run("The parser refuses a dangling template, a duplicate name, children, a nested repeat and a reserved Id", VerifyManifestStructureIsRefused);
        Run("The engine's template vocabulary cannot drift from the host's", VerifyTemplateVocabularyCannotDrift);
        Run("The Repeat declaration itself is contract-checked at creation", VerifyRepeatDeclarationContract);
        Run("An item-local key nobody bound draws the kind's default and reports once", VerifyAbsentItemLocalKeysAreFailSoft);
        Run("A mistyped item-local value draws the kind's default and reports once", VerifyMistypedItemLocalValuesAreFailSoft);
        Run("An item-local value that arrives a frame later is read without a recovery trip", VerifyLateItemLocalValueIsReadLater);
        Run("An existing atom keeps its own throwing-read contract", VerifyAtomsKeepTheirOwnContract);
        Run("An absent item-local key on an atom draws its default and reports once", VerifyAbsentAtomKeyIsLoud);
        return failures;
    }

    // --- lanes ---------------------------------------------------------------------------------

    private static void VerifyRowSetRendersPerKey()
    {
        using UiHost host = NewHost(new[] { "a", "b", "c" });
        Draw(host);

        for (int i = 0; i < Keys.Count; i++)
        {
            Check(Node(host, "row#" + Keys[i]) != null, "row '" + Keys[i] + "' has an element of its own");
        }

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(240f, 400f));
        Check(snapshot.RectById.ContainsKey("row#a") && snapshot.RectById.ContainsKey("row#c"),
            "every row's geometry is published under its item-qualified identity");
        Check(snapshot.RectById["row#a"].y < snapshot.RectById["row#c"].y, "rows follow the binding's order");
        Check(!snapshot.RectById.ContainsKey("row"),
            "the declared template Id alone addresses no row: a repeated element's instance key carries the item");
    }

    private static void VerifyItemLocalScope()
    {
        pageLevelDone = 0;
        using UiHost host = NewHost(new[] { "a", "b" });
        Draw(host);

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(240f, 400f));
        Rect flag = snapshot.RectById["flag#b"];
        var centre = new Vector2(flag.x + flag.width * 0.5f, flag.y + flag.height * 0.5f);
        Pump(host, EventType.MouseDown, centre);
        Pump(host, EventType.MouseUp, centre);

        Check(Done["b"], "the clicked row wrote its own item-scoped key");
        Check(!Done["a"], "and no other row's value moved");
        Check(pageLevelDone == 0,
            "the page-level key the template names ('done') was neither read nor written by the row");
    }

    /// <summary>
    /// B1 (2026-09-22): the item-scope table carries <c>SelectedKey</c> too, because "which row is
    /// selected" is one row's answer and not the page's - the same criterion that deliberately keeps
    /// <c>Tab</c> out of that table. The observable is the number of rows painted with the active
    /// treatment: the template declares <c>SelectedKey="selected"</c> once, every row reads it, and the
    /// page-level binding of that very name is a decoy answering <c>true</c>. A row that resolved against
    /// the page instead of its own item scope lights every row - which is the defect the consumer hit,
    /// where a data-driven row set could not say which of its rows was the selected one.
    /// </summary>
    private static void VerifySelectedKeyIsPerRow()
    {
        UiFitAudit.Reset();
        UiTheme theme = UiTheme.DarkGold;
        var activeEdge = new Color(0.91f, 0.11f, 0.17f, 1f);
        // The Active cell's border is theme.AccentGold and a rule paints exactly that border, so the probe
        // colour counts whole rows without depending on the order the rows were drawn in.
        theme.AccentGold = activeEdge;

        var keys = new List<string> { "a", "b", "c" };
        var selected = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            { "a", false }, { "b", true }, { "c", false },
        };
        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<string>>("items", () => keys, UiInvalidation.Structure);
        // The decoy: the page-level key the template's SelectedKey names. It answers true, so every row
        // that reads the page scope instead of its own item scope goes active.
        bindings.BindReadOnly<bool>("selected", () => true);
        for (int i = 0; i < keys.Count; i++)
        {
            string captured = keys[i];
            bindings.BindReadOnly<bool>("items." + captured + ".selected", () => selected[captured]);
        }

        using var host = new UiHost(
            Scope, UiLayoutManifest.Parse(SelectedKeyPageXml), bindings, theme, new StubMetrics(), new StubTranslation());

        UiLayoutSnapshot snapshot = Arrange(host);
        List<Rect> active = ActiveRuleRects(activeEdge);
        Check(active.Count == 1,
            "exactly one row carries the active treatment, not one per row: " + active.Count + " active row(s) of "
            + keys.Count + " (a row reading the page-level decoy 'selected' would make it " + keys.Count + ")");
        Check(active.Count == 1 && Within(active[0], snapshot.RectById["row#b"]),
            "and it is the row whose own item key answers true ('b')");

        // The same claim from the other side: the truth moves to another item key and the one active row
        // moves with it, so the answer is per row and not a fixed slot.
        selected["b"] = false;
        selected["c"] = true;
        host.Bindings.NotifyChanged("items");
        snapshot = Arrange(host);
        active = ActiveRuleRects(activeEdge);
        Check(active.Count == 1 && Within(active[0], snapshot.RectById["row#c"]),
            "moving which item key answers true moves the one active row with it");

        // A future-regression guard rather than the mutation-proving half: with every item key bound,
        // nothing takes the fail-soft path - the silence a correctly scoped page has to keep.
        Check(UiFitAudit.StyleFallbackCount == 0,
            "no row needed the fail-soft path: every SelectedKey resolved in its own item scope");
    }

    /// <summary>
    /// Draws one frame and returns the arrangement. The recording hooks are cleared first: they are
    /// process-wide accumulators, and a count taken over a list another lane (or the previous frame)
    /// already wrote to is not a measurement of this frame.
    /// </summary>
    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        ClearDrawRecordings();
        Draw(host);
        return host.MeasureAndArrange(new Vector2(240f, 400f));
    }

    private static List<Rect> ActiveRuleRects(Color activeEdge)
    {
        var colors = (IList)DrawRecording("DrawBoxSolidColors");
        var rects = (IList)DrawRecording("DrawBoxSolidRects");
        var found = new List<Rect>();
        for (int i = 0; i < colors.Count && i < rects.Count; i++)
        {
            if (SameColor((Color)colors[i]!, activeEdge)) found.Add((Rect)rects[i]!);
        }

        return found;
    }

    private static void ClearDrawRecordings()
    {
        ((IList)DrawRecording("DrawBoxSolidColors")).Clear();
        ((IList)DrawRecording("DrawBoxSolidRects")).Clear();
    }

    /// <summary>
    /// The harness's visual recording lives on the Verse stub and is reached by reflection, because the
    /// test assembly compiles against the game's reference assembly and only executes against the stub.
    /// </summary>
    private static object DrawRecording(string name)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("the Verse stub carries no recording hook named " + name);
        return field.GetValue(null)!;
    }

    private static bool Within(Rect inner, Rect outer)
    {
        return inner.y >= outer.y - 0.001f && inner.yMax <= outer.yMax + 0.001f;
    }

    private static bool SameColor(Color left, Color right)
    {
        return Math.Abs(left.r - right.r) < 0.001f && Math.Abs(left.g - right.g) < 0.001f
            && Math.Abs(left.b - right.b) < 0.001f && Math.Abs(left.a - right.a) < 0.001f;
    }


    private static void VerifyReorderKeepsState()
    {
        using UiHost host = NewHost(new[] { "a", "b" });
        Draw(host);

        UiNode rowA = Require(host, "row#a");
        UiNode rowB = Require(host, "row#b");
        rowA.GetOrCreateState("probe").FloatValue = 7f;

        UiLayoutSnapshot before = host.MeasureAndArrange(new Vector2(240f, 400f));
        bool aWasFirst = before.RectById["row#a"].y < before.RectById["row#b"].y;

        Keys.Clear();
        Keys.Add("b");
        Keys.Add("a");
        host.Bindings.NotifyChanged("items");
        Draw(host);

        UiNode afterA = Require(host, "row#a");
        UiNode afterB = Require(host, "row#b");
        Check(ReferenceEquals(rowA, afterA) && ReferenceEquals(rowB, afterB),
            "reordering reuses each key's node instead of rebuilding it");
        Check(Near(afterA.GetOrCreateState("probe").FloatValue, 7f),
            "the state written under key 'a' is still under key 'a' after the reorder");
        Check(Near(afterB.GetOrCreateState("probe").FloatValue, 0f), "and none of it bled onto key 'b'");

        UiLayoutSnapshot after = host.MeasureAndArrange(new Vector2(240f, 400f));
        bool aIsFirst = after.RectById["row#a"].y < after.RectById["row#b"].y;
        Check(aWasFirst != aIsFirst, "while the drawn order followed the binding to its new order");
    }

    private static void VerifyInsertDoesNotRenumber()
    {
        using UiHost host = NewHost(new[] { "a", "c" });
        Draw(host);

        UiNode rowA = Require(host, "row#a");
        UiNode rowC = Require(host, "row#c");
        rowC.GetOrCreateState("probe").FloatValue = 5f;

        Keys.Insert(1, "b");
        BindItemKeys(host.Bindings);
        host.Bindings.NotifyChanged("items");
        Draw(host);

        Check(ReferenceEquals(rowA, Require(host, "row#a")) && ReferenceEquals(rowC, Require(host, "row#c")),
            "inserting a key kept both existing rows' nodes");
        Check(Near(Require(host, "row#c").GetOrCreateState("probe").FloatValue, 5f),
            "and their state - the inserted key did not take the third slot's identity");
        Check(Node(host, "row#b") != null, "the inserted key got a row of its own");

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(240f, 400f));
        Check(snapshot.RectById["row#a"].y < snapshot.RectById["row#b"].y
            && snapshot.RectById["row#b"].y < snapshot.RectById["row#c"].y,
            "and the rows sit in the new binding order");
    }

    private static void VerifyCyclesDoNotAccumulate()
    {
        using UiHost host = NewHost(new[] { "a", "b" });
        Draw(host);

        int baselineNodes = CountSessionNodes(host.Session);
        int baselineWidgets = CountEngineWidgets(host);
        int baselineDerived = CountRepeatDerived(host);
        Check(baselineDerived == Keys.Count, "the materialization starts at one derived subtree per key");

        for (int cycle = 0; cycle < 4; cycle++)
        {
            string key = "temp" + cycle.ToString();
            Keys.Add(key);
            BindItemKeys(host.Bindings);
            host.Bindings.NotifyChanged("items");
            Draw(host);
            Check(Node(host, "row#" + key) != null, "cycle " + cycle + ": the inserted key renders");

            Keys.Remove(key);
            host.Bindings.NotifyChanged("items");
            Draw(host);
            Check(host.Session.GetNodeByElementId("row#" + key) == null,
                "cycle " + cycle + ": the removed key's node is gone from the session, not merely zeroed");
        }

        Check(CountSessionNodes(host.Session) == baselineNodes,
            "four insert/remove cycles leave the session's node table at its baseline size");
        Check(CountEngineWidgets(host) == baselineWidgets,
            "and the engine's widget instances too: a removed row's instances are released, not pooled");
        Check(CountRepeatDerived(host) == Keys.Count,
            "and the per-key materialization holds exactly the keys the binding supplies now");
    }

    private static void VerifyRemovalReleasesEverything()
    {
        using UiHost host = NewHost(new[] { "a", "b" });
        Draw(host);

        UiNode rowA = Require(host, "row#a");
        rowA.GetOrCreateState("kept").FloatValue = 9f;
        UiNode probe = ChildNamed(Require(host, "probe#a"), "probe")
            ?? throw new Exception("the row's widget minted no sub-node");
        probe.GetOrCreateState("probe").FloatValue = 3f;
        UiNodeId probeId = probe.Id;
        UiNodeId rowAId = rowA.Id;

        Keys.Remove("a");
        host.Bindings.NotifyChanged("items");
        Draw(host);

        Check(host.Session.GetNodeByElementId("row#a") == null,
            "the removed item's element node is gone from the session");
        Check(host.Session.GetNode(probeId) == null, "and the sub-node its widget minted went with it");
        Check(host.Session.GetValueStates(rowAId).Count == 0,
            "and the state slots that node owned are released with it");
        Check(Require(host, "row#b") != null, "while the surviving row is untouched");
    }

    private static void VerifyHiddenCollectionKeepsState()
    {
        using UiHost host = NewHost(new[] { "a", "b" });
        Draw(host);

        UiNode rowA = Require(host, "row#a");
        rowA.GetOrCreateState("probe").FloatValue = 4f;

        showRows = false;
        host.Bindings.NotifyChanged("show-rows");
        host.DrawFrame(new Rect(0f, 0f, 240f, 400f));
        UiLayoutSnapshot hidden = host.MeasureAndArrange(new Vector2(240f, 400f));
        Check(!hidden.RectById.ContainsKey("row#a"), "a hidden collection arranges no row");
        Check(Node(host, "row#a") != null, "but its rows keep their identity: hiding is not removal");
        Check(Near(Require(host, "row#a").GetOrCreateState("probe").FloatValue, 4f),
            "and the state the hide was not supposed to touch is still there");

        showRows = true;
        host.Bindings.NotifyChanged("show-rows");
        Draw(host);
        Check(ReferenceEquals(rowA, Require(host, "row#a")),
            "showing it again reuses the very node it had before the hide");
        Check(Near(rowA.GetOrCreateState("probe").FloatValue, 4f), "with its state intact");
    }

    private static void VerifyDuplicateKeyIsRefused()
    {
        UiFitAudit.Reset();
        using UiHost host = NewHost(new[] { "a", "a", "b" });
        Draw(host);

        Check(Require(host, "rows").Children.Count == 2,
            "a duplicate key renders no second row: the row is refused rather than reconciled");
        Check(Node(host, "row#a") != null && Node(host, "row#b") != null,
            "while the rows that can be identified still render");
        Check(
            UiFitAudit.LastStyleFallbackDiagnostic != null
                && UiFitAudit.LastStyleFallbackDiagnostic.Contains("no row")
                && UiFitAudit.LastStyleFallbackDiagnostic.Contains("Items"),
            "and the refusal is reported through the bounded channel: " + UiFitAudit.LastStyleFallbackDiagnostic);
    }

    private static void VerifyInvalidKeysAreRefused()
    {
        UiFitAudit.Reset();
        using UiHost host = NewHost(new[] { "", "a", "b/c", "d#e" });
        Draw(host);

        Check(Require(host, "rows").Children.Count == 1,
            "only the one row whose key can carry identity is materialized");
        Check(Node(host, "row#a") != null, "and it is the well-formed key's row");
        Check(UiFitAudit.StyleFallbackCount >= 3,
            "each refused key is reported (blank, path separator and the reserved identity separator): "
            + UiFitAudit.StyleFallbackCount);
    }

    private static void VerifyTemplateContractIsFailClosed()
    {
        const string unknownKind =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates>"
            + "<Row Id=\"row\"><Widget Id=\"x\" Kind=\"test/nobody-registered\" /></Row>"
            + "</Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>";
        string? refusal = CreationFailure(unknownKind);
        Check(refusal != null && refusal.Contains("row") && refusal.Contains("nobody-registered"),
            "an unknown kind inside a template is refused at creation and names the template and the kind: " + refusal);

        const string unknownAttribute =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates>"
            + "<Row Id=\"row\"><Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"done\" Nope=\"1\" /></Row>"
            + "</Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>";
        refusal = CreationFailure(unknownAttribute);
        Check(refusal != null && refusal.Contains("Nope") && refusal.Contains("row/flag"),
            "an unknown attribute inside a template is refused at creation with the template path: " + refusal);

        const string onContainer =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates>"
            + "<Row Id=\"row\" Nonsense=\"1\"><Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"done\" /></Row>"
            + "</Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>";
        refusal = CreationFailure(onContainer);
        Check(refusal != null && refusal.Contains("Nonsense"),
            "and the container vocabulary is enforced inside a template too: " + refusal);
    }

    private static void VerifyManifestStructureIsRefused()
    {
        ExpectParseFailure("a Repeat naming a template the manifest does not declare",
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Repeat Id=\"rows\" Items=\"items\" Template=\"nope\" /></UiPage>");
        ExpectParseFailure("a Repeat with no Template attribute",
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Repeat Id=\"rows\" Items=\"items\" /></UiPage>");
        ExpectParseFailure("a Repeat carrying its own children",
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\" /></Templates>"
            + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\"><Widget Id=\"x\" Kind=\"chrome/rule\" /></Repeat></UiPage>");
        ExpectParseFailure("two templates under one Id",
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\" /><Row Id=\"row\" /></Templates>"
            + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>");
        ExpectParseFailure("a template element Id carrying the reserved item-key separator",
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row#x\" /></Templates><Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>");
        ExpectParseFailure("a nested Repeat inside a template",
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\"><Repeat Id=\"inner\" Items=\"items\" Template=\"row\" /></Row></Templates>"
            + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>");
        ExpectParseFailure("a second <Templates> section",
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\" /></Templates>"
            + "<Templates><Row Id=\"other\" /></Templates></UiPage>");
    }

    private static void VerifyTemplateVocabularyCannotDrift()
    {
        AssertSameVocabulary("ContainerAttributes", "TemplateContainerAttributes");
        AssertSameVocabulary("CommonWidgetAttributes", "EngineWideWidgetAttributes");
    }

    private static void VerifyRepeatDeclarationContract()
    {
        using UiHost host = NewHost(new[] { "a" });
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(240f, 400f));
        Check(snapshot.RectById.ContainsKey("row#a"), "the declared page arrangement succeeded");

        string unknownAttribute =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\" /></Templates>"
            + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Bogus=\"1\" /></UiPage>";
        Check(CreationFailure(unknownAttribute) is string first && first.Contains("Bogus"),
            "an attribute outside the Repeat schema is refused at creation: " + CreationFailure(unknownAttribute));

        string missingItems =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\" /></Templates>"
            + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" /></UiPage>";
        Check(CreationFailure(missingItems) == null, "and the same page with the binding present is accepted");

        string unboundItems =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\" /></Templates>"
            + "<Repeat Id=\"rows\" Items=\"nobody-bound-this\" Template=\"row\" /></UiPage>";
        Check(CreationFailure(unboundItems) is string second && second.Contains("nobody-bound-this"),
            "an Items key nothing bound is refused at creation, not discovered as an empty row set: "
            + CreationFailure(unboundItems));

        string mistypedItems =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\"><Templates><Row Id=\"row\" /></Templates>"
            + "<Repeat Id=\"rows\" Items=\"not-a-list\" Template=\"row\" /></UiPage>";
        Check(CreationFailure(mistypedItems) != null,
            "and an Items key bound to another type is refused at creation too");
    }

    // --- the item-local binding contract ----------------------------------------------------------

    /// <summary>
    /// The chosen contract, pinned from both sides: an item-local key that is absent or bound to another type
    /// draws the kind's declared default and records one deduplicated report, while an existing atom keeps its
    /// own documented behaviour (a throwing read reaches one recovery slot). Code, comments and
    /// <c>docs/development/0.5/20-api-and-xml.md</c> 7.4 say exactly this and nothing else.
    /// </summary>
    private static void VerifyAbsentItemLocalKeysAreFailSoft()
    {
        UiFitAudit.Reset();
        using UiHost host = NewHost(new[] { "a" }, ItemBindingMode.NewKindsAbsent);
        Draw(host);

        UiNode flag = Require(host, "flag#a");
        UiNode bar = Require(host, "bar#a");
        Check(!host.Session.IsTripped(flag) && !host.Session.IsTripped(bar),
            "a key nobody bound draws the kind's declared default instead of replacing the slot with a recovery band");
        Check(!host.Session.IsTripped(Require(host, "caption#a")),
            "while the atom whose key is bound keeps drawing normally in the same row");

        int reports = UiFitAudit.StyleFallbackCount;
        Check(reports == 2, "one report per unresolved element, and none for the bound one: " + reports);
        Draw(host);
        Draw(host);
        Check(UiFitAudit.StyleFallbackCount == reports, "and repeated frames add no more: the report is deduplicated");
    }

    private static void VerifyMistypedItemLocalValuesAreFailSoft()
    {
        UiFitAudit.Reset();
        using UiHost host = NewHost(new[] { "a" }, ItemBindingMode.NewKindsMistyped);
        Draw(host);

        UiNode flag = Require(host, "flag#a");
        UiNode bar = Require(host, "bar#a");
        Check(!host.Session.IsTripped(flag),
            "a value of another type is answered with the declared default, not the recovery band");
        Check(!host.Session.IsTripped(bar), "for the progress read too");
        Check(UiFitAudit.StyleFallbackCount >= 2,
            "and each unresolved read is recorded once through the bounded channel: " + UiFitAudit.StyleFallbackCount);
        Check(
            UiFitAudit.LastStyleFallbackDiagnostic is string diagnostic && diagnostic.Contains("items.a."),
            "the report names the key it could not resolve: " + UiFitAudit.LastStyleFallbackDiagnostic);
    }

    private static void VerifyLateItemLocalValueIsReadLater()
    {
        UiFitAudit.Reset();
        UiBindings bindings = MakeBindings(new[] { "a" }, ItemBindingMode.NewKindsAbsent);
        using var host = new UiHost(
            Scope, UiLayoutManifest.Parse(PageXml), bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

        Draw(host);
        Check(!host.Session.IsTripped(Require(host, "flag#a")),
            "the first frame draws the default without a recovery trip");

        BindItemValues(bindings, "a");
        Done["a"] = true;
        Draw(host);
        Check(!host.Session.IsTripped(Require(host, "flag#a")),
            "and the frame that brings the value stays out of recovery too");

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(240f, 400f));
        Rect flag = snapshot.RectById["flag#a"];
        var centre = new Vector2(flag.x + flag.width * 0.5f, flag.y + flag.height * 0.5f);
        Pump(host, EventType.MouseDown, centre);
        Pump(host, EventType.MouseUp, centre);
        Check(!Done["a"], "the value the page bound one frame later is read and written like any other");
    }

    private static void VerifyAbsentAtomKeyIsLoud()
    {
        UiFitAudit.Reset();
        using UiHost host = NewHost(new[] { "a" }, ItemBindingMode.CaptionAbsent);
        Draw(host);

        UiNode caption = Require(host, "caption#a");
        Check(!host.Session.IsTripped(caption),
            "an absent bound key is not an error for the atom either: it draws its documented default");
        Check(caption.Rect.HasValue && caption.Rect.Value.height > 0f,
            "and the band it reserves while empty still exists, so the empty leaf cannot collapse its siblings");

        int reports = UiFitAudit.StyleFallbackCount;
        Check(reports == 1, "while the emptiness is recorded once, like every other fail-soft answer: " + reports);
        Check(
            UiFitAudit.LastStyleFallbackDiagnostic is string diagnostic && diagnostic.Contains("items.a.caption"),
            "and the report names the key it could not resolve: " + UiFitAudit.LastStyleFallbackDiagnostic);
        Draw(host);
        Draw(host);
        Check(UiFitAudit.StyleFallbackCount == reports, "repeated frames add no more: the report is deduplicated");
    }

    private static void VerifyAtomsKeepTheirOwnContract()
    {
        UiFitAudit.Reset();
        using UiHost host = NewHost(new[] { "a" }, ItemBindingMode.CaptionMistyped);
        Draw(host);

        UiNode caption = Require(host, "caption#a");
        Check(host.Session.IsTripped(caption),
            "an existing atom's throwing read still reaches one recovery slot - its own contract, unchanged");
        Check(
            host.Session.TryGetTripLog(caption, out string log) && log.Length > 0,
            "with the slot's diagnostic recorded: " + log);
        Check(
            Require(host, "flag#a") != null && !host.Session.IsTripped(Require(host, "flag#a")),
            "and the page keeps drawing around the tripped slot: the row's other controls are intact");
        Draw(host);
        Check(host.Session.IsTripped(caption), "the trip stays once per slot rather than once per frame");
    }

    // --- page and model -------------------------------------------------------------------------

    private const string PageXml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Templates>"
        + "<Row Id=\"row\" Gap=\"2\" Padding=\"2\">"
        + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"done\" Label=\"Done\" />"
        + "<Widget Id=\"bar\" Kind=\"display/progress\" Bind=\"pct\" Height=\"8\" />"
        + "<Widget Id=\"caption\" Kind=\"text/wrapped\" Bind=\"caption\" />"
        + "<Widget Id=\"probe\" Kind=\"" + ChildProbeKind + "\" Height=\"4\" />"
        + "</Row>"
        + "</Templates>"
        + "<Column Id=\"page\" Gap=\"4\">"
        + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Gap=\"4\" VisibleKey=\"show-rows\" />"
        + "</Column>"
        + "</UiPage>";

    /// <summary>
    /// The B1 page: one rule per row, and the only key the row declares is <c>SelectedKey</c>. A rule
    /// carries no binding of its own, so the active border is the sole thing a row can be telling us.
    /// </summary>
    private const string SelectedKeyPageXml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Templates>"
        + "<Row Id=\"row\" Gap=\"2\" Padding=\"2\">"
        + "<Widget Id=\"pick\" Kind=\"chrome/rule\" Height=\"8\" SelectedKey=\"selected\" />"
        + "</Row>"
        + "</Templates>"
        + "<Column Id=\"page\" Gap=\"4\">"
        + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Gap=\"4\" />"
        + "</Column>"
        + "</UiPage>";

    private static UiHost NewHost(IReadOnlyList<string> keys, ItemBindingMode mode = ItemBindingMode.Correct)
    {
        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(PageXml),
            MakeBindings(keys, mode),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    /// <summary>
    /// The consumer's half of the page: the model's key projection plus one binding per item key. Nothing
    /// here knows an element's identity, a rectangle or an input rule - which is the round's success
    /// criterion stated as code rather than as prose.
    /// </summary>
    /// <summary>
    /// Which of a row's item-local bindings the page registers, so the contract lanes can isolate one state at
    /// a time: the new kinds' absent and mistyped cases, the value that arrives later, and the existing atom's
    /// own throwing-read case.
    /// </summary>
    private enum ItemBindingMode
    {
        /// <summary>Every key the template declares is bound with the type it declares.</summary>
        Correct,

        /// <summary>The new kinds' keys are not bound at all; the atom's key is.</summary>
        NewKindsAbsent,

        /// <summary>The new kinds' keys are bound to another type; the atom's key is correct.</summary>
        NewKindsMistyped,

        /// <summary>The existing atom's key is bound to another type, so its own read contract is observable.</summary>
        CaptionMistyped,

        /// <summary>The existing atom's key is not bound at all, so its documented default is observable.</summary>
        CaptionAbsent,
    }

    private static UiBindings MakeBindings(IReadOnlyList<string> keys, ItemBindingMode mode = ItemBindingMode.Correct)
    {
        Keys.Clear();
        Keys.AddRange(keys);
        Done.Clear();
        Pct.Clear();
        Caption.Clear();
        BoundKeys.Clear();
        pageLevelDone = 0;
        showRows = true;

        var bindings = new UiBindings();
        bindings.BindReadOnly<IReadOnlyList<string>>("items", () => Keys, UiInvalidation.Structure);
        // The decoy: the template's declared key at page scope. A row that resolved against the page
        // bindings instead of its own item scope would read and write this one.
        bindings.BindValue<bool>("done", () => pageLevelDone != 0, value => pageLevelDone = value ? 1 : 0);
        bindings.BindReadOnly<float>("pct", () => 0f, UiInvalidation.Paint);
        bindings.BindReadOnly<bool>("not-a-list", () => true, UiInvalidation.Paint);
        bindings.BindReadOnly<bool>("show-rows", () => showRows, UiInvalidation.Structure);
        BindItemKeys(bindings, mode);
        return bindings;
    }

    private static void BindItemKeys(IUiBindings bindings, ItemBindingMode mode = ItemBindingMode.Correct)
    {
        for (int i = 0; i < Keys.Count; i++)
        {
            string key = Keys[i];
            if (!BoundKeys.Add(key)) continue;

            Caption[key] = "Caption " + key;
            BindItemKey(bindings, key, mode);
        }
    }

    /// <summary>
    /// One item key's bindings. The atom's key is bound in every mode but <see cref="ItemBindingMode.CaptionAbsent"/>,
    /// because a tripped sibling would drown the signal the contract lanes are looking for.
    /// </summary>
    private static void BindItemKey(IUiBindings bindings, string key, ItemBindingMode mode)
    {
        string captured = key;
        Done[captured] = false;
        Pct[captured] = 0f;

        if (mode == ItemBindingMode.CaptionMistyped)
        {
            bindings.BindReadOnly<bool>("items." + captured + ".caption", () => true);
        }
        else if (mode != ItemBindingMode.CaptionAbsent)
        {
            bindings.BindReadOnly<string>("items." + captured + ".caption", () => Caption[captured]);
        }

        switch (mode)
        {
            case ItemBindingMode.Correct:
            case ItemBindingMode.CaptionMistyped:
            case ItemBindingMode.CaptionAbsent:
                BindItemValues(bindings, captured);
                break;
            case ItemBindingMode.NewKindsMistyped:
                // Deliberately the wrong types: a bool key bound to a string, a float key bound to a bool.
                bindings.BindReadOnly<string>("items." + captured + ".done", () => "not-a-bool");
                bindings.BindReadOnly<bool>("items." + captured + ".pct", () => true);
                break;
            case ItemBindingMode.NewKindsAbsent:
                break;
        }
    }

    /// <summary>The two new-kind value bindings for one item key, bound with their declared types.</summary>
    private static void BindItemValues(IUiBindings bindings, string key)
    {
        string captured = key;
        bindings.BindValue<bool>(
            "items." + captured + ".done", () => Done[captured], value => Done[captured] = value, UiInvalidation.Paint);
        bindings.BindReadOnly<float>("items." + captured + ".pct", () => Pct[captured], UiInvalidation.Paint);
    }

    private static string? CreationFailure(string xml)
    {
        try
        {
            using var host = new UiHost(
                Scope,
                UiLayoutManifest.Parse(xml),
                MakeBindings(new[] { "a" }),
                UiTheme.DarkGold,
                new StubMetrics(),
                new StubTranslation());
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static void ExpectParseFailure(string what, string xml)
    {
        try
        {
            UiLayoutManifest.Parse(xml);
            Check(false, "the manifest parser refused " + what);
        }
        catch (FormatException)
        {
            Check(true, "the manifest parser refused " + what);
        }
        catch (Exception ex)
        {
            Check(false, "the manifest parser refused " + what + " with " + ex.GetType().Name + " instead of FormatException");
        }
    }

    // --- helpers --------------------------------------------------------------------------------

    private static void RegisterProbeKind()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(
            Scope, ChildProbeKind, () => new ChildProbeWidget(), new[] { "Id", "Kind", "Height" });
    }

    private static void Draw(UiHost host)
    {
        host.DrawFrame(new Rect(0f, 0f, 240f, 400f));
    }

    private static UiNode Require(UiHost host, string elementId)
    {
        return host.Session.GetNodeByElementId(elementId)
            ?? throw new Exception("no arranged node for '" + elementId + "'");
    }

    private static UiNode? Node(UiHost host, string elementId)
    {
        return host.Session.GetNodeByElementId(elementId);
    }

    private static UiNode? ChildNamed(UiNode node, string name)
    {
        for (int i = 0; i < node.Children.Count; i++)
        {
            if (node.Children[i].Path.EndsWith("@" + name, StringComparison.Ordinal)) return node.Children[i];
        }

        return null;
    }

    private static void Pump(UiHost host, EventType type, Vector2 point)
    {
        Event e = Event.KeyboardEvent("space");
        e.type = type;
        e.button = 0;
        e.mousePosition = point;
        Event.current = e;
        host.DrawFrame(new Rect(0f, 0f, 240f, 400f));
        Event.current = null;
    }

    private static int CountSessionNodes(UiSession session)
    {
        return Collection(Field(typeof(UiSession), "nodes", session));
    }

    private static int CountEngineWidgets(UiHost host)
    {
        object engine = Field(typeof(UiHost), "engine", host) ?? throw new Exception("the host has no engine");
        return Collection(Field(engine.GetType(), "widgetInstances", engine));
    }

    private static int CountRepeatDerived(UiHost host)
    {
        object engine = Field(typeof(UiHost), "engine", host) ?? throw new Exception("the host has no engine");
        var states = (IDictionary)Field(engine.GetType(), "repeatStates", engine);
        int count = 0;
        foreach (DictionaryEntry entry in states)
        {
            object state = entry.Value!;
            count += Collection(Field(state.GetType(), "DerivedByKey", state));
        }

        return count;
    }

    private static int Collection(object dictionary)
    {
        return ((IDictionary)dictionary).Count;
    }

    private static object Field(Type type, string name, object? instance)
    {
        FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null) throw new Exception(type.Name + " has no field '" + name + "'");
        return field.GetValue(instance)!;
    }

    private static void AssertSameVocabulary(string hostField, string engineField)
    {
        var hostSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in (IEnumerable)Field(typeof(UiHost), hostField, null))
        {
            hostSet.Add(name);
        }

        var engineSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in (IEnumerable)Field(typeof(UiLayoutEngine), engineField, null))
        {
            engineSet.Add(name);
        }

        var missing = new List<string>();
        foreach (string name in hostSet)
        {
            if (!engineSet.Contains(name)) missing.Add(name);
        }

        Check(hostSet.Count > 0 && missing.Count == 0 && hostSet.Count == engineSet.Count,
            "the engine's " + engineField + " is UiHost's " + hostField
            + " (the copy cannot drift; missing: " + string.Join(",", missing) + ")");
    }

    private static bool Near(float left, float right)
    {
        return Math.Abs(left - right) <= 0.001f;
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

    /// <summary>A widget that mints its own sub-node, so the removal lane can watch one die with its row.</summary>
    private sealed class ChildProbeWidget : IUiWidget
    {
        private UiElementSpec spec = UiElementSpec.Empty;

        string IUiWidget.Kind => ChildProbeKind;

        public void Configure(UiElementSpec spec)
        {
            this.spec = spec;
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 4f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            UiNode child = ctx.Child("probe").Node ?? throw new Exception("Child() returned no node");
            child.GetOrCreateState("probe").FloatValue = 3f;
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
