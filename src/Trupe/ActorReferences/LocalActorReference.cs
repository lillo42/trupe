using System.Collections.Concurrent;
using System.Reflection;
using Sigil;
using Trupe.Messages;

namespace Trupe.ActorReferences;

public class LocalActorReference(Uri name, IActor actor, IMailbox mailbox) : IActorReference
{
    private static readonly ConcurrentDictionary<Type, Dictionary<Type, Func<IActor, object, CancellationToken, ValueTask>>> Actions = new();
    private readonly Dictionary<Type, Func<IActor, object, CancellationToken, ValueTask>> _actions = Actions.GetOrAdd(actor.GetType(), CreateActions);
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _consumer;

    public Uri Name { get; } = name;

    public void Tell<TMessage>(TMessage message)
    {
        mailbox.Enqueue(new LocalMessage(message));
    }

    public void TellAsync<TMessage>(TMessage message)
    {
        mailbox.Enqueue(new LocalMessage(message));
    }

    public void Start()
    {
        Stop();
        
        _cancellationTokenSource = new CancellationTokenSource();
        _consumer = ExecuteAsync(_cancellationTokenSource.Token);
        
        _consumer = Task.Factory.StartNew(async ct => await ExecuteAsync((CancellationToken)ct), 
            _cancellationTokenSource.Token, 
            _cancellationTokenSource.Token, 
            TaskCreationOptions.LongRunning, 
            TaskScheduler.Default);
    }

    public void Stop()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }


        if (_consumer != null)
        {
            try
            {
                _consumer.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                
            }

            _consumer = null;
        }
    }

    public async Task StartAsync()
    {
        await StopAsync();
        
        _cancellationTokenSource = new CancellationTokenSource();
        _consumer = ExecuteAsync(_cancellationTokenSource.Token);
        
        _consumer = Task.Factory.StartNew(async ct => await ExecuteAsync((CancellationToken)ct), 
            _cancellationTokenSource.Token,
            _cancellationTokenSource.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }
    
    public async Task StopAsync()
    {
        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }


        if (_consumer != null)
        {
            try
            {
                await _consumer;
            }
            catch (OperationCanceledException)
            {
                
            }

            _consumer = null;
        }
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = await mailbox.DequeueAsync(cancellationToken);
                if (message.Value != null && _actions.TryGetValue(message.Value.GetType(), out var action))
                {
                    await action(actor, message.Value, message.CancellationToken);
                }
                else
                {
                    await actor.ReceiveAsync(message.Value, message.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static Dictionary<Type, Func<IActor, object, CancellationToken, ValueTask>> CreateActions(Type actorType)
    {
        var types = actorType.GetInterfaces();
        var actions = new Dictionary<Type, Func<IActor, object, CancellationToken, ValueTask>>();
        foreach (var type in types)
        {
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IActor<>))
            {
                continue;
            }

            var method = type.GetMethods()[0];
            var exe = Emit<Func<IActor, object, CancellationToken, ValueTask>>
                .NewDynamicMethod($"Receive{Guid.NewGuid():N}")
                .LoadArgument(0)
                .CastClass(type)
                .LoadArgument(1)
                .CastClass(type.GenericTypeArguments[0])
                .LoadArgument(2)
                .CallVirtual(method)
                .Return();
                
            actions[type.GenericTypeArguments[0]] = exe.CreateDelegate();
        }

        return actions;
    }
}