using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One way the Pistol can set off: from perch <see cref="Perch"/>, riding <see cref="Dir"/> (+1 toward the
    /// newer perches, -1 toward the older ones).</summary>
    public readonly struct PathChoice
    {
        public readonly int Perch, Dir;
        public PathChoice(int perch, int dir) { Perch = perch; Dir = dir >= 0 ? 1 : -1; }
        /// <summary>The island it rides: segment k joins perch k and perch k + 1.</summary>
        public int Segment => Dir > 0 ? Perch : Perch - 1;
        public bool Same(PathChoice o) => o.Perch == Perch && o.Dir == Dir;
    }

    /// <summary>
    /// One pointer on the path map, touch first (Garrett, 2026-10-25: "the control priority for 2d characters is mobile,
    /// gamepad, mouse/keyboard"): a press that moves past the slop is a DRAG (it spins the map, frame by frame); one that
    /// lifts quickly without moving is a TAP (it picks); a press already down when the map opened (the finger diving
    /// through the perch) is CARRIED: held still it asks to ride on, moved it spins.
    /// </summary>
    public struct MapPointer
    {
        public bool Down, Carried, Moved;
        public Vector2 From, Last;
        public float Held;

        /// <summary>The map opens with the pointer <paramref name="held"/> at <paramref name="at"/>.</summary>
        public void Open(bool held, Vector2 at) { Down = held; Carried = held; Moved = false; From = Last = at; Held = 0f; }

        /// <summary>One frame. <paramref name="drag"/> is how far it dragged this frame (zero until it passes
        /// <paramref name="slop"/>); <paramref name="tap"/> is true on the frame a quick, still press lifts, at
        /// <paramref name="tapAt"/>; <paramref name="carriedStill"/> while a carried press stays put.</summary>
        public void Step(bool began, bool held, Vector2 at, float dt, float slop, float tapMaxSeconds,
                         out Vector2 drag, out bool tap, out Vector2 tapAt, out bool carriedStill)
        {
            drag = Vector2.zero; tap = false; tapAt = From; carriedStill = false;
            if (began) { Down = true; Carried = false; Moved = false; From = Last = at; Held = 0f; }
            if (!Down) return;
            if (held)
            {
                Held += dt;
                if (!Moved && (at - From).magnitude > slop) Moved = true;
                if (Moved) drag = at - Last;
                Last = at;
                carriedStill = Carried && !Moved;
                return;
            }
            tap = !Moved && !Carried && Held <= tapMaxSeconds;
            tapAt = From;
            Down = false;
        }
    }

    /// <summary>
    /// The Pistol's choice at a perch (Garrett, 2026-10-25: "when the shrimp gets to a new perch it should be able to pick
    /// another perch to teleport to. teleporting to different perches does not grow them. only traveling the distance using
    /// the dive should grow the den. the pistol should be able to travel either direction from a den as well. the view that
    /// allows the pistol to find a den and direction of interest should show which paths have collected the most opposing
    /// prisms for the vortex ring to destroy."). Pure, so the tests read it: the options along the perch chain, the cursor
    /// that walks them, the AI's pick, and which prisms count toward a path's score.
    /// </summary>
    public static class PistolPaths
    {
        /// <summary>
        /// Every (perch, direction) the Pistol can set off on, in the order a cursor walks the chain: perch 0 ahead, perch 1
        /// back, perch 1 ahead, ... perch n back, then the head perch ahead, which is PENDING (no island yet: it waits for
        /// the Goby's next dart, <see cref="Ready"/> false) and is listed whenever there is a head to wait at.
        /// </summary>
        public static void Options(int perches, int segments, List<PathChoice> into)
        {
            into.Clear();
            if (perches <= 0) return;
            int segs = Mathf.Min(segments, perches - 1);
            for (int k = 0; k < perches; k++)
            {
                if (k > 0 && k - 1 < segs) into.Add(new PathChoice(k, -1));
                if (k < segs) into.Add(new PathChoice(k, 1));
            }
            var head = new PathChoice(perches - 1, 1);
            if (head.Segment >= segs) into.Add(head);
        }

        /// <summary>
        /// The HALF PLANE a choice rides (Garrett, 2026-10-29: "it should show one side of the trail when traveling one way
        /// and pass on the other side when traveling the other direction"): riding island k ahead (A to B) plays the side its
        /// frame calls +z (the far side from the camera, <see cref="PistolPlane.Frame.M"/>); riding it back, the frame turns
        /// round and so does its +z, so it plays the other side. 2 k ahead, 2 k + 1 back: the index a choice's score is kept at.
        /// </summary>
        public static int Half(PathChoice c) => Half(c.Segment, c.Dir);

        public static int Half(int segment, int dir) => segment * 2 + (dir > 0 ? 0 : 1);

        /// <summary>Mass this close to the plane (pulled into it) lies in both halves.</summary>
        public const float PlaneSlack = 1f;

        /// <summary><see cref="InBand"/>, on the half the frame rides (+z, within <see cref="PlaneSlack"/> of the plane).</summary>
        public static bool InHalf(Vector3 fr, float endX, float ground, float reach, float band)
            => fr.z >= -PlaneSlack && InBand(fr, endX, ground, reach, band);

        /// <summary>True when the choice has an island to ride (the head's "ahead" waits for one).</summary>
        public static bool Ready(PathChoice c, int segments) => c.Segment >= 0 && c.Segment < segments;

        /// <summary>Where <paramref name="c"/> sits in <paramref name="options"/>; -1 when it is not there.</summary>
        public static int IndexOf(List<PathChoice> options, PathChoice c)
        {
            for (int i = 0; i < options.Count; i++) if (options[i].Same(c)) return i;
            return -1;
        }

        /// <summary>The choice <paramref name="delta"/> steps along the chain from <paramref name="current"/>, clamped to its
        /// ends; the nearest option on its perch when <paramref name="current"/> is gone.</summary>
        public static PathChoice Step(List<PathChoice> options, PathChoice current, int delta)
        {
            if (options.Count == 0) return current;
            int i = IndexOf(options, current);
            if (i < 0) i = Nearest(options, current.Perch);
            return options[Mathf.Clamp(i + delta, 0, options.Count - 1)];
        }

        /// <summary>The option on perch <paramref name="perch"/> (or the nearest perch that has one).</summary>
        public static int Nearest(List<PathChoice> options, int perch)
        {
            int best = 0, bestD = int.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                int d = Mathf.Abs(options[i].Perch - perch);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// The ready option whose half plane holds the most opposing prisms (<paramref name="scores"/>, one per half plane,
        /// <see cref="Half(PathChoice)"/>); on a tie the one setting off nearest <paramref name="fromPerch"/>. -1 when every
        /// half is empty, so the caller keeps its own way.
        /// </summary>
        public static int Best(List<PathChoice> options, IReadOnlyList<int> scores, int segments, int fromPerch)
        {
            int best = -1, bestScore = 0, bestD = int.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                var c = options[i];
                if (!Ready(c, segments) || Half(c) >= scores.Count) continue;
                int s = scores[Half(c)], d = Mathf.Abs(c.Perch - fromPerch);
                if (s > bestScore || (s == bestScore && s > 0 && d < bestD)) { best = i; bestScore = s; bestD = d; }
            }
            return best;
        }

        /// <summary>The Goby AI's candidate headings for its next island: 26 directions spread evenly over the sphere (a
        /// Fibonacci lattice), so no way out of a perch is left unweighed.</summary>
        public static readonly Vector3[] Headings = BuildHeadings(26);

        static Vector3[] BuildHeadings(int n)
        {
            var dirs = new Vector3[n];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < n; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / n, r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), a = golden * i;
                dirs[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            return dirs;
        }

        /// <summary>How many of <paramref name="points"/> an island laid from <paramref name="from"/> along
        /// <paramref name="dir"/> for <paramref name="length"/> would pass within <paramref name="radius"/> of (the reach
        /// the Pistol's undertow and ring have over it).</summary>
        public static int CorridorCount(Vector3 from, Vector3 dir, float length, float radius, List<Vector3> points)
        {
            int n = 0; float r2 = radius * radius;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 v = points[i] - from;
                float along = v.x * dir.x + v.y * dir.y + v.z * dir.z;
                if (along < 0f || along > length) continue;
                if ((v - dir * along).sqrMagnitude <= r2) n++;
            }
            return n;
        }

        /// <summary>Most prisms <see cref="BestHeading"/> aims a heading through (sampled evenly), on top of the fixed
        /// <see cref="Headings"/>: the fixed ones are ~40 degrees apart, too coarse for a long island's narrow corridor.</summary>
        public const int AimedHeadings = 64;

        /// <summary>
        /// The Goby AI's way to its next island (Garrett, 2026-10-25: "the goby AI was doing a bad job of taking paths that
        /// would give the shrimp plenty to destroy"): of the fixed <see cref="Headings"/> and a heading through each of up to
        /// <see cref="AimedHeadings"/> of the <paramref name="points"/> themselves, the one whose island would pass the most
        /// opposing points within <paramref name="radius"/>, never ending more than <paramref name="homeRadius"/> from
        /// <paramref name="home"/>; a tie goes to the least turn from <paramref name="forward"/>. <paramref name="misjudge"/>
        /// (0 for a sharp AI) scales each heading's count by up to ±half that, from <paramref name="seed"/>: the easier AIs
        /// misread the field. False when no heading passes any.
        /// </summary>
        public static bool BestHeading(Vector3 from, float length, float radius, List<Vector3> points, Vector3 home, float homeRadius,
                                       Vector3 forward, float misjudge, uint seed, out Vector3 heading, out int count)
        {
            heading = forward; count = 0;
            if (points.Count == 0) return false;
            float bestScore = 0f, bestCos = -2f;
            int aimed = Mathf.Min(points.Count, AimedHeadings), stride = Mathf.Max(1, points.Count / Mathf.Max(1, aimed));
            for (int i = 0; i < Headings.Length + aimed; i++)
            {
                Vector3 d;
                if (i < Headings.Length) d = Headings[i];
                else
                {
                    Vector3 v = points[((i - Headings.Length) * stride) % points.Count] - from;
                    float m = v.magnitude;
                    if (m < 1f) continue;
                    d = v / m;
                }
                if ((from + d * length - home).magnitude > homeRadius) continue;
                int c = CorridorCount(from, d, length, radius, points);
                if (c <= 0) continue;
                float score = c * (1f + misjudge * (Hash01(seed, (uint)i) - 0.5f));
                float cos = Vector3.Dot(d, forward);
                if (score > bestScore || (score == bestScore && cos > bestCos)) { heading = d; bestScore = score; bestCos = cos; count = c; }
            }
            return count > 0;
        }

        static float Hash01(uint seed, uint i)
        {
            uint h = seed * 0x9E3779B1u ^ (i + 0x7F4A7C15u) * 0x85EBCA77u;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }

        /// <summary>
        /// Whether a prism at frame coordinates <paramref name="fr"/> (x along the island, y up, z out of its plane) lies
        /// where the vortex ring can reach it: over the island (0..<paramref name="endX"/>), within the undertow's
        /// <paramref name="reach"/> of the plane (it is pulled in), and within <paramref name="band"/> of the ground.
        /// </summary>
        public static bool InBand(Vector3 fr, float endX, float ground, float reach, float band)
            => fr.x >= 0f && fr.x <= endX && Mathf.Abs(fr.z) <= reach && Mathf.Abs(fr.y - ground) <= band;
    }
}
