using UnityEngine;
using StarWriter.Core;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField] string playerName;
    [SerializeField] string playerUUID;
    [SerializeField] SO_Ship playerShipPrefab;
    [SerializeField] SO_Trail_Base playerTrailPrefab;

    public string PlayerName { get => playerName; }
    public string PlayerUUID { get => playerUUID; }
    public SO_Ship PlayerShipPrefab { get => playerShipPrefab; }
    public SO_Trail_Base PlayerTrailPrefab { get => playerTrailPrefab; }

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

    public void ToggleCollision(bool enabled)
    {
        foreach (var collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = enabled;
    }
}