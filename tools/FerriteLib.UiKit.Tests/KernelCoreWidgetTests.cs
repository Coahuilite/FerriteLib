using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
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
        failures += Run("Dropdown exact value wins over display-text fallback (A4)", VerifyDropdownValuePrecedence);
        failures += Run("Wrong-kind options registration is explained, not called missing (A5)", VerifyOptionsKindDiagnostic);
        failures += Run("Banner/empty-state bands are the text atom's band", VerifyCompositeBandsComeFromTheAtom);
        failures += Run("Composites draw text/font/rect through the one outlet", VerifyCompositeOutletTriple);
        failures += Run("Composite kinds keep their vocabulary and their manifests", VerifyCompositeVocabularyUnchanged);
        failures += Run("chrome/banner takes the role pair and keeps its ink (G5)", VerifyBannerRole);
        failures += Run("SelectedKey resolves the element to the active state (0.7.x)", VerifySelectedKey);
        failures += Run("A button command receives the payload its row declares (G2)", VerifyButtonPayload);
        failures += Run("Chrome=\"none\" paints no surface and Auto height measures the content (G3)", VerifyBareHitArea);
        failures += Run("A binding element-type mismatch is reported, not just thrown (FL-23)", VerifyBindingMismatchIsReported);
        failures += Run("Dynamic options accept typed display/value pairs without reporting (FL-16)", VerifyTypedOptionPairs);
        failures += Run("Typed choices render the label and commit the real instance (R4-A)", VerifyTypedChoices);
        failures += Run("The dropdown's selector look reserves its outlets and refuses an unknown name (SA1.1)", VerifyDropdownSelectorAppearance);
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

        using UiHost host = new("test", manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
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

        using UiHost host = new("test", manifest, bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
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

    // A4 (0.7): FindDisplayText used to test value OR text per item, so an earlier option whose
    // display text equalled the bound value could hide a later option whose value IS that value.
    // The contract is two passes — exact value first, display-text fallback second — with option
    // order authoritative inside each pass. The seam is the widget's own draw: the field's display
    // string is what reaches the single text outlet, no reflection-resolver shortcut.

    private static void VerifyDropdownValuePrecedence()
    {
        Check("Second" == FieldDrawnText("Option1=\"b\" Value1=\"a\" Option2=\"Second\" Value2=\"b\"", "b"),
            "the required case: a later exact value beats an earlier text collision");
        Check("One" == FieldDrawnText("Option1=\"One\" Value1=\"x\" Option2=\"Two\" Value2=\"y\"", "x"),
            "value-only match still resolves");
        Check("One" == FieldDrawnText("Option1=\"One\" Value1=\"x\" Option2=\"Two\" Value2=\"y\"", "One"),
            "the legacy display-text fallback still runs when no value matches");
        Check("zzz" == FieldDrawnText("Option1=\"One\" Value1=\"x\" Option2=\"Two\" Value2=\"y\"", "zzz"),
            "no match still displays the raw value");
        Check("First" == FieldDrawnText("Option1=\"First\" Value1=\"d\" Option2=\"Second\" Value2=\"d\"", "d"),
            "two value matches: the first in option order wins, unchanged");
        Check("Alpha" == FieldDrawnText("Option1=\"Alpha\" Option2=\"Beta\"", "Alpha"),
            "static pairs without Value keep working (value defaults to the text)");

        // Dynamic options (BindOptions) are value==text pairs; precedence must not shift their result.
        Check("y" == FieldDrawnText("OptionsBind=\"Options\"", "y", options: new List<string> { "x", "y" }),
            "an options binding still displays its exact member");

        // Writeback is untouched: drawing resolves for display only; the bound value survives the pass.
        string stored = "b";
        FieldDrawnText("Option1=\"b\" Value1=\"a\" Option2=\"Second\" Value2=\"b\"", "b", keepValue: s => stored = s);
        Check(stored == "b", "the draw pass committed nothing to the binding");
    }

    private static string FieldDrawnText(
        string optionAttributes, string current, List<string>? options = null, Action<string>? keepValue = null)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" " + optionAttributes + " />"
            + "</UiPage>";

        var bindings = new UiBindings();
        string value = current;
        bindings.BindValue("dropdown", () => value, v => value = v);
        if (options != null)
        {
            bindings.BindOptions("Options", () => options);
        }

        using UiHost host = new("test", UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 200f));

        IList texts = (IList)Field(typeof(Verse.Widgets), "LabelTexts", null);
        IList rects = (IList)Field(typeof(Verse.Widgets), "LabelRects", null);
        texts.Clear();
        rects.Clear();

        host.BeginFrame();
        host.Draw(new Rect(0f, 0f, 300f, 200f), snapshot);
        host.EndFrame();

        keepValue?.Invoke(value);

        // The widget carries no Label attribute, so the field display is the page's only label call.
        if (texts.Count != 1)
        {
            throw new Exception("expected exactly one drawn label, got " + texts.Count);
        }

        return (string)texts[0]!;
    }

    private static object Field(Type type, string name, object? instance)
    {
        FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        if (field == null) throw new Exception(type.Name + " has no field '" + name + "'");
        return field.GetValue(instance)!;
    }

    // A5 (0.7): the demo mod's dead end - a collection registered through BindReadOnly compiles and
    // passes value validation, and then dropdown creation reported the options binding as simply
    // "missing", naming the wrong cause. The registration category is now diagnosed; nothing is
    // coerced, and a key that legitimately carries both registrations is still valid.

    private static void VerifyOptionsKindDiagnostic()
    {
        // 1. Truly absent key: the historical message stands, with no invented category.
        var empty = new UiBindings();
        string missing = MessageOf(() => empty.ValidateOptions<string>("nobody", "page/dropdown"));
        Check(missing.Contains("Required options binding 'nobody' is missing at 'page/dropdown'")
            && !missing.Contains("BindOptions"),
            "a key with no registration at all keeps the plain missing message");

        // 2. The demo's case: a read-only collection value under the same key.
        var readOnlyList = new UiBindings();
        IReadOnlyList<string> items = new List<string> { "a", "b" };
        readOnlyList.BindReadOnly("Options", () => items);
        string wrongKind = MessageOf(() => readOnlyList.ValidateOptions<string>("Options", "page/dropdown"));
        Check(wrongKind.Contains("'Options'") && wrongKind.Contains("page/dropdown"),
            "the diagnosis names the key and the element path");
        Check(wrongKind.Contains("read-only value") && wrongKind.Contains("not as options"),
            "it says the registration category is wrong rather than calling it missing");
        Check(wrongKind.Contains("BindOptions"),
            "and it points at the registration that would fix it");

        // 3. A writable value under the same key is diagnosed as writable, not read-only.
        var writableList = new UiBindings();
        List<string> mutable = new List<string> { "a" };
        writableList.BindValue<IReadOnlyList<string>>("Options", () => mutable, v => { });
        Check(MessageOf(() => writableList.ValidateOptions<string>("Options", "p")).Contains("writable value"),
            "a writable collection value is named as writable");

        // 4. Valid options still pass; the wrong ITEM type keeps its existing distinction.
        var good = new UiBindings();
        good.BindOptions("Options", () => items);
        good.ValidateOptions<string>("Options", "page/dropdown");
        Check(true, "a real options binding validates");
        var wrongItem = new UiBindings();
        wrongItem.BindOptions("Options", () => new List<int> { 1 });
        string itemType = MessageOf(() => wrongItem.ValidateOptions<string>("Options", "p"));
        Check(itemType.Contains("'Int32'") && itemType.Contains("'String'"),
            "the wrong-option-item-type message is unchanged");

        // 5. A key may legitimately carry BOTH a value and an options registration; neither
        // validation may reject the other's presence.
        var both = new UiBindings();
        string selected = "a";
        both.BindValue("Pick", () => selected, v => selected = v);
        both.BindOptions("Pick", () => items);
        both.ValidateValue<string>("Pick", "p");
        both.ValidateOptions<string>("Pick", "p");
        Check(true, "co-existing value and options registrations both validate");

        // 6. The real creation path: the host wraps the widget's validation error, and the
        // consumer-visible message must carry the diagnosis, not the old bare "missing".
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        var hostBindings = new UiBindings();
        string current = "a";
        hostBindings.BindValue("dropdown", () => current, v => current = v);
        hostBindings.BindReadOnly("Options", () => items);
        string xml =
            "<UiPage Schema=\"2\" Source=\"test\">"
            + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" OptionsBind=\"Options\" />"
            + "</UiPage>";
        string creationMessage = MessageOf(() =>
        {
            using UiHost h = new("test", UiLayoutManifest.Parse(xml), hostBindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        });
        Check(creationMessage.Contains("read-only value") && creationMessage.Contains("BindOptions")
            && creationMessage.Contains("Options"),
            "host creation surfaces the category diagnosis through the widget-validation wrapper");
    }

    private static string MessageOf(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        throw new Exception("expected the validation to throw, but it passed");
    }

    // --- 0.4.x leaf vocabulary: the two composites rebuilt over the text atom --------------------
    // chrome/banner and state/empty used to carry their own copy of "wrap the string, reserve the
    // height". They are now compositions over text/wrapped: same kind, same schema, same label set,
    // same painted band, one arithmetic. These lanes pin the pre-refactor numbers with an oracle
    // written out here (not a call into the new code) and pin what reaches the single text outlet.

    private const string LongCjk = "这是一段用于验证换行高度的文本内容";

    private static void VerifyCompositeBandsComeFromTheAtom()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.Vanilla;

        var cases = new (string Text, float Width)[]
        {
            ("", 120f),
            ("ok", 120f),
            (LongCjk, 120f),
            (LongCjk, 200f),
            (LongCjk, 400f),
            ("abcdefghijklmnopqrstuvwxyz0123456789", 120f)
        };

        foreach ((string text, float width) in cases)
        {
            UiWidgetContext ctx = Context(session, theme, width);

            ChromeBannerWidget banner = new();
            banner.Configure(new UiElementSpec("banner", ChromeBannerWidget.Kind, Attributes(("Text", text))));
            CheckClose(Oracle(text, UiFont.Tiny, width, 6f, 22f), banner.Measure(ctx),
                "chrome/banner keeps its pre-refactor band at " + width + "px for " + Describe(text));

            EmptyStateWidget empty = new();
            empty.Configure(new UiElementSpec("empty", EmptyStateWidget.Kind, Attributes(("Text", text))));
            CheckClose(Oracle(text, UiFont.Small, width, 12f, 48f), empty.Measure(ctx),
                "state/empty keeps its pre-refactor band at " + width + "px for " + Describe(text));

            // The number both composites got is the atom's one function, at their own font and lead.
            CheckClose(SharedBandOracle(text, UiFont.Tiny, width, 6f),
                WrappedTextWidget.MeasureBand(ctx, text, UiFont.Tiny, 6f),
                "the banner's band is the atom's band contract at Tiny");
            CheckClose(SharedBandOracle(text, UiFont.Small, width, 12f),
                WrappedTextWidget.MeasureBand(ctx, text, UiFont.Small, 12f),
                "the empty state's band is the same function at Small");
        }

        // A refactor that quietly standardised both composites onto the atom's default font would make
        // these two bands equal; keeping them distinct is what proves each still measures its own font.
        UiWidgetContext narrow = Context(session, theme, 120f);
        ChromeBannerWidget tinyBanner = new();
        tinyBanner.Configure(new UiElementSpec("b", ChromeBannerWidget.Kind, Attributes(("Text", LongCjk))));
        EmptyStateWidget smallState = new();
        smallState.Configure(new UiElementSpec("e", EmptyStateWidget.Kind, Attributes(("Text", LongCjk))));
        Check(Near(30f, tinyBanner.Measure(narrow)), "the banner's band is the Tiny row (2 lines x 12 + 6)");
        Check(Near(60f, smallState.Measure(narrow)), "the empty state's band is the Small row (3 lines x 16 + 12)");
        Check(!Near(tinyBanner.Measure(narrow), smallState.Measure(narrow)),
            "the two composites still measure with their own fonts, not the atom's default");

        // The atom itself is unchanged by the refactor: same string, same width, same model.
        WrappedTextWidget atom = new();
        atom.Configure(new UiElementSpec("note", WrappedTextWidget.Kind, Attributes(("Text", LongCjk))));
        CheckClose(60f, atom.Measure(Context(session, theme, 120f)),
            "the atom's own band is still its wrapped height plus its vertical padding");
    }

    /// <summary>
    /// The drawing half. <see cref="UiThemeDraw.Label"/> is the library's only text outlet, so the
    /// (rect, text, font) triple reaching it is the drawing result that matters; the fit audit is the
    /// harness seam that exposes that triple. A band squeezed below the wrapped need makes the audit
    /// speak, and the reserved band makes it stay silent — the second half is the positive control
    /// that Measure and Draw still agree after the composites were rebuilt.
    /// </summary>
    private static void VerifyCompositeOutletTriple()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using UiSession session = new();
        UiTheme theme = UiTheme.Vanilla;
        var reports = new List<UiOverflowReport>();

        UiFitAudit.Attach(new WrappingMetrics(), report => reports.Add(report));
        try
        {
            foreach ((Type kind, string text, UiFont font) in new (Type, string, UiFont)[]
            {
                (typeof(ChromeBannerWidget), LongCjk, UiFont.Tiny),
                (typeof(EmptyStateWidget), LongCjk, UiFont.Small)
            })
            {
                reports.Clear();
                UiFitAudit.Reset();
                UiFitAudit.Enabled = true;

                IUiWidget widget = kind == typeof(ChromeBannerWidget)
                    ? new ChromeBannerWidget()
                    : new EmptyStateWidget();
                widget.Configure(new UiElementSpec("probe", KindOf(kind), Attributes(("Text", text))));

                // 1) the squeezed band: the audit reports the exact string, font and width the outlet got.
                widget.Draw(new Rect(0f, 0f, 200f, 4f), Context(session, theme, 200f));
                Check(reports.Count == 1, KindOf(kind) + " reports its one squeezed-band finding");
                if (reports.Count == 1)
                {
                    Check(reports[0].Text == text, KindOf(kind) + " draws the resolved string it measured");
                    Check(reports[0].Font == font, KindOf(kind) + " draws in its own font (" + font + ")");
                    Check(reports[0].Axis == UiOverflowAxis.Height, KindOf(kind) + " overflows on the height axis");
                    CheckClose(200f, reports[0].RectWidth, KindOf(kind) + " draws into the rect it was handed");
                }

                // 2) the reserved band: Measure's answer is exactly what Draw needs, so nothing reports.
                reports.Clear();
                UiFitAudit.Reset();
                float band = widget.Measure(Context(session, theme, 200f));
                widget.Draw(new Rect(0f, 0f, 200f, band), Context(session, theme, 200f));
                Check(reports.Count == 0,
                    KindOf(kind) + " drawing into its own measured band overflows nothing (measure meets draw)");
            }
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Reset();
        }
    }

    /// <summary>
    /// The compatibility half of "keep the names": the kind strings, the allowed-attribute sets and the
    /// declared label sets are the frozen vocabulary, and a manifest that used every declared attribute
    /// must still create, arrange and draw.
    /// </summary>
    /// <summary>G2: PayloadKey hands the command the bound payload, so a repeated row can say which item it
    /// is; a button without one still fires the payload-free command it always did.</summary>
    private static void VerifyButtonPayload()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        string seen = "";
        var bindings = new UiBindings();
        bindings.BindValue<string>("row-payload", () => "item-7", _ => { });
        bindings.BindAction<string>("act", payload => seen = payload);
        using UiHost host = new(
            "payload", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"payload\">"
                + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"act\" PayloadKey=\"row-payload\" Text=\"go\" Height=\"24\" />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new Vector2(300f, 100f));
        UiNative.ButtonOverride = _ => true;
        host.DrawFrame(new Rect(0f, 0f, 300f, 100f));
        UiNative.ButtonOverride = null;
        if (!string.Equals(seen, "item-7", StringComparison.Ordinal))
        {
            throw new Exception("the command received '" + seen + "' instead of the bound payload");
        }

        string plain = "";
        var plainBindings = new UiBindings();
        plainBindings.BindCommand("act", () => plain = "fired");
        using UiHost host2 = new(
            "payload", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"payload\">"
                + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"act\" Text=\"go\" Height=\"24\" />"
                + "</UiPage>"),
            plainBindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host2.MeasureAndArrange(new Vector2(300f, 100f));
        UiNative.ButtonOverride = _ => true;
        host2.DrawFrame(new Rect(0f, 0f, 300f, 100f));
        UiNative.ButtonOverride = null;
        if (!string.Equals(plain, "fired", StringComparison.Ordinal))
        {
            throw new Exception("a payload-free button no longer fires its command");
        }
    }

    /// <summary>G3: Chrome="none" paints no surface while the hit test still fires, and Height="Auto" takes the
    /// measured content band rather than the theme's row height.</summary>
    private static void VerifyBareHitArea()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        int fills = SurfaceFills("Text=\"go\" Height=\"24\"");
        int bare = SurfaceFills("Text=\"go\" Height=\"24\" Chrome=\"none\"");
        if (fills == 0) throw new Exception("the painted button painted no surface, so the comparison is vacuous");
        if (bare != 0) throw new Exception("Chrome=\"none\" still painted " + bare + " surface(s)");

        var bindings = new UiBindings();
        bindings.BindCommand("act", () => { });
        using UiHost host = new(
            "bare", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"bare\">"
                + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"act\" Text=\"content\" Height=\"Auto\" Chrome=\"none\" />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 100f));
        float auto = new StubMetrics().MeasureText("content", UiTheme.Vanilla.DefaultFont, 300f);
        if (Math.Abs(snapshot.RectById["b"].height - auto) > 0.01f)
        {
            throw new Exception("Height=Auto arranged " + snapshot.RectById["b"].height + " instead of the measured " + auto);
        }

        if (!Rejected("Chrome=\"painted\" Text=\"x\" Height=\"24\"", "Chrome="))
        {
            throw new Exception("an unsupported Chrome value was accepted instead of refused at creation");
        }
    }

    private static int SurfaceFills(string buttonAttributes)
    {
        var bindings = new UiBindings();
        bindings.BindCommand("act", () => { });
        ClearRecordedFill();
        using UiHost host = new(
            "bare", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"bare\">"
                + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"act\" " + buttonAttributes + " />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new Vector2(300f, 100f));
        host.DrawFrame(new Rect(0f, 0f, 300f, 100f));
        return ((IList)StubField("DrawBoxSolidColors")).Count;
    }

    /// <summary>
    /// FL-16 probe, DELIBERATELY NOT REGISTERED AS A LANE. It was written for the typed-pair work and then had
    /// to be withdrawn: with the pair path reverted (the faithful pre-fix shape) this probe stayed GREEN, which
    /// means it does not discriminate — so it is not evidence and must not run as if it were. It is kept, with
    /// the finding, because the next attempt should start from the failure it exposes (the string fallback
    /// accepted a pair-bound key instead of reporting the mismatch) rather than from a fresh guess. See
    /// MEMORY.md and docs/development/0.7/60-capability-dispositions.md for the disposition.

    /// <summary>
    /// FL-16: a dynamic options list may carry display/value PAIRS, and a page that declares them must create,
    /// render the display text and record NO diagnostic - an element type the dropdown accepts is not a
    /// mismatch, which is exactly the line FL-23's reporting makes visible. Run first with a stand-in pair
    /// type, because the public UiOption does not exist until this lane has been seen red.
    /// </summary>
    private static void VerifyTypedOptionPairs()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // Rule from FL-23's counter trap: reset, then measure this step's delta.
        UiFitAudit.Reset();

        var pairs = new List<UiOption> { new UiOption("First", "x"), new UiOption("Second", "y") };
        var bindings = new UiBindings();
        string current = "y";
        bindings.BindValue("choice", () => current, value => current = value);
        bindings.BindOptions("opts", () => pairs);

        using UiHost host = new(
            "pairs", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"pairs\">"
                + "<Widget Id=\"dd\" Kind=\"input/dropdown\" Bind=\"choice\" OptionsBind=\"opts\" Height=\"24\" />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new Vector2(300f, 100f));
        ClearRecordedLabels();
        host.DrawFrame(new Rect(0f, 0f, 300f, 100f));

        var texts = (IList)StubField("LabelTexts");
        bool showsDisplay = false;
        foreach (object? text in texts)
        {
            if (string.Equals(text as string, "Second", StringComparison.Ordinal)) showsDisplay = true;
        }

        if (!showsDisplay) throw new Exception("the field did not render the pair's display text for its bound value");
        if (UiFitAudit.StyleFallbackCount != 0)
        {
            throw new Exception("a valid pair-bound page recorded " + UiFitAudit.StyleFallbackCount + " diagnostic(s)");
        }
    }

    /// <summary>
    /// FL-23: reading an options binding as the wrong element type throws today, and that is correct - but
    /// nothing lands on the library's audit surface, so a consumer that catches the throw keeps rendering a
    /// silently degraded control and no report ever says why. The fix is one deduplicated diagnostic on the
    /// same fail-soft channel FL-21/FL-22 use; this lane is its evidence, and it fails on the pre-fix tree.
    /// </summary>
    private static void VerifyBindingMismatchIsReported()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // The channel is CUMULATIVE and shared with every other lane in the process, so the assertion must
        // measure a delta from a reset - not a total (a first pass read the total and passed for the wrong
        // reason: exactly the shape this batch exists to catch).
        UiFitAudit.Reset();
        var bindings = new UiBindings();
        bindings.BindOptions("choices", () => new List<int> { 1, 2 });

        // The throw stays: the read is answered as "wrong type", never coerced.
        bool threw = false;
        try
        {
            bindings.GetOptions<string>("choices");
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        if (!threw) throw new Exception("a mismatched options read no longer reports the wrong element type");

        int reported = UiFitAudit.StyleFallbackCount;
        if (reported != 1)
        {
            throw new Exception(
                "the mismatch threw but nothing was reported: StyleFallbackCount=" + reported
                + " (expected exactly one diagnostic a consumer can read)");
        }

        // Reading it wrong twice must not stack: the channel is a report, not a frame log.
        try
        {
            bindings.GetOptions<string>("choices");
        }
        catch (InvalidOperationException)
        {
        }

        if (UiFitAudit.StyleFallbackCount != 1)
        {
            throw new Exception("the second identical mismatch was reported again (count=" + UiFitAudit.StyleFallbackCount + ")");
        }

        // The matching read reports nothing at all.
        bindings.GetOptions<int>("choices");
        if (UiFitAudit.StyleFallbackCount != 1)
        {
            throw new Exception("a correct read recorded a diagnostic");
        }
    }

    /// <summary>True when the page is refused at creation with <paramref name="expectedInMessage"/> in the
    /// located diagnosis - the shape every vocabulary refusal in this batch must keep.</summary>
    private static bool Rejected(string attributes, string expectedInMessage)
    {
        var bindings = new UiBindings();
        bindings.BindCommand("act", () => { });
        try
        {
            using UiHost host = new(
                "bare", UiLayoutManifest.Parse(
                    "<UiPage Schema=\"2\" Source=\"bare\">"
                    + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"act\" " + attributes + " />"
                    + "</UiPage>"),
                bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
            return false;
        }
        catch (UiContractException ex)
        {
            return ex.Message.IndexOf(expectedInMessage, StringComparison.Ordinal) >= 0;
        }
    }

    /// <summary>G5: the banner resolves its ink through the role table, and an untone banner keeps the
    /// secondary ink it always had (the kind's declared default emphasis).</summary>
    private static void VerifyBannerRole()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, ChromeBannerWidget.Kind);
        if (schema == null || !Contains(schema, "Tone") || !Contains(schema, "Emphasis"))
        {
            throw new Exception("chrome/banner does not declare the role pair (G5)");
        }

        Color plain = BannerInk("Tone=\"\"");
        if (!SameColor(plain, UiTheme.Vanilla.TextSecondary))
        {
            throw new Exception("an untone banner no longer paints its documented secondary ink: " + Describe(plain));
        }

        Color danger = BannerInk("Tone=\"Danger\"");
        if (SameColor(danger, plain))
        {
            throw new Exception("an authored Tone did not move the banner's ink: " + Describe(danger));
        }
    }

    private static Color BannerInk(string toneAttribute)
    {
        ClearRecordedLabels();
        var bindings = new UiBindings();
        using UiHost host = new(
            "banner-role", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"banner-role\">"
                + "<Widget Id=\"b\" Kind=\"chrome/banner\" Text=\"status\" Height=\"24\" " + toneAttribute + " />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new Vector2(300f, 100f));
        host.DrawFrame(new Rect(0f, 0f, 300f, 100f));

        var colors = (IList)StubField("LabelColors");
        if (colors.Count == 0) throw new Exception("the banner painted no label");
        return (Color)colors[colors.Count - 1]!;
    }

    /// <summary>SelectedKey: a bound boolean resolves the element to the active treatment, and an
    /// unresolvable key is fail-soft (the authored look stands) with one recorded note.</summary>
    private static void VerifySelectedKey()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        if (ButtonFill(true) is Color selected && ButtonFill(false) is Color idle && SameColor(selected, idle))
        {
            throw new Exception("SelectedKey=true did not change the button's treatment");
        }

        bool selectedNow = false;
        var bindings = new UiBindings();
        bindings.BindValue("sel", () => selectedNow, value => selectedNow = value);
        BindCommand(bindings);
        UiFitAudit.Reset();
        using UiHost host = new(
            "selected-key", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"selected-key\">"
                + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"act\" Text=\"x\" Height=\"24\" SelectedKey=\"missing-key\" />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new Vector2(300f, 100f));
        host.DrawFrame(new Rect(0f, 0f, 300f, 100f));
        if (UiFitAudit.StyleFallbackCount != 1)
        {
            throw new Exception("an unresolvable SelectedKey recorded " + UiFitAudit.StyleFallbackCount + " note(s), expected exactly one");
        }
    }

    private static Color? ButtonFill(bool selected)
    {
        bool state = selected;
        var bindings = new UiBindings();
        bindings.BindValue("sel", () => state, value => state = value);
        BindCommand(bindings);
        ClearRecordedFill();
        using UiHost host = new(
            "selected-key", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"selected-key\">"
                + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"act\" Text=\"x\" Height=\"24\" SelectedKey=\"sel\" />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
        host.MeasureAndArrange(new Vector2(300f, 100f));
        host.DrawFrame(new Rect(0f, 0f, 300f, 100f));
        var colors = (IList)StubField("DrawBoxSolidColors");
        return colors.Count == 0 ? (Color?)null : (Color)colors[colors.Count - 1]!;
    }

    private static void BindCommand(UiBindings bindings)
    {
        bindings.BindCommand("act", () => { });
    }

    private static void ClearRecordedLabels()
    {
        ((IList)StubField("LabelColors")).Clear();
        ((IList)StubField("LabelTexts")).Clear();
        ((IList)StubField("LabelRects")).Clear();
    }

    private static void ClearRecordedFill() => ((IList)StubField("DrawBoxSolidColors")).Clear();

    private static object StubField(string name)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("Verse stub is missing " + name);
        return field.GetValue(null)!;
    }

    private static bool SameColor(Color left, Color right)
    {
        return Math.Abs(left.r - right.r) < 0.001f && Math.Abs(left.g - right.g) < 0.001f
            && Math.Abs(left.b - right.b) < 0.001f && Math.Abs(left.a - right.a) < 0.001f;
    }

    private static string Describe(Color color)
    {
        return "(" + color.r + "," + color.g + "," + color.b + "," + color.a + ")";
    }

    private static void VerifyCompositeVocabularyUnchanged()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // G5: the banner declares the atoms' role pair now, so its schema is the engine-wide set plus
        // Tone/Emphasis plus its own four names - the vocabulary move, pinned.
        CheckSchema(ChromeBannerWidget.Kind, new[]
        {
            "Id", "Kind", "Tab", "Hidden", "Visible", "VisibleKey", "HelpKey", "SelectedKey", "WidthKey",
            "WideHidden", "Tone", "Emphasis", "Bind", "Text", "TextKey", "Height"
        });
        CheckSchema(EmptyStateWidget.Kind, new[] { "Id", "Kind", "Text", "TextKey", "Height", "Tab", "Hidden" });
        CheckLabels(ChromeBannerWidget.Kind);
        CheckLabels(EmptyStateWidget.Kind);

        string xml =
            "<UiPage Schema=\"2\" Source=\"composite-test\">"
            + "<Stack Id=\"root\">"
            + "<Widget Id=\"banner\" Kind=\"chrome/banner\" Bind=\"BannerText\" Height=\"Auto\" />"
            + "<Widget Id=\"bannerKeyed\" Kind=\"chrome/banner\" TextKey=\"banner.key\" />"
            + "<Widget Id=\"empty\" Kind=\"state/empty\" Text=\"nothing here\" />"
            + "</Stack>"
            + "</UiPage>";

        string bannerText = LongCjk;
        var bindings = new UiBindings();
        bindings.BindReadOnly("BannerText", () => bannerText);

        using UiHost host = new("composite-test", UiLayoutManifest.Parse(xml), bindings, UiTheme.Vanilla, new WrappingMetrics(), new StubTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 400f));

        foreach (string id in new[] { "banner", "bannerKeyed", "empty" })
        {
            Check(snapshot.RectById.ContainsKey(id), "an existing manifest still arranges '" + id + "'");
        }

        CheckClose(Oracle(bannerText, UiFont.Tiny, 300f, 6f, 22f), snapshot.RectById["banner"].height,
            "the manifest-arranged banner band is the atom band at the banner's own font");
        CheckClose(Oracle("[banner.key]", UiFont.Tiny, 300f, 6f, 22f), snapshot.RectById["bannerKeyed"].height,
            "the keyed banner resolves through the same translation path before the band is measured");

        host.DrawFrame(new Rect(0f, 0f, 300f, 400f));
        Check(host.Session.TrippedNodes.Count == 0, "both composites draw their whole path without a trip");
    }

    private static void CheckSchema(string kind, string[] expected)
    {
        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, kind);
        if (schema == null)
        {
            Check(false, kind + " still declares an attribute schema");
            return;
        }

        Check(schema.Count == expected.Length, kind + " schema has " + expected.Length + " names, as before");
        foreach (string name in expected)
        {
            Check(Contains(schema, name), kind + " schema still allows '" + name + "'");
        }
    }

    private static void CheckLabels(string kind)
    {
        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(UiWidgetRegistry.CoreScope, kind);
        if (labels == null)
        {
            Check(false, kind + " still declares a label set");
            return;
        }

        Check(labels.Count == 2 && Contains(labels, "Text") && Contains(labels, "TextKey"),
            kind + " still declares exactly Text/TextKey for Width=Auto");
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string candidate in set)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static string KindOf(Type type)
    {
        return type == typeof(ChromeBannerWidget) ? ChromeBannerWidget.Kind : EmptyStateWidget.Kind;
    }

    private static string Describe(string text)
    {
        if (text.Length == 0) return "an empty string";
        return "'" + (text.Length <= 12 ? text : text.Substring(0, 12) + "...") + "'";
    }

    private static Dictionary<string, string> Attributes(params (string Name, string Value)[] pairs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs)
        {
            attributes[name] = value;
        }

        return attributes;
    }

    private static UiWidgetContext Context(UiSession session, UiTheme theme, float viewWidth)
    {
        return new UiWidgetContext(
            "composite-test", session, new WrappingMetrics(), theme, new StubTranslation(),
            new UiBindings(), viewWidth, "root");
    }

    /// <summary>
    /// The band as <c>chrome/banner</c> and <c>state/empty</c> computed it before this change, written
    /// out here independently of the production types. An implementation that only agrees with another
    /// implementation proves nothing, so the oracle is the old arithmetic over the shared glyph model,
    /// not a call into the new seam.
    /// </summary>
    private static float Oracle(string text, UiFont font, float width, float verticalLead, float floor)
    {
        if (text.Length == 0) return floor;
        return Math.Max(floor, WrappedLines(text, font, width) * Em(font) + verticalLead);
    }

    /// <summary>The same inputs as the atom's shared band contract; empty text is 0 there, floors live in callers.</summary>
    private static float SharedBandOracle(string text, UiFont font, float width, float verticalLead)
    {
        if (text.Length == 0) return 0f;
        return Math.Max(1f, WrappedLines(text, font, width) * Em(font)) + verticalLead;
    }

    private static int WrappedLines(string text, UiFont font, float width)
    {
        float advance = StubTextWidth.Of(text, font);
        return Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
    }

    private static float Em(UiFont font)
    {
        return font switch
        {
            UiFont.Tiny => 12f,
            UiFont.Medium => 18f,
            _ => 16f
        };
    }

    private static bool Near(float expected, float actual) => Math.Abs(expected - actual) <= 0.0001f;

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            Console.Error.WriteLine("  FAIL: " + name);
            throw new Exception(name);
        }
    }

    private static void CheckClose(float expected, float actual, string name)
    {
        if (!Near(expected, actual))
        {
            Console.Error.WriteLine("  FAIL: " + name + " (expected '" + expected + "', got '" + actual + "')");
            throw new Exception(name + " (expected '" + expected + "', got '" + actual + "')");
        }

        Console.WriteLine("  ok: " + name);
    }

    /// <summary>
    /// The lane's ruler: width from the shared half-width glyph model, height from the wrapped line
    /// count times that model's em. A constant-per-font stub could not tell 2 wrapped lines from 3,
    /// which is the whole distinction these bands are about.
    /// </summary>
    private sealed class WrappingMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return WrappedLines(text, font, width) * Em(font);
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
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

    // --- R4-A (0.7.x): the genuinely TYPED value/options path ------------------------------------------
    //
    // Three things are asserted here at once, and the legacy lanes above are the other half of the evidence
    // (they must keep passing untouched): the field renders the CHOICE's label, the setter receives the real
    // instance - by runtime type, and for a reference type by REFERENCE - and a value of the wrong type is
    // refused without writing anything. The last assertion drives a page whose bindings do not implement the
    // optional seam at all, which is the source-compatibility half of the change.

    private enum Scope
    {
        Near,
        Far
    }

    private sealed class ModeToken
    {
        internal ModeToken(string name) => Name = name;

        internal string Name { get; }
    }

    private static void VerifyTypedChoices()
    {
        // 1. An enum value. The labels are words no value-to-string conversion could produce (the members are
        // Near/Far), so a display that renders a label at all is already evidence the value was not spelled.
        Scope chosen = Scope.Near;
        string displayedBefore = ClickTypedDropdown(
            () => chosen,
            value => chosen = value,
            new List<UiChoice<Scope>>
            {
                new UiChoice<Scope>("Within reach", Scope.Near),
                new UiChoice<Scope>("Far away", Scope.Far)
            },
            clickRow: 1,
            out object? received,
            out string displayedAfter);

        Check(displayedBefore == "Within reach", "the field shows the choice's label, not the value's name");
        Check(displayedAfter == "Far away", "and it follows the choice that was clicked");
        Check(chosen == Scope.Far, "the click committed that choice's value");
        Check(received is Scope.Far && received.GetType() == typeof(Scope),
            "the typed setter received the real enum, and its runtime type is the declared one");

        // 2. A reference-typed value: what arrives is the instance the choice carried, not an equal one.
        var first = new ModeToken("first");
        var second = new ModeToken("second");
        ModeToken picked = first;
        string before = ClickTypedDropdown(
            () => picked,
            value => picked = value,
            new List<UiChoice<ModeToken>>
            {
                new UiChoice<ModeToken>("First", first),
                new UiChoice<ModeToken>("Second", second)
            },
            clickRow: 1,
            out object? arrived,
            out string after);

        Check(before == "First" && after == "Second", "a reference-typed choice renders its own labels");
        Check(ReferenceEquals(arrived, second), "the setter received the instance itself, not a copy and not a string");
        Check(ReferenceEquals(picked, second), "and the model holds that same instance");

        Scope typedMode = Scope.Near;
        Scope? writtenMode = null;
        int modeWrites = 0;
        var modeBindings = new UiBindings();
        modeBindings.BindValue("mode", () => typedMode,
            value => { typedMode = value; writtenMode = value; modeWrites++; });
        modeBindings.BindOptions("mode", () => new List<UiChoice<Scope>>
        {
            new UiChoice<Scope>("Within reach", Scope.Near),
            new UiChoice<Scope>("Far away", Scope.Far)
        });
        using (UiHost modeHost = new("typed-mode-row", UiLayoutManifest.Parse(
            "<UiPage Schema=\"2\" Source=\"typed-mode-row\">"
            + "<Widget Id=\"mode\" Kind=\"input/mode-row\" Height=\"28\" /></UiPage>"),
            modeBindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation()))
        {
            UiLayoutSnapshot modeSnapshot = modeHost.MeasureAndArrange(new Vector2(300f, 100f));
            var cells = new List<Rect>();
            try
            {
                UiNative.ButtonOverride = rect => { cells.Add(rect); return false; };
                DrawOnce(modeHost, modeSnapshot, 300f, 100f);
                Check(cells.Count == 2, "mode-row draws one actionable cell per typed choice");
                Rect target = cells[1];
                UiNative.ButtonOverride = rect => SameRect(rect, target);
                DrawOnce(modeHost, modeSnapshot, 300f, 100f);
            }
            finally { UiNative.ButtonOverride = null; }
        }
        Check(modeWrites == 1 && writtenMode == Scope.Far && typedMode == Scope.Far,
            "mode-row click sends the real enum to its setter exactly once");

        // 3. Type identity IS the validation: a mismatched value is refused, and nothing is written.
        var seam = new UiBindings();
        ModeToken kept = first;
        seam.BindValue("pick", () => kept, value => kept = value);
        seam.BindOptions("pick", () => new List<UiChoice<ModeToken>> { new UiChoice<ModeToken>("First", first) });
        Check(!seam.AcceptsValue("pick", "first"), "a string is not an accepted value where the binding is a ModeToken");
        Check(!seam.TrySetTypedValue("pick", "first"), "the mismatched write is refused");
        Check(ReferenceEquals(kept, first), "and the refusal wrote nothing");
        Check(seam.TrySetTypedValue("pick", second), "a value of the declared type is written");
        Check(ReferenceEquals(kept, second), "and it is the one that arrived");
        Check(!seam.TrySetTypedValue("nobody-bound-this", second), "an unbound key is refused, not thrown");
        seam.TryGetChoices("pick", out IReadOnlyList<UiChoice<object?>> erased);
        Check(erased.Count == 1 && erased[0].Text == "First" && ReferenceEquals(erased[0].Value, first),
            "the choices a widget receives are labels plus the real boxed values");

        // 4. The two legacy option shapes are NOT choices lists, so those pages keep their own path.
        var legacy = new UiBindings();
        legacy.BindOptions("strings", () => new List<string> { "a" });
        legacy.BindOptions("pairs", () => new List<UiOption> { new UiOption("A", "a") });
        Check(!legacy.TryGetChoices("strings", out _), "a string options list is not a choices list");
        Check(!legacy.TryGetChoices("pairs", out _), "a UiOption options list is not a choices list");

        // 5. An IUiBindings implementation WITHOUT the seam: not castable to it, and still string-only. This is
        //    the evidence that the additive seam left every existing implementation source-compatible.
        var inner = new UiBindings();
        string mode = "a";
        inner.BindValue("mode", () => mode, value => mode = value);
        inner.BindOptions("Options", () => new List<string> { "a", "b" });
        IUiBindings stringOnly = new StringOnlyBindings(inner);
        Check(!(stringOnly is IUiTypedChoices), "an implementation without the typed seam is not one");

        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        using (UiHost host = new(
            "strings", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"strings\">"
                + "<Widget Id=\"dropdown\" Kind=\"input/dropdown\" Bind=\"mode\" OptionsBind=\"Options\" Height=\"24\" />"
                + "</UiPage>"),
            stringOnly, UiTheme.Vanilla, new StubMetrics(), new StubTranslation()))
        {
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 100f));
            Check(DrawOnlyLabel(host, snapshot, 300f, 100f) == "a",
                "a seam-less implementation still renders the string path's exact member");
        }
    }

    /// <summary>
    /// SA1.1: <c>input/dropdown</c> gains the shared appearance seam with a second look, <c>selector</c>.
    /// The lane pins the four things a consumer needs to be able to rely on: the name is accepted at
    /// creation and an unknown one is refused naming both values; the default look still draws the exact
    /// outlets it always had (old consumers unchanged, which is the compatibility half); the selector's
    /// outlets come from the SAME shared helpers a consumer composite calls, so core kind and composite
    /// cannot diverge; and the selector's extra pixels are the shape it promises - rail, divider, arrow -
    /// with no reserved slot for a help button in either look.
    /// <para>
    /// MUTATION-TARGET: the outlet arithmetic (revert the selector branch and the plain outlets are found
    /// where the selector's were expected) and the solid delta (delete the arrow or the rail and the count
    /// moves; the count is exact, not a floor). The refusal message is asserted to name both accepted
    /// looks because an author holding a mistyped attribute is the person who reads it.
    /// </para>
    /// </summary>
    private static void VerifyDropdownSelectorAppearance()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        // The kind declares its look set through the schema and the seam - the manifest vocabulary gate.
        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(
            UiWidgetRegistry.CoreScope, DropdownWidget.Kind);
        Check(schema != null && Contains(schema!, UiAppearanceResolver.Attribute),
            "input/dropdown's schema declares the Appearance attribute, so a page may write it");
        UiAppearanceResolver? seam = UiWidgetRegistry.GetAppearanceResolver(
            UiWidgetRegistry.CoreScope, DropdownWidget.Kind);
        Check(seam != null && Contains(seam!.Supported, "selector") && Contains(seam!.Supported, "field"),
            "and the kind registers both looks with the shared seam");

        float padding = UiTheme.Vanilla.Geometry.Padding;

        // (1) Default look: the plain field's text outlet is exactly what it was before the seam existed,
        // and no selector part appears in its recording. Observations are GEOMETRY - which rect was painted
        // where - not call counts, so an appearance-equivalent repaint cannot redden this for the wrong
        // reason (PM r1 point 4).
        using (UiHost plain = DropdownSelectorHost(""))
        {
            UiLayoutSnapshot snapshot = plain.MeasureAndArrange(new Vector2(300f, 100f));
            Rect field = snapshot.RectById["dd"];
            DrawOnce(plain, snapshot, 300f, 100f);
            ClearRecordedFill();
            ClearRecordedLabels();
            plain.BeginFrame();
            plain.Draw(new Rect(0f, 0f, 300f, 100f), snapshot);
            plain.EndFrame();
            var rects = (IList)StubField("LabelRects");
            Rect outlet = (Rect)rects[rects.Count - 1]!;
            CheckClose(field.x + padding, outlet.x, "the default field still starts its text one padding in");
            CheckClose(field.width - padding * 2f, outlet.width,
                "and the default outlet still spans the field minus both insets");
            Check(!HasSelectorRail(RecordedSolidRects(), field),
                "the plain field paints no accent rail and no arrow zone at its edges");
        }

        // (2) Selector look: the reserved rail, the divider in front of the arrow zone, the arrow inside
        // the zone, and the text between them - each found by the rect it occupies.
        using (UiHost selector = DropdownSelectorHost(" Appearance=\"selector\""))
        {
            UiLayoutSnapshot snapshot = selector.MeasureAndArrange(new Vector2(300f, 100f));
            Rect field = snapshot.RectById["dd"];
            DrawOnce(selector, snapshot, 300f, 100f);
            ClearRecordedFill();
            ClearRecordedLabels();
            selector.BeginFrame();
            selector.Draw(new Rect(0f, 0f, 300f, 100f), snapshot);
            selector.EndFrame();
            IList solidRects = RecordedSolidRects();
            var rects = (IList)StubField("LabelRects");
            Rect outlet = (Rect)rects[rects.Count - 1]!;

            CheckClose(field.x + UiThemeDraw.SelectorAccentWidth + padding, outlet.x,
                "the selector's text starts past the reserved accent rail");
            CheckClose(field.xMax - UiThemeDraw.SelectorArrowZoneWidth - padding, outlet.xMax,
                "and ends before the reserved arrow zone: the arrow is an independent region");
            Check(HasSelectorRail(solidRects, field),
                "a rail-width solid spans the field's left edge at exactly the reserved width");
            float hairline = Math.Max(1f, UiTheme.Vanilla.Geometry.Hairline);
            Rect zone = UiThemeDraw.SelectorArrowZone(field);
            bool divider = false;
            foreach (object entry in solidRects)
            {
                Rect r = (Rect)entry!;
                divider |= Math.Abs(r.x - (zone.x - hairline)) <= 0.01f
                    && Math.Abs(r.width - hairline) <= 0.01f && Math.Abs(r.height - field.height) <= 0.01f;
            }

            Check(divider, "the arrow zone stands behind its own divider hairline");
            float centreX = zone.x + zone.width * 0.5f;
            bool arrow = false;
            foreach (object entry in solidRects)
            {
                Rect r = (Rect)entry!;
                arrow |= r.width > 2f
                    && Math.Abs(r.x + r.width * 0.5f - centreX) <= 0.5f
                    && r.x >= zone.x - 0.01f && r.xMax <= zone.xMax + 0.01f
                    && r.y >= zone.y - 0.01f && r.yMax <= zone.yMax + 0.01f;
            }

            Check(arrow, "arrow ink is painted INSIDE the reserved zone, centred on it - however many rows "
                + "the current implementation stacks it into");
        }

        // (3) An unknown look is refused at creation, naming both accepted values.
        bool refused = false;
        string message = "";
        try
        {
            DropdownSelectorHost(" Appearance=\"combo\"").Dispose();
        }
        catch (UiContractException ex)
        {
            refused = true;
            message = ex.Message;
        }

        Check(refused, "an appearance the kind does not draw is refused at creation, not degraded");
        Check(message.IndexOf("combo", StringComparison.Ordinal) >= 0
                && message.IndexOf("selector", StringComparison.Ordinal) >= 0
                && message.IndexOf("field", StringComparison.Ordinal) >= 0,
            "and the refusal names the authored text and both accepted looks: " + message);
    }

    /// <summary>The recorded solid rects of the last drawing pass.</summary>
    private static IList RecordedSolidRects() => (IList)StubField("DrawBoxSolidRects");

    /// <summary>True when a rail-width, full-height solid sits on the field's left edge.</summary>
    private static bool HasSelectorRail(IList solidRects, Rect field)
    {
        foreach (object entry in solidRects)
        {
            Rect r = (Rect)entry!;
            if (Math.Abs(r.x - field.x) <= 0.01f
                && Math.Abs(r.width - UiThemeDraw.SelectorAccentWidth) <= 0.01f
                && Math.Abs(r.height - field.height) <= 0.01f)
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// One dropdown page with the given appearance attribute text ("" for the default). The bindings mirror
    /// the long-standing field fixtures here: a writable string value and a two-option list.
    /// </summary>
    private static UiHost DropdownSelectorHost(string appearanceAttribute)
    {
        var bindings = new UiBindings();
        string mode = "one";
        bindings.BindValue("mode", () => mode, value => mode = value);
        bindings.BindOptions("Options", () => new List<string> { "one", "two" });
        return new UiHost(
            "selector", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"selector\">"
                + "<Widget Id=\"dd\" Kind=\"input/dropdown\" Bind=\"mode\" OptionsBind=\"Options\" Height=\"24\""
                + appearanceAttribute + " />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());
    }

    /// <summary>
    /// Drives one typed dropdown by its real draw path: click the field to open the popup, click the option row
    /// at <paramref name="clickRow"/> by that row's own popup rect, then let the popup close again. Returns what
    /// the field showed before the click and, through the outs, what the typed setter received and what the
    /// field shows afterwards.
    /// </summary>
    private static string ClickTypedDropdown<T>(
        Func<T> get,
        Action<T> set,
        IReadOnlyList<UiChoice<T>> options,
        int clickRow,
        out object? received,
        out string after)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();

        var bindings = new UiBindings();
        object? captured = null;
        bindings.BindValue("pick", get, value =>
        {
            set(value);
            captured = value;
        });
        bindings.BindOptions("pick-options", () => options);

        using UiHost host = new(
            "typed", UiLayoutManifest.Parse(
                "<UiPage Schema=\"2\" Source=\"typed\">"
                + "<Widget Id=\"pick\" Kind=\"input/dropdown\" OptionsBind=\"pick-options\" Height=\"24\" />"
                + "</UiPage>"),
            bindings, UiTheme.Vanilla, new StubMetrics(), new StubTranslation());

        const float width = 300f;
        const float height = 100f;
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(width, height));
        Rect field = snapshot.RectById["pick"];

        string before;
        try
        {
            before = DrawOnlyLabel(host, snapshot, width, height);
            UiNative.ButtonOverride = rect => SameRect(rect, field);
            DrawOnce(host, snapshot, width, height);
            if (!host.Session.IsPopupOpen("pick"))
            {
                throw new Exception("the typed dropdown did not open its popup");
            }

            Rect anchor = host.Session.OpenPopupAnchor!.Value;
            Rect popup = UiPopup.RectFor(anchor, options.Count, host.Session.HostViewport);
            Rect row = new(popup.x, popup.y + clickRow * UiPopup.OptionHeight, popup.width, UiPopup.OptionHeight);
            UiNative.ButtonOverride = rect => SameRect(rect, row);
            DrawOnce(host, snapshot, width, height);
        }
        finally
        {
            UiNative.ButtonOverride = null;
        }

        after = DrawOnlyLabel(host, snapshot, width, height);
        received = captured;
        return before;
    }

    /// <summary>One draw whose only label is the dropdown field's display text.</summary>
    private static string DrawOnlyLabel(UiHost host, UiLayoutSnapshot snapshot, float width, float height)
    {
        DrawOnce(host, snapshot, width, height);
        var texts = (IList)StubField("LabelTexts");
        if (texts.Count != 1)
        {
            throw new Exception("expected exactly one drawn label for the field, got " + texts.Count);
        }

        return (string)texts[0]!;
    }

    private static void DrawOnce(UiHost host, UiLayoutSnapshot snapshot, float width, float height)
    {
        ClearRecordedLabels();
        host.BeginFrame();
        host.Draw(new Rect(0f, 0f, width, height), snapshot);
        host.EndFrame();
    }

    private static bool SameRect(Rect left, Rect right)
    {
        return Math.Abs(left.x - right.x) < 0.01f
            && Math.Abs(left.y - right.y) < 0.01f
            && Math.Abs(left.width - right.width) < 0.01f
            && Math.Abs(left.height - right.height) < 0.01f;
    }

    /// <summary>
    /// An <see cref="IUiBindings"/> implementation with NO typed seam, written against the surface as it stood
    /// before R4-A. It compiles only while the typed path stays on the OPTIONAL companion interface, and it is
    /// what proves a consumer that implements the surface itself keeps building and keeps working string-only.
    /// </summary>
    private sealed class StringOnlyBindings : IUiBindings
    {
        private readonly IUiBindings inner;

        internal StringOnlyBindings(IUiBindings inner) => this.inner = inner;

        public void BindValue<T>(string elementId, Func<T> get, Action<T> set, UiInvalidation invalidates = UiInvalidation.Everything)
            => inner.BindValue(elementId, get, set, invalidates);

        public void BindReadOnly<T>(string elementId, Func<T> get, UiInvalidation invalidates = UiInvalidation.Everything)
            => inner.BindReadOnly(elementId, get, invalidates);

        public void BindOptions<T>(string elementId, Func<IReadOnlyList<T>> get, UiInvalidation invalidates = UiInvalidation.Everything)
            => inner.BindOptions(elementId, get, invalidates);

        public void BindAction<T>(string actionId, Action<T> action, UiInvalidation invalidates = UiInvalidation.Everything)
            => inner.BindAction(actionId, action, invalidates);

        public void BindCommand(string actionId, Action action, Func<bool>? canExecute = null, UiInvalidation invalidates = UiInvalidation.Paint)
            => inner.BindCommand(actionId, action, canExecute, invalidates);

        public T Get<T>(string elementId) => inner.Get<T>(elementId);

        public bool TryGet<T>(string elementId, out T value) => inner.TryGet(elementId, out value);

        public bool TryGetBool(string key, out bool value) => inner.TryGetBool(key, out value);

        public void Set<T>(string elementId, T value) => inner.Set(elementId, value);

        public bool IsWritable(string elementId) => inner.IsWritable(elementId);

        public IReadOnlyList<T> GetOptions<T>(string elementId) => inner.GetOptions<T>(elementId);

        public void Invoke<T>(string actionId, T payload) => inner.Invoke(actionId, payload);

        public void Invoke(string actionId) => inner.Invoke(actionId);

        public bool CanExecute(string actionId) => inner.CanExecute(actionId);

        public bool TryInvokeCommand(string actionId) => inner.TryInvokeCommand(actionId);

        public int GetRevision(string key) => inner.GetRevision(key);

        public UiInvalidation GetInvalidation(string key) => inner.GetInvalidation(key);

        public void NotifyChanged(params string[] keys) => inner.NotifyChanged(keys);

        public void ValidateValue<T>(string elementId, string elementPath) => inner.ValidateValue<T>(elementId, elementPath);

        public void ValidateOptions<T>(string elementId, string elementPath) => inner.ValidateOptions<T>(elementId, elementPath);

        public void ValidateAction<T>(string actionId, string elementPath) => inner.ValidateAction<T>(actionId, elementPath);

        public void ValidateCommand(string actionId, string elementPath) => inner.ValidateCommand(actionId, elementPath);
    }
}
