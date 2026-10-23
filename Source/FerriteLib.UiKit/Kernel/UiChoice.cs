using System;
namespace FerriteLib.UiKit.Kernel;
/// <summary>A choice carries its actual value, label and help together. Labels never identify a business target.</summary>
public readonly struct UiChoice<T>
{
    public UiChoice(string text,T value,string help="") { Text=text??"";Value=value;Help=help??""; }
    public string Text { get; }
    public T Value { get; }
    public string Help { get; }
}
