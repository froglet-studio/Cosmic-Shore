using PlayFab.EconomyModels;
using System.Collections.Generic;
using System;

namespace CosmicShore
{
    public class CatalogItem
    {
        /// <summary>
        /// The client-defined type of the item.
        /// </summary>
        public string ContentType;
        /// <summary>
        /// A dictionary of localized descriptions. Key is language code and localized string is the value. The NEUTRAL locale is
        /// required. Descriptions have a 10000 character limit per country code.
        /// </summary>
        public Dictionary<string, string> Description;
        /// <summary>
        /// The unique ID of the item.
        /// </summary>
        public string Id;
        /// <summary>
        /// The images associated with this item. Images can be thumbnails or screenshots. Up to 100 images can be added to an item.
        /// Only .png, .jpg, .gif, and .bmp file types can be uploaded
        /// </summary>
        public List<Image> Images;
        /// <summary>
        /// The item references associated with this item. For example, the items in a Bundle/Store/Subscription. Every item can
        /// have up to 50 item references.
        /// </summary>
        public List<CatalogItemReference> ItemReferences;

        /// <summary>
        /// The prices the item can be purchased for.
        /// </summary>
        public CatalogPriceOptions PriceOptions;

        /// <summary>
        /// Optional details for stores items.
        /// </summary>
        public StoreDetails StoreDetails;
        /// <summary>
        /// The list of tags that are associated with this item. Up to 32 tags can be added to an item.
        /// </summary>
        public List<string> Tags;
        /// <summary>
        /// A dictionary of localized titles. Key is language code and localized string is the value. The NEUTRAL locale is
        /// required. Titles have a 512 character limit per country code.
        /// </summary>
        public Dictionary<string, string> Title;
        /// <summary>
        /// The high-level type of the item. The following item types are supported: bundle, catalogItem, currency, store, ugc,
        /// subscription.
        /// </summary>
        public string Type;

    }
}