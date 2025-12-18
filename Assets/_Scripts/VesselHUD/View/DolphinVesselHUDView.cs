using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class DolphinVesselHUDView : VesselHUDView
    {
        [Header("Charge steps (ordered low → high)")]
        [SerializeField] private List<Sprite> chargeSteps = new();

        [SerializeField] private Image chargeBoostImage;

        int _stepsMinusOne;

        public void Initialize()
        {
            _stepsMinusOne = Mathf.Max(0, (chargeSteps?.Count ?? 0) - 1);

            if (chargeBoostImage)
                chargeBoostImage.enabled = false;
        }

        /// <summary>
        /// 0–1 normalized charge → choose correct sprite.
        /// </summary>
        public void SetChargeNormalized(float norm01)
        {
            if (!chargeBoostImage || chargeSteps == null || chargeSteps.Count == 0)
                return;

            norm01 = Mathf.Clamp01(norm01);

            int idx = (_stepsMinusOne <= 0)
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt(norm01 * _stepsMinusOne), 0, _stepsMinusOne);

            SetChargeStepIndex(idx);
        }

        public void SetChargeStepIndex(int idx)
        {
            if (!chargeBoostImage || chargeSteps == null || chargeSteps.Count == 0)
                return;
            if (idx < 0 || idx >= chargeSteps.Count) return;

            var sprite = chargeSteps[idx];
            if (!sprite) return;

            chargeBoostImage.enabled = true;
            chargeBoostImage.sprite  = sprite;
        }
    }
}