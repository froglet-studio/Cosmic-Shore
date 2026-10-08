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

## 0. What already shipped (so you do not redo it)

| # | Fix | Where |
|---|---|---|
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

Rows 23-71 shipped on `cece/loving-shannon-hxpdy0` (the 2026-10-08 overnight sweep). **None of them has been run in Unity.** Each compiled clean against the real 6000.0 references with `Tools/Build/unity_refcompile` in the player config (0 errors, 0 unverified - the gate was negative-controlled: a planted undefined call shows up as 1 *unverified*, so read both numbers; the seven changed files that `using` the unobtainable UGS Multiplayer package were checked line by line against the report) and, for the changed tests, the editor config; all five textual gates pass. Per-fix reports: [`BugHunt/FIX_LOG.md`](BugHunt/FIX_LOG.md) § "Overnight sweep 2026-10-08".

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

### Playtest items for rows 23-71 (overnight sweep, none run in Unity)
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
| AI objective distance: `sqr` vs linear | `AIPilot.cs:~755` | **Deliberately NOT fixed** — the behaviour is tuned around it. Change only with a playtest. |

## 5. Follow-ups from the scene cleanup (#5)

- **Neither Wildlife Blitz exists in the shipped game.** `ArcadeGameCoOpWildlifeBlitz.asset` is in no
  `SO_GameList`, and `MinigameWildlifeBlitzMultuplayerCoOp.unity` is not in Build Settings. This was
  already true before the cleanup; it only became visible when the single-player scene was deleted.
  Decide: ship it (add it to a list and to Build Settings) or retire it.
- **`BenchmarkStressTest.unity` is not in Build Settings**, but Settings ▸ Run Benchmark loads it, so
  Run Benchmark probably fails in a player build. Either add it to Build Settings or hide the
  button in players.
- **Hangar training is dead:** `Arcade.Instance` is never placed. Rhino's and Sparrow's
  `SO_TrainingGame_WildLifeBlitz` still point at `ArcadeGameWildlifeBlitz`, whose scene is deleted.
  The card was KEPT so those references do not NRE. Retire the training entries and the card
  together.
- **Orphaned classes** (no scene or prefab references them now): `WildlifeBlitzMiniGame`,
  `SinglePlayerSlipnStrideController`, `VolumeTestPlayerSpawnerAdapter`, `SandboxBenchmarkController`,
  the single-player `VesselSelectionPanelController`, `WildlifeBlitzEndGameStatsTracker`,
  `WildlifeBlitzStats`. Run the `/refactor` skill's salvage-before-delete gate before removing any
  of them. Some are referenced by `BenchmarkStressTest`.

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
