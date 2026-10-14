using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for PRISM MOVEMENT, the fundamental (<c>PrismMotion</c>, Docs/PRISM_MOTION.md): the
    /// three-level LADDER a mover climbs as it moves more prisms, and the COLLISION BURST that level 1
    /// draws (<c>PrismCollisionBurst</c>, <c>PrismCollisionBurst.hlsl</c>, Docs/PRISM_ANIMATION.md §4.11).
    ///
    /// <para><b>The ladder.</b> The collision RULE never changes — a moving prism that runs into another
    /// destroys both. What a mover can afford to spend on it does:</para>
    /// <list type="table">
    /// <item><b>Level 1, SHOWCASE</b> (≤ <see cref="ShowcaseMaxMovers"/> moving): exact oriented-box
    /// contact, and each destroyed prism comes apart as a collision burst on the ultra-high-res prism —
    /// while the burst budget has room; past it, the ordinary explosion.</item>
    /// <item><b>Level 2, PLAIN</b> (≤ <see cref="BoxMaxMovers"/>): exact boxes, the ordinary explosion.</item>
    /// <item><b>Level 3, BULK</b> (above): the spheres of the prisms' volumes, in Burst; the ordinary
    /// explosion.</item>
    /// </list>
    /// <para>Each boundary is crossed upward at its value and back downward only at three quarters of it,
    /// so a count hovering at a line does not flip every frame.</para>
    ///
    /// <para><b>The burst's brief</b>: spectacular when looked at, indistinguishable from the explosion
    /// when squinted at. Its FLIGHT, LIFE and PALETTE are the explosion's own (read from
    /// <c>PrismDebris</c>, never authored here); everything here is fine-scale detail, and the spread is
    /// capped at 1 prism radius so the cloud stays the explosion's size.</para>
    ///
    /// Place the asset at <c>Resources/PrismMotionConfig</c>. With no asset the ladder uses the defaults
    /// below and the burst is off (no material) — every collision then draws the ordinary explosion.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismMotionConfig", menuName = "ScriptableObjects/Rendering/Prism Motion Config")]
    public class PrismMotionConfigSO : ScriptableObject
    {
        public const string ResourcePath = "PrismMotionConfig";

        /// <summary>Level 1 ceiling with no asset.</summary>
        public const int DefaultShowcaseMaxMovers = 64;

        /// <summary>Level 2 ceiling with no asset.</summary>
        public const int DefaultBoxMaxMovers = 256;

        [Header("The ladder")]
        [Tooltip("LEVEL 1 ceiling. While a mover moves this many prisms or fewer, its collisions use exact boxes " +
                 "AND draw the high-res collision burst (budget permitting). Above it, level 2. Back to level 1 at " +
                 "three quarters of it. 0 = never level 1.")]
        [Min(0)]
        [SerializeField] int showcaseMaxMovers = DefaultShowcaseMaxMovers;

        [Tooltip("LEVEL 2 ceiling. Up to this many moving prisms, contact is the exact oriented box (main " +
                 "thread); above it, LEVEL 3: the sphere of each prism's volume, in Burst. Back to boxes at three " +
                 "quarters of it (256 -> 192). 0 = always spheres.")]
        [Min(0)]
        [SerializeField] int boxMaxMovers = DefaultBoxMaxMovers;

        [Header("Collision burst (level 1)")]
        [Tooltip("Master switch. Off, every collision draws the ordinary explosion.")]
        [SerializeField] bool burstEnabled = true;

        [Tooltip("The PrismCollisionBurst.shader material. Visual dials (fresnel, hot colour, inner glow, ember) " +
                 "are authored on it; the shape numbers below are written onto a runtime clone, never onto it.")]
        [SerializeField] Material burstMaterial;

        [Tooltip("Quads per face axis on the unwelded high-poly prism a burst draws: 10 is 1,200 triangles " +
                 "against the authored prism's 24. Quadratic in triangles.")]
        [Range(2, 32)]
        [SerializeField] int burstSubdivision = 10;

        [Tooltip("Hard ceiling on bursts alive at once — the 'when we can afford it'. One entity each, so the " +
                 "triangle bill is maxLiveBursts x 12 x subdivision^2 (48 x 1,200 = 57,600 at the defaults). A " +
                 "collision past it draws the ordinary explosion.")]
        [Min(0)]
        [SerializeField] int maxLiveBursts = 48;

        [Header("Variety (relative weights; 0 removes a kind)")]
        [Tooltip("SHATTER: each face fractures into shards that tumble away; the cracks flash white-hot.")]
        [Min(0f)] [SerializeField] float shatterWeight = 1f;
        [Tooltip("BLOOM: each face splits into four petals that curl open from the cube's edges.")]
        [Min(0f)] [SerializeField] float bloomWeight = 1f;
        [Tooltip("UNRAVEL: each face peels into ribbons that curl up from one end, staggered.")]
        [Min(0f)] [SerializeField] float unravelWeight = 1f;
        [Tooltip("GLITTER: every triangle is its own glinting shard, blinking out one by one.")]
        [Min(0f)] [SerializeField] float glitterWeight = 1f;

        [Header("Shape (fine-scale only — the envelope is the explosion's)")]
        [Tooltip("How far a piece leaves the travelling centre, in prism radii. Capped at 1 so the cloud stays " +
                 "the explosion's size when squinted at.")]
        [Range(0f, 1f)]
        [SerializeField] float spread = 0.55f;

        [Tooltip("Time constant of the burst opening (1 - e^(-t/tau)), seconds.")]
        [Min(0.01f)]
        [SerializeField] float openSeconds = 0.22f;

        [Tooltip("Piece spin, radians per unit of explosion speed per second — the explosion's wedges spin with " +
                 "its speed the same way.")]
        [Min(0f)]
        [SerializeField] float spinPerSpeed = 0.3f;

        [Tooltip("Seconds the fracture lines take to cool from white-hot.")]
        [Min(0.01f)]
        [SerializeField] float heatSeconds = 0.35f;

        [Tooltip("BLOOM: a petal tip's final curl, in turns. Under 0.8 so no petal rolls onto itself.")]
        [Range(0f, 0.8f)]
        [SerializeField] float bloomTurns = 0.7f;

        [Tooltip("UNRAVEL: a ribbon end's final curl, in turns. Under 0.85 so no ribbon rolls onto itself.")]
        [Range(0f, 0.85f)]
        [SerializeField] float unravelTurns = 0.8f;

        [Tooltip("UNRAVEL: ribbons per face.")]
        [Range(1, 12)]
        [SerializeField] int ribbonsPerFace = 5;

        [Tooltip("SHATTER: shard seeds per face edge (so this squared per face).")]
        [Range(1, 6)]
        [SerializeField] int shardsPerFaceEdge = 3;

        [Header("Fade")]
        [Tooltip("How ragged the fade is inside a piece (0: each piece blinks out whole). The fade's COVERAGE is " +
                 "linear in age whatever this is — the explosion's curve.")]
        [Range(0f, 1f)]
        [SerializeField] float dissolveGrain = 0.35f;

        [Tooltip("Width of the ember band behind the fade front, as a fraction of the fade order.")]
        [Range(0.001f, 0.3f)]
        [SerializeField] float emberBand = 0.05f;

        [Tooltip("SHATTER: width of a white-hot fracture line, as a fraction of a face.")]
        [Range(0.001f, 0.2f)]
        [SerializeField] float crackWidth = 0.035f;

        [Tooltip("GLITTER: how bright a shard's occasional glint is (0 = none).")]
        [Range(0f, 1f)]
        [SerializeField] float glint = 0.6f;

        public int ShowcaseMaxMovers => Mathf.Max(0, showcaseMaxMovers);
        public int BoxMaxMovers => Mathf.Max(0, boxMaxMovers);
        public bool BurstEnabled => burstEnabled;
        public Material BurstMaterial => burstMaterial;
        public int BurstSubdivision => Mathf.Clamp(burstSubdivision, 2, 32);
        public int MaxLiveBursts => Mathf.Max(0, maxLiveBursts);
        public float Spread => Mathf.Clamp01(spread);
        public float OpenSeconds => Mathf.Max(0.01f, openSeconds);
        public float SpinPerSpeed => Mathf.Max(0f, spinPerSpeed);
        public float HeatSeconds => Mathf.Max(0.01f, heatSeconds);
        public float BloomTurns => Mathf.Clamp(bloomTurns, 0f, 0.8f);
        public float UnravelTurns => Mathf.Clamp(unravelTurns, 0f, 0.85f);
        public int RibbonsPerFace => Mathf.Clamp(ribbonsPerFace, 1, 12);
        public int ShardsPerFaceEdge => Mathf.Clamp(shardsPerFaceEdge, 1, 6);
        public float DissolveGrain => Mathf.Clamp01(dissolveGrain);
        public float EmberBand => Mathf.Clamp(emberBand, 0.001f, 0.3f);
        public float CrackWidth => Mathf.Clamp(crackWidth, 0.001f, 0.2f);
        public float Glint => Mathf.Clamp01(glint);

        /// <summary>The four kind weights, in kind order (shatter, bloom, unravel, glitter).</summary>
        public float WeightOf(int kind) => kind switch
        {
            0 => Mathf.Max(0f, shatterWeight),
            1 => Mathf.Max(0f, bloomWeight),
            2 => Mathf.Max(0f, unravelWeight),
            3 => Mathf.Max(0f, glitterWeight),
            _ => 0f,
        };

        /// <summary>Worst-case burst triangles alive at once.</summary>
        public int WorstCaseTriangles => MaxLiveBursts * 12 * BurstSubdivision * BurstSubdivision;

        /// <summary>
        /// The one predicate the tests and the runtime share: the ladder is ordered, the variety has a kind
        /// to pick, the budget is bounded.
        /// </summary>
        public bool IsSane =>
            ShowcaseMaxMovers <= BoxMaxMovers &&
            WeightOf(0) + WeightOf(1) + WeightOf(2) + WeightOf(3) > 0f &&
            WorstCaseTriangles <= 200_000;

        static PrismMotionConfigSO s_instance;
        static bool s_resolved;

        /// <summary><c>Resources/PrismMotionConfig</c>, resolved once and cached; null when absent.</summary>
        public static PrismMotionConfigSO Instance
        {
            get
            {
                if (!s_resolved)
                {
                    s_instance = Resources.Load<PrismMotionConfigSO>(ResourcePath);
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
