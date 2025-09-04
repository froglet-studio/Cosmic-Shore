using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Soap;
using CosmicShore.Utility.ClassExtensions;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_Player : NetworkBehaviour, IPlayer
    { 
        public static List<R_Player> NppList { get; private set; } = new();

        // Declare the NetworkVariable without initializing its value.
        public NetworkVariable<ShipClassType> NetDefaultShipType = new(ShipClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Teams> NetTeam = new();

        public ShipClassType ShipClass { get; private set; } // => InitializeData.ShipClass;   
        public Teams Team { get; private set; } // => InitializeData.Team;
        public string Name { get; private set; } // => InitializeData.PlayerName;
        public string PlayerUUID { get; private set; } // => InitializeData.PlayerUUID;
        public IShip Ship { get; private set; }
        public bool IsActive { get; private set; }

        readonly InputController _inputController;
        public InputController InputController =>
            _inputController != null ? _inputController : gameObject.GetOrAdd<InputController>();
        public IInputStatus InputStatus => InputController.InputStatus;

        public Transform Transform => transform;
        
        IPlayer.InitializeData InitializeData;
        

        public void Initialize(IPlayer.InitializeData data, IShip ship)
        {
            InitializeData = data;
            ShipClass = InitializeData.ShipClass;
            Team = InitializeData.Team;
            Name = InitializeData.PlayerName;
            PlayerUUID = InitializeData.PlayerUUID;
            Ship = ship;
            InputController.Initialize(Ship);
        }
        
        public void InitializeShip(IShip ship) => Ship = ship;
        
        public override void OnNetworkSpawn()
        {
            NppList.Add(this);
            gameObject.name = "Player_" + OwnerClientId;
            Name = AuthenticationService.Instance.PlayerName;
            PlayerUUID = Name;
            InputController.enabled = IsOwner;

            NetDefaultShipType.OnValueChanged += OnNetDefaultShipTypeValueChanged;
            NetTeam.OnValueChanged += OnNetTeamValueChanged;
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
            Ship.ToggleAutoPilot(toggle);
            InputController.Pause(toggle);   
        }

        public void ToggleStationaryMode(bool toggle) =>
            Ship.ShipStatus.IsStationary = toggle;

        public void ToggleInputStatus(bool toggle) =>
            InputController.Pause(toggle);

        private void OnNetDefaultShipTypeValueChanged(ShipClassType previousValue, ShipClassType newValue) =>
            ShipClass = newValue;

        private void OnNetTeamValueChanged(Teams previousValue, Teams newValue) =>
            Team = newValue;
    }
}