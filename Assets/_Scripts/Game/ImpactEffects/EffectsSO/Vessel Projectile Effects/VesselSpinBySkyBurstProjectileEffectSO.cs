using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselSpinBySkyBurstProjectileEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselSpinBySkyBurstProjectileEffectSO")]
    public class VesselSpinBySkyBurstProjectileEffectSO : VesselProjectileEffectSO
    {
        [Header("Spin")]
        [SerializeField] private float spinSpeed = 1.0f;
        [SerializeField]
        private bool useGentleSpin = true;
        [SerializeField]
        private float gentleSpinSeconds = 0.5f;

        [Header("Detonation")]
        [SerializeField] private bool detonateOnHit = true;
        [SerializeField] private ProjectileDetonatorSO detonator;
        [SerializeField] private AOEExplosion[] aoePrefabs;
        [SerializeField] private float minExplosionScale = 0.75f;
        [SerializeField] private float maxExplosionScale = 2.0f;
        [SerializeField]
        private float explodeDelay = 0.15f;
        [SerializeField] private float returnDelay = 0.25f;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            if (impactor?.Vessel == null || impactee?.Projectile == null) return;

            var vesselStatus = impactor.Vessel.VesselStatus;
            var shipTf       = vesselStatus.ShipTransform;
            var projTf       = impactee.Transform;

            var impactDir = (projTf.position - shipTf.position).normalized;

            if (useGentleSpin)
            {
                var lateral   = Vector3.Cross(shipTf.up, impactDir).normalized;
                var newFwd    = (0.6f * impactDir + 0.4f * lateral * (Random.value < 0.5f ? 1f : -1f)).normalized;
                if (newFwd.sqrMagnitude < 1e-6f) newFwd = shipTf.forward;

                vesselStatus.VesselTransformer.GentleSpinShip(newFwd, shipTf.up, Mathf.Max(0.01f, gentleSpinSeconds));
            }
            else
            {
                vesselStatus.VesselTransformer.SpinShip(impactDir * spinSpeed);
            }

            if (!detonateOnHit || !detonator) return;
            var proj = impactee.Projectile;
            detonator.Detonate(new ProjectileDetonatorSO.Request
            {
                Projectile          = proj,
                Position            = impactee.transform.position,
                Rotation            = impactee.transform.rotation,
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