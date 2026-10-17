# Refactor Backlog — code & architecture (measured 2026-10-17)

Measured on `Ys-bleeding-edge` @ `419ceebe` (contains bleeding-edge `a67fee38`, PR #1165) against
`CLAUDE.md`, `Docs/claude/ANTI_PATTERNS.md`, `Docs/claude/ARCHITECTURE_CORE.md`,
`Docs/claude/KEY_SYSTEMS_AND_CONVENTIONS.md`, `Docs/ASSEMBLY_SPLIT.md` and every open row in
`Docs/{Party,Presence,Scoring}System/REFACTOR.md`, `Docs/ElementalAbilitySystem/BACKLOG.md`,
`Docs/ToySystem/BACKLOG.md`, `Docs/LAUNCH_BLOCKER_INDEX.md`, `Docs/AI_SYSTEM/`. Method: the
`/refactor` skill (classify, then measure; counts are `git grep` / comment-stripped scans and
approximate). The first pass changed no code; **§0 records the basic-refactor pass that
followed the same day** (on `Ys-bleeding-edge` only). Each row is a claim as of its date; re-run
its evidence before acting on it (`/refactor` §2).

Size key: **S** ≤1 day, mechanical, few files · **M** bounded multi-file, a few days ·
**L** architectural / long migration / LOCKED system broadly. `LOCKED` = party, scoring, ecology,
threading — the system's own rules (per-commit risk gate, MPPM smoke test) apply.

## Headline

| | count |
|---|---|
| **Not-too-complex refactors (S + M), de-duplicated** | **48** (28 S · 20 M) |
| of which LOCKED-area (party / scoring / ecology) | 9 |
| Large architectural refactors (L) — own branch + playtest each | 10 |
| Design gaps found (Incompleteness — a report, **not** a refactor) | ~15, listed in §5 |

Codebase: 2,490 first-party `.cs` under `Assets/_Scripts` (**~551k lines**, 388 editor files),
one first-party asmdef (`CosmicShore.Data`, 60 files); everything else is Assembly-CSharp.

## 0. Landed 2026-10-17 — the basic pass (`Ys-bleeding-edge` only, not bleeding-edge)

Every C# commit was compiled headless with `Tools/Build/unity_refcompile` (player: 0 errors in
project code; editor config: 0 errors in changed files — its 3 standing errors are in the untouched
`AuthenticationServiceFacade.cs`, from the unfetchable `Unity.Multiplayer.Playmode` package) and
the five standing textual gates. **`/verify-unity` did not run** (no Unity Editor in the cloud
session); open the project once and let it compile.

| row | what landed | what the measurement corrected |
|---|---|---|
| **S1** | `check_enum_member_references.py` scopes every NESTED enum to its own file; gate green, `--self-test` 13/13, negative control still catches a real rename | Two false-positive shapes, not one: excluding the private `Placement` unmasked the public nested `OnlineInfoEntry.Status` vs DrillRunner's `Status` property |
| **S2** | `ThresherLaunchSight.cs.meta` committed (fresh guid; nothing serialized referenced the type) | It was the ONLY `.cs` under `Assets/_Scripts` without a `.meta` |
| **S3** | 61 enums / 56 files numbered with their current values; csc-compiled before/after, 0 of 61 differ (negative control: 1 planted → 1 differs) | 65 needed it (runtime, editor and `Assets/FTUE`), not "57 runtime + 6 editor"; **4 left alone because in-flight branches are editing them** (`WeeklyChallengeLeaderboardWirer.Status`, `QuestItemCard.QuestItemState` on `claude/new-session-76hnyc`; `ZoomOutActionSO.ScaleSource`, `ZoomOutActionExecutor.State` on `cece/relaxed-hopper-r62hbd`) — number them after those merge |
| **S5** | the 4 dead `#else FindObjectOfType` branches removed (AppManager ×3, ShipAudioController) | Amoebius compiles those `#else` branches; its `FindObjectOfType` is an alias of the same method, so no-op there too |
| **S12** | 3 pure inspectors moved to `Assets/_Scripts/Editor/` (`CameraSettingsSOEditor`, `ShapeDefinitionEditor`, `UniversalStatsProviderEditor`), base named `UnityEditor.Editor` | Only 3 of the "7" can move: `SerializeProperty`/`ResourceDisplay` mix runtime + drawer in one file, runtime code calls `LogControlWindow`, `RewindDrawer` does not exist |
| **S17** (part) | `SingletonNetwork`, `SingletonNetworkPersistent`, `SingletonScriptableObject` deleted from `Singleton.cs` (generic, unreferenced anywhere) | **Still open:** deleting `LoginEventBus.cs` (+ the empty `System/Architectures/` folder) and the stale `Controller/IO/_Input Mapping/InputActionsAsset.cs` was blocked by the session's permission check — both measured unreferenced; needs a human to run the `git rm` |
| **S19/S20** | 24 sites / 16 files `GetComponent`+null check → `TryGetComponent` | The audit's ~159 was a loose pattern; the exact mechanical shape is **44** sites. 17 skipped (in-flight files), 3 skipped because the two lines are not sibling statements — folding them would have moved a null-warning inside a braceless `if` (a real behaviour change, caught in review) |
| **S28** (part) | `TOOLING.md`, `PartySystem/REFACTOR.md`, `ARCHITECTURE_CORE.md`, `AI_SYSTEM/ARCHITECTURE.md`, `ScoringSystem/REFACTOR.md` corrected with dated re-measurements | see below |

### Batch 2 (same day, also `Ys-bleeding-edge` only)

Same proof standard: headless compile, player and editor config, 0 errors in changed files, and none of the changed files in the unfetchable-package bucket, where errors would be hidden.

| row | what landed | what the measurement corrected |
|---|---|---|
| **S11** | `ASSEMBLY_SPLIT` phase-2 steps 1–2: `CrystalStats` / `PrismStats` / `CombatHitStats` / `AbilityStats` → `Data/Structs/`, `GameplaySFXCategory` → `Data/Enums/` (namespaces kept, moved text compared line for line); `RoundStats.cs` `git mv`'d to `Controller/Player/` | `RoundStats` was the **only** `Data/` file using Netcode or Collections, so `CosmicShore.Data`'s asmdef `references` is now `[]` — a true leaf |
| **S26** | `AIPilot`'s `#region Unused Methods` (3 private methods + a commented coroutine) and the `_hit` field only it used, deleted | The `Port/` hits are Amoebius's hand-ported copy, not callers |
| **C9** (part) | `SeedAssembler` / `CloakSeedWall` read `VesselPrismController.SecondaryTrail` instead of reflecting on the private `Trail2` | **Not done:** dropping `SeedAssembler`'s swallowing `catch {}` — it changes what happens when `StopBonding` throws, so it is a behaviour change |
| **S9** | `ButterflyAutopilotModeDriver` → `Controller/AI/Butterfly/` (guid kept), `ScarabScrambleJukePlanner` extracted verbatim to `Controller/AI/Scarab/` (AI `ARCHITECTURE.md` 4.3) | Three generators (Dustup, Sirocco, Tapestry) AUTHOR the driver's `.meta` and its folder's `.meta`; they now point at the new path, and the new folder's `.meta` carries the guid they mint (`--check` green, negative control catches a wrong guid) |

**Measured and deliberately left this batch:**
- **M5 (class name ≠ file name):** 19 concrete types (mostly Ship→Vessel leftovers, plus `MinigameHUDView.cs` → `MiniGameHUDView` and `SO_ColorPalette.cs` → `SO_Color_Palette`); 13 of them are on assets (e.g. `R_VesselElementStatsHandler.cs` → `class R_ShipElementStatsHandler`, on **16** assets). The repo's own docs (`BRANCHING_AND_RELEASE.md`, `check_generated_assets.py`) say Unity then cannot load the script. But `VesselStatus` both `[RequireComponent]`s and `GetOrAdd`s the component, so a failed binding would be hidden by a fresh default-valued component. An editor has to settle it (team task) before anyone renames classes or files.
- **Toast reflection** (`ToastNotificationAPI`/`Manager` set private fields by name; same shape as C9): every toast file is in flight on `claude/new-session-76hnyc` (Shombith's menu branch). Do it after that merges.
- **S13** `PrismSpatialIndex` job split: two `prisma-*` branches edit line 96, inside the region that would move.
- **S16** shared Swarm hash: four one-line expressions with different modulus constants, in protected ecology code that two `tandava` branches are editing. A helper would add an indirection and prevent nothing, so **not worth its code**.
- **S22** Grizzly dangling guid: `Grizzly.prefab` is in flight on `grizzly-v2` and `grizzly-v3`.
- **S24** toy emblem material leak: `Docs/ToySystem/BACKLOG.md` says itself that the fix changes matrix-station material ownership — not a no-op.

**Re-measured and NOT a basic refactor:** **S6** `"Menu_Main"` — of the 16 literals, 7 are the
deliberate fallback when `SceneNameListSO` is unassigned, and 6 are in the party layer (LOCKED), 3 of
them presence-protocol payloads rather than scene loads; leave it to the party work. **S27**
`CoOpScoreBoard` — the class has **no** scene/prefab/asset referrer at all, although
`ScoringSystem/ARCHITECTURE.md` lists it for Wildlife Blitz co-op; renaming a field on a class
nothing uses is pointless — it is a vestige proposal (with `UI/Model/MinigameHUDInspector.cs`,
which is `#if false`).

## What the docs were wrong about (re-measured)

- `TOOLING.md` "standing debt: 11 RED generators" — **all 101 `author_*.py --check` pass**. Close the row.
- `Party/REFACTOR` "PartyInviteController ~450 lines" — **731**; `ARCHITECTURE_CORE` "3 Yields in PIC" — **6**.
- `Scoring/REFACTOR` name-match in final RPCs "~14 files" — **21**; `*SignalColor` gamma rows grew (21/12/5 vs 16/5/4).
- `Scoring/REFACTOR` still cites `Scoreboard.cs:147,454` (`IsMultiplayerMode`, now 0 hits) and `GameCanvas-SkimRace.prefab` (gone). *(Fixed 2026-10-17.)*
- `AI_SYSTEM` "three private `IsAutopilotDriven` copies" — **two** (Grizzly, Manta).
- `FTUE_DIALOGUE_AI.md` documents `DialogueManager` / `DialogueUIController` as live — no asset or C# references them.
- *(Fixed 2026-10-17, §0 S1.)* **The `check_enum_member_references.py` gate is red on bleeding-edge** with 14 false positives
  (`'Placement' has no member …`): it resolves `Toy.Placement` (type `ToyPlacement`) against a private
  nested `enum Placement` in the editor-only `CrystalFlipWaveBenchmarkWindow.cs:42` (2026-10-08).
  A permanently red gate stops being read.

## 1. Quick wins — S (28)

| # | Refactor | Kind | Evidence | Notes |
|---|---|---|---|---|
| S1 | Fix enum gate false positive (scope nested enums, add self-test) | Consistency | 14 findings, all `Placement` | Highest leverage: unblocks every branch's gate |
| S2 | Commit `ThresherLaunchSight.cs.meta` | Consistency | `.cs` present, no `.meta` (c30d5401) | Each machine invents its own guid |
| S3 | Explicit numeric values on enum members | Consistency | 57/284 runtime enums, 191 members; ~15–18 are serialized field types | Number in current order; serialized first |
| S4 | `async void` → `async UniTaskVoid` + try/catch | Consistency | 28 in 11 files; ~11 are not event handlers | Party/auth files are LOCKED |
| S5 | Delete dead `#else FindObjectOfType` branches | Vestige | 4 sites behind `UNITY_2023_1_OR_NEWER` | AppManager, ShipAudioController |
| S6 | `"Menu_Main"` literals → `SceneNameListSO` | Unification | 16 literals in 8 files | |
| S7 | Lifecycle static events → SOAP (`ApplicationStateMachine`, `FlightClock`) | Unification | 5 `static event`, 6 subscribers | Documented "legacy bridge" |
| S8 | One `IsAutopilotDriven` on vessel status | Unification | 2 private copies; 15 executors read `AutoPilotEnabled` | |
| S9 | Hull AI to one home (`git mv`, keep guids) | Consistency | Butterfly driver in `Arcade/ButterflyGames/`, Scarab planner in its controller | |
| S10 | Toy duplicates: shared Catmull-Rom + post-spawn setup | Unification | 2 Catmull-Rom copies; `TrySpawnRestoredPrism` mirrors `CreateBlock` | |
| S11 | ASSEMBLY_SPLIT phase 2 steps 1–2 | Unification | 4 stats structs in `StatsManager.cs:21-84`, `GameplaySFXCategory` in `AudioSystem.cs:80`, `RoundStats` NetworkBehaviour in `Data/Enums/` | Amoebius `Port/` parity |
| S12 | Move 7 editor tools out of runtime folders into `Editor/` | Consistency | `LogControlWindow`, `CameraSettingsSOEditor`, `ShapeDefinitionEditor`, `RewindDrawer`, `UniversalStatsProviderEditor`, `SerializeProperty`, `ResourceDisplay` | Prereq for any asmdef split |
| S13 | `PrismSpatialIndex.cs`: 17 top-level jobs/structs → own files | Honesty | lines 1–700 of a 3,433-line file | Pure file move |
| S14 | Finish the `PrismRenderService` partial split | Honesty | already `partial`, 13 types in one file | |
| S15 | Namespace hygiene | Consistency | 23 runtime files with none (13 `Spawnable*Shape`), `CosmicShore.ECS` (7) / `.Game` (2) outside the 8 roots; `UI/View` + `UI/Views` | `[MovedFrom]` on every MB/SO; or update the convention doc |
| S16 | Shared bit-identical `SpatialHash3` | Unification | same 3D hash ×4 in Swarm, `Hash(long)` ×2 in Substrate | Must stay bit-identical (seeded layouts) |
| S17 | Delete zero-user plumbing | Vestige (proposal) | `SingletonNetwork`, `SingletonNetworkPersistent`, `SingletonScriptableObject`, `LoginEventBus` — only their own declarations; stale `Controller/IO/_Input Mapping/InputActionsAsset.cs` | Human verdict per `/refactor` §6 |
| S18 | `renderer.material = X` → `sharedMaterial` | Consistency | ~10 setters (Crystal, Mine, AOEExplosion, AOEConicExplosion, Flow/WarpFieldView) | |
| S19 | `GetComponent` + null check → `TryGetComponent` (gameplay batch) | Consistency | ~80 of ~159 sites | |
| S20 | Same, UI batch | Consistency | ~80 sites (FriendsListPanel 7, EpisodeScreen 5 …) | |
| S21 | Party: one timeout value | Consistency | `Bootstrap.unity:4453` = 30 vs prefab 10 | LOCKED |
| S22 | Grizzly dangling guid `dcf9d69a…` | Consistency | 1 referrer, no owning `.meta` | |
| S23 | `InputDeviceIconSetSwitcher` on Sparrow + Dolphin HUDs | Consistency | 0 occurrences in `SparrowHUDVariant` | Four-icon row (LOCKED structure) |
| S24 | Toy emblem material leak → shared `Material` overload | Consistency | `SpawnMatrixToy:1176`, `ToyVesselRoster:138` | |
| S25 | Seed Wall: `Trail2` reflection → `SecondaryTrail`, drop swallowing `catch {}` | Consistency | `SeedAssembler:145,162`, `CloakSeedWall:497` | |
| S26 | `AIPilot.cs` `#region Unused Methods` | Vestige | `AIPilot.cs:1381` | |
| S27 | `CoOpScoreBoard.OppponentScoreTextField` typo | Honesty | `CoOpScoreBoard.cs:9` | `[FormerlySerializedAs]` |
| S28 | Docs honesty pass: close/re-measure the stale rows above (H1–H8) | Honesty | see "What the docs were wrong about" | Docs-only |

## 2. Bounded — M (20)

| # | Refactor | Kind | Evidence | Notes |
|---|---|---|---|---|
| M1 | `Renderer.material` getters → MaterialPropertyBlock | Consistency | ~14 leaking reads (VesselAnimation ×5, VesselHelper ×4, DeployTeamCrystalAction, SSU widgets) | Per-material-index MPB rules |
| M2 | Raw `Debug.LogWarning/LogError` → `CSDebug`, extend the console gate | Consistency | 212 runtime sites | 3 batches + gate change |
| M3 | Coroutine/async mix → UniTask | Consistency | 5 classes: Crystal, Skimmer, SpectatorController, FriendsListPanel, ProfileModal | Crystal/Skimmer hot — check allocations |
| M4 | TODO/FIXME triage (delete stale, rest → `team_tasks.py`) | Honesty | 100 (MiniGame.cs 14, Hangar 6) | |
| M5 | Class name ≠ file name (Ship→Vessel rename left classes behind) | Honesty | ~25 types, 13 asset-referenced (`VesselHUD.cs`→`ShipHUD`, `SlowVesselViewer.cs`→`SlowShipViewer` …) | Editor-verify each referenced one |
| M6 | Retire the deprecated `MiniGame` cluster | Vestige | `MiniGame` (DEPRECATED) still base of unreferenced `ProtectMissionGame`; `CellControlTurnMonitor` uses it | |
| M7 | Collapse never-placed legacy TurnMonitor bases into Network subclasses | Unification | CrystalCollision, Joust: legacy base in 0 assets | `/EndGameConditions` skill |
| M8 | Dead-code sweep (proposals with evidence, cluster by cluster) | Vestige | 139 MB/SO scripts: guid in no asset, class named by no other `.cs` (~9,900 lines): MiniGameObjects 21, Spawning/Generators 17, legacy VesselActions 11, 6 TurnMonitors, 13 SOAP listener shims, dialogue runtime … | ~8 clusters; human verdict |
| M9 | One toast system | Unification | `ToastNotificationManager`, `ToastService`, `GameToastController` (+ `Notification System/`) | |
| M10 | One access path per dual-access service | Consistency | AudioSystem 39 `[Inject]`/60 `.Instance`, PlayerDataService 10/32, UGSDataService 5/12, CameraManager 1/74, HostConnectionService 1/36 | One service per commit |
| M11 | Route direct `SceneManager.LoadScene*` through SceneLoader/SceneTransitionManager | Consistency | 9 files (PartyInviteController, ReconnectService, SplashToAuthFlow …) | Party files LOCKED |
| M12 | Vessel HUD naming (`XVesselHUDController` vs `XHUDController`) | Honesty | 7 / 7 | Guids unchanged |
| M13 | Partial-class splits along existing regions | Structure | ArcadeGameConfigureModal 3,447, Cell 3,478 (LOCKED), AstroLeagueBall 2,864, HostConnectionService 2,629 (LOCKED), Projectile 2,105, Prism 1,927, ScreenSwitcher 1,906, GameDataSO 1,478, ThresherExecutor 1,712, VesselTransformer 1,557 | M each, behaviour-neutral; undo before asmdef extraction. Excludes generated tables (NestedGyroid, Borromean, Mandelbulb) |
| M14 | Scene-singleton `Find*ByType` → injection or SOAP variable | Unification | ~40 of 98 sites (0 unthrottled per-frame) | ~20 diagnostic sites stay |
| M15 | Public fields → `[SerializeField] private` (by folder) | Consistency | ~3,190 public fields in 322 MB/SO files | Exclude Burst job structs and Studio-mapped SOs (`studio_to_unity.py`). ~6 folder batches |
| M16 | Party: NTS `WaitUntil` polling → event waits + one timeout policy (U2) and HCS refresh policy object (U4) | Unification | 2 `WaitUntil`; 14 backoff/error-count hits | LOCKED |
| M17 | Party: init state machine replaces `bool _joining` (U5); extract `LobbyMembershipMonitor` (U7) | Unification | `HostConnectionService:206`; monitor 0 hits | LOCKED |
| M18 | Scoring: one `NetworkObjectId` match helper for final RPCs (U11); delete legacy `_playerCards` layout (U9) | Unification | `.Name == sName` in 21 files; 14 `_playerCards` refs | LOCKED |
| M19 | Scoring: server-ordered results (U10, RANKING_SYNC steps 4–7) | Unification | `BuildResults` per peer in ≥10 controllers | LOCKED |
| M20 | One AI pilot registry (U14) | Unification | `AIPilotRegistry` 0 hits; hard-coded Squirrel check in `SkimRaceAIDeployment.Claims` | |

Elemental M rows (V5 dead `SkimmerImpactor` overrides, V8 `explosionPrismEffects`, U19 `*SignalColor`,
C8 the 54 frozen `[Inject]` pairs) stay in `Docs/ElementalAbilitySystem/BACKLOG.md` and `ANTI_PATTERNS.md`
where they already live; they are counted inside the 48 only via M8/S-rows where they overlap.

## 3. Large — L (10, each its own branch + playtest)

1. **Assembly split phase 2 proper**: `CosmicShore.SOAP`, split `Utility`, editor asmdef, test asmdefs (`Docs/ASSEMBLY_SPLIT.md`).
2. **Service location → Reflex DI** project-wide (~560 `.Instance` sites, 20 base singletons + ~26 hand-rolled; keep Prism managers static and document why).
3. **Static events → SOAP channels** (78 sites / 44 files; settings, catalog/economy, rest).
4. **Extract-component decomposition** of Cell, HostConnectionService, ArcadeGameConfigureModal (RPCs out of a UI modal), AstroLeagueBall, Prism, GameDataSO (event hub vs roster vs config).
5. **Legacy `ShipAction` → `R_VesselActions`** (15 asset-referenced legacy scripts), then delete `VesselActions/`.
6. **Persistence unification**: PlayerPrefs (230 sites / 20 files) and `DataAccessor` JSON onto the CloudData repositories.
7. **Pooling unification** (`GenericPoolManager`/`UnityEngine.Pool`; 3 static + ad-hoc pools on hot paths; profile-gated).
8. **Party PIC extraction + one leave→reset→join operation + session SOAP container** (Party R1, U3, D3).
9. **Retire `IsMultiplayerMode`** (20 lines / 11 files re-measured 2026-10-17 — the first audit's "36 / 13, 13 in `MultiplayerHUD.cs`" was wrong; `MultiplayerHUD.cs` has none; after Scoring Q1 sign-off).
10. **`GrowTrailAction` order / boost multiplier** (Elemental #27, #18/#19): design fork first, then re-derive volumes.

## 4. Suggested order

S1 → S2 → S28 (cheap, unblock gates and docs) → S3–S8 → S11–S14 (prep for the asmdef split) →
S15–S27 → M2, M1, M3 → M5–M9 (vestige and naming) → M10/M14 (prep for L2) → M13 → LOCKED M16–M19
under their system's rules. L items only after the S/M groundwork they depend on
(L1 needs S11–S13/S15; L2 needs M10/M14; L3 needs S7).

Every C# row still owes `/verify-unity` before commit (CLAUDE.md). Rows touching `Data/` types
mirrored in `Port/` need Amoebius parity.

## 5. Not refactors — design gaps (report only)

Rhino Charge/Space, Serpent Mass and Time L5 ability slots (`FLEET_GAPS.md`); unauthored art
(`upgradedSprite`, omni-crystal cards for 6/8 vessels, Sparrow glyphs, Dolphin Space icon);
empty `SlowExplosionImpactorDataContainer`; `ScriptableEventSkimmerDebuffApplied` has no producer;
Serpent cannot collect crystals; AI never rolls; profiler markers only in SkimRace AI; Termite/
Falcon/Shrike have no near-field skimmer (confirm intent before wiring); Scoring R7/R11/R12 and
NetDiag TODO-1..8; toy unlock persistence.
