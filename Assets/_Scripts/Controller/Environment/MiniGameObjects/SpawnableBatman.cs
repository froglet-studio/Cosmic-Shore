using CosmicShore.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class SpawnableBatman : SpawnableBase
    {
        [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

        protected override SpawnPoint[] GeneratePoints()
        {
            Func<float, float> outerWing = (x) => (float)(2 * Math.Sqrt(-Math.Abs(Math.Abs(x) - 1) * Math.Abs(3 - Math.Abs(x)) / ((Math.Abs(x) - 1) * (3 - Math.Abs(x)))) * (1 + Math.Abs(Math.Abs(x) - 3) / (Math.Abs(x) - 3)) * Math.Sqrt(1 - Math.Pow((x / 7), 2)) + (5 + 0.97f * (Math.Abs(x - 0.5f) + Math.Abs(x + 0.5f)) - 3 * (Math.Abs(x - 0.75f) + Math.Abs(x + 0.75f))) * (1 + Math.Abs(1 - Math.Abs(x)) / (1 - Math.Abs(x))));
            Func<float, float> head1 = (x) => (float)(9 - 8 * Math.Abs(x));
            Func<float, float> head2 = (x) => (float)(3 * Math.Abs(x) + .75);
            Func<float, float> head3 = (x) => (float)(2.25);
            Func<float, float> bottom = (x) => (float)(Math.Abs(x / 2) - 0.0913722 * (Math.Pow(x, 2)) - 3 + Math.Sqrt(1 - Math.Pow((Math.Abs(Math.Abs(x) - 2) - 1), 2)));
            Func<float, float> innerWing = (x) => (float)((2.71052 + (1.5 - .5 * Math.Abs(x)) - 1.35526 * Math.Sqrt(4 - Math.Pow((Math.Abs(x) - 1), 2))) * Math.Sqrt(Math.Abs(Math.Abs(x) - 1) / (Math.Abs(x) - 1)) + 0.9);

            var pointsList = new List<SpawnPoint>();

            // Outer Wings
            for (float block = 3.2f; block < 7; block += .2f)
            {
                var x = block * 5;
                var y = outerWing(block) * 5;

                var position = new Vector3(x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));

                if (block > 4)
                {
                    position = new Vector3(x, -y, 0);
                    pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
                }

                position = new Vector3(-x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));

                if (block > 4)
                {
                    position = new Vector3(-x, -y, 0);
                    pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
                }
            }

            // Inner Wings
            for (float block = 1.2f; block <= 3; block += .2f)
            {
                var x = block * 5;
                var y = innerWing(block) * 5;

                var position = new Vector3(x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));

                position = new Vector3(-x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
            }

            // Bottom
            for (float block = 0; block <= 4; block += .2f)
            {
                var x = block * 5;
                var y = (bottom(block) * 5) - 2;

                var position = new Vector3(x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));

                position = new Vector3(-x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
            }

            // Head
            for (float block = .75f; block <= 1; block += .025f)
            {
                var x = block * 5;
                var y = (head1(block) * 5) + 5;

                var position = new Vector3(x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
                position = new Vector3(-x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
            }
            // Head2
            for (float block = .5f; block <= .75; block += .025f)
            {
                var x = block * 5;
                var y = (head2(block) * 5) + 5;

                var position = new Vector3(x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));

                position = new Vector3(-x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
            }
            // Head3
            for (float block = -.5f; block <= .5f; block += .025f)
            {
                var x = block * 5;
                var y = (head3(block) * 5) + 5;

                var position = new Vector3(x, y, 0);
                pointsList.Add(new SpawnPoint(position, SpawnPoint.LookRotation(Vector3.zero, position, Vector3.up), Vector3.one));
            }

            return pointsList.ToArray();
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            foreach (var td in trailData)
                SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed);
        }
    }
}
