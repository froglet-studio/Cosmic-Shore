using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Network-aware vessel swap for Menu_Main freestyle mode.
    ///
    /// Owner requests a vessel class change via <see cref="RequestVesselSwap_ServerRpc"/>.
    /// The server despawns the old vessel, spawns a new one with the same ownership,
    /// initializes the pair on the server, then broadcasts to all clients via
    /// <see cref="SwapVessel_ClientRpc"/> so they re-initialize the pair locally.
    ///
    /// After the swap completes, the vessel is placed in autopilot at the old vessel's
    /// pose so the transition is seamless.
    ///
    /// Must live on a GameObject with a <see cref="NetworkObject"/> that is spawned
    /// as part of the Menu_Main scene (e.g. alongside <see cref="ClientPlayerVesselInitializer"/>).
    /// </summary>
    public class MenuVesselSwapController : NetworkBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] VesselPrefabContainer vesselPrefabContainer;
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Inject] GameDataSO gameData;
        [Inject] Container _container;

        [Header("Timing")]
        [Tooltip("Delay in ms after vessel spawn before notifying clients.")]
        [SerializeField] int postSpawnDelayMs = 200;

        CancellationTokenSource _cts;
        bool _isSwapping;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _cts = new CancellationTokenSource();
        }

        public override void OnNetworkDespawn()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Whether a swap is currently in progress. UI should check this to prevent
        /// double-clicks during the swap.
        /// </summary>
        public bool IsSwapping => _isSwapping;

        // ---------------------------------------------------------
        // PUBLIC API (called by MenuOverviewPanelController)
        // ---------------------------------------------------------

        /// <summary>
        /// Initiates a vessel swap for the local player. Only the vessel owner can
        /// call this. The actual despawn/spawn happens on the server.
        /// </summary>
        public void RequestSwap(VesselClassType targetClass)
        {
            if (_isSwapping) return;

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel == null) return;

            var currentClass = localPlayer.Vessel.VesselStatus.VesselType;
            if (targetClass == currentClass) return;

            if (localPlayer is not Player netPlayer || !netPlayer.IsSpawned)
            {
                CSDebug.LogError("[MenuVesselSwap] LocalPlayer is not a networked Player.");
                return;
            }

            // Capture pose before the swap so the new vessel appears in the same spot.
            var vs = localPlayer.Vessel.VesselStatus;
            var pose = new Pose(vs.Transform.position, vs.Transform.rotation);

            RequestVesselSwap_ServerRpc(
                netPlayer.NetworkObjectId,
                netPlayer.VesselNetId,
                targetClass,
                pose.position,
                pose.rotation);
        }

        // ---------------------------------------------------------
        // SERVER
        // ---------------------------------------------------------

        [ServerRpc(RequireOwnership = false)]
        void RequestVesselSwap_ServerRpc(
            ulong playerNetId,
            ulong oldVesselNetId,
            VesselClassType targetClass,
            Vector3 snapshotPos,
            Quaternion snapshotRot,
            ServerRpcParams rpcParams = default)
        {
            HandleSwapAsync(
                rpcParams.Receive.SenderClientId,
                playerNetId,
                oldVesselNetId,
                targetClass,
                new Pose(snapshotPos, snapshotRot),
                _cts.Token).Forget();
        }

        async UniTaskVoid HandleSwapAsync(
            ulong senderClientId,
            ulong playerNetId,
            ulong oldVesselNetId,
            VesselClassType targetClass,
            Pose snapshotPose,
            CancellationToken ct)
        {
            _isSwapping = true;

            // 1. Find the player
            if (!gameData.TryGetPlayerByNetworkObjectId(playerNetId, out var player))
            {
                CSDebug.LogError($"[MenuVesselSwap] Player {playerNetId} not found.");
                _isSwapping = false;
                return;
            }

            // 2. Find and despawn the old vessel
            if (!gameData.TryGetVesselByNetworkObjectId(oldVesselNetId, out var oldVessel))
            {
                CSDebug.LogError($"[MenuVesselSwap] Old vessel {oldVesselNetId} not found.");
                _isSwapping = false;
                return;
            }

            // Remove old vessel from tracking
            gameData.Vessels.Remove(oldVessel);

            // Despawn the old vessel NetworkObject (server authority)
            if (oldVessel is VesselController oldVC && oldVC.IsSpawned)
                oldVC.NetworkObject.Despawn(true);

            // 3. Spawn the new vessel
            if (!vesselPrefabContainer.TryGetShipPrefab(targetClass, out Transform shipPrefabTransform))
            {
                CSDebug.LogError($"[MenuVesselSwap] No prefab for vessel type {targetClass}");
                _isSwapping = false;
                return;
            }

            if (!shipPrefabTransform.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError($"[MenuVesselSwap] Prefab {shipPrefabTransform.name} missing NetworkObject");
                _isSwapping = false;
                return;
            }

            var networkVessel = Instantiate(shipNetworkObject);
            GameObjectInjector.InjectRecursive(networkVessel.gameObject, _container);
            networkVessel.SpawnWithOwnership(senderClientId, true);

            // Update player's NetVesselId
            if (player is Player netPlayer)
                netPlayer.NetVesselId.Value = networkVessel.NetworkObjectId;

            // 4. Initialize the new vessel on the server (host)
            if (!networkVessel.TryGetComponent(out IVessel newVessel))
            {
                CSDebug.LogError("[MenuVesselSwap] Spawned vessel missing IVessel component.");
                _isSwapping = false;
                return;
            }

            // Wire the new vessel to the player
            player.ChangeVessel(newVessel);
            newVessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, newVessel);

            // Apply snapshot pose so the new vessel appears where the old one was
            newVessel.SetPose(snapshotPose);

            // Re-activate: start the vessel and put it in autopilot (menu mode)
            if (player is Player np)
            {
                np.StartPlayer();
                newVessel.ToggleAIPilot(true);
                np.InputController.SetPause(true);
            }

            // 5. Wait for replication, then notify all non-host clients
            await UniTask.Delay(postSpawnDelayMs, cancellationToken: ct);

            var newVesselNetId = networkVessel.NetworkObjectId;
            NotifyAllClients(playerNetId, newVesselNetId, senderClientId, snapshotPose);

            _isSwapping = false;
        }

        void NotifyAllClients(ulong playerNetId, ulong newVesselNetId, ulong ownerClientId, Pose snapshotPose)
        {
            var hostClientId = NetworkManager.Singleton.LocalClientId;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                // Skip the host — already initialized above
                if (client.ClientId == hostClientId)
                    continue;

                var target = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
                };

                SwapVessel_ClientRpc(
                    playerNetId,
                    newVesselNetId,
                    client.ClientId == ownerClientId,
                    snapshotPose.position,
                    snapshotPose.rotation,
                    target);
            }
        }

        // ---------------------------------------------------------
        // CLIENT
        // ---------------------------------------------------------

        [ClientRpc]
        void SwapVessel_ClientRpc(
            ulong playerNetId,
            ulong newVesselNetId,
            bool isOwnerClient,
            Vector3 snapshotPos,
            Quaternion snapshotRot,
            ClientRpcParams rpcParams = default)
        {
            ResolveSwapAsync(playerNetId, newVesselNetId, isOwnerClient,
                new Pose(snapshotPos, snapshotRot), _cts.Token).Forget();
        }

        async UniTaskVoid ResolveSwapAsync(
            ulong playerNetId,
            ulong newVesselNetId,
            bool isOwnerClient,
            Pose snapshotPose,
            CancellationToken ct)
        {
            // Wait for the new vessel NetworkObject to replicate.
            // Poll briefly — the vessel's OnNetworkSpawn adds it to gameData.Vessels.
            const int maxAttempts = 20;
            const int intervalMs = 100;
            IVessel newVessel = null;

            for (int i = 0; i < maxAttempts; i++)
            {
                if (gameData.TryGetVesselByNetworkObjectId(newVesselNetId, out newVessel))
                    break;
                await UniTask.Delay(intervalMs, cancellationToken: ct);
            }

            if (newVessel == null)
            {
                CSDebug.LogError($"[MenuVesselSwap] Client: new vessel {newVesselNetId} never appeared.");
                return;
            }

            if (!gameData.TryGetPlayerByNetworkObjectId(playerNetId, out var player))
            {
                CSDebug.LogError($"[MenuVesselSwap] Client: player {playerNetId} not found.");
                return;
            }

            // Re-initialize the pair on this client
            player.ChangeVessel(newVessel);
            newVessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, newVessel);
            newVessel.SetPose(snapshotPose);

            // For the owner client: re-activate with autopilot
            if (isOwnerClient && player is Player np)
            {
                np.StartPlayer();
                newVessel.ToggleAIPilot(true);
                np.InputController.SetPause(true);

                // Snap camera to the new vessel
                if (CameraManager.Instance)
                    CameraManager.Instance.SnapPlayerCameraToTarget();
            }
            else
            {
                // Non-owner: just start the vessel so it's visible and active
                if (player is Player otherP)
                    otherP.StartPlayer();
            }
        }
    }
}
