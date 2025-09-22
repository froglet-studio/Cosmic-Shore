using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(Projectile))]
    public class ProjectileImpactor : ImpactorBase
    {
        // [SerializeField, RequireInterface(typeof(IImpactEffect))]
        // ScriptableObject[] projectileShipEffectsSO;
        [SerializeField]
        VesselProjectileEffectSO[] projectileShipEffects;
        [SerializeField]
        ProjectilePrismEffectSO[]  projectilePrismEffects; 
        [SerializeField]
        ProjectileMineEffectSO[] projectileMineEffects;
        [SerializeField]
        ProjectileEndEffectSO[] projectileEndEffects;

        [SerializeField] private ProjectileImpactorDataContainerSO projectileImpactorDataContainer;

        public Projectile Projectile { get; private set; }
        

        private void Awake()
        { 
            Projectile ??= GetComponent<Projectile>();
            
        }

        public void ExecuteEndEffects()
        {
            if (projectileEndEffects.Length <= 0)
                return;
            
            foreach (var effect in projectileEndEffects)
                effect.Execute(this, this);     // here we are passing itself as impactee, coz it doesn't have any impactee.
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {    
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    if (Projectile.DisallowImpactOnVessel(shipImpactee.Vessel.VesselStatus.Domain))
                        break;
                    // ExecuteEffect(impactee, projectileShipEffects);
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectileShipEffects)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectileShipEffects)
                    {
                        effect.Execute(shipImpactee,this);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    if (Projectile.DisallowImpactOnPrism(prismImpactee.Prism.Domain))
                        break;
                    // ExecuteEffect(impactee, projectilePrismEffects);
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectilePrismEffects)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectilePrismEffects)
                    {
                        effect.Execute(this, prismImpactee);
                    }
                    break;
                case MineImpactor mineImpactee:
        
                    // ExecuteEffect(impactee, projectilePrismEffects);
                    if(!DoesEffectExist(projectileImpactorDataContainer.ProjectileMineEffect)) return;
                    foreach (var effect in projectileImpactorDataContainer.ProjectileMineEffect)
                    {
                        effect.Execute(this, mineImpactee);
                    }
                    break;
            }
            Projectile.ReturnToFactory();
        }

        private void OnValidate()
        { 
            Projectile ??= GetComponent<Projectile>();
        }
    }
}