#if NETSTANDARD2_0 || NETFRAMEWORK

using System.Threading.Tasks;

namespace System.Threading;

internal static class CancellationTokenSourceExtensions
{
    public static Task CancelAsync(this CancellationTokenSource cts)
    {
        cts.Cancel();
        return Task.CompletedTask;
    }
}

#endif
