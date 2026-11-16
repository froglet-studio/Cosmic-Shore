using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What an AI Shrike is aiming at this frame (<see cref="ThornlineAIPlan.Choose"/>).</summary>
    public enum ThornlineAim
    {
        None = 0,      // finished, or no course
        Dock = 1,      // the thorn behind the next ring, on the seat's lane
        Snipe = 2,     // the thorn after next, through both rings
        Crystal = 3,   // the next thorn, through its rim crystal
        Back = 4,      // the leg's start thorn (a short sling, or the gun)
        Hold = 5,      // the next thorn is broken and nothing else is in reach: wait on its spot
        Rescue = 6,    // no ring for StallSeconds: the nearest live thorn in sling range
    }

    /// <summary>One difficulty's AI skill in Thornline, authored on the controller (index Easy..Legendary).</summary>
    public struct ThornlineAIRow
    {
        public float NoseLeadGain;     // AIPilot.NoseLeadGain while racing (0 = off)
        public float NoseFollow;       // VesselTransformer.AINoseFollowPerSecond (-1 = the fleet lag)
        public bool Snipes;            // takes the snipes (I3+)
        public bool Crystals;          // aims through live rim crystals (I2+)
    }

    /// <summary>
    /// The AI Shrike's choice of aim in Thornline, a pure function of the course and the pilot (THORNLINE.md §4). The
    /// autopilot slings when the weapons' axis finds a block within 20 degrees of what this returns
    /// (<c>ShrikeExecutor.Autopilot</c>), so the plan returns a point the sling ANCHORS on:
    /// <list type="bullet">
    /// <item>never a thorn's centre: a <b>lane</b> point 8.5 u off it (<see cref="ThornlineCourse.LanePoint"/>), inside
    /// the 12 u the sling catches and outside the 7 u the autopilot counts toward its lance, so the course itself can
    /// never pull an AI out of Time mode, and two AIs on one leg fly different lines (a follower does not dock on the
    /// leader's sling trail);</item>
    /// <item>in order: a snipe (Hard and up), a shot through a live rim crystal (the row says), the next thorn, the
    /// leg's start thorn when the next one is not on a ring's line, and the nearest live thorn after
    /// <see cref="StallSeconds"/> without a ring;</item>
    /// <item>a broken thorn is never a target (it regrows; the AI holds on its spot meanwhile).</item>
    /// </list>
    /// The harness flies this same code through a replica of the AI's aim law (<c>Tools/Build/thornline_course_harness</c>,
    /// <c>run.sh --ai</c>), so what it measures is what ships.
    /// </summary>
    public static class ThornlineAIPlan
    {
        /// <summary>Seconds without a ring before an AI is steered at the nearest live thorn in range.</summary>
        public const float StallSeconds = 15f;

        /// <summary>How long a stalled AI is pointed at the nearest live thorn before its stall clock starts again.</summary>
        public const float RescueSeconds = 6f;

        // ── The shipped rows, by difficulty (index 0 Easy .. 3 Legendary). The controller's serialized arrays default to
        // these, the generator writes them into the scene, and the harness flies them (run.sh --ai).
        //
        // Difficulty is AIM and READING, never a delay: the autopilot detaches the moment it is reloaded after 3 s docked
        // (aiDockSeconds), and a plain thorn's reload is 3.1 s, so an AI told to wait after its reload leaves every plain
        // thorn and aims at the undocked third of the turn rate (measured, run.sh --ai, 2026-11-17). With no nose lead
        // (the shipped law) the cubic stick closes the last degree onto a 12 u disc at 1,000 u so slowly that 14 of 20
        // I1 races finish (median 216 s) and 4 of 20 at I4. ──
        public static readonly float[] NoseLeadByDifficulty = { 2.5f, 3f, 8f, 16f };
        public static readonly float[] NoseFollowByDifficulty = { -1f, -1f, -1f, -1f };
        /// <summary>How far past the aim the nose lead may put the command (AIPilot.NoseLeadMaxExtraDegrees). Tight, so the
        /// lead sharpens the last few degrees onto a 12 u disc at 1,000 u (at the fleet's 40 the replica's Hard lost its
        /// lead over Medium at I2, run.sh --ai).</summary>
        public static float NoseLeadMaxExtraDegrees = 10f;
        public const int SnipesFromDifficulty = 3;     // Hard
        public const int CrystalsFromDifficulty = 4;   // Legendary

        /// <summary>The row for difficulty <paramref name="level"/> (1 Easy .. 4 Legendary; anything else is Medium).</summary>
        public static ThornlineAIRow RowFor(int level, float[] lead = null, float[] follow = null,
                                            int snipesFrom = SnipesFromDifficulty, int crystalsFrom = CrystalsFromDifficulty)
        {
            if (level < 1 || level > 4) level = 2;
            static float At(float[] xs, float[] fallback, int i) => xs != null && i < xs.Length ? xs[i] : fallback[i];
            return new ThornlineAIRow
            {
                NoseLeadGain = At(lead, NoseLeadByDifficulty, level - 1),
                NoseFollow = At(follow, NoseFollowByDifficulty, level - 1),
                Snipes = level >= snipesFrom,
                Crystals = level >= crystalsFrom,
            };
        }

        public struct Inputs
        {
            public IReadOnlyList<Vector3> Thorns;       // world, thorn 0 is the start thorn
            public IReadOnlyList<float> Sizes;          // cube side per thorn
            public IReadOnlyList<bool> Alive;           // still standing (a lance breaks a plain thorn)
            public IReadOnlyList<RaceGate> Rings;       // world; ring k sits on the leg thorn k -> thorn k+1
            public IReadOnlyList<Vector3> Crystals;     // world
            public IReadOnlyList<int> CrystalLegs;      // the leg (its far thorn's index) each crystal sits on
            public IReadOnlyList<bool> CrystalLive;
            public Vector3 Self;
            public int Threaded;                        // rings threaded so far
            public int Seat;                            // spreads the lanes
            public ThornlineAIRow Row;
            public bool Stalled;                        // no ring for StallSeconds
        }

        /// <summary>The seat's lane on a leg: spread by seat, turned per leg by an integer hash, so no seat owns the same
        /// side of every thorn and two seats never share a line.</summary>
        public static int SeatLane(int seat, int leg) =>
            (int)((Hash((uint)leg) + (uint)(seat * 3)) % ThornlineCourse.LaneCount);

        static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352d;
                x ^= x >> 15; x *= 0x846ca68b;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>The first clear lane of thorn <paramref name="target"/> from <paramref name="from"/>, starting at the
        /// seat's (<see cref="ThornlineCourse.LaneClear"/>), or the seat's lane when none is clear.</summary>
        public static Vector3 Lane(Vector3 from, in Inputs input, int target)
        {
            int first = SeatLane(input.Seat, target);
            var avoid = CrystalsOn(input, target);
            for (int i = 0; i < ThornlineCourse.LaneCount; i++)
            {
                Vector3 p = ThornlineCourse.LanePoint(from, input.Thorns[target], first + i);
                if (ThornlineCourse.LaneClear(from, p, input.Thorns, target, avoid)) return p;
            }
            return ThornlineCourse.LanePoint(from, input.Thorns[target], first);
        }

        static readonly List<Vector3> s_avoid = new(4);

        static List<Vector3> CrystalsOn(in Inputs input, int leg)
        {
            s_avoid.Clear();
            if (input.Crystals == null) return s_avoid;
            for (int i = 0; i < input.Crystals.Count; i++)
                if (input.CrystalLegs[i] == leg && input.CrystalLive[i]) s_avoid.Add(input.Crystals[i]);
            return s_avoid;
        }

        /// <summary>The point the line from <paramref name="self"/> through the crystal reaches at the target thorn's
        /// depth, if a sling down it anchors on that thorn (within <see cref="ThornlineCourse.AnchorReach"/> - 1).</summary>
        public static bool TryCrystalShot(Vector3 self, Vector3 crystal, Vector3 thorn, out Vector3 aim)
        {
            aim = default;
            Vector3 toCrystal = crystal - self;
            float along = Vector3.Dot(thorn - self, RaceCourseGeometry.SafeNormalize(toCrystal, Vector3.forward));
            float c = toCrystal.magnitude;
            if (c < 1f || along <= c) return false;
            aim = self + toCrystal * (along / c);
            return (aim - thorn).magnitude <= ThornlineCourse.AnchorReach - 1f;
        }

        public static ThornlineAim Choose(in Inputs input, out Vector3 target)
        {
            target = default;
            var thorns = input.Thorns;
            var rings = input.Rings;
            if (thorns == null || rings == null || rings.Count == 0 || thorns.Count < 2) return ThornlineAim.None;
            int ring = input.Threaded;
            if (ring < 0 || ring >= rings.Count) return ThornlineAim.None;
            int next = ring + 1;
            if (next >= thorns.Count) return ThornlineAim.None;
            Vector3 self = input.Self;
            float range = ThornlineCourse.SlingRange - 30f;

            if (input.Stalled)
            {
                // Nearest live thorn ahead first, then the one behind: any sling that moves the AI on the chain.
                for (int o = 0; o < 3; o++)
                {
                    int k = o == 0 ? next : o == 1 ? ring : next + 1;
                    if (k < 0 || k >= thorns.Count || !input.Alive[k]) continue;
                    float d = (thorns[k] - self).magnitude;
                    if (d < ThornlineCourse.AiSlingMinDistance || d > range) continue;
                    target = Lane(self, input, k);
                    return ThornlineAim.Rescue;
                }
            }

            if (input.Row.Snipes && next + 1 < thorns.Count && ring + 1 < rings.Count && input.Alive[next + 1])
            {
                Vector3 lane = Lane(self, input, next + 1);
                float d = (lane - self).magnitude;
                float nearClear = ThornlineCourse.HalfDiagonal(input.Sizes[next]) + ThornlineCourse.HullRadius + 4f;
                if (d <= range && d >= ThornlineCourse.AiSlingMinDistance
                    && Threads(self, lane, rings[ring]) && Threads(self, lane, rings[ring + 1])
                    && ThornlineCourse.DistanceToSegment(thorns[next], self, lane) > nearClear)
                {
                    target = lane;
                    return ThornlineAim.Snipe;
                }
            }

            if (input.Row.Crystals && input.Alive[next] && input.Crystals != null)
                for (int i = 0; i < input.Crystals.Count; i++)
                {
                    if (input.CrystalLegs[i] != next || !input.CrystalLive[i]) continue;
                    if (TryCrystalShot(self, input.Crystals[i], thorns[next], out Vector3 shot) && Threads(self, shot, rings[ring]))
                    {
                        target = shot;
                        return ThornlineAim.Crystal;
                    }
                }

            if (input.Alive[next])
            {
                Vector3 lane = Lane(self, input, next);
                if (Threads(self, lane, rings[ring]))
                {
                    target = lane;
                    return ThornlineAim.Dock;
                }
            }

            if (input.Alive[ring] && (thorns[ring] - self).magnitude >= ThornlineCourse.AiSlingMinDistance)
            {
                target = Lane(self, input, ring);
                return ThornlineAim.Back;
            }

            if (input.Alive[next])
            {
                target = Lane(self, input, next);
                return ThornlineAim.Dock;
            }

            target = thorns[next];
            return ThornlineAim.Hold;
        }

        /// <summary>The line from <paramref name="from"/> to <paramref name="to"/> passes the ring inside its mouth (with
        /// the hull's room) and in its direction.</summary>
        public static bool Threads(Vector3 from, Vector3 to, in RaceGate ring) =>
            ThornlineCourse.DistanceToSegment(ring.Position, from, to) <= ring.Radius - ThornlineCourse.HullRadius - 2f
            && Vector3.Dot(to - from, ring.Axis) > 0f;
    }
}
