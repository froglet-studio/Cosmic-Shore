// Minimal UnityEngine surface for the drill harness. Only what the SHIPPED drill files touch.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public partial class Object { }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => (T)System.Activator.CreateInstance(typeof(T), true);
    }
    public class Sprite : Object { }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => new(1, 1, 1, 1); }
    public static class Resources { public static T Load<T>(string path) where T : Object => null; }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, object> S = new();
        public static int GetInt(string k, int d) => S.TryGetValue(k, out var v) ? (int)v : d;
        public static void SetInt(string k, int v) => S[k] = v;
        public static float GetFloat(string k, float d) => S.TryGetValue(k, out var v) ? (float)v : d;
        public static void SetFloat(string k, float v) => S[k] = v;
        public static string GetString(string k, string d) => S.TryGetValue(k, out var v) ? (string)v : d;
        public static void SetString(string k, string v) => S[k] = v;
        public static void DeleteKey(string k) => S.Remove(k);
        public static void Save() { }
    }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.All)] public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)] public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.All)] public class TextAreaAttribute : Attribute { public TextAreaAttribute() { } public TextAreaAttribute(int a, int b) { } }
    [AttributeUsage(AttributeTargets.All)] public class MinAttribute : Attribute { public MinAttribute(float f) { } }
    [AttributeUsage(AttributeTargets.All)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.All)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.All)] public class SerializeReference : Attribute { }
    [AttributeUsage(AttributeTargets.All)] public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    [AttributeUsage(AttributeTargets.All)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
}
namespace CosmicShore.Utility
{
    public static class CSDebug
    {
        public static readonly List<string> Warnings = new();
        public static void LogWarning(string s) => Warnings.Add(s);
    }
}
namespace CosmicShore.UI
{
    // The real class is a MonoBehaviour; only its nested enum is read here. Copied VERBATIM by
    // run.sh from InputDeviceIconSetSwitcher.cs so the values cannot drift.
    public partial class InputDeviceIconSetSwitcher { }
}
namespace CosmicShore.Core
{
    public static class ProgressionBackendGate { public static bool CloudEnabled; }
    public sealed class DrillProgressRepository
    {
        public bool IsLoaded = true;
        public DrillProgressCloudData Data = new();
        public int Dirty;
        public void MarkDirty() => Dirty++;
    }
    public sealed class UGSDataService
    {
        public static UGSDataService Instance;
        public DrillProgressRepository DrillRepo = new();
    }
}
