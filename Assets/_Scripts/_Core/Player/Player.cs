using UnityEngine;
using StarWriter.Core;
using StarWriter.Core.HangerBuilder;
using StarWriter.Core.Input;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField] string playerName;
    [SerializeField] string playerUUID;
    [SerializeField] Ship ship;
    [SerializeField] GameObject shipContainer;

    [Header("HUD Containers")]
    [SerializeField] ChargeDisplay boostDisplay;
    [SerializeField] ChargeDisplay levelDisplay;
    [SerializeField] ChargeDisplay ammoDisplay;

    public Teams Team;
    public string PlayerName { get => playerName; }
    public string PlayerUUID { get => playerUUID; }
    public Ship Ship { get => ship; }

    GameManager gameManager;

    void SetupAIShip(Ship shipInstance)
    {
        shipInstance.transform.SetParent(shipContainer.transform, false);
        shipInstance.GetComponent<AIPilot>().enabled = true;

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
                Ship shipInstance = Hangar.Instance.LoadPlayerShip(ShipTypes.Dolphin, Team);
                shipInstance.transform.SetParent(shipContainer.transform, false);
                shipInstance.GetComponent<AIPilot>().enabled = false;

                var inputController = GetComponent<InputController>();
                inputController.ship = shipInstance;

                ship = shipInstance.GetComponent<Ship>();
                ship.Team = Team;
                ship.Player = this;

                if (boostDisplay != null)
                    ship.GetComponent<ResourceSystem>().BoostDisplay = boostDisplay;
                if (levelDisplay != null)
                    ship.GetComponent<ResourceSystem>().LevelDisplay = levelDisplay;
                if (levelDisplay != null)
                    ship.GetComponent<ResourceSystem>().AmmoDisplay = ammoDisplay;

                //gameManager.WaitOnPlayerLoading();
                break;
            default: // Single player game 
                Ship shipInstance_ = Hangar.Instance.LoadPlayerShip(ShipTypes.Dolphin, Team);
                shipInstance_.transform.SetParent(shipContainer.transform, false);
                shipInstance_.GetComponent<AIPilot>().enabled = false;

                var inputController_ = GetComponent<InputController>();
                inputController_.ship = shipInstance_;

                ship = shipInstance_.GetComponent<Ship>();
                ship.Team = Team;
                ship.Player = this;

                if (boostDisplay != null)
                    ship.GetComponent<ResourceSystem>().BoostDisplay = boostDisplay;
                if (levelDisplay != null)
                    ship.GetComponent<ResourceSystem>().LevelDisplay = levelDisplay;
                if (levelDisplay != null)
                    ship.GetComponent<ResourceSystem>().AmmoDisplay = ammoDisplay;

                gameManager.WaitOnPlayerLoading();
                break;
        }
    }
}