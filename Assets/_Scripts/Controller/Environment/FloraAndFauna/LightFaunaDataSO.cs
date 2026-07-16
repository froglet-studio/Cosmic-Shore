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

        [Header("Death (wither)")]
        [Tooltip("Continuity rule - nothing pops out of existence. On death the body withers " +
                 "one spindle ring at a time, FARTHEST-from-centre first (a shark's fins / a " +
                 "brittlestar's arms evaporate before the core body), leaving the elemental " +
                 "crystal behind. Seconds between rings. 0 falls back to 0.25s so the body " +
                 "never collapses in a single frame.")]
        [Min(0f)] public float witherRingInterval = 0.25f;
    }
}