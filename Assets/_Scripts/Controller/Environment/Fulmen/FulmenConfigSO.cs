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
        [Header("Activation")]
        [Tooltip("Omni crystals one collector must gather before the bolt fires (counted per vessel, reset when it fires). 1 = every crystal.")]
        [SerializeField, Range(1, 12)] int crystalsToFire = 3;

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

        [Header("Flash (a real lightning strike: ~0.5s all told)")]
        [Tooltip("Seconds a bolt takes to reach its full range - near-instant, like a real leader.")]
        [SerializeField, Range(0.02f, 1f)] float growSeconds = 0.12f;
        [Tooltip("Seconds the fully grown bolt holds bright.")]
        [SerializeField, Range(0f, 1f)] float holdSeconds = 0.08f;
        [Tooltip("Seconds it flickers out.")]
        [SerializeField, Range(0.02f, 2f)] float fadeSeconds = 0.3f;
        [Tooltip("Flicker steps per second (the re-strokes of a real flash).")]
        [SerializeField, Range(1f, 60f)] float flickerRate = 24f;
        [Tooltip("How deep the flicker dips (0 = a smooth fade).")]
        [SerializeField, Range(0f, 1f)] float flickerDepth = 0.7f;

        [Header("Budgets (per frame, all bolts)")]
        [Tooltip("Most segment queries against the prism index per frame.")]
        [SerializeField, Range(1, 512)] int segmentQueriesPerFrame = 128;
        [Tooltip("Most prisms ignited per frame.")]
        [SerializeField, Range(1, 512)] int ignitesPerFrame = 96;

        [Header("Burn (server only, PrismBurnManager)")]
        [Tooltip("Radius round each segment that catches fire (units).")]
        [SerializeField, Min(0.1f)] float burnRadius = 6f;
        [Tooltip("Seconds a struck prism burns before it is consumed (PrismBurnManager; its spread uses the manager's own).")]
        [SerializeField, Range(0.2f, 6f)] float burnSeconds = 1.4f;
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
        [SerializeField, ColorUsage(true, true)] Color tint = new(0.42f, 0.58f, 0.95f, 1f);
        [Tooltip("Ribbon width at the root (units; thin reads eerier); forks thin by forkShrink per depth, children by childRangeScale.")]
        [SerializeField, Min(0.05f)] float width = 1.6f;
        [Tooltip("Additive material reading vertex colour + uv (FulmenBolt.mat). Empty: Sprites/Default.")]
        [SerializeField] Material material;

        public int CrystalsToFire => crystalsToFire;
        public float RootRangeShare => rootRangeShare;
        public float MinRootRange => minRootRange;
        public float ChildRangeScale => childRangeScale;
        public int MaxChainDepth => maxChainDepth;
        public int MaxChildren => maxChildren;
        public int MaxChildSpawnsPerFrame => maxChildSpawnsPerFrame;
        public float CrystalStrikeRadius => crystalStrikeRadius;
        public bool OmniCrystalsOnly => omniCrystalsOnly;
        public float GrowSeconds => growSeconds;
        public float FlickerRate => flickerRate;
        public float FlickerDepth => flickerDepth;
        public float BurnSeconds => burnSeconds;
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
