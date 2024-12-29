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
        
        private bool isScaling;
        public bool IsScaling
        {
            get => isScaling;
            set
            {
                if (isScaling != value)
                {
                    isScaling = value;
                    if (isScaling)
                    {
                        BlockScaleManager.Instance?.OnBlockStartScaling(this);
                    }
                    else
                    {
                        BlockScaleManager.Instance?.OnBlockStopScaling(this);
                    }
                }
            }
        }
        
        public Action OnScaleComplete { get; set; }

        private TrailBlock trailBlock;
        private Vector3 spread;
        private Vector3 outerDimensions;
        private MeshRenderer meshRenderer;
        private bool isRegistered;

        private void Awake()
        {
            // Cache components
            meshRenderer = GetComponent<MeshRenderer>();
            trailBlock = GetComponent<TrailBlock>();
            
            if (meshRenderer == null)
            {
                Debug.LogError($"MeshRenderer missing on {gameObject.name}");
                enabled = false;
                return;
            }

            // Start at zero scale
            transform.localScale = Vector3.zero;
            
            // Initialize spread for volume calculations
            if (meshRenderer.material != null)
            {
                spread = meshRenderer.material.GetVector("_Spread");
            }

            TryRegisterWithManager();
        }

        private void Start()
        {
            if (!isRegistered)
            {
                TryRegisterWithManager();
            }
        }

        private void TryRegisterWithManager()
        {
            if (BlockScaleManager.Instance != null && !isRegistered)
            {
                BlockScaleManager.Instance.RegisterBlock(this);
                isRegistered = true;
            }
        }

        private void OnEnable()
        {
            TryRegisterWithManager();
        }

        private void OnDisable()
        {
            if (BlockScaleManager.Instance != null && isRegistered)
            {
                BlockScaleManager.Instance.UnregisterBlock(this);
                isRegistered = false;
            }
        }

        public void BeginGrowthAnimation()
        {
            if (!enabled) return;

            // If TargetScale hasn't been set, use transform's scale as target
            if (TargetScale == Vector3.zero)
            {
                TargetScale = transform.localScale;
            }

            // Ensure we're starting from zero
            transform.localScale = Vector3.zero;
            IsScaling = true;
        }

        public void SetTargetScale(Vector3 newTarget)
        {
            if (!enabled) return;

            // Clamp the target scale within bounds
            newTarget.x = Mathf.Clamp(newTarget.x, minScale.x, maxScale.x);
            newTarget.y = Mathf.Clamp(newTarget.y, minScale.y, maxScale.y);
            newTarget.z = Mathf.Clamp(newTarget.z, minScale.z, maxScale.z);

            TargetScale = newTarget;

            // If not already scaling, start the growth animation
            if (!IsScaling)
            {
                BeginGrowthAnimation();
            }

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
            if (trailBlock == null) return;

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
            if (!enabled || trailBlock == null) return;
            Grow(amount * trailBlock.GrowthVector);
        }

        public void Grow(Vector3 growthVector)
        {
            if (!enabled) return;
            SetTargetScale(TargetScale + growthVector);
        }

        public float GetCurrentVolume()
        {
            if (!enabled) return 0f;
            outerDimensions = transform.localScale + 2 * spread;
            return outerDimensions.x * outerDimensions.y * outerDimensions.z;
        }

        private float UpdateVolume()
        {
            if (!enabled || trailBlock == null || trailBlock.TrailBlockProperties == null)
            {
                Debug.LogError($"Required components are null on {gameObject.name}");
                return 0f;
            }

            var oldVolume = trailBlock.TrailBlockProperties.volume;
            outerDimensions = TargetScale + 2 * spread;
            trailBlock.TrailBlockProperties.volume = outerDimensions.x * outerDimensions.y * outerDimensions.z;
            return trailBlock.TrailBlockProperties.volume - oldVolume;
        }

        private void OnDestroy()
        {
            if (BlockScaleManager.Instance != null && isRegistered)
            {
                BlockScaleManager.Instance.UnregisterBlock(this);
                isRegistered = false;
            }
            OnScaleComplete = null;
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
