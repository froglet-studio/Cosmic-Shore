using UnityEngine;
using System;
using CosmicShore.Utilities;

namespace CosmicShore.Core
{
    [RequireComponent(typeof(Prism))]
    public class PrismScaleAnimator : MonoBehaviour
    {
        [SerializeField]
        ScriptableEventPrismStats onPrismVolumeModified;
        
        [Header("Scale Constraints")]
        [SerializeField] private Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 maxScale = new Vector3(10f, 10f, 10f);

        public Vector3 MinScale => minScale; 
        public Vector3 MaxScale
        { 
            get => maxScale;
            set => maxScale = value; 
        }
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
                        PrismScaleManager.Instance?.OnBlockStartScaling(this);
                    }
                    else
                    {
                        PrismScaleManager.Instance?.OnBlockStopScaling(this);
                    }
                }
            }
        }

        private Prism prism;
        // private Vector3 spread;
        // private Vector3 outerDimensions;             // REMARKS -> OuterDimensions that account for spread introduces unnecessary complications. especially with dynamic spread.
        private MeshRenderer meshRenderer;
        private bool isRegistered;

        private void Awake()
        {
            // Cache components
            meshRenderer = GetComponent<MeshRenderer>();
            prism = GetComponent<Prism>();
            
            if (meshRenderer == null)
            {
                Debug.LogError($"MeshRenderer missing on {gameObject.name}");
                enabled = false;
                return;
            }

            // Start at zero scale
            transform.localScale = Vector3.zero;
            
            // Initialize spread for volume calculations
            /*if (meshRenderer.material != null)
            {
                spread = meshRenderer.material.GetVector("_Spread");
            }*/
        }

        public void Initialize()
        {
            if (!isRegistered)
            {
                TryRegisterWithManager();
            }
        }
        
        private void TryRegisterWithManager()
        {
            if (PrismScaleManager.Instance != null && !isRegistered)
            {
                PrismScaleManager.Instance.RegisterAnimator(this);
                isRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (PrismScaleManager.Instance != null && isRegistered)
            {
                PrismScaleManager.Instance.UnregisterAnimator(this);
                isRegistered = false;
            }
        }

        public void BeginGrowthAnimation()
        {
            if (!enabled) return;
            if (IsScaling) return;

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
        }
        
        public void ExecuteOnScaleComplete()
        {
            var deltaVolume = UpdateVolume();
            onPrismVolumeModified.Raise(
                new PrismStats
                {
                    Volume = deltaVolume,
                    PlayerName = prism.PlayerName,
                });
                
            /*if (StatsManager.Instance != null)
            {
                StatsManager.Instance.PrismVolumeModified(deltaVolume, trailBlock.PrismProperties);
            }*/

            if (prism == null) return;
                
            if (CheckIfIsLargest())
            {
                prism.ActivateShield();
                prism.IsLargest = true;
            }

            if (CheckIfIsSmallest())
            {
                prism.IsSmallest = true;
            }
        }
        
        private bool CheckIfIsLargest() => 
            TargetScale.x > MaxScale.x || TargetScale.y > MaxScale.y || TargetScale.z > MaxScale.z;

        private bool CheckIfIsSmallest() =>
            TargetScale.x < MinScale.x || TargetScale.y < MinScale.y || TargetScale.z < MinScale.z;

        public void Grow(float amount = 1)
        {
            if (!enabled || !prism) return;
            var growthVector = amount * prism.GrowthVector;
            SetTargetScale(TargetScale + growthVector);
            BeginGrowthAnimation();
        }

        public float GetCurrentVolume()
        {
            if (!enabled) return 0f;
            
            /*outerDimensions = transform.localScale + 2 * spread;
            return outerDimensions.x * outerDimensions.y * outerDimensions.z;*/
            
            var v = transform.localScale;
            return v.x * v.y * v.z;
        }
        
        private float UpdateVolume()
        {
            if (!enabled || prism == null || prism.prismProperties == null)
            {
                Debug.LogError($"Required components are null on {gameObject.name}");
                return 0f;
            }

            var oldVolume = prism.prismProperties.volume;
            /*outerDimensions = TargetScale + 2 * spread;
            prism.prismProperties.volume = outerDimensions.x * outerDimensions.y * outerDimensions.z;*/

            prism.prismProperties.volume = TargetScale.x * TargetScale.y * TargetScale.z;
            
            return prism.prismProperties.volume - oldVolume;
        }

        private void OnDestroy()
        {
            if (PrismScaleManager.Instance != null && isRegistered)
            {
                PrismScaleManager.Instance.UnregisterAnimator(this);
                isRegistered = false;
            }
        }

    }
}
