using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Corona" - Corona's arena, the Manta's CHARGE game (<c>Assets/_Scripts/Controller/Arcade/CORONA.md</c>).
    /// Nested prism SHELLS round an open crystal HEART, in a 0G volume with no up. Each shell is a sphere of flat
    /// tiles, one per point of a Fibonacci sphere, with round GATES cut through it; each shell's gates are turned
    /// away from the last one's, so no straight line runs from the rim to the heart.
    ///
    ///   • <b>Fuel.</b> Every tile is neutral (<see cref="Domains.Blue"/>), and a Manta's skimmer arms a bomb from
    ///     any prism it brushes, so the shells are where a Manta gets the energy to plant.
    ///   • <b>Chokes.</b> The omni crystals (the Manta's cash-out) float in the heart, so every pilot has to come in
    ///     through the gates: that is where Mantas meet and graze each other.
    ///   • <b>The cost of contact.</b> A Manta's HULL touching any prism sets off its bombs early and empties its
    ///     energy (MANTA_STING_KABLOOM.md §0), so a shell is fuel at skimmer reach and a trap at hull reach.
    ///     <see cref="rimDegrees"/> lines each gate with DANGER tiles, so cutting a gate close costs petals too.
    ///
    /// <para><b>Mirrored exactly by <c>Tools/Build/corona_budget.py</c></b>, which the generator imports for the
    /// census, the volume and the PhaseThresholds. There are no random draws at all: every tile is a Fibonacci
    /// point, every per-unit size is a const, and the only comparisons that change a count (inside a gate, inside a
    /// rim) are asserted by the mirror to clear their boundary by a margin, so float32 against float64 cannot move
    /// one tile.</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62): a tile's half diagonal is under half the
    /// least distance between two neighbouring Fibonacci points of its shell (the mirror measures it per shell), and
    /// shells sit <see cref="shellSpacing"/> apart.</para>
    ///
    /// <para><b>No intensity read.</b> The layout reads only the serialized fields below, one prefab per intensity,
    /// so each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by
    /// <c>Tools/Build/author_corona_assets.py</c>. The AI reads the same geometry through <see cref="ShellRadius"/>
    /// and <see cref="GateDirection"/>, so it routes through the gates the arena actually has.</para>
    /// </summary>
    public class SpawnableCorona : CellEnvironmentSpawnableBase
    {
        [Header("The shells")]
        [Tooltip("How many nested shells round the heart.")]
        [SerializeField, Min(1)] int shellCount = 2;
        [Tooltip("Radius (u) of the innermost shell.")]
        [SerializeField, Min(60f)] float innerShellRadius = 190f;
        [Tooltip("Radius step (u) from one shell to the next.")]
        [SerializeField, Min(40f)] float shellSpacing = 120f;

        [Header("The gates")]
        [Tooltip("Round gates cut through every shell, one per point of a Fibonacci sphere, each shell's turned " +
                 "ShellTwistDegrees further than the last.")]
        [SerializeField, Min(1)] int gatesPerShell = 6;
        [Tooltip("Half the angle (degrees) a gate spans, seen from the heart.")]
        [SerializeField, Range(4f, 40f)] float gateHalfAngle = 20f;
        [Tooltip("Width (degrees) of the band of DANGER tiles round each gate. 0 = none.")]
        [SerializeField, Range(0f, 15f)] float rimDegrees = 0f;

        // ── A tile ──
        /// <summary>Nominal centre spacing of a shell's tiles (u): a shell of radius r has round(4 pi r^2 / TileSpacing^2)
        /// Fibonacci points.</summary>
        public const float TileSpacing = 32f;
        /// <summary>A tile's width (u, square) and its thickness along the shell's normal.</summary>
        public const float TileWidth = 18f;
        public const float TileThickness = 3f;

        /// <summary>How far (degrees) each shell's gates turn past the previous shell's, about <see cref="TwistAxis"/>.</summary>
        public const float ShellTwistDegrees = 37f;
        static readonly Vector3 TwistAxis = new Vector3(0.48f, 0.64f, 0.6f);

        protected override int DefaultSeed => 8787;
        protected override int LayCapacity => 4096;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(shellCount, innerShellRadius, shellSpacing, gatesPerShell, gateHalfAngle, rimDegrees, 1);

        // ── The geometry the controller and the AI read (this structure's local space) ──
        public int ShellCount => shellCount;
        public int GatesPerShell => gatesPerShell;
        public float GateHalfAngle => gateHalfAngle;
        public float ShellRadius(int shell) => innerShellRadius + shellSpacing * shell;
        public float OuterRadius => ShellRadius(shellCount - 1);

        /// <summary>The unit direction of gate <paramref name="gate"/> on shell <paramref name="shell"/>.</summary>
        public Vector3 GateDirection(int shell, int gate) =>
            (Quaternion.AngleAxis(ShellTwistDegrees * shell, TwistAxis.normalized) * Fibonacci(gate, gatesPerShell)).normalized;

        protected override void BuildEnvironment()
        {
            for (int s = 0; s < shellCount; s++) LayShell(s);
        }

        /// <summary>Point <paramref name="k"/> of <paramref name="n"/> on the unit Fibonacci sphere.</summary>
        static Vector3 Fibonacci(int k, int n)
        {
            float y = 1f - 2f * (k + 0.5f) / n;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = GoldenAngle * k;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        /// <summary>Tiles on a shell of radius <paramref name="radius"/>.</summary>
        public static int TilesFor(float radius) =>
            Mathf.RoundToInt(4f * Mathf.PI * radius * radius / (TileSpacing * TileSpacing));

        void LayShell(int s)
        {
            float radius = ShellRadius(s);
            int n = TilesFor(radius);
            float cosGate = Mathf.Cos(gateHalfAngle * Mathf.Deg2Rad);
            float cosRim = Mathf.Cos((gateHalfAngle + rimDegrees) * Mathf.Deg2Rad);

            var gates = new Vector3[gatesPerShell];
            for (int g = 0; g < gatesPerShell; g++) gates[g] = GateDirection(s, g);

            var size = new Vector3(TileWidth, TileThickness, TileWidth);
            for (int k = 0; k < n; k++)
            {
                var p = Fibonacci(k, n);
                float nearest = -1f;
                for (int g = 0; g < gatesPerShell; g++) nearest = Mathf.Max(nearest, Vector3.Dot(p, gates[g]));
                if (nearest > cosGate) continue;   // inside a gate: open water

                var kind = rimDegrees > 0f && nearest > cosRim ? PrismKind.Danger : PrismKind.Plain;
                var tangent = Vector3.Cross(p, Mathf.Abs(p.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Emit(p * radius, Quaternion.LookRotation(tangent, p), size, Domains.Blue, kind);
            }
        }
    }
}
