using System;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Configurators;

public class ActorConfigurator(IServiceCollection services, Type actorType)
{
    public ActorConfigurator Use<TMiddleware>()
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, null);
    }

    public ActorConfigurator Use<TMiddleware>(int order)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(order, null);
    }

    public ActorConfigurator Use<TMiddleware>(object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return Use<TMiddleware>(0, metadata);
    }

    public ActorConfigurator Use<TMiddleware>(int order, object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return Use(typeof(TMiddleware), order, metadata);
    }

    public ActorConfigurator Use(Type middlewareType)
    {
        return Use(middlewareType, 0, null);
    }

    public ActorConfigurator Use(Type middlewareType, int order)
    {
        return Use(middlewareType, order, null);
    }

    public ActorConfigurator Use(Type middlewareType, object? metadata)
    {
        return Use(middlewareType, 0, metadata);
    }

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

    public ActorConfigurator UseForMessage<TMiddleware, TMessage>()
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage<TMiddleware, TMessage>(0, null);
    }

    public ActorConfigurator UseForMessage<TMiddleware, TMessage>(int order)
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage<TMiddleware, TMessage>(order, null);
    }

    public ActorConfigurator UseForMessage<TMiddleware, TMessage>(object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage<TMiddleware, TMessage>(0, metadata);
    }

    public ActorConfigurator UseForMessage<TMiddleware, TMessage>(int order, object? metadata)
        where TMiddleware : class, IMiddleware
    {
        return UseForMessage(typeof(TMiddleware), typeof(TMessage), order, metadata);
    }

    public ActorConfigurator UseForMessage(Type middlewareType, Type messageType)
    {
        return UseForMessage(middlewareType, messageType, 0, null);
    }

    public ActorConfigurator UseForMessage(Type middlewareType, Type messageType, int order)
    {
        return UseForMessage(middlewareType, messageType, order, null);
    }

    public ActorConfigurator UseForMessage(Type middlewareType, Type messageType, object? metadata)
    {
        return UseForMessage(middlewareType, messageType, 0, metadata);
    }

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
