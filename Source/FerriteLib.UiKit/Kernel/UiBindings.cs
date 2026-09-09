using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Explicit typed binding registry. Every binding is registered with a concrete <c>T</c>; all
/// widget access is generic, so type mismatches surface at Host creation time rather than Draw.
/// </summary>
public sealed class UiBindings : IUiBindings
{

    /// <summary>
    /// The value-binding key the layout engine reads to decide which <c>Tab</c>-attributed elements are
    /// visible. The engine used to hard-read this magic string out of consumer-supplied bindings without
    /// ever declaring it, so a consumer had to guess the spelling and a wrong guess was a silently
    /// invisible page (US→FL round 1, P4). Register it as a <c>string</c> value binding; a consumer that
    /// never registers it is not broken — <c>Tab</c> simply reads as no active tab, as before.
    /// </summary>
    public const string ActiveTabKey = "active-tab";
    private abstract class ValueDescriptor
    {
        public abstract Type ValueType { get; }
        public abstract bool CanWrite { get; }
        public abstract object? GetBoxed();
        public abstract void SetBoxed(object? value);
    }

    private sealed class ValueDescriptor<T> : ValueDescriptor
    {
        private readonly Func<T> get;
        private readonly Action<T>? set;

        public ValueDescriptor(Func<T> get, Action<T>? set)
        {
            this.get = get ?? throw new ArgumentNullException(nameof(get));
            this.set = set;
        }

        public override Type ValueType => typeof(T);

        public override bool CanWrite => set != null;

        public override object? GetBoxed() => get();

        public override void SetBoxed(object? value)
        {
            if (set == null)
            {
                throw new InvalidOperationException("Binding is read-only and cannot be set.");
            }

            set((T)value!);
        }
    }

    private abstract class OptionsDescriptor
    {
        public abstract Type ItemType { get; }
        public abstract object GetListBoxed();
    }

    private sealed class OptionsDescriptor<T> : OptionsDescriptor
    {
        private readonly Func<IReadOnlyList<T>> get;

        public OptionsDescriptor(Func<IReadOnlyList<T>> get)
        {
            this.get = get ?? throw new ArgumentNullException(nameof(get));
        }

        public override Type ItemType => typeof(T);

        public override object GetListBoxed() => get();
    }

    private abstract class ActionDescriptor
    {
        public abstract Type PayloadType { get; }
        public abstract void InvokeBoxed(object? payload);
    }

    private sealed class ActionDescriptor<T> : ActionDescriptor
    {
        private readonly Action<T> action;

        public ActionDescriptor(Action<T> action)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override Type PayloadType => typeof(T);

        public override void InvokeBoxed(object? payload) => action((T)payload!);
    }

    private sealed class CommandDescriptor
    {
        public CommandDescriptor(Action action)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public Action Action { get; }
    }

    private readonly Dictionary<string, ValueDescriptor> values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OptionsDescriptor> options = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionDescriptor> actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommandDescriptor> commands = new(StringComparer.Ordinal);

    public void BindValue<T>(string elementId, Func<T> get, Action<T> set)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        if (get == null) throw new ArgumentNullException(nameof(get));
        if (set == null) throw new ArgumentNullException(nameof(set));
        AddValue(elementId, new ValueDescriptor<T>(get, set));
    }

    public void BindReadOnly<T>(string elementId, Func<T> get)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        if (get == null) throw new ArgumentNullException(nameof(get));
        AddValue(elementId, new ValueDescriptor<T>(get, null));
    }

    public void BindOptions<T>(string elementId, Func<IReadOnlyList<T>> get)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        if (get == null) throw new ArgumentNullException(nameof(get));
        if (options.ContainsKey(elementId))
        {
            throw new InvalidOperationException($"Duplicate options binding '{elementId}'.");
        }

        options.Add(elementId, new OptionsDescriptor<T>(get));
    }

    public void BindAction<T>(string actionId, Action<T> action)
    {
        if (actionId == null) throw new ArgumentNullException(nameof(actionId));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (actions.ContainsKey(actionId))
        {
            throw new InvalidOperationException($"Duplicate action binding '{actionId}'.");
        }

        actions.Add(actionId, new ActionDescriptor<T>(action));
    }

    public void BindCommand(string actionId, Action action)
    {
        if (actionId == null) throw new ArgumentNullException(nameof(actionId));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (commands.ContainsKey(actionId))
        {
            throw new InvalidOperationException($"Duplicate command binding '{actionId}'.");
        }

        commands.Add(actionId, new CommandDescriptor(action));
    }

    public T Get<T>(string elementId)
    {
        if (!TryGetValueDescriptor(elementId, out ValueDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No value binding registered for '{elementId}'.");
        }

        EnsureType<T>(descriptor!.ValueType, elementId, "value");
        return (T)descriptor.GetBoxed()!;
    }

    public bool TryGet<T>(string elementId, out T value)
    {
        if (!values.TryGetValue(elementId, out ValueDescriptor? descriptor))
        {
            value = default!;
            return false;
        }

        EnsureType<T>(descriptor!.ValueType, elementId, "value");
        value = (T)descriptor.GetBoxed()!;
        return true;
    }

    public void Set<T>(string elementId, T value)
    {
        if (!TryGetValueDescriptor(elementId, out ValueDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No value binding registered for '{elementId}'.");
        }

        EnsureType<T>(descriptor!.ValueType, elementId, "value");
        descriptor.SetBoxed(value);
    }

    public IReadOnlyList<T> GetOptions<T>(string elementId)
    {
        if (!options.TryGetValue(elementId, out OptionsDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No options binding registered for '{elementId}'.");
        }

        if (descriptor!.ItemType != typeof(T))
        {
            throw new InvalidOperationException(
                $"Options binding '{elementId}' is '{descriptor.ItemType.Name}', not '{typeof(T).Name}'.");
        }

        return (IReadOnlyList<T>)descriptor.GetListBoxed();
    }

    public void Invoke<T>(string actionId, T payload)
    {
        if (!actions.TryGetValue(actionId, out ActionDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No action binding registered for '{actionId}'.");
        }

        if (descriptor!.PayloadType != typeof(T))
        {
            throw new InvalidOperationException(
                $"Action binding '{actionId}' expects '{descriptor.PayloadType.Name}', not '{typeof(T).Name}'.");
        }

        descriptor.InvokeBoxed(payload);
    }

    public void Invoke(string actionId)
    {
        if (!commands.TryGetValue(actionId, out CommandDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No command binding registered for '{actionId}'.");
        }

        descriptor!.Action();
    }

    public void ValidateValue<T>(string elementId, string elementPath)
    {
        if (!TryGetValueDescriptor(elementId, out ValueDescriptor? descriptor))
        {
            throw new InvalidOperationException($"Required value binding '{elementId}' is missing at '{elementPath}'.");
        }

        EnsureType<T>(descriptor!.ValueType, elementId, "value");
    }

    public void ValidateOptions<T>(string elementId, string elementPath)
    {
        if (!options.TryGetValue(elementId, out OptionsDescriptor? descriptor))
        {
            throw new InvalidOperationException($"Required options binding '{elementId}' is missing at '{elementPath}'.");
        }

        if (descriptor!.ItemType != typeof(T))
        {
            throw new InvalidOperationException(
                $"Options binding '{elementId}' at '{elementPath}' is '{descriptor.ItemType.Name}', not '{typeof(T).Name}'.");
        }
    }

    public void ValidateAction<T>(string actionId, string elementPath)
    {
        if (!actions.TryGetValue(actionId, out ActionDescriptor? descriptor))
        {
            throw new InvalidOperationException($"Required action binding '{actionId}' is missing at '{elementPath}'.");
        }

        if (descriptor!.PayloadType != typeof(T))
        {
            throw new InvalidOperationException(
                $"Action binding '{actionId}' at '{elementPath}' expects '{descriptor.PayloadType.Name}', not '{typeof(T).Name}'.");
        }
    }

    public void ValidateCommand(string actionId, string elementPath)
    {
        if (!commands.TryGetValue(actionId, out _))
        {
            throw new InvalidOperationException($"Required command binding '{actionId}' is missing at '{elementPath}'.");
        }
    }

    private void AddValue(string elementId, ValueDescriptor descriptor)
    {
        if (values.ContainsKey(elementId))
        {
            throw new InvalidOperationException($"Duplicate value binding '{elementId}'.");
        }

        values.Add(elementId, descriptor);
    }

    private bool TryGetValueDescriptor(string elementId, out ValueDescriptor? descriptor)
    {
        return values.TryGetValue(elementId, out descriptor);
    }

    private static void EnsureType<T>(Type actual, string elementId, string kind)
    {
        if (actual != typeof(T))
        {
            throw new InvalidOperationException(
                $"Binding '{elementId}' ({kind}) is '{actual.Name}', not '{typeof(T).Name}'.");
        }
    }
}
