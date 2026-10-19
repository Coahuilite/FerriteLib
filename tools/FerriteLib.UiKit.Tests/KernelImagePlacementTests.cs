using System;
using System.Collections;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

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
    private static UiHost Host(UiLayoutManifest page, UiBindings bindings) => new("images", page, bindings, new UiTheme(), new Metrics(), new Translation());

    private static void GeometryAndMeasure()
    {
        var texture = new Texture2D(200,100);
        var bindings = new UiBindings();
        bindings.BindReadOnly("picture", () => texture);
        var definition = new UiElementSpec("image", "display/image", new System.Collections.Generic.Dictionary<string,string>
        {
            ["Bind"] = "picture", ["Fit"] = "Stretch", ["Height"] = "50"
        });
        using (var program = Host(UiLayoutManifest.Create("program-images", new[] { definition }), bindings))
        {
            Clear();
            program.DrawFrame(new Rect(0,0,80,100));
            Rect direct = (Rect)Recorded("DrawTextureRects")[0]!;
            Check(direct.width == 80 && direct.height == 50, "ordinary C# definitions use image options without custom wrapper");
        }
        using var natural = Host(Page(""), bindings);
        float oldHeight = natural.MeasureAndArrange(new Vector2(80,200)).RectById["image"].height;
        Check(oldHeight == 100 + new UiTheme().Geometry.Padding * 2, "unconfigured natural source height plus padding remains");
        using var aspect = Host(Page("AspectHeight=\"true\""), bindings);
        Check(aspect.MeasureAndArrange(new Vector2(80,200)).RectById["image"].height == 40, "explicit aspect measure uses allocated width");
        using var explicitHeight = Host(Page("AspectHeight=\"true\" Height=\"23\""), bindings);
        Check(explicitHeight.MeasureAndArrange(new Vector2(80,200)).RectById["image"].height == 23, "explicit height takes precedence");
        using var contained = Host(Page("Height=\"100\" ImageAlignY=\"Bottom\""), bindings);
        Clear();
        contained.DrawFrame(new Rect(0,0,100,150));
        Rect fit = (Rect)Recorded("DrawTextureRects")[0]!;
        Check(fit.width == 100 && fit.height == 50 && fit.y == 50, "real image wrapper passes finite bottom aligned fit to native outlet");
        using var cover = Host(Page("Height=\"100\" Fit=\"Cover\" ImageAlignX=\"Right\""), bindings);
        int depth = (int)typeof(GUI).GetField("GroupDepth")!.GetValue(null)!;
        Clear();
        cover.DrawFrame(new Rect(0,0,100,150));
        Rect crop = (Rect)Recorded("DrawTextureRects")[0]!;
        Check(crop.x == -100 && crop.width == 200 && crop.height == 100, "cover right alignment uses oversized local rect inside slot clip");
        Check((int)typeof(GUI).GetField("GroupDepth")!.GetValue(null)! == depth, "cover closes its clip group");
        Color previousTint = GUI.color;
        FieldInfo failure = typeof(GUI).GetField("DrawTextureFailure")!;
        bool threw = false;
        failure.SetValue(null, new InvalidOperationException("pure paint failure"));
        try
        {
            UiThemeDraw.Image(new Rect(0,0,100,100), texture, new Color(.2f,.3f,.4f), new UiImageOptions(UiImageFit.Cover));
        }
        catch (InvalidOperationException) { threw = true; }
        finally { failure.SetValue(null, null); }
        Check(threw && (int)typeof(GUI).GetField("GroupDepth")!.GetValue(null)! == depth
            && GUI.color.r == previousTint.r && GUI.color.g == previousTint.g && GUI.color.b == previousTint.b,
            "cover draw failure propagates with balanced group and restored tint");        bool bad = false;
        try { using var rejected = Host(Page("Fit=\"Tile\""), bindings); } catch (Exception) { bad = true; }
        Check(bad, "unsupported image fit refused at authoring validation");
    }

    private static void RenderTargetAndReplacement()
    {
        Texture? current = new RenderTexture(120,60,0);
        var bindings = new UiBindings();
        bindings.BindReadOnly<Texture?>("picture", () => current);
        using var host = Host(Page("Height=\"80\""), bindings);
        Clear();
        host.DrawFrame(new Rect(0,0,100,100));
        Check(Recorded("DrawTextureTextures").Count == 1 && ReferenceEquals(Recorded("DrawTextureTextures")[0], current),
            "supplier RenderTexture reaches existing Texture outlet without conversion or ownership");
        current = new Texture2D(40,80);
        Clear();
        host.DrawFrame(new Rect(0,0,100,100));
        Check(ReferenceEquals(Recorded("DrawTextureTextures")[0], current), "replacement read on next draw without resource cache");
        current = null;
        Clear();
        host.DrawFrame(new Rect(0,0,100,100));
        Check(Recorded("DrawTextureTextures").Count == 0, "known null source draws placeholder not stale image");
        current = new Texture2D(0,80);
        Clear();
        host.DrawFrame(new Rect(0,0,100,100));
        Check(Recorded("DrawTextureTextures").Count == 0, "invalid source dimensions do not reach native draw");
    }

    private static void Background()
    {
        Texture? image = new Texture2D(200,100);
        var bindings = new UiBindings();
        bindings.BindReadOnly<Texture?>("back", () => image);
        var page = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"background\" BackgroundBind=\"back\"/>");
        var programBackground = new UiWindowBackground("back");
        var programPage = UiLayoutManifest.CreateWithBackground("program-background", Array.Empty<UiElementSpec>(), programBackground);
        Check(ReferenceEquals(programPage.Background, programBackground), "ordinary C# background definition shares immutable configuration without custom host");
        Check(page.Background != null && page.Background.Options.Fit == UiImageFit.Contain, "declarative empty page retains approved contain background");
        var window = new UiPageWindow(new UiWindowKey("image-test", "page", "one"), page, bindings,
            new UiTheme(), new Translation(), "title", "close", _ => "notice", new Metrics());
        window.windowRect = new Rect(0,0,300,300);
        Clear();
        Recorded("PaintTrace").Clear();
        window.WindowOnGUI();
        Check(Recorded("DrawTextureTextures").Count == 1 && ReferenceEquals(Recorded("DrawTextureTextures")[0], image),
            "standard sealed page window draws explicit background through same outlet");
        Rect actual = (Rect)Recorded("DrawTextureRects")[0]!;
        Check(actual.width == 300 && actual.height == 150 && actual.y == 75, "background spans whole shell, centered with exposed base colour");
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
        image = null;
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
        bindings.BindReadOnly<Texture>("back", () => new Texture2D(200,100));
        bindings.BindReadOnly("choice", () => "one");
        int calls = 0;
        bindings.BindCommand("act", () => calls++);
        var page = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"layered-shell\" BackgroundBind=\"back\">"
            + "<Column Id=\"body\" Padding=\"0\"><Widget Id=\"button\" Kind=\"input/button\" Text=\"content\" ActionBind=\"act\" Height=\"30\"/>"
            + "<Widget Id=\"menu\" Kind=\"input/dropdown\" Bind=\"choice\" Option1=\"one\" Option2=\"two\" Height=\"30\"/></Column></UiPage>");
        var window = new UiPageWindow(new UiWindowKey("image-test", "content", "one"), page, bindings,
            new UiTheme(), new Translation(), "title", "close", _ => "notice", new Metrics());
        window.windowRect = new Rect(0,0,300,300);
        Event.current = new Event { type = EventType.Repaint };
        window.WindowOnGUI();
        foreach (EventType type in new[] { EventType.MouseDown, EventType.MouseUp })
        {
            Event.current = new Event { type = type, button = 0, mousePosition = new Vector2(40,65) };
            window.WindowOnGUI();
        }
        Check(calls == 1, "whole window background introduces no input owner over real content button");
        foreach (EventType type in new[] { EventType.MouseDown, EventType.MouseUp })
        {
            Event.current = new Event { type = type, button = 0, mousePosition = new Vector2(40,105) };
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

