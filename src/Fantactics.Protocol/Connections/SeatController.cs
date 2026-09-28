namespace Fantactics.Protocol.Connections;

/// <summary>
/// Who controls a seat. Stored in the record as the seat label (<see cref="Core.Records.MatchSetup.Seats"/>):
/// <c>human</c>, <c>llm</c>, or <c>bot:&lt;spec&gt;</c>, the same labels the Sim CLI uses.
/// </summary>
/// <param name="Kind">The kind of player.</param>
/// <param name="BotSpec">For bots, the bot spec (e.g. <c>captain@easy</c>); otherwise <c>null</c>.</param>
public sealed record SeatController(SeatControllerKind Kind, string? BotSpec)
{
    private const string BotPrefix = "bot:";

    /// <summary>A person at this machine.</summary>
    public static SeatController Human { get; } = new(SeatControllerKind.Human, null);

    /// <summary>An LLM on a shared match file.</summary>
    public static SeatController Llm { get; } = new(SeatControllerKind.Llm, null);

    /// <summary>The seat label for the record.</summary>
    public string Label => Kind switch
    {
        SeatControllerKind.Bot => BotPrefix + BotSpec,
        SeatControllerKind.Llm => "llm",
        _ => "human",
    };

    /// <summary>A bot.</summary>
    /// <param name="spec">The bot spec, e.g. <c>captain@easy</c>.</param>
    public static SeatController Bot(string spec) => new(SeatControllerKind.Bot, spec);

    /// <summary>Reads a seat label; anything that isn't <c>llm</c> or <c>bot:…</c> is a human.</summary>
    public static SeatController Parse(string label)
    {
        string value = label.Trim().ToLowerInvariant();
        return value switch
        {
            "llm" => Llm,
            _ when value.StartsWith(BotPrefix, StringComparison.Ordinal) => Bot(value[BotPrefix.Length..]),
            _ => Human,
        };
    }
}
