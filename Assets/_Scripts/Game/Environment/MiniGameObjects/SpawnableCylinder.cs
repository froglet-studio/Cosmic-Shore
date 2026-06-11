using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore
{
    public class SpawnableCylinder : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

        #region Attributes for Explosion Parameters
        [Header("Block Parameters")]
        [SerializeField] int blockCount = 12;

        [SerializeField] int ringCount = 15;
        [SerializeField] float radius = 30; //y scaler
        [SerializeField] float height = 60f; //x scaler
        [SerializeField] Vector3 blockScale = Vector3.one;
        [SerializeField] Vector3 Orgin;
        #endregion

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();

            for (int ring = 0; ring < ringCount; ring++)
            {
                var points = new SpawnPoint[blockCount];

                for (int block = 0; block < blockCount; block++)
                {
                    float phase = ring % 2 * 0.5f;
                    float scale = (0.5f * Mathf.Cos(ring / -radius * (float)ringCount * Mathf.PI) + 0.5f) * radius;
                    float tilt = ring / (float)ringCount;
                    float distanceTowardTail = (ring * height / ringCount) + radius;

                    // CreateRingBlock offset calculation
                    float theta = ((block + phase) / blockCount) * 2 * Mathf.PI;
                    var offset = scale * radius * theta * transform.right +
                                 scale * radius * theta * transform.up +
                                 distanceTowardTail * -transform.forward;

                    var position = transform.position + offset;
                    var tempBlockScale = new Vector3(blockScale.x * scale, blockScale.y, blockScale.z * scale);

                    // Original look direction: tilt * transform.forward - (offset + transform.position)
                    // Original up: transform.forward
                    Vector3 lookDirection = tilt * transform.forward - (offset + transform.position);
                    Vector3 up = transform.forward;
                    var rotation = SpawnPoint.LookRotation(lookDirection, up);

                    points[block] = new SpawnPoint(position, rotation, tempBlockScale);
                }

                trailDataList.Add(new SpawnTrailData(points, false, domain));
            }

            return trailDataList.ToArray();
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(blockCount, ringCount, radius, height, blockScale, seed);
        }
    }
}
