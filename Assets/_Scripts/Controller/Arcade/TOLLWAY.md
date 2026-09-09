# Tollway — Technical Documentation

## Overview

**Tollway** is the Scarab-only **ring race**, and the mode built on the one idea the vessel's
own design record calls its best and no shipped mode had ever used
(`R_VesselActions/SCARAB.md §5`): **a switch pays its PLACER when ANY ball threads it, friend or
enemy.**

> Plants grow in the court, and a ring can only be grafted onto one. Fly at a plant, plant your
> ring at its foot, then drive a ball across the court and through the mouth — and every ball that
> threads it, yours or theirs or a stray off the wall, pays **you** and raises a monument on the
> spot. Rings are spent when they pay, so keep planting — and you get **one ring at a time**,
> so where it goes is the whole game. First team to 4 tolls.

2–4 players, 2–3 domains, AI backfill, through the same unified Netcode scene pipeline as every
other domain minigame.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity` (cloned from
  `MinigameScarabScramble` — same arena machinery, mode wiring swapped in place at the same
  fileIDs)
- **GameMode enum**: `GameModes.Tollway = 48`
- **Controller**: `TollwayController : MultiplayerDomainGamesController` (1 round / 1 turn,
  `HasEndGame=false`, `UseSceneReloadForReplay=true`, server winner detection in
  `OnTurnEndedCustom` → snapshot `SyncFinalScores_ClientRpc` — the DogFight/Scramble shape)
- **Scoring**: `TollwayScoringRuleSO : AstroLeagueScoringRuleSO` (`metric = Goals`, points not
  golf; race to `GameDataSO.GoalTargetCount` over domain sums). Toll target lives in
  **EndConditionOverridesSO** (FrogletTools ▸ Game Modes ▸ End Game Conditions), default **4**,
  resolved by `TollwayTollTurnMonitor`
- **Config**: every mode number lives in `TollwaySettingsSO`
  (`Assets/_SO_Assets/Games/TollwaySettings.asset`). The **switch's** numbers deliberately do
  not: ring radius, recharge cadence, standing-ring ceiling, the refund a threading pays and the
  **anchor reach** are `PlaceSwitchAction.asset`'s, because a Scarab plants rings in freestyle and
  in Scramble too. Nor do the **anchors** themselves: they are 14 NetworkSynced phyllotactic flora
  in the intensity's own `Tollway Spawn Profile N`, seeded by the ordinary Cell spawner
- **Vessels**: **Scarab only** (`ArcadeGameTollway.asset` `Vessels = [Scarab]`), enforced by the
  three platform layers (`SyncFromArcadeGame`, `ResolveSpawnVesselType`, the AI clamp). No
  mode-local vessel check
- **Intensity**: **traffic**, and it is `CellTypeChoiceOptions.IntensityWise` over **four** cell
  configs (list order = intensity, the Rampage / Peel the Cage shape). Court radius climbs
  (480→720) while the crystal count falls (`CrystalCountMode.IntensityScaled`: 4 players get
  7 / 6 / 4 / 2), so intensity 1 is a small court thick with balls and intensity 4 is a big court
  where every ring has to be aimed at a line somebody will actually fly — and each setting grows
  **its own anchor species**, one per growth family (Spire → Gyroid → Cacti → Quasicrystal), so
  it is visibly a different *kind* of place rather than the same court at four sizes

## Anchors — the rule the mode turns on, and the one it shipped without

The first cut let a ring land wherever the pilot's nose pointed. That made the whole game **one
move long**: forge a ball, plant a ring 150 units in front of it, nudge it through, repeat. There
was no shot to get better at, so there was nothing to come back to — and a minigame that is not
infinitely replayable is not finished.

**The fix is that a ring may only be grafted onto a LIVING PLANT'S HEART.** That rule belongs to
the **vessel**, not to this mode: `ScarabSwitchAnchors` snaps every Scarab's switch onto the
nearest free flora crystal within `PlaceSwitchActionSO.anchorReach` (70u) of the line the pilot is
aiming down, in every arena a Scarab flies in, with nothing wired. All Tollway adds is the
**refusal** — its `PlacementResolver` returns false when the vessel's snap found nothing — and the
plants: 14 NetworkSynced flora seeded into a band inside the court by the cell's ordinary spawn
profile.

### Why it is flora and not a mode-owned socket

The first shipped version of this rule built its own sockets: `TollwayTollPosts`, a seeded
Fibonacci band of emblems, replicated from an `int`, drawn by the controller, with its own
occupancy book, its own layout assertion in the generator and its own test suite. It worked, and
it was the wrong owner. **A flora crystal is the same affordance the ecology already produces
everywhere**, and it arrives with four properties a mode-owned socket had to be given by hand:

- it is **placed by the food web**, so the anchor field is alive rather than authored — a plant
  can be grazed away, and the seeder brings it back;
- it is **already a thing a pilot flies at**, visible from across the court, and it is already
  drawn;
- it is **already replicated** when a species opts in (`FloraConfigurationSO.NetworkSynced`), so
  the cross-peer agreement the placement rule needs is a field on an asset rather than a seed, a
  NetworkVariable and a re-derived walk; and
- it is a **joustable heart**, so an opponent can kill an anchor to deny it and take an element
  level for doing so. That counter-play did not exist and was not designed — it fell out of using
  the ecology's own object.

So the whole `TollwayTollPosts` system, its tests and its 60-line Python re-derivation are deleted
and the rule reads: **fly at a plant, plant your ring at its foot.** The general lesson is the one
`SCARABSCRAMBLE.md` records about walls and CLAUDE.md about cell-owned visuals, pointed the other
way: *before a mode builds a set of points of interest, check whether the platform already grows
one.*

### One anchor species per intensity — and each is a different KIND of plant

Intensity in this mode is **traffic**, and the anchor field is what a pilot reads the court by,
so each of the four settings grows a different species and the marker **gets bigger as the court
does** — a plant two hundred units further away has to read correspondingly larger. The four are
not four variations on one growth rule: the project ships **three flora growth families** and all
three are represented, so a pilot at intensity 3 is flying through a cactus grove and one at
intensity 4 through a lattice of needles.

| Intensity | Court | Species | Growth family | Prisms/plant | Leaf vol | One plant | Standing forest |
|---|---|---|---|---|---|---|---|
| 1 | 480u | **Spire** | `PhyllotacticFlora` | 40 | 14.26 | 570 vol | 560 prisms / 7,986 vol |
| 2 | 560u | **Gyroid** | `AssembledFlora` | 30 | 50.27 | 1,508 vol | 420 prisms / 21,113 vol |
| 3 | 640u | **Cacti** | `BranchingFlora` | 40 | 75.00 | 3,000 vol | 560 prisms / 42,000 vol |
| 4 | 720u | **Quasicrystal** | `AssembledFlora` | 110 | 46.39 | 5,103 vol | 1,540 prisms / 71,441 vol |

Gyroid and Quasicrystal share a growth *component* and share nothing a player can see — one is a
smooth minimal surface of 7×4.5×3.5 plates, the other an aperiodic cage whose struts run to 44
units. The generator asserts the roster spans as many families as four intensities can, and that
no family takes more than an even share; both bars are **derived from what the project ships**
(it counts the distinct `Flora` subclasses across the flora prefabs) rather than written as
literals, so adding a fourth family tightens the gate on its own.

**A lattice species keeps its own per-plant budget; the other two take the cell's.** A gyroid
octagon is 24 prisms around one crystal and a quasicrystal heart cell is one vertex's tree of
struts (`Docs/ECOSYSTEM.md` §32.7/§36), so a cell-imposed number does not thin those plants, it
truncates a shape mid-figure — *plant count is the only lever*. Spire and Cacti grow to whatever
budget they are handed, and 40 keeps them markers rather than scenery. That is also why **both
ladders are now per-intensity**: the previous single-family roster grew 560 prisms at every
setting so the count ladder could be shared, and a Quasicrystal field is 1,540 against a Gyroid's
420.

Every number in that table is **read out of the shipped assets** by
`author_tollway_assets.py`, never transcribed — with the fallback order stated explicitly, because
the three families do not share a shape of authoring: leaf size comes from the element asset's
variant tuning, else the prefab's own `leafSize` (a `BranchingFlora` species authors no per-element
geometry at all, so a Cacti pad is 5×5×3 for every element while a Gyroid is four different
plates). *A sentinel is not a measurement*: `LeafSize: {x: 0, y: 0, z: 0}` and
`MaxTotalSpawnedObjects: -1` are `FloraVariantTuning`'s documented "keep what you have", and
reading the zero as a real leaf priced the Cacti at 56.25 volume against its true 75 — one
element authoring the sentinel and three omitting the block, averaged. Both sentinels now fall
through to the prefab by rule.

#### The ring-mouth rule this roster replaces is RETIRED — the ball has no prism collision

The previous pass picked its roster by proving each species' body rises *out* of the 24u ring
planted at its heart, and rejected four phyllotactic forms (Rosette, Coral, Frond, Tendril) for
filling their own mouth. The rule measured something real and gated on something that does not
exist:

> The ball **NEVER** physically collides with prisms — it passes through **ALL** of them and
> resolves them by domain via a per-tick spatial scan (`ProcessPrismInteractions`).
> — `AstroLeagueBall`

A ball cannot be blocked by a plant, so **a plant cannot block its own ring**, and no flora
geometry can make an anchor unthreadable. What a plant's mass in the mouth actually does is get
*resolved by domain* as the ball passes: an opposing plant is destroyed and an own-domain one
takes a shield. That is the food web and the scoring meeting each other, and it is good — a ring
grafted onto an enemy plant clears that plant the first time anyone scores through it.

The roster is therefore chosen for how the four **look**, and the only geometry that constrains it
is the volume ladder below. The general rule is worth carrying past this mode: **before gating a
design on a clearance, find out what actually has to pass through the gap** — the thing that
threads a Tollway ring is the one object in the game with no prism collision at all. The
`--self-test` that proved the old partition is gone with the rule it proved.

### The line, not the point — the first playtest could not plant a single ring

Playtest, first outing: *"the AI did a good job of placing rings at the right points. However, I
could not place any rings at all."* Both halves of that were caused by one line of arithmetic.

The admission test asked whether an anchor lay within 70u of the **ring centre**, and the centre
is `ship + course × placementDistance` — **150u ahead of the nose** on the shipped asset. Flying
straight at an anchor from range `d`, that point sits `|d − 150|` from it, so a press was admitted
only while **80 ≤ d ≤ 220** and refused at *every range inside 80*. The HUD objective arrow points
**at** an anchor, so a pilot who followed it flew through the only window that worked and then
pressed from close range, forever, into a refusal that wrote one line to a verbose log channel
that is off by default. The AI never hit it because it presses on a **pacing timer while still
approaching** — which is exactly why it looked like a bot placing rings competently next to a
player who could not place one at all.

`ScarabSwitchAnchors.TryResolve` takes the **segment** `[ship, ring centre]` and admits the nearest
free heart within reach of it. That makes the rule the one a player would state — *plant a ring on
a plant you are flying at* — at any range up to the ability's reach, point-blank included.
`ScarabSwitchAnchorGeometryTests` pins it over `FloraHeartRegistry.DistanceToSegment`, with the
point-blank case named as the regression and a negative control asserting the old endpoint really
was 145u out (so widening the radius was never the fix: 145 > 70, and a radius that large would
admit half the court).

Two general rules come out of it:

- **When an ability's effect is offset ahead of the vessel, a proximity gate on the OFFSET POINT is
  an annulus, not a radius** — and the hole in the middle is point-blank, which is precisely the
  range a player guided by a HUD arrow will be at. Gate the path, not the projected point.
- **A refusal that only logs is indistinguishable from a dead button**, and it is what let a
  placement rule nobody could satisfy reach playtest. A refused press now posts
  `GameToastSituation.TollwayNoAnchor` ("No plant on this line — fly at one and plant your ring"),
  rate-limited to 4s and fenced to the machine whose own pilot was refused (`IsLocalUser` is false
  for an AI and for a remote replica). It is a side effect on the way *out* of the resolver, so it
  cannot change what the resolver returns and the cross-peer determinism the resolver contract
  demands is intact.

Three things about the shape of that rule are the point:

- **It removes exactly one degree of freedom.** The anchor fixes the **where**. It does *not* fix
  the **facing**: the ring's axis is still the course the placer flew in on, so aiming the mouth
  at the line you intend the ball to arrive from is still a real decision, and so is *which*
  plant (a read of where the traffic is, since any ball pays the ring's owner). The mechanic
  takes away the freedom that was breaking the mode and leaves the two that were making it.
- **It is a guarantee, not a tuning.** A ring's position is now not a function of the ball at
  all — it is one of N places the *ecology* chose — so "place it in front of the ball" is not a
  move that exists. What is left is the shot the mode is actually about: get to a plant, plant,
  then drive a ball across the court and through a mouth two dozen units wide.
- **The loop has no closed form.** The ball's position is emergent, the anchor set is re-seeded by
  the food web, anchors are consumed and re-contested, and a spent anchor is progressively harder
  to reuse because the dais it paid out is now standing around it.

The **aiming window falls out for free**: a ring lands `PlaceSwitchActionSO.placementDistance`
(150u) out along the course, so a pilot flies *at* a plant and presses when it is about that far
ahead. `placementDistance` stopped being a number nobody had to think about and became the mode's
timing.

**Why the anchors must be NetworkSynced.** A switch placement re-executes on every peer (the action
handler's ServerRpc→ClientRpc trip), so the anchor set has to agree everywhere or one machine
builds a ring where nobody else has one — permanently, since nothing about a placed switch is
replicated. Snapping to a **discrete** set is more forgiving than the continuous placement it
replaced (two peers whose vessel transforms differ by interpolation still pick the same plant
unless the ship is near-equidistant between two), but only if the plants agree — and flora are
**per-peer by default**: every machine runs its own spawner off its own `UnityEngine.Random`. The
anchor species therefore sets `FloraConfigurationSO.NetworkSynced`, which replicates the planting
DECISION (species, root pose, domain, element) and so the heart's world position with it. **Tollway
is the first shipped user of `FloraNetworkSync`**; the scene's cell already carried the component
from the Scramble clone. Occupancy reads `ScarabSwitch.Live`, itself built per-peer from the same
replicated presses, so the claim book cannot desync further than the switch list already does.

Freestyle and Scramble deliberately do **not** rely on it: there the snap is an assist, a miss
places free, and neither cell authors flora at all — so their behaviour is byte-for-byte what
shipped.

**The anchor band is authored against the SMALLEST court.** Planting fractions are of the
**membrane** (1200u) while the court is the nucleus resized per intensity (480 → 720), so the band
is `0.16 … 0.34` of the membrane — exactly the `0.40 … 0.85` of the intensity-1 court the retired
posts used. `author_tollway_assets.py` asserts the three properties that are arithmetic rather
than taste: the band's outer edge is inside the smallest court (408 < 480, so no plant is ever
beyond a wall a ball cannot cross), its inner edge clears the middle by more than one aim window
plus one ring mouth (192 > 94, so an anchor is a place you *fly to* rather than one claimed
incidentally by anyone crossing the court), and the band has real depth (216u — collapsed to a
shell, "which anchor" is a purely angular choice). The consequence at higher intensities is
deliberate: the court grows and the anchor field does not, so a bigger court is more open water
around the same contested middle.

**The platform change this needed** is that `Flora.ResolvePlantRadius` clamped its inner edge
outside the nucleus *unconditionally*. Here the nucleus **is** the court, so the whole arena was
un-plantable. Both stated reasons for that clamp — nucleus mass is the territorial claim, and it is
excluded from the fauna targeting grids — **are** the control zone, and this cell has already
declared it has none (`Cell.NucleusIsControlZone = false`). The clamp now reads that flag. It is
`Docs/ECOSYSTEM.md §25.1`'s trap from the other side: Astro League's food web silently did nothing
because a mode borrowed the nucleus as play geometry and inherited its semantics; here a mode that
borrowed it as its court could not seed a plant inside its own arena. The one reason that does
survive — a standard crystal respawns in the nucleus volume — is accepted: it is clutter in a
volume a court-mode has already filled with play, not mass the ecology cannot reach.

**What the anchors cost.** 14 plants at every intensity, but a different KIND of plant at each,
so the mass differs where the collider count does not: **560 / 420 / 560 / 1,540 prisms** and
**7,986 / 21,113 / 42,000 / 71,441 volume** (Spire / Gyroid / Cacti / Quasicrystal), standing from
the first seconds and folded into both bands of the cell's ladder rather than left for it to
discover — which is why BOTH ladders are per-intensity here and not just the volume one. Collider
budget: **14 always-on** heart colliders (one per plant) at every setting, which is the number that
stays flat while everything else about the field changes; the body prisms are LOD-cullable boxes. Each
species' four elements roll uniformly, so a quarter of the anchors are **Charge** and therefore
shielded (`Flora.ResolveShieldPeriod`) — shielded mass is never food and leaves the fauna
targeting grids, so those anchors are the durable ones. Emergent, not authored.

**What the mode cost.** The toll target has come down **twice** — 12 → 8 when rings became
anchored, then **8 → 4** when the switch went to one ring at a time on a 60-second recharge — and
the comeback rate moved with it every time (**0.5 → 0.75 → 1.5**), because
`bonusLevels = deficit × rate` makes the rate a **function of the target** (the trap `DOGFIGHT.md`,
`BENDS.md` and `WILDLIFE_LIBERATION.md` all record — this is its sixth outing). Halving the target
halves a quarter-of-target deficit, so the rate has to double to keep buying the same 1.5 element
levels the 12-toll race gave. The cell's volume ladder was restated in the same currency:
**Restless at 3 monuments, Frenzy at 7**, out of the **10** a maximum-length 4-toll match can raise
(the winner's 4 plus 3 for each losing domain).

**And one of those ladder asserts was itself the same trap.** The gate that keeps Frenzy out of
the early race was written as `FrenzyEnterVolume > trailBand + 8 × daisVolume` — where **8 was the
toll target** at the time, transcribed as a literal. It stopped meaning anything the moment the
target halved, while still passing. Both gates are now stated as fractions of a maximum-length
match (`MAX_MATCH_DAISES = target + 2 × (target − 1)`), and the assert asks whether Frenzy arrives
before the race is half run. *A threshold that is a function of the target must be written as one;
a threshold nobody has watched fail is worse than no threshold, because it reads as a gate.*

One consequence worth knowing rather than designing around: a dais's planar band reaches
**155.3 × ringRadius/20** (`SCARAB.md §5.1`), which at the switch's own 24u mouth is ~186u. So a
paid toll crowds its *neighbouring* anchors with monument, and a MASS-heavy Scarab's dais blankets
several. That does not block placement (the resolver's occupancy test is live switches, not
prisms) but it does block the ball's line, so the court genuinely gets harder to build in as the
match runs. Emergent, not authored, and untested — see the follow-ups.

## The design, and why each rule is the inverse of a sibling's

Astro League and Scarab Scramble are both "get the ball through the ring". Tollway keeps the
ball and inverts everything around it.

1. **The scoring surfaces are PLACED BY PLAYERS INTO THE COURT'S OWN SOCKETS, and CONSUMED ON
   USE.** There is no net to defend
   and no arena-owned hoop, only empty posts. A ring is one point; it is spent the moment it pays
   and has to be replanted. That makes *which post the next ring goes into* the whole strategy
   layer — and it is why the
   switch's charge had to start recharging at all (`SCARAB.md §5.2`, the sibling change: before
   it a pilot could place exactly one ring per life, so this mode was not buildable).
2. **You score off other people's shots.** Because any ball pays the ring's owner, the defensive
   play and the economic play are the same play: rings belong where the *enemy's* balls are
   going. A pilot who only attacks starves. Herding your own ball through an enemy's ring scores
   for them **and** refunds them a charge — that is the central tension, not a trap, because
   rings are large, domain-coloured and you chose your line.
3. **The arena is built by the scoring.** Every paid toll raises a 255-prism scarab-wing dais on
   the spot (`SCARAB.md §5.1`), so the terrain grows out of the match and the scoreboard is
   readable off the court. Those monuments are ordinary conserved mass: they block lanes, their
   danger blades punish a pilot who flies the rosette, their shielded blades turn a ball, and the
   food web grazes them once the volume ladder wakes up.
4. **A ball is NOT spent by a toll.** Scramble detonates a scored ball because its hoops are
   permanent and its balls are the scarce thing; here it is exactly the other way round. So one
   shot threading two rings is the mode's signature screamer — the `CHAIN x{n}` toast — and
   traffic keeps paying until something else claims it.
5. **The court is a sphere and the walls reflect, and the mode installs none of it.** The court
   IS the nucleus (`SetNucleusWorldRadius`), and a ball bounces off its cell's nucleus as a
   property of the BALL (`AstroLeagueBall.ResolveNucleusBoundary`), so resizing the nucleus is
   the entire act of building the arena. Every carom sends a ball back through the middle, and in
   this mode a ball crossing the middle is a ball that might pay somebody.

## The one platform change this mode needed

`ScarabSwitch` gained a **`static event Action<ScarabSwitch, AstroLeagueBall> OnThreaded`** plus
`PlacerName` / `PlacerDomain` / `RingRadius` accessors and a **`Live`** roster. At the merge base
a threading raised the dais and told nobody — nothing outside the class could observe the event
the whole ability is built around, and no mode could score it. Three properties of the event
matter to anyone else who subscribes:

- **It is raised on EVERY peer**, because detection is per-peer (each machine runs its own
  plane-crossing test against its own copy of the replicated ball), which is the same reason the
  dais is laid on every peer rather than replicated. Anything that SCORES must gate on
  `IsServer`; anything presentational should not.
- **The payer is read off the SWITCH, never off the ball.** "Any ball pays the ring's owner" is
  the whole rule, so there is deliberately no arming gate, no ownership test and no own goal.
- **It is raised inside a try/catch.** A throwing listener must not cost the switch its dais —
  that is conserved mass the player earned, and a mode's scoring bug should not silently eat it.

`ScarabSwitch.Live` is the `AstroLeagueBall.Live` shape and exists so the AI and the HUD arrow
can both ask "the nearest ring of my domain" without `FindObjectsByType`. A switch joins on
`Build` (so its domain is already known and no reader can ever see a Blue one) and leaves the
instant it is spent or retired, ahead of its own destruction.

The anchor rule added a **second** one: `PlaceSwitchActionExecutor.PlacementResolver`, a static
mode veto on *where* a ring may go. It is the sibling of `ScarabBallForge.ForgeGate` and makes the
same argument — a rule about how a mode uses an ability belongs to the mode, and putting it on the
vessel would make freestyle and Scramble inherit it. The delegate now also carries **whether the
vessel's own snap found an anchor**, so the *finding* is the vessel's and only the *refusal* is
the mode's; Tollway's whole resolver is `if (anchored) return true;`. Two constraints on anyone
else who installs one:

- **It must be a pure function of replicated state.** It is consulted on every peer, so a resolver
  that answered differently on two machines would build a switch on one and not the other,
  permanently. Tollway's reads only `anchored`, itself a function of the replicated flora slot
  list and the live switch roster.
- **It is consulted before the charge is spent**, so a refusal costs the pilot nothing but the
  press — the same shape as the existing no-charge refusal. It is identity-guarded on removal
  (`if (PlacementResolver == ResolveSwitchPlacement)`), because a leaked resolver would silently
  refuse every switch in the next scene.

## Class inventory

| Class | Role |
|---|---|
| `TollwayController` | Match director: court build (the nucleus resize; no per-ball boundary), the `OnThreaded` subscription and server scoring, chain tracking, toast beats (toll / chain / match point / lead change), AI steering + **AI ring planting**, fauna exclusion sweep, final-score snapshot |
| `TollwaySettingsSO` | Court radius per intensity, the AI's anchor aiming window, chain window, fauna exclusion, AI dials. Deliberately owns nothing about the switch, the ball, or where a ring may go |
| `TollwayScoringRuleSO` | Thin subclass of the Astro League rule so the mode owns its asset |
| `TollwayTollTurnMonitor` | Resolves the toll target from `EndConditionOverridesSO`, NV-syncs it, publishes `GameDataSO.GoalTargetCount`, ends the turn via `rule.IsObjectiveReached`, shows the local DOMAIN's deficit |
| `TollwayObjectiveProvider` | HUD arrow, three steps: your nearest own-domain ring (measured **from the ball**, so it names the ring you would actually herd it into) → the ball → the nearest omni crystal |

## AI — and why it is not optional here

**An AI that cannot plant a ring cannot score in this mode**, so an all-AI domain would be an
opponent that could not play. That makes AI ring planting a correctness requirement rather than
polish, and it is the one place this mode reaches past the Scramble template.

`TollwayController.TickAISwitchPlacement` plants a ring for each AI through
**`R_VesselActionHandler.PerformShipControllerActionsReplicated`** — the same owner→server→
every-peer trip a human's press makes — and NOT through `AIPilot.abilities`, which calls
`StartAction` locally. An AI pilot runs server-only, so a local press would build the ring and
lay its dais **on the server alone**: invisible to every client, and conserved mass that exists
on one machine. The platform already records the rule ("replicate an AI's press when the
ability's output does not already ride some other replicated channel"), and a placed structure
rides nothing. The control is asked for by ability TYPE (`TryGetInputForAction<PlaceSwitchActionSO>`),
so a future rebind keeps working.

Once rings could only be grafted onto plants, the timer stopped being a metronome and became a
**cooldown**: an AI presses only when a ring pressed *right now* would land on a free heart, tested
with the executor's own arithmetic (the ring lands `placementDistance` out along the course) asked
of the same asset, so the two cannot drift. The tolerance is the mode's own
`aiAnchorAimTolerance`, deliberately NOT the vessel's `anchorReach`: it is how fussy the bot is
about its approach, and being wrong either way is free — too loose costs one refused press, which
spends nothing, and too tight only makes it fly a little further before planting. Failing the test
costs nothing either: the cooldown is left alone and the AI tries again next frame, which is what
makes the steering below pay off.

Steering has three states, and the first is new with the anchors: **no ring of yours standing → fly
to the nearest free plant**, because a ring is the only thing that scores and it can only go on a
heart, so escorting a ball with nowhere to put it is the mode's one dead end. Then the Scramble
shape: no team ball → fetch the nearest omni crystal (forging happens by flying through it); team
ball live → escort it, aiming behind the predicted ball on the far side from **your domain's**
nearest ring. An AI deliberately never aims at an enemy ring; it will still thread one
occasionally, which is the mode working.

The HUD arrow follows the same order (`TollwayObjectiveProvider`), so a new player is taught the
loop in the order they have to perform it: *go to a plant → take your ball to your ring → go make
a ball*. It points at the plant's **crystal transform**, so an anchor that is grazed away takes the
arrow with it instead of leaving it aimed at a remembered spot.

## Cell ecosystem

The standard Cell owns the environment. `Tollway Cell Config 1..4` are **forked from
`Scarab Scramble Cell Config` for exactly two reasons — the ANCHORS and the volume ladder** — and
reuse that arena's fauna species, membrane, nucleus and cytoplasm verbatim (the cell is per-arena,
not per-mode). The cell is `CellTypeChoiceOptions.IntensityWise` and **list order is the
intensity** (`Cell.IntensityIndex`), so the generator asserts the four configs appear as an
ordered slice rather than testing membership four times — a correct set in the wrong order is a
wrong arena at three of four settings and every membership test would still pass. The nucleus IS
the court: `SetNucleusWorldRadius(courtRadius)` + **`NucleusIsControlZone = false`** (play
geometry, not a claim — skip it and the whole pitch is inedible, `Docs/ECOSYSTEM.md §25.1`).

**Why the spawn profiles had to be forked.** Scramble authors `SupportedFloras: []` — no flora at
all — and this mode's scoring sockets are plants. `Tollway Spawn Profile 1..4` are Scramble's with
one flora entry each and the whole three-species cleanup crew carried across verbatim (the
generator asserts every one of them survived every fork; a fork that quietly loses a species is
how an arena ends up with a ladder describing fauna it does not have).

**Why the ladder had to be re-authored.** In Scramble a switch dais is a rare event, so its gates
are "the trail band plus 3 and 7 spent switches" (Restless 164,000 / Frenzy 391,000). Here a
**toll IS a dais**, so a match raises three to five times the mass and both of Scramble's gates
would be crossed before the race was half run — after which the ladder conveys nothing. Restated
in the currency this mode actually runs on, at **50,773 volume and 255 prisms per monument**, and
including the standing anchor forest, because that mass is present from the first seconds at
every phase. **Both** ladders are per-intensity: the four species differ in how many prisms they
grow *and* how big each one is, so a Quasicrystal field is 1,540 prisms against a Gyroid's 420 and
one shared count backstop would be four times too tight at one end and slack at the other.

| gate | arithmetic | I1 Spire | I2 Gyroid | I3 Cacti | I4 Quasicrystal |
|---|---|---|---|---|---|
| standing anchor forest | 14 plants × budget × leaf volume | 7,986 | 21,113 | 42,000 | 71,441 |
| `RestlessEnterVolume` | 12,000 trail band + anchors + **3** monuments | **172,000** | **185,000** | **206,000** | **236,000** |
| `RestlessExitVolume` | enter − 4,000 | 168,000 | 181,000 | 202,000 | 232,000 |
| `FrenzyEnterVolume` | 36,000 trail band + anchors + **7** monuments | **399,000** | **413,000** | **433,000** | **463,000** |
| `FrenzyExitVolume` | enter − 6,000 | 393,000 | 407,000 | 427,000 | 457,000 |

| count backstop | arithmetic | I1 Spire | I2 Gyroid | I3 Cacti | I4 Quasicrystal |
|---|---|---|---|---|---|
| standing anchor forest | 14 plants × budget | 560 | 420 | 560 | 1,540 |
| `RestlessEnter` | 900 + anchors + 3 × 255 × ~1.6 headroom | **2,680** | **2,540** | **2,680** | **3,660** |
| `RestlessExit` | enter − 100 | 2,580 | 2,440 | 2,580 | 3,560 |
| `FrenzyEnter` | 3,000 + anchors + 7 × 255 × ~1.6 | **6,420** | **6,280** | **6,420** | **7,400** |
| `FrenzyExit` | enter − 210 | 6,210 | 6,070 | 6,210 | 7,190 |

The generator also asserts the forest is under **half** of its own `RestlessEnterVolume` — folded
into the threshold, a forest big enough to be most of it would make the ladder describe the
scenery rather than the match, which is the Lattice cell's "no single colony's own ceiling may
reach `FrenzyEnterVolume`" (`Docs/ECOSYSTEM.md` §36) applied one arena down. The shipped worst
case is the Quasicrystal at 30%.

The trail band and the headroom factor are Scramble's, unchanged; only the monument count and the
anchor forest differ. Both monument budgets are **fractions of a maximum-length match** rather
than constants — `MAX_MATCH_DAISES = target + 2 × (target − 1)` = **10** at the shipped 4-toll
target — and `author_tollway_assets.py` asserts both that Frenzy does not arrive before the race
is half run and that the budget is ordered `0 < Restless < Frenzy ≤ max`, so the ladder can
neither saturate early nor be unreachable.

**The anchors are ordinary food-web citizens**, which is the point of using flora rather than a
mode-owned socket: the cleanup crew grazes them once the cell leaves Calm, so an anchor can be
eaten and the seeder brings it back within `NewPlantPeriod` (20s). A quarter of them roll **Charge**
and are therefore shielded (`Flora.ResolveShieldPeriod`), which takes them out of the fauna
targeting grids entirely — so the field thins unevenly rather than uniformly. Emergent, untested,
and the lever if it goes wrong is the species roll or the reseed period, never a cull.

The read this buys is the good one the volume spine exists for: **the cleanup crew arrives in
proportion to how much has been scored.** Fauna wait outside the court while the cell is Calm
(`Cell.FaunaExclusionRadius`, swept — the Astro League pattern) and pour over the wall to graze
the monuments once it leaves.

Crystals spawn inside the court by the platform's own rule (the omni respawn volume IS the
nucleus), count per intensity, neutral domain.

**Collider budget.** The arena's growth is bounded **by the win condition, not by a culler**: at
most 10 monuments (4 + 3 + 3) × 255 prisms = **2,550 prisms**, plus the standing **560** anchor
prisms — comparable to PeelTheCage's intensity-1 cage (10,620) and well inside Atlantis (~69k). A
typical match lands nearer 10–14 monuments. The anchors add **14 always-on** colliders (one heart
each); their body prisms are LOD-cullable boxes. Rings themselves cost nothing — `ToyFactory.AddSwitchRing` is a generated mesh with
**no collider**, which is also why a vessel flies straight through one and only a ball can
trigger it. Balls carry one SphereCollider each, capped by `AstroLeagueBall.cellBallLimit`; fauna
are bounded by `MaxLivePopulation`.

## Shared-code touchpoints (why non-Tollway files are in this branch)

| Change | File |
|---|---|
| `Tollway = 48` | `_Scripts/Data/Enums/GameModes.cs` (+ `EnumIntegrityTests` count 46 → 47; 45/46/47 went to Switchback, Hijack and Drumfire upstream) |
| `OnThreaded` + `Live` roster + `PlacerName`/`PlacerDomain`/`RingRadius` | `Vessel/R_VesselActions/ScarabSwitch.cs` |
| Switch charge RECHARGE (60 s), ONE-ring ceiling, single charge, threading refund | `PlaceSwitchActionSO` / `PlaceSwitchActionExecutor` / `PlaceSwitchAction.asset` / `Scarab.prefab` (see `SCARAB.md §5.2`) |
| `tollwayTollTarget` live/build/getter/window rows, default 4 | `EndConditionOverridesSO` + `EndConditionOverridesWindow` + `Resources/EndConditionOverrides.asset` |
| `case GameModes.Tollway → Goals` | `ElementalComebackSystem.DefaultSourceFor` + `ElementalComebackSystemTests.LiveSourceCases` |
| Objective-provider case | `_Scripts/UI/MiniGameHUD.cs` |
| `GameToastSituation` 70–75 (toll, chain, match point, lead change, ring hint, no-anchor refusal) | `_Scripts/Data/Enums/GameToastSituation.cs` |
| Charge-count re-tint only on a CHANGE, plus the Mass row's **cooldown veil** | `_Scripts/UI/Controller/ScarabHUDController.cs` |
| Flora anchoring: the live-heart registry, the vessel-side snap, `anchorReach` | `_Scripts/Controller/Environment/FloraAndFauna/FloraHeartRegistry.cs`, `Vessel/R_VesselActions/ScarabSwitchAnchors.cs`, `PlaceSwitchActionSO` / `Executor` |
| `LifeForm.HeartTransform`; `Flora` registers/unregisters its heart | `_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs`, `Flora.cs` |
| The nucleus planting clamp now honours `NucleusIsControlZone` | `_Scripts/Controller/Environment/FloraAndFauna/Flora.cs` (`ResolvePlantRadius`, `ClampToPlantingBand`) |

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameTollway.unity`: every script reference resolves (no *Missing (Mono Script)*);
   the `Game` GO carries `TollwayController` + `TollwayTollTurnMonitor`, and the controller's
   `settings` / `rule` / `arenaCell` / `cellData` are all wired.
2. **Enter play** (solo + AI backfill, intensity 1): the court sphere ≈480 blooms as the nucleus;
   crystals appear inside it; no console errors.
3. **Find the anchors**: scattered through the court are **14 plants of this intensity's own
   species** — I1 Spire (a phyllotactic pillar), I2 Gyroid (a minimal-surface plate colony), I3
   Cacti (a squat branching cactus), I4 Quasicrystal (an aperiodic needle cage) — in a band
   roughly 192–408 u from the middle, inside the court wall and well clear of the core. Confirm
   they are INSIDE the court (the `NucleusIsControlZone` planting fix) rather than ringing it from
   outside, that each carries a visible elemental crystal, and that **switching intensity switches
   the species to a visibly different kind of plant** — this is the one step that catches a wrong
   `FloraPrefab` component fileID, which resolves to no component at all and grows nothing.
   The two lattice species grow to their OWN budget (30 and 110 prisms) rather than the cell's 40,
   so I2 and I4 should read as denser structures, not as bigger versions of I1.
4. **Placement is refused away from a plant**: press the switch control (A / Button1) in open
   space. **Nothing happens and no charge is spent**, and a toast reads *"No plant on this line —
   fly at one and plant your ring"* (rate-limited to one per 4 s).
5. **Plant a ring ON a plant**: fly at one and press — **at close range, at 150 u, and at ~200 u**,
   which is the point-blank regression. A domain-coloured ring blooms **centred exactly on the
   plant's crystal**, facing **along your course** — approach the same plant from two directions to
   confirm the position is the plant's and the facing is yours. That press spends your **whole**
   meter.
6. **A claimed plant refuses a second ring**: press again at the same plant → refused. Fly to the
   next one.
7. **Recharge and the COOLDOWN VEIL**: after that press, watch the Mass card — a clockwise radial
   veil covers the icon and depletes over **60 s**, clearing with the ready flash the instant the
   charge lands. Confirm a press during the veil does nothing, and that the veil is the only thing
   on screen saying so.
8. **ONE RING AT A TIME**: with a ring standing, wait out the recharge and plant a second → your
   FIRST ring shrinks away over ~0.5 s and pays no dais, and its plant frees up for anyone. This
   is the whole point of the pass: you never have two scoring surfaces standing.
9. **Score a toll**: forge a ball (fly your SKIMMER through a bright/omni crystal) and drive it
   across the court and through your ring → the ring vanishes, the scarab-wing dais rises around
   the spot over a few frames, the toast reads `{name} collects a toll - n/4`, the HUD domain sums
   move, and **your meter refills instantly** (`chargeRefundOnThread`, now a whole meter — so a
   ring somebody uses is free and only a wasted one costs you the minute). This is the shot the
   whole redesign exists to create — confirm it takes real ball control rather than a nudge.
10. **The central rule**: knock a ball through an **enemy's** ring → it scores for THEM and
    refunds THEM. Confirm nothing scores for you.
11. **Chain**: get one ball through two of your rings inside 4 s → `CHAIN x2!` toast.
12. **A ball survives a toll**: confirm the ball flies on after paying rather than detonating.
13. **The HUD arrow teaches the loop**: with no ring standing it points at the nearest free PLANT;
    plant one and it points at the ring (measured from your ball); with a ring and no ball it
    points at an omni crystal.
14. **AI**: watch an AI domain — it should fly to a free plant, plant, then escort its balls toward
    its own rings, at most one ring per ~66 s (paced just above the vessel's own 60 s recharge, and
    the generator fails the build if it drops under it). **An AI domain must be able to reach the
    target on its own** — this is the number most at risk from the recharge change, because an AI
    that presses into a filling meter plants a fraction of the rings it should.
15. **Match end**: first domain to 4 → winner banner, scoreboard, Play Again reloads.
16. **MPPM two-client — the one that matters most in this pass**: the **14 plants are in the SAME
    places on both machines** (`FloraNetworkSync`, this mode being its first shipped user); a
    client's ring appears on the host **on the same plant** and at the same size; a toll scored on
    either machine moves both scoreboards; an AI's ring is visible on the client (the
    replicated-press path). If the plants disagree, nothing else in this list is meaningful — check
    the cell GO carries `FloraNetworkSync` and the species asset has `NetworkSynced: 1`.
17. **Ecology**: play on until the monuments silt the court (~169,000–177,000 volume depending on
    intensity, roughly 3 tolls) →
    the cleanup crew pours over the court wall and grazes the daises **and the anchor plants**.
    Watch whether the anchor count recovers to 14 between waves; a court that ends with two plants
    in it is the failure mode the follow-ups name.
18. **Freestyle regression**: fly a Scarab in Menu_Main and plant a ring in open space. It must
    still land 150 u ahead of the nose exactly as before — that cell authors no flora, so the snap
    finds nothing and places free. Same in **Scarab Scramble**.

## Known limitations / follow-ups

- **A refused press with NO CHARGE BANKED is still silent.** That refusal happens inside
  `PlaceSwitchActionExecutor` before the mode's resolver is consulted, so the mode has nothing to
  hook. It is now much less likely to read as a dead button, because the Mass card carries a
  **cooldown veil** over exactly that state (`ScarabHUDController` → `SetAbilityCooldown`) — but a
  veil is not a refusal message, and if it still reads badly in play the honest fix is the follow-up
  SCARAB.md §5.2 already names: a can-this-action-run veto on `ShipActionSO` consulted in
  `R_VesselActionHandler.OnButtonPressed`, which would give both refusals one home.
- **Every number here is authored, not play-tested.** The toll target (4), the anchor count
  (14), the anchor band (0.40–0.85 of the intensity-1 court) and reach (70 u), the per-plant prism
  budget (40), the reseed period (20 s), the AI ring cooldown (66 s), the chain window (4 s), the
  crystal ladder and the volume ladder are all first-pass. The anchor numbers are the ones most
  worth a play-test: too few plants and every match is the same three shots, too many and one is
  always conveniently to hand — which is the degenerate placement the mechanism exists to remove,
  back by the front door.
  The ladder in particular is an ESTIMATE pending FrogletTools ▸ Ecology ▸ Measure Cell
  Environment Baselines, exactly as Scramble's is.
- **The one-ring-at-a-time pass is the biggest untested change, and the number to watch is the
  RECHARGE, not the target.** A pilot now holds one ring on a 60 s cooldown, refunded in full the
  moment somebody threads it. The intended loop is *place well, get it used, place again* — a good
  placement costs nothing and a wasted one costs a minute. The risk is the failure case: a pilot
  whose ring nobody threads spends a minute of a four-toll race with **no scoring surface at all**,
  which could read as being locked out rather than as having made a bad call. If it does, the lever
  is `rechargeSecondsPerCharge` (a vessel number, so it moves freestyle and Scramble with it) and
  the target is the *second* lever, since the two are coupled through the comeback rate.
- **A switch can be missing on a third peer.** Pre-existing and not introduced here: placement
  runs on every peer against that peer's own charge meter, and an elemental crystal's grant is
  replayed only onto the vessel's OWNER, so in a match with three or more machines a third peer
  can be a charge short and refuse a ring the placer built. The recharge makes the meters agree
  *more* than they did. The real fix is a can-this-action-run veto on `ShipActionSO` consulted
  before the RPC goes out; see `SCARAB.md §5.2`.
- **A switch's crossing detection is PER-PEER, and this mode makes that scoring-relevant.**
  `ScarabSwitch.Update` runs its own plane-crossing test on every machine (that is what lays the
  dais on every machine), and only the SERVER's detection scores. The ball is server-simulated
  and clients interpolate along essentially the same path, and the test is a continuous segment
  test rather than a per-frame point test, so the two agree except at the very rim of the mouth.
  When they disagree the visible symptom is a ring that spends and raises its monument on one
  peer without a toll appearing on the scoreboard. This is a pre-existing property of the switch
  rather than something this mode introduced, but the mode is what makes it matter; the real fix
  is to make the SPEND server-authoritative and replicate it, which would make `ScarabSwitch` a
  `NetworkBehaviour` and is a bigger change than this branch should carry.
- **An AI picks the NEAREST free anchor, not the best one.** It flies to whichever plant is
  closest and plants there. Since it is otherwise flying at crystals and balls its rings land near
  traffic, which is good enough for v1 — but choosing an anchor near a predicted ball line, or near
  an *enemy* ball's line (which is where the points actually are in this mode), is the obvious
  improvement.
- **An anchor is never re-earned; it just frees up.** A spent anchor is immediately re-claimable,
  and the only thing making that harder is the dais now standing around it. That is a pleasing
  emergent consequence rather than a designed one, and it has not been play-tested — if the same
  two anchors get farmed all match, the lever is a cooldown on the plant rather than on the pilot.
- **The anchor field can be eaten faster than it re-seeds, and that is untested.** Fauna pour over
  the wall once the cell leaves Calm and the anchors are ordinary grazeable mass; the seeder tops
  the population back to 14 every 20s, so the steady state is a tug-of-war nobody has watched yet.
  The failure mode to look for is a late-match court with two anchors in it. Levers, in order:
  raise `NewPlantPeriod`'s cadence, raise `MaxLivePopulation`, or open `faunaExclusionCourtFraction`
  so the crew stays out longer. **Never** shield the anchors to protect them — a shield reaches
  1.5 × `leafSize` (`Docs/ECOSYSTEM.md §35`) and would also take them out of the targeting grids,
  which is a different mode.
- **A plant's canopy is inside its own ring, and that is now a LOOK question rather than a
  playability one.** The ring is centred on the heart, so the host plant stands in its own mouth —
  but a ball has no prism collision at all (`AstroLeagueBall.ProcessPrismInteractions`), so it
  passes through and resolves that mass by domain instead: an opposing plant is destroyed by the
  ball that scores through its ring, an own-domain one takes a shield. What is untested is how a
  thread READS when the mouth is full of a lattice cage — the plant is not in the way, but it may
  look like it is. Watch **I4 Quasicrystal** first (110 struts around one heart, the densest in the
  roster) against **I1 Spire** (40 prisms, the airiest). If it reads as cluttered the lever is the
  species, or the per-plant budget on the two that take the cell's — never the ring radius, which
  is a vessel-wide number.
- **No `ForgeGate`, no ball cap of this mode's own.** The per-CELL ball limit
  (`AstroLeagueBall.cellBallLimit`) applies as a platform rule and this mode installs nothing;
  the cell overload will detonate loose balls here as it does in Scramble, and there is no
  Tollway toast for it.
- **No `ModeControlsLibrary` entry** (the card shows the vessel's four abilities, which is right)
  and **not in the Maelstrom pool** — the mode is domain-scored and 2–4 players so it qualifies;
  adding it is one asset edit once it has been play-tested.
- **Card art is unauthored** (`IconActive`/`IconInactive`/`CardBackground` = 0, the Scarab
  Scramble card's own current state).
- **Max players is 4.** Scramble seats 6, and more pilots means more rings and more traffic,
  which probably suits this mode — worth trying after the first play-test.
- **The mode preview is honest but empty**: rings are placed by pilots at runtime, so a preview
  arena has nothing to thread until somebody plants one.
