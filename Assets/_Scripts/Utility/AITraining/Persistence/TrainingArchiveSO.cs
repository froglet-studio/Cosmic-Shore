using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// The deployable end product of training. For each (vessel × game mode ×
    /// intensity) bucket it stores:
    ///
    ///   Genome — the single best genome found (the hall-of-fame champion), and
    ///   Roster — up to RosterCapacity behaviorally DISTINCT strong genomes.
    ///
    /// The roster is what makes deployed AI replayable: instead of every match
    /// fielding the same champion, deployment samples a personality per AI per
    /// match (SampleRoster). Distinctness is enforced by TrainingGenome
    /// .BehaviorHash, so the roster holds a Rammer and a Drifter and a Cruiser
    /// rather than four near-clones of the champion; each carries a derived
    /// personality name (PilotTuningGenes.PersonalityName) so match logs can say
    /// who showed up.
    ///
    /// Intensity 4 entries are the flawless pilots; lower intensities are
    /// normally produced by intensity dithering at runtime, with explicit
    /// lower-intensity entries overriding the dither when authored.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TrainingArchive",
        menuName = "ScriptableObjects/AI Training/Archive",
        order = 203)]
    public class TrainingArchiveSO : ScriptableObject
    {
        public const int RosterCapacity = 4;

        /// <summary>
        /// Roster admission: a genome must score at least this fraction of the
        /// champion's fitness to be kept. Keeps the roster "strong and varied",
        /// never "varied and bad".
        /// </summary>
        public const float RosterFitnessFloor = 0.6f;

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
            public List<TrainingGenome> Roster = new();

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
        /// Returns the closest available genome for the requested combination,
        /// preferring exact matches, then same vessel/mode at other intensities,
        /// then anything trained. matchScore 4 = exact.
        /// </summary>
        public TrainingGenome FindBestAvailable(VesselClassType vessel, GameModes mode, int intensity, out int matchScore)
        {
            var exact = Find(vessel, mode, intensity);
            if (exact?.Genome != null) { matchScore = 4; return exact.Genome; }

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

        /// <summary>
        /// Samples a personality from the bucket's roster (champion included),
        /// uniformly at random. Falls back through the same chain as
        /// FindBestAvailable when the bucket is empty. This is the deployment
        /// entry point for "who does the player meet tonight?".
        /// </summary>
        public TrainingGenome SampleRoster(VesselClassType vessel, GameModes mode, int intensity, out string personality)
        {
            var entry = Find(vessel, mode, intensity);
            if (entry?.Genome != null)
            {
                int pool = 1 + (entry.Roster?.Count ?? 0);
                int pick = UnityEngine.Random.Range(0, pool);
                var genome = pick == 0 ? entry.Genome : entry.Roster[pick - 1];
                personality = PilotTuningGenes.PersonalityName(genome);
                return genome;
            }

            var fallback = FindBestAvailable(vessel, mode, intensity, out _);
            personality = PilotTuningGenes.PersonalityName(fallback);
            return fallback;
        }

        /// <summary>
        /// Records a trained genome. Champions replace the Genome slot; strong,
        /// behaviorally distinct genomes are folded into the roster; near-clones
        /// of an existing roster member replace it only when fitter.
        /// </summary>
        public void Upsert(VesselClassType vessel, GameModes mode, int intensity, TrainingGenome genome, float fitness, int generation, string notes = "")
        {
            if (genome == null) return;

            var entry = Find(vessel, mode, intensity);
            if (entry == null)
            {
                entry = new Entry
                {
                    Vessel = vessel,
                    GameMode = mode,
                    Intensity = intensity,
                    Genome = genome.Clone(),
                    Fitness = fitness,
                    Generation = generation,
                    TrainedUtc = DateTime.UtcNow.ToString("o"),
                    Notes = notes
                };
                entry.Genome.Fitness = fitness;
                Entries.Add(entry);
                return;
            }

            entry.TrainedUtc = DateTime.UtcNow.ToString("o");
            if (!string.IsNullOrEmpty(notes)) entry.Notes = notes;

            if (fitness >= entry.Fitness || entry.Genome == null)
            {
                // New champion. The dethroned champion is a strong genome by
                // definition — give it a seat in the roster rather than the void.
                var previous = entry.Genome;
                float previousFitness = entry.Fitness;
                entry.Genome = genome.Clone();
                entry.Genome.Fitness = fitness;
                entry.Fitness = fitness;
                entry.Generation = generation;
                if (previous != null)
                    FoldIntoRoster(entry, previous, previousFitness);
            }
            else
            {
                FoldIntoRoster(entry, genome, fitness);
            }
        }

        static void FoldIntoRoster(Entry entry, TrainingGenome genome, float fitness)
        {
            if (genome == null) return;
            entry.Roster ??= new List<TrainingGenome>();

            // Not strong enough relative to the champion → no seat.
            if (entry.Fitness > 0f && fitness < entry.Fitness * RosterFitnessFloor) return;

            var candidate = genome.Clone();
            candidate.Fitness = fitness;
            int hash = candidate.BehaviorHash();

            // Same behavior fingerprint as the champion → redundant.
            if (entry.Genome != null && entry.Genome.BehaviorHash() == hash) return;

            // Same fingerprint as a roster member → keep the fitter of the two.
            for (int i = 0; i < entry.Roster.Count; i++)
            {
                if (entry.Roster[i].BehaviorHash() != hash) continue;
                if (fitness > entry.Roster[i].Fitness) entry.Roster[i] = candidate;
                return;
            }

            entry.Roster.Add(candidate);
            entry.Roster.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
            while (entry.Roster.Count > RosterCapacity)
                entry.Roster.RemoveAt(entry.Roster.Count - 1);
        }
    }
}
