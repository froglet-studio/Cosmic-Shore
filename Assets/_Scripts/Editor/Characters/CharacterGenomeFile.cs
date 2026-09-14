using System.IO;
using CosmicShore.Gameplay;
using UnityEditor;

namespace CosmicShore.Editor
{
    /// <summary>Genome save/load as JSON. A saved genome handed to someone else reproduces the identical face.</summary>
    public static class CharacterGenomeFile
    {
        public const string Extension = "genome.json";

        public static bool Save(CharacterGenome genome, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            File.WriteAllText(path, genome.ToJson(true));
            return true;
        }

        public static bool TryLoad(string path, out CharacterGenome genome)
        {
            genome = CharacterGenome.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            return CharacterGenome.TryFromJson(File.ReadAllText(path), out genome);
        }

        public static string PromptSavePath(CharacterGenome genome) =>
            EditorUtility.SaveFilePanel("Save genome", DefaultFolder(), $"{SafeName(genome)}.{Extension}", "json");

        public static string PromptLoadPath() => EditorUtility.OpenFilePanel("Load genome", DefaultFolder(), "json");

        static string DefaultFolder()
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "Library", "CharacterGenomes");
            Directory.CreateDirectory(folder);
            return folder;
        }

        static string SafeName(CharacterGenome g) =>
            g.IsPureHuman ? $"Human_{g.Seed}" : $"{g.CladeA}_{g.CladeB}_{g.Seed}";
    }
}
