#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prisma.Training
{
    /// <summary>
    /// Resuming a parallel training run from the checkpoints its workers write at every generation
    /// boundary (Port/docs/AI_TRAINING.md "Long runs"). Pure file logic, shared with the tests: the
    /// supervisor (CosmicShore.Player.TrainingWorkers) calls <see cref="Prepare"/> before starting
    /// the workers with <c>--resume</c> on the paths it returns.
    /// </summary>
    public static class TrainingRunResume
    {
        /// <summary>
        /// Picks the generation the run resumes at and makes every worker slot's checkpoint hold it.
        ///
        /// <para>Each worker writes its own <c>ckpt_wN.json</c> right after its own merge, when its
        /// current match ends, so the workers write seconds apart, and a kill in that window
        /// leaves the slots one generation apart (or a slot with no checkpoint yet in the first
        /// generation). Every worker holds the identical evolved population after the merge, so
        /// the NEWEST checkpoint is valid for every slot: it is copied over each slot that is
        /// behind or missing, and the run resumes there. The run is refused only when no slot has
        /// a checkpoint, a checkpoint carries no generation, a checkpoint belongs to a slot the
        /// requested worker count does not have, or the run already finished.</para>
        ///
        /// <para>It then removes a failed-run marker and the result files of the resumed generation
        /// and later (those flights are re-flown from the resumed state and must not be merged
        /// against a stale slice). <paramref name="note"/> says which slots were brought forward,
        /// or is null when they all agreed. Returns the generation; <paramref name="checkpoints"/>
        /// is null, with <paramref name="refusal"/> set, when the run cannot resume.</para>
        /// </summary>
        public static int Prepare(string dir, int workers, out string[]? checkpoints, out string? refusal, out string? note)
        {
            checkpoints = null; refusal = null; note = null;
            if (!Directory.Exists(dir)) { refusal = $"no run directory {dir}"; return -1; }
            string done = Path.Combine(dir, "done");
            bool failedMarker = false;
            if (File.Exists(done))
            {
                string mark = File.ReadAllText(done).Trim();
                if (mark != "failed") { refusal = $"the run is already done (generation {mark})"; return -1; }
                failedMarker = true;
            }

            var found = new SortedDictionary<int, (string Path, int Generation)>();
            foreach (var f in Directory.GetFiles(dir, "ckpt_w*.json"))
            {
                int slot = CheckpointSlot(Path.GetFileName(f));
                if (slot < 0) continue;
                if (slot >= workers)
                {
                    refusal = $"{f} is worker {slot}'s checkpoint but --workers is {workers}; --workers must match the run that was killed";
                    return -1;
                }
                int g = CheckpointGeneration(File.ReadAllText(f));
                if (g < 0) { refusal = $"{f} carries no generation"; return -1; }
                found[slot] = (f, g);
            }
            if (found.Count == 0)
            {
                refusal = $"no worker checkpoint (ckpt_wN.json) in {dir}; a run killed before its first generation finished has nothing to resume";
                return -1;
            }

            // The newest generation; among equals the lowest slot, so the choice is deterministic.
            var newest = found.OrderByDescending(kv => kv.Value.Generation).ThenBy(kv => kv.Key).First();
            int generation = newest.Value.Generation;
            string json = File.ReadAllText(newest.Value.Path);
            var paths = new string[workers];
            var behind = new List<int>();
            for (int w = 0; w < workers; w++)
            {
                paths[w] = Path.Combine(dir, $"ckpt_w{w}.json");
                if (found.TryGetValue(w, out var own) && own.Generation == generation) continue;
                File.WriteAllText(paths[w] + ".tmp", json);
                File.Move(paths[w] + ".tmp", paths[w], overwrite: true);
                behind.Add(w);
            }

            if (failedMarker) File.Delete(done);
            foreach (var f in Directory.GetFiles(dir, "g*_w*.txt"))
                if (ResultGeneration(Path.GetFileName(f)) >= generation) File.Delete(f);

            if (behind.Count > 0)
            {
                note = $"worker(s) {string.Join(", ", behind)} had not checkpointed generation {generation} when the run stopped; " +
                       $"every worker resumes from {Path.GetFileName(newest.Value.Path)}";
                // Worker 0 alone appends the progress row and exports the trained genome, in its
                // merge, before it checkpoints: a worker 0 that was behind may not have written them.
                if (behind.Contains(0) && generation > 0)
                    note += $"; generation {generation - 1}'s progress row and trained export are written by worker 0's merge, which may not have run";
            }
            checkpoints = paths;
            return generation;
        }

        /// <summary>The worker slot a checkpoint file name (ckpt_wN.json) belongs to, or -1.</summary>
        public static int CheckpointSlot(string fileName)
        {
            const string prefix = "ckpt_w", suffix = ".json";
            if (!fileName.StartsWith(prefix, StringComparison.Ordinal) || !fileName.EndsWith(suffix, StringComparison.Ordinal)
                || fileName.Length <= prefix.Length + suffix.Length) return -1;
            return int.TryParse(fileName.AsSpan(prefix.Length, fileName.Length - prefix.Length - suffix.Length),
                NumberStyles.None, CultureInfo.InvariantCulture, out int slot) ? slot : -1;
        }

        /// <summary>The population generation a checkpoint (TrainingSessionStateSO as JsonUtility JSON) holds, or -1.</summary>
        public static int CheckpointGeneration(string json)
        {
            var m = Regex.Match(json, "\"generation\"\\s*:\\s*(\\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }

        /// <summary>The generation a worker result file name (gNNNNN_wK.txt) belongs to, or -1.</summary>
        public static int ResultGeneration(string fileName)
        {
            int us = fileName.IndexOf('_');
            if (fileName.Length < 2 || fileName[0] != 'g' || us <= 1) return -1;
            return int.TryParse(fileName.AsSpan(1, us - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int g) ? g : -1;
        }
    }
}
