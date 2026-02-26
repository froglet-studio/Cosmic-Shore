using UnityEngine;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.Environment
{
    /// <summary>
    /// Generates points along an ascending spiral (helix path).
    /// </summary>
    public class SpiralTowerGenerator : SpawnableBase
    {
        [Header("Spiral Tower")]
        [SerializeField] int count = 10;
        [SerializeField] float towerHeight = 100f;
        [SerializeField] float towerRadius = 20f;
        [SerializeField] float rotationsPerUnit = 0.1f;
        [SerializeField] Vector3 origin;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[count];
            for (int i = 0; i < count; i++)
            {
                float height = (i * towerHeight) / Mathf.Max(count, 1);
                float angle = height * rotationsPerUnit * Mathf.PI * 2;

                var pos = new Vector3(
                    Mathf.Cos(angle) * towerRadius,
                    height,
                    Mathf.Sin(angle) * towerRadius
                ) + origin;

                var lookTarget = new Vector3(0, height, 0) + origin;
                var rot = SpawnPoint.LookRotation(pos, lookTarget, Vector3.up);

                points[i] = new SpawnPoint(pos, rot);
            }
            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(count, towerHeight, towerRadius, rotationsPerUnit, seed, origin);
        }
    }
}
