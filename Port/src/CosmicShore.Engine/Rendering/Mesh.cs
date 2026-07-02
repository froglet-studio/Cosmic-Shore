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
    public class Mesh : Object
    {
        Vector3[] _vertices = Array.Empty<Vector3>();
        Vector3[] _normals = Array.Empty<Vector3>();
        Vector2[] _uv = Array.Empty<Vector2>();
        Color[] _colors = Array.Empty<Color>();
        readonly List<int[]> _submeshes = new() { Array.Empty<int>() };
        Bounds _bounds;

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
            if (channel == 0) _uv = uvs?.ToArray() ?? Array.Empty<Vector2>();
        }

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
