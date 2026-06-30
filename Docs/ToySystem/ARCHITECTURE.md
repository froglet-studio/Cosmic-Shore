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
| Shared runtime refs handed to each toy | `Assets/_Scripts/Controller/Toys/ToyContext.cs` (`ToyContext` + `ToyPlacement`) |
| Procedural toy GameObject builder | `Assets/_Scripts/Controller/Toys/ToyFactory.cs` |
| Vessel Changer toy | `Assets/_Scripts/Controller/Toys/VesselChangerToy.cs` |
| Domain Changer toy | `Assets/_Scripts/Controller/Toys/DomainChangerToy.cs` |
| Painting ("fly by numbers") toy | `Assets/_Scripts/Controller/Toys/PaintingToy.cs` |
| Painting director (revived glue) | `Assets/_Scripts/Controller/Toys/FreestylePaintingDirector.cs` |
| Placement + lifecycle | `Assets/_Scripts/Controller/Toys/ToyboxController.cs` |
| Per-toy config (abstract) | `Assets/_Scripts/ScriptableObjects/Toys/ToyDefinitionSO.cs` |
| Vessel/Domain/Painting configs | `Assets/_Scripts/ScriptableObjects/Toys/*ToyDefinitionSO.cs` |
| Toybox registry + unlock state | `Assets/_Scripts/ScriptableObjects/Toys/ToyboxSO.cs` |
| One-click editor setup | `Assets/_Scripts/Editor/ToyboxSetupTool.cs` |

## The three toys

### Vessel Changer (`VesselChangerToy`)
Fly through → cycles to the next vessel class via
`MenuServerPlayerVesselInitializer.RequestSwap(next)` — the **same** Netcode despawn/spawn/RPC
pipeline the vessel-selection panel uses, so the change replicates to all clients. The old
freestyle vessel-change lived in the pause menu (which now backs out to the app shell); this
is the in-world replacement. Cycle list defaults to the full playable set (Manta…Sparrow);
override per-asset via `vesselCycle`.

### Domain Changer (`DomainChangerToy`)
Fly through → cycles the local player's domain Jade → Ruby → Gold (within the session's
active-domain slice, `GameDataSO.RequestedDomainCount`). Routes through the
server-authoritative `Player.RequestSetDomain_ServerRpc` — **never** a client-local write —
so the recolour replicates via `Player.NetDomain` (CLAUDE.md "Never write domain state from
client code").

### Painting / Fly-by-Numbers (`PaintingToy` + `FreestylePaintingDirector`)
Revives the existing-but-unwired shape-drawing toy. On activation the toy raises the existing
decoupled `ShapeSignEvents.OnShapeSelected(shape, pos)`. A `FreestylePaintingDirector`
listens and drives `ShapeDrawingManager` through its `StartShapeSequence → (preview) →
BeginDrawing` flow — reviving the glue that the removed `SinglePlayerFreestyleController`
(deleted in commit `2009ae54`) used to provide. Toy-faithful: **no Ready button, no
countdown, no score gate** — after the preview cinematic the drawing simply begins, and when
the player exits the shape the toy re-arms.

> The painting toy needs the heavy shape infrastructure that is **not** in Menu_Main today:
> a `ShapeDrawingManager` (+ its `ShapeDrawingCrystalManager`, `CellRuntimeDataSO`, optional
> `EndShapeDetailHUD`) and a `FreestylePaintingDirector` referencing the manager. All of that
> code exists; only the scene wiring is missing (recoverable from the removed
> `MinigameFreestyle.unity` in git history at `2009ae54^`). Until it is wired, the painting
> toy still spawns and re-arms but its activation is a harmless no-op. The two cyclers work
> with zero additional wiring.

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

1. Add a `Toy` subclass with the behaviour (`OnActivated(IVesselStatus localVessel)`).
2. Add a `ToyDefinitionSO` subclass whose `CreateToy(...)` builds it via `ToyFactory`.
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
