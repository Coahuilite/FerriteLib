using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;
internal static class UiChoiceSource
{
    internal static string Label<T>(UiChoice<T> choice,IUiTranslation translation)=>choice.TextKey.Length==0?choice.Text:translation.Translate(choice.TextKey);
    internal static string ValueKey(UiElementSpec spec) => spec.TryGetAttribute("Bind", out string key) && key.Length > 0 ? key : spec.Id;
    internal static string ChoiceKey(UiElementSpec spec) => spec.TryGetAttribute("OptionsBind", out string key) && key.Length > 0 ? key : ValueKey(spec);
    internal static void Validate(IUiBindingReader reader, UiElementSpec spec, string path)
    {
        UiBindings.VerifyReaderKey(reader,ValueKey(spec),UiBindingRole.Value);
        if(spec.Choices.Count==0)UiBindings.VerifyReaderKey(reader,ChoiceKey(spec),UiBindingRole.Choices);
        UiValueEndpoint value = reader.Value(ValueKey(spec));
        Type type = spec.Choices.Count > 0 ? typeof(string) : reader.Choices(ChoiceKey(spec)).ValueType;
        if (type != value.ValueType)
            throw new InvalidOperationException("Choice/value type mismatch at '" + path + "': " + type.Name + " / " + value.ValueType.Name + ".");
        if (spec.Choices.Count > 0 && spec.TryGetAttribute("OptionsBind", out string key) && key.Length > 0)
            throw new InvalidOperationException("Two choice sources at '" + path + "'.");
    }

    internal static IReadOnlyList<UiChoice<object?>> Read(IUiBindingReader reader, UiElementSpec spec,IUiTranslation translation)
    {
        var result = new List<UiChoice<object?>>();
        if(spec.Choices.Count==0)
        {
            UiBindings.VerifyReaderKey(reader,ChoiceKey(spec),UiBindingRole.Choices);
            foreach(var choice in reader.Choices(ChoiceKey(spec)).Read())
                result.Add(new UiChoice<object?>(Label(choice,translation),choice.Value,choice.Help));
        }
        else foreach (var choice in spec.Choices)
            result.Add(new UiChoice<object?>(Label(choice,translation), choice.Value, choice.Help));
        return result.AsReadOnly();
    }
}
