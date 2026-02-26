using CosmicShore.Game.Ship;
using CosmicShore.Game.Environment;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Models.Enums;
namespace CosmicShore.Game.Environment
{
    public class SpawnableWall : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;
        [SerializeField] Crystal crystal;
        [SerializeField] float blockSize = 1f;
        [SerializeField] float padding = .1f;
        public int Width = 6;
        public int Height = 6;

        public override GameObject Spawn(int intensity = 1)
        {
            Width = 6 - intensity;
            Height = 6 - intensity;
            InvalidateCache();
            return base.Spawn(intensity);
        }

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[Width * Height];
            var size = new Vector3(1, 1, .1f) * blockSize;
            var blockSpacing = blockSize + padding;
            int idx = 0;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var correction = new Vector3(blockSpacing * .5f, blockSpacing * .5f, 0);
                    var position = new Vector3(x * blockSpacing, y * blockSpacing, 0) + correction;
                    points[idx++] = new SpawnPoint(position, Quaternion.identity, size);
                }
            }

            return points;
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
            {
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, Domains.Blue);

                if (crystal != null)
                {
                    foreach (var point in td.Points)
                    {
                        var newCrystal = Instantiate(crystal, container.transform);
                        newCrystal.transform.localPosition = point.Position +
                            (Vector3.forward * ((float)rng.NextDouble() * 2f - 1f) * Width * blockSize);
                        newCrystal.transform.localScale *= 5f * Mathf.Pow((float)rng.NextDouble(), 16) + 1f;
                    }
                }
            }
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(Width, Height, blockSize, padding, seed);
        }
    }
}
