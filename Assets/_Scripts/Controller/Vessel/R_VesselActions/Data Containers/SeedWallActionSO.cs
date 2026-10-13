using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Serialization;
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
        [Tooltip("The seed the CLOAK lays. Super-shielded: a cloak wall outlives the cloak and grows " +
                 "until its seed's super-shield is broken.")]
        [SerializeField] private ShieldMode shieldOnSeed = ShieldMode.SuperShield;
        [Tooltip("The seed the stopped STANCE lays. Never super-shielded (only the cloak lays one): a " +
                 "stance wall grows while the Serpent stays stopped and ends, like a wall whose " +
                 "super-shield was broken, when it flies again.")]
        [SerializeField] private ShieldMode stanceShieldOnSeed = ShieldMode.Shield;
        [Tooltip("Legacy Wall / Gyroid assemblers only: how many bricks the wall may claim, the seed " +
                 "excluded. The Serpent lattice ignores it (it has no brick limit).")]
        [SerializeField] private int bondingDepth = 50;

        [Header("Serpent lattice - bricks (MASS scales size AND spacing)")]
        [Tooltip("Short side of a wall brick at the resting Mass level, in world units. The long " +
                 "side is twice this (SerpentWallLattice.Aspect) and the lattice pitch is 5x it, " +
                 "so shielded bricks point long vertex at short vertex with half a short side of " +
                 "air between them. 3 matches the Serpent's " +
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

        [Header("Serpent lattice - growth (steals from its edge)")]
        [Tooltip("Seconds between steal sweeps. Each sweep is one spatial query and a few waves " +
                 "of claims (SerpentWallStealPlanner); prisms in flight are steered every frame, " +
                 "everything else waits for the next sweep.")]
        [SerializeField, Min(0.05f)] private float stealSweepInterval = 0.3f;
        [Tooltip("How far from an open EDGE site (one next to a brick) a loose prism can be and " +
                 "still be stolen into it, at rest. Scaled by the Mass multiplier (the whole " +
                 "wall's geometry scales with Mass) and by the live Space multiplier below.")]
        [FormerlySerializedAs("recruitRadius")]
        [SerializeField, Min(1f)] private float stealRadius = 40f;
        [Tooltip("Steal-radius multiplier at Space 10 (x1 at rest), read live on every sweep. " +
                 "Space is the wall's reach.")]
        [SerializeField, Min(0.1f)] private float spaceStealRadiusMultiplierAtFull = 2f;
        [Tooltip("Floor for the Space multiplier, so a Space deficit cannot stop the wall stealing.")]
        [SerializeField, Min(0.05f)] private float minSpaceStealRadiusMultiplier = 0.5f;
        [Tooltip("Most prisms one sweep may claim.")]
        [SerializeField, Min(1)] private int maxStealsPerSweep = 48;
        [Tooltip("Waves per sweep: each wave's new bricks open the next wave's edge, so one sweep " +
                 "can creep this many rings along a trail.")]
        [SerializeField, Min(1)] private int stealWavesPerSweep = 4;
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
        [Tooltip("Crack left between each danger panel and the shielded bricks around its hole, " +
                 "as a multiple of the brick's short side (so it scales with Mass). The panel's " +
                 "sides run parallel to the shield edges, so the crack is the same width all round. " +
                 "0.25 is 0.75 units at rest, a panel 10.6 across in a 12.1 hole.")]
        [SerializeField, Range(0f, 1f)] private float lockPanelClearance = 0.25f;
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
        /// <summary>Never SuperShield: <see cref="OnValidate"/> corrects an authored one in the
        /// inspector, and this guards an asset that was written without passing through it.</summary>
        public ShieldMode StanceShieldOnSeed => stanceShieldOnSeed == ShieldMode.SuperShield
            ? ShieldMode.Shield
            : stanceShieldOnSeed;
        public int BondingDepth => bondingDepth;

        public float BrickShortSide => brickShortSide;
        public float BrickDepth => brickDepth;
        public float MassSizeMultiplierAtFull => massSizeMultiplierAtFull;
        public float MinMassSizeMultiplier => minMassSizeMultiplier;
        public float StealSweepInterval => stealSweepInterval;
        public float StealRadius => stealRadius;
        public float SpaceStealRadiusMultiplierAtFull => spaceStealRadiusMultiplierAtFull;
        public float MinSpaceStealRadiusMultiplier => minSpaceStealRadiusMultiplier;
        public int MaxStealsPerSweep => maxStealsPerSweep;
        public int StealWavesPerSweep => stealWavesPerSweep;
        public float OwnPullSpeed => ownPullSpeed;
        public float OpponentPullSpeed => opponentPullSpeed;
        public float PullRotateRate => pullRotateRate;
        public float ShieldRippleStep => shieldRippleStep;
        public float BeamSeconds => beamSeconds;
        public float BeamWidth => beamWidth;
        public float LockPanelClearance => lockPanelClearance;
        public float LockPanelThickness => lockPanelThickness;
        public PrismEventChannelWithReturnSO PrismSpawnChannel => prismSpawnChannel;

        void OnValidate()
        {
            // The super-shield is the cloak's alone (SERPENT_SEED_WALL.md, "The stance's wall").
            if (stanceShieldOnSeed == ShieldMode.SuperShield) stanceShieldOnSeed = ShieldMode.Shield;
        }

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
