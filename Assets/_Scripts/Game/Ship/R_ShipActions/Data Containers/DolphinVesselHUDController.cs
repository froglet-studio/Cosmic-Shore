using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class DolphinVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private DolphinVesselHUDView view;

        [Header("Resource Binding")]
        [SerializeField, Tooltip("Which resource drives the Dolphin charge bar (Energy = 0).")]
        private int energyResourceIndex = 0;

        ResourceSystem _resources;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view)
                view = View as DolphinVesselHUDView;

            _resources = vesselStatus?.ResourceSystem;
            if (_resources == null || view == null)
                return;

            _resources.OnResourceChanged += HandleResourceChanged;

            if ((uint)energyResourceIndex < _resources.Resources.Count)
            {
                var r = _resources.Resources[energyResourceIndex];
                SetFromAmounts(r.CurrentAmount, r.MaxAmount);
            }
            else
            {
                view.SetChargeStepIndex(0);
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
            if (!view) return;

            var norm = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            view.SetChargeNormalized(norm);
        }
    }
}
