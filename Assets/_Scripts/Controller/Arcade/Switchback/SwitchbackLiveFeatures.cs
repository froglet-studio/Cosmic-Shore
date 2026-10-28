using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>A charge ribbon (Plan 2): a wall of plain prisms beside a leg, and the line a full skim runs along.</summary>
    public readonly struct SwitchbackRibbon
    {
        public readonly int Leg;          // the leg from gate Leg to gate Leg + 1
        public readonly Vector3 Start;    // the wall line's two ends (the prisms' centres)
        public readonly Vector3 End;
        public readonly Vector3 Side;     // unit, from the racing line toward the wall

        public SwitchbackRibbon(int leg, Vector3 start, Vector3 end, Vector3 side)
        {
            Leg = leg;
            Start = start;
            End = end;
            Side = side;
        }

        public float Length => (End - Start).magnitude;
    }

    /// <summary>
    /// A shortcut ring (Plan 2) beside a hairpin: threading it while the hairpin gate is your next gate credits
    /// that gate, so you cut the hairpin's corner instead of flying out to it.
    /// </summary>
    public readonly struct SwitchbackShortcut
    {
        public readonly int SkipsGate;    // the hairpin gate it credits
        public readonly RaceGate Ring;    // where it hangs (on the chord from the gate before to the gate after)

        public SwitchbackShortcut(int skipsGate, RaceGate ring)
        {
            SkipsGate = skipsGate;
            Ring = ring;
        }
    }

    /// <summary>
    /// A cluster of danger prisms (Plan 2, I4) that sweeps ACROSS its leg's racing line and back:
    /// offset(t) = <see cref="Amplitude"/> x sin(omega t + <see cref="Phase"/>) along <see cref="Side"/>. The prisms
    /// are LAID at their t = 0 pose; the mover moves them by the difference.
    /// </summary>
    public readonly struct SwitchbackMovingCluster
    {
        public readonly int FirstPrism;   // index into the layout's Prisms
        public readonly int Count;
        public readonly Vector3 Side;
        public readonly float Amplitude;
        public readonly float Phase;      // radians

        public SwitchbackMovingCluster(int firstPrism, int count, Vector3 side, float amplitude, float phase)
        {
            FirstPrism = firstPrism;
            Count = count;
            Side = side;
            Amplitude = amplitude;
            Phase = phase;
        }

        /// <summary>The offset from the laid pose at <paramref name="seconds"/> on the shared clock.</summary>
        public Vector3 DisplacementAt(float seconds) =>
            Side * (Amplitude * (Mathf.Sin(SwitchbackLiveFeatures.MovingOmega * seconds + Phase) - Mathf.Sin(Phase)));
    }

    /// <summary>Everything Plan 2 hangs on one course.</summary>
    public sealed class SwitchbackFeatureLayout
    {
        public readonly List<SwitchbackFeaturePrism> Prisms = new(256);
        public readonly List<SwitchbackRibbon> Ribbons = new(16);
        public readonly List<SwitchbackShortcut> Shortcuts = new(16);
        public readonly List<SwitchbackMovingCluster> MovingClusters = new(32);
    }

    /// <summary>
    /// Plan 2, LIVE FEATURES (<see cref="SwitchbackFunType.Plan2_LiveFeatures"/>, and <c>Combined</c>): things on the
    /// course that react to how you fly it. A pure function of the course and its settings, like
    /// <see cref="SwitchbackFeatures"/>, so every peer lays the same thing and the tests hold it.
    /// <list type="bullet">
    /// <item><b>Charge ribbons (I1-I3):</b> a wall beside a long leg (every third leg at I1-I2, every second at I3).
    /// Skim it end to end - stay inside <see cref="RibbonSkimBand"/> of the wall from its first fifth to its last -
    /// and the run gives a speed burst (<c>SwitchbackFunTypeRuntime</c>).</item>
    /// <item><b>Shortcut rings (I2-I4):</b> a needle-sized ring on the chord that cuts each hairpin. Threading it
    /// while the hairpin gate is next credits that gate.</item>
    /// <item><b>Moving danger (I4):</b> the shipped I4 danger cluster on every leg, sweeping across the racing line
    /// and back: the fast line is open only part of the time.</item>
    /// </list>
    /// The prism clearances are <see cref="SwitchbackFeatures"/>' own: never in a mouth, across another leg, or in
    /// the nucleus. A moving cluster is checked at both ends of its sweep.
    /// </summary>
    public static class SwitchbackLiveFeatures
    {
        public const float RibbonOffset = 24f;     // u from the racing line to the wall
        public const float RibbonSpacing = 14f;
        public const float RibbonStart = 0.2f;
        public const float RibbonEnd = 0.8f;
        public const float RibbonMinLeg = 400f;
        /// <summary>How close to the wall line (prism centres) a skim must stay: inside the wall's 2.5 u half
        /// width plus about four hulls.</summary>
        public const float RibbonSkimBand = 14f;
        /// <summary>A skim counts from inside the ribbon's first fifth to past its last fifth.</summary>
        public const float RibbonEnterShare = 0.2f;
        public const float RibbonExitShare = 0.8f;
        /// <summary>A brief lift off the band (a twitch, a single prism dropped near a gate) is forgiven.</summary>
        public const float RibbonGraceSeconds = 0.3f;
        public const float BurstThrottle = 1.4f;   // x throttle...
        public const float BurstSeconds = 1.5f;    // ...for this long

        public const float ShortcutGateClearanceRings = 3f;   // a shortcut keeps 3 ring radii from every gate

        public const float MovingAmplitude = 22f;  // u either side of the racing line
        public const float MovingPeriod = 4f;      // s for a full sweep and back
        public static float MovingOmega => 2f * Mathf.PI / MovingPeriod;

        public static bool HasRibbons(int intensity) => intensity <= 3;
        public static bool HasShortcuts(int intensity) => intensity >= 2;
        public static bool HasMovingDanger(int intensity) => intensity >= 4;
        static int RibbonEvery(int intensity) => intensity >= 3 ? 2 : 3;

        /// <summary>
        /// Plan 2's layout of <paramref name="course"/>. <paramref name="settings"/> is the course's own
        /// (<see cref="SwitchbackCourseSettings.ForIntensity(int, SwitchbackFunType)"/>): it says which corners are
        /// hairpins and how wide a needle is.
        /// </summary>
        public static SwitchbackFeatureLayout Layout(IReadOnlyList<RaceGate> course, SwitchbackCourseSettings settings,
                                                     int intensity, Vector3 centre = default, float clearRadius = 0f)
        {
            var layout = new SwitchbackFeatureLayout();
            if (course == null || course.Count < 3) return layout;
            settings.GateCount = course.Count;
            intensity = Mathf.Clamp(intensity, 1, 4);

            for (int k = 1; k < course.Count - 1; k++)
            {
                Vector3 a = course[k].Position, b = course[k + 1].Position;
                Vector3 leg = b - a;
                float length = leg.magnitude;
                if (length < 1f) continue;
                Vector3 dir = leg / length;
                Vector3 side = RaceCourseGeometry.Perpendicular(dir) * (k % 2 == 0 ? 1f : -1f);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.Cross(side, dir));

                if (HasRibbons(intensity) && k % RibbonEvery(intensity) == 1 && length >= RibbonMinLeg)
                {
                    int before = layout.Prisms.Count;
                    float from = length * RibbonStart, to = length * RibbonEnd;
                    for (float d = from; d <= to; d += RibbonSpacing)
                        TryAdd(layout.Prisms, course, centre, clearRadius, RibbonOffset, -1,
                               new SwitchbackFeaturePrism(a + dir * d + side * RibbonOffset, rot,
                                                          SwitchbackFeatures.WallPrismScale, PrismKind.Plain));
                    // A ribbon that lost most of its wall to clearances is no ribbon.
                    if (layout.Prisms.Count - before >= 0.6f * ((to - from) / RibbonSpacing))
                        layout.Ribbons.Add(new SwitchbackRibbon(k, a + dir * from + side * RibbonOffset,
                                                                a + dir * to + side * RibbonOffset, side));
                    else
                        layout.Prisms.RemoveRange(before, layout.Prisms.Count - before);
                }

                if (HasMovingDanger(intensity))
                {
                    float phase = k * 1.7f;   // deterministic, staggered from leg to leg
                    float laid = MovingAmplitude * Mathf.Sin(phase);
                    float mid = length * 0.5f;
                    var cluster = new List<SwitchbackFeaturePrism>(SwitchbackFeatures.DangerPerLeg);
                    bool clear = true;
                    for (int j = 0; j < SwitchbackFeatures.DangerPerLeg && clear; j++)
                    {
                        float d = mid + (j - (SwitchbackFeatures.DangerPerLeg - 1) * 0.5f) * SwitchbackFeatures.DangerSpacing;
                        Vector3 onLine = a + dir * d;
                        // Both ends of the sweep must clear every gate, every OTHER leg and the nucleus.
                        clear = Clear(course, centre, clearRadius, onLine + side * MovingAmplitude, MovingAmplitude, k)
                             && Clear(course, centre, clearRadius, onLine - side * MovingAmplitude, MovingAmplitude, k);
                        cluster.Add(new SwitchbackFeaturePrism(onLine + side * laid, rot,
                                                               SwitchbackFeatures.DangerPrismScale, PrismKind.Danger));
                    }
                    if (clear)
                    {
                        layout.MovingClusters.Add(new SwitchbackMovingCluster(layout.Prisms.Count, cluster.Count, side,
                                                                              MovingAmplitude, phase));
                        layout.Prisms.AddRange(cluster);
                    }
                }
            }

            if (HasShortcuts(intensity) && settings.HairpinEvery > 0)
            {
                float radius = SwitchbackCourse.NeedleRadius(settings);
                for (int h = 1; h < course.Count - 1; h++)
                {
                    if (!SwitchbackCourse.IsHairpinCorner(h, settings)) continue;
                    Vector3 p = course[h - 1].Position, q = course[h + 1].Position;
                    Vector3 chord = q - p;
                    if (chord.sqrMagnitude < 1f) continue;
                    Vector3 m = (p + q) * 0.5f;
                    if ((m - centre).sqrMagnitude < clearRadius * clearRadius) continue;
                    bool ok = true;
                    for (int g = 0; g < course.Count && ok; g++)
                        if ((m - course[g].Position).magnitude < ShortcutGateClearanceRings * course[g].Radius + radius)
                            ok = false;
                    for (int s = 0; s < layout.Shortcuts.Count && ok; s++)
                        if ((m - layout.Shortcuts[s].Ring.Position).magnitude < 4f * radius) ok = false;
                    if (!ok) continue;
                    layout.Shortcuts.Add(new SwitchbackShortcut(h, new RaceGate(m, chord.normalized, radius)));
                }
            }

            return layout;
        }

        static bool Clear(IReadOnlyList<RaceGate> course, Vector3 centre, float clearRadius, Vector3 p, float offset, int ownLeg)
        {
            if ((p - centre).sqrMagnitude < clearRadius * clearRadius) return false;
            for (int g = 0; g < course.Count; g++)
                if ((p - course[g].Position).magnitude < course[g].Radius + SwitchbackFeatures.MouthClearance) return false;
            float lineClearance = offset * SwitchbackFeatures.LineClearanceShare;
            for (int k = 0; k + 1 < course.Count; k++)
                if (k != ownLeg &&
                    SwitchbackFeatures.DistanceToSegment(p, course[k].Position, course[k + 1].Position) < lineClearance)
                    return false;
            return true;
        }

        static void TryAdd(List<SwitchbackFeaturePrism> prisms, IReadOnlyList<RaceGate> course, Vector3 centre,
                           float clearRadius, float offset, int ownLeg, in SwitchbackFeaturePrism prism)
        {
            if (Clear(course, centre, clearRadius, prism.Position, offset, ownLeg)) prisms.Add(prism);
        }

        // ── the skim, as a pure step the runtime and the tests share ─────────

        /// <summary>Where <paramref name="p"/> sits against a ribbon: share along it (0 at Start, 1 at End) and
        /// distance from its wall line.</summary>
        public static void Measure(in SwitchbackRibbon ribbon, Vector3 p, out float along, out float distance)
        {
            Vector3 ab = ribbon.End - ribbon.Start;
            float sq = ab.sqrMagnitude;
            along = sq > 1e-6f ? Vector3.Dot(p - ribbon.Start, ab) / sq : 0f;
            distance = (p - (ribbon.Start + ab * Mathf.Clamp01(along))).magnitude;
        }
    }

    /// <summary>
    /// One pilot's skim of one ribbon: <see cref="Step"/> it every frame with the ship's position; it returns true
    /// ONCE, on the frame the skim completes (entered in the first fifth, stayed in the band, passed the last fifth).
    /// </summary>
    public struct SwitchbackRibbonSkim
    {
        bool _active;
        bool _done;
        float _offBandSince;

        public bool Done => _done;

        public bool Step(in SwitchbackRibbon ribbon, Vector3 position, float time)
        {
            if (_done) return false;
            SwitchbackLiveFeatures.Measure(ribbon, position, out float along, out float distance);
            bool inBand = distance <= SwitchbackLiveFeatures.RibbonSkimBand;

            if (!_active)
            {
                if (inBand && along >= 0f && along <= SwitchbackLiveFeatures.RibbonEnterShare)
                {
                    _active = true;
                    _offBandSince = -1f;
                }
                return false;
            }

            if (inBand && along >= SwitchbackLiveFeatures.RibbonExitShare)
            {
                _active = false;
                _done = true;
                return true;
            }
            if (inBand) { _offBandSince = -1f; return false; }

            if (_offBandSince < 0f) _offBandSince = time;
            if (time - _offBandSince > SwitchbackLiveFeatures.RibbonGraceSeconds || along > 1.1f || along < -0.1f)
                _active = false;   // broken: may re-enter from the start
            return false;
        }
    }
}
