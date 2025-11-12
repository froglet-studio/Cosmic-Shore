using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game
{
    public class DolphinVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private DolphinVesselHUDView view;

        [Header("Resource Binding")]
        [SerializeField, Tooltip("Which resource drives the Dolphin charge bar (Energy = 0).")]
        private int energyResourceIndex = 0;

        private ResourceSystem _resources;
        private int _stepsMinusOne;

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            view = view != null ? view : baseView as DolphinVesselHUDView;

            if (view != null && !view.isActiveAndEnabled)
                view.gameObject.SetActive(true);

            if (view == null || view.chargeSteps == null || view.chargeSteps.Count == 0)
                return;

            _stepsMinusOne = Mathf.Max(0, view.chargeSteps.Count - 1);
            _resources     = vesselStatus?.ResourceSystem;

            if (_resources == null) return;

            // subscribe
            _resources.OnResourceChanged += HandleResourceChanged;

            // initial sync from current values
            if (energyResourceIndex >= 0 && energyResourceIndex < _resources.Resources.Count)
            {
                var r = _resources.Resources[energyResourceIndex];
                SetFromAmounts(r.CurrentAmount, r.MaxAmount);
            }
            else
            {
                SetSpriteIndex(0);
            }
        }

        void OnDisable()
        {
            if (_resources != null)
                _resources.OnResourceChanged -= HandleResourceChanged;
        }

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != energyResourceIndex) return;
            SetFromAmounts(current, max);
        }

        void SetFromAmounts(float current, float max)
        {
            if (view == null || view.chargeSteps == null || view.chargeSteps.Count == 0) return;

            float norm = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
            int idx = Mathf.Clamp(Mathf.RoundToInt(norm * _stepsMinusOne), 0, _stepsMinusOne);
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
                view.chargeBoostImage.sprite  = sprite;
            }
        }
    }
}
