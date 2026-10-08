# Branch archive: `claude/fix-multiplayer-scoring-Jsl98`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Fix multiplayer scoring and team colors**

A series of fixes for online matches where scores and team colors were wrong: the scoreboard cards, the player's team (domain) replication over the network, and round stats being reset properly at the start of each round. It also touched Skim Race (HexRace) co-op crystal scoring. Several commits add diagnostic logging, and it went back and forth on approaches.

- **Status:** Abandoned experiment
- **Areas:** multiplayer, scoring, scoreboard, Skim Race
- **Already in bleeding-edge:** No matching commits by subject; bleeding-edge has since reworked these paths (e.g. 9bb4954dc 'fix(vessel): omni card wore the no-team sentinel's colour', the domain-picker fix documented in CLAUDE.md, 826ec4a9f multiplayer rematch work). The specific NetServerDomain/ServerRpc workaround was not found.
- **Risk if deleted:** medium
- **Suggestion (2026-10-08):** can be deleted after archiving — Iterative debugging with diagnostic logs whose root cause (nulled domain-picker wiring) was later fixed differently in bleeding-edge.

## Evidence

- **Last commit:** 2026-05-01 by Claude
- **Unmerged commits:** 19
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/506
- **Forked from:** `16f954ee0` (2026-04-30, engine mix change)
- **Tip:** `a2d689f1b`
- **Files touched (17):**
  - `Assets/_Scripts/Controller/Arcade/HexRaceController.cs`
  - `Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs`
  - `Assets/_Scripts/Controller/Managers/StatsManager.cs`
  - `Assets/_Scripts/Controller/Multiplayer/MenuVesselSelectionPanelController.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`
  - `Assets/_Scripts/Data/Enums/IRoundStats.cs`
  - `Assets/_Scripts/Data/Enums/RoundStats.cs`
  - `Assets/_Scripts/Tests/EditMode/GameDataSOTests.cs`
  - `Assets/_Scripts/Tests/EditMode/IRoundStatsCleanupTests.cs`
  - `Assets/_Scripts/UI/Elements/PlayerScoreEntry.cs`
  - `Assets/_Scripts/UI/Elements/QuickPlayButton.cs`
  - `Assets/_Scripts/UI/HexRaceHUD.cs`
  - `Assets/_Scripts/UI/HexRaceScoreboard.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`
  - `Assets/_Scripts/UI/MultiplayerHUD.cs`

### `e0b6f9043` — fix(hexrace): pool team crystals for co-op scoring + refresh card domain on replication

_Claude, 2026-04-30 18:46:40 +0000_

```text
Two regressions in the multiplayer player score card:

1. Co-op scoring did not "add" teammates' crystals. The race-end check
   (NetworkCrystalCollisionTurnMonitor.CheckForEndOfTurn) and winner detection
   (HexRaceController.OnTurnEndedCustom) both keyed off individual
   CrystalsCollected, so a co-op team where teammates each held 19 of a
   39-crystal target never finished. Both now pool by Domain — the first
   Domain whose summed CrystalsCollected reaches the target wins, and
   independent-team play collapses to the legacy "first individual" rule.
   Live HexRaceHUD cards now show the team-pooled count so co-op teammates
   see shared progress, and the in-game "remaining" UI also pools per team.
   Losing-team penalty score is now based on the team's pooled deficit
   instead of each member's individual deficit, so co-op losing teammates
   display a single shared "X Crystals Left" value on the scoreboard.

2. The client could see Color.white instead of the host's team color on the
   in-game player score card. RoundStats.n_Domain replicated silently —
   the OnValueChanged callback only updated _domainLocal with no event,
   so cards built earlier at turn start kept stale colors when Domain
   landed afterward. Add IRoundStats.OnDomainChanged, raise it from both
   the property setter (non-networked path) and n_Domain.OnValueChanged
   (replication path), and have MultiplayerHUD subscribe per-card to
   refresh the team color via PlayerScoreEntry.SetDomainColor when Domain
   replicates after the card was created. Test mocks updated to satisfy
   the new interface member.
```

```text
 Assets/_Scripts/Controller/Arcade/HexRaceController.cs                | 36 +++++++++++++++++++++++++++------
 .../Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs         | 31 ++++++++++++++++++++++++++--
 Assets/_Scripts/Data/Enums/IRoundStats.cs                             |  8 ++++++++
 Assets/_Scripts/Data/Enums/RoundStats.cs                              | 18 ++++++++++++++++-
 Assets/_Scripts/Tests/EditMode/GameDataSOTests.cs                     |  1 +
 Assets/_Scripts/Tests/EditMode/IRoundStatsCleanupTests.cs             |  1 +
 Assets/_Scripts/UI/Elements/PlayerScoreEntry.cs                       | 14 +++++++++++--
 Assets/_Scripts/UI/HexRaceHUD.cs                                      | 34 +++++++++++++++++++++++++++++--
 Assets/_Scripts/UI/HexRaceScoreboard.cs                               |  5 ++++-
 Assets/_Scripts/UI/MultiplayerHUD.cs                                  | 16 +++++++++++++++
 10 files changed, 150 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 354 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
index 8cbf97cdd..2c68c2f5b 100644
--- a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
@@ -220,28 +220,52 @@ namespace CosmicShore.Gameplay
             if (!IsServer || _raceEnded) return;
 
             int target = ResolveCrystalsToFinishTarget();
-            var winner = gameData.RoundStatsList.FirstOrDefault(s => s.CrystalsCollected >= target);
-            if (winner == null) return;
+
+            // Team-aware win detection: the first Domain whose POOLED CrystalsCollected
+            // reaches the target wins. In co-op (multiple humans share a Domain) this
+            // means teammates' contributions are added together; in independent-team
+            // play each player is their own Domain so this collapses to "first individual
+            // to target" — same as the legacy behavior.
+            var winningGroup = gameData.RoundStatsList
+                .Where(s => s != null && s.Domain != Domains.Unassigned && s.Domain != Domains.None)
+                .GroupBy(s => s.Domain)
+                .FirstOrDefault(g => g.Sum(s => s.CrystalsCollected) >= target);
+
+            if (winningGroup == null) return;
+
+            Domains winningDomain = winningGroup.Key;
+
+            // The "winner" name is the highest individual contributor on the winning team —
+            // used as the tie-break label and legacy WinnerName consumer. Victory/defeat
+            // attribution in end-game screens uses WinnerDomain (domain equality), not name,
+            // so teammates correctly see the victory cinematic.
+            var winner = winningGroup.OrderByDescending(s => s.CrystalsCollected).First();
 
             _raceEnded = true;
 
             // All players share the same elapsed time since turn start.
             // The score tracker updates LocalRoundStats.Score every frame with elapsed time.
             float finishTime = gameData.LocalRoundStats?.Score ?? 0f;
-            Domains winningDomain = winner.Domain;
 
-            // Team-aware scoring: every player on the winner's Domain inherits the
+            // Team-aware scoring: every player on the winning Domain inherits the
             // finish time — they share the victory. Players on other domains get the
-            // losing penalty score based on their own crystals remaining.
+            // losing penalty score based on their TEAM's pooled crystals remaining,
+            // so co-op losing-team members display a single shared "X Crystals Left"
+            // value instead of each member showing their own individual deficit.
             foreach (var stats in gameData.RoundStatsList)
             {
+                if (stats == null) continue;
+
                 if (stats.Domain == winningDomain)
                 {
                     stats.Score = finishTime;
                 }
                 else
                 {
-                    int crystalsLeft = Mathf.Max(0, target - stats.CrystalsCollected);
+                    int teamTotal = gameData.RoundStatsList
+                        .Where(s => s != null && s.Domain == stats.Domain)
+                        .Sum(s => s.CrystalsCollected);
+                    int crystalsLeft = Mathf.Max(0, target - teamTotal);
                     stats.Score = 10000f + crystalsLeft;
                 }
             }
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
index 7ee773ffc..2342e2aa7 100644
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
+++ b/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
@@ -12,6 +12,11 @@ namespace CosmicShore.Gameplay
     /// resolves the crystal target (from inspector override, waypoints, or default),
     /// this subclass syncs it to all clients via NetworkVariable and publishes it
     /// to <see cref="GameDataSO.CrystalTargetCount"/> so any system can read it.
+    ///
+    /// End-of-turn check is team-aware: a team's pooled <c>CrystalsCollected</c> hitting
+    /// the target ends the race. In co-op (multiple humans on the same Domain) this means
+    /// teammates progress together; in solo/independent-team play each player is their
+    /// own team so the behavior matches the legacy "first individual to target" rule.
     /// </summary>
     public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
     {
@@ -59,7 +64,12 @@ namespace CosmicShore.Gameplay
```

</details>

### `90715ab5d` — fix(scoring): revert team-pool, push NetDomain/NetName to RoundStats

_Claude, 2026-04-30 19:17:28 +0000_

```text
The previous commit's team-pool change misread the bug. The user wants
INDIVIDUAL per-player scoring on the in-game player score card; pooling made
both teammates' cards display the same total, which manifested as "when I
hit a crystal, both players' cards go up". Revert NetworkCrystalCollisionTurnMonitor,
HexRaceController.OnTurnEndedCustom, HexRaceHUD, and HexRaceScoreboard to
their pre-pool behavior — first individual to reach the target wins, cards
show individual OmniCrystalsCollected.

Keep the OnDomainChanged event added in the previous commit (it's a defensive
fix for the white-domain-on-client bug) and add two related defensive pushes
in Player so RoundStats stays in sync with NetworkVariable state:

- Player.OnNetDomainChanged now writes RoundStats.Domain server-side when
  NetDomain replicates. RoundStats.Domain was previously only set during
  InitializeForMultiplayerMode and went stale if a player picked their team
  after their Player+Vessel pair had already initialized — leaving clients
  showing Color.white on the score card.

- Player.OnNetNameValueChanged now mirrors Name to RoundStats.Name server-side
  when NetName replicates. Without this, the score card lookup key (registered
  under the old name) drifts away from live update tags when the cloud profile
  loads after spawn (HandleProfileLoadedAfterSpawn).
```

```text
 Assets/_Scripts/Controller/Arcade/HexRaceController.cs                | 36 ++++++---------------------------
 .../Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs         | 31 ++--------------------------
 Assets/_Scripts/Controller/Player/Player.cs                           | 22 ++++++++++++++++++--
 Assets/_Scripts/UI/HexRaceHUD.cs                                      | 35 +++-----------------------------
 Assets/_Scripts/UI/HexRaceScoreboard.cs                               |  5 +----
 5 files changed, 32 insertions(+), 97 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 236 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
index 2c68c2f5b..8cbf97cdd 100644
--- a/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
+++ b/Assets/_Scripts/Controller/Arcade/HexRaceController.cs
@@ -220,52 +220,28 @@ namespace CosmicShore.Gameplay
             if (!IsServer || _raceEnded) return;
 
             int target = ResolveCrystalsToFinishTarget();
-
-            // Team-aware win detection: the first Domain whose POOLED CrystalsCollected
-            // reaches the target wins. In co-op (multiple humans share a Domain) this
-            // means teammates' contributions are added together; in independent-team
-            // play each player is their own Domain so this collapses to "first individual
-            // to target" — same as the legacy behavior.
-            var winningGroup = gameData.RoundStatsList
-                .Where(s => s != null && s.Domain != Domains.Unassigned && s.Domain != Domains.None)
-                .GroupBy(s => s.Domain)
-                .FirstOrDefault(g => g.Sum(s => s.CrystalsCollected) >= target);
-
-            if (winningGroup == null) return;
-
-            Domains winningDomain = winningGroup.Key;
-
-            // The "winner" name is the highest individual contributor on the winning team —
-            // used as the tie-break label and legacy WinnerName consumer. Victory/defeat
-            // attribution in end-game screens uses WinnerDomain (domain equality), not name,
-            // so teammates correctly see the victory cinematic.
-            var winner = winningGroup.OrderByDescending(s => s.CrystalsCollected).First();
+            var winner = gameData.RoundStatsList.FirstOrDefault(s => s.CrystalsCollected >= target);
+            if (winner == null) return;
 
             _raceEnded = true;
 
             // All players share the same elapsed time since turn start.
             // The score tracker updates LocalRoundStats.Score every frame with elapsed time.
             float finishTime = gameData.LocalRoundStats?.Score ?? 0f;
+            Domains winningDomain = winner.Domain;
 
-            // Team-aware scoring: every player on the winning Domain inherits the
+            // Team-aware scoring: every player on the winner's Domain inherits the
             // finish time — they share the victory. Players on other domains get the
-            // losing penalty score based on their TEAM's pooled crystals remaining,
-            // so co-op losing-team members display a single shared "X Crystals Left"
-            // value instead of each member showing their own individual deficit.
+            // losing penalty score based on their own crystals remaining.
             foreach (var stats in gameData.RoundStatsList)
             {
-                if (stats == null) continue;
-
                 if (stats.Domain == winningDomain)
                 {
                     stats.Score = finishTime;
                 }
                 else
                 {
-                    int teamTotal = gameData.RoundStatsList
-                        .Where(s => s != null && s.Domain == stats.Domain)
-                        .Sum(s => s.CrystalsCollected);
-                    int crystalsLeft = Mathf.Max(0, target - teamTotal);
+                    int crystalsLeft = Mathf.Max(0, target - stats.CrystalsCollected);
                     stats.Score = 10000f + crystalsLeft;
                 }
             }
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
index 2342e2aa7..7ee773ffc 100644
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
+++ b/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkCrystalCollisionTurnMonitor.cs
@@ -12,11 +12,6 @@ namespace CosmicShore.Gameplay
     /// resolves the crystal target (from inspector override, waypoints, or default),
     /// this subclass syncs it to all clients via NetworkVariable and publishes it
     /// to <see cref="GameDataSO.CrystalTargetCount"/> so any system can read it.
-    ///
-    /// End-of-turn check is team-aware: a team's pooled <c>CrystalsCollected</c> hitting
-    /// the target ends the race. In co-op (multiple humans on the same Domain) this means
-    /// teammates progress together; in solo/independent-team play each player is their
-    /// own team so the behavior matches the legacy "first individual to target" rule.
     /// </summary>
     public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
     {
@@ -64,12 +59,7 @@ namespace CosmicShore.Gameplay
```

</details>

### `928a49292` — fix(player): use RoundStats property for lazy init in NetDomain/NetName replication handlers

_Claude, 2026-04-30 19:48:39 +0000_

```text
The previous commit's defensive sync used the private _roundStats field, which
stays null until something else accesses the RoundStats property. If
OnNetDomainChanged or OnNetNameValueChanged fired before
InitializeForMultiplayerMode (the first caller), the null check skipped the
RoundStats update — leaving the bug in place.

Switch to accessing through the RoundStats property so GetOrAdd lazy-initializes
the reference on first replication callback.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 10 ++++++----
 1 file changed, 6 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 8d5de2f04..dcce85012 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -404,8 +404,9 @@ namespace CosmicShore.Gameplay
             // is only set once during InitializeForMultiplayerMode and can become stale —
             // resulting in clients displaying Color.white on the player score card when
             // a player picks a team after their Player+Vessel pair has already initialized.
-            if (IsServer && _roundStats != null && _roundStats.IsSpawned)
-                _roundStats.Domain = newValue;
+            // Use the RoundStats property so the cached reference is lazy-initialized.
+            if (IsServer && RoundStats is RoundStats rs && rs.IsSpawned)
+                rs.Domain = newValue;
         }
 
         void OnNetNameValueChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
@@ -416,8 +417,9 @@ namespace CosmicShore.Gameplay
             // NetName changes (e.g. cloud profile loads after spawn — see
             // HandleProfileLoadedAfterSpawn). Without this, the card was registered under
             // the old name and live updates fail to match.
-            if (IsServer && _roundStats != null && _roundStats.IsSpawned)
-                _roundStats.Name = Name;
+            // Use the RoundStats property so the cached reference is lazy-initialized.
+            if (IsServer && RoundStats is RoundStats rs && rs.IsSpawned)
+                rs.Name = Name;
 
             TryRaiseDeferredSpawnEvent();
         }
```

</details>

### `c5c5a1f67` — fix(scoreboard): re-key cards by IRoundStats; full color+score refresh on turn start

_Claude, 2026-04-30 20:06:01 +0000_

```text
The user reports persistent host→client desync on the in-game player score
card despite the previous Domain-replication fixes. Two more defensive
improvements:

1. Re-key in-game cards by IRoundStats reference (stable) in addition to the
   legacy name-keyed map. NetName replication can land after CreateCardForPlayer
   ran with an empty/old name — making name-keyed UpdatePlayerCard miss every
   subsequent crystal-collected event. The new dictionary tracks the same
   cards by RoundStats reference so live updates land regardless of name
   changes.

2. RefreshAllPlayerCards (fires on OnMiniGameTurnStarted) now re-applies BOTH
   the team color AND the score from the current authoritative IRoundStats,
   keyed by reference. This catches cases where Domain or score replicated
   in the millis between InitializePlayerCards and the same-frame
   RefreshAllPlayerCards call — which would otherwise leave non-owner cards
   showing default Color.white or stale crystal counts.

HexRaceHUD.HandleCrystalStatChanged now uses the new stats-keyed
UpdatePlayerCard overload so live OmniCrystalsCollected updates always reach
the right card.
```

```text
 Assets/_Scripts/UI/HexRaceHUD.cs     |  5 ++++-
 Assets/_Scripts/UI/MultiplayerHUD.cs | 37 +++++++++++++++++++++++++++++++++----
 2 files changed, 37 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 111 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/HexRaceHUD.cs b/Assets/_Scripts/UI/HexRaceHUD.cs
index c27029e6a..fac6f6f3b 100644
--- a/Assets/_Scripts/UI/HexRaceHUD.cs
+++ b/Assets/_Scripts/UI/HexRaceHUD.cs
@@ -22,7 +22,10 @@ namespace CosmicShore.UI
         private void HandleCrystalStatChanged(IRoundStats updatedStats)
         {
             if (updatedStats == null) return;
-            UpdatePlayerCard(updatedStats.Name, updatedStats.OmniCrystalsCollected);
+            // Use the stats-reference-keyed update path so live score updates still
+            // land on the right card even if NetName replicates after the card was
+            // created (which would have made the legacy name-keyed lookup miss).
+            UpdatePlayerCard(updatedStats, updatedStats.OmniCrystalsCollected);
         }
     }
 }
diff --git a/Assets/_Scripts/UI/MultiplayerHUD.cs b/Assets/_Scripts/UI/MultiplayerHUD.cs
index 1157b7ef9..7caef9b31 100644
--- a/Assets/_Scripts/UI/MultiplayerHUD.cs
+++ b/Assets/_Scripts/UI/MultiplayerHUD.cs
@@ -10,6 +10,9 @@ namespace CosmicShore.UI
         [Header("Multiplayer View")]
         [SerializeField] protected MultiplayerHUDView multiplayerView;
 
+        // Cards are keyed by the IRoundStats reference (stable across name changes)
+        // and a parallel name-keyed map exists for legacy callers (UpdatePlayerCard).
+        protected Dictionary<IRoundStats, PlayerScoreEntry> _cardsByStats = new();
         protected Dictionary<string, PlayerScoreEntry> _playerCards = new();
 
         protected override void OnEnable()
@@ -42,13 +45,21 @@ namespace CosmicShore.UI
             }
         }
 
+        /// <summary>
+        /// Defensive refresh on turn start: re-applies team color AND score to every
+        /// card from the current authoritative <see cref="IRoundStats"/>, in case the
+        /// card was built with stale values during the replication race window
+        /// (Domain or score landed after CreateCardForPlayer ran).
+        /// </summary>
         void RefreshAllPlayerCards()
         {
             if (gameData?.RoundStatsList == null) return;
 
             foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
             {
-                UpdatePlayerCard(stats.Name, GetInitialCardValue(stats));
+                if (!_cardsByStats.TryGetValue(stats, out var card) || card == null) continue;
+                card.SetDomainColor(view.GetColorForDomain(stats.Domain));
+                card.UpdateScore(GetInitialCardValue(stats));
             }
         }
 
@@ -77,12 +88,14 @@ namespace CosmicShore.UI
             UnsubscribeFromAllStats();
             UnsubscribeFromGameSpecificEvents();
             _playerCards.Clear();
+            _cardsByStats.Clear();
         }
 
         private void InitializePlayerCards()
         {
             view.ClearPlayerList();
             _playerCards.Clear();
+            _cardsByStats.Clear();
             AssignAIProfiles();
 
             for (int i = 0; i < gameData.RoundStatsList.Count; i++)
@@ -113,6 +126,7 @@ namespace CosmicShore.UI
             card.SetAvatar(avatarSprite);
 
             _playerCards[stats.Name] = card;
+            _cardsByStats[stats] = card;
 
             // Refresh team-color when stats.Domain replicates after the card was created.
             // RoundStats.n_Domain replication can land after the turn-start UI build,
@@ -135,10 +149,14 @@ namespace CosmicShore.UI
         private void HandleDomainChanged(IRoundStats updatedStats)
         {
             if (updatedStats == null) return;
```

</details>

### `a30d4f32b` — fix(multiplayer-scoring): gate StatsManager + crystal impactor on NetworkManager.IsServer

_Claude, 2026-04-30 20:19:02 +0000_

```text
Root cause of "client shows 8, host shows 0" desync: StatsManager._allowRecord
defaulted to true and was supposed to flip to false on clients via its
NetcodeHooks.OnNetworkSpawn callback. But StatsManager lives in the Bootstrap
scene as DontDestroyOnLoad, and its NetcodeHooks NetworkObject is not part
of any scene that's network-loaded — so the spawn callback never fires
reliably, and on clients _allowRecord stayed at the default true.

Combined with any race where OmniCrystalImpactor.IsNetworkClient() returned
false on the client (Crystal.CrystalManager.IsSpawned briefly false right after
crystal Instantiate, before InjectDependencies-triggered NetworkBehaviour state
caught up), the client raised OnCrystalCollected locally → StatsManager.CrystalCollected
fired with _allowRecord still true → s.OmniCrystalsCollected++ → property setter
updated _omniCrystalsCollectedLocal locally without writing n_OmniCrystalsCollected
(server-only). Result: client locally tracked its own count, server stayed at 0,
and the two never reconciled.

Both gates now query NetworkManager.Singleton directly:
- StatsManager: replace the cached _allowRecord field with an AllowRecord
  property that returns true when there's no active NetworkManager (singleplayer)
  or when this machine is the server. The OnNetworkSpawn callback becomes a
  no-op kept only for inspector-wiring backward compat.
- OmniCrystalImpactor.IsNetworkClient: same change, using
  NetworkManager.Singleton.IsListening + !IsServer instead of the
  CrystalManager NetworkBehaviour state.

This is the authoritative gate — no caches, no spawn-callback dependency,
no chance of stale state surviving a scene reload.
```

```text
 .../Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs         | 14 ++++++++-
 Assets/_Scripts/Controller/Managers/StatsManager.cs                   | 54 +++++++++++++++++++++------------
 2 files changed, 48 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 171 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs
index 3edbb3693..2b9541878 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs
@@ -23,7 +23,19 @@ namespace CosmicShore.Gameplay
         /// </summary>
         protected virtual bool IsDomainMatching(Domains domain) => true;
 
-        bool IsNetworkClient() => Crystal.CrystalManager.IsSpawned && !Crystal.CrystalManager.IsServer;
+        /// <summary>
+        /// True when this machine is a non-server client in an active networked session.
+        /// Querying <see cref="NetworkManager.Singleton"/> directly (instead of going
+        /// through <see cref="Crystal.CrystalManager"/>) avoids a class of races where
+        /// CrystalManager hadn't finished spawning yet, the IsSpawned check returned
+        /// false, and the client mistakenly processed the trigger locally — silently
+        /// double-counting crystals on the client while the server tracked them too.
+        /// </summary>
+        bool IsNetworkClient()
+        {
+            var nm = NetworkManager.Singleton;
+            return nm != null && nm.IsListening && !nm.IsServer;
+        }
 
         protected override void AcceptImpactee(IImpactor impactee)
         {
diff --git a/Assets/_Scripts/Controller/Managers/StatsManager.cs b/Assets/_Scripts/Controller/Managers/StatsManager.cs
index 3be6f94cf..087e98b94 100644
--- a/Assets/_Scripts/Controller/Managers/StatsManager.cs
+++ b/Assets/_Scripts/Controller/Managers/StatsManager.cs
@@ -49,7 +49,28 @@ namespace CosmicShore.Gameplay
         [SerializeField]
         NetcodeHooks _netcodeHooks;
 
-        bool _allowRecord = true;
+        /// <summary>
+        /// True when this machine is allowed to mutate stats. In multiplayer, only the
+        /// server records — clients receive replicated values via NetworkVariable.
+        /// In singleplayer (no NetworkManager listening), every machine records its own.
+        ///
+        /// We query <see cref="Unity.Netcode.NetworkManager"/> directly instead of relying
+        /// on a cached flag set by <see cref="OnNetworkSpawn"/>: <see cref="StatsManager"/>
+        /// lives in the Bootstrap scene (DontDestroyOnLoad), and its <see cref="NetcodeHooks"/>
+        /// NetworkObject is not part of any network-loaded scene, so its OnNetworkSpawn
+        /// callback is unreliable. Without this fix, the cached flag stayed at the default
+        /// <c>true</c> on clients and the client locally double-incremented every crystal
+        /// collection — manifesting as "client shows 8, host shows 0" desync.
+        /// </summary>
+        bool AllowRecord
+        {
+            get
+            {
+                var nm = Unity.Netcode.NetworkManager.Singleton;
+                if (nm == null || !nm.IsListening) return true; // singleplayer or no network yet
+                return nm.IsServer;                              // multiplayer: server only
+            }
+        }
 
         void OnEnable()
         {
@@ -65,18 +86,13 @@ namespace CosmicShore.Gameplay
 
         void OnNetworkSpawn()
         {
-            if (_netcodeHooks.IsServer)
-            {
-                _allowRecord = true;
-                return;
-            }
-
-            _allowRecord = false;
+            // No-op — kept for backward compatibility with existing inspector wiring.
+            // Authoritative gating now lives in the AllowRecord property.
         }
 
         public void LifeformCreated(int cellID)
         {
-            if (!_allowRecord || cellData == null) return;
+            if (!AllowRecord || cellData == null) return;
 
             var cellStatsList = cellData.CellStatsList;
```

</details>

### `6044b190e` — fix(player): ClientRpc-broadcast RoundStats reset to clear menu-mode leftovers

_Claude, 2026-05-01 17:32:08 +0000_

```text
User reported: client always starts the game with non-zero crystal counts and
shows white domain on its own card. Root cause: the persistent Player
NetworkObject (DestroyWithScene=false) carries RoundStats from menu scene to
game scene, and the client's local _xxxLocal fields can drift from the
server's authoritative state because:

1. In menu mode (before commit a30d4f3), the client's StatsManager._allowRecord
   stayed true on clients, so the client locally incremented its own
   OmniCrystalsCollected when its vessel hit menu crystals. Server stayed at 0.

2. When entering the game scene, server's PrepareForNewScene calls
   RoundStats.Cleanup() which writes 0 through every NetworkVariable. Netcode
   skips OnValueChanged when the new value equals the current value, so a
   server-side reset of an already-zero NetworkVariable doesn't reach clients
   whose local fields had drifted.

3. Same pattern for Domain: client's _domainLocal caught the default
   Domains.Unassigned at first spawn (before the server wrote n_Domain), and
   subsequent server writes of the same value never replicated. Card showed
   white because GetColorForDomain(Unassigned) falls through to Color.white.

Fix: PrepareForNewScene now broadcasts a ClientRpc carrying the authoritative
Domain and Name, and the RPC body runs Cleanup() on every machine plus
explicitly writes Domain and Name through the property setters. The setter on
a non-server spawned client only touches the local _xxxLocal fields (it skips
the server-only NetworkVariable write), which is exactly the alignment we
need without re-touching NetworkVariables that don't need to replicate.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 45 +++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 45 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index dcce85012..325b71012 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -281,6 +281,21 @@ namespace CosmicShore.Gameplay
             // update NetworkVariables on the server. Name/Domain are re-set below.
             RoundStats.Cleanup();
 
+            // Broadcast the reset to all clients. Without this, stats that the client
+            // accumulated locally in menu mode (or that drifted between server and
+            // client local fields) would persist into the game scene — NetworkVariable
+            // replication is skipped when the new value equals the current value, so
+            // a server-side reset of an already-zero NetworkVariable doesn't reach
+            // clients with non-zero local fields. The RPC also pushes the
+            // authoritative Domain + Name so the client's RoundStats local fields
+            // align even when n_Domain / n_Name replication is a no-op.
+            if (IsServer)
+            {
+                ResetStatsLocal_ClientRpc(
+                    NetDomain.Value,
+                    new Unity.Collections.FixedString64Bytes(NetName.Value.ToString()));
+            }
+
             // Reset input state (joystick positions, throttle, flags).
             InputStatus?.ResetForReplay();
 
@@ -335,6 +350,36 @@ namespace CosmicShore.Gameplay
             Destroy(gameObject);
         }
 
+        /// <summary>
+        /// Called on every client (and host) to reset the local <see cref="RoundStats"/>
+        /// fields when entering a new scene. RoundStats lives on a persistent Player
+        /// NetworkObject (DestroyWithScene=false), so its local <c>_xxxLocal</c> fields
+        /// can carry values from the menu scene (or from earlier code that incremented
+        /// stats client-side). Cleanup() resets all local fields immediately on this
+        /// machine; on the server it also writes 0 through every NetworkVariable, but
+        /// Netcode skips replication when the new value equals the current value, so
+        /// the client's locally-drifted fields wouldn't otherwise be cleared.
+        /// </summary>
+        [ClientRpc]
+        void ResetStatsLocal_ClientRpc(Domains domain, Unity.Collections.FixedString64Bytes name)
+        {
+            // RoundStats getter lazy-creates the component. Cleanup() runs through the
+            // property setters, which on a non-server client take the !IsSpawned-or-server
+            // path that writes only the local field — exactly what we want here.
+            var rs = RoundStats;
+            if (rs == null) return;
+            rs.Cleanup();
+
+            // Push the authoritative Domain + Name as well. Cleanup() doesn't touch
+            // these (they aren't gameplay stats), but on the client side they can be
+            // stale: the initial spawn sync caught a default value before the server
+            // wrote n_Domain/n_Name, and subsequent server writes of the same value
+            // never replicated. Writing through the property setter on a non-server
+            // client updates the local field without re-touching the NetworkVariable.
+            rs.Domain = domain;
+            rs.Name = name.ToString();
+        }
+
         public void StartPlayer()
         {
             // Vessel can be null on non-host clients when a ClientRpc (e.g. countdown end)
```

</details>

### `852204279` — chore(player): add diagnostic logging to ResetStatsLocal_ClientRpc

_Claude, 2026-05-01 17:48:30 +0000_

```text
User still sees stale crystal counts on client after the previous fix.
Add before/after value logging in the ClientRpc body so we can confirm:
1. The RPC actually fires on the client
2. The reset writes (Cleanup, Domain, Name) actually take effect
3. Whether something else writes _omniCrystalsCollectedLocal back up after the reset

Per-machine [FLOW-4] log line shows IsServer/IsOwner context plus the
before→after values for OmniCrystalsCollected, CrystalsCollected, and Domain.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 11 +++++++++++
 1 file changed, 11 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 325b71012..12a32686f 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -368,6 +368,11 @@ namespace CosmicShore.Gameplay
             // path that writes only the local field — exactly what we want here.
             var rs = RoundStats;
             if (rs == null) return;
+
+            int beforeOmni = rs.OmniCrystalsCollected;
+            int beforeCrystals = rs.CrystalsCollected;
+            var beforeDomain = rs.Domain;
+
             rs.Cleanup();
 
             // Push the authoritative Domain + Name as well. Cleanup() doesn't touch
@@ -378,6 +383,12 @@ namespace CosmicShore.Gameplay
             // client updates the local field without re-touching the NetworkVariable.
             rs.Domain = domain;
             rs.Name = name.ToString();
+
+            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] ResetStatsLocal_ClientRpc on '{Name}' " +
+                $"(IsServer={IsServer}, IsOwner={IsOwner}) — " +
+                $"OmniCrystals: {beforeOmni}→{rs.OmniCrystalsCollected}, " +
+                $"Crystals: {beforeCrystals}→{rs.CrystalsCollected}, " +
+                $"Domain: {beforeDomain}→{rs.Domain}</color>");
         }
 
         public void StartPlayer()
```

</details>

### `87db9533e` — fix(scoring): fall back to Jade when NetDomain is Unassigned + always raise OnDomainChanged on local writes

_Claude, 2026-05-01 18:09:02 +0000_

```text
Diagnostic logs from the user confirmed:
1. ResetStatsLocal_ClientRpc fires correctly on the client (OmniCrystals: 1→0,
   13→0 — RPC body works, local fields get reset).
2. Server's view of the client's NetDomain reads as Domains.Unassigned even
   when the user picked a team. Cause: NetDomain is owner-writable, so the
   server's local NetworkVariable instance starts at default(Domains)=Unassigned
   and only gets the owner's value via initial sync — for remote clients that
   never wrote NetDomain, or where initial sync hasn't propagated by the time
   PrepareForNewScene reads it, the server sees Unassigned. The RPC then
   pushed Unassigned to RoundStats.Domain on every client, causing the
   in-game player score card to display Color.white (no entry for
   Domains.Unassigned in the HUD's domainColors palette).

Fix:
- Player.PrepareForNewScene now validates the domain server-side: if
  NetDomain.Value is Unassigned/None, fall back to Jade. The fallback value
  is force-written to RoundStats.Domain (server-write var, no permission
  issue) so all clients receive the corrected color via NetworkVariable
  replication, and is also passed in the RPC payload.
- RoundStats.Domain setter now raises OnDomainChanged on any local write
  whose value differs from the previous value (not just the !IsSpawned
  branch). The HUD card subscribes to OnDomainChanged via MultiplayerHUD,
  but ResetStatsLocal_ClientRpc writes the property locally without
  touching n_Domain, so the OnValueChanged-driven raise never fired and the
  card kept its stale white color even after the RPC corrected the data.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 18 +++++++++++++++++-
 Assets/_Scripts/Data/Enums/RoundStats.cs    | 16 ++++++++++++----
 2 files changed, 29 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 12a32686f..43b6c6d44 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -289,10 +289,26 @@ namespace CosmicShore.Gameplay
             // clients with non-zero local fields. The RPC also pushes the
             // authoritative Domain + Name so the client's RoundStats local fields
             // align even when n_Domain / n_Name replication is a no-op.
+            //
+            // NetDomain is owner-writable, so the server's view of a remote client's
+            // NetDomain can be Domains.Unassigned (the C# default(T)) when the client
+            // never picked a team or when initial-sync hasn't propagated by this
+            // point. Fall back to Jade so the score card always renders a valid team
+            // color (Color.white otherwise — there is no Unassigned entry in the HUD
+            // inspector palette).
             if (IsServer)
             {
+                var domain = NetDomain.Value;
+                if (domain == Domains.Unassigned || domain == Domains.None)
+                    domain = Domains.Jade;
+
+                // Force-write through the server-write n_Domain so all clients receive
+                // the corrected value even if NetDomain itself stays at Unassigned.
+                if (RoundStats is RoundStats rs && rs.IsSpawned)
+                    rs.Domain = domain;
+
                 ResetStatsLocal_ClientRpc(
-                    NetDomain.Value,
+                    domain,
                     new Unity.Collections.FixedString64Bytes(NetName.Value.ToString()));
             }
 
diff --git a/Assets/_Scripts/Data/Enums/RoundStats.cs b/Assets/_Scripts/Data/Enums/RoundStats.cs
index d199dad9a..0e9fc66ad 100644
--- a/Assets/_Scripts/Data/Enums/RoundStats.cs
+++ b/Assets/_Scripts/Data/Enums/RoundStats.cs
@@ -226,10 +226,18 @@ namespace CosmicShore.Data
                 _domainLocal = value;
                 if (IsSpawned && IsServer) n_Domain.Value = value;
 
-                // Server-side and non-networked: raise immediately. The client side
-                // raises from n_Domain.OnValueChanged so it fires after replication
-                // (skipping the duplicate raise here when IsSpawned).
-                if (!IsSpawned && changed)
+                // Raise OnDomainChanged on any local write that actually changed the
+                // value. The server side typically also gets a duplicate raise from
+                // n_Domain.OnValueChanged when its own write replicates back, but
+                // OnValueChanged only fires on actual value changes — so a same-value
+                // write on the server, or a forced server-side correction (see
+                // Player.PrepareForNewScene), would otherwise miss the event.
+                // Non-server clients also need this path: ResetStatsLocal_ClientRpc
+                // writes the property locally without touching n_Domain, so the
+                // OnValueChanged path doesn't fire either; without this raise, the
+                // HUD card subscribed to OnDomainChanged would not refresh its
+                // team color when the RPC fixes a stale local domain.
+                if (changed)
                     RaiseSpecific(OnDomainChanged);
             }
         }
```

</details>

### `65afb1f22` — revert(scoring): drop Jade fallback for Unassigned NetDomain + add server-side diagnostic

_Claude, 2026-05-01 18:24:27 +0000_

```text
Per user request — they want proper sync, not a fallback to mask the underlying
issue. The diagnostic logs showed the server reading NetDomain.Value as
Domains.Unassigned for the client's player, which my previous commit covered up
with a Jade default. Removing the fallback restores the actual symptom so we
can chase the real cause.

Add a server-side log in PrepareForNewScene: prints NetDomain.Value, the
player's NetName, OwnerClientId, and IsLocalUser at the moment the RPC is
about to fire. This lets us tell (a) whether the server reads the same
Unassigned value as what's in the RPC payload, and (b) whether the timing is
"server reads stale value before owner's initial sync" or "owner never wrote".

Also need from the user:
- The full RPC log line for Shombith (the AFTER value was truncated in the
  screenshot — I need to know whether it stays at Unassigned or transitions
  to a real domain via subsequent OnNetDomainChanged hooks).
- Confirmation of what team they picked on each client.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 21 ++++-----------------
 1 file changed, 4 insertions(+), 17 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 43b6c6d44..ff073973f 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -289,26 +289,13 @@ namespace CosmicShore.Gameplay
             // clients with non-zero local fields. The RPC also pushes the
             // authoritative Domain + Name so the client's RoundStats local fields
             // align even when n_Domain / n_Name replication is a no-op.
-            //
-            // NetDomain is owner-writable, so the server's view of a remote client's
-            // NetDomain can be Domains.Unassigned (the C# default(T)) when the client
-            // never picked a team or when initial-sync hasn't propagated by this
-            // point. Fall back to Jade so the score card always renders a valid team
-            // color (Color.white otherwise — there is no Unassigned entry in the HUD
-            // inspector palette).
             if (IsServer)
             {
-                var domain = NetDomain.Value;
-                if (domain == Domains.Unassigned || domain == Domains.None)
-                    domain = Domains.Jade;
-
-                // Force-write through the server-write n_Domain so all clients receive
-                // the corrected value even if NetDomain itself stays at Unassigned.
-                if (RoundStats is RoundStats rs && rs.IsSpawned)
-                    rs.Domain = domain;
-
+                Debug.Log($"<color=#FFA500>[FLOW-4] [Player.PrepareForNewScene] Server reads " +
+                    $"NetDomain.Value={NetDomain.Value} for Player '{NetName.Value}' " +
+                    $"(OwnerClientId={OwnerClientId}, IsLocalUser={IsLocalUser})</color>");
                 ResetStatsLocal_ClientRpc(
-                    domain,
+                    NetDomain.Value,
                     new Unity.Collections.FixedString64Bytes(NetName.Value.ToString()));
             }
 
```

</details>

### `ddd058a3c` — fix(player): owner re-establishes NetDomain initial value at spawn (Netcode quirk)

_Claude, 2026-05-01 18:47:53 +0000_

```text
Diagnostic logs revealed the root cause of the white-domain bug:

Host's [FLOW-4]: Server reads NetDomain.Value=Unassigned for Player 'Shombith'
Client's [FLOW-4]: OnNetworkSpawn DONE — Name=Shombith, Domain=Unassigned

The Player.NetDomain field initializer specifies Domains.Jade as the initial
value (`new(Domains.Jade, ...)`), but at OnNetworkSpawn — even on the OWNER
side — NetDomain.Value reads as Unassigned. Netcode's spawn-message
deserialization overwrites the owner-side m_InternalValue with default(T) for
owner-write NetworkVariables, ignoring the constructor argument. The server
side then has no way to learn the intended initial value: it can't write
itself (owner-write permission), and the owner never wrote because — from
the owner's perspective — the value already exists.

This made the symptom non-obvious: NormalizeHumanDomains, InitializeForMultiplayerMode,
PrepareForNewScene, and the OnNetDomainChanged hook all read NetDomain.Value
to drive RoundStats.Domain, and they all read Unassigned. The score card's
GetColorForDomain(Unassigned) falls through to Color.white because the HUD
inspector palette has no entry for Unassigned. (The field initializer's
intent was specifically to avoid this — Domains.Jade exists in the palette.)

Fix: in Player.OnNetworkSpawn, after the existing owner-side writes
(NetName/NetAvatarId/NetDefaultVesselType), the owner now re-establishes a
non-Unassigned NetDomain value. The owner-side write replicates to the server
normally, so the server reads Jade instead of Unassigned. Subsequent
team-selection writes through MenuVesselSelectionPanelController or the
ArcadeGameConfigureModal overwrite Jade as expected.

This is not a fallback at read time — the data flows through the same
owner-write path the rest of the system relies on, just with the missing
initial write that Netcode's spawn deserialization swallowed.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 16 ++++++++++++++++
 1 file changed, 16 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index ff073973f..b2f749f0e 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -194,6 +194,22 @@ namespace CosmicShore.Gameplay
                 // vessel selection in multiplayer.
                 if (!IsValidVesselTypeForSpawn(NetDefaultVesselType.Value))
                     NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
+
+                // Owner-write NetworkVariables can land at default(T) on the owner
+                // side post-spawn even when the field initializer specifies a
+                // non-default value: Netcode's spawn-message deserialization
+                // overwrites m_InternalValue with default(Domains) = Unassigned for
+                // owner-write vars on the owner. Without this owner-side write,
+                // server's view stays at Unassigned forever (no replication signal,
+                // because owner never wrote), the score-card team color falls
+                // through to Color.white, and PrepareForNewScene's RPC carries
+                // Unassigned into RoundStats.Domain on every client.
+                // Re-establish the field-initializer's intended Jade default so
+                // owner-write replication picks it up immediately. Subsequent team
+                // selections through MenuVesselSelectionPanelController or the
+                // ArcadeGameConfigureModal overwrite this normally.
+                if (NetDomain.Value == Domains.Unassigned || NetDomain.Value == Domains.None)
+                    NetDomain.Value = Domains.Jade;
             }
 
             // --- Raise spawn event AFTER all local writes ---
```

</details>

### `15cf7a1ed` — fix(player): server-authoritative RoundStats.Domain assignment in PrepareForNewScene

_Claude, 2026-05-01 19:11:25 +0000_

```text
User's diagnostic: 'jade domain and old scores again. Just reset the score
when the client joins from the server and set the domain'.

Root cause confirmed by [FLOW-4] logs:
  Server reads NetDomain.Value=Jade for Player 'dragon' ✓ (host's own — owner=server)
  Server reads NetDomain.Value=Unassigned for Player 'Shombith' ❌ (client's)
  OnNetworkSpawn DONE — Name=Shombith, Domain=Unassigned (on client side too)

NetDomain is owner-write. Netcode 2.x's spawn-message deserialization in MPPM
overwrites the owner-side m_InternalValue with default(Domains)=Unassigned,
ignoring the field-initializer's Domains.Jade. The owner's view ends up at
Unassigned, never fires OnValueChanged (no 'change' from default), and
nothing replicates to the server. The server's view stays at Unassigned for
the client's player forever — until the user explicitly picks a team.

Per the user's request, make the gameplay side server-authoritative and
bypass NetDomain entirely:
- Server picks the canonical domain in PrepareForNewScene. Preferred source
  is NetDomain.Value when it's a real team (Jade/Ruby/Gold/Blue); when it's
  Unassigned/None (the spawn-quirk default), fall back to
  DomainAssigner.GetDomainsByGameModes() — the same logic the rest of the
  codebase uses for team assignment.
- Server force-writes the canonical domain through RoundStats.Domain. That
  NetworkVariable is server-write, has no permission issues, and replicates
  reliably. RoundStats.Domain is what the in-game score card actually reads
  via stats.Domain in MultiplayerHUD.GetColorForDomain.
- The same value is broadcast in ResetStatsLocal_ClientRpc so each client's
  local RoundStats._domainLocal aligns immediately, including before
  NetworkVariable replication delivers it.

Score reset is unchanged (RoundStats.Cleanup() + RPC body Cleanup() run on
every machine), addressing the 'old scores again' part of the report.

The user's owner-write NetDomain still tracks their UI-side team selection
for any code path that reads it directly, but anything that drives the
score-card team color now goes through the server-authoritative
RoundStats.Domain path and is immune to the spawn-deserialization quirk.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 43 +++++++++++++++++++++++++++++++------------
 1 file changed, 31 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index b2f749f0e..065e54bd2 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -297,21 +297,40 @@ namespace CosmicShore.Gameplay
             // update NetworkVariables on the server. Name/Domain are re-set below.
             RoundStats.Cleanup();
 
-            // Broadcast the reset to all clients. Without this, stats that the client
-            // accumulated locally in menu mode (or that drifted between server and
-            // client local fields) would persist into the game scene — NetworkVariable
-            // replication is skipped when the new value equals the current value, so
-            // a server-side reset of an already-zero NetworkVariable doesn't reach
-            // clients with non-zero local fields. The RPC also pushes the
-            // authoritative Domain + Name so the client's RoundStats local fields
-            // align even when n_Domain / n_Name replication is a no-op.
+            // Server-authoritative reset + domain assignment for the joining/persistent
+            // player. The owner-write NetDomain has a Netcode quirk in MPPM where the
+            // spawn-message deserialization swallows the field-initializer's Jade value
+            // and seats m_InternalValue at default(Domains)=Unassigned, with no
+            // OnValueChanged because the owner's "no write" looks like no change. The
+            // server then can't read NetDomain reliably and the score card falls
+            // through to Color.white.
+            //
+            // Bypass NetDomain on the gameplay side entirely: the server picks the
+            // canonical domain (preferring NetDomain when valid, falling back to
+            // DomainAssigner for unique-team-per-player), force-writes it through the
+            // server-write RoundStats.Domain (no permission issue, no spawn quirk),
+            // and broadcasts the same value in the ClientRpc so each client's local
+            // RoundStats._domainLocal aligns immediately. Clients still see whatever
+            // owner-write value NetDomain ends up with, but the score card uses
+            // RoundStats.Domain (n_Domain) which is always server-authoritative.
             if (IsServer)
             {
-                Debug.Log($"<color=#FFA500>[FLOW-4] [Player.PrepareForNewScene] Server reads " +
-                    $"NetDomain.Value={NetDomain.Value} for Player '{NetName.Value}' " +
-                    $"(OwnerClientId={OwnerClientId}, IsLocalUser={IsLocalUser})</color>");
+                var domain = NetDomain.Value;
+                bool fromNetDomain = domain != Domains.Unassigned && domain != Domains.None;
+
+                if (!fromNetDomain)
+                    domain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
+
+                Debug.Log($"<color=#FFA500>[FLOW-4] [Player.PrepareForNewScene] Server-authoritative domain " +
+                    $"assignment for '{NetName.Value}' (OwnerClientId={OwnerClientId}): " +
+                    $"NetDomain.Value={NetDomain.Value} → using {domain} " +
+                    $"(source={(fromNetDomain ? "NetDomain" : "DomainAssigner")})</color>");
+
+                if (RoundStats is RoundStats rs && rs.IsSpawned)
+                    rs.Domain = domain;
+
                 ResetStatsLocal_ClientRpc(
-                    NetDomain.Value,
+                    domain,
                     new Unity.Collections.FixedString64Bytes(NetName.Value.ToString()));
             }
 
```

</details>

### `7be338179` — fix(roundstats): NotifyAllStatsChanged() to refresh HUD after server-driven reset

_Claude, 2026-05-01 20:57:35 +0000_

```text
Final missing piece: ResetStatsLocal_ClientRpc was correctly resetting
client-side _xxxLocal fields and pushing the server-authoritative Domain,
but the in-game player score card's _displayedScore stayed stale.

Cause: every IRoundStats property setter (Score, OmniCrystalsCollected,
Domain, etc.) suppresses its OnXxxChanged event when IsSpawned is true on
a non-server client — they rely on n_xxx.OnValueChanged to fire the event
after replication. Cleanup() runs every setter on the client, but
NetworkVariable.OnValueChanged only fires on actual value changes; if the
server already had 0 (or the matching value), no replication, no callback,
and the HUD card subscribed to OnOmniCrystalsCollectedChanged never gets
notified to redraw its number.

Fix: add RoundStats.NotifyAllStatsChanged() that explicitly invokes every
per-stat event (OnScoreChanged, OnDomainChanged, OnOmniCrystalsCollected
Changed, plus the rest of the IRoundStats event surface). Call it from the
end of ResetStatsLocal_ClientRpc after Cleanup() + Domain/Name writes —
this forces every subscribed HUD card to re-read the now-correct _local
fields. The card's UpdateScore animates from its stale _displayedScore down
to 0 (or the actual current value), and SetDomainColor lands on the right
team palette entry.

Combined with the previous server-authoritative domain assignment in
PrepareForNewScene, this completes the user's request: server resets score
on client-join AND server sets the domain, and both reach the visible UI.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 10 +++++++++-
 Assets/_Scripts/Data/Enums/RoundStats.cs    | 31 +++++++++++++++++++++++++++++++
 2 files changed, 40 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 065e54bd2..8f1fe10e9 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -404,7 +404,7 @@ namespace CosmicShore.Gameplay
             // RoundStats getter lazy-creates the component. Cleanup() runs through the
             // property setters, which on a non-server client take the !IsSpawned-or-server
             // path that writes only the local field — exactly what we want here.
-            var rs = RoundStats;
+            var rs = RoundStats as RoundStats;
             if (rs == null) return;
 
             int beforeOmni = rs.OmniCrystalsCollected;
@@ -422,6 +422,14 @@ namespace CosmicShore.Gameplay
             rs.Domain = domain;
             rs.Name = name.ToString();
 
+            // Force-fire every OnXxxChanged event so HUD subscribers (score cards)
+            // refresh their cached _displayedScore and team color — without this the
+            // property setters' !IsSpawned guard suppresses event raising on a spawned
+            // client, leaving the card showing menu-mode leftovers indefinitely
+            // because n_xxx.OnValueChanged only fires on actual NetworkVariable
+            // value changes (and our reset-to-0 may equal the existing server value).
+            rs.NotifyAllStatsChanged();
+
             Debug.Log($"<color=#00FF00>[FLOW-4] [Player] ResetStatsLocal_ClientRpc on '{Name}' " +
                 $"(IsServer={IsServer}, IsOwner={IsOwner}) — " +
                 $"OmniCrystals: {beforeOmni}→{rs.OmniCrystalsCollected}, " +
diff --git a/Assets/_Scripts/Data/Enums/RoundStats.cs b/Assets/_Scripts/Data/Enums/RoundStats.cs
index 0e9fc66ad..8fec926fc 100644
--- a/Assets/_Scripts/Data/Enums/RoundStats.cs
+++ b/Assets/_Scripts/Data/Enums/RoundStats.cs
@@ -192,6 +192,37 @@ namespace CosmicShore.Data
             OnAnyStatChanged?.Invoke(this);
         }
 
+        /// <summary>
+        /// Forces every per-stat OnXxxChanged event to fire. Used after a server-driven
+        /// reset (Player.ResetStatsLocal_ClientRpc) when the property setters wrote
+        /// new local values without going through the n_xxx.OnValueChanged callback —
+        /// HUD subscribers (score cards) would otherwise keep their cached display
+        /// values until the next legitimate NetworkVariable change.
+        /// </summary>
+        public void NotifyAllStatsChanged()
+        {
+            OnScoreChanged?.Invoke();
+            RaiseSpecific(OnDomainChanged);
+            RaiseSpecific(OnBlocksCreatedChanged);
+            RaiseSpecific(OnBlocksDestroyedChanged);
+            RaiseSpecific(OnBlocksRestoredChanged);
+            RaiseSpecific(OnPrismsStolenChanged);
+            RaiseSpecific(OnPrismsRemainingChanged);
+            RaiseSpecific(OnFriendlyPrismsDestroyedChanged);
+            RaiseSpecific(OnHostilePrismsDestroyedChanged);
+            RaiseSpecific(OnVolumeCreatedChanged);
+            RaiseSpecific(OnTotalVolumeDestroyedChanged);
+            RaiseSpecific(OnFriendlyVolumeDestroyedChanged);
+            RaiseSpecific(OnHostileVolumeDestroyedChanged);
+            RaiseSpecific(OnVolumeRestoredChanged);
+            RaiseSpecific(OnVolumeStolenChanged);
+            RaiseSpecific(OnVolumeRemainingChanged);
+            RaiseSpecific(OnCrystalsCollectedChanged);
+            RaiseSpecific(OnOmniCrystalsCollectedChanged);
+            RaiseSpecific(OnElementalCrystalsCollectedChanged);
+            RaiseSpecific(OnJoustCollisionChanged);
+        }
+
         /// <summary>
         /// Allows external callers (e.g. StatsManager) to fire OnJoustCollisionChanged
         /// without needing access to the private event backing field.
```

</details>

### `95f75f696` — fix(player): keep IRoundStats reference for default interface Cleanup() call

_Claude, 2026-05-01 20:59:41 +0000_

```text
Compile error from previous commit:
  CS1061: 'RoundStats' does not contain a definition for 'Cleanup'

IRoundStats.Cleanup() is a default interface method, callable only through
the IRoundStats type — not the concrete RoundStats class. The previous
commit downcast to RoundStats too early and broke the call. Keep the
IRoundStats-typed reference for Cleanup() + the property writes, downcast
inline for the concrete NotifyAllStatsChanged() call afterwards.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 7 +++++--
 1 file changed, 5 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 8f1fe10e9..5315895d5 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -404,7 +404,10 @@ namespace CosmicShore.Gameplay
             // RoundStats getter lazy-creates the component. Cleanup() runs through the
             // property setters, which on a non-server client take the !IsSpawned-or-server
             // path that writes only the local field — exactly what we want here.
-            var rs = RoundStats as RoundStats;
+            // Cleanup() is a default interface method on IRoundStats, so we keep the
+            // interface-typed reference for that call and downcast for the concrete
+            // RoundStats.NotifyAllStatsChanged() call afterwards.
+            var rs = RoundStats;
             if (rs == null) return;
 
             int beforeOmni = rs.OmniCrystalsCollected;
@@ -428,7 +431,7 @@ namespace CosmicShore.Gameplay
             // client, leaving the card showing menu-mode leftovers indefinitely
             // because n_xxx.OnValueChanged only fires on actual NetworkVariable
             // value changes (and our reset-to-0 may equal the existing server value).
-            rs.NotifyAllStatsChanged();
+            (rs as RoundStats)?.NotifyAllStatsChanged();
 
             Debug.Log($"<color=#00FF00>[FLOW-4] [Player] ResetStatsLocal_ClientRpc on '{Name}' " +
                 $"(IsServer={IsServer}, IsOwner={IsOwner}) — " +
```

</details>

### `dd41f5cef` — fix(player): bypass broken NetDomain replication with NetServerDomain + ServerRpc

_Claude, 2026-05-01 21:13:12 +0000_

```text
User reported the score card never matches what the vessel/crystals show on
the client. Vessel and crystals read Player.Domain (set locally by the owner),
which is correct on the client. The score card reads RoundStats.Domain
(server-write), seeded from NetDomain.Value on the server — which Netcode
2.x in MPPM never receives because the owner-write replication of NetDomain
is unreliable (spawn-message deserialization seats m_InternalValue at
default(Domains)=Unassigned and subsequent owner writes don't always
propagate).

Fix: stop relying on owner-write NetDomain replication for the score-card
pipeline. Add a server-write NetServerDomain that the owner explicitly
populates via a dedicated ServerRpc — this path is reliable.

Player.cs:
  - New NetworkVariable<Domains> NetServerDomain (server-write).
  - RequestDomainChange(Domains) — owner-side helper. Writes NetDomain
    locally so vessel/crystal local systems see the change immediately,
    AND calls SyncDomainToServer_ServerRpc to push the value to the server.
  - [ServerRpc(RequireOwnership=true)] SyncDomainToServer_ServerRpc(domain)
    — server writes NetServerDomain, mirrors to local Player.Domain, and
    force-writes RoundStats.Domain so remote clients refresh immediately.
  - Player.OnNetworkSpawn — owner now calls SyncDomainToServer_ServerRpc
    after re-establishing NetDomain to Jade, so the server has the value
    even if the user never explicitly picks a team.
  - PrepareForNewScene now prefers NetServerDomain.Value (definitive)
    over NetDomain.Value (unreliable), with DomainAssigner as last resort
    if neither has a real team. Diagnostic log shows the source.

Updated team-selection writers to use RequestDomainChange:
  - MenuVesselSelectionPanelController.OnTeamSelected
  - ArcadeGameConfigureModal.HandleTeamSelected
  - ArcadeGameConfigureModal.HandleDomainSelected
  - QuickPlayButton (random domain assignment)

The user's "vessel takes the color correctly but the card does not update"
was the cleanest possible diagnostic: vessel uses the local-correct
Player.Domain, score card uses the server-driven RoundStats.Domain, and
the gap between them is exactly the unreliable-owner-write replication
that NetServerDomain bypasses.
```

```text
 .../Controller/Multiplayer/MenuVesselSelectionPanelController.cs      |   7 +-
 Assets/_Scripts/Controller/Player/Player.cs                           | 124 ++++++++++++++++++++++++++------
 Assets/_Scripts/UI/Elements/QuickPlayButton.cs                        |   6 +-
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                 |  13 +++-
 4 files changed, 124 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 232 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/MenuVesselSelectionPanelController.cs b/Assets/_Scripts/Controller/Multiplayer/MenuVesselSelectionPanelController.cs
index 4753d3f0a..d6ec5b99d 100644
--- a/Assets/_Scripts/Controller/Multiplayer/MenuVesselSelectionPanelController.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/MenuVesselSelectionPanelController.cs
@@ -144,7 +144,12 @@ namespace CosmicShore.Gameplay
         {
             if (Player is not Player player) return;
             if (!player.IsOwner) return;
-            player.NetDomain.Value = domain;
+            // RequestDomainChange writes NetDomain locally AND pushes the value
+            // to the server via SyncDomainToServer_ServerRpc — owner-write
+            // NetDomain replication isn't reliable in MPPM, so the explicit
+            // ServerRpc is what actually gets the team to the server's score-card
+            // pipeline (RoundStats.Domain).
+            player.RequestDomainChange(domain);
         }
 
         // ---------------------------------------------------------
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 5315895d5..eaa2e3da5 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -34,8 +34,65 @@ namespace CosmicShore.Gameplay
         public NetworkVariable<bool> NetIsAI = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
         public NetworkVariable<int> NetAvatarId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
 
+        /// <summary>
+        /// Server-write mirror of the owner's NetDomain. Exists because Netcode 2.x
+        /// owner-write NetworkVariable replication is unreliable in MPPM — the spawn-
+        /// deserialization swallows the field initializer and subsequent owner writes
+        /// don't always reach the server. Owners explicitly push their team to this
+        /// var via SyncDomainToServer_ServerRpc, so the server (and the score card,
+        /// which reads RoundStats.Domain seeded from this var) always has the
+        /// authoritative value the owner actually picked. Default Jade so even if
+        /// the ServerRpc never fires, the score card still has a valid color.
+        /// </summary>
+        public NetworkVariable<Domains> NetServerDomain = new(Domains.Jade, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
+
         public Domains Domain { get; private set; } = Domains.Jade;
 
+        /// <summary>
+        /// Owner-side helper: sets the local NetDomain (so the local vessel/crystal
+        /// systems pick up the new team color immediately) AND pushes the value to
+        /// the server's authoritative NetServerDomain. Use this from any UI that
+        /// changes the player's team (TeamSelectionPanel, MenuVesselSelectionPanel
+        /// Controller, ArcadeGameConfigureModal) instead of writing NetDomain.Value
+        /// directly — the direct-write path doesn't replicate to the server in
+        /// MPPM, leaving the score card's team color stuck on the field-initializer
+        /// default.
+        /// </summary>
+        public void RequestDomainChange(Domains domain)
+        {
+            if (!IsOwner) return;
+            NetDomain.Value = domain;
+            if (!IsServer)
+                SyncDomainToServer_ServerRpc(domain);
+            else if (NetServerDomain.Value != domain)
+                NetServerDomain.Value = domain;
+        }
+
+        /// <summary>
+        /// Owner-only ServerRpc that mirrors the owner's chosen NetDomain into the
+        /// server-write NetServerDomain. Server validates by checking the sender
+        /// owns this NetworkObject (RequireOwnership=true). The score card's
+        /// authoritative team color flows from this NetworkVariable through
+        /// RoundStats.Domain in PrepareForNewScene.
+        /// </summary>
+        [ServerRpc(RequireOwnership = true)]
+        void SyncDomainToServer_ServerRpc(Domains domain)
+        {
+            if (NetServerDomain.Value != domain)
+                NetServerDomain.Value = domain;
+
+            // Mirror to local Player.Domain so server-side systems that read
+            // Player.Domain (NetworkCrystalManager domain assignment, score
+            // tracker, etc.) see the right value without waiting for the next
+            // OnNetDomainChanged tick.
+            Domain = domain;
+
+            // Mirror to RoundStats.Domain too if the component is already spawned —
```

</details>

### `8b10447da` — chore(roundstats): add OmniCrystalsCollected replication diagnostic

_Claude, 2026-05-01 21:27:48 +0000_

```text
User reports score not counting on the client even after the
NetServerDomain ServerRpc fix. Add a [FLOW-STATS] log to the
n_OmniCrystalsCollected.OnValueChanged callback so we can tell:
- Whether the NetworkVariable is actually replicating to the client
- Whether the local _omniCrystalsCollectedLocal updates when it does
- Whether OnValueChanged fires at all on the client side

If the log fires on the client when the server increments, replication
works and the bug is in the HUD card refresh path. If it doesn't fire,
NetworkVariable replication itself is the issue and we need a different
sync mechanism (explicit RPC carrying stat values).
```

```text
 Assets/_Scripts/Data/Enums/RoundStats.cs | 2 ++
 1 file changed, 2 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/RoundStats.cs b/Assets/_Scripts/Data/Enums/RoundStats.cs
index 8fec926fc..0311e35e9 100644
--- a/Assets/_Scripts/Data/Enums/RoundStats.cs
+++ b/Assets/_Scripts/Data/Enums/RoundStats.cs
@@ -839,6 +839,8 @@ namespace CosmicShore.Data
             n_OmniCrystalsCollected.OnValueChanged += (_, v) =>
             {
                 _omniCrystalsCollectedLocal = v;
+                Debug.Log($"<color=#00BFFF>[FLOW-STATS] n_OmniCrystalsCollected.OnValueChanged for '{_nameLocal}' " +
+                    $"(IsServer={IsServer}, IsOwner={IsOwner}) → _local={v}</color>");
                 RaiseSpecific(OnOmniCrystalsCollectedChanged);
             };
 
```

</details>

### `c7e503ab6` — fix(roundstats): add UnityEngine import for Debug.Log

_Claude, 2026-05-01 21:34:32 +0000_

```text
CS0103: 'Debug' not in context — RoundStats.cs only imported System,
CosmicShore.Data, Unity.Netcode, Unity.Collections. Add UnityEngine for
the diagnostic log added in the previous commit.
```

```text
 Assets/_Scripts/Data/Enums/RoundStats.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/RoundStats.cs b/Assets/_Scripts/Data/Enums/RoundStats.cs
index 0311e35e9..ca0c976b0 100644
--- a/Assets/_Scripts/Data/Enums/RoundStats.cs
+++ b/Assets/_Scripts/Data/Enums/RoundStats.cs
@@ -2,6 +2,7 @@ using System;
 using CosmicShore.Data;
 using Unity.Netcode;
 using Unity.Collections;
+using UnityEngine;
 namespace CosmicShore.Data
 {
     public class RoundStats : NetworkBehaviour, IRoundStats
```

</details>

### `5877113ed` — chore(multiplayerhud): add diagnostic logs to card creation and update

_Claude, 2026-05-01 21:43:42 +0000_

```text
Replication is confirmed working ([FLOW-STATS] fires on the client). The
bug is in the HUD card refresh path. Add [FLOW-CARD] logs to:
- CreateCardForPlayer: log stats hash, name, initial value, domain
- UpdatePlayerCard(IRoundStats, int): log whether the dictionary lookup
  succeeded, and what the stats hash was vs what's stored

This will tell us:
(A) If the stats hash at card creation differs from the stats hash at
    update time → reference mismatch (different RoundStats instances)
(B) If found=false but the name matches an existing card → dictionary
    lookup is broken (key inequality despite same name)
(C) If UpdatePlayerCard never fires for the client's score updates →
    HandleCrystalStatChanged isn't being called → subscription is broken
(D) If found=true and card is non-null but the card visually doesn't
    change → bug is in PlayerScoreEntry.UpdateScore animation
```

```text
 Assets/_Scripts/UI/MultiplayerHUD.cs | 10 +++++++++-
 1 file changed, 9 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/MultiplayerHUD.cs b/Assets/_Scripts/UI/MultiplayerHUD.cs
index 7caef9b31..a6762cb1d 100644
--- a/Assets/_Scripts/UI/MultiplayerHUD.cs
+++ b/Assets/_Scripts/UI/MultiplayerHUD.cs
@@ -128,6 +128,10 @@ namespace CosmicShore.UI
             _playerCards[stats.Name] = card;
             _cardsByStats[stats] = card;
 
+            Debug.Log($"<color=#FFFF00>[FLOW-CARD] CreateCardForPlayer — name='{stats.Name}', " +
+                $"statsHash={stats.GetHashCode()}, initialValue={GetInitialCardValue(stats)}, " +
+                $"domain={stats.Domain}, _cardsByStats.Count={_cardsByStats.Count}</color>");
+
             // Refresh team-color when stats.Domain replicates after the card was created.
             // RoundStats.n_Domain replication can land after the turn-start UI build,
             // which would otherwise leave non-owner cards showing the default Color.white.
@@ -180,7 +184,11 @@ namespace CosmicShore.UI
         protected void UpdatePlayerCard(IRoundStats stats, int newValue)
         {
             if (stats == null) return;
-            if (_cardsByStats.TryGetValue(stats, out var card) && card != null)
+            bool found = _cardsByStats.TryGetValue(stats, out var card);
+            Debug.Log($"<color=#FFFF00>[FLOW-CARD] UpdatePlayerCard(stats) — name='{stats.Name}', " +
+                $"statsHash={stats.GetHashCode()}, newValue={newValue}, found={found}, " +
+                $"cardNull={card == null}, _cardsByStats.Count={_cardsByStats.Count}</color>");
+            if (found && card != null)
                 card.UpdateScore(newValue);
         }
     }
```

</details>

### `6e1775d38` — fix(scoring): re-reset RoundStats at countdown-end to clear cinematic-time hits

_Claude, 2026-05-01 21:52:29 +0000_

```text
Logs from the user's last test pinpointed the score-not-zero bug:

  [FLOW-CARD] CreateCardForPlayer — name='Shombith', initialValue=5,
  domain=Jade, _cardsByStats.Count=1
  [FLOW-CARD] UpdatePlayerCard for 'dragon' — found=True, cardNull=False,
  newValue=1

Card update path works (found=True for dragon when host hits a crystal).
The actual bug: Shombith starts the turn already at 5. The pre-game
cinematic moves the vessel through spawned crystals; the server's
StatsManager processes those collisions because gameplay-side gating
treats the world as live. By the time OnMiniGameTurnStarted runs and
CreateCardForPlayer reads stats.OmniCrystalsCollected, the value is 5,
not 0 — so the card's _displayedScore is initialized to 5 and never
visually drops to 0.

Fix: add a second-pass reset at the SERVER's OnCountdownTimerEnded (just
before the client-side StartTurn RPC fires). Iterate gameData.Players,
call a new Player.ResetStatsForTurnStart() that:
  1. Runs RoundStats.Cleanup() server-side, zeroing _local + writing 0
     through every n_xxx NetworkVariable (replicates to clients).
  2. Re-broadcasts ResetStatsLocal_ClientRpc with the canonical
     NetServerDomain so each client's local fields and HUD subscribers
     are explicitly aligned to 0.

The domain-side RoundStats.Domain is preserved (it was already set
authoritatively in PrepareForNewScene from NetServerDomain or
DomainAssigner). Subsequent in-game crystal hits replicate normally
and update cards via OnOmniCrystalsCollectedChanged.
```

```text
 Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs | 14 ++++++++++++++
 Assets/_Scripts/Controller/Player/Player.cs                           | 27 +++++++++++++++++++++++++++
 2 files changed, 41 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
index 164d494a0..91e0b1300 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerDomainGamesController.cs
@@ -24,6 +24,20 @@ namespace CosmicShore.Gameplay
                 return;
 
             Debug.Log($"<color=#00CED1>[FLOW-9] [DomainGamesCtrl] OnCountdownTimerEnded (server) — activating players. Players={gameData.Players.Count}, RoundStats={gameData.RoundStatsList.Count}</color>");
+
+            // Cinematic-time crystal collisions (vessel moves through the spawned
+            // crystals during the pre-game cinematic) inflate RoundStats before the
+            // turn officially starts. Wipe each player's stats one more time here,
+            // server-side, so the score cards created in OnMiniGameTurnStarted read
+            // 0 instead of whatever leaked in during the cinematic. Each Player has
+            // its own ResetStatsLocal_ClientRpc that broadcasts the cleanup to all
+            // clients including the host.
+            for (int i = gameData.Players.Count - 1; i >= 0; i--)
+            {
+                if (gameData.Players[i] is Player p && p.IsSpawned)
+                    p.ResetStatsForTurnStart();
+            }
+
             OnCountdownTimerEnded_ClientRpc();
         }
 
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index eaa2e3da5..e2e9f152d 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -472,6 +472,33 @@ namespace CosmicShore.Gameplay
             Destroy(gameObject);
         }
 
+        /// <summary>
+        /// Server-only entry point that wipes RoundStats one more time right at
+        /// turn start, after the pre-game cinematic finished. Cinematic-time
+        /// vessel/crystal collisions get processed by the server's StatsManager
+        /// even though gameplay hasn't officially started; this leaves the score
+        /// cards (created at OnMiniGameTurnStarted) initialized with non-zero
+        /// values. Re-running Cleanup + the ResetStatsLocal_ClientRpc broadcast
+        /// here lets the cards read 0 at creation.
+        /// </summary>
+        public void ResetStatsForTurnStart()
+        {
+            if (!IsServer) return;
+            if (RoundStats is RoundStats rs && rs.IsSpawned)
+            {
+                rs.Cleanup();
+                // Preserve domain (server-write n_Domain) — it's the canonical
+                // value computed in PrepareForNewScene from NetServerDomain or
+                // DomainAssigner. Don't disturb it with a re-write.
+            }
+
+            // Broadcast the same Cleanup to all clients via the existing RPC so
+            // their _local fields and HUD subscribers are aligned at zero.
+            ResetStatsLocal_ClientRpc(
+                NetServerDomain.Value,
+                new Unity.Collections.FixedString64Bytes(NetName.Value.ToString()));
+        }
+
         /// <summary>
         /// Called on every client (and host) to reset the local <see cref="RoundStats"/>
         /// fields when entering a new scene. RoundStats lives on a persistent Player
```

</details>

### `a2d689f1b` — fix(player): call Cleanup through IRoundStats reference (default interface method)

_Claude, 2026-05-01 21:55:37 +0000_

```text
Same CS1061 compile error as before — IRoundStats.Cleanup() is a default
interface method, only callable through the interface type, not the
concrete RoundStats class. Keep the IRoundStats reference for Cleanup,
do the IsSpawned check via a local 'concrete' downcast.
```

```text
 Assets/_Scripts/Controller/Player/Player.cs | 5 ++++-
 1 file changed, 4 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index e2e9f152d..a4bf8c420 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -484,7 +484,10 @@ namespace CosmicShore.Gameplay
         public void ResetStatsForTurnStart()
         {
             if (!IsServer) return;
-            if (RoundStats is RoundStats rs && rs.IsSpawned)
+            // Cleanup() is a default interface method on IRoundStats — call through
+            // the interface type, not the concrete RoundStats class.
+            var rs = RoundStats;
+            if (rs != null && rs is RoundStats concrete && concrete.IsSpawned)
             {
                 rs.Cleanup();
                 // Preserve domain (server-write n_Domain) — it's the canonical
```

</details>
