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

        [Header("Withering (starvation death)")]
        [Tooltip("On starvation the creature withers from its extremity spindles inward " +
                 "instead of vanishing, leaving its core crystal behind. Seconds between " +
                 "each spindle ring collapsing — total wither time ≈ this × spindle count. " +
                 "<= 0 falls back to a sensible default (0.25s). See Docs/ECOSYSTEM.md.")]
        [Min(0f)] public float witherRingInterval = 0.25f;
        [Tooltip("Leave the core crystal behind when a starved creature withers (the mass " +
                 "recycle). The cell bounds how many such crystals persist. Turn OFF to make " +
                 "withering simply despawn — useful for isolating crystal-accumulation perf.")]
        public bool leaveCrystalOnWither = true;
    }
}