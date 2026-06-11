using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public abstract class VesselProjectileEffectSO : ImpactEffectSO
    {
        [SerializeField]
        protected VesselClassType[] vesselTypesToImpact;
        
        public abstract void Execute(VesselImpactor impactor, ProjectileImpactor impactee);
    }
}