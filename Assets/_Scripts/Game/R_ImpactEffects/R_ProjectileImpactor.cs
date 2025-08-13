using System;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ProjectileImpactor : R_ImpactorBase
    {
        [SerializeField] Projectile projectile;

        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] projectileShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] projectilePrismEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] projectileFakeCrystalEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] projectileEndEffectsSO;
        
        
        public Projectile Projectile => projectile;

        R_IImpactEffect[] projectileShipEffects;
        R_IImpactEffect[] projectilePrismEffects;
        R_IImpactEffect[] projectileFakeCrystalEffects;
        R_IImpactEffect[] projectileEndEffects;

        private void Awake()
        { 
            projectileShipEffects = Array.ConvertAll(projectileShipEffectsSO, so => so as R_IImpactEffect);
            projectilePrismEffects = Array.ConvertAll(projectilePrismEffectsSO,  so => so as R_IImpactEffect);
            projectileFakeCrystalEffects = Array.ConvertAll(projectileFakeCrystalEffectsSO, so => so as R_IImpactEffect);
            projectileEndEffects = Array.ConvertAll(projectileEndEffectsSO, so  => so as R_IImpactEffect);
        }

        public void ExecuteEndEffects()
        {
            if (projectileEndEffects.Length <= 0)
                return;
            
            foreach (var effect in projectileEndEffects)
                effect.Execute(this, this);     // here we are passing itself as impactee, coz it doesn't have any impactee.
        }
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    if (projectile.DisallowImpactOnVessel(shipImpactor.Ship.ShipStatus.Team))
                        break;
                    ExecuteEffect(impactee, projectileShipEffects);
                    break;
                
                case R_PrismImpactor prismImpactor:
                    if (projectile.DisallowImpactOnPrism(prismImpactor.TrailBlock.Team))
                        break;
                    ExecuteEffect(impactee, projectilePrismEffects);
                    break;
                
                case R_FakeCrystalImpactor fakeCrystalImpactor:
                    ExecuteEffect(impactee, projectileFakeCrystalEffects);
                    break;
            }
        }
    }
}