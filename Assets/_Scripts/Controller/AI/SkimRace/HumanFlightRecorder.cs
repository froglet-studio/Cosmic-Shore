#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI training recorder: records how a HUMAN flies, in every game mode and every vessel, so the AIs can learn
    /// from it (Docs/AI_SYSTEM/AI_TRAINING_PLAN.md L3; Docs/SKIM_RACE_AI.md §16). AUTOMATIC (Yash, 2026-10-15/16): no
    /// button. Recording starts when a match's turn starts (Go) and stops when it ends (the Play Again menu) or when Play
    /// stops. Single player for now; recording every online player's inputs is later.
    ///
    /// <para>Everything <c>Docs/AI_SYSTEM/SITUATION_RECORDING_PLAN.md</c> §2.1-2.2 asks for (and the camera of Caleb's
    /// V1). Ten times a second, for the local player's own vessel: position, heading and up, speed, boost, the four
    /// stick axes, the throttle and both triggers (the same input an AI writes), the course, rings threaded; the hull
    /// state (velocity, pitch / yaw / roll rates, boost charge, the boosting / discharging / drifting / slowed / attached
    /// flags, the four element levels); who was flying (<c>held</c> = 1 while the AI holds the hull through the You / AI
    /// toggle - recorded, and every learner skips it); the camera the pilot looked through (position, forward, field of
    /// view, mode); every other pilot's position, velocity and rings; and the nearest crystals. Once per race: every
    /// ring (order, position, axis, mouth radius, laps), every rail (its prisms and up axes, contact shell, owning
    /// domain), the rival difficulty, the build and the course fingerprint, each rail prism destroyed or restored
    /// while the race ran (<c>railEvents</c>), and the pilot's own button presses (<c>buttonEvents</c>). The hull, mode
    /// and race target are named when recording starts, and one last sample is taken on the frame the race ends, so the
    /// ring that ended it is counted. The rails come from the arena
    /// (<see cref="RegattaCourseSource.TryResolveLiveRails"/>), copied when recording starts, so
    /// <c>Tools/Build/learn_from_flights.py</c> and <c>situation_features.py</c> can measure the flight against them.</para>
    ///
    /// <para>One <c>flight_&lt;Mode&gt;_&lt;Vessel&gt;_&lt;time&gt;.json</c> per match (schema <c>cosmic-shore.flight/3</c>,
    /// with the mode, the vessel and the training type) in <c>&lt;Diagnostics folder&gt;/Flights/</c>. After Play, the editor
    /// sends each new record to the person's <c>bugledger/&lt;user&gt;</c> branch without a question (since 2026-10-16;
    /// <c>BugHuntAutoSend</c>, <c>BugHuntReports</c> kind <c>flights</c>) and logs one Console line naming the vessel and
    /// the mode. A match that could not be stored says why in one Console warning. The recorder compiles in editor and
    /// development builds; only the automatic send is editor-only, so a development build's records stay on its machine.
    /// Players get their own consent question before any of this ships.</para>
    /// </summary>
    public sealed class HumanFlightRecorder : MonoBehaviour
    {
        public const string CommandName = "flightrec";
        public const string Schema = "cosmic-shore.flight/3";   // /3: the situation (SITUATION_RECORDING_PLAN.md §2); columns are read by name, so additions keep /3

        /// <summary>The kind of AI training a record serves, written into the record's <c>training</c> field.</summary>
        public const string TrainingType = "imitation: learning from your flying";
        const float SampleHz = 10f;
        const float LookEvery = 0.5f;
        const int MaxCrystals = 12;   // the nearest crystals each sample (x, y, z, element)

        // The "state" column's bits (written into the file's header as "stateBits").
        const int StateBoosting = 1, StateDischarging = 2, StateDrifting = 4, StateSlowed = 8, StateAttached = 16, StateStationary = 32;

        /// <summary>The flights folder: beside the diagnostics captures, so Store AI record finds them.</summary>
        public static string Folder => Path.Combine(DiagnosticsHUD.OutputDirectory, "Flights");

        /// <summary>True while a race is being recorded.</summary>
        public static bool Recording => s_instance && s_instance._recording;

        /// <summary>What the recorder is doing, in words (HUD <c>flightrec</c>).</summary>
        public static string Status =>
            !Application.isPlaying ? "waiting for Play"
            : !s_instance ? "not started (no recorder object)"
            : s_instance._recording ? $"RECORDING {Time.time - s_instance._raceStart:0}s ({s_instance._samples.Count} samples)"
            : s_instance._waiting;

        /// <summary>The last file written.</summary>
        public static string LastFile { get; private set; }

        /// <summary>Flights saved in <see cref="Folder"/> (counted at most every two seconds).</summary>
        public static int SavedCount
        {
            get
            {
                float now = Time.realtimeSinceStartup;
                if (now < s_countAt) return s_count;
                s_countAt = now + 2f;
                try { s_count = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "flight_*.json").Length : 0; }
                catch (Exception) { s_count = 0; }
                return s_count;
            }
        }
        static int s_count;
        static float s_countAt = -1f;
        static HumanFlightRecorder s_instance;

        struct Sample
        {
            public float T, Speed, Boost, XSum, YSum, XDiff, YDiff, Throttle, Lt, Rt;
            public Vector3 Pos, Fwd, Up, Course;
            public int Rings;
            public int RivalStart, RivalCount;   // into _rivals: x, y, z, rings per rival; into _rivalVel: vx, vy, vz per rival
            public Vector3 Vel, Rates;           // velocity (course x speed); pitch, yaw, roll rates in degrees / s
            public float Charge;                 // charged-boost charge
            public int State;                    // State* bits
            public float LvC, LvM, LvS, LvT;     // element levels, 0..1 (charge, mass, space, time)
            public float M0, M1, M2, M3;         // the hull's first four resource meters (CurrentAmount), -1 if absent
            public int TimeMode;                 // the Stoat's mode: 1 Time (catch sling), 0 Mass (field dipole), -1 another hull
            public bool Held;                    // the AI held the hull (the You / AI toggle)
            public Vector3 CamPos, CamFwd, CamUp;    // forward + up = the camera's full rotation
            public float Fov, Aspect;            // vertical field of view (degrees), width / height
            public int CamMode;                  // CameraSettingsSO.mode of the active player camera, -1 if none
            public bool RearView;                // look-behind (CustomCameraController.RearView)
            public int CrystalStart, CrystalCount;   // into _crystals: x, y, z, element per crystal
        }

        /// <summary>One rail, copied when recording starts: its prisms (watched for holes), their positions and up axes.</summary>
        sealed class RailCopy
        {
            public string Domain;
            public Vector3 ShellHalf;
            public readonly List<Prism> Prisms = new(512);
            public readonly List<Vector3> Points = new(512), Ups = new(512);
            public readonly List<bool> Dead = new(512);
        }

        readonly List<Sample> _samples = new(2048);
        readonly List<float> _rivals = new(2048 * 16);
        readonly List<float> _rivalVel = new(2048 * 12);
        readonly List<string> _rivalHulls = new(8);
        readonly List<float> _crystals = new(2048 * MaxCrystals * 4);
        readonly List<float> _crystalPick = new(MaxCrystals * 2);
        readonly List<RailCopy> _rails = new(4);
        readonly List<float> _railEvents = new(256);   // t, rail, prism index, 1 destroyed / 0 restored
        readonly List<float> _ringData = new(64 * 8);   // index, x, y, z, ax, ay, az, radius per ring
        // The pilot's own button presses and releases (InputEvents: abilities, the sticks' actions, flip): t, event, 1 / 0.
        // Only the local pilot's input strategies raise these events, so an AI's presses never arrive here.
        readonly List<float> _buttonEvents = new(512);
        // The hull's resource meters, named once (the Dolphin's drift charge, ammo, ...), and which one its charged
        // boost spends (ChargeBoostActionSO.BoostResourceIndex; -1 when it has none): what the Dolphin AI decides on.
        readonly List<string> _meterNames = new(4);
        int _boostMeter = -1;
        // The Stoat's mode switch (its triggers are the field dipole in Mass mode, the catch sling in Time mode), found
        // once per race; null on every other hull.
        StoatModeExecutor _stoatMode;
        ScriptableEventInputEvents _pressed, _released;
        GameDataSO _gameData;
        Player _player;
        float _nextSample, _nextLook, _raceStart, _frameSum, _prevSampleT;
        Quaternion _prevRot;
        bool _havePrevRot;
        int _frames, _startRings, _ownRail = -1, _courseSeed, _courseIntensity;
        bool _recording, _turnWasRunning;
        // Named when recording starts and kept: by the time the turn ends the hull may already be despawned and the game
        // data reset (leaving to the menu mid-race wrote a flight with hull "Unknown" and raceTarget 0, 2026-10-16).
        string _hullName = "Unknown", _modeName = "Unknown", _domainName = "";
        int _target, _intensity;
        string _waiting = "waiting for a match", _railWhy = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            DiagnosticsHUD.RegisterCommand(CommandName, Handle);
            if (s_instance) return;
            // A plain (hidden-in-hierarchy) object: a DontSave one is not guaranteed its Update in every editor setup.
            var go = new GameObject("[Human Flight Recorder]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<HumanFlightRecorder>();
        }

        /// <summary>The console command: what the recorder is doing, how many flights are saved, and where.</summary>
        public static string Handle(string[] args)
        {
            string last = string.IsNullOrEmpty(LastFile) ? "" : $" | last: {Path.GetFileName(LastFile)}";
            return $"AI training recorder (automatic, every mode): {Status} | {SavedCount} stored in {Folder}{last}";
        }

        void Update()
        {
            float now = Time.time;
            if (now >= _nextLook && !_recording)
            {
                _nextLook = now + LookEvery;
                Look();
            }
            if (!_gameData) return;

            // Every game mode: an AI training record of whichever vessel the local player flies by hand.
            bool running = _gameData.IsTurnRunning;
            if (!running)
            {
                if (_recording)
                {
                    // The ring that ended the race is threaded on this frame or the last; samples run at 10 Hz, so without
                    // this last one a finished race was written one ring short (23 / 24, Regatta 2026-10-16).
                    TakeSample(now);
                    Finish(_target > 0 && LastRings() >= _target ? "race over" : "race left before the finish");
                }
                _turnWasRunning = false;
                return;
            }

            if (!_turnWasRunning)
            {
                _turnWasRunning = true;
                Begin(now);
            }
            if (!_recording)
            {
                if (now < _nextLook) return;
                _nextLook = now + LookEvery;
                // Development diagnostics switched off (DevDiagnostics): the build plays like a release one, which
                // records nothing. A race already being recorded when they go off still finishes and is stored.
                if (!DevDiagnostics.Enabled) { _waiting = "development diagnostics are off"; return; }
                if (!TryFindLocalHuman()) { _waiting = "waiting for your hull (AI-only races are not recorded)"; return; }
                _recording = true;
                _startRings = _player.RoundStats?.SwitchesThreaded ?? 0;
                NameRace();
                ListenToButtons();
                CopyCourse();
            }

            _frames++;
            _frameSum += Time.unscaledDeltaTime;
            if (now < _nextSample) return;
            _nextSample = now + 1f / SampleHz;
            TakeSample(now);
        }

        /// <summary>Find the match's game data: the asset whose local player is set (else one with players).</summary>
        void Look()
        {
            if (_gameData && _gameData.Players != null && _gameData.Players.Count > 0) return;
            _gameData = null;
            var all = Resources.FindObjectsOfTypeAll<GameDataSO>();
            for (int i = 0; i < all.Length && !_gameData; i++)
                if (all[i] && all[i].LocalPlayer != null) _gameData = all[i];
            for (int i = 0; i < all.Length && !_gameData; i++)
                if (all[i] && all[i].Players != null && all[i].Players.Count > 0) _gameData = all[i];
            _waiting = !_gameData ? "waiting for a match" : $"waiting for the {_gameData.GameMode} race to start";
        }

        void Begin(float now)
        {
            ClearRace();
            _player = null;
            _recording = false;
            _frames = 0;
            _frameSum = 0f;
            _raceStart = now;
            _nextSample = now;
            _nextLook = now;
        }

        void NameRace()
        {
            if (_player && _player.Vessel?.VesselStatus != null) _hullName = _player.Vessel.VesselStatus.VesselType.ToString();
            if (_player) _domainName = _player.Domain.ToString();
            if (!_gameData) return;
            _modeName = _gameData.GameMode.ToString();
            if (_gameData.SwitchTargetCount > 0) _target = _gameData.SwitchTargetCount;
            if (_gameData.SelectedIntensity != null && _gameData.SelectedIntensity.Value > 0) _intensity = _gameData.SelectedIntensity.Value;
        }

        void ListenToButtons()
        {
            StopListening();
            var input = _player && _player.Vessel?.VesselStatus != null ? _player.Vessel.VesselStatus.InputStatus : null;
            if (input == null) return;
            _pressed = input.OnButtonPressed;
            _released = input.OnButtonReleased;
            if (_pressed) _pressed.OnRaised += OnPressed;
            if (_released) _released.OnRaised += OnReleased;
        }

        void StopListening()
        {
            if (_pressed) _pressed.OnRaised -= OnPressed;
            if (_released) _released.OnRaised -= OnReleased;
            _pressed = _released = null;
        }

        void OnPressed(InputEvents e) => RecordButton(e, 1);
        void OnReleased(InputEvents e) => RecordButton(e, 0);

        void RecordButton(InputEvents e, int down)
        {
            // IdleAction arrives every few frames while both thumbs are lifted; it is the "no input" state, not a press.
            if (!_recording || e == InputEvents.IdleAction || PilotControl.IsHeldByAI(_player)) return;
            _buttonEvents.Add(Time.time - _raceStart); _buttonEvents.Add((int)e); _buttonEvents.Add(down);
        }

        int LastRings() => _samples.Count > 0 ? _samples[_samples.Count - 1].Rings : 0;

        bool TryFindLocalHuman()
        {
            if (_gameData.LocalPlayer is Player local && local && !local.IsInitializedAsAI && local.Vessel != null)
            {
                _player = local;
                return true;
            }
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] is Player p && p && p.IsLocalUser && !p.IsInitializedAsAI && p.Vessel != null)
                {
                    _player = p;
                    return true;
                }
            }
            return false;
        }

        void TakeSample(float now)
        {
            if (!_player || _player.Vessel?.VesselStatus == null) return;
            var status = _player.Vessel.VesselStatus;
            var input = status.InputStatus;
            var tf = _player.Vessel.Transform;
            if (input == null || !tf) return;
            float t = now - _raceStart;
            NameRace();
            int rivalStart = _rivals.Count, crystalStart = _crystals.Count;
            RecordRivals();
            RecordCrystals(tf.position);
            RecordRailEvents(t);

            // Turn rates from the rotation since the last sample, in the hull's own frame (x pitch, y yaw, z roll).
            Quaternion rot = tf.rotation;
            Vector3 rates = Vector3.zero;
            float dt = t - _prevSampleT;
            if (_havePrevRot && dt > 1e-3f)
            {
                (Quaternion.Inverse(_prevRot) * rot).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x)) rates = axis * (angle / dt);
            }
            _prevRot = rot;
            _prevSampleT = t;
            _havePrevRot = true;

            NameMeters(status);
            var resources = status.ResourceSystem;
            int state = (status.IsBoosting ? StateBoosting : 0) | (status.IsChargedBoostDischarging ? StateDischarging : 0)
                        | (status.IsDrifting ? StateDrifting : 0) | (status.IsSlowed ? StateSlowed : 0)
                        | (status.IsAttached ? StateAttached : 0) | (status.IsStationary ? StateStationary : 0);
            ReadCamera(out Vector3 camPos, out Vector3 camFwd, out Vector3 camUp, out float fov, out float aspect, out int camMode, out bool rearView);

            _samples.Add(new Sample
            {
                T = t,
                Pos = tf.position, Fwd = tf.forward, Up = tf.up, Course = status.Course,
                Speed = status.Speed, Boost = status.BoostMultiplier,
                XSum = input.XSum, YSum = input.YSum, XDiff = input.XDiff, YDiff = input.YDiff,
                Throttle = input.Throttle, Lt = input.LeftTriggerAnalog, Rt = input.RightTriggerAnalog,
                Rings = (_player.RoundStats?.SwitchesThreaded ?? 0) - _startRings,
                RivalStart = rivalStart, RivalCount = (_rivals.Count - rivalStart) / 4,
                Vel = status.Course * status.Speed, Rates = rates,
                Charge = status.ChargedBoostCharge, State = state,
                LvC = Level(resources, Element.Charge), LvM = Level(resources, Element.Mass),
                LvS = Level(resources, Element.Space), LvT = Level(resources, Element.Time),
                M0 = Meter(resources, 0), M1 = Meter(resources, 1), M2 = Meter(resources, 2), M3 = Meter(resources, 3),
                TimeMode = _stoatMode ? (_stoatMode.IsTimeMode ? 1 : 0) : -1,
                // The AI holding this hull (the Third Eye's You / AI toggle) is not the human's flight: kept, marked,
                // and skipped by every learner (situation_features.py, situation.js, learn_from_flights.py).
                Held = PilotControl.IsHeldByAI(_player),
                CamPos = camPos, CamFwd = camFwd, CamUp = camUp, Fov = fov, Aspect = aspect, CamMode = camMode, RearView = rearView,
                CrystalStart = crystalStart, CrystalCount = (_crystals.Count - crystalStart) / 4,
            });
        }

        static float Meter(ResourceSystem resources, int index)
        {
            if (!resources || resources.Resources == null || (uint)index >= (uint)resources.Resources.Count) return -1f;
            var res = resources.Resources[index];
            return res != null ? res.CurrentAmount : -1f;
        }

        /// <summary>Name the hull's meters once per race (the first sample).</summary>
        void NameMeters(IVesselStatus status)
        {
            if (_meterNames.Count > 0) return;
            var resources = status.ResourceSystem;
            if (resources && resources.Resources != null)
                for (int i = 0; i < resources.Resources.Count && i < 4; i++)
                    _meterNames.Add(resources.Resources[i] != null ? resources.Resources[i].Name ?? "" : "");
            if (_meterNames.Count == 0) _meterNames.Add("");
            _boostMeter = status.ActionHandler != null && status.ActionHandler.TryGetBoundAction<ChargeBoostActionSO>(out var charge, out _)
                ? charge.BoostResourceIndex : -1;
            var registry = _player && _player.Vessel?.Transform ? _player.Vessel.Transform.GetComponentInChildren<ActionExecutorRegistry>() : null;
            _stoatMode = registry ? registry.Get<StoatModeExecutor>() : null;
        }

        static float Level(ResourceSystem resources, Element element)
        {
            if (!resources) return 0f;
            try { return resources.GetNormalizedLevel(element); }
            catch (Exception) { return 0f; }
        }

        /// <summary>The camera the pilot was looking through (Caleb's V1: learn from the pilot's VIEW): its pose, vertical
        /// field of view, aspect, mode and look-behind, so <c>situation_features.py</c> can redraw the screen offline.
        /// <see cref="CameraManager.ViewCamera"/> resolves once a frame; nothing here searches or allocates.</summary>
        static void ReadCamera(out Vector3 pos, out Vector3 fwd, out Vector3 up, out float fov, out float aspect, out int mode, out bool rearView)
        {
            pos = fwd = up = Vector3.zero;
            fov = aspect = 0f;
            mode = -1;
            rearView = false;
            var cam = CameraManager.ViewCamera;
            if (!cam) return;
            var tr = cam.transform;
            pos = tr.position;
            fwd = tr.forward;
            up = tr.up;
            fov = cam.fieldOfView;
            aspect = cam.aspect;
            var manager = CameraManager.Instance;
            if (manager && manager.GetActiveController() is CustomCameraController controller && controller)
            {
                rearView = controller.RearView;
                if (controller.CurrentSettings) mode = (int)controller.CurrentSettings.mode;
            }
        }

        /// <summary>The nearest <see cref="MaxCrystals"/> crystals to the pilot: position and element.</summary>
        void RecordCrystals(Vector3 at)
        {
            var active = Crystal.Active;
            if (active == null) return;
            // Keep the nearest few: _crystalPick holds (distance², index) pairs, sorted by distance.
            _crystalPick.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                var crystal = active[i];
                if (!crystal) continue;
                float d2 = (crystal.transform.position - at).sqrMagnitude;
                int n = _crystalPick.Count / 2;
                if (n == MaxCrystals && d2 >= _crystalPick[_crystalPick.Count - 2]) continue;
                int k = 0;
                while (k < n && _crystalPick[k * 2] <= d2) k++;
                _crystalPick.Insert(k * 2, i);
                _crystalPick.Insert(k * 2, d2);
                if (_crystalPick.Count > MaxCrystals * 2) _crystalPick.RemoveRange(MaxCrystals * 2, 2);
            }
            for (int k = 0; k < _crystalPick.Count; k += 2)
            {
                var crystal = active[(int)_crystalPick[k + 1]];
                Vector3 p = crystal.transform.position;
                _crystals.Add(p.x); _crystals.Add(p.y); _crystals.Add(p.z);
                _crystals.Add((int)crystal.crystalProperties.Element);
            }
        }

        /// <summary>Each rail prism destroyed (or restored) since the last sample: [t, rail, prism index, 1 / 0].</summary>
        void RecordRailEvents(float t)
        {
            for (int r = 0; r < _rails.Count; r++)
            {
                var rail = _rails[r];
                for (int i = 0; i < rail.Prisms.Count; i++)
                {
                    var prism = rail.Prisms[i];
                    bool dead = !prism || prism.destroyed;
                    if (dead == rail.Dead[i]) continue;
                    rail.Dead[i] = dead;
                    _railEvents.Add(t); _railEvents.Add(r); _railEvents.Add(i); _railEvents.Add(dead ? 1 : 0);
                }
            }
        }

        /// <summary>The race's static world, copied when recording starts (every prism is still whole then): the rings
        /// and, where the mode lays them (Regatta), every rail.</summary>
        void CopyCourse()
        {
            try
            {
                var rings = FindObjectsByType<RaceGateRing>(FindObjectsSortMode.None);
                Array.Sort(rings, (a, b) => a.Index.CompareTo(b.Index));
                foreach (var ring in rings)
                {
                    if (!ring) continue;
                    Vector3 p = ring.transform.position, a = ring.Axis;
                    _ringData.Add(ring.Index);
                    _ringData.Add(p.x); _ringData.Add(p.y); _ringData.Add(p.z);
                    _ringData.Add(a.x); _ringData.Add(a.y); _ringData.Add(a.z);
                    _ringData.Add(ring.Radius);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HumanFlightRecorder] rings not copied: {e.GetType().Name}: {e.Message}");
            }
            if (_gameData && _gameData.GameMode == GameModes.Regatta) TryCopyRails();
        }

        /// <summary>Every rail in the arena: each prism's position and up axis, the contact shell's half-size (authored
        /// scale x 1.5) and the domain that owns it. The pilot's own rail is the one in their domain colour (else the first).</summary>
        bool TryCopyRails()
        {
            _rails.Clear();
            _ownRail = -1;
            try
            {
                if (!RegattaCourseSource.TryResolveLiveRails(out var rails, out _railWhy)) return false;
                _courseSeed = rails.CourseSeed;
                _courseIntensity = rails.CourseIntensity;
                var trails = rails.GetTrails();
                if (trails == null || trails.Count == 0) { _railWhy = "the rails have no lanes"; return false; }
                Domains domain = _player ? _player.Domain : Domains.Jade;
                for (int t = 0; t < trails.Count; t++)
                {
                    var list = trails[t]?.TrailList;
                    if (list == null) continue;
                    var copy = new RailCopy();
                    for (int i = 0; i < list.Count; i++)
                    {
                        var prism = list[i];
                        if (!prism) continue;
                        var tr = prism.transform;
                        if (copy.Prisms.Count == 0)
                        {
                            copy.Domain = prism.Domain.ToString();
                            Vector3 scale = prism.TargetScale.sqrMagnitude > 1e-8f
                                ? (tr.parent ? Vector3.Scale(prism.TargetScale, tr.parent.lossyScale) : prism.TargetScale)
                                : tr.lossyScale;
                            copy.ShellHalf = scale * SkimRaceShell.SuperShieldHalfScale;
                            if (_ownRail < 0 && prism.Domain == domain) _ownRail = _rails.Count;
                        }
                        copy.Prisms.Add(prism);
                        copy.Points.Add(tr.position);
                        copy.Ups.Add(tr.up);
                        copy.Dead.Add(prism.destroyed);
                    }
                    if (copy.Prisms.Count >= 3) _rails.Add(copy);
                }
                if (_rails.Count == 0) { _railWhy = "the rails have no prisms"; return false; }
                if (_ownRail < 0) _ownRail = 0;
                _railWhy = "";
                return true;
            }
            catch (Exception e)
            {
                _rails.Clear();
                _railWhy = $"{e.GetType().Name}: {e.Message}";
                return false;
            }
        }

        void ClearRace()
        {
            _samples.Clear();
            _rivals.Clear();
            _rivalVel.Clear();
            _rivalHulls.Clear();
            _crystals.Clear();
            _rails.Clear();
            _railEvents.Clear();
            _buttonEvents.Clear();
            _meterNames.Clear();
            _boostMeter = -1;
            _stoatMode = null;
            StopListening();
            _ringData.Clear();
            _ownRail = -1;
            _courseSeed = _courseIntensity = 0;
            _railWhy = "";
            _havePrevRot = false;
            _prevSampleT = 0f;
            _hullName = _modeName = "Unknown";
            _domainName = "";
            _target = _intensity = 0;
        }

        /// <summary>Every other pilot's hull this sample: position, velocity and rings threaded, in the match's player order (the
        /// rival part of the situation: who is near, who is ahead). Their hull classes are named once, from the first sample.</summary>
        void RecordRivals()
        {
            var players = _gameData ? _gameData.Players : null;
            if (players == null) return;
            bool first = _samples.Count == 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] is not Player p || !p || p == _player) continue;
                var vessel = p.Vessel;
                if (vessel?.VesselStatus == null || !vessel.Transform) continue;
                Vector3 at = vessel.Transform.position, vel = vessel.VesselStatus.Course * vessel.VesselStatus.Speed;
                _rivals.Add(at.x); _rivals.Add(at.y); _rivals.Add(at.z);
                _rivals.Add(p.RoundStats?.SwitchesThreaded ?? 0);
                _rivalVel.Add(vel.x); _rivalVel.Add(vel.y); _rivalVel.Add(vel.z);
                if (first) _rivalHulls.Add(vessel.VesselStatus.VesselType.ToString().ToLowerInvariant());
            }
        }

        // Stopping Play mid-race (or quitting) writes the race so far as an unfinished flight.
        void OnApplicationQuit()
        {
            if (_recording) Finish("play stopped");
        }

        void OnDestroy()
        {
            if (_recording) Finish("play stopped");
            if (s_instance == this) s_instance = null;
        }

        void Finish(string why)
        {
            _recording = false;
            int byHand = 0;
            for (int i = 0; i < _samples.Count; i++) if (!_samples[i].Held) byHand++;
            if (byHand < 2 * SampleHz)
            {
                Debug.LogWarning($"[HumanFlightRecorder] AI training record not stored ({why}): only {byHand} hand-flown samples.");
                ClearRace();
                return;
            }
            try
            {
                Directory.CreateDirectory(Folder);
                string hull = _hullName, mode = _modeName;
                string file = Path.Combine(Folder, $"flight_{mode}_{hull}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(file, Serialize(hull, why));
                LastFile = file;
                s_countAt = -1f;
                CSDebug.LogVerbose(CSLogChannel.AITraining, $"[HumanFlightRecorder] {_samples.Count} samples ({why}) -> {file}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HumanFlightRecorder] AI training record not stored ({why}): {e.GetType().Name}: {e.Message}");
            }
            ClearRace();
        }

        string Serialize(string hull, string why)
        {
            var c = CultureInfo.InvariantCulture;
            // A rail to measure the flight against exists only where the mode lays one (Regatta, for now). Copied when
            // recording started; if it was not there yet, copied now (no hole events then).
            bool regatta = _modeName == nameof(GameModes.Regatta);
            if (regatta && _rails.Count == 0) TryCopyRails();
            bool haveLane = regatta && _rails.Count > 0 && _ownRail >= 0;
            var own = haveLane ? _rails[_ownRail] : null;
            Vector3 shellHalf = own?.ShellHalf ?? Vector3.zero;
            int railPrisms = 0;
            for (int r = 0; r < _rails.Count; r++) railPrisms += _rails[r].Points.Count;
            var sb = new StringBuilder(64 * 1024 + railPrisms * 60 + _samples.Count * 400 + _crystals.Count * 6);
            int target = _target;
            int rings = _samples[_samples.Count - 1].Rings;
            int ringCount = _ringData.Count / 8;
            int intensity = _intensity;
            sb.Append("{\n");
            sb.Append("\"schema\":\"").Append(Schema).Append("\",\n");
            sb.Append("\"mode\":\"").Append(_modeName).Append("\",\"hull\":\"").Append(hull).Append("\",");
            sb.Append("\"source\":\"unity\",\"hullKey\":\"").Append(hull.ToLowerInvariant()).Append("\",");
            sb.Append("\"training\":\"").Append(TrainingType).Append("\",");
            sb.Append("\"domain\":\"").Append(_domainName).Append("\",");
            sb.Append("\"intensity\":").Append(intensity).Append(',');
            sb.Append("\"rivalDifficulty\":\"").Append(_gameData ? _gameData.RequestedAIDifficulty.ToString() : "").Append("\",");
            sb.Append("\"build\":\"").Append(Esc(Application.version)).Append("\",\"unity\":\"").Append(Esc(Application.unityVersion)).Append("\",");
            sb.Append("\"courseSeed\":").Append(_courseSeed).Append(",\"courseIntensity\":").Append(_courseIntensity).Append(',');
            sb.Append("\"date\":\"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", c)).Append("\",");
            sb.Append("\"ended\":\"").Append(why).Append("\",");
            sb.Append("\"raceTarget\":").Append(target).Append(",\"rings\":").Append(rings).Append(',');
            sb.Append("\"laps\":").Append(target > 0 && ringCount > 0 ? Mathf.CeilToInt(target / (float)ringCount) : 0).Append(',');
            sb.Append("\"finished\":").Append(target > 0 && rings >= target ? "true" : "false").Append(',');
            sb.Append("\"raceSeconds\":").Append(F(_samples[_samples.Count - 1].T)).Append(',');
            sb.Append("\"meanFrameMs\":").Append(F(_frames > 0 ? 1000f * _frameSum / _frames : 0f, "0.0")).Append(',');
            sb.Append("\"sampleHz\":").Append(F(SampleHz)).Append(',');
            sb.Append("\"referenceShellHeight\":").Append(F(SkimRaceCourse.ReferenceShellHeight, "0.###")).Append(',');
            sb.Append("\"shellHalf\":[").Append(F(shellHalf.x)).Append(',').Append(F(shellHalf.y)).Append(',').Append(F(shellHalf.z)).Append("],");
            sb.Append("\"laneError\":\"").Append(haveLane ? "" : Esc(_railWhy)).Append("\",\n");
            sb.Append("\"camera\":\"camx..camfz: position and forward, camux..camuz: up, fov: vertical degrees, camAspect: width / height, camRear: look-behind\",\n");
            sb.Append("\"meterNames\":[");
            for (int i = 0; i < _meterNames.Count; i++) sb.Append(i > 0 ? ",\"" : "\"").Append(Esc(_meterNames[i])).Append('"');
            sb.Append("],\"boostMeter\":").Append(_boostMeter).Append(",\n");
            sb.Append("\"stateBits\":{\"boosting\":").Append(StateBoosting).Append(",\"discharging\":").Append(StateDischarging)
              .Append(",\"drifting\":").Append(StateDrifting).Append(",\"slowed\":").Append(StateSlowed)
              .Append(",\"attached\":").Append(StateAttached).Append(",\"stationary\":").Append(StateStationary).Append("},");
            sb.Append("\"crystalElements\":{\"charge\":").Append((int)Element.Charge).Append(",\"mass\":").Append((int)Element.Mass)
              .Append(",\"space\":").Append((int)Element.Space).Append(",\"time\":").Append((int)Element.Time).Append(",\"omni\":").Append((int)Element.Omni).Append("},\n");

            // The pilot's own rail (kept for flight/2 readers) and every rail.
            sb.Append("\"lane\":");
            AppendPrisms(sb, own);
            sb.Append(",\n\"ownRail\":").Append(haveLane ? _ownRail : -1).Append(",\n\"rails\":[");
            for (int r = 0; r < _rails.Count; r++)
            {
                var rail = _rails[r];
                sb.Append(r > 0 ? ",\n{" : "\n{");
                sb.Append("\"domain\":\"").Append(rail.Domain).Append("\",\"shellHalf\":[").Append(F(rail.ShellHalf.x)).Append(',')
                  .Append(F(rail.ShellHalf.y)).Append(',').Append(F(rail.ShellHalf.z)).Append("],\"prisms\":");
                AppendPrisms(sb, rail);
                sb.Append('}');
            }
            sb.Append("],\n");

            // Rings: [order, x, y, z, axis x, y, z, mouth radius].
            sb.Append("\"ringColumns\":[\"index\",\"x\",\"y\",\"z\",\"ax\",\"ay\",\"az\",\"radius\"],\"ringList\":[");
            for (int i = 0; i < ringCount; i++)
            {
                sb.Append(i > 0 ? ",[" : "[");
                for (int k = 0; k < 8; k++)
                {
                    if (k > 0) sb.Append(',');
                    float v = _ringData[i * 8 + k];
                    sb.Append(k == 0 ? ((int)v).ToString(c) : F(v, k >= 4 && k <= 6 ? "0.###" : "0.##"));
                }
                sb.Append(']');
            }
            sb.Append("],\n");

            // Rail prisms destroyed (1) or restored (0) while the race ran: [t, rail, prism index, 1 / 0].
            sb.Append("\"railEvents\":[");
            for (int i = 0; i + 3 < _railEvents.Count; i += 4)
            {
                sb.Append(i > 0 ? ",[" : "[").Append(F(_railEvents[i])).Append(',').Append((int)_railEvents[i + 1]).Append(',')
                  .Append((int)_railEvents[i + 2]).Append(',').Append((int)_railEvents[i + 3]).Append(']');
            }
            sb.Append("],\n");
            // The pilot's button presses: [t, InputEvents value, 1 pressed / 0 released].
            sb.Append("\"buttonNames\":{");
            var names = (InputEvents[])Enum.GetValues(typeof(InputEvents));
            for (int i = 0; i < names.Length; i++)
                sb.Append(i > 0 ? ",\"" : "\"").Append(names[i]).Append("\":").Append((int)names[i]);
            sb.Append("},\"buttonEvents\":[");
            for (int i = 0; i + 2 < _buttonEvents.Count; i += 3)
                sb.Append(i > 0 ? ",[" : "[").Append(F(_buttonEvents[i])).Append(',').Append((int)_buttonEvents[i + 1]).Append(',')
                  .Append((int)_buttonEvents[i + 2]).Append(']');
            sb.Append("],\n");

            sb.Append("\"rivalHulls\":[");
            for (int i = 0; i < _rivalHulls.Count; i++) sb.Append(i > 0 ? ",\"" : "\"").Append(_rivalHulls[i]).Append('"');
            sb.Append("],\n\"rivals\":[");
            for (int i = 0; i < _samples.Count; i++)
            {
                var m = _samples[i];
                sb.Append(i > 0 ? ",[" : "[");
                for (int k = 0; k < m.RivalCount * 4; k++)
                {
                    if (k > 0) sb.Append(',');
                    float v = _rivals[m.RivalStart + k];
                    sb.Append(k % 4 == 3 ? ((int)v).ToString(c) : F(v));
                }
                sb.Append(']');
            }
            // Each rival's velocity, aligned with "rivals": vx, vy, vz per rival.
            sb.Append("],\n\"rivalVel\":[");
            for (int i = 0; i < _samples.Count; i++)
            {
                var m = _samples[i];
                sb.Append(i > 0 ? ",[" : "[");
                int start = m.RivalStart / 4 * 3;
                for (int k = 0; k < m.RivalCount * 3; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append(F(_rivalVel[start + k], "0.#"));
                }
                sb.Append(']');
            }
            // The nearest crystals each sample, aligned with "samples": x, y, z, element per crystal.
            sb.Append("],\n\"crystals\":[");
            for (int i = 0; i < _samples.Count; i++)
            {
                var m = _samples[i];
                sb.Append(i > 0 ? ",[" : "[");
                for (int k = 0; k < m.CrystalCount * 4; k++)
                {
                    if (k > 0) sb.Append(',');
                    float v = _crystals[m.CrystalStart + k];
                    sb.Append(k % 4 == 3 ? ((int)v).ToString(c) : F(v, "0.#"));
                }
                sb.Append(']');
            }
            sb.Append("],\n");

            sb.Append("\"columns\":[\"t\",\"x\",\"y\",\"z\",\"fx\",\"fy\",\"fz\",\"ux\",\"uy\",\"uz\",\"speed\",\"boost\",\"xSum\",\"ySum\",\"xDiff\",\"yDiff\",\"throttle\",\"rings\",\"lt\",\"rt\",\"cx\",\"cy\",\"cz\","
                    + "\"vx\",\"vy\",\"vz\",\"pitchRate\",\"yawRate\",\"rollRate\",\"charge\",\"state\",\"lvC\",\"lvM\",\"lvS\",\"lvT\",\"held\","
                    + "\"camx\",\"camy\",\"camz\",\"camfx\",\"camfy\",\"camfz\",\"fov\",\"camMode\",\"camux\",\"camuy\",\"camuz\",\"camAspect\",\"camRear\","
                    + "\"m0\",\"m1\",\"m2\",\"m3\",\"timeMode\"],\n");
            sb.Append("\"samples\":[\n");
            for (int i = 0; i < _samples.Count; i++)
            {
                var m = _samples[i];
                if (i > 0) sb.Append(",\n");
                sb.Append('[').Append(F(m.T)).Append(',')
                  .Append(F(m.Pos.x)).Append(',').Append(F(m.Pos.y)).Append(',').Append(F(m.Pos.z)).Append(',')
                  .Append(F(m.Fwd.x, "0.###")).Append(',').Append(F(m.Fwd.y, "0.###")).Append(',').Append(F(m.Fwd.z, "0.###")).Append(',')
                  .Append(F(m.Up.x, "0.###")).Append(',').Append(F(m.Up.y, "0.###")).Append(',').Append(F(m.Up.z, "0.###")).Append(',')
                  .Append(F(m.Speed)).Append(',').Append(F(m.Boost)).Append(',')
                  .Append(F(m.XSum, "0.###")).Append(',').Append(F(m.YSum, "0.###")).Append(',')
                  .Append(F(m.XDiff, "0.###")).Append(',').Append(F(m.YDiff, "0.###")).Append(',')
                  .Append(F(m.Throttle, "0.###")).Append(',').Append(m.Rings).Append(',')
                  .Append(F(m.Lt, "0.###")).Append(',').Append(F(m.Rt, "0.###")).Append(',')
                  .Append(F(m.Course.x, "0.###")).Append(',').Append(F(m.Course.y, "0.###")).Append(',').Append(F(m.Course.z, "0.###")).Append(',')
                  .Append(F(m.Vel.x, "0.#")).Append(',').Append(F(m.Vel.y, "0.#")).Append(',').Append(F(m.Vel.z, "0.#")).Append(',')
                  .Append(F(m.Rates.x, "0.#")).Append(',').Append(F(m.Rates.y, "0.#")).Append(',').Append(F(m.Rates.z, "0.#")).Append(',')
                  .Append(F(m.Charge, "0.###")).Append(',').Append(m.State).Append(',')
                  .Append(F(m.LvC, "0.###")).Append(',').Append(F(m.LvM, "0.###")).Append(',').Append(F(m.LvS, "0.###")).Append(',').Append(F(m.LvT, "0.###")).Append(',')
                  .Append(m.Held ? '1' : '0').Append(',')
                  .Append(F(m.CamPos.x)).Append(',').Append(F(m.CamPos.y)).Append(',').Append(F(m.CamPos.z)).Append(',')
                  .Append(F(m.CamFwd.x, "0.###")).Append(',').Append(F(m.CamFwd.y, "0.###")).Append(',').Append(F(m.CamFwd.z, "0.###")).Append(',')
                  .Append(F(m.Fov, "0.#")).Append(',').Append(m.CamMode).Append(',')
                  .Append(F(m.CamUp.x, "0.###")).Append(',').Append(F(m.CamUp.y, "0.###")).Append(',').Append(F(m.CamUp.z, "0.###")).Append(',')
                  .Append(F(m.Aspect, "0.###")).Append(',').Append(m.RearView ? '1' : '0').Append(',')
                  .Append(F(m.M0, "0.###")).Append(',').Append(F(m.M1, "0.###")).Append(',').Append(F(m.M2, "0.###")).Append(',').Append(F(m.M3, "0.###")).Append(',').Append(m.TimeMode).Append(']');
            }
            sb.Append("\n]\n}\n");
            if (regatta && !haveLane)
                Debug.LogWarning($"[HumanFlightRecorder] Regatta record stored WITHOUT its rail ({_railWhy}); the learner cannot measure it against the rail.");
            return sb.ToString();
        }

        /// <summary>A rail's prisms as [x, y, z, up x, up y, up z]; [] when there is none.</summary>
        static void AppendPrisms(StringBuilder sb, RailCopy rail)
        {
            sb.Append('[');
            if (rail != null)
            {
                for (int i = 0; i < rail.Points.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Vector3 p = rail.Points[i], u = rail.Ups[i];
                    sb.Append('[').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
                      .Append(F(u.x, "0.###")).Append(',').Append(F(u.y, "0.###")).Append(',').Append(F(u.z, "0.###")).Append(']');
                }
            }
            sb.Append(']');
        }

        static string Esc(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string F(float v, string format = "0.##") =>
            float.IsNaN(v) || float.IsInfinity(v) ? "0" : v.ToString(format, CultureInfo.InvariantCulture);
    }
}
#endif
