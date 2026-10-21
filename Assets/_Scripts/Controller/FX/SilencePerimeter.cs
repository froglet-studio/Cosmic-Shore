using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The SHAPE of the silence perimeter (Docs/ElementalAbilitySystem/SILENCE.md §5): a ring of faceted 3D
    /// shards on the silenced pilot's screen edge, where a vignette would sit, each facet showing the frame
    /// behind it bent (reality breaking down). This holds where each shard sits and how it is cut; the
    /// drawing is <see cref="SilencePerimeterPass"/> and <c>SilencePerimeter.shader</c>.
    ///
    /// <para>Pure and deterministic, so the Silence Vignette Lab (<c>Docs/Studios/SilenceVignetteLab.html</c>)
    /// runs a line-for-line JavaScript twin of it (between its <c>&lt;perimeter-twin&gt;</c> markers) and
    /// <c>Tools/Build/silence_vignette_parity/run.sh</c> holds the two together. Random draws are an integer
    /// hash cut to 24 bits, exact in float and double, so both sides cut the same shards.</para>
    ///
    /// <para><b>Space.</b> Poses are in the camera's own space (+x right, +y up, +z forward, Unity's camera
    /// transform), at about <see cref="Depth"/> units out. The pass scales the whole arrangement about the
    /// camera to clear the near plane, which leaves the picture unchanged.</para>
    /// </summary>
    public static class SilencePerimeter
    {
        public const int ShapeCrystal = 0;
        public const int ShapeSlab = 1;
        public const float Depth = 4f;
        public const int MaxShards = 40;

        /// <summary>Everything that decides where the shards sit and how they are cut. Copied from
        /// <c>VesselSilenceConfigSO</c>; the lab's keys are its fields' names.</summary>
        public struct Settings
        {
            public int Shape;    // ShapeCrystal or ShapeSlab
            public int Count;    // shards in the ring
            public int Seed;     // the arrangement
            public float Size;   // shard length, x the screen's height
            public float Reach;  // how far in from the edge at full silence, x half the height
            public float Spin;   // radians a second about the long axis
        }

        /// <summary>One shard: its centre and its three axes, each scaled by its length (the geometry is
        /// one unit long along +y), its presence 0..1 and a per-shard random for the shader.</summary>
        public struct Pose
        {
            public Vector3 Position;
            public Vector3 Right;
            public Vector3 Up;
            public Vector3 Forward;
            public float Presence;
            public float Seed;
        }

        static uint Hash(uint h)
        {
            unchecked
            {
                h ^= h >> 16; h *= 0x7feb352du;
                h ^= h >> 15; h *= 0x846ca68bu;
                return h ^ (h >> 16);
            }
        }

        /// <summary>A stable random number 0..1 for shard <paramref name="shard"/>, draw <paramref name="k"/>.</summary>
        public static float Rand(int seed, int shard, int k)
        {
            unchecked
            {
                uint h = Hash((uint)seed * 0x9e3779b1u ^ (uint)(shard + 1) * 0x85ebca77u ^ (uint)(k + 1) * 0xc2b2ae3du);
                return (h >> 8) / 16777216f;
            }
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>Shard <paramref name="i"/>'s pose for a silence at <paramref name="weight"/> (0 hidden
        /// beyond the edge, 1 fully in), a hit flare <paramref name="pulse"/> (jolts it inward), at
        /// <paramref name="time"/> seconds, on a screen of width / height <paramref name="aspect"/> seen
        /// through a vertical field of view of <paramref name="fovDeg"/>.</summary>
        public static Pose Layout(int i, in Settings s, float weight, float pulse, float time, float aspect, float fovDeg)
        {
            int n = Mathf.Max(1, s.Count);
            float theta = 2f * Mathf.PI * (i + 0.8f * (Rand(s.Seed, i, 0) - 0.5f)) / n;
            float depth = Depth * (0.85f + 0.3f * Rand(s.Seed, i, 1));
            float h = depth * Mathf.Tan(fovDeg * 0.5f * Mathf.PI / 180f), w = h * aspect;
            float cx = Mathf.Cos(theta), cy = Mathf.Sin(theta);
            float tx = Mathf.Abs(cx) > 1e-4f ? w / Mathf.Abs(cx) : 1e9f, ty = Mathf.Abs(cy) > 1e-4f ? h / Mathf.Abs(cy) : 1e9f;
            float edge = Mathf.Min(tx, ty);
            float presence = Smooth(Clamp01(Clamp01(weight) * 1.35f - 0.35f * Rand(s.Seed, i, 2)));
            float len = s.Size * 2f * h * (0.6f + 0.8f * Rand(s.Seed, i, 3));
            float jolt = 1f + 0.6f * Mathf.Max(0f, pulse) * (0.5f + Rand(s.Seed, i, 4));
            float reach = s.Reach * h * presence * jolt;
            float sway = 0.04f * h * Mathf.Sin(time * (0.6f + Rand(s.Seed, i, 5)) + Rand(s.Seed, i, 6) * 6.2831855f);
            float along = edge + 0.6f * len * (1f - presence) - reach;
            float px = cx * along - cy * sway, py = cy * along + cx * sway;
            // The long axis points inward, turned a little in the screen's plane and tilted toward or away from you.
            float turn = (Rand(s.Seed, i, 7) - 0.5f) * 0.9f, tilt = (Rand(s.Seed, i, 8) - 0.5f) * 1.6f;
            float ax = -(cx * Mathf.Cos(turn) - cy * Mathf.Sin(turn)), ay = -(cx * Mathf.Sin(turn) + cy * Mathf.Cos(turn)), az = tilt;
            float al = Mathf.Sqrt(ax * ax + ay * ay + az * az); ax /= al; ay /= al; az /= al;
            float x0x = ay, x0y = -ax;                                    // cross(axis, forward), forward = (0, 0, 1)
            float xl = Mathf.Sqrt(x0x * x0x + x0y * x0y); x0x /= xl; x0y /= xl;
            float z0x = x0y * az, z0y = -x0x * az, z0z = x0x * ay - x0y * ax;   // cross(x0, axis)
            float phi = s.Spin * (0.5f + Rand(s.Seed, i, 9)) * time * (Rand(s.Seed, i, 10) < 0.5f ? -1f : 1f) + Rand(s.Seed, i, 11) * 6.2831855f;
            float c = Mathf.Cos(phi), sn = Mathf.Sin(phi);
            return new Pose
            {
                Position = new Vector3(px, py, depth),
                Right = new Vector3((x0x * c + z0x * sn) * len, (x0y * c + z0y * sn) * len, z0z * sn * len),
                Up = new Vector3(ax * len, ay * len, az * len),
                Forward = new Vector3((-x0x * sn + z0x * c) * len, (-x0y * sn + z0y * c) * len, z0z * c * len),
                Presence = presence,
                Seed = Rand(s.Seed, i, 12),
            };
        }

        /// <summary>Shard <paramref name="i"/>'s facets as flat triangles, one unit long along +y, appended to
        /// the lists: positions, normals and barycentrics (a quad's diagonal carries 1, so the shader draws only
        /// the real edges). Wound so cross(b - a, c - a) faces OUT, which is Unity's front face.</summary>
        public static void Geometry(in Settings s, int i, List<Vector3> positions, List<Vector3> normals, List<Vector3> barycentrics)
        {
            var g = new Builder { Positions = positions, Normals = normals, Barycentrics = barycentrics };
            int seed = s.Seed;
            float R(int k) => Rand(seed, i, 20 + k);
            if (s.Shape == ShapeSlab)
            {
                float hx = 0.32f * (0.8f + 0.4f * R(0)), hz = 0.18f * (0.8f + 0.4f * R(1)), shear = (R(2) - 0.5f) * 0.3f;
                Vector3 V(float x, float y, float z) => new Vector3(x * hx + y * shear * hx, y * 0.5f, z * hz);
                g.Quad(V(-1, -1, -1), V(1, -1, -1), V(1, 1, -1), V(-1, 1, -1));
                g.Quad(V(-1, -1, 1), V(-1, 1, 1), V(1, 1, 1), V(1, -1, 1));
                g.Quad(V(-1, -1, -1), V(-1, 1, -1), V(-1, 1, 1), V(-1, -1, 1));
                g.Quad(V(1, -1, -1), V(1, -1, 1), V(1, 1, 1), V(1, 1, -1));
                g.Quad(V(-1, 1, -1), V(1, 1, -1), V(1, 1, 1), V(-1, 1, 1));
                g.Quad(V(-1, -1, -1), V(-1, -1, 1), V(1, -1, 1), V(1, -1, -1));
                return;
            }
            int k = 5 + Mathf.FloorToInt(R(0) * 3f);
            float rad = 0.16f * (0.8f + 0.4f * R(1));
            var top = new Vector3((R(2) - 0.5f) * 0.1f, 0.5f, (R(3) - 0.5f) * 0.1f);
            var bottom = new Vector3((R(4) - 0.5f) * 0.1f, -0.5f * (0.6f + 0.4f * R(5)), (R(6) - 0.5f) * 0.1f);
            var lo = new Vector3[k];
            var hi = new Vector3[k];
            for (int j = 0; j < k; j++)
            {
                float ang = 2f * Mathf.PI * (j + 0.3f * (R(7 + j) - 0.5f)) / k, rr = rad * (0.8f + 0.4f * R(14 + j));
                lo[j] = new Vector3(Mathf.Cos(ang) * rr, -0.18f, Mathf.Sin(ang) * rr);
                hi[j] = new Vector3(Mathf.Cos(ang) * rr * 0.9f, 0.18f, Mathf.Sin(ang) * rr * 0.9f);
            }
            for (int j = 0; j < k; j++)
            {
                int j1 = (j + 1) % k;
                g.Quad(lo[j], lo[j1], hi[j1], hi[j]);
                g.Tri(hi[j], hi[j1], top, -1);
                g.Tri(lo[j1], lo[j], bottom, -1);
            }
        }

        struct Builder
        {
            public List<Vector3> Positions, Normals, Barycentrics;

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c, 1);
                Tri(a, c, d, 2);
            }

            // hide: the index (0..2) of the vertex opposite an edge that is not a real edge, or -1.
            public void Tri(Vector3 a, Vector3 b, Vector3 c, int hide)
            {
                float nx = (b.y - a.y) * (c.z - a.z) - (b.z - a.z) * (c.y - a.y);
                float ny = (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z);
                float nz = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                float mx = a.x + b.x + c.x, my = a.y + b.y + c.y, mz = a.z + b.z + c.z;
                if (nx * mx + ny * my + nz * mz < 0f)
                {
                    (b, c) = (c, b);
                    nx = -nx; ny = -ny; nz = -nz;
                    hide = hide == 1 ? 2 : hide == 2 ? 1 : hide;
                }
                float nl = Mathf.Sqrt(nx * nx + ny * ny + nz * nz); nx /= nl; ny /= nl; nz /= nl;
                var normal = new Vector3(nx, ny, nz);
                Positions.Add(a); Positions.Add(b); Positions.Add(c);
                for (int j = 0; j < 3; j++)
                {
                    Normals.Add(normal);
                    Barycentrics.Add(new Vector3(j == 0 || hide == 0 ? 1f : 0f, j == 1 || hide == 1 ? 1f : 0f, j == 2 || hide == 2 ? 1f : 0f));
                }
            }
        }
    }
}
