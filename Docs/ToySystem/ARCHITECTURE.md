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
| Painting ("fly by numbers") toy | `Assets/_Scripts/Controller/Toys/PaintingToy.cs` |
| Self-contained fly-by-numbers runner | `Assets/_Scripts/Controller/Toys/MenuShapePainter.cs` |
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

### Painting / Fly-by-Numbers (`PaintingToy` + `MenuShapePainter`)
Fly through → starts a self-contained painting run. `MenuShapePainter` reads a
`ShapeDefinition`'s waypoints, draws a ghost outline + a guide line + a lit marker at the next
point, and advances as the vessel flies near each in order — **the vessel's own trail does the
painting**. Deliberately minimal (no Cell, no crystal manager, no scoring, no HUD) so it runs
in the menu where none of that exists. Toy-faithful: completes when the last point is reached,
then fades; no fail state. (The full `ShapeDrawingManager` experience — preview cinematic,
scoring, reveal — remains available for a gameplay scene that has the ecology infra; the toy
uses the lightweight runner so it works everywhere.)

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
