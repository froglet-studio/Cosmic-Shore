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

        /// <summary>
        /// Self-side shell narrowphase: when THIS prism has an engaged shield,
        /// a contact entering its enlarged broadphase box must reach the
        /// analytic shell (within this impactor's margin threshold) before the
        /// impact dispatches. See Docs/CollisionLOD/DESIGN.md §3.6.
        /// </summary>
        protected override bool PassesOwnShieldNarrowphase(Collider other)
        {
            var gate = Prism != null ? Prism.ActiveShieldGate : null;
            if (gate == null)
                return true;

            // Same shape-aware test as the impactee side, but the toucher is
            // `other` and the shell is THIS prism's — so a hull grazing our
            // octahedron/stella tips is caught, not just a centre-facing point.
            return ColliderReachesShell(other, gate, transform.position,
                ShieldMarginThreshold, other.transform.position);
        }

        // True only while this prism's shell is engaged — lets ImpactorBase drop a
        // self-side parked contact if the shell pops/withdraws before it dispatches.
        protected override bool HasOwnShieldGate => Prism != null && Prism.ActiveShieldGate != null;


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
            Prism ??= GetComponent<Prism>();
        }
    }
}