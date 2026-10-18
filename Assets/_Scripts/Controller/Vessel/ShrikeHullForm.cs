using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's hull as a PURE function of its proportions (design: <c>R_VesselActions/SHRIKE.md</c> §2; the shape is
    /// the Shrike Studio's <c>makeHull</c>, with three.js's −Z nose turned to Unity's +Z): a long six-sided fuselage, a
    /// dark sensor head, the Void Lance's barrel slung under the nose, two forward-swept wings with domain stripes, a
    /// tail fin and the engine flame. No scene and no Unity object beyond the vector types, so <c>ShrikeTests</c> build
    /// it as-is.
    ///
    /// <para><b>Submeshes.</b> 0 is the plating, 1 the accents (barrel, stripes, fin, muzzle): <c>ShipHelper</c> paints
    /// the domain colour onto slot 1 of the hull's renderer.</para>
    ///
    /// <para><b>Element morphs</b> (each 0..1, the Notion document's mapping): Charge thickens the lance's barrel, Mass
    /// fattens the fuselage, Space lengthens the barrel (the reach of the lock), Time spreads the wings and lengthens the
    /// flame.</para>
    /// </summary>
    public static class ShrikeHullForm
    {
        public const int BodySubmesh = 0;
        public const int AccentSubmesh = 1;

        public struct Settings
        {
            public float BodyRadius, BodyLength;
            public float HeadRadius, HeadLength, HeadZ;
            public float BarrelRadius, BarrelLength, BarrelZ, BarrelY;
            public float WingSpan, WingChord, WingThickness, WingZ, WingSweep;
            public float FinHeight, FinLength, FinZ;
            public float FlameRadius, FlameLength;
        }

        /// <summary>The page's <c>makeHull</c> numbers.</summary>
        public static Settings Defaults => new()
        {
            BodyRadius = 1.1f, BodyLength = 7f,
            HeadRadius = 0.9f, HeadLength = 1.76f, HeadZ = 3.6f,
            BarrelRadius = 0.21f, BarrelLength = 7.5f, BarrelZ = 2.6f, BarrelY = -0.75f,
            WingSpan = 4.4f, WingChord = 1.4f, WingThickness = 0.16f, WingZ = -0.6f, WingSweep = 0.42f,
            FinHeight = 1.4f, FinLength = 1.8f, FinZ = -2.8f,
            FlameRadius = 0.55f, FlameLength = 1.6f,
        };

        /// <summary>The settings at the given element weights (charge, mass, space, time; each 0..1).</summary>
        public static Settings Morph(Settings s, float charge, float mass, float space, float time)
        {
            s.BarrelRadius *= 1f + 0.8f * Mathf.Clamp01(charge);
            s.BodyRadius *= 1f + 0.3f * Mathf.Clamp01(mass);
            s.HeadRadius *= 1f + 0.3f * Mathf.Clamp01(mass);
            s.BarrelLength *= 1f + 0.4f * Mathf.Clamp01(space);
            s.WingSpan *= 1f + 0.25f * Mathf.Clamp01(time);
            s.FlameLength *= 1f + 0.6f * Mathf.Clamp01(time);
            return s;
        }

        /// <summary>The muzzle: the barrel's front, where the lance's beam starts.</summary>
        public static Vector3 Muzzle(Settings s) => new(0f, s.BarrelY, s.BarrelZ + s.BarrelLength * 0.5f);

        /// <summary>One mesh: flat-shaded, two submeshes.</summary>
        public sealed class MeshData
        {
            public readonly List<Vector3> Verts = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Body = new();
            public readonly List<int> Accent = new();
        }

        /// <summary>The hull (everything but the flame), in hull space.</summary>
        public static MeshData Hull(Settings s)
        {
            var m = new MeshData();
            float half = s.BodyLength * 0.5f;
            // The fuselage: base at the tail, point at the nose.
            Frustum(m, Vector3.zero, -half, s.BodyRadius, half, 0f, 6, accent: false);
            // The sensor head, dark plating, its point further forward.
            Frustum(m, Vector3.zero, s.HeadZ - s.HeadLength * 0.5f, s.HeadRadius, s.HeadZ + s.HeadLength * 0.5f, 0f, 6, accent: false);
            // The lance's barrel and its muzzle.
            float bz = s.BarrelZ - s.BarrelLength * 0.5f;
            Frustum(m, new Vector3(0f, s.BarrelY, 0f), bz, s.BarrelRadius * 1.15f, bz + s.BarrelLength, s.BarrelRadius * 0.85f, 8, accent: true);
            Vector3 muzzle = Muzzle(s);
            Box(m, muzzle, Vector3.one * (s.BarrelRadius * 1.5f), Quaternion.identity, accent: true);
            // Wings (forward-swept, as the page draws them) and their stripes.
            for (int side = -1; side <= 1; side += 2)
            {
                var rot = Quaternion.AngleAxis(-side * s.WingSweep * Mathf.Rad2Deg, Vector3.up);
                Vector3 centre = new(side * s.WingSpan * 0.52f, 0f, s.WingZ);
                Box(m, centre, new Vector3(s.WingSpan * 0.5f, s.WingThickness * 0.5f, s.WingChord * 0.5f), rot, accent: false);
                Vector3 stripe = centre + rot * new Vector3(side * s.WingSpan * 0.18f, s.WingThickness * 0.5f + 0.01f, 0.2f);
                Box(m, stripe, new Vector3(s.WingSpan * 0.25f, 0.02f, 0.18f), rot, accent: true);
            }
            // The tail fin.
            Box(m, new Vector3(0f, s.FinHeight * 0.5f, s.FinZ), new Vector3(0.08f, s.FinHeight * 0.5f, s.FinLength * 0.5f), Quaternion.identity, accent: true);
            return m;
        }

        /// <summary>The engine flame: a cone from the tail plane backward, its root at the local origin (so its z scale
        /// is its length). The animation scales it with speed.</summary>
        public static MeshData Flame(Settings s)
        {
            var m = new MeshData();
            Frustum(m, Vector3.zero, -s.FlameLength, 0f, 0f, s.FlameRadius, 8, accent: true);
            return m;
        }

        /// <summary>Where the flame roots: the fuselage's tail plane.</summary>
        public static Vector3 FlameRoot(Settings s) => new(0f, 0f, -s.BodyLength * 0.5f);

        /// <summary>The signed volume a closed mesh encloses: positive when every face winds outward (the divergence
        /// theorem over its triangles). Tests hold each primitive to it.</summary>
        public static float SignedVolume(MeshData m)
        {
            float v = 0f;
            foreach (var list in new[] { m.Body, m.Accent })
                for (int i = 0; i + 2 < list.Count; i += 3)
                    v += Vector3.Dot(m.Verts[list[i]], Vector3.Cross(m.Verts[list[i + 1]], m.Verts[list[i + 2]])) / 6f;
            return v;
        }

        // ---- primitives (flat shaded, outward winding: the normal is cross(b - a, c - a), Unity's front face) ----

        static void Tri(MeshData m, Vector3 a, Vector3 b, Vector3 c, bool accent)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = m.Verts.Count;
            m.Verts.Add(a); m.Verts.Add(b); m.Verts.Add(c);
            m.Normals.Add(n); m.Normals.Add(n); m.Normals.Add(n);
            m.Uvs.Add(Vector2.zero); m.Uvs.Add(Vector2.right); m.Uvs.Add(Vector2.up);
            var list = accent ? m.Accent : m.Body;
            list.Add(i); list.Add(i + 1); list.Add(i + 2);
        }

        static void Quad(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool accent)
        {
            Tri(m, a, b, c, accent);
            Tri(m, a, c, d, accent);
        }

        /// <summary>An n-sided frustum along +Z from (z0, r0) to (z1, r1), offset by <paramref name="at"/>, capped.</summary>
        public static void Frustum(MeshData m, Vector3 at, float z0, float r0, float z1, float r1, int sides, bool accent)
        {
            for (int k = 0; k < sides; k++)
            {
                float a0 = (k + 0.5f) / sides * Mathf.PI * 2f, a1 = (k + 1.5f) / sides * Mathf.PI * 2f;
                Vector3 d0 = new(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                Vector3 p00 = at + d0 * r0 + Vector3.forward * z0, p01 = at + d1 * r0 + Vector3.forward * z0;
                Vector3 p10 = at + d0 * r1 + Vector3.forward * z1, p11 = at + d1 * r1 + Vector3.forward * z1;
                // side: the normal cross(b - a, c - a) points away from the axis
                if (r0 > 0f && r1 > 0f) Quad(m, p00, p01, p11, p10, accent);
                else if (r1 <= 0f) Tri(m, p00, p01, p10, accent);
                else Tri(m, p00, p11, p10, accent);
                // caps: the z0 cap faces -Z, the z1 cap +Z
                Vector3 c0 = at + Vector3.forward * z0, c1 = at + Vector3.forward * z1;
                if (r0 > 0f) Tri(m, c0, p01, p00, accent);
                if (r1 > 0f) Tri(m, c1, p10, p11, accent);
            }
        }

        public static void Box(MeshData m, Vector3 centre, Vector3 half, Quaternion rot, bool accent)
        {
            Vector3 P(float x, float y, float z) => centre + rot * Vector3.Scale(half, new Vector3(x, y, z));
            Quad(m, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), accent);      // +x
            Quad(m, P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), accent);  // -x
            Quad(m, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), accent);      // +y
            Quad(m, P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), accent);  // -y
            Quad(m, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), accent);      // +z
            Quad(m, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), accent);  // -z
        }
    }
}
