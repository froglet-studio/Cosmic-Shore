using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileDamagePrismEffect", menuName = "ScriptableObjects/Impact Effects/ProjectileDamagePrismEffectSO")]
    public class ProjectileDamagePrismEffectSO : ImpactEffectSO<ProjectileImpactor, PrismImpactor>
    {
        [SerializeField]
        float _inertia;
        
        protected override void ExecuteTyped(ProjectileImpactor projectileImpactor, PrismImpactor prismImpactee)
        {
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            var shipStatus = projectileImpactor.Projectile.ShipStatus;
            
            trailBlockProperties.trailBlock.Damage(
                shipStatus.Course * shipStatus.Speed * _inertia, 
                shipStatus.Team, shipStatus.PlayerName);
        }
    }
}