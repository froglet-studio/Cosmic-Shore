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
- **Combat-hit latch (1.7):** fight with a long-window weapon (Rhino sword) while a short-window one
  (Urchin spike) is also landing hits in the same match. One sword swing must pay once per 1.4 s
  window, never twice. Re-check the Broadside balance model (`BROADSIDE.md`) against the windows.

---

## 1. Tier 2 — small, local, high value (do these first)

### 1.9 Analytics timestamps are culture-dependent — High
- **Where:** `PostHogAnalyticsSink.cs:199`, `AnalyticsServiceFacade.cs:768`. Also sweep
  `ScreenshotDirectorConfigSO.cs` (~523) and `DesktopPlatformServices.cs` (~136).
- **Bug:** `ToString("yyyy-MM-dd…")` with no culture renders in the device's calendar. On ar-SA,
  th-TH and fa-IR that is not Gregorian (see the weekly-challenge finding in CLAUDE.md).
- **Fix:** pass `CultureInfo.InvariantCulture` to every one.

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
