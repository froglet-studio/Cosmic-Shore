using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One intensity's cut of Slip Line (<see cref="SlipLineCourse.ForIntensity"/>).</summary>
    public struct SlipLineSettings
    {
        public int RingCount;
        public float InnerRadius;              // the nucleus shell: no leg passes inside it (+ CoreMargin)
        public float MinLeg, MaxLeg;           // ring to ring, u
        public float MinBend, MaxBend;         // degrees off the ring's axis
        public float RingRadius;
        public int Lanes;                      // rails per leg: 3 = one of each colour, 1 = one rail of one colour
        public float ShortShare;               // the share of legs whose rail is SHORT (0: every rail long)
        public float DetourMin, DetourMax;     // degrees a rail leans off its leg (0: on the leg's line)
        public int ChainEvery;                 // every Nth leg's rail runs straight on into the next leg's rail (0: none)
    }

    /// <summary>A rail: a straight line of prisms of one colour, threading its ring inside the mouth.</summary>
    public readonly struct Rail
    {
        public readonly Vector3 Start, End;    // End lies RailPast beyond the ring, Start back along the rail
        public readonly int Colour;            // the Dynamo's colour: 1 Ruby, 2 Jade, 3 Gold
        public readonly int Ring;              // the ring it threads (its leg runs into this ring)
        public readonly bool Chained;          // it starts where the rail of the leg before ends (a chain)

        public Rail(Vector3 start, Vector3 end, int colour, int ring, bool chained)
        {
            Start = start;
            End = end;
            Colour = colour;
            Ring = ring;
            Chained = chained;
        }

        public Vector3 Direction => (End - Start).normalized;
        public float Length => (End - Start).magnitude;
    }

    /// <summary>
    /// Slip Line, the HONEYGUIDE's Time race (SLIP_LINE.md). The Honeyguide flies the Sparrow's airframe at about 60 u/s, and
    /// its ride is the SLIP: touch a trail of a colour its battery holds with the nose along it, and it zips the trail at
    /// 480 u/s, up to 400 u, spending that charge. The battery is the RATEL's: every pattern the Ratel makes on the Dynamo
    /// sends a prism of a random one of its colours (about four a minute from the Hard AI, three at most held). So the race
    /// is a co-op budget: a ring flown is ~7 s, a ring slipped is ~1 s, and there are more rails than charges.
    ///
    /// <list type="number">
    /// <item><b>I1 Rails:</b> every leg carries three long rails, one of each colour, threading its ring. Any charge rides:
    /// learn to line the nose up and touch.</item>
    /// <item><b>I2 Colours:</b> one rail a leg, of one colour, half of them SHORT. A charge spent on a short rail is a charge
    /// not spent on the long one two legs on: read the colours ahead and hold.</item>
    /// <item><b>I3 Leans:</b> plus every rail leans 15-25 degrees off its leg, so its start lies off the line: reaching it is a
    /// detour that a long rail repays and a short one does not.</item>
    /// <item><b>I4 Chains:</b> plus every fourth rail runs straight on into the next leg's rail: hold both colours and one
    /// ride takes two rings. The smallest mouths.</item>
    /// </list>
    ///
    /// <para><b>The rails are a function of the broadcast rings</b> (<see cref="RailsFor"/>): positions from the rings,
    /// colours and lengths from a hash of each leg's index and its ring's axis, which is broadcast bit for bit and which a translation does not change. Nothing new is
    /// replicated. The vessel kit is untouched: the slip, the battery, the Dynamo and its AI are the shipped kit.</para>
    /// </summary>
    public static class SlipLineCourse
    {
        // ── The vessel this course is cut for (read back off HoneyguideConfig.asset by SlipLineCourseTests) ──

        /// <summary>`slipReach`, `zipSpeed`, `zipRamp`, `exitOffset`, `contactRadius`, `aimCone`, `minSlipTrailLength`.</summary>
        public const float SlipReach = 400f, ZipSpeed = 480f, ZipRamp = 0.15f, ExitOffset = 8f, ContactRadius = 5.5f, AimCone = 45f;
        public const int MinSlipTrailLength = 6;

        /// <summary>The Honeyguide's airframe: `MinimumSpeed` + `ThrottleScaler` at full throttle (Honeyguide.prefab).</summary>
        public const float CruiseSpeed = 60f;

        // ── The course's own constants ───────────────────────────────────

        /// <summary>Where every ring sits, from the cell centre.</summary>
        public const float ShellMin = 600f, ShellMax = 1050f;

        /// <summary>Ring 0, its axis, and the start line behind it.</summary>
        public static readonly Vector3 StartRing = new(0f, 0f, -820f);
        public static readonly Vector3 StartAxis = Vector3.right;
        public const float StartStandoff = 260f, StartSpacing = 30f;

        public const float CoreMargin = 60f, MinRingSeparation = 170f, RingClearance = 25f;

        /// <summary>A rail ends this far past its ring, so the ride crosses the ring mid-zip (the landing's zero-length pose
        /// is a teleport and threads nothing).</summary>
        public const float RailPast = 40f;

        /// <summary>A rail passes its ring this share of the mouth's radius off the centre, so a ring flown through its
        /// centre never touches the rail.</summary>
        public const float RailOffset = 0.5f;

        /// <summary>Prisms along a rail: this far apart, each this long along the rail and this thick.</summary>
        public const float RailSpacing = 20f, PrismLength = 14f, PrismWidth = 2.5f;

        /// <summary>Rail lengths, end to end: long (most of a leg, cut back to start clear of the ring before) and short.</summary>
        public const float LongMin = 260f, LongMax = 370f, ShortMin = 100f, ShortMax = 120f;

        /// <summary>A chain's second leg, and the most its rail turns off the first (well inside the slip's aim cone).</summary>
        public const float ChainLegMin = 270f, ChainLegMax = 320f, ChainTurnMax = 25f;

        /// <summary>Every line a pilot flies ring to ring clears every rail by this much more than the slip's catch.</summary>
        public const float LineClearance = 6f;

        /// <summary>The gate races' nucleus fallback (RaceCourseSource.DefaultInnerRadiusFallback).</summary>
        public const float DefaultInnerRadius = 480f;

        const int CandidatesPerStep = 64;
        const int Attempts = 128;

        public static SlipLineSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new SlipLineSettings
            {
                RingCount = 16,
                InnerRadius = DefaultInnerRadius,
                MinLeg = new[] { 400f, 400f, 400f, 400f }[i],
                MaxLeg = new[] { 470f, 480f, 500f, 520f }[i],
                MinBend = new[] { 15f, 20f, 20f, 20f }[i],
                MaxBend = new[] { 35f, 40f, 45f, 50f }[i],
                RingRadius = new[] { 72f, 66f, 60f, 54f }[i],
                Lanes = new[] { 3, 1, 1, 1 }[i],
                ShortShare = new[] { 0f, 0.5f, 0.5f, 0.4f }[i],
                DetourMin = new[] { 0f, 0f, 15f, 15f }[i],
                DetourMax = new[] { 0f, 0f, 25f, 25f }[i],
                ChainEvery = new[] { 0, 0, 0, 4 }[i],
            };
        }

        /// <summary>Leg <paramref name="ring"/> (into ring <paramref name="ring"/>) starts a chain: its rail runs straight on
        /// into the next leg's.</summary>
        public static bool StartsChain(int ring, in SlipLineSettings s) =>
            s.ChainEvery > 0 && ring >= 1 && ring % s.ChainEvery == s.ChainEvery - 1 && ring + 1 < s.RingCount;

        // ── The start ────────────────────────────────────────────────────

        public static Pose[] StartPoses(int count)
        {
            count = Mathf.Max(1, count);
            var poses = new Pose[count];
            Vector3 across = Vector3.Cross(StartAxis, Vector3.up).normalized;
            Vector3 line = StartRing - StartAxis * StartStandoff;
            var facing = Quaternion.LookRotation(StartAxis, Vector3.up);
            for (int i = 0; i < count; i++)
                poses[i] = new Pose(line + across * ((i - 0.5f * (count - 1)) * StartSpacing), facing);
            return poses;
        }

        // ── The walk ─────────────────────────────────────────────────────

        public static List<RaceGate> Generate(int seed, in SlipLineSettings s)
        {
            var p = new List<Vector3>();
            var a = new List<Vector3>();
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var rng = new RaceCourseGeometry.Rng(unchecked(seed * 7919 + attempt * 104729 + 53));
                if (!TryWalk(ref rng, s, p, a)) continue;
                var rings = new List<RaceGate>(p.Count);
                for (int i = 0; i < p.Count; i++) rings.Add(new RaceGate(p[i], a[i], s.RingRadius));
                if (Valid(s, rings, out _)) return rings;
            }
            return null;
        }

        static bool TryWalk(ref RaceCourseGeometry.Rng rng, in SlipLineSettings s, List<Vector3> p, List<Vector3> a)
        {
            p.Clear();
            a.Clear();
            p.Add(StartRing);
            a.Add(StartAxis);
            for (int k = 1; k < s.RingCount; k++)
            {
                bool second = StartsChain(k - 1, s);   // the leg after a chain's first: nearly straight on, shorter
                float legMin = second ? ChainLegMin : s.MinLeg, legMax = second ? ChainLegMax : s.MaxLeg;
                float bendMin = second ? 5f : s.MinBend, bendMax = second ? 15f : s.MaxBend;
                Vector3 prev = a[k - 1];
                Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(prev, Vector3.up), Vector3.forward);
                Vector3 e2 = Vector3.Cross(prev, e1);
                bool placed = false;
                for (int c = 0; c < CandidatesPerStep && !placed; c++)
                {
                    float bend = rng.Range(bendMin, bendMax);
                    float phi = rng.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector3 about = e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi);
                    Vector3 d = RaceCourseGeometry.RotateAbout(prev, about, bend * Mathf.Deg2Rad);
                    Vector3 next = p[k - 1] + d * rng.Range(legMin, legMax);
                    if (!Fits(s, p, next)) continue;
                    p.Add(next);
                    a.Add(d);
                    placed = true;
                }
                if (!placed) return false;
            }
            return true;
        }

        static bool Fits(in SlipLineSettings s, List<Vector3> p, Vector3 next)
        {
            float r = next.magnitude;
            if (r < ShellMin || r > ShellMax) return false;
            if (DistanceToSegment(Vector3.zero, p[p.Count - 1], next) < s.InnerRadius + CoreMargin) return false;
            for (int i = 0; i < p.Count - 1; i++)
                if ((p[i] - next).magnitude < MinRingSeparation) return false;
            float clear = s.RingRadius + RingClearance;
            for (int i = 0; i < p.Count - 1; i++)
                if (DistanceToSegment(p[i], p[p.Count - 1], next) < clear) return false;
            for (int i = 1; i < p.Count - 1; i++)
                if (DistanceToSegment(next, p[i - 1], p[i]) < clear) return false;
            return true;
        }

        // ── The rails ────────────────────────────────────────────────────

        /// <summary>A leg's own dice: its index and the exact bits of its ring's AXIS. The axis is broadcast as it was made
        /// and, unlike a position, never has the cell's centre added to it, so every peer reads the same bits wherever the
        /// cell is (a length or a position would differ in its last bits, and a hash flips on them). FNV-1a.</summary>
        static uint LegHash(IReadOnlyList<RaceGate> rings, int k, int salt)
        {
            var axis = rings[k].Axis;
            uint h = 2166136261u;
            void Mix(int v) { unchecked { for (int b = 0; b < 4; b++) { h ^= (uint)((v >> (8 * b)) & 0xFF); h *= 16777619u; } } }
            Mix(k);
            Mix(System.BitConverter.SingleToInt32Bits(axis.x));
            Mix(System.BitConverter.SingleToInt32Bits(axis.y));
            Mix(System.BitConverter.SingleToInt32Bits(axis.z));
            Mix(salt);
            return h;
        }

        static float Unit(uint h) => (h & 0xFFFFFF) / (float)0x1000000;

        /// <summary>The leg into ring <paramref name="k"/>'s direction (the start line's for ring 0).</summary>
        public static Vector3 LegDirection(IReadOnlyList<RaceGate> rings, int k) =>
            k == 0 ? rings[0].Axis : RaceCourseGeometry.SafeNormalize(rings[k].Position - rings[k - 1].Position, rings[k].Axis);

        /// <summary>Where a pilot flying the leg into ring <paramref name="k"/> without a rail starts it: the ring before,
        /// or the start line.</summary>
        public static Vector3 LegStart(IReadOnlyList<RaceGate> rings, int k) =>
            k == 0 ? rings[0].Position - rings[0].Axis * StartStandoff : rings[k - 1].Position;

        /// <summary>
        /// The rails, cell-local or world (as the rings are). Every leg into ring 1 onward has its rails (the start leg has
        /// none: the battery starts empty). Each rail ends <see cref="RailPast"/> beyond its ring,
        /// <see cref="RailOffset"/> of the mouth off the centre, and runs back along its leg (leaning off it at I3-I4) for
        /// a long or a short length; a chain's second rail starts where the first's ride lands.
        /// </summary>
        public static void RailsFor(IReadOnlyList<RaceGate> rings, in SlipLineSettings s, List<Rail> rails)
        {
            rails.Clear();
            if (rings == null) return;
            for (int k = 1; k < rings.Count; k++)
            {
                var g = rings[k];
                Vector3 leg = LegDirection(rings, k);
                Vector3 e1 = RaceCourseGeometry.SafeNormalize(Vector3.Cross(g.Axis, Vector3.up), Vector3.forward);
                Vector3 e2 = Vector3.Cross(g.Axis, e1);
                // the rails pass the ring on the side AWAY from where the next leg leaves it, so the line flown on out of
                // the ring never crosses a rail's tail; a three-rail leg puts the other two square to that
                float spin = Unit(LegHash(rings, k, 1)) * Mathf.PI * 2f;
                if (k + 1 < rings.Count)
                {
                    Vector3 onward = Vector3.ProjectOnPlane(rings[k + 1].Position - g.Position, g.Axis);
                    if (onward.sqrMagnitude > 1f) spin = Mathf.Atan2(Vector3.Dot(-onward, e2), Vector3.Dot(-onward, e1));
                }
                int lanes = Mathf.Max(1, s.Lanes);
                int firstColour = 1 + (int)(Unit(LegHash(rings, k, 2)) * 3f) % 3;
                bool chainedIn = StartsChain(k - 1, s);
                for (int lane = 0; lane < lanes; lane++)
                {
                    float ang = spin + (lanes == 3 ? (lane - 1) * (Mathf.PI * 0.5f) : lane * (Mathf.PI * 2f / lanes));
                    Vector3 off = (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * (RailOffset * g.Radius);
                    int colour = lanes == 3 ? lane + 1 : firstColour;
                    if (chainedIn && lanes == 1)
                    {
                        // a chain's second rail: from where the first rail's ride lands, through this ring, on past it
                        var first = rails[rails.Count - 1];
                        Vector3 from = first.End + first.Direction * ExitOffset;
                        Vector3 through = g.Position + off;
                        Vector3 d2 = RaceCourseGeometry.SafeNormalize(through - from, leg);
                        if (colour == first.Colour) colour = colour % 3 + 1;   // a chain asks for two colours
                        rails.Add(new Rail(from, through + d2 * RailPast, colour, k, true));
                        continue;
                    }
                    bool shortRail = !StartsChain(k, s) && Unit(LegHash(rings, k, 3)) < s.ShortShare;
                    float u = Unit(LegHash(rings, k, 4 + lane));
                    float length = shortRail ? Mathf.Lerp(ShortMin, ShortMax, u) : Mathf.Lerp(LongMin, LongMax, u);
                    // a long rail starts clear of the ring before (its mouth, and the line a pilot leaves it on)
                    float legLength = (g.Position - LegStart(rings, k)).magnitude;
                    length = Mathf.Min(length, legLength + RailPast - rings[k - 1].Radius - RingClearance - 15f);
                    Vector3 dir = leg;
                    if (s.DetourMax > 0f && !StartsChain(k, s))
                    {
                        float lean = Mathf.Lerp(s.DetourMin, s.DetourMax, Unit(LegHash(rings, k, 7)));
                        Vector3 about = RaceCourseGeometry.SafeNormalize(Vector3.Cross(leg, off), e1);
                        dir = RaceCourseGeometry.RotateAbout(leg, about, -lean * Mathf.Deg2Rad);   // its start swings out, away from the line
                    }
                    Vector3 end = g.Position + off + dir * RailPast;
                    rails.Add(new Rail(end - dir * length, end, colour, k, false));
                }
            }
        }

        /// <summary>The prisms of a rail: positions every <see cref="RailSpacing"/> from its start to its end.</summary>
        public static void PrismsOf(in Rail rail, List<Vector3> points)
        {
            points.Clear();
            float len = rail.Length;
            int n = Mathf.Max(MinSlipTrailLength, Mathf.CeilToInt(len / RailSpacing) + 1);
            for (int i = 0; i < n; i++) points.Add(Vector3.Lerp(rail.Start, rail.End, i / (float)(n - 1)));
        }

        // ── Validity ─────────────────────────────────────────────────────

        /// <summary>The course the tests hold: inside the shell, clear of the core, rings apart, legs clear of other rings;
        /// every rail threads its own ring inside the mouth, fits one slip, keeps clear of every other ring's mouth and of
        /// every line a pilot flies ring to ring, and no two rails touch (but a chain's join).</summary>
        public static bool Valid(in SlipLineSettings s, IReadOnlyList<RaceGate> rings, out string why)
        {
            why = null;
            if (rings == null || rings.Count != s.RingCount) { why = "wrong ring count"; return false; }
            float clear = s.RingRadius + RingClearance;
            for (int i = 0; i < rings.Count; i++)
            {
                float r = rings[i].Position.magnitude;
                if (r < ShellMin - 1f || r > ShellMax + 1f) { why = $"ring {i} outside the shell ({r:F0})"; return false; }
                for (int j = 0; j < i; j++)
                    if ((rings[i].Position - rings[j].Position).magnitude < MinRingSeparation - 1f) { why = $"rings {j} and {i} too close"; return false; }
                if (i == 0) continue;
                if (DistanceToSegment(Vector3.zero, rings[i - 1].Position, rings[i].Position) < s.InnerRadius + CoreMargin - 1f)
                { why = $"leg {i} cuts the core"; return false; }
                for (int j = 0; j < rings.Count; j++)
                    if (j != i && j != i - 1 && DistanceToSegment(rings[j].Position, rings[i - 1].Position, rings[i].Position) < clear - 1f)
                    { why = $"leg {i} passes through ring {j}"; return false; }
            }
            var rails = new List<Rail>();
            RailsFor(rings, s, rails);
            float keep = ContactRadius + LineClearance;
            for (int a = 0; a < rails.Count; a++)
            {
                var rail = rails[a];
                var g = rings[rail.Ring];
                if (rail.Length > SlipReach - 20f) { why = $"rail {rail.Ring} is longer than a slip"; return false; }
                if (!Threads(rail.Start, rail.End, g, 4f)) { why = $"rail {rail.Ring} misses its ring"; return false; }
                if (rail.Chained && Vector3.Angle(rails[a - 1].Direction, rail.Direction) > ChainTurnMax)
                { why = $"the chain into ring {rail.Ring} turns too far"; return false; }
                if (rail.Chained && rail.Length > SlipReach - 20f) { why = $"chain rail {rail.Ring} too long"; return false; }
                for (int j = 0; j < rings.Count; j++)
                {
                    if (j == rail.Ring || (rail.Chained && j == rail.Ring - 1)) continue;   // a chain's second rail starts just past the first's ring
                    if (DistanceToSegment(rings[j].Position, rail.Start, rail.End) < rings[j].Radius + RingClearance)
                    { why = $"rail {rail.Ring} passes through ring {j}"; return false; }
                }
                // the line a pilot flies ring to ring (and from the start line) never touches a rail
                for (int k = 0; k < rings.Count; k++)
                {
                    Vector3 from = LegStart(rings, k), to = rings[k].Position;
                    if (SegmentDistance(from, to, rail.Start, rail.End) < keep) { why = $"the line into ring {k} touches rail {rail.Ring}"; return false; }
                }
                for (int b = 0; b < a; b++)
                {
                    if (rail.Chained && b == a - 1) continue;
                    if (rails[b].Ring == rail.Ring) { if (SegmentDistance(rails[b].Start, rails[b].End, rail.Start, rail.End) < 2f * ContactRadius) { why = $"two rails of ring {rail.Ring} touch"; return false; } continue; }
                    if (SegmentDistance(rails[b].Start, rails[b].End, rail.Start, rail.End) < 4f * ContactRadius) { why = $"rails {rails[b].Ring} and {rail.Ring} touch"; return false; }
                }
            }
            return true;
        }

        // ── A ride, and the AI's choice of one ───────────────────────────

        /// <summary>Seconds a slip of <paramref name="length"/> takes: the ramp to zip speed, then the rest at zip speed.</summary>
        public static float RideSeconds(float length)
        {
            float rampDist = 0.5f * ZipSpeed * ZipRamp;
            return length <= rampDist ? Mathf.Sqrt(2f * length * ZipRamp / ZipSpeed) : ZipRamp + (length - rampDist) / ZipSpeed;
        }

        /// <summary>
        /// The seconds a ride on <paramref name="rail"/> saves over flying from <paramref name="from"/> through its ring and
        /// on to <paramref name="after"/> (the next ring, or the ring itself at the end): the flight minus the fly to the
        /// rail's start, the ride, and the flight from its landing to <paramref name="after"/>.
        /// </summary>
        public static float Saving(Vector3 from, in Rail rail, in RaceGate ring, Vector3 after)
        {
            float plain = ((ring.Position - from).magnitude + (after - ring.Position).magnitude) / CruiseSpeed;
            Vector3 land = rail.End + rail.Direction * ExitOffset;
            float ride = (rail.Start - from).magnitude / CruiseSpeed + RideSeconds(rail.Length) + (after - land).magnitude / CruiseSpeed;
            return plain - ride;
        }

        /// <summary>The <paramref name="nth"/>-best saving (1 = best) a <paramref name="colour"/> rail offers on the legs
        /// after ring <paramref name="fromRing"/> to the finish, each judged from the ring before it; 0 when fewer remain.</summary>
        public static float NthBestSavingAhead(IReadOnlyList<RaceGate> rings, IReadOnlyList<Rail> rails, int fromRing, int nth, int colour)
        {
            var saves = new List<float>();
            for (int i = 0; i < rails.Count; i++)
            {
                var r = rails[i];
                if (r.Colour != colour || r.Ring <= fromRing || r.Chained) continue;
                Vector3 after = r.Ring + 1 < rings.Count ? rings[r.Ring + 1].Position : rings[r.Ring].Position;
                saves.Add(Saving(LegStart(rings, r.Ring), r, rings[r.Ring], after));
            }
            if (saves.Count < nth) return 0f;
            saves.Sort((a, b) => b.CompareTo(a));
            return saves[nth - 1];
        }

        /// <summary>
        /// The rail an AI (or a reading pilot) rides on the leg into ring <paramref name="next"/>, or -1 to fly it. It rides a
        /// rail of a colour the battery holds when the ride saves at least <see cref="MinSaving"/> s and at least
        /// <see cref="KeepShare"/> of the ride that charge would otherwise buy: with n charges of that colour held, the
        /// n-th best ride that colour has left before the finish (none left: spend it) - unless the battery is full, when
        /// the oldest charge is about to be lost anyway. A chain's first rail counts its second when
        /// the battery holds that colour too.
        /// </summary>
        public static int ChooseRail(Vector3 hull, IReadOnlyList<RaceGate> rings, IReadOnlyList<Rail> rails, int next,
            IReadOnlyList<int> battery, int batteryCap)
        {
            if (battery == null || battery.Count == 0) return -1;
            bool full = battery.Count >= batteryCap;
            int best = -1;
            float bestSave = 0f;
            Vector3 after = next + 1 < rings.Count ? rings[next + 1].Position : rings[next].Position;
            for (int i = 0; i < rails.Count; i++)
            {
                var r = rails[i];
                if (r.Ring != next || r.Chained || !Holds(battery, r.Colour)) continue;
                float save = Saving(hull, r, rings[next], after);
                if (i + 1 < rails.Count && rails[i + 1].Chained && Holds(battery, rails[i + 1].Colour, r.Colour == rails[i + 1].Colour ? 2 : 1))
                {
                    var c = rails[i + 1];
                    Vector3 after2 = c.Ring + 1 < rings.Count ? rings[c.Ring + 1].Position : rings[c.Ring].Position;
                    save += (rings[c.Ring].Position - rings[next].Position).magnitude / CruiseSpeed - RideSeconds(c.Length)
                            - (after2 - (c.End + c.Direction * ExitOffset)).magnitude / CruiseSpeed + (after2 - rings[c.Ring].Position).magnitude / CruiseSpeed;
                }
                if (save < MinSaving) continue;
                if (!full && save < KeepShare * NthBestSavingAhead(rings, rails, next, Count(battery, r.Colour), r.Colour)) continue;
                if (save > bestSave) { bestSave = save; best = i; }
            }
            return best;
        }

        /// <summary>A ride must save this many seconds to be worth a charge.</summary>
        public const float MinSaving = 1f;

        /// <summary>A ride takes a charge only when it saves this share of the ride the charge would otherwise buy.</summary>
        public const float KeepShare = 0.7f;

        static bool Holds(IReadOnlyList<int> battery, int colour, int count = 1) => Count(battery, colour) >= count;

        static int Count(IReadOnlyList<int> battery, int colour)
        {
            int n = 0;
            for (int i = 0; i < battery.Count; i++) if (battery[i] == colour) n++;
            return n;
        }

        /// <summary>Where an AI steers to catch <paramref name="rail"/>: a point <paramref name="lead"/> on along the rail
        /// from the hull's foot on its line (never before the start), so the hull closes onto the line already along it.</summary>
        public static Vector3 RailPursuit(Vector3 hull, in Rail rail, float lead)
        {
            Vector3 d = rail.Direction;
            float along = Mathf.Clamp(Vector3.Dot(hull - rail.Start, d), 0f, rail.Length);
            return rail.Start + d * Mathf.Min(rail.Length, along + lead);
        }

        // ── Geometry ─────────────────────────────────────────────────────

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-6f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>The closest two segments come (sampled: exact enough for clearances of a few units on legs of hundreds).</summary>
        public static float SegmentDistance(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            float best = float.MaxValue;
            const int n = 48;
            for (int i = 0; i <= n; i++)
            {
                best = Mathf.Min(best, DistanceToSegment(Vector3.Lerp(a0, a1, i / (float)n), b0, b1));
                best = Mathf.Min(best, DistanceToSegment(Vector3.Lerp(b0, b1, i / (float)n), a0, a1));
            }
            return best;
        }

        /// <summary>A segment threads a ring's mouth (either way across its plane) with <paramref name="margin"/> to spare.</summary>
        public static bool Threads(Vector3 from, Vector3 to, in RaceGate ring, float margin = 4f)
        {
            float a = Vector3.Dot(from - ring.Position, ring.Axis), b = Vector3.Dot(to - ring.Position, ring.Axis);
            if (a * b > 0f || Mathf.Approximately(a, b)) return false;
            Vector3 x = Vector3.Lerp(from, to, a / (a - b));
            return (x - ring.Position).magnitude <= ring.Radius - margin;
        }
    }
}
