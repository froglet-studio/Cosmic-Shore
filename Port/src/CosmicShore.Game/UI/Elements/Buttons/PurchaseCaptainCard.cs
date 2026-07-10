// Ported from Assets/_Scripts/UI/Elements/Buttons/PurchaseCaptainCard.cs (Store unit
// 2026-07-10) — verbatim; Reflex.Attributes → CosmicShore.Engine.Injection.
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Engine.Injection;

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
