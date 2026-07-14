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
        [Tooltip("Attack range = (longest danger-prism dimension) x this multiplier, " +
                 "measured from the mouth (danger-prism centroid). Prey inside it breaks " +
                 "apart and suctions into the mouth. 1 = exactly the danger prism's length.")]
        [Min(0.1f)] public float attackRangeMultiplier = 1f;
        [Tooltip("Attack range fallback (world units) used when the predator has no live " +
                 "danger prisms to measure (e.g. players destroyed the mouth prisms — they " +
                 "remain fully vulnerable to normal prism destruction).")]
        [Min(0f)] public float attackRangeFallback = 15f;

        [Header("Death (wither)")]
        [Tooltip("Continuity rule — nothing pops out of existence. On death the body withers " +
                 "one spindle ring at a time, FARTHEST-from-centre first (a shark's fins / a " +
                 "brittlestar's arms evaporate before the core body), leaving the elemental " +
                 "crystal behind. Seconds between rings. 0 falls back to 0.25s so the body " +
                 "never collapses in a single frame.")]
        [Min(0f)] public float witherRingInterval = 0.25f;
    }
}