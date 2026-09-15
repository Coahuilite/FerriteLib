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
    private static readonly HashSet<string> BoundKeys = new(StringComparer.Ordinal);
    private static int pageLevelDone;
    private static bool showRows = true;

    public static int RunAll()
    {
        failures = 0;
        RegisterProbeKind();
        Run("A row set renders one element per item key", VerifyRowSetRendersPerKey);
        Run("A row reads its own item scope, not the page's key of the same name", VerifyItemLocalScope);
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

    // --- page and model -------------------------------------------------------------------------

    private const string PageXml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Templates>"
        + "<Row Id=\"row\" Gap=\"2\" Padding=\"2\">"
        + "<Widget Id=\"flag\" Kind=\"input/checkbox\" Bind=\"done\" Label=\"Done\" />"
        + "<Widget Id=\"bar\" Kind=\"display/progress\" Bind=\"pct\" Height=\"8\" />"
        + "<Widget Id=\"probe\" Kind=\"" + ChildProbeKind + "\" Height=\"4\" />"
        + "</Row>"
        + "</Templates>"
        + "<Column Id=\"page\" Gap=\"4\">"
        + "<Repeat Id=\"rows\" Items=\"items\" Template=\"row\" Gap=\"4\" VisibleKey=\"show-rows\" />"
        + "</Column>"
        + "</UiPage>";

    private static UiHost NewHost(IReadOnlyList<string> keys)
    {
        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(PageXml),
            MakeBindings(keys),
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());
    }

    /// <summary>
    /// The consumer's half of the page: the model's key projection plus one binding per item key. Nothing
    /// here knows an element's identity, a rectangle or an input rule - which is the round's success
    /// criterion stated as code rather than as prose.
    /// </summary>
    private static UiBindings MakeBindings(IReadOnlyList<string> keys)
    {
        Keys.Clear();
        Keys.AddRange(keys);
        Done.Clear();
        Pct.Clear();
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
        BindItemKeys(bindings);
        return bindings;
    }

    private static void BindItemKeys(IUiBindings bindings)
    {
        for (int i = 0; i < Keys.Count; i++)
        {
            string key = Keys[i];
            if (!BoundKeys.Add(key)) continue;

            string captured = key;
            Done[captured] = false;
            Pct[captured] = 0f;
            bindings.BindValue<bool>(
                "items." + captured + ".done", () => Done[captured], value => Done[captured] = value, UiInvalidation.Paint);
            bindings.BindReadOnly<float>("items." + captured + ".pct", () => Pct[captured], UiInvalidation.Paint);
        }
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
