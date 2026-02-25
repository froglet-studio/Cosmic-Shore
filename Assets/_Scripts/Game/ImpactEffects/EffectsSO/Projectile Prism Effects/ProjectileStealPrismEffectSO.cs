using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.ProjectilePrismEffects
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
