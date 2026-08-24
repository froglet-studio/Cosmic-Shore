# Mode Preview — the window the game plays in

Replaces the arcade card's pre-rendered preview video with a window that is a **live scale model
of the arena the mode actually builds** while you browse, and **the mode itself, playing**, once
you click into it. It never leaves the modal and never changes size.

---

## 0. What was there, and what actually changed

`SO_Game.PreviewClip` is a `VideoPlayer` **prefab** reference, instantiated by
`ArcadeGameConfigureModal.InitializeGameMetaView` into `selectedGamePreviewWindow`. So the preview
was never on the card face — the grid paints a static `CardBackground` sprite and only the
*selected* game shows a clip. **One live preview at a time is all this feature ever needs**, which
is what makes a real-time diorama affordable at all.

That path is not deleted. A mode with no `ModePreviewDefinitionSO` still plays its video, so this
rolls out one mode at a time rather than as a flag day — and it has to, because the arcade lists
**42** `SO_ArcadeGame` assets while only ~15 have a scene on disk.

---

## 1. The shape, and why it is this shape

The window has two states and **never changes size in either of them**:

1. **Idle** — a slowly turning scale model of the arena the mode actually builds. Cheap; this is
   what you see while browsing cards.
2. **Live** — the real gameplay camera, following the real vessel in the mode's own arena,
   rendered into that same frame. The game is simply playing in there.

**Clicking the window gives it FOCUS.** Input moves from the UI to the vessel. Clicking away, or
Cancel / Escape, moves it back. The window does not grow, the modal does not close, and the menu
scene behind it never changes. It is a focusable widget that happens to contain a game.

Three things follow from that, and each is a constraint the code obeys rather than a preference:

**Input ownership is exclusive, so focus has to be explicit.** `ScreenSwitcher` already kills
`EventSystem.sendNavigationEvents` when the pad is flying a ship; a live vessel behind an
interactive card grid would be two consumers of one stick. Focus is that same handoff — AI off,
input unpaused, navigation off — **without** the fades, the camera blend or the `MainMenuState`
change, because none of those belong in a window.

**The menu world must survive untouched, so the arena is a SATELLITE, not a swap.**
`Cell.RequestCellSwap` would replace the menu world — cheap on colliders, but it changes the scene,
which is exactly what this must not do. Instead `Cell.InitializeSatellite` stands a second,
fully-isolated cell up 120,000 units away. The menu's Lattice cell keeps its own volume ladder, its
own spawner and its own bookkeeping; the satellite gets its own of each.

**There is exactly one local pilot, so the preview flies the player's OWN vessel.** The occlusion
corridor and the speed tunnel are single-writer globals bound to `IsLocalPilot`; a second vessel
would be a second local pilot, which the platform does not support. The menu vessel is relocated to
the arena for the duration and put back afterwards — so the lava lamp has no player ship in it
while you are previewing, which is fine, because the ship is the thing you are watching.

---

## 2. The pieces

| Piece | Location | Job |
|---|---|---|
| `ModePreviewDefinitionSO` | `_Scripts/ScriptableObjects/` | Per-mode: preview cell, optional structure prop, vessel, objective metric/target, duration, spawn standoff, idle-model settings |
| `ModePreviewLibrarySO` | `_Scripts/ScriptableObjects/` | Mode → definition lookup. `Resources/ModePreviewLibrary`. Excludes Tournament in code |
| `ModePreviewWindow` | `_Scripts/UI/View/` | The two-state window and the focus interaction. Owns the RenderTexture |
| `ModePreviewSession` | `_Scripts/Controller/Arcade/Preview/` | Stands the arena, parks the vessel, lends the camera, routes focus, and stops |
| `ModePreviewArena` | `_Scripts/Controller/Arcade/Preview/` | The satellite cell: stand and strike |
| `ModePreviewRunner` | `_Scripts/Controller/Arcade/Preview/` | Watches one stat and a clock. Plain MonoBehaviour |
| `ModePreviewHUD` | `_Scripts/UI/View/` | Objective, progress, timer, exit — beside the window, never over the screen |
| `ModePreviewSetupTool` | `_Scripts/Editor/` | `FrogletTools > Scene Setup > Setup Mode Preview` |
| `Cell.InitializeSatellite` | `_Scripts/Controller/Environment/` | The platform capability this needed: a second cell beside a running one |

Assets: `Assets/_SO_Assets/Mode Previews/`, `Assets/Resources/ModePreviewLibrary.asset`.

---

## 3. The idle model

`CellMiniatureBuilder` — the same builder the Cell Selector toy uses — strides the environment
generator's own output into **one mesh with a submesh per domain**, spawning **no prisms**.

Three rules keep it off the frame budget:

1. **A private layer.** The idle stage lives on `ModePreview` (layer 19) and its camera's
   `cullingMask` is *that layer alone*, so it never renders the menu world. Without the layer the
   idle view would be a second full pass over the Lattice cell — so `EnsureStage` **fails loud and
   refuses to render** rather than falling back to `Everything`.
2. **Distance.** The stage sits 50,000 units up, beyond every gameplay camera's far clip (8,000 in
   Menu_Main).
3. **Lifetime.** The idle camera is enabled only while the window is idle and shown; it is switched
   off the moment the window goes live or hides.

The stage light also carries a `cullingMask` — lights ignore layers unless told to.

---

## 4. Going live

```
click the window
 └─ ModePreviewWindow.OnFocusRequested
     └─ ModePreviewSession.StartArena
         ├─ ModePreviewArena.Stand(...)              satellite cell, 120k units out
         ├─ RequestSwap(mode's hull)                 pose / speed / domain preserved
         ├─ wait for the world to finish building    no veil - a satellite never holds the screen
         ├─ vessel.SetPose(arena.SpawnPose)          the framing the real mode opens on
         ├─ CameraManager.BeginWindowedPlayerCamera  the REAL gameplay rig → the window's texture
         ├─ window.GoLive()                          same frame, same size, different source
         └─ TakeFocus()                              AI off, input on, UI navigation off
```

Releasing focus does **not** strike the arena — clicking back in is instant, which is why it is
kept standing. The arena is struck when the card changes, the modal closes, a game launches, or the
session is torn down.

### 4.1 The camera

`CameraManager.BeginWindowedPlayerCamera` is deliberately **not** `SetupGamePlayCameras`: that
routes through `SetActiveCamera`, which deactivates every other managed camera and claims
`_activeController`. A windowed camera is an *additional* view — the menu keeps whatever camera is
drawing the screen and nothing else's idea of "the active camera" changes. Using the real gameplay
rig is what makes the window show the real game: the occlusion corridor and the speed tunnel are
already bound to it.

`EndWindowedPlayerCamera` only stands the rig down when it is not the active controller, so a
preview can never switch off the screen camera in a gameplay scene.

### 4.2 The satellite cell

`Cell.InitializeSatellite` is the platform capability this feature needed. Everything that makes two
cells safe together was already per-instance — the volume summation id, the spatial-index bindings
keyed by it, the block/domain/fauna books, every lattice colony frontier. What is **not**
per-instance is `CellRuntimeDataSO`, a shared *asset*, so a satellite is handed its own instance.

Two ordering rules are load-bearing:

- **Bind the runtime while the cell is still INACTIVE.** `Cell.OnEnable` clears `runtime.Config` to
  stop a stale config leaking across a scene load — so a satellite instantiated straight from the
  prefab would wipe the config out from under the live menu cell using the same asset.
  `ModePreviewArena` instantiates under an inactive root, binds, then activates.
- **A satellite never raises the `EnvironmentLoadVeil`.** The veil is a full-screen hold, and a
  satellite builds beside a scene the player is still using.

### 4.3 One way out

Every route — releasing focus, changing card, closing the modal, launching the real game, leaving
the menu, teardown — funnels through `ModePreviewSession.Stop`, which is a no-op while idle or
already striking. `AbortHard` is the teardown-only variant that drops everything without unwinding.

### 4.4 The invariants it respects

- **Local only.** No `NetworkObject` is created; `ModePreviewArena.SpawnStructure` refuses a prefab
  that carries one, loudly. `GameDataSO`'s launch fields are never written.
- **Mass is conserved.** The arena is created by an explicit player action and struck by one — the
  same event class as a cell swap or a scene load (`Docs/ECOSYSTEM.md §19`). Nothing is on a clock;
  nothing is culled. The objective's duration ends the **counting**, not the world.
- **Collider budget.** This is the expensive half and it is deliberate: a satellite pays for a
  second cell on top of the menu's, where a swap would have kept it flat. The trade buys "the menu
  never changes". Keep preview cells to their lightest authored intensity.
- **The objective reads the mode's own metric** through `ScoringMetrics.Read`, relative to a
  baseline taken when the preview starts.

---

## 5. Authoring a preview

1. Create a `ModePreviewDefinitionSO` (`ScriptableObjects > Game > Mode Preview`).
2. Point `PreviewCell` at **the mode's own shipped `CellConfigDataSO`**. That is the default and it
   is why most modes need no new assets.
3. Leave `Vessel` at `Any` — a vessel-locked mode already declares its hull on its `SO_ArcadeGame`,
   and the modal passes it down.
4. Pick an objective metric the mode already scores on, and a **small** target. This is a taste.
5. Set `SpawnDistanceOutsideNucleus`. **A cell with no nucleus reports radius 0**, so this field
   carries the whole standoff — Ribcage and the Boneyard both need a large value or the preview
   opens *inside* the arena.
6. Add it to `Resources/ModePreviewLibrary`.
7. Run `FrogletTools > Scene Setup > Setup Mode Preview` if the wiring is not already in place.

### 5.1 Shipped definitions

| Mode | Cell | Objective | Diorama | Notes |
|---|---|---|---|---|
| Rampage | Rampage Cell Config 1 | 150 prisms destroyed / 90 s | ✗ | Arena is GROWN flora — see §5.2 |
| Ribcage | Ribcage Cell Config 1 | 200 prisms destroyed / 90 s | ✓ | Spawn 500 — no nucleus, cage outer radius 360 |
| Wildlife Liberation | WL Cell Config 1 | 5 lifeforms killed / 90 s | ✓ | Spawn 1200 — outside the 1050 outer cage |
| Dog Fight | Boneyard Cell Config 1 | open-ended / 60 s | ✓ | Scores on gunnery; solo has nobody to shoot |
| Scarab Scramble | Scarab Scramble Cell Config | open-ended / 60 s | ✗ | Court IS the nucleus; hoops controller-built, see §6 |

### 5.2 A grown arena has no diorama, and that is not a gap

`CellMiniatureBuilder` samples a **generator's** output, so a cell only has a scale model if it
authors an `EnvironmentPrefab`. Two of the five shipped modes do not: **Rampage**'s arena is a
flora forest seeded by its `SpawnProfile`, and **Scarab Scramble**'s court *is* the nucleus.
`ModePreviewDefinitionSO.HasDiorama` is the gate, and the modal falls those modes back to the
legacy video while **still** offering a Test Flight — which is the right split, because the
flight shows a grown arena perfectly and only the static model cannot. Taking the diorama branch
for them would render an empty black window, which reads as broken rather than as absent.

Modelling grown mass (flora populations, the nucleus) is the obvious extension, and it is the
same question the Cell Selector's mini-cells already have.

**Maelstrom (Tournament) is excluded in code**, not by omission: it draws *other* modes per round,
so it has no arena of its own to shrink.

---

## 6. Known limitations

- **A satellite cell doubles the live ecology while a preview is standing.** That is the price of
  "the menu never changes" and it is the one hard number to watch
  (`Docs/ECOSYSTEM_MASTERPLAN.md §4`): Menu_Main already boots the Lattice cell, the heaviest in
  the game. Prefer the lightest authored intensity for a preview cell, and if a mode's arena is too
  heavy, author a lighter variant and **re-measure its `PhaseThresholds`**
  (`FrogletTools > Ecology > Measure Cell Environment Baselines`) — a small world inheriting a big
  world's volume ladder pins at Frenzy immediately. `SpawnProfileSO.FloraPopulationScale` /
  `FaunaPopulationScale` / `FloraPlantBudgetScale` scale a forest without forking per-species assets.
- **Two cameras render while a preview is live.** The menu camera draws the screen and the gameplay
  rig draws the window. The window's texture is small, but both cameras still cull.
- **The vessel swap is a networked round-trip.** Clicking into a vessel-locked mode despawns and
  respawns the player's hull, and leaving swaps it back. Party members see it.
- **If the active menu camera rig frames the VESSEL rather than the cell, the screen follows the
  ship out to the arena.** Menu_Main's lava-lamp rig frames the cell (`MenuCameraRigKind.LavaLamp`),
  so this does not bite today — but an orbit/trail/chase/top-down menu config would.
- **A mode whose gameplay structure is built by its controller previews as an empty arena.**
  Scarab's hoops, Astro League's goals and HexRace's track are built by the controller from a
  settings SO with `NetworkVariable`s, not by the cell. `StructurePrefab` is the hook for a local,
  `NetworkObject`-free stand-in; nothing authors one yet. The better fix is extracting those arena
  builders so the controller and the preview call the same code.
- **A stat channel that only fires in a real match reads 0 here.** The runner treats that as a
  flight with no counter rather than as an error.
- **The generated UI is functional, not designed.** The setup tool places a focus frame, a hint and
  a HUD panel at sane coordinates; all three want a layout pass.
- **Not verified in the Editor.** See §7.

## 7. Verification still owed

- `/verify-unity` (Editor compile + load) — not run; no Unity in the authoring environment. The C#
  is Roslyn syntax-clean and `Tools/CI/validate_project.py` passes, which is not the same thing.
- **The satellite cell is the highest-risk piece.** Confirm the menu cell keeps its config and its
  world when a preview stands and strikes (the inactive-bind ordering in §4.2 is what protects it),
  and that striking leaves no orphaned prisms, crystals or lifeforms behind.
- Play-mode: for each shipped mode, confirm the idle model renders and turns; click in and confirm
  the window goes live **without resizing**, input reaches the vessel, and the arcade grid stops
  taking the stick; click away / Escape / B and confirm input returns.
- Confirm the main camera never shows the idle stage or the satellite arena.
- Frame cost with a preview live, on the Lattice cell, on a mobile target — two cameras and two
  cells is the worst case this feature has.
