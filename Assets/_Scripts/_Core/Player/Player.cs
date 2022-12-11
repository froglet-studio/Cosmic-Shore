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

    public Teams Team;
    public string PlayerName { get => playerName; }
    public string PlayerUUID { get => playerUUID; }
    public Ship Ship { get => ship; }

    GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;

        foreach (Transform child in shipContainer.transform) Destroy(child.gameObject);

        if (playerUUID == "admin")  //TODO check if this is local client
        {
            Ship shipInstance = Hangar.Instance.LoadPlayerShip();
            shipInstance.transform.SetParent(shipContainer.transform, false);
            shipInstance.GetComponent<AIPilot>().enabled = false;

            var inputController = GetComponent<InputController>();
            inputController.shipTransform = shipInstance.transform;
            inputController.shipData = shipInstance.GetComponent<ShipData>();
            inputController.shipAnimation = shipInstance.GetComponent<ShipAnimation>();

            ship = shipInstance.GetComponent<Ship>();
            ship.Team = Team;
            ship.Player = this;

            gameManager.WaitOnPlayerLoading();
        }
        else
        {
            // TODO: random dice roll, or opposite of player ship selection
            Ship shipInstance = Hangar.Instance.LoadAI1Ship();
            shipInstance.transform.SetParent(shipContainer.transform, false);
            shipInstance.GetComponent<AIPilot>().enabled = true;

            // TODO: should AIPilot script be getting setup here? Yes probably. The AIPilot should likely be on

            ship = shipInstance.GetComponent<Ship>();
            ship.Team = Team;
            ship.Player = this;

            gameManager.WaitOnAILoading(ship.GetComponent<AIPilot>());
        }
    }
}