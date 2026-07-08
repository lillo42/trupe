using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Trupe.Abstractions;
using Trupe.Abstractions.Pipelines;
using Trupe.Extensions;

namespace Trupe.IntegrationTests;

public class SendPipelineTest
{
    private IServiceProvider _serviceProvider = null!;
    private IRootSupervisor _rootSupervisor = null!;
    
    [Before(Test)]
    public async Task Before()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IActorProcessRegistry>(new ActorProcessRegistry());
        collection.AddSingleton<Interceptor>();
        collection
            .AddLogging()
            .AddTrupe(opt =>
            {
                opt.AddMiddleware<GlobalMiddleware>()
                    .AddMiddleware<PingMiddleware>()
                    .AddMiddleware<GlobalMiddlewareViaConfig>()
                    .AddMiddleware<GlobalMiddlewareWithoutAttribute>()
                    .AddMiddleware<PingMiddlewareWithAttribute>();

                opt.Use<GlobalMiddlewareViaConfig>();

                opt.AddActor<ActorWithMiddleware>(actor => actor
                    .Use<GlobalMiddlewareWithoutAttribute>()
                    .UseForMessage<PingMiddlewareWithAttribute, Ping>());
                
                opt.ConfigureRootSupervisor(root => root.AddActor<ActorWithMiddleware>());
            });
        
        _serviceProvider = collection.BuildServiceProvider();
        _rootSupervisor = _serviceProvider.GetRequiredService<IRootSupervisor>();

        var system = _serviceProvider.GetRequiredService<ActorSystem>();
        await system.StartAsync();

        await Task.Delay(1_000);
    }

    [After(Test)]
    public async Task After()
    {
        var system = _serviceProvider.GetRequiredService<ActorSystem>();
        await system.StopAsync();
    }
    
    [Test]
    [Timeout(60_000)]
    public async Task PipelineRunInvokeAllSent(CancellationToken cancellationToken)
    {
        await Assert.That(_rootSupervisor.Children).Count().IsEqualTo(1);
        
        var @ref = _rootSupervisor.Children.First();
        
        var pong = await @ref.AskAsync<Pong>(new Ping(nameof(PipelineRunInvokeAllSent)), new Dictionary<string, object?>
        {
            ["TestContext"] = nameof(PipelineRunInvokeAllSent)
        }, cancellationToken);
        
        await Assert.That(pong)
            .IsNotNull()
            .And.Member(x => x.Message, 
                y => y.IsEqualTo(nameof(PipelineRunInvokeAllSent)));
        
        var interceptor = _serviceProvider.GetRequiredService<Interceptor>();

        var values = interceptor.GetValues(nameof(PipelineRunInvokeAllSent));
        await Assert.That(values)
            .Count().IsEqualTo(5)
            .And.Contains(nameof(GlobalMiddlewareViaConfig))
            .And.Contains(nameof(GlobalMiddleware))
            .And.Contains(nameof(GlobalMiddlewareWithoutAttribute))
            .And.Contains(nameof(PingMiddleware))
            .And.Contains(nameof(PingMiddlewareWithAttribute));
    }
    
    public class Interceptor
    {
        private readonly ConcurrentDictionary<string, List<string>> _values = [];

        public void Record(string context ,string value)
        {
            var list = _values.GetOrAdd(context, _ => []);
            list.Add(value);
        }

        public List<string> GetValues(string context)
        {
            return _values[context];
        }
    }
    
    public record Ping(string Message);
    public record Pong(string Message);
    
    public class GlobalMiddlewareAttribute() : MiddlewareAttribute(0)
    {
        public override Type MiddlewareType => typeof(GlobalMiddleware);
    }
    
    public class GlobalMiddleware(Interceptor interceptor) : ISendMiddleware
    {
        public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            if (context.Message.Metadata.TryGetValue("TestContext", out var obj) && obj is string testContext)
            {
                interceptor.Record(testContext, nameof(GlobalMiddleware));
            }
            
            await next(context);
        }
    }
    
    public class GlobalMiddlewareViaConfig(Interceptor interceptor) : ISendMiddleware 
    {
        public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            if (context.Message.Metadata.TryGetValue("TestContext", out var obj) && obj is string testContext)
            {
                interceptor.Record(testContext, nameof(GlobalMiddlewareViaConfig));
            }
            
            await next(context);
        }
    }
    
    public class GlobalMiddlewareWithoutAttribute(Interceptor interceptor) : ISendMiddleware  
    {
        public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            if (context.Message.Metadata.TryGetValue("TestContext", out var obj) && obj is string testContext)
            {
                interceptor.Record(testContext, nameof(GlobalMiddlewareWithoutAttribute));
            }
            
            await next(context);
        }
    }
    
    public class PingMiddlewareAttribute() : MiddlewareAttribute(1)
    {
        public override Type MiddlewareType => typeof(PingMiddleware);
    }
    
    public class PingMiddleware(Interceptor interceptor) : ISendMiddleware 
    {
        public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            if (context.Message.Metadata.TryGetValue("TestContext", out var obj) && obj is string testContext)
            {
                interceptor.Record(testContext, nameof(PingMiddleware));
            }
            
            await next(context);
        }
    }
    
    public class PingMiddlewareWithAttribute(Interceptor interceptor) : ISendMiddleware 
    {
        public async ValueTask InvokeAsync(ISendPipelineContext context, NextSendDelegate next)
        {
            if (context.Message.Metadata.TryGetValue("TestContext", out var obj) && obj is string testContext)
            {
                interceptor.Record(testContext, nameof(PingMiddlewareWithAttribute));
            }
            
            await next(context);
        }
    }
    
    [GlobalMiddleware]
    public class ActorWithMiddleware : Actor, IHandleActorMessage<Ping>
    {
        [PingMiddleware]
        public ValueTask HandleAsync(Ping message, CancellationToken cancellationToken = default)
        {
            Context.Response = new Pong(message.Message);
            return new ValueTask();
        }
    }
}