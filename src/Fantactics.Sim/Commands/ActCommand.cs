using System.Collections.Immutable;
using System.Text.Json;
using Fantactics.Core.Commands;
using Fantactics.Core.Engine;
using Fantactics.Core.Serialization;
using Fantactics.Sim.Matches;
using Fantactics.Sim.Orders;
using Fantactics.Sim.Output;
using Fantactics.Sim.Views;
using McMaster.Extensions.CommandLineUtils;

namespace Fantactics.Sim.Commands;

/// <summary>
/// Submits a seat's decision. Bot seats then play until a non-bot seat owes a decision, and the output ends with
/// this seat's next decision and its options, so a playing seat needs one call per decision.
/// </summary>
/// <param name="store">Match file store.</param>
/// <param name="output">Where results are printed.</param>
[Command("act", Description = "Submit a seat's decision.")]
public sealed class ActCommand(MatchStore store, OutputWriter output) : SeatCommand(output)
{
    /// <summary>Numbered action option.</summary>
    [Option("--pick", Description = "Action phase: the number of an option from 'legal'.")]
    public int? Pick { get; set; }

    /// <summary>Move, deploy, or placement orders in the compact grammar.</summary>
    [Option("--orders", Description = "Moves (A>5,3 A>3,1>5,3 A=hold D@1,7) or placement (A@0,5 B@1,6).")]
    public string? Orders { get; set; }

    /// <summary>Draft in the compact grammar.</summary>
    [Option("--draft", Description = "Draft: starting unit types, then | and reserve types.")]
    public string? Draft { get; set; }

    /// <summary>A canonical serialized Core command.</summary>
    [Option("--json", Description = "A serialized command, with \"$type\" as its first property.")]
    public string? Json { get; set; }

    /// <summary>Reasoning to store in the record for post-game review.</summary>
    [Option("--note", Description = "Your reasoning; stored in the record, never shown to the opponent.")]
    public string? Note { get; set; }

    /// <inheritdoc />
    protected override int Execute()
    {
        int given = new object?[] { Pick, Orders, Draft, Json }.Count(value => value is not null);
        if (given != 1)
        {
            throw new SimException("Give exactly one of --pick, --orders, --draft, or --json.");
        }

        using IDisposable fileLock = store.Lock(MatchFile);
        MatchSession session = store.Load(MatchFile);
        Decision? decision = GameEngine.PendingDecisionFor(session.State, As);
        if (decision is null)
        {
            string waiting = string.Join(", ", ViewBuilder.WaitingFor(PlayerView.Project(session.State, As)));
            return Fail(
                "not-your-decision",
                session.State.Outcome is null
                    ? $"{As} doesn't owe a decision right now; waiting for {waiting}."
                    : $"The match is over: {ViewBuilder.Outcome(session.State.Outcome)}.",
                ExitCodes.NotYourDecision);
        }

        ICommand command = BuildCommand(session, decision);
        int before = session.CommandCount;
        if (session.Submit(As, command, Note) is Rejected rejected)
        {
            return Fail(rejected.Violation.Code, rejected.Violation.Message, ExitCodes.RuleViolation);
        }

        session.AdvanceBots();
        store.Save(MatchFile, session);
        Output.Write(Result(session, before), Format);
        return ExitCodes.Ok;
    }

    private ICommand BuildCommand(MatchSession session, Decision decision)
    {
        UnitHandles handles = session.HandlesFor(As);
        return (decision, Pick, Orders, Draft, Json) switch
        {
            (_, _, _, _, string json) => ParseJson(json),
            (ChooseUnitActionDecision, int pick, _, _, _) => PickOption(session, pick),
            (SubmitMoveOrdersDecision, _, string orders, _, _) =>
                OrderParser.ParseMoveOrders(orders, session.State, As, handles),
            (PlaceStartingArmyDecision, _, string orders, _, _) =>
                OrderParser.ParsePlacement(orders, session.State, As, handles),
            (DraftArmyDecision, _, _, string draft, _) => OrderParser.ParseDraft(draft),
            _ => throw new SimException(
                $"That input doesn't fit the pending {ViewBuilder.KindOf(decision)} decision. "
                + "Use --draft for Draft, --orders for Placement and Moves, --pick for Action.",
                ExitCodes.RuleViolation),
        };
    }

    private ICommand PickOption(MatchSession session, int pick)
    {
        ImmutableArray<ActionOption> options = LegalActions.For(session.State, As)?.Actions ?? [];
        return pick >= 1 && pick <= options.Length
            ? options[pick - 1].Command
            : throw new SimException($"--pick must be between 1 and {options.Length}.", ExitCodes.RuleViolation);
    }

    private static ICommand ParseJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ICommand>(json, CoreJson.Options)
                ?? throw new SimException("--json is empty.", ExitCodes.RuleViolation);
        }
        catch (JsonException ex)
        {
            throw new SimException($"--json isn't a valid command: {ex.Message}", ExitCodes.RuleViolation);
        }
    }

    private ActResult Result(MatchSession session, int before)
    {
        UnitHandles handles = session.HandlesFor(As);
        ImmutableArray<EventLine> events = EventFormatter.Format(session.Events.Where(e => e.Seq > before), handles);
        Decision? next = GameEngine.PendingDecisionFor(session.State, As);
        SeatView? view = next is null
            ? null
            : ViewBuilder.Build(session, As, includeMap: next is not ChooseUnitActionDecision) with { Recent = [] };
        return new ActResult(
            "ok",
            events,
            view,
            next is null ? null : LegalBuilder.Build(session, As),
            ViewBuilder.WaitingFor(PlayerView.Project(session.State, As)),
            ViewBuilder.Outcome(session.State.Outcome));
    }
}
