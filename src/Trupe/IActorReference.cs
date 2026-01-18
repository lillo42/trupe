using System;
using System.Threading.Tasks;

namespace Trupe;

public interface IActorReference
{
    Uri Name { get; }

    void Start();
    void Stop();

    Task StartAsync();
    Task StopAsync();

    void Tell<TMessage>(TMessage message);

    void TellAsync<TMessage>(TMessage message);
}

