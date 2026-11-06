using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// WHERE Regatta's lifeforms may live, relative to the course: how many prepared planting sites
    /// each leg offers, how far from the spine they sit, on which side, and how far they keep from the
    /// rings - plus the tube round the spine the cell's fauna keep out of. Serialized on
    /// <see cref="SpawnableRegattaRails"/> (one prefab per course) and written by
    /// <c>Tools/Build/author_regatta_assets.py</c> from <c>Tools/Build/regatta_lifeforms.py</c>.
    ///
    /// <para><b>The defaults are today's course exactly</b>: no entry in <see cref="sitesPerLeg"/> means
    /// no site is sown, and a zero <see cref="faunaCorridorRadius"/> means no corridor, so a prefab that
    /// authors nothing lays the rails and nothing else.</para>
    ///
    /// <para><b>The arena never spawns a lifeform</b> (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md: the Cell
    /// owns the ecology). The sites are prepared ground handed to the Cell's ordinary flora spawner
    /// (<see cref="Cell.TryTakePlantingSite(FloraSiteKind, out Vector3, out Vector3)"/>), WHICH species
    /// and HOW MANY are the cell config's spawn profile, and every plant grows, is grazed, withers to
    /// its heart and is re-seeded by the same law as in any other cell.</para>
    /// </summary>
    [Serializable]
    public sealed class RegattaLifeformSiteSettings
    {
        [Tooltip("Planting sites sown on each leg, in gate order (leg i runs from ring i to ring i+1). " +
                 "A missing entry is 0. Empty = no sites (today's course). The cell's spawn profile says " +
                 "how many plants of which species take them; a site count above the plant count is " +
                 "spare ground a grazed plant's replacement can root in.")]
        public int[] sitesPerLeg = Array.Empty<int>();

        [Tooltip("Nearest a site sits to the spine (u). Never nearer than the rails' lane offset plus " +
                 "RegattaLifeformSites.MinRailClearance, whatever is authored: a plant must never root on " +
                 "a rider's rail.")]
        [Min(0f)] public float offsetMin = 90f;

        [Tooltip("Farthest a site sits from the spine (u).")]
        [Min(0f)] public float offsetMax = 160f;

        [Tooltip("Centre of the side window, in degrees round the spine's tangent: 0 = straight out from " +
                 "the cell centre, 180 = in toward the nucleus, 90 / 270 = the two sides across the circuit's plane.")]
        [Range(0f, 360f)] public float sideDegrees = 0f;

        [Tooltip("Half-width of the side window (degrees). 180 = all round the spine.")]
        [Range(0f, 180f)] public float sideSpreadDegrees = 180f;

        [Tooltip("Where along each leg sites may sit (0 = at the leg's first ring, 1 = at its last).")]
        [Range(0f, 1f)] public float legWindowStart = 0.2f;

        [Tooltip("See legWindowStart.")]
        [Range(0f, 1f)] public float legWindowEnd = 0.8f;

        [Tooltip("No site within a ring's mouth radius plus this many units of its centre, so a plant can " +
                 "never grow across a mouth.")]
        [Min(0f)] public float ringClearance = 60f;

        [Tooltip("Radius of the tube round the spine the cell's fauna keep out of (FaunaCorridor: born " +
                 "outside it, goals pushed out of it, mass in it not prey - a spatial diet and steering " +
                 "rule, never a wall). 0 = no corridor (today's course; also the setting when fauna MAY " +
                 "cross the racing line).")]
        [Min(0f)] public float faunaCorridorRadius = 0f;

        [Tooltip("How far past the corridor wall a pushed-out goal lands (FaunaCorridor.GoalStandoff).")]
        [Min(0f)] public float faunaCorridorStandoff = 50f;

        public bool HasSites
        {
            get
            {
                if (sitesPerLeg == null) return false;
                for (int i = 0; i < sitesPerLeg.Length; i++)
                    if (sitesPerLeg[i] > 0) return true;
                return false;
            }
        }

        public int SitesOnLeg(int leg) =>
            sitesPerLeg != null && leg >= 0 && leg < sitesPerLeg.Length ? Mathf.Max(0, sitesPerLeg[leg]) : 0;

        public int RequestedSites
        {
            get
            {
                int n = 0;
                if (sitesPerLeg != null)
                    for (int i = 0; i < sitesPerLeg.Length; i++) n += Mathf.Max(0, sitesPerLeg[i]);
                return n;
            }
        }

        /// <summary>Every field that changes what is sown, for the spawnable's build cache.</summary>
        public int ParameterHash()
        {
            int h = HashCode.Combine(offsetMin, offsetMax, sideDegrees, sideSpreadDegrees,
                                     legWindowStart, legWindowEnd, ringClearance);
            h = HashCode.Combine(h, faunaCorridorRadius, faunaCorridorStandoff);
            if (sitesPerLeg != null)
                for (int i = 0; i < sitesPerLeg.Length; i++) h = HashCode.Combine(h, sitesPerLeg[i]);
            return h;
        }
    }

    /// <summary>
    /// The pure placement math behind <see cref="RegattaLifeformSiteSettings"/>: a function of the rings,
    /// the settings, the lane offset and a seed, with no Unity randomness, no <c>System.Random</c> and no
    /// scene access (the <see cref="RaceCourseGeometry"/> rule), so every peer sows the same sites and an
    /// EditMode test can hold the contract. The spine is the same closed Hermite through the rings that
    /// <see cref="RegattaCourse.BuildRails"/> hangs the rails on, re-evaluated here so the course
    /// generator itself is not touched.
    /// </summary>
    public static class RegattaLifeformSites
    {
        /// <summary>A site is never nearer the spine than the lane offset plus this (u): the rails are
        /// 6 u prisms, so 30 u leaves a lane's own width four times over before a plant's root.</summary>
        public const float MinRailClearance = 30f;

        /// <summary>The shell sites may sit in, from the cell centre: outside the nucleus with room, and
        /// inside the 1,200 u membrane with room.</summary>
        public const float ShellInner = RegattaCourse.NucleusWorldRadius * 1.15f;
        public const float ShellOuter = 1150f;

        /// <summary>Spine samples per leg used to keep a site off every OTHER leg's line too.</summary>
        const int GuardSamplesPerLeg = 32;

        /// <summary>The nearest a site may root to the spine for these settings.</summary>
        public static float EffectiveOffsetMin(RegattaLifeformSiteSettings s, float laneOffset) =>
            Mathf.Max(s.offsetMin, laneOffset + MinRailClearance);

        /// <summary>
        /// Sow the sites, cell-local, into <paramref name="into"/> (appended). Returns how many were sown.
        /// A candidate that would sit inside a ring's mouth clearance, nearer ANY leg's spine than
        /// <see cref="EffectiveOffsetMin"/>, or outside the shell is skipped - deterministically, so every
        /// peer skips the same ones. Each site's up is the direction away from the spine, so a plant grows
        /// away from the racing line.
        /// </summary>
        public static int Sow(IReadOnlyList<RaceGate> gates, RegattaLifeformSiteSettings s, float laneOffset,
                              int seed, List<FloraPlantingSite> into)
        {
            int n = gates?.Count ?? 0;
            if (s == null || into == null || n < 3 || !s.HasSites) return 0;

            float minOff = EffectiveOffsetMin(s, laneOffset);
            float maxOff = Mathf.Max(minOff, s.offsetMax);
            float w0 = Mathf.Clamp01(Mathf.Min(s.legWindowStart, s.legWindowEnd));
            float w1 = Mathf.Clamp01(Mathf.Max(s.legWindowStart, s.legWindowEnd));

            var guard = SpineSamples(gates, GuardSamplesPerLeg);
            float guardSqr = minOff * minOff * 0.999f;
            int sown = 0;

            for (int leg = 0; leg < n; leg++)
            {
                int count = s.SitesOnLeg(leg);
                for (int k = 0; k < count; k++)
                {
                    int key = unchecked(seed * 92821 + leg * 1031 + k * 7);
                    // Stratified along the window, so a leg's sites spread out rather than clump.
                    float t = Mathf.Lerp(w0, w1, (k + Hash01(key, 1)) / count);
                    SpineAt(gates, leg, t, out var p, out var tangent);

                    Vector3 outward = p - Vector3.Project(p, tangent);
                    outward = RaceCourseGeometry.SafeNormalize(outward, RaceCourseGeometry.Perpendicular(tangent));
                    Vector3 across = Vector3.Cross(tangent, outward);
                    float theta = (s.sideDegrees + s.sideSpreadDegrees * (2f * Hash01(key, 2) - 1f)) * Mathf.Deg2Rad;
                    Vector3 dir = outward * Mathf.Cos(theta) + across * Mathf.Sin(theta);
                    float off = Mathf.Lerp(minOff, maxOff, Hash01(key, 3));
                    Vector3 site = p + dir * off;

                    if (!Admissible(site, gates, guard, guardSqr, s.ringClearance)) continue;
                    into.Add(new FloraPlantingSite(site, dir, FloraSiteKind.Bed));
                    sown++;
                }
            }
            return sown;
        }

        /// <summary>True when a site clears every ring's mouth, every leg's spine and the shell.</summary>
        public static bool Admissible(Vector3 site, IReadOnlyList<RaceGate> gates, IReadOnlyList<Vector3> spine,
                                      float minSpineDistanceSqr, float ringClearance)
        {
            float r = site.magnitude;
            if (r < ShellInner || r > ShellOuter) return false;
            for (int g = 0; g < gates.Count; g++)
            {
                float keep = gates[g].Radius + ringClearance;
                if ((site - gates[g].Position).sqrMagnitude < keep * keep) return false;
            }
            for (int j = 0; j < spine.Count; j++)
                if (DistanceToSegmentSqr(site, spine[j], spine[(j + 1) % spine.Count]) < minSpineDistanceSqr)
                    return false;
            return true;
        }

        /// <summary>The closed spine, <paramref name="perLeg"/> points per leg - also the fauna corridor's path.</summary>
        public static List<Vector3> SpineSamples(IReadOnlyList<RaceGate> gates, int perLeg)
        {
            int n = gates?.Count ?? 0;
            var pts = new List<Vector3>(Mathf.Max(0, n * perLeg));
            if (n < 3) return pts;
            for (int leg = 0; leg < n; leg++)
                for (int k = 0; k < perLeg; k++)
                {
                    SpineAt(gates, leg, k / (float)perLeg, out var p, out _);
                    pts.Add(p);
                }
            return pts;
        }

        /// <summary>The spine at <paramref name="t"/> along a leg: the same Hermite, tangents scaled by the
        /// chord, that <see cref="RegattaCourse.BuildRails"/> uses.</summary>
        public static void SpineAt(IReadOnlyList<RaceGate> gates, int leg, float t, out Vector3 position, out Vector3 tangent)
        {
            var a = gates[leg];
            var b = gates[(leg + 1) % gates.Count];
            float chord = (b.Position - a.Position).magnitude;
            Vector3 m0 = a.Axis.normalized * chord;
            Vector3 m1 = b.Axis.normalized * chord;
            position = RegattaCourse.Hermite(a.Position, m0, b.Position, m1, t);
            tangent = RaceCourseGeometry.SafeNormalize(RegattaCourse.HermiteDerivative(a.Position, m0, b.Position, m1, t), a.Axis);
        }

        static float DistanceToSegmentSqr(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len < 1e-8f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
            return (p - (a + ab * t)).sqrMagnitude;
        }

        /// <summary>Order-independent hash in [0,1) of a key and a channel.</summary>
        static float Hash01(int key, int channel)
        {
            unchecked
            {
                uint h = (uint)key * 0x9E3779B1u ^ (uint)channel * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h & 0xffffffu) / (float)0x1000000;
            }
        }
    }
}
