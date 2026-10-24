// The slice of UnityEngine the Starlings' rules touch (StarlingRules, StarlingCardDeckSO, StarlingConfigSO), so their SHIPPED
// sources and their SHIPPED edit-mode test compile and run outside Unity. Behaviour is Unity's where the rules read it.
using System;

namespace UnityEngine
{
    public class Object { public string name; public static implicit operator bool(Object o) => o != null; }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        // Unity rounds half to even (Math.Round's default), the same as RoundToInt.
        public static int RoundToInt(float v) => (int)Math.Round(v);
    }

    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class TextAreaAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class MinAttribute : Attribute { public MinAttribute(float m) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class)] public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
}
