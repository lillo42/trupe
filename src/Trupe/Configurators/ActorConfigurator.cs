using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Configurators;

/// <summary>
/// Fluent configurator for registering pipeline middlewares scoped to a specific actor type.
/// </summary>
/// <param name="services">The service collection to register middleware configurations into.</param>
/// <param name="actorType">The actor type that the configured middlewares apply to.</param>
public class ActorConfigurator(
    IServiceCollection services,
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )]
        Type actorType
)
{
    /// <summary>
    /// Registers a middleware of type <typeparamref name="TMiddleware"/> with the specified order and metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator Use<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )] TMiddleware>(int order = 0, object? metadata = null)
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage(typeof(TMiddleware), null, order, metadata);
    }

    /// <summary>
    /// Registers a middleware by type with the specified order and metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator Use(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicMethods
        )]
            Type middlewareType,
        int order = 0,
        object? metadata = null
    )
    {
        return UseForMessage(middlewareType, null, order, metadata);
    }

    /// <summary>
    /// Registers a middleware for a specific message type with the specified order and metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <typeparam name="TMessage">The message type the middleware applies to.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator UseForMessage<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicMethods
    )] TMiddleware, TMessage>(
        int order = 0,
        object? metadata = null
    )
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage(typeof(TMiddleware), typeof(TMessage), order, metadata);
    }

    /// <summary>
    /// Registers a middleware by type for a specific message type with the specified order and metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="messageType">The message type the middleware applies to.</param>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator UseForMessage(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicMethods
        )]
            Type middlewareType,
        Type? messageType,
        int order = 0,
        object? metadata = null
    )
    {
        services.Configure<PipelineOptions>(opt =>
            opt.Middlewares.Add(
                new PipelineMiddlewareConfiguration
                {
                    Order = order,
                    Metadata = metadata,
                    MiddlewareType = middlewareType,
                    ActorType = actorType,
                    MessageType = messageType,
                }
            )
        );

        return this;
    }
}
