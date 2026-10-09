using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Explicit typed binding registry. Every binding is registered with a concrete <c>T</c>; all
/// widget access is generic, so type mismatches surface at Host creation time rather than Draw.
/// <para>
/// This is also the notification side: every key carries a monotonic revision and an invalidation class,
/// and <see cref="NotifyChanged"/> is the one entry point a consumer calls when its authoritative model
/// moved. The registry never polls, diffs or wraps the model - it records that a key was announced, and
/// the engine folds the announcements into one commit at the next arrangement boundary.
/// </para>
/// </summary>
public sealed class UiBindings : IUiBindings, IUiTypedChoices
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

        /// <summary>
        /// True when the item type is a <see cref="UiChoice{T}"/>: the list carries typed choices rather than
        /// strings or <see cref="UiOption"/> pairs, which is what the type-erased seam needs to know before it
        /// can hand a widget labels plus boxed values.
        /// </summary>
        public virtual bool IsChoiceList => false;

        /// <summary>The value type those choices declare, or null when this is not a choices list.</summary>
        public virtual Type? ChoiceValueType => null;
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

        // The shape is answered WITHOUT reflection and without an instance of the list: default(T) is a boxed
        // UiChoice<U> exactly when T is one (a struct, so it implements the erased face on its own), and a
        // default choice carries the declared U in its ValueType. A string list, a UiOption list and every
        // other item type answer false, so their pages keep the string path untouched.
        public override bool IsChoiceList => default(T) is IUiChoice;

        public override Type? ChoiceValueType => (default(T) is IUiChoice sample) ? sample.ValueType : null;
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
        public CommandDescriptor(Action action, Func<bool>? canExecute)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            CanExecute = canExecute;
        }

        public Action Action { get; }

        /// <summary>Null means "no predicate": a registered command runs whenever it is invoked.</summary>
        public Func<bool>? CanExecute { get; }
    }

    private readonly Dictionary<string, ValueDescriptor> values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OptionsDescriptor> options = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActionDescriptor> actions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommandDescriptor> commands = new(StringComparer.Ordinal);

    // The notification side. declaredInvalidation is what each key says an announcement of it means;
    // a key with no entry answers UiInvalidation.Everything. revisions is the monotonic per-key counter
    // NotifyChanged moves, read through GetRevision.
    private readonly Dictionary<string, UiInvalidation> declaredInvalidation = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> revisions = new(StringComparer.Ordinal);

    public void BindValue<T>(
        string elementId, Func<T> get, Action<T> set, UiInvalidation invalidates = UiInvalidation.Everything)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        if (get == null) throw new ArgumentNullException(nameof(get));
        if (set == null) throw new ArgumentNullException(nameof(set));
        AddValue(elementId, new ValueDescriptor<T>(get, set), invalidates);
    }

    public void BindReadOnly<T>(
        string elementId, Func<T> get, UiInvalidation invalidates = UiInvalidation.Everything)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        if (get == null) throw new ArgumentNullException(nameof(get));
        AddValue(elementId, new ValueDescriptor<T>(get, null), invalidates);
    }

    public void BindOptions<T>(
        string elementId, Func<IReadOnlyList<T>> get, UiInvalidation invalidates = UiInvalidation.Everything)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        if (get == null) throw new ArgumentNullException(nameof(get));
        if (options.ContainsKey(elementId))
        {
            throw new InvalidOperationException($"Duplicate options binding '{elementId}'.");
        }

        options.Add(elementId, new OptionsDescriptor<T>(get));
        DeclareInvalidation(elementId, invalidates);
    }

    public void BindAction<T>(
        string actionId, Action<T> action, UiInvalidation invalidates = UiInvalidation.Everything)
    {
        if (actionId == null) throw new ArgumentNullException(nameof(actionId));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (actions.ContainsKey(actionId))
        {
            throw new InvalidOperationException($"Duplicate action binding '{actionId}'.");
        }

        actions.Add(actionId, new ActionDescriptor<T>(action));
        DeclareInvalidation(actionId, invalidates);
    }

    public void BindCommand(
        string actionId,
        Action action,
        Func<bool>? canExecute = null,
        UiInvalidation invalidates = UiInvalidation.Paint)
    {
        if (actionId == null) throw new ArgumentNullException(nameof(actionId));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (commands.ContainsKey(actionId))
        {
            throw new InvalidOperationException($"Duplicate command binding '{actionId}'.");
        }

        commands.Add(actionId, new CommandDescriptor(action, canExecute));
        DeclareInvalidation(actionId, invalidates);
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

    /// <summary>
    /// The probe <see cref="IUiBindings.TryGetBool"/> documents: a missing key and a key bound to another
    /// type are both "not resolved", because the caller here is a per-frame visibility query rather than
    /// a caller that knows what it bound.
    /// </summary>
    public bool TryGetBool(string key, out bool value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (values.TryGetValue(key, out ValueDescriptor? descriptor) && descriptor!.ValueType == typeof(bool))
        {
            value = (bool)descriptor.GetBoxed()!;
            return true;
        }

        value = false;
        return false;
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

    /// <summary>
    /// The read side of <see cref="Set{T}"/>: a bind registered through <see cref="BindReadOnly{T}"/>
    /// answers false, a key nobody bound answers false (the conservative answer for "may this write?"),
    /// and a null key throws like the rest of the surface.
    /// </summary>
    public bool IsWritable(string elementId)
    {
        if (elementId == null) throw new ArgumentNullException(nameof(elementId));
        return values.TryGetValue(elementId, out ValueDescriptor? descriptor) && descriptor!.CanWrite;
    }

    public IReadOnlyList<T> GetOptions<T>(string elementId)
    {
        if (!options.TryGetValue(elementId, out OptionsDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No options binding registered for '{elementId}'.");
        }

        if (descriptor!.ItemType != typeof(T))
        {
            // FL-23: the throw already names the wrong element type, but a consumer that catches it keeps
            // rendering a degraded control with nothing on the library's audit surface to explain why. The
            // refusal is unchanged; what is added is that it is REPORTED, once per distinct mismatch, on the
            // same fail-soft channel the appearance and overflow findings use (FL-21/FL-22).
            UiFitAudit.ReportStyleFallback(
                elementId, "binding", "OptionsBind", descriptor.ItemType.Name, typeof(T).Name);
            throw new InvalidOperationException(
                $"Options binding '{elementId}' is '{descriptor.ItemType.Name}', not '{typeof(T).Name}'.");
        }

        return (IReadOnlyList<T>)descriptor.GetListBoxed();
    }

    // --- IUiTypedChoices: the additive, type-erased half the non-generic widgets probe (R4-A, 0.7.x) -------

    /// <summary>
    /// The declared type of a key: its value binding's type when it has one, else the value type its typed
    /// choices declare. A key that is neither answers false, so a widget can tell "not typed here" from
    /// "bound to string" without guessing.
    /// </summary>
    public bool TryGetValueType(string key, out Type valueType)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (values.TryGetValue(key, out ValueDescriptor? descriptor))
        {
            valueType = descriptor!.ValueType;
            return true;
        }

        if (options.TryGetValue(key, out OptionsDescriptor? choices) && choices!.ChoiceValueType != null)
        {
            valueType = choices.ChoiceValueType!;
            return true;
        }

        valueType = typeof(object);
        return false;
    }

    /// <summary>The current value as its real instance, boxed - the read half of the typed path.</summary>
    public bool TryGetTypedValue(string key, out object? value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (values.TryGetValue(key, out ValueDescriptor? descriptor))
        {
            value = descriptor!.GetBoxed();
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// True when the value the consumer's own setter would receive is an instance of the declared type. This is
    /// the acceptance test the write applies, exposed so a control can judge its whole option list at creation
    /// time - an option whose value is the wrong type is an authoring mistake, not something to find one
    /// refused click at a time. Writes nothing.
    /// </summary>
    public bool AcceptsValue(string key, object? value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return values.TryGetValue(key, out ValueDescriptor? descriptor) && Accepts(descriptor!.ValueType, value);
    }

    /// <summary>
    /// Writes a boxed value through the key's own typed setter, and REFUSES a value of the wrong type without
    /// writing anything: the decorated <c>(T)value</c> cast would otherwise throw inside the setter (or, worse,
    /// coerce), so the type check happens here, before any write, and a refusal is reported as false.
    /// </summary>
    public bool TrySetTypedValue(string key, object? value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (!values.TryGetValue(key, out ValueDescriptor? descriptor) || !descriptor!.CanWrite) return false;
        if (!Accepts(descriptor.ValueType, value)) return false;
        descriptor.SetBoxed(value);
        return true;
    }

    /// <summary>
    /// The typed choices bound at a key, erased to labels plus boxed values. False for every other options
    /// shape (a string list, a <see cref="UiOption"/> list) and for a key with no options binding, which is
    /// what keeps those pages on their existing path.
    /// </summary>
    public bool TryGetChoices(string key, out IReadOnlyList<UiChoice<object?>> choices)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (options.TryGetValue(key, out OptionsDescriptor? descriptor)
            && descriptor!.IsChoiceList
            && descriptor.GetListBoxed() is System.Collections.IEnumerable items)
        {
            var erased = new List<UiChoice<object?>>();
            foreach (object? item in items)
            {
                if (item is IUiChoice choice)
                {
                    erased.Add(new UiChoice<object?>(choice.Text, choice.BoxedValue));
                }
            }

            choices = erased;
            return true;
        }

        choices = Array.Empty<UiChoice<object?>>();
        return false;
    }

    /// <summary>
    /// The whole acceptance rule, in one place: null is a value of the declared type only where the declared
    /// type is a reference type or a nullable value type, and any other value must be an instance of that type
    /// (unwrapped for nullables, whose boxed form is the underlying value).
    /// </summary>
    private static bool Accepts(Type declared, object? value)
    {
        Type? underlying = Nullable.GetUnderlyingType(declared);
        if (value == null) return !declared.IsValueType || underlying != null;
        return (underlying ?? declared).IsInstanceOfType(value);
    }

    public void Invoke<T>(string actionId, T payload)
    {
        if (!actions.TryGetValue(actionId, out ActionDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No action binding registered for '{actionId}'.");
        }

        if (descriptor!.PayloadType != typeof(T))
        {
            // FL-23, the same silence one member over: an action payload read as the wrong type is refused
            // and now also reported, so a caught exception is still visible to whoever reads the audit.
            UiFitAudit.ReportStyleFallback(
                actionId, "binding", "ActionBind", descriptor.PayloadType.Name, typeof(T).Name);
            throw new InvalidOperationException(
                $"Action binding '{actionId}' expects '{descriptor.PayloadType.Name}', not '{typeof(T).Name}'.");
        }

        descriptor.InvokeBoxed(payload);
    }

    /// <summary>
    /// Runs the command only when it may run. The disabled answer is the owner's declaration, so it is a
    /// no-op here and not an exception - but it is enforced on the execution path, which is what keeps a
    /// control that forgot to ask from firing a command its owner has taken away.
    /// </summary>
    public void Invoke(string actionId)
    {
        if (!commands.TryGetValue(actionId, out CommandDescriptor? descriptor))
        {
            throw new KeyNotFoundException($"No command binding registered for '{actionId}'.");
        }

        if (!CanRun(descriptor!)) return;
        descriptor!.Action();
    }

    /// <summary>
    /// The veto the disabled decision consults. A registered command with no predicate is executable; one
    /// with a predicate follows it; a key with no command binding answers true so an action-bound or
    /// value-bound element is never mistaken for a disabled command.
    /// </summary>
    public bool CanExecute(string actionId)
    {
        if (actionId == null) throw new ArgumentNullException(nameof(actionId));
        return !commands.TryGetValue(actionId, out CommandDescriptor? descriptor) || CanRun(descriptor!);
    }

    /// <summary>
    /// Existence AND executability in one answer, for the caller that must not throw: the Cancel ladder asks a
    /// declared key whether it names a command it can run now, and a key nobody bound is one more rung to
    /// climb past rather than an exception to recover from. <see cref="CanExecute"/> cannot serve this read
    /// because it answers true for an unbound key on purpose, and <see cref="Invoke(string)"/> cannot because
    /// it throws for one.
    /// </summary>
    public bool TryInvokeCommand(string actionId)
    {
        if (actionId == null) throw new ArgumentNullException(nameof(actionId));
        if (!commands.TryGetValue(actionId, out CommandDescriptor? descriptor)) return false;
        if (!CanRun(descriptor!)) return false;

        descriptor!.Action();
        return true;
    }

    private static bool CanRun(CommandDescriptor descriptor)
    {
        return descriptor.CanExecute == null || descriptor.CanExecute();
    }

    public int GetRevision(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return revisions.TryGetValue(key, out int revision) ? revision : 0;
    }

    public UiInvalidation GetInvalidation(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return declaredInvalidation.TryGetValue(key, out UiInvalidation invalidates)
            ? invalidates
            : UiInvalidation.Everything;
    }

    /// <summary>
    /// Moves each named key's revision. Repeating a key before the engine's next commit only moves the
    /// counter further - the commit compares a revision once and marks the declaring node at most once -
    /// so a consumer that announces generously cannot multiply work.
    /// </summary>
    public void NotifyChanged(params string[] keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(
                    "NotifyChanged needs a binding key per entry; entry " + i + " is null or empty.", nameof(keys));
            }

            revisions[key] = (revisions.TryGetValue(key, out int revision) ? revision : 0) + 1;
        }
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
            // A5 (0.7): when the SAME key is registered as a value the "missing" message names the
            // wrong cause - BindReadOnly<IReadOnlyList<T>> compiles, passes value validation, and
            // then the dropdown creation blames an options table nobody ever claimed to write
            // (found by the demo mod 2026-09-16). Explain the category error and name the fix.
            // Diagnose only: no value registration is ever coerced into an options one.
            if (values.TryGetValue(elementId, out ValueDescriptor? valueDescriptor))
            {
                throw new InvalidOperationException(
                    $"Required options binding '{elementId}' is missing at '{elementPath}': the same key is registered as a {(valueDescriptor!.CanWrite ? "writable" : "read-only")} value of type '{valueDescriptor.ValueType.Name}', not as options. Register the option list with BindOptions<{valueDescriptor.ValueType.Name}>('{elementId}', ...).");
            }

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

    private void AddValue(string elementId, ValueDescriptor descriptor, UiInvalidation invalidates)
    {
        if (values.ContainsKey(elementId))
        {
            throw new InvalidOperationException($"Duplicate value binding '{elementId}'.");
        }

        values.Add(elementId, descriptor);
        DeclareInvalidation(elementId, invalidates);
    }

    private void DeclareInvalidation(string key, UiInvalidation invalidates)
    {
        declaredInvalidation[key] = invalidates;
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
