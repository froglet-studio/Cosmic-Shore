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
| **Prisms / Mass** | the Painting toy lays a *conserved-mass* prism pattern (no caps/TTLs) |
| **Cells** | toys are placed relative to the Cell membrane (read, never duplicated) |

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
- **Re-arm on exit** — a toy is *not consumed*; after the vessel leaves it re-arms, so you
  can play with it indefinitely.

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
| Painting station (one per painting) | `Assets/_Scripts/Controller/Toys/PaintingToy.cs` |
| Multi-stroke fly-by-numbers runner | `Assets/_Scripts/Controller/Toys/PaintingRunner.cs` |
| Painting data (strokes + domains) | `Assets/_Scripts/ScriptableObjects/Toys/PaintingDefinitionSO.cs` |
| Preset generators (Star…Taj Mahal) | `Assets/_Scripts/Controller/Toys/PaintingPresetLibrary.cs` |
| Painting progress persistence | `Assets/_Scripts/Controller/Toys/PaintingProgressStore.cs` |
| Drawing state (per-prism pose/domain) | `Assets/_Scripts/Controller/Toys/PaintingPrismStore.cs` |
| Web share export (inline-WebGL viewer) | `Assets/_Scripts/Controller/Toys/PaintingShareExporter.cs` |
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

**Lost-control fix:** the swap pipeline drops the new vessel into autopilot with input paused
(that's why the old toy left you unable to steer). `VesselChangerToySet.RestoreControlAfterSwap`
waits for `IsSwapping` to clear, then re-hands freestyle control — mirroring
`MenuVesselSelectionPanelController.RestoreFreestyleAfterSwapAsync`.

Collection defaults to a curated set (Manta, Dolphin, Rhino, Squirrel, Serpent, Sparrow) so
the ring isn't crowded with all 11; override per-asset via `vesselCollection`.

### Domain Changer (`DomainChangerToySet`)
Two toys (in a 3-domain session), each **tinted the domain it will switch you to**
(`ThemeManagerData.GetDomainUIColor`) and labelled with the domain name — always the two
colours you are *not*. Flying through one requests that domain via the server-authoritative
`Player.RequestSetDomain_ServerRpc` (**never** a client-local write — CLAUDE.md), and the toy
flips to the colour you just left.

### Painting / Fly-by-Numbers (`PaintingToy` + `PaintingRunner`)

The painting toy is a **gallery**: `PaintingToyDefinitionSO` spawns one `PaintingToy` station per
`PaintingDefinitionSO`, fanned around its ring slot, each labelled with the painting's name and
live progress. A painting is **multi-stroke and multi-domain** — a list of `PaintingStroke`s
(name, domain, ordered 3D points) flown in author order — and it stands as a fixed, upright
**monument-in-progress** anchored just outside the toy ring (front facing the ring), not a
billboard that follows the vessel. The ladder of built-in presets (`PaintingPresetLibrary`):

| Painting | Size | Strokes | Domains | What it teaches |
|---|---|---|---|---|
| Star | 420 | 1 | Gold | the basic trace, big enough to feel real |
| Rainbow | 700 | 3 | all three | the domain gates, one band per colour |
| Saturn | 800 | 3 | all three | genuinely 3D flying (tilted rings) |
| **Taj Mahal** | 1100 | ~55 | all three | the monument: plinth, chamfered body, grand iwan + niches, onion-dome rib cage, 4 chhatris, 4 minarets with balconies, jade reflecting pool + charbagh — hours of flying |

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
- **Guide + marker.** A guide line runs from the vessel to the next point; a pulsing marker
  sits on it. The advance threshold tightens automatically on fine-detail strokes (minaret
  balconies) so tight loops must actually be flown.
- **Progress, pause, resume.** Progress is stroke-granular. Re-flying the station benches /
  resumes the run ("put the brush down"); progress also persists across sessions
  (`PaintingProgressStore`, the FavoriteSystem `DataAccessor` JSON pattern — completed strokes
  re-render as dim ghosts after a restart since prisms live only as long as the scene). After
  the completion celebration, flying the station again clears the canvas for a repaint.
- **Toy-faithful.** No score, no timer, no fail state — only progress. Solo-painting only: the
  runner tracks the *local* player; party members see the painted prisms replicate but not
  your gates/ghosts (same local-station model as every toy).

(The full `ShapeDrawingManager` experience — preview cinematic, scoring, reveal — remains
available for gameplay scenes; any existing `ShapeDefinition` can also become a painting via
`PaintingDefinitionSO.sourceShape`, which splits pen-up gaps into strokes.)

#### Shape language — one vocabulary of interactables

Toys teach each other by recycling shapes (mindshare recycling): every interactable that does
the same *kind* of thing wears the same form, in the domain's **prism material** (the exact
shader the painted trail wears — `ToyFactory.DomainPrismMaterial` →
`ThemeManagerDataContainerSO.GetTeamBlockMaterial`).

| Shape | Meaning | Where |
|---|---|---|
| **Cone** (apex = "this way next") | *turns / keeps your trail ON* | stroke-gate hubs, every intermediate stroke point (apex points at the stroke's next point), and the **Domain Changer** bodies (apex points the way you fly through) |
| **Jack** (three rods through a centre) | *turns your trail OFF* | each stroke's final point (reaching it ends the stroke and pens up) |
| **Ring** (fly-through portal) | *crossing commits a choice* | stroke start gates, the SHARE/REPAINT completion gates |

The domain changer and the painting gates deliberately share the cone so meeting either one
first sets up expectations for the other. Builders live in `ToyFactory` (`AddConeBody`,
`AddJackBody`, `AddRingBody`).

#### Authoring rule — order strokes by decreasing radius of curvature

Strokes are flown in author order, so **sequence them from the broadest curvature to the
tightest**: long straight/broad strokes first (pool outlines, plinth rectangles), fine detail
last (balcony rings, crescents). The painting then doubles as its own difficulty ramp — the
player warms up on sweeping lines and earns the precision work — and the adaptive per-stroke
reach (which tightens on short segments) ramps with them. The Taj Mahal preset is the
reference: pools → plinth → body → arches → dome → chhatris → minaret balconies. Batch
domains at meaningful architectural boundaries within that ordering (see
`PaintingPresetLibrary`).

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

1. authors the three toy definition assets under `Assets/_SO_Assets/Toys/`,
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

The framework + three toys are in and compile-reviewed; they are **not yet play-verified in an
editor**. Polish/improvement work (per-toy tuning, mini-model materials, painting pen-up,
placement anchor, unlock persistence, tests) is tracked in **`BACKLOG.md`**, grouped so each area
can be its own branch.
