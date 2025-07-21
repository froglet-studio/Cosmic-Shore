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
        public ShipClassType ShipType => InitializeData.ShipType;   
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

        public void StartAutoPilot()
        {
            var ai = Ship?.ShipStatus?.AIPilot;
            if (!ai)
            {
                Debug.LogError("StartAutoPilot: no AIPilot found on ShipStatus.");
                return;
            }

            // TODO - It should not initialize,
            /*ai.AssignShip(Ship);
            ai.Initialize(true);*/

            InputController.SetPaused(true);
            // Debug.Log("StartAutoPilot: AI initialized and player input paused.");
        }

        public void StopAutoPilot()
        {
            var ai = Ship?.ShipStatus?.AIPilot;
            if (!ai)
            {
                Debug.LogError("StopAutoPilot: no AIPilot found on ShipStatus.");
                return;
            }

            // TODO - It should not initialize,
            // ai.Initialize(false);

            InputController.SetPaused(false);
            // Debug.Log("StopAutoPilot: AI disabled and player input unpaused.");
        }
    }
}