# Bug Hunt Handoff — September 2026

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

### Playtest items for the shipped fixes
- **Squirrel ring (#6):** fly Menu_Main freestyle → an arcade game → back, 2-3 round trips, then
  boost repeatedly. Every ring should lay the full set of prisms, every time.
- **Bloomrush (#4):** the score is now real destroyed VOLUME. It was 0 before, so the numbers will
  look big. Check the 120 s round still reads as a race.
- **Undertow (#1):** the comeback now sees bends AND creature kills (it saw bends alone before).
- **The Bends / Wrecking Ball (#1):** the trailing domain should visibly get comeback buffs. Before
  this fix their scenes read the wrong stat.
- **Any domain mode (#3):** scores read 0 at the instant the countdown ends, never a leftover value.

---

## 1. Tier 2 — small, local, high value (do these first)

### 1.1 AI never releases a held drift when its pilot stops — High
- **Where:** `Assets/_Scripts/Controller/AI/AIPilot.cs`, `StopAIPilot()` (~620) and `OnDisable()` (~303).
- **Bug:** the AI starts a drift through `PerformShipControllerActions(InputEvents.CommitControl …)`.
  `StopAIPilot` and `OnDisable` stop the brain but never send the matching stop.
- **Trigger:** an AI vessel is mid-drift when autopilot is switched off. Examples: Menu_Main
  freestyle entry (the local vessel's autopilot goes off), a vessel swap, a spectator takeover.
- **Consequence:** the vessel stays drifting (course locked, throttle policy of a drift) under the
  human pilot until they tap drift themselves.
- **Fix:** in both methods,
  `if (VesselStatus.IsDrifting) handler.StopShipControllerActions(InputEvents.CommitControl);`.
  Use whichever control the drift is bound to (resolve it the way the press was resolved; see
  `R_VesselActionHandler.TryGetInputForAction<T>`). Mirror `ReleaseHeldInputs` rather than
  inventing a second release path.

### 1.2 Gamepad triggers stay held across a strategy switch / pause — High
- **Where:** `Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs`. Compare with
  `KeyboardInputStrategy.OnStrategyDeactivated` (54) / `OnPaused` (62).
- **Bug:** the keyboard strategy clears held trigger/button state on deactivate and pause. The
  gamepad strategy has no override, so a trigger held at the moment of the switch is never released.
- **Trigger:** hold a trigger, then either plug in a keyboard or move the mouse (the device switch
  hands the family over), or pause.
- **Consequence:** the ability stays held until the trigger is pressed and released again.
- **Fix:** add the same two overrides to `GamepadInputStrategy`, releasing whatever it holds.

### 1.3 Cached-auth timeout resumes off the main thread — High
- **Where:** `Assets/_Scripts/System/AuthenticationSceneController.cs`,
  `TrySignInCachedWithTimeoutAsync` (303).
- **Bug:** `CancelAfter` fires on a timer thread, so the `catch (OperationCanceledException)`
  resumes on the ThreadPool. `.AttachExternalCancellation` sits OUTSIDE `.AsMainThread()`, so shipped
  fix #7 does not cover this path. The caller then touches UI off-thread.
- **Trigger:** a slow or unreachable UGS at boot (the `cachedAuthTimeout` expires).
- **Consequence:** an `EnsureRunningOnMainThread` exception during the auth scene. The player may
  land on a frozen auth screen.
- **Fix:** first statement of both `catch` blocks: `await MainThreadDispatcher.SwitchToMainThreadAsync();`
  (the method must become `async` in the catch; it already is). The same shape exists in
  `HostConnectionService.WaitForProfileInitAsync` (2239) — check its timeout path too.

### 1.4 Guest / auto sign-in treats a silent failure as success — Medium
- **Where:** `AuthenticationSceneController.OnGuestLoginAsync` (356) and `AttemptAutoSignInAsync` (329).
- **Bug:** both await the facade and proceed to `HandlePostAuthFlow` without checking the result.
  The facade reports failure through `OnSignInFailed` rather than throwing.
- **Consequence:** the scene navigates as signed in and then waits on a profile that never loads,
  until the safety timeout.
- **Fix:** after the await, `if (!_facade.IsSignedIn) { show the error / re-enable the button; return; }`.

### 1.5 Friends init latches `_initialized` before the service is actually up — High
- **Where:** `Assets/_Scripts/Controller/Party/FriendsInitializer.cs` (~236-241).
- **Bug:** `_initialized = true` right after `friendsService.InitializeAsync()`. The facade swallows
  its own failures, so a failed init still latches, and the guard at 236 then refuses every retry
  for the session.
- **Fix:** `_initialized = friendsService.IsInitialized; if (!_initialized) return;` (use whatever
  the facade exposes for "ready" — `FriendsDataSO.IsInitialized` is the SOAP mirror).

### 1.6 Online Duel rematch starts with stale round/turn counters — Medium
- **Where:** `MultiplayerMiniGameControllerBase.ResetForReplay_ClientRpc` (793).
- **Bug:** the in-place replay path resets scores but not `RoundsPlayed` / `TurnsTakenThisRound`.
- **Trigger:** Cellular Duel (the one mode that does NOT replay by scene reload) → rematch.
- **Consequence:** the rematch ends early or skips the vessel-swap round.
- **Fix:** zero both counters in the RPC, on every peer.

### 1.7 Combat-hit latch prunes by one global window — Medium
- **Where:** `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/VesselCombatHitLatch.cs`.
- **Bug:** windows are authored PER ASSET (the Rhino sword 1.4 s, the Urchin spike 0.12 s), but the
  prune pass uses a single window. An entry is either dropped before its own window elapses (double
  pay) or kept past it (a legitimate hit refused).
- **Fix:** store the window on the entry and prune `now - t > entry.window`.
- **Note:** Broadside's balance rests on these windows (`BROADSIDE.md`). Re-check the balance model
  after the fix.

### 1.8 `Cell.countGrids` never disposed on destroy — High
- **Where:** `Assets/_Scripts/Controller/Environment/Cell.cs`, `OnDestroy` (1276). The grids are
  allocated around 1833-1838.
- **Bug:** re-initialisation disposes the old grids (1833), but `OnDestroy` does not. Each
  `BlockCountDensityGrid` owns native memory.
- **Consequence:** a native leak per cell per scene load. The editor logs "A Native Collection has
  not been disposed".
- **Fix:** in `OnDestroy`: `foreach (var g in countGrids.Values) g?.Dispose(); countGrids.Clear();`.

### 1.9 Analytics timestamps are culture-dependent — High
- **Where:** `PostHogAnalyticsSink.cs:199`, `AnalyticsServiceFacade.cs:768`. Also sweep
  `ScreenshotDirectorConfigSO.cs` (~523) and `DesktopPlatformServices.cs` (~136).
- **Bug:** `ToString("yyyy-MM-dd…")` with no culture renders in the device's calendar. On ar-SA,
  th-TH and fa-IR that is not Gregorian (see the weekly-challenge finding in CLAUDE.md).
- **Fix:** pass `CultureInfo.InvariantCulture` to every one.

### 1.10 Crystal material lerp leaks two materials per lerp — High
- **Where:** `Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs`, `LerpCrystalMaterialCoroutine` (~788).
- **Bug:** `new Material(renderer.material)`. `renderer.material` already clones, so each call mints
  TWO materials and neither is destroyed.
- **Fix:** `new Material(renderer.sharedMaterial)`, then `Destroy(tempMaterial)` when the lerp
  finishes or the coroutine is stopped.

### 1.11 Non-ASCII glyphs render as tofu — High
- **Where:** `SpectatorOverlay.cs` 125 (`◀`), 138 (`▶`), 150 (`✕`). Also check
  `ToyConfigureModal.cs` (~514), `DogFightScoringRuleSO.cs` (~141), the `·` in the Broadside and
  Undertow scoring-rule labels, and `ToyVariantCard.cs` (~188).
- **Bug:** the only UI font (ALDRICH) has 97 glyphs: ASCII plus nbsp and the ellipsis. See CLAUDE.md,
  "Any non-ASCII character in a string that reaches a TMP_Text".
- **Fix:** use ASCII (`<`, `>`, `X`, `-`), or add the glyphs to the font asset.

### 1.12 A departed player's vessel converted to AI is not marked AI — Medium
- **Where:** `ServerPlayerVesselInitializer.ConvertPlayerToAI` (809).
- **Bug:** the player is handed to autopilot but not recorded the way a spawned AI is (the
  processed-player bookkeeping and `IsInitializedAsAI`).
- **Consequence:** later passes treat it as a human (ready gates, domain normalisation).
- **Fix:** mark it exactly as `ServerPlayerVesselInitializerWithAI` marks its own AI.

### 1.13 Fauna does not unregister from its cell on destroy — Medium
- **Where:** `Fauna.OnDestroy` (424); `Cell.UnregisterSpawnedObject` (1361).
- **Bug:** a creature destroyed outside the normal death path (scene teardown, cell swap, split)
  stays in the cell's spawned-object list.
- **Fix:** `hostCell?.UnregisterSpawnedObject(gameObject);` in `OnDestroy`.

---

## 2. Larger items (need design or several files)

### 2.1 Cloud Save cannot tell "load failed" from "no data yet" — High, highest stakes
- **Where:** `Assets/_Scripts/System/CloudData/Providers/UGSCloudSaveProvider.cs` (~64-117: the
  catches return `null`). Also `CloudDataRepository` and `PlayerDataService`.
- **Bug:** a network/auth error returns the same `null` as a missing key. The repository then
  treats the player as new, seeds defaults, and **saves those defaults over the real cloud record**
  on the next write.
- **Consequence:** progression / unlock / profile wipe on a flaky connection.
- **Fix:** return a result type (`Loaded`, `Missing`, `Failed`). On `Failed`, fall back to
  `LocalCloudDataCache` and **block writes** for that key until a successful load. This touches
  every repository, so plan it as its own PR.

### 2.2 Presence lobby is never rejoined after it drops — Medium
- **Where:** `HostConnectionService.cs`: the `Update` gate (~418), the refresh loop (~1496-1528), and
  join (~537-572).
- **Bug:** once the presence lobby is lost (network blip, lobby expiry), the refresh loop's gate
  stays closed. Nothing re-runs the join.
- **Consequence:** the online list goes empty and invites stop arriving until the app restarts.
- **Fix:** on refresh failure with a lobby-not-found/unauthorised class error, clear the lobby and
  re-enter the join path with backoff. `Docs/PresenceSystem/ARCHITECTURE.md` has the lifecycle.

### 2.3 Invite-clear runs without the property-write mutex — Medium
- **Where:** `HostConnectionService.cs` ~2034 (`needsLock`). Callers: ~757, 1729, 1810, 1915, 2000, 2422.
- **Bug:** some paths clear `invite_payloads` without taking the lock the send path takes, so a
  clear and a send race and one overwrites the other.
- **Consequence:** an invite that silently never arrives, or one that re-fires.
- **Fix:** every write to the per-player invite property goes through the same lock. Audit the six
  callers.

### 2.4 `ApplicationStateMachine` refuses MainMenu → Authenticating — High
- **Where:** `Assets/_Scripts/System/ReconnectService.cs:193` calls
  `TransitionTo(ApplicationState.Authenticating)` from `MainMenu`. The transition table does not
  allow it.
- **Consequence:** reconnect logs `Invalid transition` and the app-state mirror stays `MainMenu`
  while the auth scene runs. Anything keyed on app state misreads the phase.
- **Fix:** add `MainMenu → Authenticating` to the table (reconnect is a legitimate path), and add a
  case to `ApplicationStateMachineTests`.

---

## 3. Confirm first

### 3.1 Play Again (scene reload) may leave the screen black
- **Where:** `MultiplayerMiniGameControllerBase.cs` ~171-179.
- **Suspicion:** `FadeFromBlackOnReplay` is subscribed AFTER the `InitDelayMs` wait, so if
  `OnClientReady` fires inside that window, the fade-from-black is missed.
- **Repro:** Skim Race → finish → Play Again, several times, host and client. If it never sticks
  black, close it.

---

## 4. Minor

| Item | Where | Fix |
|---|---|---|
| Bloomrush restarts a tied 0-0-0 round when Jade is not fielded | Bloomrush end-of-round | tie-break over FIELDED domains only |
| NaN volume accepted by the stat report RPCs | `Player.Report*_ServerRpc` | `if (!(volume >= 0f)) return;` (catches NaN) |
| Report RPCs are not gated on a running turn | same | `if (!gameData.IsTurnRunning) return;` |
| Trunk count re-rolled every iteration | `BranchingFlora.cs:~150` | roll once before the loop |
| Name generator never picks the last word | `NameGenerationData.cs:20-21` | `Random.Range(0, list.Count)` (dead code today) |
| `PrismTimerManager.CancelScheduledActions` is O(N²) | `PrismTimerManager` | index by prism; only matters on mass cancels |
| `Input.touches` under the new Input System | `ThumbCursor.cs:75`, `ThumbPerimeter.cs:86` | `Touchscreen.current` / EnhancedTouch |
| `Trail` ushort index wraps past 65,535 prisms | `Trail` | widen to `int`, or assert; a very long freestyle trail reaches it |
| AI objective distance: `sqr` vs linear | `AIPilot.cs:~755` | **Deliberately NOT fixed** — the behaviour is tuned around it. Change only with a playtest. |

---

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
