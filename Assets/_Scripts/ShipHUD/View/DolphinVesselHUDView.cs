using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class DolphinVesselHUDView : VesselHUDView
    {
        [Header("Charge steps (ordered)")]
        public List<Sprite> chargeSteps = new();

        public Image chargeBoostImage;
    }
}