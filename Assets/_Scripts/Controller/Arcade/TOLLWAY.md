# Tollway — Technical Documentation

## Overview

**Tollway** is the Scarab-only **ring race**, and the mode built on the one idea the vessel's
own design record calls its best and no shipped mode had ever used
(`R_VesselActions/SCARAB.md §5`): **a switch pays its PLACER when ANY ball threads it, friend or
enemy.**

> Plant rings anywhere you like. Every ball that threads one — yours, theirs, a stray off the
> wall — pays the pilot who planted it and raises a monument on the spot. Rings are spent when
> they pay, so keep planting. First team to 12 tolls.

2–4 players, 2–3 domains, AI backfill, through the same unified Netcode scene pipeline as every
other domain minigame.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameTollway.unity` (cloned from
  `MinigameScarabScramble` — same arena machinery, mode wiring swapped in place at the same
  fileIDs)
- **GameMode enum**: `GameModes.Tollway = 45`
- **Controller**: `TollwayController : MultiplayerDomainGamesController` (1 round / 1 turn,
  `HasEndGame=false`, `UseSceneReloadForReplay=true`, server winner detection in
  `OnTurnEndedCustom` → snapshot `SyncFinalScores_ClientRpc` — the DogFight/Scramble shape)
- **Scoring**: `TollwayScoringRuleSO : AstroLeagueScoringRuleSO` (`metric = Goals`, points not
  golf; race to `GameDataSO.GoalTargetCount` over domain sums). Toll target lives in
  **EndConditionOverridesSO** (FrogletTools ▸ Game Modes ▸ End Game Conditions), default **12**,
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

## The design, and why each rule is the inverse of a sibling's

Astro League and Scarab Scramble are both "get the ball through the ring". Tollway keeps the
ball and inverts everything around it.

1. **The scoring surfaces are PLACED BY PLAYERS and CONSUMED ON USE.** There is no net to defend
   and no arena-owned hoop. A ring is one point; it is spent the moment it pays and has to be
   replanted. That makes *where the next ring goes* the whole strategy layer — and it is why the
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

## Class inventory

| Class | Role |
|---|---|
| `TollwayController` | Match director: court build (the nucleus resize; no per-ball boundary), the `OnThreaded` subscription and server scoring, chain tracking, toast beats (toll / chain / match point / lead change), AI steering + **AI ring planting**, fauna exclusion sweep, final-score snapshot |
| `TollwaySettingsSO` | Court radius per intensity, chain window, fauna exclusion, AI dials. Deliberately owns nothing about the switch or the ball |
| `TollwayScoringRuleSO` | Thin subclass of the Astro League rule so the mode owns its asset |
| `TollwayTollTurnMonitor` | Resolves the toll target from `EndConditionOverridesSO`, NV-syncs it, publishes `GameDataSO.GoalTargetCount`, ends the turn via `rule.IsObjectiveReached`, shows the local DOMAIN's deficit |
| `TollwayObjectiveProvider` | HUD arrow, three steps: your nearest own-domain ring (measured **from the ball**, so it names the ring you would actually herd it into) → the ball → the nearest omni crystal |

## AI — and why it is not optional here

**An AI that cannot plant a ring cannot score in this mode**, so an all-AI domain would be an
opponent that could not play. That makes AI ring planting a correctness requirement rather than
polish, and it is the one place this mode reaches past the Scramble template.

`TollwayController.TickAISwitchPlacement` plants a ring for each AI on a timer through
**`R_VesselActionHandler.PerformShipControllerActionsReplicated`** — the same owner→server→
every-peer trip a human's press makes — and NOT through `AIPilot.abilities`, which calls
`StartAction` locally. An AI pilot runs server-only, so a local press would build the ring and
lay its dais **on the server alone**: invisible to every client, and conserved mass that exists
on one machine. The platform already records the rule ("replicate an AI's press when the
ability's output does not already ride some other replicated channel"), and a placed structure
rides nothing. The control is asked for by ability TYPE (`TryGetInputForAction<PlaceSwitchActionSO>`),
so a future rebind keeps working.

Steering is the Scramble shape with the destination swapped: no team ball → fetch the nearest
omni crystal (forging happens by flying through it); team ball live → escort it, aiming behind
the predicted ball on the far side from **your domain's** nearest ring. An AI deliberately never
aims at an enemy ring; it will still thread one occasionally, which is the mode working.

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
| `Tollway = 45` | `_Scripts/Data/Enums/GameModes.cs` (+ `EnumIntegrityTests` count 43 → 44) |
| `OnThreaded` + `Live` roster + `PlacerName`/`PlacerDomain`/`RingRadius` | `Vessel/R_VesselActions/ScarabSwitch.cs` |
| Switch charge RECHARGE, standing-ring ceiling, threading refund | `PlaceSwitchActionSO` / `PlaceSwitchActionExecutor` / `PlaceSwitchAction.asset` / `Scarab.prefab` (see `SCARAB.md §5.2`) |
| `tollwayTollTarget` live/build/getter/window rows, default 12 | `EndConditionOverridesSO` + `EndConditionOverridesWindow` + `Resources/EndConditionOverrides.asset` |
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
3. **Plant a ring**: press the switch control (A / Button1). A domain-coloured ring blooms 150 u
   ahead **along your course** — drift and place again to confirm it goes where you are *going*,
   not where your nose points. The HUD's Mass icon steps down one charge.
4. **Recharge** (the sibling change): wait ~20 s → the charge count steps back up, and the icon
   re-tints. Confirm you can place three, wait, and place more — this is the fix for the switch
   being single-use.
5. **Ceiling**: plant a 4th ring → your OLDEST ring shrinks away over ~0.5 s and pays no dais.
6. **Score a toll**: forge a ball (fly your SKIMMER through a bright/omni crystal) and drive it
   through your own ring → the ring vanishes, the scarab-wing dais rises around the spot over a
   few frames, the toast reads `{name} collects a toll - n/12`, the HUD domain sums move, and
   **you get a charge back**.
7. **The central rule**: knock a ball through an **enemy's** ring → it scores for THEM and
   refunds THEM. Confirm nothing scores for you.
8. **Chain**: line up two of your rings and put one ball through both inside 4 s →
   `CHAIN x2!` toast.
9. **A ball survives a toll**: confirm the ball flies on after paying rather than detonating.
10. **AI**: watch an AI domain — it should plant its first ring ~5 s after the countdown and one
    roughly every 22 s, and should escort its balls toward its own rings. **An AI domain must be
    able to reach the target on its own.**
11. **Match end**: first domain to 12 → winner banner, scoreboard, Play Again reloads.
12. **MPPM two-client**: a client's ring appears on the host at the same place and size; a toll
    scored on either machine moves both scoreboards; an AI's ring is visible on the client (the
    replicated-press path).
13. **Ecology**: play on until the monuments silt the court (~418,000 volume, roughly 8 tolls) →
    the cleanup crew pours over the court wall and grazes the daises.

## Known limitations / follow-ups

- **Every number here is authored, not play-tested.** The toll target (12), the AI ring cadence
  (22 s), the chain window (4 s), the crystal ladder and the volume ladder are all first-pass.
  The ladder in particular is an ESTIMATE pending FrogletTools ▸ Ecology ▸ Measure Cell
  Environment Baselines, exactly as Scramble's is.
- **A switch can be missing on a third peer.** Pre-existing and not introduced here: placement
  runs on every peer against that peer's own charge meter, and an elemental crystal's grant is
  replayed only onto the vessel's OWNER, so in a match with three or more machines a third peer
  can be a charge short and refuse a ring the placer built. The recharge makes the meters agree
  *more* than they did. The real fix is a can-this-action-run veto on `ShipActionSO` consulted
  before the RPC goes out; see `SCARAB.md §5.2`.
- **AI ring placement is unaimed.** An AI plants along its own course wherever it happens to be.
  Because it is already flying at crystals and balls its rings land near traffic, which is good
  enough for v1 — but placing near a predicted ball line is the obvious improvement.
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
