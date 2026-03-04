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

        [Inject] private PlayerDataService playerDataService;

        public NetworkVariable<VesselClassType> NetDefaultVesselType = new(VesselClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Domains> NetDomain = new();
        public NetworkVariable<FixedString128Bytes> NetName = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<ulong> NetVesselId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> NetIsAI = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<int> NetAvatarId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public Domains Domain { get; private set; }

        /// <summary>
        /// Changes the player's domain at runtime. Used by shape mode to match
        /// the player's prism color to the collided shape's domain.
        /// </summary>
        public void SetDomain(Domains newDomain)
        {
            Domain = newDomain;
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
            Domain = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
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
            base.OnNetworkSpawn();

            // Players persist across Netcode scene transitions.
            // Connection approval spawns with destroyWithScene=false internally,
            // but we reinforce it so all clients consistently preserve Player objects.
            NetworkObject.DestroyWithScene = false;

            // Add to game data early so ServerPlayerVesselInitializer can find us.
            gameData.Players.Add(this);

            VesselNetId = NetVesselId.Value;

            NetDomain.OnValueChanged += OnNetDomainChanged;
            NetName.OnValueChanged += OnNetNameValueChanged;
            NetVesselId.OnValueChanged += OnNetVesselIdChanged;
            NetAvatarId.OnValueChanged += OnNetAvatarIdChanged;

            // Domain is NOT assigned here — it is the spawner's responsibility
            // (DomainAssigner pool management in ServerPlayerVesselInitializer).
            if (IsServer)
                NetIsAI.Value = IsInitializedAsAI;

            if (IsOwner)
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

                NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
            }

            InputController.Initialize();
        }

        public override void OnNetworkDespawn()
        {
            gameData.Players.Remove(this);

            NetDomain.OnValueChanged -= OnNetDomainChanged;
            NetName.OnValueChanged -= OnNetNameValueChanged;
            NetVesselId.OnValueChanged -= OnNetVesselIdChanged;
            NetAvatarId.OnValueChanged -= OnNetAvatarIdChanged;
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
            // Clear stale references from previous scene.
            // Vessels have destroyWithScene=true and are already destroyed.
            Vessel = null;
            IsActive = false;
            VesselNetId = 0;

            // Update owner-writable NetworkVariables to match new game config.
            if (IsOwner)
                NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;

            // Update server-writable NetworkVariables.
            if (IsServer)
            {
                NetDomain.Value = DomainAssigner.GetDomainsByGameModes(gameData.GameMode);
                NetVesselId.Value = 0;
            }

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
                return;
            Destroy(gameObject);
        }

        public void StartPlayer()
        {
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
        
        void OnNetDomainChanged(Domains previousValue, Domains newValue) =>
            Domain = newValue;
        
        void OnNetNameValueChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue) =>
            Name = newValue.ToString();

        void OnNetVesselIdChanged(ulong previousValue, ulong newValue) =>
            VesselNetId = newValue;

        void OnNetAvatarIdChanged(int previousValue, int newValue) =>
            AvatarId = newValue;

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
