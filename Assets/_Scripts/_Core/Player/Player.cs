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
    //[SerializeField] AIGunner aiGunner;
    public Team Team;

    public string PlayerName { get => playerName; }
    public string PlayerUUID { get => playerUUID; }
    public Ship Ship { get => ship; }

    GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;

        if (playerUUID == "admin")  //TODO check if this is local client
        {
            SO_Ship shipSO = Hangar.Instance.LoadPlayerShip();
            foreach (Transform child in shipContainer.transform) Destroy(child.gameObject);
            
            var shipInstance = Instantiate(shipSO.Prefab);
            shipInstance.transform.SetParent(shipContainer.transform, false);

            var inputController = GetComponent<InputController>();
            inputController.shipTransform = shipInstance.transform;

            ship = shipInstance.GetComponent<Ship>();
            ship.Team = Team;
            ship.Player = this;

            gameManager.WaitOnPlayerLoading();
        }
        else
        {
            Hangar.Instance.LoadAIShip();

            // TODO: random dice roll, or opposite of player ship selection
            SO_Ship shipSO = Hangar.Instance.LoadAIShip();
            var shipInstance = Instantiate(shipSO.Prefab);
            shipInstance.transform.SetParent(shipContainer.transform, false);
            ship = shipInstance.GetComponent<Ship>();

            ship.Team = Team;
            ship.Player = this;
            //aiGunner.trailSpawner = ship.TrailSpawner;

            gameManager.WaitOnAILoading(ship.GetComponent<AIPilot>());
        }
    }
}