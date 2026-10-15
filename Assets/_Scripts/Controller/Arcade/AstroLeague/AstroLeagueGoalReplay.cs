using System;
using System.Threading;
using CosmicShore.Utility;
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
    ///
    /// <para>The samples and their interpolation are the Theater's (<see cref="TheaterPose"/>,
    /// <see cref="TheaterTrack.TryEvaluate"/>), the same ones every match replay plays back through
    /// (<c>Docs/REPLAYS.md</c>), so a goal replay and a match replay cannot disagree about where a
    /// recorded object was between two samples. What stays here is what only this shot needs: the
    /// short ring buffer, the ghost ball and the broadcast camera.</para>
    /// </summary>
    public class AstroLeagueGoalReplay : MonoBehaviour
    {
        /// <summary>
        /// How far back, in replay seconds, the ghost looks to find its direction of travel. About
        /// one physics step, so the heading is the slope of the segment the ghost is on.
        /// </summary>
        const float TravelProbeSeconds = 0.02f;

        AstroLeagueBall _ball;
        AstroLeagueSettingsSO _settings;
        CameraManager _cameraManager;

        TheaterPose[] _samples;
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
            _samples = new TheaterPose[capacity];
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
            _samples[_head] = new TheaterPose
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
                var flight = new TheaterTrack();
                flight.Poses.Capacity = n;
                for (int i = 0; i < n; i++)
                    flight.Poses.Add(_samples[(_head - (n - 1 - i) + _samples.Length * 2) % _samples.Length]);

                var first = flight.Poses[0];
                float span = flight.EndTime - flight.StartTime;
                if (span < 0.2f) return;

                float playWindow = Mathf.Max(0.5f, windowSeconds * _settings.goalReplayWindowFraction);
                float speed = Mathf.Max(_settings.goalReplayMinPlaybackSpeed, span / playWindow);

                BuildGhost(first);
                // Aim the ghost down the shot from frame one (orients its motion trail).
                Vector3 firstTravel = flight.Poses[1].Position - first.Position;
                if (firstTravel.sqrMagnitude > 0.0001f)
                    _ghostRoot.transform.rotation = Quaternion.LookRotation(firstTravel.normalized, Vector3.up);

                // Broadcast framing: the camera holds a FIXED vantage beside the whole recorded
                // flight (elevated, pulled back to fit it in the FOV) and PANS to the action -
                // it does not chase the ball at a fixed distance.
                _replayCam = _cameraManager != null ? _cameraManager.BeginManualReplayCamera() : null;
                if (_replayCam != null)
                {
                    _replayCam.position = ComputeVantage(flight);
                    Vector3 toStart = first.Position - _replayCam.position;
                    if (toStart.sqrMagnitude > 0.01f)
                        _replayCam.rotation = Quaternion.LookRotation(toStart.normalized, Vector3.up);
                }

                // Bloom the ghost in (continuity - nothing pops in), then retrace the shot.
                await ScaleGhostAsync(Vector3.zero, _ghostVisualScale, 0.25f, token);

                float elapsed = 0f;
                while (elapsed * speed < span)
                {
                    token.ThrowIfCancellationRequested();

                    float replayTime = flight.StartTime + elapsed * speed;
                    if (!flight.TryEvaluate(replayTime, out Vector3 position, out Quaternion tumble, out _))
                        break;

                    // Direction of travel: the slope just behind the ghost, measured in replay time
                    // so it does not depend on the frame rate.
                    Vector3 travel = Vector3.zero;
                    float back = Mathf.Min(TravelProbeSeconds, replayTime - flight.StartTime);
                    if (back > 1e-4f && flight.TryEvaluate(replayTime - back, out Vector3 behind, out _, out _))
                        travel = (position - behind) / back;

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
                        _ghostVisual.rotation = tumble;

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
        Vector3 ComputeVantage(TheaterTrack flight)
        {
            var poses = flight.Poses;
            int n = poses.Count;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; i++) centroid += poses[i].Position;
            centroid /= n;

            float pathRadius = 10f * _arenaScale; // floor so a short tap-in still frames sanely
            for (int i = 0; i < n; i++)
                pathRadius = Mathf.Max(pathRadius, Vector3.Distance(centroid, poses[i].Position));

            Vector3 shotLine = poses[n - 1].Position - poses[0].Position;
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

        void BuildGhost(TheaterPose first)
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
