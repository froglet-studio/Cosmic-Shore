# Branch archive: `claude/connect-dots-arcade-game-xtu44w`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Fake Artist arcade game mode**

A complete new arcade game mode, 'Fake Artist' (mode 39), modeled on the party game: players draw a shared picture with their vessel trails, one of them is a secret faker, and everyone votes. It includes the minigame core, vote scoring, a gallery view, a per-player winner display for free-for-all modes, replicated pen-up trail state, an arcade card, scene and assets, a setup tool, tests and design docs.

- **Status:** Unique work
- **Areas:** Game modes (Fake Artist), Arcade cards, Vessel trails, Scoring/End conditions, Editor tools
- **Already in bleeding-edge:** None found. No FakeArtist files or commits exist in bleeding-edge, and GameModes id 39 has since been taken by Cleave.
- **Risk if deleted:** high
- **Suggestion (2026-10-08):** keep — This is a complete, tested game mode found nowhere else; reviving it would need a new mode id.

## Evidence

- **Last commit:** 2026-07-23 by Claude
- **Unmerged commits:** 13
- **Forked from:** `6827d8a81` (2026-07-22, Merge pull request #617 from froglet-studio/claude/skimmers-shielded-prisms-he)
- **Tip:** `b825fdfa2`
- **Files touched (54):**
  - `Assets/Resources/EndConditionOverrides.asset`
  - `Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameFakeArtist.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameFakeArtist.asset.meta`
  - `Assets/_SO_Assets/Games/FakeArtistConfig.asset`
  - `Assets/_SO_Assets/Games/FakeArtistConfig.asset.meta`
  - `Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset`
  - `Assets/_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset`
  - `Assets/_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset.meta`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity.meta`
  - `Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs`
  - `Assets/_Scripts/Controller/Arcade/FAKEARTIST.md`
  - `Assets/_Scripts/Controller/Arcade/FAKEARTIST.md.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistBrushes.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistBrushes.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistConfigSO.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistConfigSO.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistGalleryCam.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistGalleryCam.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistRevealGhost.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistRevealGhost.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistScorer.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistScorer.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistVotePanel.cs`
  - `Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistVotePanel.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/Scoring/FakeArtistScoringRuleSO.cs`
  - `Assets/_Scripts/Controller/Arcade/Scoring/FakeArtistScoringRuleSO.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/FakeArtistTurnMonitor.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/FakeArtistTurnMonitor.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/VesselController.cs`
  - `Assets/_Scripts/Controller/Vessel/VesselPrismController.cs`
  - … and 14 more

### `4b2c8d8db` — feat(vessel): replicate trail pen-up state to all peers

_Claude, 2026-07-22 07:52:22 +0000_

```text
Why: trails are per-peer simulations driven by replicated kinematics, but
SetSpawnerPaused was a local flag - remote peers kept laying prisms while
the owner held the pen up (painting-toy stroke gaps desync on party
clients; fatal for stroke-based modes where all peers must see the same
drawing).

What: owner-write n_TrailPenUp NetworkVariable on VesselController mirrors
VesselPrismController.IsSpawnerPaused each frame; non-owner peers apply it
via SetSpawnerPaused (plus a catch-up read on subscribe). Also exposes
SetTrailShielded for per-player trail-identity modes.
```

```text
 Assets/_Scripts/Controller/Vessel/VesselController.cs      | 17 +++++++++++++++--
 Assets/_Scripts/Controller/Vessel/VesselPrismController.cs | 11 +++++++++++
 2 files changed, 26 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/VesselController.cs b/Assets/_Scripts/Controller/Vessel/VesselController.cs
index 3943712d9..f8b0a9ded 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselController.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselController.cs
@@ -40,6 +40,12 @@ namespace CosmicShore.Gameplay
         readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);
         readonly NetworkVariable<bool> n_IsTranslationRestricted =
             new(writePerm: NetworkVariableWritePermission.Owner);
+        // Trail pen-up (VesselPrismController.SetSpawnerPaused) is a LOCAL flag, but every
+        // peer simulates every vessel's trail from replicated kinematics - without this
+        // mirror, a remote peer keeps laying prisms while the owner holds the pen up
+        // (visible today as painting-toy stroke gaps missing on party clients, and fatal
+        // for the Fake Artist minigame where all peers must see the same strokes).
+        readonly NetworkVariable<bool> n_TrailPenUp = new(writePerm: NetworkVariableWritePermission.Owner);
         
         public ulong PlayerNetId { get; private set; }
         public ulong VesselNetId => NetworkObjectId;
@@ -85,6 +91,7 @@ namespace CosmicShore.Gameplay
                 n_Speed.Value = VesselStatus.Speed;
                 n_Course.Value = VesselStatus.Course;
                 n_BlockRotation.Value = VesselStatus.blockRotation;
+                n_TrailPenUp.Value = VesselStatus.VesselPrismController.IsSpawnerPaused;
                 CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountNetVarDirty(3);
             }
         }
@@ -356,21 +363,27 @@ namespace CosmicShore.Gameplay
         void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => VesselStatus.Course = newValue;
         void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => VesselStatus.blockRotation = newValue;
         void OnIsTranslationRestrictedValueChanged(bool previousValue, bool newValue) => VesselStatus.IsTranslationRestricted = newValue;
-        
+        void OnTrailPenUpChanged(bool previousValue, bool newValue) => VesselStatus.VesselPrismController.SetSpawnerPaused(newValue);
+
         void SubscribeToNetworkVariables()
         {
             n_Speed.OnValueChanged += OnSpeedChanged;
             n_Course.OnValueChanged += OnCourseChanged;
             n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
             n_IsTranslationRestricted.OnValueChanged += OnIsTranslationRestrictedValueChanged;
+            n_TrailPenUp.OnValueChanged += OnTrailPenUpChanged;
+            // Late-join/pair-swap catch-up: apply the current replicated pen state (the
+            // OnValueChanged callback only fires on future deltas).
+            VesselStatus.VesselPrismController.SetSpawnerPaused(n_TrailPenUp.Value);
         }
-        
+
         void UnsubscribeFromNetworkVariables()
         {
             n_Speed.OnValueChanged -= OnSpeedChanged;
             n_Course.OnValueChanged -= OnCourseChanged;
             n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
             n_IsTranslationRestricted.OnValueChanged -= OnIsTranslationRestrictedValueChanged;
+            n_TrailPenUp.OnValueChanged -= OnTrailPenUpChanged;
         }
         
         void ToggleStationaryMode(bool enable) =>
diff --git a/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs b/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs
index 109c3e4e8..1a691459d 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselPrismController.cs
@@ -149,6 +149,17 @@ namespace CosmicShore.Gameplay
         /// </summary>
         public void SetSpawnerPaused(bool paused) => trailPenUp = paused;
 
+        /// <summary>Current pen state (mirrored to peers by <see cref="VesselController"/>'s n_TrailPenUp).</summary>
+        public bool IsSpawnerPaused => trailPenUp;
+
+        /// <summary>
+        /// Whole-trail shield override (the prefab-authored <see cref="shielded"/> flag, exposed for
+        /// modes that assign per-player trail identities - e.g. Fake Artist's shielded brushes).
+        /// Rides the existing pre-Initialize flag path so prisms arrive shielded at birth.
+        /// Set it identically on every peer's copy of the vessel (trails are per-peer simulations).
+        /// </summary>
+        public void SetTrailShielded(bool value) => shielded = value;
+
         public void ToggleBlockWaitTime(bool extended)
         {
             waitTime = extended ? defaultWaitTime * 3f : defaultWaitTime;
```

</details>

### `1ad7247de` — feat(arcade): per-player winner surfaces for free-for-all modes + FakeArtist enum

_Claude, 2026-07-22 07:52:22 +0000_

```text
Why: EndGameSequencer's win check and the Scoreboard banner are
domain-only; in a free-for-all every player sharing the winner's domain
saw VICTORY. New FFA modes need an individual-player winner path.

What: ScoringRuleSO.UsesPerPlayerWinner virtual (default false);
EndGameSequencer.DidLocalPlayerWin compares WinnerName when set;
Scoreboard shows a '{NAME} WINS' banner via new SetBannerForPlayer.
Adds GameModes.FakeArtist = 39 (IDs 7/31 stay reserved).
```

```text
 Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs |  8 ++++++++
 Assets/_Scripts/Data/Enums/GameModes.cs                    |  6 ++++++
 Assets/_Scripts/UI/Scoreboard.cs                           | 30 ++++++++++++++++++++++++++----
 Assets/_Scripts/Utility/DataContainers/EndGameSequencer.cs |  6 ++++++
 4 files changed, 46 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 101 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs b/Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs
index ae5f51e08..03d5dd0b5 100644
--- a/Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs
+++ b/Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs
@@ -28,6 +28,14 @@ namespace CosmicShore.Gameplay
         public ScoringMetric Metric => metric;
         public bool GolfRules => golfRules;
 
+        /// <summary>
+        /// True for free-for-all modes where the winner is an individual PLAYER
+        /// (<see cref="GameDataSO.WinnerName"/>), not a domain - shared end-game surfaces
+        /// (EndGameSequencer's win check, the Scoreboard banner) branch on this instead of
+        /// comparing domains, because in FFA multiple players can share a domain.
+        /// </summary>
+        public virtual bool UsesPerPlayerWinner => false;
+
         /// <summary>The metric value for one player - what the HUD card shows.</summary>
         public int LiveMetric(IRoundStats stats) => ScoringMetrics.Read(stats, metric);
 
diff --git a/Assets/_Scripts/Data/Enums/GameModes.cs b/Assets/_Scripts/Data/Enums/GameModes.cs
index 58f5df474..d53557624 100644
--- a/Assets/_Scripts/Data/Enums/GameModes.cs
+++ b/Assets/_Scripts/Data/Enums/GameModes.cs
@@ -57,5 +57,11 @@ namespace CosmicShore.Data
         // scores a point; first domain to the wave target (default 3) wins. See
         // _Scripts/Controller/Arcade/NUCLEUSRUSH.md.
         NucleusRush = 38,
+        // FakeArtist (39): free-for-all social-deduction painting minigame (3-12
+        // players) built on the Connect-the-Dots painting toy. Everyone draws
+        // assigned strokes of a secret artwork; one player (the fake artist) knows
+        // the subject but gets no ring guides. Vote, score, repeat - first to the
+        // point target (default 8) wins. See _Scripts/Controller/Arcade/FAKEARTIST.md.
+        FakeArtist = 39,
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/UI/Scoreboard.cs b/Assets/_Scripts/UI/Scoreboard.cs
index 63fa81e2d..8cc3837db 100644
--- a/Assets/_Scripts/UI/Scoreboard.cs
+++ b/Assets/_Scripts/UI/Scoreboard.cs
@@ -298,10 +298,21 @@ namespace CosmicShore.UI
             // produce results (freestyle, DuelForCell) fall back to the legacy path.
             if (gameData.Results is { Count: > 0 })
             {
-                var winnerDomain = gameData.WinnerDomain != Domains.Blue
-                    ? gameData.WinnerDomain
-                    : gameData.Results[0].Domain;
-                SetBannerForDomain(winnerDomain);
+                // Free-for-all modes crown an individual player, not a domain - the
+                // "{DOMAIN} VICTORY" banner would be wrong (and FFA brush palettes may
+                // use domains outside the labeled four).
+                if (gameData.ScoringRule != null && gameData.ScoringRule.UsesPerPlayerWinner
+                    && !string.IsNullOrEmpty(gameData.WinnerName))
+                {
+                    SetBannerForPlayer(gameData.WinnerName, gameData.WinnerDomain);
+                }
+                else
+                {
+                    var winnerDomain = gameData.WinnerDomain != Domains.Blue
+                        ? gameData.WinnerDomain
+                        : gameData.Results[0].Domain;
+                    SetBannerForDomain(winnerDomain);
+                }
                 PopulateFromResults(gameData.Results);
                 return;
             }
@@ -378,6 +389,17 @@ namespace CosmicShore.UI
             }
         }
 
+        /// <summary>
+        /// Free-for-all banner: names the individual winner. Tinted by the winner's
+        /// domain when the palette knows it (unknown/synthetic domains render gray via
+        /// GetDomainColor's fallback).
+        /// </summary>
+        protected virtual void SetBannerForPlayer(string winnerName, Domains winnerDomain)
+        {
+            if (BannerImage) BannerImage.color = GetDomainColor(winnerDomain);
+            if (BannerText) BannerText.text = $"{winnerName.ToUpper()} WINS";
+        }
```

</details>

### `23c6dfb26` — feat(arcade): Fake Artist minigame core (mode 39)

_Claude, 2026-07-22 08:09:45 +0000_

```text
Free-for-all social-deduction painting party game (3-12 players) built on
the Connect-the-Dots painting toolkit: server generates a parametric
variation of a preset artwork each round, deals 3 strokes per player via
targeted ClientRpcs (secrets stay per-client), one fake artist knows the
subject but gets start/end dots only; simultaneous drawing, two-question
vote, config-driven scoring (+1/+1/-1/+4), first to the win target takes
the gallery. 12 trail identities = 6 paint colors (4 real domains + minted
fire/lime synthetic material sets) x normal/shielded prisms.

New: FakeArtistController (round phase machine, brush table, AI painters +
AI ballots), FakeArtistBrushes, FakeArtistArtworkBuilder (deterministic
variation + repartition + deal), FakeArtistStrokeGuide (client-side ring
guides, zero colliders), FakeArtistRevealGhost, FakeArtistVotePanel
(runtime-built overlay UI), FakeArtistTurnMonitor, FakeArtistScoringRuleSO
(per-player rule), FakeArtistScorer (pure tally), FakeArtistConfigSO.
Touchpoints: ElementalComebackSystem source case, MiniGameHUD objective
relay case.
```

```text
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs          |   3 +
 .../_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs | 302 +++++++++++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistBrushes.cs     | 304 +++++++++++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistConfigSO.cs    |  59 +++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs  | 880 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistRevealGhost.cs | 107 ++++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistScorer.cs      |  99 ++++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs | 326 ++++++++++++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistVotePanel.cs   | 354 +++++++++++++
 Assets/_Scripts/Controller/Arcade/Scoring/FakeArtistScoringRuleSO.cs  | 104 ++++
 .../_Scripts/Controller/Arcade/TurnMonitors/FakeArtistTurnMonitor.cs  |  89 ++++
 Assets/_Scripts/UI/MiniGameHUD.cs                                     |   4 +
 12 files changed, 2631 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 2713 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index 0174ae9a3..9557a6915 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -75,6 +75,9 @@ namespace CosmicShore.Gameplay
                 case GameModes.Rampage: // Score lands only at game end - destruction is the live stat
                     system.differenceSource = ScoreDifferenceSource.PrismsDestroyed;
                     break;
+                case GameModes.FakeArtist: // Score lands only at game end - points live on GoalsScored
+                    system.differenceSource = ScoreDifferenceSource.Goals;
+                    break;
                 default:
                     system.differenceSource = ScoreDifferenceSource.Score;
                     break;
diff --git a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs
new file mode 100644
index 000000000..ceefeecca
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs
@@ -0,0 +1,302 @@
+using System;
+using System.Collections.Generic;
+using CosmicShore.ScriptableObjects;
+using UnityEngine;
+using Tk = CosmicShore.Gameplay.PaintingStrokeToolkit;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Deterministic artwork generation for the Fake Artist minigame. Given
+    /// (preset, seed, playerCount) it produces exactly playerCount x strokesPerPlayer
+    /// medium strokes: the preset's strokes (fixed internal seeds, so identical on every
+    /// machine) run through a seeded parametric variation (yaw, mirror, scale jitter,
+    /// mild curl-field warp - "players never play the same thing twice"), then get
+    /// repartitioned by splitting the longest strokes / dropping the shortest until the
+    /// count matches, and finally reordered by OrderForFlightContinuity so a contiguous
+    /// deal gives each player a spatially-coherent bundle.
+    ///
+    /// Pure and allocation-only - no UnityEngine.Object access - so the SERVER generates
+    /// gameplay data from it (per-player stroke dots go out via targeted ClientRpcs and
+    /// stay secret), and every CLIENT regenerates the identical artwork locally at
+    /// reveal time from just (preset, size, seed).
+    /// </summary>
+    public static class FakeArtistArtworkBuilder
+    {
+        /// <summary>Presets used for round artwork + as decoy subject options.</summary>
+        public static readonly PaintingPreset[] SubjectCatalog =
+        {
+            PaintingPreset.Star, PaintingPreset.Rainbow, PaintingPreset.Saturn,
+            PaintingPreset.TajMahal, PaintingPreset.Nautilus, PaintingPreset.Lotus,
+            PaintingPreset.Buckyball, PaintingPreset.TorusKnot, PaintingPreset.DoubleHelix,
+            PaintingPreset.SpiralGalaxy, PaintingPreset.LionsHead, PaintingPreset.Phoenix,
+            PaintingPreset.Peacock, PaintingPreset.Rose, PaintingPreset.StarryNight,
+            PaintingPreset.BobRossVista,
+        };
+
+        /// <summary>Player-facing subject names for the vote's multiple choice.</summary>
+        public static string SubjectName(PaintingPreset preset) => preset switch
+        {
+            PaintingPreset.Star => "Star",
+            PaintingPreset.Rainbow => "Rainbow",
+            PaintingPreset.Saturn => "Saturn",
+            PaintingPreset.TajMahal => "Taj Mahal",
+            PaintingPreset.Nautilus => "Nautilus",
+            PaintingPreset.Lotus => "Lotus",
+            PaintingPreset.Buckyball => "Buckyball",
+            PaintingPreset.TorusKnot => "Torus Knot",
+            PaintingPreset.DoubleHelix => "Double Helix",
+            PaintingPreset.SpiralGalaxy => "Spiral Galaxy",
+            PaintingPreset.LionsHead => "Lion's Head",
+            PaintingPreset.Phoenix => "Phoenix",
+            PaintingPreset.Peacock => "Peacock",
+            PaintingPreset.Rose => "Rose",
+            PaintingPreset.StarryNight => "Starry Night",
+            PaintingPreset.BobRossVista => "Mountain Vista",
+            _ => preset.ToString(),
+        };
+
+        /// <summary>
+        /// Builds the round's strokes in artwork-LOCAL space (y=0 base like the presets),
```

</details>

### `f082cb0a3` — feat(arcade): Fake Artist win target in End Game Conditions (default 8)

_Claude, 2026-07-22 08:11:08 +0000_

```text
Live + build field pair, GetFakeArtistWinTarget(), window row + effective/
baseline display, committed asset keeps live == build. Resolved at
FakeArtistTurnMonitor.StartMonitor -> GameDataSO.GoalTargetCount (never a
per-scene field).
```

```text
 Assets/Resources/EndConditionOverrides.asset                 |  2 ++
 Assets/_Scripts/Editor/EndConditionOverridesWindow.cs        | 10 ++++++++--
 Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs | 19 ++++++++++++++++++-
 3 files changed, 28 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 121 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/EndConditionOverridesWindow.cs b/Assets/_Scripts/Editor/EndConditionOverridesWindow.cs
index f874486f7..ffd000705 100644
--- a/Assets/_Scripts/Editor/EndConditionOverridesWindow.cs
+++ b/Assets/_Scripts/Editor/EndConditionOverridesWindow.cs
@@ -71,7 +71,9 @@ namespace CosmicShore.Editor
                 "  • Brood Rush: claimed fauna waves to win (race to N), default " +
                 EndConditionOverridesSO.DefaultNucleusRushWaveTarget + ".\n" +
                 "  • Rampage: hostile prisms destroyed to win (race to N), default " +
-                EndConditionOverridesSO.DefaultRampagePrismTarget + ".",
+                EndConditionOverridesSO.DefaultRampagePrismTarget + ".\n" +
+                "  • Fake Artist: points a PLAYER needs to win (race to N, free-for-all), default " +
+                EndConditionOverridesSO.DefaultFakeArtistWinTarget + ".",
                 MessageType.Info);
 
             // ---- Live input fields (used at runtime) ----
@@ -84,6 +86,7 @@ namespace CosmicShore.Editor
             int mw  = Mathf.Max(0, EditorGUILayout.IntField("Maelstrom - Win Target (points)", _config.maelstromWinTarget));
             int nr  = Mathf.Max(0, EditorGUILayout.IntField("Brood Rush - Wave Target", _config.nucleusRushWaveTarget));
             int ra  = Mathf.Max(0, EditorGUILayout.IntField("Rampage - Prism Target", _config.rampagePrismTarget));
+            int fa  = Mathf.Max(0, EditorGUILayout.IntField("Fake Artist - Win Target (points)", _config.fakeArtistWinTarget));
             if (EditorGUI.EndChangeCheck())
                 Persist("Edit End Game Conditions", () =>
                 {
@@ -93,6 +96,7 @@ namespace CosmicShore.Editor
                     _config.maelstromWinTarget = mw;
                     _config.nucleusRushWaveTarget = nr;
                     _config.rampagePrismTarget = ra;
+                    _config.fakeArtistWinTarget = fa;
                 });
 
             EditorGUILayout.Space();
@@ -104,6 +108,7 @@ namespace CosmicShore.Editor
             EditorGUILayout.LabelField("Maelstrom", mw > 0 ? mw.ToString() : EndConditionOverridesSO.DefaultMaelstromWinTarget + " (default)");
             EditorGUILayout.LabelField("Brood Rush", nr > 0 ? nr.ToString() : EndConditionOverridesSO.DefaultNucleusRushWaveTarget + " (default)");
             EditorGUILayout.LabelField("Rampage", ra > 0 ? ra.ToString() : EndConditionOverridesSO.DefaultRampagePrismTarget + " (default)");
+            EditorGUILayout.LabelField("Fake Artist", fa > 0 ? fa.ToString() : EndConditionOverridesSO.DefaultFakeArtistWinTarget + " (default)");
             EditorGUI.indentLevel--;
 
             // ---- Build baseline (read-only display + capture button) ----
@@ -138,7 +143,8 @@ namespace CosmicShore.Editor
                    "Joust: " + Fmt(_config.joustCountBuild, "default " + EndConditionOverridesSO.DefaultJoustCount) + "\n" +
                    "Maelstrom: " + Fmt(_config.maelstromWinTargetBuild, "default " + EndConditionOverridesSO.DefaultMaelstromWinTarget) + "\n" +
                    "Brood Rush: " + Fmt(_config.nucleusRushWaveTargetBuild, "default " + EndConditionOverridesSO.DefaultNucleusRushWaveTarget) + "\n" +
-                   "Rampage: " + Fmt(_config.rampagePrismTargetBuild, "default " + EndConditionOverridesSO.DefaultRampagePrismTarget);
+                   "Rampage: " + Fmt(_config.rampagePrismTargetBuild, "default " + EndConditionOverridesSO.DefaultRampagePrismTarget) + "\n" +
+                   "Fake Artist: " + Fmt(_config.fakeArtistWinTargetBuild, "default " + EndConditionOverridesSO.DefaultFakeArtistWinTarget);
 
             static string Fmt(int value, string zeroMeaning) => value > 0 ? value.ToString() : "0 (" + zeroMeaning + ")";
         }
diff --git a/Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs b/Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs
index ba9837870..6b4c60ef1 100644
--- a/Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs
@@ -46,6 +46,9 @@ namespace CosmicShore.ScriptableObjects
         /// <summary>Rampage hostile-prism target used when <see cref="rampagePrismTarget"/> is 0 (auto/default).</summary>
         public const int DefaultRampagePrismTarget = 2000;
 
+        /// <summary>Fake Artist win target used when <see cref="fakeArtistWinTarget"/> is 0 (auto/default).</summary>
+        public const int DefaultFakeArtistWinTarget = 8;
+
         [Header("Live counts - used at runtime. 0 = auto/default (edit via Tools > Cosmic Shore > End Game Conditions)")]
         [Tooltip("HexRace crystals to end the race. 0 = auto-calc from the track waypoints.")]
         [Min(0)] public int hexRaceCrystalCount = 0;
@@ -68,6 +71,10 @@ namespace CosmicShore.ScriptableObjects
                  "(race to N). 0 = default (2000).")]
         [Min(0)] public int rampagePrismTarget = 2000;
 
+        [Tooltip("Fake Artist: points a PLAYER (free-for-all) needs to win the gallery " +
+                 "(race to N across rounds). 0 = default (8).")]
+        [Min(0)] public int fakeArtistWinTarget = 8;
+
         [Header("Build baseline - what a shipping build uses. Set via the tool's \"Set Build Values\" button.")]
         [Min(0)] public int hexRaceCrystalCountBuild = 0;
         [Min(0)] public int crystalCaptureCrystalCountBuild = 20;
@@ -75,6 +82,7 @@ namespace CosmicShore.ScriptableObjects
         [Min(0)] public int maelstromWinTargetBuild = 6;
         [Min(0)] public int nucleusRushWaveTargetBuild = 3;
         [Min(0)] public int rampagePrismTargetBuild = 2000;
+        [Min(0)] public int fakeArtistWinTargetBuild = 8;
 
```

</details>

### `9e643d4c4` — feat(editor): Fake Artist scene + asset setup tool

_Claude, 2026-07-22 08:13:53 +0000_

```text
Tools > Cosmic Shore > Setup Fake Artist Minigame - idempotently authors
the mode's assets (scoring rule, config, arcade card + OrganicRematchGames
registration), clones MinigameNucleusRush into MinigameFakeArtist, swaps
the mode components, removes the Cell/NetworkCrystalManager (v1 is
cell-less by design), grows the spawn ring to 12, clears the domain-panel
HUD wiring for the FFA per-player layout, rewires Scoreboard/PauseMenu,
and registers the scene in Build Settings.
```

```text
 Assets/_Scripts/Editor/FakeArtistSetupTool.cs | 428 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 428 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 434 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/FakeArtistSetupTool.cs b/Assets/_Scripts/Editor/FakeArtistSetupTool.cs
new file mode 100644
index 000000000..379c8646c
--- /dev/null
+++ b/Assets/_Scripts/Editor/FakeArtistSetupTool.cs
@@ -0,0 +1,428 @@
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Data;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using UnityEditor;
+using UnityEditor.SceneManagement;
+using UnityEngine;
+using UnityEngine.SceneManagement;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Tools &gt; Cosmic Shore &gt; Setup Fake Artist Minigame - authors everything the
+    /// Fake Artist mode (GameModes.FakeArtist = 39) needs, idempotently
+    /// (ToyboxSetupTool pattern):
+    ///
+    ///  1. Assets: FakeArtistScoringRule.asset (metric=Goals, points),
+    ///     FakeArtistConfig.asset, ArcadeGameFakeArtist.asset (card, 3-12 players,
+    ///     comeback off) + registration in GameLists/OrganicRematchGames.asset.
+    ///  2. Scene: clones MinigameNucleusRush.unity → MinigameFakeArtist.unity
+    ///     (never hand-write scene YAML - Unity 6 rejects it), then swaps the mode
+    ///     components on the Game object, removes the Cell + NetworkCrystalManager
+    ///     (no ecology in v1: fauna would eat the gallery and CellItems auto-shield
+    ///     prisms, corrupting brush identities), grows the spawn ring to 12, clears
+    ///     the MultiplayerHUDView domain-panel wiring (free-for-all uses the
+    ///     per-player HUD layout), and rewires Scoreboard/PauseMenu/CountdownTimer
+    ///     references.
+    ///  3. Registers the scene in EditorBuildSettings.
+    ///
+    /// Re-run safe: existing assets/scene are re-wired, not recreated. After running,
+    /// verify in-editor per FAKEARTIST.md's checklist (card icons, Menu_Main card-grid
+    /// slot count, spawn-ring placement).
+    /// </summary>
+    public static class FakeArtistSetupTool
+    {
+        const string TemplateScenePath = "Assets/_Scenes/Multiplayer Scenes/MinigameNucleusRush.unity";
+        const string ScenePath = "Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity";
+        const string RulePath = "Assets/_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset";
+        const string ConfigPath = "Assets/_SO_Assets/Games/FakeArtistConfig.asset";
+        const string CardPath = "Assets/_SO_Assets/Games/ArcadeGameFakeArtist.asset";
+        const string NucleusCardPath = "Assets/_SO_Assets/Games/ArcadeGameNucleusRush.asset";
+        const string GameListPath = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset";
+
+        const int SpawnPointCount = 12;
+
+        [MenuItem("Tools/Cosmic Shore/Setup Fake Artist Minigame")]
+        public static void Setup()
+        {
+            var summary = new List<string>();
+
+            var rule = LoadOrCreateRule(summary);
+            var config = LoadOrCreateConfig(summary);
+            var card = LoadOrCreateCard(summary);
+            RegisterInGameList(card, summary);
+
+            bool sceneOk = SetupScene(rule, config, summary);
+            if (sceneOk)
+                RegisterSceneInBuildSettings(summary);
+
+            AssetDatabase.SaveAssets();
+            AssetDatabase.Refresh();
+
+            EditorUtility.DisplayDialog("Fake Artist Setup",
+                string.Join("\n", summary) +
+                "\n\nManual follow-ups (see FAKEARTIST.md):" +
+                "\n• Card icons/background on ArcadeGameFakeArtist.asset (placeholders copied from Brood Rush)." +
+                "\n• Verify Menu_Main's Arcade grid has a free GameCard slot for an 8th game." +
+                "\n• Optionally add mode 39 to ProgressionConfig.asset alwaysUnlockedModes." +
+                "\n• Fly the scene once and adjust the 12-spawn ring to taste.",
+                "OK");
+        }
+
+        // ── Assets ──────────────────────────────────────────────────────────
```

</details>

### `26d6ab2bd` — test(arcade): Fake Artist artwork builder + vote scorer edit-mode tests

_Claude, 2026-07-22 08:15:43 +0000_

```text
Locks the load-bearing determinism (server deals from the same generator
clients regenerate the reveal from), exact stroke-count repartition, dot
endpoint guarantees, subject-choice shuffling, and the +1/+1/-1/+4 tally
rules incl. flat accused penalty and imposter-vote/self-vote rejection.
Also adds Unity .meta files for the new mode scripts.
```

```text
 Assets/_Scripts/Controller/Arcade/FakeArtist.meta                     |   8 ++
 .../Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs.meta     |  11 ++
 .../_Scripts/Controller/Arcade/FakeArtist/FakeArtistBrushes.cs.meta   |  11 ++
 .../_Scripts/Controller/Arcade/FakeArtist/FakeArtistConfigSO.cs.meta  |  11 ++
 .../Controller/Arcade/FakeArtist/FakeArtistController.cs.meta         |  11 ++
 .../Controller/Arcade/FakeArtist/FakeArtistRevealGhost.cs.meta        |  11 ++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistScorer.cs.meta |  11 ++
 .../Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs.meta        |  11 ++
 .../_Scripts/Controller/Arcade/FakeArtist/FakeArtistVotePanel.cs.meta |  11 ++
 .../Controller/Arcade/Scoring/FakeArtistScoringRuleSO.cs.meta         |  11 ++
 .../Controller/Arcade/TurnMonitors/FakeArtistTurnMonitor.cs.meta      |  11 ++
 Assets/_Scripts/Editor/FakeArtistSetupTool.cs.meta                    |  11 ++
 Assets/_Scripts/Tests/EditMode/FakeArtistTests.cs                     | 236 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/EditMode/FakeArtistTests.cs.meta                |  11 ++
 14 files changed, 376 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 242 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/EditMode/FakeArtistTests.cs b/Assets/_Scripts/Tests/EditMode/FakeArtistTests.cs
new file mode 100644
index 000000000..59f975f54
--- /dev/null
+++ b/Assets/_Scripts/Tests/EditMode/FakeArtistTests.cs
@@ -0,0 +1,236 @@
+#if UNITY_EDITOR
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using NUnit.Framework;
+using UnityEngine;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// Pure-logic tests for the Fake Artist minigame's two deterministic cores:
+    /// <see cref="FakeArtistArtworkBuilder"/> (the server generates gameplay data from it
+    /// and every client regenerates the identical artwork at reveal - byte-equal results
+    /// are load-bearing) and <see cref="FakeArtistScorer"/> (the vote tally that encodes
+    /// the game's point rules).
+    /// </summary>
+    public class FakeArtistArtworkBuilderTests
+    {
+        [TestCase(3, 3)]
+        [TestCase(5, 3)]
+        [TestCase(12, 3)]
+        [TestCase(12, 1)]
+        public void BuildStrokes_ProducesExactStrokeCount(int playerCount, int strokesPerPlayer)
+        {
+            int target = playerCount * strokesPerPlayer;
+            var strokes = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Saturn, 600f, 12345, target);
+            Assert.AreEqual(target, strokes.Count);
+        }
+
+        [Test]
+        public void BuildStrokes_EveryStrokeIsFlyable()
+        {
+            var strokes = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Phoenix, 600f, 777, 36);
+            foreach (var stroke in strokes)
+            {
+                Assert.GreaterOrEqual(stroke.points.Count, 2, $"{stroke.name} has too few points");
+                Assert.Greater(FakeArtistArtworkBuilder.ArcLength(stroke.points), 0f, $"{stroke.name} has zero length");
+            }
+        }
+
+        [Test]
+        public void BuildStrokes_IsDeterministic()
+        {
+            var a = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.TorusKnot, 600f, 42, 27);
+            var b = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.TorusKnot, 600f, 42, 27);
+
+            Assert.AreEqual(a.Count, b.Count);
+            for (int s = 0; s < a.Count; s++)
+            {
+                Assert.AreEqual(a[s].points.Count, b[s].points.Count, $"stroke {s} point count");
+                for (int i = 0; i < a[s].points.Count; i++)
+                    Assert.That((a[s].points[i] - b[s].points[i]).sqrMagnitude, Is.LessThan(1e-8f),
+                        $"stroke {s} point {i} differs");
+            }
+        }
+
+        [Test]
+        public void BuildStrokes_DifferentSeedsDiffer()
+        {
+            var a = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Star, 600f, 1, 9);
+            var b = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Star, 600f, 2, 9);
+
+            bool anyDifferent = false;
+            for (int s = 0; s < a.Count && !anyDifferent; s++)
+            {
+                if (a[s].points.Count != b[s].points.Count) { anyDifferent = true; break; }
+                for (int i = 0; i < a[s].points.Count; i++)
+                {
+                    if ((a[s].points[i] - b[s].points[i]).sqrMagnitude > 1e-4f) { anyDifferent = true; break; }
+                }
+            }
+            Assert.IsTrue(anyDifferent, "two seeds produced identical artwork - parametric variation is dead");
+        }
```

</details>

### `70c3e5870` — docs(arcade): FAKEARTIST.md + mode 39 index updates

_Claude, 2026-07-22 08:19:00 +0000_

```text
Per-mode technical reference (NUCLEUSRUSH.md skeleton + AstroLeague's
Class Inventory / Networking Model tables, collider-budget statement,
headless-authoring verification checklist). Updates CLAUDE.md (scenes
table incl. AstroLeague ID fix 36->37, GameModes paragraph, controller
tree, doc index incl. the missing NUCLEUSRUSH.md row), Docs/SCENES.md
(6 sections), and the ToySystem backlog's score-bearing-mode bullet.
```

```text
 Assets/_Scripts/Controller/Arcade/FAKEARTIST.md      | 246 +++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Arcade/FAKEARTIST.md.meta |   7 ++
 CLAUDE.md                                            |  10 +-
 Docs/SCENES.md                                       |  26 +++++-
 Docs/ToySystem/BACKLOG.md                            |   5 +
 5 files changed, 289 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 390 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
new file mode 100644
index 000000000..8dd203200
--- /dev/null
+++ b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
@@ -0,0 +1,246 @@
+# Fake Artist — Technical Documentation
+
+## Overview
+
+Fake Artist (`GameModes.FakeArtist = 39`) is the free-for-all social-deduction painting
+party game — Cosmic Shore's take on the tabletop game *A Fake Artist Goes to New York*,
+inverted: here the imposter **knows the subject but not how to draw it**, while the honest
+artists **know how to draw their strokes but not what they're drawing**. It is built
+directly on the Connect-the-Dots painting toy's stroke toolkit (BACKLOG.md's sanctioned
+"separate, score-bearing mode" — the toy itself stays scoreless).
+
+Each round, up to 12 players simultaneously fly 3 assigned strokes of one secret artwork.
+Honest artists get full "connect the dots" ring guides; the fake artist gets only each
+stroke's start and end. Then everyone but the fake artist votes on two questions — *what
+are we drawing?* and *who is the fake artist?* — points land, the full blueprint blooms
+over the painted prisms as the reveal, and the next round starts on a fresh canvas. First
+player to the win target (default 8) takes the gallery.
+
+- **The drawing IS the deduction surface.** Nothing is announced; players read the
+  emerging prism artwork itself. The imposter's improvised middles betray them; the honest
+  artists' ring-accurate strokes betray the subject. Working entirely through Prisms/Mass.
+- **Everyone is their own team.** No team selection. 12 trail identities = 6 paint colors
+  (Jade, Ruby, Gold, Blue + synthetic Fire and Lime) x 2 states (normal / shielded
+  octahedron prisms) — identity is carried by the mass itself, per-player scoring rides
+  `RoundStats.GoalsScored`.
+- **The gallery is conserved mass.** Rounds place their canvases on a golden-angle ring;
+  finished artworks persist for the whole session (no TTLs, no cullers — Universality).
+  The scene-reload replay is the only sink.
+- **Parametric variation, never the same painting twice.** Every round re-generates a
+  preset through a seeded transform (yaw, mirror, scale jitter, curl-field warp) and
+  repartitions it to exactly `players x 3` medium strokes.
+
+**Key architectural facts:**
+
+- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity` — single unified
+  scene, no separate singleplayer variant; solo-ish play is a party of one + AI backfill
+  (minimum 3 players total). Authored by **Tools ▸ Cosmic Shore ▸ Setup Fake Artist
+  Minigame** (clones the NucleusRush scene and swaps the mode stack).
+- **GameMode enum**: `GameModes.FakeArtist = 39`, display name "Fake Artist".
+- **Controller**: `FakeArtistController : MultiplayerDomainGamesController` (ready-sync +
+  feed posts + roster cleanup; the domain-sum NetworkVariables are harmless noise in FFA).
+- **Scoring**: `FakeArtistScoringRuleSO`, `metric = ScoringMetric.Goals`, points (not
+  golf). PER-PLAYER rule: `UsesPerPlayerWinner => true` switches EndGameSequencer's win
+  check and the Scoreboard banner to `WinnerName`. Round tally (`FakeArtistScorer`, values
+  on `FakeArtistConfig.asset`): +1 correct subject, +1 correct accusation, flat −1 to any
+  player accused by ≥1 voter (imposter or not), +4 to the fake artist every round (a
+  caught imposter nets +3). Totals can go negative.
+- **Turn monitor**: `FakeArtistTurnMonitor` resolves the win target from
+  `EndConditionOverridesSO.GetFakeArtistWinTarget()` at StartMonitor (never a per-scene
+  field), syncs via NetworkVariable → `GameDataSO.GoalTargetCount`, and ends the turn when
+  the controller's round resolves (draw → vote → reveal). The first-to-N check runs in
+  `OnTurnEndedCustom` via `rule.IsObjectiveReached` (per-player scan, never SumByDomain).
+- **Domains**: `MinDomainsAllowed = MaxDomainsAllowed = 3` (pinned; cosmetic — domains are
+  paint, not teams). `MinPlayersAllowed = 3`, `MaxPlayersAllowed = 12` (the modal's hard
+  cap). AI backfill fills `PlayerCount − humans`.
+- **Vessels**: all six playable vessels (list copied from the Brood Rush card).
+- **AI opponents**: fly their dealt dots via `AIPilot.SetExternalTargetProvider` closures
+  (Rampage pattern) with server-side pen control; an AI imposter gets start+end dots only,
+  same as a human. AI ballots are injected server-side with config-driven accuracy
+  (`AICorrectSubjectChance` / `AICorrectImposterChance`).
+- **Comeback**: `ComebackRatePerScoreDeficit = 0` — elemental comeback is a flight-power
+  system; deduction rounds shouldn't buff trailing players' vessels. (Source case `Goals`
+  registered in `ElementalComebackSystem.EnsureExists` should the rate ever be raised.)
+- **Config**: `_SO_Assets/Games/ArcadeGameFakeArtist.asset`, registered in
+  `GameLists/OrganicRematchGames.asset`; mode tuning on
+  `_SO_Assets/Games/FakeArtistConfig.asset`.
+
+## Class Inventory (`_Scripts/Controller/Arcade/FakeArtist/`)
+
+| Class | Role |
+|---|---|
+| `FakeArtistController` | Round phase machine (Idle → Drawing → Voting → Revealing → Resolved), brush table, targeted deal RPCs, vote collection, tally + reveal, first-to-N final sync |
+| `FakeArtistBrushes` | The 12 trail identities: slot→(paint domain, shielded) table, synthetic Fire `(Domains)5` / Lime `(Domains)6` material-set minting + registration, per-spawn brush integrity (`PrismKinds.Clear` + `ActivateShield`), UI/ribbon colors |
+| `FakeArtistArtworkBuilder` | Pure/deterministic: preset → seeded parametric variation → repartition to exactly `players x strokesPerPlayer` strokes → flight-order deal; ride-dot extraction; golden-angle round anchors; subject-choice building |
```

</details>

### `99f3a1596` — fix(arcade): Fake Artist review pass - marker dimming, under-deal, guards

_Claude, 2026-07-22 14:14:30 +0000_

```text
- FakeArtistStrokeGuide: per-marker target scale so dimmed (non-current)
  dots hold at 0.65 instead of the shared bloomer dragging them back to
  full size.
- FakeArtistController: track actual strokes dealt per player
  (_strokesDealt) so a preset that under-delivers strokes doesn't stall
  the drawing phase until the timer; AI + human completion key off it.
- FakeArtistTurnMonitor: explicit if (!IsServer) return false guard on
  CheckForEndOfTurn (matches every other monitor; belt-and-suspenders
  over the server-only phase machine).
- VesselController: bump the netvar-dirty benchmark count 3->4 for the
  added n_TrailPenUp write.
```

```text
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs  | 17 ++++++++++---
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs | 44 ++++++++++++++++-----------------
 .../_Scripts/Controller/Arcade/TurnMonitors/FakeArtistTurnMonitor.cs  |  4 +++
 Assets/_Scripts/Controller/Vessel/VesselController.cs                 |  2 +-
 4 files changed, 40 insertions(+), 27 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 187 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs
index eb288730f..c17e5c422 100644
--- a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs
+++ b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs
@@ -54,6 +54,7 @@ namespace CosmicShore.Gameplay
         readonly Dictionary<string, int> _points = new();
         readonly Dictionary<string, int> _imposterCounts = new();
         readonly Dictionary<string, int> _strokesDone = new();
+        readonly Dictionary<string, int> _strokesDealt = new(); // actual strokes each player got this round
         readonly Dictionary<string, FakeArtistScorer.Answers> _votes = new();
         string _imposterName = string.Empty;
         PaintingPreset _preset;
@@ -321,6 +322,7 @@ namespace CosmicShore.Gameplay
             // Per-player world-space dots; the fake artist's strokes degrade to start+end.
             _dealtDots = new Vector3[_roster.Count][][];
             _strokesDone.Clear();
+            _strokesDealt.Clear();
             _votes.Clear();
             for (int p = 0; p < _roster.Count; p++)
             {
@@ -338,6 +340,10 @@ namespace CosmicShore.Gameplay
                     _dealtDots[p][s] = world.ToArray();
                 }
                 _strokesDone[_roster[p]] = 0;
+                // Actual strokes dealt - usually StrokesPerPlayer, but a preset with too
+                // little total path can under-deliver; AllPaintersFinished keys off this
+                // so a short-changed player doesn't stall the phase until the timer.
+                _strokesDealt[_roster[p]] = bundle.Count;
             }
 
             _phase = RoundPhase.Drawing;
@@ -456,7 +462,8 @@ namespace CosmicShore.Gameplay
             if (_phase != RoundPhase.Drawing) return;
             var player = FindHumanPlayerByClientId(rpcParams.Receive.SenderClientId);
             if (player == null || !_strokesDone.ContainsKey(player.Name)) return;
-            _strokesDone[player.Name] = Mathf.Min(_strokesDone[player.Name] + 1, config.StrokesPerPlayer);
+            int dealt = _strokesDealt.TryGetValue(player.Name, out var d) ? d : config.StrokesPerPlayer;
+            _strokesDone[player.Name] = Mathf.Min(_strokesDone[player.Name] + 1, dealt);
         }
 
         void HandleTurnStartedAllPeers()
@@ -495,7 +502,8 @@ namespace CosmicShore.Gameplay
         {
             foreach (var name in _roster)
             {
-                if (!_strokesDone.TryGetValue(name, out int done) || done < config.StrokesPerPlayer)
+                int dealt = _strokesDealt.TryGetValue(name, out var d) ? d : config.StrokesPerPlayer;
+                if (!_strokesDone.TryGetValue(name, out int done) || done < dealt)
                     return false;
             }
             return true;
@@ -730,9 +738,10 @@ namespace CosmicShore.Gameplay
                         if (dot >= strokes[stroke].Length)
                         {
                             pen.SetSpawnerPaused(true);  // pen-up between strokes
+                            int aiDealt = _strokesDealt.TryGetValue(name, out var dealtCount)
+                                ? dealtCount : config.StrokesPerPlayer;
                             _strokesDone[name] = Mathf.Min(
-                                (_strokesDone.TryGetValue(name, out int v) ? v : 0) + 1,
-                                config.StrokesPerPlayer);
+                                (_strokesDone.TryGetValue(name, out int v) ? v : 0) + 1, aiDealt);
                             stroke++;
                             dot = 0;
                             if (stroke >= strokes.Length)
diff --git a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs
index e6f38cff5..32ccd8298 100644
--- a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs
+++ b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs
@@ -70,8 +70,12 @@ namespace CosmicShore.Gameplay
         bool _running;
         bool _penHeld;              // true while this guide is holding the pen up
 
-        readonly List<Transform> _bloomers = new();
-        readonly List<GameObject> _dotMarkers = new();   // markers of the CURRENT stroke, index-aligned with its dots
+        // Markers of the CURRENT stroke, index-aligned with its dots (retired entries -> null).
+        readonly List<GameObject> _dotMarkers = new();
+        // Per-marker animation target: each marker eases toward its stored scale (bloom-in
+        // to full for the current dot, to DimmedDotScale for the rest) - a single shared
+        // bloomer target would drag dimmed dots back to full size.
+        readonly Dictionary<Transform, float> _markerTargets = new();
```

</details>

### `fda4c785e` — feat(arcade): commit Fake Artist card, scene + assets so it shows in the arcade

_Claude, 2026-07-22 14:59:05 +0000_

```text
The setup tool needs Unity to run; it never did, so nothing surfaced the
card. Author the assets directly instead:

- ArcadeGameFakeArtist / FakeArtistConfig / FakeArtistScoringRule assets
  (+ metas), registered in OrganicRematchGames (the DI list AND
  GameCard.AllGames), unlocked via ProgressionConfig alwaysUnlockedModes.
  Menu_Main's grid has 12 card slots and only 7 were used, so the 8th
  game fills a spare slot - no scene-hierarchy edit needed.
- MinigameFakeArtist.unity: clone of MinigameNucleusRush with the
  controller + turn monitor scripts swapped (fileIDs preserved, so
  Scoreboard/PauseMenu/TurnMonitorController refs stay wired), rule +
  config wired, monitor.controller wired, and the NetworkCrystalManager
  removed (no bootstrap crystal -> no CellItems/fauna -> brush identity
  stays intact and the gallery survives the round). Added to Build
  Settings.

Card now appears, is unlocked, and launches. The scene still carries
NucleusRush's 4 spawn points + domain-panel HUD; the setup tool refines
those (grows to 12, per-player HUD, full Cell removal) and is idempotent.
```

```text
 Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset          |     1 +
 Assets/_SO_Assets/Games/ArcadeGameFakeArtist.asset               |    43 +
 Assets/_SO_Assets/Games/ArcadeGameFakeArtist.asset.meta          |     8 +
 Assets/_SO_Assets/Games/FakeArtistConfig.asset                   |    27 +
 Assets/_SO_Assets/Games/FakeArtistConfig.asset.meta              |     8 +
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset      |     1 +
 Assets/_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset      |    16 +
 Assets/_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset.meta |     8 +
 Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity       | 10575 +++++++++++++++++++++++++++++++++++
 Assets/_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity.meta  |     7 +
 Assets/_Scripts/Controller/Arcade/FAKEARTIST.md                  |    61 +-
 ProjectSettings/EditorBuildSettings.asset                        |     3 +
 12 files changed, 10736 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 91 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
index 8dd203200..a0f900d0c 100644
--- a/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
+++ b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
@@ -62,8 +62,13 @@ player to the win target (default 8) takes the gallery.
   system; deduction rounds shouldn't buff trailing players' vessels. (Source case `Goals`
   registered in `ElementalComebackSystem.EnsureExists` should the rate ever be raised.)
 - **Config**: `_SO_Assets/Games/ArcadeGameFakeArtist.asset`, registered in
-  `GameLists/OrganicRematchGames.asset`; mode tuning on
-  `_SO_Assets/Games/FakeArtistConfig.asset`.
+  `GameLists/OrganicRematchGames.asset` (both the DI list and `GameCard.AllGames` point
+  at it) and unlocked via `ProgressionConfig.asset` `alwaysUnlockedModes` (39); mode
+  tuning on `_SO_Assets/Games/FakeArtistConfig.asset`. **These assets + the scene are
+  committed directly** (not only tool-generated) so the card appears and launches from a
+  fresh checkout — Menu_Main's arcade grid has 12 physical card slots, so the 8th game
+  fills a spare slot with no scene-hierarchy edit. The `Setup Fake Artist Minigame` tool
+  remains the way to *refine* the scene (see limitations).
 
 ## Class Inventory (`_Scripts/Controller/Arcade/FakeArtist/`)
 
@@ -159,14 +164,18 @@ domains in modes with Cell control or domain-aggregated scoring.**
 
 ## Ecology configuration
 
-**Fake Artist v1 is deliberately cell-less** — the setup tool removes the cloned scene's
-Cell and NetworkCrystalManager. Two hard reasons: `Prism.OnTriggerEnter(CellItem)`
-auto-shields touched prisms (corrupting normal-brush identities — the deduction surface),
-and fauna would graze the gallery mid-round (the artwork must survive to the vote). No
-crystals, no flora, no fauna; the environment IS the accumulating painted gallery. If a
-future pass wants membrane/cytoplasm ambience, wire a standard Cell with an empty
-SpawnProfile and no bootstrap crystal (spawner never starts) via the `/ecology` skill —
-do not build a mode-local substitute.
+**Fake Artist v1 runs with no active ecology.** The committed scene keeps the cloned
+NucleusRush **Cell** as an inert membrane (visual boundary) but **removes the
+`NetworkCrystalManager`** — with no bootstrap crystal the Cell's spawner never starts, so
+no crystals, flora, or fauna appear. Two hard reasons this matters:
+`Prism.OnTriggerEnter(CellItem)` auto-shields touched prisms (which would corrupt
+normal-brush identities — the deduction surface), and fauna would graze the gallery
+mid-round (the artwork must survive to the vote). The environment IS the accumulating
+painted gallery. (The `Setup Fake Artist Minigame` tool goes further and removes the Cell
+GameObject entirely; leaving it as inert ambience is an equally valid, lower-risk state.)
+If a future pass wants live cytoplasm ambience, wire a standard Cell with an empty
+SpawnProfile and no bootstrap crystal via the `/ecology` skill — never a mode-local
+substitute.
 
 **Collider-budget impact: ~zero net.** Guide rings/gates/jacks use NO colliders (pure
 distance latching). Painted trail prisms carry the standard per-prism BoxCollider budget —
@@ -215,23 +224,31 @@ Authored ONLY via **Tools ▸ Cosmic Shore ▸ End Game Conditions**
 
 | Asset | Path |
 |---|---|
-| Arcade card | `_SO_Assets/Games/ArcadeGameFakeArtist.asset` (created by the setup tool; registered in `GameLists/OrganicRematchGames.asset`) |
-| Scoring rule | `_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset` (setup tool) |
-| Mode config | `_SO_Assets/Games/FakeArtistConfig.asset` (setup tool) |
-| Scene | `_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity` (setup tool clone of MinigameNucleusRush + component swap; added to Build Settings) |
+| Arcade card | `_SO_Assets/Games/ArcadeGameFakeArtist.asset` (committed; registered in `GameLists/OrganicRematchGames.asset`) |
+| Scoring rule | `_SO_Assets/Scoring Rules/FakeArtistScoringRule.asset` (committed; `metric=Goals`, `golfRules=0`) |
+| Mode config | `_SO_Assets/Games/FakeArtistConfig.asset` (committed) |
+| Scene | `_Scenes/Multiplayer Scenes/MinigameFakeArtist.unity` (committed clone of MinigameNucleusRush + component swap + crystal-manager removal; in Build Settings) |
+| Unlock | `ProgressionConfig.asset` `alwaysUnlockedModes` includes 39 |
 | Win target | `Resources/EndConditionOverrides.asset` → `fakeArtistWinTarget` (default 8) |
 
 ## Known limitations / follow-ups
 
-- **In-editor verification pending (authored headless).** Run **Tools ▸ Cosmic Shore ▸
-  Setup Fake Artist Minigame**, then: (1) open the scene — confirm the component swap,
-  12-point spawn ring, no Cell; (2) MPPM 2-player run — brush colors/shields on both
+- **In-editor verification pending (authored headless).** The card + scene are committed
+  and should appear/launch as-is, but no Unity pass has run. Verify: (1) card visible +
+  clickable in the Arcade grid; (2) MPPM 2-player run — brush colors/shields on both
   peers, pen-up gaps identical on both peers, deal secrecy (client log has no other
-  player's strokes), vote flow, reveal ghost, first-to-8 scoreboard; (3) card visible +
-  launchable from the Arcade grid (check the grid has a free GameCard slot for an 8th
-  game); (4) icons on the card are Brood Rush placeholders — replace.
-- **Mode not in `ProgressionConfig.asset` `alwaysUnlockedModes`** (AstroLeague/NucleusRush
-  precedent) — decide explicitly whether fresh accounts see it.
+  player's strokes), vote flow, reveal ghost, first-to-8 scoreboard.
+- **The committed scene is the minimal clone** (controller/monitor swapped, rule/config
+  wired, crystal manager removed). It still carries NucleusRush's **4 spawn points**
+  (`GetRandomSpawnPose` recycles them, so a 12-player game overlaps spawns) and its
```

</details>

### `7ef6339f1` — fix(editor): make Fake Artist setup tool idempotent against its own output

_Claude, 2026-07-22 15:36:56 +0000_

```text
The tool captured countdownTimer/_onToggleReadyButton (and the monitor's
gameData/display) from the NucleusRush template, which is null once the
scene has already been swapped to FakeArtist components (the committed
MinigameFakeArtist.unity, or a second run). It then wrote those nulls over
the good references, breaking the countdown wiring.

Now it captures from the old template OR falls back to the live
FakeArtist component, and writes refs only when non-null (SetIfNotNull),
so running the tool on the committed scene refines it (12 spawn points,
per-player HUD, Cell removal) without blanking anything.
```

```text
 Assets/_Scripts/Editor/FakeArtistSetupTool.cs | 66 +++++++++++++++++++++++++++++++++------------------------
 1 file changed, 38 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 109 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/FakeArtistSetupTool.cs b/Assets/_Scripts/Editor/FakeArtistSetupTool.cs
index 379c8646c..d7dc82bfd 100644
--- a/Assets/_Scripts/Editor/FakeArtistSetupTool.cs
+++ b/Assets/_Scripts/Editor/FakeArtistSetupTool.cs
@@ -238,25 +238,9 @@ namespace CosmicShore.Editor
             }
             var gameGO = monitorController.gameObject;
 
-            // Capture the template's serialized references before removing its components.
-            var oldController = gameGO.GetComponentInChildren<NucleusRushController>(true);
-            var oldMonitor = gameGO.GetComponentInChildren<NucleusRushWaveTurnMonitor>(true);
-
-            Object countdownTimer = null, toggleReadyEvent = null, gameDataAsset = null, displayEvent = null;
-            if (oldController != null)
-            {
-                var so = new SerializedObject(oldController);
-                countdownTimer = so.FindProperty("countdownTimer")?.objectReferenceValue;
-                toggleReadyEvent = so.FindProperty("_onToggleReadyButton")?.objectReferenceValue;
-            }
-            if (oldMonitor != null)
-            {
-                var so = new SerializedObject(oldMonitor);
-                gameDataAsset = so.FindProperty("gameData")?.objectReferenceValue;
-                displayEvent = so.FindProperty("onUpdateTurnMonitorDisplay")?.objectReferenceValue;
-            }
-
-            // Mode components: add ours first, then remove the template's.
+            // Mode components: ensure ours exist (they may already, if the scene was
+            // authored directly - the committed MinigameFakeArtist.unity already carries
+            // the swapped components; a fresh clone of MinigameNucleusRush does not).
             var controller = gameGO.GetComponent<FakeArtistController>();
             if (controller == null)
             {
@@ -270,6 +254,20 @@ namespace CosmicShore.Editor
                 summary.Add("Added FakeArtistTurnMonitor.");
             }
 
+            // Capture base-class wiring (countdown timer, ready-button event, monitor
+            // gameData/display) from whichever component currently carries it: the old
+            // NucleusRush template on a fresh clone, ELSE the already-swapped FakeArtist
+            // component on the committed scene. Falling back to the live component (and the
+            // SetIfNotNull writes below) makes this idempotent - a re-run never blanks out
+            // a good reference by reading from a component that no longer exists.
+            var oldController = gameGO.GetComponentInChildren<NucleusRushController>(true);
+            var oldMonitor = gameGO.GetComponentInChildren<NucleusRushWaveTurnMonitor>(true);
+
+            var countdownTimer = ReadRef(oldController, "countdownTimer") ?? ReadRef(controller, "countdownTimer");
+            var toggleReadyEvent = ReadRef(oldController, "_onToggleReadyButton") ?? ReadRef(controller, "_onToggleReadyButton");
+            var gameDataAsset = ReadRef(oldMonitor, "gameData") ?? ReadRef(monitor, "gameData");
+            var displayEvent = ReadRef(oldMonitor, "onUpdateTurnMonitorDisplay") ?? ReadRef(monitor, "onUpdateTurnMonitorDisplay");
+
             if (oldController != null) { Object.DestroyImmediate(oldController); summary.Add("Removed NucleusRushController."); }
             if (oldMonitor != null) { Object.DestroyImmediate(oldMonitor); summary.Add("Removed NucleusRushWaveTurnMonitor."); }
 
@@ -289,15 +287,13 @@ namespace CosmicShore.Editor
                 summary.Add("Removed the Cell (Fake Artist v1 is cell-less - see FAKEARTIST.md).");
             }
 
-            // Controller wiring.
+            // Controller wiring (never overwrite a good ref with null - SetIfNotNull).
             {
                 var so = new SerializedObject(controller);
                 so.FindProperty("rule").objectReferenceValue = rule;
                 so.FindProperty("config").objectReferenceValue = config;
-                if (so.FindProperty("countdownTimer") != null)
-                    so.FindProperty("countdownTimer").objectReferenceValue = countdownTimer;
-                if (so.FindProperty("_onToggleReadyButton") != null)
-                    so.FindProperty("_onToggleReadyButton").objectReferenceValue = toggleReadyEvent;
+                SetIfNotNull(so, "countdownTimer", countdownTimer);
+                SetIfNotNull(so, "_onToggleReadyButton", toggleReadyEvent);
                 so.ApplyModifiedPropertiesWithoutUndo();
             }
 
@@ -305,10 +301,8 @@ namespace CosmicShore.Editor
             {
                 var so = new SerializedObject(monitor);
                 so.FindProperty("controller").objectReferenceValue = controller;
-                if (so.FindProperty("gameData") != null)
-                    so.FindProperty("gameData").objectReferenceValue = gameDataAsset;
-                if (so.FindProperty("onUpdateTurnMonitorDisplay") != null)
```

</details>

### `c8b5aa19f` — feat(arcade): Fake Artist round tuning - gallery view, closer/bendier strokes, scaling

_Claude, 2026-07-22 20:16:16 +0000_

```text
Addresses playtest feedback:
- Gallery camera: during voting (and reveal) each client's camera orbits the
  shared painting (CameraManager manual replay rig) and the vote panel moves
  to the lower third, so players study the creation while answering instead
  of staring at where their vessel stopped. Degrades gracefully if no end
  camera is wired.
- Closer strokes: AnchorForRound is now a tight phyllotaxis spiral - round 0
  sits on the spawn cluster and later canvases stay within easy flying
  distance (was a far ~1030u ring).
- Bendier strokes: a stronger curl warp plus a direction-change metric
  (DirectionChanges) - Repartition drops the straightest strokes first and
  keeps the ones that bend in different directions, so a fake artist flying
  start->end straight reads as obviously wrong.
- Recognizable total: strokes-per-player scales up in small groups
  (StrokesPerPlayerFor) to keep the total >= MinTotalStrokes.
- Player-count scaling: 2 fake artists in large groups
  (SecondImposterAtPlayers) and a fake-artist reward that scales with player
  count (ImposterRewardFor). Scorer now takes an imposter-index set; accusing
  any fake artist scores, each fake artist is rewarded.

Tests updated for the multi-imposter scorer + new anchor/bendiness.
```

```text
 Assets/_SO_Assets/Games/FakeArtistConfig.asset                        |  10 ++-
 .../_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs |  68 ++++++++++----
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistConfigSO.cs    |  71 +++++++++++++--
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistController.cs  | 151 ++++++++++++++++++++++----------
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistGalleryCam.cs  |  78 +++++++++++++++++
 .../Controller/Arcade/FakeArtist/FakeArtistGalleryCam.cs.meta         |  11 +++
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistScorer.cs      |  34 ++++---
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistVotePanel.cs   |  14 ++-
 Assets/_Scripts/Tests/EditMode/FakeArtistTests.cs                     |  67 +++++++++++---
 9 files changed, 402 insertions(+), 102 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 955 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs
index ceefeecca..150555b01 100644
--- a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs
+++ b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistArtworkBuilder.cs
@@ -116,19 +116,22 @@ namespace CosmicShore.Gameplay
         }
 
         /// <summary>
-        /// Deterministic artwork anchor for a round: successive rounds place their canvas
-        /// around a golden-angle ring so finished artworks accumulate as a gallery (mass is
-        /// conserved - nothing is culled between rounds; the scene reload on replay is the
-        /// only sink). Rotation faces the canvas back toward the arena center.
+        /// Deterministic artwork anchor for a round. A phyllotaxis (golden-angle) spiral
+        /// with a tight one-artwork gap tiles successive canvases outward so finished
+        /// artworks accumulate as a gallery (mass is conserved - nothing is culled between
+        /// rounds; the scene reload on replay is the only sink) - but the FIRST canvas sits
+        /// right on the spawn cluster and every round stays within easy flying distance, so
+        /// players don't fly far to reach their strokes. Rotation faces the canvas back
+        /// toward the arena center (round 0 keeps the preset's default +Z facing).
         /// </summary>
         public static void AnchorForRound(int roundIndex, float artSize, out Vector3 origin, out Quaternion rotation)
         {
             const float goldenAngle = 137.50776f;
             float angle = roundIndex * goldenAngle;
-            float radius = artSize * 1.35f + 220f;
+            float radius = artSize * 0.62f * Mathf.Sqrt(Mathf.Max(0, roundIndex));
             var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
             origin = dir * radius;
-            rotation = Quaternion.LookRotation(-dir, Vector3.up);
+            rotation = radius > 1f ? Quaternion.LookRotation(-dir, Vector3.up) : Quaternion.identity;
         }
 
         // ── Parametric variation ─────────────────────────────────────────────
@@ -140,8 +143,11 @@ namespace CosmicShore.Gameplay
             float yaw = rng.Range(0f, 360f);
             bool mirror = rng.Chance(0.5f);
             float scale = rng.Range(0.85f, 1.1f);
-            float warpAmp = size * rng.Range(0.015f, 0.045f);
-            float warpFreq = 2.2f / Mathf.Max(1f, size);
+            // A slightly stronger curl warp adds gentle S-bends to otherwise clean preset
+            // arcs, so a fake artist flying a stroke's start->end straight line reads as
+            // obviously wrong - but stays mild enough to keep the subject recognizable.
+            float warpAmp = size * rng.Range(0.03f, 0.06f);
+            float warpFreq = 2.6f / Mathf.Max(1f, size);
             int warpSeed = (int)rng.NextUInt();
 
             var rot = Quaternion.Euler(0f, yaw, 0f);
@@ -188,21 +194,24 @@ namespace CosmicShore.Gameplay
                     work[i].points = Tk.EnforceMaxSegment(work[i].points, maxSeg);
             }
 
-            // Too many strokes: drop the shortest (fine detail) until we fit.
+            // Too many strokes: drop the LEAST INTERESTING first - short AND straight
+            // (a single obvious arc). Keeping the strokes that bend in different directions
+            // means the fake artist, who only gets start+end, can't fake them with a clean
+            // arc. interest = arcLength * (1 + directionChanges).
             while (work.Count > target)
             {
-                int shortest = 0;
-                float shortestLen = float.MaxValue;
+                int drop = 0;
+                float lowest = float.MaxValue;
                 for (int i = 0; i < work.Count; i++)
                 {
-                    float len = ArcLength(work[i].points);
-                    if (len < shortestLen)
+                    float interest = ArcLength(work[i].points) * (1 + DirectionChanges(work[i].points));
+                    if (interest < lowest)
                     {
-                        shortestLen = len;
-                        shortest = i;
+                        lowest = interest;
+                        drop = i;
                     }
                 }
-                work.RemoveAt(shortest);
+                work.RemoveAt(drop);
             }
 
             // Too few strokes: split the longest at its arc midpoint until we fit.
```

</details>

### `dd9fc3969` — docs(arcade): FAKEARTIST.md - gallery view, player-count scaling, bendy strokes

_Claude, 2026-07-22 20:16:50 +0000_

```text
 Assets/_Scripts/Controller/Arcade/FAKEARTIST.md | 22 +++++++++++++++++-----
 1 file changed, 17 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
index a0f900d0c..62c2f239d 100644
--- a/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
+++ b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
@@ -42,9 +42,20 @@ player to the win target (default 8) takes the gallery.
 - **Scoring**: `FakeArtistScoringRuleSO`, `metric = ScoringMetric.Goals`, points (not
   golf). PER-PLAYER rule: `UsesPerPlayerWinner => true` switches EndGameSequencer's win
   check and the Scoreboard banner to `WinnerName`. Round tally (`FakeArtistScorer`, values
-  on `FakeArtistConfig.asset`): +1 correct subject, +1 correct accusation, flat −1 to any
-  player accused by ≥1 voter (imposter or not), +4 to the fake artist every round (a
-  caught imposter nets +3). Totals can go negative.
+  on `FakeArtistConfig.asset`): +1 correct subject, +1 for accusing any fake artist, flat
+  −1 to any player accused by ≥1 voter (imposter or not), and the fake-artist reward
+  (base 4) to each fake artist every round. Totals can go negative.
+- **Player-count scaling** (`FakeArtistConfigSO` helpers): strokes-per-player rises in
+  small groups so the total stays ≥ `MinTotalStrokes` (a recognizable picture); a second
+  fake artist is added at `SecondImposterAtPlayers` (default 8); the fake-artist reward
+  scales up with player count (`ImposterRewardFor`, capped at `ImposterRewardMax`).
+- **Round staging**: canvases anchor on a tight phyllotaxis spiral — round 0 on the spawn
+  cluster, later rounds within easy flying distance (`AnchorForRound`). Stroke selection
+  keeps strokes that bend in different directions (`DirectionChanges`) so a fake artist's
+  start→end shortcut reads as wrong. During voting/reveal a gallery camera
+  (`FakeArtistGalleryCam`, the `CameraManager` manual-replay rig) orbits the shared
+  painting and the vote panel drops to the lower third, so players study the creation
+  while answering.
 - **Turn monitor**: `FakeArtistTurnMonitor` resolves the win target from
   `EndConditionOverridesSO.GetFakeArtistWinTarget()` at StartMonitor (never a per-scene
   field), syncs via NetworkVariable → `GameDataSO.GoalTargetCount`, and ends the turn when
@@ -74,9 +85,10 @@ player to the win target (default 8) takes the gallery.
 
 | Class | Role |
 |---|---|
-| `FakeArtistController` | Round phase machine (Idle → Drawing → Voting → Revealing → Resolved), brush table, targeted deal RPCs, vote collection, tally + reveal, first-to-N final sync |
+| `FakeArtistController` | Round phase machine (Idle → Drawing → Voting → Revealing → Resolved), brush table, targeted deal RPCs, 1-2 fake artists, vote collection, tally + reveal, gallery-camera lifecycle, first-to-N final sync |
 | `FakeArtistBrushes` | The 12 trail identities: slot→(paint domain, shielded) table, synthetic Fire `(Domains)5` / Lime `(Domains)6` material-set minting + registration, per-spawn brush integrity (`PrismKinds.Clear` + `ActivateShield`), UI/ribbon colors |
-| `FakeArtistArtworkBuilder` | Pure/deterministic: preset → seeded parametric variation → repartition to exactly `players x strokesPerPlayer` strokes → flight-order deal; ride-dot extraction; golden-angle round anchors; subject-choice building |
+| `FakeArtistArtworkBuilder` | Pure/deterministic: preset → seeded parametric variation (bendier warp) → repartition (drops straightest strokes, keeps multi-bend ones) to exactly `players x strokesPerPlayer` strokes → flight-order deal; ride-dot extraction; phyllotaxis round anchors near the players; `DirectionChanges` metric; subject-choice building |
+| `FakeArtistGalleryCam` | Per-client vote/reveal camera that orbits the shared painting (CameraManager manual-replay rig); restores gameplay camera on stop |
 | `FakeArtistStrokeGuide` | Local player's private guide: ring markers per dot (imposter: start+end only), distance-latch completion (zero colliders), pen up/down, objective-arrow relay |
 | `FakeArtistRevealGhost` | Post-vote whole-artwork LineRenderer blueprint, regenerated locally from (preset, size, seed) — no geometry crosses the wire pre-vote |
 | `FakeArtistVotePanel` | Runtime-built overlay UI: role card, two-question timed vote, imposter waiting card, reveal card; CanvasGroup fades |
```

</details>

### `b825fdfa2` — feat(arcade): Fake Artist - upper-left dialog + one ring at a time

_Claude, 2026-07-23 00:40:21 +0000_

```text
- Directions/options dialog moved to a compact upper-left corner card (the
  toast spot), left-aligned and narrowed, so the gallery view of the shared
  creation stays unobstructed (was a wide lower-third panel).
- Stroke guide now shows ONLY the immediate next ring, never the whole
  stroke's dots - the next ring spawns as the current one is latched
  (the painting-toy milestone model). Start ring keeps the cone + label,
  end ring keeps the jack.
```

```text
 Assets/_Scripts/Controller/Arcade/FAKEARTIST.md                       |  15 ++--
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs | 128 ++++++++++++--------------------
 Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistVotePanel.cs   |  77 +++++++++----------
 3 files changed, 93 insertions(+), 127 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 400 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
index 62c2f239d..94c2dc10d 100644
--- a/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
+++ b/Assets/_Scripts/Controller/Arcade/FAKEARTIST.md
@@ -10,11 +10,12 @@ directly on the Connect-the-Dots painting toy's stroke toolkit (BACKLOG.md's san
 "separate, score-bearing mode" — the toy itself stays scoreless).
 
 Each round, up to 12 players simultaneously fly 3 assigned strokes of one secret artwork.
-Honest artists get full "connect the dots" ring guides; the fake artist gets only each
-stroke's start and end. Then everyone but the fake artist votes on two questions — *what
-are we drawing?* and *who is the fake artist?* — points land, the full blueprint blooms
-over the painted prisms as the reveal, and the next round starts on a fresh canvas. First
-player to the win target (default 8) takes the gallery.
+Honest artists get "connect the dots" ring guides — one ring at a time, the immediate
+next dot only, never the whole stroke — while the fake artist gets only each stroke's
+start and end. Then everyone but the fake artist votes on two questions — *what are we
+drawing?* and *who is the fake artist?* — points land, the full blueprint blooms over the
+painted prisms as the reveal, and the next round starts on a fresh canvas. First player to
+the win target (default 8) takes the gallery.
 
 - **The drawing IS the deduction surface.** Nothing is announced; players read the
   emerging prism artwork itself. The imposter's improvised middles betray them; the honest
@@ -54,8 +55,8 @@ player to the win target (default 8) takes the gallery.
   keeps strokes that bend in different directions (`DirectionChanges`) so a fake artist's
   start→end shortcut reads as wrong. During voting/reveal a gallery camera
   (`FakeArtistGalleryCam`, the `CameraManager` manual-replay rig) orbits the shared
-  painting and the vote panel drops to the lower third, so players study the creation
-  while answering.
+  painting and the directions/vote dialog tucks into the upper-left corner (the toast
+  spot), so players study the creation while answering.
 - **Turn monitor**: `FakeArtistTurnMonitor` resolves the win target from
   `EndConditionOverridesSO.GetFakeArtistWinTarget()` at StartMonitor (never a per-scene
   field), syncs via NetworkVariable → `GameDataSO.GoalTargetCount`, and ends the turn when
diff --git a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs
index 32ccd8298..8f9b30a69 100644
--- a/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs
+++ b/Assets/_Scripts/Controller/Arcade/FakeArtist/FakeArtistStrokeGuide.cs
@@ -46,7 +46,6 @@ namespace CosmicShore.Gameplay
 
         const float BloomLerpSpeed = 6f;
         const float MarkerDespawnSeconds = 0.45f;
-        const float DimmedDotScale = 0.65f;
 
         /// <summary>Raised once per completed stroke (index into this player's bundle).</summary>
         public event Action<int> StrokeCompleted;
@@ -70,12 +69,9 @@ namespace CosmicShore.Gameplay
         bool _running;
         bool _penHeld;              // true while this guide is holding the pen up
 
-        // Markers of the CURRENT stroke, index-aligned with its dots (retired entries -> null).
-        readonly List<GameObject> _dotMarkers = new();
-        // Per-marker animation target: each marker eases toward its stored scale (bloom-in
-        // to full for the current dot, to DimmedDotScale for the rest) - a single shared
-        // bloomer target would drag dimmed dots back to full size.
-        readonly Dictionary<Transform, float> _markerTargets = new();
+        // Only ONE ring is shown at a time - the immediate NEXT dot to fly to, never the
+        // whole stroke's dots. It blooms in on spawn and scales out when latched.
+        GameObject _currentMarker;
         Transform _objectiveAnchor;
 
         /// <summary>
@@ -111,7 +107,7 @@ namespace CosmicShore.Gameplay
             if (!_running && IsFinished) return;
             _running = false;
             SetPen(true);
-            ClearStrokeMarkers();
+            RetireCurrentMarker();
             if (ObjectiveRelay.Active == this) ObjectiveRelay.Active = null;
         }
 
@@ -126,10 +122,11 @@ namespace CosmicShore.Gameplay
             }
 
             _strokeIndex = 0;
+            _dotIndex = 0;
             StrokesCompletedCount = 0;
             _running = true;
             ObjectiveRelay.Active = this;
-            SpawnStrokeMarkers();
+            SpawnCurrentMarker(); // only the first (start) ring
```

</details>
