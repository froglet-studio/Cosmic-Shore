using System;
using CosmicShore.Models.Enums;

namespace CosmicShore.VesselHUD.View
{
    [Serializable]
    public struct BoostChangedPayload
    {
        public float BoostMultiplier;
        public float MaxMultiplier;
        public Domains SourceDomain;
    }
}
