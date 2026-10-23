using System;
using System.Collections.Generic;
using FerriteLib.UiKit.Kernel;

namespace FerriteLib.UiKit.AuthorExamples;

/// <summary>Public-only author proxy: same reader/Host, replace backing between synchronous passes.</summary>
public class ForwardingBindings : IUiBindingReader
{
    public ForwardingBindings(UiBindings current) => Current = current;
    public UiBindings Current { get; set; }
    public virtual UiBindings BindingOwner => Current;
    public T Get<T>(string key) => Current.Get<T>(key);
    public bool TryGet<T>(string key, out T value) => Current.TryGet(key, out value);
    public bool TryGetBool(string key, out bool value) => Current.TryGetBool(key, out value);
    public void Set<T>(string key, T value) => Current.Set(key, value);
    public bool IsWritable(string key) => Current.IsWritable(key);
    public IReadOnlyList<T> GetOptions<T>(string key) => Current.GetOptions<T>(key);
    public void Invoke<T>(string key, T payload) => Current.Invoke(key, payload);
    public void Invoke(string key) => Current.Invoke(key);
    public bool CanExecute(string key) => Current.CanExecute(key);
    public bool TryInvokeCommand(string key) => Current.TryInvokeCommand(key);
    public void NotifyChanged(params string[] keys) => Current.NotifyChanged(keys);
    public int GetRevision(string key) => Current.GetRevision(key);
    public UiInvalidation GetInvalidation(string key) => Current.GetInvalidation(key);
    public void ValidateValue<T>(string key, string path) => Current.ValidateValue<T>(key, path);
    public void ValidateOptions<T>(string key, string path) => Current.ValidateOptions<T>(key, path);
    public void ValidateAction<T>(string key, string path) => Current.ValidateAction<T>(key, path);
    public void ValidateCommand(string key, string path) => Current.ValidateCommand(key, path);
    public bool TryValue(string key, out UiValueEndpoint? endpoint) => Current.TryValue(key, out endpoint);
    public UiValueEndpoint Value(string key) => Current.Value(key);
    public virtual UiChoiceEndpoint Choices(string key) => Current.Choices(key);
}

