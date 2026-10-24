using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns prisms along a helicoid - the only ruled minimal surface besides the plane.
    ///
    /// The helicoid is parameterized as:
    ///   x = v · cos(u)
    ///   y = c · u          (height along axis)
    ///   z = v · sin(u)
    ///
    /// where u is the angular parameter and v is the radial distance from the central axis.
    /// The result is a sweeping helical ramp - like a spiral staircase made of blocks.
    ///
    /// Multiple interleaved helicoids at angular offsets create parallel ramps that
    /// spiral around each other, offering layered flight paths at different altitudes.
    /// (As laid, y = c·u holds for every sheet, so an angular offset slides a sheet ALONG the one
    /// helicoid rather than stacking it: the sheets interleave on a single surface, a fraction of a
    /// row apart. Each block's length is fitted flush to its nearest neighbour of any sheet - see
    /// GenerateTrailData.)
    ///
    /// The helicoid is locally isometric to the catenoid via the Bonnet transformation;
    /// both are minimal surfaces (zero mean curvature everywhere).
    /// </summary>
    public class SpawnableHelicoid : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2.5f, 2.5f, 2.5f);

        [Header("Helicoid Shape")]
        [Tooltip("Number of full turns of the helix.")]
        [SerializeField] float turns = 4f;

        [Tooltip("Total height of the structure.")]
        [SerializeField] float height = 200f;

        [Tooltip("Inner radius - distance from axis where blocks start.")]
        [SerializeField] float innerRadius = 8f;

        [Tooltip("Outer radius - distance from axis where blocks end.")]
        [SerializeField] float outerRadius = 55f;

        [Header("Density")]
        [Tooltip("Angular sample points per full turn.")]
        [SerializeField] int samplesPerTurn = 40;

        [Tooltip("Radial sample points from inner to outer radius.")]
        [SerializeField] int radialSamples = 8;

        [Header("Multi-Ramp")]
        [Tooltip("Number of interleaved helicoid sheets. 2 = double helix, 3 = triple, etc.")]
        [SerializeField] int sheets = 2;

        [Header("Visual")]
        [SerializeField] bool colorBySheet = true;
        [SerializeField] Domains[] sheetDomains = new Domains[]
        {
            Domains.Blue,
            Domains.Gold,
            Domains.Jade,
        };

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();

            float pitch = height / (turns * 2f * Mathf.PI); // c in the parametrization
            int totalAngularSamples = Mathf.RoundToInt(turns * samplesPerTurn);
            float step = 2f * Mathf.PI * turns / Mathf.Max(1, totalAngularSamples);   // u between rows

            for (int sheet = 0; sheet < sheets; sheet++)
            {
                float sheetOffset = 2f * Mathf.PI * sheet / sheets;

                Domains sheetDomain = colorBySheet && sheetDomains.Length > 0
                    ? sheetDomains[sheet % sheetDomains.Length]
                    : domain;

                var points = new List<SpawnPoint>();

                for (int iu = 0; iu <= totalAngularSamples; iu++)
                {
                    float u = 2f * Mathf.PI * turns * iu / totalAngularSamples + sheetOffset;

                    for (int iv = 0; iv < radialSamples; iv++)
                    {
                        float v = Mathf.Lerp(innerRadius, outerRadius, (float)iv / (radialSamples - 1));

                        Vector3 position = Pose(u, v, pitch, out var rotation);

                        // FLUSH, not overlapping (Docs/ECOSYSTEM.md 49.10). A ring of fixed v is
                        // one helix that EVERY sheet samples: y = c·u holds for all of them, so a
                        // sheet's angular offset slides it along the same surface rather than
                        // stacking it - three sheets interleave on one helicoid (on the shipped
                        // 40-per-turn, 3-sheet prefab, a third of a row apart). A block's
                        // neighbours along the helix are therefore the NEAREST samples of any
                        // sheet, and near the axis they are closer than the block is long. Its
                        // length is fitted to them, in closed form (the gap to each sheet's
                        // nearest row, then FlushPairLength) - pose, cross-section and count are
                        // untouched, and out where the rows are tens of units apart nothing changes.
                        float gapBefore = float.PositiveInfinity, gapAfter = float.PositiveInfinity;
                        for (int other = 0; other < sheets; other++)
                        {
                            float origin = 2f * Mathf.PI * other / sheets;
                            float k = (u - origin) / step;
                            int below = Mathf.FloorToInt(k + 1e-4f), above = Mathf.CeilToInt(k - 1e-4f);
                            if (other == sheet) { below = iu - 1; above = iu + 1; }
                            else if (below == above) { below--; above++; }   // a row shared with this one (sheets coinciding)
                            if (below >= 0 && below <= totalAngularSamples)
                                gapBefore = Mathf.Min(gapBefore, u - (origin + below * step));
                            if (above >= 0 && above <= totalAngularSamples)
                                gapAfter = Mathf.Min(gapAfter, origin + above * step - u);
                        }
                        float length = blockScale.z;
                        if (!float.IsPositiveInfinity(gapBefore))
                            length = Mathf.Min(length, FlushMargin * FlushPairLength(Pose(u - gapBefore, v, pitch, out var rb), rb, position, rotation));
                        if (!float.IsPositiveInfinity(gapAfter))
                            length = Mathf.Min(length, FlushMargin * FlushPairLength(position, rotation, Pose(u + gapAfter, v, pitch, out var ra), ra));

                        points.Add(new SpawnPoint(position, rotation, new Vector3(blockScale.x, blockScale.y, length)));
                    }
                }

                trailDataList.Add(new SpawnTrailData(points.ToArray(), false, sheetDomain));
            }

            return trailDataList.ToArray();
        }

        /// <summary>The block at (u, v): on the helicoid, looking back along the spiral tangent
        /// (the original lay's +0.01 u look target), +y as near world-up as the tangent allows.</summary>
        Vector3 Pose(float u, float v, float pitch, out Quaternion rotation)
        {
            Vector3 position = new Vector3(v * Mathf.Cos(u), pitch * u - height * 0.5f, v * Mathf.Sin(u)); // centred vertically
            float uNext = u + 0.01f;
            Vector3 tangent = new Vector3(v * Mathf.Cos(uNext), pitch * uNext - height * 0.5f, v * Mathf.Sin(uNext));
            rotation = SpawnPoint.LookRotation(tangent, position, Vector3.up);
            return position;
        }

        /// <summary>How far under exact contact a fitted block stops, so float noise in the poses
        /// can never flip two touching faces into a clip.</summary>
        const float FlushMargin = 0.998f;

        /// <summary>
        /// The longest length two neighbouring blocks (this shape's cross-section, +z along the
        /// spiral) can BOTH take and still only touch: separated on the plane normal to the chord
        /// between their centres, each box reaches <c>half-length |z.n| + half-width |x.n| +
        /// half-height |y.n|</c> along it, and the two reaches may sum to the chord and no more.
        /// </summary>
        float FlushPairLength(Vector3 pa, Quaternion ra, Vector3 pb, Quaternion rb)
        {
            Vector3 d = pb - pa;
            float dist = d.magnitude;
            if (dist < 1e-5f) return 0f;
            Vector3 n = d / dist;
            float lateral = 0.5f * blockScale.x * (Mathf.Abs(Vector3.Dot(ra * Vector3.right, n)) + Mathf.Abs(Vector3.Dot(rb * Vector3.right, n)))
                          + 0.5f * blockScale.y * (Mathf.Abs(Vector3.Dot(ra * Vector3.up, n)) + Mathf.Abs(Vector3.Dot(rb * Vector3.up, n)));
            float along = Mathf.Abs(Vector3.Dot(ra * Vector3.forward, n)) + Mathf.Abs(Vector3.Dot(rb * Vector3.forward, n));
            if (along < 1e-5f) return float.PositiveInfinity;
            return 2f * Mathf.Max(0f, dist - lateral) / along;
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(turns, height, innerRadius, outerRadius,
                System.HashCode.Combine(samplesPerTurn, radialSamples, sheets, blockScale, seed));
        }
    }
}
