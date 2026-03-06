using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerForcefieldCracklePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerForcefieldCracklePrismEffect")]
    public class SkimmerForcefieldCracklePrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Crackle Timing")]
        [SerializeField, Range(0.1f, 2f), Tooltip("How long each crackle persists on the sphere surface (seconds).")]
        private float impactDuration = 0.6f;

        [SerializeField, Range(0.2f, 3f), Tooltip("Multiplier on how fast the ripple ring expands outward. 1 = reaches max radius exactly at expiry.")]
        private float rippleSpeed = 1f;

        [Header("Crackle Appearance")]
        [SerializeField, Range(0.1f, 5f), Tooltip("Brightness / emission multiplier for the crackle.")]
        private float impactIntensity = 1.5f;

        [SerializeField, Range(0.05f, 1f), Tooltip("Angular radius of the crackle spread (0 = point, 1 ≈ hemisphere).")]
        private float impactRadius = 0.25f;

        [Header("Arc Pattern")]
        [SerializeField, Range(4f, 20f), Tooltip("Number of arc branches radiating from each impact point.")]
        private float arcDensity = 8f;

        [SerializeField, Range(0.01f, 0.5f), Tooltip("Arc width — lower values produce thinner, sharper lightning arcs.")]
        private float arcSharpness = 0.06f;

        [Header("Ring / Wave Shape")]
        [SerializeField, Range(0.05f, 1f), Tooltip("Thickness of the expanding ring wavefront relative to angular radius.")]
        private float ringThickness = 0.4f;

        [SerializeField, Range(0f, 1f), Tooltip("How much the impact center fills in (vs. purely a ring). 0 = ring only, 1 = solid fill at center.")]
        private float centerFillAmount = 0.3f;

        [Header("Colors")]
        [SerializeField, Tooltip("Primary crackle color (the dominant line color).")]
        private Color crackleColorA = new Color(0.3f, 0.6f, 1f, 1f);

        [SerializeField, Tooltip("Secondary crackle color (highlights / hot edges).")]
        private Color crackleColorB = new Color(0.8f, 0.9f, 1f, 1f);

        [Header("Fresnel Rim")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Ambient rim glow intensity when no impacts are active.")]
        private float fresnelRimIntensity = 0.08f;

        [SerializeField, Range(1f, 8f), Tooltip("Fresnel power — higher values make the rim thinner.")]
        private float fresnelRimPower = 3f;

        [SerializeField, Tooltip("Rim glow color.")]
        private Color fresnelRimColor = new Color(0.3f, 0.5f, 0.8f, 1f);

        [Header("Optional Particle Burst")]
        [SerializeField, Tooltip("Small particle burst prefab spawned at impact point for extra visual detail. Leave null to skip.")]
        private GameObject particleBurstPrefab;

        [SerializeField, Range(0.5f, 3f), Tooltip("Scale multiplier applied to the particle burst.")]
        private float particleBurstScale = 1f;

        /// <summary>
        /// Pushes all visual config to the controller so it can forward them to the shader.
        /// Called once per impact — the controller caches until values change.
        /// </summary>
        void PushVisualConfig(ForcefieldCrackleController controller)
        {
            controller.SetVisualParams(
                crackleColorA,
                crackleColorB,
                fresnelRimColor,
                arcDensity,
                arcSharpness,
                ringThickness,
                centerFillAmount,
                rippleSpeed,
                fresnelRimIntensity,
                fresnelRimPower
            );
        }

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

            PushVisualConfig(controller);
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
