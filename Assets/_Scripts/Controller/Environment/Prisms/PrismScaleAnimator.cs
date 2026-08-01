using UnityEngine;
using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.ECS;
using Unity.Mathematics;

namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(Prism))]
    public class PrismScaleAnimator : MonoBehaviour
    {
        [SerializeField] ScriptableEventPrismStats onPrismVolumeModified;

        [Header("Scale Constraints")]
        [SerializeField] private Vector3 minScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 maxScale = new Vector3(10f, 10f, 10f);

        [Header("Defaults")]
        [SerializeField] private bool usePrefabScaleAsDefaultTarget;
        [SerializeField] private Vector3 authoredTargetScale;
        public Vector3 MinScale => minScale;
        public Vector3 MaxScale { get => maxScale; set => maxScale = value; }

        public Vector3 TargetScale { get; private set; }
        public Vector3 AuthoredTargetScale => authoredTargetScale;
        public float GrowthRate { get; set; } = 0.01f;

        private Prism prism;
        private MeshRenderer meshRenderer;
        private bool isRegistered;

        /// <summary>Owning prism — PrismScaleManager pushes transform updates to
        /// the companion render entity through this after each scale write.</summary>
        internal Prism OwnerPrism => prism;

        /// <summary>Time.time of this animator's last processed growth step.
        /// Written by PrismScaleManager: the sliced growth pass steps each grower
        /// with its true elapsed dt, so tempo is preserved no matter how many
        /// frames sit between a grower's slices.</summary>
        internal float LastStepTime { get; set; }

        private bool isScaling;
        public bool IsScaling
        {
            get => isScaling;
            set
            {
                if (isScaling.Equals(value)) return;
                isScaling = value;

                if (isScaling) PrismScaleManager.Instance?.OnBlockStartScaling(this);
                else PrismScaleManager.Instance?.OnBlockStopScaling(this);
            }
        }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            prism = GetComponent<Prism>();

            if (meshRenderer == null)
            {
                CSDebug.LogError($"MeshRenderer missing on {gameObject.name}");
                enabled = false;
                return;
            }

            if (authoredTargetScale.Equals(Vector3.zero))
                authoredTargetScale = transform.localScale;

            if (TargetScale == Vector3.zero)
                SetTargetScale(authoredTargetScale);

            transform.localScale = Vector3.zero;
        }

        public void Initialize()
        {
            if (isRegistered) return;
            if (!PrismScaleManager.Instance) return;
            PrismScaleManager.Instance.RegisterAnimator(this);
            isRegistered = true;
        }

        private void OnDisable()
        {
            if (PrismScaleManager.Instance == null || !isRegistered) return;
            PrismScaleManager.Instance.UnregisterAnimator(this);
            isRegistered = false;
        }
        
        // ------------------------------------------------------------------
        // Clock-material growth (Docs/PRISM_ANIMATION.md, LOCKED law).
        // When the clock path is live (PrismRenderService.ClockAnimationEnabled
        // + this prism renders through a companion entity), growth is ONE stamp:
        // transform/volume/spatial go FINAL immediately (gameplay-final-at-
        // start), the GPU blooms the visual off _Time.y, and settledness is a
        // pure clock predicate. The legacy PrismScaleManager pass is never
        // engaged for clock-grown prisms.
        // ------------------------------------------------------------------

        bool _clockActive;            // a clock grow has been stamped this life
        float _clockT0;               // stamp time (PrismClock epoch)
        float _clockK;                // exponential-approach rate (per second)
        float3 _clockStartFrac;       // per-axis displayed/final fraction at t0
        float _clockSettleTime;       // analytic time the visual is within the legacy threshold

        // Same threshold the legacy manager's completion pass used
        // (COMPLETION_THRESHOLD_SQR = 0.01 on localScale distance).
        const float SETTLE_EPSILON = 0.1f;

        /// <summary>True when this prism can animate via the clock material:
        /// master toggle on AND the companion entity is drawing.</summary>
        bool ClockPathAvailable =>
            PrismRenderService.ClockAnimationEnabled &&
            prism != null &&
            PrismRenderService.IsHandleUsable(in prism.RenderHandle);

        /// <summary>Clock-aware "is the visual still blooming" — the replacement
        /// for reading IsScaling directly (legacy path still reports IsScaling).</summary>
        public bool IsVisuallyGrowing =>
            _clockActive ? PrismClock.Now < _clockSettleTime
                         : isActiveAndEnabled && IsScaling;

        /// <summary>Clock-aware settledness for the arena-ready gate: on the clock
        /// path the transform is final from the stamp, so settled == the visual has
        /// run its course; on the legacy path it's the old transform check.</summary>
        public bool IsVisuallySettled =>
            _clockActive ? PrismClock.Now >= _clockSettleTime
                         : !IsScaling && IsAtTarget;

        /// <summary>Pool-reuse reset — called from Prism.ResetState so a recycled
        /// prism can't inherit the previous life's clock state.</summary>
        internal void ResetClockState()
        {
            _clockActive = false;
            _clockSettleTime = 0f;
        }

        /// <summary>The per-second exponential-approach rate k — the exact per-tick
        /// tempo the legacy manager derived from GrowthRate (PrismScaleManager.cs,
        /// dtNominal = the project's fixed timestep 0.04).</summary>
        float ClockRateK
        {
            get
            {
                const float dtNominal = 0.04f;
                return Mathf.Clamp(GrowthRate * dtNominal, 0.05f, 0.1f) / dtNominal;
            }
        }

        /// <summary>Analytic displayed scale under the active clock stamp — the
        /// interruption start-state for retargets (no transform read needed;
        /// the transform holds the FINAL scale).</summary>
        Vector3 ClockDisplayedScale(Vector3 finalScale)
        {
            if (!_clockActive) return transform.localScale;
            float decay = Mathf.Exp(-_clockK * Mathf.Max(PrismClock.Now - _clockT0, 0f));
            return new Vector3(
                finalScale.x * (1f - (1f - _clockStartFrac.x) * decay),
                finalScale.y * (1f - (1f - _clockStartFrac.y) * decay),
                finalScale.z * (1f - (1f - _clockStartFrac.z) * decay));
        }

        /// <summary>Stamps (or re-stamps) the clock grow. Gameplay state goes final
        /// HERE: transform, render matrix, volume cache. Side effects that the legacy
        /// path ran at completion (volume SOAP raise, IsLargest shield) run at the
        /// stamp, per the law. Only called when ClockPathAvailable and the prism's
        /// creation is complete (pre-creation calls defer to CreateBlockCoroutine's
        /// BeginGrowthAnimation at creation-complete).</summary>
        void StampClockGrowth()
        {
            var target = TargetScale;
            if (target == Vector3.zero) return;

            float now = PrismClock.Now;
            float k = ClockRateK;

            float3 startFrac;
            if (_clockActive && PrismClock.Now < _clockSettleTime)
            {
                // Retarget mid-bloom: continue from the analytically-known displayed
                // scale. Fractions may exceed 1 (shrink toward the new target).
                var displayed = ClockDisplayedScale(_clockLastTarget);
                startFrac = new float3(
                    Mathf.Approximately(target.x, 0f) ? 1f : displayed.x / target.x,
                    Mathf.Approximately(target.y, 0f) ? 1f : displayed.y / target.y,
                    Mathf.Approximately(target.z, 0f) ? 1f : displayed.z / target.z);
            }
            else
            {
                // Not mid-bloom: the transform IS the displayed scale (zero for a
                // fresh from-zero creation, the settled old target for a mid-life
                // retarget). Grow from wherever it visibly stands.
                var current = transform.localScale;
                startFrac = current == Vector3.zero
                    ? float3.zero
                    : new float3(
                        Mathf.Approximately(target.x, 0f) ? 1f : current.x / target.x,
                        Mathf.Approximately(target.y, 0f) ? 1f : current.y / target.y,
                        Mathf.Approximately(target.z, 0f) ? 1f : current.z / target.z);
            }

            // Stamp first: a failed stamp (entity lost between the availability
            // check and here) must leave the transform untouched so the legacy
            // manager fallback still has its real start scale to grow from.
            if (!PrismRenderService.StampGrow(in prism.RenderHandle, now, k, in startFrac))
            {
                _clockActive = false;
                IsScaling = true;
                return;
            }

            // Gameplay-final-at-start: transform, entity matrix, volume, shell.
            transform.localScale = target;
            prism.SyncRenderTransform();
            prism.RefreshVolumeCache();

            _clockActive = true;
            _clockT0 = now;
            _clockK = k;
            _clockStartFrac = startFrac;
            _clockLastTarget = target;

            // Settle when the largest remaining per-axis delta decays under the
            // legacy completion threshold: t = ln(d0/eps)/k.
            float d0 = Mathf.Max(
                Mathf.Abs(target.x * (1f - startFrac.x)),
                Mathf.Max(Mathf.Abs(target.y * (1f - startFrac.y)),
                          Mathf.Abs(target.z * (1f - startFrac.z))));
            _clockSettleTime = d0 <= SETTLE_EPSILON
                ? now
                : now + Mathf.Log(d0 / SETTLE_EPSILON) / k;

            // Completion side effects run at the START on the clock path (the law:
            // gameplay state final at start; only photons animate).
            ExecuteOnScaleComplete();
        }

        Vector3 _clockLastTarget;

        public void BeginGrowthAnimation(bool resetToZero = false)
        {
            if (!enabled) return;

            if (TargetScale == Vector3.zero)
                TargetScale = transform.localScale;

            if (ClockPathAvailable)
            {
                // Pre-creation calls (spawners set TargetScale before Initialize /
                // during the spawn window) defer: CreateBlockCoroutine calls this
                // again at creation-complete, when the entity is visible and the
                // stamp can take effect. The visual then blooms from zero on
                // reveal — the continuity law's preferred reading of the spawn.
                if (prism != null && !prism.IsCreationComplete) return;

                if (resetToZero) transform.localScale = Vector3.zero;
                StampClockGrowth();
                return;
            }

            if (IsScaling) return;

            if (resetToZero)
                transform.localScale = Vector3.zero;

            IsScaling = true;
        }

        public void SetTargetScale(Vector3 newTarget)
        {
            if (!enabled) return;

            newTarget.x = Mathf.Clamp(newTarget.x, minScale.x, maxScale.x);
            newTarget.y = Mathf.Clamp(newTarget.y, minScale.y, maxScale.y);
            newTarget.z = Mathf.Clamp(newTarget.z, minScale.z, maxScale.z);

            TargetScale = newTarget;
        }

        public void Grow(float amount = 1)
        {
            if (!enabled || !prism) return;

            var growthVector = amount * prism.GrowthVector;
            SetTargetScale(TargetScale + growthVector);
            BeginGrowthAnimation();
        }

        public float GetCurrentVolume()
        {
            if (!enabled) return 0f;
            var v = transform.lossyScale; // Use lossyScale to get the actual world scale, which accounts for parent scaling
            return v.x * v.y * v.z;
        }

        /// <summary>True when the visual scale has settled at TargetScale (same threshold the
        /// scale manager's completion pass uses).</summary>
        public bool IsAtTarget => (transform.localScale - TargetScale).sqrMagnitude <= 0.01f;

        /// <summary>
        /// Snap to TargetScale NOW, running the exact bookkeeping the scale manager's completion
        /// pass would (render sync, volume cache, completion callbacks, active-set removal).
        /// ONLY for windows where the world is covered (the loading gate's connecting screen) —
        /// an on-screen snap would violate continuity of existence.
        /// </summary>
        public void CompleteImmediately()
        {
            if (!enabled) return;
            var target = TargetScale;
            if (target == Vector3.zero) return; // never authored/set — nothing to settle to

            // Clock path: the transform/volume are already final — settling is one
            // stamp rewrite into the past (the visual renders fully grown from the
            // next draw; side effects already ran at the stamp).
            if (_clockActive)
            {
                if (PrismClock.Now < _clockSettleTime)
                {
                    PrismRenderService.StampGrow(in prism.RenderHandle,
                        PrismClock.SettledPast, _clockK, in _clockStartFrac);
                    _clockSettleTime = PrismClock.Now;
                }
                return;
            }

            if ((transform.localScale - target).sqrMagnitude > 0.01f)
            {
                transform.localScale = target;
                prism?.SyncRenderTransform();
                prism?.RefreshVolumeCache();
            }

            if (IsScaling)
            {
                IsScaling = false; // setter unregisters from PrismScaleManager's active set
                ExecuteOnScaleComplete();
            }
        }

        public void ExecuteOnScaleComplete()
        {
            var deltaVolume = UpdateVolume();
            onPrismVolumeModified.Raise(new PrismStats
            {
                Volume = deltaVolume,
                OwnName = prism.PlayerName,
            });

            if (!prism) return;

            if (CheckIfIsLargest())
            {
                prism.ActivateShield();
                prism.IsLargest = true;
            }

            if (CheckIfIsSmallest())
            {
                prism.IsSmallest = true;
            }
        }

        private bool CheckIfIsLargest() =>
            TargetScale.x > MaxScale.x || TargetScale.y > MaxScale.y || TargetScale.z > MaxScale.z;

        private bool CheckIfIsSmallest() =>
            TargetScale.x < MinScale.x || TargetScale.y < MinScale.y || TargetScale.z < MinScale.z;

        private float UpdateVolume()
        {
            if (!enabled || !prism || prism.prismProperties == null)
            {
                CSDebug.LogError($"Required components are null on {gameObject.name}");
                return 0f;
            }

            var oldVolume = prism.prismProperties.volume;
            prism.prismProperties.volume = TargetScale.x * TargetScale.y * TargetScale.z;
            return prism.prismProperties.volume - oldVolume;
        }

        private void OnDestroy()
        {
            if (!PrismScaleManager.Instance || !isRegistered) return;
            PrismScaleManager.Instance.UnregisterAnimator(this);
            isRegistered = false;
        }
    }
}
