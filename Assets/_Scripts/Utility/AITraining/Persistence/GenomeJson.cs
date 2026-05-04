using System;
using System.IO;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// JSON sidecar export so trained genomes can be checked into git, shared,
    /// reviewed in PRs, or shipped without serializing the whole archive asset.
    /// </summary>
    public static class GenomeJson
    {
        public static string Export(TrainingGenome genome) => JsonUtility.ToJson(genome, prettyPrint: true);

        public static TrainingGenome Import(string json)
        {
            try { return JsonUtility.FromJson<TrainingGenome>(json); }
            catch (Exception e)
            {
                Debug.LogError($"[GenomeJson] Failed to import: {e.Message}");
                return null;
            }
        }

        public static void SaveToFile(TrainingGenome genome, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Export(genome));
        }

        public static TrainingGenome LoadFromFile(string path)
        {
            if (!File.Exists(path)) return null;
            return Import(File.ReadAllText(path));
        }
    }
}
