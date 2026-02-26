using UnityEngine;

namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(
        fileName = "DetonateSparrowProjectileEndEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile/End Effects/DetonateSparrowProjectileEndEffectSO")]
    public class DetonateSparrowProjectileEndEffectSO : ProjectileEndEffectSO
    {
        public override void Execute(ProjectileImpactor impactor, ImpactorBase impactee)
        {
            var projectile = impactor.Projectile;
            projectile.ReturnToFactory();
        }
    }
}