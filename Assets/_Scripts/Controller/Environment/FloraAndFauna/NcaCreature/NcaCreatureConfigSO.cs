using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number an NCA creature runs on (Docs/NCA_CREATURES.md): which trained network grows its body, how big a
    /// voxel is in the world, how fast the network steps, how it swims, eats, is wounded and heals, and how it dies.
    /// One asset per species; authored by Tools/Build/author_nca_creatures.py - hand edits are drift.
    ///
    /// Units: the network runs in VOXELS and STEPS. <see cref="VoxelSize"/> converts a voxel to world units and
    /// <see cref="StepsPerSecond"/> converts steps to seconds (the lizard's swim stroke is ~68 steps).
    /// </summary>
    [CreateAssetMenu(fileName = "NcaCreatureConfig", menuName = "ScriptableObjects/Fauna/NCA Creature Config")]
    public class NcaCreatureConfigSO : ScriptableObject
    {
        [Header("Body")]
        [Tooltip("The trained network and body frame (<Species>NcaWeights.json, written by author_nca_creatures.py " +
                 "from the research export). The same runtime grows any species - only this file differs.")]
        public TextAsset Weights;

        [Tooltip("World units per voxel. The lizard's grid is 44 voxels long.")]
        public float VoxelSize = 1.2f;

        [Tooltip("Drawn prism shape per surface voxel (x across, y along the skin normal, z along the body), in voxels " +
                 "- the lab renderer's scales, so the skin reads as overlapping scales.")]
        public Vector3 ScaleShape = new(1.35f, 0.6f, 1.55f);

        [Tooltip("Alpha above which a voxel is BODY (counted, hittable, drawn full size). Fainter voxels are drawn " +
                 "small - the growing/healing fringe.")]
        public float AlphaThreshold = 0.3f;

        [Tooltip("Most voxels one creature may draw at once (its prism entity pool).")]
        public int MaxShown = 1600;

        [Tooltip("The theme whose per-domain BLOCK material the body wears - the same material a live prism of the " +
                 "creature's domain wears, so it colours, opens and animates like one.")]
        public ThemeManagerDataContainerSO Theme;

        [Header("Network stepping")]
        [Tooltip("Network steps per second while cruising (on a worker thread). The step self-limits: a step is only " +
                 "started when the previous one has finished.")]
        public float StepsPerSecond = 20f;

        [Tooltip("Network steps per second while bolting from a vessel - the stroke quickens.")]
        public float BoltStepsPerSecond = 30f;

        [Tooltip("Steps per second where there is no worker thread (WebGL): the step runs inline on the main thread.")]
        public float InlineStepsPerSecond = 8f;

        [Tooltip("Body segments along the long axis: each is one spatial-index entry (what AOE, hitscan and " +
                 "projectiles find) and one place a hit can land.")]
        [Min(1)] public int Segments = 8;

        [Header("Swimming")]
        [Tooltip("Cruise speed, world units per second.")]
        public float SwimSpeed = 14f;

        [Tooltip("Bolt speed, world units per second.")]
        public float BoltSpeed = 38f;

        [Tooltip("Turn rate, degrees per second.")]
        public float TurnDegreesPerSecond = 40f;

        [Tooltip("Within this distance of its goal the creature slows (to a quarter at the goal) so it can graze.")]
        public float SlowRadius = 40f;

        [Tooltip("Share of the speed carried by the stroke: 0 = constant speed, 1 = all thrust comes from the tail " +
                 "beat (the network's own bend rate).")]
        [Range(0f, 1f)] public float StrokeThrust = 0.4f;

        [Tooltip("Bend change per step (voxels) that counts as a full-power stroke.")]
        public float FullStrokeBendRate = 0.35f;

        [Header("Vessels")]
        [Tooltip("How far vessels are sensed (bolt trigger and fly-through bites).")]
        public float VesselSenseRadius = 120f;

        [Tooltip("A vessel closing faster than this (units/s) inside the sense radius makes the creature bolt.")]
        public float BoltClosingSpeed = 25f;

        [Tooltip("How long a bolt lasts, seconds.")]
        public float BoltSeconds = 3f;

        [Tooltip("A vessel of another domain passing within this many voxels of the body takes a bite.")]
        public float VesselBiteReachVoxels = 1.5f;

        [Tooltip("Seconds before the same vessel can bite again.")]
        public float VesselBiteCooldown = 0.8f;

        [Header("Wounds")]
        [Tooltip("Radius of the hole one hit cuts, voxels.")]
        public float BiteRadiusVoxels = 3.5f;

        [Tooltip("Seconds an unexploded hit prism stands in for its segment before it is retired.")]
        public float HitPrismSeconds = 2f;

        [Tooltip("The transient body prism a weapon hit materialises at a segment (HealthBlock). One per creature " +
                 "at a time - the collider budget.")]
        public HealthPrism HitPrismPrefab;

        [Tooltip("World scale of the hit prism.")]
        public Vector3 HitPrismScale = new(4f, 4f, 4f);

        [Header("Mass")]
        [Tooltip("The whole body's volume (what the cell counts, what the scars owe). author_nca_creatures.py writes " +
                 "GrownVoxels x VoxelSize^3.")]
        public float BodyVolume = 1975f;

        [Tooltip("Share of each meal that pays toward open wounds; the rest feeds the stomach (and breeding).")]
        [Range(0f, 1f)] public float HealShareOfMeal = 0.5f;

        [Header("Feeding")]
        [Tooltip("Mouth reach ahead of the head's front, world units.")]
        public float MouthReach = 2f;

        [Tooltip("Radius the mouth gathers edible flora prisms from, world units.")]
        public float MouthRadius = 9f;

        [Tooltip("Seconds between mouthfuls.")]
        public float FeedInterval = 1.2f;

        [Tooltip("Most prisms one mouthful takes.")]
        [Min(1)] public int PrismsPerMouthful = 3;

        [Header("Life and death")]
        [Tooltip("Body voxels (share of GrownVoxels) that count as grown - before this the creature is hatching and " +
                 "cannot die of its wounds.")]
        [Range(0f, 1f)] public float MatureFraction = 0.8f;

        [Tooltip("A grown creature whose body falls under this share of GrownVoxels dies of its wounds.")]
        [Range(0f, 1f)] public float DeathFraction = 0.35f;

        [Tooltip("The creature's heart, world scale (Docs/ECOSYSTEM.md §40: K x bodyLength^0.5, written by " +
                 "author_nca_creatures.py with the K author_lifeform_heart_sizes.py solves).")]
        public float HeartWorldScale = 2.9f;

        [Tooltip("How far behind the head's front the heart sits, voxels (it must sit wholly behind the front plane).")]
        public float HeartSeatDepthVoxels = 4f;

        [Tooltip("Seconds a withering body takes to come apart around its heart.")]
        public float WitherSeconds = 3f;

        [Tooltip("Seconds a devoured body takes to be pulled into the eater.")]
        public float DevourSeconds = 1.2f;
    }
}
