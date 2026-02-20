using System;

namespace Trupe.Abstractions;

/// <summary>
/// Provides utility methods for generating unique identifiers (UUIDs).
/// </summary>
/// <remarks>
/// This class abstracts the UUID generation process, using the most appropriate
/// method based on the target framework:
/// <list type="bullet">
/// <item><description>For .NET 9.0 and later: Uses <see cref="Guid.CreateVersion7()"/> which generates time-based UUIDs (Version 7)</description></item>
/// <item><description>For earlier versions: Uses <see cref="Guid.NewGuid()"/> which generates random UUIDs (Version 4)</description></item>
/// </list>
/// </remarks>
public static class Uuid
{
    /// <summary>
    /// Creates a new globally unique identifier (GUID/UUID).
    /// </summary>
    /// <returns>A new <see cref="Guid"/> value.</returns>
    /// <remarks>
    /// The implementation varies based on the target framework:
    /// <list type="bullet">
    /// <item><description>.NET 9.0+: Returns a Version 7 UUID with timestamp-based ordering for better database indexing performance</description></item>
    /// <item><description>Earlier versions: Returns a Version 4 UUID using cryptographically strong random numbers</description></item>
    /// </list>
    /// </remarks>
    public static Guid NewUuid()
    {
#if NET9_0_OR_GREATER
        return Guid.CreateVersion7();
#else
        return Guid.NewGuid();
#endif
    }
}
