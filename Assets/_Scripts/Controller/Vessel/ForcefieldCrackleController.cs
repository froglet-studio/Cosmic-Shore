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

        // Shader property IDs — cached once
        static readonly int ImpactPositionsId = Shader.PropertyToID("_ImpactPositions");
        static readonly int ImpactParamsId    = Shader.PropertyToID("_ImpactParams");
        static readonly int ImpactCountId     = Shader.PropertyToID("_ImpactCount");

        static readonly ProfilerMarker s_UpdateMarker =
            new("ForcefieldCrackleController.Update");

        // Ring buffer of impact slots
        readonly Vector4[] _positions = new Vector4[MaxImpacts]; // xyz = local pos on unit sphere, w = elapsed time
        readonly Vector4[] _params    = new Vector4[MaxImpacts]; // x = intensity, y = radius, z = maxLifetime, w = unused
        int _activeCount;
        int _nextSlot;

        MaterialPropertyBlock _propBlock;

        void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            // Zero out all slots
            for (int i = 0; i < MaxImpacts; i++)
            {
                _positions[i] = Vector4.zero;
                _params[i]    = Vector4.zero;
            }
        }

        void Update()
        {
            if (_activeCount == 0) return;

            using (s_UpdateMarker.Auto())
            {
                float dt = Time.deltaTime;
                int alive = 0;

                for (int i = 0; i < MaxImpacts; i++)
                {
                    if (_params[i].z <= 0f) continue; // slot empty

                    // Advance elapsed time (stored in w of positions)
                    _positions[i].w += dt;

                    if (_positions[i].w >= _params[i].z)
                    {
                        // Expired — clear slot
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
        /// Register a new impact on the forcefield surface.
        /// </summary>
        /// <param name="worldPoint">World-space point on the sphere surface.</param>
        /// <param name="duration">How long the crackle persists (seconds).</param>
        /// <param name="intensity">Brightness multiplier.</param>
        /// <param name="radius">Normalized angular radius of the crackle spread (0–1, where 1 ≈ hemisphere).</param>
        public void AddImpact(Vector3 worldPoint, float duration, float intensity, float radius)
        {
            // Convert to local space relative to this transform (the overlay sphere's parent)
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

            // Normalize to unit sphere direction (shader works in object space on a unit sphere)
            Vector3 dir = localPoint.normalized;

            int slot = _nextSlot;
            _positions[slot] = new Vector4(dir.x, dir.y, dir.z, 0f); // w = elapsed time, starts at 0
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
            _propBlock.SetVectorArray(ImpactPositionsId, _positions);
            _propBlock.SetVectorArray(ImpactParamsId, _params);
            _propBlock.SetInt(ImpactCountId, _activeCount);
            overlayRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
