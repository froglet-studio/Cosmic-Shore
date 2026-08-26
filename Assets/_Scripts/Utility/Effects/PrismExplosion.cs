using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Handles visual + positional explosion effect for prism destruction.
    /// Clock-material driven (Docs/PRISM_ANIMATION.md B3): ONE stamp, the GPU
    /// flies/shatters/fades the debris, ONE scheduled completion.
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

        [Tooltip("How much harder a DANGER prism detonates than a plain one. Scales the debris " +
                 "speed AND the shatter rate together with the whole clamp band - deliberately ONE " +
                 "number, because on this contract debris speed and shatter rate are one quantity " +
                 "(see CLAUDE.md > AOE blast impulse); splitting them makes a blast that finishes " +
                 "shattering while the debris crawls, or the reverse. 1 = a danger prism dies " +
                 "exactly like a plain one and only its palette differs. TUNING NOTE: the batched " +
                 "debris path caches this off the prefab in PrismDebris.Configure and only " +
                 "re-reads it when the prefab reference itself changes (same as minSpeed/maxSpeed), " +
                 "so edit it in edit mode - a play-mode edit will not take effect until the next " +
                 "domain reload.")]
        [SerializeField]
        private float dangerDetonationMultiplier = 1.6f;

        [SerializeField]
        private MeshRenderer _renderer;

        private MaterialPropertyBlock _mpb;

        // Pool callback (set by PoolManager)
        public Action<PrismExplosion> OnReturnToPool;

        // Cache shader property IDs for performance (strict clock mode: only the
        // wiring diagnostic reads a property by name — stamps go through
        // PrismRenderService's per-instance overrides)
        private static readonly int ExplodeStartTimeId = Shader.PropertyToID("_ExplodeStartTime");

        /// <summary>The unpressured animation length - what a death looks like when the
        /// scene is not saturated with them. Extended 1.5x (5 -> 7.5, 2026-08-11) so the
        /// per-face erosion wipe visibly completes well before the debris retires — the
        /// wipe itself also finishes with margin (PRISM_EROSION_END_MARGIN in
        /// PrismOcclusionCorridor.hlsl), so the two tunings compose rather than race.</summary>
        internal const float DefaultDuration = 7.5f;

        // Shortest an explosion is squeezed to under full pressure. Still long enough
        // to read as a death (~20 frames at 60fps) while raising the sustainable
        // effect throughput ~20x — the headroom a dense blast needs for every prism
        // to animate out rather than pop (continuity law). Scaled with DefaultDuration
        // (x1.5, 2026-08-11) so the pressure ramp keeps its shape.
        const float MinPressuredDuration = 0.33f;

        // Live-effect count at which pressure shortening starts (full length below
        // half of this, eased to MinPressuredDuration at/above it). On the clock
        // path effects cost no per-frame CPU, so this bounds POOL size and entity
        // count rather than an animation pass — same product-of-concurrency-and-
        // duration reasoning as the retired manager's ceiling.
        const int PressureCeiling = 256;

        /// <summary>
        /// This instance's animation length. Assigned by TriggerExplosion on every
        /// spawn from the live-effect pressure (so a pooled instance can never
        /// inherit a previous life's value): it shortens under load so a dense
        /// blast's effects COMPLETE as smaller, quicker puffs instead of piling up.
        /// </summary>
        internal float MaxDuration { get; private set; } = DefaultDuration;

        /// <summary>
        /// Animation length for an effect starting while <paramref name="activeCount"/>
        /// are already running (the EnabledInstances registry — the live set). Full
        /// length until half the ceiling, then eased down to the pressured minimum.
        /// Expansion and drift are speed·elapsed, so a pressured effect is a SMALLER,
        /// quicker puff rather than the same bloom fast-forwarded — deliberate
        /// (scaling speed to compensate would fling debris at up to 22x).
        /// </summary>
        static float PressuredDuration(int activeCount)
        {
            // Benchmark/diagnostic lift (PrismFactory.EffectPressureScalingDisabled):
            // every death animates at full length no matter how many effects are
            // live, so the stress rig measures the clock system UNWEAKENED. On the
            // clock path an effect costs one stamp + one entity — the pressure model
            // bounds pool/entity count, not a per-frame animation pass — so lifting
            // it is a memory/entity-count experiment, not a frame-cost cheat.
            if (Gameplay.PrismFactory.EffectPressureScalingDisabled) return DefaultDuration;

            const float pressureFloor = PressureCeiling * 0.5f;
            if (activeCount <= pressureFloor) return DefaultDuration;

            float pressure = Mathf.InverseLerp(pressureFloor, PressureCeiling, activeCount);
            return Mathf.Lerp(DefaultDuration, MinPressuredDuration, pressure);
        }

        internal bool IsActive { get; private set; }
        internal MeshRenderer Renderer => _renderer;

        /// <summary>Authored debris-speed clamp band — PrismDebris reads these off the
        /// pool prefab so the batched entity path ships identical clamp semantics.</summary>
        public float MinDebrisSpeed => minSpeed;
        public float MaxDebrisSpeed => maxSpeed;

        /// <summary>Authored danger-tier detonation gain, likewise read off the pool prefab
        /// by PrismDebris so both routes detonate identically.</summary>
        public float DangerDetonationMultiplier => Mathf.Max(0.01f, dangerDetonationMultiplier);

        /// <summary>
        /// The gain applied to a dying prism's debris because of the TIER it was wearing.
        /// Scales the flight speed, the shatter rate and the clamp band as one quantity —
        /// only DANGER differs today, because it is the only tier whose whole identity is
        /// "this is a hazard": its debris should read as a detonation rather than as
        /// ordinary mass coming apart. Shielded/super-shielded mass gets its own PALETTE
        /// (which is the part that was wrong) but plain dynamics; a shielded prism only
        /// ever explodes to a devastating hit, whose own force already carries that read.
        ///
        /// It applies on BOTH impulse paths — the legacy inertia gain and the true-velocity
        /// one (<c>proportionalDebris</c>, where "the vector IS the debris velocity"). That
        /// is a deliberate, narrow deviation from that contract: a danger prism carries its
        /// own stored energy, so what leaves it is not only the impactor's momentum, and the
        /// alternative — scaling only the legacy path — would make danger detonate harder
        /// when a hull rams it but not when a Dolphin cone does, which reads as a bug.
        /// </summary>
        public static float DetonationGain(PrismKind kind, float dangerMultiplier) =>
            kind == PrismKind.Danger ? Mathf.Max(0.01f, dangerMultiplier) : 1f;

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

            // Start with renderer disabled — the companion entity draws.
            // This prevents pool-retrieved objects from flashing.
            if (_renderer != null)
                _renderer.enabled = false;
        }

        // Enabled-instance registry for PrismEffectsManager's zombie audit — replaces the
        // periodic FindObjectsByType full-scene scans (a recurring dev-build profiler spike).
        //
        // Removal is O(1) swap-remove keyed by a stored index: List.Remove(this) is an
        // O(n) scan with UnityEngine.Object equality per element, and under a mass-death
        // burst it ran once per pool interaction against a registry tens of thousands
        // long — profiled at 1,863 ms of a single frame (2,408 pool-miss deactivations
        // scanning ~50k live effects each, most for an instance not even in the list).
        // Order is not part of the registry's contract: the audit walks it backwards
        // with idempotent checks, so a swap moving an already-visited entry into the
        // current slot is harmless.
        internal static readonly List<PrismExplosion> EnabledInstances = new();

        // OnEnable/OnDisable keep this balanced across a clean play exit; the reset covers the
        // unclean one so Count (which feeds PressuredDuration + GameLoadSampler) starts true.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => EnabledInstances.Clear();
        int _enabledIndex = -1;

        private void OnEnable()
        {
            _enabledIndex = EnabledInstances.Count;
            EnabledInstances.Add(this);
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

            IsActive = false;

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
                // (the companion entity draws; the GameObject renderer never shows).
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
        /// Fire the explosion animation: ONE clock stamp + ONE scheduled completion
        /// (Docs/PRISM_ANIMATION.md B3 — no per-frame stepping anywhere).
        /// </summary>
        /// <param name="speedLimitOverride">
        /// Per-impact ceiling replacing <see cref="maxSpeed"/>; 0 keeps the authored value.
        /// Supplied by impacts that hand over a TRUE velocity rather than the legacy
        /// inertia/volume product, so their accurate magnitude is not clipped by a guard
        /// sized for a different quantity.
        /// </param>
        /// <param name="kind">
        /// The tier the dying prism was wearing. Affects the DYNAMICS only (danger detonates
        /// harder — see <see cref="DetonationGain"/>); the palette arrives separately through
        /// <see cref="SetTeamColors"/>.
        /// </param>
        public void TriggerExplosion(Vector3 velocity, float speedLimitOverride = 0f,
                                     PrismKind kind = PrismKind.Plain)
        {
            if (_renderer == null || _mpb == null)
                return;

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            // Tier gain scales the vector AND the whole clamp band together, so a danger prism
            // genuinely detonates harder instead of saturating against a band sized for plain
            // mass. Applied before the clamp so the floor lifts with it too.
            float gain = DetonationGain(kind, dangerDetonationMultiplier);
            velocity *= gain;

            bool hasOverride = speedLimitOverride > 0f;
            float ceiling = (hasOverride ? speedLimitOverride : maxSpeed) * gain;

            // Clamp velocity and calculate speed
            velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed * gain, ceiling, out float speed);

            // ClampMagnitude reports the PRE-clamp magnitude, so Speed - which drives the
            // shatter rate (_ExplosionAmount = speed * elapsed) - has always run at the raw
            // value while the translation was capped. On the legacy gain that quirk is
            // load-bearing tuning, so leave it alone; but a true-velocity impact must keep
            // both channels on one number, or raising its ceiling would finish the shatter
            // inside a single frame while the debris crawled.
            if (hasOverride)
                speed = velocity.magnitude;

            IsActive = true;

            // Pressure-scale THIS life's duration before anything reads it — the
            // clock stamp, the bounds envelope, and the scheduled completion all
            // key off MaxDuration. EnabledInstances is the live-effect registry.
            MaxDuration = PressuredDuration(EnabledInstances.Count);

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

                // ONE stamped vector: the WORLD-space velocity. The world->object
                // conversion for the flight offset happens on the GPU inside
                // PrismExplosionClock (raw inverse-model multiply) — no CPU-side
                // matrix math on the animation path, and the shatter-spin axis
                // chain reads the same vector.
                PrismRenderService.StampExplosionClock(in RenderHandle,
                    PrismClock.Now, speed, MaxDuration,
                    new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z));
                SyncRenderTransform();

                // Culling envelope: bounds must cover the WHOLE deterministic flight
                // (the entity matrix never moves), else debris culls against the
                // unexploded box. RenderBounds is a CPU-owned Entities Graphics
                // structure (frustum culling runs on the CPU), so this one-shot
                // object-space sizing at the stamp is the envelope's home — it is
                // initial-conditions data, not animation. Reset first: pooled reuse
                // must not compound envelopes run over run.
                if (_meshFilter != null)
                    PrismRenderService.ResetBoundsToMesh(in RenderHandle, _meshFilter.sharedMesh);
                Vector3 objDispV = transform.InverseTransformVector(velocity) * MaxDuration;
                var objDisp = new Unity.Mathematics.float3(objDispV.x, objDispV.y, objDispV.z);
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
                PrismClockDiagnostics.WarnNoRenderEntity($"explosion:{name}", this,
                    $"entity never created for this pooled effect [service: {PrismRenderService.StatusLine()}]");
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
        /// Stops the animation and clears overrides.
        /// </summary>
        internal void CompleteEffect()
        {
            IsActive = false;

            // Clock path cleanup: cancel the scheduled completion (idempotent) and
            // retire the stamp so a later reuse of this entity can't replay a
            // stale clock animation.
            PrismTimerManager.Instance?.CancelScheduledActions(this);
            PrismRenderService.ClearExplosionClockStamp(in RenderHandle);

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);

            if (_renderer != null)
            {
                // Keep the renderer disabled (the companion entity draws). Leaving
                // it enabled here caused undistorted sphere flashes when
                // OnReturnToPool was null or pool deactivation was delayed.
                _renderer.enabled = false;
                if (_mpb != null)
                {
                    _mpb.Clear();
                    _renderer.SetPropertyBlock(_mpb);
                }
            }
        }

        /// <summary>
        /// Fired by the scheduled completion at MaxDuration (touchpoint 3).
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
