using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skim Race's course, read as the Squirrel's Time ability (Skimming): every ring is a HOOP placed so the skim line
    /// threads it, and the ladder adds one skimming question per intensity. Pure functions of the authored waypoint
    /// track and the random numbers the caller draws, so the shipped placement is the tested one
    /// (<c>SkimRaceHoopsTests</c>, run headless by <c>Tools/Build/skimrace_hoops_harness/run.sh</c>).
    ///
    /// <para><b>The ladder (one new demand per level).</b></para>
    /// <code>
    ///   I1  Over the rail   the hoop hangs on the skim line with the rail through its lower half: skim either face
    ///   I2  Pick the face   the hoop stands ON one face of the rail (its rim touches the plate): skim that face
    ///   I3  Time on the line  + Time crystals on the skim line between hoops: skim them and the skim pays more
    ///   I4  Live wire       + a Time-5 start (the card) and danger-prism wires strung over the rail before each hoop:
    ///                         at Time 5 a skimmed danger prism pays ten times (the Squirrel's L5 "Live Wire")
    /// </code>
    ///
    /// <para><b>Why hoops.</b> Until 2026-11-11 a ring stood on a random point of a 35 u shell round its anchor, and the
    /// anchors sit ON the rail, so most rings were within a few units of the plate's super-shielded contact shell and
    /// a pilot threading one struck the rail and lost the whole skim boost (<c>SKIM_RACE_AI.md</c> §9: the AI's 15-30
    /// strikes a race were these). A hoop keeps the randomness (along the rail, across it, which face) but always
    /// stands where skimming the rail threads it, so the verb that builds the speed is the verb that takes the ring.</para>
    ///
    /// <para><b>The vessel is untouched.</b> Every number the rail is cut for is the shipped Squirrel's
    /// (<c>Docs/SKIM_RACE_AI.md</c> §2): the 7.5 u skimmer at Space 0, the 4.12 x 0.59 hull box, the plate's
    /// super-shield. The Time-5 start is the card's <c>StartingElements</c> (Headlong's I4 is the precedent).</para>
    /// </summary>
    public static class SkimRaceHoops
    {
        // ── The ladder ───────────────────────────────────────────────────────

        /// <summary>I1 hangs the hoop on the skim line round the rail (either face threads); I2+ stands it on one face.</summary>
        public static readonly bool[] StandsOnFaceByIntensity = { false, true, true, true };

        /// <summary>One Time crystal every N anchor gaps (0 = none), I1-I4.</summary>
        public static readonly int[] TimeCrystalEveryByIntensity = { 0, 0, 2, 2 };

        /// <summary>Live wires strung over the rail before each hoop, I1-I4.</summary>
        public static readonly bool[] LiveWiresByIntensity = { false, false, false, true };

        /// <summary>The Time level the Squirrel starts the race at, I1-I4: the card's <c>StartingElements</c>, written by
        /// <c>Tools/Build/author_skimrace_assets.py</c> from this array (never typed twice). Time 5 switches on the
        /// Squirrel's L5 Live Wire, which the I4 wires are for.</summary>
        public static readonly int[] StartingTimeLevelByIntensity = { 0, 0, 0, 5 };

        public static int Clamp(int intensity) => Mathf.Clamp(intensity, 1, 4) - 1;
        public static bool StandsOnFace(int intensity) => StandsOnFaceByIntensity[Clamp(intensity)];
        public static int TimeCrystalEvery(int intensity) => TimeCrystalEveryByIntensity[Clamp(intensity)];
        public static bool LiveWires(int intensity) => LiveWiresByIntensity[Clamp(intensity)];
        public static int StartingTimeLevel(int intensity) => StartingTimeLevelByIntensity[Clamp(intensity)];

        // ── The rail and the hull (the shipped numbers) ──────────────────────

        /// <summary>Half the plate's width as the hull meets it: the 10 u plate's super-shield is 1.5x (15 u).</summary>
        public const float PlateHalfWidth = 15f;

        /// <summary>Half the plate's thickness as the hull meets it: its super-shield (1 u plate x 1.5).</summary>
        public const float PlateShellHalfThickness = 1.5f;

        /// <summary>The plate's own surface (1 u thick): what the skimmer measures to and what a standing hoop's rim sits on.</summary>
        public const float PlateTop = 0.5f;

        /// <summary>The Squirrel's skimmer radius at Space 0 (sphere 0.5 x scale 15).</summary>
        public const float SkimmerRadius = 7.5f;

        /// <summary>Half the Squirrel's hull box across (4.12 u) and its half height (0.59 u).</summary>
        public const float HullHalfWidth = 2.06f;
        public const float HullHalfHeight = 0.3f;

        /// <summary>The skim line: the hull-centre height above the plate's mid-plane that a pilot skimming the rail
        /// flies. Above the shell (1.5 + 0.3) with room, and inside the skimmer's reach of the plate (0.5 + 7.5 = 8).</summary>
        public const float SkimLine = 6f;

        /// <summary>The skim band: every hull-centre height that both clears the shell with a margin and skims the plate.</summary>
        public const float SkimBandLow = 4f;
        public const float SkimBandHigh = PlateTop + SkimmerRadius;

        /// <summary>How far inside the drawn rim a hull centre must stay: the hull's half diagonal plus the ring's own
        /// prism half-thickness and a hair.</summary>
        public const float RimMargin = 3.5f;

        // ── Randomness kept (the caller draws it) ────────────────────────────

        /// <summary>How far along the rail (u, either way) a hoop may stand from its anchor's foot.</summary>
        public const float AlongJitter = 30f;

        /// <summary>How far across the rail (u, either way) an I1 hoop may hang: the rail still passes clear through it.</summary>
        public const float HangingLateralJitter = 4f;

        /// <summary>How far across the rail (u, either way) a standing hoop's foot may be: on the plate, and the skim
        /// line over the plate still threads it.</summary>
        public const float StandingLateralJitter = 8f;

        /// <summary>Arc spacing (u) of the sampled rail the frames are read from. Finer than the 12 u plate pitch.</summary>
        public const float SampleSpacing = 6f;

        // ── Live wires (I4) ──────────────────────────────────────────────────

        /// <summary>Height of a wire's centre over the rail's mid-plane, on its face. A hull centre at 5-8 u skims the
        /// plate (within 8) and the wire (its underside at 12.5, within 7.5) at once, and clears the wire by 4 u.</summary>
        public const float WireHeight = 13f;

        /// <summary>A wire runs over the rail from this far before its hoop's anchor...</summary>
        public const float WireFrom = 200f;

        /// <summary>...to this far before it (beyond <see cref="AlongJitter"/>, so a hoop never stands under a wire).</summary>
        public const float WireTo = 70f;

        /// <summary>Arc spacing (u) of a wire's danger prisms; each is <see cref="WirePrismLength"/> long, so the wire
        /// reads as one line with short gaps.</summary>
        public const float WireSpacing = 10f;

        /// <summary>A wire prism's size: thin across, long along the rail.</summary>
        public const float WirePrismThickness = 1f;
        public const float WirePrismLength = 7f;

        // ── The rail, sampled ────────────────────────────────────────────────

        /// <summary>The rail at one arc length: where its centre line is, which way it runs, its floor's normal (the
        /// face a +1 hoop stands on) and its lateral (across the plate).</summary>
        public readonly struct Frame
        {
            public readonly Vector3 Position, Tangent, Normal, Lateral;
            public readonly float Arc;

            public Frame(Vector3 position, Vector3 tangent, Vector3 normal, Vector3 lateral, float arc)
            {
                Position = position; Tangent = tangent; Normal = normal; Lateral = lateral; Arc = arc;
            }
        }

        /// <summary>
        /// The closed rail as the track lays it: the authored waypoints, straight or Catmull-Rom (the same uniform
        /// spline <c>SpawnableWaypointTrack</c> lays its plates on), with the authored ribbon normals (or world up).
        /// </summary>
        public sealed class Ribbon
        {
            readonly List<Vector3> _p = new();
            readonly List<Vector3> _n = new();
            readonly List<float> _s = new();

            public int Count => _p.Count;
            public float Length { get; private set; }
            public Vector3 PointAt(int i) => _p[i];
            public float ArcOf(int i) => _s[i];

            public static Ribbon Build(IReadOnlyList<Vector3> waypoints, IReadOnlyList<Vector3> ups, bool spline,
                                       float spacing = SampleSpacing)
            {
                var r = new Ribbon();
                int count = waypoints?.Count ?? 0;
                if (count < 2) return r;
                bool hasUps = ups != null && ups.Count == count;
                for (int seg = 0; seg < count; seg++)
                {
                    Vector3 a = waypoints[seg], b = waypoints[(seg + 1) % count];
                    int steps = Mathf.Max(2, Mathf.CeilToInt((b - a).magnitude / Mathf.Max(0.5f, spacing)));
                    for (int k = 0; k < steps; k++)
                    {
                        float t = (float)k / steps;
                        r._p.Add(spline ? SplinePoint(waypoints, seg, t) : Vector3.Lerp(a, b, t));
                        Vector3 up = Vector3.up;
                        if (hasUps)
                        {
                            Vector3 u = spline ? SplinePoint(ups, seg, t) : Vector3.Lerp(ups[seg], ups[(seg + 1) % count], t);
                            if (u.sqrMagnitude > 1e-6f) up = u;
                        }
                        r._n.Add(up);
                    }
                }
                float s = 0f;
                for (int i = 0; i < r._p.Count; i++)
                {
                    r._s.Add(s);
                    s += (r._p[(i + 1) % r._p.Count] - r._p[i]).magnitude;
                }
                r.Length = s;
                return r;
            }

            /// <summary>The sample nearest <paramref name="point"/> (a straight scan: called on a pickup, not per frame).</summary>
            public int Nearest(Vector3 point)
            {
                int best = 0;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < _p.Count; i++)
                {
                    float d = (_p[i] - point).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = i; }
                }
                return best;
            }

            public float Wrap(float s)
            {
                if (Length <= 0f) return 0f;
                s %= Length;
                return s < 0f ? s + Length : s;
            }

            /// <summary>The rail's frame at arc length <paramref name="s"/> (wrapped), interpolated between samples.</summary>
            public Frame At(float s)
            {
                s = Wrap(s);
                int n = _p.Count;
                int lo = 0, hi = n - 1;
                while (lo < hi)   // the last sample whose arc is <= s
                {
                    int mid = (lo + hi + 1) / 2;
                    if (_s[mid] <= s) lo = mid; else hi = mid - 1;
                }
                int i = lo, j = (lo + 1) % n;
                float span = (j == 0 ? Length : _s[j]) - _s[i];
                float t = span > 1e-5f ? (s - _s[i]) / span : 0f;
                Vector3 p = Vector3.Lerp(_p[i], _p[j], t);
                Vector3 tangent = (_p[j] - _p[i]).normalized;
                if (tangent.sqrMagnitude < 0.5f) tangent = (_p[(i + 2) % n] - _p[i]).normalized;
                Vector3 up = Vector3.Lerp(_n[i], _n[j], t);
                Vector3 normal = (up - tangent * Vector3.Dot(up, tangent)).normalized;
                if (normal.sqrMagnitude < 0.5f) normal = Vector3.Cross(tangent, Vector3.right).normalized;
                Vector3 lateral = Vector3.Cross(normal, tangent).normalized;
                return new Frame(p, tangent, normal, lateral, s);
            }

            /// <summary>Signed arc from <paramref name="from"/> forward to <paramref name="to"/>, in (-L/2, L/2].</summary>
            public float Delta(float from, float to)
            {
                float d = Wrap(to - from);
                return d > Length * 0.5f ? d - Length : d;
            }

            static Vector3 SplinePoint(IReadOnlyList<Vector3> p, int segment, float t)
            {
                int c = p.Count;
                Vector3 p0 = p[((segment - 1) % c + c) % c], p1 = p[segment], p2 = p[(segment + 1) % c], p3 = p[(segment + 2) % c];
                float t2 = t * t, t3 = t2 * t;
                return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }
        }

        // ── Hoops ────────────────────────────────────────────────────────────

        /// <summary>A placed hoop: its centre (the crystal's position), the rail's frame at its foot, the face it stands
        /// on (+1 the floor's normal side, -1 the underside) and the height of its centre over the rail's mid-plane.</summary>
        public readonly struct Hoop
        {
            public readonly Vector3 Centre;
            public readonly Frame Foot;
            public readonly float Face, Height, Lateral, Mouth;

            public Hoop(Vector3 centre, Frame foot, float face, float height, float lateral, float mouth)
            {
                Centre = centre; Foot = foot; Face = face; Height = height; Lateral = lateral; Mouth = mouth;
            }
        }

        /// <summary>
        /// The hoop for a crystal spawned against <paramref name="anchor"/>, at a mouth of <paramref name="mouthRadius"/>.
        /// <paramref name="along01"/>, <paramref name="face01"/> and <paramref name="lateral01"/> are uniform draws in
        /// [0, 1) - the randomness the anchor's 35 u shell had, kept where it cannot cost a strike.
        /// </summary>
        public static Hoop Place(Ribbon ribbon, Vector3 anchor, int intensity, float mouthRadius,
                                 float along01, float face01, float lateral01)
        {
            int foot = ribbon.Nearest(anchor);
            float s = ribbon.ArcOf(foot) + Mathf.Lerp(-AlongJitter, AlongJitter, along01);
            return PlaceAt(ribbon.At(s), intensity, mouthRadius, face01, lateral01);
        }

        public static Hoop PlaceAt(in Frame foot, int intensity, float mouthRadius, float face01, float lateral01)
        {
            float face = face01 < 0.5f ? 1f : -1f;
            bool standing = StandsOnFace(intensity);
            float lateral = Mathf.Lerp(-1f, 1f, lateral01) * (standing ? StandingLateralJitter : HangingLateralJitter);
            // Standing: the rim's lowest point rests on the plate's surface (a wheel on a road). Hanging: the centre is on
            // the skim line, and the mouth is wide enough at I1 that the rail passes clear through its lower half.
            float height = standing ? mouthRadius + PlateTop : SkimLine;
            Vector3 centre = foot.Position + foot.Normal * (face * height) + foot.Lateral * lateral;
            return new Hoop(centre, foot, face, height, lateral, mouthRadius);
        }

        /// <summary>
        /// The widest lateral window (u) at hull-centre height <paramref name="h"/> (signed: + is the hoop's own face)
        /// through which a hull threads the hoop with <see cref="RimMargin"/> inside the rim, clipped to the plate (a
        /// pilot skimming the rail flies over it). 0 = no hull threads it at that height.
        /// </summary>
        public static float ThreadWindow(in Hoop hoop, float h)
        {
            float r = hoop.Mouth - RimMargin;
            if (r <= 0f) return 0f;
            float dh = h - hoop.Height;
            float half = r * r - dh * dh;
            if (half <= 0f) return 0f;
            half = Mathf.Sqrt(half);
            float lo = Mathf.Max(hoop.Lateral - half, -PlateHalfWidth + HullHalfWidth);
            float hi = Mathf.Min(hoop.Lateral + half, PlateHalfWidth - HullHalfWidth);
            return Mathf.Max(0f, hi - lo);
        }

        /// <summary>The rail's cross-section never touches a HANGING hoop's rim: how far (u) the farthest corner of the
        /// plate's shell stays inside the rim. Negative = the rim cuts the plate.</summary>
        public static float HangingRimClearance(in Hoop hoop)
        {
            float far = 0f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                float dx = sx * PlateHalfWidth - hoop.Lateral;
                float dy = sy * PlateShellHalfThickness - hoop.Height;
                far = Mathf.Max(far, Mathf.Sqrt(dx * dx + dy * dy));
            }
            return hoop.Mouth - far;
        }

        // ── Time crystals (I3+) ─────────────────────────────────────────────

        /// <summary>A course piece the controller lays on every peer: where it is, the rail's direction there, and the
        /// face's normal (a wire prism's rotation is <c>LookRotation(Forward, Up)</c>).</summary>
        public readonly struct Piece
        {
            public readonly Vector3 Position, Forward, Up;
            public readonly int Anchor;
            public readonly float Face;

            public Piece(Vector3 position, Vector3 forward, Vector3 up, int anchor, float face)
            {
                Position = position; Forward = forward; Up = up; Anchor = anchor; Face = face;
            }
        }

        /// <summary>The anchors' feet on the rail, in anchor order (arc lengths).</summary>
        public static void Feet(Ribbon ribbon, IReadOnlyList<Vector3> anchors, List<float> into)
        {
            into.Clear();
            if (anchors == null) return;
            for (int i = 0; i < anchors.Count; i++) into.Add(ribbon.ArcOf(ribbon.Nearest(anchors[i])));
        }

        /// <summary>
        /// Time crystals on the skim line, half way (by arc) from each anchor's foot to the next, every
        /// <see cref="TimeCrystalEvery"/> gaps, on alternating faces - so a pilot on either face meets one a lap.
        /// Deterministic: every peer mints the same from the authored anchors.
        /// </summary>
        public static void PlaceTimeCrystals(Ribbon ribbon, IReadOnlyList<Vector3> anchors, int intensity, List<Piece> into)
        {
            into.Clear();
            int every = TimeCrystalEvery(intensity);
            if (every <= 0 || ribbon.Count == 0 || anchors == null || anchors.Count < 2) return;
            var feet = new List<float>(anchors.Count);
            Feet(ribbon, anchors, feet);
            int placed = 0;
            for (int k = 0; k < feet.Count; k += every)
            {
                float a = feet[k], b = feet[(k + 1) % feet.Count];
                float gap = ribbon.Wrap(b - a);
                var f = ribbon.At(a + gap * 0.5f);
                float face = placed % 2 == 0 ? 1f : -1f;
                into.Add(new Piece(f.Position + f.Normal * (face * SkimLine), f.Tangent, f.Normal * face, k, face));
                placed++;
            }
        }

        // ── Live wires (I4) ──────────────────────────────────────────────────

        /// <summary>
        /// The live wires: before every anchor, a line of danger prisms strung <see cref="WireHeight"/> over the rail's
        /// centre line from <see cref="WireFrom"/> to <see cref="WireTo"/> before the anchor's foot, on alternating faces
        /// (anchor 0's on the floor's normal side). Deterministic: every peer lays the same from the authored anchors.
        /// </summary>
        public static void PlaceWires(Ribbon ribbon, IReadOnlyList<Vector3> anchors, int intensity, List<Piece> into)
        {
            into.Clear();
            if (!LiveWires(intensity) || ribbon.Count == 0 || anchors == null || anchors.Count == 0) return;
            var feet = new List<float>(anchors.Count);
            Feet(ribbon, anchors, feet);
            for (int k = 0; k < feet.Count; k++)
            {
                float face = k % 2 == 0 ? 1f : -1f;
                for (float d = WireFrom; d >= WireTo - 0.01f; d -= WireSpacing)
                {
                    var f = ribbon.At(feet[k] - d);
                    into.Add(new Piece(f.Position + f.Normal * (face * WireHeight), f.Tangent, f.Normal * face, k, face));
                }
            }
        }

        /// <summary>The prism scale a wire prism is laid at (x across, y up, z along).</summary>
        public static Vector3 WirePrismScale => new(WirePrismThickness, WirePrismThickness, WirePrismLength);

        /// <summary>
        /// The double-skim slot under a wire: the hull-centre heights that skim BOTH the plate and the wire at once
        /// (low, high), and how far the top of the slot keeps the hull from the wire's underside.
        /// </summary>
        public static void WireSlot(out float low, out float high, out float wireClearance)
        {
            float wireUnderside = WireHeight - WirePrismThickness * 0.5f;
            low = wireUnderside - SkimmerRadius;
            high = SkimBandHigh;
            wireClearance = wireUnderside - (high + HullHalfHeight);
        }
    }
}
