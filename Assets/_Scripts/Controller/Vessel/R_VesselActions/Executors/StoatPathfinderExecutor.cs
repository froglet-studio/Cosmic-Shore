using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's flight CLOCK - its Time ability, passive (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1b). It keeps its old
    /// name so the prefab's script reference holds; it draws nothing.
    ///
    /// <para><b>The warp.</b> While the swing's hole is up (<see cref="StoatSlingExecutor.WarpsFlight"/>) the hull's own flight
    /// clock (<see cref="VesselTransformer.FlightTimeScale"/>) rises toward <see cref="StoatSlingConfigSO.CatchWarpTarget"/> of
    /// <see cref="StoatDipoleConfigSO.Boost"/> (Time is rate) - EARNED: before the swing starts only
    /// <see cref="StoatSlingConfigSO.PressWarpShare"/> of it, the whole of it (and <see cref="StoatSlingConfigSO.OrbitSpeedBonus"/>
    /// more) while the swing turns the hull, and after the fling its momentum (<see cref="StoatSlingExecutor.MomentumWarp"/>). The
    /// largest wins; they never add. This is the swing's speed-up: the one slingshot.</para>
    ///
    /// <para><b>Nothing is drawn, for any pilot</b> (Yash, 2026-11-22: "I dont need to see the drawing line either"). The swing's
    /// reach ring, its dotted exit line and its fling ring were deleted with <c>StoatPathfinderWorldDots</c>; Time 5's Foresight
    /// and every older path and ring went on 2026-11-21.</para>
    ///
    /// <para><b>One writer.</b> This is the only writer of <see cref="VesselTransformer.FlightTimeScale"/> on the Stoat; it
    /// hands it back at 1 whenever it stops (disable, no authority). The warp runs on the machine that flies the hull.</para>
    /// </summary>
    public sealed class StoatPathfinderExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("The warp's boost lives in the Stoat's dipole config. A passive ability's config is wired on its executor.")]
        [SerializeField] StoatDipoleConfigSO config;

        IVesselStatus _status;
        StoatSlingExecutor _sling;
        float _boost = 1f;
        bool _warped;
        bool _wasWarped;
        float _overclockUntil = float.NegativeInfinity;

        /// <summary>The boost multiplier on the hull's flight clock this frame (1 = none).</summary>
        public float BoostMultiplier => _boost;
        /// <summary>True while the warp is on (a swing hole up, its momentum, or the omni crystal's Overclock).</summary>
        public bool IsWarped => _warped;
        /// <summary>True while an omni crystal's Overclock holds the boost at full.</summary>
        public bool IsOverclocked => Time.time < _overclockUntil;

        /// <summary>
        /// The omni crystal's OVERCLOCK (<see cref="StoatOverclockByCrystalEffectSO"/>): for <paramref name="seconds"/> the
        /// warp holds at the field's full boost whether or not a hole is up. A second pickup extends it.
        /// </summary>
        public void Overclock(float seconds) => _overclockUntil = Mathf.Max(_overclockUntil, Time.time) + Mathf.Max(0f, seconds);

        public override void Initialize(IVesselStatus shipStatus)
        {
            ReleaseClock();
            _status = shipStatus;
            _sling = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<StoatSlingExecutor>() : null;
            if (!_sling) TryGetComponent(out _sling);
            _boost = 1f;
            _warped = _wasWarped = false;
            _overclockUntil = float.NegativeInfinity;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatPathfinderExecutor has no StoatDipoleConfig — no warp. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset on the prefab.");
        }

        void Update()
        {
            if (config == null || _status == null) return;
            var transformer = _status.VesselTransformer;
            if (!transformer || !_status.Transform || !MantaStingActionExecutor.IsSimAuthority(_status))
            {
                ReleaseClock();
                return;
            }

            bool flying = transformer.IsActive && !_status.IsStationary && !_status.IsTranslationRestricted;
            // The omni crystal's Overclock holds the field's full boost; otherwise the swing's warp.
            float target = Mathf.Max(1f, config.Boost.EvaluateLive(_status));
            bool warped = IsOverclocked;
            var sc = _sling ? _sling.Config : null;
            if (!warped && sc)
            {
                // EARNED: a laid hole alone gives pressWarpShare of the swing's warp, the swing all of it and a little more,
                // a fling its momentum. The flight runs at the largest, never their sum.
                float swingTop = sc.CatchWarpTarget(target);
                float held = _sling.IsOrbiting ? swingTop * (1f + sc.OrbitSpeedBonus)
                    : _sling.WarpsFlight ? 1f + (swingTop - 1f) * sc.PressWarpShare : 1f;
                target = Mathf.Max(held, _sling.MomentumWarp);
                warped = target > 1.001f;
            }
            _warped = flying && warped;
            _boost = flying ? StoatDipoleMath.StepBoost(_boost, warped, target, config.BoostRise, config.BoostFadeSeconds, Time.deltaTime) : 1f;
            transformer.FlightTimeScale = _boost;

            if (warped && !_wasWarped) PlayOneShot(config.WarpEvent);
            _wasWarped = warped;
        }

        void ReleaseClock()
        {
            _boost = 1f;
            _warped = false;
            var transformer = _status?.VesselTransformer;
            if (transformer) transformer.FlightTimeScale = 1f;
        }

        void PlayOneShot(EventReference reference)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        void OnDisable()
        {
            ReleaseClock();
            _wasWarped = false;
        }
    }
}
