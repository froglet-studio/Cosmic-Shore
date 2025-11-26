using CosmicShore.Core;
using CosmicShore.Utilities;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    [CreateAssetMenu(
        fileName = "DangerHemisphereConfig",
        menuName = "ScriptableObjects/Projectiles/DangerHemisphereConfigSO")]
    public sealed class DangerHemisphereConfigSO : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField] private float explosionDuration = 2f;
        [SerializeField] private float explosionDelay    = 0.2f;

        [Header("Depth Scaling")]
        [SerializeField] private ElementalFloat depthScale = new(1f);

        [Header("Block Shape")]
        [SerializeField] private Vector3 baseBlockScale = new Vector3(10f, 5f, 5f);
        [SerializeField] private int numberOfRays  = 16;
        [SerializeField] private int blocksPerRay  = 5;
        [SerializeField] private float minRadius   = 10f;
        [SerializeField] private float maxRadius   = 50f;
        [SerializeField] private float raySpread   = 15f;

        [Header("Growth")]
        [SerializeField] private float growthRate = 0.05f;
        [SerializeField] private AnimationCurve scaleCurve =
            AnimationCurve.Linear(0f, 1f, 1f, 0.5f);

        [Header("Events")]
        [SerializeField] private PrismEventChannelWithReturnSO prismSpawnEvent;

        [Header("Prism Behaviour")]
        [SerializeField] private bool markShielded  = true;
        [SerializeField] private bool markDangerous = true;

        [Header("Material")]
        [SerializeField] private ThemeManagerDataContainerSO themeManagerDataContainer;

        // ----------------- PROPERTIES -----------------

        public float ExplosionDuration      => explosionDuration;
        public float ExplosionDelay         => explosionDelay;
        public ElementalFloat DepthScale    => depthScale;

        public Vector3 BaseBlockScale       => baseBlockScale;
        public int NumberOfRays             => numberOfRays;
        public int BlocksPerRay             => blocksPerRay;
        public float MinRadius              => minRadius;
        public float MaxRadius              => maxRadius;
        public float RaySpread              => raySpread;

        public float GrowthRate             => growthRate;
        public AnimationCurve ScaleCurve    => scaleCurve;

        public PrismEventChannelWithReturnSO PrismSpawnEvent => prismSpawnEvent;

        public bool MarkShielded            => markShielded;
        public bool MarkDangerous           => markDangerous;

        public ThemeManagerDataContainerSO ThemeManagerDataContainer => themeManagerDataContainer;
    }
}
