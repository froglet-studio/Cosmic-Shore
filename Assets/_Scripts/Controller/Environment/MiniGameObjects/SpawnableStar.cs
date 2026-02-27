using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a 5-pointed star shape made of prism trails.
/// Block count scales with intensity via GetScaledBlockCount().
/// </summary>
public class SpawnableStar : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Star Parameters")]
    [SerializeField] float outerRadius = 20f;
    [SerializeField] float innerRadius = 8f;
    [SerializeField] int points = 5;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        int totalVertices = points * 2;
        var spawnPoints = new SpawnPoint[blockCount];

        for (int i = 0; i < blockCount; i++)
        {
            // Map block index to position along the star outline
            float t = (float)i / blockCount * totalVertices;
            int vertexIndex = Mathf.FloorToInt(t);
            float frac = t - vertexIndex;

            // Current and next vertex positions on the star
            var posA = GetStarVertex(vertexIndex % totalVertices);
            var posB = GetStarVertex((vertexIndex + 1) % totalVertices);
            var position = Vector3.Lerp(posA, posB, frac);

            var lookPosition = i == 0 ? position : spawnPoints[i - 1].Position;
            var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
            spawnPoints[i] = new SpawnPoint(position, rotation, Vector3.one);
        }

        return spawnPoints;
    }

    Vector3 GetStarVertex(int index)
    {
        float sizeMul = GetIntensitySizeMultiplier();
        int totalVertices = points * 2;
        float angle = (index / (float)totalVertices) * Mathf.PI * 2f - Mathf.PI / 2f;
        float r = (index % 2 == 0) ? outerRadius * sizeMul : innerRadius * sizeMul;
        return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override Prism GetPrismPrefab() => prism;

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(outerRadius, innerRadius, points, baseBlockCount, intensityLevel, seed, domain);
    }
}
