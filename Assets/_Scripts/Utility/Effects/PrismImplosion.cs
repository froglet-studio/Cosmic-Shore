using System;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Utility
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

        // Wall-clock start time, used by the watchdog. Tracking via Time.time (not the
        // manager-driven Elapsed counter) is robust against state-reset bugs that
        // would otherwise keep Elapsed pinned at 0 and starve the natural completion path.
        float _activatedAtTime;
        const float WatchdogDurationMultiplier = 2f;

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

        private void OnEnable()
        {
            // Backstop the watchdog timer for the case where the pool re-activates
            // a GameObject but the consumer never gets to call StartImplosion (e.g.,
            // an exception in PrismFactory between pool.Get and StartImplosion).
            // StartImplosion / StartGrow overwrite this with their own timestamps so
            // the legitimate path still uses the activation moment of the effect itself.
            _activatedAtTime = Time.time;
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

            // Re-entry on an already-active instance: this should not happen during
            // normal pool flow (Get always returns an inactive instance), but it does
            // happen if the prefab's OnMiniGameTurnEnd EventListener interleaves with
            // a fresh Implode call. Unregister cleanly so RegisterImplosion below
            // doesn't dedup-skip and the instance gets a fresh tick cadence.
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
            _activatedAtTime = Time.time;

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
            _activatedAtTime = Time.time;

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
        /// Cleans up, notifies pool, and force-deactivates the GameObject as a
        /// safety net so the implosion VFX can never visually loop even if the
        /// pool callback chain is broken (e.g., OnReturnToPool was nulled by an
        /// external owner like ShapeDrawingManager, or a duplicate Get/Release
        /// cycle left subscriptions in an inconsistent state).
        /// </summary>
        internal void OnEffectComplete()
        {
            CompleteEffect();

            // Snapshot + clear before invoke. Prevents a re-entrant Invoke (e.g., a
            // listener that triggers another Implode on the same instance) from
            // re-firing the same callbacks on a partially-completed implosion.
            var callback = OnReturnToPool;
            OnReturnToPool = null;
            callback?.Invoke(this);

            // Force-deactivate as a safety net. The pool callback above normally
            // does this via OnReleaseToPool → SetActive(false). If that path failed
            // for any reason, the GameObject would remain active and the shader
            // animation would visibly continue / loop on the next StartImplosion
            // call against the same instance. This is a no-op when the pool ran cleanly.
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void Update()
        {
            // Wall-clock watchdog: fires for ANY active GameObject that's been alive
            // longer than 2x the configured duration. We deliberately do NOT gate on
            // IsActive because the dominant failure mode is an instance whose IsActive
            // was cleared by OnDisable but whose GameObject was reactivated through
            // the pool without StartImplosion ever running again — those leak past
            // an IsActive-only check. Tracking via Time.time (set in OnEnable as a
            // backstop, refreshed in StartImplosion / StartGrow) is the only signal
            // that survives all the state-reset failure modes.
            if (Time.time - _activatedAtTime <= implosionDuration * WatchdogDurationMultiplier) return;

            CSDebug.LogWarning($"[PrismImplosion] Watchdog force-completed implosion on '{name}' " +
                               $"after {Time.time - _activatedAtTime:F2}s (duration={implosionDuration}, " +
                               $"IsActive={IsActive}). Likely cause: OnReturnToPool subscription was lost or duplicated.");
            OnEffectComplete();
        }
    }
}
