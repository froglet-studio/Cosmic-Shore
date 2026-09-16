using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Panes" - Cleave's intensity-1 arena, and the most forgiving thing in the mode to put a
    /// sword through: nine FLAT SLABS cutting the cell at nine different angles, each one a
    /// corduroy of parallel ribs with its own grain direction.
    ///
    /// The design problem this solves is that a SHELL (the cage, intensity 3) is a bad first
    /// lesson. A shell curves away from you: you cross it perpendicular and you are through in a
    /// moment, or you try to run along it and it bends out from under the blade. A PLANE does
    /// neither. Line the vessel up with a pane and the mass stays exactly where you left it for
    /// the full 1440 units of its diameter, so the pilot's first discovery is the one the whole
    /// mode is built on - <b>a sword rewards commitment to a line</b>.
    ///
    /// Four properties are load-bearing:
    ///
    ///   • <b>The grain.</b> Every pane lays its ribs along its own authored <c>GrainDeg</c>, so
    ///     each slab has a direction that cuts long and a direction that cuts wide. Ribs are
    ///     spaced <see cref="RibStep"/> apart across the grain and their planks OVERLAP along it
    ///     (<see cref="PlankStep"/> under <see cref="PlankLength"/>), which is what makes a rib
    ///     read as one continuous line rather than a row of beads.
    ///   • <b>The angles are AUTHORED, not generated.</b> <see cref="PaneSpecs"/> is a gameplay
    ///     surface - which approaches are cheap, where the arena is thick, how much of it you can
    ///     see through from the spawn ring - and nine hand-picked normals are far easier to tune
    ///     and to read than a formula that produces nine. The same reasoning as the cage's
    ///     <c>ShellTilts</c>.
    ///   • <b>Panes are OFFSET from the centre, and not all by the same amount.</b> Nine planes
    ///     all through the origin would pile every one of their intersections into a single dense
    ///     knot at the middle and leave the rest of the ball empty. Spreading the offsets
    ///     (<c>OffsetFraction</c>, -0.55..+0.5) scatters the intersections through the volume, and
    ///     it is those intersections that carry the <b>mullions</b> below.
    ///   • <b>Mullions are the prize.</b> Where two panes cross, their intersection is a straight
    ///     chord, and a beam is laid along it. A mullion is the densest run of mass in the arena
    ///     AND it is dead straight, so the best single cut available is to find one and fly it.
    ///     They cost nothing to place - the line is exact analytic geometry, not a search.
    ///
    /// Every prism is <see cref="PrismKind.Plain"/> except the sparse <see cref="PrismKind.Danger"/>
    /// traps on the pane RIMS - the frame around each slab, which is exactly what you clip when
    /// you misjudge a pass. Nothing is <see cref="PrismKind.Shielded"/> or
    /// <see cref="PrismKind.SuperShielded"/>, for the mode-wide reason recorded in CLEAVE.md: a
    /// super-shielded prism can only be popped by an ENERGIZED blade, an AI never pulls triggers
    /// and so can never energize, and an all-AI domain that cannot break the arena cannot play.
    ///
    /// Painted across the full domain triad so the arena reads as contested neutral matter rather
    /// than anyone's property. Deterministic per seed like every cell environment.
    /// </summary>
    public class SpawnablePanes : CellEnvironmentSpawnableBase
    {
        /// <summary>This arena IS intensity 1, so it carries that rung's dials as constants.</summary>
        const float R = SliceArenaGeometry.OuterRadiusI1;

        /// <summary>Authored-units -> world-units. Every LENGTH below is a number that was tuned
        /// against <c>SliceArenaGeometry.AuthoredRadius</c> and is multiplied by this; the noise
        /// frequency is divided by it so the void pattern keeps the same size RELATIVE to the
        /// arena. Counts, angles and fractions-of-R are deliberately left bare - writing every
        /// scaled value as `x * S` is what makes an unscaled one visible in review.</summary>
        const float S = SliceArenaGeometry.LengthScaleI1;

        /// <summary>Extra ACROSS-grain spacing, and the ONLY dial here that moves a prism count.
        /// It multiplies <see cref="RibStep"/> alone: ribs sit G times further apart while each rib
        /// is still a line of overlapping planks, so a pane thins out without breaking into beads.
        /// Rib count falls by G; planks per rib, mullions and rims are untouched.</summary>
        const float G = SliceArenaGeometry.GapScaleI1;

        // ── The weave ────────────────────────────────────────────────────────
        /// <summary>Spacing ALONG a rib. Under <see cref="PlankLength"/> on purpose: consecutive
        /// planks overlap, so a rib reads as one continuous bar you can drag a blade down.</summary>
        const float PlankStep = 15f * S;
        const float PlankLength = 17f * S;
        /// <summary>Spacing ACROSS the grain, i.e. rib to rib. Deliberately much wider than
        /// <see cref="PlankStep"/> - that ratio IS the corduroy, and it is what lets a pilot see
        /// (and fly) through a pane instead of meeting a wall.</summary>
        const float RibStep = 21f * S * G;

        /// <summary>Value-noise threshold below which a plank is skipped. Breaks each pane into
        /// weathered patches and open windows so the arena is porous rather than nine barricades.</summary>
        const float VoidThreshold = 0.30f;
        const float VoidFreq = 0.016f / S;

        // ── Mullions (pane x pane intersections) ─────────────────────────────
        const float MullionStep = 19f * S;
        const float MullionLength = 22f * S;

        // ── Rims (the frame around each pane, and the arena's only traps) ─────
        const float RimStep = 16f * S;
        const float RimLength = 18f * S;
        /// <summary>Every Nth rim prism is a trap. The rim is what a sloppy pass clips, so the
        /// downside sits exactly where the mistake is.</summary>
        const int DangerEveryNthRimPrism = 7;

        static readonly Domains[] Triad = { Domains.Jade, Domains.Ruby, Domains.Gold };

        /// <summary>
        /// One slab. <c>TiltDeg</c>/<c>AzimuthDeg</c> aim its normal (same composition as the
        /// cage's shell tilts - azimuth decides WHICH way, tilt decides how far),
        /// <c>OffsetFraction</c> slides it off the centre along that normal as a fraction of the
        /// arena radius, and <c>GrainDeg</c> spins the rib direction within the plane.
        /// </summary>
        readonly struct PaneSpec
        {
            public readonly float TiltDeg, AzimuthDeg, OffsetFraction, GrainDeg;
            public PaneSpec(float tilt, float azimuth, float offset, float grain)
            {
                TiltDeg = tilt; AzimuthDeg = azimuth; OffsetFraction = offset; GrainDeg = grain;
            }
        }

        /// <summary>
        /// Nine panes. Authored so that (a) no two normals are within ~25 degrees of each other,
        /// so there is no orientation from which several panes collapse into one; (b) the offsets
        /// alternate sign and magnitude, so the intersections spread through the ball; and (c) the
        /// grains do not rhyme, so a pilot who learns one pane's line has to re-read the next.
        /// </summary>
        static readonly PaneSpec[] PaneSpecs =
        {
            new PaneSpec(  0f,    0f,  0.10f,   0f),
            new PaneSpec( 31f,   47f, -0.38f,  55f),
            new PaneSpec( 58f,  118f,  0.44f,  20f),
            new PaneSpec( 84f,  196f, -0.12f,  78f),
            new PaneSpec( 46f,  259f,  0.29f,  38f),
            new PaneSpec( 70f,  324f, -0.50f,  12f),
            new PaneSpec( 24f,  152f,  0.50f,  66f),
            new PaneSpec( 62f,   82f, -0.22f,  85f),
            new PaneSpec( 39f,  293f,  0.33f,  47f),
        };

        protected override int DefaultSeed => 391;

        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnablePanes), R, PlankStep, PlankLength, RibStep,
            System.HashCode.Combine(VoidThreshold, VoidFreq, MullionStep, RimStep,
                                    DangerEveryNthRimPrism),
            PaneHash());

        static int PaneHash()
        {
            int h = 17;
            foreach (var p in PaneSpecs)
                h = h * 31 + System.HashCode.Combine(p.TiltDeg, p.AzimuthDeg, p.OffsetFraction, p.GrainDeg);
            return h;
        }

        protected override int LayCapacity => 6400;

        /// <summary>A pane resolved into the three directions and two scalars every builder needs,
        /// so no Build* method re-derives a frame.</summary>
        readonly struct Pane
        {
            public readonly Vector3 Normal;    // plane normal
            public readonly Vector3 Grain;     // in-plane, along the ribs
            public readonly Vector3 Cross;     // in-plane, rib to rib
            public readonly Vector3 Centre;    // Normal * (OffsetFraction * R)
            public readonly float Offset;      // signed distance of the plane from the cell centre
            public readonly float Rho;         // radius of the disc this plane cuts out of the ball

            public Pane(in PaneSpec spec)
            {
                var orientation = Quaternion.AngleAxis(spec.AzimuthDeg, Vector3.up)
                                * Quaternion.AngleAxis(spec.TiltDeg, Vector3.forward);
                Normal = (orientation * Vector3.up).normalized;

                // Any vector not parallel to the normal gives a usable in-plane basis. `forward`
                // is only parallel to the normal for a pane authored at tilt 90 / azimuth 90, so
                // `right` is the fallback rather than a general solution.
                var seedDir = Mathf.Abs(Vector3.Dot(Normal, Vector3.forward)) > 0.95f
                    ? Vector3.right : Vector3.forward;
                var e1 = Vector3.Cross(Normal, seedDir).normalized;
                var e2 = Vector3.Cross(Normal, e1);

                float g = spec.GrainDeg * Mathf.Deg2Rad;
                Grain = (e1 * Mathf.Cos(g) + e2 * Mathf.Sin(g)).normalized;
                Cross = Vector3.Cross(Normal, Grain);

                Offset = spec.OffsetFraction * R;
                Centre = Normal * Offset;
                Rho = Mathf.Sqrt(Mathf.Max(1f, R * R - Offset * Offset));
            }

            public Vector3 At(float along, float across) => Centre + Grain * along + Cross * across;
        }

        Pane[] _panes;

        /// <summary>A Cleave arena STATES its prism sizes: scaling the arena is a
        /// SIMILARITY, so the prisms grow with the spacing and a rib keeps reading as a
        /// continuous bar. The Panes state plank/rim/mullion lengths of 102/108/132 at 6 x — the last two clear
        /// the shared prefab's max of 100, and a clamped mullion is a gap in a wall.</summary>
        protected override bool AdmitsAuthoredPrismScale => true;

        protected override void BuildEnvironment()
        {
            _panes = new Pane[PaneSpecs.Length];
            for (int i = 0; i < PaneSpecs.Length; i++) _panes[i] = new Pane(PaneSpecs[i]);

            for (int i = 0; i < _panes.Length; i++)
            {
                BuildPaneRibs(_panes[i], i);
                BuildRim(_panes[i], i);
            }

            BuildMullions();
        }

        /// <summary>
        /// The corduroy. Ribs run along the pane's grain; each rib is a line of overlapping planks.
        /// Alternate ribs are offset by half a plank so the ends never line up into a visible seam
        /// running across the slab.
        /// </summary>
        void BuildPaneRibs(in Pane pane, int paneIndex)
        {
            int ribs = Mathf.Max(1, Mathf.FloorToInt(pane.Rho / RibStep));

            for (int rib = -ribs; rib <= ribs; rib++)
            {
                float across = rib * RibStep;
                // Half-chord of the disc at this offset - how far the rib runs before it leaves
                // the arena. Ribs near the edge are short; that taper IS the pane's round edge.
                float half = Mathf.Sqrt(Mathf.Max(0f, pane.Rho * pane.Rho - across * across));
                if (half < PlankLength) continue;

                int planks = Mathf.Max(1, Mathf.FloorToInt(half / PlankStep));
                float stagger = (rib & 1) == 0 ? 0f : PlankStep * 0.5f;
                var dom = Triad[(paneIndex + Mathf.Abs(rib)) % Triad.Length];

                for (int i = -planks; i <= planks; i++)
                {
                    float along = i * PlankStep + stagger;
                    if (Mathf.Abs(along) > half) continue;

                    var pos = pane.At(along, across);
                    if (N01(pos.x * VoidFreq, pos.y * VoidFreq, pos.z * VoidFreq, 2) < VoidThreshold)
                        continue;

                    Emit(pos, SpawnPoint.LookRotation(pane.Grain, pane.Normal),
                        Jit(new Vector3(3.4f * S, 3.4f * S, PlankLength)), dom);
                }
            }
        }

        /// <summary>
        /// The frame around a pane's disc, and the arena's only traps. A rim prism lies along the
        /// boundary circle's own tangent, so the edge reads as a hoop rather than as ribs that
        /// happen to stop.
        /// </summary>
        void BuildRim(in Pane pane, int paneIndex)
        {
            int n = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * pane.Rho / RimStep));

            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);

                var pos = pane.At(c * pane.Rho, s * pane.Rho);
                var tangent = pane.Grain * -s + pane.Cross * c;

                // Phase the trap walk per pane so the traps do not stack up at the same bearing
                // on every rim - the same reason the cage phases its danger walk per shell.
                bool danger = (paneIndex * 31 + i) % DangerEveryNthRimPrism == 0;

                Emit(pos, SpawnPoint.LookRotation(tangent, pane.Normal),
                    Jit(new Vector3(4.2f * S, 4.2f * S, RimLength)), Domains.Blue,
                    danger ? PrismKind.Danger : PrismKind.Plain);
            }
        }

        /// <summary>
        /// A beam down every pane-pair intersection - the straightest, densest run of mass in the
        /// arena and therefore the best cut available.
        ///
        /// The line is exact rather than searched. Two planes (n1,d1) and (n2,d2) meet along a line
        /// with direction <c>n1 x n2</c> passing through
        /// <c>((d1 - d2 (n1.n2)) n1 + (d2 - d1 (n1.n2)) n2) / (1 - (n1.n2)^2)</c>; that point is the
        /// one on the line closest to the cell centre, so the chord it cuts out of the ball is
        /// symmetric about it and the half-length is a single Pythagoras.
        /// </summary>
        void BuildMullions()
        {
            for (int a = 0; a < _panes.Length; a++)
            for (int b = a + 1; b < _panes.Length; b++)
            {
                ref readonly Pane p = ref _panes[a];
                ref readonly Pane q = ref _panes[b];

                float dot = Vector3.Dot(p.Normal, q.Normal);
                float denom = 1f - dot * dot;
                // Near-parallel panes have no usable intersection inside the ball; the authored
                // normals are all >25 degrees apart, so this is a guard, not a case.
                if (denom < 0.05f) continue;

                var dir = Vector3.Cross(p.Normal, q.Normal).normalized;
                var foot = (p.Normal * (p.Offset - q.Offset * dot)
                          + q.Normal * (q.Offset - p.Offset * dot)) / denom;

                float footSq = Vector3.Dot(foot, foot);
                if (footSq >= R * R) continue;              // the line misses the ball entirely
                float half = Mathf.Sqrt(R * R - footSq);
                if (half < MullionLength) continue;

                int n = Mathf.Max(1, Mathf.FloorToInt(half / MullionStep));
                var dom = Triad[(a + b) % Triad.Length];

                for (int i = -n; i <= n; i++)
                {
                    var pos = foot + dir * (i * MullionStep);
                    // `dir` lies in BOTH planes, so either normal is a legal "up" for it. Using
                    // p's keeps a mullion visually parented to the pane whose grain it crosses.
                    Emit(pos, SpawnPoint.LookRotation(dir, p.Normal),
                        Jit(new Vector3(5.2f * S, 5.2f * S, MullionLength)), dom);
                }
            }
        }
    }
}
