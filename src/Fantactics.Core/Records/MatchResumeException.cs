namespace Fantactics.Core.Records;

/// <summary>A match record can't be resumed: its history no longer replays and it has no snapshot to fall back on.</summary>
/// <param name="message">What went wrong.</param>
public sealed class MatchResumeException(string message) : Exception(message);
