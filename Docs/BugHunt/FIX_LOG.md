# Fix Log

One report per shipped fix, **newest first**. Dates are IST. The workflow and the other docs are
described in [`README.md`](README.md); the problem-class guides are in [`PLAYBOOK.md`](PLAYBOOK.md).

The `Editor-2026-09-26-0447.log` and `Editor-2026-09-26-0510.log` names below are local copies
of Yash's `Editor.log`. The 0510 copy contains the whole 0447 session plus the later sessions.

---

## BH-4.7 — PrismTimerManager.CancelScheduledActions was O(N²)

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom (risk):** mass pool returns (many prisms cancelled in one frame) hitch while cancel walks the
  whole scheduled list and `RemoveAt`-shifts for each match.
- **Root cause:** a single flat `List<ScheduledAction>` scanned on every cancel.
- **Fix:** actions are stored in `Dictionary<owner, List<…>>`. Cancel removes that owner's list in
  one step; Update walks owners via a scratch key list so callbacks can reschedule safely.
- **Verification:** gate scripts pass; not run in Unity. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-4.6 — ThumbCursor / ThumbPerimeter used legacy Input.touches

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom (risk):** under the new Input System, `Input.touches` is empty, so the thumb cursor and
  perimeter never saw a finger down (both scripts are currently TEMP-disabled, so this was latent).
- **Root cause:** legacy Input Manager API.
- **Fix:** both read active touch count from `Touchscreen.current.touches` (same package as
  `InputController` / `InputDeviceActuation`).
- **Verification:** gate scripts pass; not run in Unity. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-4.5 — name generator skipped the last word in each list

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom:** the last entry in `FirstWordList` / `SecondWordList` could never appear in a generated name.
- **Root cause:** `Random.Range(0, list.Length - 1)` — Unity's int overload treats the max as exclusive, so
  the last index was never chosen. Dead for gameplay today if the lists are unused, but wrong when called.
- **Fix:** `Random.Range(0, list.Length)`.
- **Verification:** gate scripts pass; not run in Unity. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-4.4 — BranchingFlora re-rolled the trunk count every loop iteration

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom (risk):** trunk count drifted as `SeedBranches` ran, and a late roll could keep the loop
  going far past `maxTrunks` (or forever if the rolled value stayed above `i`).
- **Root cause:** `for (int i = 0; i < Random.Range(minTrunks, maxTrunks + 1); i++)` re-evaluates
  the upper bound every iteration.
- **Fix:** roll `trunkCount` once before the loop.
- **Verification:** gate scripts pass; not run in Unity. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-4.1 — Bloomrush restarted a 0-0-0 round when Jade was not fielded

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom:** in a Bloomrush match that fielded only Ruby and Gold (or any set without Jade), a
  round that ended 0-0-0 never showed a winner and started again.
- **Root cause:** `BloomrushScoringRuleSO.ResolveWinner` walked `ActiveDomains[0..RequestedDomainCount)`,
  so Jade won every all-zero tie even with no Jade players. `BloomrushController` then looked for a
  Jade `winnerRep`, found none, returned without setting `_finalResultsSent`, and `SetupNewRound`
  ran again.
- **Fix:** `ResolveWinner` now considers only domains that appear in `RoundStatsList` (fielded
  players), still iterating ActiveDomains order so Jade → Ruby → Gold remains the last resort
  among those that played. Matches how `ResolvePlacementOrder` already works.
- **Verification:** gate scripts pass; not run in Unity. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-4.2 / 4.3 — owner stat-report RPCs accepted NaN volumes and out-of-turn reports

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom (risk):** a client could send a NaN volume that passed the `volume < 0f` check (every
  comparison with NaN is false) and poisoned that player's volume totals. A report that arrived
  after the turn had ended, or before it started, was credited into `RoundStats`, which can
  disagree with a result the server already froze.
- **Root cause:** the report RPCs in `Player.cs` trusted the value and ignored the turn state;
  only `ReportSwitchThreaded_ServerRpc` had a turn gate.
- **Fix:** `ReportEnvironmentPrismDestroyed_ServerRpc` and `ReportPrismStolen_ServerRpc` use
  `if (!(volume >= 0f)) return;` so NaN is rejected. A new `TurnAcceptsStatReports` property
  (`gameData.IsTurnRunning`) gates `ReportFaunaKill`, `ReportCombatHit`, `ReportFusesBeaten`,
  `ReportEnvironmentPrismDestroyed` and `ReportPrismStolen`.
- **Trade-off:** a legitimate report still in flight when the turn ends is now dropped. That is the
  intended behaviour, the same as the switch-thread gate.
- **Verification:** gate scripts pass; not run in Unity (needs a multiplayer match). Retest is on
  the handoff playtest list.
- **PR/commit:** pending.

---

## BH-3.1 — Play Again could leave the screen black when the vessel was ready early

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom (suspected):** after Play Again (scene-reload replay) a host or client stays on a black
  overlay and never fades in.
- **Root cause:** `MultiplayerMiniGameControllerBase` subscribed `FadeFromBlackOnReplay` to
  `OnClientReady` inside `InitializeAfterDelay`, after the `InitDelayMs` (1 s) wait and after
  `InitializeGame()`. The player vessel can finish initialising inside that window, so
  `OnClientReady` had already fired, the subscription never saw it, and nothing lowered the overlay.
- **Fix:** the fade is now armed in `OnNetworkSpawn`, as soon as `IsReplayReload` is seen (the flag
  is cleared there). The late block is removed. `OnNetworkDespawn` unsubscribes so an armed fade
  cannot outlive the scene. The handler still unsubscribes itself on first run.
- **Verification:** gate scripts pass; not run in Unity (needs a multiplayer replay). Retest is on
  the handoff playtest list.
- **PR/commit:** pending.

---

## BH-2.4 — reconnect from the main menu was refused by the app-state machine

- **Date:** fixed 2026-10-05; kept on `Bug_Hunt` with the retest deferred to the handoff revisit list (not merged to bleeding-edge). Repro skipped.
- **Symptom:** after tapping Reconnect in the menu the console shows `[AppState] Invalid
  transition: MainMenu → Authenticating`, and the app-state mirror stays `MainMenu` while the auth
  scene runs. Anything keyed on app state sees the wrong phase during the re-boot.
- **Root cause:** `ReconnectService` calls `TransitionTo(Authenticating)` from the menu, but the
  transition table only listed `MainMenu → LoadingGame`.
- **Fix:** `Authenticating` is now a valid target from `MainMenu`, `LoadingGame`, `InGame` and
  `GameOver` (reconnect is a legitimate path from any of them, since the disconnect notice can
  appear mid-game). The class summary lists it, and `ApplicationStateMachineTests` has new cases.
- **Not changed:** `Bootstrapping`, `None` and `ShuttingDown` still do not accept it, and
  `Disconnected` already did.
- **Verification:** gate scripts pass; the new EditMode test was not run (Unity not available
  here). Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-2.3 — invite-clear could skip the lobby mutex and race an invite send

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom (risk):** an invite that never arrives, or one that fires twice, when a clear and a
  send overlap.
- **Root cause:** `HandleInviteClearedAsync` decided whether to take `_lobbyMutex` from a shared
  `_insideRefreshCycle` flag. That flag only meant "some refresh or reconcile is running", not
  "this caller holds the lock". So user-cancel, the party-leave callback and the fire-and-forget
  clears started inside a refresh (which keep running after the refresh releases the mutex) wrote the
  invite property without the lock whenever a refresh was in flight.
- **Fix:** `_insideRefreshCycle` is removed. `ClearOutgoingInviteIfPresentAsync` and
  `HandleInviteClearedAsync` take `callerHoldsLobbyMutex` (default false). Only the awaited call
  inside `RefreshPartyMembersAsync`, which always runs under the mutex, passes true. All other
  callers (Update expiry, user cancel, presence-leave, presence-join, party-leave) wait for the
  mutex. The fire-and-forget ones are not awaited by the refresh, so they cannot deadlock it.
- **Audit:** the six callers were checked; the invite send path (`SendInviteAsync`) writes under
  its own mutex hold and does not call the clear.
- **Verification:** all gate scripts pass; not run in Unity (two players needed). Retest is on
  the handoff playtest list.
- **PR/commit:** pending.

---

## BH-2.2 — presence lobby was never rejoined after a failed reconnect

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** after a network blip the online list stays empty and invites stop arriving until
  the app is restarted.
- **Root cause:** after three consecutive refresh errors `RefreshAsync` calls `ForceReset()` and
  runs `JoinOrCreateAsync` once. If that attempt fails (`CreateAsync` swallows its own errors and
  leaves the lobby null), `Update` is gated on `IsInPresenceLobby` (lobby not null), so nothing ever
  ran the join again. A throw from `JoinOrCreateAsync` there was also unobserved.
- **Fix:** `HostConnectionService` sets `_presenceRejoinPending` when the rejoin leaves no lobby
  (or throws). `Update` then calls `TryPresenceRejoin`, which retries `JoinOrCreateAsync` with
  exponential backoff (3s doubling to 60s). It stops when the lobby is back, the service is
  disconnected, the session is offline, or the normal `EnsureInitializedAsync` path takes over.
  `Docs/PresenceSystem/ARCHITECTURE.md` documents it under ForceReset.
- **Verification:** all gate scripts pass; not run in Unity (needs two players and a network cut).
  Retest is on the handoff playtest list. The identity republish after rejoin still comes from
  `LivePropertySource` as before.
- **PR/commit:** pending.

---

## BH-2.1 — Cloud Save could not tell "load failed" from "no data yet"

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom (risk):** on a flaky connection a player's progression, unlocks or profile could be
  replaced by defaults.
- **Root cause:** `UGSCloudSaveProvider.LoadAsync` returned `null` both for a missing key and
  for any error (offline, auth, network, unreadable value). `CloudDataRepository` treated both as
  "new player", kept its fresh default object and uploaded it on the next write, over the
  real record.
- **Fix:**
  - `ICloudSaveProvider.TryLoadAsync` returns `CloudLoadResult<T>` with a `CloudLoadStatus` of
    `Loaded`, `Missing` or `Failed`. `LoadAsync` stays as a wrapper. A stored value that cannot be
    read is `Failed`, not `Missing`.
  - `CloudDataRepository.LoadAsync` records a failed load. It still falls back to the local
    snapshot so the player can play. `SaveAsync` still writes the local snapshot, but while the
    load is marked failed it does not upload: it retries the load first. `Missing` clears the flag
    and allows the upload, `Loaded` adopts the cloud record (cloud wins, as on a normal load; the
    pending local edits are dropped and a warning is logged), and `Failed` leaves the data dirty to
    retry later. `ResetAsync` clears the flag because a deliberate wipe is meant to overwrite.
- **Trade-off:** edits made while the load was failing are dropped if the real record then loads.
  That matches what already happened on the next launch, and is safer than overwriting the record.
- **Verification:** all gate scripts pass; not run in Unity. No test double implements
  `ICloudSaveProvider`, so no tests needed updating. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.12 — a departed player's vessel handed to the AI was not marked AI

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** after a client leaves mid-match the ship flies on under the AI, but the rest of the
  game still treats that Player as a human (ready gates, round reset, HUD checks).
- **Root cause:** `ServerPlayerVesselInitializer.ConvertPlayerToAI` flips the networked
  `NetIsAI` only. `Player.IsInitializedAsAI`, the flag everything reads, is a local copy that was
  refreshed only when the pair was first initialised, so it stayed `false` on the server and on
  every client. A spawned backfill bot has it set at spawn, which is why bots were fine.
- **Fix:** `Player` subscribes to `NetIsAI.OnValueChanged` (subscribed in `OnNetworkSpawn`,
  unsubscribed in `OnNetworkDespawn`) and updates `IsInitializedAsAI` and the object name.
  The player is already in `_processedPlayers` from when it was a human, so no change was needed
  there.
- **Verification:** all gate scripts pass; not run in Unity (needs two devices). Retest is on the
  handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.11 — non-ASCII characters in UI strings rendered as empty boxes

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** arrows, a cross, a middle dot, a times sign and shape bullets in UI text show as
  empty boxes. Nothing in the Console.
- **Root cause:** the only UI font (`ALDRICH-REGULAR SDF`) carries 97 glyphs (ASCII, nbsp and an
  ellipsis) with no fallback table. See `Docs/claude/ANTI_PATTERNS.md`.
- **Fix:** ASCII replacements, all in strings that reach a `TMP_Text`:
  - `SpectatorOverlay`: `<` and `>` buttons, `X  LEAVE`, and the hint line `< > / Q E ...`.
  - `ToyConfigureModal`: `<  Back`. `ToyVariantCard`: branch marker `>`.
  - `DogFightScoringRuleSO`: `N pts - B rounds, M rockets` (was `N pts · B×● M×◆`, the shapes
    had no meaning without a legend).
  - `BroadsideScoringRuleSO` and `UndertowScoringRuleSO`: the `·` separator became `, `.
- **Verification:** all gate scripts pass; no test asserted the old strings; not run in Unity.
  Retest is on the handoff playtest list. Alternative if the symbols are wanted back: add the
  glyphs to the font asset instead.
- **PR/commit:** pending.

---

## BH-1.9 — culture-dependent timestamps in analytics and file names

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** on a device whose culture uses a non-Gregorian calendar or non-Latin digits (ar-SA,
  th-TH, fa-IR) the PostHog event timestamp and generated file names carried the wrong year or
  digits. Nothing in the Console.
- **Root cause:** `DateTime.ToString("yyyy-MM-dd...")` and `$"{dt:format}"` with no culture use the
  current culture.
- **Fix:** `CultureInfo.InvariantCulture` on all four sites the handoff listed:
  `PostHogAnalyticsSink` (event timestamp), `AnalyticsServiceFacade` (`timestamp_utc_iso`),
  `ScreenshotDirectorConfigSO.BuildFileName` and `DesktopPlatformServices.TimestampedName`.
- **Not changed (dev tools only):** a similar `DateTime...ToString` stamp exists in
  `PrismExplosionBenchmark`, `LoadInsightReport`, `DiagnosticsHUD`, `ProfilerCsvLogger` and
  `LogControlWindow` (benchmark/diagnostic file names and display text). They are outside the
  handoff list; sweep them if a diagnostic ever needs to be machine-parsed.
- **Verification:** all gate scripts pass; not run in Unity. Retest is on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.7 — combat-hit latch pruned every entry by one window

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the retest and Broadside balance re-check deferred to the handoff revisit list. Repro skipped.
- **Symptom:** a hit with a long per-weapon window (Rhino sword 1.4 s) could pay twice, because its
  latch entry was dropped before its own window ran out.
- **Root cause:** windows are authored per weapon asset, but `VesselCombatHitLatch.Prune` judged
  every entry against the cooldown of whichever call happened to trigger the periodic sweep (every
  128 admissions). A short-window call (Urchin spike 0.12 s) therefore pruned long-window entries
  early. The handoff also said entries could be KEPT past their window, but admission compares
  against the calling asset's own cooldown, so a stale entry only costs memory; only the early-drop
  half was a real gameplay bug.
- **Fix:** `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/VesselCombatHitLatch.cs`
  stores the admitting window on each `Entry` and `Prune(now)` drops an entry only when
  `now - entry.Time >= entry.Window`. `TryAdmit` behaviour is unchanged.
- **Verification:** all gate scripts pass; not run in Unity. Retest and the Broadside balance
  re-check are on the handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.6 — online duel rematch started with the last game's round/turn counters

- **Date:** fixed 2026-10-05; merged 2026-10-05 at Yash's call with the two-peer retest deferred to the handoff revisit list. Repro skipped.
- **Symptom:** Cellular Duel, finish a game, Play Again. The rematch ends early and/or swaps the
  vessels on its very first round.
- **Root cause:** `MultiplayerMiniGameControllerBase.ResetForReplay_ClientRpc` (the in-place replay;
  Cellular Duel is the one mode that does not reload the scene) reset scores and players but never
  `GameDataSO.RoundsPlayed` / `TurnsTakenThisRound`. The server's `SetupNewRound` zeroes the turn
  counter, but `RoundsPlayed` stayed at the old game's value on every peer, so
  `RoundsPlayed >= numberOfRounds` held almost at once and
  `OnlineDuelForTheCellController.SetupNewRound` (`allowSwap = RoundsPlayed > 0`) swapped on round one.
- **Fix:** `Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs` zeroes both
  counters in `ResetForReplay_ClientRpc`, so it runs on every peer. Deliberately NOT
  `GameDataSO.ResetRuntimeDataForReplay`, which also clears `GameConfigSynced` and the spawn poses
  that a live session must keep.
- **Verification:** all gate scripts pass; not run in Unity. Retest steps (two peers) are on the
  handoff playtest list.
- **PR/commit:** pending.

---

## BH-1.5 — friends init latched `_initialized` even when the service failed to start

- **Date:** fixed 2026-10-02; awaiting Yash's retest on `Bug_Hunt`. Repro skipped.
- **Symptom:** if Friends initialization fails once (UGS slow or unreachable at sign-in), friends and
  presence stay dead for the rest of the session. Only a warning from the facade is logged.
- **Root cause:** `FriendsServiceFacade.InitializeAsync` catches its own exceptions and returns, so
  `FriendsInitializer.InitializeFriendsAsync` could not tell failure from success and set
  `_initialized = true`. The guard in that method and in `HandleSignedInEvent` then refused every
  retry.
- **Fix:** `Assets/_Scripts/Controller/Party/FriendsInitializer.cs` now sets
  `_initialized = friendsService.IsInitialized` and returns early (no presence write) when the
  service is not up, so the next sign-in event can retry. The facade already resets its own
  in-progress flag on failure, so a retry is a real second attempt.
- **Verification:** all gate scripts pass; not run in Unity. Retest steps are on the handoff
  playtest list. Same lesson as BH-1.4 (PLAYBOOK §7): a call that swallows failures is not proof of
  success; check the state after it.
- **PR/commit:** pending.

---

## CAM-1 — Sparrow freestyle: camera stops following after the turret stance (partial)

- **Date:** 2026-10-02. **Status: not reproduced; root cause of the stance trigger NOT found.**
- **Symptom (reported):** Menu_Main freestyle, Sparrow, gamepad. Pressing Turret Stance (A) makes
  the camera stop following the ship; exiting to the menu and re-entering freestyle fixes it.
- **What was established:** the Sparrow's camera is hard-attached (`SparrowCameraSettingsSO` has
  no `mode` key, so `FixedCamera`), so a frozen view needs the player rig to have NO follow
  target, be inactive, or have its framed point overridden. Two full read-only traces (forward
  from the stance, backward from the camera) found no path from the stance to any of those.
  The menu round-trip heals it because freestyle entry re-runs
  `CameraManager.SetupGamePlayCameras`, which re-points the rig.
- **Fixed in this neighborhood:**
  - `CameraManager.EndWindowedPlayerCamera` restored `_windowedPreviousTarget` even when no loan
    was running (null), so an unmatched or repeated End (the mode preview calls it from four
    teardown paths) handed the gameplay camera a NULL follow target: exactly this symptom. The
    loan is now balanced. And when the preview swaps the hull while it holds the loan (its tap-in
    and tap-out both do), `SetupGamePlayCameras` now records the NEW hull as the target to give
    back - `End` used to restore the hull captured at `Begin`, which the swap had just destroyed.
  - `CustomCameraController` now reports, once, when the on-screen player rig loses its follow
    target, naming the call stack that cleared it (or that the target was destroyed), and
    re-latches onto `CameraManager.PlayerFollowTarget`. **This is the diagnostic for the next
    repro:** if the stance still breaks the camera, the Console names the culprit.
  - `ScreenSwitcher`'s freestyle input gate now re-applies if anything re-opens
    `sendNavigationEvents` mid-flight (both preview hosts restore it), instead of trusting its
    own "applied" flag.
  - `MainMenuController.HandleMenuReady` is guarded against freestyle, like its camera twin: a
    re-raised `OnClientReady` used to put the hull on autopilot and pause input mid-flight.
  - `ToggleTranslationModeActionExecutor.End` cleared the stance locally but not the replicated
    `n_IsTranslationRestricted`; it now goes through `VesselController.SetTranslationRestricted`.
  - Both `ToggleStationaryModeAction` assets serialized a dead `mode` key, so both ran the
    default (`Serpent`). They now author `stationaryMode` with the value each hull already ran
    (no behaviour change; the Sparrow has no seed assembler, so its branch is identical).
- **Verification:** out-of-editor gates pass (conditional compilation, using directives,
  self-referential locals, duplicate attributes, console logging, abstract members, enum refs,
  switch collisions). **Not compiled or run in the Unity editor.**
- **Retest:** freestyle → vessel changer → Sparrow → gamepad A (stance) on and off, fly. If the
  camera still freezes, copy the `[CustomCameraController] The player camera lost its follow
  target` warning (with its stack) into this entry.
- **Follow-ups seen, not fixed (rows, unmeasured in play):**
  - After a mode-preview tap-out the hull swap runs `SetupGamePlayCameras`, which makes the player
    rig the ACTIVE controller; `EndWindowedPlayerCamera` then skips its `Deactivate` because
    `_activeController == _playerCamera`. So the player rig may stay enabled behind the menu
    camera after a preview. Measure: after tapping out of a card preview, read
    `CameraManager.GetActiveController()` and whether the player rig GameObject is active.
  - `SingleStickVesselTransformer.Initialize` creates a new `CourseObject` GameObject on every
    call and never destroys it (only used until the first `RotateShip`). Measure: count
    `CourseObject` roots in the hierarchy after several vessel swaps.
  - `ControllerButtonPress` gates on an `EventSystem` cached via `FindAnyObjectByType` while
    `ScreenSwitcher` gates `EventSystem.current`. Each of Bootstrap / Authentication / Menu_Main
    authors one root EventSystem (none DDOL in the scene files), so this is only a defect if two
    are ever alive at once. Measure: `FindObjectsByType<EventSystem>` count in Menu_Main at runtime.

---

## BH-1.3 / BH-1.4 — auth scene: timeout off the main thread, and silent sign-in failure

- **Date:** fixed 2026-10-02; retest deferred by Yash on 2026-10-02 and parked on the handoff's revisit/playtest list. Repro skipped at Yash's call.
- **Symptom (1.3):** with a slow or unreachable UGS at boot, the cached-auth timeout expires and the
  auth scene can throw `EnsureRunningOnMainThread` or freeze on the auth screen.
- **Symptom (1.4):** a failed guest or auto sign-in navigates on as if signed in, then waits out the
  whole profile timeout for a profile that never loads.
- **Root cause (1.3):** the timeout is raised by `CancelAfter`'s timer thread. In
  `TrySignInCachedWithTimeoutAsync`, `.AttachExternalCancellation` sat outside `.AsMainThread()` (the
  call used `.AsUniTask()`), so the `catch (OperationCanceledException)` resumed on the timer
  thread and its caller then touched Unity/UI state. `HostConnectionService.WaitForProfileInitAsync`
  has the identical shape.
- **Root cause (1.4):** `AuthenticationServiceFacade` reports a failed sign-in through its
  `OnSignInFailed` event and never throws, so `OnGuestLoginAsync` / `AttemptAutoSignInAsync` carried
  on to `HandlePostAuthFlowAsync` after a failure.
- **Fix:**
  - `AuthenticationSceneController.TrySignInCachedWithTimeoutAsync`: `.AsMainThread()` on the
    success path, and `await MainThreadDispatcher.SwitchToMainThreadAsync()` as the first statement
    of both catch blocks.
  - `HostConnectionService.WaitForProfileInitAsync`: the same switch at the top of its catch.
  - `OnGuestLoginAsync`: after the await, `if (!_facade.IsSignedIn)` shows the sign-in error and
    re-enables the button (the existing `finally`), via a shared `ShowGuestSignInFailed`.
  - `AttemptAutoSignInAsync`: after the await, `if (!_facade.IsSignedIn)` logs and goes to the main
    menu, matching its existing failure behaviour, instead of waiting out the profile timeout.
- **Verification:** all gate scripts pass; not run in Unity. Retest steps are in the handoff
  playtest list and PLAYBOOK §7.
- **PR/commit:** pending.

---

## BH-1.2 — gamepad triggers stayed held across a strategy switch or pause

- **Date:** fixed 2026-10-02; Yash retested on `Bug_Hunt` and it works. Skipped the repro on
  `bleeding-edge` at Yash's call; the cause is clear from the code.
- **Symptom:** hold a gamepad trigger, then touch the keyboard or mouse (the input controller
  hands over to another strategy) or pause. The vessel keeps the trigger's ability held (drift,
  charge, and so on) until the trigger is pressed and released again on the pad. Nothing in the
  Console.
- **Root cause:** `KeyboardInputStrategy` releases held triggers and speed gestures in
  `OnStrategyDeactivated` and `OnPaused`; `GamepadInputStrategy` had neither override. Once it
  stops being the live strategy `ProcessInput` no longer runs, so the release edge
  (`leftJustReleased` and friends) is never raised, and its remembered `prevLeftTriggerActive` /
  `prevRightTriggerActive` stay stale.
- **Repro:** not run on `bleeding-edge`. With a pad connected, hold a trigger, then move the mouse
  or press a key, and check whether the ability stays on.
- **Fix:** `Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs`.
  - Added `OnStrategyDeactivated` (release triggers and speed effects, `ResetInput`, reset state)
    and `OnPaused` (release triggers, zero sticks and analog triggers), mirroring the keyboard.
  - The trigger edge logic moved unchanged into `DispatchTriggers(left, right)`, so a release is
    the same code path as a real let-go (`ReleaseHeldTriggers` calls it with 0, 0).
- **Verification:** all gate scripts pass; Yash retested with a pad and it works. Re-verify steps are
  kept in PLAYBOOK §6 and the handoff playtest list in case it recurs.
- **PR/commit:** pending.

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

- **Date:** fixed 2026-10-02; Yash retested on `Bug_Hunt` and it works. Skipped the repro on
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

- **Date:** fixed 2026-10-02; Yash retested on `Bug_Hunt` and it works.
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
