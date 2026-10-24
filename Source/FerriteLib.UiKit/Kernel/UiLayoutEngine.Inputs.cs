using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

internal sealed partial class UiLayoutEngine
{
    private sealed class BoundInput<T>
    {
        internal IUiBindingAccess Bindings = null!;
        internal int Revision, Pass;
        internal bool Found;
        internal T Value = default!;
    }
    private sealed class RoleInput
    {
        internal UiElementSpec Spec = null!;
        internal UiMeasureInputs Context = null!;
        internal UiMeasureInputs? Environment;
        internal ITextMetrics Metrics = null!;
        internal IUiTranslation Translation = null!;
        internal int TranslationRevision;
        internal float Value;
    }
    private Dictionary<UiNodeId, RoleInput> autoWidths = new(), labelContributions = new();
    private float ReadLayoutRole(Dictionary<UiNodeId, RoleInput> records, UiElementSpec spec, UiWidgetContext ctx, bool shared, Func<float> calculate)
    {
        if (ctx.Node == null) return calculate();
        UiMeasureInputs context = shared ? MeasureContext(ctx) : new UiMeasureInputs(numbers: new[] {
            ctx.ViewWidth, (float)ctx.Theme.DefaultFont, ctx.Theme.Geometry.Padding, ctx.Theme.Geometry.Spacing,
            ctx.Theme.Geometry.Gap, ctx.Theme.Geometry.RowHeight, ctx.Theme.Geometry.Hairline });
        UiMeasureInputs? environment = (ctx.Metrics as IUiTextMetricsEnvironment)?.CaptureEnvironment(ctx.Theme.DefaultFont);
        if (!layoutRequested && !ctx.Node.IsDirty && records.TryGetValue(ctx.Node.Id, out RoleInput? prior)
            && ReferenceEquals(prior.Spec, spec) && ReferenceEquals(prior.Metrics, textMetricsCache!.Source)
            && ReferenceEquals(prior.Translation, ctx.Translation) && prior.TranslationRevision == ctx.Translation.TranslationRevision
            && context.Equals(prior.Context) && (environment == null ? prior.Environment == null : environment.Equals(prior.Environment))) return prior.Value;
        float result = calculate();
        records[ctx.Node.Id] = new RoleInput { Spec = spec, Context = context, Environment = environment, Metrics = textMetricsCache!.Source,
            Translation = ctx.Translation, TranslationRevision = ctx.Translation.TranslationRevision, Value = result };
        return result;
    }
    private Dictionary<string, BoundInput<bool>> visibleInputs = new(StringComparer.Ordinal);
    private Dictionary<string, BoundInput<float>> widthInputs = new(StringComparer.Ordinal);
    private Dictionary<string, BoundInput<string>> tabInputs = new(StringComparer.Ordinal);
    private static void PruneRoleInputs(UiSession session, Dictionary<UiNodeId, RoleInput> records)
    {
        var gone = new List<UiNodeId>();
        foreach (UiNodeId id in records.Keys) if (session.GetNode(id) == null) gone.Add(id);
        foreach (UiNodeId id in gone) records.Remove(id);
    }
    private void PruneBindingInputs<T>(Dictionary<string, BoundInput<T>> records)
    {
        var gone = new List<string>();
        foreach (string key in records.Keys) if (!keyNodes.ContainsKey(key)) gone.Add(key);
        foreach (string key in gone) records.Remove(key);
    }
    private bool InputCurrent<T>(BoundInput<T> prior, IUiBindingAccess bindings, string key)
        => ReferenceEquals(prior.Bindings, bindings) && prior.Revision == bindings.GetRevision(key)
            && (!layoutRequested || prior.Pass == layoutPass);
    private bool TryLayoutBool(IUiBindingAccess bindings, string key, out bool value)
    {
        if (!visibleInputs.TryGetValue(key, out BoundInput<bool>? input) || !InputCurrent(input, bindings, key))
        {
            input = new BoundInput<bool> { Bindings = bindings, Revision = bindings.GetRevision(key), Pass = layoutPass };
            input.Found = bindings.TryGetBool(key, out input.Value); visibleInputs[key] = input;
        }
        value = input.Value; return input.Found;
    }
    private bool TryLayoutFloat(IUiBindingAccess bindings, string key, out float value)
    {
        if (!widthInputs.TryGetValue(key, out BoundInput<float>? input) || !InputCurrent(input, bindings, key))
        {
            input = new BoundInput<float> { Bindings = bindings, Revision = bindings.GetRevision(key), Pass = layoutPass };
            input.Found = bindings.TryGet(key, out input.Value); widthInputs[key] = input;
        }
        value = input.Value; return input.Found;
    }
    private bool TryLayoutTab(IUiBindingAccess bindings, out string value)
    {
        string key = UiBindings.ActiveTabBind;
        if (!tabInputs.TryGetValue(key, out BoundInput<string>? input) || !InputCurrent(input, bindings, key))
        {
            input = new BoundInput<string> { Bindings = bindings, Revision = bindings.GetRevision(key), Pass = layoutPass };
            input.Found = bindings.TryGet(key, out input.Value); tabInputs[key] = input;
        }
        value = input.Value; return input.Found;
    }
}
