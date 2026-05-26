using System;

namespace Trupe.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when required pipeline metadata of an expected type is not present in the context.
/// </summary>
/// <param name="metadataType">The type of metadata that was expected but not found.</param>
public class RequiredMetadataNotFoundException(Type metadataType)
    : TrupeException($"Required metadata of type {metadataType.FullName} not found.")
{
    /// <summary>
    /// Gets the type of metadata that was expected but not found in the pipeline context.
    /// </summary>
    public Type MetadataType { get; } = metadataType;
}
