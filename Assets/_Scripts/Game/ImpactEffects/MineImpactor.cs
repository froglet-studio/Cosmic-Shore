using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent((typeof(Mine)))]
    public class MineImpactor : ImpactorBase
    {
        public Mine Mine;
        
        MineShipEffectSO[] mineShipEffects;
        MineExplosionEffectSO[] mineExplosionEffects;
        MineProjectileEffectSO[] mineProjectileEffects;
        
        protected virtual void Awake()
        {
            Mine ??= GetComponent<Mine>();
        }
        
        private void Reset()
        {
            Mine ??= GetComponent<Mine>();
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactee:
                    // ExecuteEffect(impactee, mineShipEffects);
                    if(!DoesEffectExist(mineShipEffects)) return;
                    foreach (var effect in mineShipEffects)
                    {
                        effect.Execute(this, shipImpactee);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    // ExecuteEffect(impactee, mineProjectileEffects);
                    if(!DoesEffectExist(mineProjectileEffects)) return;
                    foreach (var effect in mineProjectileEffects)
                    {
                        effect.Execute(this, projectileImpactee);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    // ExecuteEffect(impactee, mineExplosionEffects);
                    if(!DoesEffectExist(mineExplosionEffects)) return;
                    foreach (var effect in mineExplosionEffects)
                    {
                        effect.Execute(this, explosionImpactee);
                    }
                    break;
            }
        }
    }
}