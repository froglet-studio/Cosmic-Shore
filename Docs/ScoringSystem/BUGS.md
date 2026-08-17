# Scoring System — Open Issues

Correctness issues found while documenting the Scoring System. These are
**candidates** surfaced from reading the source; confirm the repro before
fixing. Fix order follows `REFACTOR.md` discipline (one per commit, no
regressions). Read `ARCHITECTURE.md` first.

## Status legend
🔴 open · 🟡 partially mitigated · 🟢 fixed (verify only) · ⚪ deferred

---

### B1 — 🟢 `PlayerScoreCard` empty DataPanels background never re-hides
`ShowSecondaryStat` activated `dataPanelsRoot`, but `HideSecondaryStat` only hid
`secondaryStatText` — so the `DataPanels` background (it has a CanvasRenderer +
Image, defaults active) stayed visible behind cards with no extra stats. **Done**
(commit `47bf46c1`). A naive "also hide the root" is wrong: `crystalRewardRoot`
(`CrystalScore`) is a **child** of `dataPanelsRoot` (`DataPanels`) in the prefab,
so hiding the root would make a winner's "+N" reward invisible whenever they had
no secondary stat. Fix drives the shared root from **both** children via
`RefreshDataPanelsRoot()` (visible iff secondary stat OR crystal reward is
showing), called from all four Show/Hide methods so it's order-independent.
File: `_Scripts/UI/PlayerScoreCard.cs`.

### B2 — 🟢 Joust "jousts left" differs between reveal and scoreboard
The end-game **score reveal** computed a loser's remaining jousts as
**individual** (`needed - localStats.JoustCollisions`) while the **final
scoreboard** computed it as the **domain deficit**
(`needed - SumJoustCollisionsByDomain(domain)`), so in team games a losing player
could see one number on the reveal and a different one on the scoreboard.
Canonical = **domain deficit**. **Done** as part of `REFACTOR.md` R10 (commit
`7ea5b8ae`): both surfaces now read the rule's `ScoreResult` — `JoustScoringRuleSO`
builds the loser line as the domain deficit once (`BuildResults` + `BuildReveal`),
so they can't diverge. The per-mode `MultiplayerJoustEndGameController` /
`MultiplayerJoustScoreboard` overrides were deleted.

### B3 — 🟢 `scoreboardRowStagger` is dead config
`HUDAnimationSettingsSO.scoreboardRowStagger` was never read — `PlayerScoreCard`'s
entrance staggers by `cardEntranceStagger`, so tuning it did nothing. **Done**
(commit `b0d30871`): deleted the unused field. Pure removal, no behavior change.
File: `_Scripts/UI/HUDAnimationSettingsSO.cs`.

### B4 — 🟢 Two crystal-reward amounts can double-award / drift
The winner reward existed twice: `Scoreboard.winnerCrystalReward` (winner-only,
`AwardCrystalsIfLocalWinner`) and `EndGameCinematicController.crystalsPerGame`
(awarded with **no** winner check in `AwardCrystalReward`, skipped only while
`delegateCrystalRewardToScoreboard == true`). A scene flipping the flag to `false`
with `winnerCrystalReward > 0` would award the local winner twice (and the two
amounts could drift). **Resolved by `REFACTOR.md` R3** (commit `ed6517ab`): the
cinematic award path + flag were removed, leaving one value + one award path — the
double-award is now structurally impossible.
Files: `_Scripts/UI/Scoreboard.cs`,
`_Scripts/Utility/DataContainers/EndGameCinematicController.cs`.

### B5 — ⚪ Winner-delta recomputed independently per surface
The "won/lost by N" delta is computed independently inside each mode's
`EndGameCinematicController` and again (for ordering/format) in each scoreboard.
If a mode changes its scoring formula, the two can disagree. **Tracked by
`REFACTOR.md` R10** (one server-authoritative ranked results list) — centralize
result computation on the server so every surface reads the same ordered results.

### B6 — 🟢 Score-card secondary stat never renders (field unwired in prefab)
`secondaryStatText` was unassigned (`fileID: 0`) in the only score-card prefab
(`_Prefabs/UI Elements/In Game/PlayerScoreCard.prefab`), so the secondary line
that `HexRaceScoreboard` ("`N Crystals`") and `MultiplayerJoustScoreboard`
("`N Jousts`") feed through `Scoreboard.ShowMultiplayerView` → `ShowSecondaryStat`
was silently dropped. **Done** (commit `3aa3b5b7`): wired it to the existing
orphaned `TextMeshProUGUI` (`ScoreText`, placeholder "expand to view") under
`DataPanels` — sibling of `CrystalScore`, inactive by default. Data-only prefab
change; composes with B1's `RefreshDataPanelsRoot`. Verify the element's on-card
position visually in a HexRace/Joust end-game.

### B7 — 🟢 End-game vessel podium ranked golf modes backwards
`EndGameVesselDisplayManager.GatherVesselData` ranked players by `RoundStatsList`
sorted **descending by raw `Score`** — a second "who placed where" path that
ignored the rule-produced `gameData.Results`. For golf modes (HexRace, Joust) the
winner's `Score` is a small finish time and the loser's is the `10000+` sentinel,
so descending put the **loser 1st**: a solo HexRace win (5 crystals first) showed
the AI as "1st" and the human as "2nd", and the winner vessel icon
(`EndGameVesselDisplay` keys it off `ranking == 1`) went to the loser. **Done**
(commit `fd0dee09`): the podium reads each player's rank from `gameData.Results`
(the SSOT every other end-game surface uses — golf-aware, ranked once by the mode's
`ScoringRuleSO`), keeping the legacy descending-Score sort only as a fallback for
modes that produce no Results (e.g. WildlifeBlitz). CrystalCapture (points) was
already correct and is unchanged. This was the last end-game surface still
re-deriving rank locally — completes `REFACTOR.md` R10 Phase B's consumer migration.
File: `_Scripts/Utility/DataContainers/EndGameVesselDisplayManager.cs`.

### B8 — 🟢 Client domain boxes built from a stale turn-start snapshot
In multiplayer the in-game domain boxes were wrong on **clients** (correct on the
host). `MultiplayerHUD.InitializeDomainPanels` built the panel set ONCE at
`OnMiniGameTurnStarted`, snapshotting `LocalPlayer.Domain`, each `stats.Domain`, and
`RequestedDomainCount`. On a client those replicate around turn start, so a late
arrival produced a wrong ally/opposing set that was never corrected — and
`RoundStats.n_Domain`'s replication callback raised **no** event, so the HUD never
learned a domain changed; updates routed by the live `stats.Domain` into the stale
panel dict were silently dropped. **Done** (commit `fa2515f7`): `RoundStats`
n_Domain/n_Name callbacks now raise `OnAnyStatChanged`; `MultiplayerHUD` rebuilds the
panel set reactively (idempotent `RebuildDomainPanels` + allocation-free
`DomainLayoutChanged`), reconciles on each stat event, and subscribes `OnPlayerAdded`
for late roster.
Files: `_Scripts/Data/Enums/RoundStats.cs`, `_Scripts/UI/MultiplayerHUD.cs`.

### B9 — 🟢 Client's OWN metric count frozen on its own screen (domain layout)
After B8, domains mapped correctly but a client's OWN crystal count stayed frozen
(e.g. stuck at 6) on the client while correct on the host; remote players' counts
replicated fine. Crystal counting is fully server-authoritative
(`OmniCrystalImpactor` bails on clients; `StatsManager` records server-only), so the
host is the source of truth — but a client re-summing its OWN per-player `RoundStats`
could freeze (owner-side replication of its own value proved unreliable; root cause
not isolated). **Done** (commit `e25290cc`, "Approach B"): clients no longer re-sum
per-player stats for the domain boxes. The server (`MultiplayerDomainGamesController`)
computes each active domain's `ScoringMetrics.SumByDomain(rule.Metric, …)` and
replicates it via 3 NetworkVariables; every peer mirrors it into
`GameDataSO.SetDomainMetricSum` and `MultiplayerHUD` displays it verbatim, so every
client matches the host. Generalizes to all three domain modes via the per-mode
`rule.Metric` (Crystals / Jousts). **Residual:** the LEGACY per-player layout still
reads per-player stats and is NOT covered — `TODOS.md` **TD1**. Needs the 2-human
play-test (HexRace / Joust / CrystalCapture — `TESTS.md` T11/T12).
Files: `_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs`,
`_Scripts/Utility/DataContainers/GameDataSO.cs`, `_Scripts/UI/MultiplayerHUD.cs`.
**Correction (post-B10):** the "owner replication unreliable" framing was disproven
(RoundStats is baked on the same NetworkObject as Player). The frozen count was the
SAME domain-divergence root as B10 — the local player's crystals summed under a stale
`RoundStats.Domain`. With B10's fix the domain is correct everywhere, so Approach B's
server-synced sums are now **redundant but harmless** robustness; optional retirement
tracked in `TODOS.md` TD3.
**✅ Verified in engine** (2-human test — client domain counts track the host).
**Post-B12 correction:** the frozen OWN count is most plausibly B12's stale shadow —
on a party-joined client the local player's entry in `RoundStatsList` was a destroyed
pre-party component with frozen stats, so any client-side re-sum of its OWN stats read
the corpse. With B12 fixed, retiring Approach B (TODOS.md TD3) is safer.

### B10 — 🟢 Client's OWN profile icon grouped into the wrong domain box
With 2 humans on different domains (host Jade, client Ruby), the client's screen
grouped BOTH icons into the host's box while the host's screen was correct.
**Corrected root cause** — an earlier "owner doesn't receive its own RoundStats
replication" theory (and B10's first fix `352ed485`) was **DISPROVEN**: `RoundStats`
is a baked `NetworkBehaviour` on the SAME `NetworkObject` as `Player`
(`_Prefabs/CORE/Player.prefab`), so its NetworkVariables replicate to the owner
exactly like `Player.NetDomain`. The real cause: domain was networked **twice** —
`Player.NetDomain` (authoritative, set directly by the pick) and `RoundStats.n_Domain`
(a server-DERIVED copy re-replicated through a round-trip). The derived copy lagged
`Player.Domain` on the client, and the HUD **mixed sources** — ally box from
`Player.Domain` (correct), icon grouping from `RoundStats.Domain` (lagging) — so the
client's own icon grouped by the stale copy into the host's box. A membership-blind
reconcile (rebuilds only on a domain-SET change, not when a player MOVES boxes) meant
it never healed, which is why the first attempts (`fa2515f7`, `352ed485`) never
re-rendered. **Done** in two commits:
- `5442d3d0` — the in-game HUD groups entirely off `Player.Domain` (via
  `gameData.Players`) with a membership-aware reconcile (layout-signature hash), so the
  icon is in the right box from the first build.
- `aaabc1b6` — retire `RoundStats.n_Domain`; `RoundStats.Domain` is a local mirror
  Player keeps in sync on every peer from the authoritative `NetDomain` (one source of
  truth), fixing every other `RoundStats.Domain` consumer too.
Files: `_Scripts/UI/MultiplayerHUD.cs`, `_Scripts/Data/Enums/RoundStats.cs`,
`_Scripts/Controller/Player/Player.cs`.
**✅ Verified in engine** (2-human test — the client's own profile icon now sits in its
own domain box). Broader mode coverage (CrystalCapture / Joust) continuing.

### B11 — 🟢 Client-local menu domain writes desynced the live mirrors (B10-relapse class)
`MainMenuController.ApplyMenuDomain` ran on each client at `OnClientReady` and (a)
attempted `NetDomain.Value = Jade` — illegal off-host (`NetDomain` is Server-write;
the rejection can throw and abort the autopilot activation chain →
`../PartySystem/BUGS.md` B9's drift), and (b) stamped `Player.Domain` /
`RoundStats.Domain` locally — the exact B10 sin of writing mirrors from anywhere but
`NetDomain`. A stamp landing after a modal pick (duplicate `OnClientReady` raises from
the roster-pull retries) left that machine believing Jade until the next NetDomain
delta. **Done** (commits `53294068`/`65d4da96`/`c073636e` — squashed into the
bleeding-edge merge `0ea12370`, so the originals no longer resolve; change verified
in code): menu domain reset moved
server-side (`MenuServerPlayerVesselInitializer.OnPlayerReadyToSpawnAsync`, before
vessel spawn), `ApplyMenuDomain` deleted, and the replication repaint completed by
folding `RefreshShipMaterial` into `ShipHelper.SetShipProperties` (init-aware: skipped
until `VesselCustomization.Initialize` has collected `ShipGeometries`). **Rule:**
client code must NEVER write `NetDomain`, `Player.Domain`, or `RoundStats.Domain` —
mirrors sync only from `NetDomain` (`InitializeForMultiplayerMode` +
`OnNetDomainChanged`).
Files: `_Scripts/System/MainMenuController.cs`,
`_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`,
`_Scripts/Controller/Vessel/VesselHelper.cs`.

### B12 — 🟢 Stale pre-party RoundStats shadowed the live entry (own ready-text frozen Jade)
On a party-joined client, the pre-party SOLO session had already put its own
`RoundStats` (Name = own name, Domain = Jade) into `gameData.RoundStatsList`. The
invite-accept NetworkManager shutdown destroyed that Player, but
`ResetRuntimeDataForPartyJoin` cleared only `Players`/`Vessels`/`LocalPlayer` — the
destroyed component stayed in `RoundStatsList` with its managed `Name` intact, so
`AddPlayer`'s name-keyed dedup REJECTED the new live `RoundStats`. Clients never run
the full `ResetRuntimeData` at launch (scene load defers to the server), so the dead
shadow rode into every game: the ready feed colored the client's OWN name Jade on its
own screen (correct on the host, whose list is rebuilt at launch),
`SyncFinalScores_ClientRpc` wrote results onto the dead object, and client-side
domain sums counted the local player under Jade with frozen stats. `LocalRoundStats`
got the live instance, so the centerline score worked — masking the bug. **Done**
(commits `52923bf8`/`6400eca0` — squashed into `0ea12370`, originals no longer
resolve; change verified in code): `ResetRuntimeDataForPartyJoin` clears
`RoundStatsList`/`DomainStatsList`/`LocalRoundStats` too; `AddPlayer` prunes destroyed
roster entries and replaces same-name stale instances with the live component (logs
"Replacing stale RoundStats entry"); the ready feed reads the live `Player.Domain`
(B10 doctrine) with the list as fallback.
**✅ Verified in engine** (2-human test — client's own ready-text now shows its picked
domain on both peers).
Files: `_Scripts/Utility/DataContainers/GameDataSO.cs`,
`_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs`.

### B13 — 🟢 Scoreboard Play Again dead in Joust + Crystal Capture (null onClick target)
Clicking Play Again on the Joust scoreboard did nothing (Crystal Capture had the
identical defect). The controller side was already correct
(`UseSceneReloadForReplay=true` since `21d538d3`) — the click never reached it.
Root cause is a scene-wiring class: all three domain scenes share the
`GameCanvas-HexRace` prefab, whose `PlayAgainButton.onClick` persistent call
targets the prefab's INTERNAL `Scoreboard` component. Joust and Crystal Capture
remove that internal Scoreboard from the instance and add their own scene-level
`Scoreboard` (correctly wired to the mode's controller) — but the button was
never retargeted: Joust's scene override pointed the call at `{fileID: 0}`, and
Crystal Capture left the prefab default pointing at the now-removed component.
A persistent call with a null target is silently skipped, so the button played
its click audio and nothing else. HexRace never hit this because it KEEPS the
prefab's internal Scoreboard and overrides only its `gameController`
(`multiplayerController` legacy path), which also proves the call's stale
`m_TargetAssemblyTypeName` (the deleted `HexRaceScoreboard` type) is harmless at
runtime when the target is set — Unity resolves the method from the live
target's type. **Done** (commit `e21c778a`): both scenes override
`m_OnClick…m_Calls[0].m_Target` on the PlayAgainButton to the scene-added
Scoreboard (+ refresh the type name to `CosmicShore.UI.Scoreboard`).
**✅ Verified in engine** (Joust: Play Again reloads the scene and a fresh match
plays; owner-tested). Wiring requirement recorded in `JOUST.md` §9 /
`CRYSTAL_CAPTURE.md` §9 so the next scene edit doesn't reintroduce it.
Files: `_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`,
`_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`.

### B14 — 🟢 Host-only scoreboard nav gating no-oped (fields unwired) + no anti-spam hide
Same defect class as B6 (field unwired ⇒ silent no-op): `ConfigureLobbyButtons`
hides Play Again / Main Menu for non-host clients, but the GameCanvas-HexRace
prefab's serialized Scoreboard data predates the `playAgainButton` /
`mainMenuButton` fields, so they deserialized null and the gating silently
no-oped — clients saw both buttons in ALL three domain modes (clicks were only
blocked code-side by the host guards). Additionally nothing hid the buttons
after a host click, so the host could spam Play Again / Main Menu during the
transition (the controller's `_isResetting` gate is even released early on the
host by `PrepareForSceneReload_ClientRpc` running locally, so UI-level gating
matters). **Done** (commit `3a021e50`):
- `Scoreboard.HideHostNavButtons()` hides both buttons once a navigation
  commits — Play Again directly in `OnPlayAgainButtonPressed`; Main Menu via a
  new `onClickToMainMenu` field subscribed to `EventOnClickToMainMenuButton`
  (the same asset `PauseMenu.OnClickMainMenu` raises AFTER its host guard, so a
  rejected client click never falsely hides). `ConfigureLobbyButtons`
  re-activates on the next game end.
- Wired `playAgainButton` (PlayAgainButton GO), `mainMenuButton` (HomeButton
  GO), and the event asset in all three scenes — stripped-GO references on the
  scene-added Scoreboards (Joust/CC), property overrides on the prefab's
  internal Scoreboard (HexRace).
**✅ Verified in engine** (owner-tested). Clients see neither nav button and
follow the host via the Netcode scene load; `PauseMenu` already gated its own
Restart/Main Menu the same way.
Files: `_Scripts/UI/Scoreboard.cs` + the three scene files above +
`_Scenes/Multiplayer Scenes/MinigameHexRace.unity`.

### B15 — 🟢 Game end dead on the SECOND game after a menu return (stale RoundStats subscribers)
**Reported (2026-06-12).** Party returns to Menu_Main together, host relaunches
HexRace, the race plays normally — but when a domain reaches the crystal target,
the Game End flow never fires (no turn end, no cinematic, no scoreboard, on any
machine). S9's "repeat the menu → game → menu cycle with no leftover state" is
the failing case.

**Root cause (audit).** `RoundStats` lives on the persistent Player
NetworkObject and survives every scene transition; its C# stat events
(`OnScoreChanged`, `OnAnyStatChanged`, `OnCrystalsCollectedChanged`,
`OnJoustCollisionChanged`, …) are subscribed each turn by SCENE objects — HUDs
(`MiniGameHUD`/`MultiplayerHUD`), network turn monitors, and `BaseScoring`
strategies. Two compounding defects let those subscriptions outlive their
owners and ride into the next game:

1. **Turn-end-gated cleanup.** The HUDs detach their per-stats handlers only in
   `OnMiniGameTurnEnd`. A mid-turn exit (pause-menu **Main Menu**) destroys
   them without that event ever firing — nothing detached.
2. **List-based unsubscription vs. reset ordering.** The monitors, the HUD's
   `UnsubscribeFromAllStats`, and `BaseScoring.Unsubscribe` all iterated
   `gameData.RoundStatsList` to detach. `SceneLoader.LoadSceneAsync` calls
   `ResetRuntimeData()` (clearing that list) ~0.5 s BEFORE the old scene's
   objects are destroyed, so every list-based unsubscribe loop ran over an
   EMPTY list and detached nothing — even cleanups that DID run at teardown
   (e.g. `TurnMonitorController.OnDisable → StopMonitors`, and
   `BaseScoreTracker`'s `OnClickToMainMenu → OnTurnEnded` abort hook).

The leaked delegates then fire inside the next game's stat-setter chains
(`RoundStats` setters raise events synchronously from the NetworkVariable
`OnValueChanged`). Consequences range from silent corruption (a dead
`CrystalsCollectedScoring.UpdateScore` is pure C# — it overwrites the new
game's `Score` from a destroyed tracker) to chain-aborting exceptions when a
dead handler touches a destroyed view: the turn-start raise
(`StartMonitors` never runs → `CheckForEndOfTurn` never polled) or the
turn-end chain (`AssignScores`' Score writes throw mid-raise after
`TurnMonitorController` has already latched `_isRunning=false`) — either way
the game end is permanently lost while gameplay continues normally, matching
the report. Self-perpetuating: the only way out of an endless race is another
mid-turn Main-Menu exit, which re-poisons the next game.

**Fix (commit `d3cbbabb` — squashed into the bleeding-edge merge `0ea12370`, so
the original no longer resolves; change verified in code).**
- **Chokepoint reset:** new `RoundStats.ClearEventSubscriptions()` severs every
  external stat-event delegate. Called from `Player.PrepareForNewScene()`
  (server, BEFORE `Cleanup()` so the zeroing writes can't raise into dead
  handlers) and `Player.InitializeForMultiplayerMode()` (every peer, once per
  pair-init per scene, before any of the new scene's subscribers attach) — so
  every scene entry starts with a clean subscriber list regardless of how the
  previous scene exited.
- **Own-records unsubscription:** `NetworkCrystalCollisionTurnMonitor`,
  `NetworkJoustCollisionTurnMonitor`, `MultiplayerHUD`, and
  `CrystalsCollectedScoring` now track the stats they subscribed to and detach
  from that record (plus `OnDestroy` safety nets on the monitors and HUDs) —
  no more dependence on `RoundStatsList` still being populated at teardown.
- **Deterministic monitor lifecycle:** `TurnMonitorController.SubscribeToEvents`
  is idempotent (OnEnable + OnNetworkSpawn both ran it in networked scenes,
  double-subscribing `StartMonitors`/`StopMonitors`);
  `CrystalCollisionTurnMonitor` made its `ownStats` subscribe idempotent.
- **Diagnostics:** `[FLOW-10]` logs at the two end-detection chokepoints
  (`TurnMonitorController` raise, `HexRaceController.OnTurnEndedCustom`
  objective-reached) so any future break pinpoints the failing link.
- Also fixed: `CrystalsCollectedScoring.Subscribe`'s early-`return` (one
  unresolved name skipped all remaining players);
  `GameDataSO.ResetAllData()` now resets `RequestedDomainCount` as
  `ResetRuntimeData`'s comment already claimed.

**✅ Verified in engine (2026-06-12)** — the reported repro (party menu-return →
HexRace relaunch → objective reached) now runs the full end flow. The regression
steps are codified as `TESTS.md` **T15**: (a) menu → HexRace → finish →
scoreboard → Main Menu → HexRace again → finish: end flow fires on every
machine; (b) same but exit the FIRST race mid-turn via pause-menu Main Menu —
the second race must still end cleanly (the poison path); (c) repeat 2–3× per
`../PartySystem/TESTS.md` S9.

Files: `_Scripts/Data/Enums/RoundStats.cs`, `_Scripts/Controller/Player/Player.cs`,
`_Scripts/Controller/Arcade/TurnMonitorController.cs`,
`_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs`,
`_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs`,
`_Scripts/Controller/Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs`,
`_Scripts/UI/MiniGameHUD.cs`, `_Scripts/UI/MultiplayerHUD.cs`,
`_Scripts/Controller/Arcade/Scoring/CrystalsCollectedScoring.cs`,
`_Scripts/Utility/DataContainers/GameDataSO.cs`,
`_Scripts/Controller/Arcade/HexRaceController.cs`.

### B16 — 🟡 Joust toasts without scores: client-observed jousts were never recorded (ghost feedback)
**Reported (2026-07-16).** Most jousts never reach the scoreboard; the game feed
posts "X jousted Y" but the joust count/score doesn't move. Reported in solo and
multiplayer both — in multiplayer it is systematic.

**Root cause (audit).** The joust pipeline had two independent halves that could
disagree:

1. **Detection & feedback ran locally on EVERY machine.** Vessel-into-skimmer
   overlaps are plain physics triggers, simulated per machine on replicated
   (interpolation-delayed) transforms. `VesselExplosionBySkimmerEffectSO.Execute`
   fired wherever an overlap was locally observed and unconditionally spawned the
   explosion, played the SFX, posted the game-feed toast, and raised
   `OnJoustCollision`.
2. **Recording only happened on the server.** `StatsManager.ExecuteJoustCollision`
   early-outs on clients (`_allowRecord=false`), so a client's raise recorded
   nothing — and the "client reports up" branch in
   `NetworkJoustCollisionTurnMonitor.OnCollisionChanged → ReportCollision_ServerRpc`
   was unreachable dead code: it only triggers from the `JoustCollisions` setter,
   which on a client is only ever invoked by the server's own
   `SyncCollision_ClientRpc` echo.

So a joust counted only if the HOST's physics happened to observe the same
overlap. At joust closing speeds with NetworkTransform interpolation the host
frequently does NOT see the pass that the jouster's own machine sees (its vessel
+ skimmer are at true positions locally; on the host they trail by
speed × interpolation delay). Result: client machines showed toast + explosion
for jousts that were never recorded anywhere ("ghost feedback"), while
host-observed jousts updated client scores silently with no toast — the exact
reported symptoms.

**Fix — owner-authoritative confirm + broadcast (crystal-impact pattern).**
Detection still runs everywhere, but only the machine that OWNS the scoring
(impactee) vessel may confirm a joust — it is the most reliable observer of its
own skimmer sweep, and exactly-once semantics fall out of ownership (AI vessels
are host-owned, so solo/AI jousts confirm on the host as before). After the
speed/domain/cooldown checks pass on the owner, the joust routes
`NetworkVesselImpactor.ReportJoust → ExecuteJoust_ServerRpc → ExecuteJoust_ClientRpc`
(mirroring the crystal round-trip), and EVERY machine — server included — runs
`ExecuteConfirmed`: explosion, `OnJoustCollision` raise, audio, game-feed post.
The server's raise is the one StatsManager records (single-writer preserved);
`RoundStats.n_JoustCollisions` + the monitor's existing `SyncCollision_ClientRpc`
fan the count back out to every HUD. Toast ⟺ score, by construction. Offline /
unspawned contexts fall back to direct local execution (previous behavior).

**Verification (engine):** 2-human party Joust — every "X jousted Y" toast must
be accompanied by the jouster's domain count moving on BOTH machines (HUD
"jousts left" + end scoreboard), and the scoreboard totals must equal the toast
count each player saw. Solo-vs-AI: same invariant. Also confirm the explosion +
toast now appear on the machine that got jousted even when its own physics
missed the pass.

Files: `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/VesselExplosionBySkimmerEffectSO.cs`,
`_Scripts/Controller/ImpactEffects/Impactors/NetworkVesselImpactor.cs`,
`_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs`,
`_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs`.

### B17 — ✅ Wildlife Blitz DNF loss: reveal header says VICTORY (pre-existing, surfaced by Y1.1 review; FIXED 2026-07-20)

**Symptom:** lose the solo blitz (clock runs out, cell uncleared) - the scoreboard state is
correct (no winner credited; DNF row), but the EndGameSequencer reveal header shows VICTORY.

**Root cause:** `EndGameSequencer.DidLocalPlayerWin()` falls back to
`gameData.IsLocalDomainWinner(...)` when `WinnerDomain == Blue`, and `IsLocalDomainWinner`
matches the local domain against `DomainStatsList` - which always contains the local player's
domain in a solo game, so the fallback returns true. `Domains.Blue` is overloaded: "not set"
vs "explicitly nobody won" are indistinguishable to the sequencer. The legacy blitz tracker
behaved identically (it also ran `CalculateDomainStats` before `InvokeWinnerCalculated`), so
this is NOT a Y1.1 regression - but the Y1.1 blitz migration's explicit `WinnerDomain = Blue`
write fixes only the scoreboard banner derivation, not this reveal fallback.

**Fix (shipped 2026-07-20, option (a)):** `GameDataSO.HasNoWinner` — a distinct
"explicitly nobody won" signal, set on every peer by
`MultiplayerDomainGamesController.SyncFinalResults_ClientRpc` alongside the
`WinnerName`/`WinnerDomain` writes (`true` iff the broadcast winner is `Blue`), reset with
the other Winner* fields in both runtime-data resets. Consumers: `EndGameSequencer.
DidLocalPlayerWin` returns false before any fallback (DEFEAT reveal + the rule's
"CELL UNCLEARED" body), and `Scoreboard.ShowMultiplayerView` renders a neutral
`SetNoWinnerBanner()` ("GAME OVER", Blue tint) instead of crediting `Results[0].Domain`.
Legacy modes that never write Winner* keep both fallbacks (flag stays false).
Engine verification: solo blitz, let the clock expire → DEFEAT reveal + GAME OVER banner;
clear the cell → VICTORY + clear time (unchanged).

---

B1–B4, B6, B7, B8 fixed (verify only — B6 also warrants a visual position check).
B9 (count) + B10 (domain icon placement) fixed for the **domain** layout and **verified
in a 2-human engine test** (broader mode coverage continuing; legacy-layout residual
tracked in `TODOS.md` TD1). B11 (client-local menu domain writes) + B12 (stale
pre-party RoundStats shadow) fixed 2026-06-11 — B12 verified in engine; B11's
host-return sweep pending (`../PartySystem/BUGS.md` B9). B13 (dead Play Again,
Joust + CC) + B14 (unwired host-nav gating + anti-spam hide) fixed 2026-06-12 and
owner-verified in engine (Joust; HexRace/CC share the same fix shape — sweep with
`TESTS.md` T13/T14). B15 (stale RoundStats subscribers killing the second game's
end flow) fixed & verified in engine 2026-06-12 — regression steps in `TESTS.md`
T15. B5 remains scheduled into **R10** (the unified ranked `ScoreResult` list
dissolves it). B16 (ghost joust toasts / unrecorded client-observed jousts) fixed
2026-07-16 — code-complete, engine verification pending (see B16's verification
steps). B17 (blitz DNF reveal header) fixed 2026-07-20 via `GameDataSO.HasNoWinner` — engine
verification pending (see B17's verification steps). No other open read-through findings remain.

### B18 — 🟢 Non-host players started every match with the PREVIOUS game's score (unhealable mirror drift)

**Symptom (reported repeatedly, reproduced by the reporter every time).** Start a multiplayer
game, play another one, then launch a third — *"every other person except the host had different
scores from the beginning"*. The host always read 0. Not dependent on exiting a game mid-way (the
reporter confirmed it happens without that).

**Root cause — the "except the host" is the whole clue.** Every `RoundStats` stat setter writes
BOTH halves, but the network half only on the server:

```csharp
set {
    _hostilePrismsDestroyedLocal = value;                          // always
    if (IsSpawned && IsServer) n_HostilePrismsDestroyed.Value = value;   // server only
}
```

So on a client, anything that assigns a stat locally moves the mirror **without** moving the
authoritative value. The common case is every mode's end-of-game snapshot
(`SyncFinalScores_ClientRpc` assigns `stat.Score` / `stat.HostilePrismsDestroyed` / … on clients);
`ResetStatsDataForReplay` inside `ResetForReplay_ClientRpc` and `StatsManager` running on a client
before its `OnNetworkSpawn` clears `_allowRecord` do the same.

The drift is then **unhealable by replication**: a `NetworkVariable` raises `OnValueChanged` only
when the value actually CHANGES, so once the server's value and the client's mirror have both
settled — server 0, client 842 — the server writing 0 again is a no-op and the stale mirror
survives into the next game, and the next. The host is immune because its setters write the mirror
and the NetworkVariable together.

This is why the two earlier fixes were not enough: making the per-scene reset unconditional
(`ServerPlayerVesselInitializer`, see the branch) and zeroing at game start
(`MiniGameControllerBase.ZeroStatsForGameStartOnce`) both do the right thing on the SERVER — but
neither can reach a client mirror that the server's own writes cannot move.

**Fix.** `RoundStats.SyncLocalMirrorsFromNetwork()` — the initial-pull block from `OnNetworkSpawn`,
extracted and made callable — re-derives every local mirror from the replicated values.
`Player.InitializeForMultiplayerMode` calls it at every scene entry (it runs once per player per
scene on EVERY peer), so a client can never carry a diverged mirror across a game boundary.
`OnNetworkSpawn` now calls the same method, so there is one definition of "pull the truth".

**Rule for anyone writing a mode.** Assigning a stat inside a `ClientRpc` sets that peer's mirror
only. It is fine as a display convenience at game end, but it makes the client authoritative-looking
and wrong; anything that must survive into the next game has to come from the server's
`NetworkVariable`, and the mirror has to be re-based on scene entry.

**Verification.** The setter, the NetworkVariable change-only semantics and both peers were modelled
and compiled: the model reproduces the bug (client stuck at 842 while the host reads 0), shows
server re-writes failing to heal it, and shows `SyncLocalMirrorsFromNetwork` fixing it without
clobbering a live mid-game value. Engine verification pending.

---

### B19 — a client scored nothing for ENVIRONMENT mass (flora, fauna, laid structure)

**Symptom.** 2-player Rampage: the host scored off everything; the client could only ever score
off the **other pilot's trail**, never off a cactus it flew through and shattered. In a mode whose
entire score is destroyed environment mass, the client was effectively playing a slot machine —
whatever the *server's* own physics happened to knock over got credited to them instead.

**Root cause, and it is platform-wide.** `StatsManager` records prism destruction **server-only**
(`_allowRecord`), and its own doc comments state the justification twice:

> "a prism sits at the same place on the server, so the server's own physics sees a client's ram
> and records it"

That is true of a TRAIL prism — laid from replicated vessel motion, so both peers have one in the
same place, which is exactly why trail kills were the one thing that worked. It is **false** of
flora and fauna, and `CellNetworkSync`'s class doc has always said so: *"Flora and fauna spawning
is non-deterministic per-side (each client runs its own IntensityWiseLifeSpawner with local
Random.value rolls)."* The server's copy of the cactus the client just shredded is somewhere else
entirely, so nothing was recorded anywhere.

**Fix.** `Player.ReportEnvironmentPrismDestroyed_ServerRpc` — the third instance of the same
owner-detects → server-records round-trip as `ReportFaunaKill_ServerRpc` (fauna have no
NetworkObject) and `ReportCombatHit_ServerRpc` (projectiles are not networked). Identity comes from
RPC ownership, never a name string.

The other half is **who must NOT credit**: `StatsManager.OwnsAttacker` lets the server credit only
players it simulates (its own + every AI, both server-owned NetworkObjects) and DROP environment
kills it observed a remote player make. Rostered victims are untouched and stay server-recorded.
Each kill lands exactly once on both paths.

**Rule.** Server-only stat recording is correct only for things that exist identically on every
peer. Before adding one, ask what the stat's SOURCE is: a trail (replicated motion — fine), or a
per-peer simulation (flora, fauna, projectiles — needs the owner round-trip).

### B20 — environment mass was hostile to EVERY domain, including its own colour

**Symptom / cause.** The only hostility test in `StatsManager.PrismDestroyed` was the owner-name /
roster comparison. Flora, fauna bodies and laid cell structure carry non-roster owner names, so
they fell to the `else` branch and counted as hostile to everyone — including the third of a
mixed-domain forest wearing the destroying pilot's own colour. Domain was decoration.

**Fix.** `PrismStats` carries the destroyed prism's `OwnDomain`, and
`StatsManager.IsFriendlyEnvironmentPrism` applies to the world the rule trails always had — **your
own colour is worth nothing** — with `Domains.Blue` (the "no team" sentinel) staying hostile to
everyone so neutral structure still scores. Ribcage rides the same metric and is unaffected in
practice: its cage is painted across the full triad plus Blue joints, so a team still reaches a
2,000 target out of ~10,620 prisms.

**Verification.** Both are compile-by-inspection + traced call paths; engine verification pending
(MPPM, 1 host + 1 client — see RAMPAGE.md's checklist).
