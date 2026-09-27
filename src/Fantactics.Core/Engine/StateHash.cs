using System.Security.Cryptography;
using Fantactics.Core.Serialization;
using Fantactics.Core.State;

namespace Fantactics.Core.Engine;

/// <summary>
/// A stable hash of a state's canonical JSON, used to verify replays and detect rules drift (Simulation §5).
/// Stable across processes because every collection in the state is sorted or ordered.
/// </summary>
public static class StateHash
{
    /// <summary>Lowercase hex SHA-256 of <paramref name="state"/>.</summary>
    public static string Compute(GameState state) =>
        Convert.ToHexString(SHA256.HashData(CoreJson.SerializeToUtf8Bytes(state))).ToLowerInvariant();
}
