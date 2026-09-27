using System.Diagnostics.CodeAnalysis;

namespace Fantactics.Core.Engine;

/// <summary>Thrown by rule validation; <see cref="GameEngine.Apply"/> turns it into a <see cref="Rejected"/> result.</summary>
/// <param name="code">Stable violation code.</param>
/// <param name="message">Readable explanation.</param>
internal sealed class RuleViolationException(string code, string message) : Exception(message)
{
    /// <summary>The violation to report.</summary>
    public RuleViolation Violation { get; } = new(code, message);

    /// <summary>Throws a violation unless <paramref name="condition"/> holds.</summary>
    public static void ThrowUnless([DoesNotReturnIf(false)] bool condition, string code, string message)
    {
        if (!condition)
        {
            throw new RuleViolationException(code, message);
        }
    }
}
