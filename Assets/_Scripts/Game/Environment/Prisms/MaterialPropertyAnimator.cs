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
        public bool IsAnimating { get; set; }

        private Material activeTransparentMaterial;
        private Material activeOpaqueMaterial;

        private void Awake()
        {
            PropertyBlock = new MaterialPropertyBlock();
            MeshRenderer = GetComponent<MeshRenderer>();
            MaterialStateManager.Instance.RegisterAnimator(this);
        }

        public void UpdateMaterial(Material transparentMaterial, Material opaqueMaterial, float duration = 0.8f, Action onComplete = null)
        {
            if (activeTransparentMaterial == null || activeOpaqueMaterial == null)
            {
                var trailBlock = GetComponent<TrailBlock>();
                activeOpaqueMaterial = MeshRenderer.material = ThemeManager.Instance.GetTeamBlockMaterial(trailBlock.Team);
                activeTransparentMaterial = ThemeManager.Instance.GetTeamTransparentBlockMaterial(trailBlock.Team);
            }

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
                StartBrightColor = MeshRenderer.material.GetColor(BrightColorId);
                StartDarkColor = MeshRenderer.material.GetColor(DarkColorId);
                StartSpread = MeshRenderer.material.GetVector(SpreadId);
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
                var trailBlock = GetComponent<TrailBlock>();
                MeshRenderer.material = trailBlock.TrailBlockProperties.IsTransparent ?
                    transparentMaterial : opaqueMaterial;
                onComplete?.Invoke();
            };
        }

        public void SetTransparency(bool transparent)
        {
            if (!IsAnimating)
            {
                MeshRenderer.material = transparent ? activeTransparentMaterial : activeOpaqueMaterial;
            }
        }

        private void OnDestroy()
        {
            MaterialStateManager.Instance.UnregisterAnimator(this);
        }
    }
}