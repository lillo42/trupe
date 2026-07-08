using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trupe.Abstractions;
using Trupe.Abstractions.Options;
using Trupe.Abstractions.Supervisors;
using Trupe.Supervisors;

namespace Trupe;

/// <summary>
/// The default root supervisor that initializes child actors from <see cref="RootSupervisorOptions"/>
/// and restarts them on failure.
/// </summary>
/// <param name="options">The options containing the child actor specifications.</param>
/// <param name="logger">The logger instance.</param>
public class RootSupervisor(IOptions<RootSupervisorOptions> options, ILogger<RootSupervisor> logger)
    : Supervisor(logger),
        IRootSupervisor
{
    /// <inheritdoc />
    protected override ValueTask OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var child in options.Value.Children)
        {
            AddChild(child);
        }

        return new ValueTask();
    }

    /// <inheritdoc />
    protected override FailureAction ResolveFailureAction(Child child, Exception exception)
    {
        return FailureAction.Restart;
    }
}
