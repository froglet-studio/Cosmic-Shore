# Branch archive: `claude/fix-team-scoreboard-BC9OV`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-21 by Claude
- **Unmerged commits:** 1
- **Forked from:** `08dad3dfe` (2026-04-21, fix(scoreboard): share team victory across winning domain in scoreboard and en)
- **Tip:** `55fbaca29`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs`
  - `Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs`
  - `Assets/_Scripts/Controller/Player/Player.cs`

### `55fbaca29` — fix(multiplayer): guard server-only timer RPC and unify teammate domain

_Claude, 2026-04-21 20:20:06 +0000_

```text
Two bug fixes hit on successive HexRace/Crystal Capture sessions.

1. NetworkTimeBasedTurnMonitor.UpdateTimerUI NRE on restart.
   StartMonitors fires via the OnMiniGameTurnStarted SOAP event on every
   client, so UpdateTimerUI ran on non-servers and tried to send a
   ClientRpc before the behaviour was spawned, hitting a null internal
   reference. Guard with IsSpawned + IsServer so only the server
   broadcasts the timer text.

2. HexRace teammate shows "X Crystals Left" instead of race time.
   The party-domain unification in NormalizeHumanDomains wrote directly
   to Player.NetDomain, but NetDomain has Owner write permission, so
   writes to remote clients' Players were silently dropped. The
   teammate's RoundStats.Domain stayed on their original pick and the
   winning-domain comparison in HexRaceController missed them, so they
   were scored as losers.

   Fix is three-pronged, applied after base.OnPlayerReadyToSpawnAsync so
   RoundStats exists:
     - player.SetDomain updates server-side Player.Domain
     - player.RoundStats.Domain writes the Server-permission n_Domain
       NetworkVariable, which replicates cleanly to all clients
     - ApplyAuthoritativeDomain_ClientRpc asks the owning client to
       rewrite its own NetDomain so local UI (domain color, score card)
       matches the unified team
```

```text
 .../Controller/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs     |  3 +-
 .../Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs     | 71 +++++++++++++++++++++++++++++++--
 Assets/_Scripts/Controller/Player/Player.cs                           | 14 +++++++
 3 files changed, 84 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 153 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs
index c76d9048a..caf348c10 100644
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs
+++ b/Assets/_Scripts/Controller/Arcade/TurnMonitors/NetworkTimeBasedTurnMonitor.cs
@@ -7,7 +7,8 @@ namespace CosmicShore.Gameplay
     {
         protected override void UpdateTimerUI()
         {
-            FixedString32Bytes message = GetTimeToDisplay(); 
+            if (!IsSpawned || !IsServer) return;
+            FixedString32Bytes message = GetTimeToDisplay();
             UpdateTimerUI_ClientRpc(message);
         }
 
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
index e0b6f4229..9d9301335 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs
@@ -1,7 +1,9 @@
 using System.Collections.Generic;
+using System.Threading;
 using CosmicShore.Data;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
+using Cysharp.Threading.Tasks;
 using Reflex.Attributes;
 using Reflex.Injectors;
 using Unity.Netcode;
@@ -38,6 +40,12 @@ namespace CosmicShore.Gameplay
         [Tooltip("Optional AI profile list for assigning unique names to AI opponents.")]
         [SerializeField] SO_AIProfileList aiProfileList;
 
+        /// <summary>
+        /// Unified party domain computed by NormalizeHumanDomains. All humans in
+        /// the party are forced onto this domain so teammate scoring works correctly.
+        /// </summary>
+        Domains _partyDomain = Domains.Unassigned;
+
         protected override void OnNetworkSpawn()
         {
             if (!NetworkManager.Singleton.IsServer)
@@ -254,6 +262,12 @@ namespace CosmicShore.Gameplay
         /// Ensures all human players share one team.
         /// The first human's chosen domain is respected (even if it's Ruby or Gold).
         /// Called on the server before AI spawning so team counts are accurate.
+        ///
+        /// NOTE: NetDomain has Owner write permission, so direct server writes here
+        /// succeed only for the host's own Player. Remote clients' NetDomain stays
+        /// at its original value — the authoritative server-side unification happens
+        /// later in <see cref="UnifyHumanDomainAuthoritatively"/> after their
+        /// RoundStats has been initialized.
         /// </summary>
         void NormalizeHumanDomains(List<Player> humans)
         {
@@ -273,12 +287,16 @@ namespace CosmicShore.Gameplay
             if (partyDomain == Domains.Unassigned)
                 partyDomain = GameDataSO.TeamDomains[0];
 
-            // Assign all human players to the party domain
+            _partyDomain = partyDomain;
+
+            // Best-effort NetDomain write. Succeeds for host (IsOwner on server);
+            // fails silently for remote clients. Remote unification is completed
+            // in UnifyHumanDomainAuthoritatively via ClientRpc + direct RoundStats write.
             foreach (var player in humans)
             {
-                if (player.NetDomain.Value != partyDomain)
+                if (player.NetDomain.Value != partyDomain && player.IsOwner)
                 {
-                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeHumanDomains: Reassigning {player.NetName.Value} from {player.NetDomain.Value} to {partyDomain}</color>");
+                    Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeHumanDomains: Reassigning host {player.NetName.Value} from {player.NetDomain.Value} to {partyDomain}</color>");
                     player.NetDomain.Value = partyDomain;
                 }
             }
@@ -286,6 +304,53 @@ namespace CosmicShore.Gameplay
             Debug.Log($"<color=#FF00FF>[FLOW-5AI] NormalizeHumanDomains: {humans.Count} humans → domain={partyDomain}, teamCount={gameData.RequestedTeamCount}</color>");
         }
 
+        /// <summary>
+        /// Server-authoritative domain unification for a single human player.
+        /// Called after <see cref="Player.InitializeForMultiplayerMode"/> has run on the
+        /// server so <see cref="RoundStats"/> exists and its server-write NetworkVariable
+        /// replicates cleanly to clients.
+        ///
+        /// Three writes:
+        ///   1. <c>player.SetDomain</c> — server-side Player.Domain property
+        ///   2. <c>player.RoundStats.Domain</c> — server-side write to n_Domain
+        ///      NetworkVariable; clients read replicated value via OnValueChanged
+        ///   3. <c>ApplyAuthoritativeDomain_ClientRpc</c> — asks the owning client
+        ///      to rewrite its own NetDomain so its local UI (domain color,
+        ///      in-game score card) matches the unified team
+        /// </summary>
+        void UnifyHumanDomainAuthoritatively(Player player)
+        {
+            if (player == null || player.NetIsAI.Value) return;
+            if (_partyDomain == Domains.Unassigned || _partyDomain == Domains.None) return;
+
+            if (player.Domain != _partyDomain)
+                player.SetDomain(_partyDomain);
+
+            if (player.RoundStats != null && player.RoundStats.Domain != _partyDomain)
+                player.RoundStats.Domain = _partyDomain;
+
+            var hostClientId = NetworkManager.Singleton.LocalClientId;
+            if (player.OwnerClientId != hostClientId)
+            {
+                var rpcParams = new ClientRpcParams
+                {
+                    Send = new ClientRpcSendParams { TargetClientIds = new[] { player.OwnerClientId } }
+                };
+                player.ApplyAuthoritativeDomain_ClientRpc(_partyDomain, rpcParams);
+            }
+        }
+
+        /// <summary>
+        /// Overrides base to unify the human's domain after the base-class spawn
+        /// and initialization chain finishes. This is the only point where the
+        /// server can reliably override a remote client's team assignment.
+        /// </summary>
+        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
+        {
+            await base.OnPlayerReadyToSpawnAsync(player, ct);
+            UnifyHumanDomainAuthoritatively(player);
+        }
+
         /// <summary>
         /// Builds team counts from the given human players.
         /// Active teams are built around the human's domain (not hardcoded Jade-first).
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index 33378a4c2..7bf99be88 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -44,6 +44,20 @@ namespace CosmicShore.Gameplay
         {
             Domain = newDomain;
         }
+
+        /// <summary>
+        /// Server → owner client: asks the owning client to write the given domain
+        /// into its own NetDomain. Required because NetDomain has Owner write permission,
+        /// so the server cannot directly update a remote client's value. Used by
+        /// ServerPlayerVesselInitializerWithAI to unify human party domains.
+        /// </summary>
+        [ClientRpc]
+        public void ApplyAuthoritativeDomain_ClientRpc(Domains domain, ClientRpcParams rpcParams = default)
+        {
+            if (!IsOwner) return;
+            if (NetDomain.Value != domain)
+                NetDomain.Value = domain;
+        }
```

</details>
