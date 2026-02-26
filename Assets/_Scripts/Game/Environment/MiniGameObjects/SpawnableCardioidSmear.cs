using CosmicShore.Game.Environment;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Environment
{
    public class SpawnableCardioidSmear : SpawnableEllipsoid
    {
        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();

            float localLength = (float)rng.Next(1, 100) / 100 * maxlength;
            float localWidth = (float)rng.Next(1, 100) / 100 * maxwidth;
            float localHeight = (float)rng.Next(1, 100) / 100 * maxheight;

            int cardioidCount = 12;
            float offsetAngle = Mathf.PI * 2 / cardioidCount;

            for (int cardioidIndex = 0; cardioidIndex < cardioidCount; cardioidIndex++)
            {
                int blockCount = 30;
                var points = new SpawnPoint[blockCount];

                for (int block = 0; block < blockCount; block++)
                {
                    var t = ((float)block / blockCount) * Mathf.PI * 2;
                    var r = localWidth * (1 - Mathf.Sin(t));

                    var x = r * Mathf.Cos(t + offsetAngle * cardioidIndex);
                    var y = r * Mathf.Sin(t + offsetAngle * cardioidIndex);
                    var position = new Vector3(x, y, 0);

                    var lookPosition = block == 0 ? position : points[block - 1].Position;
                    var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);

                    points[block] = new SpawnPoint(position, rotation, PrismScale);
                }

                trailDataList.Add(new SpawnTrailData(points, true, domain));
            }

            return trailDataList.ToArray();
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(maxlength, maxwidth, maxheight, seed);
        }
    }
}
