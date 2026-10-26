using System;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Reflex.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CosmicShore.Utility
{
    /// <summary>Everything one watch needs, gathered by whoever opens it (the replay browser).</summary>
    public sealed class ReplayWatchRequest
    {
        public TheaterRecording Recording;

        /// <summary>The replay's summary: its intensity and the arena origin its poses are around.</summary>
        public ReplaySummary Summary;

        public TheaterConfigSO Theater;
        public ReplayConfigSO Replay;
        public CameraManager CameraManager;

        /// <summary>The replay's own controls canvas - the one root canvas the curtain leaves up.</summary>
        public Canvas Keep;

        /// <summary>True when watched from the menu screens, false from the lava lamp.</summary>
        public bool BackToMenuCamera;

        /// <summary>Handed to the arena: a runtime-built cell gets no injection of its own.</summary>
        public Container Container;

        public ThemeManagerDataContainerSO Theme;
    }

    /// <summary>
    /// Plays one replay for a player: the arena it was raced in (<see cref="ReplayArenaStage"/>),
    /// the Theater's <see cref="TheaterPlayback"/> (its ghosts, stage and four shots, unchanged)
    /// behind the <see cref="IReplayTransport"/> every <see cref="IReplayCommand"/> runs against, and
    /// a <see cref="ReplayCurtain"/> that clears the screen for the replay's own controls.
    ///
    /// <para><b>The arena first, then the ghosts.</b> <see cref="BeginAsync"/> stands the mode's own
    /// arena as a satellite far from the menu world and shifts the recording onto it
    /// (<see cref="ReplayStagePlacement"/>), so the race plays through its real rails and rings.
    /// When no arena can be stood (a mode with no preview definition, a build that times out) the
    /// replay still plays, on the Theater's dark stage with the live world masked off.</para>
    ///
    /// <para><b>The stage is configured per replay, never by editing the shared asset.</b> It plays
    /// on a runtime copy of <see cref="TheaterConfigSO"/> with the stage's UI hiding turned off (the
    /// curtain does that job and keeps the EventSystem).</para>
    ///
    /// <para><b>Leaving puts the screen back the way it was found.</b> The Theater returns to the
    /// gameplay camera when it stops; watched from the menu (not the lava lamp), there is no
    /// gameplay camera to return to, so the player hands the view back to the menu's camera. The
    /// arena then comes down, pool-safely, over the next frames.</para>
    /// </summary>
    public sealed class ReplayPlayer : IReplayTransport
    {
        readonly TheaterPlayback _playback = new();
        readonly ReplayCurtain _curtain = new();
        readonly ReplayArenaStage _stage = new();

        TheaterConfigSO _stageConfig;
        CameraManager _cameraManager;
        bool _backToMenuCamera;
        int _drainPrismsPerFrame = 150;

        /// <summary>Raised once when watching ends, by any path, after the screen is back.</summary>
        public event Action Stopped;

        /// <summary>True while the arena is being stood, before playback starts.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>True when the replay plays inside its own arena rather than on the dark stage.</summary>
        public bool OnArena => _stage.IsStanding;

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
        /// Stand the replay's arena, then start watching. False (and everything put back) when the
        /// recording has no vessels, a replay is already playing here, or the Theater refused it.
        /// Cancelling <paramref name="ct"/> while the arena builds strikes it and throws.
        /// </summary>
        public async UniTask<bool> BeginAsync(ReplayWatchRequest request, CancellationToken ct)
        {
            var recording = request?.Recording;
            if (recording == null || recording.Tracks.Count == 0 || IsPlaying || IsLoading) return false;

            var replay = request.Replay ? request.Replay : ReplayConfigSO.Resolve();
            _drainPrismsPerFrame = replay.ArenaDrainPrismsPerFrame;
            var summary = request.Summary;

            bool onArena;
            IsLoading = true;
            try
            {
                onArena = await _stage.StandAsync((GameModes)recording.GameMode, Mathf.Max(1, summary != null ? summary.Intensity : 1),
                                                  recording.CellConfigName, Vector3.back * replay.StageDistance,
                                                  replay.ArenaPrismStride, replay.ArenaBuildTimeoutSeconds,
                                                  request.Container, request.Theme, _drainPrismsPerFrame, ct,
                                                  summary?.Gates);
                ct.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // A cancel during the build is struck by the stage itself; one that lands just after
                // the arena stood is struck here.
                if (_stage.IsStanding) _stage.StrikeAsync(_drainPrismsPerFrame).Forget();
                throw;
            }
            finally
            {
                IsLoading = false;
            }

            if (onArena)
                ReplayStagePlacement.Shift(recording,
                    ReplayStagePlacement.Offset(summary != null ? summary.ArenaOrigin : Vector3.zero, _stage.Origin));

            if (StartPlayback(request, onArena)) return true;

            _stage.StrikeAsync(_drainPrismsPerFrame).Forget();
            return false;
        }

        bool StartPlayback(ReplayWatchRequest request, bool onArena)
        {
            var config = request.Theater;
            _stageConfig = config ? Object.Instantiate(config) : ScriptableObject.CreateInstance<TheaterConfigSO>();
            _stageConfig.name = "TheaterConfig (replay)";
            _stageConfig.hideGameplayUI = false;
            // On its arena the world IS what the replay shows. Without one, a replay recorded in
            // another scene masks the live world off (the menu is not the arena).
            if (onArena) _stageConfig.hideWorld = false;
            else if (!RecordedHere(request.Recording)) _stageConfig.hideWorld = true;

            if (!_playback.Begin(request.Recording, _stageConfig, request.CameraManager))
            {
                Object.Destroy(_stageConfig);
                _stageConfig = null;
                return false;
            }

            // Open on the configured shot - the free camera by default, seeded from the orbit's framing
            // (Begin poses the orbit first), so the whole match is there to fly around.
            var startShot = (request.Replay ? request.Replay : ReplayConfigSO.Resolve()).StartShot;
            if (startShot != _playback.Shot) _playback.SetShotByIndex((int)startShot);

            _cameraManager = request.CameraManager;
            _backToMenuCamera = request.BackToMenuCamera;
            var controller = _cameraManager ? _cameraManager.GetActiveController() as CustomCameraController : null;
            _curtain.Raise(request.Keep, controller ? controller.Camera : null);
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
            // The arena goes last, off screen, a slice per frame.
            _playback.Stop();
            if (_backToMenuCamera && _cameraManager) _cameraManager.SetMainMenuCameraActive();
            _curtain.Lower();
            _stage.StrikeAsync(_drainPrismsPerFrame).Forget();

            if (_stageConfig) Object.Destroy(_stageConfig);
            _stageConfig = null;
            _cameraManager = null;
            Stopped?.Invoke();
        }

        static bool RecordedHere(TheaterRecording recording) =>
            string.Equals(recording.SceneName, SceneManager.GetActiveScene().name, StringComparison.Ordinal);
    }
}
