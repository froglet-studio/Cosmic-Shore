# Unified Systems — Yash (systems engineering: unification programs, netcode, wiring)

**Companions:** `AUDIT.md` (evidence — § references point there) · `GARRETT.md` §1 (the D-gates).
**Gating:** `[default-ok Dx]` = proceed on the recommended default unless Garrett's markup says
otherwise. `[hard-gate Dx]` = wait for explicit markup.
**Discipline:** one item per commit; guid-grep verify every wiring claim at the moment of change
(AUDIT method header); no temporary fixes; follow `Docs/ScoringSystem/REFACTOR.md` ground rules
(SOAP-first, fail-loud, no new statics/singletons). Line numbers in AUDIT were verified 2026-07-17
against bleeding-edge `1f558502` — re-verify before editing.

Why you: this doc is scene/prefab wiring, Netcode flow, and the scoring/spawn programs — the
integration work you already shepherd (Menu_Main/Bootstrap/game-scene wiring, the multiplayer and
perf branches).

---

## Y0 — Wire-fix wave (bug-fix grade, all unblocked)

- **Y0.1 Mount `GameModeProgressionService`.** Add the component to the Bootstrap scene's
  PlayerDataService GameObject (copy serialized refs — `questList`/`progressionConfig`/`gameData`,
  e.g. `SO_GameModeQuestList` guid `5eee61fa…` — from
  `_Prefabs/MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab`). Its sibling
  `ParticipationXpAwarder` is already scene-wired and shows the intended pattern. Verify in
  editor: quest track renders beyond mode 0, intensity gating works, quest-complete toast fires.
  THEN delete the whole `MIgration_Prefabs (DELETE LATER)/` folder (9 prefabs, zero external
  refs — AUDIT §1.6/§1.9). *Risk: low. Player-visible payoff: the quest chain comes alive.*
  - **Verified plan (2026-07-18, guid-grep re-check — all AUDIT claims held; one correction).**
    Script guid `541692fb0a8f1b6478f85df5b78951a7` has exactly ONE content hit (the orphaned
    prefab); all 9 migration-folder prefab guids have ZERO external refs (none cross-reference
    each other either). **Correction:** the source prefab does NOT serialize `progressionConfig`
    — wire only `questList` (`5eee61facaac4bb46b9e9892512f74cb` → `GameModeQuestList.asset`) +
    `gameData` (`b35f33752bb10a44cb5033b5670f50aa` → `Runtime GameData.asset`), leave
    `progressionConfig: {fileID: 0}` (code lazily creates a default; sibling
    `ParticipationXpAwarder` is wired identically). Mount target: `Bootstrap.unity` GameObject
    `PlayerDataService` fileID `&483927156` (add 5th component after ParticipationXpAwarder
    `483927160`, mirroring its block shape). The service is self-contained (own singleton guard,
    `SetParent(null)` + `DontDestroyOnLoad` in Awake, Reflex `[Inject] UGSDataService` +
    `[Inject] AnalyticsServiceFacade` — same proven pattern as the co-located PlayerDataService).
    6 null-guarded runtime consumers + LogControlWindow (editor) light up on mount;
    `QuestTrackView.cs` behavior confirmed (null Instance ⇒ only quest 0 unlocked).
- **Y0.2 De-wire the legacy `NetworkScoreTracker` from Joust + Crystal Capture scenes.** It fires
  a duplicate `SortRoundStats`+`InvokeWinnerCalculated` 500 ms after the authoritative RPC, and
  Joust's instance has `golfRules:0` on a golf mode (AUDIT §0.1/§2.1). Before removing, replace
  the mid-turn centerline score feed it provides: mirror HexRace's slim elapsed-time write or read
  `rule.Remaining`/metric in the HUD. Verify: mid-turn score still ticks; end-game results
  identical on host + client; no second winner event (watch `EndGameSequencer`).
  *Risk: medium (touches live score display) — verify in MPPM.*
  - **Verified plan (2026-07-18, guid-grep re-check — all AUDIT claims held; design decided).**
    Tracker guid `7cf9c7929c7c484faf5a985004c9caee` lives in 6 scenes; in scope only Joust
    (block `&1628508336`, `golfRules:0`, Mode 2 TimePlayed) and CC (block `&1628508336`,
    `golfRules:0`, Mode 7 CrystalsCollected), both enabled on the "Game" GO beside the migrated
    controllers. Removal is pure: zero `GetComponent<NetworkScoreTracker>` hits, zero serialized
    refs to the component's fileID beyond the GO's own m_Component list; UGS reporting is
    independent (`JoustStatsReporter`/`CrystalCaptureStatsReporter` fire on `OnMiniGameEnd` and
    read post-RPC values). **Load-bearing mechanic (RoundStats.cs `Score` setter):** a spawned
    client's local write does NOT fire `OnScoreChanged` — the centerline event reaches peers only
    via server write → `n_Score` replication. (Corollary: HexRace clients tick from the server's
    `TimePlayedScoring` loop, not from `HexRaceScoreTracker.Update`'s local write.) The
    replacement feed must therefore be a **server-side write**. **Decided design — in-controller
    feeds** (scene edits become pure deletions; the Y1.2 hoist later absorbs the code with no
    second round of scene surgery): Joust — server-only 0.25 s UniTask loop (destroy-linked CTS,
    `IsTurnRunning`-guarded, live `RoundStatsList` re-read each tick, no cached stats refs)
    writing `Time.time - gameData.TurnStartTime` (the exact winner-finishTime expression,
    `MultiplayerJoustController.CalculateJoustScores_Server`) into every RoundStats; CC —
    server-only per-stats `OnCrystalsCollectedChanged` subscription writing
    `rule.LiveMetric(stats)`, with B15 own-record teardown (turn end + `OnNetworkDespawn` +
    `OnDestroy`; precedent: `NetworkCrystalCollisionTurnMonitor._subscribedStats`). No
    `OnClickToMainMenu` subscription needed. Turn-start roster snapshot suffices for CC (server
    roster complete before any turn). The 4 other tracker scenes (WildlifeBlitz co-op,
    CellularDuel MP, Freestyle MP, 2v2CoOpVsAI) are legacy-primary — Y1.1 scope, do not touch
    under Y0.2; class deletion is Y1.5. Note: removing a NetworkBehaviour shifts NB indices on
    that NetworkObject — safe same-build, don't mix builds across peers.
- **Y0.3 Touch inverts `[default-ok D9]`.** Minimal fix now (add the inversion block to
  `TouchInputStrategy.Reparameterize`, mirroring `GamepadInputStrategy.cs:182-202`) — or skip and
  let Y3 fix it by construction if you're starting Y3 immediately. If D9 says "deliberate
  exemption," document it in the strategy instead.
  - **Verified (2026-07-18):** claim re-confirmed on current bleeding-edge — Gamepad, Keyboard,
    and DualMouse strategies all apply `InvertYEnabled`/`InvertThrottleEnabled` post-calc;
    `TouchInputStrategy.Reparameterize` (line ~286) contains neither. Fix remains as described.

## Y1 — Scoring unification program `[default-ok, but D21 hard-gates Y1.4]`

> **STATUS (2026-07-20, executed on `claude/unified-yash-refactor-9sc0ws`):** Y1 executed with
> Y1.2 hoisted FIRST (engineering call: the 5 migrated modes verify the hoist as a pure refactor,
> then Y1.1's migrations onboard onto the template instead of hand-copying tails that Y1.2 would
> immediately delete). Commits: `a77b3917` Y1.2 hoist (SyncFinalResults template + shared ClientRpc
> tail in `MultiplayerDomainGamesController`; shadowed countdown RPC + dead EndGame override
> deleted; 5 controllers converted; 3 adversarial review passes clean — one accepted delta: Joust's
> representative WinnerName now credits the top jouster, telemetry-only). `2366ecd5` Y1.1
> Freestyle (ScoringMetric.VolumeCreated + FreestyleScoringRuleSO sandbox rule + server feed;
> tracker de-wired). `b2453bf9` Y1.1 MP CellularDuel (ScoringMetric.VolumeActivity composite +
> CellularDuelScoringRuleSO; 2-round-aware SyncFinalResults; latch-guarded vessel swap; tracker
> de-wired). `468e1229` Y1.1 SP CellularDuel (shares the duel rule asset; SP EndGame runs the rule
> tail — fixes the missing CalculateDomainStats drift for this mode). `11ccc36e` Y1.1 SP
> WildlifeBlitz (standalone WildlifeBlitzScoreKeeper off the tracker family + golf
> WildlifeBlitzScoringRuleSO; explicit Winner* writes AFTER SetResults — the derive would show
> VICTORY on a DNF). `1ac6a181` Y1.3 (ObjectiveTurnMonitor base: sealed rule end-check +
> RaiseRemainingUI + B15 lifecycle by type incl. sealed OnDestroy + optional NetworkVariable
> target leg; dead non-network Crystal/Joust bases collapsed into the network classes — zero
> scene edits). `aa172c9d` Y1.4 doc-only (fork map refreshed + D21 replacement-signal note;
> EXECUTION still hard-gated on D21). `a4728138` Y1.5 scoped (two verified-dead blitz classes
> deleted; family deletion blocked — see Y1.5 note below).
>
> **Y1.1 dead stacks skipped (owner default):** co-op WildlifeBlitz (32) and 2v2CoOpVsAI (30) are
> player-UNREACHABLE — their SO_ArcadeGame assets are in NO game list; the co-op scene runs a
> `MultiplayerCellularDuelController` leftover (`MultiplayerWildlifeBlitzMiniGame` was an orphan,
> now deleted); 2v2 has no controller class (scene carries `MultiplayerDomainGamesController`
> directly). Their scenes keep their `NetworkScoreTracker`s; the co-op scene's end-game is inert
> under the duel controller's `HasEndGame=false` (unreachable content). Fate = **D3** (prune or
> revive + re-list). Consequence: `HasEndGame` cannot be sealed in the domain base until 2v2's
> fate resolves (it relies on the legacy `SyncGameEnd` path).
>
> **Y1.5 remaining blockers:** `NetworkScoreTracker` wired in Joust + CC (Y0.2 — documented, not
> executed, owner decision) and the two dead-stack scenes (D3); offline `ScoreTracker` in the two
> Recording Studio tool scenes; `SinglePlayerWildlifeBlitzScoreTracker` wired in the out-of-build
> `BenchmarkStressTest.unity` (D16); `HexRaceScoreTracker` deliberately retained (still extends
> `BaseScoreTracker`); `ScoringModes` + `BaseScoring` strategies referenced by
> `BaseScoreTracker.CreateScoring`. Unblock order: Y0.2 + D3 + D16 + a HexRaceScoreTracker
> de-basing, then the family falls in one commit.

The declared target: one always-networked, domain-aggregated scoring path (REFACTOR.md). Order:

1. **Y1.1** Migrate the legacy-primary modes onto `ScoringRuleSO`: CellularDuel (SP+MP),
   WildlifeBlitz (SP + co-op), MultiplayerFreestyle (since retired 2026-07-21 — the lava lamp
   is the only freestyle), 2v2CoOpVsAI. Author rules per mode; wire
   `IsObjectiveReached`/`AssignScores`/`BuildResults`; keep `EndConditionOverrides.asset`
   authoring intact (the `EndGameConditions` editor window remains the source of end-condition
   counts — do not reintroduce per-scene inspector fields).
2. **Y1.2** Hoist the domain end-game protocol (AUDIT §3.3): seal `HasEndGame=>false` in
   `MultiplayerDomainGamesController`, one protected `SyncFinalResults(...)` + shared ClientRpc
   tail (WinnerName → SortRoundStats → CalculateDomainStats → SetResults → InvokeWinnerCalculated
   → InvokeMiniGameEnd); delete the shadowed countdown RPC. Modes supply only metric extraction.
3. **Y1.3** `ObjectiveTurnMonitor` base (AUDIT §3.4): NetworkVariable target + sealed
   server-side `rule.IsObjectiveReached` check + `rule.Remaining` display + **base-class-owned
   B15 `_subscribedStats` lifecycle** so the leak fix is enforced by type. Keep Joust's RPC pair
   as the one extension; collapse the content-dead non-network Crystal/Joust bases.
4. **Y1.4** `[hard-gate D21]` Retire `IsMultiplayerMode`: first refresh the STALE fork map in
   `Docs/ScoringSystem/ARCHITECTURE.md` §8 to the measured sites (AUDIT §2.1 has the list:
   2 behavioral reads, RPC round-trip, 2 diagnostic reads, 7 writes), write the replacement-signal
   design note for Garrett, then execute on approval.
5. **Y1.5** Delete the `BaseScoreTracker` family, `ScoringModes`, and remaining `BaseScoring`
   strategies once Y1.1 lands (the three throwing stubs + enum IDs 3-6 are already gone).
   R10-D (server-ordered results) per `RANKING_SYNC_PLAN.md` while you're in the sync path.

## Y2 — Spawn/controller spine — **EXECUTED 2026-07-20** (owner resolved D19/D16/D21/D3; solo modes retired outright)

Shipped as the solo-retirement program (commits C1–C7 on `claude/unified-yash-refactor-9sc0ws`),
going further than originally scoped — the owner's direction "solo modes are just multiplayer
game modes with one party member as host" retired the entire solo axis:

- **C1** Cellular Duel consolidated onto mode 29 (SP scene/controller/card deleted; MP scene
  gained `ServerPlayerVesselInitializerWithAI` + spawn points + `MinDomainsAllowed: 2`).
- **C2** Wildlife Blitz (26) converted in place to the networked single-host model:
  `MultiplayerWildlifeBlitzController` (domain-games spine) + `WildlifeBlitzObjectiveTurnMonitor`
  + server-authoritative `WildlifeBlitzScoreKeeper`; scene moved to Multiplayer Scenes with the
  network stack; shared-tail hardening (Blue/DNF ends + Winner*-after-SetResults) + B17 fixed
  via `GameDataSO.HasNoWinner`.
- **C3** Benchmark rebuilt on the converted-blitz pattern (`SandboxBenchmarkController` re-parented
  onto the MP spine, auto-start, no monitors, in Build Settings) — resolves D16.
- **C4** SP path deleted: `PlayerSpawner`, `VesselSpawner`, `PlayerSpawnerAdapterBase`,
  `MiniGamePlayerSpawnerAdapter`, the `Player and Vessel Spawner` prefab,
  `SinglePlayerMiniGameControllerBase` branch, `Player.InitializeForSinglePlayerMode`, the
  legacy `VesselSelectionPanelController`, and the base's local `EndTurn`/`EndRound`/`EndGame`
  limbs (the noted `CalculateDomainStats` drift died with them — the MP path owns the lifecycle).
- **C5** `GameDataSO.IsMultiplayerMode` + `SO_Game.IsMultiplayer` retired (11 sites; the
  `MultiplayerSetup` matchmaking path deleted whole; per-site table in
  `Docs/ScoringSystem/ARCHITECTURE.md` §8) — resolves D21.
- **C6** Dead solo content deleted (23 scene-less cards, 4 orphaned lists, mode-32 stack,
  Singleplayer Scenes folder; retired enum IDs annotated do-not-reuse) — executes D3 as
  delete-outright per owner.
- **C7** Docs sweep (this note, SCENES.md, CLAUDE.md, GARRETT.md annotations).

Remaining verification: the MPPM regression runs listed in the program's test guide (solo-as-host
duel + blitz, 2-human runs, HexRace/Joust regression, Settings → Run Benchmark).

## Y3 — Input pipeline template `[default-ok D9/D12]`

`BaseInputStrategy` becomes a template method: strategies override only
`ReadDevice(out leftStick, out rightStick, out triggers…)` + button reads; base owns
reparameterize + inversion + speed/direction hysteresis + drift-composite raising (AUDIT §3.5,
~250 duplicated lines across 4 live strategies incl. DualMouse). Touch's drift-composite
derivation from touch-count transitions stays a deliberate override. If D12 says port mouse-look,
add it here as a new strategy capability. *Risk: medium — input feel; verify per-platform
(gamepad, touch, keyboard, dual-mouse) before/after with the same hands-on drills.*

## Y4 — PlayFab excision `[hard-gates D4, D5, D6, D7]`

One program, sequenced by decision: delete `System/Playfab/` + `Assets/PlayFabSDK/` +
`CosmicShore.PlayFabTests` + `PlayfabProductGenerator` + `AndroidIAPExample` + `LoginEventBus`/
`TestLoginUI`; remove `AuthenticationManager` from `Authentication.unity`; remove `CaptainManager`
from AppManager DI + Bootstrap. Port-or-disable per decisions:
- **Y4.1** Store UI per **D4** (port to UGS Economy / hide / delete).
- **Y4.2** Captains per **D5** (register `CaptainProgressRepository` in `UGSDataService` —
  fields + `CreateRepositories` + accessors + init/flush/reset + `UGSKeys` — and port
  `HangarCaptainsView`/`XpHandler` consumers; or delete both sides).
- **Y4.3** `Arcade` singleton per **D2**: migrate the four live launcher call sites
  (FactionMissionModal, HangarTrainingModal, ArcadeExploreView, DailyChallengeSystem) onto the
  `GameDataSO`/configure-modal pipeline, then delete `Arcade.cs` + the `MiniGame` static bag
  (route `ArcadeExploreView`'s reads through `GameDataSO` per its own TODO, then delete
  `MiniGame.cs` + `CellularBrawlMiniGame.cs` + `ProtectMissionGame.cs` per D2) + `GameCanvas.cs`.
- **Y4.4** DailyChallenge per **D6** (port onto `DailyChallengeRepo` + `PlayerDataService`, kill
  the 10 PlayerPrefs keys with one-time import; or remove the feature).
- **Y4.5** Cloud mirrors per **D7** (per-domain: port reads/writes onto the repo with one-time
  local import, following the HangarRepo/VesselUnlockSystem pattern — or delete repo + model +
  key). Also fix `ProfileModal`'s hanging PlayFab random-name coroutine (UGS path).
*Risk: large surface but mostly dead code; the ports are the risk — feature-verify each screen.*

## Y5 — SOAP/static-event migrations `[default-ok D18]`

- **Y5.1** Delete the 7 zero-subscriber static events + Invoke sites (AUDIT §1.5).
- **Y5.2** Unify crystal collection onto ONE channel: `ElementalCrystalImpactor`'s static
  `Action<string>` → the SOAP `ScriptableEventCrystalStats` its sibling already uses; migrate the
  3 subscribers.
- **Y5.3** `GameSetting` → `GameSettingsDataSO` SOAP container (mirroring `HostConnectionDataSO`),
  GameSetting stays single writer; collapses the four audio controllers' hand-copied subscribe
  blocks; fix `AnalyticsServiceFacade`'s un-unsubscribable lambdas. Coordinate SettingsModal UI
  side with Shombith.
- **Y5.4** Per **D18**: per-service single access path (drop CameraManager's dead DI registration
  or convert its 48 `.Instance` sites); enforcement tests — edit-mode reflection tests: no new
  `static event` under `Controller/`, no new `: Singleton` outside the prism-manager whitelist.

## Y6 — Impact-effects cleanup + helpers `[mostly default-ok]`

- **Y6.1** Strip the unassignable effect arrays + dead `AcceptImpactee` bodies from
  `PrismImpactor`/`MineImpactor`; delete the 3 orphan abstract effect types (AUDIT §1.5).
- **Y6.2** Shared helpers per AUDIT §2.5: `ElementSfxHelper`, `ElementalPulseSpec` (one cooldown
  map WITH eviction — fixes two slow leaks), `InputMuteSpec`, optional `SpinHelper` — following
  the existing `HapticSpec`/`ResourceChangeSpec` pattern.
- **Y6.3** `[default-ok D10]` Squirrel skim FX per Garrett's A/B; then delete the `[Obsolete]`
  class + asset (and the inert Dolphin YAML override) once unreferenced.
- **Y6.4** `[default-ok D11]` Delete (or finish) the four half-built abilities: ShardFieldBus
  family, Squirrel align-toggle artifacts, FireTrailBlock pair, Manta decoy + orphan asset.
- **Y6.5** `[default-ok D14]` Delete the FlowField + WarpField families (13 scripts + 8 assets);
  relocate the live `CapsuleMembrane.cs`/`CapsuleMembraneAnimationSO.cs` out of `_Scripts/Game/`
  to `Controller/Environment/` (guid-preserving move) and fix the namespace.

## Y7 — Legacy vessel actions per **D1** `[hard-gate if delete]`

If D1 = keep: no action (they're inert). If D1 = delete: strip the 17 inert components from
Urchin/Grizzly/Termite/Falcon/Shrike + Squirrel's ToggleAlignAction, delete the remaining legacy
scripts + `ShipHelper` dead overloads (AUDIT §1.1), re-homing `IScaleProvider` (live R_ consumers)
and `SeedAssemblerConfigurator` first. If D1 = port: one R_ SO+executor pair per ability, one
commit each.

## Y8 — Deferred/decision-shaped

- **Y8.1** `[default-ok D17]` AudioSystem/Jukebox dead-member cleanup (~150 lines, exact list in
  AUDIT §1.8) now; full music-on-FMOD only if D17 says migrate (then: FMOD music events +
  `bus:/Music`, convert Jukebox + 2 `PlaySFXClip` callers, delete the AudioSource/mixer path,
  repoint `sfxBusPath` at `bus:/SFX`). Either way strip the per-instance slider math from the
  four emitter controllers (slider² bug) and delete dead `FloraAmbientAudioController`.
- **Y8.2** Camera per **D13**: option (a) — rewrite `CameraMigrationReview.md` as
  coexistence-final + widen `ICameraController` to what consumers need (kill the ~10 down-casts)
  or drop the interfaces; option (b) — its own project.
- **Y8.3** `BlockProjectileFactory` → `PrismFactory` fold (AUDIT §2.8); scope: only the
  destroyed-prism recycle path is broken (one-way consumption is the conserved-mass norm).

## Starting prompt (new Claude Code session)

```
Read Docs/UnifiedSystems/AUDIT.md (method + evidence), Docs/UnifiedSystems/YASH.md
(my work list), and Docs/UnifiedSystems/GARRETT.md §1 (decision gates — unmarked
decisions mean the recommended default applies only to [default-ok] items; [hard-gate]
items wait). Start with Y0.1 and Y0.2, one item per commit on a
claude/unified-yash-<item> branch off bleeding-edge. Before every deletion or de-wire,
re-verify the AUDIT claim with a guid grep (.cs.meta guid across *.prefab/*.unity/
*.asset) and a repo-wide class-name grep — line numbers may have drifted. Follow
Docs/ScoringSystem/REFACTOR.md ground rules (SOAP-first, fail-loud, no new statics or
singletons, no temporary fixes). For anything touching Joust/CrystalCapture/HexRace
scenes, run the MPPM checks in Docs/ScoringSystem/TESTS.md before pushing. Do not
start Y2 until Garrett has marked D19 and D16 is resolved.
```
