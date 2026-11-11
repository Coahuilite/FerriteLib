using System;
using System.Collections.Generic;
using System.Linq;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

// New cases are confined to the adopted FL09 event-tiering semantics. Recording execution
// exercises the real Host/wrapper/control paths; native label isolation uses the existing stub.
internal static class KernelEventTieringTests
{
    private static readonly UiLayoutRect View = new(0, 0, 240, 200);
    private static int Run(string name, Action action)
    {
        try { NativeFixture.ResetInput(); action(); Console.WriteLine("  ok: TIER " + name); return 0; }
        catch (Exception error) { Console.Error.WriteLine("  FAIL: TIER " + name + " " + error); return 1; }
    }
    public static int RunAll()
    {
        UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore();
        int failures = 0;
        failures += Run("Schema defaults, explicit overrides and programmatic synonym", Schemas);
        failures += Run("Located execution-policy refusals", InvalidDefinitions);
        failures += Run("Repaint-bounded T2 and per-pass custom Draw/hit publication", Cycles);
        failures += Run("Every declared change resumes synchronization", DeclaredChanges);
        failures += Run("Repaint adopts silent ordinary layout/display and unknown-ruler changes", FreshRepaint);
        failures += Run("T1 native control sequence, editing and eligibility remain per pass", Controls);
        failures += Run("Getter-free default preserves ordinary display and selected-role reads", SafeDefault);
        failures += Run("Backend without isolation keeps all display work", WithoutIsolation);
        failures += Run("Overlay policy inheritance, false override, reveal and compatibility", Clipping);
        failures += Run("Deferred display retains faults and last paint", RetainedFault);
        failures += Run("Hidden Cancel subject and help grace keep their pass lifetimes", CancelAndHelp);
        failures += Run("Native label style restoration includes throwing display", NativeStyleIsolation);
        NativeFixture.ResetInput();
        return failures;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static UiTheme Theme() => new() { Geometry = new UiGeometry(0, 0, 0, 20, 1) };
    private static UiHost Host(string body, UiBindings? bindings = null, string schema = "3", string policy = "",
        ITextMetrics? metrics = null, UiTheme? theme = null, Translation? translation = null)
        => new("tier", UiLayoutManifest.Parse("<UiPage Source=\"tier\" Schema=\"" + schema + "\" " + policy + ">" + body + "</UiPage>"),
            bindings ?? new UiBindings(), theme ?? Theme(), metrics ?? new Metrics(), translation ?? new Translation());
    private static UiNode Node(UiHost host, string id) => host.Session.GetNodeByElementId(id) ?? throw new Exception("Missing " + id);
    private static void Pump(UiHost host, Recording execution, UiFramePhase phase, UiLayoutPoint? point = null, UiLayoutRect? view = null)
    {
        execution.Frame = new UiFrame(phase, point ?? new UiLayoutPoint(-20, -20));
        execution.Controls.Clear();
        host.DrawFrame(view ?? View, execution.Frame, execution);
    }
    private static void Schemas()
    {
        UiLayoutManifest Parse(string attrs) => UiLayoutManifest.Parse("<UiPage Source=\"tier\" " + attrs + "/>");
        Check(!Parse("Schema=\"2\"").TieredEvents, "Schema 2 changed its default");
        Check(Parse("Schema=\"3\"").TieredEvents, "Schema 3 is not tiered by default");
        Check(Parse("Schema=\"2\" TieredEvents=\"true\"").TieredEvents, "Schema 2 cannot opt in");
        Check(!Parse("Schema=\"3\" TieredEvents=\"false\"").TieredEvents, "Schema 3 cannot opt out");
        Check(Parse("Schema=\"3\"").SchemaVersion == "3", "Parsing lost the schema identity");
        UiLayoutManifest old = UiLayoutManifest.Create("tier", Array.Empty<UiElementSpec>());
        UiLayoutManifest opted = old.WithTieredEvents(true);
        Check(!old.TieredEvents && old.SchemaVersion == "2" && opted.TieredEvents && ReferenceEquals(old.Roots, opted.Roots), "Programmatic policy changed the old factory or definitions");
        using var legacy = Host("<Widget Id=\"t\" Kind=\"text/wrapped\" Text=\"a\" Height=\"20\"/>", schema: "2");
        using var optedOut = Host("", policy: "TieredEvents=\"false\"");
        var execution = new Isolated();
        foreach (UiHost host in new[] { legacy, optedOut })
        { Pump(host, execution, UiFramePhase.Layout); Pump(host, execution, UiFramePhase.KeyDown); Check(host.Session.LayoutWork.Passes == 2, "Per-pass opt-out reused T2"); }
    }
    private static void InvalidDefinitions()
    {
        foreach (string xml in new[] {
            "<UiPage Schema=\"4\" Source=\"tier\"/>",
            "<UiPage Schema=\"3\" Source=\"tier\" TieredEvents=\"1\"/>",
            "<UiPage Schema=\"3\" Source=\"tier\"><Column TieredEvents=\"true\"/></UiPage>",
            "<UiPage Schema=\"3\" Source=\"tier\"><Overlay DeferClippedDisplay=\"1\"/></UiPage>",
            "<UiPage Schema=\"3\" Source=\"tier\"><Column DeferClippedDisplay=\"true\"/></UiPage>",
            "<UiPage Schema=\"3\" Source=\"tier\" DeferClippedDisplay=\"true\"/>",
            "<UiPage Schema=\"3\" Source=\"tier\"><Templates TieredEvents=\"true\"/></UiPage>" })
        {
            try { UiLayoutManifest.Parse(xml); throw new Exception("Accepted " + xml); }
            catch (FormatException error) { Check(error.Message.Contains("line ") && error.Message.Contains("position "), "Missing source location: " + error.Message); }
        }
        UiElementSpec invalid = new UiElementSpec("bad", "Column", new Dictionary<string, string> { ["TieredEvents"] = "true" });
        try { using var host = new UiHost("tier", UiLayoutManifest.Create("tier", new[] { invalid }), new UiBindings(), Theme(), new Metrics(), new Translation()); throw new Exception("Programmatic nested policy accepted"); }
        catch (UiContractException error) { Check(error.ElementId == "bad", "Missing programmatic element location"); }
    }
    private static void Cycles()
    {
        var widget = new Custom();
        UiWidgetRegistry.Define(new UiWidgetDefinition("tier", "tier/custom", () => widget));
        using var host = Host("<Widget Id=\"custom\" Kind=\"tier/custom\"/>");
        var execution = new Isolated();
        var phases = new[] { UiFramePhase.Layout, UiFramePhase.KeyDown, UiFramePhase.Layout, UiFramePhase.KeyDown,
            UiFramePhase.Layout, UiFramePhase.KeyUp, UiFramePhase.Layout, UiFramePhase.Repaint };
        foreach (UiFramePhase phase in phases) Pump(host, execution, phase);
        Check(host.Session.LayoutWork.Passes == 2 && widget.Draws == 8 && widget.PointerReads == 2, "T1/T2 split drifted: " + host.Session.LayoutWork);
        Check(host.Session.Frame == 8 && host.Session.HitLayers.Count == 1, "Hit/frame publication stopped on reused events");
        Pump(host, execution, UiFramePhase.KeyDown);
        Check(host.Session.LayoutWork.Passes == 3, "First event after Repaint did not open a new cycle");
        for (int i = 0; i < 12; i++) Pump(host, execution, UiFramePhase.Layout);
        Check(host.Session.LayoutWork.Passes == 3, "A non-Repaint event advanced the cycle");
        using var other = Host(""); Pump(other, execution, UiFramePhase.Repaint); Pump(host, execution, UiFramePhase.KeyUp);
        Check(host.Session.LayoutWork.Passes == 3, "Another Host closed this cycle");
        widget.Fail = true; Pump(host, execution, UiFramePhase.KeyDown);
        Check(Node(host, "custom").PresentationUnavailable, "Custom failure path was not reached");
        widget.Fail = false; Pump(host, execution, UiFramePhase.KeyUp);
        Check(host.Session.LayoutWork.Passes == 4 && !Node(host, "custom").PresentationUnavailable, "Faulted pointer production reused its old operation layers");
        Pump(host, execution, UiFramePhase.Layout);
        Check(host.Session.LayoutWork.Passes == 4 && widget.PointerReads == 4,
            "Successful recovery did not publish current pointer geometry at its synchronization");
    }
    private static void DeclaredChanges()
    {
        string text = "a"; var b = new UiBindings(); b.BindReadOnly("text", () => text);
        bool value = false; b.BindValue("check", () => value, v => value = v);
        var theme = Theme(); var translation = new Translation();
        using var host = Host("<Widget Id=\"t\" Kind=\"text/wrapped\" Bind=\"text\" Height=\"20\"/>", b, theme: theme, translation: translation);
        var execution = new Isolated(); Pump(host, execution, UiFramePhase.Layout);
        void Change(Action change, string reason)
        { long before = host.Session.LayoutWork.Passes; change(); Pump(host, execution, UiFramePhase.KeyDown); Check(host.Session.LayoutWork.Passes == before + 1, reason); }
        Change(() => { text = "b"; b.NotifyChanged("text"); }, "NotifyChanged did not synchronize");
        Change(host.Invalidate, "ContentRevision did not synchronize");
        Change(() => Node(host, "t").MarkDirty(), "MarkDirty did not synchronize");
        Change(() => theme.Geometry = new UiGeometry(0, 1, 0, 20, 1), "Theme layout revision did not synchronize");
        Change(() => translation.Revision++, "Translation revision did not synchronize");
        Change(() => host.ElementProperties("t").Font.Set(UiFont.Tiny), "Element property revision did not synchronize");
        Change(() => b.Set("check", true), "Completed unnotified business request did not synchronize");
        long count = host.Session.LayoutWork.Passes;
        Pump(host, execution, UiFramePhase.KeyUp, view: new UiLayoutRect(1, 0, 240, 200));
        Check(host.Session.LayoutWork.Passes == count + 1, "Viewport change did not synchronize");
        var source = b.Reactive.Text("old");
        b.BindTracked("tracked", () => source.Value);
        using var tracked = Host("<Widget Id=\"t\" Kind=\"text/wrapped\" Bind=\"tracked\"/>", b);
        Pump(tracked, execution, UiFramePhase.Layout); count = tracked.Session.LayoutWork.Passes;
        source.Value = "new"; Pump(tracked, execution, UiFramePhase.KeyDown);
        Check(tracked.Session.LayoutWork.Passes == count + 1, "Tracked demand invalidation did not synchronize");
    }
    private static void FreshRepaint()
    {
        string text = "one"; int reads = 0; bool visible = true;
        var b = new UiBindings(); b.BindReadOnly("text", () => { reads++; return text; }); b.BindReadOnly("visible", () => visible);
        var metrics = new Metrics();
        using var host = Host("<Column Padding=\"0\" Gap=\"0\"><Widget Id=\"text\" Kind=\"text/wrapped\" Bind=\"text\"/><Widget Id=\"conditional\" Kind=\"text/wrapped\" Text=\"visible\" VisibleBind=\"visible\" Height=\"20\"/></Column>", b, metrics: metrics);
        var execution = new Isolated(); Pump(host, execution, UiFramePhase.Layout); int captured = reads;
        text = "one\ntwo\nthree"; visible = false; metrics.LineHeight = 30;
        Pump(host, execution, UiFramePhase.KeyDown);
        Check(reads == captured && Node(host, "text").Rect!.Value.Height == 20 && Node(host, "conditional").Rect.HasValue, "Ordinary inputs were adopted between sync points: reads=" + reads + "/" + captured + " height=" + Node(host, "text").Rect!.Value.Height + " conditional=" + Node(host, "conditional").Rect.HasValue + " " + host.Session.LayoutWork);
        Pump(host, execution, UiFramePhase.Repaint);
        Check(Node(host, "text").Rect!.Value.Height == 90 && !Node(host, "conditional").Rect.HasValue && execution.Texts.Contains(text), "Repaint did not freshly measure and paint silent ordinary changes");
    }
    private static void Controls()
    {
        var b = new UiBindings(); int checkReads = 0, editorReads = 0; bool check = false; string edit = "draft";
        b.BindValue("check", () => { checkReads++; return check; }, v => check = v);
        b.BindValue("edit", () => { editorReads++; return edit; }, v => edit = v);
        bool allowed = true; b.BindCommand("go", () => { }, () => allowed);
        using var host = Host("<Column Padding=\"0\" Gap=\"0\"><Widget Id=\"button\" Kind=\"input/button\" ActionBind=\"go\" Text=\"Go\" Height=\"20\"/><Widget Id=\"check\" Kind=\"input/checkbox\" Bind=\"check\" Label=\"Check\" Height=\"20\"/><Widget Id=\"edit\" Kind=\"input/text-field\" Bind=\"edit\" Height=\"20\"/></Column>", b);
        var execution = new Isolated(); string[]? order = null;
        foreach (UiFramePhase phase in new[] { UiFramePhase.Layout, UiFramePhase.KeyDown, UiFramePhase.KeyUp, UiFramePhase.Repaint })
        {
            Pump(host, execution, phase);
            string[] current = execution.Controls.Select(key => key.Key).ToArray();
            if (order == null) order = current;
            else Check(Enumerable.SequenceEqual(order, current), "Control-ID order changed at " + phase);
            Check(current.Length >= 3, "Control path was not exercised");
        }
        Check(execution.Edits == 4 && checkReads >= 4 && editorReads >= 4, "T1 editing/checkbox input was omitted");
        allowed = false; long passes = host.Session.LayoutWork.Passes;
        Pump(host, execution, UiFramePhase.KeyDown); // first pass after Repaint
        passes = host.Session.LayoutWork.Passes; allowed = true; Pump(host, execution, UiFramePhase.KeyUp);
        Check(!Node(host, "button").IsDisabled && host.Session.LayoutWork.Passes == passes, "Eligibility froze between synchronization points");
    }
    private static void SafeDefault()
    {
        int ordinary = 0, selected = 0; var b = new UiBindings();
        b.BindReadOnly("ordinary", () => { ordinary++; return "ordinary"; });
        b.BindReadOnly("selected", () => { selected++; return true; });
        b.BindTracked("tracked", () => "tracked"); b.BindCommand("go", () => { }); b.BindValue("check", () => false, _ => { });
        var tr = new Translation();
        using var host = Host("<Column Padding=\"0\" Gap=\"0\"><Widget Kind=\"text/wrapped\" TextKey=\"static\" Height=\"20\"/><Widget Kind=\"text/wrapped\" Bind=\"tracked\" Height=\"20\"/><Widget Kind=\"text/wrapped\" Bind=\"ordinary\" Height=\"20\"/><Widget Kind=\"text/wrapped\" Text=\"selected\" SelectedBind=\"selected\" Height=\"20\"/><Widget Kind=\"input/button\" ActionBind=\"go\" TextKey=\"button\" SelectedBind=\"selected\" Height=\"20\"/><Widget Kind=\"input/checkbox\" Bind=\"check\" LabelKey=\"check\" SelectedBind=\"selected\" Height=\"20\"/></Column>", b, schema: "2", translation: tr);
        var execution = new Isolated();
        Pump(host, execution, UiFramePhase.Layout); Pump(host, execution, UiFramePhase.KeyDown);
        Check(ordinary == 2 && selected == 6, "Getter-free default omitted an ordinary value/selected-role read");
        Check(tr.Calls == 0 && execution.Texts.SequenceEqual(new[] { "ordinary", "ordinary" }), "Static/tracked/button/checkbox display did not defer by default");
        Pump(host, execution, UiFramePhase.Repaint);
        Check(tr.Calls == 3 && execution.Texts.Contains("tracked") && ordinary == 3 && selected == 9, "Repaint did not execute complete display");
    }
    private static void WithoutIsolation()
    {
        int reads = 0; var b = new UiBindings(); b.BindReadOnly("text", () => { reads++; return "ordinary"; });
        using var host = Host("<Widget Kind=\"text/wrapped\" Bind=\"text\" Height=\"20\"/>", b);
        var execution = new Recording(); Pump(host, execution, UiFramePhase.Layout); Pump(host, execution, UiFramePhase.KeyDown);
        Check(reads == 2 && execution.Texts.Count == 2 && host.Session.LayoutWork.Passes == 1, "Absent isolation did not fall back for display only");
    }
    private static string ClippedBody(string outer, string inner = "", string leaf = "")
        => "<Scroll Id=\"scroll\" Height=\"20\" Padding=\"0\" Gap=\"0\"><Widget Kind=\"text/wrapped\" Text=\"filler\" Height=\"40\"/><Overlay Id=\"outer\" Height=\"20\" Padding=\"0\" " + outer + "><Overlay Id=\"inner\" Height=\"20\" Padding=\"0\" " + inner + "><Widget Id=\"leaf\" Kind=\"text/wrapped\" Bind=\"text\" Height=\"20\" " + leaf + "/></Overlay></Overlay></Scroll>";
    private static void Clipping()
    {
        foreach (var policy in new[] { ("2", "DeferClippedDisplay=\"true\"", "", 0), ("2", "", "", 1),
            ("2", "DeferClippedDisplay=\"true\"", "DeferClippedDisplay=\"false\"", 1),
            ("2", "DeferClippedDisplay=\"false\"", "DeferClippedDisplay=\"true\"", 0),
            ("3", "", "", 0), ("3", "DeferClippedDisplay=\"false\"", "", 1) })
        {
            int reads = 0; string text = "old"; var b = new UiBindings(); b.BindReadOnly("text", () => { reads++; return text; });
            using var host = Host(ClippedBody(policy.Item2, policy.Item3), b, schema: policy.Item1);
            var execution = new Isolated(); Pump(host, execution, UiFramePhase.Repaint);
            Check(reads == policy.Item4, "Clipped policy inheritance mismatch " + policy);
            text = "fresh"; host.SetScrollPosition("scroll", new UiLayoutPoint(0, 40));
            Pump(host, execution, UiFramePhase.Repaint);
            Check(execution.Texts.Last() == "fresh", "Reveal reused an unnotified ordinary display");
        }
        int fallbackReads = 0; var fallback = new UiBindings(); fallback.BindReadOnly("text", () => { fallbackReads++; return "fallback"; });
        using var noIsolation = Host(ClippedBody("DeferClippedDisplay=\"true\""), fallback, schema: "2");
        Pump(noIsolation, new Recording(), UiFramePhase.Layout);
        Check(fallbackReads == 1, "Clipped opt-in imposed an isolation prerequisite on an old backend");

        int stateReads = 0; var controls = new UiBindings(); controls.BindCommand("go", () => { });
        controls.BindValue("check", () => { stateReads++; return false; }, _ => { });
        using var clippedControls = Host("<Scroll Height=\"20\" Padding=\"0\" Gap=\"0\"><Widget Kind=\"text/wrapped\" Text=\"filler\" Height=\"40\"/><Overlay Height=\"20\" Padding=\"0\" DeferClippedDisplay=\"true\"><Widget Kind=\"input/button\" ActionBind=\"go\" Text=\"clipped-button\" Height=\"20\"/><Widget Kind=\"input/checkbox\" Bind=\"check\" Label=\"clipped-check\" Height=\"20\"/></Overlay></Scroll>", controls, schema: "2");
        var isolated = new Isolated(); Pump(clippedControls, isolated, UiFramePhase.Layout);
        string[] ids = isolated.Controls.Select(key => key.Key).ToArray();
        Pump(clippedControls, isolated, UiFramePhase.Repaint);
        Check(ids.Length == 2 && Enumerable.SequenceEqual(ids, isolated.Controls.Select(key => key.Key)) && stateReads == 2
            && !isolated.Texts.Contains("clipped-button") && !isolated.Texts.Contains("clipped-check"), "Clipping omitted input reads or native control calls");
    }
    private static void RetainedFault()
    {
        bool fail = false; var b = new UiBindings();
        b.BindReadOnly("text", () => fail ? throw new UiReactiveFault("tier-fault", "display", "Retry", new Exception("tier-display-failure")) : "last-good");
        using var host = Host("<Widget Id=\"t\" Kind=\"text/wrapped\" Bind=\"text\" Height=\"20\"/>", b);
        var execution = new Isolated(); Pump(host, execution, UiFramePhase.Repaint);
        fail = true; Pump(host, execution, UiFramePhase.Repaint);
        UiReactiveFault? fault = Node(host, "t").PresentationFault;
        Check(fault != null && execution.Texts.Contains("last-good"), "Fault path did not retain the last successful paint");
        int paints = execution.Texts.Count; Pump(host, execution, UiFramePhase.Layout); Pump(host, execution, UiFramePhase.KeyDown);
        Check(ReferenceEquals(fault, Node(host, "t").PresentationFault) && execution.Texts.Count == paints, "Omission was mistaken for successful display");
        fail = false; Pump(host, execution, UiFramePhase.Repaint);
        Check(Node(host, "t").PresentationFault == null && execution.Texts.Last() == "last-good", "Real successful display did not recover");
    }
    private static void CancelAndHelp()
    {
        bool visible = true; int cancelled = 0; var b = new UiBindings(); b.BindReadOnly("visible", () => visible);
        b.BindCommand("cancel", () => cancelled++); b.BindCommand("go", () => { });
        using var host = Host("<Widget Id=\"subject\" Kind=\"input/button\" ActionBind=\"go\" Text=\"subject\" Height=\"20\" VisibleBind=\"visible\" CancelBind=\"cancel\" HelpRef=\"topic\"/>", b);
        host.Session.HelpGracePasses = 1; var execution = new Isolated();
        Pump(host, execution, UiFramePhase.Layout, new UiLayoutPoint(10, 10));
        Check(host.Session.SetCancelTarget("subject") && host.Session.CurrentHelpRef == "topic", "Subject/help path not reached");
        Pump(host, execution, UiFramePhase.KeyDown); Pump(host, execution, UiFramePhase.KeyUp);
        Check(host.Session.CurrentHelpRef == "topic", "Help grace ended before its pass allowance");
        Pump(host, execution, UiFramePhase.Layout);
        Check(host.Session.CurrentHelpRef == null && host.Session.LayoutWork.Passes == 1, "Help grace was counted in cycles instead of passes");
        visible = false; b.NotifyChanged("visible"); Pump(host, execution, UiFramePhase.KeyDown);
        Check(host.Session.CancelTargetNode == null && !host.TryHandleCancel() && cancelled == 0, "Hidden subject retained a Cancel rung after declared resync");
    }
    private static void NativeStyleIsolation()
    {
        using var execution = new VerseUiExecution();
        Check(execution is IUiIsolatedPaint, "1.6 owner did not declare isolation");
        var paint = (IUiIsolatedPaint)execution;
        Check(paint.CanIsolatePaint, "Valid native style table refused isolation");
        GUIStyle[] styles = Text.fontStyles;
        var original = styles.Select(s => (s.alignment, s.wordWrap, s.fontSize)).ToArray();
        GUIUtility.hotControl = 0;
        try
        {
            try
            {
                paint.IsolatePaint(() =>
                {
                    foreach (GUIStyle style in styles) { style.alignment = TextAnchor.LowerRight; style.wordWrap = !style.wordWrap; style.fontSize = 47; }
                    GUIUtility.hotControl = 71;
                    throw new InvalidOperationException("isolated-display-throws");
                });
                throw new Exception("Isolation swallowed display failure");
            }
            catch (InvalidOperationException error) { Check(error.Message == "isolated-display-throws", "Wrong failure"); }
            Check(styles.Select(s => (s.alignment, s.wordWrap, s.fontSize)).SequenceEqual(original), "Throwing display leaked mutable label fields");
            Check(GUIUtility.hotControl == 71, "Isolation rolled back legitimate native control state");
        }
        finally { GUIUtility.hotControl = 0; }
    }
    private sealed class Translation : IUiTranslation
    { internal int Revision, Calls; public int TranslationRevision => Revision; public string Translate(string key) { Calls++; return key; } }
    // Intentionally unknown environment, so Repaint cannot invent a font-data revision.
    private sealed class Metrics : ITextMetrics
    {
        internal float LineHeight = 20;
        public float MeasureWidth(string text, UiFont font) => text.Length * 8;
        public float MeasureText(string text, UiFont font, float width) => text.Split(new[] { '\n' }).Length * LineHeight;
    }
    private sealed class Custom : IUiWidget, IUiPointerLayout
    {
        internal int Draws, PointerReads;
        internal bool Fail;
        public string Kind => "tier/custom";
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindingAccess bindings, string path) { }
        public float Measure(UiWidgetContext context) => 20;
        public void Draw(UiLayoutRect rect, UiWidgetContext context)
        { Draws++; if (Fail) throw new UiReactiveFault("custom-tier-fault", "display", "Retry", new Exception("custom-display-failure")); }
        public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext context, UiLayoutRect bounds)
        { PointerReads++; yield return new UiPointerRegion(bounds); }
    }
    private class Recording : IUiExecution
    {
        internal UiFrame Frame = new(UiFramePhase.None);
        internal readonly List<UiControlKey> Controls = new();
        internal readonly List<string> Texts = new();
        internal int Edits;
        private UiControlKey? capture;
        public bool CaptureAvailable => capture == null;
        public void PrepareControl(UiControlKey key) => Controls.Add(key);
        public bool HasCapture(UiControlKey key) => capture == key;
        public void Capture(UiControlKey key) => capture = key;
        public void Release(UiControlKey key) { if (capture == key) capture = null; }
        public bool HasFocus(UiControlKey key) => false;
        public void ReleaseFocus(UiControlKey key) { }
        public UiNativeEditResult Edit(UiNativeEdit request) { Controls.Add(request.Control); Edits++; return new(request.Text, false); }
        public void ConsumeEvent() => Frame.Consume();
        public void WithState(Action action) => action();
        public void Scroll(UiLayoutRect viewport, UiLayoutRect content, UiLayoutPoint position, bool showScrollbar, Action<UiLayoutPoint> body) => body(position);
        public void Warning(string message) { }
        public void Clip(UiLayoutRect rect, Action action) => action();
        public void Fill(UiLayoutRect rect, UiColour colour) { }
        public void Text(UiLayoutRect rect, string text, UiFont font, UiColour colour, UiTextAnchor anchor, bool wrap) => Texts.Add(text);
        public void Line(UiLayoutPoint from, UiLayoutPoint to, UiColour colour, float width) { }
        public void Image(UiLayoutRect rect, UiImage image, UiColour tint) { }
        public void Tooltip(UiLayoutRect rect, string text) { }
    }
    private sealed class Isolated : Recording, IUiIsolatedPaint
    { public bool CanIsolatePaint => true; public void IsolatePaint(Action paint) => paint(); }
}
