using System;
namespace FerriteLib.UiKit.Kernel;
/// <summary>A choice carries its actual value, label and help together. Labels never identify a business target.</summary>
public readonly struct UiChoice<T>
{
    public UiChoice(string text,T value,string helpRef="",string textKey="") { Text=text??"";Value=value;HelpRef=helpRef??"";TextKey=textKey??""; }
    public string Text { get; }
    public T Value { get; }
    /// <summary>Opaque consumer topic reference, not text or a translation/binding key. Empty means none.</summary>
    public string HelpRef { get; }
    /// <summary>Optional consumer-owned translation key; when present it resolves instead of the literal label.</summary>
    public string TextKey { get; }
}
