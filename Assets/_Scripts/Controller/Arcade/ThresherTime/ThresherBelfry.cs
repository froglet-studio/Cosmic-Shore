using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Belfry: Thresher Time's clockwork course (Yash, 2026-11-07: "it should be about the time element"). Every
    /// ring hangs on a chain from a pivot further out from the cell's centre and SWINGS across the flight line, so a pilot
    /// times the sling to meet it. Morning-star MACES swing across the straights, and at the top intensity two danger
    /// SHUTTERS slide across every mouth on their own beat.
    ///
    /// <para><b>Closed form, on the shared clock.</b> Everything here is a pure function of the course, the intensity
    /// and the server time, like Switchback's movers: every peer lays the same prisms and poses them the same way each
    /// frame, and nothing is replicated. The ring of a gate is still the gate (the race counts a thread through its live
    /// mouth, the objective arrow follows it), it just does not stand still.</para>
    ///
    /// <para><b>Why a clock is the Time element here.</b> The fleet's Time is rate and mobility; the Thresher's Time
    /// scales how fast its spin winds up to launch speed (ElementalAbilityMaps/Thresher, the Plant). On a course that
    /// moves on a beat, winding up sooner is catching a window a slower pilot misses. Nothing about the vessel changes
    /// (CLAUDE.md, "A game mode never changes the vessel"): the clock is the track's.</para>
    /// </summary>
    public static class ThresherBelfry
    {
        /// <summary>One intensity's clockwork.</summary>
        public readonly struct Spec
        {
            public readonly float RingLength, RingSwingDeg, RingPeriod;
            /// <summary>A mace on every Nth leg (0 = none) and how many on such a leg.</summary>
            public readonly int MaceEveryNthLeg, MacesPerLeg;
            public readonly float MaceLength, MaceSwingDeg, MacePeriod;
            public readonly bool MaceDanger;
            /// <summary>0 = no shutters; otherwise the shutters' open-close period (s).</summary>
            public readonly float ShutterPeriod;

            public Spec(float ringLength, float ringSwingDeg, float ringPeriod, int maceEveryNthLeg, int macesPerLeg,
                        float maceLength, float maceSwingDeg, float macePeriod, bool maceDanger, float shutterPeriod)
            {
                RingLength = ringLength; RingSwingDeg = ringSwingDeg; RingPeriod = ringPeriod;
                MaceEveryNthLeg = maceEveryNthLeg; MacesPerLeg = macesPerLeg;
                MaceLength = maceLength; MaceSwingDeg = maceSwingDeg; MacePeriod = macePeriod; MaceDanger = maceDanger;
                ShutterPeriod = shutterPeriod;
            }

            public bool HasMaces => MaceEveryNthLeg > 0 && MacesPerLeg > 0;
            public bool HasShutters => ShutterPeriod > 0f;
        }

        /// <summary>
        /// The ladder. I1: gentle swings and an open course. I2: faster swings, a plain mace on every other straight.
        /// I3: a danger-headed mace on every straight. I4: the fastest swings, two maces a straight swinging against
        /// each other, and danger shutters across every mouth. The ring's top speed (length x swing x 2 pi / period) stays
        /// under the Thresher's 125 u/s cruise at every rung, so a pilot can always chase a ring down.
        /// </summary>
        public static Spec SpecFor(int intensity) => Mathf.Clamp(intensity, 1, 4) switch
        {
            1 => new Spec(200f, 12f, 9f, 0, 0, 150f, 35f, 6f, false, 0f),
            2 => new Spec(190f, 20f, 7.5f, 2, 1, 150f, 35f, 6f, false, 0f),
            3 => new Spec(175f, 28f, 6f, 1, 1, 150f, 40f, 5f, true, 0f),
            _ => new Spec(160f, 34f, 5f, 1, 2, 150f, 45f, 4.2f, true, 3.5f),
        };

        // Sizes (u). A chain link every LinkSpacing along a chain, a mace head of a core and six spikes, a shutter bar of
        // cubes along the mouth's diameter.
        public const float LinkSpacing = 18f;
        public const float LinkCube = 3.5f;
        public const float HeadCube = 10f;
        public const float SpikeCube = 5f;
        public const float SpikeReach = 12f;
        public const float ShutterCube = 6f;
        public const float ShutterSpacing = 8f;
        /// <summary>How far a mace head reaches from its centre: the spike's centre plus half its cube.</summary>
        public const float HeadReach = SpikeReach + SpikeCube * 0.5f;
        /// <summary>Where along a straight a mace hangs: away from both gates.</summary>
        static readonly float[] MaceFractions1 = { 0.5f };
        static readonly float[] MaceFractions2 = { 0.35f, 0.65f };
        const float GoldenAngle = 2.39996323f;

        /// <summary>A pendulum: hangs from <see cref="Pivot"/> down <see cref="Up"/> by <see cref="Length"/> and swings
        /// along <see cref="Side"/>. At angle 0 the bob is exactly <c>Pivot - Up * Length</c> (for a ring: the gate).</summary>
        public readonly struct Pendulum
        {
            public readonly Vector3 Pivot, Up, Side, Forward;
            public readonly float Length, Swing, Omega, Phase;
            /// <summary>The gate this pendulum carries (rings) or the leg it hangs over (maces).</summary>
            public readonly int Gate;

            public Pendulum(int gate, Vector3 pivot, Vector3 up, Vector3 side, Vector3 forward, float length, float swingRad,
                            float omega, float phase)
            {
                Gate = gate; Pivot = pivot; Up = up; Side = side; Forward = forward;
                Length = length; Swing = swingRad; Omega = omega; Phase = phase;
            }

            public float AngleAt(float seconds) => Swing * Mathf.Sin(Omega * seconds + Phase);

            public Vector3 BobAt(float seconds)
            {
                float a = AngleAt(seconds);
                return Pivot + Length * (-Up * Mathf.Cos(a) + Side * Mathf.Sin(a));
            }

            public Quaternion Frame => Quaternion.LookRotation(Forward, Up);
        }

        public enum Role : byte { RingChain = 0, MaceChain = 1, MaceHead = 2, Shutter = 3 }

        /// <summary>One prism of the clockwork, posed by <see cref="PoseAt"/>.</summary>
        public readonly struct Part
        {
            public readonly Role Role;
            /// <summary>Index into <see cref="Layout.Rings"/> (ring chain, shutter) or <see cref="Layout.Maces"/>.</summary>
            public readonly int Group;
            /// <summary>Chain: distance from the pivot. Head: offset in the pendulum's (side, up, forward) frame.
            /// Shutter: x = which bar (-1 / +1), y = height along the mouth.</summary>
            public readonly Vector3 Local;
            public readonly float Size;
            public readonly PrismKind Kind;

            public Part(Role role, int group, Vector3 local, float size, PrismKind kind)
            {
                Role = role; Group = group; Local = local; Size = size; Kind = kind;
            }
        }

        public sealed class Layout
        {
            public int Intensity;
            public Spec Spec;
            public readonly List<RaceGate> Gates = new();
            public readonly List<Pendulum> Rings = new();
            public readonly List<Pendulum> Maces = new();
            public readonly List<Part> Parts = new();
            public readonly List<float> ShutterPhases = new();

            public void Clear()
            {
                Gates.Clear(); Rings.Clear(); Maces.Clear(); Parts.Clear(); ShutterPhases.Clear();
            }
        }

        /// <summary>Build the clockwork for a closed lap of <paramref name="course"/> gates. Closed form.</summary>
        public static void Build(IReadOnlyList<RaceGate> course, int intensity, Layout into)
        {
            into.Clear();
            into.Intensity = Mathf.Clamp(intensity, 1, 4);
            var spec = into.Spec = SpecFor(intensity);
            int n = course?.Count ?? 0;
            if (n == 0) return;

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < n; i++) centre += course[i].Position;
            centre /= n;

            float ringOmega = 2f * Mathf.PI / spec.RingPeriod;
            for (int i = 0; i < n; i++)
            {
                var gate = course[i];
                into.Gates.Add(gate);
                Vector3 forward = gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis.normalized : Vector3.forward;
                Vector3 up = OutwardUp(gate.Position, centre, forward);
                Vector3 side = Vector3.Cross(forward, up).normalized;
                float phase = Mathf.Repeat(i * GoldenAngle, 2f * Mathf.PI);
                var ring = new Pendulum(i, gate.Position + up * spec.RingLength, up, side, forward, spec.RingLength,
                                        spec.RingSwingDeg * Mathf.Deg2Rad, ringOmega, phase);
                into.Rings.Add(ring);
                AddChain(into, Role.RingChain, into.Rings.Count - 1, spec.RingLength - gate.Radius - LinkCube);
                into.ShutterPhases.Add(Mathf.Repeat(phase + 1.3f, 2f * Mathf.PI));
                if (spec.HasShutters) AddShutters(into, i, gate.Radius);
            }

            if (!spec.HasMaces) return;
            float maceOmega = 2f * Mathf.PI / spec.MacePeriod;
            var fractions = spec.MacesPerLeg >= 2 ? MaceFractions2 : MaceFractions1;
            var kind = spec.MaceDanger ? PrismKind.Danger : PrismKind.Plain;
            for (int i = 0; i < n; i++)
            {
                if (i % spec.MaceEveryNthLeg != 0) continue;
                Vector3 a = course[i].Position, b = course[(i + 1) % n].Position;
                Vector3 forward = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector3.forward;
                for (int k = 0; k < fractions.Length; k++)
                {
                    Vector3 hang = Vector3.Lerp(a, b, fractions[k]);
                    Vector3 up = OutwardUp(hang, centre, forward);
                    Vector3 side = Vector3.Cross(forward, up).normalized;
                    // Two maces on one straight swing AGAINST each other: half a cycle apart.
                    float phase = Mathf.Repeat(i * GoldenAngle + 1.1f + k * Mathf.PI, 2f * Mathf.PI);
                    into.Maces.Add(new Pendulum(i, hang + up * spec.MaceLength, up, side, forward, spec.MaceLength,
                                                spec.MaceSwingDeg * Mathf.Deg2Rad, maceOmega, phase));
                    int group = into.Maces.Count - 1;
                    AddChain(into, Role.MaceChain, group, spec.MaceLength - HeadReach - LinkCube);
                    into.Parts.Add(new Part(Role.MaceHead, group, Vector3.zero, HeadCube, kind));
                    AddSpike(into, group, Vector3.right); AddSpike(into, group, Vector3.left);
                    AddSpike(into, group, Vector3.up); AddSpike(into, group, Vector3.down);
                    AddSpike(into, group, Vector3.forward); AddSpike(into, group, Vector3.back);
                }
            }
        }

        static void AddSpike(Layout into, int group, Vector3 dir) =>
            into.Parts.Add(new Part(Role.MaceHead, group, dir * SpikeReach, SpikeCube, PrismKind.Plain));

        static void AddChain(Layout into, Role role, int group, float reach)
        {
            for (float d = LinkSpacing; d <= reach; d += LinkSpacing)
                into.Parts.Add(new Part(role, group, new Vector3(0f, 0f, d), LinkCube, PrismKind.Plain));
        }

        static void AddShutters(Layout into, int gate, float radius)
        {
            float half = radius - ShutterCube;
            for (int bar = -1; bar <= 1; bar += 2)
                for (float y = -half; y <= half + 1e-3f; y += ShutterSpacing)
                    into.Parts.Add(new Part(Role.Shutter, gate, new Vector3(bar, y, 0f), ShutterCube, PrismKind.Danger));
        }

        /// <summary>Away from the centre, square to <paramref name="forward"/>; any square direction if the gate points
        /// straight out.</summary>
        static Vector3 OutwardUp(Vector3 at, Vector3 centre, Vector3 forward)
        {
            Vector3 radial = at - centre;
            Vector3 up = radial - Vector3.Dot(radial, forward) * forward;
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.right, forward);
            return up.normalized;
        }

        /// <summary>0 = shut (the bars meet near the middle), 1 = open (the bars at the rim).</summary>
        public static float ShutterOpen(Layout layout, int gate, float seconds)
        {
            if (!layout.Spec.HasShutters) return 1f;
            float w = 2f * Mathf.PI / layout.Spec.ShutterPeriod;
            return 0.5f + 0.5f * Mathf.Sin(w * seconds + layout.ShutterPhases[gate]);
        }

        /// <summary>The fraction of a shutter's travel the bars stay out from the middle when shut.</summary>
        public const float ShutterMinOpen = 0.15f;

        /// <summary>The gate at <paramref name="seconds"/>: the ring's live centre, its axis and mouth unchanged.</summary>
        public static RaceGate GateAt(Layout layout, int gate, float seconds)
        {
            var g = layout.Gates[gate];
            return new RaceGate(layout.Rings[gate].BobAt(seconds), g.Axis, g.Radius);
        }

        /// <summary>Where <paramref name="part"/> stands at <paramref name="seconds"/>.</summary>
        public static void PoseAt(Layout layout, in Part part, float seconds, out Vector3 position, out Quaternion rotation)
        {
            switch (part.Role)
            {
                case Role.RingChain:
                case Role.MaceChain:
                {
                    var p = part.Role == Role.RingChain ? layout.Rings[part.Group] : layout.Maces[part.Group];
                    Vector3 bob = p.BobAt(seconds);
                    Vector3 down = (bob - p.Pivot) / p.Length;
                    position = p.Pivot + down * part.Local.z;
                    rotation = Quaternion.LookRotation(down, p.Forward);
                    return;
                }
                case Role.MaceHead:
                {
                    var p = layout.Maces[part.Group];
                    Vector3 bob = p.BobAt(seconds);
                    position = bob + p.Side * part.Local.x + p.Up * part.Local.y + p.Forward * part.Local.z;
                    rotation = p.Frame;
                    return;
                }
                default:
                {
                    var p = layout.Rings[part.Group];
                    float radius = layout.Gates[part.Group].Radius;
                    float open = ShutterOpen(layout, part.Group, seconds);
                    float x = part.Local.x * radius * Mathf.Lerp(ShutterMinOpen, 1f, open);
                    position = p.BobAt(seconds) + p.Side * x + p.Up * part.Local.y;
                    rotation = p.Frame;
                    return;
                }
            }
        }
    }
}
