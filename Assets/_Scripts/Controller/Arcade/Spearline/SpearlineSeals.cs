using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One intensity's cut of Spearline's seals (<see cref="SpearlineSeals.ForIntensity"/>).</summary>
    public struct SpearlineSealSettings
    {
        public float MouthRadius;      // the ring's mouth on this rung (Headlong's own radius is overridden: SPEARLINE.md §2)
        public int SealedPerLap;       // how many of the lap's rings carry a seal (the gentlest corners, never ring 0)
        public int NeedPrisms;         // a crossing counts through a hole of at least this many prisms (HoleSize)
        public int WearPerPass;        // each closed pass lowers THAT pilot's need at THAT seal by this many prisms
        public float ArmourDegrees;    // the shielded sector of an armoured seal (0: none)
        public int ArmourEvery;        // every Nth SEALED ring is armoured (0: none, 1: all)
        public float RegrowSeconds;    // a cut seal prism (or a cracked shield) grows back this long after the field passed
    }

    /// <summary>
    /// Spearline's seals (SPEARLINE.md): a pane of prisms laid IN a ring's mouth, square to its axis, on a hex grid. A
    /// sealed ring counts only when the pilot crosses it through a HOLE: the connected run of gone prisms the crossing
    /// sits in (<see cref="HoleSize"/>) must hold at least <see cref="SpearlineSealSettings.NeedPrisms"/>.
    ///
    /// <para><b>Why a connected hole and not a share of a disc (2026-11-16).</b> The bill is a long, thin cutter: a
    /// stroke lays it flat to one side of the nose (or stands it up), so as the hull crosses the pane it cuts ONE line,
    /// up to the bill's length, out of the crossing point, while the hull punches a disc about as wide as itself. The
    /// shipped rule judged the share of a 22 u disc that was gone, and with the bill's real width (its capsule is 4.8 u
    /// across) the hull plus one stroke reached at most 42% of that disc - under every rung's need, so no single pass
    /// opened a seal at all, and whether the hull alone read 17% or 37% depended on where the crossing fell against
    /// the 9 u grid, which no pilot can see. Counted as a connected hole, the hull alone makes 3-7 prisms and the hull
    /// plus any stroke INTO the pane makes 8-19, at either bill width (<c>Tools/Build/spearline_race_model.py</c>), so
    /// 8 separates them with no grid luck.</para>
    ///
    /// <para><b>Armour is the read.</b> On an armoured seal a sector of the pane is shielded: a shielded prism only
    /// cracks under a hull or a stroke (its shield pops) and stands, so a stroke into the armour cuts nothing. The pilot
    /// has to see where the plain face is (the shields read in the world), cross it, and swing into it. The sector's
    /// angle is a pure function of the ring, so it is the same every lap: read on lap one, known after.</para>
    ///
    /// <para><b>The worn seal.</b> Every closed pass a pilot makes at a seal lowers that pilot's need there by
    /// <see cref="SpearlineSealSettings.WearPerPass"/>, so a pilot who keeps failing is through in a bounded number of
    /// passes (<see cref="MaxPasses"/>): no seal can trap anyone, human or AI.</para>
    ///
    /// <para>Pure and deterministic: a seal is a function of its broadcast ring, so every peer lays the same prisms and
    /// nothing new is replicated. The vessel is untouched (the LOCKED mode rule).</para>
    /// </summary>
    public static class SpearlineSeals
    {
        /// <summary>The hex grid's pitch, a prism's face and its depth along the ring's axis (u).</summary>
        public const float Spacing = 9f, PrismSize = 7.5f, PrismDepth = 2f;

        /// <summary>The disc stops this far inside the ring's mouth, so the seal never clips the ring's own frame.</summary>
        public const float RimInset = 2f;

        /// <summary>Two seal prisms whose centres are within this distance are neighbours in a hole (the hex pitch, with
        /// slack for float error; the next ring of the grid is 15.6 u out).</summary>
        public const float NeighbourDistance = 9.6f;

        /// <summary>The prisms the hull itself takes out as it crosses: those whose centres are within this distance of
        /// the crossing, in the ring's plane (the hull's half-width, 6 u off the airframe, plus half a prism's face).</summary>
        public const float HullReach = 6f + 0.5f * PrismSize;

        /// <summary>The MODEL of one stroke, used by the tests, the AI coach and the race model (not by the rule): the
        /// bill's length at Space 0 (Skimmer scale 24 x 2) and the half-width of the band it cuts, its capsule's radius
        /// (2.4 u, <c>Swordfish.prefab</c>) plus half a prism's face. A playtest moves the width first.</summary>
        public const float ModelBladeLength = 48f, ModelStrokeReach = 2.4f + 0.5f * PrismSize;

        // The ladder's rows, I1..I4 (SPEARLINE.md §2). Static, so ForIntensity allocates nothing (it is read every frame).
        static readonly float[] MouthByRung = { 96f, 84f, 74f, 66f };
        static readonly int[] SealedByRung = { 2, 3, 4, 5 };
        static readonly float[] ArmourByRung = { 0f, 120f, 180f, 240f };
        static readonly int[] ArmourEveryByRung = { 0, 1, 1, 1 };
        static readonly float[] RegrowByRung = { 4f, 3.5f, 3f, 2.5f };

        /// <summary>The ladder (SPEARLINE.md §2).</summary>
        public static SpearlineSealSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new SpearlineSealSettings
            {
                MouthRadius = MouthByRung[i],
                SealedPerLap = SealedByRung[i],
                NeedPrisms = 8,
                WearPerPass = 2,
                ArmourDegrees = ArmourByRung[i],
                ArmourEvery = ArmourEveryByRung[i],
                RegrowSeconds = RegrowByRung[i],
            };
        }

        /// <summary>The most passes a pilot can need at one seal: the need wears to nothing after this many closed
        /// ones, and the next crossing counts whatever it hits.</summary>
        public static int MaxPasses(in SpearlineSealSettings s) =>
            s.WearPerPass <= 0 ? int.MaxValue : Mathf.CeilToInt(s.NeedPrisms / (float)s.WearPerPass) + 1;

        /// <summary>The need at a seal for a pilot who has already made <paramref name="closedPasses"/> closed passes
        /// there. Zero means any crossing of the mouth counts.</summary>
        public static int NeedAfter(in SpearlineSealSettings s, int closedPasses) =>
            Mathf.Max(0, s.NeedPrisms - s.WearPerPass * Mathf.Max(0, closedPasses));

        // ── Which rings are sealed ──────────────────────────────────────

        /// <summary>The turn at ring <paramref name="i"/> of a closed lap, in degrees (0 = straight through).</summary>
        public static float TurnDegrees(IReadOnlyList<RaceGate> lap, int i)
        {
            int n = lap.Count;
            if (n < 3) return 0f;
            Vector3 a = lap[i].Position - lap[(i - 1 + n) % n].Position;
            Vector3 b = lap[(i + 1) % n].Position - lap[i].Position;
            return Vector3.Angle(a, b);
        }

        /// <summary>
        /// Which rings of the lap carry a seal: the <see cref="SpearlineSealSettings.SealedPerLap"/> gentlest corners,
        /// never ring 0 (the first ring off the grid), ties to the lower index. A seal at a gentle corner is met at
        /// speed, and the open rings are the hard corners, so the ramp question Headlong asks is still asked.
        /// </summary>
        public static bool[] SealedRings(IReadOnlyList<RaceGate> lap, in SpearlineSealSettings s)
        {
            int n = lap.Count;
            var sealedRings = new bool[n];
            if (n < 2 || s.SealedPerLap <= 0) return sealedRings;
            var order = new List<int>(n);
            for (int i = 1; i < n; i++) order.Add(i);
            var turn = new float[n];
            for (int i = 0; i < n; i++) turn[i] = TurnDegrees(lap, i);
            order.Sort((x, y) =>
            {
                int c = turn[x].CompareTo(turn[y]);
                return c != 0 ? c : x.CompareTo(y);
            });
            for (int k = 0; k < Mathf.Min(s.SealedPerLap, order.Count); k++) sealedRings[order[k]] = true;
            return sealedRings;
        }

        /// <summary>Is the <paramref name="ordinal"/>-th sealed ring of the lap (0-based, in ring order) armoured?</summary>
        public static bool IsArmoured(int ordinal, in SpearlineSealSettings s) =>
            s.ArmourEvery > 0 && s.ArmourDegrees > 0f && ordinal % s.ArmourEvery == s.ArmourEvery - 1;

        /// <summary>The direction (radians, in the ring's <see cref="Basis"/>) the armour sector of ring
        /// <paramref name="ring"/> faces: a hash of the ring index and its broadcast position (rounded to whole units),
        /// so the same on every peer and every lap, and different on every course.</summary>
        public static float ArmourAngle(int ring, Vector3 ringPosition)
        {
            unchecked
            {
                uint h = (uint)(ring * 73856093) ^ 0x9E3779B9u;
                h = h * 31u + (uint)Mathf.RoundToInt(ringPosition.x);
                h = h * 31u + (uint)Mathf.RoundToInt(ringPosition.y);
                h = h * 31u + (uint)Mathf.RoundToInt(ringPosition.z);
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000 * 2f * Mathf.PI;
            }
        }

        /// <summary>A deterministic 0..1 draw from three integers (the AI's per-seat read and lane error).</summary>
        public static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Does the in-plane offset (<paramref name="x"/>, <paramref name="y"/>) from the ring's centre lie in an
        /// armour sector of <paramref name="degrees"/> facing <paramref name="angle"/>? The centre itself does not.</summary>
        public static bool InArmour(float x, float y, float angle, float degrees)
        {
            if (degrees <= 0f || x * x + y * y < 1e-4f) return false;
            float a = Mathf.Atan2(y, x) - angle;
            a -= 2f * Mathf.PI * Mathf.Floor((a + Mathf.PI) / (2f * Mathf.PI));   // into [-pi, pi)
            return Mathf.Abs(a) <= 0.5f * degrees * Mathf.Deg2Rad;
        }

        // ── Laying ──────────────────────────────────────────────────────

        /// <summary>The ring's plane basis: two unit vectors square to its axis and to each other.</summary>
        public static void Basis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            axis = axis.normalized;
            Vector3 seed = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(axis, seed).normalized;
            v = Vector3.Cross(axis, u).normalized;
        }

        /// <summary>The seal prism centres for a mouth of <paramref name="mouthRadius"/> at
        /// <paramref name="gate"/>, on a hex grid clipped to the mouth (less the inset).</summary>
        public static void Lay(in RaceGate gate, float mouthRadius, List<Vector3> into)
        {
            Basis(gate.Axis, out var u, out var v);
            float r = Mathf.Max(0f, mouthRadius - RimInset - 0.5f * PrismSize);
            float rowStep = Spacing * 0.8660254f;
            int rows = Mathf.CeilToInt(r / rowStep);
            for (int j = -rows; j <= rows; j++)
            {
                float y = j * rowStep;
                float shift = (j & 1) == 0 ? 0f : 0.5f * Spacing;
                int cols = Mathf.CeilToInt(r / Spacing) + 1;
                for (int k = -cols; k <= cols; k++)
                {
                    float x = k * Spacing + shift;
                    if (x * x + y * y > r * r) continue;
                    into.Add(gate.Position + u * x + v * y);
                }
            }
        }

        /// <summary>The ring's own radius: the seal's mouth.</summary>
        public static void Lay(in RaceGate gate, List<Vector3> into) => Lay(gate, gate.Radius, into);

        /// <summary>For each prism of one seal (<paramref name="count"/> from <paramref name="start"/> in
        /// <paramref name="positions"/>), the seal-local indices of its hex neighbours.</summary>
        public static int[][] Adjacency(IReadOnlyList<Vector3> positions, int start, int count)
        {
            var adj = new int[count][];
            var scratch = new List<int>(6);
            float d2 = NeighbourDistance * NeighbourDistance;
            for (int i = 0; i < count; i++)
            {
                scratch.Clear();
                Vector3 p = positions[start + i];
                for (int j = 0; j < count; j++)
                    if (j != i && (positions[start + j] - p).sqrMagnitude <= d2) scratch.Add(j);
                adj[i] = scratch.ToArray();
            }
            return adj;
        }

        /// <summary>Where the segment <paramref name="prev"/>-<paramref name="cur"/> meets the ring's plane (false if it
        /// does not). The ring's own test (<c>RaceGateRing</c>): a segment that ends or starts ON the plane crosses it.</summary>
        public static bool PlaneCrossing(in RaceGate gate, Vector3 prev, Vector3 cur, out Vector3 point)
        {
            Vector3 n = gate.Axis.normalized;
            float a = Vector3.Dot(prev - gate.Position, n), b = Vector3.Dot(cur - gate.Position, n);
            point = cur;
            if (a * b > 0f || Mathf.Approximately(a, b)) return false;
            point = Vector3.Lerp(prev, cur, Mathf.Clamp01(a / (a - b)));
            return true;
        }

        /// <summary>The in-plane distance from <paramref name="p"/> to <paramref name="q"/> across a ring of axis
        /// <paramref name="axis"/> (unit).</summary>
        public static float PlaneDistanceSqr(Vector3 p, Vector3 q, Vector3 axis)
        {
            Vector3 d = p - q;
            d -= axis * Vector3.Dot(d, axis);
            return d.sqrMagnitude;
        }

        // ── The hole rule ───────────────────────────────────────────────

        /// <summary>
        /// The size of the hole a crossing at <paramref name="crossing"/> goes through: the prisms the hull takes out
        /// itself (within <see cref="HullReach"/> of the crossing and <paramref name="breaksUnderHull"/>: plain, or
        /// armour already cracked), plus every GONE prism connected to them through gone neighbours. All arrays are
        /// seal-local (index k is prism <paramref name="start"/> + k); <paramref name="inHole"/> (at least as long as
        /// the seal) and <paramref name="hole"/> are scratch, and hold the hole afterwards. Allocates nothing.
        /// </summary>
        public static int HoleSize(Vector3 crossing, Vector3 axis, IReadOnlyList<Vector3> positions, int start,
                                   int[][] adjacency, bool[] gone, bool[] breaksUnderHull, bool[] inHole,
                                   List<int> hole)
        {
            axis = axis.normalized;
            hole.Clear();
            int count = adjacency.Length;
            System.Array.Clear(inHole, 0, count);
            float reach2 = HullReach * HullReach;
            for (int i = 0; i < count; i++)
            {
                if (PlaneDistanceSqr(positions[start + i], crossing, axis) > reach2) continue;
                if (!gone[i] && !breaksUnderHull[i]) continue;
                inHole[i] = true;
                hole.Add(i);
            }
            for (int k = 0; k < hole.Count; k++)
            {
                var next = adjacency[hole[k]];
                for (int m = 0; m < next.Length; m++)
                {
                    int j = next[m];
                    if (inHole[j] || !gone[j]) continue;
                    inHole[j] = true;
                    hole.Add(j);
                }
            }
            return hole.Count;
        }

        // ── The stroke model (tests, the AI coach, the race model) ─────────

        /// <summary>Does one stroke, the bill laid along in-plane direction <paramref name="band"/> (unit, or zero for
        /// no stroke) out of a crossing at <paramref name="crossing"/>, cut the prism at <paramref name="p"/>?</summary>
        public static bool StrokeCuts(Vector3 p, Vector3 crossing, Vector3 axis, Vector3 band, float bladeLength,
                                      float strokeReach)
        {
            if (band.sqrMagnitude < 1e-6f) return false;
            Vector3 d = p - crossing;
            d -= axis * Vector3.Dot(d, axis);
            float along = Vector3.Dot(d, band);
            if (along < 0f || along > bladeLength) return false;
            return (d - band * along).sqrMagnitude <= strokeReach * strokeReach;
        }

        /// <summary>The coach's answer for one seal: where to cross, which stroke, and the hole that makes.</summary>
        public struct Plan
        {
            public Vector3 Crossing;   // world point in the ring's plane
            public int Stroke;         // index into the strokes offered (-1: none)
            public int Hole;           // the hole it makes, in the model
            public bool Opens;         // Hole >= the need (+ the margin)
        }

        /// <summary>
        /// The read (the AI's, and the race model's reader): of the crossings on a polar grid inside the mouth (the
        /// centre, then 12 points on each of three rings), and the <paramref name="strokes"/> on offer (in-plane unit
        /// directions: the bill to the left, to the right, stood up), the one whose MODEL hole opens the seal by
        /// <paramref name="margin"/> nearest to <paramref name="preferred"/> (the pilot's line). If none opens, the
        /// biggest hole. <paramref name="gone"/> / <paramref name="breaks"/> / <paramref name="cuttable"/> are per seal
        /// prism: gone already, breaks under the hull, and cut by a stroke (plain; armour does not cut).
        /// <paramref name="cutNow"/>, <paramref name="inHole"/> (each at least as long as the seal) and
        /// <paramref name="hole"/> are scratch: the read allocates nothing.
        /// </summary>
        public static Plan Read(in RaceGate gate, float mouthRadius, Vector3 preferred, IReadOnlyList<Vector3> positions,
                                int start, int[][] adjacency, bool[] gone, bool[] breaks, bool[] cuttable,
                                IReadOnlyList<Vector3> strokes, int need, int margin, float bladeLength,
                                float strokeReach, bool[] cutNow, bool[] inHole, List<int> hole)
        {
            Vector3 axis = gate.Axis.normalized;
            Basis(axis, out var u, out var v);
            float r = Mathf.Max(0f, mouthRadius - RimInset - 0.5f * PrismSize);
            int count = adjacency.Length;
            var best = new Plan { Stroke = -1, Hole = -1, Crossing = gate.Position };
            float bestDist = float.MaxValue;
            for (int ringNo = 0; ringNo <= 3; ringNo++)
            {
                int around = ringNo == 0 ? 1 : 12;
                float rho = r * 0.25f * ringNo;
                for (int a = 0; a < around; a++)
                {
                    float ang = 2f * Mathf.PI * a / around;
                    Vector3 c = gate.Position + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * rho;
                    float dist = PlaneDistanceSqr(c, preferred, axis);
                    if (best.Opens && dist >= bestDist) continue;
                    for (int s = -1; s < strokes.Count; s++)
                    {
                        Vector3 band = s < 0 ? Vector3.zero : strokes[s];
                        for (int i = 0; i < count; i++)
                            cutNow[i] = gone[i] || (cuttable[i] && StrokeCuts(positions[start + i], c, axis, band,
                                                                             bladeLength, strokeReach));
                        int size = HoleSize(c, axis, positions, start, adjacency, cutNow, breaks, inHole, hole);
                        bool opens = size >= need + margin;
                        if (opens && (!best.Opens || dist < bestDist))
                        {
                            best = new Plan { Crossing = c, Stroke = s, Hole = size, Opens = true };
                            bestDist = dist;
                            break;
                        }
                        if (!best.Opens && size > best.Hole)
                            best = new Plan { Crossing = c, Stroke = s, Hole = size, Opens = false };
                    }
                }
            }
            return best;
        }
    }
}
