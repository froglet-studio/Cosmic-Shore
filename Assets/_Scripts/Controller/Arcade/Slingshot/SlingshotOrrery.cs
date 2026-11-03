using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One corner's MOON: the body the Stoat orbits round to turn there (<see cref="SlingshotOrrery"/>).</summary>
    public readonly struct OrreryMoon
    {
        public readonly int Gate;            // the course gate whose corner this is
        public readonly Vector3 Centre;
        public readonly float Radius;
        public readonly Vector3 Normal;      // unit: the corner's plane (the orbit's)
        public readonly Vector3 Inbound;     // unit: the leg in
        public readonly Vector3 Outbound;    // unit: the leg out
        public readonly float BeltRadius;    // 0 = no belt
        public readonly Vector3 PressPoint;  // where a press lays its attractor on the moon (the I1 beacon)

        public OrreryMoon(int gate, Vector3 centre, float radius, Vector3 normal, Vector3 inbound, Vector3 outbound,
                          float beltRadius, Vector3 pressPoint)
        {
            Gate = gate; Centre = centre; Radius = radius; Normal = normal; Inbound = inbound; Outbound = outbound;
            BeltRadius = beltRadius; PressPoint = pressPoint;
        }
    }

    /// <summary>
    /// THE ORRERY - the Stoat's Time race made a planetary system (Yash, 2026-11-03: "unique environment for each vessels'
    /// time game ... something that resembles and can take opportunity from stoat's orbit mechanics"; <c>Arcade/SLINGSHOT.md</c>
    /// "The Orrery"). Every corner of Slingshot's circuit gets a MOON just inside its ring: the body you whip round. A press
    /// lays the Stoat's black hole <see cref="PressAhead"/> ahead and <see cref="PressSide"/> to that side, so pressing as the
    /// moon comes up on that side wraps your orbit round it, the ring sits on that orbit, and a release in the lime window
    /// threads it and launches you at the next moon. (The AI flies it on its own sling rule, which keeps off the moons.)
    /// The ladder adds one demand a level:
    /// <list type="bullet">
    /// <item><b>I1 Moons</b>: a small moon per corner and a BEACON where to press for it.</item>
    /// <item><b>I2 Belts</b>: each moon gets a debris ring outside its orbit, open only where the legs cross it, so the launch
    /// has to be aimed through the gap. No beacons.</item>
    /// <item><b>I3 Comet tails</b>: thicker belts with danger in them, and debris tubes round the straights.</item>
    /// <item><b>I4 Clockwork</b>: tighter gaps and tubes, more danger, and SHEPHERD moonlets circling outside every belt on the
    /// shared clock, sweeping across its gaps.</item>
    /// </list>
    ///
    /// <para><b>The moons are only obstacles</b> (LOCKED: a game mode never changes the vessel). They pull nothing and never
    /// catch the Stoat's pair; the skill is laying the pair where the moon is.</para>
    ///
    /// <para><b>A pure function of the course</b>, like Blast Run (<see cref="SwitchbackBlastRun"/>), whose machinery it reuses:
    /// the same <see cref="SwitchbackFeatureLayout"/> laid by <see cref="SpawnableSwitchbackFeatures"/>, and the same
    /// clock-driven <see cref="BlastRunMover"/>s posed by <see cref="SwitchbackBlastRunRuntime"/>, so every peer lays and moves
    /// the same prisms with no network traffic. <c>SlingshotOrreryTests</c> holds it without a scene;
    /// <c>Tools/Build/orrery_port.py</c> ports it for the offline model.</para>
    /// </summary>
    public static class SlingshotOrrery
    {
        // ── The Stoat's press (StoatSlingConfig.asset catchAhead / catchSide; SlingshotOrreryTests reads them back) ──
        public const float PressAhead = 150f;
        public const float PressSide = 45f;

        // ── Clearances ──────────────────────────────────────────────────────
        public const float MoonRimGap = 8f;              // u between a moon's surface and its ring's rim
        public const float LineClearance = 40f;          // u everything but a corner's own body keeps off every OTHER leg's line
        public const float MinTurnDegrees = 15f;         // a gate turning less than this is a straight: no moon

        // ── The ladder ───────────────────────────────────────────────────────
        static int I(int intensity) => Mathf.Clamp(intensity, 1, 4) - 1;
        public static float MoonRadius(int intensity) => new[] { 9f, 10f, 11f, 12f }[I(intensity)];
        public const float MoonCube = 5f;
        public static bool HasBeacons(int intensity) => intensity <= 1;
        public static bool HasBelts(int intensity) => intensity >= 2;
        public static bool HasTails(int intensity) => intensity >= 3;
        public static bool HasShepherds(int intensity) => intensity >= 4;
        /// <summary>u from a ring's rim (on the side away from its moon) to the belt.</summary>
        public const float BeltClearance = SwitchbackFeatures.MouthClearance;
        /// <summary>Degrees of belt left open round each crossing leg.</summary>
        public static float BeltGapDegrees(int intensity) => new[] { 0f, 70f, 52f, 40f }[I(intensity)];
        public static int BeltRows(int intensity) => new[] { 0, 1, 2, 3 }[I(intensity)];
        public static float BeltCube(int intensity) => new[] { 0f, 6f, 6.5f, 7f }[I(intensity)];
        public const float BeltPitch = 13f;              // u between belt cubes along the circle
        /// <summary>The tube round a straight: its radius, and how full its stations are.</summary>
        public static float TailRadius(int intensity) => new[] { 0f, 0f, 58f, 46f }[I(intensity)];
        public static float TailFill(int intensity) => new[] { 0f, 0f, 0.55f, 0.75f }[I(intensity)];
        public const float TailPitch = 22f;               // u between stations along the leg
        public const int TailSpokes = 12;                  // cubes round one station
        public const float TailCube = 7f;
        public static float DangerShare(int intensity) => new[] { 0f, 0f, 0.12f, 0.18f }[I(intensity)];
        /// <summary>Shepherds: a 2 x 2 of cubes circling the moon just outside its belt, two per moon, half a turn apart.</summary>
        public const float ShepherdOffset = 22f;           // u outside the belt
        public const float ShepherdCube = 6f;
        public const float ShepherdPeriod = 9f;            // s a lap
        // ── Budgets (Blast Run's, for the same 100 fps) ───────────────────────
        public static int TotalCap(int intensity) => new[] { 900, 1600, 2400, 3000 }[I(intensity)];
        public static int MovingPrismCap(int intensity) => new[] { 0, 0, 0, 140 }[I(intensity)];

        /// <summary>
        /// The Orrery for <paramref name="course"/> (one lap's gates, in order) at <paramref name="intensity"/>, kept out of the
        /// nucleus (<paramref name="centre"/>, <paramref name="clearRadius"/>). The moons are added to <paramref name="moons"/>
        /// (cleared first) for the tests and the controller.
        /// </summary>
        public static SwitchbackFeatureLayout Layout(IReadOnlyList<RaceGate> course, int intensity, Vector3 centre, float clearRadius,
                                                     List<OrreryMoon> moons)
        {
            var layout = new SwitchbackFeatureLayout();
            moons?.Clear();
            if (course == null || course.Count < 3) return layout;
            int n = course.Count;
            var ctx = new Ctx(course, layout, centre, clearRadius, TotalCap(intensity));
            var found = moons ?? new List<OrreryMoon>();

            // Moons first: they are the corners, and the belts and tubes keep off them.
            for (int g = 0; g < n; g++)
                if (TryMoon(course, g, intensity, out var moon)) found.Add(moon);
            for (int m = 0; m < found.Count; m++) EmitMoon(ctx, found[m]);
            if (HasBeacons(intensity))
                for (int m = 0; m < found.Count; m++) EmitBeacon(ctx, found[m]);
            if (HasShepherds(intensity))
                for (int m = 0; m < found.Count; m++) EmitShepherds(ctx, found[m], m, intensity);
            if (HasBelts(intensity))
                for (int m = 0; m < found.Count; m++) EmitBelt(ctx, found, m, intensity);
            if (HasTails(intensity))
                for (int g = 0; g < n; g++) EmitTail(ctx, found, g, intensity);
            return layout;
        }

        /// <summary>The corner at gate <paramref name="g"/>: its moon just inside the ring, on the turn's inside.</summary>
        public static bool TryMoon(IReadOnlyList<RaceGate> course, int g, int intensity, out OrreryMoon moon)
        {
            moon = default;
            int n = course.Count;
            Vector3 p = course[g].Position;
            Vector3 a = p - course[(g - 1 + n) % n].Position;
            Vector3 b = course[(g + 1) % n].Position - p;
            if (a.sqrMagnitude < 1f || b.sqrMagnitude < 1f) return false;
            a.Normalize();
            b.Normalize();
            if (Vector3.Angle(a, b) < MinTurnDegrees) return false;
            Vector3 inward = b - a;
            Vector3 normal = Vector3.Cross(a, b);
            if (inward.sqrMagnitude < 1e-6f || normal.sqrMagnitude < 1e-8f) return false;
            inward.Normalize();
            normal.Normalize();
            float radius = MoonRadius(intensity);
            float ring = course[g].Radius;
            Vector3 centre = p + inward * (ring + radius + MoonRimGap);
            float belt = HasBelts(intensity) ? (ring + radius + MoonRimGap) + ring + BeltClearance : 0f;
            // A press lays the attractor PressAhead along the nose and PressSide to that side: flying the leg in, pressing here
            // puts it on the moon.
            Vector3 press = centre - a * PressAhead - inward * PressSide;
            moon = new OrreryMoon(g, centre, radius, normal, a, b, belt, press);
            return true;
        }

        // ── The pieces ───────────────────────────────────────────────────────

        /// <summary>A voxel shell of cubes: the moon, faceted and lit, never a flat disc.</summary>
        static void EmitMoon(Ctx ctx, in OrreryMoon moon)
        {
            float r = moon.Radius, c = MoonCube;
            int cells = Mathf.CeilToInt(r / c);
            var basis = Quaternion.LookRotation(moon.Inbound, moon.Normal);
            for (int x = -cells; x <= cells; x++)
            for (int y = -cells; y <= cells; y++)
            for (int z = -cells; z <= cells; z++)
            {
                var local = new Vector3(x, y, z) * c;
                float d = local.magnitude;
                if (d > r || d < r - c * 1.2f) continue;   // the shell only: nobody sees the inside
                ctx.Add(moon.Centre + basis * local, basis, Vector3.one * (c - 0.6f), PrismKind.Plain, own: moon.Gate);
            }
        }

        /// <summary>I1: three small cubes where a press wraps the orbit round the moon - the press point, shown in the world.</summary>
        static void EmitBeacon(Ctx ctx, in OrreryMoon moon)
        {
            var basis = Quaternion.LookRotation(moon.Inbound, moon.Normal);
            for (int k = 0; k < 3; k++)
            {
                var offset = Quaternion.AngleAxis(120f * k, moon.Inbound) * (moon.Normal * 6f);
                ctx.Add(moon.PressPoint + offset, basis, Vector3.one * 3f, PrismKind.Plain, own: moon.Gate);
            }
        }

        /// <summary>
        /// I2+: the belt, a ring of cubes round the moon in its corner's plane, open where the leg in and the leg out cross it
        /// (<see cref="BeltGapDegrees"/> round each crossing). I3+ lays more rows and some danger.
        /// </summary>
        static void EmitBelt(Ctx ctx, List<OrreryMoon> moons, int m, int intensity)
        {
            var moon = moons[m];
            float rb = moon.BeltRadius;
            if (rb <= 0f) return;
            var gate = ctx.Course[moon.Gate].Position;
            Vector3 inGap = Crossing(gate, -moon.Inbound, moon.Centre, rb);
            Vector3 outGap = Crossing(gate, moon.Outbound, moon.Centre, rb);
            float half = BeltGapDegrees(intensity) * 0.5f;
            Vector3 u = (gate - moon.Centre).normalized;
            int count = Mathf.Max(12, Mathf.RoundToInt(2f * Mathf.PI * rb / BeltPitch));
            int rows = BeltRows(intensity);
            float cube = BeltCube(intensity);
            for (int k = 0; k < count; k++)
            {
                float ang = 360f * k / count;
                Vector3 dir = Quaternion.AngleAxis(ang, moon.Normal) * u;
                if (Vector3.Angle(dir, inGap) < half || Vector3.Angle(dir, outGap) < half) continue;
                for (int row = 0; row < rows; row++)
                {
                    float lift = rows > 1 ? (row - (rows - 1) * 0.5f) * (cube + 3f) : 0f;
                    Vector3 at = moon.Centre + dir * rb + moon.Normal * lift;
                    var rot = Quaternion.LookRotation(Vector3.Cross(moon.Normal, dir), moon.Normal);
                    ctx.Add(at, rot, Vector3.one * cube, ctx.Danger(k * 7 + row + m * 131, intensity), own: -1);
                }
            }
        }

        /// <summary>Where the ray from <paramref name="from"/> along <paramref name="dir"/> leaves the circle round
        /// <paramref name="centre"/>, as a unit direction from the centre.</summary>
        static Vector3 Crossing(Vector3 from, Vector3 dir, Vector3 centre, float radius)
        {
            Vector3 f = from - centre;
            float b = Vector3.Dot(f, dir);
            float c = f.sqrMagnitude - radius * radius;
            float t = -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c));
            return (from + dir * t - centre).normalized;
        }

        /// <summary>I3+: a tube of debris round the straight between gate <paramref name="g"/> and the next, its middle part.</summary>
        static void EmitTail(Ctx ctx, List<OrreryMoon> moons, int g, int intensity)
        {
            int n = ctx.Course.Count;
            Vector3 a = ctx.Course[g].Position, b = ctx.Course[(g + 1) % n].Position;
            Vector3 dir = b - a;
            float length = dir.magnitude;
            if (length < 4f * TailPitch) return;
            dir /= length;
            Vector3 s1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 s2 = Vector3.Cross(dir, s1);
            float radius = TailRadius(intensity), fill = TailFill(intensity);
            var rot = Quaternion.LookRotation(dir, s2);
            for (float t = 0.3f * length; t <= 0.7f * length; t += TailPitch)
            {
                Vector3 c = a + dir * t;
                bool nearMoon = false;
                for (int m = 0; m < moons.Count; m++)
                    if ((c - moons[m].Centre).magnitude < Mathf.Max(moons[m].BeltRadius, moons[m].Radius) + radius + 20f) { nearMoon = true; break; }
                if (nearMoon) continue;
                for (int k = 0; k < TailSpokes; k++)
                {
                    int hash = Hash(g, Mathf.RoundToInt(t), k);
                    if ((hash & 1023) / 1024f > fill) continue;
                    float ang = 360f * (k + 0.5f * (hash >> 10 & 1)) / TailSpokes;
                    Vector3 off = Quaternion.AngleAxis(ang, dir) * s1 * radius;
                    ctx.Add(c + off, rot, Vector3.one * TailCube, ctx.Danger(hash, intensity), own: g, ownNext: true);
                }
            }
        }

        /// <summary>I4: two 2 x 2 moonlets circling outside the belt, half a lap apart, on the shared clock.</summary>
        static void EmitShepherds(Ctx ctx, in OrreryMoon moon, int m, int intensity)
        {
            float orbit = moon.BeltRadius + ShepherdOffset;
            if (orbit <= ShepherdOffset) return;
            // The whole sweep must keep off every other leg and ring, or the moon gets none.
            Vector3 u = (ctx.Course[moon.Gate].Position - moon.Centre).normalized;
            for (int k = 0; k < 36; k++)
            {
                Vector3 p = moon.Centre + Quaternion.AngleAxis(10f * k, moon.Normal) * u * orbit;
                if (!ctx.Clear(p, ShepherdCube * 2f, moon.Gate, false)) return;
            }
            float omega = 2f * Mathf.PI / ShepherdPeriod;
            for (int s = 0; s < 2; s++)
            {
                if (ctx.Layout.Movers.Count * 4 + 4 > MovingPrismCap(intensity)) return;
                Vector3 dir = Quaternion.AngleAxis(90f + 180f * s, moon.Normal) * u;
                Vector3 at = moon.Centre + dir * orbit;
                var rot = Quaternion.LookRotation(Vector3.Cross(moon.Normal, dir), moon.Normal);
                int first = ctx.Layout.Prisms.Count;
                for (int q = 0; q < 4; q++)
                {
                    var off = rot * new Vector3((q & 1) - 0.5f, (q >> 1) - 0.5f, 0f) * (ShepherdCube + 1f);
                    ctx.Layout.Prisms.Add(new SwitchbackFeaturePrism(at + off, rot, Vector3.one * ShepherdCube, PrismKind.Danger));
                }
                ctx.Layout.Movers.Add(new BlastRunMover(m, first, 4, BlastRunMotion.Spin, moon.Centre, moon.Normal, 0f, omega, 0f));
            }
        }

        static int Hash(int a, int b, int c)
        {
            unchecked
            {
                int h = (int)2166136261;
                h = (h ^ a) * 16777619;
                h = (h ^ b) * 16777619;
                h = (h ^ c) * 16777619;
                return h & 0x7fffffff;
            }
        }

        /// <summary>The lay pass: the course, the budget and the clearances every static piece keeps.</summary>
        sealed class Ctx
        {
            public readonly IReadOnlyList<RaceGate> Course;
            public readonly SwitchbackFeatureLayout Layout;
            readonly Vector3 _centre;
            readonly float _clear;
            readonly int _cap;

            public Ctx(IReadOnlyList<RaceGate> course, SwitchbackFeatureLayout layout, Vector3 centre, float clear, int cap)
            {
                Course = course; Layout = layout; _centre = centre; _clear = clear; _cap = cap;
            }

            public PrismKind Danger(int hash, int intensity) =>
                (Hash(hash, 17, 3) & 1023) / 1024f < DangerShare(intensity) ? PrismKind.Danger : PrismKind.Plain;

            /// <summary>
            /// Lay one static prism unless it breaks a clearance or the budget. <paramref name="own"/> is the gate whose
            /// corner (or, with <paramref name="ownNext"/>, whose leg out) it belongs to: that gate's ring and legs may be
            /// near it; every other ring and leg may not.
            /// </summary>
            public void Add(Vector3 at, Quaternion rot, Vector3 scale, PrismKind kind, int own, bool ownNext = false)
            {
                if (Layout.Prisms.Count >= _cap) return;
                if (!Clear(at, scale.x, own, ownNext)) return;
                Layout.Prisms.Add(new SwitchbackFeaturePrism(at, rot, scale, kind));
            }

            public bool Clear(Vector3 at, float size, int own, bool ownNext)
            {
                if ((at - _centre).magnitude < _clear + size) return false;
                int n = Course.Count;
                for (int g = 0; g < n; g++)
                {
                    var gate = Course[g];
                    // Every ring's mouth (a corner's own moon sits deliberately close to its own ring, outside the rim).
                    float rim = (at - gate.Position).magnitude - gate.Radius;
                    if (g != own && rim < SwitchbackFeatures.MouthClearance + size) return false;
                    if (g == own && rim < MoonRimGap * 0.5f) return false;
                    // Every leg's line but the piece's own legs.
                    int next = (g + 1) % n;
                    bool ownLeg = g == own || next == own;
                    if (ownNext) ownLeg = g == own;
                    float line = SwitchbackFeatures.DistanceToSegment(at, gate.Position, Course[next].Position);
                    if (!ownLeg && line < LineClearance + size) return false;
                    if (ownNext && g == own && line < TailRadius(4) * 0.5f) return false;
                }
                return true;
            }
        }
    }
}
