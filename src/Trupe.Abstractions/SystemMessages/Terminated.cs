namespace Trupe.Abstractions.SystemMessages;

public record Terminated(IActorReference Reference, TerminatedReason Reason);
