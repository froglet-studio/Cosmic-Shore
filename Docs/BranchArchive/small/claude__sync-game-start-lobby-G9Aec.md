# Branch archive: `claude/sync-game-start-lobby-G9Aec`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `a0e6025e6` (2026-02-28, Add party UI prefabs)
- **Tip:** `87484ed6d`
- **Files touched (2):**
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs`

### `87484ed6d` — feat(arcade): sync game config to GameDataSO and use dynamic AI backfill from lobby

_Claude, 2026-02-28 10:21:08 +0000_

```text
ArcadeGameConfigureModal.OnStartGameClicked() now syncs SceneName,
GameMode, and IsMultiplayerMode from the selected SO_ArcadeGame into
GameDataSO before launching. Player count uses the lobby's
PartyMembers.Count with AI backfill for remaining slots via
RequestedAIBackfillCount.

ServerPlayerVesselInitializerWithAI now reads RequestedAIBackfillCount
instead of the serialized aiInitializeDatas array to determine how many
AI opponents to spawn. When the count is 0, no AI spawns. AI names come
from SO_AIProfileList and domains from DomainAssigner.
```

```text
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     | 39 +++++++++++++++++++--------------
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs                 | 23 +++++++++++++++++++
 2 files changed, 46 insertions(+), 16 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 151 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index 74164a1f3..41788d203 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -1,7 +1,9 @@
+using System.Collections.Generic;
 using CosmicShore.Data;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
 using Reflex.Injectors;
+using Unity.Collections;
 using Unity.Netcode;
 using UnityEngine;
 
@@ -12,6 +14,9 @@ namespace CosmicShore.Gameplay
     /// spawns server-owned AI players and their vessels, then delegates
     /// human player handling to the base class via OnPlayerNetworkSpawnedUlong.
     ///
+    /// AI count is driven by <see cref="GameDataSO.RequestedAIBackfillCount"/>.
+    /// When it is 0, no AI players are spawned.
+    ///
     /// OnNetworkSpawn flow:
     ///   1. SpawnAIs() — creates AI players + vessels (fires OnPlayerNetworkSpawnedUlong
     ///      for each, but we haven't subscribed yet so the base ignores them)
@@ -26,15 +31,12 @@ namespace CosmicShore.Gameplay
         [Tooltip("NetworkObject prefab that contains your Player component (must be a registered NetworkPrefab).")]
         [SerializeField] NetworkObject aiPlayerPrefab;
 
-        [Tooltip("The data needed to spawn AI")]
-        [SerializeField] IPlayer.InitializeData[] aiInitializeDatas;
-
         [Header("AI Ship Selection")]
         [Tooltip("Game list used to look up available ships for AI opponents. If unset, AI defaults to Sparrow.")]
         [SerializeField] SO_GameList gameList;
 
         [Header("AI Profiles")]
-        [Tooltip("Optional AI profile list for assigning unique names to AI opponents.")]
+        [Tooltip("AI profile list for assigning unique names/avatars to AI opponents.")]
         [SerializeField] SO_AIProfileList aiProfileList;
 
         protected override void OnNetworkSpawn()
@@ -65,18 +67,21 @@ namespace CosmicShore.Gameplay
 
         void SpawnAIs()
         {
+            int aiCount = gameData.RequestedAIBackfillCount;
+            if (aiCount <= 0) return;
+
             if (!aiPlayerPrefab)
             {
                 CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] aiPlayerPrefab is not assigned.");
                 return;
             }
 
-            for (int i = 0; i < aiInitializeDatas.Length; i++)
-            {
-                var data = aiInitializeDatas[i];
-                if (!data.AllowSpawning)
-                    return;
+            List<AIProfile> profiles = aiProfileList != null
+                ? aiProfileList.PickRandom(aiCount)
+                : null;
 
+            for (int i = 0; i < aiCount; i++)
+            {
                 var aiPlayerNO = Instantiate(aiPlayerPrefab);
                 GameObjectInjector.InjectRecursive(aiPlayerNO.gameObject, _container);
 
@@ -93,13 +98,15 @@ namespace CosmicShore.Gameplay
                     continue;
                 }
 
-                var aiVesselType = data.vesselClass;
-                if (aiVesselType == VesselClassType.Any || aiVesselType == VesselClassType.Random)
-                    aiVesselType = PickAIVesselType();
+                var aiVesselType = PickAIVesselType();
+                string aiName = profiles != null && i < profiles.Count
+                    ? profiles[i].Name
+                    : $"AI_{i + 1}";
+                var aiDomain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
 
                 aiPlayer.NetDefaultVesselType.Value = aiVesselType;
-                aiPlayer.NetName.Value = data.PlayerName;
-                aiPlayer.NetDomain.Value = data.domain;
+                aiPlayer.NetName.Value = new FixedString128Bytes(aiName);
+                aiPlayer.NetDomain.Value = aiDomain;
                 aiPlayer.NetIsAI.Value = true;
 
                 if (!TrySpawnVesselForAI(aiPlayer, out var aiVesselNO))
@@ -111,10 +118,10 @@ namespace CosmicShore.Gameplay
                 // Server-side initialization of the AI player-vessel pair
                 if (!aiVesselNO.TryGetComponent(out IVessel vessel))
                 {
-                    CSDebug.LogError("[ClientPlayerVesselInitializer] Spawned vessel missing IVessel component.");
+                    CSDebug.LogError("[ServerPlayerVesselInitializerWithAI] Spawned vessel missing IVessel component.");
                     return;
                 }
-            
+
                 clientPlayerVesselInitializer.InitializePlayerAndVessel(aiPlayer, vessel);
                 ConfigureAIPilot(aiVesselNO);
             }
diff --git a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
index 6b9692674..9f3029871 100644
--- a/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
+++ b/Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs
@@ -30,6 +30,9 @@ namespace CosmicShore.UI
         [Inject] private GameDataSO gameData;
         [SerializeField] private ScriptableVariable<int> shipClassTypeVariable; // broadcast class index
 
+        [Header("Party / Lobby")]
+        [Inject] private HostConnectionDataSO hostConnectionData;
+
         [Header("External Views")]
         [SerializeField] private ArcadeExploreView arcadeExploreView;
 
@@ -449,7 +452,9 @@ namespace CosmicShore.UI
         {
             audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
 
+            SyncGameDataForLaunch();
             startGameRequestedEvent?.Raise();
+            gameData.InvokeGameLaunch();
         }
 
         #endregion
@@ -471,6 +476,24 @@ namespace CosmicShore.UI
 
         #region GameData sync helpers
 
+        void SyncGameDataForLaunch()
+        {
+            if (!gameData || config?.SelectedGame == null) return;
+
+            var game = config.SelectedGame;
+            gameData.SceneName = game.SceneName;
+            gameData.GameMode = game.Mode;
+            gameData.IsMultiplayerMode = game.IsMultiplayer;
+
+            int lobbyPlayerCount = hostConnectionData != null && hostConnectionData.PartyMembers != null
+                ? Mathf.Max(1, hostConnectionData.PartyMembers.Count)
+                : 1;
+
+            int totalDesired = config.PlayerCount;
+            gameData.SelectedPlayerCount.Value = totalDesired;
+            gameData.RequestedAIBackfillCount = Mathf.Max(0, totalDesired - lobbyPlayerCount);
+        }
+
         void SyncGameDataConfig()
         {
```

</details>
