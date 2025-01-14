using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.AI;
using CosmicShore.Game.UI;
using CosmicShore.Game;
using Unity.Android.Gradle.Manifest;

namespace CosmicShore.Game
{
    [System.Serializable]
    public class Player : MonoBehaviour, IPlayer
    {
        [SerializeField] string playerName;

        [SerializeField] GameObject shipContainer;
        // [SerializeField] ShipTypes defaultShip = ShipTypes.Dolphin;
        [SerializeField] bool UseHangarConfiguration = true;
        [SerializeField] bool IsAI = false;
        [SerializeField] IPlayer.InitializeData InitializeData;

        public static Player ActivePlayer;

        public ShipTypes DefaultShipType { get; set; }
        public Teams Team { get; set; }
        public string Name { get; private set; }
        public string PlayerName
        {
            get
            {
                return _playerName;
            }
            set
            {
                _playerName = value;
            }
        }
        public string PlayerUUID { get; set; }
        public IShip Ship { get; private set; }
        public bool IsActive { get; private set; }

        public GameCanvas GameCanvas =>
            _gameCanvas != null ? _gameCanvas : FindFirstObjectByType<GameCanvas>();

        InputController _inputController;
        public InputController InputController =>
            _inputController != null ? _inputController : GetComponent<InputController>();

        public Transform Transform => transform;

        protected GameManager gameManager;
        GameCanvas _gameCanvas;
        string _playerName;

        void Start()
        {
            Initialize(InitializeData);
        }

        public void Initialize(IPlayer.InitializeData data)
        {
            gameManager = GameManager.Instance;
            DefaultShipType = data.DefaultShipType;
            Team = data.Team;
            _playerName = data.PlayerName;
            PlayerUUID = data.PlayerUUID;
            Name = data.PlayerName;

            Setup();
        }

        public void Setup()
        {
            if (UseHangarConfiguration)
            {
                switch (playerName)
                {
                    case "HostileOne":
                        SetupAIShip(Hangar.Instance.LoadHostileAI1Ship(Team));
                        break;
                    case "HostileTwo":
                        SetupAIShip(Hangar.Instance.LoadHostileAI2Ship());
                        break;
                    case "HostileThree":
                        SetupAIShip(Hangar.Instance.LoadHostileAI3Ship());
                        break;
                    case "FriendlyOne":
                        SetupAIShip(Hangar.Instance.LoadFriendlyAIShip());
                        break;
                    case "SquadMateOne":
                        SetupAIShip(Hangar.Instance.LoadSquadMateOne());
                        break;
                    case "SquadMateTwo":
                        SetupAIShip(Hangar.Instance.LoadSquadMateTwo());
                        break;
                    case "HostileManta":
                        SetupAIShip(Hangar.Instance.LoadHostileManta());
                        break;
                    case "PlayerOne":
                    case "PlayerTwo":
                    case "PlayerThree":
                    case "PlayerFour":
                    default: // Default will be the players Playfab username
                        Debug.Log($"Player.Start - Instantiate Ship: {PlayerName}");
                        SetupPlayerShip(Hangar.Instance.LoadPlayerShip(DefaultShipType, Team));
                        gameManager.WaitOnPlayerLoading();
                        break;
                }
            }
            else
            {
                if (IsAI)
                    SetupAIShip(Hangar.Instance.LoadShip(DefaultShipType, Team));
                else
                {
                    SetupPlayerShip(Hangar.Instance.LoadPlayerShip());
                }
            }
        }

        public void SetDefaultShipType(ShipTypes shipType) => DefaultShipType = shipType;

        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);

        protected virtual void SetupPlayerShip(IShip ship)
        {
            Ship = ship;
            Ship.Transform.SetParent(shipContainer.transform, false);
            Ship.AIPilot.enabled = false;

            InputController.Ship = Ship;
            GameCanvas.MiniGameHUD.Ship = Ship;

            Ship.Initialize(this, Team);

            gameManager.WaitOnPlayerLoading();
        }

        void SetupAIShip(IShip ship)
        {
            Debug.Log($"Player - SetupAIShip - playerName: {PlayerName}");

            Ship = ship;
            Ship.AIPilot.enabled = true;

            InputController.Ship = ship;
            Ship.Initialize(this, Team);

            gameManager.WaitOnAILoading(Ship.AIPilot);
        }

        public void ToggleActive(bool active) => IsActive = active;
    }
}