using CosmicShore.Core;
using CosmicShore.Game.Spawning;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Spawns a smiley face shape using multiple prism trails (left eye, right eye, mouth arc).
/// Block count scales with intensity via GetScaledBlockCount().
/// </summary>
public class SpawnableSmiley : SpawnableShapeBase
{
    [FormerlySerializedAs("trailBlock")] [SerializeField] Prism prism;

    [Header("Smiley Parameters")]
    [SerializeField] float faceRadius = 20f;

    protected override SpawnTrailData[] GenerateTrailData()
    {
        int totalBlocks = GetScaledBlockCount();
        float sizeMul = GetIntensitySizeMultiplier();
        float scaledFace = faceRadius * sizeMul;

        // Distribute blocks: ~20% per eye, ~60% for mouth
        int eyeBlocks = Mathf.Max(6, totalBlocks / 5);
        int mouthBlocks = totalBlocks - eyeBlocks * 2;

        var leftEye = GenerateEyePoints(-scaledFace * 0.3f, scaledFace * 0.25f, scaledFace * 0.12f, eyeBlocks);
        var rightEye = GenerateEyePoints(scaledFace * 0.3f, scaledFace * 0.25f, scaledFace * 0.12f, eyeBlocks);
        var mouth = GenerateMouthPoints(mouthBlocks, scaledFace);

        return new[]
        {
            new SpawnTrailData(leftEye, true, Domains.Jade),
            new SpawnTrailData(rightEye, true, Domains.Ruby),
            new SpawnTrailData(mouth, false, Domains.Gold),
        };
    }

    SpawnPoint[] GenerateEyePoints(float centerX, float centerY, float eyeRadius, int blockCount)
    {
        var points = new SpawnPoint[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            float t = (float)i / blockCount * Mathf.PI * 2f;
            var position = new Vector3(
                centerX + Mathf.Cos(t) * eyeRadius,
                centerY + Mathf.Sin(t) * eyeRadius,
                0f);
            var lookPosition = i == 0 ? position : points[i - 1].Position;
            var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
            points[i] = new SpawnPoint(position, rotation, Vector3.one);
        }
        return points;
    }

    SpawnPoint[] GenerateMouthPoints(int blockCount, float scaledFace)
    {
        var points = new SpawnPoint[blockCount];
        float mouthRadius = scaledFace * 0.45f;
        float mouthY = -scaledFace * 0.1f;

        for (int i = 0; i < blockCount; i++)
        {
            // Lower semicircle: from PI to 2*PI
            float t = Mathf.PI + (float)i / (blockCount - 1) * Mathf.PI;
            var position = new Vector3(
                Mathf.Cos(t) * mouthRadius,
                mouthY + Mathf.Sin(t) * mouthRadius * 0.5f,
                0f);
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

    protected override int GetParameterHash()
    {
        return System.HashCode.Combine(faceRadius, baseBlockCount, intensityLevel, seed);
    }
}
