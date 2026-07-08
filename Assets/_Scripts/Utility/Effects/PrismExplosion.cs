using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.ECS;
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
        private static readonly int BrightColorID = Shader.PropertyToID("_BrightColor");
        private static readonly int DarkColorID = Shader.PropertyToID("_DarkColor");

        // State exposed to PrismEffectsManager for batched updates
        internal Vector3 InitialPosition { get; private set; }
        internal Vector3 Velocity { get; private set; }
        internal float Speed { get; private set; }
        internal float Elapsed { get; set; }
        internal float MaxDuration => 5f;
        internal bool IsActive { get; private set; }
        internal MeshRenderer Renderer => _renderer;

        // --- Instanced rendering (Entities Graphics companion entity) -----------
        // Mirrors the prism path: the MeshRenderer stays disabled and a companion
        // entity carrying _Velocity/_ExplosionAmount/_Opacity overrides draws in
        // its place, so 64 simultaneous explosions cost one instanced batch.
        internal PrismRenderHandle RenderHandle;
        MeshFilter _meshFilter;
        Color _pendingBrightColor = Color.white;
        Color _pendingDarkColor = Color.black;
        bool _hasPendingTeamColors;

        internal bool UsesEntityRenderPath => PrismRenderService.IsHandleUsable(in RenderHandle);

        void EnsureRenderEntity()
        {
            if (PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            if (_renderer == null) return;
            if (_meshFilter == null || _meshFilter.sharedMesh == null || _renderer.sharedMaterial == null) return;
            RenderHandle = PrismRenderService.Create(
                _meshFilter.sharedMesh, _renderer.sharedMaterial,
                transform.localToWorldMatrix, gameObject.layer,
                PrismRenderOverrideSet.Explosion);
        }

        /// <summary>Team colors from PrismFactory.ConfigureForTeam — stored and
        /// applied at TriggerExplosion to whichever render path is active.</summary>
        public void SetTeamColors(Color bright, Color dark)
        {
            _pendingBrightColor = bright;
            _pendingDarkColor = dark;
            _hasPendingTeamColors = true;
        }

        /// <summary>Pushes the (pool-positioned, factory-scaled) transform to the entity.</summary>
        internal void SyncRenderTransform()
        {
            if (!PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            PrismRenderService.SetTransform(in RenderHandle, transform.localToWorldMatrix);
        }

        /// <summary>First-animated-frame show, called by PrismEffectsManager (the
        /// visual stays hidden until real values are applied to avoid a one-frame
        /// flash of the unanimated mesh — same contract as the legacy path).</summary>
        internal void EnableVisual()
        {
            if (UsesEntityRenderPath)
                PrismRenderService.SetVisible(in RenderHandle, true);
            else if (_renderer != null && !_renderer.enabled)
                _renderer.enabled = true;
        }

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
            _meshFilter = GetComponent<MeshFilter>();

            _mpb = new MaterialPropertyBlock();

            // Start with renderer disabled — only PrismEffectsManager should enable it
            // during active animation. This prevents pool-retrieved objects from flashing.
            if (_renderer != null)
                _renderer.enabled = false;
        }

        // Enabled-instance registry for PrismEffectsManager's zombie audit — replaces the
        // periodic FindObjectsByType full-scene scans (a recurring dev-build profiler spike).
        internal static readonly List<PrismExplosion> EnabledInstances = new();

        private void OnEnable() => EnabledInstances.Add(this);

        private void OnDisable()
        {
            EnabledInstances.Remove(this);

            if (IsActive)
            {
                IsActive = false;
                PrismEffectsManager.Instance?.UnregisterExplosion(this);
            }

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);

            // Parity with the MPB clear below: a pool reuse that skips
            // ConfigureForTeam must show material defaults, not the previous
            // team's palette.
            _hasPendingTeamColors = false;

            if (_renderer != null)
            {
                // Keep renderer disabled so pool-reactivated objects are invisible
                // until PrismEffectsManager explicitly enables during animation.
                _renderer.enabled = false;
                if (_mpb != null)
                {
                    _mpb.Clear();
                    _renderer.SetPropertyBlock(_mpb);
                }
            }
        }

        private void OnDestroy()
        {
            PrismRenderService.Destroy(ref RenderHandle);
        }

        /// <summary>
        /// Fire the explosion animation. Sets up state and registers with the
        /// centralized PrismEffectsManager for batched Burst-compiled updates.
        /// </summary>
        public void TriggerExplosion(Vector3 velocity)
        {
            if (_renderer == null || _mpb == null)
                return;

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

            EnsureRenderEntity();
            if (UsesEntityRenderPath)
            {
                // Entity path: initial shader params + team colors on the
                // companion entity; stays hidden until the manager's first frame.
                SyncRenderTransform();
                PrismRenderService.SetExplosionParams(in RenderHandle,
                    new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z), 0f, 1f);
                if (_hasPendingTeamColors)
                {
                    PrismRenderService.SetTeamColors(in RenderHandle,
                        PrismRenderService.ToFloat4(_pendingBrightColor),
                        PrismRenderService.ToFloat4(_pendingDarkColor));
                }
                PrismRenderService.SetVisible(in RenderHandle, false);
                _renderer.enabled = false;
            }
            else
            {
                // Legacy path. Set ALL animated shader properties to their initial
                // values so we never fall back to the material's baked defaults
                // (ExplodingBlockMaterial has _ExplosionAmount: 20.7 which looks fully exploded)
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetVector(VelocityID, velocity);
                _mpb.SetFloat(ExplosionAmountID, 0f);
                _mpb.SetFloat(OpacityID, 1f);
                if (_hasPendingTeamColors)
                {
                    _mpb.SetColor(BrightColorID, _pendingBrightColor);
                    _mpb.SetColor(DarkColorID, _pendingDarkColor);
                }
                _renderer.SetPropertyBlock(_mpb);

                // Keep renderer disabled until PrismEffectsManager applies the first animated
                // frame. The manager will set renderer.enabled = true once real values are applied.
                _renderer.enabled = false;
            }

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

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);

            if (_renderer != null)
            {
                // Disable renderer — only PrismEffectsManager should enable it during
                // active animation. Leaving it enabled here caused undistorted sphere
                // flashes when OnReturnToPool was null or pool deactivation was delayed.
                _renderer.enabled = false;
                if (_mpb != null)
                {
                    _mpb.Clear();
                    _renderer.SetPropertyBlock(_mpb);
                }
            }
        }

        /// <summary>
        /// Called by PrismEffectsManager when the animation finishes naturally (elapsed >= maxDuration).
        /// Cleans up and notifies pool.
        /// </summary>
        internal void OnEffectComplete()
        {
            CompleteEffect();

            if (OnReturnToPool == null)
            {
                // Fallback: deactivate so the object doesn't linger visibly in the scene.
                gameObject.SetActive(false);
                return;
            }

            OnReturnToPool.Invoke(this);
        }
    }
}
