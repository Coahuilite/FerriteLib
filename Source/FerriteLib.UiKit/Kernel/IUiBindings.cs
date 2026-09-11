using System;
using System.Collections.Generic;

namespace FerriteLib.UiKit.Kernel;

/// <summary>
/// Typed binding/action surface. All access is generic and validated at Host creation time;
/// no arbitrary reflection or object-dictionary conversion is used by widgets.
/// </summary>
public interface IUiBindings
{
    void BindValue<T>(string elementId, Func<T> get, Action<T> set);

    void BindReadOnly<T>(string elementId, Func<T> get);

    void BindOptions<T>(string elementId, Func<IReadOnlyList<T>> get);

    void BindAction<T>(string actionId, Action<T> action);

    void BindCommand(string actionId, Action action);

    T Get<T>(string elementId);

    bool TryGet<T>(string elementId, out T value);

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

    void Invoke(string actionId);

    void ValidateValue<T>(string elementId, string elementPath);

    void ValidateOptions<T>(string elementId, string elementPath);

    void ValidateAction<T>(string actionId, string elementPath);

    void ValidateCommand(string actionId, string elementPath);
}
