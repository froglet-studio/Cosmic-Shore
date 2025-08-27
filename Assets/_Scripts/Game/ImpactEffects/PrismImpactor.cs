using System;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(TrailBlock))]
    public class PrismImpactor : ImpactorBase
    {
        // [SerializeField, RequireInterface(typeof(IImpactEffect))]
        // ScriptableObject[] prismShipEffectsSO;
        
        PrismShipEffectsSO[] prismShipEffects;
        
        PrismProjectileEffectsSO[] prismProjectileEffects;
        
        PrismSkimmerEffectsSO[] prismSkimmerEffects;
        
        PrismExplosionEffectsSO[] prismExplosionEffects;
        
       
        
        public TrailBlock Prism;

        void Awake()
        {
            Prism ??= GetComponent<TrailBlock>();
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {    
            switch (impactee)
            {
                case ShipImpactor shipImpactee:
                    // ExecuteEffect(impactee, prismShipEffects);
                    if(!DoesEffectExist(prismShipEffects)) return;
                    foreach (var effect in prismShipEffects)
                    {
                        effect.Execute(this, shipImpactee);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    // ExecuteEffect(impactee, prismProjectileEffects);
                    if(!DoesEffectExist(prismProjectileEffects)) return;
                    foreach (var effect in prismProjectileEffects)
                    {
                        effect.Execute(this, projectileImpactee);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
                    // ExecuteEffect(impactee, prismSkimmerEffects);
                    if(!DoesEffectExist(prismSkimmerEffects)) return;
                    foreach (var effect in prismSkimmerEffects)
                    {
                        effect.Execute(this, skimmerImpactee);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    // ExecuteEffect(impactee, prismExplosionEffects);
                    if(!DoesEffectExist(prismExplosionEffects)) return;
                    foreach (var effect in prismExplosionEffects)
                    {
                        effect.Execute(this, explosionImpactee);
                    }
                    break;
            }
        }

        private void Reset()
        {
            Prism ??= GetComponent<TrailBlock>();
        }
    }
}