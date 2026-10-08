# Branch archive: `qa/results-2026-09-07-garrett`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-09-07 by Garrett Milliron
- **Unmerged commits:** 65
- **Forked from:** `e800630d2` (2026-09-04, Merge branch 'claude/weekly-challenge-leaderboard-ugs-7kmhiz' into bleeding-ed)
- **Tip:** `900357570`
- **Files touched (244):**
  - `.claude/skills/asset-surgery/SKILL.md`
  - `.claude/skills/ship/SKILL.md`
  - `Assets/Resources/ElementalAbilityMaps/Sparrow.asset`
  - `Assets/Resources/EndConditionOverrides.asset`
  - `Assets/Resources/ModeControlsLibrary.asset`
  - `Assets/Resources/ObjectiveIconSet.asset`
  - `Assets/_Graphics/Design Assests/FX/fx_arclightning.mat`
  - `Assets/_Graphics/RenderTextures/PipRenderTexture.renderTexture`
  - `Assets/_Graphics/UI/Objectives/objective_prisms_stolen.png`
  - `Assets/_Graphics/UI/Objectives/objective_prisms_stolen.png.meta`
  - `Assets/_Graphics/UI/Objectives/objective_switches_threaded.png`
  - `Assets/_Graphics/UI/Objectives/objective_switches_threaded.png.meta`
  - `Assets/_Graphics/UI/Objectives/objective_volume_destroyed.png`
  - `Assets/_Graphics/UI/Objectives/objective_volume_destroyed.png.meta`
  - `Assets/_Prefabs/Projectile/AOEMissileWarhead.prefab`
  - `Assets/_Prefabs/Projectile/AOEMissileWarhead.prefab.meta`
  - `Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab`
  - `Assets/_Prefabs/Projectile/SparrowProjectile.prefab`
  - `Assets/_Prefabs/Spacevessels/Dolphin.prefab`
  - `Assets/_Prefabs/Spacevessels/Sparrow.prefab`
  - `Assets/_Prefabs/Spacevessels/Squirrel.prefab`
  - `Assets/_Prefabs/Spacevessels/Urchin.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableDrum.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableDrum.prefab.meta`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard1.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard1.prefab.meta`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard2.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard2.prefab.meta`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard3.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard3.prefab.meta`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard4.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableSwitchyard4.prefab.meta`
  - `Assets/_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab`
  - `Assets/_Prefabs/UI Elements/VesselHUD/SparrowHUDVariant.prefab`
  - `Assets/_SO_Assets/Cell Configs/Drumfire Cell.meta`
  - `Assets/_SO_Assets/Cell Configs/Drumfire Cell/Drumfire Cell Config.asset`
  - `Assets/_SO_Assets/Cell Configs/Drumfire Cell/Drumfire Cell Config.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Drumfire Cell/Drumfire Spawn Profile.asset`
  - `Assets/_SO_Assets/Cell Configs/Drumfire Cell/Drumfire Spawn Profile.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Switchyard Cell.meta`
  - … and 204 more

### `c59cbc3f8` — feat(switchback): the Dolphin-only gate race

_Claude, 2026-09-05 10:38:07 +0000_

```text
A course of randomly placed and randomly ORIENTED switch rings, scattered
through the cell and flown in ORDER by every pilot. The first DOMAIN whose
LEAD RUNNER threads the last gate wins. GameModes.Switchback = 45.

Four things here are platform-level rather than mode-local:

ORDERED GATES MAKE ONE REPLICATED INT CARRY A RACE. A pilot may only thread
their NEXT gate, so IRoundStats.SwitchesThreaded is simultaneously the score,
the progress bar, the index of the ring to test this frame, and the token the
server validates a report against. `gateIndex != stats.SwitchesThreaded`
rejects both a client claiming the last gate from the starting line and a
duplicate report of one already paid - no per-gate state, no bitmask, and
detection is one segment test per pilot rather than pilots x gates.

A DOMAIN'S SCORE IS NOT ALWAYS A SUM. Every pilot flies the same course, so
summing teammates would hand a two-pilot domain twice the course and the win
over a one-pilot domain that flew further. ScoringRuleSO.DomainValue is the new
seam - default SumByDomain (byte-for-byte the old behaviour everywhere),
overridden here to the new ScoringMetrics.BestByDomain. It is ONE virtual
rather than four overrides because a domain's score is read in FIVE places that
must never disagree (Remaining, ResolveWinner, ResolvePlacementOrder,
DomainDelta, and the HUD's own domain boxes); a mode that overrode only its end
condition would win on the lead runner while the score row showed the sum.
The consequence is deliberate: a teammate cannot add to your score, so team
play is interference - and the Dolphin's blast cone already debuffs a rival in
every mode, with the ammunition on the course because the vessel seeds its own
crystals in every scene.

"RANDOMLY ORIENTED" IS ONLY PLAYABLE IF IT IS CONSTRAINED BY CONSTRUCTION.
A gate faces the flow BISECTOR of its corner and the jitter that makes it
random is spent from what is LEFT of a presentation cap after the corner has
taken its half, so no gate is ever edge-on. The turn cap holds because the walk
only advances its heading when a gate is PLACED and BACKTRACKS when a wall
leaves no legal escape - rotating the heading between failed attempts is the
tempting shortcut and composes two 55 degree turns into a 110 degree hairpin
between two placed gates.

THE COURSE TRAVELS, THE SEED DOES NOT. The generator is deterministic on
purpose (a specified xorshift32; no System.Random, no UnityEngine.Random), but
the server broadcasts the geometry - a shared seed would rest on Mathf.Sin/Acos
agreeing to the last bit across Mono and IL2CPP, and one flipped branch inside
the walk yields a completely different course rather than a slightly different
one.

Detection is owner-detects / server-records, the platform's fourth use of the
pattern, gated on IsNetworkOwner rather than IsLocalUser so the host advances
every AI. The start is provably fair: pilots spawn on an equatorial ring and
gate 1 sits on that ring's pole. Gates are NEUTRAL switches and markers, not
mass - one renderer, zero colliders - and which ring is yours next is answered
by the per-viewer objective arrow rather than by repainting shared geometry.
Intensity is the COURSE (mouths 72->42, corners 45->60 deg), never the arena.

Verification: the shipped course generator was compiled and EXECUTED headless
over 400 seeds x 4 intensities - every cap holds, no seed fails to generate,
no gate leaves the shell, no two mouths come within a ring diameter, and every
corner clears the Dolphin's turning circle at boost (Dubins, R = 180.7u). All
new gameplay files compile against Unity-type stubs with every member bound.
author_switchback_assets.py --check, author_objective_icons.py --check,
check_conditional_compilation.py and validate_project.py all pass. NOT yet
opened in the editor - the in-editor checklist is in SWITCHBACK.md.
```

```text
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs  |   576 ++
 .../Controller/Arcade/Switchback/SwitchbackController.cs.meta         |    11 +
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackCourse.cs      |   332 +
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackCourse.cs.meta |    11 +
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackGateRing.cs    |   123 +
 .../_Scripts/Controller/Arcade/Switchback/SwitchbackGateRing.cs.meta  |    11 +
 .../Controller/Arcade/Switchback/SwitchbackObjectiveProvider.cs       |    42 +
 .../Controller/Arcade/Switchback/SwitchbackObjectiveProvider.cs.meta  |    11 +
 .../Controller/Arcade/TurnMonitors/SwitchbackGateTurnMonitor.cs       |    96 +
 .../Controller/Arcade/TurnMonitors/SwitchbackGateTurnMonitor.cs.meta  |    11 +
 Assets/_Scripts/Controller/Player/Player.cs                           |    32 +
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    10 +-
 Assets/_Scripts/Data/Enums/IRoundStats.cs                             |    15 +
 Assets/_Scripts/Data/Enums/RoundStats.cs                              |    26 +
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                           |    11 +
 Assets/_Scripts/Editor/EndConditionOverridesWindow.cs                 |    11 +-
 Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs          |    28 +-
 Assets/_Scripts/Tests/Editor/EnumIntegrityTests.cs                    |     6 +-
 Assets/_Scripts/Tests/Editor/GameDataSOTests.cs                       |     2 +
 Assets/_Scripts/Tests/Editor/IRoundStatsCleanupTests.cs               |     4 +
 Assets/_Scripts/Tests/Editor/SwitchbackCourseTests.cs                 |   228 +
 Assets/_Scripts/Tests/Editor/SwitchbackCourseTests.cs.meta            |    11 +
 Assets/_Scripts/UI/MiniGameHUD.cs                                     |     5 +
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |    15 +
 CLAUDE.md                                                             |    52 +-
 Docs/SCENES.md                                                        |     5 +
 ProjectSettings/EditorBuildSettings.asset                             |     3 +
 Tools/Build/author_objective_icons.py                                 |    29 +
 Tools/Build/author_switchback_assets.py                               |   498 ++
 53 files changed, 13481 insertions(+), 17 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 3253 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index bdd569d29..cd9b34177 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -61,6 +61,15 @@ namespace CosmicShore.Gameplay
             /// </summary>
             CombatPoints,
 
+            /// <summary>
+            /// Switchback's course progress. The one source here folded by a domain's BEST pilot
+            /// rather than its sum - every pilot flies the same course, so the deficit that
+            /// matters is how far your lead runner is behind theirs. Reading it as a sum would
+            /// tell a one-pilot domain it was miles behind a two-pilot one that had flown the
+            /// same distance.
+            /// </summary>
+            SwitchesThreaded,
+
             /// <summary>
             /// Joust's per-domain summed joust collisions. Joust's Score lands only at game end
             /// (winner a finish time, losers a sentinel - JoustScoringRuleSO.AssignScores), so
@@ -134,6 +143,8 @@ namespace CosmicShore.Gameplay
                     return ScoreDifferenceSource.CombatPoints;
                 case GameModes.Joust: // Score lands only at game end - jousts are the live stat
                     return ScoreDifferenceSource.Jousts;
+                case GameModes.Switchback: // Score lands only at game end - gates are the live stat
+                    return ScoreDifferenceSource.SwitchesThreaded;
                 default:
                     // The legacy composite/time-scored modes (Cellular Duel, Wildlife Blitz co-op,
                     // Freestyle, 2v2) accumulate Score live via TimePlayedScoring, so Score is
@@ -450,6 +461,10 @@ namespace CosmicShore.Gameplay
                     return ScoringMetrics.SumByDomain(gameData, ScoringMetric.LifeformsKilled, domain);
                 case ScoreDifferenceSource.Jousts:
                     return ScoringMetrics.SumByDomain(gameData, ScoringMetric.Jousts, domain);
+                case ScoreDifferenceSource.SwitchesThreaded:
+                    // BestByDomain, matching SwitchbackScoringRuleSO.DomainValue - the comeback
+                    // deficit and the score on the HUD above it must be the same quantity.
+                    return ScoringMetrics.BestByDomain(gameData, ScoringMetric.SwitchesThreaded, domain);
                 case ScoreDifferenceSource.Score:
                     float sum = 0f;
                     var list = gameData.RoundStatsList;
@@ -475,6 +490,7 @@ namespace CosmicShore.Gameplay
                 ScoreDifferenceSource.LifeformsKilled => true,
                 ScoreDifferenceSource.CombatPoints => true,
                 ScoreDifferenceSource.Jousts => true,
+                ScoreDifferenceSource.SwitchesThreaded => true,
                 ScoreDifferenceSource.Score => !useGolfRules,
                 _ => !useGolfRules
             };
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
index 46ae3233e..0f8efa5cf 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
@@ -72,9 +72,9 @@ namespace CosmicShore.Gameplay
                 var rule = gameData.ScoringRule;
                 if (rule != null)
                 {
-                    n_DomainSum0.Value = ScoringMetrics.SumByDomain(gameData, rule.Metric, GameDataSO.ActiveDomains[0]);
-                    n_DomainSum1.Value = ScoringMetrics.SumByDomain(gameData, rule.Metric, GameDataSO.ActiveDomains[1]);
-                    n_DomainSum2.Value = ScoringMetrics.SumByDomain(gameData, rule.Metric, GameDataSO.ActiveDomains[2]);
+                    n_DomainSum0.Value = rule.DomainValue(gameData, GameDataSO.ActiveDomains[0]);
+                    n_DomainSum1.Value = rule.DomainValue(gameData, GameDataSO.ActiveDomains[1]);
+                    n_DomainSum2.Value = rule.DomainValue(gameData, GameDataSO.ActiveDomains[2]);
                 }
                 yield return wait;
             }
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
new file mode 100644
index 000000000..ae0f05324
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -0,0 +1,336 @@
+# Switchback — Technical Documentation
+
+> **Naming.** `GameModes.Switchback = 45` is the code/data/enum identity, and the player-facing
+> `DisplayName` on `ArcadeGameSwitchback.asset` is **"Switchback"** too. A switchback is a
+> hairpin on a mountain road, and the mode is a course of them — it also contains the platform
+> fundamental it is built on, the **switch**.
+
+## Overview
+
```

</details>

### `eeca8a814` — fix(switchback): apply adversarial review findings

_Claude, 2026-09-05 10:58:21 +0000_

```text
Two review passes (scoring/blast-radius, netcode/detection, assets/platform
laws) over the Switchback branch. Nine findings, all fixed.

Scene / assets:
- The clone inherited Rampage's serialized comeback source (PrismsDestroyed).
  EnsureExists respects a scene-authored instance as-is, so every Switchback
  case in ElementalComebackSystem was unreachable and the comeback layer was
  fed a prism-destruction deficit against a rate sized for 0-20 gates - an
  always-on max elemental buff for whoever had blasted least flora.
- The clone also inherited Rampage's IntensityScaled crystal ladder (2xplayers
  down to 1), which made intensity mean two contradictory things and made the
  mode's whole interference layer ~8x rarer at the level the gates tighten.
  Flat PlayerCountPlusExtra +1 now, ladder flattened to match, both asserted.
- ScoreDifferenceSource had no explicit values and the new member was inserted
  mid-enum, renumbering Jousts 7 -> 8. Every member is pinned; Jousts keeps 7.

Correctness:
- Course generation failing left _course empty forever, RequestCourse_ServerRpc
  silently dropping every pull, and the match unable to end or expose a Ready
  button. It now backs off (halving the ask, floor 2) and the turn monitor
  publishes the course's OWN length, so the target can never name a gate that
  does not exist.
- The mode lays no prisms, so the connecting panel's arena-ready gate released
  before the course landed and gates flown then were lost silently and
  permanently. Bracketed with BeginArenaBuild/EndArenaBuild, plus a one-shot
  error if a turn runs with no rings.
- ReportSwitchThreaded_ServerRpc had no turn gate, so a crossing made during
  the round trip after the server froze results was credited and replicated
  over the results snapshot. Gated on IsTurnRunning.
- The course was built about the world origin while the spawn ring, membrane
  and nucleus are measured from the Cell. Offset by ResolveCellCentre before
  broadcast; the generator stays a pure function of its shell.

Per-pilot readouts and AI:
- Added ScoringRuleSO.RemainingForPlayer, defaulting to the pilot's domain
  remaining (the deliberate reading in every other mode). Switchback overrides
  it, so a trailing teammate no longer sees the lead runner's progress on their
  goal row, scoreboard line and defeat reveal.
- Installing an external target provider replaces AIPilot's crystal seeking
  outright, so a racing AI could never fire the mode's only interference
  weapon. Added a distance-budgeted on-the-way crystal detour: taken only when
  it costs less than aiCrystalDetourSlack of extra flying to the next gate,
  re-tested per frame so it cannot become an orbit.

Generator hardening: three registrations were insert-if-absent, so --check
passed whatever they had drifted to; they now SET their row. The course shell
is tied to the C# defaults and the 400-seed sweep, so retuning one without the
others fails the build. Dead DONOR_CELL_FILEID removed.

Verified: shipped course generator compiled and run over 400 seeds x 4
intensities (all contracts hold, 0 Dubins-infeasible); all mode files compile
against Unity stubs; author_switchback_assets.py --check OK (22 files);
author_objective_icons.py --check OK; check_conditional_compilation.py OK;
validate_project.py 0 errors.
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameSwitchback.unity            |  20 ++--
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs          |  35 +++---
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md                       |  63 ++++++++++-
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs            |  19 ++++
 Assets/_Scripts/Controller/Arcade/Scoring/SwitchbackScoringRuleSO.cs  |  21 +++-
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs  | 183 ++++++++++++++++++++++++++++++--
 .../Controller/Arcade/TurnMonitors/SwitchbackGateTurnMonitor.cs       |  39 +++++--
 Assets/_Scripts/Controller/Player/Player.cs                           |   7 ++
 Tools/Build/author_switchback_assets.py                               | 126 ++++++++++++++++++++--
 9 files changed, 461 insertions(+), 52 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 824 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index cd9b34177..3a3df7a94 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -41,25 +41,37 @@ namespace CosmicShore.Gameplay
         /// </summary>
         public enum ScoreDifferenceSource
         {
-            Score,
-            CrystalsCollected,
-            Goals,
-            PrismsDestroyed,
-            PrismsRemaining,
+            // Explicit values: this enum is SERIALIZED on every party-game scene's
+            // ElementalComebackSystem, so an inserted member silently renumbers every member
+            // after it and every scene authored against the old numbering starts reading a
+            // different stat. Never reorder; only append with the next free value.
+            Score = 0,
+            CrystalsCollected = 1,
+            Goals = 2,
+            PrismsDestroyed = 3,
+            PrismsRemaining = 4,
+
             /// <summary>
             /// Wildlife Liberation's fauna kills. Domain-aggregated like every other source
             /// here - the mode is a domain race, so a player's deficit is their TEAM's deficit
             /// against the leading colour. (A per-player variant of this source existed while
             /// the mode was briefly a free-for-all and was removed with it.)
             /// </summary>
-            LifeformsKilled,
+            LifeformsKilled = 5,
 
             /// <summary>
             /// Dog Fight's weighted gunnery score. A team source like every entry above
             /// LifeformsKilled - Dog Fight pools points per domain - so the trailing SIDE gets
             /// the buff, not the trailing individual.
             /// </summary>
-            CombatPoints,
+            CombatPoints = 6,
+
+            /// <summary>
+            /// Joust's per-domain summed joust collisions. Joust's Score lands only at game end
+            /// (winner a finish time, losers a sentinel - JoustScoringRuleSO.AssignScores), so
+            /// the Score source would read a flat zero deficit for the whole match.
+            /// </summary>
+            Jousts = 7,
 
             /// <summary>
             /// Switchback's course progress. The one source here folded by a domain's BEST pilot
@@ -68,14 +80,7 @@ namespace CosmicShore.Gameplay
             /// tell a one-pilot domain it was miles behind a two-pilot one that had flown the
             /// same distance.
             /// </summary>
-            SwitchesThreaded,
-
-            /// <summary>
-            /// Joust's per-domain summed joust collisions. Joust's Score lands only at game end
-            /// (winner a finish time, losers a sentinel - JoustScoringRuleSO.AssignScores), so
-            /// the Score source would read a flat zero deficit for the whole match.
-            /// </summary>
-            Jousts,
+            SwitchesThreaded = 8,
         }
 
         [Header("Config")]
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index ae0f05324..2ddd1bfda 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -74,6 +74,16 @@ would win on the lead runner while the score row above it showed the team's sum,
 system would compute a deficit against a third quantity. A domain's score is read in five places
 and they must never disagree.
 
+**Its mirror image is a second seam, because a PILOT's own readouts must not show the domain
+fold.** With the domain folded by its best pilot, a trailing teammate's goal row would read the
+ace's "12/20" while their objective arrow pointed at gate 4, and their scoreboard row would sit
+3 gates flown beside 8 left of a 20-gate course. `ScoringRuleSO.RemainingForPlayer(GameDataSO,
+IRoundStats)` is that seam, and it defaults to the pilot's DOMAIN remaining — which is the right
+answer in every other mode, where a pilot's objective genuinely IS the team's shared pile.
+Switchback overrides it; the turn monitor's display channel, the scoreboard's "N Gates Left" and
```

</details>

### `dc3dcb65c` — fix(arcade): grow the game grid so a new mode is not silently dropped

_Claude, 2026-09-05 13:56:56 +0000_

```text
Switchback shipped correctly registered and did not appear in the arcade.

ArcadeExploreView's grid is authored at a fixed size - 3 rows x 4 = 12 slots
in Menu_Main - and the populate loop was bounded by it, so a roster larger
than the grid truncated silently: the alphabetically-last modes stopped
existing in the arcade, with no error and no gap in the grid to notice.
Menu_Main was sitting at exactly 12 renderable cards (13 games minus the
Maelstrom, which the grid deliberately excludes), so adding a 14th game made
13 renderable and pushed one off the end.

- EnsureGridCapacity clones the last AUTHORED row until the roster fits, so a
  new row inherits its layout group, sizing and card wiring rather than
  needing any of it re-authored, and a scene that resizes its grid keeps
  working with nothing in code to update.
- The loop's third bound, GameList.Games.Count, is removed. It was a ceiling
  on a different list - it counts the Maelstrom and any inventory-filtered
  card - so it could only ever mask the real bound.
- The roster is now resolved before the grid is walked, because the grid has
  to be big enough to hold it. Same computation, moved.

The row arithmetic is extracted as a pure RowsNeeded and asserted in
ArcadeGridCapacityTests over an exhaustive sweep, because an off-by-one there
does not throw - it hides a game mode.
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md              |  11 +++++
 Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs      |  64 +++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs.meta |  11 +++++
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs                | 104 ++++++++++++++++++++++++++++++++---------
 4 files changed, 167 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 223 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index 2ddd1bfda..e24117f73 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -366,6 +366,17 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
 
 ## Known limitations / follow-ups
 
+- **The arcade grid had to grow to show this card.** `ArcadeExploreView`'s grid is AUTHORED at a
+  fixed size — 3 rows × 4 = 12 slots in Menu_Main — and the populate loop was bounded by it, so a
+  roster larger than the grid truncated **silently**: the alphabetically-last modes simply stopped
+  existing in the arcade, with no error and no gap in the grid to notice. Menu_Main was sitting at
+  exactly 12 renderable cards (13 games minus the Maelstrom, which the grid deliberately excludes),
+  so adding Switchback made 13 and pushed Wildlife Liberation off the end. `EnsureGridCapacity`
+  clones the last authored row until the roster fits, and the loop's third bound —
+  `GameList.Games.Count`, a ceiling on a *different* list — is removed. Arithmetic is asserted in
+  `ArcadeGridCapacityTests` rather than eyeballed, because an off-by-one there does not throw, it
+  hides a game mode.
+
 - **20 gates is unmeasured.** Chosen from the arithmetic (≈9.4k units of course; 2–3 minutes at
   realistic Dolphin speeds), not from a playtest. It is one editor field.
 - **No toasts.** No `GameToastConfigSO`, so no "GATE 12/20" or lead-change announcement. The
diff --git a/Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs b/Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs
new file mode 100644
index 000000000..309af954b
--- /dev/null
+++ b/Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs
@@ -0,0 +1,64 @@
+using CosmicShore.UI;
+using NUnit.Framework;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// The arcade grid is AUTHORED at a fixed size (3 rows x 4 = 12 slots in Menu_Main) and the
+    /// populate loop is bounded by it, so a roster that outgrows it truncates SILENTLY - the
+    /// alphabetically-last modes simply stop existing in the arcade, with no error and no gap in
+    /// the grid to notice. Shipping Switchback is what crossed the line (13 renderable cards into
+    /// 12 slots), which is why the arithmetic that grows the grid is asserted here rather than
+    /// eyeballed in a scene.
+    /// </summary>
+    public class ArcadeGridCapacityTests
+    {
+        [Test]
+        public void FittingRoster_AddsNoRows()
+        {
+            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 12));
+            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 5));
+            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 0));
+        }
+
+        [Test]
+        public void OneCardOverflow_AddsExactlyOneRow()
+        {
+            // The shipped case: 12 authored slots, 13 renderable cards.
+            Assert.AreEqual(1, ArcadeExploreView.RowsNeeded(12, 4, 13));
+        }
+
+        [Test]
+        public void PartialRowStillCountsAsAWholeRow()
+        {
+            Assert.AreEqual(1, ArcadeExploreView.RowsNeeded(12, 4, 16));
+            Assert.AreEqual(2, ArcadeExploreView.RowsNeeded(12, 4, 17));
+            Assert.AreEqual(2, ArcadeExploreView.RowsNeeded(12, 4, 20));
+            Assert.AreEqual(3, ArcadeExploreView.RowsNeeded(12, 4, 21));
+        }
+
+        [Test]
+        public void ResultAlwaysClosesTheGap()
+        {
+            for (int perRow = 1; perRow <= 6; perRow++)
+            for (int capacity = 0; capacity <= 24; capacity++)
+            for (int required = 0; required <= 40; required++)
+            {
+                int rows = ArcadeExploreView.RowsNeeded(capacity, perRow, required);
+                Assert.GreaterOrEqual(capacity + rows * perRow, required,
+                    $"capacity {capacity}, perRow {perRow}, required {required} left a deficit");
+                // And never adds a row it did not need.
+                if (rows > 0)
+                    Assert.Less(capacity + (rows - 1) * perRow, required,
```

</details>

### `fe817adfc` — feat(sparrow): missiles rearm on prism kills, crystals ward, proximity fuze + warhead

_Claude, 2026-09-05 14:23:42 +0000_

```text
The skyburst's economy inverts: rockets are no longer bought by flying to an omni
crystal, they are bought by taking the arena apart. Every HOSTILE prism a Sparrow
destroys puts 0.02 back in the rack (25 prisms per rocket), and the crystal changes
jobs to grant 8s of elemental-debuff immunity instead.

VesselRearmOnPrismDestruction listens on the prism-destroyed SOAP channel rather
than hanging off an impact effect, because a Sparrow destroys prisms five ways and
the biggest one is invisible to effects: while the spatial index is up, a blast
resolves prism damage through the Burst batch path, which dispatches no per-prism
effects at all. A rule enforced at one producer can only ever see that producer.
Only hostile mass pays, via StatsManager's own environment-friendliness rule.

VesselTimedElementalWard is the event-driven half of the platform's debuff immunity
- the sibling of the condition-driven VesselElementalImmunity, which cannot express
a window that opens on an event and closes on a clock. Grants refresh rather than
stack, and are revoked on disable. Checked against the mono-vessel-mode rule: none
of Dog Fight, Salvo or Wildlife Liberation scores on an event a ward can deny.

Missiles now carry a PROXIMITY FUZE at 20x their own live hit radius (76u at resting
MASS), tripping only on an opposing vessel or a living fauna's heart - never a prism,
never flora, never own-domain. An explicit overlap rather than a second trigger: a
150-unit trigger would mint thousands of discarded PhysX pairs per frame, would arrive
through AcceptImpactee and score a missile strike on a near miss, and could not see
both vessels and crystals through the collision matrix. The arming delay is emergent:
the fuze is a multiple of the round's current size and it leaves the bay at a
twentieth of its grown one.

The existing explosion is untouched. AOEMissileWarhead is a second blast at 25x the
same base (95u) that debuffs pilots and jousts creatures while touching no mass -
AOEExplosion.affectsPrisms, honoured in one place so every blast shape respects it.
The fauna kill runs the Squirrel's own jousted death (heart freed, body unravels
outward, skeleton left standing), with two deliberate differences: nobody takes the
heart, and there is no speed contest. Spawned by ProjectileDetonatorSO, the one place
every detonation path funnels through, so it cannot fire on some and not others.

Verified without Unity: all new files and every edited method body type-check against
faithful stubs (negative-controlled); nine new edit-mode asset gates execute against
the shipped prefabs and fail correctly when the fuze/warhead ordering is inverted or
the warhead is allowed to touch prisms; zero new orphan keys or dangling fileIDs in
any edited prefab.
```

```text
 .../VesselContainers/SparrowImpactorDataContainer.asset               |   2 +-
 .../MissileWarheadWitherLifeformEffect.asset}                         |  12 +-
 .../MissileWarheadWitherLifeformEffect.asset.meta                     |   8 ++
 .../Vessel Crystal Effects/SparrowVesselWardByCrystalEffect.asset     |  15 +++
 .../SparrowVesselWardByCrystalEffect.asset.meta                       |   8 ++
 .../MissileWarheadDebuffByExplosionEffect.asset                       |  17 +++
 .../MissileWarheadDebuffByExplosionEffect.asset.meta                  |   8 ++
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md                         |  20 +++
 Assets/_Scripts/Controller/Arcade/SALVO.md                            |  16 +++
 .../ImpactEffects/Containers/ExplosionImpactorDataContainerSO.cs      |   9 ++
 .../Abstract Effect Types/ExplosionLifeformCrystalEffectSO.cs         |  18 +++
 .../Abstract Effect Types/ExplosionLifeformCrystalEffectSO.cs.meta    |  11 ++
 .../ExplosionWitherLifeformByCrystalEffectSO.cs                       |  71 ++++++++++
 .../ExplosionWitherLifeformByCrystalEffectSO.cs.meta                  |  11 ++
 .../Controller/ImpactEffects/EffectsSO/ProjectileDetonatorSO.cs       |  41 ++++++
 .../EffectsSO/Vessel Crystal Effects/VesselWardByCrystalEffectSO.cs   |  63 +++++++++
 .../Vessel Crystal Effects/VesselWardByCrystalEffectSO.cs.meta        |  11 ++
 .../_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs  | 119 +++++++++++++++--
 Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs                |  19 +++
 Assets/_Scripts/Controller/Projectiles/Projectile.cs                  | 226 +++++++++++++++++++++++++++++++-
 .../Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md         | 193 ++++++++++++++++++++++++++-
 Assets/_Scripts/Controller/Vessel/VesselRearmOnPrismDestruction.cs    | 128 ++++++++++++++++++
 .../_Scripts/Controller/Vessel/VesselRearmOnPrismDestruction.cs.meta  |  11 ++
 Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs         | 125 ++++++++++++++++++
 Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs.meta    |  11 ++
 Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs               | 203 ++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs.meta          |  11 ++
 CLAUDE.md                                                             |  14 +-
 Docs/ElementalAbilitySystem/FLEET_MAPS.md                             |  22 +++-
 36 files changed, 1653 insertions(+), 42 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1426 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
index a555bc0cc..7b92e5aec 100644
--- a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
+++ b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
@@ -230,6 +230,26 @@ effects. Today the container holds *only* the scoring effect, so outside Dog Fig
 change is that `BulletHitsLanded` / `MissileHitsLanded` start accumulating (worth 0 points
 everywhere else). Verify in-editor rather than assuming — checklist item 11.
 
+## The missile got a proximity fuze (2026-09) — this mode gets faster
+
+A skyburst now detonates when an opposing vessel comes within **20× its own hit radius** (~76 u at
+resting MASS) rather than only on contact. A missile hit is worth 50 points here and the mode runs
+to 90, so the practical effect is that the EXISTING blast — the conic/sphere pair, radius up to 85 —
+routinely catches a pilot the rocket would previously have flown past. Expect shorter matches until
+this is retuned.
+
+**Scoring is unchanged, deliberately.** The new warhead blast (which debuffs pilots and kills
+wildlife) carries **no** `VesselCombatHitByExplosionEffectSO`, so it does not add a second
+50-point event; a rocket still scores once, through its direct hit or the conic blast, sharing one
+`VesselCombatHitLatch` window. The lever if this proves too fast is
+`proximityFuzeRadiusMultiplier` on `SkyBurstProjectile.prefab`. Full mechanics:
+`_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md`.
+
+Note the Sparrow's omni crystals also changed meaning: they no longer refill the missile tank
+(prism destruction does that now) and instead grant 8 s of elemental-debuff immunity. That does not
+touch this mode's scoring — `VesselCombatHitByMissile*` runs with `requireDebuffableVictim: false`,
+so a warded pilot is still fully scoreable.
+
 ## The Boneyard (the arena)
 
 `SpawnableBoneyard : CellEnvironmentSpawnableBase`, seed 41, deterministic per seed like every
diff --git a/Assets/_Scripts/Controller/Arcade/SALVO.md b/Assets/_Scripts/Controller/Arcade/SALVO.md
index 0231866c0..870486e0d 100644
--- a/Assets/_Scripts/Controller/Arcade/SALVO.md
+++ b/Assets/_Scripts/Controller/Arcade/SALVO.md
@@ -1,5 +1,21 @@
 # Salvo — Technical Documentation
 
+
+> **⚠ Changed under this mode's feet (2026-09).** The premise below — *"the tank never regenerates
+> and the only refuel is an omni crystal"* — is no longer the whole truth. The Sparrow's missiles
+> now also recharge by **destroying hostile prisms** (0.02 per prism, 25 prisms per rocket:
+> `VesselRearmOnPrismDestruction`), which in Salvo is the mode's own objective. So the crystal run
+> is no longer the only way to reload, and the crystal-run rhythm this mode is built around is
+> correspondingly weaker.
+>
+> **The wingman reload is untouched** and is still the reason to play together: an omni crystal
+> collected by any pilot fills the whole domain's bays instantly, where prism-destruction pays one
+> pilot 2% of a rack at a time. But the balance between the two wants a playtest, and if the
+> crystal line needs to matter more the lever is `ammoPerPrism` on `Sparrow.prefab`, not this
+> mode. Missiles also now carry a proximity fuze and a second, creature-killing blast — see
+> `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md`.
+
+
 > **Naming.** `GameModes.Salvo = 44` is the code/data/enum identity, and the player-facing
 > `DisplayName` on `ArcadeGameSalvo.asset` is **"Salvo"** too. Do not rename the enum, the
 > controller, the scene, or this file (the Maelstrom/"Maelstrom" precedent covers a display
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Containers/ExplosionImpactorDataContainerSO.cs b/Assets/_Scripts/Controller/ImpactEffects/Containers/ExplosionImpactorDataContainerSO.cs
index 00f80d411..6c4229660 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Containers/ExplosionImpactorDataContainerSO.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Containers/ExplosionImpactorDataContainerSO.cs
@@ -19,5 +19,14 @@ namespace CosmicShore.Gameplay
         /// the same crystal must not start minting Astro League balls.
         /// </summary>
         public ExplosionCrystalEffectSO[] explosionCrystalEffects;
+
+        /// <summary>
+        /// What this blast does to a LIVING lifeform's embedded crystal — its heart. Empty on
+        /// every blast in the fleet except the Sparrow's missile warhead, which jousts the
+        /// creatures it engulfs (the Squirrel's own death, reached by an explosion). Authored per
+        /// blast for the same reason the crystal row is: a Dolphin cone sweeping through a shoal
+        /// must not start killing it just because a missile does.
+        /// </summary>
+        public ExplosionLifeformCrystalEffectSO[] explosionLifeformCrystalEffects;
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/ProjectileDetonatorSO.cs b/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/ProjectileDetonatorSO.cs
index 7be212a26..df6c22d01 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/ProjectileDetonatorSO.cs
```

</details>

### `9db61a89d` — feat(arcade): add Hijack (45) - the Urchin rail heist in the Switchyard

_Claude, 2026-09-05 14:28:28 +0000_

```text
The Urchin's three shipped verbs - attach to a rail, launch off its end, steal
clusters of prisms - had no arena built to reward them. Hijack is that arena and
adds no vessel mechanics at all.

Three great-circle RAILS of radius 900 ring a hollow core, 8 stations each,
meeting at 6 big spiny BURRS on the axis crossings plus 12 small ones mid-arc.
Every rail is painted in three domain thirds (fast where it is yours, a stealing
crawl where it is not) and every burr wears one colour. First DOMAIN to steal
1500 prisms wins.

It is the first mode scored on OWNERSHIP rather than destruction: nothing here is
ever destroyed, mass only changes hands - which is what lets a whole competitive
mode run inside the conserved-mass law with no food web, no respawn and no
despawn. ScoringMetric.PrismsStolen reads IRoundStats.PrismStolen, a stat
StatsManager and Player.ReportPrismStolen_ServerRpc have been accumulating in
every mode since long before a mode read it, so the metric needed zero new
gameplay plumbing.

THE LAUNCH IS AIMED BY GEOMETRY, NOT BY A BONUS. A circle's tangent at g short of
a station passes through that station's radial at R/cos g, R*tan g further on, so
every burr centre sits at exactly 900/cos 12.5 = 921.9u and a pilot who grinds a
rail to its end and does not steer flies straight into the cluster 199.5u ahead.
A per-launch bonus would score the record of a manoeuvre rather than its effect.

Three findings worth carrying, all caught by the offline model before any C# ran:

  * A launch contract is a claim about TANGENTS, which looks right in a diagram
    and is wrong in the build. Rail prism spacing is DERIVED so the 40 prisms
    span the arc endpoint-to-endpoint (8.0554u); a round 8.0 centres 312u inside
    a 314u arc, insets the terminal prism ~1u and tilts the launch 0.32 degrees
    off the burr.
  * Resolving identity by ROUNDING a float is a tolerance with a cliff in the
    middle of it. A quantize-to-whole-units key matching a rail to its burr was
    written first and rejected when the model measured a burr coordinate sitting
    0.049 of a unit from a .5 boundary. Proximity has no boundary to land on.
  * A generator written CLOSED FORM can be MIRRORED rather than estimated. There
    is no System.Random draw in BuildEnvironment, so hijack_budget.py reproduces
    it exactly and author_hijack_assets.py imports it - the cell's
    PhaseThresholds cannot drift from the arena that has to satisfy them.

No food web, and the reason is the comeback: in a nucleus-less cell herbivores
eat opposing-domain mass and the leader's colour is the most abundant, so a swarm
would preferentially eat whatever the trailing team had just stolen.

One authored field is load-bearing: ram 0 -> 1 on Urchin.prefab's AIPilot (the
Rhino's shipped value). AIPilot writes XDiff = (LookingAtCrystal && ram) ? 1 :
throttle and ReadThrottle is SIGNED around a 0.5 rest, so the authored
defaultThrottle 0.6 reads as 30 u/s on a friendly rail - below the vessel's own
50 u/s cruise. An AI Urchin would grind slower than it flies.

Collider budget: every prism is PrismKind.Plain, zero always-on mesh colliders
authored. Peak arena 9,930 prisms / 536k volume at intensity 4, the same order as
the Boneyard. The one uncapped source is a MASS-5 pilot's own ride armour.

Verification status: NOT verified in the Unity editor - built headlessly. The
nine checks a human has to run are listed in HIJACK.md section 9, load-bearing
first (the scene opens, stealing scores, the launch is aimed without steering,
the speed cliff reads, a burr rolls, the win banner, the AI plays, the comeback,
and the Urchin freestyle regression).
```

```text
 Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity                | 10494 ++++++++++++++++++++++++++++++
 Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity.meta           |     7 +
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs          |    15 +
 Assets/_Scripts/Controller/Arcade/HIJACK.md                           |   346 +
 Assets/_Scripts/Controller/Arcade/HijackController.cs                 |   507 ++
 Assets/_Scripts/Controller/Arcade/HijackController.cs.meta            |    11 +
 Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs          |   112 +
 Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs.meta     |    11 +
 Assets/_Scripts/Controller/Arcade/Scoring/HijackScoringRuleSO.cs      |    60 +
 Assets/_Scripts/Controller/Arcade/Scoring/HijackScoringRuleSO.cs.meta |    11 +
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs           |     1 +
 .../_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs |    82 +
 .../Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs.meta     |    11 +
 Assets/_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs  |   142 +
 .../Controller/Environment/MiniGameObjects/HijackYard.cs.meta         |    11 +
 .../Controller/Environment/MiniGameObjects/SpawnableSwitchyard.cs     |   492 ++
 .../Environment/MiniGameObjects/SpawnableSwitchyard.cs.meta           |    11 +
 Assets/_Scripts/Data/Enums/GameModes.cs                               |    12 +-
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                           |    11 +
 Assets/_Scripts/Editor/EndConditionOverridesWindow.cs                 |    11 +-
 Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs          |    25 +-
 Assets/_Scripts/Tests/Editor/EnumIntegrityTests.cs                    |     8 +-
 Assets/_Scripts/UI/MiniGameHUD.cs                                     |     2 +
 CLAUDE.md                                                             |    68 +-
 Docs/SCENES.md                                                        |     3 +
 ProjectSettings/EditorBuildSettings.asset                             |     3 +
 Tools/Build/author_hijack_assets.py                                   |   766 +++
 Tools/Build/author_objective_icons.py                                 |    27 +
 Tools/Build/hijack_budget.py                                          |   435 ++
 59 files changed, 14402 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 3452 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index bdd569d29..b522986e6 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -67,6 +67,16 @@ namespace CosmicShore.Gameplay
             /// the Score source would read a flat zero deficit for the whole match.
             /// </summary>
             Jousts,
+
+            /// <summary>
+            /// Hijack's per-domain summed prisms STOLEN. A team source like every entry above:
+            /// the mode is a domain race and its Score lands only at game end (winner a finish
+            /// time, losers a sentinel), so the Score source would read a flat zero deficit for
+            /// the whole match. Worth naming separately from PrismsDestroyed even though both
+            /// count prisms - nothing is destroyed in Hijack, so the destruction stat is a flat
+            /// zero there and would silently disable the comeback layer.
+            /// </summary>
+            PrismsStolen,
         }
 
         [Header("Config")]
@@ -134,6 +144,8 @@ namespace CosmicShore.Gameplay
                     return ScoreDifferenceSource.CombatPoints;
                 case GameModes.Joust: // Score lands only at game end - jousts are the live stat
                     return ScoreDifferenceSource.Jousts;
+                case GameModes.Hijack: // Score lands only at game end - steals are the live stat
+                    return ScoreDifferenceSource.PrismsStolen;
                 default:
                     // The legacy composite/time-scored modes (Cellular Duel, Wildlife Blitz co-op,
                     // Freestyle, 2v2) accumulate Score live via TimePlayedScoring, so Score is
@@ -450,6 +462,8 @@ namespace CosmicShore.Gameplay
                     return ScoringMetrics.SumByDomain(gameData, ScoringMetric.LifeformsKilled, domain);
                 case ScoreDifferenceSource.Jousts:
                     return ScoringMetrics.SumByDomain(gameData, ScoringMetric.Jousts, domain);
+                case ScoreDifferenceSource.PrismsStolen:
+                    return ScoringMetrics.SumByDomain(gameData, ScoringMetric.PrismsStolen, domain);
                 case ScoreDifferenceSource.Score:
                     float sum = 0f;
                     var list = gameData.RoundStatsList;
@@ -475,6 +489,7 @@ namespace CosmicShore.Gameplay
                 ScoreDifferenceSource.LifeformsKilled => true,
                 ScoreDifferenceSource.CombatPoints => true,
                 ScoreDifferenceSource.Jousts => true,
+                ScoreDifferenceSource.PrismsStolen => true,
                 ScoreDifferenceSource.Score => !useGolfRules,
                 _ => !useGolfRules
             };
diff --git a/Assets/_Scripts/Controller/Arcade/HIJACK.md b/Assets/_Scripts/Controller/Arcade/HIJACK.md
new file mode 100644
index 000000000..8702a0fad
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/HIJACK.md
@@ -0,0 +1,346 @@
+# Hijack — the Urchin heist race (`GameModes.Hijack = 45`)
+
+**Urchin-only. First DOMAIN to steal 1,500 prisms wins.** Nothing in this mode is ever
+destroyed: mass only changes hands.
+
+Three great-circle **rails** ring a hollow core, meeting at spiny **burrs** of raw prism where
+the rings cross, with twelve smaller burrs strung along the arcs. Every rail is painted in three
+domain thirds and every burr wears one colour, so nothing in the yard is anyone's for long. You
+latch onto a rail and grind it — **150 u/s where it wears your colour, a stealing crawl at 10
+where it does not** — spike the road ahead to make it yours, fly off the open end at full grind
+speed straight into the burr that rail points at, and rake it with a chain cascade. Then bank
+onto the next rail before a rival takes it back.
+
+---
+
+## 1. Why this mode exists
+
+The Urchin's three verbs — **attach to a rail**, **launch off its end**, **steal clusters of
+prisms** — are all shipped platform behaviour and none of them had an arena built to reward
+them. Hijack is that arena, and it adds no new vessel mechanics at all: every verb below is the
+Urchin's own, applied to geometry shaped to invite it.
+
+| verb | the vessel's own machinery | what the arena does about it |
+|---|---|---|
+| ATTACH | `TrailFollower` 1D grind, routed by `PrismscapeTopology.DimensionOf` | 24 open ribbons, so there is always one in reach |
+| CONVERT | `GunVesselTransformer.ApplyPrismscapePayoff` steals every hostile prism ridden | every rail is three domain thirds, so a raid is always available and always slow until you spike it |
+| LAUNCH | `GunVesselTransformer.LaunchOffRibbonEnd` + carried speed | every rail's far end is a real end, and a burr sits exactly on the tangent it throws you along |
```

</details>

### `1f8151058` — feat(arcade): give Hijack's metric the launch panel's objective icon too

_Claude, 2026-09-05 14:30:34 +0000_

```text
`ModeControlsLibrarySO.ObjectiveIcons` is a SECOND metric -> sprite table,
separate from `ObjectiveIconSet`: the in-game goal stack reads one, the arcade
card's objective box and its micro toast read the other. A metric missing from
this one "draws text alone, which is honest rather than broken", but the editor's
Mode Map window flags the gap and this metric has purpose-drawn art already.

Both tables now point at the same glyph, so the card and the HUD cannot disagree
about what the objective looks like. Authored by the generator with SET
semantics, so a re-run repairs a hand-edit, and asserted before writing.
```

```text
 Assets/Resources/ModeControlsLibrary.asset |  2 ++
 Tools/Build/author_hijack_assets.py        | 34 ++++++++++++++++++++++++++++++----
 2 files changed, 32 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Tools/Build/author_hijack_assets.py b/Tools/Build/author_hijack_assets.py
index a7724ba81..e95ec55aa 100644
--- a/Tools/Build/author_hijack_assets.py
+++ b/Tools/Build/author_hijack_assets.py
@@ -502,7 +502,28 @@ else:
 emit(ICON_PATH, icons)
 
 
-# ── 8. Register the card in the party-games list ────────────────────────────
+# ── 8. The launch panel's own metric icon ──────────────────────────────────
+# A SECOND metric -> sprite table, read by the arcade card's objective box and its micro toast
+# (ModeControlsLibrarySO.IconForMetric). It is separate from ObjectiveIconSet, which the in-game
+# goal stack reads, and a metric missing from it "draws text alone, which is honest rather than
+# broken" - but the editor's Mode Map window flags the gap, and this metric has purpose-drawn art
+# already. Points at the same glyph as the goal row, so the card and the HUD cannot disagree
+# about what the objective looks like. SET semantics, so a re-run repairs a hand-edit.
+CONTROLS_PATH = "Assets/Resources/ModeControlsLibrary.asset"
+controls = read(CONTROLS_PATH)
+METRIC_ICON = (f"  - Metric: 9\n"
+               f"    Icon: {{fileID: {SPRITE_FILEID}, guid: {EXISTING['ObjectiveIconPrismsStolen']}, type: 3}}\n")
+existing_icon = re.search(r"  - Metric: 9\n    Icon: \{[^}]*\}\n", controls)
+if existing_icon:
+    controls = controls.replace(existing_icon.group(0), METRIC_ICON, 1)
+else:
+    anchor = re.search(r"(  ObjectiveIcons:\n(?:  - Metric: \d+\n    Icon: \{[^}]*\}\n)+)", controls)
+    assert anchor, "ObjectiveIcons block not found in ModeControlsLibrary.asset"
+    controls = controls.replace(anchor.group(1), anchor.group(1) + METRIC_ICON, 1)
+emit(CONTROLS_PATH, controls)
+
+
+# ── 9. Register the card in the party-games list ────────────────────────────
 LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
 games = read(LIST_PATH)
 entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameHijack']}, type: 2}}\n"
@@ -512,7 +533,7 @@ if entry not in games:
 emit(LIST_PATH, games)
 
 
-# ── 9. Always-unlocked so the card is clickable on a fresh account ──────────
+# ── 10. Always-unlocked so the card is clickable on a fresh account ──────────
 PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
 prog = read(PROG_PATH)
 if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 45\n", prog, re.M) is None:
@@ -521,7 +542,7 @@ if re.search(r"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - 45\n", prog, re.M) is
 emit(PROG_PATH, prog)
 
 
-# ── 10. Build settings ──────────────────────────────────────────────────────
+# ── 11. Build settings ──────────────────────────────────────────────────────
 BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
 build = read(BUILD_PATH)
 if "MinigameHijack.unity" not in build:
@@ -535,7 +556,7 @@ if "MinigameHijack.unity" not in build:
 emit(BUILD_PATH, build)
 
 
-# ── 11. End-game condition target ───────────────────────────────────────────
+# ── 12. End-game condition target ───────────────────────────────────────────
 # SET semantics, not add-if-absent (the Dog Fight generator's lesson: an insert-only key left
 # the asset on a stale number after a target retune).
 END_PATH = "Assets/Resources/EndConditionOverrides.asset"
@@ -624,6 +645,11 @@ if SPAWN_RING_RADIUS >= budget.MEMBRANE_RADIUS:
     errors.append(f"the spawn ring {SPAWN_RING_RADIUS}u is outside the "
                   f"{budget.MEMBRANE_RADIUS:.0f}u membrane")
 
+ctrl = files[CONTROLS_PATH]
+if ctrl.count("  - Metric: 9\n") != 1:
+    errors.append("ModeControlsLibrary must carry exactly one Metric 9 objective icon - the "
+                  "arcade card's objective box reads it")
+
 # Urchin-only must be a SINGLE entry, or the clamps let another hull through
 arcade = files["Assets/_SO_Assets/Games/ArcadeGameHijack.asset"]
 vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", arcade, re.M)
```

</details>

### `5ea727d64` — perf(hijack): stop the objective arrow walking every burr four times a second

_Claude, 2026-09-05 14:31:48 +0000_

```text
`NearestHostileBurr` asked all 18 burrs "have you got anything left?" and counted
every prism in each - up to ~20k prism reads per scan at intensity 4, on the main
thread, at 4 Hz, to place a HUD arrow.

Two changes, both about asking a cheaper question. `HasHostileMass` answers the
yes/no the arrow actually needs and EARLY-OUTS on the first hostile prism, which
is the normal case; the exact count stays available as `HostileMassAt` for the
AI's rail scoring, which runs once per pilot per three seconds on the server.
And the scan now tests DISTANCE FIRST, skipping any burr that cannot beat the
best found so far - so it normally walks two or three rather than eighteen.

The full walk survives only for a burr the pilot has already emptied, which is
the rare case and the one where the answer has to be exact.
```

```text
 Assets/_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs | 35 +++++++++++++++++++++++++++++++---
 1 file changed, 32 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs b/Assets/_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs
index 6451f28c2..cc776fa04 100644
--- a/Assets/_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs
+++ b/Assets/_Scripts/Controller/Environment/MiniGameObjects/HijackYard.cs
@@ -125,16 +125,45 @@ namespace CosmicShore.Gameplay
             return hostile;
         }
 
-        /// <summary>Nearest burr holding at least one prism this pilot could take, or -1.</summary>
+        /// <summary>
+        /// Is there anything left in this burr for <paramref name="domain"/> to take? The same
+        /// question as <see cref="HostileMassAt"/> without the count, and it EARLY-OUTS on the
+        /// first hostile prism - which matters because the answer is normally yes and the caller
+        /// is a HUD readout, not a scoring path.
+        /// </summary>
+        public bool HasHostileMass(int index, Domains domain)
+        {
+            var list = _burrs[index].Trail?.TrailList;
+            if (list == null) return false;
+
+            for (int i = 0; i < list.Count; i++)
+            {
+                var prism = list[i];
+                if (prism && prism.Domain != domain) return true;
+            }
+            return false;
+        }
+
+        /// <summary>
+        /// Nearest burr holding at least one prism this pilot could take, or -1.
+        ///
+        /// <para>DISTANCE FIRST, then hostility. A burr is up to 1,143 prisms and there are 18 of
+        /// them, so asking all 18 "have you got anything?" is ~20k prism reads; asking only the
+        /// ones that could still WIN is normally two or three, and each of those early-outs on
+        /// its first hostile prism. The full walk only happens for a burr the pilot has already
+        /// emptied, which is the rare case and the one where the answer has to be exact.</para>
+        /// </summary>
         public int NearestHostileBurr(Vector3 from, Domains domain)
         {
             int best = -1;
             float bestSqr = float.MaxValue;
             for (int i = 0; i < _burrs.Count; i++)
             {
-                if (HostileMassAt(i, domain) <= 0) continue;
                 float sqr = (BurrCentre(i) - from).sqrMagnitude;
-                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
+                if (sqr >= bestSqr) continue;
+                if (!HasHostileMass(i, domain)) continue;
+                bestSqr = sqr;
+                best = i;
             }
             return best;
         }
```

</details>

### `64a11503e` — chore(hijack): author HIJACK.md's .meta with the rest of the mode's assets

_Claude, 2026-09-05 14:33:02 +0000_

```text
`Tools/CI/validate_project.py` flagged it: a doc without a .meta is an asset
whose GUID Unity mints fresh on first import, which is a DIFFERENT guid on every
machine and breaks any reference to it. Every other mode reference doc carries
one; this one now comes from the generator, so it is deterministic like the rest.
```

```text
 Assets/_Scripts/Controller/Arcade/HIJACK.md.meta |  7 +++++++
 Tools/Build/author_hijack_assets.py              | 10 ++++++++++
 2 files changed, 17 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Tools/Build/author_hijack_assets.py b/Tools/Build/author_hijack_assets.py
index e95ec55aa..368278930 100644
--- a/Tools/Build/author_hijack_assets.py
+++ b/Tools/Build/author_hijack_assets.py
@@ -70,6 +70,7 @@ G_ASSET = {
     "HijackScoringRule":      guid("asset/HijackScoringRule"),
     "SwitchyardSpawnProfile": guid("asset/SwitchyardSpawnProfile"),
     "MinigameHijack.unity":   guid("asset/MinigameHijack.unity"),
+    "HIJACK.md":              guid("asset/HIJACK.md"),
 }
 for _i in INTENSITIES:
     G_ASSET[f"SpawnableSwitchyard{_i}.prefab"] = guid(f"asset/SpawnableSwitchyard{_i}.prefab")
@@ -173,6 +174,11 @@ def prefab_meta(g: str) -> str:
             f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
 
 
+def text_meta(g: str) -> str:
+    return (f"fileFormatVersion: 2\nguid: {g}\nTextScriptImporter:\n  externalObjects: {{}}\n"
+            f"  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
+
+
 def scene_meta(g: str) -> str:
     return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
             f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
@@ -202,6 +208,10 @@ SCRIPT_PATHS = {
 for k, p in SCRIPT_PATHS.items():
     emit(p + ".meta", meta(G_SCRIPT[k]))
 
+# The mode reference doc is an ASSET too - without a .meta Unity mints a fresh GUID on first
+# import, which is a different GUID on every machine and breaks any reference to it.
+emit("Assets/_Scripts/Controller/Arcade/HIJACK.md.meta", text_meta(G_ASSET["HIJACK.md"]))
+
 
 # ── 2. SpawnableSwitchyard prefabs - ONE VARIANT PER INTENSITY ──────────────
 # INTENSITY IS BURR MASS AND NOTHING ELSE. The 24-rail network, the ring radius, the launch gaps
```

</details>

### `b2b63c8eb` — fix(arcade): grow the scroll content with the grid, not just the grid

_Claude, 2026-09-05 14:48:31 +0000_

```text
Adding a row was half a fix, and the missing half read as three separate
bugs: half a card visible, a scroll that snapped back, and a card that did
nothing when clicked.

The grid lives in a ScrollRect whose Content has a hardcoded height (1104 in
Menu_Main) and no ContentSizeFitter - it never needed one, because the
authored 3x4 grid fit exactly. A fourth row therefore hangs below the
viewport, and the viewport's Mask does two things to it: it clips the drawing,
and - being an ICanvasRaycastFilter that rejects any point outside its own
rect - it eats the click as well. The ScrollRect meanwhile has nothing to
scroll, because content is still shorter than the viewport, so a drag springs
straight back (MovementType is Elastic). All three symptoms are one cause.

GrowScrollContent adds exactly what the new rows occupy - rowHeight plus the
grid's spacing per row, and that spacing is NEGATIVE here (the rows
deliberately overlap), so it is added rather than assumed positive. Content is
not driven by a parent layout group, so its sizeDelta is ours to set and the
result is deterministic: 1104 -> 1417.92 for the one row this roster needs.
The grid's own rect grows to match so it honestly contains its rows.

Deliberately not a ContentSizeFitter: that would re-derive the height of the
already-authored three rows from their preferred sizes rather than from the
fractional anchors the scene uses, which changes the arcade's existing layout.
This only ever adds the height of rows that did not exist before.
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md | 36 +++++++++++++++++++++++----------
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs   | 59 +++++++++++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 85 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 122 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index e24117f73..0b3c76e52 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -366,16 +366,32 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
 
 ## Known limitations / follow-ups
 
-- **The arcade grid had to grow to show this card.** `ArcadeExploreView`'s grid is AUTHORED at a
-  fixed size — 3 rows × 4 = 12 slots in Menu_Main — and the populate loop was bounded by it, so a
-  roster larger than the grid truncated **silently**: the alphabetically-last modes simply stopped
-  existing in the arcade, with no error and no gap in the grid to notice. Menu_Main was sitting at
-  exactly 12 renderable cards (13 games minus the Maelstrom, which the grid deliberately excludes),
-  so adding Switchback made 13 and pushed Wildlife Liberation off the end. `EnsureGridCapacity`
-  clones the last authored row until the roster fits, and the loop's third bound —
-  `GameList.Games.Count`, a ceiling on a *different* list — is removed. Arithmetic is asserted in
-  `ArcadeGridCapacityTests` rather than eyeballed, because an off-by-one there does not throw, it
-  hides a game mode.
+- **The arcade grid had to grow to show this card, and growing it takes TWO changes.**
+  `ArcadeExploreView`'s grid is AUTHORED at a fixed size — 3 rows × 4 = 12 slots in Menu_Main —
+  and the populate loop was bounded by it, so a roster larger than the grid truncated **silently**:
+  the alphabetically-last modes simply stopped existing in the arcade, with no error and no gap in
+  the grid to notice. Menu_Main was sitting at exactly 12 renderable cards (13 games minus the
+  Maelstrom, which the grid deliberately excludes), so adding Switchback made 13 and pushed
+  Wildlife Liberation off the end. `EnsureGridCapacity` clones the last authored row until the
+  roster fits, and the loop's third bound — `GameList.Games.Count`, a ceiling on a *different*
+  list — is removed. Arithmetic is asserted in `ArcadeGridCapacityTests` rather than eyeballed,
+  because an off-by-one there does not throw, it hides a game mode.
+
+  **Adding the row is only half of it**, and the missing half reads as three unrelated bugs. The
+  grid lives in a `ScrollRect` whose Content has a HARDCODED height (1104) and no
+  `ContentSizeFitter` — it never needed one, because the authored 3×4 grid fit exactly. A fourth
+  row therefore hangs below the viewport, and the viewport's `Mask` does two things to it: it
+  clips the drawing (you see the top of a card and nothing under it) **and**, being an
+  `ICanvasRaycastFilter` that rejects any point outside its own rect, it eats the CLICK. The
+  ScrollRect meanwhile has nothing to scroll, because content is still shorter than the viewport,
+  so a drag springs straight back (MovementType is Elastic). *Half a card, a scroll that snaps
+  back, and a dead button are one cause.* `GrowScrollContent` adds exactly what the new rows
+  occupy — `rowHeight + gridSpacing` each, and the grid's spacing is **negative** in Menu_Main
+  (the rows deliberately overlap), so it is added rather than assumed positive. Content is not
+  driven by a parent layout group, so its `sizeDelta` is ours to set and the result is
+  deterministic: 1104 → 1417.92 for one added row. Deliberately **not** a `ContentSizeFitter` —
+  that would re-derive the already-authored three rows' height from their preferred sizes instead
+  of the fractional anchors the scene uses, changing the existing arcade layout.
 
 - **20 gates is unmeasured.** Chosen from the arithmetic (≈9.4k units of course; 2–3 minutes at
   realistic Dolphin speeds), not from a playtest. It is one editor field.
diff --git a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
index df8c3657d..ead9589eb 100644
--- a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
+++ b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
@@ -230,11 +230,70 @@ namespace CosmicShore.UI
                 capacity += GameSelectionGrid.GetChild(i).childCount;
 
             int rows = RowsNeeded(capacity, perRow, required);
+            if (rows <= 0) return;
+
             for (int i = 0; i < rows; i++)
             {
                 var row = Instantiate(template, GameSelectionGrid);
                 row.name = $"{template.name} ({GameSelectionGrid.childCount})";
             }
+
+            GrowScrollContent(template as RectTransform, rows);
+        }
+
+        /// <summary>
+        /// Make the scroll view tall enough to reach the rows just added.
+        ///
+        /// <para><b>Adding a row is not enough on its own, and the failure looks like three
+        /// separate bugs.</b> The grid lives in a <see cref="ScrollRect"/> whose Content has a
+        /// HARDCODED height (1104 in Menu_Main) and no <c>ContentSizeFitter</c> - it never needed
+        /// one, because the authored 3x4 grid fit exactly. A fourth row therefore hangs below the
+        /// viewport, and the viewport's <see cref="Mask"/> does two things to it: it clips the
+        /// drawing (you see the top of a card and nothing under it), and - because <c>Mask</c> is
+        /// an <c>ICanvasRaycastFilter</c> that rejects any point outside its own rect - it eats
+        /// the CLICK as well. Meanwhile the ScrollRect has nothing to scroll, because content is
+        /// still shorter than the viewport, so a drag springs straight back (MovementType is
+        /// Elastic). Half a card, a scroll that snaps back, and a dead button are all the same
+        /// cause.</para>
```

</details>

### `0f47b1d4d` — fix(hijack): review pass - compile blocker, inert comeback, and a cancelled raid

_Claude, 2026-09-05 14:53:15 +0000_

```text
An adversarial review of the branch found one blocker and two real gameplay
defects. All three are fixed here, with the checks that would have caught them.

BLOCKER - the branch did not compile. `OnResetForReplayCustom` still called
`StopProgressSampler`, a leftover from the PeelTheCage milestone sampler this
mode deliberately does not have. The method exists on four other controllers as
a private member and on no base class, so `Assembly-CSharp` failed outright.

Worth recording HOW it survived my own check: I ran the file through Roslyn and
piped the diagnostics through `grep -oP "error CS\d+" | sort -u`. The call was a
CS0103, and CS0103 was ALREADY in the output twice for the expected missing Unity
references - so `sort -u` collapsed a real third instance into a code I had
already dismissed. Deduplicating by error CODE hides a new error of a code you
have already explained away; list the diagnostics.

THE COMEBACK LAYER WAS INERT. The cloned scene carried the donor's
`differenceSource: 3` (PrismsDestroyed), and `ElementalComebackSystem.EnsureExists`
respects a scene-authored instance as-is - it only calls `Bind`, never touching
the source - so `DefaultSourceFor`, the switch this branch carefully added a
`GameModes.Hijack` arm to, is never consulted in a scene that already has the
component. And PrismsDestroyed really is a flat zero here: the Urchin's hull
container runs Attach before Damage (whose `skipWhileAttached` then returns), and
its spike container is [Embed, Steal, ChainFire] with no damage effect at all. So
every deficit read 0 and the buff never fired. The scene now authors
`differenceSource: 8` through the generator, which asserts it and cross-checks the
ordinal against the C# enum. `ScoreDifferenceSource`'s members are also given
EXPLICIT values (exactly the ordinals the compiler had already assigned, so no
authored asset changes meaning) - every game scene serializes this field, and I
had just appended a member to an enum that pinned none of them.

THE AI CANCELLED ITS OWN RAID. The ride branch tested `IsAttached` alone, but a
burr is attachable too - its prisms carry a Volume trail and the surface follower
keeps the flag true - so a raider that reached the cluster its rail aimed it at
was pinned in the ride branch, steered back at the rail it came from, and blocked
from re-picking for as long as it stuck to the burr. The raid, which is the mode's
payoff, was cancelled by arriving at it. The test is now `AttachedPrism.Trail ==
the chosen rail's Trail`. General shape: when two different structures can put a
vessel in the same STATE FLAG, the flag is not the state.

Also from the review:

  * The stall escape could not fire in the case its own tooltip named. 10 u/s is
    the hostile crawl and 6 u/s was the "parked" threshold, so a raid could never
    read as a stall - which is CORRECT (a crawler converts one prism per hop and
    crosses a third in ten seconds); the tooltips were what lied. They now say
    what the number does. And when the escape does fire it excludes that rail for
    a cooldown, because `ChooseRail` is a pure function of position and domain and
    would otherwise re-pick the abandoned rail on the very next frame.
  * `ChooseRail` walked a burr once per RAIL (24) rather than once per BURR (18),
    ~27k prism reads per pilot per refresh; the census is memoised per call.
  * The generator's serialized-key checker pooled SO_Game + ScoringRuleSO +
    RampageScoringRuleSO into `known` for EVERY asset, and its field regex also
    matched method declarations - 25 foreign names would have passed on a cell
    config. Scoped per asset and the method alternative dropped; verified it now
    rejects a name the loose set accepted.
  * `GunVesselTransformer` logged on every surface attach. Harmless while nothing
    was built to be ridden; rolling a burr is this mode's main verb, so it is now
    on `CSLogChannel.PrismscapeRide`, off by default, with a row in the toolbox.
  * The four spawnable variants all authored the same root `m_Name`.

Documented rather than fixed, in HIJACK.md section 10: prism ownership does not
replicate (the score does, and was traced end to end - but ride speed, the
objective arrow and the AI's rail choice are all per-machine, so play-test
host-side first); the Urchin still has no HUD prefab, so an Urchin-only mode ships
with no ammo gauge; and `ram: 1` is a shared-prefab change that reaches every AI
Urchin in every context.
```

```text
 Assets/_Prefabs/Spawnables/SpawnableSwitchyard1.prefab       |   2 +-
 Assets/_Prefabs/Spawnables/SpawnableSwitchyard2.prefab       |   2 +-
 Assets/_Prefabs/Spawnables/SpawnableSwitchyard3.prefab       |   2 +-
 Assets/_Prefabs/Spawnables/SpawnableSwitchyard4.prefab       |   2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity       |   2 +-
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs |  24 ++++++----
 Assets/_Scripts/Controller/Arcade/HIJACK.md                  |  72 +++++++++++++++++++++++-----
 Assets/_Scripts/Controller/Arcade/HijackController.cs        | 105 +++++++++++++++++++++++++++++------------
 Assets/_Scripts/Controller/Vessel/GunVesselTransformer.cs    |   3 +-
 Assets/_Scripts/Utility/CSDebug.cs                           |  12 +++++
 Assets/_Scripts/Utility/Tools/LogControlWindow.cs            |   1 +
 Tools/Build/author_hijack_assets.py                          |  74 +++++++++++++++++++++++------
 12 files changed, 230 insertions(+), 71 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 545 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index b522986e6..4d18fed34 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -39,34 +39,40 @@ namespace CosmicShore.Gameplay
         /// losers a sentinel), so the Score source would be dead during live play.
         /// AstroLeague uses GoalsScored.
         /// </summary>
+        /// <remarks>
+        /// Values are EXPLICIT, per the house rule about serialized enums. They are exactly the
+        /// ordinals the compiler had already assigned, so no authored asset changes meaning -
+        /// pinning them only stops the NEXT member inserted mid-list from silently re-pointing
+        /// every scene that serialized one of these. Every game scene serializes this field.
+        /// </remarks>
         public enum ScoreDifferenceSource
         {
-            Score,
-            CrystalsCollected,
-            Goals,
-            PrismsDestroyed,
-            PrismsRemaining,
+            Score = 0,
+            CrystalsCollected = 1,
+            Goals = 2,
+            PrismsDestroyed = 3,
+            PrismsRemaining = 4,
             /// <summary>
             /// Wildlife Liberation's fauna kills. Domain-aggregated like every other source
             /// here - the mode is a domain race, so a player's deficit is their TEAM's deficit
             /// against the leading colour. (A per-player variant of this source existed while
             /// the mode was briefly a free-for-all and was removed with it.)
             /// </summary>
-            LifeformsKilled,
+            LifeformsKilled = 5,
 
             /// <summary>
             /// Dog Fight's weighted gunnery score. A team source like every entry above
             /// LifeformsKilled - Dog Fight pools points per domain - so the trailing SIDE gets
             /// the buff, not the trailing individual.
             /// </summary>
-            CombatPoints,
+            CombatPoints = 6,
 
             /// <summary>
             /// Joust's per-domain summed joust collisions. Joust's Score lands only at game end
             /// (winner a finish time, losers a sentinel - JoustScoringRuleSO.AssignScores), so
             /// the Score source would read a flat zero deficit for the whole match.
             /// </summary>
-            Jousts,
+            Jousts = 7,
 
             /// <summary>
             /// Hijack's per-domain summed prisms STOLEN. A team source like every entry above:
@@ -76,7 +82,7 @@ namespace CosmicShore.Gameplay
             /// count prisms - nothing is destroyed in Hijack, so the destruction stat is a flat
             /// zero there and would silently disable the comeback layer.
             /// </summary>
-            PrismsStolen,
+            PrismsStolen = 8,
         }
 
         [Header("Config")]
diff --git a/Assets/_Scripts/Controller/Arcade/HIJACK.md b/Assets/_Scripts/Controller/Arcade/HIJACK.md
index 8702a0fad..2d36625f0 100644
--- a/Assets/_Scripts/Controller/Arcade/HIJACK.md
+++ b/Assets/_Scripts/Controller/Arcade/HIJACK.md
@@ -236,15 +236,32 @@ becomes something it orbits (`Docs/AI_ORBIT_BREAK.md`):
    once close. Arriving along the ribbon is what makes `TrailFollower.Attach` seed its travel
    direction toward the far end instead of back the way it came.
 2. **RIDE** — keep aiming past the far end so the nose stays down-rail, and tap the spike trigger
-   when the prism underfoot is not its own and the ammo meter can pay. Parked under
-   `aiParkedSpeed` for `aiStuckSeconds` → Slip off and re-pick, because a dry Urchin on a hostile
-   third crawls at 10 and cannot convert its way out.
+   when the prism underfoot is not its own and the ammo meter can pay.
 3. **RAID** — past the far end, head for the burr that rail aims at and rake it, flying through
-   the centre so the pass does not become an orbit.
+   the centre so the pass does not become an orbit. Reached both ways: launched and still in the
+   air, and attached to the cluster itself.
+
+**The RIDE state is "riding the rail I chose", not "attached to something".** That distinction is
```

</details>

### `acd8ea164` — feat(switchback): tighten the mouth with intensity, light the next gate

_Claude, 2026-09-05 15:11:29 +0000_

```text
Three things, from playtest.

ARCADE - the last slot could not be opened. Content was measured, not
incremented: the previous fix grew the scroll content by what the new row
COST, which assumes content previously contained its children exactly. It did
not - the authored 1104 was already short of the three authored rows (they
extend to ~1522), so the increment landed short too and the card in the new
row stayed below the reachable range, where the viewport's Mask both clips the
drawing and rejects the raycast. That is why favouriting a mode "fixed" it: it
only moved that mode out of the last slot and moved another one in.
FitScrollContent now reads the real extent of every active descendant at
runtime (CalculateRelativeRectTransformBounds) and grows content to match, so
it corrects the pre-existing shortfall too. It runs last and unconditionally,
because the bounds helper skips inactive objects and the cards are switched on
by the populate loop. Uses SetSizeWithCurrentAnchors rather than writing
sizeDelta, since the grid is authored with fractional vertical anchors where
sizeDelta is an offset rather than a height.

RING SIZE - the mouth now tightens with intensity, from the play-tested 72 to
4.29 at intensity 4: barely bigger than the ship. DolphinHullRadius (2.86) is
MEASURED off the shipped prefab - the eight corners of all eleven hull box
colliders pushed through their transform chains to the root - not guessed. The
ladder is geometric between the two anchored ends, so every step is the same
increment of difficulty; the stated cost is a steep level 1 -> 2 drop
(72 -> 28), which is arithmetic, and NarrowestMouthClearance is the one dial.

NEXT GATE - the local pilot's next gate goes lime. This adds a third verb to
the switch shader vocabulary, ToySwitchSignal.Next, painted in the free-pickup
CTA colour. It is the first PER-VIEWER signal - it describes the relationship
between a switch and whoever is looking at it rather than the switch itself -
which is legal here because every peer builds its own copy of the course, so a
gate already belongs to one viewer and nothing shared is repainted. It makes
no domain claim, so SwitchDomain keeps it on Blue and the reservation holds.

Verified: the mode compiles against Unity stubs; the shipped course generator
re-run over 400 seeds x 4 intensities with the new mouths (all contracts hold,
0 Dubins-infeasible); validate_project.py 0 errors; conditional-compilation
OK; author_switchback_assets.py --check OK.
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md                      | 28 +++++++---
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs | 35 ++++++++++++
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackCourse.cs     | 57 ++++++++++++++++++--
 Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackGateRing.cs   | 38 +++++++++++--
 Assets/_Scripts/Controller/Toys/ToyFactory.cs                        | 22 +++++++-
 Assets/_Scripts/Data/Enums/ToySwitchSignal.cs                        | 24 +++++++--
 Assets/_Scripts/Tests/Editor/SwitchbackCourseTests.cs                | 50 +++++++++++++++++
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs                        | 98 ++++++++++++++++++----------------
 Docs/ToySystem/ARCHITECTURE.md                                       | 13 +++++
 9 files changed, 301 insertions(+), 64 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 542 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index 0b3c76e52..74743e7b3 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -189,12 +189,28 @@ grants no domain, and it makes two pilots flying side by side see different worl
 
 One cell at every level. What climbs is how hard the gates are to fly:
 
-| intensity | ring radius | leg length | max corner | axis jitter | presentation cap |
-|---|---|---|---|---|---|
-| 1 | 72 | 420–680 | 45° | 30° | 50° |
-| 2 | 60 | 400–650 | 50° | 40° | 55° |
-| 3 | 50 | 380–620 | 55° | 50° | 60° |
-| 4 | 42 | 360–580 | 60° | 60° | 65° |
+| intensity | ring radius | mouth ⌀ | leg length | max corner | axis jitter | presentation cap |
+|---|---|---|---|---|---|---|
+| 1 | 72.00 | 144.0 | 420–680 | 45° | 30° | 50° |
+| 2 | 28.12 | 56.2 | 400–650 | 50° | 40° | 55° |
+| 3 | 10.98 | 22.0 | 380–620 | 55° | 50° | 60° |
+| 4 | 4.29 | 8.6 | 360–580 | 60° | 60° | 65° |
+
+**The mouth ladder is derived, not tabled.** Both ends are anchored and the middle is
+interpolated geometrically (each level 2.56× tighter than the last). Level 1 stays at the
+play-tested 72. Level 4 is `DolphinHullRadius × NarrowestMouthClearance` = 2.86 × 1.5 = **4.29** —
+barely bigger than the ship, which is what intensity means here. `DolphinHullRadius` is
+**measured**, not guessed: the eight corners of all eleven hull box colliders on `Dolphin.prefab`,
+pushed through their transform chains to the vessel root (root scale 1), give a hull of
+5.29 × 1.23 × 5.30 and a worst-corner distance of **2.860** on `TopNose`. The circumscribing
+radius rather than the half-width, because a pilot may be rolled to any angle when they arrive.
+
+Geometric rather than linear because the ends are an order of magnitude apart: linear would spend
+three levels barely narrowing and then fall off a cliff, where a constant ratio makes every step
+the same increment. The cost of anchoring both ends is that **level 2 is a big drop from level 1**
+(72 → 28) — arithmetic rather than a judgement. `NarrowestMouthClearance` is the one dial to
+retune if that reads as a cliff. `SwitchbackCourseTests` asserts the ladder's shape and that every
+level still clears the ship, rather than restating the numbers.
 
 Gate **count** is deliberately constant, because it is the end-game target and is authored in one
 place — so a match is the same length at every level and the four are comparable. Same reasoning as
diff --git a/Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs b/Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs
index 03bd852c8..4ea6689a8 100644
--- a/Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs
+++ b/Assets/_Scripts/Controller/Arcade/Switchback/SwitchbackController.cs
@@ -136,6 +136,7 @@ namespace CosmicShore.Gameplay
         bool _finalResultsSent;
         bool _arenaBuildAnnounced;
         bool _warnedCourseMissing;
+        int _litGate = -1;
 
         /// <summary>Per-pilot detection state, on the machine that simulates that pilot.</summary>
         class PilotRun
@@ -380,6 +381,7 @@ namespace CosmicShore.Gameplay
             _course.Clear();
             _runs.Clear();
             _courseBuilt = false;
+            _litGate = -1;
         }
 
         /// <summary>
@@ -405,6 +407,37 @@ namespace CosmicShore.Gameplay
         /// <summary>Gates in this match's course, 0 until it arrives.</summary>
         public int GateCount => _rings.Count;
 
+        /// <summary>
+        /// Light the LOCAL pilot's next gate lime and put the previous one back to neutral.
+        ///
+        /// <para>Twenty identical rings scattered through a cell is a course you have to be told
+        /// the ORDER of. The objective arrow points a direction but not at a specific ring, and at
+        /// the far end of a leg several line up behind one another - so the gate itself says
+        /// "this one", in the platform's existing free-pickup lime.</para>
+        ///
+        /// <para><b>Local only, and no networking is added.</b> Every peer builds its own copy of
+        /// the course, so a <see cref="SwitchbackGateRing"/> already belongs to exactly one viewer;
+        /// painting one here changes nothing on anyone else's screen. Driven from the pilot's live
+        /// progress rather than from the crossing event, so it is correct after a rollback, after
+        /// a late course arrival, and for a client whose report is still in flight.</para>
+        /// </summary>
+        void LightLocalNextGate()
+        {
+            int next = gameData.LocalPlayer?.RoundStats != null
```

</details>

### `253c59781` — fix(hijack): the missing using, and the arcade grid that would have hidden the card

_Claude, 2026-09-05 15:13:49 +0000_

```text
Two things that each made the mode not work, neither of which any check on this
branch could see.

1. HijackObjectiveProvider did not compile - `[Inject] GameDataSO` with no
   `using CosmicShore.Utility;`. The branch's compile gate is a REFERENCE-LESS
   Roslyn pass, so every first-party type is already unresolvable in it and a
   genuinely missing using is indistinguishable from the noise: CS0246 was
   expected 369 times and this was the 370th. The complementary check is a
   namespace audit - for each new file, every PascalCase identifier DECLARED
   under Assets/_Scripts must live in a namespace that file has a using for -
   and it is now run over all six new files (all clean).

   General rule: a reference-less compile proves syntax, never reachability.
   The two checks are not substitutes and neither alone is a compile.

2. The arcade grid is authored at a fixed 3 x 4 = 12 slots in Menu_Main and the
   populate loop was bounded by it, so a roster larger than the grid truncated
   SILENTLY - the alphabetically-last modes stopped existing in the arcade, with
   no error and no gap to notice. Menu_Main sat at exactly 12 renderable cards
   (13 games minus the Maelstrom, which the grid excludes), so Hijack is the 14th
   game and the 13th renderable one: shipping it pushes a card off the end.

   EnsureGridCapacity / GrowScrollContent / RowsNeeded are taken VERBATIM from
   the in-flight Switchback branch (dc3dcb65, b2b63c8e) rather than re-derived.
   The fix is mode-agnostic, both branches trip the same ceiling, and an
   identical blob means the two merge without a conflict in that file - which a
   second implementation of the same idea would not. Do not edit it here.
```

```text
 Assets/Resources/EndConditionOverrides.asset                 |   2 -
 Assets/Resources/ModeControlsLibrary.asset                   |   2 -
 Assets/Resources/ObjectiveIconSet.asset                      |   3 -
 Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset      |   1 -
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset  |   1 -
 Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs |   1 +
 Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs      |  64 ++++++++++++++++
 Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs.meta |  11 +++
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs                | 163 +++++++++++++++++++++++++++++++++++------
 ProjectSettings/EditorBuildSettings.asset                    |   3 -
 10 files changed, 216 insertions(+), 35 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 269 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs b/Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs
index 530fc2025..3c7a9c6d6 100644
--- a/Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs
+++ b/Assets/_Scripts/Controller/Arcade/HijackObjectiveProvider.cs
@@ -1,3 +1,4 @@
+using CosmicShore.Utility;
 using CosmicShore.Data;
 using CosmicShore.UI;
 using Reflex.Attributes;
diff --git a/Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs b/Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs
new file mode 100644
index 000000000..309af954b
--- /dev/null
+++ b/Assets/_Scripts/Tests/Editor/ArcadeGridCapacityTests.cs
@@ -0,0 +1,64 @@
+using CosmicShore.UI;
+using NUnit.Framework;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// The arcade grid is AUTHORED at a fixed size (3 rows x 4 = 12 slots in Menu_Main) and the
+    /// populate loop is bounded by it, so a roster that outgrows it truncates SILENTLY - the
+    /// alphabetically-last modes simply stop existing in the arcade, with no error and no gap in
+    /// the grid to notice. Shipping Switchback is what crossed the line (13 renderable cards into
+    /// 12 slots), which is why the arithmetic that grows the grid is asserted here rather than
+    /// eyeballed in a scene.
+    /// </summary>
+    public class ArcadeGridCapacityTests
+    {
+        [Test]
+        public void FittingRoster_AddsNoRows()
+        {
+            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 12));
+            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 5));
+            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(12, 4, 0));
+        }
+
+        [Test]
+        public void OneCardOverflow_AddsExactlyOneRow()
+        {
+            // The shipped case: 12 authored slots, 13 renderable cards.
+            Assert.AreEqual(1, ArcadeExploreView.RowsNeeded(12, 4, 13));
+        }
+
+        [Test]
+        public void PartialRowStillCountsAsAWholeRow()
+        {
+            Assert.AreEqual(1, ArcadeExploreView.RowsNeeded(12, 4, 16));
+            Assert.AreEqual(2, ArcadeExploreView.RowsNeeded(12, 4, 17));
+            Assert.AreEqual(2, ArcadeExploreView.RowsNeeded(12, 4, 20));
+            Assert.AreEqual(3, ArcadeExploreView.RowsNeeded(12, 4, 21));
+        }
+
+        [Test]
+        public void ResultAlwaysClosesTheGap()
+        {
+            for (int perRow = 1; perRow <= 6; perRow++)
+            for (int capacity = 0; capacity <= 24; capacity++)
+            for (int required = 0; required <= 40; required++)
+            {
+                int rows = ArcadeExploreView.RowsNeeded(capacity, perRow, required);
+                Assert.GreaterOrEqual(capacity + rows * perRow, required,
+                    $"capacity {capacity}, perRow {perRow}, required {required} left a deficit");
+                // And never adds a row it did not need.
+                if (rows > 0)
+                    Assert.Less(capacity + (rows - 1) * perRow, required,
+                        $"capacity {capacity}, perRow {perRow}, required {required} over-allocated");
+            }
+        }
+
+        [Test]
+        public void EmptyTemplateRowCanNeverCloseTheGap_AndIsNotAskedTo()
+        {
+            // Guards the infinite loop the earlier while-form would have run on a 0-child row.
+            Assert.AreEqual(0, ArcadeExploreView.RowsNeeded(0, 0, 13));
+        }
+    }
+}
diff --git a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
```

</details>

### `51e5213fb` — refactor(hijack): cede 45/9/8 to Switchback, and put the three registry IDs in one place

_Claude, 2026-09-05 15:14:04 +0000_

```text
A mode claims a slot in three enums that every other mode also lives in, and the
in-flight Switchback branch claimed the same three off the same base commit:
GameModes 45, ScoringMetric 9, ElementalComebackSystem.ScoreDifferenceSource 8.

NONE OF THE THREE FAILS LOUDLY ON A DOUBLE-CLAIM. C# lets two enum members share
a value, so a second mode taking the same number compiles: GameModes.Hijack would
`==` GameModes.Switchback, every switch over the enum ambiguous, and the two
metrics silently reading each other's stat. Only GameModes has a tripwire at all
(EnumIntegrityTests' member count) and it catches the COUNT, not the collision -
it would have fired on the merge with nothing pointing at the aliasing underneath.
ScoringMetric and ScoreDifferenceSource have no tripwire, and the latter is
SERIALIZED into every party-game scene, so a scene authored against one branch's
numbering reads the other branch's stat.

Hijack cedes to 46 / 10 / 9. Switchback is further along (its grid fix is already
reacting to a real run), and a collision that compiles is a worse failure than a
gap in an enum - 45 is simply free for the next mode if Switchback never lands,
carrying none of the permanent reservation 7 and 31 do. Both branches also
independently pinned every ScoreDifferenceSource ordinal explicitly, which is the
right fix arrived at twice; that stays.

The numbers now live at the top of author_hijack_assets.py (MODE_ID / METRIC_ID /
COMEBACK_SOURCE) and the generator's asserts hold the C# to them, so moving off
the next collision is a three-line edit rather than a hunt through five authored
assets. Re-running it re-authored the card (Mode 46), the scoring rule (metric
10), both metric->sprite tables, the progression unlock and the scene's comeback
source; the shared registries were reverted to base first so a re-run appends one
entry rather than leaving the old one beside it.

  hijack_budget.py: all proofs pass, unchanged
  author_hijack_assets.py --check: validation passed (38 files)
  validate_project.py: 0 errors  |  check_conditional_compilation.py: OK
  namespace audit: all six new files clean

Not editor-verified - no Unity available in this session.
```

```text
 Assets/Resources/EndConditionOverrides.asset                 |  2 ++
 Assets/Resources/ModeControlsLibrary.asset                   |  2 ++
 Assets/Resources/ObjectiveIconSet.asset                      |  3 +++
 Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset      |  1 +
 Assets/_SO_Assets/Games/ArcadeGameHijack.asset               |  2 +-
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset  |  1 +
 Assets/_SO_Assets/Scoring Rules/HijackScoringRule.asset      |  2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity       |  2 +-
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs |  2 +-
 Assets/_Scripts/Controller/Arcade/HIJACK.md                  | 36 +++++++++++++++++++++++++++--
 Assets/_Scripts/Data/Enums/GameModes.cs                      |  4 ++--
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                  |  2 +-
 CLAUDE.md                                                    |  8 +++----
 Docs/SCENES.md                                               |  2 +-
 ProjectSettings/EditorBuildSettings.asset                    |  3 +++
 Tools/Build/author_hijack_assets.py                          | 51 +++++++++++++++++++++++++++---------------
 16 files changed, 91 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 279 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index 4d18fed34..8479e2095 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -82,7 +82,7 @@ namespace CosmicShore.Gameplay
             /// count prisms - nothing is destroyed in Hijack, so the destruction stat is a flat
             /// zero there and would silently disable the comeback layer.
             /// </summary>
-            PrismsStolen = 8,
+            PrismsStolen = 9,
         }
 
         [Header("Config")]
diff --git a/Assets/_Scripts/Controller/Arcade/HIJACK.md b/Assets/_Scripts/Controller/Arcade/HIJACK.md
index 2d36625f0..c583d98f6 100644
--- a/Assets/_Scripts/Controller/Arcade/HIJACK.md
+++ b/Assets/_Scripts/Controller/Arcade/HIJACK.md
@@ -1,4 +1,4 @@
-# Hijack — the Urchin heist race (`GameModes.Hijack = 45`)
+# Hijack — the Urchin heist race (`GameModes.Hijack = 46`)
 
 **Urchin-only. First DOMAIN to steal 1,500 prisms wins.** Nothing in this mode is ever
 destroyed: mass only changes hands.
@@ -163,7 +163,7 @@ against each other by `hijack_budget.prove_extent()`.
 
 ## 4. Scoring
 
-**Metric: `ScoringMetric.PrismsStolen = 9`** → `IRoundStats.PrismStolen`. That stat already
+**Metric: `ScoringMetric.PrismsStolen = 10`** → `IRoundStats.PrismStolen`. That stat already
 existed and is already credited on both sides of the wire — `StatsManager.PrismStolen` for every
 host-simulated pilot (which covers every AI), and `Player.ReportPrismStolen_ServerRpc` for a
 client's own steals. **No new gameplay plumbing: the stat has been accumulating in every mode
@@ -353,6 +353,38 @@ item is a real check a human has to perform, in this order (load-bearing first).
 
 ---
 
+### The three registry IDs, and why 46 rather than 45
+
+A mode claims a slot in three enums that every other mode also lives in — `GameModes` (46),
+`ScoringMetric` (10) and `ElementalComebackSystem.ScoreDifferenceSource` (9). **None of the
+three fails loudly on a double-claim.** C# lets two members share a value, so a second mode
+taking the same number *compiles*: `GameModes.Hijack` would `==` the other mode, every switch
+over it ambiguous, and the two metrics silently reading each other's stat. Only `GameModes` has
+a tripwire at all (`EnumIntegrityTests.GameModes_HasExpectedMemberCount`), and it catches the
+member COUNT rather than the collision.
+
+45 / 9 / 8 were taken first by the in-flight **Switchback** branch, which was further along, so
+Hijack ceded rather than double-claim — a collision that compiles is a worse failure than a gap
+in an enum. The three numbers now live in one place at the top of
+`Tools/Build/author_hijack_assets.py` (`MODE_ID` / `METRIC_ID` / `COMEBACK_SOURCE`), and the
+generator's `--check` holds the C# to them, so moving off the next collision is a three-line
+edit rather than a hunt through five authored assets. If Switchback never lands, 45 is simply
+free for the mode after this one; it carries none of the permanent reservation 7 and 31 do.
+
+### The arcade grid had to grow before this card could render
+
+`ArcadeExploreView`'s grid is AUTHORED at a fixed 3 × 4 = 12 slots in Menu_Main and the populate
+loop was bounded by it, so a roster larger than the grid truncated **silently** — the
+alphabetically-last modes stopped existing in the arcade with no error and no gap to notice.
+Menu_Main sat at exactly 12 renderable cards (13 games minus the Maelstrom, which the grid
+deliberately excludes), so Hijack is the 14th game and the 13th renderable one: shipping it
+pushes a card off the end.
+
+`EnsureGridCapacity` / `GrowScrollContent` / `RowsNeeded` come from the Switchback branch
+verbatim (commits `dc3dcb65`, `b2b63c8e`) rather than being re-derived here — the fix is
+mode-agnostic, both branches trip the same ceiling, and taking the identical blob means the two
+merge without a conflict in that file. Do not edit it on this branch for that reason.
+
 ## 10. Known limitations
 
 - **PRISM OWNERSHIP DOES NOT REPLICATE, and this mode is the first whose whole subject is
diff --git a/Assets/_Scripts/Data/Enums/GameModes.cs b/Assets/_Scripts/Data/Enums/GameModes.cs
index 2b6108862..07d02085a 100644
--- a/Assets/_Scripts/Data/Enums/GameModes.cs
+++ b/Assets/_Scripts/Data/Enums/GameModes.cs
@@ -97,7 +97,7 @@ namespace CosmicShore.Data
         // keeps the strikers firing. First DOMAIN to the prism target wins. See
         // _Scripts/Controller/Arcade/SALVO.md.
         Salvo = 44,
-        // Hijack (45): the Urchin-only heist race. Three great-circle RAILS ring a hollow core,
```

</details>

### `d2f7f9399` — fix(sparrow): corpses, buffers, self-debuff and tank convergence in the missile pass

_Claude, 2026-09-05 15:21:56 +0000_

```text
Adversarial review of fe817adf, verified against source. Every fix below is
negative-controlled by SparrowMissileFuzeTests (13 tests, each new one proven to
fail when its invariant is broken).

A CORPSE IS NOT A TARGET (platform). Crystal.IsEmbedded does not mean alive: a
creature with a progressive wither re-homes its heart onto the cell at the top of
its death and leaves it embedded for the whole animation (ECOSYSTEM.md §26), so a
corpse's heart kept matching for seconds. The warhead jousted one and re-ran the
sealed death — a SECOND LifeformsKilled credit for one creature, on a 30-kill
target, with the heart freed while the wither was still eating inward. Fauna.Predated
now declines an already-dead creature. It never had that guard: it tested
_consumedAsPrey, which only Predated sets, so a starvation or body-prism death
walked past it, while LifeForm.Jousted has always carried the equivalent `dying`
check. Fauna is a SIBLING of LifeForm, not a subclass, so it never inherited it —
and Fauna.Jousted's own doc already promised the behaviour. ILifeFormEntity.IsDying
publishes what both types were gating on privately; the fuze and the warhead's kill
both test it.

A FIXED SCRATCH BUFFER IS A CORRECTNESS PROPERTY when the layer is shared with
things you reject. OverlapSphereNonAlloc fills in unspecified order and discards
the rest, and every discriminating test ran after the fill — so "it takes the first
qualifying hit and stops" was no defence. The Crystals layer is dominated by hearts
the fuze rejects (flora, own-domain fauna, free drops), any of which could push the
one opposing hull out of a 24-slot result. The fuze now runs TWO queries: a
Ships-only buffer that cannot truncate at a lobby's size, making the vessel half
exact, and a larger crystal buffer with grow-and-retry. SweepLifeformHearts gets the
same treatment.

THE WARHEAD NO LONGER DEBUFFS ITS OWN SHOOTER. The detonator handed it the CHARGE-5
"Domain-Safe Skybursts" snapshot the prism blasts take, which is TRUE below the
upgrade — so a 95u sphere centred at most 76u away reliably caught the firing pilot
and their wingman, at exactly the close range the fuze encourages. It passes false
unconditionally: that flag is about MASS, and this blast destroys none.

WILDLIFE IS QUARRY WHATEVER COLOUR IT WEARS. The creature kill no longer reads the
blast's friendly-fire flag. Fauna spawn in ONE colour, so borrowing it let a prism
upgrade silently switch off wildlife kills for a pilot sharing the swarm's colour —
in the only mode scored on killing wildlife, where the comeback system hands that
upgrade to whoever is losing. It also disagreed with the mode's primary kill, which
has never cared about colour.

REACH IS NOT CAPTURE. The blast grows over its duration and a vessel is caught only
when the sphere contains it, so at 0.5s the 25%/20% margin bought only ~40 u/s —
below every vessel's cruise. Now 0.15s (~130 u/s): ordinary flight is caught, a
boosting escape deliberately is not. The tooltip claimed a guarantee and the test
asserted radii only; both now state the real bound.

THE MISSILE TANK MUST AGREE ACROSS PEERS. "Only the owner fires" was false: a press
is replicated as a PRESS, replayed on every peer, where Fire reads that peer's own
tank and returns early if short — so a drifted replica silently spawned no missile,
no fuze and no warhead, and a victim there took no debuff. Spending is convergent;
earning is not (fauna/flora are per-peer local rolls). The crystal refill this
replaced was self-healing by accident. VesselRearmOnPrismDestruction is now a
NetworkBehaviour whose owner publishes an idempotent SET, rate-limited to 1s —
Salvo's shape, for Salvo's reason. It also stops calling PlayerName per prism death,
which logs a warning while Player is null (hundreds per blast during pair resolution).

Also: affectsPrisms is enforced on the Physics fallback too (the batch early-return
is exactly the state in which that path runs, and it ARMOURS prisms rather than
ignoring them); a ward can no longer be granted to a disabled component, where
neither revoker runs; the unused AffectsOwnDomain property is removed; and the
Wildlife Liberation docs, which got nothing in the first pass despite being the mode
most affected, now carry the change plus the known host-only limitation against
NetworkSynced sharks. CLAUDE.md's duplicated, self-contradicting Wildlife Liberation
paragraph (250 kills, three pens — both superseded) is deleted.
```

```text
 Assets/_Prefabs/Projectile/AOEMissileWarhead.prefab                   |   2 +-
 .../MissileWarheadWitherLifeformEffect.asset                          |   1 +
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md                         |  22 ++++-
 Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md              |  64 +++++++++++++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs         |  23 ++++++
 .../_Scripts/Controller/Environment/FloraAndFauna/ILifeFormEntity.cs  |  15 ++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs      |   5 ++
 .../ExplosionWitherLifeformByCrystalEffectSO.cs                       |  33 +++++++-
 .../Controller/ImpactEffects/EffectsSO/ProjectileDetonatorSO.cs       |  19 ++++-
 .../EffectsSO/Vessel Crystal Effects/VesselWardByCrystalEffectSO.cs   |   5 +-
 .../_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs  |  45 ++++++++---
 Assets/_Scripts/Controller/Projectiles/Projectile.cs                  | 138 ++++++++++++++++++++++----------
 .../Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md         | 114 ++++++++++++++++++++++----
 Assets/_Scripts/Controller/Vessel/VesselRearmOnPrismDestruction.cs    | 128 ++++++++++++++++++++++++++---
 Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs         |   8 ++
 Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs               | 118 +++++++++++++++++++++++++++
 CLAUDE.md                                                             |   4 +-
 17 files changed, 653 insertions(+), 91 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 997 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
index 7b92e5aec..ef3316deb 100644
--- a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
+++ b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
@@ -241,7 +241,27 @@ this is retuned.
 **Scoring is unchanged, deliberately.** The new warhead blast (which debuffs pilots and kills
 wildlife) carries **no** `VesselCombatHitByExplosionEffectSO`, so it does not add a second
 50-point event; a rocket still scores once, through its direct hit or the conic blast, sharing one
-`VesselCombatHitLatch` window. The lever if this proves too fast is
+`VesselCombatHitLatch` window.
+
+**It does not debuff YOU, and that took an explicit decision.** The warhead is a 95-unit sphere
+centred at most a fuze-radius (76 u) away, i.e. the shooter is routinely inside their own blast at
+exactly the close range the fuze encourages. `ProjectileDetonatorSO` originally handed it the same
+friendly-fire snapshot it hands the prism blasts (`AffectSelfOverride = !proj.SpareOwnDomain`),
+which is TRUE *below* CHARGE 5 — so every pilot without that upgrade took `−0.5` on all four
+elements for 4 s on their own close-range kills, and so did any wingman in the sphere. It now
+passes `false` unconditionally: domains ARE the sides here, the same rule
+`Projectile.DisallowImpactOnVessel` already enforces on the direct hit. A blast that destroys mass
+has a real reason to read that flag; one whose entire payload is a debuff on vessels does not.
+
+**A direct missile hit on a pilot is now unreachable**, and that is a consequence of the fuze
+rather than a bug: the fuze trips at 20× the round's hit radius and switches the round's own
+collider off, and the gap cannot be crossed inside one frame (53–145 u of fuze radius against
+~8 u of closure at 60 fps even head-on at combined top speed). So
+`SparrowSkyBurstProjectileImpactContainer`'s vessel branch — the spin and its combat-hit report —
+is dead for opposing pilots. Scoring survives because the conic blast carries its own report, but
+**the SPIN has no blast counterpart and is therefore gone**. Left as a design call rather than
+silently re-homed: moving the spin onto the warhead would spin every pilot in a 95-unit sphere,
+which is a much larger change than the one that was asked for. The lever if this proves too fast is
 `proximityFuzeRadiusMultiplier` on `SkyBurstProjectile.prefab`. Full mechanics:
 `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md`.
 
diff --git a/Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md b/Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md
index 11cd5a744..50e26ccad 100644
--- a/Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md
+++ b/Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md
@@ -733,3 +733,67 @@ the band and the PhaseThresholds cannot drift apart.
 - **Very heavy is very heavy.** See the collider-budget section. If intensity 4 will not hold
   frame rate on device, lower `POPULATION_SCALE` in `wildlife_cage_budget.py` first — the
   creature count, not the cage, is the cost.
+
+## ⚠ Changed under this mode's feet — the Sparrow's missile warhead
+
+This mode did not ask for a change and got a large one, because it flies the vessel that
+changed. Read this before re-tuning the 30-kill target or `POPULATION_SCALE`.
+
+**The skyburst now kills creatures directly, in a 95-unit sphere.** Every skyburst detonation
+spawns a second blast alongside the prism one (`AOEMissileWarhead.prefab`), and that blast
+JOUSTS every living fauna heart inside it — the identical death the Squirrel's Crystal Joust
+runs, credited to the firing pilot, landing straight on this mode's scoring metric
+(`ScoringMetric.LifeformsKilled`, target 30). Before it, a Sparrow killed a creature only by
+destroying its last body prism.
+
+**Two of its rules were written specifically so this mode still works, and both are the
+opposite of what the surrounding code does:**
+
+- **Wildlife is quarry whatever colour it wears** (`sparesOwnDomain: 0` on
+  `MissileWarheadWitherLifeformEffect.asset`). The creature kill deliberately does NOT read the
+  blast's friendly-fire flag the way the prism half does. Fauna spawn in exactly ONE colour —
+  the cell's controlling domain — so borrowing that flag let the Sparrow's CHARGE-5 upgrade
+  ("Domain-Safe Skybursts", authored about not destroying your own TRAIL) silently switch off
+  wildlife kills for any pilot who happened to share the swarm's colour. In the one mode scored
+  on killing wildlife. And because `ElementalComebackSystem` hands element levels to whoever is
+  LOSING, falling behind bought a hard nerf to the scoring weapon. It also disagreed with the
+  mode's own primary kill, which has never cared about colour: shooting a creature's body prisms
+  kills it whatever domain it wears.
+- **The proximity fuze does NOT trip on own-domain wildlife**, at any level. That is the
+  opposite decision from the one above and it is deliberate: the fuze picks TARGETS (a rocket
+  that armed on friendly wildlife could not cross a swarm at all), while the blast affects
+  everything it reaches. So a pilot sharing the swarm's colour flies through it normally and
+  still kills it with a blast aimed at something else.
+
+**A corpse is not a target, and finding that out fixed a platform bug.** `Crystal.IsEmbedded`
+does not mean "alive": a creature with a progressive wither re-homes its heart onto the cell at
+the TOP of its death and leaves it embedded for the whole animation (`Docs/ECOSYSTEM.md` §26),
+so a corpse's heart keeps matching for seconds. Jousting one re-ran the sealed death — **a
+second `LifeformsKilled` credit for one creature**, on a 30-kill target, and the heart popped
+free while the wither was still eating inward, which §26 forbids. `Fauna.Predated` now declines
+a creature that has already died. It never had that guard: it tested `_consumedAsPrey`, which
```

</details>

### `3bca12579` — docs(sparrow): retire the last claims that a crystal refills the missile tank

_Claude, 2026-09-05 15:34:18 +0000_

```text
The deleted SparrowVesselChangeResourceByCrystalEffect was still named as live
wiring in three places, all of them present-tense:

- SalvoController's class doc opened on "the tank does not regenerate - the ONLY
  refuel is an omni crystal", which is the premise the whole mode is built on. It
  now says what changed and, more usefully, what it COSTS that mode: a Sparrow
  tearing up the Boneyard is self-funding, so the crystal line is an accelerant
  rather than the sole tap and the shoot-versus-run tension is weaker than when
  Salvo shipped. The lever to restore the original scarcity is ammoPerPrism on the
  Sparrow, not a change in the controller.
- SALVO.md's economy table listed the effect as a current piece.
- ElementalAbilitySystem/AUDIT.md's dated RETRACTED entry says "the restock IS
  wired" and names the asset's fields. It is a log and stays, with one line saying
  it is superseded so nobody reads its present tense as current wiring.

Found by the same adversarial review as the previous commit. No behaviour change.
```

```text
 Assets/_Scripts/Controller/Arcade/SALVO.md           |  2 +-
 Assets/_Scripts/Controller/Arcade/SalvoController.cs | 32 ++++++++++++++++++++++----------
 Docs/ElementalAbilitySystem/AUDIT.md                 |  4 ++++
 3 files changed, 27 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SALVO.md b/Assets/_Scripts/Controller/Arcade/SALVO.md
index 870486e0d..487d0de56 100644
--- a/Assets/_Scripts/Controller/Arcade/SALVO.md
+++ b/Assets/_Scripts/Controller/Arcade/SALVO.md
@@ -81,7 +81,7 @@ The whole missile economy is the Sparrow's shipped wiring, not this mode's inven
 | Missile tank: max **1**, starts full, **no regeneration** | `Sparrow.prefab` ResourceSystem, resource 0 ("Missiles") |
 | A skyburst costs **0.5** of the tank → 2 rockets per refuel | `SkyBurstGunAction.asset` (`ammoCost`) |
 | Full-auto guns cost **0** — always available, chip damage | `FullAutoAction.asset` |
-| An omni crystal **sets the tank full** on collect | `SparrowVesselChangeResourceByCrystalEffect.asset` (the Sparrow's one `vesselCrystalEffects` entry), replayed on the collector's own machine by `CrystalManager.ReplayVesselCrystalEffects` |
+| ~~An omni crystal **sets the tank full** on collect~~ — **RETIRED 2026-09** | `SparrowVesselChangeResourceByCrystalEffect.asset` is DELETED. Missiles now reload by DESTROYING HOSTILE PRISMS (`VesselRearmOnPrismDestruction` on `Sparrow.prefab`, 0.02 per prism = 25 prisms per rocket); the omni crystal grants an 8 s elemental-debuff ward instead. See the ⚠ section below |
 
 Salvo's job was to build a mode where that loop is the game: stock the arena with crystals,
 make destruction the score, and extend the refuel to the domain.
diff --git a/Assets/_Scripts/Controller/Arcade/SalvoController.cs b/Assets/_Scripts/Controller/Arcade/SalvoController.cs
index 79936913b..ead5539bc 100644
--- a/Assets/_Scripts/Controller/Arcade/SalvoController.cs
+++ b/Assets/_Scripts/Controller/Arcade/SalvoController.cs
@@ -20,14 +20,25 @@ namespace CosmicShore.Gameplay
     /// server-authoritative winner detection in OnTurnEndedCustom, final scores replicated by
     /// snapshot ClientRpc, golf-timed (winners carry their finish time).
     ///
-    /// <para><b>The loop: crystals are the missile economy.</b> The Sparrow's guns are free
-    /// but chip one prism at a time; the skyburst levels whole structures but costs half the
-    /// missile tank (<c>SkyBurstGunAction.ammoCost 0.5</c> against a max of 1), and the tank
+    /// <para><b>The loop: crystals are the missile economy - but they are no longer the ONLY
+    /// refuel.</b> The Sparrow's guns are free but chip one prism at a time; the skyburst levels
+    /// whole structures but costs half the missile tank (<c>SkyBurstGunAction.ammoCost 0.5</c>
+    /// against a max of 1). The arena is stocked with crystals
+    /// (<c>CrystalCountMode.PlayerCountPlusExtra</c> + 5, the Scurry abundance rather than
+    /// Rampage's scarcity) and the match is a rhythm of crystal run → double salvo → crystal run.
+    ///
+    /// <para><b>⚠ CHANGED UNDER THIS MODE'S FEET (2026-09).</b> This doc used to say "the tank
     /// does not regenerate - the ONLY refuel is an omni crystal
-    /// (<c>SparrowVesselChangeResourceByCrystalEffect</c> sets the tank full on collect). So
-    /// the arena is stocked with them (<c>CrystalCountMode.PlayerCountPlusExtra</c> + 5, the
-    /// Scurry abundance rather than Rampage's scarcity) and the match is a rhythm of
-    /// crystal run → double salvo → crystal run.</para>
+    /// (<c>SparrowVesselChangeResourceByCrystalEffect</c>)". That asset is DELETED. The Sparrow's
+    /// missiles now reload by DESTROYING HOSTILE PRISMS
+    /// (<c>VesselRearmOnPrismDestruction</c>, 0.02 per prism = 25 prisms per rocket), and the omni
+    /// crystal instead grants an 8-second elemental-debuff ward. This mode's premise is therefore
+    /// softened rather than broken: a Sparrow tearing up the Boneyard is now self-funding, so the
+    /// crystal line is an ACCELERANT rather than the sole tap, and the tension between "shoot the
+    /// wreckage" and "run the crystals" is weaker than when this mode shipped. The wingman reload
+    /// below is untouched and is still the reason to play it together. If the mode wants its
+    /// original scarcity back, the lever is <c>ammoPerPrism</c> on the Sparrow (0 restores
+    /// crystal-only refuelling exactly), not a change here.</para></para>
     ///
     /// <para><b>The reason to play it together: the WINGMAN RELOAD.</b> A collected omni
     /// crystal reloads the missile bays of EVERY pilot on the collector's domain, not just the
@@ -35,9 +46,10 @@ namespace CosmicShore.Gameplay
     /// <see cref="RefuelDomainMissiles_ClientRpc"/>). One pilot can fly the crystal line while
     /// a wingman camps the densest wreckage and fires every reload the runner buys - a real
     /// division of labour on top of the domain-pooled score, not just parallel solo play.
-    /// The collector's own machine is also covered twice over: the platform crystal effect
-    /// refills them via <c>CrystalManager.ReplayVesselCrystalEffects</c>, and the RPC's
-    /// set-to-full is idempotent on top of it.</para>
+    /// The collector's own machine is covered by the same RPC - the platform crystal effect that
+    /// used to refill them as well is gone (see the ⚠ note above), so this RPC is now the ONLY
+    /// thing a crystal does for a missile tank. Its set-to-full is idempotent, so it remains safe
+    /// to arrive more than once.</para>
     ///
     /// <para>Ammo is deliberately LOCAL state: each machine simulates its own vessel's firing
     /// (projectiles are local objects - see DOGFIGHT.md "Multiplayer"), so a broadcast
diff --git a/Docs/ElementalAbilitySystem/AUDIT.md b/Docs/ElementalAbilitySystem/AUDIT.md
index 8115acd03..7ff1fbd0a 100644
--- a/Docs/ElementalAbilitySystem/AUDIT.md
+++ b/Docs/ElementalAbilitySystem/AUDIT.md
@@ -181,6 +181,10 @@ at."
   _resourceAmount: 1, _overrideAmount: 1`) sits in `SparrowImpactorDataContainer.asset`'s
   `vesselCrystalEffects` — any crystal impact refills Missiles to full (2 rockets). The economy
   is 2 rockets per crystal, as the ability card promises.
+  **SUPERSEDED 2026-09** — that asset is now DELETED. The Sparrow's missiles reload by destroying
+  hostile prisms (`VesselRearmOnPrismDestruction`, 0.02 per prism) and the omni crystal grants an
+  elemental-debuff ward instead. The finding above is kept as the dated record it is; do not read
+  its present tense as current wiring.
 - **Dead `Gun` with `projectileFactory: {fileID: 0}`** on the prefab (`Sparrow.prefab:212-227`,
   since Oct 2025) — unreferenced rewire trap; any `FireGun` call on it NREs.
 - Pool return is **delegated entirely to impact end-effects** (`Projectile.cs:197-198`); a
```

</details>

### `7d1cd09e8` — fix(toys): read DarkCTA off EnvironmentColors, not off SO_ColorSet

_Claude, 2026-09-05 15:37:42 +0000_

```text
CtaLime read theme.ColorSet.DarkCTA. DarkCTA is a field of the nested
EnvironmentColorSet, reached as ColorSet.EnvironmentColors.DarkCTA - the same
path SetBackgroundColor and the danger rim already use. Compile error, caught
on the user's build.

The reason it got past verification is worth recording: the headless harness
compiles the files I wrote against STUBS of everything they touch, so an
accessor path into a real type is precisely what it cannot check - the stub
SO_ColorSet was an empty class, and any member name at all would have
compiled. Fixed by compiling the REAL SO_ColorSet.cs into the harness and
adding a probe of the accessor chain, with a negative control confirming the
shipped expression is rejected by it.
```

```text
 Assets/_Scripts/Controller/Toys/ToyFactory.cs | 4 +++-
 1 file changed, 3 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Toys/ToyFactory.cs b/Assets/_Scripts/Controller/Toys/ToyFactory.cs
index 8f26409ca..f57dd4bbf 100644
--- a/Assets/_Scripts/Controller/Toys/ToyFactory.cs
+++ b/Assets/_Scripts/Controller/Toys/ToyFactory.cs
@@ -359,7 +359,9 @@ namespace CosmicShore.Gameplay
         /// the wrong colour entirely.
         /// </summary>
         public static Color CtaLime(ThemeManagerDataContainerSO theme)
-            => theme && theme.ColorSet ? theme.ColorSet.DarkCTA : new Color(0.55f, 0.95f, 0.15f);
+            => theme && theme.ColorSet && theme.ColorSet.EnvironmentColors != null
+                ? theme.ColorSet.EnvironmentColors.DarkCTA
+                : new Color(0.55f, 0.95f, 0.15f);
 
         /// <summary>
         /// A <b>switch ring</b>: one continuous ring square across the flight path, at the radius
```

</details>

### `612c2a8eb` — fix(sparrow): silence the warhead, and stop the ward stranding its own flag

_Claude, 2026-09-05 15:46:47 +0000_

```text
Last two findings from the adversarial review, both verified against source.

THE WARHEAD IS SILENT. AOEExplosion.Detonate plays the shared
GameplaySFXCategory.Explosion one-shot, and a single skyburst already spawns TWO
authored blasts (the cone and the sphere). The warhead goes off at the same point
on the same frame, so it was a THIRD identical one-shot: roughly +5 dB over one and
phasing against itself, which reads as a mix bug rather than as a bigger explosion.
AOEExplosion gains `playsDetonationSfx` (default on, so nothing else changes) and
the warhead authors it off. Deliberately NOT given a voice by routing it through
some other existing category: per the house audio rule, a blast that should sound
different gets its own inspector-exposed EventReference shipped EMPTY, so an unwired
slot is a visible TODO instead of an invisible one.

THE WARD NO LONGER STRANDS `_granted`. Apply() early-returns when the ResourceSystem
is unreachable, which left `_granted` at its previous value — and `_granted` is the
early-out at the top of the method. A failed REVOKE (teardown, a swap, a system not
yet resolved) therefore left it true forever, making every later Grant() a silent
no-op against a ResourceSystem carrying no grant, with nothing to clear it. It now
records the truth: a grant cannot outlive the object that was holding it.

SparrowMissileFuzeTests is 14 tests, the new one negative-controlled like the rest.
```

```text
 Assets/_Prefabs/Projectile/AOEMissileWarhead.prefab                       |  1 +
 Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs                    | 15 ++++++++++++++-
 Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md |  1 +
 Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs             | 12 +++++++++++-
 Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs                   | 13 +++++++++++++
 5 files changed, 40 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 92 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs b/Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs
index 51ad6a96b..683f8e43d 100644
--- a/Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs
+++ b/Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs
@@ -36,6 +36,18 @@ namespace CosmicShore.Gameplay
                  "temporarily shield half an arena.")]
         [SerializeField] protected bool affectsPrisms = true;
 
+        [Tooltip("On (default): this blast plays the shared GameplaySFXCategory.Explosion " +
+                 "one-shot when it detonates. Off: it is SILENT.\n\n" +
+                 "Turn it off for a blast that goes off at the same point and instant as another " +
+                 "one. A single skyburst already spawns two authored blasts (the cone and the " +
+                 "sphere), so its warhead would be a THIRD identical one-shot on the same frame " +
+                 "at the same position - which sums to roughly +5 dB over one and phases against " +
+                 "itself, rather than reading as a bigger explosion. If a blast should have a " +
+                 "voice of its OWN, the house rule is its own inspector-exposed EventReference " +
+                 "on the thing that makes the noise, shipped EMPTY - never a second consumer of " +
+                 "a shared category because it is close enough (CLAUDE.md, Audio).")]
+        [SerializeField] protected bool playsDetonationSfx = true;
+
         /// <summary>
         /// Whether this blast's prism pass runs at all. Read by
         /// <see cref="ExplosionImpactor.BeginBatchProcessing"/> — ONE gate, so every explosion
@@ -262,7 +274,8 @@ namespace CosmicShore.Gameplay
         {
             CancelExplosion();
             explosionCts = new CancellationTokenSource();
-            AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.Explosion, transform.position);
+            if (playsDetonationSfx)
+                AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.Explosion, transform.position);
             ExplodeAsync(explosionCts.Token).Forget();
         }
 
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md
index 930f46cb4..74a16d0db 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md
@@ -474,6 +474,7 @@ creature in the match. If the other reading is wanted, it is a one-field change:
 | `faunaOnly` | `MissileWarheadWitherLifeformEffect.asset` | on | Off lets the warhead kill FLORA too — a whole grown plant per rocket, through its heart |
 | `sparesOwnDomain` | `MissileWarheadWitherLifeformEffect.asset` | **off** | Off = wildlife is quarry whatever colour it wears. Deliberately the effect's OWN decision, NOT the blast's friendly-fire flag: fauna spawn in ONE colour, so borrowing that flag let the CHARGE-5 *prism* upgrade switch off wildlife kills in the one mode scored on them |
 | `ExplosionDuration` | `AOEMissileWarhead.prefab` | **0.15 s** | How fast the sphere reaches full size — i.e. how fast a target can be moving away and still be caught (~130 u/s here; 0.5 s bought only ~40). Reach is not capture; see the geometry section |
+| `playsDetonationSfx` | `AOEMissileWarhead.prefab` | **off** | The warhead is SILENT. A skyburst already spawns two authored blasts that each play the shared `Explosion` one-shot; a third at the same point on the same frame sums and phases rather than reading as a bigger explosion. A voice of its own would be its own `EventReference`, shipped empty — never a third consumer of a shared category |
 | `syncIntervalSeconds` | `Sparrow.prefab` → `VesselRearmOnPrismDestruction` | 1 s | How often the OWNER publishes its missile tank to the other peers as an idempotent SET. 0 disables the correction and accepts per-peer drift, which makes a replica silently skip drawing the missile |
 | Growth target / uniform | `SkyBurstProjectile.prefab` → `Projectile` | `MissileVisual` / on | Selects the model-IS-the-hit-volume path: the model grows and the collider is fitted to it. The only prefab in the game that sets it. Clearing it puts the missile on the shell path, where it would not grow at all — it has no `chargeField` |
 
diff --git a/Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs b/Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs
index b740cadb1..2104dd27e 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselTimedElementalWard.cs
@@ -124,7 +124,17 @@ namespace CosmicShore.Gameplay
             if (immune == _granted) return;
 
             var resources = _status?.ResourceSystem;
-            if (!resources) return;
+            if (!resources)
+            {
+                // No ResourceSystem to hold the ward - so we are NOT holding one, and saying so
+                // matters more than it looks. _granted is the early-out above: leaving it TRUE
+                // after a failed revoke (teardown, a swap, a system not yet resolved) makes every
+                // later Grant() a silent no-op against a ResourceSystem that has no grant on it,
+                // and nothing ever clears it. Recording the truth is also simply correct: a grant
+                // cannot outlive the object that was carrying it.
+                _granted = false;
+                return;
+            }
 
             resources.SetElementalDebuffImmunity(this, immune, wardedSources);
             _granted = immune;
diff --git a/Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs b/Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs
index ff53d4c7f..7e59082a8 100644
--- a/Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs
+++ b/Assets/_Scripts/Tests/Editor/SparrowMissileFuzeTests.cs
@@ -252,6 +252,19 @@ namespace CosmicShore.Tests
                 "the warhead trigger must exclude the TrailBlocks layer");
         }
 
+        [Test]
+        public void TheWarheadIsSilent()
+        {
+            // A single skyburst already spawns TWO authored blasts (the cone and the sphere), each
```

</details>

### `9a9320ee3` — chore(sparrow): author syncIntervalSeconds on the prefab rather than leaning on the field initializer

_Claude, 2026-09-05 15:47:55 +0000_

```text
A serialized field with no key in the YAML keeps its C# initializer until something
saves the prefab, at which point Unity writes the key and produces a diff nobody
made. Authoring it now keeps the asset and the code stating the same number.
```

```text
 Assets/_Prefabs/Spacevessels/Sparrow.prefab | 1 +
 1 file changed, 1 insertion(+)
```

### `dfdf54878` — feat(scoring): add VolumeDestroyed metric

_Claude, 2026-09-05 15:50:41 +0000_

```text
The platform's first FLOAT-backed scoring metric: a mode can now be scored on
hostile VOLUME torn out rather than on a prism COUNT, so mass built of heavier
structure is worth more than the same number of light panes.

It is rounded exactly ONCE, in ScoringMetrics.Read, so every downstream consumer
(the per-domain sum, the HUD column, the goal row, the scoreboard secondary)
keeps the single int contract the other nine metrics share.

No networking work: IRoundStats.HostileVolumeDestroyed was already credited by
StatsManager and already travels on the existing
Player.ReportEnvironmentPrismDestroyed_ServerRpc round trip.
```

```text
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs |  5 +++++
 Assets/_Scripts/Data/Enums/ScoringMetric.cs                 | 13 +++++++++++++
 2 files changed, 18 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs b/Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs
index f71675b39..c2736cda0 100644
--- a/Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs
+++ b/Assets/_Scripts/Controller/Arcade/Scoring/ScoringMetrics.cs
@@ -1,5 +1,6 @@
 using CosmicShore.Data;
 using CosmicShore.Utility;
+using UnityEngine;
 
 namespace CosmicShore.Gameplay
 {
@@ -24,6 +25,10 @@ namespace CosmicShore.Gameplay
             ScoringMetric.PrismsRemaining   => stats.PrismsRemaining,
             ScoringMetric.LifeformsKilled   => stats.LifeformsKilled,
             ScoringMetric.CombatPoints      => stats.CombatPoints,
+            // The one FLOAT-backed metric: rounded once, here, so every downstream consumer
+            // (the per-domain NetworkVariable sum, the HUD column, the goal row, the
+            // scoreboard secondary) keeps the single int contract the rest of them share.
+            ScoringMetric.VolumeDestroyed   => Mathf.RoundToInt(stats.HostileVolumeDestroyed),
             _                               => 0,
         };
 
diff --git a/Assets/_Scripts/Data/Enums/ScoringMetric.cs b/Assets/_Scripts/Data/Enums/ScoringMetric.cs
index cebe6129d..9c8c62a79 100644
--- a/Assets/_Scripts/Data/Enums/ScoringMetric.cs
+++ b/Assets/_Scripts/Data/Enums/ScoringMetric.cs
@@ -47,5 +47,18 @@ namespace CosmicShore.Data
         // The one metric whose source is vessel-vs-vessel combat rather than prisms, crystals,
         // or the ecology.
         CombatPoints = 8,
+        // Drumfire: hostile VOLUME destroyed (reads IRoundStats.HostileVolumeDestroyed, rounded
+        // to the nearest whole unit). The volume twin of PrismsDestroyed, and the first metric
+        // whose underlying stat is a FLOAT - a prism's worth here is its size, so carving a big
+        // structural pane out of the drum pays more than shattering a sliver. Rounding is the
+        // only concession: every scoring surface on the platform (the domain-sum NetworkVariable,
+        // the HUD column, the goal row) is an int, and a volume that reaches six figures loses
+        // nothing readable to the fractional part.
+        //
+        // It is credited by exactly the same path PrismsDestroyed is (StatsManager.
+        // CreditPrismDestruction on the server, Player.ReportEnvironmentPrismDestroyed_ServerRpc
+        // for a client's own environment kills - the volume travels on that RPC), so a client
+        // scores its own demolition correctly with no extra plumbing.
+        VolumeDestroyed = 9,
     }
 }
```

</details>

### `8d59985fa` — feat(crystals): add ApproachLanes placement mode

_Claude, 2026-09-05 15:50:55 +0000_

```text
A second CrystalPlacementMode: instead of scattering around anchors or the
nucleus, lay one straight LANE of evenly spaced crystals per player, struck
through that player's own spawn slot and passing the cell centre at an authored
standoff rather than running into it.

The geometry is a pure static helper (ApproachLaneGeometry) so the shipped path
is the tested one. The standoff is the mechanism: a lane leaving a point on the
spawn sphere and tilted theta off straight-in has closest approach
ringRadius * sin(theta), so sin(theta) = offset / ringRadius places it exactly,
CLAMPED because an offset past the ring radius has no line that satisfies it and
the honest answer there is a tangent, never a NaN.

Two properties are load-bearing:

- Lane ownership is EMERGENT. CellSpawnFormation places one player per
  direction and lane k is struck through spawn slot k, so nothing assigns it -
  which holds only while the scene's spawn ring radius equals the lane radius
  and the two formations match.
- The index mapping is lane-MAJOR. NetworkCrystalManager grows its slot list as
  players arrive and fills only empty entries, so a slot-major mapping would
  re-home every crystal already laid each time somebody joined.

A collected crystal reloads at its own slot, so a pilot's second pass runs the
same line as their first.

Both placement sites now go through one seam (InitialSpawnPointFor), so the
initial batch and every respawn cannot disagree about where a crystal belongs.

Verified outside Unity: 37 test cases pass against the shipped source, and the
CrystalManager refactor onto the helper is bit-identical to the previous inline
math over 1,257 placements.
```

```text
 Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs    | 149 ++++++++++++++++-
 .../Controller/Environment/FlowField/NetworkCrystalManager.cs         |   8 +-
 Assets/_Scripts/Tests/Editor/ApproachLaneGeometryTests.cs             | 283 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/ApproachLaneGeometryTests.cs.meta        |  11 ++
 Assets/_Scripts/Utility/ApproachLaneGeometry.cs                       | 110 +++++++++++++
 Assets/_Scripts/Utility/ApproachLaneGeometry.cs.meta                  |  11 ++
 6 files changed, 563 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 626 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs b/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
index 951d78e10..56d313909 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs
@@ -56,6 +56,30 @@ namespace CosmicShore.Gameplay
             IntensityScaled = 2,
         }
 
+        /// <summary>
+        /// WHERE a crystal spawns and respawns, as opposed to <see cref="CrystalCountMode"/>'s
+        /// HOW MANY.
+        /// </summary>
+        public enum CrystalPlacementMode
+        {
+            /// <summary>
+            /// The platform default: the authored per-intensity anchor list when a scene has one,
+            /// otherwise a random point in the cell's nucleus. Every mode but Drumfire.
+            /// </summary>
+            AnchorsOrNucleus = 0,
+
+            /// <summary>
+            /// One straight FIRING LANE per player, each a line of evenly spaced crystals that
+            /// runs from that player's own spawn slot past the cell centre and out the far side.
+            /// The lane geometry is derived from <see cref="CellSpawnFormation"/> - the same
+            /// function that places the players - so a lane and the pilot who flies it can never
+            /// drift apart, and the arrangement re-derives itself for any roster size instead of
+            /// being authored per scene. A collected crystal reloads at its OWN slot, so the lane
+            /// stays a lane for the whole match. Drumfire's rhythm track; see DRUMFIRE.md.
+            /// </summary>
+            ApproachLanes = 1,
+        }
+
         // IMPORTANT:
         // We compare Vector3.SqrMagnitude(...) <= MIN_SQR_DISTANCE.
         // So this constant is "distance squared".
@@ -96,6 +120,39 @@ namespace CosmicShore.Gameplay
                  "reuses the last entry.")]
         [SerializeField] private List<IntensityCrystalCount> crystalCountByIntensity = new();
 
+        [Header("Approach Lanes (CrystalPlacementMode.ApproachLanes)")]
+        [Tooltip("WHERE crystals go. AnchorsOrNucleus is the platform default; ApproachLanes " +
+                 "lays one straight firing lane per player through their own spawn slot.")]
+        [SerializeField] private CrystalPlacementMode placementMode = CrystalPlacementMode.AnchorsOrNucleus;
+
+        [Tooltip("Lane mode: the spawn-ring radius the lanes are struck from. MUST match the " +
+                 "radius ServerPlayerVesselInitializer resolves (nucleus radius + spawn distance, " +
+                 "floored by Spawn Ring Radius Floor) or the lanes will not pass through the " +
+                 "players' spawn points.")]
+        [SerializeField, Min(1f)] private float laneRingRadius = 1120f;
+
+        [Tooltip("Lane mode: how close the lane passes to the cell CENTRE. This is what makes the " +
+                 "lane a fly-PAST rather than a dive - it must clear whatever is in the middle of " +
+                 "the arena, and the clearance it leaves is the margin a pilot has to lean in and " +
+                 "graze that structure.")]
+        [SerializeField, Min(0f)] private float laneOffsetFromCenter = 420f;
+
+        [Tooltip("Lane mode: distance from a player's spawn point to the FIRST crystal of their lane. "
+                 + "Together with Lane Length this CENTRES the crystals on the lane's closest "
+                 + "approach, which is what keeps every shot in the run at a similar range - see "
+                 + "DRUMFIRE.md, where a long-range shot measured seven times a close one.")]
+        [SerializeField, Min(0f)] private float laneLeadDistance = 640f;
+
+        [Tooltip("Lane mode: distance from the first crystal to the LAST. The spacing between " +
+                 "beats is this divided by (slots - 1), so raising the crystal count tightens the " +
+                 "rhythm without moving either end of the run.")]
+        [SerializeField, Min(1f)] private float laneLength = 800f;
+
+        [Tooltip("Lane mode: MUST match ServerPlayerVesselInitializer's spawnFormation, or lane k " +
+                 "will not pass through spawn slot k.")]
+        [SerializeField] private CellSpawnFormation.Formation laneFormation =
+            CellSpawnFormation.Formation.Symmetric;
+
         [Header("Crystal Domain")]
         [SerializeField] protected bool spawnCrystalWithPlayerDomain;
         
@@ -235,9 +292,7 @@ namespace CosmicShore.Gameplay
                 {
                     // With no authored anchors the batch anchor is a placeholder, so the
                     // initial batch draws from the SAME volume every respawn draws from.
-                    Vector3 spawnPos = hasAnchors
```

</details>

### `9f41fc02e` — feat(environment): add the Drum, a shootable prism sphere

_Claude, 2026-09-05 15:51:08 +0000_

```text
SpawnableDrum builds a great porous ball of prism panes at the cell centre out
of the Orrery's sun-shell vocabulary - a phyllotaxis point set per sphere, panes
laid tangent to the surface, value-noise gaps punched through - scaled from a
46u ornament to a 320u arena feature and stacked into five concentric shells.

Point counts fall as r^2 so every shell is covered to the same fraction and the
panes stay one size, which is what makes a shot fired ACROSS the ball pass
through several skins while a shot at its middle punches one small hole.

Every scoring pane is Domains.Blue: StatsManager.IsFriendlyEnvironmentPrism
counts a prism as friendly only when it wears the attacker's own colour, so a
Blue drum is hostile to every domain and each pilot shoots the same target.
Painting it in the three playable colours would have made a third of the ball
worthless to whichever team drew that colour, decided by a spawn slot nobody
picked.

Shielded meridian ribs are the structure worth aiming at, laid proud of the skin
by 1.5 * pane thickness because a shield's octahedron reaches 1.5x leafSize
(Docs/ECOSYSTEM.md 35) and would otherwise fuse into the panes it braces. A
super-shielded core cage can never be destroyed, so the arena always leaves a
landmark. Danger studs on the outer skin only make grazing the drum for jaw
energy a real risk, and are never placed in a noise gap.

Tools/Build/drumfire_arena.py measures it offline and fails on a drift: 28,350
prisms, 1,373,051 volume, 240 always-on mesh colliders against 28,110
LOD-cullable boxes.
```

```text
 .../_Scripts/Controller/Environment/MiniGameObjects/SpawnableDrum.cs  | 212 ++++++++++++++++
 .../Controller/Environment/MiniGameObjects/SpawnableDrum.cs.meta      |  11 +
 Tools/Build/drumfire_arena.py                                         | 429 ++++++++++++++++++++++++++++++++
 3 files changed, 652 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 653 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableDrum.cs b/Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableDrum.cs
new file mode 100644
index 000000000..8a626c2cc
--- /dev/null
+++ b/Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableDrum.cs
@@ -0,0 +1,212 @@
+using UnityEngine;
+using CosmicShore.Data;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// "The Drum" - Drumfire's target, and the whole of that cell's environment: a great porous
+    /// ball of prism panes hung at the cell centre for pilots to carve. Built out of the Orrery's
+    /// sun-shell vocabulary (a phyllotaxis point set on each sphere, panes laid tangent to the
+    /// surface, noise gaps punched through) but scaled up from a 46u ornament to a ~320u arena
+    /// feature and stacked into concentric shells, so a shot fired ACROSS the ball passes through
+    /// several skins and a shot fired at its middle punches one small hole. That difference is
+    /// the mode's aiming lesson, and it is geometry rather than a rule.
+    ///
+    /// <para><b>Every scoring pane is <see cref="Domains.Blue"/>, deliberately.</b>
+    /// <c>StatsManager.IsFriendlyEnvironmentPrism</c> counts a prism as friendly only when it
+    /// wears the attacker's own colour, so a Blue drum is hostile to every domain and each pilot
+    /// is shooting at exactly the same target. Painting it in the three playable colours would
+    /// have made a third of the ball worthless to whichever team drew that colour, decided by a
+    /// spawn slot nobody picked.</para>
+    ///
+    /// <para><b>What is NOT plain skin:</b> shielded meridian RIBS (two passes to break, and
+    /// worth more volume - the reason to aim at structure), a super-shielded CORE that no blast
+    /// can touch so the drum always leaves a landmark and can never be scored to nothing, and a
+    /// scatter of danger STUDS on the outer skin only, which is what makes flying in close to
+    /// graze the surface for jaw energy a real risk rather than a free upgrade.</para>
+    ///
+    /// <para>Collider budget: the plain and danger panes ride the LOD-cullable BoxCollider, so
+    /// their active count is bounded by <c>PrismColliderLodManager</c> rather than by population,
+    /// exactly like the freestyle cell environments. Only the shielded ribs and the core carry
+    /// always-on convex MeshColliders - see <c>Tools/Build/drumfire_arena.py</c>, which counts
+    /// them and fails if the always-on total leaves the shipped band.</para>
+    /// </summary>
+    public class SpawnableDrum : CellEnvironmentSpawnableBase
+    {
+        protected override int DefaultSeed => 45;
+
+        [Header("Drum")]
+        [Tooltip("Radius of the OUTERMOST shell, in world units. The lane offset authored on the " +
+                 "scene's crystal manager must stay comfortably outside this or a pilot flying " +
+                 "their line will clip the ball.")]
+        [SerializeField, Min(1f)] float outerRadius = 320f;
+
+        [Tooltip("Concentric skins, evenly spaced from the outer radius down toward the core. " +
+                 "Each is a phyllotaxis point set; point counts fall as r^2 so every shell is " +
+                 "covered to the same fraction and the panes stay one size throughout.")]
+        [SerializeField, Min(1)] int shellCount = 5;
+
+        [Tooltip("Points on the OUTER shell before the noise gaps are punched. Inner shells scale " +
+                 "by (r/outerRadius)^2.")]
+        [SerializeField, Min(1)] int outerShellPoints = 14074;
+
+        [Tooltip("Value-noise gaps: a point whose noise sample falls below this is skipped, so " +
+                 "the skin reads as a lattice you can see and shoot through rather than a solid " +
+                 "ball. 0 = no gaps.")]
+        [SerializeField, Range(0f, 0.9f)] float gapThreshold = 0.25f;
+
+        [Tooltip("Spatial frequency of the gap noise, in 1/units. Lower = bigger, blobbier holes.")]
+        [SerializeField, Min(0.0001f)] float gapNoiseFrequency = 0.012f;
+
+        [Tooltip("One pane: X/Y span the shell surface, Z is its thickness along the normal.")]
+        [SerializeField] Vector3 paneSize = new(8f, 8f, 0.7f);
+
+        [Header("Ribs (shielded)")]
+        [Tooltip("Meridian bands of SHIELDED panes that brace the outer shell. Tougher (a hit " +
+                 "sheds the shield instead of destroying the prism) and heavier, so they are the " +
+                 "structure worth aiming at. Always-on mesh colliders - keep the total small.")]
+        [SerializeField, Min(0)] int ribCount = 3;
+
+        [SerializeField, Min(0)] int panesPerRib = 72;
+
+        [SerializeField] Vector3 ribPaneSize = new(14f, 5f, 2.4f);
+
+        [Header("Core (super-shielded)")]
```

</details>

### `8bd12536f` — feat(arcade): add Drumfire, the Dolphin's rhythm range

_Claude, 2026-09-05 15:51:28 +0000_

```text
GameModes.Drumfire = 45. The platform's first mode built to TEACH a hull rather
than to test one, and the only one whose objective is never reached.

The Dolphin has one offensive act and it is a three-step loop the vessel never
explains: bank energy by SKIMMING, fire by touching a CRYSTAL, aim by pointing
the nose. Rampage and The Bends both assume you already know it. Drumfire is
where you learn it, and the geometry teaches it - no tutorial text, no scripted
beat. The lane runs PAST the drum, so the target is always off to one side and
the pilot cannot aim by steering: they have to hold the line and turn the nose.

TIME ends it (DrumfireTimeTurnMonitor, 75s in EndConditionOverridesSO) and
VOLUME scores it. DrumfireScoringRuleSO is the only rule whose
IsObjectiveReached always answers false - every sibling ends on a target, and a
rule that ever answered true here would race the clock and hand the win to
whoever crossed an invented threshold first.

The match length is replicated because each peer runs its own copy of the base
elapsed-time loop and two peers reading different durations would disagree about
when their local monitors stop; the turn END stays server-gated either way.
TimeBasedTurnMonitor gained SetDuration for that, and the monitor authors the
duration BEFORE calling base.StartMonitor, which resets elapsed time and pushes
the first display tick.

No AIPilot hook, deliberately: the platform pilot already seeks the nearest
crystal and, once committed, drifts and swings its nose onto the densest
hostile-mass cluster from Cell.GetExplosionTarget - which in this arena is the
drum. Installing SetExternalTargetProvider would override crystal seeking
outright and disarm every AI Dolphin, per the rule RAMPAGE.md records.

ElementalComebackSystem gains ScoreDifferenceSource.VolumeDestroyed, appended
last rather than inserted because the enum is serialized on every SO_ArcadeGame.

Verified outside Unity: 12 scoring tests pass against the real sources and the
shipped asset, negative-controlled.
```

```text
 Assets/_Scripts/Controller/Arcade/DrumfireController.cs               | 203 +++++++++++++++++++++
 Assets/_Scripts/Controller/Arcade/DrumfireController.cs.meta          |  11 ++
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs          |  14 ++
 Assets/_Scripts/Controller/Arcade/Scoring/DrumfireScoringRuleSO.cs    |  90 ++++++++++
 .../_Scripts/Controller/Arcade/Scoring/DrumfireScoringRuleSO.cs.meta  |  11 ++
 .../Controller/Arcade/TurnMonitors/DrumfireTimeTurnMonitor.cs         |  61 +++++++
 .../Controller/Arcade/TurnMonitors/DrumfireTimeTurnMonitor.cs.meta    |  11 ++
 .../_Scripts/Controller/Arcade/TurnMonitors/TimeBasedTurnMonitor.cs   |  16 ++
 Assets/_Scripts/Data/Enums/GameModes.cs                               |  10 +-
 Assets/_Scripts/Editor/EndConditionOverridesWindow.cs                 |   6 +-
 Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs          |  35 +++-
 Assets/_Scripts/Tests/Editor/DrumfireScoringTests.cs                  | 303 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/DrumfireScoringTests.cs.meta             |  11 ++
 Assets/_Scripts/Tests/Editor/EnumIntegrityTests.cs                    |   8 +-
 14 files changed, 783 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 926 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DrumfireController.cs b/Assets/_Scripts/Controller/Arcade/DrumfireController.cs
new file mode 100644
index 000000000..4a9853b7a
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/DrumfireController.cs
@@ -0,0 +1,203 @@
+using System.Linq;
+using Unity.Collections;
+using Unity.Netcode;
+using UnityEngine;
+using CosmicShore.Utility;
+using CosmicShore.Data;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Drumfire - the <b>Dolphin-only rhythm range</b>, and the one mode built to TEACH a hull
+    /// rather than to test one.
+    ///
+    /// <para>A great porous drum of prisms (<see cref="SpawnableDrum"/>) hangs at the cell
+    /// centre. Every pilot is given their own firing lane: a straight line of evenly spaced
+    /// crystals struck through their own spawn slot, which passes the drum at a standoff instead
+    /// of running into it. The Dolphin's only weapon is the conic jaw blast, armed by SKIMMING
+    /// and fired by touching a CRYSTAL, so the lane IS the trigger track - and because the lane
+    /// runs past the target rather than at it, the drum is always off to one side and every shot
+    /// needs a deliberate aim. Drift to lock the course down the lane, swing the nose onto the
+    /// drum, take the next crystal: <b>fly, aim, shoot, repeat</b>. That loop is the whole
+    /// mode.</para>
+    ///
+    /// <para><b>TIME ends it and VOLUME is the score</b> (<see cref="DrumfireTimeTurnMonitor"/>,
+    /// <see cref="ScoringMetric.VolumeDestroyed"/>). There is no target to race to, so this
+    /// controller's turn end is the only one in the family that fires on the clock rather than on
+    /// a rule reporting its objective reached - everything after that point (resolve the winning
+    /// domain, assign scores, snapshot them to every peer) is the shape
+    /// <see cref="RampageController"/> established.</para>
+    ///
+    /// <para><b>No AI hook, deliberately.</b> The platform pilot already does exactly what this
+    /// mode asks: it seeks the nearest collectible cell item (here, the next crystal on its lane)
+    /// and, once its course is committed, DRIFTS and swings its nose onto the densest cluster of
+    /// hostile mass it can find (<see cref="Cell.GetExplosionTarget"/>) - which in this arena is
+    /// the drum. Installing <c>AIPilot.SetExternalTargetProvider</c> would override crystal
+    /// seeking outright and disarm every AI Dolphin, which is the rule RAMPAGE.md records; a
+    /// drift-look provider is unnecessary because the default already points at the target.</para>
+    ///
+    /// <para>Vessel restriction is the platform's two-place clamp fed by the single entry in
+    /// <c>ArcadeGameDrumfire.Vessels</c>, not anything here.</para>
+    /// </summary>
+    public class DrumfireController : MultiplayerDomainGamesController
+    {
+        [Header("Scoring")]
+        [Tooltip("Drag DrumfireScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
+        [SerializeField] ScoringRuleSO rule;
+
+        bool _finalResultsSent;
+
+        // Points, not golf: the most volume torn out of the drum wins.
+        protected override bool UseGolfRules => false;
+        protected override bool UseSceneReloadForReplay => true;
+
+        // End-game runs through OnTurnEndedCustom (server-side winner resolution) →
+        // SyncFinalScores_ClientRpc, which raises WinnerCalculated + MiniGameEnd itself. Suppress
+        // the base turn→round→game flow so those are not raised a second time.
+        protected override bool HasEndGame => false;
+
+        public override void OnNetworkSpawn()
+        {
+            base.OnNetworkSpawn();
+            gameData.ScoringRule = rule;
+            numberOfRounds = 1;
+            numberOfTurnsPerRound = 1;
+            _finalResultsSent = false;
+        }
+
+        // ── Server-authoritative game end (the clock, not a target) ───────
+
+        /// <summary>
+        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound, so
+        /// _finalResultsSent is set in time to suppress the Ready button.
+        ///
+        /// Unlike every sibling this runs because the CLOCK expired, so there is no "did anyone
```

</details>

### `446cb6ce8` — feat(arcade): author Drumfire's scene, cell and arcade card

_Claude, 2026-09-05 15:51:59 +0000_

```text
Generated by Tools/Build/author_drumfire_assets.py, which is the source; the
assets are its build. Re-run it rather than hand-editing any of them - --check
fails if one has drifted, and it cross-validates its lane constants against
drumfire_arena.py.

The scene is cloned from MinigameRampage (the nearest Dolphin sibling) with the
turn monitor, controller, cell configs, crystal block, spawn ring and comeback
source swapped. Two pairs must agree or the mode silently breaks, and the
generator asserts both:

  spawnRingRadiusFloor == laneRingRadius   (1120)
  spawnFormation       == laneFormation    (Symmetric)

Lane ownership is emergent from the spawn formation, so those are one number in
two places.

The crystal band is authored 640..1440 along a lane whose closest approach is at
1038, i.e. STRADDLING the pass. That is not a taste call. A conic blast's yield
falls as the SQUARE of its range, so the first cut - a band running outward FROM
the pass - had its opening volley destroy 31.7-52.5% of the drum while the
second pass added 0.0%. The geometric floor is omega(d^2 + R^2) / (2 pi R^2),
minimum 3.68% per full-energy shot and scale-invariant (confirmed by an R-sweep
over 320..850), so enlarging the target cannot fix it. drumfire_arena.py now
fails the build if end crystals are worth more than 2.0x middle ones, which is
the assertion that caught the wrong lead distance before it shipped.

Intensity is the BEAT, not the reach: 5/6/7/8 crystals in the same 800u band, so
a harder intensity means more and tighter beats. At 40 u/s a full pass is 36s and
the 75s clock is ~2.1 passes - only enough time for the pilot's own crystals
while moving on the slower side.

The cell authors no nucleus (the crystals are on lanes 1120u out, and the nucleus
is the LOCKED platform crystal-respawn volume, so one here would be a marker
pointing at nothing) and no flora or fauna (every prism a pilot destroys should
be the drum).
```

```text
 Assets/Resources/EndConditionOverrides.asset                          |     2 +
 Assets/Resources/ObjectiveIconSet.asset                               |     3 +
 Assets/_Graphics/UI/Objectives/objective_volume_destroyed.png         |   Bin 0 -> 1929 bytes
 Assets/_Graphics/UI/Objectives/objective_volume_destroyed.png.meta    |   104 +
 Assets/_Prefabs/Spawnables/SpawnableDrum.prefab                       |    71 +
 Assets/_Prefabs/Spawnables/SpawnableDrum.prefab.meta                  |     7 +
 Assets/_SO_Assets/Cell Configs/Drumfire Cell.meta                     |     8 +
 .../_SO_Assets/Cell Configs/Drumfire Cell/Drumfire Cell Config.asset  |    42 +
 .../Cell Configs/Drumfire Cell/Drumfire Cell Config.asset.meta        |     8 +
 .../Cell Configs/Drumfire Cell/Drumfire Spawn Profile.asset           |    26 +
 .../Cell Configs/Drumfire Cell/Drumfire Spawn Profile.asset.meta      |     8 +
 Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset               |     1 +
 Assets/_SO_Assets/Games/ArcadeGameDrumfire.asset                      |    40 +
 Assets/_SO_Assets/Games/ArcadeGameDrumfire.asset.meta                 |     8 +
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset           |     1 +
 Assets/_SO_Assets/Scoring Rules/DrumfireScoringRule.asset             |    16 +
 Assets/_SO_Assets/Scoring Rules/DrumfireScoringRule.asset.meta        |     8 +
 Assets/_Scenes/Multiplayer Scenes/MinigameDrumfire.unity              | 10496 ++++++++++++++++++++++++++++++
 Assets/_Scenes/Multiplayer Scenes/MinigameDrumfire.unity.meta         |     7 +
 ProjectSettings/EditorBuildSettings.asset                             |     3 +
 Tools/Build/author_drumfire_assets.py                                 |   747 +++
 Tools/Build/author_objective_icons.py                                 |    23 +
 22 files changed, 11629 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 794 lines)</summary>

```diff
diff --git a/Tools/Build/author_drumfire_assets.py b/Tools/Build/author_drumfire_assets.py
new file mode 100644
index 000000000..69d102360
--- /dev/null
+++ b/Tools/Build/author_drumfire_assets.py
@@ -0,0 +1,747 @@
+#!/usr/bin/env python3
+"""
+Authors every serialized asset the Drumfire game mode needs (GameModes.Drumfire = 45).
+
+Drumfire is the Dolphin-only rhythm range: a great porous DRUM of prisms at the cell centre,
+and one firing lane per pilot - a line of evenly spaced crystals struck through their own spawn
+slot that passes the drum instead of running into it. The Dolphin's jaw blast is armed by
+skimming and fired by touching a crystal, so the lane is the trigger track and the drum is
+always off to one side: fly, aim, shoot, repeat. TIME ends it, VOLUME destroyed is the score.
+
+What this script authors:
+
+  - the arcade card + scoring rule (ScoringMetric.VolumeDestroyed, points not golf)
+  - the DRUM: a SpawnableDrum prefab, plus the cell config and spawn profile that carry it
+    (no nucleus - the drum IS this cell's core; no flora, no fauna - a clean range)
+  - the scene, cloned from MinigameRampage (the other Dolphin-only mode, and already wired for
+    a cell-relative spawn ring, Dolphin AI templates and a crystal manager) with the mode
+    identity, the arena and the crystal LANES swapped in
+  - the registrations (game list, progression, build settings, match clock)
+
+Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
+produces byte-identical output. Validates the whole result in memory and only then writes.
+
+    python3 Tools/Build/author_drumfire_assets.py [--check]
+
+The arena's numbers are MEASURED, not guessed - Tools/Build/drumfire_arena.py counts the drum
+and checks the lane geometry, and this script asserts the two agree. See
+Assets/_Scripts/Controller/Arcade/DRUMFIRE.md.
+"""
+import hashlib
+import math
+import os
+import re
+import sys
+
+ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
+CHECK_ONLY = "--check" in sys.argv
+
+
+def guid(name: str) -> str:
+    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
+    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()
+
+
+# ── New script GUIDs (the .cs.meta files this script also writes) ─────────────
+G_SCRIPT = {
+    "DrumfireController":       guid("script/DrumfireController"),
+    "DrumfireTimeTurnMonitor":  guid("script/DrumfireTimeTurnMonitor"),
+    "DrumfireScoringRuleSO":    guid("script/DrumfireScoringRuleSO"),
+    "SpawnableDrum":            guid("script/SpawnableDrum"),
+}
+
+# ── New asset GUIDs ──────────────────────────────────────────────────────────
+G_ASSET = {
+    "ArcadeGameDrumfire":       guid("asset/ArcadeGameDrumfire"),
+    "DrumfireScoringRule":      guid("asset/DrumfireScoringRule"),
+    "MinigameDrumfire.unity":   guid("asset/MinigameDrumfire.unity"),
+    "SpawnableDrum.prefab":     guid("asset/SpawnableDrum.prefab"),
+    "DrumfireCellConfig":       guid("asset/DrumfireCellConfig"),
+    "DrumfireSpawnProfile":     guid("asset/DrumfireSpawnProfile"),
+    "DrumfireCellFolder":       guid("folder/DrumfireCell"),
+}
+
+# ── Existing GUIDs we reference (read from the repo, never invented) ──────────
+EXISTING = {
+    "SO_ArcadeGame":            "fe040efad3307fb449b6b72ad15362da",
+    "CellConfigDataSO":         "01f934d50526431a9392a6ceca1dc33d",
+    "SpawnProfileSO":           "e8d8aa5d835249798a256e18f2f7d912",
+    # donor scene wiring to swap out
+    "RampageController":        "e11ff862e6844a89a951292673243625",
+    "RampagePrismTurnMonitor":  "694b571734fe4a55a57f6cc672c7fcc2",
+    "RampageScoringRule":       "7d1bfbd4091c4a12a12c730553bf293a",
+    # shared content
+    "Vessel_Dolphin":           "c0f30e9f09616874780edc0a375ce686",
```

</details>

### `97ce58b40` — fix(arcade): take the launch panel and its preview window down on LAUNCH

_Claude, 2026-09-05 15:52:01 +0000_

```text
The arcade launch panel stayed on screen — with its live preview RenderTexture —
over the match it had just started.

Two independent gaps, both on the launch route only, which is why closing the
card with the ✕ always looked correct:

1. ModalWindowOut() is not the whole close on this modal. It fades the modal
   PREFAB's root (CanvasGroup + "Window Out" animator, both of which that prefab
   does have). The launch panel is NOT in that prefab — MinigameLaunchPanel is a
   Menu_Main SCENE object the modal drives — so nothing in that call reaches the
   panel, its controls block, its objective box, or the preview window sitting in
   it. HandleAllPlayersReady called only ModalWindowOut; the ✕ route
   (CloseAndNotifyClients) calls ShutDownPreview + _activePanel.Hide as well.
   The launch route now takes the same two teardown steps, and deliberately NOT
   CloseAndNotifyClients — that tells the party the config was DISMISSED, which
   is the opposite of what just happened.

2. Nothing told the WINDOW the preview had ended. ShutDownPreview is the only
   caller of ModePreviewWindow.Hide, and its own comment claimed three routes —
   "the modal closing, the modal being disabled, a launch" — of which the third
   did not exist. Meanwhile ModePreviewSession stops itself on launch through its
   own OnLaunchGame subscription (AbortHard), which returned the camera loan and
   struck the arena but did not raise OnPreviewEnded at all; and the modal's
   HandlePreviewEnded was an EMPTY method, on the reasoning that "the modal never
   went anywhere, so there is nothing to restore".

   That reasoning answers the wrong question. The modal is not what was showing
   the arena — the RawImage is, and it stays enabled over a RenderTexture nothing
   renders into any more. AbortHard now raises OnPreviewEnded (suppressed on the
   OnDestroy path, alongside the strike, for the reason already recorded there),
   and HandlePreviewEnded takes the frame down. The window's visibility now
   follows the SESSION's lifetime rather than modal-close choreography, so the
   freestyle-entry route (Stop → OnPreviewEnded, previously also silent) is
   covered by the same line.

Ordering is safe on every route: a stop followed by a new state already
re-asserts it AFTER the stop — a card change calls ShowLoading next, a failed
stand calls ShowUnavailable next (deliberately last, for exactly this reason) —
and hiding an already-hidden window is a no-op.

Deliberately NOT changed: ModalWindowManager.SetCanvasGroupVisible silently
returns when a root has no CanvasGroup. It is guarded by
[RequireComponent(typeof(CanvasGroup))] and all three modal roots satisfy it, so
it is not implicated here; ensuring the component would have changed behaviour on
two modals this branch has no business touching.

General rule this leaves behind: when a panel is torn down by TWO routes, the one
that is not "the user dismissed it" is the one that gets forgotten — and a
teardown method's own comment naming a route is not evidence the route exists.

Not editor-verified — no Unity in this session. Needs a play test: open an arcade
card, launch, and confirm the panel and its preview window are gone in the match.
```

```text
 Assets/_Scripts/Controller/Arcade/Preview/ModePreviewSession.cs | 16 ++++++++--
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs           | 62 +++++++++++++++++++++++++++++++++++----
 2 files changed, 70 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 132 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/Preview/ModePreviewSession.cs b/Assets/_Scripts/Controller/Arcade/Preview/ModePreviewSession.cs
index 6606d3e54..3264aca0a 100644
--- a/Assets/_Scripts/Controller/Arcade/Preview/ModePreviewSession.cs
+++ b/Assets/_Scripts/Controller/Arcade/Preview/ModePreviewSession.cs
@@ -161,7 +161,9 @@ namespace CosmicShore.Gameplay
         {
             Unsubscribe();
             Detach();
-            AbortHard(strikeWorld: false);   // never create GameObjects while the scene closes
+            // Never create GameObjects while the scene closes, and never raise into subscribers
+            // that are being destroyed alongside us.
+            AbortHard(strikeWorld: false, notify: false);
         }
 
         void Subscribe()
@@ -701,10 +703,12 @@ namespace CosmicShore.Gameplay
         /// dies with the scene; only the camera loan, the AI retarget and the runtime SO instance
         /// need explicit hands.</para>
         /// </summary>
-        void AbortHard(bool strikeWorld = true)
+        void AbortHard(bool strikeWorld = true, bool notify = true)
         {
             if (_state == State.Idle) return;
 
+            var mode = ActiveMode;
+
             _cts?.Cancel();
             _cts?.Dispose();
             _cts = null;
@@ -726,6 +730,14 @@ namespace CosmicShore.Gameplay
             SetLocalTrailPaused(false);
             _state = State.Idle;
             ActiveMode = GameModes.Random;
+
+            // (4) A hard abort ENDS THE PREVIEW, so it has to say so. Stop() raises this and
+            // AbortHard did not, which meant the one route that reaches here on its own - a
+            // LAUNCH - never told the window it had nothing left to draw. The frame then sat on
+            // screen holding the last picture its RenderTexture ever received, over the top of
+            // whatever the player did next. Suppressed on the OnDestroy path for the same reason
+            // the strike is: subscribers are being torn down alongside us.
+            if (notify) OnPreviewEnded?.Invoke(mode, ModePreviewOutcome.Abandoned);
         }
 
         void HandleLaunchRequested() => AbortHard();
diff --git a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
index 9849d7d4b..b4b842b00 100644
--- a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
+++ b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
@@ -1050,8 +1050,15 @@ namespace CosmicShore.UI
         }
 
         /// <summary>
-        /// Stop anything running in the window and let go of it. Called from every route that
-        /// takes the window off screen - the modal closing, the modal being disabled, a launch.
+        /// Stop anything running in the window and let go of it. Reached from exactly TWO routes -
+        /// the ✕ (<see cref="CloseAndNotifyClients"/>) and the modal being disabled.
+        ///
+        /// <para>This comment used to claim a third, "a launch", and that route does not exist:
+        /// <see cref="HandleAllPlayersReady"/> closes through <c>ModalWindowOut</c>, which fades a
+        /// CanvasGroup and never deactivates the GameObject, so <c>OnDisable</c> never fires. The
+        /// window is taken down on that route by <see cref="HandlePreviewEnded"/> instead, off the
+        /// session's own end event - which is the better place for it anyway, since it holds on
+        /// every stop rather than on the ones the modal happens to hear about.</para>
         /// </summary>
         void ShutDownPreview()
         {
@@ -1124,10 +1131,34 @@ namespace CosmicShore.UI
         }
 
         /// <summary>
-        /// A preview stopped - by the player clicking away, by the card changing, or by the modal
-        /// closing. Nothing to restore: the modal never went anywhere.
+        /// A preview stopped - by the player clicking away, by the card changing, by entering
+        /// freestyle, or by a LAUNCH. Whatever ended it, the arena is struck and the camera loan
+        /// is returned, so <b>the window has nothing left to draw</b> and must come down here.
+        ///
+        /// <para>This method used to be empty, on the reasoning that "the modal never went
+        /// anywhere, so there is nothing to restore". That answers the wrong question: the modal
+        /// is not what was showing the arena - the RawImage is, and it stays enabled over a
+        /// RenderTexture nothing renders into any more. The visible result is the launch panel
```

</details>

### `8850bb844` — docs(arcade): document Drumfire

_Claude, 2026-09-05 15:52:10 +0000_

```text
DRUMFIRE.md carries the design, the measurements and the tuning order, plus the
two findings worth keeping past this mode:

- A shot's yield falls as the SQUARE of its range and the floor is
  scale-invariant, so when a weapon's reach scales with the range it is fired
  from, "make the target bigger" is not a lever. Centre the trigger band on the
  lane's closest approach instead.
- Lane ownership is emergent from the spawn formation, so the ring radius and
  the lane radius are one number in two places and a generator should assert the
  pair rather than trusting a comment.

CLAUDE.md gains the mode paragraph, the scene table row, the controller
hierarchy entry, the documentation index row, the enum count (44 members, IDs
0..45 skipping 7 and 31), and a note on the spawn-ring row that a mode may now
build approach lanes on the formation rather than only spawn from it.

Also records the verification status honestly: everything was measured or
executed offline and nothing has been opened in Unity, with the exact in-editor
steps to confirm listed in DRUMFIRE.md section 9.
```

```text
 Assets/_Scripts/Controller/Arcade/DRUMFIRE.md      | 321 +++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Arcade/DRUMFIRE.md.meta |   7 ++
 CLAUDE.md                                          |  61 +++++++++-
 Docs/SCENES.md                                     |   3 +
 4 files changed, 390 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 462 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DRUMFIRE.md b/Assets/_Scripts/Controller/Arcade/DRUMFIRE.md
new file mode 100644
index 000000000..281d0f9f1
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/DRUMFIRE.md
@@ -0,0 +1,321 @@
+# Drumfire — the Dolphin's rhythm range
+
+`GameModes.Drumfire = 45` · scene `MinigameDrumfire` · `DrumfireController`
+· **Dolphin only** · 2–4 pilots · 2–3 domains · 4 intensities
+
+> **The one-line pitch.** A great porous drum of prisms hangs in the middle of the cell. Your own
+> line of crystals runs **past** it, not at it — so the target is always off to one side, and every
+> shot needs a deliberate turn off your flight vector. Drift to hold the line, swing the nose onto
+> the drum, take the next crystal to let the jaws go. **Fly, aim, shoot, repeat.** Most volume torn
+> out when the clock stops wins.
+
+Drumfire is the platform's first mode built to **teach a hull** rather than to test one. Everything
+below follows from that: it has no race target (a pilot who is losing is still practising), no
+opposing fire (nothing to react to except your own line), and one target that everybody shares.
+
+---
+
+## 1 · Why the Dolphin needs this
+
+The Dolphin has essentially one offensive act, and it is a three-step loop the vessel never
+explains:
+
+1. **Bank energy by SKIMMING** — the conic blast's gape is the energy you banked
+   (`MaxScale = lerp(400, 2080, energy)`), and the only way to bank it is to fly close to mass.
+2. **Fire by touching a CRYSTAL** — there is no trigger. The crystal *is* the trigger.
+3. **Aim by pointing the nose** — the blast leaves the jaws, and a drifting Dolphin's nose and
+   course are different directions.
+
+Rampage pays you for aiming that cone at a forest; The Bends pays you for catching a rival in it.
+Both assume you already know the loop. Drumfire is where you learn it, and the geometry is what
+teaches it — there is no tutorial text and no scripted beat.
+
+---
+
+## 2 · The lane — the whole lesson, expressed as geometry
+
+Each pilot is given **their own straight line of crystals**, struck through **their own spawn
+slot**. `CrystalManager.CrystalPlacementMode.ApproachLanes` is the platform capability this mode
+added; `ApproachLaneGeometry` is the pure math behind it (unit-tested in
+`ApproachLaneGeometryTests`).
+
+| Authored on the scene's `NetworkCrystalManager` | Value |
+|---|---|
+| `laneRingRadius` | **1120** |
+| `laneOffsetFromCenter` | **420** |
+| `laneLeadDistance` | **640** |
+| `laneLength` | **800** |
+| `laneFormation` | `Symmetric` |
+
+**The standoff is the mechanism.** A lane leaves a point on the spawn sphere and is tilted
+`theta` off the straight-in direction, so its closest approach to the centre is
+`ringRadius · sin(theta)`. Solving for the authored standoff gives
+
+```
+sin(theta) = laneOffsetFromCenter / laneRingRadius = 420 / 1120 = 0.375   ->   theta = 22.0 deg
+```
+
+so a pilot flying their own crystals passes the drum at **420u** from its centre — **100u clear of
+its 320u skin** — instead of flying into it. Because the lane never points at the target, the pilot
+cannot aim by steering: they have to hold the line and turn the nose. That is the drift.
+
+### 2.1 · The crystal band is centred on the pass, and getting that wrong killed the first design
+
+The lane's closest approach sits **1038u** along its 1120u+ run. The band therefore runs
+`640 … 1440`, straddling it: the pilot shoots on the way in, at the pass, and on the way out.
+
+The first cut ran the band `300 … 1800` — starting at the pass and running outward. It did not
+work, and the reason generalises:
+
+> **Blast yield falls as the square of range.** Measured single-shot yields across that band ran
+> from **6,678** panes (slot 1, full energy, at the pass) down to **926** (slot 3, empty). The
+> first volley of four shots destroyed **31.7 – 52.5 %** of the drum and the second pass added
+> **0.0 %** — the arena was gone before the rhythm had a second bar.
+
```

</details>

### `688df2c32` — fix(arcade): measure the scroll content to a fixed point, and say when a card is unreachable

_Claude, 2026-09-05 18:43:16 +0000_

```text
The 13th arcade card could be seen but not opened. Three separate-looking
symptoms - half a card drawn, a scroll that snapped back, a press that did
nothing - are one cause: the card sat below the ScrollRect content's reachable
range, where the viewport Mask clips the drawing AND, as an
ICanvasRaycastFilter, rejects the raycast. Favouriting a mode "fixed" it only
by moving something else into the last slot.

Two earlier attempts at the height each fell short, and the reason is the
useful part. Growing Content by what the new row costs assumes Content
contained its children exactly, and it did not - the authored 1104 was already
731 short of three authored rows whose lowest edge is at -1835.78. Measuring
once with CalculateRelativeRectTransformBounds fixed that and still fell short,
because Content's own VerticalLayoutGroup force-expands its children: surplus
height becomes spacing, which pushes the grid down and demands more height
again (1104 -> 1835 -> 2199 -> 2442 ...), so a single set can never catch up.

FitScrollContent now switches force-expand off on the scrolling Content - its
job is to be as tall as its contents, not to distribute an authored height -
and iterates grid-then-content until nothing grows. The needed height is
max(bounds.size.y, -bounds.min.y) because both rects are top-pivoted.

Every failure in this area was silent: a truncated roster left no gap and a
clipped row looked like a scroll that had ended, so both read as "not shipped
yet". ReportUnreachableCards names, once per repopulate, any mode with no slot
and any active card past the content's own height.

General rule recorded in CLAUDE.md and SWITCHBACK.md so mode 46 needs none of
this repeated: adding a card is adding a ROW, and a row is only reachable if
the scroll content was measured after it.
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md |  60 +++++++++++++++++++++++++-------
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs   | 101 ++++++++++++++++++++++++++++++++++++++++++++++++------
 CLAUDE.md                                       |   1 +
 3 files changed, 138 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 214 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index 74743e7b3..c26d5714e 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -395,19 +395,53 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
 
   **Adding the row is only half of it**, and the missing half reads as three unrelated bugs. The
   grid lives in a `ScrollRect` whose Content has a HARDCODED height (1104) and no
-  `ContentSizeFitter` — it never needed one, because the authored 3×4 grid fit exactly. A fourth
-  row therefore hangs below the viewport, and the viewport's `Mask` does two things to it: it
-  clips the drawing (you see the top of a card and nothing under it) **and**, being an
-  `ICanvasRaycastFilter` that rejects any point outside its own rect, it eats the CLICK. The
-  ScrollRect meanwhile has nothing to scroll, because content is still shorter than the viewport,
-  so a drag springs straight back (MovementType is Elastic). *Half a card, a scroll that snaps
-  back, and a dead button are one cause.* `GrowScrollContent` adds exactly what the new rows
-  occupy — `rowHeight + gridSpacing` each, and the grid's spacing is **negative** in Menu_Main
-  (the rows deliberately overlap), so it is added rather than assumed positive. Content is not
-  driven by a parent layout group, so its `sizeDelta` is ours to set and the result is
-  deterministic: 1104 → 1417.92 for one added row. Deliberately **not** a `ContentSizeFitter` —
-  that would re-derive the already-authored three rows' height from their preferred sizes instead
-  of the fractional anchors the scene uses, changing the existing arcade layout.
+  `ContentSizeFitter` — nobody had noticed, because the 3×4 grid was never scrolled to its end. A
+  card that ends up below the reachable range is clipped by the viewport's `Mask`, and that `Mask`
+  does two things to it: it cuts the drawing off (you see the top of a card and nothing under it)
+  **and**, being an `ICanvasRaycastFilter` that rejects any point outside its own rect, it eats the
+  PRESS. The ScrollRect meanwhile stops at the authored height, so dragging further springs back
+  (MovementType is Elastic). *Half a card, a scroll that snaps back, and a dead button are one
+  cause* — which is also why favouriting a mode "fixed" it: that only moved it out of the last
+  slot and moved something else in.
+
+  **Getting the height right took three passes, and the two failures are the useful part.**
+
+  1. *Increment.* Grow Content by what the new rows cost (`rowHeight + gridSpacing` — the grid's
+     spacing is **negative** in Menu_Main, the rows deliberately overlap). This assumes Content
+     previously contained its children exactly, and it did not: the authored 1104 was already
+     short of the three authored rows, whose lowest edge sits at −1835.78 in content space. The
+     increment landed short and the card stayed out of reach.
+  2. *Measure once.* Replace the increment with
+     `RectTransformUtility.CalculateRelativeRectTransformBounds`, which reads the real extent of
+     every **active** descendant at runtime. Still short.
+  3. *Measure to a FIXED POINT.* Content's own `VerticalLayoutGroup` has
+     `ChildForceExpandHeight: 1`, and under force-expand **height becomes spacing**: whatever
+     surplus Content has over what its children need is shared out between them, which pushes the
+     grid further down and demands more height again. Modelled numerically, one pass asks for
+     1835.78, which creates 545.64 of surplus, which pushes the grid 363.76 further down, which
+     asks for 2199.54 — a converging series that a single set can never catch. `FitScrollContent`
+     therefore switches force-expand **off** on the scrolling Content (its job is to be as tall as
+     its contents, not to distribute an authored height) and then iterates until nothing grows.
+
+  Two details that look like polish and are not: the needed height is
+  `max(bounds.size.y, -bounds.min.y)` because both rects are TOP-pivoted, so what has to be
+  covered is how far the lowest child reaches *below the origin* rather than the bounds' total
+  height; and the fit runs **last and unconditionally** in `PopulateGameSelectionList`, because
+  `CalculateRelativeRectTransformBounds` skips inactive objects and the pre-existing shortfall
+  wants repairing whether or not a row was added this time. Deliberately **not** a
+  `ContentSizeFitter` — that would re-derive the already-authored rows' height from their
+  preferred sizes instead of the fractional anchors the scene uses, changing the existing layout.
+
+  **And it now says so when it fails.** Every version of this bug was silent — a truncated roster
+  left no gap, and a clipped row looked like a scroll that had reached its end, so both read as
+  "that mode is not shipped yet". `ReportUnreachableCards` logs an error naming any mode with no
+  slot, and any card whose lowest edge sits past the content's own height, once per repopulate.
+
+  **The general rule for the next mode:** *adding a card is adding a ROW, and a row is only
+  reachable if the scroll content was measured after it — never assume an authored content height
+  contains what the scene authored into it.* The three moving parts are the roster bound (fixed),
+  the grid's capacity (`EnsureGridCapacity`, general) and the content's height (`FitScrollContent`,
+  general), so mode 46 needs none of this repeated.
 
 - **20 gates is unmeasured.** Chosen from the arithmetic (≈9.4k units of course; 2–3 minutes at
   realistic Dolphin speeds), not from a playtest. It is one editor field.
diff --git a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
index 10ab43629..12c23ba14 100644
--- a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
+++ b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
@@ -202,6 +202,7 @@ namespace CosmicShore.UI
             // on. Unconditional because the authored content height was already short of the
             // authored rows before any row was added - this is a repair as much as a fit.
             FitScrollContent();
+            ReportUnreachableCards(sortedGames.Count);
```

</details>

### `4856d1894` — fix(palette): read the CTA at signal strength, and test the switch reservation over every signal

_Claude, 2026-09-05 18:49:04 +0000_

```text
Ship review found two things in the lime next-gate work.

A CTA pair is authored for a CRYSTAL, and a crystal composes both halves -
lerp(dull, bright, (1-N.V)^4), so DarkCTA is ~93% of the surface and BrightCTA
a 2.5% rim. That is a statement about a crystal. A switch ring is a PRISM,
which composes nothing, so painting it from DarkCTA alone renders the shipped
OriginalColorSetSO value (0.28, 0.50, 0.08) as a dark olive rather than as the
free-pickup lime. SO_ColorSet.GetCtaSignalColor is the sibling of
GetDomainSignalColor, normalised identically, and gives (0.5625, 1, 0.1562) -
which also settles a disagreement the raw read had created, since the no-theme
fallback (0.55, 0.95, 0.15) sat 0.79 from the raw value and is 0.069 from the
normalised one, and a fallback that does not match what it falls back from is
not a fallback. It reports an unauthored CTA as alpha 0 rather than returning
black: both inactive palettes author the pair (0,0,0,0), and an accessor that
can return black can make an element vanish.

ToySwitchVocabularyTests guarded the reservation - only a Domain switch may
wear a playable domain - by naming Neutral. Adding Next left the law untested
for the new member. Both halves now enumerate every non-Domain signal from the
enum, so the next signal is covered on the day it is added. Measured against
the live palette, lime clears the 0.5 distinguishability gate on all four
domain colours (nearest Gold, 0.94).

Also records the in-editor checks for the lime next gate and for the thirteenth
arcade card, which had no steps.
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md          |  9 +++++++++
 Assets/_Scripts/Controller/Toys/ToyFactory.cs            | 31 ++++++++++++++++++++++---------
 Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs         | 27 +++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/ToySwitchVocabularyTests.cs | 46 +++++++++++++++++++++++++++++++---------------
 CLAUDE.md                                                |  2 +-
 Docs/PALETTE.md                                          | 28 ++++++++++++++++++++++++++++
 Docs/ToySystem/ARCHITECTURE.md                           | 24 +++++++++++++++++++-----
 7 files changed, 137 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 266 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index c26d5714e..584890c73 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -379,6 +379,15 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
     the all-element debuff for 4s. It does not change either pilot's gate count.
 13. **Regression — Rampage unchanged.** Launch Rampage: cactus forest, four intensity configs,
     prisms-destroyed scoring, Symmetric spawn. The two modes share a donor scene, not assets.
+14. **The next gate is LIME, and only yours.** Your next ring reads lime green while every other
+    ring reads neutral blue; thread it and the lime moves to the following ring. In a real lobby,
+    confirm the two pilots see the lime on DIFFERENT rings when their counts differ — the signal is
+    local and never replicated, so a shared lime would mean it is being set on the wrong side.
+15. **The arcade shows all thirteen cards.** Open the arcade with **no favourites set**. Scroll to
+    the bottom: a fourth row exists, every card is fully drawn, the scroll does not spring back,
+    and the last card OPENS its launch panel. Then favourite a mode and repeat — the card that
+    moves into last place must also open. The console must be silent: any
+    `ArcadeExploreView - the … card sits N units past …` error means the fit did not settle.
 
 ## Known limitations / follow-ups
 
diff --git a/Assets/_Scripts/Controller/Toys/ToyFactory.cs b/Assets/_Scripts/Controller/Toys/ToyFactory.cs
index f57dd4bbf..4617dd420 100644
--- a/Assets/_Scripts/Controller/Toys/ToyFactory.cs
+++ b/Assets/_Scripts/Controller/Toys/ToyFactory.cs
@@ -351,17 +351,30 @@ namespace CosmicShore.Gameplay
                 : DomainAccentColor(theme, SwitchDomain(signal, domain));
 
         /// <summary>
-        /// The free-pickup LIME - <c>SO_ColorSet.DarkCTA</c>, the platform's "this one is
-        /// available to you" colour. <b>Dark</b>, not <b>Bright</b>: in every crystal shader the
-        /// composition is <c>lerp(dull, bright, (1-N.V)^4)</c>, so <c>DarkCTA</c> is what ~93% of
-        /// a CTA-coloured surface actually shows and <c>BrightCTA</c> is a hairline rim
-        /// (<c>Docs/PALETTE.md</c> section 2.2) - a ring painted from the rim colour would read as
-        /// the wrong colour entirely.
+        /// The free-pickup LIME - the platform's "this one is available to you" colour, taken from
+        /// <c>SO_ColorSet.GetCtaSignalColor</c> and NOT from <c>DarkCTA</c> directly.
+        ///
+        /// <para>A switch ring is a PRISM, and a prism has no <c>lerp(dull, bright, (1-N.V)^4)</c>
+        /// composition to put the pair back together - so the dull half alone renders the shipped
+        /// (0.28, 0.50, 0.08) as a dark olive rather than as lime. The signal accessor normalizes
+        /// the CTA hue to full strength, which is also what makes the themed value and the fixed
+        /// fallback below agree: at (0.5625, 1, 0.15625) against (0.55, 0.95, 0.15) they are the
+        /// same colour, where reading DarkCTA raw put them 0.79 apart and a fallback that does not
+        /// match what it falls back FROM is not a fallback.</para>
+        ///
+        /// <para>The fixed value covers both an absent theme and a palette that authors no CTA at
+        /// all - <c>CosmicWaveColorSetSO</c> and <c>PastelColorSetSO</c> both author it (0,0,0,0),
+        /// so a raw read would paint the ring black on either.</para>
         /// </summary>
         public static Color CtaLime(ThemeManagerDataContainerSO theme)
-            => theme && theme.ColorSet && theme.ColorSet.EnvironmentColors != null
-                ? theme.ColorSet.EnvironmentColors.DarkCTA
-                : new Color(0.55f, 0.95f, 0.15f);
+        {
+            if (theme && theme.ColorSet)
+            {
+                var cta = theme.ColorSet.GetCtaSignalColor();
+                if (cta.a > 0f) return cta;
+            }
+            return new Color(0.55f, 0.95f, 0.15f);
+        }
 
         /// <summary>
         /// A <b>switch ring</b>: one continuous ring square across the flight path, at the radius
diff --git a/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs b/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs
index 2592c99b7..c66411da0 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs
@@ -64,6 +64,33 @@ namespace CosmicShore.ScriptableObjects
             return new Color(c.r / peak, c.g / peak, c.b / peak, 1f);
         }
 
+        /// <summary>
+        /// The free-pickup CTA at SIGNAL strength — the sibling of
+        /// <see cref="GetDomainSignalColor"/>, and needed for the same reason.
+        ///
+        /// <para><b>A CTA pair is authored for a CRYSTAL, and a crystal composes both halves.</b>
+        /// In every crystal shader the composition is <c>lerp(dull, bright, (1-N.V)^4)</c>, so
+        /// <see cref="EnvironmentColorSet.DarkCTA"/> paints ~93% of a CTA crystal and
+        /// <see cref="EnvironmentColorSet.BrightCTA"/> is a 2.5% hairline rim
```

</details>

### `7dcc44b42` — docs(skills): record the stub-harness accessor blind spot and the enum-member test gap

_Claude, 2026-09-05 18:50:05 +0000_

```text
Two things this session paid for and would otherwise be re-learned.

asset-surgery: a stub of a FIRST-PARTY type cannot verify an accessor path
through it, because writing the stub is the moment you decide the shape and the
shape is what needed checking. Stubbing SO_ColorSet with a DarkCTA field
compiled a call Unity rejected with CS1061 - the real field is on a nested
EnvironmentColorSet. The rule is to compile the real .cs plus a two-line probe,
and to prove the gate with a negative control first. Its asset-space sibling is
recorded alongside it: the class tells you the type, the assets tell you the
number.

ship: a rule-guarding test that names the enum members it knows about stops
testing the rule the day a member is added, and nothing fails to say so. At two
members a hand list and a loop look identical; the third is where it breaks.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 29 +++++++++++++++++++++++++++++
 .claude/skills/ship/SKILL.md          | 11 +++++++++++
 2 files changed, 40 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 91314a89c..75e141b78 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -783,6 +783,35 @@ file you did not write, naming nothing about the stub. Anything the target code
 `class X` / `struct X` in the real source rather than inferring from usage; it is one grep and it
 is the difference between a five-minute harness and a confusing one.
 
+**A stub of a PROJECT type cannot verify an accessor PATH through it — compile the real file
+instead.** The rules above are about stubbing ENGINE types faithfully. The trap is different when
+the type you stub is one of ours: writing the stub is the moment you decide what shape it has, so
+the harness confirms whatever you assumed and the assumption is precisely what you needed checked.
+A session stubbed `SO_ColorSet` with a `DarkCTA` field, compiled `theme.ColorSet.DarkCTA` green,
+and shipped a file Unity rejected with `CS1061` — the real `DarkCTA` lives on a nested
+`EnvironmentColorSet` reached through `ColorSet.EnvironmentColors`. No amount of stub discipline
+finds that, because the stub IS the claim under test. **So: never stub a first-party type whose
+member layout your new code depends on. Add its real `.cs` to the compile** (it usually drags in
+only a couple more engine stubs) and put the expression you are about to write in a two-line probe
+file next to it:
+
+```csharp
+// ColorProbe.cs — compiles the exact accessor chain the real call site will use.
+public static Color Lime(ThemeManagerDataContainerSO theme)
+    => theme && theme.ColorSet ? theme.ColorSet.GetCtaSignalColor() : Color.white;
+```
+
+Then **prove the gate**, the same way the base-class table above was produced: compile the probe
+with the WRONG path first and confirm you get the exact `CS1061` the editor gave, before fixing it.
+A harness that has not failed on the defect you are hunting is not a harness — and here the
+negative control is one line, so there is no excuse for skipping it.
+
+**The sibling of this in ASSET space: a value read off a class's field initializer is not the value
+the game runs on.** The same session read `DarkCTA`'s meaning from the C# and the shipped palettes
+disagreed — two of the three author it `(0,0,0,0)`. Whenever the code you are writing turns on an
+authored value, grep the `.asset` YAML for every instance of it and tabulate the real spread before
+deciding anything; the class tells you the type, the assets tell you the number.
+
 ### Fallback: `mcs` (only when dotnet can't be installed)
 
 `apt-get install mono-mcs` gives you `mcs`, and a Unity gameplay file usually touches a
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index f6f47de9a..63b22897f 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -168,6 +168,17 @@ Walk every changed file against these gates:
   away.** Ask what makes the two cases different at the SOURCE; if the answer is "the type", the
   heuristic is a bug waiting for the one mode that uses both.
 
+- **A rule-guarding test that NAMES the members it knows about stops testing the rule the day a
+  member is added.** A law expressed over an enum ("only `Domain` may wear a playable domain",
+  "only these metrics fold by sum") is usually guarded by a test that enumerates the cases by
+  hand, because at two members a hand list and a loop look identical. Add a third and the test
+  still passes, still reads as the law's guard, and now covers two thirds of it — and the gap is
+  invisible, because nothing fails. `ToySwitchVocabularyTests` guarded the switch reservation by
+  naming `Neutral`; `ToySwitchSignal.Next` landed and the law went untested for it. Whenever a
+  branch adds an enum member, grep for tests that mention the SIBLING members by name and convert
+  them to enumerate the enum (`Enum.GetValues(...).Where(x => x != TheException)`), so the next
+  member is covered on the day it is added rather than the day someone remembers.
+
 - **A comment asserting an ABSENCE rots exactly as silently as one asserting a presence.**
   §2's producer rule and its dead-surface mirror both cover claims about what the code DOES.
   The third shape is a comment that argues why something is NOT there — "no property block",
```

</details>

### `eb5683b01` — fix(ui): a modal closed while the menu is PAUSED never went away

_Claude, 2026-09-06 00:49:38 +0000_

```text
Second pass — the first fix was aimed at the wrong layer, and one of its premises
was wrong: MinigameLaunchPanel is not a Menu_Main scene object outside the modal.
The component is scene-ADDED, but onto ConfigurationContent, which lives inside
ArcadeGameConfigureModal.prefab. The panel is under the modal's root CanvasGroup,
so fading that group does hide it. Corrected here.

The actual defect is that the fade never ran.

1. ModalWindowOut() hands the visual half of the close to a coroutine —
   `yield return new WaitForSeconds(0.5f)` — and WaitForSeconds is scaled by
   Time.timeScale. THIS MENU RUNS PAUSED: PauseSystem.TogglePauseGame sets
   timeScale = 0 whenever the player is not flying, and the launch route closes
   the modal without unpausing first. So the coroutine could never complete,
   SetCanvasGroupVisible(false) never ran, and the window stayed fully on screen
   with every other part of the close already done — backdrop off, modal stack
   popped, isOn false, OnModalClosed raised. A modal that is logically shut and
   visually still there, which is exactly what a player reports as UI in the way
   that nothing takes down. Now WaitForSecondsRealtime, matching every other
   timing in this UI layer (ScreenSwitcher.SmoothMove, the freestyle cooldown).

2. ScreenSwitcher.CloseAllModals decided WHAT to close by visibility
   (cg.alpha > 0.01f) and then acted through ModalWindowOut, which is gated on
   OPENNESS (isOn). Those disagree in reachable states — ModalWindowIn refuses to
   open while the freestyle gate is engaged, and ArcadeLaunchPanel.Show makes the
   panel visible before asking its host modal to open — and in every one of them
   the modal ignored the close and stayed up over the flight. New
   ForceCloseImmediate() takes the window down regardless of isOn, and skips the
   animate-out dwell because this is the "get out of the way" path: half a second
   of menu over the start of flight is the complaint, not the remedy. The arcade
   modal is also closed via its own ScreenSwitcher field, since it is not required
   to be in the Modals list and is the one carrying a live RenderTexture.

3. The arcade modal now unwinds its CONTENT off its own OnModalClosed
   (HandleSelfClosed) instead of each caller remembering to. CloseAndNotifyClients
   and OnDisable did; the launch route, gamepad B and CloseAllModals did not. One
   subscription replaces one rule per caller — the difference between "every route
   we thought of" and "every route" — and it lets HandleAllPlayersReady go back to
   a bare ModalWindowOut().

General rule: when a caller picks its targets by one property and acts through a
method gated on another, the gap is invisible at both ends — the caller looks
correct, the callee looks correct, and the object in the middle is simply never
touched.

Not editor-verified — no Unity in this session.
```

```text
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs | 49 +++++++++++++++++++++----------------
 Assets/_Scripts/UI/Modals/ModalWindowManager.cs       | 64 ++++++++++++++++++++++++++++++++++++++++++++++++-
 Assets/_Scripts/UI/ScreenSwitcher.cs                  | 18 ++++++++++++--
 3 files changed, 108 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 193 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
index b4b842b00..46faaa05b 100644
--- a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
+++ b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
@@ -279,6 +279,15 @@ namespace CosmicShore.UI
 
         void OnEnable()
         {
+            // Whatever closes this modal, its CONTENT has to come down with it. The window and the
+            // content are separate things here - the preview's satellite arena and RenderTexture,
+            // the panel, the controls block - and each close route used to be responsible for
+            // remembering them: CloseAndNotifyClients did, OnDisable did, and the launch route,
+            // gamepad B and ScreenSwitcher.CloseAllModals did not. Hooking the modal's OWN close
+            // event makes that one subscription instead of one rule per caller, which is the
+            // difference between "every route we thought of" and "every route".
+            OnModalClosed += HandleSelfClosed;
+
             // On the one-panel layout the intensity row and the domain tiles live INSIDE whichever
             // panel the card selects, so they are wired when that panel becomes active (see
             // WireActivePanel) rather than here. Wiring both would double-subscribe every handler.
@@ -336,6 +345,8 @@ namespace CosmicShore.UI
         {
             base.OnDisable();
 
+            OnModalClosed -= HandleSelfClosed;
+
             UnwireActivePanel();
 
             if (!UsesLaunchPanels)
@@ -1060,6 +1071,20 @@ namespace CosmicShore.UI
         /// session's own end event - which is the better place for it anyway, since it holds on
         /// every stop rather than on the ones the modal happens to hear about.</para>
         /// </summary>
+        /// <summary>
+        /// This modal closed - by any route at all. Take the CONTENT down with the window.
+        ///
+        /// <para>Re-entrancy is safe: <c>_activePanel.Hide()</c> on a panel with a host modal
+        /// calls <c>ModalWindowOut()</c> back, and by the time this runs <c>isOn</c> is already
+        /// false, so that call returns immediately. The useful half of Hide - the preview window,
+        /// the controls block, the objective box, the micro toast - has already run by then.</para>
+        /// </summary>
+        void HandleSelfClosed()
+        {
+            ShutDownPreview();
+            if (_activePanel) _activePanel.Hide();
+        }
+
         void ShutDownPreview()
         {
             UnsubscribeFromPreviewSession();
@@ -2394,26 +2419,10 @@ namespace CosmicShore.UI
             _pendingWeeklyChallenge = false;
             if (config) config.ResetState();
 
-            // Close the modal on all instances.
-            //
-            // ModalWindowOut() is NOT the whole close, and on this modal it is barely half of it.
-            // It fades the modal PREFAB's own root (CanvasGroup + "Window Out"), and the launch
-            // panel is not in that prefab - MinigameLaunchPanel is a Menu_Main SCENE object the
-            // modal drives - so nothing in that call reaches the panel, its controls block, its
-            // objective box, or the live preview window sitting in it. The ✕ route hides all of
-            // that through CloseAndNotifyClients; the launch route never did, which left the panel
-            // and a frozen RenderTexture on screen over the match it had just started.
-            //
-            // Deliberately NOT CloseAndNotifyClients(): that tells the party the config was
-            // DISMISSED, which is the opposite of what just happened. This takes the same two
-            // teardown steps and leaves the notification alone.
-            //
-            // Safe after InvokeGameLaunch above: the session already unwound itself on the launch
-            // event (ModePreviewSession.HandleLaunchRequested), so Stop() inside ShutDownPreview
-            // no-ops on an Idle session and this is just the frame coming down.
-            ShutDownPreview();
-            if (_activePanel) _activePanel.Hide();
-
+            // Close the modal on all instances. The preview window and the panel come down with
+            // it through HandleSelfClosed (hooked to OnModalClosed), so this route needs no
+            // teardown of its own - deliberately NOT CloseAndNotifyClients(), which would tell the
+            // party the config was DISMISSED, the opposite of what just happened.
             ModalWindowOut();
         }
```

</details>

### `4ec0dbbe0` — fix(arcade): pin the scroll content's children before growing it, and report why a card cannot be pressed

_Claude, 2026-09-06 01:06:58 +0000_

```text
The previous fix stretched the Maelstrom banner, and the diagnosis behind it
was wrong. Content's VerticalLayoutGroup in Menu_Main is m_Enabled: 0 - it has
never laid anything out - so force-expand was never doing anything and turning
it off changed nothing. The layout is pure ANCHORS, and two of Content's three
children are anchored to a FRACTION of its height: the game grid spans y
0.218-0.843 (0.625 x H) and the Maelstrom banner spans 0.761-1.0 (0.239 x H).
Only the weekly challenge card is point-anchored, which is why it alone never
moved.

So every unit added to Content stretched the grid by 0.625 - its bottom
receded as fast as the content grew, which is why iterating could not reach it -
and stretched the banner by 0.239, from its authored 264 to 584 at the height
the iteration settled on. That is the too-tall card.

PinVerticalAnchorsToTop re-anchors each child to the content's top at the height
it is drawing right now. It is arithmetically a no-op, verified against both
children's authored values, and it is the VERTICAL axis only because the banner
is deliberately anchored wider than the content (x 0.474-1.715) so it runs past
the scroll view's right edge. After it, the content's height is a pure scroll
extent, the grid is sized to its rows once, and the fit settles in ONE pass at
1409.5 with the banner still 264.

The last slot has now been reported unpressable twice, and the scene cannot
settle why: the card is active, correctly titled and geometrically reachable, so
the press is being eaten by the geometry, the button or the progression lock and
on screen all three look identical. ReportCardPressability states that card's
path every repopulate whether or not it is broken - interactable flag, listener
count, and what a real EventSystem raycast at its centre actually lands on.

Two rules recorded in CLAUDE.md and SWITCHBACK.md: a ScrollRect's Content is a
scroll extent rather than a layout frame, so anything anchored to a fraction of
it is resized by every change to that extent; and a disabled component reads
exactly like an enabled one in a YAML dump, so check m_Enabled before reasoning
from a component's fields.
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md |  60 ++++++++++++++++-------
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs   | 140 +++++++++++++++++++++++++++++++++++++++++++-----------
 CLAUDE.md                                       |   3 +-
 3 files changed, 155 insertions(+), 48 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 279 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index 584890c73..e983a8176 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -413,38 +413,62 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
   cause* — which is also why favouriting a mode "fixed" it: that only moved it out of the last
   slot and moved something else in.
 
-  **Getting the height right took three passes, and the two failures are the useful part.**
+  **Getting the height right took FOUR passes, and the three failures are the useful part —
+  the third was wrong about the CAUSE while looking like it worked.**
 
   1. *Increment.* Grow Content by what the new rows cost (`rowHeight + gridSpacing` — the grid's
      spacing is **negative** in Menu_Main, the rows deliberately overlap). This assumes Content
      previously contained its children exactly, and it did not: the authored 1104 was already
-     short of the three authored rows, whose lowest edge sits at −1835.78 in content space. The
-     increment landed short and the card stayed out of reach.
+     short of the three authored rows, whose lowest edge sits at −1835.78 under the grid's own
+     stretched height. The increment landed short and the card stayed out of reach.
   2. *Measure once.* Replace the increment with
      `RectTransformUtility.CalculateRelativeRectTransformBounds`, which reads the real extent of
      every **active** descendant at runtime. Still short.
-  3. *Measure to a FIXED POINT.* Content's own `VerticalLayoutGroup` has
-     `ChildForceExpandHeight: 1`, and under force-expand **height becomes spacing**: whatever
-     surplus Content has over what its children need is shared out between them, which pushes the
-     grid further down and demands more height again. Modelled numerically, one pass asks for
-     1835.78, which creates 545.64 of surplus, which pushes the grid 363.76 further down, which
-     asks for 2199.54 — a converging series that a single set can never catch. `FitScrollContent`
-     therefore switches force-expand **off** on the scrolling Content (its job is to be as tall as
-     its contents, not to distribute an authored height) and then iterates until nothing grows.
-
-  Two details that look like polish and are not: the needed height is
+  3. *Iterate to a "fixed point", blamed on `ChildForceExpandHeight`.* **Wrong diagnosis, and it
+     shipped a second bug.** Content's `VerticalLayoutGroup` is `m_Enabled: 0` — it has never
+     laid anything out, so force-expand was never doing anything and switching it off changed
+     nothing. The real cause was one line up the hierarchy.
+  4. *Pin the children, then measure once.* **Content's layout is pure ANCHORS, and two of its
+     three children are anchored to a FRACTION of its height** — `MelstromMode` spans y
+     0.761→1.0 (0.239 × H) and `GameGrid` spans 0.218→0.843 (0.625 × H); only `WeeklyChallenge`
+     is point-anchored, which is why it alone never moved. So every unit added to Content
+     stretched the grid by 0.625 — the grid's bottom receded as fast as the content grew, which
+     is why no amount of iterating reached it — **and stretched the Maelstrom banner by 0.239**,
+     which is the too-tall card that pass 3 shipped (264 → 584 at the height it settled on).
+     `PinVerticalAnchorsToTop` re-anchors each child to Content's top at the height it is
+     drawing right now — arithmetically a no-op, verified against both children's authored
+     values — after which Content's height is a pure scroll extent, the grid is sized to its
+     rows once, and the fit settles in ONE pass at 1409.5 with the banner still 264.
+
+  **The rule pass 3 got wrong is worth more than the fix: a ScrollRect's Content is a SCROLL
+  EXTENT, not a layout frame.** Anything anchored to a fraction of it is resized by every change
+  to that extent, so "make the content taller" silently resizes the page. And the reason the
+  wrong diagnosis survived a round of review is that *a disabled component reads exactly like an
+  enabled one* in a YAML dump unless you look for `m_Enabled` — the layout group's fields were
+  all there, all plausible, and all inert.
+
+  Three details that look like polish and are not: the needed height is
   `max(bounds.size.y, -bounds.min.y)` because both rects are TOP-pivoted, so what has to be
   covered is how far the lowest child reaches *below the origin* rather than the bounds' total
-  height; and the fit runs **last and unconditionally** in `PopulateGameSelectionList`, because
+  height; the pin runs **before** the measurement, or the measurement is of a layout it is about
+  to move; and the fit runs **last and unconditionally** in `PopulateGameSelectionList`, because
   `CalculateRelativeRectTransformBounds` skips inactive objects and the pre-existing shortfall
-  wants repairing whether or not a row was added this time. Deliberately **not** a
-  `ContentSizeFitter` — that would re-derive the already-authored rows' height from their
-  preferred sizes instead of the fractional anchors the scene uses, changing the existing layout.
+  wants repairing whether or not a row was added this time. Only the VERTICAL anchors are
+  pinned — the Maelstrom banner is deliberately anchored wider than the content (x 0.474→1.715)
+  so it runs past the scroll view's right edge, and normalising that would move it on screen.
+  Deliberately **not** a `ContentSizeFitter`, which would re-derive the authored rows' height
+  from their preferred sizes rather than from the scene's anchors.
 
   **And it now says so when it fails.** Every version of this bug was silent — a truncated roster
   left no gap, and a clipped row looked like a scroll that had reached its end, so both read as
   "that mode is not shipped yet". `ReportUnreachableCards` logs an error naming any mode with no
-  slot, and any card whose lowest edge sits past the content's own height, once per repopulate.
+  slot and any card whose lowest edge sits past the content's own height. Beside it,
+  `ReportCardPressability` states the LAST card's press path every repopulate whether or not it
+  is broken — its `Button`'s interactable flag and listener count, and what a real `EventSystem`
+  raycast at the card's centre actually lands on. **That slot has been reported dead twice**, and
+  on screen a swallowed press looks identical whether the geometry, the button or the
```

</details>

### `ccc002e2b` — feat(sparrow): rank the missile's three radii, and let a bullet actually reach a pilot

_Claude, 2026-09-06 01:22:01 +0000_

```text
Two reported problems, one of them mine to have caused.

**Guns scored nothing in Dog Fight, and the wiring was never the issue.** The
combat-hit effect is on both gun containers and always was; the rounds were
TUNNELLING. The projectile mover teleports (`position += Velocity*dt`) and
PhysX samples a trigger once per FIXED timestep - 0.04s here - so a Sparrow
round covers 15u between samples at its base 375 u/s against a ~6u hull
window. Roughly 60% of otherwise-perfect shots passed straight through a
pilot, ~97% at SPACE 10. Prisms were already swept; vessels were not, so the
weapon read as working on the arena and broken on people.

`Projectile.sweptVesselDetection` is the exact twin of the prism flag, on
both gun rounds. A capsule OVERLAP rather than a sphere cast, because a cast
ignores colliders it starts already inside - which is precisely the
round-is-mid-hull case. Contacts are ordered nearest-first, deduplicated per
HULL (one hull is many colliders, and while the latch would collapse the
duplicates for scoring, every other effect in the container would still have
run once each), and `ProjectileImpactor` suppresses the trigger's vessel arm
on a sweeping round so nothing double-dispatches. Vessels sweep BEFORE prisms
so a hull strike is not consumed by a prism further along the same segment.
`SweepHit` now carries `ImpactorBase`, which both sweeps share.

**A rocket now scores by HOW CLOSE it got: 10 / 20 / 30.** `CombatHitClass`
gains `MissileBlast` and `MissileShockwave` (values 3 and 4, appended so every
already-serialized 0/1/2 keeps its meaning), and the three tiers are RANKED,
not additive: one skyburst reaches a pilot through three concentric radii and
a victim inside the inner one is always inside the outer ones, so
`VesselCombatHitLatch` folds all three onto ONE window per victim. A
centre-punch is worth 30, not 60.

The latch UPGRADES rather than first-wins, and that is forced by geometry
rather than taste: the warhead is both the largest radius and the fastest to
expand, so on an ordinary proximity kill the CHEAPEST tier lands first, and
first-wins would pay a centre-punch as a graze. `TryAdmit` reports what an
admission supersedes and `CombatHitScoring.Credit` pays only the difference,
without re-counting the raw missile hit - it is the same rocket arriving
closer. It never revises downward.

`DogFightScoringRuleSO` is now an exhaustive switch. The `hitClass == X ? a :
b` shape it replaces prices every enum member added later as the default arm,
which is how The Bends' Debuff class was once paid at the bullet rate - and
would have priced both new tiers at 1 here.

**50 prisms per rocket, and the pilot can see the charge.** `ammoPerPrism`
0.02 -> 0.01, on the ask to double the price. Nothing else moved: the tank is
still 0..1 and a rocket still costs 0.5. The Charge card (Charge upgrades the
skyburst, and the row is ordered charge/mass/space/time, so it is the first
card) gains the fleet's standard linear gauge filling toward the NEXT rocket
and resetting each time one is earned. The icon ladder says how many the bay
HOLDS and the gauge how close the next is - a tank of two cannot say both on
one bar - and both are driven from one `OnAmmoChanged` so they cannot
disagree. A full rack reads FULL, not empty: `frac(1.0/0.5)` is 0, and a gauge
that empties as the bay fills says the opposite of the truth.

The shot's COST comes from the weapon asset (`FireGunActionExecutor.
defaultAction`), never re-authored on the HUD where it could drift from what
the gun spends - the same reasoning `VesselRearmOnPrismDestruction` already
uses for its ammo index.

Verification status: 30 assertions run out of editor against the shipped
source and the shipped assets (16 new, 14 existing) - including the real
`CombatHitScoring.Credit` and `CombatHitClasses` compiled from the repo, so
the centre-punch-pays-30-not-60 case is tested code and not a mirror. Syntax
gate and `check_conditional_compilation.py` clean; asset integrity swept for
introduced dangling fileIDs and int64-overflowing anchors. NOT verified in the
editor: the gauge's on-screen placement inside the ability lockup, and the
swept-vessel fix in play. `author_dogfight_assets.py` is updated to keep the
numbers authored rather than hand-edited, but its `--check` cannot run - it is
a documented one-shot whose donor scene has since moved on, and it already
failed that way at HEAD.
```

```text
 .../Vessel Explosion Effects/VesselCombatHitByMissileBlast.asset      |   2 +-
 .../Vessel Explosion Effects/VesselCombatHitByMissileShockwave.asset  |  17 ++
 .../VesselCombatHitByMissileShockwave.asset.meta                      |   8 +
 ...lCombatHitByMissile.asset => VesselCombatHitByMissileDirect.asset} |   2 +-
 ...ByMissile.asset.meta => VesselCombatHitByMissileDirect.asset.meta} |   0
 Assets/_SO_Assets/Scoring Rules/DogFightScoringRule.asset             |   4 +-
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md                         |  79 ++++--
 Assets/_Scripts/Controller/Arcade/SALVO.md                            |   4 +-
 Assets/_Scripts/Controller/Arcade/Scoring/CombatHitScoring.cs         |  46 +++-
 Assets/_Scripts/Controller/Arcade/Scoring/DogFightScoringRuleSO.cs    |  44 +++-
 Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md              |   2 +-
 .../ImpactEffects/EffectsSO/Helpers/VesselCombatHitLatch.cs           |  59 ++++-
 .../Vessel Explosion Effects/VesselCombatHitByExplosionEffectSO.cs    |   4 +-
 .../Vessel Projectile Effects/VesselCombatHitByProjectileEffectSO.cs  |   4 +-
 .../_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs |   8 +
 Assets/_Scripts/Controller/Managers/StatsManager.cs                   |  17 +-
 Assets/_Scripts/Controller/Player/Player.cs                           |  10 +-
 Assets/_Scripts/Controller/Projectiles/Projectile.cs                  | 153 ++++++++++-
 .../Vessel/R_VesselActions/Executors/FireGunActionExecutor.cs         |  85 +++++-
 .../Controller/Vessel/R_VesselActions/SPARROW_SKYBURST_BAY.md         |  46 +++-
 Assets/_Scripts/Controller/Vessel/VesselRearmOnPrismDestruction.cs    |   6 +-
 Assets/_Scripts/Data/Enums/CombatHitClass.cs                          |  83 ++++--
 Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs                | 454 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs.meta           |  11 +
 Assets/_Scripts/UI/Controller/SparrowHUDController.cs                 |  17 ++
 Assets/_Scripts/UI/View/SparrowHUDView.cs                             |  45 ++++
 CLAUDE.md                                                             |  32 ++-
 Docs/VESSEL_TAIL_AND_JETS.md                                          |   5 +-
 Tools/Build/author_dogfight_assets.py                                 | 106 ++++++--
 34 files changed, 1326 insertions(+), 110 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1845 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
index ef3316deb..e6728059e 100644
--- a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
+++ b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
@@ -10,9 +10,16 @@
 
 Dog Fight is the **Sparrow-only gun duel**. Two to four pilots hunt each other through the
 **Boneyard** — a wrecked world of hollow hulks, leaning spires and rubble canyons built for
-close encounters and hiding places. A **bullet hit scores 1**, a **missile hit scores 50**
-(direct strike *or* caught in the blast), and the first **DOMAIN** to the point target
-(default **90**) wins.
+close encounters and hiding places. A **bullet hit scores 1**, a **rocket scores by HOW CLOSE
+it got** — **10** for the warhead shockwave, **20** for the prism blast, **30** for a direct
+strike — and the first **DOMAIN** to the point target (default **90**) wins.
+
+**The three missile tiers are RANKED, not additive.** One skyburst reaches a pilot through three
+concentric radii and a victim inside the inner one is always inside the outer ones, so
+`VesselCombatHitLatch` folds all three onto ONE window per victim and pays the best tier
+achieved: a centre-punch is worth 30, not 10+20+30. The shockwave is the ordinary outcome (the
+proximity fuze trips at 20x the round's hit radius, so a rocket almost always detonates before
+it can touch a hull) and the two inner tiers are correspondingly rare.
 
 **One axis, and it is gunnery.** The scored stat is `IRoundStats.CombatPoints` — a weighted sum
 of landed vessel-vs-vessel hits. Nothing else scores: not the wreckage, not crystals, not
@@ -120,9 +127,14 @@ DogFightController.OnTurnEndedCustom → AssignScores → SyncFinalScores_Client
 ## Where the point VALUES live, and why
 
 The platform counts landed hits as **raw facts** (`BulletHitsLanded` / `MissileHitsLanded`) and
-has no opinion about what one is worth. `DogFightScoringRuleSO` says a bullet is 1 and a rocket
-is 50, through the new `ScoringRuleSO.PointsForCombatHit` virtual (default **0** — every other
-mode counts gunnery and scores none of it).
+has no opinion about what one is worth. `DogFightScoringRuleSO` prices a bullet at 1 and a
+rocket by proximity — 10 / 20 / 30 — through the `ScoringRuleSO.PointsForCombatHit` virtual
+(default **0** — every other mode counts gunnery and scores none of it).
+
+It is written as an **exhaustive switch**, not `hitClass == X ? a : b`. That shape prices every
+enum member added later as the default arm, which is exactly how The Bends' `Debuff` class was
+once paid at the bullet rate — and it would have priced both new missile tiers at 1 here. A
+class this mode has no opinion about is worth 0 and says so.
 
 **Both of the Sparrow's fire modes count as "bullet".** Full-auto rounds and turret-stance prism
 rounds are the same weapon class — one direct projectile hit — so
@@ -188,8 +200,8 @@ rather than each carrying its own:
 1. **A rocket scores through two code paths for one shot.** A skyburst that hits a vessel
    directly *detonates on impact* (`VesselSpinBySkyBurstProjectileEffectSO.detonateOnHit`), so
    the direct hit fires from `ProjectileImpactor` and the blast fires again from
-   `ExplosionImpactor` a fraction of a second later. One missile, two events — and at 50 points
-   each that is not a rounding error.
+   `ExplosionImpactor` a fraction of a second later, and the *warhead* blast fires from a third.
+   One missile, three events — and at 10-30 points each that is not a rounding error.
 2. **A hull is more than one collider.** The Squirrel carries two box colliders and the Manta a
    body per wing, so a single blast sphere raises `OnTriggerEnter` once per pair.
    `VesselImpactor` already latches *crystals* for exactly this reason.
@@ -198,9 +210,19 @@ The window is therefore also an anti-spam floor: two genuinely different rockets
 same pilot inside 0.5 s score once. That is intended — a dogfight should reward two hits a
 second apart, not a shotgun of simultaneous detonations.
 
-The generator **asserts** the two missile effects carry the same non-zero cooldown, because
+The generator **asserts** all three missile effects carry the same non-zero cooldown, because
 splitting them silently reinstates the double-count.
 
+**The latch UPGRADES rather than first-wins, and that ordering is forced by geometry.** The
+warhead is both the largest radius and the fastest to expand, so on an ordinary proximity kill
+the *cheapest* tier lands first. Under first-wins it would claim the window and a victim who was
+also inside the blast — or took the round on the nose — would be paid as a graze. `TryAdmit`
+therefore reports what an admission **supersedes** (`out int supersededRank`), and
+`CombatHitScoring.Credit` pays only the difference: the raw `MissileHitsLanded` count is *not*
+incremented on an upgrade, because it is the same rocket arriving closer. It never revises
+downward — a shockwave arriving after a direct hit is the same rocket's outer edge and is
+refused.
+
 ## The skyburst launches from the missile bay (2026-08)
 
 The rocket no longer materializes at a floating gun point: the press opens the Sparrow's
@@ -208,7 +230,7 @@ animated missile bay (right bay first, left bay second) and the projectile — n
 own missile, not the wedge polyhedron — spawns **0.2 s later at the live bay bone's pose**
 (`SkyBurstGunAction.launchDelaySeconds`; `FireGunActionExecutor` cancels a pending launch on
 turn end or vessel teardown, with ammo staying spent). For this mode that means ~0.2 s of
-fire-to-impact latency on the 50-point weapon; the scoring path, cooldown latch, hit sphere,
+fire-to-impact latency on the 10-30-point weapon; the scoring path, cooldown latch, hit sphere,
```

</details>

### `77f4f632c` — tools(diagnostics): name the UI that is actually on screen, instead of guessing at it

_Claude, 2026-09-06 01:45:07 +0000_

```text
FrogletTools > Diagnostics > Report On-Screen UI. Reader tool - reports only,
writes no assets, so no ship panel or change ledger.

Run it in play mode while the unwanted UI is showing. It lists every enabled
Graphic whose screen rect covers >=2% of the display, biggest first, with the
full hierarchy path, the effective alpha AND which CanvasGroup set it, whether it
raycasts, its sprite/texture, and its canvas + render mode + sorting order. Two
things that are not Graphics get their own sections because both draw over the
game and neither appears in a UI hierarchy: cameras with a partial viewport rect
or a leftover targetTexture, and VideoPlayers on a camera-plane render mode.

Written because I got this wrong three times in a row. "There is UI in the way"
is a report about a RENDERED FRAME, and a frame is precisely what reading scenes
and prefabs statically cannot show you: whether a panel is on screen is the
product of its GameObject's active state, its Graphic's enabled flag, everything
every CanvasGroup above it multiplied its alpha by, which Canvas draws it in what
order, and whatever a controller last did to it - five facts in five files, each
of which reads as fine on its own. I inspected several of them and produced three
confident wrong answers: a scene object that turned out to be inside the modal
prefab, a missing CanvasGroup that was actually present (my prefab parser had
assumed the first GameObject in the YAML is the root, which is not guaranteed),
and a connecting panel whose authored colour does not match what is on screen.

The general rule: when the symptom is "what I see", the fix has to start from a
measurement of what is drawn, not from an inference about what should be.
```

```text
 Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs      | 228 ++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs.meta |  11 ++
 2 files changed, 239 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 234 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs b/Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs
new file mode 100644
index 000000000..7119a69f9
--- /dev/null
+++ b/Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs
@@ -0,0 +1,228 @@
+using System;
+using System.Collections.Generic;
+using System.Linq;
+using System.Text;
+using UnityEditor;
+using UnityEngine;
+using UnityEngine.UI;
+using UnityEngine.Video;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Name every piece of UI that is actually ON SCREEN right now, biggest first.
+    ///
+    /// <para><b>Why this exists.</b> "There is UI in the way" is a report about a rendered frame,
+    /// and a rendered frame is the one thing static analysis of scenes and prefabs cannot see. A
+    /// panel is on screen because of the product of several things that live in different files -
+    /// is its GameObject active, is its Graphic enabled, what did every CanvasGroup above it
+    /// multiply its alpha by, which Canvas draws it and in what order, and did some controller
+    /// leave it that way - and reading any one of them in isolation invites a confident wrong
+    /// answer. Three of those in a row is what this tool is a reaction to.</para>
+    ///
+    /// <para><b>Use it while the bad frame is on screen.</b> Enter play mode, get to the state
+    /// where the unwanted UI is showing, then run this. It reports every enabled
+    /// <see cref="Graphic"/> whose screen rect covers at least <see cref="MinCoverage"/> of the
+    /// display, with the full hierarchy path, the effective alpha and WHICH CanvasGroup set it,
+    /// whether it eats clicks, its texture/sprite, and its canvas. The offender is normally the
+    /// first line.</para>
+    ///
+    /// <para>Two things that are not Graphics get their own sections, because both draw over the
+    /// game and neither shows up in a UI hierarchy: cameras rendering into a RenderTexture or
+    /// restricted to a partial viewport rect, and VideoPlayers set to draw on a camera plane.</para>
+    ///
+    /// <para>READER TOOL: reports only, writes no assets, so it carries no ship panel or change
+    /// ledger (<c>Docs/TOOLING.md</c> § "Tool output is a deliverable").</para>
+    /// </summary>
+    public static class OnScreenUIReport
+    {
+        /// <summary>Ignore anything smaller than this fraction of the screen - the question is
+        /// "what is IN THE WAY", and a 2% badge never is.</summary>
+        const float MinCoverage = 0.02f;
+
+        [MenuItem("FrogletTools/Diagnostics/Report On-Screen UI")]
+        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
+                     Description = "In play mode: name every UI element actually covering the " +
+                                   "screen right now, biggest first, with its alpha, its owning " +
+                                   "CanvasGroup and its canvas. Run it while the bad frame is up.")]
+        static void Run()
+        {
+            if (!Application.isPlaying)
+            {
+                EditorUtility.DisplayDialog(
+                    "Report On-Screen UI",
+                    "Enter play mode first, get to the frame where the unwanted UI is showing, " +
+                    "then run this again.\n\nThis reads what is actually rendered - outside play " +
+                    "mode there is no frame to read.",
+                    "OK");
+                return;
+            }
+
+            var sb = new StringBuilder();
+            sb.AppendLine($"=== ON-SCREEN UI REPORT === {Screen.width}x{Screen.height}  " +
+                          $"timeScale={Time.timeScale}");
+
+            ReportGraphics(sb);
+            ReportCameras(sb);
+            ReportVideoPlayers(sb);
+
+            Debug.Log(sb.ToString());
+            EditorGUIUtility.systemCopyBuffer = sb.ToString();
+            Debug.Log("[OnScreenUIReport] Report copied to the clipboard.");
+        }
+
+        static void ReportGraphics(StringBuilder sb)
```

</details>

### `af5a16803` — diag(arcade): log the press itself, and record which slots the lock ate

_Claude, 2026-09-06 02:46:42 +0000_

```text
The last card's dead press has been reported twice and every static lead is
now closed, so the remaining question is not "what is on top of the card" but
"does the click arrive at all" - and nothing was measuring that. A press that
never reaches SelectGame and a press that reaches it and gets no modal are the
same observation on screen and have nothing in common underneath: one is the
grid's problem, the other the modal's.

- SelectGame logs the press, naming the card and whether the configure modal
  reference is even wired.
- ReportCardPressability reports whether a SelectGame listener was attached.
  This is RECORDED at the decision (_lockedSlots), never read back off the
  Button: onClick can report its PERSISTENT count and nothing else, and
  SelectGame is added at runtime, so the count reads identically on a wired
  card and an unwired one.
- It also states how many cards are active and how many were locked, so a
  difference between the last card and the other twelve is visible without
  printing thirteen lines.

SWITCHBACK.md records what the failure is NOT, so none of it is re-checked:
nothing is drawn over the grid (the scroll view is Explore's last child and the
panel's later siblings miss it by 600px); all twelve authored cards are
component-identical and the cloned row copies one of them; SetLocked is
symmetric and _originalBgColor initialises to white; the CTA indicator is a
96x96 corner badge with RaycastTarget 0; MinigameLaunchPanel accepts every
non-Maelstrom card and is not a HostModal, so OpenFor always calls
ModalWindowIn; and ten of the thirteen roster modes - Switchback among them -
are absent from GameModeQuestList entirely, so the progression chain cannot
single a card out here.

It also records a roster fact that changes what "the last spot" means: the
injected SO_GameList is OrganicRematchGames (wired on AppManager.prefab), whose
sorted tail is Switchback, The Bends, Wildlife Liberation. Favouriting sorts
favourites first, so favouriting anything other than Wildlife Liberation leaves
Wildlife Liberation in slot 13 - every report so far is equally consistent with
"Wildlife Liberation is dead", and the two have not been told apart.

Verified: the shipped method text extracted verbatim and compiled through
Roslyn at langversion 9 against the stub harness (clean, warnings only); the
full file parse-checked with zero CS1xxx; check_conditional_compilation.py OK
(1887 files); author_switchback_assets.py --check OK (22 files match).
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md | 43 ++++++++++++++++++++++++++++++++++++++-----
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs   | 32 +++++++++++++++++++++++++++++---
 2 files changed, 67 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 134 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index e983a8176..e30fe7cc0 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -464,11 +464,44 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
   "that mode is not shipped yet". `ReportUnreachableCards` logs an error naming any mode with no
   slot and any card whose lowest edge sits past the content's own height. Beside it,
   `ReportCardPressability` states the LAST card's press path every repopulate whether or not it
-  is broken — its `Button`'s interactable flag and listener count, and what a real `EventSystem`
-  raycast at the card's centre actually lands on. **That slot has been reported dead twice**, and
-  on screen a swallowed press looks identical whether the geometry, the button or the
-  progression lock ate it; reasoning could not separate them from the scene alone, so the view is
-  made to say which.
+  is broken — its `Button`'s interactable flag, whether a `SelectGame` listener was attached, and
+  what a real `EventSystem` raycast at the card's centre actually lands on — and `SelectGame`
+  itself logs the press. **That slot has been reported dead twice**, and on screen a swallowed
+  press looks identical whether the geometry, the button, the lock or the modal ate it; reasoning
+  could not separate them from the scene alone, so the view is made to say which.
+
+  Two details there are not incidental. **Whether `SelectGame` was wired is RECORDED at the
+  decision, never read back off the `Button`** — `onClick` can report its PERSISTENT count and
+  nothing else, and `SelectGame` is added at runtime, so the count reads identically on a wired
+  card and an unwired one; `_lockedSlots` is the record. And the press is logged in `SelectGame`
+  because that is the OTHER half of the measurement: everything the card-side report can see is
+  about the card, and "the click never arrived" and "the click arrived and the modal declined to
+  open" are the same observation on screen with nothing in common underneath — one is the grid's
+  problem, the other the modal's.
+
+  **What the last slot's dead press is NOT** (each ruled out from the shipped assets, so none of
+  it is worth re-checking): nothing is drawn over the grid — `GameSelectScrollView` is `Explore`'s
+  last child, and the panel's only later siblings (`ArcadeLobbyList`, `CloseButton`) miss it,
+  since the scroll view is inset 600px from the panel's left edge. All twelve authored cards are
+  component-identical (`RectTransform, CanvasRenderer, Image, Mask, SpriteMask, Button, GameCard,
+  CallToActionTarget, MenuAudio`) across three rows of four, and the cloned row is a copy of one
+  of them; `LayoutGroup.SetChildAlongAxis` forces child anchors, so the grid's enabled
+  `VerticalLayoutGroup` normalises row 4 like the rest. `GameCard.SetLocked` is symmetric
+  (`btn.interactable = !locked`) and `_originalBgColor` initialises to `Color.white`, so a card
+  cannot be left dead by a lock it no longer has. The `CallToActionIndicator` is a 96x96 corner
+  badge with `RaycastTarget: 0`, inactive by default. `MinigameLaunchPanel.Handles` accepts every
+  non-Maelstrom card and is not a `HostModal`, so `OpenFor` always calls `ModalWindowIn`. And the
+  progression chain cannot single a card out here: **ten of the thirteen roster modes are absent
+  from `GameModeQuestList` entirely**, Switchback among them, and Switchback plays.
+
+  One roster fact is worth carrying because it changes what "the last spot" means. The injected
+  `SO_GameList` is `OrganicRematchGames` (wired on `AppManager.prefab`) — 14 entries, 13
+  renderable once the Maelstrom is excluded — and sorted the way `PopulateGameSelectionList`
+  sorts it, the tail is Switchback, The Bends, **Wildlife Liberation**. Favouriting sorts
+  favourites first, so favouriting anything OTHER than Wildlife Liberation leaves Wildlife
+  Liberation in slot 13. *Every report of "the last slot is dead" so far is therefore also
+  consistent with "Wildlife Liberation is dead", and the two have not yet been told apart* —
+  favouriting Wildlife Liberation itself is the one-press experiment that separates them.
 
   **The general rule for the next mode:** *adding a card is adding a ROW, and a row is only
   reachable if the scroll content was measured after it — never assume an authored content height
diff --git a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
index cf5a8a6d7..db45a2ec8 100644
--- a/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
+++ b/Assets/_Scripts/UI/Views/ArcadeExploreView.cs
@@ -40,6 +40,12 @@ namespace CosmicShore.UI
         SO_ArcadeGame SelectedGame;
         List<GameCard> GameCards;
 
+        // Slots the progression chain locked THIS populate. Recorded rather than re-derived,
+        // because "did this card get a SelectGame listener?" is only answerable at the moment the
+        // decision is made - Button.onClick can report its PERSISTENT count and nothing else, so a
+        // runtime listener is invisible to any later inspection.
+        readonly List<int> _lockedSlots = new();
+
         // The sync manager this view subscribed to, remembered so the unsubscribe cannot miss
         // it if the scene's instance is replaced between enable and disable.
         ArcadeConfigSyncManager _pickSource;
@@ -82,6 +88,7 @@ namespace CosmicShore.UI
         public void PopulateGameSelectionList()
         {
             GameCards = new List<GameCard>();
+            _lockedSlots.Clear();
             // Rebuild the dpad grid from scratch - AddRow calls below would otherwise
             // append duplicate rows on every repopulate (inventory load, progression
             // change, favorite toggle), breaking gamepad navigation.
@@ -178,6 +185,8 @@ namespace CosmicShore.UI
```

</details>

### `695485d81` — fix(arcade): inject the cloned card row, and stop a persistent listener eating the press

_Claude, 2026-09-06 14:22:04 +0000_

```text
The 13th arcade card rendered with the right title and art, reported
interactable=true, passed an EventSystem raycast, and opened nothing. Four
investigations chased geometry, masks, scroll extents, overlays, the
progression lock and the modal. It was none of those.

MenuAudio.PlayAudio is the ONE persistent onClick listener every GameCard's
Button carries, and it dereferences an [Inject] AudioSystem. Reflex populates
[Inject] for objects present at SCENE LOAD; EnsureGridCapacity creates its row
at RUNTIME to hold a 13th mode, so nothing injected it and that field was null
on all four of its cards.

UnityEvent.Invoke runs PERSISTENT listeners BEFORE runtime ones
(InvokableCallList.PrepareInvoke: m_ExecutingCalls = persistent + runtime) and
guards neither, so the NullReferenceException from PlayAudio aborted the invoke
before reaching the runtime listener the view attaches, () => SelectGame(game).
A null [Inject] on a UI object is not a lost feature, it is a dead button.

That is why it read as positional rather than as belonging to a mode: only
cards in a cloned row are un-injected, and at 13 modes only the first of them
is active. At 14 modes the second would have died too. Favouriting worked
around it by moving a mode onto one of the twelve authored cards, which the
scene's ContainerScope had injected at load.

Fixed in two layers, and the second is the one that generalises:

1. Inject at the creating site. ArcadeExploreView holds [Inject] Container and
   calls GameObjectInjector.InjectRecursive on every row it clones, so any
   [Inject] field any future card component grows is covered too.
   ProjectilePoolManager already carries the identical fix for the identical
   reason ("an un-injected projectile NREs on its null AudioSystem in
   LaunchProjectile and every shot from that instance is a dud") - this is the
   second outing of one bug class, not a new one.

2. Fail safe on the persistent listener. Layer 1 must be remembered once per
   spawn site forever; layer 2 holds everywhere at once. MenuAudio falls back
   to AudioSystem.Instance and warns once per component, so the next
   runtime-created UI object nobody injects loses a SOUND rather than a BUTTON.

MenuAudioResilienceTests pins both halves: an un-injected MenuAudio must not
throw, and must stay quiet across repeated presses (a per-press report is
indistinguishable from spam).

CLAUDE.md records the rule beside the existing [Inject]-at-runtime
anti-pattern; SWITCHBACK.md replaces its speculative "Open" section with the
root cause, the two-layer fix, and the six things that were ruled out - kept
because the lesson is the search, not the list: when a card is authored
identically to eleven working ones and still does nothing, stop auditing the
asset and ask what is only true of it at RUNTIME.

Verified: MenuAudio.cs compiled in full through Roslyn at langversion 9 against
a stub harness (clean, only the expected CS0649 for [SerializeField]/[Inject]
fields); ArcadeExploreView.cs and the new test parse-checked with zero CS1xxx;
check_conditional_compilation.py OK (1888 files);
author_switchback_assets.py --check OK (22 files match).
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md               | 68 +++++++++++++++++++++++++++--------------
 Assets/_Scripts/Tests/Editor/MenuAudioResilienceTests.cs      | 67 ++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/MenuAudioResilienceTests.cs.meta | 11 +++++++
 Assets/_Scripts/UI/MenuAudio.cs                               | 57 +++++++++++++++++++++++++++++++++-
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs                 | 37 ++++++++++++++++++++++
 CLAUDE.md                                                     |  1 +
 6 files changed, 217 insertions(+), 24 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 305 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index e30fe7cc0..d8fe708fe 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -479,29 +479,51 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
   open" are the same observation on screen with nothing in common underneath — one is the grid's
   problem, the other the modal's.
 
-  **What the last slot's dead press is NOT** (each ruled out from the shipped assets, so none of
-  it is worth re-checking): nothing is drawn over the grid — `GameSelectScrollView` is `Explore`'s
-  last child, and the panel's only later siblings (`ArcadeLobbyList`, `CloseButton`) miss it,
-  since the scroll view is inset 600px from the panel's left edge. All twelve authored cards are
-  component-identical (`RectTransform, CanvasRenderer, Image, Mask, SpriteMask, Button, GameCard,
-  CallToActionTarget, MenuAudio`) across three rows of four, and the cloned row is a copy of one
-  of them; `LayoutGroup.SetChildAlongAxis` forces child anchors, so the grid's enabled
-  `VerticalLayoutGroup` normalises row 4 like the rest. `GameCard.SetLocked` is symmetric
-  (`btn.interactable = !locked`) and `_originalBgColor` initialises to `Color.white`, so a card
-  cannot be left dead by a lock it no longer has. The `CallToActionIndicator` is a 96x96 corner
-  badge with `RaycastTarget: 0`, inactive by default. `MinigameLaunchPanel.Handles` accepts every
-  non-Maelstrom card and is not a `HostModal`, so `OpenFor` always calls `ModalWindowIn`. And the
-  progression chain cannot single a card out here: **ten of the thirteen roster modes are absent
-  from `GameModeQuestList` entirely**, Switchback among them, and Switchback plays.
-
-  One roster fact is worth carrying because it changes what "the last spot" means. The injected
-  `SO_GameList` is `OrganicRematchGames` (wired on `AppManager.prefab`) — 14 entries, 13
-  renderable once the Maelstrom is excluded — and sorted the way `PopulateGameSelectionList`
-  sorts it, the tail is Switchback, The Bends, **Wildlife Liberation**. Favouriting sorts
-  favourites first, so favouriting anything OTHER than Wildlife Liberation leaves Wildlife
-  Liberation in slot 13. *Every report of "the last slot is dead" so far is therefore also
-  consistent with "Wildlife Liberation is dead", and the two have not yet been told apart* —
-  favouriting Wildlife Liberation itself is the one-press experiment that separates them.
+  **And the last slot's dead press was none of those things — it was DEPENDENCY INJECTION.**
+  `MenuAudio.PlayAudio` is the ONE persistent `onClick` listener every `GameCard`'s Button
+  carries, and it dereferences an `[Inject] AudioSystem`. Reflex populates `[Inject]` for objects
+  present at SCENE LOAD; `EnsureGridCapacity` creates its row at RUNTIME, so nothing injected it
+  and that field was null on all four of its cards. **`UnityEvent.Invoke` runs PERSISTENT
+  listeners BEFORE runtime ones** (`InvokableCallList.PrepareInvoke` builds
+  `m_ExecutingCalls = m_PersistentCalls + m_RuntimeCalls`) and guards neither, so the
+  `NullReferenceException` from `PlayAudio` aborted the invoke list before reaching the runtime
+  listener this view attaches — `() => SelectGame(game)`. The card rendered with the right title
+  and art, reported `interactable = true`, passed an `EventSystem` raycast, played no sound, and
+  opened nothing.
+
+  That is exactly why it read as POSITIONAL: only cards in a CLONED row are un-injected, and at
+  13 modes only the first of them is ever active. At 14 modes the second would have died too.
+  Favouriting "fixed" a mode by moving it onto one of the twelve AUTHORED cards, which the scene's
+  `ContainerScope` had injected at load.
+
+  The fix is in two layers, and the second is the one that generalises past this grid.
+  **(1) Inject at the creating site.** `EnsureGridCapacity` now holds `[Inject] Container
+  _container` and calls `GameObjectInjector.InjectRecursive(row.gameObject, _container)` on every
+  row it clones — which covers not just `MenuAudio` but any `[Inject]` field any future card
+  component grows. `ProjectilePoolManager` already carries the identical fix for the identical
+  reason (*"an un-injected projectile NREs on its null AudioSystem in LaunchProjectile and every
+  shot from that instance is a dud"*), so this is the second outing of one bug class, not a new
+  one. **(2) Fail safe on the persistent listener.** Layer 1 has to be remembered once per spawn
+  site, forever; layer 2 holds everywhere at once. `MenuAudio` now falls back to
+  `AudioSystem.Instance` and warns ONCE per component, so the next runtime-created UI object that
+  nobody injects loses a SOUND rather than a BUTTON. `MenuAudioResilienceTests` pins it.
+
+  **Four investigations missed it because every one of them was looking at authored state.** The
+  scene YAML is byte-identical for all twelve cards; the clone is a faithful copy of one of them;
+  the difference exists only at runtime and only in a field no serialized data mentions. For the
+  record, all of these were ruled out and none was the cause: nothing is drawn over the grid
+  (`GameSelectScrollView` is `Explore`'s last child, and the panel's later siblings
+  `ArcadeLobbyList` and `CloseButton` miss it — the scroll view is inset 600px from the panel's
+  left edge); the rows do not overlap in a way that matters (spacing −142.08 against 456-tall rows
+  holding 202.72-tall cards, so the CARDS never overlap, and the clone is a later sibling anyway,
+  so it raycasts first); `GameCard.SetLocked` is symmetric and `_originalBgColor` initialises to
+  `Color.white`; the `CallToActionIndicator` is a 96×96 corner badge with `RaycastTarget: 0`;
+  `MinigameLaunchPanel.Handles` accepts every non-Maelstrom card and is not a `HostModal`, so
+  `OpenFor` always calls `ModalWindowIn`; `ArcadeDPadNav` never writes `interactable`; and the
+  progression chain cannot single a slot out, since ten of the thirteen roster modes — Switchback
+  among them — are absent from `GameModeQuestList` entirely. **The lesson is the search, not the
+  list: when a card is authored identically to eleven working ones and still does nothing, stop
+  auditing the asset and ask what is only true of it at RUNTIME.**
 
   **The general rule for the next mode:** *adding a card is adding a ROW, and a row is only
   reachable if the scroll content was measured after it — never assume an authored content height
diff --git a/Assets/_Scripts/Tests/Editor/MenuAudioResilienceTests.cs b/Assets/_Scripts/Tests/Editor/MenuAudioResilienceTests.cs
```

</details>

### `3a1720d0e` — fix(ui): make a persistent listener unable to eat a press, and gate the class

_Claude, 2026-09-06 16:02:28 +0000_

```text
An eight-lens fan-out with three adversarial verifiers per candidate confirmed
695485d8's root cause at high confidence, re-deriving it across all twelve
authored cards rather than a sample, and turned up defects in that commit and
one thing it stated wrongly.

CORRECTED: this is the THIRD outing of the bug class, not the second.
ProfileIconSelectView.cs:167-170 already carries the identical InjectRecursive
call with a comment naming the identical failure ("Reflex doesn't auto-inject
Instantiate()'d prefabs - inject so the button's [Inject] AudioSystem resolves,
otherwise OnClick NREs on the audio call"), as does ProjectilePoolManager.
A comment written twice for other subsystems did not stop it a third time, so
the fix is no longer only code.

NEW GATE - Tools/Build/audit_persistent_listener_injection.py (--check).
Resolves every persistent UnityEvent listener in every scene and prefab to its
target's script and fails on any (class, method) whose class declares an
[Inject] field and is not reviewed. It RATCHETS: today's 28 pairs are frozen as
an explicitly UNREVIEWED baseline, so it passes now and fails on anything new;
only MenuAudio.PlayAudio is actually reviewed. It deliberately does NOT guess a
class from m_TargetAssemblyTypeName - Menu_Main serialises 190 stale
CosmicShore.App.* names for a namespace no first-party file declares, and Unity
resolves a persistent call from the LIVE target's type, so guessing invented
pairs like MenuAudio.SetActive, which is not a method on MenuAudio. Proven to
have teeth by a negative control: removing one baseline pair exits 1.

DEFECTS FIXED IN MY OWN PRIOR COMMITS:
- SelectGame logged "NOT WIRED - nothing can open" and then dereferenced the
  null field on the next statement, so it stated a diagnosis and destroyed it.
  Early return.
- ReportCardPressability's success branch was UNREACHABLE. Every GameCard's
  root Image is authored m_RaycastTarget: 0, so the raycast always lands on a
  descendant and `results[0].gameObject == last.gameObject` can never be true -
  the instrument always took a fault-shaped branch and MANUFACTURED the
  overlay/clipping verdicts that three investigations then chased. Now compares
  with IsChildOf, and says so when the card is merely scrolled out of view.

SAME CLASS, ONE COMPONENT OVER: GameCard.ToggleFavorite and OnCardClicked
dereferenced AudioSystem.Instance unguarded, and ToggleFavorite is persistent
call [0] on every card's favourite star, ahead of FavoriteSystem.ToggleFavorite
and the repopulate. A null there would have eaten the favourite itself - on the
one control that was the human's workaround.

ModalWindowManager.ModalWindowIn was the codebase's only silent, log-free modal
refusal; on screen it is indistinguishable from the dead button just fixed, so
it now warns and names the leak (ModePreviewSession clears sendNavigationEvents
and restores it only on focus release).

Verified: the injection call shape REAL-COMPILED through Roslyn against Reflex
stubs (csc exit 0, zero errors, assembly emitted) - the previous commit only
parse-checked it, and Reflex has no source on disk, so nothing had actually
compiled that call; a negative control swapping the argument order fails with
CS1503 on both arguments. Every changed file parse-checked with zero CS1xxx.
check_conditional_compilation.py OK (1888 files), author_switchback_assets.py
--check OK (22 files), audit_persistent_listener_injection.py --check OK.

Still unverified in the editor, and that is the one thing that can close it.
```

```text
 Assets/_Scripts/Controller/Arcade/SWITCHBACK.md    |  15 +++-
 Assets/_Scripts/UI/Elements/GameCard.cs            |  14 ++-
 Assets/_Scripts/UI/Modals/ModalWindowManager.cs    |  17 ++++
 Assets/_Scripts/UI/Views/ArcadeExploreView.cs      |  27 +++++-
 CLAUDE.md                                          |   2 +-
 Tools/Build/audit_persistent_listener_injection.py | 244 +++++++++++++++++++++++++++++++++++++++++++++++++++
 6 files changed, 309 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 406 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
index d8fe708fe..3b2db5fea 100644
--- a/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
+++ b/Assets/_Scripts/Controller/Arcade/SWITCHBACK.md
@@ -500,10 +500,17 @@ over 400 seeds × 4 intensities (all contracts hold); nothing below has been run
   **(1) Inject at the creating site.** `EnsureGridCapacity` now holds `[Inject] Container
   _container` and calls `GameObjectInjector.InjectRecursive(row.gameObject, _container)` on every
   row it clones — which covers not just `MenuAudio` but any `[Inject]` field any future card
-  component grows. `ProjectilePoolManager` already carries the identical fix for the identical
-  reason (*"an un-injected projectile NREs on its null AudioSystem in LaunchProjectile and every
-  shot from that instance is a dud"*), so this is the second outing of one bug class, not a new
-  one. **(2) Fail safe on the persistent listener.** Layer 1 has to be remembered once per spawn
+  component grows. This was the **third** outing of one bug class, and the first two had
+  already written the fix down: `ProfileIconSelectView.cs:167-170` and
+  `ProjectilePoolManager.cs:27-33` each carry the identical call with a comment naming the
+  identical failure (*"an un-injected projectile NREs on its null AudioSystem in LaunchProjectile
+  and every shot from that instance is a dud"*). **A comment written twice for other subsystems
+  did not stop it a third time**, which is why the fix is not only two code changes but a gate:
+  `Tools/Build/audit_persistent_listener_injection.py --check` resolves every persistent
+  `UnityEvent` listener in every scene and prefab to its target's script and fails on any
+  `(class, method)` whose class declares an `[Inject]` field and is not reviewed. It RATCHETS —
+  today's 28 pairs are frozen as an explicitly UNREVIEWED baseline, so it passes now and fails on
+  anything new. **(2) Fail safe on the persistent listener.** Layer 1 has to be remembered once per spawn
   site, forever; layer 2 holds everywhere at once. `MenuAudio` now falls back to
   `AudioSystem.Instance` and warns ONCE per component, so the next runtime-created UI object that
   nobody injects loses a SOUND rather than a BUTTON. `MenuAudioResilienceTests` pins it.
diff --git a/Assets/_Scripts/UI/Elements/GameCard.cs b/Assets/_Scripts/UI/Elements/GameCard.cs
index 7bb568602..e39787e7c 100644
--- a/Assets/_Scripts/UI/Elements/GameCard.cs
+++ b/Assets/_Scripts/UI/Elements/GameCard.cs
@@ -240,14 +240,24 @@ namespace CosmicShore.UI
         {
             Favorited = !Favorited;
             StarImage.sprite = Favorited ? StarIconActive : StarIconInActive;
-            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
+
+            // The audio is a FLOURISH and the two lines under it are the ACTION, so the audio must
+            // not be able to stop them. This is persistent call [0] on every card's favourite star,
+            // ahead of the favourite itself - an unguarded deref here eats the toggle, on the one
+            // control that was the workaround for the dead-card bug this file's row-clone sibling
+            // just fixed. Same class, one component over.
+            if (AudioSystem.Instance)
+                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
+
             FavoriteSystem.ToggleFavorite(gameMode);
             ExploreView.PopulateGameSelectionList();
         }
 
         public void OnCardClicked()
         {
-            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
+            if (AudioSystem.Instance)
+                AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
+
             CSDebug.Log($"GameCard - Clicked: Gamemode: {gameMode}");
 
             SO_ArcadeGame game = AllGames.Games.Where(x => x.Mode == gameMode).FirstOrDefault();
diff --git a/Assets/_Scripts/UI/Modals/ModalWindowManager.cs b/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
index 680ded39b..4b905d98b 100644
--- a/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
+++ b/Assets/_Scripts/UI/Modals/ModalWindowManager.cs
@@ -1,6 +1,7 @@
 using System.Collections;
 using CosmicShore.Core;
 using Reflex.Attributes;
+using CosmicShore.Utility;
 using UnityEngine;
 using UnityEngine.EventSystems;
 using UnityEngine.InputSystem;
@@ -167,7 +168,23 @@ namespace CosmicShore.UI
             // appshell UI is hidden and non-raycastable in freestyle, so there is no
             // legitimate open; the freestyle pause menu is not a ModalWindowManager.
             if (!isOn && EventSystem.current && !EventSystem.current.sendNavigationEvents)
+            {
+                // Say so. This was the codebase's only silent, log-free modal refusal, and on
+                // screen it is indistinguishable from a dead button - a press arrives, is
+                // accepted, and nothing opens. That is exactly the symptom the arcade's 13th
+                // card produced for five rounds from a completely different cause, so the two
+                // must not look alike in a console. Note the flag can also be LEAKED rather than
+                // legitimately held: ModePreviewSession clears sendNavigationEvents when a
```

</details>

### `7017c745c` — fix(tools): OnScreenUIReport was missing using CosmicShore.Editor.Froglet

_Claude, 2026-09-06 16:43:50 +0000_

```text
[FrogletTool] lives in CosmicShore.Editor.Froglet, not CosmicShore.Editor, so the
attribute, its category enum and its named properties were all unresolvable.

Second time this exact class of error has reached the editor from me in this
session. My compile check is reference-less, so every first-party type is already
unresolvable in it and a genuinely missing using is indistinguishable from the
noise; the complementary namespace audit DOES catch it, and I ran it on the four
gameplay files I touched and then not on the one new file I added. The check is
only worth having if it runs on everything new, so: run it on every added file,
not just the ones that felt risky.
```

```text
 Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs b/Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs
index 7119a69f9..f62adae2b 100644
--- a/Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs
+++ b/Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs
@@ -2,6 +2,7 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Text;
+using CosmicShore.Editor.Froglet;
 using UnityEditor;
 using UnityEngine;
 using UnityEngine.UI;
```

</details>

### `c65fa7ec1` — fix(sparrow): repair the enum rename I missed, and gate the class of miss

_Claude, 2026-09-06 16:50:29 +0000_

```text
`CombatHitClass.Missile` became `MissileDirect`, and one reference survived:
the explosion effect's serialized default. Fixed, along with a stale `<see
cref>` naming the same retired member.

**The gate that should have caught it cannot, and that is the more useful
half.** The out-of-editor check is a `dotnet build` over the changed C# with
no Unity assemblies, and for any file whose base type lives in the
Assembly-CSharp monolith - every MonoBehaviour and every ScriptableObject
here - Roslyn abandons class-body binding, so a wrong member inside a field
initializer is reported as nothing at all. It came back clean over the exact
file that carried the bug. I reported that as verification; it verified
syntax and nothing else. Stubbing Unity's attributes does not help (measured
- only the unresolved BASE type matters) and stubbing the monolith is not
maintainable, so the answer is not a better compiler invocation.

`Tools/Build/check_enum_member_references.py` checks renames TEXTUALLY
instead: every `EnumName.Member` in _Scripts against the members that enum
declares. ~3 s, no Unity, 174 enums over 1,805 files.

Scoped deliberately, because a noisy gate is a gate that gets switched off -
each of these was a real false positive before it was excluded:

- unqualified references only. `Slider.Direction.LeftToRight` (a nested type
  on another type) and `line.Direction.magnitude` (a property sharing a name
  with one of our enums) are ordinary code.
- `System.Enum`'s own members pass (`ToString`, `IsDefined`, `HasFlag`, ...).
- camelCase members are instance members, not enum members.
- two same-named enums are skipped rather than guessed at.
- comments and string literals are stripped, so a doc reference to a retired
  name is not a build failure.

One parsing trap worth naming: a per-line member parse silently captures only
the first member of a single-line `enum Phase { A, B, C }`, which then reports
every other member of that enum as missing. That false-positive flood matters
more than the true positives, so members are split on commas.

`--self-test` is the gate's own negative control: it reproduces the escaped
bug plus the five shapes that must NOT fire. A gate nobody has watched fail is
a gate nobody should trust.

General rule, recorded in CLAUDE.md: a check that cannot resolve a type cannot
see errors ABOUT that type, so what it proves shrinks silently as the code
under it gets more Unity-shaped. Say what a gate actually covered, never what
it is named after.

Verification status: enum gate clean and its 8 self-test checks pass; the
negative control was run against the reintroduced bug and named it by file and
line. Conditional-compilation gate clean. The 30 out-of-editor assertions (16
tier + 14 fuze) still pass. Syntax gate over the changed C# shows only
unresolved-Unity-type errors - which is now documented as all it can show.
```

```text
 .../Vessel Explosion Effects/VesselCombatHitByExplosionEffectSO.cs    |  10 +-
 .../Vessel Projectile Effects/VesselCombatHitByProjectileEffectSO.cs  |   2 +-
 CLAUDE.md                                                             |   1 +
 Tools/Build/check_enum_member_references.py                           | 267 ++++++++++++++++++++++++++++++++
 4 files changed, 276 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 285 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 223f27902..d1a57c047 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -3302,6 +3302,7 @@ All game code lives under `CosmicShore.*` with 8 primary namespaces:
 - **A sound that isn't an inspector-exposed `EventReference` on the thing that makes it** — a hardcoded FMOD event path in code, a "temp" event plugged in so something is audible, a new ability routed through an existing `GameplaySFXCategory` because it's close enough, or a new gameplay sound built on `AudioClip` + `AudioSource`. Every noise must be findable and swappable in the component view of its own prefab, and every ship ability needs its own dedicated event field. Guard an empty reference for **silence**, never for substitution. See "Audio (FMOD)" under Architecture Patterns
 - `RuntimeManager.PlayOneShot` / `PlayOneShotAttached` directly — they take no per-instance volume, so the sound ignores the in-game SFX slider whenever the FMOD bus fails to resolve. Go through `AudioSystem.PlaySFXEvent` / `PlaySFXEventAttached` or `FMODOneShotVolumeHelper`
 - **Guarding `using UnityEngine;` (or any using an unguarded declaration needs) behind `#if UNITY_EDITOR` / `#if DEVELOPMENT_BUILD`.** A guard must cover a self-consistent unit: if the class declaration is outside the guard, everything it depends on must be too. `#if UNITY_EDITOR\nusing UnityEngine;\n#endif` above an unguarded `class Foo : MonoBehaviour` compiles fine in the Editor and in Development builds, then fails the **Release** player build with `CS0246: 'MonoBehaviour' could not be found` — which is the automated build, not yours. Likewise, never touch the `UnityEditor` namespace outside `#if UNITY_EDITOR` in a file that isn't under an `Editor/` folder. Run `python3 Tools/Build/check_conditional_compilation.py` (~1s, no Unity needed) before committing any guarded script. Full rules + the two safe patterns: `Docs/CONDITIONAL_COMPILATION.md`
+- **Renaming or removing an ENUM MEMBER and trusting a compile-free check to find the stragglers.** A `dotnet build` over the changed C# with no Unity assemblies — the standing out-of-editor gate — is a **SYNTAX gate and nothing more** for any file whose base type lives in the `Assembly-CSharp` monolith, which is every `MonoBehaviour` and every `ScriptableObject` in this project. Roslyn abandons class-body binding when the base type is unresolved, so a renamed member inside a serialized field's DEFAULT is reported as *nothing*: `[SerializeField] CombatHitClass hitClass = CombatHitClass.Missile;` came back clean over the very file that carried it, and the Editor found it on the next compile. **Stubbing Unity's attributes does not fix it** (measured — only the unresolved BASE type matters), and stubbing the monolith is not maintainable. So a rename is checked TEXTUALLY: run `python3 Tools/Build/check_enum_member_references.py` (~3 s, no Unity), which verifies every `EnumName.Member` reference in `_Scripts` against the members that enum actually declares. It is scoped on purpose — unqualified references only (`Slider.Direction.LeftToRight` and `line.Direction.magnitude` are ordinary code, not defects), `System.Enum`'s own members allowed, two same-named enums skipped rather than guessed — and `--self-test` is its own negative control (it reproduces the escaped bug and the four shapes that must NOT fire), because a gate nobody has watched FAIL is a gate nobody should trust. **General rule: a check that cannot resolve a type cannot see errors ABOUT that type, so what it proves shrinks silently as the code under it gets more Unity-shaped** — say what a gate actually covered, never what it is named after.
 - A per-vessel component that drives the gameplay camera's FOV or the Panini override — the speed tunnel is a PLATFORM LAW driven by the single static `VesselSpeedTunnel` (`Docs/SPEED_TUNNEL.md`). `PostProcessingManager.SetSpeedTunnelPanini` is one global override with no ref-counting, so a second writer silently stomps the first and an outgoing vessel's teardown releases the incoming vessel's effect mid-swap. Bind platform-wide vessel behaviour in `VesselController.Initialize` under `IsLocalPilot`, never on a prefab
 - **Clearing a vessel renderer's MaterialPropertyBlock (`SetPropertyBlock(null, index)`) to undo your own override.** A vessel's renderers are written by several systems at once — the vessel vision band's domain tint, the Echo Sight hull highlight, the Serpent's cloak alpha, the Rhino's sword FX — and they compose correctly *only* because each does a get-modify-set round trip, which preserves foreign properties. A null clear is the one operation that does not: it wipes every other system's channel, and the symptom is a permanently unmarkable ship with nothing in the console. **When several systems write one channel, each restores what IT changed; only an owner may clear** — the same rule the speed tunnel records for the un-ref-counted Panini override. (`Docs/VESSEL_VISION.md §3.1`; `EchoSightVesselHighlighter.Restore` is the worked example, and `VesselVisionShading` heals its own stamp round-robin because relying on every future writer to remember is not a law.)
 - **Writing the RENDERER-WIDE MaterialPropertyBlock on a surface whose other writers use PER-MATERIAL-INDEX blocks.** The get-modify-set law above is necessary and NOT sufficient: Unity gives a per-index block **precedence over — not merged with —** the renderer-wide block, so a perfectly-formed renderer-wide write lands on an object that never reads it. Every vessel-renderer writer in the fleet is per-index (`VesselVisionShading` stamps EVERY submesh of EVERY vessel renderer; `EchoSightVesselHighlighter` matches it), so on a vessel the renderer-wide block is dead surface — the Scarab's whole hull-flare feature shipped that way and reached the screen exactly never, with no error anywhere. **Match the INDEXING of the surface's existing writers, not just the round-trip.** Note the allocation trap that comes with it: the `sharedMaterials` getter mints a managed `Material[]` per access, so a per-index loop must read the material COUNT from a cache (or `GetSharedMaterials(reusedList)`), never re-read the array per frame — "one reference compare per frame" hid one array allocation per frame per live vessel in `ScarabHullBuilder`. And a cached last-written-value early-out is not composable with a sibling that restores the same property to the MATERIAL's rest: it will never re-assert, so the channel stays dead until something else changes it — carry a re-assert window (`ScarabAnimation.ApplyFlare`, the vision band's round-robin heal at fleet scale).
diff --git a/Tools/Build/check_enum_member_references.py b/Tools/Build/check_enum_member_references.py
new file mode 100644
index 000000000..be30a52b8
--- /dev/null
+++ b/Tools/Build/check_enum_member_references.py
@@ -0,0 +1,267 @@
+#!/usr/bin/env python3
+"""Verify that every `EnumName.Member` reference in first-party C# names a member the
+enum actually declares.
+
+WHY THIS EXISTS, and why the compiler is not enough here.
+
+The out-of-editor syntax gate (a `dotnet build` over the changed files with no Unity
+assemblies) can only report errors Roslyn is still willing to bind. A file whose BASE
+TYPE lives elsewhere in the `Assembly-CSharp` monolith — every MonoBehaviour and every
+ScriptableObject in this project — fails to bind its class body at all, so a wrong enum
+member inside a serialized field's DEFAULT is reported as nothing:
+
+    [SerializeField] CombatHitClass hitClass = CombatHitClass.Missile;   // renamed away
+
+That shipped once (2026-09, `CombatHitClass.Missile` -> `MissileDirect`): the gate came
+back clean over the very file that carried it, and the Editor found it on the next
+compile. Stubbing Unity's attributes does not fix it — measured; only the unresolved
+base type matters — and stubbing the monolith is not a thing anyone will maintain.
+
+So renames are checked TEXTUALLY, which needs no compiler and cannot be degraded by a
+missing type. Cheap enough to run on every commit (~0.3 s over ~1,900 files).
+
+Limits, stated rather than implied: this understands `EnumName.Member`. It does not see
+a member reached through a `using static`, an alias, or a variable of the enum's type,
+and it does not type-check anything else. It is a targeted guard for the one failure
+mode above, not a substitute for compiling.
+
+    python3 Tools/Build/check_enum_member_references.py
+"""
+
+import os
+import re
+import sys
+
+# Tools/Build/<this file> -> Tools/Build -> Tools -> the repository root.
+ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
+SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")
+
+# `public enum Name` / `public enum Name : byte`, then members up to the closing brace.
+ENUM_RE = re.compile(
+    r"\benum\s+(\w+)\s*(?::\s*\w+\s*)?\{(.*?)\}", re.S)
+# Members every enum has from System.Enum / System.Object. A reference to one of these
+# is correct code, not a stale member name.
+INHERITED_MEMBERS = {
+    "ToString", "Equals", "GetHashCode", "GetType", "CompareTo", "HasFlag",
+    "Parse", "TryParse", "GetName", "GetNames", "GetValues", "IsDefined",
+    "Format", "GetUnderlyingType", "ToObject",
+}
+
+ATTR_RE = re.compile(r"\[[^\]]*\]")
+LEADING_IDENT_RE = re.compile(r"^\s*([A-Za-z_]\w*)")
+
+
+def enum_members(body: str) -> "set[str]":
+    """Members of one enum body.
+
+    Split on COMMAS, not on lines: a single-line `enum Phase { A, B, C }` is common in
+    this codebase, and a per-line parse silently captures only the first member — which
+    then reports every other member of that enum as missing. That false-positive flood
+    is what makes a gate get switched off, so it matters more than the true positives.
+    """
+    out = set()
```

</details>

### `892a7c099` — test(sparrow): enumerate CombatHitClass instead of naming the members I knew about

_Claude, 2026-09-07 15:49:42 +0000_

```text
Applies the rule bleeding-edge added to the ship skill in 7dcc44b4, to the branch
that provoked it: this branch adds two members to CombatHitClass, and both of the
tests guarding the missile-tier law listed the members by hand.

Proven, not assumed. A hypothetical sixth class that quietly ranks as a missile
tier - the real regression, since a ranked class joins the missile latch entry and
can suppress a genuine tier - fails the enumerated guard with two assertions and
PASSES the old hand-named one. Measured by compiling the real CombatHitClass.cs
against both forms.

The three tiers stay named where naming them IS the law (which classes are radii of
one rocket, and their order); the "everything else" half now derives from
Enum.GetValues. Dog Fight's price list gets the same treatment: the four priced
classes are named, and every other member of the enum must be worth exactly 0.
```

```text
 Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs | 67 ++++++++++++++++++++++++++++++++++++++----------
 1 file changed, 53 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 97 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs b/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
index 291600157..6f59d98e2 100644
--- a/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
+++ b/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
@@ -136,6 +136,16 @@ namespace CosmicShore.Tests
 
         #region The price list
 
+        // The four classes Dog Fight has an opinion about. Everything else in the enum must be
+        // worth 0 - asserted by enumeration below, not by naming the leftovers.
+        static readonly CombatHitClass[] PricedClasses =
+        {
+            CombatHitClass.Bullet,
+            CombatHitClass.MissileShockwave,
+            CombatHitClass.MissileBlast,
+            CombatHitClass.MissileDirect,
+        };
+
         [Test]
         public void DogFightPricesTheTiersByProximity()
         {
@@ -146,24 +156,44 @@ namespace CosmicShore.Tests
             Assert.AreEqual(30f, Field(rule, "missileDirectPoints"));
         }
 
+        // The three missile classes a single rocket can land. Named here because "which classes
+        // are tiers of ONE event" is the law itself, not a case list - everything else is
+        // derived from the enum below so a member added later is covered on the day it lands.
+        static readonly CombatHitClass[] MissileTiersClosestLast =
+        {
+            CombatHitClass.MissileShockwave,
+            CombatHitClass.MissileBlast,
+            CombatHitClass.MissileDirect,
+        };
+
+        static CombatHitClass[] AllClasses() =>
+            (CombatHitClass[])Enum.GetValues(typeof(CombatHitClass));
+
         // The ranks are what let the latch upgrade a claim, and the ordering is the whole reason
         // there are three classes rather than one.
+        //
+        // The "everything else" half ENUMERATES the enum rather than naming Bullet and Debuff,
+        // per the rule bleeding-edge records in the ship skill: a guard that lists the members it
+        // knows about stops guarding the law the day a member is added, and nothing fails to say
+        // so. A sixth class that quietly ranked non-zero would join the missile latch entry and
+        // suppress a real rocket tier; this test now fails the moment it does.
         [Test]
         public void ProximityRankOrdersTheTiersAndIgnoresEverythingElse()
         {
-            Assert.Less(CombatHitClasses.MissileProximityRank(CombatHitClass.MissileShockwave),
-                        CombatHitClasses.MissileProximityRank(CombatHitClass.MissileBlast));
-            Assert.Less(CombatHitClasses.MissileProximityRank(CombatHitClass.MissileBlast),
-                        CombatHitClasses.MissileProximityRank(CombatHitClass.MissileDirect));
-
-            Assert.AreEqual(0, CombatHitClasses.MissileProximityRank(CombatHitClass.Bullet));
-            Assert.AreEqual(0, CombatHitClasses.MissileProximityRank(CombatHitClass.Debuff));
-
-            Assert.IsFalse(CombatHitClasses.IsMissile(CombatHitClass.Bullet));
-            Assert.IsFalse(CombatHitClasses.IsMissile(CombatHitClass.Debuff));
-            Assert.IsTrue(CombatHitClasses.IsMissile(CombatHitClass.MissileShockwave));
-            Assert.IsTrue(CombatHitClasses.IsMissile(CombatHitClass.MissileBlast));
-            Assert.IsTrue(CombatHitClasses.IsMissile(CombatHitClass.MissileDirect));
+            for (int i = 1; i < MissileTiersClosestLast.Length; i++)
+                Assert.Less(CombatHitClasses.MissileProximityRank(MissileTiersClosestLast[i - 1]),
+                            CombatHitClasses.MissileProximityRank(MissileTiersClosestLast[i]),
+                            $"{MissileTiersClosestLast[i - 1]} must rank further out than " +
+                            $"{MissileTiersClosestLast[i]}");
+
+            foreach (var hitClass in AllClasses())
+            {
+                bool isTier = Array.IndexOf(MissileTiersClosestLast, hitClass) >= 0;
+                Assert.AreEqual(isTier, CombatHitClasses.MissileProximityRank(hitClass) > 0,
+                                $"{hitClass} ranks as a missile tier: {!isTier}");
+                Assert.AreEqual(isTier, CombatHitClasses.IsMissile(hitClass),
+                                $"{hitClass} reports IsMissile: {!isTier}");
+            }
         }
 
         // NEGATIVE CONTROL for the trap CLAUDE.md records twice: a rule written as
@@ -177,7 +207,16 @@ namespace CosmicShore.Tests
```

</details>

### `7be1c01a8` — fix(projectile): depth-rent the vessel sweep's buffers like the prism sweep's

_Claude, 2026-09-07 15:53:58 +0000_

```text
Review catch. SweepVesselsAlong used one shared static list and one shared collider
buffer, and its comment argued that was safe because no vessel effect fires another
projectile mid-sweep. That is true - but only because ProjectileChainFire is authored
as a projectile-PRISM effect, so ProjectileImpactor's ship arm cannot reach it. The
guard is which container someone dropped an asset into, not anything in this file,
and the Urchin's [Embed, Steal, ChainFire] ordering shows a chain effect sitting in a
container is an ordinary thing to author.

The prism sweep a hundred lines up carries the same assertion in its own comment with
"and the Urchin disproved it" attached. Rather than repeat the instance, take the
class: rent both buffers by depth, exactly as RentSweepBuffers does. OverlapCapsuleGrowing
can replace the array it is handed, so the grown one is written back to its pool slot.

Measured with a standalone transcription of both disciplines under the exact reentrancy
(dispatch at index 0 starts a child sweep): the shared buffer drops the parent's
remaining two contacts and re-dispatches two of the child's; the rented buffer
dispatches all three parent contacts in order.
```

```text
 Assets/_Scripts/Controller/Projectiles/Projectile.cs | 112 ++++++++++++++++++++++++++++++-------------------
 1 file changed, 68 insertions(+), 44 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 138 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Projectiles/Projectile.cs b/Assets/_Scripts/Controller/Projectiles/Projectile.cs
index 1c6df3cbb..9978f39de 100644
--- a/Assets/_Scripts/Controller/Projectiles/Projectile.cs
+++ b/Assets/_Scripts/Controller/Projectiles/Projectile.cs
@@ -1641,12 +1641,28 @@ namespace CosmicShore.Gameplay
         }
 
         // Bounded scratch for the vessel sweep. A hull is several colliders and an arena holds
-        // at most a handful of vessels, so this is generous; it is also NOT depth-rented like
-        // the prism buffers, because a vessel impact cannot re-enter this method (no vessel
-        // effect fires another projectile mid-sweep the way a chain-firing spike does).
-        // Grown on saturation by the same rule the fuze uses - see OverlapCapsuleGrowing.
-        static Collider[] s_vesselSweepHits = new Collider[32];
-        static readonly List<SweepHit> s_vesselSweepOrdered = new();
+        // at most a handful of vessels, so 32 is generous; grown on saturation by the same rule
+        // the fuze uses - see OverlapCapsuleGrowing.
+        //
+        // DEPTH-RENTED like the prism buffers, even though no vessel effect fires another
+        // projectile mid-sweep today. That is true only because ProjectileChainFire is authored
+        // as a projectile-PRISM effect, so the ship arm cannot reach it - an absence guarded by
+        // which container someone dropped an asset into, not by anything in this file. The prism
+        // sweep asserted the same non-reentrancy in a comment and the Urchin disproved it; the
+        // rent costs six lines and removes the class rather than the instance.
+        static readonly List<Collider[]> s_vesselHitPool = new();
+        static readonly List<List<SweepHit>> s_vesselOrderedPool = new();
+        static int s_vesselSweepDepth;
+
+        static List<SweepHit> RentVesselBuffers(int depth)
+        {
+            while (s_vesselHitPool.Count <= depth)
+            {
+                s_vesselHitPool.Add(new Collider[32]);
+                s_vesselOrderedPool.Add(new List<SweepHit>(8));
+            }
+            return s_vesselOrderedPool[depth];
+        }
 
         // Ships, resolved once through the fuze's own cache: both readers want the same layer,
         // and a second copy is a second thing to keep in step.
@@ -1694,52 +1710,60 @@ namespace CosmicShore.Gameplay
             int vesselMask = ResolveLayerMaskOnce(ref s_sweepVesselMask, "Ships");
             if (vesselMask <= 0) return;
 
-            int found = OverlapCapsuleGrowing(from, to, _sweepRadius, vesselMask,
-                                              ref s_vesselSweepHits);
-            if (found == 0) return;
+            int depth = s_vesselSweepDepth++;
+            try
+            {
+                var ordered = RentVesselBuffers(depth);
+                var buffer = s_vesselHitPool[depth];
 
-            Vector3 ab = to - from;
-            float abLenSq = ab.sqrMagnitude;
+                int found = OverlapCapsuleGrowing(from, to, _sweepRadius, vesselMask, ref buffer);
+                s_vesselHitPool[depth] = buffer;   // OverlapCapsuleGrowing may have replaced it
+                if (found == 0) return;
 
-            s_vesselSweepOrdered.Clear();
-            for (int i = 0; i < found; i++)
-            {
-                var col = s_vesselSweepHits[i];
-                if (!col) continue;
-                if (!col.TryGetComponent(out ImpactCollider impactCollider)) continue;
-                if (impactCollider.Impactor is not VesselImpactor vesselImpactor) continue;
-
-                // One entry per HULL, not per collider.
-                bool already = false;
-                for (int j = 0; j < s_vesselSweepOrdered.Count && !already; j++)
-                    already = ReferenceEquals(s_vesselSweepOrdered[j].Impactor, vesselImpactor);
-                if (already) continue;
-
-                Vector3 centre = col.bounds.center;
-                float t = abLenSq > 1e-8f
-                    ? Mathf.Clamp01(Vector3.Dot(centre - from, ab) / abLenSq)
-                    : 0f;
+                Vector3 ab = to - from;
+                float abLenSq = ab.sqrMagnitude;
 
-                s_vesselSweepOrdered.Add(new SweepHit(t, vesselImpactor));
```

</details>

### `a099a9cc8` — test(sparrow): run the tier scoring through the latch, not around it

_Claude, 2026-09-07 15:56:15 +0000_

```text
Review catch on my own tests. Every upgrade-arithmetic test handed supersededRank to
CombatHitScoring.Credit by hand - so they proved the arithmetic and never touched the
thing that has to PRODUCE that rank. The value under test was supplied by the test.

That matters because the whole ranked-not-additive design lives in one line of
VesselCombatHitLatch.Key's constructor: the three missile classes canonicalise to one
key so they contend for a single window. Key them apart and a centre-punch pays 60 -
and not one existing assertion would have moved.

Four tests now run the full path an effect asset takes (TryAdmit, then Credit with
whatever it reported): the centre-punch pays 30 and counts one rocket, a shockwave
after a direct hit is refused rather than paid or refunded, a repeated tier pays once,
and bullets and debuffs keep their own keys.

Verified out of editor first, by compiling the real VesselCombatHitLatch.cs and
CombatHitScoring.cs against the shipped Dog Fight prices and the 0.5s cooldown the
three tier assets share: all seven cases pass, including a second rocket after the
window paying again (the one case an edit-mode test cannot express, since Time.time
does not advance inside a test).
```

```text
 Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs | 96 ++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 96 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 107 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs b/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
index 6f59d98e2..d692bcc64 100644
--- a/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
+++ b/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
@@ -301,6 +301,102 @@ namespace CosmicShore.Tests
             finally { UnityEngine.Object.DestroyImmediate(rule); }
         }
 
+        // THE GAP THE TESTS ABOVE LEAVE: every one of them hands `supersededRank` to Credit by
+        // hand, so they prove the arithmetic and never touch the latch that has to PRODUCE that
+        // rank. The value under test was supplied by the test. These run the whole path an
+        // effect asset takes - TryAdmit first, then Credit with whatever it reported - so a
+        // latch that keyed the three tiers apart (and therefore paid a centre-punch 60) would
+        // fail here even though the arithmetic tests would all still pass.
+        //
+        // The cooldown is the one authored identically on all three tier assets; the shared-key
+        // canonicalisation lives in VesselCombatHitLatch.Key's constructor.
+        const float TierCooldown = 0.5f;
+
+        static void Land(FakeRoundStats stats, CombatHitClass hitClass, DogFightScoringRuleSO rule)
+        {
+            if (!VesselCombatHitLatch.TryAdmit("shooter", "victim", hitClass, TierCooldown,
+                                               out int supersededRank))
+                return;
+            CombatHitScoring.Credit(stats, hitClass, rule, supersededRank);
+        }
+
+        [Test]
+        public void ThroughTheLatch_ACentrePunchPaysThirtyAndCountsOneRocket()
+        {
+            VesselCombatHitLatch.Clear();
+            var rule = ShippedRule();
+            var stats = new FakeRoundStats();
+            try
+            {
+                // Geometry order: the warhead is the largest radius AND the fastest to expand,
+                // so the cheapest tier is the one that lands first.
+                Land(stats, CombatHitClass.MissileShockwave, rule);
+                Land(stats, CombatHitClass.MissileBlast, rule);
+                Land(stats, CombatHitClass.MissileDirect, rule);
+
+                Assert.AreEqual(30, stats.CombatPoints,
+                                "the three tiers share one latch key and must not be additive");
+                Assert.AreEqual(1, stats.MissileHitsLanded);
+            }
+            finally { UnityEngine.Object.DestroyImmediate(rule); VesselCombatHitLatch.Clear(); }
+        }
+
+        [Test]
+        public void ThroughTheLatch_TheClaimIsNeverRevisedDown()
+        {
+            VesselCombatHitLatch.Clear();
+            var rule = ShippedRule();
+            var stats = new FakeRoundStats();
+            try
+            {
+                Land(stats, CombatHitClass.MissileDirect, rule);
+                Land(stats, CombatHitClass.MissileShockwave, rule);   // same rocket's outer edge
+
+                Assert.AreEqual(30, stats.CombatPoints,
+                                "a shockwave arriving after a direct hit is the same rocket's " +
+                                "outer edge and must be refused, not paid and not refunded");
+                Assert.AreEqual(1, stats.MissileHitsLanded);
+            }
+            finally { UnityEngine.Object.DestroyImmediate(rule); VesselCombatHitLatch.Clear(); }
+        }
+
+        [Test]
+        public void ThroughTheLatch_TheSameTierTwiceInOneWindowPaysOnce()
+        {
+            VesselCombatHitLatch.Clear();
+            var rule = ShippedRule();
+            var stats = new FakeRoundStats();
+            try
+            {
+                Land(stats, CombatHitClass.MissileShockwave, rule);
+                Land(stats, CombatHitClass.MissileShockwave, rule);
+
+                Assert.AreEqual(10, stats.CombatPoints);
+                Assert.AreEqual(1, stats.MissileHitsLanded);
```

</details>

### `2abd2afe3` — fix(tools): stop the spent scene one-shot from taking every Dog Fight check with it

_Claude, 2026-09-07 16:01:58 +0000_

```text
author_dogfight_assets.py --check is advertised at the top of the file for CI /
pre-commit use, and it has been validating NOTHING. Section 9 clones the Rampage scene
and asserts on a donor field block the Rampage rework deleted; every validation in the
file lives ~130 lines further down (errors = [] at 891), so the abort took all of it -
including the four checks this branch added for the missile tiers, which therefore never
ran once.

Confirmed pre-existing, not a regression: the same assertion fails at the merge base and
at bleeding-edge tip, and this branch touches neither the donor scene nor that block.

A spent one-shot must stand down, not abort. SCENE_STEP_LIVE tests for the donor block
and skips section 9 when it is gone, registering the SHIPPED scene (and its .meta, which
the minted-GUID sweep needs in `files` or it reports the generator's own committed scene
as a foreign collision) so the scene-dependent checks describe the artifact that actually
loads.

Proven, not assumed - the gate now fails on:
  price list out of proximity order (blast 35 above direct 30), and
  one tier emitted with a different sameVictimCooldownSeconds (0.2 vs 0.5).
Both were silently green before this change. --check exits 0 on the tree as it stands.

The header now also states what --check does NOT cover: it validates the recipe in
memory and never diffs against disk, so it cannot see an asset hand-edited away from
what the script would author.
```

```text
 Tools/Build/author_dogfight_assets.py | 245 +++++++++++++++++++++++++++++++++++++---------------------------
 1 file changed, 143 insertions(+), 102 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 266 lines)</summary>

```diff
diff --git a/Tools/Build/author_dogfight_assets.py b/Tools/Build/author_dogfight_assets.py
index 586efff6c..94df1e271 100644
--- a/Tools/Build/author_dogfight_assets.py
+++ b/Tools/Build/author_dogfight_assets.py
@@ -10,6 +10,18 @@ Run from the repo root:  python3 Tools/Build/author_dogfight_assets.py [--check]
 
 --check validates without writing (CI / pre-commit use).
 
+WHAT --check COVERS, precisely. It builds every asset in memory and validates THAT - the
+generator's own constants, the relationships between them, and the wiring it would emit. It
+does NOT diff against what is on disk, so it cannot see a hand-edit that has drifted an asset
+away from what this script would author; read a green --check as "the recipe is coherent", not
+as "the shipped assets match it".
+
+It also has to keep RUNNING to mean anything, and it once quietly stopped: section 9's
+one-shot scene clone asserts against a donor scene that has since been reworked, and every
+validation in this file lives after it, so the abort took the whole check with it while
+--check still looked like it was being run. A spent one-shot now stands down (SCENE_STEP_LIVE)
+instead of aborting.
+
 The arena geometry and the PhaseThresholds are IMPORTED from boneyard_budget.py - which
 mirrors SpawnableBoneyard.cs's loops exactly - so the wreck field and the phase ladder that
 sits above it can never drift apart behind a stale constant here.
@@ -725,111 +737,140 @@ for i in INTENSITIES:
 # so the asserts below fire on a donor that is simply a different scene now. That is the
 # expected end state of a migration generator, not a break to repair - if Dog Fight ever needs
 # re-authoring, re-point it at a current donor rather than trying to satisfy these asserts.
-scene = read("Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity")
-
-# 9a. turn monitor script swap (field set is identical - base TurnMonitor fields only)
-scene, n = re.subn(EXISTING["RampagePrismTurnMonitor"], G_SCRIPT["DogFightPointTurnMonitor"], scene)
-assert n == 1, f"turn monitor guid appeared {n} times"
-
-# 9b. controller script swap + its serialized field block
-scene, n = re.subn(EXISTING["RampageController"], G_SCRIPT["DogFightController"], scene)
-assert n == 1, f"controller guid appeared {n} times"
-
-OLD_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}
-  arenaCell: {{fileID: 1700000065}}
-  aiRetargetSeconds: 1.5
-"""
-NEW_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['DogFightScoringRule']}, type: 2}}
+# The donor check is a GUARD, not an assert, and that distinction is the whole point: the
+# asserts below abort the script, and every validation this file performs lives AFTER them
+# (errors = [] is ~130 lines further down). So the moment the donor moved on, `--check` -
+# advertised at the top of this file for CI / pre-commit use - stopped validating anything
+# at all, silently, while still being run and still exiting 0-on-write-mode-abort. A spent
+# one-shot must STAND DOWN, not take the checks with it.
+DONOR = "Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity"
+_donor_scene = read(DONOR)
+_donor_fields = f"""  rule: {{fileID: 11400000, guid: {EXISTING['RampageScoringRule']}, type: 2}}
   arenaCell: {{fileID: 1700000065}}
-  firstMilestoneFraction: 0.25
-  secondMilestoneFraction: 0.5
-  progressSampleSeconds: 0.5
-  elementalCrystalCount: 14
-  crystalScatterRadius: 400
-  crystalScatterSeed: 41
   aiRetargetSeconds: 1.5
-  aiLeadSeconds: 0.6
-  aiBreakOffDistance: 120
-  aiExtendDistanceMultiplier: 3
-  aiMaxExtendSeconds: 4
 """
-assert OLD_FIELDS in scene, "controller field block not found in donor scene"
-scene = scene.replace(OLD_FIELDS, NEW_FIELDS)
-
-# 9c. Cell: swap the donor's single config for the FOUR per-intensity configs and flip the
-# choice mode to IntensityWise - the platform's own way to vary a cell by intensity.
-OLD_CELL = f"""  CellConfigs:
-  - {{fileID: 11400000, guid: {EXISTING['RampageCellConfig']}, type: 2}}
-  cellTypeChoiceOptions: 0
-"""
-NEW_CELL = "  CellConfigs:\n" + "".join(
-    f"  - {{fileID: 11400000, guid: {G_ASSET[f'BoneyardCellConfig{i}']}, type: 2}}\n"
-    for i in INTENSITIES) + "  cellTypeChoiceOptions: 1\n"
-assert OLD_CELL in scene, "donor Cell config block not found"
-scene = scene.replace(OLD_CELL, NEW_CELL)
-
-# 9d. Spawn on a SPHERE outside the wreck field. The donor's four authored transforms sit at
```

</details>

### `aa824f4bc` — docs(sparrow): repair the rename's stragglers and record what the review found

_Claude, 2026-09-07 16:05:30 +0000_

```text
Doc pass over what §2 turned up, plus two references the enum rename left behind
(the gate is scoped to _Scripts/**/*.cs on purpose - a doc reference is not a build
failure - so markdown needs its own sweep).

DOGFIGHT.md
  - the shared-code touchpoints table still described CombatHitClass as "(Bullet /
    Missile)". Now names all five, says the two added in 2026-09 took values 3 and 4
    so existing serialized hitClass: 0/1/2 keep their meaning, and gains rows for
    sweptVesselDetection, the ProjectileImpactor suppression, and the two new vessel
    components.
  - the latch section now states where the ranked-not-additive design actually lives:
    one line in Key's constructor canonicalising the three missile classes to one key.
    Key them apart and the tiers are additive again (60 for a centre-punch) through
    three individually-correct fresh windows, and the upgrade rule cannot compensate
    because a fresh key has nothing to supersede. With the note that the first round of
    tests could not have seen it.
  - known limitations: the generator's --check was validating nothing, with the two
    sibling generators still in that state named.

BENDS.md - its worked example quotes CombatHitClass.Missile. Preserved as it stood
(it is a snapshot of the bug) and labelled, with the follow-on: the enum grew twice
more and Enum.IsDefined absorbed both without anyone revisiting it, which is what the
fix was for.

CLAUDE.md
  - the vessel/prism sweeps rent their scratch by DEPTH, and why an absence guarded by
    which container an asset sits in is not guarded by the code.
  - two new gate traps: a whole-file #if UNITY_EDITOR makes an out-of-editor compile
    see nothing (78 of 111 test files carry one), and a spent one-shot assert above a
    generator's validation section silently disables every check below it.
```

```text
 Assets/_Scripts/Controller/Arcade/BENDS.md    |  9 +++++++++
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md | 37 ++++++++++++++++++++++++++++++++++++-
 CLAUDE.md                                     | 14 +++++++++++---
 3 files changed, 56 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 135 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/BENDS.md b/Assets/_Scripts/Controller/Arcade/BENDS.md
index 1dda7fe4e..90723d2b7 100644
--- a/Assets/_Scripts/Controller/Arcade/BENDS.md
+++ b/Assets/_Scripts/Controller/Arcade/BENDS.md
@@ -167,6 +167,7 @@ server-owned and its hits must still be recorded:
 `Player.ReportCombatHit_ServerRpc` re-validated the wire value like this:
 
 ```csharp
+// (as it stood; the member is now called MissileDirect — see below)
 var resolved = hitClass == (int)CombatHitClass.Missile
     ? CombatHitClass.Missile
     : CombatHitClass.Bullet;      // ← everything else collapses here
@@ -184,6 +185,14 @@ the point of re-validating rather than trusting the wire.
 **General lesson:** a "check for the one special member, else the default" validator encodes the
 current size of an enum. It does not fail when the enum grows — it mis-files.
 
+**And the enum did grow again, twice, which is the point.** `CombatHitClass.Missile` was renamed
+`MissileDirect` in 2026-09 when the skyburst's three radii became three ranked classes
+(`MissileBlast = 3`, `MissileShockwave = 4`; the snippet above is preserved as it stood). Nothing
+about this mode changed — a Dolphin bend is still `Debuff`, still validated by `Enum.IsDefined`,
+still paid at this mode's rate and nowhere else — which is exactly what the fix bought: the
+validator absorbed two new members without anyone revisiting it. The version it replaced would
+have mis-filed both as `Bullet`.
+
 ---
 
 ## Where the point VALUES live
diff --git a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
index e6728059e..bf26b5dc6 100644
--- a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
+++ b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
@@ -223,6 +223,22 @@ incremented on an upgrade, because it is the same rocket arriving closer. It nev
 downward — a shockwave arriving after a direct hit is the same rocket's outer edge and is
 refused.
 
+**The whole ranked-not-additive design rests on one line in `VesselCombatHitLatch.Key`'s
+constructor**: the three missile classes canonicalise to `MissileDirect`, so they contend for a
+single window. Key them apart — the obvious reading of "dedupe by (shooter, victim, class)" —
+and the tiers become additive again: a centre-punch pays 10 + 20 + 30 = 60 through three fresh
+windows, each of them individually correct. The upgrade rule is machinery *on top of* that one
+line and cannot compensate for its absence, because a fresh key has nothing to supersede.
+
+That is worth stating because the first round of tests could not see it. Every upgrade test
+called `CombatHitScoring.Credit` directly with a hand-supplied `supersededRank`, so it proved
+the arithmetic and never touched the thing that has to produce that rank — the value under test
+was supplied by the test, and keying the tiers apart would not have moved one assertion.
+`SparrowCombatTierTests`'s `ThroughTheLatch_*` cases now run the full path an effect asset takes
+(`TryAdmit`, then `Credit` with whatever it reported). **General shape: when a design lives in a
+lookup key, a test that starts downstream of the lookup is testing the consequence, not the
+design.**
+
 ## The skyburst launches from the missile bay (2026-08)
 
 The rocket no longer materializes at a floating gun point: the press opens the Sparrow's
@@ -744,7 +760,7 @@ the bullet effect onto `SparrowFullAutoProjectileImpactContainer` **and**
 | Site | Change |
 |---|---|
 | `GameModes` | `DogFight = 41` |
-| `CombatHitClass` | new enum (`Bullet` / `Missile`) |
+| `CombatHitClass` | new enum. `Bullet` / `MissileDirect` / `Debuff` / `MissileBlast` / `MissileShockwave` — the three missile members are RANKED tiers of one rocket (`CombatHitClasses.MissileProximityRank` / `IsMissile`). The two added in 2026-09 took values **3 and 4**, so every already-serialized `hitClass: 0/1/2` keeps its meaning |
 | `IRoundStats` / `RoundStats` | `BulletHitsLanded`, `MissileHitsLanded`, `CombatPoints` (+ events, + server-write NetworkVariables, + `Cleanup`, + `ClearEventSubscriptions`) |
 | `ScoringMetric` / `ScoringMetrics.Read` | `CombatPoints = 8` |
 | `ScoringRuleSO` | `PointsForCombatHit` virtual — a mode's opinion of what a landed hit is worth (0 everywhere else) |
@@ -766,6 +782,10 @@ the bullet effect onto `SparrowFullAutoProjectileImpactContainer` **and**
 | `ElementalCrystalSetSO` | `RandomElementFrom(System.Random)` — a seeded pick, so a scatter can be reproduced identically on every peer |
 | `AOEConicSkyBurst.prefab` | given the explosion container it never had — a skyburst BLAST can now reach a pilot |
 | `IRoundStatsCleanupTests` | asserts the three new stats zero |
+| `Projectile` | `sweptVesselDetection` — the vessel twin of `sweptPrismDetection`. Both sweeps rent their scratch BY DEPTH: dispatching a swept contact runs the effect list synchronously, and a chain-firing effect re-enters mid-iteration. That re-entry is unreachable from the ship arm *today* only because `ProjectileChainFire` happens to be authored as a projectile-PRISM effect — a guard made of which container an asset sits in, not of anything in the code |
+| `ProjectileImpactor` | vessel-case suppression mirroring the prism arm: a sweeping round's PhysX trigger path is skipped for vessels, so nothing double-dispatches |
+| `VesselRearmOnPrismDestruction` | new: the missile tank fills from hostile prism kills (0.01/prism, 50 per rocket) |
+| `VesselTimedElementalWard` | new: the event-driven half of debuff immunity — a window that opens on an event and closes on a clock, which `VesselElementalImmunity`'s condition-polling cannot express |
 
 ## In-editor verification (authored headless — NOT yet run)
 
@@ -871,6 +891,21 @@ the bullet effect onto `SparrowFullAutoProjectileImpactContainer` **and**
 
 ## Known limitations / follow-ups
 
+- **`author_dogfight_assets.py --check` was validating nothing, and two sibling generators
+  still are.** Section 9 clones the Rampage scene and asserts on a donor field block the Rampage
```

</details>

### `f27f623de` — docs(skills): three gate traps this branch paid for

_Claude, 2026-09-07 16:06:20 +0000_

```text
asset-surgery - a whole-file #if makes the compile see NOTHING, and that reads as
clean. Sits a rung below the base-type binding trap already recorded: 78 of this
project's 111 test files open with #if UNITY_EDITOR, so a harness that does not define
the symbol compiles an empty file and reports zero errors. The tell is the error COUNT
- zero CS0246 on a file full of MonoBehaviours means the compiler never saw it.

ship §2 - three review gates:
  - a test that SUPPLIES the value under test proves the consequence, not the design.
    The missile tiers are ranked-not-additive because of one line in a Dictionary key's
    constructor, and every upgrade test handed Credit a hand-made supersededRank, so
    keying the tiers apart would not have moved an assertion.
  - a gate that ABORTS looks exactly like a gate that passes if nobody reads its output.
    Names the two generators still in that state, and asks of any --check whether it
    diffs against disk or only validates its own recipe.
  - an absence that holds only because of where an ASSET was filed is not guarded by
    the code - the editorial-vs-structural question to ask of any "this cannot happen"
    comment.

Complements 7dcc44b4 rather than restating it: that one covers stubbing a first-party
type and enumerating an enum in a rule-guarding test; these are about what a gate
actually executed.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 14 ++++++++++++++
 .claude/skills/ship/SKILL.md          | 37 +++++++++++++++++++++++++++++++++++++
 2 files changed, 51 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 75e141b78..d79dfb951 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -1416,6 +1416,20 @@ reconstructing rather than replaying: your dump/render code contains an offset,
 multiply, or a pivot decision that ALSO exists in the shipped code. That duplicated line is the
 one nobody is testing.
 
+### Trap: a whole-file `#if` makes the compile see NOTHING, and that reads as clean
+
+The traps above are about what a compile can and cannot BIND. This one is a rung below: it may
+not have compiled a single line. **78 of this project's 111 test files open with
+`#if UNITY_EDITOR`** (it is the convention for a test under an `Editor/` folder), so a §4 harness
+that does not set `<DefineConstants>UNITY_EDITOR</DefineConstants>` compiles an empty file and
+reports zero errors — indistinguishable from a clean pass, and arrived at faster.
+
+The tell is the error COUNT, not its absence: a real Unity gameplay or test file compiled without
+`UnityEngine.dll` produces *hundreds* of `CS0246`s. **Zero unresolved-type errors on a file full
+of `MonoBehaviour`s means the compiler never saw the file.** Check that before believing a green
+run, and grep the file's first line for a guard before writing the csproj. The same applies to
+any `#if` a file is wrapped in — `DEVELOPMENT_BUILD`, a package define, a custom symbol.
+
 ### Trap: compiling a COPY cannot see whole-class consistency
 
 The harness pattern in §4 — paste the block under test into a stub file and compile it — proves
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index 63b22897f..9a80e9df8 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -179,6 +179,43 @@ Walk every changed file against these gates:
   them to enumerate the enum (`Enum.GetValues(...).Where(x => x != TheException)`), so the next
   member is covered on the day it is added rather than the day someone remembers.
 
+- **A test that SUPPLIES the value under test proves the consequence, not the design.** A helper
+  that takes the interesting quantity as a parameter is trivially easy to test, and a suite built
+  on it can be thorough, green, and blind to the thing that actually decides the behaviour. The
+  Sparrow's three missile tiers are ranked-not-additive because of ONE line in
+  `VesselCombatHitLatch.Key`'s constructor (the three classes canonicalise to one key); every
+  upgrade test called `CombatHitScoring.Credit` with a hand-supplied `supersededRank`, so keying
+  the tiers apart — which makes a centre-punch pay 60 through three individually-correct fresh
+  windows — would not have moved a single assertion. **The tell: you are passing a value your
+  production code derives.** Find where it is derived and start the test there; if that is hard
+  to reach, that difficulty is the finding. Its close relative — **when a design lives in a lookup
+  KEY, a test that starts downstream of the lookup cannot see the design at all.**
+
+- **A gate that ABORTS looks exactly like a gate that passes, if nobody reads its output.** This
+  repo's `Tools/Build/author_*.py` generators do a one-time migration first (clone a donor scene,
+  patch its wiring) and validate everything they built AFTER it. When the donor moves on, the
+  migration's `assert` fires and takes every check below it — while `--check` is still in the
+  workflow, still cited in commit messages, and still exits in a way nobody looks at.
+  `author_dogfight_assets.py` validated *nothing* for months, including four checks a branch had
+  just added to guard its own scoring; `author_ribcage_assets.py` and
+  `author_wildlife_liberation_assets.py` are in that state now. So: **a green gate is only
+  evidence if you can name a failure it produced.** Before citing one, break the thing it guards
+  and watch it fail — and when a spent one-shot is the blocker, make it STAND DOWN (guard the
+  step, register the already-committed output so downstream checks describe the shipped artifact)
+  rather than deleting the checks or living with the abort. Also worth asking of any `--check`:
+  does it diff against DISK, or only validate its own recipe in memory? Those are very different
+  promises and the flag name does not distinguish them.
+
+- **An absence that is true only because of where an ASSET was filed is not guarded by the code.**
+  The absence-claim rule below covers comments that rot. This is the variant that was never true
+  in the way it read: `Projectile.SweepVesselsAlong` used single static scratch and argued it was
+  safe because no vessel effect fires another projectile mid-sweep — correct, but only because
+  `ProjectileChainFire` happens to be authored as a projectile-PRISM effect, so the ship arm
+  cannot reach it. Nothing stops someone dropping it into a ship container. When a comment
+  explains why a hazard cannot arise, ask whether the reason is structural or editorial; if a
+  designer could falsify it from the inspector, take the class (here: rent by depth, as the
+  sibling sweep already did) rather than restating the instance.
+
 - **A comment asserting an ABSENCE rots exactly as silently as one asserting a presence.**
   §2's producer rule and its dead-surface mirror both cover claims about what the code DOES.
   The third shape is a comment that argues why something is NOT there — "no property block",
```

</details>

### `dde77ea90` — docs(sparrow): repair three numbers the two retunes left behind

_Claude, 2026-09-07 16:07:49 +0000_

```text
Consistency sweep after the ammo rate halved (0.02 -> 0.01) and the Dog Fight target
moved 500 -> 120 -> 90. Every one of these read as authoritative and was simply out of
date; none is a behaviour change.

  DOGFIGHT.md step 14 said milestones fire at 125 and 250 points. The rungs are
  FRACTIONS of the target (0.25 / 0.5), so at 90 they fire at 23 and 45 - the numbers
  in that step were the 500-target ones and survived two re-targets. Restated as the
  fractions with a note to read them off the target, not off the line.

  FLEET_MAPS.md, AUDIT.md and SalvoController.cs's own docstring all still said
  "0.02 per prism, so 25 prisms per rocket". Salvo's is the one that mattered most:
  that docstring is the mode's argument about how self-funding its Sparrows are.

DOGFIGHT.md also gains verification step 17e for the missile charge gauge, with the
two ways it can be wrong and still look plausible spelled out: a bar that saturates
instead of resetting is saying what the ICON ladder already says, and firing a rocket
must drop the ladder while the bar keeps its own progress.
```

```text
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md        | 19 +++++++++++++++++--
 Assets/_Scripts/Controller/Arcade/SalvoController.cs |  2 +-
 Docs/ElementalAbilitySystem/AUDIT.md                 |  2 +-
 Docs/ElementalAbilitySystem/FLEET_MAPS.md            |  5 +++--
 4 files changed, 22 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
index bf26b5dc6..eda56f125 100644
--- a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
+++ b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
@@ -844,8 +844,12 @@ the bullet effect onto `SparrowFullAutoProjectileImpactContainer` **and**
     time, everyone else "N Points Left", and the secondary line reads `N pts · X×● Y×◆`. Confirm
     a *teammate* of the winner DOES get the winner's time — teammates pool, so they share the
     win. Replay (scene reload) resets the milestones and the counters.
-14. **Milestones.** When the leading domain reaches **125** points the device should shake hard
-    for ~1.2 s; again at **250**. Nothing else should change.
+14. **Milestones.** The rungs are FRACTIONS of the target (`firstMilestoneFraction` 0.25,
+    `secondMilestoneFraction` 0.5), so at the shipped 90-point target the leading domain crosses
+    them at **23** and **45** points — the device shakes hard for ~1.2 s at each, and nothing
+    else changes. *(This step read "125 / 250" until 2026-09: those were the 500-target numbers
+    and survived two re-targets. The rungs move with the target — read them off
+    `EndConditionOverridesSO.dogFightPointTarget` × the two fractions, never off this line.)*
 15. **AI DOGFIGHTS — it must LEAVE.** Watch an AI Sparrow for a minute. The loop should read as
     *close → pass → run out a long way → turn → come back in*, with a visible gap between passes.
     If it stays glued to you circling, the extend is not committing (check that
@@ -877,6 +881,17 @@ the bullet effect onto `SparrowFullAutoProjectileImpactContainer` **and**
 17d. **Mass grows what you HIT WITH.** With Mass buffed, turret rounds should be both visibly
     longer *and* easier to land. If they look bigger but feel identical to aim, the hit diameter
     has stopped riding the multiplier.
+17e. **THE MISSILE GAUGE FILLS AND RESETS** (new in 2026-09, and visible in every mode, not
+    just this one). The Charge card's plate is now the missile charge bar. Destroy hostile prisms
+    and watch it fill; on the **50th** it should snap back to empty as a rocket lands in the bay,
+    then start filling again toward the second. Two specific things to confirm, because they are
+    the two ways this can be wrong and still look plausible: the bar must **reset**, not saturate
+    (a gauge stuck full says the tank is full, which is what the ICON ladder says — the bar says
+    how close the NEXT one is, and a tank of two cannot say both on one bar); and firing a rocket
+    must drop the icon ladder while the bar keeps its own progress, since spending ammo is not
+    the same event as earning it. Seed check: entering a match, the bar should paint from the
+    live ammo value rather than starting at 0 and jumping on the first kill.
+
 18. **The objective arrow points at an ENEMY.** In a 2v2, confirm the marker tracks an opposing
     pilot and never your wingman, and that it re-targets when your quarry disappears behind a
     hulk.
diff --git a/Assets/_Scripts/Controller/Arcade/SalvoController.cs b/Assets/_Scripts/Controller/Arcade/SalvoController.cs
index ead5539bc..db28208f6 100644
--- a/Assets/_Scripts/Controller/Arcade/SalvoController.cs
+++ b/Assets/_Scripts/Controller/Arcade/SalvoController.cs
@@ -31,7 +31,7 @@ namespace CosmicShore.Gameplay
     /// does not regenerate - the ONLY refuel is an omni crystal
     /// (<c>SparrowVesselChangeResourceByCrystalEffect</c>)". That asset is DELETED. The Sparrow's
     /// missiles now reload by DESTROYING HOSTILE PRISMS
-    /// (<c>VesselRearmOnPrismDestruction</c>, 0.02 per prism = 25 prisms per rocket), and the omni
+    /// (<c>VesselRearmOnPrismDestruction</c>, 0.01 per prism = 50 prisms per rocket), and the omni
     /// crystal instead grants an 8-second elemental-debuff ward. This mode's premise is therefore
     /// softened rather than broken: a Sparrow tearing up the Boneyard is now self-funding, so the
     /// crystal line is an ACCELERANT rather than the sole tap, and the tension between "shoot the
diff --git a/Docs/ElementalAbilitySystem/AUDIT.md b/Docs/ElementalAbilitySystem/AUDIT.md
index 7ff1fbd0a..0ee9f6b7d 100644
--- a/Docs/ElementalAbilitySystem/AUDIT.md
+++ b/Docs/ElementalAbilitySystem/AUDIT.md
@@ -182,7 +182,7 @@ at."
   `vesselCrystalEffects` — any crystal impact refills Missiles to full (2 rockets). The economy
   is 2 rockets per crystal, as the ability card promises.
   **SUPERSEDED 2026-09** — that asset is now DELETED. The Sparrow's missiles reload by destroying
-  hostile prisms (`VesselRearmOnPrismDestruction`, 0.02 per prism) and the omni crystal grants an
+  hostile prisms (`VesselRearmOnPrismDestruction`, 0.01 per prism = 50 per rocket) and the omni crystal grants an
   elemental-debuff ward instead. The finding above is kept as the dated record it is; do not read
   its present tense as current wiring.
 - **Dead `Gun` with `projectileFactory: {fileID: 0}`** on the prefab (`Sparrow.prefab:212-227`,
diff --git a/Docs/ElementalAbilitySystem/FLEET_MAPS.md b/Docs/ElementalAbilitySystem/FLEET_MAPS.md
index 6590e6861..843d21e2c 100644
--- a/Docs/ElementalAbilitySystem/FLEET_MAPS.md
+++ b/Docs/ElementalAbilitySystem/FLEET_MAPS.md
@@ -120,8 +120,9 @@ beside the code: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER
 
 **CHARGE row, changed 2026-09 — the rocket's ECONOMY and its FUZE:**
 
-- **Missiles are no longer crystal-stocked.** They recharge by DESTROYING HOSTILE MASS (0.02 per
-  prism, so 25 prisms per rocket) through `VesselRearmOnPrismDestruction` on the vessel root, which
+- **Missiles are no longer crystal-stocked.** They recharge by DESTROYING HOSTILE MASS (**0.01**
+  per prism, so **50 prisms per rocket** and 100 for a full rack — halved from 0.02 later in the
+  same pass) through `VesselRearmOnPrismDestruction` on the vessel root, which
   listens on the prism-destroyed SOAP channel — the only producer that sees all five ways a Sparrow
   destroys a prism, including the missile blast, whose Burst batch path dispatches no per-prism
   effects at all.
```

</details>

### `4aea0e484` — fix(ui): the Urchin's PIP painted a navy slab over half the screen

_Claude, 2026-09-07 16:19:23 +0000_

```text
Reported as "appshell UI with a render texture covering half my screen while
trying to play". It is neither the appshell nor the render texture: it is
Pip.prefab's `border`, a RawImage pointing at texture guid
24ca4c74937a9ed4d9251952057575ab, which no asset in the project carries any
more. A RawImage with a missing texture does not draw nothing - it draws a
SOLID QUAD in its own tint, and that tint is (0.14, 0.29, 0.52). At 780x400
canvas units it covers 55% of the display, with the actual 300x150 view
sitting inside it. The only evidence anywhere is the rectangle itself, which
is why three static reads of scenes and prefabs got it wrong before the
on-screen report named the object.

Why it surfaced now, and the answer to "how did the Dolphin mode avoid this":
eight of eleven hulls carry a Pip component (Falcon, Grizzly, Manta, Serpent,
Shrike, Squirrel, Termite, Urchin) and Dolphin, Sparrow, Rhino and Scarab do
not - so Rampage, Bends, Dog Fight, Salvo, Scarab Scramble, Peel the Cage,
Astro League and Switchback all sidestepped it by the hull they lock to.
Hijack is the first arcade mode locked to a vessel that has one.

Three defects, and only the first is the reported one:

1. PipUI now refuses to draw a RawImage with no texture, and NAMES it once with
   its size and tint. Deliberately a runtime statement rather than switching
   the component off in the prefab: an asset edit cannot say which texture is
   missing, would keep the frame dark forever once the art returned, and would
   not cover the next one. Silence is not a fallback hiding a fault - the
   warning IS the fault, stated once; what is suppressed is a lie about the UI.

2. Pip gated on `!AutoPilotEnabled` as a stand-in for "is this my ship". That
   flag is set by AIPilot.ActivateAutopilot, which the spawn chain runs AFTER
   the vessel exists, so at Start it reads false on EVERY vessel - which is how
   the report caught two Urchin FarViewCameras live at once, both writing the
   one shared PipRenderTexture, and how an AI hull could raise IsActive on a
   global SOAP channel that has no owner test. Ownership now comes from
   VesselController.Initialize/ChangePlayer under IPlayer.IsLocalPilot, the same
   two sites and the same test the prism occlusion corridor, the speed tunnel
   and the vessel vision band bind at, with an identity-guarded static owner so
   a non-local vessel bound in an arbitrary order cannot blank the local pilot's
   panel. Awake defaults the camera OFF - PipCamera.prefab ships active and
   enabled and four vessels instance it while carrying no Pip at all, so those
   hulls were each rendering a whole extra camera pass into a texture nothing
   was showing - and it applies the last known bind rather than a literal false,
   since a vessel spawned inactive runs Awake after Initialize.

3. PipUI.SetMirrored negated the stored scale, so it was correct exactly once
   and flipped the panel back on a second call. Harmless while the panel was
   told its facing once at Start; not harmless now that it is told on every
   ownership bind. It reads from the absolute value instead, which is what makes
   "tell it again" safe for a caller.

Verified: 1815 files parse clean; a shape-only Roslyn pass diffed against the
bleeding-edge baseline adds no finding beyond the pre-existing
UniTask-without-references noise; check_conditional_compilation OK. Needs a
play-mode confirmation that the Urchin's Pip now shows its view with no navy
frame and that only one FarViewCamera is live.
```

```text
 Assets/_Scripts/Controller/Vessel/Pip.cs              | 110 +++++++++++++++++++++++++++++++++++++++++-------
 Assets/_Scripts/Controller/Vessel/VesselController.cs |  11 +++++
 Assets/_Scripts/UI/PipUI.cs                           |  63 ++++++++++++++++++++++++++-
 3 files changed, 166 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 245 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/Pip.cs b/Assets/_Scripts/Controller/Vessel/Pip.cs
index d6ee89349..1e3fa10cf 100644
--- a/Assets/_Scripts/Controller/Vessel/Pip.cs
+++ b/Assets/_Scripts/Controller/Vessel/Pip.cs
@@ -1,34 +1,112 @@
 using CosmicShore.ScriptableObjects;
 using UnityEngine;
-using CosmicShore.Gameplay;
-using CosmicShore.UI;
+
 namespace CosmicShore.Gameplay
 {
+    /// <summary>
+    /// The vessel's picture-in-picture view: a second camera on the hull rendering into the
+    /// shared <c>PipRenderTexture</c>, shown in the HUD's Pip panel. Eight vessels carry one
+    /// (Falcon, Grizzly, Manta, Serpent, Shrike, Squirrel, Termite, Urchin); Dolphin, Sparrow,
+    /// Rhino and Scarab do not, which is why no arcade mode locked to those four has ever had
+    /// to think about it.
+    ///
+    /// <para><b>Exactly one vessel in a match may drive it, and that vessel is the LOCAL PILOT'S.
+    /// </b> There is one <c>PipRenderTexture</c> asset and one Pip panel on the HUD, so a second
+    /// camera writing that texture is not a second view - it is two cameras overwriting each
+    /// other's frames, and the panel shows whichever wrote last. Ownership therefore comes from
+    /// <see cref="VesselController"/>, the one method every vessel routes through on every spawn
+    /// path, under the same <c>IPlayer.IsLocalPilot</c> test the prism occlusion corridor, the
+    /// speed tunnel and the vessel vision band use.</para>
+    ///
+    /// <para><b>What it must NOT ask is <c>AutoPilotEnabled</c>, and the reason is an ordering
+    /// one.</b> That flag is set by <c>AIPilot.ActivateAutopilot</c>, which the spawn chain runs
+    /// AFTER the vessel is instantiated - so at <c>Start</c> it reads false on every vessel in
+    /// the match, AI and human alike. Gating on it therefore switched the camera on for every
+    /// AI hull too and raised <c>IsActive = true</c> on a global SOAP channel that has no owner
+    /// test, which is how a Hijack match ended up running two Urchin far-view cameras into one
+    /// render texture. The flag was never a stand-in for "is this my ship"; it only ever looked
+    /// like one because a human's autopilot is off by the time anyone looks.</para>
+    ///
+    /// <para>The default is OFF, applied in <c>Awake</c> so it lands before any
+    /// <c>Initialize</c>: <c>PipCamera.prefab</c> ships active and enabled, and four vessels
+    /// (Sparrow and Scarab among them) instance it while carrying no <see cref="Pip"/> at all -
+    /// so without a default-off every one of those hulls renders a whole extra camera pass,
+    /// forever, into a texture nothing is showing.</para>
+    /// </summary>
     [RequireComponent(typeof(IVesselStatus))]
     public class Pip : MonoBehaviour
     {
-        [SerializeField] Camera pipCamera;
-        [SerializeField] bool mirrored;
+        [SerializeField, Tooltip("The second camera on this hull. Rendered only while this " +
+                                 "vessel is the local pilot's.")]
+        Camera pipCamera;
+
+        [SerializeField, Tooltip("Mirror the panel horizontally - a rear-view hull wants it, a " +
+                                 "forward-looking one does not.")]
+        bool mirrored;
+
+        [SerializeField] ScriptableEventPipData _EventPipEventData;
 
-        [SerializeField]
-        ScriptableEventPipData _EventPipEventData;
+        /// <summary>
+        /// The one vessel currently speaking for the HUD panel, or null. IDENTITY-GUARDED for
+        /// the same reason the corridor and the speed tunnel guard their targets: several
+        /// vessels are bound in an order nobody controls - AI hulls are pre-spawned ahead of the
+        /// human's, a client receives every pair in one RPC, and Cellular Duel hands a live
+        /// vessel from a human to an AI at a round boundary. If a NON-local vessel were allowed
+        /// to announce "panel off", whichever of those happened to be bound last would blank the
+        /// local pilot's view, and the panel would be dark or lit depending on spawn order.
+        /// </summary>
+        static Pip _panelOwner;
 
+        /// <summary>The last answer <see cref="SetLocalPilot"/> was given; false until it is
+        /// called, which IS the default-off.</summary>
+        bool _isLocalPilot;
+
+        void Awake()
+        {
+            // Default OFF: PipCamera.prefab ships active and enabled, so a hull that is never
+            // bound would otherwise render a whole extra camera pass forever into a texture
+            // nothing is showing - four vessels instance that prefab without carrying a Pip.
+            //
```

</details>

### `d8cc73e98` — tune(hijack): halve the steal target to 750, and move the comeback rate with it

_Claude, 2026-09-07 16:53:42 +0000_

```text
Requested. 750 is still 41% of the ~1,848 prisms an intensity-1 yard holds
hostile to any one domain, so the yard is nowhere near exhausted at the
whistle; the intended match length is now roughly half the original 3-5 minute
estimate, and that estimate was never measured either (HIJACK.md says so).

The rate had to move with it. `bonusLevels = deficit x rate`, so halving the
target halves what a given rate buys - the trap Dog Fight, The Bends and
Wildlife Liberation have each recorded independently. At 0.008 a
quarter-of-target deficit would have bought 1.5 element levels instead of 3.0,
and the generator's assert would NOT have caught it: that assert is a floor
(fails under one whole level), not the spec. Rate doubles 0.008 -> 0.016 so a
quarter-of-target deficit still buys 3.0 levels, matching Wildlife Liberation's
curve. The comment now says the assert is a floor, so the next retune does not
read a pass as a confirmation.

Authored through the generator, which owns every consumer and asserts the C#
constant matches its own: EndConditionOverridesSO's default, live and build
fields, EndConditionOverrides.asset, and ArcadeGameHijack.asset's comeback rate.
Docs and the turn monitor's docstring follow (the goal row now reads
STEAL PRISMS n/750).

Verified: author_hijack_assets --check passes (38 files), hijack_budget --check
all proofs passed, 1815 files parse clean with no new shape findings against the
bleeding-edge baseline, check_conditional_compilation OK.
```

```text
 Assets/Resources/EndConditionOverrides.asset                             |  4 ++--
 Assets/_SO_Assets/Games/ArcadeGameHijack.asset                           |  2 +-
 Assets/_Scripts/Controller/Arcade/HIJACK.md                              | 22 ++++++++++++----------
 Assets/_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs |  4 ++--
 Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs             | 10 +++++-----
 CLAUDE.md                                                                |  2 +-
 Tools/Build/author_hijack_assets.py                                      | 18 ++++++++++++------
 7 files changed, 35 insertions(+), 27 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 164 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/HIJACK.md b/Assets/_Scripts/Controller/Arcade/HIJACK.md
index c583d98f6..5e7e135fc 100644
--- a/Assets/_Scripts/Controller/Arcade/HIJACK.md
+++ b/Assets/_Scripts/Controller/Arcade/HIJACK.md
@@ -1,6 +1,6 @@
 # Hijack — the Urchin heist race (`GameModes.Hijack = 46`)
 
-**Urchin-only. First DOMAIN to steal 1,500 prisms wins.** Nothing in this mode is ever
+**Urchin-only. First DOMAIN to steal 750 prisms wins.** Nothing in this mode is ever
 destroyed: mass only changes hands.
 
 Three great-circle **rails** ring a hollow core, meeting at spiny **burrs** of raw prism where
@@ -32,7 +32,7 @@ Urchin's own, applied to geometry shaped to invite it.
 ## 2. The loop
 
 **0:00–0:10** — spawn on the equatorial ring at r = 1120 facing the core. A rail crosses your
-path a couple of hundred units ahead; the goal row reads `STEAL PRISMS 0/1500`; the arrow points
+path a couple of hundred units ahead; the goal row reads `STEAL PRISMS 0/750`; the arrow points
 at the nearest burr still holding mass you could take.
 
 **0:10–0:30 — the arena is the tutorial.** Fly into the rail and you attach. On your colour's
@@ -183,13 +183,14 @@ prism. A count is also the only thing a goal row can say.
 - **Turn monitor:** `HijackStealTurnMonitor`, reading `EndConditionOverridesSO.GetHijackStealTarget()`
   → NetworkVariable → `GameDataSO.PrismTargetCount`.
 - **Goal row:** one new `ObjectiveIconSet` entry (`metric 9`, "Steal prisms") drives
-  `STEAL PRISMS 340/1500` through the existing GoalStack with zero HUD code. A new metric is the
+  `STEAL PRISMS 340/750` through the existing GoalStack with zero HUD code. A new metric is the
   one thing that needs new art; the glyph is the family's own prism silhouette, solid behind a
   chevron front and hollow ahead of it.
-- **Target 1,500.** Explicitly **unmeasured** (the Salvo precedent), sized against the
-  intensity-1 yard's 2,772 prisms of which ~1,848 are hostile to any one domain. One editor field
-  is the dial.
-- **Comeback rate 0.008** → a quarter-of-target deficit (375) buys **3.0** element levels. The
+- **Target 750.** Explicitly **unmeasured** (the Salvo precedent), sized against the
+  intensity-1 yard's 2,772 prisms of which ~1,848 are hostile to any one domain — so the target
+  is 41% of what one domain can take and the yard is far from exhausted at the whistle. One
+  editor field is the dial. **HALVED from 1,500** on request; the rate below moved with it.
+- **Comeback rate 0.016** → a quarter-of-target deficit (187) buys **3.0** element levels. The
   generator FAILS the build if a retune ever drops that under one whole level — the trap Dog
   Fight, The Bends and Wildlife Liberation have each recorded independently.
 
@@ -341,7 +342,7 @@ item is a real check a human has to perform, in this order (load-bearing first).
    back after a spike tap.
 5. **Roll a burr** — you attach and marble-roll the spines; yours grow, hostile ones flip one per
    hop.
-6. **Win + scoreboard.** First domain to 1,500 ends the turn; "HEIST TIME" for the winners.
+6. **Win + scoreboard.** First domain to 750 ends the turn; "HEIST TIME" for the winners.
 7. **AI plays.** AI Urchins grind rails at full speed (the `ram: 1` check), launch off ends, and
    their domain's score climbs. They must not orbit pilots or converge on the core crystal.
 8. **Comeback.** Fall ~375 behind: the trailing pilots' element flowers fill ~3 levels; at Time 5
@@ -414,8 +415,9 @@ merge without a conflict in that file. Do not edit it on this branch for that re
   up on its objective, not just here. It has the Rhino's precedent and it is AI-only, so no human
   pilot is affected; if it ever needs to be narrower, the honest lever is a per-mode setter rather
   than a prefab field.
-- **1,500 is unmeasured**, and so is the intensity ladder's effect on match length. Intended
-  3–5 minutes; the target is one editor field.
+- **750 is unmeasured**, and so is the intensity ladder's effect on match length. It was halved
+  from 1,500 for pace without re-measuring either, so the intended length is now roughly half of
+  the original 3–5 minute estimate — which is itself an estimate. The target is one editor field.
 - **Mass-5 armour on rail prisms** is the one uncapped collider source — see §7.
 - **No `ModePreview_Hijack.asset`**, so the arcade card shows "LEVEL PREVIEW NOT AVAILABLE".
   Salvo ships the same way, so this is a gap rather than a regression.
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs
index 6576107fa..a00eb96e3 100644
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs
+++ b/Assets/_Scripts/Controller/Arcade/TurnMonitors/HijackStealTurnMonitor.cs
@@ -7,13 +7,13 @@ namespace CosmicShore.Gameplay
 {
     /// <summary>
     /// Turn monitor for Hijack. The steal TARGET - how many prisms a domain must take between
-    /// them to win (default 1500) - is resolved at <see cref="StartMonitor"/> from
+    /// them to win (default 750) - is resolved at <see cref="StartMonitor"/> from
     /// <see cref="EndConditionOverridesSO"/> (FrogletTools &gt; Game Modes &gt; End Game
     /// Conditions; never a per-scene field), synced to every client via NetworkVariable, and
     /// published to <see cref="GameDataSO.PrismTargetCount"/>. The turn ends (server-side) when
     /// the mode's <see cref="ScoringRuleSO.IsObjectiveReached"/> reports an active domain's steal
     /// sum has reached it. The display channel carries the LOCAL player's domain deficit, which
-    /// the goal row renders as "STEAL PRISMS n/1500". Structural clone of
+    /// the goal row renders as "STEAL PRISMS n/750". Structural clone of
     /// <see cref="SalvoPrismTurnMonitor"/> reading its own overrides key.
```

</details>

### `6c1f9ee40` — docs: record the two Pip findings and the on-screen UI reader

_Claude, 2026-09-07 16:59:14 +0000_

```text
CLAUDE.md gains two anti-patterns, both of which cost this session real time:

- A RawImage with a MISSING texture draws a solid quad in its own tint, not
  nothing - so a frame whose art was deleted becomes an opaque panel and the
  only evidence anywhere is the panel itself. It is invisible to static reads
  of scene/prefab YAML, which is why three of them produced three confident
  wrong answers. Carries the corollary the timing turned on: a defect fenced
  behind a per-prefab component is dormant rather than absent, so Dolphin,
  Sparrow, Rhino and Scarab carrying no Pip is the whole reason no arcade mode
  had hit it before one locked to the Urchin.

- AutoPilotEnabled is false on EVERY vessel at Start (ActivateAutopilot runs
  later in the spawn chain), so it cannot stand in for "is this my ship" - it
  merely looks like one on a human. Ownership binds at
  VesselController.Initialize AND ChangePlayer under IsLocalPilot, and a shared
  single-owner surface is guarded by identity the way the three platform laws
  guard theirs.

Docs/DIAGNOSTICS.md gains the Report On-Screen UI section (a fourth entry in
the family's table, its own section, and a Files row), and CLAUDE.md's doc-index
row points at it, because the tool is only useful if you reach for it BEFORE
reading YAML rather than after.
```

```text
 CLAUDE.md           | 36 +++++++++++++++++++++++++++++++++++-
 Docs/DIAGNOSTICS.md | 32 +++++++++++++++++++++++++++++---
 2 files changed, 64 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 109 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 424255974..53141be14 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -1267,7 +1267,7 @@ MiniGameControllerBase (abstract, NetworkBehaviour)
 | `PRISM_PERFORMANCE_AUDIT.md` | `_Scripts/Game/Prisms/` | Prism system performance analysis (vestigial location) |
 | `UNIT_TESTING_GUIDE.md` | `_Scripts/Tests/` | Unit testing guidelines and inventory |
 | `BENCHMARK_TOOL.md` | `_Scripts/Utility/PerformanceBenchmark/` | Performance Benchmark tool guide (tabs, score/hints, sweep, Load Time Insights, customization) |
-| `DIAGNOSTICS.md` | `Docs/` | The **FrogletTools ▸ Diagnostics** family: the editor **Crash Detector** (off-thread error journal + heartbeat sentinel; abnormal exits — crashes, PC faults, hangs-then-kills — are reported on the next launch from the journal + Unity's own `Editor-prev.log`) and the **Bug Ledger** (the team's live bug list: every distinct red-error signature auto-files ONE issue file into the gitignored `BugLedger/local/` store — version control sees ledger data only when it is explicitly staged & pushed from the tool's Stage & Push tab, which commits ledger paths ONLY; a fix is only believed once the game validates it — clean play/editor sessions for captured errors, a clean full re-run for tool-filed findings — then archived to `BugLedger/local/resolved/`; a recurrence reopens the issue as a regression). The signature core is the runtime-safe `CosmicShore.Utility.BugSignature`, shared with the planned in-game reporter. Also the opt-in **Compile Timing** tab (compile + domain-reload seconds per edit, and which assemblies rebuilt — the measurement behind `Docs/ASSEMBLY_SPLIT.md`). **Read before touching anything under `Assets/_Scripts/Editor/Diagnostics/`, `BugSignature`, or the `BugLedger/` store — and before wiring an auditor's findings into the ledger.** |
+| `DIAGNOSTICS.md` | `Docs/` | The **FrogletTools ▸ Diagnostics** family: the editor **Crash Detector** (off-thread error journal + heartbeat sentinel; abnormal exits — crashes, PC faults, hangs-then-kills — are reported on the next launch from the journal + Unity's own `Editor-prev.log`) and the **Bug Ledger** (the team's live bug list: every distinct red-error signature auto-files ONE issue file into the gitignored `BugLedger/local/` store — version control sees ledger data only when it is explicitly staged & pushed from the tool's Stage & Push tab, which commits ledger paths ONLY; a fix is only believed once the game validates it — clean play/editor sessions for captured errors, a clean full re-run for tool-filed findings — then archived to `BugLedger/local/resolved/`; a recurrence reopens the issue as a regression). The signature core is the runtime-safe `CosmicShore.Utility.BugSignature`, shared with the planned in-game reporter. Also the opt-in **Compile Timing** tab (compile + domain-reload seconds per edit, and which assemblies rebuilt — the measurement behind `Docs/ASSEMBLY_SPLIT.md`), and the standalone **Report On-Screen UI** reader — run it IN PLAY MODE on the bad frame and it names every enabled `Graphic` covering ≥2% of the display, biggest first, with its path, its effective alpha and which `CanvasGroup` set it, plus the cameras and VideoPlayers that draw over the game without appearing in any UI hierarchy. **Reach for it the moment a report is about what is ON SCREEN**: a rendered frame is the one thing static analysis of scenes and prefabs cannot see, and three confident wrong answers were read out of that YAML before this tool named the object in one line. **Read before touching anything under `Assets/_Scripts/Editor/Diagnostics/`, `BugSignature`, or the `BugLedger/` store — and before wiring an auditor's findings into the ledger.** |
 | `TOOLING.md` | `Docs/` | **The editor-tooling convention.** One menu root (`FrogletTools/`), one auto-discovering board (Froglet Master Tool), one shared palette, and — for any tool that WRITES assets — the ship contract: record what you wrote, draw `FrogletToolShipPanel` (Validate & Push / Retire Tool), because a tool's output is the deliverable and it lands in the working tree, not the branch. **Read before adding ANY `[MenuItem]`** — a tool outside `FrogletTools/` is flagged as non-conforming by the board itself. |
 | `CODEX.md` | `Docs/` | **The in-game encyclopedia's data layer** — every **Ethirion** (the player-facing name for a crystal: Charge / Mass / Space / Time / Omni), all of **Ecology** (16 flora species, 6 fauna species) and every **Tool** (the player-facing name for a freestyle **Toy**), as ONE `CodexSO` at `Assets/Resources/Codex.asset` the runtime UI loads with no per-scene wiring. **An entry is a PAGE, not an asset**: one per species with its four elements as variants inside, one per element family, one per toy with the choices it offers inside — 33 pages over 88 lifeform configs, the crystal set and 6 toy definitions, because the player's question is "what is a Shark", not "what is a Shark Mass". A crystal's impactor class (elemental / omni / team) is deliberately absent: it decides who may collect one, which the palette already says in-world, and is mechanics rather than encyclopedia content. Authored by **FrogletTools > Interface > Codex**, whose load-bearing property is that **Scan & Merge is always safe to run** — a field-ownership contract splits every field into harvester-owned (wiring, harvested facts), filled-only-when-empty (name, image, accent) and never-touched (all prose, ordering, discovery, preview pose), so a generated encyclopedia still has room for a writer. Three findings generalise: **species are grouped by PREFAB, not by asset name** (the fauna set carries a `WormColonyFaunaConfig` beside four `Worm Colony <Element>` assets, so a name-prefix grouping invents a fifth species — the prefab is the thing the player meets, and the display name is a MAJORITY vote among the configs sharing it); a **stat is a formatted string, never a typed number**, because a codex row is prose and a typed value forces the UI to carry a formatter per stat kind; and an entry whose source asset vanished is reported as an **orphan and never auto-deleted**, since a tool that answers a mid-refactor by deleting hand-written body copy is one nobody runs twice. Images bake to `Assets/_Graphics/Codex/` off the prefab ASSET so nothing ever `Awake`s, with **alpha recovered from two opaque renders** (black and white, `a = 1 - (white - black)`) rather than trusting the render target's alpha channel, which is pipeline-dependent, and a coverage check that falls back to a lit silhouette rather than writing a blank PNG when a gameplay shader reading per-frame globals renders empty. **A FLORA IS ASKED TO DRAW ITSELF**: every flora prefab carries exactly ONE prism (the seed) because a plant is a growth RULE, not a model, so harvesting its meshes photographs a box - `Flora.TryPreviewGrowth` runs the rule in the abstract and `CellMiniatureBuilder.BuildFromLays` turns the poses into one mesh, the same answer the lava lamp's Lifeform bench reached (`FloraIconBuilder`), reached through the same two calls rather than a second copy. That required **`PhyllotacticFlora.TryPreviewGrowth`**, which the 8 Hesperides forms had been missing (so they were anonymous spheres on the bench too); it mirrors `SeedTips`/`DecideStep`/`DecideWhorl` with three substitutions and only three - a caller-seeded `System.Random` (the contract forbids touching `UnityEngine.Random`), a local claim list instead of `PrismSpatialIndex`, and a node that becomes a tip immediately because there are no frames - while `StemPrismScale` / `LeafPrismScale` stay the LIVE ones so a preview cannot drift from the plant on taper, cross-section or jitter. Two corollaries: **fauna are harvested normally** (unlike flora they ARE authored in place - a shark's wings and danger rods sit at real offsets), and **a COLONY's body is its MEMBERS** (the worm colony root carries no mesh and no nested instance, so the baker lays a chain of its head/body/tail prefabs at the colony's own spacing). **The TOOLS kingdom differs from the other two in two load-bearing ways.** (1) **A toy has no prefab** — it is built at runtime by `ToyFactory` from its `ToyDefinitionSO` — so its entry carries `SourceConfig` where the others carry `SourcePrefab` (`CodexEntry.HasSource` is the one orphan question both answer), and its portrait is **DRAWN** rather than photographed: `ToolPortraitBuilder` renders the toy's own `ToyEmblem` grammar (core + satellites inside the switch ring) off `ToyEmblem`'s published constants, so retuning the emblem retunes the portraits. Calling `ToyFactory`'s builders instead is wrong twice over in an editor pass — `AddSphereBody` discards its collider with `Object.Destroy` (illegal in edit mode, logs per bake) and `AddRingBody` attaches a live `ToyIdleSpin` plus an unowned static mesh — so the geometry is built and owned outright, per the rule that a bake wakes no gameplay component. (2) **Every toy declares a CATEGORY, and the categories are FUNDAMENTALS**: `ToyCategory` divides the toybox by what a toy CHANGES — **Pilot** (you: Vessel Changer, Domain Changer), **World** (where you are: Cell Selector, Wanderway), **Creation** (what it leaves behind: Connect the Dots, Lifeform Matrix) — which is the only division that stays true as toys are added, and a toy fitting none of them is the signal to have the fundamentals conversation rather than to widen the enum. `ToyDefinitionSO.Category` is **abstract and declared in code**, never serialized, because a toy's category is a property of what it DOES and an authored field can disagree with the behaviour under it; abstract means a new toy cannot be added without answering. It reaches the codex as `CodexEntry.Group`, a harvester-owned sub-heading WITHIN a kingdom (general — any kingdom that divides gets it, one that does not leaves it empty), carrying an ordering prefix (`1 · Pilot`) so the sections read lightest-touch-to-heaviest rather than alphabetically. Tool facts are read **per TYPE by pattern match, not by field name** — the opposite trade from the ecology probes and the right one, since a rename becomes a compile error — and the switch's default arm **warns**, so adding a toy without teaching the codex what it offers is noisy. `Tagline` moved from never-touched to filled-only-when-empty to carry a toy's own authored one-liner, which is safe by that tier's definition: a blank field has no human value to protect. **A VARIANT is drawn as a card in a grid under its entry, and most variant icons are NOT baked** — the governing question is "is this variant a distinct object?", and for most the answer is no: a species' ELEMENT resolves to that element's own ethirion image (one picture, not 123 copies of it), a DOMAIN draws its `AccentColor` as a chip (a PNG of a flat colour says nothing), and a KINGDOM row falls back to the entry's portrait. Only a PAINTING (its strokes, and the one place this codex colours by domain — there the domains ARE the subject) and a HULL bake art, ~24 icons instead of ~150. `CodexSO.VariantImage` is the single resolver and lives on the CATALOG rather than on `CodexEntry` because the element step is a CROSS-KINGDOM lookup — resolved at draw time, so re-baking one ethirion updates every lifeform that drops it with nothing to re-scan. Two hull traps: **five of the eight hulls are SKINNED**, so a `MeshFilter` walk finds nothing on any of them (icons go through `CodexImageBaker.HarvestModel`, which covers both vessel families, never `ToyModelBuilder`); and a hull bakes FLAT always, because the shared vessel graph is domain-tinted and reads per-frame globals, so the authored pass renders black and falls back anyway. Variant LABELS are disambiguated at the source (`Charge · <config>`) when a species carries several configs per element — not cosmetic, because the label is the key the merge matches on and `ToDictionary` throws on a duplicate; it had never fired only because no variant had ever carried an image. **Read before adding a crystal, a lifeform species or a TOY, or before building any UI that lists them.** |
 | `GAME_MODE_TOPBAR.md` | `Docs/` | **The game-mode top bar's CENTRE** — one prefab, eleven scenes. The centred score block is one row **divided into a column per domain**: team score (64px) over that team's player icons over a 3px accent. It retires the `Scoreboard.png` triangle+parallelogram plate, the per-player score card, the column backgrounds, and **every player NAME** (an icon already identifies a player, and a name under one avatar made that column a different height, so the row stopped reading as one divided block; the local player is marked by their chip taking the domain colour at full strength instead). **A column carries LIGHT, not a plate** — a team-tinted glow rising off the accent strip, which says whose column it is without adding an edge and, unlike a plate, can MOVE: it breathes continuously and punches on that team's score change, the punch PAUSING the breath rather than killing it so a run of scores cannot leave the light stuck at full. The glow sprite is authored by `Tools/Build/author_topbar_glow.py` (`--check`), which asserts it reaches zero at both side edges (so a row of columns can never seam) and is brightest at the BOTTOM — a PNG's row 0 is the top, so the intuitive way round yields an upside-down glow. It needs **no branch in the HUD**: both `AllyDomainContainer` and `OpposingDomainsContainer` resolve to the same transform in the single-bar layout, so the existing build order lays the columns out and a HUD wired the old way still works. **The LEFT is the goal stack** (§2), which retired both 90x90 ring clusters. `RoundTime` was never a clock: EVERY turn monitor raises `onUpdateTurnMonitorDisplay` with the metric **REMAINING**, so a timer face was drawn over an unlabelled objective count with no target — the row shows the same number with the two things the ring could not, *what you are counting and how many it takes* ("COLLECT CRYSTALS 18/30"). It adds **no plumbing**: the count rides the channel the ring was already on, while the glyph, the label and the target come from the mode's own `ScoringRuleSO` via `ObjectiveIconSetSO` — keyed on **`ScoringMetric`, never on the game mode**, so a new mode picking an existing metric gets a correct goal line for free and the row can never disagree with the condition that ends the turn. `TargetCount` stays `protected` (the extension point, overridden by all eleven rules) and `TargetFor` exposes the value. Both rings are **switched off, not deleted**, so `roundTimeDisplay`/`lifeFormCounter` stay valid and re-activating two GameObjects restores the old UI. **A monitor's payload cannot be told apart by looking at it** — `GetTimeToDisplay` returns a bare `"72"`, not `"1:12"`, so every monitor on that channel publishes an integer string and a row that decided by PARSING renders seconds as an objective count in Cellular Duel multiplayer, which has a time monitor *and* a scoring rule. The monitor declares it instead (`TurnMonitor.PublishesSecondsRemaining`, overridden true by `TimeBasedTurnMonitor`), the HUD resolves the scene's monitor once and passes the answer, and the six time-based scenes get a **clock row** formatted `m:ss` with no target and no bar. A count the stack cannot NAME draws nothing. **Authored into BOTH canvases** by `Tools/Build/author_goal_stack.py` (`--check`), because `GameCanvas-SkimRace.prefab` is a hard COPY and is the one **12** domain scenes instance against 10 for `CORE/GameCanvas.prefab` — authoring only the latter ships a feature invisible in every modern mode. **The plate is GENERATED, never sprited** — a trapezoid has no 9-slice (slanted edges do not tile), so a sprited one freezes the slant into the art and is crisp only at the size it was exported at; the first cut shipped a 112x36 PNG stretched to 312x48 and read exactly that blurry. It is a `TrapezoidGraphic`, the ability lockup's own component, behind the lockup's own `LockupBloom` — so the two surfaces are one product, and the shape is exact at any resolution. Two numbers that look arbitrary and are not: **the chamfer is authored in PIXELS and converted**, because the component takes widths as FRACTIONS of the rect and the lockup's `trapezoidInset 9` on a 104-wide card is 8.7% — on a bar three times as wide that same fraction is a parallelogram, so what carries over is the ANGLE, not the fraction; and **a 9-slice border is a constraint on the SMALLEST rect a sprite can be drawn into**, which is why the bloom's authored 48px border is scaled to ~29 by `m_PixelsPerUnitMultiplier 0.6` (at 1, two 48px borders leave a 4px middle in a 104px-tall glow and it reads as two blobs with a seam). **A row is sized to the widest LABEL it can be asked to show, and wrapping is OFF** — at 312 wide the 128-unit label box wrapped 6 of the 10 authored objectives (widest, `COLLECT OMNI CRYSTALS`, needs 186.3 units at font 16, measured off the shipped TTF), and *a wrapped label does not read as an overflow, it reads as two goals*; the row is 400 wide with a 196-unit label box and a 132-unit value column (`1997/2000` needs 124.6 and three modes run to 2000), and `author_goal_stack.py` asserts the fit against the shipped fonts and the shipped `ObjectiveIconSet.asset` before writing — wrapping-off and the assert go together, since the assert is what makes a loud overflow safe. **The whole top bar drops by ONE amount** (`TOP_BAR_DROP` 39, stack 13→52, centre block 3→42), sized by the left: `DiagnosticsHUD` builds its own `ConstantPixelSize` canvas and owns roughly the first 44 screen px, so the old 13-unit top margin sat *inside* the FPS panel. **A progress bar needs a BED**: at 0/30 a bare fill draws nothing, so the bar reads as missing rather than as empty and the first pickup makes a bar appear out of nowhere instead of moving one — track and fill share ONE derived rect, inset clear of the chamfer AT THE BAR'S OWN TOP EDGE (the slant is widest at the plate's bottom, which is exactly where the bar lives), asserted arithmetically before the generator writes anything. **Open gap: secondary goals have no producer** — a `ScoringRuleSO` names exactly one objective, so rows 2-3 ship inactive and `GoalStack.SetGoals` is the seam a mode-authored list plugs into. **Read before touching `MultiplayerHUD`, `DomainScorePanel`, the centred score block, or the top-left objective readout.** |
@@ -3416,6 +3416,40 @@ All game code lives under `CosmicShore.*` with 8 primary namespaces:
 - **Assigning `Slider.minValue` / `maxValue` / `wholeNumbers` to re-range a bound control.** Every one of those setters ends in `Set(m_Value, sendCallback: true)`, so NARROWING a window clamps the value the slider is carrying and **broadcasts the clamped result to every listener — including the persistent ones authored on the prefab**, which code cannot conveniently detach. When that persistent listener is the one that PERSISTS the setting, merely binding the control overwrites the player's saved value with the clamp. That is not hypothetical: the Music, SFX and Haptics rows in `OptionsMenuContent.prefab` shipped as copies of the FIELD-OF-VIEW slider (`60..90`, whole numbers, value 71), so binding them to the 0..1 audio window clamped 71 to 1, fired `onValueChanged(1)`, and `AudioLevelSlider.SetVolume` saved **full volume** over the player's level — and the panel then displayed the value it had just destroyed, on every launch, with a completely correct persistence layer underneath faithfully syncing the corruption to the cloud. Use `SliderRange.ApplyWithoutNotify(slider, min, max, wholeNumbers, value)`, which widens the window to cover both the carried and the incoming value, seats the value silently, then narrows — every assignment is a no-op clamp, so the callback cannot fire. General rule: **re-ranging a bound control is a WRITE, not a display change.** Record + regression test: `Docs/AudioSystem/FMOD_AUDIT.md §1.0`, `SliderRangeTests`
 - **Trusting an authored `ScrollRect` Content height, or growing it by an increment — and, once you do grow it, forgetting that its children may be anchored to a FRACTION of it.** A scrolling list that has never been scrolled to its end tells you nothing about whether it can be: a card below the reachable range is clipped by the viewport's `Mask`, which cuts the drawing off **and**, being an `ICanvasRaycastFilter`, eats the PRESS — while the scroll stops at the authored height, so a drag springs back. *Half a card, a scroll that snaps back and a dead button are ONE cause.* The arcade grid shipped exactly this the day it grew from 12 cards to 13 (`_Scripts/Controller/Arcade/SWITCHBACK.md`). **A ScrollRect's Content is a SCROLL EXTENT, not a layout frame**: in Menu_Main its `VerticalLayoutGroup` is `m_Enabled: 0` and the layout is pure ANCHORS, with two of its three children anchored to a fraction of its height (the game grid 0.625, the Maelstrom banner 0.239). So every unit added to Content stretched the grid by 0.625 — its bottom receded as fast as the content grew, which is why iterating to a "fixed point" could never reach it — and stretched the Maelstrom banner by 0.239, shipping a visibly too-tall card as the fix for the first bug. Pin every child of the Content to its TOP at the height it is currently drawing (`ArcadeExploreView.PinVerticalAnchorsToTop` — arithmetically a no-op, VERTICAL axis only, since a child may be deliberately anchored WIDER than the content), then measure with `RectTransformUtility.CalculateRelativeRectTransformBounds` once. Two details that are not polish: the needed height is `max(bounds.size.y, -bounds.min.y)` on a TOP-pivoted rect, and the fit must run **after** the cards are activated (the bounds call skips inactive objects). Two general rules, and the second is the one that cost a whole extra pass: **adding a card is adding a ROW, and a row is only reachable if the scroll content was measured after it**; and **a DISABLED component reads exactly like an enabled one** — a YAML dump of that layout group showed spacing, padding, force-expand and control-height, every field plausible and every one inert, so a whole diagnosis was built on a component that had never laid anything out. Check `m_Enabled` before reasoning from a component's fields. Because every failure here is silent, `ReportUnreachableCards` names any mode with no slot or past the content's height, and `ReportCardPressability` states the last card's interactable flag, listener count and what a real `EventSystem` raycast at its centre lands on
 
+- **A `RawImage` whose texture is missing draws a SOLID QUAD in its own tint, not nothing** — so a
+  frame or overlay whose art was deleted from the project stops being a frame and becomes an opaque
+  panel the size of its rect, and the ONLY evidence anywhere is the panel itself. `Pip.prefab`'s
+  `border` shipped this way: its `m_Texture` names guid `24ca4c74937a9ed4d9251952057575ab`, which no
+  asset carries any more, and its tint is (0.14, 0.29, 0.52) — so switching the picture-in-picture
+  panel on painted a 780x400 navy rectangle over **55% of the display**, with the actual 300x150
+  view sitting inside it, and it was reported as "the appshell UI with a render texture is covering
+  half my screen". **It is invisible to static analysis of scenes and prefabs**, because a rendered
+  frame is the one thing they cannot see: three separate reads of the prefab/scene YAML produced
+  three confident wrong answers before **FrogletTools > Diagnostics > Report On-Screen UI** (run in
+  play mode, on the bad frame) named the object in one line. Reach for that tool the moment a report
+  is about what is ON SCREEN. The guard is `PipUI.SilenceUntexturedGraphics` — a runtime statement
+  rather than an asset edit, deliberately: it NAMES the offender with its size and tint (an asset
+  edit cannot), it covers the next textureless graphic, and it undoes itself the day the art
+  returns, where switching the component off in the prefab would keep the frame dark forever with
+  nothing to say why. **It also went unseen for years because of WHICH HULLS carry the component**:
+  eight of eleven vessels have a `Pip` (Falcon, Grizzly, Manta, Serpent, Shrike, Squirrel, Termite,
+  Urchin) and Dolphin, Sparrow, Rhino and Scarab do not, so every recent arcade mode sidestepped it
+  by the hull it locks to and Hijack was simply the first mode locked to a vessel that has one.
+  *A defect fenced behind a per-prefab component is dormant, not absent — adding one consumer is
+  what ships it.*
+
+- **`AutoPilotEnabled` is FALSE on every vessel at `Start`, so it cannot stand in for "is this my
+  ship".** `AIPilot.ActivateAutopilot` runs later in the spawn chain than the vessel's own `Start`,
+  which makes the flag look like an ownership test on a human (whose autopilot really is off by the
+  time anyone looks) and makes it wrong for every AI hull. `Pip` gated on it and therefore switched
+  its camera on for EVERY Urchin in a Hijack match — two `FarViewCamera`s writing the one shared
+  `PipRenderTexture`, and an AI raising `IsActive` on a global SOAP channel that has no owner test.
+  Ownership for anything per-vessel comes from `VesselController.Initialize` **and** `ChangePlayer`
+  under `IPlayer.IsLocalPilot` — the same two sites and the same test the prism occlusion corridor,
+  the speed tunnel and the vessel vision band bind at. Where a shared surface (one render texture,
+  one HUD panel) can only have one owner, guard it by IDENTITY like those laws do, so a non-local
+  vessel bound in an arbitrary order cannot switch off what the local pilot just switched on.
+
 - `renderer.material` (clones material) — use `renderer.sharedMaterial` + MaterialPropertyBlock instead
 - **Expressing a "held / frozen / pinned / parked" gameplay state as `isKinematic` plus a per-tick position write.** It reads as the obvious way to say "this object is not moving right now", and it silently breaks every force in the game in BOTH directions: a force that acts by writing `rb.linearVelocity` (which is every AOE blast — `ApplyBlastServer`) writes into a body that does not integrate and evaporates, while a force that acts by writing POSITION (depenetration, an eject, a nudge) is undone by the pin on the next tick and the object visibly jitters. Neither failure logs anything. Prefer a state that is BOOKKEEPING over one that is a physics mode: leave the body live and let a flag suspend only the specific rule that must not apply (`AstroLeagueBall`'s seeded ball suspends containment and nothing else — SCARAB.md §4.6). Its companion: **a state transition that each force announces for itself is one the unwired force never announces** — have the object OBSERVE that it moved (`TickNucleusDepartureServer`) instead, so a force added tomorrow is covered with no wiring. And if a body must be able to receive a blast, consider that a body at REST sleeps, and a sleeping actor paired with a rigidbody-less growing trigger is not a pair a physics engine owes you an event for (`rb.sleepThreshold = 0`)
 - Swapping a prism's `MeshFilter` mesh (or its `MeshRenderer` materials) directly to restyle it — **prisms draw through the instanced companion entity (`PrismRenderService`), so a GameObject-local swap renders NOTHING**: the companion keeps drawing the plain box while your new mesh sits on a renderer that isn't drawing (exactly how the stellated super-shield first shipped invisible). Any per-prism visual override must hand rendering across explicitly: `Prism.SetRenderMeshOverride(sharedMesh)` + `SetExoticVisualActive(false)` for anything shareable (fetch it from the quantized-geometry caches — `OctahedronMeshGenerator.GetSharedShieldMesh` / `StellatedOctahedronMeshGenerator.GetSharedShieldMesh` — so same-size prisms batch as ONE mesh instead of a per-prism draw-call storm), `Prism.SetExoticVisualActive(true)` ONLY while genuinely showing per-prism-unique geometry, and `ClearRenderMeshOverride()` + `SetExoticVisualActive(false)` on the way back (including pool-return `OnDisable`). **Reach for a GPU morph over the shared mesh before you reach for unique geometry**: the shield engage/shatter morphs were the last holders of the `true` side and gave it up in 2026-08-15's B4 migration (`Docs/PRISM_ANIMATION.md` §4.8) by baking per-face centroids into TEXCOORD1 — the settled shared mesh became the morph mesh, and the animation kept its batch instead of minting a mesh (and a draw call) per prism. Nothing in the project drives `SetExoticVisualActive(true)` today. `PrismOctahedronShield` and `PrismStellatedOctahedronShield` are still the reference implementations of the handoff. **Two corollaries an exotic visual must respect** (`Docs/PRISM_ANIMATION.md` §4.5, learned the hard way from §3.8 #10): (1) taking over *rendering* must never suppress companion-entity *creation* — clock stamps are one-shot, so a prism with no entity at the instant it is stamped loses that animation permanently; entity existence and entity visibility are separate concerns, and the transient morph mesh must never be registered with Entities Graphics (it mints a `BatchMeshID` per prism) — read the batchable geometry from `Prism.EffectiveRenderMesh()`/`SyncRenderMesh()`; (2) a visual state applied while `!Prism.IsCreationComplete` is part of the prism's BIRTH, not a transition on live mass — it snaps (`PrismStateManager.IsBirthTransition`), because the grow-in bloom already carries continuity of existence and a morph there is invisible by construction while costing draw calls, per-frame mesh rebuilds, and one SFX per prism laid
diff --git a/Docs/DIAGNOSTICS.md b/Docs/DIAGNOSTICS.md
index b25acd4ff..cf121278e 100644
--- a/Docs/DIAGNOSTICS.md
+++ b/Docs/DIAGNOSTICS.md
@@ -1,14 +1,39 @@
-# Diagnostics — the crash detector, the shared bug ledger, and compile timing
+# Diagnostics — the crash detector, the shared bug ledger, compile timing, and the on-screen UI report
 
-**FrogletTools ▸ Diagnostics** is one window with four tabs:
+**FrogletTools ▸ Diagnostics** is one window with four tabs, plus one standalone report:
 
 | Tool | Question it answers | Data it writes |
 |---|---|---|
 | **Crash Detector** | *Why did my editor die?* — even when Unity itself never got to say | `Logs/CrashDetector/` (machine-local, gitignored) |
 | **Bug Ledger** (+ its **Stage & Push** tab) | *What is broken right now, and is the fix actually proven?* | `BugLedger/local/` (gitignored live store) → published to the tracked `BugLedger/shared/` only through the Stage & Push tab |
 | **Compile Timing** | *What does an edit cost?* — compile + domain-reload seconds, and which assemblies rebuilt | `Logs/CompileTiming/` (machine-local, gitignored) |
+| **Report On-Screen UI** (menu item, not a tab) | *What is that thing covering my screen?* — every enabled `Graphic` ≥2% of the display, biggest first | nothing — console + clipboard |
+
+## Report On-Screen UI
+
+**Run it in play mode, on the bad frame.** "There is UI in the way" is a report about a RENDERED
+FRAME, and a rendered frame is the one thing static analysis of scenes and prefabs cannot see: a
+panel is on screen because of the product of several things living in different files — is its
+GameObject active, is its `Graphic` enabled, what did every `CanvasGroup` above it multiply its
+alpha by, which `Canvas` draws it and in what order, and did some controller leave it that way.
+Reading any one of those in isolation invites a confident wrong answer; three of those in a row is
```

</details>

### `87fb61fae` — docs(skills): the shared line a conflict splits on can be a whole statement

_Claude, 2026-09-07 17:00:19 +0000_

```text
The ship skill's keep-both trap tells you to look for a dangling `&&`, `+`,
`,` or `?` at the end of a hunk. That test caught two of the three instances
this branch's merge produced and structurally cannot catch the third: a
conflict is bounded by the lines both sides SHARE, so when two branches each
add a member ending in the same closing line, git puts that line outside the
hunk and keeping both bodies hands it to whichever side you place last.

Here that was a Python `return m` shared by two generator functions - the
first silently returned None and the icon generator crashed on the next
--check - and a `/// <summary>` shared by two enum members, which is not a
syntax error in C# at all. The check that would have caught both is to read
the line AFTER the hunk and ask which side it belongs to.
```

```text
 .claude/skills/ship/SKILL.md | 10 ++++++++++
 1 file changed, 10 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index 9a80e9df8..401fdf6f4 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -66,6 +66,16 @@ run the `/reorient` skill first and act on its verdict before shipping.
   call silently gaining a third parameter). Compile after every keep-both resolution — and treat
   any conflict hunk whose last non-blank character is `&&`, `+`, `,` or `?` as one needing a
   hand-joined merge, not a concatenation.
+  **The dangling-operator test is necessary and NOT sufficient: the shared line the conflict split
+  on can be a COMPLETE statement.** A conflict is bounded by the lines both sides share, so when
+  two branches each add a function/member ending in the same closing line, git puts that line
+  OUTSIDE the hunk and keeping both bodies gives it to whichever side you place last — the other
+  falls off its own end. Three landed in one merge and only two had a dangling operator: a Python
+  `return m` shared by two generator functions (so `switches_threaded()` silently returned `None`
+  and the icon generator crashed), and a `/// <summary>` shared by two enum members (so the second
+  member's doc comment lost its opening tag). Neither is a syntax error in C#, and the Python one
+  is not caught by any parse. After every keep-both resolution, read the line immediately AFTER
+  the hunk and ask which side it belongs to — if the answer is "both", duplicate it.
   **The same trap exists in hand-authored YAML, and it is invisible to a compiler.** A Unity
   `EditorBuildSettings.asset`-style list item is multiple lines wide (`- enabled: 1` /
   `path: ...` / `guid: ...`), so when two branches both append a new scene entry after the SAME
```

</details>

### `3aa0d44a7` — docs(arcade): re-scope Drumfire against Switchback, and record what the merge taught

_Claude, 2026-09-07 17:08:20 +0000_

```text
Switchback landed in parallel and is also a Dolphin-only teaching mode, so both
docs now say which half of the hull each one teaches: Switchback the FLYING
(skim, drift, boost, no target at all), Drumfire the WEAPON. Neither substitutes
for the other, and a reader landing on one should not have to discover the other.

DRUMFIRE.md also carries a note on the renumber, because its own git history
shows mode 45 and metric 9, and a follow-ups section naming four open items -
the missing preview definition (shared with Salvo and Switchback, so it wants
one pass over all three), the un-playtested lane band, the absent toasts, and
why the mode is deliberately out of the Maelstrom pool.

Skill capture - four traps this merge paid for, none previously written down:

- An enum collision can arrive by a member LOSING its explicit value, not only
  by two branches authoring the same literal. The "never reorder" comment
  cannot prevent that; only a duplicate-value check over the whole enum sees it.
- Two branches appending the IDENTICAL list line is deduplicated to one, so one
  branch's entry vanishes with no conflict and a file that still parses. The
  mirror of the mis-concatenation trap already recorded.
- A conflict between two function BODIES shares the hunk's trailing lines, so
  ordering them strips the `return` from whichever goes first.
- A hand-written mock of a wide interface is what a parallel branch breaks, and
  it takes the editor assembly down for everyone.

And one technique into asset-surgery: compile a merge-resolved file ALONE with
no stubs and treat only CS1xxx as real - missing-type errors mean nothing once
you have removed its world, but nothing except a structural break parses wrong.
Negative-controlled, because a gate nobody has watched fail is not a gate.
```

```text
 .claude/skills/asset-surgery/SKILL.md         | 30 ++++++++++++++++++++++++++++++
 .claude/skills/ship/SKILL.md                  | 27 +++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Arcade/DRUMFIRE.md | 34 +++++++++++++++++++++++++++++++++-
 CLAUDE.md                                     | 12 +++++++++++-
 4 files changed, 101 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 171 lines)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index d79dfb951..069206e86 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -1360,6 +1360,36 @@ off any longer symbol that starts with the same name. One session added a delibe
 thing separating them — so the sibling got its own test asserting it is NOT counted, which is what
 fails if someone later renames the primary to a prefix of something else.
 
+### Technique: SYNTAX-only compile — prove a file parses without stubbing its whole world
+
+A merge-resolved file usually cannot be compiled: it reaches into fifty Unity types you would
+have to stub. But the failure a merge introduces is almost always STRUCTURAL — two statements
+where one expression belonged, a lost `return`, an argument list that gained a member — and
+structure is exactly what the parser sees before it ever needs a type.
+
+So compile the file **alone, with no stubs at all**, and classify the errors:
+
+```sh
+dotnet build -p:TARGET=/abs/path/File.cs 2>&1 | grep -oE "error CS[0-9]{4}"
+```
+
+- `CS0246` / `CS0103` / `CS0234` / `CS1061` / `CS0117` / `CS0535` — *missing type or member*.
+  Expected, and means nothing: you removed its world.
+- **`CS1xxx` — a SYNTAX error.** `CS1002` (`;` expected), `CS1003`, `CS1519`, `CS1525`, `CS1513`.
+  Nothing but a genuine structural break produces these, so any hit is a real defect.
+
+One `.csproj` with `<Compile Include="$(TARGET)" />` and `EnableDefaultCompileItems=false`
+serves every file, so this is a loop over the whole changed set rather than a project per file.
+It caught nothing here — but only because it was negative-controlled first: injecting the exact
+defect a bad keep-both produces (splitting an `&&` chain into two statements) raised `CS1003`,
+and the restored file went back to zero. **A gate you have not watched fail is not a gate.**
+
+Two limits worth knowing. It cannot see semantic breaks — a `+` chain that gained a third
+ARGUMENT is well-formed C#, so for those extract the one method and compile it for real against
+tiny stubs (`DescribeBuildValues` compiled and RAN in about thirty lines of stub, and printing
+its output proved both modes landed on their own lines). And a whole-file `#if` still makes the
+compile see nothing, per the trap below.
+
 ### Trap: a stub-harness error is a STUB GAP until proven otherwise — but not always
 
 Running the shipped file against transcribed stubs means every compile error has two possible
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index 9a80e9df8..6b40b4713 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -59,6 +59,33 @@ run the `/reorient` skill first and act on its verdict before shipping.
   the toast config's `gameMode:`/`situation:`/`resetOnSituation:` ids. Code that switches on the
   enum by NAME needs no change, which is exactly why the stale numbers hide in assets. Verify with
   a duplicate-value check over the whole enum, not just your own rows.
+  **Two ways that collision actually arrives, and the second is the one review misses.** The
+  obvious one is both sides authoring the same literal. The subtle one is a member LOSING its
+  explicit value in the resolution and silently taking the next IMPLICIT one: keep-both on
+  `VolumeDestroyed = 9` / `SwitchesThreaded = 9` produced an enum where mine had no `= N` at all,
+  so it took `CombatPoints + 1` and collided with a `Jousts = 7` neither branch had touched. The
+  enum's own "never reorder, only append" comment cannot prevent this — nothing was reordered by
+  hand — so the duplicate-value check is the ONLY thing that catches it. Run it over the whole
+  enum, and separately assert every member still carries an explicit value if the enum requires
+  them.
+  **Two branches appending the IDENTICAL line to a list is silently deduplicated to one.** The
+  YAML trap above is about mis-concatenation; this is its mirror, omission by identity. Both
+  branches added `  - 45` to `ProgressionConfig`'s unlock list after the same anchor, git saw one
+  change, kept one line, and one mode's entry simply was not there — no conflict, no marker, and
+  the file still parses. Any list where two branches append a value derived from the same "next
+  free number" is exposed: count the entries against what both sides should SUM to, and check
+  your own value is present by name, not just that the list grew.
+  **A conflict whose two sides are two function BODIES shares the hunk's trailing lines.** Git
+  ends the hunk at the last differing line, so a common tail — a `return`, a closing call, a
+  `}` — belongs to whichever body you put LAST. Ordering the two bodies therefore silently
+  strips it from the other: `switches_threaded()` lost its `return m` and the generator crashed
+  with `'NoneType' object is not subscriptable` several frames away from the cause. After any
+  keep-both of two callables, check each one still ends the way it did on its own branch — or
+  just run the thing, which is what caught it here.
+  **A test mock of a wide interface is what a parallel branch breaks.** `IRoundStats` gained two
+  members, so every hand-written `IRoundStats` mock stopped compiling — and a broken test mock
+  takes `Assembly-CSharp-Editor` down for everyone, which no gameplay compile-check would show.
+  Grep for other implementers of any interface the base branch widened.
 - **"Keep both sides" is right for list entries and WRONG inside a chain.** Resolving conflicts by
   concatenating HEAD and theirs works for independent fields, list items and doc paragraphs. It
   produces invalid code when both sides are links in one expression: two halves of a `&&` chain
diff --git a/Assets/_Scripts/Controller/Arcade/DRUMFIRE.md b/Assets/_Scripts/Controller/Arcade/DRUMFIRE.md
```

</details>

### `ac26243f1` — fix(tests): implement SwitchesThreaded on SparrowCombatTierTests fake

_Claude, 2026-09-07 17:14:33 +0000_

```text
`IRoundStats` gained `SwitchesThreaded` / `OnSwitchesThreadedChanged` with the
Switchback gate race, and two of the three test fakes were updated
(`IRoundStatsCleanupTests.TestRoundStats`, `GameDataSOTests.MockRoundStats`);
`SparrowCombatTierTests.FakeRoundStats` was missed, so `Assembly-CSharp-Editor`
failed to compile with CS0535 on both members.

Members are declared in the interface's own ordering (event after
`OnCombatPointsChanged`, property after `CombatPoints`) so the fake stays a
readable mirror of the interface.

Verified by compiling the shipped `IRoundStats.cs` plus all three fakes
extracted verbatim in a standalone Roslyn harness: 0 errors, and a negative
control that strips the two members back out reproduces exactly the two
reported CS0535s.
```

```text
 Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs | 2 ++
 1 file changed, 2 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs b/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
index d692bcc64..fe6337d78 100644
--- a/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
+++ b/Assets/_Scripts/Tests/Editor/SparrowCombatTierTests.cs
@@ -535,6 +535,7 @@ namespace CosmicShore.Tests
             public event Action<IRoundStats> OnMissileHitsLandedChanged;
             public event Action<IRoundStats> OnDebuffHitsLandedChanged;
             public event Action<IRoundStats> OnCombatPointsChanged;
+            public event Action<IRoundStats> OnSwitchesThreadedChanged;
             public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
             public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
             public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
@@ -576,6 +577,7 @@ namespace CosmicShore.Tests
             public int MissileHitsLanded { get; set; }
             public int DebuffHitsLanded { get; set; }
             public int CombatPoints { get; set; }
+            public int SwitchesThreaded { get; set; }
             public float FullSpeedStraightAbilityActiveTime { get; set; }
             public float RightStickAbilityActiveTime { get; set; }
             public float LeftStickAbilityActiveTime { get; set; }
```

</details>

### `87c6f425b` — dolphin audio added + tweeks to squirell drift behavior

_aradia1, 2026-09-07 14:09:44 -0400_

```text
 Assets/_Graphics/RenderTextures/PipRenderTexture.renderTexture        |    6 +-
 Assets/_Prefabs/Spacevessels/Dolphin.prefab                           | 1948 +++++++++++++++++--------------
 Assets/_Prefabs/Spacevessels/Squirrel.prefab                          |  197 ++--
 .../Metadata/Event/{10e1c214-2d0b-4b6f-a94e-759c9828221f}.xml         |  472 ++++++++
 Cosmic Shore/.unsaved/metadataFileMapping.data                        |  Bin 0 -> 530 bytes
 Cosmic Shore/Build/Desktop/Master.strings.bank                        |  Bin 4472 -> 4560 bytes
 Cosmic Shore/Build/Desktop/SFX.bank                                   |  Bin 39567072 -> 39571488 bytes
 .../Metadata/Event/{10e1c214-2d0b-4b6f-a94e-759c9828221f}.xml         |   21 -
 .../Metadata/Event/{918db840-9507-41b3-be4b-11a90406838f}.xml         |  492 ++++++++
 .../Metadata/Event/{e5eaad0e-b631-4200-8e2f-2b745b9c78e2}.xml         |  134 +++
 10 files changed, 2275 insertions(+), 995 deletions(-)
```

### `ecdac65aa` — mixing fixes

_aradia1, 2026-09-07 14:48:07 -0400_

```text
 Assets/_Graphics/Design Assests/FX/fx_arclightning.mat                |    1 +
 Assets/_Prefabs/Spacevessels/Sparrow.prefab                           | 2179 ++++++++++++++++---------------
 .../Metadata/Event/{51c68861-7cb5-43a8-8611-02c5d84a4267}.xml         |  492 +++++++
 .../Metadata/Event/{e5eaad0e-b631-4200-8e2f-2b745b9c78e2}.xml         |  249 ++++
 Cosmic Shore/.unsaved/metadataFileMapping.data                        |  Bin 530 -> 1582 bytes
 Cosmic Shore/Build/Desktop/Master.strings.bank                        |  Bin 4560 -> 4610 bytes
 Cosmic Shore/Build/Desktop/SFX.bank                                   |  Bin 39571488 -> 39576000 bytes
 7 files changed, 1853 insertions(+), 1068 deletions(-)
```

### `900357570` — docs(qa): Garrett session results, 2026-09-07

_Garrett Milliron, 2026-09-07 16:35:41 -0400_

```text
 Docs/QA/.applied.json                 |  5 +++++
 Docs/QA/RESULTS/2026-09-07-garrett.md | 46 ++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 51 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/.applied.json b/Docs/QA/.applied.json
index 5778bad71..b7f06875f 100644
--- a/Docs/QA/.applied.json
+++ b/Docs/QA/.applied.json
@@ -11,6 +11,11 @@
           "date": "2026-08-14"
         }
       }
+    },
+    "2026-09-07-garrett.md": {
+      "submitted_hash": "4d3caabcdc23e14e1b08b3bacb208335795906ece319c7fe4752683193dfc31b",
+      "applied_hash": null,
+      "items": {}
     }
   }
 }
diff --git a/Docs/QA/RESULTS/2026-09-07-garrett.md b/Docs/QA/RESULTS/2026-09-07-garrett.md
new file mode 100644
index 000000000..a3490482f
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-09-07-garrett.md
@@ -0,0 +1,46 @@
+# QA session results
+
+**Nothing here is published until you press Submit.** Add rows across as many days
+as you like and submit whenever you want what you have so far to reach engineering;
+only new verdicts are published each time. **A published verdict is frozen — a
+retest is a NEW session file, never an edit.** Guide: `Docs/QA/README.md`.
+
+## Session
+
+| Field | Value |
+|---|---|
+| Tester | Garrett |
+| Date | 2026-09-07 |
+| Branch | bleeding-edge |
+| Commit | `ecdac65aa` |
+| Unity version | 6000.3.17f1 |
+| Platform(s) | Editor (WindowsEditor) |
+| Submitted | yes — 2026-09-07 16:35 · 1 verdict(s), 1 new this run |
+## Results
+
+<!-- qa-results-table -->
+
+| ID | Result | Notes |
+|---|---|---|
+| QA-SPEED-TUNNEL | PASS |  |
+
+<!-- /qa-results-table -->
+
+---
+
+## Working notes
+
+*(Scratch space. Only the table above is read — write freely here as you go; it
+becomes the dev task's detail if an item fails.)*
+
+
+## Evidence
+
+Files live in `Docs/QA/RESULTS/evidence/2026-09-07-garrett/` and are referenced from the Notes
+cell of the row they belong to.
+
+## Anything else
+
+*(Feel, tuning opinions, and anything that looked wrong but was not on the list.
+These change no item's status, but they are read when the backlog is regenerated
+and can become new items.)*
```

</details>

_Also contains 12 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
