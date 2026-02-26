using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
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
