# Branch archive: `claude/unified-systems-refactor-apze59`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-08-01 by Yash Sadhukhan
- **Unmerged commits:** 59
- **Forked from:** `6f263c225` (2026-07-30, Merge pull request #637 from froglet-studio/claude/vessel-ability-icons-3idb9y)
- **Tip:** `ed661382b`
- **Files touched (414):**
  - `.claude/skills/EndGameConditions/SKILL.md`
  - `Assets/Wwise.meta`
  - `Assets/Wwise/API.meta`
  - `Assets/Wwise/API/Runtime.meta`
  - `Assets/Wwise/API/Runtime/Plugins.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86/DSP.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86/Debug.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86/Profile.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86/Release.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86_64.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86_64/DSP.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86_64/Debug.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86_64/Profile.meta`
  - `Assets/Wwise/API/Runtime/Plugins/Windows/x86_64/Release.meta`
  - `Assets/_Prefabs/CORE/Hangar.prefab`
  - `Assets/_Prefabs/CORE/Hangar.prefab.meta`
  - `Assets/_Prefabs/CORE/Player and Vessel Spawner.prefab`
  - `Assets/_Prefabs/CORE/Player and Vessel Spawner.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER).meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ArcadeScreen.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ArcadeScreen.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/IAPManager.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/IAPManager.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ModalWindows.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ModalWindows.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/NavBar.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/NavBar.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/Screens.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/Screens.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ToastNotificationContainer.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ToastNotificationContainer.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ToastNotificationManager.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ToastNotificationManager.prefab.meta`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/UGSStatsManager.prefab`
  - `Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/UGSStatsManager.prefab.meta`
  - `Assets/_Prefabs/UI Elements/LoadoutCard.prefab`
  - … and 374 more

### `3943e21dc` — docs(unified-systems): add verified audit of vestigial, parallel, and unenforced systems

_Claude, 2026-07-17 08:34:20 +0000_

```text
Measured inventory of unification debt in three classes: (A) completed
unifications whose losing generation remains in-tree, (B) parallel systems
still running side by side, (C) cross-class contracts held only by
convention. 67 findings, each guid-grep wiring-verified and independently
adversarially checked (53 confirmed, 14 adjusted, 0 refuted), including a
table of live bugs caused directly by unification debt and a five-wave
sequencing plan.
```

```text
 Docs/UnifiedSystems/AUDIT.md | 557 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 557 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 563 lines)</summary>

```diff
diff --git a/Docs/UnifiedSystems/AUDIT.md b/Docs/UnifiedSystems/AUDIT.md
new file mode 100644
index 000000000..0aeb3b940
--- /dev/null
+++ b/Docs/UnifiedSystems/AUDIT.md
@@ -0,0 +1,557 @@
+# Unified Systems — Audit
+
+**Date:** 2026-07-17 · **Branch:** `claude/unified-systems-refactor-apze59` (== bleeding-edge `1f558502`)
+**Companion doc:** `Docs/CODEBASE_OUTLINE.md` (branch `claude/game-codebase-outline-yqkz47`) — the whole-game
+atlas whose per-area observations seeded this audit's hypotheses.
+
+**What this is.** A measured inventory of the codebase's *unification debt*, in three classes:
+
+- **(A) Vestigial cleanup** — a unification already happened and won, but the losing generation's
+  corpse is still in the tree (dead classes, unwired prefab components, `[Obsolete]` assets still
+  executing, commented-out bodies kept "as reference").
+- **(B) Unify opportunities** — two (or more) parallel systems still do the same job today; one
+  should absorb the other.
+- **(C) Unenforced patterns** — a contract held across a *class* of things (the 11 vessels, the
+  domain minigames, the input strategies, "all cross-system events are SOAP") that exists only by
+  convention/comment, so members drift or silently ship incomplete.
+
+This is the same shape the Elemental Ability system just solved (`Docs/ElementalAbilitySystem/`):
+the quantitative layer became a *fleet contract* (`ElementalAbilityMapSO` per vessel, display that
+"literally cannot ship without" existing via `SilhouetteController.CreateDefaultElementBars`). This
+audit finds everywhere else that same move is either half-done or still waiting.
+
+**Method + confidence.** Every finding was produced by one auditing agent and then independently
+**adversarially verified** by a second agent attempting to refute it against the live tree. Wiring
+claims were measured, not assumed: each suspect class's `.cs.meta` guid was grepped across all
+`*.prefab` / `*.unity` / `*.asset` files, and code references were grepped repo-wide.
+"**fully dead**" below means *zero content references AND zero live code references*. Of 67
+findings, 53 verified as stated and 14 were **adjusted** (corrections incorporated below); **zero
+were refuted**. Verify at the moment of fixing anyway — line numbers drift.
+
+**Locked designs are respected.** Nothing here proposes decay/timers, client domain writes, lazy
+Relay, or SOAP violations — several findings *restore* locked designs where drift crept in.
+
+---
+
+## 0. Executive summary
+
+The codebase has completed (or nearly completed) at least **eight major unifications** — R_ vessel
+actions, `ScoringRuleSO` domain scoring, `GenericPoolManager` pooling, UGS auth/data over PlayFab,
+FMOD SFX, `ElementalBarsView`, `EventDrivenStatsProvider`, the networked spawn pipeline — and in
+every single case the losing system was left in the tree. Roughly **80+ fully-dead classes**,
+**~23 scene-less `SO_ArcadeGame` assets**, a **68k-LOC inert SDK**, and a dozen half-wired
+"successor built but never mounted" systems remain. Worse, some vestiges are not inert: the audit
+found **live bugs caused directly by unfinished unifications** (§0.1).
+
+Three unifications are genuinely *unfinished* and already have declared target states in team
+docs: **one always-networked scoring path** (`Docs/ScoringSystem/REFACTOR.md` R1), **one spawn
+pipeline** (`Player.cs:124` — "No offline single-player: every session is a Relay host"), and
+**one audio pipeline** (`AudioSystem.cs:33` header prescribes the music-on-FMOD migration).
+
+The highest-leverage *new* work is **enforcement**: fleet-contract and convention tests (the
+`EnumIntegrityTests` precedent) that turn each convention into a failing test, so none of this
+regrows.
+
+### 0.1 Live bugs caused by unification debt (fix-with-cleanup)
+
+| Bug | Root cause | Evidence |
+|---|---|---|
+| Joust results can mis-sort after game end | Legacy `NetworkScoreTracker` still enabled in the migrated Joust scene, `golfRules:0` on a golf mode, fires a **second** `SortRoundStats` + `InvokeWinnerCalculated` 500 ms after the controller's authoritative RPC (masked only by `EndGameSequencer._isRunning`) | `MinigameJoust_Gameplay.unity:10613,10618`, `NetworkScoreTracker.cs:37-47`, `EndGameSequencer.cs:110` |
+| Invert-Y / invert-throttle silently ignored on touch — the mobile-first platform | Input pipeline copy-pasted per strategy; Touch's copy omits the inversion block | `TouchInputStrategy.cs:286-304` (no `InvertYEnabled` hits), vs `GamepadInputStrategy.cs:182-202` |
+| Daily challenge play/claim NREs; faction missions, hangar training, arcade-explore launches dead | `Arcade` singleton attached to nothing (`Instance` null) and PlayFab `CatalogManager`/`DailyRewardHandler` prefabs unplaced, but 5 live UI paths still call them | `Arcade.cs:20` (guid 0 content hits), `FactionMissionModal.cs:25`, `HangarTrainingModal.cs:175`, `DailyChallengeSystem.cs:205,243` |
+| Quest track shows only mode 0; intensity unlocks fall back to defaults; quest toasts never fire | `GameModeProgressionService` exists **only** in `_Prefabs/MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab`, referenced by nothing — `Instance` is permanently null while 8+ shipping UI files null-guard around it | `GameModeProgressionService.cs:20`, `QuestTrackView.cs:180-181` |
+| Store and Port (leaderboards) screens dead-render | PlayFab teardown stopped halfway: manager prefabs unplaced, static events never fire, but screens still gate on them (UGS replacement `UGSStatsManager` exists) | `StoreScreen.cs:83,100`, `LeaderboardsMenu.cs:47,67` |
+| ProfileModal random-name flow hangs a coroutine | Live `WaitUntil` on a disabled PlayFab `GetTitleData` call | `ProfileModal.cs:193-195`, `AuthenticationManager.cs:58-80` |
+| Continuous vessel SFX play at slider² volume | Four FMOD emitter controllers hand-copy per-instance slider math on top of `AudioSystem`'s bus-level slider | `AudioSystem.cs:683,700-707`, `DriftAudioController.cs:549` etc. |
+| Settings → "Run Benchmark" button loads an unloadable scene | `BenchmarkStressTest.unity` absent from EditorBuildSettings; scene also carries the wrong controller vs. docs | `BenchmarkSceneLauncher.cs:20,40`, `EditorBuildSettings.asset` |
+| 5 Arcade UI cards launch games whose scenes don't exist | `ArcadeGames.asset` still lists BlockBandit, Darts, MazeRunner, Rampage, SlipNStride | `_SO_Assets/Games/GameLists/ArcadeGames.asset` |
+| Squirrel align-toggle & shard-toggle abilities silently no-op | Align: legacy component undispatchable + R_ asset never wired. Shard: `ShardFieldBus` bodies commented out, zero registered listeners | `Squirrel.prefab:3336`, `ShardFieldBus.cs:17,22`, `ShardToggleActionExecutor.cs:50` |
+| Sparrow full-auto block prisms never recycle when destroyed | `BlockProjectileFactory.ReturnBlock` is a commented-out stub; the one live release call site is a no-op (one-way consumption is the conserved-mass norm — the bug is only the *destroyed-prism* recycle path) | `BlockProjectileFactory.cs:50-62`, `DomainCheckProjectilePrismHitEffectSO.cs:68` |
+| Loadout/squad/training progress don't roam across devices | Live systems write `DataAccessor` local files while their finished CloudSave mirror repos load every session with zero consumers | `LoadoutSystem.cs:30,108`, `UGSDataService.cs:141-152` |
+
+---
+
+## 1. Class A — Vestigial cleanup (the unification happened; delete the corpse)
```

</details>

### `16cc36e86` — chore(cleanup): delete verified-dead legacy classes and assets (zero-risk wave)

_Claude, 2026-07-17 16:58:35 +0000_

```text
Removes only items re-verified as fully dead this session (zero content
guid references, zero live code references; remaining mentions are inside
comment blocks):

- Scoring: CompositeScoring, ScoreData, BaseScoringMode, CompositeScoringMode,
  the three NotImplementedException stubs (TurnsPlayed, VolumeAndBlocksStolen,
  TeamVolumeDifference) + their CreateScoring arms + retired enum IDs 3-6
- Turn monitors: six zero-instance stubs (CellControl, Distance,
  ResourceAccumulation, VesselCollision, VolumeCreated, VolumeDestroyed)
- Stats providers: dead per-mode HexRace/Joust/CrystalCapture providers
- Vessel: VesselCollider, ElementalFloatBinder (+ stale commented call site),
  ElementPipsView/ConfigSO + orphaned asset
- Projectiles: TrailBlockBufferManager, ExplodableProjectile
- UI: ToastRequest, HexRaceHUDView, MinigameHUDContainer, empty
  IMiniGameHUD* interfaces (+ marker removal), dead WildlifeBlitzMiniGame,
  VolumeTest controller/adapter pair
- Managers: deprecated Hangar singleton + orphaned CORE prefab
- Dialogue: DialogueUIController, DialogueEditorRuntimeTester
- Input: dead KeyboardMouseInputStrategy; moved live KeyboardInputStrategy
  from Assets/ root into Controller/IO (guid preserved)
- Audio: empty Wwise folder husk; CLAUDE.md corrected to FMOD (3 sites)
  and the _Scripts/Game note updated for the live CapsuleMembrane scripts

Deliberately NOT touched pending owner decisions: unshipped-vessel legacy
actions, mission/training stack, dead arcade SO assets, Flow/WarpField,
Notification System, VesselDecoy, ShardFieldBus, FireTrailBlock pair,
SlipnStride, SandboxBenchmark, dead animation classes, UniversalStatsProvider
framework, LoginEventBus (compile-coupled to PlayFab), all static-event
removals in live gameplay files.
```

```text
 Assets/_Scripts/Controller/Projectiles/ExplodableProjectile.cs.meta   |  11 --
 Assets/_Scripts/Controller/Projectiles/Gun.cs                         |   1 -
 Assets/_Scripts/Controller/Projectiles/TrailBlockBufferManager.cs     | 149 -----------------
 .../_Scripts/Controller/Projectiles/TrailBlockBufferManager.cs.meta   |   2 -
 Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs              | 102 ------------
 Assets/_Scripts/Controller/Vessel/ElementPipsConfigSO.cs.meta         |  11 --
 Assets/_Scripts/Controller/Vessel/ElementPipsView.cs                  | 225 -------------------------
 Assets/_Scripts/Controller/Vessel/ElementPipsView.cs.meta             |  11 --
 .../Vessel/R_VesselActions/Data Containers/VesselActionSO.cs          |   4 -
 .../_Scripts/Controller/Vessel/VesselActions/ElementalFloatBinder.cs  |  38 -----
 .../Controller/Vessel/VesselActions/ElementalFloatBinder.cs.meta      |   3 -
 Assets/_Scripts/Controller/Vessel/VesselCollider.cs                   |  19 ---
 Assets/_Scripts/Controller/Vessel/VesselCollider.cs.meta              |   3 -
 Assets/_Scripts/System/Helpers/DialogueEditorRuntimeTester.cs         |  24 ---
 Assets/_Scripts/System/Helpers/DialogueEditorRuntimeTester.cs.meta    |   2 -
 Assets/_Scripts/System/Runtime/View/DialogueUIController.cs           | 268 ------------------------------
 Assets/_Scripts/System/Runtime/View/DialogueUIController.cs.meta      |   2 -
 Assets/_Scripts/UI/Controller/MinigameHUDContainer.cs                 |  19 ---
 Assets/_Scripts/UI/Controller/MinigameHUDContainer.cs.meta            |   2 -
 Assets/_Scripts/UI/HexRaceHUDView.cs                                  |  11 --
 Assets/_Scripts/UI/HexRaceHUDView.cs.meta                             |   3 -
 Assets/_Scripts/UI/Interfaces/IMinigameHUDController.cs               |   8 -
 Assets/_Scripts/UI/Interfaces/IMinigameHUDController.cs.meta          |   2 -
 Assets/_Scripts/UI/Interfaces/IMinigameHUDView.cs                     |   8 -
 Assets/_Scripts/UI/Interfaces/IMinigameHUDView.cs.meta                |   2 -
 Assets/_Scripts/UI/ToastSystem/ToastRequest.cs                        |  37 -----
 Assets/_Scripts/UI/ToastSystem/ToastRequest.cs.meta                   |   3 -
 Assets/_Scripts/UI/View/MinigameHUDView.cs                            |   2 +-
 CLAUDE.md                                                             |   7 +-
 97 files changed, 6 insertions(+), 2737 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2836 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs b/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs
index 6e6f3e6bd..dba60ed34 100644
--- a/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs
+++ b/Assets/_Scripts/Controller/Arcade/BaseScoreTracker.cs
@@ -124,10 +124,6 @@ namespace CosmicShore.Gameplay
                 ScoringModes.TimePlayed => new TimePlayedScoring(this, gameData, multiplier),
                 ScoringModes.LifeFormsKilled => new LifeFormsKilledScoring(this, gameData, multiplier),
                 ScoringModes.ElementalCrystalsCollectedBlitz => new ElementalCrystalsCollectedBlitzScoring(this, gameData, multiplier),
-                ScoringModes.TurnsPlayed => new TurnsPlayedScoring(this, gameData, multiplier),
-                ScoringModes.VolumeStolen => new VolumeAndBlocksStolenScoring(this, gameData, multiplier),
-                ScoringModes.BlocksStolen => new VolumeAndBlocksStolenScoring(this, gameData, multiplier, true),
-                ScoringModes.TeamVolumeDifference => new TeamVolumeDifferenceScoring(this, gameData, multiplier),
                 ScoringModes.CrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier),
                 ScoringModes.OmniCrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Omni),
                 ScoringModes.ElementalCrystalsCollected => new CrystalsCollectedScoring(this, gameData, multiplier, CrystalsCollectedScoring.CrystalType.Elemental),
diff --git a/Assets/_Scripts/Controller/Arcade/HexRaceStatsProvider.cs b/Assets/_Scripts/Controller/Arcade/HexRaceStatsProvider.cs
deleted file mode 100644
index fb66774e1..000000000
--- a/Assets/_Scripts/Controller/Arcade/HexRaceStatsProvider.cs
+++ /dev/null
@@ -1,79 +0,0 @@
-using System.Collections.Generic;
-using CosmicShore.UI;
-using UnityEngine;
-
-namespace CosmicShore.Gameplay
-{
-    public class HexRaceStatsProvider : ScoreboardStatsProvider
-    {
-        [Header("Dependencies")]
-        [SerializeField] HexRaceScoreTracker scoreTracker;
-
-        [Header("Icons")]
-        [SerializeField] Sprite cleanStreakIcon;
-        [SerializeField] Sprite driftIcon;
-        [SerializeField] Sprite joustIcon;
-
-        public override List<StatData> GetStats()
-        {
-            if (scoreTracker == null)
-                return new List<StatData>();
-
-            var exposed = scoreTracker.GetExposedStats();
-            if (exposed == null || exposed.Count == 0)
-                return new List<StatData>();
-
-            var stats = new List<StatData>();
-
-            if (exposed.TryGetValue("Max Clean Streak", out var streak))
-            {
-                stats.Add(new StatData
-                {
-                    Label = "Best Streak",
-                    Value = FormatInt(streak),
-                    Icon = cleanStreakIcon
-                });
-            }
-
-            if (exposed.TryGetValue("Longest Drift", out var drift))
-            {
-                stats.Add(new StatData
-                {
-                    Label = "Longest Drift",
-                    Value = FormatFloat(drift),
-                    Icon = driftIcon
-                });
-            }
-
-            if (exposed.TryGetValue("Jousts Won", out var jousts))
-            {
-                stats.Add(new StatData
-                {
-                    Label = "Jousts Won",
-                    Value = FormatInt(jousts),
-                    Icon = joustIcon
-                });
-            }
-
-            if (exposed.TryGetValue("Max Boost Time", out var boost))
-            {
```

</details>

### `bf694647c` — docs(unified-systems): split audit into role docs with decision sheet and session prompts

_Claude, 2026-07-17 20:15:05 +0000_

```text
- GARRETT.md: decision sheet D1-D21 (recommended defaults, hard gates marked),
  owner work items, session starting prompt. Captures every intent-to-use /
  design-nuance risk surfaced by the risk review so cleanup cannot override
  roadmap intent (unshipped-vessel abilities, missions/training, store/economy,
  captains, daily challenge, cloud roaming, dialogue, skim FX look, half-built
  abilities, camera end state, fields, benchmark, music, DI policy, SP-path
  retirement, stats framework winner, IsMultiplayerMode signals)
- YASH.md: systems programs Y0-Y8 (wire fixes, scoring unification, spawn/
  controller spine, input template, PlayFab excision, SOAP migrations, impact
  helpers) with per-item risk notes, verification steps, and D-gates
- SHOMBITH.md: UI programs S0-S5 (domain-color re-unification, toast plumbing,
  HUD dedup, fleet-contract enforcement tests, renames, stats UI, hygiene)
  with the same gating
- AUDIT.md: header pointer to the role docs + executed-wave note
```

```text
 Docs/UnifiedSystems/AUDIT.md    |   6 +++
 Docs/UnifiedSystems/GARRETT.md  | 179 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/UnifiedSystems/SHOMBITH.md | 143 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/UnifiedSystems/YASH.md     | 171 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 4 files changed, 499 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 528 lines)</summary>

```diff
diff --git a/Docs/UnifiedSystems/AUDIT.md b/Docs/UnifiedSystems/AUDIT.md
index 0aeb3b940..c1c43bbfb 100644
--- a/Docs/UnifiedSystems/AUDIT.md
+++ b/Docs/UnifiedSystems/AUDIT.md
@@ -4,6 +4,12 @@
 **Companion doc:** `Docs/CODEBASE_OUTLINE.md` (branch `claude/game-codebase-outline-yqkz47`) — the whole-game
 atlas whose per-area observations seeded this audit's hypotheses.
 
+> **This is the evidence base.** The working documents are `GARRETT.md` (decision sheet D1-D21 +
+> owner items), `YASH.md` (systems engineering programs Y0-Y8), and `SHOMBITH.md` (UI
+> consolidation + tooling S0-S5). The zero-risk deletion wave described in §4 Wave 1 has been
+> partially executed on this branch (commit `16cc36e8`) — re-verify remaining claims at the
+> moment of change; line numbers drift.
+
 **What this is.** A measured inventory of the codebase's *unification debt*, in three classes:
 
 - **(A) Vestigial cleanup** — a unification already happened and won, but the losing generation's
diff --git a/Docs/UnifiedSystems/GARRETT.md b/Docs/UnifiedSystems/GARRETT.md
new file mode 100644
index 000000000..e51095705
--- /dev/null
+++ b/Docs/UnifiedSystems/GARRETT.md
@@ -0,0 +1,179 @@
+# Unified Systems — Garrett (owner: decisions, design nuance, in-editor verification)
+
+**Companions:** `AUDIT.md` (evidence base — every § reference below points there) ·
+`YASH.md` (systems engineering) · `SHOMBITH.md` (UI consolidation + tooling).
+
+**How this works.** Section 1 is the decision sheet — mark it up like `FLEET_MAPS.md` (edit in
+place: bold your pick, strike what's dead, add notes). Every gated item in YASH.md / SHOMBITH.md
+cites a `D-number`. Each decision ships with a **recommended default**; items marked
+`[default-ok]` in the other docs may proceed on the default if you haven't marked the sheet —
+items marked `[hard-gate]` wait for your explicit markup. Section 2 is your own work list.
+Section 3 is the starting prompt for your next Claude Code session.
+
+**Already done (zero-risk wave, commits `3943e21d` + `16cc36e8`):** the audit itself; ~40
+verified-dead files deleted (fully-commented corpses, zero-reference classes, throwing scoring
+stubs + retired enum IDs 3-6, six stub turn monitors, dead per-mode stats providers, Hangar
+singleton + prefab, ElementPips family, Wwise husk); stray `KeyboardInputStrategy.cs` moved into
+`Controller/IO/`; CLAUDE.md corrected (FMOD ×3, `_Scripts/Game` note). Nothing with plausible
+intent-to-use was touched.
+
+---
+
+## 1. Decision sheet
+
+### Roadmap / content intent
+
+**D1 — Unshipped vessels' legacy-only abilities.** Urchin (ghost/energize/detach/barrage),
+Grizzly (charged-fire/spin/turret), Termite (drone swarms), Falcon/Shrike (gyro/seed-assembler)
+exist ONLY as legacy `ShipAction` components that can never fire (dispatch only reaches R_
+actions — AUDIT §1.1). Their support pieces (dead `SparrowAnimationController`, Termite's
+`HUDContainer`/`ShipHUD` chain) are in the same state.
+Options: (a) **keep in place until R_ ports exist** *(recommended — zero re-derivation risk;
+costs only tree cleanliness)*; (b) delete now, git is the archive; (c) port designs to R_ now.
+Gates: YASH Y7, SHOMBITH S2 (ShipHUD chain), S5 (dead animation deletions).
+
+**D2 — Missions & training.** `ProtectMissionGame` (only mission gameplay, on the deprecated
+base) is unreachable; `FactionMissionModal` + `HangarTrainingModal` are LIVE Menu_Main UI calling
+the never-instantiated `Arcade.Instance` (those buttons NRE today — AUDIT §0.1). SlipnStride's
+scene doesn't exist.
+Options: (a) **training yes / missions parked** *(recommended — training feeds daily challenge +
+Hangar; disable the mission modal entry, keep `ThreatSpawner` wave design parked)*; (b) both
+returning — rebuild launch paths, delete nothing; (c) cut both.
+Gates: YASH Y4.3 (Arcade singleton fate), SHOMBITH S6.2 (modal hiding).
+
+**D3 — Dead arcade content.** ~23 scene-less `SO_ArcadeGame` assets; 5 still render as playable
+Arcade cards that fail at load (BlockBandit, Darts, MazeRunner, Rampage, SlipNStride);
+`PreviousAllGames.asset` referenced by nothing; the mode-32 co-op blitz stack dead end-to-end.
+Options: (a) **de-list the 5 cards now + move scene-less assets to an `_Archive` folder**
+*(recommended — stops shipping broken buttons, preserves authored config)*; (b) delete outright;
+(c) leave. Gate: SHOMBITH S6.1.
+
+**D4 — Store / economy path.** Store screen + purchase/reward cards ride disabled PlayFab
+(dead-render/NRE today). UGS Purchasing is already in the stack; `Docs/MENU_PROGRESSION_AND_IAP.md`
+exists.
+Options: (a) **port to UGS Economy per the IAP doc** *(recommended if monetization is near-term)*;
+(b) hide Store + cards now, defer the UGS build to its own project *(recommended if not)*;
+(c) delete the store UI. Gate: YASH Y4 (PlayFab excision scope). **[hard-gate]**
+
```

</details>

### `90b5524a7` — docs(unified-systems): record verified Y0 plans, corrections, and decided designs

_Claude, 2026-07-18 01:12:54 +0000_

```text
Why: Y0.1/Y0.2/Y0.3 are documented but their AUDIT claims were re-verified
in-session (guid greps on current bleeding-edge) and two load-bearing facts
surfaced that must not be lost: the source prefab never serialized
progressionConfig (Y0.1), and RoundStats.Score suppresses local OnScoreChanged
while spawned, so any mid-turn feed must be a server-side write (Y0.2).
What: verified-plan annotations under each Y0 item in YASH.md, including the
decided Y0.2 in-controller feed design and its B15 teardown requirements.
```

```text
 Docs/UnifiedSystems/YASH.md | 42 ++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 42 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UnifiedSystems/YASH.md b/Docs/UnifiedSystems/YASH.md
index 13a9ac4d6..7ccb53680 100644
--- a/Docs/UnifiedSystems/YASH.md
+++ b/Docs/UnifiedSystems/YASH.md
@@ -24,6 +24,20 @@ perf branches).
   editor: quest track renders beyond mode 0, intensity gating works, quest-complete toast fires.
   THEN delete the whole `MIgration_Prefabs (DELETE LATER)/` folder (9 prefabs, zero external
   refs — AUDIT §1.6/§1.9). *Risk: low. Player-visible payoff: the quest chain comes alive.*
+  - **Verified plan (2026-07-18, guid-grep re-check — all AUDIT claims held; one correction).**
+    Script guid `541692fb0a8f1b6478f85df5b78951a7` has exactly ONE content hit (the orphaned
+    prefab); all 9 migration-folder prefab guids have ZERO external refs (none cross-reference
+    each other either). **Correction:** the source prefab does NOT serialize `progressionConfig`
+    — wire only `questList` (`5eee61facaac4bb46b9e9892512f74cb` → `GameModeQuestList.asset`) +
+    `gameData` (`b35f33752bb10a44cb5033b5670f50aa` → `Runtime GameData.asset`), leave
+    `progressionConfig: {fileID: 0}` (code lazily creates a default; sibling
+    `ParticipationXpAwarder` is wired identically). Mount target: `Bootstrap.unity` GameObject
+    `PlayerDataService` fileID `&483927156` (add 5th component after ParticipationXpAwarder
+    `483927160`, mirroring its block shape). The service is self-contained (own singleton guard,
+    `SetParent(null)` + `DontDestroyOnLoad` in Awake, Reflex `[Inject] UGSDataService` +
+    `[Inject] AnalyticsServiceFacade` — same proven pattern as the co-located PlayerDataService).
+    6 null-guarded runtime consumers + LogControlWindow (editor) light up on mount;
+    `QuestTrackView.cs` behavior confirmed (null Instance ⇒ only quest 0 unlocked).
 - **Y0.2 De-wire the legacy `NetworkScoreTracker` from Joust + Crystal Capture scenes.** It fires
   a duplicate `SortRoundStats`+`InvokeWinnerCalculated` 500 ms after the authoritative RPC, and
   Joust's instance has `golfRules:0` on a golf mode (AUDIT §0.1/§2.1). Before removing, replace
@@ -31,10 +45,38 @@ perf branches).
   `rule.Remaining`/metric in the HUD. Verify: mid-turn score still ticks; end-game results
   identical on host + client; no second winner event (watch `EndGameSequencer`).
   *Risk: medium (touches live score display) — verify in MPPM.*
+  - **Verified plan (2026-07-18, guid-grep re-check — all AUDIT claims held; design decided).**
+    Tracker guid `7cf9c7929c7c484faf5a985004c9caee` lives in 6 scenes; in scope only Joust
+    (block `&1628508336`, `golfRules:0`, Mode 2 TimePlayed) and CC (block `&1628508336`,
+    `golfRules:0`, Mode 7 CrystalsCollected), both enabled on the "Game" GO beside the migrated
+    controllers. Removal is pure: zero `GetComponent<NetworkScoreTracker>` hits, zero serialized
+    refs to the component's fileID beyond the GO's own m_Component list; UGS reporting is
+    independent (`JoustStatsReporter`/`CrystalCaptureStatsReporter` fire on `OnMiniGameEnd` and
+    read post-RPC values). **Load-bearing mechanic (RoundStats.cs `Score` setter):** a spawned
+    client's local write does NOT fire `OnScoreChanged` — the centerline event reaches peers only
+    via server write → `n_Score` replication. (Corollary: HexRace clients tick from the server's
+    `TimePlayedScoring` loop, not from `HexRaceScoreTracker.Update`'s local write.) The
+    replacement feed must therefore be a **server-side write**. **Decided design — in-controller
+    feeds** (scene edits become pure deletions; the Y1.2 hoist later absorbs the code with no
+    second round of scene surgery): Joust — server-only 0.25 s UniTask loop (destroy-linked CTS,
+    `IsTurnRunning`-guarded, live `RoundStatsList` re-read each tick, no cached stats refs)
+    writing `Time.time - gameData.TurnStartTime` (the exact winner-finishTime expression,
+    `MultiplayerJoustController.CalculateJoustScores_Server`) into every RoundStats; CC —
+    server-only per-stats `OnCrystalsCollectedChanged` subscription writing
+    `rule.LiveMetric(stats)`, with B15 own-record teardown (turn end + `OnNetworkDespawn` +
+    `OnDestroy`; precedent: `NetworkCrystalCollisionTurnMonitor._subscribedStats`). No
+    `OnClickToMainMenu` subscription needed. Turn-start roster snapshot suffices for CC (server
+    roster complete before any turn). The 4 other tracker scenes (WildlifeBlitz co-op,
+    CellularDuel MP, Freestyle MP, 2v2CoOpVsAI) are legacy-primary — Y1.1 scope, do not touch
+    under Y0.2; class deletion is Y1.5. Note: removing a NetworkBehaviour shifts NB indices on
+    that NetworkObject — safe same-build, don't mix builds across peers.
 - **Y0.3 Touch inverts `[default-ok D9]`.** Minimal fix now (add the inversion block to
   `TouchInputStrategy.Reparameterize`, mirroring `GamepadInputStrategy.cs:182-202`) — or skip and
   let Y3 fix it by construction if you're starting Y3 immediately. If D9 says "deliberate
   exemption," document it in the strategy instead.
+  - **Verified (2026-07-18):** claim re-confirmed on current bleeding-edge — Gamepad, Keyboard,
+    and DualMouse strategies all apply `InvertYEnabled`/`InvertThrottleEnabled` post-calc;
+    `TouchInputStrategy.Reparameterize` (line ~286) contains neither. Fix remains as described.
 
 ## Y1 — Scoring unification program `[default-ok, but D21 hard-gates Y1.4]`
 
```

</details>

### `a77b39177` — refactor(scoring): hoist domain end-game protocol into the domain base

_Claude, 2026-07-20 19:49:34 +0000_

```text
Why: five domain controllers (HexRace, Joust, CrystalCapture, NucleusRush,
AstroLeague) hand-copied the same end-game convention - HasEndGame=false,
latch, roster snapshot, ~35-line ClientRpc ending in the six-step results
tail - enforced only by comments (AUDIT 3.3). A sixth mode could forget any
step.
What: MultiplayerDomainGamesController now owns the serialized ScoringRuleSO
rule (field name unchanged - scene YAML binds as before), a FinalResultsSent
latch, one protected SyncFinalResults(winnerDomain, finishTime) template and
one shared SyncFinalResults_ClientRpc tail (WinnerName -> SortRoundStats ->
CalculateDomainStats -> SetResults -> InvokeWinnerCalculated ->
InvokeMiniGameEnd); modes supply only winner detection. New
ScoringMetrics.Write mirrors Read for the metric round-trip. Deleted: the
verbatim-shadowed countdown RPC (logs ported to the parent) and the dead
EndGame() override, plus all five per-mode snapshot/RPC/latch copies
(net -350 lines).
Notes: Joust's representative WinnerName now credits the domain's top
jouster instead of an arbitrary post-sort teammate (telemetry-only;
VICTORY/DEFEAT keys off WinnerDomain). Not-yet-migrated users of the base
(CellularDuel MP, 2v2) are unchanged - no rule wired, HasEndGame path
untouched; HasEndGame stays unsealed until those modes migrate (Y1.1).
Verified by three adversarial review passes (compile, behavior, netcode/
consumers) - no player-observable regression found.
```

```text
 Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md                      |   4 +-
 .../_Scripts/Controller/Arcade/AstroLeague/AstroLeagueController.cs   | 110 +++------------------
 Assets/_Scripts/Controller/Arcade/CRYSTAL_CAPTURE.md                  |   8 +-
 Assets/_Scripts/Controller/Arcade/HEXRACE.md                          |  12 +--
 Assets/_Scripts/Controller/Arcade/HexRaceController.cs                | 113 ++--------------------
 Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs              |   2 +-
 Assets/_Scripts/Controller/Arcade/JOUST.md                            |   8 +-
 .../_Scripts/Controller/Arcade/MultiplayerCrystalCaptureController.cs | 124 +++---------------------
 Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs | 166 ++++++++++++++++++++++++++------
 Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs       | 140 +++------------------------
 .../_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs   |   6 +-
 Assets/_Scripts/Controller/Arcade/NUCLEUSRUSH.md                      |   2 +-
 Assets/_Scripts/Controller/Arcade/NucleusRushController.cs            | 105 ++------------------
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs           |  17 ++++
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |   4 +-
 15 files changed, 235 insertions(+), 586 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1339 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md b/Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md
index f3fae0f1a..a470cf9f4 100644
--- a/Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md
+++ b/Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md
@@ -31,7 +31,7 @@ Joust / Crystal Capture — solo play is just a party of one plus AI backfill.
 
 | Class | Role |
 |---|---|
-| `AstroLeagueController` | Match director (server-authoritative): kickoffs, goal attribution, celebrations, golden-goal overtime, winner banner, AI striker arming, final-score sync (HexRace/Joust/CC `SyncFinalScores_ClientRpc` pattern) |
+| `AstroLeagueController` | Match director (server-authoritative): kickoffs, goal attribution, celebrations, golden-goal overtime, winner banner, AI striker arming, final-score sync (HexRace/Joust/CC `SyncFinalResults_ClientRpc (shared MultiplayerDomainGamesController tail)` pattern) |
 | `AstroLeagueBall` | Server-simulated billiard payload (`NetworkBehaviour`). Server owns a real non-kinematic rigidbody with full **angular dynamics**; clients dead-reckon from replicated position + velocity + **angular velocity** NetworkVariables (the kinematic replica free-spins so the faceted icosphere's tumble shows everywhere). Vessel hits are a **momentum-conserving elastic bounce off the moving hull** (off-center → spin) and the ball can never clip a vessel. Carries the **last-striker's domain** (`n_LastHitDomain`) which drives the ball tint and the selective prism interaction (own color → pass through + shield; opposing unshielded → slow by mass + destroy; opposing shielded → unshield + leave). The ball bounces elastically only off the **court boundary (`AstroLeagueBoundary`) and vessels**, never off prisms. Strike velocity comes from server-side per-vessel transform sampling (vessels are transform-driven, so rigidbody velocity and remote `VesselStatus.Speed` are useless). Impact juice replicates via ClientRpc |
 | `AstroLeagueMatchMonitor` | `TurnMonitor` match clock, server-authoritative ("M:SS"/"OT" pushed by ClientRpc on the shared display channel). Pauses during celebrations; the controller decides full-time vs overtime; turn ends only on `ForceEnd()` |
 | `AstroLeagueGoal` | Accurate goal detector (server-gated): per-tick polls the ball for a genuine INWARD crossing of the goal-line plane WITHIN the mouth circle (no fat-trigger false positives, teleport-guarded); reports to `AstroLeagueController.HandleGoalServer` — attribution lives in the controller |
@@ -66,7 +66,7 @@ Clock expires           tied + goldenGoalOvertime → OVERTIME (sudden death, "O
                         else → FinishMatch(rule.ResolveWinner)
 FinishMatch             winner banner (real time) → matchMonitor.ForceEnd()
                         → OnTurnEndedCustom (server): AssignScores + Sort +
-                        CalculateDomainStats → SyncFinalScores_ClientRpc
+                        CalculateDomainStats → SyncFinalResults_ClientRpc (shared MultiplayerDomainGamesController tail)
                         → WinnerName/WinnerDomain/Results on every peer
                         → InvokeWinnerCalculated + InvokeMiniGameEnd → shared
                         end-game cinematic + scoreboard
diff --git a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueController.cs b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueController.cs
index c13d88d09..456e27c00 100644
--- a/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueController.cs
+++ b/Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueController.cs
@@ -20,7 +20,7 @@ namespace CosmicShore.Gameplay
     ///
     ///   Ready → shared 3-2-1 countdown (first kickoff count-in) → live play →
     ///   GOAL! celebration → kickoff count-in → ... → full time →
-    ///   golden-goal overtime if tied → winner banner → SyncFinalScores → shared scoreboard.
+    ///   golden-goal overtime if tied → winner banner → SyncFinalResults → shared scoreboard.
     ///
     /// Server-authoritative throughout: goal attribution (last non-defending striker),
     /// per-player GoalsScored (NetworkVariable on RoundStats), match phase, and the clock all
@@ -33,8 +33,6 @@ namespace CosmicShore.Gameplay
     {
         [Header("Astro League")]
         [SerializeField] AstroLeagueSettingsSO settings;
-        [Tooltip("Drag AstroLeagueScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
-        [SerializeField] ScoringRuleSO rule;
         [SerializeField] AstroLeagueBall ball;
         [SerializeField] AstroLeagueArena arena;
         [Tooltip("The standard Cell whose nucleus is scaled to become the spherical play boundary.")]
@@ -89,7 +87,6 @@ namespace CosmicShore.Gameplay
         enum MatchPhase { PreMatch, Kickoff, Live, Celebration, Overtime, Finished }
         MatchPhase phase = MatchPhase.PreMatch;
 
-        bool _finalResultsSent;
         Domains _matchWinner = Domains.Blue;
         CancellationTokenSource matchCts;
 
@@ -100,18 +97,16 @@ namespace CosmicShore.Gameplay
         protected override bool UseGolfRules => false;
         protected override bool UseSceneReloadForReplay => true;
 
-        // End-game runs through OnTurnEndedCustom → SyncFinalScores_ClientRpc (HexRace/Joust/
-        // CrystalCapture pattern); suppress the base turn→round→game flow so we don't get a
-        // duplicate InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
+        // End-game runs through OnTurnEndedCustom → the base SyncFinalResults template;
+        // suppress the base turn→round→game flow so we don't get a duplicate
+        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
         protected override bool HasEndGame => false;
 
         public override void OnNetworkSpawn()
         {
             base.OnNetworkSpawn();
-            gameData.ScoringRule = rule;
             numberOfRounds = 1;
             numberOfTurnsPerRound = 1;
-            _finalResultsSent = false;
             phase = MatchPhase.PreMatch;
             matchCts = new CancellationTokenSource();
 
@@ -422,7 +417,7 @@ namespace CosmicShore.Gameplay
         /// </summary>
         public void HandleGoalServer(AstroLeagueGoal goal, AstroLeagueBall scoredBall)
         {
-            if (!IsServer || _finalResultsSent) return;
+            if (!IsServer || FinalResultsSent) return;
```

</details>

### `2366ecd5b` — refactor(scoring): migrate Multiplayer Freestyle onto ScoringRuleSO (Y1.1)

_Claude, 2026-07-20 19:54:44 +0000_

```text
Why: Freestyle was one of the four legacy-primary modes still scored by the
NetworkScoreTracker pipeline (AUDIT 2.1). Its tracker's only live behavior
was the VolumeCreatedScoring mid-turn Score feed - the winner path was
unreachable (empty turn-monitor list; the sandbox never ends by design).
What: new ScoringMetric.VolumeCreated (+ ScoringMetrics Read/Write cases),
new FreestyleScoringRuleSO (config for the live feed; IsObjectiveReached
always false - the sandbox has no objective) + authored asset, and a
server-side per-stats OnVolumeCreatedChanged feed in
MultiplayerFreestyleController writing rule.LiveMetric into Score (n_Score
replication drives every peer's centerline, same as before). B15 own-record
teardown (despawn + OnDestroy). The scene drops its NetworkScoreTracker
component and wires the rule asset onto the controller.
The tracker remains in 5 scenes: Joust + Crystal Capture (Y0.2, deferred to
docs) and CellularDuel MP / WildlifeBlitz co-op / 2v2 (later Y1.1 items).
```

```text
 Assets/_SO_Assets/Scoring Rules/FreestyleScoringRule.asset            | 16 ++++++++++
 Assets/_SO_Assets/Scoring Rules/FreestyleScoringRule.asset.meta       |  8 +++++
 .../Multiplayer Scenes/MinigameFreestyleMultiplayer_Gameplay.unity    | 20 +-----------
 Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs   | 55 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs   | 33 ++++++++++++++++++++
 .../_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs.meta | 11 +++++++
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs           |  2 ++
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                           |  2 ++
 8 files changed, 128 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 158 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs
index 568b1a339..850d6dc92 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs
@@ -1,27 +1,82 @@
+using System.Collections.Generic;
 using Cysharp.Threading.Tasks;
 using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
+using CosmicShore.Data;
 using CosmicShore.Utility;
 
 namespace CosmicShore.Gameplay
 {
     public class MultiplayerFreestyleController : MultiplayerMiniGameControllerBase
     {
+        [Header("Scoring")]
+        [Tooltip("Drag FreestyleScoringRule.asset - config for the live centerline feed (volume created). The sandbox has no objective; the rule never ends the game.")]
+        [SerializeField] ScoringRuleSO rule;
+
+        // ── Live centerline feed (replaces the legacy NetworkScoreTracker's VolumeCreatedScoring) ──
+        // Server-only: RoundStats.Score suppresses the local OnScoreChanged while spawned, so only
+        // a server write (→ n_Score replication) reaches the MiniGameHUD centerline on every peer.
+        // B15 (Docs/ScoringSystem/BUGS.md): detach from THIS record only - never by iterating
+        // gameData.RoundStatsList (SceneLoader clears it before old-scene destruction) - and never
+        // gate cleanup on turn-end alone (freestyle's turn never ends; exits are scene changes).
+        readonly List<IRoundStats> _scoreFeedStats = new();
+
         public override void OnNetworkSpawn()
         {
             base.OnNetworkSpawn();
+            gameData.ScoringRule = rule;
             gameData.OnClientReady.OnRaised += OnClientReady;
+
+            if (IsServer)
+                gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
         }
 
         public override void OnNetworkDespawn()
         {
             base.OnNetworkDespawn();
             gameData.OnClientReady.OnRaised -= OnClientReady;
+
+            if (IsServer)
+                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
+            StopScoreFeed();
+        }
+
+        public override void OnDestroy()
+        {
+            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
+            base.OnDestroy();
         }
 
         void OnClientReady() => gameData.SetNonOwnerPlayersActiveInNewClient();
 
+        /// <summary>
+        /// Freestyle raises StartTurn once per player activation, so this runs repeatedly -
+        /// the Contains guard makes re-subscription a no-op while catching stats that joined
+        /// the roster since the last activation.
+        /// </summary>
+        void RefreshScoreFeed()
+        {
+            foreach (var stats in gameData.RoundStatsList)
+            {
+                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
+                stats.OnVolumeCreatedChanged += FeedScore;
+                _scoreFeedStats.Add(stats);
+            }
+        }
+
+        void StopScoreFeed()
+        {
+            foreach (var stats in _scoreFeedStats)
+            {
+                if (stats == null) continue;
+                stats.OnVolumeCreatedChanged -= FeedScore;
+            }
+            _scoreFeedStats.Clear();
```

</details>

### `b2453bf9d` — refactor(scoring): migrate Multiplayer Cellular Duel onto ScoringRuleSO (Y1.1)

_Claude, 2026-07-20 19:58:33 +0000_

```text
Why: the duel scene still scored via the legacy NetworkScoreTracker (three
volume strategies summed into Score, duplicate winner pass 500ms after
SyncGameEnd, winner derived at scoreboard time from the sorted list - never
authoritatively).
What: new ScoringMetric.VolumeActivity (created + hostile destroyed +
friendly destroyed - the exact composite the legacy strategies summed at
multiplier 1; composite = not writable back, components replicate via their
own NetworkVariables), new CellularDuelScoringRuleSO (points; time-based
mode so IsObjectiveReached never fires; results team-major with bare int
ScoreText matching DuelForCellScoreboard's legacy formatting) + authored
asset wired in the scene. MultiplayerCellularDuelController now publishes
the rule (via the domain base), feeds Score server-side from the three
volume events (B15 own-record teardown), suppresses the legacy end path
(HasEndGame=false) and calls the base SyncFinalResults template at the
FINAL round's end - producing authoritative WinnerName/WinnerDomain/Results
the duel never had. Vessel swap between rounds is latch-guarded so no swap
fires after the game ends. Scene drops its NetworkScoreTracker.
Note: this controller is also a leftover on the dead, player-unreachable
mode-32 co-op WildlifeBlitz scene (no game list entry, AUDIT 1.7, fate =
decision D3): that scene wires no rule, so its end-game is now inert under
HasEndGame=false - acceptable for unreachable content, documented in the
class doc.
```

```text
 Assets/_SO_Assets/Scoring Rules/CellularDuelScoringRule.asset         |  16 ++++
 Assets/_SO_Assets/Scoring Rules/CellularDuelScoringRule.asset.meta    |   8 ++
 .../Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity  |  24 +-----
 .../_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs   | 127 +++++++++++++++++++++++++++++---
 .../_Scripts/Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs   |  63 ++++++++++++++++
 .../Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs.meta       |  11 +++
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs           |   5 ++
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                           |   8 ++
 8 files changed, 228 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 287 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs
index 0914291b1..eefad7376 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs
@@ -1,31 +1,136 @@
-using System;
-using Cysharp.Threading.Tasks;
+using System.Collections.Generic;
 using Unity.Netcode;
-using UnityEngine;
+using CosmicShore.Data;
 using CosmicShore.Utility;
 
 namespace CosmicShore.Gameplay
 {
+    /// <summary>
+    /// Multiplayer Cellular Duel: two 120s rounds (NetworkTimeBasedTurnMonitor), vessel
+    /// ownership swap between rounds, scored by cumulative volume activity via
+    /// CellularDuelScoringRuleSO. End-game runs the base SyncFinalResults template at the
+    /// final round's end.
+    /// NOTE: this controller is also a leftover on the dead mode-32 co-op WildlifeBlitz
+    /// scene (player-unreachable, in no game list - AUDIT 1.7, fate is decision D3). That
+    /// scene wires no rule, so its end-game is inert under HasEndGame=false - acceptable
+    /// for unreachable content; D3 decides prune-or-revive.
+    /// </summary>
     public class MultiplayerCellularDuelController : MultiplayerDomainGamesController
     {
+        // Duel ends through OnTurnEndedCustom → the base SyncFinalResults template at the
+        // final round; suppress the base turn→round→game flow so we don't get a duplicate
+        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
+        protected override bool HasEndGame => false;
+
+        // ── Mid-turn centerline feed (replaces the legacy NetworkScoreTracker's three
+        // volume strategies, Y1.1). Server-only: RoundStats.Score suppresses the local
+        // OnScoreChanged while spawned, so only a server write (→ n_Score replication)
+        // reaches the MiniGameHUD centerline on every peer. B15
+        // (Docs/ScoringSystem/BUGS.md): detach from THIS record only - never by iterating
+        // gameData.RoundStatsList - and never gate cleanup on turn-end alone.
+        readonly List<IRoundStats> _scoreFeedStats = new();
+
+        public override void OnNetworkSpawn()
+        {
+            base.OnNetworkSpawn();
+            if (IsServer)
+                gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            if (IsServer)
+                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
+            StopScoreFeed();
+            base.OnNetworkDespawn();
+        }
+
+        public override void OnDestroy()
+        {
+            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
+            base.OnDestroy();
+        }
+
+        /// <summary>
+        /// Runs at every round's turn start - the Contains guard makes re-subscription a
+        /// no-op. The rule-null case is the leftover co-op WildlifeBlitz scene, which keeps
+        /// its legacy Score source untouched.
+        /// </summary>
+        void RefreshScoreFeed()
+        {
+            if (rule == null) return;
+            foreach (var stats in gameData.RoundStatsList)
+            {
+                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
+                stats.OnVolumeCreatedChanged += FeedScore;
+                stats.OnHostileVolumeDestroyedChanged += FeedScore;
+                stats.OnFriendlyVolumeDestroyedChanged += FeedScore;
+                _scoreFeedStats.Add(stats);
+            }
+        }
+
+        void StopScoreFeed()
```

</details>

### `468e12290` — refactor(scoring): migrate SP Cellular Duel onto ScoringRuleSO (Y1.1)

_Claude, 2026-07-20 20:00:54 +0000_

```text
Why: the single-player duel scene scored via the offline ScoreTracker
(three volume strategies + a duplicate winner pass on OnMiniGameEnd), and
the SP EndGame path lacked CalculateDomainStats and never produced
Results/WinnerName (AUDIT 2.2 measured drift).
What: SinglePlayerCellularDuelController now carries the shared
CellularDuelScoringRule asset (same asset as the multiplayer duel),
publishes it, feeds Score locally from the three volume stat events (SP
players are not network-spawned, so local writes raise OnScoreChanged
directly; B15 own-record teardown), and overrides EndGame with the
rule-driven tail - AssignScores, Sort, CalculateDomainStats,
SetResults(BuildResults) (WinnerName/WinnerDomain derived from Results[0]),
InvokeWinnerCalculated, InvokeMiniGameEnd - the SP twin of the domain
base's SyncFinalResults. The scene drops its ScoreTracker component; the
offline ScoreTracker class now survives only in the two Recording Studio
tool scenes.
```

```text
 Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity         | 25 +--------
 .../_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs  | 96 ++++++++++++++++++++++++++++++---
 2 files changed, 90 insertions(+), 31 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 121 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs b/Assets/_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs
index 98a0391aa..b90c0747a 100644
--- a/Assets/_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs
+++ b/Assets/_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs
@@ -1,21 +1,103 @@
-
+using System.Collections.Generic;
+using UnityEngine;
+using CosmicShore.Data;
 
 namespace CosmicShore.Gameplay
 {
     /// <summary>
-    /// Cellular Duel game mode.
-    /// Features: 2-player duel with vessel swapping between rounds
+    /// Cellular Duel game mode (single-player scene).
+    /// Features: 2-player duel with vessel swapping between rounds. Scored by
+    /// CellularDuelScoringRuleSO (same asset as the multiplayer duel): each player's
+    /// Score is their cumulative volume activity, fed locally from the volume stat
+    /// events; EndGame runs the rule-driven results tail (the SP twin of the domain
+    /// base's SyncFinalResults - no RPC, single machine).
     /// </summary>
-    public class SinglePlayerCellularDuelController : SinglePlayerMiniGameControllerBase 
+    public class SinglePlayerCellularDuelController : SinglePlayerMiniGameControllerBase
     {
+        [Header("Scoring")]
+        [Tooltip("Drag CellularDuelScoringRule.asset - the per-mode scoring strategy (winner, scores, results). Shared with the multiplayer duel.")]
+        [SerializeField] ScoringRuleSO rule;
+
+        // ── Mid-turn centerline feed (replaces the legacy offline ScoreTracker's three
+        // volume strategies, Y1.1). SP players are not network-spawned, so local Score
+        // writes raise OnScoreChanged directly. B15 discipline still applies: detach from
+        // THIS record only, never by iterating gameData.RoundStatsList, with an OnDestroy
+        // safety net.
+        readonly List<IRoundStats> _scoreFeedStats = new();
+
         protected override bool ShouldResetPlayersOnTurnEnd => true;
-        
+
+        protected override void Start()
+        {
+            gameData.ScoringRule = rule;
+            gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
+            base.Start();
+        }
+
+        protected override void OnDisable()
+        {
+            if (gameData != null)
+                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
+            StopScoreFeed();
+            base.OnDisable();
+        }
+
+        public override void OnDestroy()
+        {
+            StopScoreFeed(); // B15: destruction paths that bypass OnDisable ordering must still detach
+            base.OnDestroy();
+        }
+
+        void RefreshScoreFeed()
+        {
+            foreach (var stats in gameData.RoundStatsList)
+            {
+                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
+                stats.OnVolumeCreatedChanged += FeedScore;
+                stats.OnHostileVolumeDestroyedChanged += FeedScore;
+                stats.OnFriendlyVolumeDestroyedChanged += FeedScore;
+                _scoreFeedStats.Add(stats);
+            }
+        }
+
+        void StopScoreFeed()
+        {
+            foreach (var stats in _scoreFeedStats)
+            {
+                if (stats == null) continue;
+                stats.OnVolumeCreatedChanged -= FeedScore;
+                stats.OnHostileVolumeDestroyedChanged -= FeedScore;
+                stats.OnFriendlyVolumeDestroyedChanged -= FeedScore;
```

</details>

### `11ccc36e7` — refactor(scoring): migrate SP Wildlife Blitz onto ScoringRuleSO (Y1.1)

_Claude, 2026-07-20 20:06:44 +0000_

```text
Why: the blitz scored through SinglePlayerWildlifeBlitzScoreTracker (a
BaseScoreTracker subclass): kill/crystal counting via static events raced
the strategy-sum overwrites for Score, and the end path fired the family's
duplicate SortAndInvokeResults.
What: new standalone WildlifeBlitzScoreKeeper (no BaseScoreTracker
inheritance) owns the composite - Score recomputed absolutely as
hostileVolume + 5*kills + 20*crystals (the legacy scoringConfigs weights,
now serialized killPoints/crystalPoints) - plus the UGS blitz report, which
now fires on OnMiniGameEnd AFTER the finalized Score, mirroring the
Joust/CC reporter pattern. New WildlifeBlitzScoringRuleSO (golf): a non-Blue
winner means the local player cleared the cell (Score = clear time), Blue
means DNF (999); results format time-or-DNF. The controller runs the
rule-driven EndGame tail with explicit WinnerDomain/WinnerName writes AFTER
SetResults (a DNF loss must not let SetResults derive the DNF'd local
player as winner). WildlifeBlitzStatsProvider retyped onto the keeper's
totals. The scene swaps the tracker component in place (same fileID - the
controller's and provider's references bind unchanged) and wires the rule.
The old tracker class STAYS for now: it is still wired in the out-of-build
BenchmarkStressTest.unity (D16) and referenced by the orphaned
WildlifeBlitzEndGameStatsTracker - both fall to Y1.5/D16.
```

```text
 Assets/_SO_Assets/Scoring Rules/WildlifeBlitzScoringRule.asset        |  16 ++++
 Assets/_SO_Assets/Scoring Rules/WildlifeBlitzScoringRule.asset.meta   |   8 ++
 Assets/_Scenes/Singleplayer Scenes/MinigameWildlifeBlitz.unity        |  18 +---
 .../_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs  |  64 +++++++++++++
 .../Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs.meta      |  11 +++
 .../_Scripts/Controller/Arcade/SinglePlayerWildlifeBlitzController.cs |  67 +++++++++++---
 Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs         | 158 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs.meta    |  11 +++
 Assets/_Scripts/UI/WildlifeBlitzStatsProvider.cs                      |  32 +++----
 9 files changed, 339 insertions(+), 46 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 406 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs b/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs
new file mode 100644
index 000000000..1a9066d9c
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs
@@ -0,0 +1,64 @@
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Data;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Wildlife Blitz (single-player scene): golf. Mid-turn, the local player's Score is
+    /// the blitz composite (hostile volume + weighted kills + weighted crystals, fed by
+    /// WildlifeBlitzScoreKeeper) racing the cell's CellEndGameScore threshold
+    /// (SingleplayerWildlifeBlitzTurnMonitor). At game end the LOCAL player's Score
+    /// becomes their finish time on a win or the DNF sentinel on a loss - the convention
+    /// carried by AssignScores: a non-Blue winner means the local player won and
+    /// finishTime is their clear time; Blue means the blitz timed out.
+    /// The end condition is the monitor's Score-threshold check, so
+    /// <see cref="IsObjectiveReached"/> never fires.
+    /// </summary>
+    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/WildlifeBlitz", fileName = "WildlifeBlitzScoringRule")]
+    public class WildlifeBlitzScoringRuleSO : ScoringRuleSO
+    {
+        public const float DnfScore = 999f;
+
+        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
+        {
+            // The SP blitz monitor ends the turn on the Score threshold (win) or the
+            // clock (loss); the rule only formats the outcome.
+            winner = Domains.Blue;
+            return false;
+        }
+
+        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
+        {
+            var local = gameData.LocalRoundStats;
+            if (local == null) return;
+            local.Score = winner != Domains.Blue ? finishTime : DnfScore;
+        }
+
+        public override List<ScoreResult> BuildResults(GameDataSO gameData)
+        {
+            // Golf: finish time ascending; DNF (and any AI composite scores, which are
+            // never finalized in the solo blitz) sort behind a real clear time.
+            var ordered = gameData.RoundStatsList
+                .OrderBy(s => s.Score)
+                .ThenBy(s => s.Name, System.StringComparer.Ordinal);
+
+            var rows = ordered.Select(s => new ScoreResultBuilder.Row(
+                s.Name,
+                s.Domain,
+                s.Score,
+                s.Score < DnfScore ? ScoreResultBuilder.FormatTime(s.Score) : "DNF",
+                null)).ToList();
+
+            return ScoreResultBuilder.BuildRanked(rows);
+        }
+
+        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
+        {
+            return didWin
+                ? new ScoreReveal("VICTORY", "CLEAR TIME", (int)localStats.Score, true)
+                : new ScoreReveal("DEFEAT", "CELL UNCLEARED", 0, false);
+        }
+    }
+}
diff --git a/Assets/_Scripts/Controller/Arcade/SinglePlayerWildlifeBlitzController.cs b/Assets/_Scripts/Controller/Arcade/SinglePlayerWildlifeBlitzController.cs
index a4e1b518f..573b0b46b 100644
--- a/Assets/_Scripts/Controller/Arcade/SinglePlayerWildlifeBlitzController.cs
+++ b/Assets/_Scripts/Controller/Arcade/SinglePlayerWildlifeBlitzController.cs
@@ -1,27 +1,42 @@
 using UnityEngine;
-using CosmicShore.Gameplay;
+using CosmicShore.Data;
 using CosmicShore.Utility;
+
```

</details>

### `1ac6a1815` — refactor(scoring): ObjectiveTurnMonitor base - rule check, remaining UI, B15 by type (Y1.3)

_Claude, 2026-07-20 20:12:17 +0000_

```text
Why: the three objective monitors hand-copied the same trio - server-only
rule.IsObjectiveReached end check, domain-deficit Remaining display, and the
correctness-critical B15 own-record subscription bookkeeping - enforced only
by comments (AUDIT 3.4). The non-network Crystal/Joust monitor bases had
zero content instances (dead branches kept as base classes).
What: new abstract ObjectiveTurnMonitor owns the sealed end check, the
RaiseRemainingUI helper, the roster-subscription lifecycle (subclasses
attach/detach through Attach/DetachStatsHandlers; detachment runs off the
base's own-record list in StopMonitor and a SEALED OnDestroy - the B15 leak
fix is now enforced by type), and the optional NetworkVariable target leg
(SyncTarget/PublishTarget - used by Crystal + NucleusRush; Joust's per-peer
constant resolve stays deliberate per R10). The two dead bases collapse into
the network classes (target resolution via EndConditionOverridesSO kept
intact - the End Game Conditions tool remains the only authoring path;
GetRemainingCrystalsCountToCollect keeps its individual-remaining semantics
for HexRaceScoreTracker, whose field retypes to the network monitor).
Scene guids unchanged - zero scene edits. Skill + doc references updated.
```

```text
 .claude/skills/EndGameConditions/SKILL.md                             |  14 +--
 Assets/_Scripts/Controller/Arcade/HEXRACE.md                          |   2 +-
 Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs              |   2 +-
 .../Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs     |  94 --------------------
 .../Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs       |  80 -----------------
 .../Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs.meta  |   3 -
 .../Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs         | 138 ++++++++++++-----------------
 .../Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs           |  98 ++++++++-------------
 .../Controller/Arcade/TurnMonitors/NucleusRushWaveTurnMonitor.cs      |  83 ++++--------------
 .../_Scripts/Controller/Arcade/TurnMonitors/ObjectiveTurnMonitor.cs   | 151 ++++++++++++++++++++++++++++++++
 ...ystalCollisionTurnMonitor.cs.meta => ObjectiveTurnMonitor.cs.meta} |   8 +-
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |   2 +-
 CLAUDE.md                                                             |   2 +-
 13 files changed, 277 insertions(+), 400 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 863 lines)</summary>

```diff
diff --git a/.claude/skills/EndGameConditions/SKILL.md b/.claude/skills/EndGameConditions/SKILL.md
index 7ee390ca2..93a2d1ba8 100644
--- a/.claude/skills/EndGameConditions/SKILL.md
+++ b/.claude/skills/EndGameConditions/SKILL.md
@@ -18,8 +18,10 @@ at runtime via `Resources.Load`.
 ## The rule (do not break this)
 
 - **There are NO per-scene inspector fields for these counts.** The old
-  `CrystalCollisionTurnMonitor.CrystalCollisions` and `JoustCollisionTurnMonitor.collisionsNeeded`
-  `[SerializeField]`s were removed on purpose. **Do not re-add `[SerializeField]` to them** — they
+  `CrystalCollisions` and `collisionsNeeded` `[SerializeField]`s (now plain fields on
+  `NetworkCrystalCollisionTurnMonitor` / `NetworkJoustCollisionTurnMonitor` - the non-network
+  base classes were collapsed into them under `ObjectiveTurnMonitor`, Y1.3) were removed on
+  purpose. **Do not re-add `[SerializeField]` to them** — they
   are now plain internal fields that just hold the *resolved* value. If you want a count changed,
   change it in the tool, not in a scene.
 - **`0` = auto/default, `> 0` = explicit count** (same semantic the old field had, moved into the tool):
@@ -42,8 +44,8 @@ at runtime via `Resources.Load`.
 EndConditionOverridesSO (Resources/EndConditionOverrides.asset)   ← edited by the Tools window
         │  GetCrystalCount(mode, autoCalc) / GetJoustCount() / GetMaelstromWinTarget()
         ▼
-CrystalCollisionTurnMonitor.GetCrystalCollisionCount()   → resolves CrystalCollisions (HexRace, Crystal Capture)
-JoustCollisionTurnMonitor.StartMonitor()                 → resolves collisionsNeeded (Joust)
+NetworkCrystalCollisionTurnMonitor.GetCrystalCollisionCount() → resolves CrystalCollisions (HexRace, Crystal Capture)
+NetworkJoustCollisionTurnMonitor.StartMonitor()               → resolves collisionsNeeded (Joust)
 TournamentController.StartTournamentInternal()           → TournamentDataSO.ResolveWinTarget(...) (Maelstrom)
         │  (0 in the tool → waypoint auto-calc / default 3 / default 6)
         ▼
@@ -109,8 +111,8 @@ build restore.
 | Config asset (committed) | `Assets/Resources/EndConditionOverrides.asset` |
 | Editor window (the menu) | `Assets/_Scripts/Editor/EndConditionOverridesWindow.cs` |
 | Build-time auto-restore | `Assets/_Scripts/Editor/EndConditionBuildRestore.cs` |
-| Crystal modes read it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs` (`GetCrystalCollisionCount`) |
-| Joust reads it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs` (`StartMonitor`) |
+| Crystal modes read it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs` (`GetCrystalCollisionCount`) |
+| Joust reads it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkJoustCollisionTurnMonitor.cs` (`StartMonitor`) |
 | Maelstrom resolves it here | `Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs` (`StartTournamentInternal` → `ResolveWinTarget`) |
 | Maelstrom reads it here | `Assets/_Scripts/Utility/DataContainers/Tournament/TournamentDataSO.cs` (`EffectiveWinTarget`, `IsShuffleComplete`) |
 | Network sync (unchanged) | `NetworkCrystalCollisionTurnMonitor.cs` (`CrystalTargetCount`), `NetworkJoustCollisionTurnMonitor.cs` (`JoustTargetCount`) |
diff --git a/Assets/_Scripts/Controller/Arcade/HEXRACE.md b/Assets/_Scripts/Controller/Arcade/HEXRACE.md
index c507049fd..4cc88d1f3 100644
--- a/Assets/_Scripts/Controller/Arcade/HEXRACE.md
+++ b/Assets/_Scripts/Controller/Arcade/HEXRACE.md
@@ -443,7 +443,7 @@ ugsStatsManager.ReportHexRaceStats(
 
 3. **Deterministic track**: All clients must produce identical tracks from the same seed + intensity. The `SegmentSpawner` uses `Random.InitState(seed)` before spawning to ensure determinism.
 
-4. **Crystal target resolution**: The crystal target is resolved by `CrystalCollisionTurnMonitor.GetCrystalCollisionCount()` in priority order: (1) inspector `CrystalCollisions` field if non-zero, (2) `SpawnableWaypointTrack` waypoint count × laps, (3) default 39. The resolved target is synced to all clients via `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable and published to `gameData.CrystalTargetCount`.
+4. **Crystal target resolution**: The crystal target is resolved by `NetworkCrystalCollisionTurnMonitor.GetCrystalCollisionCount()` in priority order: (1) inspector `CrystalCollisions` field if non-zero, (2) `SpawnableWaypointTrack` waypoint count × laps, (3) default 39. The resolved target is synced to all clients via `NetworkCrystalCollisionTurnMonitor._netCrystalCollisions` NetworkVariable and published to `gameData.CrystalTargetCount`.
 
 5. **Comeback mechanics**: The `ElementalComebackSystem` is critical for competitive balance — it buffs losing players proportionally to their crystal deficit, preventing runaway victories. Configured via `SO_ElementalComebackProfile` with per-vessel, per-element weights.
 
diff --git a/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs b/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs
index 94c5ce3de..fff4f8b21 100644
--- a/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs
+++ b/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs
@@ -16,7 +16,7 @@ namespace CosmicShore.Gameplay
     public class HexRaceScoreTracker : BaseScoreTracker, IStatExposable
     {
         [Header("Dependencies")]
-        [SerializeField] CrystalCollisionTurnMonitor turnMonitor;
+        [SerializeField] NetworkCrystalCollisionTurnMonitor turnMonitor;
 
         [Header("Settings")]
         [SerializeField] bool showDebugLogs = true;
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs
deleted file mode 100644
index 7e8b61847..000000000
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs
+++ /dev/null
@@ -1,94 +0,0 @@
-using System; // Required for Action
-using CosmicShore.Gameplay;
-using CosmicShore.Data;
-using CosmicShore.ScriptableObjects;
-using UnityEngine;
-using CosmicShore.Utility;
-using System.Linq;
-
-namespace CosmicShore.Gameplay
```

</details>

### `aa172c9db` — docs(scoring): refresh IsMultiplayerMode fork map + D21 replacement-signal note (Y1.4)

_Claude, 2026-07-20 20:13:35 +0000_

```text
Why: ARCHITECTURE.md §8's fork map was stale (listed removed Scoreboard/
PauseMenu forks, missed the Tournament/Benchmark/Analytics sites) and D21
needs concrete replacement signals to approve before R1 can execute.
What: §8 rebuilt from a fresh measurement (2 behavioral reads, the config
RPC round-trip, 2 diagnostic reads, 7 writes, with per-site classification)
plus the Y1.4 replacement-signal design note: session gate →
HostConnectionDataSO.PartyMembers.Count > 1 (requested-session semantics);
presence → ApplicationState + the party-session id FriendPresenceActivity
already carries. GARRETT.md D21 now points at the note. Execution remains
hard-gated on D21.
```

```text
 Docs/ScoringSystem/ARCHITECTURE.md | 61 +++++++++++++++++++++++++++++++++++++++++++++----------------
 Docs/UnifiedSystems/GARRETT.md     |  5 +++++
 2 files changed, 50 insertions(+), 16 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 89 lines)</summary>

```diff
diff --git a/Docs/ScoringSystem/ARCHITECTURE.md b/Docs/ScoringSystem/ARCHITECTURE.md
index 8ccb93f95..33f2ac227 100644
--- a/Docs/ScoringSystem/ARCHITECTURE.md
+++ b/Docs/ScoringSystem/ARCHITECTURE.md
@@ -307,23 +307,52 @@ scoring data is already RPC-synced; the goal is to delete the
 single-player/multiplayer **fork** so solo-host and online render identically
 (domain-aggregated, RPC-synced).
 
-**`IsMultiplayerMode` fork map (current call sites):**
+**`IsMultiplayerMode` fork map (measured 2026-07-20 on the working tree — the
+UI forks the old map listed in `Scoreboard`/`PauseMenu` are GONE; verify with a
+fresh grep before executing R1):**
 
-| File:line | Use |
-|---|---|
-| `Controller/Managers/Arcade.cs:66,107,162` | **writes** the flag; `:107` sets `isMultiplayer && SelectedPlayerCount > 1` (so a 1-player game is marked non-MP) |
-| `Controller/Arcade/MultiplayerMiniGameControllerBase.cs:44,450` | reads/writes during config sync |
-| `UI/Scoreboard.cs:154,456` | lobby-button visibility + Play-Again host guard |
-| `UI/PauseMenu.cs:131` | pause-menu behavior |
-| `Controller/Multiplayer/MultiplayerSetup.cs:86` | session creation path |
-| `Controller/Party/HostConnectionService.cs:1714` | presence activity string |
-| `System/SceneLoader.cs:139` | log line only |
-
-**Target:** route all the above through the always-networked host model (solo =
-host + AI), driving lobby/scoreboard behavior off concrete signals
-(connected client count, `WinnerDomain`, domain membership) rather than the
-`IsMultiplayerMode` boolean, then remove the flag. **This is a discuss-first
-item** (`REFACTOR.md` R1) — agree the per-site replacement before any code.
+| Class | File:line | Use |
+|---|---|---|
+| **Behavioral read** | `Controller/Multiplayer/MultiplayerSetup.cs:84` | session gate: `if (IsMultiplayerMode)` decides shutdown-local-host + create/join a UGS Relay game session vs staying on the menu host |
+| **Behavioral read** | `Controller/Party/HostConnectionService.cs:1860` | presence: non-MP games return an empty activity string (friends see no in-game detail for solo modes) |
+| RPC round-trip | `Controller/Arcade/MultiplayerMiniGameControllerBase.cs:43` (read → ClientRpc arg) + `:467` (client write) | `SyncGameConfigToClients_ClientRpc` mirrors the host's flag onto clients |
+| Diagnostic read | `System/SceneLoader.cs:139` | log line only |
+| Diagnostic read | `System/Instrumentation/AnalyticsServiceFacade.cs:421` | `is_multiplayer` analytics payload field |
+| Write | `Controller/Managers/Arcade.cs:66,107,162` | legacy launcher writes; `:107` sets `isMultiplayer && SelectedPlayerCount > 1` (a 1-player game is marked non-MP) — `Arcade.Instance` is null today (AUDIT §0.1); fate = D2/Y4.3 |
+| Write | `Controller/Arcade/Tournament/TournamentController.cs:377` | forces `true` for every shuffle leg |
+| Write | `Controller/Settings/BenchmarkSceneLauncher.cs:42` | forces `false` for the benchmark scene (D16) |
+| Write | `Utility/DataContainers/GameDataSO.cs:255` | set from `SO_ArcadeGame.IsMultiplayer` at launch config |
+| Write | `Utility/DataContainers/GameDataSO.cs:444` | reset to `false` in `ResetRuntimeData` |
+
+(Declaration: `GameDataSO.cs:74`. `Arcade.cs:129` is `SO_Game.IsMultiplayerModes(gameMode)` —
+a different symbol, not the flag.)
+
+**Replacement-signal design note (Y1.4, for D21 sign-off):**
+
+- `MultiplayerSetup.cs:84` (session gate) — replace with **requested-session
+  semantics**: the launch config should carry an explicit "needs a shared UGS
+  game session" intent. Concrete signal: `HostConnectionDataSO.PartyMembers.Count > 1`
+  (a party of humans needs Relay) — the session exists FOR the extra humans, so
+  the party human count is the truth the boolean was approximating. Per R1/Q1
+  guidance this is domain data, not a player-count boolean read at game time.
+- `HostConnectionService.cs:1860` (presence) — replace with
+  **ApplicationState + party context**: publish in-game presence detail whenever
+  `ApplicationState == InGame`, with the party-session id already carried by
+  `FriendPresenceActivity.PartySessionId`. Whether the mode was "multiplayer"
+  is irrelevant to friends; what matters is joinability, which the party session
+  id already encodes.
+- RPC round-trip — dissolves once the two behavioral reads are gone: the flag
+  no longer needs mirroring; drop the ClientRpc arg + client write with the flag.
+- Diagnostic reads — `SceneLoader` log: delete with the flag. Analytics
+  `is_multiplayer`: replace the payload value with
+  `NetworkManager.ConnectedClientsIds.Count > 1` at report time (the actually-true
+  fact analytics wants).
+- Writes — all become dead once no reads remain; delete with the flag
+  (`Arcade.cs`'s writes fall under D2/Y4.3 regardless).
+
+**Execution stays hard-gated on D21** (`GARRETT.md`): approve the two
+replacement signals above, then R1 ships one fork-site (or small group) per
+commit per `REFACTOR.md` R1.
 
 ---
 
diff --git a/Docs/UnifiedSystems/GARRETT.md b/Docs/UnifiedSystems/GARRETT.md
index e51095705..580d1a09e 100644
--- a/Docs/UnifiedSystems/GARRETT.md
+++ b/Docs/UnifiedSystems/GARRETT.md
@@ -151,6 +151,11 @@ controller today). Gate: YASH Y2. **[hard-gate]**
 (`MultiplayerSetup.cs:84` session gate; `HostConnectionService.cs:1860` presence). REFACTOR.md Q1
 requires sign-off on the replacement signals (party human count / requested-session semantics /
```

</details>

### `a47281388` — chore(scoring): delete two verified-dead blitz classes (Y1.5, scoped)

_Claude, 2026-07-20 20:14:10 +0000_

```text
Why: Y1.5 targets the BaseScoreTracker family, but full deletion is blocked:
NetworkScoreTracker stays wired in Joust + Crystal Capture (Y0.2 deferred to
docs by owner decision) and the dead co-op-WB/2v2 scenes (D3); the offline
ScoreTracker in two Recording Studio tool scenes; HexRaceScoreTracker is
deliberately retained; SinglePlayerWildlifeBlitzScoreTracker is wired in the
out-of-build BenchmarkStressTest scene (D16). ScoringModes + the strategies
stay referenced by BaseScoreTracker.CreateScoring until those fall.
What: deletes the two members that ARE fully dead now, re-verified by guid +
class-name grep (zero content, zero code refs): MultiplayerWildlifeBlitzMiniGame
(orphan controller - its scene runs a MultiplayerCellularDuelController
leftover) and WildlifeBlitzEndGameStatsTracker (orphan, was the last
non-benchmark referent of the legacy blitz tracker).
```

```text
 Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzMiniGame.cs | 64 ----------------------------
 .../Controller/Arcade/MultiplayerWildlifeBlitzMiniGame.cs.meta        |  3 --
 .../Controller/Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs     | 74 ---------------------------------
 .../Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs.meta           |  3 --
 4 files changed, 144 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 152 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzMiniGame.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
deleted file mode 100644
index 02c7812f8..000000000
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzMiniGame.cs
+++ /dev/null
@@ -1,64 +0,0 @@
-﻿using Cysharp.Threading.Tasks;
-using Unity.Netcode;
-
-namespace CosmicShore.Gameplay
-{
-    public class MultiplayerWildlifeBlitzMiniGame : MultiplayerMiniGameControllerBase
-    {
-        int readyClientCount;
-
-        protected override void OnReadyClicked_()
-        {
-            RaiseToggleReadyButtonEvent(false);
-            OnReadyClicked_ServerRpc();
-        }
-
-        [ServerRpc(RequireOwnership = false)]
-        void OnReadyClicked_ServerRpc(ServerRpcParams rpcParams = default)
-        {
-            readyClientCount++;
-
-            // Use connected clients count (humans only - excludes AI)
-            int humanCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
-            if (readyClientCount < humanCount)
-                return;
-
-            readyClientCount = 0;
-            OnReadyClicked_ClientRpc();
-        }
-
-        [ClientRpc]
-        void OnReadyClicked_ClientRpc()
-        {
-            StartCountdownTimer();
-        }
-
-        protected override void OnCountdownTimerEnded()
-        {
-            if (!IsServer)
-                return;
-
-            OnCountdownTimerEnded_ClientRpc();
-        }
-
-        [ClientRpc]
-        void OnCountdownTimerEnded_ClientRpc()
-        {
-            gameData.SetPlayersActive();
-            gameData.StartTurn();
-            EnsureLocalHumanCanMove();
-        }
-
-        protected override void SetupNewRound()
-        {
-            SetupNewRound_ClientRpc();
-        }
-
-        [ClientRpc]
-        void SetupNewRound_ClientRpc()
-        {
-            RaiseToggleReadyButtonEvent(true);
-            base.SetupNewRound();
-        }
-    }
-}
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs b/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs
deleted file mode 100644
index 6a7858c2f..000000000
--- a/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzEndGameStatsTracker.cs
+++ /dev/null
@@ -1,74 +0,0 @@
-using CosmicShore.Gameplay;
-using CosmicShore.Utility;
-using Reflex.Attributes;
```

</details>

### `01d5feca5` — docs(unified-systems): record Y1 execution status, dead-stack scope, Y1.5 blockers

_Claude, 2026-07-20 20:14:44 +0000_

```text
 Docs/UnifiedSystems/YASH.md | 38 ++++++++++++++++++++++++++++++++++++++
 1 file changed, 38 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UnifiedSystems/YASH.md b/Docs/UnifiedSystems/YASH.md
index 7ccb53680..a010ace6d 100644
--- a/Docs/UnifiedSystems/YASH.md
+++ b/Docs/UnifiedSystems/YASH.md
@@ -80,6 +80,44 @@ perf branches).
 
 ## Y1 — Scoring unification program `[default-ok, but D21 hard-gates Y1.4]`
 
+> **STATUS (2026-07-20, executed on `claude/unified-yash-refactor-9sc0ws`):** Y1 executed with
+> Y1.2 hoisted FIRST (engineering call: the 5 migrated modes verify the hoist as a pure refactor,
+> then Y1.1's migrations onboard onto the template instead of hand-copying tails that Y1.2 would
+> immediately delete). Commits: `a77b3917` Y1.2 hoist (SyncFinalResults template + shared ClientRpc
+> tail in `MultiplayerDomainGamesController`; shadowed countdown RPC + dead EndGame override
+> deleted; 5 controllers converted; 3 adversarial review passes clean — one accepted delta: Joust's
+> representative WinnerName now credits the top jouster, telemetry-only). `2366ecd5` Y1.1
+> Freestyle (ScoringMetric.VolumeCreated + FreestyleScoringRuleSO sandbox rule + server feed;
+> tracker de-wired). `b2453bf9` Y1.1 MP CellularDuel (ScoringMetric.VolumeActivity composite +
+> CellularDuelScoringRuleSO; 2-round-aware SyncFinalResults; latch-guarded vessel swap; tracker
+> de-wired). `468e1229` Y1.1 SP CellularDuel (shares the duel rule asset; SP EndGame runs the rule
+> tail — fixes the missing CalculateDomainStats drift for this mode). `11ccc36e` Y1.1 SP
+> WildlifeBlitz (standalone WildlifeBlitzScoreKeeper off the tracker family + golf
+> WildlifeBlitzScoringRuleSO; explicit Winner* writes AFTER SetResults — the derive would show
+> VICTORY on a DNF). `1ac6a181` Y1.3 (ObjectiveTurnMonitor base: sealed rule end-check +
+> RaiseRemainingUI + B15 lifecycle by type incl. sealed OnDestroy + optional NetworkVariable
+> target leg; dead non-network Crystal/Joust bases collapsed into the network classes — zero
+> scene edits). `aa172c9d` Y1.4 doc-only (fork map refreshed + D21 replacement-signal note;
+> EXECUTION still hard-gated on D21). `a4728138` Y1.5 scoped (two verified-dead blitz classes
+> deleted; family deletion blocked — see Y1.5 note below).
+>
+> **Y1.1 dead stacks skipped (owner default):** co-op WildlifeBlitz (32) and 2v2CoOpVsAI (30) are
+> player-UNREACHABLE — their SO_ArcadeGame assets are in NO game list; the co-op scene runs a
+> `MultiplayerCellularDuelController` leftover (`MultiplayerWildlifeBlitzMiniGame` was an orphan,
+> now deleted); 2v2 has no controller class (scene carries `MultiplayerDomainGamesController`
+> directly). Their scenes keep their `NetworkScoreTracker`s; the co-op scene's end-game is inert
+> under the duel controller's `HasEndGame=false` (unreachable content). Fate = **D3** (prune or
+> revive + re-list). Consequence: `HasEndGame` cannot be sealed in the domain base until 2v2's
+> fate resolves (it relies on the legacy `SyncGameEnd` path).
+>
+> **Y1.5 remaining blockers:** `NetworkScoreTracker` wired in Joust + CC (Y0.2 — documented, not
+> executed, owner decision) and the two dead-stack scenes (D3); offline `ScoreTracker` in the two
+> Recording Studio tool scenes; `SinglePlayerWildlifeBlitzScoreTracker` wired in the out-of-build
+> `BenchmarkStressTest.unity` (D16); `HexRaceScoreTracker` deliberately retained (still extends
+> `BaseScoreTracker`); `ScoringModes` + `BaseScoring` strategies referenced by
+> `BaseScoreTracker.CreateScoring`. Unblock order: Y0.2 + D3 + D16 + a HexRaceScoreTracker
+> de-basing, then the family falls in one commit.
+
 The declared target: one always-networked, domain-aggregated scoring path (REFACTOR.md). Order:
 
 1. **Y1.1** Migrate the legacy-primary modes onto `ScoringRuleSO`: CellularDuel (SP+MP),
```

</details>

### `eed80cc58` — fix(scoring): add missing CosmicShore.Utility using to the three new rule SOs

_Claude, 2026-07-20 20:30:54 +0000_

```text
Why: GameDataSO lives in CosmicShore.Utility; the three Y1.1 rule classes
(Freestyle, CellularDuel, WildlifeBlitz) named it in their overridden
signatures without the using - the assembly would not compile (caught by the
post-commit adversarial review; every pre-existing rule SO carries the same
using for the same reason).
Also: log B17 in Docs/ScoringSystem/BUGS.md - the review established that on
a solo blitz DNF loss the EndGameSequencer reveal header shows VICTORY via
the IsLocalDomainWinner fallback (pre-existing, identical under the legacy
tracker; the Y1.1 WinnerDomain=Blue write guards only the scoreboard banner
derivation). Fix direction documented; cross-mode change, not taken here.
```

```text
 Assets/_Scripts/Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs  |  1 +
 Assets/_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs     |  1 +
 Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs |  1 +
 Docs/ScoringSystem/BUGS.md                                              | 22 +++++++++++++++++++++-
 4 files changed, 24 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs b/Assets/_Scripts/Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs
index 865188081..8877f9afe 100644
--- a/Assets/_Scripts/Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs
+++ b/Assets/_Scripts/Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs
@@ -1,6 +1,7 @@
 using System.Collections.Generic;
 using System.Linq;
 using CosmicShore.Data;
+using CosmicShore.Utility;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
diff --git a/Assets/_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs b/Assets/_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs
index 053dfffca..a5893eb50 100644
--- a/Assets/_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs
+++ b/Assets/_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs
@@ -1,5 +1,6 @@
 using System.Collections.Generic;
 using CosmicShore.Data;
+using CosmicShore.Utility;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
diff --git a/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs b/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs
index 1a9066d9c..df763a03e 100644
--- a/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs
+++ b/Assets/_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs
@@ -1,6 +1,7 @@
 using System.Collections.Generic;
 using System.Linq;
 using CosmicShore.Data;
+using CosmicShore.Utility;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
diff --git a/Docs/ScoringSystem/BUGS.md b/Docs/ScoringSystem/BUGS.md
index 4d7a509a7..aaf813239 100644
--- a/Docs/ScoringSystem/BUGS.md
+++ b/Docs/ScoringSystem/BUGS.md
@@ -398,6 +398,26 @@ Files: `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/Vesse
 `_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs`,
 `_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs`.
 
+### B17 — 🔴 SP Wildlife Blitz DNF loss: reveal header says VICTORY (pre-existing, surfaced by Y1.1 review)
+
+**Symptom:** lose the solo blitz (clock runs out, cell uncleared) - the scoreboard state is
+correct (no winner credited; DNF row), but the EndGameSequencer reveal header shows VICTORY.
+
+**Root cause:** `EndGameSequencer.DidLocalPlayerWin()` falls back to
+`gameData.IsLocalDomainWinner(...)` when `WinnerDomain == Blue`, and `IsLocalDomainWinner`
+matches the local domain against `DomainStatsList` - which always contains the local player's
+domain in a solo game, so the fallback returns true. `Domains.Blue` is overloaded: "not set"
+vs "explicitly nobody won" are indistinguishable to the sequencer. The legacy blitz tracker
+behaved identically (it also ran `CalculateDomainStats` before `InvokeWinnerCalculated`), so
+this is NOT a Y1.1 regression - but the Y1.1 blitz migration's explicit `WinnerDomain = Blue`
+write fixes only the scoreboard banner derivation, not this reveal fallback.
+
+**Fix direction (pick one, cross-mode change - verify against freestyle + all domain modes):**
+(a) a distinct "nobody won" signal (e.g. `gameData.HasWinnerResult` bool set by the end-game
+paths) that the sequencer prefers over the Blue fallback; or (b) drop the
+`IsLocalDomainWinner` fallback once every mode authoritatively writes `WinnerDomain`
+(post-Y1.1 all LIVE modes do; the fallback exists for legacy modes that never set it).
+
 ---
 
 B1–B4, B6, B7, B8 fixed (verify only — B6 also warrants a visual position check).
@@ -413,4 +433,4 @@ end flow) fixed & verified in engine 2026-06-12 — regression steps in `TESTS.m
 T15. B5 remains scheduled into **R10** (the unified ranked `ScoreResult` list
 dissolves it). B16 (ghost joust toasts / unrecorded client-observed jousts) fixed
 2026-07-16 — code-complete, engine verification pending (see B16's verification
-steps). No other open read-through findings remain.
+steps). B17 (blitz DNF reveal header) is OPEN - pre-existing, logged 2026-07-20 during the Y1.1 adversarial review. No other open read-through findings remain.
```

</details>

### `76765a9e2` — refactor(solo): consolidate Cellular Duel onto mode 29 - solo plays as host + AI

_Claude, 2026-07-20 22:56:37 +0000_

```text
Why: solo modes are retired as a concept (owner decision) - a solo game is a
multiplayer game whose party is one host, the HexRace/Joust model. Cellular
Duel existed twice: the mode-8 solo scene (offline PlayerSpawner stack) and
the mode-29 networked scene that could not backfill an AI opponent.
What: MinigameDuelForCellMultiplayer_Gameplay swaps its plain
ServerPlayerVesselInitializer for ServerPlayerVesselInitializerWithAI
in place (same fileID keeps component refs) with one Manta AI opponent
(profile-named via MainAIProfileList); ArcadeGameMultiplayerCellularDuel
gains MinDomainsAllowed: 2 so a solo host and the backfilled AI always land
on opposing domains (the duel is domain-scored). MinPlayers 2 guarantees
exactly one AI for a party of one. Deleted: the mode-8 scene +
SinglePlayerCellularDuelController + the mode-8 card (7 SO_Class Games
lists repointed to the mode-29 card; removed from the live
LaunchPartyAllGames; the four orphaned lists still holding the dead guid
are themselves deleted in the upcoming content-prune commit) + the
EditorBuildSettings entry.
```

```text
 Assets/_SO_Assets/Classes/SO_Class_Grizzly.asset                      |    2 +-
 Assets/_SO_Assets/Classes/SO_Class_Manta.asset                        |    2 +-
 Assets/_SO_Assets/Classes/SO_Class_Rhino.asset                        |    2 +-
 Assets/_SO_Assets/Classes/SO_Class_Serpent.asset                      |    2 +-
 Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset                     |    2 +-
 Assets/_SO_Assets/Classes/SO_Class_Termite.asset                      |    2 +-
 Assets/_SO_Assets/Classes/SO_Class_Urchin.asset                       |    2 +-
 Assets/_SO_Assets/Games/ArcadeGameCellularDuel.asset                  |   40 -
 Assets/_SO_Assets/Games/ArcadeGameCellularDuel.asset.meta             |    8 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerCellularDuel.asset       |    1 +
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset           |    1 -
 .../Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity  |   12 +-
 Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity         | 4559 -------------------------------
 Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity.meta    |    7 -
 .../_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs  |  113 -
 .../Controller/Arcade/SinglePlayerCellularDuelController.cs.meta      |   11 -
 ProjectSettings/EditorBuildSettings.asset                             |    3 -
 17 files changed, 19 insertions(+), 4750 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 119 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs b/Assets/_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs
deleted file mode 100644
index b90c0747a..000000000
--- a/Assets/_Scripts/Controller/Arcade/SinglePlayerCellularDuelController.cs
+++ /dev/null
@@ -1,113 +0,0 @@
-using System.Collections.Generic;
-using UnityEngine;
-using CosmicShore.Data;
-
-namespace CosmicShore.Gameplay
-{
-    /// <summary>
-    /// Cellular Duel game mode (single-player scene).
-    /// Features: 2-player duel with vessel swapping between rounds. Scored by
-    /// CellularDuelScoringRuleSO (same asset as the multiplayer duel): each player's
-    /// Score is their cumulative volume activity, fed locally from the volume stat
-    /// events; EndGame runs the rule-driven results tail (the SP twin of the domain
-    /// base's SyncFinalResults - no RPC, single machine).
-    /// </summary>
-    public class SinglePlayerCellularDuelController : SinglePlayerMiniGameControllerBase
-    {
-        [Header("Scoring")]
-        [Tooltip("Drag CellularDuelScoringRule.asset - the per-mode scoring strategy (winner, scores, results). Shared with the multiplayer duel.")]
-        [SerializeField] ScoringRuleSO rule;
-
-        // ── Mid-turn centerline feed (replaces the legacy offline ScoreTracker's three
-        // volume strategies, Y1.1). SP players are not network-spawned, so local Score
-        // writes raise OnScoreChanged directly. B15 discipline still applies: detach from
-        // THIS record only, never by iterating gameData.RoundStatsList, with an OnDestroy
-        // safety net.
-        readonly List<IRoundStats> _scoreFeedStats = new();
-
-        protected override bool ShouldResetPlayersOnTurnEnd => true;
-
-        protected override void Start()
-        {
-            gameData.ScoringRule = rule;
-            gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
-            base.Start();
-        }
-
-        protected override void OnDisable()
-        {
-            if (gameData != null)
-                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
-            StopScoreFeed();
-            base.OnDisable();
-        }
-
-        public override void OnDestroy()
-        {
-            StopScoreFeed(); // B15: destruction paths that bypass OnDisable ordering must still detach
-            base.OnDestroy();
-        }
-
-        void RefreshScoreFeed()
-        {
-            foreach (var stats in gameData.RoundStatsList)
-            {
-                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
-                stats.OnVolumeCreatedChanged += FeedScore;
-                stats.OnHostileVolumeDestroyedChanged += FeedScore;
-                stats.OnFriendlyVolumeDestroyedChanged += FeedScore;
-                _scoreFeedStats.Add(stats);
-            }
-        }
-
-        void StopScoreFeed()
-        {
-            foreach (var stats in _scoreFeedStats)
-            {
-                if (stats == null) continue;
-                stats.OnVolumeCreatedChanged -= FeedScore;
-                stats.OnHostileVolumeDestroyedChanged -= FeedScore;
-                stats.OnFriendlyVolumeDestroyedChanged -= FeedScore;
-            }
-            _scoreFeedStats.Clear();
-        }
-
```

</details>

### `3983bfec9` — refactor(solo): Wildlife Blitz plays on the networked single-host model

_Claude, 2026-07-20 23:20:49 +0000_

```text
Why: solo modes are retired - a solo blitz is a party of one hosting; co-op
teammates (human or AI backfill) join the same single scene, the
HexRace/Joust model.
What: new MultiplayerWildlifeBlitzController (domain-base spine; objective
met -> the co-op domain wins with the shared clear time; clock expired ->
Blue no-winner DNF). WildlifeBlitzScoringRuleSO becomes the co-op TEAM
rule: objective = summed blitz composite >= GoalTargetCount, Remaining
override, team-wide clear-time/DNF scores. New
WildlifeBlitzObjectiveTurnMonitor (ObjectiveTurnMonitor base) publishes the
cell's CellEndGameScore through the synced target leg and keeps the blitz
HUD's target + lifeform-counter events; the loss clock becomes
NetworkTimeBasedTurnMonitor. WildlifeBlitzScoreKeeper: every peer counts
its local sim (display + own UGS report, the old fidelity) but only the
SERVER writes Score (n_Score replication drives HUDs + the objective),
per-player attribution by event name, B15 own-record teardown.
Shared-tail hardening in MultiplayerDomainGamesController: SyncFinalResults
now accepts a Blue winner as a valid NO-WINNER end, and the ClientRpc tail
writes WinnerName/WinnerDomain AFTER SetResults so the derive can never
credit a roster row on a no-winner end (closes the B17 hazard class for
every domain mode).
Scene: spawner stack (MiniGamePlayerSpawnerAdapter + Player and Vessel
Spawner prefab) removed; SP controller/monitor/crystal-manager script-
swapped in place (same fileIDs keep refs); NetworkObject + NetcodeHooks +
ClientPlayerVesselInitializer + ServerPlayerVesselInitializerWithAI (2 AI
teammate slots) added to the Game GO; scene moved to Multiplayer Scenes
(guid preserved) + build path updated; card flips IsMultiplayer (Min 1 /
Max 3). Editor note: re-saving the scene will normalize the hand-authored
NetworkObject GlobalObjectIdHash - all peers ship the same scene file so
matching is consistent either way.
```

```text
 Assets/_SO_Assets/Games/ArcadeGameWildlifeBlitz.asset                 |   1 +
 .../MinigameWildlifeBlitz.unity                                       | 159 ++++++++++----------------------
 .../MinigameWildlifeBlitz.unity.meta                                  |   0
 Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs |  17 ++--
 .../_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs  |  76 +++++++++++++++
 .../Controller/Arcade/MultiplayerWildlifeBlitzController.cs.meta      |  11 +++
 .../_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs  |  60 ++++++++----
 .../Arcade/TurnMonitors/WildlifeBlitzObjectiveTurnMonitor.cs          |  61 ++++++++++++
 .../Arcade/TurnMonitors/WildlifeBlitzObjectiveTurnMonitor.cs.meta     |  11 +++
 Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs         | 124 ++++++++++++++++---------
 ProjectSettings/EditorBuildSettings.asset                             |   2 +-
 11 files changed, 346 insertions(+), 176 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 494 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
index 43a657765..9e9b02439 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
@@ -115,6 +115,8 @@ namespace CosmicShore.Gameplay
         /// calls are no-ops. Modes call this from <see cref="OnTurnEndedCustom"/> once their
         /// winning domain is known; <paramref name="finishTime"/> feeds golf-style
         /// <see cref="ScoringRuleSO.AssignScores"/> (pass 0 for points modes).
+        /// A <see cref="Domains.Blue"/> winner is a valid NO-WINNER end (e.g. a co-op DNF):
+        /// results still broadcast, with no representative name and DEFEAT attribution.
         /// </summary>
         protected void SyncFinalResults(Domains winnerDomain, float finishTime)
         {
@@ -125,8 +127,8 @@ namespace CosmicShore.Gameplay
 
             // Representative winner-name resolves before AssignScores mutates Score
             // (LiveMetric is Score-independent, but keep the read upfront for clarity).
-            string winnerName = ResolveWinnerRepresentativeName(winnerDomain);
-            if (string.IsNullOrEmpty(winnerName)) return; // no roster entry on the winning domain
+            string winnerName = winnerDomain == Domains.Blue ? "" : ResolveWinnerRepresentativeName(winnerDomain);
+            if (winnerDomain != Domains.Blue && string.IsNullOrEmpty(winnerName)) return; // no roster entry on the winning domain
 
             Debug.Log($"<color=#00CED1>[FLOW-10] [{GetType().Name}] Final results - domain {winnerDomain} wins ('{winnerName}', finishTime={finishTime:F2}). Broadcasting.</color>");
             FinalResultsSent = true;
@@ -196,14 +198,17 @@ namespace CosmicShore.Gameplay
                 ScoringMetrics.Write(stat, rule.Metric, metricValues[i]);
             }
 
-            // Authoritative winner - written to gameData, consumed by EndGameControllers.
+            gameData.SortRoundStats(UseGolfRules);
+            gameData.CalculateDomainStats(UseGolfRules);
+            gameData.SetResults(rule.BuildResults(gameData));
+
+            // Authoritative winner - written AFTER SetResults so the explicit values always
+            // win: SetResults derives Winner* from Results[0] when unset, which would credit
+            // a roster row on a no-winner (Blue) end and flip a DNF into VICTORY.
             // OnWinnerCalculated (below) is the "results ready" signal.
             gameData.WinnerName = winnerName.ToString();
             gameData.WinnerDomain = (Domains)winnerDomain;
 
-            gameData.SortRoundStats(UseGolfRules);
-            gameData.CalculateDomainStats(UseGolfRules);
-            gameData.SetResults(rule.BuildResults(gameData));
             gameData.InvokeWinnerCalculated();
             gameData.InvokeMiniGameEnd();
         }
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs
new file mode 100644
index 000000000..874d1c287
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs
@@ -0,0 +1,76 @@
+using UnityEngine;
+using CosmicShore.Data;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Wildlife Blitz on the networked single-host model: co-op clear-the-cell, playable
+    /// by a party of one (host) or up to three (AI teammates backfill via
+    /// ServerPlayerVesselInitializerWithAI). The TEAM's summed blitz composite (fed
+    /// server-side by WildlifeBlitzScoreKeeper) races the cell's CellEndGameScore
+    /// (published by WildlifeBlitzObjectiveTurnMonitor); the loss clock is the scene's
+    /// NetworkTimeBasedTurnMonitor. End-game runs the base SyncFinalResults template:
+    /// objective met → the co-op domain wins with the shared clear time; clock expired →
+    /// a Blue no-winner DNF end.
+    /// </summary>
+    public class MultiplayerWildlifeBlitzController : MultiplayerDomainGamesController
+    {
+        [Header("Blitz")]
+        [Tooltip("The server-authoritative blitz composite keeper (kills/crystals/volume → Score).")]
+        [SerializeField] WildlifeBlitzScoreKeeper scoreTracker;
+
+        protected override bool UseGolfRules => true;
+        protected override bool UseSceneReloadForReplay => true; // lifeforms/crystals don't reset in place
+
+        // End-game runs through OnTurnEndedCustom → the base SyncFinalResults template;
+        // suppress the base turn→round→game flow so we don't get a duplicate
+        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
+        protected override bool HasEndGame => false;
```

</details>

### `1fe9f2787` — fix(solo): prune dangling duel-card refs from 4 game lists + add 3rd blitz spawn point

_Claude, 2026-07-20 23:23:53 +0000_

```text
Review follow-ups on the C1/C2 consolidation:
- ArcadeGames/AllGames/PreviousAllGames/LeaderboardGames still carried the
  deleted ArcadeGameCellularDuel guid (null list entries; Arcade.ArcadeGames
  iteration has no null guard). Zero refs remain repo-wide.
- MinigameWildlifeBlitz seats up to 3 players but authored only 2 spawn
  points; added SpawnPoints/3 so the third vessel doesn't overlap-spawn.
```

```text
 Assets/_SO_Assets/Games/GameLists/AllGames.asset              |  1 -
 Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset           |  1 -
 Assets/_SO_Assets/Games/GameLists/LeaderboardGames.asset      |  1 -
 Assets/_SO_Assets/Games/GameLists/PreviousAllGames.asset      |  1 -
 Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeBlitz.unity | 33 +++++++++++++++++++++++++++++++++
 5 files changed, 33 insertions(+), 4 deletions(-)
```

### `704647b61` — fix(scoring): B17 - an authoritative no-winner end is never shown as VICTORY

_Claude, 2026-07-20 23:27:48 +0000_

```text
Adds GameDataSO.HasNoWinner, set on every peer by the SyncFinalResults
ClientRpc tail (true iff the broadcast winner is Blue) and reset with the
other Winner* fields. EndGameSequencer.DidLocalPlayerWin returns false for
such ends instead of falling through to IsLocalDomainWinner (which always
matched the local domain and flipped a co-op blitz DNF into a VICTORY
reveal), and the Scoreboard renders a neutral GAME OVER banner instead of
crediting the top DNF row's domain. Legacy modes that never write Winner*
keep both fallbacks (the flag stays false).

Also from the behavior review:
- WildlifeBlitzScoreKeeper: gameData is [Inject]-populated after
  Awake/OnEnable, so the OnEnable-only subscription could silently skip
  UGS reporting; retry in Start with a dedup guard (Cell.cs precedent).
- MinigameDuelForCellMultiplayer: author 2 spawn points and wire
  playerSpawnPoints (was []), so the solo-as-host duel no longer spawns
  host + AI overlapping at the origin.
```

```text
 .../Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity  | 100 +++++++++++++++++++++++++++++++-
 Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs |   1 +
 Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs         |  21 ++++---
 Assets/_Scripts/UI/Scoreboard.cs                                      |  22 +++++--
 Assets/_Scripts/Utility/DataContainers/EndGameSequencer.cs            |   4 ++
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |  13 +++++
 Docs/ScoringSystem/BUGS.md                                            |  21 ++++---
 7 files changed, 163 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 196 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
index 9e9b02439..dbc12f010 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
@@ -208,6 +208,7 @@ namespace CosmicShore.Gameplay
             // OnWinnerCalculated (below) is the "results ready" signal.
             gameData.WinnerName = winnerName.ToString();
             gameData.WinnerDomain = (Domains)winnerDomain;
+            gameData.HasNoWinner = (Domains)winnerDomain == Domains.Blue;
 
             gameData.InvokeWinnerCalculated();
             gameData.InvokeMiniGameEnd();
diff --git a/Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs b/Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs
index a6b39ae2f..491ed960d 100644
--- a/Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs
+++ b/Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs
@@ -40,6 +40,7 @@ namespace CosmicShore.Gameplay
         readonly Dictionary<string, int> _crystals = new();
         readonly List<IRoundStats> _subscribedStats = new();
         bool _isTracking;
+        bool _gameEventsSubscribed;
 
         /// <summary>Team total lifeforms killed this game (scoreboard stats + UGS).</summary>
         public int TotalLifeFormsKilled
@@ -53,22 +54,28 @@ namespace CosmicShore.Gameplay
             get { int t = 0; foreach (var v in _crystals.Values) t += v; return t; }
         }
 
-        void OnEnable()
+        // Reflex populates [Inject] fields after Awake/OnEnable on scene load, so the
+        // OnEnable attempt can see a null gameData - retry in Start with a dedup guard
+        // (the Cell.cs deferred-subscription precedent).
+        void OnEnable() => TrySubscribeGameEvents();
+        void Start() => TrySubscribeGameEvents();
+
+        void TrySubscribeGameEvents()
         {
-            if (gameData != null)
-            {
-                gameData.OnMiniGameEnd.OnRaised += ReportStats;
-                gameData.OnResetForReplay.OnRaised += ResetScores;
-            }
+            if (_gameEventsSubscribed || gameData == null) return;
+            gameData.OnMiniGameEnd.OnRaised += ReportStats;
+            gameData.OnResetForReplay.OnRaised += ResetScores;
+            _gameEventsSubscribed = true;
         }
 
         void OnDisable()
         {
-            if (gameData != null)
+            if (_gameEventsSubscribed && gameData != null)
             {
                 gameData.OnMiniGameEnd.OnRaised -= ReportStats;
                 gameData.OnResetForReplay.OnRaised -= ResetScores;
             }
+            _gameEventsSubscribed = false;
             StopTracking();
         }
 
diff --git a/Assets/_Scripts/UI/Scoreboard.cs b/Assets/_Scripts/UI/Scoreboard.cs
index 63fa81e2d..f502521ac 100644
--- a/Assets/_Scripts/UI/Scoreboard.cs
+++ b/Assets/_Scripts/UI/Scoreboard.cs
@@ -298,10 +298,14 @@ namespace CosmicShore.UI
             // produce results (freestyle, DuelForCell) fall back to the legacy path.
             if (gameData.Results is { Count: > 0 })
             {
-                var winnerDomain = gameData.WinnerDomain != Domains.Blue
-                    ? gameData.WinnerDomain
-                    : gameData.Results[0].Domain;
-                SetBannerForDomain(winnerDomain);
+                // An authoritative no-winner end (co-op DNF) must not credit the top DNF
+                // row - neutral banner instead of "<domain> VICTORY".
+                if (gameData.HasNoWinner)
+                    SetNoWinnerBanner();
+                else
+                    SetBannerForDomain(gameData.WinnerDomain != Domains.Blue
+                        ? gameData.WinnerDomain
+                        : gameData.Results[0].Domain);
```

</details>

### `2511779ee` — refactor(solo): benchmark scene runs on the networked single-host model

_Claude, 2026-07-20 23:33:54 +0000_

```text
Rebuilds BenchmarkStressTest.unity as a structural copy of the converted
MinigameWildlifeBlitz scene (NetworkObject + NetcodeHooks +
ClientPlayerVesselInitializer + ServerPlayerVesselInitializerWithAI,
NetworkCrystalManager) with the benchmark's Blob Cell Config on all four
intensity slots, its own GlobalObjectIdHash, NO turn monitors (endless),
8 spawn points, and 8 Squirrel AI templates. The old scene still carried
the retired SP spawn stack (PlayerSpawner + MiniGamePlayerSpawnerAdapter +
SP blitz controller/monitor/tracker) and was not even registered in Build
Settings, so the Settings button could not load it.

- SandboxBenchmarkController re-parents onto MultiplayerWildlifeBlitzController:
  keeps the keeper-fed live score, never ends (no monitors), and overrides
  SetupNewTurn to auto-begin (countdown -> SetPlayersActive + StartTurn RPC)
  instead of showing the Ready button.
- BenchmarkSceneLauncher: drops the IsMultiplayerMode=false write (field
  retires next commit), pins RequestedDomainCount=3 so the AI spread does
  not inherit the previous game's domain count.
- Scene moved to Multiplayer Scenes/ (guid preserved) and added to
  EditorBuildSettings (enabled). Docs/SettingsSystem/ARCHITECTURE.md updated.

In-editor verification: Settings > Run Benchmark -> scene loads via the
host's Netcode scene management, auto-starts after ~1s with no Ready click,
1 human Squirrel + AiCrowdSize AI Squirrels spawn on distinct points, HUD
score accrues, game never ends; Exit returns to menu.
```

```text
 .../BenchmarkStressTest.unity                                         | 475 +++++++++++++++++++-------------
 .../BenchmarkStressTest.unity.meta                                    |   0
 Assets/_Scripts/Controller/Arcade/SandboxBenchmarkController.cs       |  38 +--
 Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs         |  12 +-
 Docs/SettingsSystem/ARCHITECTURE.md                                   |  24 +-
 ProjectSettings/EditorBuildSettings.asset                             |   3 +
 6 files changed, 331 insertions(+), 221 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 138 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SandboxBenchmarkController.cs b/Assets/_Scripts/Controller/Arcade/SandboxBenchmarkController.cs
index 2b233c3cf..68bd0eb53 100644
--- a/Assets/_Scripts/Controller/Arcade/SandboxBenchmarkController.cs
+++ b/Assets/_Scripts/Controller/Arcade/SandboxBenchmarkController.cs
@@ -3,43 +3,47 @@ using UnityEngine;
 namespace CosmicShore.Gameplay
 {
     /// <summary>
-    /// Endless free-flight controller for the benchmark / stress-test scene. Reuses the single-player
-    /// pipeline (player + AI spawn, countdown, player activation) but never ends: with no TurnMonitor
-    /// wired, <see cref="MiniGameControllerBase.EndTurn"/> is never reached, and <see cref="HasEndGame"/>
-    /// is false. The human flies their Squirrel and AI Squirrels fly alongside while the environment
-    /// spawners (flora/fauna/crystals/gyroids) ramp load up - exactly the sustained workload a stress
-    /// test wants.
+    /// Endless free-flight controller for the benchmark / stress-test scene, on the
+    /// networked single-host model every mode now uses: the always-on Relay host loads the
+    /// scene, <c>ServerPlayerVesselInitializerWithAI</c> spawns the human's Squirrel plus
+    /// the Settings-configured AI crowd (<c>GameDataSO.RequestedAIBackfillCount</c>, set by
+    /// <c>BenchmarkSceneLauncher</c>), and the environment spawners ramp load - exactly the
+    /// sustained workload a stress test wants.
     ///
-    /// Drop-in: replace the cloned reference scene's single-player controller with this, or keep that
-    /// controller and simply remove its TurnMonitor (either yields an endless sandbox).
+    /// Extends the blitz controller for its keeper wiring (live score feed) but never ends:
+    /// the scene wires NO turn monitors, so the turn-end path is unreachable, and
+    /// <see cref="SetupNewTurn"/> auto-begins the turn instead of showing the Ready button -
+    /// the scene "just works" on entry with zero clicks.
     /// </summary>
-    public class SandboxBenchmarkController : SinglePlayerMiniGameControllerBase
+    public class SandboxBenchmarkController : MultiplayerWildlifeBlitzController
     {
         [Header("Sandbox / Benchmark")]
-        [SerializeField, Tooltip("Activate players automatically after setup so the scene 'just works' " +
+        [SerializeField, Tooltip("Begin the turn automatically after setup so the scene 'just works' " +
                                  "on entry - no Ready button click needed.")]
         bool autoStart = true;
 
-        [SerializeField, Min(0f), Tooltip("Delay before auto-activating, so spawners and DI settle first.")]
+        [SerializeField, Min(0f), Tooltip("Delay before auto-beginning, so spawners and DI settle first.")]
         float autoStartDelaySeconds = 1f;
 
-        protected override bool HasEndGame => false;          // endless - never EndGame()
         protected override bool ShowEndGameSequence => false;
 
+        /// <summary>
+        /// Server-only (the base round flow only calls this on the server). Skips the base's
+        /// ShowReadyButton_ClientRpc and auto-begins: the countdown RPC activates players and
+        /// starts the turn on every peer.
+        /// </summary>
         protected override void SetupNewTurn()
         {
-            // No TurnMonitor is wired in the benchmark scene, so the turn never ends on its own -
-            // precisely what a free-flight stress test wants. Auto-begin for a frictionless entry.
-            if (autoStart)
+            if (autoStart && IsServer)
                 Invoke(nameof(AutoBegin), autoStartDelaySeconds);
         }
 
         void AutoBegin()
         {
             if (countdownTimer != null)
-                StartCountdownTimer();   // countdown → OnCountdownTimerEnded → SetPlayersActive + StartTurn
+                StartCountdownTimer();   // countdown → OnCountdownTimerEnded → SetPlayersActive + StartTurn (ClientRpc)
             else
-                OnCountdownTimerEnded();  // no countdown wired → activate directly
+                OnCountdownTimerEnded(); // no countdown wired → activate directly
         }
     }
 }
diff --git a/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs b/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs
index e24592e62..3390d613b 100644
--- a/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs
+++ b/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs
@@ -11,8 +11,9 @@ namespace CosmicShore.Core
     /// <c>OnLaunchGame</c> → <c>SceneLoader.LaunchGame</c>), so the always-on Relay host loads it
     /// correctly via Netcode scene management and nothing special-cases it.
     ///
-    /// The scene runs as a single-player sandbox (<see cref="SandboxBenchmarkController"/> +
-    /// <c>MiniGamePlayerSpawnerAdapter</c>) - the host loads it, the Relay simply idles.
+    /// The scene runs on the networked single-host model like every mode
```

</details>

### `aac63c525` — refactor(solo): delete the single-player spawn path - every mode is networked

_Claude, 2026-07-20 23:37:03 +0000_

```text
Removes the now-unreachable SP pipeline (zero scene/prefab/code references
after the blitz + benchmark conversions; verified by guid + class-name grep):
- PlayerSpawner, VesselSpawner, PlayerSpawnerAdapterBase,
  MiniGamePlayerSpawnerAdapter, Player and Vessel Spawner.prefab
- SinglePlayerMiniGameControllerBase, SinglePlayerSlipnStrideController,
  SinglePlayerWildlifeBlitzController, SingleplayerWildlifeBlitzTurnMonitor,
  SinglePlayerWildlifeBlitzScoreTracker
- VesselSelectionPanelController (legacy SP vessel panel; the menu's
  network-aware MenuVesselSelectionPanelController is unrelated and stays)
- Player.InitializeForSinglePlayerMode + its IPlayer declaration (the
  IPlayer.InitializeData class stays - AI templates serialize it)
- MiniGameControllerBase.EndTurn/EndRound/EndGame - the local turn-flow
  limbs only the deleted SP base subscribed/reached; the multiplayer base
  owns the server-authoritative equivalents (ExecuteServerTurnEnd chain)

The offline ScoreTracker stays (Recording Studio tool scenes). The legacy
MiniGame.cs family stays untouched (dormant missions/training, D2/D6).
```

```text
 Assets/_Prefabs/CORE/Player and Vessel Spawner.prefab                 |  79 ----------
 Assets/_Prefabs/CORE/Player and Vessel Spawner.prefab.meta            |   7 -
 Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs           |  41 +----
 .../_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs  |  50 ------
 .../Controller/Arcade/SinglePlayerMiniGameControllerBase.cs.meta      |   3 -
 .../_Scripts/Controller/Arcade/SinglePlayerSlipnStrideController.cs   | 100 ------------
 .../Controller/Arcade/SinglePlayerSlipnStrideController.cs.meta       |   3 -
 .../_Scripts/Controller/Arcade/SinglePlayerWildlifeBlitzController.cs | 103 -------------
 .../Controller/Arcade/SinglePlayerWildlifeBlitzController.cs.meta     |   3 -
 .../Controller/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs        | 121 ---------------
 .../Controller/Arcade/SinglePlayerWildlifeBlitzScoreTracker.cs.meta   |   3 -
 .../Controller/Arcade/SingleplayerWildlifeBlitzTurnMonitor.cs         |  78 ----------
 .../Controller/Arcade/SingleplayerWildlifeBlitzTurnMonitor.cs.meta    |   3 -
 Assets/_Scripts/Controller/Player/IPlayer.cs                          |   1 -
 Assets/_Scripts/Controller/Player/MiniGamePlayerSpawnerAdapter.cs     |  62 --------
 .../_Scripts/Controller/Player/MiniGamePlayerSpawnerAdapter.cs.meta   |   3 -
 Assets/_Scripts/Controller/Player/Player.cs                           |  18 ---
 Assets/_Scripts/Controller/Player/PlayerSpawner.cs                    |  46 ------
 Assets/_Scripts/Controller/Player/PlayerSpawner.cs.meta               |   3 -
 Assets/_Scripts/Controller/Player/PlayerSpawnerAdapterBase.cs         |  45 ------
 Assets/_Scripts/Controller/Player/PlayerSpawnerAdapterBase.cs.meta    |   3 -
 Assets/_Scripts/Controller/Vessel/VesselSpawner.cs                    |  57 -------
 Assets/_Scripts/Controller/Vessel/VesselSpawner.cs.meta               |   3 -
 Assets/_Scripts/UI/VesselSelectionPanelController.cs                  | 259 --------------------------------
 Assets/_Scripts/UI/VesselSelectionPanelController.cs.meta             |  11 --
 25 files changed, 3 insertions(+), 1102 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1094 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs
index 58ecd8e29..1ff8fa643 100644
--- a/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MiniGameControllerBase.cs
@@ -55,57 +55,22 @@ namespace CosmicShore.Gameplay
         {
         }
 
-        protected void EndTurn()
-        {
-            OnTurnEndedCustom();
-            
-            if (ShouldResetPlayersOnTurnEnd)
-                gameData.ResetPlayers();
-            
-            gameData.TurnsTakenThisRound++;
-
-            if (gameData.TurnsTakenThisRound >= numberOfTurnsPerRound)
-                EndRound();
-            else 
-                SetupNewTurn();
-        }
-        
         protected virtual void OnTurnEndedCustom()
         {
         }
-        
+
         protected virtual void SetupNewRound()
         {
             gameData.TurnsTakenThisRound = 0;
             gameData.InvokeMiniGameRoundStarted();
             SetupNewTurn();
         }
-        
-        protected void EndRound()
-        {
-            OnRoundEndedCustom();
-            
-            gameData.RoundsPlayed++;
-            gameData.InvokeMiniGameRoundEnd();
-            
-            if (HasEndGame && gameData.RoundsPlayed >= numberOfRounds)
-                EndGame();
-            else
-                SetupNewRound();
-        }
-        
+
         protected virtual void OnRoundEndedCustom()
         {
         }
 
-        protected virtual void EndGame()
-        {
-            if (!ShowEndGameSequence) return;
-            gameData.SortRoundStats(UseGolfRules);
-            gameData.InvokeWinnerCalculated();
-            gameData.InvokeMiniGameEnd();
-        }
-        
+
         protected virtual void OnResetForReplay()
         {
             SetupNewRound();
diff --git a/Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs
deleted file mode 100644
index 113b0e650..000000000
--- a/Assets/_Scripts/Controller/Arcade/SinglePlayerMiniGameControllerBase.cs
+++ /dev/null
@@ -1,50 +0,0 @@
-using UnityEngine;
-using CosmicShore.Utility;
-
-namespace CosmicShore.Gameplay
-{
-    /// <summary>
-    /// Base controller for single-player game modes.
-    /// Handles event subscriptions and initial setup.
-    /// </summary>
```

</details>

### `cfc9c4951` — refactor(solo): retire GameDataSO.IsMultiplayerMode + SO_Game.IsMultiplayer

_Claude, 2026-07-20 23:42:19 +0000_

```text
Solo modes no longer exist (solo = party-of-one host), so the solo/MP flag
distinguishes nothing. All 11 mapped sites resolved (ScoringSystem
ARCHITECTURE.md §8 carries the per-site table):

- MultiplayerSetup: the sign-in gate died with the WHOLE legacy matchmaking
  path it guarded (ExecuteMultiplayerSetup + query/join/create helpers +
  rate-limit machinery + property keys) - provably dead: AppManager runs
  ResetAllData() before sign-in so the flag was always false there, and the
  eager per-user Relay party session IS the session for every game.
- HostConnectionService presence: every in-game scene now advertises its
  match name (friends see solo games too - intentional change).
- SyncGameConfigToClients_ClientRpc: isMultiplayer arg + client write dropped.
- Analytics is_multiplayer: now ConnectedClientsIds.Count > 1 at report time
  (metric meaning change: more than one connected human; AI excluded).
- SceneLoader log field, TournamentController / BenchmarkSceneLauncher /
  dormant Arcade.cs writes, SyncFromArcadeGame + ResetAllData writes: deleted.
- SO_Game.IsMultiplayer deleted; ArcadeGameConfigureModal + dormant
  ArcadeExploreView pass constant true into the loadout key; IsMultiplayer:
  YAML lines stripped from the 12 game-card assets.
- Loadout.IsMultiplayer / LoadoutCloudData.IsMultiplayer stay as documented
  tombstones (persisted cloud-save schema) - never branch on them.
- Docs: ARCHITECTURE.md §8 rewritten as executed, §4 Scoreboard fork
  corrected to NetworkManager.IsServer; GARRETT.md D3/D16/D19/D21 annotated
  resolved-by-owner 2026-07-20.
```

```text
 Assets/_SO_Assets/Games/ArcadeGameAstroLeague.asset                   |   1 -
 Assets/_SO_Assets/Games/ArcadeGameBlockBandit.asset                   |   1 -
 Assets/_SO_Assets/Games/ArcadeGameHexRace.asset                       |   1 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayer2v2CoOpVsAI.asset        |   1 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerCellularDuel.asset       |   1 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerCrystalCapture.asset     |   1 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerFreestyle.asset          |   1 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerJoust.asset              |   1 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerWildlifeBlitz.asset      |   1 -
 Assets/_SO_Assets/Games/ArcadeGameNucleusRush.asset                   |   1 -
 Assets/_SO_Assets/Games/ArcadeGameTournament.asset                    |   1 -
 Assets/_SO_Assets/Games/ArcadeGameWildlifeBlitz.asset                 |   1 -
 .../_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs   |   4 +-
 Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs  |   1 -
 Assets/_Scripts/Controller/Managers/Arcade.cs                         |   7 --
 Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs            | 207 +-------------------------------
 Assets/_Scripts/Controller/Party/HostConnectionService.cs             |   2 +-
 Assets/_Scripts/ScriptableObjects/SO_Game.cs                          |   1 -
 Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs      |   6 +-
 Assets/_Scripts/System/LoadOut/Loadout.cs                             |   6 +
 Assets/_Scripts/System/SceneLoader.cs                                 |   2 +-
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                 |   4 +-
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs                         |   4 +-
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |   3 -
 Docs/ScoringSystem/ARCHITECTURE.md                                    |  76 ++++--------
 Docs/UnifiedSystems/GARRETT.md                                        |  18 +++
 26 files changed, 65 insertions(+), 288 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 617 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
index 1380a972b..44fb8a34e 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -40,7 +40,6 @@ namespace CosmicShore.Gameplay
                 SyncGameConfigToClients_ClientRpc(
                     gameData.SceneName,
                     (int)gameData.GameMode,
-                    gameData.IsMultiplayerMode,
                     (int)gameData.selectedVesselClass.Value,
                     gameData.SelectedIntensity.Value,
                     gameData.SelectedPlayerCount.Value,
@@ -456,7 +455,7 @@ namespace CosmicShore.Gameplay
         /// </summary>
         [ClientRpc]
         void SyncGameConfigToClients_ClientRpc(
-            string sceneName, int gameMode, bool isMultiplayer,
+            string sceneName, int gameMode,
             int vesselClass, int intensity, int playerCount, int aiBackfillCount,
             int domainCount, bool isTournament, float comebackRate)
         {
@@ -464,7 +463,6 @@ namespace CosmicShore.Gameplay
 
             gameData.SceneName = sceneName;
             gameData.GameMode = (GameModes)gameMode;
-            gameData.IsMultiplayerMode = isMultiplayer;
             gameData.selectedVesselClass.Value = (VesselClassType)vesselClass;
             gameData.SelectedIntensity.Value = intensity;
             gameData.SelectedPlayerCount.Value = playerCount;
diff --git a/Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs b/Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs
index fb2c5cad2..58fce75a6 100644
--- a/Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs
+++ b/Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs
@@ -374,7 +374,6 @@ namespace CosmicShore.Gameplay
         {
             _gameData.SceneName = _tournament.LobbySceneName;
             _gameData.GameMode = CosmicShore.Data.GameModes.Tournament;
-            _gameData.IsMultiplayerMode = true;
             _gameData.IsTournamentMode = true;
             _gameData.InvokeGameLaunch();
         }
diff --git a/Assets/_Scripts/Controller/Managers/Arcade.cs b/Assets/_Scripts/Controller/Managers/Arcade.cs
index 1ff84d9f5..2b1401369 100644
--- a/Assets/_Scripts/Controller/Managers/Arcade.cs
+++ b/Assets/_Scripts/Controller/Managers/Arcade.cs
@@ -63,7 +63,6 @@ namespace CosmicShore.Core
             gameData.IsDailyChallenge = false;
             gameData.IsTraining = false;
             gameData.IsMission = true;
-            gameData.IsMultiplayerMode = false;
             gameData.GameMode = gameMode;
             gameData.SelectedPlayerCount.Value = 1;
             gameData.SelectedIntensity.Value = intensity;
@@ -100,11 +99,6 @@ namespace CosmicShore.Core
             gameData.IsTraining = false;
             gameData.IsMission = false;
             gameData.GameMode = gameMode;
-            
-            // For multiplayer-capable games with only 1 human player, run locally with AI
-            // instead of doing online matchmaking. Use gameData.SelectedPlayerCount (set by
-            // the config modal) rather than the legacy numberOfPlayers parameter.
-            gameData.IsMultiplayerMode = isMultiplayer && gameData.SelectedPlayerCount.Value > 1;
             gameData.SceneName = ArcadeGameLookup[gameMode].SceneName;
             gameData.InvokeGameLaunch();
 
@@ -159,7 +153,6 @@ namespace CosmicShore.Core
             gameData.IsDailyChallenge = isDailyChallenge;
             gameData.IsTraining = !isDailyChallenge;
             gameData.IsMission = false;
-            gameData.IsMultiplayerMode = false;
             gameData.SceneName = TrainingGameLookup[gameMode].Game.SceneName;
             gameData.InvokeGameLaunch();
             
diff --git a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
index 0abeb18f6..cf2c24d43 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MultiplayerSetup.cs
@@ -1,10 +1,7 @@
 using System;
-using System.Linq;
```

</details>

### `8f66e0538` — refactor(solo): delete dead solo arcade content

_Claude, 2026-07-20 23:46:19 +0000_

```text
- 23 scene-less solo SO_ArcadeGame cards deleted (incl. the mode-collision
  trio Sidewinder/Multipass/_CosmicDrift) - their scenes have been gone for
  months and the live arcade grid (OrganicRematchGames, DI-injected) never
  carried them.
- The 4 fully-orphaned game lists deleted (ArcadeGames, AllGames,
  PreviousAllGames, LeaderboardGames - none wired into any live scene/prefab;
  DI registers OrganicRematchGames, which is untouched).
- Mode-32 co-op blitz stack deleted (MinigameWildlifeBlitzMultuplayerCoOp
  scene + ArcadeGameMultiplayerWildlifeBlitz card; its controller class was
  already gone and it had no build entry) - WildlifeBlitz(26) IS the
  networked co-op blitz now.
- Deleted-card guids pruned from the live LaunchPartyAllGames (survives with
  blitz + MP freestyle + MP duel) and the 8 SO_Class vessel game lists; every
  remaining guid verified to resolve.
- GameModes: retired IDs annotated do-not-reuse; members kept so serialized
  ints in the dormant training/mission assets stay stable (D2/D6 untouched:
  SO_TrainingGame assets, TrainingGames/MissionGames lists, SO_Mission_Protect,
  the never-placed Arcade singleton). Known dangling refs, both inert: 5
  dormant SO_TrainingGame_* assets point at deleted cards (subsystem is
  dormant; revival re-authors them) and the DELETE LATER migration
  Screens.prefab pointed at AllGames (Y0.1 deletes that folder).
- SplashScreen.unity moved out (guid preserved); Singleplayer Scenes folder
  removed - every scene now lives under Multiplayer Scenes, Tools, or root.
```

```text
 Assets/_SO_Assets/Games/ArcadeGameRampage.asset                       |   35 -
 Assets/_SO_Assets/Games/ArcadeGameRampage.asset.meta                  |    8 -
 Assets/_SO_Assets/Games/ArcadeGameRiskyDriftness.asset                |   33 -
 Assets/_SO_Assets/Games/ArcadeGameRiskyDriftness.asset.meta           |    8 -
 Assets/_SO_Assets/Games/ArcadeGameSidewinder.asset                    |   33 -
 Assets/_SO_Assets/Games/ArcadeGameSidewinder.asset.meta               |    8 -
 Assets/_SO_Assets/Games/ArcadeGameSlipNStride.asset                   |   33 -
 Assets/_SO_Assets/Games/ArcadeGameSlipNStride.asset.meta              |    8 -
 Assets/_SO_Assets/Games/ArcadeGameSoar.asset                          |   33 -
 Assets/_SO_Assets/Games/ArcadeGameSoar.asset.meta                     |    8 -
 Assets/_SO_Assets/Games/GameLists/AllGames.asset                      |   27 -
 Assets/_SO_Assets/Games/GameLists/AllGames.asset.meta                 |    8 -
 Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset                   |   24 -
 Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset.meta              |    8 -
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset           |    9 -
 Assets/_SO_Assets/Games/GameLists/LeaderboardGames.asset              |   19 -
 Assets/_SO_Assets/Games/GameLists/LeaderboardGames.asset.meta         |    8 -
 Assets/_SO_Assets/Games/GameLists/PreviousAllGames.asset              |   32 -
 Assets/_SO_Assets/Games/GameLists/PreviousAllGames.asset.meta         |    8 -
 Assets/_SO_Assets/Games/_ArcadeGameCosmicDrift.asset                  |   32 -
 Assets/_SO_Assets/Games/_ArcadeGameCosmicDrift.asset.meta             |    8 -
 Assets/_SO_Assets/Games/_ArcadeGameShootingGallery.asset              |   32 -
 Assets/_SO_Assets/Games/_ArcadeGameShootingGallery.asset.meta         |    8 -
 .../Multiplayer Scenes/MinigameWildlifeBlitzMultuplayerCoOp.unity     | 1834 -------------------------------
 .../MinigameWildlifeBlitzMultuplayerCoOp.unity.meta                   |    7 -
 Assets/_Scenes/Singleplayer Scenes.meta                               |    8 -
 Assets/_Scenes/{Singleplayer Scenes => }/SplashScreen.unity           |    0
 Assets/_Scenes/{Singleplayer Scenes => }/SplashScreen.unity.meta      |    0
 Assets/_Scripts/Data/Enums/GameModes.cs                               |   12 +
 71 files changed, 12 insertions(+), 2998 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/GameModes.cs b/Assets/_Scripts/Data/Enums/GameModes.cs
index e3249a34e..b29e8d392 100644
--- a/Assets/_Scripts/Data/Enums/GameModes.cs
+++ b/Assets/_Scripts/Data/Enums/GameModes.cs
@@ -5,6 +5,14 @@ namespace CosmicShore.Data
     public enum GameModes
     {
         Random = 0,
+        // ── RETIRED SOLO IDS — DO NOT REUSE ─────────────────────────────────
+        // Solo modes were retired 2026-07-20 (solo = a multiplayer game whose
+        // party is one host). IDs 1-6, 8-25, and 27 kept their enum members so
+        // the serialized ints inside the kept-but-dormant training/mission
+        // assets (SO_TrainingGame_*, SO_Mission_Protect) stay stable, but their
+        // SO_ArcadeGame cards and scenes are deleted. CellularDuel(8) play lives
+        // on as MultiplayerCellularDuel(29); WildlifeBlitz(26) is LIVE (the
+        // networked single-host co-op blitz). Do not reuse any retired ID.
         Elimination = 1,
         Rampage = 2,
         Darts = 3,
@@ -37,6 +45,10 @@ namespace CosmicShore.Data
         MultiplayerFreestyle = 28,
         MultiplayerCellularDuel = 29,
         Multiplayer2v2CoOpVsAI = 30,
+        // 32 retired 2026-07-20: the separate co-op blitz stack (scene + card)
+        // was deleted - WildlifeBlitz(26) IS the networked co-op blitz now.
+        // Member kept for serialized-int stability; do not reuse. (31 stays
+        // reserved - never assigned.)
         MultiplayerWildlifeBlitzGame = 32,
         HexRace = 33,
         MultiplayerJoust = 34,
```

</details>

### `7070e8fe9` — docs(solo): sweep the docs for the retired solo path

_Claude, 2026-07-20 23:50:42 +0000_

```text
- CLAUDE.md: scene tables (no Singleplayer Scenes; blitz + benchmark under
  Multiplayer Scenes), controller hierarchy (SP branch removed;
  MultiplayerWildlifeBlitzController + SandboxBenchmarkController added),
  spawning section (SP path removed), key-files/key-systems rows, lava-lamp
  vessel-selection notes, retired-modes paragraph.
- Docs/SCENES.md: same sweep + a new co-op Wildlife Blitz mode section
  replacing the three retired SP sections; mode table collapses retired IDs.
- Docs/UnifiedSystems/YASH.md: Y2 recorded as EXECUTED (C1-C7 summary).
- Docs/ScoringSystem/REFACTOR.md: Q1 resolved by owner, R1 executed;
  stale still-open note corrected.
```

```text
 CLAUDE.md                      |  80 ++++++++++++++---------------------------
 Docs/SCENES.md                 | 138 +++++++++++++++++++++--------------------------------------------------
 Docs/ScoringSystem/REFACTOR.md |  26 ++++++++------
 Docs/UnifiedSystems/YASH.md    |  40 +++++++++++++++------
 4 files changed, 112 insertions(+), 172 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 549 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 652b3a3e7..afb90391a 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -149,7 +149,7 @@ Assets/
 │   │   ├── Animation/         # Per-vessel animation controllers
 │   │   ├── Camera/            # CustomCameraController, CameraSettingsSO, ICameraController
 │   │   ├── Multiplayer/       # Netcode: ServerPlayerVesselInitializer (+ WithAI, Menu variants), ClientPlayerVesselInitializer, MultiplayerSetup, MenuCrystalClickHandler, DomainAssigner, NetworkStatsManager
-│   │   ├── Player/            # Player (NetworkBehaviour), PlayerSpawner, IPlayer, PlayerSpawnerAdapterBase, MiniGamePlayerSpawnerAdapter
+│   │   ├── Player/            # Player (NetworkBehaviour), IPlayer, RoundStats
 │   │   ├── Prisms/            # PrismFactory
 │   │   ├── Assemblers/        # Gyroid/wall assembly systems
 │   │   ├── Party/             # HostConnectionService, PartyInviteController, FriendsInitializer
@@ -238,17 +238,13 @@ See `Docs/SCENES.md` for the full scene and game mode reference. Summary below.
 | **Authentication** | 1 | Auth UI, cached session check, NetworkManager host start |
 | **Menu_Main** | 2 | Main menu with networked autopilot vessel, screen navigation |
 
-#### Single-Player Game Scenes
-
-| Scene | Game Mode | Controller |
-|---|---|---|
-| `MinigameCellularDuel` | `CellularDuel (8)` | `SinglePlayerCellularDuelController` |
-| `MinigameWildlifeBlitz` | `WildlifeBlitz (26)` | `SinglePlayerWildlifeBlitzController` |
-
-All in `Assets/_Scenes/Singleplayer Scenes/`.
-
 #### Multiplayer Game Scenes
 
+There are no single-player scenes: **solo play is a multiplayer game whose party is one
+host** (eager Relay session + AI backfill via `ServerPlayerVesselInitializerWithAI`).
+The former `Singleplayer Scenes/` folder is gone; `SplashScreen.unity` lives at
+`Assets/_Scenes/`.
+
 | Scene | Game Mode | Controller |
 |---|---|---|
 | `MinigameHexRace` | `HexRace (33)` | `HexRaceController` |
@@ -256,9 +252,10 @@ All in `Assets/_Scenes/Singleplayer Scenes/`.
 | `MinigameCrystalCaptureMultiplayer_Gameplay` | `MultiplayerCrystalCapture (35)` | `MultiplayerCrystalCaptureController` |
 | `MinigameDuelForCellMultiplayer_Gameplay` | `MultiplayerCellularDuel (29)` | `MultiplayerCellularDuelController` |
 | `MinigameJoust_Gameplay` | `MultiplayerJoust (34)` | `MultiplayerJoustController` |
-| `MinigameWildlifeBlitzMultuplayerCoOp` | `MultiplayerWildlifeBlitzGame (32)` | `MultiplayerWildlifeBlitzMiniGame` |
 | `MinigameAstroLeague` | `AstroLeague (36)` | `AstroLeagueController` |
 | `MinigameNucleusRush` | `NucleusRush (38)` | `NucleusRushController` |
+| `MinigameWildlifeBlitz` | `WildlifeBlitz (26)` | `MultiplayerWildlifeBlitzController` |
+| `BenchmarkStressTest` | (Settings → Run Benchmark; `WildlifeBlitz` mode) | `SandboxBenchmarkController` |
 | `ArcadeGameMultiplayer2v2CoOpVsAI` | `Multiplayer2v2CoOpVsAI (30)` | Domain games variant |
 
 All in `Assets/_Scenes/Multiplayer Scenes/`.
@@ -273,7 +270,7 @@ All in `Assets/_Scenes/Multiplayer Scenes/`.
 
 38 game modes with explicit numeric IDs (highest is `NucleusRush(38)`; IDs 7 and 31 are skipped). Single-player: `Elimination(1)` through `ProtectMission(27)`. Multiplayer: `MultiplayerFreestyle(28)`, `MultiplayerCellularDuel(29)`, `Multiplayer2v2CoOpVsAI(30)`, `MultiplayerWildlifeBlitzGame(32)`, `HexRace(33)`, `MultiplayerJoust(34)`, `MultiplayerCrystalCapture(35)`, `AstroLeague(37)`, `NucleusRush(38)`. Meta-mode: `Tournament(36)` — the session-level meta that chains HexRace → Joust → Crystal Capture back-to-back via sequential `Single` loads (see `Docs/TournamentSystem/ARCHITECTURE.md`). `AstroLeague(37)` is hypersea soccer (a standalone domain minigame, see `_Scripts/Controller/Arcade/ASTROLEAGUE.md`). `NucleusRush(38)` (display name "Brood Rush") is the nucleus-control fauna-wave race (see `_Scripts/Controller/Arcade/NUCLEUSRUSH.md`). Meta sentinel: `Random(0)`. Note: IDs 7 and 31 are skipped — 7 was the retired standalone arcade Freestyle game (freestyle now lives in Menu_Main as the lava lamp; see "Lava-Lamp Mode"), 31 was never assigned. Do not reuse either ID.
 
-Many single-player modes (1-6, 9-25, 27) reference scenes that no longer exist on disk — their `SO_ArcadeGame` assets still exist and appear in the Arcade UI, but launching them would fail.
+Solo modes were retired 2026-07-20: their scene-less `SO_ArcadeGame` cards are deleted and the retired enum IDs (1-6, 8-25, 27, 32) are kept only for serialized-int stability (annotated do-not-reuse in `GameModes.cs`). `WildlifeBlitz (26)` lives on as the networked single-host co-op blitz; `CellularDuel` play lives on as `MultiplayerCellularDuel (29)`.
 
 #### Controller Hierarchy
 
@@ -281,17 +278,10 @@ Many single-player modes (1-6, 9-25, 27) reference scenes that no longer exist o
 MiniGameControllerBase (abstract, NetworkBehaviour)
 │   Template Method: rounds → turns → countdown → gameplay → end
 │
-├── SinglePlayerMiniGameControllerBase (abstract)
-│   ├── SinglePlayerCellularDuelController — vessel swap on turn end
-│   ├── SinglePlayerSlipnStrideController  — procedural course with intensity scaling
-│   ├── SinglePlayerWildlifeBlitzController — blitz scoring
-│   └── WildlifeBlitzMiniGame             — minimal variant
-│
 └── MultiplayerMiniGameControllerBase (abstract, NetworkBehaviour)
     │   Server-authoritative turn/round/game flow via ClientRpc
     │
     ├── MultiplayerFreestyleController     — sandbox, per-player activation
-    ├── MultiplayerWildlifeBlitzMiniGame    — co-op, own ready-sync
     │
     └── MultiplayerDomainGamesController
         ├── HexRaceController              — crystal race, deterministic track, golf scoring
@@ -299,9 +289,15 @@ MiniGameControllerBase (abstract, NetworkBehaviour)
         ├── MultiplayerCellularDuelController — vessel ownership swap between rounds
         ├── MultiplayerCrystalCaptureController — minimal (1 round, 1 turn)
         ├── AstroLeagueController             — hypersea soccer, server-simulated ball, golden goal
-        └── NucleusRushController             — nucleus-control fauna-wave race, brood scoring
```

</details>

### `a67556c93` — refactor(freestyle): retire the standalone MultiplayerFreestyle game - freestyle IS the lava lamp

_Claude, 2026-07-21 00:21:10 +0000_

```text
There is no separate freestyle scene: freestyle is integrated into Menu_Main
as the lava lamp (party members fly it together there). The standalone
mode-28 sandbox duplicated it, so the stack is deleted (all verified
sole-use by guid + class grep before deletion):

- MinigameFreestyleMultiplayer_Gameplay.unity (+ EditorBuildSettings entry)
- MultiplayerFreestyleController (referenced only by that scene)
- FreestyleScoringRuleSO + FreestyleScoringRule.asset (only consumer was
  the scene's controller block; ScoringMetric.VolumeCreated stays - shared)
- ArcadeGameMultiplayerFreestyle card, pruned from LaunchPartyAllGames
  (the live list survives with MP duel + blitz)

Also removed the vestigial SceneNameListSO.MultiplayerScene member - its
default pointed at the deleted scene and it had ZERO runtime consumers
(gameplay scenes launch by each card's SceneName); the orphaned serialized
line is stripped from SceneNameList.asset and the two test assertions
removed. GameModes(28) + CallToActionTargetType(428) members kept for
serialized-int stability, annotated do-not-reuse.

The lava lamp is untouched and independent (verified at guid level):
Menu_Main references none of the deleted scripts/assets - menu freestyle
runs on MenuFreestyleEventsContainerSO + MainMenuState.Freestyle, and
gameData.ScoringRule stays null in Menu_Main with all readers null-guarded.

Docs: CLAUDE.md + SCENES.md scene tables/hierarchy/naming notes (both
standalone freestyles now retired; lava lamp is the only freestyle),
YASH.md Y1.1 note; SCENES.md also drops a stale mode-32 co-op section.
```

```text
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerFreestyle.asset          |   39 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerFreestyle.asset.meta     |    8 -
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset           |    1 -
 Assets/_SO_Assets/SceneNameList.asset                                 |    1 -
 Assets/_SO_Assets/Scoring Rules/FreestyleScoringRule.asset            |   16 -
 Assets/_SO_Assets/Scoring Rules/FreestyleScoringRule.asset.meta       |    8 -
 .../Multiplayer Scenes/MinigameFreestyleMultiplayer_Gameplay.unity    | 2173 -------------------------------
 .../MinigameFreestyleMultiplayer_Gameplay.unity.meta                  |    7 -
 Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs   |  104 --
 .../_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs.meta |    3 -
 Assets/_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs   |   34 -
 .../_Scripts/Controller/Arcade/Scoring/FreestyleScoringRuleSO.cs.meta |   11 -
 Assets/_Scripts/Data/Enums/CallToActionTargetType.cs                  |    2 +-
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    4 +
 Assets/_Scripts/System/Bootstrap/Tests/SceneFlowIntegrationTests.cs   |   42 +-
 Assets/_Scripts/Utility/DataContainers/SceneNameListSO.cs             |    8 +-
 CLAUDE.md                                                             |    7 +-
 Docs/SCENES.md                                                        |   34 +-
 Docs/UnifiedSystems/YASH.md                                           |    3 +-
 ProjectSettings/EditorBuildSettings.asset                             |    3 -
 20 files changed, 20 insertions(+), 2488 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 400 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs
deleted file mode 100644
index 850d6dc92..000000000
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerFreestyleController.cs
+++ /dev/null
@@ -1,104 +0,0 @@
-using System.Collections.Generic;
-using Cysharp.Threading.Tasks;
-using Unity.Collections;
-using Unity.Netcode;
-using UnityEngine;
-using CosmicShore.Data;
-using CosmicShore.Utility;
-
-namespace CosmicShore.Gameplay
-{
-    public class MultiplayerFreestyleController : MultiplayerMiniGameControllerBase
-    {
-        [Header("Scoring")]
-        [Tooltip("Drag FreestyleScoringRule.asset - config for the live centerline feed (volume created). The sandbox has no objective; the rule never ends the game.")]
-        [SerializeField] ScoringRuleSO rule;
-
-        // ── Live centerline feed (replaces the legacy NetworkScoreTracker's VolumeCreatedScoring) ──
-        // Server-only: RoundStats.Score suppresses the local OnScoreChanged while spawned, so only
-        // a server write (→ n_Score replication) reaches the MiniGameHUD centerline on every peer.
-        // B15 (Docs/ScoringSystem/BUGS.md): detach from THIS record only - never by iterating
-        // gameData.RoundStatsList (SceneLoader clears it before old-scene destruction) - and never
-        // gate cleanup on turn-end alone (freestyle's turn never ends; exits are scene changes).
-        readonly List<IRoundStats> _scoreFeedStats = new();
-
-        public override void OnNetworkSpawn()
-        {
-            base.OnNetworkSpawn();
-            gameData.ScoringRule = rule;
-            gameData.OnClientReady.OnRaised += OnClientReady;
-
-            if (IsServer)
-                gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
-        }
-
-        public override void OnNetworkDespawn()
-        {
-            base.OnNetworkDespawn();
-            gameData.OnClientReady.OnRaised -= OnClientReady;
-
-            if (IsServer)
-                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
-            StopScoreFeed();
-        }
-
-        public override void OnDestroy()
-        {
-            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
-            base.OnDestroy();
-        }
-
-        void OnClientReady() => gameData.SetNonOwnerPlayersActiveInNewClient();
-
-        /// <summary>
-        /// Freestyle raises StartTurn once per player activation, so this runs repeatedly -
-        /// the Contains guard makes re-subscription a no-op while catching stats that joined
-        /// the roster since the last activation.
-        /// </summary>
-        void RefreshScoreFeed()
-        {
-            foreach (var stats in gameData.RoundStatsList)
-            {
-                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
-                stats.OnVolumeCreatedChanged += FeedScore;
-                _scoreFeedStats.Add(stats);
-            }
-        }
-
-        void StopScoreFeed()
-        {
-            foreach (var stats in _scoreFeedStats)
-            {
-                if (stats == null) continue;
-                stats.OnVolumeCreatedChanged -= FeedScore;
-            }
```

</details>

### `bec6338c7` — fix(vessel): stop AI pilot control leaking onto human players

_Claude, 2026-07-22 20:10:40 +0000_

```text
Solo-with-AI, the human could not drift (or use any button action): a
vessel that had ever run the AIPilot kept AutoPilotEnabled=true in human
hands, so R_VesselActionHandler.OnButtonPressed/OnButtonReleased returned
early while AIPilot.Update kept steering and toggling drift on its own.
Party games were unaffected only because no AI vessels exist there.

- VesselController.ToggleAIPilot is now the single choke point ending ALL
  AI-driven control: it also stops the AICinematicBehavior flourish (which
  previously had no callers of StopCinematicBehavior at all).
- Player.StartPlayer human branch now calls ToggleAIPilot(false), symmetric
  with the AI branch - a human turn never starts with autopilot on (covers
  the EndGameSequencer flourish leak on in-place replays).
- VesselController.ChangePlayer stops the pilot when a human receives a
  vessel (covers Cellular Duel's between-round swap vs an AI opponent).
- AIPilot: StartAIPilot is idempotent (menu double-activation stacked
  duplicate ability/seek coroutines); StopAIPilot uses StopAllCoroutines
  (StopCoroutine(UseAbilityCoroutine(x)) stopped a fresh enumerator, not
  the running coroutine); the component's enabled flag mirrors the pilot
  state so inactive vessels take no per-frame Update dispatch.
- AICinematicBehavior only enables its Update during an active flourish.
- DriftActionSO plays the DriftStart/DriftEnd one-shots behind the existing
  IsLocalUser gate: AI Dolphins rapidly toggling drift no longer spam
  full-volume 2D drift cues over the local pilot's own feedback.
```

```text
 Assets/_Scripts/Controller/AI/AIPilot.cs                                 | 30 +++++++++++++++++++++++++-----
 Assets/_Scripts/Controller/Player/Player.cs                              |  7 +++++++
 .../Controller/Vessel/R_VesselActions/Data Containers/DriftActionSO.cs   | 23 ++++++++++++++---------
 Assets/_Scripts/Controller/Vessel/VesselController.cs                    | 11 +++++++++++
 Assets/_Scripts/Utility/DataContainers/AICinematicBehavior.cs            | 14 ++++++++++++++
 5 files changed, 71 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 141 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index 318ad5d24..8548185d0 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -283,11 +283,28 @@ namespace CosmicShore.Gameplay
 
             // Pick up any crystals that were spawned before this AI was initialized
             UpdateCellContent();
+
+            // Update() only runs while the pilot is active (StartAIPilot/StopAIPilot
+            // toggle it) - human-piloted vessels pay no per-frame AI cost.
+            enabled = AutoPilotEnabled;
         }
 
         public void StartAIPilot()
         {
+            // Idempotent: menu activation calls this twice for the same vessel
+            // (MenuServerPlayerVesselInitializer.ActivateAutopilot + the client-side
+            // ActivateLocalPlayerAutopilot) and a second pass would stack duplicate
+            // ability/seek coroutines.
+            if (AutoPilotEnabled)
+                return;
+
             AutoPilotEnabled = true;
+            enabled = true;
+
+            // The OnCellItemsUpdated subscription was inactive while the component was
+            // disabled - re-seed the target so the pilot doesn't fly a stale heading
+            // until the next raise.
+            UpdateCellContent();
 
             foreach (var ability in abilities)
             {
@@ -303,11 +320,14 @@ namespace CosmicShore.Gameplay
         public void StopAIPilot()
         {
             AutoPilotEnabled = false;
-            
-            foreach (var ability in abilities)
-            {
-                StopCoroutine(UseAbilityCoroutine(ability));
-            }
+
+            // StopCoroutine(UseAbilityCoroutine(ability)) stopped a freshly created
+            // enumerator, never the running coroutine, so abilities could fire one more
+            // Start/Stop cycle after handing the vessel to a human. Kill them all.
+            StopAllCoroutines();
+
+            // No Update() dispatch while a human pilots this vessel.
+            enabled = false;
         }
 
         void Update()
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 3259ffd19..dd8a06331 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -400,7 +400,14 @@ namespace CosmicShore.Gameplay
                 ToggleInputPause(true);
             }
             else
+            {
+                // A human-controlled turn must never start with autopilot on. Vessels can
+                // arrive here still AI-driven (Cellular Duel's between-round vessel swap,
+                // the EndGameSequencer flourish on in-place replays); a live AIPilot blocks
+                // every button action in R_VesselActionHandler and fights the pilot's input.
+                ToggleAIPilot(false);
                 ToggleInputPause(false);
+            }
         }
         
 
diff --git a/Assets/_Scripts/Controller/Vessel/VesselController.cs b/Assets/_Scripts/Controller/Vessel/VesselController.cs
index 3943712d9..5afa891d2 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselController.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselController.cs
@@ -207,6 +207,11 @@ namespace CosmicShore.Gameplay
 
         public void ToggleAIPilot(bool toggle)
         {
```

</details>

### `6e8999939` — perf(input): disable InputController for non-local players at pair-init

_Claude, 2026-07-22 20:10:59 +0000_

```text
Every AI and remote player's InputController ran Update() per frame on
every machine, polling the local physical devices and raising duplicate
global OnButtonPressed/OnButtonReleased events until input was paused -
against the class's own contract ("Don't initialize this for any AI /
Multiplayer Non Owner Players"). Gate the component's enabled flag on
IsLocalUser in InitializeForMultiplayerMode, the earliest point locality
is reliable (NetIsAI is written by the AI spawner after Spawn(), so
OnNetworkSpawn cannot decide). Plain members (SetPause/SetIdle/
InputStatus) keep working on the disabled component, and player locality
never changes at runtime (duel swaps exchange vessels, not players).
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 7 +++++++
 1 file changed, 7 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index dd8a06331..1eb08d38c 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -146,6 +146,13 @@ namespace CosmicShore.Gameplay
             AvatarId = NetAvatarId.Value;
             Vessel = vessel;
 
+            // Only the local human's InputController polls devices (its class contract).
+            // Locality isn't knowable at OnNetworkSpawn (the AI spawner writes NetIsAI
+            // after Spawn()), so gate here: a disabled controller stops the per-frame
+            // device polling and duplicate global OnButtonPressed raises from AI/remote
+            // players' copies. SetPause/SetIdle/InputStatus remain usable while disabled.
+            InputController.enabled = IsLocalUser;
+
             // RoundStats.Domain is a LOCAL mirror of the player's domain on EVERY peer -
             // Player.NetDomain is the single networked source (RoundStats.n_Domain is retired). Set
             // it on clients too, so a client's own RoundStats.Domain is correct immediately instead
```

</details>

### `e9915b7ef` — chore(diag): TEMPORARY [DRIFT-DIAG] logging across the Squirrel drift chain

_Claude, 2026-07-22 21:12:15 +0000_

```text
Instruments the four links of the drift pipeline to pinpoint where it
breaks in Scurry (Crystal Capture) solo-with-AI while the lava-lamp works:
button event received (with autopilot/mute/ownership state), action
resolution (device + override map), drift action start/stop (with trigger
analog + locality), and transformer BeginDrift (with active/stationary/
restricted gates). Stop() logs a stack trace to identify unexpected
stoppers. All lines are tagged [DRIFT-DIAG] and this commit is to be
REVERTED once the investigation closes.
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs                         | 16 +++++++++++++++-
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/DriftActionSO.cs |  8 ++++++++
 Assets/_Scripts/Controller/Vessel/VesselTransformer.cs                             |  5 +++++
 3 files changed, 28 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs
index 3455d6034..a4f7a5483 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs
@@ -122,6 +122,12 @@ namespace CosmicShore.Gameplay
 
         public void PerformShipControllerActions(InputEvents controlType)
         {
+            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+            CosmicShore.Utility.CSDebug.Log($"[DRIFT-DIAG] Perform({controlType}) vessel={vesselStatus?.Name}/{vesselStatus?.PlayerName} " +
+                        $"muted={IsInputMuted(controlType)} hasAction={HasAction(controlType)} " +
+                        $"device={vesselStatus?.InputStatus?.ActiveInputDevice} " +
+                        $"overrides={(GetActiveOverrides() == null ? "none" : "device")} baseMapCount={_shipControlActions.Count}");
+
             if (IsInputMuted(controlType)) return;
             if (!HasAction(controlType)) return;
 
@@ -134,6 +140,10 @@ namespace CosmicShore.Gameplay
 
         public void StopShipControllerActions(InputEvents controlType)
         {
+            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+            CosmicShore.Utility.CSDebug.Log($"[DRIFT-DIAG] Stop({controlType}) vessel={vesselStatus?.Name}/{vesselStatus?.PlayerName} " +
+                        $"hasAction={HasAction(controlType)}\n{new System.Diagnostics.StackTrace(1, false)}");
+
             if (!HasAction(controlType)) return;
 
             float duration = 0f;
@@ -192,7 +202,11 @@ namespace CosmicShore.Gameplay
 
         void OnButtonPressed(InputEvents ie)
         {
-            if (vesselStatus.AutoPilotEnabled) 
+            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+            CosmicShore.Utility.CSDebug.Log($"[DRIFT-DIAG] OnButtonPressed({ie}) vessel={vesselStatus?.Name}/{vesselStatus?.PlayerName} " +
+                        $"auto={vesselStatus?.AutoPilotEnabled} muted={IsInputMuted(ie)} spawned={IsSpawned} owner={IsOwner}");
+
+            if (vesselStatus.AutoPilotEnabled)
                 return;
             if (IsInputMuted(ie)) return;
             if (IsSpawned && IsOwner)
diff --git a/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs b/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs
index 7df707f53..9d5ae454d 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs
@@ -262,6 +262,11 @@ public class VesselTransformer : MonoBehaviour
         /// </summary>
         public void BeginDrift(float rotMult, float dampTarget, bool isSharp)
         {
+            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+            CSDebug.Log($"[DRIFT-DIAG] BeginDrift sharp={isSharp} mult={rotMult} damp={dampTarget} " +
+                        $"vessel={VesselStatus?.Name} active={isActive} stationary={VesselStatus?.IsStationary} " +
+                        $"restricted={VesselStatus?.IsTranslationRestricted} triggerSum={GetTriggerSum():F2}");
+
             _driftEaseOutPending = false;
 
             if (!_hasDriftBase)
```

</details>

### `5a4aed573` — fix(comeback): defer game-event subscription until GameDataSO is assigned

_Claude, 2026-07-22 21:53:52 +0000_

```text
EnsureExists ran AddComponent<ElementalComebackSystem>(), whose OnEnable
executes INLINE - before the following gameData assignment - so every
auto-created comeback system logged "GameDataSO is not assigned!" and
returned without subscribing to the turn events, leaving comeback dead
for the whole game. Scene-authored instances hit the same hazard: per
the project DI rules, [Inject] fields land after Awake/OnEnable.

Apply the documented deferred-subscription pattern: attempt in OnEnable,
complete explicitly after EnsureExists assigns the reference, retry in
Start (which also keeps the fail-loud error, now raised only after every
init path had its chance). OnDisable unsubscribes only what was actually
subscribed, and an intentionally disabled authored instance is not
force-subscribed while inactive.
```

```text
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs | 35 +++++++++++++++++++++++++++++------
 1 file changed, 29 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index 5439cd6f0..e846ea0da 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -54,11 +54,19 @@ namespace CosmicShore.Gameplay
             if (existing)
             {
                 existing.gameData ??= gameData;
+                // Respect an intentionally disabled authored instance - OnEnable
+                // completes the subscription if it is activated later.
+                if (existing.isActiveAndEnabled)
+                    existing.TrySubscribeToGameEvents();
                 return existing;
             }
 
+            // AddComponent runs the new component's OnEnable INLINE, before the field
+            // assignment below - gameData is still null there, so subscription is
+            // deferred and completed explicitly once the reference is set.
             var system = host.AddComponent<ElementalComebackSystem>();
             system.gameData = gameData;
+            system.TrySubscribeToGameEvents();
             switch (gameData ? gameData.GameMode : GameModes.Random)
             {
                 case GameModes.HexRace: // Score is elapsed time - crystals are the honest stat
@@ -104,18 +112,32 @@ namespace CosmicShore.Gameplay
         // Index matches AllElements order: Mass=0, Charge=1, Space=2, Time=3.
         readonly float[] _lastComebackAudioTime = { -999f, -999f, -999f, -999f };
 
-        void OnEnable()
+        bool _subscribed;
+
+        // Deferred-subscription pattern (CLAUDE.md DI rules): gameData is not available in
+        // OnEnable - [Inject] lands after Awake/OnEnable for scene-authored instances, and
+        // EnsureExists assigns it only after AddComponent (whose OnEnable runs inline).
+        // Attempt on every entry point; the guard makes repeats no-ops.
+        void OnEnable() => TrySubscribeToGameEvents();
+
+        void Start()
         {
-            if (gameData == null)
-            {
+            TrySubscribeToGameEvents();
+
+            // Fail loud, but only once every init path has had its chance to assign gameData.
+            if (!_subscribed)
                 CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned!");
-                return;
-            }
+        }
+
+        void TrySubscribeToGameEvents()
+        {
+            if (_subscribed || gameData == null) return;
             // Profile is optional now (initial-levels only) - the system runs without one.
 
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
             gameData.OnMiniGameEnd.OnRaised += OnGameEnded;
+            _subscribed = true;
 
             if (debugLogging)
                 CSDebug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
@@ -123,10 +145,11 @@ namespace CosmicShore.Gameplay
 
         void OnDisable()
         {
-            if (gameData == null) return;
+            if (!_subscribed) return;
             gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
             gameData.OnMiniGameEnd.OnRaised -= OnGameEnded;
+            _subscribed = false;
         }
 
         void OnTurnStarted()
```

</details>

### `b47a5ac42` — chore(diag): TEMPORARY [DRIFT-DIAG] raw device logging in input strategies

_Claude, 2026-07-22 23:08:05 +0000_

```text
Scurry run showed the full action pipeline healthy but Gamepad.current's
left trigger never crossing the deadzone (no LeftStickAction raised on
press or release while stick-pose events still fired). Extend the
diagnostics one level down: log raw trigger reads (throttled, only while
non-zero) with the identity of the pad Unity is reading, and log which
strategy activates. To be REVERTED with the other [DRIFT-DIAG] commit.
```

```text
 Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs  | 17 +++++++++++++++++
 Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs |  3 +++
 2 files changed, 20 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
index 71fa8b29b..d51e7ee6f 100644
--- a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
@@ -25,10 +25,17 @@ namespace CosmicShore.Gameplay
             ResetInput();
         }
 
+        // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+        private float _diagNextRawLogTime;
+
         public override void OnStrategyActivated()
         {
             base.OnStrategyActivated();
             inputStatus.ActiveInputDevice = InputDeviceType.Gamepad;
+
+            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+            CSDebug.Log($"[DRIFT-DIAG] GamepadStrategy ACTIVATED pad='{Gamepad.current?.displayName}' " +
+                        $"type={Gamepad.current?.GetType().Name} allGamepads={Gamepad.all.Count}");
         }
 
         public override void ProcessInput()
@@ -106,6 +113,16 @@ namespace CosmicShore.Gameplay
             inputStatus.LeftTriggerAnalog = leftTriggerValue;
             inputStatus.RightTriggerAnalog = rightTriggerValue;
 
+            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+            // Logs at most once per second, only while a trigger physically reads non-zero.
+            if ((leftTriggerValue > 0.01f || rightTriggerValue > 0.01f)
+                && Time.unscaledTime >= _diagNextRawLogTime)
+            {
+                _diagNextRawLogTime = Time.unscaledTime + 1f;
+                CSDebug.Log($"[DRIFT-DIAG] RawTrigger L={leftTriggerValue:F2} R={rightTriggerValue:F2} " +
+                            $"pad='{Gamepad.current.displayName}' type={Gamepad.current.GetType().Name}");
+            }
+
             bool leftActive = leftTriggerValue > TriggerDeadzone;
             bool rightActive = rightTriggerValue > TriggerDeadzone;
 
diff --git a/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs b/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
index 3e080ae8e..59f7a6c9f 100644
--- a/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
@@ -264,6 +264,9 @@ namespace CosmicShore.Gameplay
         {
             ResetSmoothingState();
             inputStatus.ActiveInputDevice = InputDeviceType.Keyboard;
+
+            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
+            CosmicShore.Utility.CSDebug.Log("[DRIFT-DIAG] KeyboardStrategy ACTIVATED (no gamepad detected)");
         }
 
         public override void OnStrategyDeactivated()
```

</details>

### `e27c0fdff` — fix(input): calibrate trigger edges and analog to the trigger's resting baseline

_Claude, 2026-07-22 23:20:40 +0000_

```text
Field repro: an Xbox pad whose left trigger RESTS at ~0.38. With the edge
detector's absolute TriggerDeadzone (0.05), such a trigger reads as
permanently held - leftJustPressed never fires, so trigger-bound actions
(the Squirrel's single-trigger drift) never start, in every scene. The
rest value wanders between sessions, which made the failure look
mode-specific and intermittent (menu vs Scurry, solo vs party).

Track the minimum observed raw value per trigger as its resting baseline
(min-latch; self-corrects if the trigger is held at activation, reset on
strategy re-activation for pad swaps) and remap [rest..1] onto [0..1]
before the deadzone edge detection and the InputStatus analog writes, so
edges and the Squirrel's analog drift intensity behave as on a healthy
pad. Extends the (to-be-reverted) [DRIFT-DIAG] RawTrigger log with
raw/rest values for verification.
```

```text
 Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs | 35 +++++++++++++++++++++++++++++++----
 1 file changed, 31 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
index d51e7ee6f..e62d3222e 100644
--- a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
@@ -10,6 +10,20 @@ namespace CosmicShore.Gameplay
     {
         private const float TriggerDeadzone = 0.05f;
 
+        // Worn/miscalibrated triggers can REST well above zero (field repro: an Xbox
+        // pad resting at L=0.38). A rest value above TriggerDeadzone makes the edge
+        // detector read the trigger as permanently held - the press edge never fires
+        // and trigger-bound actions (e.g. the Squirrel's drift) go dead, while the
+        // analog intensity idles non-zero. Track the minimum observed raw value per
+        // trigger as its resting baseline and remap [baseline..1] onto [0..1] so
+        // edges and analog behave as on a healthy pad. Min-tracking self-corrects if
+        // the trigger happens to be held when the strategy activates.
+        private float _leftTriggerRestBaseline = 1f;
+        private float _rightTriggerRestBaseline = 1f;
+
+        static float RemapFromRest(float raw, float rest) =>
+            rest >= 0.99f ? 0f : Mathf.Clamp01((raw - rest) / (1f - rest));
+
         private bool fullSpeedStraightEffectsStarted;
         private bool minimumSpeedStraightEffectsStarted;
 
@@ -33,6 +47,10 @@ namespace CosmicShore.Gameplay
             base.OnStrategyActivated();
             inputStatus.ActiveInputDevice = InputDeviceType.Gamepad;
 
+            // Re-calibrate on (re)activation - the active pad may have changed.
+            _leftTriggerRestBaseline = 1f;
+            _rightTriggerRestBaseline = 1f;
+
             // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
             CSDebug.Log($"[DRIFT-DIAG] GamepadStrategy ACTIVATED pad='{Gamepad.current?.displayName}' " +
                         $"type={Gamepad.current?.GetType().Name} allGamepads={Gamepad.all.Count}");
@@ -107,19 +125,28 @@ namespace CosmicShore.Gameplay
             // Triggers - read analog values and use custom deadzone for edge detection.
             // This gives full analog range (0-1) for drift scaling while keeping
             // binary event compatibility for button-style triggers (which snap 0/1).
-            float leftTriggerValue = Gamepad.current.leftTrigger.ReadValue();
-            float rightTriggerValue = Gamepad.current.rightTrigger.ReadValue();
+            // Values are measured from the trigger's calibrated resting baseline (see
+            // RemapFromRest) so a drifting trigger can't read as permanently held.
+            float leftTriggerRaw = Gamepad.current.leftTrigger.ReadValue();
+            float rightTriggerRaw = Gamepad.current.rightTrigger.ReadValue();
+
+            _leftTriggerRestBaseline = Mathf.Min(_leftTriggerRestBaseline, leftTriggerRaw);
+            _rightTriggerRestBaseline = Mathf.Min(_rightTriggerRestBaseline, rightTriggerRaw);
+
+            float leftTriggerValue = RemapFromRest(leftTriggerRaw, _leftTriggerRestBaseline);
+            float rightTriggerValue = RemapFromRest(rightTriggerRaw, _rightTriggerRestBaseline);
 
             inputStatus.LeftTriggerAnalog = leftTriggerValue;
             inputStatus.RightTriggerAnalog = rightTriggerValue;
 
             // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
             // Logs at most once per second, only while a trigger physically reads non-zero.
-            if ((leftTriggerValue > 0.01f || rightTriggerValue > 0.01f)
+            if ((leftTriggerRaw > 0.01f || rightTriggerRaw > 0.01f)
                 && Time.unscaledTime >= _diagNextRawLogTime)
             {
                 _diagNextRawLogTime = Time.unscaledTime + 1f;
-                CSDebug.Log($"[DRIFT-DIAG] RawTrigger L={leftTriggerValue:F2} R={rightTriggerValue:F2} " +
+                CSDebug.Log($"[DRIFT-DIAG] RawTrigger L={leftTriggerValue:F2} (raw={leftTriggerRaw:F2} rest={_leftTriggerRestBaseline:F2}) " +
+                            $"R={rightTriggerValue:F2} (raw={rightTriggerRaw:F2} rest={_rightTriggerRestBaseline:F2}) " +
                             $"pad='{Gamepad.current.displayName}' type={Gamepad.current.GetType().Name}");
             }
 
```

</details>

### `1a3248d02` — chore(diag): remove [DRIFT-DIAG] instrumentation (investigation closed)

_Claude, 2026-07-22 23:25:32 +0000_

```text
Root cause landed as the trigger rest-calibration fix in
GamepadInputStrategy; the temporary chain logging is no longer needed.
```

```text
 Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs                             | 3 ---
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/DriftActionSO.cs | 8 --------
 Assets/_Scripts/Controller/Vessel/VesselTransformer.cs                             | 5 -----
 3 files changed, 16 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs b/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
index 59f7a6c9f..3e080ae8e 100644
--- a/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
@@ -264,9 +264,6 @@ namespace CosmicShore.Gameplay
         {
             ResetSmoothingState();
             inputStatus.ActiveInputDevice = InputDeviceType.Keyboard;
-
-            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
-            CosmicShore.Utility.CSDebug.Log("[DRIFT-DIAG] KeyboardStrategy ACTIVATED (no gamepad detected)");
         }
 
         public override void OnStrategyDeactivated()
diff --git a/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs b/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs
index 9d5ae454d..7df707f53 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselTransformer.cs
@@ -262,11 +262,6 @@ public class VesselTransformer : MonoBehaviour
         /// </summary>
         public void BeginDrift(float rotMult, float dampTarget, bool isSharp)
         {
-            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
-            CSDebug.Log($"[DRIFT-DIAG] BeginDrift sharp={isSharp} mult={rotMult} damp={dampTarget} " +
-                        $"vessel={VesselStatus?.Name} active={isActive} stationary={VesselStatus?.IsStationary} " +
-                        $"restricted={VesselStatus?.IsTranslationRestricted} triggerSum={GetTriggerSum():F2}");
-
             _driftEaseOutPending = false;
 
             if (!_hasDriftBase)
```

</details>

### `ce11eaf68` — fix(input): recalibrate trigger baselines on pad change; idempotent button subscription

_Claude, 2026-07-22 23:25:32 +0000_

```text
Generalizes the trigger rest-calibration: baselines now re-latch whenever
the active Gamepad.current device changes (mid-session pad swap), not just
on strategy activation, so every controller - healthy, worn, or
DirectInput-style mid-rest axes - gets correct edges and analog range with
no device-specific constants. Also removes the remaining [DRIFT-DIAG]
lines from these two files.

R_VesselActionHandler: SubscribeToInputEvents is now idempotent. The local
vessel requests subscription from two independent paths
(VesselController.Initialize and the input-unpause replay through
OnToggleInputPaused), stacking the delegates so every button event
dispatched its actions twice - confirmed by the investigation's duplicated
event logs.
```

```text
 Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs      | 51 +++++++++++++++++---------------------------
 Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs | 24 +++++++++------------
 2 files changed, 30 insertions(+), 45 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 155 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
index e62d3222e..27444a75a 100644
--- a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
@@ -10,16 +10,20 @@ namespace CosmicShore.Gameplay
     {
         private const float TriggerDeadzone = 0.05f;
 
-        // Worn/miscalibrated triggers can REST well above zero (field repro: an Xbox
-        // pad resting at L=0.38). A rest value above TriggerDeadzone makes the edge
-        // detector read the trigger as permanently held - the press edge never fires
-        // and trigger-bound actions (e.g. the Squirrel's drift) go dead, while the
-        // analog intensity idles non-zero. Track the minimum observed raw value per
-        // trigger as its resting baseline and remap [baseline..1] onto [0..1] so
-        // edges and analog behave as on a healthy pad. Min-tracking self-corrects if
-        // the trigger happens to be held when the strategy activates.
+        // Trigger rest calibration. Triggers do not universally rest at zero: worn or
+        // miscalibrated springs drift upward, and some DirectInput-style pads map a
+        // trigger to an axis that rests mid-range by design. Any rest value above
+        // TriggerDeadzone makes the absolute edge detector read the trigger as
+        // permanently held - the press edge never fires and trigger-bound actions
+        // (e.g. the Squirrel's drift) go dead, while the analog intensity idles
+        // non-zero. Track the minimum observed raw value per trigger as its resting
+        // baseline and remap [baseline..1] onto [0..1] so edges and analog behave
+        // identically on every pad (a trigger resting at zero latches baseline 0 and
+        // passes through unchanged). Min-tracking self-corrects if the trigger
+        // happens to be held during calibration; a device change re-calibrates.
         private float _leftTriggerRestBaseline = 1f;
         private float _rightTriggerRestBaseline = 1f;
+        private Gamepad _calibratedPad;
 
         static float RemapFromRest(float raw, float rest) =>
             rest >= 0.99f ? 0f : Mathf.Clamp01((raw - rest) / (1f - rest));
@@ -39,21 +43,10 @@ namespace CosmicShore.Gameplay
             ResetInput();
         }
 
-        // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
-        private float _diagNextRawLogTime;
-
         public override void OnStrategyActivated()
         {
             base.OnStrategyActivated();
             inputStatus.ActiveInputDevice = InputDeviceType.Gamepad;
-
-            // Re-calibrate on (re)activation - the active pad may have changed.
-            _leftTriggerRestBaseline = 1f;
-            _rightTriggerRestBaseline = 1f;
-
-            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
-            CSDebug.Log($"[DRIFT-DIAG] GamepadStrategy ACTIVATED pad='{Gamepad.current?.displayName}' " +
-                        $"type={Gamepad.current?.GetType().Name} allGamepads={Gamepad.all.Count}");
         }
 
         public override void ProcessInput()
@@ -126,7 +119,14 @@ namespace CosmicShore.Gameplay
             // This gives full analog range (0-1) for drift scaling while keeping
             // binary event compatibility for button-style triggers (which snap 0/1).
             // Values are measured from the trigger's calibrated resting baseline (see
-            // RemapFromRest) so a drifting trigger can't read as permanently held.
+            // RemapFromRest) so a non-zero-resting trigger can't read as permanently held.
+            if (!ReferenceEquals(_calibratedPad, Gamepad.current))
+            {
+                _calibratedPad = Gamepad.current;
+                _leftTriggerRestBaseline = 1f;
+                _rightTriggerRestBaseline = 1f;
+            }
+
             float leftTriggerRaw = Gamepad.current.leftTrigger.ReadValue();
             float rightTriggerRaw = Gamepad.current.rightTrigger.ReadValue();
 
@@ -139,17 +139,6 @@ namespace CosmicShore.Gameplay
             inputStatus.LeftTriggerAnalog = leftTriggerValue;
             inputStatus.RightTriggerAnalog = rightTriggerValue;
 
-            // TEMPORARY [DRIFT-DIAG]: remove after the Scurry drift investigation.
-            // Logs at most once per second, only while a trigger physically reads non-zero.
-            if ((leftTriggerRaw > 0.01f || rightTriggerRaw > 0.01f)
-                && Time.unscaledTime >= _diagNextRawLogTime)
-            {
```

</details>

### `e75569d3d` — perf(vessel): trim redundant execution in the drift / AI-pilot paths

_Claude, 2026-07-23 00:01:57 +0000_

```text
- AIPilot.StopAIPilot early-outs when already stopped - every human turn
  start clears the pilot defensively, so the native StopAllCoroutines and
  enabled writes no longer run on every countdown end.
- VesselController.ToggleAIPilot uses TryGetComponent for the cinematic
  stop instead of the VesselStatus GetOrAdd accessor, so vessels that
  never ran the end-game flourish don't get an AICinematicBehavior
  component instantiated just to no-op stop it.
- GamepadInputStrategy.RemapFromRest short-circuits to identity for
  triggers resting at zero - healthy pads skip the remap divide entirely.
- DriftAudioController self-disables when the vessel-class gate fails,
  matching its existing remote/AI self-disable, so wrong-class vessels
  carrying the component stop paying Update() dispatch for a permanent
  early-out.
```

```text
 Assets/_Scripts/Controller/AI/AIPilot.cs              | 5 +++++
 Assets/_Scripts/Controller/FX/DriftAudioController.cs | 8 +++++++-
 Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs | 4 +++-
 Assets/_Scripts/Controller/Vessel/VesselController.cs | 6 +++++-
 4 files changed, 20 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index 8548185d0..7d440559f 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -319,6 +319,11 @@ namespace CosmicShore.Gameplay
 
         public void StopAIPilot()
         {
+            // Already stopped - every human turn start clears the pilot defensively,
+            // so skip the redundant native StopAllCoroutines/enabled writes.
+            if (!AutoPilotEnabled && !enabled)
+                return;
+
             AutoPilotEnabled = false;
 
             // StopCoroutine(UseAbilityCoroutine(ability)) stopped a freshly created
diff --git a/Assets/_Scripts/Controller/FX/DriftAudioController.cs b/Assets/_Scripts/Controller/FX/DriftAudioController.cs
index 3180affca..938126d5e 100644
--- a/Assets/_Scripts/Controller/FX/DriftAudioController.cs
+++ b/Assets/_Scripts/Controller/FX/DriftAudioController.cs
@@ -242,7 +242,13 @@ namespace CosmicShore.Gameplay.Audio
                         this);
                 }
             }
-            if (!_classGatePass) return;
+            if (!_classGatePass)
+            {
+                // Self-disable like the remote/AI branch below - a wrong-class vessel
+                // should not keep paying Update() dispatch for a permanent early-out.
+                enabled = false;
+                return;
+            }
 
             // Local-user gating: skip remote/AI vessels in default mode. We
             // can't decide until Player is set, so retry each frame until
diff --git a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
index 27444a75a..c60bccfbb 100644
--- a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
@@ -26,7 +26,9 @@ namespace CosmicShore.Gameplay
         private Gamepad _calibratedPad;
 
         static float RemapFromRest(float raw, float rest) =>
-            rest >= 0.99f ? 0f : Mathf.Clamp01((raw - rest) / (1f - rest));
+            rest <= 0f ? raw                                          // healthy pad: identity, no divide
+            : rest >= 0.99f ? 0f                                      // pinned/broken axis: never active
+            : Mathf.Clamp01((raw - rest) / (1f - rest));
 
         private bool fullSpeedStraightEffectsStarted;
         private bool minimumSpeedStraightEffectsStarted;
diff --git a/Assets/_Scripts/Controller/Vessel/VesselController.cs b/Assets/_Scripts/Controller/Vessel/VesselController.cs
index 5afa891d2..03dd14615 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselController.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselController.cs
@@ -210,7 +210,11 @@ namespace CosmicShore.Gameplay
             // Any explicit pilot-mode change ends a cinematic flourish. EndGameSequencer
             // starts its behavior AFTER enabling the pilot, so the flourish still works;
             // nothing else may leave the cinematic writing input into a live vessel.
-            VesselStatus.AICinematicBehavior.StopCinematicBehavior();
+            // TryGetComponent, not the VesselStatus GetOrAdd accessor: a vessel that
+            // never flourished shouldn't have the component instantiated just to
+            // no-op stop it.
+            if (TryGetComponent<AICinematicBehavior>(out var cinematic))
+                cinematic.StopCinematicBehavior();
 
             if (toggle)
                 VesselStatus.AIPilot.StartAIPilot();
```

</details>

### `7e6c84400` — docs: record the drift-conflict investigation's fixes and locked rules

_Claude, 2026-07-23 00:31:31 +0000_

```text
- CLAUDE.md Input Strategy Pattern: only the local human's InputController
  runs (gated at pair-init, where AI-ness is first reliable); gamepad
  triggers are rest-calibrated - never compare raw trigger reads against an
  absolute threshold.
- CLAUDE.md AI Opponent System: pilot lifecycle rules - ToggleAIPilot is
  the single choke point (also ends the cinematic flourish), a human turn
  never starts with autopilot on (StartPlayer/ChangePlayer symmetry, the
  root of the AI-vs-player drift conflict), enabled mirrors active state,
  StartAIPilot idempotent / StopAIPilot StopAllCoroutines.
- CLAUDE.md DI Patterns: AddComponent runs Awake/OnEnable inline before the
  caller can assign fields - factories must complete the deferred
  subscription explicitly (ElementalComebackSystem reference).
- SQUIRREL_TUBE.md: the two robustness rules protecting the single-trigger
  drift scheme (rest calibration; local-pilot-only drift one-shots).
- PERFORMANCE_OPTIMIZATION.md: shipped rows for bec6338c / 6e899993 /
  ce11eaf6 / e75569d3 (Update-dispatch eliminations, input polling gate,
  idempotent subscription, drift-path trims).
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_TUBE.md | 14 ++++++++++++++
 CLAUDE.md                                                          | 31 ++++++++++++++++++++++++++++++-
 Docs/PERFORMANCE_OPTIMIZATION.md                                   |  4 ++++
 3 files changed, 48 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 96 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_TUBE.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_TUBE.md
index dc095718f..b5fa66065 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_TUBE.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_TUBE.md
@@ -19,6 +19,20 @@ across the full 0-2 drift range so a single trigger spans the light→sharp rang
 from summing both triggers. See `DriftActionSO.playDriftSfx` (off on the sharp tier) so the shared
 drift SFX isn't doubled when both tiers stack on one trigger.
 
+Two robustness rules protect this control scheme:
+
+- **Trigger rest calibration** (`GamepadInputStrategy`): trigger analog values are measured from a
+  min-latched per-trigger resting baseline, not raw — a worn trigger resting above the deadzone
+  (field repro: an Xbox pad resting at 0.38) otherwise reads as permanently held, the press edge
+  never fires, and drift goes dead on that pad in every scene. The remap also restores the full
+  analog range the 0-2 drift ramp depends on.
+- **Drift start/end one-shots are local-pilot-only** (`DriftActionSO`): the 2D
+  `DriftStart`/`DriftEnd` SFX and the HUD raises sit behind the `IsLocalUser` gate — the action
+  pipeline replays on every peer and AI pilots drive it too, so ungated one-shots spam full-volume
+  drift cues over the local pilot's own feedback. Physics (`BeginDrift`/`IsDrifting`) still runs on
+  all peers; remote/AI vessels keep only spatialized per-vessel audio (`DriftAudioController`,
+  already local-gated).
+
 The ability fires on the trigger **press** (`Begin`); release (`Commit`) does nothing.
 
 ## Placement — straight out the front, led by speed
diff --git a/CLAUDE.md b/CLAUDE.md
index e40f5d485..64e105400 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -725,7 +725,7 @@ The project uses Reflex DI with `AppManager` as the root `IInstaller`. All persi
 #### DI Patterns to Follow
 
 - **Use `[Inject]` for shared assets**: `GameDataSO`, `SceneNameListSO`, and other DI-registered assets should be accessed via `[Inject]`, not `[SerializeField]`. This eliminates manual inspector wiring and serialization drift.
-- **Injection timing**: `[Inject]` fields are populated after `Awake()` but before `Start()`. Access injected fields in `Start()` or later — never in `Awake()`. If you need to subscribe to events in `OnEnable()`, use a deferred pattern: attempt in `OnEnable()`, retry with duplicate guards in `Start()`.
+- **Injection timing**: `[Inject]` fields are populated after `Awake()` but before `Start()`. Access injected fields in `Start()` or later — never in `Awake()`. If you need to subscribe to events in `OnEnable()`, use a deferred pattern: attempt in `OnEnable()`, retry with duplicate guards in `Start()`. The same hazard applies to runtime creation: `AddComponent<T>()` runs the new component's `Awake`/`OnEnable` INLINE, before the caller's next line can assign any field — so a factory that assigns dependencies after `AddComponent` must also explicitly complete the deferred subscription (reference: `ElementalComebackSystem.EnsureExists` + `TrySubscribeToGameEvents`).
 - **ContainerScope per scene**: Each scene that uses `[Inject]` must have a Reflex `ContainerScope` component (via the `ContainerScope.prefab` in `_Prefabs/CORE/`). The Bootstrap scene's scope is the root; other scenes get child scopes.
 
 ### Input Strategy Pattern
@@ -738,6 +738,18 @@ Platform-agnostic input via `Assets/_Scripts/Controller/IO/`:
 - `InputController` — manages active strategy and input state
 - `IInputStatus` / `InputStatus` — input state container
 - Input strategies are swappable per platform/context at runtime
+- **Only the local human's `InputController` runs.** `Player.InitializeForMultiplayerMode`
+  sets `InputController.enabled = IsLocalUser` (the earliest point AI-ness is reliable —
+  `NetIsAI` is written by the AI spawner *after* `Spawn()`, so `OnNetworkSpawn` cannot gate
+  this). AI pilots write `InputStatus` directly; remote vessels replicate theirs.
+- **Gamepad triggers are rest-calibrated.** Triggers do not universally rest at zero (worn
+  springs drift upward; some DirectInput-style pads rest mid-range by design), and a rest
+  value above the deadzone reads as "permanently held" — the press edge never fires and
+  trigger-bound actions (e.g. the Squirrel's drift) go dead. `GamepadInputStrategy`
+  min-latches each trigger's observed resting baseline (re-latched on `Gamepad.current`
+  device change) and remaps `[rest..1] → [0..1]` before edge detection and the
+  `InputStatus` analog writes. Do not compare raw trigger reads against an absolute
+  threshold anywhere else.
 
 ### Impact Effects Architecture
 
@@ -1456,6 +1468,23 @@ Runtime-configurable AI opponents at `Assets/_Scripts/Controller/AI/`:
 - AI profiles used for score cards and multiplayer backfill
 - Configurable AI ship selection and behavior at runtime
 
+**AI pilot lifecycle (do not regress):**
+
+- `VesselController.ToggleAIPilot(bool)` is the single choke point for AI control of a
+  vessel — it also stops any `AICinematicBehavior` flourish (the `EndGameSequencer` starts
+  its behavior *after* enabling the pilot, so the end-game flourish still works). Never
+  call `AIPilot.StartAIPilot`/`StopAIPilot` or start a cinematic around this seam.
+- **A human-controlled turn never starts with autopilot on**: `Player.StartPlayer`'s human
+  branch calls `ToggleAIPilot(false)` (symmetric with the AI branch), and
+  `VesselController.ChangePlayer` clears the pilot when a human receives a vessel
+  (Cellular Duel's between-round swap vs an AI opponent). A leaked `AutoPilotEnabled`
+  blocks every button action in `R_VesselActionHandler` while `AIPilot.Update` fights the
+  player's input — the root of the "AI drifting conflicts with my drifting" class of bug.
+- `AIPilot` and `AICinematicBehavior` keep their `enabled` flag mirrored to their active
+  state: no `Update()` dispatch on vessels they aren't driving. `StartAIPilot` is
+  idempotent; `StopAIPilot` uses `StopAllCoroutines` (a `StopCoroutine(new enumerator)`
+  never stops the running coroutine).
+
 ### Menu Screen Navigation (Menu_Main Scene)
 
```

</details>

### `9d5439856` — docs: restructure CLAUDE.md into a lean rules-and-routing dictionary

_Claude, 2026-07-23 00:41:54 +0000_

```text
CLAUDE.md is loaded into every session, so per-system deep-dives paid a
permanent context tax and diluted the always-mandatory rules. 2151 -> 1022
lines with ZERO information loss:

Stays inline (rules that must never be missed): Prime Directive, LOCKED
ecosystem invariants, project structure, SOAP contract + anti-patterns,
threading contract, DI patterns, input rules, domain-sync rules, code
style, perf standards, debugging methodology, never-do list, Design
Philosophy, the Key Systems dictionary, and a rules digest + pointer per
extracted topic.

Moved verbatim to Docs/ (each with a canonical-home preamble):
- BOOTSTRAP_AUTH_FLOW.md - bootstrap/auth/app-state flow diagrams + tables
- MULTIPLAYER_SPAWNING.md - Netcode components, spawn chains, freestyle
  flight, player-count/AI-backfill pipeline
- PARTY_SOCIAL.md - party lobby + friend system reference (services, SOAP
  types, facade API, UI/SO inventories)
- HEXRACE_SUMMARY.md - condensed HexRace notes (canonical: HEXRACE.md)
- MENU_NAVIGATION.md - ScreenSwitcher/IScreen screen system
- LAVALAMP.md - menu-freestyle merge (hierarchies, HUD lifecycle, phases)
- ELEMENTAL_BARS.md - petal-flower HUD spec

Documentation Index gains rows for the new docs (existing rows preserved -
several carry unique locked content). Cross-mode rules previously buried in
the HexRace section now live in a Domain Game Modes digest.
```

```text
 CLAUDE.md                    | 1373 +++++++-----------------------------------------------------------------
 Docs/BOOTSTRAP_AUTH_FLOW.md  |  261 ++++++++++++++
 Docs/ELEMENTAL_BARS.md       |   29 ++
 Docs/HEXRACE_SUMMARY.md      |  112 ++++++
 Docs/LAVALAMP.md             |  213 ++++++++++++
 Docs/MENU_NAVIGATION.md      |   63 ++++
 Docs/MULTIPLAYER_SPAWNING.md |  311 +++++++++++++++++
 Docs/PARTY_SOCIAL.md         |  238 +++++++++++++
 8 files changed, 1349 insertions(+), 1251 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2696 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 64e105400..af9caf639 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -226,82 +226,26 @@ All first-party gameplay code compiles in Unity's default assembly (no runtime `
 
 Third-party assemblies: `Obvious.Soap`, `PlayFab`, `Lofelt.NiceVibrations`, `NativeShare.Runtime`
 
-### Scene Inventory
-
-See `Docs/SCENES.md` for the full scene and game mode reference. Summary below.
-
-#### Core Application Scenes
-
-| Scene | Build Order | Purpose |
-|---|---|---|
-| **Bootstrap** | 0 (must be first) | App entry: DI registration, platform config, auth start, splash |
-| **Authentication** | 1 | Auth UI, cached session check, NetworkManager host start |
-| **Menu_Main** | 2 | Main menu with networked autopilot vessel, screen navigation |
-
-#### Multiplayer Game Scenes
-
-There are no single-player scenes: **solo play is a multiplayer game whose party is one
-host** (eager Relay session + AI backfill via `ServerPlayerVesselInitializerWithAI`).
-The former `Singleplayer Scenes/` folder is gone; `SplashScreen.unity` lives at
-`Assets/_Scenes/`.
-
-| Scene | Game Mode | Controller |
-|---|---|---|
-| `MinigameHexRace` | `HexRace (33)` | `HexRaceController` |
-| `MinigameCrystalCaptureMultiplayer_Gameplay` | `MultiplayerCrystalCapture (35)` | `MultiplayerCrystalCaptureController` |
-| `MinigameDuelForCellMultiplayer_Gameplay` | `MultiplayerCellularDuel (29)` | `MultiplayerCellularDuelController` |
-| `MinigameJoust_Gameplay` | `MultiplayerJoust (34)` | `MultiplayerJoustController` |
-| `MinigameAstroLeague` | `AstroLeague (36)` | `AstroLeagueController` |
-| `MinigameNucleusRush` | `NucleusRush (38)` | `NucleusRushController` |
-| `MinigameWildlifeBlitz` | `WildlifeBlitz (26)` | `MultiplayerWildlifeBlitzController` |
-| `BenchmarkStressTest` | (Settings → Run Benchmark; `WildlifeBlitz` mode) | `SandboxBenchmarkController` |
-| `ArcadeGameMultiplayer2v2CoOpVsAI` | `Multiplayer2v2CoOpVsAI (30)` | Domain games variant |
-
-All in `Assets/_Scenes/Multiplayer Scenes/`.
-
-#### Tool & Test Scenes
-
-`Recording Studio`, `MattsRecording Studio`, `PhotoBooth` (in `_Scenes/Tools/`), `AudioTestSandbox` (in `_Scenes/Game_TestDesign/`).
-
-### Game Modes & Controllers
-
-#### GameModes Enum (`Assets/_Scripts/Data/Enums/GameModes.cs`)
-
-38 game modes with explicit numeric IDs (highest is `NucleusRush(38)`; IDs 7 and 31 are skipped). Single-player: `Elimination(1)` through `ProtectMission(27)`. Multiplayer: `MultiplayerCellularDuel(29)`, `Multiplayer2v2CoOpVsAI(30)`, `MultiplayerWildlifeBlitzGame(32)`, `HexRace(33)`, `MultiplayerJoust(34)`, `MultiplayerCrystalCapture(35)`, `AstroLeague(37)`, `NucleusRush(38)`. Meta-mode: `Tournament(36)` — the session-level meta that chains HexRace → Joust → Crystal Capture back-to-back via sequential `Single` loads (see `Docs/TournamentSystem/ARCHITECTURE.md`). `AstroLeague(37)` is hypersea soccer (a standalone domain minigame, see `_Scripts/Controller/Arcade/ASTROLEAGUE.md`). `NucleusRush(38)` (display name "Brood Rush") is the nucleus-control fauna-wave race (see `_Scripts/Controller/Arcade/NUCLEUSRUSH.md`). Meta sentinel: `Random(0)`. Note: IDs 7 and 31 are skipped — 7 was the retired standalone arcade Freestyle game, 31 was never assigned. `MultiplayerFreestyle(28)` was retired 2026-07-21: freestyle lives ONLY in Menu_Main as the lava lamp (see "Lava-Lamp Mode"); the standalone sandbox scene/controller/card are deleted. Do not reuse any of these IDs.
-
-Solo modes were retired 2026-07-20: their scene-less `SO_ArcadeGame` cards are deleted and the retired enum IDs (1-6, 8-25, 27, 32) are kept only for serialized-int stability (annotated do-not-reuse in `GameModes.cs`). `WildlifeBlitz (26)` lives on as the networked single-host co-op blitz; `CellularDuel` play lives on as `MultiplayerCellularDuel (29)`.
-
-#### Controller Hierarchy
-
-```
-MiniGameControllerBase (abstract, NetworkBehaviour)
-│   Template Method: rounds → turns → countdown → gameplay → end
-│
-└── MultiplayerMiniGameControllerBase (abstract, NetworkBehaviour)
-    │   Server-authoritative turn/round/game flow via ClientRpc
-    │
-    └── MultiplayerDomainGamesController
-        ├── HexRaceController              — crystal race, deterministic track, golf scoring
-        ├── MultiplayerJoustController      — collision tracking, golf scoring
-        ├── MultiplayerCellularDuelController — vessel ownership swap between rounds
-        ├── MultiplayerCrystalCaptureController — minimal (1 round, 1 turn)
-        ├── AstroLeagueController             — hypersea soccer, server-simulated ball, golden goal
-        ├── NucleusRushController             — nucleus-control fauna-wave race, brood scoring
-        └── MultiplayerWildlifeBlitzController — co-op clear-the-cell vs clock (golf: clear time / DNF)
-            └── SandboxBenchmarkController — endless auto-start benchmark (no monitors)
-```
-
-The single-player controller branch (`SinglePlayerMiniGameControllerBase` + subclasses)
-and the non-networked spawn path (`PlayerSpawner`/`VesselSpawner`/spawner adapters) were
-deleted 2026-07-20 — every mode runs the networked single-host model.
-
-#### Game Launch Pipeline
-
-1. **`SO_ArcadeGame` asset** — static config (mode, scene, captains, player/intensity ranges, scoring)
```

</details>

### `a5e187112` — docs(tests): persist the solo-retirement verification checklist

_Claude, 2026-07-23 01:56:38 +0000_

```text
Tracks the consolidated in-editor/MPPM pass for the branch (Y1 + C1-C8 +
the upcoming C9 duel retirement). Steps 1-6 marked owner-verified
2026-07-21; the two Cellular Duel steps struck as obsolete ahead of C9.
```

```text
 Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md | 56 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 56 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
new file mode 100644
index 000000000..d178d7415
--- /dev/null
+++ b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
@@ -0,0 +1,56 @@
+# Solo-Retirement Program — Verification Checklist (`claude/unified-yash-refactor-9sc0ws`)
+
+One consolidated in-editor/MPPM pass covering everything on this branch: Y1 scoring
+unification → C1–C8 (solo retirement, benchmark conversion, `IsMultiplayerMode` removal,
+dead-content deletion, standalone-freestyle retirement) → C9 (Cellular Duel retirement).
+Steps are ordered to minimize scene churn. Keep the numbering — progress is tracked
+against it.
+
+Legend: `[x]` verified by owner · `[ ]` pending · ~~struck~~ = obsolete (feature retired
+after the step was written; do not test).
+
+## Setup
+
+- [x] **1.** Pull the branch, open in Unity, let it compile — zero compile errors. *(verified 2026-07-21)*
+- [x] **2.** Test Runner → EditMode → run all: `EnumIntegrityTests`, `SceneFlowIntegrationTests`, `DomainAssignerTests`, Bootstrap tests — all green. *(verified 2026-07-21)*
+- [x] **3.** For Part D, MPPM with one clone (2 humans total). *(verified 2026-07-21)*
+
+## Part A — Boot + lava lamp (C5, C8)
+
+- [x] **4.** Boot → auth → Menu_Main: normal startup, host starts, no errors (C5 deleted the legacy matchmaking path in `MultiplayerSetup` — sign-in host start must be unaffected). *(verified 2026-07-21)*
+- [x] **5.** Lava-lamp regression (C8): autopilot vessel drifts behind UI → tap crystal → control + Game UI + vessel HUD. Toys all work (vessel changer keeps domain/speed + HUD re-shows, domain changer, painting, Wanderway). Gamepad **Start** exits; center-tap returns to menu. *(verified 2026-07-21)*
+- [x] **6.** Arcade grid contents (C6, C8, C9): no solo cards, no Freestyle card, no Cellular Duel card. Expected: HexRace, Joust, Crystal Capture, Maelstrom (OrganicRematchGames) + Wildlife Blitz (LaunchPartyAllGames surfaces). *(verified 2026-07-21 pre-C9 — re-check only that the Duel card is gone)*
+
+## Part B — Solo runs (solo = party of one + AI)
+
+- ~~**7.** Cellular Duel solo~~ — **OBSOLETE**: Cellular Duel retired (C9, 2026-07-21). Do not test.
+- [ ] **8.** Wildlife Blitz solo — WIN (C2): launch alone → no AI. Kills/crystals/hostile volume tick the centerline (server-fed composite); HUD shows score target + lifeform counter. Clear the cell before the 120 s clock → VICTORY reveal + clear time; scoreboard shows the time.
+- [ ] **9.** Wildlife Blitz solo — LOSS (**the B17 fix — most important single test**): relaunch, idle until the clock expires → DEFEAT reveal + "CELL UNCLEARED" and a neutral **GAME OVER** banner (Blue tint). If you see VICTORY, that is a failure.
+- [ ] **10.** Blitz solo with AI teammates: select 3 players → 2 AI teammates spawn (co-op, same domain); their kills feed the shared team objective.
+- [ ] **11.** Blitz Play Again: full scene reload, fresh cell, second run scores from zero (no dead end-game on the second run).
+- [ ] **12.** Benchmark (C3): Settings → Run Benchmark → loads via Netcode, auto-starts in ~1 s with no Ready click, your Squirrel + AI-crowd-size AI Squirrels on distinct spawn points, HUD score accrues, never ends; Exit returns to menu.
+
+## Part C — Regression on untouched modes (the Y1.2/C2 shared tail touched all of them)
+
+- [ ] **13.** HexRace solo: 3 AI backfill, ends at crystal target, VICTORY/DEFEAT + time, ranked scoreboard, Play Again (scene reload).
+- [ ] **14.** Joust solo: normal end; winner name = top jouster on the winning domain (accepted Y1.2 delta).
+- [ ] **15.** Crystal Capture solo: target/remaining display correct, normal end.
+- [ ] **16.** AstroLeague + NucleusRush solo: quick sanity — launch, score, end sequence fires once.
+- [ ] **17.** Maelstrom/Tournament solo (C5 touched `TournamentController`): full chain HexRace → Joust → Crystal Capture, standings fold, race-to-6, summary screen.
+
+## Part D — 2-human party (MPPM)
+
+- [ ] **18.** Party: invite → client joins → both vessels visible in the menu lava lamp; each toggles freestyle independently (C8 safety).
+- [ ] **19.** Presence (C5 intentional change): while one player is in ANY game — including solo — the other sees the match name in the friends list.
+- [ ] **20.** HexRace 2-human: centerline + domain boxes live on both machines, identical final results host vs client, single end sequence, Play Again, then the menu-cycle (T15): game → menu → another game, no dead second end-game.
+- [ ] **21.** Joust + Crystal Capture 2-human: same checks (also proves the C5 config-RPC change — client receives correct intensity/player count).
+- ~~**22.** Duel 2-human~~ — **OBSOLETE**: Cellular Duel retired (C9, 2026-07-21). Do not test.
+- [ ] **23.** Blitz 2-human co-op: both on one team feeding the shared objective; win → both share the clear time; DNF → both DEFEAT + GAME OVER banner.
+
+## Throughout
+
+- [ ] **24.** Console watch: no NREs, no `[Invalid Destroy]`, no missing-script/reference errors from deleted classes. (The one known pre-existing missing-script component lived in the duel scene — gone with C9; none should remain anywhere.)
+
+---
+
+*Update the checkboxes as you verify; anything that fails → report the step number + console output.*
```

</details>

### `9428a5a2b` — refactor(duel): retire Cellular Duel (mode 29) - delete the whole stack

_Claude, 2026-07-23 02:11:59 +0000_

```text
Owner decision: Cellular Duel is obsolete. Deleted (all verified sole-use by
guid + class-name grep at execution time):
- MinigameDuelForCellMultiplayer_Gameplay.unity (+ build-settings entry;
  takes the repo's last missing-script component with it)
- MultiplayerCellularDuelController, CellularDuelScoringRuleSO +
  CellularDuelScoringRule.asset, ArcadeGameMultiplayerCellularDuel.asset
- GameDataSO.SwapVessels + IVessel/VesselController.ChangePlayer (the
  between-round ownership-swap machinery; SwapVessels was its only caller,
  the duel controller the only caller of SwapVessels; the menu vessel
  changer uses the separate RequestSwap/ReInitializePair path)
- Opportunistic verified-dead: LocalVolumeUIController (orphaned by the
  freestyle retirement), MinigameHUDInspector (#if false, references a
  nonexistent enum)

Pruned the card from LaunchPartyAllGames (now blitz-only) and the 7
SO_Class vessel game lists (now empty, matching Dolphin/Sparrow).
ScoringMetric.VolumeCreated(5)/VolumeActivity(6) annotated retired and
their ScoringMetrics arms removed (no rule asset selects either).
GameModes 29 + CTA 410/429 annotated do-not-reuse (members kept for
serialized-int stability - EnumIntegrityTests pins stay green).

KEPT (shared with the 2v2CoOpVsAI scene, mode 30): DuelForCellScoreboard +
the Duel Cell Stats prefab family + their 2 UI scripts. NOTE: mode 30's
card is referenced by nothing (unreachable content) - if it is retired
next, those six items flip to sole-use. Duel-named card art (icons,
background, preview prefab/video) is shared by 6-7 live cards incl.
Maelstrom and stays.

Docs: CLAUDE.md (replay exception + AI-pilot handover rationale),
SCENES.md (tables/hierarchy/section), ScoringSystem ARCHITECTURE/
REFACTOR/TESTS touchpoints.
```

```text
 Assets/_SO_Assets/Games/ArcadeGameMultiplayerCellularDuel.asset.meta  |    8 -
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset           |    1 -
 Assets/_SO_Assets/Scoring Rules/CellularDuelScoringRule.asset         |   16 -
 Assets/_SO_Assets/Scoring Rules/CellularDuelScoringRule.asset.meta    |    8 -
 .../Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity  | 5143 -------------------------------
 .../MinigameDuelForCellMultiplayer_Gameplay.unity.meta                |    7 -
 .../_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs   |  161 -
 .../Controller/Arcade/MultiplayerCellularDuelController.cs.meta       |    3 -
 .../_Scripts/Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs   |   64 -
 .../Controller/Arcade/Scoring/CellularDuelScoringRuleSO.cs.meta       |   11 -
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs           |   10 +-
 Assets/_Scripts/Controller/Player/Player.cs                           |    2 +-
 Assets/_Scripts/Controller/Vessel/IVessel.cs                          |    1 -
 Assets/_Scripts/Controller/Vessel/VesselController.cs                 |   40 -
 Assets/_Scripts/Data/Enums/CallToActionTargetType.cs                  |    4 +-
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    7 +-
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                           |   16 +-
 Assets/_Scripts/UI/LocalVolumeUIController.cs                         |   57 -
 Assets/_Scripts/UI/LocalVolumeUIController.cs.meta                    |    3 -
 Assets/_Scripts/UI/Model/MinigameHUDInspector.cs                      |  110 -
 Assets/_Scripts/UI/Model/MinigameHUDInspector.cs.meta                 |    2 -
 Assets/_Scripts/UI/Scoreboard.cs                                      |    6 +-
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |   15 -
 CLAUDE.md                                                             |    6 +-
 Docs/SCENES.md                                                        |   22 +-
 Docs/ScoringSystem/ARCHITECTURE.md                                    |    3 +-
 Docs/ScoringSystem/REFACTOR.md                                        |    5 +-
 Docs/ScoringSystem/TESTS.md                                           |    4 +-
 ProjectSettings/EditorBuildSettings.asset                             |    3 -
 37 files changed, 39 insertions(+), 5760 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 810 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs
deleted file mode 100644
index eefad7376..000000000
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerCellularDuelController.cs
+++ /dev/null
@@ -1,161 +0,0 @@
-using System.Collections.Generic;
-using Unity.Netcode;
-using CosmicShore.Data;
-using CosmicShore.Utility;
-
-namespace CosmicShore.Gameplay
-{
-    /// <summary>
-    /// Multiplayer Cellular Duel: two 120s rounds (NetworkTimeBasedTurnMonitor), vessel
-    /// ownership swap between rounds, scored by cumulative volume activity via
-    /// CellularDuelScoringRuleSO. End-game runs the base SyncFinalResults template at the
-    /// final round's end.
-    /// NOTE: this controller is also a leftover on the dead mode-32 co-op WildlifeBlitz
-    /// scene (player-unreachable, in no game list - AUDIT 1.7, fate is decision D3). That
-    /// scene wires no rule, so its end-game is inert under HasEndGame=false - acceptable
-    /// for unreachable content; D3 decides prune-or-revive.
-    /// </summary>
-    public class MultiplayerCellularDuelController : MultiplayerDomainGamesController
-    {
-        // Duel ends through OnTurnEndedCustom → the base SyncFinalResults template at the
-        // final round; suppress the base turn→round→game flow so we don't get a duplicate
-        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
-        protected override bool HasEndGame => false;
-
-        // ── Mid-turn centerline feed (replaces the legacy NetworkScoreTracker's three
-        // volume strategies, Y1.1). Server-only: RoundStats.Score suppresses the local
-        // OnScoreChanged while spawned, so only a server write (→ n_Score replication)
-        // reaches the MiniGameHUD centerline on every peer. B15
-        // (Docs/ScoringSystem/BUGS.md): detach from THIS record only - never by iterating
-        // gameData.RoundStatsList - and never gate cleanup on turn-end alone.
-        readonly List<IRoundStats> _scoreFeedStats = new();
-
-        public override void OnNetworkSpawn()
-        {
-            base.OnNetworkSpawn();
-            if (IsServer)
-                gameData.OnMiniGameTurnStarted.OnRaised += RefreshScoreFeed;
-        }
-
-        public override void OnNetworkDespawn()
-        {
-            if (IsServer)
-                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshScoreFeed;
-            StopScoreFeed();
-            base.OnNetworkDespawn();
-        }
-
-        public override void OnDestroy()
-        {
-            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
-            base.OnDestroy();
-        }
-
-        /// <summary>
-        /// Runs at every round's turn start - the Contains guard makes re-subscription a
-        /// no-op. The rule-null case is the leftover co-op WildlifeBlitz scene, which keeps
-        /// its legacy Score source untouched.
-        /// </summary>
-        void RefreshScoreFeed()
-        {
-            if (rule == null) return;
-            foreach (var stats in gameData.RoundStatsList)
-            {
-                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
-                stats.OnVolumeCreatedChanged += FeedScore;
-                stats.OnHostileVolumeDestroyedChanged += FeedScore;
-                stats.OnFriendlyVolumeDestroyedChanged += FeedScore;
-                _scoreFeedStats.Add(stats);
-            }
-        }
-
-        void StopScoreFeed()
-        {
-            foreach (var stats in _scoreFeedStats)
```

</details>

### `8e157e316` — refactor(blitz): retire Wildlife Blitz (mode 26) - delete the stack, decouple the benchmark

_Claude, 2026-07-23 03:03:24 +0000_

```text
Owner decision: Wildlife Blitz is obsolete. Deleted (verified sole-use after
the benchmark decouple):
- MinigameWildlifeBlitz.unity + build-settings entry
- MultiplayerWildlifeBlitzController, WildlifeBlitzObjectiveTurnMonitor,
  WildlifeBlitzScoreKeeper, WildlifeBlitzScoringRuleSO + rule asset,
  WildlifeBlitzHUD, WildlifeBlitzStatsProvider, WildlifeBlitzStats (zero refs),
  ArcadeGameWildlifeBlitz card, the 3 blitz-only SOAP event assets
  (EventOnSetScoreTarget / EventOnUpdateLifeFormCounterDisplayString /
  EventOnScoreChanged), UGSStatsManager.ReportBlitzStats (keeper was its
  sole caller)

Benchmark decoupled (it was built on the blitz stack in C3):
- SandboxBenchmarkController re-parents onto MultiplayerMiniGameControllerBase
  (auto-start kept; no scoring)
- BenchmarkStressTest.unity surgically stripped: keeper/HUD/provider
  components + their prefab-instance addedComponents entries and the
  EventListener/name override groups binding them removed; the kept
  Ready-button persistent calls retarget-typed to SandboxBenchmarkController;
  stale rule/scoreTracker/EndGameSequencer fields cleaned; orphaned stripped
  docs pruned; scene lints clean (67 docs, zero bad refs)

LaunchPartyAllGames is now an empty list (still wired into Menu_Main /
Arcade Screen / Minigames Screen / LoadoutCard - those surfaces render zero
cards; consumers loop null-safely). GameModes 26 annotated retired-as-playable
(BenchmarkSceneLauncher still sets the id for AI crystal-seek behavior);
CTA 427 annotated. KEPT: WildlifeBlitzPlayerStatsProfile +
PlayerStatsProfile.BlitzStats + cloud models (persisted schema),
ElementalCrystalsCollectedBlitzScoring (legacy BaseScoreTracker family,
separate blocker list), dormant SO_TrainingGame_WildLifeBlitz + quest assets
(D2/D6; known inert dangling card ref like the other 5 training assets).

Docs: SCENES.md, SettingsSystem/ARCHITECTURE.md, GameModes comments,
SOLO_RETIREMENT_TESTS.md (steps 8-11 + 23 struck).
```

```text
 Assets/_SO_Assets/Games/ArcadeGameWildlifeBlitz.asset.meta            |    8 -
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset           |    3 +-
 Assets/_SO_Assets/Scoring Rules/WildlifeBlitzScoringRule.asset        |   16 -
 Assets/_SO_Assets/Scoring Rules/WildlifeBlitzScoringRule.asset.meta   |    8 -
 Assets/_Scenes/Multiplayer Scenes/BenchmarkStressTest.unity           |  197 +--
 Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeBlitz.unity         | 2051 -------------------------------
 Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeBlitz.unity.meta    |    7 -
 .../_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs  |   76 --
 .../Controller/Arcade/MultiplayerWildlifeBlitzController.cs.meta      |   11 -
 Assets/_Scripts/Controller/Arcade/SandboxBenchmarkController.cs       |   13 +-
 .../_Scripts/Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs  |   91 --
 .../Controller/Arcade/Scoring/WildlifeBlitzScoringRuleSO.cs.meta      |   11 -
 .../Arcade/TurnMonitors/WildlifeBlitzObjectiveTurnMonitor.cs          |   61 -
 .../Arcade/TurnMonitors/WildlifeBlitzObjectiveTurnMonitor.cs.meta     |   11 -
 Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs         |  203 ---
 Assets/_Scripts/Controller/Arcade/WildlifeBlitzScoreKeeper.cs.meta    |   11 -
 Assets/_Scripts/Data/Enums/CallToActionTargetType.cs                  |    2 +-
 Assets/_Scripts/Data/Enums/GameModes.cs                               |   14 +-
 Assets/_Scripts/UI/UGSStatsManager.cs                                 |   11 -
 Assets/_Scripts/UI/WildlifeBlitzHUD.cs                                |   98 --
 Assets/_Scripts/UI/WildlifeBlitzHUD.cs.meta                           |    3 -
 Assets/_Scripts/UI/WildlifeBlitzStatsProvider.cs                      |   38 -
 Assets/_Scripts/UI/WildlifeBlitzStatsProvider.cs.meta                 |    3 -
 Assets/_Scripts/Utility/DataContainers/WildlifeBlitzStats.cs          |   21 -
 Assets/_Scripts/Utility/DataContainers/WildlifeBlitzStats.cs.meta     |    3 -
 Docs/SCENES.md                                                        |   42 +-
 Docs/SettingsSystem/ARCHITECTURE.md                                   |   17 +-
 Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md                          |   18 +-
 ProjectSettings/EditorBuildSettings.asset                             |    3 -
 36 files changed, 51 insertions(+), 3110 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 914 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs
deleted file mode 100644
index 874d1c287..000000000
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerWildlifeBlitzController.cs
+++ /dev/null
@@ -1,76 +0,0 @@
-using UnityEngine;
-using CosmicShore.Data;
-
-namespace CosmicShore.Gameplay
-{
-    /// <summary>
-    /// Wildlife Blitz on the networked single-host model: co-op clear-the-cell, playable
-    /// by a party of one (host) or up to three (AI teammates backfill via
-    /// ServerPlayerVesselInitializerWithAI). The TEAM's summed blitz composite (fed
-    /// server-side by WildlifeBlitzScoreKeeper) races the cell's CellEndGameScore
-    /// (published by WildlifeBlitzObjectiveTurnMonitor); the loss clock is the scene's
-    /// NetworkTimeBasedTurnMonitor. End-game runs the base SyncFinalResults template:
-    /// objective met → the co-op domain wins with the shared clear time; clock expired →
-    /// a Blue no-winner DNF end.
-    /// </summary>
-    public class MultiplayerWildlifeBlitzController : MultiplayerDomainGamesController
-    {
-        [Header("Blitz")]
-        [Tooltip("The server-authoritative blitz composite keeper (kills/crystals/volume → Score).")]
-        [SerializeField] WildlifeBlitzScoreKeeper scoreTracker;
-
-        protected override bool UseGolfRules => true;
-        protected override bool UseSceneReloadForReplay => true; // lifeforms/crystals don't reset in place
-
-        // End-game runs through OnTurnEndedCustom → the base SyncFinalResults template;
-        // suppress the base turn→round→game flow so we don't get a duplicate
-        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
-        protected override bool HasEndGame => false;
-
-        public override void OnNetworkSpawn()
-        {
-            base.OnNetworkSpawn();
-            numberOfRounds = 1;
-            numberOfTurnsPerRound = 1;
-
-            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
-            gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
-        }
-
-        public override void OnNetworkDespawn()
-        {
-            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
-            gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
-            base.OnNetworkDespawn();
-        }
-
-        void HandleTurnStarted()
-        {
-            if (scoreTracker) scoreTracker.StartTracking();
-        }
-
-        void HandleTurnEnded()
-        {
-            if (scoreTracker) scoreTracker.StopTracking();
-        }
-
-        /// <summary>
-        /// Server-side end detection. The turn ends either because the objective monitor
-        /// saw the team sum reach the target (WIN - the whole team shares the clear time)
-        /// or because the time monitor expired (LOSS - a Blue no-winner DNF). Score
-        /// assignment, roster snapshot, and the canonical results tail are owned by the
-        /// base SyncFinalResults template.
-        /// </summary>
-        protected override void OnTurnEndedCustom()
-        {
-            base.OnTurnEndedCustom();
-            if (!IsServer || FinalResultsSent) return;
-
-            bool cleared = rule.IsObjectiveReached(gameData, out var winningDomain);
-            if (cleared)
-                SyncFinalResults(winningDomain, Time.time - gameData.TurnStartTime);
-            else
-                SyncFinalResults(Domains.Blue, 0f);
-        }
```

</details>

### `8b8c12df4` — refactor(purge): erase retired mode ids; benchmark gets its own GameMode; BlitzStats out of the cloud profile

_Claude, 2026-07-23 03:24:32 +0000_

```text
No tombstone enum members for deleted content (owner: no temp fixes keeping
obsolete blitz/duel things):
- GameModes: members CellularDuel(8), WildlifeBlitz(26),
  MultiplayerFreestyle(28), MultiplayerCellularDuel(29),
  MultiplayerWildlifeBlitzGame(32) DELETED (terse do-not-reuse comments
  remain); new honest Benchmark(39) added for the stress-test context.
- BenchmarkSceneLauncher sets GameModes.Benchmark - every mode-keyed
  consumer (presence display, connecting panel, comeback switch, HUD
  objective factory, AI vessel fallback) has a safe default arm, so no
  behavior depended on the borrowed blitz id.
- CTA targets 410/427/428/429/430 deleted (no CTA asset used them).
- Cloud profile: PlayerStatsProfile.BlitzStats + WildlifeBlitzPlayerStatsProfile
  deleted; PlayerStatsRepository null-heal + UGSStatsManager blitz
  high-score branch + LogControlWindow debug block removed (JsonUtility
  ignores the stale BlitzStats key in existing saves).
- Dormant blitz content purged with the mode: SO_TrainingGame_WildLifeBlitz
  (pruned from TrainingGames + SO_Class_Sparrow, incl. a duplicate entry)
  and GameModeQuest_WildlifeBlitz (pruned from GameModeQuestList).
- EnumIntegrityTests: retired-member pins removed; count assertion now
  matches the real member count (the old 33 was stale drift - the enum had
  37 members before this commit).
```

```text
 Assets/_SO_Assets/Classes/SO_Class_Sparrow.asset                      |  4 +--
 Assets/_SO_Assets/GameModeQuest/GameModeQuestList.asset               |  1 -
 Assets/_SO_Assets/GameModeQuest/GameModeQuest_WildlifeBlitz.asset     | 29 -----------------
 .../_SO_Assets/GameModeQuest/GameModeQuest_WildlifeBlitz.asset.meta   |  8 -----
 Assets/_SO_Assets/Games/GameLists/TrainingGames.asset                 |  1 -
 Assets/_SO_Assets/Games/Training/SO_TrainingGame_WildLifeBlitz.asset  | 55 ---------------------------------
 .../Games/Training/SO_TrainingGame_WildLifeBlitz.asset.meta           |  8 -----
 Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs         |  2 +-
 Assets/_Scripts/Data/Enums/CallToActionTargetType.cs                  | 10 +++---
 Assets/_Scripts/Data/Enums/GameModes.cs                               | 31 +++++++------------
 .../_Scripts/System/CloudData/Repositories/PlayerStatsRepository.cs   |  1 -
 Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs                  |  6 ----
 Assets/_Scripts/UI/PlayerStatsProfile.cs                              |  1 -
 Assets/_Scripts/UI/UGSStatsManager.cs                                 |  5 ---
 Assets/_Scripts/UI/WildlifeBlitzPlayerStatsProfile.cs                 | 26 ----------------
 Assets/_Scripts/UI/WildlifeBlitzPlayerStatsProfile.cs.meta            |  3 --
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs                     |  6 ----
 Docs/SCENES.md                                                        |  8 ++---
 Docs/SettingsSystem/ARCHITECTURE.md                                   |  2 +-
 19 files changed, 23 insertions(+), 184 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 260 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs b/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs
index 3390d613b..f19b661ce 100644
--- a/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs
+++ b/Assets/_Scripts/Controller/Settings/BenchmarkSceneLauncher.cs
@@ -39,7 +39,7 @@ namespace CosmicShore.Core
                 : 3;
 
             gameData.SceneName = BenchmarkSceneName;
-            gameData.GameMode = GameModes.WildlifeBlitz; // ecosystem mode → AI seeks crystals
+            gameData.GameMode = GameModes.Benchmark;
             gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
             gameData.SelectedIntensity.Value = intensity;
             gameData.RequestedDomainCount = 3; // deterministic Jade/Ruby/Gold AI spread (don't inherit the last game's)
diff --git a/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs b/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs
index 80a29a4ee..38195fa1d 100644
--- a/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs
+++ b/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs
@@ -32,7 +32,7 @@ namespace CosmicShore.Data
         PlayGameBlockBandit = 407,
         PlayGameShootingGallery = 408,
         // 409 (PlayGameFreestyle) retired with the standalone arcade Freestyle game.
-        PlayGameCellularDuel = 410, // retired with Cellular Duel (2026-07-21) - do not reuse
+        // 410 (PlayGameCellularDuel) retired with Cellular Duel - do not reuse.
         PlayGameCellularBrawl = 411,
         PlayGameDashAndGrab = 412,
         PlayGameRiskyDriftness = 413,
@@ -49,10 +49,10 @@ namespace CosmicShore.Data
         PlayGameSidewinder = 424,
         PlayGameMultipass = 425,
         PlayGameMazeRunner = 426,
-        PlayGameWildlifeBlitz = 427, // retired with Wildlife Blitz (2026-07-21) - do not reuse
-        PlayGameMultiplayerFreestyle = 428, // retired with the standalone freestyle game (2026-07-21) - do not reuse
-        PlayGameMultiplayerDuelForCell = 429, // retired with Cellular Duel (2026-07-21) - do not reuse
-        PlayGameMultiplayerWildlifeBlitzGame = 430,
+        // 427 (PlayGameWildlifeBlitz) retired with Wildlife Blitz - do not reuse.
+        // 428 (PlayGameMultiplayerFreestyle) retired with the standalone freestyle game - do not reuse.
+        // 429 (PlayGameMultiplayerDuelForCell) retired with Cellular Duel - do not reuse.
+        // 430 (PlayGameMultiplayerWildlifeBlitzGame) retired with the co-op blitz stack - do not reuse.
         PlayGameHexRace = 431,
         PlayGameMultiplayer2v2CoOpVsAI = 432,
         PlayGameMultiplayerCrystalCapture = 433,
diff --git a/Assets/_Scripts/Data/Enums/GameModes.cs b/Assets/_Scripts/Data/Enums/GameModes.cs
index 71929dfae..03d983cb9 100644
--- a/Assets/_Scripts/Data/Enums/GameModes.cs
+++ b/Assets/_Scripts/Data/Enums/GameModes.cs
@@ -20,7 +20,7 @@ namespace CosmicShore.Data
         // 7 (Freestyle) retired: the standalone arcade Freestyle game was removed.
         // Freestyle now refers to the Menu_Main lava-lamp experience (see CLAUDE.md,
         // "Lava-Lamp Mode"). Do not reuse ID 7.
-        CellularDuel = 8,
+        // 8 (CellularDuel) retired 2026-07-21 with the Cellular Duel deletion - do not reuse.
         DashNGrab = 9,
         CellularBrawl = 10,
         Denial = 11,
@@ -38,27 +38,13 @@ namespace CosmicShore.Data
         BotDuel = 23,
         Curvatious = 24,
         MazeRunner = 25,
-        // 26 retired as a PLAYABLE mode 2026-07-21: the co-op blitz scene/controller/
-        // card were deleted. The member stays (serialized-int stability, dormant
-        // training/quest assets) and BenchmarkSceneLauncher still sets it as the
-        // benchmark's GameMode so AI crystal-seeking behavior keys correctly.
-        // Do not reuse.
-        WildlifeBlitz = 26,
+        // 26 (WildlifeBlitz) retired 2026-07-21 with the Wildlife Blitz deletion - do not reuse.
         ProtectMission = 27,
-        // 28 retired 2026-07-21: the standalone MultiplayerFreestyle sandbox game
-        // (scene + controller + card) was deleted - freestyle IS the Menu_Main
-        // lava lamp now (see CLAUDE.md "Lava-Lamp Mode"). Member kept for
-        // serialized-int stability; do not reuse.
-        MultiplayerFreestyle = 28,
-        // 29 retired 2026-07-21: Cellular Duel deleted outright (scene + controller +
-        // rule + card). Member kept for serialized-int stability; do not reuse.
-        MultiplayerCellularDuel = 29,
+        // 28 (MultiplayerFreestyle) retired 2026-07-21 - freestyle IS the Menu_Main lava lamp. Do not reuse.
+        // 29 (MultiplayerCellularDuel) retired 2026-07-21 with the Cellular Duel deletion - do not reuse.
         Multiplayer2v2CoOpVsAI = 30,
-        // 32 retired 2026-07-20: the separate co-op blitz stack (scene + card)
-        // was deleted; the mode-26 blitz it folded into was itself retired
-        // 2026-07-21. Member kept for serialized-int stability; do not reuse.
```

</details>

### `3353bcfcc` — refactor(purge): retire the unreachable 2v2CoOpVsAI stack; LaunchPartyAllGames deleted, holders rewired to OrganicRematchGames

_Claude, 2026-07-23 03:26:00 +0000_

```text
- 2v2CoOpVsAI (mode 30) was unreachable content: its card was referenced by
  nothing, so players could not launch it, yet its scene shipped enabled in
  the build. Deleted: scene + build entry + card + the duel-family UI that
  survived C9 only through it (DuelForCellScoreboard, Duel Cell Stats Panel
  prefab family + its 2 UI controllers) + the orphaned CoOpScoreBoard.
  GameModes member 30 + CTA 432 deleted; EnumIntegrityTests updated
  (32 members). Every mode now uses the base Scoreboard.
- LaunchPartyAllGames.asset deleted (it was an empty list after the blitz
  retirement); all 6 holders rewired to OrganicRematchGames (Menu_Main x4,
  LoadoutCard, Minigames Screen, Arcade Screen + Backup, migration
  ArcadeScreen) - the launch-party/minigame surfaces now show the four live
  cards (HexRace, Joust, Crystal Capture, Maelstrom) instead of rendering
  empty.
- Build settings: 12 well-formed entries. Docs + test checklist updated.
```

```text
 .../Duel Cell Stats Panel - Player and Round Row.prefab               | 2234 --------------
 .../Duel Cell Stats Panel - Player and Round Row.prefab.meta          |    7 -
 .../Panels/Duel Cell Stats Panel/Duel Cell Stats Panel.prefab         | 3867 ------------------------
 .../Panels/Duel Cell Stats Panel/Duel Cell Stats Panel.prefab.meta    |    7 -
 .../Panels/Duel Cell Stats Panel/Panel Heading - Stats Cell.prefab    |  214 --
 .../Duel Cell Stats Panel/Panel Heading - Stats Cell.prefab.meta      |    7 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayer2v2CoOpVsAI.asset        |   39 -
 Assets/_SO_Assets/Games/ArcadeGameMultiplayer2v2CoOpVsAI.asset.meta   |    8 -
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset           |   15 -
 Assets/_SO_Assets/Games/GameLists/LaunchPartyAllGames.asset.meta      |    8 -
 Assets/_Scenes/Menu_Main.unity                                        |    8 +-
 .../_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity | 5043 -------------------------------
 .../Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity.meta    |    7 -
 Assets/_Scripts/Data/Enums/CallToActionTargetType.cs                  |    2 +-
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    2 +-
 Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs                  |    4 +-
 Assets/_Scripts/UI/CoOpScoreBoard.cs                                  |   11 -
 Assets/_Scripts/UI/CoOpScoreBoard.cs.meta                             |    3 -
 Assets/_Scripts/UI/DuelCellStatsRoundUIController.cs                  |  344 ---
 Assets/_Scripts/UI/DuelCellStatsRoundUIController.cs.meta             |    3 -
 Assets/_Scripts/UI/DuelForCellScoreboard.cs                           |   26 -
 Assets/_Scripts/UI/DuelForCellScoreboard.cs.meta                      |    3 -
 Assets/_Scripts/UI/DuellCellStatsRowUIController.cs                   |   83 -
 Assets/_Scripts/UI/DuellCellStatsRowUIController.cs.meta              |    3 -
 Docs/SCENES.md                                                        |    3 +-
 Docs/ScoringSystem/ARCHITECTURE.md                                    |    3 +-
 Docs/ScoringSystem/REFACTOR.md                                        |    2 +-
 Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md                          |    2 +-
 ProjectSettings/EditorBuildSettings.asset                             |    3 -
 37 files changed, 27 insertions(+), 14524 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 612 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs b/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs
index 38195fa1d..0b8b32ff8 100644
--- a/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs
+++ b/Assets/_Scripts/Data/Enums/CallToActionTargetType.cs
@@ -54,7 +54,7 @@ namespace CosmicShore.Data
         // 429 (PlayGameMultiplayerDuelForCell) retired with Cellular Duel - do not reuse.
         // 430 (PlayGameMultiplayerWildlifeBlitzGame) retired with the co-op blitz stack - do not reuse.
         PlayGameHexRace = 431,
-        PlayGameMultiplayer2v2CoOpVsAI = 432,
+        // 432 (PlayGameMultiplayer2v2CoOpVsAI) retired with the 2v2 stack - do not reuse.
         PlayGameMultiplayerCrystalCapture = 433,
         PlayGameMultiplayerJoust = 434,
         PlayGameBotDuel = 435,
diff --git a/Assets/_Scripts/Data/Enums/GameModes.cs b/Assets/_Scripts/Data/Enums/GameModes.cs
index 03d983cb9..b7d780ac9 100644
--- a/Assets/_Scripts/Data/Enums/GameModes.cs
+++ b/Assets/_Scripts/Data/Enums/GameModes.cs
@@ -42,7 +42,7 @@ namespace CosmicShore.Data
         ProtectMission = 27,
         // 28 (MultiplayerFreestyle) retired 2026-07-21 - freestyle IS the Menu_Main lava lamp. Do not reuse.
         // 29 (MultiplayerCellularDuel) retired 2026-07-21 with the Cellular Duel deletion - do not reuse.
-        Multiplayer2v2CoOpVsAI = 30,
+        // 30 (Multiplayer2v2CoOpVsAI) retired 2026-07-21 - unreachable content deleted. Do not reuse.
         // 31 stays reserved - never assigned.
         // 32 (MultiplayerWildlifeBlitzGame) retired 2026-07-20 with the co-op blitz stack - do not reuse.
         HexRace = 33,
diff --git a/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs b/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs
index ce5f60b62..516508524 100644
--- a/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs
+++ b/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs
@@ -136,7 +136,7 @@ namespace CosmicShore.Tests
         public void GameModes_HasExpectedMemberCount()
         {
             var values = Enum.GetValues(typeof(GameModes));
-            Assert.AreEqual(33, values.Length,
+            Assert.AreEqual(32, values.Length,
                 "GameModes member count changed. Update tests if a game mode was added/removed.");
         }
 
@@ -150,7 +150,6 @@ namespace CosmicShore.Tests
 
         [Test]
         [TestCase(GameModes.Random, 0)]
-        [TestCase(GameModes.Multiplayer2v2CoOpVsAI, 30)]
         [TestCase(GameModes.HexRace, 33)]
         [TestCase(GameModes.MultiplayerJoust, 34)]
         [TestCase(GameModes.MultiplayerCrystalCapture, 35)]
@@ -176,7 +175,6 @@ namespace CosmicShore.Tests
             // Convention check: multiplayer modes should be identifiable by name.
             var multiplayerModes = new[]
             {
-                GameModes.Multiplayer2v2CoOpVsAI,
                 GameModes.MultiplayerJoust,
                 GameModes.MultiplayerCrystalCapture
             };
diff --git a/Assets/_Scripts/UI/CoOpScoreBoard.cs b/Assets/_Scripts/UI/CoOpScoreBoard.cs
deleted file mode 100644
index 581cef263..000000000
--- a/Assets/_Scripts/UI/CoOpScoreBoard.cs
+++ /dev/null
@@ -1,11 +0,0 @@
-using CosmicShore.Gameplay;
-using TMPro;
-using UnityEngine;
-using CosmicShore.UI;
-namespace CosmicShore.UI
-{
-    public class CoOpScoreBoard : Scoreboard
-    {
-        [SerializeField] TMP_Text OppponentScoreTextField;
-    }
-}
\ No newline at end of file
diff --git a/Assets/_Scripts/UI/DuelCellStatsRoundUIController.cs b/Assets/_Scripts/UI/DuelCellStatsRoundUIController.cs
deleted file mode 100644
index b0b37f5f7..000000000
--- a/Assets/_Scripts/UI/DuelCellStatsRoundUIController.cs
+++ /dev/null
@@ -1,344 +0,0 @@
-using System;
```

</details>

### `63aecc53c` — feat(progression): mount GameModeProgressionService in Bootstrap - the quest chain comes alive (Y0.1)

_Claude, 2026-07-23 23:39:34 +0000_

```text
Adds the service as the 5th component on Bootstrap's PlayerDataService GO
(mirroring the co-located ParticipationXpAwarder pattern): questList ->
GameModeQuestList.asset, gameData -> Runtime GameData.asset,
progressionConfig left null (code lazily creates a default). The service
was fully implemented but never scene-mounted - its 6 null-guarded runtime
consumers (QuestTrackView unlock/claim chain, intensity gating,
quest-complete toast) silently degraded to only-quest-0-unlocked.

Then deletes the orphaned 'MIgration_Prefabs (DELETE LATER)' folder
(9 prefabs; re-verified zero external refs by guid grep - the mounted
service block was copied from its PlayerDataService.prefab first).

In-editor verification: quest track unlocks beyond quest 0, intensity
gating works, quest-complete toast fires after finishing a quest's game.
```

```text
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER).meta                 |     8 -
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ArcadeScreen.prefab  | 14021 --------
 .../MIgration_Prefabs (DELETE LATER)/ArcadeScreen.prefab.meta         |     7 -
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/IAPManager.prefab    |    47 -
 .../_Prefabs/MIgration_Prefabs (DELETE LATER)/IAPManager.prefab.meta  |     7 -
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/ModalWindows.prefab  | 53417 ------------------------------
 .../MIgration_Prefabs (DELETE LATER)/ModalWindows.prefab.meta         |     7 -
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/NavBar.prefab        |  1805 -
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/NavBar.prefab.meta   |     7 -
 .../MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab         |    63 -
 .../MIgration_Prefabs (DELETE LATER)/PlayerDataService.prefab.meta    |     7 -
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/Screens.prefab       | 42972 ------------------------
 Assets/_Prefabs/MIgration_Prefabs (DELETE LATER)/Screens.prefab.meta  |     7 -
 .../ToastNotificationContainer.prefab                                 |    64 -
 .../ToastNotificationContainer.prefab.meta                            |     7 -
 .../MIgration_Prefabs (DELETE LATER)/ToastNotificationManager.prefab  |    51 -
 .../ToastNotificationManager.prefab.meta                              |     7 -
 .../_Prefabs/MIgration_Prefabs (DELETE LATER)/UGSStatsManager.prefab  |    47 -
 .../MIgration_Prefabs (DELETE LATER)/UGSStatsManager.prefab.meta      |     7 -
 Assets/_Scenes/Bootstrap.unity                                        |    16 +
 20 files changed, 16 insertions(+), 112558 deletions(-)
```

### `edca3fce4` — fix(joust): server-side elapsed-time centerline feed; legacy NetworkScoreTracker de-wired (Y0.2a)

_Claude, 2026-07-23 23:41:02 +0000_

```text
The legacy tracker fired a duplicate SortRoundStats + InvokeWinnerCalculated
500ms after the authoritative SyncFinalResults RPC, and its Joust instance
had golfRules:0 on a golf mode. Its one live contribution - the mid-turn
elapsed-time centerline - is replaced by an in-controller server-only
0.25s UniTask loop (destroy-linked CTS, IsTurnRunning-guarded, live
RoundStatsList re-read per tick, stops once FinalResultsSent) writing
Time.time - gameData.TurnStartTime into every RoundStats.Score - the exact
winner-finishTime expression OnTurnEndedCustom uses. Server writes are the
load-bearing path: a spawned peer's local Score write does not raise
OnScoreChanged; only server writes replicate via n_Score to every HUD.

Scene edit is a pure deletion of the tracker component block (verified: no
serialized refs to its fileID beyond the GO component list; UGS reporting
is independent via JoustStatsReporter on OnMiniGameEnd). NB-index shift on
the Game NetworkObject is safe same-build - do not mix builds across peers.

MPPM verification: mid-turn centerline ticks on host + client, identical
final results, exactly ONE winner event (watch EndGameSequencer).
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity  | 19 ----------------
 Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs | 46 +++++++++++++++++++++++++++++++++++++++
 2 files changed, 46 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
index d35a4d7b2..59ff0bcb6 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerJoustController.cs
@@ -1,5 +1,7 @@
 // MultiplayerJoustController.cs
 using System.Linq;
+using System.Threading;
+using Cysharp.Threading.Tasks;
 using UnityEngine;
 using CosmicShore.Utility;
 using CosmicShore.Data;
@@ -17,11 +19,55 @@ namespace CosmicShore.Gameplay
         // InvokeMiniGameEnd from SyncGameEnd_ClientRpc.
         protected override bool HasEndGame => false;
 
+        // Mid-turn centerline feed (replaces the legacy NetworkScoreTracker's
+        // TimePlayedScoring): a spawned peer's LOCAL Score write does not raise
+        // OnScoreChanged - only a SERVER write replicates via n_Score and reaches every
+        // peer's HUD - so the elapsed-time tick must be a server-side write (the same
+        // expression the winner's finishTime uses in OnTurnEndedCustom).
+        CancellationTokenSource _scoreFeedCts;
+
         public override void OnNetworkSpawn()
         {
             base.OnNetworkSpawn();
             numberOfRounds = 1;
             numberOfTurnsPerRound = 1;
+
+            if (IsServer)
+            {
+                _scoreFeedCts = CancellationTokenSource.CreateLinkedTokenSource(
+                    this.GetCancellationTokenOnDestroy());
+                RunScoreFeedAsync(_scoreFeedCts.Token).Forget();
+            }
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            _scoreFeedCts?.Cancel();
+            _scoreFeedCts?.Dispose();
+            _scoreFeedCts = null;
+            base.OnNetworkDespawn();
+        }
+
+        async UniTaskVoid RunScoreFeedAsync(CancellationToken token)
+        {
+            while (!token.IsCancellationRequested)
+            {
+                if (gameData.IsTurnRunning && !FinalResultsSent)
+                {
+                    float elapsed = Time.time - gameData.TurnStartTime;
+                    // Re-read the live roster each tick - no cached stats refs (B15).
+                    var statsList = gameData.RoundStatsList;
+                    for (int i = 0; i < statsList.Count; i++)
+                    {
+                        var stats = statsList[i];
+                        if (stats != null)
+                            stats.Score = elapsed;
+                    }
+                }
+
+                await UniTask.Delay(250, DelayType.UnscaledDeltaTime, cancellationToken: token)
+                    .SuppressCancellationThrow();
+            }
         }
 
         // ── Server-authoritative game end ─────────────────────────────────
```

</details>

### `a08cf5ca0` — fix(crystal-capture): server-side crystal centerline feed; legacy NetworkScoreTracker de-wired (Y0.2b)

_Claude, 2026-07-23 23:42:11 +0000_

```text
Same defect as Joust: the legacy tracker fired a duplicate SortRoundStats +
InvokeWinnerCalculated 500ms after the authoritative SyncFinalResults RPC.
Its mid-turn centerline is replaced by a server-only per-stats
OnCrystalsCollectedChanged subscription writing rule.LiveMetric(stats) into
Score (replicates via n_Score). B15 lifecycle: turn-start roster snapshot
(server roster complete pre-turn; Contains guard makes re-subscription a
no-op), teardown off the controller's OWN record list in OnNetworkDespawn +
OnDestroy - never by iterating RoundStatsList (precedent:
NetworkCrystalCollisionTurnMonitor).

Scene edit is a pure deletion of the tracker component block. UGS reporting
unaffected (CrystalCaptureStatsReporter reads post-RPC values on
OnMiniGameEnd).

MPPM verification: centerline ticks as crystals are collected on host +
client, identical final results, exactly ONE winner event.
```

```text
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  | 19 -----------
 .../_Scripts/Controller/Arcade/MultiplayerCrystalCaptureController.cs | 56 +++++++++++++++++++++++++++++++++
 2 files changed, 56 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerCrystalCaptureController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerCrystalCaptureController.cs
index 19d02ad8a..74dbbfaa2 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerCrystalCaptureController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerCrystalCaptureController.cs
@@ -1,3 +1,4 @@
+using System.Collections.Generic;
 using CosmicShore.Data;
 
 namespace CosmicShore.Gameplay
@@ -13,11 +14,66 @@ namespace CosmicShore.Gameplay
         // a duplicate InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
         protected override bool HasEndGame => false;
 
+        // Mid-turn centerline feed (replaces the legacy NetworkScoreTracker's
+        // CrystalsCollectedScoring): server-only per-stats subscription writing the rule
+        // metric into Score - a spawned peer's local write does not raise OnScoreChanged;
+        // only server writes replicate via n_Score to every HUD. B15: detach from THIS
+        // record list only (never by iterating gameData.RoundStatsList at teardown), with
+        // OnNetworkDespawn + OnDestroy nets (precedent: NetworkCrystalCollisionTurnMonitor).
+        readonly List<IRoundStats> _scoreFeedStats = new();
+
         public override void OnNetworkSpawn()
         {
             base.OnNetworkSpawn();
             numberOfRounds = 1;
             numberOfTurnsPerRound = 1;
+
+            if (IsServer)
+                gameData.OnMiniGameTurnStarted.OnRaised += StartScoreFeed;
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            if (IsServer)
+                gameData.OnMiniGameTurnStarted.OnRaised -= StartScoreFeed;
+            StopScoreFeed();
+            base.OnNetworkDespawn();
+        }
+
+        public override void OnDestroy()
+        {
+            StopScoreFeed(); // B15: destruction paths that bypass despawn must still detach
+            base.OnDestroy();
+        }
+
+        /// <summary>
+        /// Turn-start roster snapshot suffices: the server roster is complete before any
+        /// turn starts, and the Contains guard makes re-subscription a no-op.
+        /// </summary>
+        void StartScoreFeed()
+        {
+            foreach (var stats in gameData.RoundStatsList)
+            {
+                if (stats == null || _scoreFeedStats.Contains(stats)) continue;
+                stats.OnCrystalsCollectedChanged += FeedScore;
+                _scoreFeedStats.Add(stats);
+            }
+        }
+
+        void StopScoreFeed()
+        {
+            foreach (var stats in _scoreFeedStats)
+            {
+                if (stats == null) continue;
+                stats.OnCrystalsCollectedChanged -= FeedScore;
+            }
+            _scoreFeedStats.Clear();
+        }
+
+        void FeedScore(IRoundStats stats)
+        {
+            if (FinalResultsSent) return;
+            stats.Score = rule.LiveMetric(stats);
         }
 
         // ── Server-authoritative game end ─────────────────────────────────
```

</details>

### `0bc1b52b7` — chore(scoring): delete NetworkScoreTracker - scene-less after the Joust/CC de-wire (Y0.2c)

_Claude, 2026-07-23 23:44:08 +0000_

```text
Zero content refs and zero code refs beyond historical comments. The rest
of the legacy BaseScoreTracker family stays: HexRaceScoreTracker (live)
and the offline ScoreTracker (Recording Studio tool scenes) still extend
it, its ScoringModes factory keeps the strategy classes referenced, and
UniversalStatsProvider's fate is gated on D20. De-basing HexRaceScoreTracker
is the next unlock if the family is to go entirely.
```

```text
 Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs      | 49 -----------------------------------------
 Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs.meta |  3 ---
 2 files changed, 52 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs b/Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs
deleted file mode 100644
index dbfe9b4e0..000000000
--- a/Assets/_Scripts/Controller/Arcade/NetworkScoreTracker.cs
+++ /dev/null
@@ -1,49 +0,0 @@
-using Cysharp.Threading.Tasks;
-using Unity.Netcode;
-
-namespace CosmicShore.Gameplay
-{
-    public class NetworkScoreTracker : BaseScoreTracker
-    {
-        public override void OnNetworkSpawn()
-        {
-            if (!IsServer)
-                return;
-
-            gameData.OnInitializeGame.OnRaised += InitializeScoringMode;
-            gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
-            gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
-            gameData.OnMiniGameEnd.OnRaised += CalculateWinnerOnServer;
-            OnClickToMainMenu.OnRaised += OnTurnEnded;
-        }
-
-        public override void OnNetworkDespawn()
-        {
-            if (!IsServer)
-                return;
-
-            gameData.OnInitializeGame.OnRaised -= InitializeScoringMode;
-            gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
-            gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
-            gameData.OnMiniGameEnd.OnRaised -= CalculateWinnerOnServer;
-            OnClickToMainMenu.OnRaised -= OnTurnEnded;
-        }
-
-        private void CalculateWinnerOnServer()
-        {
-            DelayAndSendResults().Forget(); // fire and forget async call
-        }
-
-        private async UniTaskVoid DelayAndSendResults()
-        {
-            await UniTask.Delay(500); // waits for 0.5 seconds (500ms)
-            SendRoundStats_ClientRpc();
-        }
-
-        [ClientRpc]
-        private void SendRoundStats_ClientRpc()
-        {
-            SortAndInvokeResults();
-        }
-    }
-}
\ No newline at end of file
```

</details>

### `3841bfbb2` — fix(input): touch respects Invert Y / Invert Throttle (Y0.3)

_Claude, 2026-07-23 23:45:03 +0000_

```text
TouchInputStrategy.Reparameterize was the one strategy missing the
post-calc inversion block - Gamepad, Keyboard, and DualMouse all apply
InvertYEnabled/InvertThrottleEnabled after their sum/diff math. Mirrors
the gamepad block verbatim. Y3's input-pipeline template will later own
this in the base by construction.
```

```text
 Assets/_Scripts/Controller/IO/TouchInputStrategy.cs | 13 +++++++++++++
 1 file changed, 13 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs b/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
index 64a73d442..ede4eaf38 100644
--- a/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
@@ -301,6 +301,19 @@ namespace CosmicShore.Gameplay
             inputStatus.YSum = -Ease(rightNormalizedJoystickPosition.y + leftNormalizedJoystickPosition.y);
             inputStatus.XDiff = (rightNormalizedJoystickPosition.x - leftNormalizedJoystickPosition.x + 2) / 4;
             inputStatus.YDiff = Ease(rightNormalizedJoystickPosition.y - leftNormalizedJoystickPosition.y);
+
+            // Apply inversions AFTER calculations (parity with Gamepad/Keyboard/DualMouse -
+            // touch was the one strategy missing the settings' Invert Y / Invert Throttle).
+            if (inputStatus.InvertYEnabled)
+            {
+                inputStatus.YSum *= -1f;   // Invert pitch
+                inputStatus.YDiff *= -1f;  // Invert roll
+            }
+
+            if (inputStatus.InvertThrottleEnabled)
+            {
+                inputStatus.XDiff = 1f - inputStatus.XDiff;  // Invert throttle/speed
+            }
         }
 
         private void PerformSpeedAndDirectionalEffects()
```

</details>

### `a20a25483` — docs(unified-systems): record Y0 executed; add Y0 verification steps to the checklist

_Claude, 2026-07-23 23:45:03 +0000_

```text
 Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md | 11 +++++++++++
 Docs/UnifiedSystems/YASH.md                  | 14 +++++++++++++-
 2 files changed, 24 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
index 9fad002ac..da44e5fcb 100644
--- a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
+++ b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
@@ -21,6 +21,17 @@ after the step was written; do not test).
 - [x] **5.** Lava-lamp regression (C8): autopilot vessel drifts behind UI → tap crystal → control + Game UI + vessel HUD. Toys all work (vessel changer keeps domain/speed + HUD re-shows, domain changer, painting, Wanderway). Gamepad **Start** exits; center-tap returns to menu. *(verified 2026-07-21)*
 - [x] **6.** Arcade grid contents (C6, C8, C9, C10, C11): the only cards anywhere are HexRace, Joust, Crystal Capture, Maelstrom — every game-list surface now binds OrganicRematchGames (LaunchPartyAllGames was deleted and its holders rewired; the 2v2 card/scene are gone). *(verified 2026-07-21 pre-C9 — re-check the surfaces show the four live cards)*
 
+## Part A2 — Y0 wire-fix wave (W1-W5, 2026-07-21)
+
+- [ ] **25.** Quest chain alive (Y0.1): Arcade → quest track — quests unlock beyond quest 0,
+  intensity gating works, completing a quest's game fires the quest-complete toast.
+- [ ] **26.** Joust + CC centerline feeds (Y0.2): covered inside steps 14/15/21 — mid-turn the
+  centerline score ticks on BOTH peers (Joust: elapsed time; CC: crystals), final results
+  identical host vs client, exactly ONE end sequence (the legacy tracker's duplicate winner
+  event is gone).
+- [ ] **27.** Touch inversion (Y0.3, touch device/simulator): Settings → Invert Y / Invert
+  Throttle now affect touch flight (pitch/roll flip; throttle flips).
+
 ## Part B — Solo runs (solo = party of one + AI)
 
 - ~~**7.** Cellular Duel solo~~ — **OBSOLETE**: Cellular Duel retired (C9, 2026-07-21). Do not test.
diff --git a/Docs/UnifiedSystems/YASH.md b/Docs/UnifiedSystems/YASH.md
index 5955ef6ac..878ea5ca2 100644
--- a/Docs/UnifiedSystems/YASH.md
+++ b/Docs/UnifiedSystems/YASH.md
@@ -14,7 +14,19 @@ perf branches).
 
 ---
 
-## Y0 — Wire-fix wave (bug-fix grade, all unblocked)
+## Y0 — Wire-fix wave — **EXECUTED 2026-07-21** (branch `claude/unified-yash-refactor-9sc0ws`)
+
+> **STATUS:** all three items shipped per the verified plans below. Y0.1: service mounted in
+> Bootstrap (questList + gameData wired, progressionConfig lazily defaulted), migration folder
+> deleted (re-verified zero external refs). Y0.2: in-controller SERVER feeds replace the legacy
+> tracker - Joust: 0.25s destroy-linked elapsed-time loop; CC: per-stats
+> OnCrystalsCollectedChanged -> rule.LiveMetric with B15 own-record teardown - then the tracker
+> blocks were deleted from both scenes and `NetworkScoreTracker` itself deleted (scene-less; the
+> C9-C12 retirements had removed its other 4 carrier scenes). Remaining family blockers:
+> HexRaceScoreTracker de-basing + Recording Studio's offline ScoreTracker + D20
+> (UniversalStatsProvider). Y0.3: inversion block added to TouchInputStrategy.Reparameterize.
+> In-editor verification steps live in `SOLO_RETIREMENT_TESTS.md` (steps 25-27).
+
 
 - **Y0.1 Mount `GameModeProgressionService`.** Add the component to the Bootstrap scene's
   PlayerDataService GameObject (copy serialized refs — `questList`/`progressionConfig`/`gameData`,
```

</details>

### `914a8d1c4` — docs(tests): add Part E post-merge verification steps (28-33)

_Claude, 2026-07-25 02:24:13 +0000_

```text
The bleeding-edge merge created seams that neither branch's own test steps
cover - a break there is a merge-integration bug, not a regression in either
branch. Adds six steps for those seams and refreshes two existing ones:

- 28: arcade grid is now SEVEN live cards (Rampage joined), still zero
  retired ones on any surface.
- 29: Rampage end-to-end, incl. an inspector check that the controller's
  Scoring slot still binds RampageScoringRule (the merge removed a field
  that shadowed the hoisted base one - a null there means the serialized
  binding did not survive).
- 30: Crystal Capture must show BOTH behaviours - my mid-turn crystal
  centerline feed AND their end-of-match finish-time golf score. Only one
  of the two showing up means the union resolution regressed.
- 31: game toasts replaced GameEventFeed (Ready, disconnect, Brood wave).
- 32: Bootstrap union - their loader rework AND my quest-service mount.
- 33: 13 build-settings scenes all resolve.

Step 6 re-opened for the new seven-card count; step 24's console watch
extended to scenes bleeding-edge touched, the other place a missing-script
can surface after a cross-branch merge.
```

```text
 Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md | 38 +++++++++++++++++++++++++++++++++++---
 1 file changed, 35 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
index da44e5fcb..7b6ad2cc8 100644
--- a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
+++ b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
@@ -2,7 +2,8 @@
 
 One consolidated in-editor/MPPM pass covering everything on this branch: Y1 scoring
 unification → C1–C8 (solo retirement, benchmark conversion, `IsMultiplayerMode` removal,
-dead-content deletion, standalone-freestyle retirement) → C9 (Cellular Duel retirement).
+dead-content deletion, standalone-freestyle retirement) → C9 (Cellular Duel retirement) →
+C10–C12 (Wildlife Blitz + 2v2 retirement) → Y0 wire-fix wave → M1–M3 (`bleeding-edge` merge).
 Steps are ordered to minimize scene churn. Keep the numbering — progress is tracked
 against it. (C10 retired Wildlife Blitz: steps 8-11 and 23 are struck.)
 
@@ -19,7 +20,7 @@ after the step was written; do not test).
 
 - [x] **4.** Boot → auth → Menu_Main: normal startup, host starts, no errors (C5 deleted the legacy matchmaking path in `MultiplayerSetup` — sign-in host start must be unaffected). *(verified 2026-07-21)*
 - [x] **5.** Lava-lamp regression (C8): autopilot vessel drifts behind UI → tap crystal → control + Game UI + vessel HUD. Toys all work (vessel changer keeps domain/speed + HUD re-shows, domain changer, painting, Wanderway). Gamepad **Start** exits; center-tap returns to menu. *(verified 2026-07-21)*
-- [x] **6.** Arcade grid contents (C6, C8, C9, C10, C11): the only cards anywhere are HexRace, Joust, Crystal Capture, Maelstrom — every game-list surface now binds OrganicRematchGames (LaunchPartyAllGames was deleted and its holders rewired; the 2v2 card/scene are gone). *(verified 2026-07-21 pre-C9 — re-check the surfaces show the four live cards)*
+- [x] **6.** Arcade grid contents (C6, C8, C9, C10, C11): every game-list surface now binds OrganicRematchGames (LaunchPartyAllGames was deleted and its holders rewired; the 2v2 card/scene are gone). *(verified 2026-07-21 pre-C9)* — **re-verify post-merge**: the live set is now **seven** cards (HexRace, Joust, Crystal Capture, Maelstrom, AstroLeague, NucleusRush, **Rampage**) and none of the retired ones. See step 28.
 
 ## Part A2 — Y0 wire-fix wave (W1-W5, 2026-07-21)
 
@@ -58,9 +59,40 @@ after the step was written; do not test).
 - ~~**22.** Duel 2-human~~ — **OBSOLETE**: Cellular Duel retired (C9, 2026-07-21). Do not test.
 - ~~**23.** Blitz 2-human co-op~~ — **OBSOLETE**: Wildlife Blitz retired (C10, 2026-07-21). Do not test.
 
+## Part E — Post-merge with `bleeding-edge` (M1–M3, 2026-07-25)
+
+The merge brought in Rampage, the game-toast system (which replaced `GameEventFeed`), and
+finish-time golf scoring for Crystal Capture. These steps cover the seams where the two
+branches met — a break here is a *merge-integration* bug, not a regression in either
+branch alone.
+
+- [ ] **28.** Arcade grid post-merge: **seven** live cards — HexRace, Joust, Crystal Capture,
+  Maelstrom, AstroLeague, NucleusRush, **Rampage**. None of Wildlife Blitz / Cellular Duel /
+  Freestyle / 2v2 reappears on ANY surface (grid, rematch list, quest track, CTA).
+- [ ] **29.** **Rampage** solo (new mode, ID 2): launches from the card, AI pilots hunt the
+  densest hostile-mass region (they should visibly converge on other domains' trails, not
+  wander), the match ends on the prism target, scoreboard shows **finish time** for the
+  winning domain and the remaining-prisms sentinel for the losers, exactly ONE end sequence,
+  Play Again reloads the scene. Watch the Rampage controller in the inspector: its **Scoring**
+  slot must hold `RampageScoringRule` (the merge removed a shadowing field — a null here means
+  the serialized binding didn't survive).
+- [ ] **30.** **Crystal Capture** post-merge (both behaviours must coexist): mid-turn the
+  centerline score ticks **crystals** on both peers (my Y0.2 server feed), and at the end the
+  scoreboard flips to **finish time** for the winner / remaining-crystals sentinel for the
+  losers (their golf scoring). Getting only one of the two = the union resolution regressed.
+- [ ] **31.** **Game toasts** replaced the old event feed: a toast appears on player-Ready and
+  on a mid-game disconnect, and on the Brood Rush wave-scored beat in NucleusRush. The old
+  bottom-of-screen `GameEventFeed` strip must be gone entirely (no empty UI object left behind).
+- [ ] **32.** Bootstrap union check (W1 vs their Bootstrap UI rework): boot completes, the new
+  heartbeat loader animates, AND the quest chain still works (step 25) — the
+  `GameModeProgressionService` component must still be mounted on the `PlayerDataService`
+  GameObject in `Bootstrap.unity`.
+- [ ] **33.** Build settings: File → Build Settings lists 13 scenes, all resolving (no
+  `<missing>` rows), including `MinigameRampage` and `BenchmarkStressTest`.
+
 ## Throughout
 
-- [ ] **24.** Console watch: no NREs, no `[Invalid Destroy]`, no missing-script/reference errors from deleted classes — especially in the benchmark scene, which was surgically decoupled from the deleted blitz stack (C10). (The one known pre-existing missing-script component lived in the duel scene — gone with C9; none should remain anywhere.)
+- [ ] **24.** Console watch: no NREs, no `[Invalid Destroy]`, no missing-script/reference errors from deleted classes — especially in the benchmark scene, which was surgically decoupled from the deleted blitz stack (C10), and in any scene bleeding-edge touched (the merge is the other place a "missing script" can appear: their asset referencing my deleted script, or mine referencing their deleted `GameFeedAPI`). (The one known pre-existing missing-script component lived in the duel scene — gone with C9; none should remain anywhere.)
 
 ---
 
```

</details>

### `fdda5a896` — fix(hangar): complete C6 - drop training wrappers orphaned by the card deletion

_Claude, 2026-07-25 02:39:37 +0000_

```text
C6 deleted the retired solo arcade cards but left the SO_TrainingGame_*
wrappers that pointed at them, so four wrappers deserialized with a null
Game. The path is live, not theoretical: two Menu_Main buttons call
HangarScreen.DisplayTrainingModal, whose "TrainingGames != null" guard
passes on a non-null list, and HangarTrainingModal.SelectGame then
dereferences SelectedGame.Game.DisplayName - an NRE on the Train button
for Manta, Dolphin, Serpent and Squirrel. Verified against the merge base:
all four wrapper targets existed there, so this is C6 fallout, not merge
fallout.

- Delete SO_TrainingGame_{BlockBandit,Darts,MazeRunner,SlipAndStride}: their
  entire content was a pointer to a card C6 removed.
- Prune them from SO_Class_{Manta,Dolphin,Serpent,Squirrel} (each listed its
  wrapper twice, so the modal's two buttons showed the same game anyway).
- Delete TrainingGames.asset: every element was one of the four, leaving an
  empty list with zero referrers - the same "empty list + its UI surface"
  cleanup applied in C11.

Also fixes the entry-point guard, which was wrong independently of the above:
SetTrainingGames indexes [0] and [1] unconditionally, so a null check never
protected it. Grizzly, Termite and Urchin already had empty lists at the merge
base and would have thrown ArgumentOutOfRangeException there too. The guard
now requires two games.

The surface stays wired rather than retired - bleeding-edge added
SO_TrainingGame_Rampage pointing at a live card, which reads as intent to
repopulate training with the surviving multiplayer modes. That wrapper is
left untouched; it is currently in no list.
```

```text
 Assets/_SO_Assets/Classes/SO_Class_Dolphin.asset                      |  4 +--
 Assets/_SO_Assets/Classes/SO_Class_Manta.asset                        |  4 +--
 Assets/_SO_Assets/Classes/SO_Class_Serpent.asset                      |  4 +--
 Assets/_SO_Assets/Classes/SO_Class_Squirrel.asset                     |  4 +--
 Assets/_SO_Assets/Games/GameLists/TrainingGames.asset                 | 19 ------------
 Assets/_SO_Assets/Games/GameLists/TrainingGames.asset.meta            |  8 -----
 Assets/_SO_Assets/Games/Training/SO_TrainingGame_BlockBandit.asset    | 55 ---------------------------------
 .../_SO_Assets/Games/Training/SO_TrainingGame_BlockBandit.asset.meta  |  8 -----
 Assets/_SO_Assets/Games/Training/SO_TrainingGame_Darts.asset          | 55 ---------------------------------
 Assets/_SO_Assets/Games/Training/SO_TrainingGame_Darts.asset.meta     |  8 -----
 Assets/_SO_Assets/Games/Training/SO_TrainingGame_MazeRunner.asset     | 55 ---------------------------------
 .../_SO_Assets/Games/Training/SO_TrainingGame_MazeRunner.asset.meta   |  8 -----
 Assets/_SO_Assets/Games/Training/SO_TrainingGame_SlipAndStride.asset  | 55 ---------------------------------
 .../Games/Training/SO_TrainingGame_SlipAndStride.asset.meta           |  8 -----
 Assets/_Scripts/UI/Screens/HangarScreen.cs                            |  8 ++++-
 15 files changed, 11 insertions(+), 292 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Screens/HangarScreen.cs b/Assets/_Scripts/UI/Screens/HangarScreen.cs
index cec0f5774..edf673315 100644
--- a/Assets/_Scripts/UI/Screens/HangarScreen.cs
+++ b/Assets/_Scripts/UI/Screens/HangarScreen.cs
@@ -308,7 +308,13 @@ namespace CosmicShore.UI
         {
             if (SelectedShip == null || SelectedShip.IsLocked) return;
 
-            if (HangarTrainingModal && SelectedShip.TrainingGames != null)
+            // The modal is a two-button picker: SetTrainingGames indexes [0] and [1]
+            // unconditionally, so a null check is not enough - a vessel whose TrainingGames
+            // list is empty or single-entry threw ArgumentOutOfRangeException here. Every
+            // vessel is currently unpopulated (the solo training games this pointed at were
+            // retired with the rest of the solo content); the surface stays wired so the
+            // list can be repopulated with live modes.
+            if (HangarTrainingModal && SelectedShip.TrainingGames is { Count: >= 2 })
             {
                 HangarTrainingModal.SetTrainingGames(SelectedShip.TrainingGames);
                 HangarTrainingModal.ModalWindowIn();
```

</details>

### `0243abb3f` — refactor(rampage): fold the end-game tail onto the shared SyncFinalResults template

_Claude, 2026-07-25 02:41:57 +0000_

```text
Rampage arrived from bleeding-edge with a hand-copied end-game tail: its own
_finalResultsSent latch, a SyncFinalScoresSnapshot/SyncFinalScores_ClientRpc
pair, and a SetupNewRound guard. That was correct when it was written - it
predates the Y1.2 hoist - but after the merge it left one of six domain modes
off the template, which is exactly the duplication Y1.2 removed.

The fold is exact, not approximate:
- The rule's metric is PrismsDestroyed, so the base's rule.LiveMetric snapshot
  and ScoringMetrics.Write replicate the same HostilePrismsDestroyed field the
  bespoke arrays carried.
- ResolveWinnerRepresentativeName breaks ties on the first roster entry, which
  is what the bespoke OrderByDescending(HostilePrismsDestroyed) already did.
- The base owns the SetupNewRound suppression (FinalResultsSent) and the latch
  reset in OnResetForReplayCustom, so both overrides were pure duplicates.
- gameData.ScoringRule = rule dropped: the base already publishes it, null
  guarded, so the unconditional re-assignment only defeated that guard.

Fixes two latent defects the copy carried:
- gameData.HasNoWinner was never written by Rampage. Masked today only because
  ResetRuntimeData clears it on every scene load, so a stale true could not
  survive - latent, but a DNF would not have rendered correctly.
- Winner* was written BEFORE SetResults, the inverse of the documented order.
  SetResults derives Winner* from Results[0] when unset, which is why the
  template writes them after.

No serialized state changed: the scene's seven keys on the component
(ShowTopMostFoldoutHeaderGroup, numberOfRounds, numberOfTurnsPerRound,
countdownTimer, _onToggleReadyButton, rule, arenaCell, aiRetargetSeconds) all
still map to live fields, and rule still resolves to RampageScoringRule.asset.
Removing a ClientRpc changes only the RPC table, which is same-build safe.

All six domain modes now share the one tail. Verify with test step 29.
```

```text
 Assets/_Scripts/Controller/Arcade/RampageController.cs | 117 +++++------------------------------------------
 Docs/UnifiedSystems/YASH.md                            |  13 ++++++
 2 files changed, 25 insertions(+), 105 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 203 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/RampageController.cs b/Assets/_Scripts/Controller/Arcade/RampageController.cs
index 5732d7410..edf95c0dc 100644
--- a/Assets/_Scripts/Controller/Arcade/RampageController.cs
+++ b/Assets/_Scripts/Controller/Arcade/RampageController.cs
@@ -1,8 +1,4 @@
-using System.Linq;
-using Unity.Collections;
-using Unity.Netcode;
 using UnityEngine;
-using CosmicShore.Utility;
 using CosmicShore.Data;
 
 namespace CosmicShore.Gameplay
@@ -12,7 +8,8 @@ namespace CosmicShore.Gameplay
     /// destroy the prism target first (hostile prisms only - another domain's mass; shattering
     /// your own trail is worthless). Structural clone of
     /// <see cref="MultiplayerCrystalCaptureController"/>: 1 round / 1 turn, server-authoritative
-    /// winner detection in OnTurnEndedCustom, final scores replicated by snapshot ClientRpc.
+    /// winner detection in OnTurnEndedCustom, final results replicated by the shared
+    /// <see cref="MultiplayerDomainGamesController.SyncFinalResults"/> template.
     /// The destruction stat itself auto-increments via StatsManager.PrismDestroyed (SOAP
     /// block-destroyed channel), so no per-event listener is needed here.
     /// </summary>
@@ -32,15 +29,13 @@ namespace CosmicShore.Gameplay
                  "so pilots sample on this cadence and fly at the cached point between samples.")]
         [SerializeField, Min(0.25f)] float aiRetargetSeconds = 1.5f;
 
-        private bool _finalResultsSent;
-
         // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
         // (see RampageScoringRuleSO.AssignScores) - lower is better, like HexRace.
         protected override bool UseGolfRules => true;
         protected override bool UseSceneReloadForReplay => true;
 
         // Rampage handles end-game through OnTurnEndedCustom (server-side winner detection) →
-        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
+        // the base SyncFinalResults template, which broadcasts the canonical results tail.
         // Suppress the base controller's turn→round→game flow so we don't get a duplicate
         // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
         protected override bool HasEndGame => false;
@@ -48,10 +43,8 @@ namespace CosmicShore.Gameplay
         public override void OnNetworkSpawn()
         {
             base.OnNetworkSpawn();
-            gameData.ScoringRule = rule;
             numberOfRounds = 1;
             numberOfTurnsPerRound = 1;
-            _finalResultsSent = false;
         }
 
         // ── AI mass hunters (server) ──────────────────────────────────────
@@ -96,110 +89,25 @@ namespace CosmicShore.Gameplay
         // ── Server-authoritative game end ─────────────────────────────────
 
         /// <summary>
-        /// Server-side winner detection, mirroring the Crystal Capture pattern.
-        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound,
-        /// so _finalResultsSent is set in time to suppress the Ready button.
+        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
+        /// ExecuteServerTurnEnd → SetupNewRound, so FinalResultsSent latches in time for the
+        /// base SetupNewRound to suppress the Ready button. Winning domain (highest destruction
+        /// sum, Jade→Ruby→Gold tie-break) delegated to the rule; score assignment, the
+        /// representative winner name (best individual contributor on the winning domain), the
+        /// roster snapshot and the canonical results tail are owned by the base template.
         /// </summary>
         protected override void OnTurnEndedCustom()
         {
             base.OnTurnEndedCustom();
-            if (!IsServer || _finalResultsSent) return;
-            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;
+            if (!IsServer || FinalResultsSent) return;
 
-            // Winning domain (highest destruction sum, Jade→Ruby→Gold tie-break) delegated to
-            // the rule; representative winner-name = best individual contributor on that domain
-            // (legacy display field - victory/defeat attribution uses WinnerDomain).
             var winningDomain = rule.ResolveWinner(gameData);
             if (winningDomain == Domains.Blue) return;
 
-            var winnerRep = gameData.RoundStatsList
-                .Where(s => s.Domain == winningDomain)
```

</details>

### `e7bf9c3a9` — fix(scenes): drop two prefab overrides left stale by deleted assets

_Claude, 2026-07-25 02:43:57 +0000_

```text
Both are prefab-instance m_Modifications entries whose property no longer
exists on the target script, so Unity silently discards them on load and
re-serializes - meaning anyone who opened either scene got a dirty file they
did not edit. They were also the last references anywhere to two deleted
assets.

- MinigameRampage.unity: "domainColorPalette" override on the Scoreboard
  component, pointing at DomainColorPalette.asset. Both the asset and
  DomainColorPaletteSO were deleted on bleeding-edge; no live script declares
  the field.
- MinigameJoust_Gameplay.unity: "entryPrefab" override pointing at
  FeedEntry.prefab. Doubly stale - the GameEventFeed system was deleted with
  the toast migration, and the override's target fileID no longer exists in
  GameCanvas-HexRace.prefab either.

Neither existed on my branch alone; both arrived with bleeding-edge and were
inert there too, so this is cleanup rather than a merge fix. After removal
both scenes lint clean (81 and 89 documents, all anchors unique, every
remaining override a well-formed target/propertyPath/value/objectReference
quad) and both deleted GUIDs now have zero referrers repo-wide.
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity | 6 ------
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity        | 6 ------
 2 files changed, 12 deletions(-)
```

### `affcd9c01` — docs: correct toast coverage - the panel is per-scene and only 2 scenes have it

_Claude, 2026-07-25 02:44:08 +0000_

```text
GameToastController/GameToastView live on a per-scene panel ("Lives on the
toast panel prefab next to the view"), not on a persistent object, so a scene
without one receives nothing - GameToastAPI.Post fires into a scene with no
listener and is silently dropped.

Measured coverage after the merge: HexRace (script mounted directly) and Joust
(NotificationUI.prefab instanced). Crystal Capture, Maelstrom, AstroLeague,
NucleusRush, Rampage and Menu_Main have no toast UI. This is bleeding-edge's
rollout state rather than merge damage, but it changes what test step 31 should
expect, and step 31 as written would have read as a failure.

- LAVALAMP.md: the Game UI diagram still listed "NotificationUI [GameEventFeed]"
  under MiniGameHUD - doubly wrong, since GameEventFeed was deleted AND no
  NotificationUI instance exists in Menu_Main. Replaced with a note saying the
  lava lamp shows no toasts.
- SOLO_RETIREMENT_TESTS.md step 31: states the expected partial coverage
  explicitly, including that the Brood Rush wave beat will not appear, and flags
  wiring the remaining six scenes as a follow-up outside this branch.
```

```text
 Docs/LAVALAMP.md                             |  5 ++++-
 Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md | 10 ++++++++--
 2 files changed, 12 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/LAVALAMP.md b/Docs/LAVALAMP.md
index 9c3320a58..fa3675238 100644
--- a/Docs/LAVALAMP.md
+++ b/Docs/LAVALAMP.md
@@ -47,9 +47,12 @@ Game UI [RectTransform, CanvasGroup]
 │   ├── RoundTime (rotating circles + countdown TMP)
 │   ├── LifeFormCounter (rotating circles + counter TMP)
 │   ├── ThumbCursors (LeftCursor, RightCursor — ThumbCursor)
-│   ├── NotificationUI [GameEventFeed]
 │   └── PlayerScoreContainer [Transform — for dynamically instantiated PlayerScoreCards]
 │
+│   (No toast panel here. `GameEventFeed` was retired with the game-toast system;
+│    its replacement — `NotificationUI.prefab` [GameToastController + GameToastView] —
+│    is per-scene and is NOT instanced in Menu_Main, so the lava lamp shows no toasts.)
+│
 ├── Vessel Selection Panel [CanvasGroup, VesselSelectionPanelUI, MenuVesselSelectionPanelController]
 │   ├── Buttons (Resume, Close)
 │   └── Menu [GridLayout, 6× ShipCardView]
diff --git a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
index 7b6ad2cc8..0dbcff4db 100644
--- a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
+++ b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
@@ -81,8 +81,14 @@ branch alone.
   scoreboard flips to **finish time** for the winner / remaining-crystals sentinel for the
   losers (their golf scoring). Getting only one of the two = the union resolution regressed.
 - [ ] **31.** **Game toasts** replaced the old event feed: a toast appears on player-Ready and
-  on a mid-game disconnect, and on the Brood Rush wave-scored beat in NucleusRush. The old
-  bottom-of-screen `GameEventFeed` strip must be gone entirely (no empty UI object left behind).
+  on a mid-game disconnect. The old `GameEventFeed` strip must be gone entirely (no empty UI
+  object left behind). **Expect partial coverage** — `GameToastController`/`GameToastView` live
+  on a per-scene panel, and only **HexRace** (script mounted directly) and **Joust**
+  (`NotificationUI.prefab` instanced) currently carry one. Crystal Capture, Maelstrom,
+  AstroLeague, NucleusRush, Rampage and Menu_Main have no toast UI, so they will show nothing —
+  including the Brood Rush wave beat, whose `GameToastAPI.Post` call fires into a scene with no
+  listener. This is bleeding-edge's rollout state, not merge damage; wiring the remaining six
+  scenes is a follow-up, not part of this branch.
 - [ ] **32.** Bootstrap union check (W1 vs their Bootstrap UI rework): boot completes, the new
   heartbeat loader animates, AND the quest chain still works (step 25) — the
   `GameModeProgressionService` component must still be mounted on the `PlayerDataService`
```

</details>

### `2c7f0656c` — fix(arcade): CS0103 - client config RPC body used a parameter C5 removed

_Claude, 2026-07-30 19:21:37 +0000_

```text
SyncGameConfigToClients_ClientRpc lost its isMultiplayer parameter when C5
retired IsMultiplayerMode (every mode runs the networked single-host model, so
there is nothing left to sync). Their Load Time Insights work added a
LoadInsights.SetGameContext call inside that same method body which passes the
parameter. Each side only touched its own half, so the merge produced my
signature with their body and no conflict marker:

  Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs(533,79):
  error CS0103: The name 'isMultiplayer' does not exist in the current context

Passes isMultiplayer: true, matching the identical fix already applied to
GameDataSO.InvokeGameLaunch in the merge commit. The solo-vs-party distinction
the flag used to carry is readable off humanPlayers, which the same call
already reports.

Why the merge sweep missed it: that sweep grepped for the deleted TYPE and
MEMBER names (IsMultiplayerMode, SO_Game.IsMultiplayer). This was a lowercase
local parameter, invisible to those greps. Added a check for the actual failure
shape - a merged method signature that dropped a parameter its merged body still
uses - validated by confirming it fires on this exact bug, then run across all
88 both-modified files that survive in the tree: no other instance. A companion
call-site arity check over the four methods whose parameter count differs
between the two parents (SyncGameConfigToClients_ClientRpc, AssignLineage,
SpawnFaunaPopulation, ShowAsync) is also clean - declaration and call site agree
in every case.
```

```text
 Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs | 6 +++++-
 1 file changed, 5 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
index 2efe9d3f2..c976d4c94 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -530,7 +530,11 @@ namespace CosmicShore.Gameplay
             LoadInsights.Mark("Game config received from server");
             LoadInsights.SetGameContext(
                 sceneName, ((GameModes)gameMode).ToString(), intensity, playerCount,
-                Mathf.Max(0, playerCount - aiBackfillCount), aiBackfillCount, isMultiplayer);
+                Mathf.Max(0, playerCount - aiBackfillCount), aiBackfillCount,
+                // The RPC no longer carries an isMultiplayer flag: C5 retired
+                // IsMultiplayerMode because every mode runs the networked single-host
+                // model, so there is nothing left to sync. Matches GameDataSO.InvokeGameLaunch.
+                isMultiplayer: true);
         }
     }
 }
\ No newline at end of file
```

</details>

### `2b8cd449d` — docs(unified-systems): record C8-C12 + M1-M4, refresh what's left, hand off

_Claude, 2026-08-01 07:33:14 +0000_

```text
Closes out the session's work in the tracking docs so the next one starts from
state rather than re-deriving it.

YASH.md
- New "Program status at a glance" table: Y0/Y1/Y2 + C8-C12 + M1-M4 executed;
  Y3/Y5/Y6/Y8.1 open and [default-ok]; Y4/Y7 still hard-gated. States plainly
  that in-editor verification is the one thing gating everything shipped.
- Flags Y2's C1/C2 bullets as SUPERSEDED - they describe consolidating Cellular
  Duel and converting Wildlife Blitz, and C9/C10 then deleted both stacks. Left
  in place as history with a warning, since acting on them would be wrong.
- Records C8-C12 (the retirement wave) and M1-M4 (228 upstream commits, 21
  conflicts) with what each actually changed.
- Adds a "Merge hazards" note: both CS0103s this session reached the tree
  through CLEAN auto-merges - this branch's signature plus upstream's body -
  which conflict markers and deleted-symbol greps cannot catch. Includes the
  check that does catch them, and the warning that the first detector written
  for it silently skipped every method without an access modifier and reported
  clean, so any such detector must be proven to fire on a known bug first.
- Rewrites the new-session starting prompt: Y0-Y2 are done and must not be
  re-planned, editor verification comes before new work, Y4/Y7 stay untouched.
- Lists four known non-blocking issues carried deliberately (partial toast
  scene coverage, six stale silhouette refs identical on bleeding-edge, the
  instance-less Arcade singleton, the intentionally empty training lists) so
  they are not rediscovered as new bugs.

SOLO_RETIREMENT_TESTS.md
- Status header: 6 of 33 live steps verified; everything since is static-only
  verification, which cannot catch wrong gameplay values or a broken end-game
  sequence. Suggests an order if time is short.
- New Part F (34-37) for the second merge: the HexRace per-intensity lap table
  with expected targets per intensity (24/30/56/54) because that failure is
  silent, a compile check naming both fixed CS0103 sites, a cloud-profile check
  after the schema swap, and the Train-button no-op.

ScoringSystem/REFACTOR.md
- Records that the unified-path goal is MET: all six domain modes end through
  the one SyncFinalResults template, Rampage folded on as the last holdout.
  Adds the rule for a seventh mode - never hand-write a tail.

ScoringSystem/ARCHITECTURE.md
- Notes the merge hazard directly on the two sites in the §8 fork-site table
  where it actually fired, including why grepping IsMultiplayerMode misses it.
```

```text
 Docs/ScoringSystem/ARCHITECTURE.md           |  13 ++++++
 Docs/ScoringSystem/REFACTOR.md               |  22 +++++++++
 Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md |  41 ++++++++++++++++
 Docs/UnifiedSystems/YASH.md                  | 144 ++++++++++++++++++++++++++++++++++++++++++++++++++++-----
 4 files changed, 208 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 285 lines)</summary>

```diff
diff --git a/Docs/ScoringSystem/ARCHITECTURE.md b/Docs/ScoringSystem/ARCHITECTURE.md
index c5a6a2529..3862c3c6c 100644
--- a/Docs/ScoringSystem/ARCHITECTURE.md
+++ b/Docs/ScoringSystem/ARCHITECTURE.md
@@ -331,6 +331,19 @@ What each fork-site became:
 | Writes (`TournamentController`, `BenchmarkSceneLauncher`, `Arcade.cs` dead launcher, `GameDataSO.SyncFromArcadeGame` + `ResetAllData`) | deleted with the flag |
 | `Loadout.IsMultiplayer` / `LoadoutCloudData.IsMultiplayer` | KEPT as documented tombstones (persisted cloud-save schema); callers pass constant `true`; never branch on them |
 
+> **Merge hazard at two of these sites (hit twice, 2026-07-25).** Removing the `isMultiplayer`
+> parameter from `SyncGameConfigToClients_ClientRpc` and the flag from
+> `GameDataSO.InvokeGameLaunch` changed those methods' **signatures** on this branch while
+> `bleeding-edge` independently added a `LoadInsights.SetGameContext(...)` call **inside both
+> method bodies** that passes the flag. Git auto-merges signature-from-one-side with
+> body-from-the-other and emits **no conflict marker**; the result is `CS0103: The name
+> 'isMultiplayer' does not exist in the current context`. Both sites now pass
+> `isMultiplayer: true` — unconditional post-C5, and the solo-vs-party distinction is readable
+> off `humanPlayers`, which the same call already reports.
+>
+> If a future merge touches either method, check the body before trusting a clean merge. Grepping
+> for `IsMultiplayerMode` does **not** find this — the reintroduced identifier is a lowercase
+> parameter name.
 
 ---
 
diff --git a/Docs/ScoringSystem/REFACTOR.md b/Docs/ScoringSystem/REFACTOR.md
index 6c92a70da..3de5b3dfc 100644
--- a/Docs/ScoringSystem/REFACTOR.md
+++ b/Docs/ScoringSystem/REFACTOR.md
@@ -29,6 +29,28 @@ These come from the project owner and govern **every** item below:
 
 ---
 
+> **The unified path goal is MET (2026-07-25, `claude/unified-yash-refactor-9sc0ws`).** All
+> **six** domain modes — HexRace, Joust, Crystal Capture, NucleusRush, AstroLeague and Rampage —
+> now end through the one `MultiplayerDomainGamesController.SyncFinalResults` template
+> (score assignment → sort → aggregate → snapshot → `SetResults` → `Winner*` → `HasNoWinner` →
+> `InvokeWinnerCalculated` → `InvokeMiniGameEnd`). No mode reimplements the tail, and there is no
+> `IsMultiplayerMode` fork left to remove — the property itself was deleted in C5.
+>
+> Rampage was the last holdout: it arrived from `bleeding-edge` with a hand-copied tail written
+> before the Y1.2 hoist existed, and was folded onto the template on arrival (commit `0243abb3`).
+> Doing so also fixed two latent defects the copy carried — it never wrote `gameData.HasNoWinner`,
+> and it wrote `Winner*` **before** `SetResults`, the inverse of the order that stops a DNF
+> rendering as VICTORY.
+>
+> **If you add a seventh domain mode, do not hand-write an end-game tail.** Set
+> `HasEndGame => false`, resolve the winning domain in `OnTurnEndedCustom`, and call
+> `SyncFinalResults(domain, finishTime)`. The template owns everything after that.
+>
+> Still unverified in the editor: B17's engine check and the per-mode regression runs
+> (`Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md` steps 13–17, 20–21, 29–30, 34).
+
+---
+
 ## Open design questions (agree before coding)
 
 ### Q1 — ✅ RESOLVED by owner 2026-07-20: solo modes retired outright; flag deleted (see R1)
diff --git a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
index 0dbcff4db..657b9b47e 100644
--- a/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
+++ b/Docs/UnifiedSystems/SOLO_RETIREMENT_TESTS.md
@@ -10,6 +10,16 @@ against it. (C10 retired Wildlife Blitz: steps 8-11 and 23 are struck.)
 Legend: `[x]` verified by owner · `[ ]` pending · ~~struck~~ = obsolete (feature retired
 after the step was written; do not test).
 
+> **Status 2026-07-25 — 6 of 33 live steps verified.** Steps 1–6 passed in the editor on
+> 2026-07-21; everything since (C9–C12, Y0, and BOTH `bleeding-edge` merges) has been verified
+> **statically only** — guid greps, deleted-symbol sweeps, scene/YAML lint, build-settings
+> resolution. That catches dangling references and compile breaks; it cannot catch wrong
+> gameplay values, a broken end-game sequence, or a HUD that no longer binds. Steps 12–21 and
+> 24–37 are the outstanding work, and they gate starting Y3+.
+>
+> Highest-value first if time is short: **34** (silent failure mode), **29/30** (the two merge
+> unions), **35** (compile), then **13–17** (per-mode regression).
+
 ## Setup
 
 - [x] **1.** Pull the branch, open in Unity, let it compile — zero compile errors. *(verified 2026-07-21)*
@@ -96,6 +106,37 @@ branch alone.
 - [ ] **33.** Build settings: File → Build Settings lists 13 scenes, all resolving (no
```

</details>

_Also contains 6 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
