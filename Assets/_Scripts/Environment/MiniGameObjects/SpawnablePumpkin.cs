using System.Collections.Generic;
using _Scripts._Core.Ship;
using UnityEngine;

namespace _Scripts.Environment.MiniGameObjects
{
    public class SpawnablePumpkin : SpawnableEllipsoid
    {
        static int SegmentsSpawned = 0;
        private bool isLooping = false;
        private GameObject container;

        public override GameObject Spawn()
        {
            container = CreatePumpkinContainer();
            int blockCount = 15;
            int totalCurveCount = 4 * 12; // 4 cycles of a pattern with 12 periods
            List<Trail> trails = new List<Trail>();

            float pumpkinWidth = SetPumpkinWidth();
            float curveScale = pumpkinWidth;
            float zOffset = pumpkinWidth / 2.5f;
            float kHome = SetKHome();

            for (int cardioidIndex = 0; cardioidIndex < totalCurveCount; cardioidIndex++)
            {
                Trail trail = CreateTrail(trails);

                float rotationAngle = CalculateRotationAngle(cardioidIndex, totalCurveCount);
                float kModulation = GetKModulationValue(cardioidIndex);

                float k = kHome + kModulation;
                float sizeMultiplier = 1 - k;

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
            int modValue = cardioidIndex % 4;
            return modValue switch
            {
                0 or 3 => 0f,
                1 or 2 => 0.08f,
                _ => 0.16f
            };
        }

        private void CreateBlocksForTrail(Trail trail, int blockCount, float pumpkinWidth, float curveScale, float zOffset, float sizeMultiplier, float rotationAngle, GameObject container)
        {
            for (int block = 0; block < blockCount; block++)
            {
                float t = ((float)block / (float)blockCount) * Mathf.PI;

                float z = sizeMultiplier * curveScale * (2 * Mathf.Cos(t) - 0.5f * Mathf.Cos(3 * t));
                float y = 0;
                float x = zOffset + sizeMultiplier * curveScale * (2 * Mathf.Sin(t) - 0.5f * Mathf.Sin(3 * t));

                float rotatedX = x * Mathf.Cos(rotationAngle) - y * Mathf.Sin(rotationAngle);
                float rotatedY = x * Mathf.Sin(rotationAngle) + y * Mathf.Cos(rotationAngle);

                Vector3 position = new Vector3(rotatedX, rotatedY, z);
                Vector3 lookPosition = position;

                if (block != 0)
                {
                    lookPosition = trail.GetBlock(block - 1).transform.position;
                }

                CreateBlock(position, lookPosition, container.name + "::BLOCK::" + block, trail, sizeMultiplier * pumpkinWidth * trailBlock.transform.localScale * Mathf.Sin(t), trailBlock, container, Teams.Yellow);
            }
        }
    }
}
