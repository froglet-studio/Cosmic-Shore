using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SerpentShipHUDView : ShipHUDView
    {
        [Header("SEED WALL")] 
        public Sprite[] shieldIconsByCount;
        public Image    shieldIcon;

        [Header("BOOST")]
        public Image boostPip1;
        public Image boostPip2;
        public Image boostPip3;
        public Image boostPip4;

        public Image[] BoostPips => new[] { boostPip1, boostPip2, boostPip3, boostPip4 };
    }
}