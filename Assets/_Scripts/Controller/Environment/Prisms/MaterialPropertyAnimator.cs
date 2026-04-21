using UnityEngine;
using System;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class MaterialPropertyAnimator : MonoBehaviour
    {
        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        private static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");

        public MaterialPropertyBlock PropertyBlock { get; private set; }
        public MeshRenderer MeshRenderer { get; private set; }
        public float AnimationProgress { get; set; } = 1f;
        public Action OnAnimationComplete { get; set; }

        public Color StartBrightColor { get; private set; }
        public Color TargetBrightColor { get; private set; }
        public Color StartDarkColor { get; private set; }
        public Color TargetDarkColor { get; private set; }
        public Vector3 StartSpread { get; private set; }
        public Vector3 TargetSpread { get; private set; }

        public float Duration { get; private set; }
        
        private bool isAnimating;
        public bool IsAnimating
        {
            get => isAnimating;
            set
            {
                if (isAnimating != value)
                {
                    isAnimating = value;
                    if (isAnimating)
                    {
                        MaterialStateManager.Instance?.OnAnimatorStartAnimating(this);
                    }
                    else
                    {
                        MaterialStateManager.Instance?.OnAnimatorStopAnimating(this);
                    }
                }
            }
        }

        private Material activeTransparentMaterial;
        private Material activeOpaqueMaterial;
        private bool isRegistered;
        private Prism cachedPrism;
        private bool materialsDirty;

        private void Awake()
        {
            // Cache components
            MeshRenderer = GetComponent<MeshRenderer>();
            cachedPrism = GetComponent<Prism>();
            
            if (MeshRenderer == null)
            {
                CSDebug.LogError($"MeshRenderer missing on {gameObject.name}");
                enabled = false;
                return;
            }

            PropertyBlock = new MaterialPropertyBlock();
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
            if (MaterialStateManager.Instance != null && !isRegistered)
            {
                MaterialStateManager.Instance.RegisterAnimator(this);
                isRegistered = true;
            }
        }

        private void OnEnable()
        {
            TryRegisterWithManager();
        }

        private void OnDisable()
        {
            if (MaterialStateManager.Instance != null && isRegistered)
            {
                MaterialStateManager.Instance.UnregisterAnimator(this);
                isRegistered = false;
            }
        }

        private bool ValidateMaterials()
        {
            if (!materialsDirty && activeTransparentMaterial != null && activeOpaqueMaterial != null)
                return true;

            if (cachedPrism == null)
                return false;

            try
            {
                var team = cachedPrism.Domain;
                activeOpaqueMaterial = _themeManagerData.GetTeamBlockMaterial(team);
                activeTransparentMaterial = _themeManagerData.GetTeamTransparentBlockMaterial(team);
                
                if (activeOpaqueMaterial != null && activeTransparentMaterial != null && MeshRenderer != null)
                {
                    if (cachedPrism.prismProperties != null && cachedPrism.prismProperties.IsTransparent)
                        MeshRenderer.sharedMaterial = activeTransparentMaterial;
                    else
                        MeshRenderer.sharedMaterial = activeOpaqueMaterial;
                }
                
                materialsDirty = false;
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogError($"Error validating materials: {e.Message}");
                return false;
            }
        }

        public void UpdateMaterial(Material transparentMaterial, Material opaqueMaterial, float duration = 0.8f, Action onComplete = null)
        {
            if (!enabled || MeshRenderer == null) return;

            if (transparentMaterial == null || opaqueMaterial == null)
            {
                CSDebug.LogError($"Invalid materials provided to {gameObject.name}");
                return;
            }

            if (!ValidateMaterials()) return;

            // If already animating, capture current state as start state
            if (IsAnimating)
            {
                MeshRenderer.GetPropertyBlock(PropertyBlock);
                StartBrightColor = PropertyBlock.GetColor(BrightColorId);
                StartDarkColor = PropertyBlock.GetColor(DarkColorId);
                StartSpread = PropertyBlock.GetVector(SpreadId);
            }
            else
            {
                var currentMaterial = MeshRenderer.sharedMaterial;
                StartBrightColor = currentMaterial.GetColor(BrightColorId);
                StartDarkColor = currentMaterial.GetColor(DarkColorId);
                StartSpread = currentMaterial.GetVector(SpreadId);
            }

            // Set target values
            TargetBrightColor = transparentMaterial.GetColor(BrightColorId);
            TargetDarkColor = transparentMaterial.GetColor(DarkColorId);
            TargetSpread = transparentMaterial.GetVector(SpreadId);

            Duration = duration;
            AnimationProgress = 0f;
            IsAnimating = true;
            OnAnimationComplete = () =>
            {
                activeTransparentMaterial = transparentMaterial;
                activeOpaqueMaterial = opaqueMaterial;

                if (MeshRenderer != null && cachedPrism != null &&
                    cachedPrism.prismProperties != null)
                {
                    MeshRenderer.sharedMaterial = cachedPrism.prismProperties.IsTransparent ?
                        transparentMaterial : opaqueMaterial;
                }

                onComplete?.Invoke();
            };
        }

        /// <summary>
        /// Synchronously swap sharedMaterial and update the cached active materials.
        /// Bypasses the 0.8s color-blend animation run by MaterialStateManager — use
        /// this when the visual state change must be immediate (e.g. shield engage
        /// on a prism that may be consumed within a single fauna behavior tick).
        /// </summary>
        public void SetMaterialImmediate(Material transparentMaterial, Material opaqueMaterial)
        {
            if (!enabled || MeshRenderer == null) return;
            if (transparentMaterial == null || opaqueMaterial == null) return;

            activeTransparentMaterial = transparentMaterial;
            activeOpaqueMaterial = opaqueMaterial;
            materialsDirty = false;

            bool useTransparent = cachedPrism != null
                                  && cachedPrism.prismProperties != null
                                  && cachedPrism.prismProperties.IsTransparent;

            MeshRenderer.sharedMaterial = useTransparent ? transparentMaterial : opaqueMaterial;

            // Kill any in-flight color animation so it doesn't overwrite the swap
            // on the next MaterialStateManager tick.
            if (IsAnimating)
            {
                IsAnimating = false;
                AnimationProgress = 1f;
                OnAnimationComplete = null;
            }
        }

        public void SetTransparency(bool transparent)
        {
            if (MeshRenderer != null && ValidateMaterials())
            {
                MeshRenderer.sharedMaterial = transparent ? activeTransparentMaterial : activeOpaqueMaterial;
                cachedPrism.prismProperties.IsTransparent = transparent;
            }
        }

        public void MarkMaterialsDirty()
        {
            materialsDirty = true;
        }

        private void OnDestroy()
        {
            if (MaterialStateManager.Instance != null && isRegistered)
            {
                MaterialStateManager.Instance.UnregisterAnimator(this);
                isRegistered = false;
            }
            OnAnimationComplete = null;
        }
    }
}