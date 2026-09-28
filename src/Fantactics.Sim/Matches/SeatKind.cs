namespace Fantactics.Sim.Matches;

/// <summary>Who plays a seat: <c>llm</c>, <c>human</c>, or <c>bot:&lt;name&gt;</c>.</summary>
/// <param name="Value">The kind as written on the command line and in the match record.</param>
public sealed record SeatKind(string Value)
{
    private const string BotPrefix = "bot:";

    /// <summary>Whether the CLI plays this seat itself (auto-advance).</summary>
    public bool IsBot => Value.StartsWith(BotPrefix, StringComparison.Ordinal);

    /// <summary>The bot name, e.g. <c>random</c>, or <c>null</c> for non-bot seats.</summary>
    public string? BotName => IsBot ? Value[BotPrefix.Length..] : null;

    /// <summary>Parses and validates a seat kind.</summary>
    /// <exception cref="SimException">The kind is unknown.</exception>
    public static SeatKind Parse(string value)
    {
        SeatKind kind = new(value.Trim().ToLowerInvariant());
        bool known = kind.Value is "llm" or "human" || (kind.BotName is string bot && BotFactory.IsKnown(bot));
        return known
            ? kind
            : throw new SimException(
                $"Unknown seat kind '{value}'. Use llm, human, or {BotFactory.Usage}.");
    }
}
