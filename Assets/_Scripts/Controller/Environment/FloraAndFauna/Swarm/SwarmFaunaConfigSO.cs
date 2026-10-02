using CosmicShore.Data;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the swarm fauna runs on (Docs/SWARM_FAUNA.md). One asset serves every swarm:
    /// which creature a swarm STARTS as is not here - it is the species config's Element
    /// (FaunaConfigurationSO.Element, the platform's element-as-data channel), and where it lives
    /// is that config's band. Authored by Tools/Build/author_swarm_fauna.py; hand edits are drift.
    ///
    /// Units: the sim runs in the RESEARCH's units ("voxels", per STEP). <see cref="UnitScale"/>
    /// converts positions to world units and <see cref="TickHz"/> converts steps to seconds, so a
    /// sim speed of 0.8 voxels/step is 0.8 x UnitScale x TickHz world units per second.
    /// </summary>
    [CreateAssetMenu(fileName = "SwarmFaunaConfig", menuName = "ScriptableObjects/Fauna/Swarm Fauna Config")]
    public class SwarmFaunaConfigSO : ScriptableObject
    {
        [Header("Model")]
        [Tooltip("Which simulation drives a swarm that uses this config. FIELD: designed attractor " +
                 "fields + boids - crisp, every tadpole owns a slot. GRID: the grid morphogen (research " +
                 "hgrid2) - every tadpole reads only fields at its own position; looser and more organic. " +
                 "SORT: emergent cell sorting (research sort) - each tadpole commits to one positional-" +
                 "information well of its element, unlike elements repel harder than like ones, and a surplus " +
                 "member MOLTS into a missing element (lossless). Docs/SWARM_FAUNA.md §8, §9.")]
        public SwarmModel Model = SwarmModel.Field;

        [Header("Body plans (Tools/Build/swarm_plans.py)")]
        [Tooltip("The Charge plan - the pufferfish.")] public TextAsset ChargePlan;
        [Tooltip("The Mass plan - the whale.")] public TextAsset MassPlan;
        [Tooltip("The Space plan - the jellyfish.")] public TextAsset SpacePlan;
        [Tooltip("The Time plan - the dragonfly.")] public TextAsset TimePlan;

        [Header("Member")]
        [Tooltip("The tadpole every member is: a heart, a spindle and one body prism. Must carry NO " +
                 "NetworkObject - swarm members are client-local (Docs/PartySystem/BUGS.md B16).")]
        public SwarmTadpoleFauna TadpolePrefab;

        [Header("Scale and clock")]
        [Tooltip("World units per research voxel. 2 makes the whale ~130 units long and a tadpole's " +
                 "slot spacing ~5 units - a body made of shootable pieces, not dust.")]
        [Min(0.1f)] public float UnitScale = 2f;
        [Tooltip("Simulation steps per second. The render interpolates between steps, so this is the " +
                 "BEHAVIOUR rate, not the frame rate.")]
        [Range(2f, 30f)] public float TickHz = 10f;
        [Tooltip("Steps the sim may run in one frame before it drops time (a hitch must not cascade).")]
        [Min(1)] public int MaxStepsPerFrame = 3;
        [Tooltip("CELL-WIDE CPU budget, in ms per frame, shared by EVERY swarm's simulation. The swarms are " +
                 "stepped round-robin from one scheduler until this is spent; a swarm left over waits for " +
                 "the next frame and its clock drops the time (it swims in slow motion) rather than catching " +
                 "up. That is what stops a slow frame from making the next one slower: without it every swarm " +
                 "ran MaxStepsPerFrame catch-up steps once a frame passed 1/TickHz, and the cell locked at a " +
                 "few FPS. The cell takes the largest value any live swarm authors.")]
        [Min(0.1f)] public float SimBudgetMsPerFrame = 3f;
        [Tooltip("Members of a swarm closer than this to the camera are re-posed every frame. Further " +
                 "swarms are re-posed every FarPoseInterval frames (staggered across swarms). Posing - a " +
                 "transform, a spatial-index entry and a render-entity matrix per member - is paid every " +
                 "frame for every member, so it is the swarm's other big bill beside the simulation.")]
        [Min(0f)] public float PoseEveryFrameWithin = 500f;
        [Tooltip("Frames between re-poses of a swarm beyond PoseEveryFrameWithin. 1 = every frame.")]
        [Range(1, 8)] public int FarPoseInterval = 4;

        [Header("Seed")]
        [Tooltip("Tadpoles a new swarm hatches with, at its plan's element mix, each on a slot of its " +
                 "own element - so a newborn swarm already reads as a sparse ghost of its creature.")]
        [Min(4)] public int SeedMembers = 24;

        [Header("Feeding - laying is FUNDED (mass is conserved)")]
        [Tooltip("World radius around a tadpole in which it can take a bite of a flora prism.")]
        [Min(0.5f)] public float BiteRadius = 7f;
        [Tooltip("Tadpoles that try to bite per step (round robin over the body).")]
        [Min(1)] public int BitersPerStep = 12;
        [Tooltip("An egg of element e costs this many units of eaten VOLUME of element e (x = Charge, " +
                 "y = Mass, z = Space, w = Time). Authored at the tadpole's own body-prism volume, so " +
                 "a swarm converts eaten mass to its own mass 1:1.")]
        public Vector4 EggVolume = new(40f, 40f, 40f, 40f);
        [Tooltip("An egg paid for out of ANOTHER element's food costs this many times as much - the " +
                 "feeding-ground lever: what a swarm eats shapes what it can afford to regrow.")]
        [Min(1f)] public float CrossElementCost = 2f;
        [Tooltip("Most food (in eggs) a swarm can bank. A full swarm stops grazing, so a grown body is " +
                 "not a machine that strips its feeding ground for nothing.")]
        [Min(1f)] public float StomachEggs = 24f;
        [Tooltip("Eggs per step as a share of the headcount, and at most this many per step.")]
        [Range(0f, 1f)] public float LayRate = 0.02f;
        [Min(1)] public int LayMax = 2;
        [Tooltip("Cell-wide budget of tadpole births made VISIBLE per frame, shared by every swarm. A " +
                 "laid member past it is already alive in the sim and simply hatches a frame or two later, " +
                 "so a burst of laying (or a whole cell of swarms seeding at once) never spikes a frame.")]
        [Min(1)] public int MaxSpawnsPerFrame = 48;
        [Tooltip("A WOUNDED swarm holds its eggs: every kill postpones laying by this long. This is what " +
                 "keeps a morph reachable at a fast lay rate - without it a fed swarm re-lays its majority " +
                 "faster than any ship can kill it (harness test 3c). 0 = no hold.")]
        [Min(0f)] public float KillLayHoldSeconds = 2f;

        [Header("Starvation - the only death the swarm deals itself")]
        [Tooltip("Seconds without a meal before the swarm starts shedding members (each withers to " +
                 "its crystal, leaving its body prism as a skeleton). Feeding resets the clock.")]
        [Min(1f)] public float StarvationSeconds = 90f;
        [Tooltip("Seconds between starvation sheds once starving.")]
        [Min(0.1f)] public float ShedIntervalSeconds = 4f;
        [Tooltip("Seconds a swarm with no members left lingers before its (bodiless) anchor removes " +
                 "itself so the cell's seeder can hatch a fresh one (extinction recovery).")]
        [Min(0f)] public float ExtinctLingerSeconds = 10f;

        [Header("Swimming")]
        [Tooltip("Anchor cruise in voxels per step (x UnitScale x TickHz = world units/s).")]
        [Min(0f)] public float Cruise = 0.35f;
        [Tooltip("Max heading turn in radians per step.")]
        [Min(0.001f)] public float TurnPerStep = 0.03f;
        [Tooltip("World units: a swarm with nothing to eat in sight wanders to a fresh point in its " +
                 "band at least this far away.")]
        [Min(0f)] public float WanderReach = 300f;

        [Header("Vessels")]
        [Tooltip("World radius the swarm treats a vessel as (the research's predator sphere).")]
        [Min(0.5f)] public float VesselRadius = 9f;
        [Tooltip("World units beyond the body's radius in which vessels are sensed.")]
        [Min(0f)] public float SenseMargin = 220f;
        [Tooltip("Startle at which a Charge tadpole's plate turns to DANGER (and below which it relaxes).")]
        [Range(0f, 1f)] public float DangerEnter = 0.45f;
        [Range(0f, 1f)] public float DangerExit = 0.15f;

        [Header("Hearts and body prisms (per element: x Charge, y Mass, z Space, w Time)")]
        [Tooltip("World scale of each element's heart - the canonical Tadpole species' own band values " +
                 "(Assets/_SO_Assets/Lifeforms/Tadpole Fauna *), so a swarm member pays exactly what a " +
                 "tadpole pays.")]
        public Vector4 HeartWorldScale = new(2.298f, 1.737f, 2.298f, 1.737f);
        [Tooltip("Body prism size multiplier on the research half-extents (x2, x UnitScale).")]
        [Min(0.05f)] public float PrismScale = 1f;
        [Tooltip("World gap between the heart and the body prism behind it.")]
        [Min(0f)] public float HeartPrismGap = 0.6f;
        [Tooltip("Seconds a newborn tadpole takes to grow in from nothing (continuity of existence).")]
        [Min(0.05f)] public float BirthBloomSeconds = 0.8f;
        [Tooltip("Seconds a molting heart takes to shrink away (and again to re-form as its new element).")]
        [Min(0.05f)] public float MoltHeartSeconds = 0.5f;

        [Header("Grid model (Model = Grid; research combo's values - hgrid2 made lossless - unless noted)")]
        [Tooltip("Coarse grid resolution (cells per side) and cell size in voxels. The grid rides the " +
                 "body's centre; 16 x 6 and 8 x 12 both span 96 voxels - every plan fits. 8 x 12 is the " +
                 "research's published combo (C8): the same accuracy at a fraction of the grid work " +
                 "(Docs/SWARM_FAUNA.md §10).")]
        [Range(8, 24)] public int GridSize = 8;
        [Min(1f)] public float GridCell = 12f;
        [Tooltip("Gain on a tadpole's own class DEFICIT gradient (wanted - actual density).")]
        [Min(0f)] public float GridKClass = 10f;
        [Tooltip("Gain on the all-class deficit gradient (fills the outline).")]
        [Min(0f)] public float GridKTotal = 1f;
        [Tooltip("Velocity persistence per step (the body's inertia).")]
        [Range(0f, 0.95f)] public float GridPersist = 0.6f;
        [Tooltip("Velocity noise per step (voxels).")]
        [Min(0f)] public float GridNoise = 0.05f;
        [Tooltip("Laying probability per hatched tadpole at full growth pressure (its class's relative " +
                 "deficit). The stomach is the real brake: an egg it cannot pay for is not laid.")]
        [Range(0f, 1f)] public float GridLayChance = 0.1f;
        [Tooltip("Share of eggs that take the most-wanted element instead of the parent's.")]
        [Range(0f, 1f)] public float GridCrossChance = 0.25f;
        [Tooltip("Eggs a grid swarm may lay in one step.")]
        [Min(1)] public int GridLayMaxPerStep = 3;
        [Tooltip("Gain on the FINE per-class morphogen (one tadpole's scale) and its bump width in voxels.")]
        [Min(0f)] public float GridKFine = 2f;
        [Tooltip("The fine bump width, x each plan's own mean nearest-neighbour spacing (hgrid2 `sigma_rel`). " +
                 "0 = use the fixed GridSigma below (the pre-round-5 port).")]
        [Min(0f)] public float GridSigmaRel = 1.2f;
        [Min(0.5f)] public float GridSigma = 3.5f;
        [Tooltip("MIGRANTS: a tadpole where its element is barely wanted heads, at this x its top speed, for " +
                 "the nearest site where its element is missing (hgrid2 `k_mig`). 0 = off.")]
        [Min(0f)] public float GridMigrate = 1f;
        [Tooltip("Feed-forward: the share of the nearby same-class targets' own motion a tadpole takes.")]
        [Min(0f)] public float GridFeedForward = 1.5f;
        [Tooltip("Steps per plan animation frame (x Charge, y Mass, z Space, w Time). The dragonfly's " +
                 "wings move 10 voxels a frame, so it runs at 16.")]
        public Vector4 GridFramePeriod = new(8f, 8f, 8f, 16f);
        [Tooltip("Steps a committed plan holds before the majority may switch it again (GAME: research " +
                 "ran 0). The grid commits the step a new element leads, so without a lock a near-tie " +
                 "would flicker between two animals.")]
        [Min(0)] public int GridPlanLock = 30;
        [Tooltip("The LOSSLESS corrector (research combo): a member of a surplus element re-forms its crystal " +
                 "into the element the body is most short of, so after a morph the old majority's surplus " +
                 "becomes the new body's missing parts instead of clinging to it as debris (finding 17). Off = " +
                 "the pre-round-5 grid core, which never corrected the mix.")]
        public bool GridLossless = true;
        [Tooltip("Molt clock per step of being surplus (x a per-member factor in [0.5, 1.5]): ~17-50 steps.")]
        [Range(0f, 1f)] public float GridMoltRate = 0.04f;
        [Tooltip("Steps a committed molt takes on screen (the heart shrinks away and re-forms as the new element).")]
        [Min(1)] public int GridMoltSteps = 10;
        [Tooltip("No egg while the body holds this x the plan's headcount, eggs included (combo `lay_cap`). " +
                 "A body that cannot shed surplus must not breed more of it. 0 = off.")]
        [Min(0f)] public float GridLayCap = 1f;

        [Header("Sort model (Model = Sort; research sort's published values unless noted)")]
        [Tooltip("Positional information: at most this many Gaussian wells per element, and at most one " +
                 "per this many of the plan's units of it.")]
        [Range(1, 32)] public int SortWellsPerType = 12;
        [Min(1)] public int SortUnitsPerWell = 4;
        [Tooltip("Well width, x the spread of the plan units it was fitted to.")]
        [Min(0.05f)] public float SortWellWidth = 0.89f;
        [Tooltip("Gain up a tadpole's own fated well (its log-density gradient), and the cap on that step (voxels).")]
        [Min(0f)] public float SortWellGain = 0.412f;
        [Min(0.01f)] public float SortWellClip = 0.525f;
        [Tooltip("Collision spacing (voxels) and repulsion gain.")]
        [Min(0.5f)] public float SortSpacing = 2.25f;
        [Min(0f)] public float SortRepulsion = 0.151f;
        [Tooltip("Differential adhesion: radius (voxels) and the pair coefficients - x same element, " +
                 "y same element other region, z same region other element, w neither. Negative repels. " +
                 "Unlike elements repelling HARDER than like ones is what sorts the tissues (Steinberg).")]
        [Min(0f)] public float SortAdhesionRadius = 5.38f;
        public Vector4 SortAdhesion = new(-0.05f, -0.0374f, -0.027f, -0.0637f);
        [Tooltip("Potts swaps: two touching tadpoles that would each sit better in the other's spot slide " +
                 "past one another (gain, radius in voxels). The research measured them as nearly inert.")]
        [Min(0f)] public float SortSwap = 0.709f;
        [Min(0f)] public float SortSwapRadius = 3.63f;
        [Tooltip("Velocity persistence per step.")]
        [Range(0f, 0.95f)] public float SortInertia = 0.687f;
        [Tooltip("GAME: velocity noise per step (voxels). Without it a member on a still well reads as " +
                 "frozen while the animated body moves around it (swarm_feel `stuck`). Round 6 measured it " +
                 "against the wander: 0 trims the own-plan losses and doubles coherence, 0.1 is smoother on " +
                 "every seed (0.917 vs 0.837) - kept (Docs/SWARM_FAUNA.md §12). Research: 0.")]
        [Min(0f)] public float SortNoise = 0.1f;
        [Tooltip("GAME: the share of its well's own animation a tadpole takes (feed-forward). Research: 0.")]
        [Min(0f)] public float SortFeedForward = 1f;
        [Tooltip("Steps a new majority must lead before the swarm commits to its plan.")]
        [Min(0)] public int SortDwell = 12;
        [Tooltip("The composition homeostat: eggs per step as a share of the headcount (Poisson), at most " +
                 "SortLayMax a step. The stomach is the real brake: an egg it cannot pay for is not laid.")]
        [Range(0f, 1f)] public float SortLayRate = 0.084f;
        [Min(1)] public int SortLayMax = 5;
        [Tooltip("Chance a parent whose element is full lays its body's most-needed element instead.")]
        [Range(0f, 1f)] public float SortCrossChance = 0.466f;
        [Tooltip("A parent breeds true only while its element is within this of the least-filled one.")]
        [Range(0f, 1f)] public float SortFillTolerance = 0.15f;
        [Tooltip("The body is laid to this share of the plan's headcount (research: slightly small).")]
        [Range(0.5f, 1.2f)] public float SortBodyFill = 0.939f;
        [Tooltip("Chance per step a member of a SURPLUS element begins to molt into a missing one, and the " +
                 "steps the molt takes (the heart shrinks away and re-forms as the new element).")]
        [Range(0f, 1f)] public float SortMoltRate = 0.03f;
        [Min(1)] public int SortMoltSteps = 10;
        [Tooltip("GAME: steps per plan animation frame (x Charge, y Mass, z Space, w Time) - the wells ride " +
                 "the plan's own animation. The dragonfly's wings run at 16.")]
        public Vector4 SortFramePeriod = new(8f, 8f, 8f, 16f);
        [Tooltip("ROUND 6 (research sortfeel): FLAT-BOTTOMED wells - inside this many well-sigmas a member " +
                 "feels no pull, so a tissue fills its well as a loose liquid instead of being crushed into " +
                 "flat sheets (the crystal look the lead disliked). 0 = sort's plain wells.")]
        [Min(0f)] public float SortWellDead = 0.7f;
        [Tooltip("The flat-bottom radius on the dragonfly (Time) plan only. NEGATIVE = SortWellDead. " +
                 "0 (sortfeel v2: thin wings want tight wells) measured better than the held lite config's " +
                 "-1 in both research and game mode (Docs/SWARM_FAUNA.md §12).")]
        public float SortWellDeadTime = 0f;
        [Tooltip("ROUND 6: per-member Ornstein-Uhlenbeck wander (voxels/step) - each member drifts on its " +
                 "own slow path and is turned back by its well's wall. Melts the lattice. 0 = off.")]
        [Min(0f)] public float SortWander = 0.05f;
        [Tooltip("The wander's correlation time in steps (each member +-40% of it).")]
        [Min(1f)] public float SortWanderTau = 12f;
        [Tooltip("ROUND 6 (research lite_sortfeel): only 1 member in this many re-steers each step (its " +
                 "neighbours, adhesion, swaps); the rest coast on their last velocity. Cheaper (the pair " +
                 "work falls by this factor) AND smoother - coasting eases every change in. A member a " +
                 "vessel startles re-steers every step. 1 = every member every step (sort).")]
        [Range(1, 16)] public int SortUpdateFraction = 8;

        [Header("EvoFate model (Model = EvoFate; research evofate C2 - the trained G2 rule given a fate)")]
        [Tooltip("The trained G2 network (Tools/Build/author_swarm_fauna.py writes it from the research's " +
                 "results/live/evo_rule.json). An evofate swarm without it does not hatch (fail loud). Its code " +
                 "and composition are SORT's: the Sort fields above (wells, lay homeostat, molting, frame periods) apply.")]
        public TextAsset EvoRule;
        [Tooltip("The designed fate pull toward a member's committed well (x the well step) - only outside the dead " +
                 "zone, only on the steps its network fires. 0 = the pure evolved motion (it does not sort).")]
        [Min(0f)] public float EvoPull = 2f;
        [Tooltip("Dead zone: no pull while the fated well's energy (half its Mahalanobis^2) is under this; full by " +
                 "twice it. Inside it a member is pure G2, which is what keeps the swarming texture.")]
        [Min(0f)] public float EvoDeadZone = 1.5f;
        [Tooltip("Differential adhesion, x sort's Steinberg matrix.")]
        [Min(0f)] public float EvoAdhesion = 0.35f;
        [Tooltip("Time runners: G2 speed x this (vmax 2 lets them wander out of the dragonfly's thin wings), and " +
                 "their dead zone x EvoTimeDeadZone.")]
        [Range(0f, 1.5f)] public float EvoTimeSpeed = 0.7f;
        [Range(0f, 1f)] public float EvoTimeDeadZone = 0.3f;

        [Header("Audio")]
        [Tooltip("FMOD loop the swarm plays at its body's centre. Empty = silent (the FMOD rule: an " +
                 "empty slot is a visible TODO, never a borrowed event). One per swarm, not per tadpole.")]
        public EventReference SwarmLoopEvent;
        [Tooltip("One-shot when a swarm commits to a new body plan (the morph). Empty = silent.")]
        public EventReference MorphEvent;

        /// <summary>Per-element lookup on the Vector4 convention (Charge, Mass, Space, Time).</summary>
        public static float Of(Vector4 v, Element e) => e switch
        {
            Element.Charge => v.x,
            Element.Mass => v.y,
            Element.Space => v.z,
            Element.Time => v.w,
            _ => v.y,
        };

        /// <summary>Research element index (0 Charge .. 3 Time) to the game's Element.</summary>
        public static Element ToElement(int researchIndex) => (Element)(researchIndex + 1);

        /// <summary>The game's Element to the research index (0 Charge .. 3 Time); -1 for None/Omni.</summary>
        public static int ToIndex(Element e) => e is >= Element.Charge and <= Element.Time ? (int)e - 1 : -1;
    }
}
