using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The wrecking ball's mesh as a PURE function (design: <c>R_VesselActions/THRESHER.md</c> §5): a
    /// flat-faceted MORNING STAR — a once-subdivided icosahedron (80 facets) studded with twelve
    /// five-sided spikes on the icosahedron's own vertices — in place of the runtime sphere and six cubes it
    /// replaced. One mesh, one renderer, one draw. Diameter 1 at the core (radius 0.5), so the executor's
    /// existing scale (<c>2 × BallRadius × ballVisualScale</c>) sizes it exactly as it sized the sphere; the
    /// spikes stand proud of that as the old studs did (tips at <see cref="SpikeTipRadius"/>).
    ///
    /// <para><b>Why facets and spikes, on the fresnel material.</b> <c>ThresherBallFresnelShader</c> is
    /// <c>lerp(dark, bright, (1 − N·V)^1.5)</c>. Flat facets give the body a faceted sparkle as it rolls, and
    /// each spike's five faces swing from face-on (dull) to edge-on (rim-bright) over a turn — the roll is
    /// read from the spikes, the job the cube studs did, with the twelve evenly spread so no axis of roll
    /// hides them.</para>
    ///
    /// <para>Run offline by <c>Tools/Build/thresher_hull_harness</c> (watertight per piece, every face wound
    /// outward, the spikes on the icosahedron's twelve five-fold axes).</para>
    /// </summary>
    public static class ThresherBallForm
    {
        /// <summary>The core's radius: the gameplay ball's radius in the mesh's own units.</summary>
        public const float CoreRadius = 0.5f;
        /// <summary>How far a spike's tip stands from the centre.</summary>
        public const float SpikeTipRadius = 0.7f;
        /// <summary>A spike's base, as a radius on the core's surface.</summary>
        public const float SpikeBaseRadius = 0.13f;
        /// <summary>Faces around a spike.</summary>
        public const int SpikeSides = 5;

        /// <summary>The icosahedron's twelve vertices (unit length): the core's corners and the spikes' axes.</summary>
        public static Vector3[] IcosahedronVertices()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        static readonly int[] IcosahedronFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        /// <summary>Build the ball, flat-shaded (three unshared vertices per triangle).</summary>
        public static void Build(List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            verts.Clear(); normals.Clear(); tris.Clear();
            var ico = IcosahedronVertices();

            // The core: every icosahedron face split into four, pushed onto the sphere.
            for (int f = 0; f < IcosahedronFaces.Length; f += 3)
            {
                var a = ico[IcosahedronFaces[f]]; var b = ico[IcosahedronFaces[f + 1]]; var c = ico[IcosahedronFaces[f + 2]];
                var ab = (a + b).normalized; var bc = (b + c).normalized; var ca = (c + a).normalized;
                Tri(verts, normals, tris, a * CoreRadius, ab * CoreRadius, ca * CoreRadius, Vector3.zero);
                Tri(verts, normals, tris, ab * CoreRadius, b * CoreRadius, bc * CoreRadius, Vector3.zero);
                Tri(verts, normals, tris, ca * CoreRadius, bc * CoreRadius, c * CoreRadius, Vector3.zero);
                Tri(verts, normals, tris, ab * CoreRadius, bc * CoreRadius, ca * CoreRadius, Vector3.zero);
            }

            // The spikes: a five-sided pyramid on each vertex, its base sunk a little into the core so no
            // seam shows at a glancing angle, capped underneath (each spike is its own closed solid).
            for (int i = 0; i < ico.Length; i++)
            {
                var axis = ico[i];
                var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var w = Vector3.Cross(axis, u);
                var baseC = axis * (CoreRadius * 0.94f);
                var tip = axis * SpikeTipRadius;
                var ring = new Vector3[SpikeSides];
                for (int k = 0; k < SpikeSides; k++)
                {
                    float th = k * Mathf.PI * 2f / SpikeSides;
                    ring[k] = baseC + (u * Mathf.Cos(th) + w * Mathf.Sin(th)) * SpikeBaseRadius;
                }
                var inside = baseC + (tip - baseC) * 0.25f;
                for (int k = 0; k < SpikeSides; k++)
                    Tri(verts, normals, tris, ring[k], ring[(k + 1) % SpikeSides], tip, inside);
                for (int k = 1; k < SpikeSides - 1; k++)
                    Tri(verts, normals, tris, ring[0], ring[k], ring[k + 1], inside, -axis);
            }
        }

        static void Tri(List<Vector3> verts, List<Vector3> normals, List<int> tris, Vector3 a, Vector3 b, Vector3 c,
            Vector3 inside, Vector3? hint = null)
        {
            var n = Vector3.Cross(b - a, c - a);
            var outward = hint ?? ((a + b + c) / 3f - inside);
            if (Vector3.Dot(n, outward) < 0f) { var t = b; b = c; c = t; n = -n; }
            n = n.normalized;
            int i0 = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        }
    }
}
