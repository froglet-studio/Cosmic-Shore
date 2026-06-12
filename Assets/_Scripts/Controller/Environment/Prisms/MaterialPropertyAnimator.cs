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

        // Last colors actually displayed, written by MaterialStateManager each
        // animated frame. This is the interruption start-state for BOTH render
        // paths — the entity path has no MaterialPropertyBlock to read back from.
        public Color CurrentBrightColor { get; internal set; }
        public Color CurrentDarkColor { get; internal set; }
        public Vector3 CurrentSpread { get; internal set; }

        /// <summary>Owning prism — MaterialStateManager routes animated colors to
        /// its companion render entity when the instanced path is active.</summary>
        internal Prism CachedPrism => cachedPrism;

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
                    cachedPrism.SyncRenderMaterial();
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

            // If already animating, capture current state as start state. Uses the
            // manager-tracked current values (works for both the MPB path and the
            // entity path, which has no property block to read back).
            if (IsAnimating)
            {
                StartBrightColor = CurrentBrightColor;
                StartDarkColor = CurrentDarkColor;
                StartSpread = CurrentSpread;
            }
            else
            {
                var currentMaterial = MeshRenderer.sharedMaterial;
                StartBrightColor = currentMaterial.GetColor(BrightColorId);
                StartDarkColor = currentMaterial.GetColor(DarkColorId);
                StartSpread = currentMaterial.GetVector(SpreadId);
            }

            // Seed the tracked currents so an interruption before the first
            // animated frame still has a valid start state.
            CurrentBrightColor = StartBrightColor;
            CurrentDarkColor = StartDarkColor;
            CurrentSpread = StartSpread;

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
                    cachedPrism.SyncRenderMaterial();
                }

                onComplete?.Invoke();
            };
        }

        public void SetTransparency(bool transparent)
        {
            if (MeshRenderer != null && ValidateMaterials())
            {
                MeshRenderer.sharedMaterial = transparent ? activeTransparentMaterial : activeOpaqueMaterial;
                cachedPrism.prismProperties.IsTransparent = transparent;
                cachedPrism.SyncRenderMaterial();
            }
        }

        public void MarkMaterialsDirty()
        {
            materialsDirty = true;
        }

        /// <summary>
        /// Writes the currently displayed colors into the renderer's
        /// MaterialPropertyBlock. Used when rendering hands off from the
        /// companion entity to the GameObject (octahedron engage): the MPB may
        /// hold colors from long before the entity path took over, and a stale
        /// block would flash for a frame. Mid-animation we pin the tracked
        /// current values; at rest we clear so the base sharedMaterial (which
        /// the completed animation already matched) shows through.
        /// </summary>
        internal void FlushDisplayedColorsToRenderer()
        {
            if (MeshRenderer == null || PropertyBlock == null) return;
            PropertyBlock.Clear();
            if (IsAnimating)
            {
                PropertyBlock.SetColor(BrightColorId, CurrentBrightColor);
                PropertyBlock.SetColor(DarkColorId, CurrentDarkColor);
                PropertyBlock.SetVector(SpreadId, new Vector4(CurrentSpread.x, CurrentSpread.y, CurrentSpread.z, 0));
            }
            MeshRenderer.SetPropertyBlock(PropertyBlock);
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