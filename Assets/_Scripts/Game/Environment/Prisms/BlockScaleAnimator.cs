using UnityEngine;
using System;
using Unity.Mathematics;

namespace CosmicShore.Core
{
    public class BlockScaleAnimator : MonoBehaviour
    {
        [Header("Scale Constraints")]
        [SerializeField] private Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 maxScale = new Vector3(10f, 10f, 10f);

        public Vector3 MinScale => minScale; 
        public Vector3 MaxScale => maxScale; 
        public Vector3 TargetScale { get; private set; }  
        public float GrowthRate { get; set; } = 0.01f;
        public bool IsScaling { get; set; }
        public Action OnScaleComplete { get; set; }

        private TrailBlock trailBlock;
        private Vector3 spread;
        private Vector3 outerDimensions;

        private void Awake()
        {
            trailBlock = GetComponent<TrailBlock>();
            BlockScaleManager.Instance.RegisterBlock(this);
            TargetScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            spread = GetComponent<MeshRenderer>().material.GetVector("_Spread");
            UpdateVolume();
        }

        public void InitializeScale()
        {
            // Set initial microscopic scale
            transform.localScale = Vector3.one * Mathf.Epsilon;
        }

        public void BeginGrowthAnimation()
        {
            SetTargetScale(TargetScale);
            IsScaling = true;
        }

        public void SetTargetScale(Vector3 newTarget)
        {
            // Clamp the target scale within bounds
            newTarget.x = Mathf.Clamp(newTarget.x, minScale.x, maxScale.x);
            newTarget.y = Mathf.Clamp(newTarget.y, minScale.y, maxScale.y);
            newTarget.z = Mathf.Clamp(newTarget.z, minScale.z, maxScale.z);

            TargetScale = newTarget;
            IsScaling = true;
            OnScaleComplete = () =>
            {
                var deltaVolume = UpdateVolume();
                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.BlockVolumeModified(deltaVolume, trailBlock.TrailBlockProperties);
                }

                CheckScaleBounds();
            };
        }

        private void CheckScaleBounds()
        {
            if (TargetScale.x > MaxScale.x || TargetScale.y > MaxScale.y || TargetScale.z > MaxScale.z)
            {
                trailBlock.ActivateShield();
                trailBlock.IsLargest = true;
            }
            if (TargetScale.x < MinScale.x || TargetScale.y < MinScale.y || TargetScale.z < MinScale.z)
            {
                trailBlock.IsSmallest = true;
            }
        }

        public void Grow(float amount = 1)
        {
            Grow(amount * trailBlock.GrowthVector);
        }

        public void Grow(Vector3 growthVector)
        {
            SetTargetScale(TargetScale + growthVector);
        }

        public float GetCurrentVolume()
        {
            outerDimensions = transform.localScale + 2 * spread;
            return outerDimensions.x * outerDimensions.y * outerDimensions.z;
        }

        private float UpdateVolume()
        {
            var oldVolume = trailBlock.TrailBlockProperties.volume;
            outerDimensions = TargetScale + 2 * spread;
            trailBlock.TrailBlockProperties.volume = outerDimensions.x * outerDimensions.y * outerDimensions.z;
            return trailBlock.TrailBlockProperties.volume - oldVolume;
        }

        private void OnDestroy()
        {
            if (BlockScaleManager.Instance != null)
            {
                BlockScaleManager.Instance.UnregisterBlock(this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure min scale is never larger than max scale
            minScale.x = Mathf.Min(minScale.x, maxScale.x);
            minScale.y = Mathf.Min(minScale.y, maxScale.y);
            minScale.z = Mathf.Min(minScale.z, maxScale.z);
        }
#endif
    }
}