#if NETSTANDARD2_0 || NETFRAMEWORK
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class,
    Inherited = false
)]
internal sealed class RequiresDynamicCodeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresDynamicCodeAttribute"/> class
    /// with the specified message.
    /// </summary>
    /// <param name="message">
    /// A message that contains information about the usage of dynamic code.
    /// </param>
    public RequiresDynamicCodeAttribute(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Indicates whether the attribute should apply to static members.
    /// </summary>
    public bool ExcludeStatics { get; set; }

    /// <summary>
    /// Gets a message that contains information about the usage of dynamic code.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets or sets an optional URL that contains more information about the method,
    /// why it requires dynamic code, and what options a consumer has to deal with it.
    /// </summary>
    public string? Url { get; set; }
}

#endif
