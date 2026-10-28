using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A SPACE creature's spindle, eroded (Docs/ECOSYSTEM.md §64.9): its thickest sections are thinned toward the spine
    /// by the lattice's Space exponent, its thin ones are left alone, so the whole body reads more spindly at the same
    /// length (<see cref="FaunaElementalForm.ErodedGirth"/>).
    ///
    /// <para>Measured once per source mesh, in the mesh's own (bind) space: the spine is the mesh's principal axis, its
    /// WIDTH the narrowest one, and the body is cut into <see cref="Sections"/> slices along the spine. A slice's half-width
    /// sets its girth factor; a vertex keeps its place along the spine and its offset from the spine is scaled by the
    /// factor at that place (linear between slice centres, so the skin does not crease). Normals take the map's
    /// inverse-transpose. The skin weights and bones are untouched, so the rig drives the eroded skin as it drove the
    /// prefab's.</para>
    ///
    /// <para>Pure geometry apart from the <see cref="Mesh"/> copy: no scene state is read, so every instance of a
    /// prefab shares one eroded mesh (the cache in <see cref="Fauna"/>).</para>
    /// </summary>
    public sealed class SpindleErosion
    {
        /// <summary>Slices along the spine. Enough to follow a body's taper, few enough that one slice holds many
        /// vertices of a ~3,000-vertex creature.</summary>
        public const int Sections = 24;

        readonly Vector3 _anchor, _spine;
        readonly float _start, _step;
        readonly float[] _girth = new float[Sections];

        /// <summary>The girth factor of the thickest section (<see cref="FaunaElementalForm.ThickestGirth"/> for a
        /// body that has a thick part at all, 1 for one already thinner than the law's reference everywhere).</summary>
        public float Thickest { get; }

        SpindleErosion(IReadOnlyList<Vector3> vertices, Vector3 anchor)
        {
            _anchor = anchor;
            PrincipalAxes(vertices, out _spine, out var width);

            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < vertices.Count; i++)
            {
                float s = Vector3.Dot(vertices[i] - anchor, _spine);
                lo = Mathf.Min(lo, s); hi = Mathf.Max(hi, s);
            }
            _start = lo;
            _step = Mathf.Max(1e-6f, (hi - lo) / Sections);

            // Each slice's half-width across the body's narrowest axis (a fin is thin across it, so a tall fin does not
            // read as a thick section), smoothed 1-2-1 so one ridge cannot crease the skin.
            var half = new float[Sections];
            for (int i = 0; i < vertices.Count; i++)
            {
                var d = vertices[i] - anchor;
                int k = Mathf.Clamp((int)((Vector3.Dot(d, _spine) - _start) / _step), 0, Sections - 1);
                half[k] = Mathf.Max(half[k], Mathf.Abs(Vector3.Dot(d, width)));
            }
            var smooth = (float[])half.Clone();
            for (int k = 1; k < Sections - 1; k++) smooth[k] = (half[k - 1] + 2f * half[k] + half[k + 1]) * 0.25f;

            float thickest = 0f;
            for (int k = 0; k < Sections; k++) thickest = Mathf.Max(thickest, smooth[k]);
            float reference = FaunaElementalForm.ErosionReference(thickest);
            float least = 1f;
            for (int k = 0; k < Sections; k++)
            {
                _girth[k] = FaunaElementalForm.ErodedGirth(smooth[k], reference);
                least = Mathf.Min(least, _girth[k]);
            }
            Thickest = least;
        }

        /// <summary>Measures a spindle. <paramref name="anchor"/> is the point the spine runs through, in the mesh's
        /// space - the creature's heart for its body, so its cavity closes concentrically round the heart.</summary>
        public static SpindleErosion Measure(IReadOnlyList<Vector3> vertices, Vector3 anchor) =>
            vertices is { Count: > 0 } ? new SpindleErosion(vertices, anchor) : null;

        /// <summary>The centroid of a vertex set: the anchor for a spindle that does not hold the heart.</summary>
        public static Vector3 Centroid(IReadOnlyList<Vector3> vertices)
        {
            var sum = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++) sum += vertices[i];
            return vertices.Count > 0 ? sum / vertices.Count : Vector3.zero;
        }

        /// <summary>The girth factor at a point (mesh space): 1 where the body is thin, down to <see cref="Thickest"/>.</summary>
        public float GirthAt(Vector3 point)
        {
            float x = (Vector3.Dot(point - _anchor, _spine) - _start) / _step - 0.5f;
            if (x <= 0f) return _girth[0];
            if (x >= Sections - 1) return _girth[Sections - 1];
            int k = (int)x;
            return Mathf.Lerp(_girth[k], _girth[k + 1], x - k);
        }

        /// <summary>How far a point lies from the spine (mesh space).</summary>
        public float OffSpine(Vector3 point)
        {
            var d = point - _anchor;
            return (d - _spine * Vector3.Dot(d, _spine)).magnitude;
        }

        /// <summary>Where a point of the prefab's body is on the eroded one (mesh space).</summary>
        public Vector3 Map(Vector3 point)
        {
            var d = point - _anchor;
            var along = _spine * Vector3.Dot(d, _spine);
            return _anchor + along + (d - along) * GirthAt(point);
        }

        /// <summary>A direction at <paramref name="point"/> carried by the map (its local linear part: the spine
        /// unchanged, across it scaled by the girth).</summary>
        public Vector3 MapDirection(Vector3 point, Vector3 direction)
        {
            var along = _spine * Vector3.Dot(direction, _spine);
            return along + (direction - along) * GirthAt(point);
        }

        Vector3 MapNormal(Vector3 point, Vector3 normal)
        {
            var along = _spine * Vector3.Dot(normal, _spine);
            return (along + (normal - along) / Mathf.Max(1e-4f, GirthAt(point))).normalized;
        }

        /// <summary>A copy of <paramref name="source"/> with every vertex mapped, then moved by <paramref name="shift"/>
        /// (mesh space: how far the spindle's root moved when the body it hangs from was eroded). Null when the source is
        /// not readable.</summary>
        public Mesh Erode(Mesh source, Vector3 shift)
        {
            if (!source || !source.isReadable) return null;
            var vertices = new List<Vector3>(source.vertexCount);
            var normals = new List<Vector3>(source.vertexCount);
            var tangents = new List<Vector4>(source.vertexCount);
            source.GetVertices(vertices);
            source.GetNormals(normals);
            source.GetTangents(tangents);
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                if (i < normals.Count) normals[i] = MapNormal(v, normals[i]);
                if (i < tangents.Count)
                {
                    var t = tangents[i];
                    var mapped = MapDirection(v, new Vector3(t.x, t.y, t.z)).normalized;
                    tangents[i] = new Vector4(mapped.x, mapped.y, mapped.z, t.w);
                }
                vertices[i] = Map(v) + shift;
            }
            var mesh = Object.Instantiate(source);
            mesh.name = source.name + " (Space spindle)";
            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count) mesh.SetNormals(normals);
            if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The longest and the shortest principal axis of a vertex cloud (a 3x3 covariance, Jacobi-rotated to
        /// its eigenvectors). A curved fin's long axis is oblique to its bounds, so the bounds cannot stand in.</summary>
        static void PrincipalAxes(IReadOnlyList<Vector3> vertices, out Vector3 longest, out Vector3 shortest)
        {
            var c = Centroid(vertices);
            var a = new float[3, 3];
            for (int i = 0; i < vertices.Count; i++)
            {
                var d = vertices[i] - c;
                for (int r = 0; r < 3; r++)
                for (int q = 0; q < 3; q++)
                    a[r, q] += d[r] * d[q];
            }
            var v = new float[3, 3] { { 1f, 0f, 0f }, { 0f, 1f, 0f }, { 0f, 0f, 1f } };
            for (int sweep = 0; sweep < 32; sweep++)
            {
                float off = Mathf.Abs(a[0, 1]) + Mathf.Abs(a[0, 2]) + Mathf.Abs(a[1, 2]);
                if (off < 1e-9f * (Mathf.Abs(a[0, 0]) + Mathf.Abs(a[1, 1]) + Mathf.Abs(a[2, 2]) + 1e-12f)) break;
                for (int p = 0; p < 2; p++)
                for (int q = p + 1; q < 3; q++)
                {
                    if (Mathf.Abs(a[p, q]) < 1e-12f) continue;
                    float theta = 0.5f * Mathf.Atan2(2f * a[p, q], a[q, q] - a[p, p]);
                    float cs = Mathf.Cos(theta), sn = Mathf.Sin(theta);
                    for (int k = 0; k < 3; k++)
                    {
                        float akp = a[k, p], akq = a[k, q];
                        a[k, p] = cs * akp - sn * akq; a[k, q] = sn * akp + cs * akq;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float apk = a[p, k], aqk = a[q, k];
                        a[p, k] = cs * apk - sn * aqk; a[q, k] = sn * apk + cs * aqk;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = cs * vkp - sn * vkq; v[k, q] = sn * vkp + cs * vkq;
                    }
                }
            }
            int hi = 0, lo = 0;
            for (int k = 1; k < 3; k++)
            {
                if (a[k, k] > a[hi, hi]) hi = k;
                if (a[k, k] < a[lo, lo]) lo = k;
            }
            longest = new Vector3(v[0, hi], v[1, hi], v[2, hi]).normalized;
            shortest = new Vector3(v[0, lo], v[1, lo], v[2, lo]).normalized;
        }
    }
}
