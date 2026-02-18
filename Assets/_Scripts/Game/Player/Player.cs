using System;
using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.Soap;
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
        
        public NetworkVariable<VesselClassType> NetDefaultVesselType = new(VesselClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Domains> NetDomain = new();
        public NetworkVariable<FixedString128Bytes> NetName = new(string.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<ulong> NetVesselId = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> NetIsAI = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        
        public Domains Domain { get; private set; }
        public string Name { get; private set; }
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
            IsInitializedAsAI = NetIsAI.Value;
            Domain = NetDomain.Value;
            Name = NetName.Value.ToString();
            Vessel = vessel;

            if (!IsServer) 
                return;
            
            RoundStats.Name = Name;
            RoundStats.Domain = Domain;
            
            SetGameObjectName();
        }
        
        public override void OnNetworkSpawn()
        {
            // Cache it to game data early, so that later,
            // ClientInitializer can find the player and vessels with their Ids
            gameData.Players.Add(this);

            VesselNetId = NetVesselId.Value;
            
            NetDomain.OnValueChanged += OnNetDomainChanged;
            NetName.OnValueChanged += OnNetNameValueChanged;
            NetVesselId.OnValueChanged += OnNetVesselIdChanged;

            if (!IsLocalUser) 
                return;
            
            NetDefaultVesselType.Value = gameData.selectedVesselClass.Value;
            NetName.Value = AuthenticationService.Instance.PlayerName;
            InputController.Initialize();
        }
        
        public override void OnNetworkDespawn()
        {
            NetDomain.OnValueChanged -= OnNetDomainChanged;
            NetName.OnValueChanged -= OnNetNameValueChanged;
            NetVesselId.OnValueChanged += OnNetVesselIdChanged;
        }


        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => 
            gameObject.SetActive(toggle);

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
            // Always reset the vessel and make it stationary.
            Vessel.ResetForPlay();
            ToggleActive(false);
            
            if (IsNetworkClient)
                return;
            
            if (IsInitializedAsAI)
                ToggleAIPilot(false);
                
            InputStatus.ResetForReplay();
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
        
        void SetGameObjectName()
        {
            string playerName;
            if (IsInitializedAsAI)
                playerName = "AI";
            else
                playerName = "Player_" + OwnerClientId;
            gameObject.name = playerName;
        }
    }
}
