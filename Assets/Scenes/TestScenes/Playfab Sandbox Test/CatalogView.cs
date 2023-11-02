using _Scripts._Core.Playfab_Models.Economy;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore;

public class CatalogView : MonoBehaviour
{
    [SerializeField] private Button purchaseVesselButton;
    [SerializeField] private Button addShardsButton;
    const string VesselId = "aaf7670c-96a2-46d8-9489-0d971c6dc742";
    const string ShardId = "88be4041-cc48-4231-8595-d440b371d015";
    [SerializeField] private int amount = 100;
    
    // Start is called before the first frame update
    void Start()
    {
        purchaseVesselButton.onClick.AddListener(PurchaseItemTest);
        addShardsButton.onClick.AddListener(AddShardsTest);
    }
    
    /// <summary>
    /// Purchase Item Test
    /// Buy a test vessel using shards, the amount of shards should be the exact price tag on the test vessel shards
    /// </summary>
    private void PurchaseItemTest()
    {
        
        CatalogManager.Instance.PurchaseItem(VesselId, ShardId, 1, 5);
    }

    /// <summary>
    /// Add Currency (Shards) Test
    /// Add shards to player inventory
    /// </summary>
    private void AddShardsTest()
    {
        var inventoryItemRef = new VirtualItemModel() { Id = ShardId };
        CatalogManager.Instance.AddInventoryItem(inventoryItemRef, amount);
        // RefreshInventory();
    }
    
    /// <summary>
    /// Refresh Inventory
    /// Update to see the contents inside player inventory
    /// </summary>
    private void RefreshInventory()
    {
        CatalogManager.Instance.LoadInventory();
    }
}
