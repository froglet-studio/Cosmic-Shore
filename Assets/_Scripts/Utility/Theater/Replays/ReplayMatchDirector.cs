using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Watches a replay inside the real match scene (<see cref="ReplayMatch"/>, <c>Docs/REPLAYS.md</c> §6.3).
    /// It lets the scene start as a match does (the controller, the cell, its ecology, the course from the
    /// recorded seed, every seat spawned with the recorded hull and domain), presses Ready, and at Go takes
    /// every vessel over:
    /// <list type="bullet">
    /// <item><b>its path is the recording's.</b> The vessel's own flight is switched off
    /// (<c>VesselTransformer.ToggleActive(false)</c>, the Honeyguide zip's hold) and its pose, speed and course
    /// are set from its track every frame, so its trail lays real prisms where the match laid them;</item>
    /// <item><b>its actions are the recording's.</b> No pilot flies it (the autopilots stand down, a human's
    /// input pauses) and every recorded press, release and autopilot action is re-run on it at its moment
    /// (<see cref="R_VesselActionHandler.ReplayInput"/>), so its guns, boosts and abilities fire with their
    /// real effects;</item>
    /// <item><b>the match never ends by itself.</b> The turn monitors are off, so no score ends the replay
    /// early; it ends when the recording does.</item>
    /// </list>
    /// What the vessels' actions do to the world (prisms broken, crystals taken, creatures caught) is the game
    /// computing it again from the same paths and actions. The ecology rolls its own dice per run, so
    /// creatures that wandered are not guaranteed to wander the same way (§6.3 lists it).
    ///
    /// <para><b>Controls</b> (none clash with the free camera's sticks, shoulders, triggers, WASD/QE and
    /// mouse): A / Space pause; D-pad left / right or J / L skip 5 s back / forward; D-pad up / down or
    /// = / - faster / slower; Y / Tab cycles the free camera and a chase camera on each pilot; B / Escape
    /// leaves. A skip forward fast-forwards; a skip back reloads the scene and fast-forwards to the moment.</para>
    /// </summary>
    public sealed class ReplayMatchDirector : MonoBehaviour
    {
        const float SkipSeconds = 5f;
        const float FastForwardScale = 8f;
        const float MinSpeed = 0.25f, MaxSpeed = 4f;
        const float EndHoldSeconds = 2f;
        const float ChaseDistance = 28f, ChaseHeight = 7f;
        const float FreeMoveSpeed = 0.9f, FreeLookSpeed = 140f, SceneRadius = 400f;
        /// <summary>How long to wait for the HUD's Ready button (it shows after the intro) before pressing Ready in code.</summary>
        const float ReadyButtonWaitSeconds = 20f;
        const string ReadyButtonName = "ReadyButton";

        sealed class Binding
        {
            public TheaterTrack Track;
            public IVessel Vessel;
            public IVesselStatus Status;
            public Transform Root;
            public readonly Dictionary<string, ShipActionSO> Actions = new();
        }

        enum Phase { WaitingForSeats, WaitingForGo, Playing, Leaving }

        GameDataSO _gameData;
        TheaterRecording _recording;
        Phase _phase;
        bool _readyPressed;
        float _seatedAt = -1f;
        float _goTime, _endedAt = -1f;
        int _actionCursor;
        float _speed = 1f;
        bool _paused;
        float _fastForwardTo = -1f;
        readonly List<Binding> _bindings = new();
        readonly Binding[] _byTrack = new Binding[64];

        CameraManager _cameras;
        Transform _rig;
        readonly TheaterFreeCamera _free = new();
        Vector3 _freePosition;
        int _follow = -1;   // -1 = free camera

        /// <summary>Create the director for the scene that just loaded to watch <see cref="ReplayMatch.Recording"/>.</summary>
        public static ReplayMatchDirector Create(GameDataSO gameData)
        {
            var go = new GameObject("[ReplayMatch]");
            var director = go.AddComponent<ReplayMatchDirector>();
            director._gameData = gameData;
            director._recording = ReplayMatch.Recording;
            return director;
        }

        /// <summary>Seconds into the recording now.</summary>
        public float Position => _phase == Phase.Playing ? Time.time - _goTime : ReplayMatch.StartAt;

        // Start, not OnEnable: AddComponent runs OnEnable before Create has handed the data over.
        void Start() => _gameData.OnMiniGameTurnStarted.OnRaised += HandleGo;

        void OnDestroy()
        {
            if (_gameData) _gameData.OnMiniGameTurnStarted.OnRaised -= HandleGo;
            Time.timeScale = 1f;
            if (_rig && _cameras) _cameras.RestoreGameplayCamera();
        }

        void Update()
        {
            if (_recording == null || !_gameData) return;
            switch (_phase)
            {
                case Phase.WaitingForSeats:
                    if (SeatsReady()) PressReady();
                    break;
                case Phase.Playing:
                    ReadControls();
                    break;
            }
        }

        void LateUpdate()
        {
            if (_phase != Phase.Playing) return;
            float t = _recording.StartTime + Position;
            for (int i = 0; i < _bindings.Count; i++) Drive(_bindings[i], t);
            RunActions(t);
            UpdateFastForward();
            UpdateCamera();

            float end = _recording.StartTime + _recording.Duration;
            if (t < end) return;
            if (_endedAt < 0f) _endedAt = Time.unscaledTime;
            else if (Time.unscaledTime - _endedAt >= EndHoldSeconds) Leave("the recording ended");
        }

        // ---------------------------------------------------------------- start

        bool SeatsReady()
        {
            int seated = 0;
            foreach (var p in _gameData.Players)
                if (p != null && p.Vessel != null) seated++;
            if (seated < _recording.Tracks.Count) return false;
            // Skim Race builds its track from the replicated seed a moment after the scene starts.
            var skim = FindAnyObjectByType<SkimRaceController>();
            return !skim || skim.TrackSeed != 0;
        }

        /// <summary>
        /// Press Ready the way a player does: the HUD's own button, once the intro has shown it, so the HUD
        /// leaves its waiting state as it does in a match. A HUD that never shows it gets the controller's
        /// Ready after <see cref="ReadyButtonWaitSeconds"/>. One press: a second restarts the countdown.
        /// </summary>
        void PressReady()
        {
            if (_readyPressed) return;
            if (_seatedAt < 0f) _seatedAt = Time.unscaledTime;

            var go = GameObject.Find(ReadyButtonName);
            if (go && go.TryGetComponent(out UnityEngine.UI.Button button) && button.IsInteractable())
            {
                _readyPressed = true;
                _phase = Phase.WaitingForGo;
                button.onClick.Invoke();
                return;
            }
            if (Time.unscaledTime - _seatedAt < ReadyButtonWaitSeconds) return;

            var controller = FindAnyObjectByType<MiniGameControllerBase>();
            if (!controller) return;
            _readyPressed = true;
            _phase = Phase.WaitingForGo;
            controller.OnReadyClicked();
        }

        void HandleGo()
        {
            if (_phase != Phase.WaitingForGo && _phase != Phase.WaitingForSeats) return;

            // The match never ends by itself: the recording's end does.
            foreach (var monitors in FindObjectsByType<TurnMonitorController>(FindObjectsSortMode.None))
                monitors.enabled = false;

            Bind();
            _goTime = Time.time - ReplayMatch.StartAt;
            _actionCursor = 0;
            _phase = Phase.Playing;
            // Actions before the start point are skipped; their effects are what the fast-forward replays.
            if (ReplayMatch.StartAt > 0f)
            {
                _goTime = Time.time;
                _fastForwardTo = ReplayMatch.StartAt;
                Time.timeScale = FastForwardScale;
            }
            BeginCamera();
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] playing the match: {_bindings.Count} vessels, {_recording.Actions.Count} actions");
        }

        /// <summary>Pair every recorded track with a live seat: the local player takes the human track, AI seats the rest by hull and domain.</summary>
        void Bind()
        {
            _bindings.Clear();
            System.Array.Clear(_byTrack, 0, _byTrack.Length);
            var tracks = _recording.Tracks;
            int human = ReplayMatch.HumanTrack(_recording);
            var free = new List<IPlayer>();
            IPlayer local = _gameData.LocalPlayer;
            foreach (var p in _gameData.Players)
                if (p != null && p.Vessel != null && !ReferenceEquals(p, local)) free.Add(p);

            for (int i = 0; i < tracks.Count && i < _byTrack.Length; i++)
            {
                IPlayer seat = null;
                if (i == human && local != null && local.Vessel != null) seat = local;
                else seat = TakeSeat(free, tracks[i]);
                if (seat == null) continue;

                var b = new Binding { Track = tracks[i], Vessel = seat.Vessel, Status = seat.Vessel.VesselStatus };
                b.Root = b.Status.Transform;
                if (!b.Root) continue;
                if (seat is Player player && player.IsServer && !string.IsNullOrEmpty(tracks[i].PlayerName) && seat != local)
                    player.NetName.Value = tracks[i].PlayerName;
                StandDownPilots(seat, b);
                if (i == human) _follow = _bindings.Count;
                _bindings.Add(b);
                _byTrack[i] = b;
            }
        }

        static IPlayer TakeSeat(List<IPlayer> free, TheaterTrack track)
        {
            int best = -1;
            for (int k = 0; k < free.Count; k++)
            {
                var status = free[k].Vessel.VesselStatus;
                bool hull = (int)status.VesselType == track.VesselType;
                bool domain = (int)status.Domain == track.Domain;
                if (hull && domain) { best = k; break; }
                if (best < 0 && hull) best = k;
            }
            if (best < 0 && free.Count > 0) best = 0;
            if (best < 0) return null;
            var seat = free[best];
            free.RemoveAt(best);
            return seat;
        }

        /// <summary>No pilot flies a replayed vessel: the recording does.</summary>
        static void StandDownPilots(IPlayer seat, Binding b)
        {
            var root = b.Root;
            if (b.Status.AutoPilotEnabled) b.Vessel.ToggleAIPilot(false);
            if (root.TryGetComponent(out SkimRacePilot racer)) racer.enabled = false;
            if (root.TryGetComponent(out AIPilot pilot)) pilot.enabled = false;
            seat.InputController?.SetPause(true);
            b.Status.ActionHandler?.ReleaseHeldInputs();
            b.Status.VesselTransformer?.ToggleActive(false);
        }

        // ---------------------------------------------------------------- playing

        static void Drive(Binding b, float t)
        {
            if (!b.Root) return;
            // Something (a respawn, a player restart) may have handed the hull back to its flight model.
            var transformer = b.Status.VesselTransformer;
            if (transformer && transformer.IsActive) transformer.ToggleActive(false);
            if (!b.Track.TryEvaluate(t, out var position, out var rotation, out float speed)) return;
            b.Root.SetPositionAndRotation(position, rotation);
            b.Status.Speed = speed;
            b.Status.Course = rotation * Vector3.forward;
        }

        void RunActions(float t)
        {
            var actions = _recording.Actions;
            while (_actionCursor < actions.Count && actions[_actionCursor].Time <= t)
            {
                var e = actions[_actionCursor++];
                // Skipped ahead of: its effects come from the fast-forward, not from firing it late.
                if (e.Time < t - 1f && !FastForwarding) continue;
                if (e.Track < 0 || e.Track >= _byTrack.Length) continue;
                var b = _byTrack[e.Track];
                if (b == null || b.Status.ActionHandler == null) continue;
                Run(b, e);
            }
        }

        bool FastForwarding => _fastForwardTo >= 0f;

        static void Run(Binding b, in TheaterActionEvent e)
        {
            var handler = b.Status.ActionHandler;
            if (e.Kind == TheaterActionEvent.KindInput)
            {
                handler.ReplayInput((InputEvents)e.Input, e.Device, e.Down);
                return;
            }
            if (string.IsNullOrEmpty(e.Action)) return;
            if (!b.Actions.TryGetValue(e.Action, out var action))
            {
                if (!handler.TryFindAction(e.Action, out action) && b.Status.AIPilot)
                    b.Status.AIPilot.TryFindAbility(e.Action, out action);
                b.Actions[e.Action] = action;
            }
            if (!action) return;
            if (e.Down) action.StartAction(handler.Executors, b.Status);
            else action.StopAction(handler.Executors, b.Status);
        }

        // ---------------------------------------------------------------- transport

        void ReadControls()
        {
            var pad = Gamepad.current;
            var keys = Keyboard.current;
            bool Pressed(ButtonControl button, Key key) =>
                (button != null && button.wasPressedThisFrame) || (keys != null && keys[key].wasPressedThisFrame);

            if (Pressed(pad?.buttonSouth, Key.Space)) TogglePause();
            if (Pressed(pad?.dpad.right, Key.L)) Skip(SkipSeconds);
            if (Pressed(pad?.dpad.left, Key.J)) Skip(-SkipSeconds);
            if (Pressed(pad?.dpad.up, Key.Equals)) SetSpeed(_speed * 2f);
            if (Pressed(pad?.dpad.down, Key.Minus)) SetSpeed(_speed * 0.5f);
            if (Pressed(pad?.buttonNorth, Key.Tab)) CycleCamera();
            if (Pressed(pad?.buttonEast, Key.Escape)) Leave("the viewer left");
        }

        void TogglePause()
        {
            _paused = !_paused;
            if (!FastForwarding) Time.timeScale = _paused ? 0f : _speed;
        }

        void SetSpeed(float speed)
        {
            _speed = Mathf.Clamp(speed, MinSpeed, MaxSpeed);
            if (!_paused && !FastForwarding) Time.timeScale = _speed;
        }

        void Skip(float seconds)
        {
            float target = Mathf.Clamp(Position + seconds, 0f, _recording.Duration);
            if (seconds < 0f)
            {
                // Back: the world cannot be un-broken, so the scene loads again and fast-forwards to the moment.
                _phase = Phase.Leaving;
                Time.timeScale = 1f;
                ReplayMatch.Relaunch(_gameData, target);
                return;
            }
            _fastForwardTo = target;
            Time.timeScale = FastForwardScale;
        }

        void UpdateFastForward()
        {
            if (!FastForwarding || Position < _fastForwardTo) return;
            _fastForwardTo = -1f;
            Time.timeScale = _paused ? 0f : _speed;
        }

        void Leave(string why)
        {
            if (_phase == Phase.Leaving) return;
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] leaving at {Position:0.0}s of {_recording.Duration:0.0}s: {why}");
            _phase = Phase.Leaving;
            Time.timeScale = 1f;
            ReplayMatch.Clear();
            var loader = FindAnyObjectByType<SceneLoader>();
            if (loader) loader.ReturnToMainMenu();
        }

        // ---------------------------------------------------------------- camera

        void BeginCamera()
        {
            _cameras = CameraManager.Instance;
            var main = Camera.main;
            if (main) _freePosition = main.transform.position;
            _rig = _cameras ? _cameras.BeginManualReplayCamera() : null;
            if (_rig && main) _free.Seed(main.transform.rotation);
            // Opens behind the recorded pilot (Bind chose the human's seat), as the match looked to them.
            if (_follow >= _bindings.Count) _follow = -1;
        }

        void CycleCamera()
        {
            _follow++;
            if (_follow >= _bindings.Count) _follow = -1;
            if (_follow < 0 && _rig) { _freePosition = _rig.position; _free.Seed(_rig.rotation); }
        }

        void UpdateCamera()
        {
            if (!_rig) return;
            if (_follow >= 0 && _follow < _bindings.Count && _bindings[_follow].Root)
            {
                var subject = _bindings[_follow].Root;
                Vector3 at = subject.position - subject.forward * ChaseDistance + subject.up * ChaseHeight;
                _rig.SetPositionAndRotation(at, Quaternion.LookRotation(subject.position - at, subject.up));
                return;
            }
            _free.Tick(Time.unscaledDeltaTime, SceneRadius, FreeMoveSpeed, FreeLookSpeed, ref _freePosition, out var rotation);
            _rig.SetPositionAndRotation(_freePosition, rotation);
        }
    }
}
