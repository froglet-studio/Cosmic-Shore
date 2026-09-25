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
    /// and replays the fleet's real hulls as domain-coloured ghosts inside the
    /// <see cref="TheaterStage"/> — a recording AREA, with the live world masked off the camera and
    /// four shots to watch it from (free-flying, orbit, chase, static). It records nothing else
    /// yet; prisms are P1 and everything that moves is P2 (see <c>Docs/THEATER.md</c>).</para>
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
        /// <summary>
        /// How much larger the dev overlay is drawn than IMGUI's own 1x. Two, because the team
        /// asked for it and because the theater is read at arm's length from a desk rather than
        /// leaned into — every control on it is a deliberate, occasional press.
        /// </summary>
        const float OverlayScale = 2f;

        /// <summary>Panel width and row height in LOGICAL units, before <see cref="OverlayScale"/>.</summary>
        const float PanelWidth = 288f;
        const float Row = 24f;

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

            if (_playback.IsPlaying)
            {
                // The pad transport is polled ONLY inside the theater. Outside it the D-pad and the
                // south button belong to whatever is on screen, and a director's shortcut that
                // fires during a match is a control the player did not press.
                TheaterGesture.ReadPadTransport(out int shotStep, out int pilotStep, out bool togglePause);
                if (shotStep != 0) _playback.CycleShot(shotStep);
                if (pilotStep != 0) _playback.CycleFollow(pilotStep);
                if (togglePause) _playback.TogglePause();

                int shot = TheaterGesture.ShotRequestedThisFrame();
                if (shot >= 0) _playback.SetShotByIndex(shot);
                if (TheaterGesture.NextPilotRequestedThisFrame()) _playback.CycleFollow(1);

                _playback.Tick(Time.unscaledDeltaTime);
            }
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

        /// <summary>
        /// The dev overlay. <b>IMGUI on purpose</b>: it needs no canvas, no prefab and no scene
        /// wiring, so it exists in every scene the director does — which is the only reason a
        /// zero-wire tool can have controls at all. It is drawn at
        /// <see cref="OverlayScale"/> through <c>GUI.matrix</c> rather than by doubling every rect,
        /// so the FONT scales with the buttons; a 2x button wearing 1x text reads as a bug.
        /// </summary>
        void OnGUI()
        {
            bool showMessage = Time.unscaledTime - _lastMessageTime < 5f;
            if (!_recorder.IsRecording && !_playback.IsPlaying && !showMessage) return;

            // Scale down on a narrow screen rather than letting the panel run off it.
            float scale = Mathf.Min(OverlayScale, Screen.width / (PanelWidth + 40f));
            Matrix4x4 restore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float logicalWidth = Screen.width / scale;
            float x = logicalWidth - PanelWidth - 12f;
            float y = 12f;

            if (_recorder.IsRecording)
            {
                GUI.Label(new Rect(x, y, PanelWidth, Row),
                    $"[REC] {_recorder.RecordedSeconds:0.0}s   {_recorder.TrackCount} vessels   " +
                    $"{_recorder.EstimatedBytes / 1024f:0} KB      [9] stop");
                y += Row + 2f;
            }

            if (_playback.IsPlaying)
            {
                string subject = _playback.Shot is TheaterShot.Chase or TheaterShot.Static
                    ? "  " + _playback.FollowName
                    : _playback.Shot == TheaterShot.Free ? $"  x{_playback.FreeCameraGear:0.##}" : string.Empty;

                GUI.Label(new Rect(x, y, PanelWidth, Row),
                    $"[THEATER] {_playback.Position:0.0} / {_playback.Duration:0.0}s   " +
                    $"{(_playback.IsPaused ? "PAUSED" : $"x{_playback.Speed:0.##}")}   " +
                    $"{_playback.Shot}{subject}");
                y += Row + 2f;

                // Transport.
                float bx = x;
                if (Button(ref bx, y, 58f, _playback.IsPaused ? "Play" : "Pause")) _playback.TogglePause();
                if (Button(ref bx, y, 44f, "-5s")) _playback.Nudge(-5f);
                if (Button(ref bx, y, 44f, "+5s")) _playback.Nudge(5f);
                if (Button(ref bx, y, 54f, "Slower")) _playback.Speed = Mathf.Max(0.1f, _playback.Speed * 0.5f);
                if (Button(ref bx, y, 54f, "Faster")) _playback.Speed = Mathf.Min(8f, _playback.Speed * 2f);
                y += Row + 2f;

                // Shots.
                bx = x;
                if (Button(ref bx, y, 48f, "Free")) _playback.Shot = TheaterShot.Free;
                if (Button(ref bx, y, 50f, "Orbit")) _playback.Shot = TheaterShot.Orbit;
                if (Button(ref bx, y, 52f, "Chase")) _playback.Shot = TheaterShot.Chase;
                if (Button(ref bx, y, 54f, "Static")) _playback.Shot = TheaterShot.Static;
                if (Button(ref bx, y, 50f, "Pilot >")) _playback.CycleFollow(1);
                y += Row + 4f;

                GUI.Label(new Rect(x, y, PanelWidth, Row * 3f),
                    "[8] leave   [1-4] shot   [5] next pilot\n" +
                    "pad: sticks fly, triggers climb, D-pad shot/pilot, A pause\n" +
                    "kb: WASD + QE, right-mouse look, Shift boost, Ctrl crawl");
                y += Row * 3f + 4f;
            }

            if (showMessage) GUI.Label(new Rect(x, y, PanelWidth, Row * 2f), _lastMessage);

            GUI.matrix = restore;
        }

        /// <summary>A button in a left-to-right run, advancing the cursor past itself.</summary>
        static bool Button(ref float x, float y, float width, string label)
        {
            bool pressed = GUI.Button(new Rect(x, y, width, Row), label);
            x += width + 4f;
            return pressed;
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
