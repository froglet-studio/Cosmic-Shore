using UnityEngine;

namespace CosmicShore.Splats
{
    [CreateAssetMenu(menuName = "CosmicShore/Splats/Splat Prism Set", fileName = "SplatPrismSet", order = 0)]
    public class SplatPrismSet : ScriptableObject
    {
        public const int MaxPrisms = 10000;

        [Header("Source")]
        [Tooltip("Original .ply path the bake came from (for traceability only).")]
        public string sourcePlyPath;

        [Header("Bake Stats")]
        public int parsedCount;
        public int afterCropCount;
        public int afterVoxelCount;
        public int finalCount;

        [Header("Bake Settings Snapshot")]
        public Vector3 sourceBoundsMin;
        public Vector3 sourceBoundsMax;
        public float voxelSize;
        public bool flippedHandedness;

        [Header("Curated Points (hard cap 10,000)")]
        public SplatPoint[] points;
    }
}
