namespace Fantactics.Ai.Profiles;

/// <summary>
/// How much work a bot may do per decision. Simulations (engine <c>Apply</c> calls used for lookahead) keep bots
/// deterministic; the optional wall-clock cap is only for interactive play, where reproducibility doesn't matter.
/// </summary>
public sealed record ThinkBudget
{
    /// <summary>Engine simulations allowed per decision.</summary>
    public int MaxSimulations { get; init; } = 2_000;

    /// <summary>Optional wall-clock cap per decision; <c>null</c> keeps the bot deterministic.</summary>
    public int? MaxMilliseconds { get; init; }
}
