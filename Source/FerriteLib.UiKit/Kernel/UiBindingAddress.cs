using System;
namespace FerriteLib.UiKit.Kernel;
/// <summary>Injective item-binding address. Source, business key and field remain separate facts.</summary>
public readonly struct UiBindingAddress : IEquatable<UiBindingAddress>
{
    public UiBindingAddress(string source,string item,string field)
    { Source=source??throw new ArgumentNullException(nameof(source));Item=item??throw new ArgumentNullException(nameof(item));Field=field??throw new ArgumentNullException(nameof(field)); }
    public string Source { get; }
    public string Item { get; }
    public string Field { get; }
    public string Key => "@item/"+Part(Source)+Part(Item)+Part(Field);
    private static string Part(string value) => value.Length+":"+value;
    public bool Equals(UiBindingAddress other)=>Source==other.Source&&Item==other.Item&&Field==other.Field;
    public override bool Equals(object? value)=>value is UiBindingAddress other&&Equals(other);
    public override int GetHashCode()=>Key.GetHashCode();
    public override string ToString()=>Key;
}
