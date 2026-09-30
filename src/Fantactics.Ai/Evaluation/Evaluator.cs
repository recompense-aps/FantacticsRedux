using Fantactics.Ai.Profiles;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Events;
using Fantactics.Core.Geometry;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Ai.Evaluation;

/// <summary>
/// Scores game states (and single unit placements) for one seat, weighted by a style. The same per-unit terms
/// score whole states and candidate move tiles, so every planner agrees on what a good position is.
/// </summary>
/// <param name="seat">The seat whose point of view is scored.</param>
/// <param name="style">Feature weights.</param>
/// <param name="skill">Blind spots.</param>
/// <param name="enemyThreats">
/// Where enemy units can strike next turn, computed for the decision's starting state.
/// </param>
public sealed class Evaluator(Seat seat, StyleWeights style, SkillSettings skill, ThreatMap enemyThreats)
{
    /// <summary>Score of a won match; a lost one scores the negative.</summary>
    public const double WinScore = 10_000;

    private const double KillBonus = 0.5;
    private const double AdvanceScale = 10;
    private const double RetreatExposureFactor = 3;
    private const int DefaultObjectivePoints = 2;
    private const double NotYetHeld = 0.5;

    /// <summary>The seat whose point of view is scored.</summary>
    public Seat Seat => seat;

    /// <summary>The weights this evaluator scores with.</summary>
    public StyleWeights Style => style;

    /// <summary>The same evaluator with different weights, e.g. for a stance; the threat map is shared.</summary>
    public Evaluator WithStyle(StyleWeights weights) => new(seat, weights, skill, enemyThreats);

    /// <summary>
    /// Scores <paramref name="state"/>; finished matches score ±<see cref="WinScore"/>, or 0 for a draw.
    /// </summary>
    public double Evaluate(GameState state) => state.Outcome is MatchOutcome outcome
        ? outcome.IsDraw ? 0 : outcome.Won(seat) ? WinScore : -WinScore
        : Score(Extract(state));

    /// <summary>The weighted sum of <paramref name="features"/>.</summary>
    public double Score(Features features) =>
        style.Material * features.Material
        + style.Score * features.Score
        + style.Objectives * features.Objectives
        + style.Exposure * features.Exposure
        + style.Opportunity * features.Opportunity
        + style.Terrain * features.Terrain
        + style.Advance * features.Advance
        + style.Cohesion * features.Cohesion
        + style.Disable * features.Disable
        + style.Hold * features.Hold;

    /// <summary>Raw features of a whole state.</summary>
    public Features Extract(GameState state)
    {
        List<Seat> enemies = [.. state.Opponents(seat)];
        List<Unit> mine = state.FieldUnits.Where(unit => unit.Owner == seat).ToList();
        List<Point> friends = mine.Select(unit => unit.Position).ToList();
        Features positional = mine
            .Select(unit => UnitFeatures(state, unit, unit.Position, friends) with { Objectives = 0, Hold = 0 })
            .Aggregate(default(Features), (sum, next) => sum + next);

        PlayerState me = state.Players[seat];
        double progress = (double)state.Turn / state.Rules.TurnLimit;
        return positional with
        {
            Material = Material(state, seat) - enemies.Sum(enemy => Material(state, enemy)),
            Score = (me.Score - enemies.Select(enemy => state.Players[enemy].Score).DefaultIfEmpty(0).Max()) * progress,
            Objectives = (ObjectivesHeld(state, seat)
                    - enemies.Select(enemy => ObjectivesHeld(state, enemy)).DefaultIfEmpty(0).Max())
                * ObjectiveStakes(state),
            Disable = enemies.Sum(enemy => Disabled(state, enemy)) - Disabled(state, seat),
        };
    }

    /// <summary>Scores <paramref name="unit"/> standing on <paramref name="tile"/> (per-unit features only).</summary>
    /// <param name="state">The state to judge against (enemy positions, terrain).</param>
    /// <param name="unit">One of the seat's units.</param>
    /// <param name="tile">Where it would stand.</param>
    /// <param name="friends">Where the seat's other units will be.</param>
    public double ScoreUnitAt(GameState state, Unit unit, Point tile, IReadOnlyCollection<Point> friends) =>
        Score(UnitFeatures(state, unit, tile, friends));

    /// <summary>A unit's worth: its Cost, halved for summoned units (they don't count toward Rout).</summary>
    public static double ValueOf(GameState state, Unit unit) =>
        state.DefinitionOf(unit).Cost * (unit.IsSummoned ? 0.5 : 1.0);

    /// <summary>
    /// Damage <paramref name="attacker"/> would deal to <paramref name="target"/> standing on <paramref name="tile"/>,
    /// attacking from its preferred distance (adjacent for melee, beyond point blank for ranged units).
    /// </summary>
    public static int EstimatedDamage(GameState state, Unit attacker, Unit target, Point tile)
    {
        UnitDefinition definition = state.DefinitionOf(attacker);
        int distance = definition.IsRanged ? Math.Max(2, definition.MinRange) : 1;
        Unit placedAttacker = attacker with { Position = new Point(tile.X + distance, tile.Y) };
        return CombatRules.Damage(state, placedAttacker, target with { Position = tile }, AttackKind.Basic);
    }

    private Features UnitFeatures(GameState state, Unit unit, Point tile, IReadOnlyCollection<Point> friends)
    {
        List<Unit> enemies = state.FieldUnits
            .Where(other => state.AreEnemies(other.Owner, seat) && Sees(unit, other))
            .ToList();
        int cohesion = friends.Count(friend => friend.IsAdjacentTo(tile));
        bool retreating = unit.Hp < style.RetreatThreshold * state.DefinitionOf(unit).Hp;
        double advance = -AdvanceDistance(state, unit, tile, enemies) * AdvanceScale
            / (state.Map.Width + state.Map.Height);
        return new Features(
            Material: 0,
            Score: 0,
            Objectives: state.Map.Objectives.Contains(tile)
                ? ObjectiveStakes(state) * (state.Rules.ObjectivesNeedHold && tile != unit.Position ? NotYetHeld : 1)
                : 0,
            Exposure: -Exposure(state, unit, tile, enemies) * (retreating ? RetreatExposureFactor : 1),
            Opportunity: Opportunity(state, unit, tile, enemies),
            Terrain: UnitRules.TerrainDefense(state, tile),
            Advance: retreating ? 0 : advance,
            Cohesion: Math.Min(cohesion, state.Rules.SupportCap),
            Disable: 0,
            Hold: tile == unit.Position ? (UnitRules.HasTrait(state, unit, TraitIds.Braced) ? 2 : 1) : 0);
    }

    /// <summary>What an enemy unit is worth to this bot: its value times the style's priority for its type.</summary>
    private double EnemyValue(GameState state, Unit enemy) =>
        ValueOf(state, enemy) * style.Priorities.GetValueOrDefault(enemy.Type, 1.0);

    private bool Sees(Unit unit, Unit enemy) =>
        skill.VisionRange is not int range || unit.Position.DistanceTo(enemy.Position) <= range;

    private double Exposure(GameState state, Unit unit, Point tile, List<Unit> enemies)
    {
        if (skill.IgnoreThreats)
        {
            return 0;
        }

        int damage = enemies
            .Where(enemy => enemyThreats.Threatens(enemy.Id, tile))
            .Sum(enemy => EstimatedDamage(state, enemy, unit, tile));
        return Math.Min(1.0, (double)damage / unit.Hp) * ValueOf(state, unit);
    }

    private double Opportunity(GameState state, Unit unit, Point tile, List<Unit> enemies)
    {
        UnitDefinition definition = state.DefinitionOf(unit);
        Unit placed = unit with { Position = tile };
        return enemies
            .Select(enemy => (Enemy: enemy, Distance: tile.DistanceTo(enemy.Position)))
            .Where(pair => pair.Distance >= definition.MinRange
                && pair.Distance <= definition.MaxRange
                && (pair.Distance == 1 || UnitRules.HasLineOfSight(state, placed, pair.Enemy.Position)))
            .Select(pair => StrikeValue(state, placed, pair.Enemy, pair.Distance))
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// What a strike on <paramref name="target"/> would be worth against a fresh copy of it. Using full HP keeps this
    /// a measure of position: judged at current HP, leaving an enemy wounded would score better than killing another.
    /// </summary>
    private double StrikeValue(GameState state, Unit attacker, Unit target, int distance)
    {
        bool pointBlank = state.DefinitionOf(attacker).IsRanged && distance == 1;
        AttackKind kind = pointBlank ? AttackKind.PointBlank : AttackKind.Basic;
        int damage = CombatRules.Damage(state, attacker, target, kind);
        int maxHp = state.DefinitionOf(target).Hp;
        double value = EnemyValue(state, target);
        double kill = damage >= maxHp ? KillBonus * value : 0;
        return Math.Min(1.0, (double)damage / maxHp) * value + kill;
    }

    /// <summary>
    /// Tiles to the nearest goal: an enemy (from attack range for ranged units) or, for styles that care about
    /// them, an objective.
    /// </summary>
    private int AdvanceDistance(GameState state, Unit unit, Point tile, List<Unit> enemies)
    {
        int preferredRange = state.DefinitionOf(unit).MaxRange;
        IEnumerable<int> toEnemies = enemies
            .Select(enemy => Math.Max(0, tile.DistanceTo(enemy.Position) - preferredRange));
        IEnumerable<int> toObjectives = style.Objectives > 0
            ? state.Map.Objectives.Select(objective => objective.DistanceTo(tile))
            : [];
        return toEnemies
            .Concat(toObjectives)
            .DefaultIfEmpty(0)
            .Min();
    }

    private double Material(GameState state, Seat owner) =>
        state.Units.Values
            .Where(unit => unit.Owner == owner)
            .Sum(unit =>
            {
                double value = owner == seat ? ValueOf(state, unit) : EnemyValue(state, unit);
                return unit.IsOnField ? value * (0.5 + 0.5 * unit.Hp / state.DefinitionOf(unit).Hp) : value;
            });

    /// <summary>
    /// Points one objective tile is worth per turn under these rules, relative to the MVP (2 points for the majority,
    /// so about 1 per tile; 0 when the rules don't score objectives), so style weights carry over to rules variants.
    /// </summary>
    private static double ObjectiveStakes(GameState state) => state.Rules.ObjectiveScoring == ObjectiveScoring.PerTile
        ? state.Rules.ObjectivePointsPerTurn
        : state.Rules.ObjectivePointsPerTurn / (double)DefaultObjectivePoints;

    /// <summary>
    /// Objective tiles <paramref name="owner"/> holds. With entrenched objectives, a unit that moved onto its tile
    /// this turn counts only <see cref="NotYetHeld"/>: it scores from next turn, if it stays.
    /// </summary>
    private static double ObjectivesHeld(GameState state, Seat owner) =>
        state.Map.Objectives
            .Select(tile => state.UnitAt(tile))
            .OfType<Unit>()
            .Where(unit => unit.Owner == owner)
            .Sum(unit => state.Rules.ObjectivesNeedHold
                && state.Phase == Phase.Action
                && !state.TurnState.Held.Contains(unit.Id)
                    ? NotYetHeld
                    : 1);

    private static double Disabled(GameState state, Seat owner) =>
        state.FieldUnits
            .Where(unit => unit.Owner == owner)
            .Sum(unit => (unit.Has(StatusKind.Rooted) ? 1.0 : 0) + (unit.Has(StatusKind.Slowed) ? 0.5 : 0));
}
