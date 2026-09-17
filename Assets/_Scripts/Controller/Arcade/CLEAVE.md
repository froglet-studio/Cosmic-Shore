# Cleave — Technical Documentation

> **Naming.** `GameModes.Cleave = 39` is the code/data/enum identity. The mode has been renamed
> TWICE — `Ribcage` → `PeelTheCage` (2026-09) → `Cleave` (immediately after, when three of its four
> intensities stopped being cages). Enum VALUES are pinned forever, so both historical names
> resolve to `Cleave` in ONE hop in `GameModeRenameMigration`; a chained map would strand the
> oldest saves on a name nothing reads. Asset GUID seeds inside
> `Tools/Build/author_cleave_assets.py` still say `Ribcage` **on purpose** — a seed is identity,
> not a name.

## Overview

Cleave is the **Rhino-only slicing race**. Domains race to be first to destroy their
intensity's **hostile-prism target** with the sword, and the arena *is* the score — cutting it apart and winning are the same
act.

**Intensity is WHICH PLACE you cut, not how much of it there is.** The four intensities are four
unrelated arenas with four different verbs, built by four different generators:

| i | arena | the verb | radius | prisms | volume | danger | far reach | target |
|---|---|---|---:|---:|---:|---:|---:|---:|
| 1 | **The Panes** | commit to a line | 2,160 | 15,380 | 38,522,229 | 511 | 2,184 | 1,200 |
| 2 | **The Swell** | follow the road | 2,160 | 14,277 | 103,293,190 | 399 | 1,997 | 1,200 |
| 3 | **The Cage** | peel inward | 720 | 14,731 | 23,357,561 | 428 | 749 | 1,500 |
| 4 | **The Twistbands** | roll the blade | 720 | 16,423 | 32,466,038 | 228 | 715 | 1,500 |

This replaced a ladder that was **2 / 3 / 4 / 5 nested shells** — the same arena four times, and
the reason the mode was renamed. The three-rind cage is the only rung kept, and its GEOMETRY is
unchanged: same generator, same seed, same 14,731 prisms, same count thresholds.

**Intensity is now SPACE as well as place.** Two scale-ups happened, and they answer different
complaints. The first made all four arenas **twice** the size they were authored at — 360 → 720 —
after a playtest read the arena as a little ball in the middle of the cell. The second, on
request, took rungs **1 and 2 three times further out again**: 720 → **2,160**, so the two easy
rungs are vast open places you cross rather than dense objects you peel. The Panes also went to
**triple** its rib spacing; the Swell was then re-authored outright (below). Their destruction
target is **1,200** against the dense rungs' 1,500, and they carry their own **3,600**-radius
membrane — the standard 1,200 one would be *inside* their arena.

**Rung 2 is a different arena from the one that first shipped here.** It was seven corrugated
wave SHEETS, and it read as the Panes with a ripple on it — the same proposition (angled surfaces
you cross) wearing a different texture. It is now five wide wavy ROADS: mass concentrated into a
handful of continuous surfaces you can FOLLOW instead of spread across seven you can only cross.
The verb moved with it, from *read the grain* to *follow the road*.

**Every rung is made of the same SMALL prisms — see "The prism dial" below.** That is the third
pass over this ladder and the one that decides how it LOOKS: prism size used to ride the envelope,
so the two 6× arenas were built from 102–132-unit slabs and read as low poly. Prism size is now
its own dial, pinned at 2 on all four rungs, so a Cleave prism is the same small piece whatever
size the place is. The two open rungs' counts tripled (5,107 → 15,380 and 4,607 → 14,277), their
volumes fell by ~9×, and their target went 400 → **1,200** to keep the same fraction of the arena.
Rungs 3 and 4 re-measured byte-for-byte identical, which is what makes one fleet-wide number
honest.

So the ladder reads as two pairs: open-and-far at 1–2, dense-and-tight at 3–4, all four made of
the same stuff. Every rung holds **7.4×–9.3×** its own target in hostile mass, which is what
`cleave_budget.py` asserts in place of the monotone prism-count check it retired — raw count
stopped meaning anything about match length the moment the target became per-intensity, and the
counts are deliberately NOT monotone.

> **Player-facing unit is PRISMS, never "bars" or "plates".** Every number a player reads counts
> PRISMS — the scoreboard, the reveal, and any toast copy. Two words for one counter reads as two
> counters.

**One axis.** Destruction is the race: `HostilePrismsDestroyed`, the same platform stat Rampage
runs on, at a PER-INTENSITY target (1,200 on the two open arenas, 1,500 on the two dense ones). Scoring mass is everything that is not your own team's laid
trail — the arena (environment mass, non-roster owner ⇒ hostile whatever colour it wears) and
rival trails. Your own and your teammates' trails never score, so there is no lay-and-smash
farming loop.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameCleave.unity`
- **GameMode enum**: `GameModes.Cleave = 39`
- **Controller**: `CleaveController : MultiplayerDomainGamesController` — structural sibling of
  `RampageController` (1 round / 1 turn, `HasEndGame=false`, server winner detection in
  `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`), plus progress milestones and the AI
- **Scoring**: `CleaveScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`; golf-timed)
- **Turn monitor**: `CleavePrismTurnMonitor` → `EndConditionOverridesSO.GetCleavePrismTarget(intensity)`
  (**1200 / 1200 / 1500 / 1500**, FrogletTools ▸ Game Modes ▸ End Game Conditions — never a
  per-scene field). Resolved SERVER-side and replicated through `_netPrismTarget`, so a client
  receives the number rather than deriving it from an intensity it may not have yet. A rung
  authored 0 falls back to the mode scalar
- **Domains**: `MinDomainsAllowed = 2`, `MaxDomainsAllowed = 3`; players **2–4** with AI backfill
- **Vessels**: **Rhino only**, enforced in two places (see "Vessel lock")

## The four arenas

Each is a `CellEnvironmentSpawnableBase` subclass under
`_Scripts/Controller/Environment/MiniGameObjects/`. They share nothing but the envelope and the
prism palette; each one exists to ask the sword a different question.

### 1 · The Panes — `SpawnablePanes`

Nine flat slabs cutting the cell at nine authored angles, each a **corduroy of parallel ribs** with
its own grain direction. Planks overlap along a rib (step **30** under length **34**) and ribs sit
**126** apart across it, so a pane is something you fly *through* as much as *at*. That ratio is
the corduroy: fine grain, wide gaps.

It is intensity 1 because a plane is the most forgiving surface in the mode. A shell curves away
from you — you cross it perpendicular and you are through, or you run along it and it bends out
from under the blade. A plane does neither: line up with it and the mass stays where you left it
for the full 1,440 units of its diameter. The pilot's first discovery is the one the whole mode is
built on — **a sword rewards commitment to a line**.

- **Panes are OFFSET, and not all by the same amount** (−0.55…+0.5 of the radius). Nine planes
  through the origin would pile every intersection into one knot at the middle and leave the rest
  of the ball empty.
- **Mullions are the prize.** Where two panes cross, a beam is laid along the intersection: the
  densest *and* straightest run of mass in the arena, so the best cut available is to find one and
  fly it. The line is exact analytic geometry (the closest point on the line of intersection to the
  cell centre, then one Pythagoras for the chord), not a search.
- **Rims carry the only traps** — the frame around each pane's disc, which is exactly what you clip
  when you misjudge a pass.

### 2 · The Swell — `SpawnableSwell`

Five wide **wavy roads**, each a plated ribbon meandering a closed circuit through the cell at its
own angle, threaded through one another. A road is **282–350 units across** — 7 or 9 plates,
roughly ten hull-widths — and its spine weaves in-plane (the primary sway) while rising and falling
out of it (a weaker secondary), so it rolls left and right and up and down for its whole lap and
never repeats. **The five spines sit at 780 / 1,080 / 1,350 / 1,572 / 1,728**, so the roads are
spread across the whole shell out to a far reach of ~2,000 rather than nested near the middle.

Where the panes teach commitment, the swell teaches the other half of it: **the line does not have
to be straight.** A ribbon is one continuous surface, so a pilot who finds one can hold the blade
down and simply FOLLOW it — the longest uninterrupted cut in the mode, and the one that asks for
flying rather than for aim.

Four things are load-bearing:

- **The road is WIDE, and the width is forgiveness.** The meander is what makes the cut
  interesting; the width is what stops a small steering error ending it. That is the whole reason
  this rung is ribbons rather than the corrugated sheets it replaced — sheets put the mass where a
  blade CROSSES it, ribbons put the mass where a blade can STAY on it.
- **The deck is MANY SMALL PLATES, and it is solid rather than a lattice.** A plate is
  **44 × 44 × 5.2**, and plates overlap in both directions (step 34 under a 44 plate, both ways),
  which is the Twistbands' rule for the Twistbands' reason: a sparse deck is something the sword
  rattles through rather than something it cuts. It is also why this is the second rung to author
  `GapScaleI2 = 1` — see § "The second dial: GAP".
- **The road is PAINTED as a road.** A Gold crown down the middle, Blue shoulders, Jade verges, as
  fractions of each road's own half-width so all five wear the same carriageway whatever their
  width works out to. A pilot can see where the middle of a ribbon is from across the arena.
  (Scoring does not care: every non-roster prism is hostile to everyone, in any colour.)
- **Running wide on a bend bites.** The only danger prisms here sit on the OUTER verge of the
  tighter corners — one lane, outside only, and only where the ribbon is genuinely turning — so the
  racing line is always clean and the mass you reach by drifting off it on a turn is the mass that
  punishes you. The bend test is curvature × the ribbon's own radius (1 for a perfect circle,
  biting above **1.35**), which is dimensionless and therefore survives the arena's length scale
  untouched.

Three construction facts worth keeping:

- **The deck never rolls about its own direction of travel.** The width direction is
  `cross(tangent, loop axis)`, so the surface banks with the climb and nothing else — deliberately,
  because holding a blade against a surface that rotates under it is the TWISTBANDS' lesson and
  this rung is the one that should be a joy rather than a test. Every ribbon's climb is authored so
  the bank stays under ~22°.
- **A road's half-width is bounded by its own narrowest spine radius** (0.6 of it, an authoring
  guard): wider than that and the inner verge folds through itself at the inside of a bend, which
  is invisible in a screenshot and unflyable in the arena.
- **Stations are walked by ARC LENGTH, not by parameter.** A meandering spine covers very different
  distance per radian at the apex of a bend than on a straight, so stepping `u` uniformly would
  bunch the deck in the corners and stretch it on the straights. The count therefore stays a ratio
  of two lengths (measured spine length ÷ plate step), which is what keeps prism counts invariant
  under `LengthScaleI2` — the same property the other three rungs rely on.
- **A road's width is a LANE BUDGET, not a fraction of its radius.** Every road is 7 or 9 plates
  across whatever its spine radius, so an outer road is the same width as an inner one and simply
  runs further. That is what keeps the deck's prism count proportional to how much PLACE a ribbon
  covers, and it is why the outer roads can sit at 1,728 without the count running away.

⚠ **This rung's VOLUME is 2.7× the Panes' on 7% fewer prisms**, because a road is tiled with square
plates where a pane is drawn with narrow ribs. Nothing reads it — the cell grows nothing — but do
not read the volume column as "how much arena there is" when comparing rungs 1 and 2. Prism COUNT
is the collider budget and the thing a player destroys.

### 3 · The Cage — `SpawnableRibcage` (the kept arena)

Three concentric hollow rinds of prism bone at radius **720 / 590 / 460** — meridian ribs, latitude
hoops, a diagonal through every cell, joints at every crossing, two polar crowns. Unchanged from
the mode that was named after it.

- **The openings are TRIANGLES.** Every rib × hoop cell carries one diagonal with an alternating
  lean, so the weave reads as a truss rather than as rounded bubbles.
- **It tightens inward.** `DensityStep` (1.05) compounds with the shrinking radius: cells run
  **188u → 148u → 112u**. The last layer is the hardest to slip through, not the easiest.
- **Every inner rind is TILTED onto its own axis** (`ShellTilts`, pole axes ≥34° apart), because a
  latitude-hoop sphere is densest at its poles and stacked caps would collapse the match into
  "everyone drills the top".
- **No free corridor** — `ShellLonOffsets` phases each rind so the gaps never line up radially.

### 4 · The Twistbands — `SpawnableTwistbands`

Three interlocked **Möbius ribbons** on the three coordinate planes, each carrying a solid plated
deck, a crosswise keel beneath it and a chunky cornice along its edge.

The other three arenas are made of surfaces that hold still — a pane is a plane, a swell ripples but
keeps its plane, a shell curves the same way everywhere. A Möbius band does something none of them
do: **its surface rotates about its own direction of travel as you fly it.** Hold a blade against
the deck and the deck turns out from under it, so the cut only continues if the pilot keeps rolling
the sword to match — which is exactly the axis the Rhino's triggers drive (RT−LT is yaw *and* roll,
`RHINO_SHIELD_SWIPE.md`). This is the one arena that asks for the swordsmanship rather than the
line, which is why it is intensity 4.

It is also the hardest to read: the bands carry an ODD number of half twists (1 / 3 / 5) and are
therefore genuinely one-sided, so flying a lap returns you to your own starting patch upside down —
mass you already cut is now above you. There is no global up and no silhouette to peel inward
through; you navigate by the ribbon.

- **The band frame is `SpawnableOurobor`'s, deliberately unchanged** — the width direction rotates
  `TwistRate · u` out of the loop plane (that IS the twist), and `AlongSurface` is the exact partial
  derivative rather than the loop tangent, so a plate near an edge is square to the surface it is
  actually on. What differs is what rides on it: Ourobor grows countryside on a band you LAND on;
  this lays a deck you CUT.
- **Lanes are painted ACROSS the ribbon**, so the deck reads as a road — and because the band is
  one-sided, the lane order MIRRORS after a lap. The arena's joke is something the player can see.
- **The cornice is ONE curve and needs `u` to run 0…4π to close.** That is the band's own proof of
  one-sidedness (after 2π the `+HalfWidth` edge has become the `−HalfWidth` edge), and it carries
  every trap in the arena — the edge is what you clip when the roll does not keep up. Halving that
  range would draw half an edge and leave the other half bare, which is the shape of bug that looks
  like a content gap.

## Nothing is shielded, and the reason is the AI

Every prism in every Cleave arena is `PrismKind.Plain` except the sparse `PrismKind.Danger` traps.
Nothing is `Shielded` and nothing is `SuperShielded`. The old doc gave one reason — a super-shielded
prism is fully invulnerable to `Prism.Damage`, so enough of them could put the destruction target
out of reach. There is a second reason, and it is the stronger one:

> **A super-shielded prism can only be popped by an ENERGIZED blade; energizing requires holding
> the both-triggers chop stance; and `AIPilot` never pulls a trigger.** (`RHINO_ENERGY_SWORD.md`
> states this outright: *"AI never pulls triggers, so AI Rhinos never energize"*.) Shielded mass is
> therefore mass an **all-AI domain can never score against**, in a mode whose entire score is
> destroying it — the Tollway rule, that an AI which cannot play is a defect.

This is asserted, not just documented: `cleave_budget.verify` fails the build if any arena emits a
shielded prism of either tier. If the mode ever wants hardened mass, it needs an AI that can
energize first.

## The envelope, per intensity

Each arena is built to its own radius from `SliceArenaGeometry`'s table, and three systems are
sized against it:

```
i1/i2   arena 2160  <  AI station 2808 (2160 × 1.3)  <  spawn ring 3150  <  membrane 3600
i3/i4   arena  720  <  AI station  936 ( 720 × 1.3)  <  spawn ring 1050  <  membrane 1200
```

- `CleaveController` parks its AI stations at `OuterRadiusFor(intensity) × AiStationStandoff`.
  `AIPilot` has no arrive-and-stop behaviour, so a station *inside* the mass is a point the AI
  orbits from within forever — the "the AI just stays inside" defect, twice.
- The scene's `spawnRingRadiusFloorByIntensity` puts players outside all of it (this cell has **no
  nucleus**, so the computed ring would otherwise collapse to the cell centre).
- Each intensity's `CellConfigDataSO` names its own `MembranePrefab`.

**It became a TABLE rather than one number the moment the rungs stopped being one size**, and each
of those three consumers is why: one shared radius across a 2,160-vs-720 spread either parks every
AI inside the two big arenas or 3,000 units away from the two small ones, and one shared spawn ring
does the same to the players. `SliceArenaGeometry` therefore owns `OuterRadiusI1..I4` +
`OuterRadiusFor(intensity)`, every consumer asks for the running intensity — server-side in all
three cases, so there is no config-sync race to lose — and `MaxOuterRadius` is there for anything
that genuinely has to bound every rung at once.

The ordering is asserted per rung in `cleave_budget.verify`, and it is asserted on the prism's
**far corner** rather than on its lay point: a lay at exactly 2,160 still puts geometry outside
2,160. The measured worst cases are **2,184** (the Panes) and **749** (the cage's rim), both
comfortably inside their own station radius.

### The arena is bigger than a nucleus, and that is the requirement

Cleave first shipped at radius **360** — *smaller than a standard nucleus* (`Nucleus.prefab` at
scale 400 is ~392 world radius). The four arenas were different places, and every one of them read
as the same thing: a small ball parked at the centre of an otherwise empty 1200-radius cell, with a
boosted Rhino (1,210 u/s off the ramp) crossing the whole of it in **0.6 s**. The envelope is now
**720**: a 1,440-unit play space, 1.8× a nucleus radius and 60% of the membrane's own diameter, so
the arena *is* the cell rather than an ornament inside it.

**The scale-up is a SIMILARITY, not a re-author.** `SliceArenaGeometry.LengthScaleFor(intensity)`
(= that rung's `OuterRadius / AuthoredRadius` — **6** on rungs 1 and 2, **2** on 3 and 4)
multiplies every authored LENGTH in all four generators that describes WHERE the arena is — shell
gaps, band radii, wave amplitudes, the across-grain spacing between one rib and the next — and
*divides* every noise FREQUENCY, so the void pattern keeps the same size relative to the arena.
Counts, angles and fractions-of-the-radius are left bare. **Prism dimensions are NOT in that list
any more** — see § "The prism dial" — which is the one place this section has been overtaken. Each
arena class is used by exactly ONE intensity, so each carries its rung's dials as `const`s. Three
things fall out of doing it that way, and each is why it was done that way:

- **Prism counts do not move, so the collider budget is untouched.** Every count in these
  generators is a ratio of two lengths that both carry the scale — `floor(radius / step)`,
  `round(arc / step)`.
- **Growing the prisms with the spacing keeps a rib reading as a bar — and it is NOT how this
  ladder is built any more.** A rib at twice the spacing with the same plank is a dotted line, not
  a bar, which is why prism size rode `LengthScale` for two passes; what that produced at 6× was an
  arena of 102–132-unit slabs, i.e. low poly. Prism size now rides its own dial and the ALONG-grain
  step goes with it, which is what keeps a rib continuous without making the pieces enormous
  (§ "The prism dial"). Prism SIZE is still free in colliders (only COUNT costs one) — which is
  also why shrinking it is *not* free: a third the size at the same along-grain density is three
  times the count.
- **The scale is an exact power of two where it can be**, so every scaled constant is bit-exact and
  no `floor` boundary or noise sample can land on the other side of itself. That is not a hope — at
  2× the harness re-measured and every arena came back with an **identical prism count, identical
  danger count, identical per-domain split, and a volume exactly 8×**. At **6** (= 2 × 3) that no
  longer holds exactly, which is why rungs 1 and 2 are re-MEASURED rather than assumed — and the
  measurement is what says the Panes' rib count fell by exactly the gap factor rather than by a
  rounding accident.

**Identical counts is also the proof that no constant was left unscaled**, and the negative control
says how much that is worth: at 2×, reverting just `SpawnablePanes.RibStep` to its unscaled `21f`
and re-measuring took the Panes from **11,021 prisms to 19,890** — an 80% collider-budget blowout
from one missed `* S`, and one that looks perfectly reasonable in a diff. Every scaled length is
written `x * S` at its declaration for exactly that reason: it is what makes an unscaled one
visible.

### The second dial: GAP

`LengthScale` answers *how big is this place*. **`GapScale` answers *how much of it is mass*** — it
multiplies the ACROSS-grain step alone, so a pane's ribs sit G times further apart while each rib
stays a continuous bar. It is the one dial that moves a prism count, and only ever down: rib count
falls by G. Only the **Panes** spends it (**G = 3**); the other three run 1.

**The gap dial and the similarity are provably orthogonal, and the control was exact.** *(Measured
at the 6×/G=3 pass, before the prism dial — the numbers below are that era's and are recorded as
history, not as today's counts.)* Setting `GapScaleI1` back to 1 and re-measuring reproduced the
Panes' pre-change count **to the prism** — 11,021, the number that arena had at 2× — which said two
things at once: the 6× `LengthScale` moved **zero** prisms (it really is a similarity), and the
whole ~54% reduction was attributable to the gap dial alone. Re-running that control today gives a
different absolute number, because the prism dial has since tripled every along-grain count; what
it still proves is the orthogonality.

**A gap scale above 1 is only definable for an arena whose across-grain density is a STEP, and only
WANTED where the surface is a set of BARS rather than a road.** The Panes samples ribs at a
spacing, so tripling it is one constant. The **Cage has no such step at all** — its density is a
rib/hoop COUNT on a sphere compounded inward — so a gap scale authored for it would be silently
INERT, which is why `SpawnableRibcage.AssertNoGapScale` says so loudly instead of leaving a comment
in a file nobody edits when they change the table. The **Swell and the Twistbands both HAVE a step
and both carry the dial at 1**, for one reason: each lays a continuous plated deck a blade is held
against, so opening its lanes up is not "sparser", it is a lattice the sword rattles through — a
different arena, not a lighter one. *Rung 2 spent that dial once, and the answer was to re-author
the arena instead.*

### The third dial: PRISM SIZE

> *"In general it is fun to destroy lots of little prisms, not single big prisms. That just makes
> the game look low poly, while lots of little prisms look high tech and beautiful."*

**`PrismScale` answers *what is this place MADE of*, and it is 2 on every rung.** A Cleave prism is
the same small piece whatever size the place is — `SliceArenaGeometry.PrismScaleI1..I4`,
`PrismScaleFor(intensity)`.

It exists because prism size was part of the similarity for the first two passes, and at 6× that
meant the two open arenas were built out of **102–132-unit slabs**: a pane's plank 20 × 20 × 102, a
mullion 31 × 31 × 132, a road's plate 132 × 15.6 × 132. Nine flat walls made of a few dozen
enormous tiles read as **low poly**, which is the opposite of what an arena you take apart with a
sword should read as. Destroying a hundred small things is the fun.

**What carries `PrismScale`, and what does not:**

| carries `PrismScale` (P) | carries `LengthScale` (S) |
|---|---|
| every prism's own X/Y/Z | shell gaps, band radii, pane offsets |
| the **along**-grain step (a run must stay continuous) | the **across**-grain step (rib to rib) |
| a plated deck's step in **both** directions (a road is continuous both ways) | the ribbon's own radius, half-width and wave amplitudes |
| | noise frequency (÷ S) |

**The cost is a count, and it is stated rather than hidden.** A count that is a ratio of two
lengths only stays invariant while both lengths share a scale; the along-grain step now carries P
where the across-grain layout carries S, so the two 6× rungs' counts rise by `S / P` = 3. Measured:
the Panes **5,107 → 15,380**, the Swell **4,607 → 14,277** (the Swell was also re-authored to spread
its roads across the whole shell in the same pass). Both are under the
`HISTORICAL_COLLIDER_CEILING` of 20,153 the mode has already shipped, so this needed no new
collider sign-off.

**It forces the target up, and that is a re-pricing rather than a longer match.** A target is a
fraction of the arena, so re-cutting the arena into three times as many pieces re-prices it:
rungs 1 and 2 went **400 → 1,200**, which keeps the same ~8% of the arena and is still *a third of
the volume* 400 of the old prisms represented. `cleave_budget` check 1 is what pins it — at 15,380
prisms the 4×–12× band admits 934…2,803, and 1,200 sits mid-band.

**One fleet-wide number is honest because rungs 3 and 4 prove it.** Their `LengthScale` was already
2, so re-measuring after the split returned **14,731 and 16,423 prisms, byte for byte** — the same
counts, danger counts, per-domain splits and volumes as before. If the split had been wrong
anywhere, those two would have moved.

**A side effect worth carrying past this mode: shrinking the prisms bought back the volume ladder's
float32 resolution.** The Panes' baseline went 345M → **38.5M**, where float32's ulp falls from 32
to **4** — under a Rhino trail prism's 4.5 — so that rung's volume ladder can be moved by laying
trail again. The Swell is still over the line (103M, ulp 8). See § "Stated cost" below, which
`cleave_budget` check 7 now measures per rung rather than asserting about the pair.

**A shared prefab's scale ceiling was the only thing in the project saying the prisms had grown
absurd — and it said it silently.** `SpawnablePrism.prefab` authors `maxScale` 100; at 6× the
Panes' rim and mullion (108 and 132) cleared it, which is why all four arenas opt into
`AdmitsAuthoredPrismScale`. Every size they state now is comfortably inside that window
(34/36/44 long), so the flag is a standing GUARD rather than a fix. *The clamp was reporting a
design problem as a rendering one.*

The **spawn ring and membrane are not pure scales of the arena either**, for a reason worth
carrying: at the 2× pass the MEMBRANE did not scale, so `576 × 2` would have put players 48 units
off the membrane wall, and the ring was authored at **1050** to keep the pre-scale absolute
clearance. At 6× the membrane *does* scale (`CleaveMembrane.prefab`, radius 3,600), so there the
ring is a clean **×3** of that play-tested pair. *When only part of a system scales, the interfaces
between the scaled and unscaled halves are what has to be re-derived by hand.*

### ⚠ Stated cost: on rung 2 the volume ladder cannot move

`Cell.liveVolumeTotal` is a **float32 running accumulator**, so its resolution is the ulp at the
value it holds. This was a both-open-rungs problem when prism size rode the envelope — every prism
was **216×**, baselines reached **345M** and **898M**, and float32's ulp there is **32** and **64**
against a Rhino trail prism's whole volume of **4.5** (`BaseScale` 3 × 3 × 0.5), so adding one was
a no-op and the cell stayed in Calm for the whole match on both.

**The prism dial fixed half of it.** The baselines are now **38.5M** (ulp 4) and **103M** (ulp 8),
so the Panes' ladder moves one trail prism at a time and **the Swell's still cannot** — a road is
tiled with square plates where a pane is drawn with narrow ribs, so rung 2 carries 2.7× the volume
on 7% fewer prisms.

That is harmless today and only because this cell grows nothing — `SupportedFloras` and
`SupportedFaunas` are both empty, so nothing reads the phase — which is exactly why it is a
**gate** rather than a paragraph: `cleave_budget` check 7 computes the ulp at each baseline, reads
the spawn profile, and fails the moment somebody gives Cleave a food web. It is stated PER RUNG, so
a rung that comes back under the line simply stops being asserted about — which is how the Panes
dropped out of it. There is no fix inside the thresholds; the resolution is a property of the
BASELINE, so the answers are a smaller arena, smaller prisms (which is what happened), or a double
accumulator.

General rule for the next mode that scales an arena up: **a uniform k× similarity is a k³ change in
the numbers a float32 volume accumulator has to hold, and past ~10⁸ it stops being able to see a
trail prism at all.**

### Admitting an authored prism size

While prism size rode the envelope, three of the Panes' authored lengths (plank 102, rim 108,
mullion 132) cleared `SpawnablePrism.prefab`'s serialized `maxScale` of 100.
`PrismScaleAnimator.SetTargetScale` clamps **per axis, inside the setter, with no log and no return
value**, so the arena would have built with 100-long mullions — gaps in a wall — and nothing
anywhere would have said so.

**Every size the four arenas state is now inside that window** (the longest is a 44-unit deck
plate), because the prism dial took size off the envelope. The overrides stay as a standing GUARD,
and the episode is worth keeping for what it says about the clamp: *it was the only thing in the
project reporting that the prisms had grown absurd, and it reported it as a rendering artefact
rather than as a design problem.*

The four arenas therefore override `CellEnvironmentSpawnableBase.AdmitsAuthoredPrismScale`, which
`SpawnLeafObjects` passes to `PrismTrailBuilder`, which calls `Prism.AdmitTargetScale` before
re-stating the size. Three things about that are load-bearing:

- **It is opt-in, not global.** `SpawnablePrism.prefab` is shared by ~30 spawnables, and admitting
  a size one of them is *relying* on the clamp to cut is a behaviour change for that spawnable, not
  for this one. A caller that does not ask lays byte-for-byte as before.
- **It runs AFTER `Initialize`, and that ordering is the trap `ScarabSwitch.TryLay` already
  records**: `Initialize` → `ResetState` → `RestoreAuthoredScaleWindow()` undoes any widening and
  then re-clamps the target against the restored window, so a size stated before `Initialize` is
  silently trimmed twice over.
- **The widening only ever widens, and pool reuse restores the authored window**, so a prism that
  once carried a 132-long mullion cannot keep that ceiling into its next life as a trail prism.

## How the numbers are measured

**`Tools/Build/cleave_arena_harness` compiles the SHIPPED arena generators and runs them.** Its
`run.sh` hands `csc` the four real `Spawnable*.cs` files from `Assets/` — they are not copied —
alongside a faithful shim of the `UnityEngine` and `CellEnvironmentSpawnableBase` surface they sit
on, and runs the result. (No `.csproj`: the repo gitignores `*.csproj` because Unity generates its
own, so a project file here would be untracked and the harness would not survive a clone. Same
shape as `Tools/Build/regatta_course_harness`.) It emits `cleave_arena_measurements.json`:
prism counts, exact volume, per-kind and per-domain breakdowns, and the far-corner reach.

Three of the four arenas cull prisms with value noise, so an analytic model would have to
re-implement that noise — and the float path into it — to be exact, and would silently become an
ESTIMATE the first time either drifted. Running the real code has no drift surface at all.

**The measurement is hash-guarded.** Every source that could move a count (the four generators, the
shared envelope, `PaintingStrokeToolkit.cs` for the noise, and the harness's own shims) is hashed
into the JSON, and `cleave_budget.load()` refuses to answer from a measurement whose sources have
moved. Re-measure with:

```
bash Tools/Build/cleave_arena_harness/run.sh
```

**Faithfulness is proven by a control:** the harness reproduces the cage's shipped **14,731** prisms
and its shipped count thresholds (15431 / 15231 / 18331 / 17731) exactly.

### The predecessor model was wrong by exactly 2×, and it mattered

`ribcage_budget.py` computed the per-prism jitter volume factor as
`((1.2)**4 - (0.8)**4) / (4*0.2)` = **2.08**. `Jit(s, 0.2)` draws `k ~ U(0.8, 1.2)`, so
`E[k³] = (1.2⁴ − 0.8⁴) / (4 × 0.4) =` **1.04** — the divisor used the *half-width* of the range
instead of its width. The file's own comment said 1.04.

Every shipped volume threshold was therefore ~2× the arena's real volume, and
**volume is the spine**: `CellPhaseThresholds.Compute` steps phase on volume and uses count only as
a Frenzy backstop. The cell's live volume sat permanently below `RestlessEnterVolume`, so the
authored ladder described a cell twice as heavy as the one that exists and never moved. The
measured baselines fix it.

⚠ **`Tools/Build/wildlife_cage_budget.py` still carries the identical expression** and is
deliberately NOT touched here — that is another mode's tuning, and correcting it makes Wildlife
Liberation's cell reach Restless/Frenzy *earlier*, which gates fauna release and so is a real
balance change rather than a pure bug fix. It wants its own pass.

## Intensity

The platform already has exactly one way for a cell to vary by intensity, and this mode uses it
rather than inventing a second:

```
Cell.AssignConfig                                     [Cell.cs]
  CellTypeChoiceOptions.IntensityWise
    → index = Clamp(gameData.SelectedIntensity - 1, 0, CellConfigs.Count - 1)
    → Cleave Cell Config 1..4, in that order
        → EnvironmentPrefab = SpawnablePanes / Swell / Ribcage / Twistbands
        → PhaseThresholds   = THAT arena's own MEASURED baseline
```

Each intensity needs its OWN `CellConfigDataSO` because `PhaseThresholds` must ride its own
baseline — and the spread is still wide: the arenas run ~14.3k…16.4k prisms and **23M…103M**
volume (rungs 1 and 2 are 6× the authored radius, so a road's plate sweeps far more space even at
the shared prism size), so one shared threshold block would put three of the four cells in the
wrong phase from frame one. It
is also where each rung's `MembranePrefab` lives, which is how intensities 1 and 2 get their
3,600-radius shell without any code branching on intensity.

**Neither prism count nor volume is monotone across the ladder, deliberately.** A monotone-count
assertion used to stand here as a proxy for "intensity reads as more to destroy"; it was retired
when the destruction target became per-intensity, because raw count then says nothing about how
long a match runs. What `cleave_budget` asserts in its place is the thing that actually has to
hold: **every rung's arena holds 4×–12× its own target in hostile mass** (measured 7.4×–9.3×), so
a domain can reach its target off the arena alone without the arena being mostly scenery.

## Collider budget

One box collider per prism, so the arena *is* the collider count: **15,380 / 14,277 / 14,731 /
16,423**, plus nothing else (no fauna, no flora in this cell). The ladder is now FLAT in colliders
— four arenas within 15% of each other — which is a consequence of the prism dial rather than a
goal: pinning prism size at 2 made the two open rungs' counts triple into the same band the two
dense ones were already in.

**The heaviest arena is still lighter than what shipped**: 20,153 (the old five-rind cage) against
today's 16,423, an **18.5% cut** to the worst case, and `cleave_budget` asserts the heaviest arena
never exceeds that 20,153, so the ladder can only ever get lighter than a number that already had
a product decision behind it. ⚠ The headroom is now **23%**, where before the prism dial it was
4.5× — *a ladder that is flat in colliders has no cheap rung left to spend*, so the next arena
change has to state its count up front.

That said, intensity 1 is still ~7× the masterplan's ≤1,500 per-cell target and intensity 4 ~11×.
This remains the branch's headline performance risk. Mitigations are the standing ones (collider-LOD
by phase, no new physics queries — scoring rides the StatsManager SOAP channel and the AI aims
analytically), and the mode's whole verb actively removes colliders as the match runs. **Measure on
device before tuning.** The bluntest dial per arena is its step size (`RibStep`/`PlankStep`,
`PlateStepAlong`/`PlateStepAcross`, `DensityStep`); re-run the harness and the generator after any
change.

## The pipeline (zero bespoke tracking)

The stat was already plumbed platform-wide (Rampage runs on it); the mode only picks it and reads
it twice — once to end the turn, once to drive the milestones.

```
Rhino cuts a prism (one hit - plain prism)
  └─ Prism.Damage → SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{…})
              ▼
StatsManager.PrismDestroyed → HostilePrismsDestroyed++   (arena mass is non-roster ⇒ hostile;
                                                          your own team's trail is filtered out)
              ▼
ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ CleavePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached   [server]
  ├─ CleaveController.SampleProgress → leader + milestone rungs           [server]
  └─ ElementalComebackSystem (source PrismsDestroyed) → trailing-team buff
              │  turn end
              ▼
CleaveController.OnTurnEndedCustom → AssignScores → SyncFinalScores_ClientRpc
```

## Vessel lock

**Rhino only** (`ArcadeGameCleave.Vessels` has one entry), enforced in **two** places because one
was not enough:

1. `GameDataSO.SyncFromArcadeGame` clamps `selectedVesselClass` into the game's allowed set. This
   covers the machine that pressed Start, on every route (modal, rematch, Maelstrom chain).
2. `ServerPlayerVesselInitializer.ResolveSpawnVesselType` re-clamps **server-side at spawn**. This
   is the one that matters in multiplayer: `Player.NetDefaultVesselType` is an OWNER-write
   NetworkVariable that each client sets from its OWN local config and from the menu's
   vessel-changer toy, so a client walked in still wearing the hull it last flew — and
   `SyncFromArcadeGame` never runs on a client, while the config ClientRpc lands *after* the spawn.
   Symptom: **the client flew a Dolphin in Rhino-only Cleave** while the AI (whose class comes from
   the scene's `aiInitializeDatas`) correctly spawned Rhinos. The server is the only authority that
   sees every player's request and the mode's rules together, so the clamp belongs there — the same
   principle as never writing domain state from client code.

## Progress milestones

At a quarter and a half of the win target, the **leading** domain crosses a rung:
`CleaveController.SampleProgress` (server, every `progressSampleSeconds` = 0.5 s) →
`AnnounceMilestone_ClientRpc` → a `GameToastSituation` post plus `HapticController.PlayAlert()` on
every peer (~1.2 s of hard rattling — the game's **third** haptic feel, and the only thing that
fires it; see `Docs/HAPTICS.md`).

These are **pure feedback — they change no game state**, so a missed or late sample costs a toast,
never a rule. Rungs ride the leader's *own* progress rather than a cross-domain total so they land
at a fixed point in the race. A lead change after the first milestone posts `CleaveLeaderChanged`.

The two rung situations are `CleaveQuarterCut` / `CleaveHalfCut` — renamed from `…QuarterPeeled` /
`…HalfPeeled` with this branch, because "peeled" described ONE of the four arenas (you do not peel
a wide wavy road). All three renames of these two values have been free for the same reason: no
`GameToastConfigSO` authors them yet, so nothing serialized points at any old name. Toast copy is
still unauthored, so **right now the shake IS the milestone feedback**.

## Spawning outside the arena

Players start on the computed cell spawn ring (`CellSpawnFormation`, all facing the cell), NOT on
authored transforms: the donor scene's four points sat at ±50, deep inside the arena.

**The formation is `EquatorialRing`, not the default `Symmetric`** — everyone evenly spaced on one
horizontal great circle. This was a fairness requirement for the cage (a latitude-hoop sphere is
densest at its poles, so a tetrahedral spread would drop two of four players onto the hard cap) and
it remains the right default for the other three, none of which is uniform about its poles either.

The ring normally measures off the cell's nucleus radius, and this cell deliberately has none — so
it would collapse to the cell centre. `spawnRingRadiusFloorByIntensity` (**3150 / 3150 / 1050 /
1050**) gives the ring a floor for exactly this case: a cell whose "core" is a structure rather
than a nucleus. The numbers are owned by `cleave_budget.SPAWN_RING`, which asserts each sits
outside its own rung's AI stations and inside its own membrane, and the generator writes them into
the scene from there.

The per-intensity list is a **platform** addition (`ServerPlayerVesselInitializer`), and the scalar
`spawnRingRadiusFloor` stays as the fallback for a rung the list does not cover — a missing or 0
entry means "this rung has nothing to say", never "no floor", so an author who sizes one rung and
leaves another blank gets the scalar rather than the centre of the cell. It is resolved SERVER-side
from `GameDataSO.SelectedIntensity`, which is set before the scene loads, so it does not meet the
config-sync race that bites a CLIENT computing an intensity-derived value.

**The arcade card's PREVIEW mirrors the scalar, and only the scalar.**
`ModePreview_Cleave.asset` carries its own copy of the spawn block, written by
`Tools/Build/author_preview_spawns.py` straight off the scene's
`ServerPlayerVesselInitializer` — so the preview satellite opens a pilot where the match would.
That tool reads the SCALAR field, which this scene sizes for its two big rungs, so the preview
stands a pilot at **3150** on all four intensities where rungs 3 and 4 spawn at 1050 in a real
match: further out than it needs to be, never inside the arena, which is the safe direction.
It went stale once already — the preview still said **576** after two envelope passes had moved
the scene to 1050 and then 3150, which would have opened the preview *inside* the intensity-1 and
-2 arenas — so **re-run `author_preview_spawns.py --check` whenever the scene's spawn ring moves**;
it is the one place this mode's geometry is written down outside the scene and the generator.

## AI

**Every AI station is OUTSIDE the arena. That is the whole fix.** `AIPilot` has no arrive-and-stop
behaviour — it steers at `_targetPosition` forever and flies through on arrival — so *any* target
inside the arena becomes a point the AI loops around from within.

One station per strike, always at `SliceArenaGeometry.OuterRadiusFor(intensity) × 1.3` — **per
intensity**, because a station at the cage's 936 would sit deep inside the Panes' 2,160 of mass,
which is the orbit-from-within defect the standoff exists to prevent. Stations walk a
golden-angle spiral, so successive stations are ~137° apart and **the chord between them passes
close to the centre** — a full crossing of the ball. The loitering happens outside; the damage
happens on the transit.

**This needs no per-arena code, and that is a property of the chord rather than luck**: a line
through the middle of the ball cuts panes, sheets, shells and ribbons alike. Every AI is phased onto
its own arc so a full lobby spreads around the sphere, and every 4th strike is a RAID on
`Cell.GetExplosionTarget` (the densest mass hostile to its domain, i.e. opponents' trails), offset
by seat so they never all raid at once.

**Invariant for anyone re-tuning this:** `SliceArenaGeometry.AiStationStandoff` must stay **> 1**.

⚠ **An AI Rhino cannot energize its blade** (no triggers), which is fine here because nothing in any
arena is super-shielded — but it is the constraint that makes "no shielded mass" a rule rather than
a preference. See above.

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.cleavePrismTargetByIntensity` — **1200 / 1200 / 1500 / 1500**, with
`cleavePrismTarget` as the scalar fallback for a rung authored 0) — the number of hostile prisms a
domain must DESTROY to win. The field was `ribcagePrismTarget` until this branch; both it and its
Build twin were renamed with the mode, and both gained a per-intensity twin.

**Why it is per-intensity:** rungs 1 and 2 are vast open arenas you CROSS where 3 and 4 are
compact objects you PEEL, so a shared target would make the two EASIEST rungs the longest matches
in the mode — the exact inversion the intensity ladder is supposed to express. At
1200/1200/1500/1500 every rung asks for a comparable *fraction* of its own arena, which
`cleave_budget` check 1 asserts as a band rather than leaving to inspection. The pair moved 400 →
1,200 when the prism dial re-cut those arenas into three times as many pieces: **a target is a
fraction of the arena, so re-cutting the arena re-prices it.**

`CleavePrismTurnMonitor` resolves it SERVER-side and replicates the result through
`_netPrismTarget`, so a client receives the number rather than deriving it from an intensity it may
not have yet — the distinction `Docs/ECOSYSTEM.md §28` records for `IntensityWise`.

> **⚠ Pacing flag — none of this has been playtested.** The original 2,000 was set when every bar
> was a two-hit shielded prism in a 14,977-prism cage; the bars are one-hit now, three of the four
> arenas are new geometry nobody has flown, and rung 2 has been re-authored outright. 1,200 is
> **8%** of the Panes' 15,380 prisms and 1,500 is **9%** of the Twistbands' 16,423 — comparable
> fractions by construction, which is what the per-intensity split bought, but the absolute match
> LENGTH is still a guess. It is four editor fields, and the milestones follow whichever applies.

Comeback rate is **`0.0125`**, so a quarter-of-target deficit buys **3.75** element levels at the
1,200 target (4.69 at 1,500) — over the one-whole-level floor the arcade recipe requires. It was
raised from `0.01` when the target was cut 500 → 400, precisely because at 400 the old rate sat
exactly ON that floor; the later rise to 1,200 only widened the margin, which is the safe
direction. ⚠ Any future CUT must raise the rate with it: `bonusLevels = deficit ×
rate`, so the rate is a function of the TARGET. That trap has now been recorded by Dog Fight, The
Bends, Wildlife Liberation and Tollway, and **`author_cleave_assets.py` now FAILS the build on
it** rather than leaving it to this paragraph.

## The fauna removal (2026-08)

The mode used to run a **fauna ladder**: a brood was penned inside the cage, the cell's controlling
domain was pinned to the race leader (`Cell.SetModeControlOverride`) so the brood hatched in the
leader's colours, and the untouched legacy herbivore diet turned it loose on every trailing team.
Fauna were **removed from the level on request**, so:

| removed | kept |
|---|---|
| The five fauna config assets and the spawn profile's `SupportedFaunas` | Every platform capability the ladder was built on |
| `ApplyStage`, `PublishLeader_ClientRpc`, `PublishRelease_ClientRpc`, the stage constants | `Cell.SetModeControlOverride` / `ModePhaseFloor` / `FaunaReleaseTier` / `FaunaContainmentRadius` / `ContainmentIntruderFrenzy` / `HasPreyInsideFaunaContainment` |
| `SpawnableRibcage.ContainmentRadius` | `SpawnProfileSO.InitialFaunaReleaseTier`, `FaunaConfigurationSO.ReleaseTier`, the batched fauna seeding, the shielded-grid fix |

The kept items are general, documented platform capabilities with no Cleave dependency — several
now have **no caller**, which is accepted for the same reason `ScoringMetric.PrismsRemaining` is
kept: churning a shared, serialized surface twice costs more than an unused-but-documented API.

## Shared-code touchpoints

| Site | Change |
|---|---|
| `GameModes` | `Cleave = 39` (was `PeelTheCage`, was `Ribcage`) |
| `GameModeRenameMigration` | `Ribcage` → `Cleave` and `PeelTheCage` → `Cleave`, each in ONE hop |
| `GameToastSituation` | `CleaveQuarterCut = 50`, `CleaveHalfCut = 51`, `CleaveLeaderChanged = 52` |
| `SliceArenaGeometry` | **new** — the one envelope all four arenas and the AI are built to |
| `SpawnablePanes` / `SpawnableSwell` / `SpawnableTwistbands` | **new** arena generators |
| `SpawnableRibcage` | reads `SliceArenaGeometry.OuterRadiusI3`; its `ShellRadius` const is retired (the controller now reads the envelope table), and it asserts its rung authors no gap scale |
| `Cell` | `SetModeControlOverride` (+ live-swarm re-colour), `ModePhaseFloor`, `FaunaReleaseTier`, fauna containment, `NotifyBlockShieldStateChanged`, shielded mass excluded from the targeting grids |
| `HapticController` | `PlayAlert()` — the third feel, gate extended per `Docs/HAPTICS.md` |
| `GameDataSO` | `SyncFromArcadeGame` clamps `selectedVesselClass` into `SO_ArcadeGame.Vessels` |
| `ServerPlayerVesselInitializer` | `spawnRingRadiusFloor` — lets the computed ring serve a cell whose core is a STRUCTURE rather than a nucleus; **`spawnRingRadiusFloorByIntensity`** — a per-rung override for a mode whose intensities are different PLACES |
| `EndConditionOverridesSO` (+ window + asset) | `cleavePrismTarget` live/build/getter; **`cleavePrismTargetByIntensity`** live/build + `GetCleavePrismTarget(int)` |
| `CellEnvironmentSpawnableBase` | **`AdmitsAuthoredPrismScale`** — opt-in widening of `PrismScaleAnimator`'s per-axis clamp for an environment that STATES a prism size outside the shared prefab's window |
| `PrismTrailBuilder` | `admitAuthoredScale` threaded through `LayOne` / `LaySync` / `LayBudgetedAsync` / `ConfigureLaid`, applied AFTER `Initialize` |
| `ElementalComebackSystem` | `GameModes.Cleave` shares Rampage's `ScoreDifferenceSource.PrismsDestroyed` case |

### The one cross-mode behaviour change: shielded mass leaves the targeting grids

`Cell.AddBlock`'s own comment already stated the rule — *"fauna must never be led to mass they
cannot eat"* — and applied it only to nucleus-interior mass. `Docs/ECOSYSTEM.md` §16.2 then removed
shielded prisms from every herbivore's **diet**, but they stayed in the **grids**, so density
centroids kept steering swarms onto mass the creatures had just been told they could not eat.

Shielded prisms are now excluded from the targeting grids at `AddBlock`, and
`NotifyBlockShieldStateChanged` re-files a prism when a shield engages or is shed. It strictly
*reduces* grid work and adds no query. **It affects two other modes and both are improvements** —
Skim Race's super-shielded track and Astro League's super-shielded edge lining no longer pull fauna
steering. Note it has **no bearing on Cleave itself**, which has no shielded mass and no fauna; it
is kept because it is a genuine platform fix.

## Assets

| Asset | Path |
|---|---|
| Arcade game config | `_SO_Assets/Games/ArcadeGameCleave.asset` |
| Scoring rule | `_SO_Assets/Scoring Rules/CleaveScoringRule.asset` |
| Cell configs (4) | `_SO_Assets/Cell Configs/Cleave Cell/Cleave Cell Config {1..4}.asset` |
| Spawn profile | `_SO_Assets/Cell Configs/Cleave Cell/Cleave Spawn Profile.asset` |
| Arena prefabs (4) | `_Prefabs/Spawnables/Spawnable{Panes,Swell,Ribcage,Twistbands}.prefab` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameCleave.unity` (in `EditorBuildSettings`) |
| End conditions | `Assets/Resources/EndConditionOverrides.asset` (`cleavePrismTarget` + `cleavePrismTargetByIntensity`) |
| Membrane (rungs 1–2) | `_Prefabs/Environment/CleaveMembrane.prefab` — a ×3 similarity of `CapsuleMembrane.prefab` (radius 3,600) |
| Measurements | `Tools/Build/cleave_arena_measurements.json` (generated; hash-guarded) |

Every asset above is authored by `Tools/Build/author_cleave_assets.py` — deterministic GUIDs,
idempotent, validates before writing. **Re-tune there and re-run** rather than hand-editing the
YAML. Its `--check` is a **real diff against disk**, not a dry run: the previous version only
re-ran its in-memory validation and printed "no files written", which passed whatever the assets
actually said.

**The Rampage scene clone is a spent one-shot and now STANDS DOWN.** Once `MinigameCleave.unity`
exists the generator patches the blocks it owns (the Cell's config list, the spawn ring, the
controller's field names) and leaves the rest alone. It only clones from Rampage when the scene is
missing. This is the `author_dogfight_assets.py` trap avoided deliberately: an `assert` on a donor
that has moved on aborts the script and takes every check below it with it, which is how four
sibling mode generators came to validate nothing.

**Re-tuning order** (any geometry change):

```
bash Tools/Build/cleave_arena_harness/run.sh   # re-measure the shipped generators
python3 Tools/Build/cleave_budget.py                    # inspect the ladder + thresholds
python3 Tools/Build/author_cleave_assets.py             # re-author the assets
python3 Tools/Build/author_cleave_assets.py --check     # must pass
```

## In-editor verification (authored headless — NOT yet run)

Nothing below has been executed. The arenas were measured by compiling and running their real
generators, which proves what they EMIT; it proves nothing about how any of it looks or plays.

1. **Open** `MinigameCleave.unity`. Every script reference resolves (no "Missing (Mono Script)"),
   the controller's inspector shows `rule` = CleaveScoringRule, the milestone fractions 0.25 / 0.5
   and **`aiArenaRadiusOverride`** (renamed this branch — if the inspector shows a stray
   `aiCageRadiusOverride`, the scene was not re-authored), and the **Cell shows four configs with
   Cell Type Choice = Intensity Wise**.
2. **Intensity picks a DIFFERENT PLACE.** Launch each of 1–4 in turn. You should get angled slabs,
   then wide wavy roads, then three nested shells, then twisted ribbons — four arenas that look
   nothing like each other. *This is the headline check*: if two intensities look alike, the Cell is
   not on `IntensityWise` or the configs are listed out of order.
3. **Baseline confirm.** FrogletTools ▸ Ecology ▸ Measure Cell Environment Baselines should report
   **15,380 / 14,277 / 14,731 / 16,423** prisms. If it disagrees, the harness and the editor have
   drifted — re-run the harness and investigate before shipping.
4. **Panes — the mullions.** Find a pane-pair intersection: it should be a straight, visibly denser
   beam. Flying one end to the other should be the best single cut in the arena.
5. **Swell — the road is legible, and holdable.** From a distance each ribbon should read as a
   carriageway: a Gold crown down the middle, Blue shoulders, Jade verges. Find one, put the blade
   on the deck and FOLLOW it through a full lap — the cut should stay unbroken through the weave
   and the climbs, and that is the whole rung. If contact keeps dropping, the plate overlap or the
   arc-length walk is wrong, not the pilot.
6. **Swell — running wide on a bend punishes, and the racing line never does.** Hold the crown
   through the tighter corners: nothing should bite. Drift out to the OUTSIDE verge on one of those
   same corners and you should full-stop, debuff all four elements for 4 s and lose boost. Danger
   on an inside verge, or on a straight, means `BendAt`'s sign or threshold is wrong.
6a. **Swell — no fold-through.** Fly the INSIDE verge of the tightest bend on each of the five
   ribbons. The deck must stay a single surface; a road doubling back over itself there means a
   half-width got authored past its spine's narrowest radius and the build-time guard did not fire.
7. **Cage — unchanged, full stop.** Three rinds at 720 / 590 / 460, triangular openings, tightening
   inward, each inner rind tilted onto its own axis, no free radial corridor. Rungs 3 and 4 were
   NOT touched by the 3× pass, so anything that differs from the previous build here is a
   regression, not a tuning.
8. **Twistbands — the roll.** Fly a band holding the blade against the deck: keeping contact should
   require continuously rolling the sword. Fly a full lap and confirm you arrive back at your start
   inverted, and that the lane colours have mirrored.
9. **Twistbands — the cornice closes.** Follow one band's edge rail all the way round: it should be
   a single continuous curve that takes TWO laps to return to its start. A rail that stops halfway
   means the 0…4π sweep was shortened.
10. **Every prism is one hit.** Nothing anywhere should shed a shield or show an octahedron.
11. **No fauna.** Nothing should hatch, at any intensity, at any point in the match.
12. **Rhino only — SOLO.** Pick a different vessel in an earlier game, then launch Cleave: you
    should spawn a Rhino, with a `clamping selected vessel` line in the log.
13. **Rhino only — MULTIPLAYER (the regression that shipped once).** Have the CLIENT fly a Dolphin
    in the menu (vessel-changer toy), then have the host launch Cleave. The client must spawn a
    **Rhino**, with a `does not allow Dolphin; spawning Rhino instead` warning on the host. Then
    return to the menu and confirm the client can pick a Dolphin again.
14. **Spawn outside, on the equator — at the RIGHT radius for the rung.** All four players start on
    ONE horizontal circle, 90° apart, facing the arena, with the whole thing visible ahead: **~3150u
    out at intensities 1–2** and **~1050u at 3–4**. If a big arena spawns you at 1050 you are
    INSIDE it, which is the whole reason the floor became per-intensity. Also check Crystal Capture
    still spawns on its sphere (tetrahedral) — that scene must be unchanged.
15. **Everyone starts at 0.** In a real lobby, check every score panel reads 0 the instant the
    countdown ends — including after a rematch and after a previous game in the same session.
16. **Smashing scores; laying does not.** The HUD domain sum should rise as you cut and not at all
    from laying trail. Shatter one of your OWN team's trail prisms — the sum must not move; a
    rival's trail must.
17. **Milestones follow the rung's own target.** At intensity 4 the leading domain should shake
    hard for ~1.2 s at **375** destroyed and again at **750**; at intensity **1** those rungs are
    **100** and **200**, because the milestones are fractions of whichever target applies.
18. **Win + scoreboard.** First domain to its rung's target (**400** at 1–2, **1,500** at 3–4) ends
    the turn; winners show a time, losers "N Prisms Left". Confirm the goal row counts to the right
    number on intensity 1 — if it says 1,500 there, the per-intensity target did not replicate.
    Replay (scene reload) resets the milestones.
19. **Pacing.** Time each intensity end to end — see the pacing flag. Most likely thing to need a
    change, and the two open arenas are the least known quantity in the mode.
20. **AI stays outside, in every arena.** Watch an AI Rhino for a minute at intensity 1 AND at
    intensity 4: it should orbit outside and cut on transits. If it settles inside, either the
    standoff has been set ≤ 1 or the controller is reading the wrong rung's radius — at intensity 1
    a station must be ~2,808u out, not 936.
21. **The big arenas' prisms are the size they were authored.** At intensity 1, a pane's rib must
    read as a continuous BAR and a pane-pair beam must be unbroken. A visibly DOTTED rib is
    `PrismScaleAnimator`'s silent 100-unit clamp, i.e. `AdmitsAuthoredPrismScale` not reaching the
    lay — it fails as geometry, never as an error.
22. **The big membrane is there.** At intensities 1 and 2 the membrane shell must sit outside the
    arena (3,600), not cut through it. A membrane inside the mass means the cell config is still
    pointing at `CapsuleMembrane.prefab`.
23. **Cloud save survives the rename.** Sign in with an account that has Cleave progress from
    before this branch: unlocks, quest completion, max unlocked intensity and bests must all still
    be there. This is what `GameModeRenameMigration` exists for, and the failure mode is silent.
24. **Regression — the grid change.** Play **Skim Race** (intensity 3) and **Astro League**: fauna
    should behave normally and should no longer park against the super-shielded track / edge lining.
25. **Collider telemetry** on device via DiagnosticsHUD / the Benchmark tool, at intensity 4.
26. **THE SCALE — the check this branch exists for.** At every intensity, the arena must fill the
    cell rather than sit in the middle of it: from the spawn ring the mass should span most of the
    view. A boosted straight-line run across the whole thing should take **~1.2 s** at intensities
    3–4 and **~3.6 s** at 1–2. Sight-check that each arena's far side is comfortably inside its own
    membrane (far corner 2,184 against 3,600 on the big rungs, 749 against 1,200 on the small ones)
    and that nothing pokes through it.
27. **THE PRISM DIAL — the check the last pass exists for.** At intensities 1 and 2 the arena must
    read as **lots of small pieces**, not as a few enormous slabs. A pane's rib is now a run of
    34-unit planks rather than 102-unit ones, and a road's deck is 44-unit plates rather than
    132-unit ones. Two failure modes to tell apart: if it still reads as slabs, a prism dimension
    is still on `S` instead of `P`; if a rib reads as **beads** rather than a continuous bar, the
    along-grain step went to `P` while its prism length did not (or vice versa). Prism counts would
    be unchanged in the second case, so no offline check can see it — this is the one thing on the
    list the harness structurally cannot prove.
28. **Nothing was silently clamped.** Select one laid arena prism at intensity 1 and read its
    transform scale: a pane rib should be **~6.8 × 6.8 × 34**, a mullion **~10.4 × 10.4 × 44**, and
    a Swell deck plate **~44 × 5.2 × 44**. Every one of those is now comfortably inside
    `SpawnablePrism.prefab`'s `maxScale` of 100 — the clamp is no longer load-bearing here — so any
    axis pinned at exactly **10** means the arena is laying through a prefab that inherits the
    default window rather than `SpawnablePrism.prefab`'s.

## Known limitations / follow-ups

- **Nothing has been run in the editor.** The arenas are measured, not seen. Every claim about how
  they LOOK — that the mullions read as beams, that a Swell ribbon reads as a carriageway at
  range, that the twistbands' roll is a manageable ask rather than an infuriating one — is a design
  intention awaiting a playtest. **Intensity 1's LAYOUT is the one exception: it was flown and
  approved at the 6x envelope**, which is why the pass that re-authored rung 2 left its geometry
  alone — but the prism pass after it re-cut rung 1 into 15,380 pieces from 5,107, so what was
  approved is where the surfaces ARE, not what they are made of. The one thing to look at first is
  whether a rib at 34 units long still reads as a continuous beam at the 2,160 spacing it is laid
  at; the approval does not cover that, because the arena it was given to had 102-unit planks.
- **The 1,200 / 1,200 / 1,500 / 1,500 targets are unmeasured for all four arenas** — see the pacing
  flag. The split makes every rung ask for a comparable FRACTION of its own arena, which is a real
  improvement over one shared number, but nothing here says what the resulting match LENGTH is.
- **The arcade card's preview opens at 3,150 on every rung**, because
  `author_preview_spawns.py` mirrors the scene's SCALAR spawn-ring floor and this mode's is sized
  for its two big arenas. Rungs 3 and 4 spawn at 1,050 in a real match, so the preview stands the
  pilot three times further out than the arena needs — further away, never inside, which is the
  safe direction, but it makes the two small arenas read as specks on the card. The honest fix is
  a per-intensity floor on `ModePreviewDefinitionSO` mirroring the one
  `ServerPlayerVesselInitializer` now has; out of scope here, and it is the only mode in the
  project whose rungs differ in envelope by 3x, so nothing else is waiting on it.
- **⚠ Intensity 2 is the least-known thing in the mode.** It is brand-new geometry at a scale
  nobody has flown, and unlike its three siblings it is not a variation on anything that has been:
  five closed meandering roads is a different proposition from a stack of surfaces, and whether a
  pilot can FIND one from the spawn ring at 3,150 units out is the first thing to check. The dials
  are the ribbon table (`RibbonSpecs` — count, radii, half-widths and harmonics) and
  `PlateStepAlong`/`PlateStepAcross`; re-running the harness plus `cleave_budget.py` is the whole
  re-tune loop. It has now been re-authored TWICE (sheets → roads, then roads spread across the
  whole shell at a third the prism size), so nothing about it is play-tested.
- **The bank ceiling is authored, not enforced.** Every ribbon's `rise x harmonic` is held under
  ~0.4 of its radius so the deck banks at most ~22°, which is what keeps this rung from quietly
  becoming a second Twistbands. Nothing asserts it — a bigger `RiseAmp` or harmonic will simply
  make the road roll, and the only signal is a playtest that says it stopped being a joy.
- **The volume phase ladder is frozen on intensity 2** — float32 cannot register a 4.5-volume
  trail prism against a 103M baseline (ulp 8). It used to be frozen on rung 1 as well and the prism
  dial fixed that one (345M → 38.5M, ulp 4). Harmless while the cell grows nothing, and gated by
  `cleave_budget` check 7 per rung so it fails loudly the day it stops being harmless. See "Stated
  cost" above.
- **⚠ The collider ladder is now FLAT and close to its ceiling** — 15,380 / 14,277 / 14,731 /
  16,423 against the 20,153 the mode has already shipped, i.e. 23% of headroom where the previous
  pass had 4.5×. Nothing is over the gate, but there is no cheap rung left: the next arena change
  on rungs 1 or 2 has to state its prism count before it is authored, and the lever if it needs to
  come down is a road's LANE count or the Panes' `GapScaleI1`, not the prism dial (which is now
  fleet-wide across the mode).
- **Toast copy is unauthored.** The three `GameToastSituation` values exist but no
  `GameToastConfigSO` authors a definition, so they are silently skipped (which is how a mode opts
  out). Author `GameToastConfig_Cleave.asset` with `{0}`=domain, `{1}`=prisms destroyed,
  `{2}`=target to make them visible.
- **`wildlife_cage_budget.py` still carries the doubled jitter factor** — see above. Out of scope
  here, but it is a live tuning defect in Wildlife Liberation's cell.
- **No objective-arrow provider**: like Rampage, `MiniGameHUD.CreateObjectiveProviderForGameMode`
  has no Cleave case — the arena surrounds you, so there is no single point to aim at.
- **No UGS stats reporter yet**, and no dedicated end-game controller — the shared scoreboard
  handles it.
- **Danger placement is a first pass in all four arenas** (pane rims, swell crests, twistband
  cornices; the cage's per-rib walk is unchanged). If they read as noise rather than as traps,
  cluster them instead — one constant per arena.
- **The harness needs a dotnet 8 SDK.** The measurement JSON is committed so nothing routine requires it,
  and the hash guard makes a stale measurement loud rather than silent — but re-tuning an arena
  does require a .NET SDK.
- **Several kept platform APIs now have no caller** (the fauna-containment family), documented above.
