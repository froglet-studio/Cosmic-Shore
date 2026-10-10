using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The replay's input strategy: an <see cref="IInputStrategy"/> that writes one recorded
    /// <see cref="ReplayStatusFrame"/> into <see cref="IInputStatus"/> per
    /// <see cref="ProcessInput"/> call and raises the frame's recorded <see cref="InputEvents"/>
    /// through the status' own <c>OnButtonPressed</c> / <c>OnButtonReleased</c> assets, exactly
    /// where a device strategy would. When the recording runs out the last frame is held.
    ///
    /// <para>Static entry points so an engine or a tool can drive it without reflection:
    /// <see cref="Start"/>, <see cref="StartFromFile"/>, <see cref="Stop"/>, <see cref="Active"/>,
    /// <see cref="Current"/>. While <see cref="Active"/>, <c>InputController.SelectStrategy</c>
    /// hands the strategy slot to <see cref="Current"/>; nothing else in the game changes.</para>
    ///
    /// <para>It never writes a vessel's pose, course, speed or score: the only outputs are the
    /// IInputStatus floats and the InputEvents, the same surface a keyboard writes.</para>
    /// </summary>
    public sealed class ReplayPlayer : BaseInputStrategy
    {
        readonly ReplayFile _file;
        readonly ReplayStatusFrame[] _frames;
        readonly HashSet<InputEvents> _held = new();
        int _index;

        /// <summary>The running replay's strategy, or null.</summary>
        public static ReplayPlayer Current { get; private set; }

        /// <summary>True while a replay owns the input strategy slot.</summary>
        public static bool Active => Current != null;

        /// <summary>Replays <paramref name="file"/>'s status stream from its first frame.</summary>
        public static ReplayPlayer Start(ReplayFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            Stop();
            Current = new ReplayPlayer(file);
            CSDebug.LogVerbose(CSLogChannel.Parity,
                $"[ReplayPlayer] started: scene {file.scene}, seed {file.seed}, {file.Status.Length} status frame(s), {file.Do.Length} do step(s)");
            return Current;
        }

        public static ReplayPlayer StartFromFile(string path) => Start(ReplayFile.Load(path));

        /// <summary>Releases any held button and hands the strategy slot back to the devices.</summary>
        public static void Stop()
        {
            if (Current == null) return;
            Current.ReleaseHeld();
            Current = null;
            CSDebug.LogVerbose(CSLogChannel.Parity, "[ReplayPlayer] stopped");
        }

        ReplayPlayer(ReplayFile file)
        {
            _file = file;
            _frames = file.Status;
        }

        public ReplayFile File => _file;

        /// <summary>Index of the next frame to play; equals <see cref="FrameCount"/> once the stream has run out.</summary>
        public int Index => _index;

        public int FrameCount => _frames.Length;

        /// <summary>True once every recorded frame has been played (the last one is then held).</summary>
        public bool Finished => _index >= _frames.Length;

        /// <summary>
        /// Initializes against <paramref name="status"/> when it is not the bound one and returns
        /// this strategy, so InputController can select it with one expression.
        /// </summary>
        public ReplayPlayer Bind(IInputStatus status)
        {
            if (!ReferenceEquals(inputStatus, status)) Initialize(status);
            return this;
        }

        public override void Initialize(IInputStatus status)
        {
            base.Initialize(status);
            ResetInput();
        }

        public override void OnStrategyActivated()
        {
            // v1 carries no device; the keyboard is what every parity case and desktop recording
            // drives, and the per-device ability maps need some device to resolve against.
            inputStatus.ActiveInputDevice = InputDeviceType.Keyboard;
        }

        public override void OnStrategyDeactivated() => ReleaseHeld();

        public override void OnPaused() => ReleaseHeld();

        public override void ProcessInput()
        {
            if (inputStatus == null || _frames.Length == 0) return;
            bool live = _index < _frames.Length;
            var frame = _frames[live ? _index : _frames.Length - 1];
            Apply(frame, raiseEvents: live);
            if (live) _index++;
        }

        void Apply(ReplayStatusFrame frame, bool raiseEvents)
        {
            inputStatus.XSum = frame.XSum;
            inputStatus.YSum = frame.YSum;
            inputStatus.XDiff = frame.XDiff;
            inputStatus.YDiff = frame.YDiff;
            inputStatus.Throttle = frame.Throttle;
            inputStatus.LeftTriggerAnalog = frame.LeftTriggerAnalog;
            inputStatus.RightTriggerAnalog = frame.RightTriggerAnalog;
            if (!raiseEvents) return;

            foreach (var name in frame.Pressed)
                if (TryParse(name, out var e))
                {
                    _held.Add(e);
                    inputStatus.OnButtonPressed?.Raise(e);
                }
            foreach (var name in frame.Released)
                if (TryParse(name, out var e))
                {
                    _held.Remove(e);
                    inputStatus.OnButtonReleased?.Raise(e);
                }
        }

        /// <summary>Every button still down gets its release, so no ability outlives the replay (KeyboardInputStrategy's ReleaseHeldButtons).</summary>
        void ReleaseHeld()
        {
            if (inputStatus == null || _held.Count == 0) { _held.Clear(); return; }
            var held = new List<InputEvents>(_held);
            _held.Clear();
            foreach (var e in held) inputStatus.OnButtonReleased?.Raise(e);
        }

        static bool TryParse(string name, out InputEvents e)
        {
            if (Enum.TryParse(name, false, out e)) return true;
            CSDebug.LogWarning($"[ReplayPlayer] unknown InputEvents name '{name}' in replay; skipped");
            return false;
        }
    }
}
