using CosmicShore.Gameplay;
using CosmicShore.Core;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    public class PurchaseGameCard : PurchaseItemCard
    {
        SO_ArcadeGame game;

        public override void SetVirtualItem(VirtualItem virtualItem)
        {
            // game = Arcade.Instance.GetArcadeGameSOByName(virtualItem.Name);
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