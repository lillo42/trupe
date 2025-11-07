namespace Trupe;

public interface IMessage
{
    object? Value { get; }
    
    CancellationToken CancellationToken { get; }
}