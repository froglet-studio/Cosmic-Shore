using UnityEngine;
using CosmicShore.Data;

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
    ///   • <b>The deck is solid, not a lattice.</b> Plates overlap in both directions
    ///     (<see cref="PlateStepAlong"/> under <see cref="PlateLength"/>,
    ///     <see cref="PlateStepAcross"/> under <see cref="PlateWidth"/>). A sparse deck on a
    ///     narrow ribbon in a mostly empty ball would be almost impossible to stay in contact with;
    ///     a solid one means a blade that finds the surface keeps paying for as long as the pilot
    ///     can hold it there.
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

        // ── The deck ─────────────────────────────────────────────────────────
        const float PlateStepAlong = 7f * S;
        const float PlateLength = 10f * S;     // long axis, along the direction of travel
        const float PlateStepAcross = 7.5f * S * G;
        const float PlateWidth = 10f * S;      // cross axis, across the ribbon
        const float PlateThickness = 3f * S;

        /// <summary>How far below the deck the keel hangs, along the surface normal.</summary>
        const float KeelDrop = 11f * S;

        /// <summary>Light weathering only. The deck's whole job is to be continuous enough to hold
        /// a blade against, so this is a fraction of the panes' cull, not a match for it.</summary>
        const float VoidThreshold = 0.12f;
        const float VoidFreq = 0.02f / S;

        // ── The cornice (the single boundary curve) ──────────────────────────
        const float CorniceStep = 13f * S;
        const float CorniceLength = 15f * S;
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
        /// inside <see cref="SliceArenaGeometry.OuterRadius"/> - asserted at build, because a band
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
            nameof(SpawnableTwistbands), R, PlateStepAlong, PlateLength, PlateStepAcross,
            System.HashCode.Combine(PlateWidth, KeelDrop, VoidThreshold, VoidFreq,
                                    CorniceStep, DangerEveryNthCornicePrism),
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
                    Debug.LogError($"[Twistbands] Band {b} reaches r={reach:F0}, outside the " +
                                   $"shared arena envelope {R}. The AI's stations and the player " +
                                   "spawn ring are both derived from that number - narrow the " +
                                   "band or pull its radius in.");
                if (spec.HalfTwists % 2 == 0)
                    Debug.LogError($"[Twistbands] Band {b} has {spec.HalfTwists} half twists - an " +
                                   "EVEN count makes an ordinary two-sided annulus, and the whole " +
                                   "arena is built on the band having one side and one edge.");

                var tilt = Quaternion.AngleAxis(spec.TiltDeg, planes[b][0]);
                _bands[b] = new Band(tilt * planes[b][0], tilt * planes[b][1], tilt * planes[b][2], spec);
            }
        }

        /// <summary>
        /// The plated deck. Plates overlap along and across, so the ribbon is a continuous surface
        /// a blade can be held against rather than a lattice it rattles through.
        /// </summary>
        void BuildDeck(in BandSpec spec, in Band band, int bandIndex)
        {
            int nu = Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * band.Radius / PlateStepAlong));
            int nv = Mathf.Max(3, Mathf.RoundToInt(2f * spec.HalfWidth / PlateStepAcross));

            for (int iu = 0; iu < nu; iu++)
            {
                float u = 2f * Mathf.PI * iu / nu;

                for (int iv = 0; iv < nv; iv++)
                {
                    // Rows staggered against each other so the plate joints never line up into a
                    // seam running the length of the ribbon.
                    float v = Mathf.Lerp(-spec.HalfWidth, spec.HalfWidth, (iv + 0.5f) / nv)
                              + ((iu & 1) == 0 ? 0f : PlateStepAcross * 0.25f);

                    var pos = band.At(u, v);
                    if (N01(pos.x * VoidFreq, pos.y * VoidFreq, pos.z * VoidFreq, 4) < VoidThreshold)
                        continue;

                    var tangent = band.AlongSurface(u, v);
                    var normal = band.Normal(u, v);
                    var dom = Lanes[(iv * Lanes.Length) / nv % Lanes.Length];

                    Emit(pos, SpawnPoint.LookRotation(tangent, normal),
                        Jit(new Vector3(PlateWidth, PlateThickness, PlateLength)), dom);

                    // The keel - half density, turned crosswise, so the ribbon has body edge-on
                    // and a deep cut pays twice.
                    if (((iu + iv) & 1) != 0) continue;

                    Emit(pos - normal * KeelDrop, SpawnPoint.LookRotation(band.Width(u), normal),
                        Jit(new Vector3(3.2f * S, 3.2f * S, PlateLength)),
                        Triad(bandIndex + iu));
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
        /// </summary>
        void BuildCornice(in BandSpec spec, in Band band, int bandIndex)
        {
            // Arc length of the edge, not of the centreline: the rail sits HalfWidth out, so it is
            // longer than 4πR by the width term, and sampling it at the centreline's rate would
            // leave visible gaps on the outside of every twist.
            float edgeRadius = band.Radius + spec.HalfWidth;
            int n = Mathf.Max(24, Mathf.RoundToInt(4f * Mathf.PI * edgeRadius / CorniceStep));

            for (int i = 0; i < n; i++)
            {
                float u = 4f * Mathf.PI * i / n;
                var pos = band.At(u, spec.HalfWidth);
                var tangent = band.AlongSurface(u, spec.HalfWidth);
                var normal = band.Normal(u, spec.HalfWidth);

                bool danger = (bandIndex * 37 + i) % DangerEveryNthCornicePrism == 0;

                Emit(pos, SpawnPoint.LookRotation(tangent, normal),
                    Jit(new Vector3(4.4f * S, 4.4f * S, CorniceLength)), Domains.Ruby,
                    danger ? PrismKind.Danger : PrismKind.Plain);
            }
        }

        static Domains Triad(int n)
        {
            int i = ((n % 3) + 3) % 3;
            return i == 0 ? Domains.Jade : i == 1 ? Domains.Ruby : Domains.Gold;
        }
    }
}
