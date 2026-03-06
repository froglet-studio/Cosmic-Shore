using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Manages a forcefield crackle overlay on the Skimmer's sphere collider.
    /// Receives impact points from <see cref="SkimmerForcefieldCracklePrismEffectSO"/>
    /// and feeds them to the crackle shader via MaterialPropertyBlock each frame.
    ///
    /// In the Editor, visual params are exposed as serialized fields so you can
    /// tweak the look in Scene view without entering Play mode. At runtime the
    /// SO calls <see cref="SetVisualParams"/> which overrides the serialized values.
    /// </summary>
    [ExecuteAlways]
    public class ForcefieldCrackleController : MonoBehaviour
    {
        const int MaxImpacts = 16;

        [Header("Refs")]
        [SerializeField, Tooltip("MeshRenderer on the child sphere overlay mesh.")]
        private MeshRenderer overlayRenderer;

        [Header("Colors")]
        [SerializeField, Tooltip("Core arc color (hot center of each lightning bolt).")]
        private Color crackleColorA = new Color(0.7f, 0.85f, 1f, 1f);

        [SerializeField, Tooltip("Outer glow color (halo around arcs).")]
        private Color crackleColorB = new Color(0.3f, 0.6f, 1f, 1f);

        [SerializeField, Tooltip("Ambient rim glow color.")]
        private Color fresnelRimColor = new Color(0.3f, 0.5f, 0.8f, 1f);

        [Header("Arc Pattern")]
        [SerializeField, Range(4f, 20f), Tooltip("Number of arc branches radiating from each impact.")]
        private float arcDensity = 8f;

        [SerializeField, Range(0.01f, 0.5f), Tooltip("Arc width — lower = thinner, sharper arcs.")]
        private float arcSharpness = 0.06f;

        [Header("Ring / Wave")]
        [SerializeField, Range(0.05f, 1f), Tooltip("Expanding ring wavefront thickness.")]
        private float ringThickness = 0.4f;

        [SerializeField, Range(0f, 1f), Tooltip("Center glow fill amount. 0 = ring only, 1 = solid center.")]
        private float centerFillAmount = 0.15f;

        [SerializeField, Range(0.2f, 3f), Tooltip("Ripple expansion speed multiplier.")]
        private float rippleSpeed = 1f;

        [Header("Fresnel Rim")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Rim glow intensity.")]
        private float fresnelRimIntensity = 0.08f;

        [SerializeField, Range(1f, 8f), Tooltip("Fresnel exponent — higher = thinner rim.")]
        private float fresnelRimPower = 3f;

        // Shader property IDs — impact data
        static readonly int ImpactPositionsId     = Shader.PropertyToID("_ImpactPositions");
        static readonly int ImpactParamsId        = Shader.PropertyToID("_ImpactParams");
        static readonly int ImpactCountId         = Shader.PropertyToID("_ImpactCount");

        // Shader property IDs — visual params
        static readonly int CrackleColorAId       = Shader.PropertyToID("_CrackleColorA");
        static readonly int CrackleColorBId       = Shader.PropertyToID("_CrackleColorB");
        static readonly int FresnelRimColorId     = Shader.PropertyToID("_FresnelRimColor");
        static readonly int ArcDensityId          = Shader.PropertyToID("_ArcDensity");
        static readonly int ArcSharpnessId        = Shader.PropertyToID("_ArcSharpness");
        static readonly int RingThicknessId       = Shader.PropertyToID("_RingThickness");
        static readonly int CenterFillAmountId    = Shader.PropertyToID("_CenterFillAmount");
        static readonly int RippleSpeedId         = Shader.PropertyToID("_RippleSpeed");
        static readonly int FresnelRimIntensityId = Shader.PropertyToID("_FresnelRimIntensity");
        static readonly int FresnelRimPowerId     = Shader.PropertyToID("_FresnelRimPower");

        static readonly ProfilerMarker s_UpdateMarker =
            new("ForcefieldCrackleController.Update");

        // Ring buffer of impact slots
        readonly Vector4[] _positions = new Vector4[MaxImpacts];
        readonly Vector4[] _params    = new Vector4[MaxImpacts];
        int _activeCount;
        int _nextSlot;

        bool _visualParamsDirty = true;

        MaterialPropertyBlock _propBlock;

        void OnEnable()
        {
            _propBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < MaxImpacts; i++)
            {
                _positions[i] = Vector4.zero;
                _params[i]    = Vector4.zero;
            }

            _visualParamsDirty = true;
            PushToShader();
        }

        void OnValidate()
        {
            _visualParamsDirty = true;

            // Push immediately if we have a prop block (edit mode live preview)
            if (_propBlock != null)
                PushToShader();
        }

        void Update()
        {
            if (_activeCount == 0 && !_visualParamsDirty) return;

            using (s_UpdateMarker.Auto())
            {
                float dt = Time.deltaTime;
                int alive = 0;

                for (int i = 0; i < MaxImpacts; i++)
                {
                    if (_params[i].z <= 0f) continue;

                    _positions[i].w += dt;

                    if (_positions[i].w >= _params[i].z)
                    {
                        _positions[i] = Vector4.zero;
                        _params[i]    = Vector4.zero;
                    }
                    else
                    {
                        alive++;
                    }
                }

                _activeCount = alive;
                PushToShader();
            }
        }

        /// <summary>
        /// Set all visual parameters from the effect SO.
        /// Overrides the serialized Inspector values at runtime.
        /// </summary>
        public void SetVisualParams(
            Color crackleColorA,
            Color crackleColorB,
            Color fresnelRimColor,
            float arcDensity,
            float arcSharpness,
            float ringThickness,
            float centerFillAmount,
            float rippleSpeed,
            float fresnelRimIntensity,
            float fresnelRimPower)
        {
            this.crackleColorA       = crackleColorA;
            this.crackleColorB       = crackleColorB;
            this.fresnelRimColor     = fresnelRimColor;
            this.arcDensity          = arcDensity;
            this.arcSharpness        = arcSharpness;
            this.ringThickness       = ringThickness;
            this.centerFillAmount    = centerFillAmount;
            this.rippleSpeed         = rippleSpeed;
            this.fresnelRimIntensity = fresnelRimIntensity;
            this.fresnelRimPower     = fresnelRimPower;
            _visualParamsDirty       = true;
        }

        /// <summary>
        /// Register a new impact on the forcefield surface.
        /// </summary>
        public void AddImpact(Vector3 worldPoint, float duration, float intensity, float radius)
        {
            _propBlock ??= new MaterialPropertyBlock();

            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            Vector3 dir = localPoint.normalized;

            int slot = _nextSlot;
            _positions[slot] = new Vector4(dir.x, dir.y, dir.z, 0f);
            _params[slot]    = new Vector4(intensity, radius, duration, 0f);

            _nextSlot = (_nextSlot + 1) % MaxImpacts;
            _activeCount = Mathf.Min(_activeCount + 1, MaxImpacts);

            PushToShader();
        }

        public void ClearAllImpacts()
        {
            for (int i = 0; i < MaxImpacts; i++)
            {
                _positions[i] = Vector4.zero;
                _params[i]    = Vector4.zero;
            }
            _activeCount = 0;
            _nextSlot = 0;
            PushToShader();
        }

        void PushToShader()
        {
            if (!overlayRenderer) return;

            _propBlock ??= new MaterialPropertyBlock();
            overlayRenderer.GetPropertyBlock(_propBlock);

            // Impact data
            _propBlock.SetVectorArray(ImpactPositionsId, _positions);
            _propBlock.SetVectorArray(ImpactParamsId, _params);
            _propBlock.SetInt(ImpactCountId, _activeCount);

            // Visual params (always push when dirty)
            if (_visualParamsDirty)
            {
                _propBlock.SetColor(CrackleColorAId, crackleColorA);
                _propBlock.SetColor(CrackleColorBId, crackleColorB);
                _propBlock.SetColor(FresnelRimColorId, fresnelRimColor);
                _propBlock.SetFloat(ArcDensityId, arcDensity);
                _propBlock.SetFloat(ArcSharpnessId, arcSharpness);
                _propBlock.SetFloat(RingThicknessId, ringThickness);
                _propBlock.SetFloat(CenterFillAmountId, centerFillAmount);
                _propBlock.SetFloat(RippleSpeedId, rippleSpeed);
                _propBlock.SetFloat(FresnelRimIntensityId, fresnelRimIntensity);
                _propBlock.SetFloat(FresnelRimPowerId, fresnelRimPower);
                _visualParamsDirty = false;
            }

            overlayRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
