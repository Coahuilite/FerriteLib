using System;
using System.Collections.Generic;
using System.Globalization;

namespace FerriteLib.UiKit.Kernel.Widgets;
internal static class SemanticActions
{
    internal static IReadOnlyList<UiSemanticAction> One(UiToolOperation operation) => new[] { new UiSemanticAction(operation) };
    internal static UiInteractionResult Activate(UiWidgetContext ctx, string binding, string part = "", object? item = null, bool writable = true)
        => ctx.Session.Interactions.Activate(ctx.Controls.InteractionTarget(ctx, binding, part, item), new UiInputEligibility(!UiControls.IsInputDisabled(ctx.Node), writable));
    internal static IReadOnlyList<UiSemanticAction> Choices(UiWidgetContext ctx, UiElementSpec spec, int maximum)
    {
        var actions = new List<UiSemanticAction>();
        IReadOnlyList<UiChoice<object?>> choices = UiChoiceSource.Prefix(ctx.Bindings, spec, ctx.Translation, maximum, out _);
        for (int i = 0; i < choices.Count; i++) actions.Add(new UiSemanticAction(UiToolOperation.Select, "option", i.ToString(CultureInfo.InvariantCulture), choices[i].Text) { Choice = choices[i].Value });
        return actions.AsReadOnly();
    }
    internal static bool Select(UiWidgetContext ctx, UiElementSpec spec, UiSemanticAction action)
    {
        string key = UiChoiceSource.ValueKey(spec);
        UiValueEndpoint endpoint = ctx.Bindings.Value(key);
        if (!endpoint.Writable) return false;
        var target = ctx.Controls.InteractionTarget(ctx, key, "option", action.Choice);
        var input = SemanticActions.Activate(ctx, key, "option", action.Choice, endpoint.Writable);
        UiValueBehavior.Request(target, UiValueBehavior.Selection(input, action.Choice), v => endpoint.TryWrite(v));
        return input.Request;
    }
    internal static IReadOnlyList<UiSemanticAction> Editing() => new[] { new UiSemanticAction(UiToolOperation.SetDraft, "editor"), new UiSemanticAction(UiToolOperation.CommitEdit, "editor"), new UiSemanticAction(UiToolOperation.CancelEdit, "editor") };
}

internal sealed partial class ButtonWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum) => SemanticActions.One(UiToolOperation.Activate);
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        UiInteractionResult input = SemanticActions.Activate(ctx, ReadActionKey());
        UiInteractionRequests.Activation(ctx.Controls.InteractionTarget(ctx, ReadActionKey()), input, () => RequestActivation(ctx));
        return input.Request;
    }
}
internal sealed partial class CheckboxWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum) => SemanticActions.One(UiToolOperation.Activate);
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        string key = ReadBindKey();
        if (!ctx.Bindings.IsWritable(key)) return false;
        var input = SemanticActions.Activate(ctx, key);
        TakeInput(ctx, key, ReadState(ctx, key), input);
        return input.Request;
    }
}
internal sealed partial class SliderWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum) => SemanticActions.One(UiToolOperation.SetNumber);
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        string key = AtomVocabulary.ReadBindKey(spec);
        if (!value.Number.HasValue || !ctx.Bindings.IsWritable(key)) return false;
        var target = ctx.Controls.InteractionTarget(ctx, key);
        var input = ctx.Session.Interactions.Select(target, Limits(), value.Number.Value, new UiInputEligibility(true, ctx.Bindings.IsWritable(key)));
        UiInteractionRequests.Range(target, input, candidate => ctx.Bindings.Set(key, candidate));
        return input.Request;
    }
}
internal sealed partial class DropdownWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum) => SemanticActions.Choices(context, spec, maximum);
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value) => SemanticActions.Select(ctx, spec, action);
}
internal sealed partial class InputModeRowWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum) => SemanticActions.Choices(context, spec, maximum);
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value) => SemanticActions.Select(ctx, spec, action);
}
internal sealed partial class TextFieldWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum) => SemanticActions.Editing();
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        string key = AtomVocabulary.ReadBindKey(spec);
        return ctx.Controls.EditSemantic(ctx, key, action.Operation, value.Text, v => v, Parse, AtomVocabulary.ReadBool(spec, "Live", false), v => ctx.Bindings.Set(key, v), () => ctx.Bindings.Get<string>(key));
    }
}
internal sealed partial class NumberFieldWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum) => SemanticActions.Editing();
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        string key = AtomVocabulary.ReadBindKey(spec);
        var numeric = new UiNumericEdit(AtomVocabulary.ReadFloat(spec, "Min", 0), AtomVocabulary.ReadFloat(spec, "Max", 1), AtomVocabulary.Read(spec, "Format"));
        return ctx.Controls.EditSemantic(ctx, key, action.Operation, value.Text, numeric.Format, numeric.Parse, AtomVocabulary.ReadBool(spec, "Live", false), v => ctx.Bindings.Set(key, v), () => ctx.Bindings.Get<float>(key));
    }
}
internal sealed partial class StepperSliderWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext context, int maximum)
    {
        var actions = new List<UiSemanticAction>(SemanticActions.Editing()) { new UiSemanticAction(UiToolOperation.SetNumber, "range") };
        return actions.AsReadOnly();
    }
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        string key = ReadBindKey();
        var numeric = new UiNumericEdit(ReadFloat("Min", 0), ReadFloat("Max", 1), Read("Format"));
        if (action.Operation != UiToolOperation.SetNumber)
            return ctx.Controls.EditSemantic(ctx, key, action.Operation, value.Text, numeric.Format, numeric.Parse, ReadBool("Live", false), v => ctx.Bindings.Set(key, v), () => ctx.Bindings.Get<float>(key));
        if (!value.Number.HasValue || !ctx.Bindings.IsWritable(key)) return false;
        var target = ctx.Controls.InteractionTarget(ctx, key);
        var input = ctx.Session.Interactions.Select(target, new UiRange(ReadFloat("Min", 0), ReadFloat("Max", 1)), value.Number.Value, new UiInputEligibility(true));
        UiEditSession<float> editor = ctx.Controls.Editor<float>(ctx, key, numeric.Format, numeric.Parse);
        UiInteractionRequests.Range(target, input, v => editor.TakeOver(v, candidate => ctx.Bindings.Set(key, candidate), () => ctx.Bindings.Get<float>(key)));
        return input.Request;
    }
}
internal sealed partial class TreeWidget : IUiSemanticWidget
{
    public IReadOnlyList<UiSemanticAction> ReadActions(UiWidgetContext ctx, int maximum)
    {
        var actions = new List<UiSemanticAction>();
        foreach (UiTreeRow row in CollectRows(ctx, ReadBindKey()))
        {
            if (actions.Count >= maximum) break;
            if (row.Expandable && AtomVocabulary.Read(spec, "DisclosureBind").Length > 0)
                actions.Add(new UiSemanticAction(row.Expanded ? UiToolOperation.Collapse : UiToolOperation.Expand, "disclosure", row.Key, row.Text));
            if (actions.Count >= maximum) break;
            if (AtomVocabulary.Read(spec, "ActionBind").Length > 0) actions.Add(new UiSemanticAction(UiToolOperation.Activate, "body", row.Key, row.Text));
        }
        return actions.AsReadOnly();
    }
    public bool ExecuteAction(UiWidgetContext ctx, UiSemanticAction action, UiToolValue value)
    {
        string command = AtomVocabulary.Read(spec, action.Part == "disclosure" ? "DisclosureBind" : "ActionBind");
        if (!ctx.Bindings.CanExecute(command)) return false;
        var target = ctx.Controls.InteractionTarget(ctx, command, action.Part, action.Item);
        var input = SemanticActions.Activate(ctx, command, action.Part, action.Item);
        UiInteractionRequests.Activation(target, input, () => ctx.Bindings.Invoke(command, action.Item));
        return input.Request;
    }
}
