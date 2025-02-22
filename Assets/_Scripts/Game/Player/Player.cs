using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.UI;
using CosmicShore.Game.Projectiles;

namespace CosmicShore.Game
{
    [System.Serializable]
    public class Player : MonoBehaviour, IPlayer
    {
        [SerializeField] bool _selfSpawn;

        [SerializeField] string playerName;

        [SerializeField] GameObject shipContainer;
        // [SerializeField] ShipTypes defaultShip = ShipTypes.Dolphin;
        [SerializeField] bool UseHangarConfiguration = true;
        [SerializeField] bool IsAI = false;
        [SerializeField] IPlayer.InitializeData InitializeData;

        [SerializeField] Gun gun;

        public static Player ActivePlayer;

        public ShipTypes DefaultShipType { get; set; }
        public Teams Team { get; set; }
        public string Name { get; private set; }
        public string PlayerName { get; private set; }
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

        void Start()
        {
            if (_selfSpawn)
                Initialize(InitializeData);
        }

        public void Initialize(IPlayer.InitializeData data)
        {
            InitializeData = data;
            gameManager = GameManager.Instance;
            DefaultShipType = data.DefaultShipType;
            Team = data.Team;
            PlayerName = data.PlayerName;
            PlayerUUID = data.PlayerUUID;
            Name = data.PlayerName;

            Setup();
        }

        void Setup()
        {
            if (UseHangarConfiguration)
            {
                switch (PlayerName)
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
        public void ToggleActive(bool active) => IsActive = active;

        void SetupPlayerShip(IShip ship)
        {
            Ship = ship;
            Ship.Transform.SetParent(shipContainer.transform, false);
            Ship.ShipStatus.AIPilot.enabled = false;

            GameCanvas.MiniGameHUD.Ship = Ship;

            InitializeShip();
            InputController.Initialize(Ship);
            gun.Initialize(Ship);

            gameManager.WaitOnPlayerLoading();
        }

        void SetupAIShip(IShip ship)
        {
            Debug.Log($"Player - SetupAIShip - playerName: {PlayerName}");

            Ship = ship;

            // TODO: Verify this works in arcade games
            Ship.Transform.SetParent(shipContainer.transform, false);

            Ship.ShipStatus.AIPilot.enabled = true;

            InitializeShip();
            InputController.Initialize(Ship);

            gameManager.WaitOnAILoading(Ship.ShipStatus.AIPilot);
        }

        void InitializeShip() => Ship.Initialize(this);
    }
}