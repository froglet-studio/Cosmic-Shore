using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    [Serializable]
    public struct BoostChangedPayload
    {
        public float BoostMultiplier;
        public float MaxMultiplier;
        public Domains SourceDomain;

        /// <summary>
        /// Source vessel this boost change originated from. boostChanged is a single
        /// shared global SOAP channel that EVERY vessel raises (including a remote
        /// owner's per-frame <c>VesselTransformer.DecayBoost</c>), so HUD/audio
        /// consumers must filter to their own vessel or a remote vessel drives the
        /// local owner's HUD. Reference-compared, so it works identically in
        /// networked and single-player (non-networked) modes. Not serialized
        /// (interface ref) - this is a transient runtime payload.
        /// </summary>
        public IVesselStatus VesselStatus;
    }
}
