using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Immutable configuration, not a texture owner. Null window configuration means the original shell.</summary>
public sealed class UiWindowBackground
{
    public UiWindowBackground(string bind, UiImageOptions options = default)
    {
        if (string.IsNullOrWhiteSpace(bind)) throw new ArgumentException("A background needs a texture binding key.", nameof(bind));
        Bind = bind.Trim();
        Options = options;
    }
    public string Bind { get; }
    public UiImageOptions Options { get; }
}

