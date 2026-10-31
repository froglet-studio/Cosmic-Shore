using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Gulag" - Serpent Gulag's sniper arena for intensities 2-4 (intensity 1 has no environment
    /// at all: open space, four pilots far apart, and the crystal field in the middle). See
    /// <c>Assets/_Scripts/Controller/Arcade/SERPENTGULAG.md</c>.
    ///
    /// Two families, both painted neutral (<see cref="Domains.Blue"/>, as the Boneyard's crust is), so
    /// every team's rifle cuts them alike:
    ///
    ///   • <b>Nests</b> - hollow pods on a ring between the spawn ring and the crystal field, one face
    ///     (+z) turned to the arena centre. That face carries a wide firing SLIT; the back face carries
    ///     an ENTRY a Serpent flies in through. A Serpent parked inside sees the field and the nests across
    ///     it through its slit and is hidden from every other angle. The Sniper Shot pierces everything
    ///     (<c>SniperShotActionSO.pierceCount</c> 0) and breaks super-shields, so a nest is CONCEALMENT and
    ///     a perch, not armour: every round through it leaves a hole, and a nest shot often enough is open.
    ///   • <b>Cell blocks</b> - barred screens (vertical bars between two rails) standing between the
    ///     nests and the field. They break the long sight lines across the middle without closing them:
    ///     a pilot reads a rival through the bars, and so does the rival.
    ///
    /// Danger plates (<see cref="dangerEveryNth"/>) sit on the nests' outer frame and the screens' bars:
    /// a sloppy entry or a pass too close to a cell block costs elements.
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62): a pod's front and back faces
    /// span its full width and height, the side walls fit BETWEEN them, and the roof and floor fit between
    /// the side walls, so no two faces share volume. Every plate is laid a hair under its cell
    /// (<see cref="Flush"/>). A screen's bars end a clearance short of its rails.</para>
    ///
    /// <para><b>No intensity read.</b> The layout depends only on the serialized fields below, one prefab
    /// per intensity, so each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by
    /// <c>Tools/Build/author_serpent_gulag_assets.py</c>.</para>
    /// </summary>
    public class SpawnableSerpentGulag : CellEnvironmentSpawnableBase
    {
        [Header("Nests")]
        [Tooltip("Sniper nests on the ring. 0 = none.")]
        [SerializeField, Min(0)] int nestCount = 6;
        [Tooltip("Radius (u) of the ring the nests stand on, around the arena centre.")]
        [SerializeField, Min(0f)] float nestRingRadius = 1200f;
        [Tooltip("Nests alternate above and below the ring's plane by up to this much (u).")]
        [SerializeField, Min(0f)] float nestHeightSpread = 300f;

        [Header("Cell blocks")]
        [Tooltip("Barred screens between the nests and the crystal field. 0 = none.")]
        [SerializeField, Min(0)] int screenCount = 0;
        [Tooltip("Radius (u) of the ring the screens stand on.")]
        [SerializeField, Min(0f)] float screenRingRadius = 700f;
        [Tooltip("Screens alternate above and below the ring's plane by up to this much (u).")]
        [SerializeField, Min(0f)] float screenHeightSpread = 200f;

        [Header("Danger")]
        [Tooltip("Every Nth nest frame plate and screen bar segment is a danger prism. 0 = none.")]
        [SerializeField, Min(0)] int dangerEveryNth = 0;

        // ── A nest (local frame: +z faces the arena centre) ──
        /// <summary>Outer size of a pod (u): roomy for a Serpent (about 9 x 12 x 11) to park in.</summary>
        const float NestWidth = 90f, NestHeight = 60f, NestDepth = 90f;
        /// <summary>A plate's thickness and the tile size on the front and back faces.</summary>
        const float Plate = 3f, Tile = 15f;
        /// <summary>The front face's slit: tiles whose centre is inside it are left out.</summary>
        const float SlitHalfWidth = 31f, SlitHalfHeight = 9f;
        /// <summary>The back face's entry, likewise.</summary>
        const float EntryHalfWidth = 16f, EntryHalfHeight = 16f;

        // ── A cell block (local frame: +z faces the arena centre, bars along y) ──
        const float ScreenWidth = 208f, ScreenHeight = 120f;
        const float BarStep = 16f, BarSection = 3f, BarSegment = 24f, RailLength = 16f;
        /// <summary>Gap between a bar's ends and the rails, so they touch nothing.</summary>
        const float RailClearance = 0.5f;

        /// <summary>How close to exact contact a fitted face is laid (a hair under).</summary>
        const float Flush = 0.995f;

        protected override int DefaultSeed => 7707;
        protected override int LayCapacity => 4096;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(nestCount, nestRingRadius, nestHeightSpread, screenCount,
                                    screenRingRadius, screenHeightSpread, dangerEveryNth, 1);

        int _frameIndex;

        protected override void BuildEnvironment()
        {
            _frameIndex = 0;
            float az0 = RangeF(0f, Mathf.PI * 2f);

            for (int k = 0; k < nestCount; k++)
            {
                float az = az0 + Mathf.PI * 2f * k / nestCount;
                float y = (k % 2 == 0 ? 1f : -1f) * nestHeightSpread * (0.5f + 0.5f * Hash01(k * 31 + 7));
                LayNest(OnRing(az, nestRingRadius, y));
            }

            for (int k = 0; k < screenCount; k++)
            {
                // Between the nests' bearings, so a screen sits across a sight line, not in front of a nest.
                float az = az0 + Mathf.PI * 2f * (k + 0.5f) / Mathf.Max(1, screenCount);
                float y = (k % 2 == 0 ? -1f : 1f) * screenHeightSpread * (0.5f + 0.5f * Hash01(k * 17 + 3));
                LayScreen(OnRing(az, screenRingRadius, y));
            }
        }

        static Vector3 OnRing(float az, float radius, float y) =>
            new(Mathf.Cos(az) * radius, y, Mathf.Sin(az) * radius);

        /// <summary>A frame turned so its +z looks at the arena centre (the structure's own origin).</summary>
        static Quaternion FacingCentre(Vector3 at)
        {
            Vector3 toCentre = -at;
            if (toCentre.sqrMagnitude < 1e-4f) toCentre = Vector3.forward;
            return Quaternion.LookRotation(toCentre.normalized, Vector3.up);
        }

        bool NextIsDanger() => dangerEveryNth > 0 && (++_frameIndex % dangerEveryNth) == 0;

        void LayNest(Vector3 at)
        {
            var rot = FacingCentre(at);
            float hw = NestWidth * 0.5f, hh = NestHeight * 0.5f, hd = NestDepth * 0.5f;

            // Front (+z) and back (-z): the full width and height, tiled; the slit and the entry left open.
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? hd - Plate * 0.5f : -hd + Plate * 0.5f;
                float openW = face == 0 ? SlitHalfWidth : EntryHalfWidth;
                float openH = face == 0 ? SlitHalfHeight : EntryHalfHeight;
                int cols = Mathf.RoundToInt(NestWidth / Tile), rows = Mathf.RoundToInt(NestHeight / Tile);
                float tw = NestWidth / cols, th = NestHeight / rows;
                for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    float x = -hw + tw * (c + 0.5f), y = -hh + th * (r + 0.5f);
                    if (Mathf.Abs(x) < openW && Mathf.Abs(y) < openH) continue;
                    bool rim = c == 0 || r == 0 || c == cols - 1 || r == rows - 1;
                    LayPlate(at, rot, new Vector3(x, y, z), new Vector3(tw * Flush, th * Flush, Plate * Flush), rim);
                }
            }

            // Side walls (+-x): between the front and back faces, the full height.
            float innerD = NestDepth - 2f * Plate;
            int sideCols = Mathf.Max(1, Mathf.RoundToInt(innerD / Tile));
            int sideRows = Mathf.Max(1, Mathf.RoundToInt(NestHeight / Tile));
            float sd = innerD / sideCols, sh = NestHeight / sideRows;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? hw - Plate * 0.5f : -hw + Plate * 0.5f;
                for (int c = 0; c < sideCols; c++)
                for (int r = 0; r < sideRows; r++)
                {
                    float z = -innerD * 0.5f + sd * (c + 0.5f), y = -hh + sh * (r + 0.5f);
                    bool rim = r == 0 || r == sideRows - 1;
                    LayPlate(at, rot, new Vector3(x, y, z), new Vector3(Plate * Flush, sh * Flush, sd * Flush), rim);
                }
            }

            // Roof and floor (+-y): between the side walls and between the front and back faces.
            float innerW = NestWidth - 2f * Plate;
            int capCols = Mathf.Max(1, Mathf.RoundToInt(innerW / Tile));
            int capRows = Mathf.Max(1, Mathf.RoundToInt(innerD / Tile));
            float cw = innerW / capCols, cd = innerD / capRows;
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? hh - Plate * 0.5f : -hh + Plate * 0.5f;
                for (int c = 0; c < capCols; c++)
                for (int r = 0; r < capRows; r++)
                {
                    float x = -innerW * 0.5f + cw * (c + 0.5f), z = -innerD * 0.5f + cd * (r + 0.5f);
                    LayPlate(at, rot, new Vector3(x, y, z), new Vector3(cw * Flush, Plate * Flush, cd * Flush), false);
                }
            }
        }

        void LayPlate(Vector3 at, Quaternion rot, Vector3 local, Vector3 size, bool rim)
        {
            var kind = rim && NextIsDanger() ? PrismKind.Danger : PrismKind.Plain;
            Emit(at + rot * local, rot, size, Domains.Blue, kind);
        }

        void LayScreen(Vector3 at)
        {
            var rot = FacingCentre(at);
            float hw = ScreenWidth * 0.5f, hh = ScreenHeight * 0.5f;

            // Two rails, top and bottom, each a run of planks along x laid end to end.
            int railPlanks = Mathf.Max(1, Mathf.RoundToInt(ScreenWidth / RailLength));
            float rl = ScreenWidth / railPlanks;
            for (int rail = 0; rail < 2; rail++)
            {
                float y = rail == 0 ? hh + BarSection * 0.5f : -hh - BarSection * 0.5f;
                for (int i = 0; i < railPlanks; i++)
                {
                    float x = -hw + rl * (i + 0.5f);
                    Emit(at + rot * new Vector3(x, y, 0f), rot,
                         new Vector3(rl * Flush, BarSection * Flush, BarSection * 1.4f), Domains.Blue);
                }
            }

            // The bars, between the rails: each a stack of segments laid end to end.
            int bars = Mathf.Max(1, Mathf.FloorToInt(ScreenWidth / BarStep));
            float span = ScreenHeight - 2f * RailClearance;
            int segs = Mathf.Max(1, Mathf.RoundToInt(span / BarSegment));
            float sl = span / segs;
            for (int b = 0; b < bars; b++)
            {
                float x = -hw + BarStep * (b + 0.5f) + (ScreenWidth - bars * BarStep) * 0.5f;
                for (int s = 0; s < segs; s++)
                {
                    float y = -hh + RailClearance + sl * (s + 0.5f);
                    var kind = NextIsDanger() ? PrismKind.Danger : PrismKind.Plain;
                    Emit(at + rot * new Vector3(x, y, 0f), rot,
                         new Vector3(BarSection, sl * Flush, BarSection), Domains.Blue, kind);
                }
            }
        }
    }
}
