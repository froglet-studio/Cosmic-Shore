using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The meshes of the Meerkat's TREE and ROOTS (<c>R_VesselActions/MEERKAT.md</c> §7), the Vessel Studio's
    /// <c>buildTree</c> (rounds 13-15, 20) and <c>buildRootSkin</c> (round 17) from <c>Docs/Studios/VesselStudio/meerkat.html</c>.
    ///
    /// <para><b>The tree</b> is the tunnel's outer layer (round 14: "the wormhole is actually a portion of the trunk of the
    /// dead tree"): a log round the whole tunnel, <c>trunkTop</c> × the mouth at the way in (its top cut) to
    /// <c>trunkBase</c> × at the way out (its bottom cut), never closer to the tube than 1.15 × its wall, ridged
    /// (<c>trunkBark</c>, <c>trunkRidges</c>) and gnarled (<c>trunkGnarl</c>); its bore is the tunnel's own wall (round 20:
    /// the trunk IS the wormhole); the cut faces carry tree rings; and <c>branchCount</c> bare branches grow out of its top
    /// <c>branchZone</c>, in pairs, leaning <c>branchUp</c>, forking <c>branchForks</c> times.</para>
    ///
    /// <para>Its vertex data is the page's attributes, packed for <c>MeerkatTree.shader</c>: uv0 = (aS the share of the way
    /// along, aBi the branch index or -1, aCap 1 on a cut face, aR 0 at the mouth's rim to 1 at the bark), uv1 = (aB the
    /// share along a branch, aPhi round the wood), uv2 = aBase (the branch's root, which it grows out of).</para>
    /// </summary>
    public static class MeerkatTreeMesh
    {
        public const int TrunkRings = 56, TrunkSeg = 36, BranchSides = 8, MaxBranches = 16, RootSides = 12;

        /// <summary>The page's tree keys (the config's <c>trunk*</c> and <c>branch*</c>).</summary>
        public struct TreeSettings
        {
            public float Top, Base, Bark, Ridges, Gnarl, BranchCount, BranchLength, BranchThickness, BranchUp, BranchZone,
                BranchSymmetry, BranchForks;
        }

        /// <summary>One branch's root: the share of the way along the trunk it leaves at, and when the tunnel's front,
        /// rippling out of its centre, reaches it.</summary>
        public struct BranchRoot
        {
            public float U, TReach;
        }

        sealed class Builder
        {
            public readonly List<Vector3> Pos = new();
            public readonly List<Vector4> Uv0 = new();
            public readonly List<Vector2> Uv1 = new();
            public readonly List<Vector3> Uv2 = new();
            public readonly List<int> Idx = new();

            public void Push(Vector3 v, float s, float bi, float cap, float r, float b, Vector3 root, float phi)
            {
                Pos.Add(v);
                Uv0.Add(new Vector4(s, bi, cap, r));
                Uv1.Add(new Vector2(b, phi));
                Uv2.Add(root);
            }

            public void Grid(int start, int rows, int cols)
            {
                for (int r = 0; r < rows; r++)
                for (int j = 0; j < cols; j++)
                {
                    int a = start + r * (cols + 1) + j, b = a + cols + 1;
                    Idx.Add(a); Idx.Add(b); Idx.Add(a + 1);
                    Idx.Add(a + 1); Idx.Add(b); Idx.Add(b + 1);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(Pos);
                m.SetUVs(0, Uv0);
                m.SetUVs(1, Uv1);
                m.SetUVs(2, Uv2);
                m.SetIndices(Idx, MeshTopology.Triangles, 0, false);
                m.RecalculateNormals();
                m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
                return m;
            }
        }

        /// <summary>The page's tree seed: where the tunnel is, so every peer's tree is the same.</summary>
        public static uint Seed(Vector3 a, float L) =>
            (uint)(Mathf.FloorToInt(Mathf.Abs(a.x * 13.7f + a.y * 7.3f + a.z * 3.1f + L)) % 100000);

        /// <summary>The page's <c>fullR</c>: the tunnel's wall radius at s, the whole tunnel formed.</summary>
        static float FullR(MeerkatWormholeExecutor.Tunnel t, float s)
        {
            float k = 2f * s / t.L - 1f;
            return Mathf.Sqrt(t.B * t.B + (t.M * t.M - t.B * t.B) * k * k);
        }

        /// <summary>The page's <c>trunkR</c>: the trunk's radius at s, angle phi.</summary>
        static float TrunkR(MeerkatWormholeExecutor.Tunnel t, in TreeSettings p, float s, float phi, uint seed)
        {
            float u = s / t.L, baseR = Mathf.Max(FullR(t, s) * 1.15f, t.M * (p.Top + (p.Base - p.Top) * u));
            float ridge = Mathf.Abs(Mathf.Sin(0.5f * phi * Mathf.Max(3, Mathf.Round(p.Ridges)) + 0.35f * Mathf.Sin(u * 9f + seed)));
            float gnarl = Mathf.Sin(u * 6.3f + phi * 2f + seed) * Mathf.Sin(u * 2.1f - phi + seed * 0.7f);
            return baseR * (1f + Mathf.Max(0f, p.Bark) * (ridge - 0.6f) + Mathf.Max(0f, p.Gnarl) * gnarl);
        }

        /// <summary>The page's <c>whSurf</c> on a tunnel: a point at angle phi, radius r; <paramref name="normal"/> its outward normal.</summary>
        static Vector3 Surf(MeerkatWormholeExecutor.Tunnel t, float s, float phi, float r, out Vector3 normal)
        {
            var p = t.At(s, out _);
            t.Frame(s, out var n, out var bn);
            float c = Mathf.Cos(phi), sn = Mathf.Sin(phi);
            normal = n * c + bn * sn;
            return p + n * (r * c) + bn * (r * sn);
        }

        /// <summary>The page's <c>buildTree</c>: the trunk, its bore, its two cut faces and its branches round tunnel
        /// <paramref name="t"/>; <paramref name="roots"/> gets each branch's root.</summary>
        public static Mesh BuildTree(MeerkatWormholeExecutor.Tunnel t, in TreeSettings p, uint seed, List<BranchRoot> roots)
        {
            var g = new Builder();
            roots.Clear();
            // the trunk's bark
            for (int r = 0; r <= TrunkRings; r++)
            for (int j = 0; j <= TrunkSeg; j++)
            {
                float u = r / (float)TrunkRings, s = u * t.L, phi = j / (float)TrunkSeg * Mathf.PI * 2f;
                var q = Surf(t, s, phi, TrunkR(t, p, s, phi, seed), out _);
                g.Push(q, u, -1f, 0f, 1f, 0f, q, j / (float)TrunkSeg);
            }
            g.Grid(0, TrunkRings, TrunkSeg);
            // round 20: the bore - the tunnel's wall is the trunk's inside, in the same wood
            int bore = g.Pos.Count;
            for (int r = 0; r <= TrunkRings; r++)
            for (int j = 0; j <= TrunkSeg; j++)
            {
                float u = r / (float)TrunkRings, s = u * t.L, phi = j / (float)TrunkSeg * Mathf.PI * 2f;
                var q = Surf(t, s, phi, FullR(t, s), out _);
                g.Push(q, u, -1f, 0f, 1f, 0f, q, j / (float)TrunkSeg);
            }
            g.Grid(bore, TrunkRings, TrunkSeg);
            // the cut faces: the annulus from the mouth's rim out to the bark, its tree rings by aR
            for (int end = 0; end <= 1; end++)
            {
                float s = end * t.L, mouth = FullR(t, s);
                int start = g.Pos.Count;
                for (int j = 0; j <= TrunkSeg; j++)
                {
                    float phi = j / (float)TrunkSeg * Mathf.PI * 2f, outer = TrunkR(t, p, s, phi, seed);
                    var a = Surf(t, s, phi, mouth, out _);
                    g.Push(a, end, -1f, 1f, 0f, 0f, a, 0f);
                    var b = Surf(t, s, phi, outer, out _);
                    g.Push(b, end, -1f, 1f, 1f, 0f, b, 0f);
                }
                for (int j = 0; j < TrunkSeg; j++)
                {
                    int a = start + 2 * j;
                    g.Idx.Add(a); g.Idx.Add(a + 1); g.Idx.Add(a + 2);
                    g.Idx.Add(a + 2); g.Idx.Add(a + 1); g.Idx.Add(a + 3);
                }
            }
            // the branches: a tapering tube along a path that curls toward the top, crooked like dead wood
            var R = new MeerkatRootMath.Rng(seed + 17);
            float Rn() => (float)R.Next();
            Vector3 top = Vector3.zero;
            void Tube(List<Vector3> pts, float r0, float r1, int bi, float u, Vector3 root, float b0, float rootLen)
            {
                int start = g.Pos.Count, n = pts.Count;
                Vector3 N = Vector3.zero;
                float dist = 0f;
                for (int i = 0; i < n; i++)
                {
                    var T = (pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                    if (i == 0) { N = Vector3.up; if (Mathf.Abs(Vector3.Dot(N, T)) > 0.9f) N = Vector3.right; }
                    N = (N - T * Vector3.Dot(N, T)).normalized;
                    var Bv = Vector3.Cross(T, N);   // carried along the path (no twist)
                    if (i > 0) dist += Vector3.Distance(pts[i], pts[i - 1]);
                    float f = i / (float)(n - 1), rad = r0 + (r1 - r0) * f;
                    for (int j = 0; j <= BranchSides; j++)
                    {
                        float a = j / (float)BranchSides * Mathf.PI * 2f;
                        g.Push(pts[i] + N * (rad * Mathf.Cos(a)) + Bv * (rad * Mathf.Sin(a)), u, bi, 0f, 1f, b0 + dist / rootLen, root,
                               j / (float)BranchSides * 0.5f);
                    }
                }
                g.Grid(start, n - 1, BranchSides);
            }
            List<Vector3> Path(Vector3 at, Vector3 dir, float len, int segs, float curl)
            {
                var pts = new List<Vector3> { at };
                var d = dir.normalized;
                var c = at;
                float step = len / segs;
                for (int i = 0; i < segs; i++)
                {
                    var ax = Vector3.Cross(d, top);
                    if (ax.sqrMagnitude > 1e-8f) d = Quaternion.AngleAxis(curl / segs * Mathf.Rad2Deg, ax.normalized) * d;   // curling toward the top
                    d.x += (Rn() - 0.5f) * 0.25f; d.y += (Rn() - 0.5f) * 0.25f; d.z += (Rn() - 0.5f) * 0.25f;   // crooked
                    d.Normalize();
                    c += d * step;
                    pts.Add(c);
                }
                return pts;
            }
            void Limb(Vector3 at, Vector3 dir, float len, float r0, int forks, int bi, float u, Vector3 root, float b0, float rootLen, float sideSgn)
            {
                var pts = Path(at, dir, len, forks > 0 ? 6 : 4, 0.5f);
                Tube(pts, r0, r0 * 0.12f, bi, u, root, b0, rootLen);
                if (forks <= 0) return;
                foreach (var (f, sgn) in new[] { (0.45f, sideSgn), (0.72f, -sideSgn) })   // two forks, on alternate sides
                {
                    int i = Mathf.RoundToInt(f * (pts.Count - 1));
                    var p0 = pts[i];
                    var d = (pts[Mathf.Min(pts.Count - 1, i + 1)] - p0).normalized;
                    var sideV = Vector3.Cross(d, top);
                    if (sideV.sqrMagnitude < 1e-6f) sideV = Vector3.right;
                    sideV.Normalize();
                    var cd = d * Mathf.Cos(0.6f) + sideV * (sgn * Mathf.Sin(0.6f));
                    Limb(p0, cd, len * (0.55f - 0.15f * f), r0 * (1f - 0.88f * f) * 0.8f, forks - 1, bi, u, root, b0 + f * len / rootLen,
                         rootLen, -sgn);
                }
            }
            int nB = Mathf.Min(MaxBranches, Mathf.Max(0, Mathf.RoundToInt(p.BranchCount)));
            float zone = Mathf.Clamp01(p.BranchZone), sym = Mathf.Clamp01(p.BranchSymmetry), up = p.BranchUp * Mathf.Deg2Rad;
            int nPairs = (nB + 1) / 2, forksN = Mathf.Max(0, Mathf.Min(3, Mathf.RoundToInt(p.BranchForks)));
            for (int i = 0; i < nB; i++)
            {
                int k = i >> 1, second = i & 1;
                float loose = 1f - sym;
                float uK = 0.06f + Mathf.Max(0f, zone - 0.06f) * (nPairs > 1 ? k / (float)(nPairs - 1) : 0.5f);
                float u = Mathf.Clamp01(uK + loose * (Rn() - 0.5f) * (second == 1 ? 0.2f : 0.08f));
                float phi = Mathf.PI / 2f + k * 1.2f + (second == 1 ? Mathf.PI : 0f) + loose * (Rn() - 0.5f) * 1.4f;   // pairs face each other
                float s = u * t.L;
                t.At(s, out var tng);
                top = -tng;
                var root = Surf(t, s, phi, TrunkR(t, p, s, phi, seed) * 0.92f, out var wallN);
                var dir = wallN * Mathf.Cos(up) + top * Mathf.Sin(up);
                float len = t.M * Mathf.Max(0f, p.BranchLength) * (0.9f + 0.2f * Rn()), r0 = Mathf.Max(0.05f, p.BranchThickness * t.M);
                if (len < 0.5f) continue;
                Limb(root, dir, len, r0, forksN, roots.Count, u, root, 0f, len, second == 1 ? -1f : 1f);
                // the time the tunnel's front, rippling out of its centre (easeOutC), reaches this root
                float c3 = Mathf.Pow(1f - Mathf.Min(1f, Mathf.Abs(2f * u - 1f)), 1f / 3f);
                roots.Add(new BranchRoot { U = u, TReach = (1f - c3) * t.FormT });
            }
            return g.ToMesh("MeerkatTree");
        }

        /// <summary>The page's <c>buildRootSkin</c> (round 17): a root's bark, a tube that tapers with the channel.</summary>
        public static Mesh BuildRootSkin(MeerkatRootMath.Channel c, double rootTip)
        {
            var g = new Builder();
            int nA = Mathf.Max(4, Mathf.RoundToInt((float)c.L / 4f));
            for (int r = 0; r <= nA; r++)
            for (int j = 0; j <= RootSides; j++)
            {
                double s = c.L * r / nA, phi = j / (double)RootSides * System.Math.PI * 2;
                var p = MeerkatRootMath.At(c.Path, s, out _);
                MeerkatRootMath.Frame(c.Path, s, out var n, out var bn);
                double rad = MeerkatRootMath.Radius(c, s, rootTip);
                var q = p + n * (rad * System.Math.Cos(phi)) + bn * (rad * System.Math.Sin(phi));
                var v = new Vector3((float)q.X, (float)q.Y, (float)q.Z);
                g.Push(v, r / (float)nA, -1f, 0f, 1f, 0f, v, j / (float)RootSides);
            }
            g.Grid(0, nA, RootSides);
            return g.ToMesh("MeerkatRoot");
        }
    }
}
