using System;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(Prism))]
    public class PrismImpactor : ImpactorBase
    {
        VesselPrismEffectSO[] vesselPrismEffects;
        
        ProjectilePrismEffectSO[] projectilePrismEffects;
        
        SkimmerPrismEffectSO[] skimmerPrismEffects;
        
        ExplosionPrismEffectSO[] explosionPrismEffects;
       
        
        public Prism Prism;
        public override Domains OwnDomain => Prism.Domain;

        void Awake()
        {
            Prism ??= GetComponent<Prism>();
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    if(!DoesEffectExist(vesselPrismEffects)) return;
                    foreach (var effect in vesselPrismEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(shipImpactee,this);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    if(!DoesEffectExist(projectilePrismEffects)) return;
                    foreach (var effect in projectilePrismEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(projectileImpactee,this);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
                    if(!DoesEffectExist(skimmerPrismEffects)) return;
                    foreach (var effect in skimmerPrismEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(skimmerImpactee,this);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    if(!DoesEffectExist(explosionPrismEffects)) return;
                    foreach (var effect in explosionPrismEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(explosionImpactee,this);
                    }
                    break;
            }
        }

        private void OnValidate()
        {
            Prism ??= GetComponent<Prism>();
        }
    }
}