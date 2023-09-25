using UnityEngine;
using UnityEngine.UI;

public class CatalogView : MonoBehaviour
{
    [SerializeField] private Button purchaseVesselButton;
    
    // Start is called before the first frame update
    void Start()
    {
        purchaseVesselButton.onClick.AddListener(PurchaseItemTest);
    }
    
    /// <summary>
    /// Purchase Item Test
    /// Buy a test vessel using shards, the amount of shards should be the exact price tag on the test vessel shards
    /// </summary>
    private void PurchaseItemTest()
    {
        string vesselId = "aaf7670c-96a2-46d8-9489-0d971c6dc742";
        string shardId = "88be4041-cc48-4231-8595-d440b371d015";
        CatalogManager.Instance.PurchaseItem(vesselId, shardId, 1, 5);
    }
}
