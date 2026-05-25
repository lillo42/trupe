using System;

namespace Trupe.Abstractions.Exceptions;

public class RequiredMetadataNotFoundException(Type metadataType)
    : TrupeException($"Required metadata of type {metadataType.FullName} not found.")
{
    public Type MetadataType { get; } = metadataType;
}
