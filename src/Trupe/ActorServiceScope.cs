using System;
using Microsoft.Extensions.DependencyInjection;

namespace Trupe;

/// <summary>
/// A no-op service scope wrapper that delegates to an existing <see cref="IServiceProvider"/> without creating a new scope.
/// Used when a message opts into sharing the actor's existing DI scope.
/// </summary>
/// <param name="serviceProvider">The service provider to expose through this scope.</param>
public class ActorServiceScope(IServiceProvider serviceProvider) : IServiceScope
{
    /// <summary>
    /// Gets the service provider associated with this scope.
    /// </summary>
    public IServiceProvider ServiceProvider => serviceProvider;

    /// <summary>
    /// No-op disposal; the underlying service provider lifetime is managed elsewhere.
    /// </summary>
    public void Dispose() { }
}
