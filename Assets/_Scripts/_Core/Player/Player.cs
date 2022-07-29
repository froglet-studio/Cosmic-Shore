using UnityEngine;
using StarWriter.Core;
using System.Collections;

[System.Serializable]
public class Player : MonoBehaviour
{
    [SerializeField]
    private string playerName;
    
    [SerializeField]
    private string playerUUID;

    [SerializeField]
    SO_Character_Base playerSO;

    [SerializeField]
    private Color playerColor = Color.black;
    
    [SerializeField]    
    private SO_Ship_Base playerShipPrefab;
    
    [SerializeField]
    private SO_Trail_Base playerTrailPrefab;

    public string PlayerName { get => playerName; }
    public string PlayerUUID { get => playerUUID; }
    public Color PlayerColor { get => playerColor; }
    public SO_Ship_Base PlayerShipPrefab { get => playerShipPrefab; }
    public SO_Trail_Base PlayerTrailPrefab { get => playerTrailPrefab; }

    GameManager gameManager;

    void Start()
    {
        InitializePlayer();
        if(playerUUID == "admin")
        {
            Debug.Log("Player " + playerName + " fired up and ready to go!");
            gameManager = GameManager.Instance;
            gameManager.WaitOnPlayerLoading();
        }
    }

    //Sets Player Fields from the assigned Scriptable Object 
    void InitializePlayer()
    {
        playerName = playerSO.CharacterName;
        playerUUID = playerSO.UniqueUserID;
        playerColor = playerSO.CharacterColor;
        playerShipPrefab = playerSO.ShipPrefab;
        playerTrailPrefab = playerSO.TrailPrefab;

        GameManager.Instance.player = this;
    }

    public void ChangeShip(SO_Ship_Base ship) 
    {
        playerSO.ShipPrefab = ship;
    }

    public void ChangeTrail(SO_Trail_Base trail)
    {
        playerSO.TrailPrefab = trail;
    }

    public void ChangeColor(Color color)
    {
        playerSO.CharacterColor = color;
    }

    public void ToggleCollision(bool enabled)
    {
        foreach (var collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = enabled;
    }

}