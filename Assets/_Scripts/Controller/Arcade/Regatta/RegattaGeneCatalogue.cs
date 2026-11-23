using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a Regatta course genome may place, and every parameter's legal range: the GENE CATALOGUE. It is data,
    /// written once by <c>Tools/Build/regatta_gene_catalogue.py</c> from the C# itself (each segment spawner's
    /// serialized fields, their defaults, their [Range]/[Min] attributes) and from a measured run of each spawner
    /// through the game's own generation (prisms, native size, finest prism), and read by both the game
    /// (<see cref="RegattaCourseGenome.Repair"/>, <see cref="RegattaPlacementBuilder"/>) and the Python search
    /// (Tools/Compute/evaluators/regatta_genome.py) from the same file,
    /// <c>Assets/Resources/Regatta/RegattaGeneCatalogue.json</c>. Never hand-kept in two places.
    /// </summary>
    public sealed class RegattaGeneCatalogue
    {
        public const string ResourcePath = "Regatta/RegattaGeneCatalogue";

        public enum ParamType { Float = 0, Int = 1, Bool = 2, Enum = 3, Domain = 4 }

        public sealed class Param
        {
            public string Name;
            public ParamType Type;
            public double Lo, Hi, Default;
            /// <summary>For <see cref="ParamType.Enum"/>: the enum's member names, index = gene value.</summary>
            public List<string> Values = new();
            /// <summary>The field's C# type name as written (an enum's type for Enum).</summary>
            public string CsType;

            public double Clamp(double v)
            {
                switch (Type)
                {
                    case ParamType.Bool: return v >= 0.5 ? 1 : 0;
                    case ParamType.Enum: return Math.Max(0, Math.Min(Math.Max(0, Values.Count - 1), Math.Round(v)));
                    case ParamType.Domain: return Math.Max(0, Math.Min(RegattaCourseGenome.PlacementDomains.Length - 2, Math.Round(v)));
                    case ParamType.Int: return Math.Max(Lo, Math.Min(Hi, Math.Round(v)));
                    default: return Math.Max(Lo, Math.Min(Hi, v));
                }
            }
        }

        public sealed class Kind
        {
            /// <summary>The gene's id: the spawner's class name without "Spawnable" (Gyroid, Kelpwood, ...).</summary>
            public string Id;
            /// <summary>The C# class (CosmicShore.Gameplay.SpawnableGyroid).</summary>
            public string Type;
            /// <summary>False = never placed; <see cref="Why"/> says why (read from the code or the measured run).</summary>
            public bool Usable;
            public string Why;
            /// <summary>True for a CellEnvironmentSpawnableBase world (it has a density dial and per-prism kinds).</summary>
            public bool World;
            /// <summary>Measured at the defaults: prisms laid, bounding radius (u), finest prism axis (u).</summary>
            public int NativePrisms;
            public double NativeRadius, NativeMinAxis;
            /// <summary>The placement's bounding radius (u) may be anywhere in [ScaleLo, ScaleHi].</summary>
            public double ScaleLo, ScaleHi;
            public readonly List<Param> Params = new();

            public Param FindParam(string name)
            {
                for (int i = 0; i < Params.Count; i++)
                    if (Params[i].Name == name) return Params[i];
                return null;
            }
        }

        public int Version;
        public readonly List<Kind> Kinds = new();
        readonly Dictionary<string, Kind> _byId = new();

        public bool TryGet(string id, out Kind kind)
        {
            kind = null;
            return id != null && _byId.TryGetValue(id, out kind);
        }

        public IEnumerable<Kind> UsableKinds
        {
            get
            {
                foreach (var k in Kinds)
                    if (k.Usable) yield return k;
            }
        }

        static RegattaGeneCatalogue _loaded;

        /// <summary>The shipped catalogue (the Resources TextAsset), parsed once. Null when the asset is missing, which
        /// <see cref="RegattaCourseGenome.Repair"/> reads as "no kind is placeable".</summary>
        public static RegattaGeneCatalogue Shipped
        {
            get
            {
                if (_loaded != null) return _loaded;
                var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourcePath);
                if (asset != null) _loaded = Parse(asset.text);
                return _loaded;
            }
            set => _loaded = value;
        }

        public static RegattaGeneCatalogue Parse(string json)
        {
            var root = RegattaJson.Obj(RegattaJson.Parse(json));
            var cat = new RegattaGeneCatalogue { Version = (int)RegattaJson.Num(root, "version", 0) };
            foreach (var ko in RegattaJson.Arr(RegattaJson.Get(root, "kinds")) ?? new List<object>())
            {
                var kd = RegattaJson.Obj(ko);
                if (kd == null) continue;
                var k = new Kind
                {
                    Id = RegattaJson.Str(kd, "id", null),
                    Type = RegattaJson.Str(kd, "type", null),
                    Usable = RegattaJson.Bool(kd, "usable", false),
                    Why = RegattaJson.Str(kd, "why", ""),
                    World = RegattaJson.Bool(kd, "world", false),
                    NativePrisms = (int)RegattaJson.Num(kd, "native_prisms", 0),
                    NativeRadius = RegattaJson.Num(kd, "native_radius", 0),
                    NativeMinAxis = RegattaJson.Num(kd, "native_min_axis", 0),
                    ScaleLo = RegattaJson.Num(kd, "scale_lo", 20),
                    ScaleHi = RegattaJson.Num(kd, "scale_hi", 200),
                };
                foreach (var po in RegattaJson.Arr(RegattaJson.Get(kd, "params")) ?? new List<object>())
                {
                    var pd = RegattaJson.Obj(po);
                    if (pd == null) continue;
                    var p = new Param
                    {
                        Name = RegattaJson.Str(pd, "name", null),
                        CsType = RegattaJson.Str(pd, "cs_type", null),
                        Lo = RegattaJson.Num(pd, "lo", 0),
                        Hi = RegattaJson.Num(pd, "hi", 1),
                        Default = RegattaJson.Num(pd, "default", 0),
                        Type = RegattaJson.Str(pd, "type", "float") switch
                        {
                            "int" => ParamType.Int,
                            "bool" => ParamType.Bool,
                            "enum" => ParamType.Enum,
                            "domain" => ParamType.Domain,
                            _ => ParamType.Float,
                        },
                    };
                    foreach (var v in RegattaJson.Arr(RegattaJson.Get(pd, "values")) ?? new List<object>())
                        if (v is string s) p.Values.Add(s);
                    if (p.Name != null) k.Params.Add(p);
                }
                if (k.Id == null) continue;
                cat.Kinds.Add(k);
                cat._byId[k.Id] = k;
            }
            return cat;
        }
    }
}
