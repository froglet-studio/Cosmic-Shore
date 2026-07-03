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
| **Cells** | toys are placed relative to the Cell membrane (read, never duplicated); Wanderway lifeforms spawn *into* the cell as ordinary citizens |
| **Flora & Fauna / Crystals** | Wanderway meadow/menagerie scenes release flora/fauna through the canonical cell spawn sequences and lay skimmable elemental crystals |

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
- **Exit-gated re-arm** — a toy is *not consumed*, but it re-arms **only once the local vessel
  has flown clear of its trigger volume** (a per-frame distance poll with `exitRadiusMultiplier`
  hysteresis, robust to the swap's despawn/respawn which fires no `OnTriggerExit`). A swap toy
  that flips to the option you just left also **re-grows slowly** (`regrowDuration`, default ~5s)
  and stays inert while it does. Together these stop a toy from immediately switching you back
  before you can escape it. And when any toy in a set fires, the coordinator disarms the **whole
  set** (`Toy.Disarm`) so a vessel that re-spawns on top of a neighbour can't chain-trigger it.
  `Toy` exposes `bloomDuration`, `regrowDuration`, and `exitRadiusMultiplier` as serialized knobs.

## File map

| Role | File |
|---|---|
| Toy base (trigger, bloom, gating, re-arm) | `Assets/_Scripts/Controller/Toys/Toy.cs` |
| Coordinated toy (reports activation to its set) | `Assets/_Scripts/Controller/Toys/SwapToy.cs` |
| Shared "set + flip" coordinator (generic) | `Assets/_Scripts/Controller/Toys/SwapToySetCoordinator.cs` |
| Shared runtime refs handed to each toy | `Assets/_Scripts/Controller/Toys/ToyContext.cs` (`ToyContext` + `ToyPlacement`) |
| Procedural body/label/collider builder | `Assets/_Scripts/Controller/Toys/ToyFactory.cs` |
| Mini vessel model (mesh-extract from prefab) | `Assets/_Scripts/Controller/Toys/VesselModelBuilder.cs` |
| Vessel Changer set | `Assets/_Scripts/Controller/Toys/VesselChangerToySet.cs` |
| Domain Changer set | `Assets/_Scripts/Controller/Toys/DomainChangerToySet.cs` |
| Painting ("fly by numbers") toy | `Assets/_Scripts/Controller/Toys/PaintingToy.cs` |
| Self-contained fly-by-numbers runner | `Assets/_Scripts/Controller/Toys/MenuShapePainter.cs` |
| Conveyor ("Wanderway") toy | `Assets/_Scripts/Controller/Toys/ConveyorToy.cs` |
| Conveyor belt runner | `Assets/_Scripts/Controller/Toys/MicrosceneConveyor.cs` |
| One conveyor scene (lay/transport/re-arrange) | `Assets/_Scripts/Controller/Toys/Microscene.cs` |
| Microscene recipe generators (pure) | `Assets/_Scripts/Controller/Toys/MicroscenePatterns.cs` |
| Placement + lifecycle | `Assets/_Scripts/Controller/Toys/ToyboxController.cs` |
| Per-toy config (abstract) | `Assets/_Scripts/ScriptableObjects/Toys/ToyDefinitionSO.cs` |
| Vessel/Domain/Painting configs | `Assets/_Scripts/ScriptableObjects/Toys/*ToyDefinitionSO.cs` |
| Toybox registry + unlock state | `Assets/_Scripts/ScriptableObjects/Toys/ToyboxSO.cs` |
| One-click editor setup | `Assets/_Scripts/Editor/ToyboxSetupTool.cs` |

## The "swap set" pattern (vessel + domain)

Both the vessel and domain changers are **sets** of toys managed by a shared generic
coordinator, `SwapToySetCoordinator<T>`. The set always shows *the options you are not
currently on* (`universe \ {current}`), each toy visually *being* the option it will switch
you to. Flying through a toy applies the change; the coordinator then **flips the used toy to
the option you just left**, so the set continuously mirrors "everything except where you are
now". Current state is polled each frame, so external changes (a panel, a menu reset) reconcile
the same way. `SwapToy` is the per-option toy; it just reports activation and lets the
coordinator own the option→visual mapping and the flip.

### Vessel Changer (`VesselChangerToySet`)
A **collection** of toys, each a **mini 3D model** of a ship you can switch into (every vessel
in the collection except the one you're flying). The model is built by `VesselModelBuilder`,
which reads mesh data straight off the ship **prefab asset** (never instantiates it, so no
NetworkObject/VesselStatus/controllers ever run). Flying through one swaps you into that ship
via `MenuServerPlayerVesselInitializer.RequestSwap` (the existing networked pipeline), and the
toy flips to a mini model of the ship you just left.

**Mini-model rendering.** `VesselModelBuilder` extracts only the **hull**: it skips builtin-
primitive meshes (the skimmer sphere, scaled 15–60× — it otherwise dominated `NormalizeToRadius`
and crushed the hull to an invisible speck; this is why only Rhino, the one ship whose skimmer
has no builtin sphere, used to render), anything named skimmer/trail/jet/forcefield/crackle/pip/
vfx, and inactive/disabled renderers (read via `activeSelf` up the chain — `activeInHierarchy` is
always false on a prefab asset). Each hull mesh is painted with one **opaque, self-lit preview
material** (`TryBuild(prefab, radius, previewColor, out model)`), because the ship's real hull
material is a transparent, runtime-theme-driven shader that renders dim/invisible at rest. The
`previewColor` is the local player's **domain colour**, so the mini ships preview "you, a
different hull". Vessels whose body isn't statically extractable fall back to the labelled
tinted sphere.

**Recolour on domain change.** The mini ships are tinted to your domain, but `ConfigureVisual`
only runs on create/flip. So the set watches the local player's domain each frame
(`SwapToySetCoordinator.OnTick` → `VesselChangerToySet`) and, on a change, re-tints **every** mini
ship + label in place (`ForEachSlot` → `RecolorSlot`, no rebuild → instant, pop-free) so they all
follow the domain changer, not just the one slot that flips on the next swap.

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
(that's why the old toy left you unable to steer). `VesselChangerToySet.RestoreControlAfterSwap`
waits for `IsSwapping` to clear, then re-hands freestyle control — mirroring
`MenuVesselSelectionPanelController.RestoreFreestyleAfterSwapAsync`.

**HUD after swap.** `VesselController.Initialize` creates every vessel's HUD **hidden**, and the
only menu code that shows the local HUD fires on *entering* freestyle — which a swap doesn't do.
So `ReInitializePair` re-raises `GameDataSO.OnPlayerPairInitialized` (as the initial-spawn path
does) and `MenuMiniGameHUD` re-shows the local HUD on that event while in freestyle (gated on
freestyle + local player). Covers both the toy swap and the vessel-selection panel swap.

Collection defaults to a curated set (Manta, Dolphin, Rhino, Squirrel, Serpent, Sparrow) so
the ring isn't crowded with all 11; override per-asset via `vesselCollection`.

### Domain Changer (`DomainChangerToySet`)
Two toys (in a 3-domain session), each **tinted the domain it will switch you to**
(`ThemeManagerData.GetDomainUIColor`) and labelled with the domain name — always the two
colours you are *not*. Flying through one requests that domain via the server-authoritative
`Player.RequestSetDomain_ServerRpc` (**never** a client-local write — CLAUDE.md), and the toy
flips to the colour you just left.

### Wanderway / Microscene Conveyor (`ConveyorToy` + `MicrosceneConveyor` + `Microscene`)

Fly through → the belt switches **ON** (the toy flips bright + relabels "flowing — fly through
to stop"; another pass switches it off) and a field of **microscenes** blooms in ahead of your
flight path, scene after scene — open-world exploring crossed with an infinite runner. **28
recipes** built from a shared geometry vocabulary (`PrismGeometry`): gate runs, helix weaves,
tunnels, slaloms, starbursts, orchards, meadows, menageries, polygon gates, serpent ribbons,
colonnades, orbitals, canyons, lattices, comet tails, spiral ramps, archways, vortices (converging
lines with an open convergence + an inviting crystal), slot corridors (parallel plates with gaps to
roll through), cube fields, torus gates, pillar halls, turbines, asteroid fields, and living
plains / groves / aviaries / preserves — each re-rolling its own radii/counts/twists/bends on every
arrival, so the same recipe never lands the same way twice. The belt **follows you anywhere at any
speed**: effective spacing = `max(sceneSpacing, speed × minSceneIntervalSeconds)` and lookahead =
`aheadTargetScenes × spacing`, so there is always a field of ~7 structures ahead.

**Geometry vs. theming (why it stays fresh, not chaotic).** A recipe produces pure *shape* only;
`MicroscenePatterns.Finalize` then themes each scene from a config-authored `MicroscenePalette`
(`ConveyorToyDefinitionSO`): a **per-scene domain scheme** (mono / banded-by-structure / accented /
neutral-veined-with-Blue — weighted so most scenes read one coherent colour, never per-prism
confetti; domains read live each draw so the Domain Changer toy takes effect), a sparse **prism-kind
scheme** (mostly plain, with occasional **danger** prisms — the Squirrel danger-skim risk/reward —
and rarer **shielded** / **supershielded** accents, capped for the collider budget), a per-scene
**scale mood** (grand vs. delicate), and a **crystal mix** (mostly elemental skims, occasional
**omni** jackpots — body-collected fuel + speed buff). "Infinitely fresh" is the cross-product of
recipe × domain-scheme × kind-scheme × scale-mood × per-arrival geometry roll; coherence comes from
theming per *scene*, not per prism. Prism lay-down goes through the shared `PrismTrailBuilder` (the
one canonical Instantiate→…→Initialize primitive, also used by the Spawnable environment system).
See `Docs/EnvironmentSpawning/UNIFICATION_ASSESSMENT.md`.

**Placement is a live-course field, not a connected ribbon.** Each scene lands directly on the
player's *current* flight line — `position + course × distanceAhead`, scattered up to
`sceneRadius × pathSpread` laterally so the field has width — with no running "head" the scenes
chain off. The field's reach is measured *along the current course inside a flight corridor*
(`FrontierProgress`), so the moment the player changes direction the old scenes fall off-corridor,
the measured reach collapses, and fresh scenes drop straight into the **new** path from
`firstSceneDistance` outward. Structures appear in front of you shortly after any turn, regardless
of where the belt was pointing — the ribbon does not have to stay connected. Passed scenes and the
now-lateral leftovers of a turn clear (suction) as new ones arrive — spawn frequency IS the clear
frequency, because the pool is finite and **closed**: a reclaimable scene (off the flight corridor,
or dropped far behind) is *suctioned* to a point, relocated onto the new path ahead, re-posed into a
fresh recipe with new domain colour, and *bloomed* back out. Scenes still in the corridor ahead
(what you're flying toward) are never reclaimed. No score, no end condition; every belt advance is
driven by the player's own motion (no timers). Exiting freestyle makes the belt dormant; toggling it
off stops the flow — either way its scenes stay in the world.

**Ecosystem invariants (this toy is ecology-adjacent — all hold by construction):**

- *Continuity of existence* — prisms grow in via their own `PrismScaleAnimator`; crystals
  `FadeIn`; recycling is suction-out → bloom-in (both sanctioned transitions). Nothing pops.
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
  `poolSize × prismBudgetPerScene` prisms (default 10 × 42 = 420 BoxColliders + ≤3 crystal
  triggers per scene + 1 toy trigger, well under the ~1,500/cell target); distant scenes are
  collider-LOD-culled by `PrismColliderLodManager` automatically. **Shielded / supershielded**
  prisms swap their BoxCollider for an always-on convex MeshCollider that LOD can't reclaim, so the
  palette caps them (`MaxShielded = 3`, `MaxSuperShielded = 1` per scene, low scheme weights) —
  worst case ≈ 40 MeshColliders across a full pool, realistic steady state a handful. Danger prisms
  keep the cheap cullable BoxCollider. The belt roams freely — mass
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

### Painting / Fly-by-Numbers (`PaintingToy` + `MenuShapePainter`)
Fly through → starts a self-contained painting run. `MenuShapePainter` reads a
`ShapeDefinition`'s waypoints, draws a ghost outline + a guide line + a lit marker at the next
point, and advances as the vessel flies near each in order — **the vessel's own trail does the
painting**. Deliberately minimal (no Cell, no crystal manager, no scoring, no HUD) so it runs
in the menu where none of that exists. Toy-faithful: completes when the last point is reached,
then fades; no fail state. (The full `ShapeDrawingManager` experience — preview cinematic,
scoring, reveal — remains available for a gameplay scene that has the ecology infra; the toy
uses the lightweight runner so it works everywhere.)

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
2. Add a `ToyDefinitionSO` subclass whose `Spawn(...)` builds it via `ToyFactory`.
3. Add the new definition asset to the `ToyboxSO` (or to `BuildDefaultToybox` for a built-in).

No central switch — definitions are polymorphic factories, so the framework never changes.

## Setup (one step in Unity)

Run **Tools → Cosmic Shore → Setup Freestyle Toybox**. It:

1. authors the four toy definition assets under `Assets/_SO_Assets/Toys/` (the conveyor's
   prism prefab + crystal effect are auto-wired: `SpawnablePrism.prefab` +
   `SkimmerAdjustElementLevelByCrystalEffect.asset`),
2. creates `Assets/Resources/Toybox.asset` and registers them, and
3. adds a `ToyboxController` to Menu_Main (on the `MenuCrystalClickHandler` object) pointing
   at the toybox.

Idempotent — safe to re-run. (Or simply drop a `ToyboxController` on any Menu_Main object and
rely on the runtime default toybox.)

## Networking notes

- Each client runs its own `ToyboxController` and spawns its own local toy GameObjects
  (deterministic placement → they overlap visually across clients). Toys are **local
  interaction stations**, not networked objects; only the *effects* (vessel swap, domain
  change) go over the network, through the existing server-authoritative paths. This matches
  the "local-only freestyle toggle, network-replicated vessel behaviour" model in CLAUDE.md.
- `IsLocalUser` ensures only your own vessel trips your toys; the freestyle gate ensures the
  autopilot lava-lamp vessel never does.

## Status & follow-up

The framework + **four toys** are in (Vessel Changer, Domain Changer, Painting, and the Wanderway
microscene conveyor), plus the vessel-changer second-pass fixes above: mini-model hull rendering,
exit-gated re-arm + slow flip re-grow, swap continuity (domain / pose / speed), recolour-on-domain,
HUD-after-swap, and gamepad-Start / input-ownership. The conveyor has been through two adversarial
review passes (compile, logic, ecology invariants, game-feel, assets, docs). All are
compile-reviewed against the real codebase but **not yet play-verified in an editor** (no Unity in
the authoring environment) — an in-editor pass is the last step before/after merge. Remaining polish
(per-toy tuning, skinned-mesh `BakeMesh` fidelity, painting pen-up, placement anchor, conveyor
recipe/pacing tuning + audio, unlock persistence, tests) is tracked in **`BACKLOG.md`**, grouped so
each area can be its own branch.

### Files touched this pass (for review)

| Area | Files |
|---|---|
| Re-arm / escape | `Controller/Toys/Toy.cs`, `Controller/Toys/SwapToySetCoordinator.cs` |
| Mini-model rendering | `Controller/Toys/VesselModelBuilder.cs`, `Controller/Toys/VesselChangerToySet.cs` |
| Recolour on domain change | `Controller/Toys/SwapToySetCoordinator.cs` (`OnTick`/`ForEachSlot`), `Controller/Toys/VesselChangerToySet.cs` |
| Domain preserved on swap | `Controller/Multiplayer/ClientPlayerVesselInitializer.cs` (`ReInitializePair`) |
| Speed inherited on swap | `Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`, `Controller/Vessel/IVessel.cs`, `Controller/Vessel/VesselController.cs`, `Controller/Vessel/VesselTransformer.cs` |
| HUD re-show after swap | `Controller/Multiplayer/ClientPlayerVesselInitializer.cs`, `UI/MenuMiniGameHUD.cs` |
| Gamepad Start / input ownership | `UI/MenuMiniGameHUD.cs`, `UI/ScreenSwitcher.cs` |
