using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a sine wave shape made of prism trails.
/// Smooth oscillating wave with configurable amplitude, wavelength, and cycle count. Scales with intensity.
/// </summary>
public class SpawnableWave : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Wave Parameters")]
    [SerializeField] float amplitude = 12f;
    [SerializeField] float wavelength = 30f;
    [SerializeField] int cycles = 2;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();
        float scaledAmplitude = amplitude * sizeMul;
        float scaledWavelength = wavelength * sizeMul;
        var points = new SpawnPoint[blockCount];

        float totalWidth = scaledWavelength * cycles;

        for (int i = 0; i < blockCount; i++)
        {
            float t = (float)i / blockCount;
            float x = t * totalWidth - totalWidth * 0.5f;
            float y = Mathf.Sin(t * cycles * Mathf.PI * 2f) * scaledAmplitude;
            var position = new Vector3(x, y, 0f);
            var lookPosition = i == 0 ? position : points[i - 1].Position;
            var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
            points[i] = new SpawnPoint(position, rotation, Vector3.one);
        }

        return points;
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override Prism GetPrismPrefab() => prism;

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(amplitude, wavelength, cycles, baseBlockCount);
    }
}
