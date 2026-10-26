using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.UI
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
    ///
    /// Per-mode intros: when the mode belongs to a family in <see cref="IntroCameraLibrarySO"/>,
    /// step 2 is one of that family's intros, picked at random (never the same one twice in a
    /// row), with the mode's own title, tagline and objective over it and letterbox bars. A mode
    /// in no family keeps the orbit below. Docs/IntroCameras/README.md.
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

        [Header("Per-Mode Intros")]
        [Tooltip("The family intros and each mode's words. Empty loads Resources/IntroCameraLibrary.")]
        [SerializeField] private IntroCameraLibrarySO introLibrary;

        public event Action OnCinematicFinished;

        private Camera _mainCamera;
        private Transform _cameraTransform;
        private Transform _playerTarget;
        private ICameraController _playerCameraController;
        private Coroutine _runningCoroutine;
        private bool _isPlaying;
        private bool _skipped;
        private Vector3 _playerCameraFollowOffset = new(0f, 10f, 0f);
        private float _playerFov;
        private IntroCameraOverlay _overlay;

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
            if (_isPlaying || !TryTakeCamera(playerTarget)) return;
            _runningCoroutine = StartCoroutine(RunCinematic(lookAtCenter));
        }

        /// <summary>
        /// Start the mode's intro: a random intro from its family, or the orbit when it has none.
        /// </summary>
        /// <param name="mode">The mode being played; picks the family and the words.</param>
        /// <param name="vessels">Every vessel in the match, for the intro's pilot anchors.</param>
        /// <param name="accent">The local pilot's domain colour, for the lower third.</param>
        public void Play(Vector3 lookAtCenter, Transform playerTarget, GameModes mode,
                         IReadOnlyList<IVessel> vessels, Color accent)
        {
            if (_isPlaying) return;

            if (!introLibrary) introLibrary = IntroCameraLibrarySO.Load();
            if (!introLibrary || !introLibrary.TryPick(mode, out var sequence))
            {
                Play(lookAtCenter, playerTarget);
                return;
            }

            if (!TryTakeCamera(playerTarget)) return;
            var scene = IntroCameraScene.Gather(lookAtCenter, playerTarget, vessels,
                                                introLibrary.ReferenceRadius, introLibrary.ScaleMultiplier);
            _overlay = CreateOverlay(introLibrary, accent);
            _runningCoroutine = StartCoroutine(RunIntro(sequence, introLibrary.WordsFor(mode), scene));
        }

        // Takes the camera from the player camera controller. False (and OnCinematicFinished
        // raised) when there is no camera to take.
        private bool TryTakeCamera(Transform playerTarget)
        {
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
                CSDebug.LogWarning("[PreGameCinematic] No camera found, skipping cinematic.");
                OnCinematicFinished?.Invoke();
                return false;
            }

            _playerFov = _mainCamera != null ? _mainCamera.fieldOfView : 60f;
            _isPlaying = true;
            _skipped = false;

            SetSkipButtonVisible(true);
            return true;
        }

        private IntroCameraOverlay CreateOverlay(IntroCameraLibrarySO style, Color accent)
        {
            // Under the canvas that holds Skip, just below it, so Skip draws on top of the bars.
            Transform parent = skipButton != null ? skipButton.transform.parent : null;
            int sibling = skipButton != null ? skipButton.transform.GetSiblingIndex() : -1;
            if (parent == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) parent = canvas.transform;
            }
            if (parent == null)
            {
                var go = new GameObject("IntroCameraCanvas", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                parent = go.transform;
            }
            return IntroCameraOverlay.Create(parent, sibling, style, accent);
        }

        /// <summary>
        /// While the cinematic plays, the gamepad's South face button (A on Xbox /
        /// Cross on PlayStation) triggers the on-screen Skip button - the same binding
        /// the in-game input strategy uses for the primary action. Mouse/touch still
        /// works through <see cref="skipButton"/>'s onClick.
        /// </summary>
        private void Update()
        {
            if (!_isPlaying || _skipped) return;

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
                Skip();
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

        private IEnumerator RunIntro(IntroSequence sequence, IntroModeWords words, IntroCameraScene scene)
        {
            var shots = sequence.Shots;
            float total = sequence.Duration;
            float lowerFrom = shots.Count > 0 ? total - Mathf.Max(0f, shots[shots.Count - 1].Duration) : total;
            float t = 0f;

            while (t < total && !_skipped)
            {
                t = Mathf.Min(total, t + Time.deltaTime);
                ApplyPose(PoseAt(shots, t, scene));
                DrawOverlay(shots, words, t, 1f, lowerFrom);
                yield return null;
            }

            // The hand-off: the bars open and the lower third holds while the camera settles.
            float handoff = _skipped ? 0.3f : transitionToPlayerTime;
            yield return StartCoroutine(TransitionToPlayer(p =>
            {
                if (_skipped) DrawOverlay(shots, null, -1f, 1f - p, float.MaxValue);
                else DrawOverlay(shots, words, total + p * handoff, 1f - p, lowerFrom);
            }));

            FinishCinematic();
        }

        private static IntroPose PoseAt(IReadOnlyList<IntroShot> shots, float t, IntroCameraScene scene)
        {
            float start = 0f;
            for (int i = 0; i < shots.Count; i++)
            {
                float d = Mathf.Max(1e-4f, shots[i].Duration);
                if (t < start + d || i == shots.Count - 1)
                    return IntroCameraMath.Evaluate(shots[i], (t - start) / d, t, scene);
                start += d;
            }
            return default;
        }

        private void ApplyPose(in IntroPose pose)
        {
            _cameraTransform.SetPositionAndRotation(pose.Position, pose.Rotation);
            if (_mainCamera != null) _mainCamera.fieldOfView = pose.Fov;
        }

        // Captions and flashes at intro time t. Timings are the shots' own (seconds after each
        // shot starts); the words are the MODE's, whichever family intro is playing.
        private void DrawOverlay(IReadOnlyList<IntroShot> shots, IntroModeWords words, float t,
                                 float letterbox, float lowerFrom)
        {
            if (_overlay == null) return;
            _overlay.SetLetterbox(letterbox);

            float flash = 0f, start = 0f;
            IntroCue? best0 = null, best1 = null, best2 = null;
            float at0 = 0f, at1 = 0f, at2 = 0f;
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                foreach (var f in shot.Flashes)
                {
                    float lt = t - (start + f.At);
                    if (lt >= 0f && lt <= f.Duration && f.Duration > 0f)
                    {
                        float u = 1f - lt / f.Duration;
                        flash = Mathf.Max(flash, 0.85f * u * u);
                    }
                }
                foreach (var c in shot.Cues)
                {
                    float a = start + c.At;
                    if (t < a || t > a + c.Duration) continue;
                    switch (c.Slot)
                    {
                        case IntroCaptionSlot.Title: best0 = c; at0 = a; break;
                        case IntroCaptionSlot.Tagline: best1 = c; at1 = a; break;
                        default: best2 = c; at2 = a; break;
                    }
                }
                start += Mathf.Max(0f, shot.Duration);
            }
            _overlay.SetFlash(flash);
            Caption(IntroCaptionSlot.Title, best0, at0, words, t);
            Caption(IntroCaptionSlot.Tagline, best1, at1, words, t);
            Caption(IntroCaptionSlot.Objective, best2, at2, words, t);

            float lowerT = t - lowerFrom;
            float lowerAlpha = words != null && lowerT >= 0f ? Mathf.Min(1f, lowerT / 0.22f) : 0f;
            _overlay.SetLowerThird(words?.Title, words?.Pilots, lowerAlpha, lowerT / 0.4f);
        }

        private void Caption(IntroCaptionSlot slot, IntroCue? cue, float at, IntroModeWords words, float t)
        {
            if (cue == null || words == null)
            {
                _overlay.SetCaption(slot, null, 0f, 1f);
                return;
            }
            float lt = t - at;
            float alpha = Mathf.Clamp01(Mathf.Min(lt / 0.22f, (cue.Value.Duration - lt) / 0.25f));
            _overlay.SetCaption(slot, words.For(slot), alpha, lt / 0.4f);
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

        private IEnumerator TransitionToPlayer(Action<float> onProgress = null)
        {
            if (_playerTarget == null)
            {
                onProgress?.Invoke(1f);
                yield break;
            }

            Vector3 startPos = _cameraTransform.position;
            Quaternion startRot = _cameraTransform.rotation;
            float startFov = _mainCamera != null ? _mainCamera.fieldOfView : _playerFov;

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
                if (_mainCamera != null) _mainCamera.fieldOfView = Mathf.Lerp(startFov, _playerFov, t);
                onProgress?.Invoke(t);
                yield return null;
            }
        }

        private void FinishCinematic()
        {
            _isPlaying = false;
            _runningCoroutine = null;

            SetSkipButtonVisible(false);
            ReleaseIntroLook();

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

        // Gives back the player camera's field of view and removes the bars and captions.
        private void ReleaseIntroLook()
        {
            if (_mainCamera) _mainCamera.fieldOfView = _playerFov;
            if (_overlay) Destroy(_overlay.gameObject);
            _overlay = null;
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
                ReleaseIntroLook();

                // `is` is a C# type test and passes for a camera Unity already destroyed: a scene
                // unloading mid-cinematic disables this after the player camera is gone, and the
                // re-enable threw (Bug Ledger E-3455204aad, jtgjones and Shombith). Test it as an Object.
                if (_playerCameraController is CustomCameraController pcc && pcc)
                {
                    pcc.enabled = true;
                    pcc.SnapToTarget();
                }
            }
        }
    }
}
