using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using System.Linq;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SeedWallAction", menuName = "ScriptableObjects/Vessel Actions/Seed Wall")]
    public class SeedWallActionSO : ShipActionSO
    {
        /// <summary>Which assembler a seed grows. <see cref="SerpentLattice"/> is the Serpent's
        /// Mass ability (R_VesselActions/SERPENT_SEED_WALL.md); the other two are the legacy
        /// flora-style mate-pulling wall.</summary>
        public enum AssemblerKind { Wall = 0, Gyroid = 1, SerpentLattice = 2 }
        public enum ShieldMode   { None = 0, Shield = 1, SuperShield = 2 }

        [Header("Resource")]
        [Tooltip("Which resource pool to consume when seeding.")]
        [SerializeField] private int resourceIndex = 0;

        [Tooltip("Cost = MaxAmount / enhancementsPerFullAmmo")]
        [SerializeField] private float enhancementsPerFullAmmo = 3f;

        [Header("Seeding Rules")]
        [SerializeField] private bool requireExistingTrailBlock = true;
        [SerializeField] private AssemblerKind assemblerType = AssemblerKind.Wall;
        [SerializeField] private ShieldMode shieldOnSeed = ShieldMode.SuperShield;
        [Tooltip("How many bricks the wall may claim, the seed excluded.")]
        [SerializeField] private int bondingDepth = 50;

        [Header("Serpent lattice - bricks (MASS scales size AND spacing)")]
        [Tooltip("Short side of a wall brick at the resting Mass level, in world units. The long " +
                 "side is twice this (SerpentWallLattice.Aspect) and the lattice pitch is 1.5x it, " +
                 "so shielded bricks touch long vertex to short vertex. 3 matches the Serpent's " +
                 "trail BaseScale (3 x 6 x 0.5), so a resting wall is made of trail-sized bricks.")]
        [SerializeField, Min(0.1f)] private float brickShortSide = 3f;
        [Tooltip("Brick thickness along the wall's normal at the resting Mass level.")]
        [SerializeField, Min(0.05f)] private float brickDepth = 0.5f;
        [Tooltip("MASS -> brick size and lattice spacing: the multiplier at Mass level 10 (1 at " +
                 "rest). One number scales both, because the pitch is derived from the brick. " +
                 "Snapshotted when the seed is placed, so a wall keeps the shape it was born with.")]
        [SerializeField, Min(0.1f)] private float massSizeMultiplierAtFull = 2f;
        [Tooltip("Floor for the Mass multiplier, so a Mass deficit cannot collapse the wall.")]
        [SerializeField, Min(0.05f)] private float minMassSizeMultiplier = 0.5f;

        [Header("Serpent lattice - growth")]
        [Tooltip("Seconds between site claims while the seed is bonding.")]
        [SerializeField, Min(0.01f)] private float siteClaimInterval = 0.15f;
        [Tooltip("How far from an empty site the wall looks for a prism to pull into it, at the " +
                 "resting Mass level (scaled by the same Mass multiplier as the bricks).")]
        [SerializeField, Min(1f)] private float recruitRadius = 40f;
        [Tooltip("Pull speed (units/s) for the Serpent's OWN domain's prisms.")]
        [SerializeField, Min(0.1f)] private float ownPullSpeed = 20f;
        [Tooltip("Pull speed (units/s) for an OPPONENT's prisms. They are stolen when they land: " +
                 "this is the wall stealing along its edge.")]
        [SerializeField, Min(0.1f)] private float opponentPullSpeed = 6f;
        [Tooltip("Rotation catch-up rate while a recruited prism is in flight.")]
        [SerializeField, Min(0.1f)] private float pullRotateRate = 8f;

        [Header("Serpent lattice - omni crystal re-shield")]
        [Tooltip("Seconds between rings of the shield ripple, outward from the seed.")]
        [SerializeField, Min(0f)] private float shieldRippleStep = 0.08f;
        [Tooltip("How long each crystal-to-seed beam is drawn (the sniper's tracer, in the " +
                 "Serpent's domain colour). 0 draws no beam; the shield ripple still runs.")]
        [SerializeField, Min(0f)] private float beamSeconds = 0.35f;
        [Tooltip("Beam width at the crystal end; it narrows to half at the seed.")]
        [SerializeField, Min(0.01f)] private float beamWidth = 0.6f;

        [Header("Serpent lattice - Mass level 5: Lockdown")]
        [Tooltip("Degrees every brick turns clockwise (seen from the Serpent's seat) on the first " +
                 "omni crystal after a Mass-5 wall is placed. The twist opens one parity of cell " +
                 "and closes the other; 15 opens the hole ~9% wider than at rest (about 20 is the peak). " +
                 "Capped at SerpentWallLattice.MaxTwistDegrees, the most the shield gap is sized for.")]
        [SerializeField, Range(1f, SerpentWallLattice.MaxTwistDegrees)] private float lockTwistDegrees = 15f;
        [Tooltip("Seconds the twist takes to play out.")]
        [SerializeField, Min(0f)] private float lockTwistSeconds = 0.4f;
        [Tooltip("Thickness of a danger panel along the wall normal.")]
        [SerializeField, Min(0.02f)] private float lockPanelThickness = 0.15f;
        [Tooltip("Spawns the danger panels. The same channel every vessel lays standalone prisms " +
                 "through (EventOnSpawnPrismAndReturn).")]
        [SerializeField] private PrismEventChannelWithReturnSO prismSpawnChannel;

        // Expose
        public int ResourceIndex => resourceIndex;
        public float EnhancementsPerFullAmmo => enhancementsPerFullAmmo;
        public bool RequireExistingTrailBlock => requireExistingTrailBlock;
        public AssemblerKind AssemblerType => assemblerType;
        public ShieldMode ShieldOnSeed => shieldOnSeed;
        public int BondingDepth => bondingDepth;

        public float BrickShortSide => brickShortSide;
        public float BrickDepth => brickDepth;
        public float MassSizeMultiplierAtFull => massSizeMultiplierAtFull;
        public float MinMassSizeMultiplier => minMassSizeMultiplier;
        public float SiteClaimInterval => siteClaimInterval;
        public float RecruitRadius => recruitRadius;
        public float OwnPullSpeed => ownPullSpeed;
        public float OpponentPullSpeed => opponentPullSpeed;
        public float PullRotateRate => pullRotateRate;
        public float ShieldRippleStep => shieldRippleStep;
        public float BeamSeconds => beamSeconds;
        public float BeamWidth => beamWidth;
        public float LockTwistDegrees => lockTwistDegrees;
        public float LockTwistSeconds => lockTwistSeconds;
        public float LockPanelThickness => lockPanelThickness;
        public PrismEventChannelWithReturnSO PrismSpawnChannel => prismSpawnChannel;

        public float ComputeCost(ResourceSystem rs)
        {
            if (rs == null || resourceIndex < 0 || resourceIndex >= rs.Resources.Count) return 0f;
            var res = rs.Resources[resourceIndex];
            if (res == null || res.MaxAmount <= 0f) return 0f;
            var denom = Mathf.Max(0.0001f, enhancementsPerFullAmmo);
            return res.MaxAmount / denom;
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SeedAssemblerActionExecutor>()?.StartSeed(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SeedAssemblerActionExecutor>()?.StopSeedCompletely();
    }
}
