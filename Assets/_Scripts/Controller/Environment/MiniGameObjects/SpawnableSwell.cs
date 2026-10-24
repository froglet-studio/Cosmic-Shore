using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Swell" - Cleave's intensity-2 arena: five great WAVY RIBBONS, each a wide plated road
    /// meandering a closed circuit through the cell, threaded through one another at five different
    /// angles.
    ///
    /// The panes (intensity 1) are flat slabs at fixed angles and teach that a sword rewards
    /// committing to a LINE. The swell holds the same mass in the same ball and teaches the other
    /// half of that: <b>the line does not have to be straight.</b> A ribbon is one continuous
    /// surface that rolls left and right and rises and falls for its whole lap, so a pilot who
    /// finds one can hold the blade down and simply FOLLOW it - the longest uninterrupted cut in
    /// the mode, and the one that asks for flying rather than for aim.
    ///
    /// Four properties are load-bearing:
    ///
    ///   • <b>The road is WIDE, and the width is forgiveness.</b> Half-widths run
    ///     <see cref="RibbonSpecs"/> 19..25 authored units - 114 to 150 in world, which at the
    ///     shipped plate step lays 7 or 9 lanes and so covers 282 to 350 units of deck. That is
    ///     roughly ten hull-widths of road, and it is what stops a small steering error ending
    ///     the cut while the meander is what makes the cut interesting. It is the whole reason
    ///     this arena is ribbons rather than the corrugated sheets it replaced: sheets put the
    ///     mass where a blade CROSSES it, ribbons put the mass where a blade can STAY on it.
    ///   • <b>The deck is MANY SMALL PLATES.</b> A plate is ~34 x 34 x 5.2 world units on a road
    ///     whose spine may be 1,700 out, because a plate's size rides
    ///     <see cref="SliceArenaGeometry.PrismScaleI2"/> and the ribbon's radius rides
    ///     <see cref="S"/>. Cutting a hundred small things is the fun; cutting one enormous slab
    ///     reads as low poly, which is exactly what this deck did at 132-unit plates.
    ///   • <b>The deck is solid, not a lattice.</b> Plates are laid FLUSH in both directions -
    ///     exactly <see cref="PlateStepAcross"/> wide, and as long as the gap to the rows either
    ///     side allows (so shorter on the inside of a bend) - the Twistbands' rule, for the
    ///     Twistbands' reason: a sparse deck is something the sword rattles through rather than
    ///     something it cuts. They used to OVERLAP (a 44-unit plate on a 34-unit step) and every
    ///     plate passed through its neighbours; flush is the same continuous road with no prism
    ///     inside another (Docs/ECOSYSTEM.md §49.10).
    ///   • <b>The road is PAINTED as a road.</b> A crown lane in Gold down the middle, shoulders in
    ///     Blue, verges in Jade, so the ribbon reads as a carriageway from right across the arena
    ///     and a pilot can see where the middle of it is before they are close enough to see the
    ///     relief. Colour carries information here; it is not decoration. (Scoring does not care:
    ///     every non-roster prism is hostile to everyone, in any colour.)
    ///   • <b>Running wide on a bend bites.</b> The only <see cref="PrismKind.Danger"/> prisms in
    ///     this arena sit on the OUTER verge of the tighter corners
    ///     (<see cref="CurveBiteRatio"/>) - the racing line is safe and the mass you reach by
    ///     drifting off it on a turn is the mass that punishes you. It is a small trap budget on
    ///     purpose: one lane, on the outside only, and only where the ribbon is actually turning.
    ///
    /// <b>The deck never rolls about its own direction of travel.</b> The width direction is
    /// <c>cross(tangent, loop axis)</c>, so the surface banks with the climb and nothing else -
    /// deliberately, because holding a blade against a surface that rotates under it is the
    /// TWISTBANDS' lesson (intensity 4) and this rung is the one that should be a joy rather than a
    /// test. Every ribbon's climb is authored so the bank stays under ~22 degrees.
    ///
    /// <b>A ribbon's half-width is bounded by its own narrowest radius</b> (<see cref="Ribbon"/>'s
    /// build guard): a road wider than about 0.6 of the closest the spine comes to its axis folds
    /// through itself at the inside of a bend, which is invisible in a screenshot and unflyable in
    /// the arena.
    ///
    /// Stations are walked by ARC LENGTH, not by parameter - a meandering spine covers very
    /// different distance per radian at the apex of a bend than on a straight, so stepping <c>u</c>
    /// uniformly would bunch the deck up in the corners and stretch it on the straights. The count
    /// therefore stays a ratio of two lengths (spine length / plate step), which is what keeps
    /// prism counts invariant under <see cref="SliceArenaGeometry.LengthScaleI2"/>.
    ///
    /// Nothing is <see cref="PrismKind.Shielded"/> or <see cref="PrismKind.SuperShielded"/> - see
    /// CLEAVE.md for why (an AI can never energize its blade, so unbreakable mass is mass an
    /// all-AI domain cannot score against). Deterministic per seed like every cell environment.
    /// </summary>
    public class SpawnableSwell : CellEnvironmentSpawnableBase
    {
        /// <summary>This arena IS intensity 2, so it carries that rung's dials as constants.</summary>
        const float R = SliceArenaGeometry.OuterRadiusI2;

        /// <summary>Authored-units -> world-units; see <c>SliceArenaGeometry</c>. Every LENGTH here
        /// is multiplied by it and the noise frequency divided by it. A ribbon's radius, half-width
        /// and wave amplitudes are lengths too, and are scaled inside <see cref="RibbonSpec"/>'s
        /// constructor - the one place an authored number becomes a world distance - so the table
        /// keeps the readable numbers it was tuned with.</summary>
        const float S = SliceArenaGeometry.LengthScaleI2;

        /// <summary>Extra ACROSS-ribbon plate spacing. Authored at 1 for this rung, for the
        /// Twistbands' reason: the deck is a continuous plated ROAD a blade is held against, so
        /// opening its lanes up is not "sparser", it is a lattice the sword rattles through. Kept
        /// as a named dial so the table reads the same on all four rungs.</summary>
        const float G = SliceArenaGeometry.GapScaleI2;

        /// <summary>Authored-units -> world-units for a PLATE'S OWN DIMENSIONS and for BOTH deck
        /// steps. A plated road is continuous in both directions, so the plate and the two steps it
        /// tiles at move together — while the ribbon's <c>Radius</c>, <c>HalfWidth</c> and wave
        /// amplitudes stay on <see cref="S"/>. A bigger place, paved with the same small plates:
        /// at <see cref="S"/> the deck was 132-unit slabs and read as low poly, and the lane count
        /// rises by <c>S / P</c> so the road is the same road made of three times as many pieces.
        /// See <c>SliceArenaGeometry</c>'s summary.</summary>
        const float P = SliceArenaGeometry.PrismScaleI2;

        // -- The deck --------------------------------------------------------
        /// <summary>Spacing ALONG the spine, measured in ARC LENGTH (see the class summary).</summary>
        const float PlateStepAlong = 17f * P;
        /// <summary>Spacing ACROSS the road, lane to lane. A plate's width is exactly this (less
        /// <see cref="Flush"/>), and its length is fitted per plate to the rows either side - see
        /// <see cref="BuildRoad"/>. Both used to be a constant 22 x P, 5 x P longer than the step,
        /// which made every plate interpenetrate all eight of its neighbours.</summary>
        const float PlateStepAcross = 17f * P * G;

        /// <summary>How close to exact contact a FITTED face is laid: a hair under, so float noise
        /// in the fit never turns a touching pair into an interpenetrating one (NO PRISM MAY
        /// INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md §49.10).</summary>
        const float Flush = 0.995f;
        const float PlateThickness = 2.6f * P;

        /// <summary>The keel: half-density plates this far below the deck along its own normal,
        /// laid crosswise, so the ribbon has body edge-on and a deep cut pays twice.</summary>
        const float KeelDrop = 12f * P;
        const float KeelPlate = 20f * P;
        const float KeelThickness = 2.4f * P;

        // -- Weathering ------------------------------------------------------
        /// <summary>Light only. The deck's whole job is to be continuous enough to hold a blade
        /// against, so this is a fraction of the panes' cull rather than a match for it.</summary>
        const float VoidThreshold = 0.10f;
        const float VoidFreq = 0.011f / S;

        // -- Traps -----------------------------------------------------------
        /// <summary>Fractions of a road's half-width at which the crown becomes shoulder and the
        /// shoulder becomes verge. Authored as fractions so all five ribbons wear the same road
        /// whatever their width and however many lanes that works out to.</summary>
        const float CrownFraction = 0.34f;
        const float ShoulderFraction = 0.70f;

        /// <summary>Dimensionless bend tightness (curvature x the ribbon's own radius, so it is
        /// invariant under <see cref="S"/>) above which the outer verge bites. A perfect circle
        /// sits at exactly 1, so this picks out the corners the meander actually tightens.</summary>
        const float CurveBiteRatio = 1.35f;

        /// <summary>Arc-length samples used to measure a spine before walking it. High enough that
        /// the measured length is exact to well under a plate, cheap because it runs five times per
        /// build.</summary>
        const int ArcSamples = 2048;

        /// <summary>A road wider than this fraction of its spine's narrowest radius folds through
        /// itself at the inside of a bend. Authoring guard only - see the class summary.</summary>
        const float MaxWidthOfNarrowestRadius = 0.6f;

        /// <summary>
        /// One ribbon. <c>TiltDeg</c>/<c>AzimuthDeg</c> aim the loop's axis exactly as a pane's
        /// normal is aimed. <c>Sway</c> is the primary meander - an in-plane wobble of the spine's
        /// radius, which is what makes the road weave left and right and is where every corner
        /// comes from. <c>Rise</c> is a weaker out-of-plane wave that lifts and drops the road;
        /// it is deliberately the SECONDARY term, because out-of-plane climb is what banks the
        /// deck and this rung is not the one that asks a pilot to track a roll.
        /// </summary>
        readonly struct RibbonSpec
        {
            public readonly float TiltDeg, AzimuthDeg;
            public readonly float Radius, HalfWidth;
            public readonly float SwayAmp, SwayPhaseDeg, RiseAmp, RisePhaseDeg;
            public readonly int SwayHarmonic, RiseHarmonic;

            public RibbonSpec(float tilt, float azimuth, float radius, float halfWidth,
                              float swayAmp, int swayHarmonic, float swayPhase,
                              float riseAmp, int riseHarmonic, float risePhase)
            {
                TiltDeg = tilt; AzimuthDeg = azimuth;
                // Scaled HERE, at the single point the authored literals below become world
                // distances, so every consumer - the deck, the keel and the envelope guard alike -
                // gets the scaled value without each having to remember to ask.
                Radius = radius * S; HalfWidth = halfWidth * S;
                SwayAmp = swayAmp * S; RiseAmp = riseAmp * S;
                SwayHarmonic = swayHarmonic; RiseHarmonic = riseHarmonic;
                SwayPhaseDeg = swayPhase; RisePhaseDeg = risePhase;
            }

            /// <summary>The closest the spine comes to its own loop axis.</summary>
            public float NarrowestRadius => Radius - SwayAmp;

            /// <summary>Everything this ribbon can reach from the cell centre, keel included. The
            /// plate's own half-extent is added by the caller.</summary>
            public float Reach =>
                Mathf.Sqrt((Radius + SwayAmp) * (Radius + SwayAmp) + RiseAmp * RiseAmp)
                + HalfWidth + KeelDrop;
        }

        /// <summary>
        /// Five ribbons, authored rather than generated for the same reason the panes are: where
        /// the arena is thick, and which approach finds a road broadside rather than end-on, is a
        /// gameplay surface. Radii climb 130..288 (authored), i.e. 780 to 1,728 in world with the
        /// outermost road reaching ~2,000 - <b>the roads are spread across the whole shell rather
        /// than nested near the middle</b>, which is the arena's answer to "the mass should be all
        /// over the cell". The harmonics are all different so no two of them wave in step: what a
        /// pilot is reading, once they are on one, is which road they are on.
        ///
        /// Half-widths are a LANE BUDGET, not a fraction of the radius. A road is 7 or 9 plates
        /// across at every radius, so an outer road is the same width as an inner one and simply
        /// runs further - which is what keeps the deck's prism count proportional to how much
        /// PLACE the ribbon covers. Two guards bound them anyway and both are checked at build:
        /// the envelope (<see cref="RibbonSpec.Reach"/> against
        /// <see cref="SliceArenaGeometry.OuterRadiusI2"/>) and the fold-through ratio, which every
        /// shipped row clears by 3x or more.
        ///
        /// <b>Every ribbon's climb is authored under the bank ceiling</b> - <c>rise x harmonic /
        /// radius</c>, the deck's worst bank angle in radians, stays at or under 0.4 (~22 degrees)
        /// on all five. It is the harmonic that bites rather than the amplitude: ribbon 2 lifts
        /// only 34 units but does it four times a lap on the sway, so its rise runs at harmonic 2.
        /// </summary>
        static readonly RibbonSpec[] RibbonSpecs =
        {
            //               tilt  azim  radius halfW  sway  k  phase  rise  k  phase
            new RibbonSpec(    0f,   0f,  130f,   24f,   30f, 2,    0f,  28f, 1,    0f),
            new RibbonSpec(   38f,  72f,  180f,   25f,   36f, 4,   55f,  34f, 2,  120f),
            new RibbonSpec(   66f, 151f,  225f,   21f,   32f, 3,  140f,  34f, 2,  200f),
            new RibbonSpec(   49f, 228f,  262f,   20f,   26f, 2,   25f,  28f, 1,   60f),
            new RibbonSpec(   81f, 305f,  288f,   19f,   20f, 4,   95f,  22f, 2,  280f),
        };

        protected override int DefaultSeed => 392;

        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnableSwell), R, PlateStepAlong, Flush, PlateStepAcross,
            System.HashCode.Combine(PlateThickness, KeelDrop, KeelPlate,
                                    VoidThreshold, VoidFreq, CurveBiteRatio, CrownFraction),
            RibbonHash());

        static int RibbonHash()
        {
            int h = 17;
            foreach (var s in RibbonSpecs)
                h = h * 31 + System.HashCode.Combine(
                    System.HashCode.Combine(s.TiltDeg, s.AzimuthDeg, s.Radius, s.HalfWidth),
                    s.SwayAmp, s.SwayHarmonic, s.SwayPhaseDeg,
                    System.HashCode.Combine(s.RiseAmp, s.RiseHarmonic, s.RisePhaseDeg));
            return h;
        }

        protected override int LayCapacity => 17000;

        /// <summary>
        /// A ribbon's resolved frame. <c>E1</c>/<c>E2</c> span the loop plane and <c>E3</c> is its
        /// axis. The spine is a wavy ring - radius modulated in-plane by the sway, lifted out of
        /// plane by the rise - and every derivative below is the EXACT partial rather than a finite
        /// difference, so the plate orientations are square to the road at any step size.
        /// </summary>
        readonly struct Ribbon
        {
            public readonly Vector3 E1, E2, E3;
            public readonly float Radius, HalfWidth, SwayAmp, RiseAmp, SwayPhase, RisePhase;
            public readonly int SwayK, RiseK;

            public Ribbon(Vector3 e1, Vector3 e2, Vector3 e3, in RibbonSpec spec)
            {
                E1 = e1; E2 = e2; E3 = e3;
                Radius = spec.Radius; HalfWidth = spec.HalfWidth;
                SwayAmp = spec.SwayAmp; RiseAmp = spec.RiseAmp;
                SwayK = spec.SwayHarmonic; RiseK = spec.RiseHarmonic;
                SwayPhase = spec.SwayPhaseDeg * Mathf.Deg2Rad;
                RisePhase = spec.RisePhaseDeg * Mathf.Deg2Rad;
            }

            Vector3 Radial(float u) => E1 * Mathf.Cos(u) + E2 * Mathf.Sin(u);
            Vector3 Tangential(float u) => E2 * Mathf.Cos(u) - E1 * Mathf.Sin(u);

            float Rho(float u) => Radius + SwayAmp * Mathf.Cos(SwayK * u + SwayPhase);
            float RhoPrime(float u) => -SwayAmp * SwayK * Mathf.Sin(SwayK * u + SwayPhase);
            float Lift(float u) => RiseAmp * Mathf.Sin(RiseK * u + RisePhase);
            float LiftPrime(float u) => RiseAmp * RiseK * Mathf.Cos(RiseK * u + RisePhase);

            public Vector3 Spine(float u) => Radial(u) * Rho(u) + E3 * Lift(u);

            /// <summary>Exact dP/du. Its MAGNITUDE is how much arc length a radian buys here,
            /// which is what the station walk divides by.</summary>
            public Vector3 SpineTangent(float u) =>
                Radial(u) * RhoPrime(u) + Tangential(u) * Rho(u) + E3 * LiftPrime(u);

            /// <summary>The road's across direction. Taking it from the LOOP AXIS rather than from
            /// a transported frame is what stops the deck rolling about its own travel: the
            /// surface banks exactly as much as the spine climbs, and never more.</summary>
            public Vector3 Width(float u) =>
                Vector3.Cross(SpineTangent(u).normalized, E3).normalized;

            public Vector3 Normal(float u)
            {
                var w = Width(u);
                return Vector3.Cross(w, SpineTangent(u).normalized).normalized;
            }

            public Vector3 At(float u, float v) => Spine(u) + Width(u) * v;
        }

        Ribbon[] _ribbons;
        float[] _stationU;
        Vector3[] _stationSpine, _stationAlong;

        /// <summary>A Cleave arena STATES its prism sizes, so its lay goes through
        /// <c>Prism.AdmitTargetScale</c> rather than trusting <c>PrismScaleAnimator</c>'s
        /// serialized window. It used to be REQUIRED here: at 132-unit plates this deck was over
        /// the shared prefab's <c>maxScale</c> of 100, which clamps PER AXIS inside the setter
        /// with no log and no return value, so the road silently rendered as 100-unit tiles.
        /// <b>Every size this arena states is now well inside that window</b> (~34 long, 5.2
        /// thick), so the flag is a standing GUARD rather than a fix - and the clamp having fired
        /// at all is worth keeping in mind: <i>a shared prefab's scale ceiling was the only thing
        /// in the project telling us the prisms had grown absurd.</i></summary>
        protected override bool AdmitsAuthoredPrismScale => true;

        protected override void BuildEnvironment()
        {
            BuildRibbons();

            for (int i = 0; i < RibbonSpecs.Length; i++)
                BuildRoad(RibbonSpecs[i], _ribbons[i], i);
        }

        void BuildRibbons()
        {
            _ribbons = new Ribbon[RibbonSpecs.Length];

            for (int i = 0; i < RibbonSpecs.Length; i++)
            {
                ref readonly RibbonSpec spec = ref RibbonSpecs[i];

                // Authoring guards, not runtime fixes - both failures are invisible in-editor.
                if (spec.Reach > R)
                    CSDebug.LogError($"[Swell] Ribbon {i} reaches r={spec.Reach:F0}, outside the " +
                                   $"arena envelope {R}. The AI's stations and the player spawn " +
                                   "ring are both derived from that number - narrow the road or " +
                                   "pull its radius in.");
                if (spec.HalfWidth > spec.NarrowestRadius * MaxWidthOfNarrowestRadius)
                    CSDebug.LogError($"[Swell] Ribbon {i} is {spec.HalfWidth:F0} half-wide against a " +
                                   $"narrowest spine radius of {spec.NarrowestRadius:F0} - the " +
                                   "inner verge folds through itself at the inside of a bend.");

                var orientation = Quaternion.AngleAxis(spec.AzimuthDeg, Vector3.up)
                                * Quaternion.AngleAxis(spec.TiltDeg, Vector3.forward);
                var axis = (orientation * Vector3.up).normalized;

                var seedDir = Mathf.Abs(Vector3.Dot(axis, Vector3.forward)) > 0.95f
                    ? Vector3.right : Vector3.forward;
                var e1 = Vector3.Cross(axis, seedDir).normalized;
                var e2 = Vector3.Cross(axis, e1);

                _ribbons[i] = new Ribbon(e1, e2, axis, spec);
            }
        }

        /// <summary>
        /// One ribbon's plated road, walked by ARC LENGTH so the deck is evenly dense whatever the
        /// spine is doing. The station count is measured length / plate step and therefore closes
        /// the loop exactly: the walk takes <c>stations</c> equal steps of <c>ds</c>, which is the
        /// measured length divided by that same count.
        /// </summary>
        void BuildRoad(in RibbonSpec spec, in Ribbon ribbon, int ribbonIndex)
        {
            float length = ArcLength(ribbon);
            int stations = Mathf.Max(24, Mathf.RoundToInt(length / PlateStepAlong));
            float ds = length / stations;

            int lanes = Mathf.Max(1, Mathf.FloorToInt(spec.HalfWidth / PlateStepAcross));

            // The walk, taken up front so every row can see BOTH of its neighbours: a plate's
            // length is fitted to the planes that bisect its row from the rows either side, and
            // the last row's front neighbour is the first. Advance is deterministic and draws
            // nothing, so walking it once more moves no prism.
            if (_stationU == null || _stationU.Length < stations)
            {
                _stationU = new float[stations]; _stationSpine = new Vector3[stations]; _stationAlong = new Vector3[stations];
            }
            var us = _stationU;
            float u = 0f;
            for (int station = 0; station < stations; station++)
            {
                us[station] = u;
                _stationSpine[station] = ribbon.Spine(u);
                _stationAlong[station] = ribbon.SpineTangent(u).normalized;
                u = Advance(ribbon, u, ds);
            }

            // A row is RIGID - every plate in it shares one rotation and they sit exactly
            // PlateStepAcross apart along its width axis - so neighbouring plates' side faces are
            // parallel and the deck is flush across at exactly that width.
            float halfWidth = 0.5f * PlateStepAcross * Flush;

            for (int station = 0; station < stations; station++)
            {
                u = us[station];
                var tangent = ribbon.SpineTangent(u);
                var across = ribbon.Width(u);
                var normal = ribbon.Normal(u);
                var rotation = SpawnPoint.LookRotation(tangent, normal);
                var keelRotation = SpawnPoint.LookRotation(across, normal);

                // The planes this row's plates may not cross: each bisects the row from a
                // neighbouring one (through the midpoint of the two spine points, square to the
                // mean of the two tangents). The neighbouring row is held to the same plane from
                // the other side, so the two touch and cannot pass through each other. On a bend
                // the plane closes in on the INNER verge, which is exactly where a constant plate
                // length used to clip.
                var along = _stationAlong[station];
                var spine = _stationSpine[station];
                int next = (station + 1) % stations, prev = (station + stations - 1) % stations;
                var frontAt = 0.5f * (spine + _stationSpine[next]);
                var front = (along + _stationAlong[next]).normalized;
                var backAt = 0.5f * (spine + _stationSpine[prev]);
                var back = -(along + _stationAlong[prev]).normalized;

                // Which way this station is turning, and how hard. Curvature x the ribbon's own
                // radius is dimensionless, so the trap threshold is a shape fact rather than a
                // length and survives the arena's length scale untouched.
                BendAt(ribbon, u, across, out float bendRatio, out int outerSide);
                bool biting = bendRatio > CurveBiteRatio;

                // Rows staggered against each other so the plate joints never line up into a seam
                // running the length of the road.
                float stagger = (station & 1) == 0 ? 0f : PlateStepAcross * 0.25f;

                for (int lane = -lanes; lane <= lanes; lane++)
                {
                    float v = lane * PlateStepAcross + stagger;
                    var pos = ribbon.At(u, v);

                    if (N01(pos.x * VoidFreq, pos.y * VoidFreq, pos.z * VoidFreq, 3) < VoidThreshold)
                        continue;

                    // Painted as a FRACTION of the road's own width rather than by lane index:
                    // lane counts run 7..9 across the five ribbons, and an integer rule at that
                    // resolution loses a whole band on the narrow ones. It also means the crown
                    // stays the same FRACTION of the road however many plates that works out to.
                    float t = Mathf.Abs(v) / spec.HalfWidth;
                    var dom = t < CrownFraction ? Domains.Gold           // the crown
                            : t < ShoulderFraction ? Domains.Blue        // the shoulders
                            : Domains.Jade;                              // the verge

                    // Run wide on a bend and you clip it. One lane, outside only, and only where
                    // the road is genuinely turning - so the racing line is always clean.
                    bool danger = biting && lane == outerSide * lanes;

                    // The plate's one jitter draw, as before, now spent on its THICKNESS alone:
                    // its length and width are fitted to its neighbours and a jitter that can grow
                    // a fitted size is a size that clips.
                    float halfThick = 0.5f * PlateThickness * JitFactor();
                    float halfLength = Flush * Mathf.Min(
                        FitToPlane(pos, across, halfWidth, normal, halfThick, along, frontAt, front),
                        FitToPlane(pos, across, halfWidth, normal, halfThick, along, backAt, back));

                    // Where this road passes THROUGH an earlier one, the plates at the crossing
                    // are cut down out of that road (YieldToEarlierRibbons).
                    var platePos = pos;
                    var plate = new Vector3(2f * halfWidth, 2f * halfThick, 2f * halfLength);
                    if (ribbonIndex > 0)
                        YieldToEarlierRibbons(ribbonIndex, ref platePos, across, normal, along, ref plate);
                    Emit(platePos, rotation, plate, dom, danger ? PrismKind.Danger : PrismKind.Plain);

                    if (((station + lane) & 1) != 0) continue;

                    var keelPos = pos - normal * KeelDrop;
                    var keel = Jit(new Vector3(KeelThickness, KeelThickness, KeelPlate));
                    if (ribbonIndex > 0)
                        YieldToEarlierRibbons(ribbonIndex, ref keelPos, along, normal, across, ref keel);
                    Emit(keelPos, keelRotation, keel, Domains.Ruby);
                }
            }
        }

        /// <summary>Total arc length of a spine, by midpoint rule over <see cref="ArcSamples"/>
        /// samples. Measured rather than approximated by <c>2 pi r</c> because the sway can add a
        /// third of the length back and the station count has to close the loop.</summary>
        static float ArcLength(in Ribbon ribbon)
        {
            float du = 2f * Mathf.PI / ArcSamples;
            float total = 0f;
            for (int i = 0; i < ArcSamples; i++)
                total += ribbon.SpineTangent((i + 0.5f) * du).magnitude * du;
            return total;
        }

        /// <summary>Advance <c>u</c> by <paramref name="ds"/> of ARC LENGTH. Four substeps rather
        /// than one because <c>|dP/du|</c> varies within a single plate on the tighter ribbons, and
        /// a one-shot division there walks visibly short through a corner.</summary>
        static float Advance(in Ribbon ribbon, float u, float ds)
        {
            const int Substeps = 4;
            float step = ds / Substeps;
            for (int i = 0; i < Substeps; i++)
                u += step / Mathf.Max(1e-4f, ribbon.SpineTangent(u).magnitude);
            return u;
        }

        /// <summary>
        /// The largest half-length along <paramref name="z"/> that keeps a box - centred at
        /// <paramref name="c"/>, half-extents <paramref name="hx"/> / <paramref name="hy"/> along
        /// <paramref name="x"/> / <paramref name="y"/> - on the near side of the plane through
        /// <paramref name="at"/> with outward normal <paramref name="n"/>: the box's support along
        /// <c>n</c> may not pass the plane. Pure arithmetic on vectors the walk already has.
        /// </summary>
        static float FitToPlane(Vector3 c, Vector3 x, float hx, Vector3 y, float hy, Vector3 z,
                                Vector3 at, Vector3 n)
        {
            float room = Vector3.Dot(n, at - c)
                       - hx * Mathf.Abs(Vector3.Dot(x, n)) - hy * Mathf.Abs(Vector3.Dot(y, n));
            return Mathf.Max(0f, room) / Mathf.Max(1e-4f, Mathf.Abs(Vector3.Dot(z, n)));
        }

        // -- Crossings --------------------------------------------------------
        /// <summary>
        /// The ribbons are threaded THROUGH one another, so where two roads cross, two decks
        /// occupy the same place. The later ribbon (higher index) yields: each of its prisms that
        /// reaches an earlier ribbon is cut down, in its own plane, to the largest piece of itself
        /// that stays out of that road. A prism whose centre is clear keeps its centre and only
        /// shrinks about it; only one whose centre lies INSIDE the other road (a placement no
        /// scale can fix) moves - to the largest piece of its own footprint on one side of it.
        /// Nothing is dropped and the earlier ribbon is untouched, so it reads as one road passing
        /// through a cut in the other.
        ///
        /// The earlier road is taken LOCALLY: the row of that ribbon whose plane holds the prism
        /// (two Newton steps on the spine from the prism's loop angle), and the two slabs the road
        /// fills there - its deck, and its keel <see cref="KeelDrop"/> below, with open space
        /// between - each widened by <see cref="CrossingMargin"/> for the curvature a tangent plane
        /// ignores. A prism's piece may sit above the deck, in the open between, or below the keel.
        /// Only a thin bar lying almost IN the other road, with no clear piece at all, moves off
        /// its own surface - the least distance along that road's normal. A test in the earlier
        /// ribbon's own loop frame skips every prism nowhere near it, which is nearly all of them,
        /// so this is a few dot products per prism.
        /// </summary>
        void YieldToEarlierRibbons(int ribbonIndex, ref Vector3 c, Vector3 x, Vector3 y, Vector3 z,
                                   ref Vector3 size)
        {
            // The hot path: every prism of a later ribbon asks, almost none is near. It stays a
            // few dot products, and the real fit lives in YieldToRibbon.
            for (int a = 0; a < ribbonIndex; a++)
            {
                ref readonly Ribbon other = ref _ribbons[a];
                float reach = 0.5f * size.magnitude;

                // In the other ribbon's loop frame its deck centres sit at height Lift(u) (the
                // width axis never leaves the loop plane) and nothing of it is more than a keel
                // and a plate from there.
                float h = Vector3.Dot(c, other.E3);
                if (Mathf.Abs(h) > other.RiseAmp + KeelDrop + 30f + reach) continue;
                float px = Vector3.Dot(c, other.E1), py = Vector3.Dot(c, other.E2);
                float rho = Mathf.Sqrt(px * px + py * py);
                float vMax = Mathf.FloorToInt(other.HalfWidth / PlateStepAcross) * PlateStepAcross
                           + 0.25f * PlateStepAcross + 0.6f * KeelPlate;
                if (Mathf.Abs(rho - other.Radius) > other.SwayAmp + vMax + 30f + reach) continue;
                YieldToRibbon(a, px, py, vMax, ref c, x, y, z, ref size);
            }
        }

        /// <summary>Fit one prism out of ribbon <paramref name="a"/>, which it is near (see
        /// <see cref="YieldToEarlierRibbons"/>): <paramref name="px"/> / <paramref name="py"/> are
        /// its coordinates in that ribbon's loop plane, <paramref name="vMax"/> how far across the
        /// road the ribbon's prisms reach.</summary>
        void YieldToRibbon(int a, float px, float py, float vMax, ref Vector3 c, Vector3 x, Vector3 y,
                           Vector3 z, ref Vector3 size)
        {
            ref readonly Ribbon other = ref _ribbons[a];
            float hx = 0.5f * size.x, hy = 0.5f * size.y, hz = 0.5f * size.z;
            float reach = Mathf.Sqrt(hx * hx + hy * hy + hz * hz);
            float u = Mathf.Atan2(py, px);
            for (int it = 0; it < 2; it++)
            {
                var t = other.SpineTangent(u);
                u += Vector3.Dot(c - other.Spine(u), t) / Mathf.Max(1e-4f, t.sqrMagnitude);
            }
            var foot = other.Spine(u);
            if (Mathf.Abs(Vector3.Dot(c - foot, other.Width(u))) > vMax + reach) return;
            var n = other.Normal(u);
            float d = Vector3.Dot(c - foot, n);
            // The other road fills two slabs along its normal: the deck, and the keel hung
            // KeelDrop under it. Between them is open.
            float deckTop = 0.6f * PlateThickness + CrossingMargin;
            float deckBottom = -deckTop;
            float keelTop = -KeelDrop + 0.6f * KeelThickness + CrossingMargin;
            float keelBottom = -KeelDrop - 0.6f * KeelThickness - CrossingMargin;
            if (d - reach >= deckTop || d + reach <= keelBottom ||
                (d - reach >= keelTop && d + reach <= deckBottom)) return;

            float gx = Vector3.Dot(x, n), gz = Vector3.Dot(z, n);
            float ax = Mathf.Abs(gx), az = Mathf.Abs(gz);
            float thick = hy * Mathf.Abs(Vector3.Dot(y, n));

            // Centre clear of both slabs: shrink about it until its support along n stops at
            // the faces either side of it.
            float room = d > deckTop ? d - deckTop
                       : d < keelBottom ? keelBottom - d
                       : d > keelTop && d < deckBottom ? Mathf.Min(d - keelTop, deckBottom - d)
                       : 0f;
            room -= thick;
            float symX = 0f, symZ = 0f;
            if (room > 0f) MaxRect(ax, az, room, hx, hz, out symX, out symZ);

            // The largest piece of its own footprint that lies wholly above the deck, wholly
            // in the open between deck and keel, or wholly below the keel.
            float bestArea = 0f, bestX = 0f, bestZ = 0f, bestCx = 0f, bestCz = 0f;
            for (int region = 0; region < 3; region++)
            {
                float lo = region == 0 ? deckTop : region == 1 ? keelTop : float.NegativeInfinity;
                float hi = region == 0 ? float.PositiveInfinity : region == 1 ? deckBottom : keelBottom;
                if (!PieceIn(d, gx, gz, thick, lo, hi, hx, hz, out float spanX, out float spanZ,
                             out float cx, out float cz)) continue;
                if (spanX * spanZ > bestArea)
                {
                    bestArea = spanX * spanZ; bestX = spanX; bestZ = spanZ; bestCx = cx; bestCz = cz;
                }
            }

            // Stay put and shrink when that keeps a fair share of what moving would; a centre
            // a hair outside the other road would otherwise shrink to a sliver.
            if (symX * symZ > 0f && 4f * (4f * symX * symZ) >= bestArea)
            {
                size = new Vector3(2f * symX, size.y, 2f * symZ);
                return;
            }
            if (bestArea > 0f)
            {
                c += x * bestCx + z * bestCz;
                size = new Vector3(bestX, size.y, bestZ);
                return;
            }

            // No piece of its footprint is clear (a thin bar lying almost IN the other road):
            // move the whole prism the least distance along the other road's normal that
            // clears it - above the deck, into the open under it, or below the keel.
            float support = ax * hx + az * hz + thick;
            float best = float.PositiveInfinity, to = d;
            float[] targets = _crossTargets;
            targets[0] = deckTop + support;
            targets[1] = keelBottom - support;
            targets[2] = 2f * support <= deckBottom - keelTop
                ? Mathf.Clamp(d, keelTop + support, deckBottom - support) : float.NaN;
            for (int i = 0; i < 3; i++)
                if (!float.IsNaN(targets[i]) && Mathf.Abs(targets[i] - d) < best)
                {
                    best = Mathf.Abs(targets[i] - d); to = targets[i];
                }
            c += n * (to - d);
            size = new Vector3(2f * hx, size.y, 2f * hz);
        }

        /// <summary>
        /// The largest axis-aligned piece of a footprint [-hx, hx] x [-hz, hz] whose support along a
        /// normal (signed distance <paramref name="d"/> + gx * x + gz * z, plus
        /// <paramref name="thick"/> either side) lies inside [<paramref name="lo"/>,
        /// <paramref name="hi"/>], and the offset of its centre from the footprint's: as close to
        /// the old centre as that piece can sit. False when no piece fits.
        /// </summary>
        static bool PieceIn(float d, float gx, float gz, float thick, float lo, float hi,
                            float hx, float hz, out float spanX, out float spanZ, out float cx, out float cz)
        {
            float ax = Mathf.Abs(gx), az = Mathf.Abs(gz);
            spanX = spanZ = cx = cz = 0f;
            if (float.IsInfinity(lo) || float.IsInfinity(hi))
            {
                // One-sided: anchor on the footprint corner furthest into the region.
                float side = float.IsInfinity(hi) ? 1f : -1f;
                float k = float.IsInfinity(hi)
                    ? d + ax * hx + az * hz - thick - lo
                    : hi - d + ax * hx + az * hz - thick;
                MaxRect(ax, az, k, 2f * hx, 2f * hz, out spanX, out spanZ);
                if (spanX <= 0f || spanZ <= 0f) return false;
                cx = side * (gx >= 0f ? 1f : -1f) * (hx - 0.5f * spanX);
                cz = side * (gz >= 0f ? 1f : -1f) * (hz - 0.5f * spanZ);
                return true;
            }

            // Two-sided: the piece's support must fit the band, then its centre slides along the
            // gradient to the band, as little as it can.
            float band = hi - lo - 2f * thick;
            MaxRect(ax, az, band, 2f * hx, 2f * hz, out spanX, out spanZ);
            if (spanX <= 0f || spanZ <= 0f) return false;
            float half = 0.5f * (ax * spanX + az * spanZ);
            float target = Mathf.Clamp(d, lo + thick + half, hi - thick - half);
            float g2 = gx * gx + gz * gz;
            if (g2 < 1e-8f) return target == d;
            float limX = hx - 0.5f * spanX, limZ = hz - 0.5f * spanZ;
            cx = Mathf.Clamp((target - d) * gx / g2, -limX, limX);
            float restZ = target - d - gx * cx;
            cz = az < 1e-4f ? 0f : Mathf.Clamp(restZ / gz, -limZ, limZ);
            float restX = target - d - gz * cz;
            if (ax >= 1e-4f) cx = Mathf.Clamp(restX / gx, -limX, limX);
            return Mathf.Abs(d + gx * cx + gz * cz - target) < 1e-3f;
        }

        /// <summary>Widening of an earlier road's slab, for the curvature and the row-to-row turn
        /// that a tangent plane at the foot does not see.</summary>
        const float CrossingMargin = 3f;

        readonly float[] _crossTargets = new float[3];

        /// <summary>The largest rectangle <paramref name="a"/> x <paramref name="b"/> (each no larger
        /// than <paramref name="maxA"/> / <paramref name="maxB"/>) whose support
        /// <c>ga * a + gb * b</c> fits in <paramref name="room"/> - the closed form of "shrink a
        /// plate only as much as it has to": both sides give up equal support unless one is
        /// already at its cap.</summary>
        static void MaxRect(float ga, float gb, float room, float maxA, float maxB, out float a, out float b)
        {
            const float Eps = 1e-4f;
            if (room <= 0f) { a = 0f; b = 0f; return; }
            if (ga < Eps && gb < Eps) { a = maxA; b = maxB; return; }
            if (ga < Eps) { a = maxA; b = Mathf.Min(maxB, room / gb); return; }
            if (gb < Eps) { b = maxB; a = Mathf.Min(maxA, room / ga); return; }
            a = room / (2f * ga); b = room / (2f * gb);
            if (a >= maxA) { a = maxA; b = Mathf.Min(maxB, (room - ga * maxA) / gb); }
            else if (b >= maxB) { b = maxB; a = Mathf.Min(maxA, (room - gb * maxB) / ga); }
        }

        /// <summary>One jitter factor in [0.8, 1.2] from the shared stream - exactly the one draw
        /// <c>Jit</c> makes, so spending it on a single axis moves no other prism.</summary>
        float JitFactor() => Jit(Vector3.one).x;

        /// <summary>
        /// How hard the spine is turning here, as a multiple of its own mean radius (1 for a
        /// perfect circle), and which SIDE the outside of that turn is on. The curvature vector
        /// <c>dT/ds</c> points toward the INSIDE of the bend, so the outer verge is the lane on the
        /// opposite side of it.
        /// </summary>
        static void BendAt(in Ribbon ribbon, float u, Vector3 across,
                           out float bendRatio, out int outerSide)
        {
            const float Du = 1e-3f;
            var t0 = ribbon.SpineTangent(u);
            var t1 = ribbon.SpineTangent(u + Du).normalized;
            float speed = Mathf.Max(1e-4f, t0.magnitude);

            var dTdu = (t1 - t0.normalized) / Du;
            bendRatio = dTdu.magnitude / speed * ribbon.Radius;
            outerSide = Vector3.Dot(dTdu, across) > 0f ? -1 : 1;
        }
    }
}
