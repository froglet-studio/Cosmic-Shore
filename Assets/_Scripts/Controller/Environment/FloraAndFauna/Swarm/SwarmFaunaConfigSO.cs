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
                 "Docs/SWARM_FAUNA.md §8.")]
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
        [Range(0f, 0.2f)] public float LayRate = 0.02f;
        [Min(1)] public int LayMax = 2;

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

        [Header("Grid model (Model = Grid; research hgrid2's values unless noted)")]
        [Tooltip("Coarse grid resolution (cells per side) and cell size in voxels. The grid rides the " +
                 "body's centre; 16 x 6 spans 96 voxels - every plan fits.")]
        [Range(8, 24)] public int GridSize = 16;
        [Min(1f)] public float GridCell = 6f;
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
        [Min(0.5f)] public float GridSigma = 3.5f;
        [Tooltip("Feed-forward: the share of the nearby same-class targets' own motion a tadpole takes.")]
        [Min(0f)] public float GridFeedForward = 1.5f;
        [Tooltip("Steps per plan animation frame (x Charge, y Mass, z Space, w Time). The dragonfly's " +
                 "wings move 10 voxels a frame, so it runs at 16.")]
        public Vector4 GridFramePeriod = new(8f, 8f, 8f, 16f);
        [Tooltip("Steps a committed plan holds before the majority may switch it again (GAME: research " +
                 "ran 0). The grid commits the step a new element leads, so without a lock a near-tie " +
                 "would flicker between two animals.")]
        [Min(0)] public int GridPlanLock = 30;

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
