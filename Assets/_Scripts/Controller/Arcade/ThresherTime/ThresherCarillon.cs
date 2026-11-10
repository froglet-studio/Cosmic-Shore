using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Carillon: Thresher Time's bells that ring together (2026-11-11, round 5 of the Time games). Pure and
    /// allocation-free, so every peer and the offline model (<c>Tools/Build/thresher_carillon_model.py</c>) agree.
    ///
    /// <para><b>Unison pairs.</b> On every lap, the two gentlest corners each hang their ring and the next one in
    /// UNISON: the same swing, on the same beat, under one yoke (a beam of prisms between their pivots,
    /// <see cref="ThresherBelfry"/>). Two rings that swing together keep the same line between them, so a pilot who
    /// plants on that line can ring both on ONE aimed launch: a <b>double peal</b>. Reading which rings are yoked, and
    /// planting for the double instead of for the next ring, is the skill the course rewards (the offline model measures
    /// what it saves). Everything is a function of the broadcast course, with no random draw.</para>
    ///
    /// <para><b>A peal</b> is a ring threaded at sling speed (<see cref="PealSpeed"/>, twice the Thresher's cruise): the
    /// verb, not a cruise. It is the local pilot's feedback only (a sound, the ring's punch, a streak); scoring is the
    /// race's, unchanged. Nothing about the vessel changes (CLAUDE.md, "A game mode never changes the vessel").</para>
    /// </summary>
    public static class ThresherCarillon
    {
        /// <summary>Unison pairs a lap, intensity 1-4. Two at every rung: taught at I1, the same reward at I4.</summary>
        public static readonly int[] PairsPerLapByIntensity = { 2, 2, 2, 2 };

        /// <summary>A thread at or above this speed (u/s) is a peal: twice the Thresher's 125 u/s cruise, so only a
        /// sling (a plain let-go above it, or an aimed launch) rings a bell.</summary>
        public const float PealSpeed = 250f;

        /// <summary>Two peals with no plant between them and at most this many seconds apart are ONE launch: a double.
        /// An aimed launch crosses a 600 u leg in well under a second; the window allows for a slow plain let-go.</summary>
        public const float DoubleWindowSeconds = 2.5f;

        /// <summary>A peal streak is announced at every multiple of this (4, 8, 12, 16).</summary>
        public const int StreakStep = 4;

        public static int PairsPerLap(int intensity) =>
            PairsPerLapByIntensity[Mathf.Clamp(intensity, 1, PairsPerLapByIntensity.Length) - 1];

        /// <summary>The turn (degrees) the course makes AT gate <paramref name="i"/>: between the leg into it and the leg
        /// out of it. A double through ring i into ring i+1 asks the pilot to come in on the line out of i, so it costs
        /// about this much off the line in; the gentlest corners are the pairs.</summary>
        public static float TurnDegrees(IReadOnlyList<RaceGate> course, int i)
        {
            int n = course.Count;
            Vector3 p = course[(i - 1 + n) % n].Position, g = course[i].Position, q = course[(i + 1) % n].Position;
            Vector3 a = g - p, b = q - g;
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return 180f;
            return Vector3.Angle(a, b);
        }

        /// <summary>
        /// The first ring of each unison pair on a lap of <paramref name="course"/>, ascending: the gates with the gentlest
        /// turns, taken in order of turn (ties by index), skipping any whose pair would share a ring with one already
        /// taken. A pair (i, i+1) wraps from the last ring to the first. Closed form.
        /// </summary>
        public static void PairsFor(IReadOnlyList<RaceGate> course, int intensity, List<int> into)
        {
            into.Clear();
            int n = course?.Count ?? 0;
            if (n < 4) return;
            int want = Mathf.Min(PairsPerLap(intensity), n / 2);
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);
            var turns = new float[n];
            for (int i = 0; i < n; i++) turns[i] = TurnDegrees(course, i);
            order.Sort((x, y) => turns[x] != turns[y] ? turns[x].CompareTo(turns[y]) : x.CompareTo(y));
            var used = new bool[n];
            foreach (int i in order)
            {
                if (into.Count >= want) break;
                int j = (i + 1) % n;
                if (used[i] || used[j]) continue;
                used[i] = used[j] = true;
                into.Add(i);
            }
            into.Sort();
        }

        /// <summary>Is <paramref name="ring"/> the FIRST ring of a unison pair in <paramref name="pairs"/>?</summary>
        public static bool IsPairFirst(IReadOnlyList<int> pairs, int ring)
        {
            if (pairs == null) return false;
            for (int k = 0; k < pairs.Count; k++)
                if (pairs[k] == ring) return true;
            return false;
        }

        /// <summary>Is <paramref name="ring"/> the SECOND ring of a unison pair (it swings on its partner's beat)?</summary>
        public static bool IsPairSecond(IReadOnlyList<int> pairs, int ring, int ringsPerLap)
        {
            if (pairs == null || ringsPerLap <= 0) return false;
            for (int k = 0; k < pairs.Count; k++)
                if ((pairs[k] + 1) % ringsPerLap == ring) return true;
            return false;
        }

        /// <summary>
        /// Does a straight line from <paramref name="from"/> to ring B's centre thread ring A on the way, at least
        /// <paramref name="margin"/> inside A's mouth? The AI's test before it aims a launch through a unison pair: A
        /// must lie between (its plane crossed between the start and B), and the crossing must be inside its rim.
        /// </summary>
        public static bool ThreadsBoth(Vector3 from, Vector3 aCentre, Vector3 aAxis, float aRadius, Vector3 bCentre,
                                       float margin)
        {
            Vector3 dir = bCentre - from;
            Vector3 axis = aAxis.sqrMagnitude > 1e-6f ? aAxis.normalized : Vector3.forward;
            float across = Vector3.Dot(dir, axis);
            if (Mathf.Abs(across) < 1e-4f) return false;               // the line runs along A's plane
            float s = Vector3.Dot(aCentre - from, axis) / across;
            if (s <= 0f || s >= 1f) return false;                       // A is not between the start and B
            Vector3 crossing = from + dir * s;
            float inside = Mathf.Max(0f, aRadius - margin);
            return (crossing - aCentre).sqrMagnitude <= inside * inside;
        }

        /// <summary>
        /// The AI's double (plan step <c>time-arcade</c>; input only: the answer is the objective its aimed launch is
        /// pointed at). With a launch coming at <paramref name="ring"/>, the first ring of a unison pair, find where the
        /// pair's second ring will be when a launch at <paramref name="speed"/> reaches it (a short fixed-point solve; a
        /// ring swings far slower than a launch), and aim there if the straight line from <paramref name="from"/> threads
        /// the first ring at <paramref name="ledFirst"/> (where it will be as the launch passes) by
        /// <paramref name="margin"/>. False: aim at the first ring as before.
        /// </summary>
        public static bool TryThroughAim(ThresherBelfry.Layout layout, int ring, Vector3 from, Vector3 ledFirst, float speed,
                                         float now, float margin, out Vector3 through)
        {
            through = default;
            int n = layout?.Rings.Count ?? 0;
            if (n < 2 || ring < 0 || ring >= n || !IsPairFirst(layout.Pairs, ring)) return false;
            var second = layout.Rings[(ring + 1) % n];
            float v = Mathf.Max(1f, speed);
            Vector3 at = second.BobAt(now);
            for (int k = 0; k < 3; k++) at = second.BobAt(now + (at - from).magnitude / v);
            var first = layout.Gates[ring];
            if (!ThreadsBoth(from, ledFirst, first.Axis, first.Radius, at, margin)) return false;
            through = at;
            return true;
        }

        /// <summary>
        /// The local pilot's peal state (feedback only, never replicated): fed every thread with the hull's speed and
        /// whether it planted since the last one; answers whether this thread pealed, how many bells this launch has rung
        /// (2 = a double), and the streak of peals in a row.
        /// </summary>
        public sealed class PealTracker
        {
            public int Streak { get; private set; }
            public int OnThisLaunch { get; private set; }
            public int Doubles { get; private set; }
            float _lastPealAt = float.NegativeInfinity;

            public void Reset()
            {
                Streak = OnThisLaunch = Doubles = 0;
                _lastPealAt = float.NegativeInfinity;
            }

            /// <summary>One forward thread at <paramref name="time"/>. Returns true when it pealed.</summary>
            public bool OnThread(float time, float speed, bool plantedSinceLastThread)
            {
                if (speed < PealSpeed)
                {
                    Streak = 0;
                    OnThisLaunch = 0;
                    _lastPealAt = float.NegativeInfinity;
                    return false;
                }
                bool sameLaunch = !plantedSinceLastThread && OnThisLaunch > 0 && time - _lastPealAt <= DoubleWindowSeconds;
                OnThisLaunch = sameLaunch ? OnThisLaunch + 1 : 1;
                if (OnThisLaunch == 2) Doubles++;
                Streak++;
                _lastPealAt = time;
                return true;
            }

            /// <summary>Announce the streak now? At every <see cref="StreakStep"/>th peal in a row.</summary>
            public bool StreakMilestone => Streak > 0 && Streak % StreakStep == 0;
        }
    }
}
