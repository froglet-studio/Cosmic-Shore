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
| `ModePreviewArena` | `_Scripts/Controller/Arcade/Preview/` | Stand / BeginStrike / FinishStrike |
| `ModePreviewRunner` / `ModePreviewHUD` | preview dir / `UI/View/` | Objective counting from first take-over; readout beside the window |
| `Cell.InitializeSatellite` / `StrikeSatelliteWorld` | `Controller/Environment/` | The platform capability pair |
| `CameraManager.BeginWindowedPlayerCamera` | `Controller/Managers/` | The real gameplay rig → a RenderTexture, additively (never `SetActiveCamera`) |
| `ModePreviewSetupTool` | `_Scripts/Editor/` | `FrogletTools > Scene Setup > Setup Mode Preview` — also the MIGRATION off earlier revisions (deletes TestFlightButton / FocusFrame / ExitButton / legacy video instances) |

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
