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
| 23 | **Reconnect from the menu is a valid app-state transition (was §2.4).** `ApplicationStateMachine` refused `MainMenu → Authenticating`, so reconnect logged `Invalid transition` and the state mirror stayed `MainMenu` during the re-boot. The edge is now in the table from the menu and from the in-game states, with tests. Shipped on `Bug_Hunt`. | `ApplicationStateMachine`, `ApplicationStateMachineTests` |
| 24 | **Play Again fade-in is armed before the init delay (was §3.1).** `FadeFromBlackOnReplay` was subscribed to `OnClientReady` after the 1 s `InitDelayMs` wait, so a vessel that became ready earlier left the replay overlay black. It is now armed in `OnNetworkSpawn` and removed on despawn. Shipped on `Bug_Hunt`. | `MultiplayerMiniGameControllerBase` |
| 25 | **Stat report RPCs reject NaN volumes and out-of-turn reports (was §4).** `ReportEnvironmentPrismDestroyed` and `ReportPrismStolen` now use `!(volume >= 0f)`, and the five owner-reported stat RPCs (fauna kill, combat hit, fuses beaten, environment prism destroyed, prism stolen) ignore reports unless a turn is running. Shipped on `Bug_Hunt`. | `Player` |
| 26 | **Bloomrush end-of-round only ranks fielded domains (was §4).** `ResolveWinner` no longer lets an unfielded Jade win a 0-0-0 and strand `_finalResultsSent`, which restarted the round. Shipped on `Bug_Hunt`. | `BloomrushScoringRuleSO` |
| 27 | **BranchingFlora rolls trunk count once before seeding (was §4).** `SeedBranches` no longer re-evaluates `Random.Range` in the for-condition. Shipped on `Bug_Hunt`. | `BranchingFlora` |
| 28 | **Name generator includes the last word of each list (was §4).** `Random.Range` upper bound is exclusive, so `Length - 1` skipped the final entry. Shipped on `Bug_Hunt`. | `NameGenerationData` |
| 29 | **Thumb UI reads touches from the new Input System (was §4).** `ThumbCursor` and `ThumbPerimeter` use `Touchscreen.current` instead of legacy `Input.touches`. Shipped on `Bug_Hunt`. | `ThumbCursor`, `ThumbPerimeter` |

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

---

## 1. Tier 2 — small, local, high value (do these first)

---

## 2. Larger items (need design or several files)

---

## 3. Confirm first

---

## 4. Minor

| Item | Where | Fix |
|---|---|---|
| `PrismTimerManager.CancelScheduledActions` is O(N²) | `PrismTimerManager` | index by prism; only matters on mass cancels |
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
