using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Headlong's stampede: the HEDGES of mass standing across the circuit's straights, and the Time crystals beside its
    /// tightest corners (2026-11-07, Garrett: "make every time game a distinct and fun game that suits that vessel").
    ///
    /// <para><b>Why hedges, and why only for the Rhino.</b> The Rhino's hull is a plough (<c>RHINO_PLOUGH.md</c>): it
    /// shoves a rammed prism aside and loses no speed doing it, which no other hull does. Its ramp boost pays only while
    /// the line stays straight (<c>RHINO_RAMP_BOOST.md</c>), so a pilot who swerves round a wall pays for it in ramp. A
    /// wall across a straight is therefore the one obstacle this vessel goes THROUGH rather than round: hold the line,
    /// keep the boost, and the hedge sprays aside. That is the bulldozer fantasy the Time row's own text states.</para>
    ///
    /// <para><b>Danger hedges are the decision.</b> A danger prism costs the Rhino no speed (it carries no
    /// <c>VesselChangeSpeedByPrismEffect</c>) but BURNS one petal of every element per contact
    /// (<c>VesselElementalDebuffByDangerPrismEffectSO</c>, the Tuned rule, one contact a second). On the Rhino a Time
    /// level is Ramp Spool: the wind-up's rate, x1 at Time 0, x2.5 at Time 10, x0.5 floor below. So a burned Rhino
    /// winds up slower out of every corner for the rest of the race. A thorn hedge (I3) carries a plain WINDOW off the
    /// line: jink through it (ramp now) or plough the thorns (spool for the rest of the race). At I4 the hull starts at
    /// Time 5, where Momentum holds (Time L5: no danger drain above 70% of top speed), and the danger hedges stand just
    /// past the exits of the costing corners: carry the speed out of the corner and you bulldoze them clean; lift too
    /// much and they burn you below Time 5, and Momentum is gone until a crystal brings it back.</para>
    ///
    /// <code>
    ///   I1  Open range   plain hedges on the four longest legs            learn the ramp: drive THROUGH, never round
    ///   I2  Grazing      + two more plain hedges, two Time crystals       greed: a crystal inside the apex is a
    ///                      inside the tightest apexes (RedlineTimeCrystals.Apex)    tighter exit now, faster spool later
    ///   I3  Thorns       the three longest legs' hedges are danger,       jink or burn: ramp now, or spool later
    ///                      each with a plain window off the line
    ///   I4  Stampede     start at Time 5 (the card's StartingElements);   carry the corner: exit above 588 u/s and
    ///                      full danger hedges just past the three           Momentum bulldozes them; lift and burn
    ///                      costing corners' exits
    /// </code>
    ///
    /// <para><b>Closed form, zero random draws</b> (Breakwater's and Serpentine's rule): every peer lays the same hedges
    /// from the broadcast course, and the offline model (<c>Tools/Build/headlong_stampede_model.py</c>, fed by
    /// <c>Tools/Build/headlong_course_harness</c>) races the same ones. Every part is <see cref="PrismKind.Plain"/> or
    /// <see cref="PrismKind.Danger"/>. <c>HeadlongHedgesTests</c> holds the ladder, the clearances and the budget.</para>
    ///
    /// <para><b>The vessel is not changed</b> (LOCKED, Shombith 2026-10-31): the plough, the burn, the spool and Momentum
    /// are the Rhino's shipped kit. The mode lays mass on its own track and sets a starting element level on its card,
    /// which Brittle Star Time's I4 did first.</para>
    /// </summary>
    public static class HeadlongHedges
    {
        public enum Kind
        {
            /// <summary>Every prism plain: ploughed aside for free.</summary>
            Plain = 0,
            /// <summary>Danger, with a plain window off the line (I3).</summary>
            Thorn = 1,
            /// <summary>Danger all through, just past a costing corner's exit (I4).</summary>
            Stampede = 2,
        }

        /// <summary>One hedge: a disc of slabs across a leg, facing along it.</summary>
        public readonly struct Hedge
        {
            /// <summary>The leg it stands on: from gate <c>Leg</c> to gate <c>Leg + 1</c> (wrapping).</summary>
            public readonly int Leg;
            public readonly Vector3 Centre;
            /// <summary>Unit, along the leg (the way the race flows through it).</summary>
            public readonly Vector3 Normal;
            public readonly float Radius;
            public readonly Kind Kind;
            /// <summary>The plain window's centre (a thorn hedge only; otherwise the hedge's centre).</summary>
            public readonly Vector3 Window;
            /// <summary>The plain window's radius (0 = none).</summary>
            public readonly float WindowRadius;
            /// <summary>How far down the leg it stands, from the gate the leg leaves (u).</summary>
            public readonly float AlongLeg;

            public Hedge(int leg, Vector3 centre, Vector3 normal, float radius, Kind kind, Vector3 window,
                         float windowRadius, float alongLeg)
            {
                Leg = leg; Centre = centre; Normal = normal; Radius = radius; Kind = kind;
                Window = window; WindowRadius = windowRadius; AlongLeg = alongLeg;
            }

            public bool Danger => Kind != Kind.Plain;
        }

        /// <summary>One slab of a hedge, in world space.</summary>
        public readonly struct Part
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            /// <summary>x and y across the hedge, z its thickness along the leg.</summary>
            public readonly Vector3 Scale;
            public readonly PrismKind Kind;

            public Part(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind)
            {
                Position = position; Rotation = rotation; Scale = scale; Kind = kind;
            }
        }

        // ── The ladder ────────────────────────────────────────────────────────────────────────

        /// <summary>Hedges a lap at I1-I4 (one per leg at most; a lap has eight legs).</summary>
        public static readonly int[] HedgesPerLapByIntensity = { 4, 6, 8, 8 };

        /// <summary>Danger hedges a lap at I1-I4: thorns at I3 (the longest legs), stampede hedges at I4 (the exits of
        /// the tightest corners).</summary>
        public static readonly int[] DangerPerLapByIntensity = { 0, 0, 3, 3 };

        /// <summary>Time crystals a lap at I1-I4, each inside the apex of one of the tightest corners.</summary>
        public static readonly int[] CrystalsPerLapByIntensity = { 0, 2, 2, 2 };

        /// <summary>The Time level the Rhino starts the race at, I1-I4 (the card's <c>StartingElements</c>, written by
        /// <c>Tools/Build/author_headlong_assets.py</c>). Level 5 is where Momentum holds.</summary>
        public static readonly int[] StartingTimeLevelByIntensity = { 0, 0, 0, 5 };

        // ── The hedge's shape ─────────────────────────────────────────────────────────────────

        /// <summary>A hedge's radius (u): wider than every mouth on the ladder (96 u at I1), so the line through a ring
        /// meets it, and a swerve round it is a real one.</summary>
        public const float HedgeRadius = 120f;

        /// <summary>The smallest hedge worth standing (u): a hedge squeezed by a neighbouring leg below this is skipped.
        /// A stampede hedge may be smaller (<see cref="MinStampedeRadius"/>): it stands just past a hairpin, where the leg
        /// in still runs close, and the line out of a 46 u mouth is narrower than a sweeper's.</summary>
        public const float MinHedgeRadius = 80f;
        public const float MinStampedeRadius = 56f;

        /// <summary>Slab spacing across the hedge (u).</summary>
        public const float Pitch = 15f;

        /// <summary>One slab (u): 11 x 11 across, 4 thick. The 4 u seams are far narrower than any hull.</summary>
        public static readonly Vector3 SlabScale = new(11f, 11f, 4f);

        /// <summary>A thorn hedge is wider than a plain one (u), so its window can sit far enough off the line that
        /// taking it is a real swerve: on the longest legs, where there is room.</summary>
        public const float ThornRadius = 160f;

        /// <summary>A thorn hedge's plain window: its radius, and how far off the hedge's centre it is set (u). The
        /// window's centre is a 110 u jink off the line, an S that holds the Rhino near half its top speed over a
        /// 400 u run-up; a hull fits through 32 u.</summary>
        public const float WindowRadius = 32f;
        public const float WindowOffset = 110f;

        /// <summary>No hedge stands nearer a ring than this along its leg (u), so it never sits in a mouth's approach.</summary>
        public const float RingClearance = 150f;

        /// <summary>A hedge keeps this far (u) from every OTHER leg's line and every ring, beyond its own radius.</summary>
        public const float OtherLegMargin = 40f;

        /// <summary>Momentum's threshold: 70% of the Rhino's top speed (<c>VesselElementalImmunity.topSpeedFraction</c>
        /// on Rhino.prefab), 588 u/s.</summary>
        public const float MomentumFraction = 0.7f;
        public static float MomentumSpeed => MomentumFraction * HeadlongCircuitSettings.RhinoTopSpeed;

        /// <summary>The ramp's wind-up at the stampede's starting Time level (u/s^2): <c>accelerationPerSecond</c> 220
        /// x Ramp Spool at Time 5 (1 + 0.5 x (2.5 - 1) = 1.75), both from RhinoRampBoostAction.asset.
        /// <c>headlong_stampede_model.py --self-test</c> holds both numbers to the asset.</summary>
        public const float RampAcceleration = 220f;
        public const float SpoolAtTimeTen = 2.5f;
        public static float StampedeAcceleration =>
            RampAcceleration * (1f + (SpoolAtTimeTen - 1f) * StartingTimeLevelByIntensity[3] / 10f);

        /// <summary>
        /// I4: the slack (u) a stampede hedge leaves a PERFECT exit. The hedge stands where a Rhino that left its corner
        /// at the fastest speed the corner can be held at (<see cref="HeadlongCircuitSettings.FastestSpeedForCorner"/>)
        /// first reaches Momentum, plus this. At a hairpin's ~285 u/s that forgives an exit about 45 u/s (16%) slow;
        /// slower than that and the hedge burns you.
        /// </summary>
        public const float StampedeSlack = 30f;

        /// <summary>Where along the exit leg the stampede hedge of a corner of <paramref name="cornerRadius"/> stands:
        /// a perfect exit's distance to Momentum plus <see cref="StampedeSlack"/>.</summary>
        public static float StampedeAlongExit(float cornerRadius)
        {
            float exit = HeadlongCircuitSettings.FastestSpeedForCorner(cornerRadius);
            float need = Mathf.Max(0f, (MomentumSpeed * MomentumSpeed - exit * exit) / (2f * StampedeAcceleration));
            return need + StampedeSlack;
        }

        /// <summary>A crystal keeps this far (u) from every hedge's plane, so taking it is never also ramming a hedge.</summary>
        public const float CrystalHedgeClearance = 50f;

        public static int HedgesPerLap(int intensity) => HedgesPerLapByIntensity[Clamp(intensity) - 1];
        public static int DangerPerLap(int intensity) => DangerPerLapByIntensity[Clamp(intensity) - 1];
        public static int CrystalsPerLap(int intensity) => CrystalsPerLapByIntensity[Clamp(intensity) - 1];
        public static int StartingTimeLevel(int intensity) => StartingTimeLevelByIntensity[Clamp(intensity) - 1];

        static int Clamp(int intensity) => Mathf.Clamp(intensity, 1, 4);

        // ── Placement ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The hedges of one lap of <paramref name="course"/> at <paramref name="intensity"/>, in leg order. Clears
        /// <paramref name="into"/>. A pure function of the course: the same gates give the same hedges on every peer.
        /// </summary>
        public static void Place(IReadOnlyList<RaceGate> course, int intensity, List<Hedge> into)
        {
            into.Clear();
            int n = course?.Count ?? 0;
            if (n < 3) return;
            int i4 = Clamp(intensity);

            // Legs, longest first (ties keep the lower index); corners, tightest first.
            var legs = new List<int>(n);
            var length = new float[n];
            for (int l = 0; l < n; l++)
            {
                legs.Add(l);
                length[l] = Vector3.Distance(course[l].Position, course[(l + 1) % n].Position);
            }
            legs.Sort((a, b) => length[a] != length[b] ? length[b].CompareTo(length[a]) : a.CompareTo(b));
            var tight = TightestCorners(course);

            var hedgeLegs = new HashSet<int>();
            for (int k = 0; k < Mathf.Min(HedgesPerLap(i4), n); k++) hedgeLegs.Add(legs[k]);

            // Which legs carry danger, and where along the leg each hedge stands.
            var kind = new Kind[n];
            var along = new float[n];
            for (int l = 0; l < n; l++) along[l] = 0.5f * length[l];
            int danger = DangerPerLap(i4);
            if (i4 == 3)
            {
                for (int k = 0; k < Mathf.Min(danger, n); k++) kind[legs[k]] = Kind.Thorn;
            }
            else if (i4 >= 4)
            {
                // A corner at gate c is left on leg c (c -> c + 1).
                for (int k = 0; k < Mathf.Min(danger, tight.Count); k++)
                {
                    int leg = tight[k];
                    kind[leg] = Kind.Stampede;
                    float r = RaceCourseGeometry.CornerRadius(course[(leg - 1 + n) % n].Position, course[leg].Position,
                                                              course[(leg + 1) % n].Position);
                    along[leg] = StampedeAlongExit(r);
                    hedgeLegs.Add(leg);
                }
            }

            for (int leg = 0; leg < n; leg++)
            {
                if (!hedgeLegs.Contains(leg)) continue;
                if (TryHedge(course, leg, length[leg], along[leg], kind[leg], out var hedge)) into.Add(hedge);
            }
        }

        static bool TryHedge(IReadOnlyList<RaceGate> course, int leg, float legLength, float alongLeg, Kind kind,
                             out Hedge hedge)
        {
            hedge = default;
            int n = course.Count;
            Vector3 a = course[leg].Position, b = course[(leg + 1) % n].Position;
            if (legLength < 2f * RingClearance) return false;
            alongLeg = Mathf.Clamp(alongLeg, RingClearance, legLength - RingClearance);
            Vector3 normal = (b - a) / legLength;
            Vector3 centre = a + normal * alongLeg;

            // Shrink the hedge to keep clear of every other leg's line and every ring it does not stand between.
            float radius = kind == Kind.Thorn ? ThornRadius : HedgeRadius;
            for (int other = 0; other < n; other++)
            {
                if (other == leg) continue;
                Vector3 p = course[other].Position, q = course[(other + 1) % n].Position;
                float d = DistanceToSegment(centre, p, q);
                radius = Mathf.Min(radius, d - OtherLegMargin);
            }
            // Every ring but the leg's own two (those stand RingClearance away along the leg, facing the same way).
            for (int g = 0; g < n; g++)
            {
                if (g == leg || g == (leg + 1) % n) continue;
                radius = Mathf.Min(radius, Vector3.Distance(centre, course[g].Position) - course[g].Radius - OtherLegMargin);
            }
            if (radius < (kind == Kind.Stampede ? MinStampedeRadius : MinHedgeRadius)) return false;

            Vector3 window = centre;
            float windowRadius = 0f;
            if (kind == Kind.Thorn)
            {
                // The window turns with the leg's index round the hedge, so neighbouring thorns ask for different jinks.
                Frame(normal, out var right, out var up);
                float phi = (leg * 137.5f) * Mathf.Deg2Rad;   // the golden angle: spread, never repeating
                Vector3 off = right * Mathf.Cos(phi) + up * Mathf.Sin(phi);
                window = centre + off * Mathf.Min(WindowOffset, radius - WindowRadius - Pitch);
                windowRadius = WindowRadius;
            }
            hedge = new Hedge(leg, centre, normal, radius, kind, window, windowRadius, alongLeg);
            return true;
        }

        /// <summary>The course's corners, tightest first (by <see cref="RaceCourseGeometry.CornerRadius"/>; ties keep
        /// the lower index). The corner at gate c is the turn from leg c - 1 into leg c.</summary>
        public static List<int> TightestCorners(IReadOnlyList<RaceGate> course)
        {
            int n = course?.Count ?? 0;
            var order = new List<int>(n);
            var radius = new float[n];
            for (int i = 0; i < n; i++)
            {
                order.Add(i);
                radius[i] = RaceCourseGeometry.CornerRadius(course[(i - 1 + n) % n].Position, course[i].Position,
                                                            course[(i + 1) % n].Position);
            }
            order.Sort((a, b) => radius[a] != radius[b] ? radius[a].CompareTo(radius[b]) : a.CompareTo(b));
            return order;
        }

        // ── The Time crystals ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// The Time crystals of one lap: inside the apex of each of the <see cref="CrystalsPerLap"/> tightest corners
        /// (<see cref="RedlineTimeCrystals.Apex"/>: a quarter of the way out, 70 u toward the inside), so taking one is
        /// a tighter exit. Nudged back along the exit leg off any hedge's plane. Clears <paramref name="into"/>.
        /// </summary>
        public static void PlaceCrystals(IReadOnlyList<RaceGate> course, int intensity, IReadOnlyList<Hedge> hedges,
                                         List<Vector3> into)
        {
            into.Clear();
            int n = course?.Count ?? 0;
            if (n < 3) return;
            int count = Mathf.Min(CrystalsPerLap(intensity), n);
            if (count <= 0) return;
            var tight = TightestCorners(course);
            for (int k = 0; k < count; k++)
            {
                int c = tight[k];
                Vector3 prev = course[(c - 1 + n) % n].Position, at = course[c].Position, next = course[(c + 1) % n].Position;
                Vector3 crystal = RedlineTimeCrystals.Apex(prev, at, next);
                // Nudged back toward the corner until no hedge's face is within the clearance (two passes settle
                // every shipped seed; four is the bound).
                for (int pass = 0; hedges != null && pass < 4; pass++)
                {
                    bool moved = false;
                    for (int h = 0; h < hedges.Count; h++)
                    {
                        var hedge = hedges[h];
                        float s = Vector3.Dot(crystal - hedge.Centre, hedge.Normal);
                        if (Mathf.Abs(s) >= CrystalHedgeClearance) continue;
                        if ((crystal - hedge.Centre - hedge.Normal * s).magnitude >= hedge.Radius) continue;   // beside it
                        crystal -= hedge.Normal * (CrystalHedgeClearance + s);
                        moved = true;
                    }
                    if (!moved) break;
                }
                into.Add(crystal);
            }
        }

        // ── The slabs ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every slab of <paramref name="hedge"/>: a square grid at <see cref="Pitch"/> clipped to the disc, each slab
        /// facing along the leg. A thorn hedge's slabs are danger except those inside its window, which are left out
        /// (the window is open air framed by plain slabs one pitch wide); a stampede hedge is danger all through.
        /// Appends to <paramref name="into"/>.
        /// </summary>
        public static void Build(in Hedge hedge, List<Part> into)
        {
            Frame(hedge.Normal, out var right, out var up);
            var rotation = Quaternion.LookRotation(hedge.Normal, up);
            int cells = Mathf.CeilToInt(hedge.Radius / Pitch);
            for (int y = -cells; y <= cells; y++)
            for (int x = -cells; x <= cells; x++)
            {
                Vector3 offset = right * (x * Pitch) + up * (y * Pitch);
                if (offset.magnitude > hedge.Radius) continue;
                Vector3 p = hedge.Centre + offset;
                var prismKind = hedge.Danger ? PrismKind.Danger : PrismKind.Plain;
                if (hedge.WindowRadius > 0f)
                {
                    float w = Vector3.Distance(p, hedge.Window);
                    if (w < hedge.WindowRadius) continue;                 // the window itself: open
                    if (w < hedge.WindowRadius + Pitch) prismKind = PrismKind.Plain;   // its plain frame
                }
                into.Add(new Part(p, rotation, SlabScale, prismKind));
            }
        }

        /// <summary>Two unit vectors across a hedge facing <paramref name="normal"/>.</summary>
        public static void Frame(Vector3 normal, out Vector3 right, out Vector3 up)
        {
            right = RaceCourseGeometry.Perpendicular(normal);
            up = Vector3.Cross(normal, right).normalized;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>Does the segment from <paramref name="from"/> to <paramref name="to"/> cross the hedge's plane
        /// inside its disc, and if so is the crossing in the window? (The model's and the AI's question.)</summary>
        public static bool Crosses(in Hedge hedge, Vector3 from, Vector3 to, out bool inWindow, out Vector3 at)
        {
            inWindow = false;
            at = default;
            float s0 = Vector3.Dot(from - hedge.Centre, hedge.Normal), s1 = Vector3.Dot(to - hedge.Centre, hedge.Normal);
            if (s0 > 0f || s1 < 0f || Mathf.Approximately(s0, s1)) return false;
            at = Vector3.Lerp(from, to, -s0 / (s1 - s0));
            if ((at - hedge.Centre).magnitude > hedge.Radius) return false;
            inWindow = hedge.WindowRadius > 0f && Vector3.Distance(at, hedge.Window) < hedge.WindowRadius;
            return true;
        }
    }
}
