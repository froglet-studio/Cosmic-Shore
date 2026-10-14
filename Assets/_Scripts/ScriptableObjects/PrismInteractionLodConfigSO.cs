using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The DETAIL LEVELS of the prism's two fundamental interactions, game-wide (Docs/PRISM_ANIMATION.md
    /// §4.11): how a prism is DESTROYED and how it is STOLEN, drawn on high-resolution meshes
    /// (<c>PrismCollisionBurst</c>, <c>PrismCollisionBurst.hlsl</c>) when the viewer is close enough to see the
    /// detail and the budget allows, and as the ordinary effect otherwise. <c>PrismInteractionLod</c> decides.
    ///
    /// <list type="bullet">
    /// <item><b>Destruction</b> is one spectrum, coarse to fine: the ordinary EXPLOSION (far away, off screen,
    /// over budget) → SHATTER → GLITTER, itself graded LARGE → MEDIUM → FULL (fragments of three sizes; each
    /// coarser glitter is the finer one with its small fragments merged back into their parents). Every finer
    /// level shares the explosion's flight, size, colours and linear fade, so squinted at it IS the explosion.</item>
    /// <item><b>Stealing</b> is the GRADE (the 0.8 s colour lerp) far away and a SHED close up — bloom, unravel or
    /// flake on a skin in the old colours — graded by mesh resolution (12, 18, 24 subdivisions), on the grade's own
    /// curve over the grade's own length, so squinted at it IS the grade.</item>
    /// </list>
    ///
    /// Both ladders are graded by DISTANCE (the prism's projected diameter over the screen height, so one number
    /// means the same on every camera and resolution) and by AMOUNT (how full the budget already is: crowded, one
    /// level coarser; packed, two). A level that does not fit the triangle budget steps down until one does.
    /// Lives at <c>Resources/PrismInteractionLodConfig</c>; absent, every interaction draws its ordinary effect.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismInteractionLodConfig",
        menuName = "ScriptableObjects/Prisms/Prism Interaction LOD Config")]
    public class PrismInteractionLodConfigSO : ScriptableObject
    {
        public const string ResourcePath = "PrismInteractionLodConfig";

        [Header("The carrier and the budget")]
        [Tooltip("The PrismCollisionBurst.shader material. Its look dials (fresnel, inside colour, dissolve noise) " +
                 "are authored on it; the shape below is written onto a runtime copy.")]
        [SerializeField] Material burstMaterial;

        [Tooltip("Subdivision of the unwelded prism the SHATTER draws (10 = 1,200 triangles, the slice's mesh).")]
        [SerializeField] int burstSubdivision = 10;

        [Tooltip("Detail-level entities alive at once, destruction and sheds together. Past it, every interaction " +
                 "draws its ordinary effect — never nothing.")]
        [SerializeField] int maxLiveBursts = 48;

        [Tooltip("Detail-level TRIANGLES alive at once. A level that would exceed it steps down to a cheaper one, " +
                 "and finally to the ordinary effect.")]
        [SerializeField] int maxLiveTriangles = 80_000;

        [Tooltip("AMOUNT: when the budget (entities or triangles, whichever is fuller) is at least this full, every " +
                 "new interaction is drawn one level coarser.")]
        [Range(0f, 1f)] [SerializeField] float crowdedFill = 0.5f;

        [Tooltip("AMOUNT: at least this full, two levels coarser.")]
        [Range(0f, 1f)] [SerializeField] float packedFill = 0.8f;

        [Header("Destruction — explosion, shatter, glitter large / medium / full")]
        [Tooltip("Off: every destruction is the ordinary explosion.")]
        [SerializeField] bool destructionEnabled = true;

        [Tooltip("The prism's projected diameter, as a fraction of the screen height, at and above which a death " +
                 "SHATTERS instead of exploding.")]
        [Range(0f, 1f)] [SerializeField] float shatterMinScreen = 0.05f;

        [Tooltip("At and above this, a death GLITTERS in its LARGE fragments only (a quarter of a face across).")]
        [Range(0f, 1f)] [SerializeField] float glitterMinScreen = 0.12f;

        [Tooltip("At and above this, the glitter adds its MEDIUM fragments (an eighth of a face).")]
        [Range(0f, 1f)] [SerializeField] float glitterMediumScreen = 0.17f;

        [Tooltip("At and above this, the FULL glitter: its small fragments too (a sixteenth of a face).")]
        [Range(0f, 1f)] [SerializeField] float glitterFullScreen = 0.24f;

        [Header("Stealing — the grade, or a shed at one of three resolutions")]
        [Tooltip("Off: every steal is the ordinary grade.")]
        [SerializeField] bool shedEnabled = true;

        [Tooltip("At and above this projected diameter a steal SHEDS (at shedSubdivision) instead of grading.")]
        [Range(0f, 1f)] [SerializeField] float shedMinScreen = 0.08f;

        [Tooltip("At and above this, the shed is drawn at shedFineSubdivision.")]
        [Range(0f, 1f)] [SerializeField] float shedFineScreen = 0.16f;

        [Tooltip("At and above this, the shed is drawn at shedFinestSubdivision.")]
        [Range(0f, 1f)] [SerializeField] float shedFinestScreen = 0.28f;

        [Tooltip("Mesh subdivision of the coarsest shed. Keep the three a multiple of ribbonsPerFace and of twice " +
                 "shardsPerFaceEdge, so ribbons and flake scales fall on whole triangles.")]
        [SerializeField] int shedSubdivision = 12;

        [Tooltip("Mesh subdivision of the finer shed (12 x subdivision^2 triangles).")]
        [SerializeField] int shedFineSubdivision = 18;

        [Tooltip("Mesh subdivision of the finest shed (24 = 6,912 triangles).")]
        [SerializeField] int shedFinestSubdivision = 24;

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
        [SerializeField] int ribbonsPerFace = 6;

        [Tooltip("Shatter seeds per face edge (n x n shards a face); the flake's scales are a grid twice as fine.")]
        [SerializeField] int shardsPerFaceEdge = 3;

        [Tooltip("How ragged the dissolve is inside a piece (0: each piece goes whole). Glitter shards and flake " +
                 "scales always go whole.")]
        [SerializeField] float dissolveGrain = 0.35f;

        public Material BurstMaterial => burstMaterial;
        public int BurstSubdivision => Mathf.Clamp(burstSubdivision, 2, 32);
        public int MaxLiveBursts => Mathf.Max(0, maxLiveBursts);
        public int MaxLiveTriangles => Mathf.Max(0, maxLiveTriangles);
        public float CrowdedFill => Mathf.Clamp01(crowdedFill);
        public float PackedFill => Mathf.Clamp01(packedFill);
        public bool DestructionEnabled => destructionEnabled;
        public float ShatterMinScreen => Mathf.Clamp01(shatterMinScreen);
        public float GlitterMinScreen => Mathf.Clamp01(glitterMinScreen);
        public float GlitterMediumScreen => Mathf.Clamp01(glitterMediumScreen);
        public float GlitterFullScreen => Mathf.Clamp01(glitterFullScreen);
        public bool ShedEnabled => shedEnabled;
        public float ShedMinScreen => Mathf.Clamp01(shedMinScreen);
        public float ShedFineScreen => Mathf.Clamp01(shedFineScreen);
        public float ShedFinestScreen => Mathf.Clamp01(shedFinestScreen);
        public int ShedSubdivision => Mathf.Clamp(shedSubdivision, 2, 32);
        public int ShedFineSubdivision => Mathf.Clamp(shedFineSubdivision, 2, 32);
        public int ShedFinestSubdivision => Mathf.Clamp(shedFinestSubdivision, 2, 32);
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

        /// <summary>The screen-size lines of the destruction ladder, coarse to fine (shatter, glitter L, M, F).</summary>
        public float DestructionLine(int level) => level switch
        {
            1 => ShatterMinScreen,
            2 => GlitterMinScreen,
            3 => GlitterMediumScreen,
            4 => GlitterFullScreen,
            _ => 0f,
        };

        /// <summary>The screen-size lines of the steal ladder, coarse to fine (shed, fine, finest).</summary>
        public float StealLine(int level) => level switch
        {
            1 => ShedMinScreen,
            2 => ShedFineScreen,
            3 => ShedFinestScreen,
            _ => 0f,
        };

        /// <summary>The mesh subdivision of steal level 1–3.</summary>
        public int ShedSubdivisionOf(int level) => level >= 3 ? ShedFinestSubdivision : level == 2 ? ShedFineSubdivision : ShedSubdivision;

        /// <summary>The most triangles one entity of any level can draw.</summary>
        public int LargestEntityTriangles =>
            Mathf.Max(12 * BurstSubdivision * BurstSubdivision, 12 * ShedFinestSubdivision * ShedFinestSubdivision);

        /// <summary>
        /// The one predicate the tests and the runtime share: each ladder is ordered, the crowding steps are ordered,
        /// a shed has a kind to pick, the budget is bounded and holds at least one entity of every level.
        /// </summary>
        public bool IsSane =>
            ShatterMinScreen <= GlitterMinScreen && GlitterMinScreen <= GlitterMediumScreen &&
            GlitterMediumScreen <= GlitterFullScreen &&
            ShedMinScreen <= ShedFineScreen && ShedFineScreen <= ShedFinestScreen &&
            ShedSubdivision <= ShedFineSubdivision && ShedFineSubdivision <= ShedFinestSubdivision &&
            CrowdedFill <= PackedFill &&
            (!ShedEnabled || BloomWeight + UnravelWeight + FlakeWeight > 0f) &&
            MaxLiveTriangles <= 200_000 && LargestEntityTriangles <= MaxLiveTriangles;

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
