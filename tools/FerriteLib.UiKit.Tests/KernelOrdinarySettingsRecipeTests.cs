using System;
using System.Collections.Generic;
using System.Globalization;

using FerriteLib.UiKit.Kernel;
using UnityEngine;
using Verse;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// B (0.7): the ordinary-settings recipe — one neutral settings group built ONLY from public surface:
/// existing atoms and containers, typed bindings, a plain C# model, and the existing window host.
/// This file is the source of truth the guide links; the guide does not keep a second listing.
/// <list type="bullet">
/// <item>The recipe halves (<see cref="SettingsModel"/>, <see cref="ValueRow"/>, <see cref="PageXml"/>,
/// <see cref="RegisterBindings"/>) are what a consumer copies: no rectangle, no Measure/Draw override,
/// no custom widget, and no second notification framework anywhere in them.</item>
/// <item>Explicit notification is the basic path: the model's mutation methods announce their own keys,
/// and BOTH the UI setter and a non-UI writer reach that same method. The optional
/// <c>INotifyPropertyChanged</c>/<c>UiNotifyAdapter</c> route is documented in the guide and already
/// carried by <c>KernelNotifyAdapterTests</c>; it is not needed by this recipe.</item>
/// <item>Only the scoring lanes below touch internal seams (the <c>UiNative</c> overrides); nothing they
/// use is part of what a consumer would copy.</item>
/// </list>
/// Like every library fixture this is not consumption evidence ("our own demo is not consumption",
/// <c>AGENTS.md</c>): it proves the recipe compiles, arranges, draws and recovers under the stub.
/// </summary>
internal static class KernelOrdinarySettingsRecipeTests
{
    private const string Consumer = "recipe";
    private const string WindowKind = "settings";
    private const string Scope = Consumer + "/" + WindowKind;


    // ---------------- scoring ----------------

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("The whole page arranges from the theme; the page authored no geometry", VerifyNoAuthoredGeometry);
        Run("An analogous setting is one helper call plus one BindValue", VerifySecondSettingNeedsNoNewCodeShape);
        Run("Slider, number field and model share one binding in both directions", VerifySharedBindingRoundTrip);
        Run("A paint-only change reuses the arrangement; a measured change re-bands", VerifyPaintVersusMeasure);
        Run("The advanced group shows and hides without reopening the page", VerifyShowHideWithoutReopen);
        Run("A long localized help string re-bands instead of clipping", VerifyLongHelpRebands);
        Run("Narrow content stacks the value row inside the scroller", VerifyNarrowPolicyAndScroll);
        Run("Close and reopen release and re-acquire exactly one model subscription", VerifyCloseReopenSubscriptions);
        return failures;
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("  ok: " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.Message);
        }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }

    // ---------------- the copyable recipe: model ----------------

    /// <summary>An ordinary C# class: no base type, no attributes, no reflection, no DI.</summary>
    internal sealed class SettingsModel
    {
        private readonly UiBindings bindings;

        internal SettingsModel(UiBindings bindings)
        {
            this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        /// <summary>
        /// The model's OWN ledger, for the subscription arithmetic the close/reopen lane checks;
        /// a real page uses it for anything the model must offer its owner.
        /// </summary>
        internal event Action? Changed;

        internal int SubscriberCount => Changed is null ? 0 : Changed.GetInvocationList().Length;

        private float level = 0.5f;
        internal float Level => level;

        /// <summary>
        /// The owned mutation path. The UI setter and every non-UI writer call THIS, and this is the
        /// one place that announces: <c>UiBindings.Set</c> stores a value, it does not notify.
        /// "level" is announced as Paint because moving a value moves no rect — the slider track and
        /// the number field's text are read live on the next paint.
        /// </summary>
        internal void SetLevel(float value)
        {
            float clamped = Clamp01(value);
            if (clamped == level) return;
            level = clamped;
            Changed?.Invoke();
            bindings.NotifyChanged("level");
        }

        private float balance;
        internal float Balance => balance;

        internal void SetBalance(float value)
        {
            float clamped = Clamp01(value);
            if (clamped == balance) return;
            balance = clamped;
            Changed?.Invoke();
            bindings.NotifyChanged("balance");
        }

        private string help = "Keeps the output between the limits the hardware reports.";
        internal string Help => help;

        /// <summary>
        /// A changed LABEL is measured content: the text atom's band depends on the string, so this
        /// announces "level-help", which the page declares and which invalidates measure. Notifying
        /// an undeclared key would leave the old band in place — the guide names this pair explicitly.
        /// </summary>
        internal void SetHelp(string value)
        {
            if (string.Equals(value, help, StringComparison.Ordinal)) return;
            help = value ?? "";
            Changed?.Invoke();
            bindings.NotifyChanged("level-help");
        }

        internal string Status => "current level: " + level.ToString("0.##", CultureInfo.InvariantCulture);

        private bool showAdvanced;
        internal bool ShowAdvanced => showAdvanced;

        /// <summary>
        /// The group appears and disappears: structure. Reaching it through the model — not through
        /// a page-local bool — is what lets the non-UI half of a consumer flip the same door.
        /// </summary>
        internal void ToggleAdvanced()
        {
            showAdvanced = !showAdvanced;
            Changed?.Invoke();
            bindings.NotifyChanged("show-advanced");
        }

        internal IReadOnlyList<string> QualityOptions => new[] { "Low", "Medium", "High" };
        internal string Quality { get; set; } = "Medium";

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }

    // ---------------- the copyable recipe: page ----------------

    /// <summary>
    /// One value row = a labeled slider plus a number field sharing ONE typed binding. An analogous
    /// setting is one more call to this and one more <c>BindValue</c>: the helper contains no rect,
    /// no measure and no draw, and neither does anything else on the page.
    /// </summary>
    private static string ValueRow(string id, string label) =>
        "<Row Id=\"" + id + "-row\" Gap=\"6\" Breakpoint=\"260\" Narrow=\"Column\">"
        + "<Widget Id=\"" + id + "\" Kind=\"input/slider\" Bind=\"" + id + "\" Label=\"" + label + "\" Min=\"0\" Max=\"1\" />"
        + "<Widget Id=\"" + id + "-number\" Kind=\"input/number-field\" Bind=\"" + id + "\" Min=\"0\" Max=\"1\" />"
        + "</Row>";

    private static string PageXml(bool withBalanceSetting)
    {
        return
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Scroll Id=\"scroller\" Height=\"240\">"
            + "<Column Id=\"body\" Gap=\"6\" Padding=\"6\">"
            + ValueRow("level", "Level")
            + (withBalanceSetting ? ValueRow("balance", "Balance") : "")
            + "<Widget Id=\"level-help\" Kind=\"text/wrapped\" Bind=\"level-help\" />"
            + "<Widget Id=\"quality\" Kind=\"input/dropdown\" Bind=\"quality\" OptionsBind=\"quality-options\" Label=\"Quality\" />"
            + "<Widget Id=\"status\" Kind=\"text/wrapped\" Bind=\"level-status\" />"
            + "<Widget Id=\"toggle\" Kind=\"input/button\" ActionBind=\"toggle-advanced\" Text=\"Advanced\" />"
            + "<Section Id=\"advanced\" Title=\"Advanced\" VisibleKey=\"show-advanced\">"
            + "<Widget Id=\"advanced-note\" Kind=\"text/wrapped\" Text=\"Extra room for later settings.\" />"
            + "</Section>"
            + "</Column>"
            + "</Scroll>"
            + "</UiPage>";
    }

    /// <summary>
    /// The whole wiring: typed values, an options list for the dropdown, one read-only display, one
    /// command behind the button, and a visible-key for the group. The invalidation class declared
    /// next to each key is the announcement contract the guide walks through.
    /// </summary>
    private static void RegisterBindings(UiBindings bindings, SettingsModel model)
    {
        bindings.BindValue("level", () => model.Level, v => model.SetLevel(v), UiInvalidation.Paint);
        bindings.BindValue("balance", () => model.Balance, v => model.SetBalance(v), UiInvalidation.Paint);
        bindings.BindReadOnly("level-help", () => model.Help, UiInvalidation.Measure);
        bindings.BindReadOnly("level-status", () => model.Status, UiInvalidation.Paint);
        bindings.BindValue("quality", () => model.Quality, v => model.Quality = v);
        bindings.BindOptions("quality-options", () => model.QualityOptions);
        bindings.BindReadOnly("show-advanced", () => model.ShowAdvanced, UiInvalidation.Structure);
        bindings.BindCommand("toggle-advanced", () => model.ToggleAdvanced());
    }

    // --- lanes -----------------------------------------------------------------------------------

    private static void VerifyNoAuthoredGeometry()
    {
        (SettingsModel model, UiBindings bindings) = Recipe();
        using UiHost host = Host(bindings);
        UiLayoutSnapshot wide = host.MeasureAndArrange(new Vector2(400f, 300f));

        foreach (string id in new[] { "level", "level-number", "level-help", "quality", "status", "toggle" })
        {
            Check(wide.RectById.ContainsKey(id), "the engine placed '" + id + "' with no author geometry");
        }

        UiTheme theme = new();
        Check(Near(theme.Geometry.RowHeight, wide.RectById["level"].height),
            "the slider's band is the theme's density row height, not a page literal");
        Check(!wide.RectById.ContainsKey("advanced"),
            "the hidden group is not laid out at all (its VisibleKey is false)");
    }

    private static void VerifySecondSettingNeedsNoNewCodeShape()
    {
        (SettingsModel model, UiBindings bindings) = Recipe();
        using UiHost host = Host(bindings, withBalance: true);
        UiLayoutSnapshot wide = host.MeasureAndArrange(new Vector2(400f, 300f));

        Check(wide.RectById.ContainsKey("balance") && wide.RectById.ContainsKey("balance-number"),
            "the analogous row placed like the first one");
        UiTheme theme = new();
        Check(Near(theme.Geometry.RowHeight, wide.RectById["balance"].height),
            "with the same theme-derived band and no new code shape");

        model.SetBalance(0.75f);
        host.BeginFrame();
        host.Draw(new Rect(0f, 0f, 400f, 300f), wide);
        host.EndFrame();
        Check(Near(0.75f, model.Balance) && Near(0.5f, model.Level),
            "and its binding writes its own model field, not the first setting's");
    }

    private static void VerifySharedBindingRoundTrip()
    {
        (SettingsModel model, UiBindings bindings) = Recipe();
        using UiHost host = Host(bindings);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(400f, 300f));

        // The UI half: a real drag through the slider's native seam commits through the model.
        UiNative.SliderOverride = (rect, current, min, max) => current < 0.8f ? 0.8f : current;
        try
        {
            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 400f, 300f), snapshot);
            host.EndFrame();
        }
        finally
        {
            UiNative.SliderOverride = null;
        }

        Check(Near(0.8f, model.Level), "the slider's write reached the model through its mutation path");
        Check(Near(0.8f, bindings.Get<float>("level")), "and the same key serves every control that binds it");

        // The shared read half: the number field renders the value the slider just committed.
        string shown = "";
        UiNative.TextFieldOverride = (rect, text) => { shown = text; return text; };
        try
        {
            host.BeginFrame();
            host.Draw(new Rect(0f, 0f, 400f, 300f), snapshot);
            host.EndFrame();
        }
        finally
        {
            UiNative.TextFieldOverride = null;
        }

        Check(shown.Contains("0.8"), "the number field displays the same binding's value on the next pass");

        // The non-UI half: a writer outside the page reaches the identical path.
        model.SetLevel(0.25f);
        Check(Near(0.25f, bindings.Get<float>("level")), "an external model change lands in the binding");
    }


    private static void VerifyPaintVersusMeasure()
    {
        (SettingsModel model, UiBindings bindings) = Recipe();
        using UiHost host = Host(bindings);
        UiLayoutSnapshot first = host.MeasureAndArrange(new Vector2(400f, 300f));

        model.SetLevel(0.66f);
        UiLayoutSnapshot afterPaint = host.MeasureAndArrange(new Vector2(400f, 300f));
        Check(ReferenceEquals(first, afterPaint),
            "a Paint-class announcement reuses the arranged snapshot: the value moves no rect");

        model.SetHelp("Keeps the output between the limits the hardware reports, per directive 7.3, "
            + "and this deliberately long replacement re-measures the band it is drawn into.");
        UiLayoutSnapshot afterMeasure = host.MeasureAndArrange(new Vector2(400f, 300f));
        Check(!ReferenceEquals(first, afterMeasure),
            "a measured content change does re-arrange");
        Check(afterMeasure.RectById["level-help"].height > first.RectById["level-help"].height,
            "and the help atom's band grew to fit the longer string");
    }

    private static void VerifyShowHideWithoutReopen()
    {
        (SettingsModel model, UiBindings bindings) = Recipe();
        using UiHost host = Host(bindings);
        UiLayoutSnapshot hidden = host.MeasureAndArrange(new Vector2(400f, 300f));
        Check(!hidden.RectById.ContainsKey("advanced"), "the group starts hidden");

        model.ToggleAdvanced();
        UiLayoutSnapshot shown = host.MeasureAndArrange(new Vector2(400f, 300f));
        Check(shown.RectById.ContainsKey("advanced") && shown.RectById.ContainsKey("advanced-note"),
            "announcing the structure key opened the group in the same host, with no reopen");

        model.ToggleAdvanced();
        UiLayoutSnapshot hiddenAgain = host.MeasureAndArrange(new Vector2(400f, 300f));
        Check(!hiddenAgain.RectById.ContainsKey("advanced"), "and the same door closes it");
    }

    private static void VerifyLongHelpRebands()
    {
        (SettingsModel model, UiBindings bindings) = Recipe();
        using UiHost host = Host(bindings);
        UiLayoutSnapshot shortWay = host.MeasureAndArrange(new Vector2(400f, 300f));
        float shortBand = shortWay.RectById["level-help"].height;

        model.SetHelp("这是一段故意加长的说明文字，用来证明测量与绘制共用同一把尺；换语言、加长文案时，"
            + "它同样会重新量带而不是被裁掉，页面其余部分的位置由引擎重新分配，作者不需要改任何一行几何代码。");
        UiLayoutSnapshot wideWay = host.MeasureAndArrange(new Vector2(400f, 300f));
        Check(wideWay.RectById["level-help"].height > shortBand,
            "the longer localized string re-bands the atom instead of clipping it");

        UiLayoutSnapshot narrowWay = host.MeasureAndArrange(new Vector2(260f, 300f));
        Check(narrowWay.RectById["level-help"].height >= wideWay.RectById["level-help"].height,
            "narrower content gives it fewer columns and never less height");
    }

    private static void VerifyNarrowPolicyAndScroll()
    {
        (SettingsModel model, UiBindings bindings) = Recipe();
        using UiHost host = Host(bindings, withBalance: true);
        UiLayoutSnapshot wide = host.MeasureAndArrange(new Vector2(400f, 300f));
        Check(wide.RectById["level-number"].x > wide.RectById["level"].x,
            "wide: the row lays its controls side by side");

        UiLayoutSnapshot narrow = host.MeasureAndArrange(new Vector2(240f, 300f));
        Check(Near(narrow.RectById["level-number"].x, narrow.RectById["level"].x)
            && narrow.RectById["level-number"].y > narrow.RectById["level"].y,
            "below the declared Breakpoint the same row stacks — the narrow policy is the container's own vocabulary");

        Check(narrow.RectById.ContainsKey("scroller")
            && Near(240f, narrow.RectById["scroller"].height),
            "the overflowing column sits inside the declared Scroll viewport — scroll region, not a cut-off");
    }

    private static void VerifyCloseReopenSubscriptions()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        var bindings = new UiBindings();
        SettingsModel model = new(bindings);
        RegisterBindings(bindings, model);

        var stack = new WindowStack();
        var catalog = new UiWindowCatalog(stack, UiFocusPolicy.FollowClicks);
        UiWindowKey key = new(Consumer, WindowKind, "");
        int attach = 0;
        int detach = 0;
        UiPageWindow? window = null;

        catalog.Register(Consumer, WindowKind, null, k =>
        {
            var created = new UiPageWindow(
                k, UiLayoutManifest.Parse(PageXml(true)), bindings, UiTheme.Vanilla,
                new RecipeTranslation(), "Recipe settings", "Close", _ => "unavailable",
                new RecipeMetrics());
            created.windowRect = new Rect(0f, 0f, 600f, 400f);
            // The ownership rule the guide states: the page subscribes on attach and releases on
            // detach, so closing a window cannot leave a handler behind on the model.
            created.HostAttached += _ => { attach++; model.Changed += OnPing; };
            created.HostDetached += _ => { detach++; model.Changed -= OnPing; };
            window = created;
            return created;
        });

        void OnPing() { }

        try
        {
            Check(catalog.Open(key), "the page opens through the catalog");
            window!.WindowOnGUI();
            Check(attach == 1 && model.SubscriberCount == 1, "one host, one subscription");

            Check(catalog.Close(key), "and it closes through the catalog");
            Check(detach == 1 && model.SubscriberCount == 0, "the close released the model handler with the host");

            Check(catalog.Open(key), "the page reopens");
            window!.WindowOnGUI();
            window!.WindowOnGUI();
            Check(attach == 2 && model.SubscriberCount == 1,
                "the reopened page holds ONE subscription — not one per attach, and not one per draw");
            Check(window!.PageHost != null, "the reopened window rebuilt its own host");

            Check(catalog.Close(key), "close again");
            Check(model.SubscriberCount == 0, "and the model is left with nothing dangling");
        }
        finally
        {
            catalog.CloseAll();
            Event.current = null;
        }
    }

    // --- harness ---------------------------------------------------------------------------------

    private static (SettingsModel, UiBindings) Recipe()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        var bindings = new UiBindings();
        SettingsModel model = new(bindings);
        RegisterBindings(bindings, model);
        return (model, bindings);
    }

    private static UiHost Host(UiBindings bindings, bool withBalance = true)
    {
        return new UiHost(
            Scope,
            UiLayoutManifest.Parse(PageXml(withBalance)),
            bindings,
            UiTheme.Vanilla,
            new RecipeMetrics(),
            new RecipeTranslation());
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.01f;

    private sealed class RecipeTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";
        public int TranslationRevision => 0;
    }

    private sealed class RecipeMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float advance = StubTextWidth.Of(text, font);
            int lines = Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
            float em = font switch
            {
                UiFont.Tiny => 12f,
                UiFont.Medium => 18f,
                _ => 16f
            };
            return lines * em;
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);
    }
}
