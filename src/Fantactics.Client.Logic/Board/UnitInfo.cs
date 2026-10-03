using System.Collections.Immutable;
using Fantactics.Core;
using Fantactics.Core.Engine;
using Fantactics.Core.Rules;
using Fantactics.Core.State;

namespace Fantactics.Client.Logic.Board;

/// <summary>What the unit info panel shows for one unit.</summary>
/// <param name="UnitId">The unit, in the viewing seat's ids.</param>
/// <param name="Owner">Its owner.</param>
/// <param name="Name">Its type in words, e.g. <c>Wolf Rider</c>.</param>
/// <param name="Tags">Owner, relation to the viewer, races, classes, and uniqueness, e.g. <c>P2 · enemy · Elves · Ranged</c>.</param>
/// <param name="Hp">Current HP.</param>
/// <param name="MaxHp">Full HP.</param>
/// <param name="Stats">Stats besides HP (see <see cref="UnitText.CombatStats"/>).</param>
/// <param name="Statuses">Active statuses, with the last turn each lasts through.</param>
/// <param name="Abilities">Abilities, with targeting, cooldown, and when each is ready.</param>
/// <param name="Traits">Traits of the unit and its races.</param>
/// <param name="Notes">Anything else that matters this turn (summoned, can't act, held, in reserve), or empty.</param>
public sealed record UnitInfo(
    int UnitId,
    Seat Owner,
    string Name,
    string Tags,
    int Hp,
    int MaxHp,
    string Stats,
    ImmutableArray<InfoLine> Statuses,
    ImmutableArray<InfoLine> Abilities,
    ImmutableArray<InfoLine> Traits,
    string Notes)
{
    /// <summary>Describes a unit the seat can see.</summary>
    public static UnitInfo Of(PlayerView view, RulesConfig rules, Unit unit) => Of(
        unit,
        rules,
        view.Turn,
        view.TurnState,
        unit.Owner == view.Seat ? "yours" : view.AreEnemies(unit.Owner, view.Seat) ? "enemy" : "ally");

    /// <summary>Describes a unit.</summary>
    /// <param name="unit">The unit.</param>
    /// <param name="rules">Rules, for its stats, abilities, and traits.</param>
    /// <param name="turn">The current turn, for cooldowns and statuses.</param>
    /// <param name="turnState">This turn's bookkeeping, for Held and Braced.</param>
    /// <param name="relation">Who the unit is to the viewer: <c>yours</c>, <c>ally</c>, or <c>enemy</c>.</param>
    public static UnitInfo Of(Unit unit, RulesConfig rules, int turn, TurnState turnState, string relation)
    {
        UnitDefinition type = rules.Units[unit.Type];
        ImmutableSortedSet<string> extraRaces = unit.ExtraRaces ?? [];
        string tags = string.Join(" · ", [
            unit.Owner.ToString(),
            relation,
            .. extraRaces.Prepend(type.Race).Distinct(),
            .. type.Classes ?? [],
            .. type.Unique ? ["unique"] : Array.Empty<string>(),
        ]);

        ImmutableArray<InfoLine> statuses = [.. unit.Statuses.Select(status => new InfoLine(
            $"{status.Key} · until end of turn {status.Value}",
            RulesText.Status(status.Key)))];
        ImmutableArray<InfoLine> abilities = [.. type.Abilities.Select(id => AbilityLine(id, rules, unit, turn))];
        ImmutableArray<InfoLine> traits = [.. rules.TraitsOf(unit.Type, extraRaces)
            .Select(trait => new InfoLine(RulesText.TraitTitle(trait.Key, trait.Value), RulesText.Trait(trait.Key, trait.Value)))];

        string notes = string.Join(" · ", new[]
            {
                unit.Location switch
                {
                    UnitLocation.Reserve => $"In reserve (Cost {type.Cost})",
                    UnitLocation.Unplaced => "Not placed yet",
                    _ => "",
                },
                unit.IsSummoned ? "Summoned: worth no points" : "",
                unit.IsOnField && unit.CannotActOnTurn == turn ? "Can't act this turn" : "",
                turnState.Held.Contains(unit.Id) ? "Held this turn" : "",
                turnState.Braced.Contains(unit.Id) ? "Braced this turn" : "",
            }
            .Where(note => note.Length > 0));

        return new UnitInfo(
            unit.Id,
            unit.Owner,
            UnitText.Words(unit.Type),
            tags,
            unit.Hp,
            type.Hp,
            UnitText.CombatStats(type),
            statuses,
            abilities,
            traits,
            notes);
    }

    private static InfoLine AbilityLine(string id, RulesConfig rules, Unit unit, int turn)
    {
        if (!rules.Abilities.TryGetValue(id, out AbilityDefinition? ability))
        {
            return new InfoLine(UnitText.Words(id), "");
        }

        int readyTurn = unit.AbilityReadyTurn.GetValueOrDefault(id);
        string title = string.Join(" · ", new[]
            {
                UnitText.Words(id),
                RulesText.Range(ability),
                ability.Cooldown > 0 ? $"Cooldown {ability.Cooldown}" : "no cooldown",
                readyTurn > turn ? $"ready turn {readyTurn}" : "ready",
            }
            .Where(part => part.Length > 0));
        return new InfoLine(title, RulesText.Ability(id, ability));
    }
}
