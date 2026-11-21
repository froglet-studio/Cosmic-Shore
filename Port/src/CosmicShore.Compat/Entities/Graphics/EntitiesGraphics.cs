using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;
using Unity.Entities;

namespace Unity.Rendering
{
    /// <summary>
    /// Binds an <see cref="IComponentData"/> to a shader property so its value overrides the
    /// material per instance (Entities Graphics). The draw collection reads it: each such component's
    /// value reaches the renderer as that property's per-instance value.
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

    /// <summary>Tag: the entity is not drawn (the draw collection skips it).</summary>
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
    /// The Entities Graphics system: registration tables (stable, reference-counted
    /// <c>RegisterMesh</c>/<c>RegisterMaterial</c> IDs that resolve back to the asset) plus the
    /// draw collection. Every entity that has <see cref="MaterialMeshInfo"/> and a
    /// <c>LocalToWorld</c>, is not tagged <see cref="DisableRendering"/> or <c>Prefab</c>, and
    /// resolves to a mesh and material is handed to the renderer through
    /// <see cref="EntityDraws"/>, carrying every [MaterialProperty] component's value as a
    /// per-instance override (uploaded verbatim, as the original does).
    /// </summary>
    public class EntitiesGraphicsSystem : SystemBase
    {
        static readonly List<EntitiesGraphicsSystem> s_live = new();

        protected override void OnCreate()
        {
            lock (s_live) s_live.Add(this);
            EntityDraws.Collect = CollectAll;
        }

        protected override void OnDestroy()
        {
            lock (s_live) s_live.Remove(this);
        }

        static void CollectAll(EntityDrawList list)
        {
            lock (s_live)
                for (int i = 0; i < s_live.Count; i++) s_live[i].CollectInto(list);
        }

        sealed class Binding
        {
            public int Slot;
            public Func<object, Vector4> Read;
        }

        static readonly Dictionary<Type, Binding> s_bindings = new();

        /// <summary>The shader slot + value reader for a component type, or null when it is not a [MaterialProperty] component.</summary>
        static Binding BindingFor(Type type)
        {
            if (s_bindings.TryGetValue(type, out var b)) return b;
            b = null;
            var attr = (MaterialPropertyAttribute)Attribute.GetCustomAttribute(type, typeof(MaterialPropertyAttribute));
            if (attr != null && type.IsValueType)
            {
                var fields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (fields.Length == 1)
                {
                    var read = BuildReader(type, fields[0]);
                    if (read != null) b = new Binding { Slot = EntityDrawList.Slot(attr.Name), Read = read };
                }
            }
            s_bindings[type] = b;
            return b;
        }

        static Func<object, Vector4> BuildReader(Type owner, System.Reflection.FieldInfo field)
        {
            var p = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
            var value = System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression.Unbox(p, owner), field);
            var ctor = typeof(Vector4).GetConstructor(new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
            System.Linq.Expressions.Expression F(System.Linq.Expressions.Expression e, string member)
                => System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.Field(e, member), typeof(float));
            var zero = System.Linq.Expressions.Expression.Constant(0f);
            System.Linq.Expressions.Expression body;
            var t = field.FieldType;
            if (t == typeof(float)) body = System.Linq.Expressions.Expression.New(ctor, value, zero, zero, zero);
            else if (t == typeof(int) || t == typeof(uint))
                body = System.Linq.Expressions.Expression.New(ctor, System.Linq.Expressions.Expression.Convert(value, typeof(float)), zero, zero, zero);
            else if (t == typeof(Unity.Mathematics.float2)) body = System.Linq.Expressions.Expression.New(ctor, F(value, "x"), F(value, "y"), zero, zero);
            else if (t == typeof(Unity.Mathematics.float3)) body = System.Linq.Expressions.Expression.New(ctor, F(value, "x"), F(value, "y"), F(value, "z"), zero);
            else if (t == typeof(Unity.Mathematics.float4)) body = System.Linq.Expressions.Expression.New(ctor, F(value, "x"), F(value, "y"), F(value, "z"), F(value, "w"));
            else return null;
            return System.Linq.Expressions.Expression.Lambda<Func<object, Vector4>>(body, p).Compile();
        }

        /// <summary>
        /// What one entity draws with, derived from its component SET (not its values): whether it
        /// draws at all, and which components carry [MaterialProperty] values. Cached on the
        /// record against its shape, so the per-frame walk reads values without re-scanning
        /// every component of every entity.
        /// </summary>
        sealed class DrawPlan
        {
            public bool Draw, HasArray, HasFilter;
            public Type[] BoundTypes;
            public Binding[] Bindings;
        }

        static DrawPlan PlanFor(Unity.Entities.EntityStore.Record rec)
        {
            if (rec.ShapeCacheShape == rec.Shape && rec.ShapeCache is DrawPlan cached) return cached;
            var comps = rec.Components;
            var dis = rec.DisabledComponents;
            var plan = new DrawPlan
            {
                Draw = comps.ContainsKey(typeof(MaterialMeshInfo))
                    && (dis == null || !dis.Contains(typeof(MaterialMeshInfo)))
                    && !comps.ContainsKey(typeof(DisableRendering)) && !comps.ContainsKey(typeof(Prefab))
                    && comps.ContainsKey(typeof(Unity.Transforms.LocalToWorld)),
                HasArray = comps.ContainsKey(typeof(RenderMeshArray)),
                HasFilter = comps.ContainsKey(typeof(RenderFilterSettings)),
            };
            var types = new List<Type>();
            var bindings = new List<Binding>();
            foreach (var kv in comps)
            {
                var b = BindingFor(kv.Key);
                if (b != null && (dis == null || !dis.Contains(kv.Key))) { types.Add(kv.Key); bindings.Add(b); }
            }
            plan.BoundTypes = types.ToArray();
            plan.Bindings = bindings.ToArray();
            rec.ShapeCache = plan;
            rec.ShapeCacheShape = rec.Shape;
            return plan;
        }

        /// <summary>
        /// One entity's resolved draw, cached on its record against (system, shape, value version,
        /// registry version): a static prism - most of a grown arena - then skips the boxed
        /// component lookups and binding reads every frame and costs one version compare.
        /// </summary>
        sealed class DrawCache
        {
            public object Owner;
            public int Shape, Value, Registry;
            public bool Draw;
            public Mesh Mesh;
            public Material Material;
            public int Submesh, Layer;
            public Matrix4x4 Matrix;
            public int[] Slots;
            public Vector4[] Values;
        }

        int _registryVersion;

        void CollectInto(EntityDrawList list)
        {
            if (World == null || !World.IsCreated) return;
            var store = World.EntityManager.StoreOrNull;
            if (store == null) return;
            for (int i = 1; i < store.SlotCount; i++)
            {
                var rec = store.SlotAt(i);
                if (rec == null || !rec.Alive) continue;
                if (rec.DrawCache is not DrawCache c || !ReferenceEquals(c.Owner, this) || c.Shape != rec.Shape
                    || c.Value != rec.ValueVersion || c.Registry != _registryVersion)
                    c = Resolve(rec);
                else if (s_verify && CosmicShore.Engine.Time.frameCount % 30 == 0) VerifyCache(rec, c);
                if (!c.Draw) continue;
                int index = list.Add(c.Mesh, c.Material, c.Submesh, in c.Matrix, c.Layer);
                var slots = c.Slots; var values = c.Values;
                for (int k = 0; k < slots.Length; k++) list.Set(index, slots[k], values[k]);
            }
        }

        // COSMIC_SHORE_VERIFY_ENTITIES=1: every 30 frames, re-resolve each cache hit from its
        // components and report any field the cache got wrong.
        static readonly bool s_verify = System.Environment.GetEnvironmentVariable("COSMIC_SHORE_VERIFY_ENTITIES") == "1";
        int _verifyFrame = -1, _verifyChecked, _verifyStale;

        void VerifyCache(Unity.Entities.EntityStore.Record rec, DrawCache cached)
        {
            int frame = CosmicShore.Engine.Time.frameCount;
            if (_verifyFrame != frame)
            {
                if (_verifyFrame >= 0) System.Console.WriteLine($"[verify-entities] frame {_verifyFrame}: {_verifyChecked} cached draws checked, {_verifyStale} stale");
                _verifyFrame = frame; _verifyChecked = 0; _verifyStale = 0;
            }
            var keep = rec.DrawCache;
            rec.DrawCache = null;
            var fresh = Resolve(rec);
            rec.DrawCache = keep;
            _verifyChecked++;
            bool same = fresh.Draw == cached.Draw && (!fresh.Draw || (ReferenceEquals(fresh.Mesh, cached.Mesh) && ReferenceEquals(fresh.Material, cached.Material)
                && fresh.Submesh == cached.Submesh && fresh.Layer == cached.Layer && fresh.Matrix.Equals(cached.Matrix)
                && System.Linq.Enumerable.SequenceEqual(fresh.Values, cached.Values)));
            if (!same && _verifyStale++ == 0) System.Console.WriteLine($"[verify-entities] stale cache on '{rec.Name}'");
        }

        DrawCache Resolve(Unity.Entities.EntityStore.Record rec)
        {
            var c = rec.DrawCache as DrawCache;
            if (c == null || !ReferenceEquals(c.Owner, this)) rec.DrawCache = c = new DrawCache { Owner = this };
            c.Shape = rec.Shape; c.Value = rec.ValueVersion; c.Registry = _registryVersion;
            c.Draw = false;
            var plan = PlanFor(rec);
            if (!plan.Draw) return c;
            var comps = rec.Components;
            var mmi = (MaterialMeshInfo)comps[typeof(MaterialMeshInfo)];
            RenderMeshArray rma = plan.HasArray ? (RenderMeshArray)comps[typeof(RenderMeshArray)] : default;
            Mesh mesh = mmi.IsRuntimeMesh ? GetMesh(mmi.MeshID) : plan.HasArray ? rma.GetMesh(mmi) : null;
            Material material = mmi.IsRuntimeMaterial ? GetMaterial(mmi.MaterialID) : plan.HasArray ? rma.GetMaterial(mmi) : null;
            if (mesh == null || material == null) return c;

            c.Draw = true;
            c.Mesh = mesh; c.Material = material; c.Submesh = mmi.SubMesh;
            c.Layer = plan.HasFilter ? ((RenderFilterSettings)comps[typeof(RenderFilterSettings)]).Layer : 0;
            c.Matrix = ((Unity.Transforms.LocalToWorld)comps[typeof(Unity.Transforms.LocalToWorld)]).Value;
            var bt = plan.BoundTypes;
            var bs = plan.Bindings;
            if (c.Slots == null || c.Slots.Length != bt.Length) { c.Slots = new int[bt.Length]; c.Values = new Vector4[bt.Length]; }
            for (int k = 0; k < bt.Length; k++)
            {
                c.Slots[k] = bs[k].Slot;
                c.Values[k] = bs[k].Read(comps[bt[k]]);
            }
            return c;
        }

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
            _registryVersion++;
            if (!_meshes.TryGetValue(meshID.value, out var e)) return;
            if (e.refs > 1) { _meshes[meshID.value] = (e.mesh, e.refs - 1); return; }
            _meshes.Remove(meshID.value);
            _meshIds.Remove(e.mesh);
        }

        public void UnregisterMaterial(BatchMaterialID materialID)
        {
            _registryVersion++;
            if (!_materials.TryGetValue(materialID.value, out var e)) return;
            if (e.refs > 1) { _materials[materialID.value] = (e.material, e.refs - 1); return; }
            _materials.Remove(materialID.value);
            _materialIds.Remove(e.material);
        }

        public Mesh GetMesh(BatchMeshID mesh) => _meshes.TryGetValue(mesh.value, out var e) ? e.mesh : null;
        public Material GetMaterial(BatchMaterialID material) => _materials.TryGetValue(material.value, out var e) ? e.material : null;
    }
}
