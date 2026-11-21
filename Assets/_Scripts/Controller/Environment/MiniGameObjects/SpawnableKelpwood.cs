using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Kelp Ball" - Kelpwood's arena, the Angler's CHARGE game (<c>Assets/_Scripts/Controller/Arcade/KELPWOOD.md</c>).
    /// Kelp strands radiate from a small open core in every direction, one per point of a Fibonacci sphere, so the ball
    /// has no up. Each strand is <see cref="StrandPrisms"/> flush prisms along a gently swaying curve out to
    /// <see cref="kelpRadius"/>.
    ///
    ///   • <b>Cover.</b> The Angler's eraser beam is a cone of 1.5 degrees and is not stopped by prisms, so the strands
    ///     do not block a lock: they hide a rival's hull from the pilot looking for it, and a rival weaving between them
    ///     is hard to hold under the nose.
    ///   • <b>Grazing.</b> Every prism is neutral (<see cref="Domains.Blue"/>), so the cell's herbivores (the spawn
    ///     profile) graze the strands, and the Angler tames them there: the kelp is where its ammunition lives, and it
    ///     thins as the match goes on.
    ///   • <b>Hazards.</b> <see cref="dangerTipEveryNth"/> strands end in <see cref="TipPrisms"/> danger prisms;
    ///     <see cref="collarEveryNth"/> strands wear a danger COLLAR (<see cref="CollarPrisms"/> prisms round the strand,
    ///     mid-way out), so the gaps you hide in start to cost petals.
    ///
    /// <para><b>Mirrored exactly by <c>Tools/Build/kelpwood_budget.py</c></b>, which the generator imports for the
    /// census, the volume and the PhaseThresholds. Every draw is <see cref="CellEnvironmentSpawnableBase.Hash01"/> of an
    /// integer key, every per-unit count is a const, and nothing is ever rejected, so the census is exact.</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62): a strand's prisms are laid along the chords
    /// of its curve at <see cref="Flush"/> of their length, and the sway is small enough that the air left at the worst
    /// bend covers the prisms' corners (the mirror asserts it). Strands start <see cref="StrandInner"/> u out and stay
    /// well apart; a collar's prisms sit <see cref="CollarRadius"/> u off the strand's axis.</para>
    ///
    /// <para><b>No intensity read.</b> The layout reads only the serialized fields below, one prefab per intensity, so
    /// each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by <c>Tools/Build/author_kelpwood_assets.py</c>.</para>
    /// </summary>
    public class SpawnableKelpwood : CellEnvironmentSpawnableBase
    {
        [Header("The kelp ball")]
        [Tooltip("Radius (u) the strands reach out to, round the arena centre.")]
        [SerializeField, Min(80f)] float kelpRadius = 320f;
        [Tooltip("Strands, one per point of a Fibonacci sphere (no up).")]
        [SerializeField, Min(1)] int strandCount = 44;

        [Header("Hazards")]
        [Tooltip("Every Nth strand (0, N, 2N...) ends in TipPrisms danger prisms. 0 = none.")]
        [SerializeField, Min(0)] int dangerTipEveryNth = 0;
        [Tooltip("Every Nth strand (1, N+1, ...) wears a danger collar mid-way out. 0 = none; must be 2 or more to mark any.")]
        [SerializeField, Min(0)] int collarEveryNth = 0;

        // ── A strand ──
        /// <summary>Prisms per strand, their square section (u), and where the strand starts (u from the centre).</summary>
        public const int StrandPrisms = 20;
        public const float Section = 3f;
        public const float StrandInner = 50f;
        /// <summary>The sway: an amplitude of SwayMin + SwaySpan x Hash01 (u), over SwayTurns radians along the strand,
        /// growing from nothing at the core to full at the tip.</summary>
        public const float SwayMin = 6f, SwaySpan = 8f;
        public const float SwayTurns = 3.2f;
        /// <summary>Danger prisms at the tip of a tipped strand.</summary>
        public const int TipPrisms = 4;

        // ── A collar ──
        /// <summary>Danger prisms round the strand, their distance from its axis, section and length (u), and the strand
        /// prism they sit on.</summary>
        public const int CollarPrisms = 6;
        public const float CollarRadius = 7f;
        public const float CollarSection = 2.5f, CollarLength = 6f;
        public const int CollarAt = 11;

        /// <summary>How close to exact contact a prism is laid along its chord.</summary>
        public const float Flush = 0.98f;

        /// <summary>Key offset into <see cref="CellEnvironmentSpawnableBase.Hash01"/> for the strands' sway draws.</summary>
        public const int StrandKeyBase = 3_000_000;

        protected override int DefaultSeed => 8686;
        protected override int LayCapacity => 2560;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(kelpRadius, strandCount, dangerTipEveryNth, collarEveryNth, 1);

        int LayoutSeed => seed != 0 ? seed : DefaultSeed;

        /// <summary>The radius (u) the strands reach out to, in this structure's local space. Read by the controller
        /// and the AI.</summary>
        public float KelpRadius => kelpRadius;

        readonly Vector3[] _knots = new Vector3[StrandPrisms + 1];

        protected override void BuildEnvironment()
        {
            for (int s = 0; s < strandCount; s++) LayStrand(s);
        }

        int Key(int family, int slot, int draw) => unchecked(LayoutSeed * 7919 + family + slot * 4 + draw);

        /// <summary>Point <paramref name="k"/> of <paramref name="n"/> on the unit Fibonacci sphere.</summary>
        static Vector3 Fibonacci(int k, int n)
        {
            float y = 1f - 2f * (k + 0.5f) / n;
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = GoldenAngle * k;
            return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
        }

        static Vector3 HelperFor(Vector3 a) => Mathf.Abs(a.y) < 0.9f ? Vector3.up : Vector3.right;

        void LayStrand(int s)
        {
            var d = Fibonacci(s, strandCount);
            var u = Vector3.Cross(d, HelperFor(d)).normalized;
            var v = Vector3.Cross(d, u);
            float phase = 2f * Mathf.PI * Hash01(Key(StrandKeyBase, s, 0));
            float amp = SwayMin + SwaySpan * Hash01(Key(StrandKeyBase, s, 1));
            var side = u + v * 0.5f;
            for (int k = 0; k <= StrandPrisms; k++)
            {
                float t = k / (float)StrandPrisms;
                float off = amp * Mathf.Sin(phase + SwayTurns * t) * t;
                _knots[k] = d * (StrandInner + (kelpRadius - StrandInner) * t) + side * off;
            }

            bool tips = dangerTipEveryNth > 0 && s % dangerTipEveryNth == 0;
            for (int k = 0; k < StrandPrisms; k++)
            {
                Vector3 ab = _knots[k + 1] - _knots[k];
                float chord = ab.magnitude;
                var axis = ab / Mathf.Max(1e-6f, chord);
                var kind = tips && k >= StrandPrisms - TipPrisms ? PrismKind.Danger : PrismKind.Plain;
                Emit(_knots[k] + ab * 0.5f, Facing(axis), new Vector3(Section, Section, chord * Flush), Domains.Blue, kind);
            }

            if (collarEveryNth > 1 && s % collarEveryNth == 1) LayCollar();
        }

        void LayCollar()
        {
            Vector3 ab = _knots[CollarAt + 1] - _knots[CollarAt];
            var c = _knots[CollarAt] + ab * 0.5f;
            var axis = ab.normalized;
            var cu = Vector3.Cross(axis, HelperFor(axis)).normalized;
            var cv = Vector3.Cross(axis, cu);
            var rot = Facing(axis);
            var size = new Vector3(CollarSection, CollarSection, CollarLength);
            for (int j = 0; j < CollarPrisms; j++)
            {
                float ang = 2f * Mathf.PI * j / CollarPrisms;
                Emit(c + (cu * Mathf.Cos(ang) + cv * Mathf.Sin(ang)) * CollarRadius, rot, size, Domains.Blue,
                     PrismKind.Danger);
            }
        }

        static Quaternion Facing(Vector3 axis) =>
            Quaternion.LookRotation(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);
    }
}
