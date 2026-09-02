# Toy System — Architecture

> Freestyle is ~¼ of the final product. While we are a party game, that is only half —
> what makes the rest right is that it is filled with **toys** instead of party games.
> A toy has **no score and no end condition**; it is something you can play with
> indefinitely. The **toybox** is the set of toys the player has unlocked, surfaced in
> the Menu_Main lava-lamp / freestyle world.

This is a new platform **fundamental** (`Toy`), added at the prompter's request. It
composes with the existing fundamentals rather than working around them:

| Composes with | How |
|---|---|
| **Vessel** | the Vessel Changer toy cycles the player's ship via the existing networked swap |
| **Domain** | the Domain Changer toy cycles the player's team colour via the server RPC |
| **Prisms / Mass** | the Painting toy lays a *conserved-mass* prism pattern (no caps/TTLs); the Wanderway conveyor transports a fixed stock of conserved prisms |
| **Cells** | toys are placed relative to the Cell membrane (read, never duplicated); Wanderway lifeforms spawn *into* the cell as ordinary citizens; the Arkway stands whole satellite Cells (the mode preview's machinery) as its corridor |
| **Flora & Fauna / Crystals** | Wanderway meadow/menagerie scenes release flora/fauna through the canonical cell spawn sequences and lay skimmable elemental crystals; the Arkway's traversal cells run their real fauna waves, which attack or defend the Ark by the shipped diet rules |
| **Ark** | the Arkway is the Ark fundamental's first vehicle: a prism-bodied mothership whose pace is the voyage's clock and whose hull is grazeable conserved mass |

A toy imposes no decay, no timer, and no win/lose — consistent with *Mass is conserved*
and *don't cheat emergence*.

## What a toy is

A **toy** is a world-space interactive station the **local** player's vessel flies into
to activate. It is modelled on the existing menu world-triggers (`FreestyleSign`,
`ModeSelectTrigger`, `ShapeSign`): a trigger collider + `GetComponentInParent<VesselStatus>`
detection. On top of that base the `Toy` class adds:

- **Local-user gating** — only the locally-owned, non-AI vessel can trip a toy
  (`IVesselStatus.IsLocalUser`). Remote/party vessels never trip your toys.
- **Freestyle-only gating** — toys are visible in the lava lamp but inert while the vessel
  is on autopilot; they only activate once the player takes control
  (`ToyContext.IsFreestyleActive`).
- **Continuity-law bloom-in** — every toy scales from zero on spawn (nothing pops in).
- **A switch ring** — every toy is drawn inside one continuous ring, square across your flight
  path, **at the radius of its own trigger collider**, in the **prism shader**. The ring is the
  affordance: thread it and something fires, and *which prism it is painted as* says what.
  See "The switch" below — there are no exemptions left.
- **Exit-gated re-arm** — a toy is *not consumed*, but it re-arms **only once the local vessel
  has flown clear of its trigger volume** (a per-frame distance poll with `exitRadiusMultiplier`
  hysteresis, robust to the swap's despawn/respawn which fires no `OnTriggerExit`). A swap toy
  that flips to the option you just left also **re-grows slowly** (`regrowDuration`, default ~5s)
  and stays inert while it does. Together these stop a toy from immediately switching you back
  before you can escape it. And when any toy in a set fires, the coordinator disarms the **whole
  set** (`Toy.Disarm`) so a vessel that re-spawns on top of a neighbour can't chain-trigger it.
  `Toy` exposes `bloomDuration`, `regrowDuration`, and `exitRadiusMultiplier` as serialized knobs.

### Every toy declares a category

`ToyDefinitionSO.Category` (`ToyCategory`) divides the toybox by **what a toy changes**, which is
the same thing as **which fundamental it composes with**:

| Category | What it changes | Composes with | Today |
|---|---|---|---|
| **Pilot** | YOU — the hull you fly or the colours you wear. The world is exactly where you left it. | Vessel, Domain | Vessel Changer, Domain Changer |
| **World** | WHERE YOU ARE — a world arrives or leaves. The heaviest thing any toy does. | Cells | Cell Selector, Wanderway, Arkway |
| **Creation** | LEAVES SOMETHING BEHIND that lives on without you. | Prisms/Mass, Flora & Fauna | Connect the Dots, Lifeform Matrix |

It is **abstract and declared in code**, never a serialized field: a toy's category is a property
of what it *does*, and an authored field is a field that can disagree with the behaviour underneath
it. Abstract also means a new toy **cannot be added without saying which fundamental it reaches
for** — and a toy that fits none of the three is the signal to have the fundamentals conversation
(CLAUDE.md, *Process for curating fundamentals*), not to widen the enum.

The in-game encyclopedia reads it: the **Tools** kingdom of the Codex groups its pages by exactly
this, Pilot → World → Creation. See `Docs/CODEX.md` §3.5.

## File map

| Role | File |
|---|---|
| Toy base (trigger, bloom, gating, re-arm, **switch ring**) | `Assets/_Scripts/Controller/Toys/Toy.cs` |
| Switch-ring layout gate (CI-style, no Unity) | `Tools/Build/toy_switch_ring_geometry.py` |
| One-toy-opens-into-many base | `Assets/_Scripts/Controller/Toys/MatrixToy.cs` |
| Shared matrix station (fly-through choice) | `Assets/_Scripts/Controller/Toys/ToyMatrixStation.cs` |
| Cell Selector (world picker + reset) | `Assets/_Scripts/Controller/Toys/CellSelectorToy.cs` |
| Scale model of a cell environment | `Assets/_Scripts/Controller/Toys/CellMiniatureBuilder.cs` |
| Cell Selector config | `Assets/_Scripts/ScriptableObjects/Toys/CellSelectorToyDefinitionSO.cs` |
| Runtime cell swap (the toy's one entry point) | `Assets/_Scripts/Controller/Environment/Cell.cs` (`RequestCellSwap`) |
| Coordinated toy (reports activation to its set) | `Assets/_Scripts/Controller/Toys/SwapToy.cs` |
| Shared "set + flip" coordinator (generic) | `Assets/_Scripts/Controller/Toys/SwapToySetCoordinator.cs` |
| Shared runtime refs handed to each toy | `Assets/_Scripts/Controller/Toys/ToyContext.cs` (`ToyContext` + `ToyPlacement`) |
| Procedural body/label/collider builder | `Assets/_Scripts/Controller/Toys/ToyFactory.cs` |
| Switch shader vocabulary (the enum) | `Assets/_Scripts/Data/Enums/ToySwitchSignal.cs` |
| Switch vocabulary law test | `Assets/_Scripts/Tests/Editor/ToySwitchVocabularyTests.cs` |
| Prefab → display-only model (shared icon engine) | `Assets/_Scripts/Controller/Toys/ToyModelBuilder.cs` |
| Mini vessel model (hull filter over the above) | `Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs` |
| Vessel Changer (matrix of ships) | `Assets/_Scripts/Controller/Toys/VesselChangerToy.cs` |
| Domain Changer set | `Assets/_Scripts/Controller/Toys/DomainChangerToySet.cs` |
| Painting gallery (matrix of paintings) | `Assets/_Scripts/Controller/Toys/PaintingGalleryToy.cs` |
| Painting station (one per painting) | `Assets/_Scripts/Controller/Toys/PaintingToy.cs` |
| Multi-stroke fly-by-numbers runner | `Assets/_Scripts/Controller/Toys/PaintingRunner.cs` |
| Painting data (strokes + domains) | `Assets/_Scripts/ScriptableObjects/Toys/PaintingDefinitionSO.cs` |
| Preset generators (Star…Peacock, 16) | `Assets/_Scripts/Controller/Toys/PaintingPresetLibrary.cs` |
| Sophisticated-stroke library (curves + curl field) | `Assets/_Scripts/Controller/Toys/PaintingStrokeToolkit.cs` |
| Painting progress persistence | `Assets/_Scripts/Controller/Toys/PaintingProgressStore.cs` |
| Drawing state (per-prism pose/domain) | `Assets/_Scripts/Controller/Toys/PaintingPrismStore.cs` |
| Web share export (inline-WebGL viewer) | `Assets/_Scripts/Controller/Toys/PaintingShareExporter.cs` |
| Toy-root emblem (core + orbiting satellites) | `Assets/_Scripts/Controller/Toys/ToyEmblem.cs` |
| Emblem build pump (one slot per frame) | `Assets/_Scripts/Controller/Toys/ToyEmblemStreamer.cs` |
| Element crystal model (shape signature) | `Assets/_Scripts/Controller/Toys/ElementCrystalModelBuilder.cs` |
| Flora icon (growth-pattern simulation) | `Assets/_Scripts/Controller/Toys/FloraIconBuilder.cs` |
| Growth-preview contract (pure, spawns nothing) | `Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs` (`TryPreviewGrowth`) |
| Idle spin for toy bodies | `Assets/_Scripts/Controller/Toys/ToyIdleSpin.cs` |
| Conveyor ("Wanderway") toy | `Assets/_Scripts/Controller/Toys/ConveyorToy.cs` |
| The wander run (canvas + tether + exits) | `Assets/_Scripts/Controller/Toys/WanderwayRun.cs` |
| Return station at the tether's end | `Assets/_Scripts/Controller/Toys/WanderwayReturnToy.cs` |
| Conveyor belt runner (+ the load-veil prime) | `Assets/_Scripts/Controller/Toys/MicrosceneConveyor.cs` |
| One conveyor scene (lay/transport/re-arrange) | `Assets/_Scripts/Controller/Toys/Microscene.cs` |
| Grand assemblies (the monument-scale eight) | `Assets/_Scripts/Controller/Toys/MicroscenePatternsGrand.cs` |
| Microscene recipe generators (pure) | `Assets/_Scripts/Controller/Toys/MicroscenePatterns.cs` |
| Microscene structural painter (domain/kind/scale) | `Assets/_Scripts/Controller/Toys/MicroscenePainter.cs` |
| Arkway (cellular Wanderway) toy | `Assets/_Scripts/Controller/Toys/ArkwayToy.cs` |
| The voyage run (leash + exits + reset) | `Assets/_Scripts/Controller/Toys/ArkwayRun.cs` |
| The corridor of traversal cells | `Assets/_Scripts/Controller/Toys/CellConveyor.cs` |
| The voyage's screen telegraph (leash countdown, banners) | `Assets/_Scripts/Controller/Toys/ArkwayVoyageHud.cs` |
| The Ark (the fundamental's body) | `Assets/_Scripts/Controller/Environment/Ark.cs` |
| Placement + lifecycle | `Assets/_Scripts/Controller/Toys/ToyboxController.cs` |
| Per-toy config (abstract) | `Assets/_Scripts/ScriptableObjects/Toys/ToyDefinitionSO.cs` |
| Vessel/Domain/Painting configs | `Assets/_Scripts/ScriptableObjects/Toys/*ToyDefinitionSO.cs` |
| Toybox registry + unlock state | `Assets/_Scripts/ScriptableObjects/Toys/ToyboxSO.cs` |
| One-click editor setup | `Assets/_Scripts/Editor/ToyboxSetupTool.cs` |

## Lifeform Matrix (`LifeformMatrixToy` + `LifeformMatrixToyDefinitionSO`)

The **release bench** — everything you can let loose into the cell (`Toy_LifeformMatrix.asset`,
placement angle 180°). Its root wears an emblem (see "Toy-root emblems"): a core of the four
element crystal MODELS — elements have SHAPE signatures, never colours — orbited by its three
KINGDOMS.

**Four passes deep at most, each one a layer further out.** `LayerOrigin(n)` puts layer *n* at
`(1.5 + 2n) × stationSpacing` from the toy along the outward radial — one clear gap of two
spacings per layer, so a new layer *extends* the rhythm the player has already learned rather than
re-tuning the ones before it. The player flies at a matrix and keeps flying: each pass carries
them toward the next layer, never back through the last.

| pass | layer | what blooms |
|---|---|---|
| the toy | 1.5 | the **KINGDOM** row — Fauna, Flora, Vessels (station radius ×1.5, so the first row you meet is the biggest thing in the corridor) |
| Fauna / Flora | 3.5 | that kingdom's **SPECIES** row, one station per registered species |
| Vessels | 3.5 | the **HANGAR** row, one mini hull per class |
| a species | 5.5 | its **VARIANT** row — one station per ELEMENT, four of them |
| a variant | — | that exact lifeform spawns live into the cell |
| a hull | — | an **AI-piloted vessel of that class, in your own domain**, is released |

**The kingdom layer exists because the flat menagerie had outgrown one wall** — 14 species on two
rows (fauna lower, flora upper), with nowhere to put a third kind of thing. Splitting by kingdom
first gives each branch its own row *and* makes "what else can I release?" a question the toy
answers by shape. Opening a layer clears every layer below it, so the matrix is always a single
path outward from the toy rather than an accumulating pile of walls.

**Icons.** Each kingdom station shows a real member of its kingdom, drawn by that kingdom's own
builder: the first fauna species (meshes harvested off the prefab asset by `ToyModelBuilder`,
never instantiated), the first flora species (simulated via `Flora.TryPreviewGrowth` — see
"Station icons"), and the first hull on the roster (`ToyVesselRoster` → `VesselModelBuilder`).
Species and hangar stations are the same, one per entry; only a species that can offer neither a
model nor a growth preview keeps the anonymous sphere. Variant stations wear the element's crystal
model **drawn at that variant's own authored heart size**, as a ratio of the platform default, so
the row shows the real size difference between the four hearts before you touch any of them (a
shark row reads big, a SchwarzP row small). A lifeform is its species and its element and nothing
else — there is no level, so there are no level rows (`Docs/ECOSYSTEM.md` §40).

**Lifeform release.** A variant spawns a POPULATION (fauna `PopulationSize` / flora
`InitialSpawnCount`) through the canonical cell spawn paths, on a runtime CLONE of its per-element
config (`_SO_Assets/Lifeforms/`) with `SpreadElements` off — the station spawns the EXACT variant
it shows, and the authored assets are never mutated. Fauna hatch on the cell's densest mass (a
creature released into empty space beyond the membrane has nothing to graze); flora root AT the
station via `Flora.SetPlantPositionOverride`. Every spawn logs, including the cell's Frenzy
growth-freeze state.

**Vessel release.** A hangar station calls
`MenuServerPlayerVesselInitializer.RequestSpawnAiCompanion(class, domain, pose)` — the menu's
**ordinary networked spawn pipeline**, not a second kind of bot. The host spawns directly; a party
client asks the host over `ClientPlayerVesselInitializer.RequestAiCompanion_ServerRpc`, the same
request/handler shape the vessel swap already uses, so the companion exists once on the server and
replicates to the whole party (a locally-spawned one would be invisible to everyone else). The
server-side chain is deliberately the same one `ServerPlayerVesselInitializerWithAI` runs for a
backfill bot: spawn the Player NetworkObject → stamp `NetIsAI` / vessel class / domain / name →
spawn the vessel → `InitializePlayerAndVessel` → `ConfigureForGameMode(seekPlayers: false)` →
`ActivateAutopilot`. It is an ordinary AI player from there on: it flies the lava lamp, lays
conserved trail mass the food web can graze, and is despawned with every other AI on the way out
of the menu (`SceneLoader.ClearPlayerVesselReferences`).

Four details that are load-bearing:

- **A companion is released UNDER WAY, never dropped at a standstill.** The pair-init hands every
  vessel a dead stop (`ResetForPlay` zeroes speed), and `VesselPrismController`'s spawn loop only
  lays a prism above **3 u/s** — so a bot released at zero lays no trail at all, which reads as a
  broken bot rather than a slow one. The AI's own drift compounds it: a held drift PINS the
  smoothed cruise speed at the value the vessel carried in (`VesselTransformer.StepTowardTarget`),
  so a bot that drifts before it has accelerated stays pinned near zero indefinitely. The release
  therefore does what the vessel swap does — `SetPose` then `SetInitialSpeed`, in that order — with
  `companionLaunchSpeed` (60, the low end of the fleet's flight band) authored on the initializer.

- **The domain is YOURS.** `ToyVesselRoster.PlayerDomain` reads the live local-player mirror at the
  moment of the pass, so the companion joins your side and the mini hulls wear your domain colour —
  and re-tint in place the instant you change domain, because here that colour is a *claim* about
  which side the bot will fly for, not decoration.
- **The bot is claimed, not adopted.** A server-owned Player carries the HOST's `OwnerClientId`, so
  its spawn event is indistinguishable from the host's own. `ClaimExternallySpawnedPlayer` (new on
  `ServerPlayerVesselInitializer`, and now also used per-spawn by the AI backfill) marks it
  processed in the same frame as the spawn, which is what stops the human path from reading the
  event as "the host wants a second vessel". The handler's 200 ms replication delay is what gives
  the synchronous claim time to land.
- **It lands one spacing back toward the cell centre**, facing in. The player is still flying
  OUTWARD through the matrix when it appears, so the two are moving apart — a bot materialising on
  the nose would be a vessel-vs-vessel impact, not a release.
- **The release calls `Player.StartPlayer`, not `ActivateAutopilot`.** For a player whose `NetIsAI`
  is set, `StartPlayer` already runs the autopilot branch itself; calling both started the AI pilot
  TWICE, which duplicates every `UseAbilityCoroutine` — and `AIPilot.StopAIPilot` cannot clean the
  duplicate up, because its `StopCoroutine` is handed a fresh iterator that matches nothing.
  `StartAIPilot` is now idempotent as well, so no caller can reintroduce it.
  `ActivateAutopilot` remains what the menu's HUMAN vessel needs, where `StartPlayer` deliberately
  does not touch autopilot.

**The roster is shared with the Vessel Changer** (`ToyVesselRoster`): one curated default list, one
meta-value/duplicate filter, one hull builder, one domain-preview colour, one re-tint. The two toys
differ in exactly one argument — the changer excludes the hull you are flying (you are about to
become one of the others), the hangar excludes nothing (a wingman in the ship you are flying is a
perfectly good thing to ask for). Author `vesselRoster` on the definition to override the default.

## Cell Selector (`CellSelectorToy` + `CellSelectorToyDefinitionSO`)

The freestyle **world picker** — and the freestyle **reset** (`Toy_CellSelector.asset`,
placement angle 300°). It exists because the freestyle six cost a multi-second
`EnvironmentLoadVeil` hold on *every* entry to Menu_Main, boot and every return from an arcade
game alike.

**The fix is two halves.** The Cell now boots on `CellTypeChoiceOptions.EnvironmentFree` — the
first config authoring no `EnvironmentPrefab` (Blob), so the menu opens with nothing to build.
The **seven** heavy worlds stay in the Cell's list and become **opt-in**: this toy is the only
place that load is ever paid. (The toy authors no cell list — it reads `Cell.AvailableConfigs` —
so adding a world is a config-asset + scene-array change, never a toy change.)

Fly the toy (a sphere ringed by three empty little worlds — they stay empty because filling
them would mean generating environments at menu boot, the exact cost this toy defers) and a
matrix of **mini-cells** blooms outward, `matrixDistanceFactor` × `stationSpacing` clear of the
toy along the outward radial: you fly AT the toy and keep going, and the choices are ahead — the Lifeform Matrix's "fly at a wall of choices" pattern, now sharing
`ToyMatrixStation`. Fly a mini-cell and the cell becomes that world. **Fly the mini-cell of the
world you are already in and you get the same cycle on the same config — that is the reset.**
What a pass costs is told by **shape**, not by a word (see "Station icons"): the world you are
already in wears a **halo ring** — that one is the reset — and an environment-free config has
nothing to model, so its slot draws visibly empty, which is what "instant" looks like. Everything
else is a plain model and builds behind the veil. (The former `RESET` / `INSTANT` / `LOAD` label
line is gone; only the world's name remains, and that is the next label to retire.)

**No parallel list.** With `cells` left empty (the default and the recommendation) the toy
reads `Cell.AvailableConfigs` — the Cell's own `CellConfigs` rotation. The Cell owns the
environment, so there is one source of truth for what a scene's cell can be and the toy cannot
drift from it. Authoring the list is an override for scenes that want a curated subset.

**A station IS its world — no cage, no orb.** Each slot is a genuine **scale model of the world
that config creates**, standing on its own with only its label. (An earlier pass wrapped each one
in gyroscopic membrane rings; they were removed — the model speaks for itself.)
`CellMiniatureBuilder` reads the generator's own output
— `SpawnableBase.GetTrailData()` plus `CellEnvironmentSpawnableBase.CachedLays` for the per-*prism*
domain that `SpawnTrailData` flattens away — strides it down to a point budget (~1.2k), and emits
one small box per sample into **one mesh with a submesh per domain**. So a mini-cell shows the real
silhouette, the real structure, and the real domain composition, in the same prism materials the
world itself is built from. **No prism is ever spawned for a model**: generation is pure math, and
the ~97%-of-cost per-prism `Instantiate` never happens.

Cost control: models stream in **one per frame** after the shells are already up (each blooms in as
it lands), the built meshes are cached on the toy for the session, and the generator's point data is
**released immediately after sampling** (`ReleaseGeneratedData`) — holding seven 34k-entry lay lists
so the menu can show seven thumbnails is the wrong trade on mobile, and re-generating on load is a
small fraction of the lay cost. A config with no environment has nothing to model and draws visibly
**empty** — which is now literally true rather than a stand-in.

**What a selection does** is `Cell.RequestCellSwap` — suction the old world away over a visible
transition, drain it in 500-prism-per-frame slices while it is invisible, then rebuild behind
the standard veil. Since C9 (2026-08-25) the prism half GPU-converges on the cell centre
(`Cell.StampRetiredWorldSuction` → `Prism.StampSuctionToward`); the root `localScale` wait is
for non-prism riders (membrane / nucleus / cytoplasm / spindles). Full step table, ordering
constraints (grids before the immediate build), and the invariant analysis (continuity
upheld; this is active removal, not decay) live in **`Docs/ECOSYSTEM.md §19`**.

**Reset scope.** `clearLooseTrailMass` (default on) also retires the **pooled** prisms the cell
tracks — the vessels' accumulated freestyle trail — which is what makes a selection a scene
reset rather than an environment swap. Prisms owned by a closed toy system (the Wanderway
conveyor transports its own fixed stock, instantiated not pooled) are never touched either way,
so a cell swap cannot break the conveyor's conservation.

## Arkway (`ArkwayToy` + `ArkwayToyDefinitionSO`) — the cellular Wanderway, and the Ark's first vehicle

The **Arkway** is the Wanderway's proposition raised one level: where the Wanderway's belt
recycles prism *assemblies*, the Arkway recycles whole **cells**. Fly the toy and a VOYAGE
begins — a corridor of real satellite `Cell`s (the mode preview's own machinery:
`BindSatelliteRuntime` → `InitializeSatellite`, thinned by `SatellitePrismStride`, injected via
`ToyContext.Container`) opens ahead, three standing at once (**previous / current / next**),
drawn shuffle-bag from the cell selector's own rotation (the definition may author an explicit
list; empty reads `Cell.AvailableConfigs` minus environment-free entries). An **Ark** — a
prism-bodied mothership in the player's domain, the new fundamental's first body — sails the
corridor at its own unhurried pace. It is the stepping stone toward faction missions: venturing
into the hypersea with, and for, a mothership.

**A traversal cell is an ORDINARY CELL** — a nucleus, a control zone, and a crystal at its core —
and the fight is the shipped ecology, composed (full record: `Docs/ECOSYSTEM.md` §41). It keeps
`NucleusIsControlZone` at its default `true`, so control is the NUCLEUS CLAIM (lay environment
mass through the core to take the cell) and the herbivore diet is the shipped SPATIAL rule: the
nucleus is sanctuary, everything outside it is voraciously grazed by any domain. Each cell runs
its REAL fauna waves (`Cell.SatelliteEcologyEnabled`, the one opt-in through the preview's
structure-only gate, scaled by `Cell.RuntimePopulationScale`), and is handed one omni crystal at
its centre (`CellConveyor.SpawnCoreCrystal` — a satellite has no `CrystalManager` feeding it, so
this is the one thing the corridor must supply; it blooms in through the crystal's own fade and,
being manager-less, is collected once).

**The Ark leaves a WAKE** (`Ark.ConfigureWake`): one prism every `arkWakeSpacing` (45) units of
TRAVEL — never on a clock, so the ribbon is dense through the slow pass under a core and sparse
across the open water — at `arkWakeScale` (6×6×12), far larger than a vessel's ~2×2×4 trail prism
so it reads as a ship's wake rather than another pilot's line. It is ordinary conserved mass in
the Ark's domain, laid through `PrismTrailBuilder.LayOne` into its own `Trail`, on a STATIONARY
root (the hull rides the Ark's transform; a wake that rode it too would just be a longer hull).
It is also the honest answer to *why does nothing eat the Ark*: 150 hull prisms against a ~10,000
prism world are a rounding error to a swarm steering at `GetDensestRegionAnyDomain`, and the
targeting grid counts PRISMS, not volume — so a bigger hull would not have helped. A wake is a
dense LINE through the feeding ground that leads to the ship, and its freshest prism is always
about one ship-length astern.

**The Ark does not move until the voyage begins** (`Ark.SetUnderway`, false until `ArkwayRun`
sets `_running`). A build behind a veil is not a pause — every `Update` runs through it — so an
Ark given its course when its hull finished laying sailed for the whole build: at 72 u/s a
40-second load carried it 2,880 units, past the first cell, and the voyage opened with no ship
anywhere (`Docs/ECOSYSTEM.md` §41.3.3.2). The gate is on the MOVEMENT, not on when a destination
is set, so no future reordering can reintroduce it.

**The voyage OPENS when the veil comes down, not when the run's bracket closes.**
`PrismTrailBuilder.EndArenaBuild` only says the run has queued its work; the veil holds until
every traversal cell's lay has drained and settled, 30–90 s later, and nothing pauses the pilot
under it. Everything that opens the voyage — the dock repose beside the Ark, the entrance, the
arrow, the banner, `_running`, `SetUnderway` — therefore waits on `PrismTrailBuilder.IsLoadGateHolding`
(`Docs/ECOSYSTEM.md` §41.3.3.3). The departure pose is the one the toy FIRED at (`_home`), so the
corridor, the Ark and the entrance are stood relative to one point rather than to wherever the
vessel had drifted by the time each was read; the entrance stands abeam of that axis on the
Ark's port side, opposite the flank the pilot docks on, so holding course from the dock never
threads it. Only the FIRST traversal cell stands behind the veil; the second is stood by
`CellConveyor.StandAhead` the moment the voyage opens, streaming in beside live play like every
later cell — two 10k-prism worlds behind the veil was the 30–90 s blind opening, one is half
that. A toy pass during the build is IGNORED, not toggled (`ArkwayRun.IsBuilding`), and
the objective arrow keeps pointing at an Ark that is on screen but further than 900 u
(`ObjectiveIndicator.HideOnScreenWithin`) — on screen and a speck is not "in view".

**The wake is laid `watchForReveal: false` and armed only AFTER the arena-build bracket.**
`PrismTrailBuilder.LayOne` otherwise registers every prism with the arena-ready gate's reveal
watch — correct for the finite cohorts every other caller lays, fatal for a CONTINUOUS source: the
wake kept adding ~1.6 prisms/s to a set the gate was waiting to see empty, so the load veil held
forever with its settling count jittering (`Docs/ECOSYSTEM.md` §41.3.3.1). `Ark.RetireAsync`
disarms it on its first line so a retiring Ark cannot lay into the next voyage's hold.

The Ark's hull is ordinary grazeable conserved mass laid through `PrismTrailBuilder`, sailing
that exterior — so **the swarm eats it the whole crossing, and it is safe only under the core it
is making for**. That is the change: the corridor used to collapse the control zone, which made
every cell legacy opposing-domain territory and left the Ark untouchable by any swarm wearing its
own colour. Control still decides the swarm's COLOUR (and is what the volume gauge reads); the
threat is now SPATIAL, which is what makes the arrival profile matter — the slow run in to the
nucleus is the run through the feeding ground. The Ark moves the
way fauna move (one container transform + the `Prism.NotifyPositionChanged` mover contract per
frame, plus `PrismSpatialIndex.NotifyCellChanged` on a coarse cadence so the local food web is
always the one that can see it).

**The Ark's pace is an ARRIVAL PROFILE, not a speed** (`Ark.SetSpeedProfile`, re-stated every
tick by `ArkwayRun.AimArk`). A ship makes way in open water and comes in slow under a harbour;
both halves are read off the SAME quantity — range to the destination — so one smoothstep gives
both: `arkSpeed × arkCruiseSpeedFactor` (18 × 4) between cells, easing to `arkSpeed` across the
destination cell's own membrane radius, so *the deceleration IS entering the cell*. Speed is a
pure function of position with no acceleration state, so the corridor advancing re-reads it on
the same frame with nothing to unwind. The radius is re-read every tick rather than once at
departure, because a freshly stood cell reports `MembraneRadius` 0 until its membrane spawns
(the `ModePreviewArena.FramingRadius` bug class) and the leg would otherwise run on the fallback.

**The HUD reads the cell you are IN.** The pause button's `DomainVolumeIndicator` used to latch
its cell on first resolve — correct for every arcade mode (one cell, never left) and wrong for the
one toy whose subject is flying from one cell into the next: it stayed pinned to the home cell for
the whole voyage, showing three wedges at zero and a fauna-spawn ring that never moved, which
reads as a broken gauge rather than as a gauge reading somewhere else. It now re-resolves each
sample (4 Hz, a walk over a handful of live cells), keeping the last good answer as the fallback,
so the wedges and the spawn-cycle ring both describe the cell around you. It resolves by **MEMBRANE**
(`Cell.FindCellByMembrane`), not by `ContainsPosition`: that one answers with the SENSING radius,
which a config may widen well past the membrane so fauna can find mass across a big arena
(`SenseRadiusOverride`) — right for prism registration, wrong for a HUD, because a wide-sensing
cell can swallow a neighbouring world and the gauge then names a cell the player is nowhere near.
It also reads
`Cell.GetControlVolume` — the same source `Cell.DominantDomain` reads — so in a nucleus cell it
shows each domain's SHARE of the nucleus claim rather than whole-cell mass; the phase ring is
hidden there, because the ladder is a whole-cell measure and says nothing about the claim. That
one is general, not Arkway-specific: fed whole-cell volume, the gauge could show one domain
leading while `DominantDomain` held the cell for another, with nothing wrong on either side.

**The arrow points at the ARK** (`ArkwayRun` is its own `IObjectiveProvider`, standing one
`ObjectiveIndicator` per live voyage at the canvas ROOT — a mid-hierarchy parent pins it in a
corner). The Ark is the objective in a way no other mode's target is: the voyage is an escort, the
leash is measured from the hull, and the only thing that ends a voyage against the player's will is
the food web reaching it — and at a three-cell-radius leash a 110-unit ship is not findable by
looking. It hides itself whenever the hull is on screen. Deliberately not the core crystal or the
entrance station: an arrow that names two things names neither, and both of those are lit landmarks
already.

**A traversal cell starts EMPTY, and that is a performance rule.** The corridor clones the LIVE
SCENE CELL (there is no prefab to instantiate at runtime), and a live cell ACCUMULATES: `Cell`
parents its authored environment to itself, and every lifeform heart the food web drops is
re-homed onto it (`Crystal.ActivateCrystal` / `DetachHeartToCell`). Cloned verbatim, all of that
lands in every traversal cell, three standing at a time, for the whole voyage — so a session that
has been running a while makes each new cell more expensive than the last, which is exactly the
shape of *the world got sparser and the frame rate got worse*. `CellConveyor.StripAccumulatedContent`
re-parents every `Prism` / `Crystal` / `LifeForm` / `Toy` / `NetworkObject` branch of the clone
into an INACTIVE scrap root and destroys it with that root — inactive because `Destroy` alone
defers to end of frame and the `root.SetActive(true)` a few lines later would wake every doomed
object first. It is a DENYLIST of content types, not an allowlist of components: the cell's own
structure is whatever the prefab author put there and must survive untouched, while the things
that accumulate are a short, knowable list.

Two smaller sweeps ride with it. A struck world's root is deliberately orphaned so the cell can
die immediately while its mass drains a slice per frame — which also means nothing else can
collect it, so `_retiringRoots` tracks them and teardown sweeps them (*an object deliberately
orphaned for the duration of an async is an object whose async no longer owns its cleanup*). And
the cell teardown path's telemetry moved to the new `CSLogChannel.CellLifecycle`: `ResetRuntimeData`
logged **one line per crystal it destroyed**, plus spawner start/stop and a line per cell stood —
written for a world built once at scene load, and running on a loop here. The per-crystal one is
guarded on `IsVerbose` before the interpolation, because `LogVerbose` is `[Conditional]` and
removes the CALL in a release build but not the argument evaluation in the Editor.

**`CellConveyor.Census()`** prints everything the corridor holds — standing cells, tracked prisms,
drains, orphaned roots, the config bag — and `ArkwayRun.LogCensus` adds the hull, the wake, both
trail ribbons, the marks and the withering list, once per crossing on that same channel. An
infinite toy needs a way to answer *what is growing?* from a play test; reading the code does not
settle it.

**The leash**: stay within a few cell radii of the Ark (`CellConveyor.CurrentCellRadius` ×
`leashRadiusFactor`, **3**, membrane-read-per-tick with the 0-until-spawned fallback — 3600 u
against a 3200 u corridor spacing, so a pilot can range out and explore a cell instead of flying
formation with the hull). Beyond it a
telegraphed countdown runs on `ArkwayVoyageHud` — a programmatic screen overlay, because
`GameToastAPI` renders nothing in Menu_Main and a leash telegraph must reach a player who is by
definition far from every world label — and then the Ark RECALLS you to its side (pen-up around
the `SetPose`, the mode preview's teleport idiom). The voyage never ends over distance.

**The trail is recycled with the CORRIDOR, and the way home does NOT follow you.** These are one
finding. The Wanderway's return station follows the player because it rides the tail of that
run's rolling tether: there, *following IS the trail cleanup* — the station is a readout of where
the recycled ribbon ends. The Arkway inherited the motion without the mechanism that gave it
meaning, and a way home that chases the ship you are escorting is a landmark that is never
anywhere. So the station is now planted at the ENTRANCE (`ArkwayRun.PlantEntrance`, 240 u down
the departure heading and 180 u abeam on the port side — clear of the Arkway toy's own ring and
off the line a pilot docked at the Ark's starboard flank flies) and stays there, marking exactly where `ReturnHome` puts you; and the cleanup moves to the thing
the Arkway actually recycles — **each struck traversal cell takes the ribbon laid up to the point
the Ark entered it** (`CellConveyor.CellRetired` → `ArkwayRun.OnCellRetired`). That is not a trail
cap and not decay: it is the rule `Cell.RequestCellSwap(clearLooseTrailMass: true)` already
applies to a swapped world, applied per cell, inside a voyage the player opted into, and unseen by
construction (a cell is struck only once its whole membrane is off screen, and the pilot is
leashed to the Ark, cells ahead). It is what lets a voyage run indefinitely. Two mechanics worth
carrying: a mark is the **head PRISM, not a count or index**, because `Trail.RemoveOldest` shifts
every survivor toward the head and any recorded number goes stale on the first roll; and the roll
is **budgeted per tick** (64), because `RemoveOldest` re-indexes the whole ribbon and an unbounded
drain is quadratic. Both ribbons roll (a double-trail vessel puts every other prism in
`SecondaryTrail`), only pooled prisms are recycled (`OnReturnToPool != null` — the Cell's own
loose-mass test), and each withers on the grow clock before it is handed back.

**Four exits, one path** (`ArkwayRun.End`): the DISEMBARK station standing at the entrance you
sailed from (a `WanderwayReturnToy`, neutral ring — coming home grants no domain);
another pass through the toy; leaving freestyle (the same `IsFreestyleActive` edge the
Wanderway watches); and the Ark falling — the food web eating its last hull prism — which
RESETS the toy. End reposes the player home FIRST, withers the Ark out (then destroy-drains
it like environment mass — hull prisms carry no pool-return handler), and QUEUES every
standing cell for the same off-screen-gated retirement the mid-voyage advance uses
(`StrikeSatelliteWorld` + the 150-per-frame drain, one cell at a time as it leaves view); the
next voyage's Begin force-strikes any remainder only behind its raised veil. Mid-voyage, the
cell two-behind retires only once its whole membrane sphere is outside the camera frustum —
the microscene conveyor's own removal gate, applied at voyage end too.

Like the Wanderway, starting a voyage hands the host cell its bare canvas
(`Cell.BareCanvasConfig` via `RequestCellSwap`); the FIRST cell and the Ark's hull build
behind one `EnvironmentLoadVeil` hold (`BeginArenaBuild` bracketed in a `finally`), and the
second and later cells stream in unveiled beside live play — which is what a satellite build is
for. Collider
budget: three cells at stride 4 ≈ ≤30k prisms, the Wanderway-stock envelope, against a
bare-canvas home world.

## The "one toy, then many" pattern (`MatrixToy`)

Three toys share one shape: **one station until you fly it, then many.** A pass unfolds a
MATRIX of choices out ahead; another pass folds it away. A toybox of a dozen permanently-visible
stations is clutter — a single toy that opens into its options reads as one thing you can pick up
and put down.

`MatrixToy` owns the whole of it: the toggle, the grid geometry, and the teardown. The matrix
blooms `matrixDistanceFactor` × `stationSpacing` from the toy along the **outward radial** (away
from the cell centre — the toy faces the centre, so outward is `-forward`), laid out as a
roughly-square grid in the toy's own right × up plane. You fly AT the toy and keep going: the
choices are ahead, never back through where you came from. Subclasses supply the item count, the
spacing knobs, and `BuildStation(index, parent, position, radius)`; `OnMatrixOpened` /
`OnMatrixClosed` hook whatever streaming the toy needs.

Stations may be light (`ToyMatrixStation` — a trigger with a short cooldown, via
`MatrixToy.CreateStation`) or a full `Toy` when the station needs its own bloom, exit-gated
re-arm, and `Update` (the painting stations are full toys for exactly that reason).

Anything a station starts that must **outlive the matrix** parents to `MatrixToy.ToyboxRoot`,
not to the grid — that is how a painting run survives folding the gallery away.

Users: **Cell Selector** (worlds), **Connect the Dots** (paintings), **Vessel Changer** (ships).

## Station icons: a choice shows itself (heading toward no text labels)

**Every selection station is a 3D icon of the thing it selects.** The text label is a crutch we
are actively removing: a station whose icon reads at a glance doesn't need a name floating over
it, and a matrix of named spheres is a menu, not a toy. Two icon strategies, both drawn from the
selection itself — never a decorative stand-in, and never a hand-authored symbol library:

| Strategy | Meaning | Used by |
|---|---|---|
| **Scaled-down view** | the whole thing, small | Vessel Changer (mini hulls), Lifeform bench (mini creatures + its hangar's mini hulls) |
| **Signature extract** | the few parts that identify it | Connect the Dots (under-budget icons), Cell Selector (signature structures) |
| **Growth simulation** | the rule it grows by, run in the abstract | Lifeform bench flora (no model exists to shrink) |

The split is a legibility call, not a taste one. A ship is one compact object and survives being
shrunk. A 55-stroke painting or a 34k-prism world does **not**: drawn whole at thumbnail size they
cross-hatch into a fuzzy ball that reads identically for every entry — which is exactly the
failure a text label then has to paper over. So those two take the few most identifying parts and
draw them boldly:

- **Connect the Dots** — `MiniaturePaintingBuilder` scales fidelity to the icon: the stroke budget
  is `radius × 1.1` (clamped 5–64), so a gallery station (radius 44) draws ~48 strokes — a Rose
  shows all four petal whorls — while an emblem satellite (7.5) draws 8. Line width comes down as
  density goes up so a full icon doesn't blob. When everything fits, everything is drawn; when it
  doesn't, strokes are chosen by **farthest-point dispersion** over their centroids, seeded with the
  longest stroke of each domain (colour identity). Dispersion is the load-bearing part: longest-first
  clusters, and on a radially symmetric painting it took several strokes from one side — the icon
  showed *half a rose*. The frame is fitted to the strokes actually drawn.
- **Cell Selector** — `CellMiniatureBuilder.KeepSignatureStructures` bins the generator's samples
  into a 12³ voxel grid and keeps the densest voxels until they hold `signatureCoverage` (0.7) of
  the mass. What survives is the motifs the generator actually builds, at their true relative
  positions, with the haze between them gone. Nothing is moved, invented, or re-coloured — it is
  still an honest scale model, just the recognisable part of one. Ties break on the voxel key, so
  a given environment always yields the same icon.

**Flora are the third case: they have no model to shrink.** A flora species *is* a growth rule — it
builds itself out of prisms at runtime — so there is nothing for `ToyModelBuilder` to harvest, which
is why those stations were anonymous spheres. `Flora.TryPreviewGrowth(budget, seed, into)` is the
answer: the species runs **its own growth rule in the abstract** and reports where prisms would land
— no prism, no spindle, no GameObject, no cell, no spatial-index reservation, no config mutation,
and never `UnityEngine.Random` (a preview must not advance a sequence the simulation draws from).
`FloraIconBuilder` then feeds those poses through the *existing* icon pipeline —
`CellMiniatureBuilder.BuildFromLays` → `ToyFactory.AddMiniatureBody` — so a flora icon is made of the
same stuff, in the same domain prism materials, as a mini-cell or a microscene.

| species family | preview | source of the rule |
|---|---|---|
| `BranchingFlora` (Branching, Cacti, Pine, Nerve) | the branch walk | its own serialized params — branch angles, counts, the `leafChance` climb, the 1/depth step and scale falloff |
| `AssembledFlora` + `GyroidAssembler` (Gyroid) | a patch of the real gyroid | `GyroidBondMateDataContainer`'s bond table, composed exactly as `CalculateGlobalBondSite` + `CalculateRotation` do |
| `AssembledFlora` + `WallAssembler` (Wall) | the square sheet | its four in-plane bond offsets |
| `AssembledFlora` + `SchwarzPAssembler` (SchwarzP) | a patch of the tunnel network | `SchwarzPAssembler.TryPreviewLattice` — the same seed anchor, tangent sites, Newton projection and parallel-transported heading as live growth, sharing the now-static `SiteDirection` / `TryStepAlongSurface`, with the occupancy claims swapped for a local visited set |

Every one of the four is the species' real rule, shared with live growth rather than re-derived —
the Schwarz P walk in particular reuses the assembler's own surface math, so an edit to the surface
moves the icon with it. The previews deliberately skip what a thumbnail cannot show: `growthChance` (it paces growth over
time, it does not change the shape a branch eventually takes), the Frenzy gate, and prism budgets.
They preview *form*; they are not a second implementation of growth for gameplay.

`ToyModelBuilder` is the shared scaled-down-view engine: it harvests meshes off a **prefab asset**
(never instantiated — no NetworkObject, no registry entry, no controllers), paints them with one
opaque self-lit preview material, and fits the result to the station radius. Callers pass a
`RendererFilter` for what to leave out — `VesselModelBuilder` is now exactly that filter (hull
only: no skimmer sphere, trails, jets, VFX), and the lifeform bench passes the equivalent for
creature bodies. **Any new toy that offers prefabs gets its icons from this, not from a sphere.**

State that used to be text is becoming shape too: the Cell Selector marks the world you are
already in with a **second, inner halo ring** hugging the model — inside the switch ring every
station wears, and counter-spinning so the two never read as one thick rim — instead of the word
`RESET` (an environment-free config has nothing to model, so its empty slot already reads as
"instant").

### Toy-root emblems (`ToyEmblem` + `ToyEmblemStreamer`)

The same rule, applied one level up: **a toy root is an icon of the toy**, not a tinted ball with
a name over it. The grammar is the third strategy —

> **core = what you are · orbiting satellites = what a pass would offer you**

— which is the `SwapToySetCoordinator` semantic ("you are this, these are the others") lifted onto
the roots that don't unfold. Every item is real content, built by the same builders the matrix
stations use.

| toy | core | satellites | orbit |
|---|---|---|---|
| **Vessel Changer** | the hull you're flying now | the next 3 you'd be offered | 10°/s |
| **Connect the Dots** | the on-ramp painting you've taken furthest | the other 3 on-ramp canvases | 6°/s |
| **Lifeform bench** | the 4 element crystals on a sub-ring | its 3 kingdoms — a real creature, plant and hull | 8°/s |
| **Wanderway** | a real microscene ("Gate Run") | 3 more recipes (Tunnel, Archway, Torus Knot) | **0 / 3 / 18** = off / dormant / flowing |
| **Cell Selector** | the world you're in right now | **none, structurally** | core spins at 8°/s |
| **Domain Changer** | *(no emblem — its switch ring IS its read; see below)* | | |

**Geometry** — one const block in `ToyEmblem`, all multiples of the toy's body radius `R` (22 in
Menu_Main): core `0.46R`, orbit `1.18R`, satellite `0.34R`, halo tilt 32°. Outer extent `1.52R`
= 33.4u, deliberately **inside** both the 42u trigger radius and the 41.8u label height — an
emblem never reads bigger than its own interaction volume. First satellite sits at 6 o'clock so
12 o'clock stays clear under the label.

**Motion is the second identity channel.** Silhouette carries the far read (~250u, "that's a
different toy"); real content carries the near read (~100u, "that's the hangar"); distinct orbit
rates carry both. The rates stay clear of the reserved body spins (cone 45, jack 22, ring 15).

**Nothing is built on the caller's frame.** `ToyboxController.PlaceToys` runs on the
`OnClientReady` frame of *every* entry to Menu_Main and is already the menu's most expensive. So
`Attach` creates **holders only** (~23 GameObjects toybox-wide; zero meshes, zero materials, zero
`Shader.Find`, zero `Instantiate`), and `ToyEmblemStreamer` fills one slot per frame. The pump
**yields before its first build** — an `async UniTaskVoid` body runs synchronously up to its first
suspension, so without that yield the first registered emblem's core would build on the stack of
whoever started the pump: the spawn frame on registration, and inside `ToyEmblem.Update` on a
live-key rebuild (where the Cell Selector's `heavy` mesh assembly would land on the frame right
after a cell swap). A source that ignores the shared material declares `UsesSharedMaterial =>
false`, so it never triggers the `Shader.Find` chain behind it — **round-robin,
breadth-first**, so every toy's core lands before any satellite. 18 items ≈ 0.3s, entirely inside
the toys' 1.2s bloom-in, so the emblem assembles *inside* the growth and nothing pops in. A slot
that reports itself `heavy` gets a clear frame after it.

> **Coupling with no compile-time guard:** that property depends on `Toy.bloomDuration` (1.2s)
> exceeding the stream (~0.3s). Drop it below ~0.5s and emblems visibly assemble.

**The Cell Selector's zero satellites are structural, not tuning.** A satellite would be another
world, and any world not already loaded costs a full ~34k-lay generation to picture — the exact
cost `CellTypeChoiceOptions.EnvironmentFree` exists to defer. Its core is free or it is nothing:
the matrix's cached miniature, else the live environment's `CachedLays` via the new
`CellMiniatureBuilder.BuildFromLays` (which **cannot reach `GetTrailData()`** — the restriction is
enforced by API, not by comment), else empty. At boot the cell is environment-free, so the emblem
is a small bare core: *you are in the empty one.* Zero cost on every entry to Menu_Main.

**Fail-soft.** `Attach` doesn't replace the factory's sphere — it **rescales it to core size** and
keeps it as a placeholder, fading it out when a real core lands. It is **withdrawn, never
destroyed**: a rebuild can legitimately produce nothing (pick a world, then pick the
environment-free one) and the fallback has to have something to bring back, or the toy becomes an
invisible trigger under a floating label. On an empty stream it returns — animated, since it is a
body already on screen — to the plain full-size body, *except* for a core-only source, which stays
small: that is the Cell Selector at boot, where "a small bare core" is the honest read. That is
also why `ToyFactory.CreateRoot` and all five `Spawn` overrides are untouched.

**Liveness** is a 0.5s poll of the source's own key/tint (there is no cell-config-changed event to
subscribe to, and the vessel source's existing mid-swap guard is exactly the "hold, don't rebuild"
signal we want). A changed key rebuilds every slot; a changed tint writes the emblem's **own** one
material — never `ToyFactory.AccentMaterial`'s shared per-colour cache and never a theme asset.

**The Domain Changer deliberately has no emblem** — it rebuilds its body on every flip, so an
emblem there would be re-emitted constantly for no legibility gain. It **does** wear a switch ring
now: its slots used to be cones you flew at, that cone is reserved for a booster, and what replaced
it is the ring itself carrying the meaning in its shader (see "The switch"). The old
`SwapToySetCoordinator.SlotsWearSwitchRing` exemption is deleted with it; what the coordinator
carries now is `SlotRingRadius`, the same neighbour clamp every matrix station uses.

**Labels stay for now**, hung clear above the switch ring (`ToyFactory.AddRingedLabel` — the font
is unchanged, only the height moved). They come off once the ring-distance legibility pass
confirms each toy is identifiable without them — a separate, gated change, and now a more likely
one, since the ring carries the far read the label used to.

### Layout tuning (matrix scale & distance)

Icons only pay off if they're big enough to read on approach, so the matrices were re-tuned:

| Toy | Station radius | Spacing | Distance factor |
|---|---|---|---|
| Cell Selector | 9 → **18** | 55 → **110** | 3 (distance 165 → **330**, since distance = spacing × factor) |
| Connect the Dots | body radius → **×2** (`iconScaleBodies`) | derived from radius, so ×2 | 3 → **4** |
| Lifeform bench | 6 → **12** | 45 → **90** | derived (×1.5 / ×3.5 / ×5.5 of spacing — one per hierarchy layer), so ×2 |
| Vessel Changer | **unchanged** | **unchanged** (60) | 3 → **6** |

Everything lands at roughly **2× size and 2× distance**. The Vessel Changer is the deliberate
exception: mini ships already read at their current size, so only the distance doubles — with the
spacing unchanged, the factor has to carry the approach on its own.

## The "swap set" pattern (domain)

The domain changer is a **set** of toys managed by the generic coordinator
`SwapToySetCoordinator<T>` — its universe is small enough (two toys in a 3-domain session) that
showing it laid out around you beats unfolding it. The set always shows *the options you are not
currently on* (`universe \ {current}`), each toy visually *being* the option it will switch you
to. Flying through a toy applies the change; the coordinator then **flips the used toy to the
option you just left**, so the set continuously mirrors "everything except where you are now".
Current state is polled each frame, so external changes (a panel, a menu reset) reconcile the
same way. `SwapToy` is the per-option toy; it just reports activation and lets the coordinator
own the option→visual mapping and the flip.

### Vessel Changer (`VesselChangerToy`)
**One toy that opens into the hangar.** Fly it and a matrix of **mini 3D ship models** blooms
out ahead — every vessel in the collection except the one you're flying. The model is built by
`VesselModelBuilder`, which reads mesh data straight off the ship **prefab asset** (never
instantiates it, so no NetworkObject/VesselStatus/controllers ever run). Fly a ship and you swap
into it via `MenuServerPlayerVesselInitializer.RequestSwap` (the existing networked pipeline);
the matrix closes behind you, since it was "everything except what you fly" and what you fly just
changed.

**Mini-model rendering.** `VesselModelBuilder` extracts only the **hull**: it skips builtin-
primitive meshes (the skimmer sphere, scaled 15–60× — it otherwise dominated `NormalizeToRadius`
and crushed the hull to an invisible speck; this is why only Rhino, the one ship whose skimmer
has no builtin sphere, used to render), anything named skimmer/trail/jet/forcefield/crackle/pip/
vfx, and inactive/disabled renderers (read via `activeSelf` up the chain — `activeInHierarchy` is
always false on a prefab asset). Vessels whose body isn't statically extractable fall back to the
labelled tinted sphere.

**A station shows the ACTUAL ship; a glyph stays flat.** The mini hulls used to be painted with
one opaque, self-lit preview fill in the local player's domain colour, because a vessel's real
materials are dark unlit theme shaders that read as a black blob with nothing to say which team
they belong to. The **vessel vision band** (`Docs/VESSEL_VISION.md`) answers both halves of that
now, so the fill is retired for anything you fly AT:

| | Built by | Materials | Domain read |
|---|---|---|---|
| **Station** (vessel-changer matrix, Lifeform Matrix hangar) | `ToyVesselRoster.TryBuildLiveHull` | the ship's **own** materials, with the domain-role slots swapped for the live domain ship material | the vision band's mark, stamped via `VesselVisionShading.StampDisplayModel` |
| **Glyph** (a toy's emblem, the kingdom icons) | `ToyVesselRoster.TryBuildHull` | one flat, self-lit preview fill | the fill's own colour |

The split is a distance argument, and the geometry already made it: a vessel matrix blooms
`StationSpacing × MatrixDistanceFactor` = **360 units** out along the outward radial, which lands
the whole grid just past the band's `nearFullStart` (350). So the stations **arrive already at full
mark**, read as domain-coloured cel silhouettes for the entire approach while you are choosing
between them, and **resolve into their real hulls over the last stretch as you commit to one** —
choosing at range, arriving at a ship. A glyph sits *on* the toy, inside the band's near cutoff
where the mark is correctly zero and where a real hull would be a black blob, so it keeps the fill.

**Two kinds of mini hull must be re-tinted in opposite ways, and one list holds both** (the
Lifeform Matrix's `_hullBodies` carries its kingdom glyph *and* its hangar stations). A flat model
owns a preview material built for it, so a domain change repaints that material. A **live** model
draws with shared **project assets** — repainting one would recolour every ship in the game,
permanently, in the editor. So live models carry a `ToyLiveHull` marker and everything routes
through `ToyVesselRoster.ApplyDomain`, which dispatches; `Recolor` is now documented as flat-only.
The marker is deliberate rather than a heuristic ("is this material a project asset?"), because
the cost of guessing wrong is corrupting shipped assets.

**Recolour on domain change.** The mini ships are tinted to your domain, but they are built once
when the matrix opens. So `VesselChangerToy.Update` watches the local player's domain and, on a
change, re-tints **every** open mini ship in place (no rebuild → instant, pop-free) so they all
follow the domain changer.

**Swap continuity.** A swap is seamless — the new vessel inherits the old one's:
- **Domain / colour** — the swap re-syncs `Player.Domain` from the authoritative `NetDomain`
  before it repaints (`ClientPlayerVesselInitializer.ReInitializePair`), so the new hull keeps the
  domain you chose instead of snapping back to the Jade menu default (which desynced the domain
  changer). Domain lives per-player in `NetDomain`, so it persists across every ship change.
- **Position + orientation** — via `newVessel.SetPose(snapshot)` (also seeds `accumulatedRotation`
  so the rotation doesn't snap back).
- **Speed** — `SwapVesselAsync` captures the outgoing `VesselStatus.Speed` before despawn and
  seeds it onto the new vessel (`IVessel.SetInitialSpeed` → `VesselTransformer.SetInitialSpeed`,
  routed through a ClientRpc like `SetPose` so a party client's own vessel inherits it too),
  avoiding the post-`ResetForPlay` dead stop.

**Lost-control fix:** the swap pipeline drops the new vessel into autopilot with input paused
(that's why the old toy left you unable to steer). `VesselChangerToy.RestoreControlAfterSwap`
waits for `IsSwapping` to clear, then re-hands freestyle control — mirroring
`MenuVesselSelectionPanelController.RestoreFreestyleAfterSwapAsync`.

**HUD after swap.** `VesselController.Initialize` creates every vessel's HUD **hidden**, and the
only menu code that shows the local HUD fires on *entering* freestyle — which a swap doesn't do.
So `ReInitializePair` re-raises `GameDataSO.OnPlayerPairInitialized` (as the initial-spawn path
does) and `MenuMiniGameHUD` re-shows the local HUD on that event while in freestyle (gated on
freestyle + local player). Covers both the toy swap and the vessel-selection panel swap.

Collection defaults to a curated set (Manta, Dolphin, Rhino, Squirrel, Serpent, Sparrow) rather
than all 11; override per-asset via `vesselCollection`. Layout knobs: `stationSpacing`,
`matrixDistanceFactor`.

### Domain Changer (`DomainChangerToySet`)
Two **switches** (in a 3-domain session), each a ring in the **prism material of the domain it
will switch you to**, labelled with the domain name — always the two colours you are *not*.
Threading one requests that domain via the server-authoritative
`Player.RequestSetDomain_ServerRpc` (**never** a client-local write — CLAUDE.md), and the switch
flips to the colour you just left (`Toy.SetSwitchSignal` repaints the live ring; the prism
materials are shared theme assets, so it swaps the reference and never mutates one).

**It used to be a cone you flew at**, apex pointing the way through, in that domain's prism
material. That shape is now **reserved for a booster** (prompter-directed): a cone big enough to
fly at is a chevron, and a chevron pointing the way you are going is the one thing a booster can
be. Losing it cost this toy its whole read — which is what made the switch vocabulary worth
having, because the meaning moved from the toy's **shape** to its **shader**, where every other
switch can carry one too. Inside the ring is a hub sphere (`HubBodyFraction`, ½ the body radius)
in the same prism material, so the switch reads as one object at range rather than as a thin hoop;
it is a sphere on purpose, because it must make no claim about direction.

The slots sit `anglePerToyDeg` (14°) apart on the toybox's placement circle, so their ring radius
is **clamped against that chord** exactly as a matrix station's is against its spacing
(`SwapToySetCoordinator.SlotRingRadius` → `ToyFactory.StationRingRadius`). On the menu membrane
(~984u) the clamp does nothing; on the toybox's no-membrane `fallbackRadius` (300u) it takes the
ring 42 → 32.9, and without it the two rings would overlap by 17.6u.

### Painting / Connect the Dots (`PaintingGalleryToy` + `PaintingToy` + `PaintingRunner`)

**One toy that opens into the whole gallery.** `PaintingGalleryToy` is a `MatrixToy`: fly it and
the collection unfolds out ahead, one `PaintingToy` station per painting, each a miniature of its
own canvas with its name and live progress. Fly it again and the gallery folds away. (Sixteen
permanently-visible stations fanned around the membrane was clutter — and the gallery's stroke
generation used to be paid at menu **boot**, which is exactly the cost this branch moves off the
boot path. The first open now pays it instead.)

**A run outlives the matrix.** `PaintingRunner` is parented to `MatrixToy.ToyboxRoot`, not to the
grid, so folding the gallery away mid-painting leaves the canvas untouched; `PaintingToy` keeps a
static id→runner map and **re-adopts** a live run when the matrix re-opens, so re-flying a
painting resumes it instead of starting a second run on the same canvas. Monument anchors come
from the same proximity-first sphere packing as before, computed once on the first open.


The painting toy (player-facing name **"Connect the Dots"**, formerly "Fly by Numbers") is a
**gallery**: `PaintingToyDefinitionSO` spawns one `PaintingToy` station per `PaintingDefinitionSO`,
fanned around its ring slot, each labelled with the painting's name and live progress. A painting
is **multi-stroke and multi-domain** — a list of `PaintingStroke`s (name, domain, ordered 3D
points) flown in author order — and it stands as a fixed, upright **monument-in-progress** anchored
just outside the toy ring (front facing the ring), not a billboard that follows the vessel. The
built-in gallery (`PaintingPresetLibrary`) is a 16-painting ladder: a four-station on-ramp, then a
dozen **grandiose non-planar constructions** that dwarf the Taj (every one is >20·W of flight, most
>100 strokes — the Taj is ~55 / 15·W):

| # | Painting | Size | Strokes | What it is |
|---|---|---|---|---|
| 1 | Star | 840 | 1 | the basic trace, big enough to feel real (2× — the warm-up should already feel grand) |
| 2 | Rainbow | 700 | 3 | the domain gates, one band per colour |
| 3 | Saturn | 800 | 3 | genuinely 3D flying (tilted rings) |
| 4 | **Taj Mahal** | 1100 | ~55 | plinth, chamfered body, iwan+niches, onion-dome rib cage, 4 chhatris, 4 minarets, pool + charbagh |
| 5 | Torus Knot | 1000 | ~19 | the exact (3,2) trefoil as a machine-clean TUBE: 6 rotation-minimizing longitudes (one barber-pole twist), 12 rings, spine |
| 6 | Buckyball | 1000 | ~62 | exact C60: **12 pentagons + 20 hexagons** (planar faces) + the 30 real 6:6 double bonds as inset dashes |
| 7 | Double Helix | 900 | ~88 | true B-DNA: pitch/diameter 1.7, 10 bp/turn, 144° grooves, ribboned backbones, purine+pyrimidine pairs, phosphate ticks |
| 8 | Nautilus | 900 | ~67 | the real shell model: embracing log-spiral whorls, 58 growth-line ribs, tiger striping, the open aperture |
| 9 | Lotus | 900 | ~76 | the FULL lotus: wide-open outer leaves (Jade) descending through five whorls (10+9+8+6+5) into a closed pure-petal Ruby corolla and Gold bud heart |
| 10 | Rose | 900 | ~67 | the ENCHANTED rose: a long stem owning two-thirds of the height, two leaflets, sepals curling under a compact wrapped bloom with a furled Gold heart |
| 11 | Spiral Galaxy | 1200 | ~187 | a TWO-arm grand design at 17° pitch, inclined 22°: dust lanes, old-gold bulge, star streaks flowing along the arms |
| 12 | Phoenix | 1400 | 260 | **baked from a real sculpture**: the *Striding Eagle* museum scan (threedscans / Saint Louis Art Museum, no restrictions) — 79.7·W of engraving contours + flame-chained feather feature lines |
| 13 | Almighty Mountain | 1500 | 111 | **baked from real terrain**: the Matterhorn's actual DEM (AWS Terrain Tiles) — elevation contours (Ruby rock / Jade snowline) + Gold ridge polylines |
| 14 | Starry Night | 1300 | 173 | **baked from the real painting** (v2 retrace): 11 star/moon ring clusters, the double-swirl as two long coherent Jade spirals, 6 Ruby cypress flames, streamlines bent onto an immersive curved canvas with luminance relief |
| 15 | Lion's Head | 1800 | 124 | **baked from a real sculpture**: the CC0 Temperance Union Lion scan (1896), Squirrel-scaled — engraving contours + 62 mane-curl feature lines (micro-curls under 28u turn radius filtered out) |
| 16 | Peacock | 1300 | 236 | **baked from a real scan**: YahooJAPAN's peafowl photogrammetry (CC-BY 4.0 — attribution ships in the asset description) — the fanned train, scalloped eye-feather rim, body and legs |

**Gallery stations are miniatures in a wall, not balls in a line.** Each station's body IS its
painting in miniature (`MiniaturePaintingBuilder`: 5 SIGNATURE strokes — see "Station icons" — domain-
tinted, on a slow turntable) — a sphere only as fallback for stroke-less paintings. The sixteen
stations arrange as a roughly-square matrix cluster at the toybox slot (columns along the ring
tangent, rows climbing the off-plane vertical), and the monuments anchor behind their column in
vertical tiers — a wall of masterpieces. Every toy label (stations, gates, all toys) wears
`BillboardLabel`, facing the camera each frame so text reads from any approach.

Rows 5–11 are built by composition from **`PaintingStrokeToolkit`** (below); rows 12–16 are
**baked from real references** by the offline **painting pipeline** (`Tools/PaintingPipeline/` —
mesh→engraving-stroke converter + painting-flow tracer + asset baker; licences audited in
`Tools/PaintingPipeline/REFERENCE_MODELS.md`). Baked paintings live as authored `strokes` on the
`PaintingDefinitionSO` asset — the SO's highest-priority source — so they need zero runtime code
and their `preset` remains as fallback (fallback catalog descriptions deliberately do NOT claim
reference provenance — the CC-BY/DEM attributions belong to the baked strokes only). Monument
anchors come from **proximity-first sphere packing** (`PackMonumentAnchors`, locked by
`PaintingToyLayoutTests`): each painting occupies its bounding sphere + half `paintingClearance`;
anchors are chosen from deterministic Fibonacci shells around the slot, nearest valid spot first.
Pack order is hybrid — the four on-ramp entries first in ladder order (they sit right at the
stations, Star ≈ 600u), then largest-first so the giants sit at their physical floor (the
Matterhorn ≈ 2.4km — membrane + its own ~1.5km bounding radius is the floor; a flat wall layout
had exiled it ~6.5km). No two monuments interpenetrate, nothing pokes through the membrane
(studio zones may still overlap; `BenchOtherRunners` arbitrates the brush).

How a run plays:

- **Ghost blueprint.** Every stroke renders as a `LineRenderer` ghost tinted its domain colour —
  pending faint, the current stroke bright, completed strokes dim-solid. The whole blueprint
  blooms in (continuity law) and fades away after completion, leaving only the painted prisms.
- **Start gates.** Each stroke opens with a ring gate at its first point (a `SwapToy`, so it
  inherits bloom/local-user/freestyle gating/re-arm), labelled `n/total StrokeName` and tinted
  the stroke's domain. Flying through it **requests that domain via
  `Player.RequestSetDomain_ServerRpc`** (never a client write; silently skipped if the session's
  `RequestedDomainCount` excludes it) so the trail recolours, then the stroke begins. This is
  the domain-changer composed into the painting — colour changes are part of the flying.
- **Pen-up between strokes.** Inside the painting's "studio zone" (bounding sphere + margin),
  the trail spawner is paused between strokes via `VesselPrismController.SetSpawnerPaused`
  (pen-up), so transit flight never scribbles across the artwork; painting a stroke, leaving
  the zone, exiting freestyle, benching, or destroying the runner ALWAYS restores it. Pausing
  the spawner is the sanctioned mass-law lever ("not creating mass is allowed; aging it out is
  not") — the painted trail itself is conserved mass, no caps/TTLs.
- **Checkpoint riding (not vertex-chasing).** A stroke is ridden through SPARSE checkpoints
  (`PaintingStrokeToolkit.RideCheckpoints`): spaced by arc (≥ max(90u, 8.5% of the painting's
  bounds diagonal)), never parked on tight curvature (>28° local turn — a hairpin apex is a
  punishing target at speed; on an all-tight stretch the flattest vertex is used so progress
  can't stall), with the stroke start (gate) and end (jack) always included. **Closed loops
  always keep a mid checkpoint near the half-arc** — otherwise a ring's end milestone sits at
  its own gate and the stroke would complete unridden the moment the gate fires.
- **Rings, not lines, not cones.** While AWAITING its gate the next stroke's ghost shows faintly
  (something to aim at); once you are RIDING it the line eases away entirely (continuity law —
  `_lineFade`, and it eases back in as the dim "done" memory line on completion) — the ride is
  the milestone RINGS and your own painted trail. Rings are **low-poly flat-shaded tori**
  (12×6 crystal facets in the domain prism material, slowly spinning so the facets glint —
  `ToyFactory.AddRingBody`, same shape family as the cone/jack), not line renderings.
  Each milestone is a ring gate faced along the
  local flight tangent whose **SphereCollider trigger is scaled to the ring radius** (flying
  through the ring IS the hit test; a slightly tighter distance check backstops fast physics
  misses, and all effects run on the Update tick, never in the physics callback — the trigger
  resolves the local vessel via the shared `Toy.TryGetLocalVessel`, which also guards the
  null-`Player` window during a mid-stroke vessel swap). The ride ring **never outlives
  engagement**: leaving the studio zone or exiting freestyle folds it away (and it re-blooms on
  re-engage) so the lava-lamp autopilot can never drift through it and latch a checkpoint nobody
  rode. The trail-on **cone** appears only on the stroke's START gate; the final milestone ring
  carries the trail-off **jack** in its centre. Rings fold away as they're swept and the next
  blooms in (continuity law). Wayfinding uses the game's **standard `ObjectiveIndicator`** (the
  edge-of-screen arrow every mode shares, not a bespoke guide line): the runner implements
  `IObjectiveProvider` via a persistent objective anchor (the gate while awaiting it, the current
  ride ring while painting — the anchor outlives the ring folding on disengage), and ONE shared
  arrow is lazily created at the HUD **Canvas root** (the indicator stretches to its parent and
  clamps to that rect's edges — a mid-hierarchy container is not a full-screen rect and pins the
  arrow in a corner), routed through `PaintingObjectiveRelay`
  to whichever runner holds the brush. The arrow hides itself while the target is on screen, so
  the world rings stay the primary guidance. (The old guide line's perfect-ride glow retired with
  it; re-express that juice in-world — e.g. ring emission — in the tuning pass.)
- **Progress, pause, resume.** Progress is stroke-granular. Re-flying the station benches /
  resumes the run ("put the brush down"); progress also persists across sessions
  (`PaintingProgressStore`, the FavoriteSystem `DataAccessor` JSON pattern — completed strokes
  re-render as dim ghosts after a restart since prisms live only as long as the scene). After
  the completion celebration, flying the station again clears the canvas for a repaint.
- **Toy-faithful.** No score, no timer, no fail state — only progress. Solo-painting only: the
  runner tracks the *local* player; party members see the painted prisms replicate but not
  your gates/ghosts (same local-station model as every toy).

(`ShapeDrawingManager` — the scored preview/cinematic/reveal minigame — was **deleted
2026-08-25**, C15. Any existing `ShapeDefinition` can still become a painting via
`PaintingDefinitionSO.sourceShape`, which splits pen-up gaps into strokes.)

#### Shape language — one vocabulary of interactables

Toys teach each other by recycling shapes (mindshare recycling): every interactable that does
the same *kind* of thing wears the same form, in the domain's **prism material** (the exact
shader the painted trail wears — `ToyFactory.DomainPrismMaterial` →
`ThemeManagerDataContainerSO.GetTeamBlockMaterial`).

| Shape | Meaning | Where |
|---|---|---|
| **Ring — the SWITCH** | *thread it and something fires* | **every toy root, every matrix station and every Domain Changer slot**, stroke start gates, stroke milestones, the SHARE/REPAINT completion gates, the Wanderway return station — and, outside the toybox, the Scarab's placed switches and Astro League's goals |
| **Cone** (apex = "this way next") | *turns / keeps your trail ON* — **at hub scale only** | stroke-gate hubs. As a BODY (one you fly at rather than one inside a ring) it is **reserved for a booster** |
| **Jack** (three rods through a centre) | *turns your trail OFF* | each stroke's final point (reaching it ends the stroke and pens up) |
| **Emblem** (tilted ring of discrete objects around a hub) | *this is what I am, and what I'd offer* | the toy roots — see "Toy-root emblems" |

Builders live in `ToyFactory` (`AddSwitchRing`, `AddConeBody`, `AddJackBody`; `AddRingBody` is
the raw torus underneath).

**The cone is spoken for.** It used to be shared by the painting's stroke gates and the Domain
Changer's bodies, deliberately, so meeting either first taught the other. The Domain Changer is a
switch now and the body-scale cone is held for a **booster** — a chevron pointing the way you are
going is the one thing a booster can be, and a shape reserved *after* something else is using it
is not reserved. What survives is the hub-scale cone at a stroke gate's centre, which is a marker
inside a switch rather than the interactable itself.

**Ring vs. emblem, the disambiguation rule:** *one continuous ring square across your flight
path is a switch — thread it and something fires. A tilted ring of separate objects orbiting a
hub is an emblem — it is a label, not an interactable.* Emblems are therefore built from models,
never from `AddRingBody`, and they are tilted 32° so they never present as a switch. An emblem
adds **no collider**: the toy's own trigger sphere remains the entire interaction surface.

#### The switch — a ring is how anything is activated

> **A switch is a ring you thread, and threading it activates something.** It is the one word the
> platform has for "this does something when you go through it", and it is deliberately
> *threader-agnostic*: a **vessel** threads a toy, a **ball** threads a Scarab switch or an Astro
> League goal. Same shape, same promise.

Before this pass the toybox taught it inconsistently — the painting's stroke gates and milestones
were rings, but a toy root was a tinted sphere with a name floating over it and an invisible 42-unit
trigger, so "how do I use this thing?" was answered by flying at it and finding out. Now **every
toy is drawn inside its ring**, and one rule makes it teachable rather than decorative:

> **The ring IS the trigger volume, drawn at its own radius.**

so a ring can never advertise a volume the collider does not have, and *fly through the ring* is a
promise the code keeps. `PaintingRunner`'s milestones already worked this way (`trigger.radius =
ringR; // the hit volume IS the ring`); this generalises it.

**It is drawn by the base, not by each builder.** `Toy.Initialize` reads its own `SphereCollider` —
the same collider `LocalVesselOutsideTrigger` measures for the exit gate — and draws the ring from
it, so a toy authored tomorrow wears one without anybody remembering to add it (the same reason the
bloom-in and the exit-gated re-arm live there). The ring is a child of the toy root, so it blooms in
with the toy; it carries **no collider of its own** and costs one shared static mesh, one renderer
and one `ToyIdleSpin` per station. Light `ToyMatrixStation`s (which are not `Toy`s) get theirs from
`MatrixToy.CreateStation` / `LifeformMatrixToy.CreateStation`.

**Exactly one opt-out** now, explicit, `Toy.ConfigureSwitchRing(radius)` *before* `Initialize`:

| Opt-out | Who uses it | Why |
|---|---|---|
| **A smaller radius** | painting gallery stations, every `MatrixToy` station via `ToyFactory.StationRingRadius`, and the Domain Changer's slots via `SwapToySetCoordinator.SlotRingRadius` | A station's trigger can legitimately overrun half the gap to its neighbour; rings that interpenetrate read as chain-link, not as a row of switches. Clamped to `MaxRingSpacingFraction` (0.45) of the spacing. A ring **smaller** than its trigger still always fires when threaded, so the promise above survives the clamp — only "the trigger is no bigger than the ring" is given up. |

*(The second opt-out — radius 0, waiving the ring entirely — existed for the Domain Changer alone
and is gone: that toy is a switch now.)*

#### What a switch's SHADER says

> **Every switch is drawn in the PRISM shader**, the same material family the painted trail wears
> — so a switch is made of the same stuff as the world it acts on. The one channel left free to
> carry meaning is *which prism it is painted as*, and that channel is `ToySwitchSignal`.

| Signal | Painted as | Says | Wearers |
|---|---|---|---|
| `Neutral` | `Domains.Blue`'s plain prism material — the platform's existing "no team / neutral entity" sentinel | *thread me and something happens* | every toy root, every matrix station, the painting's milestones and its SHARE/REPAINT gates, the Wanderway return station |
| `Domain` | that domain's plain prism material | *threading me makes your trail this domain* | the Domain Changer's slots; the painting's **stroke-start gates** (crossing one calls `RequestStrokeDomain`, so it really does hand you one) |

**The reservation:** *a switch wearing a playable domain's colour is one that hands you that
domain.* Nothing else in the toybox may wear one. Half of that is structural and needs no
discipline — `ToyFactory.SwitchDomain` forces a `Neutral` switch to Blue **whatever domain a
caller passes**, and `AddSwitchRing` takes no raw `Color` or `Material` at all, so the signal is
the only door and a neutral switch cannot be painted a playable domain even by mistake. The other
half is a call-site fact (*who may ask for `Domain`*), which lives in source where no compiler
sees it, so `ToySwitchVocabularyTests` reads the source and holds an allow-list — in both
directions, so a row that stops being used has to be deleted rather than left describing the
reservation while hiding it.

**The encyclopedia's tool portraits are deliberately NOT repainted.** `ToolPortraitBuilder` draws
a tool's emblem — core, satellites and switch ring — from one material in that entry's accent
colour, because a codex portrait is a monochrome ICON identifying an entry in a gallery, not a
render of the object. A blue hoop on all six would trade the gallery's per-tool identity for a
consistency nobody is looking at from inside the codex. If that ever stops being the call, the
change is a second material in `ToolPortraitBuilder.Build`, not a change here.

**Neutral is Blue, not "untinted".** Every switch had been wearing its own toy's accent, which is
how the Vessel Changer's stations came to be gold and the Lifeform bench's pale green — colours a
player has every reason to read as Gold and Jade. Painting the rest Blue is not a loss of identity
(that lives on each toy's label, hub, emblem and content) but the thing that makes the domain
colours mean something when they do appear.

**The one wearer outside the toybox** is the Scarab's placed switch, where the domain colour names
the domain the switch *belongs* to rather than one it grants (`SCARAB.md` §5 — whose colour it is
decides who it pays). Nothing in that mode changes a pilot's domain, so the two readings never
share a screen; it is listed in the test's allow-list with that reason. Do not add a third toybox
wearer without settling which reading wins. It draws in the **live** per-domain prism material —
the same asset the dais prisms it pays out are laid in, so the two cannot drift — reached by
injecting `GameDataSO` into `PlaceSwitchActionExecutor` (the vessel is DI-injected on spawn, the
same door `ScarabCavitationBlast` on that hull already comes through). That let it drop a
duplicated per-domain palette of its own.

**The fallback, and the trap it exists to survive.** When the per-domain sets are not built yet
(the toybox before `ThemeManager.Awake`), `ToyFactory` mints a prism-shader material instead —
preferring a **clone of the base set's own `BlockMaterial`**, and only synthesising one via
`Shader.Find("Shader Graphs/BlockGraph")` when even that is unavailable. Cloning is not
belt-and-braces: **a Shader Graph property's authored default is not the value the shipped
material carries**, and on `BlockGraph` that gap is fatal — `_Alpha` defaults to **0** while
`PrismMaterial.mat` sets **1** with `_AlphaClip`/`_ALPHATEST_ON` on, so a bare
`new Material(Shader.Find(...))` is a correctly-tinted prism that alpha-clips to nothing. A clone
carries every render-state property *and* the shader keywords; the synthesised path has to restate
them and can only restate the ones we know about. (`AstroLeagueBall` mints a `BlockGraph` material
the same way and does not set `_Alpha` — flagged in BACKLOG, not touched here.)

**Measured geometry.** The law is enforced in code; its LAYOUT consequences are not, because they
move in *data* — a station radius, a matrix spacing, `toyBodyRadius`/`toyTriggerRadius` — where no
compiler sees them. **`Tools/Build/toy_switch_ring_geometry.py`** (`--check` to gate) re-derives
them from the shipped constants (`ToyFactory`, `ToyEmblem`, `ToyboxController`) and the shipped
authored assets (`_SO_Assets/Toys/Toy_*.asset`), and asserts three things per site: the ring
**encloses its own content**, it **clears its own label**, and it **clears its neighbours**. Today:

| Site | ring R | ring inner | neighbour gap | emblem/content | label |
|---|---|---|---|---|---|
| toy root (×5) | 42.0 | 38.6 | — (angularly spaced) | emblem outer 33.4 → **5.2 clear** | 72.0, block bottom 46.9 > outer 45.4 ✓ |
| Domain Changer slot | 32.9 *(clamped)* | 30.3 | 2.0 ✓ | hub 11 | 62.2 ✓ |
| Cell Selector station | 28.8 | 26.5 | 47.8 ✓ | model 18, inner halo 22.5 | 52.9 ✓ |
| Vessel Changer station | 27.0 *(clamped)* | 24.8 | 1.7 ✓ | ship 22 | 55.8 ✓ |
| Lifeform kingdom station | 28.8 | 26.5 | 27.8 ✓ | kingdom sample 18 | 52.9 ✓ |
| Lifeform species station | 19.2 | 17.7 | 48.5 ✓ | creature 12 | 35.3 ✓ |
| Lifeform hangar station | 19.2 | 17.7 | 48.5 ✓ | hull 12 | 35.3 ✓ |
| Lifeform variant station | 19.2 | 17.7 | 48.5 ✓ | crystal 12 (drawn 0.35–1.53× the default heart) | 35.3 ✓ |
| Painting gallery station | 63.4 *(clamped)* | 58.3 | 3.9 ✓ | miniature 44 | 121.7 ✓ |

The Wanderway return station is not in the table because it is sized off its own
`returnStationRadius` (22 authored → ring 48.4) rather than the toybox's placement, and it has no
neighbours. The tightest margins — Vessel Changer 1.7, Domain Changer 2.0 and gallery 3.9 — come
from spacings that were already tight before rings existed, and are exactly what the clamp exists
to hold; at 0.60 both of the first two interpenetrate (the script's own negative control).

The Domain Changer row is modelled on the toybox's **no-membrane `fallbackRadius` (300u)**, the
tightest circle it can place on: its slots are 14° apart, so their spacing is a *chord* and shrinks
with the placement radius. On the menu membrane (~984u) the chord is 239.8 and the clamp does
nothing; at 300 it is 73.1, and an unclamped 42 ring would overlap its neighbour by 17.6. Its
`content` (11) and its `label` basis (22) differ because the two are different questions — the
ring must enclose the **hub** drawn inside it, while the label is sized for the distance the
**station** is read from, and this is the only site where those disagree. *(A third used to sit
between them: the level-5 lifeform variant station at 2.5, whose radius was
`StationRadius × (1 + 0.35 × (L − 1))`. With levels retired every variant station is the plain
`StationRadius`, so that row is now identical to the species row and its clamp is no longer
exercised. `Tools/Build/toy_switch_ring_geometry.py` has dropped `LIFEFORM_LEVEL5_FACTOR` and
prints the row above.)* Run the
script rather than nudging the constant.

**The Wanderway return station is the one ring with no fixed axis.** Every other toy faces the cell
centre, which *is* the axis you approach it on. The return station rides the tether's tail and you
come back at it from wherever you wandered, so it **turns to face the vessel** on the same easing it
already used to follow the tail — a portal you can only ever see edge-on teaches nothing. (Roll is
free here: a torus is symmetric about its own axis.)

#### Stroke order — flight continuity first, computed at runtime

Authored stroke order is no longer flown verbatim: `PaintingDefinitionSO.EnsureStrokes` passes
every source (authored, converted, preset) through
**`PaintingStrokeToolkit.OrderForFlightContinuity`** — a greedy nearest-next-start tour, so
**each stroke begins near where the previous one ended** (prompter-directed: flight continuity
takes precedence in the sort). Within a near-tie band (4% of the painting diagonal) the LEAST
curvy candidate flies first, so fine detail still lands later and the difficulty ramp survives
as a tiebreak. The order stays **domain-contiguous** (grouped by domain, each group entered at
its most continuous stroke) so the trail recolours at most once per domain — on the shipped
gallery this cut stroke-to-stroke transit 27–70% (Phoenix 138k→42k units) and collapsed
mid-painting recolours (Rose 51→2). Stroke 0 keeps its place (the authored opening + its gate);
the pass is deterministic, so progress-store stroke indices stay stable across sessions.
Authors therefore only choose the opening stroke and the stroke *content* — sequencing is the
runtime's job.

#### The stroke toolkit — where sophisticated strokes come from

The grandiose constructions (rows 5–16 above) are not hand-authored point lists — they are
composed from **`PaintingStrokeToolkit`** (`Assets/_Scripts/Controller/Toys/`), a pure, deterministic,
unit-tested geometry library. It answers "where do we pull more sophisticated strokes from?" with
*math*, not assets:

- **Deterministic PRNG** (`Rng`) — a seedable xorshift, never `UnityEngine.Random`, so every painting
  regenerates identically (and is testable). Same painting id → same monument every time.
- **Parametric curve families** — `CatmullRom`, `TorusKnot(p,q)`, `FibonacciSphere`, plus the
  `TruncatedIcosahedron` / `SoccerBallFaces` graph (exact 60-vertex/90-edge/32-face buckyball).
  Botanical/terrain helpers: `MidpointRidge` (fractal mountains), `ReflectY` (lake reflections),
  `FirTree`, `FeatherStroke`, `RadialCurlStroke` (mane/flame strands). Only primitives with a
  production caller live in the toolkit — the speculative API (helices, phyllotaxis, rose
  curves…) was pruned in the pre-PR review; compose new primitives when a generator needs them.
- **The impressionist field** — `CurlNoise` is a divergence-free 3D flow field (curl of a value-noise
  vector potential; `∇·(∇×Ψ)=0`, so streamlines fill space without converging to a point).
  `ImpressionistStrokes` integrates short strokes along it whose radii of curvature stochastically
  fill a region in every direction. **Scope note (quality direction):** random curl fill reads as
  scribble on *objects*, so the reference-grade rebuilds (rows 5–11) use **structured**
  surface-following strokes instead — growth lines, veins, bonds, orbital streaks. The curl field
  remains the right tool for genuinely turbulent subjects (the Van Gogh sky, flame, mist) and for
  the mane/feather sprays of the representational five, pending their real-model rebuild.
- **Structure kit (reference-grade rebuilds)** — `MinSegFilter` (dense parametric sampling never
  emits a degenerate segment), `TransportFrames` (rotation-minimizing frames — tube longitudes that
  never flip on curves like torus knots), `TubeLongitudes` / `TubeRing` (engineered tube rendering),
  `SoccerBallDoubleBonds` (C60's 30 hexagon–hexagon 6:6 bonds).

**Invariants every generator upholds** (locked by `Generate` + tests): base plane at y=0
(`RebaseToGround` runs after every grandiose preset), front toward +Z, only Jade/Ruby/Gold, flyable
segments (no degenerate or unflyable jumps), genuine non-planarity, and >20·W of flight. Multi-domain
impressionist fills are **batched by domain** so the trail recolours ≤2× per fill rather than at every
scattered stroke. Adding a construction = adding a `PaintingPreset` enum value + a generator that
composes the toolkit; nothing else changes.

**Collider budget / perf.** A painting is drawn by the *vessel's own trail* (conserved mass, no
caps) — the geometry cost is one `LineRenderer` ghost + one start gate per stroke, created at
`PaintingRunner.Begin`. The largest pieces (Phoenix 260, Peacock 236) therefore stand up a few hundred
lightweight LineRenderers; that is the deliberate "hours of flying" ceiling and is tracked in
`BACKLOG.md` for an in-editor perf pass.

#### Drawing state — the painting survives everything

Progress is not just a stroke counter: while a stroke is painted, every prism laid inside the
studio zone is recorded (painting-local **position, orientation, size, domain**, prism type)
via `VesselPrismController.OnBlockSpawned` and committed per completed stroke to
`PaintingPrismStore` (one `DataAccessor` JSON file per painting). That makes the run robust to
everything between strokes: **swap vessels** (capture re-resolves the controller), **switch to
another painting** (runs bench each other), or **leave for a whole game mode / quit** — on
return, the completed strokes' prisms are *regrown* through the normal `PrismFactory` channel
(pooled spawn, grow-in animation, streamed over frames so a monument reads as growing back,
never popping). Restored prisms are ordinary conserved mass. Abandoned mid-stroke prisms are
deliberately not persisted — an unfinished stroke re-flies fresh.

#### Sharing — the masterpiece leaves the game

Finishing a painting offers two fly-through choice gates at the station: **REPAINT** (clears
progress + drawing state, fresh canvas) and **SHARE** — `PaintingShareExporter` writes a
single self-contained HTML file (inline WebGL, zero external dependencies) that reconstructs
the painting from its saved prisms with drag/pinch orbit, zoom, and a gentle auto-spin, then
hands it to the platform share sheet via the NativeShare plugin. Paintings finished before
drawing-state capture existed fall back to boxes laid along the stroke polylines, so share
always works.

### Wanderway / Microscene Conveyor (`ConveyorToy` + `MicrosceneConveyor` + `Microscene`)

Fly through and you **leave for a wander** (`WanderwayRun` — see *The run* below): the cell reverts
to its bare canvas, the belt builds its **entire conserved stock behind a load veil** (see *Scale*),
and a field of **microscenes** stands ahead of your flight path, scene after scene — open-world
exploring crossed with an infinite runner.

The toy's **emblem carries the live state in its orbit speed**, and uniquely it tells the truth
about all *three* states rather than the two a label can hold: stopped, **flowing** (spun up, you
are wandering), and **dormant** — a belt that is still running while you are out of freestyle
orbits at a crawl rather than lying about being off. The label flips alongside it to say which way
the next pass will toggle the toy.

**48 recipes** built from a shared geometry vocabulary (`PrismGeometry`), in two families.

**The classic forty** (`MicroscenePatterns`): gate runs, helix weaves,
tunnels, slaloms, starbursts, orchards, meadows, menageries, polygon gates, serpent ribbons,
colonnades, orbitals, canyons, lattices, comet tails, spiral ramps, archways, vortices (converging
lines with an open convergence + an inviting crystal), slot corridors (parallel plates with gaps to
roll through), cube fields, torus gates, pillar halls, turbines, asteroid fields, living
plains / groves / aviaries / preserves, and a batch built on **superstructure-oriented
surfaces** — shingled domes and grotto vaults, torus-knot chases, Möbius rails, petal rosettes,
rifled terrace spirals, banked ribbon chicanes (parallel-transport-swept plate decks that roll into
turns), split tubes (facing curved shell walls), and four **Medley** slots that compose a spine
(straight / arc / S-curve / helix drift) with alternating motifs (hoops, polygon gates, torus
rings, shell dishes, blade crosses, clusters) — a combinatorial space no fixed recipe list could
enumerate. Each recipe re-rolls its own radii/counts/twists/bends on every arrival, so the same
recipe never lands the same way twice.

**The grand eight** (`MicroscenePatternsGrand`) — monument-scale set pieces for a belt whose
per-scene budget is measured in thousands: a **Cathedral** (nave of piers, ribbed vault, clerestory,
flying buttresses, rose window), a **World Tree** (braided trunk, curl-noise boughs, phyllotaxis
canopy, root buttresses), an **Orrery** (nested tilted torus rings each carrying a body, around a
core), a **Sunken City** (terraced ziggurats on a plaza, causeways slung between rooftops, a spire),
a **Leviathan** (serpentine spine, ribs, dorsal fins, a jaw-arch at the head), a **Geode Vault**
(shingled shell with a mouth cut through it and the interior bristling inward), an **Aurora Veil**
(layered curl-noise ribbon curtains to weave), and a **Hypersphere** (nested geodesic shells with a
bore drilled clean through). They borrow the construction idioms of the authored cell environments
(`SpawnableYggdra`, `SpawnableOrrery`, `SpawnableAtlantis`, `SpawnableGeode`, `SpawnableZephyr`) —
the freestyle six are the proof that a 30k-prism world reads as a *place*, and the conveyor now
transports one.

**Why two families, and how they scale.** The classic forty are hand-tuned in ABSOLUTE world units
around `MicroscenePatterns.DesignRadius` (80) and derive their part counts by *dividing* the budget
(a gate run is always 3–6 gates however much mass it is handed) — so at grand budgets they get
denser, never bigger: solid rings inside a mostly-empty envelope. They are therefore generated at
their design radius and scaled **bodily** to the live scene (`ScaleToScene`), POSITIONS only — never
prism scales, so a grand scene reads as *more architecture at the same grain*, and per-prism volume
(which feeds the host cell's phase ladder) does not inflate just because the belt got bigger. The
grand eight instead take the scene radius as their own basis and *multiply* their part counts with
the budget: more mass buys more bays, more branches, more shells. They join the shuffle bag only at
`prismBudgetPerScene ≥ MicroscenePatterns.GrandBudgetThreshold` (400), weighted ×3 so a grand ride
lands a landmark roughly every third scene while the classic forty carry the variety between them.
Edit-mode tests lock both properties: every recipe emits exactly the budget, stays inside the
advertised scene envelope, and — for the grand family — fills its budget with *architecture* rather
than letting `FitToBudget` pad it with ambient scatter.

The belt **follows you anywhere at any speed**: effective spacing =
`max(sceneSpacing, speed × minSceneIntervalSeconds)` and lookahead = `aheadTargetScenes × spacing`,
so there is always a field of structures ahead.

**Scale — 30,000 conserved prisms, built once behind a veil.** The belt's whole stock is
`poolSize × prismBudgetPerScene` (**20 × 1500 = 30,000** at the authored defaults — the same order as
an authored cell environment, which is the proven envelope for the instanced render path + collider
LOD). It is built **up front**, on the first pass through the toy, behind the same
`EnvironmentLoadVeil` the Cell Selector raises for a world swap: `MicrosceneConveyor.PrimeAsync`
brackets `PrismTrailBuilder.BeginArenaBuild`/`EndArenaBuild`, raises the veil, and lays all
`poolSize` scenes concurrently through `PrismTrailBuilder.LayBudgetedAsync` — the time-budgeted,
multithreaded-clone lay the cell environments use. The gate raises the lay slice ~10× while the veil
holds and releases only when every prism is laid, created AND grown, so the ride opens on a world
that is simply *there*. **After the prime the belt never instantiates again**: every arrival is
transport of mass that already exists. (The predecessor created one scene per belt tick, which at
grand scale would drip structures into view for the first minute of the ride and instantiate under
live gameplay — the exact failure the cell environments already learned.)

**Geometry vs. theming (why it stays fresh, not chaotic).** A recipe produces pure *shape* plus
**structural metadata** — `MicroscenePlan.CloseStructure()` after each gate/strand/tree/wall stamps
every point with its substructure id + t-along-path — and `MicroscenePainter` then paints each
scene from a config-authored `MicroscenePalette` (`ConveyorToyDefinitionSO`). Painting keys off the
structure, never bare indices: **domain schemes** (mono / per-structure rainbow runs /
gradient-along-flight / accented / radial pinwheel / candy-stripe / port-starboard mirror /
neutral-veined-with-Blue) always draw from the **full playable triad** — belt prisms are
environment mass, not player property, so scenes are never limited to the session's domains;
**kind schemes** use danger/shield as palette tools (all-plain / danger sprinkle / one whole
**danger structure** — a gate of fire to thread or deliberately skim for the Squirrel danger
boost / **danger tips** on arm-and-blade ends / shielded sprinkle / **shielded ribs** armouring
one frame / a **supershielded keystone** guarding the crystal — shield counts capped for the
collider budget); **scale moods** reshape whole scenes (uniform grand/delicate × long-axis stretch
for wiry-vs-chunky × per-structure taper riding the structure-t — with every scale family also
jittering each axis independently); and a **crystal mix** (mostly elemental skims, occasional
**omni** jackpots — body-collected fuel + speed buff). "Infinitely fresh" is the cross-product of
recipe × domain-scheme × kind-scheme × scale-moods × per-arrival geometry roll; coherence comes
from painting per *structure*, not per prism. Prism lay-down goes through the shared
`PrismTrailBuilder` (the one canonical Instantiate→…→Initialize primitive, also used by the
Spawnable environment system). See `Docs/EnvironmentSpawning/UNIFICATION_ASSESSMENT.md`.

**Placement is a connected ribbon that can break and re-lay.** Every scene sits *on* the flight
line — never scattered laterally (no orthogonal "sphere in front of you"). Each tick the belt scans
a **forward cone** (half-angle `turnBreakDegrees`, default 55°) around the live course and does one
of two things:

- **Near-fill** — if nothing lies just ahead on the current heading (start-up, or a turn just
  ejected the near scenes out of the cone), it drops a scene directly ahead at `firstSceneDistance`,
  so a structure appears in front of you right after any turn.
- **Extend** — otherwise it chains the next scene off the *actual frontmost scene* along the current
  course (`tip + course × spacing`). Chaining off **real mass** (not a free-floating "head") keeps the
  ribbon connected and lets it **bend** with gentle/moderate turns without ever drifting into a
  parallel path far to the side — the drift that made the old head-chained belt lay a distant
  shadow ribbon on any deviation.

A **sharp turn** drops the whole old ribbon out of the cone: its measured reach collapses, near-fill
re-lays straight down the **new** heading from `firstSceneDistance` outward, and the now-lateral
leftovers become the farthest-first recycle candidates that rebuild ahead — so the ribbon *breaks and
restarts in front of you* on a hard turn while staying a continuous ribbon through gentler ones.
Passed scenes and a turn's leftovers clear as new ones arrive — spawn frequency IS the clear
frequency, because the pool is finite and **closed**: a reclaimable scene (off the flight cone, or
dropped far behind) *collapses*, is relocated onto the ribbon ahead, re-posed into a fresh recipe
with new domain colour, and *bloomed* back out. The transport runs in three phases, **none of which
costs a per-frame CPU pass over the scene's prisms** (`Docs/PRISM_ANIMATION.md` §5 C8):

1. **Collapse** — one grow-clock re-stamp per prism toward the animator's min scale, budgeted at
   `Microscene.TransportBudgetMsPerFrame`. The GPU runs the shrink; gameplay state goes final at the
   stamp. *(The predecessor scaled the CONTAINER over ~2.4 s and re-synced every child prism's
   spatial entry AND companion render entity every frame to make that visible — because a container
   scale is invisible on the instanced path otherwise. At 1,500 prisms/scene that was ~180,000
   writes per recycle.)*
2. **Transport** — the stock is hidden (`Prism.HideForTransport`) and the container moves in ONE
   transform write. Unseen by construction: the off-screen removal gate below is what licenses it.
3. **Re-pose + bloom** — the same prism instances take fresh slots, domains and kinds, budgeted, and
   bloom back in from zero on the clock. `Prism.BeginBulkTransport` raises the creation-completion
   budget for the duration so a grand scene re-enters in about a second instead of trickling back
   over four. Scenes still in the cone ahead (what
you're flying toward) are never reclaimed, and a scene mid-recycle **claims its destination slot
immediately** (`Microscene.PendingAnchor`) so a rebuild never piles several arrivals onto one point
while the blooms are in flight. No score, no end condition; every belt advance is driven by the
player's own motion (no timers). Exiting freestyle makes the belt dormant; toggling it off stops the
flow — either way its scenes stay in the world.

**Visibility guards — the transport itself is invisible.** The belt should read as a world that is
simply *there*, never as props spawning and despawning in view, so two gates constrain where the
continuity transitions may run:
- **Placement floor** (`minPlacementDistance`, default 380 u) — a scene never blooms in closer than
  this to the vessel. Near-fill (`≥ firstSceneDistance`) and extend already target far ahead; this is
  the hard floor (squared compare) that also covers degenerate geometry, so a structure never
  materialises in the player's face. Keep it at or below `firstSceneDistance` so it never fights
  normal near-fill.
- **Off-screen removal** (`offscreenMargin`, default 80 u) — a scene is only reclaimed (collapsed
  and carried away from its old anchor) once its whole body — a `sceneRadius + offscreenMargin`
  sphere — lies
  **fully outside the player camera's view frustum** (`GeometryUtility.CalculateFrustumPlanes`,
  non-allocating overload; a straddling sphere counts as *visible* and is left alone). The collapse
  and the hide are therefore never watched; the bloom then happens far ahead at the new pose. This
  gate is load-bearing twice over: it is also what makes phase 2's outright hide legitimate rather
  than a continuity breach. If every pooled scene is
  on screen the belt simply idles — placing nothing — until the player's motion pushes a scene out of
  view, at which point the field self-heals. Camera-less fallback (rare): a scene clearly behind the
  flight course, which the follow camera cannot see. The pool still *fills* to `poolSize` regardless
  (new placements don't remove anything), so this gate only ever throttles recycling, never growth.

### The run — Wanderway as its own mode (`WanderwayRun` + `WanderwayReturnToy`)

The belt is what you fly *through*; the **run** is what makes the wander a place you go to and come
back from. Starting one does three things, and all three are undone when it ends:

- **A bare canvas.** The host cell is handed its **bare-canvas** config — the one that grows
  nothing (`Cell.BareCanvasConfig`: no `EnvironmentPrefab` **and** a `SpawnProfile` listing no
  flora and no fauna, which is `Barren`) — through the one sanctioned entry point,
  `Cell.RequestCellSwap(canvas, clearLooseTrailMass: true)`. Re-selecting the config the cell is
  already on is the documented freestyle **reset**, which is exactly what starting a wander should
  mean. It is requested *before* the belt's stock build so both join ONE load-veil hold instead of
  stacking two covers. Authored off (`revertCellOnStart`) if a designer wants the wander to happen
  inside whatever world is up; with no bare config it falls back to the cheapest environment-free
  one, and with none of those it warns and leaves the world alone.

  This used to read `Cell.EnvironmentFreeConfig` — "the first config with no `EnvironmentPrefab`",
  which was the Blob and was therefore also bare. **Those are two different properties**, and the
  Lattice cell separated them: it authors no environment (so it boots instantly, and it is now
  Menu_Main's boot world) and then grows a 21,600-prism forest out of eight seeds — cheap to
  build, the opposite of empty. Reverting a wander onto it would have grown a garden under the
  belt's own 30,000 transported prisms. See `Docs/ECOSYSTEM.md` §36.10.
- **A rolling tether.** The trail follows you as a ribbon of exactly `tetherPrisms` (100): as you
  lay at the head, the oldest prism at the tail withers and **recycles back into the pool it came
  from**, so the next prism you lay is very often the one that just left. Turn around and your trail
  is there; fly on and a little flying lays a fresh path home. That closed loop is what makes the
  wander a *truly infinite runner at fixed memory* — see the invariant note below.
- **A way home at its tail.** The **return station** rides the oldest end of that ribbon, so the way
  out is always exactly one tether-length behind you, for the whole wander. It is a full `Toy`, not
  a bespoke trigger, so it inherits local-user detection, freestyle gating, the bloom-in, deferred
  activation, and the exit-gated re-arm. It glides onto the tail every frame rather than snapping on
  the run's tick — the tail advances a prism at a time and a station that teleported after it would
  read as a pop.

> **The rolling tether is an AUTHORIZED EXCEPTION to mass conservation** — the one sanctioned place
> trail mass is recycled, granted by explicit sign-off so the Wanderway can be an endless runner
> without an ever-growing world. It is mechanically the reverted `maxTrailBlocks` cap, and it is
> fenced so it cannot leak: `WanderwayRun.RollTether` is the ONLY caller of `Trail.RemoveOldest`,
> it runs only while a run is live, and `VesselPrismController` grew no cap field — outside a run
> the trail is untouched and the law holds in full. **Continuity of existence is not waived**: a
> retiring prism withers on the GPU clock (one grow-clock re-stamp toward a near-zero scale — the
> belt's own collapse, `Docs/PRISM_ANIMATION.md` §5 C8) and returns to the pool only once it has
> shrunk away. Full record: `Docs/ECOSYSTEM.md` §0. Do not generalise it; do not revert it.

**Three exits, one path.** The return station, another pass through the Wanderway toy, and the
**overview button** (the freestyle HUD's Volume/Pause button, and gamepad **Start**) all call
`WanderwayRun.End(returnToCell: true)`. The overview route needs no new wiring: that button routes
through `MenuCrystalClickHandler.ToggleTransition`, which drops freestyle, and the run watches
`ToyContext.IsFreestyleActive` for that edge. Ending a run stops the belt, stops recycling, retires
the station, flips the toy's label, and puts the vessel back where the wander started
(`IVessel.SetPose` + `SetInitialSpeed`, the same repose the menu vessel-swap uses, so speed carries
through) — skipped when they are already home, so ending the run AT the toy never jerks their pose.
The belt's **scenes stay in the world**: conserved mass and released citizens are not toy props to
vanish. So does the Blob cell — restoring a previous world is the Cell Selector's job, not the
wander's.

**Ecosystem invariants (this toy is ecology-adjacent — all hold by construction):**

- *Continuity of existence* — prisms grow in via their own `PrismScaleAnimator` (the GPU clock);
  crystals `FadeIn`; recycling is collapse-out → bloom-in (both sanctioned transitions), and the one
  step that is neither — the hide before the move — is provably unseen (off-screen removal gate).
  Nothing pops.
- *Mass conservation / no passive removal* — the belt never destroys a prism; recycling
  **transports the same prism instances** (movers contract: `Prism.NotifyPositionChanged`).
  The only sink is fauna grazing belt prisms (an active force); grazed slots are re-minted
  through the sanctioned pool-reuse lifecycle (`Prism.Initialize`), which is creation. Player
  trails through scenes are never touched. This is *not* the rejected "recycle the oldest prism"
  budget cheat: nothing is removed, the belt's total stock is fixed, and only toy-owned
  exhibit content rides the belt.
- *No imposed death* — lifeforms are **released, not owned**: meadow/menagerie scenes spawn
  flora/fauna into the host `Cell` via the canonical sequences (`Initialize(cell)`,
  `AssignLineage`, `RegisterSpawnedObject`) and never track, move, or despawn them. They live
  and die by the food web only.
- *No domain asymmetry* — fauna spawn in `Cell.ControllingDomain`; flora in a random playable
  domain (both via the canonical `CellLifeSpawnerBase` spawn path the cell's own spawner uses).
  Multi-domain colouring applies only to *prisms* (neutral mass), distributed per-scene by a
  coherent domain scheme (incl. optional neutral-Blue veins) — not a per-domain spawn bias.
- *Volume is the spine / collider budget* — belt mass is bounded at
  `poolSize × prismBudgetPerScene` prisms (default 20 × 1500 = **30,000** BoxColliders + ≤6 crystal
  triggers per scene + 1 toy trigger — the same order as an authored cell environment, and the belt
  is spread over ~10 km of ribbon so only the near scenes are ever un-culled); distant scenes are
  collider-LOD-culled by `PrismColliderLodManager` automatically. **Shielded / supershielded**
  prisms now KEEP their authored cullable `BoxCollider` trigger (the octahedron / stellation is a
  look-only change — no convex MeshCollider, no convex cook), so they cost the same as any other
  prism and LOD reclaims them normally; the palette caps (`MaxShielded = 3`, `MaxSuperShielded = 1`
  per scene, low scheme weights) now bound spawn variety, not a collider-cost floor. Danger prisms
  likewise keep the cheap cullable BoxCollider. The belt roams freely — mass
  laid inside a cell registers with that cell's volume/grids as usual; mass laid in open space
  is ordinary registered prism mass with no cell binding (same as any open-space track). The
  conveyor adds **zero physics queries** — placement is pure arithmetic.
- *Cell owns the environment* — no parallel spawner/boundary/population: lifeforms come from the
  cell's own `SpawnProfileSO` configs, respect `FloraPlantingEnabled` + `MaxLivePopulation` +
  the prey floor, and are released only when the scene sits INSIDE a live cell
  (`Cell.FindCellContaining`) — open-space scenes are prisms + crystals only.

Crystals are the four elemental pickups (`ElementalCrystalSetSO`, Resources-loaded), made
skimmable at runtime via the internal setters added to `ImpactCollider` /
`ElementalCrystalImpactor` (the runtime mirror of the components lifeform prefabs author in the
inspector). Content is local-only, like every toy (party guests don't see your belt).

## Freestyle input ownership (reaching / leaving the toybox)

Toys only activate in freestyle, so who owns the gamepad matters. In the menu two readers poll
the one pad: the **vessel** (`InputController` → `GamepadInputStrategy`, gated by
`InputController.SetPause`) and the **appshell** (`ScreenSwitcher` screen-nav, already gated on
`_isInFreestyle`, plus the EventSystem's UI `Navigate`/`Submit`). The EventSystem was ungated, so
in freestyle the steering stick also drove the UI selection ring and the fire button also
submitted the (still touch-interactable) vessel HUD.

- **Enter/leave** freestyle via `MenuCrystalClickHandler.ToggleTransition` (tap the crystal to
  enter; the on-screen Volume/Pause button *or* — new — the **gamepad Start button** to leave).
  `MenuMiniGameHUD.Update` polls `Gamepad.current.startButton` while in freestyle and calls
  `ToggleTransition`, so pad players hand control back to the appshell without reaching for a
  touch button.
- **Mutually-exclusive ownership:** in menu state the vessel input is paused (autopilot); in
  freestyle `ScreenSwitcher.HandleEnterFreestyle` sets `EventSystem.sendNavigationEvents = false`
  (restored on exit), so the pad flies the ship and no longer double-drives the UI. Pointer/touch
  input is unaffected, so touch HUD buttons still work.

## Placement

`ToyboxController` places one toy per **unlocked** definition once the menu vessel is ready
(`GameDataSO.OnClientReady`). Each toy sits near the **cell membrane**, spaced far apart:

- If a `Cell` is active in the scene (`Cell.FindNearestActiveCell`), toys ring its membrane
  at `center = cell.transform.position`, `radius = cell.MembraneRadius * membraneFraction`
  (default 0.82 — just inside the boundary where the vessel flies).
- **Menu_Main has no Cell/membrane today**, so the controller falls back to a configurable
  `fallbackCenter` + `fallbackRadius`. The moment a Cell is added to the menu, placement
  snaps to the real membrane with no code change.

Definitions with `placementAngleDegrees < 0` (the default) auto-distribute evenly around the
ring so they stay far apart; set a specific angle per toy to pin it.

## Toybox & unlock state

`ToyboxSO` is the registry: a list of `ToyDefinitionSO` + an id→bool unlock map.

- **Unlock conditions are deferred.** Every toy ships unlocked
  (`ToyDefinitionSO.UnlockedByDefault = true`). The unlock *state* lives in `ToyboxSO` with a
  clean `SetToyUnlocked(id, unlocked)` hook and an `OnToyboxChanged` event a future
  persistence layer can drive (load on sign-in → `SetToyUnlocked`; subscribe to
  `OnToyboxChanged` → save), mirroring the `FavoriteSystem` JSON pattern. No persistence is
  implemented yet.
- **Zero-config fallback.** If no `ToyboxSO` is assigned and none exists at
  `Resources/Toybox`, the controller synthesises a default toybox of the three built-in toys
  at runtime (the `ElementalBarsView` "zero-wire by default" precedent). So the system works
  the moment `ToyboxController` is in the scene, before any assets are authored.

## Adding a new toy

1. Add a `Toy` subclass with the behaviour (`OnActivated(IVesselStatus localVessel)`), or a
   `SwapToySetCoordinator<T>` subclass for a flip-set toy.
2. Add a `ToyDefinitionSO` subclass whose `Spawn(...)` builds it via `ToyFactory`, and override
   `Category` — it is abstract, so the compiler asks.
2b. Its switch ring needs nothing: `Toy` draws one from the toy's own trigger collider, painted
   `Neutral`. Only reach for `ToySwitchSignal.Domain` if threading it genuinely hands the pilot a
   domain — `ToySwitchVocabularyTests` will otherwise fail, by design.
3. Add the new definition asset to the `ToyboxSO` (or to `BuildDefaultToybox` for a built-in).
4. Add a case to `ToolCodexHarvester.AddKindFacts` so the encyclopedia can say what the toy
   offers, then run **FrogletTools ▸ Interface ▸ Codex** ▸ *Scan & Merge* and *Bake Missing*.

The framework never changes — definitions are polymorphic factories, so there is no central switch
in the toy system itself. Step 4 is the one place a new toy is named outside its own files, and it
**warns rather than fails**: a toy with no case gets an encyclopedia page carrying only the rows
every tool shares, which is visible in the tool's own scan report.

## Setup (one step in Unity)

Run **Tools → Cosmic Shore → Setup Freestyle Toybox**. It:

1. authors the toy definition assets under `Assets/_SO_Assets/Toys/` (the conveyor's
   prism prefab + crystal effect are auto-wired: `SpawnablePrism.prefab` +
   `SkimmerAdjustElementLevelByCrystalEffect.asset`; the Cell Selector needs no wiring — it
   reads the Cell's own rotation),
2. creates `Assets/Resources/Toybox.asset` and registers them, and
3. adds a `ToyboxController` to Menu_Main (on the `MenuCrystalClickHandler` object) pointing
   at the toybox.

Idempotent — safe to re-run. (Or simply drop a `ToyboxController` on any Menu_Main object and
rely on the runtime default toybox.)

**One scene setting the tool cannot infer:** the Menu_Main `Cell`'s **Cell Type Choice Options**
must be **EnvironmentFree** for the fast boot (it is already set in the committed scene). Leave
it on `Random` and the menu goes back to rolling a heavy world on every entry — the Cell
Selector still works, it just is not the only place the load is paid.

## Networking notes

- **Cell selection is local**, like every other toy effect with no server-authoritative path:
  in a party each client would run its own cell. Not a regression — environments already build
  locally with no seed sync, and the `Random` roll it replaces already gave each client a
  *different* cell. Making it authoritative means an RPC on the menu cell (`BACKLOG.md`).
- Each client runs its own `ToyboxController` and spawns its own local toy GameObjects
  (deterministic placement → they overlap visually across clients). Toys are **local
  interaction stations**, not networked objects; only the *effects* (vessel swap, domain
  change, AI-companion release) go over the network, through the existing server-authoritative
  paths. This matches the "local-only freestyle toggle, network-replicated vessel behaviour"
  model in CLAUDE.md.
- **The Lifeform Matrix's VESSELS branch is server-authoritative**, unlike its flora/fauna
  branches. That is not an inconsistency: lifeforms are already client-local by construction
  (every peer runs its own spawner off local `Random` rolls — `Docs/ECOSYSTEM.md`), while a
  vessel is a `NetworkObject` with a `Player`, so there is no such thing as a local one. It
  routes through the same host-does-it / client-asks shape as the vessel swap.
- `IsLocalUser` ensures only your own vessel trips your toys; the freestyle gate ensures the
  autopilot lava-lamp vessel never does.

## Status & follow-up

The framework + **six toys** are in (Vessel Changer, Domain Changer, Painting, the Wanderway
microscene conveyor, the Lifeform Matrix — now three-kingdom, with an AI-companion hangar — and
the Cell Selector),
plus the vessel-changer second-pass fixes above: mini-model hull rendering,
exit-gated re-arm + slow flip re-grow, swap continuity (domain / pose / speed), recolour-on-domain,
HUD-after-swap, and gamepad-Start / input-ownership. The conveyor has been through two adversarial
review passes (compile, logic, ecology invariants, game-feel, assets, docs). All are
compile-reviewed against the real codebase but **not yet play-verified in an editor** (no Unity in
the authoring environment) — an in-editor pass is the last step before/after merge. Remaining polish
(per-toy tuning, skinned-mesh `BakeMesh` fidelity, painting pen-up, placement anchor, conveyor
recipe/pacing tuning + audio, unlock persistence, tests) is tracked in **`BACKLOG.md`**, grouped so
each area can be its own branch.

### Files touched — Lifeform Matrix kingdom pass (for review)

| Area | Files |
|---|---|
| The hierarchy (kingdom → species/hangar → variant) | `Controller/Toys/LifeformMatrixToy.cs`, `ScriptableObjects/Toys/LifeformMatrixToyDefinitionSO.cs` (`vesselRoster`), `_SO_Assets/Toys/Toy_LifeformMatrix.asset` |
| Shared vessel roster + hull builder (recycled from the changer) | `Controller/Toys/ToyVesselRoster.cs` (new), `Controller/Toys/VesselChangerToy.cs` |
| AI companion release (server-authoritative) | `Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs` (`RequestSpawnAiCompanion`), `Controller/Multiplayer/ClientPlayerVesselInitializer.cs` (`RequestAiCompanion_ServerRpc`, `OnAiCompanionRequested`) |
| Externally-spawned player claim | `Controller/Multiplayer/ServerPlayerVesselInitializer.cs` (`ClaimExternallySpawnedPlayer`), `Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs` |
| Ring geometry gate | `Tools/Build/toy_switch_ring_geometry.py` (kingdom + hangar sites) |

### Files touched — one-toy-opens-into-many pass (for review)

| Area | Files |
|---|---|
| Shared base | `Controller/Toys/MatrixToy.cs` (new) |
| Cell Selector (orbs removed, moved onto the base) | `Controller/Toys/CellSelectorToy.cs` |
| Vessel Changer (set → matrix) | `Controller/Toys/VesselChangerToy.cs` (new), `ScriptableObjects/Toys/VesselChangerToyDefinitionSO.cs`, `Controller/Toys/VesselChangerToySet.cs` (deleted), `Controller/Toys/SwapToySetCoordinator.cs` (docs) |
| Painting gallery (16 stations → matrix) | `Controller/Toys/PaintingGalleryToy.cs` (new), `ScriptableObjects/Toys/PaintingToyDefinitionSO.cs`, `Controller/Toys/PaintingToy.cs` (run survives the fold) |
| Assets | `_SO_Assets/Toys/Toy_VesselChanger.asset`, `_SO_Assets/Toys/Toy_Painting.asset` |

### Files touched — Cell Selector pass (for review)

| Area | Files |
|---|---|
| Environment-free boot | `Controller/Environment/Cell.cs` (`CellTypeChoiceOptions.EnvironmentFree`, `FirstEnvironmentFreeIndex`), `_Scenes/Menu_Main.unity` |
| Runtime cell swap | `Controller/Environment/Cell.cs` (`AvailableConfigs`, `RequestCellSwap`, `SwapCellConfigRoutine`, `ReleaseRetiredWorld`, `RetireWorldIntoSuctionRoot`, `SetVesselTrailsDetached`, `SpawnVisuals(spawnEnvironment)`) |
| The toy | `Controller/Toys/CellSelectorToy.cs`, `ScriptableObjects/Toys/CellSelectorToyDefinitionSO.cs` |
| Shared matrix station (extracted) | `Controller/Toys/ToyMatrixStation.cs`, `Controller/Toys/LifeformMatrixToy.cs` |
| Registration | `Controller/Toys/ToyboxController.cs`, `Editor/ToyboxSetupTool.cs`, `_SO_Assets/Toys/Toy_CellSelector.asset`, `Resources/Toybox.asset` |

### Files touched — vessel-changer pass (for review)

| Area | Files |
|---|---|
| Re-arm / escape | `Controller/Toys/Toy.cs`, `Controller/Toys/SwapToySetCoordinator.cs` |
| Mini-model rendering | `Controller/Toys/ToyModelBuilder.cs`, `Controller/Toys/VesselModelBuilder.cs`, `Controller/Toys/VesselChangerToy.cs` |
| Recolour on domain change | `Controller/Toys/VesselChangerToy.cs` (`Update`) |
| Domain preserved on swap | `Controller/Multiplayer/ClientPlayerVesselInitializer.cs` (`ReInitializePair`) |
| Speed inherited on swap | `Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`, `Controller/Vessel/IVessel.cs`, `Controller/Vessel/VesselController.cs`, `Controller/Vessel/VesselTransformer.cs` |
| HUD re-show after swap | `Controller/Multiplayer/ClientPlayerVesselInitializer.cs`, `UI/MenuMiniGameHUD.cs` |
| Gamepad Start / input ownership | `UI/MenuMiniGameHUD.cs`, `UI/ScreenSwitcher.cs` |
