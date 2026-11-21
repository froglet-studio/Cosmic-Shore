using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure geometry of Flatline's arena (<see cref="SpawnableFlatline"/>): an OPEN field of structures scattered
    /// through the cell, each made of thin square-tiled PANES, with space to fly between and into them (Shombith,
    /// 2026-11-09: "these shapes should be scattered or open designed where we can go inside"). Settings in, structures,
    /// tiles and crystal sites out; no Unity object is touched, so the tests and <c>Tools/Build/flatline_budget.py</c>
    /// (its line-for-line Python twin) read the same layout the game lays. See
    /// <c>Assets/_Scripts/Controller/Arcade/FLATLINE.md</c>.
    ///
    /// <para><b>Three structures</b>, each in its own random frame (0G: no axis is up):</para>
    ///   • a loose <b>pane</b>: one square plate of (2h+1)² tiles, cover you fly round;
    ///   • a <b>tunnel</b>: four walls of a hollow cube, open at both ends along its axis, to fly through;
    ///   • a <b>cage</b>: all six walls, each with a 3 x 3 window in its middle, to fly into from any side.
    /// The crystal sites are the rooms' centres first (you go inside to re-arm), then a little in front of loose panes.
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62). Structures never touch: each is placed only
    /// where its bounding sphere clears every earlier one by <see cref="Gap"/>. Inside a room, each wall spans one tile
    /// less than the room on every side, so neighbouring walls meet at an open slot, not an overlap.</para>
    ///
    /// <para><b>Counts are integer arithmetic.</b> Which tiles a structure has depends only on integers (its half-size,
    /// the grid indices, the hole hash), so the count is exact in the twin as long as every structure is placed; the
    /// generator asserts the twin places them all, and the tests assert the C# does.</para>
    /// </summary>
    public static class FlatlinePanes
    {
        /// <summary>A tile's edge (u) and the grid's pitch.</summary>
        public const float Tile = 10f;
        /// <summary>A tile's thickness (u): a pane is a thin sheet.</summary>
        public const float Thickness = 2f;
        /// <summary>How close to exact contact a tile is laid (a hair under its cell).</summary>
        public const float Flush = 0.995f;
        /// <summary>Clear space kept between two structures' bounding spheres (u): room to fly between them.</summary>
        public const float Gap = 40f;
        /// <summary>Placement draws per structure before it is given up (never reached at the authored densities; the
        /// generator asserts every structure lands).</summary>
        public const int MaxAttempts = 64;
        /// <summary>How far in front of a loose pane its crystal site floats (u).</summary>
        public const float CrystalOffset = 25f;
        /// <summary>A cage window's half-size in tiles (1 = a 3 x 3 opening).</summary>
        public const int WindowHalf = 1;
        /// <summary>Hash salt for the holes punched through a loose pane.</summary>
        const int HoleSalt = 5;

        public const int KindPane = 0;
        public const int KindTunnel = 1;
        public const int KindCage = 2;

        /// <summary>Everything that shapes a layout: the serialized fields of a <see cref="SpawnableFlatline"/>.</summary>
        public struct Settings
        {
            public int Seed;
            public float InnerRadius, OuterRadius;
            public int PaneCount, PaneHalfMin, PaneHalfMax;
            public int TunnelCount, CageCount, RoomHalf;
            public int RibEvery, DangerPaneEvery, DangerRoomEvery;
            public float HoleShare;
            public int HoleCellTiles;
        }

        /// <summary>One placed structure: its kind, centre, frame (N its axis; U, W across it) and half-size in tiles.</summary>
        public readonly struct Structure
        {
            public readonly int Kind, Half, Index;
            public readonly Vector3 Centre, N, U, W;
            public readonly bool Danger;

            public Structure(int kind, int half, int index, Vector3 centre, Vector3 n, bool danger)
            {
                Kind = kind;
                Half = half;
                Index = index;
                Centre = centre;
                N = n;
                Danger = danger;
                Basis(n, out U, out W);
            }

            /// <summary>The bounding sphere's radius (u): a pane's half-diagonal, a room's corner, plus a tile.</summary>
            public float Radius => Kind == KindPane
                ? (Half + 0.5f) * Tile * 1.4142135f + Tile
                : (Half + 1f) * Tile * 1.7320508f + Tile;
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
        public static Vector3 TileScale => new(Tile * Flush, Tile * Flush, Thickness);

        /// <summary>A frame's cross axes: perpendicular to its axis, the same rule in the twin.</summary>
        public static void Basis(Vector3 n, out Vector3 u, out Vector3 w)
        {
            Vector3 t = Mathf.Abs(n.x) < 0.9f ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 1f, 0f);
            u = Vector3.Cross(n, t).normalized;
            w = Vector3.Cross(n, u);
        }

        /// <summary>Floor division for a negative grid index (C#'s / truncates toward zero).</summary>
        public static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>A unit vector from two hashes: uniform on the sphere.</summary>
        static Vector3 UnitFrom(float h1, float h2)
        {
            float z = 2f * h1 - 1f;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float phi = 2f * Mathf.PI * h2;
            return new Vector3(ring * Mathf.Cos(phi), ring * Mathf.Sin(phi), z);
        }

        /// <summary>
        /// Place every structure: the rooms (cages, then tunnels) first, because they are the biggest, then the loose
        /// panes. Each takes the first of up to <see cref="MaxAttempts"/> hashed draws whose bounding sphere clears every
        /// earlier one by <see cref="Gap"/>; positions are spread evenly by volume through the shell between
        /// <see cref="Settings.InnerRadius"/> and <see cref="Settings.OuterRadius"/>. Returns how many were placed.
        /// </summary>
        public static int Layout(Settings s, List<Structure> into)
        {
            into.Clear();
            float r0 = s.InnerRadius * s.InnerRadius * s.InnerRadius;
            float r1 = s.OuterRadius * s.OuterRadius * s.OuterRadius;
            int total = s.CageCount + s.TunnelCount + s.PaneCount;
            int span = Mathf.Max(1, s.PaneHalfMax - s.PaneHalfMin + 1);
            for (int k = 0; k < total; k++)
            {
                int kind, half, local;
                if (k < s.CageCount) { kind = KindCage; half = s.RoomHalf; local = k; }
                else if (k < s.CageCount + s.TunnelCount) { kind = KindTunnel; half = s.RoomHalf; local = k - s.CageCount; }
                else { kind = KindPane; local = k - s.CageCount - s.TunnelCount; half = s.PaneHalfMin + local % span; }
                int every = kind == KindPane ? s.DangerPaneEvery : s.DangerRoomEvery;
                bool danger = every > 0 && local % every == every - 1;

                for (int t = 0; t < MaxAttempts; t++)
                {
                    int key = s.Seed * 104729 + k * 1031 + t * 7;   // 64 draws x 7 < 1031: no two structures share a key
                    float rr = Mathf.Pow(r0 + Hash01(key) * (r1 - r0), 1f / 3f);
                    Vector3 c = UnitFrom(Hash01(key + 1), Hash01(key + 2)) * rr;
                    Vector3 n = UnitFrom(Hash01(key + 3), Hash01(key + 4));
                    var candidate = new Structure(kind, half, k, c, n, danger);
                    bool clear = true;
                    for (int j = 0; j < into.Count && clear; j++)
                    {
                        float need = candidate.Radius + into[j].Radius + Gap;
                        clear = (into[j].Centre - c).sqrMagnitude >= need * need;
                    }
                    if (!clear) continue;
                    into.Add(candidate);
                    break;
                }
            }
            return into.Count;
        }

        /// <summary>
        /// Every tile of every structure, in structure order then wall order then grid order. Every
        /// <see cref="Settings.RibEvery"/>th interior grid line is a super-shielded RIB (nothing breaks it, so a worn wall stays a
        /// readable lattice); a danger structure's outer ring of tiles is danger; a <see cref="Settings.HoleShare"/> of a
        /// loose pane's <see cref="Settings.HoleCellTiles"/>-square cells is left open. Returns the number laid.
        /// </summary>
        public static int Tiles(IReadOnlyList<Structure> structures, Settings s, List<TileLay> into)
        {
            into?.Clear();
            int laid = 0;
            for (int i = 0; i < structures.Count; i++)
            {
                var st = structures[i];
                if (st.Kind == KindPane)
                {
                    laid += Wall(st, st.Centre, st.N, st.U, st.W, st.Half, false, s, true, into);
                    continue;
                }
                // A room: walls at (Half + 0.5) tiles from the centre, each a tile smaller than the room on every side.
                float d = (st.Half + 0.5f) * Tile;
                int wallHalf = st.Half - 1;
                bool window = st.Kind == KindCage;
                laid += Wall(st, st.Centre + st.U * d, st.U, st.W, st.N, wallHalf, window, s, false, into);
                laid += Wall(st, st.Centre - st.U * d, -st.U, st.W, st.N, wallHalf, window, s, false, into);
                laid += Wall(st, st.Centre + st.W * d, st.W, st.N, st.U, wallHalf, window, s, false, into);
                laid += Wall(st, st.Centre - st.W * d, -st.W, st.N, st.U, wallHalf, window, s, false, into);
                if (st.Kind == KindCage)
                {
                    laid += Wall(st, st.Centre + st.N * d, st.N, st.U, st.W, wallHalf, true, s, false, into);
                    laid += Wall(st, st.Centre - st.N * d, -st.N, st.U, st.W, wallHalf, true, s, false, into);
                }
            }
            return laid;
        }

        /// <summary>One square wall of (2h+1)² tiles in the plane through <paramref name="centre"/> normal to
        /// <paramref name="normal"/>, its grid along <paramref name="a"/> and <paramref name="b"/>.</summary>
        static int Wall(Structure st, Vector3 centre, Vector3 normal, Vector3 a, Vector3 b, int h, bool window,
                        Settings s, bool holes, List<TileLay> into)
        {
            var rot = Quaternion.LookRotation(normal, a);
            int laid = 0;
            for (int i = -h; i <= h; i++)
            for (int j = -h; j <= h; j++)
            {
                if (window && Mathf.Abs(i) <= WindowHalf && Mathf.Abs(j) <= WindowHalf) continue;
                PrismKind kind;
                bool rim = Mathf.Abs(i) == h || Mathf.Abs(j) == h;
                if (st.Danger && rim) kind = PrismKind.Danger;
                else if (s.RibEvery > 0 && (i % s.RibEvery == 0 && Mathf.Abs(i) < h || j % s.RibEvery == 0 && Mathf.Abs(j) < h))
                    kind = PrismKind.SuperShielded;   // interior lines only: a wall's skeleton, never its rim
                else
                {
                    if (holes && s.HoleShare > 0f && s.HoleCellTiles > 0)
                    {
                        int cx = FloorDiv(i, s.HoleCellTiles), cy = FloorDiv(j, s.HoleCellTiles);
                        if (Hash01(st.Index * 7919 + cx * 131 + cy * 17 + HoleSalt) < s.HoleShare) continue;
                    }
                    kind = PrismKind.Plain;
                }
                laid++;
                into?.Add(new TileLay(centre + a * (i * Tile) + b * (j * Tile), rot, kind, st.Index));
            }
            return laid;
        }

        /// <summary>The crystal sites, in the arena's local space: every room's centre first (in placement order), then
        /// <see cref="CrystalOffset"/> in front of each loose pane, until <paramref name="count"/> are listed.</summary>
        public static void CrystalSites(IReadOnlyList<Structure> structures, int count, List<Vector3> into)
        {
            into.Clear();
            for (int i = 0; i < structures.Count && into.Count < count; i++)
                if (structures[i].Kind != KindPane) into.Add(structures[i].Centre);
            for (int i = 0; i < structures.Count && into.Count < count; i++)
                if (structures[i].Kind == KindPane) into.Add(structures[i].Centre + structures[i].N * CrystalOffset);
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
