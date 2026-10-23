using System;
namespace FerriteLib.UiKit.Kernel;
internal static class UiTextureBinding
{
    internal static void Validate(IUiBindingReader bindings, string key, string path) => bindings.ValidateValue<UiImage>(key, path);
    internal static bool TryRead(IUiBindingReader bindings, string key, out UiImage image) => bindings.TryGet(key, out image);
}

