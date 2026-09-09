using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The CANOPY: Skim Race's waypoint course re-laid for a vessel that swings rather than skims.
    /// The same per-intensity waypoint sets (the racing line, the crystals and the turn monitor all
    /// key on them) grow BOUGHS - short super-shielded prism columns placed ALTERNATELY left and
    /// right of the line every <see cref="boughSpacing"/> - and a prism HOOP around every crystal
    /// spot, with an optional thin guide vine along the line itself. The geometry IS the mode's
    /// argument: alternating anchors make the Gibbon's left-arm / right-arm rhythm the fastest way
    /// round, and the hoop around each crystal is the fling target (its radius is authored to the
    /// crystal's spawn jitter, so a crystal always lands inside its hoop).
    ///
    /// Subclass, not sibling, on purpose: the scene's <c>CrystalCollisionTurnMonitor.optionalEnvironment</c>
    /// is typed <see cref="SpawnableWaypointTrack"/>, so a guid swap in the cloned scene keeps that
    /// reference alive and the auto-calculated crystal target (waypoints x laps) with it. Numbers
    /// derive from the Gibbon's beat: latch-to-latch at 150-250 u/s on the swing-rate-floored line
    /// is 0.6-1.3 s, i.e. ~180u along the line (~360u same side), and the resting aim yaw lands an
    /// abeam anchor ~80u out. Authored + budgeted by Tools/Build/author_canopy_run_assets.py.
    /// </summary>
    public class SpawnableCanopyTrack : SpawnableWaypointTrack
    {
        [Header("Canopy - Boughs")]
        [Tooltip("Prism the boughs are built from. Empty = the track prism.")]
        [SerializeField] Prism boughPrism;
        [Tooltip("Distance along the racing line between consecutive boughs (they alternate sides, so the same-side spacing is twice this). Sized to the Gibbon's beat: ~one grab per bough at race speed.")]
        [SerializeField] float boughSpacing = 180f;
        [Tooltip("How far each bough sits off the racing line. Sized so the resting aim yaw at race speed lands on it.")]
        [SerializeField] float boughLateralOffset = 80f;
        [Tooltip("Prisms per bough, stacked along the local up so a bough reads as a short column rather than a dot.")]
        [SerializeField] int boughPrismCount = 3;
        [Tooltip("Spacing between a bough's prisms along its column.")]
        [SerializeField] float boughPrismSpacing = 12f;
        [Tooltip("Scale of each bough prism (a fat cube: an anchor you can see from 170u).")]
        [SerializeField] Vector3 boughPrismScale = new(6f, 6f, 6f);
        [Tooltip("Alternate boughs are also raised/lowered by this much (world units), so a course reads as a 3D canopy rather than a corridor. 0 = flat.")]
        [SerializeField] float boughVerticalStagger = 0f;

        [Header("Canopy - Hoops")]
        [Tooltip("Prisms per hoop around each crystal spot.")]
        [SerializeField] int hoopPrismCount = 8;
        [Tooltip("Hoop radius. Must exceed the CrystalManager's anchorJitterRadius plus a crystal's radius, or a crystal can spawn inside the hoop wall. The generator asserts this.")]
        [SerializeField] float hoopRadius = 34f;
        [Tooltip("Scale of each hoop prism (long axis along the ring).")]
        [SerializeField] Vector3 hoopPrismScale = new(4f, 4f, 9f);

        [Header("Canopy - Guide vine")]
        [Tooltip("Lay a thin line of small prisms along the racing line itself, so the course reads from a distance.")]
        [SerializeField] bool layGuideVine = true;
        [Tooltip("Spacing of the guide vine prisms along the line.")]
        [SerializeField] float guideSpacing = 40f;
        [Tooltip("Scale of a guide vine prism (long axis along the line).")]
        [SerializeField] Vector3 guidePrismScale = new(1.5f, 1.5f, 6f);

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(base.GetParameterHash(),
                System.HashCode.Combine(boughSpacing, boughLateralOffset, boughPrismCount, boughPrismSpacing, boughVerticalStagger),
                System.HashCode.Combine(hoopPrismCount, hoopRadius, layGuideVine, guideSpacing));
        }

        // ── Layout ─────────────────────────────────────────────────────────

        readonly struct Lay
        {
            public readonly Vector3 Position; public readonly Quaternion Rotation; public readonly Vector3 Scale; public readonly bool IsHoop; public readonly bool IsGuide;
            public Lay(Vector3 p, Quaternion r, Vector3 s, bool hoop, bool guide) { Position = p; Rotation = r; Scale = s; IsHoop = hoop; IsGuide = guide; }
        }

        /// <summary>Sample the closed racing line by ARC LENGTH (spline or linear) - a parameter-space walk bunches on short segments.</summary>
        List<(Vector3 pos, Vector3 tangent)> SampleCentreline(int intensity, out float totalLength)
        {
            var positions = waypoints[intensity - 1].positions;
            int segCount = positions.Count;
            bool spline = UseSpline(intensity);
            var pts = new List<Vector3>(segCount * 24);
            const int samplesPerSegment = 24;
            for (int seg = 0; seg < segCount; seg++)
                for (int s = 0; s < samplesPerSegment; s++)
                {
                    float t = (float)s / samplesPerSegment;
                    pts.Add(spline ? GetSplinePoint(positions, seg, t)
                                   : Vector3.Lerp(positions[seg], positions[(seg + 1) % segCount], t));
                }
            totalLength = 0f;
            var cum = new List<float>(pts.Count + 1) { 0f };
            for (int i = 0; i < pts.Count; i++)
            {
                totalLength += Vector3.Distance(pts[i], pts[(i + 1) % pts.Count]);
                cum.Add(totalLength);
            }
            var result = new List<(Vector3, Vector3)>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 next = pts[(i + 1) % pts.Count];
                Vector3 tangent = (next - pts[i]).normalized;
                result.Add((pts[i], tangent));
            }
            return result;
        }

        static void PointAt(List<(Vector3 pos, Vector3 tangent)> line, float distance, float totalLength, out Vector3 pos, out Vector3 tangent)
        {
            distance = Mathf.Repeat(distance, totalLength);
            float acc = 0f;
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 a = line[i].pos, b = line[(i + 1) % line.Count].pos;
                float len = Vector3.Distance(a, b);
                if (acc + len >= distance || i == line.Count - 1)
                {
                    float t = len > 1e-4f ? Mathf.Clamp01((distance - acc) / len) : 0f;
                    pos = Vector3.Lerp(a, b, t);
                    tangent = line[i].tangent;
                    return;
                }
                acc += len;
            }
            pos = line[0].pos; tangent = line[0].tangent;
        }

        static void Frame(Vector3 tangent, out Vector3 right, out Vector3 up)
        {
            right = Vector3.Cross(Vector3.up, tangent);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.Cross(Vector3.forward, tangent);
            right.Normalize();
            up = Vector3.Cross(tangent, right);
        }

        IEnumerable<Lay> BuildLayout(int intensity)
        {
            var line = SampleCentreline(intensity, out float total);
            var positions = waypoints[intensity - 1].positions;

            // Boughs: alternate sides along the arc length.
            float spacing = Mathf.Max(boughSpacing, 10f);
            int boughs = Mathf.Max(2, Mathf.RoundToInt(total / spacing));
            float step = total / boughs; // close the loop exactly
            for (int b = 0; b < boughs; b++)
            {
                PointAt(line, b * step, total, out Vector3 p, out Vector3 t);
                Frame(t, out Vector3 right, out Vector3 up);
                float side = (b % 2 == 0) ? 1f : -1f;
                float lift = boughVerticalStagger * ((b / 2) % 2 == 0 ? 1f : -1f);
                Vector3 centre = p + right * (side * boughLateralOffset) + up * lift;
                int n = Mathf.Max(1, boughPrismCount);
                for (int k = 0; k < n; k++)
                {
                    float along = (k - (n - 1) * 0.5f) * boughPrismSpacing;
                    yield return new Lay(centre + up * along, Quaternion.LookRotation(t, up), boughPrismScale, false, false);
                }
            }

            // Hoops: one ring around every waypoint (= every crystal spot), in the plane across the line.
            int hn = Mathf.Max(3, hoopPrismCount);
            for (int w = 0; w < positions.Count; w++)
            {
                Vector3 p = positions[w];
                Vector3 t = (positions[(w + 1) % positions.Count] - positions[(w - 1 + positions.Count) % positions.Count]).normalized;
                if (t.sqrMagnitude < 0.5f) t = Vector3.forward;
                Frame(t, out Vector3 right, out Vector3 up);
                for (int k = 0; k < hn; k++)
                {
                    float ang = k * (2f * Mathf.PI / hn);
                    Vector3 radial = right * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                    Vector3 ringTangent = Vector3.Cross(t, radial);
                    yield return new Lay(p + radial * hoopRadius, Quaternion.LookRotation(ringTangent, radial), hoopPrismScale, true, false);
                }
            }

            // Guide vine along the line.
            if (layGuideVine && guideSpacing > 1f)
            {
                int gn = Mathf.Max(2, Mathf.RoundToInt(total / guideSpacing));
                float gstep = total / gn;
                for (int g = 0; g < gn; g++)
                {
                    PointAt(line, g * gstep, total, out Vector3 p, out Vector3 t);
                    Frame(t, out _, out Vector3 up);
                    yield return new Lay(p, Quaternion.LookRotation(t, up), guidePrismScale, false, true);
                }
            }
        }

        // ── Spawn ──────────────────────────────────────────────────────────

        public override GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            trails.Clear();

            if (!IsValidIntensityLevel(intensityLevel))
            {
                CSDebug.LogError($"[CanopyTrack] Need at least 2 waypoints for intensity level {intensityLevel}.");
                return new GameObject("EmptyCanopy");
            }

            GameObject container = new GameObject { name = $"CanopyTrack_{name}" };
            var trail = new Trail();
            int total = 0;
            Prism boughPrefab = boughPrism != null ? boughPrism : prism;
            Prism hoopPrefab = waypointPrism != null ? waypointPrism : prism;

            foreach (var lay in BuildLayout(intensityLevel))
            {
                Prism prefab = lay.IsHoop ? hoopPrefab : (lay.IsGuide ? prism : boughPrefab);
                Domains domain = lay.IsHoop ? waypointDomain : trackDomain;
                var block = Instantiate(prefab, container.transform);
                block.ChangeTeam(domain);
                block.ownerID = $"{container.name}::BLOCK::{total}";
                block.transform.localPosition = lay.Position;
                block.transform.localRotation = lay.Rotation;
                block.TargetScale = lay.Scale;
                block.Initialize();
                block.AssignTrail(trail);   // AFTER Initialize - reset clears membership
                trail.Add(block);
                PrismTrailBuilder.WatchForReveal(block); // the arena-ready gate covers every prism laid here
                total++;
            }

            trails.Add(trail);
            CSDebug.Log($"[CanopyTrack] intensity {intensityLevel}: {total} prisms (boughs every {boughSpacing}u at +/-{boughLateralOffset}u, hoops r={hoopRadius}).");
            return container;
        }

        /// <summary>Editor/preview layout: the same walk as <see cref="Spawn"/>, no prism lifecycle.</summary>
        public override IEnumerable<PreviewBlock> GetPreviewBlocks(int intensityLevelArg)
        {
            if (!IsValidIntensityLevel(intensityLevelArg)) yield break;
            intensityLevel = intensityLevelArg;
            foreach (var lay in BuildLayout(intensityLevelArg))
                yield return new PreviewBlock(lay.Position, lay.Rotation, lay.Scale, lay.IsHoop);
        }
    }
}
