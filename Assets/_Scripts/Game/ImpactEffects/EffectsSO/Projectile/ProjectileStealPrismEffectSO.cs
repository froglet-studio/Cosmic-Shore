using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ProjectileStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile/ProjectileStealPrismEffectSO")]
    public class ProjectileStealPrismEffectSO : ProjectilePrismEffectSO
    {
        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Projectile.ShipStatus;
            Steal(prismImpactee, status);
        }
    }
}