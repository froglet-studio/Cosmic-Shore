using Cysharp.Threading.Tasks;
using CosmicShore.Game.Projectiles;
using UnityEngine;
using System;

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
        float returnDelay = 0.25f;

        public override void Execute(ProjectileImpactor impactor, ImpactorBase _)
        {
            if (!impactor) return;

            var proj = impactor.Projectile;
            if (!proj) return;

            var status = proj.VesselStatus;
            var pos = impactor.transform.position;
            var rot = impactor.transform.rotation;
            
            proj.GetComponent<Collider>().enabled = false;
            
            if (faceExitVelocity && proj.Velocity.sqrMagnitude > 1e-6f)
                rot = Quaternion.LookRotation(proj.Velocity.normalized, Vector3.up);

            float charge01 = Mathf.Clamp01(proj.Charge);
            float targetScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, charge01);
            
            foreach (var prefab in aoePrefabs)
            {
                if (!prefab) continue;

                var spawned = Instantiate(prefab, pos, rot);
                spawned.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnDomain           = status.Domain,
                    Vessel              = status.Vessel,
                    MaxScale            = targetScale,
                    OverrideMaterial    = status.AOEExplosionMaterial,
                    AnnonymousExplosion = false,
                    SpawnPosition       = pos,
                    SpawnRotation       = rot
                });
                
                spawned.Detonate();
            }

            ReturnAfterDelay(proj, returnDelay).Forget();
        }

        private async UniTaskVoid ReturnAfterDelay(Projectile proj, float delay)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay));
                proj.ReturnToFactory();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DetonateEndEffectSO] Delayed return failed: {e.Message}");
            }
        }
    }
}
