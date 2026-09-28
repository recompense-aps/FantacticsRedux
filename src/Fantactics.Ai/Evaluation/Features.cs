namespace Fantactics.Ai.Evaluation;

/// <summary>
/// Raw evaluation features from one seat's point of view; higher is better for that seat in every field. The score
/// is their dot product with <see cref="Profiles.StyleWeights"/>, so tuning can work on the raw values.
/// </summary>
/// <param name="Material">Own army value minus the enemy's, with field units scaled by remaining HP.</param>
/// <param name="Score">Turn-limit score difference, scaled by how close the turn limit is.</param>
/// <param name="Objectives">Objective tiles held minus the enemy's (per unit: standing on one).</param>
/// <param name="Exposure">Minus the value the enemy could destroy next turn.</param>
/// <param name="Opportunity">Value units could destroy next turn from where they stand.</param>
/// <param name="Terrain">Terrain Defense under units.</param>
/// <param name="Advance">Minus the (scaled) distance to the enemy and the objectives.</param>
/// <param name="Cohesion">Friendly neighbors, up to the Support cap.</param>
/// <param name="Disable">Enemy units Rooted or Slowed, minus own.</param>
/// <param name="Hold">Standing still for the Held (2 for Braced units) initiative bonus.</param>
public readonly record struct Features(
    double Material,
    double Score,
    double Objectives,
    double Exposure,
    double Opportunity,
    double Terrain,
    double Advance,
    double Cohesion,
    double Disable,
    double Hold)
{
    /// <summary>The field-wise sum.</summary>
    public static Features operator +(Features a, Features b) => new(
        a.Material + b.Material,
        a.Score + b.Score,
        a.Objectives + b.Objectives,
        a.Exposure + b.Exposure,
        a.Opportunity + b.Opportunity,
        a.Terrain + b.Terrain,
        a.Advance + b.Advance,
        a.Cohesion + b.Cohesion,
        a.Disable + b.Disable,
        a.Hold + b.Hold);
}
