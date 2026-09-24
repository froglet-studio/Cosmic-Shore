using System;
using System.IO;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The theater's one entry point: press <b>9</b> to record a match, <b>8</b> to watch it back.
    ///
    /// <para><b>P0 — vessel ghost recorder.</b> It records every vessel's pose for the whole match
    /// and replays it as domain-coloured ghosts on a free camera. It records nothing else yet;
    /// prisms are P1 and everything that moves is P2 (see <c>Docs/THEATER.md</c>).</para>
    ///
    /// <para><b>Outcomes, never inputs.</b> Halo 3's theater replayed controller input against a
    /// deterministic simulation, and this game does not have one: <c>MoveShip</c> integrates in
    /// <c>Update</c>, the life spawners roll on each peer's own <c>Random</c>, <c>Mathf.Sin</c> is
    /// not bit-identical across Mono and IL2CPP, and remote vessels arrive as replicated transforms
    /// that were never simulated here at all. Recording where things WERE needs none of that to be
    /// true, so none of it has to be fixed and none of it can break a recording later.</para>
    ///
    /// <para>Zero-wire, exactly as <c>ScreenshotDirector</c> is: a
    /// <c>[RuntimeInitializeOnLoadMethod]</c> stands it up once per launch with nothing in any
    /// scene referencing it, so it exists in every scene and costs one <c>Update</c> branch when
    /// idle.</para>
    ///
    /// <para><b>Recordings are never pushed.</b> They land in the repo's git-ignored
    /// <c>Recordings</c> folder beside the screenshots, for the same reason: a recording is for the
    /// person who made it to look at and then decide about.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TheaterDirector : SingletonPersistent<TheaterDirector>
    {
        TheaterConfigSO _config;
        CameraManager _cameraManager;
        readonly TheaterRecorder _recorder = new();
        readonly TheaterPlayback _playback = new();

        string _lastMessage = string.Empty;
        float _lastMessageTime = -999f;

        TheaterConfigSO Config => _config != null ? _config : _config = TheaterConfigSO.Resolve();

        public bool IsRecording => _recorder.IsRecording;
        public bool IsPlaying => _playback.IsPlaying;

        /// <summary>
        /// Stand the director up once per launch, with nothing in any scene referencing it. Guards
        /// on a live <see cref="SingletonPersistent{T}.Instance"/> so an inspector-placed one wins.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance == null)
                new GameObject("[TheaterDirector]").AddComponent<TheaterDirector>();
        }

        void Update()
        {
            if (TheaterGesture.ToggleRecordRequestedThisFrame()) ToggleRecording();
            if (TheaterGesture.TogglePlaybackRequestedThisFrame()) TogglePlayback();

            // Scaled delta while RECORDING (the recording follows the game's own clock, so a
            // slow-motion celebration is recorded as the slow motion the players saw), unscaled
            // while PLAYING (a paused or slowed game must not crawl the theater).
            if (_recorder.IsRecording) _recorder.Tick(Time.deltaTime);
            if (_playback.IsPlaying) _playback.Tick(Time.unscaledDeltaTime);
        }

        public void ToggleRecording()
        {
            if (_recorder.IsRecording) { StopRecording(); return; }
            StartRecording();
        }

        public void StartRecording()
        {
            if (_recorder.IsRecording) return;
            if (!Config.recordVessels)
            {
                Report("Theater: recordVessels is off in TheaterConfig - nothing to record.");
                return;
            }

            // The SCENE NAME is the mode: every arcade mode has its own scene (Docs/SCENES.md), so
            // it identifies the match uniquely on its own. GameMode and CellConfigName are left at
            // their empty values rather than guessed — P1 stands the arena back up from those two
            // fields, and a placeholder that lies is worse to build on than a field that is empty.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            _recorder.Begin(Config, scene.name, gameMode: 0, cellConfigName: string.Empty);
            Report($"Theater: RECORDING {scene.name}.");
            CSDebug.LogVerbose(CSLogChannel.Theater, $"[Theater] recording started in {scene.name}");
        }

        public void StopRecording()
        {
            if (!_recorder.IsRecording) return;
            _recorder.Stop();

            string path = _recorder.TryWrite(Config, out string error);
            if (path == null)
            {
                Report($"Theater: recording NOT saved - {error}");
                CSDebug.LogWarning($"[Theater] recording not saved: {error}");
                return;
            }

            Report($"Theater: saved {Path.GetFileName(path)} " +
                   $"({_recorder.TrackCount} vessels, {_recorder.PoseCount} poses, " +
                   $"{_recorder.EstimatedBytes / 1024f:0} KB)");
            CSDebug.LogVerbose(CSLogChannel.Theater, $"[Theater] wrote {path}");
        }

        public void TogglePlayback()
        {
            if (_playback.IsPlaying) { _playback.Stop(); Report("Theater: playback stopped."); return; }
            PlayMostRecent();
        }

        /// <summary>Load and play the newest recording in the output folder.</summary>
        public void PlayMostRecent()
        {
            if (_recorder.IsRecording)
            {
                Report("Theater: stop recording (9) before playing back.");
                return;
            }

            string folder = Config.ResolveOutputFolder();
            string path = FindMostRecent(folder);
            if (path == null)
            {
                Report($"Theater: no .{TheaterRecording.Extension} recordings in {folder}");
                return;
            }

            TheaterRecording recording;
            string error;
            try
            {
                using var stream = File.OpenRead(path);
                if (!TheaterRecording.TryRead(stream, out recording, out error))
                {
                    Report($"Theater: cannot read {Path.GetFileName(path)} - {error}");
                    return;
                }
            }
            catch (IOException e) { Report($"Theater: cannot open recording - {e.Message}"); return; }
            catch (UnauthorizedAccessException e) { Report($"Theater: cannot open recording - {e.Message}"); return; }

            if (_cameraManager == null) _cameraManager = FindAnyObjectByType<CameraManager>();

            if (!_playback.Begin(recording, Config, _cameraManager))
            {
                Report("Theater: recording has no tracks.");
                return;
            }

            Report($"Theater: playing {Path.GetFileName(path)} " +
                   $"({recording.Tracks.Count} vessels, {recording.Duration:0.0}s)");
            CSDebug.LogVerbose(CSLogChannel.Theater, $"[Theater] playing {path}");
        }

        static string FindMostRecent(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return null;
                string[] files = Directory.GetFiles(folder, "*." + TheaterRecording.Extension);
                if (files.Length == 0) return null;

                string best = files[0];
                DateTime bestTime = File.GetLastWriteTimeUtc(best);
                for (int i = 1; i < files.Length; i++)
                {
                    DateTime t = File.GetLastWriteTimeUtc(files[i]);
                    if (t <= bestTime) continue;
                    best = files[i];
                    bestTime = t;
                }
                return best;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        void Report(string message)
        {
            _lastMessage = message;
            _lastMessageTime = Time.unscaledTime;
        }

        void OnGUI()
        {
            bool showMessage = Time.unscaledTime - _lastMessageTime < 5f;
            if (!_recorder.IsRecording && !_playback.IsPlaying && !showMessage) return;

            const int width = 420;
            int x = Screen.width - width - 12;
            int y = 12;

            if (_recorder.IsRecording)
            {
                GUI.Label(new Rect(x, y, width, 22),
                    $"[REC] {_recorder.RecordedSeconds:0.0}s   {_recorder.TrackCount} vessels   " +
                    $"{_recorder.EstimatedBytes / 1024f:0} KB      [9] stop");
                y += 22;
            }

            if (_playback.IsPlaying)
            {
                GUI.Label(new Rect(x, y, width, 22),
                    $"[THEATER] {_playback.Position:0.0} / {_playback.Duration:0.0}s   " +
                    $"{(_playback.IsPaused ? "PAUSED" : $"x{_playback.Speed:0.##}")}   " +
                    $"{_playback.Shot}{(_playback.Shot == TheaterShot.Follow ? " " + _playback.FollowName : "")}");
                y += 24;

                if (GUI.Button(new Rect(x, y, 64, 22), _playback.IsPaused ? "Play" : "Pause")) _playback.TogglePause();
                if (GUI.Button(new Rect(x + 68, y, 44, 22), "-5s")) _playback.Nudge(-5f);
                if (GUI.Button(new Rect(x + 116, y, 44, 22), "+5s")) _playback.Nudge(5f);
                if (GUI.Button(new Rect(x + 164, y, 52, 22), "Slower")) _playback.Speed = Mathf.Max(0.1f, _playback.Speed * 0.5f);
                if (GUI.Button(new Rect(x + 220, y, 52, 22), "Faster")) _playback.Speed = Mathf.Min(8f, _playback.Speed * 2f);
                if (GUI.Button(new Rect(x + 276, y, 60, 22), "Orbit")) _playback.Shot = TheaterShot.Orbit;
                if (GUI.Button(new Rect(x + 340, y, 70, 22), "Follow +")) _playback.CycleFollow(1);
                y += 26;
            }

            if (showMessage) GUI.Label(new Rect(x, y, width, 40), _lastMessage);
        }

        void OnDestroy()
        {
            // A playback holds the prism-occlusion-corridor and speed-tunnel suppressions through
            // CameraManager; leaving it running past teardown would latch both platform laws off
            // for the rest of the session.
            if (_playback.IsPlaying) _playback.Stop();
        }
    }
}
