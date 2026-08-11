using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SkyBurstProjectileDamagePrismEffectSO",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Prism/SkyBurstProjectileDamagePrismEffectSO")]
    public class SkyBurstProjectileDamagePrismEffectSO : ProjectilePrismEffectSO
    {
        [Header("Damage")]
        [SerializeField] float inertia = 70f;

        [Header("Detonation")]
        [SerializeField] private bool detonateOnHit = true;
        [SerializeField] private ProjectileDetonatorSO detonator;
        [SerializeField] private AOEExplosion[] aoePrefabs;
        [SerializeField] private float minExplosionScale = 0.75f;
        [SerializeField] private float maxExplosionScale = 2.0f;

        [Tooltip("Wait this long (s) after impact, with projectile stopped at contact, before spawning AOE.")]
        [SerializeField] private float explodeDelay = 0.15f;

        [SerializeField] private float returnDelay = 0.25f;

        public override void Execute(ProjectileImpactor impactor, PrismImpactor prismImpactee)
        {
            if (!impactor || !impactor.Projectile) return;

            var status = impactor.Projectile.VesselStatus;

            // 1) Apply your damage/destroy prism.
            // CHARGE level-5 'Domain-Safe Skybursts': the direct-hit damage spares prisms of the
            // shooter's own domain (per-shot snapshot at fire time). The AOE follows the same
            // snapshot — ProjectileDetonatorSO passes AffectSelfOverride = !SpareOwnDomain, so
            // below the unlock the blast friendly-fires like every other Sparrow shot. This gate
            // lives strictly in the projectile/explosion layer — never in Prism.Damage and never
            // in danger-prism effects (the locked Prism→Vessel danger law is untouched).
            bool sparedByCharge5 = impactor.Projectile.SpareOwnDomain
                                   && prismImpactee.Prism.Domain == status.Domain;
            if (!sparedByCharge5)
                PrismEffectHelper.Damage(status, prismImpactee, inertia, status.Course, status.Speed);

            // 2) Stop at exact contact and detonate after a small delay (like end effect)
            if (detonateOnHit && detonator)
            {
                detonator.Detonate(new ProjectileDetonatorSO.Request
                {
                    Projectile          = impactor.Projectile,
                    Position            = impactor.transform.position,
                    Rotation            = impactor.transform.rotation,
                    FaceExitVelocity    = false,            // keep contact orientation
                    MinScale            = minExplosionScale,
                    MaxScale            = maxExplosionScale,
                    ExplodeDelaySeconds = explodeDelay,     // << wait before AOE
                    ReturnDelay         = returnDelay,
                    StopAtImpact        = true,             // << freeze in place
                    DisableColliderNow  = true,             // avoid re-hitting stuff
                    Prefabs             = aoePrefabs,
                    Anonymous           = false,
                    OverrideMaterial    = status.AOEExplosionMaterial,
                    DIContainer         = impactor.DIContainer
                });
            }
        }
    }
}