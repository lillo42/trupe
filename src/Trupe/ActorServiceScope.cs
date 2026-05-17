using System;
using Microsoft.Extensions.DependencyInjection;

namespace Trupe;

public class ActorServiceScope(IServiceProvider serviceProvider) : IServiceScope
{
    public IServiceProvider ServiceProvider => serviceProvider;

    public void Dispose() { }
}
