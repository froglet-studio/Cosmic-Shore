using CosmicShore.Engine;
using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    // PORT Deviation (V13, restore when Prism ports): [RequireComponent(typeof(Prism))]
    public class PrismScaleAnimator : MonoBehaviour
    {
        [SerializeField] ScriptableEventPrismStats onPrismVolumeModified;

        [Header("Scale Constraints")]
        [SerializeField] private Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 maxScale = new Vector3(10f, 10f, 10f);

        [Header("Defaults")]
        [SerializeField] private bool usePrefabScaleAsDefaultTarget;
        [SerializeField] private Vector3 authoredTargetScale;
        public Vector3 MinScale => minScale;
        public Vector3 MaxScale { get => maxScale; set => maxScale = value; }

        public Vector3 TargetScale { get; private set; }
        public Vector3 AuthoredTargetScale => authoredTargetScale;
        public float GrowthRate { get; set; } = 0.01f;

        // PORT Deviation (V13, restore when Prism ports): private Prism prism;
        private MeshRenderer meshRenderer;
        private bool isRegistered;

        private bool isScaling;
        public bool IsScaling
        {
            get => isScaling;
            set
            {
                if (isScaling.Equals(value)) return;
                isScaling = value;

                // PORT Deviation (V13, restore when PrismScaleManager ports): if (isScaling) PrismScaleManager.Instance?.OnBlockStartScaling(this);
                // PORT Deviation (V13, restore when PrismScaleManager ports): else PrismScaleManager.Instance?.OnBlockStopScaling(this);
            }
        }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            // PORT Deviation (V13, restore when Prism ports): prism = GetComponent<Prism>();

            if (meshRenderer == null)
            {
                CSDebug.LogError($"MeshRenderer missing on {gameObject.name}");
                enabled = false;
                return;
            }

            if (authoredTargetScale.Equals(Vector3.zero))
                authoredTargetScale = transform.localScale;

            if (TargetScale == Vector3.zero)
                SetTargetScale(authoredTargetScale);

            transform.localScale = Vector3.zero;
        }

        public void Initialize()
        {
            if (isRegistered) return;
            // PORT Deviation (V13, restore when PrismScaleManager ports): if (!PrismScaleManager.Instance) return;
            // PORT Deviation (V13, restore when PrismScaleManager ports): PrismScaleManager.Instance.RegisterAnimator(this);
            isRegistered = true;
        }

        private void OnDisable()
        {
            // PORT Deviation (V13, restore when PrismScaleManager ports): if (PrismScaleManager.Instance == null || !isRegistered) return;
            if (!isRegistered) return;
            // PORT Deviation (V13, restore when PrismScaleManager ports): PrismScaleManager.Instance.UnregisterAnimator(this);
            isRegistered = false;
        }

        public void BeginGrowthAnimation(bool resetToZero = false)
        {
            if (!enabled) return;
            if (IsScaling) return;

            if (TargetScale == Vector3.zero)
                TargetScale = transform.localScale;

            if (resetToZero)
                transform.localScale = Vector3.zero;

            IsScaling = true;
        }

        public void SetTargetScale(Vector3 newTarget)
        {
            if (!enabled) return;

            newTarget.x = Mathf.Clamp(newTarget.x, minScale.x, maxScale.x);
            newTarget.y = Mathf.Clamp(newTarget.y, minScale.y, maxScale.y);
            newTarget.z = Mathf.Clamp(newTarget.z, minScale.z, maxScale.z);

            TargetScale = newTarget;
        }

        public void Grow(float amount = 1)
        {
            // PORT Deviation (V13, restore when Prism ports): if (!enabled || !prism) return;
            if (!enabled) return;

            // PORT Deviation (V13, restore when Prism ports): var growthVector = amount * prism.GrowthVector;
            // PORT Deviation (V13, restore when Prism ports): SetTargetScale(TargetScale + growthVector);
            BeginGrowthAnimation();
        }

        public float GetCurrentVolume()
        {
            if (!enabled) return 0f;
            var v = transform.lossyScale; // Use lossyScale to get the actual world scale, which accounts for parent scaling
            return v.x * v.y * v.z;
        }

        public void ExecuteOnScaleComplete()
        {
            var deltaVolume = UpdateVolume();
            onPrismVolumeModified.Raise(new PrismStats
            {
                Volume = deltaVolume,
                // PORT Deviation (V13, restore when Prism ports): OwnName = prism.PlayerName,
            });

            // PORT Deviation (V13, restore when Prism ports): if (!prism) return;

            if (CheckIfIsLargest())
            {
                // PORT Deviation (V13, restore when Prism ports): prism.ActivateShield();
                // PORT Deviation (V13, restore when Prism ports): prism.IsLargest = true;
            }

            if (CheckIfIsSmallest())
            {
                // PORT Deviation (V13, restore when Prism ports): prism.IsSmallest = true;
            }
        }

        private bool CheckIfIsLargest() =>
            TargetScale.x > MaxScale.x || TargetScale.y > MaxScale.y || TargetScale.z > MaxScale.z;

        private bool CheckIfIsSmallest() =>
            TargetScale.x < MinScale.x || TargetScale.y < MinScale.y || TargetScale.z < MinScale.z;

        private float UpdateVolume()
        {
            // PORT Deviation (V13, restore when Prism (V15) / PrismProperties (V14) port): if (!enabled || !prism || prism.prismProperties == null)
            if (!enabled)
            {
                CSDebug.LogError($"Required components are null on {gameObject.name}");
                return 0f;
            }

            // The conserved-mass volume record lives on PrismProperties (V14); until it
            // lands the delta reports 0 rather than fabricating a different bookkeeping.
            // PORT Deviation (V13, restore when Prism (V15) / PrismProperties (V14) port): var oldVolume = prism.prismProperties.volume;
            // PORT Deviation (V13, restore when Prism (V15) / PrismProperties (V14) port): prism.prismProperties.volume = TargetScale.x * TargetScale.y * TargetScale.z;
            // PORT Deviation (V13, restore when Prism (V15) / PrismProperties (V14) port): return prism.prismProperties.volume - oldVolume;
            return 0f;
        }

        private void OnDestroy()
        {
            // PORT Deviation (V13, restore when PrismScaleManager ports): if (!PrismScaleManager.Instance || !isRegistered) return;
            if (!isRegistered) return;
            // PORT Deviation (V13, restore when PrismScaleManager ports): PrismScaleManager.Instance.UnregisterAnimator(this);
            isRegistered = false;
        }
    }
}
