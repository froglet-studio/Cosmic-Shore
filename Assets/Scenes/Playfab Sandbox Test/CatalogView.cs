using UnityEngine;
using UnityEngine.UI;

public class CatalogView : MonoBehaviour
{
    [SerializeField] private Button purchaseVesselButton;
    
    // Start is called before the first frame update
    void Start()
    {
        purchaseVesselButton.onClick.AddListener(CatalogManager.Instance.PurchaseItemTest);
    }
    
}
