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
            MarkDirty();
        }

        /// <summary>
        /// Returns the next genome to evaluate. Cycles through the population.
        /// </summary>
        public PilotGenome GetNextGenome()
        {
            if (population.Count == 0) InitializePopulation();

            var genome = population[evaluationIndex % population.Count];
            Debug.Log($"[PilotEvolution] Serving genome {evaluationIndex % population.Count} of {population.Count} (gen {generation})");
            return genome;
        }

        /// <summary>
        /// Advances to the next genome in the evaluation queue.
        /// </summary>
        public void AdvanceEvaluation()
        {
            evaluationIndex++;
            Debug.Log($"[PilotEvolution] Advanced to eval index {evaluationIndex}/{population.Count}");
            if (evaluationIndex >= population.Count)
            {
                Evolve();
                evaluationIndex = 0;
            }
            MarkDirty();
        }

        /// <summary>
        /// Records fitness for the genome currently being evaluated.
        /// Uses running average if evaluated multiple times.
        /// </summary>
        public void ReportFitness(float fitness)
        {
            if (population.Count == 0) return;
            var genome = population[evaluationIndex % population.Count];
            genome.evaluationCount++;
            // Running average
            genome.fitness += (fitness - genome.fitness) / genome.evaluationCount;
            Debug.Log($"[PilotEvolution] Genome {evaluationIndex % population.Count} fitness={genome.fitness:F2} (eval #{genome.evaluationCount})");
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
