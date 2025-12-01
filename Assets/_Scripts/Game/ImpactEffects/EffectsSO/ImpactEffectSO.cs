using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class ImpactEffectSO : ScriptableObject
    {
        /// <summary>
        /// Check whether this effect is allowed for the guest vessel type.
        /// </summary>
        /// <param name="guestVesselType">The vessel type of the vessel we going to impact</param>
        /// <param name="allowedTypes">The allowed vessel types to be mentioned in the effect so</param>
        /// <returns></returns>
        protected bool IsVesselAllowedToImpact(VesselClassType guestVesselType, in VesselClassType[] allowedTypes) => 
            allowedTypes.Length == 0 || allowedTypes.Any(v => v == guestVesselType);
    }
}