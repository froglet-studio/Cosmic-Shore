﻿using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using CosmicShore.Game.AI;
using CosmicShore.Game.UI;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField] string playerName;
    [SerializeField] string playerUUID;
    [SerializeField] Ship ship;
    [SerializeField] GameObject shipContainer;
    [SerializeField] public GameCanvas GameCanvas;
    [SerializeField] public ShipTypes defaultShip = ShipTypes.Dolphin;
    [SerializeField] bool UseHangarConfiguration = true;
    [SerializeField] bool IsAI = false;

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
                    SetupPlayerShip(Hangar.Instance.LoadPlayerShip(defaultShip, Team));
                    break;
            }
        }
        else
        {
            if (IsAI)
                SetupAIShip(Hangar.Instance.LoadShip(defaultShip, Team));
            else
            {
                SetupPlayerShip(Hangar.Instance.LoadPlayerShip());
                gameManager.WaitOnPlayerLoading();
            }
        }
    }

    void SetupPlayerShip(Ship shipInstance)
    {
        ActivePlayer = this;

        shipInstance.transform.SetParent(shipContainer.transform, false);
        shipInstance.GetComponent<AIPilot>().enabled = false;

        ship = shipInstance;
        GetComponent<InputController>().ship = ship;
            
        GameCanvas.MiniGameHUD.ship = ship;
        ship.Team = Team;
        ship.Player = this;
        ship.ResourceSystem.ResourceDisplays = GameCanvas.ResourceDisplayGroup;
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