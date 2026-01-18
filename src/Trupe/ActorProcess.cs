using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Trupe;

internal class ActorProcess(IActor actor, IMailbox mailbox)
{
    private static readonly ConcurrentDictionary<
        Type,
        Func<IActor, IMessage, ValueTask>
    > _typedCallHandle = new();

    private Task? _executing;

    public void Start()
    {
        if (_executing != null)
        {
            return;
        }

        _executing = Task.Run(Run);
    }

    private async Task Run()
    {
        await foreach (var message in mailbox)
        {
            if (message.Value == null || !RuntimeFeature.IsDynamicCodeSupported)
            {
                await actor.Handle(message.Value, message.CancellationToken);
                continue;
            }

            var callHandle = _typedCallHandle.GetOrAdd(
                message.Value.GetType(),
                CreateCallHandleDelegate
            );

            await callHandle(actor, message);
        }
    }

    private static async ValueTask CallHandle<TMessage>(IActor actor, IMessage message)
    {
        if (actor is IHandleActorMessage<TMessage> handle)
        {
            await handle.Handle((TMessage)message.Value!, message.CancellationToken);
        }
        else
        {
            await actor.Handle(message.Value, message.CancellationToken);
        }
    }

    private static readonly MethodInfo s_callHandleMethodInfo = typeof(ActorProcess).GetMethod(
        nameof(CallHandle),
        BindingFlags.Static | BindingFlags.NonPublic
    )!;

    private static Func<IActor, IMessage, ValueTask> CreateCallHandleDelegate(Type messageType)
    {
        var typed = s_callHandleMethodInfo.MakeGenericMethod(messageType);
        return typed.CreateDelegate<Func<IActor, IMessage, ValueTask>>();
    }
}
