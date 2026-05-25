using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Trupe.Abstractions.Exceptions;

namespace Trupe.Abstractions.Pipelines;

/// <summary>
/// A read-only, indexed collection of metadata objects associated with a pipeline execution.
/// </summary>
/// <param name="value">The immutable list of metadata objects backing this collection.</param>
public class PipelineMetadataCollection(ImmutableList<object> value) : IReadOnlyList<object>
{
    /// <summary>
    /// Gets the metadata object at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the metadata object to retrieve.</param>
    /// <returns>The metadata object at the specified index.</returns>
    public object this[int index] => value[index];

    /// <summary>
    /// Gets the number of metadata objects in the collection.
    /// </summary>
    public int Count => value.Count;

    /// <summary>
    /// Returns an enumerator that iterates through the metadata objects.
    /// </summary>
    /// <returns>An enumerator for the metadata collection.</returns>
    public IEnumerator<object> GetEnumerator()
    {
        return value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Returns the first metadata object of the specified type, or <c>null</c> if none exists.
    /// </summary>
    /// <typeparam name="T">The type of metadata to search for.</typeparam>
    /// <returns>The first matching metadata instance, or <c>null</c>.</returns>
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

    /// <summary>
    /// Returns the first metadata object of the specified type, or throws if none exists.
    /// </summary>
    /// <typeparam name="T">The type of metadata to search for.</typeparam>
    /// <returns>The first matching metadata instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no metadata of type <typeparamref name="T"/> is found.</exception>
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

        throw new RequiredMetadataNotFoundException(typeof(T));
    }

    /// <summary>
    /// Returns all metadata objects of the specified type, preserving their original order.
    /// </summary>
    /// <typeparam name="T">The type of metadata to filter by.</typeparam>
    /// <returns>A read-only list of all matching metadata instances.</returns>
    public IReadOnlyList<T> GetOrderedMetadata<T>()
        where T : class
    {
        return value.OfType<T>().ToImmutableList();
    }
}
