using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Content.Models;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Content.Scenes
{
    /// <summary>
    /// The model prefab Unity's ModelImporter generates from a model file, as a
    /// <see cref="PrefabGraph"/> — so a <c>PrefabInstance</c> whose <c>m_SourcePrefab</c> is
    /// an FBX expands through exactly the same code as a YAML prefab (transform parent,
    /// modifications, removed components, stripped aliases), and a model can be instantiated
    /// on its own.
    ///
    /// Every <see cref="ModelNode"/> becomes a GameObject (class 1) with a Transform (4), plus
    /// <list type="bullet">
    ///   <item>MeshFilter (33) + MeshRenderer (23) when the node carries an unskinned mesh,</item>
    ///   <item>SkinnedMeshRenderer (137) when the mesh has a skin or blend shapes,</item>
    ///   <item>Animator (95) on the root when the meta's <c>animationType</c> is Generic/Human.</item>
    /// </list>
    /// Object ids are Unity's own (<see cref="ModelFileIds"/>: the GameObject hashes its
    /// hierarchy path, a component hashes <c>path/ClassName</c>), so the fileIDs prefabs use to
    /// target model objects land on these objects. Bodies are written in Unity's serialized
    /// shape; intra-model references are LOCAL refs (re-mapped by instance expansion), asset
    /// references (mesh, materials, avatar) carry the model's guid or the meta's material remap.
    /// </summary>
    public static class ModelPrefabGraph
    {
        public const int GameObjectClass = 1, TransformClass = 4, MeshRendererClass = 23, MeshFilterClass = 33,
                         AnimatorClass = 95, SkinnedMeshRendererClass = 137;

        /// <summary>fileID of a model's Avatar sub-asset (<c>m_Avatar: {fileID: 9000000, guid: model}</c>).</summary>
        public const long AvatarFileId = 9000000;

        /// <summary>The model prefab graph for a model file; null when the model fails to import.</summary>
        public static PrefabGraph Build(AssetDatabase db, AssetFile file)
        {
            var model = db.LoadModel(file.Guid);
            return model == null ? null : Build(model, file);
        }

        public static PrefabGraph Build(ImportedModel model, AssetFile file)
        {
            var g = new PrefabGraph(file);
            string guid = model.Guid ?? file.Guid;
            foreach (var node in model.Nodes)
                AddNode(g, model, node, guid, file);
            return g;
        }

        /// <summary>The ref a model renderer carries for one of its FBX materials.</summary>
        public static YMap MaterialRef(ImportedModel model, string materialName, string guid)
        {
            if (materialName == null) return YMap.Ref(0);
            if (model.Settings.ExternalMaterials.TryGetValue(materialName, out var ext) && !ext.IsNull)
                return YMap.Ref(ext.FileId, ext.Guid, ext.Type == 0 ? 2 : ext.Type);
            return YMap.Ref(ModelFileIds.Material(materialName), guid, 3);
        }

        /// <summary>True when <paramref name="fileId"/> names an object of the model prefab (or the prefab asset itself).</summary>
        public static bool OwnsObject(ImportedModel model, long fileId)
        {
            if (fileId == ModelFileIds.PrefabAsset) return true;
            foreach (var n in model.Nodes)
            {
                if (n.GameObjectId == fileId) return true;
                foreach (var cls in ComponentClassesOf(model, n))
                    if (n.ComponentId(cls) == fileId) return true;
            }
            return false;
        }

        static IEnumerable<string> ComponentClassesOf(ImportedModel model, ModelNode node)
        {
            yield return "Transform";
            if (node.Mesh != null)
            {
                if (node.Skinned) yield return "SkinnedMeshRenderer";
                else { yield return "MeshFilter"; yield return "MeshRenderer"; }
            }
            if (node == model.Root && model.Settings.AnimationType is 2 or 3) yield return "Animator";
        }

        static void AddNode(PrefabGraph g, ImportedModel model, ModelNode node, string guid, AssetFile file)
        {
            long goId = node.GameObjectId;
            var components = new YSeq();
            foreach (var cls in ComponentClassesOf(model, node))
            {
                var entry = new YMap();
                entry.Add("component", YMap.Ref(node.ComponentId(cls)));
                components.List.Add(entry);
            }

            var go = new YMap();
            go.Add("m_Component", components);
            go.Add("m_Layer", S(0));
            go.Add("m_Name", new YScalar(node.Name));
            go.Add("m_TagString", new YScalar("Untagged"));
            go.Add("m_IsActive", S(1));
            Add(g, goId, GameObjectClass, "GameObject", go, file);

            foreach (var cls in ComponentClassesOf(model, node))
            {
                var body = new YMap();
                body.Add("m_GameObject", YMap.Ref(goId));
                switch (cls)
                {
                    case "Transform":
                        body.Add("m_LocalRotation", Quat(node.LocalRotation));
                        body.Add("m_LocalPosition", Vec(node.LocalPosition));
                        body.Add("m_LocalScale", Vec(node.LocalScale));
                        var children = new YSeq();
                        foreach (var c in node.Children) children.List.Add(YMap.Ref(c.ComponentId("Transform")));
                        body.Add("m_Children", children);
                        body.Add("m_Father", YMap.Ref(node.Parent?.ComponentId("Transform") ?? 0));
                        Add(g, node.ComponentId(cls), TransformClass, "Transform", body, file);
                        break;
                    case "MeshFilter":
                        body.Add("m_Mesh", YMap.Ref(node.Mesh.FileId, guid, 3));
                        Add(g, node.ComponentId(cls), MeshFilterClass, "MeshFilter", body, file);
                        break;
                    case "MeshRenderer":
                        AddRendererFields(body, model, node, guid);
                        Add(g, node.ComponentId(cls), MeshRendererClass, "MeshRenderer", body, file);
                        break;
                    case "SkinnedMeshRenderer":
                        AddRendererFields(body, model, node, guid);
                        var weights = new YSeq();
                        int shapes = node.Mesh.Mesh != null ? node.Mesh.Mesh.blendShapeCount : 0;
                        for (int i = 0; i < shapes; i++) weights.List.Add(S(0));
                        body.Add("m_BlendShapeWeights", weights);
                        body.Add("m_Mesh", YMap.Ref(node.Mesh.FileId, guid, 3));
                        var bones = new YSeq();
                        foreach (var b in node.Bones) bones.List.Add(YMap.Ref(b.ComponentId("Transform")));
                        body.Add("m_Bones", bones);
                        body.Add("m_RootBone", YMap.Ref(node.RootBone?.ComponentId("Transform") ?? 0));
                        Add(g, node.ComponentId(cls), SkinnedMeshRendererClass, "SkinnedMeshRenderer", body, file);
                        break;
                    case "Animator":
                        body.Add("m_Enabled", S(1));
                        body.Add("m_Avatar", YMap.Ref(AvatarFileId, guid, 3));
                        body.Add("m_Controller", YMap.Ref(0));
                        body.Add("m_CullingMode", S(0));
                        body.Add("m_UpdateMode", S(0));
                        body.Add("m_ApplyRootMotion", S(0));
                        Add(g, node.ComponentId(cls), AnimatorClass, "Animator", body, file);
                        break;
                }
            }
        }

        static void AddRendererFields(YMap body, ImportedModel model, ModelNode node, string guid)
        {
            body.Add("m_Enabled", S(1));
            body.Add("m_CastShadows", S(1));
            body.Add("m_ReceiveShadows", S(1));
            var mats = new YSeq();
            int submeshes = node.Mesh.Mesh != null ? System.Math.Max(1, node.Mesh.Mesh.subMeshCount) : node.Materials.Count;
            for (int i = 0; i < submeshes; i++)
                mats.List.Add(MaterialRef(model, i < node.Materials.Count ? node.Materials[i] : null, guid));
            body.Add("m_Materials", mats);
        }

        static void Add(PrefabGraph g, long id, int classId, string typeName, YMap body, AssetFile file)
            => g.Objects[id] = new GraphObject { Id = id, ClassId = classId, TypeName = typeName, Body = body, Origin = file };

        static YScalar S(long v) => new(v.ToString(CultureInfo.InvariantCulture));
        static YScalar F(float v) => new(v.ToString("R", CultureInfo.InvariantCulture));

        static YMap Vec(Vector3 v)
        {
            var m = new YMap();
            m.Add("x", F(v.x)); m.Add("y", F(v.y)); m.Add("z", F(v.z));
            return m;
        }

        static YMap Quat(Quaternion q)
        {
            var m = new YMap();
            m.Add("x", F(q.x)); m.Add("y", F(q.y)); m.Add("z", F(q.z)); m.Add("w", F(q.w));
            return m;
        }
    }
}
