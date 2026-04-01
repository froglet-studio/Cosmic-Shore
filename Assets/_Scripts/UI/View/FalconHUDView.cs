using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class FalconHUDView : VesselHUDView
    {
        [Header("Boost")]
        [SerializeField] private Image boostFill;

        public override void Initialize()
        {
            if (boostFill)
                boostFill.fillAmount = 0f;
        }

        public void SetBoostNormalized(float norm01)
        {
            if (!boostFill) return;
            boostFill.fillAmount = Mathf.Clamp01(norm01);
        }
    }
}
