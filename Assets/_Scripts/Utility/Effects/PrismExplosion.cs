using System;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Handles visual + positional explosion effect for prism destruction.
    /// Animation is driven by PrismEffectsManager via batched Burst jobs
    /// instead of per-instance async loops.
    /// Uses MaterialPropertyBlock to keep prefab-assigned materials intact.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PrismExplosion : MonoBehaviour
    {
        [SerializeField]
        private float minSpeed = 30f;

        [SerializeField]
        private float maxSpeed = 250f;

        [SerializeField]
        private MeshRenderer _renderer;

        private MaterialPropertyBlock _mpb;

        // Pool callback (set by PoolManager)
        public Action<PrismExplosion> OnReturnToPool;

        // Cache shader property IDs for performance
        private static readonly int VelocityID = Shader.PropertyToID("_Velocity");
        private static readonly int ExplosionAmountID = Shader.PropertyToID("_ExplosionAmount");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        // State exposed to PrismEffectsManager for batched updates
        internal Vector3 InitialPosition { get; private set; }
        internal Vector3 Velocity { get; private set; }
        internal float Speed { get; private set; }
        internal float Elapsed { get; set; }
        internal float MaxDuration => 5f;
        internal bool IsActive { get; private set; }
        internal MeshRenderer Renderer => _renderer;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!_renderer)
                _renderer = GetComponent<MeshRenderer>();
        }
#endif

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<MeshRenderer>();

            _mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                IsActive = false;
                PrismEffectsManager.Instance?.UnregisterExplosion(this);
            }

            if (_renderer != null && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Fire the explosion animation. Sets up state and registers with the
        /// centralized PrismEffectsManager for batched Burst-compiled updates.
        /// </summary>
        public void TriggerExplosion(Vector3 velocity)
        {
            if (_renderer == null || _mpb == null)
            {
                CSDebug.LogError("[PrismExplosion] Missing required components, cannot trigger explosion.");
                return;
            }

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            // If already active, unregister first
            if (IsActive)
                PrismEffectsManager.Instance?.UnregisterExplosion(this);

            // Clamp velocity and calculate speed
            velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, maxSpeed, out float speed);

            // Store state for manager to read
            InitialPosition = transform.position;
            Velocity = velocity;
            Speed = speed;
            Elapsed = 0f;
            IsActive = true;

            // Set ALL animated shader properties to their initial values so
            // we never fall back to the material's baked defaults
            // (ExplodingBlockMaterial has _ExplosionAmount: 20.7 which looks fully exploded)
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetVector(VelocityID, velocity);
            _mpb.SetFloat(ExplosionAmountID, 0f);
            _mpb.SetFloat(OpacityID, 1f);
            _renderer.SetPropertyBlock(_mpb);

            // Register with batched manager for frame updates (auto-creates if not in scene)
            PrismEffectsManager.EnsureInstance().RegisterExplosion(this);
        }

        /// <summary>
        /// Immediately return this instance to the pool.
        /// Also reparents under the PoolManager's transform for hierarchy cleanliness.
        /// </summary>
        public void ReturnToPool()
        {
            CompleteEffect();
            OnReturnToPool?.Invoke(this);
        }

        /// <summary>
        /// Called internally or by PrismEffectsManager to stop the animation and clear overrides.
        /// </summary>
        internal void CompleteEffect()
        {
            if (IsActive)
            {
                IsActive = false;
                PrismEffectsManager.Instance?.UnregisterExplosion(this);
            }

            if (_renderer != null && _mpb != null)
            {
                _mpb.Clear();
                _renderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Called by PrismEffectsManager when the animation finishes naturally (elapsed >= maxDuration).
        /// Cleans up and notifies pool.
        /// </summary>
        internal void OnEffectComplete()
        {
            CompleteEffect();
            OnReturnToPool?.Invoke(this);
        }
    }
}
