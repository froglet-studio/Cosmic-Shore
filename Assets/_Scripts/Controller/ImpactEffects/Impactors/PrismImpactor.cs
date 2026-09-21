using System;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(Prism))]
    public class PrismImpactor : ImpactorBase
    {
        // NOTE: none of these four arrays is [SerializeField] and nothing assigns them, so
        // every branch below is unreachable today (DoesEffectExist sees null and returns).
        // They are kept guarded rather than deleted so that wiring one later inherits the
        // empty-slot report and the per-effect isolation the sibling impactors already have.
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
                    // ExecuteEffect(impactee, vesselPrismEffects);
                    if(!DoesEffectExist(vesselPrismEffects)) return;
                    for (int e = 0; e < vesselPrismEffects.Length; e++)
                    {
                        if (IsEffectSlotEmpty(vesselPrismEffects[e], this, nameof(vesselPrismEffects), e)) continue;
                        var ef = vesselPrismEffects[e];
                        RunEffectIsolated(() => ef.Execute(shipImpactee,this), ef);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    // ExecuteEffect(impactee, projectilePrismEffects);
                    if(!DoesEffectExist(projectilePrismEffects)) return;
                    for (int e = 0; e < projectilePrismEffects.Length; e++)
                    {
                        if (IsEffectSlotEmpty(projectilePrismEffects[e], this, nameof(projectilePrismEffects), e)) continue;
                        var ef = projectilePrismEffects[e];
                        RunEffectIsolated(() => ef.Execute(projectileImpactee,this), ef);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
                    // ExecuteEffect(impactee, skimmerPrismEffects);
                    if(!DoesEffectExist(skimmerPrismEffects)) return;
                    for (int e = 0; e < skimmerPrismEffects.Length; e++)
                    {
                        if (IsEffectSlotEmpty(skimmerPrismEffects[e], this, nameof(skimmerPrismEffects), e)) continue;
                        var ef = skimmerPrismEffects[e];
                        RunEffectIsolated(() => ef.Execute(skimmerImpactee,this), ef);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    // ExecuteEffect(impactee, explosionPrismEffects);
                    if(!DoesEffectExist(explosionPrismEffects)) return;
                    for (int e = 0; e < explosionPrismEffects.Length; e++)
                    {
                        if (IsEffectSlotEmpty(explosionPrismEffects[e], this, nameof(explosionPrismEffects), e)) continue;
                        var ef = explosionPrismEffects[e];
                        RunEffectIsolated(() => ef.Execute(explosionImpactee,this), ef);
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