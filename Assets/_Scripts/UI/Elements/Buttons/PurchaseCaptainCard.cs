using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;

namespace CosmicShore.UI
{
    public class PurchaseCaptainCard : PurchaseItemCard
    {
        [Inject] CaptainManager _captainManager;
        SO_Captain captain;

        public override void SetVirtualItem(VirtualItem virtualItem)
        {
            captain = _captainManager.GetCaptainSOByName(virtualItem.Name);
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