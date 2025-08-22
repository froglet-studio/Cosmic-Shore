using System;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(Projectile))]
    public class ProjectileImpactor : ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] projectileShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] projectilePrismEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] projectileFakeCrystalEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] projectileEndEffectsSO;

        public Projectile Projectile;

        IImpactEffect[] projectileShipEffects;
        IImpactEffect[] projectilePrismEffects;
        IImpactEffect[] projectileFakeCrystalEffects;
        IImpactEffect[] projectileEndEffects;

        private void Awake()
        { 
            Projectile ??= GetComponent<Projectile>();
            
            projectileShipEffects = Array.ConvertAll(projectileShipEffectsSO, so => so as IImpactEffect);
            projectilePrismEffects = Array.ConvertAll(projectilePrismEffectsSO,  so => so as IImpactEffect);
            projectileFakeCrystalEffects = Array.ConvertAll(projectileFakeCrystalEffectsSO, so => so as IImpactEffect);
            projectileEndEffects = Array.ConvertAll(projectileEndEffectsSO, so  => so as IImpactEffect);
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
                case ShipImpactor shipImpactor:
                    if (Projectile.DisallowImpactOnVessel(shipImpactor.Ship.ShipStatus.Team))
                        break;
                    ExecuteEffect(impactee, projectileShipEffects);
                    break;
                
                case PrismImpactor prismImpactor:
                    if (Projectile.DisallowImpactOnPrism(prismImpactor.Prism.Team))
                        break;
                    ExecuteEffect(impactee, projectilePrismEffects);
                    break;
            }
        }

        private void Reset()
        { 
            Projectile ??= GetComponent<Projectile>();
        }
    }
}