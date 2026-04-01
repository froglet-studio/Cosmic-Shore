using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    public class FalconVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private FalconHUDView view;

        [Header("Resource Binding")]
        [SerializeField, Tooltip("Which resource index drives the boost bar.")]
        private int boostResourceIndex;

        ResourceSystem _resources;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view)
                view = View as FalconHUDView;

            _resources = vesselStatus?.ResourceSystem;
            if (_resources == null || view == null)
                return;

            _resources.OnResourceChanged += HandleResourceChanged;

            if ((uint)boostResourceIndex < _resources.Resources.Count)
            {
                var r = _resources.Resources[boostResourceIndex];
                view.SetBoostNormalized(r.MaxAmount > 0f ? r.CurrentAmount / r.MaxAmount : 0f);
            }
        }

        void OnDisable()
        {
            if (_resources != null)
                _resources.OnResourceChanged -= HandleResourceChanged;
        }

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != boostResourceIndex) return;
            if (!view) return;
            view.SetBoostNormalized(max > 0f ? Mathf.Clamp01(current / max) : 0f);
        }
    }
}
