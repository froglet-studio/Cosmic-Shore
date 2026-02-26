using CosmicShore.Integrations.Playfab;

namespace CosmicShore.UI.Elements
{
    public class PurchaseCaptainCard : PurchaseItemCard
    {
        SO_Captain captain;

        public override void SetVirtualItem(VirtualItem virtualItem)
        {
            captain = CaptainManager.Instance.GetCaptainSOByName(virtualItem.Name);
            ItemImage.sprite = captain.Image;
            ItemNameLabel.text = captain.Name;
            ItemDescriptionLabel.text = captain.Description;

            base.SetVirtualItem(virtualItem);
        }

        protected override bool PurchaseLimitReached()
        {
            // Check if owned and update UI accordingly
            return CatalogManager.Inventory.ContainsCaptain(captain.Name);
        }
    }
}