using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DetonateEndEffect", menuName = "ScriptableObjects/Impact Effects/Projectile/End Effects/Detonate")]
    public class DetonateEndEffectSO : ProjectileEndEffectSO
    {
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private bool faceVelocity = true;


        public override void Execute(ProjectileImpactor impactor, ImpactorBase _)
        {
            if (impactor == null || explosionPrefab == null) return;

            var pos = impactor.transform.position;
            var rot = impactor.transform.rotation;

            var proj = impactor.Projectile;
            if (faceVelocity && proj != null && proj.Velocity.sqrMagnitude > 1e-6f)
                rot = Quaternion.LookRotation(proj.Velocity.normalized, Vector3.up);

            GameObject instance = null;

            instance = Instantiate(explosionPrefab, pos, rot);
  
            if (instance != null && instance.TryGetComponent(out AOEExplosion aoe))
            {
                aoe.Detonate();
            }
            else
            {
                Debug.LogWarning("DetonateEndEffectSO: spawned explosion has no AOEExplosion component.");
            }
        }
    }
}