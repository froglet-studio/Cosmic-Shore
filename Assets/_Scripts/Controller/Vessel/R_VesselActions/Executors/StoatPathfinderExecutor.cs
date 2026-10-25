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
    /// The Stoat's PATHFINDER — its Time ability, passive (<c>R_VesselActions/STOAT_DIPOLE.md</c>): the
    /// hull's own flight run forward on this frame's inputs (<see cref="StoatDipoleMath.PredictPath"/>,
    /// the studio's <c>fieldPath</c>) through its dipole's field, drawn for the pilot as evenly spaced dots
    /// from just ahead of the nose. While the poles WARP that path at all — it bends at least
    /// <see cref="StoatDipoleConfigSO.WarpDegrees"/>, or it goes through the wormhole — it turns lime and
    /// the hull flies down it faster: a warp of the hull's own FLIGHT CLOCK
    /// (<see cref="VesselTransformer.FlightTimeScale"/>), so the path stays the path and only arrives
    /// sooner. Time is rate: the boost is <see cref="StoatDipoleConfigSO.Boost"/>, read live.
    ///
    /// <para><b>In Time mode there is no path</b> (2026-10-14: "instead of showing your path it shows a detection radius
    /// and an orbital radius"). While a Time-mode pair is laid (<see cref="StoatSlingExecutor"/>) two rings are drawn round
    /// its attractor, in the plane of the hull's pass: the DETECTION radius (faint — where it can catch you) and the ORBIT
    /// radius (where it holds you; brighter once it has caught you, easing toward lime as the quarter-turn nears and LIME
    /// while a release would pay the whole kick, <see cref="StoatSlingExecutor.Timing01"/>). That is the cue the 90°
    /// slingshot is timed by. No path is predicted in Time mode; the warp boost runs while the pair is held
    /// (<see cref="StoatSlingExecutor.WarpsFlight"/>, the studio's catch warp, 2026-10-14), to
    /// <see cref="StoatSlingConfigSO.CatchWarpShare"/> of the field's boost, so the two modes fly at one pace.</para>
    ///
    /// <para><b>Time 5 — Foresight</b> (the replicated unlock bit): the nearest rivals' paths are drawn too, in their
    /// domain colours, each flown forward on the turn and speed it is holding right now (measured from its pose, no
    /// input needed). Presentation only, local pilot only.</para>
    ///
    /// <para>The prediction and the boost run on the machine that flies the hull (the boost moves it);
    /// the dots are drawn only for a local human pilot. They are drawn on a screen-space overlay, AFTER
    /// the black-hole lens (<see cref="StoatPathfinderDots"/>): a prediction is not light, so the lens
    /// must not bend or double it (the studio's round-15 finding).</para>
    ///
    /// <para><b>One writer.</b> This is the only writer of <see cref="VesselTransformer.FlightTimeScale"/>
    /// on the Stoat; it hands it back at 1 whenever it stops (disable, no authority).</para>
    /// </summary>
    public sealed class StoatPathfinderExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("The pathfinder's numbers live with the dipole's (one Stoat dipole config). A passive ability's config is wired on its executor.")]
        [SerializeField] StoatDipoleConfigSO config;

        const int MaxPoints = 1024;
        const int MaxJumps = 8;
        const float ForesightRefreshSeconds = 1f;

        IVesselStatus _status;
        StoatDipoleExecutor _dipole;
        StoatSlingExecutor _sling;
        readonly Vector3[] _points = new Vector3[MaxPoints];
        readonly int[] _jumps = new int[MaxJumps];
        StoatDipoleMath.PathResult _path;
        float _boost = 1f;
        bool _wasWarped;
        bool _timeMode;
        StoatModeExecutor _mode;
        readonly Vector3[] _ring = new Vector3[RingSegments];
        const int RingSegments = 96;
        StoatPathfinderDots _dots;
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

        // Update read 1.06-1.80 ms/frame self in Regatta captures with a Stoat (2026-10-25/26), unsplit.
        static readonly ProfilerMarker s_predictMarker = new("StoatPathfinder.Predict");
        static readonly ProfilerMarker s_dotsMarker = new("StoatPathfinder.Dots");
        static readonly ProfilerMarker s_foresightMarker = new("StoatPathfinder.Foresight");
        float _nextForesightScan;
        float _overclockUntil = float.NegativeInfinity;

        /// <summary>The boost multiplier on the hull's flight clock this frame (1 = none).</summary>
        public float BoostMultiplier => _boost;
        /// <summary>The boost the warp would give at the pilot's Time level right now (the HUD's Time card's full mark).</summary>
        public float BoostTarget => config ? Mathf.Max(1f, config.Boost.EvaluateLive(_status)) : 1f;
        /// <summary>True while the poles warp the predicted path (the line is lime and the boost is on).</summary>
        public bool IsWarped => _path.Warped;
        /// <summary>The last prediction's verdict and length.</summary>
        public StoatDipoleMath.PathResult LastPath => _path;
        /// <summary>The last prediction's points (the first <see cref="StoatDipoleMath.PathResult.Count"/> are live).</summary>
        public Vector3[] Points => _points;
        /// <summary>How many rivals the Time-5 Foresight is drawing this frame.</summary>
        public int ForesightCount => _rivals.Count;
        /// <summary>True while an omni crystal's Overclock holds the boost at full.</summary>
        public bool IsOverclocked => Time.time < _overclockUntil;

        /// <summary>
        /// The omni crystal's OVERCLOCK (<see cref="StoatOverclockByCrystalEffectSO"/>): for <paramref name="seconds"/> the
        /// boost holds at full and the line stays lime whether or not the poles warp it. A second pickup extends it.
        /// </summary>
        public void Overclock(float seconds) => _overclockUntil = Mathf.Max(_overclockUntil, Time.time) + Mathf.Max(0f, seconds);

        public override void Initialize(IVesselStatus shipStatus)
        {
            ReleaseClock();
            _status = shipStatus;
            bool hasRegistry = TryGetComponent<ActionExecutorRegistry>(out var registry);
            _dipole = hasRegistry ? registry.Get<StoatDipoleExecutor>() : null;
            if (!_dipole) TryGetComponent(out _dipole);
            _sling = hasRegistry ? registry.Get<StoatSlingExecutor>() : null;
            if (!_sling) TryGetComponent(out _sling);
            _mode = hasRegistry ? registry.Get<StoatModeExecutor>() : null;
            if (!_mode) TryGetComponent(out _mode);
            _boost = 1f;
            _path = default;
            _wasWarped = false;
            _overclockUntil = float.NegativeInfinity;
            ClearForesight();
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatPathfinderExecutor has no StoatDipoleConfig — no path, no boost. " +
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
                HideDots();
                ClearForesight();
                return;
            }

            bool flying = transformer.IsActive && !_status.IsStationary && !_status.IsTranslationRestricted;
            // Time mode draws the catch's rings, never a path (and so never warps one).
            _timeMode = _mode && _mode.IsTimeMode;
            if (flying && !_timeMode) using (s_predictMarker.Auto()) Predict(transformer, hull);
            else _path = default;

            float dt = Time.deltaTime;
            float target = Mathf.Max(1f, config.Boost.EvaluateLive(_status));
            if (flying && IsOverclocked) _path.Warped = true;   // the omni crystal: the line is lime and the boost on
            // Time mode: the catch warps the clock while its pair is held (the field's rule, "held" in place of "lime").
            bool warped = _path.Warped;
            if (_timeMode && _sling && _sling.Config && !IsOverclocked)
            {
                warped = _sling.WarpsFlight;
                target = _sling.Config.CatchWarpTarget(target);
            }
            _boost = flying ? StoatDipoleMath.StepBoost(_boost, warped, target, config.BoostRise, config.BoostFadeSeconds, dt) : 1f;
            transformer.FlightTimeScale = _boost;

            if (warped && !_wasWarped) PlayOneShot(config.WarpEvent);
            _wasWarped = warped;

            bool pilot = _status.IsLocalUser && !_status.AutoPilotEnabled;
            if (pilot && flying) using (s_dotsMarker.Auto()) DrawDots();
            else HideDots();

            if (pilot && ForesightActive()) using (s_foresightMarker.Auto()) Foresight(dt);
            else ClearForesight();
        }

        void Predict(VesselTransformer transformer, Transform hull)
        {
            StoatDipoleMath.Field field = default;
            bool hasField = _dipole && _dipole.TryGetField(out field);
            float clock = Mathf.Max(1e-3f, transformer.FlightTimeScale);
            float engine = Mathf.Max(0f, _status.Speed) / clock;
            var start = new StoatDipoleMath.Body
            {
                Position = hull.position, Rotation = hull.rotation, GravitySpeed = _dipole ? _dipole.GravitySpeed : 0f,
                Winding = _dipole ? _dipole.Winding : 0f,
                Command = transformer.CommandedRotation, HasCommand = true,
            };
            // Fly the hull as the transformer does (the nose chasing the command, the speed easing to its target), not as
            // a body that turns and speeds up the instant it is asked: the dots are where THIS hull will go.
            float target = Mathf.Max(0f, transformer.CurrentThrottleTarget);
            var response = new StoatDipoleMath.HullResponse
            {
                Enabled = true,
                // THIS hull's rate: a human pilot's nose has no lag (infinite rate), an AI's keeps the fleet's.
                NoseFollowRate = transformer.NoseFollowRate,
                SpeedFollowRate = VesselTransformer.RotationFollowRate,
                // The target in the same units as `engine` (the published speed carries the speed multiplier and warp field).
                TargetSpeed = target * transformer.SpeedMultiplier * WarpFieldRuntime.ScaleAt(hull.position),
            };
            _path = StoatDipoleMath.PredictPath(start, engine, SteeringRate(transformer), field, hasField,
                config.Flight(transformer.CruiseSpeed), config.Path(), _points, _jumps, response);
        }

        /// <summary>
        /// The steering the pilot is holding, rad/s in the hull's own axes — the rates
        /// <c>VesselTransformer.Pitch/Yaw/Roll</c> command from the same sticks (pitch about the hull's right,
        /// yaw about its up, roll about its forward), so the line bends the way the hull is about to.
        /// </summary>
        Vector3 SteeringRate(VesselTransformer transformer)
        {
            var input = _status.InputStatus;
            if (input == null) return Vector3.zero;
            float k = Mathf.Deg2Rad * Mathf.Max(0f, transformer.ExternalTurnRateMultiplier);
            return new Vector3(input.YSum * transformer.PitchScaler, input.XSum * transformer.YawScaler,
                transformer.BankIntoTurnSuppressed ? 0f : input.YDiff * transformer.RollScaler) * k;
        }

        void DrawDots()
        {
            var cam = ResolveCamera();
            if (!cam) { HideDots(); return; }
            if (config.DotsInWorld)
            {
                if (_dots) _dots.Hide();
                _worldDots ??= new StoatPathfinderWorldDots();
                if (!_worldDots.Begin(cam)) return;
                if (_timeMode) AddRings(hull: _status.Transform);
                else
                    _worldDots.Add(_points, _path.Count, _jumps, _path.JumpCount,
                        _path.Warped ? config.WarpedColor : config.OpenColor,
                        config.WorldDotSize, config.WorldDotSpacing, config.WorldDotMinPixels);
                _worldDots.End();
                return;
            }
            if (!_dots) _dots = StoatPathfinderDots.Create(name);
            if (!_dots) return;
            // The flat overlay (dotsInWorld off) draws the path only; Time mode's rings are world dots.
            if (_timeMode) { _dots.Hide(); return; }
            var pathColor = _path.Warped ? config.WarpedColor : config.OpenColor;
            _dots.Show(cam, _points, _path.Count, _jumps, _path.JumpCount, _path.Warped ? _path.LoopIndex : -1, pathColor,
                config.DotPixels, config.DotGap, _path.Warped, config.DotDepthMinScale, config.DotDepthFade);
        }

        /// <summary>
        /// Time mode: the laid pair's DETECTION ring (faint) and ORBIT ring round its attractor, in the plane of the hull's
        /// pass. The orbit ring brightens once it has caught the hull, eases toward lime as the quarter-turn nears, and is
        /// lime while a release would pay the whole kick.
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

        void HideDots()
        {
            if (_dots) _dots.Hide();
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
            _path = default;
            _wasWarped = false;
            HideDots();
            ClearForesight();
        }

        void OnDestroy()
        {
            if (_dots) Destroy(_dots.transform.root.gameObject);
            _worldDots?.Dispose();
            _worldDots = null;
            ClearForesight();
        }
    }
}
