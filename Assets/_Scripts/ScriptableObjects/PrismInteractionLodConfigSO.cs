using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The DETAIL LEVELS of the prism's two fundamental interactions, game-wide (Docs/PRISM_ANIMATION.md
    /// §4.11): how a prism is DESTROYED and how it is STOLEN, drawn on the ultra-high-res prism
    /// (<c>PrismCollisionBurst</c>, <c>PrismCollisionBurst.hlsl</c>) when the viewer is close enough to see the
    /// detail and the budget allows, and as the ordinary effect otherwise. <c>PrismInteractionLod</c> decides.
    ///
    /// <list type="bullet">
    /// <item><b>Destruction</b> is one spectrum, coarse to fine: the ordinary EXPLOSION (far away, off screen,
    /// over budget) → SHATTER → GLITTER (close up). Every finer level shares the explosion's flight, size,
    /// colours and linear fade, so squinted at it IS the explosion.</item>
    /// <item><b>Stealing</b> is the GRADE (the 0.8 s colour lerp) far away and a SHED close up: the prism takes
    /// its new colour at once and a skin wearing the old colours comes off it — bloom, unravel or flake — on
    /// the grade's own curve over the grade's own length, so squinted at it IS the grade.</item>
    /// </list>
    ///
    /// Distances are measured as the prism's projected DIAMETER over the screen HEIGHT, so one number means
    /// the same thing on every camera, field of view and resolution. Lives at
    /// <c>Resources/PrismInteractionLodConfig</c>; absent, every interaction draws its ordinary effect.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismInteractionLodConfig",
        menuName = "ScriptableObjects/Prisms/Prism Interaction LOD Config")]
    public class PrismInteractionLodConfigSO : ScriptableObject
    {
        public const string ResourcePath = "PrismInteractionLodConfig";

        [Header("The carrier — the ultra-high-res prism")]
        [Tooltip("The PrismCollisionBurst.shader material. Its look dials (fresnel, inside colour, dissolve noise) " +
                 "are authored on it; the shape below is written onto a runtime copy.")]
        [SerializeField] Material burstMaterial;

        [Tooltip("Subdivision of the unwelded high-poly prism every detail level draws (10 = 1,200 triangles, the " +
                 "slice's mesh).")]
        [SerializeField] int burstSubdivision = 10;

        [Tooltip("Detail-level entities alive at once, destruction and sheds together. The triangle bill is " +
                 "maxLiveBursts x 12 x subdivision^2 (48 x 1,200 = 57,600 at the defaults). An interaction past " +
                 "the budget draws its ordinary effect — never nothing.")]
        [SerializeField] int maxLiveBursts = 48;

        [Header("Destruction — explosion, shatter, glitter")]
        [Tooltip("Off: every destruction is the ordinary explosion.")]
        [SerializeField] bool destructionEnabled = true;

        [Tooltip("The prism's projected diameter, as a fraction of the screen height, at and above which a death " +
                 "SHATTERS instead of exploding.")]
        [Range(0f, 1f)] [SerializeField] float shatterMinScreen = 0.05f;

        [Tooltip("The projected diameter at and above which a death GLITTERS (every triangle a shard) instead of " +
                 "shattering. Must not be below the shatter's.")]
        [Range(0f, 1f)] [SerializeField] float glitterMinScreen = 0.15f;

        [Header("Stealing — the grade, or a shed")]
        [Tooltip("Off: every steal is the ordinary grade.")]
        [SerializeField] bool shedEnabled = true;

        [Tooltip("The projected diameter at and above which a steal SHEDS its old colours instead of grading.")]
        [Range(0f, 1f)] [SerializeField] float shedMinScreen = 0.08f;

        [Tooltip("Relative weight of the BLOOM shed (petals curling open off each face); 0 removes it.")]
        [Min(0f)] [SerializeField] float bloomWeight = 1f;

        [Tooltip("Relative weight of the UNRAVEL shed (ribbons peeling off along each face); 0 removes it.")]
        [Min(0f)] [SerializeField] float unravelWeight = 1f;

        [Tooltip("Relative weight of the FLAKE shed (scales tipping off in a wave down the prism); 0 removes it.")]
        [Min(0f)] [SerializeField] float flakeWeight = 1f;

        [Tooltip("How far the shed skin sits outside the prism, in prism radii, so the two never share a depth.")]
        [Range(0f, 0.2f)] [SerializeField] float shedInflate = 0.02f;

        [Header("Shape (fine scale only — the envelope is the ordinary effect's)")]
        [Tooltip("How far a piece leaves, in prism radii. Capped at 1 so a destruction stays the explosion's size " +
                 "squinted at.")]
        [SerializeField] float spread = 0.55f;

        [Tooltip("Time constant of the opening (1 - e^(-t/tau)), seconds: short is a crack, long a slow bloom.")]
        [SerializeField] float openSeconds = 0.22f;

        [Tooltip("Shard tumble, radians per unit of explosion speed per second — the explosion's wedges spin with " +
                 "its speed the same way.")]
        [SerializeField] float spinPerSpeed = 0.3f;

        [Tooltip("A bloom petal tip's final curl, in turns (capped 0.8).")]
        [SerializeField] float bloomTurns = 0.7f;

        [Tooltip("An unravelling ribbon end's final curl, in turns (capped 0.85).")]
        [SerializeField] float unravelTurns = 0.8f;

        [Tooltip("Ribbons per face for the unravel.")]
        [SerializeField] int ribbonsPerFace = 5;

        [Tooltip("Shatter seeds per face edge (n x n shards a face); the flake's scales are a grid twice as fine.")]
        [SerializeField] int shardsPerFaceEdge = 3;

        [Tooltip("How ragged the dissolve is inside a piece (0: each piece goes whole). Glitter shards and flake " +
                 "scales always go whole.")]
        [SerializeField] float dissolveGrain = 0.35f;

        public Material BurstMaterial => burstMaterial;
        public int BurstSubdivision => Mathf.Clamp(burstSubdivision, 2, 32);
        public int MaxLiveBursts => Mathf.Max(0, maxLiveBursts);
        public bool DestructionEnabled => destructionEnabled;
        public float ShatterMinScreen => Mathf.Clamp01(shatterMinScreen);
        public float GlitterMinScreen => Mathf.Clamp01(glitterMinScreen);
        public bool ShedEnabled => shedEnabled;
        public float ShedMinScreen => Mathf.Clamp01(shedMinScreen);
        public float BloomWeight => Mathf.Max(0f, bloomWeight);
        public float UnravelWeight => Mathf.Max(0f, unravelWeight);
        public float FlakeWeight => Mathf.Max(0f, flakeWeight);
        public float ShedInflate => Mathf.Clamp(shedInflate, 0f, 0.2f);
        public float Spread => Mathf.Clamp01(spread);
        public float OpenSeconds => Mathf.Max(0.01f, openSeconds);
        public float SpinPerSpeed => Mathf.Max(0f, spinPerSpeed);
        public float BloomTurns => Mathf.Clamp(bloomTurns, 0f, 0.8f);
        public float UnravelTurns => Mathf.Clamp(unravelTurns, 0f, 0.85f);
        public int RibbonsPerFace => Mathf.Clamp(ribbonsPerFace, 1, 12);
        public int ShardsPerFaceEdge => Mathf.Clamp(shardsPerFaceEdge, 1, 6);
        public float DissolveGrain => Mathf.Clamp01(dissolveGrain);

        /// <summary>Worst-case detail-level triangles alive at once.</summary>
        public int WorstCaseTriangles => MaxLiveBursts * 12 * BurstSubdivision * BurstSubdivision;

        /// <summary>
        /// The one predicate the tests and the runtime share: the spectrum is ordered, a shed has a kind to pick,
        /// the budget is bounded.
        /// </summary>
        public bool IsSane =>
            ShatterMinScreen <= GlitterMinScreen &&
            (!ShedEnabled || BloomWeight + UnravelWeight + FlakeWeight > 0f) &&
            WorstCaseTriangles <= 200_000;

        static PrismInteractionLodConfigSO s_instance;
        static bool s_resolved;

        /// <summary><c>Resources/PrismInteractionLodConfig</c>, resolved once and cached; null when absent.</summary>
        public static PrismInteractionLodConfigSO Instance
        {
            get
            {
                if (!s_resolved)
                {
                    s_instance = Resources.Load<PrismInteractionLodConfigSO>(ResourcePath);
                    s_resolved = true;
                }
                return s_instance;
            }
        }

        /// <summary>Editor tooling: forget the cached asset so the next ask re-reads it.</summary>
        public static void InvalidateInstance()
        {
            s_instance = null;
            s_resolved = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => InvalidateInstance();
    }
}
