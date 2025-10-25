using System;
using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.SOAP;
using CosmicShore.Utility.ClassExtensions;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    public class Player : NetworkBehaviour, IPlayer
    { 
        [FormerlySerializedAs("miniGameData")] [SerializeField]
        GameDataSO gameData;
        
        public static List<IPlayer> NppList { get; } = new();

        // Declare the NetworkVariable without initializing its value.
        public NetworkVariable<VesselClassType> NetDefaultShipType = new(VesselClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Domains> NetTeam = new();
        public NetworkVariable<FixedString128Bytes> NetName = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); 
        
        public Domains Domain { get; private set; }
        public string Name { get; private set; }
        public string PlayerUUID => Name;
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
        public bool IsNetworkOwner => IsSpawned && IsOwner;
        public bool IsNetworkClient => IsSpawned && !IsOwner;
        public bool IsLocalPlayer => IsNetworkOwner || (!IsInitializedAsAI && !IsNetworkClient);
       
        IPlayer.InitializeData InitializeData;
        
        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel)
        {
            InitializeData = data;
            IsInitializedAsAI = InitializeData.IsAI;
            Domain = InitializeData.domain;
            Name = InitializeData.PlayerName;
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
            IsInitializedAsAI = false;
            Domain = NetTeam.Value;
            Name = NetName.Value.ToString();
            Vessel = vessel;

            if (!IsServer) 
                return;
            
            RoundStats.Name = Name;
            RoundStats.Domain = Domain;
        }
        
        public override void OnNetworkSpawn()
        {
            NppList.Add(this);
            gameObject.name = "Player_" + OwnerClientId;
            
            NetTeam.OnValueChanged += OnNetTeamDomainChanged;
            NetName.OnValueChanged += OnNetNameValueChanged;
            
            if (IsOwner)
            {
                NetDefaultShipType.Value = gameData.selectedVesselClass.Value;
                InputController.Initialize();
                NetName.Value = AuthenticationService.Instance.PlayerName;
            }

            if (IsServer)
            {
                NetTeam.Value = DomainAssigner.GetAvailableDomain();
            }
        }
        
        public override void OnNetworkDespawn()
        {
            NppList.Remove(this);
            
            NetTeam.OnValueChanged -= OnNetTeamDomainChanged;
            NetName.OnValueChanged -= OnNetNameValueChanged;
        }

        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);
        
        public void ToggleActive(bool active) => IsActive = active;
        
        public void ToggleStationaryMode(bool toggle) => Vessel.VesselStatus.IsStationary = toggle;

        public void ToggleAIPilot(bool toggle) => Vessel.ToggleAIPilot(toggle);
        
        public void ToggleInputPause(bool toggle) => InputController.Pause(toggle);

        public void DestroyPlayer()
        {
            if (IsSpawned)
                return;
            Destroy(gameObject);
        }

        public void ResetForPlay()
        {
            // Always reset the vessel and make it stationary.
            Vessel.ResetForPlay();
            ToggleStationaryMode(true);
            ToggleActive(false);

            if (IsInitializedAsAI)
                ToggleAIPilot(false);
            
            if (IsSpawned && !IsOwner)
                return;
                
            InputStatus.ResetForReplay();
            ToggleInputPause(true);
        }

        public void ChangeVessel(IVessel vessel)
        {
            Vessel = vessel;

            if (IsSpawned && IsOwner)
                NetDefaultShipType.Value = Vessel.VesselStatus.VesselType;
        }
        
        private void OnNetTeamDomainChanged(Domains previousValue, Domains newValue) =>
            Domain = newValue;
        
        private void OnNetNameValueChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue) =>
            Name = newValue.ToString();
    }
}