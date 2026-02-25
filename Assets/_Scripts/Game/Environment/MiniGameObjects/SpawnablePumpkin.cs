using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Models.Enums;

namespace CosmicShore.Environment.MiniGameObjects
{
    public class SpawnablePumpkin : SpawnableEllipsoid
    {
        private bool isLooping = false;

        private void Reset()
        {
            domain = Domains.Gold;
        }

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var trailDataList = new List<SpawnTrailData>();
            int blockCount = 15;
            int totalCurveCount = 4 * 12;

            var pumpkinWidth = (float)rng.Next(25, 100) / 100 * maxwidth;
            var curveScale = pumpkinWidth;
            var zOffset = pumpkinWidth / 2.5f;
            var kHome = (float)rng.Next(-16, 16) / 100;

            for (var cardioidIndex = 0; cardioidIndex < totalCurveCount; cardioidIndex++)
            {
                var rotationAngle = Mathf.Deg2Rad * 360f / totalCurveCount * cardioidIndex;

                var modValue = cardioidIndex % 4;
                float kModulation = modValue switch
                {
                    0 or 3 => 0f,
                    1 or 2 => 0.08f,
                    _ => 0.16f
                };

                var k = kHome + kModulation;
                var sizeMultiplier = 1 - k;

                var points = new SpawnPoint[blockCount];
                for (var block = 0; block < blockCount; block++)
                {
                    var t = ((float)block / blockCount) * Mathf.PI;

                    var z = sizeMultiplier * curveScale * (2 * Mathf.Cos(t) - 0.5f * Mathf.Cos(3 * t));
                    var x = zOffset + sizeMultiplier * curveScale * (2 * Mathf.Sin(t) - 0.5f * Mathf.Sin(3 * t));

                    var rotatedX = x * Mathf.Cos(rotationAngle);
                    var rotatedY = x * Mathf.Sin(rotationAngle);

                    var position = new Vector3(rotatedX, rotatedY, z);
                    var lookPosition = block == 0 ? position : points[block - 1].Position;
                    var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
                    var blockScale = sizeMultiplier * pumpkinWidth * PrismScale * Mathf.Sin(t);

                    points[block] = new SpawnPoint(position, rotation, blockScale);
                }

                trailDataList.Add(new SpawnTrailData(points, isLooping, domain));
            }

            return trailDataList.ToArray();
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(maxwidth, seed);
        }
    }
}
