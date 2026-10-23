using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Raised when a manifest references a widget Kind that is not registered for the scope.</summary>
public sealed class UiUnknownWidgetKindException : Exception
{
    public string Scope { get; }

    public string Kind { get; }

    public UiUnknownWidgetKindException(string scope, string kind)
        : base($"Unknown widget kind '{kind}' for scope '{scope}'.")
    {
        Scope = scope ?? "";
        Kind = kind ?? "";
    }
}
