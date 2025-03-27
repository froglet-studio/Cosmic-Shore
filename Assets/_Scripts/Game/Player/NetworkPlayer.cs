using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.UI;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// This player is spawned to each client from server as 
    /// the main multiplayer player prefab instance.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour, IPlayer
    {
        public static List<NetworkPlayer> NppList { get; private set; } = new();

        [SerializeField]
        ShipTypes _defaultShipType;

        [SerializeField]
        Teams _defaultTeam;

        public ShipTypes ShipType { get => _defaultShipType; set => _defaultShipType = value; }

        // Declare the NetworkVariable without initializing its value.
        public NetworkVariable<ShipTypes> NetDefaultShipType = new();
        public NetworkVariable<Teams> NetTeam = new();

        public Teams Team { get; private set; }
        public string PlayerName { get; private set; }
        public string PlayerUUID { get; private set; }
        public string Name { get; private set; }

        InputController _inputController;
        public InputController InputController =>
            _inputController = _inputController != null ? _inputController : GetComponent<InputController>();

        public GameCanvas GameCanvas { get; private set; }
        public Transform Transform => transform;
        public bool IsActive { get; private set; } = false;

        IShip _ship;
        public IShip Ship => _ship;

        public override void OnNetworkSpawn()
        {
            NppList.Add(this);

            gameObject.name = "PersistentPlayer_" + OwnerClientId;

            if (IsServer)
            {
                // Initialize the network variable with the default value from the inspector.
                NetDefaultShipType.Value = _defaultShipType;
                NetTeam.Value = _defaultTeam;
            }

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

        private void OnNetDefaultShipTypeValueChanged(ShipTypes previousValue, ShipTypes newValue)
        {
            ShipType = newValue;
        }

        private void OnNetTeamValueChanged(Teams previousValue, Teams newValue)
        {
            Team = newValue;
        }

        public void Initialize(IPlayer.InitializeData data)
        {
            /*_defaultShipType = data.DefaultShipType;
            if (IsServer)
            {
                NetDefaultShipType.Value = data.DefaultShipType;
            }
            Team = data.Team;
            PlayerName = data.PlayerName;
            PlayerUUID = data.PlayerUUID;
            Name = data.PlayerName;*/
        }

        public void ToggleActive(bool active) => IsActive = active;

        /// <summary>
        /// Setup the player.
        /// </summary>
        /// <param name="ship">The ship instance.</param>
        /// <param name="isOwner">Is this player owned by this client?</param>
        public void Setup(IShip ship)
        {
            _ship = ship;
            _ship = Hangar.Instance.LoadPlayerShip(_ship, _ship.ShipStatus.Team, IsOwner);

            _ship.Initialize(this);

            if (IsOwner)
            {
                /*GameCanvas = FindAnyObjectByType<GameCanvas>(); - DECIDE GAME CANVAS LATER
                GameCanvas.MiniGameHUD.Ship = _ship;*/
                InputController.Initialize(_ship);
            }
        }

        /// <summary>
        /// Sets the default ship type via the network variable.
        /// This method should only be called on the server.
        /// </summary>
        public void InitializeShip(ShipTypes shipType, Teams team)
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
    }
}
