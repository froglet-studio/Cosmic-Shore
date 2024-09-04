using CosmicShore.Core;
using System.Collections.Generic;

using UnityEngine;

namespace CosmicShore.Environment.MiniGameObjects
{
    public class SpawnablePumpkin : SpawnableEllipsoid
    {
        static int SegmentsSpawned = 0;
        private bool isLooping = false;
        private GameObject container;
        [SerializeField] Teams Team = Teams.Gold;

        public override GameObject Spawn()
        {
            container = CreatePumpkinContainer();
            var blockCount = 15;
            var totalCurveCount = 4 * 12; // 4 cycles of a pattern with 12 periods
            var trails = new List<Trail>();

            var pumpkinWidth = SetPumpkinWidth();
            var curveScale = pumpkinWidth;
            var zOffset = pumpkinWidth / 2.5f;
            var kHome = SetKHome();

            for (var cardioidIndex = 0; cardioidIndex < totalCurveCount; cardioidIndex++)
            {
                var trail = CreateTrail(trails);

                var rotationAngle = CalculateRotationAngle(cardioidIndex, totalCurveCount);
                var kModulation = GetKModulationValue(cardioidIndex);

                var k = kHome + kModulation;
                var sizeMultiplier = 1 - k;

                CreateBlocksForTrail(trail, blockCount, pumpkinWidth, curveScale, zOffset, sizeMultiplier, rotationAngle, container);
            }

            return container;
        }

        private GameObject CreatePumpkinContainer()
        {
            container = new GameObject
            {
                name = "Pumpkin" + SegmentsSpawned++
            };
            return container;
        }

        private float SetPumpkinWidth()
        {
            return (float)rng.Next(25, 100) / 100 * maxwidth;
        }

        private float SetKHome()
        {
            return (float)rng.Next(-16, 16) / 100;
        }

        private Trail CreateTrail(List<Trail> trails)
        {
            var trail = new Trail(isLooping);
            trails.Add(trail);
            return trail;
        }

        private float CalculateRotationAngle(int cardioidIndex, int totalCurveCount)
        {
            return Mathf.Deg2Rad * 360 / totalCurveCount * cardioidIndex;
        }

        private float GetKModulationValue(int cardioidIndex)
        {
            var modValue = cardioidIndex % 4;
            return modValue switch
            {
                0 or 3 => 0f,
                1 or 2 => 0.08f,
                _ => 0.16f
            };
        }

        private void CreateBlocksForTrail(Trail trail, int blockCount, float pumpkinWidth, float curveScale, float zOffset, float sizeMultiplier, float rotationAngle, GameObject container)
        {
            for (var block = 0; block < blockCount; block++)
            {
                var t = ((float)block / (float)blockCount) * Mathf.PI;

                var z = sizeMultiplier * curveScale * (2 * Mathf.Cos(t) - 0.5f * Mathf.Cos(3 * t));
                //float y = 0;
                var x = zOffset + sizeMultiplier * curveScale * (2 * Mathf.Sin(t) - 0.5f * Mathf.Sin(3 * t));

                var rotatedX = x * Mathf.Cos(rotationAngle);// - y * Mathf.Sin(rotationAngle);
                var rotatedY = x * Mathf.Sin(rotationAngle);// + y * Mathf.Cos(rotationAngle);

                var position = new Vector3(rotatedX, rotatedY, z);
                var lookPosition = position;

                if (block != 0)
                {
                    lookPosition = trail.GetBlock(block - 1).transform.position;
                }

                CreateBlock(position, lookPosition, container.name + "::BLOCK::" + block, trail, sizeMultiplier * pumpkinWidth * trailBlock.transform.localScale * Mathf.Sin(t), trailBlock, container, Team);
            }
        }
    }
}
