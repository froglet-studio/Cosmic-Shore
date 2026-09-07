# Tollway — Technical Documentation

## Overview

**Tollway** is the Scarab-only **ring race**, and the mode built on the one idea the vessel's
own design record calls its best and no shipped mode had ever used
(`R_VesselActions/SCARAB.md §5`): **a switch pays its PLACER when ANY ball threads it, friend or
enemy.**

> The court is studded with **toll posts**. Fly to one, plant your ring in it, then drive a ball
> across the court and through the mouth — and every ball that threads it, yours or theirs or a
> stray off the wall, pays **you** and raises a monument on the spot. Rings are spent when they
> pay, so keep planting. First team to 8 tolls.

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
  **EndConditionOverridesSO** (FrogletTools ▸ Game Modes ▸ End Game Conditions), default **8**,
  resolved by `TollwayTollTurnMonitor`
- **Config**: every mode number lives in `TollwaySettingsSO`
  (`Assets/_SO_Assets/Games/TollwaySettings.asset`). The **switch's** numbers deliberately do
  not: ring radius, recharge cadence, standing-ring ceiling and the refund a threading pays are
  `PlaceSwitchAction.asset`'s, because a Scarab plants rings in freestyle and in Scramble too
- **Vessels**: **Scarab only** (`ArcadeGameTollway.asset` `Vessels = [Scarab]`), enforced by the
  three platform layers (`SyncFromArcadeGame`, `ResolveSpawnVesselType`, the AI clamp). No
  mode-local vessel check
- **Intensity**: **traffic**. Court radius climbs (480→720) while the crystal count falls
  (`CrystalCountMode.IntensityScaled`: 4 players get 7 / 6 / 4 / 2), so intensity 1 is a small
  court thick with balls and intensity 4 is a big court where every ring has to be aimed at a
  line somebody will actually fly

## Toll posts — the rule the mode turns on, and the one it shipped without

The first cut let a ring land wherever the pilot's nose pointed. That made the whole game **one
move long**: forge a ball, plant a ring 150 units in front of it, nudge it through, repeat. There
was no shot to get better at, so there was nothing to come back to — and a minigame that is not
infinitely replayable is not finished.

**The fix is that a ring may only be planted in a TOLL POST.** The court is studded with sockets
(10 / 12 / 14 / 16 by intensity) spread over a band between 0.40 and 0.85 of the court radius, and
`PlaceSwitchActionExecutor` refuses any placement whose ring centre does not fall within
`postClaimRadius` (70u) of a **free** one — and snaps it exactly onto that post when it does.

Three things about the shape of that rule are the point:

- **It removes exactly one degree of freedom.** The post fixes the **where**. It does *not* fix
  the **facing**: the ring's axis is still the course the placer flew in on, so aiming the mouth
  at the line you intend the ball to arrive from is still a real decision, and so is *which*
  socket (a read of where the traffic is, since any ball pays the ring's owner). The mechanic
  takes away the freedom that was breaking the mode and leaves the two that were making it.
- **It is a guarantee, not a tuning.** A ring's position is now not a function of the ball at
  all — it is one of N places the *court* chose — so "place it in front of the ball" is not a
  move that exists. What is left is the shot the mode is actually about: get to a post, plant,
  then drive a ball across the court and through a mouth two dozen units wide.
- **The loop has no closed form.** The ball's position is emergent, the post set is redrawn from
  a fresh seed every match, posts are consumed and re-contested, and a spent post is progressively
  harder to reuse because the dais it paid out is now standing around it.

The **aiming window falls out for free**: a ring lands `PlaceSwitchActionSO.placementDistance`
(150u) out along the course, so a pilot flies *at* a post and presses when it is about that far
ahead. `placementDistance` stopped being a number nobody had to think about and became the mode's
timing.

**Why the layout is derived rather than replicated.** A switch placement re-executes on every peer
(the action handler's ServerRpc→ClientRpc trip), so the socket book has to agree everywhere or one
machine builds a ring where nobody else has one — permanently, since nothing about a placed switch
is replicated. The layout is therefore a pure function of one replicated `int` seed plus the
already-replicated court radius (the SkimRace track-seed shape), and it takes **no input that
lags**: no ball positions, no velocities, nothing a client can hold a different opinion about.
Occupancy reads `ScarabSwitch.Live`, itself built per-peer from the same replicated presses, so
the claim book cannot desync further than the switch list already does. The seed is forced **odd**
so that `0` can mean "not published yet" — the radius and the seed are separate NetworkVariables
whose callbacks can fire in either order.

**A post is an EMBLEM, never a switch.** It is drawn as a core sphere with a **tilted, spinning
halo** at the mouth radius a ring will get, because `Docs/ToySystem/ARCHITECTURE.md` § "The switch"
reserves the continuous ring square across the flight path for something you **thread**. Threading
a post does nothing, so a post that looked like a switch would be a lie about a trigger volume
that does not exist. The halo doubles as a statement of the mouth size on offer — a MASS-heavy
Scarab's ring overhangs its socket, which is the upgrade reading itself out on the court.

Markers are generated meshes, not prisms: they are not food, not conserved mass, and not on the
cell's volume ladder. The monuments a paid toll raises are the mass in this mode.

**The layout is MEASURED, not eyeballed.** `author_tollway_assets.py` walks the shipped numbers
over 400 seeds per intensity — a deliberate re-derivation of `TollwayTollPosts.ComputeLayout` in
Python, so if the C# moves and the generator does not, the two disagree and the build fails — and
asserts two properties that are arithmetic rather than taste:

| intensity (posts / court) | worst min pair | median min pair | worst post-to-centre |
|---|---|---|---|
| 1 — 10 / 480 | 190.2 | 237.7 | 192.1 |
| 2 — 12 / 560 | 202.5 | 252.6 | 224.1 |
| 3 — 14 / 640 | 214.2 | 266.7 | 256.1 |
| 4 — 16 / 720 | 225.4 | 277.7 | 288.1 |

- **No two posts closer than the 70u claim radius** (worst case 190u, ~2.7× clear). Closer than
  that and one requested centre sits inside two sockets at once, which makes `TryResolve`'s
  "nearest free" a coin-flip near the midpoint and lets one planted ring shadow its neighbour's
  aim.
- **No post within `claimRadius + mouth` (94u) of the middle** (worst case 192u, ~2× clear).
  Sockets have to be places you *fly to*; one near the centre is claimed incidentally by anyone
  crossing the court. Note the crystals are no help here — the nucleus IS the court in this mode,
  so they respawn across the whole volume rather than in a core, and the band's inner edge
  (`postInnerCourtFraction` 0.40) is the only thing holding the middle open.

One consequence worth knowing rather than designing around: a dais's planar band reaches
**155.3 × ringRadius/20** (`SCARAB.md §5.1`), which at the switch's own 24u mouth is ~186u — about
the minimum post separation. So a paid toll crowds its *neighbouring* sockets with monument, and a
MASS-heavy Scarab's dais blankets several. That does not block placement (the resolver's occupancy
test is live switches, not prisms) but it does block the ball's line, so the court genuinely gets
harder to build in as the match runs. Emergent, not authored, and untested — see the follow-ups.

**What it cost.** The toll target came down **12 → 8** and the comeback rate went **0.5 → 0.75**
with it, because a toll is now several times the work it was and because
`bonusLevels = deficit × rate` makes the rate a function of the target (the trap `DOGFIGHT.md`,
`BENDS.md` and `WILDLIFE_LIBERATION.md` all record — this is its fifth outing). At 8 and 0.75 a
quarter-of-target deficit still buys 1.5 element levels, exactly what the 12-toll race gave. The
cell's volume ladder was restated in the same currency: Restless at the trail band + **6**
monuments, Frenzy + **16**.

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

The toll posts added a **second** one: `PlaceSwitchActionExecutor.PlacementResolver`, a static
mode veto on *where* a ring may go. It is the sibling of `ScarabBallForge.ForgeGate` and makes the
same argument — a rule about how a mode uses an ability belongs to the mode, and putting it on the
vessel would make freestyle and Scramble inherit it. Two constraints on anyone else who installs
one:

- **It must be a pure function of replicated state.** It is consulted on every peer, so a resolver
  that answered differently on two machines would build a switch on one and not the other,
  permanently. Tollway's reads only its seed-derived socket book and the live switch roster.
- **It is consulted before the charge is spent**, so a refusal costs the pilot nothing but the
  press — the same shape as the existing no-charge refusal. It is identity-guarded on removal
  (`if (PlacementResolver == ResolveSwitchPlacement)`), because a leaked resolver would silently
  refuse every switch in the next scene.

## Class inventory

| Class | Role |
|---|---|
| `TollwayController` | Match director: court build (the nucleus resize; no per-ball boundary), the `OnThreaded` subscription and server scoring, chain tracking, toast beats (toll / chain / match point / lead change), AI steering + **AI ring planting**, fauna exclusion sweep, final-score snapshot |
| `TollwayTollPosts` | The socket book: seed-derived layout (Fibonacci sphere, seeded spin, radii drawn in a band), the free/claimed test against `ScarabSwitch.Live`, the snap-or-refuse `TryResolve` the placement resolver calls, and the emblem markers |
| `TollwaySettingsSO` | Court radius and post count per intensity, the post band + claim radius + marker size, chain window, fauna exclusion, AI dials. Deliberately owns nothing about the switch or the ball |
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

Once rings could only go into toll posts, the timer stopped being a metronome and became a
**cooldown**: an AI presses only when a ring pressed *right now* would land in a free post, tested
with the executor's own arithmetic (the ring lands `placementDistance` out along the course) asked
of the same asset, so the two cannot drift. Failing the test costs nothing — the cooldown is left
alone and the AI tries again next frame, which is what makes the steering below pay off.

Steering has three states, and the first is new with the posts: **no ring of yours standing → fly
to the nearest free post**, because a ring is the only thing that scores and it can only go in a
socket, so escorting a ball with nowhere to put it is the mode's one dead end. Then the Scramble
shape: no team ball → fetch the nearest omni crystal (forging happens by flying through it); team
ball live → escort it, aiming behind the predicted ball on the far side from **your domain's**
nearest ring. An AI deliberately never aims at an enemy ring; it will still thread one
occasionally, which is the mode working.

The HUD arrow follows the same order (`TollwayObjectiveProvider`), so a new player is taught the
loop in the order they have to perform it: *go to a post → take your ball to your ring → go make
a ball*.

## Cell ecosystem

The standard Cell owns the environment. `Tollway Cell Config` is **forked from
`Scarab Scramble Cell Config` for exactly one reason — the volume ladder** — and reuses that
arena's spawn profile, fauna species, membrane, nucleus and cytoplasm verbatim (the cell is
per-arena, not per-mode). The nucleus IS the court:
`SetNucleusWorldRadius(courtRadius)` + **`NucleusIsControlZone = false`** (play geometry, not a
claim — skip it and the whole pitch is inedible, `Docs/ECOSYSTEM.md §25.1`).

**Why the ladder had to be re-authored.** In Scramble a switch dais is a rare event, so its gates
are "the trail band plus 3 and 7 spent switches" (Restless 164,000 / Frenzy 391,000). Here a
**toll IS a dais**, so a match raises three to five times the mass and both of Scramble's gates
would be crossed before the race was half run — after which the ladder conveys nothing. Restated
in the currency this mode actually runs on, at **50,773 volume and 255 prisms per monument**:

| gate | arithmetic | value |
|---|---|---|
| `RestlessEnterVolume` | 12,000 trail band + **8** monuments | **418,000** |
| `RestlessExitVolume` | | **414,000** |
| `FrenzyEnterVolume` | 36,000 trail band + **20** monuments | **1,051,000** |
| `FrenzyExitVolume` | | **1,045,000** |
| `RestlessEnter` (count backstop) | 900 + 8 × 255 × ~1.6 headroom | **4,160** |
| `RestlessExit` | | **4,060** |
| `FrenzyEnter` (count backstop) | 3,000 + 20 × 255 × ~1.6 | **11,160** |
| `FrenzyExit` | | **10,950** |

The trail band and the headroom factor are Scramble's, unchanged; only the monument count
differs. `author_tollway_assets.py` asserts both that Frenzy is **not** reachable inside the
Restless monument budget (or the top of the ladder is dead early) and that it **is** reachable in
a maximum-length match (34 monuments — the winner's 12 plus 11 for each losing domain), so the
ladder can neither saturate early nor be unreachable.

The read this buys is the good one the volume spine exists for: **the cleanup crew arrives in
proportion to how much has been scored.** Fauna wait outside the court while the cell is Calm
(`Cell.FaunaExclusionRadius`, swept — the Astro League pattern) and pour over the wall to graze
the monuments once it leaves.

Crystals spawn inside the court by the platform's own rule (the omni respawn volume IS the
nucleus), count per intensity, neutral domain.

**Collider budget.** The arena's growth is bounded **by the win condition, not by a culler**: at
most 34 monuments (12 + 11 + 11) × 255 prisms = **8,670 prisms**, comparable to PeelTheCage's
intensity-1 cage (10,620) and well inside Atlantis (~69k). A typical match lands nearer 15–20
monuments. Rings themselves cost nothing — `ToyFactory.AddSwitchRing` is a generated mesh with
**no collider**, which is also why a vessel flies straight through one and only a ball can
trigger it. Balls carry one SphereCollider each, capped by `AstroLeagueBall.cellBallLimit`; fauna
are bounded by `MaxLivePopulation`.

## Shared-code touchpoints (why non-Tollway files are in this branch)

| Change | File |
|---|---|
| `Tollway = 48` | `_Scripts/Data/Enums/GameModes.cs` (+ `EnumIntegrityTests` count 46 → 47; 45/46/47 went to Switchback, Hijack and Drumfire upstream) |
| `OnThreaded` + `Live` roster + `PlacerName`/`PlacerDomain`/`RingRadius` | `Vessel/R_VesselActions/ScarabSwitch.cs` |
| Switch charge RECHARGE, standing-ring ceiling, threading refund | `PlaceSwitchActionSO` / `PlaceSwitchActionExecutor` / `PlaceSwitchAction.asset` / `Scarab.prefab` (see `SCARAB.md §5.2`) |
| `tollwayTollTarget` live/build/getter/window rows, default 8 | `EndConditionOverridesSO` + `EndConditionOverridesWindow` + `Resources/EndConditionOverrides.asset` |
| `case GameModes.Tollway → Goals` | `ElementalComebackSystem.DefaultSourceFor` + `ElementalComebackSystemTests.LiveSourceCases` |
| Objective-provider case | `_Scripts/UI/MiniGameHUD.cs` |
| `GameToastSituation` 70–74 (toll, chain, match point, lead change, ring hint) | `_Scripts/Data/Enums/GameToastSituation.cs` |
| Charge-count re-tint only on a CHANGE (a continuous recharge fires the event every frame) | `_Scripts/UI/Controller/ScarabHUDController.cs` |

## In-editor verification (authored headless — NOT yet run)

1. **Open** `MinigameTollway.unity`: every script reference resolves (no *Missing (Mono Script)*);
   the `Game` GO carries `TollwayController` + `TollwayTollTurnMonitor`, and the controller's
   `settings` / `rule` / `arenaCell` / `cellData` are all wired.
2. **Enter play** (solo + AI backfill, intensity 1): the court sphere ≈480 blooms as the nucleus;
   crystals appear inside it; no console errors.
3. **Find the posts**: scattered through the court are ~10 emblem markers — a small sphere inside
   a tilted, slowly spinning halo. Confirm they are unmistakably NOT switch rings (a switch ring
   is one continuous ring square across the flight path) and that flying through one does nothing.
4. **Placement is refused away from a post**: press the switch control (A / Button1) in open
   space. **Nothing happens and no charge is spent** — the console logs a verbose refusal on the
   `ScarabSwitch` channel (FrogletTools ▸ Toolbox ▸ Logging).
5. **Plant a ring IN a post**: fly at a marker and press when it is ~150 u ahead. A
   domain-coloured ring blooms **centred exactly on the post**, facing **along your course** —
   approach the same post from two directions to confirm the position is the post's and the
   facing is yours. The HUD's Mass icon steps down one charge.
6. **A claimed post refuses a second ring**: press again at the same post → refused. Fly to the
   next one.
7. **Recharge** (the sibling change): wait ~20 s → the charge count steps back up, and the icon
   re-tints. Confirm you can place three, wait, and place more — this is the fix for the switch
   being single-use.
8. **Ceiling**: plant a 4th ring → your OLDEST ring shrinks away over ~0.5 s and pays no dais,
   and its post frees up for anyone.
9. **Score a toll**: forge a ball (fly your SKIMMER through a bright/omni crystal) and drive it
   across the court and through your ring → the ring vanishes, the scarab-wing dais rises around
   the spot over a few frames, the toast reads `{name} collects a toll - n/8`, the HUD domain sums
   move, and **you get a charge back**. This is the shot the whole redesign exists to create —
   confirm it takes real ball control rather than a nudge.
10. **The central rule**: knock a ball through an **enemy's** ring → it scores for THEM and
    refunds THEM. Confirm nothing scores for you.
11. **Chain**: get one ball through two of your rings inside 4 s → `CHAIN x2!` toast.
12. **A ball survives a toll**: confirm the ball flies on after paying rather than detonating.
13. **The HUD arrow teaches the loop**: with no ring standing it points at the nearest free POST;
    plant one and it points at the ring (measured from your ball); with a ring and no ball it
    points at an omni crystal.
14. **AI**: watch an AI domain — it should fly to a free post, plant, then escort its balls toward
    its own rings, at most one ring per ~22 s. **An AI domain must be able to reach the target on
    its own.**
15. **Match end**: first domain to 8 → winner banner, scoreboard, Play Again reloads (and the next
    match draws a DIFFERENT post layout).
16. **MPPM two-client**: the post markers are in the SAME places on both machines (the seeded
    layout); a client's ring appears on the host at the same post and size; a toll scored on
    either machine moves both scoreboards; an AI's ring is visible on the client (the
    replicated-press path).
17. **Ecology**: play on until the monuments silt the court (~317,000 volume, roughly 6 tolls) →
    the cleanup crew pours over the court wall and grazes the daises.

## Known limitations / follow-ups

- **Every number here is authored, not play-tested.** The toll target (8), the post count
  (10–16), the post band (0.40–0.85 of the court) and claim radius (70 u), the AI ring cooldown
  (22 s), the chain window (4 s), the crystal ladder and the volume ladder are all first-pass.
  The post numbers are the ones most worth a play-test: too few posts and every match is the same
  three shots, too many and one is always conveniently to hand — which is the degenerate
  placement the mechanism exists to remove, back by the front door.
  The ladder in particular is an ESTIMATE pending FrogletTools ▸ Ecology ▸ Measure Cell
  Environment Baselines, exactly as Scramble's is.
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
- **An AI picks the NEAREST free post, not the best one.** It flies to whichever socket is closest
  and plants there. Since it is otherwise flying at crystals and balls its rings land near
  traffic, which is good enough for v1 — but choosing a post near a predicted ball line, or near
  an *enemy* ball's line (which is where the points actually are in this mode), is the obvious
  improvement.
- **A post is never re-earned; it just frees up.** A spent post is immediately re-claimable, and
  the only thing making that harder is the dais now standing around it. That is a pleasing
  emergent consequence rather than a designed one, and it has not been play-tested — if the same
  two posts get farmed all match, the lever is a cooldown on the socket rather than on the pilot.
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
