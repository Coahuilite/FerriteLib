using System;
using System.Collections.Generic;
namespace FerriteLib.UiKit.Kernel;

/// <summary>Runtime value capability minted by the binding registry. A write always resolves the current registration.</summary>
public sealed class UiValueEndpoint
{
    private readonly UiBindings bindings; private readonly string key;
    internal UiValueEndpoint(UiBindings bindings,string key) { this.bindings=bindings;this.key=key; }
    public Type ValueType => bindings.EndpointType(key);
    public bool Writable => bindings.IsWritable(key);
    public object? Read() => bindings.EndpointRead(key);
    public T Read<T>() => bindings.Get<T>(key);
    public bool Accepts(object? value) => bindings.AcceptsValue(key,value);
    public bool TryWrite(object? value) => bindings.TrySetTypedValue(key,value);
}
/// <summary>One typed choice source. Erasure occurs only here, without reflection or string conversion.</summary>
public sealed class UiChoiceEndpoint
{
    private readonly UiBindings bindings; private readonly string key;
    internal UiChoiceEndpoint(UiBindings bindings,string key) { this.bindings=bindings;this.key=key; }
    public Type ValueType => bindings.ChoiceType(key);
    public IReadOnlyList<UiChoice<object?>> Read() => bindings.ReadChoices(key);
    public IReadOnlyList<UiChoice<object?>> Prefix(int maximum,out int count) => bindings.ReadChoicePrefix(key,maximum,out count);
}
public sealed partial class UiBindings
{
    public bool TryValue(string key,out UiValueEndpoint? endpoint) { endpoint=values.ContainsKey(key)?new UiValueEndpoint(this,key):null;return endpoint!=null; }
    public UiValueEndpoint Value(string key)
    { if(!values.ContainsKey(key))throw new InvalidOperationException("Missing value binding '"+key+"'.");return new UiValueEndpoint(this,key); }
    public UiChoiceEndpoint Choices(string key)
    { ChoiceType(key);return new UiChoiceEndpoint(this,key); }
    internal Type EndpointType(string key) => values.TryGetValue(key,out var d)?d.ValueType:throw new InvalidOperationException("Released value binding '"+key+"'.");
    internal object? EndpointRead(string key) => values.TryGetValue(key,out var d)?ReadValue(key,d):throw new InvalidOperationException("Released value binding '"+key+"'.");
    internal Type ChoiceType(string key) => options.TryGetValue(key,out var d)&&d.ChoiceValueType!=null?d.ChoiceValueType:throw new InvalidOperationException("Binding '"+key+"' is not a typed choice source.");
    internal IReadOnlyList<UiChoice<object?>> ReadChoices(string key)
    { ChoiceType(key);var d=options[key];return d.Choices(ReadOptions(key,d)); }
    internal IReadOnlyList<UiChoice<object?>> ReadChoicePrefix(string key,int maximum,out int count) { if(maximum<0)throw new ArgumentOutOfRangeException(nameof(maximum));ChoiceType(key);var d=options[key];using IDisposable? pure=d.Tracked&&!UsesPresentation?Reactive.Pure():null;object list=d.Tracked&&UsesPresentation?ReadOptions(key,d):d.GetListBoxed();return d.ChoicePrefix(list,maximum,out count); }
    public void BindChoices<T>(string key,Func<IReadOnlyList<UiChoice<T>>> get,UiInvalidation invalidates=UiInvalidation.Everything)
    {
        if(key==null)throw new ArgumentNullException(nameof(key));if(get==null)throw new ArgumentNullException(nameof(get));
        if(options.ContainsKey(key))throw new InvalidOperationException("Duplicate choices binding '"+key+"'.");
        options.Add(key,new ChoiceDescriptor<T>(get));DeclareInvalidation(key,invalidates);
    }
    public void BindTrackedChoices<T>(string key,Func<IReadOnlyList<UiChoice<T>>> get,Func<UiChoice<T>,UiChoice<T>,bool> equal)
    {
        if(equal==null)throw new ArgumentNullException(nameof(equal));Reactive.VerifyAccess();BindChoices(key,get);
        OptionsDescriptor d=options[key];d.Tracked=true;d.Equal=(a,b)=>{
            var left=(IReadOnlyList<UiChoice<T>>)a!;var right=(IReadOnlyList<UiChoice<T>>)b!;
            if(left.Count!=right.Count)return false;for(int i=0;i<left.Count;i++)if(!equal(left[i],right[i]))return false;return true;
        };reactiveEnabled=true;
    }
    private sealed class ChoiceDescriptor<T> : OptionsDescriptor
    {
        private readonly Func<IReadOnlyList<UiChoice<T>>> get;
        internal ChoiceDescriptor(Func<IReadOnlyList<UiChoice<T>>> get) => this.get=get;
        public override Type ItemType=>typeof(UiChoice<T>);
        public override Type? ChoiceValueType=>typeof(T);
        public override object GetListBoxed()=>get();
        public override object CaptureBoxed()=>new List<UiChoice<T>>(get()).AsReadOnly();
        public override IReadOnlyList<UiChoice<object?>> Choices(object list)
        { var result=new List<UiChoice<object?>>();foreach(var c in (IReadOnlyList<UiChoice<T>>)list)result.Add(new UiChoice<object?>(c.Text,c.Value,c.Help));return result.AsReadOnly(); }
        public override IReadOnlyList<UiChoice<object?>> ChoicePrefix(object list,int maximum,out int count) {var source=(IReadOnlyList<UiChoice<T>>)list;count=source.Count;var result=new List<UiChoice<object?>>();for(int i=0;i<Math.Min(count,maximum);i++){var c=source[i];result.Add(new UiChoice<object?>(c.Text,c.Value,c.Help));}return result.AsReadOnly();}
        public override IReadOnlyList<object?> ReadPrefix(object list,int maximum,out int count)
        { var source=(IReadOnlyList<UiChoice<T>>)list;count=source.Count;var result=new List<object?>();for(int i=0;i<Math.Min(maximum,count);i++)result.Add(source[i]);return result.AsReadOnly(); }
    }
}

