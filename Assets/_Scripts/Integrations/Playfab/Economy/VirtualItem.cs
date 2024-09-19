using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.PlayFab.Economy
{
    [Serializable]
    public class VirtualItem
    {
        /// <summary>
        /// The unique ID of the item.
        /// </summary>
        public string ItemId;
        
        /// <summary>
        /// The name of the item
        /// </summary>
        public string Name;
        
        /// <summary>
        /// NEUTRAL local description. Descriptions have a 10000 character limit per country code.
        /// </summary>
        public string Description;
        
        /// <summary>
        /// The client-defined type of the item.
        /// </summary>
        public string ContentType;

        /// <summary>
        /// The images associated with this item. Images can be thumbnails or screenshots. Up to 100 images can be added to an item.
        /// Only .png, .jpg, .gif, and .bmp file types can be uploaded
        /// </summary>
        // public List<Image> Images;
        
        /// <summary>
        /// Economy categories in PlayFab, such as Items, Currency, UGC, etc.
        /// </summary>
        public string Categories;
        
        /// <summary>
        /// The prices the item can be purchased for. One item can have multiple price tags (purchasing conditions)
        /// </summary>
        public List<ItemPrice> Price;
        
        /// <summary>
        /// The list of tags that are associated with this item. Up to 32 tags can be added to an item.
        /// </summary>
        public List<string> Tags;
        
        /// <summary>
        /// The high-level type of the item. The following item types are supported: bundle, catalogItem, currency, store, ugc,
        /// subscription.
        /// </summary>
        public string Type;
        
        // This member goes to Inventory Item, 
        public int Amount;
    }
}