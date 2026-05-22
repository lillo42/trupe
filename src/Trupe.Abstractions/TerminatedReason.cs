namespace Trupe.Abstractions;

public record TerminatedReason(string? Reason = null)
{
    public static TerminatedReason Stopped { get; } = new("Stopped");
}
