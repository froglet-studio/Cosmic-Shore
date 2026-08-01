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
        private float minSpeed = 10f;

        [Tooltip("Default ceiling on debris speed. This is a GUARD against the legacy impactVector/volume gain (which spans ~100x across prism sizes), not a physical bound - so it sits far below any real impact speed and flattens the magnitude of everything that hits it. An impact that hands over a true velocity passes its own limit through PrismEventData.DebrisSpeedLimit instead. This band and the proportional paths' restitution/debrisSpeedLimit are one tuning group - scale them together or the retune clips instead of toning down.")]
        [SerializeField]
        private float maxSpeed = 33.33f;

        [SerializeField]
        private MeshRenderer _renderer;

        private MaterialPropertyBlock _mpb;

        // Pool callback (set by PoolManager)
        public Action<PrismExplosion> OnReturnToPool;

        // Cache shader property IDs for performance (strict clock mode: only the
        // wiring diagnostic reads a property by name — stamps go through
        // PrismRenderService's per-instance overrides)
        private static readonly int ExplodeStartTimeId = Shader.PropertyToID("_ExplodeStartTime");

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

            // Start with renderer disabled - only PrismEffectsManager should enable it
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

            // A pooled-out clock explosion must not fire a stale completion later.
            PrismTimerManager.Instance?.CancelScheduledActions(this);
            PrismRenderService.ClearExplosionClockStamp(in RenderHandle);

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
        /// <param name="speedLimitOverride">
        /// Per-impact ceiling replacing <see cref="maxSpeed"/>; 0 keeps the authored value.
        /// Supplied by impacts that hand over a TRUE velocity rather than the legacy
        /// inertia/volume product, so their accurate magnitude is not clipped by a guard
        /// sized for a different quantity.
        /// </param>
        public void TriggerExplosion(Vector3 velocity, float speedLimitOverride = 0f)
        {
            if (_renderer == null || _mpb == null)
                return;

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            // If already active, unregister first
            if (IsActive)
                PrismEffectsManager.Instance?.UnregisterExplosion(this);

            bool hasOverride = speedLimitOverride > 0f;
            float ceiling = hasOverride ? speedLimitOverride : maxSpeed;

            // Clamp velocity and calculate speed
            velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, ceiling, out float speed);

            // ClampMagnitude reports the PRE-clamp magnitude, so Speed - which drives the
            // shatter rate (_ExplosionAmount = speed * elapsed) - has always run at the raw
            // value while the translation was capped. On the legacy gain that quirk is
            // load-bearing tuning, so leave it alone; but a true-velocity impact must keep
            // both channels on one number, or raising its ceiling would finish the shatter
            // inside a single frame while the debris crawled.
            if (hasOverride)
                speed = velocity.magnitude;

            // Store state for manager to read
            InitialPosition = transform.position;
            Velocity = velocity;
            Speed = speed;
            Elapsed = 0f;
            IsActive = true;

            EnsureRenderEntity();

            // Clock-material path (Docs/PRISM_ANIMATION.md §4.4, B3) — STRICT MODE,
            // the ONLY path: ONE stamp of {t0, speed, duration, velocity}; the shader
            // flies/shatters/fades the debris off _Time.y (offset = v·t,
            // amount = speed·t, opacity = 1−t/dur) with zero further CPU writes; ONE
            // scheduled completion returns it to the pool. The entity transform holds
            // the initial pose and never moves. PrismEffectsManager is never engaged.
            // No fallback: a missing entity or unwired graph fails LOUD and the
            // effect is skipped/frozen until the wiring lands.
            _renderer.enabled = false;

            if (UsesEntityRenderPath)
            {
                if (_renderer.sharedMaterial == null ||
                    !_renderer.sharedMaterial.HasProperty(ExplodeStartTimeId))
                    PrismClockDiagnostics.WarnUnwiredMaterial(_renderer.sharedMaterial, "_ExplodeStartTime", this);

                // Flight velocity in OBJECT space, converted once here against the
                // pose the entity is frozen at — the shader does offset = v·t with
                // no per-instance matrix reads (exact direction by construction).
                Vector3 objVelocity = transform.InverseTransformVector(velocity);
                var objVel3 = new Unity.Mathematics.float3(objVelocity.x, objVelocity.y, objVelocity.z);

                PrismRenderService.StampExplosionClock(in RenderHandle,
                    PrismClock.Now, speed, MaxDuration,
                    new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z),
                    in objVel3);
                SyncRenderTransform();

                // Culling envelope: bounds must cover the WHOLE deterministic flight
                // (the entity matrix never moves), else debris culls against the
                // unexploded box. Reset first — pooled reuse must not compound.
                if (_meshFilter != null)
                    PrismRenderService.ResetBoundsToMesh(in RenderHandle, _meshFilter.sharedMesh);
                var objDisp = objVel3 * MaxDuration;
                float pad = 4f + 0.25f * Unity.Mathematics.math.length(objDisp);
                PrismRenderService.ExpandBoundsForClockAnimation(in RenderHandle, in objDisp, pad);
                if (_hasPendingTeamColors)
                {
                    PrismRenderService.SetTeamColors(in RenderHandle,
                        PrismRenderService.ToFloat4(_pendingBrightColor),
                        PrismRenderService.ToFloat4(_pendingDarkColor));
                }
                // Visible immediately: the stamp itself IS the correct initial state
                // (amount 0, opacity 1 at t = t0) — no unanimated-mesh flash to hide.
                PrismRenderService.SetVisible(in RenderHandle, true);
            }
            else
            {
                PrismClockDiagnostics.WarnNoRenderEntity($"explosion:{name}", this);
            }

            // Touchpoint 3: the analytic end (pool return), on both outcomes so the
            // pool flow never wedges.
            var timers = PrismTimerManager.EnsureInstance();
            timers.CancelScheduledActions(this);
            timers.ScheduleAction(this, MaxDuration, OnEffectComplete);
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

            // Clock path cleanup: cancel the scheduled completion (idempotent) and
            // retire the stamp so a later legacy-path reuse of this entity can't
            // replay a stale clock animation.
            PrismTimerManager.Instance?.CancelScheduledActions(this);
            PrismRenderService.ClearExplosionClockStamp(in RenderHandle);

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);

            if (_renderer != null)
            {
                // Disable renderer - only PrismEffectsManager should enable it during
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
