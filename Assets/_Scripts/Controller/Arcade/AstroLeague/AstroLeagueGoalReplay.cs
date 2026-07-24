using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Per-peer goal replay for Astro League. Every FixedUpdate the (already replicated) ball
    /// flight is recorded into a ring buffer; on a goal the controller plays it back as a
    /// visual-only GHOST ball retracing the shot on the shared END camera (the "replay
    /// camera") while the real arena resets behind it - vessels re-park, speeds zero, the
    /// field prisms sweep clean. Purely local on every peer: the ball's position/rotation
    /// replicate anyway, so all peers hold the same trajectory and no extra networking is
    /// needed. Continuity law: the ghost blooms in, shrinks out, and the recording never
    /// crosses a kickoff (cleared at every GO).
    ///
    /// Added at runtime by <see cref="AstroLeagueController"/> (no scene wiring) and driven
    /// through <see cref="Configure"/> / <see cref="Play"/> / <see cref="Stop"/> /
    /// <see cref="ClearRecording"/>.
    /// </summary>
    public class AstroLeagueGoalReplay : MonoBehaviour
    {
        struct Sample
        {
            public float Time;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        AstroLeagueBall _ball;
        AstroLeagueSettingsSO _settings;
        CameraManager _cameraManager;

        Sample[] _samples;
        int _head = -1; // index of the newest sample
        int _count;

        GameObject _ghostRoot;
        Transform _ghostVisual;
        Vector3 _ghostVisualScale;
        float _arenaScale = 1f;
        Transform _replayCam; // manually-driven end camera rig while a replay runs
        CancellationTokenSource _playCts;

        /// <summary>True while a ghost playback is on screen (recording pauses meanwhile).</summary>
        public bool IsPlaying { get; private set; }

        public void Configure(AstroLeagueBall ball, AstroLeagueSettingsSO settings, CameraManager cameraManager)
        {
            _ball = ball;
            _settings = settings;
            _cameraManager = cameraManager;

            float recordSeconds = Mathf.Max(1f, settings != null ? settings.goalReplayRecordSeconds : 4f);
            int capacity = Mathf.Max(8, Mathf.CeilToInt(recordSeconds / Time.fixedDeltaTime));
            _samples = new Sample[capacity];
            ClearRecording();
        }

        void FixedUpdate()
        {
            // Record only live flight: a hidden (detonated) or frozen (kickoff showpiece) ball is
            // not part of the shot, and recording pauses during playback so the buffer still holds
            // the goal when a late Stop() inspects it.
            if (IsPlaying || _samples == null || _ball == null) return;
            if (_ball.IsHidden || _ball.IsFrozen) return;

            _head = (_head + 1) % _samples.Length;
            _samples[_head] = new Sample
            {
                Time = Time.time,
                Position = _ball.transform.position,
                Rotation = _ball.transform.rotation
            };
            if (_count < _samples.Length) _count++;
        }

        /// <summary>Forget the recorded flight - called at every kickoff GO so a replay never crosses a reset.</summary>
        public void ClearRecording()
        {
            _head = -1;
            _count = 0;
        }

        /// <summary>
        /// Play the recorded flight as a ghost on the replay camera, fitted into
        /// <paramref name="windowSeconds"/> (the celebration + kickoff-freeze span). Playback speed
        /// derives from the recording length (slow-mo for short recordings, floored by settings);
        /// the gameplay camera is restored when playback finishes or on <see cref="Stop"/>.
        /// <paramref name="arenaScale"/> sizes the replay-camera offset with the court.
        /// </summary>
        public void Play(float windowSeconds, float arenaScale = 1f)
        {
            if (IsPlaying || _count < 2 || _ball == null || _settings == null) return;

            _arenaScale = Mathf.Max(0.01f, arenaScale);
            _playCts = new CancellationTokenSource();
            PlayAsync(windowSeconds, _playCts.Token).Forget();
        }

        /// <summary>Abort a running playback (kickoff GO / match end) - restores the gameplay camera.</summary>
        public void Stop()
        {
            if (IsPlaying) _playCts?.Cancel();
        }

        async UniTaskVoid PlayAsync(float windowSeconds, CancellationToken token)
        {
            IsPlaying = true;
            try
            {
                // Snapshot the ring oldest → newest; recording is paused while IsPlaying.
                int n = _count;
                var flight = new Sample[n];
                for (int i = 0; i < n; i++)
                    flight[i] = _samples[(_head - (n - 1 - i) + _samples.Length * 2) % _samples.Length];

                float span = flight[n - 1].Time - flight[0].Time;
                if (span < 0.2f) return;

                float playWindow = Mathf.Max(0.5f, windowSeconds * _settings.goalReplayWindowFraction);
                float speed = Mathf.Max(_settings.goalReplayMinPlaybackSpeed, span / playWindow);

                BuildGhost(flight[0]);
                // Aim the ghost down the shot from frame one (orients its motion trail).
                Vector3 firstTravel = flight[1].Position - flight[0].Position;
                if (firstTravel.sqrMagnitude > 0.0001f)
                    _ghostRoot.transform.rotation = Quaternion.LookRotation(firstTravel.normalized, Vector3.up);

                // Broadcast framing: the camera holds a FIXED vantage beside the whole recorded
                // flight (elevated, pulled back to fit it in the FOV) and PANS to the action -
                // it does not chase the ball at a fixed distance.
                _replayCam = _cameraManager != null ? _cameraManager.BeginManualReplayCamera() : null;
                if (_replayCam != null)
                {
                    _replayCam.position = ComputeVantage(flight, n);
                    Vector3 toStart = flight[0].Position - _replayCam.position;
                    if (toStart.sqrMagnitude > 0.01f)
                        _replayCam.rotation = Quaternion.LookRotation(toStart.normalized, Vector3.up);
                }

                // Bloom the ghost in (continuity - nothing pops in), then retrace the shot.
                await ScaleGhostAsync(Vector3.zero, _ghostVisualScale, 0.25f, token);

                float elapsed = 0f;
                int cursor = 0;
                while (elapsed * speed < span)
                {
                    token.ThrowIfCancellationRequested();

                    float replayTime = flight[0].Time + elapsed * speed;
                    while (cursor < n - 2 && flight[cursor + 1].Time < replayTime) cursor++;

                    Sample a = flight[cursor];
                    Sample b = flight[Mathf.Min(cursor + 1, n - 1)];
                    float dt = Mathf.Max(1e-4f, b.Time - a.Time);
                    float t = Mathf.Clamp01((replayTime - a.Time) / dt);

                    Vector3 position = Vector3.LerpUnclamped(a.Position, b.Position, t);
                    Vector3 travel = (b.Position - a.Position) / dt;

                    _ghostRoot.transform.position = position;
                    // Root faces the direction of travel (orients the motion trail, smoothed);
                    // the recorded tumble spins the visual child.
                    if (travel.sqrMagnitude > 0.01f)
                    {
                        var travelRot = Quaternion.LookRotation(travel.normalized, Vector3.up);
                        _ghostRoot.transform.rotation = Quaternion.Slerp(
                            _ghostRoot.transform.rotation, travelRot,
                            1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
                    }

                    // The broadcast pan: rotate (never translate) toward the ghost, smoothed so
                    // the ball leads the frame a touch like a real camera operator.
                    if (_replayCam != null)
                    {
                        Vector3 toGhost = position - _replayCam.position;
                        if (toGhost.sqrMagnitude > 0.01f)
                        {
                            var panRot = Quaternion.LookRotation(toGhost.normalized, Vector3.up);
                            _replayCam.rotation = Quaternion.Slerp(_replayCam.rotation, panRot,
                                1f - Mathf.Exp(-Mathf.Max(0.1f, _settings.goalReplayPanSpeed) * Time.unscaledDeltaTime));
                        }
                    }
                    if (_ghostVisual != null)
                        _ghostVisual.rotation = Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, t);

                    // Unscaled: the solo celebration slow-mo must not crawl the replay itself.
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    elapsed += Time.unscaledDeltaTime;
                }

                // Shrink out at the goal moment (continuity - nothing pops out).
                await ScaleGhostAsync(_ghostVisualScale, Vector3.zero, 0.2f, token);
            }
            catch (OperationCanceledException) { /* kickoff GO / match end / scene teardown */ }
            finally
            {
                if (_ghostRoot != null) Destroy(_ghostRoot);
                _ghostRoot = null;
                _ghostVisual = null;
                _replayCam = null;
                _cameraManager?.RestoreGameplayCamera();
                _playCts?.Dispose();
                _playCts = null;
                IsPlaying = false;
            }
        }

        /// <summary>
        /// The broadcast vantage: beside the recorded flight (perpendicular to the overall shot
        /// line), elevated, pulled back far enough that the whole flight fits the camera's field
        /// of view times the framing margin. The camera then only PANS from here.
        /// </summary>
        Vector3 ComputeVantage(Sample[] flight, int n)
        {
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; i++) centroid += flight[i].Position;
            centroid /= n;

            float pathRadius = 10f * _arenaScale; // floor so a short tap-in still frames sanely
            for (int i = 0; i < n; i++)
                pathRadius = Mathf.Max(pathRadius, Vector3.Distance(centroid, flight[i].Position));

            Vector3 shotLine = flight[n - 1].Position - flight[0].Position;
            Vector3 side = Vector3.Cross(
                shotLine.sqrMagnitude > 0.01f ? shotLine.normalized : Vector3.forward, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            else side.Normalize();

            float fov = _cameraManager != null ? _cameraManager.ReplayCameraFieldOfView : 60f;
            float halfTan = Mathf.Max(0.1f, Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
            float distance = Mathf.Max(60f * _arenaScale,
                pathRadius * Mathf.Max(1f, _settings.goalReplayFramingMargin) / halfTan);

            return centroid
                   + side * distance
                   + Vector3.up * (distance * _settings.goalReplayVantageElevation);
        }

        void BuildGhost(Sample first)
        {
            _ghostRoot = new GameObject("AstroLeagueGoalReplayGhost");
            _ghostRoot.transform.position = first.Position;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(_ghostRoot.transform, false);
            var meshFilter = visual.AddComponent<MeshFilter>();
            var meshRenderer = visual.AddComponent<MeshRenderer>();
            var ghostTrail = visual.AddComponent<TrailRenderer>();
            _ball.DressReplayGhost(meshFilter, meshRenderer, ghostTrail);

            _ghostVisual = visual.transform;
            _ghostVisualScale = _ghostVisual.localScale;
            _ghostVisual.localScale = Vector3.zero; // bloomed in by ScaleGhostAsync
            ghostTrail.Clear();
        }

        async UniTask ScaleGhostAsync(Vector3 from, Vector3 to, float seconds, CancellationToken token)
        {
            float t = 0f;
            while (t < 1f && _ghostVisual != null)
            {
                token.ThrowIfCancellationRequested();
                _ghostVisual.localScale = Vector3.LerpUnclamped(from, to, t);
                await UniTask.Yield(PlayerLoopTiming.Update);
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
            }
            if (_ghostVisual != null) _ghostVisual.localScale = to;
        }

        void OnDestroy()
        {
            _playCts?.Cancel();
            _playCts?.Dispose();
            _playCts = null;
            if (_ghostRoot != null) Destroy(_ghostRoot);
        }
    }
}
