using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class SquirrelVesselHUDController : VesselHUDController
    {
        [Header("View (optional override)")]
        [SerializeField] private SquirrelVesselHUDView view;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (!view)
                view = View as SquirrelVesselHUDView;
            
        }
    }
}