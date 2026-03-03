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

        [Header("Spherical Shell Boundaries")]
        [Tooltip("Inner radius — shards spawn outside this (the nucleus boundary)")]
        [SerializeField] float nucleusRadius = 30f;
        [Tooltip("Outer radius — shards spawn inside this (the membrane boundary)")]
        [SerializeField] float membraneRadius = 200f;

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

            float innerR = nucleusRadius;
            float outerR = membraneRadius;

            // Compute shard count from shell volume and desired spacing
            float shellVolume = (4f / 3f) * Mathf.PI * (outerR * outerR * outerR - innerR * innerR * innerR);
            float cellVolume = shardDistance * shardDistance * shardDistance;
            int shardCount = Mathf.Max(1, Mathf.RoundToInt(shellVolume / cellVolume));

            // Pre-compute cubed radii for volume-uniform spherical shell sampling
            float innerR3 = innerR * innerR * innerR;
            float outerR3 = outerR * outerR * outerR;

            shards = new GameObject[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                GameObject tempSnow = Instantiate(snow, transform, true);
                tempSnow.transform.localScale = Vector3.one * nodeScaler;

                // Volume-uniform random point in spherical shell
                float r = Mathf.Pow(Random.Range(innerR3, outerR3), 1f / 3f);
                float cosTheta = Random.Range(-1f, 1f);
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
                float phi = Random.Range(0f, 2f * Mathf.PI);

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
