using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Game.CameraSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// Controls a pre-game cinematic flythrough that shows the level/map
    /// before the player presses Ready. Supports multiple camera behaviors
    /// configured via <see cref="PreGameCinematicSetupSO"/>.
    ///
    /// Flow:
    /// 1. MiniGameHUD calls Play() when connecting panel finishes.
    /// 2. Camera runs the configured cinematic type (orbit, track flyover, etc.).
    /// 3. Camera smoothly transitions to behind the player vessel.
    /// 4. OnCinematicFinished fires so the HUD can show the Ready button.
    /// 5. Skip button allows skipping at any time.
    /// </summary>
    public class PreGameCinematicController : MonoBehaviour
    {
        [Header("Cinematic Setup")]
        [Tooltip("SO-driven configuration. If null, uses legacy Inspector values.")]
        [SerializeField] private PreGameCinematicSetupSO setup;

        [Header("Waypoints (Legacy)")]
        [Tooltip("Local-space waypoints the camera visits. If empty, auto-generates a circle path.")]
        [SerializeField] private Transform[] waypoints;

        [Header("Auto-Generated Path (legacy fallback when no SO assigned)")]
        [SerializeField] private float orbitRadius = 150f;
        [SerializeField] private float orbitHeight = 60f;
        [SerializeField] private float orbitDuration = 6f;
        [SerializeField] private int orbitSegments = 4;

        [Header("Timing (legacy fallback)")]
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

        // Injected data for specialized modes
        private List<Transform> _allPlayerTransforms;
        private SegmentSpawner _segmentSpawner;

        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// Assign the SO config at runtime (called by MiniGameHUD when resolved from library).
        /// </summary>
        public void SetSetup(PreGameCinematicSetupSO cinematicSetup)
        {
            setup = cinematicSetup;
        }

        /// <summary>
        /// Provide all player transforms for PlayerShowcase mode.
        /// </summary>
        public void SetPlayerTransforms(List<Transform> playerTransforms)
        {
            _allPlayerTransforms = playerTransforms;
        }

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

            if (_cameraTransform == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera != null)
                    _cameraTransform = _mainCamera.transform;
            }

            if (_cameraTransform == null)
            {
                CSDebug.LogWarning("[PreGameCinematic] No camera found, skipping cinematic.");
                OnCinematicFinished?.Invoke();
                return;
            }

            _isPlaying = true;
            _skipped = false;

            SetSkipButtonVisible(true);

            if (setup != null)
                _runningCoroutine = StartCoroutine(RunConfiguredCinematic(lookAtCenter));
            else
                _runningCoroutine = StartCoroutine(RunLegacyCinematic(lookAtCenter));
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

        #region SO-Driven Cinematic Dispatch

        private IEnumerator RunConfiguredCinematic(Vector3 center)
        {
            switch (setup.cinematicType)
            {
                case PreGameCinematicType.Orbit:
                    yield return StartCoroutine(RunOrbitCinematic(center));
                    break;

                case PreGameCinematicType.TrackFlyover:
                    yield return StartCoroutine(RunTrackFlyoverCinematic(center));
                    break;

                case PreGameCinematicType.PlayerShowcase:
                    yield return StartCoroutine(RunPlayerShowcaseCinematic(center));
                    break;

                case PreGameCinematicType.WideOrbit:
                    yield return StartCoroutine(RunWideOrbitCinematic(center));
                    break;

                case PreGameCinematicType.SpiralReveal:
                    yield return StartCoroutine(RunSpiralRevealCinematic(center));
                    break;

                default:
                    yield return StartCoroutine(RunOrbitCinematic(center));
                    break;
            }

            yield return StartCoroutine(TransitionToPlayer(setup.transitionToPlayerTime));
            FinishCinematic();
        }

        #endregion

        #region Orbit Cinematic

        private IEnumerator RunOrbitCinematic(Vector3 center)
        {
            GenerateOrbitPathFromSetup(center, setup.orbitRadius, setup.orbitHeight,
                setup.orbitSegments, out var positions, out var rotations);

            float travelTime = setup.orbitDuration / Mathf.Max(1, positions.Length - 1);

            yield return StartCoroutine(FlyThroughPoints(positions, rotations, travelTime, setup.pauseBetweenKeyframes));
        }

        #endregion

        #region Track Flyover Cinematic (Hex Race)

        private IEnumerator RunTrackFlyoverCinematic(Vector3 center)
        {
            // Find the SegmentSpawner in the scene to get track layout
            if (_segmentSpawner == null)
                _segmentSpawner = FindAnyObjectByType<SegmentSpawner>();

            if (_segmentSpawner == null || _segmentSpawner.StraightLineLength <= 0)
            {
                // Fallback to orbit if no track found
                yield return StartCoroutine(RunOrbitCinematic(center));
                yield break;
            }

            // Build flyover path along the track
            var spawnerTransform = _segmentSpawner.transform;
            var trackOrigin = _segmentSpawner.origin + spawnerTransform.position;
            float segmentLength = _segmentSpawner.StraightLineLength;
            int segmentCount = _segmentSpawner.NumberOfSegments;
            float totalTrackLength = segmentLength * segmentCount;
            float height = setup.flyoverHeight;

            // Start position: above and slightly before the track
            Vector3 startPos = trackOrigin + new Vector3(0, height, -setup.flyoverLeadDistance);

            // End position: near the end of the track
            Vector3 endPos = trackOrigin + new Vector3(0, height, totalTrackLength * 0.85f);

            // Generate intermediate waypoints along the track for a smooth path
            int waypointCount = Mathf.Max(3, segmentCount + 1);
            var positions = new Vector3[waypointCount];
            var rotations = new Quaternion[waypointCount];

            for (int i = 0; i < waypointCount; i++)
            {
                float t = (float)i / (waypointCount - 1);
                Vector3 pos = Vector3.Lerp(startPos, endPos, t);

                // Add gentle sinusoidal lateral drift for visual interest
                pos.x += Mathf.Sin(t * Mathf.PI * 2f) * 15f;

                positions[i] = pos;

                // Look direction: angled down toward the track
                Vector3 trackPoint = Vector3.Lerp(trackOrigin, trackOrigin + new Vector3(0, 0, totalTrackLength), t);
                Vector3 lookDir = (trackPoint - pos).normalized;

                // Mix in a downward angle
                Vector3 downAngled = Quaternion.AngleAxis(setup.flyoverLookDownAngle, Vector3.right) * Vector3.forward;
                lookDir = Vector3.Slerp(lookDir, downAngled, 0.3f);

                rotations[i] = lookDir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(lookDir)
                    : Quaternion.identity;
            }

            float travelTime = setup.flyoverDuration / Mathf.Max(1, waypointCount - 1);
            yield return StartCoroutine(FlyThroughPoints(positions, rotations, travelTime, 0f));
        }

        #endregion

        #region Player Showcase Cinematic (Crystal Capture)

        private IEnumerator RunPlayerShowcaseCinematic(Vector3 center)
        {
            // Optional initial orbit
            if (setup.doInitialOrbitBeforeShowcase)
            {
                GenerateOrbitPathFromSetup(center, setup.orbitRadius, setup.orbitHeight,
                    setup.orbitSegments, out var orbitPositions, out var orbitRotations);

                float orbitTravelTime = setup.initialOrbitDuration / Mathf.Max(1, orbitPositions.Length - 1);
                yield return StartCoroutine(FlyThroughPoints(orbitPositions, orbitRotations, orbitTravelTime, 0f));
            }

            if (_skipped) yield break;

            // Focus on each player vessel
            var playerTargets = _allPlayerTransforms;
            if (playerTargets == null || playerTargets.Count == 0)
            {
                // Nothing to showcase, done
                yield break;
            }

            for (int i = 0; i < playerTargets.Count && !_skipped; i++)
            {
                var target = playerTargets[i];
                if (target == null) continue;

                // Position camera in front and above the player
                float angle = (float)i / playerTargets.Count * 360f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Sin(angle) * setup.playerFocusDistance,
                    setup.playerFocusHeight,
                    Mathf.Cos(angle) * setup.playerFocusDistance
                );

                Vector3 targetPos = target.position + offset;
                Vector3 lookDir = target.position - targetPos;
                Quaternion targetRot = lookDir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(lookDir)
                    : _cameraTransform.rotation;

                // Smooth transition to this player
                yield return StartCoroutine(MoveBetweenPoints(
                    _cameraTransform.position, _cameraTransform.rotation,
                    targetPos, targetRot,
                    setup.perPlayerFocusTime * 0.4f));

                // Hold on player briefly
                if (!_skipped)
                    yield return new WaitForSeconds(setup.perPlayerFocusTime * 0.6f);
            }
        }

        #endregion

        #region Wide Orbit Cinematic (Joust)

        private IEnumerator RunWideOrbitCinematic(Vector3 center)
        {
            int count = Mathf.Max(2, setup.orbitSegments + 1);
            var positions = new Vector3[count];
            var rotations = new Quaternion[count];

            float angleStep = 360f / (count - 1);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                float angle = i * angleStep * Mathf.Deg2Rad;

                // Height varies sinusoidally for dramatic sweeping
                float heightWave = Mathf.Sin(t * Mathf.PI * setup.sweepCycles) * setup.heightVariation;
                float currentHeight = setup.orbitHeight + heightWave;

                Vector3 pos = center + new Vector3(
                    Mathf.Sin(angle) * setup.orbitRadius,
                    currentHeight,
                    Mathf.Cos(angle) * setup.orbitRadius
                );
                positions[i] = pos;

                Vector3 dir = center - pos;
                rotations[i] = dir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(dir)
                    : Quaternion.identity;
            }

            float travelTime = setup.orbitDuration / Mathf.Max(1, count - 1);
            yield return StartCoroutine(FlyThroughPoints(positions, rotations, travelTime, setup.pauseBetweenKeyframes));
        }

        #endregion

        #region Spiral Reveal Cinematic (Freestyle)

        private IEnumerator RunSpiralRevealCinematic(Vector3 center)
        {
            float duration = setup.spiralDuration;
            float elapsed = 0f;

            float startAngle = 0f;
            float totalAngle = setup.spiralRotations * 360f * Mathf.Deg2Rad;

            while (elapsed < duration && !_skipped)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                float currentAngle = startAngle + t * totalAngle;
                float currentDistance = Mathf.Lerp(setup.spiralStartDistance, setup.spiralEndDistance, t);
                float currentHeight = Mathf.Lerp(setup.spiralStartHeight, setup.spiralEndHeight, t);

                Vector3 pos = center + new Vector3(
                    Mathf.Sin(currentAngle) * currentDistance,
                    currentHeight,
                    Mathf.Cos(currentAngle) * currentDistance
                );

                _cameraTransform.position = pos;

                Vector3 dir = center - pos;
                if (dir.sqrMagnitude > 0.001f)
                    _cameraTransform.rotation = Quaternion.LookRotation(dir);

                yield return null;
            }
        }

        #endregion

        #region Shared Helpers

        private void GenerateOrbitPathFromSetup(Vector3 center, float radius, float height,
            int segments, out Vector3[] positions, out Quaternion[] rotations)
        {
            int count = Mathf.Max(2, segments + 1);
            positions = new Vector3[count];
            rotations = new Quaternion[count];

            float angleStep = 360f / (count - 1);

            for (int i = 0; i < count; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(
                    Mathf.Sin(angle) * radius,
                    height,
                    Mathf.Cos(angle) * radius
                );
                positions[i] = pos;

                Vector3 dir = center - pos;
                rotations[i] = dir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(dir)
                    : Quaternion.identity;
            }
        }

        private IEnumerator FlyThroughPoints(Vector3[] positions, Quaternion[] rotations,
            float travelTime, float pauseTime)
        {
            for (int i = 0; i < positions.Length - 1 && !_skipped; i++)
            {
                yield return StartCoroutine(MoveBetweenPoints(
                    positions[i], rotations[i],
                    positions[i + 1], rotations[i + 1],
                    travelTime));

                if (!_skipped && pauseTime > 0)
                    yield return new WaitForSeconds(pauseTime);
            }
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

        private IEnumerator TransitionToPlayer(float duration)
        {
            if (_playerTarget == null)
                yield break;

            Vector3 startPos = _cameraTransform.position;
            Quaternion startRot = _cameraTransform.rotation;

            Vector3 behindPlayer = _playerTarget.position
                + _playerTarget.rotation * _playerCameraFollowOffset;
            Vector3 lookDir = _playerTarget.position - behindPlayer;
            Quaternion targetRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir, _playerTarget.up)
                : startRot;

            float actualDuration = _skipped ? 0.3f : duration;
            float elapsed = 0f;

            while (elapsed < actualDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / actualDuration));

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

        #endregion

        #region Legacy Path (no SO assigned)

        private IEnumerator RunLegacyCinematic(Vector3 center)
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
                GenerateOrbitPathFromSetup(center, orbitRadius, orbitHeight, orbitSegments,
                    out positions, out rotations);
                waypointTravelTime = orbitDuration / Mathf.Max(1, positions.Length - 1);
            }

            yield return StartCoroutine(FlyThroughPoints(positions, rotations, waypointTravelTime, waypointPauseTime));
            yield return StartCoroutine(TransitionToPlayer(transitionToPlayerTime));

            FinishCinematic();
        }

        #endregion

        private void FinishCinematic()
        {
            _isPlaying = false;
            _runningCoroutine = null;

            SetSkipButtonVisible(false);

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

        private void Update()
        {
            if (!_isPlaying) return;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                Skip();
        }

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
