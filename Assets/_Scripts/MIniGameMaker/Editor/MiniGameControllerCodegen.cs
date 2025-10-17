using System.IO;
using UnityEditor;

namespace CosmicShore.Tools.MiniGameMaker
{
    public static class MiniGameControllerCodegen
    {
        public static string Generate(string sceneName, string folderPath)
        {
            string className = $"{Sanitize(sceneName)}Controller";
            string filePath  = Path.Combine(folderPath, $"{className}.cs");

            Directory.CreateDirectory(folderPath);

            var code =
                $@"using UnityEngine;
using CosmicShore.Game.Arcade;

namespace CosmicShore.Game.MiniGames
{{
    /// <summary>Auto-generated. Configure via MiniGameProfileSO in the editor tool.</summary>
    public sealed class {className} : SinglePlayerMiniGameControllerBase
    {{
        [Header(""Profile"")]
        [SerializeField] private ScriptableObject profile; // MiniGameProfileSO (editor assigns)
        // NOTE: Do NOT re-declare 'miniGameData' here — it exists in the base class.
    }}
}}";
            File.WriteAllText(filePath, code);
            AssetDatabase.Refresh();
            return filePath;
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "MiniGame";
            var safe = System.Text.RegularExpressions.Regex.Replace(raw, @"[^A-Za-z0-9_]", "");
            if (char.IsDigit(safe[0])) safe = "_" + safe;
            return safe;
        }
    }
}