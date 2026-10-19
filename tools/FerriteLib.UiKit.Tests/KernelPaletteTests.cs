using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

internal static class KernelPaletteTests
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("  ok: PALETTE " + name);
    }
    public static int RunAll()
    {
        Event? previous = Event.current;
        try
        {
            AuthoringAndIsolation();
            CaptureAndRepaint();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("  FAIL: PALETTE " + error);
            return 1;
        }
        finally
        {
            Event.current = previous;
            UiNative.DebugMousePositionEnabled = false;
            GUIUtility.hotControl = 0;
        }
    }
    private static void AuthoringAndIsolation()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        var theme = new UiTheme();
        theme.Palette[UiPaletteRole.SuccessForeground] = new UiColour(.1f,.2f,.3f);
        theme.Palette[UiPaletteRole.WarningSurface] = new UiColour(.3f,.2f,.1f);
        Check(theme.Styles.Resolve(UiStatusTone.Success).Text == theme[UiPaletteRole.SuccessForeground]
            && theme.Styles.Resolve(UiStatusTone.Active).Text != theme.Styles.Resolve(UiStatusTone.Success).Text,
            "ordinary program palette separates success and selection text without subclassing");
        Check(theme.Styles.Resolve(UiStatusTone.Warning).Fill != theme.Styles.Resolve(UiStatusTone.Danger).Fill,
            "warning override does not recolour danger");
        var bindings = new UiBindings();
        int requests = 0;
        bindings.BindCommand("go", () => requests++);
        string xml = "<UiPage Schema=\"2\" Source=\"palette-author\"><Stack Id=\"group\">"
            + "<Widget Id=\"first\" Kind=\"input/button\" ActionBind=\"go\" Text=\"A\" Palette.ControlSurface=\"#102030\"/>"
            + "<Widget Id=\"second\" Kind=\"input/button\" ActionBind=\"go\" Text=\"B\"/></Stack></UiPage>";
        using var host = new UiHost("palette-author", UiLayoutManifest.Parse(xml), bindings, theme, new Metrics(), new Translation());
        using var peer = new UiHost("palette-peer", UiLayoutManifest.Parse(xml), bindings, new UiTheme(), new Metrics(), new Translation());
        Event.current = new Event { type = EventType.Repaint };
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(200,100));
        host.DrawFrame(new Rect(0,0,200,100));
        UiNode first = host.Session.GetNodeByElementId("first")!;
        UiNode second = host.Session.GetNodeByElementId("second")!;
        Check(first.Properties.ResolvedColours["ControlSurface"].Value == new UiColour(16f/255,32f/255,48f/255)
            && second.Properties.ResolvedColours["ControlSurface"].Origin == UiPropertyOrigin.Fallback,
            "declarative local colour changes only its same-kind instance");
        theme.Palette[UiPaletteRole.ControlSurface] = new UiColour(.7f,.1f,.2f);
        host.DrawFrame(new Rect(0,0,200,100));
        Check(second.Properties.ResolvedColours["ControlSurface"].Origin == UiPropertyOrigin.Host
            && second.Properties.ResolvedColours["ControlSurface"].Value == theme.Palette[UiPaletteRole.ControlSurface]
            && ReferenceEquals(snapshot, host.MeasureAndArrange(new Vector2(200,100))) && requests == 0,
            "current host palette reaches untouched sibling without geometry or business requests");
        peer.DrawFrame(new Rect(0,0,200,100));
        Check(peer.Session.GetNodeByElementId("second")!.Properties.ResolvedColours["ControlSurface"].Value == UiPalette.Default(UiPaletteRole.ControlSurface),
            "another host remains isolated");
        UiPalette originalPalette = theme.Palette;
        UiTheme held = theme.Clone();
        theme.Palette[UiPaletteRole.Accent] = new UiColour(.1f,.7f,.2f);
        theme.RestoreFrom(held);
        Check(ReferenceEquals(originalPalette, theme.Palette), "theme restoration preserves ordinary author's palette reference");
        Color before = GUI.color;
        UiThemeDraw.StatusBadge(new Rect(0,0,50,28), "test", theme, UiStatusTone.Warning);
        Check(GUI.color == before, "visual helper restores ambient GUI colour");
    }
    private static void CaptureAndRepaint()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        var theme = new UiTheme();
        var bindings = new UiBindings();
        int requests = 0;
        bindings.BindCommand("go", () => requests++);
        using var host = new UiHost("palette-capture", UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"palette-capture\"><Widget Id=\"button\" Kind=\"input/button\" ActionBind=\"go\" Text=\"Go\" Height=\"28\"/></UiPage>"),
            bindings, theme, new Metrics(), new Translation());
        UiNative.DebugMousePositionEnabled = false;
        Event.current = new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(20,14) };
        host.DrawFrame(new Rect(0,0,100,50));
        UiInteractionTarget? owner = host.Session.Interactions.CaptureOwner;
        int? hot = host.Session.OwnedHotControl;
        Check(owner.HasValue && hot.HasValue && requests == 0, "real primary press holds capture before palette change");
        UiNode node = host.Session.GetNodeByElementId("button")!;
        UiLayoutSnapshot before = host.MeasureAndArrange(new Vector2(100,50));
        theme.Palette[UiPaletteRole.SelectedSurface] = new UiColour(.2f,.6f,.1f);
        Event.current = new Event { type = EventType.Repaint, mousePosition = new Vector2(20,14) };
        host.DrawFrame(new Rect(0,0,100,50));
        Check(host.Session.Interactions.CaptureOwner.HasValue && host.Session.OwnedHotControl == hot
            && ReferenceEquals(node, host.Session.GetNodeByElementId("button")) && requests == 0
            && ReferenceEquals(before, host.MeasureAndArrange(new Vector2(100,50))),
            "recolour preserves real capture, instance and measured snapshot without requests");
        Event.current = new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(20,14) };
        host.DrawFrame(new Rect(0,0,100,50));
        Check(requests == 1 && !host.Session.Interactions.CaptureOwner.HasValue, "held operation still releases exactly once after recolour");
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
