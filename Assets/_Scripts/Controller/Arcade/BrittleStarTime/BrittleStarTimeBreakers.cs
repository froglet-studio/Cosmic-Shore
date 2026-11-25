using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's BREAKERS at I4 (BRITTLESTARTIME.md section 0b; Yash, 2026-11-24: "Intensity 4 environment
    /// needs to be more filled along the flight path, brittle star need to break through obstacles to pass rings").
    ///
    /// <para><b>What stands.</b> A disc of plain slabs across the way into EVERY ring, <see cref="RingLead"/> before it
    /// and <see cref="RingRadius"/> wide, so no line that threads the mouth misses it; and on a straight long enough to
    /// carry a ruin (the corkscrew's return straight), a disc every <see cref="StraightSpacing"/> u, the ruin standing
    /// round them. The discs and their slabs are Headlong's (<see cref="HeadlongHedges.Hedge"/>,
    /// <see cref="HeadlongHedges.Build"/>), placed by this mode's own rule: Headlong's placement keeps 150 u off every
    /// ring, and the coil's rings stand ~170 u apart.</para>
    ///
    /// <para><b>Why it is a decision, with the kit as shipped.</b> A Brittle Star that rams a plain prism breaks it, loses
    /// its boost and is slowed (its <c>VesselChangeSpeedByPrism</c>: up to half its speed for a second, by the prism's
    /// mass). Its guns and cords break prisms ahead of it. So a pilot either rams each disc and pays in speed, or clears
    /// a hole first and flies the ring at full Speed. The mode changes nothing on the vessel.</para>
    ///
    /// <para><b>What is kept clear.</b> A slab nearer than <see cref="LegClearance"/> to any other leg's line, or nearer
    /// than <see cref="PostClearance"/> to a let-go post, is not laid: the discs stand in this ring's way in and nowhere
    /// else. Pure: a function of the broadcast lap, so every peer lays the same discs (<c>BrittleStarTimeBreakersTests</c>).</para>
    /// </summary>
    public static class BrittleStarTimeBreakers
    {
        /// <summary>The first rung that carries breakers.</summary>
        public const int FromIntensity = 4;

        /// <summary>How far before a ring its disc stands, along the way in (u).</summary>
        public const float RingLead = 30f;

        /// <summary>A ring's disc radius (u): the mouth (42) and the cone a pilot comes in on from beyond the lead.</summary>
        public const float RingRadius = 70f;

        /// <summary>A straight's disc radius (u): as wide as a ring's, since pilots run the straight up to ~60 u off its
        /// centre line (the ruin's lane edge); the ruin keeps off the slabs (<see cref="BrittleStarTimeRuins.BreakerClearance"/>).</summary>
        public const float StraightRadius = 70f;

        /// <summary>The gap between a straight's discs (u).</summary>
        public const float StraightSpacing = 180f;

        /// <summary>A straight's discs stand at least this far from either end (u): the turn's exit and the ring's disc.</summary>
        public const float StraightEndClear = 140f;

        /// <summary>No slab nearer than this (u) to the line of any leg but the disc's own.</summary>
        public const float LegClearance = 25f;

        /// <summary>No slab nearer than this (u) to a let-go post.</summary>
        public const float PostClearance = 20f;

        public static bool On(int intensity) => intensity >= FromIntensity;

        /// <summary>
        /// The way a pilot comes into ring <paramref name="k"/>: down its facing when it faces the leg into it; else
        /// from where the turn off the ring before ends (<see cref="BrittleStarTimeRuins.TurnExit"/>), which is the
        /// straight a ring facing across its leg is flown in on; else along the leg.
        /// </summary>
        public static Vector3 WayIn(IReadOnlyList<RaceGate> course, int k, float turnRadius, out bool fromTurn, out Vector3 exit)
        {
            int n = course.Count;
            fromTurn = false;
            exit = default;
            if (BrittleStarTimeLine.FacesLeg(course, k)) return BrittleStarTimeLine.Flow(course, k);
            int p = (k - 1 + n) % n;
            Vector3 before = course[p].Position;
            Vector3 flow = BrittleStarTimeLine.FacesLeg(course, p) ? BrittleStarTimeLine.Flow(course, p) : BrittleStarTimeLine.Approach(course, p);
            if (BrittleStarTimeRuins.TurnExit(before, flow, course[k].Position, turnRadius, out exit) &&
                (course[k].Position - exit).magnitude > RingLead + RingRadius)
            {
                fromTurn = true;
                return (course[k].Position - exit).normalized;
            }
            return BrittleStarTimeLine.Approach(course, k);
        }

        /// <summary>Every disc of <paramref name="course"/> on this rung, appended to <paramref name="into"/> (none
        /// below <see cref="FromIntensity"/>).</summary>
        public static void Place(IReadOnlyList<RaceGate> course, int intensity, float turnRadius, List<HeadlongHedges.Hedge> into)
        {
            if (!On(intensity) || course.Count < 3) return;
            int n = course.Count;
            for (int k = 0; k < n; k++)
            {
                int leg = (k - 1 + n) % n;
                Vector3 ring = course[k].Position;
                Vector3 normal = WayIn(course, k, turnRadius, out bool fromTurn, out Vector3 exit);
                float legLength = (ring - course[leg].Position).magnitude;
                if (fromTurn)
                {
                    float straight = (ring - exit).magnitude;
                    if (straight >= SpearlineRuins.MinLegLength)
                        for (float s = StraightEndClear; s <= straight - RingLead - StraightEndClear; s += StraightSpacing)
                        {
                            Vector3 c = exit + normal * s;
                            into.Add(new HeadlongHedges.Hedge(leg, c, normal, StraightRadius, HeadlongHedges.Kind.Plain,
                                                              c, 0f, legLength - (straight - s)));
                        }
                }
                Vector3 centre = ring - normal * RingLead;
                into.Add(new HeadlongHedges.Hedge(leg, centre, normal, RingRadius, HeadlongHedges.Kind.Plain, centre, 0f,
                                                  legLength - RingLead));
            }
        }

        /// <summary>
        /// The slabs of <paramref name="hedges"/>, appended to <paramref name="into"/> with the disc each belongs to in
        /// <paramref name="owner"/>: <see cref="HeadlongHedges.Build"/>, less every slab that would stand on another
        /// leg's line or by a let-go post.
        /// </summary>
        public static void Build(IReadOnlyList<RaceGate> course, IReadOnlyList<HeadlongHedges.Hedge> hedges,
                                 IReadOnlyList<BrittleStarTimeLine.Post> posts, List<HeadlongHedges.Part> into, List<int> owner)
        {
            var slabs = new List<HeadlongHedges.Part>(64);
            for (int h = 0; h < hedges.Count; h++)
            {
                slabs.Clear();
                HeadlongHedges.Build(hedges[h], slabs);
                for (int s = 0; s < slabs.Count; s++)
                {
                    Vector3 p = slabs[s].Position;
                    if (OtherLegDistance(course, hedges[h].Leg, p) < LegClearance) continue;
                    if (posts != null && NearestPost(posts, p) < PostClearance) continue;
                    into.Add(slabs[s]);
                    owner?.Add(h);
                }
            }
        }

        /// <summary>How near <paramref name="p"/> comes to the line of any leg but <paramref name="ownLeg"/>.</summary>
        public static float OtherLegDistance(IReadOnlyList<RaceGate> course, int ownLeg, Vector3 p)
        {
            int n = course.Count;
            float best = float.MaxValue;
            for (int j = 0; j < n; j++)
            {
                if (j == ownLeg) continue;
                best = Mathf.Min(best, HeadlongHedges.DistanceToSegment(p, course[j].Position, course[(j + 1) % n].Position));
            }
            return best;
        }

        static float NearestPost(IReadOnlyList<BrittleStarTimeLine.Post> posts, Vector3 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < posts.Count; k++) best = Mathf.Min(best, (posts[k].Position - p).magnitude);
            return best;
        }

        /// <summary>Is <paramref name="p"/> within <paramref name="clearance"/> of any slab?</summary>
        public static bool Near(IReadOnlyList<HeadlongHedges.Part> slabs, Vector3 p, float clearance)
        {
            float c2 = clearance * clearance;
            for (int i = 0; i < slabs.Count; i++)
                if ((slabs[i].Position - p).sqrMagnitude < c2) return true;
            return false;
        }
    }
}
