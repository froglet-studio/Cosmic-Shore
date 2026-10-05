using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// The deployable end product of training. Stores the best-known genome for
    /// each (vessel × game mode × intensity) bucket. Intensity 4 entries are the
    /// "flawless" pilots; lower intensities are normally produced by the
    /// IntensityDitherer at runtime, but explicit entries here override that
    /// (useful when an intensity has its own trained pilot rather than a dither).
    ///
    /// This SO is what the existing AIPilot reads from at deployment time, so
    /// shipping a new AI is "drop in a new TrainingArchive asset, point AIPilot
    /// at it, hit play".
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrainingArchive",
        menuName = "ScriptableObjects/AI Training/Archive",
        order = 203)]
    public class TrainingArchiveSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public VesselClassType Vessel;
            public GameModes GameMode;
            [Range(1, 4)] public int Intensity = 4;
            public TrainingGenome Genome;
            public float Fitness;
            public int Generation;
            public string TrainedUtc;
            public string Notes;

            public string Key => MakeKey(Vessel, GameMode, Intensity);
        }

        public List<Entry> Entries = new();

        public static string MakeKey(VesselClassType vessel, GameModes mode, int intensity)
            => $"{vessel}_{mode}_I{intensity}";

        public Entry Find(VesselClassType vessel, GameModes mode, int intensity)
        {
            string key = MakeKey(vessel, mode, intensity);
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].Key == key) return Entries[i];
            return null;
        }

        /// <summary>
        /// Returns the closest available genome for the requested combination.
        /// Falls back from (intensity) -> (intensity 4 same vessel/mode) -> any
        /// trained genome on that mode -> any trained genome on that vessel ->
        /// registry defaults. Used by AIPilot when an exact entry doesn't exist
        /// yet (e.g. partway through an overnight run).
        /// </summary>
        public TrainingGenome FindBestAvailable(VesselClassType vessel, GameModes mode, int intensity, out int matchScore)
        {
            // Exact
            var exact = Find(vessel, mode, intensity);
            if (exact?.Genome != null) { matchScore = 4; return exact.Genome; }

            // Same vessel + mode, any intensity (prefer 4)
            Entry bestMatch = null;
            int bestPriority = -1;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e.Genome == null) continue;
                int p = 0;
                if (e.Vessel == vessel) p += 4;
                if (e.GameMode == mode) p += 2;
                if (e.Intensity == 4) p += 1;
                if (p > bestPriority) { bestPriority = p; bestMatch = e; }
            }
            if (bestMatch != null) { matchScore = bestPriority; return bestMatch.Genome; }

            matchScore = 0;
            return TrainingGenome.FromRegistryDefaults();
        }

        public void Upsert(VesselClassType vessel, GameModes mode, int intensity, TrainingGenome genome, float fitness, int generation, string notes = "")
        {
            var existing = Find(vessel, mode, intensity);
            if (existing == null)
            {
                Entries.Add(new Entry
                {
                    Vessel = vessel,
                    GameMode = mode,
                    Intensity = intensity,
                    Genome = genome.Clone(),
                    Fitness = fitness,
                    Generation = generation,
                    TrainedUtc = DateTime.UtcNow.ToString("o"),
                    Notes = notes
                });
                return;
            }

            existing.Genome = genome.Clone();
            existing.Fitness = fitness;
            existing.Generation = generation;
            existing.TrainedUtc = DateTime.UtcNow.ToString("o");
            if (!string.IsNullOrEmpty(notes)) existing.Notes = notes;
        }
    }
}
