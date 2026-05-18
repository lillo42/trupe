using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Extensions;
using Trupe.IntegrationTests.Actors;
using Trupe.Supervisors;

namespace Trupe.IntegrationTests;

#region Test Middlewares

/// <summary>
/// Thread-safe log used by test middlewares to record invocations.
/// </summary>
public static class MiddlewareLog
{
    private static readonly ConcurrentQueue<string> _entries = new();

    public static void Record(string entry) => _entries.Enqueue(entry);

    public static List<string> GetEntries() => _entries.ToList();

    public static void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}

/// <summary>
/// A receive middleware that logs before and after calling next.
/// </summary>
public class LoggingReceiveMiddleware : IReceiveMiddleware
{
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        MiddlewareLog.Record($"ReceiveBefore:{context.Message.Payload.GetType().Name}");
        await next(context);
        MiddlewareLog.Record($"ReceiveAfter:{context.Message.Payload.GetType().Name}");
    }
}

/// <summary>
/// A send middleware that logs before and after calling next.
/// </summary>
public class LoggingSendMiddleware : ISendMiddleware
{
    public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
    {
        MiddlewareLog.Record($"SendBefore:{context.Message.Payload.GetType().Name}");
        await next(context);
        MiddlewareLog.Record($"SendAfter:{context.Message.Payload.GetType().Name}");
    }
}

/// <summary>
/// A receive middleware that modifies items in the pipeline context.
/// </summary>
public class ItemSettingReceiveMiddleware : IReceiveMiddleware
{
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        context.Items["touched-by-middleware"] = true;
        await next(context);
    }
}

/// <summary>
/// A receive middleware that records the execution order.
/// </summary>
public class OrderedReceiveMiddlewareA : IReceiveMiddleware
{
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        MiddlewareLog.Record("MiddlewareA");
        await next(context);
    }
}

/// <summary>
/// Another receive middleware for order testing.
/// </summary>
public class OrderedReceiveMiddlewareB : IReceiveMiddleware
{
    public async ValueTask InvokeAsync(IReceivePipelineContext context, NextReceiveDelegate next)
    {
        MiddlewareLog.Record("MiddlewareB");
        await next(context);
    }
}

/// <summary>
/// A send middleware that enriches message metadata with a custom key.
/// </summary>
public class MetadataEnrichingSendMiddleware : ISendMiddleware
{
    public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
    {
        MiddlewareLog.Record($"SendEnrich:{context.ActorType.Name}");
        await next(context);
    }
}

#endregion

/// <summary>
/// Integration tests for the receive pipeline middleware.
/// </summary>
public class ReceivePipelineTests
{
    [Test]
    public async Task ReceiveMiddleware_IsInvoked_WhenActorReceivesMessage()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<EchoActor>();
            cfg.AddMiddleware(new LoggingReceiveMiddleware());
            cfg.Use<LoggingReceiveMiddleware>();
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act
            var response = await actorRef.AskAsync<Pong>(new Ping("pipeline-test"));

            // Assert - response should be correct
            await Assert.That(response.Payload).IsEqualTo("pipeline-test");

            // Assert - middleware should have been invoked
            var entries = MiddlewareLog.GetEntries();
            await Assert.That(entries).Contains("ReceiveBefore:Ping");
            await Assert.That(entries).Contains("ReceiveAfter:Ping");
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task ReceiveMiddleware_ExecutesInOrder()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<EchoActor>();
            cfg.AddMiddleware(new OrderedReceiveMiddlewareA());
            cfg.AddMiddleware(new OrderedReceiveMiddlewareB());
            // A has lower order (1) so it should execute first
            cfg.Use<OrderedReceiveMiddlewareA>(1);
            cfg.Use<OrderedReceiveMiddlewareB>(2);
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act
            var response = await actorRef.AskAsync<Pong>(new Ping("order-test"));
            await Assert.That(response.Payload).IsEqualTo("order-test");

            // Assert - MiddlewareA should appear before MiddlewareB
            var entries = MiddlewareLog.GetEntries();
            var indexA = entries.IndexOf("MiddlewareA");
            var indexB = entries.IndexOf("MiddlewareB");
            await Assert.That(indexA).IsGreaterThanOrEqualTo(0);
            await Assert.That(indexB).IsGreaterThanOrEqualTo(0);
            await Assert.That(indexA).IsLessThan(indexB);
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task MultipleReceiveMiddlewares_AllInvoked()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<EchoActor>();
            cfg.AddMiddleware(new LoggingReceiveMiddleware());
            cfg.AddMiddleware(new OrderedReceiveMiddlewareA());
            cfg.Use<LoggingReceiveMiddleware>(1);
            cfg.Use<OrderedReceiveMiddlewareA>(2);
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act
            await actorRef.AskAsync<Pong>(new Ping("multi-middleware"));

            // Assert - both middlewares should have been invoked
            var entries = MiddlewareLog.GetEntries();
            await Assert.That(entries).Contains("ReceiveBefore:Ping");
            await Assert.That(entries).Contains("MiddlewareA");
        }
        finally
        {
            await system.StopAsync();
        }
    }
}

/// <summary>
/// Integration tests for the send pipeline middleware.
/// </summary>
public class SendPipelineTests
{
    [Test]
    public async Task SendMiddleware_IsInvoked_WhenMessageIsSent()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<EchoActor>();
            cfg.AddMiddleware(new LoggingSendMiddleware());
            cfg.Use<LoggingSendMiddleware>();
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act
            var response = await actorRef.AskAsync<Pong>(new Ping("send-test"));

            // Assert
            await Assert.That(response.Payload).IsEqualTo("send-test");

            var entries = MiddlewareLog.GetEntries();
            await Assert.That(entries).Contains("SendBefore:Ping");
            await Assert.That(entries).Contains("SendAfter:Ping");
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task SendMiddleware_HasAccessToTargetActorType()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<EchoActor>();
            cfg.AddMiddleware(new MetadataEnrichingSendMiddleware());
            cfg.Use<MetadataEnrichingSendMiddleware>();
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act
            await actorRef.AskAsync<Pong>(new Ping("enrich-test"));

            // Assert - middleware should have logged the actor type name
            var entries = MiddlewareLog.GetEntries();
            await Assert.That(entries).Contains("SendEnrich:EchoActor");
        }
        finally
        {
            await system.StopAsync();
        }
    }
}

/// <summary>
/// Integration tests for combined send and receive pipelines.
/// </summary>
public class CombinedPipelineTests
{
    [Test]
    public async Task BothPipelines_InvokedDuringAsk()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<EchoActor>();
            cfg.AddMiddleware(new LoggingReceiveMiddleware());
            cfg.AddMiddleware(new LoggingSendMiddleware());
            cfg.Use<LoggingReceiveMiddleware>();
            cfg.Use<LoggingSendMiddleware>();
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act
            var response = await actorRef.AskAsync<Pong>(new Ping("both-pipelines"));

            // Assert
            await Assert.That(response.Payload).IsEqualTo("both-pipelines");

            var entries = MiddlewareLog.GetEntries();
            // Send pipeline should have been invoked
            await Assert.That(entries).Contains("SendBefore:Ping");
            await Assert.That(entries).Contains("SendAfter:Ping");
            // Receive pipeline should have been invoked
            await Assert.That(entries).Contains("ReceiveBefore:Ping");
            await Assert.That(entries).Contains("ReceiveAfter:Ping");
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task BothPipelines_InvokedDuringTell()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<CounterActor>();
            cfg.AddMiddleware(new LoggingReceiveMiddleware());
            cfg.AddMiddleware(new LoggingSendMiddleware());
            cfg.Use<LoggingReceiveMiddleware>();
            cfg.Use<LoggingSendMiddleware>();
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(CounterActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act
            await actorRef.TellAsync(new Increment());
            await Task.Delay(200);

            // Assert
            var entries = MiddlewareLog.GetEntries();
            await Assert.That(entries).Contains("SendBefore:Increment");
            await Assert.That(entries).Contains("SendAfter:Increment");
            await Assert.That(entries).Contains("ReceiveBefore:Increment");
            await Assert.That(entries).Contains("ReceiveAfter:Increment");
        }
        finally
        {
            await system.StopAsync();
        }
    }

    [Test]
    public async Task Pipelines_InvokedForEachMessage()
    {
        // Arrange
        MiddlewareLog.Clear();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrupe(cfg =>
        {
            cfg.AddActor<EchoActor>();
            cfg.AddMiddleware(new LoggingReceiveMiddleware());
            cfg.Use<LoggingReceiveMiddleware>();
            cfg.ConfigureRootSupervisor(opts =>
            {
                opts.Children.Add(new ChildSpecification(typeof(EchoActor)));
            });
        });

        var provider = services.BuildServiceProvider();
        var system = provider.GetRequiredService<ActorSystem>();
        await system.StartAsync();
        await Task.Delay(200);

        try
        {
            var supervisor = provider.GetRequiredService<IRootSupervisor>();
            var actorRef = supervisor.Children.First();

            // Act - send 3 messages
            for (int i = 0; i < 3; i++)
            {
                await actorRef.AskAsync<Pong>(new Ping($"msg-{i}"));
            }

            // Assert - middleware should have been invoked at least 3 times for Ping
            // (system messages like InitializeActor also go through the pipeline)
            var entries = MiddlewareLog.GetEntries();
            var receiveBeforeCount = entries.Count(e => e == "ReceiveBefore:Ping");
            var receiveAfterCount = entries.Count(e => e == "ReceiveAfter:Ping");
            await Assert.That(receiveBeforeCount).IsGreaterThanOrEqualTo(3);
            await Assert.That(receiveAfterCount).IsGreaterThanOrEqualTo(3);
        }
        finally
        {
            await system.StopAsync();
        }
    }
}
