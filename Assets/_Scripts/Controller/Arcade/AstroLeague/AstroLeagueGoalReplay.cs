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
        /// </summary>
        public void Play(float windowSeconds)
        {
            if (IsPlaying || _count < 2 || _ball == null || _settings == null) return;

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
                _cameraManager?.SetupReplayCameraFollow(_ghostRoot.transform);

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
                    // Root faces the direction of travel so the follow camera trails the shot;
                    // the recorded tumble spins the visual child, never the camera.
                    if (travel.sqrMagnitude > 0.01f)
                        _ghostRoot.transform.rotation = Quaternion.LookRotation(travel.normalized, Vector3.up);
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
                _cameraManager?.RestoreGameplayCamera();
                _playCts?.Dispose();
                _playCts = null;
                IsPlaying = false;
            }
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
