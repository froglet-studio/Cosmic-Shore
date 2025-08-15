using System;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(TrailBlock))]
    public class R_PrismImpactor : R_ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] prismShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] prismProjectileEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] prismSkimmerEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] prismExplosionEffectsSO;
        
        R_IImpactEffect[] prismShipEffects;
        R_IImpactEffect[] prismProjectileEffects;
        R_IImpactEffect[] prismSkimmerEffects;
        R_IImpactEffect[] prismExplosionEffects;

        private TrailBlock prism;
        public TrailBlock Prism => prism ??= GetComponent<TrailBlock>();

        void Awake()
        {
            prismShipEffects = Array.ConvertAll(prismShipEffectsSO, so => so as R_IImpactEffect);
            prismProjectileEffects = Array.ConvertAll(prismProjectileEffectsSO, so => so as R_IImpactEffect);
            prismSkimmerEffects = Array.ConvertAll(prismSkimmerEffectsSO, so => so as R_IImpactEffect);
            prismExplosionEffects = Array.ConvertAll(prismExplosionEffectsSO, so => so as R_IImpactEffect);
        }
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, prismShipEffects);
                    break;
                case R_ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, prismProjectileEffects);
                    break;
                case R_SkimmerImpactor skimmerImpactor:
                    ExecuteEffect(impactee, prismSkimmerEffects);
                    break;
                case R_ExplosionImpactor explosionImpactor:
                    ExecuteEffect(impactee, prismExplosionEffects);
                    break;
            }
        }
    }
}