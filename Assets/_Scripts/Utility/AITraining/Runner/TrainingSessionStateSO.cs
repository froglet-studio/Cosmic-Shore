using System;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Survives editor domain reloads and machine restarts. The runner reads/writes
    /// this every generation; if Unity crashes or the machine reboots, picking up
    /// where we left off is a one-line operation: assign the asset and press Play.
    ///
    /// The population itself is serialized (not a JSON sidecar) so all the numeric
    /// values, lineage strings, and the novelty archive ride along.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrainingSessionState",
        menuName = "ScriptableObjects/AI Training/Session State",
        order = 202)]
    public class TrainingSessionStateSO : ScriptableObject
    {
        [Header("Identity")]
        public string ScenarioKey;       // matches TrainingScenarioSO.Key
        public string LastWriteUtc;
        public int EpisodesCompleted;
        public int EpisodesRequested;

        [Header("Population")]
        public TrainingPopulation Population = new();

        [Header("Best Found")]
        public TrainingGenome HallOfFameBest;
        public float HallOfFameBestFitness;

        [Header("Recent Fitness History (for charts)")]
        public RingBuffer FitnessHistory = new(256);

        [Serializable]
        public class RingBuffer
        {
            public int Capacity;
            public int Head;
            public int Count;
            public float[] Values;
            public int[] Generations;

            public RingBuffer(int capacity)
            {
                Capacity = Mathf.Max(1, capacity);
                Values = new float[Capacity];
                Generations = new int[Capacity];
            }

            public void Push(float value, int generation)
            {
                if (Values == null || Values.Length != Capacity)
                {
                    Values = new float[Capacity];
                    Generations = new int[Capacity];
                    Head = 0; Count = 0;
                }
                Values[Head] = value;
                Generations[Head] = generation;
                Head = (Head + 1) % Capacity;
                if (Count < Capacity) Count++;
            }
        }

        /// <summary>
        /// Unity calls this at asset creation. Initialises the population so the
        /// asset is usable without anyone having to right-click → Initialise.
        /// </summary>
        void Reset()
        {
            ResetForScenario(scenarioKey: "", scenario: null);
        }

        /// <summary>
        /// Wipes run state and (optionally) re-applies population settings from a
        /// scenario. Called by the runner whenever it sees the state has drifted
        /// out of sync with the scenario it's about to train.
        /// </summary>
        public void ResetForScenario(string scenarioKey, TrainingScenarioSO scenario)
        {
            ScenarioKey = scenarioKey;
            EpisodesCompleted = 0;
            EpisodesRequested = 0;
            HallOfFameBest = null;
            HallOfFameBestFitness = float.NegativeInfinity;
            FitnessHistory = new RingBuffer(256);

            Population = new TrainingPopulation();
            if (scenario != null)
            {
                Population.ConfiguredSize = scenario.PopulationSize;
                Population.EliteCount = scenario.EliteCount;
                Population.NumericMutationRate = scenario.NumericMutationRate;
                Population.NumericMutationStrength = scenario.NumericMutationStrength;
                Population.StructuralMutationRate = scenario.StructuralMutationRate;
                Population.NoveltyWeight = scenario.NoveltyWeight;
            }
            Population.Initialize(TrainingGenome.FromRegistryDefaults());
            LastWriteUtc = DateTime.UtcNow.ToString("o");
        }

        public void RecordEpisode(TrainingFitness fitness, TrainingGenome justEvaluated)
        {
            EpisodesCompleted++;
            FitnessHistory.Push(fitness.Total, Population.Generation);
            if (justEvaluated != null && justEvaluated.Fitness > HallOfFameBestFitness)
            {
                HallOfFameBest = justEvaluated.Clone();
                HallOfFameBestFitness = justEvaluated.Fitness;
            }
            LastWriteUtc = DateTime.UtcNow.ToString("o");
        }
    }
}
