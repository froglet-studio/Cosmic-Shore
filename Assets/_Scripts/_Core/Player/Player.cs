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
    [SerializeField] Skimmer skimmer;
    [SerializeField] GameObject shipContainer;

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
        ship.skimmer.Player = this;

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
            default: // Player
                Ship shipInstance = Hangar.Instance.LoadPlayerShip();
                shipInstance.transform.SetParent(shipContainer.transform, false);
                shipInstance.GetComponent<AIPilot>().enabled = false;

                var inputController = GetComponent<InputController>();
                inputController.ship = shipInstance;

                ship = shipInstance.GetComponent<Ship>();
                ship.Team = Team;
                ship.Player = this;
                ship.skimmer.Player = this;

                gameManager.WaitOnPlayerLoading();
                break;
        }
    }

    public Ship[] LoadSecondShip(ShipTypes PlayerShipType)
    {
        Ship shipInstance2 = Hangar.Instance.LoadSecondPlayerShip(PlayerShipType);
        shipInstance2.transform.SetParent(shipContainer.transform, false);
        shipInstance2.GetComponent<AIPilot>().enabled = false;

        var inputController = GetComponent<InputController>();
        inputController.ship = shipInstance2;
        ship = shipInstance2.GetComponent<Ship>();
        ship.Team = Team;
        ship.Player = this;
        //skimmer.Player = this;
        ship.skimmer = skimmer;
        shipInstance2.enabled = false;
        Ship[] ships = new Ship[] { ship, shipInstance2 };
        return ships;
    }

}