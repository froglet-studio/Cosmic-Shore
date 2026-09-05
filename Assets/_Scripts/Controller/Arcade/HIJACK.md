# Hijack — the Urchin heist race (`GameModes.Hijack = 45`)

**Urchin-only. First DOMAIN to steal 1,500 prisms wins.** Nothing in this mode is ever
destroyed: mass only changes hands.

Three great-circle **rails** ring a hollow core, meeting at spiny **burrs** of raw prism where
the rings cross, with twelve smaller burrs strung along the arcs. Every rail is painted in three
domain thirds and every burr wears one colour, so nothing in the yard is anyone's for long. You
latch onto a rail and grind it — **150 u/s where it wears your colour, a stealing crawl at 10
where it does not** — spike the road ahead to make it yours, fly off the open end at full grind
speed straight into the burr that rail points at, and rake it with a chain cascade. Then bank
onto the next rail before a rival takes it back.

---

## 1. Why this mode exists

The Urchin's three verbs — **attach to a rail**, **launch off its end**, **steal clusters of
prisms** — are all shipped platform behaviour and none of them had an arena built to reward
them. Hijack is that arena, and it adds no new vessel mechanics at all: every verb below is the
Urchin's own, applied to geometry shaped to invite it.

| verb | the vessel's own machinery | what the arena does about it |
|---|---|---|
| ATTACH | `TrailFollower` 1D grind, routed by `PrismscapeTopology.DimensionOf` | 24 open ribbons, so there is always one in reach |
| CONVERT | `GunVesselTransformer.ApplyPrismscapePayoff` steals every hostile prism ridden | every rail is three domain thirds, so a raid is always available and always slow until you spike it |
| LAUNCH | `GunVesselTransformer.LaunchOffRibbonEnd` + carried speed | every rail's far end is a real end, and a burr sits exactly on the tangent it throws you along |
| STEAL | the chain-spike cascade | a burr is a few hundred prisms in ONE colour, so one volley is worth a hundred |

---

## 2. The loop

**0:00–0:10** — spawn on the equatorial ring at r = 1120 facing the core. A rail crosses your
path a couple of hundred units ahead; the goal row reads `STEAL PRISMS 0/1500`; the arrow points
at the nearest burr still holding mass you could take.

**0:10–0:30 — the arena is the tutorial.** Fly into the rail and you attach. On your colour's
third you grind at 150. Crossing into a hostile third reads as **braking to 10** while the goal
row ticks up as you crawl — that brake *is* the lesson that stealing is the score. Tap the spike
trigger: the prisms ahead flip to your colour and the speed snaps back. The rail runs out and you
**LAUNCH** at 150, aimed by construction at the burr ~200u ahead. Tap again mid-air — a spike's
velocity is `direction × speed + the vessel's`, so a volley thrown at grind speed reaches roughly
3.5× further than one thrown at cruise — and the cascade rolls through the cluster.

**0:30 onward — three exits, all readable from the arena.** Bank inward and catch the next rail
on this ring; at a big burr, turn 90° onto the crossing ring; or fly straight into the burr and
**marble-roll over its spines** (yours grow under you, hostile ones flip one per hop). Riding
recharges spike ammo, so time on a rail is what pays for the next volley.

**The yard changes colour under everyone.** Your fast lanes are the rails you own; a rival on
your rail crawls unless they spike it first, and every prism they crawl over is one of yours
flipped back. A re-steal credits the re-stealer and debits nobody, so two Urchins farming one
burr gain nothing on each other — **the winning play is to be where the rival isn't**, and the
launch network gets you there faster than cruising.

---

## 3. The arena: the Switchyard

`SpawnableSwitchyard : CellEnvironmentSpawnableBase`. **Closed form — there is no
`System.Random` draw anywhere in `BuildEnvironment`**, which is what lets
`Tools/Build/hijack_budget.py` MIRROR it exactly rather than estimate it, and what lets
`author_hijack_assets.py` derive the cell's `PhaseThresholds` from the same numbers that build
the arena. The inherited `seed` and `density` knobs are inert here by design.

### Rings and rails

Three great circles of radius **900**, in the XY, YZ and ZX planes, parametrised so a 120°
rotation about (1,1,1) maps ring *k* to ring *k+1* — which is what makes the painting below
provably 3-fold symmetric rather than symmetric-looking.

Eight **stations** per ring at 45° intervals. Even stations are the ring's four axis crossings
(six unique points, each shared by two rings) and carry **big burrs**; odd stations carry
**small burrs**, twelve in all.

**24 rails, one per (ring, station).** Each is the arc from θⱼ + 12.5° to θⱼ₊₁ − 12.5° — 20° of
arc, 314u — laid as **40 prisms of scale (3, 3, 6)**. That is the Track Projector's exact prism,
so a rail a pilot projects reads as arena rail. Rotation is `LookRotation(arc tangent, radial
out)`, so local **+Z runs along the rail** (the invariant the 1D ride rests on). Each rail is its
own `new Trail(isLoop: false)` — **open, so it launches**.

### The launch contract, exact

A circle's tangent at an angle *g* short of a station passes through that station's **radial** at
radius `R / cos g`, a distance `R · tan g` further along. So:

| | |
|---|---|
| burr centre, from the core | `900 / cos 12.5° =` **921.9u** |
| rail end to burr centre | `900 · tan 12.5° =` **199.5u** |

**Every burr centre is placed at exactly that radius.** A launched pilot who does not steer flies
straight into the burr. *This is the whole answer to "how is the launch rewarded" — by geometry,
not by a bonus.*

**Prism spacing is DERIVED, and that is load-bearing.** The 40 prisms span the arc **endpoint to
endpoint** (8.0554u apart), so the terminal prism sits exactly on the tangent that aims at the
burr. Authoring a round 8.0 spacing centres 312u of prisms inside a 314u arc, insets the terminal
prism by ~1u, and tilts the launch **0.32°** off the burr. That was caught by
`prove_launch_geometry()` before a line of the C# was written; do not "tidy" the spacing back to
a round number without re-running it.

### Burrs

Concentric Fibonacci shells at radius `10·s`, `round(4πs²)` spines on shell *s*, each spine's
local **+Z radial out** (a 6-long spike, and the flat outward face a marble roll lands on). Each
burr is one `Trail { Dimension = Volume }` — a solid, ridden on its boundary, which is what it
honestly is.

| intensity | big burr | small burr | burr prisms | rail prisms | total | volume |
|---|---|---|---|---|---|---|
| 1 | 176 (r 30) | 63 (r 20) | 1,812 | 960 | **2,772** | 149,688 |
| 2 | 377 (r 40) | 63 (r 20) | 3,018 | 960 | **3,978** | 214,812 |
| 3 | 691 (r 50) | 176 (r 30) | 6,258 | 960 | **7,218** | 389,772 |
| 4 | 1,143 (r 60) | 176 (r 30) | 8,970 | 960 | **9,930** | 536,220 |

**Intensity scales burr mass and NOTHING else.** The rail network, the radii, the launch gaps and
the spawn ring are identical at every level, so the arena's shape, its aiming and its spawn
geometry never move. A bigger yard is a **longer, more contested** match at a fixed target, not a
scarcer one.

### Painting — the full triad, exactly equal per domain, no Blue

- **Rail (k, j) in three THIRDS** from its low-θ end (14/13/13 prisms), the run rotated by
  `(j + k)`. So **every rail offers every domain a fast third** — fair from any spawn slot — the
  speed cliff at each boundary is the mode's own tutorial, and each domain owns exactly **320**
  rail prisms.
- **Big burr** at the crossing of rings *k* and *k′* wears the THIRD domain — **always hostile to
  both rings that launch into it**, so the biggest prizes are contested by construction. Two each.
- **Small burr** wears `D[(k + (j−1)/2) % 3]`. Four each.

**A two-domain lobby** finds the third colour's mass hostile to both sides and, by the 3-fold
symmetry, equidistant from both: symmetric unclaimed loot with no asymmetry to patch. *Rejected:
painting it `Domains.Blue`.* Blue is a real domain to the ride, and the triad construction already
gives a symmetric neutral set without introducing a fourth reading.

### Cell

No `NucleusPrefab` — no control zone, and nothing here reads `DominantDomain`. The mode's
territory IS the mass, which is the point: a nucleus would add a fauna-sanctuary rule to an arena
whose every prism is meant to be takeable (`Docs/ECOSYSTEM.md §25.1`). Membrane and cytoplasm are
the shared prefabs; `SenseRadiusOverride 1200`; one omni crystal idling in the hollow core at
`noNucleusSpawnRadius 300`.

**That crystal is not the objective** — it is an elemental pickup a pilot may take in passing, and
the mode's arrow deliberately points at burrs instead (`HijackObjectiveProvider`). The radius must
be authored because this cell has no nucleus: without it the crystal falls through to its own
`SphereRadius` and respawns on the arena's exact centre, which is the defect Dog Fight recorded.

### Orientation and spawn

The whole yard is rotated **22.5° about world Y** as the last build step, so the equatorial spawn
ring lines up with rail midpoints rather than the gaps between them. Players spawn through
`arrangeSpawnPointsAroundCell` + `spawnFormation EquatorialRing` + `spawnRingRadiusFloor 1120` —
**equatorial, not the default symmetric sphere**, for the same reason Peel the Cage is: the yard's
rails ring the core, so a polar spawn slot would face no rail at all.

Outermost mass reaches **985u** < spawn ring **1120** < membrane **1200**. All three are asserted
against each other by `hijack_budget.prove_extent()`.

---

## 4. Scoring

**Metric: `ScoringMetric.PrismsStolen = 9`** → `IRoundStats.PrismStolen`. That stat already
existed and is already credited on both sides of the wire — `StatsManager.PrismStolen` for every
host-simulated pilot (which covers every AI), and `Player.ReportPrismStolen_ServerRpc` for a
client's own steals. **No new gameplay plumbing: the stat has been accumulating in every mode
since long before a mode read it.** Hijack is simply the first to score it.

It is the first metric whose source is **ownership** rather than destruction, and that is what
lets a whole mode be played inside the conserved-mass law with no food web and no despawn.

**COUNT, not volume.** Every prism in the yard is 54 volume, so volume adds nothing — and volume
would quietly pay a re-stealer MORE than the original thief, because a friendly ride `Grow`s a
prism. A count is also the only thing a goal row can say.

- **Rule:** `HijackScoringRuleSO : RampageScoringRuleSO`, `metric 9`, golf-timed. Winners carry
  their finish time; everyone else a sentinel encoding their team's remaining steals. Overrides
  only the wording ("HEIST TIME" / "LEFT TO STEAL", "n Stolen") and the teammate tiebreak, which
  moves from Rampage's destruction count (a flat zero here) to the live metric.
- **Turn monitor:** `HijackStealTurnMonitor`, reading `EndConditionOverridesSO.GetHijackStealTarget()`
  → NetworkVariable → `GameDataSO.PrismTargetCount`.
- **Goal row:** one new `ObjectiveIconSet` entry (`metric 9`, "Steal prisms") drives
  `STEAL PRISMS 340/1500` through the existing GoalStack with zero HUD code. A new metric is the
  one thing that needs new art; the glyph is the family's own prism silhouette, solid behind a
  chevron front and hollow ahead of it.
- **Target 1,500.** Explicitly **unmeasured** (the Salvo precedent), sized against the
  intensity-1 yard's 2,772 prisms of which ~1,848 are hostile to any one domain. One editor field
  is the dial.
- **Comeback rate 0.008** → a quarter-of-target deficit (375) buys **3.0** element levels. The
  generator FAILS the build if a retune ever drops that under one whole level — the trap Dog
  Fight, The Bends and Wildlife Liberation have each recorded independently.

### Why the launch pays nothing bespoke

No per-launch bonus, no airtime multiplier. It pays through **geometry** (burrs sit on rail-end
tangents), **physics** (a spike's velocity composes with the vessel's, so a volley thrown at
grind speed reaches ~3.5× further) and **economy** (only riding banks ammo). A per-launch bonus
would score the *record of a manoeuvre* rather than its effect — the scripted-outcome cheat. Dog
Fight and Bends weight distinct scoring EVENTS (a bullet against a missile, a debuff); the
analogue here is the prism itself, and **a steal is a steal**.

---

## 5. Why no fauna

`SpawnProfileSO` authors no flora and no fauna, and the reason is the **comeback**, not an
omission worth fixing.

In a nucleus-less cell herbivores eat **opposing-domain** mass, and the leader's colour is by
definition the most abundant — so a swarm would preferentially eat whatever the **trailing** team
had just stolen. An anti-comeback current is the wrong current in a mode whose entire economy is
contested ownership. The profile asset is the one-file door if it is ever wanted.

*(Rejected: a `HalfNucleus`-with-empty-core trick to get "colour-blind" grazing. Fauna spawn in
the cell's `ControllingDomain`, which falls back to the leading roster team, then the local
pilot's domain, then Jade — **never Blue** — so the trick does not do what it claims.)*

---

## 6. The AI

Hijack is **not** in `ServerPlayerVesselInitializerWithAI`'s seek-players set.
`HijackController.ArmRaiders` installs one closure per AI at `OnCountdownTimerEnded`, steering
through `AIPilot.SetExternalTargetProvider` and driving abilities through
`R_VesselActionHandler.PerformShipControllerActionsReplicated` (the host owns every AI, so the
press replicates and every peer runs the same deterministic cascade).

Three states, and every one of them targets a point **beyond** the thing it wants, because
`AIPilot` has no arrive-and-stop behaviour and a target inside its own minimum turn radius
becomes something it orbits (`Docs/AI_ORBIT_BREAK.md`):

1. **APPROACH** — aim short of a chosen rail's near end while far out, then aim THROUGH the rail
   once close. Arriving along the ribbon is what makes `TrailFollower.Attach` seed its travel
   direction toward the far end instead of back the way it came.
2. **RIDE** — keep aiming past the far end so the nose stays down-rail, and tap the spike trigger
   when the prism underfoot is not its own and the ammo meter can pay.
3. **RAID** — past the far end, head for the burr that rail aims at and rake it, flying through
   the centre so the pass does not become an orbit. Reached both ways: launched and still in the
   air, and attached to the cluster itself.

**The RIDE state is "riding the rail I chose", not "attached to something".** That distinction is
load-bearing and getting it wrong cancelled the whole raid: a burr is attachable too — its prisms
carry a `Volume` trail and the surface follower keeps `IsAttached` true — so a raider that reached
the cluster its rail aimed it at was pinned in the ride branch, steered *back at the rail it came
from*, and blocked from re-picking for as long as it stuck to the burr. The test is
`AttachedPrism.Trail == the chosen rail's Trail`, which the yard already stores.
*General shape: when two different structures can put a vessel in the same STATE FLAG, the flag is
not the state.*

Rail choice is `loot / (1 + distance/300) × (own fraction + 0.3)` — how much is in the burr at the
far end, how far the rail is, and how much of it is already yours. Loot is counted **once per
burr, not once per rail**: two rails launch into every big burr and a burr is up to 1,143 prisms,
so the naive per-rail walk costs ~27k prism reads per pilot per refresh to answer 18 questions.

**The stall escape catches a PARKED ride, not a slow one.** `aiParkedSpeed` is 6 u/s, deliberately
under the 10 u/s hostile crawl: a crawler is converting one prism per hop and will cross a
13-prism third in about ten seconds, which is a raid in progress and must never be read as a
stall. What the escape is for is a ride that has genuinely stopped — a reversal caught in the
throttle deadband, a ribbon whose prisms were taken out from under it. When it fires it excludes
that rail for `aiSlippedRailCooldown`, because the scoring is a pure function of position and
domain and would otherwise re-pick the abandoned rail on the very next frame.

**Overriding crystal seeking is correct here and would be a defect in Rampage.** The prohibition
Rampage records is about a mode whose OBJECTIVE is a crystal; this mode's objective is a burr, and
an AI that spent the match orbiting the core crystal would steal nothing.

### Why the Urchin needed `ram`

**One authored field is load-bearing: `ram: 0 → 1` on `Urchin.prefab`'s AIPilot** — the **Rhino's**
shipped value, so this is a fleet precedent rather than an invention.

`AIPilot` writes `XDiff = (LookingAtCrystal && ram) ? 1 : throttle`, and
`GunVesselTransformer.ReadThrottle` is SIGNED around a 0.5 rest. So the Urchin's authored
`defaultThrottle 0.6` reads as **+0.2 signed throttle = 30 u/s on a friendly rail — below its own
50 u/s cruise**. An AI Urchin would grind slower than it flies and carry nothing off a launch.
With `ram: 1`, an AI whose course is on target (which a rider always is) grinds at the full 150.

It is AI-only, so it changes nothing for a human pilot in any mode.

---

## 7. Budget and collider impact

`Tools/Build/hijack_budget.py` is the mirror, and `author_hijack_assets.py` imports it — the same
discipline as `boneyard_budget.py` and `ribcage_budget.py`. Running it prints the table above and
runs six proofs: the launch aim, the launch gap, rail separation, burr clearance, the arena
extent against the spawn ring and membrane, and the painting balance.

- **Colliders: every prism is `PrismKind.Plain`.** Zero always-on mesh colliders are authored —
  no shielded, super-shielded or danger mass anywhere — so the active count is bounded by
  `PrismColliderLodManager`'s radius rather than by the 2,772–9,930 population.
- **The one uncapped collider source is a player.** A MASS-5 Urchin's ride comes up SHIELDED,
  and a shield reaches `1.5 × leafSize` = 9u along a 6-long prism at 8.06u spacing — so armoured
  neighbours on one rail interpenetrate visually and each carries a mesh collider. Bounded by how
  much rail one pilot can ride, and it is a player act on the player's own mass.
- **Peak arena 9,930 prisms / 536k volume** at intensity 4 — the same order as the Boneyard
  (9k–35k) and well under Atlantis (~69k).
- **Growth headroom.** This is the first mode where a vessel ability is a mass **source** with no
  food web to remove it (a friendly ride `Grow`s every prism crossed). `PhaseThresholds` are
  authored as measured baseline + the standard Blob deltas, and it is worth stating that the
  ladder gates nothing here beyond collider-LOD-by-phase, because the SpawnProfile is empty.

---

## 8. Files

| what | where |
|---|---|
| mode controller | `_Scripts/Controller/Arcade/HijackController.cs` |
| scoring rule | `_Scripts/Controller/Arcade/Scoring/HijackScoringRuleSO.cs` |
| turn monitor | `_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs` |
| objective arrow | `_Scripts/Controller/Arcade/HijackObjectiveProvider.cs` |
| arena generator | `_Scripts/Controller/Environment/MiniGameObjects/SpawnableSwitchyard.cs` |
| the arena's map of itself | `_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs` |
| budget + geometry proofs | `Tools/Build/hijack_budget.py` |
| asset generator | `Tools/Build/author_hijack_assets.py` |
| scene | `_Scenes/Multiplayer Scenes/MinigameHijack.unity` |
| card | `_SO_Assets/Games/ArcadeGameHijack.asset` |
| cell configs | `_SO_Assets/Cell Configs/Switchyard Cell/` |
| spawnable variants | `_Prefabs/Spawnables/SpawnableSwitchyard{1..4}.prefab` |

---

## 9. Verification status

**Nothing below has been run in the Unity editor** — this branch was built headlessly. Every
item is a real check a human has to perform, in this order (load-bearing first).

1. **Open `MinigameHijack.unity`.** Every script reference resolves; the Cell shows four
   Switchyard configs on Intensity Wise; the controller shows `rule` = HijackScoringRule.
2. **STEALING SCORES.** Grind onto a hostile rail third — the goal row ticks up as you crawl. Tap
   the spike trigger into a hostile burr — the row jumps 100+. Riding your OWN colour scores
   nothing.
3. **THE LAUNCH IS AIMED.** Grind a rail to its end without steering. You must launch and fly
   into the burr. If you have to steer, the tangent geometry is wrong (re-run
   `hijack_budget.py`).
4. **THE SPEED CLIFF READS.** 150 on your third, a visible brake to 10 on a hostile one, snapping
   back after a spike tap.
5. **Roll a burr** — you attach and marble-roll the spines; yours grow, hostile ones flip one per
   hop.
6. **Win + scoreboard.** First domain to 1,500 ends the turn; "HEIST TIME" for the winners.
7. **AI plays.** AI Urchins grind rails at full speed (the `ram: 1` check), launch off ends, and
   their domain's score climbs. They must not orbit pilots or converge on the core crystal.
8. **Comeback.** Fall ~375 behind: the trailing pilots' element flowers fill ~3 levels; at Time 5
   they ride hostile rails at full speed.
9. **Regression.** The Urchin still flies correctly in freestyle (the `ram` change is AI-only).
10. **HOST-SIDE FIRST, then a client.** Prism ownership does not replicate (§10), so the two
    machines will disagree about which rail thirds are fast and which burrs still hold loot. The
    SCORE should agree; the arena will not. Confirm the score does.

---

## 10. Known limitations

- **PRISM OWNERSHIP DOES NOT REPLICATE, and this mode is the first whose whole subject is
  ownership.** `PrismTeamManager` is a plain `MonoBehaviour`: `ChangeTeam` / `Steal` mutate a
  local field and raise a SOAP event, with no NetworkVariable and no RPC anywhere. Every peer
  builds a byte-identical yard (the generator is closed form) and then diverges from the first
  steal. **The SCORE is correct on every peer** — that rides the owner-detects / server-records
  round trip and was traced end to end — but three reads are per-machine:
  ride speed (`TrailFollower.GetTerrainAwareBlockSpeed`, so two pilots disagree about which
  thirds are fast), the objective arrow (`HijackYard.HasHostileMass` walks the local trail), and
  the AI's rail choice — which runs server-side and never sees a client's steals, so AI raiders
  keep attacking burrs a human client already emptied. This is a platform gap rather than
  something the mode introduced (`CLAUDE.md` records the destruction half of it), but Hijack is
  where it stops being academic. **Play-test it host-side first.**
- **The Urchin has no HUD prefab.** There is no `UrchinHUDVariant.prefab` and the vessel wires
  none, so an Urchin-only mode ships with no ability lockup row, no elemental petal bars, no
  control chips and **no ammo gauge** — while the pilot's only weapon is gated on exactly that
  meter. It is also noisy: `VesselStatus.VesselHUDController` logs an error whenever the field
  does not implement the interface, which includes null, and it is read on every vessel spawn and
  every HUD hide/show. Latent and not reachable from this mode: four call sites in
  `VesselController` dereference the same getter unguarded, so any mode that calls `ChangePlayer`
  on an Urchin (today only Cellular Duel's ownership swap) would throw and leave the vessel
  uncontrollable.
- **`ram: 1` is a FLEET-WIDE AI change made for one mode.** `Urchin.prefab` is shared, so every
  AI Urchin in every context — the menu lava-lamp autopilot, the Lifeform Matrix's vessel hangar,
  any future mode that does not lock its hull — now flies at full throttle whenever it is lined
  up on its objective, not just here. It has the Rhino's precedent and it is AI-only, so no human
  pilot is affected; if it ever needs to be narrower, the honest lever is a per-mode setter rather
  than a prefab field.
- **1,500 is unmeasured**, and so is the intensity ladder's effect on match length. Intended
  3–5 minutes; the target is one editor field.
- **Mass-5 armour on rail prisms** is the one uncapped collider source — see §7.
- **No `ModePreview_Hijack.asset`**, so the arcade card shows "LEVEL PREVIEW NOT AVAILABLE".
  Salvo ships the same way, so this is a gap rather than a regression.
- **Not suppressed while riding: `AIPilot`'s orbit-break.** `UpdateOrbitBreak` exempts drifting
  but not attachment, and `breakOrbits` defaults on. If it ever fires mid-grind it drops
  `LookingAtCrystal` (and with it `ram`, so 150 → 90 or 10 → 6) and swings the nose off-rail.
  A 20° arc supplies nowhere near the 540° of sweep the detector wants, so it is very unlikely to
  trip — but it is unproven rather than ruled out, and the cheap guard is one clause.
