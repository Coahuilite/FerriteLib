using System;
namespace FerriteLib.UiKit.Kernel;
internal static class UiTextureBinding
{
    internal static void Validate(IUiBindingAccess bindings, string key, string path) => bindings.ValidateValue<UiImage>(key, path);
    internal static bool TryRead(IUiBindingAccess bindings, string key, out UiImage image) => bindings.TryGet(key, out image);
}
