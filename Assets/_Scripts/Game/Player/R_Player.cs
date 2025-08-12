using System;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Soap;
using CosmicShore.Utility.ClassExtensions;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_Player : NetworkBehaviour, IPlayer
    { 
        public ShipClassType ShipClass => InitializeData.ShipClass;   
        public Teams Team => InitializeData.Team;
        public string PlayerName => InitializeData.PlayerName;
        public string PlayerUUID => InitializeData.PlayerUUID;
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
            Ship = ship;
            InputController.Initialize(Ship);
        }

        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);
        public void ToggleActive(bool active) => IsActive = active;

        public void ToggleAutoPilotMode(bool toggle)
        {
            Ship.ToggleAutoPilot(toggle);
            InputController.SetPaused(toggle);   
        }

        public void ToggleStationaryMode(bool toggle) =>
            Ship.ShipStatus.IsStationary = toggle;
    }
}