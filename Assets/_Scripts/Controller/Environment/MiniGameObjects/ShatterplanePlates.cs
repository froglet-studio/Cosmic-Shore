using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure geometry of Shatterplane's arena (<see cref="SpawnableShatterplane"/>): wide, one-prism-thick round
    /// PLATES of neutral prisms hanging in the cell, built for the one thing the Brittle Star's ring does best. Settings
    /// in, structures, tiles and crystal sites out; no Unity object is touched, so the tests and
    /// <c>Tools/Build/shatterplane_budget.py</c> (its line-for-line Python twin) read the same layout the game lays. See
    /// <c>Assets/_Scripts/Controller/Arcade/SHATTERPLANE.md</c>.
    ///
    /// <para><b>Why plates.</b> The thrown ring bursts in a flat disk across its line of flight, about as wide as the
    /// cell's nucleus and under 2 u thick. Square to a plate, that disk lies IN the plate and takes the whole of it;
    /// a few degrees off, it cuts one band across it. The guns break one prism a round from any angle. So a plate is
    /// the shape that turns the ring into the spike of the match, and squaring up is the skill.</para>
    ///
    /// <para><b>A structure is a stack of plates</b> sharing one axis: one plate, or (I3, the Shelves) several parallel
    /// plates <see cref="Settings.ShelfGap"/> apart, so a square ring peels the front one and the next waits behind it.
    /// Its axis <see cref="Structure.N"/> always points AWAY from the arena centre (the side a pilot comes in from), and
    /// its crystal sites float on that axis, in front, so taking a crystal lines you up square. Every
    /// <see cref="Settings.ScreenEvery"/>th structure (I4) has a SCREEN in front of it: a super-shielded grille offset to
    /// one side, which nothing breaks and which a ring blows on, so the square line runs beside it.</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62). Tiles are a sparse lattice (a
    /// <see cref="Fill"/> share of their cell), structures never touch (each is placed only where its bounding sphere
    /// clears every earlier one by <see cref="Gap"/>), and a stack's plates and its screen stand tens of units apart.</para>
    ///
    /// <para><b>Counts are integer arithmetic.</b> Which tiles a plate has depends only on integers (its radius in
    /// tiles, the grid indices), so the count is exact in the twin as long as every structure is placed; the generator
    /// asserts the twin places them all, and the tests assert the C# does.</para>
    /// </summary>
    public static class ShatterplanePlates
    {
        /// <summary>The grid's pitch (u): one tile per cell.</summary>
        public const float Tile = 8f;
        /// <summary>A tile's edge as a share of its cell: a sparse lattice, so a stray round can pass a gap while the
        /// ring's disk takes every tile in its plane.</summary>
        public const float Fill = 0.625f;
        /// <summary>A tile's thickness (u): a plate is a thin sheet.</summary>
        public const float Thickness = 2f;
        /// <summary>Clear space kept between two structures' bounding spheres (u): room to fly between them.</summary>
        public const float Gap = 40f;
        /// <summary>Placement draws per structure before it is given up (the generator asserts every one lands).</summary>
        public const int MaxAttempts = 64;
        /// <summary>How far in front of a structure's front plate its first crystal site floats (u): far enough that a
        /// pilot who takes it is already lined up, near enough that a ring thrown from there reaches the plate.</summary>
        public const float CrystalOffset = 200f;
        /// <summary>How much farther out each further crystal on the same axis floats (u).</summary>
        public const float CrystalStep = 120f;

        /// <summary>Everything that shapes a layout: the serialized fields of a <see cref="SpawnableShatterplane"/>.</summary>
        public struct Settings
        {
            public int Seed;
            public float InnerRadius, OuterRadius;
            public int StructureCount;
            public int PlateRadius;          // tiles
            public int PlatesPerStack;
            public float ShelfGap;           // u, between a stack's plates
            public bool FaceOut;             // every axis points straight out from the centre (I1)
            public int RibEvery;             // every Nth interior grid line is super-shielded; 0 = none
            public bool DangerRim;           // a plate's outer ring of tiles is danger
            public int ScreenEvery;          // every Nth structure has a screen in front; 0 = none
            public int ScreenRadius;         // tiles
            public float ScreenOffset;       // u in front of the front plate
            public int ScreenRibEvery;       // the grille's pitch in tiles
        }

        /// <summary>One placed structure: its centre, axis N (pointing away from the arena centre), cross axes U, W.</summary>
        public readonly struct Structure
        {
            public readonly int Index;
            public readonly Vector3 Centre, N, U, W;
            public readonly float Radius;
            public readonly bool Screen;

            public Structure(int index, Vector3 centre, Vector3 n, float radius, bool screen)
            {
                Index = index;
                Centre = centre;
                N = n;
                Radius = radius;
                Screen = screen;
                Basis(n, out U, out W);
            }
        }

        /// <summary>One laid tile, in the arena's local space.</summary>
        public readonly struct TileLay
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly PrismKind Kind;
            public readonly int Structure;

            public TileLay(Vector3 position, Quaternion rotation, PrismKind kind, int structure)
            {
                Position = position;
                Rotation = rotation;
                Kind = kind;
                Structure = structure;
            }
        }

        /// <summary>The size every tile is laid at.</summary>
        public static Vector3 TileScale => new(Tile * Fill, Tile * Fill, Thickness);

        /// <summary>A frame's cross axes: perpendicular to its axis, the same rule in the twin.</summary>
        public static void Basis(Vector3 n, out Vector3 u, out Vector3 w)
        {
            Vector3 t = Mathf.Abs(n.x) < 0.9f ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 1f, 0f);
            u = Vector3.Cross(n, t).normalized;
            w = Vector3.Cross(n, u);
        }

        /// <summary>Half a stack's depth (u): from its centre plane to its front plate.</summary>
        public static float HalfDepth(Settings s) => (Mathf.Max(1, s.PlatesPerStack) - 1) * 0.5f * s.ShelfGap;

        /// <summary>How far sideways a screen stands from the axis (u): half the plate, so half the plate is behind it.</summary>
        public static float ScreenShift(Settings s) => s.PlateRadius * Tile * 0.5f;

        /// <summary>A structure's bounding radius (u): its plates, and its screen when it has one, plus a tile.</summary>
        public static float BoundingRadius(Settings s, bool screen)
        {
            float rp = (s.PlateRadius + 0.5f) * Tile;
            float hd = HalfDepth(s) + Thickness;
            float r = Mathf.Sqrt(rp * rp + hd * hd);
            if (screen)
            {
                float front = HalfDepth(s) + s.ScreenOffset;
                float shift = ScreenShift(s);
                float rs = Mathf.Sqrt(front * front + shift * shift) + (s.ScreenRadius + 0.5f) * Tile;
                r = Mathf.Max(r, rs);
            }
            return r + Tile;
        }

        /// <summary>A unit vector from two hashes: uniform on the sphere.</summary>
        static Vector3 UnitFrom(float h1, float h2)
        {
            float z = 2f * h1 - 1f;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float phi = 2f * Mathf.PI * h2;
            return new Vector3(ring * Mathf.Cos(phi), ring * Mathf.Sin(phi), z);
        }

        /// <summary>
        /// Place every structure. Each takes the first of up to <see cref="MaxAttempts"/> hashed draws whose bounding
        /// sphere clears every earlier one by <see cref="Gap"/>; positions are spread evenly by volume through the shell
        /// between <see cref="Settings.InnerRadius"/> and <see cref="Settings.OuterRadius"/>. An axis points straight out
        /// (<see cref="Settings.FaceOut"/>) or is drawn at random and turned to face away from the centre. Returns how
        /// many were placed.
        /// </summary>
        public static int Layout(Settings s, List<Structure> into)
        {
            into.Clear();
            float r0 = s.InnerRadius * s.InnerRadius * s.InnerRadius;
            float r1 = s.OuterRadius * s.OuterRadius * s.OuterRadius;
            for (int k = 0; k < s.StructureCount; k++)
            {
                bool screen = s.ScreenEvery > 0 && k % s.ScreenEvery == s.ScreenEvery - 1;
                float radius = BoundingRadius(s, screen);
                for (int t = 0; t < MaxAttempts; t++)
                {
                    int key = s.Seed * 104729 + k * 1031 + t * 7;   // 64 draws x 7 < 1031: no two structures share a key
                    float rr = Mathf.Pow(r0 + Hash01(key) * (r1 - r0), 1f / 3f);
                    Vector3 c = UnitFrom(Hash01(key + 1), Hash01(key + 2)) * rr;
                    Vector3 n = s.FaceOut ? c.normalized : UnitFrom(Hash01(key + 3), Hash01(key + 4));
                    if (Vector3.Dot(n, c) < 0f) n = -n;
                    bool clear = true;
                    for (int j = 0; j < into.Count && clear; j++)
                    {
                        float need = radius + into[j].Radius + Gap;
                        clear = (into[j].Centre - c).sqrMagnitude >= need * need;
                    }
                    if (!clear) continue;
                    into.Add(new Structure(k, c, n, radius, screen));
                    break;
                }
            }
            return into.Count;
        }

        /// <summary>
        /// Every tile of every structure, in structure order, then plate order (back to front), then grid order, then the
        /// screen. A plate is every grid cell inside its radius; its outer ring is danger when
        /// <see cref="Settings.DangerRim"/>, and every <see cref="Settings.RibEvery"/>th interior line is a super-shielded
        /// rib. A screen is the super-shielded grid lines of a smaller disc, nothing between them. Returns the number laid.
        /// </summary>
        public static int Tiles(IReadOnlyList<Structure> structures, Settings s, List<TileLay> into)
        {
            into?.Clear();
            int laid = 0;
            int plates = Mathf.Max(1, s.PlatesPerStack);
            float hd = HalfDepth(s);
            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                for (int p = 0; p < plates; p++)
                {
                    Vector3 centre = st.Centre + st.N * (p * s.ShelfGap - hd);
                    laid += Plate(st, centre, s, into);
                }
                if (st.Screen)
                {
                    Vector3 centre = st.Centre + st.N * (hd + s.ScreenOffset) + st.U * ScreenShift(s);
                    laid += Screen(st, centre, s, into);
                }
            }
            return laid;
        }

        static int Plate(Structure st, Vector3 centre, Settings s, List<TileLay> into)
        {
            var rot = Quaternion.LookRotation(st.N, st.U);
            int r = s.PlateRadius, rimInside = (r - 1) * (r - 1), outside = r * r;
            int laid = 0;
            for (int a = -r; a <= r; a++)
            for (int b = -r; b <= r; b++)
            {
                int d2 = a * a + b * b;
                if (d2 > outside) continue;
                PrismKind kind;
                bool rim = d2 > rimInside;
                if (s.DangerRim && rim) kind = PrismKind.Danger;
                else if (!rim && s.RibEvery > 0 && (a % s.RibEvery == 0 || b % s.RibEvery == 0))
                    kind = PrismKind.SuperShielded;   // interior lines only: a plate's skeleton, never its rim
                else kind = PrismKind.Plain;
                laid++;
                into?.Add(new TileLay(centre + st.U * (a * Tile) + st.W * (b * Tile), rot, kind, st.Index));
            }
            return laid;
        }

        static int Screen(Structure st, Vector3 centre, Settings s, List<TileLay> into)
        {
            var rot = Quaternion.LookRotation(st.N, st.U);
            int r = s.ScreenRadius, outside = r * r, e = Mathf.Max(1, s.ScreenRibEvery);
            int laid = 0;
            for (int a = -r; a <= r; a++)
            for (int b = -r; b <= r; b++)
            {
                if (a * a + b * b > outside) continue;
                if (a % e != 0 && b % e != 0) continue;
                laid++;
                into?.Add(new TileLay(centre + st.U * (a * Tile) + st.W * (b * Tile), rot, PrismKind.SuperShielded,
                    st.Index));
            }
            return laid;
        }

        /// <summary>The crystal sites, in the arena's local space: on each structure's axis in front of its front plate,
        /// one per structure in placement order, then a second round farther out, until <paramref name="count"/> are
        /// listed.</summary>
        public static void CrystalSites(IReadOnlyList<Structure> structures, Settings s, int count, List<Vector3> into)
        {
            into.Clear();
            if (structures.Count == 0) return;
            float front = HalfDepth(s);
            for (int k = 0; into.Count < count; k++)
            {
                var st = structures[k % structures.Count];
                int round = k / structures.Count;
                into.Add(st.Centre + st.N * (front + CrystalOffset + round * CrystalStep));
            }
        }

        /// <summary><c>CellEnvironmentSpawnableBase.Hash01</c>, verbatim (the layout reads it from a static context).</summary>
        public static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n;
                h = (h ^ 61u) ^ (h >> 16);
                h *= 9u;
                h ^= h >> 4;
                h *= 0x27d4eb2du;
                h ^= h >> 15;
                return (h & 0xffffffu) / (float)0x1000000;
            }
        }
    }
}
