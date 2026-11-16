using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The POWDER KEGS of Grizzly Time (Docs/GrizzlyTime/POWDER_KEGS_SPEC.md): a small cage of prisms beside every ring,
    /// placed so the Grizzly's own kit becomes the race. Freeze a bomb in the keg, get round to its far side, blow it: the
    /// keg bursts, the gate is yours (you were beside an open keg), and the blast throws you down the next leg - because
    /// a Grizzly inside its own blast is launched AWAY FROM THE BOMB (<c>GrizzlyTriggerBombExecutor.LaunchSelf</c>), so
    /// where you stand when you blow it is how you steer. The ring mouth still counts, so the old line is never closed.
    /// <list type="bullet">
    /// <item><b>I1 KEGS</b>: one plain keg on each corner's inside. One bomb is both key and engine.</item>
    /// <item><b>I2 BANK SHOT</b>: a chalk rail marks each keg's exit lane; blow it from that side and the push points
    /// at the next gate.</item>
    /// <item><b>I3 TWO KEYS</b>: the core is shielded - one blast strips it (from range, no push), a second opens it. A
    /// stripped core shields itself again.</item>
    /// <item><b>I4 BREAK YOUR OWN</b>: the tightest credit radius and the fastest regrow - a rival's open keg is gone
    /// before you reach it. (I4 was designed as twin kegs, inside and outside; the throw-chain sweep found the inside keg
    /// right at 99% of gates and reading the choice worth 0.1%, so the twins were dropped as the spec's bar said.)</item>
    /// </list>
    /// <para><b>Closed form, zero random draws</b> (Breakwater's, Thresher Time's rule): every peer builds the same kegs
    /// from the broadcast course. Pure: no <c>UnityEngine.Object</c>, no <c>Time</c>, no scene. The core is listed
    /// first. <c>GrizzlyTimeKegsTests</c> holds the geometry, the ladder and the budget;
    /// <c>Tools/Build/grizzly_time_course_harness</c> runs them offline and measures the throw chain.</para>
    /// <para><b>The vessel is not touched</b> (CLAUDE.md, "A game mode never changes the vessel"): the reach constants are
    /// READ from <see cref="GrizzlyTimeCourse"/>, which reads them back off the shipped bomb config.</para>
    /// </summary>
    public static class GrizzlyTimeKegs
    {
        // ── The ladder (index = intensity - 1) ─────────────────────────────

        /// <summary>How near (u) a pilot must pass an OPEN keg for it to clear the gate: beside it when it broke, or through
        /// its wreck before it regrows. Shrinks so the top levels are a race to break your own.</summary>
        public static readonly float[] CreditRadiusByIntensity = { 160f, 140f, 120f, 100f };

        /// <summary>Seconds an opened keg stays down before it grows back (it waits on while a hull is inside
        /// <see cref="HullClear"/>).</summary>
        public static readonly float[] RegrowSecondsByIntensity = { 3f, 3f, 2.5f, 2f };

        /// <summary>The core's armour: a plain core opens to one blast or one ram; a shielded core sheds its shield to the
        /// first and opens to the second.</summary>
        public static readonly PrismKind[] CoreKindByIntensity =
            { PrismKind.Plain, PrismKind.Plain, PrismKind.Shielded, PrismKind.Shielded };

        /// <summary>A chalk rail marks the exit lane (I2 up).</summary>
        public static readonly bool[] ChalkByIntensity = { false, true, true, true };

        /// <summary>Seconds after it was stripped that a still-standing shielded core shields itself again (I3 up), once
        /// no hull is inside <see cref="HullClear"/>.</summary>
        public const float ReshieldSeconds = 2.5f;

        public static float CreditRadius(int intensity) => CreditRadiusByIntensity[Level(intensity)];
        public static float RegrowSeconds(int intensity) => RegrowSecondsByIntensity[Level(intensity)];
        public static PrismKind CoreKind(int intensity) => CoreKindByIntensity[Level(intensity)];
        public static bool HasChalk(int intensity) => ChalkByIntensity[Level(intensity)];
        static int Level(int intensity) => Mathf.Clamp(intensity, 1, 4) - 1;

        // ── Geometry ───────────────────────────────────────────────────────

        /// <summary>Keg centre from the ring centre (u), in the ring's plane toward the corner's inside. Past the widest
        /// mouth plus a full blast, so a pilot threading the mouth neither hits a keg nor opens one.</summary>
        public const float KegOffset = 140f;
        /// <summary>The keg's hoop: <see cref="HoopSlabs"/> plain slabs round the core, in the plane across the exit lane,
        /// so the keg reads as a barrel pointing where it will throw you.</summary>
        public const float HoopRadius = 16f;
        public const int HoopSlabs = 12;
        public static readonly Vector3 HoopSlabScale = new(3f, 1.5f, 7f);
        /// <summary>The core: <see cref="CoreBlocks"/> cubes of <see cref="CoreCube"/> u, <see cref="CoreSpacing"/> apart
        /// across the lane. Break any one and the keg bursts.</summary>
        public const int CoreBlocks = 3;
        public const float CoreCube = 6f;
        public const float CoreSpacing = 5f;

        /// <summary>The exit chalk: one rail of <see cref="ChalkDots"/> plain dots, <see cref="ChalkHalfGap"/> u to the
        /// corner's inside of the exit line, from <see cref="ChalkFrom"/> to <see cref="ChalkTo"/> u down it. It starts
        /// beyond a full blast at the keg, so opening the keg never clears it. ONE rail, on the inside: the outside rail of
        /// the first cut sat on the ring-to-ring line (median 3-10 u from it over 400 seeds), in a mouth-threader's way.</summary>
        public const float ChalkFrom = 120f;
        public const float ChalkTo = 260f;
        public const float ChalkHalfGap = 70f;
        public const int ChalkDots = 6;
        public const float ChalkDotSize = 5f;

        /// <summary>No keg regrows or re-shields while a hull is this near (u): a full blast's reach plus a margin.</summary>
        public const float HullClear = 110f;
        /// <summary>No chalk dot regrows while a hull is this near (u).</summary>
        public const float ChalkHullClear = 60f;
        /// <summary>Corners deflecting less than this (deg) have no clear inside; the side is a hash of the ring.</summary>
        public const float StraightDegrees = 10f;

        /// <summary>The most mode prisms one lap may stand (8 kegs with chalk is 168).</summary>
        public const int MaxPrismsPerLap = 200;

        /// <summary>A full squeeze's blast radius (u): half the bomb's <c>maxBlastScale</c>.</summary>
        public static float FullReach => GrizzlyTimeCourse.MaxBlastScale * 0.5f;
        /// <summary>A tap's blast radius (u): half the bomb's <c>minBlastScale</c>.</summary>
        public static float TapReach => GrizzlyTimeCourse.MinBlastScale * 0.5f;

        // ── AI (the mode's aim only; the bombs stay the vessel's autopilot) ──

        /// <summary>An AI more than this far (u) from a keg flies the ring the ordinary way.</summary>
        public const float AIKegAimRange = 600f;
        /// <summary>The AI aims this far (u) past the keg down its exit lane, so it passes BESIDE the core (inside every
        /// credit radius and every full blast) instead of ramming the cage.</summary>
        public const float AIKegPassOffset = 40f;
        /// <summary>Within this distance (u) of its keg aim point with the keg still standing, the AI gives that keg up
        /// for this gate and threads the mouth.</summary>
        public const float AIKegGiveUp = 50f;
        /// <summary>Seconds without a gate before the AI is steered back to its ring (about 3x a gate's measured 6-8 s).</summary>
        public const float AIStallSeconds = 18f;
        /// <summary>The stall rescue's run-up: this far (u) back along the ring's axis, then through the mouth.</summary>
        public const float AIStallRunUp = 300f;
        public const float AIStallHandBack = 60f;

        // ── Types ──────────────────────────────────────────────────────────

        /// <summary>The keg beside ring <see cref="Ring"/> (0 .. lap-1).</summary>
        public readonly struct Keg
        {
            public readonly int Ring;
            public readonly Vector3 Centre;
            /// <summary>Unit: from the keg toward the next ring - the way a blast from the keg's far side throws you.</summary>
            public readonly Vector3 ExitDir;
            /// <summary>Unit, in the ring's plane: from the ring centre toward this corner's inside.</summary>
            public readonly Vector3 InsideNormal;

            public Keg(int ring, Vector3 centre, Vector3 exitDir, Vector3 insideNormal)
            {
                Ring = ring; Centre = centre; ExitDir = exitDir; InsideNormal = insideNormal;
            }
        }

        /// <summary>One prism of a keg or its chalk, in world space.</summary>
        public readonly struct Part
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public readonly PrismKind Kind;
            public readonly bool Core;

            public Part(Vector3 position, Quaternion rotation, Vector3 scale, PrismKind kind, bool core)
            {
                Position = position; Rotation = rotation; Scale = scale; Kind = kind; Core = core;
            }
        }

        // ── Placement ──────────────────────────────────────────────────────

        /// <summary>A stable value in [0,1) for ring <paramref name="ring"/> (integer hash: identical on every runtime).</summary>
        public static float Hash01(int ring)
        {
            unchecked
            {
                uint h = (uint)ring * 0x9E3779B1u + 0x7F4A7C15u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// The corner's inside at ring <paramref name="ring"/>, in the ring's plane: the bisector of the two legs, with its
        /// axis component removed. A corner that hardly turns (under <see cref="StraightDegrees"/>) has no inside, so the
        /// side across the axis is picked by <see cref="Hash01"/>.
        /// </summary>
        public static Vector3 InsideNormal(Vector3 prev, in RaceGate gate, Vector3 next, int ring)
        {
            Vector3 axis = RaceCourseGeometry.SafeNormalize(gate.Axis, Vector3.forward);
            Vector3 toPrev = RaceCourseGeometry.SafeNormalize(prev - gate.Position, -axis);
            Vector3 toNext = RaceCourseGeometry.SafeNormalize(next - gate.Position, axis);
            float deflection = 180f - Vector3.Angle(toPrev, toNext);
            Vector3 bisector = toPrev + toNext;
            Vector3 inPlane = bisector - Vector3.Dot(bisector, axis) * axis;
            if (deflection >= StraightDegrees && inPlane.sqrMagnitude > 1e-6f) return inPlane.normalized;

            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            return Hash01(ring) < 0.5f ? side : -side;
        }

        /// <summary>From the keg toward the next ring (unit).</summary>
        public static Vector3 ExitDirection(Vector3 kegCentre, Vector3 next, Vector3 fallback) =>
            RaceCourseGeometry.SafeNormalize(next - kegCentre, fallback);

        /// <summary>Every keg of one lap, one per ring, in ring order. The same at every intensity: the ladder is the
        /// keg's armour, its chalk and its credit, not where it stands.</summary>
        public static void KegsFor(IReadOnlyList<RaceGate> lap, List<Keg> into)
        {
            into.Clear();
            int n = lap.Count;
            if (n < 3) return;
            for (int k = 0; k < n; k++)
            {
                var gate = lap[k];
                Vector3 prev = lap[(k - 1 + n) % n].Position;
                Vector3 next = lap[(k + 1) % n].Position;
                Vector3 inside = InsideNormal(prev, gate, next, k);
                Vector3 fallback = RaceCourseGeometry.SafeNormalize(gate.Axis, Vector3.forward);

                Vector3 c = gate.Position + inside * KegOffset;
                into.Add(new Keg(k, c, ExitDirection(c, next, fallback), inside));
            }
        }

        /// <summary>The direction across the exit lane, in the plane of the keg's exit and the corner's inside.</summary>
        public static Vector3 Lateral(in Keg keg)
        {
            Vector3 lat = keg.InsideNormal - Vector3.Dot(keg.InsideNormal, keg.ExitDir) * keg.ExitDir;
            if (lat.sqrMagnitude > 1e-6f) return lat.normalized;
            return Vector3.Cross(keg.ExitDir, Mathf.Abs(keg.ExitDir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        }

        /// <summary>Lay one keg into <paramref name="into"/>: the core first (<see cref="CoreBlocks"/> cubes across the
        /// lane), then the hoop round it in the plane across <see cref="Keg.ExitDir"/>.</summary>
        public static void Build(in Keg keg, int intensity, List<Part> into)
        {
            into.Clear();
            Vector3 exit = keg.ExitDir;
            Vector3 across = Lateral(keg);
            Vector3 up = Vector3.Cross(exit, across).normalized;
            var coreRot = Quaternion.LookRotation(exit, up);
            var cube = Vector3.one * CoreCube;
            var coreKind = CoreKind(intensity);
            for (int b = 0; b < CoreBlocks; b++)
            {
                float offset = (b - (CoreBlocks - 1) * 0.5f) * CoreSpacing;
                into.Add(new Part(keg.Centre + across * offset, coreRot, cube, coreKind, core: true));
            }
            for (int s = 0; s < HoopSlabs; s++)
            {
                float a = s * Mathf.PI * 2f / HoopSlabs;
                Vector3 radial = across * Mathf.Cos(a) + up * Mathf.Sin(a);
                Vector3 tangent = -across * Mathf.Sin(a) + up * Mathf.Cos(a);
                into.Add(new Part(keg.Centre + radial * HoopRadius, Quaternion.LookRotation(tangent, radial),
                                  HoopSlabScale, PrismKind.Plain, core: false));
            }
        }

        /// <summary>A chalk dot keeps this far (u) outside every ring's mouth, so the safe line meets no chalk.</summary>
        public const float ChalkMouthClear = 25f;

        /// <summary>The exit chalk of one keg: a rail of plain dots on the inside of its exit line, skipping any dot that
        /// would crowd a ring's mouth in <paramref name="lap"/> (a short leg can bring the lane past another ring).</summary>
        public static void Chalk(in Keg keg, IReadOnlyList<RaceGate> lap, List<Part> into)
        {
            into.Clear();
            Vector3 across = Lateral(keg);
            var rot = Quaternion.LookRotation(keg.ExitDir, Vector3.Cross(keg.ExitDir, across).normalized);
            var scale = Vector3.one * ChalkDotSize;
            for (int d = 0; d < ChalkDots; d++)
            {
                float along = ChalkDots > 1 ? Mathf.Lerp(ChalkFrom, ChalkTo, d / (float)(ChalkDots - 1)) : ChalkFrom;
                Vector3 at = keg.Centre + keg.ExitDir * along + across * ChalkHalfGap;
                if (CrowdsAMouth(at, lap)) continue;
                into.Add(new Part(at, rot, scale, PrismKind.Plain, core: false));
            }
        }

        static bool CrowdsAMouth(Vector3 at, IReadOnlyList<RaceGate> lap)
        {
            for (int r = 0; r < lap.Count; r++)
            {
                float keep = lap[r].Radius + ChalkMouthClear + ChalkDotSize;
                if ((at - lap[r].Position).sqrMagnitude <= keep * keep) return true;
            }
            return false;
        }

        /// <summary>Prisms one keg stands.</summary>
        public static int PrismsPerKeg => CoreBlocks + HoopSlabs;
        /// <summary>The most chalk dots one keg stands (fewer where a dot would crowd a mouth).</summary>
        public static int ChalkPerKeg => ChalkDots;

        /// <summary>Mode prisms standing on one lap of <paramref name="gates"/> rings at <paramref name="intensity"/>.</summary>
        public static int PrismsPerLap(int intensity, int gates = GrizzlyTimeCourse.GatesPerLap)
        {
            int perKeg = PrismsPerKeg + (HasChalk(intensity) ? ChalkPerKeg : 0);
            return gates * perKeg;
        }

        // ── Reading a keg ──────────────────────────────────────────────────

        /// <summary>Where to stand to blow a keg so its blast throws you down its exit lane: behind the core, opposite the
        /// next ring, <paramref name="standOff"/> u out (inside a full blast).</summary>
        public static Vector3 BlowPoint(in Keg keg, float standOff = 60f) => keg.Centre - keg.ExitDir * standOff;

        /// <summary>
        /// Was a keg breaking beside the hull a BANK SHOT? The hull was inside a full blast of the keg and the launch it got
        /// (<paramref name="shiftDir"/>: the transformer's world-space push) points within <paramref name="maxDeg"/> of the
        /// keg's exit lane. The local feedback's test (the bank-shot toast and sound); it scores nothing.
        /// </summary>
        public static bool IsBankShot(Vector3 kegPos, Vector3 hullPos, Vector3 shiftDir, Vector3 exitDir, float maxDeg = 30f)
        {
            if ((hullPos - kegPos).sqrMagnitude > FullReach * FullReach) return false;
            if (shiftDir.sqrMagnitude < 1f || exitDir.sqrMagnitude < 1e-6f) return false;
            return Vector3.Angle(shiftDir, exitDir) <= maxDeg;
        }

        /// <summary>Where a full throw from <paramref name="keg"/> lands you: its blow point plus one carry down its lane.</summary>
        public static Vector3 ThrowLanding(in Keg keg) =>
            BlowPoint(keg) + keg.ExitDir * GrizzlyTimeCourse.CarryAfterLaunch(1f);
    }
}
