using CosmicShore.Soap;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SnowChanger : MonoBehaviour
    {
        [SerializeField]
        CellRuntimeDataSO cellData;

        [SerializeField] GameObject snow;

        float nucleusRadius;
        float membraneRadius;

        [Header("Shard Density")]
        [Tooltip("Approximate spacing between shards — lower values produce more shards")]
        [SerializeField] int shardDistance = 100;

        [Header("Optional Fields")]
        [SerializeField] bool lookAt;

        GameObject[] shards;
        readonly float nodeScaler = 10;
        readonly float nodeSize = .25f;
        Vector3 origin = Vector3.zero;
        Vector3 targetAxis;

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

            // Outer shell (nucleus → membrane): full density
            float outerShellVolume = (4f / 3f) * Mathf.PI * (outerR * outerR * outerR - innerR * innerR * innerR);
            int outerCount = Mathf.Max(1, Mathf.RoundToInt(outerShellVolume / cellVolume));

            // Inner sphere (0 → nucleus): 1/4 density
            float innerSphereVolume = (4f / 3f) * Mathf.PI * (innerR * innerR * innerR);
            int innerCount = Mathf.Max(0, Mathf.RoundToInt(0.25f * innerSphereVolume / cellVolume));

            int shardCount = outerCount + innerCount;
            float innerR3 = innerR * innerR * innerR;
            float outerR3 = outerR * outerR * outerR;

            shards = new GameObject[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                // First outerCount shards go in the shell, the rest inside the nucleus
                float r = i < outerCount
                    ? Mathf.Pow(Random.Range(innerR3, outerR3), 1f / 3f)
                    : Mathf.Pow(Random.Range(0f, innerR3), 1f / 3f);

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

        public void ChangeSnowOrientation()
        {
            if (!cellData.TryGetLocalCrystal(out Crystal crystal))
                return;

            if (shards == null) return;

            float nodeScalerOverThree = nodeScaler / 3;
            for (int i = 0; i < shards.Length; i++)
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
                                 (Vector3.Dot(shard.transform.position, targetAxis.normalized) *
                                  targetAxis.normalized);
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
        }

        public void SetOrigin(Vector3 o) => origin = o;
    }
}
