using System;

namespace CosmicShore.Integrations.PlayFab.Economy
{
	[Serializable]
    public class ItemPrice
    {
        /// <summary>
        /// The Item Id of the price.
        /// </summary>
        public string ItemId;
        /// <summary>
        /// The amount of the price.
        /// </summary>
        public int Amount;
        
    }
}