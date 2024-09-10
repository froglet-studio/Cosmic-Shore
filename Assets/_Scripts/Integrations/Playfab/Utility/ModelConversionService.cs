using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.Economy;
using PlayFab.EconomyModels;
using UnityEngine;
using UnityEngine.Assertions;

namespace CosmicShore.Integrations.Playfab.Utility
{
    public class ModelConversionService
    {
        /// <summary>
        /// Convert PlayFab Price to Cosmic Shore Custom Price model
        /// </summary>
        /// <param name="price"></param>
        /// <returns></returns>
        private static ItemPrice PlayFabToCosmicShorePrice(CatalogPriceOptions price)
        {
            ItemPrice itemPrice = new();
            if (price.Prices.Count >= 1)
            {
                itemPrice.ItemId = price.Prices[0].Amounts[0].ItemId;
                itemPrice.Amount = price.Prices[0].Amounts[0].Amount;
                Assert.IsTrue(price.Prices[0].UnitAmount != null, $"Misconfigured Catalog Item - Item { itemPrice.ItemId } Unit Amount should not be null.");
                itemPrice.UnitAmount = price.Prices[0].UnitAmount ?? 1;
            }
            return itemPrice;
        }
        
        /// <summary>
        /// Convert PlayFab Catalog Item To Cosmic Shore Virtual Item model
        /// </summary>
        /// <param name="catalogItem"></param>
        /// <returns></returns>
        public static VirtualItem ConvertCatalogItemToVirtualItem(CatalogItem catalogItem)
        {
            VirtualItem virtualItem = new();
            virtualItem.ItemId = catalogItem.Id;
            virtualItem.Name = catalogItem.Title["NEUTRAL"];
            virtualItem.Description = catalogItem.Description.GetValueOrDefault("NEUTRAL", "No Description");
            virtualItem.ContentType = catalogItem.ContentType;
            
            virtualItem.Price = new()
            {
                // TODO: Do this in a loop
                PlayFabToCosmicShorePrice(catalogItem.PriceOptions)
            };

            virtualItem.Tags = catalogItem.Tags;
            virtualItem.Type = catalogItem.Type;
            return virtualItem;
        }
        
        /// <summary>
        /// Load Cosmic Shore Virtual Item details for the corresponding PlayFab Inventory Item by looking it up on the store shelf
        /// We load it from the store shelf since PF's inventory API doesn't return all of the expected fields (e.g the item's name, tags, ...)
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static VirtualItem ConvertInventoryItemToVirtualItem(InventoryItem item)
        {
            if (!CatalogManager.StoreShelve.allItems.ContainsKey(item.Id))
            {
                Debug.LogWarning($"Inventory Item no longer in catalog - id:{item.Id}, type:{item.Type}");
                return null;
            }

            var virtualItem = CatalogManager.StoreShelve.allItems[item.Id];
            virtualItem.Amount = (int)item.Amount;

            return virtualItem;
        }
    }
}
