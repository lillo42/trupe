using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Trupe.Guards;

internal static class ObjectDisposedGuard
{
    [StackTraceHidden]
    public static void ThrowIf([DoesNotReturnIf(true)] bool conditional, string objectName)
    {
        if (conditional)
        {
            throw new ObjectDisposedException(objectName);
        }
    }
}
