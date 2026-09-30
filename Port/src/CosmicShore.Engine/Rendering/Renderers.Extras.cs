using System;
using System.Collections.Generic;
using CosmicShore.Engine.Rendering;

namespace CosmicShore.Engine.Rendering
{
    public enum LightProbeUsage { Off = 0, BlendProbes = 1, UseProxyVolume = 2, CustomProvided = 4 }
    public enum ReflectionProbeUsage { Off = 0, BlendProbes = 1, BlendProbesAndSkybox = 2, Simple = 3 }

    [Flags]
    public enum MeshUpdateFlags { Default = 0, DontValidateIndices = 1, DontResetBoneBounds = 2, DontNotifyMeshUsers = 4, DontRecalculateBounds = 8 }
}

namespace CosmicShore.Engine
{
    public enum LineTextureMode { Stretch = 0, Tile = 1, DistributePerSegment = 2, RepeatPerSegment = 3, Static = 4 }
    public enum LineAlignment { View = 0, Local = 1, TransformZ = 1 }
    public enum MotionVectorGenerationMode { Camera = 0, Object = 1, ForceNoMotion = 2 }
    public enum SpriteMeshType { FullRect = 0, Tight = 1 }
    public enum SkinQuality { Auto = 0, Bone1 = 1, Bone2 = 2, Bone4 = 4 }

    public partial class Renderer
    {
        /// <summary>Hide this renderer without disabling it (original contract).</summary>
        public bool forceRenderingOff { get; set; }
        public bool isVisible => enabled && !forceRenderingOff && gameObject != null && gameObject.activeInHierarchy;
        public LightProbeUsage lightProbeUsage { get; set; } = LightProbeUsage.BlendProbes;
        public ReflectionProbeUsage reflectionProbeUsage { get; set; } = ReflectionProbeUsage.BlendProbes;
        public MotionVectorGenerationMode motionVectorGenerationMode { get; set; } = MotionVectorGenerationMode.Object;
        public bool allowOcclusionWhenDynamic { get; set; } = true;
        public int sortingOrder { get; set; }
        public int sortingLayerID { get; set; }
        public string sortingLayerName { get; set; } = "Default";
        public int rendererPriority { get; set; }
        public uint renderingLayerMask { get; set; } = 1;
        public int lightmapIndex { get; set; } = -1;
        public Vector4 lightmapScaleOffset { get; set; } = new(1, 1, 0, 0);
        public bool staticShadowCaster { get; set; }
        public Transform probeAnchor { get; set; }

        /// <summary>Local-space AABB (the mesh bounds for mesh renderers, a unit cube otherwise).</summary>
        public virtual Bounds localBounds
        {
            get
            {
                Mesh mesh = this is SkinnedMeshRenderer skinned ? skinned.sharedMesh : GetComponent<MeshFilter>()?.sharedMesh;
                return mesh ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            }
            set { _localBoundsOverride = value; }
        }

        internal Bounds? _localBoundsOverride;
        public void ResetLocalBounds() => _localBoundsOverride = null;
        public void ResetBounds() { }

        public void GetSharedMaterials(List<Material> m) { m.Clear(); m.AddRange(sharedMaterials); }
        public void GetMaterials(List<Material> m) { m.Clear(); m.AddRange(materials); }
        public void SetSharedMaterials(List<Material> m) => sharedMaterials = m.ToArray();
        public void SetMaterials(List<Material> m) => materials = m.ToArray();

        readonly Dictionary<int, MaterialPropertyBlock> _indexedBlocks = new();

        /// <summary>Per-material-index block; takes precedence over the renderer-wide block for that submesh (original contract).</summary>
        public void SetPropertyBlock(MaterialPropertyBlock properties, int materialIndex)
        {
            if (properties == null || properties.isEmpty) { _indexedBlocks.Remove(materialIndex); return; }
            if (!_indexedBlocks.TryGetValue(materialIndex, out var b)) _indexedBlocks[materialIndex] = b = new MaterialPropertyBlock();
            b.CopyFrom(properties);
        }

        public void GetPropertyBlock(MaterialPropertyBlock dest, int materialIndex)
            => dest.CopyFrom(_indexedBlocks.TryGetValue(materialIndex, out var b) ? b : null);

        // The renderer-wide block is stored null when empty (SetPropertyBlock), so presence is a
        // field test. Both accessors are allocation-free: the render backend asks for every drawn
        // renderer every frame (it used to mint and copy a block per ask).
        public bool HasPropertyBlock() => _propertyBlock != null || _indexedBlocks.Count > 0;

        /// <summary>
        /// The block that applies to submesh <paramref name="materialIndex"/> (index block wins;
        /// renderer-wide otherwise), or null. The stored block itself: READ ONLY — write through
        /// SetPropertyBlock.
        /// </summary>
        public MaterialPropertyBlock PropertyBlockFor(int materialIndex)
        {
            if (materialIndex >= 0 && _indexedBlocks.Count > 0 && _indexedBlocks.TryGetValue(materialIndex, out var b)) return b;
            return _propertyBlock is { isEmpty: false } whole ? whole : null;
        }
    }

    public partial class SkinnedMeshRenderer
    {
        public Transform rootBone { get; set; }
        public Transform[] bones { get; set; } = Array.Empty<Transform>();
        public bool updateWhenOffscreen { get; set; }
        public SkinQuality quality { get; set; } = SkinQuality.Auto;
        public bool skinnedMotionVectors { get; set; } = true;
        public bool forceMatrixRecalculationPerRender { get; set; }

        /// <summary>
        /// The skinned hull's bounds in ROOT-BONE space (original contract: the imported AABB is
        /// the bind-pose mesh expressed in the root bone's frame, which is why a caller scales it
        /// by rootBone.lossyScale). Falls back to mesh space when there is no root bone in the skin.
        /// </summary>
        public override Bounds localBounds
        {
            get
            {
                if (_localBoundsOverride.HasValue) return _localBoundsOverride.Value;
                if (!sharedMesh) return new Bounds(Vector3.zero, Vector3.one);
                int root = rootBone != null && bones != null ? Array.IndexOf(bones, rootBone) : -1;
                var bindposes = sharedMesh.bindposes;
                if (root < 0 || bindposes == null || root >= bindposes.Length) return sharedMesh.bounds;
                if (_rootBoundsMesh != sharedMesh || _rootBoundsBone != root)
                {
                    var bp = bindposes[root];
                    var verts = sharedMesh.vertices;
                    Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
                    foreach (var v in verts)
                    {
                        var p = bp.MultiplyPoint3x4(v);
                        lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
                    }
                    _rootBounds = verts.Length > 0 ? new Bounds((lo + hi) * 0.5f, hi - lo) : sharedMesh.bounds;
                    _rootBoundsMesh = sharedMesh; _rootBoundsBone = root;
                }
                return _rootBounds;
            }
            set => _localBoundsOverride = value;
        }

        Bounds _rootBounds;
        Mesh _rootBoundsMesh;
        int _rootBoundsBone = -1;

        public void BakeMesh(Mesh mesh, bool useScale) => BakeMesh(mesh);
        public int GetBlendShapeCount() => sharedMesh ? sharedMesh.blendShapeCount : 0;
    }

    public partial class LineRenderer
    {
        public LineTextureMode textureMode { get; set; } = LineTextureMode.Stretch;
        public LineAlignment alignment { get; set; } = LineAlignment.View;
        public bool generateLightingData { get; set; }
        public float widthMultiplier { get; set; } = 1f;
        public AnimationCurve widthCurve { get; set; } = AnimationCurve.Constant(0f, 1f, 1f);
        public Gradient colorGradient { get; set; } = new();
        public float shadowBias { get; set; }
        public Vector2 textureScale { get; set; } = Vector2.one;
        public void SetPositions(List<Vector3> positions) => SetPositions(positions.ToArray());
        public void Simplify(float tolerance) { }
    }

    /// <summary>
    /// Trail (original contract: TrailRenderer). Points are sampled from the transform each frame by
    /// <see cref="Sample"/> (the render layer calls it) while <see cref="emitting"/>, and age out after
    /// <see cref="time"/> seconds; scripts may also add/edit points directly.
    /// </summary>
    public partial class TrailRenderer
    {
        readonly List<(Vector3 pos, float t)> _points = new();

        public float widthMultiplier { get; set; } = 1f;
        public AnimationCurve widthCurve { get; set; } = AnimationCurve.Constant(0f, 1f, 1f);
        public LineTextureMode textureMode { get; set; } = LineTextureMode.Stretch;
        public LineAlignment alignment { get; set; } = LineAlignment.View;
        public int numCornerVertices { get; set; }
        public bool autodestruct { get; set; }
        public float shadowBias { get; set; }
        public Vector2 textureScale { get; set; } = Vector2.one;

        public int positionCount => _points.Count;

        public void AddPosition(Vector3 position) => _points.Add((position, Time.time));
        public void AddPositions(Vector3[] positions) { foreach (var p in positions) AddPosition(p); }
        public void SetPosition(int index, Vector3 position) => _points[index] = (position, _points[index].t);
        public Vector3 GetPosition(int index) => _points[index].pos;
        public void SetPositions(Vector3[] positions) { _points.Clear(); AddPositions(positions); }

        public int GetPositions(Vector3[] positions)
        {
            int n = Math.Min(positions.Length, _points.Count);
            for (int i = 0; i < n; i++) positions[i] = _points[i].pos;
            return n;
        }

        public float GetPointAge(int index) => Time.time - _points[index].t;

        /// <summary>One frame of trail simulation: expire old points, append the transform's position when it moved far enough.</summary>
        public void Sample()
        {
            float now = Time.time;
            while (_points.Count > 0 && now - _points[0].t > time) _points.RemoveAt(0);
            if (!emitting || gameObject == null || !gameObject.activeInHierarchy) return;
            var p = transform.position;
            if (_points.Count == 0 || (_points[^1].pos - p).sqrMagnitude >= minVertexDistance * minVertexDistance)
                _points.Add((p, now));
            if (autodestruct && _points.Count == 0 && !emitting) Object.Destroy(gameObject);
        }
    }
}
