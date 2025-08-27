using System;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(Projectile))]
    public class ProjectileImpactor : ImpactorBase
    {
        // [SerializeField, RequireInterface(typeof(IImpactEffect))]
        // ScriptableObject[] projectileShipEffectsSO;
        
        ProjectileShipEffectSO[] projectileShipEffects;
        ProjectilePrismEffectSO[]  projectilePrismEffects; 
        ProjectileMineEffectSO[] projectileMineEffects;
        
        ProjectileEndEffectSO[] projectileEndEffects;

        public Projectile Projectile;
        

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
                case ShipImpactor shipImpactee:
                    if (Projectile.DisallowImpactOnVessel(shipImpactee.Ship.ShipStatus.Team))
                        break;
                    // ExecuteEffect(impactee, projectileShipEffects);
                    if(!DoesEffectExist(projectileShipEffects)) return;
                    foreach (var effect in projectileShipEffects)
                    {
                        effect.Execute(this, shipImpactee);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    if (Projectile.DisallowImpactOnPrism(prismImpactee.Prism.Team))
                        break;
                    // ExecuteEffect(impactee, projectilePrismEffects);
                    if(!DoesEffectExist(projectilePrismEffects)) return;
                    foreach (var effect in projectilePrismEffects)
                    {
                        effect.Execute(this, prismImpactee);
                    }
                    break;
            }
        }

        private void Reset()
        { 
            Projectile ??= GetComponent<Projectile>();
        }
    }
}