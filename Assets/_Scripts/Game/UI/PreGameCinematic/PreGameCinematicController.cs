using System;
using System.Collections;
using CosmicShore.Game.CameraSystem;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Controls a pre-game cinematic flythrough that shows the level/map
    /// before the player presses Ready. Attach to a GameObject in the game scene.
    ///
    /// Flow:
    /// 1. MiniGameHUD calls Play() when connecting panel finishes.
    /// 2. Camera flies through configurable waypoints relative to a scene center.
    /// 3. Camera smoothly transitions to behind the player vessel.
    /// 4. OnCinematicFinished fires so the HUD can show the Ready button.
    /// 5. Skip button allows skipping at any time.
    /// </summary>
    public class PreGameCinematicController : MonoBehaviour
    {
        [Header("Waypoints")]
        [Tooltip("Local-space waypoints the camera visits. If empty, auto-generates a circle path.")]
        [SerializeField] private Transform[] waypoints;

        [Header("Auto-Generated Path (used when no waypoints assigned)")]
        [SerializeField] private float orbitRadius = 150f;
        [SerializeField] private float orbitHeight = 60f;
        [SerializeField] private float orbitDuration = 6f;
        [SerializeField] private int orbitSegments = 4;

        [Header("Timing")]
        [SerializeField] private float waypointTravelTime = 2f;
        [SerializeField] private float waypointPauseTime = 0.3f;
        [SerializeField] private float transitionToPlayerTime = 1.5f;

        [Header("UI")]
        [SerializeField] private Button skipButton;
        [SerializeField] private CanvasGroup skipButtonCanvasGroup;

        public event Action OnCinematicFinished;

        private Camera _mainCamera;
        private Transform _cameraTransform;
        private Transform _playerTarget;
        private ICameraController _playerCameraController;
        private Coroutine _runningCoroutine;
        private bool _isPlaying;
        private bool _skipped;
        private Vector3 _playerCameraFollowOffset = new(0f, 10f, 0f);

        /// <summary>
        /// Whether the cinematic is currently playing.
        /// </summary>
        public bool IsPlaying => _isPlaying;

        private void Awake()
        {
            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);

            SetSkipButtonVisible(false);
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.onClick.RemoveListener(Skip);
        }

        /// <summary>
        /// Configure the skip button at runtime (used when auto-created by MiniGameHUD).
        /// </summary>
        public void SetupSkipButton(Button button, CanvasGroup canvasGroup)
        {
            if (skipButton != null)
                skipButton.onClick.RemoveListener(Skip);

            skipButton = button;
            skipButtonCanvasGroup = canvasGroup;

            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);

            SetSkipButtonVisible(false);
        }

        /// <summary>
        /// Start the cinematic flythrough.
        /// </summary>
        /// <param name="lookAtCenter">Center of the scene to look at during orbit.</param>
        /// <param name="playerTarget">The player vessel transform to transition to at the end.</param>
        public void Play(Vector3 lookAtCenter, Transform playerTarget)
        {
            if (_isPlaying) return;

            _playerTarget = playerTarget;
            _cameraTransform = null;

            // Prefer CameraManager for reliable camera access over Camera.main
            if (CameraManager.Instance != null)
            {
                _playerCameraController = CameraManager.Instance.GetActiveController();
                if (_playerCameraController is CustomCameraController pcc)
                {
                    _cameraTransform = pcc.transform;
                    _mainCamera = pcc.Camera;
                    _playerCameraFollowOffset = pcc.GetFollowOffset();
                    pcc.enabled = false;
                }
            }

            // Fallback to Camera.main
            if (_cameraTransform == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera != null)
                    _cameraTransform = _mainCamera.transform;
            }

            if (_cameraTransform == null)
            {
                Debug.LogWarning("[PreGameCinematic] No camera found, skipping cinematic.");
                OnCinematicFinished?.Invoke();
                return;
            }

            _isPlaying = true;
            _skipped = false;

            SetSkipButtonVisible(true);
            _runningCoroutine = StartCoroutine(RunCinematic(lookAtCenter));
        }

        public void Skip()
        {
            _skipped = true;
        }

        private void SetSkipButtonVisible(bool visible)
        {
            if (skipButtonCanvasGroup != null)
            {
                skipButtonCanvasGroup.alpha = visible ? 1f : 0f;
                skipButtonCanvasGroup.interactable = visible;
                skipButtonCanvasGroup.blocksRaycasts = visible;
            }
            else if (skipButton != null)
            {
                skipButton.gameObject.SetActive(visible);
            }
        }

        private IEnumerator RunCinematic(Vector3 center)
        {
            Vector3[] positions;
            Quaternion[] rotations;

            if (waypoints != null && waypoints.Length >= 2)
            {
                positions = new Vector3[waypoints.Length];
                rotations = new Quaternion[waypoints.Length];
                for (int i = 0; i < waypoints.Length; i++)
                {
                    positions[i] = waypoints[i].position;
                    rotations[i] = waypoints[i].rotation;
                }
            }
            else
            {
                // Auto-generate orbit positions around center
                GenerateOrbitPath(center, out positions, out rotations);
            }

            // Fly through waypoints
            for (int i = 0; i < positions.Length - 1 && !_skipped; i++)
            {
                yield return StartCoroutine(MoveBetweenPoints(
                    positions[i], rotations[i],
                    positions[i + 1], rotations[i + 1],
                    waypointTravelTime));

                if (!_skipped && waypointPauseTime > 0)
                    yield return new WaitForSeconds(waypointPauseTime);
            }

            // Transition to player camera position
            yield return StartCoroutine(TransitionToPlayer());

            FinishCinematic();
        }

        private void GenerateOrbitPath(Vector3 center, out Vector3[] positions, out Quaternion[] rotations)
        {
            int count = Mathf.Max(2, orbitSegments + 1);
            positions = new Vector3[count];
            rotations = new Quaternion[count];

            float angleStep = 360f / (count - 1);

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(
                    Mathf.Sin(angle) * orbitRadius,
                    orbitHeight,
                    Mathf.Cos(angle) * orbitRadius
                );
                positions[i] = pos;

                // Look at center
                Vector3 dir = center - pos;
                rotations[i] = dir != Vector3.zero
                    ? Quaternion.LookRotation(dir)
                    : Quaternion.identity;
            }

            // Update travel time to distribute evenly
            waypointTravelTime = orbitDuration / Mathf.Max(1, count - 1);
        }

        private IEnumerator MoveBetweenPoints(
            Vector3 fromPos, Quaternion fromRot,
            Vector3 toPos, Quaternion toRot,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !_skipped)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                _cameraTransform.position = Vector3.Lerp(fromPos, toPos, t);
                _cameraTransform.rotation = Quaternion.Slerp(fromRot, toRot, t);
                yield return null;
            }
        }

        private IEnumerator TransitionToPlayer()
        {
            if (_playerTarget == null)
            {
                yield break;
            }

            Vector3 startPos = _cameraTransform.position;
            Quaternion startRot = _cameraTransform.rotation;

            // Use the actual camera follow offset for the target position
            Vector3 behindPlayer = _playerTarget.position
                + _playerTarget.rotation * _playerCameraFollowOffset;
            Vector3 lookDir = _playerTarget.position - behindPlayer;
            Quaternion targetRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir, _playerTarget.up)
                : startRot;

            float duration = _skipped ? 0.3f : transitionToPlayerTime;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                // Recalculate target each frame in case player moves
                behindPlayer = _playerTarget.position
                    + _playerTarget.rotation * _playerCameraFollowOffset;
                lookDir = _playerTarget.position - behindPlayer;
                if (lookDir.sqrMagnitude > 0.001f)
                    targetRot = Quaternion.LookRotation(lookDir, _playerTarget.up);

                _cameraTransform.position = Vector3.Lerp(startPos, behindPlayer, t);
                _cameraTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
        }

        private void FinishCinematic()
        {
            _isPlaying = false;
            _runningCoroutine = null;

            SetSkipButtonVisible(false);

            // Re-enable the player camera controller
            if (_playerCameraController is CustomCameraController pcc)
            {
                pcc.enabled = true;
                pcc.SnapToTarget();
            }
            else if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SnapPlayerCameraToTarget();
            }

            OnCinematicFinished?.Invoke();
        }

        /// <summary>
        /// Force-stop the cinematic if the object is disabled mid-play.
        /// </summary>
        private void OnDisable()
        {
            if (_isPlaying)
            {
                if (_runningCoroutine != null)
                    StopCoroutine(_runningCoroutine);

                _isPlaying = false;
                SetSkipButtonVisible(false);

                if (_playerCameraController is CustomCameraController pcc)
                {
                    pcc.enabled = true;
                    pcc.SnapToTarget();
                }
            }
        }
    }
}
