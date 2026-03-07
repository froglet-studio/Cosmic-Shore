using System;
using System.Collections;
using CosmicShore.App.Systems;
using CosmicShore.App.UI;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Soap;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace CosmicShore.App.UI.MainMenuVesselInteraction
{
    /// <summary>
    /// Always-available feature on the main menu: double-tap center of screen (or gamepad button)
    /// to take manual control of the AI vessel. Press B / tap exit button to return to app shell.
    /// </summary>
    public class MainMenuVesselReentryController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDataSO gameData;

        [Header("UI References")]
        [SerializeField] private ScreenSwitcher screenSwitcher;
        [SerializeField] private GameObject navBar;
        [SerializeField] private GameObject menu;
        [SerializeField] private VesselTutorialUI tutorialUI;

        [Header("Camera")]
        [SerializeField] private float cameraPanDuration = 1.5f;
        [SerializeField] private float cameraReturnDuration = 1.5f;

        [Header("Double-Tap Settings")]
        [SerializeField] private float doubleTapWindow = 0.4f;
        [SerializeField] private float centerRegionFraction = 0.4f;

        private IPlayer player;
        private IVessel vessel;
        private bool isControllingVessel;
        private Coroutine activeCoroutine;

        // Stored menu camera state
        private Vector3 menuCameraPosition;
        private Quaternion menuCameraRotation;

        // Double-tap tracking
        private float lastTapTime;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            // Don't process if tutorial is running or we're in a transition
            if (MainMenuVesselTutorialController.IsActive)
                return;

            if (isControllingVessel)
            {
                // Check for exit: gamepad B or exit button (handled by event)
                if (IsGamepadBPressed())
                    ExitVesselControl();
                return;
            }

            // Detect double-tap to enter
            DetectDoubleTap();
            DetectGamepadEntry();
        }

        private void DetectDoubleTap()
        {
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began)
                    continue;

                if (!IsInCenterRegion(touch.screenPosition))
                    continue;

                if (Time.unscaledTime - lastTapTime < doubleTapWindow)
                {
                    lastTapTime = 0f;
                    EnterVesselControl();
                    return;
                }

                lastTapTime = Time.unscaledTime;
            }
        }

        private void DetectGamepadEntry()
        {
            // Press both sticks to enter vessel control on gamepad
            var gamepad = Gamepad.current;
            if (gamepad == null) return;

            if (gamepad.leftStickButton.wasPressedThisFrame && gamepad.rightStickButton.isPressed)
                EnterVesselControl();
            else if (gamepad.rightStickButton.wasPressedThisFrame && gamepad.leftStickButton.isPressed)
                EnterVesselControl();
        }

        private bool IsInCenterRegion(Vector2 screenPosition)
        {
            float halfRegion = centerRegionFraction * 0.5f;
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;
            float regionWidth = Screen.width * halfRegion;
            float regionHeight = Screen.height * halfRegion;

            return Mathf.Abs(screenPosition.x - centerX) < regionWidth
                && Mathf.Abs(screenPosition.y - centerY) < regionHeight;
        }

        private void EnterVesselControl()
        {
            if (isControllingVessel) return;

            if (gameData.Players == null || gameData.Players.Count == 0)
                return;

            player = gameData.Players[0];
            vessel = player.Vessel;
            if (vessel == null) return;

            if (activeCoroutine != null)
                StopCoroutine(activeCoroutine);

            activeCoroutine = StartCoroutine(EnterVesselControlCoroutine());
        }

        public void ExitVesselControl()
        {
            if (!isControllingVessel) return;

            if (activeCoroutine != null)
                StopCoroutine(activeCoroutine);

            activeCoroutine = StartCoroutine(ExitVesselControlCoroutine());
        }

        private IEnumerator EnterVesselControlCoroutine()
        {
            isControllingVessel = true;

            // Store menu camera state
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                menuCameraPosition = mainCam.transform.position;
                menuCameraRotation = mainCam.transform.rotation;
            }

            // Hide menu UI
            navBar.SetActive(false);
            menu.SetActive(false);

            // Ensure time is running
            PauseSystem.TogglePauseGame(false);

            // Stop AI, enable player input
            vessel.ToggleAIPilot(false);
            player.InputController.SetPause(false);

            // Pan camera behind vessel
            yield return PanCameraBehindVessel();

            // Show exit button
            tutorialUI.ShowExitButton();
            tutorialUI.OnExitRequested += OnExitButtonPressed;
        }

        private IEnumerator ExitVesselControlCoroutine()
        {
            tutorialUI.OnExitRequested -= OnExitButtonPressed;
            tutorialUI.HideAll();

            // Pause player input, re-enable AI
            player.InputController.SetPause(true);
            vessel.ToggleAIPilot(true);

            // Pan camera back to menu
            yield return PanCameraToMenu();

            // Show menu UI
            navBar.SetActive(true);
            menu.SetActive(true);

            isControllingVessel = false;
            activeCoroutine = null;
        }

        private void OnExitButtonPressed()
        {
            ExitVesselControl();
        }

        private IEnumerator PanCameraBehindVessel()
        {
            var cameraManager = CameraManager.Instance;
            if (cameraManager == null) yield break;

            var followTarget = vessel.VesselStatus.CameraFollowTarget;
            cameraManager.SetupGamePlayCameras(followTarget);

            var playerCamCtrl = cameraManager.GetActiveController() as CustomCameraController;
            if (playerCamCtrl == null) yield break;

            playerCamCtrl.enabled = false;
            var camTransform = playerCamCtrl.transform;
            Vector3 startPos = menuCameraPosition;
            Quaternion startRot = menuCameraRotation;

            float elapsed = 0f;
            while (elapsed < cameraPanDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / cameraPanDuration));

                Vector3 behindVessel = followTarget.position + followTarget.rotation * playerCamCtrl.GetFollowOffset();
                Vector3 lookDir = followTarget.position - behindVessel;
                Quaternion targetRot = lookDir.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(lookDir, followTarget.up)
                    : startRot;

                camTransform.position = Vector3.Lerp(startPos, behindVessel, t);
                camTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                yield return null;
            }

            playerCamCtrl.enabled = true;
            playerCamCtrl.SnapToTarget();
        }

        private IEnumerator PanCameraToMenu()
        {
            var cameraManager = CameraManager.Instance;
            if (cameraManager == null) yield break;

            var playerCamCtrl = cameraManager.GetActiveController() as CustomCameraController;
            if (playerCamCtrl == null)
            {
                cameraManager.SetMainMenuCameraActive();
                yield break;
            }

            playerCamCtrl.enabled = false;
            var camTransform = playerCamCtrl.transform;
            Vector3 startPos = camTransform.position;
            Quaternion startRot = camTransform.rotation;

            float elapsed = 0f;
            while (elapsed < cameraReturnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / cameraReturnDuration));

                camTransform.position = Vector3.Lerp(startPos, menuCameraPosition, t);
                camTransform.rotation = Quaternion.Slerp(startRot, menuCameraRotation, t);

                yield return null;
            }

            cameraManager.SetMainMenuCameraActive();
        }

        private bool IsGamepadBPressed()
        {
            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
        }
    }
}
