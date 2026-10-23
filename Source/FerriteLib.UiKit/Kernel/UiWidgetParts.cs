using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
/// <summary>Optional capabilities for a declarative wrapper. Direct C# drawing and behavior do not need this wrapper.</summary>
public interface IUiWidgetCapabilities
{
    IUiPointerLayout? Pointer { get; }

    IUiDescriptiveWidget? Description { get; }
}

/// <summary>Default wiring for a custom element; choose only the capabilities the element owns.</summary>
public sealed class UiWidgetParts : IUiWidget, IUiMeasureParticipant, IUiWidgetCapabilities
{
    private readonly Func<UiWidgetContext, UiMeasurement>? measure;
    private readonly Action<UiLayoutRect, UiWidgetContext>? paint, operate;
    private readonly Action<UiElementSpec, IUiBindingReader, string>? validate;
    private UiElementSpec spec = UiElementSpec.Empty;
    public UiWidgetParts(string kind, Func<UiWidgetContext, UiMeasurement>? measure = null, Action<UiLayoutRect, UiWidgetContext>? paint = null, Action<UiLayoutRect, UiWidgetContext>? operate = null, Func<UiWidgetContext, UiLayoutRect, IEnumerable<UiPointerRegion>>? pointer = null, Func<UiWidgetContext, UiDescription>? describe = null, Action<UiElementSpec, IUiBindingReader, string>? validate = null)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        this.measure = measure;
        this.paint = paint;
        this.operate = operate;
        this.validate = validate;
        Pointer = pointer == null ? null : new PointerPart(pointer);
        Description = describe == null ? null : new DescriptionPart(describe);
    }

    public string Kind { get; }
    public IUiPointerLayout? Pointer { get; }
    public IUiDescriptiveWidget? Description { get; }

    public void Configure(UiElementSpec spec) => this.spec = spec;
    public void Validate(IUiBindingReader bindings, string path) => validate?.Invoke(spec, bindings, path);
    public UiMeasurement CaptureMeasurement(UiWidgetContext ctx) => measure?.Invoke(ctx) ?? new UiMeasurement(new UiMeasureInputs(), () => 0);
    public float Measure(UiWidgetContext ctx) => CaptureMeasurement(ctx).Calculate();
    public void Draw(UiLayoutRect rect, UiWidgetContext ctx)
    {
        operate?.Invoke(rect, ctx);
        if (!ctx.IsActive)
            return;
        using UiBindings.DisplayScope? display = (ctx.Bindings as UiBindings)?.DisplayOnly();
        paint?.Invoke(rect, ctx);
    }

    private sealed class PointerPart : IUiPointerLayout
    {
        private readonly Func<UiWidgetContext, UiLayoutRect, IEnumerable<UiPointerRegion>> read;
        internal PointerPart(Func<UiWidgetContext, UiLayoutRect, IEnumerable<UiPointerRegion>> read) => this.read = read;
        public IEnumerable<UiPointerRegion> PointerRegions(UiWidgetContext ctx, UiLayoutRect bounds) => read(ctx, bounds);
    }

    private sealed class DescriptionPart : IUiDescriptiveWidget
    {
        private readonly Func<UiWidgetContext, UiDescription> read;
        internal DescriptionPart(Func<UiWidgetContext, UiDescription> read) => this.read = read;
        public UiDescription Describe(UiWidgetContext ctx) => read(ctx);
    }
}

internal static class UiWidgetCapabilities
{
    internal static IUiPointerLayout? Pointer(IUiWidget widget) => widget as IUiPointerLayout ?? (widget as IUiWidgetCapabilities)?.Pointer;
    internal static IUiDescriptiveWidget? Description(IUiWidget widget) => widget as IUiDescriptiveWidget ?? (widget as IUiWidgetCapabilities)?.Description;
}
