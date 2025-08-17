using System;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(TrailBlock))]
    public class PrismImpactor : ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] prismShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] prismProjectileEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] prismSkimmerEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] prismExplosionEffectsSO;
        
        IImpactEffect[] prismShipEffects;
        IImpactEffect[] prismProjectileEffects;
        IImpactEffect[] prismSkimmerEffects;
        IImpactEffect[] prismExplosionEffects;
        
        public TrailBlock Prism;

        void Awake()
        {
            Prism ??= GetComponent<TrailBlock>();
            
            prismShipEffects = Array.ConvertAll(prismShipEffectsSO, so => so as IImpactEffect);
            prismProjectileEffects = Array.ConvertAll(prismProjectileEffectsSO, so => so as IImpactEffect);
            prismSkimmerEffects = Array.ConvertAll(prismSkimmerEffectsSO, so => so as IImpactEffect);
            prismExplosionEffects = Array.ConvertAll(prismExplosionEffectsSO, so => so as IImpactEffect);
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {    
            switch (impactee)
            {
                case ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, prismShipEffects);
                    break;
                case ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, prismProjectileEffects);
                    break;
                case SkimmerImpactor skimmerImpactor:
                    ExecuteEffect(impactee, prismSkimmerEffects);
                    break;
                case ExplosionImpactor explosionImpactor:
                    ExecuteEffect(impactee, prismExplosionEffects);
                    break;
            }
        }

        private void Reset()
        {
            Prism ??= GetComponent<TrailBlock>();
        }
    }
}