using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class SpawnableComet : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

        #region Attributes for Explosion Parameters
        [Header("Block Parameters")]
        [SerializeField] int blockCount = 8;

        [SerializeField] int ringCountHead = 4;
        [SerializeField] int ringCountTail = 9;
        [SerializeField] float headRadius = 30; //y scaler
        [SerializeField] float tailLength = 60f; //x scaler
        [SerializeField] Vector3 blockScale = Vector3.one;
        [SerializeField] Vector3 Orgin;
        #endregion

        protected override SpawnTrailData[] GenerateTrailData()
        {
            int totalRings = ringCountHead + ringCountTail;
            var trailDataList = new List<SpawnTrailData>(totalRings);

            // Head
            for (int ring = 0; ring < ringCountHead; ring++)
            {
                var points = new SpawnPoint[blockCount];
                for (int block = 0; block < blockCount; block++)
                {
                    float scale = Mathf.Sqrt(Mathf.Pow(headRadius, 2) - Mathf.Pow(((ring / (float)ringCountHead) - 1) * headRadius, 2));
                    float tilt = -headRadius;
                    float distanceTowardTail = ring * headRadius / ringCountHead;
                    float phase = ring % 2 * 0.5f;

                    points[block] = CreateRingPoint(block, phase, scale, tilt, distanceTowardTail);
                }
                FitRing(points, ring % 2 * 0.5f, Mathf.Sqrt(Mathf.Pow(headRadius, 2) - Mathf.Pow(((ring / (float)ringCountHead) - 1) * headRadius, 2)), ring * headRadius / ringCountHead);
                trailDataList.Add(new SpawnTrailData(points, false, domain));
            }

            // Tail
            for (int ring = ringCountHead; ring < ringCountHead + ringCountTail; ring++)
            {
                var points = new SpawnPoint[blockCount];
                for (int block = 0; block < blockCount; block++)
                {
                    float scale = (0.5f * Mathf.Cos((ring - ringCountHead) / ((float)ringCountTail) * Mathf.PI) + 0.5f) * headRadius;
                    float tilt = -headRadius - ((ring - ringCountHead) / (float)ringCountTail * tailLength);
                    float distanceTowardTail = ((ring - ringCountHead) * tailLength / ringCountTail) + headRadius;
                    float phase = ring % 2 * 0.5f;

                    points[block] = CreateRingPoint(block, phase, scale, tilt, distanceTowardTail);
                }
                FitRing(points, ring % 2 * 0.5f,
                    (0.5f * Mathf.Cos((ring - ringCountHead) / ((float)ringCountTail) * Mathf.PI) + 0.5f) * headRadius,
                    ((ring - ringCountHead) * tailLength / ringCountTail) + headRadius);
                trailDataList.Add(new SpawnTrailData(points, false, domain));
            }

            MakeFlush(trailDataList);
            return trailDataList.ToArray();
        }

        /// <summary>The smallest axis the prism prefab lays (its PrismScaleAnimator window grows
        /// anything thinner back to this), so a flush block is never sized under it.</summary>
        const float MinPrismAxis = 0.5f;

        /// <summary>Blocks are sized to this fraction of their exact flush size, so float noise
        /// never turns two faces that touch into a clip.</summary>
        const float FlushMargin = 0.995f;

        /// <summary>How many rings back a block is sized against: rings alternate their phase by
        /// half a block, so a block lines up with the blocks two rings away.</summary>
        const int RingReach = 2;

        /// <summary>
        /// Lay a ring wide enough for its blocks (Docs/ECOSYSTEM.md §49.10). The head's first ring
        /// has radius 0 (every block on one point - the shipped comets' 28 and 15 coincident pairs)
        /// and the tail narrows to almost nothing, but the prism prefab never lays an axis under
        /// MinPrismAxis, so the blocks of those rings sat inside each other. Each pair of the ring's
        /// blocks lies along the chord between them, so they part once the radius gives that chord
        /// the two blocks' reach along it (their sizes as the prefab lays them): a ring narrower
        /// than the widest such radius is laid at it instead - every block moved straight out from
        /// the comet's axis by the same smallest amount, its size and rotation kept. Rings already
        /// wide enough do not move.
        /// </summary>
        void FitRing(SpawnPoint[] points, float phase, float scale, float distanceTowardTail)
        {
            int n = points.Length;
            if (n < 2) return;
            float needed = 0f;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    float ai = Angle(i, phase), aj = Angle(j, phase);
                    Vector3 chord = new Vector3(Mathf.Cos(aj) - Mathf.Cos(ai), Mathf.Sin(aj) - Mathf.Sin(ai), 0f);
                    float unit = chord.magnitude;                          // the chord of a unit ring
                    if (unit < 1e-6f) continue;
                    chord /= unit;
                    needed = Mathf.Max(needed, (LaidReach(points[i], chord) + LaidReach(points[j], chord)) / unit);
                }
            needed /= FlushMargin;
            if (needed <= scale) return;
            for (int i = 0; i < n; i++)
            {
                float a = Angle(i, phase);
                points[i].Position = needed * Mathf.Cos(a) * Vector3.right + needed * Mathf.Sin(a) * Vector3.up
                                   + distanceTowardTail * -Vector3.forward;
            }
        }

        float Angle(int block, float phase) => (block + phase) / blockCount * 2 * Mathf.PI;

        /// <summary>A block's reach along a direction at the size the prefab lays it (no axis under
        /// MinPrismAxis).</summary>
        static float LaidReach(in SpawnPoint p, Vector3 l)
        {
            Vector3 s = new Vector3(Mathf.Max(Mathf.Abs(p.Scale.x), MinPrismAxis), Mathf.Max(Mathf.Abs(p.Scale.y), MinPrismAxis),
                                    Mathf.Max(Mathf.Abs(p.Scale.z), MinPrismAxis));
            return 0.5f * (Mathf.Abs(Vector3.Dot(p.Rotation * Vector3.right, l)) * s.x
                         + Mathf.Abs(Vector3.Dot(p.Rotation * Vector3.up, l)) * s.y
                         + Mathf.Abs(Vector3.Dot(p.Rotation * Vector3.forward, l)) * s.z);
        }

        /// <summary>
        /// Size the comet's blocks FLUSH (Docs/ECOSYSTEM.md §49.10). Each ring's blocks lean in
        /// towards a point behind the ring and run blockScale.y along the comet; where the head's
        /// rings are close (headRadius / ringCountHead apart) a block ran into the block lined up
        /// with it two rings on - the shipped SpawnableLongComet's 18 clipping pairs, rings 2-6.
        /// Each block is sized against its own ring's neighbours and the blocks of the next
        /// RingReach rings: a clipping pair shrinks by the common factor that makes it flush, on
        /// whichever axes of one or both keep the most volume (SpawnableEllipsoid.PrismFit) - on the
        /// shipped comet, the nearer ring's blocks alone, along the comet and across the ring. The
        /// centres and rotations never move.
        /// </summary>
        static void MakeFlush(List<SpawnTrailData> rings)
        {
            for (int r = 0; r < rings.Count; r++)
            {
                var ring = rings[r].Points;
                for (int i = 0; i < ring.Length; i++)
                    if ((i + 1) % ring.Length != i)                      // round the ring
                        SpawnableEllipsoid.PrismFit.Shrink(ref ring[i], ref ring[(i + 1) % ring.Length], MinPrismAxis, FlushMargin);
                for (int ahead = 1; ahead <= RingReach && r + ahead < rings.Count; ahead++)
                {
                    var other = rings[r + ahead].Points;
                    for (int i = 0; i < ring.Length; i++)
                        for (int j = 0; j < other.Length; j++)
                            SpawnableEllipsoid.PrismFit.Shrink(ref ring[i], ref other[j], MinPrismAxis, FlushMargin);
                }
            }
        }

        private SpawnPoint CreateRingPoint(int block, float phase, float scale, float tilt, float distanceTowardTail)
        {
            var offset = scale * Mathf.Cos(((block + phase) / blockCount) * 2 * Mathf.PI) * Vector3.right +
                         scale * Mathf.Sin(((block + phase) / blockCount) * 2 * Mathf.PI) * Vector3.up +
                         distanceTowardTail * -Vector3.forward;

            var position = offset;
            var tempBlockScale = new Vector3(blockScale.x * scale, blockScale.y, blockScale.z * scale);

            // lookDirection in the old code was: tilt * transform.forward - (offset + transform.position)
            // In local space (origin at 0,0,0): tilt * forward - offset
            var lookDirection = tilt * Vector3.forward - offset;
            var rotation = SpawnPoint.LookRotation(lookDirection, Vector3.forward);

            return new SpawnPoint(position, rotation, tempBlockScale);
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed, blockCount, ringCountHead, ringCountTail, headRadius, tailLength, blockScale, Orgin);
        }
    }
}
