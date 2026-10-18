using System;
using System.Collections.Generic;
using FerriteLib.UiKit;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;
internal static class KernelDescriptiveFactsTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("  ok: DESCRIBE " + message);
    }

    public static int RunAll()
    {
        try
        {
            DescriptionWithoutDrawing();
            FontAndGeometry();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("  FAIL: DESCRIBE " + ex);
            return 1;
        }
        finally
        {
            Event.current = null;
            UiNative.ButtonOverride = null;
            GUIUtility.hotControl = 0;
        }
    }

    private static void DescriptionWithoutDrawing()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using var session = new UiSession();
        float model = .6f;
        var bindings = new UiBindings();
        bindings.BindReadOnly("progress", () => model);
        bindings.BindReadOnly<Texture2D>("picture", () => null !);
        var ctx = new UiWidgetContext("description", session, new Metrics(), UiTheme.Vanilla, new Translation(), bindings, 200, "root");
        var kinds = new[]
        {
            WrappedTextWidget.Kind,
            SectionHeaderWidget.Kind,
            ChromeBannerWidget.Kind,
            EmptyStateWidget.Kind,
            ImageWidget.Kind,
            RuleWidget.Kind,
            ProgressWidget.Kind
        };
        var roles = new[]
        {
            UiDescriptionRole.Text,
            UiDescriptionRole.Heading,
            UiDescriptionRole.Status,
            UiDescriptionRole.Status,
            UiDescriptionRole.Image,
            UiDescriptionRole.Decoration,
            UiDescriptionRole.Progress
        };
        UiNative.ButtonOverride = _ => throw new Exception("descriptive input is invented");
        for (int i = 0; i < kinds.Length; i++)
        {
            IUiWidget widget = UiWidgetRegistry.Resolve("description", kinds[i]);
            var attributes = new Dictionary<string, string>();
            if (i <= 3)
                attributes[i == 1 ? "Title" : "Text"] = "actual text";
            if (i == 4)
            {
                attributes["Bind"] = "picture";
                attributes["Description"] = "An informative image";
            }

            if (i == 6)
            {
                attributes["Bind"] = "progress";
                attributes["Max"] = "2";
            }

            widget.Configure(new UiElementSpec("description", kinds[i], attributes));
            widget.Validate(bindings, "root");
            var descriptive = (IUiDescriptiveWidget)widget;
            UiDescription facts = descriptive.Describe(ctx);
            Check(facts.Role == roles[i] && session.ActiveEditNode == null && !session.Interactions.CaptureOwner.HasValue, "actual read-only role without a Draw or input state: " + kinds[i]);
            if (i <= 3)
                Check(facts.Text == "actual text", "text projection uses the exact presentation input: " + kinds[i]);
            if (i == 4)
            {
                Check(facts.Available == false && facts.Name == "An informative image", "missing meaningful image retains its declared description");
                attributes["Decorative"] = "true";
                widget.Configure(new UiElementSpec("description", kinds[i], attributes));
                Check(descriptive.Describe(ctx).Role == UiDescriptionRole.Decoration, "declared decorative image is not an operation or meaningful image");
            }

            if (i == 6)
            {
                Check(facts.Range?.Value == model && Math.Abs(facts.Range.Value.Fraction - .3f) < .0001f, "progress exposes actual model value separately from clamped fill");
                model = float.NaN;
                Check(descriptive.Describe(ctx).Range?.Finite == false, "nonfinite model is explicit unknown range, never a repaired value");
                attributes["Bind"] = "not-arrived";
                widget.Configure(new UiElementSpec("description", kinds[i], attributes));
                facts = descriptive.Describe(ctx);
                Check(!facts.Range.HasValue && facts.UnknownReason == "MissingBinding", "missing runtime value is unknown, not an invented business zero");
            }

            Event.current = new Event
            {
                type = EventType.MouseDown,
                mousePosition = new Vector2(20, 14),
                button = 0
            };
            GUIUtility.hotControl = 777;
            widget.Draw(new Rect(0, 0, 200, 60), ctx);
            Check(Event.current.type == EventType.MouseDown && GUIUtility.hotControl == 777, "descriptive paint neither consumes a press nor captures: " + kinds[i]);
            GUIUtility.hotControl = 0;
        }
    }

    private static void FontAndGeometry()
    {
        foreach (string container in new[]
        {
            "Row",
            "Column",
            "Stack",
            "Wrap",
            "Overlay",
            "Section",
            "Surface",
            "Scroll",
            "Clip"
        }

        )
        {
            UiWidgetRegistry.Clear();
            UiWidgetRegistry.InitializeCore();
            string alignment = container == "Overlay" ? " AlignX=\"Left\"" : "";
            var manifest = UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"description\"><" + container + " Id=\"parent\" Padding=\"0\" Gap=\"0\"><Widget Id=\"banner\" Kind=\"chrome/banner\" Text=\"ab\" Width=\"Auto\"" + alignment + " Height=\"28\" /></" + container + "></UiPage>");
            using var host = new UiHost("description", manifest, new UiBindings(), UiTheme.Vanilla, new Metrics(), new Translation());
            var available = new Vector2(400, 300);
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(available);
            UiNode parent = host.Session.GetNodeByElementId("parent")!;
            UiNode banner = host.Session.GetNodeByElementId("banner")!;
            Check(snapshot.RectById["banner"].width == 10 && banner.Properties.ResolvedFont.Value == UiFont.Tiny, "undeclared banner keeps Tiny fallback in actual Auto geometry: " + container);
            parent.Properties.Font.Set(UiFont.Medium);
            snapshot = host.MeasureAndArrange(available);
            Check(snapshot.RectById["banner"].width == 24 && banner.Properties.ResolvedFont.Origin == UiPropertyOrigin.Ancestor, "Auto geometry and inherited Medium share the child's effective font: " + container);
            banner.Properties.Font.Set(UiFont.Tiny);
            snapshot = host.MeasureAndArrange(available);
            Check(snapshot.RectById["banner"].width == 10, "explicit child Tiny wins: " + container);
            banner.Properties.Font.Remove();
            snapshot = host.MeasureAndArrange(available);
            Check(snapshot.RectById["banner"].width == 24, "removal follows current Medium without copying: " + container);
            parent.Properties.Font.Remove();
            snapshot = host.MeasureAndArrange(available);
            Check(snapshot.RectById["banner"].width == 10 && banner.Properties.ResolvedFont.Origin == UiPropertyOrigin.Fallback, "removing the environment restores the kind fallback: " + container);
        }
    }

    private sealed class Metrics : ITextMetrics
    {
        public float MeasureWidth(string text, UiFont font) => text.Length * (font == UiFont.Tiny ? 5 : font == UiFont.Medium ? 12 : 8);
        public float MeasureText(string text, UiFont font, float width) => Math.Max(1, (float)Math.Ceiling(MeasureWidth(text, font) / Math.Max(1, width))) * 16;
    }

    private sealed class Translation : IUiTranslation
    {
        public int TranslationRevision => 0;

        public string Translate(string key) => key;
    }
}
