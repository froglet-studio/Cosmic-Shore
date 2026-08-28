# Mode Preview — the window the game plays in

Replaces the arcade card's pre-rendered preview video. Selecting a card starts the mode's own
arena **playing live, under AI, inside the modal's preview window** — the way the old video showed
a game in progress, except it is the real one. Tapping the window takes the stick from the AI;
tapping outside gives it back. The window **never changes size**, the modal never closes, and the
menu scene behind it never changes.

`SO_Game.PreviewClip` is **deleted** — there is no video path left anywhere (the Daily Challenge,
Faction Mission and Hangar Training surfaces had their video instantiation stripped with it). A
mode either previews live or its window says **"LEVEL PREVIEW NOT AVAILABLE"**. Nothing else may
ever draw in the frame: the white rectangles, leaked vessel imagery and stale videos of the first
playtest were all fallback branches, and the fix was deleting the branches.

---

## 1. The flow — a MODEL to look at, the real cell to fly

```
card selected (SetSelectedGame)
 └─ session.SetDefinition(def, mode's hull, intensity)
     ├─ no definition / no authored environment → "LEVEL PREVIEW NOT AVAILABLE"
     └─ otherwise → "LOADING <MODE>…", then automatically:
         ├─ arena.StandModel()             a SCALE MODEL - no prisms, no cell, no ecology
         ├─ arena.BeginArenaCamera()       its own camera → the window's RenderTexture
         └─ state = SHOWING                the world, slowly orbiting. "TAP TO PLAY"

tap the window     → state = LIVE:
         ├─ arena.Stand()                  NOW the real satellite cell is built
         ├─ RequestSwap(mode's hull)       pose / speed / domain preserved
         ├─ vessel.SetPose(arena spawn)
         ├─ AIPilot.RetargetCell(arena)    ← load-bearing, see §3
         ├─ CameraManager.BeginWindowedPlayerCamera → the same RenderTexture
         └─ arena.EndArenaCamera() + StrikeModel()   AFTER the handover, never before

tap outside /
Escape / Start     → back to SHOWING
card change /
modal close        → Stop(): model struck, arena struck and drained (§4)
```

### 1.1 A card you are LOOKING at must not build a cell

The first build stood the real satellite the moment a card was selected. That is a full per-prism
build — the Boneyard alone is **~69k prisms**, on top of the menu world that is already live — so
browsing cards meant a multi-second freeze each and a frame-rate collapse for as long as one was
up. Measured in the Editor at **1 FPS**.

Selecting a card now builds a **scale model** instead, through `CellMiniatureBuilder` — the same
path the Cell Selector toy already uses to show a world you have not chosen yet. It reads the
generator's point data and spawns **no prisms**: generation is pure math, and the per-prism
`Instantiate` that is ~97% of a real build never happens. One mesh, a submesh per domain, a few
draw calls. The lays are released immediately after sampling, because retaining a 34k-entry list
per card somebody browsed past is the trade this path exists to refuse.

**The real cell is built on the tap** — the only moment anybody has asked for it.

A config with no authored `EnvironmentPrefab` has no structure to *sample* — see §1.1.1 and
§1.1.2 for what those cells show instead.

### 1.1.1 A cell with no authored environment still has a SHAPE

Joust, Scurry and Skim Race run on the Barren cell and Rampage GROWS its forest, so none of them
authors an `EnvironmentPrefab` — and a model path that only understood authored environments told
all four "LEVEL PREVIEW NOT AVAILABLE".

`StandModel` now also stands the config's **membrane and nucleus** as display copies, scaled into
the framing radius. Two objects, so it is free next to the environment model, and it is what those
cells genuinely look like at the start of a match — a truer answer than a refusal. The copies are
stripped of colliders and behaviours: nothing here is a `Cell`, so a live component would tick
against one that does not exist.

(This is not the "never hand-place a membrane" rule being broken — that rule protects a live
`Cell`'s own tracked instance from being shadowed by a scene copy. There is no `Cell` here.)

### 1.1.2 A GROWN world shows its PLANTING

Only **three** of the seventeen preview cells author an `EnvironmentPrefab` — the Boneyard, the
Ribcage and the Wildlife cages. The other fourteen have no generator at all: their arenas are
**planted by the spawn profile once a match starts**, so at the instant a card is opened there is
literally nothing built to sample. That is data, not a defect, and it is why "the environment does
not show up" was true of almost every card while the model path was working perfectly.

What *does* exist before anything grows is the **planting**: how many of each species, and which
band of the cell each occupies. `ModePreviewPlantingModel` emits one marker per plant at a position
drawn the same way the spawner draws it, and hands the result to the ordinary
`CellMiniatureBuilder.BuildFromLays` — so it costs the same nothing as an authored model (no
prisms, one mesh, a submesh per domain).

Three things it gets right on purpose:

- **The band is a CELL fact, not a species fact.** The prefab's own
  `plantRadiusCellFraction`/`…Min` are only the species' default; a cell states its own layout with
  `FloraConfigurationSO.PlantRadiusCellFractionMaxOverride`/`MinOverride`, applied at spawn through
  `Flora.ApplyVariantTuning` and winning over both the prefab and the rolled element variant.
  **Rampage's entire cactus belt lives there** (0.1–0.95, 0.1–0.8, 0.14–0.9, 0.4–0.96, 0.3–0.97
  across its five species) — read the prefab alone and every species comes back at the default
  shell, so the model would draw a belt the mode does not plant.
- **The draw is volume-uniform** (`cbrt` between the cubed walls), matching
  `CellLifeSpawnerBase.RandomBandRadius`. A uniform-in-radius draw puts 63% of a population inside
  the innermost quarter-volume, so a model using one would show a band the cell does not plant.
- **The RNG is borrowed, not spent.** `Random.state` is saved and restored around the build: this
  runs while the menu's own ecology is live, and an ecology is exactly the kind of system whose
  behaviour depends on its RNG sequence. The seed is a stable hash of the cell name, so a card
  shows the same arena every time it is opened — a preview that reshuffled would read as
  instability in the arena rather than in the seed.

**It models the planting, not the plants.** A grown plant's geometry is emergent — it depends on
how long it has lived and what has eaten it — so a marker stands for "a plant of this species is
here", never for its shape. Fauna are excluded for the same reason from the other direction: they
move *through* an arena rather than being part of it, and a still model of where they happened to
start says nothing true.

### 1.1.3 A SCENE-BUILT environment shows its track

"Shell alone" was still wrong for three cards, and the reason is a third place an arena can come
from: **the scene**. Joust, Scurry and Skim Race all run cells that author no environment and
plant no flora — and none of those arenas is open water, because each mode's scene carries a
`SegmentSpawner` that stands the structure at match start. No `CellConfigDataSO` can say that, so
the cell-only model path was structurally blind to it.

`ModePreviewDefinitionSO.TrackSpawnablesByIntensity` closes it: the definition names the
`SpawnableBase` prefabs the mode's own spawner stands, and the model samples them exactly the way
it samples an authored `EnvironmentPrefab` — pure generation math, no prisms. Authored by
`Tools/Build/author_preview_tracks.py`, which reads the SHAPE straight out of the scene files
(`--check` verifies the assets still agree with the scenes):

- **Joust** and **Scurry** author `spawnableByIntensity` — four prefabs, one per intensity
  (Torus Knot / Hopf / Schwarz P / Gyroid; Clifford Torus / Concentric Spheres / Helicoid /
  Atlantis) — which maps 1:1 onto the list. Note Scurry's intensity 4 is `SpawnableAtlantis`,
  a ~34k-lay generation pass: it runs once on that card+intensity and the lays are released,
  but it is the most expensive model any card stands.
- **Skim Race**'s track is a scene-local `SpawnableWaypointTrack` — an object, not a prefab —
  so it was baked verbatim into `_Prefabs/Environment/Spawners/HexRaceWaypointTrack.prefab`
  (the component body copied byte-for-byte, four authored waypoint sets included) and the
  definition holds that ONE intensity-aware entry. The model walks
  `SpawnableWaypointTrack.GetPreviewBlocks(intensity)` — the mirror of `Spawn` that already
  existed for editor preview — via `ModePreviewTrackModel.BuildWaypointLays`, with domains
  cycling the playable triad per waypoint segment (matching `SegmentSpawner`'s live painting).
  **If the scene's track is retuned, re-bake the prefab and re-run the author script.**

Two knock-on rules the track added:

- **"Same cell" is not "same arena".** Skim Race runs the Barren cell at every intensity while
  its track changes completely, so the session's rebuild-skip now asks
  `ModePreviewDefinitionSO.ArenaDiffers(a, b)` — one method answering for every axis the arena
  can vary on (cell, track, whatever comes next) — instead of comparing cells at the call site.
- The track model is **additive** to shell/planting/environment: nothing today authors both, but
  a mode that did would genuinely have both in its arena.

Measured coverage after this (per preview definition, at every authored intensity):

| What the card shows | Modes |
|---|---|
| Full scale model of an authored environment | Dog Fight, Peel the Cage, Wildlife Liberation |
| Track model + shell (per-intensity) | Joust, Scurry, Skim Race |
| Planting model + shell | Rampage, The Bends (59 markers / 5 species), Wildlife Blitz ×2 (4 / 1) |
| Shell alone | Astro League, Brood Rush, Scarab Scramble, Freestyle, Cellular Duel ×2, 2v2 Co-Op |

The remaining shell-only cards split two ways: Brood Rush / Freestyle / Cellular Duel / 2v2
genuinely are open water at t=0 (no environment, fauna-only profiles, inert scene spawners) —
what fills them is the players' own trails. **Astro League and Scarab Scramble are the honest
remaining gap**: their arenas are built by their CONTROLLERS (goals, hoops, the ricochet court),
which is the `StructurePrefab` extraction §7 already records.

### 1.1.3 The vessel arrives where the MODE would put it

`SpawnPose` used to build a one-player `Symmetric` ring at a standoff the preview definition
authored for itself. That is an independent guess at a number the mode's own scene already states,
and the two disagreed badly — measured against the scenes:

| Mode | Scene says | Preview said |
|---|---|---|
| Skim Race | hand-placed at `(700, 20, −200)`, facing down the track | ring at 70 u |
| Rampage / The Bends | ring, +500 u outside the nucleus | 70 u |
| Wildlife Liberation | ring floor **1150**, EquatorialRing | 70 u, Symmetric |
| Scarab Scramble | ring floor **760** | 70 u |
| Dog Fight | ring floor **700** | 70 u |
| Peel the Cage | ring floor **576**, EquatorialRing | 70 u, Symmetric |
| Joust / Astro League / Brood Rush | hand-placed on a 70.7 u ring, each facing the core | 70 u ring |

So a card opened you inside the arena the mode starts you outside of, and the two modes whose scenes
ask for `EquatorialRing` — the ones whose arenas have a meaningful pole — got a spherical spread.

The definition now carries the scene's own spawn block (`SpawnFromCellRing`,
`SpawnDistanceOutsideNucleus`, `SpawnRingRadiusFloor`, `SpawnFormation`, `SpawnPoints`) and
`ResolveSpawnPose` runs the mode's own resolution: `CellSpawnFormation` with the scene's radius and
formation for a ring mode, the scene's own transforms for a hand-placed one. It is authored by
`Tools/Build/author_preview_spawns.py --check`, which reads the scenes rather than trusting anybody
to keep two numbers in step.

**Hand-placed poses are stored relative to the scene's CELL, not absolute.** A preview arena is
parked 120k units from the menu world, so an absolute scene coordinate would put the vessel back at
the menu's origin, in the middle of the lava lamp.

**The flight arena builds THINNED — the mode's real shape at a fraction of the prisms.**
Tapping in used to hitch the menu: the satellite built the mode's FULL world (environment +
track structure, tens of thousands of prisms with colliders and spatial-index entries) beside a
scene that was still running. The preview now lays every dense trail at a STRIDE
(`PrismLayDecimation` — a scoped every-Nth subsample applied in `SpawnableBase.SpawnPrismTrail`
*before* the builder, so streamed lays that outlive the scope were already thinned): a torus is
still a torus, at a fraction of the prisms. The knob is
`ModePreviewLibrarySO.FlightPrismStride` (authored 4 — every 4th prism; 1 = full density), trails
under 25 prisms always lay complete (thinning small furniture changes what it IS), and the stride
rides ON THE CELL (`Cell.SatellitePrismStride`, honoured only while `IsSatellite`) because the
environment build can be DEFERRED past `InitializeSatellite` — a caller-side scope would never
reach it. A real scene cell is pinned to stride 1 in code. Laying fewer prisms is "not creating
mass", which the conserved-mass law permits.

**The satellite is STRUCTURE, not ecology — its life spawner never starts, and it gets no
cytoplasm.** The first playtest showed previews seeding the mode's full living world beside the
running menu — flora colonies growing thousands of lattice prisms, Wildlife Liberation's ~519
fauna, and one always-on heart collider (an elemental crystal) per lifeform — which was most of
the lag, and none of it is what a preview is for. `Cell.StartSpawnerForMode` now returns before
picking a spawner class when `IsSatellite`, which covers `RandomLifeSpawner` and
`IntensityWiseLifeSpawner` alike and every start site (satellite bootstrap, swap completion,
`RestartSpawnerForMode`) by construction — and with no seeds, every downstream producer
(fauna/flora reproduction, lattice colony frontiers, heart drops) never begins. This is production
GATING, permitted by `Docs/ECOSYSTEM.md §0`; nothing is culled and nothing decays. The minted omni
crystals are untouched (they are the arena's own, not the spawner's). Stated cost: a GROWN world —
Rampage's cactus belt IS its spawner's planting — previews as its authored structure alone; the
looking-phase miniature still models the planting as markers (`ModePreviewPlantingModel`).
`Cell.SpawnCytoplasm` is likewise a no-op for satellites: the `SnowChanger` spawns ~4k individual
"shard" GameObjects (its own field names — the spiky star motes the second playtest reported as
"shards in a lot of levels"), and a preview paying a second cell's worth of them beside the menu
is atmosphere nobody asked for at a frame cost everybody feels.

**A kill-scored card releases a HANDFUL of authored creatures** — the one deliberate exception to
structure-only, because a `LifeformsKilled` objective with nothing to shoot is not a taste of the
mode. `ModePreviewDefinitionSO.PreviewFauna` + `PreviewFaunaCount` (the three wildlife cards
author 4 × QuadFish, the game's smallest species) are released by `ModePreviewArena` through the
canonical `CellLifeSpawnerBase.SpawnFaunaWithDomain` + `AssignLineage` path on a runtime clone —
the Lifeform Matrix bench's idiom — so each creature registers in the cell's lifeform book and the
strike retires it with the world. They spawn in **`Domains.Blue`** (the neutral sentinel, hostile
to every pilot) so anyone's rounds land; a kill still drops the heart (the lifeform-crystal
invariant is untouched), and `SpawnPreviewFauna` warns-and-skips on any card that authors a
species without being kill-scored.

**PrismLayDecimation applies at BOTH lay paths.** `SpawnableBase.SpawnPrismTrail` covers track
structures — but every `CellEnvironmentSpawnableBase` world (the Ribcage cage, Atlantis, the
freestyle seven) lays through `PrismTrailBuilder` with its own `_cachedLays` list and never calls
`SpawnPrismTrail`, so authored-environment previews silently built at FULL density while the
stride only thinned tracks. `SpawnLeafObjects` now hands the builder
`PrismLayDecimation.Apply(_cachedLays)` — a strided COPY inside a scope, the cached list left
whole for the miniature builder and the planting model, and byte-identical behaviour outside any
scope.

**A party CLIENT previews locally, and TWO defects hid that.** The preview is deliberately
unsynced — each machine stands its own local satellite (the client's hull swap already routes
through `RequestVesselSwap_ServerRpc`; only the sparring partner stays host-only, so a client
previews without one). What broke the client, twice over:

1. **The phantom modal.** The scene holds TWO `ArcadeGameConfigureModal` instances — the paneled
   arcade modal, and the Maelstrom panel's own window, which carries the component only as its
   `ModalWindowManager` and authors no launch panels. The config-open ClientRpc is a broadcast
   event, so the panel-less instance also "opened" on every client and drew its LEGACY detail
   view — the retired video path's solid white rectangle, with its chip spawn logging
   `No suitable tile` — over the real panel. The ready path already carried a two-instance guard
   (`HandleAllPlayersReady` bails with no `_selectedGame`); the client-follow handlers now carry
   the matching one: a modal with no launch panels does not follow remote opens/closes/screens.
2. **The window's Awake state wipe.** A client opens everything in one ClientRpc burst — arm,
   then `ModalWindowIn` — so `ModePreviewWindow.Awake` could run AFTER the session had driven the
   window to Loading/Live, and its unconditional `Apply(State.Hidden)` wiped that state. `Awake`
   now re-asserts the CURRENT state (`Apply(_state)`); a fresh window is already Hidden, so the
   cold path is unchanged.

**Intensity follows the host, and it is the ONE synced preview input.** The host's
`HandleIntensitySelected` broadcasts `ArcadeConfigSyncManager.NotifyIntensityChanged`; the
client's mirror (`HandleIntensityChangedOnClient`) clamps, reflects the row, and re-arms its own
LOCAL preview. The session's `SetDefinition` does the rest: an arena that differs strikes and
rebuilds — unwinding the client out of an active flight first, so "the host changed intensity
while I was flying" lands you back at the window with the NEW world standing, one tap from
re-entering — and an arena that does not differ (Rampage's identical forest) changes nothing
mid-flight.

**The tap-out drain is paced for the menu, not for speed.** The retiring root sits far outside
every camera, so the drain has no reason to hurry — but each frame's `Destroy` slice is paid IN the
menu the player just returned to. 500 prisms/frame read as a hitch train on the way back to the
lava lamp; the slice is 150/frame, trading invisible drain duration for visible frame cost.

**A minimum-two-player mode previews with a SPARRING PARTNER.** Joust and its siblings are
meaningless alone, so when the card's `MinPlayersAllowed >= 2` the modal arms the session with
`sparringPartner: true` and, on entering flight, the session spawns one AI through the menu's
ordinary networked pipeline (`MenuServerPlayerVesselInitializer.RequestSpawnAiCompanion` — the
Lifeform Matrix hangar's path, never a parallel local bot), seated at the mode's **seat 1**
(`SpawnPose`/`ResolveSpawnPose` grew a seat parameter: hand-placed modes use their second
authored pose, ring modes the second ring slot), in the first active domain that is not the
player's. The spawn API returns no handle, so the session snapshots the AI player set before
asking and polls ~5 s for the arrival, then `RetargetCell`s its `AIPilot` at the satellite (the
same serialized-cellData trap §3 records). It is despawned — vessel NetworkObject first, then
player — at all three places the AI retarget is restored (exit-flight, stop, abort), and the
whole feature is server-side only: a party client previews without one rather than asking the
host to spawn into its menu.

**A fire-and-forget spawn cannot be cancelled, so the teardown has to be able to reach an AI
that does not exist yet.** Tapping out during the ~5 s poll runs the despawn while
`_sparringPlayer` is still null — it removes nothing, and the AI then arrives into a struck
arena and flies on in the menu forever. Two things close it, and neither is "cancel the
watcher": the watcher deliberately does **not** take the session's `CancellationToken` (a
cancelled watcher orphans the very AI it was sent to collect, so it runs to its own deadline
instead), and the teardown bumps a **generation counter** which the watcher compares against
the one it captured — a partner nobody is waiting for any more despawns itself on arrival
through the same `DespawnAiPlayer` path. The watcher also re-checks each frame that a server
still exists, so a menu torn down under it exits rather than yielding forever. *General shape:
when a request is already away, the question is not how to stop it but how the result gets
cleaned up if nobody wants it.*

### 1.1.4 The window renders at the size it is DRAWN at

`renderHeight` was a fixed authored **360**, which is not a resolution — it is a resolution the
window happens to be correct at. The card's surface is ~625 px tall on a 1080p display, so every
preview was upscaled ~1.7× and read as soft; on a 4K display it would have been 3.5×. The height is
now measured from the surface's own rect **through the canvas scale factor** (a `RectTransform`'s
rect is in canvas units, and a `CanvasScaler` is the entire point of this project's UI), clamped to
an authored ceiling. Anti-aliasing follows `QualitySettings.antiAliasing` instead of being pinned
off.

**A live texture is never swapped.** A camera is bound to it and is not told, so a resize mid-flight
would leave a camera drawing into a destroyed surface — the white rectangle §1.2's ordered handover
exists to make impossible. Resizes therefore wait for the window to be idle, which is when a card is
opened anyway.

### 1.1.5 The arena camera borrows the game camera's SETTINGS

The tap-in phase always looked right because it borrows the real gameplay camera
(`CameraManager.BeginWindowedPlayerCamera`). The browsing phase built a bare
`AddComponent<Camera>()`, which comes up with **URP's defaults, not the project's** — no
post-processing, no anti-aliasing, SDR — so the two phases rendered the same world at visibly
different quality, which reads as "the preview is low quality" rather than as two cameras.

`AdoptGameCameraSettings` copies the base camera fields *and* the `UniversalAdditionalCameraData`,
which is the load-bearing half: post-processing, anti-aliasing and shadows all live there, not on
`Camera`. The scriptable **renderer index** is deliberately not copied — URP exposes `SetRenderer`
but no public getter for it in this version, so there is nothing to copy it from without reaching
into internals, and a camera in the same scene gets the pipeline's default renderer anyway.

The framing factor also moved 1.25 → **1.95**: at 1.25 the camera sat close enough to the membrane
that a card showed the inside of a wall rather than an arena.

### 1.2 Two camera rules, both learned the hard way

- **The handover is ORDERED, both ways.** The incoming camera takes the texture *before* the
  outgoing one lets go, so the surface never has a frame with nobody drawing into it — that frame
  is the white rectangle the window exists to make impossible.
- **Framing must not be sampled once.** `Cell.MembraneRadius` returns **0** until the membrane has
  spawned, so a `Max(1, radius)` fallback parked the camera 1.25 units from the arena centre, where
  every mode looked identical (skybox and a few distant prisms) and changing intensity rebuilt a
  world the camera was still standing inside. One camera in one wrong place, reading as two bugs.
  The radius is re-read on every orbit tick.

The objective runner starts on the tap, which is also the arrival.

## 2. Focus — who holds the stick

Focus is an input handoff and nothing else: `ToggleAIPilot(false)` + `InputController.SetPause`
+ `EventSystem.sendNavigationEvents = false`. No fades, no camera blend, no state-machine change.

**Gamepad B is deliberately NOT a release.** While flying, every face button belongs to the
vessel. `sendNavigationEvents = false` only silences EventSystem-driven UI — three places poll the
gamepad **directly** and each carries an explicit gate on the static
`ModePreviewWindow.AnyHasFocus`:

| Direct poll | Without the gate |
|---|---|
| `ModalWindowManager.Update` B-to-close | B while flying closed the modal → dumped to the arcade |
| `ArcadeGameConfigureModal.Update` d-pad + A | intensity rows silently changed behind the game |
| `ScreenSwitcher.Update` triggers / Y | already gated on `HasActiveModal` — no change needed |

Release routes: tap/click **outside** the window (mouse and touchscreen both), **Escape**, or
gamepad **Start** (the one pad button flight never uses; mirrors the freestyle exit). There is
**no leave button** — an on-screen button during flight is exactly the UI the focus gate exists to
keep out of the pad's way.

## 3. The satellite arena, and the two references that MUST move with the vessel

`Cell.InitializeSatellite` stands a second, fully-isolated cell (own volume ladder, own spatial
bindings, own colony frontiers — all already per-instance; `CellRuntimeDataSO` is the one shared
asset, so the satellite gets its own instance, bound **while the cell is still inactive** because
`OnEnable` clears `runtime.Config` on whatever asset it holds).

Two things learned from the first playtest, both now in code:

- **`AIPilot.cellData` is a serialized reference to the scene's shared runtime asset.** A vessel
  relocated 120k units away kept hunting the *menu* cell's crystals and immediately flew back out
  of the arena — the window then showed a lone vessel in empty space, which read as "the card
  shows an image of the vessel". `AIPilot.RetargetCell` points it at the arena's runtime instance
  for the duration (dropping its held objective, or commitment hysteresis keeps the old one) and
  is restored on stop.
- **A satellite never receives the first-crystal event that completes a scene cell's bootstrap**
  (cytoplasm, modifiers, **spawner**), because `CrystalManager` is scene-level.
  `InitializeSatellite` now runs `InitilizePostFirstCellItem` itself — without it a GROWN world
  (Rampage's cactus forest is nothing but its spawner's planting) stands lifeless and empty.

Isolation is by **distance** (120k units, past every camera's 8000 far clip), not by layer —
prisms, crystals and lifeforms interact through the physics matrix, and moving an arena onto a
private layer would quietly change how it plays. The idle-diorama stage and its `ModePreview`
layer were removed with the diorama itself.

## 4. The strike is POOL-SAFE, and that is not optional

The first teardown called `Destroy` on the arena root. The vessel's trail laid in the arena is
**pooled** prisms — destroying a pooled prism corrupts the pool's accounting, and a corrupted pool
breaks every trail in the scene, permanently: that was "the lava lamp is destroyed and the preview
no more works". The teardown now mirrors `RequestCellSwap`'s retire path exactly:

1. `Cell.StrikeSatelliteWorld()` — cancel any pending build, stop the spawner, detach every
   vessel's trail bookkeeping (a `Trail` dereferences its prisms without null guards), gather the
   world into a retiring root, **return pooled prisms to their pool**, clear the cell's
   bookkeeping. Returns the root holding only instantiated mass.
2. The session drains that root **500 prisms per frame** (a 10-20k-prism world destroyed in one
   frame is a multi-second freeze), then `FinishStrike()` destroys the cell, root and runtime
   instance — after the drain, so a prism destroyed mid-drain never dereferences a dead cell.

The vessel goes **home before** the strike, so the ribbon it laid in the arena is let go before
its prisms are returned. No suction animation: the arena is beyond every camera's far clip and the
window that showed it is gone — the same unseen-removal clause the microscene conveyor rides.
Mass conservation holds: created by a player action, removed by one (`Docs/ECOSYSTEM.md §19`).

### 4.1 The teardown is one SERIALIZED sequence — that is the "leave and come back" fix

The second playtest's "first time is cool, leave and come back and everything goes to chaos" was
the teardown racing the next entry. Three concrete races, all closed:

- **The hull-restore swap was fire-and-forget.** `MenuServerPlayerVesselInitializer.RequestSwap`
  silently drops a request while a swap is in flight (its `_isSwapping` guard) — so re-entering a
  preview while the restore swap was still running dropped the mode-hull swap on the floor.
  `Stop` now runs one awaited sequence (~a second: camera back → AI retarget restored → vessel
  home → hull swap **awaited** → arena struck and drained) and the session stays in `Striking`
  until every step lands; the auto-start driver only fires from `Idle`. `SwapVessel` additionally
  waits out any in-flight swap *before* requesting, so a request can never be dropped.
- **`DomainFaunaBuffSystem.EnsureExists` rebinds the scene's buff system onto whatever runtime it
  is handed** — a satellite's `Initialize` was handing it the satellite's instance, which the
  strike then destroyed, leaving the menu's fauna-buff system holding a dead SO. Satellites now
  skip that call outright (`Cell.Initialize` guards on `IsSatellite`); a preview arena's hearts
  are not the menu's economy.
- **The local trail spawner is penned up across the teleport home** — a spawner live for one
  frame after `SetPose` lays a prism bridging 120k units of empty space.

**A destroyed vessel PASSES `Vessel != null`, and that is the "play, leave, re-pick →
NO LEVEL PREVIEW AVAILABLE" bug.** `Player.Vessel` is typed `IVessel`, and Unity's
destroyed-object `==` overload only runs through `UnityEngine.Object`-typed references — so
after a match destroys the hull, the stale reference passes every interface-typed null check,
the session believes the vessel is ready, and the first dereference throws
`MissingReferenceException` inside `EnterFlightAsync`, whose catch shows the Unavailable card.
The session now tests liveness through ONE helper — `Alive(IVessel v) => v is UnityEngine.Object
o && o` — swept through the auto-start driver, `SwapVessel`, `ParkVesselInArena`,
`ReturnVesselHome`, `GrantStick`, `HandCameraToWindow` and `ExitFlightAsync`. General trap:
**an interface-typed reference to a `UnityEngine.Object` never fake-nulls; route the check
through the object type.** The previously-silent `StandModel` failure branch also logs the
config + intensity now, so any residual Unavailable card names its cause.

Entering freestyle (the lava lamp) also stops any running preview outright — the session
subscribes to `OnGameStateTransitionStart`. Normally the modal closing gets there first; this is
the guarantee. When the arcade modal is later restored (ScreenSwitcher's return-state), the card's
`SetSelectedGame` re-arms the preview from scratch, which the serialized teardown makes safe.

**A scene-reload cleanup (routing through Bootstrap) was considered and declined**: it would tear
down the Netcode host and the party session with it, and it treats the symptom — the races above
are the disease. If play-testing still finds teardown corruption, that fallback stays on the
table, but it cannot be the shipped shape.

## 5. The pieces

| Piece | Location | Job |
|---|---|---|
| `ModePreviewDefinitionSO` | `_Scripts/ScriptableObjects/` | Per-mode: cell, optional structure prop, vessel, objective, duration, spawn standoff |
| `ModePreviewLibrarySO` | `_Scripts/ScriptableObjects/` | Mode → definition. `Resources/ModePreviewLibrary`. Tournament excluded in code |
| `ModePreviewWindow` | `_Scripts/UI/View/` | Three states (unavailable / loading / live), the focus interaction, the RenderTexture, static `AnyHasFocus` |
| `ModePreviewSession` | `_Scripts/Controller/Arcade/Preview/` | Auto-start driver, vessel + AI bookkeeping, the camera loan, the strike |
| `ModePreviewArena` | `_Scripts/Controller/Arcade/Preview/` | Stand / StandModel / BeginStrike / FinishStrike |
| `ModePreviewPlantingModel` | `_Scripts/Controller/Arcade/Preview/` | A grown world's PLANTING as lays — one marker per plant, band resolved cell-override-first |
| `Tools/Build/author_preview_spawns.py` | `Tools/Build/` | Authors every definition's spawn block from the mode's own scene (`--check` verifies) |
| `ModePreviewTrackModel` | `_Scripts/Controller/Arcade/Preview/` | A waypoint track's blocks as lays, per intensity, triad-cycled per segment |
| `Tools/Build/author_preview_tracks.py` | `Tools/Build/` | Writes `TrackSpawnablesByIntensity` from the scenes' own spawners; `--check` in CI style |
| `ModePreviewRunner` / `ModePreviewHUD` | preview dir / `UI/View/` | Objective counting from first take-over. The beside-the-window readout is RETIRED: `StartRunner` hides the HUD and re-raises progress as `ModePreviewSession.OnObjectiveProgress(delta, total)`, which the modal routes into the launch panel's objective box + micro toast (`Docs/ArcadeLaunch/ARCHITECTURE.md` §5.5) — one counting source, one visible readout |
| `Cell.InitializeSatellite` / `StrikeSatelliteWorld` | `Controller/Environment/` | The platform capability pair |
| `CameraManager.BeginWindowedPlayerCamera` | `Controller/Managers/` | The real gameplay rig → a RenderTexture, additively (never `SetActiveCamera`) |
| ~~`ModePreviewSetupTool`~~ | *retired* | **Gone — scaffolding, its job done.** It stood the preview window up in `Menu_Main` and migrated the scene off earlier revisions (deleting TestFlightButton / FocusFrame / ExitButton / legacy video instances). The migrated scene is on the branch; recover the tool from git history if a scene ever needs the migration again. |

## 6. Shipped definitions (17) — every playable card

Every arcade card whose scene exists on disk now has a definition, so **every playable mode
previews** and only genuinely dead modes (the ~24 single-player cards whose scenes were deleted)
show the label. The display names that hid three of them: **Skim Race = HexRace(33), Joust =
MultiplayerJoust(34), Scurry = MultiplayerCrystalCapture(35)**.

| Group | Modes | Arena source |
|---|---|---|
| Full arenas | Rampage, Ribcage, Wildlife Liberation, Dog Fight, Scarab Scramble, The Bends, Nucleus Rush, Astro League, Skim Race, Scurry, Wildlife Blitz ×2 | The mode's own cell config — authored environment or grown via its spawn profile |
| Barren-cell modes | Joust, Duel for the Cell ×2, Multiplayer Freestyle, 2v2 CoOp | Their own scenes run on the Barren cell: open water + nucleus + the vessel. Sparse by construction, and the definitions' Notes say so |

Objectives count only where a stat fires solo (prisms destroyed, lifeforms killed); everything
else is open-ended — the satellite has no `CrystalManager`, so crystal-scored modes cannot count
yet (§7). Maelstrom (Tournament) stays excluded in code.

## 7. Known limitations

- **A satellite doubles the live ecology while a card with a preview is selected**, and browsing
  cards stands/strikes an arena per selection (plus a networked hull swap for vessel-locked
  modes). This is the cost of "a game already playing in the window". If browsing thrash shows up
  in profiling, debounce the auto-start by a second or two.
- **`SpawnableBase`-built arenas now exist in the FLIGHT phase too.** The tap-in arena builds the
  mode's `TrackSpawnablesByIntensity` entry for real (`ModePreviewArena.SpawnTrackStructure`) — the
  same asset the scene's `SegmentSpawner` builds, at the cell centre where the scenes build it — so
  Scurry's torus/shells/helicoid and Skim Race's track are flyable, and the spawn seat visibly sits
  next to the structure the way it does in the game. Spawn-then-parent-same-frame is the Cell's own
  environment idiom, safe because these spawnables STREAM their prisms (the torus was opted into
  `layAcrossFrames` with this change — a synchronous lay would register its spatial-index poses at
  the world origin and then move 120k). The prisms are instantiated, never pooled, and ride the
  strike's retiring-root drain. What still doesn't exist in previews: CONTROLLER-built structures
  (Astro League's goals, Scarab's hoops) — `StructurePrefab` is the hook; nothing authors one yet,
  and modelling one needs the arena-builder extraction or a renderer-bounds sampler.
- **The planting model shows SEEDS, not the mature forest.** Rampage plants 59 and matures to 88
  (`MaxLivePopulation`); the card shows 59, which is what a match actually opens with. If the
  mature figure ever reads better, it is one field in `ModePreviewPlantingModel.ResolveCount`.
- **Crystal-scored modes now preview WITH crystals** (`ModePreviewArena.SpawnPreviewCrystals`):
  the omni prefab is minted the way the Wanderway conveyor mints it — the prefab carries its own
  impactor + collider and `Crystal`'s manager-less guards make a local mint collectible with no
  `CrystalManager`. A waypoint-track mode (Skim Race) gets one per sampled waypoint; any other
  crystal-scored mode (Scurry) gets a volume-uniform scatter inside the nucleus, per §27's "the
  omni respawn volume IS the nucleus". Registered on the satellite's runtime data so the AI hunts
  them. **Nothing respawns** — a preview is a taste; collected means gone. The prefab reference
  lives on `ModePreviewLibrarySO.OmniCrystalPrefab` (Resources), authored to
  `_Prefabs/Environment/Crystal.prefab`.
- **Tap-in no longer walls the window with "LOADING <MODE>"** — the orbiting scale model stays on
  camera while the real cell builds beside it, and the gameplay camera takes over when ready. The
  label survives only for a tap-in with nothing standing to show.
- **In a party, your vessel visibly teleports** to the arena and flies there; party members keep
  flying the menu world.
- **Not verified in the Editor this round** — same verification debt as before: play-mode over
  every definition, all focus routes, strike-then-reopen cycles, and the pool staying sound.
