using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.HangerBuilder;
using StarWriter.Core.IO;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField] string playerName;
    [SerializeField] string playerUUID;
    [SerializeField] Ship ship;
    [SerializeField] GameObject shipContainer;
    [SerializeField] public GameCanvas GameCanvas;
    [SerializeField] public ShipTypes defaultShip = ShipTypes.Dolphin;

    public static Player ActivePlayer;

    public Teams Team;
    public string PlayerName { get => playerName; set => playerName = value; }
    public string PlayerUUID { get => playerUUID; set => playerUUID = value; }
    public Ship Ship { get => ship; }

    GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;

        foreach (Transform child in shipContainer.transform) Destroy(child.gameObject);

        switch (playerName)
        {
            case "HostileOne":
                SetupAIShip(Hangar.Instance.LoadHostileAI1Ship());
                break;
            case "HostileTwo":
                SetupAIShip(Hangar.Instance.LoadHostileAI2Ship());
                break;
            case "FriendlyOne":
                SetupAIShip(Hangar.Instance.LoadFriendlyAIShip()); 
                break;
            case "PlayerOne":
            case "PlayerTwo":
            case "PlayerThree":
            case "PlayerFour":
                Debug.Log($"Player.Start - Instantiate Ship: {PlayerName}");
                Ship shipInstance = Hangar.Instance.LoadPlayerShip(defaultShip, Team);
                SetupPlayerShip(shipInstance);
                break;
            default: // Single player game 
                Debug.Log($"Player.Start - Instantiate Ship: Single Player");
                Ship shipInstance_ = Hangar.Instance.LoadPlayerShip();
                SetupPlayerShip(shipInstance_);
                gameManager.WaitOnPlayerLoading();
                break;
        }

        void SetupPlayerShip(Ship shipInstance)
        {
            ActivePlayer = this;

            shipInstance.transform.SetParent(shipContainer.transform, false);
            shipInstance.GetComponent<AIPilot>().enabled = false;

            GetComponent<InputController>().ship = shipInstance;
            
            ship = shipInstance.GetComponent<Ship>();
            ship.Team = Team;
            ship.Player = this;

            // TODO: P1 do we want to refactor to just give the resource system a display group?
            ship.ResourceSystem.BoostDisplay = GameCanvas.ResourceDisplayGroup.BoostDisplay;
            ship.ResourceSystem.AmmoDisplay = GameCanvas.ResourceDisplayGroup.AmmoDisplay;
            ship.ResourceSystem.ChargeDisplay = GameCanvas.ResourceDisplayGroup.ChargeDisplay;
            ship.ResourceSystem.ChargeLevelDisplay = GameCanvas.ResourceDisplayGroup.ChargeLevelDisplay;
            ship.ResourceSystem.MassLevelDisplay = GameCanvas.ResourceDisplayGroup.MassLevelDisplay;
            ship.ResourceSystem.SpaceLevelDisplay = GameCanvas.ResourceDisplayGroup.SpaceLevelDisplay;
            ship.ResourceSystem.TimeLevelDisplay = GameCanvas.ResourceDisplayGroup.TimeLevelDisplay;
        }

        void SetupAIShip(Ship shipInstance)
        {
            Debug.Log($"Player - SetupAIShip - playerName: {playerName}");

            shipInstance.transform.SetParent(shipContainer.transform, false);
            shipInstance.GetComponent<AIPilot>().enabled = true;

            var inputController = GetComponent<InputController>();
            inputController.ship = shipInstance;

            ship = shipInstance.GetComponent<Ship>();
            ship.Team = Team;
            ship.Player = this;
            ship.InputController = inputController;

            gameManager.WaitOnAILoading(ship.GetComponent<AIPilot>());
        }
    }
}