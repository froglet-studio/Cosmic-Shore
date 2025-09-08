using System;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(TrailBlock))]
    public class PrismImpactor : ImpactorBase
    {
        ShipPrismEffectsSO[] prismShipEffects;
        
        ProjectilePrismEffectSO[] prismProjectileEffects;
        
        SkimmerPrismEffectSO[] prismSkimmerEffects;
        
        ExplosionPrismEffectSO[] prismExplosionEffects;
       
        
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
                        effect.Execute(shipImpactee,this);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    // ExecuteEffect(impactee, prismProjectileEffects);
                    if(!DoesEffectExist(prismProjectileEffects)) return;
                    foreach (var effect in prismProjectileEffects)
                    {
                        effect.Execute(projectileImpactee,this);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
                    // ExecuteEffect(impactee, prismSkimmerEffects);
                    if(!DoesEffectExist(prismSkimmerEffects)) return;
                    foreach (var effect in prismSkimmerEffects)
                    {
                        effect.Execute(skimmerImpactee,this);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    // ExecuteEffect(impactee, prismExplosionEffects);
                    if(!DoesEffectExist(prismExplosionEffects)) return;
                    foreach (var effect in prismExplosionEffects)
                    {
                        effect.Execute(explosionImpactee,this);
                    }
                    break;
            }
        }

        private void OnValidate()
        {
            Prism ??= GetComponent<TrailBlock>();
        }
    }
}