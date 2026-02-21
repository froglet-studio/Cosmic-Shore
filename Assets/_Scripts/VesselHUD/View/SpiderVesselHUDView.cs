using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SpiderVesselHUDView : VesselHUDView
    {
        [Header("Spider - Swing Indicator")]
        [SerializeField] private Image swingIndicator;
        [SerializeField] private Color swingActiveColor = Color.cyan;
        [SerializeField] private Color swingInactiveColor = Color.white;

        public override void Initialize()
        {
            if (swingIndicator)
            {
                swingIndicator.color = swingInactiveColor;
                swingIndicator.enabled = false;
            }
        }

        public void SetSwinging(bool isSwinging)
        {
            if (!swingIndicator) return;

            swingIndicator.enabled = isSwinging;
            swingIndicator.color = isSwinging ? swingActiveColor : swingInactiveColor;
        }
    }
}
