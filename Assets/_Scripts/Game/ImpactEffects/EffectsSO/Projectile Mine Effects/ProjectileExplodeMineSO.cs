using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileExplodeMine", menuName = "ScriptableObjects/Impact Effects/Projectile/ProjectileExplodeMineSO")]
    public class ProjectileExplodeMineSO : ProjectileMineEffectSO
    {
        public override void Execute(ProjectileImpactor impactor, MineImpactor mineImpactee)
        {
            var mine = mineImpactee.Mine; 
            if (mine == null) return;
            
            var projectileVelocity = impactor.Projectile;
            mine.NullifyDelayedExplosion(projectileVelocity.Velocity);
        }
    }
}