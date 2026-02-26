using System;
using UnityEngine;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Utility.ClassExtensions; // if you still need it elsewhere
using CosmicShore.Utility.Recording;

namespace CosmicShore.Utility.Effects
{
    /// <summary>
    /// Handles prism implosion/grow VFX. Managed by PrismImplosionPoolManager.
    /// Animation is driven by PrismEffectsManager via batched Burst jobs
    /// instead of per-instance async loops.
    /// Uses MaterialPropertyBlock so prefab materials remain untouched.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class PrismImplosion : MonoBehaviour
    {
        [SerializeField] private Renderer prismRenderer;
        [SerializeField] private float implosionDuration = 2f;
        [SerializeField] private float growDelay = 0.25f;

        private MaterialPropertyBlock mpb;

        /// <summary> Callback for pooling system when effect finishes. </summary>
        public Action<PrismImplosion> OnReturnToPool;

        // Shader property IDs
        private static readonly int ImplosionProgressID = Shader.PropertyToID("_State");
        private static readonly int ConvergencePointID = Shader.PropertyToID("_Location");

        // State exposed to PrismEffectsManager for batched updates
        internal Vector3 TargetPosition { get; private set; }
        internal float Elapsed { get; set; }
        internal float Duration => implosionDuration;
        internal float GrowDelayRemaining { get; set; }
        internal float Progress { get; set; }
        internal bool IsActive { get; private set; }
        internal bool IsGrowing { get; private set; }
        internal Renderer Renderer => prismRenderer;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!prismRenderer)
                prismRenderer = GetComponent<Renderer>();
        }
#endif

        private void Awake()
        {
            if (!prismRenderer)
                prismRenderer = GetComponent<Renderer>();

            mpb = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                IsActive = false;
                PrismEffectsManager.Instance?.UnregisterImplosion(this); // safe: may already be null during teardown
            }

            if (prismRenderer != null && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }
        }

        // ---------------- API ----------------

        /// <summary> Start implosion (shader: 0 -> 1). </summary>
        public void StartImplosion(Transform convergenceTransform)
        {
            if (!prismRenderer || mpb == null)
            {
                CSDebug.LogError("[PrismImplosion] Missing required components, cannot start implosion.");
                return;
            }

            if (IsActive)
                PrismEffectsManager.Instance?.UnregisterImplosion(this); // safe: may already be null during teardown

            var targetPos = convergenceTransform.position;

            // Store state for manager to read
            TargetPosition = targetPos;
            Elapsed = 0f;
            Progress = 0f;
            IsGrowing = false;
            GrowDelayRemaining = 0f;
            IsActive = true;

            // Set initial shader state
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, 0f);
            mpb.SetVector(ConvergencePointID, targetPos);
            prismRenderer.SetPropertyBlock(mpb);

            // Register with batched manager for frame updates (auto-creates if not in scene)
            PrismEffectsManager.EnsureInstance().RegisterImplosion(this);
        }

        /// <summary> Start grow (shader: 1 -> 0). </summary>
        public void StartGrow(Transform ownerTransform)
        {
            if (!prismRenderer || mpb == null)
            {
                CSDebug.LogError("[PrismImplosion] Missing required components, cannot start grow.");
                return;
            }

            if (IsActive)
                PrismEffectsManager.Instance?.UnregisterImplosion(this); // safe: may already be null during teardown

            var startPosition = ownerTransform.position;

            // Store state for manager to read
            TargetPosition = startPosition;
            Elapsed = 0f;
            Progress = 1f;
            IsGrowing = true;
            GrowDelayRemaining = growDelay;
            IsActive = true;

            // Set initial collapsed state
            prismRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ImplosionProgressID, 1f);
            mpb.SetVector(ConvergencePointID, startPosition);
            prismRenderer.SetPropertyBlock(mpb);

            // Register with batched manager for frame updates (auto-creates if not in scene)
            PrismEffectsManager.EnsureInstance().RegisterImplosion(this);
        }

        /// <summary>
        /// Immediately stop any animation, clear overrides, and return to pool.
        /// </summary>
        public void ReturnToPool()
        {
            CompleteEffect();
            OnReturnToPool?.Invoke(this);
        }

        public float GetImplosionProgress() => Progress;

        /// <summary>Externally stop (cancels animation, but does not auto-return).</summary>
        public void StopEffect() => CompleteEffect();

        // ---------------- Internals ----------------

        /// <summary>
        /// Called internally or by PrismEffectsManager to stop the animation and clear overrides.
        /// </summary>
        internal void CompleteEffect()
        {
            if (IsActive)
            {
                IsActive = false;
                PrismEffectsManager.Instance?.UnregisterImplosion(this); // safe: may already be null during teardown
            }

            if (prismRenderer && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Called by PrismEffectsManager when the animation finishes naturally.
        /// Cleans up and notifies pool.
        /// </summary>
        internal void OnEffectComplete()
        {
            CompleteEffect();
            OnReturnToPool?.Invoke(this);
        }
    }
}
