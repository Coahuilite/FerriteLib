using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Typed runtime binding access: reads, writes, requests and owner-bound value/choice endpoints.
/// Binding arguments are registry addresses, not element identities. Registration and NotifyChanged
/// belong to UiBindings; ordinary getters gain no reactive prerequisite. Widgets may use the supplied
/// sealed endpoints (including TryWrite) rather than Get/Set. Forward both endpoints and BindingOwner
/// from the same current registry; Get/Set alone are not a universal interception hook.
/// </summary>
public interface IUiBindingAccess
{
    /// <summary>
    /// The current registry owning endpoints and optional tracked presentation capabilities.
    /// A forwarding reader returns its current backing, while reads/commands still go through the
    /// reader. Ordinary bindings need no tracked registration. This is not ownership transfer.
    /// </summary>
    UiBindings BindingOwner { get; }

    T Get<T>(string binding);

    bool TryGet<T>(string binding, out T value);

    /// <summary>
    /// Reads a bool value binding for a query that must be able to answer "not resolvable" instead of
    /// throwing. <see cref="TryGet{T}"/> reports a key bound to another type as an
    /// <see cref="InvalidOperationException"/>, which is the right contract for a caller that knows the
    /// type it bound and the wrong one for the engine's per-frame visibility query: an authoring mistake
    /// there must leave the element visible and be reported once, not end the frame. False when nobody
    /// bound the key, or bound it to something other than a bool.
    /// </summary>
    bool TryGetBool(string key, out bool value);

    void Set<T>(string binding, T value);

    /// <summary>
    /// Answers whether the value bound to <paramref name="binding"/> may be written back through
    /// <see cref="Set{T}"/>. A read-only binding is a value its owner publishes but does not accept
    /// changes for, and without this query a widget cannot tell such an element from a writable one: it
    /// draws the same control and only finds out by throwing, or by silently doing nothing while the
    /// player clicks it.
    /// <para>
    /// A key with no value binding answers false. The question is "may this control write?", so anything
    /// that cannot be shown writable answers no — the conservative direction, and the one that keeps an
    /// unresolved element from presenting itself as usable. A null key is a caller error and throws, as
    /// the rest of the surface does.
    /// </para>
    /// </summary>
    bool IsWritable(string binding);

    IReadOnlyList<T> GetOptions<T>(string binding);

    void Invoke<T>(string actionBind, T payload);

    /// <summary>
    /// Runs the command bound to <paramref name="actionBind"/> only when it may run. A registered command
    /// whose executability predicate answers false is a legal no-op here rather than an error: the
    /// disabled state is the owner's declared answer about the command, not a caller's mistake - and the
    /// guard lives on the execution path so that a control which forgot to ask still cannot fire a
    /// command its owner said is unavailable. A key nobody bound still throws, exactly as before.
    /// </summary>
    void Invoke(string actionBind);

    /// <summary>
    /// Answers whether the command bound to <paramref name="actionBind"/> may run now: a registered command
    /// with no predicate does, one with a predicate follows it, and a key with no command binding answers
    /// true. That last answer is deliberate, because this read is the veto a disabled decision consults
    /// rather than an existence check: an action-bound element (a chart publishing a point through
    /// <see cref="BindAction{T}"/>) must not read as disabled, and a page that binds no commands must
    /// behave exactly as it did. Existence is <see cref="Invoke(string)"/>'s answer - it throws for a key
    /// nobody bound.
    /// </summary>
    bool CanExecute(string actionBind);

    /// <summary>
    /// Runs the command bound to <paramref name="actionBind"/> when — and only when — such a command is
    /// registered AND may run now, and reports whether it ran. The existence-aware sibling of
    /// <see cref="CanExecute"/> and <see cref="Invoke(string)"/>, added for the one caller that must ask both
    /// questions and is not allowed to throw: the Cancel ladder walks the element tree looking for the nearest
    /// layer a declared key actually names, and a page may declare a key its consumer never registered — a
    /// thrown <see cref="System.Collections.Generic.KeyNotFoundException"/> there would take the window down
    /// instead of moving one layer up the tree.
    /// </summary>
    bool TryInvokeCommand(string actionBind);

    /// <summary>
    /// The revision of one binding key: monotonic, starts at zero, and moves when the consumer announces
    /// that the authoritative model behind the key changed (<see cref="UiBindings.NotifyChanged"/>). This is the
    /// per-key replacement for one global counter, and it is what a widget, a diagnostic or a test reads
    /// to know whether the key it cares about moved.
    /// </summary>
    int GetRevision(string key);

    /// <summary>
    /// The invalidation class declared for one key when it was registered. A key with no declaration
    /// answers <see cref="UiInvalidation.Everything"/>: not knowing what a key moves is not a reason to
    /// under-invalidate, and an over-invalidation costs one arrangement while a stale one shows geometry
    /// the model no longer has.
    /// </summary>
    UiInvalidation GetInvalidation(string key);

    void ValidateValue<T>(string binding, string elementPath);

    void ValidateOptions<T>(string binding, string elementPath);

    void ValidateAction<T>(string actionBind, string elementPath);

    void ValidateCommand(string actionBind, string elementPath);
    bool TryValue(string key,out UiValueEndpoint? endpoint);
    UiValueEndpoint Value(string key);
    UiChoiceEndpoint Choices(string key);
}
