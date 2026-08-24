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

## 1. The flow

```
card selected (SetSelectedGame)
 └─ session.SetDefinition(def, mode's hull)
     ├─ no definition            → window: "LEVEL PREVIEW NOT AVAILABLE"   (~27 modes, honest)
     └─ definition               → window: "LOADING <MODE>…", then automatically:
         ├─ ModePreviewArena.Stand()          satellite cell, 120k units out
         ├─ RequestSwap(mode's hull)          pose / speed / domain preserved
         ├─ vessel.SetPose(arena spawn)       the framing the real mode opens on
         ├─ AIPilot.RetargetCell(arena data)  ← load-bearing, see §3
         ├─ CameraManager.BeginWindowedPlayerCamera → window's RenderTexture
         └─ window.GoLive()                   the AI is flying; "TAP TO PLAY"

tap the window     → AI off, input on, sendNavigationEvents off, AnyHasFocus = true
tap outside /
Escape / Start     → input paused, AI back ON — the preview keeps playing in the window
card change /
modal close /
launch             → Stop(): camera back, AI retarget restored, vessel home, arena struck (§4)
```

The objective runner (the mode's own `ScoringMetric`, baselined) starts counting at the player's
**first take-over** — the AI's flight is a demo, not their progress. Completing it tears nothing
down; it just stops counting.

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

## 5. The pieces

| Piece | Location | Job |
|---|---|---|
| `ModePreviewDefinitionSO` | `_Scripts/ScriptableObjects/` | Per-mode: cell, optional structure prop, vessel, objective, duration, spawn standoff |
| `ModePreviewLibrarySO` | `_Scripts/ScriptableObjects/` | Mode → definition. `Resources/ModePreviewLibrary`. Tournament excluded in code |
| `ModePreviewWindow` | `_Scripts/UI/View/` | Three states (unavailable / loading / live), the focus interaction, the RenderTexture, static `AnyHasFocus` |
| `ModePreviewSession` | `_Scripts/Controller/Arcade/Preview/` | Auto-start driver, vessel + AI bookkeeping, the camera loan, the strike |
| `ModePreviewArena` | `_Scripts/Controller/Arcade/Preview/` | Stand / BeginStrike / FinishStrike |
| `ModePreviewRunner` / `ModePreviewHUD` | preview dir / `UI/View/` | Objective counting from first take-over; readout beside the window |
| `Cell.InitializeSatellite` / `StrikeSatelliteWorld` | `Controller/Environment/` | The platform capability pair |
| `CameraManager.BeginWindowedPlayerCamera` | `Controller/Managers/` | The real gameplay rig → a RenderTexture, additively (never `SetActiveCamera`) |
| `ModePreviewSetupTool` | `_Scripts/Editor/` | `FrogletTools > Scene Setup > Setup Mode Preview` — also the MIGRATION off earlier revisions (deletes TestFlightButton / FocusFrame / ExitButton / legacy video instances) |

## 6. Shipped definitions (8)

| Mode | Cell | Objective | Notes |
|---|---|---|---|
| Rampage | Rampage 1 | 150 prisms / 90s | Grown forest — needs the satellite bootstrap fix |
| Ribcage | Ribcage 1 | 200 prisms / 90s | No nucleus → spawn 500 clears the 360u cage |
| Wildlife Liberation | WL 1 | 5 kills / 90s | Spawn 1200, outside the outer cage |
| Dog Fight | Boneyard 1 | open-ended / 60s | Solo has nobody to shoot; teaches the arena |
| Scarab Scramble | Scarab | open-ended / 60s | Hoops are controller-built; see §7 |
| The Bends | Rampage 1 | open-ended / 60s | Same arena as the mode itself reuses; Dolphin pinned |
| Nucleus Rush | Nucleus Rush | open-ended / 60s | The cell IS the loop (nucleus claim + fauna waves) |
| Astro League | Astro League | open-ended / 60s | Goals/ball are controller-built; Rhino pinned |

Maelstrom (Tournament) is excluded **in code** — it draws other modes; it has no arena to shrink.
Every other mode without a definition shows the label.

## 7. Known limitations

- **A satellite doubles the live ecology while a card with a preview is selected**, and browsing
  cards stands/strikes an arena per selection (plus a networked hull swap for vessel-locked
  modes). This is the cost of "a game already playing in the window". If browsing thrash shows up
  in profiling, debounce the auto-start by a second or two.
- **Controller-built structures (hoops, goals, tracks) don't exist in previews** —
  `StructurePrefab` is the hook; nothing authors one yet. The real fix is extracting those arena
  builders so mode and preview call the same code.
- **The satellite has no crystals** (`CrystalManager` is scene-level), so the AI seeks the cell
  centre when the arena offers no items, and crystal-driven modes preview without their pickups.
  Wiring a satellite-local crystal source is the single highest-value follow-up.
- **In a party, your vessel visibly teleports** to the arena and flies there; party members keep
  flying the menu world.
- **Not verified in the Editor this round** — same verification debt as before: play-mode over
  every definition, all focus routes, strike-then-reopen cycles, and the pool staying sound.
