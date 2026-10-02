# Builders and thieves: port design

How Direction D's species map onto the game's real code paths. Every number here is measured in
`Tools/Ecology/builders/` (sim at 10 Hz) unless it says "estimate". Nothing here has been run in Unity.

## 0. Why stealing is cheap in colliders

A builder colony creates **no prisms**. Every brick it lays, every prism a wearer wears, already existed: a
pilot's trail prism or environment mass, with its own collider, its own spatial-index entry, its own companion
render entity. Stealing changes three things on an existing prism: its domain (one `ChangeTeam` material swap),
its position (only while it is being carried), and its owner registry. **Net collider impact of the structures: 0.**
The only new colliders are the workers' hearts (one elemental crystal per lifeform - the colony's members ARE the
lifeforms, CLAUDE.md "a connected COLONY is a population"), which is the colony's size.

| species | new always-on colliders | prisms moving at once (steady state) | position writes / s (sim) |
|---|---|---|---|
| fortress / nest (48 workers) | 48 hearts | <= 48 (carriers) | 90-150 |
| wasp comb (48) | 48 | <= 48 | 75-90 |
| trap builders (30, tuned) | 30 | <= 30, parked carriers 0 | 25-65 |
| wearers (40 hearts, 1-2 creatures of 240-400 prisms) | 40 | the whole body | 1,900-3,100 prism writes; 31-44 container writes; 1,700-2,800 index re-buckets |

The builders are cheap because **a built structure never moves**. The wearers are expensive because a body is
moving mass by definition - see §4.

## 1. The shared colony (all four species)

A `BuilderColony` is one Fauna-like owner of N worker agents in struct-of-arrays (the round-7 `SwarmSortCore`
shape: Burst sim, GPU-instanced members, no GameObject per worker), plus:

- **forage**: on a fractional schedule (1 in 4 workers per frame, as in the sim), `PrismSpatialIndex.QuerySphere(pos,
  sense, scratch)` on the main thread, filtered by ONE predicate `Fauna.IsStealableForMe(prism)`:
  `!prism.destroyed && !Fauna.IsShieldedMass(prism) && prism.Domain != colonyDomain && !BuilderRegistry.IsBuilt(prism)`
  plus the creature's band (`IsInsideBand`). Shielded and super-shielded mass is never a target - the same rule as
  the herbivore diet, for the same reason (a target you cannot take is a feed-hold you never finish).
- **steal**: `prism.Steal(colonyName, colonyDomain, superSteal: false)` ON PICKUP. The predicate has already
  excluded shields, so the `!superSteal && IsShielded -> DeactivateShields` branch is never reached; a creature
  never strips armour. The steal raises `onPrismStolen` with `OwnName = colonyName`, which no roster contains, so it
  scores for nobody - exactly the environment-mass convention.
- **carry**: the prism is live gameplay data, outside the clock-material law (`Docs/PRISM_ANIMATION.md §1`): each
  carrier writes its prism's transform and calls `Prism.NotifyPositionChanged()` (the mover contract - spatial index
  + shell + render entity), as `WallAssembler.MoveMateToSite` and fauna bodies do. Keep the collider ON while carried:
  a pilot who rams a carrier knocks the prism loose (and kills the worker - a heart drop).
- **deposit**: `PrismSpatialIndex.TryReserve(site, clearRadius)` (claim-before-place, as `WallAssembler.GetGrowthInfo`
  already does), then the prism's FINAL pose is written at once (collider, index and volume final at the start), and
  the short settle from the carrier to the site is a **clock stamp** (a straight-line flight from the drop point; the
  endpoints are known, so it is animation, not live data). Register in a `BuilderRegistry` keyed by
  `(colonyId, Vector3Int site)` - integer addressing, like `SchwarzPTileRegistry` / `GyroidOctagonRegistry`.
- **fields**: per colony, small dense NativeArrays aligned to its lattice (cement, alarm, Q template: 61^3 floats =
  0.9 MB each - cut to 33^3 = 0.14 MB for a 40 u nest) diffused by a Burst job every 3-5 ticks. The trap colony's lane
  and flow fields are per CELL (60^3 at 40 u) and are fed by trail prism CREATION (the prism's own rotation is the
  heading), not by watching vessels.
- **upkeep**: a destroyed built prism (`Prism.MarkDestroyed` path, or a `BuilderRegistry` sweep of `destroyed` flags on
  the colony's tick) frees its site and deposits alarm.

## 2. Best builder to port first: the FORTRESS (with the nest as its peaceful phase)

Why this one first:
- it is the emergent behaviour a player can WATCH and understand in seconds: cut the wall, the swarm converges on the
  wound, the wall knits shut (t50 6.4 s with alarm + gap rules, vs 39.5 s without);
- it is the cheapest: a static shell plus <= N carriers;
- it is closest to shipped code (`WallAssembler` already finds prisms through `QuerySphere`, moves them with
  `NotifyPositionChanged` and calls `Steal` on snap);
- its counterplay closes the loop with the player's own trail: 98% of the material that closes a wound is the cutting
  pilot's trail. Flying a shorter, tighter attack run (or not laying trail near it) starves the repair.

Rules to port, verbatim from `builders/nest.py` + `fortress.py`:
1. template `Q(r) = exp(-((r - Rc) / w)^2)` around the core (Rc 40, w 7 for a fortress; Rc 44, w 30 for a nest);
2. deposit `p = Q * (0.05 + C^2 / (C^2 + k^2))` on a free site touching the structure, `Q * nucleate` otherwise;
3. gap boost `p *= 1 + 2.5 * max(0, nb26 - 4) / 6`;
4. alarm: a freed site gets +1; alarm diffuses (0.5 self / 0.5 neighbours) and decays 5% every 3 ticks; a laden worker
   that smells alarm > 0.02 climbs its gradient;
5. defence escalation (`Colony.defend`): alarm level +0.6/s while a pilot is inside the alarm radius; guard screen below
   0.6, strike at 0.6. Telegraph measured 0.5 s; raider hits 1.0-1.7/min; ~4 crystals/min for a raid.
6. brood: one crystal-store unit per 20 prisms woven (the flora `GrowthPerOffspring` currency); a raider reaching the
   core collects the store.

Ecology invariants it touches, all held: mass conserved (audit 0 every run; stealing changes hands, only a vessel
ability removes a built prism); no imposed death (workers die only to pilots or starvation); continuity of existence
(a stolen prism never pops - it travels, and its colour changes on the material lerp); one spawn colour (the colony
builds in the cell's controlling domain); shielded mass never taken (test_rules.py, negative control caught).

**Collider budget:** +N heart crystals (N = 24-48 recommended), 0 prisms.

## 3. Trap builders - a mode-scoped threat

Lane learning is the strongest effect measured: the colony makes a fixed racing line about as dangerous as you like
(4-38 hits/min by dose) while a pilot who varies the line takes ~0. That makes it a natural fit for the GATE RACE
modes (Headlong, Redline, Switchback, Breakwater), where a course forces a racing line: the trap colony would learn the
line players actually fly and string danger webs across it, so the best line moves during a race. In game: the
lane field is fed from trail prism creation in the cell; every placed prism calls `Prism.MakeDangerous()` (as
`WallAssembler` does to its bottom mates); a vessel touching it takes the existing danger-prism effects, and the
collision springs it (whatever the vessel's prism effects already do). `lane_min` is the design dial: "how many laps
before the web goes up".

## 4. Wearers - the showpiece, and what its body costs

A wearer body is a moving prism structure, like an Ark hull or a fauna body. Measured for one 300-prism body:
~31-44 container writes/s (one per creature that moved this step) versus 1,900-3,100 per-prism writes/s, and
1,700-2,800 spatial-index re-buckets/s (88% of moves cross an 8 u bucket at 10 Hz; at 60 fps an estimate of
N * |v| * 1.5 / 8 ~ 3,400/s for N 300 at 60 u/s).

Port shape:
- worn prisms are parented under ONE creature transform (`HealthPrism.Reparent`-style; the stolen prism is a plain
  `Prism`, so a generic reparent is needed) - transform writes collapse to one per creature;
- the mover contract still owes each prism a `NotifyPositionChanged` per frame (Ark and Fauna precedent). This is
  the cost. Two levers, both measured or bounded here:
  - **a body cap** B (e.g. 150 prisms): at cap the creature stops stealing (production gating - permitted, it is not
    a cull). Cost then scales with B * creatures;
  - **a fractional notify** (1 in k body prisms per frame). The index position lags by up to k frames of body motion
    (k = 4 at 60 u/s = 4 u, inside one bucket) - but a LUNGE at ~200 u/s lags 13 u: notify every prism during the
    rear/lunge phases only.
- contact: the strike tests the body prisms themselves (a long tail sweeps), so the creature's real silhouette is
  the hitbox; colliders are the prisms' own (0 new). Stripping = `prism.Steal(pilotName, pilotDomain)` + un-parent:
  the prism falls loose in the pilot's domain, mass conserved.

**Collider budget:** +N hearts (40 measured), 0 prisms. **CPU:** the body-notify cost above, bounded by the cap.
