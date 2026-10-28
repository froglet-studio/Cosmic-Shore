# Match replays

Player-facing replays of a whole match: recorded automatically, kept on the device, listed from any device, and
watched with the Theater's free / pilot / orbit / chase cameras. **Built on Will Bogen's Theater** (`Docs/THEATER.md`),
which already records every vessel and plays the fleet back as ghosts. This layer decides *when* a match is recorded,
*where* it is kept, and *what* the browser lists; it does not re-implement recording or playback.

Design record with diagrams: https://claude.ai/artifact/4Ykh9qZEdf81cY6hPZonaK (private to the owner until shared).

**Where to find them (no match needed):** Home -> the pilot avatar (or **Y**) -> Profile -> **MY REPLAYS**, beside BACK
in the header. Also Arcade -> **REPLAYS**, beside BACK. Both open My Replays: the player's last 10 games, newest first;
**WATCH** plays one inside its arena. (Overwatch 2 keeps replays in the career profile; this follows that.)

---

## 1. Decisions (2026-10-15)

| Question | Decision | Why |
|---|---|---|
| Record inputs (like Overwatch 2) or outcomes? | **Outcomes** - poses, never inputs | Flight integrates in `Update`, gameplay rolls the global `Random`, AI paces with coroutines: re-running inputs drifts. `Docs/THEATER.md` §1 makes the same call; do not revisit it. |
| Build a recorder, or use the Theater's? | **The Theater's**, unchanged | Will's recorder, format, ghosts and cameras already exist; a second recorder would be a fork. |
| Which mode first? | **Regatta** (`GameModes.Regatta = 56`) | Its course is authored on the arena, so a replay needs no seed; the AI-training recorder already reads its rails. |
| Which modes record? (2026-10-26) | **Every mode, every match**, except the menu's freestyle (the lava lamp) | Yash: "any game mode I play, it should be automatically recorded". `ReplayConfigSO.recordEveryMode` (on), `neverRecordModes` (Random); the main menu scene (`SceneNameListSO.MainMenuScene`) never records. |
| How is a replay watched? (2026-10-26) | **On the free camera** by default (`ReplayConfigSO.startShot`) | "simulate the entire game with my free cameras as in third eye": the Theater's Free shot (sticks / WASD, seeded from the orbit framing); Pilot / Orbit / Chase stay one D-pad press away. |
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

**The world is recorded too (2026-10-26, Theater P1).** Beside the vessel poses, `TheaterWorldRecorder` records every
prism's birth, death, restoration and movement (section 6.2). It watches `PrismSpatialIndex`, the one place every real
prism registers, so nothing is hooked into `Prism`.

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
- **The newest `ReplayConfigSO.KeepNewest` (10) are kept** - My Replays is "the last 10 games".

## 4. Cloud Save index

`ReplayIndexRepository` (key `REPLAY_INDEX`) holds the newest `ReplayConfigSO.CloudIndexSize` (50) summaries, never
frames. `ReplayService` owns it and loads it on first use; `CloudDataRepository` already refuses to upload over a record
it failed to load (BH-2.1), so an offline match cannot wipe another device's list. `ReplayService.ListFromOtherDevicesAsync`
returns the summaries that are not on this device.

## 5. Config

`ReplayConfigSO` (`Resources/ReplayConfig`, optional; defaults work with nothing authored):

| Field | Default | Meaning |
|---|---|---|
| Record Every Mode | on | Every mode records itself (the main menu scene never does) |
| Auto Record Modes | Regatta | With Record Every Mode off, the modes that record |
| Never Record Modes | Random | Never recorded, even with Record Every Mode on |
| Start Shot | Free | The camera a replay opens on |
| Results Wait Seconds | 20 | Real-time wait for the end screen before saving without results |
| Keep Newest | 10 | Replays kept on the device - the player's last 10 games (Yash, 2026-10-26) |
| Listed Replays | 10 | My Replays lists this many per section (this device, other devices), newest first |
| Cloud Index Size | 50 | Summaries mirrored to Cloud Save (0 = off) |
| Nudge Seconds | 5 | How far the skip controls jump |
| Speed Step | 2 | Faster multiplies the playback rate by this, Slower divides |
| Min Speed / Max Speed | 0.25 / 4 | The playback-rate limits |
| Stage Distance | 120000 | How far from the menu world the replay's arena is parked (on -Z; the mode preview uses +X) |
| Arena Prism Stride | 1 | Every Nth prism of the arena's rails laid (1 = as the match laid them; the preview flies at 4) |
| Arena Build Timeout Seconds | 45 | Real-time wait for the arena; after it the replay plays on the dark stage |
| Arena Drain Prisms Per Frame | 150 | Prisms destroyed per frame while the arena comes down after watching |
| Record World | on | Record the prism world beside the vessels (section 6.2) |
| Record Moving Prisms | on | Also record prisms that move (swarm creatures, drifting walls); off keeps births and deaths only |
| Move Min Seconds / Move Min Distance | 0.2 / 1 | A moving prism is recorded again only after both |
| Move Jump Distance | 8 | A prism that jumps this far is recorded at once |
| Max World Events | 250000 | Prism events one replay keeps (about 50 bytes each); past it new prisms stop being recorded, and the log says so once |
| World Prism Material | empty | Material the recorded prisms are drawn with, tinted per domain (GPU instancing on); empty builds a lit URP material at runtime |
| Record Actors | on | Record the crystals, plants, creatures and cytoplasm shards beside the vessels (section 6.4) |
| Actor Jump Distance | 8 | A recorded actor that moves this far between samples is a cut (a respawn): the replay snaps it there instead of gliding, and a crystal that far from its recorded place is moved through its manager |

Sample rate, length cap, ghosts and cameras stay on `TheaterConfigSO`.

## 6. The browser and watching (2026-10-24)

```
Profile header [MY REPLAYS] [B BACK]       OpenModalButton (target REPLAYS = 19) -> ScreenSwitcher.OpenModal
Arcade header  [REPLAYS] [B BACK]          the same
      |
ReplayBrowserModal  (ModalWindowManager, ModalWindows.REPLAYS) - "My replays", "Your last 10 games"
      |  on open: ReplayService.ListOnDevice(), newest 10 -> ReplayRowView per replay: WATCH, DELETE (asks twice)
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
| MY REPLAYS (Profile) | `Tools/Build/author_menu_profile.py` (`build_view`) | Left of BACK in the Profile header (2026-10-26); a KitButtonSmall carrying `OpenModalButton`. Reached with the D-pad and A, or a click. |
| Replays button (Arcade) | `Tools/Build/author_menu_arcade.py` (`replays_button`) | Left of Back in the Arcade header; `OpenModalButton` names the window by TYPE, like `MenuHubButton`. |
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
- **the race rings the match was flown through**, captured at Go (`ReplaySummary.Gates`: every `RaceGateRing` in the
  scene in its first seconds, relative to the arena origin) and rebuilt exactly - a gate race draws a new course seed
  each match and a guest never sees it, so nothing else could place them. A replay recorded before 2026-10-26 has no
  rings captured: Regatta and Skein still stand the preview's course (theirs comes off the arena prefab), a seeded
  race stands none. The Cloud Save index carries no rings;
- the poses shifted by the stage origin minus `ReplaySummary.ArenaOrigin` (the match scene's cell position, recorded at
  Go; zero for a summary written before it, which is where every arcade scene's cell sits).

If no arena can be stood (a mode with no preview definition, no scene cell to clone, a build past
`ArenaBuildTimeoutSeconds`) the replay still plays, on the Theater's dark stage. The arena comes down through the
preview's pool-safe strike (`ModePreviewArena.BeginStrike`, a frame-sliced drain, `FinishStrike`).

**What the arena is not**: the match's world as it was at each moment. It is the arena at its start - rails whole,
ecology freshly grown. That is why a replay recorded from 2026-10-26 on does not use it (section 6.2); it stays the
stage for older replays.

### 6.2 The recorded world (Theater P1, 2026-10-26)

A replay recorded with **Record World** on carries its prism world as the recording's second stream
(`TheaterRecording.StreamWorld`, `TheaterWorld`). Such a replay **builds no arena**: it plays among exactly the prisms the
match had, at each moment - the trails the pilots laid, the walls they popped, the rails, the flora and the fauna's body
prisms - plus the recorded race rings, at the stage origin. Nothing waits, so it opens at once.

- **Recording.** `TheaterWorldRecorder` is `PrismSpatialIndex.LifeObserver` while a match records. At Go it takes every
  prism standing as a birth (`PrismSpatialIndex.VisitLive`), then logs births (`Register`), deaths (`MarkDestroyed`,
  `Unregister`; a double report is dropped), restorations (`MarkRestored`) and moves (`UpdatePosition`,
  `UpdatePositionsBatch`) on the vessels' `Time.time` clock. Creatures' body members are VIRTUAL entries (`RegisterVirtual`: swarm,
  substrate, NCA and builder-colony fauna, no GameObject) and are recorded too (2026-10-27), as boxes sized from their
  bounding radius; a member suspended while its proxy prism stands is a death, and its return a restoration where it
  last was. Each birth takes a new id, because the index reuses a slot
  once its prism is gone. Moves are thinned (Move Min Seconds and Distance, Move Jump Distance) and the whole log is
  capped (Max World Events). With no observer the index pays one null check per call.
- **On disk.** A birth or restoration is 51 bytes (time, id, position, rotation, size, domain, shield/danger flags),
  a move 37, a death 9. An older build skips the stream by its length; a recording without it reads as before.
- **Playback.** `TheaterWorldPlayback` keeps one list of matrices per (domain, shielded) and draws each with
  `Graphics.RenderMeshInstanced` (a unit cube scaled to the prism's recorded box, slices of 1023): no GameObjects, no
  colliders, nothing the game can touch. It follows the replay clock every frame; a backward seek or a loop replays the
  log from the start in one pass. Shielded prisms are drawn lighter.
- **What it shows and what it does not.** Prisms only, as domain-coloured boxes: not the prism shader's look, not the
  cell's membrane and nucleus, not crystals, projectiles or the vessels' own effects (P2). Prisms that are not in the
  spatial index (the menu's preview arenas, pictures) are not recorded, which is right: they were never in the match.


**Every word is authored**: the row lines (`whenTemplate`, `dateFormat`, `winnerTemplate`, `noResultLine`,
`detailTemplate`, `openFailedLine`) are fields on `ReplayBrowserModal`; the HUD's (`timeTemplate`, `speedTemplate`,
`pauseLabel`, `playLabel`, `shotNames`) on `ReplayPlaybackHud`, and its loading panel's line, hint and CANCEL on that
panel's texts; Delete's confirm word on `ReplayRowView`. The generator
authors them and fails on an empty or non-ASCII one.

### 6.3 The match itself, in its real scene (2026-10-27)

Yash, after testing 6.1-6.2: "I want it exactly like Overwatch 2 ... the entire scene need to be the actual scene ...
every single prism, every single game object". Decided with him the same day: **the recorded path plus the recorded
actions**, in **the real scene**, skip back by **reload and fast-forward**.

**WATCH now plays the match in its own scene.** `ReplayService.TryWatchMatch` -> `ReplayMatch.TryLaunch` writes what
the Arcade's Start writes (the mode's card, the recorded intensity, the human seat's hull and domain on the host's
Player, the AI seats' hulls and domains from the recorded tracks, the seat count) and forces the recorded course seed
(`CourseSeedOverride`: Skim Race's track and the gate races' rings; `ReplaySummary.CourseSeed`, captured at Go). The
scene loads as a match does: the cell and its ecology, the course, crystals, every seat spawned. Then
`ReplayMatchDirector`:

1. presses the HUD's own Ready once the intro shows it (the controller's Ready after 20 s if it never does);
2. at Go turns the turn monitors off, so no score ends the replay; the recording's end does, then it returns to the
   menu;
3. pairs each recorded track with a live seat (the local player takes the recorded human, AI seats by hull and domain,
   renamed to the recorded pilot) and stands every pilot down (autopilots off, input paused, flight model off with
   `VesselTransformer.ToggleActive(false)`);
4. every frame sets each vessel's pose, speed and course from its track, so its trail lays real prisms where the match
   laid them, its jets and skims are its own, and it takes the crystals it took;
5. re-runs every recorded action at its moment: a press or release through `R_VesselActionHandler.ReplayInput`
   (resolved against the device it was recorded with), an autopilot's own action by name. So guns fire real
   projectiles that break real prisms with their real effects, and boosts burn.

**Actions are recorded beside the path** (`VesselActionTape`, `TheaterRecording.Actions`, stream 3): every input press
and release that ran an action (humans, and the autopilots that press inputs) and every action an autopilot starts
itself (the AI ability coroutine, the boost policies, a crew's triggers). An action another action dispatches (the
Sparrow's and Grizzly's mode-switching fire) is not recorded: re-running the outer one re-runs it.

**Controls** (none clash with the free camera): the replay opens on a chase camera behind the recorded pilot; **Y /
Tab** cycles the other pilots and the free camera (sticks / WASD, mouse, shoulders for speed, triggers or Q/E to rise);
**A / Space** pause; **D-pad left / right** or **J / L** skip 5 s (forward fast-forwards at 8x; back reloads the scene and
fast-forwards to the moment); **D-pad up / down** or **= / -** speed (0.25x to 4x); **B / Escape** leaves.

**Console**: `replay latest` (F7 console, development builds and Amoebius) watches the newest replay as the match;
`replay list` lists them. A replay being watched is never recorded again.

**What it is not, yet:**
- **The ecology and the crystals still roll their own dice**, but since 2026-10-29 the replay overrides where they
  stand: crystals, plants, creatures and the cytoplasm shards are recorded as outcomes and placed every frame (6.4).
- **Analog inputs** (stick and trigger values) are not recorded: the pose is, and an action reads its vessel's pose.
- **No on-screen replay bar yet** in the scene (the menu's playback HUD is the in-menu picture's); the game's own HUD
  shows, with the live crystal counter.
- Verified end to end in Amoebius (`Docs/claude/VIDEOS_AND_PREVIEWS.md`); not yet in the Unity editor.

### 6.4 Crystals, plants, creatures and shards where the match had them (2026-10-29)

Yash, 2026-10-29: "I want Crystal's position, flora, fauna, shards, etc everyone's position, orientation, to mimic the
whole game pixel by pixel". The scene's own spawners still draw at random (6.3), so a replay records **outcomes** and
puts each live actor where its recorded one was, the way the vessels ride their tracks.

- **Recording** (`TheaterActorRecorder`, on with **Record Actors**; `TheaterRecorder.RecordActors`). On every vessel
  sample it takes a census: each manager crystal (`Crystal.Active`, id > 0; a heart crystal rides its plant or
  creature), each plant and creature root the enabled cells hold (`Cell.SpawnedLifeForms`, `Cell.LiveFauna`) gets its
  position, rotation and scale looked at, and its track keeps the pose when it changed (`TheaterActorThinning`: 1 cm,
  0.25 deg, a 0.001 scale; the still pose before a move is kept too, so a glide starts where it should). A move past
  **Actor Jump Distance** is flagged a cut. An actor no longer present has its track closed. At Go every cell's
  cytoplasm field is taken whole (`SnowChanger.CopyShardPositions`), and every shard a dying limb leaves afterwards
  is logged with its moment, place, heading, condense start and duration (`SnowChanger.MorphedShardAdded`).
- **On disk.** Stream 4 (`TheaterRecording.StreamActors`, `TheaterActors`), written last: per track 14 bytes plus its
  name plus 45 per pose; per shard field 13 bytes plus its cell name, 12 per shard and 40 per limb shard. An older
  build skips the stream by its length; a recording without it reads as before. `ReplayStagePlacement` shifts it with
  the rest.
- **Playback** (`ReplayActorSync`, driven by `ReplayMatchDirector` after the vessels each frame).
  - **Crystals** pair by crystal id (a manager hands ids out in the same order every run). Between a crystal's stays
    the last pose holds; a cut snaps. A crystal further than the jump distance from its recorded place is moved
    through its own manager (`CrystalManager.RelocateCrystal`, so the cell's slots, bloom and item listeners agree),
    and every frame its pose and scale are set.
  - **Plants and creatures** pair by species (prefab name without "(Clone)") and nearness: every 6 frames each live
    one without a partner takes the nearest free recorded one of its species present at that moment; from then on its
    root stands where that one stood. A live one with no partner is hidden (its renderers) until one is free; the
    renderers come back when the replay ends.
  - **Shards** pair by field (cell config name and order among the enabled cells of that config). Each field is stood
    at its recorded positions once (`SnowChanger.ApplyShardPositions`, grown or trimmed to the recorded count), then
    the recorded limb shards are added at their moments with their own condense start (`AddRecordedMorphedShard`).
    While a replay plays, live dying limbs leave none (`SnowChanger.ReplayHoldsMorphs`). Where a shard points and how
    long it is follow from the replayed crystal by the shard law, as in the match.
- **Not covered yet:** a plant's prism growth shape (its prisms are in the world stream, 6.2, but the live plant grows
  its own); swarm and virtual members and child creatures (only roots are driven); a crystal's own model animation; heart
  crystals (they ride their host); a recorded actor with no live counterpart gets no stand-in; a hidden plant or
  creature keeps its colliders; on a networked match `RelocateCrystal` goes through a ServerRpc (a replay is local, so
  the host is the player). None of it is verified in the Unity editor yet (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## 7. Try it now

1. Play Regatta to the end screen.
2. Look in `<repo>/Recordings/` for the `.cstheater` and its `.replay.json`.
3. Back on the menu: Home -> the pilot avatar (or Y) -> **MY REPLAYS** (or Arcade -> **REPLAYS**). The race is the first
   row; **WATCH** loads the race's own scene and plays the match in it (6.3), opening behind your pilot; **B** or
   **Escape** returns to the menu. (In a development build, the F7 console's `replay latest` does the same.)

The full pass/fail list is in `Docs/UNITY_VERIFICATION_CHECKLIST.md` ("Match replays on Will's Theater" and "Replay
browser", and "Replays inside the arena").

## 8. What is not built yet

| Next | What | Notes |
|---|---|---|
| The rest of the world | Projectiles, blasts, the cell's membrane and nucleus | The Theater's P2 (`Docs/THEATER.md` §6). Prisms (section 6.2) and crystals, plants, creatures and shards (section 6.4) are recorded. |
| Life forms in full | A plant's growth shape, swarm members, child creatures, stand-ins for unmatched actors | Section 6.4's "Not covered yet". |
| Prisms in their own look | The prism shader on recorded prisms | Recorded prisms are lit, domain-coloured boxes; assign **World Prism Material** for another look. |
| Rings that move | Skim Race's crystal rings | Captured once, at Go; a ring that moves with its crystal later in the match stays at its first place. |
| A mode with no preview definition | Maelstrom (and any mode the preview library lacks) | Recorded and watchable, on the dark stage. |
| A controller-built arena | Astro League's goals, Scarab's hoops | Their structures are built by the match controller, which the preview does not model either (`Docs/ModePreview/ARCHITECTURE.md` §7); such a replay shows the cell around the ghosts. |
| One camera director | Spectate, replay and Third Eye on one set of shots | `SpectatorController`, `TheaterSubjectCamera` / `TheaterFreeCamera` and the editor-only `ThirdEyeRig` all pose cameras separately today; unifying them is its own refactor. |

## 9. Ownership

The Theater (`Assets/_Scripts/Utility/Theater/*`, `TheaterConfigSO`, `Docs/THEATER.md`) is **Will Bogen's** work,
brought onto this branch unchanged apart from three things recorded in its landing commit: its log lines ride the
`ArcadeMatch` channel (the `CSLogChannel` enum has no free bit), its index row moved to
`Docs/claude/DOCUMENTATION_INDEX.md`, and `TheaterDirector` gained `PlayFile(path)`. Check with Will before changing
anything else in it. The replay player reads `TheaterPlayback`'s public API and configures its stage through a runtime
copy of `TheaterConfigSO`; it changes nothing in the Theater.

The actor recording (6.4) adds, all additive: `TheaterRecording` stream 4 (`StreamActors`, `Actors`, written last; the
other streams unchanged), `TheaterRecorder.RecordActors`, `Cell.SpawnedLifeForms` (read-only), and on `SnowChanger`
`CopyShardPositions`, `ApplyShardPositions`, `AddRecordedMorphedShard`, the `MorphedShardAdded` event and the
`ReplayHoldsMorphs` gate (its one change to existing behaviour: while a replay plays, `AddMorphedShard` adds nothing).

The world recording adds one hook to the prism spatial index (`PrismSpatialIndex.LifeObserver`, `VisitLive`, and the
observer calls in `Register`, `Unregister`, `MarkDestroyed`, `MarkRestored`, `UpdatePosition` and
`UpdatePositionsBatch`; the area Garrett and Yash work in). It changes nothing the index does.

The arena is **Caleb's** mode preview (`ModePreviewArena`, `ModePreviewGateCourse`, `Docs/ModePreview/ARCHITECTURE.md`),
used through its public API. The one change in it is an optional `prismStride` parameter on `ModePreviewArena.Stand`
(0 keeps the library's stride, so the preview behaves exactly as before).

The browser rides **Shombith's** style-B menu (`Docs/MENU_REDESIGN_B.md` §75): MY REPLAYS is in his Profile generator,
the Replays button in his Arcade generator,
the window is built from the same kit and frame as Credits, and the look awaits his review on the team board.
