using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Greenfield core widget smoke tests: registration, typed bindings, session popup and responsive
/// mode-row. These are stub-harness evidence only; they do not claim in-game UI verification.
/// </summary>
internal static class KernelCoreWidgetTests
{
    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Kernel core widgets measure/draw", VerifyCoreWidgetsMeasureDraw);
        failures += Run("Kernel dropdown opens session popup", VerifyDropdownOpensPopup);
        return failures;
    }

    private static int Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
            return 1;
        }
    }

    private static void VerifyCoreWidgetsMeasureDraw()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Stack Id=\"root\" Gap=\"8\" Padding=\"8\">"
            + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Bind=\"BannerText\" />"
            + "<Widget Id=\"mode\" Kind=\"input/mode-row\""
            + " Title1=\"A\" Value1=\"A\" Title2=\"B\" Value2=\"B\" />"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "<Widget Id=\"volume\" Kind=\"input/stepper-slider\" Min=\"0\" Max=\"1\" />"
            + "<Widget Id=\"chart\" Kind=\"chart/line\" Points=\"0,0;1,1\" />"
            + "</Stack>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string banner = "hello";
        string mode = "A";
        string dropdown = "x";
        float volume = 0.5f;
        bindings.BindReadOnly("BannerText", () => banner);
        bindings.BindValue("mode", () => mode, v => mode = v);
        bindings.BindValue("dropdown", () => dropdown, v => dropdown = v);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });
        bindings.BindValue("volume", () => volume, v => volume = v);
        // chart/line validates its typed points binding by Id (Bind falls back to Id).
        bindings.BindReadOnly<IReadOnlyList<Vector2>>(
            "chart", () => new List<Vector2> { new(0f, 0f), new(1f, 1f) });

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(500f, 600f));
        if (!snapshot.RectById.ContainsKey("banner")
            || !snapshot.RectById.ContainsKey("mode")
            || !snapshot.RectById.ContainsKey("dropdown")
            || !snapshot.RectById.ContainsKey("volume")
            || !snapshot.RectById.ContainsKey("chart"))
        {
            throw new Exception("Missing one or more widget rects");
        }

        host.BeginFrame();
        host.Draw(new Rect(0f, 0f, 500f, 600f), snapshot);
        host.EndFrame();
    }

    private static void VerifyDropdownOpensPopup()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        string current = "x";
        bindings.BindValue("dropdown", () => current, v => current = v);
        bindings.BindOptions("Options", () => new List<string> { "x", "y" });

        using UiHost host = new("test", manifest, bindings, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 200f));
        Rect fieldRect = snapshot.RectById["dropdown"];

        try
        {
            UiNative.ButtonOverride = rect => Math.Abs(rect.x - fieldRect.x) < 0.01f
                && Math.Abs(rect.y - fieldRect.y) < 0.01f
                && Math.Abs(rect.width - fieldRect.width) < 0.01f
                && Math.Abs(rect.height - fieldRect.height) < 0.01f;

            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 300f, 200f), snapshot);
            host.EndFrame();

            if (!host.Session.IsPopupOpen("dropdown"))
            {
                throw new Exception("Dropdown did not open a session popup");
            }
        }
        finally
        {
            UiNative.ButtonOverride = null;
        }
    }

    private sealed class StubMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            return font switch
            {
                UiFont.Tiny => 16f,
                UiFont.Small => 24f,
                _ => 32f
            };
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
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
