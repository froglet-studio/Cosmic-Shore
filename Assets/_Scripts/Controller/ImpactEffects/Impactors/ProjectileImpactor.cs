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
            if (projectileImpactorDataContainer == null) return;
            if (projectileImpactorDataContainer.ProjectileEndEffects.Length <= 0)
                return;

            foreach (var effect in projectileImpactorDataContainer.ProjectileEndEffects)
            {
                if (effect == null) continue;
                effect.Execute(this, this);     // here we are passing itself as impactee, coz it doesn't have any impactee.
            }
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (projectileImpactorDataContainer == null) return;

            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    if (Projectile.DisallowImpactOnVessel(shipImpactee.Vessel.VesselStatus.Domain))
                        break;
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectileShipEffects)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectileShipEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(shipImpactee,this);
                    }
                    break;

                case PrismImpactor prismImpactee:
                    if (Projectile.DisallowImpactOnPrism(prismImpactee.Prism.Domain))
                        break;
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectilePrismEffects)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectilePrismEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(this, prismImpactee);
                    }
                    break;
                case MineImpactor mineImpactee:
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectileMineEffect)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectileMineEffect)
                    {
                        if (effect == null) continue;
                        effect.Execute(this, mineImpactee);
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