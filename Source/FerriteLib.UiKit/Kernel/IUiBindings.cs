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

    IReadOnlyList<T> GetOptions<T>(string elementId);

    void Invoke<T>(string actionId, T payload);

    void Invoke(string actionId);

    void ValidateValue<T>(string elementId, string elementPath);

    void ValidateOptions<T>(string elementId, string elementPath);

    void ValidateAction<T>(string actionId, string elementPath);

    void ValidateCommand(string actionId, string elementPath);
}
