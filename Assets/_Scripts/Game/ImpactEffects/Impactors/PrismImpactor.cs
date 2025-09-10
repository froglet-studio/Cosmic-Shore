using System;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(TrailBlock))]
    public class PrismImpactor : ImpactorBase
    {
        VesselPrismEffectSO[] vesselPrismEffects;
        
        ProjectilePrismEffectSO[] projectilePrismEffects;
        
        SkimmerPrismEffectSO[] skimmerPrismEffects;
        
        ExplosionPrismEffectSO[] explosionPrismEffects;
       
        
        public TrailBlock Prism;

        void Awake()
        {
            Prism ??= GetComponent<TrailBlock>();
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {    
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    // ExecuteEffect(impactee, vesselPrismEffects);
                    if(!DoesEffectExist(vesselPrismEffects)) return;
                    foreach (var effect in vesselPrismEffects)
                    {
                        effect.Execute(shipImpactee,this);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    // ExecuteEffect(impactee, projectilePrismEffects);
                    if(!DoesEffectExist(projectilePrismEffects)) return;
                    foreach (var effect in projectilePrismEffects)
                    {
                        effect.Execute(projectileImpactee,this);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
                    // ExecuteEffect(impactee, skimmerPrismEffects);
                    if(!DoesEffectExist(skimmerPrismEffects)) return;
                    foreach (var effect in skimmerPrismEffects)
                    {
                        effect.Execute(skimmerImpactee,this);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    // ExecuteEffect(impactee, explosionPrismEffects);
                    if(!DoesEffectExist(explosionPrismEffects)) return;
                    foreach (var effect in explosionPrismEffects)
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