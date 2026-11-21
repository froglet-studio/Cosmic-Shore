using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Twistbands" - Cleave's intensity-4 arena: three interlocked MÖBIUS ribbons, each
    /// carrying a solid plated deck, threaded through one another like the great circles of a
    /// sphere.
    ///
    /// The other three arenas are made of surfaces that hold still. A pane is a plane, a swell
    /// ripples but keeps its plane, and a shell curves but curves the same way everywhere. A
    /// Möbius band does something none of them do: <b>its surface rotates about its own direction
    /// of travel as you fly it</b>. Hold a blade against the deck and the deck turns out from under
    /// it, so the cut only continues if the pilot keeps rolling the sword to match - which is
    /// precisely the axis the Rhino's triggers drive (RT-LT is yaw AND roll, see
    /// <c>RHINO_SHIELD_SWIPE.md</c>). This is the one arena in the mode that asks for the
    /// swordsmanship rather than the line.
    ///
    /// It is also the hardest to READ, which is why it sits at the top of the ladder: the band
    /// carries an ODD number of half twists and is therefore genuinely one-sided, so flying a lap
    /// returns you to your own starting patch upside down. Mass you already cut is now above you.
    /// There is no global up to orient against and no silhouette to peel inward through - you
    /// navigate by the ribbon itself.
    ///
    /// The band frame (<see cref="Band"/>) is <c>SpawnableOurobor</c>'s, deliberately unchanged:
    /// the width direction rotates <c>TwistRate * u</c> out of the loop plane, which IS the twist,
    /// and <c>AlongSurface</c> is the exact partial derivative rather than the loop tangent, so a
    /// plate laid near an edge is square to the surface it is actually on. What differs is what
    /// rides on it - Ourobor grows countryside and a skyline on a band you LAND on; this lays a
    /// deck you CUT.
    ///
    /// Four properties are load-bearing:
    ///
    ///   • <b>The deck is solid, not a lattice.</b> Plates are laid FLUSH in both directions,
    ///     each fitted to the gap its neighbours leave (<see cref="BuildDeck"/>). A sparse deck on
    ///     a narrow ribbon in a mostly empty ball would be almost impossible to stay in contact
    ///     with; a solid one means a blade that finds the surface keeps paying for as long as the
    ///     pilot can hold it there. (They used to OVERLAP - a 20-unit plate on a 14-unit step - and
    ///     every plate passed through its neighbours; NO PRISM MAY INTERPENETRATE ANOTHER,
    ///     Docs/ECOSYSTEM.md §49.10.)
    ///   • <b>Lanes are painted ACROSS the ribbon.</b> Three domain lanes, so the deck reads as a
    ///     road - and because the band is one-sided, the lane order MIRRORS after a lap. The
    ///     arena's central joke is a thing the player can see rather than a thing the doc claims.
    ///   • <b>There is a keel.</b> A one-plate deck is invisible edge-on and a blade can cross the
    ///     plane of it without touching anything. Half-density plates hang
    ///     <see cref="KeelDrop"/> below, turned crosswise, so the ribbon has body from every angle.
    ///   • <b>The cornice is ONE curve, and it needs u to run 0..4π to close.</b> That is the
    ///     band's own proof of one-sidedness (after 2π the <c>+halfWidth</c> edge has become the
    ///     <c>-halfWidth</c> edge), and it is where every <see cref="PrismKind.Danger"/> trap in
    ///     this arena sits - the edge is what a pilot clips when the roll does not keep up.
    ///
    /// Nothing is <see cref="PrismKind.Shielded"/> or <see cref="PrismKind.SuperShielded"/> - see
    /// CLEAVE.md. Deterministic per seed like every cell environment.
    /// </summary>
    public class SpawnableTwistbands : CellEnvironmentSpawnableBase
    {
        /// <summary>This arena IS intensity 4, so it carries that rung's dials as constants.</summary>
        const float R = SliceArenaGeometry.OuterRadiusI4;

        /// <summary>Authored-units -> world-units; see <c>SliceArenaGeometry</c>. Every LENGTH here
        /// is multiplied by it and the noise frequency divided by it. A band's radius and half-width
        /// are lengths too, and are scaled inside <see cref="BandSpec"/>'s constructor so that EVERY
        /// reader of them - the deck, the cornice and the envelope guard alike - gets the scaled
        /// value without each having to remember to ask.</summary>
        const float S = SliceArenaGeometry.LengthScaleI4;

        /// <summary>Extra ACROSS-ribbon plate spacing. Authored at 1 for this rung: the deck is a
        /// continuous plated ROAD a blade is held against, so opening its lanes up is not "sparser",
        /// it is a lattice the sword rattles through. Kept as a named dial so the table reads the
        /// same on all four rungs.</summary>
        const float G = SliceArenaGeometry.GapScaleI4;

        /// <summary>Authored-units -> world-units for a PRISM'S OWN DIMENSIONS and for BOTH deck
        /// steps. A plated deck is continuous in both directions, so the plate and the two steps it
        /// tiles at move together, while the ribbon's <c>Radius</c>/<c>HalfWidth</c> stay on
        /// <see cref="S"/> — a bigger place, paved with the same small plates. This rung's
        /// <see cref="S"/> is already 2, so the split changes nothing here.</summary>
        const float P = SliceArenaGeometry.PrismScaleI4;

        // ── The deck ─────────────────────────────────────────────────────────
        /// <summary>Row spacing along the centreline. A plate's LENGTH is no longer a constant:
        /// it is fitted per plate to the half-planes between its row and the next (see
        /// <see cref="BuildDeck"/>), where a constant 10 x P on this 7 x P step used to put every
        /// plate through all eight of its neighbours.</summary>
        const float PlateStepAlong = 7f * P;
        /// <summary>Nominal lane spacing; the real one is <c>2 HalfWidth / nv</c>, and a plate is
        /// exactly that wide (less <see cref="Flush"/>).</summary>
        const float PlateStepAcross = 7.5f * P * G;
        const float PlateThickness = 3f * P;
        /// <summary>The keel bar's long axis, laid across the ribbon under a plate.</summary>
        const float KeelLength = 10f * P;

        /// <summary>How close to exact contact a FITTED face is laid: a hair under, so float noise
        /// in the fit never turns a touching pair into an interpenetrating one (NO PRISM MAY
        /// INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md §49.10).</summary>
        const float Flush = 0.995f;
        /// <summary>Clearance the outermost deck plates keep from the cornice's inner face, for the
        /// row-to-row turn of the width axis between a plate and the rail prism beside it.</summary>
        const float EdgeMargin = 1f;

        /// <summary>How far below the deck the keel hangs, along the surface normal.</summary>
        const float KeelDrop = 11f * S;

        /// <summary>Light weathering only. The deck's whole job is to be continuous enough to hold
        /// a blade against, so this is a fraction of the panes' cull, not a match for it.</summary>
        const float VoidThreshold = 0.12f;
        const float VoidFreq = 0.02f / S;

        // ── The cornice (the single boundary curve) ──────────────────────────
        const float CorniceStep = 13f * P;
        /// <summary>The rail's width (facing the deck, fixed so the deck can stop at it) and
        /// nominal height (jittered). Its length is fitted to its two neighbours.</summary>
        const float CorniceWidth = 4.4f * P;
        /// <summary>Every Nth cornice prism is a trap.</summary>
        const int DangerEveryNthCornicePrism = 4;

        /// <summary>Three lanes across the ribbon, so the deck reads as a road - and mirrors after
        /// a lap, which is the one-sidedness made visible.</summary>
        static readonly Domains[] Lanes = { Domains.Jade, Domains.Blue, Domains.Gold };

        readonly struct BandSpec
        {
            public readonly float Radius, HalfWidth, TiltDeg, PhaseDeg;
            public readonly int HalfTwists;   // MUST be odd - that is what makes the band one-sided

            public BandSpec(float radius, float halfWidth, int halfTwists, float tiltDeg, float phaseDeg)
            {
                // Scaled HERE, at the single point the authored literals below become world
                // distances, so spec.Radius / spec.HalfWidth are already in world units for every
                // consumer and the table stays the readable numbers the bands were tuned as.
                Radius = radius * S; HalfWidth = halfWidth * S; HalfTwists = halfTwists;
                TiltDeg = tiltDeg; PhaseDeg = phaseDeg;
            }
        }

        /// <summary>
        /// Three bands, one per coordinate plane so they interlock rather than nest. Radii and
        /// half-widths are in AUTHORED units (see <see cref="S"/>); the constructor scales them.
        /// They are authored so the outermost reach (<c>Radius + HalfWidth + KeelDrop</c>) stays
        /// inside <see cref="SliceArenaGeometry.OuterRadiusI4"/> - asserted at build, because a band
        /// that quietly grew past the envelope would put mass outside the AI's stations and the
        /// spawn ring without failing anything.
        ///
        /// The half-twist counts climb 1/3/5 so the three ribbons feel different at the same
        /// speed: one half twist is a long lazy roll over a whole lap, five is nearly a corkscrew.
        /// </summary>
        static readonly BandSpec[] BandSpecs =
        {
            new BandSpec(200f, 74f, 1,   8f,   0f),
            new BandSpec(254f, 66f, 3, -13f,  47f),
            new BandSpec(308f, 40f, 5,  17f, 101f),
        };

        protected override int DefaultSeed => 394;

        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnableTwistbands), R, PlateStepAlong, Flush, PlateStepAcross,
            System.HashCode.Combine(KeelLength, KeelDrop, VoidThreshold, VoidFreq,
                                    CorniceStep, DangerEveryNthCornicePrism, CorniceWidth),
            BandHash());

        static int BandHash()
        {
            int h = 17;
            foreach (var b in BandSpecs)
                h = h * 31 + System.HashCode.Combine(b.Radius, b.HalfWidth, b.HalfTwists,
                                                     b.TiltDeg, b.PhaseDeg);
            return h;
        }

        protected override int LayCapacity => 20000;

        /// <summary>
        /// A band's working frame - <c>SpawnableOurobor</c>'s, unchanged. <c>E1</c>/<c>E2</c> span
        /// the loop plane and <c>E3</c> is its normal; the width direction rotates out of E1/E2
        /// into E3 as it goes round, which IS the Möbius twist.
        /// </summary>
        readonly struct Band
        {
            public readonly Vector3 E1, E2, E3;
            public readonly float Radius, HalfWidth, TwistRate, Phase;

            public Band(Vector3 e1, Vector3 e2, Vector3 e3, in BandSpec spec)
            {
                E1 = e1; E2 = e2; E3 = e3;
                Radius = spec.Radius; HalfWidth = spec.HalfWidth;
                TwistRate = spec.HalfTwists * 0.5f;
                Phase = spec.PhaseDeg * Mathf.Deg2Rad;
            }

            public Vector3 Radial(float u) => E1 * Mathf.Cos(u) + E2 * Mathf.Sin(u);
            public Vector3 Along(float u) => E2 * Mathf.Cos(u) - E1 * Mathf.Sin(u);

            public Vector3 Width(float u)
            {
                float h = Phase + TwistRate * u;
                return Radial(u) * Mathf.Cos(h) + E3 * Mathf.Sin(h);
            }

            public Vector3 Rise(float u)
            {
                float h = Phase + TwistRate * u;
                return Radial(u) * -Mathf.Sin(h) + E3 * Mathf.Cos(h);
            }

            public Vector3 At(float u, float v) => Radial(u) * Radius + Width(u) * v;

            /// <summary>Exact dP/du - the loop tangent stretched by the width term, plus the
            /// twist's own contribution. Using this rather than <see cref="Along"/> is what keeps
            /// a plate near an edge square to the surface it is actually on.</summary>
            public Vector3 AlongSurface(float u, float v)
            {
                float h = Phase + TwistRate * u;
                return Along(u) * (Radius + v * Mathf.Cos(h)) + Rise(u) * (v * TwistRate);
            }

            public Vector3 Normal(float u, float v) =>
                Vector3.Cross(AlongSurface(u, v), Width(u)).normalized;
        }

        Band[] _bands;

        /// <summary>A Cleave arena STATES its prism sizes: scaling the arena is a
        /// SIMILARITY, so the prisms grow with the spacing and a rib keeps reading as a
        /// continuous bar. The Twistbands sit inside the shared prefab's window at 2 x, so this changes nothing they
        /// lay today; it is on so all four rungs of one mode answer the question the same way.</summary>
        protected override bool AdmitsAuthoredPrismScale => true;

        protected override void BuildEnvironment()
        {
            BuildBands();

            for (int b = 0; b < BandSpecs.Length; b++)
            {
                BuildDeck(BandSpecs[b], _bands[b], b);
                BuildCornice(BandSpecs[b], _bands[b], b);
            }
        }

        void BuildBands()
        {
            Vector3[][] planes =
            {
                new[] { Vector3.right, Vector3.up, Vector3.forward },
                new[] { Vector3.up, Vector3.forward, Vector3.right },
                new[] { Vector3.forward, Vector3.right, Vector3.up },
            };

            _bands = new Band[BandSpecs.Length];
            for (int b = 0; b < BandSpecs.Length; b++)
            {
                ref readonly BandSpec spec = ref BandSpecs[b];

                // Authoring guards, not runtime fixes - both failures are invisible in-editor.
                float reach = spec.Radius + spec.HalfWidth + KeelDrop;
                if (reach > R)
                    CSDebug.LogError($"[Twistbands] Band {b} reaches r={reach:F0}, outside the " +
                                   $"shared arena envelope {R}. The AI's stations and the player " +
                                   "spawn ring are both derived from that number - narrow the " +
                                   "band or pull its radius in.");
                if (spec.HalfTwists % 2 == 0)
                    CSDebug.LogError($"[Twistbands] Band {b} has {spec.HalfTwists} half twists - an " +
                                   "EVEN count makes an ordinary two-sided annulus, and the whole " +
                                   "arena is built on the band having one side and one edge.");

                var tilt = Quaternion.AngleAxis(spec.TiltDeg, planes[b][0]);
                _bands[b] = new Band(tilt * planes[b][0], tilt * planes[b][1], tilt * planes[b][2], spec);
            }
        }

        /// <summary>
        /// The plated deck, laid FLUSH so the ribbon is a continuous surface a blade can be held
        /// against rather than a lattice it rattles through - and so no plate passes through
        /// another (Docs/ECOSYSTEM.md §49.10).
        ///
        /// Both fits are exact, because the band's geometry hands them over:
        ///   • ACROSS: a row is a straight line along <see cref="Band.Width"/>, and every plate in
        ///     it has that line as its own x axis (its normal and tangent are both square to it), so
        ///     however the twist rolls the plates about the line their side faces stay square to
        ///     it. Plates <c>2 HalfWidth / nv</c> apart are flush at exactly that width.
        ///   • ALONG: row <c>u</c> lies wholly in the half-plane through the loop axis at angle
        ///     <c>u</c> (Width never leaves span(Radial, E3)), so the half-plane at
        ///     <c>u +/- du/2</c> bisects it from each neighbouring row, all the way across. A plate's
        ///     length is the most that keeps it on its own side of both - longer on the outside of
        ///     the loop, shorter on the inside, and shorter where the twist tips it.
        /// The outermost lanes also stop at the cornice's inner face, and the one jitter draw each
        /// plate makes now goes to its THICKNESS, the one axis nothing touches.
        /// </summary>
        void BuildDeck(in BandSpec spec, in Band band, int bandIndex)
        {
            int nu = Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * band.Radius / PlateStepAlong));
            int nv = Mathf.Max(3, Mathf.RoundToInt(2f * spec.HalfWidth / PlateStepAcross));
            float du = 2f * Mathf.PI / nu;
            float laneStep = 2f * spec.HalfWidth / nv;
            // The deck's last usable width: inside the cornice's inner face, either edge.
            float deckEdge = spec.HalfWidth - 0.5f * CorniceWidth - EdgeMargin;

            for (int iu = 0; iu < nu; iu++)
            {
                float u = 2f * Mathf.PI * iu / nu;
                var front = band.Along(u + 0.5f * du);
                var back = -band.Along(u - 0.5f * du);
                var across = band.Width(u);

                for (int iv = 0; iv < nv; iv++)
                {
                    // Rows staggered against each other so the plate joints never line up into a
                    // seam running the length of the ribbon.
                    float v = Mathf.Lerp(-spec.HalfWidth, spec.HalfWidth, (iv + 0.5f) / nv)
                              + ((iu & 1) == 0 ? 0f : PlateStepAcross * 0.25f);

                    var pos = band.At(u, v);
                    if (N01(pos.x * VoidFreq, pos.y * VoidFreq, pos.z * VoidFreq, 4) < VoidThreshold)
                        continue;

                    var normal = band.Normal(u, v);
                    var dom = Lanes[(iv * Lanes.Length) / nv % Lanes.Length];

                    // Across: the plate fills its own slot between its row neighbours, cut back
                    // to the cornice's inner face at the two edges. Inside the deck that is the
                    // slot itself, centred where the plate was laid; an outermost plate keeps its
                    // inner face against its neighbour and stops at the cornice, so its centre
                    // shifts inward by half the cut (a staggered one used to sit with its centre
                    // UNDER the cornice) and the deck stays continuous to the rail.
                    float lo = Mathf.Max(v - 0.5f * laneStep, -deckEdge);
                    float hi = Mathf.Min(v + 0.5f * laneStep, deckEdge);
                    bool edge = lo > v - 0.5f * laneStep || hi < v + 0.5f * laneStep;
                    float pv = edge ? 0.5f * (lo + hi) : v;
                    float halfWidth = 0.5f * (hi - lo) * Flush;

                    var platePos = edge ? band.At(u, pv) : pos;
                    var tangent = band.AlongSurface(u, pv);
                    var plateNormal = edge ? band.Normal(u, pv) : normal;
                    var along = tangent.normalized;

                    float halfThick = 0.5f * PlateThickness * JitFactor();
                    float halfLength = Flush * Mathf.Min(
                        FitToPlane(platePos, across, halfWidth, plateNormal, halfThick, along, Vector3.zero, front),
                        FitToPlane(platePos, across, halfWidth, plateNormal, halfThick, along, Vector3.zero, back));

                    // Where this band passes THROUGH an earlier one, the plates at the crossing
                    // are cut down out of that band (YieldToEarlierBands).
                    var plate = new Vector3(2f * halfWidth, 2f * halfThick, 2f * halfLength);
                    if (bandIndex > 0)
                        YieldToEarlierBands(bandIndex, ref platePos, across, plateNormal, along, ref plate);
                    Emit(platePos, SpawnPoint.LookRotation(tangent, plateNormal), plate, dom);

                    // The keel - half density, turned crosswise, so the ribbon has body edge-on
                    // and a deep cut pays twice. It hangs under the plate's LAID point.
                    if (((iu + iv) & 1) != 0) continue;

                    var keelPos = pos - normal * KeelDrop;
                    var keel = Jit(new Vector3(3.2f * P, 3.2f * P, KeelLength));
                    if (bandIndex > 0)
                        YieldToEarlierBands(bandIndex, ref keelPos, Vector3.Cross(normal, across).normalized,
                                            normal, across, ref keel);
                    Emit(keelPos, SpawnPoint.LookRotation(across, normal), keel, Triad(bandIndex + iu));
                }
            }
        }

        /// <summary>
        /// The band's single boundary curve, and the arena's only traps.
        ///
        /// <c>u</c> runs 0..4π rather than 0..2π because there is only ONE edge: after a full lap
        /// the <c>+HalfWidth</c> side has become the <c>-HalfWidth</c> side (an odd half-twist
        /// count flips <see cref="Band.Width"/>'s sign), so a single sweep of twice the loop traces
        /// the whole rail and closes. Halving this range would draw half an edge and leave the
        /// other half bare, which is the shape of bug that looks like a content gap.
        ///
        /// A rail prism is as long as the planes that bisect it from its two neighbours allow
        /// (each the perpendicular bisector of the two centres), so the rail is flush however the
        /// twist bends it; its width faces the deck and is the fixed <see cref="CorniceWidth"/> the
        /// deck stops at, and its one jitter draw goes to its height.
        /// </summary>
        void BuildCornice(in BandSpec spec, in Band band, int bandIndex)
        {
            // Arc length of the edge, not of the centreline: the rail sits HalfWidth out, so it is
            // longer than 4πR by the width term, and sampling it at the centreline's rate would
            // leave visible gaps on the outside of every twist.
            float edgeRadius = band.Radius + spec.HalfWidth;
            int n = Mathf.Max(24, Mathf.RoundToInt(4f * Mathf.PI * edgeRadius / CorniceStep));
            float hw = spec.HalfWidth;

            for (int i = 0; i < n; i++)
            {
                float u = 4f * Mathf.PI * i / n;
                var pos = band.At(u, hw);
                var tangent = band.AlongSurface(u, hw);
                var normal = band.Normal(u, hw);
                var along = tangent.normalized;
                var across = band.Width(u);

                var next = band.At(4f * Mathf.PI * (i + 1) / n, hw);
                var prev = band.At(4f * Mathf.PI * (i - 1) / n, hw);

                bool danger = (bandIndex * 37 + i) % DangerEveryNthCornicePrism == 0;

                float halfWidth = 0.5f * CorniceWidth;
                float halfThick = 0.5f * CorniceWidth * JitFactor();
                float halfLength = Flush * Mathf.Min(
                    FitToPlane(pos, across, halfWidth, normal, halfThick, along,
                               0.5f * (pos + next), (next - pos).normalized),
                    FitToPlane(pos, across, halfWidth, normal, halfThick, along,
                               0.5f * (pos + prev), (prev - pos).normalized));

                var rail = new Vector3(2f * halfWidth, 2f * halfThick, 2f * halfLength);
                if (bandIndex > 0) YieldToEarlierBands(bandIndex, ref pos, across, normal, along, ref rail);
                Emit(pos, SpawnPoint.LookRotation(tangent, normal), rail, Domains.Ruby,
                    danger ? PrismKind.Danger : PrismKind.Plain);
            }
        }

        /// <summary>
        /// The largest half-length along <paramref name="z"/> that keeps a box - centred at
        /// <paramref name="c"/>, half-extents <paramref name="hx"/> / <paramref name="hy"/> along
        /// <paramref name="x"/> / <paramref name="y"/> - on the near side of the plane through
        /// <paramref name="at"/> with outward normal <paramref name="n"/>: the box's support along
        /// <c>n</c> may not pass the plane. Pure arithmetic on vectors the build already has.
        /// </summary>
        static float FitToPlane(Vector3 c, Vector3 x, float hx, Vector3 y, float hy, Vector3 z,
                                Vector3 at, Vector3 n)
        {
            float room = Vector3.Dot(n, at - c)
                       - hx * Mathf.Abs(Vector3.Dot(x, n)) - hy * Mathf.Abs(Vector3.Dot(y, n));
            return Mathf.Max(0f, room) / Mathf.Max(1e-4f, Mathf.Abs(Vector3.Dot(z, n)));
        }

        /// <summary>One jitter factor in [0.8, 1.2] from the shared stream - exactly the one draw
        /// <c>Jit</c> makes, so spending it on a single axis moves no other prism.</summary>
        float JitFactor() => Jit(Vector3.one).x;

        // ── Crossings ────────────────────────────────────────────────────────
        /// <summary>
        /// The three bands interlock, and where two of them cross their decks occupy the same
        /// place. The later band (higher index) yields: each of its prisms that reaches an earlier
        /// band is cut down, in its own plane, to the largest piece of itself that stays out of
        /// that band. A prism whose centre is clear keeps its centre and only shrinks about it;
        /// one whose centre lies INSIDE the other band (a placement no scale can fix) moves to the
        /// largest piece of its own box on one side of it (cutting thickness too, when that keeps
        /// more), and a thin bar lying almost flat in the other band moves the least distance
        /// along its normal that clears it - unless it lies in that band's keel layer between two
        /// keels (<see cref="TouchesKeel"/>), where it stays. Nothing is dropped and the earlier
        /// band is untouched, so it reads as one band passing through a slot in the other.
        ///
        /// The earlier band is taken LOCALLY. A point's own loop angle in that band's frame IS the
        /// row that holds it (every row lies in its half-plane through the loop axis), so the foot
        /// is exact: no search. That row's plate plane, and the keel <see cref="KeelDrop"/> under
        /// it, are the two slabs to keep out of, each widened by <see cref="CrossingMargin"/> for
        /// the twist a tangent plane does not see. A distance test against the band's own centre
        /// circle skips every prism nowhere near it, which is nearly all of them.
        /// </summary>
        void YieldToEarlierBands(int bandIndex, ref Vector3 c, Vector3 x, Vector3 y, Vector3 z,
                                 ref Vector3 size)
        {
            // The hot path: every prism of a later band asks, almost none is near. It stays a
            // few dot products, and the real fit lives in YieldToBand.
            for (int a = 0; a < bandIndex; a++)
            {
                ref readonly Band other = ref _bands[a];
                float reach = 0.5f * size.magnitude;
                float h = Vector3.Dot(c, other.E3);
                float px = Vector3.Dot(c, other.E1), py = Vector3.Dot(c, other.E2);
                float rho = Mathf.Sqrt(px * px + py * py) - other.Radius;
                float far = BandSpecs[a].HalfWidth + KeelDrop + CorniceWidth + reach;
                if (rho * rho + h * h > far * far) continue;
                YieldToBand(a, h, px, py, rho, ref c, x, y, z, ref size);
            }
        }

        /// <summary>Fit one prism out of band <paramref name="a"/>, which it is near (see
        /// <see cref="YieldToEarlierBands"/>): <paramref name="h"/>, <paramref name="px"/>,
        /// <paramref name="py"/> are its coordinates in that band's loop frame and
        /// <paramref name="rho"/> its distance from the band's centre circle in the loop plane.</summary>
        void YieldToBand(int a, float h, float px, float py, float rho, ref Vector3 c, Vector3 x, Vector3 y,
                         Vector3 z, ref Vector3 size)
        {
            ref readonly Band other = ref _bands[a];
            float otherHalfWidth = BandSpecs[a].HalfWidth;
            float hx = 0.5f * size.x, hy = 0.5f * size.y, hz = 0.5f * size.z;
            float reach = Mathf.Sqrt(hx * hx + hy * hy + hz * hz);
            // The row's parameter as the deck LAID it, u in [0, 2pi): the band is one-sided,
            // so the same row read at u - 2pi has its width and normal flipped, and the keel
            // would be on the wrong side. Within a row of the seam both rows are close, and
            // they disagree on which side is down, so both are kept out of.
            float du = 2f * Mathf.PI / Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * other.Radius / PlateStepAlong));
            float u0 = Mathf.Atan2(py, px);
            if (u0 < -0.5f * du) u0 += 2f * Mathf.PI;
            bool nearSeam = u0 < 0.5f * du || u0 > 2f * Mathf.PI - 1.5f * du;
            for (int branch = 0; branch < (nearSeam ? 2 : 1); branch++)
            {
                float u = branch == 0 ? u0 : u0 < Mathf.PI ? u0 + 2f * Mathf.PI : u0 - 2f * Mathf.PI;
                // In the row's half-plane the band is the line through (Radius, 0) along
                // (cos t, sin t), t the twist angle, so the prism's offsets ALONG the row (v)
                // and OFF it (rise) are two products - and its distance from the plate plane
                // is that rise times the plate normal's share of Rise, the Along and Rise parts
                // of the surface tangent (Radius + v cos t, v TwistRate) taken in proportion.
                // No trig past the twist angle.
                float twist = other.Phase + other.TwistRate * u;
                float ct = Mathf.Cos(twist), st = Mathf.Sin(twist);
                float dRho = rho, v = dRho * ct + h * st, rise = -dRho * st + h * ct;
                if (Mathf.Abs(v) > otherHalfWidth + 0.5f * CorniceWidth + reach) continue;
                float vOn = Mathf.Clamp(v, -otherHalfWidth, otherHalfWidth);
                float onAlong = other.Radius + vOn * ct, onRise = vOn * other.TwistRate;
                float d = -rise * onAlong / Mathf.Sqrt(onAlong * onAlong + onRise * onRise);

                // The other band fills two slabs along its normal: the deck (as tall as the
                // cornice near the edge), and the keel hung KeelDrop under it. Between is open.
                float deckTop = 0.6f * (Mathf.Abs(v) > otherHalfWidth - CorniceWidth - CorniceGuard
                                    ? CorniceWidth : PlateThickness) + CrossingMargin;
                float deckBottom = -deckTop;
                float keelTop = -KeelDrop + 0.6f * 3.2f * P + CrossingMargin;
                float keelBottom = -KeelDrop - 0.6f * 3.2f * P - CrossingMargin;
                reach = Mathf.Sqrt(hx * hx + hy * hy + hz * hz);
                if (d - reach >= deckTop || d + reach <= keelBottom ||
                    (d - reach >= keelTop && d + reach <= deckBottom)) continue;

                var n = other.Normal(u, vOn);
                float gx = Vector3.Dot(x, n), gz = Vector3.Dot(z, n);
                float ay = Mathf.Abs(Vector3.Dot(y, n));
                if (!FitOutOf(d, gx, gz, ay, deckTop, deckBottom, keelTop, keelBottom,
                              ref hx, ref hy, ref hz, out float cx, out float cz, out float lift))
                    continue;
                // A bar lying flat in the other band's KEEL layer may be lying between its
                // keels (they hang under every other plate): move it only if one is there.
                if (lift != 0f && d + (Mathf.Abs(gx) * hx + Mathf.Abs(gz) * hz + ay * hy) < deckBottom &&
                    !TouchesKeel(a, c, x, y, z, new Vector3(hx, hy, hz)))
                    continue;
                c += x * cx + z * cz + n * lift;
            }
            size = new Vector3(2f * hx, 2f * hy, 2f * hz);
        }

        /// <summary>
        /// Fit one prism - signed distance <paramref name="d"/> from another band's plate plane,
        /// gradient <paramref name="gx"/> / <paramref name="gz"/> along its own x / z, support
        /// <paramref name="ay"/> per unit of its half-thickness - out of that band's two slabs (the
        /// deck, and the keel under it), shrinking it as little as it can.
        ///
        /// When the prism's centre is clear it keeps it and shrinks about it, every axis giving up
        /// an equal share of support (the volume-optimal cut, <see cref="MaxBox"/>). When the centre
        /// is inside the other band - a placement no scale can fix - it keeps the largest piece of
        /// its own box that lies wholly above the deck, between deck and keel, or below the keel,
        /// and that piece's centre is where it moves to. It also takes the piece when staying put
        /// would keep under a quarter of what the piece does, since a centre a hair outside the
        /// other band would otherwise shrink to a sliver. Only when no piece of its box is clear (a
        /// bar lying flat in the other band) does it move off its own surface: the least distance
        /// along the other band's normal, out to whichever side is nearer.
        /// </summary>
        bool FitOutOf(float d, float gx, float gz, float ay, float deckTop, float deckBottom,
                      float keelTop, float keelBottom, ref float hx, ref float hy, ref float hz,
                      out float cx, out float cz, out float lift)
        {
            float ax = Mathf.Abs(gx), az = Mathf.Abs(gz);
            cx = cz = lift = 0f;

            float room = d > deckTop ? d - deckTop
                       : d < keelBottom ? keelBottom - d
                       : d > keelTop && d < deckBottom ? Mathf.Min(d - keelTop, deckBottom - d)
                       : 0f;
            float symVolume = 0f, sx = 0f, sy = 0f, sz = 0f;
            if (room > 0f)
            {
                MaxBox(ax, ay, az, room, hx, hy, hz, out sx, out sy, out sz);
                symVolume = 8f * sx * sy * sz;
            }

            // Four ways out, each a piece anchored on the corner of the box furthest that way:
            // above the deck, down off the deck into the open (not past the keel's top), up off
            // the keel into the open (not past the deck's underside), or below the keel.
            float best = 0f, bx = 0f, by = 0f, bz = 0f, bcx = 0f, bcz = 0f;
            for (int way = 0; way < 4; way++)
            {
                bool up = way == 0 || way == 2;
                float face = way == 0 ? deckTop : way == 1 ? deckBottom : way == 2 ? keelTop : keelBottom;
                float limit = way == 1 ? keelTop : way == 2 ? deckBottom : float.NaN;
                if (!PieceBeyond(d, gx, gz, ay, face, up, limit, hx, hy, hz, out float spanX,
                                 out float pieceHy, out float spanZ, out float pcx, out float pcz)) continue;
                float volume = spanX * spanZ * 2f * pieceHy;
                if (volume > best)
                {
                    best = volume; bx = spanX; by = pieceHy; bz = spanZ; bcx = pcx; bcz = pcz;
                }
            }

            if (symVolume > 0f && 4f * symVolume >= best)
            {
                hx = sx; hy = sy; hz = sz;
                return true;
            }
            if (best > 0f)
            {
                cx = bcx; cz = bcz;
                hx = 0.5f * bx; hy = by; hz = 0.5f * bz;
                return true;
            }

            float support = ax * hx + az * hz + ay * hy;
            float bestMove = float.PositiveInfinity, to = d;
            _crossTargets[0] = deckTop + support;
            _crossTargets[1] = keelBottom - support;
            _crossTargets[2] = 2f * support <= deckBottom - keelTop
                ? Mathf.Clamp(d, keelTop + support, deckBottom - support) : float.NaN;
            for (int i = 0; i < 3; i++)
                if (!float.IsNaN(_crossTargets[i]) && Mathf.Abs(_crossTargets[i] - d) < bestMove)
                {
                    bestMove = Mathf.Abs(_crossTargets[i] - d); to = _crossTargets[i];
                }
            lift = to - d;
            return true;
        }

        /// <summary>
        /// Whether a box reaches any keel band <paramref name="bandIndex"/> actually laid near it -
        /// the rows either side of the box's own loop angle, the lanes either side of its width
        /// coordinate, each keel at its largest jitter. Asked only for the rare bar that lies flat
        /// inside that band's keel layer and cannot be cut clear of it, so it can stay where it was
        /// laid when it sits in a gap between keels rather than being moved off its own surface.
        /// </summary>
        bool TouchesKeel(int bandIndex, Vector3 c, Vector3 x, Vector3 y, Vector3 z, Vector3 h)
        {
            ref readonly Band band = ref _bands[bandIndex];
            float hw = BandSpecs[bandIndex].HalfWidth;
            int nu = Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * band.Radius / PlateStepAlong));
            int nv = Mathf.Max(3, Mathf.RoundToInt(2f * hw / PlateStepAcross));
            float angle = Mathf.Atan2(Vector3.Dot(c, band.E2), Vector3.Dot(c, band.E1));
            int row = Mathf.RoundToInt(angle / (2f * Mathf.PI / nu));
            var keelHalf = new Vector3(0.6f * 3.2f * P, 0.6f * 3.2f * P, 0.6f * KeelLength);
            for (int dr = -1; dr <= 1; dr++)
            {
                int iu = ((row + dr) % nu + nu) % nu;
                float u = 2f * Mathf.PI * iu / nu;
                float stagger = (iu & 1) == 0 ? 0f : PlateStepAcross * 0.25f;
                float v = Vector3.Dot(c - band.Radial(u) * band.Radius, band.Width(u));
                int lane = Mathf.RoundToInt((v - stagger + hw) / (2f * hw / nv) - 0.5f);
                for (int iv = Mathf.Max(0, lane - 1); iv <= Mathf.Min(nv - 1, lane + 1); iv++)
                {
                    if (((iu + iv) & 1) != 0) continue;
                    float lv = Mathf.Lerp(-hw, hw, (iv + 0.5f) / nv) + stagger;
                    var plate = band.At(u, lv);
                    if (N01(plate.x * VoidFreq, plate.y * VoidFreq, plate.z * VoidFreq, 4) < VoidThreshold)
                        continue;
                    var kn = band.Normal(u, lv);
                    var kz = band.Width(u);
                    if (BoxesOverlap(c, x, y, z, h, plate - kn * KeelDrop,
                                     Vector3.Cross(kn, kz).normalized, kn, kz, keelHalf))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Two oriented boxes overlap: no separating axis among the 15 of the SAT.</summary>
        static bool BoxesOverlap(Vector3 ca, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 ha,
                                 Vector3 cb, Vector3 b0, Vector3 b1, Vector3 b2, Vector3 hb)
        {
            var t = cb - ca;
            for (int i = 0; i < 15; i++)
            {
                Vector3 L = i < 3 ? (i == 0 ? a0 : i == 1 ? a1 : a2)
                          : i < 6 ? (i == 3 ? b0 : i == 4 ? b1 : b2)
                          : Vector3.Cross(((i - 6) / 3) == 0 ? a0 : ((i - 6) / 3) == 1 ? a1 : a2,
                                          ((i - 6) % 3) == 0 ? b0 : ((i - 6) % 3) == 1 ? b1 : b2);
                if (L.sqrMagnitude < 1e-8f) continue;
                float ra = ha.x * Mathf.Abs(Vector3.Dot(a0, L)) + ha.y * Mathf.Abs(Vector3.Dot(a1, L))
                         + ha.z * Mathf.Abs(Vector3.Dot(a2, L));
                float rb = hb.x * Mathf.Abs(Vector3.Dot(b0, L)) + hb.y * Mathf.Abs(Vector3.Dot(b1, L))
                         + hb.z * Mathf.Abs(Vector3.Dot(b2, L));
                if (Mathf.Abs(Vector3.Dot(t, L)) >= ra + rb) return false;
            }
            return true;
        }

        /// <summary>Widening of an earlier band's slabs, for the twist and the row-to-row turn
        /// that a tangent plane at the foot does not see.</summary>
        const float CrossingMargin = 1f;

        /// <summary>How far in from the edge the other band's deck is treated as cornice-tall.</summary>
        const float CorniceGuard = 2f;

        readonly float[] _crossTargets = new float[3];

        /// <summary>
        /// The largest box a x b x c (each no larger than its cap) whose support
        /// <c>ga a + gb b + gc c</c> fits in <paramref name="room"/> - the closed form of "shrink a
        /// prism only as much as it has to": water-filling, every axis gives up an equal share of
        /// support unless it is already at its cap (or costs nothing, gradient ~ 0).
        /// </summary>
        static void MaxBox(float ga, float gb, float gc, float room, float maxA, float maxB, float maxC,
                           out float a, out float b, out float c)
        {
            const float Eps = 1e-4f;
            a = maxA; b = maxB; c = maxC;
            if (room <= 0f) { a = b = c = 0f; return; }
            // Cost of each axis at its cap; free axes (gradient ~ 0) stay at their caps.
            float ca = ga < Eps ? 0f : ga * maxA, cb = gb < Eps ? 0f : gb * maxB, cc = gc < Eps ? 0f : gc * maxC;
            if (ca + cb + cc <= room) return;
            int left = (ca > 0f ? 1 : 0) + (cb > 0f ? 1 : 0) + (cc > 0f ? 1 : 0);
            float rest = room;
            bool fa = ca == 0f, fb = cb == 0f, fc = cc == 0f;
            for (int pass = 0; pass < 3 && left > 0; pass++)
            {
                float share = rest / left;
                bool capped = false;
                if (!fa && ca <= share) { fa = true; rest -= ca; left--; capped = true; }
                if (!fb && cb <= share) { fb = true; rest -= cb; left--; capped = true; }
                if (!fc && cc <= share) { fc = true; rest -= cc; left--; capped = true; }
                if (capped) continue;
                if (!fa) a = share / ga;
                if (!fb) b = share / gb;
                if (!fc) c = share / gc;
                return;
            }
        }

        /// <summary>
        /// The largest piece of a box (half-extents hx, hy, hz; spans 2hx along x, 2hz along z)
        /// lying wholly beyond <paramref name="face"/> - above it when <paramref name="up"/>, else
        /// below - along a normal on which the box's centre sits at <paramref name="d"/>, its x / z
        /// run at <paramref name="gx"/> / <paramref name="gz"/> and its half-thickness costs
        /// <paramref name="ay"/> per unit. The piece is anchored on the box's corner furthest that
        /// way, and may not reach <paramref name="limit"/> on the far side (NaN: no limit). Returns
        /// its spans and the offset of its centre from the box's. False when no piece fits.
        /// </summary>
        static bool PieceBeyond(float d, float gx, float gz, float ay, float face, bool up, float limit,
                                float hx, float hy, float hz, out float spanX, out float pieceHy,
                                out float spanZ, out float cx, out float cz)
        {
            float ax = Mathf.Abs(gx), az = Mathf.Abs(gz), side = up ? 1f : -1f;
            spanX = spanZ = pieceHy = cx = cz = 0f;
            // How far the anchored corner (at zero thickness) clears the face.
            float corner = side * (d - face) + ax * hx + az * hz;
            MaxBox(ax, ay, az, corner, 2f * hx, hy, 2f * hz, out spanX, out pieceHy, out spanZ);
            if (spanX <= 0f || spanZ <= 0f || pieceHy <= 0f) return false;
            if (!float.IsNaN(limit) && side * (limit - (d + side * (ax * hx + az * hz))) < ay * pieceHy)
                return false;
            cx = side * (gx >= 0f ? 1f : -1f) * (hx - 0.5f * spanX);
            cz = side * (gz >= 0f ? 1f : -1f) * (hz - 0.5f * spanZ);
            return true;
        }

        static Domains Triad(int n)
        {
            int i = ((n % 3) + 3) % 3;
            return i == 0 ? Domains.Jade : i == 1 ? Domains.Ruby : Domains.Gold;
        }
    }
}
