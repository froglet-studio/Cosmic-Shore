using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.SOAP;
using CosmicShore.Utility.ClassExtensions;
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
        public NetworkVariable<Teams> NetTeam = new();

        public VesselClassType VesselClass { get; private set; } // => InitializeData.ShipClass;   
        public Teams Team { get; private set; } // => InitializeData.Team;
        public string Name { get; private set; } // => InitializeData.PlayerName;
        public string PlayerUUID { get; private set; } // => InitializeData.PlayerUUID;
        public IVessel Vessel { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsAIModeActivated { get; private set; }

        readonly InputController _inputController;
        public InputController InputController =>
            _inputController != null ? _inputController : gameObject.GetOrAdd<InputController>();
        public IInputStatus InputStatus => InputController.InputStatus;

        public Transform Transform => transform;
        
        IPlayer.InitializeData InitializeData;
        

        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel)
        {
            InitializeData = data;
            VesselClass = InitializeData.vesselClass;
            Team = InitializeData.Team;
            Name = InitializeData.PlayerName;
            PlayerUUID = InitializeData.PlayerUUID;
            InputController.Initialize();
            Vessel = vessel;
        }

        /// <summary>
        /// TODO -> A temp way to initialize in multiplayer, try for better approach.
        /// </summary>
        public void InitializeForMultiplayerMode(IVessel vessel)
        {
            Team = NetTeam.Value;
            Vessel = vessel;
        }
        
        public override void OnNetworkSpawn()
        {
            NppList.Add(this);
            gameObject.name = "Player_" + OwnerClientId;
            Name = AuthenticationService.Instance.PlayerName;
            PlayerUUID = Name;
            
            NetDefaultShipType.OnValueChanged += OnNetDefaultShipTypeValueChanged;
            NetTeam.OnValueChanged += OnNetTeamValueChanged;
            
            if (IsOwner)
            {
                NetDefaultShipType.Value = miniGameData.SelectedShipClass.Value;
                InputController.Initialize();
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
        }

        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);
        public void ToggleActive(bool active) => IsActive = active;

        public void ToggleAutoPilotMode(bool toggle)
        {
            Vessel.ToggleAutoPilot(toggle);
            InputController.Pause(toggle);   
            IsAIModeActivated = toggle;
        }

        public void ToggleStationaryMode(bool toggle) =>
            Vessel.VesselStatus.IsStationary = toggle;

        public void ToggleInputStatus(bool toggle) =>
            InputController.Pause(toggle);
        
        public void Destroy() => Destroy(gameObject);

        private void OnNetDefaultShipTypeValueChanged(VesselClassType previousValue, VesselClassType newValue) =>
            VesselClass = newValue;

        private void OnNetTeamValueChanged(Teams previousValue, Teams newValue) =>
            Team = newValue;
    }
}