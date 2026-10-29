using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// FULMEN's tuning (the Charge fundamental; <c>Fulmen/README.md</c>): how big a root bolt is (a share of its cell's
    /// radius), how a struck omni crystal chains a smaller child, how fast bolts grow, the per-frame budgets, the burn, and
    /// the look. Vessel-agnostic: any effect, vessel or scene that hands this to <see cref="FulmenSystem"/> gets the same bolt.
    /// </summary>
    [CreateAssetMenu(fileName = "FulmenConfig", menuName = "ScriptableObjects/Environment/Fulmen Config")]
    public sealed class FulmenConfigSO : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("Root bolt range as a share of its cell's radius (SolitonNetwork.ResolveCell).")]
        [SerializeField, Range(0.05f, 1f)] float rootRangeShare = 0.6f;
        [Tooltip("Never smaller than this (units), whatever the cell.")]
        [SerializeField, Min(10f)] float minRootRange = 120f;
        [Tooltip("A child bolt's range is its parent's times this.")]
        [SerializeField, Range(0.05f, 1f)] float childRangeScale = 0.4f;

        [Header("Chain")]
        [Tooltip("Deepest child generation (root = 0).")]
        [SerializeField, Range(0, 4)] int maxChainDepth = 2;
        [Tooltip("Most child bolts one chain may spawn in total.")]
        [SerializeField, Range(0, 32)] int maxChildren = 6;
        [Tooltip("Most child bolts spawned in one frame (the rest wait).")]
        [SerializeField, Range(1, 8)] int maxChildSpawnsPerFrame = 1;
        [Tooltip("A branch strikes a crystal whose centre is this close to it (units).")]
        [SerializeField, Min(0.5f)] float crystalStrikeRadius = 12f;
        [Tooltip("Only omni crystals chain (off: any live crystal).")]
        [SerializeField] bool omniCrystalsOnly = true;

        [Header("Growth & life")]
        [Tooltip("Bolt front speed, units per second along each stream.")]
        [SerializeField, Min(1f)] float growthSpeed = 600f;
        [Tooltip("Seconds a fully grown bolt holds before fading.")]
        [SerializeField, Min(0f)] float holdSeconds = 0.25f;
        [Tooltip("Seconds it fades.")]
        [SerializeField, Min(0.01f)] float fadeSeconds = 0.4f;

        [Header("Budgets (per frame, all bolts)")]
        [Tooltip("Most segment queries against the prism index per frame.")]
        [SerializeField, Range(1, 256)] int segmentQueriesPerFrame = 48;
        [Tooltip("Most prisms ignited per frame.")]
        [SerializeField, Range(1, 512)] int ignitesPerFrame = 64;

        [Header("Burn (server only, PrismBurnManager)")]
        [Tooltip("Radius round each segment that catches fire (units).")]
        [SerializeField, Min(0.1f)] float burnRadius = 6f;
        [Tooltip("Burnout converts the prism to the igniter's team instead of destroying it.")]
        [SerializeField] bool convertOnBurnout;

        [Header("Shape (FulmenMath)")]
        [SerializeField, Range(1, 16)] int roots = 5;
        [SerializeField, Range(8, 512)] int maxSegments = 160;
        [SerializeField, Range(1, 64)] int maxBranches = 24;
        [SerializeField, Range(0, 4)] int maxBranchDepth = 2;
        [SerializeField, Range(0.01f, 0.5f)] float segmentMin = 0.06f;
        [SerializeField, Range(0.01f, 0.5f)] float segmentMax = 0.14f;
        [SerializeField, Range(0f, 2f)] float jitter = 0.55f;
        [SerializeField, Range(0f, 2f)] float outward = 0.35f;
        [SerializeField, Range(0f, 1f)] float forkChance = 0.18f;
        [SerializeField, Range(0.05f, 1f)] float forkShrink = 0.7f;

        [Header("Look")]
        [SerializeField, ColorUsage(true, true)] Color tint = new(0.55f, 0.75f, 1f, 1f);
        [Tooltip("Ribbon width at the root (units); forks thin by forkShrink per depth, children by childRangeScale.")]
        [SerializeField, Min(0.05f)] float width = 3f;
        [Tooltip("Optional additive material (vertex colour). Empty: Sprites/Default.")]
        [SerializeField] Material material;

        public float RootRangeShare => rootRangeShare;
        public float MinRootRange => minRootRange;
        public float ChildRangeScale => childRangeScale;
        public int MaxChainDepth => maxChainDepth;
        public int MaxChildren => maxChildren;
        public int MaxChildSpawnsPerFrame => maxChildSpawnsPerFrame;
        public float CrystalStrikeRadius => crystalStrikeRadius;
        public bool OmniCrystalsOnly => omniCrystalsOnly;
        public float GrowthSpeed => growthSpeed;
        public float HoldSeconds => holdSeconds;
        public float FadeSeconds => fadeSeconds;
        public int SegmentQueriesPerFrame => segmentQueriesPerFrame;
        public int IgnitesPerFrame => ignitesPerFrame;
        public float BurnRadius => burnRadius;
        public bool ConvertOnBurnout => convertOnBurnout;
        public int MaxSegments => maxSegments;
        public Color Tint => tint;
        public float Width => width;
        public Material Material => material;

        // --- runtime stats (play mode only, never serialized; FulmenConfigSOEditor shows them) ---
        /// <summary>Prisms set alight by the most recent strike with this config (its whole chain). Counted where the burn runs:
        /// the server, or offline; 0 on a client.</summary>
        [System.NonSerialized] public int LastPrismsBurnt;
        /// <summary>Crystals the most recent strike's chain struck.</summary>
        [System.NonSerialized] public int LastCrystalsStruck;
        /// <summary>Bolts (root + children) the most recent strike drew.</summary>
        [System.NonSerialized] public int LastBolts;
        /// <summary>Time.time of the most recent strike; -1 before the first.</summary>
        [System.NonSerialized] public float LastStrikeTime = -1f;
        /// <summary>Strikes with this config since play mode began.</summary>
        [System.NonSerialized] public int Strikes;

        internal void BeginStrikeStats(float now)
        {
            LastPrismsBurnt = LastCrystalsStruck = LastBolts = 0;
            LastStrikeTime = now;
            Strikes++;
        }

        void OnEnable()
        {
            LastPrismsBurnt = LastCrystalsStruck = LastBolts = Strikes = 0;
            LastStrikeTime = -1f;
        }

        /// <summary>The root range in a cell of <paramref name="cellRadius"/>.</summary>
        public float RootRange(float cellRadius) => Mathf.Max(minRootRange, cellRadius * rootRangeShare);

        /// <summary>The generator's settings for a bolt of <paramref name="range"/>.</summary>
        public FulmenMath.Settings Shape(float range) => new()
        {
            Range = range, Roots = roots, MaxSegments = maxSegments, MaxBranches = maxBranches, MaxDepth = maxBranchDepth,
            SegmentMin = Mathf.Min(segmentMin, segmentMax), SegmentMax = Mathf.Max(segmentMin, segmentMax),
            Jitter = jitter, Outward = outward, ForkChance = forkChance, ForkShrink = forkShrink,
        };
    }
}
