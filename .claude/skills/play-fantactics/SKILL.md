---
name: play-fantactics
description: Play a seat in a Fantactics match through the fantactics-sim CLI (as P1 or P2, against a bot, a human, or another LLM). Use when asked to play, playtest, or self-play Fantactics, or to referee an LLM-vs-LLM match.
---

# Playing Fantactics with fantactics-sim

You are a player in a turn-based tactics match. The match lives in a JSON file; you only ever touch it through the
CLI. Design and rules: `notes/design/Simulation.md` (§6), `notes/design/GameDesign.md` (§4–5),
`notes/design/RacesAndUnits.md`.

## Rules of conduct

- **Never read, open, grep, or edit the match file** (or its `.lock`/`.tmp` siblings). It contains the opponent's
  hidden orders. Use only `status`, `view`, `legal`, `act`, and `log`. `replay` is for after the match ends.
- Play to win, within the rules. Don't probe the engine for exploits by submitting junk.
- Give every decision a short `--note` with your intent. Notes are private until the match ends.
- If a rule seems broken, confusing, or degenerate, say so in a note starting with `RULES?`, e.g.
  `--note "RULES? Scouts can body-block bridges forever"`.

## Setup

Build once, then call the built CLI (faster than `dotnet run`):

```sh
dotnet build src/Fantactics.Sim
SIM="dotnet src/Fantactics.Sim/bin/Debug/net8.0/fantactics-sim.dll"
```

Create a match if you weren't given one (defaults: P1 llm Elves, P2 bot:random Goblins, map riverford):

```sh
$SIM new --out playtests/match-1.json --p1 llm --p2 bot:random --seed 7
```

## The loop

Always pass `--format toon` (compact tables; about half the tokens of JSON).

1. `$SIM view <match> --as <seat> --format toon` once, to get oriented: map rows, your units, the enemy's units,
   your reserve, and what you owe.
2. `$SIM legal <match> --as <seat> --format toon` for the options of the pending decision.
3. `$SIM act <match> --as <seat> ... --note "..." --format toon`. The output lists what happened (including bot moves
   that followed), then your next view and options. Keep calling `act` from that output. Go back to `view`/`legal`
   only when you need the full map again.
4. Exit code 3 means it's not your decision: run `status` and wait (in self-play, the referee tells you when to act).
   Exit code 2 means your input broke a rule or the grammar; read the message and try again.

## Input forms

| Decision | Form | Example |
|---|---|---|
| Draft | `--draft "starting types \| reserve types"` | `--draft "Druid Archer Archer Ranger Herbalist Scout \| Ranger Herbalist"` |
| Placement | `--orders` with `@` for every listed unit | `--orders "A@1,6 B@2,5 C@2,8"` |
| Moves | `--orders`: `X>x,y` move, `X>a,b>x,y` via waypoints, `X=hold`, `X@x,y` deploy a reserve unit | `--orders "D>7,2 F>3,9>5,8 G@0,4"` |
| Action | `--pick N` from the numbered options | `--pick 2` |

- Unit ids: UPPERCASE letters are yours (including reserve units), lowercase are the enemy's. Ids never change.
- Coordinates are `x,y`: x is the column and y the row, counted from the top-left, as in the map rows.
- Units without a move clause hold (Held gives +1 initiative; Braced units get +3 when an enemy moves into range).
- Move orders are hidden and simultaneous. Enemies entering the same tile, or swapping tiles, clash to the death with
  basic strikes. Zone of control stops a unit that becomes adjacent to an enemy.
- Attack options show exact damage and whether they kill. Combat is deterministic.

## Winning (Deathmatch)

A player whose army value (units on the field plus undeployed reserve) falls below 25% of the draft budget at the end
of a turn loses (Rout). Otherwise, after turn 15 the player who destroyed more enemy value wins.

## Self-play (referee)

When asked to run LLM vs LLM:

1. `$SIM new --out playtests/<name>.json --p1 llm --p2 llm --seed N`.
2. Start two subagents, one per seat, each given this skill and its seat. Each keeps its own context, so neither
   sees the other's plans.
3. Loop on `$SIM status <match> --format toon`. Message the subagent for each seat that owes a decision (both, in
   simultaneous phases) and wait for it to act.
4. When the match ends, run `$SIM replay <match> --format text`, then summarize the game and every `RULES?` note.
