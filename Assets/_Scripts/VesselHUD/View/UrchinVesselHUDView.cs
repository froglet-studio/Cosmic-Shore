using UnityEngine;

namespace CosmicShore.Game
{
    public class UrchinVesselHUDView : VesselHUDView
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
