using System;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    [Serializable]
    public struct BoostChangedPayload
    {
        public float BoostMultiplier;
        public float MaxMultiplier;
        public Domains SourceDomain;
    }
}
