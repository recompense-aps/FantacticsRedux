# Fantactics: Races and Units

> Status: **Draft**, started 2026-09-27. Companion to [GameDesign](GameDesign.md) (stats, damage, and turn rules are in §4 there).
> Source brainstorms: [GameIdeas](../og/GameIdeas.md), [UnitIdeas](../og/UnitIdeas.md).
> Focus: **Elves** and **Goblins** (the MVP matchup) are designed in detail. The other races are stubs.

All numbers are first-pass values for the prototype. They are here so the rules can be tested, not because they are balanced.

## 1. Race Roster

Proposed final roster: **seven races**. Races are unit tags, not army choices: players draft from every race's units (GameDesign §4.4, and §2.4 below).

| Race | Identity | Home terrain | Race mechanic | Mage | Status |
|---|---|---|---|---|---|
| [Elves](#3-elves) | Few, fragile, ranged, mobile | Forest | Forest movement and sight; hidden in forest (later) | Druid: grows forest, entangles | **MVP, detailed** |
| [Goblins](#4-goblins) | Many, cheap, melee swarm | Mountains | Bloodthirst (heal on attack); mountain movement | Shaman: summons Grunts | **MVP, detailed** |
| [Humans](#51-humans) | Well-rounded, disciplined, light magic | Plains, roads | Reshape land (Workers); healing | Cleric: heal / light | Stub |
| [Dwarves](#52-dwarves) | Defensive, siege | Mountains, hills | Fortify over time; tunnels | Runesmith: create/remove mountains, tunnels | Stub |
| [Undead](#53-undead) | Attrition, won't stay dead | Swamp | Units return after N turns; raises fallen units as Undead hybrids | Necromancer: raise fallen units | Stub |
| [Merfolk](#54-merfolk) | Water control | Water | Must touch water every N turns | Tidecaller: summons water | Stub |
| [Demons](#55-demons) | Expensive, sacrifice | Any | Sacrifice to cut costs or buff | Lord of Death / mind control | Stub |

**Roster decisions (proposed):**

- **Holy/Light folds into Humans.** Humans already had "light magic" and healing in the brainstorm. A separate Holy race would overlap with them.
- **Undead and Demons stay separate, split by theme.** Undead is about *returning* (revival, reanimation). Demons is about *sacrifice* (spending your own units for power). The brainstorm's Demon Zombie and "raise corpses" ideas move to Undead. The Lord of Death stays with Demons but feeds on sacrifices instead of raising the dead.
- ~~**Wizards stay a race.**~~ **Wizards dissolve into the Mage class** (decided 2026-09-28). With the open draft, "every unit is a caster" is what the Mage class describes, so it no longer needs its own race. The Wizard unit ideas move to other races' mages (§5.6).
- **Undead is a race and a template.** Undead keeps a small draftable roster, and its Necromancer turns fallen units into Undead versions of themselves. This is where multi-race units come from (§2.4, §5.3).

## 2. Shared Rules

Terms the unit tables use. These go in GameDesign §4 once they're settled.

### 2.1 Unit Tags

- **Cost:** draft points-buy value, and the Command it costs to deploy the unit from reserve (GameDesign §4.4). Goblins are priced at roughly half of an elf per unit.
- **Unique:** at most one per army.
- **Summoned:** created by an ability during the match. Summoned units can't act on the turn they appear and are worth no points. Reserves that arrive outside their deploy zone also can't act that turn, but they keep their Cost value (GameDesign §4.4).

### 2.2 Status Effects

A duration of "1 turn" means the effect lasts through the *next* movement phase and action phase, then expires.

| Status | Effect |
|---|---|
| Slowed | −2 Movement (minimum 1). |
| Rooted | Can't move (its move order must be **Hold**). It can still act. It doesn't get the Held initiative bonus, because it didn't choose to hold. |

A new application of the same status refreshes its duration; it doesn't stack.

### 2.3 Shared Traits

Traits more than one race can use.

| Trait | Effect |
|---|---|
| Braced | From GameDesign §4.1: a held unit that an enemy moved next to gets a larger initiative bonus. For melee defenders. |
| Retaliate | From GameDesign §4.3: strikes back once per turn at a melee attacker it survives. |
| Aura (X, r) | Friendly units within `r` tiles get effect X. Auras of the same name don't stack. Range is checked at the moment the effect applies (e.g. when an attack deals damage), so it always reflects current positions. |
| Cooldown N | After use, the ability can't be used again for N turns: used on turn t, it's ready again on turn t + N + 1. |

### 2.4 Races, Classes, and the Open Draft

> Decided and implemented 2026-09-28 (rules 0.6.0). Engine notes:
> - A setup may still limit a seat to some races (`allowedRaces`; the CLI's `--p1-races`); drafting outside them is rejected with `race-not-allowed`. Tournaments use this for mono-race comparisons, and records made before 0.6.0 load their one race per seat as that filter.
> - Class tags live on unit definitions (`classes`); the Beast/Mounted class id is `Mounted`.
> - Ally-targeting abilities carry an `allyScope` (`OwnRace` by default, or `AnyFriendly`).
> - Extra races sit on the unit itself (`extraRaces`), ready for the Necromancer; no content uses them yet.
> - Bots have a `raceFocus` style knob that pulls later draft picks toward the races already drafted.

Players draft from **every race's units** (GameDesign §4.4). Race is a tag on each unit, not a choice made for the army.

**Tags.** Every unit has:

- **One race** (a raised unit has two; see Hybrids below). Race traits (Forestwalk, Bloodthirst, …) belong to every unit of that race, as they already do in the engine.
- **Zero or more classes**, assigned by the designer rather than derived from stats:

| Class | Meaning | MVP units |
|---|---|---|
| Mage | Casters and terrain shapers; the race's signature unit | Druid, Shaman |
| Ranged | Fights mainly from range | Archer, Ranger |
| Beast/Mounted | Animals and riders | Wolf Rider |
| Defender | Frontline anchors (Braced, Retaliate) | Tank |

Classes start as tags that abilities and auras can target (e.g. "Mages within 3", "can't target Beasts"). Whether classes get synergies of their own is open (§7).

**Race unity** is rewarded **on the board**, not with draft thresholds:

- **Positional auras** that buff nearby units of the same race (War Cry is the model: "other Goblins within 2").
- **Race-scoped abilities** that only affect the caster's own race.
- So unity is something you keep up through positioning, and a mixed army pays for its flexibility with auras that cover fewer units.

**Scope rule.** A race mechanic or ability affects **its own race by default**. An ability that reaches other races says so explicitly ("any friendly unit", "any unit"). Abilities that target enemies or terrain aren't restricted by race. Current MVP scopes:

| Mechanic | Scope | Change? |
|---|---|---|
| Forestwalk, Canopy Sight, Bloodthirst, Mountain-born | The unit itself (race trait) | No |
| From the Trees | Elf reserves | Already per unit |
| Out of the Caves | Goblin reserves | Already per unit |
| War Cry | Other Goblins | No |
| Mend | Another friendly **Elf** | **Yes:** was any friendly unit |
| Call the Horde | Summons Goblin Grunts | No |
| Pinning Shot, Entangle, Throw Net, Hamstring | Enemies of any race | No |
| Overgrowth | Terrain | No |

**Mages.** There's no cap on Mage-class units beyond each mage being Unique. An army may field a Druid and a Shaman (and later a Runesmith). Watch tournaments for terraforming armies that shut down every other plan.

**Hybrids (multi-race units)** come from the **Undead template**, not from the draft:

- The Necromancer raises a fallen unit as "Undead *X*" (e.g. an Undead Grunt), tagged with **both** its original race and Undead.
- It gets **both races' traits in full** and counts for both races' auras and race-scoped abilities.
- It follows the **Summoned** rule: it can't act on the turn it appears, and it's worth 0 for army value, Rout, and destroyed value (§2.1, GameDesign §4.5).
- Draftable hybrids (Half-Elf and similar) are out for now.

**Information.** After both drafts lock in, each player sees the other's races and unit count per race (GameDesign §4.4).

**Balance target** (Simulation §7): mono-race and mixed armies are both viable. Two-race armies should be the most common winning shape, mono-race close behind, and "each race's best units" the weakest. Tournaments should report win rate by army shape (mono, two-race, three or more) as well as per unit.

## 3. Elves

**Few, fragile, ranged units that own the forest.** Elves want to shoot from cover, then fall back before the goblins close the gap. They lose any fight they're forced into up close.

### 3.1 Race Traits

| Trait | Effect | MVP |
|---|---|---|
| **Forestwalk** | Forest costs 1 Movement for elves (instead of 2). | ✅ |
| **Canopy Sight** | Forest tiles don't block line of sight for elves' ranged attacks. Elves can shoot into, out of, and through forests; enemies can't shoot through forest at them. | ✅ |
| **From the Trees** | Reserves may also arrive on any forest tile with no enemy within 2 tiles (GameDesign §4.4). | ✅ |
| **Sylvan Veil** | An elf in forest is hidden from the enemy unless an enemy unit is adjacent to it, or it attacked this turn. | Later (needs per-player views / fog of war, GameDesign §7) |

Canopy Sight is the MVP stand-in for invisibility: it gives elves a forest advantage the player can see, without hidden information.

Elves also get the general ranged rules from GameDesign §4.3: half Attack at point blank (min range 1), or no melee attack at all (min range 2).

### 3.2 Units

| Unit | Role | Cost | HP | Atk | Def | Mv | Range | Init | Vision | Traits / abilities |
|---|---|---|---|---|---|---|---|---|---|---|
| Archer | Main damage | 5 | 7 | 4 | 1 | 4 | 2–3 | 5 | 5 | — |
| Ranger | Movement control | 6 | 7 | 3 | 1 | 5 | 1–3 | 6 | 5 | Pinning Shot |
| Herbalist | Support, healer | 4 | 6 | 1 | 0 | 4 | 1–2 | 4 | 4 | Mend |
| Scout | Recon, blocker | 2 | 3 | 1 | 0 | 5 | 1 | 7 | 7 | Forest Stride, Slippery |
| Druid | Mage | 6 | 6 | 2 | 0 | 4 | 1–2 | 4 | 4 | Unique; Overgrowth, Entangle |

**Archer.** The backbone. Longest range, but it can't attack adjacent units at all (min range 2). It lost Braced on 2026-09-27 (GameDesign §4.1): a held Archer line that always shot first made attacking pointless. Archers now rely on range, forest cover, and screens.

**Ranger.** The movement-limiting specialist from the brainstorm. Shorter range, faster, and it can defend itself up close (at half Attack).

- **Pinning Shot** (ability, Cooldown 2): a normal ranged attack that also **Roots** the target for 1 turn. This is the elves' main tool for stalling a rush: the rooted goblin can't close the gap in the next movement phase.

**Herbalist.** Weak in a fight, and the only elf healing.

- **Mend** (ability, range 1–2): heal another friendly unit (not the Herbalist itself) for 3 HP and remove Slowed and Rooted from it. This is the answer to the Goblin Mauler. It heals **Elves only** (§2.4, rules 0.6.0).

**Scout.** Ridiculously frail; it exists to move.

- **Forest Stride:** moving from one forest tile to an *orthogonally adjacent* forest tile costs 0 Movement. A Scout can cross any connected forest in one turn.
- **Slippery:** when the Scout would clash, it always retreats: it stays on the tile it came from instead of fighting. The enemy takes the contested tile without a fight. (Automatic, so resolution never pauses for a decision.)
- Vision matters once fog of war arrives. In the MVP, the Scout's job is blocking paths, spending enemy attacks, and baiting clashes.

**Druid** (mage). Reshapes the map in the elves' favor.

- **Overgrowth** (ability, Cooldown 3, range 1–3): turn a Plains, Road, or Hills tile into Forest. This works on an occupied tile, including one an enemy stands on.
- **Entangle** (ability, Cooldown 4): Root every enemy unit standing in Forest within 2 tiles of the Druid for 1 turn.
- Combo: Overgrowth under a goblin one turn, then Entangle it (and anyone else in the woods) the next.

### 3.3 Later Candidates

- **Bladedancer:** the one elf melee unit, fast and fragile. Answers "are elves ranged only?" with "almost".
- **Treant** (summoned by the Druid): a slow wall that blocks a lane.

## 4. Goblins

**Many cheap melee units that heal by fighting.** Goblins want to close the gap, surround a target for Support (GameDesign §4.3), and trade. Their losses are cheap, and every hit that lands patches them up.

### 4.1 Race Traits

| Trait | Effect | MVP |
|---|---|---|
| **Bloodthirst N** | After this unit's attack deals damage (not in clashes, which are pure fighting), it heals N HP, up to its max HP. Most goblins have Bloodthirst 1; the number varies by unit. | ✅ |
| **Mountain-born** | Mountains cost 2 Movement for goblins (instead of 3). | ✅ |
| **Horde** | Goblins are cheap: about two goblins per elf in points. There's no rule for this; it's reflected in costs. | ✅ |
| **Out of the Caves** | Deploying a reserve of Cost 3 or more costs 1 less Command. Reserves may also arrive on mountain tiles in or next to the deploy zone (GameDesign §4.4). | ✅ |

Mountains give +2 Defense and block line of sight (GameDesign §4.3, §5). Goblins in the mountains are hard to shoot and hard to hurt, which mirrors elves in the forest.

### 4.2 Units

| Unit | Role | Cost | HP | Atk | Def | Mv | Range | Init | Vision | Traits / abilities |
|---|---|---|---|---|---|---|---|---|---|---|
| Grunt | Basic | 2 | 5 | 3 | 0 | 4 | 1 | 3 | 3 | Bloodthirst 1 |
| Rusher | Fast flanker | 3 | 4 | 3 | 0 | 6 | 1 | 6 | 4 | Bloodthirst 1, Reckless |
| Wolf Rider | Fast raider | 4 | 7 | 4 | 0 | 7 | 1 | 6 | 5 | Bloodthirst 1, Reckless |
| Bruiser | Heavy hitter | 4 | 7 | 5 | 0 | 4 | 1 | 3 | 3 | Crush (no Bloodthirst) |
| Tank | Frontline | 4 | 8 | 2 | 2 | 3 | 1 | 2 | 3 | Bloodthirst 3, Retaliate, Braced |
| Mauler | Movement control | 4 | 6 | 3 | 1 | 4 | 1 | 4 | 3 | Bloodthirst 1, Hamstring, Throw Net |
| War Lord | Leader | 6 | 8 | 4 | 1 | 4 | 1 | 4 | 4 | Unique; Bloodthirst 1, War Cry |
| Shaman | Mage | 5 | 5 | 2 | 0 | 4 | 1–2 | 3 | 4 | Unique; Call the Horde |

**Grunt.** The basic goblin, and the unit the Shaman summons. Alone it barely scratches an Archer in forest (3 − 2 = 1 damage); with two allies adjacent to the target it hits for 3.

**Rusher.** Very high movement and initiative. It's the unit that reaches the elf backline and the one best at forcing clashes.

- **Reckless:** +1 Attack in clashes.

**Wolf Rider.** A heavier, faster Rusher that closes the gap before an Archer line gets its second volley. It has 7 HP, so it survives an Archer shot (4 damage) and reaches melee. It's the Goblins' answer to elves kiting from range. Added 2026-09-28 after bot tournaments showed Elves winning 78% of Captain mirrors; with it, Elves win 48% (Simulation §10). The original candidate traded Bloodthirst for Movement; the tested version keeps Bloodthirst 1 and Reckless.

**Bruiser.** Big damage, no healing.

- **Crush:** ignores the target's terrain Defense bonus. A negative terrain Defense (swamp) still applies. This is the answer to elves holding forest (and later dwarves in the mountains).

**Tank.** Low damage, heals huge amounts, and punishes anyone who hits it in melee.

- Bloodthirst 3 heals 3 HP whenever its attack deals any damage, even the minimum 1.
- Retaliate makes it a bad target for other melee units. Elves can shoot it freely, but that spends shots that aren't going into the rest of the swarm.
- Braced: a held Tank that an enemy charges into strikes first (initiative 2 → 5), which makes it the Goblins' anchor on an objective tile.

**Mauler** (name settled 2026-09-28). Stops elves from kiting.

- **Hamstring:** its attacks (not clash strikes) also apply **Slowed** for 1 turn.
- **Throw Net** (ability, Cooldown 3, range 2–3): deals no damage and **Roots** the target for 1 turn. Needs line of sight.

**War Lord.** Makes the swarm sturdier.

- **War Cry** (Aura, range 2): other goblins get +1 Bloodthirst. A Grunt heals 2 per hit; a Tank heals 4. A Bruiser gets Bloodthirst 1.

**Shaman** (mage). Refills the horde.

- **Call the Horde** (ability, Cooldown 3): summon two Grunts on empty tiles adjacent to the Shaman. At most 4 summoned Grunts can be alive at once. If only one tile is free, only one Grunt appears.

### 4.3 Later Candidates

- **Sapper:** sets traps (GameDesign §7), or blows up after a countdown (possibly shared with the Demon Explosive Servant).

## 5. Other Races (Stubs)

Enough to keep the identities distinct. Units are from the brainstorm and aren't statted yet.

### 5.1 Humans

- **Identity:** well-rounded and disciplined, with some light magic. The baseline race other races are measured against.
- **Race mechanic (draft):** reshape land with Workers; healing.
- **Mage:** **Cleric**: heal and "light" magic (reveal hidden units? blind?).
- **Units:** Footman (basic), Arbalist (ranged, has to reload), Defender (high HP/armor, low damage; Braced, Retaliate?), Alchemist (weak healer; removes status effects), Worker (turns tiles into Plains over several turns: forest 3, mountain 7).
- **Open:** Holy/Light ideas (paladins, smiting undead and demons) would go here.

### 5.2 Dwarves

- **Identity:** defensive, with an offensive siege style.
- **Race mechanic (draft):** **Fortify**: +1 Defense for each consecutive turn held, up to a cap. Tunnels only dwarves can use.
- **Mage:** **Runesmith**: create or remove a mountain, or turn one into a defensive structure or tunnel.
- **Units:** Axeman (basic), Craftsman (buffs Attack or Defense temporarily), Twin Lords (two units that are strongest next to each other; line damage between them, can jump to or toss each other), King (Unique).

### 5.3 Undead

- **Identity:** attrition. Killing an undead unit isn't the end of it.
- **Race mechanic (draft):** dead Undead units return after N turns at a graveyard or where they fell.
- **Template** (decided 2026-09-28): Undead is also something that happens to other races' units. A raised unit becomes "Undead *X*", keeps its original race and traits, gains the Undead tag and traits, and is Summoned (can't act on arrival, worth 0). See §2.4.
- **Mage:** **Necromancer**: raises fallen units through the template. The earlier curse version still works as the trigger: curse an enemy or ally, and if the cursed unit dies while the curse lasts, it comes back as an Undead version under the Necromancer's control. Enemies may prefer to leave a cursed unit alive for a while.
- **Units (draft):** a small roster: Skeleton (basic), Zombie (slow, tough; moved over from the Demons brainstorm), Necromancer (Mage, Unique).
- **Open:** the Undead race traits. They stack in full on raised hybrids, so keep them modest, or limit raising (Cooldown, a cap on raised units alive, raised at reduced HP). Can raising target enemy corpses? Do raised units return through the race mechanic?

### 5.4 Merfolk

- **Identity:** water control.
- **Race mechanic (draft):** all units move through water. Out of water, a unit weakens (or can't attack) unless it has touched water in the last N turns. Can't enter desert.
- **Mage:** **Tidecaller**: turn a nearby tile into water.
- **Units:** TBD.

### 5.5 Demons

- **Identity:** very expensive, powerful units, paid for with sacrifices.
- **Race mechanic (draft):** sacrifice your own units to reduce costs or grant buffs.
- **Mage:** very low chance to mind control an enemy unit (temporary or permanent). This is one of the few places randomness is allowed (GameDesign §4.3).
- **Units:** Cultist (cheap sacrifice fodder), Legion (splits when sacrificed into), Explosive Servant (suicide unit on a countdown), Lord of Death (grows stronger from each sacrifice).

### 5.6 Wizards (dissolved)

No longer a race (decided 2026-09-28). The **Mage** class (§2.4) covers "technical spellcasters", so these ideas are a bank for other races' Mage-class units:

- **Grand Wizard:** elemental spells (time, fire, lightning in a piercing line, ice, earth).
- **Loyalist:** buffs mages (a natural class-targeting aura: "Mages within N").
- **Novice:** Grand Wizard damage, but hurts itself.
- **Apprentice:** a subset of Grand Wizard spells at lower damage.
- **Battle Mage:** melee-range AoE.
- **Open:** which races get them (Humans are the likely home).

## 6. Matchup Sanity Checks (Elves vs Goblins)

Quick math with the damage formula, `damage = max(1, Attack + Support − (Defense + terrain Defense))`:

| Attack | Damage | Notes |
|---|---|---|
| Archer → Grunt on plains | 4 | Grunt survives at 1; two shots to kill. |
| Archer → Grunt on mountains | 2 | Mountains block line of sight anyway unless the grunt is in the open. |
| Grunt → Archer on plains | 2 | 4 hits alone. |
| Grunt → Archer in forest | 1 | Alone it's almost useless… |
| Grunt → Archer in forest, 2 allies adjacent | 3 | …surrounded, it hits hard and heals 1. |
| Bruiser → Archer in forest | 4 | Crush ignores the forest. |
| Tank → Archer on plains | 1 | Heals 3 anyway. |
| Archer → Tank on plains | 2 | 4 shots to kill a Tank that isn't healing. |

**Example drafts (40 points, 30 on the field at the start, GameDesign §4.4):**

- Elves, starting army: Druid, 2 Archers, Ranger, Herbalist, 2 Scouts (6 + 10 + 6 + 4 + 4 = 30): 7 units. Reserve: Ranger, Herbalist (6 + 4 = 10).
- Goblins, starting army: Shaman, War Lord, Tank, Mauler, Rusher, 4 Grunts (5 + 6 + 4 + 4 + 3 + 8 = 30): 9 units, plus up to 4 summoned. Reserve: Bruiser, Rusher, Grunt (4 + 3 + 2 = 9) or 5 Grunts (10). With Out of the Caves, the Bruiser + Rusher + Grunt reserve costs 7 Command to deploy; Grunts get no discount.
- Mixed (open draft, §2.4), starting army: 3 Archers, Tank, 2 Grunts, Herbalist, Scout (15 + 4 + 4 + 4 + 2 = 29): 8 units. Reserve: Wolf Rider, Archer (4 + 5 = 9). Goblins screen the Archers, but the Herbalist can't Mend them and there's no War Cry to boost two Grunts. The Wolf Rider deploys at 3 Command.

## 7. Open Questions

1. **Elf HP.** GameDesign §4.3 proposes elves at 6–10 HP. The Scout at 3 is a deliberate exception. Is 7 enough for Archers to survive one goblin wave?
2. ~~**Bloodthirst in clashes.**~~ Decided 2026-09-27: clashes are pure fighting, so Bloodthirst doesn't heal in them (GameDesign §4.3).
3. **Horde Support.** Should goblins get a higher Support cap (+3) as a race trait, or is Support + Bloodthirst enough?
4. **Pinning Shot vs Throw Net.** Both sides have a Root on a cooldown. Is that too symmetrical? An alternative is to make Throw Net an area Slow instead.
5. **Summon limits.** Is the 4-Grunt cap and Cooldown 3 enough to stop the Shaman from turning into an endless stall?
6. **Druid Overgrowth under enemies.** Should turning an enemy's tile into forest be allowed, given the Entangle combo and the +1 Defense it gives *them*?
7. ~~**Leaders.**~~ Deathmatch has no leader unit (GameDesign §4.5). Revisit only if a later mode needs one.
8. **Mauler name:** Mauler, Hobbler, Netter, or Snarer?
9. ~~**Out of the Caves discount.**~~ Decided 2026-09-27: it applies only to units of Cost 3 or more, so Grunts no longer flood in at 1 Command each.
10. **Open draft: race auras.** Elves have no same-race aura yet, only Mend. What rewards a mono-Elf or Elf-heavy army on the board? Goblins already have War Cry.
11. **Open draft: classes.** Are classes only tags for targeting and draft reveal, or do some get their own synergies? Which Elf/Goblin units beyond those in §2.4 get classes (Herbalist, Scout, Mauler)?
12. **Open draft: budget.** Does the 40 / 30 draft budget still fit when armies can mix cheap Goblins with elite Elves?
13. **Open draft: raised hybrids.** See §5.3: Undead traits, raising enemies, and whether raised units return.
