using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One intensity's cut of Thornline (<see cref="ThornlineCourse.ForIntensity"/>).</summary>
    public struct ThornlineSettings
    {
        public int RingCount;
        public float InnerRadius;        // the nucleus shell: no leg passes inside it
        public float MinLeg, MaxLeg;     // thorn to thorn, u
        public float MinBend, MaxBend;   // degrees off the previous leg at a plain bend
        public int SkipBends;            // bends a far thorn snipes through two rings at once
        public float ThornSize;          // the thorn's cube side, u: the sniper's target
        public float RingRadius;
        public int PlainEvery;           // 0 = every thorn super-shielded, 1 = every thorn plain, 2 = every second
        public int Crystals;             // Time crystals on the edge line of a leg
    }

    /// <summary>A thorn: one big block every Shrike docks on and slings from.</summary>
    public readonly struct Thorn
    {
        public readonly Vector3 Position;
        public readonly float Size;
        public readonly PrismKind Kind;

        public Thorn(Vector3 position, float size, PrismKind kind)
        {
            Position = position;
            Size = size;
            Kind = kind;
        }
    }

    /// <summary>
    /// Thornline, the SHRIKE's Time race (THORNLINE.md). The Shrike cruises at 18 u/s, so the race is its Void Sling: aim
    /// down the weapons' axis at a block within 1,600 u, tether out, and fly there in one second whatever the distance,
    /// then wait out the sling's cooldown docked (x1.6 on a plain block, x2.5 on super-shielded mass). The course is a
    /// chain of THORNS - one big block each - with a ring at the middle of every leg, so threading a ring IS hitting the
    /// next thorn.
    ///
    /// <para><b>What the race asks, in the Shrike's own kit.</b> Leg length costs nothing (the sling is one second at any
    /// distance), so the clock is the cooldown and the aim:</para>
    /// <list type="number">
    /// <item><b>I1, the sling:</b> big super-shielded thorns (the fast x2.5 reload), gentle bends, wide rings.</item>
    /// <item><b>I2, the reload:</b> every second thorn is plain (x1.6), and three Time crystals sit on the EDGE line of
    /// a leg - aim at the thorn's rim, not its centre, and the crystal resets the sling.</item>
    /// <item><b>I3, the snipe:</b> three gentle bends where the thorn after next lines up through BOTH rings: hit a
    /// target 1,200-1,450 u away and skip a whole reload, or dock on the near thorn and wait.</item>
    /// <item><b>I4, the larder:</b> every thorn plain, the smallest faces, the longest legs, the sharpest bends.</item>
    /// </list>
    ///
    /// <para><b>Everything a peer lays is a function of the broadcast rings.</b> Rings sit at the MIDDLE of each leg, so a
    /// thorn is the intersection of two consecutive rings' lines (<see cref="ThornsFor"/>), and the crystals and the
    /// thorns' kinds follow from the thorns and the intensity. The vessel is untouched: the sling, the dock bonus and the
    /// crystal reset are the shipped kit.</para>
    /// </summary>
    public static class ThornlineCourse
    {
        // ── The vessel this course is cut for (read back off ShrikeConfig.asset by ThornlineCourseTests) ──

        /// <summary>`slingRange` on ShrikeConfig.asset: the farthest block the sling can anchor on.</summary>
        public const float SlingRange = 1600f;

        /// <summary>`slingSeconds`: a sling takes this long at any distance.</summary>
        public const float SlingSeconds = 1f;

        /// <summary>`slingCooldown` at Time 0 and at Time 10.</summary>
        public const float SlingCooldownTimeZero = 6f;
        public const float SlingCooldownTimeTen = 2.5f;

        /// <summary>`dockBonus` / `dockBonusShielded`: how much faster the cooldown runs docked.</summary>
        public const float DockBonus = 1.6f;
        public const float DockBonusShielded = 2.5f;

        /// <summary>`hullRadius` on ShrikeConfig.asset.</summary>
        public const float HullRadius = 3f;

        // ── The course's own constants ───────────────────────────────────

        /// <summary>Where every thorn sits, from the cell centre.</summary>
        public const float ShellMin = 650f, ShellMax = 1050f;

        /// <summary>The start thorn, on the spawn ring's pole: every pilot snipes it first, from the same distance.</summary>
        public const float StartThornDistance = 760f;
        public const float StartThornScale = 1.5f;

        /// <summary>No leg passes closer than this outside the nucleus shell.</summary>
        public const float CoreMargin = 60f;

        /// <summary>A designated snipe passes each of its rings this far inside the mouth...</summary>
        public const float SkipInside = 12f;

        /// <summary>...and any other line between two thorns misses at least one ring by this much, so no skip is an accident.</summary>
        public const float AccidentMargin = 15f;

        /// <summary>A leg clears every thorn but its own two by this much (a sling docks on the FIRST block it touches).</summary>
        public const float ThornClearance = 20f;

        /// <summary>Ring centres keep this far apart, so no ring reads as two.</summary>
        public const float MinRingSeparation = 160f;

        /// <summary>A snipe's two legs together: inside the sling's range with room to spare.</summary>
        public const float SkipMinSpan = 1150f, SkipMaxSpan = SlingRange - 150f;

        /// <summary>A crystal sits this far along its leg, on the line to a point this far out on the thorn's face.</summary>
        public const float CrystalAlong = 0.8f, CrystalFaceFraction = 0.75f;

        /// <summary>The gate races' nucleus fallback (RaceCourseSource.DefaultInnerRadiusFallback; the tests hold the two equal).</summary>
        public const float DefaultInnerRadius = 480f;

        const int CandidatesPerStep = 48;
        const int Attempts = 64;

        public static ThornlineSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4) - 1;
            return new ThornlineSettings
            {
                RingCount = 16,
                InnerRadius = DefaultInnerRadius,
                MinLeg = new[] { 550f, 600f, 650f, 750f }[i],
                MaxLeg = new[] { 750f, 850f, 950f, 1150f }[i],
                MinBend = new[] { 35f, 30f, 30f, 35f }[i],
                MaxBend = new[] { 50f, 60f, 75f, 95f }[i],
                SkipBends = new[] { 0, 0, 3, 3 }[i],
                ThornSize = new[] { 56f, 46f, 40f, 32f }[i],
                RingRadius = new[] { 64f, 56f, 50f, 44f }[i],
                PlainEvery = new[] { 0, 2, 2, 1 }[i],
                Crystals = new[] { 0, 3, 3, 3 }[i],
            };
        }

        /// <summary>The seconds one leg costs a pilot who slings the moment the cooldown is up: the one-second sling at
        /// rate 1, then the rest of the cooldown docked. Distance is not in it.</summary>
        public static float LegSeconds(float cooldown, bool shieldedDock) =>
            SlingSeconds + Mathf.Max(0f, cooldown - SlingSeconds) / (shieldedDock ? DockBonusShielded : DockBonus);

        /// <summary>The sling's cooldown at a Time level (0-10), the shipped linear dial.</summary>
        public static float CooldownAtTime(float timeLevel) =>
            Mathf.Lerp(SlingCooldownTimeZero, SlingCooldownTimeTen, Mathf.Clamp01(timeLevel / 10f));

        public static float HalfDiagonal(float size) => size * 0.8660254f;

        public static PrismKind KindFor(int thornIndex, int plainEvery) =>
            plainEvery <= 0 ? PrismKind.SuperShielded
            : plainEvery == 1 ? PrismKind.Plain
            : thornIndex % plainEvery == plainEvery - 1 ? PrismKind.Plain : PrismKind.SuperShielded;

        /// <summary>The bends (thorn indices) a snipe is cut at: spread evenly, never the first or the last thorn.</summary>
        public static bool IsSkipBend(int thornIndex, int ringCount, int skipBends)
        {
            if (skipBends <= 0) return false;
            for (int s = 0; s < skipBends; s++)
                if (thornIndex == Mathf.RoundToInt((s + 1f) * ringCount / (skipBends + 1f))) return true;
            return false;
        }

        // ── The walk ─────────────────────────────────────────────────────

        /// <summary>The course's rings, cell-local, or null when no attempt fits. <paramref name="thorns"/> receives the
        /// thorn chain (thorn 0 is the start thorn; ring k sits at the middle of thorn k-1 to thorn k).</summary>
        public static List<RaceGate> Generate(int seed, in ThornlineSettings s, List<Vector3> thorns)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var rng = new RaceCourseGeometry.Rng(unchecked(seed * 7919 + attempt * 104729 + 17));
                if (TryWalk(ref rng, s, thorns)) return RingsFor(thorns, s.RingRadius);
            }
            thorns.Clear();
            return null;
        }

        static bool TryWalk(ref RaceCourseGeometry.Rng rng, in ThornlineSettings s, List<Vector3> t)
        {
            t.Clear();
            Vector3 start = Vector3.up * StartThornDistance;
            t.Add(start);
            // The first leg leaves the pole sideways: the spawn ring is below it, and the line must stay in the shell.
            Vector3 dir = RaceCourseGeometry.SafeNormalize(
                RaceCourseGeometry.RotateAbout(Vector3.forward, Vector3.up, rng.Range(0f, 2f * Mathf.PI)), Vector3.forward);
            dir = RaceCourseGeometry.SafeNormalize(dir + Vector3.up * 0.15f, dir);

            int k = 1;
            while (k <= s.RingCount)
            {
                bool placed = false;
                if (IsSkipBend(k, s.RingCount, s.SkipBends) && k < s.RingCount)
                {
                    for (int c = 0; c < CandidatesPerStep && !placed; c++)
                        placed = TrySkip(ref rng, s, t, dir, out dir);
                    if (placed) { k += 2; continue; }
                    return false;
                }
                for (int c = 0; c < CandidatesPerStep && !placed; c++)
                {
                    Vector3 d = Bend(ref rng, dir, rng.Range(s.MinBend, s.MaxBend));
                    Vector3 next = t[t.Count - 1] + d * rng.Range(s.MinLeg, s.MaxLeg);
                    if (!Fits(s, t, next)) continue;
                    t.Add(next);
                    if (!NoAccidents(s, t)) { t.RemoveAt(t.Count - 1); continue; }
                    dir = d;
                    placed = true;
                }
                if (!placed) return false;
                k++;
            }
            return t.Count == s.RingCount + 1 && Valid(s, t, out _);
        }

        /// <summary>A snipe: thorn C 1,150-1,450 u down a gently bent line, thorn B off its middle by enough to stay off
        /// the line and little enough that both legs' mid-rings sit inside it.</summary>
        static bool TrySkip(ref RaceCourseGeometry.Rng rng, in ThornlineSettings s, List<Vector3> t, Vector3 dir, out Vector3 outDir)
        {
            outDir = dir;
            Vector3 a = t[t.Count - 1];
            Vector3 d = Bend(ref rng, dir, rng.Range(s.MinBend, s.MaxBend));
            float span = rng.Range(SkipMinSpan, SkipMaxSpan);
            Vector3 c = a + d * span;
            float minOff = HalfDiagonal(s.ThornSize) + HullRadius + ThornClearance;
            float maxOff = 2f * (s.RingRadius - SkipInside);
            if (minOff > maxOff) return false;
            Vector3 side = RaceCourseGeometry.SafeNormalize(
                RaceCourseGeometry.RotateAbout(RaceCourseGeometry.Perpendicular(d), d, rng.Range(0f, 2f * Mathf.PI)),
                RaceCourseGeometry.Perpendicular(d));
            Vector3 b = (a + c) * 0.5f + side * rng.Range(minOff, maxOff);
            float ab = (b - a).magnitude, bc = (c - b).magnitude;
            float minLeg = SkipMinSpan * 0.45f;
            if (ab < minLeg || bc < minLeg) return false;
            if (!Fits(s, t, b, minLeg)) return false;
            t.Add(b);
            if (!Fits(s, t, c, minLeg)) { t.RemoveAt(t.Count - 1); return false; }
            t.Add(c);
            if (!NoAccidents(s, t)) { t.RemoveRange(t.Count - 2, 2); return false; }
            outDir = RaceCourseGeometry.SafeNormalize(c - b, d);
            return true;
        }

        static Vector3 Bend(ref RaceCourseGeometry.Rng rng, Vector3 dir, float degrees)
        {
            Vector3 axis = RaceCourseGeometry.RotateAbout(RaceCourseGeometry.Perpendicular(dir), dir,
                                                          rng.Range(0f, 2f * Mathf.PI));
            return RaceCourseGeometry.SafeNormalize(RaceCourseGeometry.RotateAbout(dir, axis, degrees * Mathf.Deg2Rad), dir);
        }

        /// <summary>Can <paramref name="next"/> follow the chain: in the shell, its leg clear of the core and of every
        /// earlier thorn, no earlier leg passing through it, and its ring clear of every earlier ring.</summary>
        static bool Fits(in ThornlineSettings s, List<Vector3> t, Vector3 next) => Fits(s, t, next, s.MinLeg * 0.9f);

        static bool Fits(in ThornlineSettings s, List<Vector3> t, Vector3 next, float minLeg)
        {
            float r = next.magnitude;
            if (r < ShellMin || r > ShellMax) return false;
            Vector3 prev = t[t.Count - 1];
            float leg = (next - prev).magnitude;
            if (leg < minLeg || leg > SlingRange - 100f) return false;
            if (DistanceToSegment(Vector3.zero, prev, next) < s.InnerRadius + CoreMargin) return false;
            float clear = HalfDiagonal(s.ThornSize) * StartThornScale + HullRadius + ThornClearance;
            for (int i = 0; i < t.Count - 1; i++)
                if (DistanceToSegment(t[i], prev, next) < clear) return false;            // the new leg hits an old thorn
            for (int i = 1; i < t.Count; i++)
                if (DistanceToSegment(next, t[i - 1], t[i]) < clear) return false;        // an old leg hits the new thorn
            Vector3 ring = (prev + next) * 0.5f;
            for (int i = 1; i < t.Count; i++)
                if (((t[i - 1] + t[i]) * 0.5f - ring).magnitude < MinRingSeparation) return false;
            return true;
        }

        /// <summary>No line between two thorns two or more apart threads every ring between them unless the bend between
        /// them is a designated snipe - and a designated snipe threads both with room.</summary>
        static bool NoAccidents(in ThornlineSettings s, List<Vector3> t)
        {
            int j = t.Count - 1;
            for (int i = j - 2; i >= 0; i--)
            {
                if ((t[j] - t[i]).magnitude > SlingRange) continue;
                bool designated = j - i == 2 && IsSkipBend(i + 1, s.RingCount, s.SkipBends);
                float worst = 0f;
                for (int k = i + 1; k <= j; k++)
                    worst = Mathf.Max(worst, DistanceToSegment((t[k - 1] + t[k]) * 0.5f, t[i], t[j]));
                if (designated ? worst > s.RingRadius - SkipInside : worst < s.RingRadius + AccidentMargin) return false;
            }
            return true;
        }

        /// <summary>Re-checks a whole chain (the tests' oracle, and the walk's last word).</summary>
        public static bool Valid(in ThornlineSettings s, List<Vector3> t, out string why)
        {
            why = null;
            var scratch = new List<Vector3> { t[0] };
            for (int i = 1; i < t.Count; i++)
            {
                bool snipeLeg = IsSkipBend(i, s.RingCount, s.SkipBends) || IsSkipBend(i - 1, s.RingCount, s.SkipBends);
                if (!Fits(s, scratch, t[i], snipeLeg ? SkipMinSpan * 0.45f : s.MinLeg * 0.9f)) { why = $"thorn {i} does not fit"; return false; }
                scratch.Add(t[i]);
                if (!NoAccidents(s, scratch)) { why = $"thorn {i} opens an accidental skip or spoils a snipe"; return false; }
            }
            return true;
        }

        // ── What every peer derives from the broadcast rings ─────────────

        public static List<RaceGate> RingsFor(List<Vector3> thorns, float radius)
        {
            var rings = new List<RaceGate>(thorns.Count - 1);
            for (int k = 1; k < thorns.Count; k++)
                rings.Add(new RaceGate((thorns[k - 1] + thorns[k]) * 0.5f,
                                       RaceCourseGeometry.SafeNormalize(thorns[k] - thorns[k - 1], Vector3.forward), radius));
            return rings;
        }

        /// <summary>The thorn chain back from the rings. Each inner thorn is where two consecutive rings' lines meet; the
        /// ends follow from the rings sitting at the middle of their legs.</summary>
        public static void ThornsFor(IReadOnlyList<RaceGate> rings, List<Vector3> thorns)
        {
            thorns.Clear();
            int n = rings.Count;
            if (n == 0) return;
            var inner = new Vector3[n + 1];
            for (int k = 1; k < n; k++)
                inner[k] = Meet(rings[k - 1].Position, rings[k - 1].Axis, rings[k].Position, rings[k].Axis);
            if (n == 1)
            {
                inner[0] = rings[0].Position - rings[0].Axis * StartThornDistance * 0.5f;
                inner[1] = rings[0].Position + rings[0].Axis * StartThornDistance * 0.5f;
            }
            else
            {
                inner[0] = 2f * rings[0].Position - inner[1];
                inner[n] = 2f * rings[n - 1].Position - inner[n - 1];
            }
            thorns.AddRange(inner);
        }

        /// <summary>The midpoint of the closest approach of two lines (exact for lines that meet).</summary>
        static Vector3 Meet(Vector3 p, Vector3 u, Vector3 q, Vector3 v)
        {
            Vector3 w = p - q;
            float a = Vector3.Dot(u, u), b = Vector3.Dot(u, v), c = Vector3.Dot(v, v);
            float d = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
            float den = a * c - b * b;
            if (Mathf.Abs(den) < 1e-6f) return (p + q) * 0.5f;
            float sc = (b * e - c * d) / den, tc = (a * e - b * d) / den;
            return ((p + u * sc) + (q + v * tc)) * 0.5f;
        }

        /// <summary>The thorns as laid: size and kind per index (the start thorn is bigger and always super-shielded).</summary>
        public static void Lay(List<Vector3> thornPositions, in ThornlineSettings s, List<Thorn> thorns)
        {
            thorns.Clear();
            for (int i = 0; i < thornPositions.Count; i++)
                thorns.Add(i == 0
                    ? new Thorn(thornPositions[i], s.ThornSize * StartThornScale, PrismKind.SuperShielded)
                    : new Thorn(thornPositions[i], s.ThornSize, KindFor(i, s.PlainEvery)));
        }

        /// <summary>The Time crystals: on legs spread through the race (never a snipe's two legs), each on the line from
        /// the leg's start to a point <see cref="CrystalFaceFraction"/> of the way out on the thorn's face, so the pilot
        /// who aims at the RIM takes it and the one who aims at the centre does not.</summary>
        public static void PlaceCrystals(List<Vector3> thorns, in ThornlineSettings s, List<Vector3> crystals)
        {
            crystals.Clear();
            int n = thorns.Count - 1;
            if (s.Crystals <= 0 || n < 2) return;
            for (int c = 0; c < s.Crystals; c++)
            {
                int first = Mathf.Clamp(Mathf.RoundToInt((c + 0.5f) * n / s.Crystals) + 1, 1, n);
                for (int leg = first; leg <= n; leg++)
                {
                    if (IsSkipBend(leg, s.RingCount, s.SkipBends) || IsSkipBend(leg - 1, s.RingCount, s.SkipBends)) continue;
                    Vector3 crystal = CrystalOn(thorns, leg, s.ThornSize);
                    if (!ClearOfOtherLines(thorns, leg, crystal, 0.35f * 0.5f * s.ThornSize)) continue;
                    crystals.Add(crystal);
                    break;
                }
            }
        }

        static Vector3 CrystalOn(List<Vector3> thorns, int leg, float thornSize)
        {
            Vector3 a = thorns[leg - 1], b = thorns[leg];
            Vector3 along = RaceCourseGeometry.SafeNormalize(b - a, Vector3.forward);
            // A fixed, seed-free side: the crystals follow from the thorns alone, so every peer agrees.
            Vector3 side = RaceCourseGeometry.SafeNormalize(Vector3.Cross(along, Vector3.up), RaceCourseGeometry.Perpendicular(along));
            if ((leg & 1) == 1) side = -side;
            Vector3 rim = b + side * (0.5f * thornSize * CrystalFaceFraction);
            return Vector3.Lerp(a, rim, CrystalAlong);
        }

        /// <summary>No OTHER leg's centre line passes the crystal: only aiming this leg's rim takes it.</summary>
        static bool ClearOfOtherLines(List<Vector3> thorns, int leg, Vector3 crystal, float clear)
        {
            for (int k = 1; k < thorns.Count; k++)
                if (k != leg && DistanceToSegment(crystal, thorns[k - 1], thorns[k]) < clear) return false;
            return true;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
