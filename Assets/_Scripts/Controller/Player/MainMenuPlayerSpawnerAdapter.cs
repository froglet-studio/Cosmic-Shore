using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main player setup adapter. Does NOT spawn players or AI via the old
    /// single-player path. Instead, waits for the NetworkManager host to create
    /// the Player prefab automatically (via ConnectionApprovalCallback), then
    /// spawns a Squirrel vessel, initializes the player in multiplayer mode,
    /// and enables AI pilot for ambient flight in the menu.
    ///
    /// Camera is wired to the vessel automatically through VesselCameraCustomizer
    /// → OnInitializePlayerCamera SOAP event → CameraManager.SetupGamePlayCameras.
    /// </summary>
    public class MainMenuPlayerSpawnerAdapter : PlayerSpawnerAdapterBase
    {
        [Header("Network Vessel Spawning")]
        [SerializeField] VesselPrefabContainer vesselPrefabContainer;
        [SerializeField] ThemeManagerDataContainerSO themeManagerData;

        bool _initialized;

        void Start()
        {
            _gameData.InitializeGame();
            AddSpawnPosesToGameData();

            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                CSDebug.LogWarning("[MainMenuSpawner] NetworkManager not available.");
                return;
            }

            nm.OnClientConnectedCallback += OnClientConnected;

            // If the host is already running and local client connected,
            // process immediately.
            if (nm.IsHost && nm.IsConnectedClient)
                OnClientConnected(nm.LocalClientId);
        }

        void OnDisable()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
                nm.OnClientConnectedCallback -= OnClientConnected;
        }

        void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || clientId != nm.LocalClientId) return;
            if (_initialized) return;
            _initialized = true;

            SetupHostPlayerAsync(clientId).Forget();
        }

        async UniTaskVoid SetupHostPlayerAsync(ulong clientId)
        {
            var nm = NetworkManager.Singleton;

            // Wait for Player's OnNetworkSpawn to complete.
            await UniTask.Delay(500, DelayType.UnscaledDeltaTime);

            var playerNetObj = nm.SpawnManager.GetPlayerNetworkObject(clientId);
            if (!playerNetObj)
            {
                CSDebug.LogError("[MainMenuSpawner] Player NetworkObject not found for host.");
                return;
            }

            var player = playerNetObj.GetComponent<Player>();
            if (!player)
            {
                CSDebug.LogError("[MainMenuSpawner] Player component missing on host object.");
                return;
            }

            // Configure host player on the server.
            DomainAssigner.Initialize();
            player.NetDomain.Value = Domains.Jade;
            player.NetIsAI.Value = false;

            // Spawn a Squirrel vessel for the host.
            if (!vesselPrefabContainer.TryGetShipPrefab(VesselClassType.Squirrel, out Transform shipPrefab))
            {
                CSDebug.LogError("[MainMenuSpawner] No prefab found for Squirrel vessel.");
                return;
            }

            if (!shipPrefab.TryGetComponent(out NetworkObject shipNetworkObject))
            {
                CSDebug.LogError("[MainMenuSpawner] Squirrel prefab missing NetworkObject.");
                return;
            }

            var vesselNO = Instantiate(shipNetworkObject);
            vesselNO.SpawnWithOwnership(clientId, true);
            player.NetVesselId.Value = vesselNO.NetworkObjectId;

            // Initialize player + vessel in multiplayer mode.
            // VesselController.Initialize sets up:
            //   - AIPilot, VesselTransformer, ActionHandler
            //   - VesselCameraCustomizer → raises OnInitializePlayerCamera
            //     → CameraManager switches from menu cam to player cam
            var vessel = vesselNO.GetComponent<IVessel>();
            player.InitializeForMultiplayerMode(vessel);
            vessel.Initialize(player);

            // Apply team material properties.
            ShipHelper.SetShipProperties(themeManagerData, vessel);

            // Register with game data (sets pose, resets for play).
            _gameData.AddPlayer(player);

            // Activate the player (starts vessel motion, enables subsystems).
            _gameData.SetPlayersActive();

            // Enable AI pilot — vessel is the player's but AI-controlled in the menu.
            vessel.ToggleAIPilot(true);
            player.InputController.SetPause(true);

            // Signal menu systems that rely on these events.
            _gameData.InvokeMiniGameRoundStarted();
            _gameData.InvokeTurnStarted();

            CSDebug.Log("[MainMenuSpawner] Host player setup complete — Squirrel vessel with AI pilot.");
        }
    }
}
