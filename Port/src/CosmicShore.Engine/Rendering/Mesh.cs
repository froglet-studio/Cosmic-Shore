using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// Original-engine index buffer widths (UnityEngine.Rendering.IndexFormat).
    /// Data-only in the headless engine. Numeric values frozen to the original.
    /// </summary>
    public enum IndexFormat
    {
        UInt16 = 0,
        UInt32 = 1,
    }
}

namespace CosmicShore.Engine
{
    /// <summary>
    /// Original-contract mesh data container (the mesh arc). Headless-first: holds the
    /// vertex/normal/uv/color buffers, per-submesh index buffers, and bounds that ported
    /// code (OctahedronMeshGenerator, IcosphereMeshGenerator, VesselModelBuilder,
    /// CapsuleMembrane, the prism shields) reads and writes; a render backend draws from
    /// the same state later.
    ///
    /// Contract notes (all matching the original engine):
    ///   • Buffer property getters return COPIES — mutate the copy, then assign it back
    ///     (the PopulateMeshFaceScale pattern). Setters copy in.
    ///   • Setting <see cref="triangles"/> writes submesh 0 and resets
    ///     <see cref="subMeshCount"/> to 1; reading it concatenates all submeshes.
    ///   • <see cref="bounds"/> is settable and refreshed by <see cref="RecalculateBounds"/>.
    ///   • <see cref="MarkDynamic"/> is a GPU-upload hint — a no-op headless.
    ///   • Growing <see cref="subMeshCount"/> adds empty submeshes; shrinking drops the
    ///     tail. <see cref="SetTriangles(int[], int)"/> auto-grows to submesh+1 (small
    ///     port convenience over the original's set-count-first requirement, documented).
    /// </summary>
    public partial class Mesh : Object
    {
        Vector3[] _vertices = Array.Empty<Vector3>();
        Vector3[] _normals = Array.Empty<Vector3>();
        Vector2[] _uv = Array.Empty<Vector2>();
        Color[] _colors = Array.Empty<Color>();
        readonly List<int[]> _submeshes = new() { Array.Empty<int>() };
        Bounds _bounds;
        Vector4[] _tangents = Array.Empty<Vector4>();
        // uv (channel 0) lives in _uv; channels 1..7 (uv2..uv8) here.
        readonly Vector2[][] _extraUvs = new Vector2[7][];
        BoneWeight[] _boneWeights = Array.Empty<BoneWeight>();
        CosmicShore.Engine.Matrix4x4[] _bindposes = Array.Empty<CosmicShore.Engine.Matrix4x4>();
        readonly List<BlendShape> _blendShapes = new();

        sealed class BlendShapeFrame
        {
            public float Weight;
            public Vector3[] DeltaVertices, DeltaNormals, DeltaTangents;
        }

        sealed class BlendShape
        {
            public string Name;
            public readonly List<BlendShapeFrame> Frames = new();
        }

        /// <summary>Original contract: false when the importer did not keep a CPU copy (Read/Write off). Data stays readable headless.</summary>
        public bool isReadable { get; set; } = true;

        /// <summary>Index buffer width (original default UInt16). Data-only headless.</summary>
        public Rendering.IndexFormat indexFormat = Rendering.IndexFormat.UInt16;

        public int vertexCount => _vertices.Length;

        public Vector3[] vertices
        {
            get => Copy(_vertices);
            set => _vertices = Copy(value);
        }

        public Vector3[] normals
        {
            get => Copy(_normals);
            set => _normals = Copy(value);
        }

        public Vector2[] uv
        {
            get => Copy(_uv);
            set => _uv = Copy(value);
        }

        public Color[] colors
        {
            get => Copy(_colors);
            set => _colors = Copy(value);
        }

        /// <summary>Per-vertex tangents (xyz + handedness w).</summary>
        /// <summary>Vertex colors as bytes (a view over <see cref="colors"/>).</summary>
        public Color32[] colors32
        {
            get { var c = colors; if (c == null) return System.Array.Empty<Color32>(); var r = new Color32[c.Length]; for (int i = 0; i < c.Length; i++) r[i] = c[i]; return r; }
            set { if (value == null) { colors = null; return; } var r = new Color[value.Length]; for (int i = 0; i < value.Length; i++) r[i] = value[i]; colors = r; }
        }

        public Vector4[] tangents
        {
            get => Copy(_tangents);
            set => _tangents = Copy(value);
        }

        public Vector2[] uv2 { get => GetUvChannel(1); set => SetUvChannel(1, value); }
        public Vector2[] uv3 { get => GetUvChannel(2); set => SetUvChannel(2, value); }
        public Vector2[] uv4 { get => GetUvChannel(3); set => SetUvChannel(3, value); }
        public Vector2[] uv5 { get => GetUvChannel(4); set => SetUvChannel(4, value); }
        public Vector2[] uv6 { get => GetUvChannel(5); set => SetUvChannel(5, value); }
        public Vector2[] uv7 { get => GetUvChannel(6); set => SetUvChannel(6, value); }
        public Vector2[] uv8 { get => GetUvChannel(7); set => SetUvChannel(7, value); }

        Vector2[] GetUvChannel(int channel) => channel == 0 ? Copy(_uv) : Copy(_extraUvs[channel - 1]);

        void SetUvChannel(int channel, Vector2[] value)
        {
            if (channel == 0) _uv = Copy(value);
            else _extraUvs[channel - 1] = Copy(value);
        }

        /// <summary>Per-vertex skin weights (up to four influences), parallel to <see cref="vertices"/>.</summary>
        public BoneWeight[] boneWeights
        {
            get => Copy(_boneWeights);
            set => _boneWeights = Copy(value);
        }

        /// <summary>Inverse bind matrices, one per bone of the skinned renderer (bone-from-mesh space).</summary>
        public CosmicShore.Engine.Matrix4x4[] bindposes
        {
            get => Copy(_bindposes);
            set => _bindposes = Copy(value);
        }

        // ── Blend shapes (original contract) ─────────────────────────

        public int blendShapeCount => _blendShapes.Count;

        public string GetBlendShapeName(int shapeIndex) => _blendShapes[shapeIndex].Name;

        public int GetBlendShapeIndex(string blendShapeName)
        {
            for (int i = 0; i < _blendShapes.Count; i++)
                if (_blendShapes[i].Name == blendShapeName) return i;
            return -1;
        }

        public int GetBlendShapeFrameCount(int shapeIndex) => _blendShapes[shapeIndex].Frames.Count;

        public float GetBlendShapeFrameWeight(int shapeIndex, int frameIndex) => _blendShapes[shapeIndex].Frames[frameIndex].Weight;

        /// <summary>Copies a frame's deltas into the caller's arrays (each vertexCount long, or null to skip).</summary>
        public void GetBlendShapeFrameVertices(int shapeIndex, int frameIndex, Vector3[] deltaVertices, Vector3[] deltaNormals, Vector3[] deltaTangents)
        {
            var f = _blendShapes[shapeIndex].Frames[frameIndex];
            CopyInto(f.DeltaVertices, deltaVertices);
            CopyInto(f.DeltaNormals, deltaNormals);
            CopyInto(f.DeltaTangents, deltaTangents);
        }

        /// <summary>
        /// Appends a frame to the named shape (creating it). Frame weights must strictly
        /// increase within a shape and delta arrays must match vertexCount (null = zeros) —
        /// the original's argument checks.
        /// </summary>
        public void AddBlendShapeFrame(string shapeName, float frameWeight, Vector3[] deltaVertices, Vector3[] deltaNormals, Vector3[] deltaTangents)
        {
            if (deltaVertices != null && deltaVertices.Length != _vertices.Length)
                throw new ArgumentException("Blend shape delta vertex count must match the mesh vertex count.", nameof(deltaVertices));
            int index = GetBlendShapeIndex(shapeName);
            BlendShape shape;
            if (index < 0) { shape = new BlendShape { Name = shapeName }; _blendShapes.Add(shape); }
            else
            {
                shape = _blendShapes[index];
                if (shape.Frames.Count > 0 && frameWeight <= shape.Frames[^1].Weight)
                    throw new ArgumentException("Blend shape frame weights must be increasing.", nameof(frameWeight));
            }
            shape.Frames.Add(new BlendShapeFrame
            {
                Weight = frameWeight,
                DeltaVertices = deltaVertices != null ? Copy(deltaVertices) : new Vector3[_vertices.Length],
                DeltaNormals = deltaNormals != null ? Copy(deltaNormals) : null,
                DeltaTangents = deltaTangents != null ? Copy(deltaTangents) : null,
            });
        }

        public void ClearBlendShapes() => _blendShapes.Clear();

        static void CopyInto(Vector3[] source, Vector3[] destination)
        {
            if (destination == null) return;
            if (source == null) { Array.Clear(destination, 0, destination.Length); return; }
            Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
        }

        /// <summary>
        /// Concatenated indices across all submeshes on get; on set, becomes the single
        /// submesh 0 (subMeshCount resets to 1) — the original contract.
        /// </summary>
        public int[] triangles
        {
            get
            {
                int total = 0;
                foreach (var sub in _submeshes) total += sub.Length;
                var all = new int[total];
                int offset = 0;
                foreach (var sub in _submeshes)
                {
                    Array.Copy(sub, 0, all, offset, sub.Length);
                    offset += sub.Length;
                }
                return all;
            }
            set
            {
                _submeshes.Clear();
                _submeshes.Add(Copy(value));
            }
        }

        /// <summary>Number of submeshes (index buffers). Growing adds empty submeshes; shrinking drops the tail.</summary>
        public int subMeshCount
        {
            get => _submeshes.Count;
            set
            {
                int count = Math.Max(0, value);
                while (_submeshes.Count < count) _submeshes.Add(Array.Empty<int>());
                if (_submeshes.Count > count) _submeshes.RemoveRange(count, _submeshes.Count - count);
            }
        }

        /// <summary>Axis-aligned bounds in mesh-local space. Settable; refreshed by <see cref="RecalculateBounds"/>.</summary>
        public Bounds bounds
        {
            get => _bounds;
            set => _bounds = value;
        }

        // ── Buffer setters (List overloads used by IcosphereMeshGenerator) ──

        public void SetVertices(List<Vector3> inVertices) => _vertices = inVertices?.ToArray() ?? Array.Empty<Vector3>();
        public void SetVertices(Vector3[] inVertices) => _vertices = Copy(inVertices);

        public void SetNormals(List<Vector3> inNormals) => _normals = inNormals?.ToArray() ?? Array.Empty<Vector3>();
        public void SetNormals(Vector3[] inNormals) => _normals = Copy(inNormals);

        public void SetUVs(int channel, List<Vector2> uvs)
        {
            if (channel < 0 || channel > 7) throw new ArgumentOutOfRangeException(nameof(channel));
            SetUvChannel(channel, uvs?.ToArray() ?? Array.Empty<Vector2>());
        }

        public void SetUVs(int channel, Vector2[] uvs)
        {
            if (channel < 0 || channel > 7) throw new ArgumentOutOfRangeException(nameof(channel));
            SetUvChannel(channel, uvs);
        }

        /// <summary>Fills <paramref name="uvs"/> with the channel's coordinates (cleared first).</summary>
        public void GetUVs(int channel, List<Vector2> uvs)
        {
            if (uvs == null) return;
            uvs.Clear();
            if (channel < 0 || channel > 7) return;
            uvs.AddRange(channel == 0 ? _uv : _extraUvs[channel - 1] ?? Array.Empty<Vector2>());
        }

        public void SetTangents(List<Vector4> inTangents) => _tangents = inTangents?.ToArray() ?? Array.Empty<Vector4>();
        public void SetTangents(Vector4[] inTangents) => _tangents = Copy(inTangents);

        public void SetColors(List<Color> inColors) => _colors = inColors?.ToArray() ?? Array.Empty<Color>();

        /// <summary>Set the index buffer of one submesh. Auto-grows subMeshCount to <paramref name="submesh"/>+1.</summary>
        public void SetTriangles(int[] inTriangles, int submesh)
        {
            if (submesh < 0) throw new ArgumentOutOfRangeException(nameof(submesh));
            if (subMeshCount <= submesh) subMeshCount = submesh + 1;
            _submeshes[submesh] = Copy(inTriangles);
        }

        public void SetTriangles(List<int> inTriangles, int submesh)
            => SetTriangles(inTriangles?.ToArray() ?? Array.Empty<int>(), submesh);

        /// <summary>Indices of one submesh (copy).</summary>
        public int[] GetTriangles(int submesh)
            => submesh >= 0 && submesh < _submeshes.Count ? Copy(_submeshes[submesh]) : Array.Empty<int>();

        // ── Recalculation ────────────────────────────────────────────

        /// <summary>Local-space AABB over the vertex buffer. Empty mesh → zero bounds at the origin.</summary>
        public void RecalculateBounds()
        {
            if (_vertices.Length == 0)
            {
                _bounds = new Bounds(Vector3.zero, Vector3.zero);
                return;
            }

            Vector3 min = _vertices[0], max = _vertices[0];
            for (int i = 1; i < _vertices.Length; i++)
            {
                Vector3 v = _vertices[i];
                if (v.x < min.x) min.x = v.x;
                if (v.y < min.y) min.y = v.y;
                if (v.z < min.z) min.z = v.z;
                if (v.x > max.x) max.x = v.x;
                if (v.y > max.y) max.y = v.y;
                if (v.z > max.z) max.z = v.z;
            }
            _bounds = new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>
        /// Smooth per-vertex normals from the triangle topology: accumulate the (area-weighted)
        /// face cross products at each referenced vertex, then normalize — the original
        /// engine's shared-vertex smoothing. Flat-shaded meshes (unique verts per face)
        /// naturally come out per-face.
        /// </summary>
        public void RecalculateNormals()
        {
            var accumulated = new Vector3[_vertices.Length];
            foreach (var sub in _submeshes)
            {
                for (int i = 0; i + 2 < sub.Length; i += 3)
                {
                    int i0 = sub[i], i1 = sub[i + 1], i2 = sub[i + 2];
                    Vector3 faceNormal = Vector3.Cross(_vertices[i1] - _vertices[i0], _vertices[i2] - _vertices[i0]);
                    accumulated[i0] += faceNormal;
                    accumulated[i1] += faceNormal;
                    accumulated[i2] += faceNormal;
                }
            }
            for (int i = 0; i < accumulated.Length; i++)
                accumulated[i] = accumulated[i].sqrMagnitude > 1e-12f ? accumulated[i].normalized : Vector3.zero;
            _normals = accumulated;
        }

        /// <summary>Drop every buffer and submesh (one empty submesh remains, like the original).</summary>
        public void Clear()
        {
            _vertices = Array.Empty<Vector3>();
            _normals = Array.Empty<Vector3>();
            _uv = Array.Empty<Vector2>();
            _colors = Array.Empty<Color>();
            _tangents = Array.Empty<Vector4>();
            Array.Clear(_extraUvs, 0, _extraUvs.Length);
            _boneWeights = Array.Empty<BoneWeight>();
            _blendShapes.Clear();
            _submeshes.Clear();
            _submeshes.Add(Array.Empty<int>());
            _bounds = new Bounds(Vector3.zero, Vector3.zero);
        }

        /// <summary>GPU dynamic-buffer hint — a no-op in the headless engine.</summary>
        public void MarkDynamic() { }

        /// <summary>Deep copy of every buffer/submesh/bounds into <paramref name="destination"/> (BakeMesh / MeshFilter.mesh instancing).</summary>
        internal void CopyTo(Mesh destination)
        {
            if (destination is null) return;
            destination._vertices = Copy(_vertices);
            destination._normals = Copy(_normals);
            destination._uv = Copy(_uv);
            destination._colors = Copy(_colors);
            destination._tangents = Copy(_tangents);
            for (int i = 0; i < _extraUvs.Length; i++) destination._extraUvs[i] = _extraUvs[i] == null ? null : Copy(_extraUvs[i]);
            destination._boneWeights = Copy(_boneWeights);
            destination._bindposes = Copy(_bindposes);
            destination._blendShapes.Clear();
            foreach (var shape in _blendShapes)
            {
                var clone = new BlendShape { Name = shape.Name };
                foreach (var f in shape.Frames)
                    clone.Frames.Add(new BlendShapeFrame
                    {
                        Weight = f.Weight, DeltaVertices = Copy(f.DeltaVertices),
                        DeltaNormals = f.DeltaNormals == null ? null : Copy(f.DeltaNormals),
                        DeltaTangents = f.DeltaTangents == null ? null : Copy(f.DeltaTangents),
                    });
                destination._blendShapes.Add(clone);
            }
            destination.isReadable = isReadable;
            destination._submeshes.Clear();
            foreach (var sub in _submeshes) destination._submeshes.Add(Copy(sub));
            destination._bounds = _bounds;
            destination.indexFormat = indexFormat;
        }

        static T[] Copy<T>(T[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<T>();
            var copy = new T[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    /// <summary>
    /// Original-contract per-vertex skin influence: up to four (bone index, weight) pairs,
    /// weights normalized and sorted descending by the importer.
    /// </summary>
    public struct BoneWeight : IEquatable<BoneWeight>
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;

        public bool Equals(BoneWeight o)
            => boneIndex0 == o.boneIndex0 && boneIndex1 == o.boneIndex1 && boneIndex2 == o.boneIndex2 && boneIndex3 == o.boneIndex3
               && weight0 == o.weight0 && weight1 == o.weight1 && weight2 == o.weight2 && weight3 == o.weight3;
        public override bool Equals(object obj) => obj is BoneWeight o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(boneIndex0, boneIndex1, boneIndex2, boneIndex3, weight0, weight1, weight2, weight3);
        public static bool operator ==(BoneWeight a, BoneWeight b) => a.Equals(b);
        public static bool operator !=(BoneWeight a, BoneWeight b) => !a.Equals(b);
    }

    /// <summary>
    /// Original-contract mesh holder component. <see cref="sharedMesh"/> is the plain
    /// reference (assigning does not clone). <see cref="mesh"/> follows the original's
    /// instance-on-access semantics: the first get clones the shared mesh into an
    /// instance owned by this filter (named "<i>name</i> Instance") and returns it on
    /// every subsequent get; setting it adopts the given mesh as both the instance and
    /// the shared reference. Kept simple and data-only — no leak tracking.
    /// </summary>
    public class MeshFilter : Component
    {
        Mesh _sharedMesh;
        Mesh _instance;

        public Mesh sharedMesh
        {
            get => _sharedMesh;
            set
            {
                _sharedMesh = value;
                _instance = null; // a fresh instance is cloned from the new shared mesh on next .mesh get
            }
        }

        public Mesh mesh
        {
            get
            {
                if (_instance) return _instance;
                _instance = new Mesh { name = (_sharedMesh ? _sharedMesh.name : "Mesh") + " Instance" };
                if (_sharedMesh) _sharedMesh.CopyTo(_instance);
                _sharedMesh = _instance;
                return _instance;
            }
            set
            {
                _instance = value;
                _sharedMesh = value;
            }
        }
    }
}
