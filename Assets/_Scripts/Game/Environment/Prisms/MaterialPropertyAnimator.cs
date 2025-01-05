using UnityEngine;
using System;
using Unity.Mathematics;

namespace CosmicShore.Core
{
    public class MaterialPropertyAnimator : MonoBehaviour
    {
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
        private TrailBlock cachedTrailBlock;
        private bool materialsDirty;

        private void Awake()
        {
            // Cache components
            MeshRenderer = GetComponent<MeshRenderer>();
            cachedTrailBlock = GetComponent<TrailBlock>();
            
            if (MeshRenderer == null)
            {
                Debug.LogError($"MeshRenderer missing on {gameObject.name}");
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

            if (cachedTrailBlock == null || ThemeManager.Instance == null)
                return false;

            try
            {
                var team = cachedTrailBlock.Team;
                activeOpaqueMaterial = ThemeManager.Instance.GetTeamBlockMaterial(team);
                activeTransparentMaterial = ThemeManager.Instance.GetTeamTransparentBlockMaterial(team);
                
                if (activeOpaqueMaterial != null && MeshRenderer != null)
                {
                    MeshRenderer.material = activeOpaqueMaterial;
                }
                
                materialsDirty = false;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error validating materials: {e.Message}");
                return false;
            }
        }

        public void UpdateMaterial(Material transparentMaterial, Material opaqueMaterial, float duration = 0.8f, Action onComplete = null)
        {
            if (!enabled || MeshRenderer == null) return;

            if (transparentMaterial == null || opaqueMaterial == null)
            {
                Debug.LogError($"Invalid materials provided to {gameObject.name}");
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
                var currentMaterial = MeshRenderer.material;
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
                
                if (MeshRenderer != null && cachedTrailBlock != null && 
                    cachedTrailBlock.TrailBlockProperties != null)
                {
                    MeshRenderer.material = cachedTrailBlock.TrailBlockProperties.IsTransparent ?
                        transparentMaterial : opaqueMaterial;
                }

                //cachedTrailBlock.BlockCollider.size = Vector3.one + VectorDivision(TargetSpread, cachedTrailBlock.TargetScale);
                onComplete?.Invoke();
            };
        }

        public void SetTransparency(bool transparent)
        {
            if (!IsAnimating && MeshRenderer != null && ValidateMaterials())
            {
                MeshRenderer.material = transparent ? activeTransparentMaterial : activeOpaqueMaterial;
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

        Vector3 VectorDivision(Vector3 Vector1, Vector3 Vector2) // TODO: move to tools
        {
            return new Vector3(Vector1.x / Vector2.x, Vector1.y / Vector2.y, Vector1.z / Vector2.z);
        }
    }
}
