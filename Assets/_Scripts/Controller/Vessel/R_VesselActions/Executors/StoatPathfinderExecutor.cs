using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's flight CLOCK and the swing's drawn REACH and EXIT LINE - its Time ability, passive
    /// (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1b).
    ///
    /// <para><b>The warp.</b> While the swing's hole is up (<see cref="StoatSlingExecutor.WarpsFlight"/>) the hull's own flight
    /// clock (<see cref="VesselTransformer.FlightTimeScale"/>) rises toward <see cref="StoatSlingConfigSO.CatchWarpTarget"/> of
    /// <see cref="StoatDipoleConfigSO.Boost"/> (Time is rate) - EARNED: before the swing starts only
    /// <see cref="StoatSlingConfigSO.PressWarpShare"/> of it, the whole of it (and <see cref="StoatSlingConfigSO.OrbitSpeedBonus"/>
    /// more) while the swing turns the hull, and after the fling its momentum (<see cref="StoatSlingExecutor.MomentumWarp"/>). The
    /// largest wins; they never add.</para>
    ///
    /// <para><b>What is drawn, and for whom.</b> Only for the LOCAL HUMAN pilot (<see cref="IsLocalHumanPilot"/>: the local
    /// owner, no AI seat, no autopilot): a faint ring round the swing's hole where the swing starts, and a dotted line along the
    /// heading the swing would throw you on right now. No AI hull and no remote hull draws anything (Yash, 2026-11-21: "I see the
    /// pathfinding dotted line in Stoat AIs").</para>
    ///
    /// <para><b>Deleted 2026-11-21</b> (Yash: "delete all other old pathfinding"): Time 5's Foresight, which drew the old field
    /// trajectory's predicted path in front of every rival - the dotted line on the AI Stoats - and the catch orbit's,
    /// trampoline's and skim sling's rings. Mass mode's predicted path was removed 2026-11-02.</para>
    ///
    /// <para><b>One writer.</b> This is the only writer of <see cref="VesselTransformer.FlightTimeScale"/> on the Stoat; it
    /// hands it back at 1 whenever it stops (disable, no authority). The warp runs on the machine that flies the hull.</para>
    /// </summary>
    public sealed class StoatPathfinderExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("The warp's boost and the drawn ring's colours live in the Stoat's dipole config. A passive " +
                 "ability's config is wired on its executor.")]
        [SerializeField] StoatDipoleConfigSO config;

        const int RingSegments = 96;

        IVesselStatus _status;
        StoatSlingExecutor _sling;
        float _boost = 1f;
        bool _warped;
        bool _wasWarped;
        readonly Vector3[] _ring = new Vector3[RingSegments];
        StoatPathfinderWorldDots _worldDots;

        static readonly ProfilerMarker s_ringsMarker = new("StoatPathfinder.Rings");
        float _overclockUntil = float.NegativeInfinity;

        /// <summary>The boost multiplier on the hull's flight clock this frame (1 = none).</summary>
        public float BoostMultiplier => _boost;
        /// <summary>True while the warp is on (a pair held, or the omni crystal's Overclock).</summary>
        public bool IsWarped => _warped;
        /// <summary>True while an omni crystal's Overclock holds the boost at full.</summary>
        public bool IsOverclocked => Time.time < _overclockUntil;

        /// <summary>
        /// The omni crystal's OVERCLOCK (<see cref="StoatOverclockByCrystalEffectSO"/>): for <paramref name="seconds"/> the
        /// warp holds at the field's full boost whether or not a pair is held. A second pickup extends it.
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
                CSDebug.LogError($"[Stoat] {name}: StoatPathfinderExecutor has no StoatDipoleConfig — no warp, no rings. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatDipoleConfig.asset on the prefab.");
        }

        void Update()
        {
            if (config == null || _status == null) return;
            var transformer = _status.VesselTransformer;
            var hull = _status.Transform;
            if (!transformer || !hull || !MantaStingActionExecutor.IsSimAuthority(_status))
            {
                ReleaseClock();
                return;
            }

            bool flying = transformer.IsActive && !_status.IsStationary && !_status.IsTranslationRestricted;
            float dt = Time.deltaTime;
            // The omni crystal's Overclock holds the field's full boost; otherwise the held pair's catch warp, a little more
            // while the orbit holds the hull.
            float target = Mathf.Max(1f, config.Boost.EvaluateLive(_status));
            bool warped = IsOverclocked;
            var sc = _sling ? _sling.Config : null;
            if (!warped && sc)
            {
                // EARNED (2026-11-02): a held pair alone gives pressWarpShare of the catch's warp, the orbit all of it and a
                // little more, a timed launch its momentum. The flight runs at the largest, never their sum.
                float catchTop = sc.CatchWarpTarget(target);
                float held = _sling.IsOrbiting ? catchTop * (1f + sc.OrbitSpeedBonus)
                    : _sling.WarpsFlight ? 1f + (catchTop - 1f) * sc.PressWarpShare : 1f;
                target = Mathf.Max(held, _sling.MomentumWarp);
                warped = target > 1.001f;
            }
            _warped = flying && warped;
            _boost = flying ? StoatDipoleMath.StepBoost(_boost, warped, target, config.BoostRise, config.BoostFadeSeconds, dt) : 1f;
            transformer.FlightTimeScale = _boost;

            if (warped && !_wasWarped) PlayOneShot(config.WarpEvent);
            _wasWarped = warped;

            if (IsLocalHumanPilot && flying) using (s_ringsMarker.Auto()) DrawRings(hull);
        }

        /// <summary>The person flying this hull on this machine: a player's hull, the local owner, no AI seat and no autopilot.
        /// Nothing is drawn for anyone else - an AI hull on the host is "local" too, and an AI seat may be waiting for its
        /// autopilot to start.</summary>
        bool IsLocalHumanPilot => _status.Player != null && _status.IsLocalUser && !_status.IsInitializedAsAI &&
                                  !_status.AutoPilotEnabled;

        void DrawRings(Transform hull)
        {
            var cam = ResolveCamera();
            if (!cam) return;
            _worldDots ??= new StoatPathfinderWorldDots();
            if (!_worldDots.Begin(cam)) return;
            AddSwingRings(hull);
            _worldDots.End();
        }

        /// <summary>
        /// The swing (STOAT_DIPOLE.md §1b), world dots only. The REACH: a faint ring round the black hole in the plane
        /// of your pass, where the swing starts. The EXIT LINE: dots from the hull along the heading the swing would throw you
        /// on right now - the stick's angle about the hole - so moving the stick visibly moves where you will go; lime while it
        /// swings you. Each fling throws a lime RIPPLE ring growing out of the hole, fading.
        /// </summary>
        void AddSwingRings(Transform hull)
        {
            var sc = _sling ? _sling.Config : null;
            if (!sc || !hull) return;
            int n;
            float age = _sling.SwingFxAge;
            const float rippleSeconds = 0.6f;
            if (age >= 0f && age < rippleSeconds)
            {
                var ripple = config.WarpedColor;
                ripple.a *= 1f - age / rippleSeconds;
                float r = sc.SwingReach * (0.3f + 0.9f * age / rippleSeconds);
                n = StoatSlingMath.Ring(_sling.SwingFxCentre, _sling.SwingFxNormal, r, RingSegments, _ring);
                _worldDots.Add(_ring, n, null, 0, ripple, sc.RingDotSize * 1.3f, sc.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);
            }
            if (!_sling.IsSwingUp) return;
            var hole = _sling.SwingHolePosition;
            bool swinging = _sling.IsSwinging;

            var reach = config.OpenColor;
            reach.a *= swinging ? sc.OrbitRingAlpha : sc.DetectionRingAlpha;
            var normal = StoatSlingMath.RingNormal(hull.position, hull.forward, hole, hull.up);
            n = StoatSlingMath.Ring(hole, normal, _sling.SwingReachRadius, RingSegments, _ring);
            _worldDots.Add(_ring, n, null, 0, reach, sc.RingDotSize, sc.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);

            // The exit line: from just ahead of the nose, along the heading the stick asks for now.
            var exit = _sling.SwingExitHeading;
            const int linePoints = 2;
            _ring[0] = hull.position + exit * 30f;
            _ring[1] = hull.position + exit * 180f;
            var line = swinging ? config.WarpedColor : config.OpenColor;
            line.a *= swinging ? 1f : sc.OrbitRingAlpha;
            _worldDots.Add(_ring, linePoints, null, 0, line, sc.RingDotSize, sc.RingDotSpacing, config.WorldDotMinPixels);
        }

        static Camera ResolveCamera()
        {
            var manager = CameraManager.Instance;
            if (manager && manager.GetActiveController() is CustomCameraController controller && controller.Camera)
                return controller.Camera;
            return BlackHoleLens.ViewCamera();
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

        void OnDestroy()
        {
            _worldDots?.Dispose();
            _worldDots = null;
        }
    }
}
