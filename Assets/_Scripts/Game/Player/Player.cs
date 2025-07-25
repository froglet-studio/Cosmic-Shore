using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Utility.ClassExtensions;
using System;
using CosmicShore.Soap;


namespace CosmicShore.Game
{
    /// <summary>
    ///  DEPRECATED SCRIPT - Use R_Player instead
    /// </summary>
    public class Player : MonoBehaviour, IPlayer
    {
        [SerializeField] ShipPrefabContainer _shipPrefabContainer;

        [Tooltip("Set to true if this player should be spawned automatically at the start of the scene. Make sure to fill up the initialize data")]
        [SerializeField] bool _selfSpawn;

        // TODO -> only show the initialize data in inspector, if _selfSpawn is selected
        [Tooltip("Only needed if is self spawning")]
        [SerializeField] IPlayer.InitializeData InitializeData;

        [SerializeField] bool _isAI = false;

        public ShipClassType ShipClass { get; private set; }
        public Teams Team { get; private set; }
        public string PlayerName { get; private set; }
        public string PlayerUUID { get; private set; }
        public IShip Ship { get; private set; }
        public bool IsActive { get; private set; }

        readonly InputController _inputController;
        public InputController InputController =>
            _inputController != null ? _inputController : gameObject.GetOrAdd<InputController>();
        public IInputStatus InputStatus => InputController.InputStatus;

        public Transform Transform => transform;

        /*void Start()
        {
            if (_selfSpawn)
                Initialize(InitializeData);
        }
        */

        public void Initialize(IPlayer.InitializeData data, IShip ship)
        {
            InitializeData = data;
            ShipClass = data.ShipClass;
            Team = data.Team;
            PlayerName = data.PlayerName;
            PlayerUUID = data.PlayerUUID;

            if (ShipClass == ShipClassType.Random)
            {
                var values = Enum.GetValues(typeof(ShipClassType));
                var random = new System.Random();
                ShipClass = (ShipClassType)values.GetValue(random.Next(1, values.Length));
            }

            if (!_shipPrefabContainer.TryGetShipPrefab(ShipClass, out Transform shipPrefab))
            {
                Debug.LogError($"Could not find ship prefab for {ShipClass}");
                return;
            }

            // TODO - Dont Instantiate here, deprecated script!!
            // Instantiate(shipPrefab).TryGetComponent(out IShip ship);
            Ship = Hangar.Instance.SetShipProperties(ship, Team, !_isAI);
            Ship.Initialize(this, _isAI);
            
            if (_isAI) return;
            InputController.Initialize(Ship);
            CameraManager.Instance.Initialize(Ship);
        }

        // TODO - Unnecessary usage of two methods, can be replaced with a single method.
        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);
        public void ToggleAutoPilotMode(bool toggle)
        {
            throw new NotImplementedException();
        }

        public void ToggleStationaryMode(bool toggle)
        {
            throw new NotImplementedException();
        }

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