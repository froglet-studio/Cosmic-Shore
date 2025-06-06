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
        [SerializeField] internal bool IsAI = false;
        [SerializeField] IPlayer.InitializeData InitializeData;

        [SerializeField] Gun gun;

        public static Player ActivePlayer;

        public ShipTypes ShipType { get; set; }
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
            ShipType = data.DefaultShipType;
            Team = data.Team;
            PlayerName = data.PlayerName;
            PlayerUUID = data.PlayerUUID;
            Name = data.PlayerName;

            Setup();
        }

        public void InitializeShip(ShipTypes shipType, Teams team)
        {
            ShipType = shipType;
            Team = team;
        }

        public void ToggleGameObject(bool toggle) => gameObject.SetActive(toggle);
        public void ToggleActive(bool active) => IsActive = active;

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
                        SetupPlayerShip(Hangar.Instance.LoadPlayerShip(ShipType, Team));
                        break;
                }
            }
            else
            {
                if (IsAI)
                    SetupAIShip(Hangar.Instance.LoadShip(ShipType, Team));
                else
                {
                    SetupPlayerShip(Hangar.Instance.LoadPlayerShip());
                }
            }
        }

        void SetupPlayerShip(IShip ship)
        {
            Ship = ship;
            Ship.Transform.SetParent(shipContainer.transform, false);
            Ship.ShipStatus.AIPilot.enabled = false;

            GameCanvas.MiniGameHUD.Ship = Ship;

            InitializeShip();
            InputController.Initialize(Ship);
            // TODO: P0 - this is a stop gap to get ships loading again, but is not a full fix
            if (gun != null)
                gun.Initialize(Ship);

            gameManager.WaitOnPlayerLoading();
        }

        void SetupAIShip(IShip ship)
        {
            Debug.Log($"Player - SetupAIShip - playerName: {PlayerName}");

            Ship = ship;

            Ship.Transform.SetParent(shipContainer.transform, false);

            Ship.ShipStatus.AIPilot.enabled = true;

            InitializeShip();
            InputController.Initialize(Ship);

            gameManager.WaitOnAILoading(Ship.ShipStatus.AIPilot);
        }

        void InitializeShip() => Ship.Initialize(this);

        public void StartAutoPilot()
        {
            Ship.ShipStatus.AutoPilotEnabled = true;
            var ai = Ship?.ShipStatus?.AIPilot;
            if (ai == null)
            {
                Debug.LogError("StartAutoPilot: no AIPilot found on ShipStatus.");
                return;
            }

            ai.AssignShip(Ship);

            ai.Initialize(true);

            InputController.SetPaused(true);

            Debug.Log("StartAutoPilot: AI initialized and player input paused.");
        }

        public void StopAutoPilot()
        {
            Ship.ShipStatus.AutoPilotEnabled = false;
            var ai = Ship?.ShipStatus?.AIPilot;
            if (ai == null)
            {
                Debug.LogError("StopAutoPilot: no AIPilot found on ShipStatus.");
                return;
            }

            ai.Initialize(false);

            InputController.SetPaused(false);

            Debug.Log("StopAutoPilot: AI disabled and player input unpaused.");
        }


    }
}