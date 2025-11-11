using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
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

            // 1) Apply your damage/destroy prism
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
                    OverrideMaterial    = status.AOEExplosionMaterial
                });
            }
        }
    }
}