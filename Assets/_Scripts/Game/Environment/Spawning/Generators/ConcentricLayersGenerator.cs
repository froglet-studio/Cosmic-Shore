using UnityEngine;

namespace CosmicShore.Game.Spawning
{
    /// <summary>
    /// Generates concentric layers at the origin with decreasing scale.
    /// Wire any SpawnableBase as a child to create nested structures —
    /// e.g., a SpawnableSpherene child produces concentric geodesic shells.
    /// </summary>
    public class ConcentricLayersGenerator : SpawnableBase
    {
        [Header("Concentric Layers")]
        [Tooltip("Number of nested layers.")]
        [SerializeField, Min(1)] int layers = 3;

        [Tooltip("Scale multiplier applied per layer. Each successive layer is scaled by this factor relative to the previous.")]
        [SerializeField, Range(0.1f, 0.99f)] float scaleFalloff = 0.7f;

        [Tooltip("Optional rotation offset (degrees) applied per layer for visual variety.")]
        [SerializeField] Vector3 rotationPerLayer = Vector3.zero;

        protected override SpawnPoint[] GeneratePoints()
        {
            var points = new SpawnPoint[layers];
            float currentScale = 1f;
            var currentRotation = Quaternion.identity;

            for (int i = 0; i < layers; i++)
            {
                points[i] = new SpawnPoint(
                    Vector3.zero,
                    currentRotation,
                    Vector3.one * currentScale
                );

                currentScale *= scaleFalloff;

                if (rotationPerLayer != Vector3.zero)
                    currentRotation *= Quaternion.Euler(rotationPerLayer);
            }

            return points;
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(layers, scaleFalloff, rotationPerLayer, seed);
        }
    }
}
