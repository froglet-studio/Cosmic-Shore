using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "DetonateEndEffect",
        menuName = "ScriptableObjects/Impact Effects/Projectile/End Effects/Detonate")]
    public class DetonateEndEffectSO : ProjectileEndEffectSO
    {
        [SerializeField] private AOEExplosion[] aoePrefabs;
        [SerializeField] private float minExplosionScale = 0.75f;
        [SerializeField] private float maxExplosionScale = 2.5f;
        [SerializeField] private bool faceExitVelocity = true;

        public override void Execute(ProjectileImpactor impactor, ImpactorBase impactee)
        {
            if (!impactor || !impactor.Projectile || aoePrefabs == null || aoePrefabs.Length == 0) return;

            var p   = impactor.Projectile;
            var pos = p.transform.position;
            var rot = faceExitVelocity && p.Velocity.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(p.Velocity.normalized, Vector3.up)
                : p.transform.rotation;

            foreach (var aoePrefab in aoePrefabs)
            {
                if (!aoePrefab) continue;

                var spawned = Instantiate(aoePrefab, pos, rot);
                // spawned.transform.SetParent(null, true);            
                // spawned.transform.position = pos;                   
                // spawned.transform.rotation = rot;

                var status = p.VesselStatus;
                spawned.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnDomain          = status?.Domain ?? Domains.None,
                    Vessel             = status?.Vessel,
                    MaxScale           = Mathf.Lerp(minExplosionScale, maxExplosionScale, Mathf.Clamp01(p.Charge)),
                    OverrideMaterial   = status?.AOEExplosionMaterial,
                    AnnonymousExplosion = false,
                    SpawnPosition      = pos,
                    SpawnRotation      = rot
                });

                spawned.Detonate();
            }
            p.ReturnToFactory();
        }

    }
}