#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What kind of control a config field gets in the Black Hole tool.</summary>
    public enum BlackHoleToolFieldKind
    {
        Unsupported = 0,
        Float = 1,
        Int = 2,
        Bool = 3,
        Vector3 = 4,
        /// <summary>An asset reference (a material): shown by name, edited on the asset.</summary>
        Reference = 5,
    }

    /// <summary>
    /// One serialized field of a config ScriptableObject as the Black Hole tool edits it: its
    /// header group, a readable label, its tooltip, its control kind and its bounds — all read
    /// from the field's own attributes (<c>[Header]</c>, <c>[Tooltip]</c>, <c>[Range]</c>,
    /// <c>[Min]</c>), so the tool never carries a second copy of the config's ranges.
    /// </summary>
    public sealed class BlackHoleToolField
    {
        public FieldInfo Field { get; }
        public string Name => Field.Name;
        /// <summary>The <c>[Header]</c> this field opens, or null when it continues the previous group.</summary>
        public string Header { get; }
        public string Label { get; }
        public string Tooltip { get; }
        public BlackHoleToolFieldKind Kind { get; }
        /// <summary>True when the field has a <c>[Range]</c> — it gets a slider.</summary>
        public bool HasRange { get; }
        public float Min { get; }
        public float Max { get; }

        internal BlackHoleToolField(FieldInfo field, string header, string tooltip, BlackHoleToolFieldKind kind,
            bool hasRange, float min, float max)
        {
            Field = field;
            Header = header;
            Label = BlackHoleToolModel.Nicify(field.Name);
            Tooltip = tooltip;
            Kind = kind;
            HasRange = hasRange;
            Min = min;
            Max = max;
        }

        /// <summary><paramref name="value"/> inside the field's bounds (and whole, for an int field).</summary>
        public float Clamp(float value)
        {
            if (float.IsNaN(value)) value = Min > float.NegativeInfinity ? Min : 0f;
            value = Mathf.Clamp(value, Min, Max);
            return Kind == BlackHoleToolFieldKind.Int ? Mathf.Round(value) : value;
        }

        public float GetNumber(object target) => Kind switch
        {
            BlackHoleToolFieldKind.Float => (float)Field.GetValue(target),
            BlackHoleToolFieldKind.Int => (int)Field.GetValue(target),
            _ => 0f,
        };

        /// <summary>Writes a float or int field, clamped. Returns the value actually stored.</summary>
        public float SetNumber(object target, float value)
        {
            float v = Clamp(value);
            if (Kind == BlackHoleToolFieldKind.Int) Field.SetValue(target, (int)v);
            else if (Kind == BlackHoleToolFieldKind.Float) Field.SetValue(target, v);
            return v;
        }

        public bool GetBool(object target) => Kind == BlackHoleToolFieldKind.Bool && (bool)Field.GetValue(target);
        public void SetBool(object target, bool value) { if (Kind == BlackHoleToolFieldKind.Bool) Field.SetValue(target, value); }

        public Vector3 GetVector(object target) => Kind == BlackHoleToolFieldKind.Vector3 ? (Vector3)Field.GetValue(target) : Vector3.zero;
        public void SetVector(object target, Vector3 value) { if (Kind == BlackHoleToolFieldKind.Vector3) Field.SetValue(target, value); }
    }

    /// <summary>
    /// The Black Hole tool's logic, apart from its uGUI (<see cref="BlackHoleTool"/>): which fields
    /// of a config it edits and how, and how its console switch parses. Pure — no scene, no
    /// camera, no UI — so it is tested directly (BlackHoleToolTests) and against the compiled
    /// assembly offline.
    /// </summary>
    public static class BlackHoleToolModel
    {
        /// <summary>
        /// The config fields the tool's SPAWN rows show, in order (BlackHoleConfigSO's Spawn and
        /// Pair sections). Every other serialized field is in the config view.
        /// </summary>
        public static readonly string[] SpawnFieldNames =
            { "spawnStrength", "spawnHorizonRadius", "spawnAheadOfCamera", "spawnDistanceHorizons", "spawnPosition", "spawnVelocity", "spawnSpinAxis",
              "pairAheadHorizons", "pairHalfGapHorizons", "pairDriftSpeed", "pairLifetime" };

        /// <summary>
        /// Every field of <paramref name="type"/> Unity serializes (public, or <c>[SerializeField]</c>,
        /// and not <c>[NonSerialized]</c>), in declaration order, with its header group, tooltip,
        /// control kind and bounds. A field of a type the tool cannot draw comes back as
        /// <see cref="BlackHoleToolFieldKind.Unsupported"/> rather than being skipped, so a test can
        /// see it.
        /// </summary>
        public static List<BlackHoleToolField> EditableFields(Type type)
        {
            var result = new List<BlackHoleToolField>();
            if (type == null) return result;

            var fields = new List<FieldInfo>();
            // Base types first, so an inherited field keeps its place in the inspector order.
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(ScriptableObject) && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
                chain.Insert(0, t);
            foreach (var t in chain)
            {
                var declared = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Array.Sort(declared, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
                fields.AddRange(declared);
            }

            foreach (var f in fields)
            {
                bool serialized = (f.IsPublic || f.IsDefined(typeof(SerializeField), false)) &&
                                  !f.IsDefined(typeof(NonSerializedAttribute), false) && !f.IsInitOnly;
                if (!serialized) continue;

                var header = f.GetCustomAttribute<HeaderAttribute>(false);
                var tooltip = f.GetCustomAttribute<TooltipAttribute>(false);
                var range = f.GetCustomAttribute<RangeAttribute>(false);
                var minAttr = f.GetCustomAttribute<MinAttribute>(false);

                var kind = f.FieldType == typeof(float) ? BlackHoleToolFieldKind.Float
                    : f.FieldType == typeof(int) ? BlackHoleToolFieldKind.Int
                    : f.FieldType == typeof(bool) ? BlackHoleToolFieldKind.Bool
                    : f.FieldType == typeof(Vector3) ? BlackHoleToolFieldKind.Vector3
                    : typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType) ? BlackHoleToolFieldKind.Reference
                    : BlackHoleToolFieldKind.Unsupported;

                bool hasRange = range != null;
                float min = hasRange ? range.min : minAttr != null ? minAttr.min : float.NegativeInfinity;
                float max = hasRange ? range.max : float.PositiveInfinity;

                result.Add(new BlackHoleToolField(f, header?.header, tooltip?.tooltip, kind, hasRange, min, max));
            }
            return result;
        }

        /// <summary><c>spawnHorizonRadius</c> → <c>Spawn Horizon Radius</c> (ObjectNames.NicifyVariableName is editor-only).</summary>
        public static string Nicify(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return fieldName;
            string name = fieldName.TrimStart('_');
            if (name.Length > 2 && name[0] == 'm' && name[1] == '_') name = name.Substring(2);
            var sb = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool boundary = i > 0 && char.IsUpper(c) &&
                                (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));
                if (boundary) sb.Append(' ');
                sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The console's on/off word: <c>on</c>/<c>open</c>/<c>show</c>/<c>1</c>/<c>true</c> → true,
        /// <c>off</c>/<c>close</c>/<c>hide</c>/<c>0</c>/<c>false</c> → false, <c>toggle</c> or nothing →
        /// null (flip), anything else → null too, with <paramref name="recognised"/> false.
        /// </summary>
        public static bool? ParseSwitch(string word, out bool recognised)
        {
            recognised = true;
            if (string.IsNullOrWhiteSpace(word)) return null;
            switch (word.Trim().ToLowerInvariant())
            {
                case "on": case "open": case "show": case "1": case "true": return true;
                case "off": case "close": case "hide": case "0": case "false": return false;
                case "toggle": return null;
                default: recognised = false; return null;
            }
        }

        /// <summary>Invariant-culture float parse, so a decimal point works on every locale.</summary>
        public static bool TryParse(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>A number as the tool's input fields show it.</summary>
        public static string Format(float value, BlackHoleToolFieldKind kind) =>
            kind == BlackHoleToolFieldKind.Int
                ? ((int)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString(Mathf.Abs(value) >= 1000f ? "F0" : "0.###", CultureInfo.InvariantCulture);
    }
}
#endif
