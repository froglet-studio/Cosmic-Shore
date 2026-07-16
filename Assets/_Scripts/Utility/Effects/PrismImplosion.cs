using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.ECS;
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

        // The live convergence target (the consuming fauna / vessel). The suction sink
        // FOLLOWS this transform as it keeps moving - a fauna swims a long way during the
        // ~2s implosion, so a position snapshotted at consumption time would suck the
        // prisms toward where the creature WAS, not where it is. Refreshed each frame by
        // PrismEffectsManager via RefreshConvergence(). Held as a reference (not copied to
        // a Vector3 and discarded) precisely so it can track. Fake-null safe: if the target
        // withers / despawns mid-suction (starvation & predation outlive this VFX), we fall
        // back to the last known position rather than throwing.
        private Transform _convergenceTransform;

        // State exposed to PrismEffectsManager for batched updates
        internal Vector3 TargetPosition { get; private set; }
        internal float Elapsed { get; set; }
        internal float Duration => implosionDuration;
        internal float GrowDelayRemaining { get; set; }
        internal float Progress { get; set; }
        internal bool IsActive { get; private set; }
        internal bool IsGrowing { get; private set; }
        internal Renderer Renderer => prismRenderer;

        // --- Instanced rendering (Entities Graphics companion entity) -----------
        // Companion entity carrying _State/_Location overrides draws in the
        // renderer's place - swarm-eat implosion storms batch instead of issuing
        // one draw + SetPass each.
        internal PrismRenderHandle RenderHandle;
        MeshFilter _meshFilter;
        Color _pendingBrightColor = Color.white;
        Color _pendingDarkColor = Color.black;
        bool _hasPendingTeamColors;

        internal bool UsesEntityRenderPath => PrismRenderService.IsHandleUsable(in RenderHandle);

        private static readonly int BrightColorID = Shader.PropertyToID("_BrightColor");
        private static readonly int DarkColorID = Shader.PropertyToID("_DarkColor");

        void EnsureRenderEntity()
        {
            if (PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            if (prismRenderer == null) return;
            if (_meshFilter == null || _meshFilter.sharedMesh == null || prismRenderer.sharedMaterial == null) return;
            RenderHandle = PrismRenderService.Create(
                _meshFilter.sharedMesh, prismRenderer.sharedMaterial,
                transform.localToWorldMatrix, gameObject.layer,
                PrismRenderOverrideSet.Implosion);
        }

        /// <summary>Team colors from PrismFactory.ConfigureForTeam - stored and
        /// applied at StartImplosion/StartGrow to whichever render path is active.</summary>
        public void SetTeamColors(Color bright, Color dark)
        {
            _pendingBrightColor = bright;
            _pendingDarkColor = dark;
            _hasPendingTeamColors = true;
        }

        /// <summary>Routes initial shader state + visibility to the active render
        /// path. Implosions are visible from frame zero (progress 0 = whole block).</summary>
        void ApplyInitialVisualState(float initialState, Vector3 location)
        {
            EnsureRenderEntity();
            if (UsesEntityRenderPath)
            {
                PrismRenderService.SetTransform(in RenderHandle, transform.localToWorldMatrix);
                PrismRenderService.SetImplosionParams(in RenderHandle, initialState,
                    new Unity.Mathematics.float3(location.x, location.y, location.z));
                if (_hasPendingTeamColors)
                {
                    PrismRenderService.SetTeamColors(in RenderHandle,
                        PrismRenderService.ToFloat4(_pendingBrightColor),
                        PrismRenderService.ToFloat4(_pendingDarkColor));
                }
                PrismRenderService.SetVisible(in RenderHandle, true);
                if (prismRenderer != null) prismRenderer.enabled = false;
            }
            else
            {
                if (prismRenderer != null && !prismRenderer.enabled) prismRenderer.enabled = true;
                prismRenderer.GetPropertyBlock(mpb);
                mpb.SetFloat(ImplosionProgressID, initialState);
                mpb.SetVector(ConvergencePointID, location);
                if (_hasPendingTeamColors)
                {
                    mpb.SetColor(BrightColorID, _pendingBrightColor);
                    mpb.SetColor(DarkColorID, _pendingDarkColor);
                }
                prismRenderer.SetPropertyBlock(mpb);
            }
        }

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
            _meshFilter = GetComponent<MeshFilter>();

            mpb = new MaterialPropertyBlock();
        }

        // Enabled-instance registry for PrismEffectsManager's zombie audit - replaces the
        // periodic FindObjectsByType full-scene scans (a recurring dev-build profiler spike).
        internal static readonly List<PrismImplosion> EnabledInstances = new();

        private void OnEnable()
        {
            EnabledInstances.Add(this);

            // Backstop the watchdog timer for the case where the pool re-activates
            // a GameObject but the consumer never gets to call StartImplosion (e.g.,
            // an exception in PrismFactory between pool.Get and StartImplosion).
            // StartImplosion / StartGrow overwrite this with their own timestamps so
            // the legitimate path still uses the activation moment of the effect itself.
            _activatedAtTime = Time.time;
        }

        private void OnDisable()
        {
            EnabledInstances.Remove(this);

            // Pool return / scene teardown may bypass CompleteEffect - never carry a target
            // reference (possibly a destroyed transform) across pool reuse.
            _convergenceTransform = null;

            if (IsActive)
            {
                IsActive = false;
                PrismEffectsManager.Instance?.UnregisterImplosion(this); // safe: may already be null during teardown
            }

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);

            // Parity with the MPB clear below: a pool reuse that skips
            // ConfigureForTeam must show material defaults, not the previous
            // team's palette.
            _hasPendingTeamColors = false;

            if (prismRenderer != null && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }
        }

        private void OnDestroy()
        {
            PrismRenderService.Destroy(ref RenderHandle);
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

            // Keep the live transform so the sink tracks the moving fauna each frame.
            _convergenceTransform = convergenceTransform;

            // Store state for manager to read
            TargetPosition = targetPos;
            Elapsed = 0f;
            Progress = 0f;
            IsGrowing = false;
            GrowDelayRemaining = 0f;
            IsActive = true;
            _activatedAtTime = Time.time;

            // Set initial shader state on the active render path
            ApplyInitialVisualState(0f, targetPos);

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

            // Grow is the reverse animation but uses the same moving-target convergence:
            // track the owner transform so a prism growing out of a moving creature stays
            // anchored to it instead of to its spawn-instant position.
            _convergenceTransform = ownerTransform;

            // Store state for manager to read
            TargetPosition = startPosition;
            Elapsed = 0f;
            Progress = 1f;
            IsGrowing = true;
            GrowDelayRemaining = growDelay;
            IsActive = true;
            _activatedAtTime = Time.time;

            // Set initial collapsed state on the active render path
            ApplyInitialVisualState(1f, startPosition);

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
        /// Re-read the convergence point from the live target transform. Called once per
        /// frame by PrismEffectsManager before it samples TargetPosition into the job, so
        /// the suction sink tracks the still-moving fauna. One Transform.position read per
        /// active implosion - the per-frame _Location write to the shader already happens
        /// unconditionally, so this is the only marginal cost of following the target.
        /// Fake-null safe: a target destroyed mid-suction leaves TargetPosition at its last
        /// known value.
        /// </summary>
        internal void RefreshConvergence()
        {
            if (_convergenceTransform)
                TargetPosition = _convergenceTransform.position;
        }

        /// <summary>
        /// Called internally or by PrismEffectsManager to stop the animation and clear overrides.
        /// </summary>
        internal void CompleteEffect()
        {
            // Drop the target reference so a pooled instance can't keep a destroyed
            // transform alive or track a stale target on its next reuse.
            _convergenceTransform = null;

            if (IsActive)
            {
                IsActive = false;
                PrismEffectsManager.Instance?.UnregisterImplosion(this); // safe: may already be null during teardown
            }

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);

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
            // the pool without StartImplosion ever running again - those leak past
            // an IsActive-only check. Tracking via Time.time (set in OnEnable as a
            // backstop, refreshed in StartImplosion / StartGrow) is the only signal
            // that survives all the state-reset failure modes.
            if (Time.time - _activatedAtTime <= implosionDuration * WatchdogDurationMultiplier) return;

            CSDebug.LogWarning($"[PrismImplosion] Watchdog force-completed '{name}' " +
                               $"at world {transform.position} after {Time.time - _activatedAtTime:F2}s " +
                               $"(duration={implosionDuration}, IsActive={IsActive}, target={TargetPosition}). " +
                               "Likely cause: OnReturnToPool subscription was lost or duplicated.");
            OnEffectComplete();
        }
    }
}
