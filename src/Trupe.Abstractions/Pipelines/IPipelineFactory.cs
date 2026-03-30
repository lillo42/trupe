using System;
using System.Diagnostics.CodeAnalysis;

namespace Trupe.Abstractions.Pipelines;

public interface IPipelineFactory
{
    IPipeline Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type actorType,
        Type messageType
    );
}
