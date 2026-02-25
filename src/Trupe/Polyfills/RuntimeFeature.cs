#if NETSTANDARD2_0 || NETFRAMEWORK

namespace System.Runtime.CompilerServices;

internal static class RuntimeFeature
{
    public static bool IsDynamicCodeSupported => false;
}

#endif
