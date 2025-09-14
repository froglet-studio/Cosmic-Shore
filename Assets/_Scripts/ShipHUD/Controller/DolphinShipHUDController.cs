using UnityEngine;

namespace CosmicShore.Game
{
    public class DolphinShipHUDController : ShipHUDController
    {
        [Header("View")]
        [SerializeField] private DolphinShipHUDView view;

        [Header("Action")]
        [SerializeField] private ChargeBoostAction chargeBoostAction;

        private float _maxUnits = 1f;
        private int   _stepsMinusOne = 0;

        public override void Initialize(IShipStatus shipStatus, ShipHUDView baseView)
        {
            base.Initialize(shipStatus, baseView);
            view = view != null ? view : baseView as DolphinShipHUDView;

            if (view != null && !view.isActiveAndEnabled) view.gameObject.SetActive(true);

            if (view == null || view.chargeSteps == null || view.chargeSteps.Count == 0)
                return;

            _stepsMinusOne = Mathf.Max(0, view.chargeSteps.Count - 1);

            if (chargeBoostAction != null)
            {
                _maxUnits = Mathf.Max(0.0001f, chargeBoostAction.MaxChargeUnits);

                chargeBoostAction.OnChargeStarted     += SetFromUnits;
                chargeBoostAction.OnChargeProgress    += SetFromUnits;
                chargeBoostAction.OnChargeEnded       += () => SetSpriteIndex(_stepsMinusOne); 

                chargeBoostAction.OnDischargeStarted  += SetFromUnits;
                chargeBoostAction.OnDischargeProgress += u => SetFromUnits(u);
                chargeBoostAction.OnDischargeEnded    += () => SetSpriteIndex(0);        
            }

            // start empty
            SetSpriteIndex(0);
        }

        void SetFromUnits(float units)
        {
            if (view == null || view.chargeSteps == null || view.chargeSteps.Count == 0) return;

            float u = Mathf.Clamp(units, 0f, _maxUnits);
            float t =  _maxUnits > 0f  ? u / _maxUnits : 0f; 

            int idx = Mathf.Clamp(Mathf.RoundToInt(t * _stepsMinusOne), 0, _stepsMinusOne);
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
