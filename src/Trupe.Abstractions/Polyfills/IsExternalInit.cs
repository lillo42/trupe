#if NETSTANDARD2_0 || NETFRAMEWORK
using System.ComponentModel;

namespace System.Runtime.CompilerServices;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit { }
#endif
