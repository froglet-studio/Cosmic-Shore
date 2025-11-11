using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkyBurstProjectileExplodeMine",
        menuName = "ScriptableObjects/Impact Effects/Projectile - Mine/SkyBurstProjectileExplodeMineSO")]
    public class SkyBurstProjectileExplodeMineSO : ProjectileMineEffectSO
    {
        [Header("Mine Reaction")]
        [SerializeField]
        private bool notifyMineWithVelocity = true;

        [Header("Detonation (like end effect)")]
        [SerializeField] private ProjectileDetonatorSO detonator;
        [SerializeField] private AOEExplosion[] aoePrefabs;
        [SerializeField] private float minExplosionScale = 0.75f;
        [SerializeField] private float maxExplosionScale = 2.0f;
        [SerializeField]
        private float explodeDelay = 0.12f;
        [SerializeField] private float returnDelay = 0.25f;

        public override void Execute(ProjectileImpactor impactor, MineImpactor mineImpactee)
        {
            var mine = mineImpactee?.Mine;
            var proj = impactor?.Projectile;
            if (!mine || !proj) return;

            if (notifyMineWithVelocity)
                mine.NullifyDelayedExplosion(proj.Velocity);

            if (!detonator) return;

            detonator.Detonate(new ProjectileDetonatorSO.Request
            {
                Projectile          = proj,
                Position            = impactor.transform.position,
                Rotation            = impactor.transform.rotation,
                FaceExitVelocity    = false,
                MinScale            = minExplosionScale,
                MaxScale            = maxExplosionScale,
                ExplodeDelaySeconds = Mathf.Max(0f, explodeDelay),
                ReturnDelay         = Mathf.Max(0f, returnDelay),
                StopAtImpact        = true,
                DisableColliderNow  = true,
                Prefabs             = aoePrefabs,
                Anonymous           = false,
                OverrideMaterial    = proj.VesselStatus.AOEExplosionMaterial
            });
        }
    }
}