using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;
using Unity.Entities;

namespace Unity.Rendering
{
    /// <summary>
    /// Binds an <see cref="IComponentData"/> to a shader property so its value overrides the
    /// material per instance (Entities Graphics). Metadata only in the port — no renderer reads it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class MaterialPropertyAttribute : Attribute
    {
        public string Name { get; }
        public short OverrideSizeGPU { get; }

        public MaterialPropertyAttribute(string materialPropertyName, short overrideSizeGPU = -1)
        {
            Name = materialPropertyName;
            OverrideSizeGPU = overrideSizeGPU;
        }
    }

    /// <summary>Tag: the entity is not drawn. (No entity is drawn in the port; the tag is honored as data.)</summary>
    public struct DisableRendering : IComponentData { }

    /// <summary>Tag the original adds to per-instance-culled entities.</summary>
    public struct PerInstanceCullingTag : IComponentData { }

    /// <summary>Tag the original adds to entities whose shader needs a world-to-local matrix.</summary>
    public struct WorldToLocal_Tag : IComponentData { }

    /// <summary>
    /// Which mesh/material an entity draws with. Positive values are registered
    /// <see cref="BatchMaterialID"/>/<see cref="BatchMeshID"/> values; negative values are
    /// indices into the entity's <see cref="RenderMeshArray"/> (the original's encoding).
    /// </summary>
    public struct MaterialMeshInfo : IComponentData, IEquatable<MaterialMeshInfo>
    {
        public int Material;
        public int Mesh;
        public ushort SubMesh;

        public MaterialMeshInfo(BatchMaterialID materialID, BatchMeshID meshID, ushort submeshIndex = 0)
        {
            Material = (int)materialID.value;
            Mesh = (int)meshID.value;
            SubMesh = submeshIndex;
        }

        public static int ArrayIndexToStaticIndex(int index) => index < 0 ? index : -(index + 1);
        public static int StaticIndexToArrayIndex(int staticIndex) => Math.Abs(staticIndex) - 1;

        public static MaterialMeshInfo FromRenderMeshArrayIndices(int materialIndexInRenderMeshArray, int meshIndexInRenderMeshArray, ushort submeshIndex = 0) =>
            new MaterialMeshInfo
            {
                Material = ArrayIndexToStaticIndex(materialIndexInRenderMeshArray),
                Mesh = ArrayIndexToStaticIndex(meshIndexInRenderMeshArray),
                SubMesh = submeshIndex,
            };

        public bool IsRuntimeMaterial => Material >= 0;
        public bool IsRuntimeMesh => Mesh >= 0;

        public BatchMaterialID MaterialID
        {
            get => IsRuntimeMaterial ? new BatchMaterialID { value = (uint)Material } : BatchMaterialID.Null;
            set => Material = (int)value.value;
        }

        public BatchMeshID MeshID
        {
            get => IsRuntimeMesh ? new BatchMeshID { value = (uint)Mesh } : BatchMeshID.Null;
            set => Mesh = (int)value.value;
        }

        public int MaterialArrayIndex
        {
            get => IsRuntimeMaterial ? -1 : StaticIndexToArrayIndex(Material);
            set => Material = ArrayIndexToStaticIndex(value);
        }

        public int MeshArrayIndex
        {
            get => IsRuntimeMesh ? -1 : StaticIndexToArrayIndex(Mesh);
            set => Mesh = ArrayIndexToStaticIndex(value);
        }

        public bool Equals(MaterialMeshInfo o) => Material == o.Material && Mesh == o.Mesh && SubMesh == o.SubMesh;
        public override bool Equals(object obj) => obj is MaterialMeshInfo m && Equals(m);
        public override int GetHashCode() => HashCode.Combine(Material, Mesh, SubMesh);
    }

    /// <summary>The meshes/materials a group of entities indexes into (shared component).</summary>
    public struct RenderMeshArray : ISharedComponentData, IEquatable<RenderMeshArray>
    {
        public Material[] MaterialReferences;
        public Mesh[] MeshReferences;

        public RenderMeshArray(Material[] materials, Mesh[] meshes)
        {
            MaterialReferences = materials;
            MeshReferences = meshes;
        }

        public Material GetMaterial(MaterialMeshInfo info) =>
            !info.IsRuntimeMaterial && MaterialReferences != null && info.MaterialArrayIndex < MaterialReferences.Length ? MaterialReferences[info.MaterialArrayIndex] : null;

        public Mesh GetMesh(MaterialMeshInfo info) =>
            !info.IsRuntimeMesh && MeshReferences != null && info.MeshArrayIndex < MeshReferences.Length ? MeshReferences[info.MeshArrayIndex] : null;

        public bool Equals(RenderMeshArray o) => ReferenceEquals(MaterialReferences, o.MaterialReferences) && ReferenceEquals(MeshReferences, o.MeshReferences);
        public override bool Equals(object obj) => obj is RenderMeshArray r && Equals(r);
        public override int GetHashCode() => HashCode.Combine(MaterialReferences, MeshReferences);
    }

    /// <summary>Per-group render filtering (layer, shadows) — shared component.</summary>
    public struct RenderFilterSettings : ISharedComponentData, IEquatable<RenderFilterSettings>
    {
        public int Layer;
        public uint RenderingLayerMask;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public bool StaticShadowCaster;

        public static RenderFilterSettings Default => new RenderFilterSettings
        {
            Layer = 0,
            RenderingLayerMask = 0xffffffff,
            ShadowCastingMode = ShadowCastingMode.On,
            ReceiveShadows = true,
            StaticShadowCaster = false,
        };

        public bool IsInDepthPass => ShadowCastingMode != ShadowCastingMode.Off;

        public bool Equals(RenderFilterSettings o) =>
            Layer == o.Layer && RenderingLayerMask == o.RenderingLayerMask && ShadowCastingMode == o.ShadowCastingMode &&
            ReceiveShadows == o.ReceiveShadows && StaticShadowCaster == o.StaticShadowCaster;
        public override bool Equals(object obj) => obj is RenderFilterSettings r && Equals(r);
        public override int GetHashCode() => HashCode.Combine(Layer, RenderingLayerMask, ShadowCastingMode, ReceiveShadows, StaticShadowCaster);
    }

    /// <summary>
    /// How <see cref="RenderMeshUtility.AddComponents(Entity, EntityManager, in RenderMeshDescription, MaterialMeshInfo)"/>
    /// sets an entity up to draw. The original's MotionVectorGenerationMode / LightProbeUsage
    /// parameters are omitted (the engine has neither type; nothing in the game passes them).
    /// </summary>
    public struct RenderMeshDescription
    {
        public RenderFilterSettings FilterSettings;

        public RenderMeshDescription(ShadowCastingMode shadowCastingMode, bool receiveShadows = false, int layer = 0,
                                     uint renderingLayerMask = uint.MaxValue, bool staticShadowCaster = false)
        {
            FilterSettings = new RenderFilterSettings
            {
                Layer = layer,
                RenderingLayerMask = renderingLayerMask,
                ShadowCastingMode = shadowCastingMode,
                ReceiveShadows = receiveShadows,
                StaticShadowCaster = staticShadowCaster,
            };
        }

        public RenderMeshDescription(RenderFilterSettings filterSettings) { FilterSettings = filterSettings; }
    }

    /// <summary>
    /// The Entities Graphics system. The port keeps its registration tables (so
    /// <c>RegisterMesh</c>/<c>RegisterMaterial</c> hand out stable, reference-counted IDs that
    /// resolve back to the asset) but DRAWS NOTHING — there is no BatchRendererGroup. It is
    /// deliberately not created by <see cref="DefaultWorldInitialization"/>, so game code that
    /// probes for it (PrismRenderService) keeps its MonoBehaviour render path.
    /// </summary>
    public class EntitiesGraphicsSystem : SystemBase
    {
        readonly Dictionary<Mesh, uint> _meshIds = new();
        readonly Dictionary<uint, (Mesh mesh, int refs)> _meshes = new();
        readonly Dictionary<Material, uint> _materialIds = new();
        readonly Dictionary<uint, (Material material, int refs)> _materials = new();
        uint _nextMesh = 1, _nextMaterial = 1;

        protected override void OnUpdate() { }

        public BatchMeshID RegisterMesh(Mesh mesh)
        {
            if (mesh == null) return BatchMeshID.Null;
            if (_meshIds.TryGetValue(mesh, out var id))
            {
                var e = _meshes[id];
                _meshes[id] = (e.mesh, e.refs + 1);
            }
            else
            {
                id = _nextMesh++;
                _meshIds[mesh] = id;
                _meshes[id] = (mesh, 1);
            }
            return new BatchMeshID { value = id };
        }

        public BatchMaterialID RegisterMaterial(Material material)
        {
            if (material == null) return BatchMaterialID.Null;
            if (_materialIds.TryGetValue(material, out var id))
            {
                var e = _materials[id];
                _materials[id] = (e.material, e.refs + 1);
            }
            else
            {
                id = _nextMaterial++;
                _materialIds[material] = id;
                _materials[id] = (material, 1);
            }
            return new BatchMaterialID { value = id };
        }

        public void UnregisterMesh(BatchMeshID meshID)
        {
            if (!_meshes.TryGetValue(meshID.value, out var e)) return;
            if (e.refs > 1) { _meshes[meshID.value] = (e.mesh, e.refs - 1); return; }
            _meshes.Remove(meshID.value);
            _meshIds.Remove(e.mesh);
        }

        public void UnregisterMaterial(BatchMaterialID materialID)
        {
            if (!_materials.TryGetValue(materialID.value, out var e)) return;
            if (e.refs > 1) { _materials[materialID.value] = (e.material, e.refs - 1); return; }
            _materials.Remove(materialID.value);
            _materialIds.Remove(e.material);
        }

        public Mesh GetMesh(BatchMeshID mesh) => _meshes.TryGetValue(mesh.value, out var e) ? e.mesh : null;
        public Material GetMaterial(BatchMaterialID material) => _materials.TryGetValue(material.value, out var e) ? e.material : null;
    }
}
