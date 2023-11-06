using System;

namespace _Scripts._Core.Playfab_Models.Economy
{
	[Serializable]
    public class ItemPriceModel
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