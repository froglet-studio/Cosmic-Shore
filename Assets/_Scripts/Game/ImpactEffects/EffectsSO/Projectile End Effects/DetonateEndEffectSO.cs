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
            if (!impactor || !impactor.Projectile || aoePrefabs == null || aoePrefabs.Length == 0)
                return;

            var projectile = impactor.Projectile;
            var status     = projectile.VesselStatus;
            var pos        = projectile.transform.position;
            var rot        = projectile.transform.rotation;

            if (faceExitVelocity && projectile.Velocity.sqrMagnitude > 1e-6f)
                rot = Quaternion.LookRotation(projectile.Velocity.normalized, Vector3.up);

            foreach (var aoePrefab in aoePrefabs)
            {
                if (!aoePrefab) continue;

                var spawned = Object.Instantiate(aoePrefab, pos, rot);

                var team     = status?.Domain ?? Domains.None;
                var vessel   = status?.Vessel; 
                var mat      = status?.AOEExplosionMaterial;

                spawned.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnDomain             = team,
                    Vessel              = vessel,  
                    MaxScale            = Mathf.Lerp(minExplosionScale, maxExplosionScale, Mathf.Clamp01(projectile.Charge)),
                    OverrideMaterial    = mat,    
                    AnnonymousExplosion = false,
                    SpawnPosition       = pos,
                    SpawnRotation       = rot
                });

                spawned.Detonate();
            }
            Debug.Log("Detonating End Effect");
            projectile.ReturnToFactory();
        }
    }
}