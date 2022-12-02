using UnityEngine;
using StarWriter.Core;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField] string playerName;
    [SerializeField] string playerUUID;
    [SerializeField] Ship ship;
    public Team Team;

    public string PlayerName { get => playerName; }
    public string PlayerUUID { get => playerUUID; }
    public Ship Ship { get => ship; }

    GameManager gameManager;

    void Start()
    {
        if (playerUUID == "admin")  //TODO check if this is local client
        {
            Debug.Log("Player " + playerName + " fired up and ready to go!");
            gameManager = GameManager.Instance;
            gameManager.WaitOnPlayerLoading();
        }
    }
}