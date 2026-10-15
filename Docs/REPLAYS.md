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
| Command pattern? | **For playback controls**, not for recording | Transport commands (play, pause, seek, speed, follow) are the browser's next step; recording inputs is ruled out above. |

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

Sample rate, length cap, ghosts and cameras stay on `TheaterConfigSO`.

## 6. Try it now

1. Play Regatta to the end screen.
2. Look in `<repo>/Recordings/` for the `.cstheater` and its `.replay.json`.
3. Still in the Regatta scene, press **8**. The Theater plays the race; **1-4** pick Free / Pilot / Orbit / Chase,
   **5** the next pilot, **A** / the overlay's buttons pause, seek and change speed.

The full pass/fail list is in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Match replays on Will's Theater").

## 7. What is not built yet

| Next | What | Notes |
|---|---|---|
| Replay browser | `ModalWindows.REPLAYS = 19` (the next free value after `CREDITS = 18`; retired values are never reused), a list of `ReplayService.ListOnDevice()` plus other-device entries, a Replays button in the Arcade modal (10), gamepad B back to Arcade | Mirrors `FriendsListPanel` / `OnlineInfoEntry`. Check in-flight branches before taking 19. |
| Transport commands | `IReplayCommand` (Play, Pause, Seek, SetSpeed, FollowPilot, SetCameraMode) queued by the browser's buttons, keys and pad, run once a frame by the playback | The command pattern from the original ask. Today the Theater's own overlay and keys drive playback. |
| Watching from the menu | Load the replay's scene as a stage (no match, no network spawn) and play it there | Today `ReplayService.Watch` plays in the scene you are in, so it is meant for the scene the match was recorded in. |
| The world in a replay | Prisms, fauna, crystals, projectiles | The Theater's P1 and P2 (`Docs/THEATER.md` §6). Until then the world in shot is the live one. |
| One camera director | Spectate, replay and Third Eye on one set of shots | `SpectatorController`, `TheaterSubjectCamera` / `TheaterFreeCamera` and the editor-only `ThirdEyeRig` all pose cameras separately today; unifying them is its own refactor. |
| Second mode | Skim Race | Add it to Auto Record Modes; its course is seeded, so a menu-side stage needs the seed (`GateRaceController`'s `RaceCourseSource`). |

## 8. Ownership

The Theater (`Assets/_Scripts/Utility/Theater/*`, `TheaterConfigSO`, `Docs/THEATER.md`) is **Will Bogen's** work,
brought onto this branch unchanged apart from three things recorded in its landing commit: its log lines ride the
`ArcadeMatch` channel (the `CSLogChannel` enum has no free bit), its index row moved to
`Docs/claude/DOCUMENTATION_INDEX.md`, and `TheaterDirector` gained `PlayFile(path)`. Check with Will before changing
anything else in it.
