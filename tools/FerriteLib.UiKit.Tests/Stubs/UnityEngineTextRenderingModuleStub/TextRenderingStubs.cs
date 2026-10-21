using System.Runtime.CompilerServices;
using UnityEngine;

// The real Unity distribution keeps TextAnchor in UnityEngine.TextRenderingModule while the minimal
// UiKit stub keeps it in UnityEngine.CoreModule. This assembly forwards the type so UiKit code
// compiled against the real ref assemblies resolves to the executable CoreModule stub at runtime.
[assembly: TypeForwardedTo(typeof(UnityEngine.TextAnchor))]

namespace UnityEngine
{
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public sealed class Font : Object
    {
        public int fontSize => 14;
        public bool dynamic => false;
        public string[] fontNames { get; set; } = new[] { "Stub font; no native raster evidence" };
    }
}
