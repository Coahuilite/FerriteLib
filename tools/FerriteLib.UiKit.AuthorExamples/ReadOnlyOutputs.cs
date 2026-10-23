using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.AuthorExamples;

// Compiled as an external consumer, with no InternalsVisibleTo access to the library.
public static class ReadOnlyOutputs
{
    public static void Verify(ITextMetrics metrics, IUiTranslation translation, Action<bool, string> check)
    {
        var bindings = new UiBindings(); bindings.BindValue("value", () => .5f, _ => { });
        const string xml = "<UiPage Schema=\"2\" Source=\"readonly-author\"><Styles Schema=\"1\">"
            + "<Scheme Name=\"base\"><Palette Role=\"Foreground\" Value=\"#334455\"/><Metric Token=\"RowHeight\" Value=\"40\"/><Metric Token=\"Gap\" Value=\"bad\"/></Scheme>"
            + "<Density Name=\"compact\"><Metric Token=\"Padding\" Value=\"4\"/></Density></Styles>"
            + "<Column Id=\"parent\" Scheme=\"base\" Padding=\"0\" Gap=\"0\"><Scroll Id=\"scroll\" Height=\"80\" Padding=\"0\">"
            + "<Widget Id=\"range\" Kind=\"input/slider\" Bind=\"value\" Label=\"Range\"/></Scroll></Column></UiPage>";
        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        using var host = new UiHost("readonly-author", manifest, bindings, new UiTheme(), metrics, translation);
        using var execution = new VerseUiExecution();
        void Pump() { host.DrawFrame(new UiLayoutRect(0,0,400,300),new UiFrame(UiFramePhase.Repaint),execution); }
        Pump();
        UiElementProperties field = host.ElementProperties("range"), parent = host.ElementProperties("parent");
        var resolvedMetrics = field.ResolvedMetrics; var colours = field.ResolvedColours;
        check(resolvedMetrics.Count == 5 && colours.Count == 30 && resolvedMetrics["RowHeight"].Value == 40
            && resolvedMetrics["RowHeight"].Origin == UiPropertyOrigin.Ancestor, "real Host produces current five-metric/thirty-colour source facts");
        DictionaryWrites(resolvedMetrics, check, "ResolvedMetrics");
        DictionaryWrites(colours, check, "ResolvedColours");
        ListWrites(field.ColourNames, check, "ColourNames (already protected)");
        check(manifest.Styles.Issues.Count == 1 && manifest.Styles.Issues[0].Message.Contains("bad"), "actual style parser reports the dropped invalid metric");
        ListWrites(manifest.Styles.Issues, check, "UiStyleDocument.Issues");
        CollectionWrites(manifest.Styles.SchemeNames, check, "SchemeNames (already protected)");
        CollectionWrites(manifest.Styles.DensityNames, check, "DensityNames (already protected)");
        var resolver = new UiStyleResolver(new UiTheme(), manifest.Styles);
        var resolverIssues = resolver.Issues;
        resolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "missing-one") });
        check(resolverIssues.Count == 1, "held resolver issue view observes actual first missing-scheme resolution");
        ListWrites(resolverIssues, check, "UiStyleResolver.Issues");
        resolver.ThemeFor(new[] { new UiStyleDeclaration(scheme: "missing-two") });
        check(ReferenceEquals(resolverIssues, resolver.Issues) && resolverIssues.Count == 2
            && resolverIssues[1].Message.Contains("missing-two"), "same readonly resolver issue view observes subsequent normal resolution drop");

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new UiLayoutPoint(400, 300));
        Console.WriteLine("READONLY-SNAPSHOT-INPUT viewports=" + snapshot.Viewports.Count + " contents=" + snapshot.ScrollContents.Count + " diagnostics=" + snapshot.Diagnostics.Count);
        check(snapshot.Viewports.Count == 1 && snapshot.ScrollContents.Count == 1,
            "real arranged snapshot includes actual scroll viewport/content maps");
        DictionaryWrites(snapshot.RectById, check, "snapshot.RectById");
        DictionaryWrites(snapshot.Viewports, check, "snapshot.Viewports");
        DictionaryWrites(snapshot.ScrollContents, check, "snapshot.ScrollContents");
        ListWrites(snapshot.VisibleIds, check, "snapshot.VisibleIds");
        using var diagnosticHost = new UiHost("readonly-author", UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"readonly-author\"><Widget Id=\"narrow\" Kind=\"input/stepper-slider\" Bind=\"value\"/></UiPage>"),
            bindings, new UiTheme(), metrics, translation);
        UiLayoutSnapshot withDiagnostic = diagnosticHost.MeasureAndArrange(new UiLayoutPoint(80, 100));
        check(withDiagnostic.Diagnostics.Count > 0, "real constrained Host produces nonempty insufficient-width diagnostics");
        ListWrites(withDiagnostic.Diagnostics, check, "snapshot.Diagnostics (engine already supplied a protected list)");
        UiLayoutRect oldRange = snapshot.RectById["range"];

        parent.Metric("RowHeight").Set(52); parent.Palette(UiPaletteRole.Foreground).Set(new UiColour(0, 1, 0)); Pump();
        check(ReferenceEquals(resolvedMetrics, field.ResolvedMetrics) && ReferenceEquals(colours, field.ResolvedColours)
            && resolvedMetrics["RowHeight"].Value == 52 && colours["Foreground"].Value == new UiColour(0, 1, 0),
            "held readonly views observe normal ancestor metric/colour resolution updates without reacquiring");
        field.Metric("RowHeight").Set(60); field.Palette(UiPaletteRole.Foreground).Set(new UiColour(0, 0, 1)); Pump();
        check(resolvedMetrics["RowHeight"].Value == 60 && resolvedMetrics["RowHeight"].Origin == UiPropertyOrigin.Instance
            && colours["Foreground"].Origin == UiPropertyOrigin.Instance, "normal explicit source changes remain visible through the same views");
        field.Metric("RowHeight").Remove(); field.Palette(UiPaletteRole.Foreground).Remove();
        parent.Metric("RowHeight").Set(64); parent.Palette(UiPaletteRole.Foreground).Set(new UiColour(1, 0, 0)); Pump();
        check(resolvedMetrics["RowHeight"].Value == 64 && resolvedMetrics["RowHeight"].Origin == UiPropertyOrigin.Ancestor
            && colours["Foreground"].Value == new UiColour(1, 0, 0) && colours["Foreground"].Origin == UiPropertyOrigin.Ancestor,
            "normal remove reveals current ancestor64/red, not a readonly-view creation snapshot");
        UiLayoutSnapshot next = host.MeasureAndArrange(new UiLayoutPoint(400, 300));
        check(next.RectById["range"].Height == 64 && snapshot.RectById["range"].X == oldRange.X
            && snapshot.RectById["range"].Y == oldRange.Y && snapshot.RectById["range"].Width == oldRange.Width && snapshot.RectById["range"].Height == oldRange.Height
            && manifest.Styles.Issues.Count == 1, "new arrangement exposes current geometry while retained old snapshot/issues stay intact");
        // Public snapshot constructors also accept caller-owned read sources. Keep their existing read-through semantics.
        var rects = new Dictionary<string, UiLayoutRect>(StringComparer.OrdinalIgnoreCase) { ["entry"] = oldRange };
        var visible = new List<string> { "entry" }; var diagnostics = new List<UiLayoutDiagnostic> { withDiagnostic.Diagnostics[0] };
        var authored = new UiLayoutSnapshot(UiLayoutPoint.Zero, rects, visible, rects, rects, 0, diagnostics);
        DictionaryWrites(authored.RectById, check, "constructed snapshot map"); ListWrites(authored.VisibleIds, check, "constructed snapshot ids");
        ListWrites(authored.Diagnostics, check, "constructed snapshot diagnostics");
        rects["entry"] = new UiLayoutRect(1, 2, 3, 4); visible.Add("later");
        check(authored.RectById["ENTRY"].Width == 3 && authored.VisibleIds.Count == 2,
            "constructor-owner source updates and custom comparer retain their original legal reading semantics");
        Console.WriteLine("READONLY-TRACE metrics-view=same colours-view=same row=40->52->60->64 source=Ancestor->Instance->Ancestor colour=green->blue->red old-snapshot="
            + oldRange + " current-height=" + next.RectById["range"].Height + " issues=" + manifest.Styles.Issues.Count);
    }

    private static void Reject(Action write, Action<bool, string> check, string name)
    {
        bool rejected = false; try { write(); } catch (NotSupportedException) { rejected = true; }
        check(rejected, name + " rejects external mutation");
    }
    private static void DictionaryWrites<T>(IReadOnlyDictionary<string, T> view, Action<bool, string> check, string name)
    {
        var before = view.ToArray(); check(before.Length > 0, name + " uses actual nonempty output");
        if (view is IDictionary<string, T> map)
        {
            Reject(() => map[before[0].Key] = before[0].Value, check, name + " generic setter");
            Reject(() => map.Add("__readonly_probe__", before[0].Value), check, name + " generic Add");
            Reject(() => map.Remove(before[0].Key), check, name + " generic Remove");
            Reject(map.Clear, check, name + " generic Clear");
        }
        else check(true, name + " has no generic writable dictionary interface");
        if (view is IDictionary raw)
        {
            Reject(() => raw[before[0].Key] = before[0].Value, check, name + " nongeneric setter");
            Reject(raw.Clear, check, name + " nongeneric Clear");
        }
        else check(true, name + " has no nongeneric writable dictionary interface");
        check(view.Count == before.Length && before.All(pair => view.TryGetValue(pair.Key, out T value)
            && EqualityComparer<T>.Default.Equals(pair.Value, value)), name + " content remains unchanged");
    }
    private static void ListWrites<T>(IReadOnlyList<T> view, Action<bool, string> check, string name)
    {
        T[] before = view.ToArray(); check(before.Length > 0, name + " uses actual nonempty output");
        if (view is IList<T> list)
        {
            Reject(() => list[0] = before[0], check, name + " generic setter");
            Reject(() => list.Add(before[0]), check, name + " generic Add");
            Reject(() => list.RemoveAt(0), check, name + " generic RemoveAt"); Reject(list.Clear, check, name + " generic Clear");
        }
        else check(true, name + " has no generic writable list interface");
        if (view is IList raw)
        {
            Reject(() => raw[0] = before[0], check, name + " nongeneric setter"); Reject(() => raw.Add(before[0]), check, name + " nongeneric Add");
            Reject(raw.Clear, check, name + " nongeneric Clear");
        }
        else check(true, name + " has no nongeneric writable list interface");
        check(view.SequenceEqual(before), name + " content remains unchanged");
    }
    private static void CollectionWrites(IReadOnlyCollection<string> view, Action<bool, string> check, string name)
    {
        string[] before = view.ToArray();
        if (view is ICollection<string> collection)
        { Reject(() => collection.Add("__readonly_probe__"), check, name + " Add"); Reject(collection.Clear, check, name + " Clear"); }
        check(view.SequenceEqual(before), name + " content remains unchanged");
    }
}
