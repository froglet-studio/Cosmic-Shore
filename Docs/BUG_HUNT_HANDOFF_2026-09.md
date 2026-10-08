# Bug Hunt Handoff — September 2026

> Workflow, per-fix reports and the troubleshooting playbook: [`BugHunt/README.md`](BugHunt/README.md) · [`BugHunt/FIX_LOG.md`](BugHunt/FIX_LOG.md).

**For:** whoever picks up the rest of the September 2026 low-blast-radius bug hunt.
**Branch that fixed the first seven:** `cece/great-albattani-14izai` (PR against `bleeding-edge`).
**Line numbers:** as of the merge of `bleeding-edge @ 44a9d5fe` into that branch. They will drift;
every item also names the method, so search by name if a number is off.

The hunt looked for bugs where the fix is small and local but the consequence is large — a mode
that silently scores wrong, a button that silently does nothing, a leak that silently degrades a
session. Seven were fixed on the branch above. **Everything below is NOT fixed.** Each item says
what is wrong, how to trigger it, what the player sees, the minimal fix, and how confident the
diagnosis is. Nothing here has been run in the Unity Editor. That includes the seven that were
fixed.

Confidence scale:
- **High:** read end to end; the failure path is certain.
- **Medium:** the defect is certain but the trigger or its frequency is inferred.
- **Confirm first:** a plausible reading that needs a repro before anyone changes code.

---

## Merge log (bleeding-edge → Bug_Hunt)

- **2026-10-06 — merged `bleeding-edge @ 0c48d08f5` (PR #965 tip) into `Bug_Hunt`** (merge commit
  `eb05ce26b`, Bug_Hunt first parent; merge-base `3ba8ea1d2`). Brought in **158 BE commits** (115
  non-merge): swarm fauna (#960) with substrate / builder colonies / threat flora / ecology LOD,
  GPU-drawn swarm members on the prism platform (`PrismRenderService`, `PrismSpatialIndex`,
  `Prism`, `Cell`, `Fauna`, `LightFauna`, `Flora`), objective arrows for Bends / Cleave / Sirocco /
  Wildlife Liberation / Brood Rush / Scurry (#964, `MiniGameHUD`), silent-hull FMOD slots (#966),
  Wildlife / Tollway gates (#967), QA arcade matrix (#968), generator gates (#969), registration
  copy drift (#965), Port / Froglet Engine work.
  - **Conflicts (2):** `Tollway Cell Config 4.asset` — kept BE's new Description (Borromean
    anchors, 78558 vol / 3654 prisms) in BH-5.2's single-quoted folded form
    (`wrap_yaml_scalar`); `author_dogfight_assets.py` — kept both imports (BE `aml`, BH
    `wrap_yaml_scalar`).
  - **No follow-up fix needed:** BE did not touch `EditorBuildSettings` (Wildlife Blitz co-op
    removal stands), no BE code / asset references any BH-5.3..5.7 deleted file or GUID, and
    nothing references `PrismTimerManager.scheduledActions`. BH fixes in BE-touched files
    survived (`Cell.OnDestroy` grid dispose, `Fauna.OnDestroy` unregister,
    `Flora.RemoveHealthBlock` base call, `Crystal.ActivateCrystal` guard).
  - **Gates:** 4 textual gates OK; strict YAML on the 113 changed assets OK; `author_{tollway,
    dogfight,regatta,broadside,waystation}_assets.py --check` OK; `dotnet build
    Port/src/CosmicShore.Player` 0 errors. Not run in Unity. Playtest list for the merged branch:
    the "Playtest items" section below plus the merge-risk checks (swarm / fauna / prism platform,
    objective-arrow HUD).

---

## 0. What already shipped (so you do not redo it)

| # | Fix | Where |
|---|---|---|
| 39 | **PrismTimerManager.OnDestroy clears the owner-indexed schedule (BH-4.7 follow-up).** Left-over `scheduledActions.Clear()` after the rename caused CS0103. | `PrismTimerManager.OnDestroy` |
| 1 | **Comeback now reads the mode's own score.** `ElementalComebackSystem` had a per-scene `ScoreDifferenceSource` that eight cloned scenes set to their donor's stat. The comeback now reads `ScoringRuleSO.DomainValue` through `ElementalComebackSystem.DomainScore`, the same function the HUD, the end condition and the placement order use. The per-scene field is deleted, so the bug class cannot recur. | `ElementalComebackSystem.cs`, `MultiplayerMiniGameControllerBase.OnNetworkSpawn`, 22 scenes, 9 generators |
| 2 | `Flora.RemoveHealthBlock` now calls `base`. It had skipped the lifeform's death bookkeeping. | `Flora.cs` |
| 3 | Stats zero when the countdown ends, not only at the turn start. | `MultiplayerDomainGamesController.OnCountdownTimerEnded_ClientRpc` |
| 4 | Destroyed-prism volume is read before the scale animator is disabled. It used to read 0, so every destruction stat and Bloomrush's whole score were 0. A dead `/ volume` divide was also removed. | `Prism.SetupDestruction`, `Prism.Explode` |
| 5 | The retired single-player Duel for the Cell and Wildlife Blitz scenes are deleted, along with their vestiges. | see the PR |
| 6 | **Pool double-release / handler stacking.** `GenericPoolManager` released an instance twice, which put it in the pool twice. That handed the same prism to two callers. The per-Get `OnReturnToPool += Release` handlers also stacked. This is the likely cause of **the Squirrel's boost-ring prisms losing spawn consistency over a session.** | `GenericPoolManager` + 4 subclasses |
| 7 | `.AsMainThread()` returns to the main thread on the EXCEPTION path too (`try/finally`). A faulted UGS task previously resumed its `catch` block off-thread. | `UniTaskExtensions.cs` |
| 8 | **`Cell.countGrids` disposed on destroy (was §1.8).** Only re-initialisation disposed the density grids, so each destroyed cell leaked 4 grids × 6 persistent NativeArrays (24 allocations) per scene load. `OnDestroy` now disposes them (idempotent) and clears the map. `AddBlock`/`RemoveBlock` use `TryGetValue` so a prism torn down after its cell cannot throw on the emptied map. Shipped on `Bug_Hunt`. | `Cell.OnDestroy`, `Cell.AddBlock`, `Cell.RemoveBlock` |
| 9 | **Crystal colour-lerp material leak (was §1.10).** `LerpCrystalMaterialCoroutine` ran `new Material(renderer.material)`; the `.material` getter clones the renderer's material onto it, and that clone was orphaned by the next assignment, so every colour change leaked one Material (it logs nothing). It now copies `sharedMaterial`, and each fade copy is tracked and destroyed in `OnDestroy` if the crystal dies mid-fade. Shipped on `Bug_Hunt`. | `Crystal.LerpCrystalMaterialCoroutine`, `Crystal.OnDestroy` |
| 10 | **Fauna leaves its cell on destroy (was §1.13).** `Fauna` is not a `LifeForm`, so nothing removed a dead or torn-down creature from `Cell.spawnedLifeForms` (flora does it in `LifeForm.Die`); the dead entry stayed and `LifeFormsInCell` stayed inflated. That count feeds `AllLifeFormsDestroyedTurnMonitor` (used by the Wildlife Blitz co-op scene) and the Wildlife Blitz monitors. `Fauna.OnDestroy` now calls `hostCell.UnregisterSpawnedObject`. Shipped on `Bug_Hunt`. | `Fauna.OnDestroy` |
| 11 | **AI no longer leaves a held drift when stopped (was §1.1).** Already fixed by `4c866f880` (2026-09-26): `AIPilot.StopAIPilot` releases the commit drift, stops every started ability and clears the aim telegraph, and `PilotSwap` releases the hull's held inputs while the server owns it. Verified by Yash in Menu_Main freestyle takeover on `Bug_Hunt`. No code change was needed; `AIPilot.OnDisable` was left alone on purpose (teardown path, vessel is going away). | `AIPilot.StopAIPilot`, `PilotSwap` |
| 12 | **Gamepad triggers released on strategy switch and pause (was §1.2).** `GamepadInputStrategy` had no `OnStrategyDeactivated` / `OnPaused`, so a trigger or speed gesture held when the player touched the keyboard or mouse (or paused) never sent its release and the vessel kept the ability held. It now releases held triggers and speed effects and resets its state, mirroring `KeyboardInputStrategy`; the trigger edge logic moved into a shared `DispatchTriggers`. Shipped on `Bug_Hunt`. | `GamepadInputStrategy` |
| 13 | **Auth scene: cached-auth timeout stays on the main thread, and a silent sign-in failure is no longer treated as success (was §1.3 and §1.4).** `TrySignInCachedWithTimeoutAsync` switches to the main thread in both catches (the `CancelAfter` timer thread used to resume it) and uses `.AsMainThread()` on the success path; `HostConnectionService.WaitForProfileInitAsync` got the same switch. `OnGuestLoginAsync` and `AttemptAutoSignInAsync` now check `_facade.IsSignedIn` after the await: guest shows the error and re-enables the button, auto sign-in goes to the main menu instead of waiting out the profile timeout. Shipped on `Bug_Hunt`. | `AuthenticationSceneController`, `HostConnectionService` |
| 14 | **Friends init no longer latches on a failed start (was §1.5).** `FriendsInitializer.InitializeFriendsAsync` set `_initialized = true` right after `friendsService.InitializeAsync()`, but the facade swallows its own failures, so a failed init still latched and every retry was refused for the session. It now takes `friendsService.IsInitialized` and returns (without setting presence) when the service is not up. Shipped on `Bug_Hunt`. | `FriendsInitializer` |
| 15 | **Online Duel rematch starts from zero round/turn counters (was §1.6).** `ResetForReplay_ClientRpc` reset scores but not `RoundsPlayed` / `TurnsTakenThisRound`, so the in-place replay (Cellular Duel, the one mode that does not reload the scene) started with the last game's counters: the rematch ended early and swapped vessels on its first round. Both are now zeroed on every peer in the RPC. Shipped on `Bug_Hunt`. | `MultiplayerMiniGameControllerBase` |
| 16 | **Combat-hit latch prunes each entry by its own window (was §1.7).** `VesselCombatHitLatch` pruned every entry with whichever call's cooldown triggered the sweep, so a long-window entry (Rhino sword 1.4 s) could be dropped early by a short-window call (Urchin spike 0.12 s) and the same hit paid twice. Each entry now stores the window it was admitted under and is pruned by that. Admission itself is unchanged. Shipped on `Bug_Hunt`; Broadside's balance should be re-checked (see playtest list). | `VesselCombatHitLatch` |
| 17 | **Timestamps formatted with the invariant culture (was §1.9).** `PostHogAnalyticsSink` and `AnalyticsServiceFacade` wrote their ISO-8601 UTC timestamps with the device culture, and `ScreenshotDirectorConfigSO.BuildFileName` and `DesktopPlatformServices.TimestampedName` built file names the same way. On ar-SA, th-TH and fa-IR that renders a non-Gregorian year or non-Latin digits. All four now pass `CultureInfo.InvariantCulture`. Shipped on `Bug_Hunt`. | `PostHogAnalyticsSink`, `AnalyticsServiceFacade`, `ScreenshotDirectorConfigSO`, `DesktopPlatformServices` |
| 18 | **Non-ASCII UI strings replaced with ASCII (was §1.11).** The only UI font (ALDRICH) has 97 glyphs, so `◀ ▶ ✕ › · × ● ◆` rendered as empty boxes. Replaced in `SpectatorOverlay` (buttons and hint), `ToyConfigureModal` (Back), `ToyVariantCard` (branch marker), `DogFightScoringRuleSO` (breakdown now reads `N pts - B rounds, M rockets`), and the Broadside and Undertow scoring-rule labels (`·` became `,`). Shipped on `Bug_Hunt`. | `SpectatorOverlay`, `ToyConfigureModal`, `ToyVariantCard`, `DogFightScoringRuleSO`, `BroadsideScoringRuleSO`, `UndertowScoringRuleSO` |
| 19 | **A departed player's vessel handed to the AI is now marked AI (was §1.12).** `ConvertPlayerToAI` only flipped `NetIsAI`, so the local `Player.IsInitializedAsAI` stayed false on the server and every client. `Player` now follows `NetIsAI` changes (`OnNetIsAIChanged`). Shipped on `Bug_Hunt`. | `Player` |
| 20 | **Cloud Save no longer tells a failed load apart from "no data" (was §2.1).** `ICloudSaveProvider.TryLoadAsync` reports `Loaded`, `Missing` or `Failed`. After a `Failed` load a repository keeps using its local snapshot but does not upload to the cloud until a retry gets a definite answer (`Missing` allows the write; `Loaded` adopts the real record). An unreadable stored value now counts as `Failed`. Shipped on `Bug_Hunt`. | `ICloudSaveProvider`, `UGSCloudSaveProvider`, `CloudDataRepository` |
| 21 | **Presence lobby is rejoined with backoff after a failed reconnect (was §2.2).** After three refresh errors the lobby was cleared and rejoined once; if that one attempt failed the lobby stayed null and nothing retried. `HostConnectionService` now retries from `Update` at 3s, 6s, 12s up to 60s until the lobby is back. Shipped on `Bug_Hunt`. | `HostConnectionService` |
| 22 | **Invite-clear always takes the lobby mutex unless the caller holds it (was §2.3).** The shared `_insideRefreshCycle` flag meant "some refresh is running", so a user cancel, a party-leave callback, or a fire-and-forget clear that outlived its refresh skipped the lock and could race a send. It is replaced by an explicit `callerHoldsLobbyMutex` argument, true only for the one awaited call inside `RefreshPartyMembersAsync`. Shipped on `Bug_Hunt`. | `HostConnectionService`, `LobbyPropertyWriter` (comment) |
| 23 | **Reconnect from the menu is a valid app-state transition (was §2.4).** `ApplicationStateMachine` refused `MainMenu → Authenticating`, so reconnect logged `Invalid transition` and the state mirror stayed `MainMenu` during the re-boot. The edge is now in the table from the menu and from the in-game states, with tests. Shipped on `Bug_Hunt`. | `ApplicationStateMachine`, `ApplicationStateMachineTests` |
| 24 | **Play Again fade-in is armed before the init delay (was §3.1).** `FadeFromBlackOnReplay` was subscribed to `OnClientReady` after the 1 s `InitDelayMs` wait, so a vessel that became ready earlier left the replay overlay black. It is now armed in `OnNetworkSpawn` and removed on despawn. Shipped on `Bug_Hunt`. | `MultiplayerMiniGameControllerBase` |
| 25 | **Stat report RPCs reject NaN volumes and out-of-turn reports (was §4).** `ReportEnvironmentPrismDestroyed` and `ReportPrismStolen` now use `!(volume >= 0f)`, and the five owner-reported stat RPCs (fauna kill, combat hit, fuses beaten, environment prism destroyed, prism stolen) ignore reports unless a turn is running. Shipped on `Bug_Hunt`. | `Player` |
| 26 | **Bloomrush end-of-round only ranks fielded domains (was §4).** `ResolveWinner` no longer lets an unfielded Jade win a 0-0-0 and strand `_finalResultsSent`, which restarted the round. Shipped on `Bug_Hunt`. | `BloomrushScoringRuleSO` |
| 27 | **BranchingFlora rolls trunk count once before seeding (was §4).** `SeedBranches` no longer re-evaluates `Random.Range` in the for-condition. Shipped on `Bug_Hunt`. | `BranchingFlora` |
| 28 | **Name generator includes the last word of each list (was §4).** `Random.Range` upper bound is exclusive, so `Length - 1` skipped the final entry. Shipped on `Bug_Hunt`. | `NameGenerationData` |
| 29 | **Thumb UI reads touches from the new Input System (was §4).** `ThumbCursor` and `ThumbPerimeter` use `Touchscreen.current` instead of legacy `Input.touches`. Shipped on `Bug_Hunt`. | `ThumbCursor`, `ThumbPerimeter` |
| 30 | **PrismTimerManager cancels by owner index (was §4).** Scheduled settle actions are keyed by owner, so mass pool returns no longer scan-and-`RemoveAt` the full list. Shipped on `Bug_Hunt`. | `PrismTimerManager` |
| 31 | **Trail block indices are int, not ushort (was §4).** A freestyle trail past 65,535 prisms no longer wraps the index map. Shipped on `Bug_Hunt`. | `Trail` |
| 32 | **Crystal.ActivateCrystal no longer NREs when the cell is gone (console).** `ActivateCrystal` returns if the scene is unloading and only reparents when `cellData.Cell` is still alive (same Unity-null pattern as `DetachHeartToCell`). Was an open console issue from `Fauna.ReleaseHeart` ← `LightFauna.WitherCoroutine`. Shipped on `Bug_Hunt`. | `Crystal.ActivateCrystal` |
| 33 | **Strict YAML on cell configs / arcade cards / captain SOs (console, was FIX_LOG open).** `arcade_mode_lib.wrap_yaml_scalar` + five `author_*_assets.py` generators; 17 assets re-quoted / hand-fixed. Shipped on `Bug_Hunt`. | generators + captain SOs |
| 34 | **Run Benchmark is Editor-only (was §5).** Button hidden / unwired in players; `LaunchBenchmark` no-ops outside the Editor. `BenchmarkStressTest` stays out of Build Settings. Shipped on `Bug_Hunt`. | `GameSettingsPanelController`, `BenchmarkSceneLauncher` |
| 35 | **Hangar Wildlife Blitz training retired (was §5).** Rhino/Sparrow `TrainingGames` cleared; `SO_TrainingGame_WildLifeBlitz` and `ArcadeGameWildlifeBlitz` deleted. Shipped on `Bug_Hunt`. | class SOs, TrainingGames list |
| 36 | **Wildlife Blitz retired from shipped surfaces (was §5).** Co-op scene out of Build Settings; preview + arcade card deleted. Shipped on `Bug_Hunt`. | Build Settings, ModePreviewLibrary |
| 37 | **Orphans deleted after salvage-before-delete (was §5).** Removed unused `WildlifeBlitzMiniGame`, SlipnStride controller, VolumeTest adapter, `SandboxBenchmarkController`, end-game stats tracker, `WildlifeBlitzStats`; kept Benchmark stack. Shipped on `Bug_Hunt`. | see FIX_LOG BH-5.6 |
| 38 | **Wildlife Blitz co-op leftovers deleted (follow-up to 5.5/5.6).** Removed `MinigameWildlifeBlitzMultuplayerCoOp` scene + `CoOpWildlifeBlitzMiniGame`; cleaned training launcher refs; Settings ARCHITECTURE aligned to BenchmarkStressTest stack. Shipped on `Bug_Hunt`. | see FIX_LOG BH-5.7 |
| 23 | **Reconnect boot chain is a legal transition (was §2.4).** `ApplicationStateMachine` now allows `MainMenu → Authenticating` (ReconnectService re-runs the boot chain from Menu_Main); edit-mode test added. | `ApplicationStateMachine`, `ApplicationStateMachineTests` |
| 24 | **Client report RPCs are turn-gated and reject NaN (was §4).** One `CanCreditReport()` (RoundStats + `IsTurnRunning`) and `IsCreditableVolume()` (finite, ≥ 0) gate every owner-detects / server-records RPC on `Player`. | `Player` |
| 25 | **BranchingFlora rolls its trunk count once; the name generator reaches the last word (was §4).** | `BranchingFlora.SeedBranches`, `NameGenerationData` |
| 26 | **An empty domain no longer wins an all-zero tie (was §4, "Bloomrush restarts a tied 0-0-0 round").** `ScoringRuleSO.ResolveWinner` and Bloomrush skip domains with no RoundStats row (`IsFielded`); tests added. | `ScoringRuleSO`, `BloomrushScoringRuleSO`, `ScoringRuleFieldedDomainTests` |
| 27 | **Suspended ThumbCursor / ThumbPerimeter no longer throw on frame 1 (was §4, `Input.touches`).** Inert until initialized; touch counts read EnhancedTouch. | `ThumbCursor`, `ThumbPerimeter` |
| 28 | **Trail index is `int` (was §4, ushort wrap past 65,535).** | `Trail` |
| 29 | **Play Again (scene reload) no longer misses its fade-in (was §3.1, traced in code).** The handler subscribed after the 1000 ms `InitDelayMs`, but the host's vessel readies after `preSpawnDelayMs` (~200 ms). Now subscribed before the wait; the fade itself still waits for `InitializeGame`. | `MultiplayerMiniGameControllerBase` |
| 30 | **19 network/backend waits are unscaled.** Every non-HOME Menu_Main screen sets `Time.timeScale = 0`; scaled `UniTask.Delay`s in the party/presence/session services, the menu vessel swap, `MultiplayerSetup` and `NetworkMonitor` never completed there (a guest's vessel swap left them shipless; the reconcile retry held `_lobbyMutex` and froze the online list, invites and Leave). | `MenuServerPlayerVesselInitializer`, `HostConnectionService`, `LobbyPropertyWriter`, `PresenceLobbyService`, `PartySessionService`, `AcceptanceSignalService`, `MultiplayerSetup`, `NetworkMonitor` |
| 31 | **A boot-time presence-lobby join failure is retried** (BH-2.2's backoff was only reachable from the refresh watchdog, which never runs without a lobby). | `HostConnectionService.EnsureInitializedAsync` |
| 32 | **Menu vessel swap checks ownership** (`RequireOwnership = false` RPC trusted a client-supplied player id). | `MenuServerPlayerVesselInitializer.SwapVesselAsync` |
| 33 | **`InitializeAfterDelay` is cancelled with its controller** (a destroyed controller raised `InitializeGame` into the next scene). | `MultiplayerMiniGameControllerBase` |
| 34 | **A human Player's `DontDestroyWithOwner` is assigned every pass**, so the menu (which never adopts) clears it: a guest leaving the party from the menu no longer leaves a ghost Player in the host's roster. | `ServerPlayerVesselInitializer` |
| 35 | **Every match paid its placement crystals twice.** `GameCanvas.prefab` carried a second, added `EndGameSequencer` on the EndGameStatsPanel; both raised `OnShowGameEndScreen`, and `Scoreboard.AwardCrystalsToLocalPlayer` had no latch. The duplicate is removed and the award latched once per game. | `GameCanvas.prefab`, `Scoreboard` |
| 36 | **Cellular Duel's in-place rematch shows its end screen** (`EndGameSequencer._isRunning` is now cleared on `OnResetForReplay`). | `EndGameSequencer` |
| 37 | **Pausing a match other players are in no longer freezes time.** The GameCanvas pause button (and Escape / Start) took the single-player path in every live mode; it now takes the multiplayer path whenever another peer is connected. | `PauseMenu` |
| 38 | **The card's per-hull starting elements survive the turn start** (the comeback profile's all-zero initial levels wiped Regatta's and Broadside's handicap tables). | `ElementalComebackSystem` |
| 39 | **`IsLocalDomainWinner` answers for the top domain only** (both duelists got VICTORY and the WinMatch quest). | `GameDataSO` |
| 40 | **Only the server's clock ends a networked timed turn** (client clocks ended the turn early and raised it twice); clients no longer invoke the timer ClientRpc. | `NetworkTimeBasedTurnMonitor` |
| 41 | **Timed rounds run their full duration** (the t=0 loop tick counted as a second: 120 s rounds ended at 119). | `TimeBasedTurnMonitor` |
| 42 | **Regatta's placement order agrees with its winner on a tied team total**; test added. | `RegattaScoringRuleSO`, `RegattaTeamPlayTests` |
| 43 | **Petal loss no longer reads a level low from float32 drift** (`1.0f - 0.1f - 0.1f` floored to 7); `ResourceSystem.ToLevel` adds 1e-4; downward test added. | `ResourceSystem`, `ElementalScalingUnificationTests` |
| 44 | **Engine flare and the slowed-ship broadcast are edge-triggered** (a `.materials` allocation per frame per hull; a ServerRpc + ClientRpc per frame per slowed vessel). | `VesselTransformer` |
| 45 | **Serpent cloak frees its baked mesh and material clones** (leaked per cloak, per peer). | `CloakSeedWallActionExecutor` |
| 46 | **Squirrel tube cleanup never recycles a prism reissued to someone else**; the list is pruned at each lay. | `SquirrelTubeActionExecutor` |
| 47 | **A release stops the action list its press started** (re-resolving by the live device stopped the wrong SOs after a touch→pad switch). | `R_VesselActionHandler` |
| 48 | **The `OnInitializeGame` pass no longer wipes a cell the first crystal already bootstrapped** (registries cleared under a planting spawner). | `Cell.Initialize` |
| 49 | **A destroyed cell retires its colony books** (six static per-cell books leaked on every Menu_Main load). | `Cell.OnDestroy` |
| 50 | **Unity fake-null no longer defeats `??` / `??=`** in HealthPrism/Spindle linking (also parent-safe for a skeleton at the scene root), the stellated super-shield, and four UI `GetComponent() ?? AddComponent()` sites. Builds were unaffected; the Editor diverged. | `HealthPrism`, `Spindle`, `PrismStateManager`, `SegmentSpawner`, `EndGameSequencer`, `VesselHUDController`, weekly-challenge panel/modal |
| 51 | **SwarmFauna's loop stops on destroy; the pose job completes before its inputs are released** (both latent). | `SwarmFauna` |
| 52 | **Vessel audio follows the pilot when a live hull changes hands** (Duel round swap, arena PilotSwap): FMOD listener, engine, drift and boost audio. | `ShipStudioListenerGate`, `ShipAudioController`, `DriftAudioController`, `ProximityBoostAudioController` |
| 53 | **Pad and keyboard face buttons always send their release** (device switch, focus loss). | `GamepadInputStrategy`, `KeyboardInputStrategy` |
| 54 | **Lifting the last thumb releases what the touch held**; touch gets `OnStrategyDeactivated` / `OnPaused`. | `TouchInputStrategy` |
| 55 | **An empty drift-event slot warns once** instead of erroring every frame. | `DriftAudioController` |
| 56 | **An avatar picked before the profile loads is applied for the session** (was dropped). | `ProfileIconSelectView`, `PlayerDataService.ApplyAvatarIdLocally` |
| 57 | **A party invite gets its row when the sender also has a pending friend request.** | `FriendsListPanel` |
| 58 | **`VolumeUI` destroys its per-instance material.** | `VolumeUI` |
| 59 | **A failed immediate progression save is retried** (`SaveAsync` returns false; it does not throw). | `GameModeProgressionService` |
| 60 | **Profile, progression and stats follow their repositories when the data object is replaced** (a recovered failed load or a late sign-in). The profile case uploaded a fresh default over the adopted real record - the loss BH-2.1 exists to prevent. | `PlayerDataService`, `GameModeProgressionService`, `UGSStatsManager` |
| 61 | **`DataAccessor` saves atomically, sets a corrupt file aside, writes UTF-8** (a mid-write kill used to wipe painting progress). | `DataAccessor` |
| 62 | **No crystal spend on a hangar unlock that cannot persist** (backend gate closed). | `VesselUnlockSystem` |
| 63 | **Daily toy reward, paid episode-token grants and token purchases wait for the profile to load** (pre-load grants were discarded by the merge; a paid grant was reported successful and lost). | `DailyToyActivity`, `EpisodeTokenService`, `EpisodeTokenController` |
| 64 | **`ClientPrefs.SetAvailableProfiles` writes under its key.** | `ClientPrefs` |
| 65 | **Cellular Duel's round swap is applied on every peer** (it ran on the host only). | `OnlineDuelForTheCellController` |
| 66 | **Legacy metric scorers are per-player** (Duel totals included the opponent's numbers). | `BaseScoring` + 7 scorers, `BaseScoreTracker` |
| 67 | **`LifeFormsInCell` has one writer** (StatsManager double-decremented each flora death). | `StatsManager` |
| 68 | **Two more tofu glyphs** (`×` in the resolution dropdown, `·` in the connecting status line). | `GameSettingsPanelController`, `ConnectingPanelController` |
| 69 | **Seven serialized enums carry explicit values** (assigned in declaration order, so nothing serialized moves). | `StatModuleSO`, `AICinematicBehavior`, `CameraSettingsSO`, `ShapeDefinition`, `SpawnableLSystem`, `RewardData` |
| 70 | **Undertow's winner banner names the teammate who contributed most** (the representative is ordered with the rule's kill weighting). | `UndertowController` |
| 71 | **`IsPartyClient`'s comment no longer claims `IsPartyHost` is never written.** | `ArcadeConfigSyncManager` |
| 72 | **A stopped Serpent wall seed stops searching** (`WallAssembler`'s inner loop ignored `isStopped` and its handle was never kept, so every cloak left a loop stamping and pulling prisms within 40u forever). | `WallAssembler` |
| 73 | **Conic blasts (Dolphin cone, Sparrow skyburst) subscribe to turn end / replay reset and free their material.** | `AOEConicExplosion` |
| 74 | **`AOEBlockCreation` (Manta Kabloom) retires itself and never `Destroy`s pooled prisms on replay reset.** | `AOEBlockCreation` |
| 75 | **An abandoned weekly run cannot finish against the next ordinary match** (quit-to-menu cleared nothing; the attempt ticked, completed and submitted a leaderboard time in another mode). | `WeeklyChallengeService`, `ArcadeGameConfigureModal` |
| 76 | **Astro League's celebration slow-mo / hitstop no longer un-pause a paused solo match.** | `AstroLeagueController`, `AstroLeagueBall` |
| 77 | **Scarab Scramble clients see the cell-overload toast** (subscribed only in the server-only forge hooks). | `ScarabScrambleController` |
| 78 | **The weekly leaderboard refetches on every open** (its work ran in `OnEnable`, once per menu load, because closing only hides the CanvasGroup). | `WeeklyChallengeLeaderboardModal` |
| 79 | **A reused leaderboard row no longer shows the previous player's avatar.** | `WeeklyChallengeLeaderboardPanel` |

Rows 23-79 shipped on `cece/loving-shannon-hxpdy0` (the 2026-10-08 overnight sweep). **None of them has been run in Unity.** Each compiled clean against the real 6000.0 references with `Tools/Build/unity_refcompile` in the player config (0 errors, 0 unverified - the gate was negative-controlled: a planted undefined call shows up as 1 *unverified*, so read both numbers; the seven changed files that `using` the unobtainable UGS Multiplayer package were checked line by line against the report) and, for the changed tests, the editor config; all five textual gates pass. Per-fix reports: [`BugHunt/FIX_LOG.md`](BugHunt/FIX_LOG.md) § "Overnight sweep 2026-10-08".

### Playtest items for the shipped fixes
- **Squirrel ring (#6):** fly Menu_Main freestyle → an arcade game → back, 2-3 round trips, then
  boost repeatedly. Every ring should lay the full set of prisms, every time.
- **Bloomrush (#4):** the score is now real destroyed VOLUME. It was 0 before, so the numbers will
  look big. Check the 120 s round still reads as a race.
- **Undertow (#1):** the comeback now sees bends AND creature kills (it saw bends alone before).
- **The Bends / Wrecking Ball (#1):** the trailing domain should visibly get comeback buffs. Before
  this fix their scenes read the wrong stat.
- **Any domain mode (#3):** scores read 0 at the instant the countdown ends, never a leftover value.
- **Gamepad held triggers (1.2):** with a pad, hold a trigger then move the mouse or tap a key, and
  the ability/drift must end; hold a trigger and pause, and it must release, and the pad must work
  after resume. Re-check this if a held ability ever sticks after an input switch.
- **STILL TO TEST (revisit): 1.3 and 1.4 were pushed on `Bug_Hunt` (`6d16219`) but not yet retested in Unity.** Run
  the next two items before merging them, or whenever the auth scene is next touched.
- **Auth timeout (1.3):** set `cachedAuthTimeout` to ~0.1 s on the auth scene controller (or go
  offline with a cached session) and boot. After "Cached auth timed out" it should carry on to the
  auth panel or main menu with no `EnsureRunningOnMainThread` error and no frozen screen.
- **Silent sign-in failure (1.4):** with no network and no session, press Guest. It should show the
  sign-in error and re-enable the button immediately, not sit on "Loading profile…" until the
  profile timeout. Re-check this if the auth scene ever hangs after a failed sign-in.
- **Friends init retry (1.5):** boot with no network / UGS Friends unreachable, then restore the
  connection and sign in again (or trigger the sign-in event). Friends should initialize on the
  second attempt, and the log should show "Friends service did not come up" for the first one.
- **STILL TO TEST (revisit): 1.6 was merged without a two-peer retest** (and 1.3/1.4 above). Run the next item.
- **Online Duel rematch (1.6):** play a full Cellular Duel with two peers, then Play Again (rematch).
  The rematch must play the full set of rounds/turns, and its first round must NOT swap vessels.
  Re-check this if a rematch ever ends early or starts swapped.
- **STILL TO TEST (revisit): 1.7 was merged without a retest or the Broadside balance re-check.** Run the next item.
- **Combat-hit latch (1.7):** fight with a long-window weapon (Rhino sword) while a short-window one
  (Urchin spike) is also landing hits in the same match. One sword swing must pay once per 1.4 s
  window, never twice. Re-check the Broadside balance model (`BROADSIDE.md`) against the windows.
- **STILL TO TEST (revisit): 1.9 was merged without a retest.** Run the next item.
- **Culture-safe timestamps (1.9):** set the device/Editor culture to ar-SA, th-TH or fa-IR, then
  trigger an analytics event, a screenshot and a share. The PostHog payload timestamp must read like
  `2026-10-05T12:00:00.000Z` and the file names must use Latin digits and the Gregorian year.
- **STILL TO TEST (revisit): 1.11, merged untested at Yash's call.**
- **No tofu in UI text (1.11):** open the spectator overlay (buttons, hint line, Leave), the Toy
  configure modal (Back, and a branching variant card), and finish a Dogfight, Broadside and
  Undertow round (scoreboard breakdown lines). Every character must be a real glyph, no empty boxes.
- **STILL TO TEST (revisit): 1.12, merged untested at Yash's call.**
- **Departed pilot becomes AI (1.12):** start a 2-device match (host plus one client), have the
  client quit mid-round. The ship must keep flying under the AI, its object name must change to
  `AI`, and at the next round reset it must be restarted by the AI (not left idle). Watch for the
  host console showing no new errors.
- **STILL TO TEST (revisit): 2.1, merged untested at Yash's call.**
- **Cloud Save failed load (2.1):** (a) normal boot online: profile, hangar, settings and
  progress load as before, and a change you make is still saved (check the cloud record or relaunch).
  (b) With a signed-in account that has progress, start the game with the network cut (or block
  Unity Services), make a change, then restore the network and wait a minute or play on: the
  console may show "adopting it instead of uploading local data", and the original progress must
  still be there. It must never reset to a new-player state. (c) A fresh account still saves its
  first changes.
- **STILL TO TEST (revisit): 2.2, merged untested at Yash's call.**
- **Presence lobby rejoin (2.2):** needs two online players. Start both on Menu_Main and confirm
  each sees the other in the online list. Cut one machine's network for about 30 seconds (long
  enough for three failed refreshes, then a failed rejoin), then restore it. Within about a
  minute the console should show "Presence lobby rejoined", the online list should refill on both
  sides, and an invite sent after that should arrive. Before the fix the list stayed empty until
  a restart.
- **STILL TO TEST (revisit): 2.3, merged untested at Yash's call.**
- **Invite clear vs send (2.3):** with two players, send an invite and cancel it straight away,
  then send it again; the second invite must arrive and fire once. Repeat while the other player
  joins or leaves the party. Also let an invite time out, then re-invite. No stuck or doubled
  invites, and no hang on the invite button (a hang would mean a lock deadlock).
- **STILL TO TEST (revisit): 2.4, kept on Bug_Hunt untested at Yash's call (no merge to bleeding-edge until all fixes are tested together).**
- **Reconnect app state (2.4):** in Menu_Main (and again from the disconnect notice mid-game or on the game-over screen), trigger Reconnect (the online status indicator or the
  disconnect notice). The console must not show `Invalid transition: MainMenu → Authenticating`;
  with the `Boot` channel verbose you should see `[AppState] MainMenu → Authenticating`, then
  `Authenticating → MainMenu` once the menu is back. The menu should still load as before.
- **STILL TO TEST (revisit): 3.1, kept on Bug_Hunt untested at Yash's call.**
- **Play Again fade (3.1):** in Skim Race (host plus a client if you can), finish a round and press
  Play Again several times in a row. Every peer must fade back in to the arena each time; the
  screen must never stay black. Also quit to the menu right after pressing Play Again once, and
  start a normal game: it must not fade oddly.
- **STILL TO TEST (revisit): 4 (stat report RPC guards), kept on Bug_Hunt untested at Yash's call.**
- **Stat reports (§4):** play a multiplayer match with a client in a mode that scores kills or hits
  (Dogfight, Rampage or a flora mode). The client's scores must still rise during the round, as
  before. After the round ends, the final scoreboard must not change. A normal match is all that is
  needed; a NaN cannot be sent without a hacked build.
- **STILL TO TEST (revisit): 4.1 Bloomrush fielded-domain tie-break, kept on Bug_Hunt untested at Yash's call.**
- **Bloomrush 0-0-0 (§4.1):** start Bloomrush with only Ruby and Gold fielded (no Jade). Let the
  round end with no blooms scored. It must declare a winner (Ruby, by enum order among fielded
  domains) and show the scoreboard — not restart the round. A normal two-domain match that does
  score should still rank by volume, then fuses beaten.
- **STILL TO TEST (revisit): 4.4 BranchingFlora trunk roll, kept on Bug_Hunt untested at Yash's call.**
- **BranchingFlora trunks (§4.4):** in a mode with BranchingFlora (Skim Race / freestyle flora),
  watch a few flora seed: trunk counts should stay between min and max, with no runaway branching.
- **STILL TO TEST (revisit): 4.5 name generator last word, kept on Bug_Hunt untested at Yash's call.**
- **Name generator (§4.5):** if anything still calls `NameGenerationData.GenerateName`, confirm the
  last adjective and noun in the lists can appear. Otherwise this is a correctness fix with no play path.
- **STILL TO TEST (revisit): 4.6 thumb touch input, kept on Bug_Hunt untested at Yash's call.**
- **Thumb touch (§4.6):** only matters if the thumb cursor/perimeter scripts are re-enabled. On a
  touch device, a finger down should light the active sprite / perimeter; lifting should clear it.
- **STILL TO TEST (revisit): 4.7 follow-up OnDestroy clear (compile fix).**
- **STILL TO TEST (revisit): 4.7 PrismTimerManager cancel, kept on Bug_Hunt untested at Yash's call.**
- **Prism timers (§4.7):** play a mode that shields/settles many prisms, then leave the scene or
  destroy a large patch. No hitch spike unique to that teardown; shield settle still fires on time.
- **STILL TO TEST (revisit): 4.8 Trail ushort wrap, kept on Bug_Hunt untested at Yash's call.**
- **Trail indices (§4.8):** only reachable on a very long freestyle trail. Normal play is enough to
  confirm nothing regressed; a wrap repro needs 65k+ prisms.
- **STILL TO TEST (revisit): 5.1 Crystal.ActivateCrystal teardown guard, kept on Bug_Hunt untested at Yash's call.**
- **Crystal heart drop (5.1):** in any mode with fauna (Rampage / freestyle), kill or starve a few
  creatures, then leave the scene / stop play. Console must not show `NullReferenceException` in
  `Crystal.ActivateCrystal`. Hearts that drop while the cell is still live must still become
  collectible.
- **STILL TO TEST (revisit): 5.2 strict YAML assets, kept on Bug_Hunt untested at Yash's call.**
- **Strict YAML (5.2):** reimport the touched cell configs / Broadside / Waystation / three captain
  SOs in the Editor; console must show no `Unable to parse file`. Spot-check Description / Flavor /
  IconActive still display on those cards.
- **STILL TO TEST (revisit): 5.3 Run Benchmark Editor-only, kept on Bug_Hunt untested at Yash's call.**
- **Run Benchmark (5.3):** in the Editor, Settings ▸ Run Benchmark from Menu_Main still loads
  `BenchmarkStressTest`. In a player build the button must be absent/hidden and must not appear in
  the settings panel.
- **STILL TO TEST (revisit): 5.4 Hangar Wildlife Blitz training retirement, kept on Bug_Hunt untested at Yash's call.**
- **Hangar training (5.4):** open the hangar for Rhino and Sparrow — no Wildlife Blitz training
  card/row; remaining training games (if any) still open without NRE.
- **STILL TO TEST (revisit): 5.5 Wildlife Blitz retirement, kept on Bug_Hunt untested at Yash's call.**
- **Wildlife Blitz retired (5.5):** Arcade / Arena / party lists must not show Wildlife Blitz; Build
  Settings must not list `MinigameWildlifeBlitzMultuplayerCoOp`; mode preview must not offer it.
  Editor Run Benchmark (5.3) must still load `BenchmarkStressTest`.
- **STILL TO TEST (revisit): 5.6 orphan deletions, kept on Bug_Hunt untested at Yash's call.**
- **Orphans / Benchmark (5.6):** Editor Run Benchmark must still enter `BenchmarkStressTest` and
  fly. Console must not miss scripts for deleted orphans. MiniGameHUD Ready must not log a missing
  `WildlifeBlitzMiniGame` target.
- **STILL TO TEST (revisit): 5.7 co-op leftover deletion, kept on Bug_Hunt untested at Yash's call.**
- **Co-op leftovers gone (5.7):** project must not contain
  `MinigameWildlifeBlitzMultuplayerCoOp` or `CoOpWildlifeBlitzMiniGame`; Editor Run Benchmark (5.3)
  must still load `BenchmarkStressTest` and find `SinglePlayerWildlifeBlitzController`.

### Playtest items for rows 23-79 (overnight sweep, none run in Unity)
- **Crystal payout (35):** finish any match and note the wallet before and after - it must rise by the placement payout ONCE, the GameEnd sound and reveal toast play once, and only one crystals-earned event is sent.
- **Party pause (37):** host plus one client in any arcade mode. Host presses Escape: the client keeps flying normally and the host's AI keep moving. Client presses Escape: other vessels keep moving on the client's screen. A solo match still freezes on pause.
- **Play Again fade (29):** Skim Race / Bloomrush → finish → Play Again, host and client, three times. The screen must fade in every time and the arena should be built (not popping) when it does.
- **Menu screens with a party (30, 31):** with a guest in the party, sit on ARK / HANGAR / PORT and (a) have the guest swap vessels, (b) have the guest leave, (c) send an invite. Each must complete while you stay on that screen. Also boot with UGS Lobby blocked, then unblock: the online list fills within a minute.
- **Duel (36, 39, 65, 66):** a full two-peer Cellular Duel. Round 2: the remote pilot flies the swapped hull with their own input, HUD, camera and engine sound. Only the winner sees VICTORY. Play Again: the rematch reaches its end screen.
- **Regatta / Broadside handicaps (38):** check the per-hull starting petals are still there after the countdown.
- **Timed rounds (40, 41):** Bloomrush / Tapestry: the clock starts at 120 (not 119), ends at 0, and a client never sees the turn end before the host.
- **Vessel audio after a swap (52):** arena PilotSwap into an AI hull: 3D sounds come from your new hull, you hear your new hull's engine (and not the old one), drift/boost audio and drift haptics work.
- **Input releases (47, 53, 54):** hold B (Sparrow boost) and move the mouse; hold Space and Alt-Tab; on a phone, lift one thumb (Dolphin drift) then the other. The ability must end each time.
- **Cell bootstrap (48):** in SkimRace / Rampage, the first wave of flora is counted once (no double floor of plants a second after load).
- **Editor flora (50):** in Editor play, graze the leaves off a branch - the branch must evaporate (it did in builds only).
- **Profile edge cases (56, 60, 63):** boot offline, pick an avatar (it shows everywhere for the session); boot with a fresh install while Cloud Save is blocked, unblock, earn crystals - the real record's crystals and name must survive.
- **Painting progress (61):** paint, kill the process mid-session, relaunch - progress is intact.
- **Weekly challenge (75, 78, 79):** start a weekly run, quit via Pause > Main Menu, then play an ordinary match of another mode to the end - the weekly card must NOT show completed and no leaderboard time is submitted. Open the weekly leaderboard twice in one menu visit (after the first fetch failed offline, then online): the second open refetches and animates in; switch World/Friends - no face appears beside the wrong name.
- **Serpent cloak (72):** cloak a few times near enemy trails; nearby prisms must not keep resizing/being pulled after the cloak ends.
- **Astro League (76):** solo match, score a goal and open the pause menu during the celebration slow-mo - the match stays frozen until Resume.
- **Scarab Scramble (77):** two peers, get four balls into one cell - the client sees the overload toast.

---

## 1. Tier 2 — small, local, high value (do these first)

Nothing open at this tier. Everything found by the 2026-10-08 sweep that was small and certain shipped (rows 23-68).

---

## 2. Larger items (need design, a playtest, or several files)

### 2.5 Rename migration drops a value on a key collision — Medium
- **Where:** `GameModeRenameMigration.MigrateKeys` keeps the NEW key on a collision (`if (!map.ContainsKey(newKey))`), by design and pinned by `MigrateKeys_ReKeysAndPrefersTheNewKeyOnCollision`.
- **Problem:** two historical names that map to one current name (Ribcage and PeelTheCage both → Cleave), or a record holding both names, keeps one and drops the other - losing a higher `MaxUnlockedIntensity` or a play count.
- **Fix (design call):** merge on collision per field: `Math.Max` for intensity, sum for play counts, better-of for best stats. Changes the pinned test's contract.

### 2.6 Boid cohesion pulls toward world origin — Medium (playtest first)
- **Where:** `Boid.CalculateBehavior` (~:401): `cohesion = (cohesion - transform.position).normalized` subtracts an absolute position from a weighted direction sum, and divides by `prismCount - 1` (all prisms in radius, not boids).
- **Consequence:** every boid gets a constant `cohesionWeight` pull toward world origin - out of any off-origin cell (Arkway traversal cells, satellites).
- **Fix:** count boid neighbours separately and `cohesion = (cohesion / boidCount).normalized`, no `- transform.position`. Steering may be tuned around the current term (ecology is LOCKED - use `/ecology`), so playtest before changing.

### 2.8 Final-score ClientRpcs match players by DISPLAY NAME — Medium
- **Where:** `SyncFinalScores_ClientRpc` in Undertow, Tapestry, Dustup, Sirocco, Broadside, AstroLeague, GateRace (`GateRaceController`), Hijack, ScarabScramble, Tollway, Salvo, DogFight, WildlifeLiberation (and Bloomrush): `RoundStatsList.FirstOrDefault(s => s.Name == sName)`. `GameDataSO.RemovePlayerData(name)` shares the weakness.
- **Problem:** two players with the same name (two humans with one display name, or a human named like an AI profile) both match the FIRST entry: it takes the second player's Score/metric/Domain and the second is never written; on the host the Score write replicates, so a domain can vanish from Results and placement, and placement crystals go to the wrong side.
- **Fix:** skip the loop on the host (its values are authoritative); on clients match each entry once (`HashSet<IRoundStats> used`); durable fix is to send the RoundStats `NetworkObjectId` and match on it. Thirteen near-identical edits - do it as one change with a shared helper.

### 2.7 `PrismTimerManager.CancelScheduledActions` is O(N²) on mass cancels (was §4)
- Deliberately NOT changed overnight: CLAUDE.md says profile first. A per-owner count map would make the common "nothing scheduled" cancel O(1). Profile a mass explosion first.

---

## 3. Confirm first

### 3.2 Rhino's DriftAudioController is gated to the Squirrel
- `Rhino.prefab` ~2949: `restrictToVesselClass: 1`, `targetVesselClass: 6` (Squirrel), with the Squirrel's drift event borrowed. The Rhino gets no drift audio and no drift haptics. Design call: open the gate (and empty the borrowed slot per the FMOD convention) or delete the component.

### 3.3 Joust and Scurry scenes still carry a `NetworkScoreTracker` with `golfRules: 0`
- 500 ms after game end its `SendRoundStats_ClientRpc` re-sorts `RoundStatsList` / `DomainStatsList` as points (these modes are golf-timed), sets `IsGolfRules = false` and raises `OnWinnerCalculated` again. Nothing visible today (the scoreboard reads `Results` / `WinnerDomain`; the duplicate sequencer that would have re-run the reveal is gone, row 35). Set `golfRules: 1` or remove the trackers.

### 3.4 Non-ASCII in user-facing default strings
- Several serialized defaults and labels use `—` and `…` (e.g. `DisconnectNoticeConfigSO` "Reconnecting…"). Whether the ALDRICH font's TMP fallback covers them is an Editor check; fix 1.11 found `· ×` did NOT render. Look at one disconnect notice on screen.

---

## 4. Minor

| Item | Where | Fix |
|---|---|---|
| Hangar UNLOCK button live while the hangar quest is locked (dormant behind the developer unlock) | `HangarVesselDetailView.RefreshLockState` / `OnUnlockClicked` | require `IsVesselHangarUnlocked()` for `interactable`, show LOCKED |
| `DailyRewardCard` never flips back at midnight | `DailyRewardCard.Update` (`secondsUntilMidnight > 0` always true); credits the legacy CatalogManager | retire with the legacy inventory |
| `TrainingGameProgressSystem.ReportProgress` NREs on first call | reads `Progress` without `LoadProgress()`; hangar training is dead (§5) | retire with hangar training |
| Legacy `UnityEngine.Input` in unreachable code | `PhoneFlipDetector` (disabled in Menu_Main; `DeviceOrientationHandler` covers it), `ArcadeProfileWidget` (no referrer), `CaptureScreenShot` (`#if UNITY_EDITOR`, no referrer) | delete or port to `Accelerometer.current` / `Keyboard.current` if revived |
| `??=` on serialized impactor fields | `NetworkVesselImpactor`, `VesselImpactor`, `MineImpactor`, `PrismImpactor`, `CrystalImpactor` | latent: every asset assigns them; use explicit Unity-null tests if one ever ships unassigned |
| Conic blasts ignore `DevastatingOverride` / `DurationOverride` | `AOEConicExplosion.Initialize` replaces the base and never applies them | apply both (changes cone tuning - decide with design) |
| Kabloom's "flower" never blooms | `AOEFlowerCreation.FlowerAsync` is only reached from `BeginExplosion`, which nothing calls; Kabloom lays `AOEBlockCreation`'s three rings | design: is the ring the intended visual? |
| Grizzly puppetry allocates a `List` per frame | `BufoAnimation.PerformShipPuppetry` (`new List<Transform>{...}`) | cache the thruster array in `AssignTransforms` (profile first; file is on an active Grizzly branch) |
| Wall assembler stamped on pooled prisms is never removed | `SeedAssemblerActionExecutor.EnsureAssembler` (Gyroid kind also maps to `WallAssembler`) | remove on stop, or pool-reset it |
| A departed player's RoundStats removal is dead code | `MultiplayerDomainGamesController.OnPlayerLeavingFromSession` parses a UGS id with `ulong.TryParse` | delete it - "fixing" the parse would wipe the departed pilot's team score that the AI takeover promises to keep |
| Waystation AI fold may teleport past a ring | Waystation fold aims at the ring plane; a teleport never threads a gate | confirm in play |
| AI objective distance: `sqr` vs linear | `AIPilot.cs:~755` | **Deliberately NOT fixed** — the behaviour is tuned around it. Change only with a playtest. |

## 5. Follow-ups from the scene cleanup (#5)

_All items from this section are done (BH-5.3–5.7). See §0 rows 34–38._


---

## 6. How to work these

- One item per commit. Each fix is small, and a reviewer should be able to read each one alone.
- Run `python3 Tools/Build/check_enum_member_references.py --check`,
  `check_using_directives.py --check`, `check_conditional_compilation.py` and
  `check_self_referential_locals.py --all` before pushing. They are syntax/textual gates, not a
  compile.
- **Compile in the Editor.** Nothing in this document, and none of the seven shipped fixes, has been
  compiled or run.
- When you finish an item, delete its row here (or move it to §0) so this document stays the list
  of what is left.
