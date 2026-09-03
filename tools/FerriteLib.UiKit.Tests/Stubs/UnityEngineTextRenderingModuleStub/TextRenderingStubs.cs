using System.Runtime.CompilerServices;
using UnityEngine;

// The real Unity distribution keeps TextAnchor in UnityEngine.TextRenderingModule while the minimal
// UiKit stub keeps it in UnityEngine.CoreModule. This assembly forwards the type so UiKit code
// compiled against the real ref assemblies resolves to the executable CoreModule stub at runtime.
[assembly: TypeForwardedTo(typeof(UnityEngine.TextAnchor))]
