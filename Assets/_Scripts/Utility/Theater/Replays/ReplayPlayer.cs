using System;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Plays one replay for a player: the Theater's <see cref="TheaterPlayback"/> (its ghosts, stage
    /// and four shots, unchanged) behind the <see cref="IReplayTransport"/> every
    /// <see cref="IReplayCommand"/> runs against, with a <see cref="ReplayCurtain"/> that clears the
    /// screen for the replay's own controls.
    ///
    /// <para><b>The stage is configured per replay, never by editing the shared asset.</b> It plays
    /// on a runtime copy of <see cref="TheaterConfigSO"/> with the stage's UI hiding turned off (the
    /// curtain does that job and keeps the EventSystem), and with the live world masked off whenever
    /// the replay was recorded in another scene - a Regatta watched from the menu flies on the
    /// Theater's dark stage instead of through the menu's world.</para>
    ///
    /// <para><b>Leaving puts the screen back the way it was found.</b> The Theater returns to the
    /// gameplay camera when it stops; watched from the menu (not the lava lamp), there is no
    /// gameplay camera to return to, so the player hands the view back to the menu's camera.</para>
    /// </summary>
    public sealed class ReplayPlayer : IReplayTransport
    {
        readonly TheaterPlayback _playback = new();
        readonly ReplayCurtain _curtain = new();

        TheaterConfigSO _stageConfig;
        CameraManager _cameraManager;
        bool _backToMenuCamera;

        /// <summary>Raised once when watching ends, by any path.</summary>
        public event Action Stopped;

        public bool IsPlaying => _playback.IsPlaying;
        public bool IsPaused => _playback.IsPaused;
        public float Position => _playback.Position;
        public float Duration => _playback.Duration;
        public TheaterShot Shot => _playback.Shot;
        public string FollowName => _playback.FollowName;

        public float Speed
        {
            get => _playback.Speed;
            set => _playback.Speed = value;
        }

        /// <summary>
        /// Start watching. False (and nothing changed) when the recording has no vessels or a replay
        /// is already playing here.
        /// </summary>
        /// <param name="keep">The replay's own controls canvas - the one root canvas the curtain leaves up.</param>
        /// <param name="backToMenuCamera">True when watched from the menu screens, false from the lava lamp.</param>
        public bool Begin(TheaterRecording recording, TheaterConfigSO config, CameraManager cameraManager,
                          Canvas keep, bool backToMenuCamera)
        {
            if (recording == null || recording.Tracks.Count == 0 || IsPlaying) return false;

            _stageConfig = config ? Object.Instantiate(config) : ScriptableObject.CreateInstance<TheaterConfigSO>();
            _stageConfig.name = "TheaterConfig (replay)";
            _stageConfig.hideGameplayUI = false;
            if (!RecordedHere(recording)) _stageConfig.hideWorld = true;

            if (!_playback.Begin(recording, _stageConfig, cameraManager))
            {
                Object.Destroy(_stageConfig);
                _stageConfig = null;
                return false;
            }

            _cameraManager = cameraManager;
            _backToMenuCamera = backToMenuCamera;
            var controller = cameraManager ? cameraManager.GetActiveController() as CustomCameraController : null;
            _curtain.Raise(keep, controller ? controller.Camera : null);
            return true;
        }

        /// <summary>Advance the replay. Real time: a paused or slowed game does not slow it.</summary>
        public void Tick(float unscaledDeltaTime) => _playback.Tick(unscaledDeltaTime);

        public void TogglePause() => _playback.TogglePause();
        public void Seek(float seconds) => _playback.Seek(seconds);
        public void SetShot(int index) => _playback.SetShotByIndex(index);
        public void CycleShot(int step) => _playback.CycleShot(step);
        public void CycleFollow(int step) => _playback.CycleFollow(step);

        public void Stop()
        {
            if (!_playback.IsPlaying && !_curtain.IsUp) return;

            // The Theater first: it lifts its camera mask and the platform holds, then returns to the
            // gameplay camera. Only then does the menu take its view back and the curtain come down.
            _playback.Stop();
            if (_backToMenuCamera && _cameraManager) _cameraManager.SetMainMenuCameraActive();
            _curtain.Lower();

            if (_stageConfig) Object.Destroy(_stageConfig);
            _stageConfig = null;
            _cameraManager = null;
            Stopped?.Invoke();
        }

        static bool RecordedHere(TheaterRecording recording) =>
            string.Equals(recording.SceneName, SceneManager.GetActiveScene().name, StringComparison.Ordinal);
    }
}
