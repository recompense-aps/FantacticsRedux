namespace Fantactics.Core.Players;

/// <summary>
/// The seed for a bot's decision. Every host (the Sim CLI, local and shared-file play) creates a fresh agent per
/// decision with this seed, so a record plays out the same whichever process runs the bot.
/// </summary>
public static class BotSeeds
{
    /// <summary>The seed for the decision that will become command <paramref name="nextSeq"/>.</summary>
    /// <param name="matchSeed">The match seed.</param>
    /// <param name="nextSeq">Sequence number the bot's command will get.</param>
    /// <param name="seat">The bot's seat.</param>
    public static int For(ulong matchSeed, int nextSeq, Seat seat) =>
        unchecked((int)(matchSeed ^ ((ulong)nextSeq * 0x9E3779B97F4A7C15UL))) ^ (int)seat;
}
