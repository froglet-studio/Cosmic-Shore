using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    public class BrittleStarHUDController : VesselHUDController
    {
        [Header("View binding")]
        [SerializeField] private BrittleStarHUDView view;

        [Header("Executors")]
        [SerializeField] private BrittleStarBoostActionExecutor boostExecutor;

        private IVesselStatus _vesselStatus;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as BrittleStarHUDView;

            if (!view) return;

            view.Initialize();
        }

        void Update()
        {
            if (!view || _vesselStatus == null) return;

            view.SetBoostState(
                _vesselStatus.IsBoosting ? 1f : 0f,
                _vesselStatus.IsBoosting);
        }
    }
}
