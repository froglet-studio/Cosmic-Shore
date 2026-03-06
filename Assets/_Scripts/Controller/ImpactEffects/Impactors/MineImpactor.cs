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
        public override Domains OwnDomain => Domains.None;

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
                        if (effect == null) continue;
                        effect.Execute(shipImpactee, this);
                    }
                    break;
                case ProjectileImpactor projectileImpactee:
                    if(!DoesEffectExist(mineProjectileEffects)) return;
                    foreach (var effect in mineProjectileEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(projectileImpactee, this);
                    }
                    break;
                case ExplosionImpactor explosionImpactee:
                    if(!DoesEffectExist(mineExplosionEffects)) return;
                    foreach (var effect in mineExplosionEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(explosionImpactee, this);
                    }
                    break;
            }
        }
    }
}