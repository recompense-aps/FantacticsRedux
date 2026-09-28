namespace Fantactics.Client.Logic.Playback;

/// <summary>A message across the board (a new turn, the end of the match).</summary>
/// <param name="Text">What it says.</param>
public sealed record BannerStep(string Text) : Step;
