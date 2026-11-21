using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Content.Scenes;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Content.Models
{
    /// <summary>One renderer in a prefab that draws a mesh of a model, with the materials it gives it.</summary>
    public sealed class ModelMaterialUse
    {
        /// <summary>Full path of the prefab.</summary>
        public string Prefab;
        /// <summary>The renderer's GameObject name in that prefab.</summary>
        public string GameObject;
        public ImportedMesh Mesh;
        public readonly List<ObjRef> Materials = new();
    }

    /// <summary>A material as a swatch: its name, shader and the colour it reads as.</summary>
    public readonly record struct MaterialSwatch(string Name, string Path, string Shader, Color? Color);

    /// <summary>
    /// What the game actually puts on a model. The model's own <c>.meta</c> rarely remaps its
    /// materials: in this project the real ones (the vessel graphs, team colours) are assigned by
    /// the prefabs that draw the model's meshes, a MeshFilter/SkinnedMeshRenderer whose
    /// <c>m_Mesh</c> names <c>{fileID: mesh, guid: model}</c> beside an <c>m_Materials</c> list. This
    /// finds those renderers, so a model can be shown in the colours the player sees.
    /// </summary>
    public static class ModelMaterialUsage
    {
        const int MeshFilterClass = 33, MeshRendererClass = 23, SkinnedMeshRendererClass = 137, GameObjectClass = 1;

        /// <summary>Every prefab renderer that draws a mesh of <paramref name="model"/>, prefabs by path.</summary>
        public static List<ModelMaterialUse> Find(AssetDatabase db, ImportedModel model)
        {
            var uses = new List<ModelMaterialUse>();
            if (model?.Guid == null) return uses;
            foreach (var path in db.AllAssetPaths.Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                                                 .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string text;
                try { text = File.ReadAllText(path); } catch (IOException) { continue; }
                if (!text.Contains(model.Guid, StringComparison.Ordinal)) continue;
                List<UnityDocument> docs;
                try { docs = UnityYaml.ParseDocuments(text); } catch (Exception) { continue; }
                uses.AddRange(InPrefab(path, docs, model));
            }
            return uses;
        }

        static IEnumerable<ModelMaterialUse> InPrefab(string path, List<UnityDocument> docs, ImportedModel model)
        {
            var meshOf = new Dictionary<long, ImportedMesh>();          // GameObject id → model mesh
            var materialsOf = new Dictionary<long, YNode>();            // GameObject id → m_Materials
            var names = new Dictionary<long, string>();
            foreach (var d in docs)
            {
                if (d.Body == null || d.Stripped) continue;
                long go = ObjRef.From(d.Body["m_GameObject"]).FileId;
                switch (d.ClassId)
                {
                    case GameObjectClass: names[d.FileId] = d.Body.Str("m_Name"); break;
                    case MeshFilterClass:
                        if (MeshRef(d.Body, model) is { } m) meshOf[go] = m;
                        break;
                    case MeshRendererClass:
                        materialsOf[go] = d.Body["m_Materials"];
                        break;
                    case SkinnedMeshRendererClass:
                        if (MeshRef(d.Body, model) is { } sm) { meshOf[go] = sm; materialsOf[go] = d.Body["m_Materials"]; }
                        break;
                }
            }
            foreach (var (go, mesh) in meshOf)
            {
                if (!materialsOf.TryGetValue(go, out var mats)) continue;
                var use = new ModelMaterialUse { Prefab = path, GameObject = names.GetValueOrDefault(go) ?? mesh.Name, Mesh = mesh };
                foreach (var item in mats?.Items ?? Array.Empty<YNode>()) use.Materials.Add(ObjRef.From(item));
                if (use.Materials.Count > 0) yield return use;
            }
        }

        static ImportedMesh MeshRef(YMap body, ImportedModel model)
        {
            var r = ObjRef.From(body["m_Mesh"]);
            return r.Guid == model.Guid && model.MeshById.TryGetValue(r.FileId, out var m) ? m : null;
        }

        /// <summary>
        /// The materials to draw each mesh with, as the game does: the prefab that draws the most of
        /// the model's meshes wins (then the first by path), other prefabs fill the meshes it leaves
        /// out, and a mesh no prefab draws keeps the model's own (the meta's remap or the FBX's).
        /// </summary>
        public static Dictionary<long, List<ObjRef>> Resolve(ImportedModel model, IReadOnlyList<ModelMaterialUse> uses, out string prefab)
        {
            var result = new Dictionary<long, List<ObjRef>>();
            // Ties go to the prefab named like the model (Manta_shapekey_rigged.fbx -> Manta.prefab).
            string modelName = System.IO.Path.GetFileNameWithoutExtension(model.Path ?? model.Name ?? "");
            prefab = uses.GroupBy(u => u.Prefab)
                         .OrderByDescending(g => g.Select(u => u.Mesh.FileId).Distinct().Count())
                         .ThenByDescending(g => modelName.Contains(System.IO.Path.GetFileNameWithoutExtension(g.Key), StringComparison.OrdinalIgnoreCase))
                         .FirstOrDefault()?.Key;
            var chosen = prefab;
            foreach (var u in uses.OrderBy(u => u.Prefab == chosen ? 0 : 1))
                result.TryAdd(u.Mesh.FileId, u.Materials);
            foreach (var m in model.Meshes)
                if (m.Mesh != null && !result.ContainsKey(m.FileId))
                    result[m.FileId] = Own(model, m);
            return result;
        }

        /// <summary>The model's own materials for a mesh: the meta's remap, else the FBX's embedded ones.</summary>
        public static List<ObjRef> Own(ImportedModel model, ImportedMesh mesh) =>
            (mesh.Node?.Materials ?? new List<string>())
                .Select(name => ObjRef.From(ModelPrefabGraph.MaterialRef(model, name, model.Guid))).ToList();

        static readonly string[] s_colorKeys = { "_Color1", "_BaseColor", "_Color", "_MainColor", "_TintColor", "_Tint" };

        /// <summary>A material's name, shader and the colour it reads as (null when it has none, e.g. a missing file).</summary>
        public static MaterialSwatch Swatch(AssetDatabase db, ImportedModel model, ObjRef r)
        {
            if (r.IsNull) return new MaterialSwatch("(none)", null, null, null);
            if (model != null && r.Guid == model.Guid)
            {
                // An embedded FBX material: Unity's default Lit with the FBX's diffuse colour.
                var o = model.Scene.ObjectList.FirstOrDefault(x => x.Kind == "Material" && x.Name != null && ModelFileIds.Material(x.Name) == r.FileId);
                if (o == null) return new MaterialSwatch("(embedded)", null, "Universal Render Pipeline/Lit", null);
                var d = o.HasProp("DiffuseColor") ? o.PropVector("DiffuseColor", 1, 1, 1) : null;
                return new MaterialSwatch(o.Name, null, "Universal Render Pipeline/Lit", d == null ? null : new Color((float)d[0], (float)d[1], (float)d[2], 1f));
            }
            var path = db.PathOf(r.Guid);
            if (path == null || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                return new MaterialSwatch(path == null ? "(missing)" : System.IO.Path.GetFileName(path), path, null, null);
            var body = db.Load(r.Guid)?.Documents.FirstOrDefault(d => d.TypeName == "Material")?.Body;
            if (body == null) return new MaterialSwatch(System.IO.Path.GetFileNameWithoutExtension(path), path, null, null);
            var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
            foreach (var entry in body["m_SavedProperties"]?["m_Colors"]?.Items ?? Array.Empty<YNode>())
                if (entry is YMap m && m.Entries.Count == 1) colors[m.Entries[0].Key] = SerializedReader.ReadColor(m.Entries[0].Value);
            Color? pick = null;
            foreach (var key in s_colorKeys)
                if (colors.TryGetValue(key, out var c))
                {
                    pick ??= c;
                    if (!IsWhite(c)) { pick = c; break; }
                }
            string shader = MaterialImporter.ShaderNameFor(ObjRef.From(body["m_Shader"]), db);
            return new MaterialSwatch(body.Str("m_Name") ?? System.IO.Path.GetFileNameWithoutExtension(path), path, shader, pick);
        }

        static bool IsWhite(Color c) => c.r > 0.99f && c.g > 0.99f && c.b > 0.99f;
    }
}
