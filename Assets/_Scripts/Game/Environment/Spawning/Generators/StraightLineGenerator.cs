using UnityEngine;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Generates points along a straight line on the Z axis.
    /// Supports random orientation per point or constant cumulative rotation.
    /// </summary>
    public class StraightLineGenerator : SpawnableBase
    {
        public enum RotationMode { Random, Constant }

        [Header("Straight Line")]
        [SerializeField] int count = 10;
        [SerializeField] float spacing = 400f;
        [SerializeField] RotationMode rotationMode = RotationMode.Random;
        [SerializeField] float rotationAmount = 10f;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(0, 0, i * spacing) + origin;
                Quaternion rot;

                switch (rotationMode)
                {
                    case RotationMode.Constant:
                        rot = Quaternion.Euler(0, 0, i * rotationAmount);
                        break;
                    case RotationMode.Random:
                    default:
                        rot = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 180f);
                        break;
                }

                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, spacing, rotationMode, rotationAmount, seed, origin);
        }
    }
}
