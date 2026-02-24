using UnityEngine;

namespace CosmicShore.Game
{
    public class GrizzlyVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private GrizzlyVesselHUDView view;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view)
                view = View as GrizzlyVesselHUDView;
        }
    }
}
