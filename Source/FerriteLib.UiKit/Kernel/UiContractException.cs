using System;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Creation-time UI contract failure carrying source, element id/kind and layout path.</summary>
public sealed class UiContractException : Exception
{
    public string Scope { get; }

    public string ElementId { get; }

    public string Kind { get; }

    public string ElementPath { get; }

    public UiContractException(
        string message,
        string scope,
        string elementId,
        string kind,
        string elementPath,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Scope = scope ?? "";
        ElementId = elementId ?? "";
        Kind = kind ?? "";
        ElementPath = elementPath ?? "";
    }
}
