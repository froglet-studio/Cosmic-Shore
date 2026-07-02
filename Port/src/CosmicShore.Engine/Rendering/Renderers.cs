using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Engine
{
    /// <summary>
    /// E2 renderer data stubs (VESSEL_LAYER.md). Headless-first: these hold the data
    /// ported code reads/writes (materials, blend-shape weights) and no-op the GPU
    /// side. A future render backend reads the same state. Unlike Unity, `material`
    /// does NOT clone — the engine has no instancing leak to defend against, and
    /// ported gameplay treats the returned instance as the thing to mutate.
    /// </summary>
    public class Renderer : Component
    {
        public bool enabled = true;

        /// <summary>Shadow casting mode (original default: On). Data-only until a render backend reads it.</summary>
        public Rendering.ShadowCastingMode shadowCastingMode = Rendering.ShadowCastingMode.On;

        /// <summary>Whether this renderer receives shadows (original default: true). Data-only.</summary>
        public bool receiveShadows = true;

        /// <summary>
        /// World-space AABB. With real mesh data available (the mesh arc): a sibling
        /// <see cref="MeshFilter"/>'s shared mesh — or a <see cref="SkinnedMeshRenderer"/>'s
        /// own <see cref="SkinnedMeshRenderer.sharedMesh"/> — provides the local bounds,
        /// transformed through the full TRS into a world AABB (8-corner sweep, so rotation
        /// is respected). Without a mesh this keeps the pre-mesh-arc unit-cube convention:
        /// center at the transform position, size = |lossyScale| — the same convention the
        /// prism slabs and trigger-pass box bounds use.
        /// </summary>
        public Bounds bounds
        {
            get
            {
                Mesh mesh = this is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh)
                    return TransformedMeshBounds(mesh.bounds, transform);

                Vector3 s = transform.lossyScale;
                return new Bounds(transform.position,
                    new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            }
        }

        /// <summary>Local mesh bounds → world AABB via the 8 transformed corners.</summary>
        internal static Bounds TransformedMeshBounds(Bounds local, Transform transform)
        {
            Vector3 c = local.center, e = local.extents;
            Vector3 min = default, max = default;
            for (int i = 0; i < 8; i++)
            {
                var corner = transform.TransformPoint(new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z)));
                if (i == 0) { min = corner; max = corner; continue; }
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }
            return new Bounds((min + max) * 0.5f, max - min);
        }

        Material[] _materials = System.Array.Empty<Material>();

        public Material[] materials
        {
            get => _materials;
            set => _materials = value ?? System.Array.Empty<Material>();
        }

        public Material[] sharedMaterials
        {
            get => _materials;
            set => _materials = value ?? System.Array.Empty<Material>();
        }

        public Material material
        {
            get => _materials.Length > 0 ? _materials[0] : null;
            set
            {
                if (_materials.Length == 0) _materials = new Material[1];
                _materials[0] = value;
            }
        }

        public Material sharedMaterial
        {
            get => material;
            set => material = value;
        }

        MaterialPropertyBlock _propertyBlock;

        /// <summary>Snapshot <paramref name="properties"/> onto this renderer (copy-on-set; null/empty clears).</summary>
        public void SetPropertyBlock(MaterialPropertyBlock properties)
        {
            if (properties == null || properties.isEmpty)
            {
                _propertyBlock = null;
                return;
            }
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.CopyFrom(properties);
        }

        /// <summary>Overwrite <paramref name="dest"/> with this renderer's current block (cleared if none set).</summary>
        public void GetPropertyBlock(MaterialPropertyBlock dest) => dest.CopyFrom(_propertyBlock);
    }

    public class MeshRenderer : Renderer
    {
    }

    public class SkinnedMeshRenderer : Renderer
    {
        readonly Dictionary<int, float> _blendShapeWeights = new();

        /// <summary>The skinned mesh in its authored (bind) pose. Data-only — no skinning runs headless.</summary>
        public Mesh sharedMesh;

        public void SetBlendShapeWeight(int index, float value) => _blendShapeWeights[index] = value;

        public float GetBlendShapeWeight(int index)
            => _blendShapeWeights.TryGetValue(index, out var w) ? w : 0f;

        /// <summary>
        /// Original contract: snapshot the current deformed state into <paramref name="mesh"/>.
        /// Headless, no skinning ever runs, so the snapshot IS the shared mesh — a deep copy
        /// of <see cref="sharedMesh"/>'s buffers (empty if none assigned).
        /// </summary>
        public void BakeMesh(Mesh mesh)
        {
            if (mesh is null) return;
            if (sharedMesh) sharedMesh.CopyTo(mesh);
            else mesh.Clear();
        }
    }

    /// <summary>
    /// Polyline renderer (original LineRenderer contract, data-only). Holds the point
    /// buffer, widths, endpoint colors, and cap/corner tessellation counts that ported
    /// code writes; a render backend draws from the same state later.
    /// </summary>
    public class LineRenderer : Renderer
    {
        public bool useWorldSpace = true;
        public float startWidth = 1f;
        public float endWidth = 1f;
        public Color startColor = Color.white;
        public Color endColor = Color.white;
        public int numCapVertices;
        public int numCornerVertices;

        Vector3[] _positions = System.Array.Empty<Vector3>();

        /// <summary>
        /// Number of line points. Resizing preserves existing points; new slots are
        /// zero-initialized (original contract).
        /// </summary>
        public int positionCount
        {
            get => _positions.Length;
            set
            {
                int count = value < 0 ? 0 : value;
                if (count == _positions.Length) return;
                var next = new Vector3[count];
                System.Array.Copy(_positions, next, System.Math.Min(_positions.Length, count));
                _positions = next;
            }
        }

        public void SetPosition(int index, Vector3 position) => _positions[index] = position;

        public Vector3 GetPosition(int index) => _positions[index];

        /// <summary>Copies min(positions.Length, positionCount) points — does not resize (original contract).</summary>
        public void SetPositions(Vector3[] positions)
        {
            if (positions == null) return;
            System.Array.Copy(positions, _positions, System.Math.Min(positions.Length, _positions.Length));
        }

        /// <summary>Copies up to <paramref name="positions"/>.Length points out; returns the count copied.</summary>
        public int GetPositions(Vector3[] positions)
        {
            if (positions == null) return 0;
            int n = System.Math.Min(positions.Length, _positions.Length);
            System.Array.Copy(_positions, positions, n);
            return n;
        }
    }

    public class TrailRenderer : Renderer
    {
        public bool emitting = true;
        public float time;
        public float startWidth;
        public float endWidth;

        // Data-only trail shape/tint knobs (E18 — the Astro League ball's speed-reactive
        // trail sets them); a render backend reads them in the presentation phase.
        public int numCapVertices;
        public float minVertexDistance = 0.1f;
        public bool generateLightingData;
        public Color startColor = Color.white;
        public Color endColor = Color.white;

        Gradient _colorGradient = new();

        /// <summary>
        /// Color ramp over the trail's length (E14). Data-only: held by reference
        /// (callers that re-tint assign a fresh <see cref="Gradient"/>, the
        /// VesselTrailCustomization pattern); null assignment restores a default ramp.
        /// </summary>
        public Gradient colorGradient
        {
            get => _colorGradient;
            set => _colorGradient = value ?? new Gradient();
        }

        public void Clear() { }
    }

    /// <summary>Original-engine light types (UnityEngine.LightType, values frozen). Data-only headless.</summary>
    public enum LightType
    {
        Spot = 0,
        Directional = 1,
        Point = 2,
        Area = 3,
    }

    /// <summary>Original-engine shadow options (UnityEngine.LightShadows, values frozen). Data-only headless.</summary>
    public enum LightShadows
    {
        None = 0,
        Hard = 1,
        Soft = 2,
    }

    /// <summary>
    /// Data-only Light component (E18 — the renderer-stub family): holds the authored
    /// type/color/range/intensity so lighting setup code (e.g. the Astro League ball's
    /// speed-reactive point light) ports verbatim. A render backend interprets it in
    /// the presentation phase.
    /// </summary>
    public class Light : Behaviour
    {
        public LightType type = LightType.Spot;
        public Color color = Color.white;
        public float range = 10f;
        public float intensity = 1f;
        public LightShadows shadows = LightShadows.None;
    }
}
