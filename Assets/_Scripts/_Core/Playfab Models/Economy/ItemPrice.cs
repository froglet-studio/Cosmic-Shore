using System;

namespace CosmicShore
{
	[Serializable]
    public class ItemPrice
    {
        /// <summary>
        /// The amount of the price.
        /// </summary>
        public int Amount;
        /// <summary>
        /// The Item Id of the price.
        /// </summary>
        public string ItemId;
    }
}