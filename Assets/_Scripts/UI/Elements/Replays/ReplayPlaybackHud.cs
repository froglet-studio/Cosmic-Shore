using System;
using System.Globalization;
using System.Threading;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The controls over a replay being watched: its own root canvas, spawned by the
    /// <see cref="ReplayBrowserModal"/> for one replay and destroyed when the viewer leaves.
    ///
    /// <para><b>Every control is a command.</b> A HUD button, a pad button and a key all build an
    /// <see cref="IReplayCommand"/> and run it against the <see cref="ReplayPlayer"/>, so what a
    /// control does is written once (<see cref="ReplayCommands"/>).</para>
    ///
    /// <para><b>The pad drives the replay through shortcuts, never through focus.</b> The free and
    /// subject cameras read the sticks, so a D-pad walking between buttons would also be flying the
    /// camera. The pad keeps the Theater's own transport (<see cref="TheaterGesture.ReadPadTransport"/>:
    /// D-pad left / right the shot, up / down the pilot, A pause) plus the shoulders to skip, the
    /// triggers for speed and B to leave; the buttons are for touch and the mouse, and the HUD clears
    /// the EventSystem's selection every frame so a clicked button can never also take an A press.
    /// Keyboard: Space pause, arrows skip, 1-4 the shots and 5 the next pilot (the Theater's keys),
    /// Escape to leave.</para>
    ///
    /// <para><b>The arena builds first.</b> While <see cref="ReplayPlayer"/> stands the replay's arena
    /// the HUD shows its loading panel instead of the transport; B, Escape or EXIT cancel the build
    /// and close the HUD.</para>
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class ReplayPlaybackHud : MonoBehaviour
    {
        [Header("Words (empty shows nothing)")]
        [SerializeField, Tooltip("Tokens: {position} {duration}, each as m:ss.")]
        string timeTemplate;

        [SerializeField, Tooltip("Token: {speed}.")]
        string speedTemplate;

        [SerializeField, Tooltip("The pause button's label while the replay plays.")]
        string pauseLabel;

        [SerializeField, Tooltip("The pause button's label while the replay is paused.")]
        string playLabel;

        [SerializeField, Tooltip("One name per shot, in TheaterShot order: Free, Pilot, Orbit, Chase.")]
        string[] shotNames;

        [Header("Readouts")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text whenText;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text speedText;
        [SerializeField] TMP_Text shotText;
        [SerializeField, Tooltip("The pilot the shot is watching. Hidden on the free camera.")] TMP_Text followText;
        [SerializeField] CanvasGroup followGroup;
        [SerializeField] TMP_Text pauseText;
        [SerializeField, Tooltip("Shows the position; dragging it seeks.")] Slider scrub;

        [Header("Panels")]
        [SerializeField, Tooltip("Shown while the replay's arena is being built (its words are authored on its texts).")]
        CanvasGroup loadingGroup;

        [SerializeField, Tooltip("The title band and the transport band - shown once the replay plays.")]
        CanvasGroup controlsGroup;

        [SerializeField, Tooltip("On the loading panel: stops building the arena and closes the HUD.")]
        Button cancelButton;

        [Header("Controls")]
        [SerializeField] Button pauseButton;
        [SerializeField] Button backButton;
        [SerializeField] Button forwardButton;
        [SerializeField] Button slowerButton;
        [SerializeField] Button fasterButton;
        [SerializeField] Button shotButton;
        [SerializeField] Button pilotButton;
        [SerializeField] Button exitButton;

        readonly ReplayPlayer _player = new();
        ReplayConfigSO _config;
        CancellationTokenSource _loadCts;
        bool _closing;
        int _startFrame = -1;

        /// <summary>Raised once when the HUD goes away - the replay ended, the build was cancelled, or it failed.</summary>
        public event Action Closed;

        /// <summary>True when the HUD closed because the replay could not be played (read it in <see cref="Closed"/>).</summary>
        public bool Failed { get; private set; }

        public bool IsPlaying => _player.IsPlaying;

        /// <summary>True when the replay plays inside its own arena rather than on the dark stage.</summary>
        public bool OnArena => _player.OnArena;

        void Awake()
        {
            Bind(pauseButton, () => ReplayCommands.TogglePause);
            Bind(backButton, () => ReplayCommands.Nudge(-Config.NudgeSeconds));
            Bind(forwardButton, () => ReplayCommands.Nudge(Config.NudgeSeconds));
            Bind(slowerButton, () => SlowerCommand);
            Bind(fasterButton, () => FasterCommand);
            Bind(shotButton, () => ReplayCommands.CycleShot(1));
            Bind(pilotButton, () => ReplayCommands.CycleFollow(1));
            Bind(exitButton, () => ReplayCommands.Stop);
            if (cancelButton) cancelButton.onClick.AddListener(Close);
            if (scrub) scrub.onValueChanged.AddListener(HandleScrub);
            _player.Stopped += HandleStopped;
        }

        ReplayConfigSO Config => _config ? _config : _config = ReplayConfigSO.Resolve();
        IReplayCommand SlowerCommand => ReplayCommands.ScaleSpeed(1f / Config.SpeedStep, Config.MinSpeed, Config.MaxSpeed);
        IReplayCommand FasterCommand => ReplayCommands.ScaleSpeed(Config.SpeedStep, Config.MinSpeed, Config.MaxSpeed);

        /// <summary>
        /// Build the replay's arena, then start watching. The HUD closes itself (raising
        /// <see cref="Closed"/>, with <see cref="Failed"/> set when it could not play) if the build
        /// is cancelled or the Theater refuses the recording.
        /// </summary>
        public async UniTaskVoid PlayAsync(TheaterRecording recording, ReplaySummary summary, string title, string when,
                                           bool backToMenuCamera, Container container, ThemeManagerDataContainerSO theme)
        {
            if (titleText) titleText.text = title;
            if (whenText) whenText.text = when;
            ShowLoading(true);

            _loadCts = new CancellationTokenSource();
            var request = new ReplayWatchRequest
            {
                Recording = recording, Summary = summary, Theater = TheaterConfigSO.Resolve(), Replay = Config,
                CameraManager = CameraManager.Instance, Keep = GetComponent<Canvas>(),
                BackToMenuCamera = backToMenuCamera, Container = container, Theme = theme
            };

            bool playing;
            bool cancelled = false;
            try { playing = await _player.BeginAsync(request, _loadCts.Token); }
            catch (OperationCanceledException) { playing = false; cancelled = true; }
            finally
            {
                _loadCts?.Dispose();
                _loadCts = null;
            }

            if (!this) { if (playing) _player.Stop(); return; }
            if (!playing)
            {
                CloseHud(failed: !cancelled);
                return;
            }

            _startFrame = Time.frameCount;
            ShowLoading(false);
            Refresh();
        }

        /// <summary>Stop watching (or stop building the arena) and close the HUD.</summary>
        public void Close()
        {
            if (_player.IsLoading) _loadCts?.Cancel();
            else Run(ReplayCommands.Stop);
        }

        void ShowLoading(bool loading)
        {
            KitVisibility.SetShown(loadingGroup, loading);
            KitVisibility.SetShown(controlsGroup, !loading);
        }

        void CloseHud(bool failed)
        {
            if (_closing) return;
            _closing = true;
            Failed = failed;
            Closed?.Invoke();
            Destroy(gameObject);
        }

        void Update()
        {
            if (_player.IsLoading)
            {
                if (LeavePressed()) Close();
                return;
            }
            if (!_player.IsPlaying) return;

            // The Theater's dev key (8) started its own playback over this one: give the stage to it.
            var director = TheaterDirector.Instance;
            if (director && director.IsPlaying)
            {
                Close();
                return;
            }

            // Not on the frame it opened: the A press that chose Watch is still "pressed this frame".
            if (Time.frameCount != _startFrame) ReadInput();
            if (!_player.IsPlaying) return;
            _player.Tick(Time.unscaledDeltaTime);
            Refresh();
        }

        void LateUpdate()
        {
            var events = EventSystem.current;
            if (events && events.currentSelectedGameObject) events.SetSelectedGameObject(null);
        }

        void OnDestroy()
        {
            _player.Stopped -= HandleStopped;
            _loadCts?.Cancel();
            if (_player.IsPlaying) _player.Stop();
        }

        static bool LeavePressed() =>
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        void ReadInput()
        {
            TheaterGesture.ReadPadTransport(out int shotStep, out int pilotStep, out bool togglePause);
            if (shotStep != 0) Run(ReplayCommands.CycleShot(shotStep));
            if (pilotStep != 0) Run(ReplayCommands.CycleFollow(pilotStep));
            if (togglePause) Run(ReplayCommands.TogglePause);

            int shot = TheaterGesture.ShotRequestedThisFrame();
            if (shot >= 0) Run(ReplayCommands.SetShot(shot));
            if (TheaterGesture.NextPilotRequestedThisFrame()) Run(ReplayCommands.CycleFollow(1));

            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.leftShoulder.wasPressedThisFrame) Run(ReplayCommands.Nudge(-Config.NudgeSeconds));
                if (pad.rightShoulder.wasPressedThisFrame) Run(ReplayCommands.Nudge(Config.NudgeSeconds));
                if (pad.leftTrigger.wasPressedThisFrame) Run(SlowerCommand);
                if (pad.rightTrigger.wasPressedThisFrame) Run(FasterCommand);
                if (pad.buttonEast.wasPressedThisFrame) Run(ReplayCommands.Stop);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) Run(ReplayCommands.TogglePause);
                if (keyboard.leftArrowKey.wasPressedThisFrame) Run(ReplayCommands.Nudge(-Config.NudgeSeconds));
                if (keyboard.rightArrowKey.wasPressedThisFrame) Run(ReplayCommands.Nudge(Config.NudgeSeconds));
                if (keyboard.escapeKey.wasPressedThisFrame) Run(ReplayCommands.Stop);
            }
        }

        void Run(IReplayCommand command)
        {
            if (_closing || command == null) return;
            if (_player.IsLoading)
            {
                // While the arena builds, the only control that means anything is leaving.
                if (command == ReplayCommands.Stop) Close();
                return;
            }
            command.Execute(_player);
        }

        void Bind(Button button, Func<IReplayCommand> command)
        {
            if (button) button.onClick.AddListener(() => Run(command()));
        }

        void HandleScrub(float value)
        {
            if (_player.IsPlaying) Run(ReplayCommands.SeekFraction(value));
        }

        void HandleStopped() => CloseHud(failed: false);

        void Refresh()
        {
            if (timeText)
                timeText.text = TemplateText.Fill(timeTemplate, "position", ReplaySummary.Clock(_player.Position), "duration", ReplaySummary.Clock(_player.Duration));
            if (speedText)
                speedText.text = TemplateText.Fill(speedTemplate, "speed", _player.Speed.ToString("0.##", CultureInfo.InvariantCulture));
            if (pauseText) pauseText.text = _player.IsPaused ? playLabel : pauseLabel;

            int shot = (int)_player.Shot;
            if (shotText) shotText.text = shotNames != null && shot < shotNames.Length ? shotNames[shot] : string.Empty;
            bool following = _player.Shot != TheaterShot.Free;
            KitVisibility.SetShown(followGroup, following);
            if (followText && following) followText.text = _player.FollowName;

            if (scrub)
                scrub.SetValueWithoutNotify(_player.Duration > 0f ? Mathf.Clamp01(_player.Position / _player.Duration) : 0f);
        }
    }
}
