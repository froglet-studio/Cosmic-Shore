using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent((typeof(Mine)))]
    public class MineImpactor : ImpactorBase
    {
        public Mine Mine;
        
        VesselMineEffectSO[] mineShipEffects;
        ExplosionMineEffectSO[] mineExplosionEffects;
        ProjectileMineEffectSO[] mineProjectileEffects;
        
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
                case VesselImpactor shipImpactee:
                    // ExecuteEffect(impactee, mineShipEffects);
                    if(!DoesEffectExist(mineShipEffects)) return;
                    foreach (var effect in mineShipEffects)
                    {
                        effect.Execute(shipImpactee, this);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    // ExecuteEffect(impactee, mineProjectileEffects);
                    if(!DoesEffectExist(mineProjectileEffects)) return;
                    foreach (var effect in mineProjectileEffects)
                    {
                        effect.Execute(projectileImpactee, this);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    // ExecuteEffect(impactee, mineExplosionEffects);
                    if(!DoesEffectExist(mineExplosionEffects)) return;
                    foreach (var effect in mineExplosionEffects)
                    {
                        effect.Execute(explosionImpactee, this);
                    }
                    break;
            }
        }
    }
}