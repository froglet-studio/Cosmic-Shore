using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure geometry of Flatline's arena (<see cref="SpawnableFlatline"/>): thin square-tiled PANES crossing the
    /// cell, and the crystal sites on the seams where they meet. Settings in, tiles and sites out; no Unity object is
    /// touched, so the tests and <c>Tools/Build/flatline_budget.py</c> (its line-for-line Python twin) read the same
    /// layout the game lays. See <c>Assets/_Scripts/Controller/Arcade/FLATLINE.md</c>.
    ///
    /// <para><b>0G.</b> No pane is a floor and no axis is up: the arrangements are the three axes, five pages round one
    /// spine, the six icosahedral axes, and a geode of ten panes standing off the centre.</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62). Within a pane the tiles sit on a square grid,
    /// each a hair under its cell (<see cref="Flush"/>). Where two panes cross, the LATER pane leaves a slot
    /// <see cref="SeamHalfWidth"/> either side of the earlier pane's plane, wide enough that no tile of it can reach a
    /// tile of the earlier one at any angle: a tile reaches at most its half-diagonal (7.07) plus half its thickness
    /// along any direction, and the earlier tile half its thickness.</para>
    ///
    /// <para><b>Counts are integer arithmetic.</b> Which tiles a pane has is decided on the integer grid (a tile
    /// is in when a² + b² ≤ R²), so the only float comparison that can move a count is the seam test, which the
    /// Python twin computes in float32 in the same order.</para>
    /// </summary>
    public static class FlatlinePanes
    {
        /// <summary>A tile's edge (u) and the grid's pitch.</summary>
        public const float Tile = 10f;
        /// <summary>A tile's thickness (u): a pane is a thin sheet.</summary>
        public const float Thickness = 2f;
        /// <summary>How close to exact contact a tile is laid (a hair under its cell).</summary>
        public const float Flush = 0.995f;
        /// <summary>Half the slot a later pane leaves round an earlier pane's plane (u). Must exceed a tile's reach
        /// along any direction (√(5² + 5² + 1²) = 7.14) plus the earlier tile's half-thickness (1): 8.14.</summary>
        public const float SeamHalfWidth = 8.5f;
        /// <summary>The seam slot is cut only where the earlier pane has tiles: within its radius plus this (u).</summary>
        public const float SeamReachMargin = Tile;
        /// <summary>Hash salt for the holes punched through a pane (the I4 lattice).</summary>
        const int HoleSalt = 5;

        public const int ArrangementThreePlanes = 0;
        public const int ArrangementFivePages = 1;
        public const int ArrangementIcosaPanes = 2;
        public const int ArrangementPaneGeode = 3;

        /// <summary>One pane: a disc of tiles in the plane through <see cref="Centre"/> normal to
        /// <see cref="Normal"/>, its grid along <see cref="U"/> and <see cref="W"/>.</summary>
        public readonly struct Pane
        {
            public readonly Vector3 Normal, Centre, U, W;
            public readonly int RadiusTiles;
            public float Radius => RadiusTiles * Tile;

            public Pane(Vector3 normal, Vector3 centre, int radiusTiles)
            {
                Normal = normal;
                Centre = centre;
                RadiusTiles = radiusTiles;
                Basis(normal, out U, out W);
            }
        }

        /// <summary>One laid tile, in the arena's local space.</summary>
        public readonly struct TileLay
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly PrismKind Kind;
            public readonly int Pane;

            public TileLay(Vector3 position, Quaternion rotation, PrismKind kind, int pane)
            {
                Position = position;
                Rotation = rotation;
                Kind = kind;
                Pane = pane;
            }
        }

        /// <summary>The size every tile is laid at.</summary>
        public static Vector3 TileScale => new(Tile * Flush, Tile * Flush, Thickness);

        /// <summary>The unit normals of an arrangement, in order (a pane's index is its place here).</summary>
        public static void Normals(int arrangement, List<Vector3> into)
        {
            into.Clear();
            switch (arrangement)
            {
                case ArrangementThreePlanes:
                    into.Add(new Vector3(1f, 0f, 0f));
                    into.Add(new Vector3(0f, 1f, 0f));
                    into.Add(new Vector3(0f, 0f, 1f));
                    return;
                case ArrangementFivePages:
                    // Five pages round the y spine, 36° apart.
                    for (int k = 0; k < 5; k++)
                    {
                        float a = Mathf.PI * k / 5f;
                        into.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                    }
                    return;
                default:
                    // The six icosahedral axes; the geode adds the opposites of the first four (ten panes).
                    float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
                    into.Add(new Vector3(0f, 1f, phi).normalized);
                    into.Add(new Vector3(0f, 1f, -phi).normalized);
                    into.Add(new Vector3(1f, phi, 0f).normalized);
                    into.Add(new Vector3(1f, -phi, 0f).normalized);
                    into.Add(new Vector3(phi, 0f, 1f).normalized);
                    into.Add(new Vector3(phi, 0f, -1f).normalized);
                    if (arrangement == ArrangementPaneGeode)
                        for (int k = 0; k < 4; k++) into.Add(-into[k]);
                    return;
            }
        }

        /// <summary>The panes of an arrangement: each at <paramref name="offset"/> along its own normal from the
        /// centre (0 = every pane through the centre).</summary>
        public static void Panes(int arrangement, int radiusTiles, float offset, List<Pane> into)
        {
            into.Clear();
            var normals = new List<Vector3>(10);
            Normals(arrangement, normals);
            for (int i = 0; i < normals.Count; i++)
                into.Add(new Pane(normals[i], normals[i] * offset, radiusTiles));
        }

        /// <summary>A pane's grid axes: perpendicular to its normal, the same rule in the twin.</summary>
        public static void Basis(Vector3 n, out Vector3 u, out Vector3 w)
        {
            Vector3 t = Mathf.Abs(n.x) < 0.9f ? new Vector3(1f, 0f, 0f) : new Vector3(0f, 1f, 0f);
            u = Vector3.Cross(n, t).normalized;
            w = Vector3.Cross(n, u);
        }

        /// <summary>Floor division for a negative grid index (C#'s / truncates toward zero).</summary>
        public static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>
        /// Every tile of every pane, in pane order then grid order. <paramref name="dangerRimTiles"/> outer rings are
        /// danger; every <paramref name="ribEvery"/>th grid line is a super-shielded RIB (the pane's skeleton, which no
        /// cord, round or ring blast breaks, so a pane worn to a lattice still reads as a plane); a
        /// <paramref name="holeShare"/> of the <paramref name="holeCellTiles"/>-square cells between the ribs is left
        /// open. Returns the number of tiles laid.
        /// </summary>
        public static int Tiles(IReadOnlyList<Pane> panes, int dangerRimTiles, int ribEvery, float holeShare,
                                int holeCellTiles, List<TileLay> into)
        {
            into?.Clear();
            int laid = 0;
            for (int j = 0; j < panes.Count; j++)
            {
                var pane = panes[j];
                int r = pane.RadiusTiles;
                int r2 = r * r;
                int inner = Mathf.Max(0, r - dangerRimTiles);
                int inner2 = inner * inner;
                var rot = Quaternion.LookRotation(pane.Normal, pane.U);
                for (int a = -r; a <= r; a++)
                for (int b = -r; b <= r; b++)
                {
                    int d2 = a * a + b * b;
                    if (d2 > r2) continue;

                    Vector3 p = pane.Centre + pane.U * (a * Tile) + pane.W * (b * Tile);
                    if (InSeam(panes, j, p)) continue;

                    PrismKind kind;
                    if (dangerRimTiles > 0 && d2 > inner2) kind = PrismKind.Danger;
                    else if (ribEvery > 0 && (a % ribEvery == 0 || b % ribEvery == 0)) kind = PrismKind.SuperShielded;
                    else
                    {
                        if (holeShare > 0f && holeCellTiles > 0)
                        {
                            int cx = FloorDiv(a, holeCellTiles), cy = FloorDiv(b, holeCellTiles);
                            if (Hash01(j * 7919 + cx * 131 + cy * 17 + HoleSalt) < holeShare) continue;
                        }
                        kind = PrismKind.Plain;
                    }

                    laid++;
                    into?.Add(new TileLay(p, rot, kind, j));
                }
            }
            return laid;
        }

        /// <summary>Is <paramref name="p"/>, on pane <paramref name="j"/>, inside the slot it leaves round any earlier
        /// pane that has tiles there?</summary>
        static bool InSeam(IReadOnlyList<Pane> panes, int j, Vector3 p)
        {
            for (int i = 0; i < j; i++)
            {
                var other = panes[i];
                Vector3 rel = p - other.Centre;
                float dist = Vector3.Dot(rel, other.Normal);
                if (Mathf.Abs(dist) >= SeamHalfWidth) continue;
                Vector3 inPlane = rel - other.Normal * dist;
                float reach = other.Radius + SeamReachMargin;
                if (inPlane.sqrMagnitude < reach * reach) return true;
            }
            return false;
        }

        /// <summary>
        /// The crystal sites, in the arena's local space: on the SEAMS, where two panes cross (the places a ring thrown
        /// along either pane reaches). Each distinct seam line inside both panes is listed once, in pane-pair order;
        /// site k sits on line k mod L, stepped <paramref name="spacing"/> out from the line's nearest point to the
        /// centre, alternately each way (+1, -1, +2, -2 ... steps), so one line can carry several.
        /// </summary>
        public static void CrystalSites(IReadOnlyList<Pane> panes, int count, float spacing, List<Vector3> into)
        {
            into.Clear();
            if (count <= 0) return;
            var points = new List<Vector3>();
            var dirs = new List<Vector3>();
            for (int i = 0; i < panes.Count; i++)
            for (int j = i + 1; j < panes.Count; j++)
            {
                Vector3 n1 = panes[i].Normal, n2 = panes[j].Normal;
                Vector3 d = Vector3.Cross(n1, n2);
                float dd = d.sqrMagnitude;
                if (dd < 1e-6f) continue;   // parallel panes never meet
                float h1 = Vector3.Dot(n1, panes[i].Centre), h2 = Vector3.Dot(n2, panes[j].Centre);
                Vector3 p0 = (Vector3.Cross(n2, d) * h1 + Vector3.Cross(d, n1) * h2) / dd;
                if ((p0 - panes[i].Centre).magnitude > panes[i].Radius) continue;
                if ((p0 - panes[j].Centre).magnitude > panes[j].Radius) continue;
                Vector3 dn = d.normalized;
                bool seen = false;
                for (int k = 0; k < dirs.Count && !seen; k++)
                {
                    if (Mathf.Abs(Vector3.Dot(dirs[k], dn)) < 0.999f) continue;
                    Vector3 off = p0 - points[k];
                    seen = (off - dirs[k] * Vector3.Dot(off, dirs[k])).sqrMagnitude < 1f;
                }
                if (seen) continue;
                points.Add(p0);
                dirs.Add(dn);
            }
            if (points.Count == 0) return;
            for (int k = 0; k < count; k++)
            {
                int line = k % points.Count, step = k / points.Count;
                float t = spacing * (step / 2 + 1) * (step % 2 == 0 ? 1f : -1f);
                into.Add(points[line] + dirs[line] * t);
            }
        }

        /// <summary><c>CellEnvironmentSpawnableBase.Hash01</c>, verbatim (the holes read it from a static context).</summary>
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
