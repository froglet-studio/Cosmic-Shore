using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Initializes player-vessel pairs.
    ///
    /// On the server/host:
    ///   Called directly by ServerPlayerVesselInitializer after spawning a vessel.
    ///
    /// On remote clients:
    ///   Subscribes to OnVesselNetworkSpawned SOAP event.
    ///   When a vessel appears, waits for the Player's NetVesselId to replicate,
    ///   then pairs and initializes the player-vessel.
    /// </summary>
    public class ClientPlayerVesselInitializer : NetworkBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        [Inject] protected GameDataSO gameData;

        /// <summary>
        /// Timeout (in ms) for waiting on player-vessel pairing on the client.
        /// </summary>
        const int InitTimeoutMs = 10_000;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Only non-server clients need the SOAP event path.
            // The server initializes directly via InitializePlayerAndVessel().
            if (NetworkManager.Singleton.IsServer)
                return;

            gameData.OnVesselNetworkSpawned.OnRaised += OnVesselNetworkSpawned;
        }

        public override void OnNetworkDespawn()
        {
            gameData.OnVesselNetworkSpawned.OnRaised -= OnVesselNetworkSpawned;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Direct initialization called by ServerPlayerVesselInitializer on the host.
        /// </summary>
        public void InitializePlayerAndVessel(Player player, NetworkObject vesselNO)
        {
            if (!vesselNO.TryGetComponent(out IVessel vessel))
            {
                CSDebug.LogError("[ClientPlayerVesselInitializer] Spawned vessel missing IVessel component.");
                return;
            }

            InitializePair(player, vessel);
            gameData.InvokeClientReady();
        }

        /// <summary>
        /// Fires on non-server clients when a vessel's NetworkObject spawns.
        /// Tries to pair all un-initialized players with their vessels.
        /// </summary>
        void OnVesselNetworkSpawned()
        {
            TryInitializeUnmatchedPairs(this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid TryInitializeUnmatchedPairs(CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(InitTimeoutMs);

            try
            {
                // Wait for at least one un-initialized player to have a valid VesselNetId
                // that can be matched to an existing vessel in gameData.
                // This handles the replication delay where VesselController.OnNetworkSpawn
                // fires before Player.NetVesselId replicates from the server.
                await UniTask.WaitUntil(() => HasMatchablePair(), cancellationToken: cts.Token);
            }
            catch (System.OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                CSDebug.LogError("[ClientPlayerVesselInitializer] Timed out waiting for player-vessel pairing.");
                return;
            }

            bool anyInitialized = false;
            foreach (var p in gameData.Players)
            {
                if (p.Vessel != null) continue;
                if (p.VesselNetId == 0) continue;
                if (!gameData.TryGetVesselByNetworkObjectId(p.VesselNetId, out var vessel)) continue;

                InitializePair(p, vessel);
                anyInitialized = true;
            }

            if (anyInitialized)
                gameData.InvokeClientReady();
        }

        bool HasMatchablePair()
        {
            foreach (var p in gameData.Players)
            {
                if (p.Vessel != null) continue;
                if (p.VesselNetId == 0) continue;
                if (gameData.TryGetVesselByNetworkObjectId(p.VesselNetId, out _))
                    return true;
            }
            return false;
        }

        void InitializePair(IPlayer player, IVessel vessel)
        {
            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);
            ShipHelper.SetShipProperties(themeManagerData, vessel);
            gameData.AddPlayer(player);

            if (player.IsLocalUser && CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}
