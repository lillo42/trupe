using System.Threading;

namespace Trupe.Messages;

public record LocalMessage(object? Value, CancellationToken CancellationToken = default) : IMessage;

