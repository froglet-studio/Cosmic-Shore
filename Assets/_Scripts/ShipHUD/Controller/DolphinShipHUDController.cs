using UnityEngine;

namespace CosmicShore.Game
{
    public class DolphinShipHUDController : ShipHUDController
    {
        [Header("View")]
        [SerializeField] private DolphinShipHUDView view;

        [Header("Executor (runtime)")]
        [SerializeField] private ChargeBoostActionExecutor chargeBoostExecutor;

        [SerializeField] private ChargeBoostActionSO chargeBoostActionSO;

        private float _maxUnits = 1f;
        private int   _stepsMinusOne;

        public override void Initialize(IShipStatus shipStatus, ShipHUDView baseView)
        {
            base.Initialize(shipStatus, baseView);
            view = view != null ? view : baseView as DolphinShipHUDView;

            if (view != null && !view.isActiveAndEnabled)
                view.gameObject.SetActive(true);

            if (view == null || view.chargeSteps == null || view.chargeSteps.Count == 0)
                return;

            _stepsMinusOne = Mathf.Max(0, view.chargeSteps.Count - 1);

            if (chargeBoostExecutor != null)
            {
                _maxUnits = chargeBoostActionSO != null ? Mathf.Max(0.0001f, chargeBoostActionSO.MaxNormalizedCharge) : 1f;

                // Subscribe to executor events
                chargeBoostExecutor.OnChargeStarted     += SetFromUnits;
                chargeBoostExecutor.OnChargeProgress    += SetFromUnits;
                chargeBoostExecutor.OnChargeEnded       += () => SetSpriteIndex(_stepsMinusOne);

                chargeBoostExecutor.OnDischargeStarted  += SetFromUnits;
                chargeBoostExecutor.OnDischargeProgress += u => SetFromUnits(u);
                chargeBoostExecutor.OnDischargeEnded    += () => SetSpriteIndex(0);
            }

            // start empty
            SetSpriteIndex(0);
        }

        private void OnDestroy()
        {
            if (chargeBoostExecutor == null) return;

            chargeBoostExecutor.OnChargeStarted     -= SetFromUnits;
            chargeBoostExecutor.OnChargeProgress    -= SetFromUnits;
            chargeBoostExecutor.OnChargeEnded       -= () => SetSpriteIndex(_stepsMinusOne);

            chargeBoostExecutor.OnDischargeStarted  -= SetFromUnits;
            chargeBoostExecutor.OnDischargeProgress -= u => SetFromUnits(u);
            chargeBoostExecutor.OnDischargeEnded    -= () => SetSpriteIndex(0);
        }

        void SetFromUnits(float units)
        {
            if (view == null || view.chargeSteps == null || view.chargeSteps.Count == 0) return;

            float u = Mathf.Clamp(units, 0f, _maxUnits);
            float t = _maxUnits > 0f ? u / _maxUnits : 0f;

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
