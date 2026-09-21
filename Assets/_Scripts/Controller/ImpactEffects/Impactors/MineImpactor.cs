using System;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    [RequireComponent((typeof(Mine)))]
    public class MineImpactor : ImpactorBase
    {
        public Mine Mine;
        public override Domains OwnDomain => Domains.Blue;

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
                    for (int e = 0; e < mineShipEffects.Length; e++)
                    {
                        if (IsEffectSlotEmpty(mineShipEffects[e], this, nameof(mineShipEffects), e)) continue;
                        var ef = mineShipEffects[e];
                        RunEffectIsolated(() => ef.Execute(shipImpactee, this), ef);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    if(!DoesEffectExist(mineProjectileEffects)) return;
                    for (int e = 0; e < mineProjectileEffects.Length; e++)
                    {
                        if (IsEffectSlotEmpty(mineProjectileEffects[e], this, nameof(mineProjectileEffects), e)) continue;
                        var ef = mineProjectileEffects[e];
                        RunEffectIsolated(() => ef.Execute(projectileImpactee, this), ef);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    if(!DoesEffectExist(mineExplosionEffects)) return;
                    for (int e = 0; e < mineExplosionEffects.Length; e++)
                    {
                        if (IsEffectSlotEmpty(mineExplosionEffects[e], this, nameof(mineExplosionEffects), e)) continue;
                        var ef = mineExplosionEffects[e];
                        RunEffectIsolated(() => ef.Execute(explosionImpactee, this), ef);
                    }
                    break;
            }
        }
    }
}