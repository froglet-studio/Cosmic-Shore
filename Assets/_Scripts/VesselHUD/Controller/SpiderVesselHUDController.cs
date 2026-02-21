using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class SpiderVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SpiderVesselHUDView view;

        private IVesselStatus _vesselStatus;
        private SwingingVesselTransformer _swinger;

        private bool IsHudAllowed =>
            _vesselStatus is { IsInitializedAsAI: false, IsLocalUser: true };

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as SpiderVesselHUDView;

            if (vesselStatus?.VesselTransformer is SwingingVesselTransformer swinger)
                _swinger = swinger;

            view?.Initialize();
        }

        void Update()
        {
            if (!IsHudAllowed || !view || _swinger == null) return;

            view.SetSwinging(_swinger.IsSwinging);
        }
    }
}
