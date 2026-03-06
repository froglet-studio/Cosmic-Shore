using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Manages a forcefield crackle overlay on the Skimmer's sphere collider.
    /// Receives impact points from <see cref="SkimmerForcefieldCracklePrismEffectSO"/>
    /// and feeds them to the crackle shader via MaterialPropertyBlock each frame.
    /// </summary>
    public class ForcefieldCrackleController : MonoBehaviour
    {
        const int MaxImpacts = 16;

        [Header("Refs")]
        [SerializeField, Tooltip("MeshRenderer on the child sphere overlay mesh.")]
        private MeshRenderer overlayRenderer;

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

        // Cached visual params (pushed from SO, forwarded to shader)
        Color _crackleColorA       = new Color(0.7f, 0.85f, 1f, 1f);
        Color _crackleColorB       = new Color(0.3f, 0.6f, 1f, 1f);
        Color _fresnelRimColor     = new Color(0.3f, 0.5f, 0.8f, 1f);
        float _arcDensity          = 8f;
        float _arcSharpness        = 0.06f;
        float _ringThickness       = 0.4f;
        float _centerFillAmount    = 0.15f;
        float _rippleSpeed         = 1f;
        float _fresnelRimIntensity = 0.08f;
        float _fresnelRimPower     = 3f;
        bool _visualParamsDirty    = true;

        MaterialPropertyBlock _propBlock;

        void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < MaxImpacts; i++)
            {
                _positions[i] = Vector4.zero;
                _params[i]    = Vector4.zero;
            }
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
        /// Values are cached and marked dirty so the next PushToShader sends them.
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
            _crackleColorA       = crackleColorA;
            _crackleColorB       = crackleColorB;
            _fresnelRimColor     = fresnelRimColor;
            _arcDensity          = arcDensity;
            _arcSharpness        = arcSharpness;
            _ringThickness       = ringThickness;
            _centerFillAmount    = centerFillAmount;
            _rippleSpeed         = rippleSpeed;
            _fresnelRimIntensity = fresnelRimIntensity;
            _fresnelRimPower     = fresnelRimPower;
            _visualParamsDirty   = true;
        }

        /// <summary>
        /// Register a new impact on the forcefield surface.
        /// </summary>
        public void AddImpact(Vector3 worldPoint, float duration, float intensity, float radius)
        {
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

            overlayRenderer.GetPropertyBlock(_propBlock);

            // Impact data (every frame)
            _propBlock.SetVectorArray(ImpactPositionsId, _positions);
            _propBlock.SetVectorArray(ImpactParamsId, _params);
            _propBlock.SetInt(ImpactCountId, _activeCount);

            // Visual params (only when changed)
            if (_visualParamsDirty)
            {
                _propBlock.SetColor(CrackleColorAId, _crackleColorA);
                _propBlock.SetColor(CrackleColorBId, _crackleColorB);
                _propBlock.SetColor(FresnelRimColorId, _fresnelRimColor);
                _propBlock.SetFloat(ArcDensityId, _arcDensity);
                _propBlock.SetFloat(ArcSharpnessId, _arcSharpness);
                _propBlock.SetFloat(RingThicknessId, _ringThickness);
                _propBlock.SetFloat(CenterFillAmountId, _centerFillAmount);
                _propBlock.SetFloat(RippleSpeedId, _rippleSpeed);
                _propBlock.SetFloat(FresnelRimIntensityId, _fresnelRimIntensity);
                _propBlock.SetFloat(FresnelRimPowerId, _fresnelRimPower);
                _visualParamsDirty = false;
            }

            overlayRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
