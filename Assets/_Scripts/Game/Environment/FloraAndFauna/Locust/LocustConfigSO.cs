using UnityEngine;

namespace CosmicShore.Game.Fauna
{
    [CreateAssetMenu(
        fileName = "LocustConfig",
        menuName = "ScriptableObjects/Fauna/Locust Config")]
    public class LocustConfigSO : ScriptableObject
    {
        [Header("Swarm Behavior")]
        [Tooltip("Radius for detecting neighboring locusts for flocking.")]
        [SerializeField] float cohesionRadius = 8f;
        [Tooltip("Minimum separation distance between locusts.")]
        [SerializeField] float separationRadius = 2f;
        [Tooltip("How often each locust recalculates its flocking behavior (seconds).")]
        [SerializeField] float behaviorUpdateInterval = 0.5f;

        [Header("Behavior Weights")]
        [SerializeField] float separationWeight = 2f;
        [SerializeField] float alignmentWeight = 1f;
        [SerializeField] float cohesionWeight = 1f;
        [SerializeField] float targetWeight = 3f;

        [Header("Movement")]
        [SerializeField] float minSpeed = 3f;
        [SerializeField] float maxSpeed = 8f;
        [Tooltip("How quickly the locust turns toward its desired direction.")]
        [SerializeField] float steeringSmoothing = 3f;

        [Header("Targeting")]
        [Tooltip("How far the locust scans for open-ended trail prisms.")]
        [SerializeField] float prismDetectionRadius = 30f;
        [Tooltip("How close the locust must be to a prism to attach and begin consuming.")]
        [SerializeField] float attachDistance = 1.5f;
        [Tooltip("Maximum number of locusts that can attach to a single prism per unit of prism volume.")]
        [SerializeField] float maxLocustsPerVolumeUnit = 0.5f;
        [Tooltip("Minimum number of locusts allowed on any prism regardless of size.")]
        [SerializeField] int minLocustsPerPrism = 1;

        [Header("Consumption")]
        [Tooltip("Base shrink rate (scale units per second) at aggression 1.0.")]
        [SerializeField] float baseConsumptionRate = 0.3f;
        [Tooltip("Multiplier curve: X = aggression (0-1), Y = consumption rate multiplier.")]
        [SerializeField] AnimationCurve aggressionConsumptionCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 2f);
        [Tooltip("Seconds the prism lingers at minimum size before being destroyed.")]
        [SerializeField] float minSizeLingerSeconds = 1.5f;

        // Public accessors
        public float CohesionRadius => cohesionRadius;
        public float SeparationRadius => separationRadius;
        public float BehaviorUpdateInterval => behaviorUpdateInterval;
        public float SeparationWeight => separationWeight;
        public float AlignmentWeight => alignmentWeight;
        public float CohesionWeight => cohesionWeight;
        public float TargetWeight => targetWeight;
        public float MinSpeed => minSpeed;
        public float MaxSpeed => maxSpeed;
        public float SteeringSmoothing => steeringSmoothing;
        public float PrismDetectionRadius => prismDetectionRadius;
        public float AttachDistance => attachDistance;
        public float MaxLocustsPerVolumeUnit => maxLocustsPerVolumeUnit;
        public int MinLocustsPerPrism => minLocustsPerPrism;
        public float BaseConsumptionRate => baseConsumptionRate;
        public AnimationCurve AggressionConsumptionCurve => aggressionConsumptionCurve;
        public float MinSizeLingerSeconds => minSizeLingerSeconds;
    }
}
