using System;
using System.Globalization;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
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
        bool _closing;
        int _startFrame = -1;

        /// <summary>Raised once, after the replay has stopped and the screen is back.</summary>
        public event Action Closed;

        public bool IsPlaying => _player.IsPlaying;

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
            if (scrub) scrub.onValueChanged.AddListener(HandleScrub);
            _player.Stopped += HandleStopped;
        }

        ReplayConfigSO Config => _config ? _config : _config = ReplayConfigSO.Resolve();
        IReplayCommand SlowerCommand => ReplayCommands.ScaleSpeed(1f / Config.SpeedStep, Config.MinSpeed, Config.MaxSpeed);
        IReplayCommand FasterCommand => ReplayCommands.ScaleSpeed(Config.SpeedStep, Config.MinSpeed, Config.MaxSpeed);

        /// <summary>
        /// Start watching <paramref name="recording"/>. False (the caller destroys the HUD) when the
        /// Theater refused it.
        /// </summary>
        public bool Play(TheaterRecording recording, string title, string when, bool backToMenuCamera)
        {
            if (titleText) titleText.text = title;
            if (whenText) whenText.text = when;
            var cameraManager = CameraManager.Instance;
            if (!_player.Begin(recording, TheaterConfigSO.Resolve(), cameraManager, GetComponent<Canvas>(), backToMenuCamera))
                return false;
            _startFrame = Time.frameCount;
            Refresh();
            return true;
        }

        /// <summary>Stop watching (the stage comes down, the menu comes back) and destroy the HUD.</summary>
        public void Close() => Run(ReplayCommands.Stop);

        void Update()
        {
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
            if (_player.IsPlaying) _player.Stop();
        }

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

        void HandleStopped()
        {
            if (_closing) return;
            _closing = true;
            Closed?.Invoke();
            Destroy(gameObject);
        }

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
