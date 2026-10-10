#nullable enable
using System;
using System.Globalization;
using System.IO;
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
        /// Checks that every worker slot has a checkpoint and the run is not already done, reads the
        /// checkpoint generation, removes a failed-run marker and the in-flight generation's result
        /// files (generation >= the checkpoint's: those flights are re-flown from the resumed state
        /// and must not be merged against a stale slice). Returns the generation the run resumes
        /// at; <paramref name="checkpoints"/> is null, with <paramref name="refusal"/> set, when it
        /// cannot.
        /// </summary>
        public static int Prepare(string dir, int workers, out string[]? checkpoints, out string? refusal)
        {
            checkpoints = null; refusal = null;
            if (!Directory.Exists(dir)) { refusal = $"no run directory {dir}"; return -1; }
            string done = Path.Combine(dir, "done");
            if (File.Exists(done))
            {
                string mark = File.ReadAllText(done).Trim();
                if (mark != "failed") { refusal = $"the run is already done (generation {mark})"; return -1; }
                File.Delete(done);
            }
            var paths = new string[workers];
            int generation = -1;
            for (int w = 0; w < workers; w++)
            {
                paths[w] = Path.Combine(dir, $"ckpt_w{w}.json");
                if (!File.Exists(paths[w]))
                {
                    refusal = $"worker {w} has no checkpoint ({paths[w]}); every worker needs one, so --workers must match the run that was killed";
                    return -1;
                }
                int g = CheckpointGeneration(File.ReadAllText(paths[w]));
                if (g < 0) { refusal = $"{paths[w]} carries no generation"; return -1; }
                if (generation >= 0 && g != generation)
                {
                    refusal = $"checkpoints disagree on the generation ({generation} and {g}); the run cannot be merged";
                    return -1;
                }
                generation = g;
            }
            foreach (var f in Directory.GetFiles(dir, "g*_w*.txt"))
                if (ResultGeneration(Path.GetFileName(f)) >= generation) File.Delete(f);
            checkpoints = paths;
            return generation;
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
