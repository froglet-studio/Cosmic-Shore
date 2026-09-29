using System;
using System.Collections.Generic;
using CosmicShore.Content.Yaml;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// The ModelImporter settings of a model's <c>.meta</c> that change what the importer
    /// produces. Defaults are Unity's; every field is read from the meta when present.
    /// </summary>
    public sealed class ModelImportSettings
    {
        public float GlobalScale = 1f;
        public bool UseFileScale = true;
        public bool BakeAxisConversion;
        /// <summary>ModelImporterNormals: 0 Import, 1 Calculate, 2 None.</summary>
        public int NormalImportMode;
        public float NormalSmoothAngle = 60f;
        /// <summary>ModelImporterTangents: 0 Import, 1 CalculateLegacy, 2 None, 3 CalculateMikk, 4 CalculateLegacyWithSplitTangents.</summary>
        public int TangentImportMode = 3;
        /// <summary>0 Import, 1 Calculate, 2 None.</summary>
        public int BlendShapeNormalImportMode = 1;
        public bool ImportBlendShapes = true;
        public bool SwapUVChannels;
        public bool GenerateSecondaryUV;
        public bool KeepQuads;
        public bool WeldVertices = true;
        public bool IsReadable;
        public bool PreserveHierarchy;
        public bool SortHierarchyByName = true;
        public bool ImportCameras = true;
        public bool ImportLights = true;
        public int MaxBonesPerVertex = 4;
        public float MinBoneWeight = 0.001f;
        /// <summary>ModelImporterIndexFormat: 0 Auto, 1 UInt16, 2 UInt32.</summary>
        public int IndexFormat;
        /// <summary>Material remaps (<c>externalObjects</c>): FBX material name → project material.</summary>
        public readonly Dictionary<string, ObjRef> ExternalMaterials = new(StringComparer.Ordinal);
        /// <summary>Animation clip internal IDs from <c>clipAnimations</c> (name → fileID).</summary>
        public readonly Dictionary<string, long> ClipIds = new(StringComparer.Ordinal);

        public static ModelImportSettings FromMeta(YMap meta)
        {
            var s = new ModelImportSettings();
            var mi = meta?["ModelImporter"];
            if (mi == null) return s;
            var meshes = mi["meshes"];
            if (meshes != null)
            {
                s.GlobalScale = meshes.Float("globalScale", 1f);
                s.UseFileScale = meshes.Bool("useFileScale", true);
                s.BakeAxisConversion = meshes.Bool("bakeAxisConversion");
                s.ImportBlendShapes = meshes.Bool("importBlendShapes", true);
                s.SwapUVChannels = meshes.Bool("swapUVChannels");
                s.GenerateSecondaryUV = meshes.Bool("generateSecondaryUV");
                s.KeepQuads = meshes.Bool("keepQuads");
                s.WeldVertices = meshes.Bool("weldVertices", true);
                s.PreserveHierarchy = meshes.Bool("preserveHierarchy");
                s.SortHierarchyByName = meshes.Bool("sortHierarchyByName", true);
                s.ImportCameras = meshes.Bool("importCameras", true);
                s.ImportLights = meshes.Bool("importLights", true);
                s.MaxBonesPerVertex = meshes.Int("maxBonesPerVertex", 4);
                s.MinBoneWeight = meshes.Float("minBoneWeight", 0.001f);
                s.IndexFormat = meshes.Int("indexFormat");
            }
            var ts = mi["tangentSpace"];
            if (ts != null)
            {
                s.NormalImportMode = ts.Int("normalImportMode");
                s.NormalSmoothAngle = ts.Float("normalSmoothAngle", 60f);
                s.TangentImportMode = ts.Int("tangentImportMode", 3);
                s.BlendShapeNormalImportMode = ts.Int("blendShapeNormalImportMode", 1);
            }
            var anim = mi["animations"];
            s.IsReadable = (anim?["isReadable"] ?? mi["isReadable"])?.Scalar == "1";
            foreach (var clip in anim?["clipAnimations"]?.Items ?? Array.Empty<YNode>())
            {
                var name = clip.Str("name");
                if (name != null && YScalar.TryLong(clip.Str("internalID"), out var id) && id != 0) s.ClipIds[name] = id;
            }
            foreach (var e in mi["externalObjects"]?.Items ?? Array.Empty<YNode>())
            {
                var first = e["first"];
                if (first == null) continue;
                var type = first.Str("type") ?? "";
                if (!type.EndsWith("Material", StringComparison.Ordinal)) continue;
                var name = first.Str("name");
                if (name != null) s.ExternalMaterials[name] = ObjRef.From(e["second"]);
            }
            return s;
        }
    }
}
