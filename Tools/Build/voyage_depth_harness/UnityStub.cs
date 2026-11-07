// The slice of Unity the voyage rules and their suites touch outside the editor: JsonUtility (over System.Text.Json,
// public fields included, enums as numbers, the way the generator writes voyage_content.json) and the one squad
// constant the rules read. Nothing else: the rules are pure C# by design (Docs/Voyages/DEPTH.md).
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnityEngine
{
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Options = new()
        {
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
        public static string ToJson(object value) => JsonSerializer.Serialize(value, Options);
    }

    public static class Debug
    {
        public static void Log(object message) => System.Console.WriteLine(message);
        public static void LogWarning(object message) => System.Console.WriteLine("WARNING: " + message);
    }
}

namespace CosmicShore.Data
{
    /// <summary>The saved squad's one fact the voyage rules read: three seats.</summary>
    public class SavedSquad
    {
        public const int SlotCount = 3;
    }
}
