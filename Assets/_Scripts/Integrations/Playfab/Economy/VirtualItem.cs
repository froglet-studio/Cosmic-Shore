using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace CosmicShore.Integrations.Playfab.Economy
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
        /// The item references associated with this item. For example, the items in a Bundle/Store/Subscription. Every item can
        /// have up to 50 item references.
        /// </summary>
        /// // TODO: reconsider including this in VirtualItem
        /// // TODO: could use an item id list instead of the items?
        public List<VirtualItem> BundleContents;    
        /// <summary>
        /// The prices the item can be purchased for.
        /// </summary>
        [FormerlySerializedAs("priceModel")] public ItemPrice price;
        /// <summary>
        /// The list of tags that are associated with this item. Up to 32 tags can be added to an item.
        /// </summary>
        public List<string> Tags;
        /// <summary>
        /// The high-level type of the item. The following item types are supported: bundle, catalogItem, currency, store, ugc,
        /// subscription.
        /// </summary>
        public string Type;
        public int Amount;
    }
}