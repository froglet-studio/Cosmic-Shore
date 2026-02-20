using System;
using UnityEngine;

namespace CosmicShore.Game.AI
{
    [Serializable]
    public class PilotGenome
    {
        // --- Prism skimming ---
        [Range(20f, 200f)]  public float prismDetectionRadius = 120f;
        [Range(4f, 30f)]    public float skimStandoffDistance = 12f;
        [Range(10f, 60f)]   public float minPrismScanDistance = 20f;
        [Range(0.01f, 0.3f)] public float maxNudgeStrength = 0.15f;
        [Range(0.3f, 0.95f)] public float dotCrystalThreshold = 0.5f;

        // --- Collision avoidance ---
        [Range(10f, 80f)]   public float collisionAvoidanceDistance = 30f;
        [Range(0.02f, 0.4f)] public float avoidanceWeight = 0.15f;

        // --- Steering ---
        [Range(20f, 200f)]  public float steeringAggressiveness = 100f;

        // --- Throttle ---
        [Range(0.3f, 1f)]   public float throttleBase = 0.6f;
        [Range(0f, 0.01f)]  public float throttleRampRate = 0.001f;

        // --- Fitness (set after evaluation) ---
        [HideInInspector] public float fitness;
        [HideInInspector] public int evaluationCount;

        public PilotGenome() { }

        public PilotGenome(PilotGenome other)
        {
            prismDetectionRadius = other.prismDetectionRadius;
            skimStandoffDistance = other.skimStandoffDistance;
            minPrismScanDistance = other.minPrismScanDistance;
            maxNudgeStrength = other.maxNudgeStrength;
            dotCrystalThreshold = other.dotCrystalThreshold;
            collisionAvoidanceDistance = other.collisionAvoidanceDistance;
            avoidanceWeight = other.avoidanceWeight;
            steeringAggressiveness = other.steeringAggressiveness;
            throttleBase = other.throttleBase;
            throttleRampRate = other.throttleRampRate;
            fitness = 0f;
            evaluationCount = 0;
        }

        public static PilotGenome CreateRandom()
        {
            return new PilotGenome
            {
                prismDetectionRadius = UnityEngine.Random.Range(20f, 200f),
                skimStandoffDistance = UnityEngine.Random.Range(4f, 30f),
                minPrismScanDistance = UnityEngine.Random.Range(10f, 60f),
                maxNudgeStrength = UnityEngine.Random.Range(0.01f, 0.3f),
                dotCrystalThreshold = UnityEngine.Random.Range(0.3f, 0.95f),
                collisionAvoidanceDistance = UnityEngine.Random.Range(10f, 80f),
                avoidanceWeight = UnityEngine.Random.Range(0.02f, 0.4f),
                steeringAggressiveness = UnityEngine.Random.Range(20f, 200f),
                throttleBase = UnityEngine.Random.Range(0.3f, 1f),
                throttleRampRate = UnityEngine.Random.Range(0f, 0.01f),
                fitness = 0f,
                evaluationCount = 0
            };
        }

        public static PilotGenome Crossover(PilotGenome a, PilotGenome b)
        {
            var child = new PilotGenome();
            child.prismDetectionRadius      = Pick(a.prismDetectionRadius, b.prismDetectionRadius);
            child.skimStandoffDistance       = Pick(a.skimStandoffDistance, b.skimStandoffDistance);
            child.minPrismScanDistance       = Pick(a.minPrismScanDistance, b.minPrismScanDistance);
            child.maxNudgeStrength           = Pick(a.maxNudgeStrength, b.maxNudgeStrength);
            child.dotCrystalThreshold        = Pick(a.dotCrystalThreshold, b.dotCrystalThreshold);
            child.collisionAvoidanceDistance = Pick(a.collisionAvoidanceDistance, b.collisionAvoidanceDistance);
            child.avoidanceWeight            = Pick(a.avoidanceWeight, b.avoidanceWeight);
            child.steeringAggressiveness     = Pick(a.steeringAggressiveness, b.steeringAggressiveness);
            child.throttleBase               = Pick(a.throttleBase, b.throttleBase);
            child.throttleRampRate           = Pick(a.throttleRampRate, b.throttleRampRate);
            return child;
        }

        public void Mutate(float rate, float strength)
        {
            prismDetectionRadius       = MutateGene(prismDetectionRadius, rate, strength, 20f, 200f);
            skimStandoffDistance        = MutateGene(skimStandoffDistance, rate, strength, 4f, 30f);
            minPrismScanDistance        = MutateGene(minPrismScanDistance, rate, strength, 10f, 60f);
            maxNudgeStrength            = MutateGene(maxNudgeStrength, rate, strength, 0.01f, 0.3f);
            dotCrystalThreshold         = MutateGene(dotCrystalThreshold, rate, strength, 0.3f, 0.95f);
            collisionAvoidanceDistance  = MutateGene(collisionAvoidanceDistance, rate, strength, 10f, 80f);
            avoidanceWeight             = MutateGene(avoidanceWeight, rate, strength, 0.02f, 0.4f);
            steeringAggressiveness      = MutateGene(steeringAggressiveness, rate, strength, 20f, 200f);
            throttleBase                = MutateGene(throttleBase, rate, strength, 0.3f, 1f);
            throttleRampRate            = MutateGene(throttleRampRate, rate, strength, 0f, 0.01f);
        }

        static float Pick(float a, float b) => UnityEngine.Random.value < 0.5f ? a : b;

        static float MutateGene(float value, float rate, float strength, float min, float max)
        {
            if (UnityEngine.Random.value > rate) return value;
            float range = (max - min) * strength;
            return Mathf.Clamp(value + UnityEngine.Random.Range(-range, range), min, max);
        }
    }
}
