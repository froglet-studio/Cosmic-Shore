using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "SkimmerForcefieldCracklePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerForcefieldCracklePrismEffect")]
    public class SkimmerForcefieldCracklePrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Crackle Timing")]
        [SerializeField, Range(0.1f, 2f), Tooltip("How long each crackle persists on the sphere surface (seconds).")]
        private float impactDuration = 0.6f;

        [Header("Crackle Appearance")]
        [SerializeField, Range(0.1f, 5f), Tooltip("Brightness / emission multiplier for the crackle.")]
        private float impactIntensity = 1.5f;

        [SerializeField, Range(0.05f, 1f), Tooltip("Angular radius of the crackle spread (0 = point, 1 ≈ hemisphere).")]
        private float impactRadius = 0.25f;

        [Header("Optional Particle Burst")]
        [SerializeField, Tooltip("Small particle burst prefab spawned at impact point for extra visual detail. Leave null to skip.")]
        private GameObject particleBurstPrefab;

        [SerializeField, Range(0.5f, 3f), Tooltip("Scale multiplier applied to the particle burst.")]
        private float particleBurstScale = 1f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var skimmer = impactor.Skimmer;
            if (skimmer == null) return;

            // --- Resolve colliders ---
            if (!impactor.TryGetComponent<SphereCollider>(out var sphereCollider)) return;

            var prismCollider = prismImpactee.GetComponent<Collider>();
            if (prismCollider == null) return;

            // --- Compute impact point ---
            Vector3 sphereCenter = sphereCollider.transform.TransformPoint(sphereCollider.center);
            float worldRadius = sphereCollider.radius * MaxAbsComponent(sphereCollider.transform.lossyScale);

            Vector3 closestOnPrism = prismCollider.ClosestPoint(sphereCenter);

            Vector3 toImpact = closestOnPrism - sphereCenter;
            if (toImpact.sqrMagnitude < 0.0001f) return;

            Vector3 impactOnSphere = sphereCenter + toImpact.normalized * worldRadius;

            // --- Feed the controller ---
            if (!impactor.TryGetComponent<ForcefieldCrackleController>(out var controller)) return;

            controller.AddImpact(impactOnSphere, impactDuration, impactIntensity, impactRadius);

            // --- Optional particle burst ---
            if (particleBurstPrefab != null)
            {
                SpawnParticleBurst(impactOnSphere, toImpact.normalized);
            }
        }

        void SpawnParticleBurst(Vector3 position, Vector3 normal)
        {
            var go = Instantiate(particleBurstPrefab, position, Quaternion.LookRotation(normal));
            go.transform.localScale = Vector3.one * particleBurstScale;

            if (go.TryGetComponent<ParticleSystem>(out var ps))
            {
                Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(go, 2f);
            }
        }

        static float MaxAbsComponent(Vector3 v)
            => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
    }
}
