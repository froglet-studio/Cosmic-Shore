using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "DomainCheckProjectilePrismHitEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/DomainCheckProjectilePrismHitEffectSO")]
    public class DomainCheckProjectilePrismHitEffectSO : ProjectilePrismEffectSO
    {
        [Header("Behavior")]
        [SerializeField]
        [Tooltip("If true, projectiles will pass through prisms with the same domain.")]
        private bool allowFriendlyPassThrough = true;

        [SerializeField]
        [Tooltip("If true, also explode/damage the shooter's prism when hitting an enemy prism.")]
        private bool destroyShooterPrism = true;

        [Header("Factories")]
        [SerializeField]
        private BlockProjectileFactory blockFactory;

        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            if (impactor == null || prismImpactee == null || prismImpactee.Prism == null)
                return;

            var targetPrism = prismImpactee.Prism;
            var projectile  = impactor.Projectile;

            var shooterPrism = projectile.GetComponentInParent<Prism>();
            if (!shooterPrism)
            {
                impactor.ExecuteEndEffects();
                return;
            }

            var shooterDomain = shooterPrism.Domain;
            var targetDomain  = targetPrism.Domain;

            var shooterName = shooterPrism.PlayerName;
            if (string.IsNullOrEmpty(shooterName))
                shooterName = shooterPrism.ownerID;

            if (allowFriendlyPassThrough && Equals(shooterDomain, targetDomain))
                return;

            var impactVector = projectile.Velocity * shooterPrism.Volume;

            targetPrism.Damage(
                impactVector: impactVector,
                domain: shooterDomain,
                playerName: shooterName,
                devastate: false);

            if (destroyShooterPrism && shooterPrism && !Equals(shooterPrism, targetPrism))
            {
                shooterPrism.Damage(
                    impactVector: impactVector,
                    domain: shooterDomain,
                    playerName: shooterName, 
                    devastate: false);

                if (blockFactory != null)
                {
                    blockFactory.ReturnBlock(shooterPrism);
                }
            }

            impactor.ExecuteEndEffects();
        }
    }
}
