using UnityEngine;

namespace CosmicShore.Game
{
    public abstract class VesselProjectileEffectSO : ImpactEffectSO
    {
        [SerializeField]
        protected VesselClassType[] vesselTypesToImpact;
        
        public abstract void Execute(VesselImpactor impactor, ProjectileImpactor impactee);
    }
}