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
                    if(!DoesEffectExist(mineShipEffects)) return;
                    foreach (var effect in mineShipEffects)
                    {
                        effect.Execute(shipImpactee, this);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    if(!DoesEffectExist(mineProjectileEffects)) return;
                    foreach (var effect in mineProjectileEffects)
                    {
                        effect.Execute(projectileImpactee, this);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
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