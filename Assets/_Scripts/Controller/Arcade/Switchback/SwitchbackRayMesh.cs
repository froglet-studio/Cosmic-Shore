using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The geometry of the I4 gauntlet's path rays (<see cref="SwitchbackPathRays"/>): pure, so the headless tests
    /// (<c>SwitchbackBlastRunTests</c>, <c>run.sh tests</c>) hold it without a scene.
    /// </summary>
    public static class SwitchbackRayMesh
    {
        /// <summary>Facets round a ray.</summary>
        public const int Sides = 6;

        /// <summary>
        /// One ray from <paramref name="a"/> to <paramref name="b"/>: a <see cref="Sides"/>-sided prism of circumradius
        /// <paramref name="radius"/> with flat faces (each face its own vertices and normal, so it shades faceted) and
        /// capped ends, appended to the lists; every triangle winds front-out. Pure, for the headless tests.
        /// Returns the triangles added.
        /// </summary>
        public static int AppendBeam(Vector3 a, Vector3 b, float radius, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 0.5f || radius <= 0f) return 0;
            axis /= len;
            Vector3 e1 = RaceCourseGeometry.Perpendicular(axis), e2 = Vector3.Cross(axis, e1);
            int before = tris.Count;
            for (int i = 0; i < Sides; i++)
            {
                float t0 = i * 2f * Mathf.PI / Sides, t1 = (i + 1) * 2f * Mathf.PI / Sides;
                Vector3 c0 = (e1 * Mathf.Cos(t0) + e2 * Mathf.Sin(t0)) * radius;
                Vector3 c1 = (e1 * Mathf.Cos(t1) + e2 * Mathf.Sin(t1)) * radius;
                Vector3 n = (c0 + c1).normalized;
                Tri(a + c0, a + c1, b + c1, n, verts, normals, tris);
                Tri(a + c0, b + c1, b + c0, n, verts, normals, tris);
                Tri(a, a + c1, a + c0, -axis, verts, normals, tris);
                Tri(b, b + c0, b + c1, axis, verts, normals, tris);
            }
            return (tris.Count - before) / 3;
        }

        /// <summary>A triangle facing <paramref name="n"/> (its winding swapped if it would face away).</summary>
        static void Tri(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 n, List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) < 0f) (p1, p2) = (p2, p1);
            int at = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            normals.Add(n); normals.Add(n); normals.Add(n);
            tris.Add(at); tris.Add(at + 1); tris.Add(at + 2);
        }

    }
}
