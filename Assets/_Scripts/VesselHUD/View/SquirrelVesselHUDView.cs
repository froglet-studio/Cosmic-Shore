using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public sealed class SquirrelVesselHUDView : VesselHUDView
    {
        [Header("Boost")]
        [SerializeField] private Image boostFill;
        [SerializeField] private Color boostNormalColor = Color.white;
        [SerializeField] private Color boostFullColor = Color.yellow;

        public override void Initialize()
        {
            if (!boostFill) return;
            boostFill.fillAmount = 0f;
            boostFill.color = boostNormalColor;
            boostFill.enabled = false;
        }

        public void SetBoostState(float boost01, bool isBoosted, bool isFull)
        {
            if (!boostFill) return;

            boostFill.enabled = isBoosted;
            boostFill.fillAmount = isBoosted ? Mathf.Clamp01(boost01) : 0f;
            boostFill.color = isFull ? boostFullColor : boostNormalColor;
        }
    }
}