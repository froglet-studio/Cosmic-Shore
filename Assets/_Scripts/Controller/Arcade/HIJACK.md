# Hijack — the Urchin heist race (`GameModes.Hijack = 46`)

**Urchin-only. First DOMAIN to steal 750 prisms wins.** Nothing in this mode is ever
destroyed: mass only changes hands.

Three great-circle **rails** ring a hollow core, meeting at spiny **burrs** of raw prism where
the rings cross, with twelve smaller burrs strung along the arcs. Every rail is painted in three
domain thirds and every burr wears one colour, so nothing in the yard is anyone's for long. You
latch onto a rail and grind it — **300 u/s where it wears your colour, a stealing crawl at 20
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
path a couple of hundred units ahead; the goal row reads `STEAL PRISMS 0/750`; the arrow points
at the nearest burr still holding mass you could take.

**0:10–0:30 — the arena is the tutorial.** Fly into the rail and you attach. On your colour's
third you grind at 300. Crossing into a hostile third reads as **braking to 20** while the goal
row ticks up as you crawl — that brake *is* the lesson that stealing is the score. Tap the spike
trigger: the prisms ahead flip to your colour and the speed snaps back. The rail runs out and you
**LAUNCH** at 360 (the grind's 300 times the 1.2x end-of-ribbon kick), aimed by construction
at the burr ~200u ahead. Tap again mid-air — a spike's
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
**equatorial, not the default symmetric sphere**, for the same reason Cleave is: the yard's
rails ring the core, so a polar spawn slot would face no rail at all.

Outermost mass reaches **985u** < spawn ring **1120** < membrane **1200**. All three are asserted
against each other by `hijack_budget.prove_extent()`.

---

## 4. Scoring

**Metric: `ScoringMetric.PrismsStolen = 10`** → `IRoundStats.PrismStolen`. That stat already
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
  `STEAL PRISMS 340/750` through the existing GoalStack with zero HUD code. A new metric is the
  one thing that needs new art; the glyph is the family's own prism silhouette, solid behind a
  chevron front and hollow ahead of it.
- **Target 750.** Explicitly **unmeasured** (the Salvo precedent), sized against the
  intensity-1 yard's 2,772 prisms of which ~1,848 are hostile to any one domain — so the target
  is 41% of what one domain can take and the yard is far from exhausted at the whistle. One
  editor field is the dial. **HALVED from 1,500** on request; the rate below moved with it.
- **Comeback rate 0.016** → a quarter-of-target deficit (187) buys **3.0** element levels. The
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
under the 20 u/s hostile crawl: a crawler is converting one prism per hop and will cross a
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
`defaultThrottle 0.6` reads as **+0.2 signed throttle = 60 u/s on a friendly rail — below its own
65 u/s cruise**. An AI Urchin would grind slower than it flies and carry nothing off a launch. (Both
numbers doubled and rose 30% respectively when the Urchin was retuned; the ORDERING that makes
`ram: 1` load-bearing is unchanged, because both sides scaled together.)
With `ram: 1`, an AI whose course is on target (which a rider always is) grinds at the full 150.

It is AI-only, so it changes nothing for a human pilot in any mode.

**The spike and Slip presses go through the shared Urchin driver** (`UrchinAutopilotDriver`,
`_Scripts/Controller/AI/Urchin/`), the same one Skein flies its strands with. It finds each control
by CAPABILITY on the vessel's own bindings (`TryGetBoundAction<UrchinSpikeActionSO>` /
`<UrchinSlipActionSO>`) rather than by a named trigger, and gates a tap on the spike ability's own
`AmmoIndex` / `AmmoCost` - the two numbers the executor's `CanPay` checks - on top of this mode's
`aiMinSpikeAmmo` floor, so a re-bound or retuned Urchin cannot leave the raider pressing a trigger
that no-ops. The cadence and floor are still this controller's authored fields. Since #986 the
driver's own ride logic (Skein, Regatta) spikes only CONVERTIBLE hostile mass -
`UrchinAutopilotDriver.IsConvertible` skips super-shielded prisms, which `PrismTeamManager.Steal`
always refuses, so a volley there would only spend the meter. Hijack does not route through that
gate: it calls `TrySpike` directly after its own hostile test (`IsHostileUnderfoot`, read from the
replicated yard table), and the yard's rails and burrs are never super-shielded (a Mass-5 shield
is an ordinary shield, which a steal drops rather than flips), so the rule changes nothing here
today. If the yard ever authors super-shielded mass, add `IsConvertible` to that test. The driver's rail
CHOICE (ride / reverse / leave) is not used here: this yard's rails are 20 degree arcs chosen by
`ChooseRail`, and the RIDE state already rides each one to its end. Its Track Projector is not
used here either, deliberately: the yard is under 1,850 u across, and a projected track's 360 u/s
launch overshoots the 80 u approach-commit window every rail approach depends on.

---

## 7. Budget and collider impact

`Tools/Build/hijack_budget.py` is the mirror, and `author_hijack_assets.py` imports it — the same
discipline as `boneyard_budget.py` and `cleave_budget.py` (which replaced the deleted
`ribcage_budget.py`). Running it prints the table above and
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
| replicated prism ownership | `_Scripts/Controller/Arcade/HijackOwnershipLedger.cs` (RPCs on `HijackController`) |
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
4. **THE SPEED CLIFF READS.** 300 on your third, a visible brake to 20 on a hostile one, snapping
   back after a spike tap.
5. **Roll a burr** — you attach and marble-roll the spines; yours grow, hostile ones flip one per
   hop.
6. **Win + scoreboard.** First domain to 750 ends the turn; "HEIST TIME" for the winners.
7. **AI plays.** AI Urchins grind rails at full speed (the `ram: 1` check), launch off ends, and
   their domain's score climbs. They must not orbit pilots or converge on the core crystal.
8. **Comeback.** Fall ~375 behind: the trailing pilots' element flowers fill ~3 levels; at Time 5
   they ride hostile rails at full speed.
9. **Regression.** The Urchin still flies correctly in freestyle (the `ram` change is AI-only).
10. **OWNERSHIP AGREES ACROSS MACHINES (host + one client, ideally MPPM or two builds).**
    a. On the CLIENT, grind a hostile rail third and spike a burr. On the HOST, the same prisms
       turn the client's colour within ~0.1s, and the host's own ride over them is fast.
    b. On the HOST, steal a burr. On the CLIENT the burr flips, and the client's arrow moves off
       it to the next cluster that still holds loot.
    c. Watch a remote pilot grind a hostile rail on your screen: its prisms flip as it passes and
       STAY flipped (no flicker back). If a flip snaps back after about half a second, the
       owner's report is not reaching the server — check the console for RPC errors.
    d. AI raiders (host-run) stop targeting a burr a CLIENT emptied: empty one, then watch which
       rail the AI picks next.
    e. **Late join:** start a match host-only, steal a burr or two, then join a client mid-match.
       Its yard must show those burrs in the host's colour once laid, not their painted colour.
    f. Score still agrees on both machines (unchanged path).
    g. **Shields agree.** Give the CLIENT pilot Mass 5 and ride one of its own rail thirds: the
       prisms come up shielded on the client and, within ~0.1s, on the HOST. Now ride a host AI
       (or the host pilot) over that third: the first pass only breaks the shields (prisms stay
       the client's colour, on both screens), the second pass flips them. Repeat with roles
       swapped (host Mass 5, client steals).
    h. **Late join keeps shields:** with some Mass-5 armour laid, join a second client - its yard
       shows the same prisms shielded.

---

### The three registry IDs, and why 46 rather than 45

A mode claims a slot in three enums that every other mode also lives in — `GameModes` (46),
`ScoringMetric` (10) and the comeback's `ScoreDifferenceSource` (9, retired 2026-09 — the comeback
now reads the rule). **None of the
three fails loudly on a double-claim.** C# lets two members share a value, so a second mode
taking the same number *compiles*: `GameModes.Hijack` would `==` the other mode, every switch
over it ambiguous, and the two metrics silently reading each other's stat. Only `GameModes` has
a tripwire at all (`EnumIntegrityTests.GameModes_HasExpectedMemberCount`), and it catches the
member COUNT rather than the collision.

45 / 9 / 8 were taken first by the in-flight **Switchback** branch, which was further along, so
Hijack ceded rather than double-claim — a collision that compiles is a worse failure than a gap
in an enum. The three numbers now live in one place at the top of
`Tools/Build/author_hijack_assets.py` (`MODE_ID` / `METRIC_ID` / `COMEBACK_SOURCE`), and the
generator's `--check` holds the C# to them, so moving off the next collision is a three-line
edit rather than a hunt through five authored assets. If Switchback never lands, 45 is simply
free for the mode after this one; it carries none of the permanent reservation 7 and 31 do.

### The arcade grid had to grow before this card could render

`ArcadeExploreView`'s grid is AUTHORED at a fixed 3 × 4 = 12 slots in Menu_Main and the populate
loop was bounded by it, so a roster larger than the grid truncated **silently** — the
alphabetically-last modes stopped existing in the arcade with no error and no gap to notice.
Menu_Main sat at exactly 12 renderable cards (13 games minus the Maelstrom, which the grid
deliberately excludes), so Hijack is the 14th game and the 13th renderable one: shipping it
pushes a card off the end.

`EnsureGridCapacity` / `GrowScrollContent` / `RowsNeeded` come from the Switchback branch
verbatim (commits `dc3dcb65`, `b2b63c8e`) rather than being re-derived here — the fix is
mode-agnostic, both branches trip the same ceiling, and taking the identical blob means the two
merge without a conflict in that file. Do not edit it on this branch for that reason.

## 10. Known limitations

- **Prism ownership replicates for the YARD only, and the trust unit is a domain, not a pilot.**
  Fixed 2026-10 (`HijackOwnershipLedger`, §11). Platform ownership (`PrismTeamManager`) is still
  local everywhere else — this mode did not change it — so anything outside the Switchyard's 42
  trails (an Urchin's own trail, if it ever lays one here) still diverges per peer. Within the
  yard, a peer believes a flip into a domain it simulates a pilot of, so a peer also believes its
  PROXY of a remote teammate; that can make a teammate's steal land early, never hand a prism to
  the wrong team. Shield state rides the same table since 2026-10 (§11), with the same trust unit:
  a peer that simulates any rival of a prism's owner believes a proxy's break of that owner's
  shield. The SCORE path is unchanged and was already correct on every peer.
- **The Urchin HUD is placeholder art.** Since #973 (2026-10) `Urchin.prefab` wires
  `VesselStatus.vesselHUDController` to an `UrchinVesselHUDController` driving a nested
  `UrchinHUDVariant.prefab` (a Prefab Variant of `VesselHUDPrefab`, authored by
  `Tools/Build/author_urchin_hud.py`): the four-icon row Chain Spikes / Trail Rider / Track
  Projector / Slip with RT / LT / B chips, the elemental petal bars, the **ammo gauge** on the
  Charge card (the meter this mode's only weapon spends), the riding indicator on Mass and the
  Track Projector's recharge veil on Space. The four icons are white placeholder silhouettes
  awaiting the art pass, and the Chain Spikes hold-to-charge has no gauge yet (the executor does
  not expose charge progress). The old "no HUD" state also logged `VesselHUDController is null`
  on every spawn; that is gone with the wiring.
- **`ram: 1` is a fleet-wide AI field, and it was audited (2026-10) rather than narrowed.**
  `Urchin.prefab` is shared, so every AI Urchin flies at full throttle whenever it is lined up on
  its objective. Every context one flies in WANTS that: Skein and Regatta both aim an attached AI
  down its own rail precisely so `LookingAtCrystal` holds and `ram` keeps the grind at full speed
  (without it the ride runs at the authored `defaultThrottle 0.8` = +0.6 signed, 180 u/s on a
  friendly rail); Broadside's opponent lock lands a hit by ARRIVING; and the menu / hangar
  autopilot only chases crystals, where full speed is merely faster (the orbit break handles the
  wider turning circle). No mode was found where it hurts, so a per-mode setter would add a
  second authority for no behaviour change. If one is ever needed, it belongs in `AIPilot` as a
  runtime override, not as a second prefab.
- **750 is unmeasured**, and so is the intensity ladder's effect on match length. It was halved
  from 1,500 for pace without re-measuring either, so the intended length is now roughly half of
  the original 3–5 minute estimate — which is itself an estimate. The target is one editor field.
- **Mass-5 armour on rail prisms** is the one uncapped collider source — see §7.
- **The mode preview exists**: `ModePreview_Hijack.asset` (authored by
  `Tools/Build/author_mode_previews.py`). The Switchyard is an authored EnvironmentPrefab per
  intensity, so the scale model shows the rings and burrs, and the flight preview lets the Urchin
  grind a rail and launch into a burr.
- **Not suppressed while riding: `AIPilot`'s orbit-break.** `UpdateOrbitBreak` exempts drifting
  but not attachment, and `breakOrbits` defaults on. If it ever fires mid-grind it drops
  `LookingAtCrystal` (and with it `ram`, so 150 → 90 or 10 → 6) and swings the nose off-rail.
  A 20° arc supplies nowhere near the 540° of sweep the detector wants, so it is very unlikely to
  trip — but it is unproven rather than ruled out, and the cheap guard is one clause.

## 11. Replicated prism ownership

The mode's subject is who owns each prism, and ownership was local — every peer built the same
yard and diverged from the first steal, so ride speed, the arrow and the AI's rail choice all
disagreed between machines. `HijackOwnershipLedger` (plain C#) plus four RPCs on
`HijackController` (the mode's only `NetworkBehaviour`, so no scene change) make it
server-authoritative for the yard's 42 trails. Nothing about how a prism changes hands elsewhere
moved.

- **Addressing.** A prism is `(slot, index)`: slot = burrs in `HijackYard.Burrs` order then rails
  in `HijackYard.Rails` order; index = position in that trail's append-only `TrailList`, laid in
  the same order on every peer. One packed `int` per change: `state << 24 | slot << 16 | index`,
  where the state byte is `domain` (bits 0-3) `| shield << 4` (bits 4-5: 0 none, 1 shielded,
  2 super-shielded) `| via-break << 6` (client reports only, below). An unshielded entry is
  bit-identical to the original `domain << 24 | …` layout, and bit 31 is never set.
- **Who is believed.** A flip is authoritative on the machine that simulates a pilot of the NEW
  domain (server: host pilot + every AI; client: its own pilot) — `StatsManager.OwnsAttacker`'s
  rule, applied to the prism. The server writes its own straight into the table; a client reports
  its own (`ReportOwnership_ServerRpc`) and the server accepts only entries in the SENDER's
  domain, taken from its own copy of the sender's `Player`. A rejected entry is answered with the
  table's value so the sender converges.
- **Shield state is in the word.** `PrismTeamManager.Steal` drops a shield instead of flipping a
  shielded prism and refuses a super-shielded one, so a shield that existed on one machine only
  used to make the same pass a steal there and a shield-break elsewhere. The ledger now carries
  each prism's armour with its owner, and applies both (owner first, then `ActivateShield` /
  `ActivateSuperShield` / `DeactivateShields`) wherever it applies a state. Who is believed
  follows who could have caused the change: a shield GAINED with no flip is the owner's (the
  Mass-5 "Reinforced Wake" armours only your own mass), and a shield DROPPED with no flip is a
  rival's (a steal that met it). The server accepts a client's shield only on the sender's own
  mass and a client's break only of a rival's.
- **The steal-against-shield race is settled on the TABLE.** A client can flip a prism before it
  hears the owner shielded it. The server applies `Steal`'s own rule to its table: a reported flip
  of a table-shielded prism lands as a break (owner kept, shield gone), a flip of a
  super-shielded prism is refused, and the sender is answered with what landed, so it converges.
  A client that did see the shield and broke it before taking the prism sets the via-break bit,
  so a break-then-steal that coalesced into one flush is not mistaken for the race.
- **Shield changes are swept, not evented.** A flip raises `OnTeamChanged`; a shield raises
  nothing, and `PrismStateManager` is shared platform code this mode does not change. The ledger
  checks `shieldSweepBudget` (4,096) discovered prisms a frame, round-robin, two bool reads each:
  a peak 9,930-prism yard is covered every ~3 frames, well inside the 0.1s flush the change then
  waits for. Profiled under `HijackOwnershipLedger.SweepShields`.
- **Everything else is provisional.** A remote pilot's proxy grinding a rail on this machine
  still flips prisms locally (the ride code is shared and untouched); those flips stay on screen
  for `ownershipGraceSeconds` (0.5s) so the owner's real change can arrive, then are put back to
  the table. A confirmed steal therefore never flickers; a phantom one lasts half a second.
- **Bandwidth.** No per-frame traffic. Changes are coalesced per prism and flushed every
  `ownershipFlushSeconds` (0.1s), 4 bytes each, chunked at 512 per RPC — a 100-prism cascade is
  one ~400-byte message. Server → clients: `SyncOwnership_ClientRpc`.
- **Late join.** Every client sends `RequestOwnershipSnapshot_ServerRpc` on spawn; the server
  answers (to that client only) with every prism that has EVER changed hands or armour. Everything else is
  still the colour the closed-form generator painted, which the joiner's own yard already shows.
  A key that names a prism the joiner has not laid yet waits in its table and applies the moment
  the prism appears.
- **Readers.** `HijackYard.HostileMassAt` / `HasHostileMass` / `NearestHostileBurr` /
  `OwnFractionOfRail` / `DomainOf` read the table (loot excludes a super-shielded prism, which no
  steal can take; a shielded one still counts, since the first pass breaks it), so the arrow and the AI's rail choice and
  "hostile underfoot" test agree on every peer. Ride speed (`TrailFollower`, vessel code) still
  reads the prism, and agrees because the ledger keeps every yard prism showing the table's
  value (immediately for a replicated change; within the grace window for a provisional one).

