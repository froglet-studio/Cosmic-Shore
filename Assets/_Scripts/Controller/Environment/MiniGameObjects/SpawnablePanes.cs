using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

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
    ///     spaced <see cref="RibStep"/> apart across the grain and their planks are laid FLUSH
    ///     along it, end to end at <see cref="PlankStep"/>, which is what makes a rib read as one
    ///     continuous line rather than a row of beads. (They used to OVERLAP - a 34-unit plank on
    ///     a 30-unit step - and passed through each other; NO PRISM MAY INTERPENETRATE ANOTHER,
    ///     Docs/ECOSYSTEM.md §49.10.)
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
        /// is still a line of end-to-end planks, so a pane thins out without breaking into beads.
        /// Rib count falls by G; planks per rib, mullions and rims are untouched.</summary>
        const float G = SliceArenaGeometry.GapScaleI1;

        /// <summary>Authored-units -> world-units for a PRISM'S OWN DIMENSIONS and for the
        /// ALONG-grain step that keeps a rib reading as one continuous bar. Deliberately NOT
        /// <see cref="S"/>: at 6 a plank was 20 x 20 x 102 world units and the pane read as low
        /// poly. Small prisms, lots of them - see <c>SliceArenaGeometry</c>'s summary.</summary>
        const float P = SliceArenaGeometry.PrismScaleI1;

        // ── The weave ────────────────────────────────────────────────────────
        /// <summary>Spacing ALONG a rib, and a plank's length: consecutive planks meet end to end,
        /// so a rib reads as one continuous bar you can drag a blade down.</summary>
        const float PlankStep = 15f * P;
        /// <summary>The shortest half-chord a rib may have and still be laid at all. Its old second
        /// job, the plank's length, overlapped consecutive planks; it now only decides WHICH ribs
        /// exist, so it keeps its value - changing it would change the count.</summary>
        const float PlankLength = 17f * P;
        /// <summary>A plank's cross-section, jittered: nothing beside a plank is near enough to
        /// touch it, so its one jitter draw goes here.</summary>
        const float PlankSection = 3.4f * P;
        /// <summary>Spacing ACROSS the grain, i.e. rib to rib. Deliberately much wider than
        /// <see cref="PlankStep"/> - that ratio IS the corduroy, and it is what lets a pilot see
        /// (and fly) through a pane instead of meeting a wall.</summary>
        const float RibStep = 21f * S * G;

        /// <summary>Value-noise threshold below which a plank is skipped. Breaks each pane into
        /// weathered patches and open windows so the arena is porous rather than nine barricades.</summary>
        const float VoidThreshold = 0.30f;
        const float VoidFreq = 0.016f / S;

        // ── Mullions (pane x pane intersections) ─────────────────────────────
        /// <summary>Spacing along a pane-pair intersection, and a mullion prism's length: they
        /// meet end to end.</summary>
        const float MullionStep = 19f * P;
        /// <summary>The shortest half-chord an intersection may have and still carry mullions (no
        /// longer a prism size - see <see cref="PlankLength"/>).</summary>
        const float MullionLength = 22f * P;
        const float MullionSection = 5.2f * P;

        // ── Rims (the frame around each pane, and the arena's only traps) ─────
        const float RimStep = 16f * P;
        /// <summary>A rim prism's radial width (fixed: it is what the planks stop at) and nominal
        /// height (jittered). Its length is the chord to its neighbours.</summary>
        const float RimWidth = 4.2f * P;
        /// <summary>Every Nth rim prism is a trap. The rim is what a sloppy pass clips, so the
        /// downside sits exactly where the mistake is.</summary>
        const int DangerEveryNthRimPrism = 7;

        static readonly Domains[] Triad = { Domains.Jade, Domains.Ruby, Domains.Gold };

        // ── Flush ─────────────────────────────────────────────────────────────
        /// <summary>How close to exact contact a FITTED face is laid: a hair under, so float noise
        /// in the fit never turns a touching pair into an interpenetrating one (NO PRISM MAY
        /// INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md §49.10).</summary>
        const float Flush = 0.995f;
        /// <summary>Clearance a plank keeps from its pane's rim, and anything keeps from another
        /// pane's slab, on top of the exact geometry - room for float noise, nothing more.</summary>
        const float Clearance = 0.25f;

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
                                    DangerEveryNthRimPrism, Flush, Clearance),
            PaneHash());

        static int PaneHash()
        {
            int h = 17;
            foreach (var p in PaneSpecs)
                h = h * 31 + System.HashCode.Combine(p.TiltDeg, p.AzimuthDeg, p.OffsetFraction, p.GrainDeg);
            return h;
        }

        protected override int LayCapacity => 18000;

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

        /// <summary>A Cleave arena STATES its prism sizes, so its lay goes through
        /// <c>Prism.AdmitTargetScale</c> rather than trusting <c>PrismScaleAnimator</c>'s
        /// serialized window. It used to be REQUIRED here: at the old sizes the rim and mullion
        /// were 108 and 132 world units, past the shared prefab's <c>maxScale</c> of 100, and a
        /// clamped mullion is a gap in a wall. <b>Every length this arena now lays is inside
        /// that window</b> (at most 30/32/38, plank/rim/mullion, now they are laid flush), because
        /// prism size came off the envelope scale — so the
        /// flag is a standing GUARD rather than a fix, and the clamp having fired at all is worth
        /// keeping in mind: <i>a shared prefab's scale ceiling was the only thing in the project
        /// telling us the prisms had grown absurd.</i></summary>
        protected override bool AdmitsAuthoredPrismScale => true;

        protected override void BuildEnvironment()
        {
            _panes = new Pane[PaneSpecs.Length];
            for (int i = 0; i < PaneSpecs.Length; i++) _panes[i] = new Pane(PaneSpecs[i]);
            MeasureSlabs();

            for (int i = 0; i < _panes.Length; i++)
            {
                BuildPaneRibs(_panes[i], i);
                BuildRim(_panes[i], i);
            }

            BuildMullions();
        }

        /// <summary>
        /// The corduroy. Ribs run along the pane's grain; each rib is a line of planks laid end to
        /// end (a plank fills its slot between its neighbours, and stops at the rim and at any
        /// pane crossing the rib - see <see cref="TryFit"/> and <see cref="RehouseStuck"/>).
        /// Alternate ribs are offset by half a plank so the ends never line up into a visible seam
        /// running across the slab.
        /// </summary>
        void BuildPaneRibs(in Pane pane, int paneIndex)
        {
            int ribs = Mathf.Max(1, Mathf.FloorToInt(pane.Rho / RibStep));
            // The rim's inner face is planned at the planks' largest section, so a rib's end
            // stops at the same place whatever its last plank draws.
            float hsMost = 0.5f * PlankSection * 1.2f;
            float inner = pane.Rho - 0.5f * RimWidth - Clearance;

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

                // The rib's last usable length: inside the rim's inner face (the rim is the rib's
                // last neighbour, so its end plank stops at it).
                float side = Mathf.Abs(across) + hsMost;
                float end = inner > side ? Mathf.Sqrt(inner * inner - side * side) : 0f;

                // Pass 1 - the rib, planned before any plank is laid: which planks exist (the void
                // cull, exactly as before), and the piece of its own slot each one fills - the
                // whole slot, or, where another pane crosses the rib, what is clear of it.
                int count = 2 * planks + 1;
                EnsureRail(count);
                for (int j = 0; j < count; j++)
                {
                    float along = (j - planks) * PlankStep + stagger;
                    var pos = pane.At(along, across);
                    bool laid = Mathf.Abs(along) <= half &&
                                N01(pos.x * VoidFreq, pos.y * VoidFreq, pos.z * VoidFreq, 2) >= VoidThreshold;
                    // A laid plank spends its one jitter draw here, on its section - planning walks
                    // the planks in the order they are laid, so the stream is unchanged.
                    float hs = laid ? 0.5f * PlankSection * JitFactor() : hsMost;
                    PlanMember(j, laid, pos, pane.Grain, pane.Cross, hs, pane.Normal, hs,
                               Mathf.Max(-0.5f * PlankStep, -end - along),
                               Mathf.Min(0.5f * PlankStep, end - along), paneIndex, -1);
                }

                // Pass 2 - rehouse the planks the crossing left no room in (RehouseStuck).
                RehouseStuck(count, false, PlankStep);

                // Pass 3 - lay them, in the original order.
                for (int j = 0; j < count; j++)
                    if (_railLaid[j]) LayMember(j, dom, PrismKind.Plain);
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

            // A chord of the circle: the inner corners of neighbouring rim prisms meet on the
            // radius that bisects them, so a prism's half-length is its inner radius times
            // tan(half the angular step). Its radial width is fixed (the planks stop at it); its
            // height carries the jitter, drawn as it is planned (the same order it is laid in).
            float hw = 0.5f * RimWidth;
            float halfStep = Mathf.PI / n;
            float chord = (pane.Rho - hw) * Mathf.Sin(halfStep) / Mathf.Cos(halfStep);

            EnsureRail(n);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                var pos = pane.At(c * pane.Rho, s * pane.Rho);
                var tangent = pane.Grain * -s + pane.Cross * c;
                var radial = pane.Grain * c + pane.Cross * s;
                PlanMember(i, true, pos, tangent, radial, hw, pane.Normal, hw * JitFactor(),
                           -chord, chord, paneIndex, -1);
            }
            RehouseStuck(n, true, 2f * chord);

            for (int i = 0; i < n; i++)
            {
                // Phase the trap walk per pane so the traps do not stack up at the same bearing
                // on every rim - the same reason the cage phases its danger walk per shell.
                bool danger = (paneIndex * 31 + i) % DangerEveryNthRimPrism == 0;
                LayMember(i, Domains.Blue, danger ? PrismKind.Danger : PrismKind.Plain);
            }
        }

        /// <summary>
        /// The intersection line of panes <paramref name="a"/> and <paramref name="b"/>, if it
        /// carries mullions: its direction, the point on it nearest the cell centre, and the half
        /// length of the chord it cuts out of the ball.
        ///
        /// The line is exact rather than searched. Two planes (n1,d1) and (n2,d2) meet along a line
        /// with direction <c>n1 x n2</c> passing through
        /// <c>((d1 - d2 (n1.n2)) n1 + (d2 - d1 (n1.n2)) n2) / (1 - (n1.n2)^2)</c>; that point is the
        /// one on the line closest to the cell centre, so the chord it cuts out of the ball is
        /// symmetric about it and the half-length is a single Pythagoras.
        /// </summary>
        bool MullionLine(int a, int b, out Vector3 dir, out Vector3 foot, out float chordHalf)
        {
            ref readonly Pane p = ref _panes[a];
            ref readonly Pane q = ref _panes[b];
            dir = foot = Vector3.zero; chordHalf = 0f;

            float dot = Vector3.Dot(p.Normal, q.Normal);
            float denom = 1f - dot * dot;
            // Near-parallel panes have no usable intersection inside the ball; the authored
            // normals are all >25 degrees apart, so this is a guard, not a case.
            if (denom < 0.05f) return false;

            dir = Vector3.Cross(p.Normal, q.Normal).normalized;
            foot = (p.Normal * (p.Offset - q.Offset * dot)
                  + q.Normal * (q.Offset - p.Offset * dot)) / denom;

            float footSq = Vector3.Dot(foot, foot);
            if (footSq >= R * R) return false;              // the line misses the ball entirely
            chordHalf = Mathf.Sqrt(R * R - footSq);
            return chordHalf >= MullionLength;
        }

        // ── Rails ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Every structure here is a RAIL - a rib of planks, a rim, a mullion line - whose members
        /// each own a slot along their own axis. A rail is planned before any of it is laid
        /// (<see cref="PlanMember"/>: the clear piece of each member's slot), so a member the
        /// crossing pane left NO room in can be rehoused (<see cref="RehouseStuck"/>) before
        /// <see cref="LayMember"/> lays them. Planning walks the members in the order they are
        /// laid and spends each one's jitter draw as it goes, so the stream is unchanged.
        /// </summary>
        void EnsureRail(int count)
        {
            if (_railPos != null && _railPos.Length >= count) return;
            int size = Mathf.Max(count, 2 * (_railPos?.Length ?? 0));
            _railPos = new Vector3[size]; _railZ = new Vector3[size]; _railY = new Vector3[size];
            _railHx = new float[size]; _railHy = new float[size];
            _railLaid = new bool[size]; _railClear = new bool[size];
            _railLo = new float[size]; _railHi = new float[size]; _railHost = new int[size];
            _railGuestsLo = new int[size]; _railGuestsHi = new int[size]; _railGuestOf = new int[size];
        }

        Vector3[] _railPos, _railZ, _railY;
        bool[] _railLaid, _railClear;
        float[] _railLo, _railHi, _railHx, _railHy;
        int[] _railHost, _railGuestsLo, _railGuestsHi, _railGuestOf;

        /// <summary>Plan member <paramref name="j"/>: its lay point and axes, and the piece
        /// [lo, hi] of its slot (offsets along <paramref name="z"/>) that is clear of every other
        /// pane, at the section it drew (<see cref="TryFit"/>).</summary>
        void PlanMember(int j, bool laid, Vector3 pos, Vector3 z, Vector3 x, float hx, Vector3 y, float hy,
                        float lo, float hi, int ownA, int ownB)
        {
            _railPos[j] = pos; _railZ[j] = z; _railY[j] = y; _railLaid[j] = laid; _railHost[j] = j;
            _railHx[j] = hx; _railHy[j] = hy;
            // Planned whether or not it is laid: an empty slot's clear piece is room a stuck
            // neighbour can move into.
            _railClear[j] = TryFit(pos, z, x, hx, y, hy, lo, hi, ownA, ownB, out float offset, out float piece);
            _railLo[j] = _railClear[j] ? offset - piece : 0f;
            _railHi[j] = _railClear[j] ? offset + piece : 0f;
        }

        /// <summary>
        /// A member with NO clear piece of its slot sits inside a crossing pane's slab - a
        /// placement no shrink can fix - so it has to move, and the least disturbance is along
        /// its own rail: it becomes a GUEST in the nearest clear piece that has room for one more
        /// (an empty slot's - a plank the void cull removed - or a laid neighbour's), and the
        /// piece is shared evenly between its owner and its guests, no share under a quarter of a
        /// slot. The owner keeps the share nearest where it was laid; guests from either side take
        /// the shares on their side. Every share is a sub-box of a planned clear piece, laid on
        /// that slot's own line, so a move reaches nothing new; nothing but the stuck members
        /// moves, and their neighbours only shorten.
        ///
        /// Runs of stuck members are short except where the authored panes make them long:
        /// panes 0, 4 and 7 meet along three lines within 3 degrees of one another, so their
        /// mullions lie in each other for ~500 units around the triple point, and two of the
        /// three lines' members there move out to where their own line is clear.
        /// </summary>
        void RehouseStuck(int count, bool cyclic, float slot)
        {
            float least = slot / 4f;
            for (int k = 0; k < count; k++) { _railGuestsLo[k] = 0; _railGuestsHi[k] = 0; }

            for (int j = 0; j < count; j++)
            {
                if (!_railLaid[j] || _railClear[j]) continue;
                int host = -1;
                for (int d = 1; d < count && host < 0; d++)
                for (int dir = -1; dir <= 1 && host < 0; dir += 2)
                {
                    int r = j + dir * d;
                    if (cyclic) r = (r % count + count) % count;
                    else if (r < 0 || r >= count) continue;
                    if (r == j || !_railClear[r] || _railHost[r] != r) continue;
                    int members = (_railLaid[r] ? 1 : 0) + _railGuestsLo[r] + _railGuestsHi[r] + 1;
                    if ((_railHi[r] - _railLo[r]) / members < least) continue;
                    host = r;
                    // A guest from the slot's low side takes a low share, and vice versa.
                    bool fromLow = cyclic ? ((j - r + count) % count) > count / 2 : j < r;
                    if (fromLow) _railGuestsLo[r]++; else _railGuestsHi[r]++;
                    _railGuestOf[j] = fromLow ? -(_railGuestsLo[r]) : _railGuestsHi[r];
                }
                _railHost[j] = host < 0 ? j : host;
                if (host < 0)
                {
                    _railLo[j] = _railHi[j] = 0f;
                    CSDebug.LogWarning($"[Panes] A prism at {_railPos[j]} found no clear room on its rail; laid as a sliver.");
                }
            }

            // Split each shared piece: [low guests][owner][high guests], in equal shares.
            for (int r = 0; r < count; r++)
            {
                int lowN = _railGuestsLo[r], highN = _railGuestsHi[r];
                if (lowN + highN == 0) continue;
                int owner = _railLaid[r] ? 1 : 0;
                float width = (_railHi[r] - _railLo[r]) / (lowN + owner + highN);
                float baseLo = _railLo[r];
                for (int j = 0; j < count; j++)
                {
                    if (_railHost[j] != r || j == r) continue;
                    int g = _railGuestOf[j];
                    // Low guests nearest the owner take the shares next to it.
                    int index = g < 0 ? lowN + g : lowN + owner + g - 1;
                    _railLo[j] = baseLo + width * index;
                    _railHi[j] = _railLo[j] + width;
                }
                if (owner == 1)
                {
                    _railLo[r] = baseLo + width * lowN;
                    _railHi[r] = _railLo[r] + width;
                }
            }
        }

        /// <summary>Lay member <paramref name="j"/> as planned, on its host's line (its own unless
        /// it was rehoused), with its own section - no wider than the host's, which is the
        /// section its clear piece was planned at.</summary>
        void LayMember(int j, Domains dom, PrismKind kind)
        {
            int host = _railHost[j];
            float hx = Mathf.Min(_railHx[j], _railHx[host]), hy = Mathf.Min(_railHy[j], _railHy[host]);
            float centre = 0.5f * (_railLo[j] + _railHi[j]);
            float half = Mathf.Max(0.01f, 0.5f * (_railHi[j] - _railLo[j]) * Flush);
            Emit(_railPos[host] + _railZ[host] * centre, SpawnPoint.LookRotation(_railZ[host], _railY[host]),
                new Vector3(2f * hx, 2f * hy, 2f * half), dom, kind);
        }

        /// <summary>
        /// How far each pane's own prisms reach off its plane, along its normal, at their largest
        /// jitter. Everywhere: a plank's or a rim's height (<see cref="_flat"/>). Near one of its
        /// intersections, deeper - the mullion on that line, which leans across the pane at the
        /// angle between the two (<see cref="_lean"/>; <see cref="SlabNear"/> asks which lines a
        /// prism is near). A prism of any OTHER structure stays out of this slab
        /// (<see cref="TryFit"/>) and everything that belongs to the pane is inside it, so nothing
        /// can pass through anything.
        /// </summary>
        void MeasureSlabs()
        {
            int n = _panes.Length;
            if (_lean == null || _lean.Length != n * n)
            {
                _lean = new float[n * n]; _lineDir = new Vector3[n * n]; _lineFoot = new Vector3[n * n];
            }
            _flat = 0.5f * Mathf.Max(PlankSection, RimWidth) * 1.2f;
            _deepest = _flat;
            float mostMullion = 0.5f * MullionSection * 1.2f;
            for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++)
            {
                _lean[a * n + b] = 0f;
                if (a == b || !MullionLine(Mathf.Min(a, b), Mathf.Max(a, b), out var dir, out var foot, out _))
                    continue;
                // A mullion of (lo, hi) stands on lo's normal; across hi it shows its side and its
                // height at the angle between the two planes. _lean[a, b]: its depth in a's slab.
                float cos = Mathf.Abs(Vector3.Dot(_panes[a].Normal, _panes[b].Normal));
                float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
                _lean[a * n + b] = a < b ? mostMullion : mostMullion * (cos + sin);
                _lineDir[a * n + b] = dir; _lineFoot[a * n + b] = foot;
                _deepest = Mathf.Max(_deepest, _lean[a * n + b]);
            }
        }

        float _flat, _deepest;
        float[] _lean;
        Vector3[] _lineDir, _lineFoot;

        /// <summary>The depth of pane <paramref name="k"/>'s slab around a prism at
        /// <paramref name="pos"/> that reaches <paramref name="reach"/> from it: its planks and rim,
        /// and every mullion of <paramref name="k"/> whose line passes within reach.</summary>
        float SlabNear(int k, Vector3 pos, float reach)
        {
            int n = _panes.Length;
            float slab = _flat;
            float mullionRadius = 0.5f * MullionSection * 1.2f * 1.41421356f;
            for (int m = 0; m < n; m++)
            {
                float lean = _lean[k * n + m];
                if (lean <= slab) continue;
                var off = pos - _lineFoot[k * n + m];
                var dir = _lineDir[k * n + m];
                float along = Vector3.Dot(off, dir);
                if ((off - dir * along).sqrMagnitude < (mullionRadius + reach) * (mullionRadius + reach))
                    slab = lean;
            }
            return slab;
        }

        /// <summary>
        /// Fit a prism - centre <paramref name="pos"/>, laid along <paramref name="z"/>, half-extents
        /// <paramref name="hx"/> / <paramref name="hy"/> across <paramref name="x"/> /
        /// <paramref name="y"/> - into its own SLOT [<paramref name="lo"/>, <paramref name="hi"/>]
        /// along <c>z</c> (offsets from <c>pos</c>: where its own neighbours start), and out of
        /// the slab of every pane except <paramref name="ownA"/> / <paramref name="ownB"/>.
        ///
        /// The slot is filled: an interior prism keeps its centre, a rib's end plank stops at the
        /// rim. Where another pane crosses, the prism shrinks ABOUT the slot's centre until it
        /// stops at that pane's slab, unless the centre is inside the slab or shrinking in place
        /// would keep under a quarter of what the larger clear piece of its slot keeps - then it
        /// becomes that piece. Returns the piece's centre offset along <c>z</c> and half-length
        /// (before <see cref="Flush"/>); false when no piece of the slot is clear.
        /// </summary>
        bool TryFit(Vector3 pos, Vector3 z, Vector3 x, float hx, Vector3 y, float hy,
                    float lo, float hi, int ownA, int ownB, out float offset, out float half)
        {
            offset = 0.5f * (lo + hi); half = 0f;
            if (hi <= lo) return false;

            int blocks = 0;
            float far = Mathf.Max(Mathf.Abs(lo), Mathf.Abs(hi));
            for (int k = 0; k < _panes.Length; k++)
            {
                if (k == ownA || k == ownB) continue;
                ref readonly Pane other = ref _panes[k];
                float d = Vector3.Dot(other.Normal, pos) - other.Offset;
                float g = Vector3.Dot(z, other.Normal);
                float perp = hx * Mathf.Abs(Vector3.Dot(x, other.Normal))
                           + hy * Mathf.Abs(Vector3.Dot(y, other.Normal));
                // Nowhere near even the deepest slab: done in two dot products.
                if (Mathf.Abs(d) >= _deepest + Clearance + perp + Mathf.Abs(g) * far) continue;
                float reach = SlabNear(k, pos, far + hx + hy) + Clearance + perp;
                if (Mathf.Abs(d) >= reach + Mathf.Abs(g) * far) continue;
                if (Mathf.Abs(g) < 1e-5f)
                {
                    _blockLo[blocks] = float.NegativeInfinity; _blockHi[blocks] = float.PositiveInfinity;
                }
                else
                {
                    float s0 = (-reach - d) / g, s1 = (reach - d) / g;
                    _blockLo[blocks] = Mathf.Min(s0, s1); _blockHi[blocks] = Mathf.Max(s0, s1);
                }
                if (++blocks == _blockLo.Length) break;
            }

            float mid = 0.5f * (lo + hi);
            if (blocks == 0)
            {
                half = 0.5f * (hi - lo);
                return true;
            }

            // In place: the clear stretch around the slot's centre, if the centre is clear.
            float inPlace = 0f;
            bool centreClear = true;
            float room = 0.5f * (hi - lo);
            for (int i = 0; i < blocks; i++)
            {
                if (_blockLo[i] < mid && _blockHi[i] > mid) { centreClear = false; break; }
                if (_blockHi[i] <= mid) room = Mathf.Min(room, mid - Mathf.Max(lo, _blockHi[i]));
                else room = Mathf.Min(room, Mathf.Min(hi, _blockLo[i]) - mid);
            }
            if (centreClear) inPlace = Mathf.Max(0f, room);

            // The larger clear piece of the slot.
            float best = 0f, bestMid = mid;
            for (int k = -1; k < blocks; k++)
            {
                float start = k < 0 ? lo : Mathf.Max(lo, _blockHi[k]);
                if (start >= hi) continue;
                bool inside = false;
                for (int i = 0; i < blocks; i++)
                    if (_blockLo[i] < start && _blockHi[i] > start) { inside = true; break; }
                if (inside) continue;
                float stop = hi;
                for (int i = 0; i < blocks; i++)
                    if (_blockLo[i] >= start) stop = Mathf.Min(stop, _blockLo[i]);
                if (stop - start > best) { best = stop - start; bestMid = 0.5f * (start + stop); }
            }

            if (inPlace > 0f && 4f * (2f * inPlace) >= best)
            {
                offset = mid; half = inPlace;
                return true;
            }
            if (best <= 0f) return false;
            offset = bestMid; half = 0.5f * best;
            return true;
        }

        readonly float[] _blockLo = new float[8], _blockHi = new float[8];

        /// <summary>One jitter factor in [0.8, 1.2] from the shared stream - exactly the one draw
        /// <c>Jit</c> makes, so spending it on a single axis moves no other prism.</summary>
        float JitFactor() => Jit(Vector3.one).x;

        /// <summary>
        /// A beam down every pane-pair intersection - the straightest, densest run of mass in the
        /// arena and therefore the best cut available. The line is exact (<see cref="MullionLine"/>);
        /// its prisms meet end to end and stop short of any third pane crossing it.
        /// </summary>
        void BuildMullions()
        {
            for (int a = 0; a < _panes.Length; a++)
            for (int b = a + 1; b < _panes.Length; b++)
            {
                ref readonly Pane p = ref _panes[a];
                if (!MullionLine(a, b, out var dir, out var foot, out float chordHalf)) continue;

                int n = Mathf.Max(1, Mathf.FloorToInt(chordHalf / MullionStep));
                var dom = Triad[(a + b) % Triad.Length];

                // `dir` lies in BOTH planes, so either normal is a legal "up" for it. Using p's
                // keeps a mullion visually parented to the pane whose grain it crosses. End to end
                // along the line, and out of every OTHER pane's slab where a third pane crosses.
                var side = Vector3.Cross(p.Normal, dir).normalized;
                int count = 2 * n + 1;
                EnsureRail(count);
                for (int i = 0; i < count; i++)
                {
                    float hs = 0.5f * MullionSection * JitFactor();
                    PlanMember(i, true, foot + dir * ((i - n) * MullionStep), dir, side, hs,
                               p.Normal, hs, -0.5f * MullionStep, 0.5f * MullionStep, a, b);
                }
                RehouseStuck(count, false, MullionStep);
                for (int i = 0; i < count; i++) LayMember(i, dom, PrismKind.Plain);
            }
        }
    }
}
