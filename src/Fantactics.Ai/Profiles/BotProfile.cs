namespace Fantactics.Ai.Profiles;

/// <summary>A named bot: a difficulty (how well it plays) mixed with a style (how it likes to play).</summary>
/// <param name="Name">Profile name, e.g. <c>captain</c>.</param>
/// <param name="Description">One line for listings.</param>
/// <param name="Difficulty">Difficulty preset the skill settings came from.</param>
/// <param name="Skill">Search budget and mistakes.</param>
/// <param name="Style">Evaluation weights and leanings.</param>
public sealed record BotProfile(
    string Name,
    string Description,
    string Difficulty,
    SkillSettings Skill,
    StyleWeights Style);
