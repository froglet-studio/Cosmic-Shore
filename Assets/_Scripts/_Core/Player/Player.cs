using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.HangerBuilder;
using StarWriter.Core.IO;
using UnityEngine.Serialization;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField] string playerName;
    [SerializeField] string playerUUID;
    [SerializeField] Ship ship;
    [SerializeField] GameObject shipContainer;

    [Header("HUD Containers")]
    [SerializeField] ResourceDisplay boostDisplay;
    [SerializeField] ResourceDisplay levelDisplay;
    [SerializeField] ResourceDisplay ammoDisplay;
    [SerializeField] ResourceDisplay chargeDisplay;
    [SerializeField] ResourceDisplay ChargeLevelDisplay;
    [SerializeField] ResourceDisplay MassLevelDisplay;
    [FormerlySerializedAs("SpaceTimeLevelDisplay")]
    [SerializeField] ResourceDisplay SpaceLevelDisplay;
    [SerializeField] ResourceDisplay TimeLevelDisplay;


    public Teams Team;
    public string PlayerName { get => playerName; set => playerName = value; }
    public string PlayerUUID { get => playerUUID; set => playerUUID = value; }
    public Ship Ship { get => ship; }

    [SerializeField] public ShipTypes defaultShip = ShipTypes.Dolphin;

    GameManager gameManager;

    void SetupAIShip(Ship shipInstance)
    {
        shipInstance.transform.SetParent(shipContainer.transform, false);
        shipInstance.GetComponent<AIPilot>().enabled = true;

        var inputController = GetComponent<InputController>();
        inputController.ship = shipInstance;

        ship = shipInstance.GetComponent<Ship>();
        ship.Team = Team;
        ship.Player = this;

        gameManager.WaitOnAILoading(ship.GetComponent<AIPilot>());
    }

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
            shipInstance.transform.SetParent(shipContainer.transform, false);
            shipInstance.GetComponent<AIPilot>().enabled = false;

            var inputController = GetComponent<InputController>();
            inputController.ship = shipInstance;

            ship = shipInstance.GetComponent<Ship>();
            ship.Team = Team;
            ship.Player = this;

            if (boostDisplay != null)
                ship.ResourceSystem.BoostDisplay = boostDisplay;
            if (levelDisplay != null)
                ship.ResourceSystem.LevelDisplay = levelDisplay;
            if (ammoDisplay != null)
                ship.ResourceSystem.AmmoDisplay = ammoDisplay;
            if (chargeDisplay != null)
                ship.ResourceSystem.ChargeDisplay = chargeDisplay;
            if (ChargeLevelDisplay != null)
                ship.ResourceSystem.ChargeLevelDisplay = ChargeLevelDisplay;
            if (MassLevelDisplay != null)
                ship.ResourceSystem.MassLevelDisplay = MassLevelDisplay;
            if (SpaceLevelDisplay != null)
                ship.ResourceSystem.SpaceLevelDisplay = SpaceLevelDisplay;
            if (TimeLevelDisplay != null)
                ship.ResourceSystem.TimeLevelDisplay = TimeLevelDisplay;
        }
    }
}