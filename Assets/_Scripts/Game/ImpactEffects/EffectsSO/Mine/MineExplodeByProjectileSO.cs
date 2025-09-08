using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "MineExplodeByProjectile", menuName = "ScriptableObjects/Impact Effects/Mine/MineExplodeByProjectileSO")]
    public class MineExplodeByProjectileSO : MineProjectileEffectSO
    {
        public override void Execute(MineImpactor mineImpactor, ProjectileImpactor projectileImpactee)
        {
            var mine = mineImpactor.Mine; 
            if (mine == null) return;
            
            var projectileVelocity = projectileImpactee.Projectile;
            mine.NullifyDelayedExplosion(projectileVelocity.Velocity);
        }
    }
}