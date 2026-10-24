using System;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel.Widgets;
/// <summary>
/// Greenfield stepper-slider composite. Uses the session-owned <see cref = "UiNative"/> primitives
/// and writes through the typed <see cref = "IUiBindingAccess"/>; no neutral command bridge.
/// The typed value-binding key is the <c>Bind</c> attribute when present, otherwise the element Id.
/// </summary>
internal sealed class StepperSliderWidget : IUiWidget, IUiMeasureParticipant, IUiLabelledWidget, IUiLayoutDiagnosticSource, IUiPointerLayout, IUiRetainedDisplay
{
    public const string Kind = "input/stepper-slider";

    private const float DefaultHeight = 28f;
    // The label band is measured, never hard-coded: the 80f constant this replaces was the
    // library-side specimen of the N1 complaint (US->FL round 3), and its removal is the accepted
    // reshape — the same text-natural seam Width="Auto" uses, proving itself in the library's own
    // widget. The pad keeps glyphs off the band edge; it is not a size floor.
    private const float LabelPad = 4f;
    private const float DefaultButtonWidth = 24f;
    private const float DefaultFieldWidth = 64f;
    private const float Gap = 6f;

    private UiElementSpec spec = UiElementSpec.Empty;
    private readonly UiRetainedPaint retained = new();
    bool IUiRetainedDisplay.PaintLast(UiLayoutRect rect, UiWidgetContext ctx) => retained.Paint(rect, ctx);
    UiMeasurement? IUiRetainedDisplay.MeasureLast(UiWidgetContext ctx) => retained.Measure(ctx);

    public string KindName => Kind;

    string IUiWidget.Kind => Kind;

    public static void Register()
    {
UiWidgetRegistry.Define(new UiWidgetDefinition(
            UiWidgetRegistry.CoreScope,
            Kind,
            () => new StepperSliderWidget(),
            new[] { "Id", "Kind", "Bind", "Min", "Max", "Step", "Format", "Live", "ButtonWidth", "FieldWidth", "Height", "Label", "LabelKey", "Tab", "Hidden" },
            new[] { "Label", "LabelKey" }));
    }

    public void Configure(UiElementSpec spec)
    {
        this.spec = spec ?? throw new ArgumentNullException(nameof(spec));
    }

    public void Validate(IUiBindingAccess bindings, string elementPath)
    {
        string bindKey = ReadBindKey();
        if (string.IsNullOrEmpty(bindKey))
        {
            throw new InvalidOperationException(
                $"StepperSliderWidget at '{elementPath}' requires a non-empty Id or Bind to use as its typed binding key.");
        }

        bindings.ValidateValue<float>(bindKey, elementPath);
    }

    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx)
    {
        string label = ReadLabel(ctx);
        return new UiMeasurement(AtomVocabulary.LabelMeasureInputs(ctx, label), () =>
        {
            float height = ReadHeight();
            return spec.TryGetAttribute("Height", out _) ? height : Math.Max(height,
                UiSliderLayout.Measure(label, ctx.ViewWidth, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0,
                    Math.Max(1, ReadFloat("ButtonWidth", DefaultButtonWidth)) * 2 + Math.Max(1, ReadFloat("FieldWidth", DefaultFieldWidth)) + Gap * 3 + ctx.Theme.Geometry.RowHeight * 2).Height);
        });
    }

    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();

    public System.Collections.Generic.IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds)
    {
        UiStepperLayout layout = ControlLayout(ctx, bounds.Width, bounds.Height, LabelLayout(ctx, bounds.Width));
        yield return new UiPointerRegion(layout.Minus);
        yield return new UiPointerRegion(layout.Range);
        yield return new UiPointerRegion(layout.Field);
        yield return new UiPointerRegion(layout.Plus);
    }

    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        if (rect.Width <= 1f || rect.Height <= 1f)
            return;
        float min = ReadFloat("Min", 0f);
        float max = ReadFloat("Max", 1f);
        float step = Math.Max(0.0001f, ReadFloat("Step", 1f));
        string format = Read("Format");
        if (format.Length == 0)
            format = "0.##";
        bool live = ReadBool("Live", false);
        string bindKey = ReadBindKey();
        float current = ctx.Bindings.TryGet(bindKey, out float bound) ? bound : min;
        var numeric = new UiNumericEdit(min, max, format);
        UiEditSession<float> editor = ctx.Controls.Editor<float>(ctx, bindKey, numeric.Format, numeric.Parse);
        editor.Observe(current);
        string label = ReadLabel(ctx);
        UiSliderLayout labelLayout = LabelLayout(ctx, rect.Width);
        UiStepperLayout layout = ControlLayout(ctx, rect.Width, rect.Height, labelLayout);
        UiLayoutRect minusRect = Place(layout.Minus, rect);
        UiLayoutRect sliderRect = Place(layout.Range, rect);
        UiLayoutRect fieldRect = Place(layout.Field, rect);
        UiLayoutRect plusRect = Place(layout.Plus, rect);
        bool writable = ctx.Bindings.IsWritable(bindKey);
        UiInteractionResult minus = ctx.Controls.Activation(minusRect, ctx, bindKey, "decrement", writable: writable);
        UiInteractionResult plus = ctx.Controls.Activation(plusRect, ctx, bindKey, "increment", writable: writable);
        UiInteractionResult range = ctx.Controls.Range(sliderRect, ctx, bindKey, current, min, max, writable);
        // These parts explicitly participate in this editor's combination. Their press/capture is
        // not ordinary blur, even though activation waits until release and a thumb press may select nothing.
        UiInteractionTarget? capture = ctx.Session.Interactions.CaptureOwner;
        if (capture?.Node == ctx.Node.Id.Key
            && (capture.Value.Part == "decrement" || capture.Value.Part == "increment" || capture.Value.Part == ""))
            ctx.Controls.SuspendEditor(ctx, bindKey);
        bool takeover = false;
        void Set(float value) => ctx.Bindings.Set(bindKey, value);
        float ReadModel() => ctx.Bindings.Get<float>(bindKey);
        if (minus.Request || plus.Request)
        {
            UiInteractionResult operation = minus.Request ? minus : plus;
            string part = minus.Request ? "decrement" : "increment";
            UiInteractionRequests.Activation(ctx.Controls.InteractionTarget(ctx, bindKey, part), operation, () => numeric.Step(editor, ReadModel(), minus.Request ? -step : step, Set, ReadModel));
            takeover = true;
        }
        else if (range.Request)
        {
            UiInteractionRequests.Range(ctx.Controls.InteractionTarget(ctx, bindKey), range, value => editor.TakeOver(value, Set, ReadModel));
            takeover = true;
        }

        if (!ctx.Session.IsActive)
            return;
        if (!takeover)
            ctx.Controls.Edit(fieldRect, ctx, bindKey, ReadModel(), numeric.Format, numeric.Parse, live, false, Set, ReadModel);
        if (!ctx.Session.IsActive)
            return;
        UiControlState state = ctx.Session.GetControlState(ctx.Node.Id, bindKey);
        if (!editor.Focused) ctx.Controls.SuspendEditor(ctx, bindKey);
        // Only presentation follows established requests; a label failure cannot swallow an operation.
        if (label.Length > 0)
        {
            float labelWidth = labelLayout.LabelWidth;
            UiThemeDraw.Label(ctx.Drawing,new UiLayoutRect(rect.X, rect.Y, labelWidth, labelLayout.Stacked ? labelLayout.LabelHeight : layout.Minus.Height), label, ctx.Theme, ctx.Theme[UiPaletteRole.Foreground], AtomVocabulary.TextFont(ctx));
        }

        DrawButton(ctx.Drawing,minusRect, "−", ctx.Theme);
        DrawButton(ctx.Drawing,plusRect, "+", ctx.Theme);
        float paintedValue=ReadModel();
        ctx.Controls.PaintRange(sliderRect, ctx.Theme, paintedValue, min, max);
        AtomVocabulary.PaintEditFeedback(fieldRect, ctx, editor);
        retained.Capture(ctx,(r,c)=>
        {
            UiSliderLayout lastLabel=LabelLayout(c,r.Width,label);
            UiStepperLayout lastLayout=ControlLayout(c,r.Width,r.Height,lastLabel);
            if(label.Length>0) UiThemeDraw.Label(c.Drawing,new UiLayoutRect(r.X,r.Y,lastLabel.LabelWidth,lastLabel.Stacked?lastLabel.LabelHeight:lastLayout.Minus.Height),label,c.Theme,c.Theme[UiPaletteRole.Foreground],AtomVocabulary.TextFont(c));
            DrawButton(c.Drawing,Place(lastLayout.Minus,r),"−",c.Theme);DrawButton(c.Drawing,Place(lastLayout.Plus,r),"+",c.Theme);
            ctx.Controls.PaintRange(Place(lastLayout.Range,r),c.Theme,paintedValue,min,max);
            UiRetainedPaint.Field(Place(lastLayout.Field,r),c,editor.Draft);
        });
    }

    UiLayoutDiagnostic? IUiLayoutDiagnosticSource.DiagnoseLayout(UiWidgetContext ctx, float allocatedHeight)
    {
        UiSliderLayout label = LabelLayout(ctx, ctx.ViewWidth);
        UiStepperLayout layout = ControlLayout(ctx, ctx.ViewWidth, allocatedHeight, label);
        if (layout.Shortage <= 0) return null;
        string message = string.Format(CultureInfo.InvariantCulture,
            "Internal parts require {0} but allocation is {1}; shortage {2}. Explicit part widths and the outer allocation remain unchanged; choose more space or explicit part widths.",
            layout.RequiredWidth, layout.AvailableWidth, layout.Shortage);
        return new UiLayoutDiagnostic(ctx.ElementPath, "InternalWidthInsufficient", message);
    }

    private UiStepperLayout ControlLayout(UiWidgetContext ctx, float width, float height, UiSliderLayout label)
        => UiStepperLayout.Arrange(width, height, label,
            Math.Max(1, ReadFloat("ButtonWidth", DefaultButtonWidth)),
            Math.Max(1, ReadFloat("FieldWidth", DefaultFieldWidth)), Gap, ctx.Theme.Geometry.Gap);

    private static UiLayoutRect Place(UiLayoutRect local, UiLayoutRect outer)
        => new(outer.X + local.X, outer.Y + local.Y, local.Width, local.Height);
    public float MeasureLabel(UiWidgetContext ctx)
    {
        string label = ReadLabel(ctx);
        return label.Length == 0 ? 0 : ctx.Metrics.MeasureWidth(label, AtomVocabulary.TextFont(ctx)) + ctx.Theme.Geometry.Spacing;
    }

    private UiSliderLayout LabelLayout(UiWidgetContext ctx, float width, string? capturedLabel = null) => UiSliderLayout.Measure(capturedLabel ?? ReadLabel(ctx), width, AtomVocabulary.TextFont(ctx), UiElementProperties.DensityOf(ctx.Theme), ctx.Metrics, ctx.Node?.SharedLabelWidth ?? 0, Math.Max(1, ReadFloat("ButtonWidth", DefaultButtonWidth)) * 2 + Math.Max(1, ReadFloat("FieldWidth", DefaultFieldWidth)) + Gap * 3 + ctx.Theme.Geometry.RowHeight * 2);

    private string ReadBindKey()
    {
        if (spec.TryGetAttribute("Bind", out string bindKey) && bindKey.Length > 0)
        {
            return bindKey;
        }

        return spec.Id;
    }

    private string ReadLabel(UiWidgetContext ctx)
    {
        if (spec.TryGetAttribute("LabelKey", out string key) && key.Length > 0)
        {
            return ctx.Translation.Translate(key);
        }

        return spec.TryGetAttribute("Label", out string label) ? label : "";
    }

    private static void DrawButton(IUiDrawing drawing,UiLayoutRect rect, string text, UiTheme theme)
    {
        UiThemeDraw.Solid(drawing,rect, theme[UiPaletteRole.ControlSurface]);
        UiThemeDraw.Label(drawing,rect, text, theme, theme[UiPaletteRole.Foreground], anchor: UiTextAnchor.MiddleCenter);
    }


    private float ReadHeight()
    {
        if (spec.TryGetAttribute("Height", out string raw)
            && spec.TryGetNumber("Height", out float height)
            && height > 0f)
        {
            return height;
        }

        return DefaultHeight;
    }

    private string Read(string name)
    {
        return spec.TryGetAttribute(name, out string value) ? value : "";
    }

    private float ReadFloat(string name, float fallback)
    {
        return spec.TryGetAttribute(name, out string raw)
            && spec.TryGetNumber(name, out float value)
            ? value
            : fallback;
    }

    private bool ReadBool(string name, bool fallback)
    {
        if (!spec.TryGetAttribute(name, out string raw)) return fallback;
        return spec.TryGetBoolean(name, out bool value) ? value : fallback;
    }
}
