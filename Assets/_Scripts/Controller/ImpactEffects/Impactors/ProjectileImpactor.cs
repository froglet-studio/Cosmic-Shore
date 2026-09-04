using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(Projectile))]
    public class ProjectileImpactor : ImpactorBase
    {
        [SerializeField] private ProjectileImpactorDataContainerSO projectileImpactorDataContainer;

        public Projectile Projectile { get; private set; }
        public override Domains OwnDomain => Projectile.OwnDomain;


        private void Awake()
        { 
            Projectile ??= GetComponent<Projectile>();
            
        }

        public void ExecuteEndEffects()
        {
            if (projectileImpactorDataContainer.ProjectileEndEffects.Length <= 0)
                return;
            
            foreach (var effect in projectileImpactorDataContainer.ProjectileEndEffects)
            {
                var e = effect; // capture per iteration - the lambda outlives the loop variable
                RunEffectIsolated(() => e.Execute(this, this), e);   // impactee = self: end effects have none
            }
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {    
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    if (Projectile.DisallowImpactOnVessel(shipImpactee.Vessel.VesselStatus.Domain))
                        break;
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectileShipEffects)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectileShipEffects)
                    {
                        var e = effect;
                        RunEffectIsolated(() => e.Execute(shipImpactee, this), e);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    // When this projectile sweeps, the swept segment query OWNS prism
                    // contact and the PhysX trigger is suppressed for it. The trigger is
                    // not a second chance — it samples one point per physics step, so at
                    // these muzzle speeds it sees a few percent of the path — and letting
                    // both run would double-dispatch every prism the sweep already found.
                    if (Projectile.UsesSweptPrismDetection && !IsSweepDispatch)
                        break;
                    if (Projectile.DisallowImpactOnPrism(prismImpactee.Prism))
                        break;
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectilePrismEffects)) return;
                    // Isolated per effect: the Urchin spike's list is [Embed, Steal, ChainFire]
                    // and the order is load-bearing, so one throwing effect must name itself
                    // loudly and let the rest of the chain run rather than silently killing it.
                    foreach (var effect in projectileImpactorDataContainer.ProjectilePrismEffects)
                    {
                        var e = effect;
                        RunEffectIsolated(() => e.Execute(this, prismImpactee), e);
                    }

                    // SPACE < 5 default: the bullet is destroyed on its first prism impact.
                    // The level-5 'Piercing Bullets' upgrade clears the per-shot flag at fire
                    // time (restoring pierce-through). Detonating projectiles leave the flag
                    // false — their detonator owns the pool return.
                    if (Projectile.StopOnFirstPrismImpact)
                    {
                        // Death point #2. Signal BEFORE the return so a host that leaves
                        // something behind (the Sparrow turret prism's anchor) sees the
                        // impact position — this is the other half of "wherever the bullet
                        // would be destroyed".
                        Projectile.RaiseFlightEnded(stoppedByImpact: true);
                        Projectile.ReturnToFactory();
                    }
                    break;
                case MineImpactor mineImpactee:
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectileMineEffect)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectileMineEffect)
                    {
                        var e = effect;
                        RunEffectIsolated(() => e.Execute(this, mineImpactee), e);
                    }
                    break;
            }
        }

        private void OnValidate()
        { 
            Projectile ??= GetComponent<Projectile>();
        }
    }
}