using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MineImpactor : ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] mineShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] mineProjectileEffectsSO;
        
        IImpactEffect[] mineShipEffects;
        IImpactEffect[] mineProjectileEffects;
        
        protected virtual void Awake()
        {
            mineShipEffects = Array.ConvertAll(mineShipEffectsSO, so => so as IImpactEffect);
            mineProjectileEffects = Array.ConvertAll(mineProjectileEffectsSO, so => so as IImpactEffect);
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, mineShipEffects);
                    break;
                case ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, mineProjectileEffects);
                    break;
            }
        }
    }
}