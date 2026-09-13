using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plays a <see cref="GamepadRumblePattern"/> on the active gamepad by stepping its segments
    /// through <c>Gamepad.SetMotorSpeeds</c>. This is the whole of what the NiceVibrations plugin
    /// was doing for us on desktop, and the only reason the dependency existed in code.
    ///
    /// <para><b>ONE pattern at a time, by design.</b> A <see cref="Play"/> replaces whatever was
    /// playing outright. That is not an incidental limitation inherited from the plugin — it is
    /// the property <see cref="HapticController"/>'s priority gate is written against: a punish
    /// thud must be able to load over a skim train mid-pulse, and the ordering
    /// <b>alert &gt; punish &gt; skim &gt; spray</b> only means anything while the motors can
    /// carry exactly one thing. Do not add mixing or queueing here without re-reading that gate
    /// and <c>Docs/HAPTICS.md</c>; a second concurrent pattern makes every feel less legible, not
    /// more.</para>
    ///
    /// <para><b>Segment timing compensates for frame error, the same way the plugin's did.</b>
    /// A frame is the finest resolution available (16.7 ms at 60 fps, 33 ms at 30), and several
    /// authored segments are shorter than that. So the position in the pattern is derived from
    /// ELAPSED TIME rather than advanced one segment per frame: a frame that overshoots skips the
    /// segments it slept through instead of stretching them. Stretching would make a whole clip
    /// run long on a slow frame — an alert that already lasts 1.2 s would drift audibly further
    /// past the rate limit that is supposed to bound it.</para>
    ///
    /// <para><b>Motors are written once per SEGMENT, not once per frame.</b> Every
    /// <c>SetMotorSpeeds</c> is a command sent to the device; re-sending an unchanged pair every
    /// frame is pure cost, and on some backends it stutters.</para>
    ///
    /// <para><b>On device choice.</b> This targets <c>Gamepad.current</c> — Input System's own
    /// "most recently used pad" — exactly as the plugin did. It deliberately does NOT ask
    /// <see cref="InputDeviceActuation"/> which family the player is flying with: that law
    /// (<c>Docs/ONE_THUMB_MOUSE_CONTROLS.md §4.0</c>) exists so ONE component answers "which
    /// device is the player using", and this component does not ask that question at all — it
    /// asks "which pad has motors", writes to it, and no-ops when there is none. Gating rumble on
    /// the actuated family would also be a behaviour change: a pad player who has just typed
    /// something would stop feeling their own skims.</para>
    /// </summary>
    public static class GamepadRumblePlayer
    {
        static GamepadRumblePattern _pattern;
        static bool _playing;
        static float _gain;

        // Where playback started, on the same unscaled clock HapticController's gate uses — so a
        // paused game does not advance a rumble, and timeScale does not stretch one.
        static double _startTime;

        // The segment currently on the motors, and the pattern-relative time at which it began.
        // -1 means "nothing written yet this playback".
        static int _segment = -1;
        static int _segmentStartMs;

        // The device the last write actually went to, so a pad swapped out mid-pattern can be
        // silenced rather than left holding its final speeds forever.
        static Gamepad _wroteTo;

        /// <summary>True while a pattern is on the motors.</summary>
        public static bool IsPlaying => _playing;

        /// <summary>
        /// Start <paramref name="pattern"/>, replacing anything already playing.
        /// </summary>
        /// <param name="gain">
        /// The single multiplier applied to both motors — the product of the player's haptics
        /// level and the caller's per-pulse strength. Values above 1 are legal and clip hard at
        /// the motor, which is what the authored envelopes assume.
        /// </param>
        public static void Play(in GamepadRumblePattern pattern, float gain)
        {
            if (!pattern.IsValid) { Stop(); return; }

            // No explicit silence first: the Tick() below writes the new segment 0 in this same
            // frame, so the transition is a direct overwrite rather than a needless trip through
            // zero — one fewer device command per pulse, which the spray pays 22 times a second at
            // full spread. The two cases where nothing overwrites the old speeds are both covered:
            // WriteMotors silences a pad that is no longer current (including an unplug), and a
            // pattern with nothing left to play falls into Stop().
            _pattern = pattern;
            _gain = Mathf.Max(gain, 0f);
            _playing = true;
            _startTime = Time.unscaledTimeAsDouble;
            _segment = -1;
            _segmentStartMs = 0;

            // Land segment 0 in the frame the caller asked for it. Waiting for the driver's next
            // Update would put a frame of silence in front of every pulse, and the shortest feel
            // in the game is 50 ms — three frames at 60 fps.
            Tick();
        }

        /// <summary>
        /// Stop playback and turn the motors off. Safe to call when nothing is playing.
        /// </summary>
        public static void Stop()
        {
            _playing = false;
            _segment = -1;
            _segmentStartMs = 0;
            SilenceMotors();
        }

        /// <summary>
        /// One frame of playback. Called by the installed driver; public so a test or diagnostic
        /// can step it deterministically.
        /// </summary>
        public static void Tick()
        {
            if (!_playing) return;

            var durations = _pattern.DurationsMs;
            double elapsedMs = (Time.unscaledTimeAsDouble - _startTime) * 1000.0;

            // Walk forward past every segment whose time has already passed. This is the frame-
            // error compensation: on a slow frame more than one segment can end between ticks.
            int segment = _segment < 0 ? 0 : _segment;
            int segmentStartMs = _segmentStartMs;
            while (segment < durations.Length && elapsedMs >= segmentStartMs + durations[segment])
            {
                segmentStartMs += durations[segment];
                segment++;
            }

            if (segment >= durations.Length) { Stop(); return; }

            bool changed = segment != _segment;
            _segment = segment;
            _segmentStartMs = segmentStartMs;
            if (!changed) return;   // still inside the segment already on the motors

            WriteMotors(
                Mathf.Clamp01(_pattern.LowFrequencyMotorSpeeds[segment] * _gain),
                Mathf.Clamp01(_pattern.HighFrequencyMotorSpeeds[segment] * _gain));
        }

        static void WriteMotors(float low, float high)
        {
            var pad = Gamepad.current;

            // The active pad changed mid-pattern: leave the old one silent rather than frozen at
            // whatever it was last told.
            if (_wroteTo != null && _wroteTo != pad) SilenceMotors();

            if (pad == null) return;
            pad.SetMotorSpeeds(low, high);
            _wroteTo = pad;
        }

        static void SilenceMotors()
        {
            if (_wroteTo == null) return;
            _wroteTo.ResetHaptics();
            _wroteTo = null;
        }

        // ---------------------------------------------------------------- driver

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallDriver()
        {
            // Statics survive play-mode exit in the editor, so a pattern left mid-playback by the
            // previous session would otherwise resume against a stale start time.
            _playing = false;
            _segment = -1;
            _segmentStartMs = 0;
            _wroteTo = null;
            _gain = 0f;

            // HideInHierarchy, NOT HideAndDontSave — the latter exempts the object from
            // play-mode-exit cleanup. Same pattern as VesselSpeedTunnel's driver.
            var go = new GameObject("[GamepadRumblePlayer]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// Steps playback and — the half that matters more — guarantees the motors stop. A sound
        /// left playing is something the player can hear; a motor left running is not, so every
        /// way this object can go quiet has to turn it off explicitly.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void Update() => Tick();

            // Play-mode exit, scene teardown and application quit all destroy this object.
            void OnDisable() => Stop();

            // Backgrounding on mobile, and alt-tab on desktop where the platform does not take
            // the device away for us.
            void OnApplicationPause(bool paused) { if (paused) Stop(); }
            void OnApplicationFocus(bool focused) { if (!focused) Stop(); }
        }
    }
}
