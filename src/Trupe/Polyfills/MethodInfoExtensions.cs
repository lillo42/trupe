#if NETSTANDARD2_0 || NETFRAMEWORK

namespace System.Reflection;

internal static class MethodInfoExtensions
{
    public static T CreateDelegate<T>(this MethodInfo methodInfo)
        where T : Delegate
    {
        return (T)methodInfo.CreateDelegate(typeof(T));
    }
}
#endif
