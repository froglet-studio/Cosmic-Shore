using StarWriter.Core;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class SpawnablePumpkin : SpawnableEllipsoid
{
    //[SerializeField] TrailBlock trailBlock;

    //float length;
    //float width;
    //float height;

    static int SegmentsSpawned = 0;

    public override GameObject Spawn()
    {
        GameObject container = new GameObject();
        container.name = "Pumpkin" + SegmentsSpawned++;

        //length = ((float)rng.Next(1, 100)) / 100 * maxlength;
        width = ((float)rng.Next(25, 100)) / 100 * maxwidth;
        //height = ((float)rng.Next(1, 100)) / 100 * maxheight;

        int blockCount = 15;

        int periods = 12; // Number of cycles of the k pattern
        int totalCurveCount = 4 * periods;

        List<Trail> trails = new List<Trail>();

        // Scaling factor to adjust the size of the double-cusped curve
        float curveScale = width;

        // Offset to create separation between the cusps
        float zOffset = width / 2.5f;

        // Randomly determine the home position for k value between -0.16 and 0.16
        float kHome = ((float)rng.Next(-16, 16)) / 100;

        for (int cardioidIndex = 0; cardioidIndex < totalCurveCount; cardioidIndex++)
        {
            // TODO: fix bouncing between nearby two block when isLoop true, please see Trail.cs
            var trail = new Trail(false);
            trails.Add(trail);

            // Rotation angle for this curve (adjusted for totalCurveCount)
            float rotationAngle = Mathf.Deg2Rad * 360 / totalCurveCount * cardioidIndex;

            // Using a pattern to determine the scaling factor around the home position
            int modValue = cardioidIndex % 4;
            float kModulation = modValue == 0 || modValue == 4 ? 0f :
                                modValue == 1 || modValue == 3 ? 0.08f :
                                0.16f;

            // Final k value
            float k = kHome + kModulation;

            // Scaling factor based on the pattern
            float sizeMultiplier = 1 - k;

            for (int block = 0; block < blockCount; block++)
            {
                var t = ((float)block / (float)blockCount) * Mathf.PI;  // Adjusted to only cover half the curve

                // Scaled and shifted double-cusped curve mapped to zx plane
                var z = sizeMultiplier * curveScale * (2 * Mathf.Cos(t) - 0.5f * Mathf.Cos(3 * t));
                var y = 0;
                var x = zOffset + sizeMultiplier * curveScale * (2 * Mathf.Sin(t) - 0.5f * Mathf.Sin(3 * t));

                // Rotate around z-axis
                var rotatedX = x * Mathf.Cos(rotationAngle) - y * Mathf.Sin(rotationAngle);
                var rotatedY = x * Mathf.Sin(rotationAngle) + y * Mathf.Cos(rotationAngle);
                var position = new Vector3(rotatedX, rotatedY, z);

                var lookPosition = position;
                if (block != 0) lookPosition = trail.GetBlock(block - 1).transform.position;

                CreateBlock(position, lookPosition, container.name + "::BLOCK::" + block, trail, sizeMultiplier * width * trailBlock.transform.localScale * Mathf.Sin(t), trailBlock, container, Teams.Yellow);
            }

        }

        return container;
    }


}