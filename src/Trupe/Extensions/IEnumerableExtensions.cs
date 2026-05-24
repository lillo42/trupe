using System;
using System.Collections.Generic;

namespace Trupe.Extensions;

internal static class IEnumberableExtensions
{
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
        }
    }
}
