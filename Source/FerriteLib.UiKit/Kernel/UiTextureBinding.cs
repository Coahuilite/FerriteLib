using System;
using UnityEngine;

namespace FerriteLib.UiKit.Kernel;

/// <summary>Reads supplier-owned textures without converting, retaining, creating or destroying them.</summary>
internal static class UiTextureBinding
{
    internal static void Validate(IUiBindings bindings, string key, string path)
    {
        if (bindings is IUiTypedChoices typed && typed.TryGetValueType(key, out Type type))
        {
            if (!typeof(Texture).IsAssignableFrom(type))
                throw new InvalidOperationException("Image binding '" + key + "' at '" + path + "' must declare a Texture type.");
            return;
        }
        bindings.ValidateValue<Texture2D>(key, path);
    }

    internal static bool TryRead(IUiBindings bindings, string key, out Texture? texture)
    {
        texture = null;
        if (bindings is IUiTypedChoices typed)
        {
            if (!typed.TryGetValueType(key, out Type type) || !typeof(Texture).IsAssignableFrom(type)) return false;
            if (!typed.TryGetTypedValue(key, out object? value)) return false;
            texture = value as Texture;
            return true;
        }
        if (!bindings.TryGet(key, out Texture2D value2D)) return false;
        texture = value2D;
        return true;
    }
}
