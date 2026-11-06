using System.Collections.Generic;
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
    /// The Stoat's flight CLOCK and the orbit's RINGS - its Time ability, passive (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1b).
    ///
    /// <para><b>The warp.</b> While a pair is held (<see cref="StoatSlingExecutor.WarpsFlight"/>) the hull's own flight clock
    /// (<see cref="VesselTransformer.FlightTimeScale"/>) rises to <see cref="StoatSlingConfigSO.CatchWarpTarget"/> of
    /// <see cref="StoatDipoleConfigSO.Boost"/> (Time is rate: ×2.7 at rest, ×5.6 at level 10) - but EARNED (2026-11-02): before a
    /// catch only <see cref="StoatSlingConfigSO.PressWarpShare"/> of it, the whole of it (and
    /// <see cref="StoatSlingConfigSO.OrbitSpeedBonus"/> more) while the orbit holds the hull, and after a timed launch its
    /// momentum (<see cref="StoatSlingExecutor.MomentumWarp"/>). The largest wins; they never add. The path stays the path and
    /// only arrives sooner.</para>
    ///
    /// <para><b>The rings.</b> While a pair is laid two rings are drawn round its attractor, in the plane of the hull's pass:
    /// the DETECTION radius (faint - where it can catch you) and the ORBIT radius (where it holds you; brighter once it has
    /// caught you, easing toward lime as the quarter-turn nears and LIME while a release would pay the whole kick,
    /// <see cref="StoatSlingExecutor.Timing01"/>). That is the cue the 90° slingshot is timed by.</para>
    ///
    /// <para><b>Removed 2026-11-02</b> (Yash): the predicted PATH of Mass mode's field trajectory and its warp, and Gate mode's
    /// gate mouth. What they were and how to bring them back: <c>Docs/Studios/STOAT_FIELD_TRAJECTORY_PARKED.md</c>.</para>
    ///
    /// <para><b>Time 5 — Foresight</b> (the replicated unlock bit): the nearest rivals' paths are drawn, in their domain
    /// colours, each flown forward on the turn and speed it is holding right now (measured from its pose, no input needed).
    /// Presentation only, local pilot only. They are drawn on a screen-space overlay, AFTER the black-hole lens
    /// (<see cref="StoatPathfinderDots"/>): a prediction is not light, so the lens must not bend or double it.</para>
    ///
    /// <para><b>One writer.</b> This is the only writer of <see cref="VesselTransformer.FlightTimeScale"/> on the Stoat; it
    /// hands it back at 1 whenever it stops (disable, no authority). The warp runs on the machine that flies the hull.</para>
    /// </summary>
    public sealed class StoatPathfinderExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("The warp's boost, the ring colours and Foresight's numbers live in the Stoat's dipole config. A passive " +
                 "ability's config is wired on its executor.")]
        [SerializeField] StoatDipoleConfigSO config;

        const float ForesightRefreshSeconds = 1f;
        const int RingSegments = 96;

        IVesselStatus _status;
        StoatSlingExecutor _sling;
        float _boost = 1f;
        bool _warped;
        bool _wasWarped;
        readonly Vector3[] _ring = new Vector3[RingSegments];
        StoatPathfinderWorldDots _worldDots;

        /// <summary>One rival the Time-5 Foresight is drawing.</summary>
        sealed class Rival
        {
            public VesselStatus Vessel;
            public Quaternion LastRotation;
            public bool HasLast;
            public StoatPathfinderDots Dots;
            public readonly Vector3[] Points = new Vector3[512];
            public readonly int[] Jumps = new int[1];
        }

        readonly List<Rival> _rivals = new();
        static readonly List<VesselStatus> s_scan = new();

        static readonly ProfilerMarker s_ringsMarker = new("StoatPathfinder.Rings");
        static readonly ProfilerMarker s_foresightMarker = new("StoatPathfinder.Foresight");
        float _nextForesightScan;
        float _overclockUntil = float.NegativeInfinity;

        /// <summary>The boost multiplier on the hull's flight clock this frame (1 = none).</summary>
        public float BoostMultiplier => _boost;
        /// <summary>True while the warp is on (a pair held, or the omni crystal's Overclock).</summary>
        public bool IsWarped => _warped;
        /// <summary>How many rivals the Time-5 Foresight is drawing this frame.</summary>
        public int ForesightCount => _rivals.Count;
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
            ClearForesight();
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
                ClearForesight();
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

            bool pilot = _status.IsLocalUser && !_status.AutoPilotEnabled;
            if (pilot && flying) using (s_ringsMarker.Auto()) DrawRings(hull);

            if (pilot && ForesightActive()) using (s_foresightMarker.Auto()) Foresight(dt);
            else ClearForesight();
        }

        void DrawRings(Transform hull)
        {
            var cam = ResolveCamera();
            if (!cam) return;
            _worldDots ??= new StoatPathfinderWorldDots();
            if (!_worldDots.Begin(cam)) return;
            if (_sling && _sling.UsesTrampoline) AddTrampolineRings(cam, hull);
            else if (_sling && _sling.UsesSkim) AddSkimRings(cam, hull);
            else AddRings(hull);
            _worldDots.End();
        }

        /// <summary>
        /// Time mode's trampoline (STOAT_DIPOLE.md §1b), world dots only. The SHELL round the white hole as a ball: its outline
        /// facing the camera and two great circles (the plane of your pass and the one square to it), so its size and where it
        /// sits read in 3D. While you are in it, the DENT: the rim of the hollow your hull presses into the shell, lime. Each
        /// bounce throws a RIPPLE ring growing out from the shell where you left it, fading.
        /// </summary>
        void AddTrampolineRings(Camera cam, Transform hull)
        {
            var sc = _sling ? _sling.Config : null;
            if (!sc || !cam || !hull) return;
            int n;
            float age = _sling.BounceFxAge;
            const float rippleSeconds = 0.6f;
            if (age >= 0f && age < rippleSeconds)
            {
                var ripple = config.WarpedColor;
                ripple.a *= 1f - age / rippleSeconds;
                float r = _sling.BounceFxRadius * (1f + 1.5f * age / rippleSeconds);
                n = StoatSlingMath.Ring(_sling.BounceFxCentre, _sling.BounceFxNormal, r, RingSegments, _ring);
                _worldDots.Add(_ring, n, null, 0, ripple, sc.RingDotSize * 1.3f, sc.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);
            }
            if (!_sling.IsTrampolineUp) return;
            var centre = _sling.TrampolineCentre;
            float radius = _sling.TrampolineRadius;
            bool touching = _sling.IsOrbiting;

            var shell = touching ? config.WarpedColor : config.OpenColor;
            shell.a *= touching ? 1f : sc.OrbitRingAlpha;
            var facing = cam.transform.position - centre;
            if (facing.sqrMagnitude > 1e-6f)
            {
                n = StoatSlingMath.Ring(centre, facing.normalized, radius, RingSegments, _ring);
                _worldDots.Add(_ring, n, null, 0, shell, sc.RingDotSize, sc.RingDotSpacing, config.WorldDotMinPixels, closed: true);
            }
            var faint = shell;
            faint.a *= sc.DetectionRingAlpha;
            var pass = StoatSlingMath.RingNormal(hull.position, hull.forward, centre, hull.up);
            n = StoatSlingMath.Ring(centre, pass, radius, RingSegments, _ring);
            _worldDots.Add(_ring, n, null, 0, faint, sc.RingDotSize, sc.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);
            var across = Vector3.Cross(pass, centre - hull.position);
            if (across.sqrMagnitude > 1e-6f)
            {
                n = StoatSlingMath.Ring(centre, across.normalized, radius, RingSegments, _ring);
                _worldDots.Add(_ring, n, null, 0, faint, sc.RingDotSize, sc.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);
            }

            // The dent: the circle where the pressed-in hollow meets the shell, centred on the hull's normal.
            float depth = _sling.DentDepth;
            if (touching && depth > 0.05f)
            {
                var normal = _sling.DentNormal;
                float rim = Mathf.Sqrt(Mathf.Max(0f, radius * radius - (radius - depth) * (radius - depth)));
                n = StoatSlingMath.Ring(centre + normal * (radius - depth), normal, rim, RingSegments, _ring);
                _worldDots.Add(_ring, n, null, 0, config.WarpedColor, sc.RingDotSize * 1.3f, sc.RingDotSpacing, config.WorldDotMinPixels, closed: true);
            }
        }

        /// <summary>
        /// The laid pair's DETECTION ring (faint) and ORBIT ring round its attractor, in the plane of the hull's pass. The
        /// orbit ring brightens once it has caught the hull, eases toward lime as the quarter-turn nears, and is lime while a
        /// release would pay the whole kick.
        /// </summary>
        void AddRings(Transform hull)
        {
            var sc = _sling ? _sling.Config : null;
            var attractor = _sling ? _sling.Attractor : null;
            if (!sc || !attractor || !hull) return;
            var centre = attractor.transform.position;
            var normal = StoatSlingMath.RingNormal(hull.position, hull.forward, centre, hull.up);
            float timing = _sling.Timing01;
            bool caught = _sling.IsOrbiting;

            var detection = config.OpenColor;
            detection.a *= sc.DetectionRingAlpha;
            int n = StoatSlingMath.Ring(centre, normal, _sling.DetectionRadius, RingSegments, _ring);
            _worldDots.Add(_ring, n, null, 0, detection, sc.RingDotSize, sc.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);

            bool inWindow = timing >= 0.999f;
            var orbit = Color.Lerp(config.OpenColor, config.WarpedColor, inWindow ? 1f : 0.35f * timing);
            orbit.a *= caught ? 1f : sc.OrbitRingAlpha;
            n = StoatSlingMath.Ring(centre, normal, _sling.OrbitRadius, RingSegments, _ring);
            _worldDots.Add(_ring, n, null, 0, orbit, sc.RingDotSize, sc.RingDotSpacing, config.WorldDotMinPixels, closed: true);
        }

        /// <summary>
        /// Time mode's Skim Sling (STOAT_DIPOLE.md §1q), world dots only - no screen-space warp. The TIDEPOOL: a faint ring
        /// at its radius facing the camera (the capture sphere's outline), until the hull is captured. The SKIMLINE: the
        /// Skimshell's equator round the spin axis, its dots SCROLLING with the spin (faster = harder squeeze), brightening
        /// once you ride and lime while a release would pay the whole gain. A graze shell (let go of uncaught) draws its
        /// Skimline faint and no Tidepool (it captures nothing). Each SKIP throws a RIPPLE: a ring in the Skimline's plane
        /// growing out from R and fading (the one cheap world-dot ring, no particles).
        /// </summary>
        void AddSkimRings(Camera cam, Transform hull)
        {
            var sc = _sling ? _sling.Config : null;
            var skim = sc ? sc.Skim : null;
            if (!skim || !cam || !hull) return;
            int n;
            float age = _sling.SkipFxAge;
            if (age >= 0f && age < skim.SkipRippleSeconds)
            {
                var ripple = _sling.SkipFxPerfect ? config.WarpedColor : config.OpenColor;
                ripple.a *= StoatSkimMath.RippleAlpha(age, skim.SkipRippleSeconds);
                n = StoatSkimMath.Ring(_sling.SkipFxCentre, _sling.SkipFxAxis,
                    StoatSkimMath.RippleRadius(_sling.SkipFxRadius, skim.SkipRippleGrowth, age), 0f, RingSegments, _ring);
                _worldDots.Add(_ring, n, null, 0, ripple, skim.RingDotSize * 1.3f, skim.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);
            }
            if (!_sling.HasPair) return;
            var centre = _sling.SkimCentre;
            bool riding = _sling.IsOrbiting;
            bool graze = _sling.IsGrazeShell;
            if (!riding && !graze)
            {
                var facing = cam.transform.position - centre;
                if (facing.sqrMagnitude > 1e-6f)
                {
                    var pool = config.OpenColor;
                    pool.a *= skim.TidepoolAlpha;
                    n = StoatSkimMath.Ring(centre, facing.normalized, _sling.DetectionRadius, 0f, RingSegments, _ring);
                    _worldDots.Add(_ring, n, null, 0, pool, skim.RingDotSize, skim.RingDotSpacing * 1.5f, config.WorldDotMinPixels, closed: true);
                }
            }
            float timing = _sling.Timing01;
            var line = Color.Lerp(config.OpenColor, config.WarpedColor, timing >= 0.999f ? 1f : 0.35f * timing);
            line.a *= riding ? 1f : graze ? 0.5f * skim.SkimlineAlpha : skim.SkimlineAlpha;
            n = StoatSkimMath.Ring(centre, _sling.SkimAxis, _sling.OrbitRadius, _sling.SpinPhaseDegrees, RingSegments, _ring);
            _worldDots.Add(_ring, n, null, 0, line, skim.RingDotSize, skim.RingDotSpacing, config.WorldDotMinPixels, closed: true);
        }

        // ------------------------------------------------------------------ Time 5: Foresight

        bool ForesightActive()
        {
            var abilities = _status.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(Element.Time);
        }

        /// <summary>
        /// The nearest rivals' paths, each flown forward on the turn and speed it is holding now (its rotation's change
        /// since the last frame, in its own axes). The rival list is rescanned once a second, never per frame.
        /// </summary>
        void Foresight(float dt)
        {
            if (Time.unscaledTime >= _nextForesightScan)
            {
                _nextForesightScan = Time.unscaledTime + ForesightRefreshSeconds;
                ScanRivals();
            }
            var cam = ResolveCamera();
            var path = config.Path();
            path.Length = config.ForesightLength;
            path.PortalOpen = false;
            var flight = config.Flight(60f);
            for (int i = _rivals.Count - 1; i >= 0; i--)
            {
                var r = _rivals[i];
                if (!r.Vessel || !r.Vessel.isActiveAndEnabled)
                {
                    DropRival(i);
                    continue;
                }
                var t = r.Vessel.transform;
                var rotation = t.rotation;
                Vector3 spin = Vector3.zero;
                if (r.HasLast && dt > 1e-4f)
                {
                    (rotation * Quaternion.Inverse(r.LastRotation)).ToAngleAxis(out float degrees, out var axis);
                    if (degrees > 180f) degrees -= 360f;
                    if (Mathf.Abs(degrees) > 1e-3f && axis.sqrMagnitude > 1e-8f)
                        spin = Quaternion.Inverse(rotation) * (axis.normalized * (degrees * Mathf.Deg2Rad / dt));
                }
                r.LastRotation = rotation;
                r.HasLast = true;
                var start = new StoatDipoleMath.Body { Position = t.position, Rotation = rotation };
                var result = StoatDipoleMath.PredictPath(start, Mathf.Max(0f, r.Vessel.Speed), spin, default, false, flight, path,
                    r.Points, r.Jumps);
                if (!cam || result.Count < 2) { if (r.Dots) r.Dots.Hide(); continue; }
                if (!r.Dots) r.Dots = StoatPathfinderDots.Create($"{name} foresight {i}");
                if (r.Dots) r.Dots.Show(cam, r.Points, result.Count, r.Jumps, 0, -1, DomainColor(r.Vessel), config.DotPixels,
                    config.DotGap, false, config.DotDepthMinScale, config.DotDepthFade);
            }
        }

        void ScanRivals()
        {
            var hull = _status.Transform;
            if (!hull || _status.Player == null) return;
            var mine = _status.Domain;
            s_scan.Clear();
            s_scan.AddRange(VesselStatus.Live);
            float range2 = config.ForesightRange * config.ForesightRange;
            var self = _status as Component;
            s_scan.RemoveAll(v => !v || (self && v.transform == self.transform) || v.Player == null || ((IVesselStatus)v).Domain == mine
                                  || (v.transform.position - hull.position).sqrMagnitude > range2);
            var at = hull.position;
            s_scan.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
            int want = Mathf.Min(config.ForesightMaxRivals, s_scan.Count);
            // Keep the rivals still wanted (their rotation history and overlay), drop the rest, add the new.
            for (int i = _rivals.Count - 1; i >= 0; i--)
            {
                int index = s_scan.IndexOf(_rivals[i].Vessel);
                if (index < 0 || index >= want) DropRival(i);
            }
            for (int i = 0; i < want; i++)
            {
                var v = s_scan[i];
                if (_rivals.Exists(r => r.Vessel == v)) continue;
                _rivals.Add(new Rival { Vessel = v });
            }
            s_scan.Clear();
        }

        void DropRival(int index)
        {
            var r = _rivals[index];
            if (r.Dots) Destroy(r.Dots.transform.root.gameObject);
            _rivals.RemoveAt(index);
        }

        void ClearForesight()
        {
            for (int i = _rivals.Count - 1; i >= 0; i--) DropRival(i);
        }

        static Color DomainColor(VesselStatus vessel)
        {
            var colors = PrismLit.ColorSet;
            if (vessel && vessel.Player != null && colors && colors.TryGetPrismKindColors(((IVesselStatus)vessel).Domain, PrismKind.Plain, out var light, out _))
            {
                light.a = 1f;
                return light;
            }
            return Color.white;
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
            ClearForesight();
        }

        void OnDestroy()
        {
            _worldDots?.Dispose();
            _worldDots = null;
            ClearForesight();
        }
    }
}
