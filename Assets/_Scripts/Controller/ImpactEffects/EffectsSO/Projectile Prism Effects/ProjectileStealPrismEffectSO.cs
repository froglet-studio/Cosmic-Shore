using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ProjectileStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/ProjectileStealPrismEffectSO")]
    public class ProjectileStealPrismEffectSO : ProjectilePrismEffectSO
    {
        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Projectile.VesselStatus;
            PrismEffectHelper.Steal(prismImpactee, status);
        }
    }
}
