using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Gameplay;
using System.Linq;
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

        public void ChangeSnowOrientation()
        {
            if (cellData == null)
                return;

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
