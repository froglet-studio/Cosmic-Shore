using System;
using System.Collections.Generic;

namespace CosmicShore.Content.Models
{
    /// <summary>One FBX object (a child of the <c>Objects</c> record): Model, Geometry, Deformer, Material…</summary>
    public sealed class FbxObject
    {
        public long Id;
        /// <summary>Record name: Model, Geometry, Deformer, Material, NodeAttribute, Pose…</summary>
        public string Kind;
        /// <summary>Object name without the class decoration (binary "Name\0\x01Class", ASCII "Class::Name").</summary>
        public string Name;
        /// <summary>Sub-class: Mesh / Null / LimbNode for Models, Mesh / Shape for Geometry, Skin / Cluster / BlendShape / BlendShapeChannel for Deformers.</summary>
        public string SubClass;
        public FbxNode Node;
        /// <summary>Properties70 merged over the Definitions template for this object type.</summary>
        public readonly Dictionary<string, FbxNode> Props = new(StringComparer.Ordinal);

        /// <summary>Objects connected INTO this one (connection child → this parent), in file order.</summary>
        public readonly List<FbxObject> Children = new();
        /// <summary>Objects this one is connected to (this child → parent), in file order.</summary>
        public readonly List<FbxObject> Parents = new();
        /// <summary>For OP connections to this object: property name → source objects.</summary>
        public readonly List<(FbxObject Source, string Property)> PropertyInputs = new();

        public double[] PropVector(string name, double x = 0, double y = 0, double z = 0)
        {
            if (!Props.TryGetValue(name, out var p) || p.Props.Count < 7) return new[] { x, y, z };
            return new[] { p.Double(4), p.Double(5), p.Double(6) };
        }

        public double PropDouble(string name, double fallback = 0)
            => Props.TryGetValue(name, out var p) && p.Props.Count >= 5 ? p.Double(4) : fallback;

        public long PropLong(string name, long fallback = 0)
            => Props.TryGetValue(name, out var p) && p.Props.Count >= 5 ? p.Long(4) : fallback;

        public bool HasProp(string name) => Props.ContainsKey(name);

        public override string ToString() => $"{Kind}:{SubClass} '{Name}' ({Id})";
    }

    /// <summary>
    /// The object graph of an FBX file: objects by id, the OO/OP connection graph, the
    /// GlobalSettings axis/unit description and the per-type property templates.
    /// </summary>
    public sealed class FbxScene
    {
        public readonly FbxReader.Result File;
        public readonly Dictionary<long, FbxObject> Objects = new();
        public readonly List<FbxObject> ObjectList = new();
        /// <summary>Models connected to the scene root (id 0), in connection order.</summary>
        public readonly List<FbxObject> RootModels = new();
        public readonly Dictionary<string, FbxNode> GlobalSettings = new(StringComparer.Ordinal);

        public int Version => File.Version;

        public FbxScene(FbxReader.Result file)
        {
            File = file;
            foreach (var p in file.Top("GlobalSettings")?.Child("Properties70")?.ChildrenNamed("P") ?? Array.Empty<FbxNode>())
                GlobalSettings[p.String(0)] = p;

            var templates = ReadTemplates(file.Top("Definitions"));

            foreach (var n in file.Top("Objects")?.Children ?? new List<FbxNode>())
            {
                var o = new FbxObject { Kind = n.Name, Node = n };
                if (n.Props.Count > 0 && (n.Props[0] is long || n.Props[0] is int)) o.Id = n.Long(0);
                SplitName(n.String(1) ?? "", out o.Name, out _);
                o.SubClass = n.String(2) ?? "";
                if (templates.TryGetValue(n.Name, out var tpl))
                    foreach (var kv in tpl) o.Props[kv.Key] = kv.Value;
                foreach (var p in n.Child("Properties70")?.ChildrenNamed("P") ?? Array.Empty<FbxNode>())
                    o.Props[p.String(0)] = p;
                // Pre-7000 style "Properties60"/"Property" is not produced by any file in the project.
                Objects[o.Id] = o;
                ObjectList.Add(o);
            }

            foreach (var c in file.Top("Connections")?.ChildrenNamed("C") ?? Array.Empty<FbxNode>())
            {
                string type = c.String(0);
                long child = c.Long(1), parent = c.Long(2);
                if (!Objects.TryGetValue(child, out var co)) continue;
                if (parent == 0)
                {
                    if (co.Kind == "Model") RootModels.Add(co);
                    continue;
                }
                if (!Objects.TryGetValue(parent, out var po)) continue;
                if (type == "OP") po.PropertyInputs.Add((co, c.String(3)));
                po.Children.Add(co);
                co.Parents.Add(po);
            }
        }

        static Dictionary<string, Dictionary<string, FbxNode>> ReadTemplates(FbxNode definitions)
        {
            var result = new Dictionary<string, Dictionary<string, FbxNode>>(StringComparer.Ordinal);
            if (definitions == null) return result;
            foreach (var ot in definitions.ChildrenNamed("ObjectType"))
            {
                var tpl = ot.Child("PropertyTemplate");
                if (tpl == null) continue;
                var props = new Dictionary<string, FbxNode>(StringComparer.Ordinal);
                foreach (var p in tpl.Child("Properties70")?.ChildrenNamed("P") ?? Array.Empty<FbxNode>())
                    props[p.String(0)] = p;
                result[ot.String(0)] = props;
            }
            return result;
        }

        /// <summary>Binary names are "Name\0\x01Class"; ASCII names are "Class::Name".</summary>
        public static void SplitName(string raw, out string name, out string cls)
        {
            int z = raw.IndexOf('\0');
            if (z >= 0)
            {
                name = raw.Substring(0, z);
                cls = z + 2 <= raw.Length ? raw.Substring(z + 2) : "";
                return;
            }
            int c = raw.IndexOf("::", StringComparison.Ordinal);
            if (c >= 0) { cls = raw.Substring(0, c); name = raw.Substring(c + 2); return; }
            name = raw; cls = "";
        }

        public double GlobalDouble(string name, double fallback)
            => GlobalSettings.TryGetValue(name, out var p) && p.Props.Count >= 5 ? p.Double(4) : fallback;

        public int GlobalInt(string name, int fallback)
            => GlobalSettings.TryGetValue(name, out var p) && p.Props.Count >= 5 ? (int)p.Long(4) : fallback;

        public static FbxScene Load(string path) => new FbxScene(FbxReader.ReadFile(path));
    }
}
