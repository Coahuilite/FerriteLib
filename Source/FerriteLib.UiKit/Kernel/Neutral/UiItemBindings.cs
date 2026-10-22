using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Describe item fields and commands once. Callbacks always receive the current object for the key.</summary>
public sealed class UiItemBindingSchema<T> where T : class
{
    internal readonly Dictionary<string, Action<UiBindings, string, Func<T>>> Fields = new(StringComparer.Ordinal);
    private bool sealedSchema;
    internal void Seal() => sealedSchema = true;
    private void Add(string name, Action<UiBindings, string, Func<T>> register)
    {
        if (sealedSchema) throw new InvalidOperationException("Item binding schema is already sealed.");
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("An item field needs a non-empty name.", nameof(name));
        if (Fields.ContainsKey(name)) throw new ArgumentException("Duplicate item field '" + name + "'.", nameof(name));
        Fields.Add(name, register);
    }
    public void Value<TValue>(string name, Func<T, TValue> read, Action<T, TValue>? write = null, Func<TValue, TValue, bool>? equal = null)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        Add(name, (bindings, address, current) => bindings.BindTracked(address, () => read(current()),
            write == null ? null : value => write(current(), value), equal));
    }
    public void Command(string name, Action<T> execute, Func<T, bool>? canExecute = null)
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));
        Add(name, (bindings, address, current) => bindings.BindCommand(address, () => execute(current()),
            canExecute == null ? null : () => canExecute(current())));
    }
}

/// <summary>Optional keyed UI connection owner over externally owned reference objects. Refresh is explicit;
/// it does not observe arbitrary fields, reorder business data or destroy external objects.</summary>
public sealed class UiItemBindings<T> : IDisposable where T : class
{
    private readonly UiBindings bindings;
    private readonly string itemsKey;
    private readonly Func<IReadOnlyList<T>> source;
    private readonly Func<T, string> identity;
    private readonly UiItemBindingSchema<T> schema = new();
    private readonly Dictionary<string, UiReactiveValue<T?>> rows = new(StringComparer.Ordinal);
    private readonly UiReactiveValue<IReadOnlyList<string>> keys;
    private bool disposed;
    public UiItemBindings(UiBindings bindings, string itemsKey, Func<IReadOnlyList<T>> source, Func<T, string> identity,
        Action<UiItemBindingSchema<T>> configure)
    {
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        this.itemsKey = !string.IsNullOrEmpty(itemsKey) ? itemsKey : throw new ArgumentException("An item source needs a non-empty binding key.", nameof(itemsKey));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        bindings.Reactive.AssertWritable();
        if (bindings.ContainsBinding(itemsKey)) throw new InvalidOperationException("Item source binding already exists: '" + itemsKey + "'.");
        configure(schema); schema.Seal();
        keys = bindings.Reactive.Value<IReadOnlyList<string>>(Array.Empty<string>(), SameKeys);
        bindings.BindTracked(itemsKey, () => keys.Value, equal: SameKeys);
        try { Refresh(); } catch { Dispose(); throw; }
    }
    public IReadOnlyList<string> Keys { get { Check(); return keys.Value; } }
    private static bool SameKeys(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++) if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
        return true;
    }
    private string Address(string key, string field) => itemsKey + "." + key + "." + field;
    private void Check()
    { bindings.Reactive.VerifyAccess(); if (disposed) throw new ObjectDisposedException(itemsKey); }
    public void Refresh()
    {
        Check(); bindings.Reactive.AssertWritable();
        IReadOnlyList<T> snapshot = source() ?? throw new InvalidOperationException("Item source '" + itemsKey + "' returned null.");
        var next = new Dictionary<string, T>(StringComparer.Ordinal); var order = new List<string>(snapshot.Count);
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.Count; i++)
        {
            T item = snapshot[i] ?? throw new InvalidOperationException("Null item at '" + itemsKey + "' index " + i + ".");
            string key = identity(item);
            if (string.IsNullOrEmpty(key) || next.ContainsKey(key))
                throw new InvalidOperationException("Invalid or duplicate item key '" + key + "' at source '" + itemsKey + "' index " + i + ".");
            next.Add(key, item); order.Add(key);
            foreach (string field in schema.Fields.Keys)
            {
                string address = Address(key, field);
                if (!addresses.Add(address) || (!rows.ContainsKey(key) && bindings.ContainsBinding(address)))
                    throw new InvalidOperationException("Item binding address collision at '" + address + "'.");
            }
        }
        using (bindings.Reactive.Batch())
        {
            var removed = new List<string>(); foreach (string key in rows.Keys) if (!next.ContainsKey(key)) removed.Add(key);
            foreach (string key in removed) Release(key);
            foreach (var pair in next)
            {
                if (rows.TryGetValue(pair.Key, out var held))
                {
                    // Explicit external refresh also reports ordinary-field changes on retained objects.
                    if (ReferenceEquals(held.ReadCurrent(), pair.Value)) held.NotifyCurrent();
                    else held.Value = pair.Value;
                }
                else
                {
                    string key = pair.Key;
                    var current = bindings.Reactive.Value<T?>(pair.Value, (a, b) => ReferenceEquals(a, b));
                    rows.Add(key, current);
                    T Read() => current.Value ?? throw new InvalidOperationException("Item target '" + itemsKey + "." + key + "' is no longer connected.");
                    foreach (var field in schema.Fields) field.Value(bindings, Address(key, field.Key), Read);
                }
            }
            keys.Value = order.AsReadOnly();
        }
    }
    private void Release(string key)
    {
        rows[key].Value = null;
        foreach (string field in schema.Fields.Keys) bindings.RemoveItemBinding(Address(key, field));
        rows.Remove(key);
    }
    public void Dispose()
    {
        bindings.Reactive.VerifyAccess(); if (disposed) return;
        using (bindings.Reactive.Batch())
        {
            foreach (string key in new List<string>(rows.Keys)) Release(key);
            keys.Value = Array.Empty<string>(); bindings.RemoveItemBinding(itemsKey); disposed = true;
        }
    }
}
