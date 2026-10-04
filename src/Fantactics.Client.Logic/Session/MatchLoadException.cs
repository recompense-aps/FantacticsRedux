namespace Fantactics.Client.Logic.Session;

/// <summary>A match file couldn't be loaded; the message says why in words a player can read.</summary>
public sealed class MatchLoadException(string message, Exception? inner = null) : Exception(message, inner);
