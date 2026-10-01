# Fix Log

One report per shipped fix, **newest first**. Dates are IST. The workflow and the other docs are
described in [`README.md`](README.md); the problem-class guides are in [`PLAYBOOK.md`](PLAYBOOK.md).

The `Editor-2026-09-26-0447.log` and `Editor-2026-09-26-0510.log` names below are local copies
of Yash's `Editor.log`. The 0510 copy contains the whole 0447 session plus the later sessions.

---

## CI-1 — raw `Debug.Log` in `TrainingSessionRunner.LeaveSlot` failed the console-logging check

- **Date:** fixed 2026-10-02.
- **Symptom:** the `conditional-compilation` CI job failed on PR #936 (and would fail on any PR)
  at its "Check console logging" step: `TrainingSessionRunner.cs:710: raw Debug.Log - route through
  CSDebug`. The failure was in code already on `bleeding-edge`, not in the PR's own change.
- **Root cause:** the AI training commits of 2026-09-29 added a raw `Debug.Log` in `LeaveSlot`.
  The project's rule is that all logging goes through `CSDebug`.
- **Fix:** `LeaveSlot` calls the file's own `Trace` helper (`CSDebug.LogVerbose` on the
  `AITraining` channel), the same as every other training log in that file. Same message text.
- **Verification:** `python3 Tools/Build/check_console_logging.py` reports no problems (it
  reported 1 before).
- **PR/commit:** pending.

---

## BH-1.1 — AI held drift on stop (already fixed; closed with no code change)

- **Date:** closed 2026-10-02. The fix itself is `4c866f880` of 2026-09-26, which predates the
  handoff doc's review of this item.
- **Symptom (from the handoff):** a vessel stays in the AI's commit drift (course locked, nose
  free) after autopilot is switched off, until the human taps drift.
- **Cause:** `StopAIPilot` stopped the brain but never sent the matching stop for the commit drift.
- **What already fixes it:** `AIPilot.StopAIPilot` releases the commit drift (`_commitDriftHeld`),
  stops every cycled ability that had started and clears the aim telegraph. `PilotSwap` stops the
  AI and calls `ReleaseHeldInputs` while the server still owns the hull, so the release replicates.
- **Left alone on purpose:** `AIPilot.OnDisable` does not release a drift. It only runs on
  teardown, where the vessel is going away, and the one other disabler (the AI training pilot)
  calls `StopAIPilot` first. Sending input from a teardown path risks null references.
- **Verification:** Yash tested the Menu_Main freestyle takeover on `Bug_Hunt` and the ship flew
  normally, with no stuck drift.
- **PR/commit:** docs only.

---

## BH-1.13 — `Fauna` never left its cell's spawned-object list

- **Date:** fixed 2026-10-02 (commit); merge pending Yash's retest. Skipped the repro on
  `bleeding-edge` at Yash's call, because the cause is clear from the code.
- **Symptom:** none visible in normal play and nothing in the Console. `Cell.spawnedLifeForms`
  kept an entry for every creature that died or was torn down, so the cell's `LifeFormsInCell`
  stat only ever went up. That stat is read by `AllLifeFormsDestroyedTurnMonitor` (the
  Wildlife Blitz co-op scene, `MinigameWildlifeBlitzMultuplayerCoOp`) and the single-player
  Wildlife Blitz turn monitor, so "clear every creature" could never reach 0.
- **Root cause:** `Flora` leaves the list in `LifeForm.Die`, but `Fauna` derives from
  `MonoBehaviour`, not `LifeForm`. No fauna death path (starvation, predation, joust, scene
  teardown, cell swap or split) called `Cell.UnregisterSpawnedObject`, and `Fauna.OnDestroy`
  only left the live-fauna registry (`UnregisterLiveFauna`), which is a different collection.
- **Repro:** not run on `bleeding-edge`. To see it, log `spawnedLifeForms.Count` in
  `Cell.UpdateCellStats`: it only rises as creatures die.
- **Fix:** `Fauna.OnDestroy` calls `hostCell.UnregisterSpawnedObject(gameObject)`. The call is a
  no-op when the object isn't in the list, and skipped if the cell is gone, so it is safe on
  every destroy route. The handoff doc's §1.13 moved to §0.
- **Verification:** all four gate scripts pass. Needs Yash's retest on `Bug_Hunt`: play the
  Wildlife Blitz co-op scene (or any mode with fauna), let creatures die, and check that nothing
  throws and the creatures count goes down as they die.
- **PR/commit:** pending.

---

## BH-1.10 — crystal colour fade leaks a Material per colour change

- **Date:** fixed 2026-10-02 (commit); merge pending Yash's retest.
- **Symptom:** nothing in the Console. The Profiler's Materials count creeps up over a long
  session in one scene (crystal colour changes: a heart becoming a pickup, theft and decay back
  to blue), and the extra materials are named `Crystal... (Instance)`. Unity frees them on a full
  scene load, so the growth only shows within one scene.
- **Root cause:** `Crystal.LerpCrystalMaterialCoroutine` ran `new Material(renderer.material)`.
  The `.material` getter clones the renderer's current material onto the renderer, and the
  following `renderer.material = tempMaterial` replaced that clone without destroying it, so
  one Material leaked per colour change. (The handoff said two; the fade copy is destroyed at
  the end, so it is one.) The fade copy also leaked if the crystal was destroyed mid-fade,
  because the `Destroy(tempMaterial)` at the end of the coroutine never ran.
- **Repro (unfixed `bleeding-edge`):** Profiler > Memory > Take Sample Editor, then play one
  round in a mode where crystals change colour (Skim Race, hearts dropping), take a second
  sample in the same round, and compare the Material count and `Crystal (Instance)` entries.
- **Fix:** `Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs`.
  - The fade copy is `new Material(renderer.sharedMaterial)`, and the renderer is assigned
    through `sharedMaterial`, so no hidden clone is made.
  - Each fade copy is added to `_lerpTempMaterials` and removed when the coroutine destroys it;
    `OnDestroy` destroys any still listed.
  - The handoff doc's §1.10 moved to §0.
- **Verification:** all four gate scripts pass. Needs Yash's retest on `Bug_Hunt`: the same
  play path should leave the Material count flat and show no `Crystal (Instance)` entries.
- **PR/commit:** pending.

---

## BH-1.8 — `Cell.countGrids` never disposed on destroy

- **Date:** fixed 2026-09-26 (commit), merged 2026-09-29 04:46.
- **Symptom:** after a domain reload or editor quit (not during play):
  `Leak Detected : Persistent allocates N individual allocations`. The Console was empty after
  an editor restart; the report was in `Editor-prev.log`.
- **Root cause:** `Cell.SetupDensityGrids` disposes the old density grids when it rebuilds them,
  but `Cell.OnDestroy` never disposed the last set. A cell owns 4 `BlockCountDensityGrid`s (Jade,
  Ruby, Gold, plus the Blue all-domain grid), and each allocates 6 `Allocator.Persistent`
  `NativeArray`s in `BlockDensityGrid.Init`. That's 24 leaked allocations per destroyed cell.
- **Repro (unfixed `bleeding-edge`):** set Preferences > Jobs > Leak Detection Level to
  *Enabled With Stack Trace*. Play a few arcade games, exit play mode, restart the editor, then
  read `Editor-prev.log`. Yash got `Leak Detected : Persistent allocates 48 individual
  allocations` and `... 168 ...`, with stacks at `BlockDensityGrid.Init` (BlockDensityGrid.cs:316-321)
  ← `BlockCountDensityGrid..ctor` (:468). Both counts are multiples of 24. Earlier logs without
  stack traces had the same shape: `264` (0510 log line 112170, also in the 0447 log) and `24`
  (0510 log line 159993), each right after a script recompile.
- **Fix:** `Assets/_Scripts/Controller/Environment/Cell.cs`.
  - `OnDestroy` disposes every grid (null-safe) and clears `countGrids`.
    `BlockDensityGrid.Dispose` is idempotent (guarded by `jobSystemInitialized` plus `IsCreated`),
    so the rebuild path can't double-free.
  - `AddBlock`/`RemoveBlock` read the Jade/Ruby/Gold grids with `TryGetValue`, as the Blue grid
    already was, so a prism removed after its cell during teardown can't throw
    `KeyNotFoundException` on the emptied map.
  - The handoff doc's §1.8 moved to §0.
- **Verification:** Yash retested on `Bug_Hunt` (314ad51) with the same play path and forced a
  recompile (saved a `.cs`). There were no leak reports and no `KeyNotFoundException`. All four
  gate scripts passed.
- **PR/commit:** `2195795` · PR #926 · merge `546bda3`.

---

## CC-4 — `[PrismRenderVisibilityFlush]` re-created on play-mode exit

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:** on stopping play mode: `Some objects were not cleaned up when closing the scene.
  (Did you spawn new GameObjects from OnDestroy?)` listing `[PrismRenderVisibilityFlush]`. The
  Unity stack was `ValidateNoSceneObjectsAreLoaded ← EditorSceneManager::RestoreSceneBackups ←
  PlayerLoopController::ExitPlayMode`.
- **Root cause:** `PrismRenderService.QueueVisible` lazily creates a DontDestroyOnLoad host
  (`EnsureFlushHost`). `Prism.OnDisable` queues a hide for its render entity on every disable,
  including the mass disable at play-mode exit. After the teardown destroyed the host, the next
  `Prism.OnDisable` created a new one, which leaked.
- **Repro:** enter play mode, play, then stop. In the 0510 log, the first session running the
  CC-3 fix (after the recompile and asset reimport at ~line 159984–160204) ended at line 163544
  with only `[PrismRenderVisibilityFlush]` listed. Nothing logs when the host is created, so the
  call path comes from searching the code: `Prism.OnDisable` is the only teardown caller of
  `QueueVisible`.
- **Fix:**
  - `PrismRenderService`: `IsQuitting` flag set from `Application.quitting` (which also fires on
    editor play-mode exit) and reset on `SubsystemRegistration`. While it is set, `QueueVisible`
    returns early, and `EnsureFlushHost` never creates the host while quitting or when
    `!Application.isPlaying`.
  - The same guard went into `PrismShieldShatter.TryRequest`/`EnsureHost` and
    `PrismDebris.EnsureHost`.
- **Verification:** Yash tested PR #905 and confirmed the cleanup error was gone.
- **PR/commit:** `f02cef8` · PR #905 · merge `44a9d5f`.

## CC-3 — `[PrismTimerManager]` / `[PrismDebris]` re-created during scene teardown

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:** `Some objects were not cleaned up when closing the scene` listing
  `[PrismTimerManager]`, on scene switches (`UnloadGameScene`) and on play-mode exit. On exit it
  sometimes also listed `[PrismDebris]`. The 0447 log has 9 occurrences.
- **Root cause:** `Spindle.OnDisable` has a "scene unloading, don't run the death cascade"
  guard, but `parentSpindle.RemoveSpindle` and `LifeForm.RemoveSpindle` call
  `CheckForLife`/`CheckIfDead` themselves, so the cascade ran anyway during teardown.
  - The parent spindle evaporated, and `PrismTimerManager.EnsureInstance()` found the scene's
    manager already destroyed, so it auto-created a new one.
  - The LifeForm died, its structure exploded, and `PrismDebris` re-created its host.
- **Repro/evidence:** each leak is preceded by `[PrismTimerManager] No instance found in scene -
  auto-created.` with the managed stack `EnsureInstance ← Spindle.StampDeathFade ←
  StampEvaporate ← EvaporateSpindle ← CheckForLife ← … ← LifeForm.RemoveSpindle ←
  PhyllotacticFlora.RemoveSpindle ← Spindle.OnDisable`. The native frames below it are
  `UnloadGameScene` or `RestoreSceneBackups`/`ExitPlayMode`. See the 0447 log lines 7749→7828,
  10526→10605 and 14684→14738.
- **Fix:**
  - `Spindle.OnDisable`: on unload it drops the parent's reference directly and skips
    `LifeForm.RemoveSpindle`.
  - `PrismTimerManager.EnsureInstance()` returns null while quitting (`Application.quitting`,
    `ApplicationLifecycleManager.IsQuitting`) or while its own scene is unloading (flag set in
    `OnDestroy`, cleared on `sceneUnloaded`). Every caller now uses `?.`.
  - `PrismDebris.TryRequestExplosion/Implosion` return false after `Application.quitting`.
- **Verification:** in the 0510 log's post-fix session, `[PrismTimerManager]` was only
  auto-created during normal scene loads (Authentication, Menu_Main). There was no cleanup error
  on those scene switches, and the error on exit listed only `[PrismRenderVisibilityFlush]`
  (CC-4).
- **PR/commit:** `cfcd671` · PR #905 · merge `44a9d5f`.

## CC-2 — Ability map assets fail to parse (Squirrel, Dolphin, Rhino, Sparrow)

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:**
  - `Unable to parse file Assets/Resources/ElementalAbilityMaps/Squirrel.asset: [Parser Failure at line 52: Expect ':' between key and value within mapping]`
  - the same for `Dolphin.asset` (line 76)

  The whole asset fails to load.
- **Root cause:** `AbilityDescription`/`UpgradeDescription` were multi-line **plain (unquoted)**
  YAML scalars containing `: `, with continuation lines ending in a colon
  (`ILifeFormEntity.Nourish:`, `boost speed:`, `SPEED:`). Unity only reported the files that
  have a line ending in a colon. Rhino and Sparrow have only mid-line `: `, which Unity
  tolerated, but they fail a strict YAML parse.
- **Fix:** each offending field became a single-quoted scalar (`''` escapes an apostrophe), the
  way Unity writes long strings itself. Line breaks were kept, so the loaded text is identical.
  No generator writes these four maps.
- **Verification:** after the reimport in the 0510 log (~160189–160204), the post-fix session
  has no parse errors. A strict YAML parse passes for all 8 ability maps.
- **PR/commit:** `d7e3617` (Squirrel), `8df5d33` (Dolphin, Rhino, Sparrow) · PR #905 · merge `44a9d5f`.

## CC-1 — Rampage Cell Configs fail to parse; generator emitted invalid YAML

- **Date:** 2026-09-26 (part of PR #905).
- **Symptom:** `Unable to parse file Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell
  Config 1.asset: [Parser Failure at line 28: Expect ':' between key and value within mapping]`.
- **Root cause:** `Tools/Build/rampage_intensity.py` (`_wrap_yaml_scalar`) wrote the long
  `Description` as a wrapped plain scalar containing `: `, and in Config 1 one wrapped line ends
  in `hit:`. Configs 2–4 had the same invalid YAML (mid-line colons only).
- **Fix:** the generator now emits a single-quoted scalar, and all four configs were regenerated
  from it. Only the quoting changed. `rampage_intensity.py --check` passes.
- **Verification:** no parse error after the reimport (0510 log, post-fix session). A strict
  YAML parse passes for all four configs.
- **PR/commit:** `a227e1e` · PR #905 · merge `44a9d5f`.

---

## Known open console issues

Checked against `Bug_Hunt` @ `546bda3` on 2026-09-29.

- **17 assets fail a strict YAML parse.** Unity has not reported these (it tolerates a mid-line
  `: `), but they are invalid YAML, and the next edit that wraps a line ending in `:` will break
  them. Fix the generator where there is one (PLAYBOOK §3).
  - `Boneyard Cell Config 1-4`: `author_dogfight_assets.py`
  - `Regatta Cell Config 1-4`: `author_regatta_assets.py`
  - `Tollway Cell Config 1-4`: `author_tollway_assets.py`
  - `ArcadeGameBroadside`: `author_broadside_assets.py`
  - `ArcadeGameWaystation`: `author_waystation_assets.py` (**new since PR #905**, added by
    `de3a4c8`)
  - `SO_Captain_Dolphin_Space`: `Flavor: “…Death: The…”` is unquoted
  - `SO_Captain_Sparrow_Charge`: tab-indented `Space:`/`Time:` lines
  - `SO_Captain_Sparrow_Space`: `IconActive:` is on the same line as `HeadshotImage: {fileID: 0}`,
    so the value is probably lost
- **`NullReferenceException` in `Crystal.ActivateCrystal`** (Crystal.cs:699,
  `transform.parent = cellData.Cell.transform;`), called from `Fauna.ReleaseHeart ←
  LightFauna.WitherCoroutine`. It appears 3 times in the 0447 log, e.g. line 44274, about 100
  lines before a scene-cleanup error. It is likely a teardown-order problem: the cell is gone
  before the wither coroutine reaches the heart (PLAYBOOK §4). Not fixed.
