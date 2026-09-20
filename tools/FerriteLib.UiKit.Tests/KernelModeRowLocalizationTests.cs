using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// G4: localized option labels on <c>input/mode-row</c>. The kind painted only the literal <c>TitleN</c>, so a
/// bilingual consumer could not use it at all — the same kind was unusable in one of the two languages the
/// wired consumer ships, which is why this is a defect against the consumer's real use rather than a
/// convenience. The seam is the library's existing <c>*Key</c> rule: <c>TitleKeyN</c> resolved through
/// <c>IUiTranslation</c> when declared, the literal <c>TitleN</c> otherwise, the option's value when neither.
/// <para>
/// Two details are pinned because they are the ones that decide whether the seam actually works. First, the
/// declared label set carries BOTH families, so <c>Width="Auto"</c> measures the TRANSLATED title (the engine
/// resolves an attribute ending in <c>Key</c> through the same seam) instead of reserving width for a literal
/// the page will never show. Second, a <c>TitleKeyN</c> whose index has no <c>ValueN</c> is refused at
/// creation, exactly like an orphan <c>TitleN</c>: it can never be drawn, so it is the inert declaration this
/// contract refuses rather than one it ignores.
/// </para>
/// </summary>
internal static class KernelModeRowLocalizationTests
{
    private const string Scope = "mode-row-l10n-test";
    private const string LabelKind = "test/label-host";
    private const float Width = 240f;
    private const float RowHeight = 20f;

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("input/mode-row declares both label families", VerifyRegistration);
        Run("TitleKeyN resolves through the translation seam", VerifyKeyResolves);
        Run("TitleKeyN wins over the literal TitleN", VerifyKeyWins);
        Run("Neither declared falls back to the option's value", VerifyValueFallback);
        Run("Width=Auto measures the translated title", VerifyAutoMeasuresTheTranslation);
        Run("A TitleKeyN with no option is refused at creation", VerifyOrphanTitleKeyRefused);
        ResetSeams();
        return failures;
    }

    private static void VerifyRegistration()
    {
        PrepareCore();

        IReadOnlyCollection<string>? schema = UiWidgetRegistry.GetAttributeSchema(UiWidgetRegistry.CoreScope, InputModeRowWidget.Kind);
        if (schema == null)
        {
            Check(false, "input/mode-row declares an attribute schema");
            return;
        }

        Check(Contains(schema, "TitleKey1") && Contains(schema, "TitleKey8"), "the schema declares TitleKey1..8");
        Check(Contains(schema, "Title1") && Contains(schema, "Description1"), "and keeps the literal and help names it had");

        IReadOnlyCollection<string>? labels = UiWidgetRegistry.GetLabelAttributes(UiWidgetRegistry.CoreScope, InputModeRowWidget.Kind);
        Check(
            labels != null && labels.Count == 16 && Contains(labels, "Title1") && Contains(labels, "TitleKey1"),
            "the label set is both families (16 names), so Auto measures the text the page actually shows");
        Check(labels != null && !Contains(labels, "Description1"), "and still excludes the help identities, which are never painted");
    }

    private static void VerifyKeyResolves()
    {
        Fixture fixture = Build(new Dictionary<string, string> { ["TitleKey1"] = "US.Mode.Vanilla" });
        Draw(fixture);

        Check(fixture.Labels().Contains("[US.Mode.Vanilla]"), "the key is resolved through the translation seam before it is painted");
        Check(!fixture.Labels().Contains("US.Mode.Vanilla"), "and the raw key is never painted");
    }

    private static void VerifyKeyWins()
    {
        Fixture fixture = Build(new Dictionary<string, string>
        {
            ["Title1"] = "Literal",
            ["TitleKey1"] = "US.Mode.Vanilla"
        });
        Draw(fixture);

        Check(fixture.Labels().Contains("[US.Mode.Vanilla]"), "a declared key wins over the literal, the rule every other *Key pair uses");
        Check(!fixture.Labels().Contains("Literal"), "so the literal is not what a translated page shows");
    }

    private static void VerifyValueFallback()
    {
        Fixture fixture = Build(new Dictionary<string, string>());
        Draw(fixture);

        Check(fixture.Labels().Contains("A"), "with neither declared the option's value is the label, as before");
    }

    private static void VerifyAutoMeasuresTheTranslation()
    {
        PrepareCore();

        // StubTextWidth: Small em=16, so a Latin character is 8px. "[US.Mode.A]" is 11 characters -> 88px,
        // while the literal "abcdefgh" would be 64px and the value "A" 8px. Only a seam that resolves the
        // key before measuring can produce 88.
        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Padding=\"0\" Gap=\"0\">"
            + "<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Width=\"Auto\" Height=\"20\""
            + " Value1=\"A\" Title1=\"abcdefgh\" TitleKey1=\"US.Mode.A\" />"
            + "<Widget Id=\"rest\" Kind=\"" + LabelKind + "\" Height=\"10\" />"
            + "</Row></UiPage>";

        UiLayoutSnapshot snapshot = Arrange(xml);

        // The label set carries both families, so the measured natural width is the WIDER of the two -
        // the convention text/wrapped and the other labelled atoms already follow. What this check proves is
        // that the KEY half participates at all: 88px comes from the translated string and nothing else.
        var probe = new StubMetrics();
        UiFont font = UiTheme.DarkGold.DefaultFont;
        Check(Near(snapshot.RectById["mode"].width, probe.MeasureWidth("[US.Mode.A]", font)),
            "Auto measures the translated title, so a bilingual page reserves the width it shows — measured "
            + snapshot.RectById["mode"].width + ", translated=" + probe.MeasureWidth("[US.Mode.A]", font)
            + ", literal=" + probe.MeasureWidth("abcdefgh", font) + ", raw key=" + probe.MeasureWidth("US.Mode.A", font));
    }

    private static void VerifyOrphanTitleKeyRefused()
    {
        PrepareCore();

        Check(Reject("<Widget Id=\"mode\" Kind=\"input/mode-row\" Bind=\"mode\" Height=\"20\" Value1=\"A\" TitleKey3=\"US.Orphan\" />", "TitleKey3"),
            "a TitleKeyN whose index has no option is refused at creation, naming the attribute");
    }

    // --- fixture ----------------------------------------------------------------------------------

    private sealed class Fixture
    {
        public InputModeRowWidget Widget = null!;
        public UiWidgetContext Context = null!;
        public Rect Rect;

        public IList Labels()
        {
            FieldInfo? field = typeof(Verse.Widgets).GetField("LabelTexts", BindingFlags.Public | BindingFlags.Static);
            if (field == null) throw new Exception("Verse stub is missing LabelTexts");
            return (IList)field.GetValue(null)!;
        }
    }

    private static Fixture Build(Dictionary<string, string> extra)
    {
        PrepareCore();

        var attrs = new Dictionary<string, string>
        {
            ["Bind"] = "mode",
            ["Height"] = "20",
            ["Value1"] = "A"
        };
        foreach (KeyValuePair<string, string> pair in extra)
        {
            attrs[pair.Key] = pair.Value;
        }

        var bindings = new UiBindings();
        bindings.BindValue<string>("mode", () => "A", _ => { });

        var fixture = new Fixture
        {
            Widget = new InputModeRowWidget(),
            Rect = new Rect(0f, 0f, Width, 120f)
        };
        fixture.Widget.Configure(new UiElementSpec("mode", InputModeRowWidget.Kind, attrs));
        fixture.Context = new UiWidgetContext(
            Scope, new UiSession(), new StubMetrics(), UiTheme.DarkGold,
            new StubTranslation(), bindings, Width, "root");
        return fixture;
    }

    private static void Draw(Fixture fixture)
    {
        ClearLabels();
        fixture.Widget.Draw(fixture.Rect, fixture.Context);
    }

    private static UiLayoutSnapshot Arrange(string xml)
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        UiWidgetRegistry.Register(Scope, LabelKind, () => new HeightWidget(), new[] { "Id", "Kind", "Height" });

        var engine = new UiLayoutEngine(Scope);
        var bindings = new UiBindings();
        bindings.BindValue<string>("mode", () => "A", _ => { });
        var ctx = new UiWidgetContext(
            Scope, new UiSession(), new StubMetrics(), UiTheme.DarkGold,
            new StubTranslation(), bindings, 400f, "root");
        return engine.ArrangeRoots(ctx, new Vector2(400f, 600f), UiLayoutManifest.Parse(xml).Roots);
    }

    private static void PrepareCore()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
    }

    private static bool Reject(string body, string expectedInMessage)
    {
        // The value key is bound, so the refusal under test is the per-option one and not "Bind is missing".
        var bindings = new UiBindings();
        bindings.BindValue<string>("mode", () => "A", _ => { });

        try
        {
            using UiHost host = new(
                Scope,
                UiLayoutManifest.Parse("<UiPage Schema=\"2\" Source=\"" + Scope + "\">" + body + "</UiPage>"),
                bindings,
                UiTheme.DarkGold,
                new StubMetrics(),
                new StubTranslation());
            return false;
        }
        catch (UiContractException ex)
        {
            return ex.Message.IndexOf(expectedInMessage, StringComparison.Ordinal) >= 0;
        }
    }

    private static void ClearLabels()
    {
        foreach (string name in new[] { "LabelRects", "LabelTexts", "LabelColors" })
        {
            FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null) throw new Exception("Verse stub is missing " + name);
            ((IList)field.GetValue(null)!).Clear();
        }
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string candidate in set)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static bool Near(float left, float right)
    {
        return Math.Abs(left - right) <= 0.01f;
    }

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMousePosition = default;
        UiNative.DebugMouseDown = false;
        UiNative.ButtonOverride = null;
    }

    private static int Run(string name, Action action)
    {
        ResetSeams();
        int before = failures;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " threw " + ex.GetType().Name + ": " + ex.Message);
        }

        if (failures == before)
        {
            Console.WriteLine("  ok: " + name);
        }

        return failures - before;
    }

    private static void Check(bool condition, string name)
    {
        if (condition)
        {
            Console.WriteLine("  ok: " + name);
        }
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
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

    private sealed class HeightWidget : IUiWidget
    {
        string IUiWidget.Kind => LabelKind;

        public void Configure(UiElementSpec spec)
        {
        }

        public void Validate(IUiBindings bindings, string elementPath)
        {
        }

        public float Measure(UiWidgetContext ctx)
        {
            return 10f;
        }

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
        }
    }
}
