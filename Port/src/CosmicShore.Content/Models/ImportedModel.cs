using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// A model file as Unity's ModelImporter presents it: the prefab root plus its node
    /// hierarchy (every FBX Model node is a GameObject), the meshes the importer generated
    /// (named after their NODE), in Unity's left-handed, file-scaled space.
    /// </summary>
    public sealed class ImportedModel
    {
        public string Name;
        public string Path;
        public string Guid;
        public ModelImportSettings Settings;
        public FbxScene Scene;
        public ModelNode Root;
        /// <summary>Every node, depth-first pre-order from the root (children in Unity's sibling order).</summary>
        public readonly List<ModelNode> Nodes = new();
        public readonly List<ImportedMesh> Meshes = new();
        public readonly Dictionary<long, ImportedMesh> MeshById = new();
        /// <summary>Unit conversion applied to positions: file scale (UnitScaleFactor/100 when useFileScale) × globalScale.</summary>
        public double UnitScale;
        public double FileScale;
        /// <summary>FBX→Unity axis conversion (rows: source coord, up, front axes).</summary>
        public DMat4 AxisConversion;
        /// <summary>Polygons Unity's importer would discard (a zero-length edge makes an n-gon "self-intersecting").</summary>
        public int DiscardedPolygons;
        public readonly List<string> Warnings = new();

        public ImportedMesh FindMesh(string nodeName)
        {
            foreach (var m in Meshes) if (m.Name == nodeName) return m;
            return null;
        }

        public ModelNode FindNode(string name)
        {
            foreach (var n in Nodes) if (n.Name == name) return n;
            return null;
        }
    }

    public sealed class ModelNode
    {
        /// <summary>GameObject name (the FBX node name; the prefab root is named after the file).</summary>
        public string Name;
        /// <summary>Unity's hierarchy key for fileID generation (<c>//RootNode/root/...</c>).</summary>
        public string Path;
        public int PathOccurrence;
        public ModelNode Parent;
        public readonly List<ModelNode> Children = new();
        public FbxObject Source;

        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;
        public Vector3 LocalScale = Vector3.one;
        /// <summary>Unity-space local and model-space (prefab root = identity) matrices, in double precision.</summary>
        public DMat4 LocalMatrix = DMat4.Identity;
        public DMat4 ModelMatrix = DMat4.Identity;

        public ImportedMesh Mesh;
        /// <summary>True when Unity gives this node a SkinnedMeshRenderer (skin or blend shapes) instead of MeshFilter + MeshRenderer.</summary>
        public bool Skinned;
        /// <summary>Material names per submesh (the node's FBX materials, in submesh order).</summary>
        public readonly List<string> Materials = new();
        public readonly List<ModelNode> Bones = new();
        public ModelNode RootBone;

        public long GameObjectId => ModelFileIds.GameObject(Path, PathOccurrence);
        public long ComponentId(string className) => ModelFileIds.Component(Path, className, PathOccurrence);

        public override string ToString() => Path;
    }

    public sealed class ImportedMesh
    {
        public Mesh Mesh;
        /// <summary>Mesh name = node name (Unity names a model's meshes after the node carrying them).</summary>
        public string Name;
        public int Occurrence;
        public long FileId;
        public ModelNode Node;
        public FbxObject Geometry;
        public int SourcePolygons;
        public int DiscardedPolygons;
    }
}
