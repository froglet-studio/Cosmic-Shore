using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.SOAP;
using CosmicShore.Utility.ClassExtensions;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace CosmicShore.Game
{
    public class Player : NetworkBehaviour, IPlayer
    { 
        [SerializeField]
        MiniGameDataSO miniGameData;
        
        public static List<IPlayer> NppList { get; } = new();

        // Declare the NetworkVariable without initializing its value.
        public NetworkVariable<VesselClassType> NetDefaultShipType = new(VesselClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Domains> NetTeam = new();
        public NetworkVariable<FixedString128Bytes> NetName = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); 

        public VesselClassType VesselClass { get; private set; } // => InitializeData.ShipClass;   
        public Domains Domain { get; private set; } // => InitializeData.Team;
        public string Name { get; private set; } // => InitializeData.PlayerName;
        public string PlayerUUID => Name;
        public IVessel Vessel { get; private set; }
        public bool IsActive { get; private set; }
        public bool AutoPilotEnabled => Vessel.VesselStatus.AutoPilotEnabled;
        public bool IsInitializedAsAI { get; private set; }

        readonly InputController _inputController;
        public InputController InputController =>
            _inputController != null ? _inputController : gameObject.GetOrAdd<InputController>();
        public IInputStatus InputStatus => InputController.InputStatus;

        public Transform Transform => transform;
        public bool IsNetworkOwner => IsSpawned && IsOwner;
        
        IPlayer.InitializeData InitializeData;
        

        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel)
        {
            InitializeData = data;
            IsInitializedAsAI = InitializeData.IsAI;
            VesselClass = InitializeData.vesselClass;
            Domain = InitializeData.domain;
            Name = InitializeData.PlayerName;
            if (!IsInitializedAsAI)
                InputController.Initialize();
            Vessel = vessel;
            // Keep players stationary at initialize
            ToggleStationaryMode(true);
            ToggleInputPause(true);
        }

        /// <summary>
        /// TODO -> A temp way to initialize in multiplayer, try for better approach.
        /// </summary>
        public void InitializeForMultiplayerMode(IVessel vessel)
        {
            IsInitializedAsAI = false;
            Domain = NetTeam.Value;
            Name = NetName.Value.ToString();
            Vessel = vessel;
            ToggleStationaryMode(true);
            ToggleInputPause(true);
        }
        
        public override void OnNetworkSpawn()
        {
            NppList.Add(this);
            gameObject.name = "Player_" + OwnerClientId;
            
            NetDefaultShipType.OnValueChanged += OnNetDefaultShipTypeValueChanged;
            NetTeam.OnValueChanged += OnNetTeamValueChanged;
            NetName.OnValueChanged += OnNetNameValueChanged;
            
            if (IsOwner)
            {
                NetDefaultShipType.Value = miniGameData.selectedVesselClass.Value;
                InputController.Initialize();
                NetName.Value = AuthenticationService.Instance.PlayerName;
            }

            if (IsServer)
            {
                NetTeam.Value = TeamAssigner.AssignRandomTeam();
            }
        }
        
        public override void OnNetworkDespawn()
        {
            NppList.Remove(this);

            NetDefaultShipType.OnValueChanged -= OnNetDefaultShipTypeValueChanged;
            NetTeam.OnValueChanged -= OnNetTeamValueChanged;
            NetName.OnValueChanged -= OnNetNameValueChanged;
        }

        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);
        
        public void ToggleActive(bool active) => IsActive = active;
        
        public void ToggleStationaryMode(bool toggle) => Vessel.VesselStatus.IsStationary = toggle;

        public void ToggleAutoPilot(bool toggle) => Vessel.ToggleAutoPilot(toggle);
        
        public void ToggleInputPause(bool toggle) => InputController?.Pause(toggle);

        public void DestroyPlayer()
        {
            if (IsSpawned)
                return;
            Destroy(gameObject);
        }

        public void ResetForReplay()
        {
            if (IsSpawned && !IsServer)
                return;
            
            InputStatus.ResetForReplay();
            Vessel.ResetForReplay();
            ToggleStationaryMode(true);
            ToggleInputPause(true);
        }
        
        private void OnNetDefaultShipTypeValueChanged(VesselClassType previousValue, VesselClassType newValue) =>
            VesselClass = newValue;
        
        private void OnNetTeamValueChanged(Domains previousValue, Domains newValue) =>
            Domain = newValue;
        
        private void OnNetNameValueChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue) =>
            Name = newValue.ToString();
    }
}