using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "LightFaunaDataSO",
        menuName = "ScriptableObjects/LifeForms/FaunaPrefab/Light FaunaPrefab Data")]
    public class LightFaunaDataSO : ScriptableObject
    {
        [Header("Detection Settings")]
        [Min(0f)] public float detectionRadius = 100f;
        [Min(0f)] public float separationRadius = 100f;
        [Min(0f)] public float consumeRadius = 40f;
        [Min(0f)] public float behaviorUpdateRate = 2f;
        
        [Header("Behavior Weights")]
        [Min(0f)] public float separationWeight = 100f;
        [Min(0f)] public float goalWeight = 1.5f;
        
        [Header("Movement")]
        [Min(0f)] public float minSpeed = 3f;
        [Min(0f)] public float maxSpeed = 6f;
        [Min(0f)] public float rotationLerpSpeed = 5f;

        [Header("Intentional Feeding (herbivore)")]
        [Tooltip("A herbivore must be facing its meal within this many degrees before the " +
                 "suction (Consume) starts — it turns toward the prisms it is about to eat " +
                 "instead of vacuuming everything in radius. consumeRadius above is the " +
                 "minimum approach distance: feeding begins only once the creature has swum " +
                 "inside it (it never needs to touch the prisms).")]
        [Range(1f, 180f)] public float feedingFacingAngle = 25f;
        [Tooltip("Seconds the creature stays facing the spot it is consuming after the " +
                 "suction starts. Match the suction shader's travel time (PrismImplosion " +
                 "implosionDuration, 2s) so it holds until the prisms are entirely gone.")]
        [Min(0f)] public float consumeHoldSeconds = 2f;
        [Tooltip("One mouthful = the target prism plus edible prisms within this radius of " +
                 "it, all suctioned toward the creature. Keeps grazing throughput comparable " +
                 "to the old vacuum-in-radius behavior while reading as one deliberate bite.")]
        [Min(0f)] public float feedingClusterRadius = 12f;
        [Tooltip("Cap on prisms consumed per mouthful — bounds the implosion-VFX burst.")]
        [Min(1)] public int maxClusterBites = 8;
        [Tooltip("How sharply the creature brakes to a hover while feeding (per-second " +
                 "exponential damping of velocity). Higher = stops faster at the meal.")]
        [Min(0f)] public float feedingBrakeSharpness = 4f;

        [Header("Predation (predator)")]
        [Tooltip("Speed multiplier while actively pursuing a targeted prey fauna — the " +
                 "predator visibly chases rather than drifts.")]
        [Min(1f)] public float pursuitSpeedMultiplier = 1.5f;
        [Tooltip("Per-second homing rate: between behavior ticks the predator's velocity " +
                 "steers toward the live prey position so pursuit tracks a moving target. " +
                 "The behavior tick's full steering (separation from environment prisms — " +
                 "the obstacle avoidance) still applies each tick.")]
        [Min(0f)] public float pursuitAgility = 2.5f;
        [Tooltip("Attack range (world units) measured from the mouth (danger-prism " +
                 "centroid). Prey inside it breaks apart and suctions into the mouth. " +
                 "Default 15 ~ the shark's danger-prism length.")]
        [Min(0f)] public float attackRange = 15f;
        [Tooltip("TERRITORIAL (tiger-shark) predation: each predator claims a fixed den " +
                 "point at spawn and only hunts prey within this radius of it, patrolling " +
                 "home when its patch is empty — solitary hunters that never converge on " +
                 "the same school, so each herbivore group faces at most one predator. " +
                 "0 = non-territorial (legacy: hunts the nearest prey cell-wide).")]
        [Min(0f)] public float territoryRadius = 600f;
        [Tooltip("How far from the cell centre each predator's den point lands (random " +
                 "direction, rolled once at spawn) — spreads the solitary hunters apart.")]
        [Min(0f)] public float territoryAnchorDistance = 400f;
        [Tooltip("Predators hunt in PULSES: a hunt window opens every this many seconds " +
                 "(0 = always hunting, legacy). Outside the window the predator drops its " +
                 "target and cruises its territory — it neither pursues nor devours, so " +
                 "even prey swimming into its jaws survives. Guarantees herbivores " +
                 "grazing time between attacks. Each predator's cycle starts with a REST " +
                 "stretch at spawn (layered on the prey's own spawn immunity).")]
        [Min(0f)] public float huntIntervalSeconds = 20f;
        [Tooltip("How long each hunt window lasts (clamped to the interval). With the " +
                 "defaults 20/10 a predator alternates 10s resting / 10s hunting.")]
        [Min(0f)] public float huntDurationSeconds = 10f;

        [Header("Death (wither)")]
        [Tooltip("Continuity rule - nothing pops out of existence. On death the soft tissue withers " +
                 "one spindle ring at a time and the body prisms are left standing as a skeleton " +
                 "(Docs/ECOSYSTEM.md §26). Starvation runs FARTHEST-from-the-heart first (a shark's " +
                 "fins / a brittlestar's arms evaporate before the core body) and the crystal becomes " +
                 "collectable when the wither reaches the core; a JOUST runs the same rings in the " +
                 "opposite direction, because the jouster already took the heart. Seconds between " +
                 "rings. 0 falls back to 0.25s so the body never collapses in a single frame.")]
        [Min(0f)] public float witherRingInterval = 0.25f;
    }
}