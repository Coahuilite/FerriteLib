using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelLayerDispatchTests
{
    public static int RunAll()
    {
        Event? previous = Event.current;
        try
        {
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
            UiNative.DebugMousePositionEnabled = false;
            UiNative.ButtonOverride = null;
            GUIUtility.hotControl = 0;
            int large = 0;
            int small = 0;
            var bindings = new UiBindings();
            bindings.BindCommand("large", () => large++);
            bindings.BindCommand("lock", () => small++);
            using var host = new UiHost("layers", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"layers\"><Overlay Id=\"stack\" Height=\"100\">"
                + "<Widget Id=\"large\" Kind=\"input/button\" ActionBind=\"large\" Text=\"Body\" Width=\"160\" Height=\"80\" AlignX=\"Left\" AlignY=\"Top\"/>"
                + "<Widget Id=\"lock\" Kind=\"input/button\" ActionBind=\"lock\" Text=\"Lock\" Width=\"30\" Height=\"30\" AlignX=\"Left\" AlignY=\"Top\" OffsetX=\"100\" OffsetY=\"10\"/>"
                + "</Overlay></UiPage>"), bindings, new UiTheme(), new Metrics(), new Translation());
            Event.current = new Event { type = EventType.Repaint };
            host.DrawFrame(new Rect(0,0,200,100));
            Event.current = new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(115,25) };
            host.DrawFrame(new Rect(0,0,200,100));
            string owner = host.Session.Interactions.CaptureOwner?.Node ?? "none";
            Event.current = new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(115,25) };
            host.DrawFrame(new Rect(0,0,200,100));
            Console.WriteLine("  observed: LAYER fixed baseline large=" + large + " lock=" + small + " press-owner=" + owner);
            if (large != 0 || small != 1) throw new Exception("top small lock must receive the gesture, not earlier large button");
            Console.WriteLine("  ok: LAYER real host final paint order gives the small upper target one activation");
            EligibilityAndDecoration();
            CaptureAndRemoval();
            CustomGeometry();
            ClipAndScroll();
            EditorCoverage();
            CompositeEligibility();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("  FAIL: LAYER " + error);
            return 1;
        }
        finally
        {
            Event.current = previous;
            GUIUtility.hotControl = 0;
        }
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("  ok: LAYER " + name);
    }
    private const string Lock = "<Widget Id=\"lock\" Kind=\"input/button\" ActionBind=\"lock\" Text=\"Lock\" Width=\"30\" Height=\"30\" AlignX=\"Left\" AlignY=\"Top\" OffsetX=\"100\" OffsetY=\"10\"/>";
    private static UiLayoutManifest Page(string upper) => UiLayoutManifest.Parse(
        "<UiPage Schema=\"2\" Source=\"layers-more\"><Overlay Id=\"stack\" Height=\"100\">"
        + "<Widget Id=\"large\" Kind=\"input/button\" ActionBind=\"large\" Text=\"Body\" Width=\"160\" Height=\"80\" AlignX=\"Left\" AlignY=\"Top\"/>" + upper + "</Overlay></UiPage>");
    private static UiHost Host(UiBindings bindings, string upper) => new("layers-more", Page(upper), bindings, new UiTheme(), new Metrics(), new Translation());
    private static void Frame(UiHost host, EventType type, float x = 115, float y = 25)
    {
        Event.current = new Event { type = type, button = 0, mousePosition = new Vector2(x,y) };
        host.DrawFrame(new Rect(0,0,200,100));
    }
    private static void EligibilityAndDecoration()
    {
        int large = 0;
        int small = 0;
        bool enabled = false;
        var bindings = new UiBindings();
        bindings.BindCommand("large", () => large++);
        bindings.BindCommand("lock", () => small++, () => enabled);
        using (var host = Host(bindings, Lock))
        {
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 1 && small == 0, "disabled upper target passes through even on first input frame");
            enabled = true;
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 1 && small == 1, "live eligibility change takes effect without changing declaration order");
        }
        const string decoration = "<Widget Id=\"decoration\" Kind=\"display/image\" Bind=\"picture\" Width=\"30\" Height=\"30\" AlignX=\"Left\" AlignY=\"Top\" OffsetX=\"100\" OffsetY=\"10\"/>";
        bindings.BindReadOnly<Texture2D>("picture", () => null!);
        using (var host = Host(bindings, decoration))
        {
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 2 && small == 1, "painted missing-image decoration is not an opaque input layer");
        }
    }
    private static void CaptureAndRemoval()
    {
        int large = 0;
        int small = 0;
        bool enabled = true;
        var bindings = new UiBindings();
        bindings.BindCommand("large", () => large++);
        bindings.BindCommand("lock", () => small++, () => enabled);
        using var host = Host(bindings, Lock);
        Frame(host, EventType.MouseDown);
        Check(host.Session.Interactions.CaptureOwner?.Node == host.Session.GetNodeByElementId("lock")!.Id.Key, "upper target owns the real capture");
        Frame(host, EventType.MouseUp, 20,60);
        Check(large == 0 && small == 0 && !host.Session.Interactions.CaptureOwner.HasValue, "release outside owner cancels, never transfers to underlying large button");
        Frame(host, EventType.MouseDown);
        enabled = false;
        Frame(host, EventType.MouseUp);
        Check(large == 0 && small == 0 && !host.Session.Interactions.CaptureOwner.HasValue, "lost eligibility cancels owned release without a lower replay");
        enabled = true;
        Frame(host, EventType.MouseDown);
        host.UpdateDefinition(Page(""));
        Frame(host, EventType.MouseUp);
        Check(large == 0 && small == 0 && !host.Session.Interactions.CaptureOwner.HasValue, "removing upper capture target cannot replay its release below");
        Frame(host, EventType.MouseDown);
        host.Dispose();
        Check(!host.Session.Interactions.CaptureOwner.HasValue && GUIUtility.hotControl == 0, "closing host releases its capture without transferring it");
    }
    private static void CustomGeometry()
    {
        UiWidgetRegistry.Register("layers-more", "test/precise", () => new Precise(), new[] { "Id", "Kind", "Width", "Height" });
        int large = 0;
        int custom = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("large", () => large++);
        bindings.BindCommand("custom", () => custom++);
        const string upper = "<Widget Id=\"custom\" Kind=\"test/precise\" Width=\"160\" Height=\"80\" AlignX=\"Left\" AlignY=\"Top\"/>";
        using var host = Host(bindings, upper);
        Frame(host, EventType.MouseDown);
        Frame(host, EventType.MouseUp);
        Check(large == 0 && custom == 1, "custom actual private region uses the same topmost core on first input frame");
        Frame(host, EventType.MouseDown, 20,60);
        Frame(host, EventType.MouseUp, 20,60);
        Check(large == 1 && custom == 1, "custom unused outer rectangle is pass-through, not guessed opaque geometry");
    }
    private static void ClipAndScroll()
    {
        int large = 0;
        int small = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("large", () => large++);
        bindings.BindCommand("lock", () => small++);
        string clipped = "<Clip Id=\"clip\" Width=\"105\" Height=\"80\" Padding=\"0\" AlignX=\"Left\" AlignY=\"Top\"><Overlay Height=\"80\" Padding=\"0\">" + Lock + "</Overlay></Clip>";
        using (var host = Host(bindings, clipped))
        {
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 1 && small == 0, "upper region outside current nested clip cannot cover lower content");
            Frame(host, EventType.MouseDown,108,25);
            Frame(host, EventType.MouseUp,108,25);
            Check(large == 1 && small == 1, "visible clipped fragment remains the actual upper input target");
        }
        string scrolled = "<Scroll Id=\"scroll\" Width=\"140\" Height=\"60\" Padding=\"0\" AlignX=\"Left\" AlignY=\"Top\"><Overlay Height=\"150\" Padding=\"0\">"
            + Lock.Replace("OffsetY=\"10\"", "OffsetY=\"80\"") + "</Overlay></Scroll>";
        using (var host = Host(bindings, scrolled))
        {
            host.MeasureAndArrange(new Vector2(200,100));
            host.Session.SetScrollPosition(host.Session.GetNodeByElementId("scroll")!, new Vector2(0,70));
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 1 && small == 2, "current scroll transform participates before lower native input runs");
            host.Session.SetScrollPosition(host.Session.GetNodeByElementId("scroll")!, Vector2.zero);
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 2 && small == 2, "changed scroll offset cannot leave stale upper coverage");
        }
    }    private static void EditorCoverage()
    {
        int lockRequests = 0;
        int writes = 0;
        int nativeDraws = 0;
        string model = "base";
        var bindings = new UiBindings();
        bindings.BindCommand("lock", () => lockRequests++);
        bindings.BindValue("text", () => model, value => { model = value; writes++; });
        UiLayoutManifest EditorPage(int width) => UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"layer-editor\"><Overlay Height=\"100\" Padding=\"0\">"
            + "<Widget Id=\"editor\" Kind=\"input/text-field\" Bind=\"text\" Width=\"" + width + "\" Height=\"80\" AlignX=\"Left\" AlignY=\"Top\"/>"
            + Lock + "</Overlay></UiPage>");
        try
        {
            UiNative.TextFieldOverride = (_, text) => { nativeDraws++; return text; };
            using (var host = new UiHost("layer-editor", EditorPage(160), bindings, new UiTheme(), new Metrics(), new Translation()))
            {
                Frame(host, EventType.Repaint);
                Check(nativeDraws == 1, "covered editor still paints on repaint without processing pointer input");
                Frame(host, EventType.MouseDown);
                Frame(host, EventType.MouseUp);
                Check(nativeDraws == 1 && lockRequests == 1 && writes == 0, "native editor cannot steal a press covered by the upper control");
            }
            using (var host = new UiHost("layer-editor", EditorPage(80), bindings, new UiTheme(), new Metrics(), new Translation()))
            {
                UiNative.TextFieldOverride = (_, _) => "draft";
                Frame(host, EventType.MouseDown,20,20);
                Frame(host, EventType.MouseUp,20,20);
                UiNative.TextFieldOverride = (_, text) => text;
                Frame(host, EventType.MouseDown);
                Frame(host, EventType.MouseUp);
                Check(model == "draft" && writes == 1 && lockRequests == 2, "nonoverlapping later control retains ordinary outside-click edit publication");
            }
        }
        finally { UiNative.TextFieldOverride = null; }
    }    private static void CompositeEligibility()
    {
        int large = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("large", () => large++);
        bindings.BindReadOnly("choice", () => "one");
        string upper = "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" Bind=\"choice\" Option1=\"one\" Width=\"30\" Height=\"30\" AlignX=\"Left\" AlignY=\"Top\" OffsetX=\"100\" OffsetY=\"10\"/>";
        using (var host = Host(bindings, upper))
        {
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 0 && host.Session.OpenPopupId != null, "readonly Dropdown retains its legal inspection trigger and covers the lower operation");
        }
        bool enabled = false;
        int expanded = 0;
        bindings.BindReadOnly<System.Collections.Generic.IReadOnlyList<FerriteLib.UiKit.Kernel.Widgets.UiTreeRow>>("rows", () => new[] { new FerriteLib.UiKit.Kernel.Widgets.UiTreeRow("row",0,"Row",expandable:true) });
        bindings.BindAction<string>("expand", _ => expanded++);
        bindings.BindCommand("expand", () => { }, () => enabled);
        upper = "<Widget Id=\"tree\" Kind=\"container/tree\" Bind=\"rows\" DisclosureBind=\"expand\" Width=\"30\" AlignX=\"Left\" AlignY=\"Top\" OffsetX=\"100\" OffsetY=\"10\"/>";
        using (var host = Host(bindings, upper))
        {
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 1 && expanded == 0, "gated independent Tree part is not an opaque input layer");
            enabled = true;
            Frame(host, EventType.MouseDown);
            Frame(host, EventType.MouseUp);
            Check(large == 1 && expanded == 1, "eligible Tree part uses its actual shared marker geometry");
        }
    }    private sealed class Precise : IUiWidget, IUiPointerLayout
    {
        public string Kind => "test/precise";
        public void Configure(UiElementSpec spec) { }
        public void Validate(IUiBindings bindings, string path) => bindings.ValidateCommand("custom", path);
        public float Measure(UiWidgetContext ctx) => 80;
        private static Rect Region(float x, float y) => new(x + 100, y + 10, 30,30);
        public System.Collections.Generic.IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
        {
            Rect region = Region(bounds.X,bounds.Y);
            yield return new UiPointerRegion(new UiLayoutRect(region.x,region.y,region.width,region.height));
        }
        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            UiInteractionResult result = UiNative.Activation(Region(rect.x,rect.y), ctx, "custom");
            UiInteractionRequests.Activation(UiNative.InteractionTarget(ctx,"custom"), result, () => ctx.Bindings.Invoke("custom"));
        }
    }    private sealed class Metrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width) => 16;
        public float MeasureWidth(string text, UiFont font) => text.Length * 7;
    }
    private sealed class Translation : IUiTranslation
    {
        public int TranslationRevision => 0;
        public string Translate(string key) => key;
    }
}
