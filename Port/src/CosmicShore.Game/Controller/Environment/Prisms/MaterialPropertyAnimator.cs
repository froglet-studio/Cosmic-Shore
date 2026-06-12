using CosmicShore.Engine;
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
                        // PORT Deviation (V14, restore when MaterialStateManager ports): MaterialStateManager.Instance?.OnAnimatorStartAnimating(this);
                    }
                    else
                    {
                        // PORT Deviation (V14, restore when MaterialStateManager ports): MaterialStateManager.Instance?.OnAnimatorStopAnimating(this);
                    }
                }
            }
        }

        private Material activeTransparentMaterial;
        private Material activeOpaqueMaterial;
        private bool isRegistered;
        // PORT Deviation (V14, restore when Prism ports): private Prism cachedPrism;
        private bool materialsDirty;

        private void Awake()
        {
            // Cache components
            MeshRenderer = GetComponent<MeshRenderer>();
            // PORT Deviation (V14, restore when Prism ports): cachedPrism = GetComponent<Prism>();

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
            // PORT Deviation (V14, restore when MaterialStateManager ports): if (MaterialStateManager.Instance != null && !isRegistered)
            // PORT Deviation (V14, restore when MaterialStateManager ports): {
            // PORT Deviation (V14, restore when MaterialStateManager ports):     MaterialStateManager.Instance.RegisterAnimator(this);
            // PORT Deviation (V14, restore when MaterialStateManager ports):     isRegistered = true;
            // PORT Deviation (V14, restore when MaterialStateManager ports): }
        }

        private void OnEnable()
        {
            TryRegisterWithManager();
        }

        private void OnDisable()
        {
            // PORT Deviation (V14, restore when MaterialStateManager ports): if (MaterialStateManager.Instance != null && isRegistered)
            // PORT Deviation (V14, restore when MaterialStateManager ports): {
            // PORT Deviation (V14, restore when MaterialStateManager ports):     MaterialStateManager.Instance.UnregisterAnimator(this);
            // PORT Deviation (V14, restore when MaterialStateManager ports):     isRegistered = false;
            // PORT Deviation (V14, restore when MaterialStateManager ports): }
        }

        private bool ValidateMaterials()
        {
            if (!materialsDirty && activeTransparentMaterial != null && activeOpaqueMaterial != null)
                return true;

            // PORT Deviation (V14, restore when Prism ports): if (cachedPrism == null)
            // PORT Deviation (V14, restore when Prism ports):     return false;

            try
            {
                // PORT Deviation (V14, restore when Prism ports): var team = cachedPrism.Domain;
                // PORT Deviation (V14, restore when Prism ports): activeOpaqueMaterial = _themeManagerData.GetTeamBlockMaterial(team);
                // PORT Deviation (V14, restore when Prism ports): activeTransparentMaterial = _themeManagerData.GetTeamTransparentBlockMaterial(team);

                if (activeOpaqueMaterial != null && activeTransparentMaterial != null && MeshRenderer != null)
                {
                    // PORT Deviation (V14, restore when Prism ports): if (cachedPrism.prismProperties != null && cachedPrism.prismProperties.IsTransparent)
                    // PORT Deviation (V14, restore when Prism ports):     MeshRenderer.sharedMaterial = activeTransparentMaterial;
                    // PORT Deviation (V14, restore when Prism ports): else
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

                // PORT Deviation (V14, restore when Prism ports): if (MeshRenderer != null && cachedPrism != null &&
                // PORT Deviation (V14, restore when Prism ports):     cachedPrism.prismProperties != null)
                // PORT Deviation (V14, restore when Prism ports): {
                // PORT Deviation (V14, restore when Prism ports):     MeshRenderer.sharedMaterial = cachedPrism.prismProperties.IsTransparent ?
                // PORT Deviation (V14, restore when Prism ports):         transparentMaterial : opaqueMaterial;
                // PORT Deviation (V14, restore when Prism ports): }

                onComplete?.Invoke();
            };
        }

        public void SetTransparency(bool transparent)
        {
            if (MeshRenderer != null && ValidateMaterials())
            {
                MeshRenderer.sharedMaterial = transparent ? activeTransparentMaterial : activeOpaqueMaterial;
                // PORT Deviation (V14, restore when Prism ports): cachedPrism.prismProperties.IsTransparent = transparent;
            }
        }

        public void MarkMaterialsDirty()
        {
            materialsDirty = true;
        }

        private void OnDestroy()
        {
            // PORT Deviation (V14, restore when MaterialStateManager ports): if (MaterialStateManager.Instance != null && isRegistered)
            // PORT Deviation (V14, restore when MaterialStateManager ports): {
            // PORT Deviation (V14, restore when MaterialStateManager ports):     MaterialStateManager.Instance.UnregisterAnimator(this);
            // PORT Deviation (V14, restore when MaterialStateManager ports):     isRegistered = false;
            // PORT Deviation (V14, restore when MaterialStateManager ports): }
            OnAnimationComplete = null;
        }
    }
}
