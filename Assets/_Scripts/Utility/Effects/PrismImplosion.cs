using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.ECS;
using CosmicShore.Gameplay;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Handles prism implosion/grow VFX. Managed by PrismImplosionPoolManager.
    /// Clock-material driven (Docs/PRISM_ANIMATION.md B3): ONE stamp, the GPU
    /// runs the suction/grow, ONE scheduled completion. PrismEffectsManager only
    /// refreshes the moving convergence target (_Location) while the target lives.
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
        // PrismEffectsManager via RefreshConvergenceForClock(). Held as a reference (not copied to
        // a Vector3 and discarded) precisely so it can track. Fake-null safe: if the target
        // withers / despawns mid-suction (starvation & predation outlive this VFX), we fall
        // back to the last known position rather than throwing.
        private Transform _convergenceTransform;

        internal Vector3 TargetPosition { get; private set; }
        internal float Duration => EffectiveDuration;

        // Per-effect duration override (StartGrow's optional parameter — the Sparrow
        // turret rides the grow as a flight visual, so it must last one bullet
        // lifetime). 0 = the authored implosionDuration. Cleared on disable so pool
        // reuse never inherits it.
        float _durationOverride;
        float EffectiveDuration => _durationOverride > 0f ? _durationOverride : implosionDuration;

        /// <summary>Authored suction length — PrismDebris reads it off the pool prefab
        /// so the batched entity path animates for exactly as long as the pooled one
        /// (same reasoning as PrismExplosion.MinDebrisSpeed/MaxDebrisSpeed). growDelay
        /// is deliberately not exposed: it belongs to StartGrow, whose producer
        /// (PrismType.Grow) does not exist, so no batched effect ever delays.</summary>
        public float ImplosionDuration => implosionDuration;

        internal bool IsActive { get; private set; }
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

        // Wall-clock start time, used by the watchdog. Tracking via Time.time is
        // robust against state-reset bugs that would otherwise starve the natural
        // completion path.
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
        // O(1) swap-remove keyed by a stored index — same fix and profile evidence as
        // PrismExplosion.EnabledInstances (List.Remove's O(n) scan was 1.9s of one frame
        // under a mass-death burst); order is not part of the registry's contract.
        internal static readonly List<PrismImplosion> EnabledInstances = new();
        int _enabledIndex = -1;

        private void OnEnable()
        {
            _enabledIndex = EnabledInstances.Count;
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
            if (_enabledIndex >= 0)
            {
                int last = EnabledInstances.Count - 1;
                var moved = EnabledInstances[last];
                EnabledInstances[_enabledIndex] = moved;
                moved._enabledIndex = _enabledIndex;
                EnabledInstances.RemoveAt(last);
                _enabledIndex = -1;
            }

            // Pool return / scene teardown may bypass CompleteEffect - never carry a target
            // reference (possibly a destroyed transform) across pool reuse.
            _convergenceTransform = null;
            _durationOverride = 0f;

            IsActive = false;

            // A pooled-out clock implosion must not fire a stale completion later.
            PrismTimerManager.Instance?.CancelScheduledActions(this);
            PrismEffectsManager.Instance?.UnregisterClockConvergence(this);
            PrismRenderService.ClearSuctionClockStamp(in RenderHandle);

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

            var targetPos = convergenceTransform.position;

            // Keep the live transform so the sink tracks the moving fauna each frame.
            _convergenceTransform = convergenceTransform;

            TargetPosition = targetPos;
            IsActive = true;
            _activatedAtTime = Time.time;

            // Set initial shader state on the active render path
            ApplyInitialVisualState(0f, targetPos);

            // Clock-material path (Docs/PRISM_ANIMATION.md §4.4, B3) — STRICT MODE,
            // the ONLY path: progress rides the GPU clock (ONE stamp + ONE scheduled
            // completion, no per-frame _State feed, PrismEffectsManager's animation
            // pass never engaged). The MOVING convergence target is the documented
            // §1 exception: live gameplay data — the manager refreshes _Location
            // only, one float3 per frame, while the target transform lives.
            StampClockStrict(direction: 1f, delay: 0f, location: targetPos);
        }

        /// <summary>
        /// Start grow (shader: 1 -> 0) — the reverse suction: faces stream OUT of the
        /// (moving) owner point into the prism's rest shape.
        /// </summary>
        /// <param name="durationOverride">Seconds for the build-up; &lt;= 0 keeps the
        /// authored implosionDuration. The Sparrow turret passes its bullet lifetime so
        /// the last face lands exactly as the shot arrives.</param>
        public void StartGrow(Transform ownerTransform, float durationOverride = 0f)
        {
            _durationOverride = durationOverride > 0f ? durationOverride : 0f;

            if (!prismRenderer || mpb == null)
            {
                CSDebug.LogError("[PrismImplosion] Missing required components, cannot start grow.");
                return;
            }

            var startPosition = ownerTransform.position;

            // Grow is the reverse animation but uses the same moving-target convergence:
            // track the owner transform so a prism growing out of a moving creature stays
            // anchored to it instead of to its spawn-instant position.
            _convergenceTransform = ownerTransform;

            TargetPosition = startPosition;
            IsActive = true;
            _activatedAtTime = Time.time;

            // Set initial collapsed state on the active render path
            ApplyInitialVisualState(1f, startPosition);

            // Clock-material path — reverse suction (1 → 0) with the grow delay
            // baked into the stamp; same moving-target exception as StartImplosion.
            // A caller that supplies its own duration wants exact timing (the turret's
            // flight visual), so the authored pre-delay is dropped with it.
            StampClockStrict(direction: -1f,
                delay: _durationOverride > 0f ? 0f : growDelay,
                location: startPosition);
        }

        static readonly int SuctionStartTimeId = Shader.PropertyToID("_SuctionStartTime");

        /// <summary>
        /// The ONLY animation driver for StartImplosion/StartGrow (STRICT MODE —
        /// no legacy per-frame fallback). A missing entity or unwired graph fails
        /// LOUD via PrismClockDiagnostics; the completion is scheduled on every
        /// outcome so the pool flow never wedges.
        /// </summary>
        void StampClockStrict(float direction, float delay, Vector3 location)
        {
            if (UsesEntityRenderPath)
            {
                var mat = prismRenderer != null ? prismRenderer.sharedMaterial : null;
                if (mat == null || !mat.HasProperty(SuctionStartTimeId))
                    PrismClockDiagnostics.WarnUnwiredMaterial(mat, "_SuctionStartTime", this);

                if (PrismRenderService.StampSuctionClock(in RenderHandle,
                        PrismClock.Now, EffectiveDuration, direction, delay,
                        new Unity.Mathematics.float3(location.x, location.y, location.z)))
                {
                    // Culling envelope: vertices lerp toward _Location, so bounds
                    // must cover mesh ∪ convergence point (same class of bug as the
                    // explosion flight — geometry outside stale bounds frustum-culls
                    // wrong in both directions). Reset first: pooled reuse must not
                    // compound envelopes run over run.
                    if (_meshFilter != null)
                        PrismRenderService.ResetBoundsToMesh(in RenderHandle, _meshFilter.sharedMesh);
                    PrismRenderService.EncapsulateBoundsPoint(in RenderHandle,
                        ToObjectPoint(location), ConvergenceBoundsPadding);

                    // Moving-target exception: the manager refreshes ONLY _Location
                    // while the target transform lives (RefreshConvergenceForClock).
                    PrismEffectsManager.EnsureInstance()?.RegisterClockConvergence(this);
                }
            }
            else
            {
                PrismClockDiagnostics.WarnNoRenderEntity($"implosion:{name}", this,
                    $"entity never created for this pooled effect [service: {PrismRenderService.StatusLine()}]");
            }

            var timers = PrismTimerManager.EnsureInstance();
            timers.CancelScheduledActions(this);
            timers.ScheduleAction(this, delay + EffectiveDuration, OnEffectComplete);
        }

        /// <summary>
        /// Per-frame location-only refresh for a clock-stamped implosion following its
        /// moving target (the §1 documented exception). Returns false once the target
        /// is gone — the suction then freezes at the last known point and the manager
        /// drops this entry (the stamp keeps animating on the GPU regardless).
        /// </summary>
        internal bool RefreshConvergenceForClock()
        {
            if (!_convergenceTransform) return false;
            TargetPosition = _convergenceTransform.position;
            PrismRenderService.SetImplosionLocation(in RenderHandle,
                new Unity.Mathematics.float3(TargetPosition.x, TargetPosition.y, TargetPosition.z));
            // Keep the culling envelope covering the moving sink — no-op while the
            // point stays inside the stamped bounds (the common case: the creature
            // approaches its meal), a minimal grow when it wanders outside.
            PrismRenderService.EncapsulateBoundsPoint(in RenderHandle,
                ToObjectPoint(TargetPosition), ConvergenceBoundsPadding);
            return true;
        }

        // Padding around the convergence point in the culling envelope — object
        // space, generous enough to absorb small per-frame target drift without
        // re-growing bounds every frame.
        const float ConvergenceBoundsPadding = 2f;

        Unity.Mathematics.float3 ToObjectPoint(Vector3 worldPoint)
        {
            var p = transform.InverseTransformPoint(worldPoint);
            return new Unity.Mathematics.float3(p.x, p.y, p.z);
        }

        /// <summary>
        /// Immediately stop any animation, clear overrides, and return to pool.
        /// </summary>
        public void ReturnToPool()
        {
            CompleteEffect();
            OnReturnToPool?.Invoke(this);
        }

        /// <summary>Externally stop (cancels animation, but does not auto-return).</summary>
        public void StopEffect() => CompleteEffect();

        // ---------------- Internals ----------------

        /// <summary>
        /// Stops the animation and clears overrides.
        /// </summary>
        internal void CompleteEffect()
        {
            // Drop the target reference so a pooled instance can't keep a destroyed
            // transform alive or track a stale target on its next reuse.
            _convergenceTransform = null;

            IsActive = false;

            // Clock path cleanup: cancel the scheduled completion (idempotent), stop
            // the location refresh, and retire the stamp so a later reuse of this
            // entity can't replay a stale clock animation.
            PrismTimerManager.Instance?.CancelScheduledActions(this);
            PrismEffectsManager.Instance?.UnregisterClockConvergence(this);
            PrismRenderService.ClearSuctionClockStamp(in RenderHandle);

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);

            if (prismRenderer && mpb != null)
            {
                mpb.Clear();
                prismRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Fired by the scheduled completion (touchpoint 3) or the watchdog.
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
            if (Time.time - _activatedAtTime <= EffectiveDuration * WatchdogDurationMultiplier) return;

            CSDebug.LogWarning($"[PrismImplosion] Watchdog force-completed '{name}' " +
                               $"at world {transform.position} after {Time.time - _activatedAtTime:F2}s " +
                               $"(duration={EffectiveDuration}, IsActive={IsActive}, target={TargetPosition}). " +
                               "Likely cause: OnReturnToPool subscription was lost or duplicated.");
            OnEffectComplete();
        }
    }
}
