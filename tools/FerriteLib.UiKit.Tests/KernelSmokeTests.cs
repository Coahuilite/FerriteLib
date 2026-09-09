using System;
using FerriteLib.UiKit.Kernel;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Greenfield kernel vertical-slice tests. These cover Schema=2 parsing, Host validation, session
/// isolation and the typed binding path without claiming any in-game UI verification.
/// </summary>
internal static class KernelSmokeTests
{
    public static int RunAll()
    {
        int failures = 0;
        failures += Run("Schema=2 manifest parse + duplicate id rejection", VerifyManifestParse);
        failures += Run("Host vertical slice creates snapshot", VerifyHostVerticalSlice);
        failures += Run("Host DrawFrame balances EndFrame after draw failure", VerifyDrawFrameClosesFrameOnFailure);
        failures += Run("Host rejects binding type mismatch", VerifyBindingTypeMismatch);
        failures += Run("Host session isolation and disposal", VerifySessionIsolation);
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

    private static void VerifyManifestParse()
    {
        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Stack Id=\"root\" Gap=\"4\" Padding=\"8\">"
            + "<Section Id=\"section\" TitleKey=\"Test.Title\">"
            + "<Widget Id=\"value\" Kind=\"input/stepper-slider\" Min=\"0\" Max=\"1\" Step=\"0.05\" />"
            + "</Section>"
            + "</Stack>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        if (manifest.SchemaVersion != "2") throw new Exception("SchemaVersion != 2");
        if (manifest.Source != "test") throw new Exception("Source != test");
        if (manifest.Roots.Count != 1) throw new Exception("Root count != 1");
        if (manifest.Roots[0].Kind != "Stack") throw new Exception("Root kind != Stack");

        try
        {
            UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"test\">"
                + "<Widget Id=\"a\" Kind=\"x\" />"
                + "<Widget Id=\"a\" Kind=\"x\" />"
                + "</UiPage>");
            throw new Exception("Duplicate Id was not rejected");
        }
        catch (FormatException)
        {
            // expected
        }
    }

    private static void VerifyHostVerticalSlice()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Stack Id=\"root\" Gap=\"8\" Padding=\"12\">"
            + "<Widget Id=\"global-volume\" Kind=\"input/stepper-slider\" Min=\"0\" Max=\"1\" Step=\"0.05\" />"
            + "</Stack>"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        float current = 0.5f;
        bindings.BindValue("global-volume", () => current, value => current = value);

        using UiHost host = new(
            "test",
            manifest,
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(400f, 600f));
        if (!snapshot.RectById.TryGetValue("global-volume", out Rect rect)) throw new Exception("Missing global-volume rect");
        if (rect.width <= 0f || rect.height <= 0f) throw new Exception("Invalid global-volume rect");

        host.BeginFrame();
        host.Draw(new Rect(0f, 0f, 400f, 600f), snapshot);
        host.EndFrame();
    }

    private static void VerifyDrawFrameClosesFrameOnFailure()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.Register(UiWidgetRegistry.CoreScope, "test/throws",
            () => new ThrowingWidget());

        UiLayoutManifest manifest = UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"test\"><Widget Id=\"throws\" Kind=\"test/throws\" /></UiPage>");
        var bindings = new UiBindings();
        using UiHost host = new(
            "test",
            manifest,
            bindings,
            UiTheme.DarkGold,
            new StubMetrics(),
            new StubTranslation());

        // The popup pass runs after the content pass and is NOT inside the per-element guard (item C
        // recovers element draws; a popup callback is the host's own work). Failing there still has to
        // leave the frame balanced, which is the invariant this test was written to hold.
        host.Session.RegisterPopupDraw(() => throw new InvalidOperationException("draw failure"));

        try
        {
            host.DrawFrame(new Rect(0f, 0f, 400f, 200f));
            throw new Exception("DrawFrame did not propagate the popup-pass failure");
        }
        catch (InvalidOperationException ex) when (ex.Message == "draw failure")
        {
            // expected
        }

        if (!host.Session.IsActive) throw new Exception("DrawFrame disposed the session");
        if (host.Session.PopupDrawActions.Count != 0)
        {
            throw new Exception("DrawFrame did not run EndFrame after the popup-pass failure");
        }
        host.Close();
    }

    private sealed class ThrowingWidget : IUiWidget
    {
        public string Kind => "test/throws";

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx) => 20f;

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            ctx.Session.RegisterPopupDraw(() => { });
            throw new InvalidOperationException("draw failure");
        }
    }

    private static void VerifyBindingTypeMismatch()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"bad\" Kind=\"input/stepper-slider\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings = new UiBindings();
        bindings.BindValue("bad", () => "not-a-float", _ => { });

        try
        {
            using UiHost host = new(
                "test",
                manifest,
                bindings,
                UiTheme.DarkGold,
                new StubMetrics(),
                new StubTranslation());
            throw new Exception("Binding type mismatch was not rejected");
        }
        catch (UiContractException ex)
        {
            if (ex.ElementId != "bad" || ex.Kind != "input/stepper-slider")
            {
                throw new Exception("UiContractException lacks element diagnostics: " + ex.Message);
            }
        }
    }

    private static void VerifySessionIsolation()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"a\" Kind=\"input/stepper-slider\" />"
            + "</UiPage>";

        UiLayoutManifest manifest = UiLayoutManifest.Parse(xml);
        var bindings1 = new UiBindings();
        bindings1.BindValue("a", () => 0.2f, _ => { });
        var bindings2 = new UiBindings();
        bindings2.BindValue("a", () => 0.8f, _ => { });

        UiHost host1 = new("test", manifest, bindings1, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());
        UiHost host2 = new("test", manifest, bindings2, UiTheme.DarkGold, new StubMetrics(), new StubTranslation());

        if (ReferenceEquals(host1.Session, host2.Session)) throw new Exception("Hosts share a session");
        host1.Session.SetScrollPosition("scroll", new Vector2(1f, 2f));
        if (host2.Session.ScrollPositions.ContainsKey("scroll")) throw new Exception("Session state leaked across hosts");

        host1.Close();
        if (host1.Session.IsActive) throw new Exception("Closed host session still active");
        if (!host2.Session.IsActive) throw new Exception("Other host session was disposed");

        host2.Close();
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
