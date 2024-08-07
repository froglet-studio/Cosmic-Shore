using CosmicShore.Integrations.PlayFab.Economy;
using TMPro;
using UnityEngine;

namespace CosmicShore
{
    public class PurchaseGameplayTicketCard : PurchaseCard
    {
        [SerializeField] TMP_Text PriceLabel;
        VirtualItem virtualItem;

        void Start()
        {
            Debug.Log($"Start PurchaseGameplayTicketCard:{name}");
        }

        public override void InitializeView()
        {
            base.InitializeView();
        
            
        }

        public void SetVirtualItem(VirtualItem virtualItem)
        {
            Debug.Log($"SetVirtualItem - {virtualItem.Name},{virtualItem.Type},{virtualItem.ContentType}");

            this.virtualItem = virtualItem;
            PriceLabel.text = virtualItem.Price[0].Amount.ToString();
        }


        public override void Purchase()
        {
            Debug.Log($"PurchaseFactionTicketCard.Purchase");
            CatalogManager.Instance.PurchaseItem(virtualItem, virtualItem.Price[0], 5);
        }
    }
}