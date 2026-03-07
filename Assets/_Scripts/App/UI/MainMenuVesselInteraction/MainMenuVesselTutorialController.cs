using System;
using System.Collections;
using CosmicShore.App.Systems;
using CosmicShore.App.UI;
using CosmicShore.Events;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Soap;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.App.UI.MainMenuVesselInteraction
{
    /// <summary>
    /// Orchestrates the first-launch vessel tutorial on the main menu.
    /// After initialization, pans camera behind the AI vessel and walks the player
    /// through speed-up, slow-down, and prism-skim steps before returning to the app shell.
    /// </summary>
    public class MainMenuVesselTutorialController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameDataSO gameData;
        [SerializeField] private FTUEProgress ftueProgress;

        [Header("UI References")]
        [SerializeField] private ScreenSwitcher screenSwitcher;
        [SerializeField] private GameObject navBar;
        [SerializeField] private GameObject menu;
        [SerializeField] private VesselTutorialUI tutorialUI;

        [Header("Camera")]
        [SerializeField] private float cameraPanDuration = 2f;
        [SerializeField] private float cameraReturnDuration = 1.5f;

        [Header("Tutorial Thresholds")]
        [SerializeField] private float speedUpXDiffThreshold = 0.7f;
        [SerializeField] private float slowDownXDiffThreshold = 0.3f;
        [SerializeField] private float sustainedInputDuration = 1f;

        /// <summary>
        /// Static flag checked by TutorialFlowController to skip the old Phase1 flow.
        /// </summary>
        public static bool IsActive { get; private set; }

        private IPlayer player;
        private IVessel vessel;
        private Coroutine tutorialCoroutine;

        // Stored menu camera state for return transition
        private Vector3 menuCameraPosition;
        private Quaternion menuCameraRotation;

        private void OnEnable()
        {
            FTUEEventManager.InitializeFTUE += OnInitializeFTUE;
        }

        private void OnDisable()
        {
            FTUEEventManager.InitializeFTUE -= OnInitializeFTUE;
            IsActive = false;
        }

        private void OnInitializeFTUE()
        {
            if (ftueProgress.ftueDebugKey)
                return;

            if (ftueProgress.currentPhase != TutorialPhase.Phase1_Intro)
                return;

            if (gameData.Players == null || gameData.Players.Count == 0)
            {
                CSDebug.LogWarning("[VesselTutorial] No players spawned yet, cannot start tutorial.");
                return;
            }

            player = gameData.Players[0];
            vessel = player.Vessel;

            if (vessel == null)
            {
                CSDebug.LogWarning("[VesselTutorial] Player has no vessel, cannot start tutorial.");
                return;
            }

            IsActive = true;
            tutorialCoroutine = StartCoroutine(RunTutorial());
        }

        private IEnumerator RunTutorial()
        {
            // Keep UI hidden (AppInitializationModal normally shows NavBar/Menu in CloseCoroutine,
            // but we intercept before that completes visually)
            navBar.SetActive(false);
            menu.SetActive(false);
            tutorialUI.HideAll();

            // Store menu camera state before transitioning
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                menuCameraPosition = mainCam.transform.position;
                menuCameraRotation = mainCam.transform.rotation;
            }

            // Ensure time is running
            PauseSystem.TogglePauseGame(false);

            // Short delay for vessel to be fully initialized and AI to generate some trail
            yield return new WaitForSeconds(1f);

            // Stop AI and give control to player
            vessel.ToggleAIPilot(false);
            player.InputController.SetPause(false);

            // Transition camera behind vessel
            yield return PanCameraBehindVessel();

            // Step 1: Speed up
            tutorialUI.ShowPrompt("Spread your controllers apart to speed up");
            yield return WaitForSustainedInput(
                () => player.InputStatus.XDiff > speedUpXDiffThreshold,
                sustainedInputDuration
            );
            tutorialUI.HidePrompt();
            yield return new WaitForSeconds(0.5f);

            // Step 2: Slow down
            tutorialUI.ShowPrompt("Bring your controllers together to slow down");
            yield return WaitForSustainedInput(
                () => player.InputStatus.XDiff < slowDownXDiffThreshold,
                sustainedInputDuration
            );
            tutorialUI.HidePrompt();
            yield return new WaitForSeconds(0.5f);

            // Step 3: Skim a prism
            tutorialUI.ShowPrompt("Skim over a prism to collect boost");
            bool skimDetected = false;
            Action onSkim = () => skimDetected = true;
            SkimmerImpactor.OnPrismSkimmed += onSkim;
            yield return new WaitUntil(() => skimDetected);
            SkimmerImpactor.OnPrismSkimmed -= onSkim;
            tutorialUI.HidePrompt();
            yield return new WaitForSeconds(0.5f);

            // Step 4: Exit prompt
            tutorialUI.ShowPrompt("Press B or tap the exit button to return");
            tutorialUI.ShowExitButton();
            bool exitRequested = false;
            Action onExit = () => exitRequested = true;
            tutorialUI.OnExitRequested += onExit;
            yield return new WaitUntil(() => exitRequested || IsGamepadBPressed());
            tutorialUI.OnExitRequested -= onExit;

            // Exit tutorial
            yield return ExitToAppShell();
        }

        private IEnumerator WaitForSustainedInput(Func<bool> condition, float requiredDuration)
        {
            float sustainedTime = 0f;
            while (sustainedTime < requiredDuration)
            {
                if (condition())
                    sustainedTime += Time.deltaTime;
                else
                    sustainedTime = 0f;

                yield return null;
            }
        }

        private IEnumerator PanCameraBehindVessel()
        {
            var cameraManager = CameraManager.Instance;
            if (cameraManager == null) yield break;

            // Setup gameplay camera targeting the vessel
            var followTarget = vessel.VesselStatus.CameraFollowTarget;
            cameraManager.SetupGamePlayCameras(followTarget);

            // Get the player camera controller
            var playerCamCtrl = cameraManager.GetActiveController() as CustomCameraController;
            if (playerCamCtrl == null) yield break;

            // Disable the controller temporarily so we can animate manually
            playerCamCtrl.enabled = false;

            var camTransform = playerCamCtrl.transform;
            Vector3 startPos = menuCameraPosition;
            Quaternion startRot = menuCameraRotation;

            // Animate to behind-vessel position
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

            // Re-enable the camera controller and snap
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

        private IEnumerator ExitToAppShell()
        {
            tutorialUI.HideAll();

            // Pause player input and re-enable AI
            player.InputController.SetPause(true);
            vessel.ToggleAIPilot(true);

            // Pan camera back to menu
            yield return PanCameraToMenu();

            // Show menu UI
            navBar.SetActive(true);
            menu.SetActive(true);

            // Advance FTUE progress
            ftueProgress.currentPhase = TutorialPhase.Phase2_GameplayTimer;
            ftueProgress.nextIndex = 0;

            IsActive = false;
            CSDebug.Log("[VesselTutorial] Tutorial completed, returned to app shell.");
        }

        private bool IsGamepadBPressed()
        {
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
        }
    }
}
