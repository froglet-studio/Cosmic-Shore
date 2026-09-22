# Broadside — the arena brawl

`GameModes.Broadside = 57` · `MinigameBroadside.unity` · `BroadsideController`

## Overview

Regatta asked what every hull does with a racing line. **Broadside asks what every hull does with
a rival in front of it.** Seven hulls loose in Dog Fight's Boneyard, each fighting with the weapon
it actually has — a Sparrow's guns and rockets, an Urchin's chain spikes, a Rhino's energised
sword, a Squirrel's joust, a Dolphin's cone, a Scarab's plate, a Manta's bloom — and the first
**DOMAIN** to the point target wins on `ScoringMetric.CombatPoints`.

**A hit is priced by its VERB, never by its hull.** That is the whole design, and it is what lets
a mixed fleet share one score:

| verb | who lands it | price | why |
|---|---|---|---|
| **Round** (`Bullet`) | Sparrow tracer, Urchin spike | **1** | landed from range, and it repeats |
| **Strike** (`Strike`, new) | Rhino sword, Squirrel joust | **8** | you had to fly into them, inside their reach |
| **Debuff** (`Debuff`) | Dolphin cone, Scarab plate, Manta bloom | **12** | the only verb that leaves its victim measurably worse |
| **Rocket** (three tiers) | Sparrow skyburst | **10 / 20 / 30** | Dog Fight's prices — it is the same bay |

No hull is named anywhere in the scoring path, so the card can gain a hull without the rule
learning anything.

## What it had to fix before a brawl was possible

**Measured on the shipped containers, only four of the eight playable hulls could land a scoreable
hit** (Sparrow, Manta, Dolphin, Scarab). Everything below is counted platform-wide and **paid only
here** — the split Dog Fight established.

1. **`CombatHitClass.Strike`** — a contact hit, the fourth verb, unranked like `Bullet` and
   `Debuff`. It is a **verb, not a hull**: the Rhino's sword and the Squirrel's joust share it
   because both answer *"I flew into them."* Adding it is provably inert everywhere else — the
   base `PointsForCombatHit` returns 0, Dog Fight's switch is exhaustive with `_ => 0`, and
   Bends/Undertow's `? a : b` both default to `gunneryPoints`, authored 0.
2. **`VesselCombatHitBySkimmerEffectSO`** — the skimmer sibling of the projectile and explosion
   reporters. It had to solve **authority** differently: a projectile is a pooled local object and
   may raise unconditionally, but a skimmer overlap is observed on **every peer**, so an
   unconditional raise double-credits (the server records its own view *and* the shooter's client
   forwards the same contact, and the latch is per-machine). It gates on the machine that owns the
   striker — `IsNetworkOwner`, not `IsLocalUser`, or every AI goes un-scored.
3. **The Urchin's spike container had `projectileShipEffects: []`** — a spike passed straight
   through a rival pilot and did nothing at all. The same empty-container gap Dog Fight found in
   the skyburst and The Bends found in the Dolphin's cone, a third time. It gains the report **and
   a spin**, because a score with no felt cause on screen is the complaint every unwired weapon in
   this family has produced.
4. **`CombatHitScoring`'s raw tally lost its else-arm**, which had been filing a Rhino's **sword**
   as a *bullet* — a raw count, on a hull with no gun, on the scoreboard breakdown. A new
   `IRoundStats.StrikeHitsLanded` carries it. *An else-arm in a tally is the same trap the enum's
   own doc already records for pricing.*

**The Serpent is deliberately not on the card.** It has no anti-vessel verb authored at all (0/4
abilities). Listing it would seat a pilot who cannot score; giving it one is a `/vessel` job.

## The fleet table

Measured off the shipped prefabs and containers, 2026-09-16.

| hull | verb | wired before? | AI can fight? |
|---|---|---|---|
| Sparrow | guns + skyburst | ✅ 4 classes | yes — fires on its own timer once someone is ahead |
| Manta | Sting bomb → bloom | ✅ CrystalBlast | yes — planting is grazing |
| Dolphin | crystal cone | ✅ CrystalBlast | **opportunistically** — the cone needs a crystal |
| Scarab | cavitation plate | ✅ Cavitation | yes — `TryAutopilotDash` |
| Squirrel | joust | ❌ paid `Jousts` only | yes — the joust is landed by arriving |
| Rhino | energised sword | ❌ damaged + spun, reported nothing | yes — same |
| Urchin | chain spikes | ❌ **container empty** | yes — a replicated trigger tap |
| ~~Serpent~~ | — | — | **not on the card** |

## Balance

`Tools/Build/broadside_balance.py` is the offline authority; the generator **imports** it rather
than transcribing it, and asserts its claims.

**The insight the model is built on:** a weapon's fire rate is almost never what bounds its
scoring. `VesselCombatHitLatch` admits one hit per (shooter, victim, class) per that effect's own
`sameVictimCooldownSeconds`, so against one rival a hull's ceiling is
`points_per_hit / latch_window`. The Sparrow's 90-rounds-per-second full-auto is capped by a
**0.05 s** window long before its trigger is.

That makes **the windows the real dial**, and they are authored **per asset**, which is the mode's
finest lever — two hulls sharing the `Strike` class are still separated by how often the class
admits, and the separation is grounded in the mechanism:

- **Sword 1.4 s** — a swung blade is in contact *continuously* while the Rhino is alongside, so
  without the longer window it would score every second of a pass it never had to re-earn.
- **Joust 1.0 s** — a joust is *discrete*: it requires a fresh overtake at a closing speed the
  Squirrel has to win, so it re-earns its window and is allowed a shorter one.
- **Spike 0.12 s** — a volley is ~10 projectiles inside ~0.1 s; the Sparrow's 0.05 s would let one
  volley score ten times.

**Result: 1.91× spread at rest → 1.35× tuned.** That is far tighter than Regatta's 6.0× residual,
and the reason generalises:

> **A brawl is more balanceable than a race, because the mode owns the windows.** A race's rate is
> bounded by vessel speed, which a card cannot touch; a brawl's is bounded by latch windows the
> mode itself authors.

**Stated honestly — what the model cannot do.** The per-hull **connect fraction** (how much of an
engagement a hull spends with its weapon on a rival) is an **estimate, not a measurement**. It is
the one term no static read can supply, and it is where the model is weakest; each value carries
its reasoning in the source so a playtest can correct it by argument. **Time reaches nothing on
the Urchin or the Squirrel**, so those two get no handicap row — the residual is structural,
reported rather than faked. *(An earlier version of this line named the Rhino too and was simply
wrong — see "What the first playtest changed" below.)*

The model's **minutes are a floor, not a prediction**, and the first playtest is what established
that. Its points/min is a rate against a victim you are **already engaged with**; it does not
model the time a brawl spends searching and repositioning between engagements, which is exactly
what the connect fraction was meant to absorb and evidently does not absorb enough of. So the
generator's duration assert is a **ceiling only** — *if even at full engagement a hull cannot
reach the target, the target is unreachable* — and the two-sided 3.6–4.8 minute claim this
document used to make is withdrawn rather than restated at new numbers.

## What the first playtest changed

Three corrections, and the third is the one worth carrying past this mode.

**1. The target scales with team size.** It was a flat 600 and is now **100 per pilot**, resolved
as `perPilot × (1 + 0.6 × (teamSize − 1))` → **100 / 160 / 220 / 280** for a 1 / 2 / 3 / 4 pilot
team, in `EndConditionOverridesSO.GetBroadsidePointTarget`. The reason is the latch: it admits one
hit per *(shooter, victim, class)* window, so two pilots working the same victim **both** score
and a second pilot roughly doubles a domain's rate — a flat total would make a 4v4 about a quarter
the length of a 1v1. The fraction is deliberately **below 1** so a fuller side still finishes
sooner; filling your team is a real advantage rather than a flat trade. It is resolved on the
**server** and replicated by `CombatPointTurnMonitorBase`'s existing NetworkVariable, so a client
that has not finished building its roster cannot compute a different target — *it never computes
one*. Team size is the **mean** pilots per domain that actually fielded someone, because the
target is one number every domain races to: a lopsided 2v1 would otherwise either hand the pair a
free win or ask the lone pilot for a total they cannot reach.

Re-targeting also produced the **eighth** outing of the comeback trap this family records
(`bonusLevels = deficit × rate`): at 600 the rate 0.02 bought ~3 element levels at a
quarter-of-target deficit, and at 100 it silently became **half a level**. It is now **0.12**,
sized against the *solo* target — the smallest the team-size rule can produce — so a fuller lobby
only ever buys more.

**2. The AI draws a random hull from the card.** `PickAIVesselType` read the mode's roster through
`gameList`, a per-scene `[SerializeField]` that **MinigameBroadside, MinigameRegatta and
MinigameDogFight all leave null** — so the lookup fell straight through to a hardcoded
`VesselClassType.Sparrow` and both **arena** cards, whose whole premise is a mixed grid, fielded
eight identical hulls. It now reads `GameDataSO.AllowedVesselClasses`, which
`SyncFromArcadeGame` publishes at launch and `ResetRuntimeData()` deliberately does not clear, so
it survives the scene load and is already the authority every other server-side spawn check uses.
Hulls with no prefab are skipped rather than drawn-and-failed, so a roster may name a planned hull
without breaking the backfill. General rule: **a per-scene serialized reference is a per-scene
chance to forget** — when the same fact is already published on a shared runtime object, read it
there.

**3. The sword now requires being FASTER than its victim, and the Rhino got its Time slot.** This
is the correction the playtest was actually about — *"I played rhino and was not charging full
speed and straight to be a crazy fast and scary menace like he should have"* — and it had two
independent causes, one in the mode and one in the vessel.

The mode's half: `VesselCombatHitBySword` shipped `requireFasterThanVictim: 0`, on the reasoning
that *a sword connects on its own terms*. What that actually bought was a card paying the Rhino to
**park** — holding the blade alongside a rival paid 8 points every 1.4 s for nothing but
station-keeping, while charging paid 8 points and left you 1200 units away. The mode was rewarding
the exact opposite of the hull's identity. It is now `1`, the Squirrel joust's own rule. Requiring
speed does not make the Rhino fast; it makes being fast the only way it scores.

The vessel's half: **`RampBoostActionExecutor` has always read `Multiplier(Element.Time)`** and
scaled `accelerationPerSecond` by it — and `ElementalAbilityMaps/Rhino.asset`'s Time entry was an
`(open design slot)` authored `1.0 / 1.0`. **The hook was live and the asset behind it was flat**,
which is why three separate documents (this one, CLAUDE.md, and the balance model) recorded "Time
reaches nothing on the Rhino" as a measurement. It is now a real ability, **Ramp Spool** (2.5 /
0.5), and Broadside's card starts the Rhino at **+0.5**. This is a fleet change and was accepted
as one: Headlong's Rhino spools faster too. There is deliberately **no L5 upgrade** — the slot is
filled, not designed.

> **A capability that is live in code and flat in data reads exactly like a capability that does
> not exist**, and it will be written down as one. The Rhino's ramp took 5.2 s to climb from
> ~50 u/s cruise to ~1200 against a 300/s bleed — which a brawl's short straights simply do not
> contain — so at Time rest the hull was *structurally* never fast. Before recording "element X
> reaches nothing on hull Y", check the executor, not the map.

The model converts that endpoint honestly: Time is the ramp's **wind-up rate**, not its ceiling
(`maxBoostMultiplier` stays 24), so `rhino_speed_multiplier` integrates
`min(top, cruise + a·t)` over a 2-second brawl straight and takes the ratio of the means — **2.22×
at full**, not the 2.5× the acceleration row reads, and it **saturates** once the hull tops out
inside the straight. Feeding the raw acceleration ratio in overpays the hull badly. All three
constants (`cruise 50`, `top 1200`, `a 220/s`) are **read off `Rhino.prefab`** rather than copied
into the model, and that is not tidiness: the first cut hardcoded `60 / 1210` and went stale
inside the week, when a parallel branch zeroed the prefab's `DefaultMinimumSpeed` and moved both
numbers by 10 u/s. Nothing failed — the model simply went on describing a vessel the project no
longer ships. **A constant copied out of an asset is true on the day it is copied.**

That in turn exposed the level picker as a **coin toss with two faces**: it sorted the hulls around
the median and handed the lower half +1 and the upper half −0.5, so the moment the Rhino gained a
real endpoint it flipped from slowest scorer straight past every other hull to fastest, because
+1 was the only thing the bucket had to offer. `solve_levels` now moves each hull to the level
whose tuned rate is nearest the **anchor** — the median rate of the hulls Time *cannot* reach,
which is the part of the roster no handicap can move and therefore the only honest thing to
converge on. On scoring alone the solver puts the Rhino at **+0.4**; `TIME_FLOOR` clamps it UP
to the **playability floor** of **+0.5**, because its Time level is answering a playability
question, not a scoring one — the balance pass is allowed to raise that level and never to spend
it.

## The drain follows the price — TEN POINTS IS ONE PETAL

Broadside priced each verb by how hard it is to land. The **elemental drain** those same attacks
deliver was derived from nothing: every shipped drain asset carried a flat **−0.5** on every
element it touched, whatever the attack. In the units the player actually reads that is **five
petals** — a quarter of the whole `[−5, +15]` element band — from a single graze, and a
30-point centre-punch had no way to take more than a 10-point near miss.

**A hit's bite now tracks its price**, at one constant:

```
per-element magnitude = points / 10 petals = points × 0.01 normalized
```

A petal is one integer element level (the flower steps one colour per level), which is `0.1` in
the units `ResourceSystem.ApplyElementalEffect` takes — the same step `ResourceSystem.
IncrementLevel` is a bare `0.1f` for. So a direct rocket strike (30) drains **three petals** from
each element and ten bullets (1 each) drain **one**.

| attack | verb | pts | elems | per-element | petals | was | sustained¹ |
|---|---|---|---|---|---|---|---|
| Sparrow / Urchin round | Bullet | 1 | 4 | **−0.0100** | 0.1 | *none* | −0.40 |
| Squirrel joust overtake *(ally buff mirrors)* | Strike | 8 | 4 | **−0.0800** | 0.8 | −0.3333 | −0.12 |
| Sparrow skyburst warhead shockwave | MissileShockwave | 10 | 4 | **−0.1000** | 1.0 | −0.4167 | −1.20² |
| Dolphin crystal cone + Scarab cavitation plate *(shared asset)* | Debuff | 12 | 4 | **−0.1200** | 1.2 | −0.5 | −0.24 |
| Manta Kabloom bloom *(Mass + Space)* | Debuff | 12 | 2 | **−0.1200** | 1.2 | −1.0 | −0.24 |
| Sparrow skyburst prism blast | MissileBlast | 20 | 4 | **−0.2000** | 2.0 | *none* | −1.20² |
| Sparrow skyburst direct strike | MissileDirect | 30 | 4 | **−0.3000** | 3.0 | *none* | −1.20² |

¹ normalized held on each element by a **saturating** attacker:
`|M| × duration / (2 × rate limit)`. Temporary effects **stack**
(`ResourceSystem.ApplyElementalEffect` adds an entry per call), so this — not the per-hit
magnitude — is what bounds a drain. The rate limit is the asset's own `cooldown` for a
per-weapon drain and the **latch window** for a reporter-driven one, since the latch is what
admits the hit the drain rides on: a saturating full-auto Sparrow holds **−0.400** on a victim
(0.05 s window) and a theoretical direct rocket hit every 0.5 s would hold **−1.200**. The
generator fails above **1.50** sustained and **0.50** per hit, and both ceilings are reachable
by a plausible edit — lengthen a decay, tighten a window, or reprice a verb past 50 points.

² the three missile tiers **share one latch key**, so they are priced as one family at the
dearest tier and the tightest window. It is a ceiling nobody can feed — a direct rocket
strike every half second means two rockets a second, and the bay holds four at 25 prisms
each — but it is the honest bound and it is what the gate is stated against.

### PER ELEMENT, not per total — and that is the half that changed

The table used to hold the TOTAL proportional to the price, so the Manta's two-element bloom bit
twice as deep per element as the Dolphin's four-element cone and the two landed the same sum.
That is a defensible rule and it is not the spec: *"three petals in each element"* is a statement
about **each** element, so how WIDE an attack spreads its drain is now part of what the attack
is rather than something normalised away. The Manta's bloom consequently bites the same per
element as the cone and half as much in total, which is what a two-element weapon should do.

### Two places a drain can live, and they must stay DISJOINT

**`CombatHitDrain` (C#) drains straight off the HIT REPORT** for every class whose bite is a pure
function of its price — Bullet and the three missile tiers. It has to be there rather than on a
per-blast asset, because **one rocket lands in three ranked classes against one victim** and a
per-asset drain would stack all three: six petals for a thirty-point event, and a base rocket
(no warhead) taking a different total from a heavy one for the same direct hit.
`VesselCombatHitLatch` already pays the best tier once and reports what an upgrade
**supersedes**; draining from that same seam, netted the same way, makes the bite and the score
agree by construction — the score credits the difference in points, the drain takes the
difference in petals.

**The per-weapon assets** carry the classes whose drain holds design a price cannot express:
*which* elements it touches (the Manta's bloom is Mass and Space only — the rule that bars an
overtaker from touching Time) and whether it mirrors as an ally **buff** (the Squirrel's
overtake). Those are Debuff and Strike.

A class drawing from both would bite twice for one hit, so `author_combat_debuff_magnitudes.py`
asserts the two sets are disjoint **and** that the price list `CombatHitDrain` restates matches
`BroadsideScoringRule.asset` — the drain is not a mode's business, so it restates the prices
rather than reading a mode asset at runtime, and the restatement cannot drift.

### The drain inherits the balance the price already has

The balance pass flattened *points per second* across seven hulls to a 1.33× spread by tuning the
latch windows. Drain-per-second is `hits/s × magnitude × duration/2`, and magnitude is `k ×
points`, so drain-per-second carries that same 1.33× spread for free. Tying the two together is
what makes the drain balanced without a second balance pass — and it is why the per-hit
magnitude, not the sustained pressure, is the right thing to make proportional.

### ⚠ The anchor MOVED, and this is the cost

The previous pass pinned the Debuff class to the shipped `−0.5 × 4` on the grounds that it was
play-tested, and the generator FAILED on drift. The rule above overrides that: 12 points is 1.2
petals, so `ScarabCavitationDebuffByExplosionEffect` — the asset the Dolphin's cone and the
Scarab's plate **share** — goes **−0.5 → −0.12**, a **4.2× lighter** bite. **The Bends and
Undertow are scored entirely on that drain**, so those two modes need a playtest. Their
*scoring* is untouched by construction: both pay for the hit **landing**
(`requireDebuffableVictim` asks whether the victim could be debuffed, not how deeply), so no
objective, target or comeback rate moves.

It is deliberate rather than an oversight. The old numbers were not derived from anything, and a
spec that prices a 30-point centre-punch at three petals cannot also price a 12-point graze at
five.

### Blast radius, stated

These are weapon properties, not mode opinions, so each change reaches every mode that hull flies.
Every armed drain gets **lighter**, which is the safe direction: the Manta's bloom softens 8.3×
in **Bloomrush** (which scores volume and fuses, not debuffs), the Squirrel's joust 4.2× in
**Joust** and **Brood Rush** (debuff and ally buff together — Joust scores the collision), the
warhead 4.2× in **Dog Fight, Salvo, Breakwater, Wildlife Liberation**. What gets *heavier* is
what previously did not exist: a round now bites 0.1 of a petal, and the skyburst's blast and
direct tiers bite 2 and 3 where they used to fold onto the shockwave's 1 and add nothing.

## AI

**Every AI hunts through the platform; only the trigger is per hull.** Broadside joins Joust and
Dog Fight in `ServerPlayerVesselInitializerWithAI`'s seek-players set, so every AI chases a chosen
opponent whatever hull it drew — which is right here, because most of this roster's weapons are
landed by **arriving**. Two hulls need a trigger pulled, because their weapon is a stick gesture
inert under autopilot (the Tollway rule — an all-AI domain that cannot play is a defect):

- **Scarab** — `ScarabJukeController.TryAutopilotDash`.
- **Urchin** — a short **tap** through `PerformShipControllerActionsReplicated`. It must be the
  replicated path: an AI runs server-only, so a local press would fire spikes on one machine and
  show them to nobody. A tap is the aimed shotgun; a long hold would charge the omnidirectional
  burst instead.

The scene's four AI templates are `vesselClass: 0` (Random), so `PickAIVesselType` draws each bot's
hull **from the card** — a bot grid is a mixed grid too.

## Arena, reused not forked

Dog Fight's **Boneyard**, referenced verbatim (the cell is per-arena, not per-mode — Salvo does the
same). It happens to feed every hull's economy, which is why a mixed fleet can live in it: a
Squirrel skims the wreckage for boost, an Urchin grinds it, a Dolphin skims it for seed energy, a
Sparrow's rounds pay ammo off it, a Rhino's sword has mass to cut, a Scarab forges balls from its
crystals. Its intensity ladder and its `noNucleusSpawnRadius` come with it.

## Assets

Everything is authored by `Tools/Build/author_broadside_assets.py` (`--check`). Never hand-edit a
generated asset; re-run the generator.

The fleet's **elemental drain magnitudes** are a second, separate generator —
`Tools/Build/author_combat_debuff_magnitudes.py` (`--check`) — because they are *fleet-wide weapon
properties* that happen to be derived from this mode's price list, not Broadside assets. It reads
`BroadsideScoringRule.asset` for the prices, each drain asset for its own element count, and
`CombatHitDrain.cs` for the price list the runtime restates and the set of classes it declines —
so neither side can drift from the other without the check naming the file and the field.

**Gotcha:** the generator `import`s the balance model, so a fast edit→run cycle can read a stale
`Tools/Build/__pycache__` copy. `rm -rf Tools/Build/__pycache__` if a mutation seems not to take.

## Verification status

**Authored headless. Nothing has been run in the Unity editor.** All eight offline gates pass
(`author_broadside_assets.py --check`, `author_combat_debuff_magnitudes.py --check`,
switch-label collisions, enum references, conditional compilation, self-referential locals,
console logging, using directives, gamelist scenes, `author_preview_spawns.py --check`), and both
generators' asserts were negative-controlled — the price-ordering, spread and time-to-target gates
were each watched to fail and restored, and so were the drain table's: a per-element ceiling,
a saturated-pressure ceiling, a renamed field in the price list, and — since the drain moved onto
the hit report — a price in `CombatHitDrain` disagreeing with the scoring rule and a class
covered by both sources at once. (The *moved anchor* assert is retired: the anchor moved
deliberately, and a check that pins a number the spec has since overridden is a check that
argues with the spec.)

One assert was **deleted** because its negative control came back green: *"a dearer hit must bite
harder"* is true **by construction** (`total = points × k` is monotone in points), so it could
never fire. A check nobody has watched fail is a check nobody should trust — the removal is noted
in the script so it is not re-added.

What stays editor-only: whether each hull's weapon actually reaches a rival at the ranges the model
assumed, whether the three newly-wired containers fire in play, whether the AI's spike tap reads as
a volley, and everything about feel.

## Known limitations

- **Connect fractions are estimates** (above). The first playtest should correct them.
- **The Dolphin is the weakest seat** at 4.8 min to target: its cone is gated on a crystal run
  rather than on the fight. Its Time row already buys it the maximum the platform allows.
- **No Serpent.**
- **The Rhino's sword is the one verb whose drain is still authored nowhere.** Every other
  scoring class now bites in proportion to its price; a Strike is covered by the per-weapon
  table, and the only Strike asset in the fleet is the Squirrel's overtake, which gates on being
  FASTER than its victim. The blade lands a Strike and drains nothing. Arming it is a Rhino kit
  decision (a skimmer drain SO the sword's container carries), not a number — see
  *The drain follows the price*.
- **The Debuff anchor moved 4.2× lighter** and The Bends / Undertow are scored entirely on that
  drain. Their scoring is unaffected; the FEEL is not, and it wants a playtest.
- The **milestone toasts and the objective arrow** are shared with Dog Fight's; the arrow
  deliberately does not try to name a weapon, because seven hulls answer *"what do I do when I get
  there"* differently.
