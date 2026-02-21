using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Plays a pre-game camera fly-around before the countdown begins.
    /// Supports two modes:
    ///   • Orbit  – circles around <see cref="focusPoint"/> at configurable radius/height/speed.
    ///   • Waypoints – lerps through an ordered list of transforms.
    /// A skip button lets the player cut the cinematic short.
    /// Reference this component from MiniGameHUD.preGameCinematic.
    /// </summary>
    public class PreGameCinematicController : MonoBehaviour
    {
        public enum CinematicMode { Orbit, Waypoints }

        [Header("Mode")]
        [SerializeField] private CinematicMode mode = CinematicMode.Orbit;

        [Header("Orbit Settings")]
        [SerializeField] private Transform focusPoint;
        [SerializeField] private float orbitRadius = 40f;
        [SerializeField] private float orbitHeight = 15f;
        [SerializeField] private float orbitDuration = 6f;
        [SerializeField] private float orbitSpeed = 60f;

        [Header("Waypoint Settings")]
        [SerializeField] private List<Transform> waypoints = new();
        [SerializeField] private float waypointDuration = 6f;
        [SerializeField] private float waypointMoveSpeed = 8f;
        [SerializeField] private float waypointLookSpeed = 4f;

        [Header("Skip")]
        [SerializeField] private Button skipButton;

        /// <summary>Raised when the cinematic finishes (naturally or via skip).</summary>
        public event Action OnCinematicComplete;

        private CancellationTokenSource _cts;
        private Camera _cam;
        private bool _playing;

        /// <summary>True while the cinematic is still running.</summary>
        public bool IsPlaying => _playing;

        void OnDisable()
        {
            Cancel();
        }

        /// <summary>
        /// Begin the pre-game cinematic. Awaitable — resolves when the cinematic
        /// finishes naturally or the player presses Skip.
        /// </summary>
        public async UniTask PlayAsync(Camera camera, CancellationToken externalCt = default)
        {
            _cam = camera;
            if (!_cam) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _cts.Token;

            // Wire skip button
            if (skipButton)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.onClick.AddListener(OnSkipPressed);
            }

            _playing = true;

            try
            {
                switch (mode)
                {
                    case CinematicMode.Orbit:
                        await RunOrbit(ct);
                        break;
                    case CinematicMode.Waypoints:
                        await RunWaypoints(ct);
                        break;
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _playing = false;

                if (skipButton)
                {
                    skipButton.onClick.RemoveListener(OnSkipPressed);
                    skipButton.gameObject.SetActive(false);
                }

                OnCinematicComplete?.Invoke();
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnSkipPressed()
        {
            _cts?.Cancel();
        }

        #region Orbit

        private async UniTask RunOrbit(CancellationToken ct)
        {
            Vector3 center = focusPoint ? focusPoint.position : Vector3.zero;
            float elapsed = 0f;
            float angle = 0f;

            while (elapsed < orbitDuration)
            {
                ct.ThrowIfCancellationRequested();

                angle += orbitSpeed * Time.deltaTime;
                float rad = angle * Mathf.Deg2Rad;

                Vector3 pos = center + new Vector3(
                    Mathf.Sin(rad) * orbitRadius,
                    orbitHeight,
                    Mathf.Cos(rad) * orbitRadius
                );

                _cam.transform.position = pos;
                _cam.transform.LookAt(center);

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        #endregion

        #region Waypoints

        private async UniTask RunWaypoints(CancellationToken ct)
        {
            if (waypoints == null || waypoints.Count == 0) return;

            float elapsed = 0f;
            int idx = 0;
            Transform target = waypoints[idx];

            while (elapsed < waypointDuration)
            {
                ct.ThrowIfCancellationRequested();

                if (target)
                {
                    _cam.transform.position = Vector3.MoveTowards(
                        _cam.transform.position,
                        target.position,
                        waypointMoveSpeed * Time.deltaTime
                    );

                    Quaternion lookRot = Quaternion.LookRotation(target.position - _cam.transform.position);
                    _cam.transform.rotation = Quaternion.Slerp(
                        _cam.transform.rotation,
                        lookRot,
                        waypointLookSpeed * Time.deltaTime
                    );

                    // Advance to next waypoint when close
                    if (Vector3.Distance(_cam.transform.position, target.position) < 1f)
                    {
                        idx = (idx + 1) % waypoints.Count;
                        target = waypoints[idx];
                    }
                }

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        #endregion
    }
}
