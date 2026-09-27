# Fantactics: Game Design Document

> Status: **Draft**, started 2026-09-26. Sections marked **TBD** or listed under [Open Questions](#open-questions) still need a decision.
> Source brainstorms: [GameIdeas](../og/GameIdeas.md), [UnitIdeas](../og/UnitIdeas.md).
> Technical architecture: [TechnicalDesign](TechnicalDesign.md).

## 1. Overview

**Fantactics** is a fantasy-themed, grid-based, turn-based strategy game in pixel art. Players field armies from asymmetric races, each with its own playstyle, unique mechanic, and a "mage" unit whose abilities tie into that race's identity (often by reshaping the terrain).

- **Genre:** turn-based tactics / strategy
- **Engine:** Godot 4.7.2 (.NET / C#)
- **Art:** pixel art
- **Players:** 1v1 first. Local, LAN, and online play.

## 2. Design Pillars (proposed)

1. **Asymmetry with identity.** Each race plays differently, not just with different stats. Knowing a race's mechanic should change how you play against it.
2. **Terrain is a weapon.** Terrain affects movement, vision, and combat, and some races can reshape it.
3. **Readable at a glance.** Pixel art and UI make unit roles, threats, and terrain obvious without reading tooltips.
4. **Mind games.** Hidden information (fog of war, traps, invisibility) rewards scouting and bluffing.

## 3. Scope

### MVP (first playable)

- A single hand-made map on a square grid
- 1v1, **Deathmatch** only
- 2 races: **Elves vs Goblins** (see [MVP Matchup](#61-mvp-matchup-elves-vs-goblins))
- Core terrain set (plains, forest, hills, mountains, water)
- Local hotseat play first, then LAN, using the same command pipeline (see TechnicalDesign)

### Later

- Remaining races, game modes, fog of war, day/night cycle, traps, mercenaries
- Online play with lobbies
- Map editor? Campaign/AI? (**TBD**)

## 4. Core Gameplay

### 4.1 Turn Structure — TBD

- Options: **alternating full turns** (each player moves their whole army, then passes), **alternating unit activations**, or **simultaneous turns** with a resolution phase.
- Turn timer for online play?

### 4.2 Unit Actions — TBD

Proposed baseline: each unit may **move** then **act** (attack / ability / wait) once per turn. Open: can a unit act and then move? Does facing matter?

### 4.3 Combat — TBD

- Stats (proposed): HP, Attack, Defense, Movement, Range, Vision.
- Resolution: deterministic damage vs. random rolls (hit chance, damage variance)?
- Counterattacks: does the defender strike back?
- Terrain modifiers: defense bonus, blocking line of sight for ranged attacks?

### 4.4 Army Building / Economy — TBD

Options:

- **Fixed armies:** preset or drafted before the match (points buy).
- **In-match economy:** capture buildings for income and recruit during the match.
- **Hybrid:** points-buy starting army, plus limited reinforcements (e.g. towns in Guard Towns mode).

### 4.5 Win Conditions

Mode-dependent (see [Game Modes](#8-game-modes)). Deathmatch: eliminate all enemy units (or the leader/king unit? **TBD**).

## 5. Map and Terrain

- **Grid:** square vs. hex (**TBD**)
- **Tile size:** 16×16 vs. 32×32 pixels (**TBD**, strongly affects art scope)
- **Camera:** top-down orthogonal vs. isometric (**TBD**)

| Terrain | Move cost | Defense | Vision | Notes |
|---|---|---|---|---|
| Plains | 1 | 0 | — | Baseline |
| Dirt road | TBD | 0 | — | Faster movement? |
| Forest | 2 | + | Blocks? | Elves unaffected / invisible here |
| Hills | 2 | + | Bonus? | |
| Mountains | 3 / impassable? | ++ | Blocks | Goblins move better; dwarves reshape |
| Bridge | 1 | 0 | — | Chokepoint |
| Ocean / water | Impassable* | — | — | *Merfolk move freely |
| Swamp | TBD | − | — | |
| Desert | TBD | 0 | — | Merfolk can't enter? |

## 6. Races

The full unit brainstorm is in [UnitIdeas](../og/UnitIdeas.md).

| Race | Identity | Race mechanic (draft) | Mage ability (draft) |
|---|---|---|---|
| Humans | Well-rounded, mechanical | Workers reshape land; healing | Heal / "light" magic |
| Elves | Ranged, mobile, fragile | Forest mobility / invisibility | TBD |
| Dwarves | Defensive, siege | Fortify over time; tunnels | Create/remove mountains, dwarf-only tunnels |
| Goblins | Swarm, cheap | Mountain movement; health gain on attack | Spawn two basic goblins |
| Undead | Attrition | Units return after N turns | Revive-on-death curse |
| Merfolk | Water control | Must return to water every N turns | Summon water nearby |
| Demons | High cost, sacrifice | Sacrifice to reduce cost / buff | Low-chance mind control |
| Wizards | Technical spellcasters | TBD | Grand Wizard elemental spells |
| Holy/Light? | TBD | TBD | TBD |

### 6.1 MVP Matchup: Elves vs Goblins

A strong contrast to prove out the core systems: **few, fragile, ranged units that control forests** vs **many, cheap, melee units that control mountains**.

| | Elves | Goblins |
|---|---|---|
| Army shape | Small, elite, fragile | Large, cheap, expendable |
| Range | Mostly ranged | Mostly melee |
| Home terrain | Forest (no movement penalty; invisible inside?) | Mountains (better movement than other races) |
| Race mechanic (draft) | Stealth and movement in forests | Health gain on attack (the Tank heals heavily; the War Lord boosts the rate) |
| Mage (draft) | TBD | Spawn two basic goblins |
| Wants to | Kite: shoot, then fall back into cover | Close the gap and surround |
| Key threats | Slow/root effects (Goblin Mauler) that stop kiting | Movement-limiting shots (Elven specialist) that stall the rush |

**Candidate MVP units** (from [UnitIdeas](../og/UnitIdeas.md)):

- **Elves:** Archer, Ranger, Herbalist, Scout, plus a movement-limiting specialist
- **Goblins:** Rusher, Bruiser, Tank, Mauler, War Lord

**What this matchup tests:** terrain move costs and defense, ranged vs melee and counterattacks, healing, movement-control effects, and (if forest invisibility makes the cut) the first bit of hidden information.

**Open for this matchup:**

- The Elf mage ability is still undefined. Options include growing forest tiles (mirroring the Dwarf mountain mage) or rooting/entangling units.
- Is forest invisibility in the MVP? It needs per-player views of the state, which the server design supports, but it adds scope.
- Army sizes: how many goblins per elf keeps it balanced? This depends on the economy decision (open question 2).

## 7. Systems (later)

- **Fog of war and vision:** per-unit vision, terrain blocks sight, scout/reveal units.
- **Day/night cycle:** affects vision and some abilities.
- **Traps:** hidden; damage or root for 1–2 turns.
- **Mercenaries:** neutral recruitable units on the map, risky to obtain.

## 8. Game Modes

| Mode | Summary | MVP |
|---|---|---|
| Deathmatch | Eliminate the enemy | ✅ |
| Helm's Deep | Defender holds a fortress for N turns with fewer units but a defensive bonus | |
| Capture the Flag | | |
| Protect the Caravan | Escort a moving objective | |
| Guard Towns | Protect 1 large + 2 small towns that give bonuses/units; placement chosen by players | |

## 9. Art and Audio

- **Art:** pixel art. Palette, resolution and animation budget are **TBD**.
- **Base resolution:** e.g. 640×360 or 480×270, integer-scaled (**TBD**)
- **Audio:** TBD

## 10. Platforms — TBD

Windows first. Linux/macOS? Steam? Mobile? Web? (This affects the networking choices; see TechnicalDesign.)

## Open Questions

Answer inline or move decisions into the [Decision Log](#decision-log).

1. **Turn structure:** alternating full turns, alternating activations, or simultaneous?
2. **Economy:** fixed/drafted armies, in-match recruiting, or hybrid?
3. **Combat randomness:** deterministic, or dice/percent-based?
4. **Grid:** square or hex? **Tile size:** 16 or 32 px?
5. ~~**MVP races:** which two first?~~ Decided: Elves vs Goblins.
6. **Match length target:** 10 minutes? 45 minutes?
7. **Single player:** is there an AI opponent or campaign, or is it multiplayer-only?
8. **Platforms and distribution:** Windows-only to start? Steam?
9. **Team size:** solo dev? Is someone else doing the art?

## Decision Log

| Date | Decision | Notes |
|---|---|---|
| 2026-09-26 | Godot 4.7.2 with C# (.NET 8) | |
| 2026-09-26 | Pixel art | |
| 2026-09-26 | Networking lives outside Godot in a separate .NET app | See TechnicalDesign |
| 2026-09-26 | Solution layout: Core, Protocol, Server, Client (Godot), Core.Tests | See TechnicalDesign §2.1 |
| 2026-09-26 | MVP races: Elves vs Goblins | See §6.1 |
