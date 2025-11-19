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
        [Tooltip("Factory that owns the moving block prisms, used to return the shooter block on hit.")]
        private BlockProjectileFactory blockFactory;

        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            if (impactor == null || prismImpactee == null || prismImpactee.Prism == null)
                return;

            var targetPrism = prismImpactee.Prism;

            var projectile = impactor.Projectile;
            
            var shooterPrism = projectile.GetComponentInParent<Prism>();
            Domains shooterDomain = shooterPrism.Domain;
            Domains targetDomain  = targetPrism.Domain;


            if (allowFriendlyPassThrough && shooterDomain == targetDomain)
                return;

            // Enemy hit: compute impact vector from projectile velocity
            Vector3 impactVector = projectile.Velocity;

            targetPrism.Damage(
                impactVector: impactVector,
                domain: shooterDomain,
                playerName: "PrismProjectile",
                devastate: false);

            if (destroyShooterPrism && shooterPrism != null && shooterPrism != targetPrism)
            {
                shooterPrism.Damage(
                    impactVector: -impactVector,
                    domain: shooterDomain,
                    playerName: "PrismProjectile",
                    devastate: false);


                if (blockFactory != null)
                {
                    blockFactory.ReturnBlock(shooterPrism);
                }
            }

            // End projectile VFX / lifecycle
            impactor.ExecuteEndEffects();
        }
    }
}
