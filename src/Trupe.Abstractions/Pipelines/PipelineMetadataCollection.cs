using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Trupe.Abstractions.Pipelines;

public class PipelineMetadataCollection(ImmutableList<object> value) : IReadOnlyList<object>
{
    public object this[int index] => value[index];

    public int Count => value.Count;

    public IEnumerator<object> GetEnumerator()
    {
        return value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public T? GetMetadata<T>()
        where T : class
    {
        foreach (var item in value)
        {
            if (item is T metadata)
            {
                return metadata;
            }
        }

        return default;
    }

    public T GetRequiredMetadata<T>()
        where T : class
    {
        foreach (var item in value)
        {
            if (item is T metadata)
            {
                return metadata;
            }
        }

        throw new InvalidOperationException(
            $"Required metadata of type {typeof(T).FullName} not found."
        );
    }

    public IReadOnlyList<T> GetOrderedMetadata<T>()
        where T : class
    {
        return value.OfType<T>().ToImmutableList();
    }
}
