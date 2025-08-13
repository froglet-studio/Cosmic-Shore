using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileDamagePrismEffect", menuName = "ScriptableObjects/Impact Effects/ProjectileDamagePrismEffectSO")]
    public class ProjectileDamagePrismEffectSO : ImpactEffectSO<R_ProjectileImpactor, R_PrismImpactor>
    {
        [SerializeField]
        float _inertia;
        
        protected override void ExecuteTyped(R_ProjectileImpactor projectileImpactor, R_PrismImpactor prismImpactee)
        {
            var trailBlockProperties = prismImpactee.TrailBlock.TrailBlockProperties;
            var shipStatus = projectileImpactor.Projectile.ShipStatus;
            
            trailBlockProperties.trailBlock.Damage(
                shipStatus.Course * shipStatus.Speed * _inertia, 
                shipStatus.Team, shipStatus.PlayerName);
        }
    }
}