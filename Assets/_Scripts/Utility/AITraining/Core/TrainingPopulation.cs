using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Steady-state evolutionary search over TrainingGenome.
    ///
    /// Maintains a fixed-size population, hands genomes out for evaluation, and
    /// re-evolves once every genome has been scored. Elites are preserved
    /// unchanged; the rest are produced by tournament-selected crossover plus
    /// numeric and structural mutation.
    ///
    /// A novelty archive runs in parallel: every evaluated genome is hashed by
    /// its module bits and quantized parameter buckets, and that hash is used to
    /// reward genomes whose behavior fingerprint is rare. This is what
    /// stops the search from collapsing onto a single local optimum during long
    /// overnight runs.
    ///
    /// State is fully serializable so the whole population survives editor domain
    /// reloads and machine restarts.
    /// </summary>
    [Serializable]
    public class TrainingPopulation
    {
        [SerializeField] int populationSize = 24;
        [SerializeField] int eliteCount = 4;
        [SerializeField, Range(0f, 1f)] float numericMutationRate = 0.3f;
        [SerializeField, Range(0f, 1f)] float numericMutationStrength = 0.18f;
        [SerializeField, Range(0f, 1f)] float structuralMutationRate = 0.04f;
        [SerializeField, Range(0f, 1f)] float noveltyWeight = 0.15f;
        [SerializeField] int tournamentSize = 3;

        [SerializeField] int generation;
        [SerializeField] int evaluationsThisGen;
        [SerializeField] int nextCheckoutIndex;
        [SerializeField] List<TrainingGenome> population = new();
        [SerializeField] List<int> behaviorArchive = new(); // recent BehaviorHash values

        const int NoveltyArchiveCap = 256;
        const int NoveltyKNearest = 10;

        public int Generation => generation;
        public int PopulationSize => population.Count;
        public IReadOnlyList<TrainingGenome> Population => population;

        public int ConfiguredSize { get => populationSize; set => populationSize = Mathf.Max(2, value); }
        public int EliteCount { get => eliteCount; set => eliteCount = Mathf.Clamp(value, 0, populationSize); }
        public float NumericMutationRate { get => numericMutationRate; set => numericMutationRate = Mathf.Clamp01(value); }
        public float NumericMutationStrength { get => numericMutationStrength; set => numericMutationStrength = Mathf.Clamp01(value); }
        public float StructuralMutationRate { get => structuralMutationRate; set => structuralMutationRate = Mathf.Clamp01(value); }
        public float NoveltyWeight { get => noveltyWeight; set => noveltyWeight = Mathf.Clamp01(value); }

        public TrainingGenome Best => population.Count == 0
            ? null
            : population.Aggregate((a, b) => CompositeScore(a) > CompositeScore(b) ? a : b);

        public void Initialize(TrainingGenome seed = null)
        {
            population.Clear();
            behaviorArchive.Clear();
            generation = 0;
            evaluationsThisGen = 0;
            nextCheckoutIndex = 0;

            if (seed != null)
                population.Add(seed.Clone());

            for (int i = population.Count; i < populationSize; i++)
                population.Add(TrainingGenome.Random());
        }

        /// <summary>
        /// Hands the next genome out for evaluation in round-robin order. Once all
        /// genomes have been scored, evolves before serving any more.
        /// </summary>
        public TrainingGenome Checkout(out int populationIndex)
        {
            if (population.Count == 0) Initialize();

            if (evaluationsThisGen >= population.Count)
            {
                Evolve();
                evaluationsThisGen = 0;
                nextCheckoutIndex = 0;
            }

            populationIndex = nextCheckoutIndex % population.Count;
            nextCheckoutIndex++;
            return population[populationIndex];
        }

        /// <summary>
        /// Records fitness for a genome and updates the novelty archive. Once every
        /// genome in the current generation has reported, the population becomes
        /// eligible for evolution on the next Checkout.
        /// </summary>
        public void ReturnFitness(int populationIndex, TrainingFitness fitness, TrainingGenome evaluated)
        {
            if (populationIndex < 0 || populationIndex >= population.Count) return;

            var genome = population[populationIndex];
            genome.EvaluationCount++;
            // Running mean — robust to noisy fitness landscapes (random spawns, AI opponents)
            genome.Fitness += (fitness.Total - genome.Fitness) / Mathf.Max(1, genome.EvaluationCount);

            int hash = (evaluated ?? genome).BehaviorHash();
            genome.NoveltyScore = ComputeNovelty(hash);
            behaviorArchive.Add(hash);
            if (behaviorArchive.Count > NoveltyArchiveCap)
                behaviorArchive.RemoveAt(0);

            evaluationsThisGen++;
        }

        /// <summary>
        /// Composite score used for selection: fitness plus a fraction of novelty.
        /// Novelty is the average distance to the K nearest neighbors in the archive,
        /// rescaled so its dynamic range is comparable to fitness.
        /// </summary>
        public float CompositeScore(TrainingGenome g)
        {
            return g.Fitness + g.NoveltyScore * noveltyWeight;
        }

        public void Evolve()
        {
            if (population.Count < 2) return;

            generation++;

            var sorted = population
                .OrderByDescending(CompositeScore)
                .ToList();

            var next = new List<TrainingGenome>(populationSize);

            int elites = Mathf.Min(eliteCount, sorted.Count);
            for (int i = 0; i < elites; i++)
            {
                var elite = sorted[i].Clone();
                elite.Fitness = sorted[i].Fitness; // preserve rolled mean for hall-of-fame display
                elite.EvaluationCount = sorted[i].EvaluationCount;
                elite.GenerationBorn = sorted[i].GenerationBorn;
                next.Add(elite);
            }

            while (next.Count < populationSize)
            {
                var a = TournamentSelect(sorted);
                var b = TournamentSelect(sorted);
                var child = TrainingGenome.Crossover(a, b);
                child.Mutate(numericMutationRate, numericMutationStrength, structuralMutationRate);
                child.GenerationBorn = generation;
                next.Add(child);
            }

            // Re-zero fitness for non-elite members so a single lucky early eval doesn't
            // dominate the rolled mean for the rest of the run.
            for (int i = elites; i < next.Count; i++)
            {
                next[i].Fitness = 0f;
                next[i].EvaluationCount = 0;
            }

            population = next;
        }

        TrainingGenome TournamentSelect(List<TrainingGenome> sorted)
        {
            TrainingGenome winner = null;
            for (int i = 0; i < tournamentSize; i++)
            {
                var candidate = sorted[UnityEngine.Random.Range(0, sorted.Count)];
                if (winner == null || CompositeScore(candidate) > CompositeScore(winner))
                    winner = candidate;
            }
            return winner;
        }

        float ComputeNovelty(int hash)
        {
            if (behaviorArchive.Count == 0) return 1f;

            // Hamming-style distance over the 32-bit hash; cheap proxy for behavioral distance.
            int sum = 0;
            int taken = 0;
            for (int i = behaviorArchive.Count - 1; i >= 0 && taken < NoveltyKNearest; i--, taken++)
                sum += BitDistance(hash, behaviorArchive[i]);

            float avgBits = (float)sum / Mathf.Max(1, taken);
            return avgBits;  // 0..32; weight is applied separately so callers can tune magnitude
        }

        static int BitDistance(int a, int b)
        {
            int diff = a ^ b;
            int count = 0;
            while (diff != 0)
            {
                count += diff & 1;
                diff = (int)((uint)diff >> 1);
            }
            return count;
        }

        public IEnumerable<TrainingGenome> GetTopN(int n)
        {
            return population.OrderByDescending(CompositeScore).Take(n);
        }
    }
}
