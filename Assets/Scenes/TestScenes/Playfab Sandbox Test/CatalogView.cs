using System.Collections.Generic;
using _Scripts._Core.Playfab_Models.Economy;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore;
using UnityEngine.Serialization;

public class CatalogView : MonoBehaviour
{
    [Header("Test Buttons")]
    [SerializeField] private Button purchaseVesselButton;
    [SerializeField] private Button grantStartingItemsButton;
    [SerializeField] private Button loadCatalogItemsButton;
    [SerializeField] private Button loadInventoryButton;
    
    // test strings
    const string MantaShipUpgrade1Id = "6b5264af-4645-4aaa-8228-3b35ed379585";
    const string MantaShipUpgrade2Id = "806f1840-a0de-4463-8b56-4b43b07c3d5a";
    const string VesselShardId = "06bcebb1-dc41-49a8-82b0-96a15ced7c1c";
    
    
    
    // Start is called before the first frame update
    void Start()
    {
        purchaseVesselButton.onClick.AddListener(PurchaseItemTest);
        grantStartingItemsButton.onClick.AddListener(GrantStartingInventoryTest);
        loadCatalogItemsButton.onClick.AddListener(GetCatalogItemsTest);
        loadInventoryButton.onClick.AddListener(LoadInventoryTest);
    }
    
    /// <summary>
    /// Purchase Item Test
    /// Buy a test vessel using shards, the amount of shards should be the exact price tag on the test vessel shards
    /// </summary>
    private void PurchaseItemTest()
    {
        var vesselShard = new VirtualItemModel{Id = VesselShardId, Amount = 5};
        var mantaSpaceUpgrade1 = new VirtualItemModel { Id = MantaShipUpgrade1Id, Amount = 1 };
        CatalogManager.Instance.PurchaseItem(vesselShard, mantaSpaceUpgrade1);
    }

    /// <summary>
    /// Grant Starting Inventory Item Quantity (With starting items)
    /// Experimental method - should be handled by 
    /// Nothing magical here, default item quantity is 100, Granted when player created their account.
    /// </summary>
    private void GrantStartingInventoryTest()
    {
        // For now it's 100 vessel shards
        var vesselShard = new VirtualItemModel
        {
            Id = VesselShardId,
            ContentType = nameof(VirtualItemContentTypes.VesselShard),
            Amount = 100
        };
        var startingItems = new List<VirtualItemModel> { vesselShard };
        CatalogManager.Instance.GrantStartingInventory(startingItems);
    }

    /// <summary>
    /// Get Catalog Items Test
    /// </summary>
    private void GetCatalogItemsTest()
    {
        // var filter = "ContentType eq 'Vessel' and tags/any(t: t eq 'Rhino')";
        
        // Default filter is "", which means load without filter
        CatalogManager.Instance.LoadCatalogItems();
    }
    
    /// <summary>
    /// Refresh Inventory
    /// Update to see the contents inside player inventory
    /// </summary>
    private void LoadInventoryTest()
    {
        CatalogManager.Instance.LoadPlayerInventory();
    }
}
