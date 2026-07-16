using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
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

        /// <returns>True when at least one end effect ran. Callers that rely on end
        /// effects to return the projectile to its pool must handle a false return.</returns>
        public bool ExecuteEndEffects()
        {
            if (projectileImpactorDataContainer.ProjectileEndEffects.Length <= 0)
                return false;

            foreach (var effect in projectileImpactorDataContainer.ProjectileEndEffects)
                effect.Execute(this, this);     // here we are passing itself as impactee, coz it doesn't have any impactee.

            return true;
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
                        effect.Execute(shipImpactee,this);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    if (Projectile.DisallowImpactOnPrism(prismImpactee.Prism.Domain))
                        break;
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectilePrismEffects)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectilePrismEffects)
                    {
                        effect.Execute(this, prismImpactee);
                    }
                    break;
                case MineImpactor mineImpactee:
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectileMineEffect)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectileMineEffect)
                    {
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