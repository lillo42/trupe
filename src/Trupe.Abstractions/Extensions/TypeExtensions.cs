using System;
using Trupe.Abstractions.Pipelines;

namespace Trupe.Abstractions.Extensions;

/// <summary>
/// Internal extension methods for <see cref="Type"/> to check actor and supervisor assignability.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Determines whether the specified type implements <see cref="IActor"/>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><c>true</c> if the type implements <see cref="IActor"/>; otherwise, <c>false</c>.</returns>
    public static bool IsActor(this Type type)
    {
        return typeof(IActor).IsAssignableFrom(type);
    }

    /// <summary>
    /// Determines whether the specified type implements <see cref="ISupervisor"/>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><c>true</c> if the type implements <see cref="ISupervisor"/>; otherwise, <c>false</c>.</returns>
    public static bool IsSupervisor(this Type type)
    {
        return typeof(ISupervisor).IsAssignableFrom(type);
    }

    /// <summary>
    /// Determines whether the specified type implements <see cref="IRootSupervisor"/>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><c>true</c> if the type implements <see cref="IRootSupervisor"/>; otherwise, <c>false</c>.</returns>
    public static bool IsRootSupervisor(this Type type)
    {
        return typeof(IRootSupervisor).IsAssignableFrom(type);
    }

    public static bool IReceiveMiddleware(this Type type)
    {
        return typeof(IReceiveMiddleware).IsAssignableFrom(type);
    }

    public static bool IsSendMiddleware(this Type type)
    {
        return typeof(ISendMiddleware).IsAssignableFrom(type);
    }
}
