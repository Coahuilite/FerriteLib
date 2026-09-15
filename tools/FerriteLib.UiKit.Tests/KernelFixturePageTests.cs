using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// <b>The round's neutral fixture page - a library fixture, never consumer validation.</b> It exists to show
/// the shape this round was for: a data-driven row set and the new public controls, with
/// <list type="bullet">
/// <item><b>no hand-written rectangles</b> - the page half below is one XML string plus bindings; every rect
/// is the engine's, derived from the theme's density and the model's values, and the lane pins that by
/// comparing the arranged row height with the theme's own row height;</item>
/// <item><b>no manual refresh</b> - the page half never calls the host, never re-creates a tree and never
/// bumps a revision; the consumer's one announcement (<c>NotifyChanged</c>) is the whole notification, and
/// the lane shows a structure change waiting for it while a value change is read live;</item>
/// <item><b>no manual collection identity</b> - the row identity is the library's
/// <c>&lt;declaredId&gt;#&lt;itemKey&gt;</c> composition over the consumer's business keys; the page half
/// contains no <c>UiNodeId</c>, no index arithmetic and no per-row dictionary of its own;</item>
/// <item><b>no manual input rules</b> - no <c>Event</c>, no hot-control handling, no hover test: each control
/// owns its hit rule, and the lane drives real event passes to prove the clicks land.</item>
/// </list>
/// <para>
/// "Our own demo is not consumption" (<c>AGENTS.md</c>): a green run here proves the kinds draw, measure and
/// recover under the stub metrics. It does not raise the validated-surface count, and it is never provenance
/// for the promotion gate.
/// </para>
/// </summary>
internal static class KernelFixturePageTests
{
    private const string Scope = "fixture-page";

    /// <summary>
    /// The page half: the page's structure, its template, and its bindings. Nothing below this line knows a
    /// rectangle, an element identity, an input rule or a refresh call - the four things the round's success
    /// criterion names.
    /// </summary>
    private const string FixturePageXml =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Templates>"
        + "<Row Id=\"entry\" Gap=\"6\" Padding=\"2\">"
        + "<Widget Id=\"done\" Kind=\"input/checkbox\" Bind=\"done\" LabelKey=\"fixture.entry.done\" />"
        + "<Widget Id=\"caption\" Kind=\"text/wrapped\" Bind=\"caption\" />"
        + "<Widget Id=\"amount\" Kind=\"display/progress\" Bind=\"amount\" Max=\"1\" Height=\"8\" />"
        + "</Row>"
        + "</Templates>"
        + "<Column Id=\"body\" Gap=\"6\" Padding=\"6\">"
        + "<Widget Id=\"total\" Kind=\"display/progress\" Bind=\"total\" Max=\"1\" Height=\"10\" />"
        + "<Repeat Id=\"entries\" Items=\"entries\" Template=\"entry\" Gap=\"2\" />"
        + "<Widget Id=\"outline\" Kind=\"container/tree\" Bind=\"outline\" ActionBind=\"focus\" RowHeight=\"18\" />"
        + "</Column>"
        + "</UiPage>";

    private static int failures;
    private static FixtureModel model = new();

    public static int RunAll()
    {
        failures = 0;
        Run("The fixture page renders one row per model key, all geometry from the theme", VerifyRowsComeFromTheModel);
        Run("A row control writes the model, and no page-level rule decided it", VerifyControlWritesTheModel);
        Run("A value change is read live; a structure change waits for the one announcement", VerifyAnnouncementIsTheOnlyRefresh);
        Run("A Paint-class announcement reuses the arrangement", VerifyPaintClassReusesTheArrangement);
        Run("The tree control presents levels and reports the clicked row's key", VerifyTreePresentation);
        return failures;
    }

    private static void VerifyRowsComeFromTheModel()
    {
        using UiHost host = FixtureHost(out _);
        ClearDraws();
        UiLayoutSnapshot snapshot = Arrange(host);

        UiTheme theme = UiTheme.DarkGold;
        foreach (string key in model.Keys)
        {
            UiNode? row = host.Session.GetNodeByElementId("entry#" + key);
            Check(row != null, "entry '" + key + "' is a row element with its own identity");
            Check(Near(snapshot.RectById["done#" + key].height, theme.Geometry.RowHeight),
                "and the checkbox band inside it is the density's row height, not a page literal");
        }

        IList texts = (IList)Field(typeof(Verse.Widgets), "LabelTexts", null);
        foreach (string key in model.Keys)
        {
            bool found = false;
            foreach (object text in texts)
            {
                if (string.Equals((string)text!, model.Caption[key], StringComparison.Ordinal)) found = true;
            }

            Check(found, "the row drew its own item's caption: " + model.Caption[key]);
        }
    }

    private static void VerifyControlWritesTheModel()
    {
        using UiHost host = FixtureHost(out _);
        UiLayoutSnapshot snapshot = Arrange(host);
        string key = model.Keys[1];

        Check(!model.Done[key], "the model starts with the second entry unset");
        ClickAt(host, Centre(snapshot.RectById["done#" + key]));
        Check(model.Done[key], "the checkbox click wrote the model through the item-local key");
        Check(!model.Done[model.Keys[0]], "and no sibling entry's value moved");
    }

    private static void VerifyAnnouncementIsTheOnlyRefresh()
    {
        using UiHost host = FixtureHost(out IUiBindings bindings);
        Arrange(host);

        string added = "entry-c";
        model.AddEntry(added);
        Arrange(host);
        Check(host.Session.GetNodeByElementId("entry#" + added) == null,
            "a model change nobody announced does not reconcile the row set: the library never polls");

        // Binding the new item's own keys is consumer work, not a refresh: it is the same one-line projection
        // per key the page half already registered for the entries that existed at build time.
        BindEntry(bindings, added);
        bindings.NotifyChanged("entries");
        UiLayoutSnapshot after = Arrange(host);
        Check(host.Session.GetNodeByElementId("entry#" + added) != null,
            "the one announcement is what materializes the inserted row");
        Check(after.RectById.ContainsKey("entry#" + added) && after.RectById.ContainsKey("entry#" + model.Keys[0]),
            "and the existing rows keep their geometry in the same arrangement");

        // The value half of the same pairing: a done flag is read live by the control's own paint, so no page
        // code runs and nothing is re-measured for it. The recording is cleared before each pass so the two
        // counts are two frames and not the lane's history.
        string key = model.Keys[0];
        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 240f, 400f));
        int before = BoxSolids(host, "done#" + key);
        Check(before > 0, "the unchecked checkbox painted its box alone");

        model.Done[key] = true;
        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 240f, 400f));
        int afterSolids = BoxSolids(host, "done#" + key);
        Check(afterSolids > before,
            "a value change is painted on the next pass without re-creating anything on the page side ("
            + before + " -> " + afterSolids + ")");
    }

    private static void VerifyPaintClassReusesTheArrangement()
    {
        using UiHost host = FixtureHost(out IUiBindings bindings);
        UiLayoutSnapshot before = Arrange(host);
        float beforeWidth = ProgressFillWidth(host, "total");

        model.Total = 0.75f;
        bindings.NotifyChanged("total");
        UiLayoutSnapshot after = Arrange(host);

        Check(ReferenceEquals(before, after),
            "a Paint-class announcement keeps the arranged snapshot, so a fixed-size readout never re-measures the page");
        Check(ProgressFillWidth(host, "total") > beforeWidth, "while the painted proportion followed the model");
    }

    private static void VerifyTreePresentation()
    {
        using UiHost host = FixtureHost(out _);
        ClearDraws();
        UiLayoutSnapshot snapshot = Arrange(host);
        Rect tree = snapshot.RectById["outline"];

        IList rects = (IList)Field(typeof(Verse.Widgets), "LabelRects", null);
        var outlineLabels = new List<Rect>();
        foreach (object entry in rects)
        {
            var rect = (Rect)entry;
            if (rect.y >= tree.y - 1f) outlineLabels.Add(rect);
        }

        Check(outlineLabels.Count == model.Outline.Count, "the outline drew one band per supplied row: " + outlineLabels.Count);
        Check(outlineLabels[1].x < outlineLabels[2].x,
            "and each deeper level is indented past its parent: " + outlineLabels[1].x + " -> " + outlineLabels[2].x);

        UiTreeRow second = model.Outline[1];
        ClickAt(host, new Vector2(tree.x + 4f, tree.y + 18f + 9f));
        Check(string.Equals(model.Focused, second.Key, StringComparison.Ordinal),
            "a click reports the clicked row's own stable key: " + model.Focused);
    }

    // --- the page half's model -------------------------------------------------------------------

    /// <summary>
    /// The consumer's authoritative model, held beside the page: the library never owns it, never stores it in
    /// a session and never saves it. Its keys are the only identity the page supplies.
    /// </summary>
    private sealed class FixtureModel
    {
        internal readonly List<string> Keys = new();
        internal readonly Dictionary<string, bool> Done = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Caption = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, float> Amount = new(StringComparer.Ordinal);
        internal readonly List<UiTreeRow> Outline = new();
        internal float Total;
        internal string Focused = "";

        internal void AddEntry(string key)
        {
            if (Keys.Contains(key)) return;
            Keys.Add(key);
            Done[key] = false;
            Caption[key] = "Entry " + key;
            Amount[key] = 0.25f;
        }
    }

    private static UiHost FixtureHost(out IUiBindings bindings)
    {
        model = new FixtureModel();
        model.AddEntry("entry-a");
        model.AddEntry("entry-b");
        model.Total = 0.5f;
        model.Outline.Add(new UiTreeRow("group", 0, "Group", expandable: true, expanded: true));
        model.Outline.Add(new UiTreeRow("item", 1, "Item"));
        model.Outline.Add(new UiTreeRow("item-2", 2, "Item two"));

        var bindingsImpl = new UiBindings();
        bindingsImpl.BindReadOnly<IReadOnlyList<string>>("entries", () => model.Keys, UiInvalidation.Structure);
        bindingsImpl.BindReadOnly<float>("total", () => model.Total, UiInvalidation.Paint);
        bindingsImpl.BindReadOnly<IReadOnlyList<UiTreeRow>>("outline", () => model.Outline, UiInvalidation.Structure);
        bindingsImpl.BindAction<string>("focus", key => model.Focused = key);
        bindings = bindingsImpl;

        var host = new UiHost(
            Scope, UiLayoutManifest.Parse(FixturePageXml), bindingsImpl, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

        // The per-item half of the bindings: one projection per key, exactly what a consumer's own page code
        // does when its collection changes. No identity, no rectangle, no input rule.
        foreach (string key in model.Keys)
        {
            BindEntry(bindingsImpl, key);
        }

        return host;
    }

    private static void BindEntry(IUiBindings bindings, string key)
    {
        string captured = key;
        bindings.BindValue<bool>(
            "entries." + captured + ".done", () => model.Done[captured], value => model.Done[captured] = value, UiInvalidation.Paint);
        bindings.BindReadOnly<string>("entries." + captured + ".caption", () => model.Caption[captured]);
        bindings.BindReadOnly<float>("entries." + captured + ".amount", () => model.Amount[captured], UiInvalidation.Paint);
    }

    // --- helpers ---------------------------------------------------------------------------------

    private static void ClickAt(UiHost host, Vector2 point)
    {
        Pump(host, EventType.MouseDown, point);
        Pump(host, EventType.MouseUp, point);
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

    private static Vector2 Centre(Rect rect)
    {
        return new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
    }

    /// <summary>Solid calls recorded inside one element's band, after a cleared recording.</summary>
    private static int BoxSolids(UiHost host, string elementId)
    {
        UiNode? node = host.Session.GetNodeByElementId(elementId);
        if (node == null || !node.Rect.HasValue) return 0;
        Rect band = node.Rect.Value;
        IList rects = (IList)Field(typeof(Verse.Widgets), "DrawBoxSolidRects", null);
        int count = 0;
        foreach (object entry in rects)
        {
            var rect = (Rect)entry;
            if (rect.x >= band.x - 0.5f && rect.xMax <= band.xMax + 0.5f
                && rect.y >= band.y - 0.5f && rect.yMax <= band.yMax + 0.5f)
            {
                count++;
            }
        }

        return count;
    }

    private static UiLayoutSnapshot Arrange(UiHost host)
    {
        host.DrawFrame(new Rect(0f, 0f, 240f, 400f));
        return host.MeasureAndArrange(new Vector2(240f, 400f));
    }

    private static float ProgressFillWidth(UiHost host, string elementId)
    {
        ClearDraws();
        host.DrawFrame(new Rect(0f, 0f, 240f, 400f));
        UiNode? node = host.Session.GetNodeByElementId(elementId);
        if (node == null || !node.Rect.HasValue) return 0f;
        Rect band = node.Rect.Value;
        IList rects = (IList)Field(typeof(Verse.Widgets), "DrawBoxSolidRects", null);

        // The track's own edge strips are solids too, so the proportion is the widest recording inside the
        // band that is narrower than the track itself: the fill is painted last and widest.
        float widest = 0f;
        foreach (object entry in rects)
        {
            var rect = (Rect)entry;
            if (rect.x >= band.x - 0.5f && rect.xMax <= band.xMax + 0.5f
                && rect.y >= band.y - 0.5f && rect.y <= band.yMax + 0.5f
                && rect.width < band.width - 1f)
            {
                widest = Math.Max(widest, rect.width);
            }
        }

        return widest;
    }

    private static void ClearDraws()
    {
        Invoke(typeof(Verse.Widgets), "ClearDrawBoxSolidCalls");
        ((IList)Field(typeof(Verse.Widgets), "LabelRects", null)).Clear();
        ((IList)Field(typeof(Verse.Widgets), "LabelTexts", null)).Clear();
        ((IList)Field(typeof(Verse.Widgets), "LabelColors", null)).Clear();
    }

    private static object Field(Type type, string name, object? instance)
    {
        FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        if (field == null) throw new Exception(type.Name + " has no field '" + name + "'");
        return field.GetValue(instance)!;
    }

    private static void Invoke(Type type, string name)
    {
        MethodInfo? method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
        if (method == null) throw new Exception(type.Name + " has no method '" + name + "'");
        method.Invoke(null, null);
    }

    private static bool Near(float left, float right)
    {
        return Math.Abs(left - right) <= 0.01f;
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

