using UnityEngine;
using Verse;
using System;
using System.Collections;
using System.Reflection;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;

internal static class KernelImagePlacementTests
{
    public static int RunAll()
    {
        Event? previous = Event.current;
        try
        {
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
            Event.current = new Event { type = EventType.Repaint };
            GeometryAndMeasure();
            RenderTargetAndReplacement();
            Background();
            BackgroundContentAndPopup();
            BackgroundUpdates();
            BackgroundGetterFailure();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("  FAIL: IMAGE " + error);
            return 1;
        }
        finally { Event.current = previous; }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("  ok: IMAGE " + label);
    }
    private static IList Recorded(string name) => (IList)typeof(GUI).GetField(name, BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
    private static void Clear() => typeof(GUI).GetMethod("ClearDrawTextureCalls")!.Invoke(null, null);
    private static UiLayoutManifest Page(string attributes) => UiLayoutManifest.Parse(
        "<UiPage Schema=\"2\" Source=\"images\"><Column Id=\"column\" Padding=\"0\">"
        + "<Widget Id=\"image\" Kind=\"display/image\" Bind=\"picture\" " + attributes + "/></Column></UiPage>");
    private static readonly VerseUiExecution imageExecution=new();
    private static UiHost Host(UiLayoutManifest page,UiBindings bindings)
    {var host=new UiHost("images",page,bindings,new UiTheme(),new Metrics(),new Translation());host.Session.SetFrame(imageExecution.CaptureFrame(),imageExecution);return host;}

    private static void GeometryAndMeasure()
    {
        var texture = new Texture2D(200,100);
        var bindings = new UiBindings();
        UiImage picture=imageExecution.Images.Add(texture);bindings.BindReadOnly("picture",()=>picture);
        var definition = new UiElementSpec("image", "display/image", new System.Collections.Generic.Dictionary<string,string>
        {
            ["Bind"] = "picture", ["Fit"] = "Stretch", ["Height"] = "50"
        });
        using (var program = Host(UiLayoutManifest.Create("program-images", new[] { definition }), bindings))
        {
            Clear();
            NativeFixture.DrawFrame(            program,new UiLayoutRect(0,0,80,100));
            UiLayoutRect direct = NativeFixture.Record<UiLayoutRect>(Recorded("DrawTextureRects")[0]!);
            Check(direct.Width == 80 && direct.Height == 50, "ordinary C# definitions use image options without custom wrapper");
        }
        using var natural = Host(Page(""), bindings);
        float oldHeight = natural.MeasureAndArrange(new UiLayoutPoint(80,200)).RectById["image"].Height;
        Check(oldHeight == 100 + new UiTheme().Geometry.Padding * 2, "unconfigured natural source height plus padding remains");
        using var aspect = Host(Page("AspectHeight=\"true\""), bindings);
        Check(aspect.MeasureAndArrange(new UiLayoutPoint(80,200)).RectById["image"].Height == 40, "explicit aspect measure uses allocated width");
        using var explicitHeight = Host(Page("AspectHeight=\"true\" Height=\"23\""), bindings);
        Check(explicitHeight.MeasureAndArrange(new UiLayoutPoint(80,200)).RectById["image"].Height == 23, "explicit height takes precedence");
        using var contained = Host(Page("Height=\"100\" ImageAlignY=\"Bottom\""), bindings);
        Clear();
        NativeFixture.DrawFrame(        contained,new UiLayoutRect(0,0,100,150));
        UiLayoutRect fit = NativeFixture.Record<UiLayoutRect>(Recorded("DrawTextureRects")[0]!);
        Check(fit.Width == 100 && fit.Height == 50 && fit.Y == 50, "real image wrapper passes finite bottom aligned fit to native outlet");
        using var cover = Host(Page("Height=\"100\" Fit=\"Cover\" ImageAlignX=\"Right\""), bindings);
        int depth = (int)typeof(GUI).GetField("GroupDepth")!.GetValue(null)!;
        Clear();
        NativeFixture.DrawFrame(        cover,new UiLayoutRect(0,0,100,150));
        UiLayoutRect crop = NativeFixture.Record<UiLayoutRect>(Recorded("DrawTextureRects")[0]!);
        Check(crop.X == -100 && crop.Width == 200 && crop.Height == 100, "cover right alignment uses oversized local rect inside slot clip");
        Check((int)typeof(GUI).GetField("GroupDepth")!.GetValue(null)! == depth, "cover closes its clip group");
        UiColour previousTint = VerseUiExecution.Colour(GUI.color);
        FieldInfo failure = typeof(GUI).GetField("DrawTextureFailure")!;
        bool threw = false;
        failure.SetValue(null, new InvalidOperationException("pure paint failure"));
        try
        {
            UiThemeDraw.Image(imageExecution,new UiLayoutRect(0,0,100,100), picture, new UiColour(.2f,.3f,.4f), new UiImageOptions(UiImageFit.Cover));
        }
        catch (InvalidOperationException) { threw = true; }
        finally { failure.SetValue(null, null); }
        Check(threw && (int)typeof(GUI).GetField("GroupDepth")!.GetValue(null)! == depth
            && GUI.color.r == previousTint.Red && GUI.color.g == previousTint.Green && GUI.color.b == previousTint.Blue,
            "cover draw failure propagates with balanced group and restored tint");        bool bad = false;
        try { using var rejected = Host(Page("Fit=\"Tile\""), bindings); } catch (Exception) { bad = true; }
        Check(bad, "unsupported image fit refused at authoring validation");
    }

    private static void RenderTargetAndReplacement()
    {
        Texture? current = new RenderTexture(120,60,0);
        var bindings = new UiBindings();
        UiImage picture=imageExecution.Images.Add(current);bindings.BindReadOnly("picture",()=>picture);
        using var host = Host(Page("Height=\"80\""), bindings);
        Clear();
        NativeFixture.DrawFrame(        host,new UiLayoutRect(0,0,100,100));
        Check(Recorded("DrawTextureTextures").Count == 1 && ReferenceEquals(Recorded("DrawTextureTextures")[0], current),
            "supplier RenderTexture reaches existing Texture outlet without conversion or ownership");
        current = new Texture2D(40,80);imageExecution.Images.Release(picture);picture=imageExecution.Images.Add(current);
        Clear();
        NativeFixture.DrawFrame(        host,new UiLayoutRect(0,0,100,100));
        Check(ReferenceEquals(Recorded("DrawTextureTextures")[0], current), "replacement read on next draw without resource cache");
        current = null;imageExecution.Images.Release(picture);picture=default;
        Clear();
        NativeFixture.DrawFrame(        host,new UiLayoutRect(0,0,100,100));
        Check(Recorded("DrawTextureTextures").Count == 0, "known null source draws placeholder not stale image");
        current = new Texture2D(0,80);picture=imageExecution.Images.Add(current);
        Clear();
        NativeFixture.DrawFrame(        host,new UiLayoutRect(0,0,100,100));
        Check(Recorded("DrawTextureTextures").Count == 0, "invalid source dimensions do not reach native draw");
    }

    private static void Background()
    {
        Texture? image = new Texture2D(200,100);
        var bindings = new UiBindings();
        UiImage background=default;bindings.BindReadOnly("back",()=>background);
        var page = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"background\" BackgroundBind=\"back\"/>");
        var programBackground = new UiWindowBackground("back");
        var programPage = UiLayoutManifest.CreateWithBackground("program-background", Array.Empty<UiElementSpec>(), programBackground);
        Check(ReferenceEquals(programPage.Background, programBackground), "ordinary C# background definition shares immutable configuration without custom host");
        Check(page.Background != null && page.Background.Options.Fit == UiImageFit.Contain, "declarative empty page retains approved contain background");
        var window = new UiPageWindow(new UiWindowKey("image-test", "page", "one"), page, bindings,
            new UiTheme(), new Translation(), "title", "close", _ => "notice", new Metrics());
        window.windowRect = VerseUiExecution.Rect(new UiLayoutRect(0,0,300,300));
        background=window.Images.Add(image);
        Clear();
        Recorded("PaintTrace").Clear();
        window.WindowOnGUI();
        Check(Recorded("DrawTextureTextures").Count == 1 && ReferenceEquals(Recorded("DrawTextureTextures")[0], image),
            "standard sealed page window draws explicit background through same outlet");
        UiLayoutRect actual = NativeFixture.Record<UiLayoutRect>(Recorded("DrawTextureRects")[0]!);
        Check(actual.Width == 300 && actual.Height == 150 && actual.Y == 75, "background spans whole shell, centered with exposed base colour");
        IList trace = Recorded("PaintTrace");
        int imageIndex = trace.IndexOf("image");
        Check(imageIndex > 0 && (string)trace[0]! == "solid" && trace.IndexOf("label:title") > imageIndex
            && trace.IndexOf("label:close") > imageIndex, "actual shell paint order is base then image then retained title and close chrome");        var peer = new UiPageWindow(new UiWindowKey("image-test", "page", "two"), UiLayoutManifest.Create("plain", Array.Empty<UiElementSpec>()),
            bindings, new UiTheme(), new Translation(), "title", "close", _ => "notice", new Metrics());
        peer.windowRect = window.windowRect;
        Clear();
        peer.WindowOnGUI();
        Check(Recorded("DrawTextureTextures").Count == 0, "unconfigured independent window retains original shell");
        window.Background = null;

        Clear();
        Recorded("PaintTrace").Clear();
        window.WindowOnGUI();
        Check(Recorded("DrawTextureTextures").Count == 0, "instance unbind restores base without revealing definition background");
        window.Background = new UiWindowBackground("back", new UiImageOptions(UiImageFit.Cover));
        image = null;window.Images.Release(background);background=default;

        Clear();
        Recorded("PaintTrace").Clear();
        window.WindowOnGUI();
        Check(Recorded("DrawTextureTextures").Count == 0, "background null leaves original base and chrome");
        window.PreClose();
        peer.PreClose();
    }

    private static void BackgroundContentAndPopup()
    {
        var bindings = new UiBindings();
        UiImage background=default;bindings.BindReadOnly("back",()=>background);
        bindings.BindReadOnly("choice", () => "one");
        int calls = 0;
        bindings.BindCommand("act", () => calls++);
        var page = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"layered-shell\" BackgroundBind=\"back\"><Column Id=\"body\" Padding=\"0\"><Widget Id=\"button\" Kind=\"input/button\" Text=\"content\" ActionBind=\"act\" Height=\"30\"/><Widget Id=\"menu\" Kind=\"input/dropdown\" Bind=\"choice\"   Height=\"30\"><Choice Value=\"one\" Text=\"one\" HelpRef=\"one\"/><Choice Value=\"two\" Text=\"two\" HelpRef=\"two\"/></Widget></Column></UiPage>");
        var window = new UiPageWindow(new UiWindowKey("image-test", "content", "one"), page, bindings,
            new UiTheme(), new Translation(), "title", "close", _ => "notice", new Metrics());
        background=window.Images.Add(new Texture2D(200,100));
        window.windowRect = VerseUiExecution.Rect(new UiLayoutRect(0,0,300,300));
        Event.current = new Event { type = EventType.Repaint };
        window.WindowOnGUI();
        foreach (EventType type in new[] { EventType.MouseDown, EventType.MouseUp })
        {
            Event.current = new Event { type = type, button = 0, mousePosition = VerseUiExecution.Point(new UiLayoutPoint(40,65)) };
            window.WindowOnGUI();
        }
        Check(calls == 1, "whole window background introduces no input owner over real content button");
        foreach (EventType type in new[] { EventType.MouseDown, EventType.MouseUp })
        {
            Event.current = new Event { type = type, button = 0, mousePosition = VerseUiExecution.Point(new UiLayoutPoint(40,105)) };
            window.WindowOnGUI();
        }
        Check(window.Session!.OpenPopupId != null, "background preserves real dropdown popup opening");
        Recorded("PaintTrace").Clear();
        Event.current = new Event { type = EventType.Repaint };
        window.WindowOnGUI();
        IList trace = Recorded("PaintTrace");
        Check(trace.IndexOf("label:content") > trace.IndexOf("image") && trace.IndexOf("label:two") > trace.IndexOf("label:content"),
            "real shell paints background below content and actual popup option");
        Check(window.Session.Interactions.CaptureOwner == null, "background never captures while popup paints");
        window.PreClose();
        Check(window.Session == null, "closing background window releases normal host/session without a texture owner");
    }
    private static void BackgroundUpdates()
    {
        var first = new Texture2D(200,100);
        var second = new Texture2D(100,200);
        var bindings = new UiBindings();
        UiImage firstToken=default,secondToken=default;bindings.BindReadOnly("first",()=>firstToken);
        bindings.BindReadOnly("second",()=>secondToken);
        UiLayoutManifest Definition(string key, string fit = "Contain") => UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"background-update\" BackgroundBind=\"" + key + "\" BackgroundFit=\"" + fit + "\"/>");
        var window = new UiPageWindow(new UiWindowKey("image-test", "update", "one"), Definition("first"), bindings,
            new UiTheme(), new Translation(), "title", "close", _ => "notice", new Metrics());
        firstToken=window.Images.Add(first);secondToken=window.Images.Add(second);
        window.windowRect = VerseUiExecution.Rect(new UiLayoutRect(0,0,300,300));
        Event.current = new Event { type = EventType.Repaint };
        window.WindowOnGUI();
        window.PageHost!.UpdateDefinition(Definition("second", "Cover"));
        Clear();
        window.WindowOnGUI();
        Check(ReferenceEquals(Recorded("DrawTextureTextures")[0], second)
            && (NativeFixture.Record<UiLayoutRect>(Recorded("DrawTextureRects")[0]!)).Height == 600,
            "standard window follows committed definition background binding and fit");
        bindings.BindReadOnly("not-texture", () => "wrong type");
        UiLayoutManifest accepted = window.PageHost.Manifest;
        bool refused = false;
        try { window.PageHost.UpdateDefinition(Definition("not-texture")); }
        catch (InvalidOperationException) { refused = true; }
        Check(refused && ReferenceEquals(window.PageHost.Manifest, accepted), "invalid background binding is refused at UpdateDefinition prepare without replacing committed definition");
        BackgroundDocumentReload(window, bindings);
        window.PageHost.UpdateDefinition(UiLayoutManifest.Create("removed", Array.Empty<UiElementSpec>()));
        Clear();
        window.WindowOnGUI();
        Check(Recorded("DrawTextureTextures").Count == 0, "committed background removal restores standard shell");
        window.Background = new UiWindowBackground("first");
        window.PageHost.UpdateDefinition(Definition("second"));
        Clear();
        window.WindowOnGUI();
        Check(ReferenceEquals(Recorded("DrawTextureTextures")[0], first), "explicit instance override outranks committed definition");
        window.Background = null;
        window.PageHost.UpdateDefinition(Definition("second"));
        Clear();
        window.WindowOnGUI();
        Check(Recorded("DrawTextureTextures").Count == 0, "explicit instance null remains closed after definition replacement");
        window.PreClose();
    }
    private static void BackgroundDocumentReload(UiPageWindow window, UiBindings bindings)
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fl-background-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            string path = System.IO.Path.Combine(directory, "page.xml");
            string Definition(string binding) => "<UiPage Schema=\"2\" Source=\"background-update\" BackgroundBind=\"" + binding + "\"/>";
            System.IO.File.WriteAllText(path, Definition("second"));
            using var service = new UiDocumentService(autoWatch: false);
            Check(service.Add(new UiDocumentSource("background", UiDocumentKind.Layout, path), Definition("second")), "background document registers through ordinary service");
            Check(service.Attach(window.PageHost!, "background"), "standard window actual Host attaches to ordinary document reload");
            using var peer = Host(UiLayoutManifest.Parse(Definition("second")), bindings);
            service.Attach(peer, "background");
            UiLayoutManifest committed = window.PageHost!.Manifest;
            System.IO.File.WriteAllText(path, Definition("first"));
            service.DocumentCommitFaultOverride = host =>
            {
                if (!ReferenceEquals(host, peer)) return "";
                Check(ReferenceEquals(window.PageHost.Manifest, committed) && window.Background!.Bind == "second",
                    "later batch failure cannot expose staged background before seal");
                return "planted later background host commit failure";
            };
            UiReloadReport? rollback = service.Reload("background");
            service.DocumentCommitFaultOverride = null;
            Check(rollback != null && rollback.Rejected && ReferenceEquals(window.PageHost.Manifest, committed), "actual document batch rollback keeps prior background manifest");
            Clear();
            window.WindowOnGUI();
            Check(Recorded("DrawTextureTextures").Count == 1 && window.Background!.Bind == "second", "rolled-back candidate never reaches standard-window background paint");
            UiReloadReport? changed = service.Reload("background");
            Check(changed != null && changed.Accepted && window.Background!.Bind == "first", "accepted document reload updates standard window background without synchronization state");
            Clear();
            window.WindowOnGUI();
            Check(Recorded("DrawTextureTextures").Count == 1, "accepted reloaded background actually paints");
            System.IO.File.WriteAllText(path, Definition("not-texture"));
            UiReloadReport? bad = service.Reload("background");
            Check(bad != null && bad.Rejected && bad.Reason.Contains("UiImage") && window.Background!.Bind == "first",
                "document prepare reports invalid background type and keeps accepted background");
        }
        finally { System.IO.Directory.Delete(directory, recursive: true); }
    }
    private static void BackgroundGetterFailure()
    {
        int reads = 0;
        int notices = 0;
        UiColour previousColour = VerseUiExecution.Colour(GUI.color);
        Verse.GameFont previousFont = Verse.Text.Font;
        var failure = new InvalidOperationException("supplier background getter exploded");
        var bindings = new UiBindings();
        bindings.BindReadOnly<UiImage>("throwing", () =>
        {
            reads++;
            GUI.color = VerseUiExecution.Colour(new UiColour(.2f,.3f,.4f));
            Verse.Text.Font = previousFont == Verse.GameFont.Tiny ? Verse.GameFont.Medium : Verse.GameFont.Tiny;
            throw failure;
        });
        var page = UiLayoutManifest.CreateWithBackground("background-failure", Array.Empty<UiElementSpec>(), new UiWindowBackground("throwing"));
        var window = new UiPageWindow(new UiWindowKey("image-test", "failure", "one"), page, bindings,
            new UiTheme(), new Translation(), "title", "close", _ => { notices++; return "notice"; }, new Metrics());
        window.windowRect = VerseUiExecution.Rect(new UiLayoutRect(0,0,300,300));
        try
        {
            Event.current = new Event { type = EventType.Repaint };
            Recorded("PaintTrace").Clear();
            window.WindowOnGUI();
            object? recorded = typeof(UiWindowHost).GetProperty("LastFailure", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
            Check(ReferenceEquals(recorded, failure), "supplier exception is retained as actual shell failure, not silent missing image");
            Check(notices == 0 && window.Session == null, "background failure defers notice and leaves no active page in the failed pass");
            Check(GUI.color.r == previousColour.Red && GUI.color.g == previousColour.Green && GUI.color.b == previousColour.Blue
                && Verse.Text.Font == previousFont, "supplier failure restores font and colour before continuing chrome");
            Check(Recorded("PaintTrace").IndexOf("label:close") >= 0, "background failure preserves close chrome");
            window.WindowOnGUI();
            Check(reads == 1 && notices == 1, "next pass displays terminal notice without retrying supplier getter");
            window.PreClose();
            Check(window.Session == null && reads == 1, "failed background window closes without resource retry or lingering host");
        }
        finally
        {
            GUI.color = VerseUiExecution.Colour(previousColour);
            Verse.Text.Font = previousFont;
            window.PreClose();
        }
    }
    private sealed class Metrics : ITextMetrics
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
