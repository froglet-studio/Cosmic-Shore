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

        [Tooltip("MULTI-DOMAIN LINEAGES (round 9, Docs/SWARM_FAUNA.md §17; CLAUDE.md, the named swarm exception to " +
                 "'No domain asymmetry'). Off (the default, and every cell but the Swarm cell): the swarm is ONE colour - " +
                 "the cell's controlling domain - and Cell.SetModeControlOverride re-colours it. On (Sort model only): " +
                 "every SEED takes the controlling domain and every child keeps its parent's - food never colours " +
                 "anyone. A child laid into body-plan tissue no lineage holds yet (a whale's belly, before anything " +
                 "owns it) may found a new lineage (LineageDrift) in a domain drawn uniformly from those the swarm " +
                 "lacks, which then breeds true into that region. Each member is drawn, spared and credited in its " +
                 "own domain. A mode override does not re-colour a MultiDomain swarm. Other models ignore it.")]
        public bool MultiDomain = false;

        [Tooltip("Round 9: the chance that a child laid into UNOWNED body tissue founds a new lineage (a domain the " +
                 "swarm does not hold yet). Low = a body grows one colour for a while before a second appears.")]
        [Range(0f, 1f)] public float LineageDrift = 0.01f;

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
        [Tooltip("CELL-WIDE CPU budget, in ms per frame, for swarm ticks run INLINE on the main thread " +
                 "(SimulateOffMainThread off, or WebGL). Swarms are stepped round-robin until it is spent; one " +
                 "left over waits and drops the time (slow motion) rather than catching up - the round-6 rule " +
                 "that stops a slow frame making the next slower. Off-thread ticks do not draw on it.")]
        [Min(0.1f)] public float SimBudgetMsPerFrame = 3f;
        [Header("Round 7 - big bodies at ~zero main-thread cost (Docs/SWARM_FAUNA.md §14)")]
        [Tooltip("Tadpoles per plan unit. The body plan is upsampled this many times (SwarmPlanData.Upsample): " +
                 "the creature is scaled by the cube root so the DENSITY - and the spacing the sort model was " +
                 "tuned at - is unchanged. 5 turns a 192-tadpole whale into a 960-tadpole one. 1 = the plan as baked.")]
        [Range(1, 8)] public int PlanDensity = 1;
        [Tooltip("Run each swarm's tick (simulation step + the frame it draws) on a worker thread. The main " +
                 "thread then only swaps buffers. Off = run it inline on the main thread (WebGL always does).")]
        public bool SimulateOffMainThread = true;
        [Tooltip("Draw every LIVING member from one GPU buffer (SwarmMemberInstanced). Off, or on a device " +
                 "without vertex-stage structured buffers, every member gets a real GameObject again (the " +
                 "pre-round-7 cost).")]
        public bool DrawMembersOnGpu = true;
        [Tooltip("The instanced member shader (Assets/_Graphics/Materials/Graphs/SwarmMemberInstanced.shader). " +
                 "Referenced here so a player build includes it.")]
        public Shader MemberShader;
        [Tooltip("The palette the GPU-drawn members wear (the same container the prisms and crystals read).")]
        public ThemeManagerDataContainerSO Theme;
        [Tooltip("World radius around a vessel inside which a member is given a PROXY - a real GameObject " +
                 "with its heart and body prism, colliders on - so every weapon, ram, joust and skim finds " +
                 "it. Outside it a member is data and pixels: no GameObject, no collider.")]
        [Min(0f)] public float EngageRadius = 160f;
        [Tooltip("Most proxies one swarm holds at once (the nearest members to the vessels win).")]
        [Min(0)] public int MaxProxies = 160;
        [Tooltip("Seconds a proxy outlives its vessel leaving before it is given back to the simulation.")]
        [Min(0f)] public float ProxyLingerSeconds = 2f;
        [Tooltip("Round 8 (Docs/SWARM_FAUNA.md §16.2), round 11a (§19.1): most members the BULK paths may turn into " +
                 "proxies per frame, cell-wide - a blast's AOE resolve (a rocket into a 960-tadpole whale), a predator's " +
                 "prey search, a lifeform-crystal warhead's heart sweep. The spatial index asks it through " +
                 "IVirtualPrismBudget; hits past it wait in the blast's backlog for the next frame - the member keeps " +
                 "swimming until its turn, so the deaths roll through the body instead of hitching one frame. A sniper " +
                 "round and a projectile are never deferred (a handful of members each).")]
        [Min(1)] public int MaxHitMaterialisationsPerFrame = 48;
        [Header("Round 11a - one prism system (Docs/SWARM_FAUNA.md §19)")]
        [Tooltip("Draw member BODIES as ordinary prism render entities (PrismRenderService) - the platform's own " +
                 "per-domain tier materials, colours and open-with-distance spread, one Burst transform write per " +
                 "frame. Hearts stay on the instanced crystal draw either way. Off - or when the ECS render path is " +
                 "unavailable (PrismRenderService.CreateBatch declines) - the round-7 instanced body draw " +
                 "(SwarmMemberInstanced) is used instead, with no code change. Detection is the spatial index's in " +
                 "both cases.")]
        public bool UnifiedPrismBodies = true;
        [Header("Round 11f - ecology LOD (Docs/ECOLOGY_LOD.md §5)")]
        [Tooltip("Far from every pilot and unseen, the swarm COLLAPSES to its macro state: its 10 Hz worker tick " +
                 "stops, its frozen formation drifts rigidly toward its goal at cruise once per second, it keeps its " +
                 "index entries (LiveVolume and the phase ladder read the same mass) and keeps grazing at the macro " +
                 "cadence. It re-expands on the research prefetch radii (280 u, 450 u ahead) before a pilot can see " +
                 "it, or at once when hit or due to shed a starving member. Needs the GPU member draw.")]
        public bool MacroLod = true;

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
        [Tooltip("Round 9 foraging (Docs/SWARM_FAUNA.md §17.1): a swarm sets out for a plant only when its stomach " +
                 "falls below this fraction of StomachEggs. Above it, it roams its band like any sated grazer.")]
        [Range(0f, 1f)] public float ForageBelow = 0.5f;
        [Tooltip("Round 9: a foraging swarm leaves its plant once its stomach reaches this fraction (sated).")]
        [Range(0f, 1f)] public float SatedAbove = 0.9f;
        [Tooltip("Round 9: seconds at a plant without one bite taken FROM THAT PLANT before the swarm gives it up " +
                 "as grazed bare (bites elsewhere do not count).")]
        [Min(1f)] public float GiveUpSeconds = 10f;
        [Tooltip("Round 9: seconds a plant the swarm has left is passed over, so the next meal is somewhere else and " +
                 "the plant regrows.")]
        [Min(0f)] public float PlantRestSeconds = 60f;
        [Tooltip("Eggs per step as a share of the headcount, and at most this many per step.")]
        [Range(0f, 1f)] public float LayRate = 0.02f;
        [Min(1)] public int LayMax = 2;
        [Tooltip("Cell-wide budget of PROXIES (member GameObjects) created per frame, shared by every swarm. " +
                 "A member past it stays drawn by the swarm and gets its proxy a frame or two later, so a " +
                 "vessel diving into a body never spikes a frame (round 7: births themselves are free).")]
        [Min(1)] public int MaxSpawnsPerFrame = 48;
        [Tooltip("A WOUNDED swarm holds its eggs: every kill postpones laying by this long. This is what " +
                 "keeps a morph reachable at a fast lay rate - without it a fed swarm re-lays its majority " +
                 "faster than any ship can kill it (harness test 3c). 0 = no hold.")]
        [Min(0f)] public float KillLayHoldSeconds = 2f;

        [Header("Starvation - the only death the swarm deals itself")]
        [Tooltip("Seconds without a meal before a HUNGRY swarm (stomach below ForageBelow) starts shedding members " +
                 "(each withers to its crystal, leaving its body prism as a skeleton). Feeding resets the clock; a sated " +
                 "swarm never starves.")]
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

        [Header("Round 10 bestiary (SWARM_FAUNA.md §18)")]
        [Tooltip("Mass, Space and Time members strike with danger plates too. Opposing-domain pilots who hit one BURN petals.")]
        public bool Bestiary = true;
        [Tooltip("Startle at which a Time member (pack hunter) turns on a vessel; it calms below DangerExit.")]
        [Range(0f, 1f)] public float HuntEnter = 0.2f;
        [Tooltip("Seconds a pack hunter shows its startle above HuntEnter before its danger plate goes up - the wind-up a pilot " +
                 "can read. 0.4 = the Living Ecology lab's fair-burns fix (bestiary pack.py WINDUP); 0 = strike at once (round 10).")]
        [Min(0f)] public float HuntWindupSeconds = 0.4f;
        [Tooltip("Seconds a Charge pufferfish shows its startle above DangerEnter before its danger plate goes up - the same " +
                 "readable wind-up as the pack hunter's. 0.4 = the lab's fair-burns value; 0 = puff at once (round 9).")]
        [Min(0f)] public float PuffWindupSeconds = 0.4f;
        [Tooltip("A Mass member (lurker) bristles once its startle passes this and stays dangerous until it reaches DangerEnter and bolts.")]
        [Range(0f, 1f)] public float LurkCalm = 0.05f;
        [Tooltip("Seconds before the dangerous quarter of a Space (locust) cloud moves to a different quarter.")]
        [Min(0.1f)] public float LocustPhaseSeconds = 2f;

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
        [Tooltip("ROUND 11d (the post-cull jolt): the body regrows from the WOUND - an egg is budded toward the short " +
                 "well it will fill by the nearest member of its lineage, so hatchlings are born at the hole instead " +
                 "of crossing the body to it and shoving everyone on the way. Off = sort (any parent, any direction).")]
        public bool SortBudAtWound = true;
        [Tooltip("ROUND 11d: a member choosing where in its tissue to go joins the NEAREST well its type is short of " +
                 "(sort: the most-short well, wherever it is).")]
        public bool SortFateNear = true;
        [Tooltip("ROUND 11d: after a kill (once the lay hold ends) or a body switch, laying eases from one egg a step " +
                 "back to the full rate over this many seconds, instead of a flood of hatchlings the moment the hold " +
                 "lifts. 0 = the full rate at once (round 10).")]
        [Min(0f)] public float SortLayRampSeconds = 12f;

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
