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
    [CreateAssetMenu(fileName = "ProjectileDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/ProjectileDamagePrismEffectSO")]
    public class ProjectileDamagePrismEffectSO : ProjectilePrismEffectSO
    {
        [SerializeField] float inertia = 1f;   // global scalar you can tune per effect
        [SerializeField] private Vector3 overrideCourse;
        [SerializeField] private float overrideSpeed;
        
        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Projectile.VesselStatus;
            PrismEffectHelper.Damage(status, prismImpactee, inertia, impactor.Projectile.Velocity);
        }
    }
}