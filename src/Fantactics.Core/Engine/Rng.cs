namespace Fantactics.Core.Engine;

/// <summary>SplitMix64: a small deterministic RNG whose whole state is one <see cref="ulong"/> kept in the game state.</summary>
public static class Rng
{
    /// <summary>Draws a value in <c>[0, maxExclusive)</c>.</summary>
    /// <param name="state">Current RNG state.</param>
    /// <param name="maxExclusive">Upper bound; must be positive.</param>
    /// <returns>The value and the next RNG state.</returns>
    public static (int Value, ulong NextState) Next(ulong state, int maxExclusive)
    {
        ulong next = state + 0x9E3779B97F4A7C15UL;
        ulong z = next;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return ((int)(z % (ulong)maxExclusive), next);
    }
}
