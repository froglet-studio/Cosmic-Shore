using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Game.AI
{
    [CreateAssetMenu(fileName = "PilotEvolution", menuName = "ScriptableObjects/AI/Pilot Evolution")]
    public class PilotEvolution : ScriptableObject
    {
        [Header("Population")]
        [SerializeField] int populationSize = 20;
        [SerializeField] int eliteCount = 4;

        [Header("Mutation")]
        [SerializeField, Range(0f, 1f)] float mutationRate = 0.3f;
        [SerializeField, Range(0f, 1f)] float mutationStrength = 0.2f;

        [Header("State")]
        [SerializeField] int generation;
        [SerializeField] int evaluationIndex;
        [SerializeField] List<PilotGenome> population = new();

        // Runtime tracking for multi-pilot checkout/return (not serialized)
        int _evaluationsReturned;
        bool _needsEvolution;

        public int Generation => generation;
        public int PopulationSize => population.Count;
        public PilotGenome BestGenome => population.Count > 0
            ? population.OrderByDescending(g => g.fitness).First()
            : null;

        [ContextMenu("Initialize Population")]
        public void InitializePopulation()
        {
            population.Clear();

            // Seed genome 0 with the hand-tuned defaults (our current best guess)
            population.Add(new PilotGenome());

            for (int i = 1; i < populationSize; i++)
                population.Add(PilotGenome.CreateRandom());

            generation = 0;
            evaluationIndex = 0;
            _evaluationsReturned = 0;
            _needsEvolution = false;
            MarkDirty();
        }

        /// <summary>
        /// Checks out the next genome for evaluation. Each call returns a different
        /// genome and advances the internal index, so multiple pilots can each get
        /// their own genome from the same population.
        /// If a full generation has been evaluated, evolves before serving new genomes.
        /// </summary>
        public PilotGenome CheckoutGenome(out int genomeIndex)
        {
            if (population.Count == 0) InitializePopulation();

            if (_needsEvolution)
            {
                Evolve();
                _needsEvolution = false;
                _evaluationsReturned = 0;
                evaluationIndex = 0;
            }

            genomeIndex = evaluationIndex % population.Count;
            var genome = population[genomeIndex];
            evaluationIndex++;

            Debug.Log($"[PilotEvolution] Checked out genome {genomeIndex} of {population.Count} (gen {generation})");
            MarkDirty();
            return genome;
        }

        /// <summary>
        /// Reports fitness for a specific genome by index (returned from CheckoutGenome).
        /// When enough evaluations complete, flags the population for evolution on next checkout.
        /// </summary>
        public void ReturnFitness(int genomeIndex, float fitness)
        {
            if (genomeIndex < 0 || genomeIndex >= population.Count) return;

            var genome = population[genomeIndex];
            genome.evaluationCount++;
            genome.fitness += (fitness - genome.fitness) / genome.evaluationCount;

            _evaluationsReturned++;

            Debug.Log($"[PilotEvolution] Genome {genomeIndex} fitness={genome.fitness:F2} " +
                $"(eval #{genome.evaluationCount}, {_evaluationsReturned}/{population.Count} returned)");

            if (_evaluationsReturned >= population.Count)
                _needsEvolution = true;

            MarkDirty();
        }

        /// <summary>
        /// Runs one generation: keep elites, fill rest with tournament-selected crossover + mutation.
        /// </summary>
        [ContextMenu("Evolve")]
        public void Evolve()
        {
            if (population.Count < 2) return;

            generation++;

            // Sort by fitness descending
            var sorted = population.OrderByDescending(g => g.fitness).ToList();

            var nextGen = new List<PilotGenome>();

            // Keep elites unchanged
            int elites = Mathf.Min(eliteCount, sorted.Count);
            for (int i = 0; i < elites; i++)
            {
                var elite = new PilotGenome(sorted[i]);
                elite.fitness = 0f;
                elite.evaluationCount = 0;
                nextGen.Add(elite);
            }

            // Fill rest with offspring
            while (nextGen.Count < populationSize)
            {
                var parentA = TournamentSelect(sorted);
                var parentB = TournamentSelect(sorted);
                var child = PilotGenome.Crossover(parentA, parentB);
                child.Mutate(mutationRate, mutationStrength);
                nextGen.Add(child);
            }

            population = nextGen;

            Debug.Log($"[PilotEvolution] Generation {generation} evolved. Best fitness from prev gen: {sorted[0].fitness:F2}");
            MarkDirty();
        }

        PilotGenome TournamentSelect(List<PilotGenome> sorted, int tournamentSize = 3)
        {
            PilotGenome best = null;
            for (int i = 0; i < tournamentSize; i++)
            {
                var candidate = sorted[Random.Range(0, sorted.Count)];
                if (best == null || candidate.fitness > best.fitness)
                    best = candidate;
            }
            return best;
        }

        void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Log Population")]
        void LogPopulation()
        {
            for (int i = 0; i < population.Count; i++)
            {
                var g = population[i];
                Debug.Log($"[{i}] fitness={g.fitness:F2} evals={g.evaluationCount} " +
                    $"standoff={g.skimStandoffDistance:F1} nudge={g.maxNudgeStrength:F3} " +
                    $"avoid={g.avoidanceWeight:F3} throttle={g.throttleBase:F2}");
            }
        }
    }
}
