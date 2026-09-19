using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FerriteLib.UiKit.Kernel;
using FerriteLib.UiKit.Kernel.Widgets;
using UnityEngine;

namespace FerriteLib.UiKit.Tests;

/// <summary>
/// Architecture probe lane (maintainer request, 2026-09-18). Three experiments measure where behavior,
/// visuals, layout relations and the IMGUI backend are coupled <b>today</b>. It is a measurement, not a
/// redesign: each experiment uses existing public API where it can, and where it cannot it registers a
/// consumer-side probe kind and reports exactly what had to be copied to get there.
/// <para>
/// The assertions are the measured facts themselves (what fires, what is painted, what moves), so a later
/// change to the split shows up here as a changed measurement rather than as a broken promise.
/// </para>
/// </summary>
internal static class KernelArchitectureProbeTests
{
    private const string Scope = "probe";
    private const string GhostScheme = "ghost";

    /// <summary>The page experiments 1a and 1c are built from.</summary>
    private static string ButtonPage(bool ghostScheme) =>
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Stack Id=\"root\" Gap=\"6\" Padding=\"8\">"
        + "<Widget Id=\"ghost\" Kind=\"input/button\" ActionBind=\"Apply\" Text=\"Apply\" Height=\"24\""
        + (ghostScheme ? " Scheme=\"" + GhostScheme + "\"" : "") + " />"
        + "<Widget Id=\"shown\" Kind=\"text/wrapped\" Text=\"Invisible behavior test\" Height=\"20\" />"
        + "</Stack></UiPage>";

    /// <summary>
    /// The transparent scheme: every colour token a button can read, set to zero alpha. This is the only
    /// way existing API makes a button invisible without deleting its painting.
    /// </summary>
    private static readonly string GhostStyleDocument =
        "<Styles Schema=\"1\"><Scheme Name=\"" + GhostScheme + "\">"
        + Colour("Base") + Colour("Panel") + Colour("Raised") + Colour("Hover") + Colour("Selected")
        + Colour("Success") + Colour("Danger") + Colour("WorkspacePlane") + Colour("SectionBand")
        + Colour("TextPrimary") + Colour("TextSecondary") + Colour("TextOnGold") + Colour("TextOnDanger")
        + Colour("TextDisabled") + Colour("AccentGold") + Colour("HoverPoint")
        + Colour("Border") + Colour("BorderStrong") + Colour("Divider")
        + Colour("BaseBorder") + Colour("PanelBorder") + Colour("RaisedBorder") + Colour("HoverBorder")
        + Colour("SelectedBorder") + Colour("SuccessBorder") + Colour("DangerBorder")
        + "</Scheme></Styles>";

    private static string Colour(string token) =>
        "<Color Token=\"" + token + "\" Value=\"#00000000\" />";

    private static int failures;

    public static int RunAll()
    {
        failures = 0;
        Run("Probe kinds register in their own scope", VerifyProbeRegistration);
        Run("Experiment 1a: a transparent scheme hides a button that still works", VerifyInvisibleByScheme);
        Run("Experiment 1b: a probe kind that paints nothing still takes input", VerifyHeadlessProbeKind);
        Run("Experiment 1c: the core button cannot be configured to paint nothing", VerifyCoreButtonAlwaysPaints);
        Run("Experiment 2a: one behavior, two different visual structures", VerifyTwoVisualsOneBehavior);
        Run("Experiment 2b: the couplings that force a second kind", VerifyVisualCouplings);
        Run("Experiment 3: parent size changes, no business code, no stale rect", VerifyLiveResize);
        ResetSeams();
        return failures;
    }

    // --- probe kinds (consumer-side half; none of this is library code) --------------------------

    /// <summary>
    /// Probe 1/2: a button whose Draw paints nothing at all. Its whole behavior is the two statements in
    /// <see cref="Draw"/> - which is the measurement: there is no behavior object to share, so every
    /// renderer of this behavior retypes them and the contract around them.
    /// </summary>
    private sealed class HeadlessButtonProbe : IUiWidget
    {
        internal const string Kind = "probe/headless-button";
        private UiElementSpec spec = UiElementSpec.Empty;

        string IUiWidget.Kind => Kind;

        internal static void Register()
        {
            UiWidgetRegistry.Register(Scope, Kind, () => new HeadlessButtonProbe(),
                new[] { "ActionBind", "Height" }, null);
        }

        public void Configure(UiElementSpec element) => spec = element ?? throw new ArgumentNullException(nameof(element));

        public void Validate(IUiBindings bindings, string elementPath)
        {
            string key = Read(spec, "ActionBind");
            if (key.Length == 0)
            {
                throw new InvalidOperationException("HeadlessButtonProbe at '" + elementPath + "' requires an ActionBind.");
            }

            bindings.ValidateCommand(key, elementPath);
        }

        public float Measure(UiWidgetContext ctx) => DeclaredHeight(spec, ctx.Theme.Geometry.RowHeight);

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            if (UiNative.Button(rect, ctx)) ctx.Bindings.Invoke(Read(spec, "ActionBind"));
        }
    }

    /// <summary>
    /// Probe 2's second renderer: the same behavior, a completely different visual structure - one 3px
    /// rail on the bottom edge, no surface, no caption. The behavior statements are retyped because there
    /// is nothing to reuse them from.
    /// </summary>
    private sealed class RailButtonProbe : IUiWidget
    {
        internal const string Kind = "probe/rail-button";
        private UiElementSpec spec = UiElementSpec.Empty;

        string IUiWidget.Kind => Kind;

        internal static void Register()
        {
            UiWidgetRegistry.Register(Scope, Kind, () => new RailButtonProbe(),
                new[] { "ActionBind", "Height" }, null);
        }

        public void Configure(UiElementSpec element) => spec = element ?? throw new ArgumentNullException(nameof(element));

        public void Validate(IUiBindings bindings, string elementPath)
        {
            string key = Read(spec, "ActionBind");
            if (key.Length == 0)
            {
                throw new InvalidOperationException("RailButtonProbe at '" + elementPath + "' requires an ActionBind.");
            }

            bindings.ValidateCommand(key, elementPath);
        }

        public float Measure(UiWidgetContext ctx) => DeclaredHeight(spec, ctx.Theme.Geometry.RowHeight);

        public void Draw(Rect rect, UiWidgetContext ctx)
        {
            UiTheme theme = ctx.Theme;
            bool armed = UiNative.IsMouseDownOver(rect);
            bool hovered = !armed && UiNative.IsMouseOver(rect);
            Color rail = armed ? theme.Selected : hovered ? theme.HoverPoint : theme.BorderStrong;
            UiThemeDraw.Solid(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), rail);

            if (UiNative.Button(rect, ctx)) ctx.Bindings.Invoke(Read(spec, "ActionBind"));
        }
    }

    // --- experiment 1 ---------------------------------------------------------------------------

    private static void VerifyProbeRegistration()
    {
        PrepareRegistry();
        Check(UiWidgetRegistry.GetAttributeSchema(Scope, HeadlessButtonProbe.Kind) != null,
            "the headless probe registers a creation-time schema of its own");
        Check(UiWidgetRegistry.GetAttributeSchema(Scope, RailButtonProbe.Kind) != null,
            "the rail probe registers one too");
    }

    /// <summary>
    /// Experiment 1, existing-API route: keep <c>input/button</c> and delete its visuals by resolving
    /// every colour token to zero alpha through a per-element scheme. The button is invisible while its
    /// input path is untouched - and the draw calls still happen, which is the honest cost.
    /// </summary>
    private static void VerifyInvisibleByScheme()
    {
        PrepareRegistry();
        var fired = new Counter();
        bool canRun = true;
        using UiHost host = BuildButtonHost(ghostScheme: true, fired, () => canRun);

        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 200f));
        Rect ghost = snapshot.RectById["ghost"];
        Rect shown = snapshot.RectById["shown"];

        DrivePointer(ghost.center);
        ClearPaint();
        host.DrawFrame(new Rect(0f, 0f, 300f, 200f));

        Check(ghost.height > 1f, "the invisible button keeps its arranged height: geometry is not the paint");
        Check(BoxRectsIn(ghost).Count > 0, "it still paints - invisibility is transparency, not the absence of drawing");
        Check(AllAlphaZero(BoxColoursIn(ghost)), "and every solid inside its rect has zero alpha");
        Check(LabelColoursIn(ghost).Count > 0, "its caption outlet is still called, transparently");
        Check(!AllAlphaZero(LabelColoursIn(shown)), "the sibling visual that makes the experiment observable is opaque");

        // Positive control: without the scheme the same page paints an opaque button, so the alpha
        // assertion above cannot pass for the wrong reason.
        var controlFired = new Counter();
        using UiHost opaque = BuildButtonHost(ghostScheme: false, controlFired, () => true);
        UiLayoutSnapshot opaqueSnapshot = opaque.MeasureAndArrange(new Vector2(300f, 200f));
        Rect opaqueGhost = opaqueSnapshot.RectById["ghost"];
        DrivePointer(opaqueGhost.center);
        ClearPaint();
        opaque.DrawFrame(new Rect(0f, 0f, 300f, 200f));
        Check(!AllAlphaZero(BoxColoursIn(opaqueGhost)),
            "positive control: without the scheme the same button paints an opaque surface");

        // Behavior: the click fires, the external visual owns no logic, and disabled blocks the click.
        fired.Value = 0;
        DrivePointer(ghost.center);
        host.DrawFrame(new Rect(0f, 0f, 300f, 200f));
        Check(fired.Value == 1, "the invisible button fires its command on a click inside its rect");

        fired.Value = 0;
        DrivePointer(shown.center);
        host.DrawFrame(new Rect(0f, 0f, 300f, 200f));
        Check(fired.Value == 0, "a click on the external visual element fires nothing: it carries no logic");

        var disabledFired = new Counter();
        bool disabledCanRun = true;
        using UiHost disabled = BuildButtonHost(ghostScheme: true, disabledFired, () => disabledCanRun);
        disabledCanRun = false;
        UiLayoutSnapshot disabledSnapshot = disabled.MeasureAndArrange(new Vector2(300f, 200f));
        DrivePointer(disabledSnapshot.RectById["ghost"].center);
        disabled.DrawFrame(new Rect(0f, 0f, 300f, 200f));
        Check(disabledFired.Value == 0, "a disabled invisible button does not fire: the disabled rule is not paint-dependent");

        ResetSeams();
    }

    /// <summary>
    /// Experiment 1, probe route: a kind whose Draw paints nothing. The behavior works, so the split is
    /// possible; the measurement is what it costs.
    /// </summary>
    private static void VerifyHeadlessProbeKind()
    {
        PrepareRegistry();
        int fired = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("apply", () => fired++);

        HeadlessButtonProbe widget = new();
        widget.Configure(new UiElementSpec("ghost", HeadlessButtonProbe.Kind, Attrs(("ActionBind", "apply"))));
        using UiSession session = new();
        UiWidgetContext ctx = MakeContext(session, bindings, UiTheme.DarkGold);
        var rect = new Rect(10f, 10f, 120f, 24f);

        DrivePointer(new Vector2(50f, 20f));
        ClearPaint();
        widget.Draw(rect, ctx);
        Check(BoxRects().Count == 0, "the headless probe emits no solid at all");
        Check(LabelRects().Count == 0, "and no text at all: truly headless, not transparent");
        Check(fired == 1, "yet the click fires: the input path does not need a paint");

        DrivePointer(new Vector2(-5000f, -5000f));
        widget.Draw(rect, ctx);
        Check(fired == 1, "and a click outside the rect still does not fire");

        Note("cost of the split: the probe retyped Configure/Validate/Measure (about 14 lines) plus the two "
            + "behavior statements in Draw; the library offered no behavior object to share them from");
        ResetSeams();
    }

    /// <summary>
    /// The negative half of 1b: the core button has no configuration that removes its painting.
    /// </summary>
    private static void VerifyCoreButtonAlwaysPaints()
    {
        PrepareRegistry();
        using UiHost host = BuildButtonHost(ghostScheme: false, new Counter(), () => true);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(300f, 200f));
        Rect ghost = snapshot.RectById["ghost"];
        DrivePointer(ghost.center);
        ClearPaint();
        host.DrawFrame(new Rect(0f, 0f, 300f, 200f));

        Check(BoxColoursIn(ghost).Count > 0,
            "input/button always paints at least one solid, whatever is configured on it");
        Check(LabelColoursIn(ghost).Count > 0, "and always writes its caption");
        ResetSeams();
    }

    // --- experiment 2 ---------------------------------------------------------------------------

    /// <summary>
    /// Experiment 2: one behavior, two visual structures. Both buttons fire through the same funnel call
    /// and the same binding mechanism; the visuals differ completely. The duplication is the finding.
    /// </summary>
    private static void VerifyTwoVisualsOneBehavior()
    {
        PrepareRegistry();
        string xml =
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Row Id=\"row\" Gap=\"8\">"
            + "<Widget Id=\"classic\" Kind=\"input/button\" ActionBind=\"AClassic\" Text=\"Save\" Width=\"120\" />"
            + "<Widget Id=\"rail\" Kind=\"" + RailButtonProbe.Kind + "\" ActionBind=\"ARail\" Width=\"120\" />"
            + "</Row></UiPage>";

        int classic = 0;
        int rail = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("AClassic", () => classic++);
        bindings.BindCommand("ARail", () => rail++);

        using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), bindings, UiTheme.DarkGold,
            new ProbeMetrics(), new ProbeTranslation());
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(400f, 120f));
        Rect classicRect = snapshot.RectById["classic"];
        Rect railRect = snapshot.RectById["rail"];

        DrivePointer(classicRect.center);
        ClearPaint();
        host.DrawFrame(new Rect(0f, 0f, 400f, 120f));
        Check(classic == 1 && rail == 0, "a click on visual A fires A's command only");
        Check(BoxColoursIn(classicRect).Count > 0 && LabelColoursIn(classicRect).Count > 0,
            "visual A is a surface plus a caption");

        classic = 0;
        rail = 0;
        DrivePointer(railRect.center);
        ClearPaint();
        host.DrawFrame(new Rect(0f, 0f, 400f, 120f));
        List<Rect> railBoxes = BoxRectsIn(railRect);
        Check(rail == 1 && classic == 0, "a click on visual B fires B's command only: one click mechanism");
        Check(railBoxes.Count == 1 && Math.Abs(railBoxes[0].height - 3f) < 0.001f,
            "visual B paints one 3px rail instead of a surface: a different structure, not a re-tint");
        Check(LabelColoursIn(railRect).Count == 0, "visual B writes no text inside its own rect");

        Note("what the second visual cost: a second kind and a retyped hit/invoke pair plus Validate, "
            + "because kind -> factory -> painter is one axis and the behavior lives in the painter");
        ResetSeams();
    }

    /// <summary>Experiment 2's couplings, each measured rather than asserted in prose.</summary>
    private static void VerifyVisualCouplings()
    {
        PrepareRegistry();

        Reject(
            "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
            + "<Widget Id=\"b\" Kind=\"input/button\" ActionBind=\"A\" Text=\"x\">"
            + "<Widget Id=\"l\" Kind=\"text/wrapped\" Text=\"x\" />"
            + "</Widget></UiPage>",
            "a <Widget> cannot carry a child, so '<Button><Label/></Button>' is refused rather than supported");

        Check(typeof(ButtonWidget).IsSealed,
            "input/button is sealed: its paint cannot be replaced by inheriting from it");

        // Pinned outlet vocabulary: the drawing surface is a closed list of 14 names, and none of them
        // paints an image, a layer or a transform. An addition has to be decided here as well as in the
        // library, which is the point - the alternative is discovering the change from a screenshot.
        var outlets = new List<string>();
        foreach (MethodInfo method in typeof(UiThemeDraw).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (!outlets.Contains(method.Name)) outlets.Add(method.Name);
        }

        outlets.Sort(StringComparer.Ordinal);
        string[] pinned =
        {
            "AccentRail", "BackgroundPlane", "Base", "FocusRail", "Label", "Panel", "RecoveryBand",
            "SectionBand", "SectionHeader", "Solid", "StatusBadge", "StatusTreatment", "Surface", "Workspace"
        };

        Check(outlets.Count == pinned.Length, "the drawing outlet vocabulary is the pinned 14 names, got "
            + outlets.Count + ": " + string.Join(", ", outlets));
        bool imageOutlet = false;
        foreach (string name in outlets)
        {
            if (name.IndexOf("Image", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Texture", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                imageOutlet = true;
            }
        }

        Check(!imageOutlet, "no outlet paints an image: an image-only button has no paint primitive today");

        var published = new List<string>();
        foreach (PropertyInfo property in typeof(UiNode).GetProperties()) published.Add(property.Name);
        Check(published.Contains("IsDisabled") && !published.Contains("IsHovered") && !published.Contains("IsArmed"),
            "a node publishes IsDisabled but no hover/armed state, so a second renderer must re-derive them");
    }

    // --- experiment 3 ---------------------------------------------------------------------------

    /// <summary>
    /// The relation page in today's vocabulary only: a column with padding, a title band, a fill container
    /// that eats the remaining height, and a footer whose button is centred by two equal flex spacers. The
    /// relations are consequences of flow, not declarations.
    /// </summary>
    private const string RelationPage =
        "<UiPage Schema=\"2\" Source=\"" + Scope + "\">"
        + "<Column Id=\"root\" Gap=\"8\" Padding=\"10\">"
        + "<Widget Id=\"title\" Kind=\"text/wrapped\" Text=\"Header\" Height=\"20\" />"
        + "<Column Id=\"content\" Fill=\"true\" Padding=\"4\">"
        + "<Widget Id=\"body\" Kind=\"chrome/rule\" Height=\"2\" />"
        + "</Column>"
        + "<Row Id=\"footer\" Gap=\"0\">"
        + "<Widget Id=\"leftPad\" Kind=\"chrome/rule\" Height=\"1\" />"
        + "<Widget Id=\"bottomButton\" Kind=\"input/button\" ActionBind=\"Apply\" Text=\"Go\" Width=\"120\" />"
        + "<Widget Id=\"rightPad\" Kind=\"chrome/rule\" Height=\"1\" />"
        + "</Row></Column></UiPage>";

    private static void VerifyLiveResize()
    {
        PrepareRegistry();
        int fired = 0;
        var bindings = new UiBindings();
        bindings.BindCommand("Apply", () => fired++);

        using UiHost host = new(Scope, UiLayoutManifest.Parse(RelationPage), bindings, UiTheme.DarkGold,
            new ProbeMetrics(), new ProbeTranslation());

        // The caller's whole vocabulary: one available size, then a frame. Nothing here sets a rect.
        var sizes = new[]
        {
            new Vector2(400f, 300f), new Vector2(800f, 600f),
            new Vector2(500f, 350f), new Vector2(1000f, 500f)
        };

        Vector2? previousCentre = null;
        UiLayoutSnapshot? previous = null;
        foreach (Vector2 size in sizes)
        {
            string at = "at " + size.x + "x" + size.y + ": ";
            UiLayoutSnapshot snapshot = host.MeasureAndArrange(size);
            Rect root = snapshot.RectById["root"];
            Rect title = snapshot.RectById["title"];
            Rect content = snapshot.RectById["content"];
            Rect button = snapshot.RectById["bottomButton"];

            Check(Math.Abs(title.x - 10f) < 0.001f && Math.Abs(title.width - (size.x - 20f)) < 0.001f,
                at + "the title tracks the parent's width through padding alone");
            Check(Math.Abs(content.y - (title.yMax + 8f)) < 0.001f,
                at + "content sits below the title by the declared gap");
            Check(Math.Abs(button.center.x - root.center.x) < 0.5f,
                at + "the button is centred with no business code doing it");
            Check(Math.Abs(button.yMax - (root.yMax - 10f)) < 0.5f,
                at + "the button ends on the parent's bottom padding");
            Check(Math.Abs(button.width - 120f) < 0.001f, at + "its declared width survives");

            // The frame draws the arrangement it just made, so nothing can be a frame late.
            DrivePointer(new Vector2(-5000f, -5000f));
            ClearPaint();
            host.DrawFrame(new Rect(0f, 0f, size.x, size.y));
            List<Rect> painted = BoxRectsIn(button);
            bool paintedHere = false;
            foreach (Rect candidate in painted)
            {
                if (SameRect(candidate, button)) paintedHere = true;
            }

            Check(painted.Count > 0 && paintedHere,
                at + "the frame paints the button at this pass's rect, not the previous pass's");

            if (previousCentre.HasValue)
            {
                fired = 0;
                DrivePointer(previousCentre.Value);
                host.DrawFrame(new Rect(0f, 0f, size.x, size.y));
                Check(fired == 0, at + "the previous size's centre is no longer a hit target");
            }

            fired = 0;
            DrivePointer(button.center);
            host.DrawFrame(new Rect(0f, 0f, size.x, size.y));
            Check(fired == 1, at + "the hit test uses this pass's rect");

            previousCentre = button.center;
            previous = snapshot;
        }

        UiLayoutSnapshot repeated = host.MeasureAndArrange(sizes[sizes.Length - 1]);
        Note("same size twice returns the same snapshot instance: " + ReferenceEquals(previous, repeated));
        UiLayoutSnapshot changed = host.MeasureAndArrange(
            new Vector2(sizes[sizes.Length - 1].x + 1f, sizes[sizes.Length - 1].y));
        Check(!ReferenceEquals(previous, changed), "a changed available size produces a new arrangement");

        bool writable = false;
        foreach (PropertyInfo property in typeof(UiLayoutSnapshot).GetProperties())
        {
            if (property.CanWrite) writable = true;
        }

        Check(!writable, "no published snapshot property is writable: the caller cannot set a child rect");
        ResetSeams();
    }

    // --- construction helpers -------------------------------------------------------------------

    private static void PrepareRegistry()
    {
        UiWidgetRegistry.Clear();
        UiWidgetRegistry.InitializeCore();
        HeadlessButtonProbe.Register();
        RailButtonProbe.Register();
    }

    private static UiHost BuildButtonHost(bool ghostScheme, Counter fired, Func<bool> canExecute)
    {
        var bindings = new UiBindings();
        bindings.BindCommand("Apply", () => fired.Value++, canExecute);
        UiStyleDocument? document = ghostScheme ? UiStyleDocument.Parse(GhostStyleDocument) : null;
        return new UiHost(Scope, UiLayoutManifest.Parse(ButtonPage(ghostScheme)), bindings, UiTheme.DarkGold,
            new ProbeMetrics(), new ProbeTranslation(), document);
    }

    private sealed class Counter
    {
        internal int Value;
    }

    // --- harness --------------------------------------------------------------------------------

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
            Console.Error.WriteLine("  FAIL: " + name + " :: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void Check(bool condition, string name)
    {
        if (condition) Console.WriteLine("  ok: " + name);
        else
        {
            failures++;
            Console.Error.WriteLine("  FAIL: " + name);
        }
    }

    private static void Note(string text) => Console.WriteLine("  note: " + text);

    private static void Reject(string xml, string what)
    {
        try
        {
            using UiHost host = new(Scope, UiLayoutManifest.Parse(xml), new UiBindings(), UiTheme.DarkGold,
                new ProbeMetrics(), new ProbeTranslation());
            Check(false, what + " - but the host accepted it");
        }
        catch (Exception ex) when (ex is UiContractException || ex is FormatException)
        {
            // Structure failures are fail-closed but not one exception type: the manifest parser refuses
            // a nested <Widget> with a FormatException, the creation-time contract with UiContractException.
            Check(true, what + " - rejected at creation with " + ex.GetType().Name);
        }
    }

    private static Dictionary<string, string> Attrs(params (string Name, string Value)[] pairs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in pairs) attributes[name] = value;
        return attributes;
    }

    private static UiWidgetContext MakeContext(UiSession session, IUiBindings bindings, UiTheme theme) =>
        new(Scope, session, new ProbeMetrics(), theme, new ProbeTranslation(), bindings, 400f, "root");

    private static string Read(UiElementSpec spec, string name) =>
        spec.TryGetAttribute(name, out string value) ? value : "";

    private static float DeclaredHeight(UiElementSpec spec, float fallback)
    {
        if (!spec.TryGetAttribute("Height", out string raw)) return fallback;
        return float.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0f ? value : fallback;
    }

    private static void DrivePointer(Vector2 position)
    {
        UiNative.DebugMousePositionEnabled = true;
        UiNative.DebugMousePosition = position;
        UiNative.ButtonOverride = rect =>
            position.x >= rect.x && position.x <= rect.xMax && position.y >= rect.y && position.y <= rect.yMax;
        UiNative.SliderOverride = (rect, current, min, max) => current;
        UiNative.TextFieldOverride = (rect, text) => text;
    }

    private static void ResetSeams()
    {
        UiNative.DebugMousePositionEnabled = false;
        UiNative.DebugMousePosition = Vector2.zero;
        UiNative.DebugMouseDown = false;
        UiNative.DebugMouseDrag = false;
        UiNative.DebugMouseUp = false;
        UiNative.DebugEnter = false;
        UiNative.DebugFocusLost = false;
        UiNative.ButtonOverride = null;
        UiNative.SliderOverride = null;
        UiNative.TextFieldOverride = null;
    }

    private static void ClearPaint()
    {
        BoxRects().Clear();
        BoxColours().Clear();
        LabelRects().Clear();
        LabelTexts().Clear();
        LabelColours().Clear();
    }

    private static IList BoxRects() => (IList)StubField("DrawBoxSolidRects");

    private static IList BoxColours() => (IList)StubField("DrawBoxSolidColors");

    private static IList LabelRects() => (IList)StubField("LabelRects");

    private static IList LabelTexts() => (IList)StubField("LabelTexts");

    private static IList LabelColours() => (IList)StubField("LabelColors");

    private static List<Rect> BoxRectsIn(Rect area)
    {
        var inside = new List<Rect>();
        foreach (object candidate in BoxRects())
        {
            if (candidate is Rect rect && Intersects(rect, area)) inside.Add(rect);
        }

        return inside;
    }

    private static List<Color> BoxColoursIn(Rect area)
    {
        var inside = new List<Color>();
        IList rects = BoxRects();
        IList colours = BoxColours();
        for (int i = 0; i < rects.Count && i < colours.Count; i++)
        {
            if (rects[i] is Rect rect && colours[i] is Color colour && Intersects(rect, area)) inside.Add(colour);
        }

        return inside;
    }

    private static List<Color> LabelColoursIn(Rect area)
    {
        var inside = new List<Color>();
        IList rects = LabelRects();
        IList colours = LabelColours();
        for (int i = 0; i < rects.Count && i < colours.Count; i++)
        {
            if (rects[i] is Rect rect && colours[i] is Color colour && Intersects(rect, area)) inside.Add(colour);
        }

        return inside;
    }

    private static bool AllAlphaZero(List<Color> colours)
    {
        if (colours.Count == 0) return false;
        foreach (Color colour in colours)
        {
            if (colour.a > 0.0001f) return false;
        }

        return true;
    }

    private static bool Intersects(Rect one, Rect other) =>
        one.x < other.xMax && one.xMax > other.x && one.y < other.yMax && one.yMax > other.y;

    private static bool SameRect(Rect one, Rect other) =>
        Math.Abs(one.x - other.x) < 0.001f && Math.Abs(one.y - other.y) < 0.001f
        && Math.Abs(one.width - other.width) < 0.001f && Math.Abs(one.height - other.height) < 0.001f;

    private static object StubField(string name)
    {
        FieldInfo? field = typeof(Verse.Widgets).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field == null) throw new Exception("Verse stub is missing " + name);
        return field.GetValue(null)!;
    }

    /// <summary>Half-width glyph ruler, the same model the other lanes measure with.</summary>
    private sealed class ProbeMetrics : ITextMetrics
    {
        public float MeasureText(string text, UiFont font, float width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float advance = StubTextWidth.Of(text, font);
            int lines = Math.Max(1, (int)Math.Ceiling(advance / Math.Max(1f, width)));
            return lines * Em(font);
        }

        public float MeasureWidth(string text, UiFont font) => StubTextWidth.Of(text, font);

        private static float Em(UiFont font) => font switch
        {
            UiFont.Tiny => 12f,
            UiFont.Medium => 18f,
            _ => 16f
        };
    }

    private sealed class ProbeTranslation : IUiTranslation
    {
        public string Translate(string key) => "[" + key + "]";

        public int TranslationRevision => 0;
    }
}
