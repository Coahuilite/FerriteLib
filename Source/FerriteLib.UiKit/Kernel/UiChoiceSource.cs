using System;
using System.Collections.Generic;
namespace FerriteLib.UiKit.Kernel;
internal static class UiChoiceSource
{
    internal static string ValueKey(UiElementSpec spec) => spec.TryGetAttribute("Bind",out string key)&&key.Length>0?key:spec.Id;
    internal static string ChoiceKey(UiElementSpec spec) => spec.TryGetAttribute("OptionsBind",out string key)&&key.Length>0?key:ValueKey(spec);
    internal static void Validate(IUiBindingReader reader,UiElementSpec spec,string path)
    {
        UiValueEndpoint value=reader.Value(ValueKey(spec));
        Type type=spec.Choices.Count>0?typeof(string):reader.Choices(ChoiceKey(spec)).ValueType;
        if(type!=value.ValueType)throw new InvalidOperationException("Choice/value type mismatch at '"+path+"': "+type.Name+" / "+value.ValueType.Name+".");
        if(spec.Choices.Count>0&&spec.TryGetAttribute("OptionsBind",out string key)&&key.Length>0)throw new InvalidOperationException("Two choice sources at '"+path+"'.");
    }
    internal static IReadOnlyList<UiChoice<object?>> Read(IUiBindingReader reader,UiElementSpec spec)
    {
        if(spec.Choices.Count==0)return reader.Choices(ChoiceKey(spec)).Read();
        var result=new List<UiChoice<object?>>(spec.Choices.Count);
        foreach(var choice in spec.Choices)result.Add(new UiChoice<object?>(choice.Text,choice.Value,choice.Help));
        return result.AsReadOnly();
    }
}
