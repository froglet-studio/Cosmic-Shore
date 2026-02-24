using UnityEngine;

namespace CosmicShore.Game
{
    public class UrchinVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private UrchinVesselHUDView view;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view)
                view = View as UrchinVesselHUDView;
        }
    }
}
