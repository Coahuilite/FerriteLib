using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Typed binding/action surface. All access is generic and validated at Host creation time;
/// no arbitrary reflection or object-dictionary conversion is used by widgets.
/// <para>
/// There are three operations, not two. <see cref="Get{T}"/>/<see cref="Set{T}"/> read and write the
/// authoritative model; <see cref="NotifyChanged"/> is the third, and it is how a consumer says "the
/// model behind these keys moved" without the library ever polling or diffing it. Every key carries a
/// monotonic revision, and the <see cref="UiInvalidation"/> class declared when the binding was
/// registered decides what an announcement actually invalidates - so a page no longer hand-rolls a
/// refresh or an enablement decision.
/// </para>
/// </summary>
public interface IUiBindings
{
    void BindValue<T>(
        string elementId, Func<T> get, Action<T> set, UiInvalidation invalidates = UiInvalidation.Everything);

    void BindReadOnly<T>(string elementId, Func<T> get, UiInvalidation invalidates = UiInvalidation.Everything);

    void BindOptions<T>(
        string elementId, Func<IReadOnlyList<T>> get, UiInvalidation invalidates = UiInvalidation.Everything);

    void BindAction<T>(string actionId, Action<T> action, UiInvalidation invalidates = UiInvalidation.Everything);

    /// <summary>
    /// Binds one command, optionally with a dynamic executability predicate that this surface enforces
    /// rather than merely describes: <see cref="Invoke(string)"/> refuses to run a command whose predicate
    /// answers false, and <see cref="CanExecute"/> is the read a control paints and hit-tests with. A
    /// command whose executability moves is a <see cref="UiInvalidation.Paint"/> change - a disabled plane
    /// and a click that no longer does anything, not a re-measure - which is why that is the declared
    /// default here.
    /// </summary>
    void BindCommand(
        string actionId,
        Action action,
        Func<bool>? canExecute = null,
        UiInvalidation invalidates = UiInvalidation.Paint);

    T Get<T>(string elementId);

    bool TryGet<T>(string elementId, out T value);

    /// <summary>
    /// Reads a bool value binding for a query that must be able to answer "not resolvable" instead of
    /// throwing. <see cref="TryGet{T}"/> reports a key bound to another type as an
    /// <see cref="InvalidOperationException"/>, which is the right contract for a caller that knows the
    /// type it bound and the wrong one for the engine's per-frame visibility query: an authoring mistake
    /// there must leave the element visible and be reported once, not end the frame. False when nobody
    /// bound the key, or bound it to something other than a bool.
    /// </summary>
    bool TryGetBool(string key, out bool value);

    void Set<T>(string elementId, T value);

    /// <summary>
    /// Answers whether the value bound to <paramref name="elementId"/> may be written back through
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
    bool IsWritable(string elementId);

    IReadOnlyList<T> GetOptions<T>(string elementId);

    void Invoke<T>(string actionId, T payload);

    /// <summary>
    /// Runs the command bound to <paramref name="actionId"/> only when it may run. A registered command
    /// whose executability predicate answers false is a legal no-op here rather than an error: the
    /// disabled state is the owner's declared answer about the command, not a caller's mistake - and the
    /// guard lives on the execution path so that a control which forgot to ask still cannot fire a
    /// command its owner said is unavailable. A key nobody bound still throws, exactly as before.
    /// </summary>
    void Invoke(string actionId);

    /// <summary>
    /// Answers whether the command bound to <paramref name="actionId"/> may run now: a registered command
    /// with no predicate does, one with a predicate follows it, and a key with no command binding answers
    /// true. That last answer is deliberate, because this read is the veto a disabled decision consults
    /// rather than an existence check: an action-bound element (a chart publishing a point through
    /// <see cref="BindAction{T}"/>) must not read as disabled, and a page that binds no commands must
    /// behave exactly as it did. Existence is <see cref="Invoke(string)"/>'s answer - it throws for a key
    /// nobody bound.
    /// </summary>
    bool CanExecute(string actionId);

    /// <summary>
    /// The revision of one binding key: monotonic, starts at zero, and moves when the consumer announces
    /// that the authoritative model behind the key changed (<see cref="NotifyChanged"/>). This is the
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

    /// <summary>
    /// Announces that the authoritative model behind one or more keys moved. The library never polls or
    /// diffs the model; this is the whole notification contract, and the consumer owns calling it when
    /// its authoritative state changes. Announcements raised during a pass are coalesced into one commit
    /// at the next frame boundary, and announcing the same key again before that commit costs nothing
    /// extra.
    /// </summary>
    void NotifyChanged(params string[] keys);

    void ValidateValue<T>(string elementId, string elementPath);

    void ValidateOptions<T>(string elementId, string elementPath);

    void ValidateAction<T>(string actionId, string elementPath);

    void ValidateCommand(string actionId, string elementPath);
}
