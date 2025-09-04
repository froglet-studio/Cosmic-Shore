using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SparrowHUDView : ShipHUDView
    {
        [Header("Missiles")]
        public Sprite[] missileIcons;
        public Image missileIcon;

        [Header("Boost")]
        public Image  boostFill;        
    }
}