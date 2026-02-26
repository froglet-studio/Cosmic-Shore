using UnityEngine;

namespace CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes
{
    public abstract class VesselProjectileEffectSO : ImpactEffectSO
    {
        [SerializeField]
        protected VesselClassType[] vesselTypesToImpact;
        
        public abstract void Execute(VesselImpactor impactor, ProjectileImpactor impactee);
    }
}