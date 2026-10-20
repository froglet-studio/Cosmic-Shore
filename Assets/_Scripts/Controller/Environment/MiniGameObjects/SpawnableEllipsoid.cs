using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class SpawnableEllipsoid : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")]
        [Header("Trail")]
        [SerializeField] protected Prism prism;

        [Header("Spawnable Properties")]
        public float maxlength;
        public float maxwidth;
        public float maxheight;

        protected float length;
        protected float width;
        protected float height;

        /// <summary>
        /// Safe accessor for the prism prefab's localScale.
        /// Returns Vector3.one when prism is null (internal-node mode with children),
        /// producing uniform point scales that avoid shearing nested child geometry.
        /// </summary>
        protected Vector3 PrismScale => prism ? prism.transform.localScale : Vector3.one;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            length = (float)rng.Next(1, 100) / 100f * maxlength;
            width = (float)rng.Next(1, 100) / 100f * maxwidth;
            height = (float)rng.Next(1, 100) / 100f * maxheight;

            int blockCount = 30;

            // Ring 1: XY plane (Jade)
            var points1 = new SpawnPoint[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                var t = (float)block / (float)blockCount * Mathf.PI * 2;
                var x = (width / 2) * Mathf.Cos(t);
                var y = (height / 2) * Mathf.Sin(t);
                var position = new Vector3(x, y, 0);
                var lookPosition = block == 0 ? position : points1[block - 1].Position;
                var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
                points1[block] = new SpawnPoint(position, rotation, PrismScale);
            }

            // Ring 2: XZ plane (Ruby)
            var points2 = new SpawnPoint[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                var t = (float)block / (float)blockCount * Mathf.PI * 2;
                var x = (width / 2) * Mathf.Cos(t);
                var z = (length / 2) * Mathf.Sin(t);
                var position = new Vector3(x, 0, z);
                var lookPosition = block == 0 ? position : points2[block - 1].Position;
                var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
                points2[block] = new SpawnPoint(position, rotation, PrismScale);
            }

            // Ring 3: YZ plane (Gold)
            var points3 = new SpawnPoint[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                var t = (float)block / (float)blockCount * Mathf.PI * 2;
                var y = (height / 2) * Mathf.Cos(t);
                var z = (length / 2) * Mathf.Sin(t);
                var position = new Vector3(0, y, z);
                var lookPosition = block == 0 ? position : points3[block - 1].Position;
                var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
                points3[block] = new SpawnPoint(position, rotation, PrismScale);
            }

            return new[]
            {
                new SpawnTrailData(points1, true, Domains.Jade),
                new SpawnTrailData(points2, true, Domains.Ruby),
                new SpawnTrailData(points3, true, Domains.Gold),
            };
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(maxlength, maxwidth, maxheight, seed);
        }
    }
}
