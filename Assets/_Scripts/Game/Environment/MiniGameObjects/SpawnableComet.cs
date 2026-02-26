using CosmicShore.Game.Ship;
using CosmicShore.Game.Environment.Spawning;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Environment.MiniGameObjects
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
                trailDataList.Add(new SpawnTrailData(points, false, domain));
            }

            return trailDataList.ToArray();
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
