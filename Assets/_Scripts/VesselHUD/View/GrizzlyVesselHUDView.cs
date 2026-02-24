using UnityEngine;

namespace CosmicShore.Game
{
    public class GrizzlyVesselHUDView : VesselHUDView
    {
        public override void Initialize()
        {
            foreach (var h in highlights)
            {
                if (h.image)
                    h.image.enabled = false;
            }
        }
    }
}
