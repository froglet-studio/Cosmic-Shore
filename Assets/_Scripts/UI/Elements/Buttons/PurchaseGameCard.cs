using CosmicShore.Core;
using CosmicShore.Integrations.PlayFab.Economy;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore
{
    public class PurchaseGameCard : PurchaseItemCard
    {
        SO_ArcadeGame game;

        public override void SetVirtualItem(VirtualItem virtualItem)
        {
            CSDebug.Log($"SetVirtualItem - Name:{virtualItem.Name}");
            game = Arcade.Instance.GetArcadeGameSOByName(virtualItem.Name);
            CSDebug.Log($"SetVirtualItem - game:{game}");
            ItemImage.sprite = game.CardBackground;
            ItemNameLabel.text = game.DisplayName;
            ItemDescriptionLabel.text = game.Description;

            base.SetVirtualItem(virtualItem);
        }

        protected override bool PurchaseLimitReached()
        {
            // Check if owned and update UI accordingly
            return CatalogManager.Inventory.ContainsGame(game.DisplayName);
        }
    }
}