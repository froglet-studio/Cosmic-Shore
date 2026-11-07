using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>What a stretch of rail is made of. The rails were all <see cref="SuperShielded"/> until 2026-11-06; the
    /// other kinds are what make a stretch CONTESTED (Garrett: "not all supershielded").</summary>
    public enum RegattaRailKind
    {
        /// <summary>Today's rail: no cone, gun, plate or spike removes it; only the Rhino's energised sword opens it.</summary>
        SuperShielded = 0,
        /// <summary>A shielded prism: harder to cut than plain, easier than super-shielded.</summary>
        Shielded = 1,
        /// <summary>A plain prism: any ability that cuts mass cuts it, so a rider's stretch can be shot out from under it
        /// (a destroyed prism still rides at the rider's destroyed-terrain speed, URCHIN_TRAIL_RIDER.md "Holes").</summary>
        Plain = 2,
        /// <summary>No rail at all. A gap SPLITS the lane into separate trails, so a rider runs off the end and flies
        /// free across it (a hole inside one trail would be bridged at full pace instead).</summary>
        Gap = 3,
    }

    /// <summary>
    /// One stretch of one leg's rails (all three lanes together, so the braid stays fair). Measured as a share of the
    /// leg's SPINE arc, gate <see cref="Leg"/> to gate <see cref="Leg"/>+1: 0 is the ring the leg leaves, 1 the ring it
    /// arrives at. Where two sections overlap the later one in the list wins.
    /// </summary>
    [Serializable]
    public struct RegattaRailSection
    {
        [Tooltip("The leg (0 = gate 0 to gate 1). -1 = every leg.")]
        public int Leg;

        [Tooltip("Start of the stretch, as a share of the leg's spine arc (0 = the ring the leg leaves).")]
        public float From;

        [Tooltip("End of the stretch, as a share of the leg's spine arc (1 = the ring the leg arrives at).")]
        public float To;

        [Tooltip("What the stretch is made of. Gap lays nothing and splits the lane.")]
        public RegattaRailKind Kind;

        [Tooltip("Lay the stretch in RegattaCourse.NeutralDomain, no team's colour: an Urchin leaves its rail and flies " +
                 "free, a Squirrel still skims it.")]
        public bool Neutral;

        [Tooltip("How far (u) the whole braid swings OFF the racing line at the middle of the stretch, a sine-squared bow " +
                 "that is zero at both ends, so the rails still thread every ring at the lane offset. A rider rides the " +
                 "longer way round; a free flyer may cut along the spine. 0 = on the line.")]
        public float Detour;

        [Tooltip("Which way the braid swings, degrees round the spine's tangent from the rail frame's normal.")]
        public float DetourBearing;

        public RegattaRailSection(int leg, float from, float to, RegattaRailKind kind = RegattaRailKind.SuperShielded,
                                  bool neutral = false, float detour = 0f, float detourBearing = 0f)
        {
            Leg = leg; From = from; To = to; Kind = kind; Neutral = neutral; Detour = detour; DetourBearing = detourBearing;
        }

        public bool Covers(int leg, float share) =>
            (Leg < 0 || Leg == leg) && To > From && share >= From && share < To;
    }

    /// <summary>
    /// A second ring that threads the same gate: either the course ring or this one counts. It stands in the course
    /// ring's plane, facing the same way, <see cref="Offset"/> from its centre - so the rails (which thread only the
    /// course ring) and the free line can part there. Route choice, not a shortcut by construction: whether it is
    /// shorter depends on where the legs either side point.
    /// </summary>
    [Serializable]
    public struct RegattaAlternateGate
    {
        [Tooltip("The course ring (0-7) this alternate stands beside.")]
        public int Ring;

        [Tooltip("Centre-to-centre distance from the course ring, in its plane (u). Pushed out to clear both mouths by " +
                 "RegattaCourseFeatures.MinAlternateClearance when it is nearer.")]
        public float Offset;

        [Tooltip("Degrees round the ring's axis from the corner's INSIDE (0 = toward where the legs either side bend).")]
        public float Bearing;

        [Tooltip("The alternate's mouth as a share of the course ring's.")]
        public float RadiusScale;

        public RegattaAlternateGate(int ring, float offset, float bearing, float radiusScale = 1f)
        {
            Ring = ring; Offset = offset; Bearing = bearing; RadiusScale = radiusScale;
        }
    }

    /// <summary>Crystals laid ON the racing line (the spine), evenly round the lap. Garrett: "use sparingly ... likely
    /// noise", so the count is capped (<see cref="RegattaCourseFeatures.MaxLineCrystalsPerLap"/>). They replace the
    /// nucleus crystals while on (CrystalManager course keys), and no AI detours for one
    /// (<c>GateRaceController.AIDetoursForCrystals</c> stays false).</summary>
    [Serializable]
    public struct RegattaLineCrystals
    {
        [Tooltip("Crystals per lap on the spine. 0 = none (the nucleus crystals, as before).")]
        public int PerLap;

        [Tooltip("Where the first one sits, as a share of the lap's spine arc from gate 0.")]
        public float Phase;
    }

    /// <summary>
    /// Everything about a Regatta course beyond its rings: what each stretch of rail is made of and what colour, where
    /// the rail is missing, where the braid leaves the line, which gates have a second ring, and the crystals on the
    /// line. <b>An empty one is the course as it was before any of this existed</b>, prism for prism - every pure call
    /// below returns today's answer for it, and the course harness and the Python port hold that.
    ///
    /// <para>The search space for the Regatta course search (Docs/AI_SYSTEM/REGATTA_BALANCE_PLAN.md, brief 10 of the
    /// 2026-11-06 plan): Tools/Compute/evaluators/regatta_features.py ports every function here and prices each feature
    /// for each hull's model.</para>
    /// </summary>
    [Serializable]
    public sealed class RegattaCourseFeatures
    {
        /// <summary>The cap on <see cref="RegattaLineCrystals.PerLap"/>: one per leg at most, and "sparingly" means fewer.</summary>
        public const int MaxLineCrystalsPerLap = 4;

        /// <summary>The fewest missing prisms that are laid as a gap (two prisms, 16 u); a shorter one would be a hole the
        /// rider never notices, so it is laid as rail rather than split a trail for nothing.</summary>
        public const int MinGapPrisms = 2;

        /// <summary>The least rim-to-rim distance (u) between a course ring and its alternate; a nearer
        /// <see cref="RegattaAlternateGate.Offset"/> is pushed out to it.</summary>
        public const float MinAlternateClearance = 20f;

        /// <summary>The colour of a neutral stretch: not one of <see cref="RegattaCourse.LaneDomains"/>, so it is no
        /// team's lane (the same Blue the overnight branch's neutral share used).</summary>
        public const Domains NeutralDomain = Domains.Blue;

        /// <summary>A benchmark's or a test's features for every Regatta arena, or null for each prefab's own. Read where
        /// the arena lays its rails and where the controller hangs its rings; nothing in a match sets it.</summary>
        public static RegattaCourseFeatures Override;

        [Tooltip("Stretches of rail that differ from the super-shielded, team-coloured default. Later entries win.")]
        public List<RegattaRailSection> Sections = new();

        [Tooltip("Gates with a second ring: either one counts.")]
        public List<RegattaAlternateGate> AlternateGates = new();

        [Tooltip("Crystals on the racing line.")]
        public RegattaLineCrystals Crystals;

        public static RegattaCourseFeatures None => new();

        public bool IsEmpty =>
            (Sections == null || Sections.Count == 0) && (AlternateGates == null || AlternateGates.Count == 0)
            && Crystals.PerLap <= 0;

        public bool HasSections => Sections is { Count: > 0 };

        /// <summary>The branch-era lever (claude/regatta-overnight-balance's NeutralShareFor): the middle
        /// <paramref name="share"/> of every leg in no team's colour, as one section.</summary>
        public static RegattaCourseFeatures NeutralMiddle(float share)
        {
            var f = new RegattaCourseFeatures();
            float s = Mathf.Clamp01(share);
            if (s > 0f) f.Sections.Add(new RegattaRailSection(-1, 0.5f - 0.5f * s, 0.5f + 0.5f * s, neutral: true));
            return f;
        }

        /// <summary>The section in force at <paramref name="share"/> of leg <paramref name="leg"/>, or false for the
        /// default rail (super-shielded, team colour, on the line).</summary>
        public bool TrySectionAt(int leg, float share, out RegattaRailSection section)
        {
            section = default;
            if (!HasSections) return false;
            bool found = false;
            for (int i = 0; i < Sections.Count; i++)
                if (Sections[i].Covers(leg, share)) { section = Sections[i]; found = true; }
            return found;
        }

        /// <summary>How far the braid stands off the line at <paramref name="share"/> of leg <paramref name="leg"/>:
        /// the sine-squared bow of the section in force, zero outside every detour.</summary>
        public float DetourAt(int leg, float share, out float bearingDegrees)
        {
            bearingDegrees = 0f;
            if (!TrySectionAt(leg, share, out var s) || s.Detour == 0f || s.Kind == RegattaRailKind.Gap) return 0f;
            float u = (share - s.From) / (s.To - s.From);
            float bow = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
            bearingDegrees = s.DetourBearing;
            return s.Detour * bow * bow;
        }

        /// <summary>
        /// The second ring at course ring <paramref name="ring"/>, or false when that gate has none. A pure function of
        /// the course ring and its two neighbours, so every peer derives the same ring from the course it was sent.
        /// </summary>
        public bool TryAlternate(IReadOnlyList<RaceGate> gates, int ring, out RaceGate alternate)
        {
            alternate = default;
            int n = gates?.Count ?? 0;
            if (n < 3 || AlternateGates == null) return false;
            for (int i = 0; i < AlternateGates.Count; i++)
            {
                var a = AlternateGates[i];
                if (((a.Ring % n) + n) % n != ring || a.Offset <= 0f) continue;
                alternate = AlternateFor(gates, ring, a);
                return true;
            }
            return false;
        }

        /// <summary>The ring <paramref name="spec"/> stands at <paramref name="ring"/>: see <see cref="RegattaAlternateGate"/>.</summary>
        public static RaceGate AlternateFor(IReadOnlyList<RaceGate> gates, int ring, RegattaAlternateGate spec)
        {
            int n = gates.Count;
            var g = gates[ring];
            Vector3 axis = RaceCourseGeometry.SafeNormalize(g.Axis, Vector3.forward);
            Vector3 mid = (gates[(ring - 1 + n) % n].Position + gates[(ring + 1) % n].Position) * 0.5f - g.Position;
            Vector3 inside = mid - axis * Vector3.Dot(mid, axis);
            inside = RaceCourseGeometry.SafeNormalize(inside, RaceCourseGeometry.Perpendicular(axis));
            Vector3 dir = RaceCourseGeometry.RotateAbout(inside, axis, spec.Bearing * Mathf.Deg2Rad);
            float scale = spec.RadiusScale > 0f ? spec.RadiusScale : 1f;
            float radius = g.Radius * scale;
            // Never overlapping the course ring's mouth, whatever a search asks for: the two would be one hole.
            float offset = Mathf.Max(spec.Offset, g.Radius + radius + MinAlternateClearance);
            return new RaceGate(g.Position + dir * offset, axis, radius);
        }

        /// <summary>
        /// Where the line crystals sit: <see cref="RegattaLineCrystals.PerLap"/> points on the closed spine, evenly by
        /// arc from <see cref="RegattaLineCrystals.Phase"/>, capped at <see cref="MaxLineCrystalsPerLap"/>. Empty when
        /// off or when the layout carries no spine.
        /// </summary>
        public List<Vector3> LineCrystalPoints(RegattaRailLayout layout)
        {
            var points = new List<Vector3>();
            int count = Mathf.Min(Crystals.PerLap, MaxLineCrystalsPerLap);
            var spine = layout?.SpinePositions;
            var arc = layout?.SpineArc;
            if (count <= 0 || spine == null || arc == null || spine.Length < 2 || layout.SpineLength < 1f) return points;
            float phase = Crystals.Phase - Mathf.FloorToInt(Crystals.Phase);
            for (int k = 0; k < count; k++)
            {
                float target = (phase + k / (float)count) % 1f * layout.SpineLength;
                int m = spine.Length;
                int j = 0;
                while (j < m - 1 && arc[j + 1] < target) j++;
                float seg = arc[j + 1] - arc[j];
                float u = seg > 1e-6f ? Mathf.Clamp01((target - arc[j]) / seg) : 0f;
                points.Add(Vector3.Lerp(spine[j], spine[(j + 1) % m], u));
            }
            return points;
        }

        /// <summary>A stable hash of every setting, for the arena's build cache.</summary>
        public int ParameterHash()
        {
            int h = 17;
            if (Sections != null)
                foreach (var s in Sections)
                    h = HashMix(HashMix(HashMix(HashMix(h, s.Leg), s.From.GetHashCode()), s.To.GetHashCode()),
                                HashMix(HashMix(HashMix((int)s.Kind, s.Neutral ? 1 : 0), s.Detour.GetHashCode()),
                                        s.DetourBearing.GetHashCode()));
            if (AlternateGates != null)
                foreach (var a in AlternateGates)
                    h = HashMix(HashMix(HashMix(HashMix(h, a.Ring), a.Offset.GetHashCode()), a.Bearing.GetHashCode()),
                                a.RadiusScale.GetHashCode());
            return HashMix(HashMix(h, Crystals.PerLap), Crystals.Phase.GetHashCode());
        }

        static int HashMix(int a, int b) => unchecked(a * 31 + b);
    }

    /// <summary>
    /// What each prism of one lane is (kind, colour) and how the lane is cut into trails: the arena's plan, a pure
    /// function of the lane's <see cref="RegattaRailLayout.LaneLegParams"/> and the features, so a test (and the Python
    /// port) can hold it without laying anything. No gap: one closed run of every prism, all super-shielded in the team
    /// colour - the lane as it was.
    /// </summary>
    public sealed class RegattaRailPlan
    {
        public struct Run
        {
            public int Start;     // first prism index (the lane's own numbering)
            public int Count;     // prisms, counted on from Start round the loop
            public bool Closed;   // true only for a lane with no gap
        }

        public PrismKind[] Kinds;
        public bool[] Neutral;
        public bool[] Gap;
        public readonly List<Run> Runs = new();

        public int LaidCount
        {
            get { int c = 0; foreach (var r in Runs) c += r.Count; return c; }
        }

        public static RegattaRailPlan ForLane(float[] legParams, int legs, RegattaCourseFeatures features)
        {
            int n = legParams?.Length ?? 0;
            var plan = new RegattaRailPlan { Kinds = new PrismKind[n], Neutral = new bool[n], Gap = new bool[n] };
            bool any = false;
            for (int k = 0; k < n; k++)
            {
                plan.Kinds[k] = PrismKind.SuperShielded;
                if (features == null || !features.HasSections) continue;
                int leg = Mathf.Clamp((int)legParams[k], 0, Mathf.Max(0, legs - 1));
                if (!features.TrySectionAt(leg, legParams[k] - leg, out var s)) continue;
                plan.Neutral[k] = s.Neutral;
                plan.Gap[k] = s.Kind == RegattaRailKind.Gap;
                plan.Kinds[k] = s.Kind switch
                {
                    RegattaRailKind.Shielded => PrismKind.Shielded,
                    RegattaRailKind.Plain => PrismKind.Plain,
                    _ => PrismKind.SuperShielded,
                };
                any |= plan.Gap[k];
            }

            // A gap shorter than MinGapPrisms is laid as rail: it would only be a hole the rider bridges.
            if (any)
                for (int k = 0; k < n; k++)
                {
                    if (!plan.Gap[k] || plan.Gap[(k - 1 + n) % n]) continue;
                    int len = 0;
                    while (len < n && plan.Gap[(k + len) % n]) len++;
                    if (len >= RegattaCourseFeatures.MinGapPrisms || len >= n) continue;
                    for (int q = 0; q < len; q++) plan.Gap[(k + q) % n] = false;
                }

            int first = -1;
            for (int k = 0; k < n; k++)
                if (!plan.Gap[k] && plan.Gap[(k - 1 + n) % n]) { first = k; break; }
            if (first < 0)
            {
                bool allGap = n > 0 && plan.Gap[0];
                if (n > 0 && !allGap) plan.Runs.Add(new Run { Start = 0, Count = n, Closed = true });
                return plan;
            }
            for (int q = 0; q < n;)
            {
                int k = (first + q) % n;
                if (plan.Gap[k]) { q++; continue; }
                int count = 0;
                while (q + count < n && !plan.Gap[(first + q + count) % n]) count++;
                plan.Runs.Add(new Run { Start = k, Count = count, Closed = false });
                q += count;
            }
            return plan;
        }
    }

    /// <summary>The AI's route rule at a gate with an alternate ring, pure so the offline model applies the same one
    /// (Tools/Compute/evaluators/regatta_features.py <c>takes_alternate</c>).</summary>
    public static class RegattaAlternateChoice
    {
        /// <summary>True when here -> <paramref name="alternate"/> -> <paramref name="next"/> is shorter than here ->
        /// <paramref name="ring"/> -> <paramref name="next"/> by more than <paramref name="margin"/> of the latter.</summary>
        public static bool TakesAlternate(Vector3 self, Vector3 ring, Vector3 alternate, Vector3 next, float margin)
        {
            float viaRing = (ring - self).magnitude + (next - ring).magnitude;
            float viaAlternate = (alternate - self).magnitude + (next - alternate).magnitude;
            return viaAlternate < viaRing * (1f - Mathf.Clamp01(margin));
        }
    }
}
