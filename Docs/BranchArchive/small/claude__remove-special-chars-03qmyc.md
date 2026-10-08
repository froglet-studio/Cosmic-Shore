# Branch archive: `claude/remove-special-chars-03qmyc`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-26 by Claude
- **Unmerged commits:** 1
- **Forked from:** `f1e02350b` (2026-06-26, Merge pull request #567 from froglet-studio/claude/fauna-suction-shader-tracki)
- **Tip:** `b15dbda5c`
- **Files touched (636):**
  - `.claude/skills/EndGameConditions/SKILL.md`
  - `.claude/skills/ecology/SKILL.md`
  - `.claude/skills/reorient/SKILL.md`
  - `Assets/Editor/ToastNotificationSetup.cs`
  - `Assets/FTUE/Scripts/Data/TutorialPhase.cs`
  - `Assets/FTUE/Scripts/Drivers/TutorialFlowController.cs`
  - `Assets/FTUE/Scripts/Helpers/FTUEEventManager.cs`
  - `Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl`
  - `Assets/_Graphics/Materials/Shaders/HyperSeaSkybox.shader`
  - `Assets/_SO_Assets/Captains/Arcade/SO_Captain_Arcade_Freestyle_Sparrow.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Dolphin_Charge.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Dolphin_Mass.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Dolphin_Space.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Dolphin_Time.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Manta_Charge.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Manta_Mass.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Manta_Time.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Rhino_Charge.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Rhino_Space.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Rhino_Time.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Sparrow_Charge.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Sparrow_Mass.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Sparrow_Space.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Sparrow_Time.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Squirrel_Charge.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Squirrel_Mass.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Squirrel_Space.asset`
  - `Assets/_SO_Assets/Captains/Elemental/SO_Captain_Squirrel_Time.asset`
  - `Assets/_SO_Assets/Cell Configs/Astro League Cell/Astro League Cell Config.asset`
  - `Assets/_Scripts/Controller/AI/AIPilot.cs`
  - `Assets/_Scripts/Controller/Animation/SingleStickAnimationController.cs`
  - `Assets/_Scripts/Controller/Arcade/ASTROLEAGUE.md`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueArena.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueController.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueGoal.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueMatchMonitor.cs`
  - `Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs`
  - `Assets/_Scripts/Controller/Arcade/CRYSTAL_CAPTURE.md`
  - `Assets/_Scripts/Controller/Arcade/CrystalCaptureStatsReporter.cs`
  - … and 596 more

### `b15dbda5c` — style: replace non-ASCII special characters with ASCII across the project

_Claude, 2026-06-26 22:35:34 +0000_

```text
The AI's habit of decorating code and docs with special glyphs (box-drawing
banners, em/en dashes, arrows, bullets, math symbols, smart quotes, emoji,
BOMs) leaves tofu boxes and mojibake in the Unity Inspector, consoles, and
diffs. Convert all of it to plain ASCII and add a rule so it stops recurring.

Scope (first-party only; vendored plugins left untouched):
- Code: 524 .cs + 1 .shader + 1 .hlsl - comments, string literals, [Header]/
  [Tooltip] text, menu paths, and log messages. Examples: em/en dash -> '-',
  arrows -> '->', box-drawing banners -> '-'/'='/'|'/'+', bullets -> '-',
  'x'/'+/-'/'<='/'>='/'!='/'~'/'^2'/'deg'/'pi'/'...'/'Sec', smart quotes ->
  straight, emoji removed. Star-rating string keeps filled-vs-empty as '*'/'-'.
  Fixed 11 mojibake (U+FFFD) comment lines back to their intended text.
- Serialized data: 20 .asset captain flavor / cell description strings. Captain
  flavors are re-emitted as properly-escaped YAML double-quoted scalars so the
  visible quotation marks are preserved and the YAML stays valid.
- Docs: 90 .md files (CLAUDE.md + Docs/ + skills) - same ASCII conversion.

Added an explicit ASCII-only rule to CLAUDE.md (Code Style + What Claude Code
Should Never Do) so future sessions never introduce these characters again.

Verified: zero non-ASCII remaining in first-party scope; zero double-quote
parity flips across all 524 changed .cs files (no string literal broken);
captain-flavor YAML round-trips correctly through a YAML 1.1 parser.
```

```text
 Docs/PartySystem/MPPM_SESSION_LOG.md                                  |  284 +++---
 Docs/PartySystem/REFACTOR.md                                          |   48 +-
 Docs/PartySystem/TESTS.md                                             |   54 +-
 Docs/PartySystem/TODOS.md                                             |   36 +-
 Docs/PartySystem/UI.md                                                |   84 +-
 Docs/PresenceSystem/ARCHITECTURE.md                                   |   26 +-
 Docs/PresenceSystem/BUGS.md                                           |  104 +--
 Docs/PresenceSystem/REFACTOR.md                                       |   44 +-
 Docs/PresenceSystem/TESTS.md                                          |   24 +-
 Docs/PresenceSystem/TODOS.md                                          |   14 +-
 Docs/README.md                                                        |  134 +--
 Docs/SCENES.md                                                        |  316 +++----
 Docs/SPATIAL_INDEX.md                                                 |  142 +--
 Docs/ScoringSystem/ARCHITECTURE.md                                    |  174 ++--
 Docs/ScoringSystem/BUGS.md                                            |  172 ++--
 Docs/ScoringSystem/CHANGELOG.md                                       |   74 +-
 Docs/ScoringSystem/RANKING_SYNC_PLAN.md                               |   98 +-
 Docs/ScoringSystem/REFACTOR.md                                        |  158 ++--
 Docs/ScoringSystem/TESTS.md                                           |   92 +-
 Docs/ScoringSystem/TODOS.md                                           |   34 +-
 Docs/ShuffleSystem/ARCHITECTURE.md                                    |   46 +-
 Docs/THREADING.md                                                     |   60 +-
 Docs/TournamentSystem/ARCHITECTURE.md                                 |  174 ++--
 Docs/TournamentSystem/MAELSTROM_REWORK_SPEC.md                        |  226 ++---
 Docs/TournamentSystem/MAELSTROM_UX_HANDOFF.md                         |  138 +--
 GIT_RULES.md                                                          |   52 +-
 PLAN.md                                                               |   52 +-
 README.md                                                             |  168 ++--
 Tools/ecosim/README.md                                                |   28 +-
 636 files changed, 8574 insertions(+), 8561 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 47791 lines)</summary>

```diff
diff --git a/.claude/skills/EndGameConditions/SKILL.md b/.claude/skills/EndGameConditions/SKILL.md
index bc942998a..0af1f9f3b 100644
--- a/.claude/skills/EndGameConditions/SKILL.md
+++ b/.claude/skills/EndGameConditions/SKILL.md
@@ -1,15 +1,15 @@
 ---
 name: EndGameConditions
-description: Use when setting, changing, or asking where the end-game / win-condition COUNTS live for the domain modes — HexRace crystal count, Joust joust count, Crystal Capture crystal count (how many crystals/jousts end a turn), and Maelstrom/Tournament win target (placement points to win the whole shuffle, "race to N"). These are now authored ONLY through the Tools > Cosmic Shore > End Game Conditions editor window (backed by Resources/EndConditionOverrides.asset), never via per-scene inspector fields. Trigger when editing CrystalCollisionTurnMonitor / JoustCollisionTurnMonitor / NetworkCrystalCollisionTurnMonitor / NetworkJoustCollisionTurnMonitor, TournamentDataSO / TournamentController, EndConditionOverridesSO, or when someone wants to make a mode end sooner/later.
+description: Use when setting, changing, or asking where the end-game / win-condition COUNTS live for the domain modes - HexRace crystal count, Joust joust count, Crystal Capture crystal count (how many crystals/jousts end a turn), and Maelstrom/Tournament win target (placement points to win the whole shuffle, "race to N"). These are now authored ONLY through the Tools > Cosmic Shore > End Game Conditions editor window (backed by Resources/EndConditionOverrides.asset), never via per-scene inspector fields. Trigger when editing CrystalCollisionTurnMonitor / JoustCollisionTurnMonitor / NetworkCrystalCollisionTurnMonitor / NetworkJoustCollisionTurnMonitor, TournamentDataSO / TournamentController, EndConditionOverridesSO, or when someone wants to make a mode end sooner/later.
 ---
 
-# End Game Conditions — how the modes' win counts are set
+# End Game Conditions - how the modes' win counts are set
 
-The per-mode end-game **count** — how many crystals (HexRace, Crystal Capture) or jousts
+The per-mode end-game **count** - how many crystals (HexRace, Crystal Capture) or jousts
 (Joust) end a turn, and how many placement points a domain needs to win a whole **Maelstrom /
-Tournament** ("race to N") — is set in **one place** and one place only:
+Tournament** ("race to N") - is set in **one place** and one place only:
 
-> **Tools ▸ Cosmic Shore ▸ End Game Conditions**
+> **Tools > Cosmic Shore > End Game Conditions**
 
 That window edits the single config asset **`Assets/Resources/EndConditionOverrides.asset`**
 (`EndConditionOverridesSO`). The turn monitors (and `TournamentController` for Maelstrom) load it
@@ -19,16 +19,16 @@ at runtime via `Resources.Load`.
 
 - **There are NO per-scene inspector fields for these counts.** The old
   `CrystalCollisionTurnMonitor.CrystalCollisions` and `JoustCollisionTurnMonitor.collisionsNeeded`
-  `[SerializeField]`s were removed on purpose. **Do not re-add `[SerializeField]` to them** — they
+  `[SerializeField]`s were removed on purpose. **Do not re-add `[SerializeField]` to them** - they
   are now plain internal fields that just hold the *resolved* value. If you want a count changed,
   change it in the tool, not in a scene.
 - **`0` = auto/default, `> 0` = explicit count** (same semantic the old field had, moved into the tool):
-  - **HexRace / Crystal Capture** — `0` → auto-calc from the track waypoints (then 39).
-  - **Joust** — `0` → `EndConditionOverridesSO.DefaultJoustCount` (3).
-  - **Maelstrom** — `0` → `EndConditionOverridesSO.DefaultMaelstromWinTarget` (6). This is the
+  - **HexRace / Crystal Capture** - `0` -> auto-calc from the track waypoints (then 39).
+  - **Joust** - `0` -> `EndConditionOverridesSO.DefaultJoustCount` (3).
+  - **Maelstrom** - `0` -> `EndConditionOverridesSO.DefaultMaelstromWinTarget` (6). This is the
     "race to N" win target: the first DOMAIN whose cumulative `{2,1,0}` placement points reach it
-    wins the shuffle. NOT a per-turn count — it ends the whole tournament.
-- The setting **applies wherever the mode runs** — standalone arcade *and* inside a Tournament.
+    wins the shuffle. NOT a per-turn count - it ends the whole tournament.
+- The setting **applies wherever the mode runs** - standalone arcade *and* inside a Tournament.
 - HexRace and Crystal Capture share the same monitor class, so the SO keys the count by
   `gameData.GameMode` (HexRace 33 vs MultiplayerCrystalCapture 35). Keep that switch in
   `EndConditionOverridesSO.GetCrystalCount`.
@@ -36,18 +36,18 @@ at runtime via `Resources.Load`.
 ## How it flows at runtime
 
 ```
-EndConditionOverridesSO (Resources/EndConditionOverrides.asset)   ← edited by the Tools window
-        │  GetCrystalCount(mode, autoCalc) / GetJoustCount() / GetMaelstromWinTarget()
-        ▼
-CrystalCollisionTurnMonitor.GetCrystalCollisionCount()   → resolves CrystalCollisions (HexRace, Crystal Capture)
-JoustCollisionTurnMonitor.StartMonitor()                 → resolves collisionsNeeded (Joust)
-TournamentController.StartTournamentInternal()           → TournamentDataSO.ResolveWinTarget(...) (Maelstrom)
-        │  (0 in the tool → waypoint auto-calc / default 3 / default 6)
-        ▼
-NetworkCrystalCollisionTurnMonitor → gameData.CrystalTargetCount   (synced to clients)
-NetworkJoustCollisionTurnMonitor   → gameData.JoustTargetCount
-TournamentDataSO.EffectiveWinTarget → IsShuffleComplete + the lobby/summary race-rule text
-        ▼
+EndConditionOverridesSO (Resources/EndConditionOverrides.asset)   <- edited by the Tools window
+        |  GetCrystalCount(mode, autoCalc) / GetJoustCount() / GetMaelstromWinTarget()
+        v
+CrystalCollisionTurnMonitor.GetCrystalCollisionCount()   -> resolves CrystalCollisions (HexRace, Crystal Capture)
+JoustCollisionTurnMonitor.StartMonitor()                 -> resolves collisionsNeeded (Joust)
+TournamentController.StartTournamentInternal()           -> TournamentDataSO.ResolveWinTarget(...) (Maelstrom)
+        |  (0 in the tool -> waypoint auto-calc / default 3 / default 6)
+        v
+NetworkCrystalCollisionTurnMonitor -> gameData.CrystalTargetCount   (synced to clients)
+NetworkJoustCollisionTurnMonitor   -> gameData.JoustTargetCount
+TournamentDataSO.EffectiveWinTarget -> IsShuffleComplete + the lobby/summary race-rule text
+        v
 the mode's ScoringRuleSO.IsObjectiveReached(...) ends the turn on that target;
 for Maelstrom, the first DOMAIN to reach EffectiveWinTarget cumulative points ends the shuffle
 ```
@@ -62,9 +62,9 @@ networking.
 
 ## To change a count
 
-1. Unity → **Tools ▸ Cosmic Shore ▸ End Game Conditions** (auto-creates the asset on first open).
-2. Set **HexRace — Crystal Count**, **Crystal Capture — Crystal Count**, **Joust — Joust Count**,
-   and/or **Maelstrom — Win Target (points)**. Leave `0` for auto/default. The window shows the
+1. Unity -> **Tools > Cosmic Shore > End Game Conditions** (auto-creates the asset on first open).
+2. Set **HexRace - Crystal Count**, **Crystal Capture - Crystal Count**, **Joust - Joust Count**,
+   and/or **Maelstrom - Win Target (points)**. Leave `0` for auto/default. The window shows the
    effective value and saves on edit.
 3. Commit `Assets/Resources/EndConditionOverrides.asset` (and its `.meta`).
 
@@ -76,25 +76,25 @@ HexRace `0` (auto), Crystal Capture `20`, Joust `3`, Maelstrom `6`.
 The asset stores **two** sets of counts:
 
 - **Live values** (`hexRaceCrystalCount` / `crystalCaptureCrystalCount` / `joustCount` /
-  `maelstromWinTarget`) — what the turn monitors (and `TournamentController` for Maelstrom) actually
+  `maelstromWinTarget`) - what the turn monitors (and `TournamentController` for Maelstrom) actually
   read at runtime. Lower these to end a mode quickly while testing.
-- **Build baseline** (`*Build` fields) — the values a shipping build must use. They are *not* read at
-  runtime; they're the restore target. You don't type them in — click **"Set Build Values"** in the
+- **Build baseline** (`*Build` fields) - the values a shipping build must use. They are *not* read at
+  runtime; they're the restore target. You don't type them in - click **"Set Build Values"** in the
   window to snapshot the current Live values as the baseline (shown read-only above the button). Do
   this once, when the Live values are the real production counts.
 
 The safety net is the toggle **"Auto-restore build values before build"** (`autoRestoreBuildValuesBeforeBuild`,
 **on by default**). When on, `EndConditionBuildRestore` (`IPreprocessBuildWithReport`) copies the
-Build baseline onto the Live counts and saves the asset at build start — no warning, no block, it
-just restores — so test values can't ship. Turn it off to build with the current Live values.
+Build baseline onto the Live counts and saves the asset at build start - no warning, no block, it
+just restores - so test values can't ship. Turn it off to build with the current Live values.
 
-`EndConditionOverridesSO.ApplyBuildValues()` (build → live, used by the build restore),
-`CaptureBuildValues()` (live → build, used by "Set Build Values"), and `LiveMatchesBuild` (skip if
+`EndConditionOverridesSO.ApplyBuildValues()` (build -> live, used by the build restore),
+`CaptureBuildValues()` (live -> build, used by "Set Build Values"), and `LiveMatchesBuild` (skip if
 already in sync) are the shared helpers. Keep the committed asset's `*Build` fields equal to its Live
 fields so a clean checkout is already in sync.
 
-Testing workflow: (one time) set production counts → **Set Build Values** → commit. Then: drop a Live
-count → test → build (auto-restore puts the baseline back) — or click nothing and just rely on the
+Testing workflow: (one time) set production counts -> **Set Build Values** -> commit. Then: drop a Live
+count -> test -> build (auto-restore puts the baseline back) - or click nothing and just rely on the
 build restore.
 
 ## Files
@@ -107,7 +107,7 @@ build restore.
 | Build-time auto-restore | `Assets/_Scripts/Editor/EndConditionBuildRestore.cs` |
 | Crystal modes read it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/CrystalCollisionTurnMonitor.cs` (`GetCrystalCollisionCount`) |
 | Joust reads it here | `Assets/_Scripts/Controller/Arcade/TurnMonitors/JoustCollisionTurnMonitor.cs` (`StartMonitor`) |
-| Maelstrom resolves it here | `Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs` (`StartTournamentInternal` → `ResolveWinTarget`) |
+| Maelstrom resolves it here | `Assets/_Scripts/Controller/Arcade/Tournament/TournamentController.cs` (`StartTournamentInternal` -> `ResolveWinTarget`) |
 | Maelstrom reads it here | `Assets/_Scripts/Utility/DataContainers/Tournament/TournamentDataSO.cs` (`EffectiveWinTarget`, `IsShuffleComplete`) |
 | Network sync (unchanged) | `NetworkCrystalCollisionTurnMonitor.cs` (`CrystalTargetCount`), `NetworkJoustCollisionTurnMonitor.cs` (`JoustTargetCount`) |
 
@@ -116,13 +116,13 @@ build restore.
 Add a Live field **and** its `*Build` counterpart (+ a `case`/getter) in `EndConditionOverridesSO`,
 include both in `LiveMatchesBuild` / `ApplyBuildValues` / `CaptureBuildValues`, add the Live input
 row + the Build-baseline display line in the editor window, then have that mode resolve its count
-through the SO — **never** with a new per-scene `[SerializeField]`.
+through the SO - **never** with a new per-scene `[SerializeField]`.
 
 - **Per-turn modes** (crystal/joust): the turn monitor resolves the count at `StartMonitor`.
-- **Session-level modes** (Maelstrom/Tournament): there is no turn monitor — resolve once at the
+- **Session-level modes** (Maelstrom/Tournament): there is no turn monitor - resolve once at the
   session start. Maelstrom's `TournamentController.StartTournamentInternal` reads
   `GetMaelstromWinTarget()` and stamps it via `TournamentDataSO.ResolveWinTarget`; everything reads
   `TournamentDataSO.EffectiveWinTarget` (which falls back to the serialized `WinTarget` when the tool
-  asset is missing / in unit tests). Resolving once per start — instead of calling
```

</details>
