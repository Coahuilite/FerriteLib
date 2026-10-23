using System;
namespace FerriteLib.UiKit.Kernel;
/// <summary>A choice carries its actual value, label and help together. Labels never identify a business target.</summary>
public readonly struct UiChoice<T>
{
    public UiChoice(string text,T value,string help="",string textKey="") { Text=text??"";Value=value;Help=help??"";TextKey=textKey??""; }
    public string Text { get; }
    public T Value { get; }
    public string Help { get; }
    /// <summary>Optional consumer-owned translation key; when present it resolves instead of the literal label.</summary>
    public string TextKey { get; }
}
