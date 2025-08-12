using CosmicShore.Core;
using CosmicShore.Game.IO;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// DEPRECATED - Use R_Player instead
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour, IPlayer
    {
        public static List<NetworkPlayer> NppList { get; private set; } = new();

        // Declare the NetworkVariable without initializing its value.
        public NetworkVariable<ShipClassType> NetDefaultShipType = new(ShipClassType.Random, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Teams> NetTeam = new();

        public ShipClassType ShipClass { get; set; }
        public Teams Team { get; private set; }

        public string PlayerName { get; private set; }
        public string PlayerUUID { get; private set; }

        InputController _inputController;
        public InputController InputController =>
            _inputController = _inputController != null ? _inputController : GetComponent<InputController>();

        public IInputStatus InputStatus => InputController.InputStatus;

        // public GameCanvas GameCanvas { get; private set; }
        public Transform Transform => transform;
        public bool IsActive { get; private set; } = false;

        IShip _ship;
        public IShip Ship => _ship;

        public override void OnNetworkSpawn()
        {
            NppList.Add(this);

            gameObject.name = "NetworkPlayer_" + OwnerClientId;


            /* It is not necessary anymore as MultiplayerSetup is defining the default ship type and team.
            if (IsServer)
            {
                // Initialize the network variable with the default value from the inspector.
                NetDefaultShipType.Value = _defaultShipType;
                NetTeam.Value = _defaultTeam;
            }*/

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

        private void OnNetDefaultShipTypeValueChanged(ShipClassType previousValue, ShipClassType newValue)
        {
            ShipClass = newValue;
        }

        private void OnNetTeamValueChanged(Teams previousValue, Teams newValue)
        {
            Team = newValue;
        }

        public void Initialize(IPlayer.InitializeData data, IShip ship)
        {
            PlayerName = AuthenticationService.Instance.PlayerName;
            PlayerUUID = PlayerName;

            // _ship = data.Ship;
            _ship = Hangar.Instance.SetShipProperties(_ship, _ship.ShipStatus.Team, IsOwner);

            _ship.Initialize(this, false);

            if (IsOwner)
            {
                /*GameCanvas = FindAnyObjectByType<GameCanvas>(); - DECIDE GAME CANVAS LATER
                GameCanvas.MiniGameHUD.Ship = _ship;*/
                InputController.Initialize(_ship);
            }
        }

        public void ToggleActive(bool active) => IsActive = active;

        /// <summary> - NOT NEEDED IF WE OMIT CHARACTER SELECT SCENE
        /// Sets the default ship type via the network variable.
        /// This method should only be called on the server.
        /// </summary>
        public void InitializeShip(ShipClassType shipType, Teams team)
        {
            if (IsServer)
            {
                NetDefaultShipType.Value = shipType;
                NetTeam.Value = team;
            }
            else
            {
                Debug.LogWarning("Only the server can update the default ship type.");
            }
        }

        public void ToggleGameObject(bool toggle) =>
            gameObject.SetActive(toggle);

        public void ToggleAutoPilotMode(bool toggle)
        {
            throw new NotImplementedException();
        }

        public void ToggleStationaryMode(bool toggle)
        {
            throw new NotImplementedException();
        }
    }
}
