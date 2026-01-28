using System.Collections;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Controller responsible for orchestrating the end-game cinematic sequence.
    /// Handles flow control and coordinates between different systems.
    /// UI presentation is delegated to EndGameCinematicView.
    /// </summary>
    public class EndGameCinematicController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] SceneCinematicLibrarySO sceneCinematicLibrary;
        [SerializeField] CinematicCameraController cinematicCameraController;
        
        [Header("View")]
        [SerializeField] EndGameCinematicView view;

        private bool _isRunning;
        private Coroutine _runningRoutine;

        void OnEnable()
        {
            if (!gameData) return;
            gameData.OnWinnerCalculated += OnWinnerCalculated;
            
            if (view)
            {
                view.Initialize();
                view.OnContinuePressed += HandleContinuePressed;
            }
        }

        void OnDisable()
        {
            if (!gameData) return;
            gameData.OnWinnerCalculated -= OnWinnerCalculated;

            if (view)
                view.OnContinuePressed -= HandleContinuePressed;

            if (_runningRoutine != null)
            {
                StopCoroutine(_runningRoutine);
                _runningRoutine = null;
            }

            if (view)
                view.Cleanup();

            _isRunning = false;
        }

        void OnWinnerCalculated()
        {
            if (_isRunning) return;
            _isRunning = true;

            var cinematic = ResolveCinematicForThisScene();
            _runningRoutine = StartCoroutine(RunCompleteEndGameSequence(cinematic));
        }

        IEnumerator RunCompleteEndGameSequence(CinematicDefinitionSO cinematic)
        {
            // Phase 1: Victory Lap
            if (cinematic && cinematic.enableVictoryLap)
            {
                yield return StartCoroutine(RunVictoryLap(cinematic));
            }
            
            // Phase 2: AI Control
            if (cinematic && cinematic.setLocalVesselToAI)
            {
                SetLocalVesselAI(true, cinematic.aiCinematicBehavior);
            }
            
            // Phase 3: Camera Sequence
            if (cinematic && cinematic.cameraSetups is { Count: > 0 })
            {
                yield return StartCoroutine(RunCameraSequence(cinematic));
            }
            else
            {
                var delay = cinematic ? cinematic.delayBeforeEndScreen : 0.1f;
                yield return new WaitForSeconds(delay);
            }
            
            // Phase 4: Score Reveal
            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));
            
            // Phase 5: Wait for Continue Button
            if (view)
            {
                view.ShowContinueButton();
                yield return new WaitUntil(() => !view.IsContinueButtonActive());
            }
            
            // Phase 6: Transition Out
            if (view && cinematic)
            {
                view.ShowConnectingPanel();
                yield return new WaitForSeconds(cinematic.connectingPanelDuration);
                ResetGameForNewRound();
            }

            if (view)
                view.HideScoreRevealPanel();

            gameData.InvokeShowGameEndScreen();

            _runningRoutine = null;
            _isRunning = false;
        }

        #region Victory Lap
        IEnumerator RunVictoryLap(CinematicDefinitionSO cinematic)
        {
            var settings = cinematic.victoryLapSettings;
            var localPlayer = gameData.LocalPlayer;
            
            if (localPlayer?.Vessel != null)
            {
                float originalSpeed = localPlayer.Vessel.VesselStatus.Speed;
                localPlayer.Vessel.VesselStatus.Speed *= settings.speedMultiplier;
                
                if (settings.enhanceTrail)
                {
                    EnhanceTrailRenderer(localPlayer.Vessel);
                }
                
                if (cinematic.showVictoryToast && !string.IsNullOrEmpty(cinematic.scoreRevealToastString))
                {
                    view?.ShowVictoryToast(cinematic.scoreRevealToastString, cinematic.toastSettings);
                }
                
                yield return new WaitForSeconds(settings.duration);
                localPlayer.Vessel.VesselStatus.Speed = originalSpeed;
            }
            else
            {
                yield return new WaitForSeconds(settings.duration);
            }
            
            if (settings.fadeLoserTrail && gameData.IsMultiplayerMode)
            {
                FadeLoserTrails();
            }
        }

        void EnhanceTrailRenderer(IVessel vessel)
        {
            // TODO: Implement trail enhancement
            Debug.Log("Trail enhancement - To be implemented");
        }

        void FadeLoserTrails()
        {
            Debug.Log("Loser trail fading - To be implemented");
        }
        #endregion

        #region Camera Sequence
        IEnumerator RunCameraSequence(CinematicDefinitionSO cinematic)
        {
            var localPlayer = gameData.LocalPlayer;
            var mainCamera = Camera.main;

            cinematicCameraController.Initialize(mainCamera, localPlayer.Vessel.Transform);

            for (int i = 0; i < cinematic.cameraSetups.Count; i++)
            {
                var cameraSetup = cinematic.cameraSetups[i];
                cinematicCameraController.StartCameraSetup(cameraSetup);
                yield return new WaitForSeconds(cameraSetup.duration);
                cinematicCameraController.StopCameraSetup();

                if (i >= cinematic.cameraSetups.Count - 1) continue;
                yield return new WaitForSeconds(cinematic.cameraTransitionTime);
            }
            
            Debug.Log("Camera sequence complete");
        }
        #endregion

        #region Score Reveal
        IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            gameData.IsLocalDomainWinner(out DomainStats stats);
            int score = Mathf.Max(0, (int)stats.Score);
            
            string displayText = cinematic.GetCinematicTextForScore(score);
            
            yield return view.PlayScoreRevealAnimation(
                displayText,
                score,
                cinematic.scoreRevealSettings
            );
        }
        #endregion

        #region AI Control
        void SetLocalVesselAI(bool isAI, AICinematicBehaviorType behaviorName = AICinematicBehaviorType.MoveForward)
        {
            var player = gameData.LocalPlayer;
            if (player.InputController)
            {
                player.InputController.enabled = false;
            }

            player.Vessel.ToggleAIPilot(isAI);

            if (!isAI) return;
            
            var cinematicBehavior = player.Vessel.VesselStatus.AICinematicBehavior;
            var aiPilot = player.Vessel.VesselStatus.AIPilot;

            cinematicBehavior.Initialize(player.Vessel.VesselStatus, aiPilot);
            cinematicBehavior.StartCinematicBehavior(behaviorName);
        }
        #endregion

        #region Reset & Transition
        void ResetGameForNewRound()
        {
            var localPlayer = gameData.LocalPlayer;
            localPlayer.Vessel.VesselStatus.BoostMultiplier = 0f;
            
            if (localPlayer.InputController)
            {
                localPlayer.InputController.enabled = true;
            }

            localPlayer.Vessel?.ToggleAIPilot(false);
            gameData.ResetPlayers();
        }

        void HandleContinuePressed()
        {
            // Controller is notified but View handles the UI state
        }
        #endregion

        #region Helpers
        CinematicDefinitionSO ResolveCinematicForThisScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;

            if (sceneCinematicLibrary && sceneCinematicLibrary.TryGet(sceneName, out var fromLibrary))
            {
                Debug.Log($"Found cinematic definition for scene: {sceneName}");
                return fromLibrary;
            }
            
            Debug.LogWarning($"No cinematic definition found for scene: {sceneName}");
            return null;
        }
        #endregion
    }
}