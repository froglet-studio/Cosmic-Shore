using System;
using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class Player : NetworkBehaviour, IPlayer
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;

        [Inject] private PlayerDataService _injectedPlayerDataService;

        // Fallback to static singleton — Netcode-spawned Players (host's own player)
        // bypass Reflex's auto-injection since they're instantiated by NetworkManager,
        // not Instantiate() inside an injected scope.
        private PlayerDataService playerDataService
            => _injectedPlayerDataService != null
                ? _injectedPlayerDataService
                : PlayerDataService.Instance;

        public NetworkVariable<VesselClassType> NetDefaultVesselType = new(VesselClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Domains> NetDomain = new(Domains.Jade, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<FixedString128Bytes> NetName = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<ulong> NetVesselId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> NetIsAI = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> NetAvatarId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public Domains Domain { get; private set; } = Domains.Jade;

        /// <summary>
        /// Theme data stashed by <see cref="ClientPlayerVesselInitializer"/> at vessel
        /// spawn/swap. Used by <see cref="OnNetDomainChanged"/> to repaint the vessel
        /// when domain replicates after spawn (modal Blue reset, server NormalizeUnassignedHumans,
        /// shape-mode SetDomain, etc).
        /// </summary>
        internal ThemeManagerDataContainerSO _vesselThemeManagerData;

        /// <summary>
        /// Changes the player's domain at runtime. Used by shape mode to match
        /// the player's prism color to the collided shape's domain.
        /// </summary>
        public void SetDomain(Domains newDomain)
        {
            Domain = newDomain;
        }

        /// <summary>
        /// Owner-initiated request to change this player's domain.
        /// NetDomain is server-write, so clients route their selections through this RPC.
        /// Validated against <see cref="GameDataSO.IsActiveDomain"/> with the session's
        /// configured <see cref="GameDataSO.RequestedDomainCount"/>; out-of-range picks
        /// are rejected silently.
        /// </summary>
        [ServerRpc] // RequireOwnership = true is the default — only the player's owner may request
        public void RequestSetDomain_ServerRpc(Domains domain)
        {
            using var _ = CosmicShore.Utility.PerformanceBenchmark.NetMarkers.RpcDispatch.Auto();
            CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountRpc();

            if (!GameDataSO.IsActiveDomain(domain, gameData.RequestedDomainCount))
            {
                Debug.LogWarning(
                    $"[Player] RequestSetDomain_ServerRpc rejected domain {domain} for {NetName.Value} (DC={gameData.RequestedDomainCount})");
                return;
            }

            NetDomain.Value = domain;
            CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountNetVarDirty();
        }
        public string Name { get; private set; }
        public int AvatarId { get; private set; }
        public string PlayerUUID => Name;
        public ulong PlayerNetId => NetworkObjectId;
        /// <summary>
        /// Remarks, this VesselNetId will be set by server
        /// through a network variable during initialization
        /// of vessel and player.
        /// </summary>
        public ulong VesselNetId { get; private set; }
        public ulong OwnerClientNetId => OwnerClientId;
        public IVessel Vessel { get; private set; }
        public bool IsActive { get; private set; }
        public bool AutoPilotEnabled => Vessel.VesselStatus.AutoPilotEnabled;
        public bool IsInitializedAsAI { get; private set; }

        bool _spawnEventRaised;

        private InputController _inputController;
        public InputController InputController
        {
            get
            {
                if (!_inputController)
                    _inputController = gameObject.GetOrAdd<InputController>();
                return _inputController;
            }
        }

        private RoundStats _roundStats;
        public IRoundStats RoundStats
        {
            get
            {
                if (!_roundStats)
                    _roundStats = gameObject.GetOrAdd<RoundStats>();
                return _roundStats;
            }
        }
        public IInputStatus InputStatus => InputController.InputStatus;

        public Transform Transform => transform;
        public bool IsMultiplayerOwner => IsSpawned && IsOwner && !IsInitializedAsAI;
        public bool IsNetworkOwner => IsSpawned && IsOwner;
        public bool IsNetworkClient => IsSpawned && !IsOwner;
        public bool IsSinglePlayerOwner => !IsSpawned && !IsInitializedAsAI;
        public bool IsLocalUser => IsMultiplayerOwner || IsSinglePlayerOwner;
       
        IPlayer.InitializeData InitializeData;
        
        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel)
        {
            InitializeData = data;
            IsInitializedAsAI = InitializeData.IsAI;
            // Single-player & legacy menu spawns default to Jade. Multiplayer overrides
            // via NetDomain (server-write) before the vessel is initialized.
            Domain = Domains.Jade;
            Name = InitializeData.PlayerName;
            AvatarId = InitializeData.AvatarId;
            InputController.Initialize();
            ToggleInputPause(true);
            Vessel = vessel;
            RoundStats.Name = Name;
            RoundStats.Domain = Domain;
        }

        /// <summary>
        /// TODO -> A temp way to initialize in multiplayer, try for better approach.
        /// </summary>
        public void InitializeForMultiplayerMode(IVessel vessel)
        {
            IsInitializedAsAI = NetIsAI.Value;
            Domain = NetDomain.Value;
            Name = NetName.Value.ToString();
            AvatarId = NetAvatarId.Value;
            Vessel = vessel;

            if (!IsServer)
                return;

            RoundStats.Name = Name;
            RoundStats.Domain = Domain;

            SetGameObjectName();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] OnNetworkSpawn — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}, IsServer={IsServer}</color>");
            base.OnNetworkSpawn();

            // Add to game data early so ServerPlayerVesselInitializer can find us.
            gameData.Players.Add(this);

            VesselNetId = NetVesselId.Value;

            // Subscribe BEFORE writes so deferred spawn-event logic in
            // OnNetNameValueChanged / OnNetDefaultVesselTypeChanged catches
            // the first client value replication.
            NetDomain.OnValueChanged += OnNetDomainChanged;
            NetName.OnValueChanged += OnNetNameValueChanged;
            NetDefaultVesselType.OnValueChanged += OnNetDefaultVesselTypeChanged;
            NetVesselId.OnValueChanged += OnNetVesselIdChanged;
            NetAvatarId.OnValueChanged += OnNetAvatarIdChanged;

            // --- Server writes (server-perm vars) ---
            // Domain is NOT assigned here. Humans default to Jade (the NetDomain
            // initializer); the modal lets each owner pick a real domain via
            // RequestSetDomain_ServerRpc. AI players have their domain written
            // by SpawnAIs() in ServerPlayerVesselInitializerWithAI before vessel spawn.
            if (IsServer)
            {
                NetIsAI.Value = IsInitializedAsAI;
            }

            // --- Owner writes (owner-perm vars: NetName, NetAvatarId, NetDefaultVesselType) ---
            // Only the local human player writes profile data here.
            // AI players share the host's OwnerClientId (IsOwner=true) but must NOT
            // overwrite their names with the human's profile — the AI spawner sets their
            // names separately after spawn. IsLocalUser filters out AI via !IsInitializedAsAI.
            if (IsLocalUser)
            {
                if (playerDataService != null && playerDataService.IsInitialized
                    && playerDataService.CurrentProfile != null)
                {
                    NetName.Value = playerDataService.CurrentProfile.displayName;
                    NetAvatarId.Value = playerDataService.CurrentProfile.avatarId;
                }
                else if (!string.IsNullOrEmpty(gameData.LocalPlayerDisplayName))
                {
                    NetName.Value = gameData.LocalPlayerDisplayName;
                    NetAvatarId.Value = gameData.LocalPlayerAvatarId;
                }
                else
                {
                    NetName.Value = StripPlayerNameSuffix(AuthenticationService.Instance.PlayerName);
                }

                // If profile wasn't ready when we spawned, subscribe so NetName updates
                // when the cloud profile finishes loading.
                if (playerDataService != null)
                    playerDataService.OnProfileChanged += HandleProfileLoadedAfterSpawn;

                // Only set vessel type from gameData if the client hasn't already
                // chosen a vessel via the ArcadeGameConfigureModal (which writes
                // directly to NetDefaultVesselType). This preserves per-client
                // vessel selection in multiplayer.
                if (!IsValidVesselTypeForSpawn(NetDefaultVesselType.Value))
                    NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
            }

            // --- Raise spawn event AFTER all local writes ---
            // Server: only when all required values are populated.
            //   Host player (IsOwner && IsServer): name written above → raise now.
            //   Client player (!IsOwner && IsServer): name empty → deferred to
            //   OnNetNameValueChanged when the client's name replicates.
            // Non-server: raise immediately for client-side pair resolution.
            if (IsServer)
            {
                if (!_spawnEventRaised && IsSpawnReady())
                {
                    _spawnEventRaised = true;
                    gameData.OnPlayerNetworkSpawnedUlong.Raise(OwnerClientId);
                }
            }
            else
            {
                gameData.OnPlayerNetworkSpawnedUlong.Raise(OwnerClientId);
            }

            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] OnNetworkSpawn DONE — Name={NetName.Value}, VesselType={NetDefaultVesselType.Value}, Domain={NetDomain.Value}, IsAI={NetIsAI.Value}, SpawnEventRaised={_spawnEventRaised}</color>");

            InputController.Initialize();
        }

        public override void OnNetworkDespawn()
        {
            _spawnEventRaised = false;
            gameData.Players.Remove(this);

            NetDomain.OnValueChanged -= OnNetDomainChanged;
            NetName.OnValueChanged -= OnNetNameValueChanged;
            NetDefaultVesselType.OnValueChanged -= OnNetDefaultVesselTypeChanged;
            NetVesselId.OnValueChanged -= OnNetVesselIdChanged;
            NetAvatarId.OnValueChanged -= OnNetAvatarIdChanged;

            if (playerDataService != null)
                playerDataService.OnProfileChanged -= HandleProfileLoadedAfterSpawn;

            // Drop the theme reference so the next spawn re-stashes a fresh one.
            _vesselThemeManagerData = null;
        }

        /// <summary>
        /// Fires when the cloud profile finishes loading after Player has already spawned.
        /// Updates NetName/NetAvatarId so the in-game name matches the menu username.
        /// Only the owner writes to these NetworkVariables — other clients read via replication.
        /// </summary>
        private void HandleProfileLoadedAfterSpawn(PlayerProfileData profile)
        {
            if (!IsLocalUser || profile == null) return;
            if (string.IsNullOrEmpty(profile.displayName)) return;

            if (NetName.Value.ToString() != profile.displayName)
                NetName.Value = profile.displayName;
            if (NetAvatarId.Value != profile.avatarId)
                NetAvatarId.Value = profile.avatarId;
        }


        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => 
            gameObject.SetActive(toggle);

        /// <summary>
        /// Re-initializes a persistent Player for a new game scene.
        /// Player NetworkObjects survive Netcode scene loads (DestroyWithScene=false)
        /// but OnNetworkSpawn() only fires once (initial creation in Auth scene).
        /// This method handles all subsequent scene transitions:
        ///   - Clears stale vessel reference (old vessel destroyed with scene)
        ///   - Updates NetworkVariables to match new game config
        ///   - Syncs local properties from NetworkVariables
        ///   - Re-registers with gameData.Players (cleared by ResetRuntimeData)
        /// Called by ServerPlayerVesselInitializer when discovering persistent Players.
        /// </summary>
        public void PrepareForNewScene()
        {
            Debug.Log($"<color=#00FF00>[FLOW-4] [Player] PrepareForNewScene — OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}, IsOwner={IsOwner}</color>");
            // Clear stale references from previous scene.
            // Vessels have destroyWithScene=true and are already destroyed.
            Vessel = null;
            IsActive = false;
            VesselNetId = 0;

            // Reset gameplay stats from previous game.
            // Cleanup() zeroes all stats via property setters, which also
            // update NetworkVariables on the server. Name/Domain are re-set below.
            RoundStats.Cleanup();

            // Reset input state (joystick positions, throttle, flags).
            InputStatus?.ResetForReplay();

            // Update owner-writable NetworkVariables to match new game config.
            // Always overwrite vessel type so the menu autopilot uses the configured
            // menuVesselClass (e.g. Squirrel) rather than retaining the game vessel.
            // When launching a new game, ArcadeGameConfigureModal.SyncLocalPlayerVesselType()
            // writes the chosen vessel back to NetDefaultVesselType.
            if (IsOwner)
                NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;

            // Refresh NetName/NetAvatarId from the now-loaded profile.
            // OnNetworkSpawn may have run in the Auth scene before the cloud profile
            // finished loading, leaving NetName = UGS default (e.g. "CuteAwakingLightbulb").
            // By the time we enter a game scene, PlayerDataService is initialized and
            // CurrentProfile holds the menu display name (e.g. "dragon").
            if (IsLocalUser && playerDataService != null && playerDataService.IsInitialized
                && playerDataService.CurrentProfile != null)
            {
                var profile = playerDataService.CurrentProfile;
                if (!string.IsNullOrEmpty(profile.displayName) && NetName.Value.ToString() != profile.displayName)
                    NetName.Value = profile.displayName;
                if (NetAvatarId.Value != profile.avatarId)
                    NetAvatarId.Value = profile.avatarId;
            }

            // Reset server-writable NetworkVariables.
            if (IsServer)
                NetVesselId.Value = 0;

            // Force-sync local properties from NetworkVariables.
            // OnValueChanged callbacks only fire on actual changes;
            // if a value happens to be the same, the local property
            // would remain stale without this explicit sync.
            Domain = NetDomain.Value;
            Name = NetName.Value.ToString();
            AvatarId = NetAvatarId.Value;

            // Re-register with gameData (cleared by ResetRuntimeData during scene transition)
            if (!gameData.Players.Contains(this))
                gameData.Players.Add(this);
        }

        public void DestroyPlayer()
        {
            if (IsSpawned)
            {
                if (IsServer)
                    NetworkObject.Despawn(true);
                return;
            }
            Destroy(gameObject);
        }

        public void StartPlayer()
        {
            // Vessel can be null on non-host clients when a ClientRpc (e.g. countdown end)
            // arrives before ClientPlayerVesselInitializer has resolved the player-vessel pair.
            // Same transient Netcode state handled by ResetForPlay() below.
            if (Vessel == null)
            {
                Debug.LogWarning($"[Player] StartPlayer called on '{Name}' (NetObjId={NetworkObjectId}) " +
                                 "but Vessel is null — vessel pair not yet initialized. Skipping.");
                return;
            }

            ToggleActive(true);
            Vessel.StartVessel();
            ToggleInputIdle(false);

            if (IsNetworkClient)
                return;

            if (IsInitializedAsAI)
            {
                ToggleAIPilot(true);
                ToggleInputPause(true);
            }
            else
                ToggleInputPause(false);
        }
        

        public void ResetForPlay()
        {
            // Vessel can be null for persistent Players between scene transitions
            // (old vessel destroyed with scene, new vessel not yet spawned).
            Vessel?.ResetForPlay();
            ToggleActive(false);

            if (IsNetworkClient)
                return;

            if (IsInitializedAsAI && Vessel != null)
                ToggleAIPilot(false);

            InputStatus?.ResetForReplay();
        }

        public void ChangeVessel(IVessel vessel) =>
            Vessel = vessel;

        void ToggleActive(bool active) =>
            IsActive = active;

        void ToggleAIPilot(bool toggle) => 
            Vessel.ToggleAIPilot(toggle);
        
        void ToggleInputPause(bool toggle) => 
            InputController.SetPause(toggle);

        void ToggleInputIdle(bool toggle) =>
            InputController.SetIdle(toggle);
        
        void OnNetDomainChanged(Domains previousValue, Domains newValue)
        {
            Domain = newValue;

            // (a) Server keeps RoundStats authoritative; clients receive the change
            // via RoundStats.n_Domain.OnValueChanged — so writing here on the server
            // alone keeps every consumer of RoundStats.Domain (scoreboards, end-game
            // controllers, GameFeedAPI colorers) live across modal re-picks,
            // NormalizeUnassignedHumans rerolls, and shape-mode SetDomain calls.
            if (IsServer && _roundStats)
                _roundStats.Domain = newValue;

            // (b) Repaint the vessel materials. Skipped pre-spawn (no themeManagerData
            // stashed yet) and on Players whose vessel is null between scene transitions.
            if (Vessel != null && _vesselThemeManagerData != null)
                ShipHelper.SetShipProperties(_vesselThemeManagerData, Vessel);
        }
        
        void OnNetNameValueChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
        {
            Name = newValue.ToString();
            TryRaiseDeferredSpawnEvent();
        }

        void OnNetDefaultVesselTypeChanged(VesselClassType previousValue, VesselClassType newValue)
        {
            TryRaiseDeferredSpawnEvent();
        }

        /// <summary>
        /// Server-only: when a remote client's owner-written values (name, vessel type)
        /// replicate, check if we can now raise the spawn event that was deferred
        /// in OnNetworkSpawn because the owner block was skipped.
        /// </summary>
        void TryRaiseDeferredSpawnEvent()
        {
            if (IsServer && !_spawnEventRaised && IsSpawnReady())
            {
                _spawnEventRaised = true;
                gameData.OnPlayerNetworkSpawnedUlong.Raise(OwnerClientId);
            }
        }

        void OnNetVesselIdChanged(ulong previousValue, ulong newValue)
        {
            Debug.Log($"<color=#FF00FF>[PLAYER] OnNetVesselIdChanged '{Name}' — prev={previousValue}, new={newValue}, IsServer={IsServer}, IsOwner={IsOwner}</color>");
            VesselNetId = newValue;
            if (newValue == 0)
            {
                Debug.Log($"<color=#FF00FF>[PLAYER] Clearing Vessel+IsActive on '{Name}' (was VesselId={previousValue})</color>");
                Vessel = null;
                IsActive = false;
            }
        }

        void OnNetAvatarIdChanged(int previousValue, int newValue) =>
            AvatarId = newValue;
        
        bool IsSpawnReady() =>
            IsValidVesselTypeForSpawn(NetDefaultVesselType.Value)
            && !string.IsNullOrEmpty(NetName.Value.ToString());

        /// <summary>
        /// Returns true if the vessel type is a concrete, spawnable vessel
        /// (not Random, Any, or the default enum value).
        /// </summary>
        static bool IsValidVesselTypeForSpawn(VesselClassType type) =>
            type != VesselClassType.Random && type != VesselClassType.Any;

        void SetGameObjectName()
        {
            string playerName;
            if (IsInitializedAsAI)
                playerName = "AI";
            else
                playerName = "Player_" + OwnerClientId;
            gameObject.name = playerName;
        }

        /// <summary>
        /// Strips the "#XXXX" suffix that Unity Authentication appends to PlayerName.
        /// e.g. "MyName#1234" → "MyName"
        /// </summary>
        static string StripPlayerNameSuffix(string ugsName)
        {
            if (string.IsNullOrEmpty(ugsName)) return ugsName;
            int hashIndex = ugsName.LastIndexOf('#');
            return hashIndex > 0 ? ugsName.Substring(0, hashIndex) : ugsName;
        }
    }
}
