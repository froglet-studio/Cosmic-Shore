using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What an attached autopilot Urchin should do with the rail it is riding.</summary>
    public enum UrchinRailVerdict
    {
        /// <summary>Keep riding the way the rail is already carrying the pilot.</summary>
        Ride = 0,
        /// <summary>Swing the nose round: the objective is back the way the rail came.</summary>
        Reverse = 1,
        /// <summary>This rail does not go there. Slip off and fly.</summary>
        Leave = 2,
    }

    /// <summary>
    /// A rail's ride points in INDEX ORDER, as the assessment reads them. A hole (a pooled-away,
    /// destroyed-and-reused or null prism) answers false and is bridged, exactly as
    /// <see cref="Trail"/>'s own walks bridge it - so the scan follows the line the ride follows.
    /// A struct implementation is passed by <c>ref</c> through a constrained generic, so reading a
    /// live trail allocates nothing.
    /// </summary>
    public interface IUrchinRailPoints
    {
        int Count { get; }
        bool TryGetPoint(int index, out Vector3 point);
    }

    /// <summary>One direction's walk down a rail, measured against an objective.</summary>
    public readonly struct UrchinRailScan
    {
        /// <summary>False when the walk found no ridable point at all - nothing below means anything.</summary>
        public readonly bool Valid;

        /// <summary>The closest the rail's centre line comes to the objective along this walk.</summary>
        public readonly float MinDistance;

        /// <summary>Arc length from the start of the walk to that closest point.</summary>
        public readonly float ArcToMin;

        /// <summary>Arc length walked before the walk stopped.</summary>
        public readonly float ArcWalked;

        /// <summary>True when the walk stopped because the RAIL ended (an open ribbon's end, where
        /// the ride launches), false when it stopped at the arc budget.</summary>
        public readonly bool ReachesEnd;

        public UrchinRailScan(bool valid, float minDistance, float arcToMin, float arcWalked, bool reachesEnd)
        {
            Valid = valid;
            MinDistance = minDistance;
            ArcToMin = arcToMin;
            ArcWalked = arcWalked;
            ReachesEnd = reachesEnd;
        }

        public static UrchinRailScan Invalid => new(false, float.PositiveInfinity, 0f, 0f, false);
    }

    /// <summary>The tunables <see cref="UrchinRailAssessment.Decide"/> reads.</summary>
    public struct UrchinRailRules
    {
        /// <summary>A rail that passes within this of the objective THREADS it - ride it there.</summary>
        public float CaptureRadius;

        /// <summary>A rail whose closest approach is under this fraction of the current range is
        /// still worth riding toward the objective even though it does not reach it: the grind
        /// runs at several times cruise, so a detour on a rail beats a straight line in the air.</summary>
        public float ClosingFraction;

        /// <summary>While crawling hostile mass with no ammo to convert it, the rail is only worth
        /// staying on if it threads the objective within this arc.</summary>
        public float DryCrawlArc;

        /// <summary>An open end inside this arc is left by LAUNCHING (free, and faster than a Slip),
        /// not by slipping off.</summary>
        public float LaunchPreferArc;
    }

    /// <summary>
    /// The pure half of the Urchin autopilot: given where a rail goes, does riding it get the
    /// pilot to its objective? No Unity object, no clock, no randomness - so the shipped decision
    /// IS the tested one (<c>UrchinRailAssessmentTests</c>), the same split
    /// <c>AIObjectiveScoring</c> made for crystal selection.
    ///
    /// <para><b>Why the AI needs this at all.</b> A riding Urchin cannot leave a rail by steering
    /// - the ride constrains POSITION and never attitude - so an autopilot that only aims (the
    /// platform <see cref="AIPilot"/>) rides whatever it touched to wherever that rail goes. In a
    /// race whose rings are pinned to ONE rail each (Skein above intensity 1) that is a pilot who
    /// threads the rings its rails happen to pass and no others. The kit already has the answer:
    /// the pilot's FACING picks the ride direction (<c>GunVesselTransformer.Slide</c>), and Slip
    /// lets go. This decides which of the three to use.</para>
    /// </summary>
    public static class UrchinRailAssessment
    {
        /// <summary>
        /// Walk <paramref name="rail"/> from <paramref name="start"/> in <paramref name="step"/>
        /// (+1 = toward the head, -1 = toward the tail) for at most <paramref name="maxArc"/> of
        /// arc, measuring how close the centre line comes to <paramref name="objective"/>.
        /// Distance is measured to each SEGMENT, not each point, so a rail that runs through a
        /// ring between two prisms reads as threading it.
        /// </summary>
        public static UrchinRailScan Scan<T>(ref T rail, int start, int step, Vector3 objective, float maxArc)
            where T : IUrchinRailPoints
        {
            int count = rail.Count;
            if (count <= 0 || start < 0 || start >= count || step == 0) return UrchinRailScan.Invalid;
            step = step > 0 ? 1 : -1;

            // The start may itself be a hole; the walk starts from the first ridable point at or
            // past it, which is where the ride would be bridged to anyway.
            int i = start;
            Vector3 prev = default;
            bool found = false;
            for (; i >= 0 && i < count; i += step)
            {
                if (!rail.TryGetPoint(i, out prev)) continue;
                found = true;
                break;
            }
            if (!found) return UrchinRailScan.Invalid;

            float min = Vector3.Distance(prev, objective);
            float arcToMin = 0f;
            float arc = 0f;
            bool reachesEnd = true;

            for (i += step; i >= 0 && i < count; i += step)
            {
                if (!rail.TryGetPoint(i, out var p)) continue;

                float seg = Vector3.Distance(prev, p);
                if (arc + seg > maxArc)
                {
                    reachesEnd = false;
                    break;
                }

                float t = SegmentParameter(prev, p, objective);
                float d = Vector3.Distance(Vector3.Lerp(prev, p, t), objective);
                if (d < min)
                {
                    min = d;
                    arcToMin = arc + seg * t;
                }

                arc += seg;
                prev = p;
            }

            return new UrchinRailScan(true, min, arcToMin, arc, reachesEnd);
        }

        /// <summary>
        /// Ride, reverse or leave. Order matters and each rule is a thing a human rider weighs:
        /// <list type="number">
        /// <item>A rail that THREADS the objective ahead is ridden - unless the pilot is crawling
        /// hostile mass with no ammo and the objective is a long crawl away.</item>
        /// <item>One that threads it BEHIND is reversed onto, under the same condition.</item>
        /// <item>A rail that still CLOSES a good fraction of the range is ridden: the grind is
        /// several times cruise, so the rail is the faster way even when it is not the way.</item>
        /// <item>An open end close ahead is LAUNCHED off rather than slipped off.</item>
        /// <item>A rail closing much harder BEHIND than the range is reversed onto.</item>
        /// <item>Otherwise leave.</item>
        /// </list>
        /// </summary>
        public static UrchinRailVerdict Decide(in UrchinRailScan ahead, in UrchinRailScan behind,
                                               float rangeNow, in UrchinRailRules rules, bool crawlingDry)
        {
            float capture = Mathf.Max(0f, rules.CaptureRadius);

            if (Threads(ahead, capture, crawlingDry, rules.DryCrawlArc)) return UrchinRailVerdict.Ride;
            if (Threads(behind, capture, crawlingDry, rules.DryCrawlArc)) return UrchinRailVerdict.Reverse;

            if (!crawlingDry)
            {
                float closing = Mathf.Clamp01(rules.ClosingFraction);

                if (ahead.Valid && ahead.MinDistance < rangeNow * closing) return UrchinRailVerdict.Ride;

                if (ahead.Valid && ahead.ReachesEnd && ahead.ArcWalked <= rules.LaunchPreferArc)
                    return UrchinRailVerdict.Ride;

                // Reversing just to close costs the swing, so it has to be worth markedly more.
                if (behind.Valid && behind.MinDistance < rangeNow * closing * closing)
                    return UrchinRailVerdict.Reverse;
            }

            return UrchinRailVerdict.Leave;
        }

        static bool Threads(in UrchinRailScan scan, float capture, bool crawlingDry, float dryCrawlArc) =>
            scan.Valid && scan.MinDistance <= capture && (!crawlingDry || scan.ArcToMin <= dryCrawlArc);

        /// <summary>The parameter in [0,1] of the point on segment a-b closest to p.</summary>
        static float SegmentParameter(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            return len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
        }
    }
}
