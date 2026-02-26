using CosmicShore.Game.Ship;
using UnityEngine;

namespace CosmicShore.Gameplay
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