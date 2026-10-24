# Match replays

Player-facing replays of a whole match: recorded automatically, kept on the device, listed from any device, and
watched with the Theater's free / pilot / orbit / chase cameras. **Built on Will Bogen's Theater** (`Docs/THEATER.md`),
which already records every vessel and plays the fleet back as ghosts. This layer decides *when* a match is recorded,
*where* it is kept, and *what* the browser lists; it does not re-implement recording or playback.

Design record with diagrams: https://claude.ai/artifact/4Ykh9qZEdf81cY6hPZonaK (private to the owner until shared).

---

## 1. Decisions (2026-10-15)

| Question | Decision | Why |
|---|---|---|
| Record inputs (like Overwatch 2) or outcomes? | **Outcomes** - poses, never inputs | Flight integrates in `Update`, gameplay rolls the global `Random`, AI paces with coroutines: re-running inputs drifts. `Docs/THEATER.md` §1 makes the same call; do not revisit it. |
| Build a recorder, or use the Theater's? | **The Theater's**, unchanged | Will's recorder, format, ghosts and cameras already exist; a second recorder would be a fork. |
| Which mode first? | **Regatta** (`GameModes.Regatta = 56`) | Its course is authored on the arena, so a replay needs no seed; the AI-training recorder already reads its rails. |
| One file format or two? | **Two** | Frames stay in the Theater's compact binary `.cstheater`; a small JSON summary sits beside it so the browser and Cloud Save never open the frames. |
| Where do replays live? | **On the device, summaries in Cloud Save** | Frames are megabytes; a summary is about a kilobyte. A match recorded on another device can be listed, not watched. |
| Share the AI-training recorder's sampling? | **Yes, in principle** | `HumanFlightRecorder` (local pilot's sticks, 10 Hz, editor/dev only) and the Theater recorder (every hull's pose, 30 Hz, ships) still sample separately; folding the first onto the second is a later step (`Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md` says extend, do not fork). |
| AstroLeague's goal replay? | **Moved onto the Theater's pose and interpolation** | One definition of "where was it between two samples" for every replay in the game. |
| Command pattern? | **For playback controls**, not for recording | Every viewer control is an `IReplayCommand` (section 6); recording inputs is ruled out above. |

## 2. How a match is recorded

```
Go (OnMiniGameTurnStarted)          mode listed in ReplayConfigSO?  no -> nothing
   |                                               | yes
   v                                               v
TheaterRecorder.Begin  ---- every frame ---->  TheaterRecorder.Tick  (every hull's pose at TheaterConfigSO.vesselSampleHz)
   |
turn ends (OnMiniGameTurnEnd)  -> recorder stops, the replay waits for its results
   |
end screen (OnShowGameEndScreen) -> results copied, files written, oldest pruned, summary mirrored to Cloud Save
   |  (or: ResultsWaitSeconds pass, the player leaves, the app closes -> saved WITHOUT results, EndedBecause says why)
```

`ReplayService` (`Assets/_Scripts/Utility/Theater/Replays/ReplayService.cs`) does all of this from `GameDataSO`'s SOAP
events. It is a lazy DI singleton registered in `AppManager.InstallBindings` and resolved at boot by an `[Inject]` field
on `AppManager`, the same way `AnalyticsServiceFacade` is, because nothing else asks for it until the browser does.

**Every peer records its own replay.** Poses already replicate, so nothing extra crosses the network. A guest's remote
hulls are the interpolated poses it saw.

## 3. Files

Replays land in the Theater's own output folder, `TheaterConfigSO.ResolveOutputFolder()`: `<repo>/Recordings` in the
editor (git-ignored, never pushed), `<persistentDataPath>/Recordings` in a player build. That is deliberate: the Theater's
play key (**8**) plays the newest file, so a replay can be watched today with no browser.

| File | Written by | What |
|---|---|---|
| `Regatta_2026-10-15_20-00.cstheater` | `ReplayStore.TrySave` via `TheaterRecording.Write` | The frames. Will's format: 36 B per pose, chunked, unknown streams skipped. |
| `Regatta_2026-10-15_20-00.replay.json` | `ReplayStore.TrySave` | `ReplaySummary`, schema `cosmic-shore.replay-summary/1`: mode, scene, intensity, start time, duration, pilots (name, domain, hull, AI, local), winner, results, how it ended. |

Rules the store keeps (all tested in `ReplayStoreTests`):

- **The summary is what makes a file a replay.** It is written last; listing and pruning work only from summaries, so a
  recording made by hand with the Theater's 9 key is never pruned.
- **Two replays in one minute both survive** (`_2`, `_3`) instead of one overwriting the other.
- **A summary may only name a bare file name.** The name is joined to the folder to load and to delete, so `../` or a
  rooted path is refused.
- **The newest `ReplayConfigSO.KeepNewest` (20) are kept.**

## 4. Cloud Save index

`ReplayIndexRepository` (key `REPLAY_INDEX`) holds the newest `ReplayConfigSO.CloudIndexSize` (50) summaries, never
frames. `ReplayService` owns it and loads it on first use; `CloudDataRepository` already refuses to upload over a record
it failed to load (BH-2.1), so an offline match cannot wipe another device's list. `ReplayService.ListFromOtherDevicesAsync`
returns the summaries that are not on this device.

## 5. Config

`ReplayConfigSO` (`Resources/ReplayConfig`, optional; defaults work with nothing authored):

| Field | Default | Meaning |
|---|---|---|
| Auto Record Modes | Regatta | Modes recorded from Go to the end screen |
| Results Wait Seconds | 20 | Real-time wait for the end screen before saving without results |
| Keep Newest | 20 | Replays kept on the device |
| Cloud Index Size | 50 | Summaries mirrored to Cloud Save (0 = off) |
| Nudge Seconds | 5 | How far the skip controls jump |
| Speed Step | 2 | Faster multiplies the playback rate by this, Slower divides |
| Min Speed / Max Speed | 0.25 / 4 | The playback-rate limits |
| Stage Distance | 120000 | How far from the menu world the replay's arena is parked (on -Z; the mode preview uses +X) |
| Arena Prism Stride | 1 | Every Nth prism of the arena's rails laid (1 = as the match laid them; the preview flies at 4) |
| Arena Build Timeout Seconds | 45 | Real-time wait for the arena; after it the replay plays on the dark stage |
| Arena Drain Prisms Per Frame | 150 | Prisms destroyed per frame while the arena comes down after watching |

Sample rate, length cap, ghosts and cameras stay on `TheaterConfigSO`.

## 6. The browser and watching (2026-10-24)

```
Arcade header  [REPLAYS] [B BACK]          OpenModalButton (target REPLAYS = 19) -> ScreenSwitcher.OpenModal
      |
ReplayBrowserModal  (ModalWindowManager, ModalWindows.REPLAYS)
      |  on open: ReplayService.ListOnDevice()  -> ReplayRowView per replay: WATCH, DELETE (asks twice)
      |           ReplayService.ListFromOtherDevicesAsync() -> rows with "Recorded on another device", no buttons
      |  WATCH:   ReplayService.TryLoad -> Instantiate(ReplayPlaybackHud) -> hud.PlayAsync(recording, summary)
      v
ReplayPlaybackHud  (its own root canvas, sorting 500; opens on its "Building the arena" panel, CANCEL / B)
      |  HUD buttons, pad, keys  --build-->  IReplayCommand  --Execute-->  IReplayTransport
      v                                                                   |
ReplayPlayer : IReplayTransport  -------------------------------------------
      |  1. ReplayArenaStage.StandAsync: the mode's own arena as a satellite cell at -Z 120000 (ModePreviewArena.Stand
      |     with the config the match ran, every prism), plus a gate race's rings (ModePreviewGateCourse). Waits for it.
      |  2. ReplayStagePlacement.Shift: every pose += stage origin - the summary's ArenaOrigin
      |  3. TheaterPlayback (Will's ghosts, stage, four shots - unchanged), on a runtime COPY of TheaterConfigSO:
      |     hideGameplayUI off (the curtain does it and keeps the EventSystem); hideWorld off on the arena, on when
      |     no arena stood and the replay was recorded in another scene (the Theater's dark stage)
      |  ReplayCurtain: hides every other root screen-space canvas and every other base camera; puts them back
      v
Stop: TheaterPlayback.Stop -> (from the menu) CameraManager.SetMainMenuCameraActive -> curtain down -> HUD destroyed,
      focus back on the row's WATCH -> the arena struck pool-safely, 150 prisms a frame, out of sight
```

| Piece | File | Notes |
|---|---|---|
| Replays button | `Tools/Build/author_menu_arcade.py` (`replays_button`) | Left of Back in the Arcade header; `OpenModalButton` names the window by TYPE, like `MenuHubButton`. |
| Browser window | `Assets/_Scripts/UI/Modals/ReplayBrowserModal.cs`, `ReplayBrowserModal.prefab` | B closes it back to the Arcade, except while a replay plays and on the frame one ends. The right stick scrolls the list. |
| One row | `Assets/_Scripts/UI/Elements/Replays/ReplayRowView.cs`, `ReplayRow.prefab` | Mode name from the injected `SO_GameList` (Regatta is an Arena card), date in the invariant culture (the font is ASCII-only). |
| Playback HUD | `Assets/_Scripts/UI/Elements/Replays/ReplayPlaybackHud.cs`, `ReplayPlaybackHud.prefab` | Title, when, scrub bar, time, speed, shot, pilot, the transport row, Exit, and a line naming the pad controls. |
| Commands | `Assets/_Scripts/Utility/Theater/Replays/ReplayCommands.cs` | `IReplayTransport` (the receiver), `IReplayCommand`, `ReplayCommands` (TogglePause, Nudge, SeekFraction, ScaleSpeed, SetShot, CycleShot, CycleFollow, Stop). Tested without a scene in `ReplayCommandTests`. |
| Player | `ReplayPlayer.cs`, `ReplayCurtain.cs` (same folder) | The only code that touches the Theater, the cameras and the canvases while watching. |
| The arena | `ReplayArenaStage.cs`, `ReplayStagePlacement.cs` (same folder) | Stands the mode's arena on the mode preview's satellite (section 6.1); shifts the poses onto it. The shift is tested in `ReplayStagePlacementTests`. |
| Generator | `Tools/Build/author_menu_replays.py` (`--check`, `--self-test`) | The three prefabs and the Menu_Main instance (just before Credits; it and `author_menu_credits.py` run in either order). |

**Controls.** The sticks fly the free and subject cameras, so the HUD's buttons are not navigation stops and the pad
drives the replay through shortcuts; the HUD clears the EventSystem's selection every frame so a clicked button never
also takes an A press.

| Control | Pad | Keyboard | HUD |
|---|---|---|---|
| Pause / play | A | Space | PAUSE |
| Skip back / ahead (Nudge Seconds) | LB / RB | Left / Right | SKIP BACK / SKIP AHEAD |
| Slower / faster (Speed Step) | LT / RT | - | SLOWER / FASTER |
| Shot (Free, Pilot, Orbit, Chase) | D-pad left / right | 1-4 | CAMERA (cycles) |
| Pilot the shot watches | D-pad up / down | 5 | NEXT PILOT |
| Seek | - | - | drag the scrub bar |
| Leave | B | Escape | EXIT |

The pad's D-pad and A are the Theater's own transport (`TheaterGesture.ReadPadTransport`), and 1-5 are its keys, so the
dev Theater and player replays answer the same buttons. If the Theater's dev key **8** starts its own playback while a
replay is on screen, the replay steps aside.

### 6.1 The arena: the mode preview's satellite, not a scene load

A replay plays inside the arena it was raced in. That arena is **not** loaded as a scene: Menu_Main is always hosting
(the party, the lava lamp), and a mode scene carries in-scene NetworkObjects, its controller and its spawn chain, all of
which start a match - a stray local load of one is the B16 trap `Docs/THEATER.md` describes. The menu already has the
answer to "show this mode's arena without the menu changing": the mode preview's **satellite cell**
(`ModePreviewArena`, `Docs/ModePreview/ARCHITECTURE.md` §3, Caleb's area). `ReplayArenaStage` stands exactly that:

- the mode's preview definition from `Resources/ModePreviewLibrary`, and the **cell config the match ran** (the
  recording now carries its name, `TheaterRecording.CellConfigName`; an older recording falls back to the definition's
  config for the summary's intensity), so Regatta's rails come from the same `SpawnableRegattaRails` variant and seed;
- built as a satellite 120,000 units down **-Z** (the preview uses +X), past every camera's far clip, at **full prism
  density** (`ModePreviewArena.Stand`'s `prismStride`, `ReplayConfigSO.ArenaPrismStride` = 1; the preview flies at 4);
- a gate race's rings from the same `RaceCourseSource` the match controller builds from (`ModePreviewGateCourse`;
  Regatta and Skein take the course off the arena prefab, so the rings are the match's);
- the poses shifted by the stage origin minus `ReplaySummary.ArenaOrigin` (the match scene's cell position, recorded at
  Go; zero for a summary written before it, which is where every arcade scene's cell sits).

If no arena can be stood (a mode with no preview definition, no scene cell to clone, a build past
`ArenaBuildTimeoutSeconds`) the replay still plays, on the Theater's dark stage. The arena comes down through the
preview's pool-safe strike (`ModePreviewArena.BeginStrike`, a frame-sliced drain, `FinishStrike`).

**What the arena is not**: the match's world as it was at each moment. It is the arena at its start - rails whole,
ecology freshly grown - because the Theater records vessels only (P0). Prisms a Rhino popped, fauna that moved and
crystals that were taken are the Theater's P1 / P2.

**Every word is authored**: the row lines (`whenTemplate`, `dateFormat`, `winnerTemplate`, `noResultLine`,
`detailTemplate`, `openFailedLine`) are fields on `ReplayBrowserModal`; the HUD's (`timeTemplate`, `speedTemplate`,
`pauseLabel`, `playLabel`, `shotNames`) on `ReplayPlaybackHud`, and its loading panel's line, hint and CANCEL on that
panel's texts; Delete's confirm word on `ReplayRowView`. The generator
authors them and fails on an empty or non-ASCII one.

## 7. Try it now

1. Play Regatta to the end screen.
2. Look in `<repo>/Recordings/` for the `.cstheater` and its `.replay.json`.
3. Back on the menu: Arcade > **REPLAYS**. The race is the first row; **WATCH** shows "Building the arena", then plays
   the race inside Regatta's own rails and rings with the HUD over it; **B** or **EXIT** comes back to the list. (Still in the Regatta scene, the Theater's own key **8** plays
   the newest replay too.)

The full pass/fail list is in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Match replays on Will's Theater" and "Replay
browser", and "Replays inside the arena").

## 8. What is not built yet

| Next | What | Notes |
|---|---|---|
| The world in a replay | Prisms, fauna, crystals, projectiles | The Theater's P1 and P2 (`Docs/THEATER.md` §6). Until then the arena in shot is the one at the race's start (section 6.1). |
| A controller-built arena | Astro League's goals, Scarab's hoops | Their structures are built by the match controller, which the preview does not model either (`Docs/ModePreview/ARCHITECTURE.md` §7); such a replay shows the cell around the ghosts. |
| One camera director | Spectate, replay and Third Eye on one set of shots | `SpectatorController`, `TheaterSubjectCamera` / `TheaterFreeCamera` and the editor-only `ThirdEyeRig` all pose cameras separately today; unifying them is its own refactor. |
| Second mode | Skim Race | Add it to Auto Record Modes. Its rings are seeded per match, while the preview course uses the card-art seed, so record the match's course seed and pass it to the stage before turning it on. Regatta and Skein take their course off the arena prefab and are exact. |

## 9. Ownership

The Theater (`Assets/_Scripts/Utility/Theater/*`, `TheaterConfigSO`, `Docs/THEATER.md`) is **Will Bogen's** work,
brought onto this branch unchanged apart from three things recorded in its landing commit: its log lines ride the
`ArcadeMatch` channel (the `CSLogChannel` enum has no free bit), its index row moved to
`Docs/claude/DOCUMENTATION_INDEX.md`, and `TheaterDirector` gained `PlayFile(path)`. Check with Will before changing
anything else in it. The replay player reads `TheaterPlayback`'s public API and configures its stage through a runtime
copy of `TheaterConfigSO`; it changes nothing in the Theater.

The arena is **Caleb's** mode preview (`ModePreviewArena`, `ModePreviewGateCourse`, `Docs/ModePreview/ARCHITECTURE.md`),
used through its public API. The one change in it is an optional `prismStride` parameter on `ModePreviewArena.Stand`
(0 keeps the library's stride, so the preview behaves exactly as before).

The browser rides **Shombith's** style-B menu (`Docs/MENU_REDESIGN_B.md`): the Replays button is in his Arcade generator,
the window is built from the same kit and frame as Credits, and the look awaits his review on the team board.
