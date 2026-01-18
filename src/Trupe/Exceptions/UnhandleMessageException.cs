namespace Trupe.Exceptions;

public class UnhandleMessageException(object? value, IActor actor)
    : TrupeException($"Actor not able to handle '{value?.GetType().FullName ?? "null"}'")
{
    public IActor Actor { get; } = actor;

    public object? Value { get; } = value;
}

public class UnhandleMessageException<T>(T value, IActor actor)
    : TrupeException($"Actor not able to handle '{typeof(T).FullName}'")
{
    public IActor Actor { get; } = actor;

    public T Value { get; } = value;
}
