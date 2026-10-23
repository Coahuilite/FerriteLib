using System.Linq;
using UnityEngine;
using Verse;
using System;
using System.Collections;
using System.Reflection;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.Tests;
internal static class KernelCloseoutAppearanceTests
{
    private static IList List(string name) => (IList)typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); Console.WriteLine("  ok: CLOSEOUT-APPEARANCE " + name); }
    private static void Pump(UiHost host)
    { Event.current = new Event { type = EventType.Repaint }; NativeFixture.DrawFrame(host,new UiLayoutRect(0, 0, 400, 300)); }
    internal static int RunAll()
    {
        try { DefaultsAndSources(); IndependentVisual(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine("  FAIL: CLOSEOUT-APPEARANCE " + ex); return 1; }
        finally { Event.current = null; }
    }
    private static void DefaultsAndSources()
    {
        UiWidgetRegistry.Clear(); UiWidgetRegistry.InitializeCore(); var b = new UiBindings();
        b.BindValue("value", () => .5f, _ => { }); b.BindValue("pick", () => "A", _ => { });
        b.BindChoices("pick", ()=> (new[] { "A" }).Select(value=>new UiChoice<string>(value,value,value)).ToArray());
        var metrics = new Metrics();
        string xml = "<UiPage Schema=\"2\" Source=\"appearance\"><Styles Schema=\"1\"><Scheme Name=\"base\"><Metric Token=\"RowHeight\" Value=\"40\"/><Metric Token=\"Gap\" Value=\"9\"/><Metric Token=\"Font\" Value=\"Medium\"/></Scheme><Density Name=\"sparse\"><Metric Token=\"Gap\" Value=\"5\"/></Density></Styles>"
            + "<Column Id=\"parent\" Scheme=\"base\" Padding=\"0\" Gap=\"0\"><Widget Id=\"range\" Kind=\"input/slider\" Bind=\"value\" Label=\"Label\" Density=\"sparse\" Metric.Spacing=\"11\"/>"
            + "<Widget Id=\"pick\" Kind=\"input/dropdown\" Bind=\"pick\" OptionsBind=\"pick\" Appearance=\"selector\" Metric.Padding=\"12\" Metric.Hairline=\"3\"/></Column></UiPage>";
        using var host = new UiHost("appearance", UiLayoutManifest.Parse(xml), b, new UiTheme(), metrics, new Translation());
        Pump(host); UiNode parent = host.Session.GetNodeByElementId("parent")!, range = host.Session.GetNodeByElementId("range")!, pick = host.Session.GetNodeByElementId("pick")!;
        Check(range.Rect!.Value.Height == 40 && metrics.Font == UiFont.Medium && range.Properties.ResolvedMetrics["Gap"].Value == 5
            && range.Properties.ResolvedMetrics["RowHeight"].Origin == UiPropertyOrigin.Ancestor,
            "default slider consumes named font/row height while sparse Density overrides only its Gap");
        Check(range.Properties.ResolvedMetrics["Spacing"].Value == 11 && parent.Rect!.Value.Width == 400,
            "inline metric configures internal spacing without replacing parent's outer allocation");
        int text = -1; for (int i = 0; i < List("LabelTexts").Count; i++) if ((string)List("LabelTexts")[i]! == "A") text = i;
        UiLayoutRect outlet = NativeFixture.Record<UiLayoutRect>(List("LabelRects")[text]!);
        Check(outlet.X == pick.Rect!.Value.X + UiThemeDraw.SelectorAccentWidth + 12,
            "default selector's actual text outlet consumes explicit Padding12");
        bool edge = false; foreach (UiLayoutRect rect in NativeFixture.Records<UiLayoutRect>(List("DrawBoxSolidRects"))) if (rect.Width == pick.Rect.Value.Width && rect.Height == 3) edge = true;
        Check(edge, "default selector paints actual Hairline3 edge");
        foreach (string name in new[] { "Padding", "Spacing", "Gap", "RowHeight", "Hairline" })
        {
            float before = range.Properties.ResolvedMetrics[name].Value;
            range.Properties.Metric(name).Set(before); parent.Properties.Metric(name).Set(before + 2); Pump(host);
            Check(range.Properties.ResolvedMetrics[name].Value == before && range.Properties.ResolvedMetrics[name].Origin == UiPropertyOrigin.Instance,
                "explicit same-value declaration retains intent: " + name);
            range.Properties.Metric(name).Remove(); Pump(host);
            float expected = name == "Spacing" ? 11 : name == "Gap" ? 5 : before + 2;
            Check(range.Properties.ResolvedMetrics[name].Value == expected,
                "individual removal reveals current inline/named/ancestor value: " + name);
        }
        var snapshot = host.MeasureAndArrange(new UiLayoutPoint(400, 300)); long measures = range.LayoutMeasureCount;
        range.Properties.Palette(UiPaletteRole.Foreground).Set(new UiColour(1, 0, 0)); Pump(host);
        Check(ReferenceEquals(snapshot, host.MeasureAndArrange(new UiLayoutPoint(400, 300))) && range.LayoutMeasureCount == measures,
            "semantic colour is paint-only and preserves real measurement/snapshot");
        range.Properties.Palette(UiPaletteRole.Foreground).Remove(); parent.Properties.Font.Set(UiFont.Tiny); Pump(host);
        Check(range.Properties.ResolvedFont.Value == UiFont.Tiny && range.LayoutMeasureCount > measures,
            "font change reaches actual wrapper measurement, not just theme metadata");
        Console.WriteLine("CLOSEOUT-APPEARANCE-TRACE range=" + range.Rect + " font=" + range.Properties.ResolvedFont.Value + " measures=" + range.LayoutMeasureCount + " selector-outlet=" + outlet);
    }
    private static void IndependentVisual()
    {
        var theme = new UiTheme(); var metrics = new Metrics();
        foreach (string name in new[] { "Padding", "Spacing", "Gap", "RowHeight", "Hairline" })
        {
            theme.Metric(name).Set(17); var clone = theme.Clone();
            Check(Read(clone.Geometry, name) == 17, "independent visual clone retains explicit metric: " + name);
            theme.Geometry = new UiGeometry(8, 7, 6, 35, 2); theme.Metric(name).Remove();
            Check(Read(theme.Geometry, name) == Read(new UiGeometry(8, 7, 6, 35, 2), name), "independent metric removal reveals current base: " + name);
        }
        theme.DefaultFont = UiFont.Medium;
        UiSliderLayout layout = UiSliderLayout.Measure("Label", 100, theme.DefaultFont,
            new UiDensity(8, 7, 6, 35, 2), metrics);
        theme.Palette[UiPaletteRole.Foreground] = new UiColour(0, 1, 0);
        int first = List("LabelColors").Count; UiThemeDraw.Label(NativeFixture.Paint,new UiLayoutRect(0, 0, 100, layout.Height), "Independent", theme);
        Check(metrics.Font == UiFont.Medium && NativeFixture.Record<UiColour>(List("LabelColors")[first]!) == new UiColour(0, 1, 0),
            "standalone measure/draw consumes font and semantic palette without Host or page registration");
        theme.RemoveFontOverride(); theme.Palette.Remove(UiPaletteRole.Foreground);
        Check(theme.DefaultFont == UiFont.Small && theme.Palette[UiPaletteRole.Foreground] == UiPalette.Default(UiPaletteRole.Foreground),
            "standalone font and colour removal restore current remaining sources");
    }
    private static float Read(UiGeometry value, string name) => name switch
    { "Padding" => value.Padding, "Spacing" => value.Spacing, "Gap" => value.Gap, "RowHeight" => value.RowHeight, _ => value.Hairline };
    private sealed class Metrics : ITextMetrics
    { public UiFont Font; public float MeasureWidth(string text, UiFont font) { Font = font; return text.Length * (font == UiFont.Medium ? 12 : 8); } public float MeasureText(string text, UiFont font, float width) { Font = font; return font == UiFont.Medium ? 30 : 16; } }
    private sealed class Translation : IUiTranslation
    { public int TranslationRevision => 0; public string Translate(string key) => key; }
}
