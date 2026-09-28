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

### 4.1 Turn Structure

**Simultaneous movement, initiative-ordered actions.** Guessing lives in positioning; combat stays open and readable.

Each turn has three phases:

1. **Movement (simultaneous, hidden).** Both players secretly give every unit a move order (or **Hold**), lock in, and all moves resolve together.
2. **Clashes.** Enemy units that tried to enter the same tile fight immediately, before normal initiative play (see below).
3. **Action (open, per-unit initiative).** Every unit that can act is ordered by its **effective initiative**, highest first. When a unit's slot comes up, its owner chooses its action (attack / ability / wait), and the action resolves immediately. A unit killed before its slot does not act.

**Movement resolution** (decided 2026-09-27, first pass):

- **Orders are paths.** A move order is the exact tile path. The UI and tools fill in the cheapest path by default (with deterministic tie-breaks), and the player can add waypoints to pick a route.
- **One tile per tick.** All moving units advance one tile per tick, whatever the terrain costs. Cost only limits how far a unit gets, so a unit crossing costly terrain finishes its move in fewer ticks.
- **Zone of control:** after each tick, every moving unit that is now adjacent to an enemy (using the new positions) stops. It only triggers on *becoming* adjacent: a unit that starts the phase next to an enemy can move away freely, and the minimum move (§5) works for it too.
- **Paths may enter a tile an enemy stands on now**, betting it moves away. If the enemy stays, the step is blocked and the unit stops. If the enemy moves into this unit's tile at the same time, it's a swap clash (below).
- **Friendly units pass through each other** but can't end on the same tile. A unit that has to stop on a friendly-occupied tile (zone of control stops it there, or the friendly unit held on its destination) backs up to the last tile of its path it can legally end on. Orders whose destination is a friendly tile with a Hold order are rejected at submit.
  - The unit that reached the tile first keeps it; between units that arrived on the same tick, the higher base Initiative keeps it.
  - **Cascade:** if the tile a unit backs up to is held by a friend who also moved this turn, that friend backs up too, recursively. A unit that held all turn is never displaced. (Decided 2026-09-27.)
- **Friendly collisions:** if two friendly units would *end* the same tick on the same tile, the one with higher base Initiative (then lower unit ID) takes it, and the other stops short. Friendly units may swap tiles.
- **Clash:** when enemy units enter the same tile on the same tick, they stop on their previous tiles and are marked to clash.
  - **Swaps count too:** two enemy units that try to swap tiles in the same tick clash. The winner takes the tile the loser stood on. Units never pass through enemies.
  - **Three or more units:** friendly collisions are settled first, so at most one unit per side contests the tile. The others stop short. What remains is a normal 1v1 clash.
  - **The contested tile is blocked** for the rest of the movement phase. It's reserved for the clash winner, and other paths through it stop just before it.

  Clashes resolve after movement and before the action phase:
  - The units fight **to the death** with basic melee only. No abilities or special attacks, even for ranged units.
  - The winner moves onto the contested tile.
  - Later ideas: unit traits that give clash buffs, or let a unit escape a clash.
  - The winner can't attack again that turn.

  Guessing the opponent's exact destination is a gamble: you get a guaranteed fight, but you don't get to pick it, and it replaces your unit's attack.
**Initiative:**

- Each unit has a base **Initiative** stat.
- **Held (+1):** a unit with a Hold order that didn't move gets a small bonus. Any movement, even a 1-tile step, counts as moving. Rooted units don't get it (RacesAndUnits §2.2).
- **Braced (+3)** (unit trait, melee defenders only): a held unit that an enemy **moved next to** this turn gets a larger bonus instead. A braced Goblin Tank (2 → 5) strikes a charging Grunt (3) first. Bracing units are the counter to blind charges, and they give a race a defensive identity without making every unit reward sitting still. (Decided 2026-09-27, after self-play: Braced used to trigger anywhere in range and belonged to Archers, which made a held Archer line nearly unassailable. `bracedTrigger` in the rules config switches between `Adjacent` and the old `InRange`.)
- **Ties between players** go to the player with tie priority, which alternates each turn. **Who has it on turn 1 is set by the game mode.** In Deathmatch it's the player with the lower value **on the field** (the Cost of their placed starting army, reserves not counted); if the values are equal, a coin flip seeded by the match seed decides. Starting lighter to keep a bigger reserve buys the first tie.
- **Ties between one player's own units** go by unit ID, lowest first.
- After movement and clashes resolve, the full action order is shown before anyone acts.

**Consequences:**

- Kiting becomes a prediction game across turns: attack in the action phase, then guess the next movement phase.
- Slows and roots applied in the action phase visibly shape the next movement phase.
- A bracing elf holding in forest gets first shots on charging goblins; goblins can counter by stopping just outside its range to bait a move, or by charging a unit that can't brace.
- Clashes favor the Goblin swarm: cheap units can be thrown at likely elf destinations to force fights.
- Abilities act in the action phase by default. Movement abilities (dashes, teleports) are part of hidden move orders.
- Hotseat only needs a "pass the device" screen for the movement phase.

**Open:**

- ~~Size of the Held and Braced bonuses. Does a 1-tile step count as moving?~~ +1 / +3 first pass; any move counts (above). Which units can brace?
- ~~Can a player delay a unit to a later slot?~~ Yes, once per turn, to the end of the order (§4.2).
- Turtling: with bracing limited to some units, holding is less dominant. Do maps and modes still need to force engagement?
- **Clash resolution:**
  - ~~Do strikes land simultaneously, or in initiative order?~~ Alternating, in initiative order (§4.3).
  - ~~Can the winner still use a non-attack ability that turn?~~ No; the clash is its action (§4.2).
  - ~~What happens with three or more units?~~ Settled to one unit per side, then 1v1 (above).
- ~~Friendly collisions and swaps (bounce vs. initiative wins).~~ Higher initiative wins; friendlies pass through and may swap (above).
- Planning timer for online play.

### 4.2 Unit Actions

**Move, then act, once per turn.** Each unit gets one move order in the movement phase and one action in its initiative slot (§4.1). The phase split already enforces the order: nothing acts before it moves, and by default nothing moves after it acts.

**Move order** (movement phase, hidden):

- **Move** along a path within its Movement points (terrain costs in §5), or **Hold**. Only Holding earns the Held (and possibly Braced) bonus.
- **Movement abilities** (dashes, teleports) replace the normal move. Each one states whether it also uses up the unit's action. The default is that it does, so "dash in and attack" is a special trait, not a given.

**Action** (action phase, in initiative order):

A unit takes exactly **one** of:

| Action | Effect |
|---|---|
| Attack | A basic attack against a target in Range (§4.3). Moving first never prevents or weakens it. |
| Ability | Use one ability. Abilities compete with attacks for the slot. |
| Wait | Do nothing. There is no Guard or Overwatch; Held and Braced already reward standing still. |
| Delay | Once per turn, give up the slot and act after every non-delayed unit. Delayed units act in their original initiative order. A delayed unit can't delay again. |

- A **clash winner** gets no action that turn, not even a non-attack ability. The clash was its action.
- **Summoned** units can't act on the turn they appear (RacesAndUnits §2.1). Reserves that arrive outside their deploy zone follow the same rule; reserves that arrive inside it can act (§4.4).
- A unit killed before its slot doesn't act (§4.1).

**No facing.** Units have no facing, and attacks from any side are equal. Support (§4.3) already rewards surrounding a target, and facing would double-count it (and need four directions per sprite).

**Trait exceptions.** Traits can bend move-then-act in either direction:

- **Restrict:** e.g. a heavy crossbow (Human Arbalist) that can't attack if it moved this turn.
- **Extend:** rare "hit and run" traits that let a unit move a little *after* acting (e.g. a later elf unit). The move happens openly in the action phase, right after the action.

**Open:**

- Hit and run: how far can the unit move after acting (1–2 tiles?), does zone of control stop it, and is it too strong a kiting tool for elves?
- Does Delay give up the Held/Braced initiative bonus? (It shouldn't matter, since delayed units go last anyway, unless several units delay.)
- Should the UI let a player queue actions for several units ahead of their slots, or always prompt slot by slot?

### 4.3 Combat (proposed)

**Deterministic combat, uncertain positioning.** The guessing already happens in hidden movement (§4.1). Once units are placed, the player should know exactly what an attack will do. This supports pillar 3 ("Readable at a glance") and lets the UI show exact outcomes before an attack is confirmed.

**Stats** (small integers, so the math can be done in your head):

| Stat | Meaning |
|---|---|
| HP | Damage a unit can take before it dies. No regeneration unless an ability or trait grants it. |
| Attack | Base damage of the unit's basic attack. |
| Defense | Flat reduction to incoming damage. |
| Movement | Movement points per turn, spent on terrain move costs (§5). |
| Range | Min–max attack distance in tiles (Manhattan distance, §5), e.g. `1` for melee and `2–3` for an archer. |
| Initiative | Action order (§4.1). |
| Vision | Sight radius. Unused until fog of war arrives (§7). |

Units also carry **traits**, named rules that bend the defaults (Braced from §4.1 is the first). Traits keep races asymmetric without adding stats every unit has to care about.

**Damage:**

```
damage = max(1, Attack + Support − (Defense + terrain Defense))
```

- **Minimum 1 damage:** chip damage always lands, so nothing is fully immune to a swarm.
- **Support:** +1 Attack for each *other* friendly unit adjacent to the target, up to +2. It applies to **melee strikes only**: attacks from 1 tile (including point blank and Retaliate) and clash strikes. Ranged shots from 2+ tiles get none. This is how cheap goblins hurt armored or entrenched targets: surround first, then strike. (Decided 2026-09-27, after self-play: with Support, a single Archer shot killed any 5-HP Goblin, so the defender's first volley decided fights. `supportMeleeOnly` in the rules config restores the old rule.)
- **Terrain Defense** applies to the defender's tile only (values in §5, e.g. forest/hills +1, mountains +2, swamp −1).
- There are no hit chances and no damage variance. Randomness can come in later through specific abilities (e.g. the Demon mind control), never through basic attacks.

**Attack resolution** (a unit's action-phase slot):

1. The target must be within Range, and for ranged attacks in line of sight (below).
2. Deal damage and apply on-hit trait effects (e.g. goblin health gain, the Mauler's slow).
3. A unit at 0 HP dies and is removed right away. If it hadn't acted yet, it loses its initiative slot (§4.1).

**Counterattacks: none by default.** Initiative already decides who strikes first, and every surviving unit gets its own slot to hit back. Automatic counters would double-count that and punish the goblin swarm for attacking at all. Instead, **Retaliate** is a trait: a unit with it strikes back once per turn at melee attackers it survives. "Melee" means **any attack from distance 1**, including a ranged unit's point-blank shot. It's a candidate for the Goblin Tank or a Dwarf defender.

**Ranged units:**

- **Line of sight:** forest and mountain tiles *between* the attacker and the target block ranged attacks. The target's own tile never blocks. The same rule later drives vision (§7).
- **Point blank:** a ranged unit whose min range is 1 can attack adjacent enemies at half Attack (rounded down). Support is added after halving: `floor(Attack / 2) + Support − (Defense + terrain Defense)`, minimum 1. Units with min range 2 can't attack adjacent enemies at all. Together these make "close the gap" (the goblin plan in §6.1) mean something.

**Clashes** (answers the open clash questions in §4.1):

- Units trade basic attacks using the damage formula, but **alternate** instead of striking at the same time. The unit with the higher effective initiative strikes first, and ties use that turn's tie priority. Exactly one unit survives, so the contested tile is never left empty.
- Terrain Defense comes from each unit's *own* tile (where it stopped). Support counts as normal, so a clash next to friendly units favors you.
- Ranged units (max range above 1) clash at half Attack, rounded down, **including units with min range 2** such as Archers. A clash is a brawl, not a basic attack.
- **Clashes are pure fighting** (decided 2026-09-27). Damage modifiers apply: terrain Defense, Support, and Crush. Clash-specific traits apply (Reckless, Slippery). On-hit effects don't: no Bloodthirst healing, no Hamstring, and no Retaliate. This also guarantees every clash ends, since damage is at least 1 and nobody heals. The engine still stops a clash after `maxClashStrikes` strikes as a safety guard.

**Status effects:** slows, roots, and similar effects applied in the action phase last a set number of turns and are shown on the unit. They mostly matter in the *next* movement phase, which is what makes them readable (§4.1 Consequences).

**UI implications:** when targeting, show the exact damage and a "kills" marker. When a clash is possible, show the predicted winner and its remaining HP. The server still owns the math (TechnicalDesign), so the client preview is only a display.

**Open:**

- Support: is +1 per adjacent ally, capped at +2, strong enough for goblins without making them trivially beat elves? Should allies need to be *behind* or *beside* the target to count (flanking)?
- Retaliate: once per turn, or once per attacker? (~~Does it work against ranged attacks?~~ Only from distance 1, above.)
- Should some high-ground terrain (hills) give ranged attackers +1 max range?
- Stat budget: target HP ranges (e.g. goblins 4–8, elves 6–10) so fights take 2–3 hits and not 1 or 6.

### 4.4 Army Building / Economy (proposed)

**Draft once, deploy over time.** The pre-match draft is the whole economy. There are no workers, buildings, or gathered resources. Armies still grow during the match because part of the draft is held back as reserves, and a single number, **Command**, controls when they arrive.

**Draft** (before the match, hidden from the opponent):

- Each player spends a **draft budget** (first pass: **40 points**) on units at their Cost (RacesAndUnits §3.2, §4.2). Unique limits apply to the whole draft.
- Up to the **starting cap** (first pass: **30 points**) is deployed on the map at the start. Everything else goes into the **reserve**, off the map.
- A player doesn't have to fill the cap. Starting lighter means a larger reserve.
- **What the opponent sees:** once the match starts, the reserve's *composition* stays hidden. Only its total value is visible (it's part of army value in the HUD, §4.5). Units become visible when they arrive.

**Deploy zones:**

- Each player's **deploy zone** is the **3 back columns** of their short map edge, full height (§5). On a 20×14 map that's 42 tiles per side, with 14 columns between the zones, so no unit can reach the enemy on turn 1.
- **Starting placement** is hidden and simultaneous. Both players place their starting army anywhere in their own zone, lock in, and then both placements are revealed. It uses the same order UI as the movement phase (every unit gets a "deploy on this tile" order), and hotseat play uses the same pass-the-device screen.

**Command:**

- Each player gains a flat **+2 Command** at the start of every turn **from turn 2 on**, starting from 0. No reserves arrive on turn 1: the starting armies open alone. Unspent Command carries over.
- Command's only use is deploying reserves. It isn't earned from kills or objectives (see Later below).

**Deploying reserves** (part of the movement phase, §4.1):

- Deploying a reserve unit costs Command equal to its Cost and is given as a hidden move order: "deploy this unit on this tile."
- The tile must be an empty tile in the player's deploy zone and not adjacent to an enemy unit at the start of the turn. Races can change where units arrive (below).
- Arriving units are placed before moves advance, so zone of control and clashes apply to them normally.
- **Home arrivals act.** A unit that arrives inside its own deploy zone acts normally on its arrival turn. A unit that arrives anywhere else through a race arrival rule (an elf forest, a goblin mountain outside the zone, later a dwarf tunnel) follows the **Summoned** rule and can't act that turn (RacesAndUnits §2.1). This stops spawn camping from being free kills: enemies waiting near your edge can be hit by whatever arrives. Forward arrivals remain ambushes that need a turn to pay off.
- At most **2 arrivals per turn**.
- **Arrival clash:** if both players deploy onto the same tile (possible where deploy rules overlap), the two arrivals clash right away, before anyone moves. Both fight as if standing on that tile (its terrain Defense and adjacent Support count), and both players pay the Command. The survivor keeps the tile and, like any clash winner, gets no action that turn. (Decided 2026-09-27.)
- Reserves can't be sold, swapped, or refunded mid-match.

At +2 Command per turn from turn 2, a 10-point reserve is fully deployed around turn 6, so the early fight is the starting army and reinforcements shape the midgame.

**Race arrival rules.** The core rules are the same for everyone. Each race bends *how or where* its reserves arrive:

| Race | Arrival rule (draft) |
|---|---|
| Elves | May also arrive on any forest tile with no enemy within 2 tiles. |
| Goblins | Deploying a unit of Cost 3 or more costs 1 less Command. May also arrive on mountain tiles in or next to the deploy zone. |
| Dwarves | May arrive at any tunnel the Runesmith has made. |
| Merfolk | May arrive on any water tile. |
| Undead | Dead units return to the reserve after N turns (their race mechanic). |
| Demons | Sacrifice a unit on the field to cut a deploy's cost. |
| Humans | Arrivals on road tiles may move on their arrival turn. |

The MVP only needs the Elf and Goblin rules (RacesAndUnits §3.1, §4.1).

**Later (mode-specific layers):**

- **Objective Command:** held objectives (e.g. Guard Towns) grant extra Command.
- **Promotions:** veteran units upgrade in place (e.g. Grunt → Bruiser), for campaign or a custom mode.
- **Mercenaries** (§7) are recruited on the map and don't use Command.

**Open:**

- The budget numbers: 40 / 30 / +2 per turn / 2 arrivals per turn. How do they interact with match length (open question 6)?
- ~~Deploy zones on a fixed map edge invite spawn camping. Is "not adjacent to an enemy" enough, or do zones need more protection?~~ Home arrivals act on their arrival turn (above), and Rout ends hopeless games (§4.5).
- ~~Income arrives at the start of turn 1. Is that fine, or should income start on turn 2?~~ Turn 2 (above).
- ~~Is the Goblin discount too strong when combined with already cheap units?~~ It now applies only to units of Cost 3 or more.

### 4.5 Win Conditions

Mode-dependent (see [Game Modes](#8-game-modes)).

**Deathmatch: Rout plus a turn limit.** Hunting down every last unit is tedious. Summons, unspent reserves, and fast or hidden units (Scouts, later forest invisibility) could drag a lost game out for many turns. Deathmatch therefore ends before that point.

- **Army value** = the Cost of a player's units on the field plus the Cost of their undeployed reserves. Summoned units are worth 0 (RacesAndUnits §2.1).
- **Rout:** at the end of any turn, a player whose army value is below **25% of their draft budget** loses. If both players rout on the same turn, the one with more army value left wins; if that's also tied, the match is a draw.
- **Objectives** (decided 2026-09-27): maps mark objective tiles (Riverford: the four ford tiles, §5.1). At the end of each turn, the player with a unit on **more** objective tiles than the opponent scores **2 objective points**. Holding equally many scores nothing, so each side has to take tiles from the other. This exists because a safe draw at the turn limit made waiting outside the enemy's range the best play (Simulation §10).
- **Turn limit:** if nobody has routed by the end of turn **15**, the player with the higher **score** wins: destroyed value plus objective points. An equal score is a draw.
- Both players' army values, destroyed value, and objective points are always shown in the HUD, so neither a rout nor the turn-limit result comes as a surprise.

There's no leader or king unit. Not every race has a natural leader, and a fragile elf leader would just sit at the back.

**Open:** tune the 25% threshold and the 15-turn limit in playtests (see open question 6).

## 5. Map and Terrain

**Square grid, 32×32 tiles, top-down with ¾ sprites.**

- **Grid:** square.
- **Tiles:** 32×32 pixels. At the 640×360 base resolution (§9), the screen shows 20×11 tiles, so an MVP map of about 18×12 to 20×14 fits on one screen, or nearly.
- **Camera:** top-down and orthogonal. The grid logic is fully top-down, but trees, mountains, and units are drawn in ¾ view (slightly front-facing, like Advance Wars or GBA Fire Emblem). They may overlap the tile above them and are y-sorted, so height stays readable at 32 px.
- **Art scope:** only water edges and roads are auto-tiled (Godot terrain sets). Forest, hills, and mountains are decorations drawn on a grass base, with no blended edges between terrain types.

**Grid geometry:**

- **Movement is 4-way** (orthogonal only). A path costs the sum of the move costs of the tiles it enters.
- **Adjacent means the 4 orthogonal neighbors.** This applies everywhere: melee range, zone of control, Support (§4.3), clashes, and "adjacent" in abilities and arrival rules. Diagonal tiles are not adjacent.
- **Distance is Manhattan** (`|dx| + |dy|`). This covers attack Range, ability ranges, auras, and "within N tiles". An Archer's Range of 2–3 is a diamond-shaped ring.
- **Line of sight:** draw a line from the center of the attacker's tile to the center of the target's tile. It's blocked if it passes through the interior of a blocking tile. A line that passes exactly through a corner is blocked only if *both* tiles touching that corner block. The attacker's and the target's own tiles never block (§4.3).

**Minimum move** (proposed): a unit whose move order isn't Hold can always move **one tile** onto an adjacent passable tile, even if the tile costs more than its Movement. That step uses up its whole move. This keeps slow units able to cross mountains without multi-turn "climbing" state. Each difficult tile simply costs a whole turn. Examples:

- A 1-Movement unit crosses a 3-tile mountain range in 3 turns, one tile per turn, the same pace it has on plains.
- A goblin Tank (Movement 3, mountains cost 2 for goblins) also crosses it in 3 turns. An Archer (Movement 4) takes 3 turns too, but it can still move a plains tile and then enter the first mountain in the same turn (1 + 3 = 4).
- Slowed (−2 Movement, minimum 1) never stops a unit from moving. Only Rooted does.

**Terrain:**

| Terrain | Move cost | Defense | Blocks line of sight | Notes |
|---|---|---|---|---|
| Plains | 1 | 0 | No | Baseline |
| Road | 1 | 0 | No | Same as plains in the MVP; the Human arrival rule uses it later (§4.4) |
| Forest | 2 | +1 | Yes | Elves: cost 1 (Forestwalk) and see through it (Canopy Sight) |
| Hills | 2 | +1 | No | High ground for ranged units? (open, §4.3) |
| Mountains | 3 | +2 | Yes | Goblins: cost 2 (Mountain-born). Passable for everyone; slow units use the minimum move |
| Bridge | 1 | 0 | No | Crosses water; a natural chokepoint |
| Water | Impassable | — | No | Merfolk later |
| Swamp | Later | −1 | No | |
| Desert | Later | 0 | No | Merfolk can't enter? |

The MVP set is plains, road, forest, hills, mountains, bridge, and water.

**Map layout:** MVP maps are point-symmetric (the same after a 180° rotation), so both sides get the same terrain. The deploy zones sit on the short edges and are marked on the map.

**Open:**

- ~~Minimum move: should it also work out of zone of control?~~ Yes, always (§4.1).
- MVP map size: about 20×14 with 3-column deploy zones (§4.4). Confirm once army sizes are tested.
- Does road need a real benefit (e.g. +1 Movement if the whole move is on road) before Humans arrive?

### 5.1 MVP Map: Riverford (draft)

A first map, so simulations have something real to play on. It's **20×14 and point-symmetric**: tile `(x, y)` matches `(19 − x, 13 − y)`. P1 deploys in columns 0–2 and P2 in columns 17–19 (§4.4).

```
              1111111111
    01234567890123456789
 0  ..%%..^^.~~..%..+...
 1  ..%..+^..~~.%%....^.
 2  ..=======##===..+...
 3  ...+.%%..~~...^..%..
 4  .%...%...~~..++..%%.
 5  .%..^^......%%......
 6  ....^..+....%...^...
 7  ...^...%....+..^....
 8  ......%%......^^..%.
 9  .%%..++..~~...%...%.
10  ..%..^...~~..%%.+...
11  ...+..===##=======..
12  .^....%%.~~..^+..%..
13  ...+..%..~~.^^..%%..
```

Terrain: `.` plains, `=` road, `%` forest, `+` hills, `^` mountains, `#` bridge, `~` water (this is the Simulation scenario format).

- **Three ways across the river** (columns 9–10): a bridge at each end of the map (rows 2 and 11), each fed by a road, and an open ford in the middle (rows 5–8). The ford is the shortest route and the most exposed.
- **Objectives:** the four plains tiles in the middle of the ford, (9,6), (10,6), (9,7), and (10,7) (§4.5). Each side is closest to two of them, so scoring means pushing into the other half of the ford.
- **Forest and mountains on both halves:** the forest pockets next to the ford (e.g. (12–13, 5)) give elves firing positions, and mountain spurs (e.g. (4–5, 5) and (6–7, 0)) give goblins covered approaches.
- **Deploy zones** each have forest (for From the Trees) and a mountain (for Out of the Caves) nearby, so both MVP arrival rules get exercised.
- All of it is a first pass for simulations to test. Expect it to change once tournaments show which side each route favors.

## 6. Races

Race traits, unit stats, and abilities are in [RacesAndUnits](RacesAndUnits.md). The original brainstorm is in [UnitIdeas](../og/UnitIdeas.md).

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
- **Goblins:** Rusher, Wolf Rider, Bruiser, Tank, Mauler, War Lord

**What this matchup tests:** terrain move costs and defense, ranged vs melee and counterattacks, healing, movement-control effects, and (if forest invisibility makes the cut) the first bit of hidden information.

**Open for this matchup:**

- The Elf mage ability is still undefined. Options include growing forest tiles (mirroring the Dwarf mountain mage) or rooting/entangling units.
- Is forest invisibility in the MVP? It needs per-player views of the state, which the server design supports, but it adds scope.
- Army sizes: how many goblins per elf keeps it balanced? Now set by the draft budget and starting cap (§4.4).

## 7. Systems (later)

- **Fog of war and vision:** per-unit vision, terrain blocks sight, scout/reveal units.
- **Day/night cycle:** affects vision and some abilities.
- **Traps:** hidden; damage or root for 1–2 turns.
- **Mercenaries:** neutral recruitable units on the map, risky to obtain.

## 8. Game Modes

| Mode | Summary | MVP |
|---|---|---|
| Deathmatch | Rout the enemy army, or have destroyed more value by the turn limit (§4.5) | ✅ |
| Stronghold | Each side has a home base in its deploy zone; capture or destroy the enemy's to win. Needs structures, capture rules, and anti-rush balance | |
| Helm's Deep | Defender holds a fortress for N turns with fewer units but a defensive bonus | |
| Capture the Flag | | |
| Protect the Caravan | Escort a moving objective | |
| Guard Towns | Protect 1 large + 2 small towns that give bonuses/units; placement chosen by players | |

## 9. Art and Audio

- **Art:** pixel art. Palette, resolution and animation budget are **TBD**.
- **Base resolution:** 640×360, integer-scaled (×3 at 1080p, ×4 at 1440p, ×6 at 4K). Tiles are 32×32 (§5).
- **Audio:** TBD

## 10. Platforms — TBD

Windows first. Linux/macOS? Steam? Mobile? Web? (This affects the networking choices; see TechnicalDesign.)

## Open Questions

Answer inline or move decisions into the [Decision Log](#decision-log).

1. ~~**Turn structure:** alternating full turns, alternating activations, or simultaneous?~~ Decided: simultaneous movement, initiative-ordered actions (see §4.1).
2. ~~**Economy:** fixed/drafted armies, in-match recruiting, or hybrid?~~ Proposed: draft with reserves deployed using a flat Command income (see §4.4).
3. **Combat randomness:** deterministic, or dice/percent-based? Proposed: deterministic basic attacks, with randomness only in specific abilities (see §4.3).
4. ~~**Grid:** square or hex? **Tile size:** 16 or 32 px?~~ Decided: square grid, 32×32 tiles, top-down with ¾ sprites (see §5).
5. ~~**MVP races:** which two first?~~ Decided: Elves vs Goblins.
6. **Match length target:** 10 minutes? 45 minutes? Deathmatch now has a 15-turn limit (§4.5); tune it once we know how long a turn takes.
7. **Single player:** is there an AI opponent or campaign, or is it multiplayer-only? (Bots for testing are planned either way; see [Simulation](Simulation.md).)
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
| 2026-09-26 | Turn structure: simultaneous hidden movement, then per-unit initiative-ordered actions | Held bonus for all units, Braced bonus as a unit trait; same-tile enemy moves clash (basic melee to the death, winner takes the tile and can't attack again that turn) before the action phase; see §4.1 |
| 2026-09-27 | Unit actions: move then one action (attack / ability / wait / delay once to end) | No facing; clash winners get no action; movement abilities say whether they cost the action; traits may restrict or extend move-then-act (e.g. hit and run); see §4.2 |
| 2026-09-27 | Economy (proposed): pre-match draft with reserves, deployed using a flat Command income | No workers or gathered resources; 40-point draft, 30 on the field, +2 Command per turn; arrivals are hidden move orders and follow the Summoned rule; race-specific arrival rules; see §4.4 |
| 2026-09-27 | Map: square grid, 32×32 tiles, top-down with ¾ sprites, 640×360 base resolution | 4-way movement; adjacency is the 4 orthogonal neighbors everywhere; Manhattan distance; center-to-center line of sight; mountains passable for all (cost 3, goblins 2), and a minimum-move rule (proposed) lets slow units enter any passable tile one step per turn; see §5 |
| 2026-09-27 | Deathmatch win condition: Rout (army value below 25% of draft) plus a 15-turn limit decided by value destroyed | No leader unit; summoned units are worth 0; a home base becomes a separate later mode (Stronghold); see §4.5, §8 |
| 2026-09-27 | Deploy zones: 3 back columns on each short edge; starting armies placed hidden and simultaneously; reserves arriving in their own zone can act that turn | Replaces "arrivals follow the Summoned rule" from the economy entry: only arrivals outside the zone (race arrival rules) can't act; MVP maps are point-symmetric; see §4.4, §5 |
| 2026-09-27 | Movement resolution: path orders, 1 tile per tick, zone of control checked after each tick (only on becoming adjacent), friendlies pass through, enemy swaps clash, 3+ unit clashes reduce to 1v1, contested tile blocked | Held +1 / Braced +3; any move loses Held; own-unit ties by unit ID; turn-1 tie priority per game mode (Deathmatch: lower starting value on the field, then a seeded coin flip); see §4.1 |
| 2026-09-27 | Combat details: Support applies to all attacks; point blank = floor(Attack/2) + Support; ranged units (min range 2 included) clash at half Attack; Retaliate triggers on any attack from distance 1 | See §4.3 |
| 2026-09-27 | Economy details: Command income starts on turn 2; the Goblin discount applies only to Cost 3+; the opponent's reserve composition is hidden, its value is visible | See §4.4 |
| 2026-09-27 | First MVP map drafted: Riverford, 20×14, point-symmetric, two bridges and a central ford | See §5.1 |
| 2026-09-27 | Command-line tools use McMaster.Extensions.CommandLineUtils (attribute API) | Only command-line projects reference it; see TechnicalDesign §2.3 |
| 2026-09-27 | Engagement fixes after self-play showed defense dominating: Braced triggers only on enemies that end adjacent and moves from Archers to Tanks; Support counts for melee strikes only; Deathmatch scores objective points for holding more objective tiles, and the turn limit compares destroyed value plus objective points | All three are rules-config switches (`bracedTrigger`, `supportMeleeOnly`, `objectivePointsPerTurn`) so tournaments can compare variants; see §4.1, §4.3, §4.5, Simulation §10 |
| 2026-09-27 | Engine edge cases confirmed: clashes are pure fighting (no on-hit effects); paths may enter an enemy's current tile; friendly back-up cascades; Slippery always retreats; Braced range is 1 to max range; Mend targets allies only; same-tile arrivals clash | Found while building the engine and fuzzing it; see §4.1, §4.3, §4.4 and RacesAndUnits §3.2, §4.1 |
| 2026-09-27 | Headless simulation and LLM play: matches run in memory on a deterministic Core engine; bots in `Fantactics.Ai`; LLMs play through a file-backed `fantactics-sim` CLI | Bots only see a player view; match records are JSON (seed + command log + state hashes); the CLI prints TOON for LLM seats to save tokens; see [Simulation](Simulation.md) |
| 2026-09-28 | Goblins get the Wolf Rider (Cost 4, HP 7, Atk 4, Mv 7, Init 6, Bloodthirst 1, Reckless) | Bot tournaments showed Elves winning 78% of Captain mirrors. Goblin buffs beat an Archer nerf, and closing the gap beat shooting back or armoring up; the Wolf Rider brought Elves to 48% (Simulation §10). Rules 0.4.0 |
