using System;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Configurators;

/// <summary>
/// Fluent configurator for registering pipeline middlewares scoped to a specific actor type.
/// </summary>
/// <param name="services">The service collection to register middleware configurations into.</param>
/// <param name="actorType">The actor type that the configured middlewares apply to.</param>
public class ActorConfigurator(IServiceCollection services, Type actorType)
{
    /// <summary>
    /// Registers a middleware of type <typeparamref name="TMiddleware"/> with default order and no metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    public ActorConfigurator Use<TMiddleware>()
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, null);
    }

    /// <summary>
    /// Registers a middleware of type <typeparamref name="TMiddleware"/> with the specified execution order.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    public ActorConfigurator Use<TMiddleware>(int order)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(order, null);
    }

    /// <summary>
    /// Registers a middleware of type <typeparamref name="TMiddleware"/> with the specified metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator Use<TMiddleware>(object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, metadata);
    }

    /// <summary>
    /// Registers a middleware of type <typeparamref name="TMiddleware"/> with the specified order and metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator Use<TMiddleware>(int order, object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return Use(typeof(TMiddleware), order, metadata);
    }

    /// <summary>
    /// Registers a middleware by type with default order and no metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    public ActorConfigurator Use(Type middlewareType)
    {
        return Use(middlewareType, 0, null);
    }

    /// <summary>
    /// Registers a middleware by type with the specified execution order.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="order">The execution order; lower values execute first.</param>
    public ActorConfigurator Use(Type middlewareType, int order)
    {
        return Use(middlewareType, order, null);
    }

    /// <summary>
    /// Registers a middleware by type with the specified metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator Use(Type middlewareType, object? metadata)
    {
        return Use(middlewareType, 0, metadata);
    }

    /// <summary>
    /// Registers a middleware by type with the specified order and metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator Use(Type middlewareType, int order, object? metadata)
    {
        services.Configure<PipelineOptions>(opt =>
            opt.Middlewares.Add(
                new PipelineMiddlewareConfiguration
                {
                    Order = order,
                    MiddlewareType = middlewareType,
                    Metadata = metadata,
                    ActorType = actorType,
                }
            )
        );

        return this;
    }

    /// <summary>
    /// Registers a middleware for a specific message type with default order and no metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <typeparam name="TMessage">The message type the middleware applies to.</typeparam>
    public ActorConfigurator UseForMessage<TMiddleware, TMessage>()
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage<TMiddleware, TMessage>(0, null);
    }

    /// <summary>
    /// Registers a middleware for a specific message type with the specified execution order.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <typeparam name="TMessage">The message type the middleware applies to.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    public ActorConfigurator UseForMessage<TMiddleware, TMessage>(int order)
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage<TMiddleware, TMessage>(order, null);
    }

    /// <summary>
    /// Registers a middleware for a specific message type with the specified metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <typeparam name="TMessage">The message type the middleware applies to.</typeparam>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator UseForMessage<TMiddleware, TMessage>(object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage<TMiddleware, TMessage>(0, metadata);
    }

    /// <summary>
    /// Registers a middleware for a specific message type with the specified order and metadata.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <typeparam name="TMessage">The message type the middleware applies to.</typeparam>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator UseForMessage<TMiddleware, TMessage>(int order, object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage(typeof(TMiddleware), typeof(TMessage), order, metadata);
    }

    /// <summary>
    /// Registers a middleware by type for a specific message type with default order and no metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="messageType">The message type the middleware applies to.</param>
    public ActorConfigurator UseForMessage(Type middlewareType, Type messageType)
    {
        return UseForMessage(middlewareType, messageType, 0, null);
    }

    /// <summary>
    /// Registers a middleware by type for a specific message type with the specified execution order.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="messageType">The message type the middleware applies to.</param>
    /// <param name="order">The execution order; lower values execute first.</param>
    public ActorConfigurator UseForMessage(Type middlewareType, Type messageType, int order)
    {
        return UseForMessage(middlewareType, messageType, order, null);
    }

    /// <summary>
    /// Registers a middleware by type for a specific message type with the specified metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="messageType">The message type the middleware applies to.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator UseForMessage(Type middlewareType, Type messageType, object? metadata)
    {
        return UseForMessage(middlewareType, messageType, 0, metadata);
    }

    /// <summary>
    /// Registers a middleware by type for a specific message type with the specified order and metadata.
    /// </summary>
    /// <param name="middlewareType">The middleware type to register.</param>
    /// <param name="messageType">The message type the middleware applies to.</param>
    /// <param name="order">The execution order; lower values execute first.</param>
    /// <param name="metadata">Optional metadata to associate with the middleware.</param>
    public ActorConfigurator UseForMessage(
        Type middlewareType,
        Type messageType,
        int order,
        object? metadata
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
