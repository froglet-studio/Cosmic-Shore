using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The racing AI on one vessel: lifecycle, sensing, and actuation around a
    /// <see cref="SkimRaceDriver"/>. Built for Skim Race and named for it; WHAT it races for -
    /// the course, the target, the progress count - is a <see cref="SkimRaceObjective"/>, so the
    /// same pilot flies Skim Race's crystals and Regatta's rings.
    ///
    /// <b>Lifecycle.</b> Bound once per AI vessel (<see cref="Bind"/>, by
    /// <see cref="SkimRaceAIDeployment"/>). It holds neutral input and does nothing until the
    /// race OFFICIALLY starts — <c>GameDataSO.IsTurnRunning</c> rising, which is the countdown
    /// ending and control being handed to every pilot — and goes back to neutral the moment the
    /// turn ends. A replay reloads the scene, which destroys this component with the vessel, so
    /// every race starts from a fresh instance; the in-place <see cref="ResetRace"/> exists for a
    /// turn that restarts without a reload.
    ///
    /// <b>Input-only contract (do not relax).</b> Actuation is the vessel's <c>IInputStatus</c>
    /// sticks — the same four channels every dual-stick input strategy writes — its left trigger
    /// (the drift's depth, see <see cref="DriftTriggerPull"/>) and the hull's own bound drift and
    /// Boost Ring controls through <c>PerformShipControllerActions</c>. It reads the vessel's
    /// pose, speed, boost, the commanded rotation and the authoritative crystal registry; it never
    /// writes a transform, speed, course, rigidbody, crystal, score or timer.
    ///
    /// <b>AIPilot.</b> The platform autopilot writes the same sticks every frame, so it is stopped
    /// and disabled for as long as this pilot owns the vessel, and re-disabled if anything turns
    /// it back on.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public class SkimRacePilot : MonoBehaviour
    {
        IVessel _vessel;
        IVesselStatus _status;
        GameDataSO _gameData;
        SkimRaceAIConfigSO _config;
        SkimRaceDriver _driver;
        SkimRaceCourse _course;
        SkimRaceObjective _objective;
        AIPilot _aiPilot;

        bool _bound;
        bool _raceActive;
        float _raceStart;
        float _nextCourseAttempt;
        int _courseHint = -1;
        // The target's OWN projection hint. Projecting the crystal from the vessel's hint searches only
        // +-24 segments (~+-288 u) around the vessel and accepts a windowed answer within 150 u, so a
        // crystal further ahead (every anchor gap on these tracks is 186-646 u) could match the end of
        // the window and give a wrong TargetAheadOnCourse. A new target starts with a full search.
        int _targetHint = -1;
        int _targetHintId;
        Vector3 _targetHintPos;   // a crystal object may be re-used at its next anchor: a move is a new target too
        float _nextDecision;
        SkimRaceAction _held;

        // drift actuation
        InputEvents _driftInput;   // the control the held drift was pressed on - released on the same one
        bool _driftHeld;

        /// <summary>
        /// How far the pilot pulls the left trigger while its drift is held: all the way. A
        /// deliberate choice, and the only one that does not make the race depend on the host's
        /// hardware. On a PAD device the Squirrel's drift is analog -
        /// <c>VesselTransformer.GetTriggerSum</c> scales it by <c>LeftTriggerAnalog</c> - while on
        /// every other device (keyboard, mouse, touch) a started drift is simply full depth. An AI
        /// has no physical trigger, so before this the same pilot drifted at depth 0 (inert, but
        /// still reporting <c>IsDrifting</c>) on a PC with a controller plugged in and at full
        /// depth on one without. Full pull is the depth every device agrees on, it is what the
        /// driver's on/off drift decision means (enter at a sharp heading error, leave once
        /// straightened), and it is a human's buried trigger - inside the input-only contract.
        /// Not a config field for the same reason: a partial pull would only take effect on a pad.
        /// </summary>
        const float DriftTriggerPull = 1f;

        // sensing state
        Vector3 _lastForward;
        float _lastCollectionTime;
        int _lastCollected;

        public bool IsBound => _bound;
        public bool RaceActive => _raceActive;
        public SkimRaceAIConfigSO Config => _config;
        public SkimRaceDriver Driver => _driver;
        public SkimRaceCourse Course => _course;
        public SkimRaceObjective Objective => _objective;
        public SkimRaceObservation LastObservation { get; private set; }
        public SkimRaceAction LastAction => _held;
        public IVessel Vessel => _vessel;

        /// <summary>Raised on the frame the race starts for this pilot (race time 0).</summary>
        public event System.Action<SkimRacePilot> RaceStarted;

        /// <summary>Bind to <paramref name="vessel"/>. <paramref name="objective"/> null = this
        /// match's mode objective (<see cref="SkimRaceObjective.For"/>), which is Skim Race's
        /// crystals when no mode claims it.</summary>
        public void Bind(IVessel vessel, GameDataSO gameData, SkimRaceAIConfigSO config,
                         SkimRaceObjective objective = null)
        {
            _vessel = vessel;
            _status = vessel?.VesselStatus;
            _gameData = gameData;
            _config = config != null ? config : SkimRaceAIConfigSO.LoadDefault();
            _driver = new SkimRaceDriver(_config);
            _objective = objective ?? SkimRaceObjective.For(gameData) ?? new CrystalTrackObjective(gameData);
            _course = null;
            _aiPilot = _status?.AIPilot;
            _bound = _vessel != null && _status != null && _gameData != null;
            SuppressOtherPilots();
            WriteNeutral();
        }

        /// <summary>
        /// This seat's lane: its rank among the AI seats in the race, ordered by domain then name -
        /// public facts every machine agrees on. Lanes skim at different heights so one AI never
        /// flies at the height of another AI's trail rails.
        /// </summary>
        int ResolveLane()
        {
            var me = _status?.Player;
            if (me == null || _gameData?.Players == null) return 0;
            int lane = 0;
            foreach (var p in _gameData.Players)
            {
                if (p == null || p == me || !p.IsInitializedAsAI) continue;
                int c = ((int)p.Domain).CompareTo((int)me.Domain);
                if (c < 0 || (c == 0 && string.CompareOrdinal(p.Name, me.Name) < 0)) lane++;
            }
            return lane;
        }

        /// <summary>Clear all per-race state and hold neutral input.</summary>
        public void ResetRace()
        {
            _raceActive = false;
            _driver?.Reset();
            _courseHint = -1;
            _targetHint = -1;
            _nextDecision = 0f;
            _held = SkimRaceAction.Neutral;
            _lastCollected = 0;
            _lastCollectionTime = 0f;
            _objective?.Reset();
            ReleaseDrift();
            WriteNeutral();
        }

        void Update()
        {
            if (!_bound) return;
            if (_vessel == null || _status == null || _status.InputStatus == null) { _bound = false; return; }

            // A human took this hull (arena pilot swap): the sticks are theirs now. Stand down
            // without writing anything; the departed-pilot path re-binds when the AI gets it back.
            if (_status.Player != null && !_status.Player.IsInitializedAsAI)
            {
                if (_raceActive) { _raceActive = false; ReleaseDrift(); if (_ringPressed) ReleaseRing(); }
                return;
            }

            SuppressOtherPilots();

            bool turnRunning = _gameData.IsTurnRunning;
            if (!turnRunning)
            {
                if (_raceActive) ResetRace();
                return;
            }

            if (!_raceActive)
            {
                ResetRace();
                if (_driver != null) _driver.Lane = ResolveLane();
                _raceActive = true;
                _raceStart = Time.time;
                _lastForward = _vessel.Transform.forward;
                EnsureEditorRaceRecorder();
                RaceStarted?.Invoke(this);
            }

            if (_status.IsStationary) { WriteNeutral(); return; }

            EnsureCourse();

            float now = Time.time - _raceStart;
            var obs = Observe(now);
            LastObservation = obs;

            if (_config.DecisionHz <= 0f || Time.time >= _nextDecision)
            {
                FillObstacles(obs);
                _held = _driver.Decide(obs, _course, now, Time.deltaTime);
                if (_config.DecisionHz > 0f) _nextDecision = Time.time + 1f / _config.DecisionHz;
            }

            Apply(_held);
        }

        static string s_manualSession;
        static int s_manualRace;

        /// <summary>
        /// In the EDITOR, a race played by hand (no benchmark runner) records itself exactly as a benchmark
        /// race does - finish time, frame time, every seat's crystals and speed, the boost-reset log - to
        /// <c>BenchmarkResults/SkimRaceAI/manual_I&lt;n&gt;_&lt;session&gt;.jsonl</c>. Without this a slow race
        /// played by hand cannot be told apart from a slow AI. One recorder per race (the first pilot to
        /// start creates it); it only reads the game. Never in a player build.
        /// </summary>
        void EnsureEditorRaceRecorder()
        {
            if (!Application.isEditor || SkimRaceBenchmarkRunner.Active != null) return;
            if (_objective == null || !_objective.RecordsManualRaces) return;
            if (FindAnyObjectByType<SkimRaceRaceRecorder>() != null) return;
            int intensity = _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 0;
            s_manualSession ??= System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "BenchmarkResults", "SkimRaceAI");
            var go = new GameObject("[Skim Race Recorder (manual)]");
            var rec = go.AddComponent<SkimRaceRaceRecorder>();
            rec.Configure(_gameData, System.IO.Path.Combine(dir, $"manual_I{intensity}_{s_manualSession}.jsonl"),
                "manual-" + s_manualSession, "", s_manualRace++, SkimRaceRaceRecorder.DefaultLimitSeconds(intensity), 600f);
            rec.TraceFrames = true;
        }

        readonly List<Prism> _nearPrisms = new();

        /// <summary>
        /// What a pilot sees ahead that it must not touch: every live prism near the next
        /// <see cref="SkimRaceAIConfigSO.MassGuardSeconds"/> of flight that is NOT the track
        /// (the track's super-shielded prisms are the racing line) and that the game would let the
        /// hull hit - its own fresh trail is exempt for the same grace the game grants
        /// (<see cref="SelfTrailContactConfigSO.SuppressesHullContact"/>). Read from
        /// <see cref="PrismSpatialIndex"/>, the canonical index of prism mass.
        /// </summary>
        void FillObstacles(in SkimRaceObservation o)
        {
            _driver.Obstacles.Clear();
            if (_config.MassGuardSeconds <= 0f) return;
            var index = PrismSpatialIndex.Instance;
            if (index == null) return;
            float half = o.Speed * _config.MassGuardSeconds * 0.5f;
            Vector3 centre = o.Position + o.Forward * half;
            index.QuerySphere(centre, half + 20f, _nearPrisms);
            for (int i = 0; i < _nearPrisms.Count; i++)
            {
                var prism = _nearPrisms[i];
                if (prism == null || prism.prismProperties == null) continue;
                if (prism.prismProperties.IsSuperShielded) continue;
                if (SelfTrailContactConfigSO.SuppressesHullContact(prism, _status)) continue;
                var t = prism.transform;
                _driver.Obstacles.Add(new SkimRaceObstacle
                {
                    Center = t.position,
                    Rotation = t.rotation,
                    Half = t.lossyScale * 0.5f,
                });
            }
        }

        void LateUpdate()
        {
            if (_bound) SuppressOtherPilots();
        }

        void OnDisable()
        {
            ReleaseDrift();
            if (_ringPressed) ReleaseRing();
            WriteNeutral();
        }

        // ── Sensing ───────────────────────────────────────────────────────

        SkimRaceObservation Observe(float now)
        {
            var t = _vessel.Transform;
            var transformer = _status.VesselTransformer;

            var o = new SkimRaceObservation
            {
                Position = t.position,
                Forward = t.forward,
                Right = t.right,
                Up = t.up,
                CommandedForward = transformer != null ? transformer.CommandedRotation * Vector3.forward : t.forward,
                Rotation = t.rotation,
                CommandedRotation = transformer != null ? transformer.CommandedRotation : t.rotation,
                Speed = _status.Speed,
                Velocity = _status.Course * _status.Speed,
                BoostMultiplier = _status.IsBoosting ? _status.BoostMultiplier : 1f,
                MaxBoost = transformer != null ? transformer.MaxBoost : 5f,
                TurnRateDegrees = transformer != null ? transformer.MaxTurnRateDegreesPerSecond : 120f,
                FollowRate = VesselTransformer.RotationFollowRate,
                ThrottleScaler = transformer != null ? transformer.ThrottleScaler : 60f,
                IsDrifting = _status.IsDrifting,
                RaceTime = now,
            };

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 axis = Vector3.Cross(_lastForward, o.Forward);
            float ang = Vector3.Angle(_lastForward, o.Forward) * Mathf.Deg2Rad;
            o.AngularVelocity = axis.sqrMagnitude > 1e-10f ? axis.normalized * (ang / dt) : Vector3.zero;
            _lastForward = o.Forward;

            // Race bookkeeping off the authoritative round stats (crystals or gates - the objective's).
            int collected = _objective.OwnProgress(_status);
            if (collected != _lastCollected)
            {
                _lastCollected = collected;
                _lastCollectionTime = now;
            }
            o.Collected = _objective.SharedProgress(_status);
            o.Remaining = _objective.Remaining(_status);
            o.TimeSinceCollection = now - _lastCollectionTime;
            o.TimeSinceProgress = _driver.TimeSinceProgress;

            // Target: the objective's (this domain's crystal, or this pilot's next ring).
            if (_objective.TryGetTarget(_status, o.Position, out var target))
            {
                o.HasTarget = true;
                o.TargetId = target.Id;
                o.TargetPosition = target.Position;
                o.ToTarget = o.TargetPosition - o.Position;
                o.TargetDistance = o.ToTarget.magnitude;
                Vector3 dir = o.TargetDistance > 1e-3f ? o.ToTarget / o.TargetDistance : o.Forward;
                o.TargetLocalDirection = t.InverseTransformDirection(dir);
                o.TargetAlignment = Vector3.Dot(o.Forward, dir);
                o.TargetRadius = target.Radius;
            }

            if (_course != null)
            {
                o.HasCourse = true;
                o.CourseLength = _course.Length;
                o.CourseProgress = _course.Project(o.Position, ref _courseHint, out _, out o.CourseDistance);
                _course.Sample(o.CourseProgress, out o.CourseTangent, out _);
                if (o.HasTarget)
                {
                    if (o.TargetId != _targetHintId || (o.TargetPosition - _targetHintPos).sqrMagnitude > 1f)
                    {
                        _targetHintId = o.TargetId;
                        _targetHintPos = o.TargetPosition;
                        _targetHint = -1;
                    }
                    float sTarget = _course.Project(o.TargetPosition, ref _targetHint, out _, out _);
                    o.TargetAheadOnCourse = _course.Ahead(o.CourseProgress, sTarget);
                }
            }

            o.Sanitize();
            return o;
        }

        void EnsureCourse()
        {
            if (_course != null || Time.unscaledTime < _nextCourseAttempt) return;
            _nextCourseAttempt = Time.unscaledTime + 0.5f;
            if (_objective.TryBuildCourse(_status, out var course))
            {
                _course = course;
                _courseHint = -1;
                _targetHint = -1;
                CSDebug.LogVerbose(CSLogChannel.AITraining,
                    $"[SkimRacePilot] {_status.PlayerName}: course built ({course.Count} prisms, {course.Length:F0} u).");
            }
        }

        // ── Actuation ─────────────────────────────────────────────────────

        void Apply(SkimRaceAction a)
        {
            // Clamped HERE, at the actuation point, whatever the policy produced: the AI's only
            // authority is the human stick/trigger range (see check_ai_no_state_writes.py).
            a = a.Clamped();
            var input = _status.InputStatus;
            input.XSum = a.Yaw;
            input.YSum = a.Pitch;
            input.YDiff = a.Roll;
            input.XDiff = a.Throttle;

            if (a.Drift && !_driftHeld) PressDrift();
            else if (!a.Drift && _driftHeld) ReleaseDrift();
            // Written every frame like the sticks, so a neutral frame (a stationary hull) cannot
            // leave a held drift at depth 0 when flight resumes.
            input.LeftTriggerAnalog = _driftHeld ? DriftTriggerPull : 0f;

            if (_ringPressed) { ReleaseRing(); }
            if (a.Ring) PressRing();
        }

        void WriteNeutral()
        {
            var input = _status?.InputStatus;
            if (input == null) return;
            input.XSum = 0f;
            input.YSum = 0f;
            input.YDiff = 0f;
            input.XDiff = 0f;
            input.LeftTriggerAnalog = 0f;
        }

        // The drift and the Boost Ring are asked for by ability TYPE at every press, never cached:
        // the handler answers for the device the hull is being driven by (on the Squirrel both
        // abilities live only in the touch and gamepad override maps, on different controls), and
        // the answer is only good for that device. A press is a handful of dictionary probes at
        // decision rate, and each release goes to the control its own press used.

        void PressDrift()
        {
            var handler = _status.ActionHandler;
            if (handler == null || !handler.TryGetInputForAction<DriftActionSO>(out _driftInput)) return;
            _vessel.PerformShipControllerActions(_driftInput);
            _driftHeld = true;
        }

        bool _ringPressed;
        InputEvents _ringInput;

        /// <summary>The Boost Ring through the hull's own bound control (press now, release next frame).</summary>
        void PressRing()
        {
            var handler = _status.ActionHandler;
            if (handler == null || !handler.TryGetInputForAction<SquirrelTubeActionSO>(out _ringInput)) return;
            _vessel.PerformShipControllerActions(_ringInput);
            _ringPressed = true;
        }

        void ReleaseRing()
        {
            _ringPressed = false;
            if (_vessel != null) _vessel.StopShipControllerActions(_ringInput);
        }

        void ReleaseDrift()
        {
            if (!_driftHeld) return;
            _driftHeld = false;
            if (_vessel != null)
                _vessel.StopShipControllerActions(_driftInput);
        }

        /// <summary>Stop the platform autopilot (and a deployed training pilot) from writing the sticks.</summary>
        void SuppressOtherPilots()
        {
            if (_aiPilot == null) _aiPilot = _status?.AIPilot;
            if (_aiPilot != null && (_aiPilot.enabled || _aiPilot.AutoPilotEnabled))
            {
                if (_aiPilot.AutoPilotEnabled) _aiPilot.StopAIPilot();
                _aiPilot.enabled = false;
            }
        }
    }
}
