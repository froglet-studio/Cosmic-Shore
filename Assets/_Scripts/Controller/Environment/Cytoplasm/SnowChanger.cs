using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class SnowChanger : MonoBehaviour
    {
        [SerializeField]
        CellRuntimeDataSO cellData;

        [SerializeField] GameObject snow;

        float nucleusRadius;
        float membraneRadius;

        [Header("Shard Density")]
        [Tooltip("Approximate spacing between shards - lower values produce more shards")]
        [SerializeField] int shardDistance = 100;

        [Header("Optional Fields")]
        [SerializeField] bool lookAt;

        [Tooltip("Shards reoriented per frame. Reorientation used to run as one O(shardCount) " +
                 "LookAt loop inline in the OnCellItemsUpdated raise — i.e. inside the crystal-" +
                 "pickup physics callback, a multi-ms self-time spike at ~4k shards. Slicing " +
                 "spreads it invisibly over a few frames (shards drift slowly by design).")]
        [SerializeField] int shardsPerFrame = 128;

        GameObject[] shards;
        readonly float nodeScaler = 10;
        readonly float nodeSize = .25f;
        Vector3 origin = Vector3.zero;
        Vector3 targetAxis;

        // Reorientation cursor: index of the next shard to process, or >= shards.Length
        // when idle. OnCellItemsUpdated only rewinds the cursor; Update does the work.
        int _reorientCursor = int.MaxValue;

        void OnEnable()
        {
            cellData.OnCellItemsUpdated.OnRaised += ChangeSnowOrientation;
        }

        void OnDisable()
        {
            cellData.OnCellItemsUpdated.OnRaised -= ChangeSnowOrientation;
        }

        public void Initialize()
        {
            if (!cellData.TryGetLocalCrystal(out Crystal crystal))
                return;

            if (cellData.Cell != null)
            {
                nucleusRadius = cellData.Cell.NucleusRadius;
                membraneRadius = cellData.Cell.MembraneRadius;
            }

            float innerR = nucleusRadius;
            float outerR = membraneRadius;
            float cellVolume = shardDistance * shardDistance * shardDistance;

            // Uniform density throughout the full sphere (0 → membrane)
            float sphereVolume = (4f / 3f) * Mathf.PI * (outerR * outerR * outerR);
            int shardCount = Mathf.Max(1, Mathf.RoundToInt(sphereVolume / cellVolume));
            float outerR3 = outerR * outerR * outerR;

            shards = new GameObject[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                float r = Mathf.Pow(Random.Range(0f, outerR3), 1f / 3f);
                float cosTheta = Random.Range(-1f, 1f);
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
                float phi = Random.Range(0f, 2f * Mathf.PI);

                GameObject tempSnow = Instantiate(snow, transform, true);
                tempSnow.transform.localScale = Vector3.one * nodeScaler;
                tempSnow.transform.position = origin + new Vector3(
                    r * sinTheta * Mathf.Cos(phi),
                    r * sinTheta * Mathf.Sin(phi),
                    r * cosTheta);

                shards[i] = tempSnow;
            }

            ChangeSnowOrientation();
        }

        /// <summary>Requests a reorientation pass. The work is time-sliced in Update
        /// (shardsPerFrame per frame) so SOAP raisers — e.g. the crystal-pickup chain,
        /// which raises OnCellItemsUpdated from a physics callback — never pay the
        /// O(shardCount) loop inline.</summary>
        public void ChangeSnowOrientation()
        {
            _reorientCursor = 0;
        }

        void Update()
        {
            if (shards == null || _reorientCursor >= shards.Length) return;
            ReorientSlice();
        }

        void ReorientSlice()
        {
            if (cellData == null || membraneRadius <= 0f ||
                !cellData.TryGetLocalCrystal(out Crystal crystal))
            {
                _reorientCursor = int.MaxValue;
                return;
            }

            int end = Mathf.Min(_reorientCursor + Mathf.Max(1, shardsPerFrame), shards.Length);
            float nodeScalerOverThree = nodeScaler / 3;
            Vector3 axisN = targetAxis.normalized; // hoisted: targetAxis is invariant across the shard loop
            for (int i = _reorientCursor; i < end; i++)
            {
                var shard = shards[i];
                float normalizedDistance;
                if (crystal)
                {
                    float clampedDistance =
                        Mathf.Clamp((shard.transform.position - crystal.transform.position).magnitude, 0,
                            membraneRadius);
                    normalizedDistance = clampedDistance / membraneRadius;
                    shard.transform.LookAt(crystal.transform);
                }
                else
                {
                    var reject = shard.transform.position -
                                 (Vector3.Dot(shard.transform.position, axisN) * axisN);
                    float clampedDistance = Mathf.Clamp(reject.magnitude, 0, membraneRadius);
                    normalizedDistance = clampedDistance / membraneRadius;
                    if (lookAt)
                        SafeLookRotation.TrySet(shard.transform, -reject.normalized, shard);
                    else
                        SafeLookRotation.TrySet(shard.transform, targetAxis, shard);
                }

                shard.transform.localScale = Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                                             Vector3.one * (normalizedDistance * nodeScalerOverThree +
                                                            nodeSize);
            }

            _reorientCursor = end;
        }

        public void SetOrigin(Vector3 o) => origin = o;
    }
}
