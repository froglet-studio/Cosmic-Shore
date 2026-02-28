using CosmicShore.Core;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns an arrow shape made of prism trails.
/// A chevron arrowhead with a straight shaft. Scales with intensity.
/// </summary>
public class SpawnableArrow : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Arrow Parameters")]
    [SerializeField] float headWidth = 16f;
    [SerializeField] float headHeight = 12f;
    [SerializeField] float shaftLength = 24f;
    [SerializeField] float shaftWidth = 4f;

    protected override SpawnPoint[] GeneratePoints()
    {
        int blockCount = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();
        float hw = headWidth * sizeMul;
        float hh = headHeight * sizeMul;
        float sl = shaftLength * sizeMul;
        float sw = shaftWidth * sizeMul;

        // Arrow outline: tip → right wing → right shaft → bottom right → bottom left → left shaft → left wing → back to tip
        float shaftTop = -hh * 0.1f; // shaft starts slightly below tip
        var vertices = new Vector3[]
        {
            new(0f, hh, 0f),                       // tip
            new(hw * 0.5f, shaftTop, 0f),           // right wing
            new(sw * 0.5f, shaftTop, 0f),           // right shaft top
            new(sw * 0.5f, -sl, 0f),                // bottom right
            new(-sw * 0.5f, -sl, 0f),               // bottom left
            new(-sw * 0.5f, shaftTop, 0f),          // left shaft top
            new(-hw * 0.5f, shaftTop, 0f),          // left wing
        };

        int vertCount = vertices.Length;
        float totalLength = 0f;
        var segLengths = new float[vertCount];
        for (int i = 0; i < vertCount; i++)
        {
            segLengths[i] = Vector3.Distance(vertices[i], vertices[(i + 1) % vertCount]);
            totalLength += segLengths[i];
        }

        var points = new SpawnPoint[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            float dist = (float)i / blockCount * totalLength;
            float accumulated = 0f;
            int seg = 0;

            for (seg = 0; seg < vertCount - 1; seg++)
            {
                if (accumulated + segLengths[seg] > dist) break;
                accumulated += segLengths[seg];
            }

            float frac = (dist - accumulated) / segLengths[seg];
            var position = Vector3.Lerp(vertices[seg], vertices[(seg + 1) % vertCount], frac);
            var lookPosition = i == 0 ? position : points[i - 1].Position;
            var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
            points[i] = new SpawnPoint(position, rotation, Vector3.one);
        }

        return points;
    }

    protected override SpawnTrailData[] GenerateTrailData()
    {
        var pts = GeneratePoints();
        if (pts == null || pts.Length == 0)
            return System.Array.Empty<SpawnTrailData>();

        return new[] { new SpawnTrailData(pts, true, domain) };
    }

    protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
    {
        foreach (var td in trailData)
            SpawnPrismTrail(td.Points, container, prism, td.IsLoop, td.Domain);
    }

    protected override Prism GetPrismPrefab() => prism;

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(headWidth, headHeight, shaftLength, shaftWidth, baseBlockCount, intensityLevel,
            System.HashCode.Combine(seed, domain));
    }
}
