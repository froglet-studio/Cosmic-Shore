using UnityEngine;

namespace CosmicShore.Game
{
    public class DolphinShipHUDController : R_ShipHUDController
    {
        [Header("View")]
        [SerializeField] private DolphinShipHUDView view;

        [Header("Action")]
        [SerializeField] private ChargeBoostAction chargeBoostAction;

        private float _maxUnits = 1f;
        private int   _stepsMinusOne = 0;

        public override void Initialize(IShipStatus shipStatus, R_ShipHUDView baseView)
        {
            base.Initialize(shipStatus, baseView);
            view = view != null ? view : baseView as DolphinShipHUDView;
            
            if(view != null && !view.isActiveAndEnabled) view.gameObject.SetActive(true);

            _stepsMinusOne = Mathf.Max(0, view.chargeSteps.Count - 1);

            if (chargeBoostAction != null)
            {
                _maxUnits = Mathf.Max(0.0001f, chargeBoostAction.MaxChargeUnits);

                chargeBoostAction.OnChargeStarted      += u => SetFromUnits(u, true);
                chargeBoostAction.OnChargeProgress     += u => SetFromUnits(u, true);
                chargeBoostAction.OnChargeEnded        += () => SetSpriteIndex(_stepsMinusOne); // full

                chargeBoostAction.OnDischargeStarted   += u => SetFromUnits(u, false);
                chargeBoostAction.OnDischargeProgress  += u => SetFromUnits(u, false);
                chargeBoostAction.OnDischargeEnded     += () => SetSpriteIndex(0);              // empty
            }

            // start empty
            SetSpriteIndex(0);
        }

        void SetFromUnits(float units, bool charging)
        {
            if (view == null || view.chargeSteps == null || view.chargeSteps.Count == 0) return;

            float u = Mathf.Clamp(units, 0f, _maxUnits);
            float t = u / _maxUnits; // 0..1

            int n = view.chargeSteps.Count;
            int idx;

            if (charging)
            {
                // ceil to climb confidently toward the end
                idx = Mathf.Clamp(Mathf.CeilToInt(t * n) - 1, 0, n - 1);
            }
            else
            {
                // floor to descend cleanly without skipping zero
                idx = Mathf.Clamp(Mathf.FloorToInt(t * n), 0, n - 1);
            }

            SetSpriteIndex(idx);
        }

        void SetSpriteIndex(int idx)
        {
            if (view == null || view.chargeBoostImage == null) return;
            if (idx < 0 || idx >= view.chargeSteps.Count) return;

            var sprite = view.chargeSteps[idx];
            if (sprite != null)
            {
                view.chargeBoostImage.enabled = true;
                view.chargeBoostImage.sprite = sprite;
            }
        }
    }
}
