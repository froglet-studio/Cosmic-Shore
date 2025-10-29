using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SparrowHUDView : VesselHUDView
    {
        [Header("Missiles")]
        public Sprite[] missileIcons;
        public Image missileIcon;

        [Header("Boost")]
        public Image boostFill;

        [Header("Weapon Mode")]
        public Image weaponModeIcon;       
        public Sprite[] weaponModeIcons = new Sprite[2];
    }
}